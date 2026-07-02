using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Data;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Veri yükleme, WBS, haritalama ve dışa aktarma op'ları.
    /// </summary>
    public static class DataOps
    {
        // ── Genel veri yükleme ────────────────────────────────────────────────
        [EgOp("data_load",
            Description = "JSON dosyasını yükler, normalize eder, DataRegistry'ye kaydeder. " +
                          "inputs: path (data/ altı göreli veya tam yol), " +
                          "key (opsiyonel, varsayılan dosya adı), " +
                          "root_key (opsiyonel wrapper anahtarı, örn: 'rules'), " +
                          "cache_bust (opsiyonel bool, true=her seferinde taze oku)",
            Category    = "Veri")]
        public static List<Dictionary<string, object?>> DataLoad(OpContext ctx)
        {
            var relPath   = ctx.GetString("path");
            var rootKey   = ctx.GetString("root_key", "");
            var cacheBust = ctx.GetBool("cache_bust", false);

            // Göreli yol → data/ klasörüne göre çöz
            var fullPath = Path.IsPathRooted(relPath)
                ? relPath
                : Path.Combine(EgbimotoData.DataRoot, relPath);

            var key = ctx.GetString("key", Path.GetFileNameWithoutExtension(fullPath));

            // Cache kontrolü — cache_bust:true ise atla
            if (!cacheBust &&
                EgbimotoData.Registry.IsCached(key) &&
                EgbimotoData.Registry.Get(key) is List<Dictionary<string, object?>> cached)
            {
                ctx.Log($"  data_load '{key}': cache'den ({cached.Count} satır)");
                return cached;
            }

            // Dosya yoksa net hata — sessiz geçme yok
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    $"[data_load] '{key}' için veri dosyası bulunamadı: {fullPath}\n" +
                    $"  → data/ klasörüne '{relPath}' dosyasını ekleyin.");

            object? raw;
            try
            {
                raw = DataRegistry.LoadJson(fullPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[data_load] '{key}' dosyası okunamadı: {fullPath}\n  → {ex.Message}", ex);
            }

            // root_key verilmişse wrapper açılır, yoksa NormalizeRows otomatik bulur
            var rows = string.IsNullOrWhiteSpace(rootKey)
                ? DataRegistry.NormalizeRows(raw)
                : DataRegistry.NormalizeRows(raw, rootKey);

            if (rows.Count == 0)
                ctx.Log($"  ⚠ data_load '{key}': 0 satır yüklendi — " +
                        $"JSON formatını veya root_key='{rootKey}' değerini kontrol edin.");

            EgbimotoData.Registry.Set(key, rows);
            ctx.Log($"  data_load '{key}': {rows.Count} satır ({Path.GetFileName(fullPath)})");
            return rows;
        }

        [EgOp("data_get",
            Description = "DataRegistry'den params.key ile kayıtlı veriyi döner",
            Category    = "Veri")]
        public static object? DataGet(OpContext ctx)
        {
            var key  = ctx.GetString("key");
            var data = EgbimotoData.Registry.Get(key);
            ctx.Log($"  data_get '{key}': {(data is null ? "bulunamadı" : "OK")}");
            return data;
        }

        [EgOp("data_list_keys",
            Description = "DataRegistry'deki tüm kayıtlı anahtarları listeler",
            Category    = "Veri")]
        public static List<string> DataListKeys(OpContext ctx)
        {
            var keys = EgbimotoData.Registry.GetKeys();
            ctx.Log($"  data_list_keys: {keys.Count} anahtar");
            return keys;
        }

        // ── Özel veri yükleyiciler ────────────────────────────────────────────
        [EgOp("load_poz_data",
            Description = "POZ verilerini yükler. params: poz_path (opsiyonel, varsayılan data/poz/csb_poz_2026.json)",
            Category    = "Veri")]
        public static List<Dictionary<string, object?>> LoadPozData(OpContext ctx)
        {
            // "poz_path", "path" veya "file" key — hepsi desteklenir
            var path = ctx.GetString("poz_path",
                ctx.GetString("path",
                ctx.GetString("file",
                Path.Combine(EgbimotoData.DataRoot, "poz", "csb_poz_2026.json"))));

            // Göreli path → DataRoot'a bağla (DataLoad() ile tutarlı)
            if (!Path.IsPathRooted(path))
                path = Path.Combine(EgbimotoData.DataRoot, path);

            if (!File.Exists(path)) throw new FileNotFoundException($"POZ dosyası bulunamadı: {path}");
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            List<Dictionary<string, object?>> list;

            // Format 1: [ {poz_no, tanim, birim, birim_fiyat, ...}, ... ]  (düz liste)
            // Format 2: { "15.100.1001": {tanim, birim, toplam:{birim_fiyat_toplam}}, ... }  (csb_poz_2026.json)
            // Format 3: { "rates": [ {poz_no, unit, unit_price, ...} ] }  (test verisi — data/test/csb_2026_sample_rates.json)
            try
            {
                list = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json, opts) ?? new();
            }
            catch
            {
                list = new();
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, opts) ?? new();

                // Format 3: wrapper ile { "rates": [...] }
                if (dict.TryGetValue("rates", out var ratesEl) && ratesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in ratesEl.EnumerateArray())
                    {
                        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in el.EnumerateObject())
                            row[prop.Name] = prop.Value.ValueKind == JsonValueKind.Number
                                ? prop.Value.GetDouble() : (object?)prop.Value.GetString();
                        list.Add(row);
                    }
                }
                else
                {
                    // Format 2: { poz_no: { tanim, birim, toplam: { birim_fiyat_toplam } } }
                    foreach (var kv in dict)
                    {
                        if (kv.Key.StartsWith("_")) continue;
                        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["poz_no"] = kv.Key
                        };
                        if (kv.Value.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in kv.Value.EnumerateObject())
                            {
                                if (prop.Name == "toplam" && prop.Value.ValueKind == JsonValueKind.Object)
                                {
                                    // birim_fiyat_toplam → birim_fiyat olarak al
                                    if (prop.Value.TryGetProperty("birim_fiyat_toplam", out var bft))
                                        row["birim_fiyat"] = bft.GetDouble();
                                }
                                else if (prop.Value.ValueKind == JsonValueKind.String)
                                    row[prop.Name] = prop.Value.GetString();
                                else if (prop.Value.ValueKind == JsonValueKind.Number)
                                    row[prop.Name] = prop.Value.GetDouble();
                            }
                        }
                        list.Add(row);
                    }
                }
            }

            EgbimotoData.Registry.Set("poz_data", list);
            ctx.Log($"  load_poz_data: {list.Count} kayıt yüklendi ({Path.GetFileName(path)})");
            return list;
        }

        [EgOp("load_wbs_mapping",
            Description = "WBS haritalama verilerini yükler. params: wbs_path (opsiyonel)",
            Category    = "Veri")]
        public static Dictionary<string, object?> LoadWbsMapping(OpContext ctx)
        {
            var path = ctx.GetString("wbs_path",
                Path.Combine(EgbimotoData.DataRoot, "mapping", "revit_kategori_wbs.json"));
            if (!Path.IsPathRooted(path))
                path = Path.Combine(EgbimotoData.DataRoot, path);
            if (!File.Exists(path)) throw new FileNotFoundException($"WBS dosyası bulunamadı: {path}");
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            EgbimotoData.Registry.Set("wbs_mapping", data);
            ctx.Log($"  load_wbs_mapping: {data.Count} kategori");
            return data;
        }

        [EgOp("load_qa_matrix",
            Description = "QA kural matrisini yükler. params: qa_path (opsiyonel)",
            Category    = "Veri")]
        public static object? LoadQaMatrix(OpContext ctx)
        {
            var path = ctx.GetString("qa_path",
                Path.Combine(EgbimotoData.DataRoot, "semantic", "qa_rule_matrix.json"));
            if (!Path.IsPathRooted(path))
                path = Path.Combine(EgbimotoData.DataRoot, path);
            if (!File.Exists(path)) throw new FileNotFoundException($"QA matris dosyası bulunamadı: {path}");
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<object>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            EgbimotoData.Registry.Set("qa_matrix", data);
            ctx.Log($"  load_qa_matrix: {path}");
            return data;
        }

        [EgOp("load_shared_param_map",
            Description = "Paylaşımlı parametre haritasını yükler",
            Category    = "Veri")]
        public static Dictionary<string, object?> LoadSharedParamMap(OpContext ctx)
        {
            var path = ctx.GetString("map_path",
                Path.Combine(EgbimotoData.DataRoot, "mapping", "shared_param_map.json"));
            if (!Path.IsPathRooted(path))
                path = Path.Combine(EgbimotoData.DataRoot, path);
            if (!File.Exists(path)) throw new FileNotFoundException($"Parametre haritası bulunamadı: {path}");
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            EgbimotoData.Registry.Set("shared_param_map", data);
            ctx.Log($"  load_shared_param_map: {data.Count} parametre");
            return data;
        }

        [EgOp("load_ifc_mapping",
            Description = "IFC haritalama verilerini yükler",
            Category    = "Veri")]
        public static Dictionary<string, object?> LoadIfcMapping(OpContext ctx)
        {
            var path = ctx.GetString("ifc_path",
                Path.Combine(EgbimotoData.DataRoot, "mapping", "ifc_mapping.json"));
            if (!Path.IsPathRooted(path))
                path = Path.Combine(EgbimotoData.DataRoot, path);
            if (!File.Exists(path)) throw new FileNotFoundException($"IFC haritalama bulunamadı: {path}");
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            EgbimotoData.Registry.Set("ifc_mapping", data);
            ctx.Log($"  load_ifc_mapping: {data.Count} kategori");
            return data;
        }

        [EgOp("load_rayic",
            Description = "Rayiç fiyat listesini yükler. params: rayic_path (opsiyonel)",
            Category    = "Veri")]
        public static List<Dictionary<string, object?>> LoadRayic(OpContext ctx)
        {
            var path = ctx.GetString("rayic_path",
                Path.Combine(EgbimotoData.DataRoot, "poz", "rayic_2026.json"));
            if (!Path.IsPathRooted(path))
                path = Path.Combine(EgbimotoData.DataRoot, path);
            if (!File.Exists(path)) throw new FileNotFoundException($"Rayiç dosyası bulunamadı: {path}");
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            EgbimotoData.Registry.Set("rayic_data", list);
            ctx.Log($"  load_rayic: {list.Count} kayıt");
            return list;
        }

        // ── WBS İşlemleri ─────────────────────────────────────────────────────
        [EgOp("assign_wbs_code",
            Description = "Satır listesindeki kategori alanına göre WBS kodu atar. " +
                          "params: category_field (default: 'kategori'), " +
                          "aktivite_field (default: '', boşsa sadece kategori bazlı eşleme). " +
                          "Yeni format: revit_kategori_wbs.json → {wbs_kodu, disiplin, canonical_v3} döner.",
            Category    = "Veri")]
        public static List<Dictionary<string, object?>> AssignWbsCode(OpContext ctx)
        {
            var rows          = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var catField      = ctx.GetString("category_field", "kategori");
            var aktiviteField = ctx.GetString("aktivite_field", "");
            var wbsMapping    = EgbimotoData.Registry.Get("wbs_mapping")
                as Dictionary<string, object?> ?? new();

            int assigned = 0;
            foreach (var row in rows)
            {
                var cat = row.TryGetValue(catField, out var v) ? v?.ToString() ?? "" : "";

                // Önce kategori|aktivite ile dene, sonra sadece kategori
                string lookupKey = cat;
                if (!string.IsNullOrEmpty(aktiviteField) &&
                    row.TryGetValue(aktiviteField, out var ak) && ak != null)
                    lookupKey = $"{cat}|{ak}";

                object? wbsEntry = null;
                if (!wbsMapping.TryGetValue(lookupKey, out wbsEntry) && lookupKey != cat)
                    wbsMapping.TryGetValue(cat, out wbsEntry);

                if (wbsEntry != null)
                {
                    // Yeni format: JsonElement {wbs_kodu, disiplin, canonical_v3}
                    if (wbsEntry is System.Text.Json.JsonElement je &&
                        je.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        row["wbs_kodu"]     = je.TryGetProperty("wbs_kodu",     out var wk) ? wk.GetString() : "";
                        row["wbs_disiplin"] = je.TryGetProperty("disiplin",     out var wd) ? wd.GetString() : "";
                        row["canonical_v3"] = je.TryGetProperty("canonical_v3", out var cv) ? cv.GetString() : "";
                        row["ebeveyn_wbs"]  = je.TryGetProperty("ebeveyn_wbs",  out var ew) ? ew.GetString() : "";
                    }
                    else
                    {
                        // Eski format: düz string
                        row["wbs_kodu"] = wbsEntry.ToString() ?? "";
                    }
                    assigned++;
                }
                else
                {
                    row["wbs_kodu"]     = "—";
                    row["wbs_disiplin"] = "";
                    row["canonical_v3"] = "";
                }
            }
            ctx.Log($"  assign_wbs_code: {assigned}/{rows.Count} satıra WBS atandı");
            return rows;
        }

        [EgOp("link_quantity_to_wbs",
            Description = "Metraj satırlarını WBS koduna göre gruplar ve toplar",
            Category    = "Veri")]
        public static List<Dictionary<string, object?>> LinkQuantityToWbs(OpContext ctx)
        {
            var rows      = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var qtyField  = ctx.GetString("quantity_field", "hacim_m3");
            var wbsField  = ctx.GetString("wbs_field", "wbs_kodu");
            var result    = rows
                .GroupBy(r => r.TryGetValue(wbsField, out var v) ? v?.ToString() ?? "—" : "—")
                .Select(g =>
                {
                    double total = g.Sum(r =>
                        r.TryGetValue(qtyField, out var v) &&
                        double.TryParse(v?.ToString(), out var d) ? d : 0);
                    return new Dictionary<string, object?>
                    {
                        [wbsField]   = g.Key,
                        ["eleman_sayisi"] = g.Count(),
                        [qtyField]   = Math.Round(total, 3)
                    };
                })
                .OrderBy(r => r[wbsField]?.ToString())
                .ToList();
            ctx.Log($"  link_quantity_to_wbs: {result.Count} WBS grubu");
            return result;
        }

        [EgOp("export_wbs_report",
            Description = "WBS metraj raporunu JSON olarak dışa aktarır. params: output_path",
            Category    = "Veri")]
        public static string ExportWbsReport(OpContext ctx)
        {
            var rows    = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var outPath = ctx.GetString("output_path",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"EGBIMOTO_WBS_{DateTime.Now:yyyyMMdd_HHmm}.json"));
            var json = JsonSerializer.Serialize(rows,
                new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            File.WriteAllText(outPath, json, System.Text.Encoding.UTF8);
            ctx.Log($"  export_wbs_report: {outPath}");
            return outPath;
        }

        // ── Veri dönüştürme ───────────────────────────────────────────────────
        [EgOp("lookup_value",
            Description = "Satır listesindeki params.key_field değerini params.lookup_key ile lookup tablosunda arar ve params.result_field ekler",
            Category    = "Veri")]
        public static List<Dictionary<string, object?>> LookupValue(OpContext ctx)
        {
            var rows        = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var keyField    = ctx.GetString("key_field");
            var lookupKey   = ctx.GetString("lookup_key");
            var resultField = ctx.GetString("result_field", "lookup_result");
            var lookup      = EgbimotoData.Registry.Get(lookupKey)
                as Dictionary<string, object?> ?? new();
            int found = 0;
            foreach (var row in rows)
            {
                var key = row.TryGetValue(keyField, out var v) ? v?.ToString() ?? "" : "";
                if (lookup.TryGetValue(key, out var val)) { row[resultField] = val; found++; }
                else row[resultField] = null;
            }
            ctx.Log($"  lookup_value '{lookupKey}': {found}/{rows.Count} eşleşti");
            return rows;
        }

        [EgOp("pivot_table",
            Description = "Satır listesini params.row_field x params.col_field pivot tablosuna dönüştürür",
            Category    = "Veri")]
        public static List<Dictionary<string, object?>> PivotTable(OpContext ctx)
        {
            var rows     = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var rowField = ctx.RequireString("row_field");
            var colField = ctx.RequireString("col_field");
            var valField = ctx.RequireString("value_field");
            var func     = ctx.GetString("func", "sum").ToLowerInvariant();

            var cols = rows.Select(r => r.TryGetValue(colField, out var v) ? v?.ToString() ?? "" : "")
                .Distinct().OrderBy(c => c).ToList();

            var result = rows
                .GroupBy(r => r.TryGetValue(rowField, out var v) ? v?.ToString() ?? "" : "")
                .Select(g =>
                {
                    var row = new Dictionary<string, object?> { [rowField] = g.Key };
                    foreach (var col in cols)
                    {
                        var vals = g.Where(r =>
                            (r.TryGetValue(colField, out var cv) ? cv?.ToString() : "") == col)
                            .Select(r => r.TryGetValue(valField, out var vv) &&
                                double.TryParse(vv?.ToString(), out var d) ? d : 0).ToList();
                        row[col] = func switch
                        {
                            "count" => (object?)vals.Count,
                            "avg"   => vals.Count > 0 ? Math.Round(vals.Average(), 3) : 0,
                            "max"   => vals.Count > 0 ? vals.Max() : 0,
                            "min"   => vals.Count > 0 ? vals.Min() : 0,
                            _       => Math.Round(vals.Sum(), 3)
                        };
                    }
                    return row;
                }).ToList();
            ctx.Log($"  pivot_table: {result.Count} satır x {cols.Count} sütun");
            return result;
        }
    }
}
