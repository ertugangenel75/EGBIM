using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Model Kalite (QA/QC) Op'ları
    /// Grup 1: Boş parametre tarama, duplicate tespiti, seviye/faz/workset kontrolleri
    /// </summary>
    public static class QaOps
    {
        // ── M01: Boş Parametre Tarama ─────────────────────────────────────────
        [EgOp("qa_find_empty_params",
            Description = "Belirtilen parametrelerin boş olduğu elemanları tespit eder",
            Category    = "QA/QC")]
        public static List<Dictionary<string, object?>> FindEmptyParams(OpContext ctx)
        {
            var elements   = ctx.InputAsOrDefault<List<Element>>();
            var paramNames = ctx.GetList<string>("param_names");
            var results    = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                var emptyParams = new List<string>();
                foreach (var pName in paramNames)
                {
                    var p = el.LookupParameter(pName);
                    bool empty = p == null || p.AsValueString() == null || string.IsNullOrWhiteSpace(p.AsString() ?? p.AsValueString() ?? "");
                    if (empty) emptyParams.Add(pName);
                }
                if (emptyParams.Any())
                    results.Add(new Dictionary<string, object?>
                    {
                        ["element_id"]    = Rv.IdStr(el.Id),
                        ["kategori"]      = el.Category?.Name ?? "",
                        ["tip"]           = el.Name,
                        ["bos_parametreler"] = string.Join(", ", emptyParams),
                        ["adet"]          = emptyParams.Count
                    });
            }
            ctx.Log($"  qa_find_empty_params: {results.Count} eleman boş parametre içeriyor");
            return results;
        }

        // ── M02: Duplicate Element Tespiti ────────────────────────────────────
        [EgOp("qa_detect_duplicates",
            Description = "Aynı konumda birden fazla eleman tespiti (BBox karşılaştırma)",
            Category    = "QA/QC")]
        public static List<Dictionary<string, object?>> DetectDuplicates(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var tolerance = ctx.GetDouble("tolerance_mm", 10.0) / 304.8; // mm → feet
            var results   = new List<Dictionary<string, object?>>();
            var seen      = new List<(ElementId id, XYZ center)>();

            foreach (var el in elements)
            {
                var bbox = el.get_BoundingBox(null);
                if (bbox == null) continue;
                var center = (bbox.Min + bbox.Max) * 0.5;

                var dup = seen.FirstOrDefault(s => s.center.DistanceTo(center) < tolerance);
                if (dup.id != null)
                    results.Add(new Dictionary<string, object?>
                    {
                        ["element_id"]  = Rv.IdStr(el.Id),
                        ["dup_id"]      = dup.id.Value.ToString(),
                        ["kategori"]    = el.Category?.Name ?? "",
                        ["mesafe_mm"]   = Math.Round(dup.center.DistanceTo(center) * 304.8, 1),
                        ["durum"]       = "DUPLICATE"
                    });
                else
                    seen.Add((el.Id, center));
            }
            ctx.Log($"  qa_detect_duplicates: {results.Count} duplicate tespit edildi");
            return results;
        }

        // ── M03: Seviye Ataması Kontrolü ──────────────────────────────────────
        [EgOp("qa_check_level_assigned",
            Description = "Elemanların bir seviyeye atanıp atanmadığını kontrol eder",
            Category    = "QA/QC")]
        public static ValidationReport CheckLevelAssigned(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var results  = new List<ValidationResult>();

            foreach (var el in elements)
            {
                var levelParam = el.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                              ?? el.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                              ?? el.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                bool hasLevel = levelParam?.AsElementId() is { } lid && lid != ElementId.InvalidElementId;
                results.Add(new ValidationResult
                {
                    RuleId    = "M03-LEVEL",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "Seviye Ataması",
                    Passed    = hasLevel,
                    Severity  = "WARNING",
                    Message   = hasLevel ? "Seviye atandı" : "Seviye atanmamış"
                });
            }
            ctx.Log($"  qa_check_level_assigned: {results.Count(r => !r.Passed)} seviyesiz eleman");
            return MakeReport("Seviye Ataması Kontrolü", results);
        }

        // ── M04: Faz Tutarlılığı ──────────────────────────────────────────────
        [EgOp("qa_validate_phase_consistency",
            Description = "Elemanların Creation/Demolition faz tutarlılığını kontrol eder",
            Category    = "QA/QC")]
        public static ValidationReport ValidatePhaseConsistency(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var results  = new List<ValidationResult>();

            foreach (var el in elements)
            {
                var creation   = el.get_Parameter(BuiltInParameter.PHASE_CREATED)?.AsValueString() ?? "";
                var demolition = el.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED)?.AsValueString() ?? "";
                bool ok = string.IsNullOrEmpty(demolition) || creation != demolition;
                results.Add(new ValidationResult
                {
                    RuleId    = "M04-PHASE",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "Faz Tutarlılığı",
                    Passed    = ok,
                    Severity  = "WARNING",
                    Message   = ok ? $"Faz OK ({creation})" : $"Oluşturma=Yıkım fazı: {creation}"
                });
            }
            ctx.Log($"  qa_validate_phase_consistency: {results.Count(r => !r.Passed)} tutarsız faz");
            return MakeReport("Faz Tutarlılığı Kontrolü", results);
        }

        // ── M05: Workset Kontrolü ─────────────────────────────────────────────
        [EgOp("qa_validate_workset",
            Description = "Elemanların beklenen workset'e atanıp atanmadığını kontrol eder",
            Category    = "QA/QC")]
        public static ValidationReport ValidateWorkset(OpContext ctx)
        {
            var elements        = ctx.InputAsOrDefault<List<Element>>();
            var expectedWorkset = ctx.GetString("expected_workset", "");
            var results         = new List<ValidationResult>();

            foreach (var el in elements)
            {
                var ws = el.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM)?.AsValueString() ?? "Bilinmiyor";
                bool ok = string.IsNullOrEmpty(expectedWorkset) || ws == expectedWorkset;
                results.Add(new ValidationResult
                {
                    RuleId    = "M05-WORKSET",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "Workset",
                    Passed    = ok,
                    Severity  = "WARNING",
                    Message   = ok ? $"Workset OK ({ws})" : $"Beklenen: {expectedWorkset}, Mevcut: {ws}"
                });
            }
            ctx.Log($"  qa_validate_workset: {results.Count(r => !r.Passed)} yanlış workset");
            return MakeReport("Workset Kontrolü", results);
        }

        // ── M06: Warning Listesi Export ───────────────────────────────────────
        [EgOp("qa_get_model_warnings",
            Description = "Model uyarılarını toplar ve kategorize eder",
            Category    = "QA/QC")]
        public static List<Dictionary<string, object?>> GetModelWarnings(OpContext ctx)
        {
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var warnings = doc.GetWarnings();
            var results  = new List<Dictionary<string, object?>>();

            foreach (var w in warnings)
            {
                results.Add(new Dictionary<string, object?>
                {
                    ["aciklama"]    = w.GetDescriptionText(),
                    ["eleman_ids"]  = string.Join(";", w.GetFailingElements().Select(id => id.Value.ToString())),
                    ["kural_id"]    = w.GetFailureDefinitionId().Guid.ToString(),
                    ["seviye"]      = "WARNING"
                });
            }
            ctx.Log($"  qa_get_model_warnings: {results.Count} uyarı bulundu");
            return results;
        }

        // ── M07: Yerleştirilmemiş Oda Tespiti (alias to existing) ─────────────
        [EgOp("qa_find_redundant_rooms",
            Description = "Alan değeri olmayan ve modele katkısı bulunmayan odaları bulur",
            Category    = "QA/QC")]
        public static List<Dictionary<string, object?>> FindRedundantRooms(OpContext ctx)
        {
            var rooms   = ctx.InputAsOrDefault<List<Element>>();
            var results = new List<Dictionary<string, object?>>();
            foreach (var el in rooms)
            {
                if (el is Room room)
                {
                    var area = room.Area;
                    if (area < 0.01)
                        results.Add(new Dictionary<string, object?>
                        {
                            ["element_id"] = Rv.IdStr(room.Id),
                            ["adi"]        = room.Name,
                            ["numara"]     = room.Number,
                            ["alan_m2"]    = Math.Round(area * 0.0929, 3),
                            ["durum"]      = area <= 0 ? "YERLEŞTİRİLMEMİŞ" : "KÜÇÜK_ODA"
                        });
                }
            }
            ctx.Log($"  qa_find_redundant_rooms: {results.Count} sorunlu oda");
            return results;
        }

        // ── M08: Onaylı Aile Listesi Kontrolü ────────────────────────────────
        [EgOp("qa_check_approved_families",
            Description = "Modeldeki aileleri onaylı liste ile karşılaştırır",
            Category    = "QA/QC")]
        public static List<Dictionary<string, object?>> CheckApprovedFamilies(OpContext ctx)
        {
            var families     = ctx.InputAsOrDefault<List<Element>>();
            var approvedList = ctx.GetList<string>("approved_family_keywords");
            var results      = new List<Dictionary<string, object?>>();

            foreach (var el in families)
            {
                if (el is not Family fam) continue;
                bool approved = approvedList.Count == 0
                    || approvedList.Any(k => fam.Name.ToLowerInvariant().Contains(k.ToLowerInvariant()));
                if (!approved)
                    results.Add(new Dictionary<string, object?>
                    {
                        ["aile_adi"]   = fam.Name,
                        ["kategori"]   = fam.FamilyCategory?.Name ?? "",
                        ["durum"]      = "ONAYSIZ",
                        ["mesaj"]      = "Onaylı aile listesinde bulunamadı"
                    });
            }
            ctx.Log($"  qa_check_approved_families: {results.Count} onaysız aile");
            return results;
        }

        // ── M09: Koordinat Kontrolü ───────────────────────────────────────────
        [EgOp("qa_validate_coordinates",
            Description = "Projenin paylaşımlı koordinat sistemine bağlı olup olmadığını kontrol eder",
            Category    = "QA/QC")]
        public static Dictionary<string, object?> ValidateCoordinates(OpContext ctx)
        {
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var pinfo      = doc.ProjectInformation;
            var location   = doc.ActiveProjectLocation;
            var sharedSite = doc.SiteLocation;
            return new Dictionary<string, object?>
            {
                ["proje_adi"]      = pinfo?.Name ?? "",
                ["konum_adi"]      = location?.Name ?? "",
                ["enlem"]          = sharedSite?.Latitude ?? 0,
                ["boylam"]         = sharedSite?.Longitude ?? 0,
                ["koordinat_ok"]   = location != null,
                ["mesaj"]          = location != null ? "Koordinat sistemi tanımlı" : "Paylaşımlı koordinat yok"
            };
        }

        // ── M10: Model İstatistik & Boyut Analizi ─────────────────────────────
        [EgOp("qa_model_size_analysis",
            Description = "Model eleman sayıları ve ağırlık analizi üretir",
            Category    = "QA/QC")]
        public static List<Dictionary<string, object?>> ModelSizeAnalysis(OpContext ctx)
        {
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var results = new List<Dictionary<string, object?>>();
            var cats    = new[]
            {
                BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_Rooms, BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_CableTray
            };
            foreach (var cat in cats)
            {
                var count = new FilteredElementCollector(doc)
                    .OfCategory(cat).WhereElementIsNotElementType().GetElementCount();
                results.Add(new Dictionary<string, object?>
                {
                    ["kategori"] = cat.ToString().Replace("OST_", ""),
                    ["adet"]     = count,
                    ["durum"]    = count > 5000 ? "BÜYÜK" : count > 1000 ? "ORTA" : "NORMAL"
                });
            }
            ctx.Log($"  qa_model_size_analysis: {results.Count} kategori analiz edildi");
            return results;
        }

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
