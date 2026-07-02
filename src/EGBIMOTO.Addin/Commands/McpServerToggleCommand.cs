// ============================================================
// EGBIMOTO — McpServerToggleCommand  (v11)
//
// v11 düzeltmesi (Sorun 4 — MCP toggle ribbon state):
//   Mevcut durum: McpServerToggleCommand çalışıyor ama ribbon
//   butonu ON/OFF durumunu yansıtmıyor. Kullanıcı server'ın
//   çalışıp çalışmadığını bilemez.
//
//   Çözüm: IExternalCommandAvailability implementasyonu eklendi.
//   SetCommandAvailability() her Revit idle döngüsünde çağrılır.
//   IsRunning durumuna göre buton ToolTip güncellenir.
//   RibbonBuilder'da buton için AvailabilityClassName set edilir.
//
//   Revit ribbon butonları renk/ikon değiştiremez (API kısıtı),
//   ancak ToolTip ve IsEnabled değiştirilebilir. Bu yüzden:
//   • Server KAPALI → ToolTip "MCP Server Başlat (kapalı)"
//   • Server AÇIK  → ToolTip "MCP Server Durdur (port 5577 — çalışıyor ✓)"
// ============================================================

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Server;

namespace EGBIMOTO.Addin.Commands
{
    /// <summary>
    /// MCP Server başlat/durdur toggle komutu.
    /// IExternalCommandAvailability ile ribbon butonu durumu güncellenir.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class McpServerToggleCommand : IExternalCommand
    {
        private const int DefaultPort = 5577;

        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            try
            {
                var mgr = McpServerManager.Instance;

                if (mgr.IsRunning)
                {
                    mgr.Stop();
                    TaskDialog.Show("EGBIMOTO MCP Server",
                        "MCP Server durduruldu.\n\nClaude Desktop artık Revit'e bağlanamaz.");
                    return Result.Succeeded;
                }

                mgr.Start(
                    EgbimotoApp.Registry,
                    () => EgbimotoApp.ContractsPath,
                    DefaultPort,
                    token: null);

                TaskDialog.Show("EGBIMOTO MCP Server",
                    $"MCP Server başlatıldı (port {DefaultPort}).\n\n" +
                    "Claude Desktop artık bu Revit modeline bağlanabilir.\n\n" +
                    $"• Katalog:  http://127.0.0.1:{DefaultPort}/ops\n" +
                    $"• Durum:    http://127.0.0.1:{DefaultPort}/health\n\n" +
                    "Claude Desktop'ta 'egbimoto' bağlandıysa, doğrudan Türkçe komut " +
                    "verebilirsiniz (örn. 'tüm kapıları say ve rapor çıkar').");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                msg = $"MCP Server başlatılamadı: {ex.Message}";
                TaskDialog.Show("EGBIMOTO MCP Server — Hata", msg);
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// v11: MCP Server durumunu ribbon butonuna yansıtır.
    /// Revit her idle döngüsünde SetCommandAvailability() çağırır.
    /// RibbonBuilder'da PushButtonData.AvailabilityClassName bu
    /// sınıfın tam adı olarak set edilir.
    /// </summary>
    /// <summary>
    /// v11: Revit her komut öncesi IsCommandAvailable() sorgular.
    /// Bu noktada UIApplication mevcut — ToolTip güncellemesi burada yapılır.
    /// </summary>
    public sealed class McpServerAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selectedCategories)
        {
            // MCP butonu her zaman tıklanabilir — toggle Execute() içinde
            RefreshTooltip(app);
            return true;
        }

        private static void RefreshTooltip(UIApplication uiApp)
        {
            try
            {
                var isRunning = McpServerManager.Instance?.IsRunning ?? false;
                var tooltip   = isRunning
                    ? "⬛ MCP Server DURDUR  (port 5577 — çalışıyor ✓)"
                    : "▶ MCP Server BAŞLAT  (Claude Desktop bağlantısı)";
                var tipDesc   = isRunning
                    ? "Server aktif — http://127.0.0.1:5577/health"
                    : "Başlatmak için tıklayın.";

                foreach (var panel in uiApp.GetRibbonPanels("EGBIMOTO"))
                foreach (var item  in panel.GetItems())
                {
                    if (item is PushButton btn && btn.Name == "EG_MCP")
                    {
                        btn.ToolTip            = tooltip;
                        btn.LongDescription = tipDesc;
                        return;
                    }
                }
            }
            catch { /* Ribbon henüz oluşmamışsa sessizce geç */ }
        }
    }
}
