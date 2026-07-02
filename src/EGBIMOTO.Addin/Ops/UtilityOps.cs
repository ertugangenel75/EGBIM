using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    public static class UtilityOps
    {
        [EgOp("set_var",     Description = "params.value sabitini döner. Manifest sabiti tanımlamak için.", Category = "Yardımcı")]
        public static object? SetVar(OpContext ctx) =>
            ctx.Params.TryGetValue("value", out var v) ? v : null;

        [EgOp("compute",
            Description = "Sayısal hesap. Input sayı veya params.a, params.b. op: add|sub|mul|div|percent|round|min|max. func: sum|avg|min|max|count (satır listesi için)",
            Category    = "Yardımcı")]
        public static double Compute(OpContext ctx)
        {
            // Satır listesinden hesap
            if (ctx.Input is List<Dictionary<string, object?>> rows)
            {
                var field = ctx.GetString("field");
                var func  = ctx.GetString("func", "sum").ToLowerInvariant();
                var vals  = rows.Select(r =>
                    r.TryGetValue(field, out var v) && double.TryParse(v?.ToString(), out var d) ? d : 0).ToList();
                double res = func switch
                {
                    "sum"   => vals.Sum(),
                    "avg"   => vals.Count > 0 ? vals.Average() : 0,
                    "min"   => vals.Count > 0 ? vals.Min() : 0,
                    "max"   => vals.Count > 0 ? vals.Max() : 0,
                    "count" => vals.Count,
                    _       => vals.Sum()
                };
                ctx.Log($"  compute({func}, {field}) = {res}");
                return Math.Round(res, 4);
            }

            // Skalar hesap
            double a = ctx.Input is double dv ? dv : ctx.GetDouble("a");
            double b = ctx.GetDouble("b", 1.0);
            var    op= ctx.GetString("op", "mul");
            double result = op switch
            {
                "add"     => a + b,
                "sub"     => a - b,
                "mul"     => a * b,
                "div"     => b != 0 ? a / b : 0,
                "percent" => b != 0 ? (a / b) * 100.0 : 0,
                "round"   => Math.Round(a, (int)b),
                "min"     => Math.Min(a, b),
                "max"     => Math.Max(a, b),
                _         => a * b
            };
            ctx.Log($"  compute: {a} {op} {b} = {result:N4}");
            return Math.Round(result, 4);
        }

        [EgOp("sum_field",
            Description = "Dict listesindeki params.field alanlarını toplar",
            Category    = "Yardımcı")]
        public static double SumField(OpContext ctx)
        {
            var rows  = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var field = ctx.RequireString("field");
            double total = rows.Sum(r =>
                r.TryGetValue(field, out var v) && double.TryParse(v?.ToString(), out var n) ? n : 0);
            ctx.Log($"  sum '{field}' = {total:N3}");
            return Math.Round(total, 3);
        }

        [EgOp("count_items",
            Description = "Herhangi bir liste veya dict'in eleman sayısını döner",
            Category    = "Yardımcı")]
        public static int CountItems(OpContext ctx)
        {
            int count = ctx.Input switch
            {
                List<Element>                     l => l.Count,
                List<Dictionary<string, object?>> l => l.Count,
                System.Collections.ICollection    c => c.Count,
                _ => 0
            };
            ctx.Log($"  count = {count}");
            return count;
        }

        [EgOp("assert_not_empty",
            Description = "Girdi boşsa hata fırlatır. params.message ile özel mesaj.",
            Category    = "Yardımcı")]
        public static object? AssertNotEmpty(OpContext ctx)
        {
            bool empty = ctx.Input switch
            {
                null                              => true,
                List<Element>                     l => l.Count == 0,
                List<Dictionary<string, object?>> l => l.Count == 0,
                string                            s => string.IsNullOrWhiteSpace(s),
                _                                   => false
            };
            if (empty)
                throw new InvalidOperationException(
                    $"assert_not_empty: {ctx.GetString("message", "Beklenen veri bulunamadı.")}");
            return ctx.Input;
        }

        [EgOp("format_number",
            Description = "Sayıyı params.format ile biçimlendirir. Örn: 'N2', 'F3'",
            Category    = "Yardımcı")]
        public static string FormatNumber(OpContext ctx)
        {
            double val  = ctx.Input is double d ? d : ctx.GetDouble("value");
            var format  = ctx.GetString("format", "N2");
            var unit    = ctx.GetString("unit",   "");
            var result  = val.ToString(format);
            return string.IsNullOrEmpty(unit) ? result : $"{result} {unit}";
        }

        [EgOp("model_checksum",
            Description = "Modeldeki eleman sayısını kategoriye göre özetler",
            Category    = "Yardımcı")]
        public static List<Dictionary<string, object?>> ModelChecksum(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var tracked = new (string, BuiltInCategory)[]
            {
                ("Duvarlar",      BuiltInCategory.OST_Walls),
                ("Döşemeler",     BuiltInCategory.OST_Floors),
                ("Kolonlar",      BuiltInCategory.OST_StructuralColumns),
                ("Kirişler",      BuiltInCategory.OST_StructuralFraming),
                ("Temeller",      BuiltInCategory.OST_StructuralFoundation),
                ("Kapılar",       BuiltInCategory.OST_Doors),
                ("Pencereler",    BuiltInCategory.OST_Windows),
                ("Odalar",        BuiltInCategory.OST_Rooms),
                ("Borular",       BuiltInCategory.OST_PipeCurves),
                ("Kanallar",      BuiltInCategory.OST_DuctCurves),
                ("Kablo Tabaları",BuiltInCategory.OST_CableTray),
                ("Donatı",        BuiltInCategory.OST_Rebar),
            };
            return tracked
                .Select(t => new
                {
                    Kat = t.Item1,
                    Say = new FilteredElementCollector(rctx.Doc)
                              .OfCategory(t.Item2).WhereElementIsNotElementType().GetElementCount()
                })
                .Where(x => x.Say > 0)
                .Select(x => new Dictionary<string, object?> { ["kategori"]=x.Kat, ["adet"]=x.Say })
                .ToList();
        }

        [EgOp("flatten_list",
            Description = "İç içe listeleri tek düzey listeye düzleştirir",
            Category    = "Yardımcı")]
        public static List<object?> FlattenList(OpContext ctx)
        {
            var result = new List<object?>();
            FlatRecurse(ctx.Input, result);
            ctx.Log($"  flatten: {result.Count} eleman");
            return result;
        }

        [EgOp("format_message",
            Description = "params.template içindeki {key} yerlerine vars veya params değerlerini koyar",
            Category    = "Yardımcı")]
        public static string FormatMessage(OpContext ctx)
        {
            var result = ctx.GetString("template", "");
            foreach (var kv in ctx.Params)  result = result.Replace($"{{{kv.Key}}}", kv.Value?.ToString() ?? "");
            foreach (var kv in ctx.Vars)    result = result.Replace($"{{{kv.Key}}}", kv.Value?.ToString() ?? "");
            result = result.Replace("{input}", ctx.Input?.ToString() ?? "");
            ctx.Log($"  → {result}");
            return result;
        }

        private static void FlatRecurse(object? item, List<object?> acc)
        {
            if (item is System.Collections.IEnumerable ie && item is not string)
                foreach (var sub in ie) FlatRecurse(sub, acc);
            else acc.Add(item);
        }
    }
}
