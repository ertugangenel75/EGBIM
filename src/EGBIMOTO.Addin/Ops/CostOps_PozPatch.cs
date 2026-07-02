// CostOps_PozPatch.cs
// Bu dosya egbimoto_v4_fixed/src/EGBIMOTO.Addin/Ops/CostOps.cs dosyasına EKLENECEKTİR.
// Mevcut PozMatch() ve PozMatchByCode() metotlarının yanına aşağıdaki yeni op'lar eklenir.
//
// EKLEME TALİMATI:
//   CostOps.cs içinde CostOps sınıfının son kapanış süslü parantezinden önce
//   aşağıdaki tüm bloğu yapıştırın.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    public static partial class CostOps
    {
        // ── Poz parametre adayları (PyCost CSB Adapter v7.0 sırası) ─────────
        private static readonly string[] _PozParamCandidates =
        {
            "TR_CSB_PozNo", "TR_CSB_POZ_NO", "TR_CSB_Poz", "TR_POZ_NO",
            "EGBIM_POZ_NO", "Keynote", "Assembly Code", "Type Mark"
        };

        private static readonly Regex _PozNoRegex =
            new(@"\d{2}\.\d{3}\.\d{4}", RegexOptions.Compiled);

        // ────────────────────────────────────────────────────────────────────
        // READ_PARAM_WITH_FALLBACK
        // ────────────────────────────────────────────────────────────────────
        [EgOp("read_param_with_fallback",
            Description = "Elemandan öncelikli parametre listesinden ilk dolu değeri okur. " +
                          "params: param_names (string[]), output_field (string). " +
                          "Çıktı: List<Dict> — element_id + output_field değeri.",
            Category    = "Parametre")]
        public static List<Dictionary<string, object?>> ReadParamWithFallback(OpContext ctx)
        {
            var rctx      = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Revit bağlamı gerektirir.");
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var paramNames= ctx.GetParam<List<string>>("param_names", new List<string>());
            var outField  = ctx.GetString("output_field", "value");

            int found = 0;
            var result = elements.Select(el =>
            {
                string val = "";
                foreach (var pname in paramNames)
                {
                    try
                    {
                        // Önce instance, sonra type
                        val = ReadParamText(el, pname);
                        if (string.IsNullOrEmpty(val))
                        {
                            var tid = el.GetTypeId();
                            var typ = rctx.Doc.GetElement(tid);
                            if (typ != null) val = ReadParamText(typ, pname);
                        }
                        if (!string.IsNullOrEmpty(val)) { found++; break; }
                    }
                    catch { }
                }

                return new Dictionary<string, object?>
                {
                    ["element_id"] = SafeEid(el),
                    [outField]     = val,
                    ["source"]     = string.IsNullOrEmpty(val) ? "MISSING" : "FOUND",
                };
            }).ToList();

            ctx.Log($"  read_param_with_fallback: {found}/{result.Count} değer bulundu");
            return result;
        }

        // ────────────────────────────────────────────────────────────────────
        // POZ_MATCH_KEYNOTE_AWARE
        // ────────────────────────────────────────────────────────────────────
        [EgOp("poz_match_keynote_aware",
            Description = "Eleman poz'unu üç aşamada çözer:\n" +
                          "  1. Keynote / TR_CSB_PozNo (instance → type, 8 aday parametre)\n" +
                          "  2. Keynote eşleşmezse: poz_canonical_map → STA_FORMWORK vb. → section prefix ile arama\n" +
                          "  3. Hiçbiri yoksa: kategori bazlı varsayılan poz\n" +
                          "Çıktı: input row listesi — poz_no, poz_adi, birim, birim_fiyat eklenerek döner.",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> PozMatchKeynoteAware(OpContext ctx)
        {
            var rctx    = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Revit bağlamı gerektirir.");
            var rows    = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();

            // Poz veritabanı (load_poz_data ile yüklenmeli)
            var pozData = EgbimotoData.Registry.Get("poz_data")
                as List<Dictionary<string, object?>> ?? new();
            var pozIndex = pozData.ToDictionary(
                p => p.TryGetValue("poz_no", out var k) ? k?.ToString() ?? "" : "",
                p => p, StringComparer.OrdinalIgnoreCase);

            // canonical_map (opsiyonel)
            var canonMap = EgbimotoData.Registry.Get("poz_canonical_map")
                as Dictionary<string, object?> ?? new();

            // section_rules (opsiyonel)
            var sectionRules = EgbimotoData.Registry.Get("poz_section_rules")
                as Dictionary<string, List<string>> ?? new();

            // Kategori → varsayılan poz
            var defaultPoz = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Structural Columns"]     = "15.180.1002",
                ["Structural Framing"]     = "15.180.1002",
                ["Structural Foundations"] = "15.180.1002",
                ["Floors"]                 = "15.180.1003",
                ["Walls"]                  = "15.180.1003",
                ["Ceilings"]               = "15.180.1003",
                ["Roofs"]                  = "15.180.1003",
            };

            int step1 = 0, step2 = 0, step3 = 0;

            foreach (var row in rows)
            {
                // Element'i bul
                Element? el = null;
                if (row.TryGetValue("element_id", out var eidObj) && eidObj != null)
                {
                    try
                    {
                        long eid = Convert.ToInt64(eidObj);
                        el = rctx.Doc.GetElement(Rv.MakeElementId(eid));  // v6
                    }
                    catch { }
                }

                string pozNo = "";

                // ── Aşama 1: Keynote / parametre ────────────────────────────
                if (el != null)
                {
                    pozNo = ResolvePozNoFromElement(rctx.Doc, el);
                    if (!string.IsNullOrEmpty(pozNo)) step1++;
                }

                // ── Aşama 2: BIC → canonical class → section_rules prefix ───
                if (string.IsNullOrEmpty(pozNo) && el != null)
                {
                    string canonClass = GuessCanonicalClass(el);
                    if (!string.IsNullOrEmpty(canonClass) &&
                        sectionRules.TryGetValue(canonClass, out var prefixes) &&
                        prefixes.Count > 0)
                    {
                        string prefix = prefixes[0];
                        var candidate = pozIndex.Keys
                            .Where(k => k.StartsWith(prefix))
                            .OrderBy(k => k)
                            .FirstOrDefault();
                        if (candidate != null) { pozNo = candidate; step2++; }
                    }
                }

                // ── Aşama 3: Kategori varsayılan ────────────────────────────
                if (string.IsNullOrEmpty(pozNo))
                {
                    string cat = row.TryGetValue("kategori", out var cv) ? cv?.ToString() ?? "" : "";
                    if (defaultPoz.TryGetValue(cat, out var dp)) { pozNo = dp; step3++; }
                    else pozNo = "15.180.1002";
                }

                // ── Canon map zenginleştirme ─────────────────────────────────
                // pozNo çözüldükten sonra canonical_map'ten disiplin/WBS/IFC metadata ekle
                if (!string.IsNullOrEmpty(pozNo) && canonMap.TryGetValue(pozNo, out var canonRaw))
                {
                    if (canonRaw is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (je.TryGetProperty("canonical_class", out var cc))
                            row["canonical_class"] = cc.GetString();
                        if (je.TryGetProperty("discipline", out var disc))
                            row["discipline"]       = disc.GetString();
                        if (je.TryGetProperty("wbs_group", out var wbs))
                            row["wbs_group"]        = wbs.GetString();
                        if (je.TryGetProperty("ifc_entity", out var ifc))
                            row["ifc_entity"]       = ifc.GetString();
                    }
                }

                // Poz bilgilerini satıra yaz
                if (pozIndex.TryGetValue(pozNo, out var pozItem))
                {
                    row["poz_no"]      = pozNo;
                    row["poz_adi"]     = pozItem.TryGetValue("tanim",      out var ta) ? ta : "";
                    row["birim"]       = pozItem.TryGetValue("birim",      out var bi) ? bi : "m2";
                    row["birim_fiyat"] = pozItem.TryGetValue("birim_fiyat", out var up) ? up : 0.0;
                }
                else
                {
                    row["poz_no"]      = pozNo;
                    row["poz_adi"]     = "—";
                    row["birim"]       = "m2";
                    row["birim_fiyat"] = 0.0;
                }
            }

            ctx.Log($"  poz_match_keynote_aware: Keynote={step1} Semantic={step2} Default={step3}");
            return rows;
        }

        // ────────────────────────────────────────────────────────────────────
        // LOAD_POZ_CANONICAL_MAP — Registry'ye yükle
        // ────────────────────────────────────────────────────────────────────
        [EgOp("load_poz_canonical_map",
            Description = "data/poz/poz_canonical_map.json dosyasını registry'ye yükler. " +
                          "poz_match_keynote_aware için ön koşul.",
            Category    = "ETL")]
        public static Dictionary<string, object?> LoadPozCanonicalMap(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Revit bağlamı gerektirir.");

            string dataPath = ctx.GetString("path", "");
            if (string.IsNullOrEmpty(dataPath))
            {
                // Önce DataRoot/poz/, sonra assembly yanı
                var dataRootPath = System.IO.Path.Combine(EgbimotoData.DataRoot, "poz", "poz_canonical_map.json");
                string asmDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                var asmPath = System.IO.Path.Combine(asmDir, "data", "poz", "poz_canonical_map.json");
                dataPath = System.IO.File.Exists(dataRootPath) ? dataRootPath : asmPath;
            }
            else if (!System.IO.Path.IsPathRooted(dataPath))
            {
                dataPath = System.IO.Path.Combine(EgbimotoData.DataRoot, dataPath);
            }

            if (!System.IO.File.Exists(dataPath))
            {
                ctx.Log($"  ⚠ load_poz_canonical_map: dosya bulunamadı: {dataPath}");
                return new() { ["loaded"] = 0, ["error"] = "FILE_NOT_FOUND" };
            }

            string json = System.IO.File.ReadAllText(dataPath, System.Text.Encoding.UTF8);
            var map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                      ?? new();
            EgbimotoData.Registry.Set("poz_canonical_map", map);

            ctx.Log($"  load_poz_canonical_map: {map.Count} poz yüklendi");
            return new() { ["loaded"] = map.Count, ["path"] = dataPath };
        }

        // ────────────────────────────────────────────────────────────────────
        // LOAD_POZ_SECTION_RULES — Registry'ye yükle
        // ────────────────────────────────────────────────────────────────────
        [EgOp("load_poz_section_rules",
            Description = "data/poz/poz_section_rules.json dosyasını registry'ye yükler. " +
                          "canonical_class → poz prefix listesi. poz_match_keynote_aware için.",
            Category    = "ETL")]
        public static Dictionary<string, object?> LoadPozSectionRules(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Revit bağlamı gerektirir.");

            string dataPath = ctx.GetString("path", "");
            if (string.IsNullOrEmpty(dataPath))
            {
                string asmDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                dataPath = System.IO.Path.Combine(asmDir, "data", "poz", "poz_section_rules.json");
            }

            if (!System.IO.File.Exists(dataPath))
            {
                ctx.Log($"  ⚠ load_poz_section_rules: dosya bulunamadı: {dataPath}");
                return new() { ["loaded"] = 0, ["error"] = "FILE_NOT_FOUND" };
            }

            string json = System.IO.File.ReadAllText(dataPath, System.Text.Encoding.UTF8);
            var rules = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, List<string>>>(json) ?? new();
            EgbimotoData.Registry.Set("poz_section_rules", rules);

            ctx.Log($"  load_poz_section_rules: {rules.Count} kural yüklendi");
            return new() { ["loaded"] = rules.Count, ["path"] = dataPath };
        }

        // ════════════════════════════════════════════════════════════════════
        // YARDIMCI METOTLAR
        // ════════════════════════════════════════════════════════════════════

        private static string ReadParamText(Element el, string pname)
        {
            try
            {
                var p = el.LookupParameter(pname);
                if (p == null) return "";
                string? v = p.AsString();
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                v = p.AsValueString();
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
            catch { }
            return "";
        }

        private static string NormalizePozNo(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var m = _PozNoRegex.Match(raw);
            return m.Success ? m.Value : "";
        }

        private static string ResolvePozNoFromElement(Document doc, Element el)
        {
            // Instance parametre
            foreach (var pname in _PozParamCandidates)
            {
                string v = NormalizePozNo(ReadParamText(el, pname));
                if (!string.IsNullOrEmpty(v)) return v;
            }
            // Type parametre
            try
            {
                var typ = doc.GetElement(el.GetTypeId());
                if (typ != null)
                {
                    foreach (var pname in _PozParamCandidates)
                    {
                        string v = NormalizePozNo(ReadParamText(typ, pname));
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                }
            }
            catch { }
            return "";
        }

        private static string GuessCanonicalClass(Element el)
        {
            // BIC bazlı hızlı tahmin (poz_section_rules canonical_class key'leriyle örtüşmeli)
            try
            {
                int bic;
                bic = Rv.GetCategoryId(el);  // v6: Rv adapter

                if (bic == (int)BuiltInCategory.OST_StructuralColumns)     return "STA_RC_COLUMN";
                if (bic == (int)BuiltInCategory.OST_StructuralFraming)     return "STA_RC_BEAM";
                if (bic == (int)BuiltInCategory.OST_StructuralFoundation)  return "STA_FOUNDATION_SLAB";
                if (bic == (int)BuiltInCategory.OST_Floors)                return "STA_RC_SLAB";
                if (bic == (int)BuiltInCategory.OST_Walls)
                {
                    var sp = el.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT);
                    return sp?.AsInteger() == 1 ? "STA_RC_WALL" : "MIM_WALL_PARTITION";
                }
                if (bic == (int)BuiltInCategory.OST_Roofs)    return "MIM_ROOF";
                if (bic == (int)BuiltInCategory.OST_Ceilings) return "MIM_CEILING";
                if (bic == (int)BuiltInCategory.OST_Rebar)    return "STA_REBAR";
                if (bic == (int)BuiltInCategory.OST_PipeCurves)  return "MEP_PIPE";
                if (bic == (int)BuiltInCategory.OST_DuctCurves)  return "MEP_DUCT";
            }
            catch { }
            return "";
        }

        private static long SafeEid(Element el)
        {
            // v6: Rv adapter — REVIT2024: .IntegerValue, 2025+: .Value
            try { return Rv.GetId(el.Id); }
            catch { return -1; }
        }
    }
}
