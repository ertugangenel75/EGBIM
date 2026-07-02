using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Rebar;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Donatı (rebar) metraj ve TS500 doğrulama op'ları.
    ///
    /// FIX #4: Rebar.TotalLength (Revit 2021'den beri obsolete) kaldırıldı.
    ///         Yerine REBAR_ELEM_LENGTH BuiltInParameter kullanılıyor.
    /// </summary>
    public static class RebarOps
    {
        // ── Yardımcı: TotalLength yerine BuiltInParameter ─────────────────────
        /// <summary>
        /// Rebar uzunluğunu feet cinsinden döner.
        /// Rebar.TotalLength Revit 2021'den beri obsolete, Revit 2026'da kaldırılabilir.
        /// REBAR_ELEM_LENGTH tüm versiyonlarda çalışır.
        /// </summary>
        private static double GetRebarLengthFt(Element e)
            => e.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH)?.AsDouble() ?? 0;

        // ── Donatı Metraj ─────────────────────────────────────────────────────
        [EgOp("rebar_weight_table",
            Description = "Donatı listesinin çap, uzunluk ve ağırlık tablosunu döner. {element_id, cap_mm, uzunluk_m, agirlik_kg}",
            Category    = "Donatı")]
        public static List<Dictionary<string, object?>> RebarWeightTable(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var rebars = ctx.InputAsOrDefault<List<Element>>();
            return rebars.Select(e =>
            {
                var rb  = e as Rebar;
                double dia = 0, len = 0, weight = 0;
                if (rb is not null)
                {
                    var barType = rctx.Doc.GetElement(rb.GetTypeId()) as RebarBarType;
                    dia    = (barType?.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0) * 304.8; // feet -> mm
                    len    = GetRebarLengthFt(e) * 0.3048;        // feet -> m
                    weight = RebarEngine.CalcWeight(dia, len);
                }
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["cap_mm"]     = Math.Round(dia, 1),
                    ["uzunluk_m"]  = Math.Round(len, 3),
                    ["agirlik_kg"] = Math.Round(weight, 3),
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? ""
                };
            }).ToList();
        }

        [EgOp("rebar_summary_by_diameter",
            Description = "Donatıları çap bazında gruplar ve toplam ağırlık hesaplar",
            Category    = "Donatı")]
        public static List<Dictionary<string, object?>> RebarSummaryByDiameter(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var rebars = ctx.InputAsOrDefault<List<Element>>();
            return rebars
                .Select(e =>
                {
                    var rb      = e as Rebar;
                    var barType = rb is not null
                        ? rctx.Doc.GetElement(rb.GetTypeId()) as RebarBarType : null;
                    double dia    = (barType?.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0) * 304.8;
                    double len    = GetRebarLengthFt(e) * 0.3048;
                    double weight = RebarEngine.CalcWeight(dia, len);
                    return (dia, len, weight);
                })
                .GroupBy(x => Math.Round(x.dia, 0))
                .Select(g => new Dictionary<string, object?>
                {
                    ["cap_mm"]              = g.Key,
                    ["eleman_sayisi"]       = g.Count(),
                    ["toplam_uzunluk_m"]    = Math.Round(g.Sum(x => x.len), 2),
                    ["toplam_kg"]           = Math.Round(g.Sum(x => x.weight), 2)
                })
                .OrderBy(r => (double)r["cap_mm"]!)
                .ToList();
        }

        [EgOp("rebar_summary_by_level",
            Description = "Donatıları kat bazında gruplar ve toplam ağırlık hesaplar",
            Category    = "Donatı")]
        public static List<Dictionary<string, object?>> RebarSummaryByLevel(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var rebars = ctx.InputAsOrDefault<List<Element>>();
            return rebars
                .Select(e =>
                {
                    var rb      = e as Rebar;
                    var barType = rb is not null
                        ? rctx.Doc.GetElement(rb.GetTypeId()) as RebarBarType : null;
                    double dia    = (barType?.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0) * 304.8;
                    double len    = GetRebarLengthFt(e) * 0.3048;
                    double weight = RebarEngine.CalcWeight(dia, len);
                    string level  = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "—";
                    return (level, weight, len);
                })
                .GroupBy(x => x.level)
                .Select(g => new Dictionary<string, object?>
                {
                    ["kat"]                 = g.Key,
                    ["eleman_sayisi"]       = g.Count(),
                    ["toplam_uzunluk_m"]    = Math.Round(g.Sum(x => x.len), 2),
                    ["toplam_kg"]           = Math.Round(g.Sum(x => x.weight), 2)
                })
                .OrderBy(r => r["kat"]?.ToString())
                .ToList();
        }

        [EgOp("rebar_total_weight",
            Description = "Donatı listesinin toplam ağırlığını kg olarak döner",
            Category    = "Donatı")]
        public static double RebarTotalWeight(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var rebars = ctx.InputAsOrDefault<List<Element>>();
            double total = 0;
            foreach (var e in rebars)
            {
                var rb      = e as Rebar;
                var barType = rb is not null
                    ? rctx.Doc.GetElement(rb.GetTypeId()) as RebarBarType : null;
                double dia = (barType?.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0) * 304.8;
                double len = GetRebarLengthFt(e) * 0.3048;
                total += RebarEngine.CalcWeight(dia, len);
            }
            ctx.Log($"  rebar_total_weight: {total:N2} kg");
            return Math.Round(total, 2);
        }

        // ── TS500 Hesapları ───────────────────────────────────────────────────
        [EgOp("calc_lap_length",
            Description = "TS500'e göre bindirme boyu hesaplar. params: diameter_mm, fck (MPa), fyk (MPa), cover_mm",
            Category    = "Donatı")]
        public static Dictionary<string, object?> CalcLapLength(OpContext ctx)
        {
            double dia   = ctx.GetDouble("diameter_mm", 12);
            double fck   = ctx.GetDouble("fck", 25);
            double fyk   = ctx.GetDouble("fyk", 420);
            double cover = ctx.GetDouble("cover_mm", 30);
            var result   = RebarEngine.CalcLapLength(dia, fck, fyk, cover);
            ctx.Log($"  calc_lap_length Ø{dia}: {result["lap_length_mm"]} mm");
            return result;
        }

        [EgOp("calc_anchorage_length",
            Description = "TS500'e göre ankraj boyu hesaplar. params: diameter_mm, fck, fyk, cover_mm, hook (true/false)",
            Category    = "Donatı")]
        public static Dictionary<string, object?> CalcAnchorageLength(OpContext ctx)
        {
            double dia   = ctx.GetDouble("diameter_mm", 12);
            double fck   = ctx.GetDouble("fck", 25);
            double fyk   = ctx.GetDouble("fyk", 420);
            double cover = ctx.GetDouble("cover_mm", 30);
            bool   hook  = ctx.GetBool("hook", false);
            var result   = RebarEngine.CalcAnchorageLength(dia, fck, fyk, cover, hook);
            ctx.Log($"  calc_anchorage_length Ø{dia}: {result["anchorage_mm"]} mm");
            return result;
        }

        [EgOp("calc_min_spacing",
            Description = "TS500'e göre minimum donatı aralığını hesaplar. params: diameter_mm, aggregate_size_mm",
            Category    = "Donatı")]
        public static Dictionary<string, object?> CalcMinSpacing(OpContext ctx)
        {
            double dia  = ctx.GetDouble("diameter_mm", 12);
            double agg  = ctx.GetDouble("aggregate_size_mm", 20);
            double minS = Math.Max(dia, Math.Max(agg + 5, 25));
            ctx.Log($"  calc_min_spacing Ø{dia}: {minS} mm");
            return new() { ["diameter_mm"] = dia, ["aggregate_mm"] = agg, ["min_spacing_mm"] = minS };
        }

        // ── TS500 Doğrulama ───────────────────────────────────────────────────
        [EgOp("validate_rebar_ts500",
            Description = "Donatı listesini TS500 kurallarına göre doğrular. params: fck (MPa), fyk (MPa), cover_mm",
            Category    = "Donatı")]
        public static ValidationReport ValidateRebarTs500(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var rebars = ctx.InputAsOrDefault<List<Element>>();
            double fck   = ctx.GetDouble("fck", 25);
            double fyk   = ctx.GetDouble("fyk", 420);
            double cover = ctx.GetDouble("cover_mm", 30);
            var results  = new List<ValidationResult>();
            foreach (var e in rebars)
            {
                var rb      = e as Rebar;
                var barType = rb is not null
                    ? rctx.Doc.GetElement(rb.GetTypeId()) as RebarBarType : null;
                double dia = (barType?.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0) * 304.8;
                double len = GetRebarLengthFt(e) * 0.3048 * 1000; // mm

                // Minimum çap kontrolü (TS500: min 8mm)
                bool diaOk = dia >= 8;
                results.Add(new ValidationResult
                {
                    RuleId    = "TS500_MIN_DIA",
                    ElementId = Rv.IdStr(e.Id),
                    Category  = "Rebar",
                    CheckType = "MinDiameter",
                    Passed    = diaOk,
                    Message   = diaOk ? $"Ø{dia:F0} OK" : $"Ø{dia:F0} < 8mm (TS500 min)",
                    Severity  = diaOk ? "INFO" : "ERROR"
                });

                // Minimum uzunluk kontrolü (bindirme boyu)
                var lap    = RebarEngine.CalcLapLength(dia, fck, fyk, cover);
                double minLap = lap.TryGetValue("lap_length_mm", out var lv) &&
                                double.TryParse(lv?.ToString(), out var ld) ? ld : 0;
                bool lenOk = len >= minLap || len == 0;
                results.Add(new ValidationResult
                {
                    RuleId    = "TS500_LAP_LENGTH",
                    ElementId = Rv.IdStr(e.Id),
                    Category  = "Rebar",
                    CheckType = "LapLength",
                    Passed    = lenOk,
                    Message   = lenOk ? $"Uzunluk {len:F0}mm OK"
                                      : $"Uzunluk {len:F0}mm < min bindirme {minLap:F0}mm",
                    Severity  = lenOk ? "INFO" : "WARNING"
                });
            }
            var report = new ValidationReport
            {
                ManifestTitle = "TS500 Donatı Doğrulama",
                TotalChecks   = results.Count,
                Passed        = results.Count(r => r.Passed),
                Failed        = results.Count(r => !r.Passed && r.Severity == "ERROR"),
                Warnings      = results.Count(r => !r.Passed && r.Severity == "WARNING"),
                Results       = results
            };
            ctx.Log($"  validate_rebar_ts500: {report.Summary}");
            return report;
        }

        // ── Fabrika Bindirme Katsayısı Op'u (RevitPlugins-master / RebarElement mantığı) ──────

        [EgOp("rebar_weight_calc",
            Description =
                "Donatı listesini çap tablosu + fabrika boyu + bindirme katsayısıyla hesaplar.\n" +
                "RevitPlugins-master RebarElement.Calculate() mantığından türetilmiştir.\n\n" +
                "Fark: TS500 bindirme uzunlukları (Türk standardı) kullanılır.\n" +
                "Fabrika uzunluğunu aşan çubuklarda bindirme katsayısı otomatik uygulanır.\n\n" +
                "params:\n" +
                "  factory_length_mm — fabrika çubuk boyu (opsiyonel, default: 12000 — Türkiye)\n" +
                "  fck               — beton sınıfı MPa (opsiyonel, default: 25)\n" +
                "  fyk               — çelik sınıfı MPa (opsiyonel, default: 420)\n" +
                "  cover_mm          — pas payı mm (opsiyonel, default: 30)\n" +
                "  apply_overlap     — bindirme katsayısı uygulansın mı (opsiyonel, default: true)\n\n" +
                "Input: collect_rebar çıktısı (List<Element>).\n" +
                "Çıktı: List<Dictionary> — element_id, cap_mm, uzunluk_m, kg_per_m,\n" +
                "  fabrika_adet, bindirme_katsayisi, net_uzunluk_m, agirlik_kg, kat.",
            Category = "Donatı")]
        public static List<Dictionary<string, object?>> RebarWeightCalc(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] rebar_weight_calc Revit bağlamı gerektirir.");

            var rebars          = ctx.InputAsOrDefault<List<Element>>();
            double factoryLenMm = ctx.GetDouble("factory_length_mm", 12000); // Türkiye standart boy
            double fck          = ctx.GetDouble("fck",     25);
            double fyk          = ctx.GetDouble("fyk",     420);
            double coverMm      = ctx.GetDouble("cover_mm", 30);
            bool   applyOverlap = ctx.GetBool("apply_overlap", true);

            // ── Çap → kg/m tablosu (TS 708 Tablo 2 / evrensel çelik yoğunluğu) ──────────
            // Formül: kg/m = d² × 0.006165  (d: mm cinsinden)
            // Revit'te doğrudan bu tablodan okuma yapılır çünkü parametre olmayabilir.
            var kgPerMTable = new Dictionary<double, double>
            {
                { 6,  0.222 }, { 8,  0.395 }, { 10, 0.617 }, { 12, 0.888 },
                { 14, 1.208 }, { 16, 1.578 }, { 18, 1.998 }, { 20, 2.466 },
                { 22, 2.984 }, { 25, 3.853 }, { 28, 4.834 }, { 32, 6.313 },
                { 36, 7.990 }, { 40, 9.865 },
            };

            // ── Bindirme katsayısı tablosu (TS 500 Md. 9.5 referanslı) ──────────────────
            // Fabrika boyunu aşan çubuklar bindirme ile uzatılır.
            var overlapCoefTable = new Dictionary<double, double>
            {
                { 8,  1.034 }, { 10, 1.043 }, { 12, 1.051 }, { 14, 1.060 },
                { 16, 1.068 }, { 18, 1.077 }, { 20, 1.085 }, { 22, 1.094 },
                { 25, 1.107 }, { 28, 1.120 }, { 32, 1.140 }, { 36, 1.160 },
            };
            const double DEFAULT_OVERLAP_COEF = 1.10; // bilinmeyen çaplar için

            var results = new List<Dictionary<string, object?>>();
            double toplamAgirlik = 0;

            foreach (var e in rebars)
            {
                try
                {
                    var rb      = e as Rebar;
                    var barType = rb is not null
                        ? rctx.Doc.GetElement(rb.GetTypeId()) as RebarBarType
                        : null;

                    // ── Çap (mm) ─────────────────────────────────────────────
                    double diaMm = 0;
                    if (barType != null)
                    {
                        var diaParam = barType.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER);
                        if (diaParam != null)
                            diaMm = UnitUtils.ConvertFromInternalUnits(
                                diaParam.AsDouble(), UnitTypeId.Millimeters);
                    }
                    // Yuvarlakla (nominal çap: 8, 10, 12...)
                    double diaNominal = Math.Round(diaMm, 0);

                    // ── kg/m — tablo veya formül ──────────────────────────────
                    double kgPerM = kgPerMTable.TryGetValue(diaNominal, out var tableVal)
                        ? tableVal
                        : diaNominal * diaNominal * 0.006165; // TS708 formülü

                    // ── Uzunluk (mm) ──────────────────────────────────────────
                    double lenFt = GetRebarLengthFt(e);
                    double lenMm = UnitUtils.ConvertFromInternalUnits(lenFt, UnitTypeId.Millimeters);

                    if (lenMm < 0.1) continue; // sıfır uzunluklu atla

                    // ── Fabrika adedi + bindirme katsayısı ────────────────────
                    // Fabrika boyunu aşıyorsa → kaç çubuk gerekir?
                    int fabrikaAdet = (int)Math.Ceiling(lenMm / factoryLenMm);

                    double overlapCoef = 1.0;
                    if (applyOverlap && lenMm > factoryLenMm)
                    {
                        // TS500 bindirme katsayısı — çap bazlı
                        overlapCoef = overlapCoefTable.TryGetValue(diaNominal, out var oc)
                            ? oc
                            : DEFAULT_OVERLAP_COEF;
                    }

                    // ── Net uzunluk (bindirme dahil) ──────────────────────────
                    double netLenMm = lenMm * overlapCoef;
                    double netLenM  = netLenMm / 1000.0;

                    // ── Adet (rebar array veya tekil) ─────────────────────────
                    int adet = 1;
                    if (rb != null)
                    {
                        var countParam = e.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS);
                        if (countParam != null && countParam.AsInteger() > 0)
                            adet = countParam.AsInteger();
                    }

                    // ── Ağırlık (kg) ──────────────────────────────────────────
                    double agirlik = Math.Round(netLenM * kgPerM * adet, 3);
                    toplamAgirlik += agirlik;

                    var level  = rctx.Doc.GetElement(e.LevelId) as Level;

                    results.Add(new Dictionary<string, object?>
                    {
                        ["element_id"]          = e.Id.Value,
                        ["cap_mm"]              = diaNominal,
                        ["uzunluk_m"]           = Math.Round(lenMm / 1000, 3),
                        ["kg_per_m"]            = kgPerM,
                        ["fabrika_adet"]        = fabrikaAdet,
                        ["bindirme_katsayisi"]  = Math.Round(overlapCoef, 3),
                        ["net_uzunluk_m"]       = Math.Round(netLenM, 3),
                        ["adet"]                = adet,
                        ["agirlik_kg"]          = agirlik,
                        ["kat"]                 = level?.Name ?? "",
                    });
                }
                catch (Exception ex)
                {
                    ctx.Log($"  ✗ Donatı [{e.Id}] hesap hatası: {ex.Message}");
                }
            }

            ctx.Log($"  rebar_weight_calc: {results.Count} çubuk, toplam {toplamAgirlik:F1} kg");
            return results;
        }

    }
}
