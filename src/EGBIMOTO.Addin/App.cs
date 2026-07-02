// Copyright 2026 Ertuğan Genel — Apache 2.0

using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Commands;
using EGBIMOTO.Addin.UI;

namespace EGBIMOTO.Addin
{
    /// <summary>
    /// EGBIMOTO Addin giriş noktası — V2.1
    ///
    /// Ribbon yapısı (hibrit — statik + dinamik manifest butonları):
    ///   Tab: EGBIMOTO
    ///     Panel: BIM Veri       → IFC, IDS, Parametre
    ///     Panel: Hesap          → Poz Eşle, Cost, Kalıp
    ///     Panel: Otomasyon      → Manifest Browser, MCP Server
    ///     Panel: Sık Kullanılan → 6 "tam" manifest butonu [YENİ]
    ///     Panel: MEP Hesap      → Sıhhi/Elektrik/Yangın/Mekanik/Boşluk SplitButton [YENİ]
    ///     Panel: Yapısal        → Donatı/Döşeme/Temel SplitButton [YENİ]
    ///     Panel: QA/Rapor       → Model QA, Teslim SplitButton [YENİ]
    ///
    /// Özelleştirme:
    ///   manifests/ribbon_config.json → kendi panel/grup/manifest düzenin
    ///   Resources/Icons/            → özel ikonlar (kategori.png, 32x32)
    /// </summary>
    public sealed class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                var addinDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                EgbimotoApp.Initialize(addinDir);

                // Tab oluştur
                try { app.CreateRibbonTab("EGBIMOTO"); } catch { /* zaten var */ }

                // Ribbon inşa et (statik + dinamik manifest butonları)
                RibbonBuilder.Build(app, addinDir);

                // v14: Element Inspector — dockable pane + canlı seçim takibi
                try
                {
                    app.RegisterDockablePane(
                        UI.Inspector.ElementInspectorPaneProvider.PaneId,
                        "EGBIMOTO — Eleman İncelemesi",
                        new UI.Inspector.ElementInspectorPaneProvider());
                    UI.Inspector.ElementInspectorHook.Register(app);
                }
                catch (Exception ex)
                {
                    // Panel kaydı başarısız olsa bile ribbon/manifest akışı çalışmaya devam etmeli.
                    System.Diagnostics.Debug.WriteLine($"[EGBIMOTO] Element Inspector kaydı başarısız: {ex.Message}");
                }

                // v11: MCP ribbon state — McpServerAvailability.IsCommandAvailable
                // Revit her komut öncesi availability'yi sorgular; ToolTip orada güncellenir.

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("EGBIMOTO — Başlatma Hatası", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
