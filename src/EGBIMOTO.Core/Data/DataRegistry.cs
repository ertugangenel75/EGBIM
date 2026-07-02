using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EGBIMOTO.Core.Data
{
    /// <summary>
    /// Runtime veri yükleyici.
    /// JSON/CSV dosyalarını okur, normalize eder, cache'ler.
    ///
    /// Kullanım:
    ///   var dr = new DataRegistry();
    ///   dr.SetBasePath(addinDir + "/data");
    ///   var poz = dr.LoadCsbPoz2026();
    /// </summary>
    public sealed class DataRegistry
    {
        private readonly Dictionary<string, object?> _cache = new(StringComparer.OrdinalIgnoreCase);
        private string _basePath = "";

        public void SetBasePath(string path) => _basePath = path ?? "";
        public string BasePath => _basePath;

        // ── NormalizeRows ─────────────────────────────────────────────────────
        public static List<Dictionary<string, object?>> NormalizeRows(object? raw, params string[] extraKeys)
        {
            if (raw is null) return new();
            if (raw is List<Dictionary<string, object?>> list) return list;
            if (raw is IEnumerable<object> oe)
                return oe.OfType<Dictionary<string, object?>>().ToList();
            if (raw is Dictionary<string, object?> dict)
            {
                var candidates = new[] {
                    "rows","items","data","rules","matrix","classes","mapping",
                    "pozlar","parametreler","kalemler","records","results","list"
                }.Concat(extraKeys).Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var key in candidates)
                {
                    if (!dict.TryGetValue(key, out var val) || val is null) continue;
                    if (val is List<Dictionary<string, object?>> ld) return ld;
                    if (val is List<object> lo) return lo.OfType<Dictionary<string, object?>>().ToList();
                }
                return new List<Dictionary<string, object?>> { dict };
            }
            return new();
        }

        // ── TryLoad ───────────────────────────────────────────────────────────
        public bool TryLoad(string key, string path, string format, bool required, out object? data)
        {
            if (_cache.TryGetValue(key, out data)) return true;
            var resolved = Path.IsPathRooted(path) ? path : Path.Combine(_basePath, path);
            if (!File.Exists(resolved))
            {
                if (required) throw new FileNotFoundException($"Required data '{key}' missing: {resolved}");
                data = null; return false;
            }
            try
            {
                data = format.ToLowerInvariant() == "csv" ? LoadCsv(resolved) : LoadJson(resolved);
                _cache[key] = data;
                return true;
            }
            catch (Exception ex)
            {
                if (required) throw new InvalidOperationException($"DataRegistry: failed to load '{key}': {ex.Message}", ex);
                data = null; return false;
            }
        }

        public object? Get(string key) => _cache.TryGetValue(key, out var v) ? v : null;
        public List<string> GetKeys() => _cache.Keys.OrderBy(k => k).ToList();
        public void Set(string key, object? value) => _cache[key] = value;
        public bool IsCached(string key) => _cache.ContainsKey(key);

        // ── Özel yükleyiciler ─────────────────────────────────────────────────
        public List<Dictionary<string, object?>> LoadCsbPoz2026()
        {
            const string key = "csb_poz_2026";
            if (_cache.TryGetValue(key, out var c) && c is List<Dictionary<string, object?>> cached) return cached;
            var path = Path.Combine(_basePath, "poz", "csb_poz_2026.json");
            if (!File.Exists(path)) return new();
            var raw = LoadJson(path);
            List<Dictionary<string, object?>> result;
            if (raw is Dictionary<string, object?> dict)
            {
                result = new();
                foreach (var kv in dict)
                {
                    if (kv.Key.StartsWith("_")) continue;
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["poz_no"] = kv.Key };
                    if (kv.Value is Dictionary<string, object?> fields)
                        foreach (var f in fields) row[f.Key] = f.Value;
                    result.Add(row);
                }
            }
            else result = NormalizeRows(raw);
            _cache[key] = result;
            return result;
        }

        public List<Dictionary<string, object?>> LoadQaRuleMatrix()
        {
            const string key = "qa_rule_matrix";
            if (_cache.TryGetValue(key, out var c) && c is List<Dictionary<string, object?>> cached) return cached;
            var path = Path.Combine(_basePath, "semantic", "qa_rule_matrix.json");
            if (!File.Exists(path)) return new();
            var raw = LoadJson(path);
            var result = NormalizeRows(raw, "rules", "matrix");
            _cache[key] = result;
            return result;
        }

        public List<Dictionary<string, object?>> LoadSemanticClasses()
        {
            const string key = "semantic_classes";
            if (_cache.TryGetValue(key, out var c) && c is List<Dictionary<string, object?>> cached) return cached;
            var path = Path.Combine(_basePath, "semantic", "semantic_classes.json");
            if (!File.Exists(path)) return new();
            var raw = LoadJson(path);
            var result = NormalizeRows(raw, "classes");
            _cache[key] = result;
            return result;
        }

        public List<Dictionary<string, object?>> LoadWbsMapping()
        {
            const string key = "revit_kategori_wbs";
            if (_cache.TryGetValue(key, out var c) && c is List<Dictionary<string, object?>> cached) return cached;
            var path = Path.Combine(_basePath, "mapping", "revit_kategori_wbs.json");
            if (!File.Exists(path)) return new();
            var raw = LoadJson(path);
            var result = NormalizeRows(raw, "mapping");
            _cache[key] = result;
            return result;
        }

        // ── JSON / CSV yükleyici ──────────────────────────────────────────────
        private static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static object? LoadJson(string path)
        {
            var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            return Unwrap(doc.RootElement);
        }

        private static object? Unwrap(JsonElement je) => je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number => je.TryGetInt64(out var l) ? (object?)l : je.GetDouble(),
            JsonValueKind.True   => true,
            JsonValueKind.False  => false,
            JsonValueKind.Null   => null,
            JsonValueKind.Array  => je.EnumerateArray().Select(Unwrap).ToList(),
            JsonValueKind.Object => je.EnumerateObject()
                .ToDictionary(p => p.Name, p => Unwrap(p.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => null
        };

        private static List<Dictionary<string, object?>> LoadCsv(string path)
        {
            var lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
            if (lines.Length < 2) return new();
            var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
            return lines.Skip(1)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l =>
                {
                    var cols = l.Split(',');
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < headers.Length; i++)
                        row[headers[i]] = i < cols.Length ? cols[i].Trim() : null;
                    return row;
                }).ToList();
        }
    }
}
