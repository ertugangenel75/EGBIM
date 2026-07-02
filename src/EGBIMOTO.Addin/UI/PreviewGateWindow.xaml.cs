using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using EGBIMOTO.Core.Preview;
using EGBIMOTO.Core.DAG;

namespace EGBIMOTO.Addin.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PreviewGateWindow  (v1.0)
    //
    //  Kullanıcı onay penceresi — PreviewGeometryDto'yu Three.js WebView2'de gösterir.
    //
    //  Kullanım (DagExecutor UserGateCallback içinde):
    //    bool confirmed = PreviewGateWindow.ShowModal(dto);
    //    // confirmed = true  → "✓ Onayla" tıklandı
    //    // confirmed = false → "✗ İptal" tıklandı veya pencere kapatıldı
    //
    //  WebView2 köprüsü:
    //    C# → JS: ExecuteScriptAsync("loadPreview(JSON)")
    //    JS → C#: window.chrome.webview.postMessage("confirm" | "cancel" | "reset_camera")
    //
    //  Bağımlılık:
    //    • Microsoft.Web.WebView2 NuGet paketi gerekli
    //    • preview_viewer.html — Resources/preview_viewer.html (CopyAlways)
    //    • three.min.js       — Resources/three.min.js (CopyAlways)
    // ═══════════════════════════════════════════════════════════════════════════

    public partial class PreviewGateWindow : Window
    {
        // ── Durum ─────────────────────────────────────────────────────────────
        private PreviewGeometryDto? _dto;
        private bool  _confirmed   = false;
        private bool  _webViewReady = false;
        private string? _pendingJson = null;  // WebView hazır değilken beklet

        // ── Statik factory ────────────────────────────────────────────────────

        /// <summary>
        /// Modal dialog olarak gösterir. Revit ana thread'inde çağrılmalı.
        /// DagExecutor.UserGateCallback içinden çağırılır.
        /// </summary>
        public static bool ShowModal(PreviewGeometryDto dto)
        {
            var window = new PreviewGateWindow(dto);
            window.ShowDialog();
            return window._confirmed;
        }

        // ── Constructor ───────────────────────────────────────────────────────

        public PreviewGateWindow(PreviewGeometryDto dto)
        {
            InitializeComponent();
            _dto = dto;

            // Header metinleri
            TitleText.Text    = dto.OperationName;
            SubtitleText.Text = dto.Description;
            CountBadge.Text   = $"{dto.ElementCount} eleman";

            // Stats bar
            PopulateStats(dto);

            // Uyarılar
            if (dto.Warnings.Count > 0)
            {
                WarningBorder.Visibility = Visibility.Visible;
                WarningText.Text = "⚠ " + string.Join(" | ", dto.Warnings);
            }

            // WebView2 başlat
            InitWebView();
        }

        // ── WebView2 init ─────────────────────────────────────────────────────

        private async void InitWebView()
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Path.GetTempPath(), "EGBIMOTO_WebView2");

                var env = await CoreWebView2Environment.CreateAsync(
                    null, userDataFolder);

                await WebView.EnsureCoreWebView2Async(env);

                // WebMessage köprüsü: JS → C#
                WebView.CoreWebView2.WebMessageReceived += OnWebMessage;

                // HTML yükle
                var htmlPath = GetHtmlPath();
                if (File.Exists(htmlPath))
                {
                    WebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                    WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                }
                else
                {
                    // HTML bulunamazsa basit hata göster
                    WebView.CoreWebView2.NavigateToString(FallbackHtml());
                    _webViewReady = true;
                }
            }
            catch (Exception ex)
            {
                // WebView2 yüklenemedi — fallback sadece text
                MessageBox.Show(
                    $"WebView2 başlatılamadı: {ex.Message}\n\n" +
                    "Microsoft.Web.WebView2 NuGet paketinin yüklü olduğundan emin olun.\n\n" +
                    $"Önizleme: {_dto?.Description}",
                    "EGBIMOTO — Önizleme",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _webViewReady = true;
            }
        }

        private void OnNavigationCompleted(object? sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            _webViewReady = true;
            if (_pendingJson != null)
            {
                SendPreviewJson(_pendingJson);
                _pendingJson = null;
            }
            else if (_dto != null)
            {
                var json = SerializeDto(_dto);
                SendPreviewJson(json);
            }
        }

        private async void SendPreviewJson(string json)
        {
            try
            {
                // JSON boyutu → script injection güvenliği için
                // Büyük modeller için postMessage daha güvenli
                if (json.Length < 1_000_000)
                {
                    await WebView.CoreWebView2
                        .ExecuteScriptAsync($"loadPreview({json})");
                }
                else
                {
                    // Büyük veri → postMessage ile gönder
                    WebView.CoreWebView2.PostWebMessageAsString(
                        $"PREVIEW_DATA:{json}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PreviewGateWindow] JSON gönderme hatası: {ex.Message}");
            }
        }

        // ── WebView2 → C# köprüsü ────────────────────────────────────────────

        private void OnWebMessage(object? sender,
            CoreWebView2WebMessageReceivedEventArgs e)
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
                    // WebView JS içinde reset_camera() çağrılır
                    break;
            }
        }

        // ── WPF Buton handler'ları ────────────────────────────────────────────

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

        private async void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            if (_webViewReady)
            {
                try { await WebView.CoreWebView2.ExecuteScriptAsync("resetCamera()"); }
                catch { /* ignore */ }
            }
        }

        // ── Stats bar ─────────────────────────────────────────────────────────

        private void PopulateStats(PreviewGeometryDto dto)
        {
            StatsPanel.Children.Clear();
            foreach (var kv in dto.Stats)
            {
                var badge = new Border
                {
                    Background     = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E)),
                    CornerRadius   = new CornerRadius(10),
                    Margin         = new Thickness(0, 0, 8, 0),
                    Padding        = new Thickness(10, 3, 10, 3)
                };
                var txt = new TextBlock
                {
                    Text       = $"{kv.Key}: {kv.Value}",
                    FontSize   = 11,
                    Foreground = new SolidColorBrush(Colors.LightGray)
                };
                badge.Child = txt;
                StatsPanel.Children.Add(badge);
            }

            // Mesh sayısı
            var meshBadge = new Border
            {
                Background   = new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A)),
                CornerRadius = new CornerRadius(10),
                Padding      = new Thickness(10, 3, 10, 3)
            };
            meshBadge.Child = new TextBlock
            {
                Text       = $"{dto.Meshes.Count} mesh",
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x70, 0xAD, 0x47))
            };
            StatsPanel.Children.Add(meshBadge);
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private static string GetHtmlPath()
        {
            // 1. Addin klasörünün yanında Resources/
            var addinDir  = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var candidate = Path.Combine(addinDir, "Resources", "preview_viewer.html");
            if (File.Exists(candidate)) return candidate;

            // 2. Addin klasörünün kendisi
            candidate = Path.Combine(addinDir, "preview_viewer.html");
            if (File.Exists(candidate)) return candidate;

            return candidate; // bulunamazsa boş dön — NavCompleted'da handle edilir
        }

        private static string SerializeDto(PreviewGeometryDto dto)
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented        = false
            };
            return JsonSerializer.Serialize(dto, opts);
        }

        private static string FallbackHtml() =>
            "<html><body style='background:#1e1e2e;color:#ccc;font-family:sans-serif;" +
            "padding:40px'><h2>WebView2 Hazır Değil</h2>" +
            "<p>preview_viewer.html dosyası bulunamadı veya WebView2 yüklenemedi.</p>" +
            "<p>Resources klasörüne preview_viewer.html ve three.min.js kopyalayın.</p>" +
            "</body></html>";
    }
}
