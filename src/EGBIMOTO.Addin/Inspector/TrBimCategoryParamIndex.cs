using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;

namespace EGBIMOTO.Addin.Inspector
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — TrBimCategoryParamIndex  (v14)
    //
    //  data/test/trbim_category_param_index.json şu şekli taşır:
    //    { "categories": { "Walls": [...], "Structural Columns, Floors": [...] } }
    //  Anahtarlar İNGİLİZCE kategori görünen adları (virgülle çoklu olabilir).
    //
    //  ÖNEMLİ: Element.Category.Name Revit UI diline göre yerelleşir (Türkçe
    //  Revit'te "Walls" değil "Duvarlar" döner). Bu yüzden görünen ad yerine
    //  dil-bağımsız BuiltInCategory ile eşleştirme yapılır — CATEGORY_NAME_MAP
    //  bu köprüyü kurar. Yeni bir kategori eklenirse (JSON'da yeni İngilizce ad
    //  görünürse) bu haritaya eklenmesi gerekir; eksikse sessizce atlanır ve
    //  Initialize() sonucunda loglanır (TaskDialog ile değil — arka planda,
    //  ManifestBrowserWindow'un lint cache'i gibi sessiz bir uyarı).
    // ═══════════════════════════════════════════════════════════════════════════
    public static class TrBimCategoryParamIndex
    {
        private static Dictionary<BuiltInCategory, List<string>>? _index;
        public static IReadOnlyList<string> UnmappedCategoryNames { get; private set; } = Array.Empty<string>();

        // İngilizce Category.Name → BuiltInCategory. "Project Information" ve
        // "Sheets" kasıtlı olarak dışarıda bırakıldı — bunlar seçilebilir model
        // elemanı değil (proje bilgisi tekil elemandır, sayfa ayrı bir akıştır).
        private static readonly Dictionary<string, BuiltInCategory> CATEGORY_NAME_MAP = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Air Terminals"]             = BuiltInCategory.OST_DuctTerminal,
            ["Cable Tray Fittings"]       = BuiltInCategory.OST_CableTrayFitting,
            ["Cable Trays"]               = BuiltInCategory.OST_CableTray,
            ["Ceilings"]                  = BuiltInCategory.OST_Ceilings,
            ["Communication Devices"]     = BuiltInCategory.OST_CommunicationDevices,
            ["Conduit Fittings"]          = BuiltInCategory.OST_ConduitFitting,
            ["Conduits"]                  = BuiltInCategory.OST_Conduit,
            ["Curtain Panels"]            = BuiltInCategory.OST_CurtainWallPanels,
            ["Curtain Wall Mullions"]     = BuiltInCategory.OST_CurtainWallMullions,
            ["Data Devices"]              = BuiltInCategory.OST_DataDevices,
            ["Doors"]                     = BuiltInCategory.OST_Doors,
            ["Duct Accessories"]          = BuiltInCategory.OST_DuctAccessory,
            ["Duct Fittings"]             = BuiltInCategory.OST_DuctFitting,
            ["Ducts"]                     = BuiltInCategory.OST_DuctCurves,
            ["Electrical Equipment"]      = BuiltInCategory.OST_ElectricalEquipment,
            ["Electrical Fixtures"]       = BuiltInCategory.OST_ElectricalFixtures,
            ["Fire Alarm Devices"]        = BuiltInCategory.OST_FireAlarmDevices,
            ["Floors"]                    = BuiltInCategory.OST_Floors,
            ["Generic Models"]            = BuiltInCategory.OST_GenericModel,
            ["Grids"]                     = BuiltInCategory.OST_Grids,
            ["Levels"]                    = BuiltInCategory.OST_Levels,
            ["Lighting Devices"]          = BuiltInCategory.OST_LightingDevices,
            ["Lighting Fixtures"]         = BuiltInCategory.OST_LightingFixtures,
            ["MEP Spaces"]                = BuiltInCategory.OST_MEPSpaces,
            ["Mechanical Equipment"]      = BuiltInCategory.OST_MechanicalEquipment,
            ["Parking"]                   = BuiltInCategory.OST_Parking,
            ["Pipe Accessories"]          = BuiltInCategory.OST_PipeAccessory,
            ["Pipe Fittings"]             = BuiltInCategory.OST_PipeFitting,
            ["Pipes"]                     = BuiltInCategory.OST_PipeCurves,
            ["Plumbing Fixtures"]         = BuiltInCategory.OST_PlumbingFixtures,
            ["Railings"]                  = BuiltInCategory.OST_StairsRailing,
            ["Ramps"]                     = BuiltInCategory.OST_Ramps,
            ["Rebar"]                     = BuiltInCategory.OST_Rebar,
            ["Roofs"]                     = BuiltInCategory.OST_Roofs,
            ["Rooms"]                     = BuiltInCategory.OST_Rooms,
            ["Security Devices"]          = BuiltInCategory.OST_SecurityDevices,
            ["Specialty Equipment"]       = BuiltInCategory.OST_SpecialityEquipment,
            ["Sprinklers"]                = BuiltInCategory.OST_Sprinklers,
            ["Stairs"]                    = BuiltInCategory.OST_Stairs,
            ["Structural Columns"]        = BuiltInCategory.OST_StructuralColumns,
            ["Structural Foundations"]    = BuiltInCategory.OST_StructuralFoundation,
            ["Structural Framing"]        = BuiltInCategory.OST_StructuralFraming,
            ["Walls"]                     = BuiltInCategory.OST_Walls,
            ["Windows"]                   = BuiltInCategory.OST_Windows,
        };

        public static void Initialize(string dataRoot)
        {
            _index = new Dictionary<BuiltInCategory, List<string>>();
            var unmapped = new HashSet<string>();
            var path = Path.Combine(dataRoot, "test", "trbim_category_param_index.json");
            if (!File.Exists(path)) return;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
                if (!doc.RootElement.TryGetProperty("categories", out var cats)) return;

                foreach (var prop in cats.EnumerateObject())
                {
                    var paramNames = prop.Value.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToList();

                    foreach (var rawName in prop.Name.Split(','))
                    {
                        var name = rawName.Trim();
                        if (name.Length == 0) continue;
                        if (name.Equals("Project Information", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("Sheets", StringComparison.OrdinalIgnoreCase))
                            continue;   // seçilebilir eleman kategorisi değil — kasıtlı atlandı

                        if (!CATEGORY_NAME_MAP.TryGetValue(name, out var bic))
                        {
                            unmapped.Add(name);
                            continue;
                        }

                        if (!_index.TryGetValue(bic, out var list))
                            _index[bic] = list = new List<string>();
                        foreach (var p in paramNames)
                            if (!list.Contains(p)) list.Add(p);
                    }
                }
            }
            catch { /* İndeks olmadan da panel çalışır — sadece "beklenen param" bölümü boş kalır. */ }

            UnmappedCategoryNames = unmapped.ToList();
        }

        /// <summary>Bu kategori için TR_BIM'in beklediği parametre adları — yoksa boş liste.</summary>
        public static IReadOnlyList<string> ExpectedParams(BuiltInCategory bic)
            => _index != null && _index.TryGetValue(bic, out var list) ? list : Array.Empty<string>();
    }
}
