using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// v12 — Otomatik ölçülendirme. PCH_Magic_Dims (Katarzyna Lipka-Sidor)
    /// eklentisindeki "farklı kotlardaki elemanları 2D'ye düzleştirip ölçülendirme"
    /// ve "en yakın grid'e / ardışık ölçü" mantığının kategoriden bağımsız portu.
    ///
    /// KASITLI OLARAK KALIBA/DONATIYA ÖZEL DEĞİLDİR: params.categories ile
    /// herhangi bir kategori (kolon, kiriş, boru, kablo tavası, pencere/kapı
    /// merkez noktaları vb.) için kullanılabilir — manifest seviyesinde
    /// kategori seçimi yapılır.
    ///
    /// Sınırlamalar (v1):
    ///   - Yalnızca Location.Curve (Line) olan elemanlar desteklenir
    ///     (LocationPoint elemanlar — kapı/pencere vb. — v2'de eklenebilir).
    ///   - Aktif görünüm Plan/Section/Elevation/CeilingPlan olmalı.
    /// </summary>
    public static class DimensionOps
    {
        private static readonly double FtTol = 0.01;

        // ── Ortak yardımcılar ───────────────────────────────────────────────

        private static List<Element> ResolveLinearElements(OpContext ctx, Document doc)
        {
            List<Element> elems;
            if (ctx.Input is List<Element> inputElems && inputElems.Count > 0)
            {
                elems = inputElems;
            }
            else
            {
                elems = new List<Element>();
                foreach (var catName in ctx.GetStringList("categories"))
                {
                    if (!EgCategoryResolver.TryResolve(catName, out var bic)) continue;
                    try
                    {
                        elems.AddRange(new FilteredElementCollector(doc)
                            .OfCategory(bic)
                            .WhereElementIsNotElementType()
                            .ToElements());
                    }
                    catch { }
                }
            }

            // Yalnızca düz hat (Line) location'a sahip elemanlar
            return elems.Where(e => (e.Location as LocationCurve)?.Curve is Line).ToList();
        }

        private static Line? GetLine(Element e) => (e.Location as LocationCurve)?.Curve as Line;

        private static XYZ Flatten2D(XYZ p, double targetZ) => new(p.X, p.Y, targetZ);

        private static XYZ Normalize2D(XYZ v)
        {
            var v2 = new XYZ(v.X, v.Y, 0.0);
            return v2.GetLength() > 0.0001 ? v2.Normalize() : XYZ.Zero;
        }

        private static bool AreParallel(Line a, Line b, double angleToleranceCos)
        {
            var d1 = Normalize2D(a.Direction);
            var d2 = Normalize2D(b.Direction);
            if (d1.IsAlmostEqualTo(XYZ.Zero) || d2.IsAlmostEqualTo(XYZ.Zero)) return false;
            return Math.Abs(d1.DotProduct(d2)) > angleToleranceCos;
        }

        /// <summary>Elemanın location curve'üne karşılık gelen ölçülendirilebilir geometri Reference'ını bulur.</summary>
        private static Reference? FindCenterlineReference(Element e, Options opt)
        {
            var locCurve = GetLine(e);
            if (locCurve == null) return null;
            var p0 = locCurve.GetEndPoint(0);
            var p1 = locCurve.GetEndPoint(1);

            var geo = e.get_Geometry(opt);
            if (geo == null) return null;
            return FindRefInGeometry(geo, p0, p1);
        }

        private static Reference? FindRefInGeometry(GeometryElement geo, XYZ p0, XYZ p1)
        {
            foreach (var g in geo)
            {
                if (g is GeometryInstance gi)
                {
                    var found = FindRefInGeometry(gi.GetInstanceGeometry(), p0, p1);
                    if (found != null) return found;
                }
                else if (g is Line ln && ln.Reference != null)
                {
                    var g0 = ln.GetEndPoint(0);
                    var g1 = ln.GetEndPoint(1);
                    if ((g0.IsAlmostEqualTo(p0, FtTol) && g1.IsAlmostEqualTo(p1, FtTol)) ||
                        (g0.IsAlmostEqualTo(p1, FtTol) && g1.IsAlmostEqualTo(p0, FtTol)))
                        return ln.Reference;
                }
            }
            return null;
        }

        private static DimensionType? ResolveDimensionType(Document doc, string typeName)
        {
            var linearTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(dt => dt.StyleType == DimensionStyleType.Linear)
                .ToList();

            if (!string.IsNullOrWhiteSpace(typeName))
            {
                var match = linearTypes.FirstOrDefault(dt =>
                {
                    var p = dt.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM);
                    return p?.AsString()?.Equals(typeName, StringComparison.OrdinalIgnoreCase) == true;
                });
                if (match != null) return match;
            }

            return linearTypes.OrderBy(dt =>
                dt.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM)?.AsString() ?? "").FirstOrDefault();
        }

        // ════════════════════════════════════════════════════════════════════
        // DIMENSION_TO_NEAREST_GRID
        // ════════════════════════════════════════════════════════════════════
        [EgOp("dimension_to_nearest_grid",
            Description = "Verilen (veya params.categories ile toplanan) elemanları aktif görünümde " +
                          "kendine paralel en yakın grid'e ölçülendirir. Farklı kotlardaki elemanlar 2D'ye " +
                          "düzleştirilerek karşılaştırılır. " +
                          "params: categories (string[]), dim_type (string, opsiyonel), " +
                          "search_radius_m (default 9.0)",
            Category    = "Çizim",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> DimensionToNearestGrid(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var view = rctx.UiDoc.ActiveView;
            if (!(view is ViewPlan) && view.ViewType != ViewType.Section && view.ViewType != ViewType.Elevation)
            {
                ctx.Log("  ⚠ dimension_to_nearest_grid: aktif görünüm Plan/Section/Elevation olmalı");
                return new() { ["created"] = 0 };
            }

            var elements = ResolveLinearElements(ctx, rctx.Doc);
            if (elements.Count == 0)
            {
                ctx.Log("  ⚠ dimension_to_nearest_grid: ölçülendirilecek (düz hatlı) eleman bulunamadı");
                return new() { ["created"] = 0 };
            }

            var grids = new FilteredElementCollector(rctx.Doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Where(g => g.Curve is Line)
                .ToList();
            if (grids.Count == 0)
            {
                ctx.Log("  ⚠ dimension_to_nearest_grid: görünümde grid yok");
                return new() { ["created"] = 0 };
            }

            var dimType = ResolveDimensionType(rctx.Doc, ctx.GetString("dim_type", ""));
            if (dimType == null)
            {
                ctx.Log("  ⚠ dimension_to_nearest_grid: lineer DimensionType bulunamadı");
                return new() { ["created"] = 0 };
            }

            double searchRadiusFt = ctx.GetDouble("search_radius_m", 9.0) / 0.3048;
            double targetZ = (view as ViewPlan)?.GenLevel?.Elevation ?? 0.0;
            const double angleTolCos = 0.9999847; // ~0.1°

            var opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };

            int created = 0, skipped = 0;

            using var scope = new RevitWriteScope(rctx.Doc, "En Yakın Grid'e Ölçülendir", rctx.IsAtomicMode);
            foreach (var e in elements)
            {
                var curve = GetLine(e);
                if (curve == null) { skipped++; continue; }

                var mid2d = Flatten2D(curve.Evaluate(0.5, true), targetZ);

                Grid? nearestGrid = null;
                double nearestDist = double.MaxValue;
                XYZ? nearestPoint = null;

                foreach (var g in grids)
                {
                    var gLine = (Line)g.Curve;
                    if (!AreParallel(curve, gLine, angleTolCos)) continue;

                    var origin2d = Flatten2D(gLine.GetEndPoint(0), targetZ);
                    var dir2d    = Normalize2D(gLine.Direction);
                    if (dir2d.IsAlmostEqualTo(XYZ.Zero)) continue;

                    var unbound = Line.CreateUnbound(origin2d, dir2d);
                    var proj    = unbound.Project(mid2d)?.XYZPoint;
                    if (proj == null) continue;

                    var dist = mid2d.DistanceTo(proj);
                    if (dist > searchRadiusFt) continue;
                    if (dist < nearestDist) { nearestDist = dist; nearestGrid = g; nearestPoint = proj; }
                }

                if (nearestGrid == null || nearestPoint == null) { skipped++; continue; }

                var eref = FindCenterlineReference(e, opt);
                if (eref == null) { skipped++; continue; }

                var p1 = new XYZ(mid2d.X, mid2d.Y, targetZ);
                var p2 = new XYZ(nearestPoint.X, nearestPoint.Y, targetZ);
                if (p1.DistanceTo(p2) < 0.01) { skipped++; continue; }

                var dirVec = (p2 - p1).Normalize();
                var dimLine = Line.CreateBound(p1 - dirVec * 1.5, p2 + dirVec * 1.5);

                var refArray = new ReferenceArray();
                refArray.Append(eref);
                refArray.Append(new Reference(nearestGrid));

                try
                {
                    rctx.Doc.Create.NewDimension(view, dimLine, refArray, dimType);
                    created++;
                }
                catch (Exception ex)
                {
                    ctx.Log($"  ⚠ id={Rv.GetId(e.Id)}: {ex.Message}");
                    skipped++;
                }
            }
            scope.Commit();

            ctx.Log($"  dimension_to_nearest_grid: {created} ölçü oluşturuldu, {skipped} atlandı");
            return new() { ["created"] = created, ["skipped"] = skipped };
        }

        // ════════════════════════════════════════════════════════════════════
        // DIMENSION_CONTINUOUS_SELECTION
        // ════════════════════════════════════════════════════════════════════
        [EgOp("dimension_continuous_selection",
            Description = "Verilen (veya params.categories ile toplanan) birbirine paralel elemanları " +
                          "ortak doğrultuları boyunca sıralayıp tek bir ardışık (continuous) ölçü serisi " +
                          "oluşturur. Donatı/kolon aralıkları, pencere/kapı dizileri vb. için kullanılabilir. " +
                          "params: categories (string[]), dim_type (string, opsiyonel)",
            Category    = "Çizim",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> DimensionContinuousSelection(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var view = rctx.UiDoc.ActiveView;
            var elements = ResolveLinearElements(ctx, rctx.Doc);
            if (elements.Count < 2)
            {
                ctx.Log("  ⚠ dimension_continuous_selection: en az 2 eleman gerekli");
                return new() { ["created"] = 0 };
            }

            var dimType = ResolveDimensionType(rctx.Doc, ctx.GetString("dim_type", ""));
            if (dimType == null)
            {
                ctx.Log("  ⚠ dimension_continuous_selection: lineer DimensionType bulunamadı");
                return new() { ["created"] = 0 };
            }

            const double angleTolCos = 0.9999847;
            double targetZ = (view as ViewPlan)?.GenLevel?.Elevation ?? 0.0;

            var baseCurve = GetLine(elements[0]);
            if (baseCurve == null)
            {
                ctx.Log("  ⚠ dimension_continuous_selection: ilk elemanda location curve yok");
                return new() { ["created"] = 0 };
            }

            var baseDir = Normalize2D(baseCurve.Direction);
            if (baseDir.IsAlmostEqualTo(XYZ.Zero))
            {
                ctx.Log("  ⚠ dimension_continuous_selection: ilk elemanın yönü geçersiz");
                return new() { ["created"] = 0 };
            }
            var perpDir = baseDir.CrossProduct(XYZ.BasisZ).Normalize();

            var opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };

            // (proje mesafesi, Reference, eleman) — perpDir boyunca sıralamak için
            var entries = new List<(double proj, Reference reference, ElementId id)>();
            int skipped = 0;

            foreach (var e in elements)
            {
                var curve = GetLine(e);
                if (curve == null) { skipped++; continue; }
                if (!AreParallel(baseCurve, curve, angleTolCos)) { skipped++; continue; }

                var eref = FindCenterlineReference(e, opt);
                if (eref == null) { skipped++; continue; }

                var mid2d = Flatten2D(curve.Evaluate(0.5, true), targetZ);
                var proj  = mid2d.DotProduct(perpDir);
                entries.Add((proj, eref, e.Id));
            }

            if (entries.Count < 2)
            {
                ctx.Log("  ⚠ dimension_continuous_selection: paralel/referanslı yeterli eleman yok");
                return new() { ["created"] = 0, ["skipped"] = skipped };
            }

            entries.Sort((a, b) => a.proj.CompareTo(b.proj));

            var refArray = new ReferenceArray();
            foreach (var entry in entries) refArray.Append(entry.reference);

            // Ölçü hattı: ilk ve son elemanın orta noktalarından geçen, perpDir
            // doğrultusunda uzanan bir çizgi (Revit ara segmentleri referanslardan kendi hesaplar).
            var firstMid = Flatten2D(GetLine(rctx.Doc.GetElement(entries[0].id))!.Evaluate(0.5, true), targetZ);
            var lastMid  = Flatten2D(GetLine(rctx.Doc.GetElement(entries[^1].id))!.Evaluate(0.5, true), targetZ);
            var lineDir  = (lastMid - firstMid).Normalize();
            var dimLine  = Line.CreateBound(firstMid - lineDir * 1.5, lastMid + lineDir * 1.5);

            using var scope = new RevitWriteScope(rctx.Doc, "Ardışık Ölçülendir", rctx.IsAtomicMode);
            int created = 0;
            try
            {
                rctx.Doc.Create.NewDimension(view, dimLine, refArray, dimType);
                created = 1;
            }
            catch (Exception ex)
            {
                ctx.Log($"  ⚠ dimension_continuous_selection: {ex.Message}");
            }
            scope.Commit();

            ctx.Log($"  dimension_continuous_selection: {entries.Count} referanslı 1 ardışık ölçü " +
                    $"({(created == 1 ? "oluşturuldu" : "BAŞARISIZ")}), {skipped} eleman atlandı");
            return new() { ["created"] = created, ["segments"] = entries.Count, ["skipped"] = skipped };
        }
    }
}
