using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace EGBIMOTO.Addin.Commands
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  1. IFC — Dışa Aktarma
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class IfcExportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var manifestPath = EgbimotoApp.ManifestPath("ifc/export_ifc.json");

            if (!File.Exists(manifestPath))
            {
                TaskDialog.Show("IFC Dışa Aktar",
                    $"Manifest henüz tanımlanmamış.\nBeklenen: {manifestPath}\n\n" +
                    "manifests/ifc/ klasörüne export_ifc.json oluşturun.");
                return Result.Cancelled;
            }

            var result = EgbimotoApp.RunManifest(data, manifestPath);
            if (result.Success)
                TaskDialog.Show("IFC Dışa Aktar", "IFC dışa aktarma tamamlandı.");
            return result.Success ? Result.Succeeded : Result.Failed;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  2. IDS — Doğrulama
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class IdsValidateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var manifestPath = EgbimotoApp.ManifestPath("ids/ids_dogrulama.json");

            if (!File.Exists(manifestPath))
            {
                TaskDialog.Show("IDS Doğrulama",
                    $"Manifest henüz tanımlanmamış.\nBeklenen: {manifestPath}");
                return Result.Cancelled;
            }

            var result = EgbimotoApp.RunManifest(data, manifestPath);
            if (result.Success)
                TaskDialog.Show("IDS Doğrulama", "IDS doğrulama tamamlandı.");
            return result.Success ? Result.Succeeded : Result.Failed;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  3. Parametre Ekle — EGBIM paylaşımlı parametreler
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class ParamAddCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var manifestPath = EgbimotoApp.ManifestPath("parametreler/add_shared_params.json");

            if (!File.Exists(manifestPath))
            {
                TaskDialog.Show("Parametre Ekle",
                    $"Manifest henüz tanımlanmamış.\nBeklenen: {manifestPath}");
                return Result.Cancelled;
            }

            var result = EgbimotoApp.RunManifest(data, manifestPath);
            if (result.Success)
                TaskDialog.Show("Parametre Ekle", "Parametreler başarıyla eklendi.");
            return result.Success ? Result.Succeeded : Result.Failed;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  4. Poz Eşle — ÇŞB 2026
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class PozMatchCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var manifestPath = EgbimotoApp.ManifestPath("maliyet/poz_esleme.json");

            if (!File.Exists(manifestPath))
            {
                TaskDialog.Show("Poz Eşleme",
                    $"Manifest henüz tanımlanmamış.\nBeklenen: {manifestPath}");
                return Result.Cancelled;
            }

            var result = EgbimotoApp.RunManifest(data, manifestPath);
            if (result.Success)
                TaskDialog.Show("Poz Eşleme", "Poz eşleme tamamlandı.");
            return result.Success ? Result.Succeeded : Result.Failed;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  5. Cost — Maliyet Hesabı
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class CostCalcCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var manifestPath = EgbimotoApp.ManifestPath("maliyet/cost_hesap.json");

            if (!File.Exists(manifestPath))
            {
                TaskDialog.Show("Maliyet Hesabı",
                    $"Manifest henüz tanımlanmamış.\nBeklenen: {manifestPath}");
                return Result.Cancelled;
            }

            var result = EgbimotoApp.RunManifest(data, manifestPath);
            if (result.Success)
                TaskDialog.Show("Maliyet Hesabı", "Maliyet hesabı tamamlandı.");
            return result.Success ? Result.Succeeded : Result.Failed;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  6. Kalıp Kontrol
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class KalipControlCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var manifestPath = EgbimotoApp.ManifestPath("kalip/kalip_kontrol.json");

            if (!File.Exists(manifestPath))
            {
                TaskDialog.Show("Kalıp Kontrol",
                    $"Manifest henüz tanımlanmamış.\nBeklenen: {manifestPath}");
                return Result.Cancelled;
            }

            var result = EgbimotoApp.RunManifest(data, manifestPath);
            if (result.Success)
                TaskDialog.Show("Kalıp Kontrol", "Kalıp kontrolü tamamlandı.");
            return result.Success ? Result.Succeeded : Result.Failed;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  7. Manifest Browser — tüm iş akışlarını listeler ve çalıştırır
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class ManifestBrowserCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var window = new UI.ManifestBrowserWindow(data);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  7b. Element Inspector — dockable panel aç/kapat  (v14)
    // ═══════════════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════════════
    //  7b. Element Inspector — dockable panel aç/kapat  (v14)
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class ToggleElementInspectorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            try
            {
                var pane = data.Application.GetDockablePane(UI.Inspector.ElementInspectorPaneProvider.PaneId);
                if (pane.IsShown()) pane.Hide();
                else pane.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("EGBIMOTO — Eleman İncelemesi", $"Panel açılamadı: {ex.Message}");
                return Result.Failed;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  7c. Aile Kütüphanesi — klasör tarama + shared param uyumluluk denetimi  (v14)
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class FamilyLibraryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var window = new UI.FamilyLibrary.FamilyLibraryWindow(data.Application.Application);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  8. 4D Yapım Simülasyonu — zaman eksenli 3D animasyon
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class FourDSimulationCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var manifestPath = EgbimotoApp.ManifestPath("zaman_maliyet/eg_4d_structural.json");

            if (!File.Exists(manifestPath))
            {
                TaskDialog.Show("4D Simülasyon",
                    $"Manifest bulunamadı.\nBeklenen: {manifestPath}\n\n" +
                    "manifests/zaman_maliyet/ klasörüne eg_4d_structural.json oluşturun.");
                return Result.Cancelled;
            }

            var result = EgbimotoApp4D5D.Run4D5DManifest(data, manifestPath);
            if (result.Success && string.IsNullOrEmpty(result.ErrorMessage))
                TaskDialog.Show("4D Simülasyon", "4D simülasyon ve parametre yazımı tamamlandı.");
            return result.Success ? Result.Succeeded : Result.Failed;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  9. 5D Maliyet Önizleme — zaman + maliyet eksenli simülasyon + S-eğrisi
    // ═══════════════════════════════════════════════════════════════════════════

    [Transaction(TransactionMode.Manual)]
    public sealed class FiveDCostPreviewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var manifestPath = EgbimotoApp.ManifestPath("zaman_maliyet/eg_5d_cost_preview.json");

            if (!File.Exists(manifestPath))
            {
                TaskDialog.Show("4D/5D Maliyet",
                    $"Manifest bulunamadı.\nBeklenen: {manifestPath}\n\n" +
                    "manifests/zaman_maliyet/ klasörüne eg_5d_cost_preview.json oluşturun.");
                return Result.Cancelled;
            }

            var result = EgbimotoApp4D5D.Run4D5DManifest(data, manifestPath);
            if (result.Success && string.IsNullOrEmpty(result.ErrorMessage))
                TaskDialog.Show("4D/5D Maliyet", "4D/5D önizleme tamamlandı.");
            return result.Success ? Result.Succeeded : Result.Failed;
        }
    }
}
