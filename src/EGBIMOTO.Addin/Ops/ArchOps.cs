using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Mimari Op'ları (Grup 2)
    /// Sheet üretimi, oda kontrolü, kapı/pencere numaralama, erişilebilirlik, yangın bölgesi
    /// </summary>
    public static class ArchOps
    {
        // ── A01: Excel'den Sheet Üretimi ──────────────────────────────────────
        [EgOp("arch_sheets_from_data",
            RequiresTransaction = true,
            Description = "Satır listesinden Revit sheet'leri oluşturur (no/isim/view template)",
            Category    = "Mimari")]
        public static List<Dictionary<string, object?>> SheetsFromData(OpContext ctx)
        {
            var rows        = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var titleBlockId = GetFirstTitleBlock(doc);
            var results     = new List<Dictionary<string, object?>>();

            using var tx = new Transaction(doc, "EGBIMOTO: Sheet Oluştur");
            tx.Start();
            foreach (var row in rows)
            {
                try
                {
                    var no   = row.GetValueOrDefault("sheet_no")?.ToString() ?? "";
                    var name = row.GetValueOrDefault("sheet_name")?.ToString() ?? "Yeni Sheet";
                    var vs   = ViewSheet.Create(doc, titleBlockId);
                    vs.SheetNumber = no;
                    vs.Name        = name;
                    results.Add(new Dictionary<string, object?>
                    { ["sheet_no"] = no, ["sheet_name"] = name, ["id"] = Rv.IdStr(vs.Id), ["durum"] = "OLUŞTURULDU" });
                }
                catch (Exception ex)
                {
                    results.Add(new Dictionary<string, object?>
                    { ["sheet_no"] = row.GetValueOrDefault("sheet_no")?.ToString(), ["durum"] = "HATA", ["mesaj"] = ex.Message });
                }
            }
            tx.Commit();
            ctx.Log($"  arch_sheets_from_data: {results.Count(r => r["durum"]?.ToString() == "OLUŞTURULDU")} sheet oluşturuldu");
            return results;
        }

        // ── A02: View Template Uygulama ───────────────────────────────────────
        [EgOp("arch_apply_view_template",
            RequiresTransaction = true,
            Description = "İsim deseni eşleşen view'lara template uygular",
            Category    = "Mimari")]
        public static List<Dictionary<string, object?>> ApplyViewTemplate(OpContext ctx)
        {
            var views        = ctx.InputAsOrDefault<List<Element>>();
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var templateName = ctx.GetString("template_name", "");
            var viewPattern  = ctx.GetString("view_name_contains", "");
            var results      = new List<Dictionary<string, object?>>();

            var template = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.IsTemplate && v.Name == templateName);

            using var tx = new Transaction(doc, "EGBIMOTO: View Template");
            tx.Start();
            foreach (var el in views)
            {
                if (el is not View v || v.IsTemplate) continue;
                if (!string.IsNullOrEmpty(viewPattern) && !v.Name.Contains(viewPattern)) continue;
                if (template != null) v.ViewTemplateId = template.Id;
                results.Add(new Dictionary<string, object?>
                { ["view_adi"] = v.Name, ["template"] = templateName, ["durum"] = template != null ? "UYGULANDÍ" : "TEMPLATE_YOK" });
            }
            tx.Commit();
            ctx.Log($"  arch_apply_view_template: {results.Count} view işlendi");
            return results;
        }

        // ── A03: Oda Alan Doğrulama (programa karşı) ─────────────────────────
        [EgOp("arch_validate_room_area",
            Description = "Oda alanlarını minimum alan gereksinimi ile karşılaştırır",
            Category    = "Mimari")]
        public static ValidationReport ValidateRoomArea(OpContext ctx)
        {
            var rooms   = ctx.InputAsOrDefault<List<Element>>();
            var minArea = ctx.GetDouble("min_area_m2", 0.0);
            var results = new List<ValidationResult>();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;
                double areaM2 = room.Area * 0.0929;
                bool ok = areaM2 >= minArea;
                results.Add(new ValidationResult
                {
                    RuleId    = "A03-ALAN",
                    ElementId = Rv.IdStr(room.Id),
                    Category  = "Oda",
                    CheckType = "Minimum Alan",
                    Passed    = ok,
                    Severity  = "WARNING",
                    Message   = ok ? $"{room.Name}: {areaM2:F1} m² ✓"
                                   : $"{room.Name}: {areaM2:F1} m² < {minArea} m²"
                });
            }
            ctx.Log($"  arch_validate_room_area: {results.Count(r => !r.Passed)} oda yetersiz alan");
            return MakeReport("Oda Alan Kontrolü", results);
        }

        // ── A04: Kapı/Pencere Numaralama ──────────────────────────────────────
        [EgOp("arch_renumber_doors",
            RequiresTransaction = true,
            Description = "Kapıları kat ve oda sırasına göre yeniden numaralandırır",
            Category    = "Mimari")]
        public static List<Dictionary<string, object?>> RenumberDoors(OpContext ctx)
        {
            var doors   = ctx.InputAsOrDefault<List<Element>>();
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var prefix  = ctx.GetString("prefix", "K");
            var results = new List<Dictionary<string, object?>>();

            // Kat → oda → konum sırasına göre sırala
            var sorted = doors
                .Where(d => d is FamilyInstance)
                .Cast<FamilyInstance>()
                .OrderBy(d => d.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)?.AsValueString() ?? "")
                .ThenBy(d => d.get_BoundingBox(null)?.Min.X ?? 0)
                .ToList();

            using var tx = new Transaction(doc, "EGBIMOTO: Kapı Numaralama");
            tx.Start();
            int i = 1;
            foreach (var door in sorted)
            {
                var markParam = door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                var oldMark   = markParam?.AsString() ?? "";
                var newMark   = $"{prefix}{i:D3}";
                markParam?.Set(newMark);
                results.Add(new Dictionary<string, object?>
                { ["element_id"] = Rv.IdStr(door.Id), ["eski_no"] = oldMark, ["yeni_no"] = newMark });
                i++;
            }
            tx.Commit();
            ctx.Log($"  arch_renumber_doors: {results.Count} kapı numaralandırıldı");
            return results;
        }

        // ── A05: Tavan Yüksekliği Kontrolü ───────────────────────────────────
        [EgOp("arch_validate_ceiling_height",
            Description = "Oda tavan yüksekliklerini minimum değerle karşılaştırır",
            Category    = "Mimari")]
        public static ValidationReport ValidateCeilingHeight(OpContext ctx)
        {
            var rooms     = ctx.InputAsOrDefault<List<Element>>();
            var minHeight = ctx.GetDouble("min_height_mm", 2400.0) / 304.8;
            var results   = new List<ValidationResult>();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;
                var hParam  = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT);
                double h    = hParam?.AsDouble() ?? 0;
                bool ok     = h >= minHeight;
                results.Add(new ValidationResult
                {
                    RuleId    = "A05-TAVAN",
                    ElementId = Rv.IdStr(room.Id),
                    Category  = "Oda",
                    CheckType = "Tavan Yüksekliği",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "ERROR",
                    Message   = $"{room.Name}: {h * 304.8:F0} mm" + (ok ? " ✓" : $" < {minHeight * 304.8:F0} mm")
                });
            }
            ctx.Log($"  arch_validate_ceiling_height: {results.Count(r => !r.Passed)} oda yetersiz tavan");
            return MakeReport("Tavan Yüksekliği Kontrolü", results);
        }

        // ── A06: Erişilebilirlik Kontrolü (Kapı Genişliği) ───────────────────
        [EgOp("arch_validate_accessibility",
            Description = "Kapı net açıklığını erişilebilirlik standardına göre kontrol eder (min 850mm)",
            Category    = "Mimari")]
        public static ValidationReport ValidateAccessibility(OpContext ctx)
        {
            var doors   = ctx.InputAsOrDefault<List<Element>>();
            var minW    = ctx.GetDouble("min_width_mm", 850.0) / 304.8;
            var results = new List<ValidationResult>();

            foreach (var el in doors)
            {
                if (el is not FamilyInstance fi) continue;
                var wParam = fi.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                          ?? fi.Symbol.LookupParameter("Genişlik")
                          ?? fi.Symbol.LookupParameter("Width");
                double w   = wParam?.AsDouble() ?? 0;
                bool ok    = w >= minW;
                results.Add(new ValidationResult
                {
                    RuleId    = "A06-ERIŞIM",
                    ElementId = Rv.IdStr(fi.Id),
                    Category  = "Kapı",
                    CheckType = "Erişilebilirlik",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "ERROR",
                    Message   = $"{fi.Symbol.Name}: {w * 304.8:F0} mm" + (ok ? " ✓" : $" < {minW * 304.8:F0} mm")
                });
            }
            ctx.Log($"  arch_validate_accessibility: {results.Count(r => !r.Passed)} erişilemez kapı");
            return MakeReport("Erişilebilirlik Kontrolü (Kapı)", results);
        }

        // ── A07: Malzeme Atama Kontrolü ───────────────────────────────────────
        [EgOp("arch_check_material_assigned",
            Description = "Duvar/döşeme/çatı katmanlarında malzeme atanıp atanmadığını kontrol eder",
            Category    = "Mimari")]
        public static ValidationReport CheckMaterialAssigned(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var results  = new List<ValidationResult>();

            foreach (var el in elements)
            {
                if (el is not HostObject host) continue;
                // Revit 2025+: HostObject.GetCompoundStructure() kaldırıldı — type üzerinden alınır
                var cs     = (host.GetTypeId() is ElementId tid && host.Document.GetElement(tid) is HostObjAttributes hoa)
                             ? hoa.GetCompoundStructure() : null;
                bool allOk = cs == null || cs.GetLayers().All(l => l.MaterialId != ElementId.InvalidElementId);
                results.Add(new ValidationResult
                {
                    RuleId    = "A07-MALZEME",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "Malzeme Ataması",
                    Passed    = allOk,
                    Severity  = "WARNING",
                    Message   = allOk ? "Malzeme atandı" : "Boş malzeme katmanı mevcut"
                });
            }
            ctx.Log($"  arch_check_material_assigned: {results.Count(r => !r.Passed)} malzeme eksik");
            return MakeReport("Malzeme Ataması Kontrolü", results);
        }

        // ── A08: Yangın Bölgesi Sınır Sürekliliği ────────────────────────────
        [EgOp("arch_check_fire_rating_continuity",
            Description = "Yangın bölgesi duvarlarında yangın direnci rating'inin sürekliliğini kontrol eder",
            Category    = "Mimari")]
        public static ValidationReport CheckFireRatingContinuity(OpContext ctx)
        {
            var walls   = ctx.InputAsOrDefault<List<Element>>();
            var minRating = ctx.GetString("min_fire_rating", "60");
            var results = new List<ValidationResult>();

            foreach (var el in walls)
            {
                var ratingParam = el.LookupParameter("Yangın Direnci")
                               ?? el.LookupParameter("Fire Rating")
                               ?? el.get_Parameter(BuiltInParameter.FIRE_RATING);
                var rating = ratingParam?.AsString() ?? "";
                bool ok    = !string.IsNullOrWhiteSpace(rating);
                results.Add(new ValidationResult
                {
                    RuleId    = "A08-YANGINDUVARI",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "Duvar",
                    CheckType = "Yangın Rating",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "ERROR",
                    Message   = ok ? $"Rating: {rating}" : "Yangın direnci parametresi boş"
                });
            }
            ctx.Log($"  arch_check_fire_rating_continuity: {results.Count(r => !r.Passed)} duvarda rating eksik");
            return MakeReport("Yangın Bölgesi Sürekliliği", results);
        }

        // ── A09: Oda İsimlendirme Standardı ──────────────────────────────────
        [EgOp("arch_validate_room_naming",
            Description = "Oda isimlerinin belirlenen kurala uygunluğunu kontrol eder",
            Category    = "Mimari")]
        public static ValidationReport ValidateRoomNaming(OpContext ctx)
        {
            var rooms        = ctx.InputAsOrDefault<List<Element>>();
            var allowedNames = ctx.GetList<string>("allowed_keywords");
            var results      = new List<ValidationResult>();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;
                bool ok = allowedNames.Count == 0
                    || allowedNames.Any(k => room.Name.ToUpperInvariant().Contains(k.ToUpperInvariant()));
                results.Add(new ValidationResult
                {
                    RuleId    = "A09-ISIM",
                    ElementId = Rv.IdStr(room.Id),
                    Category  = "Oda",
                    CheckType = "İsimlendirme",
                    Passed    = ok,
                    Severity  = "WARNING",
                    Message   = ok ? $"{room.Name} ✓" : $"{room.Name} — standart dışı isim"
                });
            }
            ctx.Log($"  arch_validate_room_naming: {results.Count(r => !r.Passed)} standart dışı oda ismi");
            return MakeReport("Oda İsimlendirme Kontrolü", results);
        }

        // ── A10: Penceresiz Oda Kontrolü ─────────────────────────────────────
        [EgOp("arch_check_windowless_rooms",
            Description = "Pencere/doğal aydınlatma gerektiren odalarda pencere varlığını kontrol eder",
            Category    = "Mimari")]
        public static List<Dictionary<string, object?>> CheckWindowlessRooms(OpContext ctx)
        {
            var rooms       = ctx.InputAsOrDefault<List<Element>>();
            var windows     = ctx.GetParam<List<Element>>("windows") ?? new();
            var needWindows = ctx.GetList<string>("requires_window_keywords");
            var results     = new List<Dictionary<string, object?>>();

            var windowRoomIds = new HashSet<ElementId>(
                windows.OfType<FamilyInstance>()
                       .Select(w => w.Room?.Id)
                       .Where(id => id != null)
                       .Select(id => id!));

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;
                bool needsWin = needWindows.Count == 0
                    || needWindows.Any(k => room.Name.ToUpperInvariant().Contains(k.ToUpperInvariant()));
                if (!needsWin) continue;
                bool hasWin = windowRoomIds.Contains(room.Id);
                if (!hasWin)
                    results.Add(new Dictionary<string, object?>
                    {
                        ["element_id"] = Rv.IdStr(room.Id),
                        ["oda_adi"]    = room.Name,
                        ["numara"]     = room.Number,
                        ["durum"]      = "PENCERE_YOK"
                    });
            }
            ctx.Log($"  arch_check_windowless_rooms: {results.Count} odada pencere eksik");
            return results;
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────
        private static ElementId GetFirstTitleBlock(Document doc)
        {
            var tb = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .FirstElementId();
            return tb ?? ElementId.InvalidElementId;
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
