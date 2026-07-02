using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — MEP Denetim Op'ları (MepInspectOps)
    /// Tier B op'larında pre-check: eksik parametre tespiti → kullanıcıya mesaj.
    ///
    ///   duct_aspect_ratio_check   — Dikdörtgen kanal W:H oranı (≤4:1)
    ///   cable_tray_fill_check     — Kablo tava doluluk oranı (≤%70)
    ///   conduit_fill_check        — Boru doluluk oranı (≤%40, NEC/IEC)
    ///   valve_type_classify       — Vana tipini keyword'den sınıflandır
    ///   panel_phase_balance_check — Panel faz dengesizliği (≤%10)
    ///   sprinkler_head_schedule   — Sprinkler schedule (tip/zone/kat)
    ///   fa_device_schedule        — Yangın alarm cihaz schedule
    ///   lighting_emergency_check  — Acil aydınlatma devre kontrolü
    /// </summary>
    public static class MepInspectOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // I01  duct_aspect_ratio_check
        // input : List<Element>  (Duct — dikdörtgen)
        // params: max_ratio     Double  default=4.0
        //         severity      String  default=WARNING
        // output: ValidationReport
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("duct_aspect_ratio_check",
            Description = "Dikdörtgen kanalların en/boy oranını kontrol eder (ASHRAE: ≤4:1). " +
                          "Dairesel kanallar atlanır. Çıktı: ValidationReport.",
            Category    = "MEP Denetim")]
        public static ValidationReport DuctAspectRatioCheck(OpContext ctx)
        {
            var ducts    = ctx.InputAs<List<Element>>();
            double maxR  = ctx.GetDouble("max_ratio", 4.0);
            string sev   = ctx.GetString("severity",  "WARNING");
            var results  = new List<ValidationResult>();

            foreach (var el in ducts)
            {
                if (el is not Duct duct) continue;

                // Sadece dikdörtgen kanal
                var shape = duct.DuctType?.Shape;
                if (shape != ConnectorProfileType.Rectangular) continue;

                double wMm = duct.Width  * 304.8;
                double hMm = duct.Height * 304.8;
                if (hMm <= 0) continue;

                double ratio = wMm / hMm;
                // Küçük kenar her zaman bölen olsun (her iki yön için)
                if (ratio < 1) ratio = 1 / ratio;

                string ratioLabel = ratio switch
                {
                    <= 1.0 => "1:1 (en iyi)",
                    <= 2.0 => "2:1 (iyi)",
                    <= 3.0 => "3:1 (kabul edilebilir)",
                    <= 4.0 => "4:1 (maksimum)",
                    _      => $"{ratio:F1}:1 (AŞILDI)",
                };

                bool ok = ratio <= maxR;
                results.Add(new ValidationResult
                {
                    RuleId    = "I01-ORAN",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "Kanal",
                    CheckType = $"En/Boy ≤ {maxR:F0}:1",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : sev,
                    Message   = ok
                        ? $"Kanal {el.Id}: {wMm:F0}×{hMm:F0}mm → {ratioLabel} ✓"
                        : $"Kanal {el.Id}: {wMm:F0}×{hMm:F0}mm → {ratio:F1}:1 — limit {maxR:F0}:1 aşıldı",
                });
            }

            int fail = results.Count(r => !r.Passed);
            ctx.Log($"  duct_aspect_ratio_check: {results.Count} kanal, {fail} hata");
            return MakeReport("Kanal En/Boy Oranı", results);
        }

        // ─────────────────────────────────────────────────────────────────────
        // I02  cable_tray_fill_check
        // input : List<Element>  (CableTray)
        // params: fill_param     String  default="EG_KabloDoluluk"
        //                        (Tava doluluk % parametresi)
        //         max_fill_pct   Double  default=70
        //
        // Pre-check: fill_param elemanda mevcut mu?
        // output: ValidationReport
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("cable_tray_fill_check",
            Description = "Kablo tava doluluk oranını kontrol eder (standart: ≤%70). " +
                          "Pre-check: fill_param (default EG_KabloDoluluk) tavada yoksa eklenmesi istenir.",
            Category    = "MEP Denetim")]
        public static ValidationReport CableTrayFillCheck(OpContext ctx)
        {
            var trays       = ctx.InputAs<List<Element>>();
            string fillPrm  = ctx.GetString("fill_param",   "EG_KabloDoluluk");
            double maxFill  = ctx.GetDouble("max_fill_pct", 70.0);
            var results     = new List<ValidationResult>();

            // ── Pre-check ─────────────────────────────────────────────────────
            var sample = trays.FirstOrDefault();
            if (sample != null)
            {
                var missing = CheckRequiredParams(sample, new[] { fillPrm });
                if (missing.Count > 0)
                {
                    ctx.Log($"  cable_tray_fill_check: PRE-CHECK — {fillPrm} eksik");
                    return MakeReport("Kablo Tava Doluluk",
                        new List<ValidationResult>
                        {
                            new()
                            {
                                RuleId    = "PRE-CHECK",
                                ElementId = Rv.IdStr(sample.Id),
                                Category  = "Kablo Tava",
                                CheckType = "Parametre Kontrolü",
                                Passed    = false,
                                Severity  = "ERROR",
                                Message   = $"'{fillPrm}' parametresi kablo tavası elemanında bulunamadı. " +
                                            $"Shared Parameter olarak ekleyin (Yüzde/Number tipi). " +
                                            $"Örn: EG_KabloDoluluk = 65 → %65 doluluk.",
                            }
                        });
                }
            }

            // ── Asıl kontrol ─────────────────────────────────────────────────
            foreach (var el in trays)
            {
                var fp = el.LookupParameter(fillPrm);
                if (fp == null) continue;

                double fillPct = fp.AsDouble();
                bool   ok      = fillPct <= maxFill;

                // Kablo tava boyutları (bilgi amaçlı)
                double wMm = (el.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM)
                                ?.AsDouble() ?? 0) * 304.8;
                double hMm = (el.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM)
                                ?.AsDouble() ?? 0) * 304.8;

                results.Add(new ValidationResult
                {
                    RuleId    = "I02-DOLULUK",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "Kablo Tava",
                    CheckType = $"Doluluk ≤ %{maxFill:F0}",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "WARNING",
                    Message   = ok
                        ? $"Tava {el.Id} ({wMm:F0}×{hMm:F0}mm): %{fillPct:F1} ✓"
                        : $"Tava {el.Id} ({wMm:F0}×{hMm:F0}mm): %{fillPct:F1} > %{maxFill:F0}",
                });
            }

            int fail = results.Count(r => !r.Passed);
            ctx.Log($"  cable_tray_fill_check: {results.Count} tava, {fail} aşım");
            return MakeReport("Kablo Tava Doluluk Kontrolü", results);
        }

        // ─────────────────────────────────────────────────────────────────────
        // I03  conduit_fill_check
        // input : List<Element>  (Conduit)
        // params: count_param    String  default="EG_IletkenSayisi"
        //         area_param     String  default="EG_IletkenKesit_mm2"
        //         max_fill_pct   Double  default=40 (NEC 3+ iletken)
        //
        // Pre-check: count_param + area_param elemanda var mı?
        // output: ValidationReport
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("conduit_fill_check",
            Description = "Boru (conduit) iletken doluluk oranını kontrol eder (NEC/IEC: ≤%40). " +
                          "Pre-check: EG_IletkenSayisi + EG_IletkenKesit_mm2 yoksa eklenmesi istenir.",
            Category    = "MEP Denetim")]
        public static ValidationReport ConduitFillCheck(OpContext ctx)
        {
            var conduits   = ctx.InputAs<List<Element>>();
            string cntPrm  = ctx.GetString("count_param",   "EG_IletkenSayisi");
            string areaPrm = ctx.GetString("area_param",    "EG_IletkenKesit_mm2");
            double maxFill = ctx.GetDouble("max_fill_pct",  40.0);
            var results    = new List<ValidationResult>();

            // ── Pre-check ─────────────────────────────────────────────────────
            var sample = conduits.FirstOrDefault();
            if (sample != null)
            {
                var missing = CheckRequiredParams(sample, new[] { cntPrm, areaPrm });
                if (missing.Count > 0)
                {
                    ctx.Log($"  conduit_fill_check: PRE-CHECK — eksik: {string.Join(", ", missing)}");
                    return MakeReport("Boru Doluluk",
                        new List<ValidationResult>
                        {
                            new()
                            {
                                RuleId    = "PRE-CHECK",
                                ElementId = Rv.IdStr(sample.Id),
                                Category  = "Boru",
                                CheckType = "Parametre Kontrolü",
                                Passed    = false,
                                Severity  = "ERROR",
                                Message   = $"Eksik parametreler: {string.Join(", ", missing)}. " +
                                            $"Shared Parameter olarak ekleyin. " +
                                            $"{cntPrm} = iletken adedi (Integer), " +
                                            $"{areaPrm} = tek iletken kesit alanı mm² (Number).",
                            }
                        });
                }
            }

            // ── Asıl kontrol ─────────────────────────────────────────────────
            foreach (var el in conduits)
            {
                var cp = el.LookupParameter(cntPrm);
                var ap = el.LookupParameter(areaPrm);
                if (cp == null || ap == null) continue;

                int    conductorCount = cp.AsInteger();
                double conductorArea  = ap.AsDouble(); // mm²

                // Conduit iç çapı
                double diamMm = (el.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM)
                                   ?.AsDouble() ?? 0) * 304.8;
                if (diamMm <= 0) continue;

                double innerArea  = Math.PI * Math.Pow(diamMm / 2.0, 2); // mm²
                double totalCond  = conductorCount * conductorArea;       // mm²
                double fillPct    = innerArea > 0 ? (totalCond / innerArea) * 100.0 : 0;
                bool   ok         = fillPct <= maxFill;

                results.Add(new ValidationResult
                {
                    RuleId    = "I03-DOLULUK",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "Boru",
                    CheckType = $"İletken Doluluk ≤ %{maxFill:F0}",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "ERROR",
                    Message   = ok
                        ? $"Boru {el.Id} (Ø{diamMm:F0}mm): {conductorCount}×{conductorArea:F1}mm² → %{fillPct:F1} ✓"
                        : $"Boru {el.Id} (Ø{diamMm:F0}mm): {conductorCount}×{conductorArea:F1}mm² → %{fillPct:F1} > %{maxFill:F0}",
                });
            }

            int fail = results.Count(r => !r.Passed);
            ctx.Log($"  conduit_fill_check: {results.Count} boru, {fail} aşım");
            return MakeReport("Boru İletken Doluluk Kontrolü", results);
        }

        // ─────────────────────────────────────────────────────────────────────
        // I04  valve_type_classify
        // input : List<Element>  (PipeFitting / PipeAccessory)
        // params: lang  String  default="auto" | "tr" | "en"
        // output: List<Dict>
        //   element_id, family_name, type_name, valve_class,
        //   system_type, diameter_mm
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("valve_type_classify",
            Description = "Boru fitting/aksesuarlarını isim keyword'üne göre " +
                          "gate/globe/ball/butterfly/check/relief olarak sınıflandırır.",
            Category    = "MEP Denetim")]
        public static List<Dictionary<string, object?>> ValveTypeClassify(OpContext ctx)
        {
            var elements = ctx.InputAs<List<Element>>();
            var rows     = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                if (el is not FamilyInstance fi) continue;

                string fname = fi.Symbol?.FamilyName ?? "";
                string tname = fi.Symbol?.Name        ?? "";
                string combined = $"{fname} {tname}".ToLowerInvariant();

                string valveClass = ClassifyValve(combined);

                // Sistem tipi
                string systemType = "";
                var sysParam = el.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
                if (sysParam != null) systemType = sysParam.AsValueString() ?? "";

                // Çap
                double diamMm = 0;
                var dParam = el.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
                          ?? el.LookupParameter("Nominal Diameter")
                          ?? el.LookupParameter("Nominal Radius"); // fallback
                if (dParam != null) diamMm = dParam.AsDouble() * 304.8;

                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"]  = Rv.IdStr(el.Id),
                    ["family_name"] = fname,
                    ["type_name"]   = tname,
                    ["valve_class"] = valveClass,
                    ["system_type"] = systemType,
                    ["diameter_mm"] = Math.Round(diamMm, 0),
                });
            }

            ctx.Log($"  valve_type_classify: {rows.Count} eleman sınıflandırıldı");
            return rows;
        }

        private static string ClassifyValve(string text)
        {
            // Türkçe + İngilizce keyword tablosu
            if (ContainsAny(text, "gate", "sürgülü", "surgulu", "os&y", "osy"))
                return "GATE";
            if (ContainsAny(text, "globe", "küresel oturmalı", "kuresel oturmali"))
                return "GLOBE";
            if (ContainsAny(text, "ball", "küresel", "kuresel") &&
                !ContainsAny(text, "oturmalı", "oturmali"))
                return "BALL";
            if (ContainsAny(text, "butterfly", "kelebek"))
                return "BUTTERFLY";
            if (ContainsAny(text, "check", "çek", "cek", "non-return", "nrv"))
                return "CHECK";
            if (ContainsAny(text, "relief", "safety", "emniyet", "tahliye", "prv"))
                return "RELIEF";
            if (ContainsAny(text, "needle", "iğne", "igne", "angle", "köşe", "kose"))
                return "NEEDLE_ANGLE";
            if (ContainsAny(text, "diaphragm", "diyaframlı", "diyafram"))
                return "DIAPHRAGM";
            if (ContainsAny(text, "solenoid", "solenoid"))
                return "SOLENOID";
            if (ContainsAny(text, "strainer", "süzgeç", "suzgec", "filtre"))
                return "STRAINER";
            return "UNKNOWN";
        }

        private static bool ContainsAny(string text, params string[] keywords)
            => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

        // ─────────────────────────────────────────────────────────────────────
        // I05  panel_phase_balance_check
        // input : List<Element>  (ElectricalEquipment — panolar)
        // params: max_imbalance_pct  Double  default=10.0
        //         load_param         String  default="" (boşsa API'den okur)
        //
        // Pre-check: panodaki devre var mı ve yük atanmış mı?
        // output: List<Dict>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("panel_phase_balance_check",
            Description = "Panel faz dengesizliğini kontrol eder (standart: ≤%10). " +
                          "Pre-check: panoda devre ve yük ataması var mı?",
            Category    = "MEP Denetim")]
        public static List<Dictionary<string, object?>> PanelPhaseBalanceCheck(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var panels    = ctx.InputAs<List<Element>>();
            double maxImb = ctx.GetDouble("max_imbalance_pct", 10.0);
            var rows      = new List<Dictionary<string, object?>>();

            foreach (var el in panels)
            {
                // Panoya bağlı devreleri bul
                var circuits = new FilteredElementCollector(rctx.Doc)
                    .OfClass(typeof(ElectricalSystem))
                    .Cast<ElectricalSystem>()
                    .Where(es => es.BaseEquipment?.Id == el.Id)
                    .ToList();

                // ── Pre-check: devre var mı? ──────────────────────────────────
                if (circuits.Count == 0)
                {
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["panel_id"]    = Rv.IdStr(el.Id),
                        ["panel_name"]  = el.Name,
                        ["status"]      = "PARAM_MISSING",
                        ["message"]     = $"'{el.Name}' panelinde devre bulunamadı. " +
                                          "Devreleri panele bağladıktan sonra tekrar çalıştırın.",
                    });
                    continue;
                }

                // Yük ataması var mı? (en az bir devreden kontrol)
                bool hasLoad = circuits.Any(c => c.ApparentLoad > 0);
                if (!hasLoad)
                {
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["panel_id"]    = Rv.IdStr(el.Id),
                        ["panel_name"]  = el.Name,
                        ["status"]      = "PARAM_MISSING",
                        ["message"]     = $"'{el.Name}' panelindeki devrelere yük (VA) atanmamış. " +
                                          "Fixture/ekipman yük değerlerini kontrol edin.",
                    });
                    continue;
                }

                // ── Faz bazlı yük toplama ─────────────────────────────────────
                double phaseA = 0, phaseB = 0, phaseC = 0;
                foreach (var c in circuits)
                {
                    double load = c.ApparentLoad;
                    // Faz bilgisi: ElectricalSystem.PolesNumber + circuit number convention
                    // Tek fazlı devre → circuit no'ya göre faz tahsis
                    int circNo = GetCircuitNumber(c);
                    switch ((circNo - 1) % 3)
                    {
                        case 0: phaseA += load; break;
                        case 1: phaseB += load; break;
                        case 2: phaseC += load; break;
                    }
                }

                double totalVa  = phaseA + phaseB + phaseC;
                double avgVa    = totalVa / 3.0;
                double maxPhase = Math.Max(phaseA, Math.Max(phaseB, phaseC));
                double minPhase = Math.Min(phaseA, Math.Min(phaseB, phaseC));
                double imbPct   = avgVa > 0
                    ? ((maxPhase - minPhase) / avgVa) * 100.0
                    : 0;

                string status = imbPct <= maxImb ? "OK" : "WARNING";

                rows.Add(new Dictionary<string, object?>
                {
                    ["panel_id"]         = Rv.IdStr(el.Id),
                    ["panel_name"]       = el.Name,
                    ["phase_a_va"]       = Math.Round(phaseA, 0),
                    ["phase_b_va"]       = Math.Round(phaseB, 0),
                    ["phase_c_va"]       = Math.Round(phaseC, 0),
                    ["total_va"]         = Math.Round(totalVa, 0),
                    ["max_imbalance_pct"]= Math.Round(imbPct,  1),
                    ["circuit_count"]    = circuits.Count,
                    ["status"]           = status,
                });
            }

            int ok = rows.Count(r => (string?)r["status"] == "OK");
            ctx.Log($"  panel_phase_balance_check: {rows.Count} panel — OK:{ok}");
            return rows;
        }

        private static int GetCircuitNumber(ElectricalSystem es)
        {
            // Circuit numarasını string'den parse et (örn: "3" veya "3-5")
            var name = es.CircuitNumber ?? "";
            var numStr = new string(name.TakeWhile(c => char.IsDigit(c)).ToArray());
            return int.TryParse(numStr, out int n) ? n : 1;
        }

        // ─────────────────────────────────────────────────────────────────────
        // I06  sprinkler_head_schedule
        // input : List<Element>  (OST_FireProtection — sprinkler)
        // params: k_factor_param   String  default="K_Factor"
        //         coverage_param   String  default="EG_KapsaAlan_m2"
        //         zone_param       String  default="EG_Zone"
        //
        // Pre-check: k_factor_param family'de var mı?
        // output: List<Dict> (tip/zone/kat bazlı gruplar)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("sprinkler_head_schedule",
            Description = "Sprinkler head'lerden tip/K-faktör/kapsama/kat/zone özeti üretir. " +
                          "Pre-check: K_Factor parametresi family'de yoksa eklenmesi istenir.",
            Category    = "MEP Denetim")]
        public static List<Dictionary<string, object?>> SprinklerHeadSchedule(OpContext ctx)
        {
            var sprinklers  = ctx.InputAs<List<Element>>();
            string kParam   = ctx.GetString("k_factor_param",  "K_Factor");
            string covParam = ctx.GetString("coverage_param",  "EG_KapsaAlan_m2");
            string zoneParam= ctx.GetString("zone_param",      "EG_Zone");

            // ── Pre-check ─────────────────────────────────────────────────────
            var sample = sprinklers.FirstOrDefault(e => e is FamilyInstance) as FamilyInstance;
            if (sample != null)
            {
                var missing = CheckRequiredParams(sample, new[] { kParam });
                if (missing.Count > 0)
                {
                    ctx.Log($"  sprinkler_head_schedule: PRE-CHECK — {kParam} eksik");
                    return new List<Dictionary<string, object?>>
                    {
                        ParamMissingResult(missing,
                            $"Family Editor'de sprinkler family'sine '{kParam}' parametresi ekleyin " +
                            "(Number tipi, örn: K=5.6, K=8.0). " +
                            $"Opsiyonel: '{covParam}' kapsama alanı m² (Number).")
                    };
                }
            }

            // ── Schedule üretimi ──────────────────────────────────────────────
            var groups = new Dictionary<string, ScheduleGroup>();

            foreach (var el in sprinklers)
            {
                if (el is not FamilyInstance fi) continue;

                string typeName = fi.Symbol?.Name ?? "Bilinmiyor";
                double kFactor  = ReadDouble(fi, kParam);
                double coverage = ReadDouble(fi, covParam);
                string zone     = ReadString(fi, zoneParam);
                string level    = (el.Document?.GetElement(
                    el.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)?.AsElementId()
                    ?? ElementId.InvalidElementId) as Level)?.Name ?? "";

                string groupKey = $"{typeName}|{zone}|{level}";
                if (!groups.ContainsKey(groupKey))
                    groups[groupKey] = new ScheduleGroup
                    {
                        TypeName = typeName, KFactor = kFactor,
                        Coverage = coverage, Zone = zone, Level = level,
                    };
                groups[groupKey].Count++;
            }

            var rows = groups.Values
                .OrderBy(g => g.Level).ThenBy(g => g.Zone).ThenBy(g => g.TypeName)
                .Select(g => new Dictionary<string, object?>
                {
                    ["type_name"]   = g.TypeName,
                    ["k_factor"]    = g.KFactor,
                    ["coverage_m2"] = g.Coverage > 0 ? g.Coverage : (object?)null,
                    ["zone"]        = g.Zone,
                    ["level"]       = g.Level,
                    ["quantity"]    = g.Count,
                })
                .ToList();

            ctx.Log($"  sprinkler_head_schedule: {sprinklers.Count} head → {rows.Count} grup");
            return rows;
        }

        private sealed class ScheduleGroup
        {
            public string TypeName = ""; public double KFactor;
            public double Coverage; public string Zone = "";
            public string Level = ""; public int Count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // I07  fa_device_schedule
        // input : List<Element>  (OST_FireAlarmDevices)
        // params: loop_param    String  default="EG_Loop"
        //         zone_param    String  default="EG_Zone"
        //         circuit_param String  default="EG_Circuit"
        //
        // Pre-check: loop_param + zone_param var mı?
        // output: List<Dict> (tip/kat/devre/loop grupları)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fa_device_schedule",
            Description = "Yangın alarm cihazlarını tip/kat/devre/loop bazında gruplar. " +
                          "Pre-check: EG_Loop + EG_Zone parametreleri family'de yoksa eklenmesi istenir.",
            Category    = "MEP Denetim")]
        public static List<Dictionary<string, object?>> FaDeviceSchedule(OpContext ctx)
        {
            var devices    = ctx.InputAs<List<Element>>();
            string loopPrm = ctx.GetString("loop_param",    "EG_Loop");
            string zonePrm = ctx.GetString("zone_param",    "EG_Zone");
            string circPrm = ctx.GetString("circuit_param", "EG_Circuit");

            // ── Pre-check ─────────────────────────────────────────────────────
            var sample = devices.FirstOrDefault(e => e is FamilyInstance) as FamilyInstance;
            if (sample != null)
            {
                var missing = CheckRequiredParams(sample, new[] { loopPrm, zonePrm });
                if (missing.Count > 0)
                {
                    ctx.Log($"  fa_device_schedule: PRE-CHECK — eksik: {string.Join(", ", missing)}");
                    return new List<Dictionary<string, object?>>
                    {
                        ParamMissingResult(missing,
                            "Yangın alarm family'lerine Shared Parameter olarak ekleyin. " +
                            $"{loopPrm} = Loop numarası (Text/Integer), " +
                            $"{zonePrm} = Bölge adı (Text), " +
                            $"{circPrm} = Devre (Text) — opsiyonel.")
                    };
                }
            }

            // ── Gruplama ──────────────────────────────────────────────────────
            var groups = new Dictionary<string, FaGroup>();

            foreach (var el in devices)
            {
                if (el is not FamilyInstance fi) continue;

                string devType = fi.Symbol?.FamilyName ?? "Bilinmiyor";
                string loop    = ReadString(fi, loopPrm);
                string zone    = ReadString(fi, zonePrm);
                string circuit = ReadString(fi, circPrm);
                string level   = (el.Document?.GetElement(
                    el.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)?.AsElementId()
                    ?? ElementId.InvalidElementId) as Level)?.Name ?? "";

                string key = $"{devType}|{level}|{zone}|{loop}|{circuit}";
                if (!groups.ContainsKey(key))
                    groups[key] = new FaGroup
                    {
                        DeviceType = devType, Level = level,
                        Zone = zone, Loop = loop, Circuit = circuit,
                    };
                groups[key].Count++;
            }

            var rows = groups.Values
                .OrderBy(g => g.Level).ThenBy(g => g.Zone)
                .ThenBy(g => g.Loop).ThenBy(g => g.DeviceType)
                .Select(g => new Dictionary<string, object?>
                {
                    ["device_type"] = g.DeviceType,
                    ["level"]       = g.Level,
                    ["zone"]        = g.Zone,
                    ["loop"]        = g.Loop,
                    ["circuit"]     = g.Circuit,
                    ["quantity"]    = g.Count,
                })
                .ToList();

            ctx.Log($"  fa_device_schedule: {devices.Count} cihaz → {rows.Count} grup");
            return rows;
        }

        private sealed class FaGroup
        {
            public string DeviceType = ""; public string Level = "";
            public string Zone = ""; public string Loop = "";
            public string Circuit = ""; public int Count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // I08  lighting_emergency_check
        // input : List<Element>  (LightingFixtures)
        // params: emergency_param           String  default="EG_AcilAydinlatma"
        //         emergency_panel_pattern   String  default="EP"
        //                                   (panel adında bu metin varsa acil panel)
        //
        // Pre-check: emergency_param var mı?
        // output: ValidationReport
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("lighting_emergency_check",
            Description = "Acil aydınlatma fixture'larının acil devreye bağlı olduğunu doğrular. " +
                          "Pre-check: EG_AcilAydinlatma parametresi fixture family'de yoksa eklenmesi istenir.",
            Category    = "MEP Denetim")]
        public static ValidationReport LightingEmergencyCheck(OpContext ctx)
        {
            var rctx        = RequireRevit(ctx);
            var fixtures    = ctx.InputAs<List<Element>>();
            string emPrm    = ctx.GetString("emergency_param",         "EG_AcilAydinlatma");
            string panPat   = ctx.GetString("emergency_panel_pattern", "EP");
            var results     = new List<ValidationResult>();

            // ── Pre-check ─────────────────────────────────────────────────────
            var sample = fixtures.FirstOrDefault(e => e is FamilyInstance) as FamilyInstance;
            if (sample != null)
            {
                var missing = CheckRequiredParams(sample, new[] { emPrm });
                if (missing.Count > 0)
                {
                    ctx.Log($"  lighting_emergency_check: PRE-CHECK — {emPrm} eksik");
                    return MakeReport("Acil Aydınlatma Kontrolü",
                        new List<ValidationResult>
                        {
                            new()
                            {
                                RuleId    = "PRE-CHECK",
                                ElementId = Rv.IdStr(sample.Id),
                                Category  = "Aydınlatma",
                                CheckType = "Parametre Kontrolü",
                                Passed    = false,
                                Severity  = "ERROR",
                                Message   = $"'{emPrm}' parametresi aydınlatma family'sinde bulunamadı. " +
                                            "Yes/No tipi Shared Parameter olarak ekleyin. " +
                                            "Acil fixture'larda = Yes olarak işaretleyin.",
                            }
                        });
                }
            }

            // ── Kontrol ───────────────────────────────────────────────────────
            foreach (var el in fixtures)
            {
                if (el is not FamilyInstance fi) continue;

                // Acil fixture mi?
                var ep = fi.LookupParameter(emPrm) ?? fi.Symbol.LookupParameter(emPrm);
                bool isEmergency = ep?.AsInteger() == 1;

                if (!isEmergency) continue; // Acil değilse kontrol etme

                // Bağlı elektrik devresini bul
                var elecSys = fi.MEPModel?.GetElectricalSystems()
                    ?.OfType<ElectricalSystem>()
                    .FirstOrDefault();

                bool onEmergencyPanel = false;
                string panelName = "";

                if (elecSys?.BaseEquipment != null)
                {
                    panelName = elecSys.BaseEquipment.Name ?? "";
                    onEmergencyPanel = panelName.IndexOf(panPat,
                        StringComparison.OrdinalIgnoreCase) >= 0;
                }

                bool ok = onEmergencyPanel;
                results.Add(new ValidationResult
                {
                    RuleId    = "I08-ACIL",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "Aydınlatma",
                    CheckType = $"Acil Fixture → Acil Panel ({panPat}*)",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "ERROR",
                    Message   = ok
                        ? $"Acil fixture {el.Id}: Panel='{panelName}' ✓"
                        : string.IsNullOrEmpty(panelName)
                            ? $"Acil fixture {el.Id}: Hiçbir devreye bağlı değil"
                            : $"Acil fixture {el.Id}: Panel='{panelName}' — acil panel değil ('{panPat}*' bekleniyor)",
                });
            }

            int emergencyCount = results.Count;
            int fail           = results.Count(r => !r.Passed);
            ctx.Log($"  lighting_emergency_check: {emergencyCount} acil fixture, {fail} hata");
            return MakeReport("Acil Aydınlatma Devre Kontrolü", results);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ortak yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        private static List<string> CheckRequiredParams(
            Element el, IEnumerable<string> paramNames)
        {
            var missing = new List<string>();
            foreach (var name in paramNames)
            {
                var p = el.LookupParameter(name);
                if (p == null && el is FamilyInstance fi)
                    p = fi.Symbol?.LookupParameter(name);
                if (p == null) missing.Add(name);
            }
            return missing;
        }

        private static Dictionary<string, object?> ParamMissingResult(
            IEnumerable<string> missing, string hint = "")
        {
            var list = missing.ToList();
            return new Dictionary<string, object?>
            {
                ["status"]         = "PARAM_MISSING",
                ["missing_params"] = string.Join(";", list),
                ["message"]        = $"Şu parametreler family/elemana eklenmeli: " +
                                     $"{string.Join(", ", list)}." +
                                     (hint.Length > 0 ? $" {hint}" : ""),
            };
        }

        private static double ReadDouble(FamilyInstance fi, string paramName)
        {
            var p = fi.LookupParameter(paramName) ?? fi.Symbol?.LookupParameter(paramName);
            if (p == null) return 0;
            return p.StorageType == StorageType.Double ? p.AsDouble()
                 : p.StorageType == StorageType.Integer ? p.AsInteger()
                 : 0;
        }

        private static string ReadString(FamilyInstance fi, string paramName)
        {
            var p = fi.LookupParameter(paramName) ?? fi.Symbol?.LookupParameter(paramName);
            return p?.AsString() ?? p?.AsValueString() ?? "";
        }

        private static ValidationReport MakeReport(string title, List<ValidationResult> results)
            => new()
            {
                ManifestTitle = title,
                Results       = results,
                Passed        = results.Count(r => r.Passed),
                Failed        = results.Count(r => !r.Passed),
            };

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
    }
}
