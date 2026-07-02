using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO — Annotation Hizalama / Düzenleme Op'ları (AnnotationOps) — v10
    ///
    /// Kaynak fikir: github.com/simonmoreau/align-tag (MIT, Simon Moreau).
    /// EGBIMOTO'ya uyarlanırken yeniden tasarlandı:
    ///
    ///   • Leader-SAFE yaklaşım: align-tag, tag yüksekliğini ölçmek için leader'ları
    ///     geçici kaldırıp TransactionGroup.RollBack() ile geri alır. EGBIMOTO'da
    ///     yazma tek RevitWriteScope (rollback yok), bu yüzden leader'lara HİÇ
    ///     dokunmadan TagHeadPosition / TextNote.Coord üzerinden hizalıyoruz.
    ///     Sonuç: etiketlerin BAŞLARI hizalanır (kullanıcının beklediği davranış),
    ///     leader uçları host'a bağlı kalır, hiçbir leader silinmez.
    ///
    ///   • Hizalama view düzleminde yapılır: head dünya noktası view'ın
    ///     RightDirection/UpDirection eksenine izdüşürülür (1D koordinat),
    ///     hizalanır, dünya koordinatına geri taşınır. CropBox gerektirmez.
    ///
    ///   • Revit 2024+ tek API yolu (GetTaggedReferences / SetLeaderElbow).
    ///   • RevitWriteScope (atomic-mode-aware Transaction/SubTransaction).
    ///
    /// Hedef tipler: IndependentTag, TextNote, SpatialElementTag (RoomTag/SpaceTag/AreaTag).
    /// Hepsinde ortak: bir "head/anchor" noktası vardır ve taşınabilir.
    ///
    /// Op listesi:
    ///   align_tags   — head'leri hizala / eşit dağıt
    ///                  (left|right|top|bottom|center|middle|
    ///                   distribute_h|distribute_v)
    ///   arrange_tags — leader'lı IndependentTag'leri view kenarlarına diz +
    ///                  leader çaprazlarını çöz (CropBox gerekir)
    /// </summary>
    public static class AnnotationOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // A01  align_tags
        //
        // input : List<Element>  (tag / text note) — boşsa aktif view'daki tüm
        //         IndependentTag + TextNote + SpatialElementTag toplanır
        // params: mode      String  default="left"
        //                   left | right | top | bottom |
        //                   center | middle |
        //                   distribute_h | distribute_v
        //         view_id   String  opsiyonel — boşsa aktif view
        //
        // output: List<Dict> {element_id, tip, mode, tasindi, durum}
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("align_tags",
            RequiresTransaction = true,
            Description =
                "Tag/yazi elemanlarinin baslarini (head) hizalar veya esit dagitir.\n\n" +
                "params:\n" +
                "  mode     — left | right | top | bottom | center | middle |\n" +
                "             distribute_h | distribute_v   (default: left)\n" +
                "  view_id  — hedef gorunum ID'si (opsiyonel, bossa aktif gorunum)\n\n" +
                "Hizalama view duzleminde, etiket baslarina gore yapilir. Leader'lara\n" +
                "dokunulmaz (uclar host'a bagli kalir). Pinli elemanlar atlanir.\n\n" +
                "Input: List<Element> (tag/textnote) veya bos (aktif view taranir).\n" +
                "Cikti: List<Dictionary> — element_id, tip, mode, tasindi, durum",
            Category = "Annotation")]
        public static List<Dictionary<string, object?>> AlignTags(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var doc       = rctx.Doc;
            var modeStr   = ctx.GetString("mode", "left").ToLowerInvariant();
            var viewIdStr = ctx.GetString("view_id", "");
            var kind      = ParseMode(modeStr);

            View? targetView = ResolveView(rctx, viewIdStr);
            if (targetView == null)
            {
                ctx.Log("  align_tags: Aktif/hedef gorunum bulunamadi → bos");
                return ErrRow("Gorunum bulunamadi.");
            }

            // Eksen vektörleri (view düzlemi)
            XYZ right = targetView.RightDirection;
            XYZ up    = targetView.UpDirection;

            // Elemanları topla
            var elements = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            if (elements.Count == 0)
            {
                elements = new FilteredElementCollector(doc, targetView.Id)
                    .WhereElementIsNotElementType()
                    .Where(e => e is IndependentTag or TextNote
                                || e.GetType().IsSubclassOf(typeof(SpatialElementTag)))
                    .ToList();
            }

            // Head'i alınabilen, pinsiz elemanlar
            var items = new List<HeadItem>();
            foreach (var e in elements)
            {
                var hi = HeadItem.TryBuild(e, right, up);
                if (hi != null) items.Add(hi);
            }

            if (items.Count < 2)
            {
                ctx.Log($"  align_tags: hizalanacak en az 2 uygun eleman gerekir (gelen: {items.Count})");
                return ErrRow($"En az 2 uygun eleman gerekir (gelen: {items.Count}).");
            }

            ctx.Log($"  align_tags: mode='{modeStr}', {items.Count} eleman, view='{targetView.Name}'");

            using var scope = new RevitWriteScope(doc, $"Tag Hizala: {modeStr}", rctx.IsAtomicMode);

            // Hedef 1D koordinatı (u=right ekseni, v=up ekseni) hesapla
            ComputeTargets(items, kind);

            // Uygula
            var rows = new List<Dictionary<string, object?>>();
            foreach (var it in items)
            {
                string status = "OK";
                bool moved = false;
                if (it.Element.Pinned)
                {
                    status = "PINLI_ATLANDI";
                }
                else
                {
                    try
                    {
                        moved = it.ApplyMove(right, up);
                    }
                    catch (Exception ex)
                    {
                        status = $"HATA: {ex.Message}";
                        ctx.Log($"  align_tags: [{Rv.IdStr(it.Element.Id)}] tasinamadi — {ex.Message}");
                    }
                }

                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.IdStr(it.Element.Id),
                    ["tip"]        = TypeLabel(it.Element),
                    ["mode"]       = modeStr,
                    ["tasindi"]    = moved,
                    ["durum"]      = status,
                });
            }

            scope.Commit();
            int movedCount = rows.Count(r => (bool?)r["tasindi"] == true);
            ctx.Log($"  align_tags: {movedCount}/{rows.Count} eleman tasindi");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // A02  arrange_tags
        //
        // input : yok (aktif/hedef view taranır — leader'lı IndependentTag'ler)
        // params: view_id  String  opsiyonel
        // output: List<Dict> {element_id, taraf, durum}
        // not   : Hedef görünümde CropBox aktif olmalıdır.
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("arrange_tags",
            RequiresTransaction = true,
            Description =
                "Leader'li IndependentTag'leri gorunum kenarlarina otomatik dizer ve\n" +
                "leader cizgilerinin caprazlarini (overlap) konum takasiyla cozer.\n\n" +
                "params:\n" +
                "  view_id  — hedef gorunum ID'si (opsiyonel, bossa aktif gorunum)\n\n" +
                "GEREKSINIM: hedef gorunumde CropBox aktif olmali (kenar referansi).\n" +
                "Tag'ler leader ucunun konumuna gore sol/sag gruba ayrilir, kenarlara\n" +
                "esit aralikla yerlestirilir, 2 gecirste leader caprazlari cozulur.\n\n" +
                "Input: yok (view taranir).\n" +
                "Cikti: List<Dictionary> — element_id, taraf, durum",
            Category = "Annotation")]
        public static List<Dictionary<string, object?>> ArrangeTags(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var doc       = rctx.Doc;
            var viewIdStr = ctx.GetString("view_id", "");

            View? view = ResolveView(rctx, viewIdStr);
            if (view == null)
                return ErrRow("Gorunum bulunamadi.");

            if (!view.CropBoxActive)
            {
                ctx.Log("  arrange_tags: CropBox aktif degil — gorunume crop box uygulayin");
                return ErrRow("Gorunumde CropBox aktif olmali.");
            }

            var tags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .WhereElementIsNotElementType()
                .Cast<IndependentTag>()
                .Where(t => t.HasLeader)
                .ToList();

            if (tags.Count == 0)
            {
                ctx.Log("  arrange_tags: leader'li tag bulunamadi");
                return ErrRow("Leader'li tag bulunamadi.");
            }

            ctx.Log($"  arrange_tags: {tags.Count} leader'li tag, view='{view.Name}'");

            XYZ right = view.RightDirection;
            XYZ up    = view.UpDirection;

            using var scope = new RevitWriteScope(doc, "Tag Duzenle (Arrange)", rctx.IsAtomicMode);

            // Leader uçlarını serbest bırak (free end → uç host'a sabitlenir, head taşınabilir)
            foreach (var tag in tags)
            {
                try
                {
                    tag.LeaderEndCondition = LeaderEndCondition.Free;
                    var rf = tag.GetTaggedReferences().FirstOrDefault();
                    if (rf != null) tag.SetLeaderElbow(rf, tag.TagHeadPosition);
                }
                catch { /* tag bağlı değilse atla */ }
            }
            doc.Regenerate();

            // Sol/sağ grupla
            var left   = new List<TagLeaderInfo>();
            var right2 = new List<TagLeaderInfo>();
            foreach (var tag in tags)
            {
                var info = TagLeaderInfo.Build(tag, doc, view, right, up);
                if (info == null) continue;
                (info.Side == Side.Left ? left : right2).Add(info);
            }

            // Yerleşim noktaları (view kenarları)
            var leftPts  = CreateSidePoints(view, left,   Side.Left,  right, up);
            var rightPts = CreateSidePoints(view, right2, Side.Right, right, up);

            // Leader ucu V-koordinatına göre sırala
            left   = left.OrderBy(x => x.LeaderEndV).ToList();
            right2 = right2.OrderBy(x => x.LeaderEndV).ToList();

            PlaceAndUncross(leftPts,  left);
            PlaceAndUncross(rightPts, right2);

            var rows = new List<Dictionary<string, object?>>();
            foreach (var info in left.Concat(right2))
            {
                string status = "OK";
                try { info.UpdateTagPosition(right, up); }
                catch (Exception ex) { status = $"HATA: {ex.Message}"; }

                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.IdStr(info.Tag.Id),
                    ["taraf"]      = info.Side == Side.Left ? "Sol" : "Sag",
                    ["durum"]      = status,
                });
            }

            scope.Commit();
            ctx.Log($"  arrange_tags: {rows.Count} tag yerlestirildi (sol: {left.Count}, sag: {right2.Count})");
            return rows;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Hizalama çekirdeği — head 1D koordinatları (u,v) üzerinden
        // ═════════════════════════════════════════════════════════════════════

        private static void ComputeTargets(List<HeadItem> items, AlignKind kind)
        {
            switch (kind)
            {
                case AlignKind.Left:
                {
                    double u = items.Min(x => x.U);
                    foreach (var it in items) it.TargetU = u;
                    break;
                }
                case AlignKind.Right:
                {
                    double u = items.Max(x => x.U);
                    foreach (var it in items) it.TargetU = u;
                    break;
                }
                case AlignKind.Top:
                {
                    double v = items.Max(x => x.V);
                    foreach (var it in items) it.TargetV = v;
                    break;
                }
                case AlignKind.Bottom:
                {
                    double v = items.Min(x => x.V);
                    foreach (var it in items) it.TargetV = v;
                    break;
                }
                case AlignKind.Center: // ortak dikey eksen → U ortalaması (min/max ortası)
                {
                    double u = (items.Max(x => x.U) + items.Min(x => x.U)) / 2.0;
                    foreach (var it in items) it.TargetU = u;
                    break;
                }
                case AlignKind.Middle: // ortak yatay eksen → V ortalaması
                {
                    double v = (items.Max(x => x.V) + items.Min(x => x.V)) / 2.0;
                    foreach (var it in items) it.TargetV = v;
                    break;
                }
                case AlignKind.DistributeV:
                {
                    var sorted = items.OrderBy(x => x.V).ToList();
                    double lo = sorted.First().V, hi = sorted.Last().V;
                    double step = sorted.Count > 1 ? (hi - lo) / (sorted.Count - 1) : 0;
                    for (int i = 0; i < sorted.Count; i++)
                        sorted[i].TargetV = lo + i * step;
                    break;
                }
                case AlignKind.DistributeH:
                {
                    var sorted = items.OrderBy(x => x.U).ToList();
                    double lo = sorted.First().U, hi = sorted.Last().U;
                    double step = sorted.Count > 1 ? (hi - lo) / (sorted.Count - 1) : 0;
                    for (int i = 0; i < sorted.Count; i++)
                        sorted[i].TargetU = lo + i * step;
                    break;
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Arrange yardımcıları
        // ═════════════════════════════════════════════════════════════════════

        private static void PlaceAndUncross(List<XYZ> points, List<TagLeaderInfo> tags)
        {
            foreach (var t in tags)
            {
                if (points.Count == 0) break;
                var near = NearestPoint(points, t.TagCenter);
                t.TagCenter = near;
                points.Remove(near);
            }
            Uncross(tags);
            Uncross(tags);
        }

        private static void Uncross(List<TagLeaderInfo> tags)
        {
            for (int i = 0; i < tags.Count; i++)
                for (int j = 0; j < tags.Count; j++)
                {
                    if (i == j) continue;
                    var a = tags[i]; var b = tags[j];
                    if (a.BaseLine == null || a.EndLine == null ||
                        b.BaseLine == null || b.EndLine == null) continue;

                    if (a.BaseLine.Intersect(b.BaseLine, CurveIntersectResultOption.Simple).Result != SetComparisonResult.Disjoint
                       || a.BaseLine.Intersect(b.EndLine, CurveIntersectResultOption.Simple).Result != SetComparisonResult.Disjoint
                       || a.EndLine.Intersect(b.BaseLine, CurveIntersectResultOption.Simple).Result != SetComparisonResult.Disjoint
                       || a.EndLine.Intersect(b.EndLine, CurveIntersectResultOption.Simple).Result != SetComparisonResult.Disjoint)
                    {
                        (a.TagCenter, b.TagCenter) = (b.TagCenter, a.TagCenter);
                    }
                }
        }

        private static XYZ NearestPoint(List<XYZ> points, XYZ basePt)
        {
            XYZ best = points[0];
            double bestD = basePt.DistanceTo(best);
            foreach (var p in points)
            {
                double d = basePt.DistanceTo(p);
                if (d < bestD) { best = p; bestD = d; }
            }
            return best;
        }

        private static List<XYZ> CreateSidePoints(View view, List<TagLeaderInfo> tags,
                                                  Side side, XYZ right, XYZ up)
        {
            var pts = new List<XYZ>();
            if (tags.Count == 0) return pts;

            BoundingBoxXYZ bbox = view.CropBox;
            Transform t = bbox.Transform;

            double tagH = tags.Max(x => x.TagHeight);
            double step = Math.Max(tagH * 1.2, 1e-3);
            double height = bbox.Max.Y - bbox.Min.Y;
            int max = (int)Math.Round(height / step);

            // CropBox köşeleri lokal; dünya noktasına Transform ile taşı
            double baseX = side == Side.Left ? bbox.Min.X : bbox.Max.X;

            for (int i = max * 2; i > 0; i--)
            {
                XYZ local = new XYZ(baseX, bbox.Min.Y + step * i, 0);
                pts.Add(t.OfPoint(local));
            }
            return pts;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Ortak yardımcılar
        // ═════════════════════════════════════════════════════════════════════

        private static View? ResolveView(RevitOpContext rctx, string viewIdStr)
        {
            if (!string.IsNullOrEmpty(viewIdStr) && long.TryParse(viewIdStr, out long vid))
            {
                if (rctx.Doc.GetElement(Rv.MakeElementId(vid)) is View v) return v;
            }
            return rctx.UiDoc?.ActiveView ?? rctx.Doc.ActiveView;
        }

        private static AlignKind ParseMode(string m) => m switch
        {
            "left"             => AlignKind.Left,
            "right"            => AlignKind.Right,
            "top" or "up"      => AlignKind.Top,
            "bottom" or "down" => AlignKind.Bottom,
            "center"           => AlignKind.Center,
            "middle"           => AlignKind.Middle,
            "distribute_h" or "horizontally" => AlignKind.DistributeH,
            "distribute_v" or "vertically"   => AlignKind.DistributeV,
            _                  => AlignKind.Left,
        };

        private static string TypeLabel(Element e) => e switch
        {
            IndependentTag => "Tag",
            TextNote       => "Yazi",
            RoomTag        => "Oda Etiketi",
            SpaceTag       => "Mahal Etiketi",
            AreaTag        => "Alan Etiketi",
            _ when e.GetType().IsSubclassOf(typeof(SpatialElementTag)) => "Mekan Etiketi",
            _              => e.Category?.Name ?? "Eleman",
        };

        private static List<Dictionary<string, object?>> ErrRow(string msg)
            => new() { new() { ["durum"] = "HATA", ["mesaj"] = msg } };

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit baglami gerektirir.");

        // ═════════════════════════════════════════════════════════════════════
        //  İç tipler
        // ═════════════════════════════════════════════════════════════════════

        private enum AlignKind
        {
            Left, Right, Top, Bottom, Center, Middle, DistributeH, DistributeV
        }

        private enum Side { Left, Right }

        /// <summary>
        /// Hizalanabilir bir annotation'ın "head" noktası ve view düzlemindeki
        /// 1D koordinatları. Leader'a DOKUNULMAZ — yalnız head/Coord taşınır.
        /// </summary>
        private sealed class HeadItem
        {
            public Element Element = null!;
            public XYZ Head = null!;       // dünya koordinatı
            public double U, V;            // view düzlemi izdüşümleri (right·, up·)
            public double TargetU, TargetV;

            private HeadItem() { }

            public static HeadItem? TryBuild(Element e, XYZ right, XYZ up)
            {
                XYZ? head = e switch
                {
                    IndependentTag tag      => tag.TagHeadPosition,
                    TextNote note           => note.Coord,
                    SpatialElementTag setag => setag.TagHeadPosition,
                    _                       => null,
                };
                if (head == null) return null;

                var it = new HeadItem { Element = e, Head = head };
                it.U = right.DotProduct(head);
                it.V = up.DotProduct(head);
                it.TargetU = it.U;
                it.TargetV = it.V;
                return it;
            }

            /// <summary>Hedef (u,v) ile mevcut (u,v) farkını dünya vektörüne çevirip taşır.</summary>
            public bool ApplyMove(XYZ right, XYZ up)
            {
                double du = TargetU - U;
                double dv = TargetV - V;
                if (Math.Abs(du) < 1e-9 && Math.Abs(dv) < 1e-9) return false;

                XYZ worldVec = right.Multiply(du) + up.Multiply(dv);
                XYZ newHead = Head + worldVec;

                switch (Element)
                {
                    case IndependentTag tag:
                        // Free leader ise uç host'a bağlı kalır; head taşınır.
                        tag.TagHeadPosition = newHead;
                        break;
                    case TextNote note:
                        // Coord taşınır; serbest leader uçları yerinde kalır (Revit korur).
                        note.Coord = newHead;
                        break;
                    case SpatialElementTag setag:
                        setag.TagHeadPosition = newHead;
                        break;
                    default:
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Arrange için tek bir leader'lı tag'in geometrik durumu (view düzleminde).
        /// align-tag TagLeader'dan uyarlandı (Revit 2024+ API).
        /// Çaprazlık testi (u,v) düzleminde z=0 Line'lar ile yapılır; konum güncellemesi
        /// dünya koordinatında.
        /// </summary>
        private sealed class TagLeaderInfo
        {
            public IndependentTag Tag = null!;
            public Side Side;
            public double TagHeight, TagWidth;
            public double LeaderEndV;        // sıralama için up-izdüşümü
            public Line? BaseLine, EndLine;  // (u,v) düzleminde, z=0

            private Document _doc = null!;
            private XYZ _right = null!, _up = null!;
            private XYZ _leaderEnd = null!;  // dünya
            private XYZ _center = null!;     // dünya (tag merkezi ~ head)
            private XYZ _elbow = null!;      // dünya

            public XYZ TagCenter
            {
                get => _center;
                set { _center = value; UpdateLeader(); }
            }

            public static TagLeaderInfo? Build(IndependentTag tag, Document doc,
                                               View view, XYZ right, XYZ up)
            {
                try
                {
                    var info = new TagLeaderInfo { Tag = tag, _doc = doc, _right = right, _up = up };

                    var tagged = GetTaggedElement(doc, tag);
                    XYZ cropCenterWorld = view.CropBox.Transform.OfPoint(
                        (view.CropBox.Max + view.CropBox.Min) / 2.0);

                    info._leaderEnd = tagged != null
                        ? GetElementCenter(tagged, view)
                        : cropCenterWorld;

                    // Taraf: leader ucu view merkezinin solunda mı? (U ekseninde)
                    double vcU  = right.DotProduct(cropCenterWorld);
                    double endU = right.DotProduct(info._leaderEnd);
                    info.Side = vcU > endU ? Side.Left : Side.Right;
                    info.LeaderEndV = up.DotProduct(info._leaderEnd);

                    // Tag boyutları (view bbox → u/v genişlik-yükseklik)
                    var bb = tag.get_BoundingBox(view);
                    if (bb == null) return null;
                    double uMin = right.DotProduct(bb.Min), uMax = right.DotProduct(bb.Max);
                    double vMin = up.DotProduct(bb.Min),    vMax = up.DotProduct(bb.Max);
                    info.TagWidth  = Math.Abs(uMax - uMin);
                    info.TagHeight = Math.Abs(vMax - vMin);

                    info._center = tag.TagHeadPosition;
                    info.UpdateLeader();
                    return info;
                }
                catch { return null; }
            }

            private void UpdateLeader()
            {
                // (u,v) koordinatları
                double cU = _right.DotProduct(_center),    cV = _up.DotProduct(_center);
                double eU = _right.DotProduct(_leaderEnd), eV = _up.DotProduct(_leaderEnd);

                // 45° kırık dirsek (elbow) — align-tag mantığı
                double abU = eU - cU, abV = eV - cV;
                double prod = abU * abV;
                double mult = Math.Abs(prod) < 1e-12 ? 1 : prod / Math.Abs(prod);
                double elbowU = cU + (abU - abV * Math.Tan(mult * Math.PI / 4));
                double elbowV = cV;

                // Dünya elbow: center'ın düzlem-dışı bileşeni + (elbowU,elbowV)
                XYZ perp = _center - _right.Multiply(cU) - _up.Multiply(cV);
                _elbow = perp + _right.Multiply(elbowU) + _up.Multiply(elbowV);

                XYZ endPlane    = new XYZ(eU, eV, 0);
                XYZ centerPlane = new XYZ(cU, cV, 0);
                XYZ elbowPlane  = new XYZ(elbowU, elbowV, 0);

                double tol = _doc.Application.ShortCurveTolerance;
                EndLine  = endPlane.DistanceTo(elbowPlane) > tol
                    ? Line.CreateBound(endPlane, elbowPlane)
                    : null;
                BaseLine = elbowPlane.DistanceTo(centerPlane) > tol
                    ? Line.CreateBound(elbowPlane, centerPlane)
                    : null;
            }

            public void UpdateTagPosition(XYZ right, XYZ up)
            {
                Tag.LeaderEndCondition = LeaderEndCondition.Free;

                double half = Math.Abs(TagWidth) * 0.5 + 0.1;
                double signU = Side == Side.Left ? -1 : 1;
                XYZ edge = right.Multiply(signU * half);

                Tag.TagHeadPosition = _center + edge;

                var rf = Tag.GetTaggedReferences().FirstOrDefault();
                if (rf != null) Tag.SetLeaderElbow(rf, _elbow);
            }

            public static Element? GetTaggedElement(Document doc, IndependentTag tag)
            {
                try
                {
                    LinkElementId? linkId = tag.GetTaggedElementIds().FirstOrDefault();
                    if (linkId == null) return null;

                    if (linkId.HostElementId == ElementId.InvalidElementId)
                    {
                        if (doc.GetElement(linkId.LinkInstanceId) is RevitLinkInstance li)
                        {
                            var ldoc = li.GetLinkDocument();
                            return ldoc?.GetElement(linkId.LinkedElementId);
                        }
                        return null;
                    }
                    return doc.GetElement(linkId.HostElementId);
                }
                catch { return null; }
            }

            public static XYZ GetElementCenter(Element tagged, View view)
            {
                var bb = tagged.get_BoundingBox(view);
                if (bb != null) return (bb.Max + bb.Min) / 2.0;
                return view.CropBox.Transform.OfPoint(
                    (view.CropBox.Max + view.CropBox.Min) / 2.0);
            }
        }
    }
}
