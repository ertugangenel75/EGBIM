using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Eleman ve satır filtreleme, gruplama, sıralama, dönüştürme op'ları.
    /// </summary>
    public static class FilterOps
    {
        // ── Eleman filtre ─────────────────────────────────────────────────────
        [EgOp("filter_by_level",
            Description = "Elemanları params.level_name kattaki elemanlarla filtreler",
            Category    = "Filtre")]
        public static List<Element> FilterByLevel(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var levelName = ctx.RequireString("level_name").Trim();
            var level     = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(Level)).Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
            if (level is null) { ctx.Log($"  ⚠ Kat bulunamadı: '{levelName}'"); return new(); }
            var result = elements.Where(e => e.LevelId == level.Id).ToList();
            ctx.Log($"  filter_by_level '{levelName}': {result.Count}/{elements.Count}");
            return result;
        }

        [EgOp("filter_by_level_range",
            Description = "Elemanları params.min_level ile params.max_level arasındaki katlara göre filtreler",
            Category    = "Filtre")]
        public static List<Element> FilterByLevelRange(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var minName  = ctx.GetString("min_level");
            var maxName  = ctx.GetString("max_level");
            var levels   = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => l.Elevation).ToList();
            var minLvl = levels.FirstOrDefault(l => l.Name.Equals(minName, StringComparison.OrdinalIgnoreCase));
            var maxLvl = levels.FirstOrDefault(l => l.Name.Equals(maxName, StringComparison.OrdinalIgnoreCase));
            if (minLvl is null || maxLvl is null)
            {
                ctx.Log($"  ⚠ Kat bulunamadı: '{minName}' veya '{maxName}'");
                return new();
            }
            var validIds = levels
                .Where(l => l.Elevation >= minLvl.Elevation && l.Elevation <= maxLvl.Elevation)
                .Select(l => l.Id).ToHashSet();
            var result = elements.Where(e => validIds.Contains(e.LevelId)).ToList();
            ctx.Log($"  filter_by_level_range [{minName}..{maxName}]: {result.Count}/{elements.Count}");
            return result;
        }

        [EgOp("filter_by_param",
            Description = "Elemanları params.param_name == params.value koşuluyla filtreler. operator: equals|contains|not_equals|starts_with|gt|lt",
            Category    = "Filtre")]
        public static List<Element> FilterByParam(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            var value     = ctx.GetString("value");
            var op        = ctx.GetString("operator", "equals").ToLowerInvariant();
            var result = elements.Where(e =>
            {
                var p = e.LookupParameter(paramName);
                if (p is null) return false;
                var val = p.StorageType switch
                {
                    StorageType.String  => p.AsString() ?? "",
                    StorageType.Integer => p.AsInteger().ToString(),
                    StorageType.Double  => p.AsDouble().ToString("F4"),
                    _                   => ""
                };
                return op switch
                {
                    "contains"    => val.Contains(value, StringComparison.OrdinalIgnoreCase),
                    "not_equals"  => !val.Equals(value, StringComparison.OrdinalIgnoreCase),
                    "starts_with" => val.StartsWith(value, StringComparison.OrdinalIgnoreCase),
                    "gt"          => double.TryParse(val, out var a) && double.TryParse(value, out var b) && a > b,
                    "lt"          => double.TryParse(val, out var c) && double.TryParse(value, out var d) && c < d,
                    _             => val.Equals(value, StringComparison.OrdinalIgnoreCase)
                };
            }).ToList();
            ctx.Log($"  filter_by_param '{paramName}' {op} '{value}': {result.Count}/{elements.Count}");
            return result;
        }

        [EgOp("filter_by_type",
            Description = "Elemanları tip adı params.type_name içerenlere göre filtreler",
            Category    = "Filtre")]
        public static List<Element> FilterByType(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var typeName = ctx.GetString("type_name").ToLowerInvariant();
            var result = elements.Where(e =>
            {
                var t = rctx.Doc.GetElement(e.GetTypeId()) as ElementType;
                return t?.Name.ToLowerInvariant().Contains(typeName) == true;
            }).ToList();
            ctx.Log($"  filter_by_type '{typeName}': {result.Count}/{elements.Count}");
            return result;
        }

        [EgOp("filter_by_category",
            Description = "Eleman listesini params.category kategorisine göre filtreler",
            Category    = "Filtre")]
        public static List<Element> FilterByCategory(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var catName  = ctx.GetString("category");
            var result   = elements.Where(e =>
                e.Category?.Name.Equals(catName, StringComparison.OrdinalIgnoreCase) == true ||
                e.Category?.BuiltInCategory.ToString().Equals(catName, StringComparison.OrdinalIgnoreCase) == true
            ).ToList();
            ctx.Log($"  filter_by_category '{catName}': {result.Count}/{elements.Count}");
            return result;
        }

        [EgOp("filter_by_workset",
            Description = "Eleman listesini params.workset_name workset'ine göre filtreler",
            Category    = "Filtre")]
        public static List<Element> FilterByWorkset(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements    = ctx.InputAsOrDefault<List<Element>>();
            var worksetName = ctx.GetString("workset_name").ToLowerInvariant();
            if (!rctx.Doc.IsWorkshared) { ctx.Log("  ⚠ Model workshared değil"); return elements; }
            var wsTable = rctx.Doc.GetWorksetTable();
            var wsIds   = new Autodesk.Revit.DB.FilteredWorksetCollector(rctx.Doc)
                .OfKind(WorksetKind.UserWorkset)
                .Where(ws => ws.Name.ToLowerInvariant().Contains(worksetName))
                .Select(ws => ws.Id)
                .ToHashSet();
            var result = elements.Where(e => wsIds.Contains(e.WorksetId)).ToList();
            ctx.Log($"  filter_by_workset '{worksetName}': {result.Count}/{elements.Count}");
            return result;
        }

        [EgOp("filter_not_empty_param",
            Description = "params.param_name parametresi dolu olan elemanları döner",
            Category    = "Filtre")]
        public static List<Element> FilterNotEmptyParam(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            var result = elements.Where(e =>
            {
                var p = e.LookupParameter(paramName);
                if (p is null) return false;
                return p.StorageType switch
                {
                    StorageType.String  => !string.IsNullOrWhiteSpace(p.AsString()),
                    StorageType.Integer => true,
                    StorageType.Double  => true,
                    _                   => false
                };
            }).ToList();
            ctx.Log($"  filter_not_empty_param '{paramName}': {result.Count}/{elements.Count}");
            return result;
        }

        [EgOp("filter_empty_param",
            Description = "params.param_name parametresi boş olan elemanları döner",
            Category    = "Filtre")]
        public static List<Element> FilterEmptyParam(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            var result = elements.Where(e =>
            {
                var p = e.LookupParameter(paramName);
                if (p is null) return true;
                return p.StorageType switch
                {
                    StorageType.String  => string.IsNullOrWhiteSpace(p.AsString()),
                    _                   => false
                };
            }).ToList();
            ctx.Log($"  filter_empty_param '{paramName}': {result.Count}/{elements.Count}");
            return result;
        }

        // ── Satır filtre ──────────────────────────────────────────────────────
        [EgOp("filter_rows",
            Description = "Dict listesini params.field operator params.value koşuluyla filtreler. operator: eq|contains|gt|lt|not_eq|starts_with",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> FilterRows(OpContext ctx)
        {
            var rows  = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var field = ctx.RequireString("field");
            var value = ctx.GetString("value");
            var op    = ctx.GetString("operator", "eq").ToLowerInvariant();
            var result = rows.Where(r =>
            {
                var s = r.TryGetValue(field, out var v) ? v?.ToString() ?? "" : "";
                return op switch
                {
                    "contains"    => s.Contains(value, StringComparison.OrdinalIgnoreCase),
                    "not_eq"      => !s.Equals(value, StringComparison.OrdinalIgnoreCase),
                    "starts_with" => s.StartsWith(value, StringComparison.OrdinalIgnoreCase),
                    "gt"          => double.TryParse(s, out var a) && double.TryParse(value, out var b) && a > b,
                    "lt"          => double.TryParse(s, out var c) && double.TryParse(value, out var d) && c < d,
                    _             => s.Equals(value, StringComparison.OrdinalIgnoreCase)
                };
            }).ToList();
            ctx.Log($"  filter_rows '{field}': {result.Count}/{rows.Count}");
            return result;
        }

        [EgOp("filter_rows_multi",
            Description = "Dict listesini birden fazla koşulla filtreler. params: conditions [{field,operator,value}]",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> FilterRowsMulti(OpContext ctx)
        {
            var rows       = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var conditions = ctx.GetList<Dictionary<string, object?>>("conditions");
            if (conditions is null || conditions.Count == 0) return rows;
            var result = rows.Where(r =>
            {
                foreach (var cond in conditions)
                {
                    if (cond is not Dictionary<string, object?> c) continue;
                    var field = c.TryGetValue("field", out var f) ? f?.ToString() ?? "" : "";
                    var value = c.TryGetValue("value", out var v) ? v?.ToString() ?? "" : "";
                    var op    = c.TryGetValue("operator", out var o) ? o?.ToString() ?? "eq" : "eq";
                    var s     = r.TryGetValue(field, out var rv) ? rv?.ToString() ?? "" : "";
                    bool pass = op.ToLowerInvariant() switch
                    {
                        "contains" => s.Contains(value, StringComparison.OrdinalIgnoreCase),
                        "not_eq"   => !s.Equals(value, StringComparison.OrdinalIgnoreCase),
                        "gt"       => double.TryParse(s, out var a) && double.TryParse(value, out var b) && a > b,
                        "lt"       => double.TryParse(s, out var c2) && double.TryParse(value, out var d) && c2 < d,
                        _          => s.Equals(value, StringComparison.OrdinalIgnoreCase)
                    };
                    if (!pass) return false;
                }
                return true;
            }).ToList();
            ctx.Log($"  filter_rows_multi: {result.Count}/{rows.Count}");
            return result;
        }

        // ── Gruplama ─────────────────────────────────────────────────────────
        [EgOp("group_by",
            Description = "Dict listesini params.field alanına göre gruplar. {key, count, rows} listesi döner",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> GroupBy(OpContext ctx)
        {
            var rows  = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var field = ctx.GetString("field");
            var result = rows
                .GroupBy(r => r.TryGetValue(field, out var v) ? v?.ToString() ?? "" : "")
                .Select(g => new Dictionary<string, object?>
                {
                    ["key"]   = g.Key,
                    ["count"] = g.Count(),
                    ["rows"]  = g.ToList()
                })
                .OrderByDescending(g => (int)g["count"]!)
                .ToList();
            ctx.Log($"  group_by '{field}': {result.Count} grup");
            return result;
        }

        [EgOp("group_elements_by_level",
            Description = "Eleman listesini katlara göre gruplar. {level, count, elements} listesi döner",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> GroupElementsByLevel(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var result = elements
                .GroupBy(e => (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "—")
                .Select(g => new Dictionary<string, object?>
                {
                    ["level"]    = g.Key,
                    ["count"]    = g.Count(),
                    ["elements"] = g.ToList()
                })
                .OrderBy(g => g["level"]?.ToString())
                .ToList();
            ctx.Log($"  group_by_level: {result.Count} kat");
            return result;
        }

        [EgOp("group_elements_by_type",
            Description = "Eleman listesini tip adına göre gruplar",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> GroupElementsByType(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements
                .GroupBy(e => (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "—")
                .Select(g => new Dictionary<string, object?>
                {
                    ["type"]     = g.Key,
                    ["count"]    = g.Count(),
                    ["elements"] = g.ToList()
                })
                .OrderByDescending(g => (int)g["count"]!)
                .ToList();
        }

        [EgOp("group_elements_by_category",
            Description = "Eleman listesini Revit kategorisine göre gruplar",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> GroupElementsByCategory(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements
                .GroupBy(e => e.Category?.Name ?? "—")
                .Select(g => new Dictionary<string, object?>
                {
                    ["kategori"] = g.Key,
                    ["count"]    = g.Count(),
                    ["elements"] = g.ToList()
                })
                .OrderByDescending(g => (int)g["count"]!)
                .ToList();
        }

        // ── Sıralama ─────────────────────────────────────────────────────────
        [EgOp("sort_rows",
            Description = "Dict listesini params.field alanına göre sıralar. params.descending=true ile ters",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> SortRows(OpContext ctx)
        {
            var rows       = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var field      = ctx.GetString("field");
            var descending = ctx.GetBool("descending", false);
            bool isNumeric = rows.Any() &&
                double.TryParse(rows[0].TryGetValue(field, out var sv) ? sv?.ToString() : null, out _);
            return (descending
                ? (isNumeric
                    ? (IEnumerable<Dictionary<string,object?>>)rows.OrderByDescending(r => Dbl(r, field))
                    : rows.OrderByDescending(r => Str(r, field)))
                : (isNumeric
                    ? rows.OrderBy(r => Dbl(r, field))
                    : (IEnumerable<Dictionary<string,object?>>)rows.OrderBy(r => Str(r, field)))).ToList();
        }

        // ── Birleştirme ───────────────────────────────────────────────────────
        [EgOp("merge_lists",
            Description = "Eleman listelerini birleştirir. inputs.lists veya from_many desteklenir.",
            Category    = "Filtre")]
        public static List<Element> MergeLists(OpContext ctx)
        {
            var result = new List<Element>();

            // inputs.lists: ["$a","$b"] — manifest üzerinden
            if (ctx.Params.TryGetValue("lists", out var listsRaw))
            {
                IEnumerable<string?> keys = listsRaw switch
                {
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Array
                        => je.EnumerateArray().Select(e => e.GetString()),
                    System.Collections.Generic.List<object?> lo
                        => lo.Select(o => o?.ToString()),
                    _ => Enumerable.Empty<string?>()
                };
                foreach (var rawKey in keys)
                {
                    var key = rawKey?.TrimStart('$');
                    if (key != null && ctx.Vars.TryGetValue(key, out var val) && val is List<Element> el)
                        result.AddRange(el);
                }
                ctx.Log($"  merge_lists: {result.Count} eleman");
                return result;
            }

            // from_many — ctx.Input = List<object?>
            if (ctx.Input is List<object?> multi)
            {
                foreach (var item in multi)
                    if (item is List<Element> list) result.AddRange(list);
                ctx.Log($"  merge_lists: {result.Count} eleman");
                return result;
            }

            return ctx.InputAsOrDefault<List<Element>>();
        }

        [EgOp("merge_rows",
            Description = "Dict listelerini birleştirir. inputs.lists ile veya from_many ile kullanılabilir.",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> MergeRows(OpContext ctx)
        {
            var result = new List<Dictionary<string, object?>>();

            // inputs.lists: ["$a", "$b", "$c"] — manifest üzerinden çoklu giriş
            if (ctx.Params.TryGetValue("lists", out var listsRaw) && listsRaw is System.Text.Json.JsonElement je
                && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in je.EnumerateArray())
                {
                    var key = el.GetString()?.TrimStart('$');
                    if (key is not null && ctx.Vars.TryGetValue(key, out var val)
                        && val is List<Dictionary<string, object?>> rows)
                        result.AddRange(rows);
                }
                ctx.Log($"  merge_rows: {result.Count} satır");
                return result;
            }

            // from_many — ctx.Input = List<object?>
            if (ctx.Input is List<object?> multi)
            {
                foreach (var item in multi)
                    if (item is List<Dictionary<string, object?>> rows) result.AddRange(rows);
                ctx.Log($"  merge_rows: {result.Count} satır");
                return result;
            }

            // Tek giriş
            return ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
        }

        [EgOp("join_rows",
            Description = "İki satır listesini params.key_field alanına göre LEFT JOIN yapar",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> JoinRows(OpContext ctx)
        {
            if (ctx.Input is not List<object?> multi || multi.Count < 2)
            {
                ctx.Log("  ⚠ join_rows: from_many ile 2 liste gerekli");
                return new();
            }
            var left     = multi[0] as List<Dictionary<string, object?>> ?? new();
            var right    = multi[1] as List<Dictionary<string, object?>> ?? new();
            var keyField = ctx.GetString("key_field");
            var rightMap = right.GroupBy(r => r.TryGetValue(keyField, out var v) ? v?.ToString() ?? "" : "")
                .ToDictionary(g => g.Key, g => g.First());
            var result = left.Select(l =>
            {
                var key  = l.TryGetValue(keyField, out var v) ? v?.ToString() ?? "" : "";
                var merged = new Dictionary<string, object?>(l);
                if (rightMap.TryGetValue(key, out var r))
                    foreach (var kv in r)
                        if (!merged.ContainsKey(kv.Key)) merged[kv.Key] = kv.Value;
                return merged;
            }).ToList();
            ctx.Log($"  join_rows on '{keyField}': {result.Count} satır");
            return result;
        }

        // ── Dönüştürme ────────────────────────────────────────────────────────
        [EgOp("take_n",
            Description = "Listeden ilk params.count elemanı alır",
            Category    = "Filtre")]
        public static object? TakeN(OpContext ctx)
        {
            int n = ctx.GetInt("count", 10);
            if (ctx.Input is List<Element>                     el) return el.Take(n).ToList();
            if (ctx.Input is List<Dictionary<string, object?>> dr) return dr.Take(n).ToList();
            return ctx.Input;
        }

        [EgOp("skip_n",
            Description = "Listeden ilk params.count elemanı atlar",
            Category    = "Filtre")]
        public static object? SkipN(OpContext ctx)
        {
            int n = ctx.GetInt("count", 0);
            if (ctx.Input is List<Element>                     el) return el.Skip(n).ToList();
            if (ctx.Input is List<Dictionary<string, object?>> dr) return dr.Skip(n).ToList();
            return ctx.Input;
        }

        [EgOp("select_columns",
            Description = "Dict listesinden params.fields (virgülle ayrılmış) alanlarını seçer",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> SelectColumns(OpContext ctx)
        {
            var rows   = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var fields = ctx.GetString("fields").Split(',').Select(f => f.Trim()).ToList();
            return rows.Select(r => fields.ToDictionary(f => f, f => r.TryGetValue(f, out var v) ? v : null)).ToList();
        }

        [EgOp("add_column",
            Description = "Dict listesine params.field adında params.value değerli sabit sütun ekler",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> AddColumn(OpContext ctx)
        {
            var rows  = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var field = ctx.GetString("field");
            var value = ctx.GetString("value");
            foreach (var r in rows) r[field] = value;
            ctx.Log($"  add_column '{field}'='{value}': {rows.Count} satır");
            return rows;
        }

        [EgOp("rename_column",
            Description = "Dict listesinde params.from sütununu params.to olarak yeniden adlandırır",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> RenameColumn(OpContext ctx)
        {
            var rows = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var from = ctx.GetString("from");
            var to   = ctx.GetString("to");
            foreach (var r in rows)
            {
                if (r.TryGetValue(from, out var v)) { r[to] = v; r.Remove(from); }
            }
            ctx.Log($"  rename_column '{from}' -> '{to}'");
            return rows;
        }

        [EgOp("elements_to_rows",
            Description = "Eleman listesini dict satır listesine dönüştürür (element_id, kategori, tip, kat)",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> ElementsToRows(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements.Select(e => new Dictionary<string, object?>
            {
                ["element_id"] = Rv.GetId(e.Id),
                ["kategori"]   = e.Category?.Name ?? "",
                ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? ""
            }).ToList();
        }

        [EgOp("elements_to_rows_with_params",
            Description = "Eleman listesini dict satır listesine dönüştürür + params.param_names (virgülle) parametrelerini ekler",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> ElementsToRowsWithParams(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements   = ctx.InputAsOrDefault<List<Element>>();
            var paramNames = ctx.GetString("param_names").Split(',').Select(p => p.Trim()).ToList();
            return elements.Select(e =>
            {
                var row = new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["kategori"]   = e.Category?.Name ?? "",
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? ""
                };
                foreach (var pn in paramNames)
                {
                    var p = e.LookupParameter(pn);
                    row[pn] = p?.StorageType switch
                    {
                        StorageType.String  => p.AsString(),
                        StorageType.Double  => p.AsDouble(),
                        StorageType.Integer => p.AsInteger(),
                        _                   => null
                    };
                }
                return row;
            }).ToList();
        }

        [EgOp("distinct_rows",
            Description = "Dict listesinden params.field alanına göre tekrar eden satırları kaldırır",
            Category    = "Filtre")]
        public static List<Dictionary<string, object?>> DistinctRows(OpContext ctx)
        {
            var rows  = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var field = ctx.GetString("field");
            var seen  = new HashSet<string>();
            var result = rows.Where(r =>
            {
                var key = r.TryGetValue(field, out var v) ? v?.ToString() ?? "" : "";
                return seen.Add(key);
            }).ToList();
            ctx.Log($"  distinct_rows '{field}': {result.Count}/{rows.Count}");
            return result;
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────
        private static double Dbl(Dictionary<string, object?> d, string k)
            => d.TryGetValue(k, out var v) && double.TryParse(v?.ToString(), out var r) ? r : 0;
        private static string Str(Dictionary<string, object?> d, string k)
            => d.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";
    }
}
