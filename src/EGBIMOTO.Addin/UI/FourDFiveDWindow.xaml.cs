using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using EGBIMOTO.Core.Schedule;
using EGBIMOTO.Core.DAG;

namespace EGBIMOTO.Addin.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  FourDFiveDWindow  (v1.0)
    //
    //  4D/5D yapım simülasyon penceresi.
    //  FourDFiveDDto → JSON → 4d5d_viewer.html (Three.js + timeline + S-eğrisi)
    //
    //  Kullanım (DagExecutor UserScheduleGateCallback içinde):
    //    bool confirmed = FourDFiveDWindow.ShowModal(dto);
    //
    //  WebView2 köprüsü:
    //    C# → JS: ExecuteScriptAsync("load4D5D(JSON)")
    //    JS → C#: window.chrome.webview.postMessage("confirm" | "cancel" | "reset_camera" | "export_excel")
    //
    //  Bağımlılık:
    //    • Microsoft.Web.WebView2 NuGet paketi
    //    • 4d5d_viewer.html — Resources/4d5d_viewer.html (CopyAlways)
    //    • three.min.js     — Resources/three.min.js (CopyAlways)
    // ═══════════════════════════════════════════════════════════════════════════

    public partial class FourDFiveDWindow : Window
    {
        private FourDFiveDDto? _dto;
        private bool   _confirmed    = false;
        private bool   _webViewReady = false;
        private string? _pendingJson = null;

        // ── Factory ───────────────────────────────────────────────────────────

        /// <summary>Modal dialog. Revit ana thread'inde çağrılmalı.</summary>
        public static bool ShowModal(FourDFiveDDto dto)
        {
            var win = new FourDFiveDWindow(dto);
            win.ShowDialog();
            return win._confirmed;
        }

        // ── Ctor ──────────────────────────────────────────────────────────────

        public FourDFiveDWindow(FourDFiveDDto dto)
        {
            InitializeComponent();
            _dto = dto;

            // Başlık bar
            TitleText.Text    = dto.OperationName;
            CountBadge.Text   = $"{dto.ElementCount} eleman";
            DateRangeText.Text = $"{dto.ProjectStart} → {dto.ProjectEnd}";

            // Stats
            PopulateStats(dto);

            // Uyarılar
            if (dto.Warnings.Count > 0)
            {
                WarningBorder.Visibility = Visibility.Visible;
                WarningText.Text = "⚠ " + string.Join("  |  ", dto.Warnings);
            }

            // 5D ise Excel butonunu göster
            ExportBtn.Visibility = dto.CostItems.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            InitWebView();
        }

        // ── WebView2 init ─────────────────────────────────────────────────────

        private async void InitWebView()
        {
            try
            {
                var userDataFolder = Path.Combine(Path.GetTempPath(), "EGBIMOTO_WebView2_4D5D");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await WebView.EnsureCoreWebView2Async(env);

                WebView.CoreWebView2.WebMessageReceived += OnWebMessage;

                var htmlPath = GetHtmlPath();
                if (File.Exists(htmlPath))
                {
                    WebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                    WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                }
                else
                {
                    WebView.CoreWebView2.NavigateToString(FallbackHtml());
                    _webViewReady = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"WebView2 başlatılamadı: {ex.Message}\n\n" +
                    "Microsoft.Web.WebView2 NuGet paketinin yüklü olduğundan emin olun.\n\n" +
                    $"Proje: {_dto?.ProjectStart} → {_dto?.ProjectEnd}",
                    "EGBIMOTO — 4D/5D Önizleme",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _webViewReady = true;
            }
        }

        private void OnNavigationCompleted(object? sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            _webViewReady = true;
            if (_pendingJson != null)
            {
                SendJson(_pendingJson);
                _pendingJson = null;
            }
            else if (_dto != null)
            {
                SendJson(SerializeDto(_dto));
            }
        }

        private async void SendJson(string json)
        {
            if (!_webViewReady) { _pendingJson = json; return; }
            try
            {
                if (json.Length < 1_500_000)
                    await WebView.CoreWebView2.ExecuteScriptAsync($"load4D5D({json})");
                else
                    WebView.CoreWebView2.PostWebMessageAsString($"FOURD5D_DATA:{json}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FourDFiveDWindow] JSON gönderme: {ex.Message}");
            }
        }

        // ── WebView → C# mesajları ────────────────────────────────────────────

        private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var msg = e.TryGetWebMessageAsString();

            switch (msg)
            {
                case "confirm":
                    _confirmed = true;
                    Dispatcher.Invoke(() => Close());
                    break;

                case "cancel":
                    _confirmed = false;
                    Dispatcher.Invoke(() => Close());
                    break;

                case "reset_camera":
                    // viewer içinde handle edilir
                    break;

                case "export_excel" when _dto != null:
                    Dispatcher.Invoke(() => ExportToExcel(_dto));
                    break;
            }
        }

        // ── Buton handler'ları ────────────────────────────────────────────────

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            _confirmed = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _confirmed = false;
            Close();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            _confirmed = false;
            Close();
        }

        private async void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            if (_webViewReady)
                try { await WebView.CoreWebView2.ExecuteScriptAsync("resetCamera()"); }
                catch { }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_dto != null) ExportToExcel(_dto);
        }

        private async void SpeedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_webViewReady) return;
            var item = SpeedCombo.SelectedItem as ComboBoxItem;
            var tag  = item?.Tag?.ToString() ?? "1";
            try { await WebView.CoreWebView2.ExecuteScriptAsync($"setSpeed({tag})"); }
            catch { }
        }

        // ── Başlık sürükleme ──────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        // ── Stats ─────────────────────────────────────────────────────────────

        private void PopulateStats(FourDFiveDDto dto)
        {
            StatsPanel.Children.Clear();

            foreach (var kv in dto.Stats)
                AddStatBadge(kv.Key, kv.Value, "#2A2A3E", Colors.LightGray);

            if (dto.CostItems.Count > 0)
                AddStatBadge("Toplam", $"{dto.Currency}{dto.TotalCost:N0}",
                    "#1A2A1A", Color.FromRgb(0x70, 0xAD, 0x47));

            if (dto.ScheduleItems.Count > 0)
                AddStatBadge("Program", $"{dto.ScheduleItems.Count} aktivite",
                    "#2A1A2A", Color.FromRgb(0x9B, 0x59, 0xB6));
        }

        private void AddStatBadge(string label, string value, string bg, Color fg)
        {
            var hexBg = bg;
            var bgColor = (Color)ColorConverter.ConvertFromString(hexBg);
            var badge = new Border
            {
                Background   = new SolidColorBrush(bgColor),
                CornerRadius = new CornerRadius(10),
                Margin       = new Thickness(4, 0, 0, 0),
                Padding      = new Thickness(10, 3, 10, 3)
            };
            badge.Child = new TextBlock
            {
                Text       = $"{label}: {value}",
                FontSize   = 11,
                Foreground = new SolidColorBrush(fg)
            };
            StatsPanel.Children.Add(badge);
        }

        // ── Excel export ──────────────────────────────────────────────────────

        private static void ExportToExcel(FourDFiveDDto dto)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "4D/5D Raporu Kaydet",
                    Filter     = "Excel Dosyası (*.xlsx)|*.xlsx",
                    FileName   = $"EGBIMOTO_4D5D_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                    DefaultExt = ".xlsx"
                };

                if (dlg.ShowDialog() != true) return;

                WriteFourDFiveDXlsx(dto, dlg.FileName);

                MessageBox.Show($"Rapor kaydedildi:\n{dlg.FileName}",
                    "EGBIMOTO — Excel Raporu", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel export hatası: {ex.Message}",
                    "EGBIMOTO", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Minimal SpreadsheetML xlsx writer (zipfile+xml — no openpyxl).
        /// Sheet 1: Program (schedule_items)
        /// Sheet 2: Maliyet (cost_items, eğer varsa)
        /// </summary>
        private static void WriteFourDFiveDXlsx(FourDFiveDDto dto, string path)
        {
            // Satırlar oluştur
            var schRows = new System.Text.StringBuilder();
            schRows.AppendLine(XlsxRow(new[] { "Görev", "Başlangıç", "Bitiş", "Faz", "WBS", "Element ID" }, isHeader: true));
            foreach (var si in dto.ScheduleItems)
                schRows.AppendLine(XlsxRow(new[] { si.TaskName, si.StartDate, si.EndDate, si.Phase, si.WbsCode, si.ElementId }));

            var costRows = new System.Text.StringBuilder();
            costRows.AppendLine(XlsxRow(new[] { "Poz No", "Poz Adı", "Miktar", "Birim", "Birim Fiyat", "Toplam" }, isHeader: true));
            foreach (var ci in dto.CostItems)
                costRows.AppendLine(XlsxRow(new[] { ci.PozNo, ci.PozAdi,
                    ci.Miktar.ToString("N3"), ci.Birim,
                    ci.BirimFiyat.ToString("N2"), ci.Toplam.ToString("N2") }));

            // SpreadsheetML şablonu
            const string contentTypes = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml""  ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/worksheets/sheet2.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>
</Types>";

            const string relsMain = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";

            const string wbRels = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet2.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>
</Relationships>";

            var wb = $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""
          xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""Program"" sheetId=""1"" r:id=""rId1""/>
    <sheet name=""Maliyet"" sheetId=""2"" r:id=""rId2""/>
  </sheets>
</workbook>";

            var sheet1 = WrapSheetXml(schRows.ToString());
            var sheet2 = WrapSheetXml(costRows.ToString());
            var ss     = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""0"" uniqueCount=""0""/>";

            // ZIP yaz
            if (File.Exists(path)) File.Delete(path);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

            WriteZipEntry(zip, "[Content_Types].xml", contentTypes);
            WriteZipEntry(zip, "_rels/.rels", relsMain);
            WriteZipEntry(zip, "xl/workbook.xml", wb);
            WriteZipEntry(zip, "xl/_rels/workbook.xml.rels", wbRels);
            WriteZipEntry(zip, "xl/worksheets/sheet1.xml", sheet1);
            WriteZipEntry(zip, "xl/worksheets/sheet2.xml", sheet2);
            WriteZipEntry(zip, "xl/sharedStrings.xml", ss);
        }

        private static string WrapSheetXml(string rows) =>
            $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
{rows}  </sheetData>
</worksheet>";

        private static string XlsxRow(string[] cells, bool isHeader = false)
        {
            var sb = new System.Text.StringBuilder("    <row>");
            foreach (var c in cells)
            {
                var esc = System.Security.SecurityElement.Escape(c ?? "");
                sb.Append($@"<c t=""inlineStr""><is><t>{esc}</t></is></c>");
            }
            sb.Append("</row>");
            return sb.ToString();
        }

        private static void WriteZipEntry(ZipArchive zip,
            string name, string content)
        {
            var entry  = zip.CreateEntry(name);
            using var w = new StreamWriter(entry.Open(), System.Text.Encoding.UTF8);
            w.Write(content);
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private static string GetHtmlPath()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var p1  = Path.Combine(dir, "Resources", "4d5d_viewer.html");
            if (File.Exists(p1)) return p1;
            var p2  = Path.Combine(dir, "4d5d_viewer.html");
            return p2;
        }

        private static string SerializeDto(FourDFiveDDto dto)
        {
            // FourDFiveDDto alanları [JsonPropertyName] ile tanımlandığından
            // NamingPolicy devre dışı. WriteIndented:false → küçük JSON.
            var opts = new JsonSerializerOptions
            {
                WriteIndented = false
            };
            return JsonSerializer.Serialize(dto, opts);
        }

        private static string FallbackHtml() =>
            "<html><body style='background:#1e1e2e;color:#ccc;font-family:sans-serif;padding:40px'>" +
            "<h2>4D/5D Viewer Hazır Değil</h2>" +
            "<p>4d5d_viewer.html veya WebView2 yüklenemedi.</p>" +
            "</body></html>";
    }
}
