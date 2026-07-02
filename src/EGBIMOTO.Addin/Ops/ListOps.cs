using System;
using System.Collections.Generic;
using System.Linq;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Liste İşlemleri (ListOps)
    /// Dynamo List node'larının EGBIMOTO karşılığı.
    /// List<object?> üzerinde çalışır → Element, XYZ, Dict, string hepsi kabul edilir.
    ///
    ///   list_map           — Her elemana şablon/kural uygula
    ///   list_zip           — İki listeyi eleman bazlı birleştir
    ///   list_cross_product — Kartezyen çarpım
    ///   list_flatten       — İç içe listeyi düzleştir (1 seviye)
    ///   list_group_by_key  — Dict listesini alan değerine göre grupla
    ///   list_sort_by       — Dict listesini alana göre sırala
    ///   list_filter_by_rule— Koşullu filtrele
    ///   list_take_every_n  — Her N. elemanı al
    ///   list_transpose     — Satır/sütun transpoze
    /// </summary>
    public static class ListOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // L01  list_map
        //
        // input : List<object?>
        // params: field       String  — Dict girdi için kaynak alan
        //         template    String  — "{field1} - {field2}" format şablonu
        //         output_field String — çıktı alan adı (Dict için)
        // output: List<object?>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_map",
            Description = "Her elemana template veya field dönüşümü uygular. " +
                          "Dict listesi için: template='{Mark} - {Level}', output_field='etiket'",
            Category    = "Liste")]
        public static List<object?> ListMap(OpContext ctx)
        {
            var list       = ctx.InputAs<List<object?>>();
            var template   = ctx.GetString("template",     "");
            var field      = ctx.GetString("field",        "");
            var outField   = ctx.GetString("output_field", "_mapped");

            var result = new List<object?>();

            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> dict)
                {
                    var copy = new Dictionary<string, object?>(dict);

                    if (!string.IsNullOrEmpty(template))
                    {
                        // {field_name} → değerle değiştir
                        string mapped = template;
                        foreach (var kv in dict)
                            mapped = mapped.Replace($"{{{kv.Key}}}", kv.Value?.ToString() ?? "");
                        copy[outField] = mapped;
                    }
                    else if (!string.IsNullOrEmpty(field) && dict.TryGetValue(field, out var v))
                    {
                        copy[outField] = v;
                    }

                    result.Add(copy);
                }
                else
                {
                    // Skaler → template'e {value} olarak geç
                    string mapped = template.Replace("{value}", item?.ToString() ?? "");
                    result.Add(string.IsNullOrEmpty(template) ? item : (object?)mapped);
                }
            }

            ctx.Log($"  list_map: {list.Count} → {result.Count} eleman");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // L02  list_zip
        //
        // input : List<object?>  (birinci liste)
        // params: second_key  String  — vars'taki ikinci listenin step_id'si
        // output: List<List<object?>>  — [[a1,b1],[a2,b2],...]
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_zip",
            Description = "İki listeyi eleman bazlı çiftler olarak birleştirir. " +
                          "params.second_key: manifest'teki ikinci listenin step_id'si.",
            Category    = "Liste")]
        public static List<object?> ListZip(OpContext ctx)
        {
            var listA = ctx.InputAs<List<object?>>();
            var secondKey = ctx.RequireString("second_key");

            // İkinci listeyi Vars'tan al
            List<object?> listB = new();
            if (ctx.Vars.TryGetValue(secondKey, out var raw) && raw is List<object?> lb)
                listB = lb;
            else if (ctx.Vars.TryGetValue(secondKey, out var raw2))
                listB = raw2 is System.Collections.IEnumerable en
                    ? en.Cast<object?>().ToList()
                    : new List<object?> { raw2 };

            int count  = Math.Min(listA.Count, listB.Count);
            var result = new List<object?>();

            for (int i = 0; i < count; i++)
                result.Add(new List<object?> { listA[i], listB[i] });

            ctx.Log($"  list_zip: {listA.Count}+{listB.Count} → {count} çift");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // L03  list_cross_product
        //
        // input : List<object?>  (A)
        // params: second_key  String
        // output: List<List<object?>>  [[a1,b1],[a1,b2],...,[a2,b1],...]
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_cross_product",
            Description = "İki listenin kartezyen çarpımını üretir. " +
                          "A=[1,2], B=[a,b] → [[1,a],[1,b],[2,a],[2,b]]",
            Category    = "Liste")]
        public static List<object?> ListCrossProduct(OpContext ctx)
        {
            var listA     = ctx.InputAs<List<object?>>();
            var secondKey = ctx.RequireString("second_key");

            List<object?> listB = new();
            if (ctx.Vars.TryGetValue(secondKey, out var raw) && raw is List<object?> lb)
                listB = lb;

            var result = new List<object?>();
            foreach (var a in listA)
                foreach (var b in listB)
                    result.Add(new List<object?> { a, b });

            ctx.Log($"  list_cross_product: {listA.Count}×{listB.Count} → {result.Count} çift");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // L04  list_flatten
        //
        // input : List<object?>  (iç içe liste)
        // params: levels  Int  default=1 (1 = bir seviye)
        // output: List<object?>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_flatten",
            Description = "İç içe listeyi düzleştirir. levels=1 bir seviye açar.",
            Category    = "Liste")]
        public static List<object?> ListFlatten(OpContext ctx)
        {
            var list   = ctx.InputAs<List<object?>>();
            int levels = ctx.GetInt("levels", 1);

            var result = FlattenRecursive(list, levels);
            ctx.Log($"  list_flatten: {list.Count} → {result.Count} eleman ({levels} seviye)");
            return result;
        }

        private static bool TryCompare(string sv, string value, Func<double, double, bool> cmp)
            => double.TryParse(sv,    System.Globalization.NumberStyles.Any,
                                      System.Globalization.CultureInfo.InvariantCulture, out var a)
            && double.TryParse(value, System.Globalization.NumberStyles.Any,
                                      System.Globalization.CultureInfo.InvariantCulture, out var b)
            && cmp(a, b);

        private static List<object?> FlattenRecursive(List<object?> list, int depth)
        {
            if (depth <= 0) return list;
            var result = new List<object?>();
            foreach (var item in list)
            {
                if (item is List<object?> inner)
                    result.AddRange(FlattenRecursive(inner, depth - 1));
                else if (item is System.Collections.IEnumerable en && item is not string)
                    result.AddRange(FlattenRecursive(en.Cast<object?>().ToList(), depth - 1));
                else
                    result.Add(item);
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // L05  list_group_by_key
        //
        // input : List<object?>  (Dict listesi)
        // params: key_field  String  zorunlu
        // output: List<object?>  (her eleman: {key, items: List<object?>})
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_group_by_key",
            Description = "Dict listesini params.key_field alanına göre gruplar. " +
                          "Çıktı: [{key_value, items: [...]}]",
            Category    = "Liste")]
        public static List<object?> ListGroupByKey(OpContext ctx)
        {
            var list     = ctx.InputAs<List<object?>>();
            var keyField = ctx.RequireString("key_field");

            var groups = new Dictionary<string, List<object?>>();

            foreach (var item in list)
            {
                string groupKey = "";
                if (item is Dictionary<string, object?> dict &&
                    dict.TryGetValue(keyField, out var kv))
                    groupKey = kv?.ToString() ?? "(null)";

                if (!groups.ContainsKey(groupKey))
                    groups[groupKey] = new List<object?>();
                groups[groupKey].Add(item);
            }

            var result = groups
                .Select(g => (object?)new Dictionary<string, object?>
                {
                    [keyField] = g.Key,
                    ["items"]  = g.Value,
                    ["count"]  = g.Value.Count,
                })
                .ToList();

            ctx.Log($"  list_group_by_key: {list.Count} → {result.Count} grup");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // L06  list_sort_by
        //
        // input : List<object?>
        // params: sort_field  String  zorunlu
        //         ascending   Bool    default=true
        // output: List<object?>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_sort_by",
            Description = "Dict listesini params.sort_field alanına göre sıralar.",
            Category    = "Liste")]
        public static List<object?> ListSortBy(OpContext ctx)
        {
            var list      = ctx.InputAs<List<object?>>();
            var sortField = ctx.RequireString("sort_field");
            bool asc      = ctx.GetBool("ascending", true);

            IOrderedEnumerable<object?> ordered;
            if (asc)
                ordered = list.OrderBy(item => GetSortKey(item, sortField));
            else
                ordered = list.OrderByDescending(item => GetSortKey(item, sortField));

            var result = ordered.ToList();
            ctx.Log($"  list_sort_by: {list.Count} eleman, alan='{sortField}', asc={asc}");
            return result;
        }

        private static IComparable GetSortKey(object? item, string field)
        {
            if (item is Dictionary<string, object?> dict &&
                dict.TryGetValue(field, out var v))
            {
                if (v is double d) return d;
                if (v is int    i) return i;
                if (v is long   l) return l;
                return v?.ToString() ?? "";
            }
            return item?.ToString() ?? "";
        }

        // ─────────────────────────────────────────────────────────────────────
        // L07  list_filter_by_rule
        //
        // input : List<object?>
        // params: field    String  zorunlu
        //         operator String  zorunlu (eq|ne|gt|lt|gte|lte|contains|starts_with)
        //         value    String  zorunlu
        // output: List<object?>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_filter_by_rule",
            Description = "List<object?> içindeki Dict elemanlarını field/operator/value kuralıyla filtreler.",
            Category    = "Liste")]
        public static List<object?> ListFilterByRule(OpContext ctx)
        {
            var list     = ctx.InputAs<List<object?>>();
            var field    = ctx.RequireString("field");
            var op       = ctx.GetString("operator", "eq").ToLowerInvariant();
            var value    = ctx.GetString("value", "");

            var result = list.Where(item =>
            {
                if (item is not Dictionary<string, object?> dict) return false;
                if (!dict.TryGetValue(field, out var fv)) return false;
                string sv = fv?.ToString() ?? "";

                return op switch
                {
                    "eq"          => sv.Equals(value, StringComparison.OrdinalIgnoreCase),
                    "ne"          => !sv.Equals(value, StringComparison.OrdinalIgnoreCase),
                    "contains"    => sv.Contains(value, StringComparison.OrdinalIgnoreCase),
                    "starts_with" => sv.StartsWith(value, StringComparison.OrdinalIgnoreCase),
                    "gt"          => TryCompare(sv, value, (x, y) => x > y),
                    "lt"          => TryCompare(sv, value, (x, y) => x < y),
                    "gte"         => TryCompare(sv, value, (x, y) => x >= y),
                    "lte"         => TryCompare(sv, value, (x, y) => x <= y),
                    _             => false,
                };
            }).ToList();

            ctx.Log($"  list_filter_by_rule: {list.Count} → {result.Count} eleman");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // L08  list_take_every_n
        //
        // input : List<object?>
        // params: n       Int  zorunlu (her N. eleman)
        //         offset  Int  default=0 (başlangıç indeksi)
        // output: List<object?>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_take_every_n",
            Description = "Listeden her N. elemanı alır. n=3, offset=0 → [0,3,6,9,...]",
            Category    = "Liste")]
        public static List<object?> ListTakeEveryN(OpContext ctx)
        {
            var list   = ctx.InputAs<List<object?>>();
            int n      = Math.Max(1, ctx.GetInt("n", 2));
            int offset = ctx.GetInt("offset", 0);

            var result = new List<object?>();
            for (int i = offset; i < list.Count; i += n)
                result.Add(list[i]);

            ctx.Log($"  list_take_every_n: {list.Count} → {result.Count} eleman (n={n})");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // L09  list_transpose
        //
        // input : List<object?>  (her eleman bir List<object?> satır)
        // output: List<object?>  (satır/sütun yer değiştirilmiş)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("list_transpose",
            Description = "İç içe listeyi transpoze eder. [[1,2],[3,4]] → [[1,3],[2,4]]",
            Category    = "Liste")]
        public static List<object?> ListTranspose(OpContext ctx)
        {
            var list = ctx.InputAs<List<object?>>();
            if (list.Count == 0) return new List<object?>();

            // İç listelere dönüştür
            var rows = list
                .Select(item => item is List<object?> inner ? inner
                    : item is System.Collections.IEnumerable en && item is not string
                        ? en.Cast<object?>().ToList()
                        : new List<object?> { item })
                .ToList();

            int cols = rows.Max(r => r.Count);
            var result = new List<object?>();

            for (int c = 0; c < cols; c++)
            {
                var col = new List<object?>();
                foreach (var row in rows)
                    col.Add(c < row.Count ? row[c] : null);
                result.Add(col);
            }

            ctx.Log($"  list_transpose: {rows.Count}×{cols} → {cols}×{rows.Count}");
            return result;
        }
    }
}
