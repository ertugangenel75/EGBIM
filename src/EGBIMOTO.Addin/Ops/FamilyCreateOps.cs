using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO Aile Oluşturma Operasyonları — v9
    ///
    /// FamilyDocument API ile programatik aile oluşturma ve düzenleme.
    /// Not: Revit'in constraint sistemi (EqualityConstraint) kararsız olabilir;
    /// karmaşık geometri için template-based yaklaşım önerilir.
    ///
    /// Op listesi:
    ///   family_open_template    — Aile şablonu aç / yeni aile belgesi oluştur
    ///   family_add_param        — Aileye parametrik boyut ekle
    ///   family_load_to_project  — Aileyi projeye yükle (güncelle)
    ///   family_type_create      — Aile içinde yeni tip oluştur
    ///   family_batch_load       — Klasördeki tüm aileleri projeye yükle
    /// </summary>
    public static class FamilyCreateOps
    {
        [EgOp("family_open_template",
            Description =
                "Bir aile şablonu dosyasını açar veya mevcut aile belgesini döner.\n" +
                "params: template_path (zorunlu) — .rft veya .rfa dosyası.\n" +
                "Çıktı: Dictionary — family_doc_title, is_new.",
            Category = "Aile")]
        public static Dictionary<string, object?> FamilyOpenTemplate(OpContext ctx)
        {
            var rctx         = (RevitOpContext)ctx;
            var uiApp        = rctx.UiApp;
            var templatePath = ctx.RequireString("template_path");

            if (!File.Exists(templatePath))
                throw new FileNotFoundException(
                    $"[{ctx.CurrentStepId}] Şablon dosyası bulunamadı: {templatePath}");

            var ext = Path.GetExtension(templatePath).ToLowerInvariant();
            Document? famDoc = null;
            bool isNew       = false;

            if (ext == ".rft")
            {
                // Yeni aile — şablondan oluştur
                famDoc = uiApp.Application.NewFamilyDocument(templatePath);
                isNew  = true;
            }
            else if (ext == ".rfa")
            {
                // Mevcut aile — aç
                famDoc = uiApp.Application.OpenDocumentFile(templatePath);
            }
            else
            {
                throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Geçersiz şablon uzantısı '{ext}'. .rft veya .rfa bekleniyor.");
            }

            ctx.Log($"  → Aile belgesi açıldı: {famDoc?.Title ?? "?"} (yeni: {isNew})");
            return new Dictionary<string, object?>
            {
                ["family_doc_title"]  = famDoc?.Title,
                ["template_path"]     = templatePath,
                ["is_new"]            = isNew,
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("family_add_param",
            Description =
                "Aktif aile belgesine yeni bir instance veya type parametresi ekler.\n" +
                "params: family_path (zorunlu), param_name (zorunlu),\n" +
                "        param_type (opsiyonel: Length|Area|Volume|Text|Integer|YesNo, default:Length),\n" +
                "        is_instance (opsiyonel: true=instance, false=type, default:true),\n" +
                "        group (opsiyonel: Dimensions|Identity|Materials, default:Dimensions).\n" +
                "Çıktı: Dictionary — eklenen param adı.",
            Category = "Aile",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> FamilyAddParam(OpContext ctx)
        {
            var rctx       = (RevitOpContext)ctx;
            var app        = rctx.UiApp.Application;
            var familyPath = ctx.RequireString("family_path");
            var paramName  = ctx.RequireString("param_name");
            var paramType  = ctx.GetString("param_type", "Length");
            var isInstance = ctx.GetBool("is_instance", true);
            var groupStr   = ctx.GetString("group", "Dimensions");

            if (!File.Exists(familyPath))
                throw new FileNotFoundException($"Aile dosyası bulunamadı: {familyPath}");

            using var famDoc = app.OpenDocumentFile(familyPath);
            if (!famDoc.IsFamilyDocument)
                throw new InvalidOperationException($"'{familyPath}' bir aile belgesi değil.");

            var fm = famDoc.FamilyManager;

            // Tip çözümle
            ForgeTypeId specTypeId = paramType.ToLowerInvariant() switch
            {
                "length"  => SpecTypeId.Length,
                "area"    => SpecTypeId.Area,
                "volume"  => SpecTypeId.Volume,
                "integer" => SpecTypeId.Int.Integer,
                "yesno"   => SpecTypeId.Boolean.YesNo,
                _         => SpecTypeId.String.Text,
            };

            // Grup çözümle
            ForgeTypeId groupTypeId = groupStr.ToLowerInvariant() switch
            {
                "identity"  => GroupTypeId.IdentityData,
                "materials" => GroupTypeId.Materials,
                _           => GroupTypeId.Constraints,
            };

            using var tx = new Transaction(famDoc, "EGBIMOTO: Param Ekle");
            tx.Start();

            // Var mı kontrol et
            var existing = fm.GetParameters()
                .FirstOrDefault(p => string.Equals(p.Definition.Name, paramName,
                                                    StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                tx.RollBack();
                ctx.Log($"  ⚠ '{paramName}' zaten mevcut — atlandı");
                return new Dictionary<string, object?>
                {
                    ["param_name"]  = paramName,
                    ["action"]      = "skipped_existing",
                };
            }

            var newParam = fm.AddParameter(paramName, groupTypeId, specTypeId, isInstance);
            tx.Commit();

            // Kaydet
            var saveOpts = new SaveAsOptions { OverwriteExistingFile = true };
            famDoc.SaveAs(familyPath, saveOpts);
            famDoc.Close(false);

            ctx.Log($"  → '{paramName}' parametresi eklendi ({(isInstance ? "instance" : "type")})");
            return new Dictionary<string, object?>
            {
                ["param_name"]   = paramName,
                ["param_type"]   = paramType,
                ["is_instance"]  = isInstance,
                ["action"]       = "created",
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("family_load_to_project",
            Description =
                "Bir .rfa dosyasını aktif Revit projesine yükler (varsa günceller).\n" +
                "params: family_path (zorunlu),\n" +
                "        overwrite (opsiyonel: true=güncelle, default:true).\n" +
                "Çıktı: Dictionary — family_name, action (loaded/updated/skipped).",
            Category = "Aile",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> FamilyLoadToProject(OpContext ctx)
        {
            var rctx       = (RevitOpContext)ctx;
            var doc        = rctx.Doc;
            var familyPath = ctx.RequireString("family_path");
            var overwrite  = ctx.GetBool("overwrite", true);

            if (!File.Exists(familyPath))
                throw new FileNotFoundException($"Aile dosyası bulunamadı: {familyPath}");

            var familyName = Path.GetFileNameWithoutExtension(familyPath);
            string action;

            // Var mı kontrol
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => string.Equals(f.Name, familyName,
                                                   StringComparison.OrdinalIgnoreCase));

            if (existing != null && !overwrite)
            {
                ctx.Log($"  ⚠ '{familyName}' zaten yüklü, overwrite=false — atlandı");
                return new Dictionary<string, object?>
                {
                    ["family_name"] = familyName,
                    ["action"]      = "skipped",
                };
            }

            using var tx = new Transaction(doc, $"EGBIMOTO: Aile Yükle — {familyName}");
            tx.Start();

            bool loaded = doc.LoadFamily(familyPath, out var loadedFamily);
            action = loaded ? (existing != null ? "updated" : "loaded") : "failed";

            tx.Commit();

            ctx.Log($"  → '{familyName}' {action}");
            return new Dictionary<string, object?>
            {
                ["family_name"]   = familyName,
                ["family_id"]     = loadedFamily?.Id.Value,
                ["action"]        = action,
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("family_type_create",
            Description =
                "Bir aile içinde yeni tip oluşturur veya varolan tipin parametrelerini günceller.\n" +
                "params: family_path (zorunlu), type_name (zorunlu),\n" +
                "        params (opsiyonel) — Dictionary<string,object> param adı→değer.\n" +
                "Çıktı: Dictionary — type_name, action.",
            Category = "Aile",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> FamilyTypeCreate(OpContext ctx)
        {
            var rctx       = (RevitOpContext)ctx;
            var app        = rctx.UiApp.Application;
            var familyPath = ctx.RequireString("family_path");
            var typeName   = ctx.RequireString("type_name");
            var paramValues = ctx.GetParam<Dictionary<string, object?>>("params",
                              new Dictionary<string, object?>());

            using var famDoc = app.OpenDocumentFile(familyPath);
            if (!famDoc.IsFamilyDocument)
                throw new InvalidOperationException($"'{familyPath}' aile belgesi değil.");

            var fm     = famDoc.FamilyManager;
            string action;

            using var tx = new Transaction(famDoc, "EGBIMOTO: Tip Oluştur");
            tx.Start();

            // Var mı?
            FamilyType? famType = null;
            foreach (FamilyType t in fm.Types)
            {
                if (string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    famType = t;
                    break;
                }
            }

            if (famType == null)
            {
                fm.NewType(typeName);
                // Yeni tip şu an aktif
                action = "created";
            }
            else
            {
                fm.CurrentType = famType;
                action = "updated";
            }

            // Parametreleri yaz
            foreach (var kv in paramValues)
            {
                var p = fm.get_Parameter(kv.Key);
                if (p == null) continue;

                try
                {
                    if (kv.Value is double d)     fm.Set(p, d);
                    else if (kv.Value is int i)   fm.Set(p, i);
                    else if (kv.Value is string s) fm.Set(p, s);
                    else if (kv.Value != null)
                        fm.Set(p, Convert.ToDouble(kv.Value));
                }
                catch (Exception ex)
                {
                    ctx.Log($"  ✗ Param '{kv.Key}' yazılamadı: {ex.Message}");
                }
            }

            tx.Commit();

            var saveOpts = new SaveAsOptions { OverwriteExistingFile = true };
            famDoc.SaveAs(familyPath, saveOpts);
            famDoc.Close(false);

            ctx.Log($"  → Tip '{typeName}' {action}: {familyPath}");
            return new Dictionary<string, object?>
            {
                ["type_name"]   = typeName,
                ["family_path"] = familyPath,
                ["action"]      = action,
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("family_batch_load",
            Description =
                "Belirtilen klasördeki tüm .rfa dosyalarını projeye toplu yükler.\n" +
                "params: folder_path (zorunlu), pattern (opsiyonel, default:*.rfa),\n" +
                "        overwrite (opsiyonel, default:true).\n" +
                "Çıktı: List<Dictionary> — her aile için yükleme sonucu.",
            Category = "Aile",
            RequiresTransaction = true)]
        public static List<Dictionary<string, object?>> FamilyBatchLoad(OpContext ctx)
        {
            var rctx       = (RevitOpContext)ctx;
            var doc        = rctx.Doc;
            var folderPath = ctx.RequireString("folder_path");
            var pattern    = ctx.GetString("pattern", "*.rfa");
            var overwrite  = ctx.GetBool("overwrite", true);

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Klasör bulunamadı: {folderPath}");

            var files = Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories);
            var results = new List<Dictionary<string, object?>>();

            using var tx = new Transaction(doc, "EGBIMOTO: Toplu Aile Yükle");
            tx.Start();

            foreach (var filePath in files)
            {
                var famName = Path.GetFileNameWithoutExtension(filePath);
                try
                {
                    var existing = new FilteredElementCollector(doc)
                        .OfClass(typeof(Family))
                        .Cast<Family>()
                        .Any(f => string.Equals(f.Name, famName, StringComparison.OrdinalIgnoreCase));

                    if (existing && !overwrite)
                    {
                        results.Add(new Dictionary<string, object?>
                            { ["family_name"] = famName, ["action"] = "skipped" });
                        continue;
                    }

                    bool loaded = doc.LoadFamily(filePath, out _);
                    results.Add(new Dictionary<string, object?>
                    {
                        ["family_name"] = famName,
                        ["action"]      = loaded ? (existing ? "updated" : "loaded") : "failed",
                        ["path"]        = filePath,
                    });
                }
                catch (Exception ex)
                {
                    ctx.Log($"  ✗ '{famName}' yükleme hatası: {ex.Message}");
                    results.Add(new Dictionary<string, object?>
                        { ["family_name"] = famName, ["action"] = "error", ["error"] = ex.Message });
                }
            }

            tx.Commit();

            var loadedCount = results.Count(r => r["action"]?.ToString() == "loaded");
            var updatedCount = results.Count(r => r["action"]?.ToString() == "updated");
            ctx.Log($"  → {files.Length} dosya: {loadedCount} yüklendi, {updatedCount} güncellendi");
            return results;
        }
    }
}
