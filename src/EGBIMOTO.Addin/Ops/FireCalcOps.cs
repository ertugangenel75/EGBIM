using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Yangın Korunum Hesap Op'ları (FireCalcOps)
    ///
    ///   calc_hazen_williams           — Boru sürtünme kaybı (Hazen-Williams)
    ///   calc_sprinkler_design_density — Tehlike sınıfından tasarım parametreleri (TR Ek-8/B)
    ///   fire_hose_cabinet_spacing_check — Yangın dolabı arası mesafe kontrolü (≤30m)
    ///   calc_duct_sheet_weight        — Kanal çevre × kalınlık × uzunluk × yoğunluk → sac ağırlığı
    /// </summary>
    public static class FireCalcOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // F01  calc_hazen_williams
        //
        // Formül: P_m = 6.05 × 10⁵ × [Q_m^1.85 / (C^1.85 × d_m^4.87)]
        //   P_m = birim uzunluktaki basınç kaybı (bar/m)
        //   Q_m = debi (lt/dak)
        //   C   = pürüzlülük katsayısı (galvaniz=120, PE/PVC=150, çelik=100)
        //   d_m = boru iç çapı (mm)
        //
        // input : List<Element> (Pipe) opsiyonel — yoksa params'tan hesap
        // params: flow_rate_lpm  Double  (lt/dak)  — input yoksa zorunlu
        //         pipe_diam_mm   Double  (mm iç çap) — input yoksa zorunlu
        //         c_factor       Double  default=120 (galvanizli çelik)
        //         pipe_length_m  Double  default=1.0 (toplam boru uzunluğu m)
        //
        // output: List<Dict>
        //   pipe_id?, diam_mm, flow_lpm, c_factor, friction_loss_bar_per_m,
        //   total_loss_bar, velocity_m_s
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("calc_hazen_williams",
            Description = "Hazen-Williams formülü ile boru sürtünme kaybı hesabı. " +
                          "P = 6.05×10⁵ × [Q^1.85 / (C^1.85 × d^4.87)]. " +
                          "Input: List<Pipe> veya params (flow_rate_lpm, pipe_diam_mm).",
            Category    = "Yangın Hesap")]
        public static List<Dictionary<string, object?>> CalcHazenWilliams(OpContext ctx)
        {
            var pipes     = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            double cDef   = ctx.GetDouble("c_factor",      120.0);
            double lenDef = ctx.GetDouble("pipe_length_m", 1.0);

            var rows = new List<Dictionary<string, object?>>();

            if (pipes.Any(e => Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_PipeCurves))
            {
                // Pipe listesinden hesapla
                foreach (var el in pipes)
                {
                    if (Rv.GetCategoryId(el) != (int)BuiltInCategory.OST_PipeCurves) continue;

                    double diamMm = (el.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
                                       ?.AsDouble() ?? 0) * 304.8;
                    double lenM   = (el.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)
                                       ?.AsDouble() ?? 0) * 0.3048;
                    double flowLs = el.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM)
                                       ?.AsDouble() ?? 0;
                    double flowLpm = flowLs * 60; // lt/s → lt/dak

                    rows.Add(CalcHWRow(Rv.IdStr(el.Id), diamMm, flowLpm, cDef, lenM));
                }
            }
            else
            {
                // Manuel giriş
                double flowLpm = ctx.GetDouble("flow_rate_lpm", 0);
                double diamMm  = ctx.GetDouble("pipe_diam_mm",  0);

                if (flowLpm <= 0 || diamMm <= 0)
                {
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["status"] = "PARAM_MISSING",
                        ["message"] = "Boru listesi veya flow_rate_lpm + pipe_diam_mm gereklidir."
                    });
                }
                else
                {
                    rows.Add(CalcHWRow(null, diamMm, flowLpm, cDef, lenDef));
                }
            }

            ctx.Log($"  calc_hazen_williams: {rows.Count} hesap tamamlandı");
            return rows;
        }

        private static Dictionary<string, object?> CalcHWRow(
            string? pipeId, double diamMm, double flowLpm, double c, double lenM)
        {
            // Hazen-Williams: P_m = 6.05e5 × [Q^1.85 / (C^1.85 × d^4.87)]  (bar/m)
            double pm = 0;
            double velocityMs = 0;

            if (diamMm > 0 && flowLpm > 0)
            {
                double qPow   = Math.Pow(flowLpm, 1.85);
                double cPow   = Math.Pow(c,       1.85);
                double dPow   = Math.Pow(diamMm,  4.87);
                pm = 6.05e5 * qPow / (cPow * dPow);

                // Akış hızı: V = Q / A  (m/s)
                double areaM2 = Math.PI * Math.Pow(diamMm / 2000.0, 2);
                double flowM3s = flowLpm / 60000.0;
                velocityMs = areaM2 > 0 ? flowM3s / areaM2 : 0;
            }

            return new Dictionary<string, object?>
            {
                ["pipe_id"]               = pipeId,
                ["diam_mm"]               = Math.Round(diamMm, 1),
                ["flow_lpm"]              = Math.Round(flowLpm, 2),
                ["c_factor"]              = c,
                ["length_m"]              = Math.Round(lenM, 2),
                ["friction_loss_bar_per_m"]= Math.Round(pm, 6),
                ["total_loss_bar"]         = Math.Round(pm * lenM, 4),
                ["velocity_m_s"]           = Math.Round(velocityMs, 3),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // F02  calc_sprinkler_design_density
        //
        // Türk Yangın Yönetmeliği Ek-8/B tablosundan tehlike sınıfına göre
        // tasarım yoğunluğu ve koruma alanı döner.
        //
        // input : — (pure calc)
        // params: hazard_class  String  zorunlu
        //         Değerler: "dusuk" | "orta_1" | "orta_2" | "orta_3" | "yuksek_1" | "yuksek_2"
        //         system_type  String  default="islak" (islak|kuru)
        //
        // output: Dict
        //   hazard_class, design_density_mm_per_min, protection_area_m2,
        //   system_type, reference, sprinkler_count_estimate (opsiyonel)
        //   + area_m2 param verilirse required_flow_lpm da hesaplanır
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("calc_sprinkler_design_density",
            Description = "Türk Yangın Yönetmeliği Ek-8/B tablosundan tehlike sınıfına göre " +
                          "tasarım yoğunluğu (mm/dak) ve koruma alanı (m²) döner.",
            Category    = "Yangın Hesap")]
        public static Dictionary<string, object?> CalcSprinklerDesignDensity(OpContext ctx)
        {
            var hazardClass = ctx.RequireString("hazard_class").ToLowerInvariant()
                .Replace(" ", "_").Replace("-", "_");
            var sysType  = ctx.GetString("system_type", "islak").ToLowerInvariant();
            double areaM2 = ctx.GetDouble("area_m2", 0);

            // Ek-8/B tablosu
            var table = new Dictionary<string, (double density, double protection)>
            {
                ["dusuk"]    = (2.25, 84),
                ["orta_1"]   = (5.0,  72),
                ["orta_2"]   = (5.0,  144),
                ["orta_3"]   = (5.0,  216),
                ["yuksek_1"] = (7.7,  260),
                ["yuksek_2"] = (10.0, 260),
            };

            // Alias desteği
            var aliases = new Dictionary<string, string>
            {
                ["dusuk_tehlike"]  = "dusuk",
                ["low"]            = "dusuk",
                ["light"]          = "dusuk",
                ["ordinary_1"]     = "orta_1",
                ["ordinary_2"]     = "orta_2",
                ["ordinary_3"]     = "orta_3",
                ["high_1"]         = "yuksek_1",
                ["high_2"]         = "yuksek_2",
                ["extra_high"]     = "yuksek_2",
            };

            if (aliases.TryGetValue(hazardClass, out var mapped))
                hazardClass = mapped;

            if (!table.TryGetValue(hazardClass, out var data))
            {
                ctx.Log($"  calc_sprinkler_design_density: '{hazardClass}' bilinmiyor");
                return new Dictionary<string, object?>
                {
                    ["status"]  = "ERROR",
                    ["message"] = $"Geçersiz tehlike sınıfı: '{hazardClass}'. " +
                                  $"Geçerli: dusuk, orta_1, orta_2, orta_3, yuksek_1, yuksek_2"
                };
            }

            double designDensity = data.density;  // mm/dak
            double protArea      = data.protection; // m²

            // Gerekli akış: Q = density × protection_area × (1 lt / (1 mm × 1 m²/dak))
            // 1 mm/dak × m² = 1 L/(dak·m²) × m² = L/dak
            double reqFlowLpm = designDensity * protArea;

            // Ek debi: yangın dolabı (100 lt/dak) + hidrant (400 lt/dak) — Ek-8/C
            double extraHose    = hazardClass == "yuksek_2" ? 200 : 100;
            double extraHydrant = hazardClass.StartsWith("yuksek") ? 1500 : 400;
            double totalPumpLpm = reqFlowLpm + extraHose + extraHydrant;

            var result = new Dictionary<string, object?>
            {
                ["hazard_class"]            = hazardClass,
                ["design_density_mm_per_min"]= designDensity,
                ["protection_area_m2"]      = protArea,
                ["system_type"]             = sysType,
                ["required_flow_lpm"]       = Math.Round(reqFlowLpm, 1),
                ["extra_hose_cabinet_lpm"]  = extraHose,
                ["extra_hydrant_lpm"]       = extraHydrant,
                ["total_pump_flow_lpm"]     = Math.Round(totalPumpLpm, 1),
                ["reference"]               = "TR Yangın Yönetmeliği Ek-8/B",
            };

            // Opsiyonel: alan verilirse sprinkler sayısı tahmini
            if (areaM2 > 0)
            {
                // Standart kapsama alanı: 4.6m × 4.6m = 21.16 m²/baş (ışık tehlike)
                double perHead = hazardClass == "dusuk" ? 21.0
                               : hazardClass.StartsWith("orta") ? 12.5
                               : 9.0;
                result["area_m2"]                  = areaM2;
                result["coverage_per_head_m2"]     = perHead;
                result["estimated_sprinkler_count"] = (int)Math.Ceiling(areaM2 / perHead);
            }

            ctx.Log($"  calc_sprinkler_design_density: {hazardClass} → " +
                    $"{designDensity}mm/dak, {protArea}m², pompa={totalPumpLpm:F0}lt/dak");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // F03  fire_hose_cabinet_spacing_check
        //
        // input : List<Element>  (yangın dolabı FamilyInstance'ları)
        // params: max_spacing_m  Double  default=30.0  (Türk Yangın Yönetmeliği)
        //         hose_length_m  Double  default=20.0  (hortum uzunluğu)
        //
        // output: ValidationReport
        //   Her dolap için komşu dolabına mesafe + hortum erişim analizi
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fire_hose_cabinet_spacing_check",
            Description = "Yangın dolapları arası mesafeyi kontrol eder (≤30m). " +
                          "Türk Yangın Yönetmeliği: her katta, yangın duvarları ile ayrılmış " +
                          "her bölümde, dolaplar arası ≤30m.",
            Category    = "Yangın Hesap")]
        public static ValidationReport FireHoseCabinetSpacingCheck(OpContext ctx)
        {
            var cabinets    = ctx.InputAs<List<Element>>();
            double maxSpaceM = ctx.GetDouble("max_spacing_m",  30.0);
            double hoseLenM  = ctx.GetDouble("hose_length_m",  20.0);
            var results      = new List<ValidationResult>();

            // XYZ noktalarını çıkar
            var points = cabinets
                .Select(el =>
                {
                    XYZ? pt = null;
                    if (el.Location is LocationPoint lp) pt = lp.Point;
                    else
                    {
                        var bb = el.get_BoundingBox(null);
                        if (bb != null) pt = (bb.Min + bb.Max) / 2.0;
                    }
                    return (el, pt);
                })
                .Where(x => x.pt != null)
                .ToList();

            if (points.Count < 2)
            {
                ctx.Log("  fire_hose_cabinet_spacing_check: Kontrol için en az 2 dolap gerekli");
                return MakeReport("Yangın Dolabı Mesafe Kontrolü", results);
            }

            // Her dolap için en yakın komşuya mesafe
            for (int i = 0; i < points.Count; i++)
            {
                var (elA, ptA) = points[i];
                double nearestM = double.MaxValue;

                for (int j = 0; j < points.Count; j++)
                {
                    if (i == j) continue;
                    var (_, ptB) = points[j];
                    double distM = ptA!.DistanceTo(ptB!) * 0.3048;
                    if (distM < nearestM) nearestM = distM;
                }

                bool spacingOk = nearestM <= maxSpaceM;
                // Hortum erişim: 2 × hose_length (her iki yön)
                bool coverageOk = nearestM <= hoseLenM * 2;

                results.Add(new ValidationResult
                {
                    RuleId    = "F03-ARALIK",
                    ElementId = Rv.IdStr(elA.Id),
                    Category  = "Yangın Dolabı",
                    CheckType = $"Dolap Aralığı ≤ {maxSpaceM:F0}m",
                    Passed    = spacingOk,
                    Severity  = spacingOk ? "INFO" : "ERROR",
                    Message   = spacingOk
                        ? $"Dolap {elA.Id}: en yakın={nearestM:F1}m ✓"
                        : $"Dolap {elA.Id}: en yakın={nearestM:F1}m > {maxSpaceM:F0}m — YÖNETMELİK AŞILDI",
                });

                if (!coverageOk && spacingOk)
                {
                    results.Add(new ValidationResult
                    {
                        RuleId    = "F03-HORTUM",
                        ElementId = Rv.IdStr(elA.Id),
                        Category  = "Yangın Dolabı",
                        CheckType = $"Hortum Erişim ({hoseLenM:F0}m)",
                        Passed    = false,
                        Severity  = "WARNING",
                        Message   = $"Dolap {elA.Id}: komşu={nearestM:F1}m, hortum={hoseLenM:F0}m " +
                                    $"— boşlukta kör nokta oluşabilir",
                    });
                }
            }

            int fail = results.Count(r => !r.Passed);
            ctx.Log($"  fire_hose_cabinet_spacing_check: {cabinets.Count} dolap, " +
                    $"{fail} sorun bulundu");
            return MakeReport("Yangın Dolabı Mesafe Kontrolü", results);
        }

        // ─────────────────────────────────────────────────────────────────────
        // F04  calc_duct_sheet_weight
        //
        // Formül (PDF 5 Hesaplanmış Parametre mantığı):
        //   Çevre = (W × 2 + H × 2)  mm
        //   Hacim = Çevre × Kalınlık × Uzunluk
        //   Ağırlık = Hacim × Yoğunluk
        //
        // input : List<Element> (Duct) — yoksa params'tan hesap
        // params: thickness_mm   Double  default=0.8  (galvaniz sac kalınlığı)
        //         density_kg_m3  Double  default=7850 (çelik yoğunluğu)
        //         width_mm       Double  (input yoksa zorunlu)
        //         height_mm      Double  (input yoksa zorunlu)
        //         length_m       Double  (input yoksa zorunlu)
        //
        // output: List<Dict>
        //   duct_id?, width_mm, height_mm, length_m, perimeter_mm,
        //   thickness_mm, sheet_volume_m3, sheet_weight_kg
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("calc_duct_sheet_weight",
            Description = "Kanal çevre × kalınlık × uzunluk × yoğunluk → sac ağırlığı (kg). " +
                          "Input: List<Duct> veya params (width_mm, height_mm, length_m). " +
                          "Sadece dikdörtgen kanallar hesaplanır.",
            Category    = "Yangın Hesap")]
        public static List<Dictionary<string, object?>> CalcDuctSheetWeight(OpContext ctx)
        {
            var ducts      = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            double thickMm = ctx.GetDouble("thickness_mm",   0.8);
            double density = ctx.GetDouble("density_kg_m3", 7850.0);

            var rows = new List<Dictionary<string, object?>>();

            bool hasRealDucts = ducts.Any(e =>
                Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_DuctCurves);

            if (hasRealDucts)
            {
                foreach (var el in ducts)
                {
                    if (Rv.GetCategoryId(el) != (int)BuiltInCategory.OST_DuctCurves) continue;

                    double wMm  = (el.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)
                                     ?.AsDouble() ?? 0) * 304.8;
                    double hMm  = (el.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)
                                     ?.AsDouble() ?? 0) * 304.8;
                    double lenFt = el.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)
                                     ?.AsDouble() ?? 0;
                    double lenM = lenFt * 0.3048;

                    // Dairesel kanalları atla
                    if (wMm <= 0 || hMm <= 0) continue;

                    rows.Add(BuildDuctRow(Rv.IdStr(el.Id), wMm, hMm, lenM, thickMm, density));
                }
            }
            else
            {
                double wMm  = ctx.GetDouble("width_mm",  0);
                double hMm  = ctx.GetDouble("height_mm", 0);
                double lenM = ctx.GetDouble("length_m",  0);

                if (wMm <= 0 || hMm <= 0 || lenM <= 0)
                {
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["status"]  = "PARAM_MISSING",
                        ["message"] = "Kanal listesi veya width_mm + height_mm + length_m gereklidir."
                    });
                }
                else
                {
                    rows.Add(BuildDuctRow(null, wMm, hMm, lenM, thickMm, density));
                }
            }

            double totalKg = rows
                .Where(r => r.ContainsKey("sheet_weight_kg"))
                .Sum(r => (double)(r["sheet_weight_kg"] ?? 0.0));

            ctx.Log($"  calc_duct_sheet_weight: {rows.Count} kanal → " +
                    $"toplam {totalKg:F1} kg sac ağırlığı");
            return rows;
        }

        private static Dictionary<string, object?> BuildDuctRow(
            string? ductId, double wMm, double hMm,
            double lenM, double thickMm, double density)
        {
            double perimMm  = (wMm + hMm) * 2.0;
            double volM3    = (perimMm / 1000.0) * (thickMm / 1000.0) * lenM;
            double weightKg = volM3 * density;

            return new Dictionary<string, object?>
            {
                ["duct_id"]        = ductId,
                ["width_mm"]       = Math.Round(wMm,      1),
                ["height_mm"]      = Math.Round(hMm,      1),
                ["length_m"]       = Math.Round(lenM,     3),
                ["perimeter_mm"]   = Math.Round(perimMm,  1),
                ["thickness_mm"]   = thickMm,
                ["density_kg_m3"]  = density,
                ["sheet_volume_m3"]= Math.Round(volM3,    6),
                ["sheet_weight_kg"]= Math.Round(weightKg, 3),
            };
        }

        private static ValidationReport MakeReport(string title, List<ValidationResult> results)
            => new()
            {
                ManifestTitle = title,
                Results       = results,
                Passed        = results.Count(r => r.Passed),
                Failed        = results.Count(r => !r.Passed),
            };
    }
}
