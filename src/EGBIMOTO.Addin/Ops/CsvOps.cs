using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO.Compression;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — CSV / Excel Veri Op'ları (CsvOps)
    ///
    ///   csv_read        — CSV dosyasını List<Dict> olarak oku
    ///   csv_write       — List<Dict> → CSV dosyasına yaz
    ///   excel_xml_read  — .xlsx dosyasını zipfile+XML ile oku (openpyxl yok)
    ///   table_to_points — Dict listesini XYZ nokta listesine dönüştür
    ///   table_validate_schema — Beklenen alan listesini kontrol et
    /// </summary>
    public static class CsvOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // D01  csv_read
        //
        // input : —
        // params: file_path    String  zorunlu
        //         delimiter    String  default=","
        //         has_header   Bool    default=true
        //         encoding     String  default="utf-8"
        //         skip_rows    Int     default=0
        //
        // output: List<Dict>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("csv_read",
            Description = "CSV dosyasını okur ve List<Dict> döner. " +
                          "has_header=true → ilk satır alan adı olur.",
            Category    = "CSV")]
        public static List<Dictionary<string, object?>> CsvRead(OpContext ctx)
        {
            var filePath  = ctx.RequireString("file_path");
            var delimiter = ctx.GetString("delimiter",  ",");
            bool hasHeader= ctx.GetBool("has_header",  true);
            var encName   = ctx.GetString("encoding",  "utf-8");
            int skipRows  = ctx.GetInt("skip_rows",    0);

            if (!File.Exists(filePath))
            {
                ctx.Log($"  csv_read: '{filePath}' bulunamadı → []");
                return new List<Dictionary<string, object?>>
                {
                    new() { ["status"] = "PARAM_MISSING",
                            ["message"] = $"Dosya bulunamadı: {filePath}" }
                };
            }

            var enc = SafeGetEncoding(encName);
            var lines = File.ReadAllLines(filePath, enc)
                .Skip(skipRows)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0) return new List<Dictionary<string, object?>>();

            string[] headers;
            int dataStart;

            if (hasHeader)
            {
                headers   = SplitCsvLine(lines[0], delimiter[0]);
                dataStart = 1;
            }
            else
            {
                // Kolon sayısına göre otomatik alan adı
                int cols = SplitCsvLine(lines[0], delimiter[0]).Length;
                headers   = Enumerable.Range(0, cols).Select(i => $"col_{i}").ToArray();
                dataStart = 0;
            }

            var rows = new List<Dictionary<string, object?>>();

            for (int i = dataStart; i < lines.Count; i++)
            {
                var values = SplitCsvLine(lines[i], delimiter[0]);
                var row    = new Dictionary<string, object?>();

                for (int j = 0; j < headers.Length; j++)
                    row[headers[j]] = j < values.Length ? (object?)TryParse(values[j]) : null;

                rows.Add(row);
            }

            ctx.Log($"  csv_read: '{Path.GetFileName(filePath)}' → {rows.Count} satır, {headers.Length} kolon");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // D02  csv_write
        //
        // input : List<Dict>
        // params: file_path    String  zorunlu
        //         delimiter    String  default=","
        //         include_header Bool  default=true
        //         encoding     String  default="utf-8-sig" (BOM - Excel uyumu)
        //
        // output: string (yazılan dosya yolu)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("csv_write",
            Description = "List<Dict> → CSV dosyasına yazar. " +
                          "encoding=utf-8-sig Excel'de Türkçe karakter desteği sağlar.",
            Category    = "CSV")]
        public static string CsvWrite(OpContext ctx)
        {
            var rows       = ctx.InputAs<List<Dictionary<string, object?>>>();
            var filePath   = ctx.RequireString("file_path");
            var delimiter  = ctx.GetString("delimiter", ",");
            bool inclHeader= ctx.GetBool("include_header", true);
            var encName    = ctx.GetString("encoding", "utf-8-sig");

            if (rows.Count == 0)
            {
                ctx.Log("  csv_write: boş liste → dosya yazılmadı");
                return "";
            }

            var enc    = SafeGetEncoding(encName);
            var dir    = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var headers = rows.SelectMany(r => r.Keys).Distinct().ToList();
            var sb      = new StringBuilder();

            if (inclHeader)
                sb.AppendLine(string.Join(delimiter,
                    headers.Select(h => QuoteCsvField(h, delimiter[0]))));

            foreach (var row in rows)
            {
                var values = headers.Select(h =>
                    row.TryGetValue(h, out var v)
                        ? QuoteCsvField(v?.ToString() ?? "", delimiter[0])
                        : "");
                sb.AppendLine(string.Join(delimiter, values));
            }

            File.WriteAllText(filePath, sb.ToString(), enc);
            ctx.Log($"  csv_write: {rows.Count} satır → '{filePath}'");
            return filePath;
        }

        // ─────────────────────────────────────────────────────────────────────
        // D03  excel_xml_read
        //
        // input : —
        // params: file_path   String  zorunlu
        //         sheet_name  String  default=""  (boşsa ilk sheet)
        //         has_header  Bool    default=true
        //         skip_rows   Int     default=0
        //
        // output: List<Dict>
        // Not: openpyxl kullanmaz — standart library zipfile+xml.etree yaklaşımı
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("excel_xml_read",
            Description = "xlsx dosyasını .NET ZipArchive + XML ile okur (openpyxl gerekmez). " +
                          "EGBIMOTO yaklaşımı: standart kütüphane, sıfır bağımlılık.",
            Category    = "CSV")]
        public static List<Dictionary<string, object?>> ExcelXmlRead(OpContext ctx)
        {
            var filePath  = ctx.RequireString("file_path");
            var sheetName = ctx.GetString("sheet_name", "");
            bool hasHeader= ctx.GetBool("has_header",  true);
            int skipRows  = ctx.GetInt("skip_rows",    0);

            if (!File.Exists(filePath))
            {
                return new List<Dictionary<string, object?>>
                {
                    new() { ["status"] = "PARAM_MISSING",
                            ["message"] = $"Dosya bulunamadı: {filePath}" }
                };
            }

            try
            {
                using var zip = ZipFile.OpenRead(filePath);

                // Shared strings
                var sharedStrings = ReadSharedStrings(zip);

                // Sheet listesi
                var workbookEntry = zip.GetEntry("xl/workbook.xml");
                if (workbookEntry == null)
                    return new List<Dictionary<string, object?>>();

                using var wbStream = workbookEntry.Open();
                var wb = XDocument.Load(wbStream);
                XNamespace ns = wb.Root?.GetDefaultNamespace() ?? "";

                var sheets = wb.Root?
                    .Descendants(ns + "sheet")
                    .Select(s => (
                        name: s.Attribute("name")?.Value ?? "",
                        id:   s.Attribute(XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships") + "id")?.Value
                              ?? s.Attribute("id")?.Value ?? ""
                    ))
                    .ToList() ?? new();

                // Doğru sheet'i bul
                string sheetId = "";
                if (!string.IsNullOrEmpty(sheetName))
                    sheetId = sheets.FirstOrDefault(s =>
                        s.name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)).id;
                else
                    sheetId = sheets.FirstOrDefault().id;

                // Sheet path'i rels'den çöz
                string sheetPath = ResolveSheetPath(zip, sheetId);
                if (string.IsNullOrEmpty(sheetPath))
                    return new List<Dictionary<string, object?>>();

                var sheetEntry = zip.GetEntry(sheetPath);
                if (sheetEntry == null)
                    return new List<Dictionary<string, object?>>();

                using var sheetStream = sheetEntry.Open();
                var sheetDoc = XDocument.Load(sheetStream);
                XNamespace sns = sheetDoc.Root?.GetDefaultNamespace() ?? "";

                var allRows = sheetDoc.Root?
                    .Descendants(sns + "row")
                    .Skip(skipRows)
                    .ToList() ?? new();

                if (allRows.Count == 0) return new List<Dictionary<string, object?>>();

                // Hücre okuma
                string[] headers = Array.Empty<string>();
                int dataStart    = 0;

                if (hasHeader && allRows.Count > 0)
                {
                    headers   = ReadRow(allRows[0], sns, sharedStrings)
                        .Select((v, i) => string.IsNullOrEmpty(v) ? $"col_{i}" : v)
                        .ToArray();
                    dataStart = 1;
                }
                else
                {
                    int cols = ReadRow(allRows[0], sns, sharedStrings).Count;
                    headers  = Enumerable.Range(0, cols).Select(i => $"col_{i}").ToArray();
                }

                var rows = new List<Dictionary<string, object?>>();
                for (int i = dataStart; i < allRows.Count; i++)
                {
                    var values = ReadRow(allRows[i], sns, sharedStrings);
                    var row    = new Dictionary<string, object?>();
                    for (int j = 0; j < headers.Length; j++)
                        row[headers[j]] = j < values.Count
                            ? (object?)TryParse(values[j])
                            : null;
                    rows.Add(row);
                }

                ctx.Log($"  excel_xml_read: {rows.Count} satır, {headers.Length} kolon");
                return rows;
            }
            catch (Exception ex)
            {
                ctx.Log($"  excel_xml_read: hata — {ex.Message}");
                return new List<Dictionary<string, object?>>
                {
                    new() { ["status"] = "ERROR", ["message"] = ex.Message }
                };
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // D04  table_to_points
        //
        // input : List<Dict>
        // params: x_field  String  default="x"
        //         y_field  String  default="y"
        //         z_field  String  default="z"
        //         unit     String  default="mm"  (mm|m|ft)
        //
        // output: List<object?>  (Revit XYZ listesi)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("table_to_points",
            Description = "Dict satırlarındaki x/y/z alanlarını Revit XYZ nokta listesine dönüştürür. " +
                          "Geometry Ops ve Create Ops için veri köprüsü.",
            Category    = "CSV")]
        public static List<object?> TableToPoints(OpContext ctx)
        {
            var rows    = ctx.InputAs<List<Dictionary<string, object?>>>();
            var xField  = ctx.GetString("x_field", "x");
            var yField  = ctx.GetString("y_field", "y");
            var zField  = ctx.GetString("z_field", "z");
            var unit    = ctx.GetString("unit",    "mm").ToLowerInvariant();

            double factor = unit switch
            {
                "m"  => 1.0 / 0.3048,
                "ft" => 1.0,
                _    => 1.0 / 304.8, // mm default
            };

            var result = new List<object?>();

            foreach (var row in rows)
            {
                double x = ParseField(row, xField) * factor;
                double y = ParseField(row, yField) * factor;
                double z = ParseField(row, zField) * factor;
                result.Add(new Autodesk.Revit.DB.XYZ(x, y, z));
            }

            ctx.Log($"  table_to_points: {rows.Count} → {result.Count} XYZ nokta (unit={unit})");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // D05  table_validate_schema
        //
        // input : List<Dict>
        // params: required_fields  String  virgülle ayrılmış zorunlu alanlar
        //         optional_fields  String  opsiyonel (sadece loglama için)
        //
        // output: Dict {valid, missing_fields, row_count, field_count}
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("table_validate_schema",
            Description = "Dict listesinin beklenen alanları içerip içermediğini kontrol eder.",
            Category    = "CSV")]
        public static Dictionary<string, object?> TableValidateSchema(OpContext ctx)
        {
            var rows     = ctx.InputAs<List<Dictionary<string, object?>>>();
            var reqStr   = ctx.RequireString("required_fields");
            var required = reqStr.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            if (rows.Count == 0)
                return new Dictionary<string, object?>
                {
                    ["valid"]  = false, ["missing_fields"] = string.Join(",", required),
                    ["row_count"] = 0,  ["field_count"] = 0,
                    ["message"] = "Boş tablo"
                };

            var sampleKeys = rows[0].Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing    = required.Where(f => !sampleKeys.Contains(f)).ToList();

            ctx.Log($"  table_validate_schema: {rows.Count} satır, eksik={missing.Count}");
            return new Dictionary<string, object?>
            {
                ["valid"]          = missing.Count == 0,
                ["missing_fields"] = string.Join(";", missing),
                ["row_count"]      = rows.Count,
                ["field_count"]    = sampleKeys.Count,
                ["message"]        = missing.Count == 0
                    ? "Şema geçerli"
                    : $"Eksik alanlar: {string.Join(", ", missing)}",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        private static string[] SplitCsvLine(string line, char delimiter)
        {
            var parts = new List<string>();
            bool inQuote = false;
            var current  = new StringBuilder();

            foreach (char c in line)
            {
                if (c == '"') { inQuote = !inQuote; }
                else if (c == delimiter && !inQuote)
                { parts.Add(current.ToString()); current.Clear(); }
                else { current.Append(c); }
            }
            parts.Add(current.ToString());
            return parts.ToArray();
        }

        private static string QuoteCsvField(string s, char delimiter)
        {
            if (s.Contains(delimiter) || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        private static object? TryParse(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (long.TryParse(s, out var l)) return l;
            if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            return s;
        }

        private static double ParseField(Dictionary<string, object?> row, string field)
        {
            if (!row.TryGetValue(field, out var v)) return 0;
            if (v is double d) return d;
            if (v is long   l) return l;
            if (double.TryParse(v?.ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var r)) return r;
            return 0;
        }

        // Excel XML helpers
        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list  = new List<string>();
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return list;
            using var s = entry.Open();
            var doc = XDocument.Load(s);
            XNamespace ns = doc.Root?.GetDefaultNamespace() ?? "";
            list.AddRange(doc.Descendants(ns + "t").Select(t => t.Value));
            return list;
        }

        private static string ResolveSheetPath(ZipArchive zip, string rId)
        {
            var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (relsEntry == null) return "";
            using var s = relsEntry.Open();
            var doc = XDocument.Load(s);
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
            var rel = doc.Descendants(ns + "Relationship")
                .FirstOrDefault(r => r.Attribute("Id")?.Value == rId);
            string? target = rel?.Attribute("Target")?.Value;
            if (target == null) return "";
            return target.StartsWith("/") ? target.TrimStart('/') : $"xl/{target}";
        }

        private static List<string> ReadRow(XElement row, XNamespace ns,
            List<string> sharedStrings)
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                string t = cell.Attribute("t")?.Value ?? "";
                string v = cell.Element(ns + "v")?.Value ?? "";
                if (t == "s" && int.TryParse(v, out int idx) && idx < sharedStrings.Count)
                    cells.Add(sharedStrings[idx]);
                else if (t == "inlineStr")
                    cells.Add(cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? "");
                else
                    cells.Add(v);
            }
            return cells;
        }
        /// <summary>
        /// "utf-8-sig" gibi .NET'in tanımadığı encoding isimlerini güvenle çözer.
        /// utf-8-sig / utf-8-bom → UTF8Encoding(BOM=true)
        /// utf-8 / utf8         → UTF8Encoding(BOM=false)
        /// Diğerleri            → Encoding.GetEncoding() ile dene
        /// </summary>
        private static Encoding SafeGetEncoding(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "utf-8-sig" or "utf-8-bom" or "utf8-sig" or "utf8-bom"
                    => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                "utf-8" or "utf8"
                    => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                _ => Encoding.GetEncoding(name),
            };
        }

    }
}
