using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace EGBIMOTO.Addin.Reports
{
    /// <summary>
    /// Kalıp BOQ Excel çıktısı — OpenXml (OOXML) manuel yazımı.
    /// Harici kütüphane bağımlılığı yok (sadece System.IO.Compression).
    ///
    /// 3 sekme:
    ///   1. POZ ÖZETİ  — poz_no, tanım, birim, miktar, birim fiyat, tutar
    ///   2. KAT ÖZETİ  — kat, kategori, toplam m², toplam TL
    ///   3. ELEMAN DETAYı — element_id, kategori, tip, kat, m², poz_no, birim fiyat, tutar
    /// </summary>
    public static class KalipXlsxBuilder
    {
        public static string Build(
            List<Dictionary<string, object?>> rows,
            Dictionary<string, Dictionary<string, object?>> pozIndex,
            string outDir, string fileBase, string projName)
        {
            string fname = $"{fileBase}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string path  = Path.Combine(outDir, fname);

            // Enrich rows with cost
            var enriched = rows.Select(r =>
            {
                string pozNo = r.TryGetValue("poz_no", out var pn) ? pn?.ToString() ?? "" : "";
                double m2    = ToD(r, "kalip_m2");
                double fiyat = 0;
                string tanim = "", birim = "m²";
                if (pozIndex.TryGetValue(pozNo, out var pi))
                {
                    tanim  = pi.TryGetValue("tanim",      out var ta) ? ta?.ToString() ?? "" : "";
                    birim  = pi.TryGetValue("birim",      out var bi) ? bi?.ToString() ?? "m²" : "m²";
                    double.TryParse(pi.TryGetValue("birim_fiyat", out var up) ?
                        up?.ToString() : "0", out fiyat);
                }
                return new
                {
                    eid    = r.TryGetValue("element_id", out var ei) ? ei?.ToString() ?? "" : "",
                    cat    = r.TryGetValue("kategori",   out var cv) ? cv?.ToString() ?? "" : "",
                    tip    = r.TryGetValue("tip",        out var ti) ? ti?.ToString() ?? "" : "",
                    kat    = r.TryGetValue("kat",        out var ka) ? ka?.ToString() ?? "" : "",
                    method = r.TryGetValue("method",     out var me) ? me?.ToString() ?? "" : "",
                    pozNo, tanim, birim, m2, fiyat, tutar = m2 * fiyat
                };
            }).ToList();

            // Sheet 1: Poz Özeti
            var pozOzet = enriched
                .GroupBy(r => r.pozNo)
                .Select(g =>
                {
                    var first = g.First();
                    double totalM2    = g.Sum(r => r.m2);
                    double birimFiyat = first.fiyat;
                    return new object?[]
                    {
                        g.Key, first.tanim, first.birim,
                        Math.Round(totalM2, 3),
                        Math.Round(birimFiyat, 2),
                        Math.Round(totalM2 * birimFiyat, 2),
                        g.Count()
                    };
                })
                .OrderByDescending(r => (double)(r[5] ?? 0))
                .ToList();

            double grandTutar = pozOzet.Sum(r => (double)(r[5] ?? 0));
            double grandM2    = pozOzet.Sum(r => (double)(r[3] ?? 0));
            pozOzet.Add(new object?[] { "TOPLAM", "", "", Math.Round(grandM2,3), "", Math.Round(grandTutar,2), enriched.Count });

            // Sheet 2: Kat Özeti
            var katOzet = enriched
                .GroupBy(r => (r.kat, r.cat))
                .Select(g => new object?[]
                {
                    g.Key.kat, g.Key.cat,
                    g.Count(),
                    Math.Round(g.Sum(r => r.m2), 3),
                    Math.Round(g.Sum(r => r.tutar), 2)
                })
                .OrderBy(r => r[0]?.ToString())
                .ThenByDescending(r => (double)(r[3] ?? 0))
                .ToList();

            // Sheet 3: Eleman Detay
            var detay = enriched.Select(r => new object?[]
            {
                r.eid, r.cat, r.tip, r.kat,
                Math.Round(r.m2, 3), r.pozNo, r.tanim,
                Math.Round(r.fiyat, 2),
                Math.Round(r.tutar, 2),
                r.method
            }).ToList<object?[]>();

            // Xlsx oluştur
            var sheets = new List<(string name, string[] headers, List<object?[]> data)>
            {
                ("Poz Özeti",
                 new[]{ "Poz No","Poz Tanımı","Birim","Miktar (m²)","Birim Fiyat (₺)","Tutar (₺)","Eleman Sayısı" },
                 pozOzet),

                ("Kat Özeti",
                 new[]{ "Kat","Kategori","Eleman Sayısı","Alan (m²)","Tutar (₺)" },
                 katOzet),

                ("Eleman Detayı",
                 new[]{ "ID","Kategori","Tip","Kat","Alan (m²)","Poz No","Poz Tanımı","Birim Fiyat","Tutar (₺)","Yöntem" },
                 detay),
            };

            WriteXlsx(path, sheets, projName);
            return path;
        }

        // ════════════════════════════════════════════════════════════════════
        // XLSX YAZICI (OOXML manuel, harici kütüphane yok)
        // ════════════════════════════════════════════════════════════════════
        private static void WriteXlsx(string path,
            List<(string name, string[] headers, List<object?[]> data)> sheets,
            string projName)
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

            // [Content_Types].xml
            AddEntry(zip, "[Content_Types].xml", ContentTypes(sheets.Count));
            // _rels/.rels
            AddEntry(zip, "_rels/.rels", Rels());
            // xl/workbook.xml
            AddEntry(zip, "xl/workbook.xml", Workbook(sheets));
            // xl/_rels/workbook.xml.rels
            AddEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(sheets.Count));
            // xl/styles.xml
            AddEntry(zip, "xl/styles.xml", Styles());
            // xl/sharedStrings.xml + sheets
            var sst = new SharedStringTable();

            for (int i = 0; i < sheets.Count; i++)
            {
                var (name, headers, data) = sheets[i];
                string sheetXml = SheetXml(headers, data, sst, i == 0);
                AddEntry(zip, $"xl/worksheets/sheet{i+1}.xml", sheetXml);
            }
            AddEntry(zip, "xl/sharedStrings.xml", sst.ToXml());
        }

        private static void AddEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
            w.Write(content);
        }

        private static string SheetXml(string[] headers, List<object?[]> data,
            SharedStringTable sst, bool freezeTop)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"");
            sb.Append(" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");

            if (freezeTop)
                sb.Append("<sheetViews><sheetView workbookViewId=\"0\">" +
                           "<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>" +
                           "</sheetView></sheetViews>");

            sb.Append("<sheetData>");

            // Header row
            sb.Append($"<row r=\"1\">");
            for (int c = 0; c < headers.Length; c++)
                sb.Append(Cell(1, c, headers[c], sst, isHeader: true));
            sb.Append("</row>");

            // Data rows
            for (int i = 0; i < data.Count; i++)
            {
                int row = i + 2;
                bool isTotal = data[i].Length > 0 &&
                    data[i][0]?.ToString()?.StartsWith("TOPLAM") == true;
                sb.Append($"<row r=\"{row}\">");
                for (int c = 0; c < data[i].Length; c++)
                    sb.Append(Cell(row, c, data[i][c], sst, isTotal: isTotal));
                sb.Append("</row>");
            }

            sb.Append("</sheetData>");

            // AutoFilter on header
            string lastCol = ColLetter(headers.Length - 1);
            sb.Append($"<autoFilter ref=\"A1:{lastCol}1\"/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static string Cell(int row, int col, object? val, SharedStringTable sst,
            bool isHeader = false, bool isTotal = false)
        {
            string addr = $"{ColLetter(col)}{row}";
            int style = isHeader ? 1 : (isTotal ? 2 : 0);

            if (val == null || val.ToString() == "")
                return $"<c r=\"{addr}\" s=\"{style}\"/>";

            if (val is double d || double.TryParse(val?.ToString(), out d))
            {
                string ds = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                int numStyle = isHeader ? 1 : (isTotal ? 3 : 4);
                return $"<c r=\"{addr}\" t=\"n\" s=\"{numStyle}\"><v>{ds}</v></c>";
            }

            int idx = sst.Add(val?.ToString() ?? "");
            return $"<c r=\"{addr}\" t=\"s\" s=\"{style}\"><v>{idx}</v></c>";
        }

        private static string ColLetter(int col)
        {
            string s = "";
            col++;
            while (col > 0) { s = (char)('A' + (col-1)%26) + s; col = (col-1)/26; }
            return s;
        }

        private static string ContentTypes(int sheetCount)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            sb.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
            for (int i = 1; i <= sheetCount; i++)
                sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sb.Append("</Types>");
            return sb.ToString();
        }

        private static string Rels() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private static string Workbook(List<(string name, string[] h, List<object?[]> d)> sheets)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.Append("<sheets>");
            for (int i = 0; i < sheets.Count; i++)
                sb.Append($"<sheet name=\"{XmlEsc(sheets[i].name)}\" sheetId=\"{i+1}\" r:id=\"rId{i+1}\"/>");
            sb.Append("</sheets></workbook>");
            return sb.ToString();
        }

        private static string WorkbookRels(int count)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 1; i <= count; i++)
                sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            sb.Append($"<Relationship Id=\"rId{count+1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            sb.Append($"<Relationship Id=\"rId{count+2}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        private static string Styles() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"3\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/><b/><color rgb=\"FFFFFFFF\"/></font>" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/><b/></font>" +
            "</fonts>" +
            "<fills count=\"4\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1A3A5C\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8F0FE\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"2\">" +
            "<border><left/><right/><top/><bottom/></border>" +
            "<border><left style=\"thin\"><color auto=\"1\"/></left>" +
            "<right style=\"thin\"><color auto=\"1\"/></right>" +
            "<top style=\"thin\"><color auto=\"1\"/></top>" +
            "<bottom style=\"thin\"><color auto=\"1\"/></bottom></border>" +
            "</borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"5\">" +
            // 0: normal
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\"/>" +
            // 1: header (koyu mavi, beyaz bold yazı)
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\"><alignment horizontal=\"center\"/></xf>" +
            // 2: total (açık mavi zemin, bold)
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\"/>" +
            // 3: total sayı
            "<xf numFmtId=\"4\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\"><alignment horizontal=\"right\"/></xf>" +
            // 4: normal sayı (#,##0.00)
            "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\"><alignment horizontal=\"right\"/></xf>" +
            "</cellXfs>" +
            "</styleSheet>";

        private static string XmlEsc(string s)
            => s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");

        private static double ToD(Dictionary<string, object?> r, string key)
        {
            if (r.TryGetValue(key, out var v) && double.TryParse(v?.ToString(), out var d)) return d;
            return 0.0;
        }

        private class SharedStringTable
        {
            private readonly List<string> _strings = new();
            private readonly Dictionary<string, int> _index = new();

            public int Add(string s)
            {
                if (_index.TryGetValue(s, out var i)) return i;
                _index[s] = _strings.Count;
                _strings.Add(s);
                return _strings.Count - 1;
            }

            public string ToXml()
            {
                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sb.Append($"<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"{_strings.Count}\" uniqueCount=\"{_strings.Count}\">");
                foreach (var s in _strings)
                    sb.Append($"<si><t xml:space=\"preserve\">{XmlEsc(s)}</t></si>");
                sb.Append("</sst>");
                return sb.ToString();
            }
        }
    }
}
