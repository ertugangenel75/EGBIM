using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Addin.Reports;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Kalıp hesaplama op'ları — egkalip_v26 Python mantığından C# portu.
    ///
    /// v2 düzeltmeleri:
    ///   - IntersectingElements: ElementIntersectsElementFilter (gerçek geometri)
    ///     + BoundingBoxIntersectsFilter (yedek) + manuel bbox (son çare)
    ///     egkalip_v26/lib/eg_kalip_utils.py::intersecting_elements() ile birebir
    ///   - KalipReport op'u: HTML rapor üretimi (özet + detay + trace)
    ///   - ExportKalipXlsx op'u: Excel BOQ çıktısı
    /// </summary>
    public static class KalipOps
    {
        private const double Ft2ToM2 = 0.09290304;
        private const double FtToM   = 0.3048;

        private static readonly Dictionary<string, string> DefaultPoz = new()
        {
            ["Structural Columns"]     = "15.180.1002",
            ["Structural Framing"]     = "15.180.1002",
            ["Structural Foundations"] = "15.180.1002",
            ["Floors"]                 = "15.180.1003",
            ["Floors_Edge"]            = "15.180.1003",
            ["Walls"]                  = "15.180.1003",
        };

        private static readonly string[] PozParamCandidates =
            { "TR_CSB_PozNo", "TR_CSB_POZ_NO", "TR_POZ_NO", "Keynote", "Assembly Code", "Type Mark" };

        /// <summary>
        /// FIX: ctx.InputAsOrDefault&lt;List&lt;Dictionary&lt;string,object?&gt;&gt;&gt;() manifest
        /// zincirinde önceki adımın çıktısı pipeline içinde JSON round-trip yaparsa
        /// (örn. System.Text.Json.JsonElement / IDictionary&lt;string,object&gt; biçimine
        /// dönüşürse) sert cast başarısız olup null dönüyordu. Bu da kalip_export_xlsx'te
        /// "Value cannot be null (Parameter 'source')" exception'ına, kalip_report'ta ise
        /// sessiz boş rapora yol açıyordu.
        /// Bu yardımcı, ctx.Input'u esnek biçimde List&lt;Dictionary&lt;string,object?&gt;&gt;'e çevirir.
        /// </summary>
        internal static List<Dictionary<string, object?>> SafeRows(OpContext ctx)
        {
            var rows = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            if (rows != null) return rows;

            var result = new List<Dictionary<string, object?>>();
            if (ctx.Input is not System.Collections.IEnumerable en || ctx.Input is string)
                return result;

            foreach (var item in en)
            {
                if (item == null) continue;

                if (item is Dictionary<string, object?> d1) { result.Add(d1); continue; }

                if (item is IDictionary<string, object> d2)
                {
                    result.Add(d2.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
                    continue;
                }

                if (item is System.Text.Json.JsonElement je &&
                    je.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in je.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.ValueKind switch
                        {
                            System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                            System.Text.Json.JsonValueKind.Number => prop.Value.GetDouble(),
                            System.Text.Json.JsonValueKind.True   => true,
                            System.Text.Json.JsonValueKind.False  => false,
                            _ => prop.Value.ToString()
                        };
                    }
                    result.Add(dict);
                    continue;
                }

                // Son çare: reflection ile public property'leri oku
                var refDict = new Dictionary<string, object?>();
                foreach (var prop in item.GetType().GetProperties())
                {
                    try { refDict[prop.Name] = prop.GetValue(item); } catch { }
                }
                if (refDict.Count > 0) result.Add(refDict);
            }
            return result;
        }

        private static readonly int BicColumn     = (int)BuiltInCategory.OST_StructuralColumns;
        private static readonly int BicBeam       = (int)BuiltInCategory.OST_StructuralFraming;
        private static readonly int BicWall       = (int)BuiltInCategory.OST_Walls;
        private static readonly int BicFloor      = (int)BuiltInCategory.OST_Floors;
        private static readonly int BicFoundation = (int)BuiltInCategory.OST_StructuralFoundation;

        // ════════════════════════════════════════════════════════════════════
        // KALIP_ALL
        // ════════════════════════════════════════════════════════════════════
        [EgOp("kalip_all",
            Description = "Tüm yapısal kategorilerin kalıp alanını tek geçişte hesaplar. " +
                          "ElementIntersectsElementFilter ile gerçek geometri prefilter. " +
                          "params: include_edges (bool, default true)",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> KalipAll(OpContext ctx)
        {
            var rctx    = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Revit bağlamı gerektirir.");
            var incEdge = ctx.GetBool("include_edges", true);

            var columns     = Collect(rctx.Doc, BuiltInCategory.OST_StructuralColumns);
            var beams       = Collect(rctx.Doc, BuiltInCategory.OST_StructuralFraming);
            var walls       = Collect(rctx.Doc, BuiltInCategory.OST_Walls)
                              .Where(IsStructuralWall).ToList();
            var floors      = Collect(rctx.Doc, BuiltInCategory.OST_Floors);
            var foundations = Collect(rctx.Doc, BuiltInCategory.OST_StructuralFoundation);

            ctx.Log($"  Toplanan: kolon={columns.Count} kiriş={beams.Count} " +
                    $"duvar={walls.Count} döşeme={floors.Count} temel={foundations.Count}");

            var all    = columns.Concat(beams).Concat(walls).Concat(floors).Concat(foundations).ToList();
            var result = new List<Dictionary<string, object?>>();
            var traces = new Dictionary<string, List<string>>();
            int zeros  = 0;

            foreach (var el in all)
            {
                var trace = new List<string>();
                try
                {
                    int bic = GetBic(el);
                    string cat = GetCategoryName(bic, el);

                    // Gerçek geometri prefilter — egkalip_v26 intersecting_elements() 
                    var pfBeams   = IntersectingElements(rctx.Doc, el, beams);
                    var pfFloors  = IntersectingElements(rctx.Doc, el, floors);
                    var pfWalls   = IntersectingElements(rctx.Doc, el, walls);
                    var pfColumns = IntersectingElements(rctx.Doc, el, columns);

                    trace.Add($"Prefilter: beams={pfBeams.Count} floors={pfFloors.Count} " +
                              $"walls={pfWalls.Count} cols={pfColumns.Count}");

                    double netFt2  = 0.0;
                    double edgeFt2 = 0.0;
                    string method  = "skip";

                    if      (bic == BicColumn)
                        (netFt2, method) = AreaColumn(el, pfBeams, pfFloors, pfWalls, trace);
                    else if (bic == BicBeam)
                        (netFt2, method) = AreaBeam(el, pfFloors, pfWalls, pfColumns, beams, trace);
                    else if (bic == BicWall)
                        (netFt2, method) = AreaWall(el, pfBeams, pfColumns, trace);
                    else if (bic == BicFloor)
                    {
                        var (_, m, alt, edge) = AreaFloor(el, incEdge, pfColumns, pfBeams, pfWalls, trace);
                        netFt2 = alt; edgeFt2 = edge; method = m;
                    }
                    else if (bic == BicFoundation)
                        (netFt2, method) = AreaFoundation(el, pfWalls, trace);

                    string eid = SafeId(el).ToString();
                    traces[eid] = trace;

                    double netM2  = netFt2  * Ft2ToM2;
                    double edgeM2 = edgeFt2 * Ft2ToM2;

                    if (netM2 > 1e-4)
                        result.Add(BuildRow(rctx.Doc, el, cat, netFt2, netM2, method));
                    else
                        zeros++;

                    if (edgeM2 > 0.001)
                        result.Add(BuildRow(rctx.Doc, el, "Floors_Edge", edgeFt2, edgeM2, "floor_edge"));
                }
                catch (Exception ex)
                {
                    trace.Add($"HATA: {ex.Message}");
                    ctx.Log($"  ⚠ id={SafeId(el)}: {ex.Message}");
                }
            }

            // Trace'leri registry'ye yaz (KalipReport için)
            EgbimotoData.Registry.Set("kalip_traces", traces);

            double grand = result.Sum(r =>
                r.TryGetValue("kalip_m2", out var v) && double.TryParse(v?.ToString(), out var d) ? d : 0);
            ctx.Log($"  kalip_all: {result.Count} satır, {zeros} sıfır, toplam {grand:N2} m²");
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        // KALIP_WRITE_BACK
        // ════════════════════════════════════════════════════════════════════
        [EgOp("kalip_write_back",
            Description = "Kalıp satırlarını Revit parametrelerine yazar: " +
                          "Formwork_Area, TR_KalipAlani, TR_KalipPozNo, TR_KalipPozAdi, " +
                          "TR_KalipBirimFiyat, TR_KalipToplamTutar",
            Category    = "Maliyet")]
        public static Dictionary<string, object?> KalipWriteBack(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Revit bağlamı gerektirir.");
            var rows = SafeRows(ctx);

            var pozData  = EgbimotoData.Registry.Get("poz_data")
                as List<Dictionary<string, object?>> ?? new();
            var pozIndex = pozData.ToDictionary(
                p => p.TryGetValue("poz_no", out var k) ? k?.ToString() ?? "" : "",
                p => p, StringComparer.OrdinalIgnoreCase);

            var rowMap = rows
                .Where(r => r.TryGetValue("kategori", out var c) && c?.ToString() != "Floors_Edge")
                .GroupBy(r => r.TryGetValue("element_id", out var id) ? id?.ToString() ?? "" : "")
                .ToDictionary(g => g.Key, g => g.First());

            int written = 0, skipped = 0;

            using var tx = new Transaction(rctx.Doc, "EG_KALIP_WRITE");
            tx.Start();
            try
            {
                var allEl = new[] {
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_StructuralFoundation
                }.SelectMany(b => Collect(rctx.Doc, b)).ToList();

                foreach (var el in allEl)
                {
                    string eid = SafeId(el).ToString();
                    if (!rowMap.TryGetValue(eid, out var row)) { skipped++; continue; }

                    double aFt2 = row.TryGetValue("area_internal", out var ai) &&
                                  double.TryParse(ai?.ToString(), out var afd) ? afd : 0.0;
                    double aM2  = aFt2 * Ft2ToM2;

                    SetParam(el, "Formwork_Area",     aFt2, aM2);
                    SetParamStr(el, "TR_KalipAlani",  $"{aM2:F2}");

                    // Poz — keynote öncelikli, sonra kategori default
                    string pozNo = ResolvePozNo(rctx.Doc, el);
                    if (string.IsNullOrEmpty(pozNo))
                    {
                        string cat = row.TryGetValue("kategori", out var cv) ? cv?.ToString() ?? "" : "";
                        pozNo = DefaultPoz.TryGetValue(cat, out var dp) ? dp : "15.180.1002";
                    }

                    double birimFiyat = 0.0;
                    string pozAdi = "";
                    if (pozIndex.TryGetValue(pozNo, out var pItem))
                    {
                        pozAdi     = pItem.TryGetValue("tanim",      out var ta) ? ta?.ToString() ?? "" : "";
                        double.TryParse(pItem.TryGetValue("birim_fiyat", out var up) ?
                            up?.ToString() : "0", out birimFiyat);
                    }

                    SetParamStr(el, "TR_KalipPozNo",       pozNo);
                    SetParamStr(el, "TR_KalipPozAdi",      pozAdi);
                    SetParamStr(el, "TR_KalipBirimFiyat",  $"{birimFiyat:F2}");
                    SetParamStr(el, "TR_KalipToplamTutar", $"{aM2 * birimFiyat:F2}");
                    written++;
                }
                tx.Commit();
            }
            catch { tx.RollBack(); throw; }

            ctx.Log($"  kalip_write_back: {written} yazıldı, {skipped} atlandı");
            return new() { ["written"] = written, ["skipped"] = skipped };
        }

        // ════════════════════════════════════════════════════════════════════
        // KALIP_REPORT — HTML rapor üret
        // ════════════════════════════════════════════════════════════════════
        [EgOp("kalip_report",
            Description = "Kalıp hesap sonuçlarını profesyonel HTML raporuna dönüştürür. " +
                          "Özet tablo + element detayları + teknik trace (açılır/kapanır). " +
                          "params: open_browser (bool), out_dir (string)",
            Category    = "Raporlama")]
        public static Dictionary<string, object?> KalipReport(OpContext ctx)
        {
            var rctx      = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Revit bağlamı gerektirir.");
            var rows      = SafeRows(ctx);
            var openBrowser = ctx.GetBool("open_browser", true);
            var outDir    = ctx.GetString("out_dir", "");

            if (rows.Count == 0)
            {
                ctx.Log("  ⚠ kalip_report: satır yok — önceki adımdan (kalip_all) veri gelmedi, rapor atlandı");
                return new() { ["path"] = "", ["rows"] = 0 };
            }

            if (string.IsNullOrEmpty(outDir))
                outDir = System.IO.Path.GetTempPath();

            // Trace'leri registry'den al
            var traces = EgbimotoData.Registry.Get("kalip_traces")
                as Dictionary<string, List<string>> ?? new();

            // Poz verisi
            var pozData  = EgbimotoData.Registry.Get("poz_data")
                as List<Dictionary<string, object?>> ?? new();
            var pozIndex = pozData.ToDictionary(
                p => p.TryGetValue("poz_no", out var k) ? k?.ToString() ?? "" : "",
                p => p, StringComparer.OrdinalIgnoreCase);

            // Proje adı
            string projName = "";
            try { projName = rctx.Doc.Title; } catch { }

            string html = KalipHtmlBuilder.Build(rows, traces, pozIndex, projName);

            string fname = $"EG_Kalip_Raporu_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            string path  = System.IO.Path.Combine(outDir, fname);
            System.IO.File.WriteAllText(path, html, System.Text.Encoding.UTF8);

            if (openBrowser)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = path, UseShellExecute = true }); }
                catch { }
            }

            ctx.Log($"  kalip_report: {path}");
            return new() { ["path"] = path, ["rows"] = rows.Count };
        }

        // ════════════════════════════════════════════════════════════════════
        // KALIP_EXPORT_XLSX
        // ════════════════════════════════════════════════════════════════════
        [EgOp("kalip_export_xlsx",
            Description = "Kalıp hesap sonuçlarını Excel BOQ formatında dışa aktarır. " +
                          "3 sekme: Özet (poz bazlı), Kat Özeti, Eleman Detayı. " +
                          "params: filename (string), out_dir (string)",
            Category    = "Raporlama")]
        public static Dictionary<string, object?> KalipExportXlsx(OpContext ctx)
        {
            var rctx   = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Revit bağlamı gerektirir.");
            var rows   = SafeRows(ctx);
            var fname  = ctx.GetString("filename", "EG_Kalip_BOQ");
            var outDir = ctx.GetString("out_dir", "");

            if (rows.Count == 0)
            {
                ctx.Log("  ⚠ kalip_export_xlsx: satır yok — önceki adımdan (kalip_all) veri gelmedi, export atlandı");
                return new() { ["path"] = "", ["rows"] = 0 };
            }

            if (string.IsNullOrEmpty(outDir))
                outDir = System.IO.Path.GetTempPath();

            var pozData  = EgbimotoData.Registry.Get("poz_data")
                as List<Dictionary<string, object?>> ?? new();
            var pozIndex = pozData.ToDictionary(
                p => p.TryGetValue("poz_no", out var k) ? k?.ToString() ?? "" : "",
                p => p, StringComparer.OrdinalIgnoreCase);

            string path = KalipXlsxBuilder.Build(rows, pozIndex, outDir, fname,
                rctx.Doc.Title);

            ctx.Log($"  kalip_export_xlsx: {path}");
            return new() { ["path"] = path, ["rows"] = rows.Count };
        }

        // ════════════════════════════════════════════════════════════════════
        // HESAP FONKSİYONLARI
        // ════════════════════════════════════════════════════════════════════

        internal static (double net, string method) AreaBeam(
            Element beam, List<Element> floors, List<Element> walls,
            List<Element> columns, List<Element> allBeams, List<string> trace)
        {
            var bb = beam.get_BoundingBox(null);
            if (bb == null) return (0.0, "beam_no_bbox");

            double dx = Abs(bb.Max.X - bb.Min.X);
            double dy = Abs(bb.Max.Y - bb.Min.Y);
            double dz = Abs(bb.Max.Z - bb.Min.Z);
            double h  = dz;
            if (h < 1e-9) return (0.0, "beam_no_h");

            double L    = BeamCurveLength(beam);
            bool   axX  = BeamAxisIsX(beam);
            double b    = axX ? dy : dx;
            if (L < 1e-9 || b < 1e-9) return (0.0, "beam_no_dims");

            double gross = L * (b + 2.0 * h);
            trace.Add($"L={L*FtToM:F3}m b={b*FtToM:F3}m h={h*FtToM:F3}m Brüt={gross*Ft2ToM2:F4}m²");

            // 1. Döşeme yan düşümü
            double yanD = 0.0;
            double slabT = 0.0;
            foreach (var fl in floors)
            {
                var fbb = fl.get_BoundingBox(null);
                if (fbb == null) continue;
                double zOv = Overlap1d(bb.Min.Z, bb.Max.Z, fbb.Min.Z, fbb.Max.Z);
                if (zOv < 1e-9) continue;
                double ft = GetDoubleParam(fl, BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM) ?? 0.0;
                if (ft < 1e-9) continue;
                if (ft > slabT) slabT = ft;
                if (Abs(bb.Max.Z - fbb.Max.Z) <= 0.20)
                    zOv = Math.Min(zOv > 1e-9 ? zOv : ft, ft);
                int sides = FloorSideCount(bb, fbb, axX);
                if (sides <= 0) continue;
                double emb = BeamEmbedLen(bb, fbb, axX);
                if (emb < 1e-9) continue;
                double d = sides * emb * zOv;
                yanD += d;
                trace.Add($"DöşemeYan: sides={sides} emb={emb*FtToM:F3}m z={zOv*FtToM:F3}m d={d*Ft2ToM2:F4}m²");
            }

            // 2. Dış yüz ekleme
            double extAdd = 0.0;
            if (slabT > 1e-9)
            {
                bool lc = false, rc = false;
                foreach (var fl in floors)
                {
                    var fbb = fl.get_BoundingBox(null);
                    if (fbb == null) continue;
                    var (l, r) = FloorSideFlags(bb, fbb, axX);
                    if (l) lc = true;
                    if (r) rc = true;
                }
                if (!lc) extAdd += L * slabT;
                if (!rc) extAdd += L * slabT;
                if (extAdd > 1e-9)
                    trace.Add($"DışYüz: left={!lc} right={!rc} slabT={slabT*FtToM:F3}m ext={extAdd*Ft2ToM2:F4}m²");
            }

            // 3. Alt destek düşümü
            double altD = 0.0;
            foreach (var sup in columns.Concat(walls.Where(IsStructuralWall)))
            {
                var sbb = sup.get_BoundingBox(null);
                if (sbb == null) continue;
                double ov = XyOverlap(sbb, bb);
                if (ov < 1e-9 || Abs(sbb.Max.Z - bb.Min.Z) > 0.066) continue;
                altD += ov;
                trace.Add($"AltDestek: id={SafeId(sup)} ov={ov*Ft2ToM2:F4}m²");
            }

            // 4. Kiriş-kiriş düşümü
            double kkD = 0.0;
            long selfId = SafeId(beam);
            foreach (var other in allBeams)
            {
                if (SafeId(other) == selfId) continue;
                var obb = other.get_BoundingBox(null);
                if (obb == null || !BboxOverlaps(bb, obb, 0.15)) continue;
                bool oAxX = BeamAxisIsX(other);
                double odx = Abs(obb.Max.X - obb.Min.X);
                double ody = Abs(obb.Max.Y - obb.Min.Y);
                double bO  = oAxX ? ody : odx;
                double hO  = Abs(obb.Max.Z - obb.Min.Z);
                double hNet = Math.Max(0.0, hO - slabT);
                if (bO < 1e-9 || hNet < 1e-9) continue;
                kkD += bO * hNet;
                trace.Add($"KirişKiriş: id={SafeId(other)} b={bO*FtToM:F3}m hNet={hNet*FtToM:F3}m d={bO*hNet*Ft2ToM2:F4}m²");
            }

            double net = Math.Max(0.0, gross - yanD + extAdd - altD - kkD);
            trace.Add($"yan={yanD*Ft2ToM2:F4} ext={extAdd*Ft2ToM2:F4} alt={altD*Ft2ToM2:F4} kk={kkD*Ft2ToM2:F4} NET={net*Ft2ToM2:F4}m²");
            return (net, "beam_v26");
        }

        internal static (double net, string method) AreaColumn(
            Element col, List<Element> beams, List<Element> floors,
            List<Element> walls, List<string> trace)
        {
            var bb = col.get_BoundingBox(null);
            if (bb == null) return (0.0, "col_no_bbox");
            double dx = Abs(bb.Max.X - bb.Min.X);
            double dy = Abs(bb.Max.Y - bb.Min.Y);
            if (dx < 1e-9 || dy < 1e-9) return (0.0, "col_no_dims");

            double cevre    = 2.0 * (dx + dy);
            double supportZ = FindSupportZ(bb, beams, floors);
            supportZ = Math.Max(bb.Min.Z, Math.Min(bb.Max.Z, supportZ));
            double hMain = Math.Max(0.0, supportZ - bb.Min.Z);

            trace.Add($"dx={dx*FtToM:F3}m dy={dy*FtToM:F3}m çevre={cevre*FtToM:F3}m H_main={hMain*FtToM:F3}m");

            // Duvar temas düşümü (ana gövde)
            double wallD = 0.0;
            foreach (char f in "WESN")
            {
                double maxW = walls.Where(IsStructuralWall).Select(w =>
                {
                    var wbb = w.get_BoundingBox(null);
                    if (wbb == null) return 0.0;
                    if (Overlap1d(wbb.Min.Z, wbb.Max.Z, bb.Min.Z, supportZ) < 1e-9) return 0.0;
                    return WallContactWidth(f, bb, wbb);
                }).DefaultIfEmpty(0.0).Max();
                if (maxW > 1e-9)
                {
                    wallD += maxW * hMain;
                    trace.Add($"DuvarTemas: yüz={f} w={maxW*FtToM:F3}m d={maxW*hMain*Ft2ToM2:F4}m²");
                }
            }
            double aMain = Math.Max(0.0, cevre * hMain - wallD);

            // Kolon başı
            double aHead = 0.0;
            if (beams.Count > 0 && bb.Max.Z > supportZ + 1e-9)
            {
                double headH = bb.Max.Z - supportZ;
                var fBeamB   = new Dictionary<char, double> { ['W']=0,['E']=0,['S']=0,['N']=0 };
                var fFloorT  = new Dictionary<char, double> { ['W']=0,['E']=0,['S']=0,['N']=0 };

                foreach (var bm in beams)
                {
                    var bbb = bm.get_BoundingBox(null);
                    if (bbb == null) continue;
                    bool axX = BeamAxisIsX(bm);
                    double bW = axX ? Abs(bbb.Max.Y-bbb.Min.Y) : Abs(bbb.Max.X-bbb.Min.X);
                    foreach (char f in "WESN")
                        if (FaceCoveredByBb(f, bb, bbb, supportZ, bb.Max.Z) && bW > fBeamB[f])
                            fBeamB[f] = bW;
                }
                foreach (var fl in floors)
                {
                    var fbb = fl.get_BoundingBox(null);
                    if (fbb == null) continue;
                    double T = GetDoubleParam(fl, BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM) ?? 0.0;
                    if (T < 1e-9) continue;
                    foreach (char f in "WESN")
                        if (FaceCoveredByBb(f, bb, fbb, supportZ, bb.Max.Z, 0.066) && T > fFloorT[f])
                            fFloorT[f] = T;
                }

                foreach (char f in "WESN")
                {
                    double dim  = (f == 'W' || f == 'E') ? dy : dx;
                    double bB   = fBeamB[f];
                    double tF   = fFloorT[f];
                    double net  = Math.Max(0.0, dim*headH - bB*headH - tF*Math.Max(0,dim-bB));
                    aHead += net;
                    trace.Add($"Yüz {f}: dim={dim*FtToM:F3}m bBeam={bB*FtToM:F3}m flT={tF*FtToM:F3}m net={net*Ft2ToM2:F4}m²");
                }
                trace.Add($"headH={headH*FtToM:F3}m A_head={aHead*Ft2ToM2:F4}m²");
            }

            double total = Math.Max(0.0, aMain + aHead);
            trace.Add($"A_main={aMain*Ft2ToM2:F4}m² A_head={aHead*Ft2ToM2:F4}m² NET={total*Ft2ToM2:F4}m²");
            return (total, "col_v26");
        }

        internal static (double net, string method) AreaWall(
            Element wall, List<Element> beams, List<Element> columns, List<string> trace)
        {
            if (!IsStructuralWall(wall)) return (0.0, "wall_skip");
            var bb = wall.get_BoundingBox(null);
            if (bb == null) return (0.0, "wall_no_bbox");
            double L  = WallLength(wall);
            double dz = Abs(bb.Max.Z - bb.Min.Z);
            if (L < 1e-9 || dz < 1e-9) return (0.0, "wall_no_dims");

            double gross = 2.0 * L * dz;
            trace.Add($"L={L*FtToM:F3}m H={dz*FtToM:F3}m Brüt={gross*Ft2ToM2:F4}m²");

            double bD = beams.Select(b =>
            {
                var bbb = b.get_BoundingBox(null);
                if (bbb == null || !BboxOverlaps(bb, bbb, 0.1)) return 0.0;
                bool axX = BeamAxisIsX(b);
                double bW = axX ? Abs(bbb.Max.Y-bbb.Min.Y) : Abs(bbb.Max.X-bbb.Min.X);
                return bW * Abs(bbb.Max.Z - bbb.Min.Z);
            }).Sum();

            double cD = columns.Select(c =>
            {
                var cbb = c.get_BoundingBox(null);
                if (cbb == null || !BboxOverlaps(bb, cbb, 0.1)) return 0.0;
                return Abs(cbb.Max.X-cbb.Min.X) * Abs(cbb.Max.Y-cbb.Min.Y);
            }).Sum();

            double net = Math.Max(0.0, gross - bD - cD);
            trace.Add($"beam_d={bD*Ft2ToM2:F4} col_d={cD*Ft2ToM2:F4} NET={net*Ft2ToM2:F4}m²");
            return (net, "wall_v23");
        }

        internal static (double total, string method, double alt, double edge) AreaFloor(
            Element floor, bool inclEdge,
            List<Element> cols, List<Element> beams,
            List<Element> walls, List<string> trace)
        {
            var ap = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            if (ap == null || !ap.HasValue) return (0,  "floor_no_area", 0, 0);
            double brut = ap.AsDouble();
            var bb = floor.get_BoundingBox(null);
            if (bb == null) return (0, "floor_no_bbox", 0, 0);

            trace.Add($"Brüt={brut*Ft2ToM2:F4}m²");

            double deduct = 0;
            foreach (var c in cols)
            {
                var cbb = c.get_BoundingBox(null);
                if (cbb == null || Abs(cbb.Max.Z - bb.Min.Z) > 0.066) continue;
                double ov = XyOverlap(cbb, bb);
                if (ov > 0) { deduct += ov; trace.Add($"Kolon id={SafeId(c)} d={ov*Ft2ToM2:F4}m²"); }
            }
            foreach (var b in beams)
            {
                var bbb = b.get_BoundingBox(null);
                if (bbb == null || Abs(bbb.Max.Z - bb.Max.Z) > 0.066) continue;
                double ov = XyOverlap(bbb, bb);
                if (ov > 0) { deduct += ov; trace.Add($"Kiriş id={SafeId(b)} d={ov*Ft2ToM2:F4}m²"); }
            }
            foreach (var w in walls.Where(IsStructuralWall))
            {
                var wbb = w.get_BoundingBox(null);
                if (wbb == null) continue;
                if (!(wbb.Max.Z >= bb.Min.Z-0.033 && wbb.Min.Z < bb.Max.Z+0.033)) continue;
                double ov = XyOverlap(wbb, bb);
                if (ov > 0) { deduct += ov; trace.Add($"Duvar id={SafeId(w)} d={ov*Ft2ToM2:F4}m²"); }
            }

            deduct = Math.Min(deduct, brut * 0.95);
            double alt = Math.Max(0.0, brut - deduct);

            double edge = 0.0;
            if (inclEdge)
            {
                double T = GetDoubleParam(floor, BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM) ?? 0.0;
                if (T > 1e-9)
                {
                    double perim = 2.0*(Abs(bb.Max.X-bb.Min.X)+Abs(bb.Max.Y-bb.Min.Y));
                    edge = perim * T;
                    trace.Add($"Kenar: çevre={perim*FtToM:F3}m T={T*FtToM:F3}m kenar={edge*Ft2ToM2:F4}m²");
                }
            }

            trace.Add($"deduct={deduct*Ft2ToM2:F4} alt={alt*Ft2ToM2:F4} NET={(alt+edge)*Ft2ToM2:F4}m²");
            string method = edge > 1e-9 ? "floor_v23_edge" : "floor_v23";
            return (alt+edge, method, alt, edge);
        }

        internal static (double net, string method) AreaFoundation(
            Element found, List<Element> walls, List<string> trace)
        {
            var bb = found.get_BoundingBox(null);
            if (bb == null) return (0.0, "found_no_bbox");
            var dims = new[] { Abs(bb.Max.X-bb.Min.X), Abs(bb.Max.Y-bb.Min.Y), Abs(bb.Max.Z-bb.Min.Z) }
                .OrderByDescending(x => x).ToArray();
            double b1 = dims[0], b2 = dims[1], H = dims[2];
            if (H < 1e-9) return (0.0, "found_no_h");

            double gross = 2.0*(b1+b2)*H;
            trace.Add($"b1={b1*FtToM:F3}m b2={b2*FtToM:F3}m H={H*FtToM:F3}m Brüt={gross*Ft2ToM2:F4}m²");

            double wD = walls.Where(IsStructuralWall).Select(w =>
            {
                var wbb = w.get_BoundingBox(null);
                if (wbb == null || !BboxOverlaps(bb, wbb, 0.1)) return 0.0;
                double t  = Math.Min(Abs(wbb.Max.X-wbb.Min.X), Abs(wbb.Max.Y-wbb.Min.Y));
                double zOv= Overlap1d(bb.Min.Z, bb.Max.Z, wbb.Min.Z, wbb.Max.Z);
                return t * zOv;
            }).Sum();

            double net = Math.Max(0.0, gross - wD);
            trace.Add($"wall_d={wD*Ft2ToM2:F4} NET={net*Ft2ToM2:F4}m²");
            return (net, "found_v23");
        }

        // ════════════════════════════════════════════════════════════════════
        // GERÇEK GEOMETRİ PREFILTER — egkalip_v26 intersecting_elements() portu
        // ════════════════════════════════════════════════════════════════════
        /// <summary>
        /// 3 katmanlı prefilter — egkalip_v26/lib/eg_kalip_utils.py::intersecting_elements()
        ///
        /// 1) ElementIntersectsElementFilter → gerçek geometri kesişimi
        /// 2) BoundingBoxIntersectsFilter    → yedek (geometri yüklenemezse)
        /// 3) Manuel BboxOverlaps3d          → son çare
        ///
        /// NOT: FilteredElementCollector(doc, viewId_veya_elemIdList) Revit API'de
        ///      YANLIŞ — ikinci argüman View.Id olmalı. Bunun yerine tüm doküman
        ///      üzerinde filtre çalıştırıp sonuçları kandidat ID seti ile kesiyoruz.
        /// </summary>
        internal static List<Element> IntersectingElements(
            Document doc, Element host, List<Element> candidates)
        {
            if (candidates.Count == 0) return new();

            var candSet = new HashSet<long>(
                candidates.Select(c => SafeId(c)).Where(id => id >= 0));
            if (candSet.Count == 0) return new();

            var hitIds = new HashSet<long>();

            // 1) Gerçek geometri
            try
            {
                var geoFilter = new ElementIntersectsElementFilter(host);
                foreach (var e in new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(geoFilter))
                {
                    long id = SafeId(e);
                    if (candSet.Contains(id)) hitIds.Add(id);
                }
            }
            catch { }

            // 2) BoundingBox (yedek)
            if (hitIds.Count == 0)
            {
                try
                {
                    var hbb = host.get_BoundingBox(null);
                    if (hbb != null)
                    {
                        const double pad = 0.05;
                        var outline = new Outline(
                            new XYZ(hbb.Min.X-pad, hbb.Min.Y-pad, hbb.Min.Z-pad),
                            new XYZ(hbb.Max.X+pad, hbb.Max.Y+pad, hbb.Max.Z+pad));
                        var bbFilter = new BoundingBoxIntersectsFilter(outline);
                        foreach (var e in new FilteredElementCollector(doc)
                            .WhereElementIsNotElementType()
                            .WherePasses(bbFilter))
                        {
                            long id = SafeId(e);
                            if (candSet.Contains(id)) hitIds.Add(id);
                        }
                    }
                }
                catch { }
            }

            // 3) Manuel bbox (son çare)
            if (hitIds.Count == 0)
            {
                var hbb = host.get_BoundingBox(null);
                if (hbb != null)
                {
                    foreach (var c in candidates)
                    {
                        var cbb = c.get_BoundingBox(null);
                        if (cbb != null && BboxOverlaps(hbb, cbb, 0.1))
                            hitIds.Add(SafeId(c));
                    }
                }
            }

            // Orijinal sırayı koru
            return candidates.Where(c => hitIds.Contains(SafeId(c))).ToList();
        }

        // ════════════════════════════════════════════════════════════════════
        // YARDIMCI METOTLAR
        // ════════════════════════════════════════════════════════════════════
        internal static List<Element> Collect(Document doc, BuiltInCategory bic)
        {
            try { return new FilteredElementCollector(doc).OfCategory(bic)
                .WhereElementIsNotElementType().ToList(); }
            catch { return new(); }
        }

        private static double Abs(double v) => Math.Abs(v);

        internal static bool BboxOverlaps(BoundingBoxXYZ a, BoundingBoxXYZ b, double t = 0.1)
            => a.Max.X > b.Min.X-t && a.Min.X < b.Max.X+t &&
               a.Max.Y > b.Min.Y-t && a.Min.Y < b.Max.Y+t &&
               a.Max.Z > b.Min.Z-t && a.Min.Z < b.Max.Z+t;

        internal static double Overlap1d(double a0, double a1, double b0, double b1)
            => Math.Max(0.0, Math.Min(a1,b1) - Math.Max(a0,b0));

        internal static double XyOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            if (a == null || b == null) return 0.0;
            double dx = Overlap1d(a.Min.X, a.Max.X, b.Min.X, b.Max.X);
            double dy = Overlap1d(a.Min.Y, a.Max.Y, b.Min.Y, b.Max.Y);
            return dx > 1e-9 && dy > 1e-9 ? dx*dy : 0.0;
        }

        internal static bool IsStructuralWall(Element w)
        {
            try
            {
                var p = w.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT);
                return p?.AsInteger() == 1;
            }
            catch { return false; }
        }

        internal static long SafeId(Element el)
        {
            // v6: Rv adapter — REVIT2024: .IntegerValue, 2025+: .Value
            try { return Rv.GetId(el.Id); }
            catch { return -1; }
        }

        private static int GetBic(Element el)
        {
            // v6: Rv adapter — REVIT2024: .IntegerValue, 2025+: (int).Value
            return Rv.GetCategoryId(el);
        }

        internal static string GetCategoryName(int bic, Element el)
        {
            if (bic == BicColumn)     return "Structural Columns";
            if (bic == BicBeam)       return "Structural Framing";
            if (bic == BicWall)       return "Walls";
            if (bic == BicFloor)      return "Floors";
            if (bic == BicFoundation) return "Structural Foundations";
            return el.Category?.Name ?? "Unknown";
        }

        private static double? GetDoubleParam(Element el, BuiltInParameter bip)
        {
            try { var p = el.get_Parameter(bip); return p?.HasValue == true ? p.AsDouble() : null; }
            catch { return null; }
        }

        private static bool BeamAxisIsX(Element beam)
        {
            try
            {
                var c = ((LocationCurve)beam.Location).Curve;
                var p0 = c.GetEndPoint(0); var p1 = c.GetEndPoint(1);
                return Abs(p1.X-p0.X) >= Abs(p1.Y-p0.Y);
            }
            catch
            {
                var bb = beam.get_BoundingBox(null);
                return bb == null || Abs(bb.Max.X-bb.Min.X) >= Abs(bb.Max.Y-bb.Min.Y);
            }
        }

        private static double BeamCurveLength(Element beam)
        {
            try { return ((LocationCurve)beam.Location).Curve.Length; }
            catch { }
            var bb = beam.get_BoundingBox(null);
            return bb == null ? 0.0 : Math.Max(Abs(bb.Max.X-bb.Min.X), Abs(bb.Max.Y-bb.Min.Y));
        }

        private static double WallLength(Element wall)
        {
            try { return ((LocationCurve)wall.Location).Curve.Length; }
            catch { }
            var bb = wall.get_BoundingBox(null);
            return bb == null ? 0.0 : Math.Max(Abs(bb.Max.X-bb.Min.X), Abs(bb.Max.Y-bb.Min.Y));
        }

        private static int FloorSideCount(BoundingBoxXYZ bm, BoundingBoxXYZ fl,
            bool axX, double tol = 0.05)
        {
            if (axX)
            {
                int s = fl.Min.Y < bm.Min.Y-tol || Abs(fl.Min.Y-bm.Min.Y) <= tol ? 1 : 0;
                int n = fl.Max.Y > bm.Max.Y+tol || Abs(fl.Max.Y-bm.Max.Y) <= tol ? 1 : 0;
                return s+n;
            }
            int w = fl.Min.X < bm.Min.X-tol || Abs(fl.Min.X-bm.Min.X) <= tol ? 1 : 0;
            int e = fl.Max.X > bm.Max.X+tol || Abs(fl.Max.X-bm.Max.X) <= tol ? 1 : 0;
            return w+e;
        }

        private static (bool, bool) FloorSideFlags(BoundingBoxXYZ bm, BoundingBoxXYZ fl,
            bool axX, double tol = 0.08)
        {
            if (axX) return (fl.Min.Y <= bm.Min.Y+tol, fl.Max.Y >= bm.Max.Y-tol);
            return (fl.Min.X <= bm.Min.X+tol, fl.Max.X >= bm.Max.X-tol);
        }

        private static double BeamEmbedLen(BoundingBoxXYZ bm, BoundingBoxXYZ fl, bool axX)
            => axX ? Overlap1d(bm.Min.X, bm.Max.X, fl.Min.X, fl.Max.X)
                   : Overlap1d(bm.Min.Y, bm.Max.Y, fl.Min.Y, fl.Max.Y);

        private static double FindSupportZ(BoundingBoxXYZ colBb,
            List<Element> beams, List<Element> floors, double tol = 0.15)
        {
            var zB = beams
                .Select(b => b.get_BoundingBox(null))
                .Where(b => b != null && XyOverlap(colBb, b) > 1e-9 &&
                            b.Min.Z >= colBb.Min.Z-tol && b.Min.Z <= colBb.Max.Z+tol)
                .Select(b => b!.Min.Z).ToList();
            if (zB.Count > 0) return zB.Min();

            var zF = floors
                .Select(f => f.get_BoundingBox(null))
                .Where(f => f != null && XyOverlap(colBb, f) > 1e-9 &&
                            f.Min.Z >= colBb.Min.Z-tol && f.Min.Z <= colBb.Max.Z+tol)
                .Select(f => f!.Min.Z).ToList();
            return zF.Count > 0 ? zF.Min() : colBb.Max.Z;
        }

        private static double WallContactWidth(char face, BoundingBoxXYZ c, BoundingBoxXYZ w, double tol=0.05)
            => face switch {
                'W' => Abs(w.Max.X-c.Min.X)<=tol ? Overlap1d(w.Min.Y,w.Max.Y,c.Min.Y,c.Max.Y) : 0,
                'E' => Abs(w.Min.X-c.Max.X)<=tol ? Overlap1d(w.Min.Y,w.Max.Y,c.Min.Y,c.Max.Y) : 0,
                'S' => Abs(w.Max.Y-c.Min.Y)<=tol ? Overlap1d(w.Min.X,w.Max.X,c.Min.X,c.Max.X) : 0,
                'N' => Abs(w.Min.Y-c.Max.Y)<=tol ? Overlap1d(w.Min.X,w.Max.X,c.Min.X,c.Max.X) : 0,
                _   => 0
            };

        private static bool FaceCoveredByBb(char face, BoundingBoxXYZ col, BoundingBoxXYZ other,
            double z0, double z1, double tol = 0.05)
        {
            if (Overlap1d(other.Min.Z, other.Max.Z, z0, z1) < 1e-9) return false;
            return face switch {
                'W' => Abs(other.Max.X-col.Min.X)<=tol && Overlap1d(other.Min.Y,other.Max.Y,col.Min.Y,col.Max.Y)>1e-9,
                'E' => Abs(other.Min.X-col.Max.X)<=tol && Overlap1d(other.Min.Y,other.Max.Y,col.Min.Y,col.Max.Y)>1e-9,
                'S' => Abs(other.Max.Y-col.Min.Y)<=tol && Overlap1d(other.Min.X,other.Max.X,col.Min.X,col.Max.X)>1e-9,
                'N' => Abs(other.Min.Y-col.Max.Y)<=tol && Overlap1d(other.Min.X,other.Max.X,col.Min.X,col.Max.X)>1e-9,
                _   => false
            };
        }

        private static string ResolvePozNo(Document doc, Element el)
        {
            var rg = new Regex(@"\d{2}\.\d{3}\.\d{4}");
            foreach (var pname in PozParamCandidates)
            {
                foreach (var target in new Element[] { el, doc.GetElement(el.GetTypeId()) })
                {
                    if (target == null) continue;
                    try
                    {
                        var p = target.LookupParameter(pname);
                        string? v = p?.AsString() ?? p?.AsValueString();
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            var m = rg.Match(v!);
                            if (m.Success) return m.Value;
                        }
                    }
                    catch { }
                }
            }
            return "";
        }

        private static void SetParam(Element el, string pname, double ftVal, double mVal)
        {
            try
            {
                var p = el.LookupParameter(pname);
                if (p == null || p.IsReadOnly) return;
                if (p.StorageType == StorageType.Double) p.Set(ftVal);
                else if (p.StorageType == StorageType.String) p.Set($"{mVal:F2}");
            }
            catch { }
        }

        private static void SetParamStr(Element el, string pname, string value)
        {
            try
            {
                var p = el.LookupParameter(pname);
                if (p == null || p.IsReadOnly) return;
                if (p.StorageType == StorageType.String) p.Set(value);
                else if (p.StorageType == StorageType.Double &&
                         double.TryParse(value, out var d)) p.Set(d);
            }
            catch { }
        }

        internal static Dictionary<string, object?> BuildRow(
            Document doc, Element el, string cat,
            double aFt2, double aM2, string method)
        {
            string pozNo = ResolvePozNo(doc, el);
            if (string.IsNullOrEmpty(pozNo))
                DefaultPoz.TryGetValue(cat, out pozNo!);
            pozNo ??= "15.180.1002";

            string tipName = "", levelName = "";
            try
            {
                var typ = doc.GetElement(el.GetTypeId()) as ElementType;
                if (typ != null) tipName = $"{typ.FamilyName} : {typ.Name}";
            }
            catch { }
            try
            {
                var lvl = doc.GetElement(el.LevelId) as Level;
                if (lvl != null) levelName = lvl.Name;
            }
            catch { }

            return new Dictionary<string, object?>
            {
                ["element_id"]    = SafeId(el),
                ["kategori"]      = cat,
                ["tip"]           = tipName,
                ["kat"]           = levelName,
                ["kalip_m2"]      = Math.Round(aM2, 3),
                ["area_internal"] = aFt2,
                ["method"]        = method,
                ["poz_no"]        = pozNo,
            };
        }
    }
}
