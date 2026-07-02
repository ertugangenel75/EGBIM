using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Revit genel yardımcı op'ları.
    /// Toplama → CollectionOps.cs
    /// Parametre okuma/yazma → ParamOps.cs (read_param, write_param)
    ///
    /// FIX #6:
    ///   get_param_value → kaldırıldı (read_param ile duplicate, ParamOps'ta tam versiyon var)
    ///   set_param_value → kaldırıldı (write_param ile duplicate; write_param int/double da destekler)
    ///   param_exists    → burada kalıyor (özet dict döner, ParamOps.param_exists_check ValidationReport döner — farklı)
    /// </summary>
    public static class RevitOps
    {
        // ── Parametre ─────────────────────────────────────────────────────────

        [EgOp("param_exists",
            Description = "Elemanlarda params.param_name parametresinin varlığını özet dict olarak döner. {found, missing, param_name}",
            Category    = "Parametre")]
        public static Dictionary<string, object?> ParamExists(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramName = ctx.GetString("param_name");
            int found = 0, missing = 0;
            foreach (var el in elements)
            {
                if (el.LookupParameter(paramName) is not null) found++; else missing++;
            }
            ctx.Log($"  '{paramName}': {found} var, {missing} eksik");
            return new() { ["found"] = found, ["missing"] = missing, ["param_name"] = paramName };
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────

        [EgOp("element_count",
            Description = "Input eleman listesinin sayısını döner",
            Category    = "Yardımcı")]
        public static int ElementCount(OpContext ctx)
        {
            var count = (ctx.Input as System.Collections.ICollection)?.Count ?? 0;
            ctx.Log($"  Eleman sayısı: {count}");
            return count;
        }

        [EgOp("log_message",
            Description = "params.message metnini log'a yazar, input'u geçirir",
            Category    = "Yardımcı")]
        public static object? LogMessage(OpContext ctx)
        {
            ctx.Log(ctx.GetString("message", "—"));
            return ctx.Input;
        }

        [EgOp("pass_through",
            Description = "Input'u değiştirmeden geçirir (debug/bağlantı için)",
            Category    = "Yardımcı")]
        public static object? PassThrough(OpContext ctx) => ctx.Input;
    }
}
