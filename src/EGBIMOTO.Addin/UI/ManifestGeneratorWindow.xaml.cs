using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using EGBIMOTO.Core.AI;
using EGBIMOTO.Core.Manifest;
using System.Threading.Tasks;

namespace EGBIMOTO.Addin.UI
{
    /// <summary>
    /// İki modda çalışır:
    ///   ⚡ Pattern — API'siz, ~100ms, %65-85 doğruluk
    ///   🤖 AI     — Claude API, 3-8s, %90-95 doğruluk (key gerekli)
    ///
    /// startInAiMode: true → AI modu vurgulanır ve istek kutusu odaklanır
    ///               false → Pattern modu vurgulanır
    /// </summary>
    public partial class ManifestGeneratorWindow : Window
    {
        private string?     _apiKey;
        private EgManifest? _generatedManifest;
        private string?     _generatedJson;
        private readonly bool _startInAiMode;

        public ManifestGeneratorWindow(bool startInAiMode = false)
        {
            InitializeComponent();
            _startInAiMode = startInAiMode;
            var saved = ApiKeyStore.Load();
            if (!string.IsNullOrEmpty(saved)) { pwdApiKey.Password = saved; _apiKey = saved; }
            UpdateApiKeyStatus();
            UpdateButtons();

            // Başlık moduna göre güncelle
            if (startInAiMode)
            {
                Title = "EGBIMOTO — 🤖 AI Manifest Üretici";
                // Yüklendikten sonra istek kutusuna odaklan
                Loaded += (_, _) =>
                {
                    txtRequest.Focus();
                    if (string.IsNullOrEmpty(_apiKey))
                        SetStatus("API key giriniz (sk-ant- ile başlamalı)", neutral: true);
                };
            }
            else
            {
                Title = "EGBIMOTO — ⚡ Pattern Manifest Üretici";
                Loaded += (_, _) => txtRequest.Focus();
            }
        }

        // ── API Key ───────────────────────────────────────────────────────────

        private void PwdApiKey_Changed(object sender, RoutedEventArgs e)
        {
            _apiKey = pwdApiKey.Password.Trim();
            ApiKeyStore.Save(_apiKey);
            UpdateApiKeyStatus();
            UpdateButtons();
        }

        private void UpdateApiKeyStatus()
        {
            bool ok = !string.IsNullOrEmpty(_apiKey) && _apiKey.StartsWith("sk-ant-");
            txtApiKeyHint.Text       = ok ? "✓ AI modu aktif" : "— Pattern modu";
            txtApiKeyHint.Foreground = ok
                ? (Brush)FindResource("AccentGreen")
                : (Brush)FindResource("AccentWarn");
        }

        private void TxtRequest_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => UpdateButtons();

        private void UpdateButtons()
        {
            bool hasText = !string.IsNullOrEmpty(txtRequest.Text?.Trim());
            btnPattern.IsEnabled = hasText;
            btnAI.IsEnabled      = hasText && !string.IsNullOrEmpty(_apiKey);
        }

        // ── Örnek butonlar ────────────────────────────────────────────────────

        private void BtnExample_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn) txtRequest.Text = btn.Tag?.ToString() ?? "";
            UpdateButtons();
        }

        // ── Pattern modu ──────────────────────────────────────────────────────

        private void BtnPattern_Click(object sender, RoutedEventArgs e)
        {
            var request = txtRequest.Text?.Trim();
            if (string.IsNullOrEmpty(request)) return;

            SetBusy(true);
            SetStatus("⚡ Pattern analizi...", neutral: true);
            ClearOutput();

            var engine = new PatternEngine(EgbimotoApp.ManifestRoot, EgbimotoApp.ContractsPath);
            var result = engine.Generate(request);

            SetBusy(false);

            if (result.Success && result.Manifest is not null)
            {
                ShowResult(result.Manifest, result.RawJson!, result.Warnings);
                var icon = result.Confidence >= 75 ? "✅" : result.Confidence >= 60 ? "🟡" : "🟠";
                SetStatus($"{icon} Pattern: \"{result.Manifest.Title}\" — {result.Manifest.Steps.Count} adım · Güven: %{result.Confidence}",
                    success: result.Confidence >= 70);
                if (result.Confidence < 65 && !string.IsNullOrEmpty(_apiKey))
                    txtValidationInfo.Text = "⚠ Güven düşük — AI modu daha iyi sonuç verir.";
            }
            else SetStatus($"❌ Pattern hatası: {result.ErrorMessage}", error: true);
        }

        // ── AI modu ───────────────────────────────────────────────────────────

        private async void BtnAI_Click(object sender, RoutedEventArgs e)
        {
            var request = txtRequest.Text?.Trim();
            if (string.IsNullOrEmpty(request) || string.IsNullOrEmpty(_apiKey)) return;

            SetBusy(true);
            SetStatus("🤖 AI çalışıyor... (3-8 saniye)", neutral: true);
            ClearOutput();

            ManifestGenerateResult result;
            try
            {
                // Task.Run ile Revit WPF SynchronizationContext'ten çık
                // ConfigureAwait(false) zaten ManifestGenerator içinde — bu ekstra güvence
                var generator = new ManifestGenerator(_apiKey!, EgbimotoApp.ContractsPath);
                result = await Task.Run(() => generator.GenerateAsync(request)).ConfigureAwait(true);
                // ConfigureAwait(true) → UI güncellemeleri için dispatcher'a geri dön
            }
            catch (Exception ex)
            {
                SetBusy(false);
                SetStatus($"❌ Beklenmeyen hata: {ex.Message}", error: true);
                return;
            }

            SetBusy(false);

            if (result.Success && result.Manifest is not null)
            {
                ShowResult(result.Manifest, result.RawJson!, result.Warnings);
                var retry = result.AttemptCount > 1 ? $" ({result.AttemptCount}. denemede)" : "";
                SetStatus($"✅ AI{retry}: \"{result.Manifest.Title}\" — {result.Manifest.Steps.Count} adım", success: true);
            }
            else
            {
                SetStatus($"❌ AI hatası: {result.ErrorMessage}", error: true);
                txtJsonPreview.Text = result.RawJson ?? "";
                if (result.ValidationErrors?.Count > 0)
                {
                    txtValidationInfo.Text      = "Validasyon hataları:\n" + string.Join("\n", result.ValidationErrors);
                    txtValidationInfo.Foreground = (Brush)FindResource("AccentRed");
                }
            }
        }

        // ── Kaydet ────────────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedManifest is null) return;
            var jsonToSave = txtJsonPreview.Text?.Trim();
            if (string.IsNullOrEmpty(jsonToSave)) return;

            var validator  = new ManifestValidator(EgbimotoApp.ContractsPath);
            var validation = validator.Validate(jsonToSave);
            if (!validation.IsValid)
            {
                var msg = "JSON'da hata var, yine de kaydetmek istiyor musunuz?\n\n" + string.Join("\n", validation.Errors);
                if (MessageBox.Show(msg, "Doğrulama Uyarısı", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }

            var cat = _generatedManifest.Category ?? "genel";
            var dir = Path.Combine(EgbimotoApp.ManifestRoot, cat);
            var dlg = new SaveFileDialog
            {
                Title = "Manifest Kaydet",
                InitialDirectory = Directory.Exists(dir) ? dir : EgbimotoApp.ManifestRoot,
                FileName = SanitizeFileName(_generatedManifest.Title) + ".json",
                Filter = "JSON Manifest|*.json"
            };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, jsonToSave, System.Text.Encoding.UTF8);
            SetStatus($"💾 Kaydedildi: {Path.GetFileName(dlg.FileName)}", success: true);
            MessageBox.Show($"Manifest kaydedildi:\n{dlg.FileName}\n\nManifest Browser'da görünecek.", "Kaydedildi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── UI yardımcıları ───────────────────────────────────────────────────

        private void ShowResult(EgManifest manifest, string json, List<string>? warnings)
        {
            _generatedManifest  = manifest;
            _generatedJson      = json;
            txtJsonPreview.Text = json;
            btnSave.IsEnabled   = true;

            if (warnings?.Count > 0)
            { txtValidationInfo.Text = "⚠ Uyarılar:\n" + string.Join("\n", warnings); txtValidationInfo.Foreground = (Brush)FindResource("AccentWarn"); }
            else
            { txtValidationInfo.Text = "✓ Validasyon geçti"; txtValidationInfo.Foreground = (Brush)FindResource("AccentGreen"); }
        }

        private void ClearOutput()
        {
            txtJsonPreview.Text = ""; txtValidationInfo.Text = "";
            btnSave.IsEnabled = false; _generatedManifest = null; _generatedJson = null;
        }

        private void SetBusy(bool busy)
        {
            btnPattern.IsEnabled   = !busy && !string.IsNullOrEmpty(txtRequest.Text?.Trim());
            btnAI.IsEnabled        = !busy && !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(txtRequest.Text?.Trim());
            btnSave.IsEnabled      = !busy && _generatedManifest is not null;
            progressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetStatus(string msg, bool success = false, bool error = false, bool neutral = false)
        {
            txtStatus.Text       = msg;
            txtStatus.Foreground = (success, error) switch
            {
                (true, _) => (Brush)FindResource("AccentGreen"),
                (_, true) => (Brush)FindResource("AccentRed"),
                _         => (Brush)FindResource("TextMuted")
            };
        }

        private static string SanitizeFileName(string name)
            => string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_").ToLowerInvariant().Trim('_');
    }

    // ── API Key saklama ───────────────────────────────────────────────────────

    internal static class ApiKeyStore
    {
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EGBIMOTO", "api_key.txt");

        public static void Save(string key)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path, key ?? "", System.Text.Encoding.UTF8); }
            catch { }
        }

        public static string Load()
        {
            try { return File.Exists(_path) ? File.ReadAllText(_path).Trim() : ""; }
            catch { return ""; }
        }
    }
}
