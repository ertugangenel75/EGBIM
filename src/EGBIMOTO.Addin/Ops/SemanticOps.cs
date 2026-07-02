using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Semantic;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Semantik sınıflandırma, disiplin çözümleme ve bSDD haritalama op'ları.
    /// </summary>
    public static class SemanticOps
    {
        [EgOp("resolve_discipline",
            Description = "Revit kategorisinden disiplin çözer (Mimari/Yapısal/MEP). params: category",
            Category    = "Semantik")]
        public static string ResolveDiscipline(OpContext ctx)
        {
            var category = ctx.GetString("category");
            if (string.IsNullOrEmpty(category) && ctx.Input is Element el)
                category = el.Category?.Name ?? "";
            return new CanonicalClassResolver().ResolveDiscipline(category);
        }

        [EgOp("resolve_canonical_class",
            Description = "Revit kategori/tip adından EGBIM canonical class çözer. params: type_name veya category",
            Category    = "Semantik")]
        public static string ResolveCanonicalClass(OpContext ctx)
        {
            var input = ctx.GetString("type_name");
            if (string.IsNullOrEmpty(input)) input = ctx.GetString("category");
            return new CanonicalClassResolver().Resolve(input);
        }

        [EgOp("classify_elements",
            Description = "Eleman listesine canonical_class ve disiplin atar",
            Category    = "Semantik")]
        public static List<Dictionary<string, object?>> ClassifyElements(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var resolver = new CanonicalClassResolver();
            return elements.Select(el =>
            {
                var catName   = el.Category?.Name ?? "";
                var typeName  = (el.Document.GetElement(el.GetTypeId()) as ElementType)?.Name ?? "";
                var canonical = resolver.Resolve(string.IsNullOrEmpty(typeName) ? catName : typeName);
                var discipline = resolver.ResolveDiscipline(
                    el.Category?.BuiltInCategory.ToString() ?? catName);
                return new Dictionary<string, object?>
                {
                    ["element_id"]      = Rv.GetId(el.Id),
                    ["kategori"]        = catName,
                    ["tip"]             = typeName,
                    ["canonical_class"] = canonical,
                    ["disiplin"]        = discipline
                };
            }).ToList();
        }

        [EgOp("classify_by_wbs",
            Description = "Eleman listesini WBS haritalama tablosuna göre sınıflandırır",
            Category    = "Semantik")]
        public static List<Dictionary<string, object?>> ClassifyByWbs(OpContext ctx)
        {
            var elements   = ctx.InputAsOrDefault<List<Element>>();
            var wbsMapping = EgbimotoData.Registry.Get("wbs_mapping")
                as Dictionary<string, object?> ?? new();
            return elements.Select(el =>
            {
                var cat = el.Category?.Name ?? "";
                wbsMapping.TryGetValue(cat, out var wbs);
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(el.Id),
                    ["kategori"]   = cat,
                    ["wbs_kodu"]   = wbs?.ToString() ?? "—"
                };
            }).ToList();
        }

        [EgOp("map_to_ifc",
            Description = "Eleman listesini IFC haritalama tablosuna göre IFC sınıfına eşler",
            Category    = "Semantik")]
        public static List<Dictionary<string, object?>> MapToIfc(OpContext ctx)
        {
            var elements   = ctx.InputAsOrDefault<List<Element>>();
            var ifcMapping = EgbimotoData.Registry.Get("ifc_mapping")
                as Dictionary<string, object?> ?? new();
            return elements.Select(el =>
            {
                var cat = el.Category?.Name ?? "";
                ifcMapping.TryGetValue(cat, out var ifc);
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(el.Id),
                    ["kategori"]   = cat,
                    ["ifc_sinifi"] = ifc?.ToString() ?? "IfcBuildingElement"
                };
            }).ToList();
        }

        [EgOp("assign_egbim_mark",
            Description = "Elemanlara EGBIM_Mark parametresi yazar (canonical_class + element_id). Transaction açar.",
            Category    = "Semantik",
            RequiresTransaction = true)]
        public static int AssignEgbimMark(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var resolver = new CanonicalClassResolver();
            int count    = 0;
            using var scope = new Host.RevitWriteScope(rctx.Doc, "EGBIM_Mark Ata", rctx.IsAtomicMode);
            foreach (var el in elements)
            {
                var p = el.LookupParameter("EGBIM_Mark");
                if (p is null || p.IsReadOnly) continue;
                var cat       = el.Category?.Name ?? "";
                var canonical = resolver.Resolve(cat);
                p.Set($"{canonical}_{Rv.GetId(el.Id)}");
                count++;
            }
            scope.Commit();
            ctx.Log($"  assign_egbim_mark: {count} elemana EGBIM_Mark atandı");
            return count;
        }
    }
}
