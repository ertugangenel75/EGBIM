using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace EGBIMOTO.Addin.FamilyLibrary
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — FamilyLibraryScanner  (v14)
    //
    //  Bir klasördeki tüm .rfa dosyalarını TEK TEK açar (Application.
    //  OpenDocumentFile — FamilyCreateOps.cs'de zaten kanıtlanmış desen),
    //  FamilyManager parametrelerini okur, kaydetmeden kapatır ve
    //  data/mapping/param_guid_map.json (v12.1 GUID standardizasyonunun SSoT'i)
    //  ile karşılaştırır.
    //
    //  KRİTİK — Revit API thread kısıtı: OpenDocumentFile/Close ANA THREAD'DE
    //  çalışmalıdır. Bu yüzden Scan() senkron çalışır (Task.Run YASAK) ve
    //  ilerleme/iptal, çağıran WPF penceresinin DispatcherFrameHelper.DoEvents()
    //  ile UI'ı pompaladığı bir döngü üzerinden yönetilir — bkz. FamilyLibraryWindow.
    //
    //  Kaydetmeme garantisi: famDoc `using` ile Dispose edilir (Document.Dispose
    //  → Close(false), kaydetmeden kapatır). Aile dosyalarında HİÇBİR
    //  Transaction açılmaz — yalnızca okuma yapılır.
    // ═══════════════════════════════════════════════════════════════════════════
    public sealed class FamilyLibraryScanner
    {
        private readonly Dictionary<string, string> _ssotGuidByName;

        public FamilyLibraryScanner(string paramGuidMapPath)
        {
            _ssotGuidByName = LoadSsot(paramGuidMapPath);
        }

        private static Dictionary<string, string> LoadSsot(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return map;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        map[prop.Name] = prop.Value.GetString() ?? "";
            }
            catch { /* SSoT okunamazsa tüm paylaşımlı paramlar "UnknownShared" görünür — güvenli varsayılan */ }
            return map;
        }

        /// <summary>Klasördeki tüm .rfa dosyalarını (alt klasörler dahil) listeler.</summary>
        public static List<string> FindFamilyFiles(string folder)
        {
            try
            {
                return Directory.EnumerateFiles(folder, "*.rfa", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith("~$"))   // Revit kilit/backup dosyaları
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        /// <summary>
        /// Tek bir aileyi açar, okur, kapatır. Çağıran (FamilyLibraryWindow) her
        /// dosyadan sonra UI'ı pompalamalı ve iptal bayrağını kontrol etmelidir.
        /// </summary>
        public FamilyScanResult ScanOne(Application app, string filePath)
        {
            var result = new FamilyScanResult
            {
                FilePath = filePath,
                FamilyName = Path.GetFileNameWithoutExtension(filePath),
            };

            Document? famDoc = null;
            try
            {
                famDoc = app.OpenDocumentFile(filePath);
                if (famDoc == null || !famDoc.IsFamilyDocument)
                {
                    result.Overall = FamilyOverallStatus.OpenFailed;
                    result.OpenError = "Aile belgesi değil veya açılamadı.";
                    return result;
                }

                result.CategoryName = famDoc.OwnerFamily?.FamilyCategory?.Name ?? "(kategorisiz)";

                var fm = famDoc.FamilyManager;
                foreach (FamilyParameter fp in fm.GetParameters())
                {
                    var name = fp.Definition?.Name ?? "";
                    if (string.IsNullOrEmpty(name)) continue;

                    var looksTr = name.StartsWith("TR_", StringComparison.OrdinalIgnoreCase) ||
                                  name.StartsWith("EG_", StringComparison.OrdinalIgnoreCase);

                    var status = new FamilyParamStatus { Name = name, IsShared = fp.IsShared };

                    if (fp.IsShared)
                    {
                        // v14 fix: FamilyManager parametrelerinde Definition, parametre
                        // paylaşımlı olsa bile INTERNAL tanımdır — ExternalDefinition cast'i
                        // asla tutmaz ve GUID karşılaştırması hiç çalışmazdı. Paylaşımlı
                        // GUID, FamilyParameter.GUID property'sindedir (IsShared iken geçerli).
                        // (ParamOps'taki ExternalDefinition.GUID farklı bağlam: shared param
                        // DOSYASINDAN okur, FamilyManager'dan değil.)
                        try { status.Guid = fp.GUID.ToString(); }
                        catch { /* teorik: IsShared'e rağmen GUID alınamazsa boş kalır */ }

                        if (status.Guid.Length > 0 && _ssotGuidByName.TryGetValue(name, out var ssotGuid))
                        {
                            status.Status = string.Equals(ssotGuid, status.Guid, StringComparison.OrdinalIgnoreCase)
                                ? ParamComplianceStatus.Ok
                                : ParamComplianceStatus.GuidConflict;
                        }
                        else
                        {
                            status.Status = ParamComplianceStatus.UnknownShared;
                        }
                    }
                    else if (looksTr)
                    {
                        status.Status = ParamComplianceStatus.NotSharedButLooksTr;
                    }
                    else
                    {
                        status.Status = ParamComplianceStatus.Irrelevant;
                    }

                    result.Params.Add(status);
                }

                result.Overall =
                    result.ConflictCount > 0 ? FamilyOverallStatus.Conflict :
                    result.WarningCount  > 0 ? FamilyOverallStatus.Warning  :
                    FamilyOverallStatus.Compliant;
            }
            catch (Exception ex)
            {
                result.Overall = FamilyOverallStatus.OpenFailed;
                result.OpenError = ex.Message;
            }
            finally
            {
                // Kaydetmeden kapat — Dispose, Document.Close(false) çağırır.
                try { famDoc?.Close(false); } catch { /* zaten kapanmış olabilir */ }
            }

            return result;
        }
    }
}
