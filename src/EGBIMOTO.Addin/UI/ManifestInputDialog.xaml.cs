// ============================================================
// EGBIMOTO — ManifestInputDialog  (v11)
//
// v11 düzeltmesi (Sorun 3 — $INPUT dialog async Revit query):
//   Mevcut durum: ManifestInputService.PrepareManifest() dialog
//   oluşturulurken Level/View/FamilyType/Sheet/ParameterName
//   listelerini SENKRON olarak Revit FilteredElementCollector ile
//   çekiyor. Büyük modelde (5000+ family symbol) bu 3-4 sn dialog
//   donmasına neden oluyor.
//
//   Çözüm: BuildControls() çağrısı hemen gerçekleşir (UI anında
//   açılır). Revit veri çekimi Task.Run ile arka planda yapılır.
//   Tamamlanınca Dispatcher.Invoke ile ComboBox'lar doldurulur.
//   Bu sürede ComboBox'lar "Yükleniyor..." placeholder ile
//   devre dışı kalır.
//
//   ManifestInputService: async overload eklendi — eski sync API
//   korundu (geriye dönük uyumluluk).
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using EGBIMOTO.Core.Input;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Addin.UI
{
    public partial class ManifestInputDialog : Window
    {
        private readonly List<ManifestInputDef>        _defs;
        private readonly Dictionary<string, UIElement> _controls     = new();
        // v11: Revit verisi bekleyen ComboBox'ların listesi — async yüklemede doldurulur
        private readonly List<(ComboBox cb, ManifestInputDef def)> _pendingCombos = new();

        // v11: Async yükleme tamamlanana kadar OK butonunu kilitle
        private bool _revitDataLoaded = false;

        public List<string> AvailableLevels         { get; set; } = new();
        public List<string> AvailableViews          { get; set; } = new();
        public List<string> AvailableSheets         { get; set; } = new();
        public List<string> AvailableFamilyTypes    { get; set; } = new();
        public List<string> AvailableParameterNames { get; set; } = new();
        public bool         Confirmed               { get; private set; }

        // v11: Async veri yükleme callback'i — ManifestInputService doldurur
        internal Func<Task<RevitLiveData>>? LoadRevitDataAsync { get; set; }

        public ManifestInputDialog(EgManifest manifest, List<ManifestInputDef> defs)
        {
            InitializeComponent();
            _defs = defs;
            txtManifestName.Text = manifest.Title;
            txtDescription.Text  = string.IsNullOrWhiteSpace(manifest.Description)
                ? $"{defs.Count} girdi gerekli" : manifest.Description;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 1. Kontrolleri anında kur (UI açık, kullanıcı görür)
            BuildControls();

            // 2. Eğer Revit verisi gereken combo yoksa hemen hazır
            if (_pendingCombos.Count == 0 || LoadRevitDataAsync == null)
            {
                _revitDataLoaded = true;
                btnOk.IsEnabled  = true;
                return;
            }

            // 3. Revit verisi arka planda yükle
            btnOk.IsEnabled      = false;
            txtLoadingIndicator.Text       = "⏳ Revit verisi yükleniyor...";
            txtLoadingIndicator.Visibility = Visibility.Visible;

            RevitLiveData? data = null;
            try
            {
                data = await LoadRevitDataAsync();
            }
            catch (Exception ex)
            {
                txtLoadingIndicator.Text = $"⚠ Veri yüklenemedi: {ex.Message}";
            }

            // 4. UI thread'de ComboBox'ları doldur
            if (data != null)
            {
                foreach (var (cb, def) in _pendingCombos)
                    FillCombo(cb, def, data);
            }

            txtLoadingIndicator.Visibility = Visibility.Collapsed;
            _revitDataLoaded = true;
            btnOk.IsEnabled  = true;
        }

        private void BuildControls()
        {
            pnlInputs.Children.Clear();
            _controls.Clear();
            _pendingCombos.Clear();

            foreach (var def in _defs)
            {
                var row = new Border
                {
                    Background      = (Brush)FindResource("BgRow"),
                    CornerRadius    = new CornerRadius(6),
                    Padding         = new Thickness(12, 8, 12, 8),
                    Margin          = new Thickness(0, 0, 0, 6),
                    BorderBrush     = (Brush)FindResource("BorderColor"),
                    BorderThickness = new Thickness(1)
                };
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                row.Child = grid;

                var label = new TextBlock
                {
                    Text       = def.Label,
                    Foreground = (Brush)FindResource("AccentCyan"),
                    FontSize   = 10,
                    FontWeight = FontWeights.Bold,
                    Margin     = new Thickness(0, 0, 0, 5)
                };
                Grid.SetRow(label, 0);
                grid.Children.Add(label);

                var ctrl = BuildControl(def);
                Grid.SetRow(ctrl, 1);
                grid.Children.Add(ctrl);
                _controls[def.UniqueKey] = ctrl;
                pnlInputs.Children.Add(row);
            }
        }

        private UIElement BuildControl(ManifestInputDef def)
        {
            switch (def.InputType)
            {
                case ManifestInputType.String:
                    return MakeTextBox(def.DefaultValue ?? "");

                case ManifestInputType.Number:
                {
                    var tb = MakeTextBox(def.DefaultValue ?? "0");
                    tb.PreviewTextInput += (s, e) =>
                    {
                        if (!e.Text.All(c => char.IsDigit(c) || c == '.' || c == '-'))
                            e.Handled = true;
                    };
                    if (def.NumMin.HasValue || def.NumMax.HasValue)
                        tb.ToolTip = $"Aralık: {def.NumMin?.ToString() ?? "—"} … {def.NumMax?.ToString() ?? "—"}";
                    return tb;
                }

                case ManifestInputType.Bool:
                    return new CheckBox
                    {
                        IsChecked  = def.DefaultValue == "true",
                        Foreground = (Brush)FindResource("TextMain"),
                        FontSize   = 12,
                        Content    = def.Label,
                        Margin     = new Thickness(0, 2, 0, 0)
                    };

                case ManifestInputType.FilePath:
                case ManifestInputType.PozFile:
                {
                    var filter  = def.Options ?? "*.xlsx";
                    var display = filter.Replace("*.", "").Split(';')[0].ToUpperInvariant();
                    return MakeFileRow(def.DefaultValue ?? "", $"{display} Dosyası|{filter}|Tüm Dosyalar|*.*");
                }

                case ManifestInputType.FolderPath:
                case ManifestInputType.OutputFolder:
                    return MakeFolderRow(def.DefaultValue ?? "");

                case ManifestInputType.Enum:
                {
                    var items = (def.Options ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    return MakeCombo(items, def.DefaultValue);
                }

                // v11: Revit verisi gereken tipler — placeholder ComboBox + _pendingCombos listesi
                case ManifestInputType.Level:
                case ManifestInputType.View:
                case ManifestInputType.Sheet:
                case ManifestInputType.FamilyType:
                case ManifestInputType.ParameterName:
                {
                    var cb = MakePendingCombo(def);
                    _pendingCombos.Add((cb, def));
                    return cb;
                }

                default:
                    return MakeTextBox(def.DefaultValue ?? "");
            }
        }

        // v11: Placeholder ComboBox — async yüklemede FillCombo ile doldurulur
        private ComboBox MakePendingCombo(ManifestInputDef def)
        {
            var cb = new ComboBox
            {
                IsEnabled   = false,    // Veri gelene kadar kilitli
                FontSize    = 12,
                Height      = 32,
                Background  = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)),
                Foreground  = (Brush)FindResource("TextMuted"),
                BorderBrush = (Brush)FindResource("BorderColor"),
                BorderThickness = new Thickness(1)
            };
            cb.Items.Add(new ComboBoxItem
            {
                Content    = "⏳ Yükleniyor...",
                Foreground = (Brush)FindResource("TextMuted"),
                FontStyle  = FontStyles.Italic
            });
            cb.SelectedIndex = 0;
            return cb;
        }

        // v11: Async yükleme tamamlanınca çağrılır
        private void FillCombo(ComboBox cb, ManifestInputDef def, RevitLiveData data)
        {
            List<string> items = def.InputType switch
            {
                ManifestInputType.Level         => data.Levels,
                ManifestInputType.View          => data.Views,
                ManifestInputType.Sheet         => data.Sheets,
                ManifestInputType.FamilyType    => FilterFamilyTypes(data.FamilyTypes, def.Options),
                ManifestInputType.ParameterName => data.ParameterNames,
                _                               => new List<string>()
            };

            bool editable = def.InputType == ManifestInputType.ParameterName;

            cb.Items.Clear();
            cb.IsEditable  = editable;
            cb.IsEnabled   = true;
            cb.Foreground  = (Brush)FindResource("TextMain");

            var placeholder = def.InputType switch
            {
                ManifestInputType.Level      => "Kat seçin...",
                ManifestInputType.View       => "View seçin...",
                ManifestInputType.Sheet      => "Pafta seçin...",
                ManifestInputType.FamilyType => "Family type seçin...",
                _                            => "Seçin..."
            };

            cb.Items.Add(new ComboBoxItem
            {
                Content    = placeholder,
                Foreground = (Brush)FindResource("TextMuted"),
                FontStyle  = FontStyles.Italic
            });

            foreach (var item in items) cb.Items.Add(item);

            // Varsayılan değeri seç
            if (!string.IsNullOrEmpty(def.DefaultValue))
                for (int i = 0; i < cb.Items.Count; i++)
                    if (cb.Items[i] is string s && s == def.DefaultValue) { cb.SelectedIndex = i; break; }

            if (cb.SelectedIndex < 0) cb.SelectedIndex = 0;
        }

        // ── Kontrol yardımcıları ──────────────────────────────────────────────

        private TextBox MakeTextBox(string text) => new()
        {
            Text = text,
            Background      = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)),
            Foreground      = (Brush)FindResource("TextMain"),
            BorderBrush     = (Brush)FindResource("BorderColor"),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(8, 5, 8, 5),
            FontSize        = 12,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        private UIElement MakeFileRow(string initial, string filter)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tb = MakeTextBox(initial);
            tb.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(tb, 0);
            var btn = MakeBrowseButton("📂");
            btn.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog { Title = "Dosya Seç", Filter = filter };
                if (!string.IsNullOrEmpty(tb.Text) && File.Exists(tb.Text))
                    dlg.InitialDirectory = Path.GetDirectoryName(tb.Text);
                if (dlg.ShowDialog() == true) tb.Text = dlg.FileName;
            };
            Grid.SetColumn(btn, 1);
            g.Children.Add(tb); g.Children.Add(btn);
            return g;
        }

        private UIElement MakeFolderRow(string initial)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tb = MakeTextBox(initial);
            tb.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(tb, 0);
            var btn = MakeBrowseButton("📁");
            btn.Click += (_, _) =>
            {
                using var dlg = new System.Windows.Forms.FolderBrowserDialog
                { Description = "Klasör Seç", ShowNewFolderButton = true };
                if (!string.IsNullOrEmpty(tb.Text) && Directory.Exists(tb.Text))
                    dlg.SelectedPath = tb.Text;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    tb.Text = dlg.SelectedPath;
            };
            Grid.SetColumn(btn, 1);
            g.Children.Add(tb); g.Children.Add(btn);
            return g;
        }

        private Button MakeBrowseButton(string icon) => new()
        {
            Content         = $"{icon} Seç",
            Width           = 72,
            Height          = 32,
            Background      = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5A)),
            Foreground      = (Brush)FindResource("AccentCyan"),
            BorderThickness = new Thickness(0),
            Cursor          = System.Windows.Input.Cursors.Hand,
            FontSize        = 11,
            FontWeight      = FontWeights.SemiBold
        };

        private ComboBox MakeCombo(List<string> items, string? selected,
                                   string placeholder = "Seçin...", bool editable = false)
        {
            var cb = new ComboBox
            {
                IsEditable      = editable,
                FontSize        = 12,
                Height          = 32,
                Background      = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)),
                Foreground      = (Brush)FindResource("TextMain"),
                BorderBrush     = (Brush)FindResource("BorderColor"),
                BorderThickness = new Thickness(1)
            };
            if (!string.IsNullOrEmpty(placeholder))
                cb.Items.Add(new ComboBoxItem
                {
                    Content    = placeholder,
                    Foreground = (Brush)FindResource("TextMuted"),
                    FontStyle  = FontStyles.Italic
                });
            foreach (var item in items) cb.Items.Add(item);
            if (!string.IsNullOrEmpty(selected))
                for (int i = 0; i < cb.Items.Count; i++)
                    if (cb.Items[i] is string s && s == selected) { cb.SelectedIndex = i; break; }
            else if (cb.Items.Count > 0) cb.SelectedIndex = 0;
            return cb;
        }

        // ── Okuma + Doğrulama ─────────────────────────────────────────────────

        private string? ReadControl(string uniqueKey)
        {
            if (!_controls.TryGetValue(uniqueKey, out var ctrl)) return null;
            return ctrl switch
            {
                TextBox  tb  => tb.Text.Trim(),
                CheckBox cb  => cb.IsChecked == true ? "true" : "false",
                ComboBox cmb => cmb.SelectedItem is string s ? s : cmb.Text?.Trim(),
                Grid     g   => g.Children.OfType<TextBox>().FirstOrDefault()?.Text.Trim(),
                _            => null
            };
        }

        private bool Validate()
        {
            foreach (var def in _defs)
            {
                var val = ReadControl(def.UniqueKey);
                if (string.IsNullOrEmpty(val) || val.StartsWith("Seçin") || val.StartsWith("seçin") || val.StartsWith("⏳"))
                { txtValidation.Text = $"⚠ '{def.Label}' boş bırakılamaz"; return false; }

                if (def.InputType == ManifestInputType.Number)
                {
                    if (!double.TryParse(val, out var num))
                    { txtValidation.Text = $"⚠ '{def.Label}' sayısal olmalı"; return false; }
                    if (def.NumMin.HasValue && num < def.NumMin.Value)
                    { txtValidation.Text = $"⚠ '{def.Label}' min {def.NumMin}"; return false; }
                    if (def.NumMax.HasValue && num > def.NumMax.Value)
                    { txtValidation.Text = $"⚠ '{def.Label}' max {def.NumMax}"; return false; }
                }
                if ((def.InputType == ManifestInputType.FilePath || def.InputType == ManifestInputType.PozFile)
                    && !File.Exists(val))
                { txtValidation.Text = $"⚠ Dosya bulunamadı"; return false; }
            }
            txtValidation.Text = "";
            return true;
        }

        // ── OK / Cancel ───────────────────────────────────────────────────────

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!_revitDataLoaded) return;   // async yükleme bitmemişse bekle
            if (!Validate()) return;
            foreach (var def in _defs) def.ResolvedValue = ReadControl(def.UniqueKey);
            Confirmed = true; DialogResult = true; Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        { Confirmed = false; DialogResult = false; Close(); }

        // ── Yardımcı ──────────────────────────────────────────────────────────

        private static List<string> FilterFamilyTypes(List<string> all, string? categoryFilter)
            => string.IsNullOrEmpty(categoryFilter) ? all
               : all.Where(ft => ft.Contains(categoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // RevitLiveData — ManifestInputService.cs içinde tanımlı (aynı namespace)
}
