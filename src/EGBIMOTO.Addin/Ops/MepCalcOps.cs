using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — MEP Hesaplama Op'ları (MepCalcOps)
    /// Saf matematiksel hesaplar — Revit modeli opsiyonel.
    ///
    ///   calc_ach_airflow    — ACH yöntemi ile gerekli hava debisi (CMH/CFM)
    ///   calc_brick_quantity — Duvar alanından tuğla adedi ve hacim hesabı
    ///   calc_room_lux       — Oda ortalama aydınlık seviyesi (lux) hesabı
    /// </summary>
    public static class MepCalcOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // C01  calc_ach_airflow
        //
        // input : List<Element> (Room) opsiyonel — yoksa params'tan okur
        // params: area_m2     Double  (input yoksa zorunlu)
        //         height_m    Double  (input yoksa zorunlu)
        //         ach         Double  default=6  (normal ventilasyon)
        //         mode        String  default="normal" | "smoke" (10–12 ACH)
        //
        // output: List<Dict>
        //   room_id?, room_name?, area_m2, height_m, volume_m3,
        //   ach, airflow_cmh, airflow_cfm,
        //   fan_option_4x_cmh, fan_option_6x_cmh
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("calc_ach_airflow",
            Description = "ACH yöntemiyle gerekli hava debisini hesaplar. " +
                          "Input: oda listesi veya params (area_m2, height_m). " +
                          "Çıktı: airflow_cmh, airflow_cfm, fan seçenek önerileri.",
            Category    = "MEP Hesap")]
        public static List<Dictionary<string, object?>> CalcAchAirflow(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            double achParam  = ctx.GetDouble("ach",      6.0);
            string mode      = ctx.GetString("mode",     "normal").ToLowerInvariant();
            double areaParam = ctx.GetDouble("area_m2",  0);
            double htParam   = ctx.GetDouble("height_m", 0);

            // Smoke extraction modunda ACH 10-12 arası önerilir
            if (mode == "smoke" && achParam < 10) achParam = 10.0;

            var rows = new List<Dictionary<string, object?>>();

            if (elements.Count > 0)
            {
                // ── Oda listesinden hesapla ──────────────────────────────────
                foreach (var el in elements)
                {
                    if (el is not Room room) continue;

                    double areaFt2   = room.Area;
                    double areaM2    = areaFt2 * 0.0929;
                    double htFt      = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT)
                                          ?.AsDouble() ?? 0;
                    double htM       = htFt * 0.3048;
                    if (htM <= 0) htM = htParam > 0 ? htParam : 3.0;

                    rows.Add(BuildAchRow(
                        Rv.IdStr(room.Id), room.Name,
                        areaM2, htM, achParam));
                }
            }
            else if (areaParam > 0 && htParam > 0)
            {
                // ── Manuel parametre girişi ──────────────────────────────────
                rows.Add(BuildAchRow(null, null, areaParam, htParam, achParam));
            }
            else
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["status"]  = "PARAM_MISSING",
                    ["message"] = "Oda listesi (from) veya area_m2 + height_m params gereklidir.",
                });
            }

            ctx.Log($"  calc_ach_airflow: {rows.Count} hesap, mod={mode}, ACH={achParam}");
            return rows;
        }

        private static Dictionary<string, object?> BuildAchRow(
            string? roomId, string? roomName,
            double areaM2, double htM, double ach)
        {
            double volumeM3   = areaM2 * htM;
            double airflowCmh = volumeM3 * ach;
            double airflowCfm = airflowCmh * 0.5886;

            return new Dictionary<string, object?>
            {
                ["room_id"]           = roomId,
                ["room_name"]         = roomName,
                ["area_m2"]           = Math.Round(areaM2,    2),
                ["height_m"]          = Math.Round(htM,        2),
                ["volume_m3"]         = Math.Round(volumeM3,   1),
                ["ach"]               = ach,
                ["airflow_cmh"]       = Math.Round(airflowCmh, 0),
                ["airflow_cfm"]       = Math.Round(airflowCfm, 0),
                // Fan seçenek önerileri
                ["fan_option_4x_cmh"] = Math.Round(airflowCmh / 4.0, 0),
                ["fan_option_6x_cmh"] = Math.Round(airflowCmh / 6.0, 0),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // C02  calc_brick_quantity
        //
        // input : List<Element> (Wall) opsiyonel
        // params: area_m2      Double  (input yoksa zorunlu)
        //         thickness_cm Double  default=19  (1 tuğla = 19cm)
        //         brick_type   String  default="tam"
        //                      yarım(10)|tam(19)|1.5(29)|2(39)
        //         mortar_ratio Double  default=0.25 (Harç oranı)
        //         waste_pct    Double  default=7.5  (Fire+kırık payı %)
        //
        // output: List<Dict> (duvar başına)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("calc_brick_quantity",
            Description = "Duvar alanından tuğla adedi ve hacim hesabı. " +
                          "Input: duvar listesi veya params (area_m2, thickness_cm). " +
                          "Çıktı: brick_count, wall_volume_m3, mortar_m3, net_brick_m3.",
            Category    = "MEP Hesap")]
        public static List<Dictionary<string, object?>> CalcBrickQuantity(OpContext ctx)
        {
            var walls        = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            double areaParam = ctx.GetDouble("area_m2",      0);
            double thickCm   = ctx.GetDouble("thickness_cm", 19);
            string brickType = ctx.GetString("brick_type",   "tam").ToLowerInvariant();
            double mortarRatio = ctx.GetDouble("mortar_ratio", 0.25);
            double wastePct    = ctx.GetDouble("waste_pct",    7.5);

            // Tuğla boyutları — 19×19×13.5 cm standart
            // Birim tuğla hacmi (harçsız)
            const double singleBrickM3 = 0.19 * 0.19 * 0.135; // 0.004869 m³

            // Thickness override: brick_type'a göre
            thickCm = brickType switch
            {
                "yarım" or "half" => 10,
                "tam"   or "full" or "1" => 19,
                "1.5"   => 29,
                "2"     => 39,
                _       => thickCm,
            };

            var rows = new List<Dictionary<string, object?>>();

            if (walls.Any(e => e is Wall))
            {
                foreach (var el in walls)
                {
                    if (el is not Wall wall) continue;

                    double areaM2 = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)
                                       ?.AsDouble() * 0.0929 ?? 0;
                    double wallThickM = wall.Width * 0.3048; // ft → m
                    double wallThickCm = wallThickM * 100;

                    rows.Add(BuildBrickRow(
                        Rv.IdStr(el.Id),
                        wall.WallType?.Name ?? "",
                        areaM2, wallThickCm,
                        mortarRatio, wastePct, singleBrickM3));
                }
            }
            else if (areaParam > 0)
            {
                rows.Add(BuildBrickRow(null, null,
                    areaParam, thickCm,
                    mortarRatio, wastePct, singleBrickM3));
            }
            else
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["status"]  = "PARAM_MISSING",
                    ["message"] = "Duvar listesi (from) veya area_m2 params gereklidir.",
                });
            }

            ctx.Log($"  calc_brick_quantity: {rows.Count} duvar hesaplandı");
            return rows;
        }

        private static Dictionary<string, object?> BuildBrickRow(
            string? wallId, string? wallType,
            double areaM2, double thickCm,
            double mortarRatio, double wastePct, double singleBrickM3)
        {
            double thickM       = thickCm / 100.0;
            double volumeM3     = areaM2 * thickM;
            double mortarM3     = volumeM3 * mortarRatio;
            double netBrickM3   = volumeM3 - mortarM3;
            int    brickCount   = (int)Math.Ceiling(netBrickM3 / singleBrickM3);
            int    totalWaste   = (int)Math.Ceiling(brickCount * (1 + wastePct / 100.0));

            return new Dictionary<string, object?>
            {
                ["wall_id"]         = wallId,
                ["wall_type"]       = wallType,
                ["area_m2"]         = Math.Round(areaM2,    2),
                ["thickness_cm"]    = Math.Round(thickCm,   1),
                ["wall_volume_m3"]  = Math.Round(volumeM3,  3),
                ["mortar_m3"]       = Math.Round(mortarM3,  3),
                ["net_brick_m3"]    = Math.Round(netBrickM3,3),
                ["brick_count"]     = brickCount,
                ["waste_pct"]       = wastePct,
                ["total_with_waste"]= totalWaste,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // C03  calc_room_lux
        //
        // input : List<Element> (Room)
        // params: lumen_param  String  default="InitialIntensity"
        //                      fixture family'deki lumen parametresi adı
        //         cu           Double  default=0.60  (Utilization Coefficient)
        //         mf           Double  default=0.80  (Maintenance Factor)
        //         target_lux   Double  default=300   (Ofis standardı)
        //
        // Pre-check: lumen_param fixture family'de mevcut mu?
        //
        // output: List<Dict>
        //   room_id, room_name, area_m2, fixture_count,
        //   total_lumens, avg_lux, target_lux, cu, mf, status
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("calc_room_lux",
            Description = "Oda ortalama aydınlık seviyesini hesaplar (lux). " +
                          "Pre-check: lumen_param fixture family'de yoksa parametre eklenmesi istenir. " +
                          "Formül: avg_lux = (toplam_lumen × CU × MF) / alan_m2",
            Category    = "MEP Hesap")]
        public static List<Dictionary<string, object?>> CalcRoomLux(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var rooms      = ctx.InputAs<List<Element>>();
            string luxParam = ctx.GetString("lumen_param",  "InitialIntensity");
            double cu       = ctx.GetDouble("cu",            0.60);
            double mf       = ctx.GetDouble("mf",            0.80);
            double target   = ctx.GetDouble("target_lux",   300.0);

            // ── Pre-check: lumen parametresi fixture family'de var mı? ─────────
            var sampleFixture = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .FirstOrDefault();

            if (sampleFixture != null)
            {
                var missing = CheckRequiredParams(sampleFixture, new[] { luxParam });
                if (missing.Count > 0)
                {
                    ctx.Log($"  calc_room_lux: PRE-CHECK HATASI — eksik param: {string.Join(", ", missing)}");
                    return new List<Dictionary<string, object?>>
                    {
                        new()
                        {
                            ["status"]         = "PARAM_MISSING",
                            ["missing_params"]  = string.Join(";", missing),
                            ["message"]        = $"Şu parametreler aydınlatma armütürü family'sine eklenmeli: " +
                                                 $"{string.Join(", ", missing)}. " +
                                                 $"Family Editor → Add Parameter → {luxParam} (Number veya Integer).",
                        }
                    };
                }
            }

            // ── BoundingBox ile oda içindeki fixture'ları bul ────────────────
            // SpatialElementCalculator Revit 2026'da kaldırıldı
            // Alternatif: Room BoundingBox + BoundingBoxIntersectsFilter
            var rows = new List<Dictionary<string, object?>>();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                double areaM2 = room.Area * 0.0929;
                if (areaM2 < 0.1) continue;

                // Room BoundingBox → fixture'ları bul
                var bb = room.get_BoundingBox(null);
                var fixtures = bb == null
                    ? new List<FamilyInstance>()
                    : new FilteredElementCollector(rctx.Doc)
                        .OfClass(typeof(FamilyInstance))
                        .WherePasses(new BoundingBoxIntersectsFilter(
                            new Outline(bb.Min, bb.Max)))
                        .Cast<FamilyInstance>()
                        .Where(fi => Rv.GetCategoryId(fi) == (int)BuiltInCategory.OST_LightingFixtures)
                        .ToList();

                // Toplam lumen
                double totalLumens = 0;
                foreach (var fi in fixtures)
                {
                    var lp = fi.LookupParameter(luxParam)
                           ?? fi.Symbol.LookupParameter(luxParam);
                    if (lp != null)
                        totalLumens += lp.AsDouble();
                }

                double avgLux = areaM2 > 0
                    ? (totalLumens * cu * mf) / areaM2
                    : 0;

                string status = avgLux >= target ? "OK"
                    : avgLux >= target * 0.8     ? "WARNING"
                    : "ERROR";

                rows.Add(new Dictionary<string, object?>
                {
                    ["room_id"]      = Rv.IdStr(room.Id),
                    ["room_name"]    = room.Name,
                    ["room_number"]  = room.Number,
                    ["area_m2"]      = Math.Round(areaM2,      2),
                    ["fixture_count"]= fixtures.Count,
                    ["total_lumens"] = Math.Round(totalLumens, 0),
                    ["cu"]           = cu,
                    ["mf"]           = mf,
                    ["avg_lux"]      = Math.Round(avgLux,      1),
                    ["target_lux"]   = target,
                    ["status"]       = status,
                });
            }

            int ok = rows.Count(r => (string?)r["status"] == "OK");
            ctx.Log($"  calc_room_lux: {rows.Count} oda — OK:{ok} hedef:{target}lux");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        internal static List<string> CheckRequiredParams(
            Element el, IEnumerable<string> paramNames)
        {
            var missing = new List<string>();
            foreach (var name in paramNames)
            {
                var p = el.LookupParameter(name);
                if (p == null && el is FamilyInstance fi)
                    p = fi.Symbol.LookupParameter(name);
                if (p == null) missing.Add(name);
            }
            return missing;
        }

        internal static Dictionary<string, object?> ParamMissingResult(
            IEnumerable<string> missing, string hint = "")
        {
            var list = missing.ToList();
            return new Dictionary<string, object?>
            {
                ["status"]        = "PARAM_MISSING",
                ["missing_params"]= string.Join(";", list),
                ["message"]       = $"Şu parametreler family/elemana eklenmeli: " +
                                    $"{string.Join(", ", list)}." +
                                    (hint.Length > 0 ? $" {hint}" : ""),
            };
        }

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
    }
}
