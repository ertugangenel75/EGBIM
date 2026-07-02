using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Manifest;
using Color = System.Windows.Media.Color;             // CS0104: Autodesk.Revit.DB.Color çakışması
using Visibility = System.Windows.Visibility;         // CS0104: Autodesk.Revit.DB.Visibility çakışması

namespace EGBIMOTO.Addin.UI.Inspector
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — ElementInspectorPane  (v14)
    //
    //  Dockable pane içeriği. ElementInspectorHook (Idling → SelectionChanged)
    //  her seçim değişikliğinde ShowElement/ShowEmpty/ShowMultiSelect çağırır.
    //
    //  Sınırlama (bilinçli, dokümante): "İlgili Manifestler" linkleri belirli
    //  bir manifesti DOĞRUDAN çalıştırmaz — dockable pane bağlamında gerçek
    //  bir ExternalCommandData yoktur (yalnızca IExternalCommand.Execute
    //  içinde üretilir). Bunun yerine RevitCommandId + UIApplication.PostCommand
    //  ile Manifest Browser'ı açar ve manifest başlığını panoya kopyalar —
    //  kullanıcı Browser'da arayarak bulur. Tam entegrasyon (otomatik seçili
    //  açılış) ManifestBrowserWindow'a bir "initialFilter" parametresi
    //  eklenmesini gerektirir; kapsam dışı bırakıldı.
    // ═══════════════════════════════════════════════════════════════════════════
    public partial class ElementInspectorPane : UserControl
    {
        public static ElementInspectorPane? Instance { get; private set; }

        private UIApplication? _uiApp;
        private Dictionary<string, EgManifest> _currentManifestsByTitle = new();

        public ElementInspectorPane()
        {
            InitializeComponent();
            Instance = this;
            ShowEmpty();
        }

        // ── Durum geçişleri ───────────────────────────────────────────────────

        public void ShowEmpty()
        {
            EmptyStateText.Visibility   = Visibility.Visible;
            MultiSelectText.Visibility  = Visibility.Collapsed;
            DetailScroll.Visibility     = Visibility.Collapsed;
        }

        public void ShowMultiSelect(int count)
        {
            EmptyStateText.Visibility  = Visibility.Collapsed;
            DetailScroll.Visibility    = Visibility.Collapsed;
            MultiSelectText.Visibility = Visibility.Visible;
            MultiSelectText.Text = $"{count} eleman seçili.\n\nTekil inceleme için tek eleman seçin, " +
                                    "toplu işlem için EGBIMOTO ribbon'undaki manifest butonlarını kullanın.";
        }

        public void ShowElement(Element el, UIApplication uiApp)
        {
            _uiApp = uiApp;
            EmptyStateText.Visibility  = Visibility.Collapsed;
            MultiSelectText.Visibility = Visibility.Collapsed;
            DetailScroll.Visibility    = Visibility.Visible;

            var catName = el.Category?.Name ?? "(kategorisiz)";
            var typeName = (el.Document.GetElement(el.GetTypeId()) as ElementType)?.Name ?? "";
            ElementTitleText.Text    = string.IsNullOrEmpty(el.Name) ? catName : el.Name;
            ElementSubtitleText.Text = $"{catName}" +
                                       (string.IsNullOrEmpty(typeName) ? "" : $"  ·  {typeName}") +
                                       $"  ·  ID {Rv.IdStr(el.Id)}";

            BuildParamHealth(el);
            BuildRelatedManifests(el);
        }

        // ── TR_ Parametre Sağlığı ─────────────────────────────────────────────

        private sealed class ParamHealthRow
        {
            public string Name { get; set; } = "";
            public bool Present { get; set; }
            public string ValuePreview { get; set; } = "";
            public Brush Dot => Present
                ? new SolidColorBrush(Color.FromRgb(0x50, 0xFA, 0x7B))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
        }

        private void BuildParamHealth(Element el)
        {
            // Rv.GetCategoryId: REVIT2024 .IntegerValue / REVIT2025+ .Value köprüsü.
            // Null kategori -1 döner; (BuiltInCategory)(-1) == INVALID, ayrı dal gerekmez.
            var bic = (BuiltInCategory)Rv.GetCategoryId(el);

            var expected = EGBIMOTO.Addin.Inspector.TrBimCategoryParamIndex.ExpectedParams(bic);
            if (expected.Count == 0)
            {
                ParamHealthSummary.Text = "Bu kategori için TR_BIM parametre beklentisi tanımlı değil.";
                ParamHealthList.ItemsSource = null;
                return;
            }

            var rows = new List<ParamHealthRow>();
            int present = 0;
            foreach (var name in expected)
            {
                var p = el.LookupParameter(name);
                var has = p != null && p.HasValue;
                if (has) present++;
                rows.Add(new ParamHealthRow
                {
                    Name = name,
                    Present = has,
                    ValuePreview = has ? SafeValueString(p!) : "—",
                });
            }

            ParamHealthSummary.Text = $"{present}/{expected.Count} parametre dolu";
            ParamHealthList.ItemsSource = rows.Select(r => BuildParamRowElement(r)).ToList();
        }

        private static string SafeValueString(Parameter p)
        {
            try
            {
                var s = p.AsValueString() ?? p.AsString() ?? "";
                return s.Length > 40 ? s[..40] + "…" : s;
            }
            catch { return "?"; }
        }

        private static FrameworkElement BuildParamRowElement(ParamHealthRow row)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var dot = new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                Background = row.Dot, Margin = new Thickness(2, 5, 6, 0), VerticalAlignment = VerticalAlignment.Top
            };
            var text = new TextBlock
            {
                Text = row.Present ? $"{row.Name} = {row.ValuePreview}" : row.Name,
                Foreground = row.Present
                    ? new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF2))
                    : new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8)),
                FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
            };
            panel.Children.Add(dot);
            panel.Children.Add(text);
            return new Border { Child = panel, Margin = new Thickness(0, 0, 0, 3) };
        }

        // ── İlgili Manifestler ─────────────────────────────────────────────────

        private void BuildRelatedManifests(Element el)
        {
            // Rv.GetCategoryId: REVIT2024 .IntegerValue / REVIT2025+ .Value köprüsü.
            // Null kategori -1 döner; (BuiltInCategory)(-1) == INVALID, ayrı dal gerekmez.
            var bic = (BuiltInCategory)Rv.GetCategoryId(el);

            var related = EGBIMOTO.Addin.Inspector.ManifestDisciplineIndex.For(bic);
            _currentManifestsByTitle = related.ToDictionary(m => m.DisplayId, m => m, StringComparer.OrdinalIgnoreCase);

            if (related.Count == 0)
            {
                RelatedManifestsList.ItemsSource = null;
                NoManifestsText.Visibility = Visibility.Visible;
            }
            else
            {
                NoManifestsText.Visibility = Visibility.Collapsed;
                RelatedManifestsList.ItemsSource = related.Select(m => m.DisplayId).ToList();
            }
        }

        private void ManifestLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not TextBlock tb || tb.Tag is not string title) return;
            if (!_currentManifestsByTitle.TryGetValue(title, out var manifest)) return;

            try { Clipboard.SetText(manifest.DisplayId); } catch { /* pano erişimi başarısız olabilir — sessiz geç */ }
            OpenManifestBrowser();
        }

        private void OpenBrowser_Click(object sender, RoutedEventArgs e) => OpenManifestBrowser();

        private void OpenManifestBrowser()
        {
            if (_uiApp == null) return;
            try
            {
                var cmdId = RevitCommandId.LookupCommandId("EGBIMOTO.Addin.Commands.ManifestBrowserCommand");
                if (cmdId != null && _uiApp.CanPostCommand(cmdId))
                    _uiApp.PostCommand(cmdId);
            }
            catch { /* komut henüz ribbon'a eklenmemiş olabilir (ilk açılış anı) — sessiz geç */ }
        }
    }
}
