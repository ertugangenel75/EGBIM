using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Dışa aktarma op'ları: Excel (xlsx), PDF (HTML print), IFC, doğrulama raporu, element raporu.
    /// xlsx: zipfile + OpenXML — sıfır bağımlılık.
    /// </summary>
    public static class ExportOps
    {
        // ── Excel (xlsx) ──────────────────────────────────────────────────────

        [EgOp("export_xlsx",
            Description = "Satır listesini Excel .xlsx dosyası olarak kaydeder. params: file_path (opsiyonel), sheet_name",
            Category    = "Çıktı")]
        public static string ExportXlsx(OpContext ctx)
        {
            var rows = SafeRows(ctx);
            // inputs.rows key'inden de almayı dene
            if (rows.Count == 0 &&
                ctx.Params.TryGetValue("rows", out var rv) &&
                rv is List<Dictionary<string, object?>> rvList)
                rows = rvList;
            if (rows == null || rows.Count == 0)
            {
                ctx.Log("  ⚠ export_xlsx: satır yok — atlandı");
                return "";
            }
            var filePath  = ctx.GetString("file_path", "");
            var sheetName = ctx.GetString("sheet_name", "EGBIMOTO");
            var title     = ctx.GetString("title", "EGBIMOTO");

            if (string.IsNullOrEmpty(filePath))
                filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"EGBIMOTO_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");

            if (rows.Count == 0) { ctx.Log("  ⚠ xlsx: satır yok"); return filePath; }

            WriteXlsx(filePath, sheetName, rows);
            ctx.Log($"  xlsx kaydedildi: {filePath}");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = filePath, UseShellExecute = true });
            return filePath;
        }

        [EgOp("export_pdf",
            Description = "Satır listesini baskı-uyumlu HTML olarak kaydeder ve tarayıcıda açar (Ctrl+P ile PDF kaydet). FIX#10: Doğrudan PDF üretmez, print-to-PDF workflow'u. params: title",
            Category    = "Çıktı")]
        public static string ExportPdf(OpContext ctx)
        {
            var rows  = SafeRows(ctx);
            var title = ctx.GetString("title", "EGBIMOTO Rapor");
            var path  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"EGBIMOTO_{DateTime.Now:yyyyMMdd_HHmm}_print.html");

            if (rows.Count == 0) { ctx.Log("  ⚠ pdf: satır yok"); return path; }

            // PDF için baskı-uyumlu HTML (print CSS + auto-print)
            var headers = rows[0].Keys.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine($"<title>{title}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("@media print{body{margin:0}}");
            sb.AppendLine("body{font-family:Arial,sans-serif;font-size:10pt;margin:15mm}");
            sb.AppendLine("h2{font-size:14pt;margin-bottom:4px}");
            sb.AppendLine("p{font-size:9pt;color:#555;margin:0 0 8px}");
            sb.AppendLine("table{border-collapse:collapse;width:100%;page-break-inside:auto}");
            sb.AppendLine("tr{page-break-inside:avoid}");
            sb.AppendLine("th{background:#1a1a2e;color:#fff;padding:5px 8px;font-size:9pt;text-align:left}");
            sb.AppendLine("td{border:1px solid #ccc;padding:4px 7px;font-size:9pt}");
            sb.AppendLine("tr:nth-child(even){background:#f5f5f5}");
            sb.AppendLine(".footer{margin-top:10px;font-size:8pt;color:#888}");
            sb.AppendLine("</style>");
            sb.AppendLine($"<script>window.onload=function(){{window.print();}}</script>");
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h2>{title}</h2>");
            sb.AppendLine($"<p>{rows.Count} kayıt — {DateTime.Now:dd.MM.yyyy HH:mm} — EGBIMOTO</p>");
            sb.AppendLine("<table><tr>");
            foreach (var h in headers) sb.Append($"<th>{h}</th>");
            sb.AppendLine("</tr>");
            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var h in headers)
                    sb.Append($"<td>{(row.TryGetValue(h, out var v) ? v?.ToString() ?? "" : "")}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            sb.AppendLine($"<p class='footer'>EGBIMOTO — {DateTime.Now:yyyy}</p>");
            sb.AppendLine("</body></html>");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = path, UseShellExecute = true });
            ctx.Log($"  PDF (print): {path}");
            return path;
        }

        // ── Doğrulama raporu ──────────────────────────────────────────────────

        [EgOp("export_validation_report",
            Description = "ValidationReport'u HTML olarak dışa aktarır. params: title, file_path",
            Category    = "Çıktı")]
        public static string ExportValidationReport(OpContext ctx)
        {
            var title = ctx.GetString("title", "EGBIMOTO Doğrulama Raporu");
            var path  = ctx.GetString("file_path", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"EGBIMOTO_VAL_{DateTime.Now:yyyyMMdd_HHmm}.html"));

            // ctx.Input (from) veya inputs.rows / inputs.input key'inden al
            object? rawInput = ctx.Input;
            if (rawInput == null && ctx.Params.TryGetValue("rows", out var rv)) rawInput = rv;
            if (rawInput == null && ctx.Params.TryGetValue("input", out var iv)) rawInput = iv;

            ValidationReport? report = null;
            if (rawInput is ValidationReport vr) report = vr;
            else if (rawInput is List<Dictionary<string, object?>> rows)
            {
                // Satır listesinden sahte report oluştur
                report = RowsToReport(title, rows);
            }

            if (report is null) { ctx.Log("  ⚠ Validation report bekleniyordu"); return path; }

            WriteValidationHtml(path, title, report);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = path, UseShellExecute = true });
            ctx.Log($"  Doğrulama raporu: {path}");
            return path;
        }

        [EgOp("element_report",
            Description = "Eleman veya satır listesini HTML rapor olarak dışa aktarır (export_html_report alias)",
            Category    = "Çıktı")]
        public static string ElementReport(OpContext ctx)
        {
            // export_html_report ile aynı mantık — V1 uyumluluk
            var rows  = SafeRows(ctx);
            var title = ctx.GetString("title", "EGBIMOTO Eleman Raporu");
            var path  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"EGBIMOTO_{DateTime.Now:yyyyMMdd_HHmm}.html");

            if (rows.Count == 0) { ctx.Log("  ⚠ element_report: satır yok"); return path; }

            var headers = rows[0].Keys.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine($"<title>{title}</title>");
            sb.AppendLine("<style>body{{font-family:sans-serif;margin:20px}}table{{border-collapse:collapse;width:100%}}");
            sb.AppendLine("th{{background:#2a2a3e;color:#fff;padding:8px;text-align:left}}td{{border:1px solid #ddd;padding:6px}}");
            sb.AppendLine("tr:nth-child(even){{background:#f9f9f9}}</style></head><body>");
            sb.AppendLine($"<h2>{title}</h2><p>{rows.Count} kayıt — {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            sb.Append("<table><tr>");
            foreach (var h in headers) sb.Append($"<th>{h}</th>");
            sb.AppendLine("</tr>");
            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var h in headers)
                    sb.Append($"<td>{(row.TryGetValue(h, out var v) ? v?.ToString() ?? "" : "")}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table></body></html>");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = path, UseShellExecute = true });
            ctx.Log($"  element_report: {path}");
            return path;
        }

        // ── IFC Dışa Aktarma ──────────────────────────────────────────────────

        [EgOp("ifc_export",
            Description = "Revit modelini IFC 2x3 / IFC 4 olarak dışa aktarır. params: output_dir, file_name, ifc_version (IFC2x3|IFC4), export_linked_files",
            Category    = "IFC")]
        public static string IfcExport(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var outputDir  = ctx.GetString("output_dir",
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            var fileName   = ctx.GetString("file_name",
                $"{Path.GetFileNameWithoutExtension(rctx.Doc.Title)}_{DateTime.Now:yyyyMMdd}");
            var ifcVersion = ctx.GetString("ifc_version", "IFC2x3");
            var linked     = ctx.GetBool("export_linked_files", false);

            Directory.CreateDirectory(outputDir);

            var settings = new IFCExportOptions();

            // IFC versiyonu ayarı
            // FIX #12: IFCVersion.IFC4x3 Revit versiyonuna göre olmayabilir.
            // Güvenli: try/catch ile IFC4 fallback.
            settings.FileVersion = ifcVersion.ToUpperInvariant() switch
            {
                "IFC4"   => IFCVersion.IFC4,
                "IFC4X3" => TryGetIfc4x3(),
                _        => IFCVersion.IFC2x3
            };

            // Revit 2025+: ExportLinkedFiles property kaldırıldı — AddOption ile
            if (linked) settings.AddOption("ExportLinkedFiles", "true");

            // WallAndColumnSplitting — TR BIM standardı için kapalı
            settings.AddOption("ExportInternalRevitPropertySets", "true");
            settings.AddOption("ExportIFCCommonPropertySets",     "true");
            settings.AddOption("Export2DElements",                 "false");

            ctx.Log($"  IFC dışa aktarılıyor: {fileName}.ifc ({ifcVersion})...");

            rctx.Doc.Export(outputDir, fileName, settings);

            var outPath = Path.Combine(outputDir, fileName + ".ifc");
            ctx.Log($"  IFC tamamlandı: {outPath}");

            Autodesk.Revit.UI.TaskDialog.Show("IFC Dışa Aktarma",
                $"Başarılı:\n{outPath}\nFormat: {ifcVersion}");

            return outPath;
        }

        [EgOp("write_trace",
            Description = "Log mesajını dosyaya yazar. params: message, file_path (opsiyonel). Debug için.",
            Category    = "Yardımcı")]
        public static string WriteTrace(OpContext ctx)
        {
            var message  = ctx.GetString("message", ctx.Input?.ToString() ?? "trace");
            var filePath = ctx.GetString("file_path", "");

            if (string.IsNullOrEmpty(filePath))
                filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "EGBIMOTO_trace.log");

            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            ctx.Log($"  trace: {line}");
            return line;
        }

        /// <summary>
        /// FIX: ctx.InputAsOrDefault sert tip cast ile JSON round-trip sonrası
        /// null dönebiliyordu. Bu yardımcı JsonElement / IDictionary tiplerini de kabul eder.
        /// </summary>
        private static List<Dictionary<string, object?>> SafeRows(OpContext ctx)
        {
            var rows = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            if (rows != null) return rows;

            var result = new List<Dictionary<string, object?>>();
            if (ctx.Input is not System.Collections.IEnumerable en || ctx.Input is string)
                return result;

            foreach (var item in en)
            {
                if (item == null) continue;
                if (item is Dictionary<string, object?> d1) { result.Add(d1); continue; }
                if (item is IDictionary<string, object> d2)
                {
                    result.Add(d2.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
                    continue;
                }
                if (item is System.Text.Json.JsonElement je &&
                    je.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in je.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.ValueKind switch
                        {
                            System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                            System.Text.Json.JsonValueKind.Number => prop.Value.GetDouble(),
                            System.Text.Json.JsonValueKind.True   => true,
                            System.Text.Json.JsonValueKind.False  => false,
                            _ => prop.Value.ToString()
                        };
                    }
                    result.Add(dict);
                    continue;
                }
                var refDict = new Dictionary<string, object?>();
                foreach (var prop in item.GetType().GetProperties())
                {
                    try { refDict[prop.Name] = prop.GetValue(item); } catch { }
                }
                if (refDict.Count > 0) result.Add(refDict);
            }
            return result;
        }

        // ── xlsx yazıcı (zipfile + OpenXML — sıfır bağımlılık) ───────────────

        private static void WriteXlsx(string path, string sheetName,
            List<Dictionary<string, object?>> rows)
        {
            var headers = rows[0].Keys.ToList();

            // shared strings
            var strings = new List<string>();
            var strIdx  = new Dictionary<string, int>(StringComparer.Ordinal);
            int AddStr(string s)
            {
                if (strIdx.TryGetValue(s, out var idx)) return idx;
                idx = strings.Count; strings.Add(s); strIdx[s] = idx;
                return idx;
            }

            // worksheet XML
            var wsSb = new StringBuilder();
            wsSb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            wsSb.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            wsSb.AppendLine("<sheetData>");

            // Header row
            wsSb.Append("<row r=\"1\">");
            for (int c = 0; c < headers.Count; c++)
            {
                var addr = ColLetter(c) + "1";
                var si   = AddStr(headers[c]);
                wsSb.Append($"<c r=\"{addr}\" t=\"s\"><v>{si}</v></c>");
            }
            wsSb.AppendLine("</row>");

            // Data rows
            for (int ri = 0; ri < rows.Count; ri++)
            {
                int rowNum = ri + 2;
                wsSb.Append($"<row r=\"{rowNum}\">");
                for (int c = 0; c < headers.Count; c++)
                {
                    var addr = ColLetter(c) + rowNum;
                    var val  = rows[ri].TryGetValue(headers[c], out var v) ? v?.ToString() ?? "" : "";
                    if (double.TryParse(val, out var num))
                        wsSb.Append($"<c r=\"{addr}\"><v>{num}</v></c>");
                    else
                    {
                        var si = AddStr(val);
                        wsSb.Append($"<c r=\"{addr}\" t=\"s\"><v>{si}</v></c>");
                    }
                }
                wsSb.AppendLine("</row>");
            }

            wsSb.AppendLine("</sheetData></worksheet>");

            // shared strings XML
            var ssSb = new StringBuilder();
            ssSb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            ssSb.AppendLine($"<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"{strings.Count}\" uniqueCount=\"{strings.Count}\">");
            foreach (var s in strings)
                ssSb.AppendLine($"<si><t xml:space=\"preserve\">{XmlEscape(s)}</t></si>");
            ssSb.AppendLine("</sst>");

            // Write zip
            if (File.Exists(path)) File.Delete(path);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

            void AddEntry(string name, string content)
            {
                var e = zip.CreateEntry(name, CompressionLevel.Fastest);
                using var w = new StreamWriter(e.Open(), Encoding.UTF8);
                w.Write(content);
            }

            AddEntry("[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>" +
                "</Types>");

            AddEntry("_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>");

            AddEntry("xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                $"<sheets><sheet name=\"{XmlEscape(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");

            AddEntry("xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>" +
                "</Relationships>");

            AddEntry("xl/worksheets/sheet1.xml", wsSb.ToString());
            AddEntry("xl/sharedStrings.xml",     ssSb.ToString());
        }

        private static string ColLetter(int col)
        {
            var sb = new StringBuilder();
            col++;
            while (col > 0)
            {
                sb.Insert(0, (char)('A' + (col - 1) % 26));
                col = (col - 1) / 26;
            }
            return sb.ToString();
        }

        private static string XmlEscape(string s)
            => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
               .Replace("\"", "&quot;").Replace("'", "&apos;");

        // ── Validation HTML ───────────────────────────────────────────────────

        private static void WriteValidationHtml(string path, string title, ValidationReport report)
        {
            var failed   = report.Results.Where(r => !r.Passed && r.Severity == "ERROR").ToList();
            var warnings = report.Results.Where(r => !r.Passed && r.Severity == "WARNING").ToList();
            var passed   = report.Results.Where(r => r.Passed).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine($"<title>{title}</title>");
            sb.AppendLine("<style>body{{font-family:sans-serif;margin:20px}}");
            sb.AppendLine(".summary{{display:flex;gap:16px;margin:16px 0}}");
            sb.AppendLine(".stat{{padding:12px 20px;border-radius:8px;text-align:center}}");
            sb.AppendLine(".pass{{background:#e8f5e9;color:#1b5e20}}");
            sb.AppendLine(".fail{{background:#ffebee;color:#b71c1c}}");
            sb.AppendLine(".warn{{background:#fff8e1;color:#e65100}}");
            sb.AppendLine(".total{{background:#e3f2fd;color:#0d47a1}}");
            sb.AppendLine("table{{border-collapse:collapse;width:100%;margin-top:12px}}");
            sb.AppendLine("th{{background:#2a2a3e;color:#fff;padding:8px;text-align:left}}");
            sb.AppendLine("td{{border:1px solid #ddd;padding:6px}}");
            sb.AppendLine(".err{{color:#c62828}}.wrn{{color:#e65100}}.ok{{color:#2e7d32}}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h2>{title}</h2>");
            sb.AppendLine($"<p>{DateTime.Now:dd.MM.yyyy HH:mm} — EGBIMOTO</p>");
            sb.AppendLine("<div class='summary'>");
            sb.AppendLine($"<div class='stat total'><div style='font-size:24px'>{report.TotalChecks}</div>Toplam</div>");
            sb.AppendLine($"<div class='stat pass'><div style='font-size:24px'>{report.Passed}</div>Geçti</div>");
            sb.AppendLine($"<div class='stat fail'><div style='font-size:24px'>{report.Failed}</div>Hata</div>");
            sb.AppendLine($"<div class='stat warn'><div style='font-size:24px'>{report.Warnings}</div>Uyarı</div>");
            sb.AppendLine("</div>");

            void Section(string heading, List<ValidationResult> items, string cls)
            {
                if (!items.Any()) return;
                sb.AppendLine($"<h3>{heading} ({items.Count})</h3>");
                sb.AppendLine("<table><tr><th>Kural</th><th>Element ID</th><th>Kategori</th><th>Kontrol</th><th>Mesaj</th></tr>");
                foreach (var r in items)
                    sb.AppendLine($"<tr><td>{r.RuleId}</td><td>{r.ElementId}</td><td>{r.Category}</td>" +
                        $"<td>{r.CheckType}</td><td class='{cls}'>{r.Message}</td></tr>");
                sb.AppendLine("</table>");
            }

            Section("❌ Hatalar",  failed,   "err");
            Section("⚠ Uyarılar", warnings, "wrn");
            Section("✅ Geçenler", passed,   "ok");
            sb.AppendLine("</body></html>");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static ValidationReport RowsToReport(string title,
            List<Dictionary<string, object?>> rows)
        {
            var results = rows.Select(r => new ValidationResult
            {
                RuleId    = r.TryGetValue("rule_id",    out var ri) ? ri?.ToString() ?? "" : "",
                ElementId = r.TryGetValue("element_id", out var ei) ? ei?.ToString() ?? "" : "",
                Category  = r.TryGetValue("kategori",   out var ca) ? ca?.ToString() ?? "" : "",
                CheckType = r.TryGetValue("check_type", out var ct) ? ct?.ToString() ?? "" : "",
                Passed    = r.TryGetValue("passed",     out var p)  && p?.ToString() == "True",
                Severity  = r.TryGetValue("severity",   out var sv) ? sv?.ToString() ?? "ERROR" : "ERROR",
                Message   = r.TryGetValue("message",    out var m)  ? m?.ToString() ?? "" : ""
            }).ToList();

            return new ValidationReport
            {
                ManifestTitle = title,
                TotalChecks   = results.Count,
                Passed        = results.Count(x => x.Passed),
                Failed        = results.Count(x => !x.Passed && x.Severity == "ERROR"),
                Warnings      = results.Count(x => !x.Passed && x.Severity == "WARNING"),
                Results       = results
            };
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────

        /// <summary>
        /// FIX #12: IFCVersion.IFC4x3 Revit 2024 öncesinde enum'da olmayabilir.
        /// Reflection ile kontrol edip yoksa IFC4'e düşer.
        /// </summary>
        private static IFCVersion TryGetIfc4x3()
        {
            try
            {
                // IFC4x3 Revit 2024+ API'sinde mevcut
                if (Enum.TryParse<IFCVersion>("IFC4x3", out var v4x3)) return v4x3;
                if (Enum.TryParse<IFCVersion>("IFC4X3", out var v4X3)) return v4X3;
            }
            catch { }
            // Fallback: IFC4
            return IFCVersion.IFC4;
        }
    }
}
