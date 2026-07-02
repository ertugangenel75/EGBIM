using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.FamilyLibrary;
using EGBIMOTO.Addin.Ops;
using RvtApp = Autodesk.Revit.ApplicationServices.Application;   // CS0104: System.Windows.Application çakışması

namespace EGBIMOTO.Addin.UI.FamilyLibrary
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — FamilyLibraryWindow  (v14)
    //
    //  Aile kütüphanesi tarayıcı: klasör seç → .rfa'ları tek tek aç/oku/kapat →
    //  param_guid_map.json SSoT'i ile karşılaştır → GUID çakışması / eksik
    //  paylaşım / bilinmeyen paylaşımlı param raporu.
    //
    //  Senkron tarama notu: FamilyLibraryScanner.ScanOne() Revit API'ye dokunur,
    //  bu yüzden ana thread'de senkron çalışır. UI'ın donmaması ve İptal
    //  butonunun tepki vermesi için her dosyadan sonra
    //  DispatcherFrameHelper.PumpUiMessages() çağrılır.
    // ═══════════════════════════════════════════════════════════════════════════
    public partial class FamilyLibraryWindow : Window
    {
        private readonly RvtApp _app;
        private string? _folder;
        private bool _cancelRequested;
        private List<FamilyListItem> _allResults = new();

        private sealed class FamilyListItem
        {
            public FamilyScanResult Result { get; set; } = null!;
            public string FamilyName => Result.FamilyName;
            public string CategoryName => Result.CategoryName;
            public Brush StatusColor => Result.Overall switch
            {
                FamilyOverallStatus.Conflict   => new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55)),
                FamilyOverallStatus.Warning    => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x6C)),
                FamilyOverallStatus.OpenFailed => new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8)),
                _                              => new SolidColorBrush(Color.FromRgb(0x50, 0xFA, 0x7B)),
            };
        }

        public FamilyLibraryWindow(RvtApp app)
        {
            InitializeComponent();
            _app = app;
        }

        // ── Klasör seçimi ────────────────────────────────────────────────────
        private void PickFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Aile Kütüphanesi Kök Klasörünü Seç",
            };
            if (dlg.ShowDialog() == true)
            {
                _folder = dlg.FolderName;
                FolderPathBox.Text = _folder;
                ScanBtn.IsEnabled = true;
            }
        }

        // ── Tarama ───────────────────────────────────────────────────────────
        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_folder)) return;

            var files = FamilyLibraryScanner.FindFamilyFiles(_folder);
            if (files.Count == 0)
            {
                TaskDialog.Show("EGBIMOTO — Aile Kütüphanesi", "Bu klasörde .rfa dosyası bulunamadı.");
                return;
            }
            if (files.Count > 300)
            {
                var confirm = TaskDialog.Show("EGBIMOTO — Aile Kütüphanesi",
                    $"{files.Count} aile bulundu. Her aile açılıp okunduğu için bu işlem uzun sürebilir " +
                    "(yaklaşık 1-3 saniye/aile). Devam edilsin mi?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (confirm != TaskDialogResult.Yes) return;
            }

            var ssotPath = Path.Combine(EgbimotoData.DataRoot, "mapping", "param_guid_map.json");
            var scanner = new FamilyLibraryScanner(ssotPath);

            _cancelRequested = false;
            ScanBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;
            PickFolderBtn.IsEnabled = false;
            ExportCsvBtn.IsEnabled = false;
            _allResults = new List<FamilyListItem>();
            FamilyList.ItemsSource = null;

            int done = 0;
            foreach (var file in files)
            {
                if (_cancelRequested)
                {
                    ProgressText.Text = $"İptal edildi — {done}/{files.Count} tarandı.";
                    break;
                }

                var result = scanner.ScanOne(_app, file);
                _allResults.Add(new FamilyListItem { Result = result });

                done++;
                ScanProgress.Value = 100.0 * done / files.Count;
                ProgressText.Text = $"{done}/{files.Count} — {result.FamilyName}";

                // Ana thread'i UI mesajları için pompala (İptal tıklaması ve
                // görsel güncelleme buradan geçer — Task.Run KULLANILMIYOR
                // çünkü Revit API yalnızca bu thread'de çağrılabilir).
                DispatcherFrameHelper.PumpUiMessages();
            }

            FamilyList.ItemsSource = _allResults;
            UpdateSummary();

            ScanBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
            PickFolderBtn.IsEnabled = true;
            ExportCsvBtn.IsEnabled = _allResults.Count > 0;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => _cancelRequested = true;

        private void UpdateSummary()
        {
            int conflict = _allResults.Count(r => r.Result.Overall == FamilyOverallStatus.Conflict);
            int warning  = _allResults.Count(r => r.Result.Overall == FamilyOverallStatus.Warning);
            int failed   = _allResults.Count(r => r.Result.Overall == FamilyOverallStatus.OpenFailed);
            int ok       = _allResults.Count - conflict - warning - failed;

            SummaryText.Text = $"{_allResults.Count} aile  ·  ✓ {ok} uyumlu  ·  ⚠ {warning} uyarı  ·  " +
                                $"✗ {conflict} GUID çakışması" + (failed > 0 ? $"  ·  {failed} açılamadı" : "");
        }

        // ── Filtre ───────────────────────────────────────────────────────────
        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = FilterBox.Text?.Trim() ?? "";
            if (q.Length == 0) { FamilyList.ItemsSource = _allResults; return; }

            FamilyList.ItemsSource = _allResults.Where(r =>
                r.FamilyName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.CategoryName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // ── Detay paneli ─────────────────────────────────────────────────────
        private void FamilyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FamilyList.SelectedItem is not FamilyListItem item)
            {
                DetailEmptyText.Visibility = Visibility.Visible;
                DetailContent.Visibility = Visibility.Collapsed;
                return;
            }

            DetailEmptyText.Visibility = Visibility.Collapsed;
            DetailContent.Visibility = Visibility.Visible;

            DetailTitleText.Text = item.FamilyName;
            DetailPathText.Text = item.Result.OpenError != null
                ? $"{item.Result.FilePath}\n⚠ {item.Result.OpenError}"
                : item.Result.FilePath;

            var relevant = item.Result.Params
                .Where(p => p.Status != ParamComplianceStatus.Irrelevant)
                .OrderBy(p => p.Status == ParamComplianceStatus.GuidConflict ? 0 :
                              p.Status == ParamComplianceStatus.NotSharedButLooksTr ? 1 :
                              p.Status == ParamComplianceStatus.UnknownShared ? 2 : 3)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DetailParamList.ItemsSource = relevant.Count == 0
                ? new List<FrameworkElement> { new TextBlock
                    {
                        Text = "TR_/EG_ ile ilgili parametre bulunamadı.",
                        Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8)),
                        FontStyle = FontStyles.Italic, FontSize = 11.5,
                    } }
                : relevant.Select(BuildParamRow).ToList();
        }

        private static FrameworkElement BuildParamRow(FamilyParamStatus p)
        {
            var (dotColor, label) = p.Status switch
            {
                ParamComplianceStatus.Ok                  => ("#50FA7B", "uyumlu"),
                ParamComplianceStatus.GuidConflict         => ("#FF5555", "GUID ÇAKIŞMASI — SSoT'ten farklı"),
                ParamComplianceStatus.NotSharedButLooksTr  => ("#FFB86C", "paylaşımlı değil (TR_/EG_ isimli)"),
                ParamComplianceStatus.UnknownShared        => ("#FFB86C", "SSoT'te tanımlı değil"),
                _                                          => ("#9090A8", ""),
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            panel.Children.Add(new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dotColor)),
                Margin = new Thickness(2, 5, 8, 0), VerticalAlignment = VerticalAlignment.Top,
            });
            var textPanel = new StackPanel();
            textPanel.Children.Add(new TextBlock
            {
                Text = p.Name, FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF2)),
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = label + (string.IsNullOrEmpty(p.Guid) ? "" : $"  ({p.Guid})"),
                FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8)),
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(textPanel);
            return panel;
        }

        // ── CSV dışa aktarım ─────────────────────────────────────────────────
        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_allResults.Count == 0) return;

            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"EGBIMOTO_AileKutuphanesi_{DateTime.Now:yyyyMMdd_HHmm}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("aile;dosya_yolu;kategori;durum;param_adi;param_durumu;guid");
            foreach (var item in _allResults)
            {
                var r = item.Result;
                if (r.Params.Count == 0)
                {
                    sb.AppendLine($"{r.FamilyName};{r.FilePath};{r.CategoryName};{r.Overall};;;");
                    continue;
                }
                foreach (var p in r.Params.Where(p => p.Status != ParamComplianceStatus.Irrelevant))
                    sb.AppendLine($"{r.FamilyName};{r.FilePath};{r.CategoryName};{r.Overall};{p.Name};{p.Status};{p.Guid}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            SummaryText.Text += $"   —   CSV kaydedildi: {filePath}";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
