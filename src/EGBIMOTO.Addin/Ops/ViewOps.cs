using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Görünüm ve Etiketleme Op'ları (ViewOps)
    ///
    ///   create_view_filter      — System Classification bazlı görünüm filtresi + renk override
    ///   detect_undefined_system — System Type = Undefined elemanları tespit et
    ///   tag_elements            — Elemanlara otomatik tag yerleştir
    ///   check_untagged_elements — Tag'siz elemanları listele
    /// </summary>
    public static class ViewOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // V01  create_view_filter
        //
        // input : List<Element> (View) — filtre uygulanacak görünümler
        // params: filter_name      String  zorunlu   ("EG_Supply_Filter")
        //         categories       String  zorunlu   virgülle: "OST_DuctCurves,OST_DuctFittings"
        //         param_name       String  default="System Classification"
        //         rule_operator    String  default="contains" (contains|equals|begins_with)
        //         rule_value       String  zorunlu   ("Supply")
        //         color_r          Int     default=0
        //         color_g          Int     default=0
        //         color_b          Int     default=255  (mavi = Supply)
        //         fill_pattern     String  default="Solid Fill"
        //         line_weight      Int     default=4
        //         overwrite        Bool    default=false (mevcut filtreni güncelle)
        //
        // output: List<Dict> {view_id, view_name, filter_id, filter_name, applied, status}
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_view_filter",
            RequiresTransaction = true,
            Description = "System Classification bazlı ParameterFilterElement oluşturur ve " +
                          "seçili görünümlere renk override ile uygular. " +
                          "Örn: Supply=Mavi, Exhaust=Yeşil, Return=Kırmızı pipeline.",
            Category    = "Görünüm")]
        public static List<Dictionary<string, object?>> CreateViewFilter(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var views      = ctx.InputAs<List<Element>>();
            var filterName = ctx.RequireString("filter_name");
            var catStr     = ctx.RequireString("categories");
            var paramName  = ctx.GetString("param_name",   "System Classification");
            var ruleOp     = ctx.GetString("rule_operator","contains").ToLowerInvariant();
            var ruleValue  = ctx.RequireString("rule_value");
            int r          = ctx.GetInt("color_r", 0);
            int g          = ctx.GetInt("color_g", 0);
            int b          = ctx.GetInt("color_b", 255);
            int lineWt     = ctx.GetInt("line_weight", 4);
            var fillPat    = ctx.GetString("fill_pattern", "Solid Fill");
            bool overwrite = ctx.GetBool("overwrite", false);

            // ── Kategori ID listesi ───────────────────────────────────────────
            var catIds = ParseCategories(rctx.Doc, catStr);
            if (catIds.Count == 0)
            {
                ctx.Log($"  create_view_filter: Geçerli kategori bulunamadı → '{catStr}'");
                return new List<Dictionary<string, object?>>
                {
                    new() { ["status"] = "ERROR", ["message"] = $"Kategori bulunamadı: {catStr}" }
                };
            }

            using var scope = new RevitWriteScope(rctx.Doc, $"Filtre: {filterName}", rctx.IsAtomicMode);

            // ── Mevcut filtre var mı? ─────────────────────────────────────────
            ParameterFilterElement? filter = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

            if (filter != null && !overwrite)
            {
                ctx.Log($"  create_view_filter: '{filterName}' zaten var, overwrite=false → atlandı");
            }
            else
            {
                // Parametre ID'si bul (System Classification gibi shared/built-in)
                var paramId = GetParameterId(rctx.Doc, paramName, catIds.First());

                // Kural oluştur
                FilterRule? rule = null;
                if (paramId != null && paramId.Value != ElementId.InvalidElementId.Value)
                {
                    rule = ruleOp switch
                    {
                        "equals"      => ParameterFilterRuleFactory.CreateEqualsRule(
                                            Rv.MakeElementId(paramId.Value), ruleValue),  // v6
                        "begins_with" => ParameterFilterRuleFactory.CreateBeginsWithRule(
                                            Rv.MakeElementId(paramId.Value), ruleValue),  // v6
                        _             => ParameterFilterRuleFactory.CreateContainsRule(
                                            Rv.MakeElementId(paramId.Value), ruleValue),  // v6
                    };
                }

                if (rule == null)
                {
                    ctx.Log($"  create_view_filter: '{paramName}' parametresi bulunamadı");
                    return new List<Dictionary<string, object?>>
                    {
                        new() { ["status"] = "ERROR",
                                ["message"] = $"'{paramName}' parametresi modelde bulunamadı." }
                    };
                }

                var elemFilter = new ElementParameterFilter(rule);

                if (filter == null)
                    filter = ParameterFilterElement.Create(rctx.Doc, filterName, catIds, elemFilter);
                else
                    filter.SetElementFilter(elemFilter);
            }

            // ── Override ayarla ───────────────────────────────────────────────
            var color   = new Color((byte)r, (byte)g, (byte)b);
            var patElem = FindFillPattern(rctx.Doc, fillPat);

            var overrideSettings = new OverrideGraphicSettings();
            overrideSettings.SetSurfaceForegroundPatternColor(color);
            overrideSettings.SetSurfaceForegroundPatternVisible(true);
            if (patElem != null)
                overrideSettings.SetSurfaceForegroundPatternId(patElem.Id);
            overrideSettings.SetProjectionLineColor(color);
            overrideSettings.SetProjectionLineWeight(lineWt);

            // ── Her görünüme uygula ───────────────────────────────────────────
            var rows = new List<Dictionary<string, object?>>();

            foreach (var el in views)
            {
                if (el is not View view) continue;
                if (!view.IsValidObject || view.IsTemplate) continue;

                bool applied = false;
                string status = "OK";
                try
                {
                    // Filtre zaten eklenmemişse ekle
                    var existingFilters = view.GetFilters();
                    if (!existingFilters.Contains(filter!.Id))
                        view.AddFilter(filter.Id);

                    view.SetFilterVisibility(filter.Id, true);
                    view.SetFilterOverrides(filter.Id, overrideSettings);
                    applied = true;
                }
                catch (Exception ex)
                {
                    status = $"ERROR: {ex.Message}";
                    ctx.Log($"  create_view_filter: '{view.Name}' — {ex.Message}");
                }

                rows.Add(new Dictionary<string, object?>
                {
                    ["view_id"]     = Rv.IdStr(view.Id),
                    ["view_name"]   = view.Name,
                    ["filter_id"]   = Rv.IdStr(filter!.Id),
                    ["filter_name"] = filterName,
                    ["applied"]     = applied,
                    ["status"]      = status,
                });
            }

            scope.Commit();
            int ok = rows.Count(r2 => (bool?)r2["applied"] == true);
            ctx.Log($"  create_view_filter: '{filterName}' → {ok}/{views.Count} görünüme uygulandı");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // V02  detect_undefined_system
        //
        // input : List<Element>  (Duct / Pipe / CableTray vb.)
        // params: discipline  String  default="all" (hvac|plumbing|electrical|all)
        // output: List<Dict>
        //   element_id, category, family_type, level, system_type, status
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("detect_undefined_system",
            Description = "System Type = Undefined olan MEP elemanlarını tespit eder. " +
                          "Modelleme hatası: fitting/takeoff fiziksel bağlantısı eksik.",
            Category    = "Görünüm")]
        public static List<Dictionary<string, object?>> DetectUndefinedSystem(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var elements   = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var discipline = ctx.GetString("discipline", "all").ToLowerInvariant();

            // Input boşsa tüm MEP kategorilerini tara
            if (elements.Count == 0)
            {
                var cats = GetMepCategories(discipline);
                foreach (var cat in cats)
                {
                    elements.AddRange(
                        new FilteredElementCollector(rctx.Doc)
                            .OfCategory(cat)
                            .WhereElementIsNotElementType()
                            .ToElements());
                }
            }

            var rows = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                // System Type parametresi
                string sysType = "";
                var sp = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM)
                      ?? el.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)
                      ?? el.LookupParameter("System Type")
                      ?? el.LookupParameter("System Classification");

                if (sp != null)
                    sysType = sp.AsString() ?? sp.AsValueString() ?? "";

                bool isUndefined = string.IsNullOrEmpty(sysType)
                    || sysType.Equals("Undefined", StringComparison.OrdinalIgnoreCase)
                    || sysType.Equals("Tanımsız",  StringComparison.OrdinalIgnoreCase);

                if (!isUndefined) continue;

                string catName  = el.Category?.Name ?? "";
                string typeName = (el.Document.GetElement(el.GetTypeId()) as ElementType)?.Name ?? "";
                string level    = (el.Document.GetElement(
                    el.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)?.AsElementId()
                    ?? ElementId.InvalidElementId) as Level)?.Name ?? "";

                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"]   = Rv.IdStr(el.Id),
                    ["category"]     = catName,
                    ["family_type"]  = typeName,
                    ["level"]        = level,
                    ["system_type"]  = sysType.Length > 0 ? sysType : "(boş)",
                    ["fix_hint"]     = "Elemanın fitting/takeoff bağlantı noktasını doğru sisteme bağlayın.",
                });
            }

            ctx.Log($"  detect_undefined_system: {elements.Count} eleman tarandı → " +
                    $"{rows.Count} Undefined bulundu");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // V03  tag_elements
        //
        // input : List<Element>  (tag eklenecek elemanlar)
        // params: tag_type_name   String  zorunlu  (tag family tip adı)
        //         view_id         String  opsiyonel (boşsa aktif view)
        //         leader          Bool    default=false
        //         orientation     String  default="horizontal" (horizontal|vertical)
        //
        // output: int (eklenen tag sayısı)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("tag_elements",
            RequiresTransaction = true,
            Description = "Eleman listesine otomatik IndependentTag yerleştirir. " +
                          "params.tag_type_name ile tag family tipi seçilir.",
            Category    = "Görünüm")]
        public static int TagElements(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var elements   = ctx.InputAs<List<Element>>();
            var tagTypName = ctx.RequireString("tag_type_name");
            var viewIdStr  = ctx.GetString("view_id", "");
            bool leader    = ctx.GetBool("leader", false);
            var orientStr  = ctx.GetString("orientation", "horizontal").ToLowerInvariant();

            var tagOrient = orientStr == "vertical"
                ? TagOrientation.Vertical
                : TagOrientation.Horizontal;

            // View çöz
            View? targetView = null;
            if (!string.IsNullOrEmpty(viewIdStr) &&
                long.TryParse(viewIdStr, out long vid))
            {
                targetView = rctx.Doc.GetElement(Rv.MakeElementId(vid)) as View;  // v6
            }
            targetView ??= rctx.UiDoc.ActiveView;

            if (targetView == null)
            {
                ctx.Log("  tag_elements: Aktif görünüm bulunamadı → 0");
                return 0;
            }

            // Tag FamilySymbol bul
            var tagSymbol = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    s.Name.Equals(tagTypName, StringComparison.OrdinalIgnoreCase) ||
                    s.FamilyName.Equals(tagTypName, StringComparison.OrdinalIgnoreCase));

            if (tagSymbol == null)
            {
                ctx.Log($"  tag_elements: '{tagTypName}' tag tipi bulunamadı → 0");
                return 0;
            }

            if (!tagSymbol.IsActive) tagSymbol.Activate();

            int count = 0;
            using var scope = new RevitWriteScope(rctx.Doc, "Eleman Etiketle", rctx.IsAtomicMode);

            foreach (var el in elements)
            {
                try
                {
                    // Tag konumu: element BBox merkezi
                    var bb  = el.get_BoundingBox(targetView) ?? el.get_BoundingBox(null);
                    if (bb == null) continue;
                    var center = (bb.Min + bb.Max) / 2.0;

                    var link = new Reference(el);
                    IndependentTag.Create(
                        rctx.Doc,
                        tagSymbol.Id,
                        targetView.Id,
                        link,
                        leader,
                        tagOrient,
                        center);

                    count++;
                }
                catch (Exception ex)
                {
                    ctx.Log($"  tag_elements: {el.Id} tag eklenemedi — {ex.Message}");
                }
            }

            scope.Commit();
            ctx.Log($"  tag_elements: {count}/{elements.Count} elemana tag eklendi");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // V04  check_untagged_elements
        //
        // input : List<Element>
        // params: view_id   String  opsiyonel
        //         category  String  opsiyonel — sadece bu kategoride kontrol
        // output: List<Dict> {element_id, category, family_type, level, tag_count}
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("check_untagged_elements",
            Description = "Görünümde tag'siz olan elemanları listeler. " +
                          "tag_elements ile birlikte QA pipeline'ı oluşturur.",
            Category    = "Görünüm")]
        public static List<Dictionary<string, object?>> CheckUntaggedElements(OpContext ctx)
        {
            var rctx     = RequireRevit(ctx);
            var elements = ctx.InputAs<List<Element>>();
            var viewIdStr= ctx.GetString("view_id", "");

            // View çöz
            View? targetView = null;
            if (!string.IsNullOrEmpty(viewIdStr) &&
                long.TryParse(viewIdStr, out long vid))
                targetView = rctx.Doc.GetElement(Rv.MakeElementId(vid)) as View;  // v6
            targetView ??= rctx.UiDoc.ActiveView;

            // Görünümdeki tüm tag'leri index'le: element_id → tag sayısı
            var tagIndex = new Dictionary<long, int>();
            if (targetView != null)
            {
                var tags = new FilteredElementCollector(rctx.Doc, targetView.Id)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>();

                foreach (var tag in tags)
                {
                    try
                    {
                        // TaggedElementId: Revit 2024+ (GetTaggedLocalElement kaldırıldı)
                    ElementId? taggedId = null;
                    try
                    {
                        var refs = tag.GetTaggedElementIds();
                        taggedId = refs?.FirstOrDefault()?.LinkedElementId
                                ?? refs?.FirstOrDefault()?.HostElementId;
                    }
                    catch { taggedId = null; }
                        if (taggedId == null || taggedId == ElementId.InvalidElementId) continue;
                        long key = taggedId.Value;
                        tagIndex[key] = tagIndex.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                    catch { /* skip */ }
                }
            }

            var rows = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                int tagCount = tagIndex.TryGetValue(Rv.GetId(el.Id), out int tc) ? tc : 0;
                if (tagCount > 0) continue; // tag'lisi atla

                string catName  = el.Category?.Name ?? "";
                string typeName = (rctx.Doc.GetElement(el.GetTypeId()) as ElementType)?.Name ?? "";
                string level    = (rctx.Doc.GetElement(
                    el.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)?.AsElementId()
                    ?? ElementId.InvalidElementId) as Level)?.Name ?? "";

                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"]  = Rv.IdStr(el.Id),
                    ["category"]    = catName,
                    ["family_type"] = typeName,
                    ["level"]       = level,
                    ["tag_count"]   = tagCount,
                });
            }

            ctx.Log($"  check_untagged_elements: {elements.Count} eleman → " +
                    $"{rows.Count} tag'siz");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        private static List<ElementId> ParseCategories(Document doc, string catStr)
        {
            var ids = new List<ElementId>();
            foreach (var s in catStr.Split(','))
            {
                var trimmed = s.Trim();
                if (Enum.TryParse<BuiltInCategory>(trimmed, true, out var bic))
                    ids.Add(new ElementId(bic));  // BuiltInCategory overload — tüm sürümlerde stabil
            }
            return ids;
        }

        private static long? GetParameterId(Document doc, string paramName, ElementId catId)
        {
            // Shared / built-in parametre ID'si
            var elem = new FilteredElementCollector(doc)
                .OfCategoryId(catId)
                .WhereElementIsNotElementType()
                .FirstOrDefault();

            if (elem == null) return null;

            var p = elem.LookupParameter(paramName);
            if (p != null) return Rv.GetId(p.Id);

            // Built-in fallback
            if (paramName.Equals("System Classification", StringComparison.OrdinalIgnoreCase))
                return (long)BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM;

            return null;
        }

        private static FillPatternElement? FindFillPattern(Document doc, string name)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fp =>
                    fp.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    fp.GetFillPattern().IsSolidFill);

        private static List<BuiltInCategory> GetMepCategories(string discipline)
        {
            var all = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_DuctTerminal,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_Sprinklers,
                BuiltInCategory.OST_FireAlarmDevices,
                BuiltInCategory.OST_LightingFixtures,
                BuiltInCategory.OST_ElectricalEquipment,
            };

            if (discipline == "hvac")
                return all.Take(4).ToList();
            if (discipline == "plumbing")
                return all.Skip(4).Take(4).ToList();
            if (discipline == "electrical")
                return all.Skip(8).ToList();
            return all;
        }

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
    }
}
