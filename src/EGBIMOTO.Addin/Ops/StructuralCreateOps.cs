using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Yapısal Oluşturma Op'ları (StructuralCreateOps)
    /// Grup 5: Eksik Create Op'ları
    ///
    ///   create_beam_by_curve           — Kiriş (eğri boyunca)
    ///   create_column_by_point         — Kolon (nokta bazlı, kat arası)
    ///   create_grid_by_line            — Aks (Curve'den)
    ///   place_adaptive_component_by_points — Adaptive family (nokta listesiyle)
    /// </summary>
    public static class StructuralCreateOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // S01  create_beam_by_curve
        //
        // input : List<object?> (Curve — genellikle line_by_points çıktısı)
        // params: family_name  String  zorunlu  (örn: "M_Concrete-Rectangular Beam")
        //         type_name    String  zorunlu
        //         level_name   String  zorunlu
        //         offset_mm    Double  default=0 (Z ofseti)
        //
        // output: List<Element>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_beam_by_curve",
            RequiresTransaction = true,
            Description = "Curve listesi boyunca yapısal kiriş oluşturur. " +
                          "Input: line_by_points veya curve_divide_* çıktısı.",
            Category    = "Yapısal Oluşturma")]
        public static List<Element> CreateBeamByCurve(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var curves     = ctx.InputAs<List<object?>>();
            var familyName = ctx.RequireString("family_name");
            var typeName   = ctx.RequireString("type_name");
            var levelName  = ctx.RequireString("level_name");
            double offFt   = ctx.GetDouble("offset_mm", 0) / 304.8;

            var symbol = FindStructuralSymbol(rctx.Doc, familyName, typeName,
                BuiltInCategory.OST_StructuralFraming);
            if (symbol == null)
            {
                ctx.Log($"  create_beam_by_curve: '{familyName}/{typeName}' bulunamadı → []");
                return new List<Element>();
            }

            var level = FindLevel(rctx.Doc, levelName);
            if (level == null)
            {
                ctx.Log($"  create_beam_by_curve: '{levelName}' bulunamadı → []");
                return new List<Element>();
            }

            if (!symbol.IsActive) symbol.Activate();

            var created = new List<Element>();
            using var scope = new RevitWriteScope(rctx.Doc, "Kiriş Oluştur", rctx.IsAtomicMode);

            foreach (var item in curves)
            {
                if (item is not Curve c) continue;

                // Z offset uygula
                Curve beam_curve = c;
                if (Math.Abs(offFt) > 1e-6)
                {
                    var s = c.GetEndPoint(0);
                    var e = c.GetEndPoint(1);
                    try
                    {
                        beam_curve = Line.CreateBound(
                            new XYZ(s.X, s.Y, s.Z + offFt),
                            new XYZ(e.X, e.Y, e.Z + offFt));
                    }
                    catch { beam_curve = c; }
                }

                try
                {
                    var beam = rctx.Doc.Create.NewFamilyInstance(
                        beam_curve, symbol, level, StructuralType.Beam);
                    created.Add(beam);
                }
                catch (Exception ex)
                {
                    ctx.Log($"  create_beam_by_curve: curve atlandı — {ex.Message}");
                }
            }

            scope.Commit();
            ctx.Log($"  create_beam_by_curve: {created.Count}/{curves.Count} kiriş oluşturuldu");
            return created;
        }

        // ─────────────────────────────────────────────────────────────────────
        // S02  create_column_by_point
        //
        // input : List<object?> (XYZ — genellikle points_grid çıktısı)
        // params: family_name    String  zorunlu
        //         type_name      String  zorunlu
        //         base_level     String  zorunlu
        //         top_level      String  opsiyonel (yoksa height_mm)
        //         height_mm      Double  default=3000 (top_level yoksa)
        //         structural     Bool    default=true
        //
        // output: List<Element>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_column_by_point",
            RequiresTransaction = true,
            Description = "XYZ nokta listesine yapısal kolon oluşturur. " +
                          "Input: points_grid veya table_to_points çıktısı.",
            Category    = "Yapısal Oluşturma")]
        public static List<Element> CreateColumnByPoint(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var points     = ctx.InputAs<List<object?>>();
            var familyName = ctx.RequireString("family_name");
            var typeName   = ctx.RequireString("type_name");
            var baseName   = ctx.RequireString("base_level");
            var topName    = ctx.GetString("top_level", "");
            double htFt    = ctx.GetDouble("height_mm", 3000) / 304.8;
            bool structural= ctx.GetBool("structural", true);

            var symbol = FindStructuralSymbol(rctx.Doc, familyName, typeName,
                BuiltInCategory.OST_StructuralColumns);
            symbol ??= FindStructuralSymbol(rctx.Doc, familyName, typeName,
                BuiltInCategory.OST_Columns);

            if (symbol == null)
            {
                ctx.Log($"  create_column_by_point: '{familyName}/{typeName}' bulunamadı → []");
                return new List<Element>();
            }

            var baseLevel = FindLevel(rctx.Doc, baseName);
            if (baseLevel == null)
            {
                ctx.Log($"  create_column_by_point: '{baseName}' bulunamadı → []");
                return new List<Element>();
            }

            Level? topLevel = null;
            if (!string.IsNullOrEmpty(topName))
                topLevel = FindLevel(rctx.Doc, topName);

            if (!symbol.IsActive) symbol.Activate();

            var created = new List<Element>();
            using var scope = new RevitWriteScope(rctx.Doc, "Kolon Oluştur", rctx.IsAtomicMode);

            foreach (var item in points)
            {
                if (item is not XYZ pt) continue;

                try
                {
                    var col = rctx.Doc.Create.NewFamilyInstance(
                        pt, symbol, baseLevel,
                        structural ? StructuralType.Column : StructuralType.NonStructural);

                    // Üst kot ata
                    if (topLevel != null)
                    {
                        col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)
                           ?.Set(topLevel.Id);
                    }
                    else
                    {
                        col.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)
                           ?.Set(htFt);
                    }

                    created.Add(col);
                }
                catch (Exception ex)
                {
                    ctx.Log($"  create_column_by_point: nokta atlandı — {ex.Message}");
                }
            }

            scope.Commit();
            ctx.Log($"  create_column_by_point: {created.Count}/{points.Count} kolon oluşturuldu");
            return created;
        }

        // ─────────────────────────────────────────────────────────────────────
        // S03  create_grid_by_line
        //
        // input : List<object?> (Curve/Line)
        // params: name_prefix  String  default="G"
        //         start_index  Int     default=1
        //
        // output: List<Element>  (Grid)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_grid_by_line",
            RequiresTransaction = true,
            Description = "Curve listesinden Grid (aks) oluşturur. " +
                          "Input: line_by_points çıktısı. Mevcut create_grid'in curve versiyonu.",
            Category    = "Yapısal Oluşturma")]
        public static List<Element> CreateGridByLine(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var curves    = ctx.InputAs<List<object?>>();
            var prefix    = ctx.GetString("name_prefix", "G");
            int startIdx  = ctx.GetInt("start_index",   1);

            var created = new List<Element>();
            using var scope = new RevitWriteScope(rctx.Doc, "Aks Oluştur", rctx.IsAtomicMode);

            int idx = startIdx;
            foreach (var item in curves)
            {
                if (item is not Curve c) continue;

                try
                {
                    // Grid sadece Line kabul eder (Revit kısıtı)
                    Line? line = c as Line;
                    if (line == null)
                    {
                        // Arc curve → başlangıç bitiş noktasından line
                        var s = c.GetEndPoint(0);
                        var e = c.GetEndPoint(1);
                        if (s.DistanceTo(e) < 1e-6) continue;
                        line = Line.CreateBound(s, e);
                    }

                    var grid = Grid.Create(rctx.Doc, line);
                    grid.Name = $"{prefix}{idx}";
                    created.Add(grid);
                    idx++;
                }
                catch (Exception ex)
                {
                    ctx.Log($"  create_grid_by_line: curve atlandı — {ex.Message}");
                }
            }

            scope.Commit();
            ctx.Log($"  create_grid_by_line: {created.Count}/{curves.Count} aks oluşturuldu");
            return created;
        }

        // ─────────────────────────────────────────────────────────────────────
        // S04  place_adaptive_component_by_points
        //
        // input : List<object?> (XYZ listesi — her adaptive için N nokta)
        // params: family_name      String  zorunlu
        //         type_name        String  zorunlu
        //         points_per_item  Int     zorunlu (adaptive family'nin nokta sayısı)
        //
        // output: List<Element>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("place_adaptive_component_by_points",
            RequiresTransaction = true,
            Description = "Adaptive family'yi nokta listeleriyle yerleştirir. " +
                          "points_per_item: her instance için kaç nokta tüketilir.",
            Category    = "Yapısal Oluşturma")]
        public static List<Element> PlaceAdaptiveComponentByPoints(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var allPoints  = ctx.InputAs<List<object?>>();
            var familyName = ctx.RequireString("family_name");
            var typeName   = ctx.RequireString("type_name");
            int ppi        = Math.Max(1, ctx.GetInt("points_per_item", 2));

            var symbol = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    s.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                    s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

            if (symbol == null)
            {
                ctx.Log($"  place_adaptive_component_by_points: '{familyName}/{typeName}' bulunamadı");
                return new List<Element>();
            }
            if (!symbol.IsActive) symbol.Activate();

            var xyzList = allPoints.OfType<XYZ>().ToList();
            var created = new List<Element>();

            using var scope = new RevitWriteScope(rctx.Doc, "Adaptive Component", rctx.IsAtomicMode);

            for (int i = 0; i + ppi <= xyzList.Count; i += ppi)
            {
                var pts = xyzList.Skip(i).Take(ppi).ToList();
                try
                {
                    var instance = AdaptiveComponentInstanceUtils
                        .CreateAdaptiveComponentInstance(rctx.Doc, symbol);

                    var paramPts = AdaptiveComponentInstanceUtils
                        .GetInstancePlacementPointElementRefIds(instance)
                        .Select(id => rctx.Doc.GetElement(id) as ReferencePoint)
                        .ToList();

                    for (int j = 0; j < Math.Min(pts.Count, paramPts.Count); j++)
                    {
                        if (paramPts[j] != null)
                            paramPts[j]!.Position = pts[j];
                    }

                    created.Add(instance);
                }
                catch (Exception ex)
                {
                    ctx.Log($"  place_adaptive_component_by_points[{i}]: {ex.Message}");
                }
            }

            scope.Commit();
            ctx.Log($"  place_adaptive_component_by_points: " +
                    $"{created.Count} instance ({xyzList.Count / ppi} beklenen)");
            return created;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        private static FamilySymbol? FindStructuralSymbol(Document doc,
            string familyName, string typeName, BuiltInCategory cat)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(cat)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    s.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                    s.Name.Equals(typeName,          StringComparison.OrdinalIgnoreCase));

        private static Level? FindLevel(Document doc, string levelName)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
    }
}
