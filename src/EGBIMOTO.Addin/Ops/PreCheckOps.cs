using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// v11 — Manifest "pre_checks" bloğunu gerçekten icra eden op'lar.
    ///
    /// Önceden manifest JSON'larındaki pre_checks alanı yalnızca ManifestLinter
    /// (statik analiz / linting) tarafından okunuyordu; DagExecutor (gerçek
    /// çalışma zamanı motoru) buna hiç dokunmuyordu. Sonuç: modelde gerekli
    /// elemanlar/parametreler/registry verisi yokken op zinciri sessizce
    /// boş/hatalı sonuç üretiyordu (özellikle kalıp hesaplarında gözlemlendi).
    ///
    /// DagExecutor.Run() her precheck için "precheck_&lt;type_lower&gt;" adlı
    /// bir op arar ve manifest'teki sırayla çağırır. Op (bool ok, string? msg)
    /// tuple'ı döner: ok=false ise on_fail politikasına göre ABORT veya WARN
    /// uygulanır.
    ///
    /// Yeni bir precheck tipi eklemek için:
    ///   [EgOp("precheck_yeni_tip")] public static (bool, string?) X(OpContext ctx)
    /// ve manifest JSON'da "type": "YENI_TIP" (büyük/küçük fark etmez).
    /// </summary>
    public static class PreCheckOps
    {
        // ── MODEL_HAS_ELEMENTS ───────────────────────────────────────────────
        // Verilen kategorilerden en az biri modelde min_count kadar eleman
        // içeriyor mu? (categories listesindeki kategorilerin TOPLAMI sayılır)
        [EgOp("precheck_model_has_elements",
            Description = "Modelde belirtilen kategorilerden yeterli sayıda eleman var mı kontrol eder",
            Category    = "PreCheck")]
        public static (bool, string?) ModelHasElements(OpContext ctx)
        {
            var rctx = (RevitOpContext)ctx;
            var cats = ctx.GetStringList("categories");
            int minCount = ctx.GetInt("min_count", 1);

            if (cats.Count == 0)
                return (true, null); // kategori belirtilmemişse kontrol anlamsız — geç

            int total = 0;
            var unresolved = new List<string>();

            foreach (var catName in cats)
            {
                if (!TryResolveCategory(catName, out var bic))
                {
                    unresolved.Add(catName);
                    continue;
                }

                total += new FilteredElementCollector(rctx.Doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
            }

            if (unresolved.Count > 0)
                ctx.Log($"  ⚠ precheck: tanınmayan kategori(ler): {string.Join(", ", unresolved)}");

            if (total >= minCount)
                return (true, null);

            return (false, $"Modelde {string.Join("/", cats)} kategorilerinden toplam {total} eleman bulundu, minimum {minCount} gerekli");
        }

        // ── element_exists — MODEL_HAS_ELEMENTS ile aynı semantik, farklı tip adı ──
        [EgOp("precheck_element_exists",
            Description = "MODEL_HAS_ELEMENTS ile aynı kontrolü yapar (eski manifest adlandırması)",
            Category    = "PreCheck")]
        public static (bool, string?) ElementExists(OpContext ctx) => ModelHasElements(ctx);

        // ── REGISTRY_KEY_EXISTS ──────────────────────────────────────────────
        // EgbimotoData.Registry (DataRegistry) içinde verilen key set edilmiş mi?
        [EgOp("precheck_registry_key_exists",
            Description = "EgbimotoData.Registry içinde belirtilen key'in dolu olup olmadığını kontrol eder",
            Category    = "PreCheck")]
        public static (bool, string?) RegistryKeyExists(OpContext ctx)
        {
            var key = ctx.GetString("key", "");
            if (string.IsNullOrWhiteSpace(key))
                return (true, null);

            if (EgbimotoData.Registry.IsCached(key))
                return (true, null);

            return (false, $"Registry key '{key}' henüz yüklenmemiş");
        }

        // ── FILE_EXISTS ───────────────────────────────────────────────────────
        // data/ kökü altındaki (veya mutlak) bir dosyanın varlığını kontrol eder.
        [EgOp("precheck_file_exists",
            Description = "Belirtilen dosyanın (data/ köküne göre veya mutlak) var olup olmadığını kontrol eder",
            Category    = "PreCheck")]
        public static (bool, string?) FileExists(OpContext ctx)
        {
            var path = ctx.GetString("path", "");
            if (string.IsNullOrWhiteSpace(path))
                return (true, null);

            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(EgbimotoData.DataRoot, path);

            if (File.Exists(fullPath))
                return (true, null);

            return (false, $"Dosya bulunamadı: {fullPath}");
        }

        // ── ACTIVE_DOCUMENT_EXISTS ───────────────────────────────────────────
        [EgOp("precheck_active_document_exists",
            Description = "Aktif bir Revit dokümanının açık olduğunu kontrol eder",
            Category    = "PreCheck")]
        public static (bool, string?) ActiveDocumentExists(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext;
            if (rctx?.Doc != null)
                return (true, null);

            return (false, "Aktif Revit dokümanı bulunamadı");
        }

        // ── PARAM_EXISTS ─────────────────────────────────────────────────────
        // Verilen kategorilerden en az bir elemanda, belirtilen parametre
        // tanımlı (ve null olmayan) mı? Genelde "önce ENSURE_PARAMS manifestini
        // çalıştırın" tipi bağımlılıkları yakalamak için kullanılır.
        [EgOp("precheck_param_exists",
            Description = "Belirtilen kategorideki elemanlarda parametrenin tanımlı olup olmadığını kontrol eder",
            Category    = "PreCheck")]
        public static (bool, string?) ParamExists(OpContext ctx)
        {
            var rctx  = (RevitOpContext)ctx;
            var param = ctx.GetString("param", "");
            var cats  = ctx.GetStringList("categories");

            if (string.IsNullOrWhiteSpace(param) || cats.Count == 0)
                return (true, null);

            foreach (var catName in cats)
            {
                if (!TryResolveCategory(catName, out var bic)) continue;

                var elements = new FilteredElementCollector(rctx.Doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .ToElements();

                foreach (var el in elements)
                {
                    var p = el.LookupParameter(param);
                    if (p != null) return (true, null); // en az bir elemanda tanımlı — yeterli
                }
            }

            return (false, $"'{param}' parametresi {string.Join("/", cats)} kategorilerindeki hiçbir elemanda tanımlı değil");
        }

        // ── FAMILY_LOADED ─────────────────────────────────────────────────────
        // Verilen aile adı (FamilyName) projeye yüklenmiş mi? (herhangi bir kategori)
        [EgOp("precheck_family_loaded",
            Description = "Belirtilen ailenin projeye yüklenmiş olup olmadığını kontrol eder",
            Category    = "PreCheck")]
        public static (bool, string?) FamilyLoaded(OpContext ctx)
        {
            var rctx     = (RevitOpContext)ctx;
            var famName  = ctx.GetString("family_name", "");
            if (string.IsNullOrWhiteSpace(famName))
                return (true, null);

            bool found = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Any(f => string.Equals(f.Name, famName, StringComparison.OrdinalIgnoreCase));

            if (found) return (true, null);

            return (false, $"'{famName}' ailesi projeye yüklenmemiş");
        }

        // ── PARAMETER_BOUND ───────────────────────────────────────────────────
        // PARAM_EXISTS'ten FARKI: elemanlarda değer arar (instance-bazlı, en az
        // bir elemanda dolu olmasını ister) — bu kontrol ise doc.ParameterBindings
        // üzerinden parametrenin projeye GERÇEKTEN bind edilip edilmediğini ve
        // hedef kategori(ler)e bağlı olup olmadığını sorar. Eleman sayısı sıfır
        // olsa bile (henüz model boşken) "bu parametre bu kategoriye bağlı mı"
        // sorusuna cevap verir — PCH_BWIC_reporter'daki get_parameter_binding()
        // deseninin pre_check karşılığı.
        //
        // Tipik kullanım: bir manifest çalışmadan önce "TR_KalipPozNo" gibi bir
        // shared/project parametresinin OST_StructuralColumns'a bağlı olduğunu
        // garanti altına almak — aksi halde write-back op'ları sessizce atlar.
        [EgOp("precheck_parameter_bound",
            Description = "Parametrenin projeye bind edilip edilmediğini ve belirtilen kategori(ler)e " +
                          "bağlı olup olmadığını doc.ParameterBindings üzerinden kontrol eder " +
                          "(eleman örneği gerekmez). params: param (string), categories (string[])",
            Category    = "PreCheck")]
        public static (bool, string?) ParameterBound(OpContext ctx)
        {
            var rctx  = (RevitOpContext)ctx;
            var param = ctx.GetString("param", "");
            var cats  = ctx.GetStringList("categories");

            if (string.IsNullOrWhiteSpace(param))
                return (true, null); // parametre belirtilmemişse kontrol anlamsız — geç

            // Hedef kategori Id'lerini çöz
            var targetCatIds = new HashSet<long>();
            var unresolved = new List<string>();
            foreach (var catName in cats)
            {
                if (!TryResolveCategory(catName, out var bic)) { unresolved.Add(catName); continue; }
                try
                {
                    var cat = Category.GetCategory(rctx.Doc, bic);
                    if (cat != null) targetCatIds.Add(Rv.GetId(cat.Id));
                }
                catch { unresolved.Add(catName); }
            }
            if (unresolved.Count > 0)
                ctx.Log($"  ⚠ precheck_parameter_bound: tanınmayan kategori(ler): {string.Join(", ", unresolved)}");

            var bindingMap = rctx.Doc.ParameterBindings;
            var iterator = bindingMap.ForwardIterator();
            iterator.Reset();

            bool paramFound = false;
            var boundCatIds = new HashSet<long>();

            while (iterator.MoveNext())
            {
                var definition = iterator.Key;
                if (!string.Equals(definition?.Name, param, StringComparison.OrdinalIgnoreCase)) continue;

                paramFound = true;
                var binding = iterator.Current as ElementBinding;
                var boundCategories = binding?.Categories;
                if (boundCategories == null) continue;

                foreach (Category c in boundCategories)
                    boundCatIds.Add(Rv.GetId(c.Id));

                break; // aynı isimde tek tanım olur — bulduktan sonra dur
            }

            if (!paramFound)
                return (false, $"'{param}' parametresi projede tanımlı (bind edilmiş) değil");

            // categories verilmemişse, parametrenin herhangi bir kategoriye
            // bind edilmiş olması yeterli kabul edilir.
            if (targetCatIds.Count == 0)
                return (true, null);

            if (targetCatIds.Overlaps(boundCatIds))
                return (true, null);

            return (false,
                $"'{param}' parametresi projede var ama {string.Join("/", cats)} kategorisine/kategorilerine " +
                "bağlı değil — Project/Shared Parameter ayarlarından kategoriyi ekleyin");
        }

        // ── Yardımcı: "OST_Walls" veya "Walls" / "PipeCurves" gibi her iki
        //    yazımı da BuiltInCategory enum değerine çözer.
        private static bool TryResolveCategory(string name, out BuiltInCategory bic)
        {
            if (Enum.TryParse(name, out bic)) return true;

            var withPrefix = name.StartsWith("OST_", StringComparison.OrdinalIgnoreCase)
                ? name
                : "OST_" + name;

            return Enum.TryParse(withPrefix, true, out bic);
        }
    }
}
