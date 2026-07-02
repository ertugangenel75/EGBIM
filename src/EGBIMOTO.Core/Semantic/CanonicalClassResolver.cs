using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EGBIMOTO.Core.Semantic
{
    /// <summary>
    /// Revit kategori / tip adından EGBIMOTO canonical class çözer.
    ///
    /// v3.2 değişiklikleri:
    ///   - semantic_classes.json'dan dinamik yükleme desteklenir.
    ///   - Reload() metodu ile runtime'da JSON yeniden yüklenebilir.
    ///   - JSON bulunamazsa hardcoded fallback devreye girer.
    ///
    /// JSON formatı (semantic_classes.json):
    /// {
    ///   "categoryMap": { "OST_Walls": "duvar", ... },
    ///   "typeRules":   [ { "keyword": "Perde", "class": "betonarme_duvar" }, ... ],
    ///   "disciplineMap": { "OST_Walls": "Mimari", ... }
    /// }
    /// </summary>
    public sealed class CanonicalClassResolver
    {
        private Dictionary<string, string>        _categoryMap;
        private List<(string keyword, string cls)> _typeRules;
        private Dictionary<string, string>        _disciplineMap;
        // v3.3 — OST BIC → canonical_classes_v3 key köprüsü (poz/WBS/IFC pipeline için)
        private Dictionary<string, string>        _canonicalV3Map;
        // v3.3 — TR canonical → v3 key köprüsü
        private Dictionary<string, string>        _trToV3Map;

        private static readonly string[] _searchPaths = new[]
        {
            "semantic_classes.json",
            "data/semantic_classes.json",
            "Resources/semantic_classes.json",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "semantic_classes.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "semantic_classes.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "semantic", "semantic_classes.json"),
        };

        public CanonicalClassResolver()
        {
            _categoryMap    = BuildFallbackCategoryMap();
            _typeRules      = BuildFallbackTypeRules();
            _disciplineMap  = BuildFallbackDisciplineMap();
            _canonicalV3Map = BuildFallbackCanonicalV3Map();
            _trToV3Map      = BuildFallbackTrToV3Map();
            TryLoadFromJson();
        }

        /// <summary>
        /// semantic_classes.json'u yeniden yükler.
        /// Dosya güncellendiğinde Revit'i yeniden başlatmadan çağrılabilir.
        /// </summary>
        public bool Reload() => TryLoadFromJson();

        private bool TryLoadFromJson()
        {
            foreach (var path in _searchPaths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var json = File.ReadAllText(path);
                    var doc  = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // categoryMap
                    if (root.TryGetProperty("categoryMap", out var catEl))
                    {
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in catEl.EnumerateObject())
                            map[prop.Name] = prop.Value.GetString() ?? "";
                        _categoryMap = map;
                    }

                    // typeRules
                    if (root.TryGetProperty("typeRules", out var rulesEl))
                    {
                        var rules = new List<(string, string)>();
                        foreach (var item in rulesEl.EnumerateArray())
                        {
                            var kw  = item.TryGetProperty("keyword", out var k) ? k.GetString() ?? "" : "";
                            var cls = item.TryGetProperty("class",   out var c) ? c.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(kw)) rules.Add((kw, cls));
                        }
                        _typeRules = rules;
                    }

                    // disciplineMap
                    if (root.TryGetProperty("disciplineMap", out var discEl))
                    {
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in discEl.EnumerateObject())
                            map[prop.Name] = prop.Value.GetString() ?? "";
                        _disciplineMap = map;
                    }

                    // canonicalV3Map — OST BIC → v3 key (yeni)
                    if (root.TryGetProperty("canonicalV3Map", out var v3El))
                    {
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in v3El.EnumerateObject())
                            map[prop.Name] = prop.Value.GetString() ?? "";
                        _canonicalV3Map = map;
                    }

                    // trToV3Map — TR canonical → v3 key (yeni)
                    if (root.TryGetProperty("trToV3Map", out var trV3El))
                    {
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in trV3El.EnumerateObject())
                            map[prop.Name] = prop.Value.GetString() ?? "";
                        _trToV3Map = map;
                    }

                    return true;
                }
                catch { }
            }
            return false;
        }

        /// <summary>TR canonical class döndürür (ör: "duvar", "betonarme_kolon")</summary>
        public string Resolve(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "bilinmeyen";

            // 1. Doğrudan kategori eşleşmesi
            if (_categoryMap.TryGetValue(input.Trim(), out var direct)) return direct;

            // 2. Tip adı anahtar kelime tarama
            foreach (var (kw, cls) in _typeRules)
                if (input.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return cls;

            // 3. Kategori kısmen içeriyorsa
            foreach (var (cat, cls) in _categoryMap)
                if (input.Contains(cat.Replace("OST_", ""), StringComparison.OrdinalIgnoreCase))
                    return cls;

            return "bilinmeyen";
        }

        /// <summary>
        /// canonical_classes_v3 key döndürür (ör: "STA_RC_COLUMN", "MIM_WALL").
        /// Poz eşleme, WBS atama, IFC export pipeline'ı için kullanılır.
        /// </summary>
        public string ResolveV3(string ostCategory)
        {
            if (string.IsNullOrWhiteSpace(ostCategory)) return "GEN_UNRESOLVED";
            if (_canonicalV3Map.TryGetValue(ostCategory.Trim(), out var v3)) return v3;
            // TR class üzerinden köprüle
            var trClass = Resolve(ostCategory);
            if (trClass != "bilinmeyen" && _trToV3Map.TryGetValue(trClass, out var v3via)) return v3via;
            return "GEN_UNRESOLVED";
        }

        /// <summary>TR canonical adından v3 key'e çevirir.</summary>
        public string TrToV3(string trClass)
            => _trToV3Map.TryGetValue(trClass, out var v3) ? v3 : "GEN_UNRESOLVED";

        public string ResolveDiscipline(string category)
            => _disciplineMap.TryGetValue(category, out var d) ? d : "Bilinmeyen";

        // ── Hardcoded fallback ─────────────────────────────────────────────────
        private static Dictionary<string, string> BuildFallbackCanonicalV3Map() =>
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["OST_Walls"]                = "MIM_WALL",
                ["OST_StructuralWalls"]      = "STA_RC_WALL",
                ["OST_Floors"]               = "STA_RC_SLAB",
                ["OST_Ceilings"]             = "MIM_CEILING",
                ["OST_Roofs"]                = "MIM_ROOF",
                ["OST_Doors"]                = "MIM_DOOR_WINDOW",
                ["OST_Windows"]              = "MIM_DOOR_WINDOW",
                ["OST_Rooms"]                = "MIM_MISC",
                ["OST_Stairs"]               = "MIM_MISC",
                ["OST_Ramps"]                = "MIM_MISC",
                ["OST_StructuralColumns"]    = "STA_RC_COLUMN",
                ["OST_StructuralFraming"]    = "STA_RC_BEAM",
                ["OST_StructuralFoundation"] = "STA_FOUNDATION_SLAB",
                ["OST_DuctCurves"]           = "MEK_DUCT",
                ["OST_FlexDuctCurves"]       = "MEK_DUCT",
                ["OST_PipeCurves"]           = "MEK_PIPE",
                ["OST_FlexPipeCurves"]       = "MEK_PIPE",
                ["OST_CableTray"]            = "ELK_CABLE",
                ["OST_Conduit"]              = "MEP_PIPE",
                ["OST_MechanicalEquipment"]  = "MEK_EQUIPMENT",
                ["OST_ElectricalEquipment"]  = "ELK_PANEL",
                ["OST_LightingFixtures"]     = "ELK_LIGHTING",
                ["OST_PlumbingFixtures"]     = "MEP_PIPE",
                ["OST_Rebar"]                = "STA_REBAR",
                ["OST_AreaRein"]             = "STA_REBAR",
                ["OST_Topography"]           = "ALT_EARTHWORKS",
                ["OST_GenericModel"]         = "MIM_GENERIC",
                ["OST_Columns"]              = "MIM_MISC",
                ["OST_Beams"]                = "STA_RC_BEAM",
            };

        private static Dictionary<string, string> BuildFallbackTrToV3Map() =>
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["betonarme_kolon"]    = "STA_RC_COLUMN",
                ["betonarme_kiri"]     = "STA_RC_BEAM",
                ["kiri"]               = "STA_RC_BEAM",
                ["betonarme_duvar"]    = "STA_RC_WALL",
                ["doseme"]             = "STA_RC_SLAB",
                ["temel"]              = "STA_FOUNDATION_SLAB",
                ["donati"]             = "STA_REBAR",
                ["duvar"]              = "MIM_WALL",
                ["kapi"]               = "MIM_DOOR_WINDOW",
                ["pencere"]            = "MIM_DOOR_WINDOW",
                ["tavan"]              = "MIM_CEILING",
                ["cerceve_catisi"]     = "MIM_ROOF",
                ["merdiven"]           = "MIM_MISC",
                ["rampa"]              = "MIM_MISC",
                ["mimar_kolon"]        = "MIM_MISC",
                ["oda"]                = "MIM_MISC",
                ["genel_model"]        = "MIM_GENERIC",
                ["topografya"]         = "ALT_EARTHWORKS",
                ["havalandirma_kanali"]= "MEK_DUCT",
                ["boru"]               = "MEK_PIPE",
                ["boru_iletkeni"]      = "MEP_PIPE",
                ["kablo_tavasi"]       = "ELK_CABLE",
                ["mekanik_ekipman"]    = "MEK_EQUIPMENT",
                ["elektrik_ekipman"]   = "ELK_PANEL",
                ["aydinlatma"]         = "ELK_LIGHTING",
                ["sihhi_tesisat"]      = "MEP_PIPE",
            };

        private static Dictionary<string, string> BuildFallbackCategoryMap() =>
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["OST_Walls"]                = "duvar",
                ["OST_StructuralWalls"]      = "betonarme_duvar",
                ["OST_Floors"]               = "doseme",
                ["OST_Ceilings"]             = "tavan",
                ["OST_Roofs"]                = "cerceve_catisi",
                ["OST_Doors"]                = "kapi",
                ["OST_Windows"]              = "pencere",
                ["OST_Rooms"]                = "oda",
                ["OST_Stairs"]               = "merdiven",
                ["OST_Ramps"]                = "rampa",
                ["OST_StructuralColumns"]    = "betonarme_kolon",
                ["OST_StructuralFraming"]    = "betonarme_kiri",
                ["OST_StructuralFoundation"] = "temel",
                ["OST_DuctCurves"]           = "havalandirma_kanali",
                ["OST_FlexDuctCurves"]       = "havalandirma_kanali",
                ["OST_PipeCurves"]           = "boru",
                ["OST_FlexPipeCurves"]       = "boru",
                ["OST_CableTray"]            = "kablo_tavasi",
                ["OST_Conduit"]              = "boru_iletkeni",
                ["OST_MechanicalEquipment"]  = "mekanik_ekipman",
                ["OST_ElectricalEquipment"]  = "elektrik_ekipman",
                ["OST_LightingFixtures"]     = "aydinlatma",
                ["OST_PlumbingFixtures"]     = "sihhi_tesisat",
                ["OST_Rebar"]                = "donati",
                ["OST_AreaRein"]             = "donati",
                ["OST_Topography"]           = "topografya",
                ["OST_GenericModel"]         = "genel_model",
                ["OST_Columns"]              = "mimar_kolon",
                ["OST_Beams"]                = "kiri",
            };

        private static List<(string keyword, string cls)> BuildFallbackTypeRules() => new()
        {
            ("Perde",      "betonarme_duvar"),
            ("Shear",      "betonarme_duvar"),
            ("Kagir",      "kagir_duvar"),
            ("Gazbeton",   "gazbeton_duvar"),
            ("Briket",     "briket_duvar"),
            ("Tuğla",      "tugla_duvar"),
            ("Bims",       "bims_duvar"),
            ("Mantolama",  "mantolama"),
            ("Yalıtım",    "yalitim"),
            ("Döşeme",     "doseme"),
            ("Plak",       "doseme"),
            ("Nervürlü",   "nervurlu_doseme"),
            ("Asmolen",    "asmolen_doseme"),
            ("Çift Yönlü", "capraz_doseme"),
            ("Temel",      "temel"),
            ("Radye",      "radye_temel"),
            ("Kazık",      "kazik_temel"),
            ("Sürekli",    "surekli_temel"),
            ("Kolonlu",    "betonarme_kolon"),
            ("Betonarme",  "betonarme"),
            ("Kiriş",      "betonarme_kiri"),
            ("Konsol",     "konsol"),
            ("Merdiven",   "merdiven"),
        };

        private static Dictionary<string, string> BuildFallbackDisciplineMap() =>
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["OST_Walls"]                = "Mimari",
                ["OST_Doors"]                = "Mimari",
                ["OST_Windows"]              = "Mimari",
                ["OST_Rooms"]                = "Mimari",
                ["OST_Floors"]               = "Mimari",
                ["OST_Ceilings"]             = "Mimari",
                ["OST_Stairs"]               = "Mimari",
                ["OST_Roofs"]                = "Mimari",
                ["OST_StructuralColumns"]    = "Yapısal",
                ["OST_StructuralWalls"]      = "Yapısal",
                ["OST_StructuralFraming"]    = "Yapısal",
                ["OST_StructuralFoundation"] = "Yapısal",
                ["OST_Rebar"]                = "Yapısal",
                ["OST_DuctCurves"]           = "MEP",
                ["OST_PipeCurves"]           = "MEP",
                ["OST_CableTray"]            = "MEP",
                ["OST_Conduit"]              = "MEP",
                ["OST_MechanicalEquipment"]  = "MEP",
                ["OST_ElectricalEquipment"]  = "MEP",
                ["OST_LightingFixtures"]     = "MEP",
                ["OST_PlumbingFixtures"]     = "MEP",
            };
    }
}
