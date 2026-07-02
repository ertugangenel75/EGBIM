using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Addin.UI.Results;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;
using EGBIMOTO.Core.Data;
using EGBIMOTO.Core.Results;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Model doğrulama op'ları: IDS, QA kuralları, parametre kontrol, geometri kontrol.
    /// </summary>
    public static class ValidationOps
    {
        // ── IDS Doğrulama ─────────────────────────────────────────────────────
        [EgOp("validate_ids",
            Description = "Eleman listesini params.ids_path IDS dosyasına göre doğrular",
            Category    = "Doğrulama")]
        public static ValidationReport ValidateIds(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var idsPathRaw = ctx.GetString("ids_path", ctx.GetString("path", ""));
            var idsPath = System.IO.Path.IsPathRooted(idsPathRaw)
                ? idsPathRaw
                : System.IO.Path.Combine(EgbimotoData.DataRoot, idsPathRaw);
            if (!System.IO.File.Exists(idsPath))
                throw new System.IO.FileNotFoundException(
                    $"[validate_ids] IDS dosyası bulunamadı: {idsPath}\n" +
                    $"  → data/ klasörüne '{idsPathRaw}' dosyasını ekleyin.");
            var rules   = IdsParser.ParseFile(idsPath);
            var results = new List<ValidationResult>();
            foreach (var el in elements)
            {
                foreach (var rule in rules)
                {
                    foreach (var req in rule.Requirements)
                    {
                        bool passed = req.Type switch
                        {
                            "property"  => el.LookupParameter(req.Name) is not null,
                            "attribute" => !string.IsNullOrEmpty(el.Name),
                            _           => true
                        };
                        results.Add(new ValidationResult
                        {
                            RuleId    = rule.Id,
                            ElementId = Rv.IdStr(el.Id),
                            Category  = el.Category?.Name ?? "",
                            CheckType = $"IDS:{req.Type}:{req.Name}",
                            Passed    = passed,
                            Severity  = req.Severity,
                            Message   = passed ? $"{req.Name} OK" : $"{req.Name} eksik"
                        });
                    }
                }
            }
            var report = MakeReport($"IDS: {System.IO.Path.GetFileName(idsPath)}", results);
            ctx.Log($"  validate_ids: {report.Summary}");
            return report;
        }

        // ── QA Kural Doğrulama ────────────────────────────────────────────────
        [EgOp("validate_qa",
            Description = "Eleman listesini params.rules_path QA kural dosyasına göre doğrular",
            Category    = "Doğrulama")]
        public static ValidationReport ValidateQa(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            if ((elements == null || elements.Count == 0) &&
                ctx.Params.TryGetValue("input", out var vi) && vi is List<Element> eli)
                elements = eli;
            if ((elements == null || elements.Count == 0) &&
                ctx.Params.TryGetValue("elements", out var ve) && ve is List<Element> ele)
                elements = ele;
            var rulesPathRaw = ctx.GetString("rules_path",
                ctx.GetString("path", ""));
            // Göreli yol → data/ klasörüne göre çöz
            var rulesPath = System.IO.Path.IsPathRooted(rulesPathRaw)
                ? rulesPathRaw
                : System.IO.Path.Combine(EgbimotoData.DataRoot, rulesPathRaw);
            if (!System.IO.File.Exists(rulesPath))
                throw new System.IO.FileNotFoundException(
                    $"[validate_qa] QA kural dosyası bulunamadı: {rulesPath}\n" +
                    $"  → data/ klasörüne '{rulesPathRaw}' dosyasını ekleyin.");
            var ruleData = EGBIMOTO.Core.Data.DataRegistry.NormalizeRows(
                EGBIMOTO.Core.Data.DataRegistry.LoadJson(rulesPath), "rules");
            var rows = (elements ?? Enumerable.Empty<Element>()).Select(el => new Dictionary<string, object?>
            {
                ["element_id"] = Rv.IdStr(el.Id),
                ["kategori"]   = el.Category?.Name ?? "",
            }).ToList();
            var report = QaRuleEngine.RunQaRules(rows, ruleData,
                $"QA: {System.IO.Path.GetFileName(rulesPath)}");
            ctx.Log($"  validate_qa: {report.Summary}");
            return report;
        }

        // ── Parametre Kontrol ─────────────────────────────────────────────────
        [EgOp("param_exists_check",
            Description = "Elemanlarda params.param_name parametresinin varlığını kontrol eder",
            Category    = "Doğrulama")]
        public static ValidationReport ParamExistsCheck(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            if (elements == null || elements.Count == 0)
            {
                ctx.Log("  ⚠ param_exists_check: eleman listesi boş — atlandı");
                return new ValidationReport { ManifestTitle = "Parametre Varlık" };
            }
            var paramName = ctx.RequireString("param_name");
            var results   = elements.Select(el =>
            {
                bool exists = el.LookupParameter(paramName) is not null;
                return new ValidationResult
                {
                    RuleId    = "PARAM_EXISTS",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "ParameterExists",
                    Passed    = exists,
                    Message   = exists ? $"'{paramName}' mevcut" : $"'{paramName}' eksik",
                    Severity  = exists ? "INFO" : "ERROR"
                };
            }).ToList();
            var report = MakeReport($"Parametre Kontrol: {paramName}", results);
            ctx.Log($"  param_exists_check '{paramName}': {report.Summary}");
            return report;
        }

        [EgOp("param_filled_check",
            Description = "Elemanlarda params.param_name parametresinin dolu olduğunu kontrol eder",
            Category    = "Doğrulama")]
        public static ValidationReport ParamFilledCheck(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            if (elements == null || elements.Count == 0)
            { ctx.Log("  ⚠ param_filled_check: eleman listesi boş — atlandı"); return new ValidationReport { ManifestTitle = "Param Doluluk" }; }
            var paramName = ctx.RequireString("param_name");
            var severity  = ctx.GetString("severity", "ERROR");
            var results   = elements.Select(el =>
            {
                var p    = el.LookupParameter(paramName);
                bool ok  = p is not null && (p.StorageType != StorageType.String ||
                           !string.IsNullOrWhiteSpace(p.AsString()));
                return new ValidationResult
                {
                    RuleId    = $"PARAM_FILLED_{paramName}",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "ParameterFilled",
                    Passed    = ok,
                    Message   = ok ? $"'{paramName}' dolu" : $"'{paramName}' boş",
                    Severity  = ok ? "INFO" : severity
                };
            }).ToList();
            var report = MakeReport($"Parametre Doluluk: {paramName}", results);
            ctx.Log($"  param_filled_check '{paramName}': {report.Summary}");
            return report;
        }

        [EgOp("param_value_check",
            Description = "Elemanlarda params.param_name == params.expected_value kontrolü yapar",
            Category    = "Doğrulama")]
        public static ValidationReport ParamValueCheck(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            var expected  = ctx.GetString("expected_value");
            var severity  = ctx.GetString("severity", "ERROR");
            var results   = elements.Select(el =>
            {
                var p   = el.LookupParameter(paramName);
                var val = p?.StorageType switch
                {
                    StorageType.String  => p.AsString() ?? "",
                    StorageType.Integer => p.AsInteger().ToString(),
                    StorageType.Double  => p.AsDouble().ToString("F4"),
                    _                   => ""
                };
                bool ok = val.Equals(expected, StringComparison.OrdinalIgnoreCase);
                return new ValidationResult
                {
                    RuleId    = $"PARAM_VALUE_{paramName}",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "ParameterValue",
                    Passed    = ok,
                    Message   = ok ? $"'{paramName}'='{val}' OK" : $"'{paramName}'='{val}' (beklenen: '{expected}')",
                    Severity  = ok ? "INFO" : severity
                };
            }).ToList();
            var report = MakeReport($"Parametre Değer: {paramName}={expected}", results);
            ctx.Log($"  param_value_check: {report.Summary}");
            return report;
        }

        [EgOp("param_range_check",
            Description = "Elemanlarda params.param_name değerinin params.min ile params.max arasında olduğunu kontrol eder",
            Category    = "Doğrulama")]
        public static ValidationReport ParamRangeCheck(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            var min       = ctx.GetDouble("min", double.MinValue);
            var max       = ctx.GetDouble("max", double.MaxValue);
            var severity  = ctx.GetString("severity", "WARNING");
            var results   = elements.Select(el =>
            {
                var p   = el.LookupParameter(paramName);
                double val = p?.StorageType == StorageType.Double ? p.AsDouble()
                           : p?.StorageType == StorageType.Integer ? p.AsInteger() : double.NaN;
                bool ok = !double.IsNaN(val) && val >= min && val <= max;
                return new ValidationResult
                {
                    RuleId    = $"PARAM_RANGE_{paramName}",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "ParameterRange",
                    Passed    = ok,
                    Message   = ok ? $"'{paramName}'={val:F3} aralıkta"
                                   : $"'{paramName}'={val:F3} aralık dışı [{min}..{max}]",
                    Severity  = ok ? "INFO" : severity
                };
            }).ToList();
            var report = MakeReport($"Parametre Aralık: {paramName} [{min}..{max}]", results);
            ctx.Log($"  param_range_check: {report.Summary}");
            return report;
        }

        // ── Geometri Kontrol ──────────────────────────────────────────────────
        [EgOp("check_zero_volume",
            Description = "Sıfır hacimli elemanları tespit eder (HOST_VOLUME_COMPUTED == 0)",
            Category    = "Doğrulama")]
        public static ValidationReport CheckZeroVolume(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            if (elements == null || elements.Count == 0)
            { ctx.Log("  ⚠ check_zero_volume: eleman listesi boş — atlandı"); return new ValidationReport { ManifestTitle = "Sıfır Hacim" }; }
            var results  = elements.Select(el =>
            {
                var p   = el.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                double v = p?.AsDouble() ?? 0;
                bool ok  = v > 0.0001;
                return new ValidationResult
                {
                    RuleId    = "ZERO_VOLUME",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "ZeroVolume",
                    Passed    = ok,
                    Message   = ok ? $"Hacim: {v * 0.0283168:F3} m3" : "Sıfır hacim!",
                    Severity  = ok ? "INFO" : "WARNING"
                };
            }).ToList();
            var report = MakeReport("Sıfır Hacim Kontrolü", results);
            ctx.Log($"  check_zero_volume: {report.Summary}");
            return report;
        }

        [EgOp("check_overlapping_rooms",
            Description = "Çakışan odaları tespit eder (aynı katta merkezi 0.5m'den yakın odalar). FIX#11: Kat bazında gruplandırma ile O(n²)→O(k×m²) iyileştirildi.",
            Category    = "Doğrulama")]
        public static ValidationReport CheckOverlappingRooms(OpContext ctx)
        {
            var rooms   = ctx.InputAsOrDefault<List<Element>>();
            var results = new List<ValidationResult>();
            var roomList = rooms.OfType<Autodesk.Revit.DB.Architecture.Room>().ToList();

            // FIX #11: Önce kat bazında grupla → O(rooms) → her grupta O(m²)
            // Büyük modellerde (1000+ oda) katlar arası karşılaştırma tamamen atlıyor.
            var byLevel = roomList.GroupBy(r => r.LevelId?.Value ?? -1);

            foreach (var levelGroup in byLevel)
            {
                var grpRooms = levelGroup.ToList();
                for (int i = 0; i < grpRooms.Count; i++)
                {
                    for (int j = i + 1; j < grpRooms.Count; j++)
                    {
                        var r1   = grpRooms[i];
                        var r2   = grpRooms[j];
                        var loc1 = r1.Location as LocationPoint;
                        var loc2 = r2.Location as LocationPoint;
                        if (loc1 is null || loc2 is null) continue;
                        // DistanceTo Revit iç birimi (feet) → *0.3048 = m
                        double dist = loc1.Point.DistanceTo(loc2.Point) * 0.3048;
                        if (dist < 0.5)
                        {
                            results.Add(new ValidationResult
                            {
                                RuleId    = "OVERLAPPING_ROOMS",
                                ElementId = $"{Rv.GetId(r1.Id)},{Rv.GetId(r2.Id)}",
                                Category  = "Rooms",
                                CheckType = "OverlappingRooms",
                                Passed    = false,
                                Message   = $"Oda #{Rv.GetId(r1.Id)} ('{r1.Name}') ve #{Rv.GetId(r2.Id)} ('{r2.Name}') çakışıyor (mesafe: {dist:F2}m)",
                                Severity  = "WARNING"
                            });
                        }
                    }
                }
            }

            if (!results.Any())
                results.Add(new ValidationResult { RuleId = "OVERLAPPING_ROOMS", Passed = true,
                    Message = "Çakışan oda yok", Severity = "INFO", CheckType = "OverlappingRooms" });
            var report = MakeReport("Çakışan Oda Kontrolü", results);
            ctx.Log($"  check_overlapping_rooms: {report.Summary}");
            return report;
        }

        [EgOp("check_unplaced_rooms",
            Description = "Yerleştirilmemiş (unplaced) odaları tespit eder",
            Category    = "Doğrulama")]
        public static ValidationReport CheckUnplacedRooms(OpContext ctx)
        {
            // FIX #9: Hardcode 0.0929 → Ft2ToM2 sabiti (1 ft² = 0.09290304 m²)
            const double Ft2ToM2 = 0.3048 * 0.3048;
            var rooms   = ctx.InputAsOrDefault<List<Element>>();
            var results = rooms.Select(el =>
            {
                var room  = el as Autodesk.Revit.DB.Architecture.Room;
                bool ok   = room?.Area > 0;
                return new ValidationResult
                {
                    RuleId    = "UNPLACED_ROOM",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "Rooms",
                    CheckType = "UnplacedRoom",
                    Passed    = ok,
                    Message   = ok ? $"Oda '{room?.Name}' yerleştirilmiş ({room!.Area * Ft2ToM2:F2} m2)"
                                   : $"Oda '{room?.Name}' yerleştirilmemiş!",
                    Severity  = ok ? "INFO" : "WARNING"
                };
            }).ToList();
            var report = MakeReport("Yerleştirilmemiş Oda Kontrolü", results);
            ctx.Log($"  check_unplaced_rooms: {report.Summary}");
            return report;
        }

        // ── Rapor ─────────────────────────────────────────────────────────────
        [EgOp("validation_summary",
            Description = "ValidationReport'u WPF sonuç penceresinde gösterir (hata/uyarı satırları, " +
                          "renk kodlaması, 'Modelde Göster', CSV dışa aktarım).",
            Category    = "Doğrulama")]
        public static string ValidationSummary(OpContext ctx)
        {
            object? rawSum = ctx.Input;
            if (rawSum == null && ctx.Params.TryGetValue("input", out var vsi)) rawSum = vsi;
            if (rawSum == null && ctx.Params.TryGetValue("report", out var vsr)) rawSum = vsr;
            if (rawSum is not ValidationReport report)
            {
                ctx.Log("  validation_summary: Input ValidationReport değil");
                return "Geçersiz input";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== {report.ManifestTitle} ===");
            sb.AppendLine(report.Summary);
            foreach (var r in report.Results.Where(r => !r.Passed).Take(50))
                sb.AppendLine($"  [{r.Severity}] {r.Category} #{r.ElementId}: {r.Message}");
            var text = sb.ToString();
            ctx.Log(text);

            // v13.5: eskiden TaskDialog metin dökümü — artık renk kodlu, tıklanabilir grid.
            var dto = ManifestResultAdapter.FromValidationReport(report);
            var uidoc = (ctx as RevitOpContext)?.UiDoc;
            ManifestResultRendererRegistry.Show(uidoc, dto);

            return text;
        }

        [EgOp("merge_validation_reports",
            Description = "ValidationReport'ları birleştirir. from_many, inputs.lists veya tekil giriş desteklenir.",
            Category    = "Doğrulama")]
        public static ValidationReport MergeValidationReports(OpContext ctx)
        {
            var title   = ctx.GetString("title", "Birleşik Doğrulama Raporu");
            var reports = new List<ValidationReport>();

            // inputs.lists: ["$a","$b","$c"] — manifest üzerinden çoklu giriş
            if (ctx.Params.TryGetValue("lists", out var listsRaw))
            {
                // listsRaw: JsonElement(Array) veya List<object?>
                IEnumerable<string?> keys = listsRaw switch
                {
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Array
                        => je.EnumerateArray().Select(e => e.GetString()),
                    System.Collections.Generic.List<object?> lo
                        => lo.Select(o => o?.ToString()),
                    _ => Enumerable.Empty<string?>()
                };
                foreach (var rawKey in keys)
                {
                    var key = rawKey?.TrimStart('$');
                    if (key is null) continue;
                    if (!ctx.Vars.TryGetValue(key, out var val))
                    {
                        ctx.Log($"  ⚠ merge_validation_reports: '{key}' vars'da bulunamadı");
                        continue;
                    }
                    if (val is ValidationReport vr2) { reports.Add(vr2); continue; }
                    // Type adı ile kontrol — farklı assembly version koruması
                    if (val?.GetType().Name == nameof(ValidationReport))
                    {
                        // Reflection ile Results al
                        var resultsP = val.GetType().GetProperty("Results");
                        if (resultsP?.GetValue(val) is System.Collections.IEnumerable re)
                        {
                            var stub = new ValidationReport { ManifestTitle = key };
                            foreach (var item in re)
                                if (item is ValidationResult vres) stub.Results.Add(vres);
                            reports.Add(stub);
                        }
                        continue;
                    }
                    if (val is null) ctx.Log($"  ⚠ merge_validation_reports: '{key}' null — atlandı");
                    else ctx.Log($"  ⚠ merge_validation_reports: '{key}' ValidationReport değil ({val.GetType().Name})");
                }
            }
            // from_many — ctx.Input = List<object?>
            else if (ctx.Input is List<object?> multi)
            {
                foreach (var item in multi)
                    if (item is ValidationReport vr) reports.Add(vr);
            }
            // Tekil ValidationReport
            else if (ctx.Input is ValidationReport single)
            {
                reports.Add(single);
            }

            var allResults = reports.SelectMany(r => r?.Results ?? Enumerable.Empty<ValidationResult>()).ToList();
            var merged = MakeReport(title, allResults);
            ctx.Log($"  merge_validation_reports: {reports.Count} rapor, {merged.Summary}");
            return merged;
        }

        [EgOp("validation_to_rows",
            Description = "ValidationReport'u dict satır listesine dönüştürür (export için)",
            Category    = "Doğrulama")]
        public static List<Dictionary<string, object?>> ValidationToRows(OpContext ctx)
        {
            object? rawIn = ctx.Input;
            if (rawIn == null && ctx.Params.TryGetValue("input", out var vi)) rawIn = vi;
            if (rawIn == null && ctx.Params.TryGetValue("report", out var vr2)) rawIn = vr2;
            if (rawIn is not ValidationReport report) return new();
            return report.Results.Select(r => new Dictionary<string, object?>
            {
                ["kural_id"]   = r.RuleId,
                ["element_id"] = r.ElementId,
                ["kategori"]   = r.Category,
                ["kontrol"]    = r.CheckType,
                ["gecti"]      = r.Passed ? "Evet" : "Hayır",
                ["seviye"]     = r.Severity,
                ["mesaj"]      = r.Message
            }).ToList();
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────
        private static ValidationReport MakeReport(string title, List<ValidationResult> results)
            => new()
            {
                ManifestTitle = title,
                TotalChecks   = results.Count,
                Passed        = results.Count(r => r.Passed),
                Failed        = results.Count(r => !r.Passed && r.Severity == "ERROR"),
                Warnings      = results.Count(r => !r.Passed && r.Severity == "WARNING"),
                Results       = results
            };
    }
}
