using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Parametre okuma/yazma, tip yönetimi, aile yükleme, sistem bilgisi op'ları.
    /// </summary>
    public static class ParamOps
    {
        // ── Parametre okuma ───────────────────────────────────────────────────
        [EgOp("read_param",
            Description = "Eleman listesinden params.param_name parametresini okur. {element_id, kategori, tip, kat, <param>}",
            Category    = "Parametre")]
        public static List<Dictionary<string, object?>> ReadParam(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            return elements.Select(e =>
            {
                var p = e.LookupParameter(paramName);
                object? val = p?.StorageType switch
                {
                    StorageType.String    => p.AsString(),
                    StorageType.Double    => Math.Round(p.AsDouble(), 4),
                    StorageType.Integer   => p.AsInteger(),
                    StorageType.ElementId => p.AsElementId()?.Value,
                    _                     => null
                };
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["kategori"]   = e.Category?.Name ?? "",
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    [paramName]    = val
                };
            }).ToList();
        }

        [EgOp("read_params",
            Description = "Eleman listesinden params.param_names (virgülle ayrılmış) parametrelerini okur",
            Category    = "Parametre")]
        public static List<Dictionary<string, object?>> ReadParams(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements   = ctx.InputAsOrDefault<List<Element>>();
            var paramNames = ctx.GetString("param_names").Split(',').Select(p => p.Trim()).ToList();
            return elements.Select(e =>
            {
                var row = new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["kategori"]   = e.Category?.Name ?? "",
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? ""
                };
                foreach (var pn in paramNames)
                {
                    var p = e.LookupParameter(pn);
                    row[pn] = p?.StorageType switch
                    {
                        StorageType.String    => p.AsString(),
                        StorageType.Double    => Math.Round(p.AsDouble(), 4),
                        StorageType.Integer   => p.AsInteger(),
                        StorageType.ElementId => p.AsElementId()?.Value,
                        _                     => null
                    };
                }
                return row;
            }).ToList();
        }

        [EgOp("read_builtin_param",
            Description = "Elemandan BuiltInParameter okur. params: builtin_param (BuiltInParameter enum adı)",
            Category    = "Parametre")]
        public static List<Dictionary<string, object?>> ReadBuiltinParam(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var bipName   = ctx.GetString("builtin_param");
            if (!Enum.TryParse<BuiltInParameter>(bipName, out var bip))
            {
                ctx.Log($"  ⚠ Bilinmeyen BuiltInParameter: '{bipName}'");
                return new();
            }
            return elements.Select(e =>
            {
                var p = e.get_Parameter(bip);
                object? val = p?.StorageType switch
                {
                    StorageType.String    => p.AsString(),
                    StorageType.Double    => Math.Round(p.AsDouble(), 4),
                    StorageType.Integer   => p.AsInteger(),
                    StorageType.ElementId => p.AsElementId()?.Value,
                    _                     => null
                };
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["kategori"]   = e.Category?.Name ?? "",
                    [bipName]      = val
                };
            }).ToList();
        }

        // ── Parametre yazma ───────────────────────────────────────────────────
        [EgOp("write_param",
            Description = "Tüm elemanlara params.param_name = params.value yazar (transaction açar)",
            Category    = "Parametre",
            RequiresTransaction = true)]
        public static int WriteParam(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            var value     = ctx.GetString("value");
            int count     = 0;
            using var scope = new Host.RevitWriteScope(rctx.Doc, $"{paramName}={value}", rctx.IsAtomicMode);
            foreach (var el in elements)
            {
                var p = el.LookupParameter(paramName);
                if (p is null || p.IsReadOnly) continue;
                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(value); count++; break;
                    case StorageType.Integer:
                        if (int.TryParse(value, out var i)) { p.Set(i); count++; } break;
                    case StorageType.Double:
                        if (double.TryParse(value, out var d)) { p.Set(d); count++; } break;
                }
            }
            scope.Commit();
            ctx.Log($"  write_param '{paramName}'='{value}': {count} eleman güncellendi");
            return count;
        }

        [EgOp("write_param_from_rows",
            Description = "Satır listesindeki element_id'lere göre params.param_name = params.value_field yazar",
            Category    = "Parametre",
            RequiresTransaction = true)]
        public static int WriteParamFromRows(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var rows       = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var paramName  = ctx.RequireString("param_name");
            var valueField = ctx.GetString("value_field");
            int count      = 0;
            using var scope = new Host.RevitWriteScope(rctx.Doc, $"write_param_from_rows {paramName}", rctx.IsAtomicMode);
            foreach (var row in rows)
            {
                if (!row.TryGetValue("element_id", out var eid)) continue;
                if (!long.TryParse(eid?.ToString(), out var id)) continue;
                var el = rctx.Doc.GetElement(Rv.MakeElementId(id));  // v6
                if (el is null) continue;
                var p = el.LookupParameter(paramName);
                if (p is null || p.IsReadOnly) continue;
                var value = row.TryGetValue(valueField, out var v) ? v?.ToString() ?? "" : "";
                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(value); count++; break;
                    case StorageType.Integer:
                        if (int.TryParse(value, out var i)) { p.Set(i); count++; } break;
                    case StorageType.Double:
                        if (double.TryParse(value, out var d)) { p.Set(d); count++; } break;
                }
            }
            scope.Commit();
            ctx.Log($"  write_param_from_rows '{paramName}': {count} eleman güncellendi");
            return count;
        }

        [EgOp("copy_param",
            Description = "Elemanlarda params.source_param değerini params.target_param'a kopyalar",
            Category    = "Parametre",
            RequiresTransaction = true)]
        public static int CopyParam(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements   = ctx.InputAsOrDefault<List<Element>>();
            var sourceParam = ctx.GetString("source_param");
            var targetParam = ctx.GetString("target_param");
            int count       = 0;
            using var scope = new Host.RevitWriteScope(rctx.Doc, $"copy_param {sourceParam}->{targetParam}", rctx.IsAtomicMode);
            foreach (var el in elements)
            {
                var src2 = el.LookupParameter(sourceParam);
                var tgt  = el.LookupParameter(targetParam);
                if (src2 is null || tgt is null || tgt.IsReadOnly) continue;
                if (src2.StorageType != tgt.StorageType) continue;
                switch (src2.StorageType)
                {
                    case StorageType.String:  tgt.Set(src2.AsString() ?? ""); count++; break;
                    case StorageType.Integer: tgt.Set(src2.AsInteger()); count++; break;
                    case StorageType.Double:  tgt.Set(src2.AsDouble()); count++; break;
                }
            }
            scope.Commit();
            ctx.Log($"  copy_param '{sourceParam}'->{targetParam}: {count} eleman");
            return count;
        }

        // ── Paylaşımlı parametre ──────────────────────────────────────────────
        [EgOp("add_shared_params",
            Description = "EGBIM paylaşımlı parametrelerini modele ekler. params: spf_path (opsiyonel), group_filter (opsiyonel)",
            Category    = "Parametre",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> AddSharedParams(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var spfPathRaw  = ctx.GetString("spf_path", ctx.GetString("path", ""));
            var spfPath = (!string.IsNullOrEmpty(spfPathRaw) && !Path.IsPathRooted(spfPathRaw))
                ? Path.Combine(EgbimotoData.DataRoot, spfPathRaw)
                : spfPathRaw;
            var groupFilter = ctx.GetString("group_filter", "");

            if (string.IsNullOrEmpty(spfPath))
            {
                var addinDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                // Önce data/mapping/ klasörüne bak (DataRoot altında), sonra addin root
                var mappingPath = Path.Combine(EgbimotoData.DataRoot, "mapping", "EGBIM_SharedParams.txt");
                spfPath = File.Exists(mappingPath)
                    ? mappingPath
                    : Path.Combine(addinDir, "data", "EGBIM_SharedParams.txt");
            }
            if (!File.Exists(spfPath))
                throw new FileNotFoundException($"SPF dosyası bulunamadı: {spfPath}");

            var app     = rctx.UiApp.Application;
            var prevSpf = app.SharedParametersFilename;
            app.SharedParametersFilename = spfPath;
            var spFile  = app.OpenSharedParameterFile()
                ?? throw new InvalidOperationException("Paylaşımlı parametre dosyası açılamadı.");

            int added = 0, skipped = 0;
            using var scope = new Host.RevitWriteScope(rctx.Doc, "Paylaşımlı Parametre Ekle", rctx.IsAtomicMode);
            foreach (DefinitionGroup group in spFile.Groups)
            {
                if (!string.IsNullOrEmpty(groupFilter) &&
                    !group.Name.Contains(groupFilter, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (Definition def in group.Definitions)
                {
                    try
                    {
                        var catSet = new CategorySet();
                        catSet.Insert(rctx.Doc.Settings.Categories.get_Item(BuiltInCategory.OST_Walls));
                        catSet.Insert(rctx.Doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralColumns));
                        catSet.Insert(rctx.Doc.Settings.Categories.get_Item(BuiltInCategory.OST_Floors));
                        var binding = rctx.UiApp.Application.Create.NewInstanceBinding(catSet);
                        var bindMap = rctx.Doc.ParameterBindings;
                        if (!bindMap.Contains(def)) {
bindMap.Insert(def, binding, GroupTypeId.IdentityData);
                            added++; }
                        else skipped++;
                    }
                    catch { skipped++; }
                }
            }
            scope.Commit();
            app.SharedParametersFilename = prevSpf;
            ctx.Log($"  add_shared_params: {added} eklendi, {skipped} atlandı");
            return new() { ["added"] = added, ["skipped"] = skipped, ["spf_path"] = spfPath };
        }

        [EgOp("list_shared_params",
            Description = "Modeldeki tüm paylaşımlı parametreleri listeler",
            Category    = "Parametre")]
        public static List<Dictionary<string, object?>> ListSharedParams(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var result = new List<Dictionary<string, object?>>();
            var it     = rctx.Doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                if (it.Key is not ExternalDefinition def) continue;
                result.Add(new()
                {
                    ["ad"]       = def.Name,
                    ["guid"]     = def.GUID.ToString(),
                    ["grup"]     = def.OwnerGroup?.Name ?? "",
                    ["tip"]      = GetParamTypeName(def)
                });
            }
            ctx.Log($"  list_shared_params: {result.Count} parametre");
            return result;
        }

        // ── Tip yönetimi ──────────────────────────────────────────────────────
        // Revit 2026 uyumlu parametre tipi okuma
        private static string GetParamTypeName(ExternalDefinition def)
        {
            // v6: Rv adapter — REVIT2024: ParameterType, 2025+: GetDataType()
            return Rv.GetParamDataType(def);
        }

        [EgOp("type_get",
            Description = "Eleman listesindeki benzersiz tipleri döner. {tip, kategori, tip_id, kullanim_sayisi, aile}",
            Category    = "Parametre")]
        public static List<Dictionary<string, object?>> TypeGet(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements
                .GroupBy(e => e.GetTypeId())
                .Select(g =>
                {
                    var type = rctx.Doc.GetElement(g.Key) as ElementType;
                    return new Dictionary<string, object?>
                    {
                        ["tip_id"]          = g.Key.Value,
                        ["tip"]             = type?.Name ?? "—",
                        ["kategori"]        = type?.Category?.Name ?? g.First().Category?.Name ?? "",
                        ["kullanim_sayisi"] = g.Count(),
                        ["aile"]            = (type as FamilySymbol)?.Family?.Name ?? ""
                    };
                })
                .OrderByDescending(r => (int)r["kullanim_sayisi"]!)
                .ToList();
        }

        // FIX #7: collect_types CollectionOps.cs'e taşındı (Category="Toplama").
        // ParamOps'ta bırakılmıyor — duplicate op exception fırlatır.

        [EgOp("type_read_param",
            Description = "Eleman listesinin TİP parametresini okur (instance değil tip parametresi)",
            Category    = "Parametre")]
        public static List<Dictionary<string, object?>> TypeReadParam(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            return elements.Select(e =>
            {
                var type = rctx.Doc.GetElement(e.GetTypeId()) as ElementType;
                var p    = type?.LookupParameter(paramName);
                object? val = p?.StorageType switch
                {
                    StorageType.String    => p.AsString(),
                    StorageType.Double    => Math.Round(p.AsDouble(), 4),
                    StorageType.Integer   => p.AsInteger(),
                    _                     => null
                };
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["tip"]        = type?.Name ?? "",
                    [paramName]    = val
                };
            }).ToList();
        }

        // ── Doğrulama ─────────────────────────────────────────────────────────
        [EgOp("param_validate_schema",
            Description = "Eleman listesini params.schema_path JSON şemasına göre doğrular",
            Category    = "Parametre")]
        public static ValidationReport ParamValidateSchema(OpContext ctx)
        {
            // ctx.Input (from/depends_on) veya inputs.input / inputs.elements key'inden al
            var elements = ctx.InputAsOrDefault<List<Element>>();
            if ((elements == null || elements.Count == 0) &&
                ctx.Params.TryGetValue("input", out var inp) && inp is List<Element> fromInp)
                elements = fromInp;
            if ((elements == null || elements.Count == 0) &&
                ctx.Params.TryGetValue("elements", out var elp) && elp is List<Element> fromElp)
                elements = fromElp;
            if (elements == null || elements.Count == 0)
            { ctx.Log("  ⚠ param_validate_schema: eleman listesi boş — atlandı"); return new ValidationReport { ManifestTitle = "Şema Doğrulama" }; }
            var schemaPathRaw = ctx.GetString("schema_path", ctx.GetString("path", ""));
            var schemaPath = Path.IsPathRooted(schemaPathRaw)
                ? schemaPathRaw
                : Path.Combine(EgbimotoData.DataRoot, schemaPathRaw);
            if (!File.Exists(schemaPath))
                throw new FileNotFoundException(
                    $"[param_validate_schema] Şema bulunamadı: {schemaPath}\n" +
                    $"  → data/ klasörüne '{schemaPathRaw}' dosyasını ekleyin.");
            var schema  = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(
                File.ReadAllText(schemaPath)) ?? new();
            var results = new List<ValidationResult>();
            foreach (var el in elements)
            {
                foreach (var rule in schema)
                {
                    var pName    = rule.TryGetValue("param", out var pv) ? pv?.ToString() ?? "" : "";
                    var required = rule.TryGetValue("required", out var rv) && rv?.ToString() == "true";
                    var p        = el.LookupParameter(pName);
                    bool exists  = p is not null;
                    bool filled  = exists && p!.StorageType == StorageType.String
                        ? !string.IsNullOrWhiteSpace(p.AsString()) : exists;
                    bool pass    = !required || filled;
                    results.Add(new ValidationResult
                    {
                        RuleId    = $"SCHEMA_{pName}",
                        ElementId = Rv.IdStr(el.Id),
                        Category  = el.Category?.Name ?? "",
                        CheckType = "ParameterSchema",
                        Passed    = pass,
                        Message   = pass ? $"'{pName}' OK" : $"'{pName}' eksik/boş",
                        Severity  = pass ? "INFO" : "ERROR"
                    });
                }
            }
            var report = new ValidationReport
            {
                ManifestTitle = $"Şema Doğrulama: {Path.GetFileName(schemaPath)}",
                TotalChecks   = results.Count,
                Passed        = results.Count(r => r.Passed),
                Failed        = results.Count(r => !r.Passed && r.Severity == "ERROR"),
                Warnings      = results.Count(r => r.Severity == "WARNING"),
                Results       = results
            };
            ctx.Log($"  param_validate_schema: {report.Summary}");
            return report;
        }

        [EgOp("validate_required_params",
            Description = "Elemanlarda params.required_params (virgülle) parametrelerinin varlığını kontrol eder",
            Category    = "Parametre")]
        public static ValidationReport ValidateRequiredParams(OpContext ctx)
        {
            var elements       = ctx.InputAsOrDefault<List<Element>>();
            if (elements == null || elements.Count == 0)
            { ctx.Log("  ⚠ validate_required_params: eleman listesi boş — atlandı"); return new ValidationReport { ManifestTitle = "Zorunlu Param" }; }
            var requiredParams = ctx.GetString("required_params").Split(',').Select(p => p.Trim()).ToList();
            var results        = new List<ValidationResult>();
            foreach (var el in elements)
            {
                foreach (var pn in requiredParams)
                {
                    var p    = el.LookupParameter(pn);
                    bool ok  = p is not null && (p.StorageType != StorageType.String ||
                               !string.IsNullOrWhiteSpace(p.AsString()));
                    results.Add(new ValidationResult
                    {
                        RuleId    = $"REQ_{pn}",
                        ElementId = Rv.IdStr(el.Id),
                        Category  = el.Category?.Name ?? "",
                        CheckType = "RequiredParam",
                        Passed    = ok,
                        Message   = ok ? $"'{pn}' dolu" : $"'{pn}' eksik",
                        Severity  = ok ? "INFO" : "ERROR"
                    });
                }
            }
            var report = new ValidationReport
            {
                ManifestTitle = "Zorunlu Parametre Kontrolü",
                TotalChecks   = results.Count,
                Passed        = results.Count(r => r.Passed),
                Failed        = results.Count(r => !r.Passed && r.Severity == "ERROR"),
                Warnings      = 0,
                Results       = results
            };
            ctx.Log($"  validate_required_params: {report.Summary}");
            return report;
        }

        // ── IDS üretimi ───────────────────────────────────────────────────────
        [EgOp("generate_ids",
            Description = "Eleman listesinden IDS (Information Delivery Specification) XML üretir. params: output_path, title",
            Category    = "Parametre")]
        public static string GenerateIds(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var title     = ctx.GetString("title", "EGBIMOTO IDS");
            var outPath   = ctx.GetString("output_path",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"EGBIMOTO_{DateTime.Now:yyyyMMdd}.ids"));

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<ids xmlns=\"http://standards.buildingsmart.org/IDS\" " +
                "xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" " +
                "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
            sb.AppendLine($"  <info><title>{title}</title><date>{DateTime.Now:yyyy-MM-dd}</date></info>");
            sb.AppendLine("  <specifications>");

            var groups = elements.GroupBy(e => e.Category?.Name ?? "Unknown");
            foreach (var g in groups)
            {
                sb.AppendLine($"    <specification name=\"{g.Key}\" ifcVersion=\"IFC2X3 IFC4\">");
                sb.AppendLine("      <applicability><entity><name><simpleValue>" +
                    g.Key.ToUpperInvariant().Replace(" ", "") + "</simpleValue></name></entity></applicability>");
                sb.AppendLine("      <requirements/>");
                sb.AppendLine("    </specification>");
            }

            sb.AppendLine("  </specifications>");
            sb.AppendLine("</ids>");
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            ctx.Log($"  generate_ids: {outPath}");
            return outPath;
        }

        // ── Aile yönetimi ─────────────────────────────────────────────────────
        [EgOp("family_ensure_loaded",
            Description = "params.family_path ailesinin yüklü olduğunu kontrol eder, yoksa yükler",
            Category    = "Parametre",
            RequiresTransaction = true)]
        public static bool FamilyEnsureLoaded(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var familyPath = ctx.RequireString("family_path");
            var familyName = Path.GetFileNameWithoutExtension(familyPath);
            var existing   = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                ctx.Log($"  family_ensure_loaded: '{familyName}' zaten yüklü");
                return true;
            }
            if (!File.Exists(familyPath))
            {
                ctx.Log($"  ⚠ Aile dosyası bulunamadı: {familyPath}");
                return false;
            }
            using var fscope = new Host.RevitWriteScope(rctx.Doc, $"Aile Yükle {familyName}", rctx.IsAtomicMode);
            bool loaded = rctx.Doc.LoadFamily(familyPath, out _);
            fscope.Commit();
            ctx.Log($"  family_ensure_loaded: '{familyName}' {(loaded ? "yüklendi" : "yüklenemedi")}");
            return loaded;
        }

        // ── Sistem bilgisi ────────────────────────────────────────────────────
        [EgOp("system_info",
            Description = "Model ve sistem bilgilerini döner (proje adı, yol, Revit versiyonu, eleman sayısı)",
            Category    = "Yardımcı")]
        public static Dictionary<string, object?> SystemInfo(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var info = rctx.Doc.ProjectInformation;
            var total = new FilteredElementCollector(rctx.Doc)
                .WhereElementIsNotElementType().GetElementCount();
            return new()
            {
                ["proje_adi"]      = info?.Name ?? rctx.Doc.Title,
                ["proje_no"]       = info?.Number ?? "",
                ["adres"]          = info?.Address ?? "",
                ["dosya_yolu"]     = rctx.Doc.PathName,
                ["revit_versiyonu"]= rctx.UiApp.Application.VersionName,
                ["eleman_sayisi"]  = total,
                ["workshared"]     = rctx.Doc.IsWorkshared,
                ["tarih"]          = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
            };
        }

        [EgOp("op_health_check",
            Description = "Kayıtlı op sayısını ve kategorilerini döner (sistem sağlık kontrolü)",
            Category    = "Yardımcı")]
        public static Dictionary<string, object?> OpHealthCheck(OpContext ctx)
        {
            var registry = EGBIMOTO.Core.Ops.OpRegistry.Instance;
            var ops      = registry.GetAll();
            var cats     = ops.GroupBy(o => o.Category ?? "—")
                .ToDictionary(g => g.Key, g => (object?)g.Count());
            var txCount  = ops.Count(o => o.RequiresTransaction);
            ctx.Log($"  op_health_check: {ops.Count} op, {cats.Count} kategori, {txCount} yazma op");
            return new()
            {
                ["toplam_op"]       = ops.Count,
                ["kategoriler"]     = cats,
                ["yazma_op_sayisi"] = txCount,
                ["tarih"]           = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
            };
        }
    }
}
