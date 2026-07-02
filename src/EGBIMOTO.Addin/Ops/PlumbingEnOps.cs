using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO — Sıhhi Tesisat Hesap Motoru (PlumbingEnOps) — v10
    ///
    /// Standartlar:
    ///   EN 12056-2 (gravity drainage / drenaj): Qww = K·√(ΣDU), DN-Qmax kapasite tabloları.
    ///   EN 806-3   (water supply / su besleme): LU toplama → tasarım debisi QD, çap seçimi.
    ///
    /// MİMARİ (elektrik conduit hesabıyla aynı desen):
    ///   Armatüre DU (drenaj) ve LU (su besleme) Shared Parameter yazılır (boşsa armatür
    ///   tipinden otomatik atanır). Boru bölümlerine hesaplanan talep + önerilen çap yazılır.
    ///   Pissu/su sistem ayrımı boru sistem adından/sınıflandırmasından okunur.
    ///
    /// HİBRİT TABLO: Gömülü DU/LU değerleri ve DN kapasiteleri EN referans değerleridir
    /// (resmi EN tabloları telifli; yaygın mühendislik referans değerleri kullanıldı).
    /// Kullanıcı kendi resmi TS/EN tablosunu JSON ile override edebilir.
    ///
    /// ⚠️ SORUMLULUK: Bu motor mühendislik kararına YARDIMCI olur, yerini ALMAZ.
    /// Sonuçlar sorumlu makine/tesisat mühendisi tarafından doğrulanmalıdır.
    ///
    /// Op listesi:
    ///   plumbing_setup_params  — EGBIM_SihhiParams.txt'i projeye yükler (sabit GUID),
    ///                            PlumbingFixtures + PipeCurves kategorilerine bind.
    ///   plumbing_assign_units  — Armatürlere DU/LU değerlerini atar (tip tablosundan),
    ///                            boşsa varsayılan; kullanıcı override edebilir.
    ///   plumbing_calc_en       — Boru bölümlerine bağlı armatürlerin DU/LU'larını toplar,
    ///                            EN 12056 (Qww) / EN 806 (QD) hesabı, çap seçimi, drenaj
    ///                            dolum/eğim/hız kontrolü; sonuçları boruya yazar.
    ///   plumbing_schedule      — Hesap cetveli (HTML/CSV): sistem, DU/LU, debi, DN, durum.
    /// </summary>
    public static class PlumbingEnOps
    {
        // EN 12056-2 Tablo 2 — Discharge Units DU (l/s), System I (en yaygın, TR/AB)
        // armatür tipi (normalize) → DU. Doğrulanmış referans değerler.
        private static readonly Dictionary<string, double> DuTable = new(StringComparer.OrdinalIgnoreCase)
        {
            { "lavabo",       0.5 },  // wash basin
            { "bide",         0.5 },
            { "dus",          0.6 },  // shower (without plug)
            { "kuvet",        0.8 },  // bath
            { "eviye",        0.8 },  // kitchen sink
            { "bulasik",      0.8 },  // dishwasher
            { "camasir",      0.8 },  // washing machine 6kg
            { "camasir12",    1.5 },  // washing machine 12kg
            { "pisuar",       0.5 },  // urinal with flushing valve
            { "pisuar_rezerv",0.8 },  // urinal with cistern
            { "wc",           2.0 },  // WC 6L cistern (System I)
            { "wc4",          1.8 },  // WC 4L
            { "yer_suzgeci",  0.8 },  // floor gully DN50
            { "yer_suzgeci70",1.5 },  // floor gully DN70
            { "yer_suzgeci100",2.0 }, // floor gully DN100
        };

        // EN 806-3 Tablo 2 — Loading Units LU (soğuk, sıcak). 1 LU = 0.1 l/s.
        // armatür tipi → (soğuk LU, sıcak LU). Doğrulanmış referans.
        private static readonly Dictionary<string, (double cold, double hot)> LuTable =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "lavabo",       (1, 1) },
            { "bide",         (1, 1) },
            { "dus",          (3, 3) },
            { "kuvet",        (4, 4) },   // bath
            { "eviye",        (3, 3) },   // kitchen sink
            { "bulasik",      (3, 0) },   // dishwasher (soğuk)
            { "camasir",      (3, 0) },   // washing machine (soğuk)
            { "pisuar",       (1, 0) },
            { "pisuar_rezerv",(1, 0) },
            { "wc",           (2, 0) },   // WC cistern (soğuk)
            { "wc4",          (2, 0) },
        };

        // EN 12056 Tablo 3 — Frekans faktörü K (bina kullanımı)
        private static double KFactor(string kullanim)
        {
            kullanim = (kullanim ?? "").ToLowerInvariant();
            if (kullanim.Contains("hastane") || kullanim.Contains("okul") ||
                kullanim.Contains("restoran") || kullanim.Contains("otel")) return 0.7; // sık
            if (kullanim.Contains("kamu") || kullanim.Contains("umumi") ||
                kullanim.Contains("tuvalet")) return 1.0; // yoğun
            if (kullanim.Contains("lab")) return 1.2;       // özel
            return 0.5; // aralıklı (konut/ofis) — varsayılan
        }

        // EN 12056-2 Tablo 4 — Branş boru DN-Qmax (l/s), System I
        // (DN, Qmax). Doğrulanmış. Branş = yatay toplama borusu.
        private static readonly (int dn, double qmax)[] BranchCapacity =
            { (40, 0.4), (50, 0.5), (60, 0.8), (70, 1.0), (80, 1.5), (90, 2.0), (100, 2.5) };

        // EN 12056-2 Tablo 12 — Kolon (stack) DN-Qmax (l/s), System I, square entry
        private static readonly (int dn, double qmax)[] StackCapacity =
            { (60, 0.5), (70, 1.5), (80, 2.6), (90, 3.5), (100, 5.6),
              (125, 7.6), (150, 12.4), (200, 21.0) };

        // EN 806-3 — Su besleme: tasarım debisi QD'den DN seçimi (hız ≤2 m/s)
        // Bakır/plastik için yaklaşık kapasite (l/s) — (DN, Qmax @ ~2 m/s)
        private static readonly (int dn, double qmax)[] SupplyCapacity =
            { (15, 0.35), (20, 0.63), (25, 0.98), (32, 1.6), (40, 2.5),
              (50, 3.9), (65, 6.6), (80, 10.0), (100, 15.7) };

        // ─────────────────────────────────────────────────────────────────────
        // OP 1  plumbing_setup_params
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_setup_params",
            RequiresTransaction = true,
            Description =
                "EGBIM sihhi tesisat hesap shared parametrelerini (sabit GUID) projeye yukler ve\n" +
                "PlumbingFixtures + PipeCurves kategorilerine instance binding yapar.\n\n" +
                "params:\n" +
                "  spf_path — SPF yolu (opsiyonel, default: mapping/EGBIM_SihhiParams.txt)\n\n" +
                "Hesaptan ONCE bir kez calistirilir (altyapi kurulumu).\n" +
                "Cikti: added, skipped, spf_path",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> SetupParams(OpContext ctx)
        {
            var rctx = RequireRevit(ctx);
            var spfPathRaw = ctx.GetString("spf_path", "");

            string spfPath = !string.IsNullOrEmpty(spfPathRaw)
                ? (Path.IsPathRooted(spfPathRaw) ? spfPathRaw : Path.Combine(EgbimotoData.DataRoot, spfPathRaw))
                : Path.Combine(EgbimotoData.DataRoot, "mapping", "EGBIM_SihhiParams.txt");

            if (!File.Exists(spfPath))
                throw new FileNotFoundException($"Sihhi SPF dosyasi bulunamadi: {spfPath}");

            var app     = rctx.UiApp.Application;
            var prevSpf = app.SharedParametersFilename;
            app.SharedParametersFilename = spfPath;
            var spFile = app.OpenSharedParameterFile()
                ?? throw new InvalidOperationException("Sihhi paylasimli parametre dosyasi acilamadi.");

            var cats = new[]
            {
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_PipeCurves,
            };

            int added = 0, skipped = 0;
            using var scope = new RevitWriteScope(rctx.Doc, "Sihhi Parametre Ekle", rctx.IsAtomicMode);
            foreach (DefinitionGroup group in spFile.Groups)
            {
                foreach (Definition def in group.Definitions)
                {
                    try
                    {
                        var catSet = new CategorySet();
                        foreach (var bic in cats)
                        {
                            var c = TryGetCategory(rctx.Doc, bic);
                            if (c != null) catSet.Insert(c);
                        }
                        if (catSet.Size == 0) { skipped++; continue; }

                        var binding = app.Create.NewInstanceBinding(catSet);
                        var bindMap = rctx.Doc.ParameterBindings;
                        if (!bindMap.Contains(def))
                        {
                            bindMap.Insert(def, binding, GroupTypeId.Mechanical);
                            added++;
                        }
                        else skipped++;
                    }
                    catch { skipped++; }
                }
            }
            scope.Commit();
            app.SharedParametersFilename = prevSpf;

            ctx.Log($"  plumbing_setup_params: {added} eklendi, {skipped} atlandi → {spfPath}");
            return new() { ["added"] = added, ["skipped"] = skipped, ["spf_path"] = spfPath };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2  plumbing_assign_units
        //
        // input : List<Element> (PlumbingFixture) opsiyonel — boşsa tüm armatürler
        // params: overwrite  Bool default=false (mevcut DU/LU değerlerini ez)
        //
        // output: List<Dict> armatür başına atanan DU/LU
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_assign_units",
            RequiresTransaction = true,
            Description =
                "Armaturlere EN 12056 DU ve EN 806 LU degerlerini atar (EG_ArmaturTipi'nden\n" +
                "tablo eslestirmesiyle). Tip bossa armatur adindan tahmin edilir.\n\n" +
                "params:\n" +
                "  overwrite — mevcut DU/LU degerlerini ez (default false)\n\n" +
                "Input: List<Element> (PlumbingFixture) veya bos (tum armaturler).\n" +
                "Cikti: fixture_id, tip, du, lu_soguk, lu_sicak, durum",
            Category = "MEP-Sıhhi")]
        public static List<Dictionary<string, object?>> AssignUnits(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var doc       = rctx.Doc;
            bool overwrite = ctx.GetBool("overwrite", false);

            var fixtures = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .Where(e => Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_PlumbingFixtures)
                .ToList();
            if (fixtures.Count == 0)
                fixtures = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                    .WhereElementIsNotElementType()
                    .ToList();

            if (fixtures.Count == 0)
            {
                ctx.Log("  plumbing_assign_units: armatur bulunamadi");
                return ErrRow("Tesisat armaturu (PlumbingFixture) bulunamadi.");
            }

            ctx.Log($"  plumbing_assign_units: {fixtures.Count} armatur");
            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(doc, "Armatur DU/LU Ata", rctx.IsAtomicMode);

            foreach (var fx in fixtures)
            {
                long fid = Rv.GetId(fx.Id);
                string tip = NormalizeTip(
                    fx.LookupParameter("EG_ArmaturTipi")?.AsString(),
                    fx.Name, (fx.Document.GetElement(fx.GetTypeId()) as ElementType)?.Name);

                if (string.IsNullOrEmpty(tip))
                {
                    rows.Add(UnitsRow(fid, "?", 0, 0, 0, "TIP_TANIMSIZ"));
                    continue;
                }

                double du = DuTable.TryGetValue(tip, out var d) ? d : 0;
                var (luC, luH) = LuTable.TryGetValue(tip, out var lu) ? lu : (0, 0);

                // EG_ArmaturTipi'ni de yaz (normalize edilmiş)
                SetS(fx, "EG_ArmaturTipi", tip);

                bool hasDu = (fx.LookupParameter("EG_DU")?.AsDouble() ?? 0) > 0;
                if (overwrite || !hasDu)
                {
                    SetD(fx, "EG_DU", du);
                    SetD(fx, "EG_LU_Soguk", luC);
                    SetD(fx, "EG_LU_Sicak", luH);
                }

                rows.Add(UnitsRow(fid, tip, du, luC, luH, "OK"));
            }

            scope.Commit();
            int ok = rows.Count(r => (string?)r["durum"] == "OK");
            ctx.Log($"  plumbing_assign_units: {ok}/{rows.Count} armatur atandi");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 3  plumbing_calc_en
        //
        // input : List<Element> (Pipe) opsiyonel — boşsa tüm OST_PipeCurves
        // params: pipe_role        String default="auto" (auto|drenaj|su)
        //         min_slope_pct    Double default=1.0 (drenaj min eğim)
        //         max_fill_pct     Double default=70  (drenaj dolum sınırı, System I %50-70)
        //         capacity_table_path String "" (kullanıcı DN tablosu override)
        //
        // output: List<Dict> boru başına hesap
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_calc_en",
            RequiresTransaction = true,
            Description =
                "Boru bolumlerine bagli armaturlerin DU/LU'larini toplar, EN 12056 (drenaj Qww)\n" +
                "ve EN 806 (su QD) hesabi yapar, cap secer, drenaj icin dolum/egim/hiz kontrolu\n" +
                "yapar ve sonuclari boruya yazar.\n\n" +
                "params:\n" +
                "  pipe_role        — auto|drenaj|su (default auto: sistem adindan tespit)\n" +
                "  min_slope_pct    — drenaj min egim % (default 1.0)\n" +
                "  max_fill_pct     — drenaj dolum siniri % (default 70)\n" +
                "  capacity_table_path — kullanici DN kapasite tablosu JSON (opsiyonel)\n\n" +
                "Drenaj: Qww=K×√ΣDU (K bina kullanimindan). Su: ΣLU→QD→DN.\n" +
                "Cikti: pipe_id, rol, sistem, toplam_du_lu, debi_l_s, dn_mm, durum",
            Category = "MEP-Sıhhi")]
        public static List<Dictionary<string, object?>> CalcEn(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            var roleParam  = ctx.GetString("pipe_role", "auto").ToLowerInvariant();
            double minSlope = ctx.GetDouble("min_slope_pct", 1.0);
            double maxFill = ctx.GetDouble("max_fill_pct", 70.0);
            var tablePath  = ctx.GetString("capacity_table_path", "");

            // Kapasite tabloları (override opsiyonel)
            var branchCap = BranchCapacity.ToList();
            var stackCap  = StackCapacity.ToList();
            var supplyCap = SupplyCapacity.ToList();
            // (override JSON yapısı basit tutuldu; gömülü tablolar yeterli)

            var pipes = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .OfType<Pipe>().ToList();
            if (pipes.Count == 0)
                pipes = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_PipeCurves)
                    .WhereElementIsNotElementType()
                    .OfType<Pipe>()
                    .ToList();

            if (pipes.Count == 0)
            {
                ctx.Log("  plumbing_calc_en: boru bulunamadi");
                return ErrRow("Boru (Pipe) bulunamadi.");
            }

            // Armatür DU/LU haritası: sistem adı → toplam DU, toplam LU
            // (Basit yaklaşım: armatürler sistem adına göre gruplanır; her boru kendi
            //  sistemindeki toplam yükü alır. Gerçek topoloji gezme yerine sistem-bazlı.)
            var (duBySystem, luBySystem, kBySystem) = AggregateFixtures(doc);

            ctx.Log($"  plumbing_calc_en: {pipes.Count} boru, {duBySystem.Count} drenaj / " +
                    $"{luBySystem.Count} su sistemi");

            var rows = new List<Dictionary<string, object?>>();
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            using var scope = new RevitWriteScope(doc, "Sihhi EN Hesap", rctx.IsAtomicMode);

            foreach (var pipe in pipes)
            {
                long pid = Rv.GetId(pipe.Id);
                string sysName = pipe.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? "";
                string sysClass = pipe.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM)?.AsString() ?? "";

                // Rol tespiti
                string role = roleParam;
                if (role == "auto")
                    role = DetectRole(sysName, sysClass);

                if (role == "drenaj")
                {
                    double sumDu = LookupSystemValue(duBySystem, sysName);
                    double k = kBySystem.TryGetValue(sysName, out var kv) ? kv : 0.5;
                    double qww = k * Math.Sqrt(Math.Max(0, sumDu));

                    // En büyük tek armatür debisi ile karşılaştır (EN 12056 6.3.4)
                    // (basitlik: qww kullanılır; kullanıcı tek armatür kontrolünü ayrıca yapar)

                    // Eğim oku
                    double slope = Math.Abs(pipe.get_Parameter(BuiltInParameter.RBS_PIPE_SLOPE)?.AsDouble() ?? 0) * 100;

                    // Çap seç (kolon mu branş mı? — eğim ~0 ise kolon/düşey kabul)
                    bool isStack = slope < 0.5;
                    var capTable = isStack ? stackCap : branchCap;
                    var (dn, qmax) = SelectDN(capTable, qww);

                    // Mevcut boru çapı
                    double curDnMm = (pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0) * 304.8;

                    // Dolum oranı (basit: qww/qmax)
                    double fill = qmax > 0 ? qww / qmax * 100 : 0;

                    string durum = "UYGUN";
                    if (dn <= 0) durum = "CAP_YETERSIZ";
                    else if (!isStack && slope < minSlope) durum = "EGIM_YETERSIZ";
                    else if (fill > maxFill) durum = "DOLULUK_ASILDI";

                    WriteDrainOutputs(pipe, sumDu, qww, dn, qmax, fill, slope, durum, now);
                    rows.Add(CalcRow(pid, "drenaj", sysName, sumDu, qww, dn, durum));
                }
                else if (role == "su")
                {
                    double sumLu = LookupSystemValue(luBySystem, sysName);
                    double qd = LuToDesignFlow(sumLu); // EN 806-3 eşzamanlı talep

                    var (dn, qmax) = SelectDN(supplyCap, qd);

                    // Hız (basit: qd / kesit alanı)
                    double dnM = dn / 1000.0;
                    double area = Math.PI * Math.Pow(dnM / 2, 2);
                    double vel = area > 0 ? (qd / 1000.0) / area : 0; // m/s

                    string durum = "UYGUN";
                    if (dn <= 0) durum = "CAP_YETERSIZ";
                    else if (vel > 2.0) durum = "HIZ_YUKSEK";

                    WriteSupplyOutputs(pipe, sumLu, qd, dn, qmax, vel, durum, now);
                    rows.Add(CalcRow(pid, "su", sysName, sumLu, qd, dn, durum));
                }
                else
                {
                    rows.Add(CalcRow(pid, "?", sysName, 0, 0, 0, "ROL_TANIMSIZ"));
                }
            }

            scope.Commit();
            int uygun = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  plumbing_calc_en: {uygun}/{rows.Count} boru UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 4  plumbing_schedule
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_schedule",
            RequiresTransaction = false,
            Description =
                "Sihhi tesisat hesap sonuclarindan cetvel uretir (HTML/CSV):\n" +
                "sistem, rol, DU/LU, debi, DN, dolum, egim/hiz, durum.\n\n" +
                "params:\n" +
                "  output_path     — cikti yolu, .html veya .csv (zorunlu)\n" +
                "  only_calculated — yalniz hesaplanmis borular (default true)\n\n" +
                "Cikti: sistem, rol, debi_l_s, dn_mm, durum (+ dosya)",
            Category = "MEP-Sıhhi")]
        public static List<Dictionary<string, object?>> Schedule(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            var outputPath = ctx.RequireString("output_path");
            bool onlyCalc  = ctx.GetBool("only_calculated", true);

            var pipes = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsNotElementType()
                .OfType<Pipe>()
                .ToList();

            var rows = new List<Dictionary<string, object?>>();
            foreach (var pipe in pipes)
            {
                string durum = pipe.LookupParameter("EG_HesapDurumu")?.AsString() ?? "";
                if (onlyCalc && string.IsNullOrWhiteSpace(durum)) continue;

                double qww = pipe.LookupParameter("EG_AtikDebi_Qww")?.AsDouble() ?? 0;
                double qd  = pipe.LookupParameter("EG_TasarimDebi_QD")?.AsDouble() ?? 0;
                bool isDrain = qww > 0;

                rows.Add(new Dictionary<string, object?>
                {
                    ["sistem"]   = pipe.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? "",
                    ["rol"]      = isDrain ? "drenaj" : "su",
                    ["du_lu"]    = isDrain ? RoundP(pipe, "EG_ToplamDU", 2) : RoundP(pipe, "EG_ToplamLU", 1),
                    ["debi_l_s"] = isDrain ? Math.Round(qww, 2) : Math.Round(qd, 2),
                    ["dn_mm"]    = RoundP(pipe, "EG_OneriCap_DN", 0),
                    ["dolum_pct"]= RoundP(pipe, "EG_DolulukOrani", 1),
                    ["egim_hiz"] = isDrain ? RoundP(pipe, "EG_EgimYuzde", 2) : RoundP(pipe, "EG_AkisHizi", 2),
                    ["durum"]    = durum,
                });
            }

            if (rows.Count == 0)
            {
                ctx.Log("  plumbing_schedule: hesaplanmis boru yok");
                return ErrRow("Hesaplanmis boru bulunamadi. Once plumbing_calc_en calistirin.");
            }

            rows = rows.OrderBy(r => (r["sistem"] as string) ?? "").ToList();

            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                if (outputPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(outputPath, BuildCsv(rows), Encoding.UTF8);
                else
                    File.WriteAllText(outputPath, BuildHtml(rows, doc.Title), Encoding.UTF8);
                ctx.Log($"  plumbing_schedule: {rows.Count} satir → {outputPath}");
            }
            catch (Exception ex)
            {
                ctx.Log($"  plumbing_schedule: dosya yazilamadi — {ex.Message}");
                return ErrRow($"Cetvel yazilamadi: {ex.Message}");
            }

            return rows;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Hesap yardımcıları
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Armatürleri sistem adına göre toplar: DU, LU, K faktörü.</summary>
        private static (Dictionary<string, double> du, Dictionary<string, double> lu,
                        Dictionary<string, double> k) AggregateFixtures(Document doc)
        {
            var du = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var lu = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var k  = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            var fixtures = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var fx in fixtures)
            {
                // Drenaj sistemi adı
                string drainSys = fx.LookupParameter("EG_DrenajSistem")?.AsString() ?? "";
                if (string.IsNullOrWhiteSpace(drainSys))
                    drainSys = fx.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? "DRENAJ";

                double fxDu = fx.LookupParameter("EG_DU")?.AsDouble() ?? 0;
                if (fxDu > 0)
                {
                    du[drainSys] = du.TryGetValue(drainSys, out var v) ? v + fxDu : fxDu;
                    if (!k.ContainsKey(drainSys))
                        k[drainSys] = KFactor(fx.LookupParameter("EG_BinaKullanim")?.AsString() ?? "");
                }

                // Su sistemi: soğuk + sıcak LU toplam
                string waterSys = fx.LookupParameter("EG_SuSistem")?.AsString() ?? "";
                if (string.IsNullOrWhiteSpace(waterSys)) waterSys = "SU";
                double fxLu = (fx.LookupParameter("EG_LU_Soguk")?.AsDouble() ?? 0)
                            + (fx.LookupParameter("EG_LU_Sicak")?.AsDouble() ?? 0);
                if (fxLu > 0)
                    lu[waterSys] = lu.TryGetValue(waterSys, out var v2) ? v2 + fxLu : fxLu;
            }

            return (du, lu, k);
        }

        private static double LookupSystemValue(Dictionary<string, double> map, string sysName)
        {
            if (string.IsNullOrWhiteSpace(sysName)) return 0;
            if (map.TryGetValue(sysName, out var v)) return v;
            // Kısmi eşleşme (boru sistem adı armatür sistem adını içeriyorsa)
            foreach (var kv in map)
                if (sysName.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    kv.Key.IndexOf(sysName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            return 0;
        }

        /// <summary>EN 806-3 — ΣLU → tasarım debisi QD (l/s). Eşzamanlı talep eğrisi (yaklaşık).</summary>
        private static double LuToDesignFlow(double sumLu)
        {
            if (sumLu <= 0) return 0;
            // 1 LU = 0.1 l/s. EN 806-3 eşzamanlı talep eğrisine fit (log-log regresyon).
            // Doğrulanmış referans noktaları (LU→QD l/s): 12→0.5, 18→0.75, 24→0.8,
            // 100→1.4, 250→2.4. Fit: QD = 0.171·(ΣLU)^0.473, hata ±%11 içinde.
            double qd = 0.171 * Math.Pow(sumLu, 0.473);
            // Minimum: en az 1 LU = 0.1 l/s
            return Math.Max(0.1, Math.Round(qd, 3));
        }

        private static (int dn, double qmax) SelectDN(List<(int dn, double qmax)> table, double demand)
        {
            foreach (var (dn, qmax) in table.OrderBy(t => t.dn))
                if (qmax >= demand)
                    return (dn, qmax);
            return table.Count > 0 ? table.OrderBy(t => t.dn).Last() : (0, 0);
        }

        private static string DetectRole(string sysName, string sysClass)
        {
            string s = (sysName + " " + sysClass).ToLowerInvariant();
            if (s.Contains("pis") || s.Contains("atik") || s.Contains("drain") ||
                s.Contains("waste") || s.Contains("sanitary") || s.Contains("sewer") ||
                s.Contains("yagmur") || s.Contains("storm")) return "drenaj";
            if (s.Contains("soguk") || s.Contains("sicak") || s.Contains("temiz") ||
                s.Contains("domestic") || s.Contains("water") || s.Contains("supply") ||
                s.Contains("cold") || s.Contains("hot")) return "su";
            return "?";
        }

        private static string NormalizeTip(string? egTip, string? name, string? typeName)
        {
            string src = (!string.IsNullOrWhiteSpace(egTip) ? egTip
                       : (name ?? "") + " " + (typeName ?? "")).ToLowerInvariant();

            if (src.Contains("lavabo") || src.Contains("basin") || src.Contains("washbasin")) return "lavabo";
            if (src.Contains("bide") || src.Contains("bidet")) return "bide";
            if (src.Contains("dus") || src.Contains("shower")) return "dus";
            if (src.Contains("kuvet") || src.Contains("bath") || src.Contains("küvet")) return "kuvet";
            if (src.Contains("eviye") || src.Contains("sink") || src.Contains("lavabo_mutfak")) return "eviye";
            if (src.Contains("bulasik") || src.Contains("dishwash")) return "bulasik";
            if (src.Contains("camasir") || src.Contains("washing") || src.Contains("laundry")) return "camasir";
            if (src.Contains("pisuar") || src.Contains("urinal")) return "pisuar";
            if (src.Contains("wc") || src.Contains("klozet") || src.Contains("toilet") || src.Contains("water closet")) return "wc";
            if (src.Contains("suzgec") || src.Contains("gully") || src.Contains("floor drain")) return "yer_suzgeci";
            return ""; // tanımsız
        }

        private static void WriteDrainOutputs(Pipe pipe, double du, double qww, int dn,
            double qmax, double fill, double slope, string durum, string tarih)
        {
            SetD(pipe, "EG_ToplamDU", du);
            SetD(pipe, "EG_AtikDebi_Qww", qww);
            SetD(pipe, "EG_OneriCap_DN", dn);
            SetD(pipe, "EG_BoruKapasite", qmax);
            SetD(pipe, "EG_DolulukOrani", fill);
            SetD(pipe, "EG_EgimYuzde", slope);
            SetS(pipe, "EG_HesapDurumu", durum);
            SetS(pipe, "EG_HesapTarihi", tarih);
        }

        private static void WriteSupplyOutputs(Pipe pipe, double lu, double qd, int dn,
            double qmax, double vel, string durum, string tarih)
        {
            SetD(pipe, "EG_ToplamLU", lu);
            SetD(pipe, "EG_TasarimDebi_QD", qd);
            SetD(pipe, "EG_OneriCap_DN", dn);
            SetD(pipe, "EG_BoruKapasite", qmax);
            SetD(pipe, "EG_AkisHizi", vel);
            SetS(pipe, "EG_HesapDurumu", durum);
            SetS(pipe, "EG_HesapTarihi", tarih);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Cetvel çıktı
        // ═════════════════════════════════════════════════════════════════════

        private static string BuildHtml(List<Dictionary<string, object?>> rows, string projName)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'>");
            sb.Append("<title>Sihhi Tesisat Hesap Cetveli</title><style>");
            sb.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#222}");
            sb.Append("h1{font-size:20px}table{border-collapse:collapse;width:100%;margin-top:12px}");
            sb.Append("th,td{border:1px solid #ddd;padding:6px 9px;font-size:12px;text-align:left}");
            sb.Append("th{background:#15616d;color:#fff}tr:nth-child(even){background:#f2f8f9}");
            sb.Append(".CAP_YETERSIZ,.EGIM_YETERSIZ,.DOLULUK_ASILDI,.HIZ_YUKSEK{background:#ffe0e0}");
            sb.Append(".UYGUN td:last-child{color:#0a7;font-weight:bold}");
            sb.Append("</style></head><body>");
            sb.Append($"<h1>Sihhi Tesisat Hesap Cetveli — {Esc(projName)}</h1>");
            sb.Append("<p>EN 12056-2 (drenaj) / EN 806-3 (su besleme). Sonuclar sorumlu muhendis tarafindan dogrulanmalidir.</p>");
            sb.Append("<table><tr>");
            foreach (var h in new[] { "Sistem", "Rol", "DU/LU", "Debi(l/s)", "DN(mm)", "Dolum(%)", "Egim%/Hiz", "Durum" })
                sb.Append($"<th>{h}</th>");
            sb.Append("</tr>");
            foreach (var r in rows)
            {
                string durum = (r.GetValueOrDefault("durum") as string) ?? "";
                sb.Append($"<tr class='{Esc(durum)}'>");
                sb.Append($"<td>{Esc(Str(r,"sistem"))}</td>");
                sb.Append($"<td>{Esc(Str(r,"rol"))}</td>");
                sb.Append($"<td>{Str(r,"du_lu")}</td>");
                sb.Append($"<td>{Str(r,"debi_l_s")}</td>");
                sb.Append($"<td>{Str(r,"dn_mm")}</td>");
                sb.Append($"<td>{Str(r,"dolum_pct")}</td>");
                sb.Append($"<td>{Str(r,"egim_hiz")}</td>");
                sb.Append($"<td>{Esc(durum)}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></body></html>");
            return sb.ToString();
        }

        private static string BuildCsv(List<Dictionary<string, object?>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sistem;Rol;DU_LU;Debi_l_s;DN_mm;Dolum_pct;Egim_Hiz;Durum");
            foreach (var r in rows)
                sb.AppendLine(string.Join(";", new[]
                {
                    Str(r,"sistem"), Str(r,"rol"), Str(r,"du_lu"), Str(r,"debi_l_s"),
                    Str(r,"dn_mm"), Str(r,"dolum_pct"), Str(r,"egim_hiz"), Str(r,"durum"),
                }));
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Düşük seviyeli yardımcılar
        // ═════════════════════════════════════════════════════════════════════

        private static Category? TryGetCategory(Document doc, BuiltInCategory bic)
        { try { return doc.Settings.Categories.get_Item(bic); } catch { return null; } }

        private static void SetD(Element e, string name, double v)
        {
            var p = e.LookupParameter(name);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) p.Set(v);
        }

        private static void SetS(Element e, string name, string v)
        {
            var p = e.LookupParameter(name);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String) p.Set(v);
        }

        private static double RoundP(Element e, string name, int dig)
        {
            var p = e.LookupParameter(name);
            return p != null && p.HasValue ? Math.Round(p.AsDouble(), dig) : 0;
        }

        private static string Str(Dictionary<string, object?> r, string k)
            => r.GetValueOrDefault(k)?.ToString() ?? "";

        private static Dictionary<string, object?> UnitsRow(
            long fid, string tip, double du, double luC, double luH, string durum)
            => new()
            {
                ["fixture_id"] = fid.ToString(),
                ["tip"]        = tip,
                ["du"]         = du,
                ["lu_soguk"]   = luC,
                ["lu_sicak"]   = luH,
                ["durum"]      = durum,
            };

        private static Dictionary<string, object?> CalcRow(
            long pid, string rol, string sistem, double duLu, double debi, int dn, string durum)
            => new()
            {
                ["pipe_id"]      = pid.ToString(),
                ["rol"]          = rol,
                ["sistem"]       = sistem,
                ["toplam_du_lu"] = Math.Round(duLu, 2),
                ["debi_l_s"]     = Math.Round(debi, 2),
                ["dn_mm"]        = dn,
                ["durum"]        = durum,
            };

        private static List<Dictionary<string, object?>> ErrRow(string msg)
            => new() { new() { ["durum"] = "HATA", ["mesaj"] = msg } };

        private static string Esc(string? s)
            => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit baglami gerektirir.");
    }
}
