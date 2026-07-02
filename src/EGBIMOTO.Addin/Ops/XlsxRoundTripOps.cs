using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// v12 — xlsx round-trip: dışa aktarılmış (export_xlsx) bir tabloyu kullanıcı
    /// Excel'de düzenledikten sonra geri okuyup (1) önce DEĞİŞEN satırları
    /// önizleme olarak gösterir, (2) onaylandıktan sonra parametrelere yazar.
    ///
    /// PCH_FilterMan (Marcin Marek) eklentisindeki excel_handler.py'nin
    /// "openpyxl yok, sadece zipfile + XML" yaklaşımının okuma (import) yönü —
    /// EGBIMOTO'nun export_xlsx'i (ExportOps.cs) zaten aynı minimal OOXML
    /// formatını (sheetData + sst) yazıyor; bu op aynı formatı (ve makul ölçüde
    /// genel Excel dosyalarını) okur.
    ///
    /// Kalıba özel değildir: hangi op zincirinin ürettiği satırlar olursa olsun
    /// (kalip_all, intersect_report, herhangi bir collect+hesap zinciri) kullanılabilir.
    /// </summary>
    public static class XlsxRoundTripOps
    {
        // ════════════════════════════════════════════════════════════════════
        // XLSX_IMPORT_PREVIEW
        // ════════════════════════════════════════════════════════════════════
        [EgOp("xlsx_import_preview",
            Description = "Bir .xlsx dosyasını okur ve (varsa) 'from' ile verilen orijinal satır " +
                          "listesiyle key_field üzerinden karşılaştırıp değişen/yeni/eksik satırları işaretler. " +
                          "Hiçbir Transaction açmaz — sadece önizleme üretir. " +
                          "params: file_path (zorunlu), key_field (default 'element_id')",
            Category    = "Çıktı")]
        public static List<Dictionary<string, object?>> XlsxImportPreview(OpContext ctx)
        {
            var filePath = ctx.RequireString("file_path");
            var keyField  = ctx.GetString("key_field", "element_id");

            if (!File.Exists(filePath))
            {
                ctx.Log($"  ⚠ xlsx_import_preview: dosya bulunamadı: {filePath}");
                return new();
            }

            var imported = ReadXlsx(filePath);
            if (imported.Count == 0)
            {
                ctx.Log("  ⚠ xlsx_import_preview: dosyada satır okunamadı");
                return new();
            }

            var original = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>() ?? new();
            var originalByKey = original
                .Where(r => r.ContainsKey(keyField))
                .GroupBy(r => r[keyField]?.ToString() ?? "")
                .ToDictionary(g => g.Key, g => g.First());

            var preview = new List<Dictionary<string, object?>>();
            int changed = 0, same = 0, added = 0;

            foreach (var row in imported)
            {
                if (!row.TryGetValue(keyField, out var kv) || kv is null)
                {
                    row["_status"] = "no_key";
                    preview.Add(row);
                    continue;
                }
                var key = kv.ToString() ?? "";

                if (!originalByKey.TryGetValue(key, out var origRow))
                {
                    row["_status"] = "new";
                    preview.Add(row);
                    added++;
                    continue;
                }

                bool isChanged = false;
                var diffFields = new List<string>();
                foreach (var col in row.Keys.ToList())
                {
                    if (col.StartsWith("_")) continue;
                    var newVal = row[col]?.ToString() ?? "";
                    var oldVal = origRow.TryGetValue(col, out var ov) ? ov?.ToString() ?? "" : "";
                    if (!string.Equals(newVal, oldVal, StringComparison.Ordinal))
                    {
                        isChanged = true;
                        diffFields.Add(col);
                    }
                }

                row["_status"] = isChanged ? "changed" : "same";
                row["_diff_fields"] = string.Join(",", diffFields);
                preview.Add(row);
                if (isChanged) changed++; else same++;
            }

            ctx.Log($"  xlsx_import_preview: {preview.Count} satır — {changed} değişti, {same} aynı, {added} yeni");
            return preview;
        }

        // ════════════════════════════════════════════════════════════════════
        // XLSX_IMPORT_APPLY
        // ════════════════════════════════════════════════════════════════════
        [EgOp("xlsx_import_apply",
            Description = "xlsx_import_preview çıktısındaki (veya doğrudan bir .xlsx dosyasındaki) " +
                          "satırları element_id üzerinden eşleştirip belirtilen kolon→parametre " +
                          "eşlemesine göre Revit parametrelerine yazar. " +
                          "params: file_path (file_path verilirse dosyadan okur, yoksa 'from' kullanılır), " +
                          "key_field (default 'element_id'), field_param_map (Dictionary<string,string>, " +
                          "kolon adı → Revit parametre adı), only_changed (bool, default true — " +
                          "_status alanı 'changed'/'new' olmayan satırları atlar).",
            Category    = "Maliyet",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> XlsxImportApply(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var keyField   = ctx.GetString("key_field", "element_id");
            var onlyChanged = ctx.GetBool("only_changed", true);
            var fieldMap    = ctx.GetParam<Dictionary<string, object?>>("field_param_map", new());

            List<Dictionary<string, object?>> rows;
            var filePath = ctx.GetString("file_path", "");
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                if (!File.Exists(filePath))
                {
                    ctx.Log($"  ⚠ xlsx_import_apply: dosya bulunamadı: {filePath}");
                    return new() { ["written"] = 0, ["skipped"] = 0 };
                }
                rows = ReadXlsx(filePath);
            }
            else
            {
                rows = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>() ?? new();
            }

            if (rows.Count == 0)
            {
                ctx.Log("  ⚠ xlsx_import_apply: yazılacak satır yok");
                return new() { ["written"] = 0, ["skipped"] = 0 };
            }

            if (fieldMap.Count == 0)
            {
                ctx.Log("  ⚠ xlsx_import_apply: field_param_map boş — hiçbir parametre yazılmayacak");
            }

            int written = 0, skipped = 0;

            using var scope = new RevitWriteScope(rctx.Doc, "Xlsx İçe Aktar", rctx.IsAtomicMode);
            foreach (var row in rows)
            {
                if (onlyChanged && row.TryGetValue("_status", out var st))
                {
                    var status = st?.ToString() ?? "";
                    if (status != "changed" && status != "new") { skipped++; continue; }
                }

                if (!row.TryGetValue(keyField, out var kv) || kv is null ||
                    !long.TryParse(kv.ToString(), out var idLong))
                {
                    skipped++;
                    continue;
                }

                var el = rctx.Doc.GetElement(Rv.MakeElementId(idLong));
                if (el == null) { skipped++; continue; }

                bool anyWritten = false;
                foreach (var kvp in fieldMap)
                {
                    var column = kvp.Key;
                    var paramName = kvp.Value?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(paramName)) continue;
                    if (!row.TryGetValue(column, out var valueObj)) continue;

                    if (WriteParam(el, paramName, valueObj?.ToString() ?? ""))
                        anyWritten = true;
                }

                if (anyWritten) written++; else skipped++;
            }
            scope.Commit();

            ctx.Log($"  xlsx_import_apply: {written} eleman güncellendi, {skipped} atlandı");
            return new() { ["written"] = written, ["skipped"] = skipped };
        }

        private static bool WriteParam(Element el, string paramName, string value)
        {
            try
            {
                var p = el.LookupParameter(paramName);
                if (p == null || p.IsReadOnly) return false;

                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(value);
                        return true;
                    case StorageType.Double:
                        if (double.TryParse(value, out var d)) { p.Set(d); return true; }
                        return false;
                    case StorageType.Integer:
                        if (int.TryParse(value, out var i)) { p.Set(i); return true; }
                        return false;
                    default:
                        return false;
                }
            }
            catch { return false; }
        }

        // ════════════════════════════════════════════════════════════════════
        // Minimal xlsx okuyucu — sadece System.IO.Compression + System.Xml.Linq
        // (openpyxl/EPPlus/ClosedXML YOK — proje kuralı).
        // Excel'in standart sheetData + (varsa) sharedStrings formatını okur.
        // İlk satır başlık (header) kabul edilir.
        // ════════════════════════════════════════════════════════════════════
        private static List<Dictionary<string, object?>> ReadXlsx(string path)
        {
            var result = new List<Dictionary<string, object?>>();

            using var archive = ZipFile.OpenRead(path);

            var sheetEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (sheetEntry == null) return result;

            var sharedStrings = new List<string>();
            var sstEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sstEntry != null)
            {
                using var sstStream = sstEntry.Open();
                var sstDoc = XDocument.Load(sstStream);
                XNamespace ns = sstDoc.Root!.Name.Namespace;
                foreach (var si in sstDoc.Root!.Elements(ns + "si"))
                {
                    // <si><t>text</t></si> veya <si><r><t>..</t></r>...</si> (zengin metin)
                    var text = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
                    sharedStrings.Add(text);
                }
            }

            using var sheetStream = sheetEntry.Open();
            var sheetDoc = XDocument.Load(sheetStream);
            XNamespace wns = sheetDoc.Root!.Name.Namespace;

            var rowsXml = sheetDoc.Root!
                .Element(wns + "sheetData")?
                .Elements(wns + "row")
                .ToList() ?? new List<XElement>();

            if (rowsXml.Count == 0) return result;

            List<string> headers = new();
            for (int ri = 0; ri < rowsXml.Count; ri++)
            {
                // Hücreleri pozisyonel değil, gerçek sütun indeksine (r="C5" → 2) göre
                // konumlandırıyoruz — Excel boş hücreleri/sütunları atlayabilir.
                var indexed = new Dictionary<int, string>();
                int maxCol = -1;
                foreach (var c in rowsXml[ri].Elements(wns + "c"))
                {
                    int colIdx = ColIndexFromRef(c.Attribute("r")?.Value ?? "");
                    if (colIdx < 0) continue;

                    var t   = c.Attribute("t")?.Value ?? "n";
                    var vEl = c.Element(wns + "v");
                    var raw = vEl?.Value ?? "";
                    string final = t switch
                    {
                        "s" when int.TryParse(raw, out var sIdx) && sIdx >= 0 && sIdx < sharedStrings.Count
                            => sharedStrings[sIdx],
                        "str" => raw,
                        "inlineStr" => c.Element(wns + "is")?.Element(wns + "t")?.Value ?? "",
                        _ => raw
                    };
                    indexed[colIdx] = final;
                    if (colIdx > maxCol) maxCol = colIdx;
                }

                if (ri == 0)
                {
                    headers = new List<string>();
                    for (int c = 0; c <= maxCol; c++)
                        headers.Add(indexed.TryGetValue(c, out var h) ? h : $"col{c}");
                    continue;
                }

                if (headers.Count == 0) continue;

                var dict = new Dictionary<string, object?>();
                for (int c = 0; c < headers.Count; c++)
                    dict[headers[c]] = indexed.TryGetValue(c, out var val) ? val : "";
                result.Add(dict);
            }

            return result;
        }

        /// <summary>"C5" gibi bir Excel hücre referansından 0-bazlı sütun indeksini çıkarır.</summary>
        private static int ColIndexFromRef(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return -1;
            int idx = 0;
            foreach (var ch in cellRef)
            {
                if (!char.IsLetter(ch)) break;
                idx = idx * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            }
            return idx - 1; // 1-bazlıdan 0-bazlıya
        }
    }
}
