using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Results;
using Color = System.Windows.Media.Color;   // CS0104: Autodesk.Revit.DB.Color çakışması

namespace EGBIMOTO.Addin.UI.Results
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — ManifestResultWindow  (v13.5)
    //
    //  ManifestResultDto için genel amaçlı sonuç penceresi. TaskDialog metin
    //  dökümünün yerini alır: sıralanabilir/filtrelenmiş DataGrid, özet
    //  çubuğu, seçili satırları modelde vurgulama, CSV dışa aktarım.
    //
    //  Kullanım:
    //    ManifestResultWindow.Show(uidoc, dto);
    //  veya (renderer registry üzerinden, ileride Kind bazlı özelleştirme için):
    //    ManifestResultRendererRegistry.Show(uidoc, dto);
    // ═══════════════════════════════════════════════════════════════════════════
    public partial class ManifestResultWindow : Window
    {
        private readonly ManifestResultDto _dto;
        private readonly UIDocument? _uidoc;
        private DataTable _table = new();

        public static void Show(UIDocument? uidoc, ManifestResultDto dto)
        {
            var win = new ManifestResultWindow(dto, uidoc);
            win.ShowDialog();
        }

        private ManifestResultWindow(ManifestResultDto dto, UIDocument? uidoc)
        {
            InitializeComponent();
            _dto   = dto;
            _uidoc = uidoc;

            TitleText.Text = dto.Title;
            BuildSummary();
            BuildTable();

            ShowInModelBtn.IsEnabled = _uidoc != null && dto.ElementIds.Count > 0;
            StatusText.Text = dto.Rows.Count == 0
                ? "Sonuç satırı yok."
                : $"{dto.Rows.Count} satır" + (dto.ElementIds.Count > 0 ? $" — {dto.ElementIds.Count} elemana bağlı" : "");

            if (dto.Warnings.Count > 0)
                StatusText.Text += $"  ⚠ {dto.Warnings.Count} uyarı";
        }

        private void BuildSummary()
        {
            SummaryPanel.ItemsSource = _dto.Summary
                .Select(kv => new { Key = kv.Key, Value = kv.Value })
                .ToList();
        }

        private void BuildTable()
        {
            _table = new DataTable();

            var columns = _dto.Columns.Count > 0
                ? _dto.Columns
                : (_dto.Rows.Count > 0 ? _dto.Rows[0].Keys.ToList() : new System.Collections.Generic.List<string>());

            foreach (var col in columns)
                _table.Columns.Add(col);

            foreach (var row in _dto.Rows)
            {
                var dr = _table.NewRow();
                foreach (var col in columns)
                    dr[col] = row.TryGetValue(col, out var v) ? (v?.ToString() ?? "") : "";
                _table.Rows.Add(dr);
            }

            ResultGrid.ItemsSource = _table.DefaultView;

            // v13.5: Doğrulama sonuçlarında severity'ye göre satır rengi.
            if (_dto.Kind == ManifestResultKind.Validation && columns.Contains("severity"))
                ResultGrid.LoadingRow += ResultGrid_LoadingRowSeverityColor;
        }

        private void ResultGrid_LoadingRowSeverityColor(object? sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is not DataRowView drv) return;
            var severity = drv.Row.Table.Columns.Contains("severity") ? drv["severity"]?.ToString() : null;
            e.Row.Background = severity?.ToUpperInvariant() switch
            {
                "ERROR"   => new SolidColorBrush(Color.FromArgb(40, 255, 85, 85)),
                "WARNING" => new SolidColorBrush(Color.FromArgb(40, 255, 184, 108)),
                _         => Brushes.Transparent,
            };
        }

        private void ResultGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowInModelBtn.IsEnabled = _uidoc != null &&
                (ResultGrid.SelectedItems.Count > 0 || _dto.ElementIds.Count > 0);
        }

        // ── Modelde Göster ────────────────────────────────────────────────────
        private void ShowInModel_Click(object sender, RoutedEventArgs e)
        {
            if (_uidoc == null) return;

            var ids = SelectedElementIdsOrAll();
            if (ids.Count == 0)
            {
                TaskDialog.Show("EGBIMOTO", "Vurgulanacak eleman bulunamadı.");
                return;
            }

            var eids = ids.Select(Rv.MakeElementId).ToList();
            try
            {
                _uidoc.Selection.SetElementIds(eids);
                _uidoc.ShowElements(eids);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("EGBIMOTO", $"Modelde gösterilemedi: {ex.Message}\n" +
                                            "(Elemanlar silinmiş veya farklı bir görünümde olabilir.)");
            }
        }

        /// <summary>Satır seçiliyse seçili satırların element_id'leri, değilse tüm dto.ElementIds.</summary>
        private System.Collections.Generic.List<long> SelectedElementIdsOrAll()
        {
            if (ResultGrid.SelectedItems.Count > 0 && _table.Columns.Contains("element_id"))
            {
                var result = new System.Collections.Generic.List<long>();
                foreach (var item in ResultGrid.SelectedItems)
                    if (item is DataRowView drv &&
                        long.TryParse(drv["element_id"]?.ToString(), out var idv))
                        result.Add(idv);
                if (result.Count > 0) return result;
            }
            return _dto.ElementIds;
        }

        // ── CSV dışa aktarım ─────────────────────────────────────────────────
        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_table.Rows.Count == 0)
            {
                TaskDialog.Show("EGBIMOTO", "Dışa aktarılacak satır yok.");
                return;
            }

            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"EGBIMOTO_{SanitizeFileName(_dto.Title)}_{DateTime.Now:yyyyMMdd_HHmm}.csv");

            var sb = new StringBuilder();
            var colNames = _table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            sb.AppendLine(string.Join(";", colNames));
            foreach (DataRow row in _table.Rows)
                sb.AppendLine(string.Join(";", colNames.Select(c => row[c]?.ToString() ?? "")));

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            StatusText.Text = $"CSV kaydedildi: {filePath}";
        }

        private static string SanitizeFileName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
