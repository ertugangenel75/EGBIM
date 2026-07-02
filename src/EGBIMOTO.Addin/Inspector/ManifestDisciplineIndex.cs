using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Addin.Inspector
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — ManifestDisciplineIndex  (v14)
    //
    //  Element Inspector'ın "Bu elemanla ilgili manifestler" bölümü için:
    //  356+ manifest'i tarar, her manifestin İLK collect_* adımından hedef
    //  kategoriyi çıkarır (normalize_manifests.py'deki COLLECT_CAT ile aynı
    //  mantık — Python offline araç, bu ise runtime C# karşılığı; iki ayrı
    //  ortamda çalıştıkları için kasıtlı olarak ayrı tutuldu).
    //
    //  Kullanım:
    //    ManifestDisciplineIndex.Build(allManifests);   // ManifestBrowserWindow'un yüklediği liste
    //    var ilgili = ManifestDisciplineIndex.For(BuiltInCategory.OST_Walls);
    // ═══════════════════════════════════════════════════════════════════════════
    public static class ManifestDisciplineIndex
    {
        private static Dictionary<BuiltInCategory, List<EgManifest>> _index = new();

        private static readonly Dictionary<string, BuiltInCategory[]> COLLECT_OP_CATEGORY = new(StringComparer.OrdinalIgnoreCase)
        {
            ["collect_walls"]                 = new[] { BuiltInCategory.OST_Walls },
            ["collect_structural_walls"]      = new[] { BuiltInCategory.OST_Walls },
            ["collect_floors"]                = new[] { BuiltInCategory.OST_Floors },
            ["collect_ceilings"]              = new[] { BuiltInCategory.OST_Ceilings },
            ["collect_roofs"]                 = new[] { BuiltInCategory.OST_Roofs },
            ["collect_doors"]                 = new[] { BuiltInCategory.OST_Doors },
            ["collect_windows"]               = new[] { BuiltInCategory.OST_Windows },
            ["collect_rooms"]                 = new[] { BuiltInCategory.OST_Rooms },
            ["collect_stairs"]                = new[] { BuiltInCategory.OST_Stairs },
            ["collect_ramps"]                 = new[] { BuiltInCategory.OST_Ramps },
            ["collect_curtain_walls"]         = new[] { BuiltInCategory.OST_Walls },
            ["collect_generic_models"]        = new[] { BuiltInCategory.OST_GenericModel },
            ["collect_casework"]              = new[] { BuiltInCategory.OST_Casework },
            ["collect_parking"]               = new[] { BuiltInCategory.OST_Parking },
            ["collect_topography"]            = new[] { BuiltInCategory.OST_Topography },
            ["collect_columns"]               = new[] { BuiltInCategory.OST_StructuralColumns },
            ["collect_beams"]                 = new[] { BuiltInCategory.OST_StructuralFraming },
            ["collect_structural_framing"]    = new[] { BuiltInCategory.OST_StructuralFraming },
            ["collect_foundations"]           = new[] { BuiltInCategory.OST_StructuralFoundation },
            ["collect_rebar"]                 = new[] { BuiltInCategory.OST_Rebar },
            ["collect_rebar_in_host"]         = new[] { BuiltInCategory.OST_Rebar },
            ["collect_ducts"]                 = new[] { BuiltInCategory.OST_DuctCurves },
            ["collect_duct_fittings"]         = new[] { BuiltInCategory.OST_DuctFitting },
            ["collect_pipes"]                 = new[] { BuiltInCategory.OST_PipeCurves },
            ["collect_pipe_fittings"]         = new[] { BuiltInCategory.OST_PipeFitting },
            ["collect_pipe_accessories"]      = new[] { BuiltInCategory.OST_PipeAccessory },
            ["collect_cable_trays"]           = new[] { BuiltInCategory.OST_CableTray },
            ["collect_conduits"]              = new[] { BuiltInCategory.OST_Conduit },
            ["collect_mechanical_equipment"]  = new[] { BuiltInCategory.OST_MechanicalEquipment },
            ["collect_electrical_equipment"]  = new[] { BuiltInCategory.OST_ElectricalEquipment },
            ["collect_electrical_fixtures"]   = new[] { BuiltInCategory.OST_ElectricalFixtures },
            ["collect_lighting_fixtures"]     = new[] { BuiltInCategory.OST_LightingFixtures },
            ["collect_lighting_devices"]      = new[] { BuiltInCategory.OST_LightingDevices },
            ["collect_plumbing_fixtures"]     = new[] { BuiltInCategory.OST_PlumbingFixtures },
            ["collect_air_terminals"]         = new[] { BuiltInCategory.OST_DuctTerminal },
            ["collect_sprinklers"]            = new[] { BuiltInCategory.OST_Sprinklers },
            ["collect_fire_alarm_devices"]    = new[] { BuiltInCategory.OST_FireAlarmDevices },
            ["collect_levels"]                = new[] { BuiltInCategory.OST_Levels },
            ["collect_grids"]                 = new[] { BuiltInCategory.OST_Grids },
        };

        public static void Build(IEnumerable<EgManifest> manifests)
        {
            var idx = new Dictionary<BuiltInCategory, List<EgManifest>>();

            foreach (var m in manifests)
            {
                var cats = new HashSet<BuiltInCategory>();
                foreach (var step in m.Steps)
                {
                    if (string.IsNullOrWhiteSpace(step.Op)) continue;

                    if (COLLECT_OP_CATEGORY.TryGetValue(step.Op, out var mapped))
                    {
                        foreach (var c in mapped) cats.Add(c);
                    }
                    else if (step.Op.Equals("collect_by_category", StringComparison.OrdinalIgnoreCase) &&
                             step.Params != null && step.Params.TryGetValue("category", out var catv))
                    {
                        foreach (var name in FlattenToStrings(catv))
                            if (Ops.EgCategoryResolver.TryResolve(name, out var bic))
                                cats.Add(bic);
                    }
                }

                foreach (var c in cats)
                {
                    if (!idx.TryGetValue(c, out var list))
                        idx[c] = list = new List<EgManifest>();
                    list.Add(m);
                }
            }

            _index = idx;
        }

        /// <summary>Bu kategoriyi hedefleyen manifestler — bulunamazsa boş liste.</summary>
        public static IReadOnlyList<EgManifest> For(BuiltInCategory bic)
            => _index.TryGetValue(bic, out var list)
                ? list.OrderBy(m => m.Title, StringComparer.OrdinalIgnoreCase).ToList()
                : Array.Empty<EgManifest>();

        private static IEnumerable<string> FlattenToStrings(object? raw)
        {
            switch (raw)
            {
                case string s:
                    yield return s;
                    break;
                case System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String:
                    yield return je.GetString() ?? "";
                    break;
                case System.Text.Json.JsonElement je2 when je2.ValueKind == System.Text.Json.JsonValueKind.Array:
                    foreach (var item in je2.EnumerateArray())
                        if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                            yield return item.GetString() ?? "";
                    break;
                case List<object?> list:
                    foreach (var o in list)
                        if (o?.ToString() is string ss) yield return ss;
                    break;
            }
        }
    }
}
