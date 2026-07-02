// ============================================================
// EGBIMOTO — ManifestRibbonCommandBase  (v11.1 — GERÇEK FIX)
// Tüm manifest ribbon butonlarının ortak çalışma mantığı.
//
// v10.7'den FARKI: btnName artık commandData.JournalData'dan
// OKUNMUYOR (bu değer normal kullanımda hep boştu — kök neden).
// Bunun yerine her alt sınıf (ManifestRibbonCommand_0000, _0001, ...
// — bkz. ManifestRibbonCommandSlots.g.cs) kendi sabit slot index'ini
// base constructor'a geçirir. RibbonBuilder, buton oluştururken
// ManifestButtonRegistry.Allocate() ile bu slot'u manifest yoluyla
// ilişkilendirmiştir. Böylece Execute() çalıştığında "hangi buton"
// sorusu hiç sorulmaz — instance zaten kendi cevabını taşır.
// ============================================================

using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Addin.Commands
{
    /// <summary>
    /// Tüm manifest ribbon butonlarının ortak (abstract) taban sınıfı.
    /// Somut alt sınıflar yalnızca kendi slot index'ini taşır
    /// (bkz. ManifestRibbonCommandSlots.g.cs).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public abstract class ManifestRibbonCommandBase : IExternalCommand
    {
        private readonly int _slot;

        protected ManifestRibbonCommandBase(int slot) => _slot = slot;

        public Result Execute(
            ExternalCommandData commandData,
            ref string          message,
            ElementSet          elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null)
            {
                TaskDialog.Show("EGBIMOTO", "Açık bir Revit dokümanı yok.");
                return Result.Cancelled;
            }

            // ── 1. Slot → manifest yolu (JournalData YOK, doğrudan sabit) ────
            var manifestPath = ManifestButtonRegistry.Resolve(_slot);

            if (string.IsNullOrEmpty(manifestPath))
            {
                var btnName = ManifestButtonRegistry.ResolveButtonName(_slot) ?? $"slot {_slot}";
                message = $"Manifest yolu bulunamadı (buton: '{btnName}', slot: {_slot}).\n" +
                          "RibbonBuilder.AddManifestPush/Split → ManifestButtonRegistry.Allocate() " +
                          "çağrısını ve slot havuzunun (ManifestButtonRegistry.SlotCount) yeterli " +
                          "olduğunu kontrol edin.";
                TaskDialog.Show("EGBIMOTO — Yapılandırma Hatası", message);
                return Result.Failed;
            }

            // ── 2. Çalıştır: $INPUT dialog + DAG EgbimotoApp üzerinden ───────
            try
            {
                var result = EgbimotoApp.RunManifest(commandData, manifestPath);

                if (result.Success)
                    ShowSuccess(result, manifestPath);

                // Başarısız durumda EgbimotoApp.RunManifest zaten TaskDialog gösterir.
                return result.Success ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("EGBIMOTO — Beklenmedik Hata", ex.Message);
                return Result.Failed;
            }
        }

        private static void ShowSuccess(ManifestRunResult result, string manifestPath)
        {
            var title = System.IO.Path.GetFileNameWithoutExtension(manifestPath);

            var logLines = result.Log?.Count > 0
                ? "\n\n" + string.Join("\n", result.Log.TakeLast(5))
                : "";

            var dlg = new TaskDialog("EGBIMOTO — Tamamlandı")
            {
                MainInstruction = $"✓ {title}",
                MainContent     = $"{result.TotalSteps} adım tamamlandı.{logLines}",
                CommonButtons   = TaskDialogCommonButtons.Ok
            };
            dlg.Show();
        }
    }
}
