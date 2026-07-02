using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Addin.UI.Results;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Results;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Genel amaçlı utility op'ları (Revit API bağımsız).
    /// echo, compute, where, select_field, show_table, export_csv, export_html_report
    /// </summary>
    public static class CoreOps
    {
        [EgOp("echo",      Description = "params.value'yu döner (test/sabit değer için)", Category = "Yardımcı")]
        public static object? Echo(OpContext ctx) => ctx.GetString("value", "");

        [EgOp("noop",      Description = "Hiçbir şey yapmaz, input'u geçirir",           Category = "Yardımcı")]
        public static object? Noop(OpContext ctx) => ctx.Input;

        [EgOp("eq",
            Description = "İki değeri karşılaştırır. params: left, right. Çıktı: bool. Condition yerine manifest adımı olarak da kullanılabilir.",
            Category    = "Yardımcı")]
        public static bool Eq(OpContext ctx)
        {
            object? left  = ctx.Params.TryGetValue("left",  out var l) ? l : ctx.Input;
            object? right = ctx.Params.TryGetValue("right", out var r) ? r : null;

            var ls = left?.ToString()  ?? "";
            var rs = right?.ToString() ?? "";

            if (double.TryParse(ls, out var ld) && double.TryParse(rs, out var rd))
                return Math.Abs(ld - rd) < 1e-9;

            return string.Equals(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        [EgOp("show_count",
            Description = "Input koleksiyonunun eleman sayısını log'a yazar",
            Category    = "Yardımcı")]
        public static int ShowCount(OpContext ctx)
        {
            var count = (ctx.Input as System.Collections.ICollection)?.Count ?? 0;
            ctx.Log($"  Sayı: {count}");
            return count;
        }

        [EgOp("show_result",
            Description = "Input'u TaskDialog ile gösterir",
            Category    = "Yardımcı")]
        public static object? ShowResult(OpContext ctx)
        {
            var title = ctx.GetString("title", "EGBIMOTO");
            TaskDialog.Show(title, ctx.Input?.ToString() ?? "(boş)");
            return ctx.Input;
        }

        [EgOp("where",
            Description = "Satır listesini filtreler. params: field, op (eq|neq|gt|lt|contains), value",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> Where(OpContext ctx)
        {
            var rows  = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            if ((rows == null || rows.Count == 0) && ctx.Params.TryGetValue("rows", out var _rv_rows) && _rv_rows is List<Dictionary<string, object?>> _rvl_rows) rows = _rvl_rows;
            var field = ctx.RequireString("field");
            var op    = ctx.GetString("op", "eq").ToLowerInvariant();
            var value = ctx.RequireString("value");
            var result = (rows ?? new()).Where(r =>
            {
                var cell = r.TryGetValue(field, out var v) ? v?.ToString() ?? "" : "";
                return op switch
                {
                    "eq"       => cell == value,
                    "neq"      => cell != value,
                    "contains" => cell.Contains(value, StringComparison.OrdinalIgnoreCase),
                    "gt"       => double.TryParse(cell, out var d1) && double.TryParse(value, out var d2) && d1 > d2,
                    "lt"       => double.TryParse(cell, out var d3) && double.TryParse(value, out var d4) && d3 < d4,
                    _          => true
                };
            }).ToList();
            ctx.Log($"  where({field} {op} {value}): {rows?.Count ?? 0} -> {result.Count}");
            return result;
        }

        [EgOp("select_field",
            Description = "Satır listesinden params.fields (virgülle ayrılmış) alanlarını seçer",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> SelectField(OpContext ctx)
        {
            var rows   = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            if ((rows == null || rows.Count == 0) && ctx.Params.TryGetValue("rows", out var _rv_rows) && _rv_rows is List<Dictionary<string, object?>> _rvl_rows) rows = _rvl_rows;
            var fields = ctx.GetString("fields").Split(',').Select(f => f.Trim()).ToList();
            return (rows ?? new()).Select(r => fields.ToDictionary(f => f, f => r.TryGetValue(f, out var v) ? v : null)).ToList();
        }

        [EgOp("show_table",
            Description = "Satır listesini WPF sonuç penceresinde gösterir (sıralanabilir tablo, " +
                          "'Modelde Göster', CSV dışa aktarım). Dict/scalar input için TaskDialog'a düşer. " +
                          "params: title, max_rows",
            Category    = "Çıktı")]
        public static object? ShowTable(OpContext ctx)
        {
            var title   = ctx.GetString("title", "EGBIMOTO Sonuç");
            var maxRows = ctx.GetInt("max_rows", 500);   // v13.5: DataGrid sanal kaydırma yapıyor — TaskDialog'un 20 satır sınırına gerek yok

            // v13.5: tablo verisi → gerçek DataGrid penceresi (eskiden TaskDialog metin dökümü)
            if (ctx.Input is List<Dictionary<string, object?>> rows)
            {
                var capped = rows.Count > maxRows ? rows.Take(maxRows).ToList() : rows;
                var dto = ManifestResultAdapter.FromRows(title, capped);
                if (rows.Count > maxRows)
                    dto.Warnings.Add($"{rows.Count - maxRows} satır daha var — max_rows ile sınırlandı.");

                var uidoc = (ctx as RevitOpContext)?.UiDoc;
                ManifestResultRendererRegistry.Show(uidoc, dto);
                return ctx.Input;
            }

            // Dict/scalar input — grid'e uygun değil, eski metin özeti korunur.
            var sb = new StringBuilder();
            if (ctx.Input is Dictionary<string, object?> dict)
                sb.Append(string.Join("\n", dict.Select(kv => $"{kv.Key}: {kv.Value}")));
            else
                sb.Append(ctx.Input?.ToString() ?? "(boş)");
            TaskDialog.Show(title, sb.ToString());
            return ctx.Input;
        }

        [EgOp("export_csv",
            Description = "Satır listesini CSV olarak kaydeder. params: file_path (opsiyonel)",
            Category    = "Çıktı")]
        public static string ExportCsv(OpContext ctx)
        {
            var rows     = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            if ((rows == null || rows.Count == 0) && ctx.Params.TryGetValue("rows", out var _rv_rows) && _rv_rows is List<Dictionary<string, object?>> _rvl_rows) rows = _rvl_rows;
            var filePath = ctx.GetString("file_path", "");
            if (string.IsNullOrEmpty(filePath))
                filePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"EGBIMOTO_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            if ((rows?.Count ?? 0) == 0) { ctx.Log("  Dışa aktarılacak satır yok"); return filePath; }
            var headers = rows![0].Keys.ToList();
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", headers));
            foreach (var row in rows)
                sb.AppendLine(string.Join(";", headers.Select(h =>
                    row.TryGetValue(h, out var v) ? v?.ToString() ?? "" : "")));
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            ctx.Log($"  CSV: {filePath}");
            TaskDialog.Show("CSV Dışa Aktarma", $"Kaydedildi:\n{filePath}");
            return filePath;
        }

        [EgOp("export_html_report",
            Description = "Satır listesini HTML rapor olarak kaydeder ve tarayıcıda açar. params: title",
            Category    = "Çıktı")]
        public static string ExportHtmlReport(OpContext ctx)
        {
            var rows  = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            if ((rows == null || rows.Count == 0) && ctx.Params.TryGetValue("rows", out var _rv_rows) && _rv_rows is List<Dictionary<string, object?>> _rvl_rows) rows = _rvl_rows;
            var title = ctx.GetString("title", "EGBIMOTO Rapor");
            var path  = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"EGBIMOTO_{DateTime.Now:yyyyMMdd_HHmm}.html");
            if ((rows?.Count ?? 0) == 0) { ctx.Log("  Rapor için satır yok"); return path; }
            var headers = rows![0].Keys.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>" + title + "</title>");
            sb.AppendLine("<style>body{font-family:sans-serif;margin:20px}");
            sb.AppendLine("table{border-collapse:collapse;width:100%}");
            sb.AppendLine("th{background:#2a2a3e;color:#fff;padding:8px;text-align:left}");
            sb.AppendLine("td{border:1px solid #ddd;padding:6px}");
            sb.AppendLine("tr:nth-child(even){background:#f9f9f9}</style></head><body>");
            sb.AppendLine("<h2>" + title + "</h2><p>" + rows.Count + " kayıt — " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</p>");
            sb.Append("<table><tr>");
            foreach (var h in headers) sb.Append("<th>" + h + "</th>");
            sb.Append("</tr>");
            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var h in headers)
                    sb.Append("<td>" + (row.TryGetValue(h, out var v) ? v?.ToString() ?? "" : "") + "</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></body></html>");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = path, UseShellExecute = true });
            ctx.Log($"  HTML rapor: {path}");
            return path;
        }
    }
}
