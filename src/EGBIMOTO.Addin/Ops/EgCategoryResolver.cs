using System;
using Autodesk.Revit.DB;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// v12 — Yeni op grupları (intersect/view-action/xlsx-roundtrip/dimension)
    /// arasında paylaşılan kategori adı çözümleyici.
    /// PreCheckOps.TryResolveCategory ile aynı semantik (private orada
    /// olduğundan burada bağımsız bir kopya tutulur — DRY ihlali kabul
    /// edilebilir, iki dosya da aynı küçük mantığı taşır).
    /// "OST_Walls" veya "Walls" / "PipeCurves" gibi her iki yazımı da
    /// BuiltInCategory enum değerine çözer.
    /// </summary>
    internal static class EgCategoryResolver
    {
        public static bool TryResolve(string name, out BuiltInCategory bic)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                bic = default;
                return false;
            }

            if (Enum.TryParse(name, true, out bic)) return true;

            var withPrefix = name.StartsWith("OST_", StringComparison.OrdinalIgnoreCase)
                ? name
                : "OST_" + name;

            return Enum.TryParse(withPrefix, true, out bic);
        }
    }
}
