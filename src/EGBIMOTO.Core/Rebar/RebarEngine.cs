using System;
using System.Collections.Generic;
using System.Linq;

namespace EGBIMOTO.Core.Rebar
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TS 500:2000 — Beton ve donatı sabitleri
    // ═══════════════════════════════════════════════════════════════════════════

    public static class TS500
    {
        public static readonly Dictionary<string, double> ConcreteClasses =
            new(StringComparer.OrdinalIgnoreCase)
            { ["C16"]=16,["C20"]=20,["C25"]=25,["C30"]=30,["C35"]=35,["C40"]=40,["C45"]=45,["C50"]=50 };

        public static readonly Dictionary<string, double> SteelClasses =
            new(StringComparer.OrdinalIgnoreCase)
            { ["B220C"]=220,["B420C"]=420,["B500C"]=500 };

        /// <summary>Pas payı (mm) — TS500 Tablo 3 / çevre sınıfı</summary>
        public static double CoverMm(string exposureClass) =>
            exposureClass.ToUpperInvariant() switch
            { "XC1"=>15,"XC2"=>25,"XC3"=>30,"XC4"=>35,"XD1"=>40,"XD2"=>45,"XS1"=>40,_=>25 };

        /// <summary>Minimum bindirme / filiz boyu (mm) — TS500 Md. 9.5</summary>
        public static double LapLength(double diaMm, string steelClass, string concreteClass)
        {
            double fck = ConcreteClasses.TryGetValue(concreteClass, out var c) ? c : 25;
            double fyk = SteelClasses.TryGetValue(steelClass, out var s) ? s : 420;
            double fbd = 1.5 * Math.Sqrt(fck);
            double lb  = (fyk / (4.0 * fbd)) * diaMm;
            return Math.Round(Math.Max(lb * 1.3, 300), 0);
        }

        /// <summary>Kıvrım iç çapı (mm) — TS500 Md. 9.2</summary>
        public static double BendDiameter(double diaMm, bool isStirrup = false)
            => isStirrup ? Math.Max(4 * diaMm, 30) : 5 * diaMm;

        public static double BarArea(double diaMm) => Math.PI * diaMm * diaMm / 4.0;
        public static double UnitWeight(double diaMm) => diaMm * diaMm * 0.00617; // kg/m
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TBDY 2018 — Deprem yönetmeliği donatı kuralları
    // ═══════════════════════════════════════════════════════════════════════════

    public static class TBDY2018
    {
        public static double MinLongRatio(string elementType) =>
            elementType.ToUpperInvariant() switch
            { "COLUMN"=>0.01,"BEAM"=>0.0033,"SHEAR_WALL"=>0.0025,"SLAB"=>0.0020,_=>0.0033 };

        public static double MaxLongRatio(string elementType) =>
            elementType.ToUpperInvariant() switch
            { "COLUMN"=>0.04,"BEAM"=>0.04,"SHEAR_WALL"=>0.015,_=>0.04 };

        public static double MaxStirrupSpacing(string elementType, double smallerDimMm, bool inConfinement)
            => elementType.ToUpperInvariant() switch
            {
                "COLUMN" => inConfinement
                    ? Math.Min(smallerDimMm / 3.0, Math.Min(8 * 16.0, 150))
                    : Math.Min(smallerDimMm / 2.0, 200),
                "BEAM"   => inConfinement
                    ? Math.Min(smallerDimMm / 4.0, Math.Min(8 * 16.0, 150))
                    : Math.Min(smallerDimMm / 2.0, 250),
                _        => 200
            };

        public static double MinStirrupDiameter(string elementType) => 8.0;

        public static double ConfinementZoneHeight(double freeHeightMm, double sectionDimMm)
            => Math.Max(Math.Max(freeHeightMm / 6.0, sectionDimMm), 500);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Veri modelleri
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class RebarRecord
    {
        public string ElementId       { get; set; } = "";
        public string ElementType     { get; set; } = "COLUMN"; // COLUMN/BEAM/SHEAR_WALL/SLAB
        public string ElementMark     { get; set; } = "";
        public string RebarShape      { get; set; } = "00";
        public double DiameterMm      { get; set; }
        public string SteelClass      { get; set; } = "B420C";
        public string ConcreteClass   { get; set; } = "C25";
        public double LengthMm        { get; set; }
        public int    Count           { get; set; } = 1;
        public string Zone            { get; set; } = "MID"; // START/MID/END/STIRRUP
        public string LevelName       { get; set; } = "";
        public string PozCode         { get; set; } = "";

        public double TotalLengthMm  => LengthMm * Count;
        public double WeightKg       => Math.Round(TotalLengthMm / 1000.0 * TS500.UnitWeight(DiameterMm), 3);
        public double AreaMm2        => TS500.BarArea(DiameterMm) * Count;
        public string Designation    => $"ø{DiameterMm:F0}";
    }

    public sealed class RebarSummaryRow
    {
        public string DiameterClass  { get; set; } = "";
        public double DiameterMm     { get; set; }
        public string SteelClass     { get; set; } = "";
        public int    BarCount       { get; set; }
        public double TotalLengthM   { get; set; }
        public double TotalWeightKg  { get; set; }
        public string PozCode        { get; set; } = "";
    }

    public sealed class RebarValidationResult
    {
        public string ElementId    { get; set; } = "";
        public string ElementType  { get; set; } = "";
        public string CheckCode    { get; set; } = "";
        public string Standard     { get; set; } = "";
        public bool   Passed       { get; set; }
        public string Message      { get; set; } = "";
        public double ActualValue  { get; set; }
        public double LimitValue   { get; set; }
        public string Unit         { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Hesap motoru
    // ═══════════════════════════════════════════════════════════════════════════

    public static class RebarEngine
    {
        public static List<RebarValidationResult> ValidateElement(
            string elementId, string elementType, double grossAreaMm2,
            double smallerDimMm, List<RebarRecord> longBars, List<RebarRecord> stirrups,
            string concreteClass = "C25", bool seismicDesign = true)
        {
            var r = new List<RebarValidationResult>();

            // ── 1. Boyuna donatı oranı ─────────────────────────────────────────
            double longArea = longBars.Sum(b => b.AreaMm2);
            double rho      = grossAreaMm2 > 0 ? longArea / grossAreaMm2 : 0;
            double rhoMin   = TBDY2018.MinLongRatio(elementType);
            double rhoMax   = TBDY2018.MaxLongRatio(elementType);

            r.Add(Chk(elementId, elementType, "TBDY-7.3-RHO-MIN", "TBDY 2018",
                rho >= rhoMin, rho * 100, rhoMin * 100, "%",
                rho >= rhoMin
                    ? $"Boyuna donatı oranı OK: %{rho*100:F3} ≥ %{rhoMin*100:F3}"
                    : $"HATA: Boyuna donatı oranı yetersiz: %{rho*100:F3} < %{rhoMin*100:F3}"));

            r.Add(Chk(elementId, elementType, "TBDY-7.3-RHO-MAX", "TBDY 2018",
                rho <= rhoMax, rho * 100, rhoMax * 100, "%",
                rho <= rhoMax
                    ? $"Maks. donatı oranı OK: %{rho*100:F3} ≤ %{rhoMax*100:F3}"
                    : $"HATA: Donatı oranı fazla: %{rho*100:F3} > %{rhoMax*100:F3}"));

            // ── 2. Etriye çapı ────────────────────────────────────────────────
            double minEtrDia = TBDY2018.MinStirrupDiameter(elementType);
            foreach (var grp in stirrups.GroupBy(s => s.DiameterMm))
            {
                r.Add(Chk(elementId, elementType, "TBDY-7.3-ETR-DIA", "TBDY 2018",
                    grp.Key >= minEtrDia, grp.Key, minEtrDia, "mm",
                    grp.Key >= minEtrDia
                        ? $"Etriye çapı OK: ø{grp.Key} ≥ ø{minEtrDia}"
                        : $"HATA: Etriye çapı yetersiz: ø{grp.Key} < ø{minEtrDia}"));
            }

            // ── 3. Etriye aralığı (sargı bölgesi) ────────────────────────────
            if (seismicDesign)
            {
                double maxConf = TBDY2018.MaxStirrupSpacing(elementType, smallerDimMm, true);
                double maxMid  = TBDY2018.MaxStirrupSpacing(elementType, smallerDimMm, false);

                var confStir = stirrups.Where(s => s.Zone == "START" || s.Zone == "END").ToList();
                var midStir  = stirrups.Where(s => s.Zone == "MID").ToList();

                if (confStir.Any())
                {
                    double sp = confStir.First().LengthMm;
                    r.Add(Chk(elementId, elementType, "TBDY-7.3-ETR-CONF", "TBDY 2018",
                        sp <= maxConf, sp, maxConf, "mm",
                        sp <= maxConf
                            ? $"Sargı etriye aralığı OK: {sp} ≤ {maxConf} mm"
                            : $"HATA: Sargı etriye aralığı fazla: {sp} > {maxConf} mm"));
                }
                if (midStir.Any())
                {
                    double sp = midStir.First().LengthMm;
                    r.Add(Chk(elementId, elementType, "TBDY-7.3-ETR-MID", "TBDY 2018",
                        sp <= maxMid, sp, maxMid, "mm",
                        sp <= maxMid
                            ? $"Orta bölge etriye aralığı OK: {sp} ≤ {maxMid} mm"
                            : $"HATA: Orta bölge etriye aralığı fazla: {sp} > {maxMid} mm"));
                }
            }

            // ── 4. Pas payı (TS500 XC2 varsayım) ─────────────────────────────
            r.Add(Chk(elementId, elementType, "TS500-COVER-XC2", "TS 500",
                true, 0, TS500.CoverMm("XC2"), "mm",
                $"TS500 XC2 min. pas payı referans: {TS500.CoverMm("XC2")} mm"));

            return r;
        }

        // ── Hesap yardımcıları (RebarOps tarafından çağrılır) ──────────────────

        /// <summary>Donatı ağırlığını kg olarak döner. dia: mm, len: m</summary>
        public static double CalcWeight(double diaMm, double lenM)
            => Math.Round(lenM * TS500.UnitWeight(diaMm), 4);

        /// <summary>TS500 bindirme boyu hesaplar. Revit op'ları için dict döner.</summary>
        public static Dictionary<string, object?> CalcLapLength(
            double diaMm, double fck, double fyk, double coverMm)
        {
            double fbd      = 1.5 * Math.Sqrt(Math.Max(fck, 12));
            double lb       = (fyk / (4.0 * fbd)) * diaMm;
            double lapMm    = Math.Round(Math.Max(lb * 1.3, 300), 0);
            double filizMm  = Math.Round(Math.Max(lb, 250), 0);
            return new Dictionary<string, object?>
            {
                ["diameter_mm"]   = diaMm,
                ["fck"]           = fck,
                ["fyk"]           = fyk,
                ["cover_mm"]      = coverMm,
                ["lap_length_mm"] = lapMm,
                ["filiz_mm"]      = filizMm,
                ["standard"]      = "TS500:2000 Md.9.5"
            };
        }

        /// <summary>TS500 ankraj boyu hesaplar.</summary>
        public static Dictionary<string, object?> CalcAnchorageLength(
            double diaMm, double fck, double fyk, double coverMm, bool hook = false)
        {
            double fbd     = 1.5 * Math.Sqrt(Math.Max(fck, 12));
            double lb      = (fyk / (4.0 * fbd)) * diaMm;
            double ancMm   = Math.Round(hook ? lb * 0.7 : lb, 0);
            ancMm = Math.Max(ancMm, hook ? 150 : 200);
            return new Dictionary<string, object?>
            {
                ["diameter_mm"]  = diaMm,
                ["fck"]          = fck,
                ["fyk"]          = fyk,
                ["hook"]         = hook,
                ["anchorage_mm"] = ancMm,
                ["standard"]     = "TS500:2000 Md.9.4"
            };
        }

        public static List<RebarSummaryRow> Summarize(IEnumerable<RebarRecord> records) =>
            records
                .GroupBy(r => $"ø{r.DiameterMm:F0} {r.SteelClass}")
                .Select(g => new RebarSummaryRow
                {
                    DiameterClass = g.Key,
                    DiameterMm    = g.First().DiameterMm,
                    SteelClass    = g.First().SteelClass,
                    BarCount      = g.Sum(r => r.Count),
                    TotalLengthM  = Math.Round(g.Sum(r => r.TotalLengthMm) / 1000.0, 2),
                    TotalWeightKg = Math.Round(g.Sum(r => r.WeightKg), 2),
                    PozCode       = g.First().PozCode
                })
                .OrderBy(s => s.DiameterMm)
                .ToList();

        private static RebarValidationResult Chk(
            string eid, string et, string code, string std,
            bool passed, double actual, double limit, string unit, string msg)
            => new() { ElementId=eid, ElementType=et, CheckCode=code, Standard=std,
                       Passed=passed, ActualValue=actual, LimitValue=limit, Unit=unit, Message=msg };

        /// <summary>
        /// Fabrika boyu + TS500 bindirme katsayısı ile net çubuk uzunluğunu hesaplar.
        /// RevitPlugins-master RebarElement._overlapCoefDict mantığından türetilmiştir.
        ///
        /// Türk yapı sektöründe standart fabrika boyu 12m (Rusya'da 11.7m).
        /// TS500 Md.9.5: bindirme boyu = lb × 1.3 ≥ 300mm
        /// Bu metod UZUNLUK × KATSAYI hesabını döner (manuel bindirme boyu hesabından farklı).
        /// </summary>
        public static double ApplyFactoryOverlap(
            double lenMm,
            double diaMm,
            double factoryLenMm = 12000)
        {
            if (lenMm <= factoryLenMm) return lenMm; // bindirme yok

            // TS500 bindirme katsayısı tablosu (çap bazlı)
            var table = new System.Collections.Generic.Dictionary<double, double>
            {
                { 8,  1.034 }, { 10, 1.043 }, { 12, 1.051 }, { 14, 1.060 },
                { 16, 1.068 }, { 18, 1.077 }, { 20, 1.085 }, { 22, 1.094 },
                { 25, 1.107 }, { 28, 1.120 }, { 32, 1.140 }, { 36, 1.160 },
            };

            double coef = table.TryGetValue(Math.Round(diaMm, 0), out var c) ? c : 1.10;
            return lenMm * coef;
        }

        /// <summary>
        /// Çap tablosundan kg/m değeri döner (TS 708 Tablo 2).
        /// </summary>
        public static double KgPerMeter(double diaMm)
        {
            var table = new System.Collections.Generic.Dictionary<double, double>
            {
                { 6, 0.222 }, { 8, 0.395 }, { 10, 0.617 }, { 12, 0.888 },
                { 14, 1.208 }, { 16, 1.578 }, { 18, 1.998 }, { 20, 2.466 },
                { 22, 2.984 }, { 25, 3.853 }, { 28, 4.834 }, { 32, 6.313 },
                { 36, 7.990 }, { 40, 9.865 },
            };
            double nom = Math.Round(diaMm, 0);
            return table.TryGetValue(nom, out var v) ? v : nom * nom * 0.006165;
        }
    }
}
