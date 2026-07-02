// Copyright 2026 Ertuğan Genel
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// ─────────────────────────────────────────────────────────────────────────────
// NOTICE: Bu dosyadaki "kapalı bölge içi MEP tespiti" yaklaşımı, açık kaynak
// projeden esinlenerek EGBIMOTO mimarisine uyarlanmıştır:
//   460707300-tech/MEPRegionMarker  (https://github.com/460707300-tech/MEPRegionMarker)
// Orijinal interaktif bölge çizimi + etiketleme komutu yerine, EGBIMOTO'da
// Room/Area sınırları (veya verilen kategori) baz alınarak içerideki MEP
// elemanları sayan/raporlayan/parametre yazan op'lara dönüştürülmüştür.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Addin.Host;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MepRegionOps  —  EGBIMOTO v10
    //
    //  Kapalı bölge (Room / Area) içindeki MEP elemanlarını tespit eder,
    //  sayar, raporlar ve isteğe bağlı olarak bölge bilgisini elemanlara
    //  parametre olarak yazar.
    //
    //  Hakediş ve oda-bazlı metraj için kritik: "hangi odada kaç metre boru var",
    //  "her mahalde kaç armatür" gibi sorulara cevap üretir.
    //
    //  Op'lar:
    //    mep_region_count    → Room/Area bazlı MEP eleman sayımı + uzunluk (RAPOR)
    //    mep_region_tag      → İçerideki MEP'lere oda adı/no parametresi yaz (YAZMA)
    //
    //  Manifest örneği:
    //    { "id": "say", "op": "mep_region_count",
    //      "params": { "region_category": "OST_Rooms",
    //                  "mep_categories": "OST_PipeCurves,OST_DuctCurves",
    //                  "z_tolerance_mm": 1500 } }
    // ═══════════════════════════════════════════════════════════════════════════

    public static class MepRegionOps
    {
        private const double FT_PER_MM = 1.0 / 304.8;
        private const double MM_PER_FT = 304.8;
        private const double FT2_TO_M2 = 0.09290304;
        private const double FT_TO_M   = 0.3048;

        // ─────────────────────────────────────────────────────────────────────
        //  OP 1: mep_region_count   (RAPOR — yazma yok)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("mep_region_count",
            Description =
                "Kapalı bölge (Room/Area) içindeki MEP elemanlarını sayar ve uzunluk toplar.\n" +
                "Yazma yapmaz. Oda-bazlı metraj / hakediş için özet üretir.\n\n" +
                "Input  : List<Element> (opsiyonel bölge listesi) — verilmezse region_category ile toplanır.\n" +
                "params :\n" +
                "  region_category  — bölge kaynağı (default: OST_Rooms; alternatif: OST_Areas)\n" +
                "  mep_categories   — sayılacak MEP kategorileri (virgül,\n" +
                "                     default: OST_PipeCurves,OST_DuctCurves,OST_CableTray)\n" +
                "  z_tolerance_mm   — dikey tolerans; eleman merkezinin oda yüksekliği ± payı (default: 1500)\n\n" +
                "Çıktı: List<Dictionary> — her satır bir bölge:\n" +
                "  region_id, region_name, region_number, level_name, area_m2,\n" +
                "  mep_count, total_length_m, pipe_count, duct_count, tray_count, by_category",
            Category = "MEP Koordinasyon")]
        public static List<Dictionary<string, object?>> MepRegionCount(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[mep_region_count] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            string regionCat = ctx.GetString("region_category", "OST_Rooms");
            var mepCats = ParseCategories(ctx.GetString("mep_categories",
                              "OST_PipeCurves,OST_DuctCurves,OST_CableTray"));
            double zTolFt = ctx.GetInt("z_tolerance_mm", 1500) * FT_PER_MM;

            var regions = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            if (regions.Count == 0)
                regions = CollectRegions(doc, regionCat);

            var mepElems = CollectByCategories(doc, mepCats);
            ctx.Log($"[MepRegion] {regions.Count} bölge, {mepElems.Count} MEP elemanı.");

            var rows = new List<Dictionary<string, object?>>();

            foreach (var region in regions)
            {
                var bb = region.get_BoundingBox(null);
                if (bb == null) continue;

                double zMin = bb.Min.Z - zTolFt;
                double zMax = bb.Max.Z + zTolFt;

                var inside = new List<Element>();
                foreach (var mep in mepElems)
                {
                    var center = ElementCenter(mep);
                    if (center == null) continue;
                    if (center.Z < zMin || center.Z > zMax) continue;
                    if (IsPointInRegion(region, center)) inside.Add(mep);
                }

                int pipeCount = inside.Count(e => Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_PipeCurves);
                int ductCount = inside.Count(e => Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_DuctCurves);
                int trayCount = inside.Count(e => Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_CableTray);

                double totalLenM = inside.Sum(GetCurveLengthM);

                var byCat = inside
                    .GroupBy(e => e.Category?.Name ?? "?")
                    .ToDictionary(g => g.Key, g => g.Count());

                rows.Add(new Dictionary<string, object?>
                {
                    ["region_id"]      = Rv.GetId(region.Id),
                    ["region_name"]    = GetRegionName(region),
                    ["region_number"]  = GetRegionNumber(region),
                    ["level_name"]     = GetLevelName(doc, region),
                    ["area_m2"]        = Math.Round(GetRegionAreaM2(region), 2),
                    ["mep_count"]      = inside.Count,
                    ["total_length_m"] = Math.Round(totalLenM, 2),
                    ["pipe_count"]     = pipeCount,
                    ["duct_count"]     = ductCount,
                    ["tray_count"]     = trayCount,
                    ["by_category"]    = string.Join("; ", byCat.Select(kv => $"{kv.Key}={kv.Value}")),
                });
            }

            ctx.Log($"[MepRegion] {rows.Count} bölge raporlandı.");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OP 2: mep_region_tag   (YAZMA)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("mep_region_tag",
            Description =
                "Kapalı bölge içindeki MEP elemanlarına bölge adı/numarasını parametre olarak yazar.\n" +
                "DİKKAT: Model değişikliği yapar.\n\n" +
                "Input  : List<Element> (opsiyonel bölge listesi) — verilmezse region_category ile toplanır.\n" +
                "params :\n" +
                "  region_category  — bölge kaynağı (default: OST_Rooms)\n" +
                "  mep_categories   — etiketlenecek MEP kategorileri (virgül, default: OST_PipeCurves,OST_DuctCurves,OST_CableTray)\n" +
                "  target_param     — yazılacak parametre adı (default: Comments / Notlar)\n" +
                "  write_mode       — 'name' | 'number' | 'name_number' (default: name_number)\n" +
                "  z_tolerance_mm   — dikey tolerans (default: 1500)\n\n" +
                "Çıktı: List<Dictionary> — region_id, region_name, tagged_count, skipped_count",
            Category = "MEP Koordinasyon",
            RequiresTransaction = true)]
        public static List<Dictionary<string, object?>> MepRegionTag(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[mep_region_tag] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            string regionCat = ctx.GetString("region_category", "OST_Rooms");
            var mepCats = ParseCategories(ctx.GetString("mep_categories",
                              "OST_PipeCurves,OST_DuctCurves,OST_CableTray"));
            string targetParam = ctx.GetString("target_param", "Comments");
            string writeMode   = ctx.GetString("write_mode", "name_number").ToLowerInvariant();
            double zTolFt      = ctx.GetInt("z_tolerance_mm", 1500) * FT_PER_MM;

            var regions = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            if (regions.Count == 0)
                regions = CollectRegions(doc, regionCat);

            var mepElems = CollectByCategories(doc, mepCats);
            var results = new List<Dictionary<string, object?>>();

            using var scope = new RevitWriteScope(doc, "MEP Bölge Etiketle", rctx.IsAtomicMode);

            foreach (var region in regions)
            {
                var bb = region.get_BoundingBox(null);
                if (bb == null) continue;

                double zMin = bb.Min.Z - zTolFt;
                double zMax = bb.Max.Z + zTolFt;

                string name = GetRegionName(region);
                string number = GetRegionNumber(region);
                string tagValue = writeMode switch
                {
                    "name"   => name,
                    "number" => number,
                    _        => $"{number} - {name}",
                };

                int tagged = 0, skipped = 0;
                foreach (var mep in mepElems)
                {
                    var center = ElementCenter(mep);
                    if (center == null) { skipped++; continue; }
                    if (center.Z < zMin || center.Z > zMax) continue;
                    if (!IsPointInRegion(region, center)) continue;

                    var p = mep.LookupParameter(targetParam);
                    if (p == null || p.IsReadOnly || p.StorageType != StorageType.String)
                    {
                        skipped++;
                        continue;
                    }
                    try { p.Set(tagValue); tagged++; }
                    catch { skipped++; }
                }

                results.Add(new Dictionary<string, object?>
                {
                    ["region_id"]     = Rv.GetId(region.Id),
                    ["region_name"]   = name,
                    ["tagged_count"]  = tagged,
                    ["skipped_count"] = skipped,
                });
            }

            scope.Commit();
            int total = results.Sum(r => (int)(r["tagged_count"] ?? 0));
            ctx.Log($"[MepRegion] Toplam {total} eleman etiketlendi.");
            return results;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  NOKTA-BÖLGE TESTİ
        // ═══════════════════════════════════════════════════════════════════════

        // Önce Room.IsPointInRoom dener (kesin). Olmazsa BBox + sınır eğrisi
        // ray-casting (formın 2D düzlemde içinde mi) ile düşer.
        private static bool IsPointInRegion(Element region, XYZ pt)
        {
            if (region is Room room)
            {
                try
                {
                    // Oda yüksekliğine projeksiyon — IsPointInRoom Z'ye duyarlıdır
                    var loc = (room.Location as LocationPoint)?.Point;
                    var probe = loc != null ? new XYZ(pt.X, pt.Y, loc.Z + 0.1) : pt;
                    return room.IsPointInRoom(probe);
                }
                catch { /* sınır eğrisi yöntemine düş */ }
            }

            // Genel yöntem: bölge sınır eğrilerini al, 2D ray-casting
            var loops = GetBoundaryLoops(region);
            if (loops.Count == 0)
            {
                // Son çare: BBox içinde mi
                var bb = region.get_BoundingBox(null);
                if (bb == null) return false;
                return pt.X >= bb.Min.X && pt.X <= bb.Max.X
                    && pt.Y >= bb.Min.Y && pt.Y <= bb.Max.Y;
            }
            return PointInPolygons(loops, pt);
        }

        private static List<List<XYZ>> GetBoundaryLoops(Element region)
        {
            var loops = new List<List<XYZ>>();
            try
            {
                if (region is SpatialElement spatial)
                {
                    var opt = new SpatialElementBoundaryOptions();
                    var segLoops = spatial.GetBoundarySegments(opt);
                    if (segLoops != null)
                    {
                        foreach (var loop in segLoops)
                        {
                            var pts = new List<XYZ>();
                            foreach (var seg in loop)
                            {
                                var c = seg.GetCurve();
                                if (c != null) pts.Add(c.GetEndPoint(0));
                            }
                            if (pts.Count >= 3) loops.Add(pts);
                        }
                    }
                }
            }
            catch { }
            return loops;
        }

        // Ray-casting: tek dış loop varsayımıyla (çoğu mahal). Birden fazla loop
        // varsa ilkini dış sınır kabul eder.
        private static bool PointInPolygons(List<List<XYZ>> loops, XYZ pt)
        {
            bool inside = false;
            foreach (var poly in loops)
            {
                if (PointInPolygon2D(poly, pt)) inside = !inside;
            }
            return inside;
        }

        private static bool PointInPolygon2D(List<XYZ> poly, XYZ pt)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[i].X, yi = poly[i].Y;
                double xj = poly[j].X, yj = poly[j].Y;
                bool intersect = ((yi > pt.Y) != (yj > pt.Y))
                    && (pt.X < (xj - xi) * (pt.Y - yi) / ((yj - yi) + 1e-12) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═══════════════════════════════════════════════════════════════════════

        private static XYZ? ElementCenter(Element e)
        {
            if (e.Location is LocationPoint lp) return lp.Point;
            if (e.Location is LocationCurve lc && lc.Curve != null)
                return lc.Curve.Evaluate(0.5, true);
            var bb = e.get_BoundingBox(null);
            if (bb != null) return 0.5 * (bb.Min + bb.Max);
            return null;
        }

        private static double GetCurveLengthM(Element e)
        {
            if (e.Location is LocationCurve lc && lc.Curve != null)
                return lc.Curve.Length * FT_TO_M;
            return 0;
        }

        private static List<Element> CollectRegions(Document doc, string regionCat)
        {
            BuiltInCategory bic = regionCat.Equals("OST_Areas", StringComparison.OrdinalIgnoreCase)
                ? BuiltInCategory.OST_Areas
                : BuiltInCategory.OST_Rooms;

            return new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(e => e is SpatialElement se && se.Area > 0) // yerleştirilmiş bölgeler
                .ToList();
        }

        private static string GetRegionName(Element region)
        {
            try
            {
                var p = region.get_Parameter(BuiltInParameter.ROOM_NAME);
                if (p != null && p.HasValue) return p.AsString() ?? "?";
            }
            catch { }
            return region.Name ?? "?";
        }

        private static string GetRegionNumber(Element region)
        {
            try
            {
                var p = region.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                if (p != null && p.HasValue) return p.AsString() ?? "?";
            }
            catch { }
            return "?";
        }

        private static string GetLevelName(Document doc, Element region)
        {
            try
            {
                if (region is SpatialElement se && se.LevelId != ElementId.InvalidElementId)
                {
                    var lvl = doc.GetElement(se.LevelId);
                    return lvl?.Name ?? "?";
                }
            }
            catch { }
            return "?";
        }

        private static double GetRegionAreaM2(Element region)
        {
            if (region is SpatialElement se) return se.Area * FT2_TO_M2;
            return 0;
        }

        private static List<BuiltInCategory> ParseCategories(string csv)
        {
            var list = new List<BuiltInCategory>();
            foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = raw.Trim();
                if (Enum.TryParse<BuiltInCategory>(name, out var bic))
                    list.Add(bic);
            }
            return list;
        }

        private static List<Element> CollectByCategories(Document doc, List<BuiltInCategory> cats)
        {
            var result = new List<Element>();
            foreach (var bic in cats)
            {
                try
                {
                    var col = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();
                    result.AddRange(col);
                }
                catch { }
            }
            return result;
        }
    }
}
