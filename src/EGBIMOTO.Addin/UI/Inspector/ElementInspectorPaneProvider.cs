using Autodesk.Revit.UI;

namespace EGBIMOTO.Addin.UI.Inspector
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — ElementInspectorPaneProvider  (v14)
    //
    //  IDockablePaneProvider — App.OnStartup içinde bir kez
    //  UIControlledApplication.RegisterDockablePane(...) ile kaydedilir.
    //  Revit, panel ilk gösterildiğinde SetupDockablePane'i çağırır ve
    //  FrameworkElement'i (ElementInspectorPane) alır.
    // ═══════════════════════════════════════════════════════════════════════════
    public sealed class ElementInspectorPaneProvider : IDockablePaneProvider
    {
        // Sabit GUID — bir kez üretildi, DEĞİŞTİRİLMEMELİ (değişirse Revit
        // kullanıcının kayıtlı dock pozisyonunu/boyutunu kaybeder).
        public static readonly DockablePaneId PaneId =
            new DockablePaneId(new System.Guid("E6B1A000-1234-4E00-9A00-000000000001"));

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = new ElementInspectorPane();
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right,
            };
        }
    }
}
