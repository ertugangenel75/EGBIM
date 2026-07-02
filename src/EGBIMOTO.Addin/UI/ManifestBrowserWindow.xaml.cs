// ============================================================
// EGBIMOTO — ManifestBrowserWindow  (v11)
//
// v11 düzeltmeleri:
//   1. VirtualizingStackPanel (XAML) — 459 manifest listesi
//      ItemsSource atamadan önce scroll donması giderildi.
//   2. Lint cache — ApplyFilter() her çağrıda tüm manifest'leri
//      ManifestLinter.Lint() ile işliyordu (O(n) linter × filtre
//      tetiklenme sayısı). Artık ManifestVm oluşturulunca bir kez
//      lint çalışır, sonuç _lintCache'te tutulur.
//   4. Disiplin renk sistemi — ManifestVm.DisciplineBg/Fg
//      kategori adından renk döner; XAML binding ile kategori
//      rozeti renkli gösterilir.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Autodesk.Revit.UI;
using EGBIMOTO.Core.Manifest;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace EGBIMOTO.Addin.UI
{
    public partial class ManifestBrowserWindow : Window
    {
        private readonly ExternalCommandData _commandData;
        private List<EgManifest>             _allManifests = new();
        private EgManifest?                  _selected;
        private string?                      _activeCategory;
        private string?                      _lastReportPath;
        private readonly List<StepVm>        _liveSteps = new();
        private bool                         _scopeV10Only = true;

        // v11: Lint cache — manifest dosya yolu → LintResult
        // ApplyFilter() her tetiklendiğinde yeniden lint çalışmaz.
        private readonly Dictionary<string, LintResult> _lintCache = new();

        // ── View Model ────────────────────────────────────────────────────────

        private sealed class ManifestVm
        {
            public EgManifest  Manifest   { get; init; } = null!;
            public LintResult  Lint       { get; init; } = null!;
            public string      Title      => Manifest.Title;
            public string      Category   => Manifest.Category;
            public string      TagText    => Lint.TagText;
            public string      ScoreText  => $"{Lint.Score}/10 · {Lint.Pattern} · {Lint.StepCount} adım";
            public SolidColorBrush TagBrush => new(ColorFromHex(Lint.TagColor));

            // Kaynak rozeti
            public string SourceLabel => Manifest.SourceLabel;
            public SolidColorBrush SourceBrush => new(Manifest.Source switch
            {
                ManifestSource.User    => Color.FromRgb(0x8B, 0xE9, 0xFD),
                ManifestSource.Project => Color.FromRgb(0x50, 0xFA, 0x7B),
                _                      => Color.FromRgb(0x62, 0x72, 0xA4),
            });

            // v11: Disiplin renk sistemi — kategori adından arka plan + metin rengi
            public SolidColorBrush DisciplineBg => new(DisciplineColors.Bg(Manifest.Category));
            public SolidColorBrush DisciplineFg => new(DisciplineColors.Fg(Manifest.Category));

            private static Color ColorFromHex(string hex)
            {
                hex = hex.TrimStart('#');
                return Color.FromRgb(
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16));
            }
        }

        // v11: Disiplin renk tablosu
        private static class DisciplineColors
        {
            // (arka plan, metin) çiftleri — koyu tema uyumlu
            private static readonly Dictionary<string, (Color Bg, Color Fg)> _map
                = new(StringComparer.OrdinalIgnoreCase)
            {
                ["sihhi_tesisat"]  = (Color.FromRgb(0x1A,0x3A,0x4A), Color.FromRgb(0x4F,0xC3,0xF7)),
                ["sıhhi_tesisat"]  = (Color.FromRgb(0x1A,0x3A,0x4A), Color.FromRgb(0x4F,0xC3,0xF7)),
                ["sihhi"]          = (Color.FromRgb(0x1A,0x3A,0x4A), Color.FromRgb(0x4F,0xC3,0xF7)),
                ["elektrik"]       = (Color.FromRgb(0x3A,0x30,0x10), Color.FromRgb(0xFF,0xD5,0x4F)),
                ["yangin"]         = (Color.FromRgb(0x3A,0x18,0x18), Color.FromRgb(0xEF,0x9A,0x9A)),
                ["yangın"]         = (Color.FromRgb(0x3A,0x18,0x18), Color.FromRgb(0xEF,0x9A,0x9A)),
                ["mekanik"]        = (Color.FromRgb(0x1A,0x30,0x20), Color.FromRgb(0x81,0xC7,0x84)),
                ["yapisal"]        = (Color.FromRgb(0x3A,0x20,0x10), Color.FromRgb(0xFF,0xAB,0x66)),
                ["yapısal"]        = (Color.FromRgb(0x3A,0x20,0x10), Color.FromRgb(0xFF,0xAB,0x66)),
                ["mimari"]         = (Color.FromRgb(0x1A,0x2A,0x3A), Color.FromRgb(0x80,0xDE,0xEA)),
                ["qa"]             = (Color.FromRgb(0x2A,0x1A,0x3A), Color.FromRgb(0xCE,0x93,0xD8)),
                ["ifc"]            = (Color.FromRgb(0x20,0x30,0x20), Color.FromRgb(0xA5,0xD6,0xA7)),
                ["ids"]            = (Color.FromRgb(0x20,0x30,0x20), Color.FromRgb(0xA5,0xD6,0xA7)),
                ["proje_yonetimi"] = (Color.FromRgb(0x20,0x20,0x3A), Color.FromRgb(0x90,0xCA,0xF9)),
                ["mep"]            = (Color.FromRgb(0x1A,0x2A,0x2A), Color.FromRgb(0x4D,0xD0,0xE1)),
                ["duvar"]          = (Color.FromRgb(0x2A,0x22,0x18), Color.FromRgb(0xD7,0xCC,0xA0)),
                ["parametreler"]   = (Color.FromRgb(0x2A,0x2A,0x2A), Color.FromRgb(0xB0,0xB0,0xC8)),
            };

            private static readonly Color _defaultBg = Color.FromRgb(0x2A,0x2A,0x3E);
            private static readonly Color _defaultFg = Color.FromRgb(0x90,0x90,0xA8);

            public static Color Bg(string category)
            {
                var key = (category ?? "").Trim().ToLowerInvariant().Replace(" ", "_");
                return _map.TryGetValue(key, out var v) ? v.Bg : _defaultBg;
            }

            public static Color Fg(string category)
            {
                var key = (category ?? "").Trim().ToLowerInvariant().Replace(" ", "_");
                return _map.TryGetValue(key, out var v) ? v.Fg : _defaultFg;
            }
        }

        // Adım VM — çalışma sırasında güncellenir
        private sealed class StepVm : System.ComponentModel.INotifyPropertyChanged
        {
            public string  Id   { get; init; } = "";
            public string  Op   { get; init; } = "";
            public string? From { get; init; }

            private string _statusIcon = "○";
            public string StatusIcon { get => _statusIcon; set { _statusIcon = value; Raise(nameof(StatusIcon)); } }

            private Brush _statusBrush = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8));
            public Brush StatusBrush { get => _statusBrush; set { _statusBrush = value; Raise(nameof(StatusBrush)); } }

            private Brush _rowBg = new SolidColorBrush(Color.FromRgb(0x31, 0x31, 0x50));
            public Brush RowBg { get => _rowBg; set { _rowBg = value; Raise(nameof(RowBg)); } }

            private string _rightText = "";
            public string RightText
            {
                get => _rightText.Length > 0 ? _rightText
                     : (string.IsNullOrEmpty(From) ? "" : $"← {From}");
                set { _rightText = value; Raise(nameof(RightText)); }
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            private void Raise(string n) => PropertyChanged?.Invoke(this, new(n));
        }

        // ── Ctor ──────────────────────────────────────────────────────────────

        public ManifestBrowserWindow(ExternalCommandData commandData)
        {
            InitializeComponent();
            _commandData = commandData;
            Loaded += (_, _) => LoadManifests();
        }

        // ── Yükleme ───────────────────────────────────────────────────────────

        private void LoadManifests()
        {
            try
            {
                var doc         = _commandData?.Application?.ActiveUIDocument?.Document;
                var projectRoot = EgbimotoApp.ProjectManifestRoot(doc);

                _allManifests = ManifestLoader.LoadAllSources(
                    builtInRoot: EgbimotoApp.ManifestRoot,
                    userRoot:    EgbimotoApp.UserManifestRoot,
                    projectRoot: projectRoot);

                // v11: Lint cache'i temizle — yeni manifests yüklenince eskisi geçersiz
                _lintCache.Clear();

                int u = _allManifests.Count(m => m.Source == ManifestSource.User);
                int p = _allManifests.Count(m => m.Source == ManifestSource.Project);
                int b = _allManifests.Count - u - p;
                txtManifestCount.Text =
                    $"{_allManifests.Count} manifest  ·  {b} built-in, {u} user, {p} project";

                BuildCategoryChips();
                ApplyFilter();
            }
            catch (Exception ex) { AppendLog($"Yükleme hatası: {ex.Message}"); }
        }

        // ── Kategori çipleri ──────────────────────────────────────────────────

        private void BuildCategoryChips()
        {
            pnlCategories.Children.Clear();

            var pool = _scopeV10Only
                ? _allManifests.Where(m => m.IsV10Catalog).ToList()
                : _allManifests;

            var cats = pool
                .GroupBy(m => string.IsNullOrWhiteSpace(m.Category) ? "genel" : m.Category)
                .OrderByDescending(g => g.Count())
                .Select(g => (Name: g.Key, Count: g.Count()))
                .ToList();

            AddChip($"Tümü ({pool.Count()})", null, isActive: true);
            foreach (var (name, count) in cats)
                AddChip($"{name} ({count})", name, isActive: false);
        }

        private void AddChip(string label, string? category, bool isActive)
        {
            var chip = new ToggleButton
            {
                Content   = label,
                Style     = (Style)FindResource("CategoryChip"),
                IsChecked = isActive,
                Tag       = category,
            };
            chip.Checked += (s, _) =>
            {
                foreach (var c in pnlCategories.Children.OfType<ToggleButton>())
                    if (!ReferenceEquals(c, s)) c.IsChecked = false;
                _activeCategory = category;
                ApplyFilter();
            };
            chip.Unchecked += (s, _) =>
            {
                if (!pnlCategories.Children.OfType<ToggleButton>().Any(c => c.IsChecked == true))
                    if (pnlCategories.Children.Count > 0 && pnlCategories.Children[0] is ToggleButton first)
                        first.IsChecked = true;
            };
            pnlCategories.Children.Add(chip);
        }

        // ── Filtre ────────────────────────────────────────────────────────────

        private void ApplyFilter()
        {
            var search = txtSearch.Text ?? "";

            IEnumerable<EgManifest> q = _allManifests;

            if (_scopeV10Only)
                q = q.Where(m => m.IsV10Catalog);

            if (!string.IsNullOrWhiteSpace(_activeCategory))
                q = q.Where(m => string.Equals(m.Category, _activeCategory, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(m =>
                    m.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    m.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    m.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    m.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)));

            var filtered = q.ToList();

            // v11: Lint cache — her manifest için bir kez çalışır
            var vms = filtered.Select(m =>
            {
                var cacheKey = m.FilePath;
                if (!_lintCache.TryGetValue(cacheKey, out var lint))
                {
                    lint = ManifestLinter.Lint(m, EgbimotoApp.Registry);
                    _lintCache[cacheKey] = lint;
                }
                return new ManifestVm { Manifest = m, Lint = lint };
            }).ToList();

            lstManifests.ItemsSource = vms;
            txtManifestCount.Text    = $"{filtered.Count} / {_allManifests.Count} manifest";
        }

        // ── Seçim ─────────────────────────────────────────────────────────────

        private void LstManifests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = lstManifests.SelectedItem as ManifestVm;
            _selected = vm?.Manifest;

            btnRemoveManifest.IsEnabled = _selected?.CanDelete == true;
            ResetRunUi();

            if (_selected is null)
            {
                pnlDetail.Visibility      = Visibility.Collapsed;
                txtNoSelection.Visibility = Visibility.Visible;
                btnRun.IsEnabled          = false;
                return;
            }

            pnlDetail.Visibility      = Visibility.Visible;
            txtNoSelection.Visibility = Visibility.Collapsed;
            btnRun.IsEnabled          = true;

            txtDetailTitle.Text = _selected.Title;
            txtDetailDesc.Text  = string.IsNullOrWhiteSpace(_selected.Description) ? "(Açıklama yok)" : _selected.Description;
            txtFilePath.Text    = _selected.FilePath;

            var lint = vm!.Lint;
            scoreBadge.Background = ColorFromHexBrush(lint.TagColor);
            txtScore.Text         = $"{lint.Score}/10";

            RenderTagChips(_selected.Tags);
            RenderPhaseIcons(lint);

            var issues = lint.Errors.Concat(lint.Warnings).ToList();
            if (issues.Count > 0)
            {
                lstLintIssues.ItemsSource   = issues;
                scrollLintIssues.Visibility = Visibility.Visible;
            }
            else scrollLintIssues.Visibility = Visibility.Collapsed;

            _liveSteps.Clear();
            foreach (var s in _selected.Steps)
                _liveSteps.Add(new StepVm { Id = s.Id, Op = s.Op, From = s.From });
            lstSteps.ItemsSource = _liveSteps;

            txtStatus.Text = lint.Summary;
        }

        // ── Tag çipleri ───────────────────────────────────────────────────────

        private void RenderTagChips(List<string> tags)
        {
            pnlTags.Children.Clear();
            if (tags == null || tags.Count == 0)
            {
                pnlTagsSection.Visibility = Visibility.Collapsed;
                return;
            }
            pnlTagsSection.Visibility = Visibility.Visible;

            foreach (var tag in tags)
            {
                var border = new Border
                {
                    Background      = new SolidColorBrush(Color.FromRgb(0x3A, 0x32, 0x52)),
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(0xBD, 0x93, 0xF9)),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(10),
                    Padding         = new Thickness(8, 2, 8, 2),
                    Margin          = new Thickness(0, 0, 5, 4),
                    Cursor          = System.Windows.Input.Cursors.Hand,
                };
                border.Child = new TextBlock
                {
                    Text       = "#" + tag,
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xBD, 0x93, 0xF9))
                };
                var captured = tag;
                border.MouseLeftButtonUp += (_, _) => { txtSearch.Text = captured; };
                pnlTags.Children.Add(border);
            }
        }

        private void RenderPhaseIcons(LintResult lint)
        {
            pnlPhases.Children.Clear();

            void AddPhase(string label, bool present)
            {
                var border = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding      = new Thickness(5, 2, 5, 2),
                    Margin       = new Thickness(0, 0, 4, 3),
                    Background   = present
                        ? new SolidColorBrush(Color.FromRgb(0x28, 0x6B, 0x3F))
                        : new SolidColorBrush(Color.FromRgb(0x5A, 0x28, 0x28)),
                };
                border.Child = new TextBlock
                {
                    Text       = (present ? "✓ " : "✗ ") + label,
                    FontSize   = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = present
                        ? new SolidColorBrush(Color.FromRgb(0x50, 0xFA, 0x7B))
                        : new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55))
                };
                pnlPhases.Children.Add(border);
            }

            AddPhase("PRECHECK",  lint.HasPrecheck);
            AddPhase("COLLECT",   lint.HasCollect);
            AddPhase("VALIDATE",  lint.HasValidate);
            AddPhase("AGGREGATE", lint.HasAggregate);
            AddPhase("SUMMARY",   lint.HasSummary);
            AddPhase("ROWS",      lint.HasRows);
            AddPhase("EXPORT",    lint.HasExport);

            var patternBorder = new Border
            {
                CornerRadius    = new CornerRadius(3),
                Padding         = new Thickness(5, 2, 5, 2),
                Margin          = new Thickness(8, 0, 0, 3),
                Background      = new SolidColorBrush(Color.FromRgb(0x28, 0x2A, 0x4A)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x6C, 0x63, 0xFF)),
                BorderThickness = new Thickness(1)
            };
            patternBorder.Child = new TextBlock
            {
                Text       = lint.Pattern,
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xBD, 0x93, 0xF9))
            };
            pnlPhases.Children.Add(patternBorder);
        }

        // ── Arama ─────────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter();

        // ── Çalıştır ──────────────────────────────────────────────────────────

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_selected is null) return;

            ResetRunUi();
            btnRun.IsEnabled = false;
            txtStatus.Text   = $"Çalışıyor: {_selected.Title}...";
            txtLog.Text      = "";

            var manifest    = _selected;
            var commandData = _commandData;
            var sw          = System.Diagnostics.Stopwatch.StartNew();
            int totalSteps  = manifest.Steps.Count;
            int doneSteps   = 0;

            void OnStep(string stepId, string op, long ms, bool success)
            {
                Dispatcher.Invoke(() =>
                {
                    var vm = _liveSteps.FirstOrDefault(s =>
                        string.Equals(s.Id, stepId, StringComparison.OrdinalIgnoreCase));
                    if (vm != null)
                    {
                        vm.StatusIcon  = success ? "✓" : "✗";
                        vm.StatusBrush = success
                            ? new SolidColorBrush(Color.FromRgb(0x50, 0xFA, 0x7B))
                            : new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
                        vm.RowBg = success
                            ? new SolidColorBrush(Color.FromRgb(0x22, 0x3A, 0x2A))
                            : new SolidColorBrush(Color.FromRgb(0x3A, 0x22, 0x22));
                        vm.RightText = $"{ms}ms";
                    }

                    doneSteps++;
                    var pct = totalSteps > 0 ? (double)doneSteps / totalSteps : 0;
                    AnimateProgress(pct);
                    txtProgress.Text = $"{doneSteps}/{totalSteps}";
                    AppendLog($"{(success ? "✓" : "✗")} {stepId} ({op}) {ms}ms");
                }, System.Windows.Threading.DispatcherPriority.Background);
            }

            ManifestRunResult result;
            try
            {
                result = await Dispatcher.InvokeAsync(() =>
                {
                    if (manifest.IsPreview)
                        return EgbimotoAppPreviewExtension.RunPreviewManifest(commandData, manifest);
                    else
                        return EgbimotoApp.RunManifest(commandData, manifest, OnStep);
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                sw.Stop();
                txtStatus.Text = $"❌ {ex.Message}";
                AppendLog($"İstisna ({sw.ElapsedMilliseconds}ms): {ex.Message}");
                btnRun.IsEnabled = true;
                return;
            }

            sw.Stop();

            var log = new StringBuilder(txtLog.Text);
            foreach (var line in result.Log)
                if (!txtLog.Text.Contains(line)) log.AppendLine(line);
            txtLog.Text = log.ToString();
            logScroll.ScrollToBottom();

            var cancelled = result.Success && result.ErrorMessage?.Contains("iptal") == true;
            AnimateProgress(cancelled ? 0 : 1.0);
            ShowTelemetry(result, sw.ElapsedMilliseconds);

            _lastReportPath = FindReportPath(result);
            btnOpenReport.Visibility = _lastReportPath != null ? Visibility.Visible : Visibility.Collapsed;

            txtStatus.Text = cancelled    ? $"⏹ İptal — {manifest.Title}"
                : result.Success ? $"✅ Tamamlandı — {manifest.Title}"
                : $"❌ Hata [{result.ErrorStep}]: {result.ErrorMessage}";
            txtStatus.Foreground = result.Success
                ? (cancelled ? (Brush)FindResource("AccentWarn") : (Brush)FindResource("AccentGreen"))
                : (Brush)FindResource("AccentRed");

            btnRun.IsEnabled = true;
        }

        // ── Telemetri ─────────────────────────────────────────────────────────

        private void ShowTelemetry(ManifestRunResult r, long wallMs)
        {
            var steps  = r.TotalSteps > 0 ? r.TotalSteps : _liveSteps.Count;
            var ms     = r.DurationMs > 0 ? r.DurationMs : wallMs;
            var parts  = new List<string> { $"{steps} adım", $"{ms}ms" };
            if (r.CachedSteps > 0)  parts.Add($"⚡{r.CachedSteps} önbellek");
            if (r.SkippedSteps > 0) parts.Add($"⏭{r.SkippedSteps} atlandı");
            txtTelemetry.Text         = string.Join("  ·  ", parts);
            telemetryBadge.Visibility = Visibility.Visible;
        }

        // ── Rapor tespiti ──────────────────────────────────────────────────────

        private static string? FindReportPath(ManifestRunResult r)
        {
            foreach (var v in r.Vars.Values.Reverse())
                if (v is string s && LooksLikeReport(s) && File.Exists(s))
                    return s;
            return null;
        }

        private static bool LooksLikeReport(string s)
        {
            var ext = Path.GetExtension(s).ToLowerInvariant();
            return ext is ".html" or ".xlsx" or ".pdf" or ".csv";
        }

        private void BtnOpenReport_Click(object sender, RoutedEventArgs e)
        {
            if (_lastReportPath == null || !File.Exists(_lastReportPath)) return;
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(_lastReportPath) { UseShellExecute = true });
            }
            catch (Exception ex) { AppendLog($"Rapor açılamadı: {ex.Message}"); }
        }

        // ── Progress ──────────────────────────────────────────────────────────

        private void AnimateProgress(double pct)
        {
            pct = Math.Max(0, Math.Min(1, pct));
            if (progressFill.Parent is FrameworkElement parent && parent.ActualWidth > 0)
                progressFill.Width = parent.ActualWidth * pct;
        }

        private void ResetRunUi()
        {
            progressFill.Width        = 0;
            txtProgress.Text          = "";
            telemetryBadge.Visibility = Visibility.Collapsed;
            btnOpenReport.Visibility  = Visibility.Collapsed;
            _lastReportPath           = null;
            foreach (var s in _liveSteps)
            {
                s.StatusIcon  = "○";
                s.StatusBrush = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8));
                s.RowBg       = new SolidColorBrush(Color.FromRgb(0x31, 0x31, 0x50));
                s.RightText   = "";
            }
        }

        // ── Manifest Üret ─────────────────────────────────────────────────────

        private void BtnPatternGenerate_Click(object sender, RoutedEventArgs e)
        {
            var genWindow = new ManifestGeneratorWindow(startInAiMode: false)
            { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            genWindow.ShowDialog();
            LoadManifests();
        }

        private void BtnAiGenerate_Click(object sender, RoutedEventArgs e)
        {
            var genWindow = new ManifestGeneratorWindow(startInAiMode: true)
            { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            genWindow.ShowDialog();
            LoadManifests();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Scope değişimi ────────────────────────────────────────────────────

        private void RbScope_Changed(object sender, RoutedEventArgs e)
        {
            if (rbScopeV10 is null || pnlCategories is null) return;
            _scopeV10Only   = rbScopeV10.IsChecked == true;
            _activeCategory = null;
            BuildCategoryChips();
            ApplyFilter();
        }

        // ── Manifest Ekle / Kaldır ────────────────────────────────────────────

        private void BtnAddManifest_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Manifest JSON dosyası seç",
                Filter = "Manifest JSON (*.json)|*.json|Tüm dosyalar (*.*)|*.*",
                Multiselect = true, CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) != true) return;

            int added = 0, skipped = 0;
            var errors = new List<string>();

            foreach (var path in dlg.FileNames)
            {
                try
                {
                    var candidate = ManifestLoader.Load(path, ManifestSource.User);
                    var lint      = ManifestLinter.Lint(candidate, EgbimotoApp.Registry);
                    if (!lint.IsValid || lint.Errors.Count > 0)
                    {
                        skipped++;
                        errors.Add($"✗ {System.IO.Path.GetFileName(path)} — {(lint.Errors.Count > 0 ? lint.Errors[0] : "Geçersiz manifest")}");
                        continue;
                    }
                    var dest = ManifestLoader.ImportManifestFile(path, EgbimotoApp.UserManifestRoot);
                    added++;
                    AppendLog($"✓ Eklendi: {System.IO.Path.GetFileName(dest)}  (skor {lint.Score}/10)");
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"✗ {System.IO.Path.GetFileName(path)} — {ex.Message}");
                }
            }

            if (errors.Count > 0)
                MessageBox.Show(this,
                    $"{added} eklendi, {skipped} atlandı.\n\nAtlananlar:\n" + string.Join("\n", errors),
                    "Manifest Ekleme", MessageBoxButton.OK,
                    added > 0 ? MessageBoxImage.Warning : MessageBoxImage.Error);
            else if (added > 0)
                txtStatus.Text = $"{added} manifest eklendi.";

            if (added > 0) LoadManifests();
        }

        private void BtnRemoveManifest_Click(object sender, RoutedEventArgs e)
        {
            if (_selected is null) return;
            if (!_selected.CanDelete)
            {
                MessageBox.Show(this, "Built-in manifest silinemez.",
                    "İzin yok", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var confirm = MessageBox.Show(this,
                $"Bu manifest kalıcı olarak silinecek:\n\n  {_selected.Title}\n  {_selected.FilePath}\n\nDevam?",
                "Manifest Kaldır", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            try
            {
                var name = _selected.Title;
                ManifestLoader.DeleteManifest(_selected);
                AppendLog($"🗑 Silindi: {name}");
                txtStatus.Text = $"Silindi: {name}";
                _selected = null;
                LoadManifests();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Silme hatası:\n{ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Yardımcı ──────────────────────────────────────────────────────────

        private void AppendLog(string line)
        {
            txtLog.Text += line + "\n";
            logScroll.ScrollToBottom();
        }

        private static SolidColorBrush ColorFromHexBrush(string hex)
        {
            hex = hex.TrimStart('#');
            return new SolidColorBrush(Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16)));
        }
    }
}
