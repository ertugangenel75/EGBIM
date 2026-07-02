using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Revit model toplama op'ları.
    /// Tüm collect_* op'ları burada — RevitOps'ta duplicate yok.
    ///
    /// Yeni kategori eklemek:
    ///   [EgOp("collect_xxx", ...)] public static List<Element> CollectXxx(OpContext ctx)
    ///       => Collect(ctx, BuiltInCategory.OST_Xxx);
    /// </summary>
    public static class CollectionOps
    {
        // ── V1 uyumluluk alias ────────────────────────────────────────────────
        [EgOp("collect_elements",
            Description = "Kategori bazlı toplama. params: category (BuiltInCategory adı). V1 uyumluluk alias.",
            Category    = "Toplama")]
        public static List<Element> CollectElements(OpContext ctx)
        {
            var catName = ctx.GetString("category", "OST_Walls");
            if (!Enum.TryParse<BuiltInCategory>(catName, out var bic))
            {
                ctx.Log($"  ⚠ collect_elements: Kategori tanınmadı '{catName}'");
                return new();
            }
            return Collect(ctx, bic);
        }

        // ── Mimari ───────────────────────────────────────────────────────────
        [EgOp("collect_walls",          Description = "Tüm duvarları toplar",              Category = "Toplama")]
        public static List<Element> CollectWalls(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_Walls);

        [EgOp("collect_floors",         Description = "Tüm döşemeleri toplar",             Category = "Toplama")]
        public static List<Element> CollectFloors(OpContext ctx)         => Collect(ctx, BuiltInCategory.OST_Floors);

        [EgOp("collect_ceilings",       Description = "Tüm tavanları toplar",              Category = "Toplama")]
        public static List<Element> CollectCeilings(OpContext ctx)       => Collect(ctx, BuiltInCategory.OST_Ceilings);

        [EgOp("collect_roofs",          Description = "Tüm çatıları toplar",               Category = "Toplama")]
        public static List<Element> CollectRoofs(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_Roofs);

        [EgOp("collect_doors",          Description = "Tüm kapıları toplar",               Category = "Toplama")]
        public static List<Element> CollectDoors(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_Doors);

        [EgOp("collect_windows",        Description = "Tüm pencereleri toplar",            Category = "Toplama")]
        public static List<Element> CollectWindows(OpContext ctx)        => Collect(ctx, BuiltInCategory.OST_Windows);

        [EgOp("collect_rooms",          Description = "Tüm odaları toplar",                Category = "Toplama")]
        public static List<Element> CollectRooms(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_Rooms);

        [EgOp("collect_stairs",         Description = "Tüm merdivenleri toplar",           Category = "Toplama")]
        public static List<Element> CollectStairs(OpContext ctx)         => Collect(ctx, BuiltInCategory.OST_Stairs);

        [EgOp("collect_ramps",          Description = "Tüm rampları toplar",               Category = "Toplama")]
        public static List<Element> CollectRamps(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_Ramps);

        [EgOp("collect_curtain_walls",  Description = "Tüm giydirme cephe duvarlarını toplar", Category = "Toplama")]
        public static List<Element> CollectCurtainWalls(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var list = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.WallType?.Kind == WallKind.Curtain)
                .Cast<Element>()
                .ToList();
            ctx.Log($"  collect_curtain_walls: {list.Count} eleman");
            return list;
        }

        [EgOp("collect_generic_models", Description = "Tüm genel modelleri toplar",        Category = "Toplama")]
        public static List<Element> CollectGenericModels(OpContext ctx)  => Collect(ctx, BuiltInCategory.OST_GenericModel);

        [EgOp("collect_furniture",      Description = "Tüm mobilyaları toplar",            Category = "Toplama")]
        public static List<Element> CollectFurniture(OpContext ctx)      => Collect(ctx, BuiltInCategory.OST_Furniture);

        [EgOp("collect_furniture_systems", Description = "Tüm mobilya sistemlerini toplar", Category = "Toplama")]
        public static List<Element> CollectFurnitureSystems(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_FurnitureSystems);

        [EgOp("collect_casework",       Description = "Tüm dolap/mutfak elemanlarını toplar", Category = "Toplama")]
        public static List<Element> CollectCasework(OpContext ctx)       => Collect(ctx, BuiltInCategory.OST_Casework);

        [EgOp("collect_parking",        Description = "Tüm otopark elemanlarını toplar",   Category = "Toplama")]
        public static List<Element> CollectParking(OpContext ctx)        => Collect(ctx, BuiltInCategory.OST_Parking);

        [EgOp("collect_site",           Description = "Tüm arazi elemanlarını toplar",     Category = "Toplama")]
        public static List<Element> CollectSite(OpContext ctx)           => Collect(ctx, BuiltInCategory.OST_Site);

        [EgOp("collect_topography",     Description = "Tüm topoğrafya yüzeylerini toplar", Category = "Toplama")]
        public static List<Element> CollectTopography(OpContext ctx)     => Collect(ctx, BuiltInCategory.OST_Topography);

        // ── Yapısal ──────────────────────────────────────────────────────────
        [EgOp("collect_columns",        Description = "Tüm yapısal kolonları toplar",      Category = "Toplama")]
        public static List<Element> CollectColumns(OpContext ctx)        => Collect(ctx, BuiltInCategory.OST_StructuralColumns);

        [EgOp("collect_beams",          Description = "Tüm yapısal kirişleri toplar",      Category = "Toplama")]
        public static List<Element> CollectBeams(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_StructuralFraming);

        [EgOp("collect_structural_framing", Description = "Yapısal çerçeve elemanlarını toplar (collect_beams ile aynı OST_StructuralFraming — alias)", Category = "Toplama")]
        public static List<Element> CollectStructuralFraming(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_StructuralFraming);

        [EgOp("collect_foundations",    Description = "Tüm temelleri toplar",              Category = "Toplama")]
        public static List<Element> CollectFoundations(OpContext ctx)    => Collect(ctx, BuiltInCategory.OST_StructuralFoundation);

        [EgOp("collect_structural_walls", Description = "Yapısal duvarları toplar (bearing)", Category = "Toplama")]
        public static List<Element> CollectStructuralWalls(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var list = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                                .Where(w =>
                {
                    // Revit 2025+: StructuralWallUsage internal — WALL_STRUCTURAL_SIGNIFICANT ile kontrol
                    var p = w.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT);
                    return p != null && p.AsInteger() == 1;
                })
                .Cast<Element>()
                .ToList();
            ctx.Log($"  collect_structural_walls: {list.Count} eleman");
            return list;
        }

        [EgOp("collect_rebar",          Description = "Tüm donatıları toplar",             Category = "Toplama")]
        public static List<Element> CollectRebar(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_Rebar);

        [EgOp("collect_rebar_in_host",
            Description = "params.host_id element_id'sine sahip elemanın içindeki donatıları toplar",
            Category    = "Toplama")]
        public static List<Element> CollectRebarInHost(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var hostId = ctx.GetInt("host_id", -1);
            if (hostId < 0) return new();
            var host = rctx.Doc.GetElement(Rv.MakeElementId(hostId));  // v6
            if (host is null) { ctx.Log($"  ⚠ host bulunamadı: {hostId}"); return new(); }
            var list = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .Where(e => e is Autodesk.Revit.DB.Structure.Rebar rb && rb.GetHostId() == host.Id)
                .ToList();
            ctx.Log($"  collect_rebar_in_host #{hostId}: {list.Count} donatı");
            return list;
        }

        // ── MEP ──────────────────────────────────────────────────────────────
        [EgOp("collect_ducts",          Description = "Tüm havalandırma kanallarını toplar", Category = "Toplama")]
        public static List<Element> CollectDucts(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_DuctCurves);

        [EgOp("collect_duct_fittings",  Description = "Tüm kanal bağlantı parçalarını toplar", Category = "Toplama")]
        public static List<Element> CollectDuctFittings(OpContext ctx)   => Collect(ctx, BuiltInCategory.OST_DuctFitting);

        [EgOp("collect_pipes",          Description = "Tüm boruları toplar",               Category = "Toplama")]
        public static List<Element> CollectPipes(OpContext ctx)          => Collect(ctx, BuiltInCategory.OST_PipeCurves);

        [EgOp("collect_pipe_fittings",  Description = "Tüm boru bağlantı parçalarını toplar", Category = "Toplama")]
        public static List<Element> CollectPipeFittings(OpContext ctx)   => Collect(ctx, BuiltInCategory.OST_PipeFitting);

        [EgOp("collect_pipe_accessories", Description = "Tüm boru aksesuarlarını toplar",  Category = "Toplama")]
        public static List<Element> CollectPipeAccessories(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_PipeAccessory);

        [EgOp("collect_cable_trays",    Description = "Tüm kablo tavalarını toplar",       Category = "Toplama")]
        public static List<Element> CollectCableTrays(OpContext ctx)     => Collect(ctx, BuiltInCategory.OST_CableTray);

        [EgOp("collect_conduits",       Description = "Tüm boru iletkenlerini toplar",     Category = "Toplama")]
        public static List<Element> CollectConduits(OpContext ctx)       => Collect(ctx, BuiltInCategory.OST_Conduit);

        [EgOp("collect_mechanical_equipment", Description = "Mekanik ekipmanları toplar",  Category = "Toplama")]
        public static List<Element> CollectMechanicalEquipment(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_MechanicalEquipment);

        [EgOp("collect_electrical_equipment", Description = "Elektrik ekipmanlarını toplar", Category = "Toplama")]
        public static List<Element> CollectElectricalEquipment(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_ElectricalEquipment);

        [EgOp("collect_electrical_fixtures", Description = "Elektrik prizlerini/anahtarlarını toplar", Category = "Toplama")]
        public static List<Element> CollectElectricalFixtures(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_ElectricalFixtures);

        [EgOp("collect_lighting_fixtures", Description = "Aydınlatma elemanlarını toplar", Category = "Toplama")]
        public static List<Element> CollectLightingFixtures(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_LightingFixtures);

        [EgOp("collect_lighting_devices", Description = "Aydınlatma cihazlarını toplar",  Category = "Toplama")]
        public static List<Element> CollectLightingDevices(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_LightingDevices);

        [EgOp("collect_plumbing_fixtures", Description = "Sıhhi tesisat elemanlarını toplar", Category = "Toplama")]
        public static List<Element> CollectPlumbingFixtures(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_PlumbingFixtures);

        [EgOp("collect_air_terminals",  Description = "Hava terminallerini toplar",        Category = "Toplama")]
        public static List<Element> CollectAirTerminals(OpContext ctx)   => Collect(ctx, BuiltInCategory.OST_DuctTerminal);

        [EgOp("collect_sprinklers",     Description = "Tüm sprinkler elemanlarını toplar", Category = "Toplama")]
        public static List<Element> CollectSprinklers(OpContext ctx)     => Collect(ctx, BuiltInCategory.OST_Sprinklers);

        [EgOp("collect_fire_alarm_devices", Description = "Yangın alarm cihazlarını toplar", Category = "Toplama")]
        public static List<Element> CollectFireAlarmDevices(OpContext ctx) => Collect(ctx, BuiltInCategory.OST_FireAlarmDevices);

        // ── Seviye / Aks / Referans ───────────────────────────────────────────
        [EgOp("collect_levels",
            Description = "Tüm katları (Level) yüksekliğe göre sıralı toplar",
            Category    = "Toplama")]
        public static List<Element> CollectLevels(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var list = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Cast<Element>()
                .ToList();
            ctx.Log($"  collect_levels: {list.Count} kat");
            return list;
        }

        [EgOp("collect_grids",
            Description = "Tüm aks çizgilerini toplar",
            Category    = "Toplama")]
        public static List<Element> CollectGrids(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            return new FilteredElementCollector(rctx.Doc).OfClass(typeof(Grid)).ToList();
        }

        [EgOp("collect_views",
            Description = "Tüm görünümleri toplar. params: view_type (FloorPlan|Section|Elevation|3D|Schedule|All)",
            Category    = "Toplama")]
        public static List<Element> CollectViews(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var viewType = ctx.GetString("view_type", "All").ToLowerInvariant();
            var all = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate);
            var filtered = viewType switch
            {
                "floorplan"  => all.Where(v => v.ViewType == ViewType.FloorPlan),
                "section"    => all.Where(v => v.ViewType == ViewType.Section),
                "elevation"  => all.Where(v => v.ViewType == ViewType.Elevation),
                "3d"         => all.Where(v => v.ViewType == ViewType.ThreeD),
                "schedule"   => all.Where(v => v.ViewType == ViewType.Schedule),
                _            => all
            };
            var list = filtered.Cast<Element>().ToList();
            ctx.Log($"  collect_views ({viewType}): {list.Count}");
            return list;
        }

        [EgOp("collect_sheets",
            Description = "Tüm çizim paftalarını toplar",
            Category    = "Toplama")]
        public static List<Element> CollectSheets(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            return new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(ViewSheet))
                .ToList();
        }

        [EgOp("collect_families",
            Description = "Yüklü aileleri toplar. params: category (opsiyonel)",
            Category    = "Toplama")]
        public static List<Element> CollectFamilies(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var catName = ctx.GetString("category", "");
            var all = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(Family))
                .Cast<Family>();
            if (!string.IsNullOrEmpty(catName))
                all = all.Where(f => f.FamilyCategory?.Name
                    .Contains(catName, StringComparison.OrdinalIgnoreCase) == true);
            var list = all.Cast<Element>().ToList();
            ctx.Log($"  collect_families: {list.Count}");
            return list;
        }

        // ── Seçim / Dinamik ───────────────────────────────────────────────────
        [EgOp("collect_selected",
            Description = "Revit'te seçili olan elemanları toplar",
            Category    = "Toplama")]
        public static List<Element> CollectSelected(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var ids  = rctx.UiDoc.Selection.GetElementIds();
            ctx.Log($"  collect_selected: {ids.Count} seçili");
            return ids.Select(id => rctx.Doc.GetElement(id)).Where(e => e != null).ToList();
        }

        [EgOp("collect_by_category",
            Description = "params.category (BuiltInCategory adı) kategorisindeki elemanları toplar",
            Category    = "Toplama")]
        public static List<Element> CollectByCategory(OpContext ctx)
        {
            var catName = ctx.GetString("category", "OST_Walls");
            if (!Enum.TryParse<BuiltInCategory>(catName, out var bic))
            {
                ctx.Log($"  ⚠ collect_by_category: bilinmeyen kategori '{catName}'");
                return new();
            }
            return Collect(ctx, bic);
        }

        [EgOp("collect_by_type_name",
            Description = "params.type_name içeren tip adına sahip elemanları toplar",
            Category    = "Toplama")]
        public static List<Element> CollectByTypeName(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var typeName = ctx.GetString("type_name").ToLowerInvariant();
            return new FilteredElementCollector(rctx.Doc)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    var t = rctx.Doc.GetElement(e.GetTypeId()) as ElementType;
                    return t?.Name.ToLowerInvariant().Contains(typeName) == true;
                }).ToList();
        }

        [EgOp("collect_by_workset",
            Description = "params.workset_name içeren workset'teki elemanları toplar",
            Category    = "Toplama")]
        public static List<Element> CollectByWorkset(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var worksetName = ctx.GetString("workset_name").ToLowerInvariant();
            if (!rctx.Doc.IsWorkshared)
            {
                ctx.Log("  ⚠ Model workshared değil");
                return new();
            }
            var wsTable = rctx.Doc.GetWorksetTable();
            var wsIds   = new Autodesk.Revit.DB.FilteredWorksetCollector(rctx.Doc)
                .OfKind(WorksetKind.UserWorkset)
                .Where(ws => ws.Name.ToLowerInvariant().Contains(worksetName))
                .Select(ws => ws.Id)
                .Where(id => wsTable.GetWorkset(id).Name
                    .ToLowerInvariant().Contains(worksetName))
                .ToList();
            if (!wsIds.Any()) { ctx.Log($"  ⚠ Workset bulunamadı: '{worksetName}'"); return new(); }
            var list = new FilteredElementCollector(rctx.Doc)
                .WhereElementIsNotElementType()
                .Where(e => wsIds.Contains(e.WorksetId))
                .ToList();
            ctx.Log($"  collect_by_workset '{worksetName}': {list.Count}");
            return list;
        }

        [EgOp("collect_by_phase",
            Description = "params.phase_name fazında oluşturulan elemanları toplar",
            Category    = "Toplama")]
        public static List<Element> CollectByPhase(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var phaseName = ctx.RequireString("phase_name").ToLowerInvariant();
            var phase     = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .FirstOrDefault(p => p.Name.ToLowerInvariant().Contains(phaseName));
            if (phase is null) { ctx.Log($"  ⚠ Faz bulunamadı: '{phaseName}'"); return new(); }
            var list = new FilteredElementCollector(rctx.Doc)
                .WhereElementIsNotElementType()
                .Where(e => e.CreatedPhaseId == phase.Id)
                .ToList();
            ctx.Log($"  collect_by_phase '{phaseName}': {list.Count}");
            return list;
        }

        [EgOp("collect_linked_elements",
            Description = "Bağlı (linked) Revit modellerindeki elemanları toplar. params: category",
            Category    = "Toplama")]
        public static List<Element> CollectLinkedElements(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var catName = ctx.GetString("category", "OST_Walls");
            Enum.TryParse<BuiltInCategory>(catName, out var bic);
            var result  = new List<Element>();
            var links   = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>();
            foreach (var link in links)
            {
                var linkDoc = link.GetLinkDocument();
                if (linkDoc is null) continue;
                var elems = new FilteredElementCollector(linkDoc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .ToList();
                result.AddRange(elems);
            }
            ctx.Log($"  collect_linked_elements ({catName}): {result.Count}");
            return result;
        }

        // ── Tip Koleksiyonu ───────────────────────────────────────────────────
        // FIX #7: collect_types ParamOps.cs'ten buraya taşındı (Category doğrulaması).
        [EgOp("collect_types",
            Description = "Belirli kategorideki tüm element tiplerini (ElementType) toplar. params: category (BuiltInCategory adı, opsiyonel)",
            Category    = "Toplama")]
        public static List<Dictionary<string, object?>> CollectTypes(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var catName = ctx.GetString("category", "");
            var collector = new FilteredElementCollector(rctx.Doc).WhereElementIsElementType();
            if (!string.IsNullOrEmpty(catName) &&
                Enum.TryParse<BuiltInCategory>(catName, out var bic))
                collector = collector.OfCategory(bic);
            var list = collector.Cast<ElementType>().Select(t => new Dictionary<string, object?>
            {
                ["tip_id"]   = Rv.GetId(t.Id),
                ["tip"]      = t.Name,
                ["kategori"] = t.Category?.Name ?? "",
                ["aile"]     = (t as FamilySymbol)?.Family?.Name ?? ""
            }).ToList();
            ctx.Log($"  collect_types ({(string.IsNullOrEmpty(catName) ? "tümü" : catName)}): {list.Count} tip");
            return list;
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────
        private static List<Element> Collect(OpContext ctx, BuiltInCategory bic)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var list = new FilteredElementCollector(rctx.Doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToList();
            ctx.Log($"  {bic}: {list.Count} eleman");
            return list;
        }
        // ── v3.1: collect_multi — paralel çoklu kategori toplama ─────────────
        /// <summary>
        /// Birden fazla Revit kategorisini tek seferde ve paralel olarak toplar.
        /// Büyük modellerde 10 ayrı collect_* adımı yerine tek adım = belirgin hız artışı.
        ///
        /// Manifest params örneği:
        ///   "categories": ["OST_StructuralColumns", "OST_StructuralFraming",
        ///                   "OST_Floors", "OST_Walls", "OST_StructuralFoundation"]
        ///   "phase_name":  "Yeni İnşaat"   (opsiyonel)
        ///
        /// Çıktı: List&lt;Element&gt; — tüm kategorilerden birleştirilmiş liste.
        /// </summary>
        [EgOp("collect_multi",
            Description = "Birden fazla kategoriyi paralel olarak toplar. params: categories (string[])",
            Category    = "Toplama")]
        public static List<Element> CollectMulti(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var categories = ctx.GetParam<List<string>>("categories", new List<string>());

            if (categories is null || categories.Count == 0)
            {
                ctx.Log("  ⚠ collect_multi: 'categories' parametresi boş. Boş liste döndürülüyor.");
                return new List<Element>();
            }

            // Geçerli BuiltInCategory değerlerini çöz
            var validBics = new List<BuiltInCategory>();
            foreach (var catName in categories)
            {
                if (Enum.TryParse<BuiltInCategory>(catName.Trim(), out var bic))
                    validBics.Add(bic);
                else
                    ctx.Log($"  ⚠ collect_multi: Tanınmayan kategori '{catName}' — atlandı");
            }

            // Phase filtresi
            var phaseName = ctx.GetString("phase_name", "");
            Phase? phase = null;
            if (!string.IsNullOrEmpty(phaseName))
            {
                phase = new FilteredElementCollector(rctx.Doc)
                    .OfClass(typeof(Phase))
                    .Cast<Phase>()
                    .FirstOrDefault(p => p.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase));
            }

            // Sıralı toplama — Revit API tek thread gerektirir.
            // collect_multi faydası: N ayrı manifest adımı yerine 1 adım.
            var bicList = validBics.ToList();
            var result  = new List<Element>();

            foreach (var bic in bicList)
            {
                try
                {
                    var collector = new FilteredElementCollector(rctx.Doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType();

                    if (phase is not null)
                    {
                        // Revit 2026: ElementPhaseStatusFilter(phaseId, statuses[]) — params array
                        var phaseFilter = new ElementPhaseStatusFilter(phase.Id,
                            new[] { ElementOnPhaseStatus.Existing, ElementOnPhaseStatus.New }, true);
                        collector = collector.WherePasses(phaseFilter);
                    }

                    result.AddRange(collector);
                }
                catch (Exception ex)
                {
                    ctx.Log($"  ⚠ collect_multi [{bic}]: {ex.Message}");
                }
            }
            ctx.Log($"  collect_multi: {bicList.Count} kategori, {result.Count} eleman" +
                    (phase is not null ? $" (faz: {phaseName})" : ""));
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // collect_by_ids   (GENEL KÖPRÜ — dict satırlarındaki ID'lerden eleman)
        //
        // Herhangi bir op'un List<Dictionary> çıktısındaki ID alan(lar)ından
        // gerçek Element listesi üretir. clash_detect_matrix (a_id/b_id),
        // mep_straighten_scan, schedule diff vb. her satır-tabanlı çıktıyı
        // move_element / set_param gibi Element bekleyen op'lara köprüler.
        //
        // input : List<Dictionary<string,object?>>  (ID taşıyan satırlar)
        // params: id_fields    String  default="element_id"
        //                      Virgülle birden çok alan: "a_id,b_id"
        //         distinct     String  default="true"  (mükerrer ID'leri tekle)
        //
        // output: List<Element>
        //
        // Örnek (clash'taki A tarafı elemanları):
        //   { "op": "collect_by_ids", "from": "clash", "params": { "id_fields": "a_id" } }
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("collect_by_ids",
            Description =
                "Dict satırlarındaki ID alan(lar)ından Element listesi toplar. Satır-tabanlı op " +
                "çıktılarını (clash, scan, diff) Element bekleyen op'lara (move_element, set_param) köprüler.\n" +
                "Input: List<Dictionary>. params: id_fields (virgülle çok alan, default 'element_id'), " +
                "distinct (default 'true').",
            Category = "Toplama")]
        public static List<Element> CollectByIds(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[collect_by_ids] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            var rows = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>(
                new List<Dictionary<string, object?>>());
            var idFields = ctx.GetString("id_fields", "element_id")
                              .Split(',', StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => s.Trim())
                              .Where(s => s.Length > 0)
                              .ToList();
            bool distinct = !ctx.GetString("distinct", "true")
                               .Equals("false", StringComparison.OrdinalIgnoreCase);

            if (idFields.Count == 0) idFields.Add("element_id");

            var seen = new HashSet<long>();
            var result = new List<Element>();
            int missing = 0;

            foreach (var row in rows)
            {
                foreach (var field in idFields)
                {
                    if (!row.TryGetValue(field, out var idObj) || idObj == null) continue;

                    long id = ToLong(idObj);
                    if (id == 0) continue;
                    if (distinct && !seen.Add(id)) continue;

                    Element? el = null;
                    try { el = doc.GetElement(Rv.MakeElementId(id)); } catch { }
                    if (el != null) result.Add(el);
                    else missing++;
                }
            }

            ctx.Log($"  collect_by_ids: {rows.Count} satır × [{string.Join(",", idFields)}] " +
                    $"→ {result.Count} eleman" + (missing > 0 ? $" ({missing} bulunamadı)" : ""));
            return result;
        }

        private static long ToLong(object? o)
        {
            if (o == null) return 0;
            if (o is long l) return l;
            if (o is int i)  return i;
            if (o is double d) return (long)d;
            return long.TryParse(o.ToString(), out var v) ? v : 0;
        }

    }
}
