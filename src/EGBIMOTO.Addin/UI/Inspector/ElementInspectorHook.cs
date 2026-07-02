using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Addin.Inspector;   // ManifestDisciplineIndex, TrBimCategoryParamIndex

namespace EGBIMOTO.Addin.UI.Inspector
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — ElementInspectorHook  (v14)
    //
    //  UIControlledApplication.OnStartup'ta tam bir UIApplication'a doğrudan
    //  erişim YOKTUR — yalnızca ilk komut çalıştığında (ExternalCommandData.
    //  Application) ya da Idling event'inin sender'ı üzerinden elde edilir.
    //  Bu, dockable pane'i canlı seçim takibine bağlamak için standart Revit
    //  API deseni:
    //
    //    1. app.Idling += OnFirstIdle   (UIControlledApplication'da mevcut)
    //    2. İlk tetiklemede sender'ı UIApplication'a cast et
    //    3. uiApp.SelectionChanged'e abone ol, kendi Idling aboneliğimizi kaldır
    //
    //  Basitleştirme (bilinçli): ManifestDisciplineIndex yalnızca bir kez,
    //  built-in + user manifest kökleriyle inşa edilir (proje-özel manifestler
    //  dahil edilmez — ProjectManifestRoot aktif Document'a bağlıdır ve Idling
    //  anında henüz bir doküman açık olmayabilir). "İlgili Manifestler" listesi
    //  bu yüzden proje-özel manifestleri kaçırabilir; bu bir öneri listesidir,
    //  tam bir arama sonucu değildir.
    // ═══════════════════════════════════════════════════════════════════════════
    public static class ElementInspectorHook
    {
        private static UIControlledApplication? _app;
        private static bool _indexBuilt;

        public static void Register(UIControlledApplication app)
        {
            _app = app;
            _app.Idling += OnFirstIdle;
        }

        private static void OnFirstIdle(object? sender, IdlingEventArgs e)
        {
            if (_app != null)
                _app.Idling -= OnFirstIdle;   // bir kereye mahsus — Idling'de kalmaya gerek yok

            if (sender is not UIApplication uiApp) return;

            uiApp.SelectionChanged += OnSelectionChanged;

            try { BuildManifestIndexOnce(); } catch { /* index olmadan da panel çalışır */ }
        }

        private static void BuildManifestIndexOnce()
        {
            if (_indexBuilt) return;

            // v14 fix: Initialize hiçbir yerden çağrılmıyordu — _index hep null
            // kalıyor, panel her elemanda "beklenti tanımlı değil" gösteriyordu.
            TrBimCategoryParamIndex.Initialize(Ops.EgbimotoData.DataRoot);

            var manifests = ManifestLoader.LoadAllSources(
                builtInRoot: EgbimotoApp.ManifestRoot,
                userRoot:    EgbimotoApp.UserManifestRoot,
                projectRoot: null);
            ManifestDisciplineIndex.Build(manifests);
            _indexBuilt = true;
        }

        private static void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var pane = ElementInspectorPane.Instance;
            if (pane == null) return;

            if (sender is not UIApplication uiApp) return;

            var ids = e.GetSelectedElements()?.ToList() ?? new System.Collections.Generic.List<ElementId>();

            if (ids.Count == 0)
            {
                pane.ShowEmpty();
            }
            else if (ids.Count == 1)
            {
                var el = e.GetDocument()?.GetElement(ids[0]);
                if (el != null) pane.ShowElement(el, uiApp);
                else pane.ShowEmpty();
            }
            else
            {
                pane.ShowMultiSelect(ids.Count);
            }
        }
    }
}
