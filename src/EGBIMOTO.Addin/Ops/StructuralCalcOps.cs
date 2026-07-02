// ============================================================
// EGBIMOTO — Yapısal Hesap Motoru (StructuralCalcOps) — v10.5
// Apache 2.0 — EGBIM / Ertugan Gocer
// ============================================================
// Op listesi (10 adet):
//   1.  struct_rebar_lap_kolon        — Kolon filiz/bindirme boyu (TBDY §7.3)
//   2.  struct_rebar_lap_perde        — Perde duvar filiz boyu (TBDY §7.7)
//   3.  struct_rebar_anchorage        — Ankraj boyu: lb=(φ/4)×(fyd/fbd) (TS 500 §8.4)
//   4.  struct_concrete_class_qa      — Beton sınıfı QA (TS 500 / TBDY min C25)
//   5.  struct_beam_depth_ratio       — Kiriş h/L oranı: h≥L/12 (TS 500 §9.1)
//   6.  struct_wall_slenderness       — Perde narinlik: h/t ≤ 25 (TBDY §7.6)
//   7.  struct_slab_thickness         — Döşeme kalınlık: t≥L/35 veya L/45 (TS 500)
//   8.  struct_foundation_bearing     — Temel taban basıncı: q=N/(B×L) (TS 500 §15)
//   9.  struct_steel_bolt_type_check  — Çelik bulon tipi QA (N/X/T/SC, AISC/ASTM)
//  10.  struct_formwork_type_select   — Kalıp sistemi seçimi (9 tip, maliyet/hız tablosu)
//
// Standartlar:
//   TS 500:2000     — Betonarme Yapıların Tasarım ve Yapım Kuralları
//   TBDY 2018       — Türkiye Bina Deprem Yönetmeliği
//   TS EN 1992-1-1  — Eurocode 2 (referans)
//   AISC 360        — Specification for Structural Steel Buildings
//   ASTM F3125      — Structural Bolt Assemblies
//   RCSC Spec.      — Research Council on Structural Connections
//   ÇŞB 2026        — Yapı İşleri İnşaat İmalatları Birim Fiyatları
//
// ⚠️ Sorumluluk: Sonuçlar sorumlu inşaat mühendisi tarafından doğrulanmalıdır.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    public static class StructuralCalcOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // TABLOLAR
        // ─────────────────────────────────────────────────────────────────────

        // Beton sınıfları: fck (MPa), fcd, fctd, fbd (TS 500 §3.2)
        // fbd = bağ dayanımı = 0.35 × √fck (TS 500 Denklem 8.7)
        private static readonly Dictionary<string, (double fck, double fcd, double fctd, double fbd)>
            ConcreteClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            { "C20", (20,  13.33, 1.00, 1.56) },
            { "C25", (25,  16.67, 1.15, 1.75) },
            { "C28", (28,  18.67, 1.25, 1.85) },
            { "C30", (30,  20.00, 1.30, 1.92) },
            { "C32", (32,  21.33, 1.35, 1.98) },
            { "C35", (35,  23.33, 1.40, 2.07) },
            { "C40", (40,  26.67, 1.55, 2.22) },
            { "C45", (45,  30.00, 1.65, 2.35) },
            { "C50", (50,  33.33, 1.75, 2.47) },
        };

        // Çelik sınıfları: fyk, fyd (MPa)
        private static readonly Dictionary<string, (double fyk, double fyd)>
            SteelClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            { "B420C", (420, 365) },
            { "B500C", (500, 435) },
            { "B500B", (500, 435) },
            { "S220",  (220, 191) },
            { "S420",  (420, 365) },
        };

        // Bulon tipleri (AISC / ASTM / RCSC)
        private static readonly Dictionary<string, (string tanim, string kesme, string[] standartlar, string not)>
            BoltTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "N", ("N-Bulon: Dişler kesme düzleminde",
                    "Düşük (dişli gövde kesme düzleminde geçer)",
                    new[]{"ASTM A325-N","ASTM A490-N"},
                    "Genel bağlantılarda kullanılır. Kesme dayanımı daha düşük.") },
            { "X", ("X-Bulon: Dişler kesme düzlemi dışında",
                    "Yüksek (düz gövde kesme düzleminde geçer)",
                    new[]{"ASTM A325-X","ASTM A490-X"},
                    "Kesme kritik bağlantılarda. N-Bulon'a göre ~%24 daha yüksek dayanım.") },
            { "T", ("T-Bulon: Tam dişli yapısal bulon",
                    "N ile benzer (her pozisyonda dişli)",
                    new[]{"ASTM A325T","ASTM A490T"},
                    "Tam dişli boydan boya. Uzun bağlantılarda kullanışlı.") },
            { "SC", ("SC Birleşim: Kayma kritik (Slip-Critical)",
                     "Sürtünme ile yük aktarımı",
                     new[]{"ASTM A325-SC","ASTM A490-SC","RCSC Spec."},
                     "Yük temas yüzeyleri arasındaki sürtünme ile aktarılır. Titreşim/yorulma kritik.") },
        };

        // Kalıp sistemi tablosu (Döşeme)
        private static readonly Dictionary<string, (
            string ilkMaliyet, string uygulamaHizi, string tekrarKullanim,
            string kullanımAlanı, string avantaj, string dezavantaj)>
            FormworkTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ahsap",        ("Düşük",     "Düşük",      "Az",       "Konut/küçük ölçek", "Düşük maliyet, kolay", "Kısa ömür, yavaş") },
            { "celik",        ("Orta-Yüksek","Orta",      "Yüksek",   "Orta/yüksek katlı", "Uzun ömür, düzgün yüzey", "Ağır, ilk maliyet yüksek") },
            { "moduler",      ("Yüksek",    "Çok Yüksek", "Çok Yüksek","Tekrarlayan kat","Hızlı montaj, standart", "Karmaşık geometri sınırlı") },
            { "flying_table", ("Yüksek",    "Çok Yüksek", "Çok Yüksek","Yüksek katlı",  "Hızlı, işçilik tasarrufu", "Vinç gerektirir") },
            { "tunel",        ("Çok Yüksek","Çok Yüksek", "Çok Yüksek","Toplu konut",   "Hızlı, duvar+döşeme birlikte", "Mimari esneklik sınırlı") },
            { "aluminyum",    ("Yüksek",    "Yüksek",     "Çok Yüksek","Büyük ölçek",   "Hafif, hızlı kurulum", "Yüksek satın alma maliyeti") },
            { "plastik",      ("Orta",      "Yüksek",     "Yüksek",   "Küçük/orta",    "Hafif, korozyona dayanıklı", "Taşıma kapasitesi sınırlı") },
            { "waffle",       ("Orta",      "Orta",       "Orta",     "AVM/geniş açıklık","Beton tasarrufu, hafif", "Uzmanlık gerektirir") },
            { "uboot",        ("Orta-Yüksek","Orta",      "Kalıcı",   "Ofis/ticaret",  "Yapı ağırlığı azaltır, sütun azalır", "Detaylı mühendislik gerektirir") },
        };

        // ─────────────────────────────────────────────────────────────────────
        // OP 1 — struct_rebar_lap_kolon
        // Kolon filiz/bindirme boyu (TBDY 2018 §7.3 + TS 500 §8.5)
        //
        // Kolon: L/D < 4 → dar kesit, yüksek gerilme → UZUN bindirme boyu
        //   l_b = α × lb0  (lb0 = temel ankraj boyu)
        //   α katsayısı: baskı bindirmesi → 1.0, çekme bindirmesi → 1.3
        //   TBDY: min bindirme boyu = 1.25 × lb (deprem bölgesinde)
        //   Min filiz boyu: 500mm veya 30φ (hangisi büyükse)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_rebar_lap_kolon",
            RequiresTransaction = false,
            Description =
                "Kolon filiz/bindirme boyu hesabi (TBDY 2018 §7.3 / TS 500 §8.5).\n\n" +
                "params:\n" +
                "  cap_mm          — donatı çapı mm (8-32)\n" +
                "  beton_sinifi    — C20|C25|C30|C35|C40 vb.\n" +
                "  celik_sinifi    — B420C|B500C (default B420C)\n" +
                "  kolon_l_d_orani — L/D oranı (default 3.0 = dar kolon)\n" +
                "  deprem_bolgesi  — bool (TBDY ek çarpan, default true)\n" +
                "  basinc_mi       — true=basınç, false=çekme bindirmesi\n\n" +
                "Cikti: lb0_mm, bindirme_mm, filiz_min_mm, l_d_tipi, durum",
            Category = "Yapısal")]
        public static Dictionary<string, object?> RebarLapKolon(OpContext ctx)
        {
            int    cap      = ctx.GetInt("cap_mm", 16);
            string beton    = ctx.GetString("beton_sinifi", "C25").ToUpper();
            string celik    = ctx.GetString("celik_sinifi", "B420C").ToUpper();
            double ld       = ctx.GetDouble("kolon_l_d_orani", 3.0);
            bool   deprem   = ctx.GetBool("deprem_bolgesi", true);
            bool   basinc   = ctx.GetBool("basinc_mi", true);

            if (!ConcreteClasses.TryGetValue(beton, out var bData))
                return ErrResult($"Bilinmeyen beton: '{beton}'. Desteklenen: {string.Join(",", ConcreteClasses.Keys)}");
            if (!SteelClasses.TryGetValue(celik, out var sData))
                return ErrResult($"Bilinmeyen çelik: '{celik}'. Desteklenen: {string.Join(",", SteelClasses.Keys)}");

            double fyd = sData.fyd;
            double fbd = bData.fbd;

            // Temel ankraj boyu: lb0 = (φ/4) × (fyd/fbd)  [TS 500 §8.4]
            double lb0 = (cap / 4.0) * (fyd / fbd);

            // Bindirme katsayısı
            double alpha = basinc ? 1.0 : 1.3; // TS 500 §8.5
            if (deprem) alpha *= 1.25;           // TBDY §7.3.3

            double bindirmeMm = alpha * lb0;

            // Min filiz boyu: max(500mm, 30φ) — TBDY §7.3.2
            double filizMin = Math.Max(500.0, 30.0 * cap);
            bindirmeMm = Math.Max(bindirmeMm, filizMin);

            // Üste yuvarlama (50mm basamak)
            bindirmeMm = Math.Ceiling(bindirmeMm / 50.0) * 50.0;

            // L/D tipi
            string ldTipi = ld < 4.0
                ? $"DAR KESİT (L/D={ld:F1}<4) — UZUN FİLİZ zorunlu"
                : $"PERDE/UZUN (L/D={ld:F1}≥4) — normal filiz";

            ctx.Log($"  struct_rebar_lap_kolon: φ{cap} {beton} {celik} → " +
                    $"lb0={lb0:F0}mm bindirme={bindirmeMm:F0}mm");

            return new()
            {
                ["cap_mm"]          = cap,
                ["beton_sinifi"]    = beton,
                ["celik_sinifi"]    = celik,
                ["fyd_mpa"]         = fyd,
                ["fbd_mpa"]         = fbd,
                ["lb0_mm"]          = Math.Round(lb0, 0),
                ["alpha"]           = alpha,
                ["bindirme_mm"]     = bindirmeMm,
                ["filiz_min_mm"]    = filizMin,
                ["l_d_orani"]       = ld,
                ["l_d_tipi"]        = ldTipi,
                ["deprem_bolgesi"]  = deprem,
                ["durum"]           = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2 — struct_rebar_lap_perde
        // Perde duvar filiz boyu (TBDY 2018 §7.7 + TS 500)
        //
        // Perde duvar: L/D > 4 → geniş yüzey, yayılı yük → NORMAL bindirme
        //   Yatay donatı: l_b ≥ 30φ veya 300mm (hangisi büyükse)
        //   Düşey donatı: l_b = α × lb0
        //   TBDY: gövde donatısı min ρ = 0.0025 her yönde
        //   Uç bölge uzunluğu: l_w ≥ max(0.2×hw, 1.5×bw)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_rebar_lap_perde",
            RequiresTransaction = false,
            Description =
                "Perde duvar filiz/bindirme boyu hesabi (TBDY 2018 §7.7 / TS 500).\n\n" +
                "params:\n" +
                "  cap_mm          — donatı çapı mm\n" +
                "  beton_sinifi    — C25|C30|C35 vb.\n" +
                "  celik_sinifi    — B420C|B500C\n" +
                "  donati_yonu     — yatay|duseyWATCH (default duseyWATCH)\n" +
                "  perde_kalinlik_mm — perde kalınlığı mm\n" +
                "  perde_yukseklik_mm— perde yüksekliği mm\n\n" +
                "Cikti: lb0_mm, bindirme_mm, uc_bolge_mm, min_donati_orani, durum",
            Category = "Yapısal")]
        public static Dictionary<string, object?> RebarLapPerde(OpContext ctx)
        {
            int    cap     = ctx.GetInt("cap_mm", 12);
            string beton   = ctx.GetString("beton_sinifi", "C25").ToUpper();
            string celik   = ctx.GetString("celik_sinifi", "B420C").ToUpper();
            string yon     = ctx.GetString("donati_yonu", "dusey").ToLowerInvariant();
            double kalinlik= ctx.GetDouble("perde_kalinlik_mm", 200);
            double yukseklik= ctx.GetDouble("perde_yukseklik_mm", 3000);

            if (!ConcreteClasses.TryGetValue(beton, out var bData))
                return ErrResult($"Bilinmeyen beton: '{beton}'");
            if (!SteelClasses.TryGetValue(celik, out var sData))
                return ErrResult($"Bilinmeyen çelik: '{celik}'");

            double fyd = sData.fyd; double fbd = bData.fbd;

            // Temel ankraj boyu
            double lb0 = (cap / 4.0) * (fyd / fbd);

            // Bindirme boyu
            double bindirme;
            if (yon == "yatay")
            {
                // Yatay donatı: min 30φ veya 300mm (TS 500 §8.5.2)
                bindirme = Math.Max(30.0 * cap, 300.0);
            }
            else
            {
                // Düşey donatı: α=1.0 (basınç) perde için, TBDY normal bindirme
                bindirme = 1.0 * lb0;
                bindirme = Math.Max(bindirme, Math.Max(500.0, 30.0 * cap));
            }
            bindirme = Math.Ceiling(bindirme / 50.0) * 50.0;

            // Uç bölge uzunluğu (TBDY §7.7.5):
            // l_u ≥ max(0.2 × hw, 1.5 × bw, 500mm)
            double ucBolge = Math.Max(0.2 * yukseklik,
                             Math.Max(1.5 * kalinlik, 500.0));
            ucBolge = Math.Ceiling(ucBolge / 50.0) * 50.0;

            // Min donatı oranı: ρmin = 0.0025 (TBDY §7.7.3)
            double rhoMin = 0.0025;

            // L/D hesabı (perde boyutu tahmini: enine kalinlik vs yükseklik)
            double ld = yukseklik / kalinlik;
            string ldNot = ld > 4.0
                ? $"L/D={ld:F1}>4 → PERDE (normal filiz) ✓"
                : $"⚠️ L/D={ld:F1}<4 → KOLON gibi davranıyor, uzun filiz değerlendir";

            ctx.Log($"  struct_rebar_lap_perde: φ{cap} {yon} {beton} → " +
                    $"bindirme={bindirme:F0}mm uç bölge={ucBolge:F0}mm");

            return new()
            {
                ["cap_mm"]              = cap,
                ["beton_sinifi"]        = beton,
                ["donati_yonu"]         = yon,
                ["lb0_mm"]              = Math.Round(lb0, 0),
                ["bindirme_mm"]         = bindirme,
                ["uc_bolge_mm"]         = ucBolge,
                ["min_donati_orani"]    = rhoMin,
                ["l_d_orani"]           = Math.Round(ld, 1),
                ["l_d_notu"]            = ldNot,
                ["durum"]               = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 3 — struct_rebar_anchorage
        // Ankraj boyu hesabı (TS 500 §8.4)
        //
        // lb0 = (φ/4) × (fyd/fbd)
        // Düz çubuk: lb = lb0
        // Kancalı:   lb = 0.7 × lb0 (standart kanca)
        // Çengelli:  lb = 0.5 × lb0
        // Min ankraj: max(10φ, 100mm)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_rebar_anchorage",
            RequiresTransaction = false,
            Description =
                "Ankraj boyu hesabi (TS 500 §8.4): lb0 = (phi/4) x (fyd/fbd).\n\n" +
                "params:\n" +
                "  cap_mm        — donatı çapı mm\n" +
                "  beton_sinifi  — C20|C25|C30 vb.\n" +
                "  celik_sinifi  — B420C|B500C\n" +
                "  ankraj_tipi   — duz|kancali|cengelli (default duz)\n" +
                "  basinc_mi     — true=basınç bölgesi (default false)\n\n" +
                "Cikti: lb0_mm, lb_mm, min_ankraj_mm, ankraj_tipi, durum",
            Category = "Yapısal")]
        public static Dictionary<string, object?> RebarAnchorage(OpContext ctx)
        {
            int    cap    = ctx.GetInt("cap_mm", 16);
            string beton  = ctx.GetString("beton_sinifi", "C25").ToUpper();
            string celik  = ctx.GetString("celik_sinifi", "B420C").ToUpper();
            string aTip   = ctx.GetString("ankraj_tipi", "duz").ToLowerInvariant();
            bool   basinc = ctx.GetBool("basinc_mi", false);

            if (!ConcreteClasses.TryGetValue(beton, out var bData))
                return ErrResult($"Bilinmeyen beton: '{beton}'");
            if (!SteelClasses.TryGetValue(celik, out var sData))
                return ErrResult($"Bilinmeyen çelik: '{celik}'");

            double fyd = sData.fyd;
            double fbd = bData.fbd;
            // Basınç bölgesinde fbd %25 artırılabilir (TS 500 §8.4.1)
            if (basinc) fbd *= 1.25;

            double lb0 = (cap / 4.0) * (fyd / fbd);

            // Ankraj tipi katsayısı
            double beta = aTip switch
            {
                "kancali"  => 0.7,
                "cengelli" => 0.5,
                _          => 1.0, // düz
            };
            double lb = beta * lb0;

            // Min ankraj
            double minAnkraj = Math.Max(10.0 * cap, 100.0);
            lb = Math.Max(lb, minAnkraj);
            lb = Math.Ceiling(lb / 25.0) * 25.0; // 25mm basamak

            ctx.Log($"  struct_rebar_anchorage: φ{cap} {beton}/{celik} {aTip} → lb={lb:F0}mm");

            return new()
            {
                ["cap_mm"]        = cap,
                ["beton_sinifi"]  = beton,
                ["celik_sinifi"]  = celik,
                ["fyd_mpa"]       = fyd,
                ["fbd_mpa"]       = Math.Round(fbd, 2),
                ["lb0_mm"]        = Math.Round(lb0, 0),
                ["ankraj_tipi"]   = aTip,
                ["beta"]          = beta,
                ["lb_mm"]         = lb,
                ["min_ankraj_mm"] = minAnkraj,
                ["basinc_mi"]     = basinc,
                ["durum"]         = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 4 — struct_concrete_class_qa
        // Beton sınıfı QA (TS 500 / TBDY 2018)
        //
        // TBDY 2018 §3.3.2:
        //   Deprem bölgesi yüksek: min C25
        //   Deprem bölgesi orta:   min C20
        //   Perde duvar:            min C25
        //   Prefabrik:              min C30
        // TS 500 §3.1:
        //   Normal beton: C16-C50
        //   Yüksek dayanımlı: >C50
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_concrete_class_qa",
            RequiresTransaction = false,
            Description =
                "Beton sinifi QA (TS 500 / TBDY 2018). Kullanim yerine gore min sinif kontrolu.\n\n" +
                "params:\n" +
                "  beton_sinifi      — C20|C25|C28|C30|C32|C35|C40|C45|C50\n" +
                "  kullanim_yeri     — genel|deprem_yuksek|deprem_orta|perde|prefabrik|zemin_temasi\n" +
                "  deprem_bolgesi    — bool (TBDY min C25, default true)\n" +
                "  eleman_tipi       — kolon|kiriş|perde|döşeme|temel|prefabrik\n\n" +
                "Cikti: fck, fcd, fctd, fbd, min_sinif, durum",
            Category = "Yapısal")]
        public static Dictionary<string, object?> ConcreteClassQa(OpContext ctx)
        {
            string beton    = ctx.GetString("beton_sinifi", "C25").ToUpper();
            string kullanim = ctx.GetString("kullanim_yeri", "genel").ToLowerInvariant();
            bool   deprem   = ctx.GetBool("deprem_bolgesi", true);
            string eleman   = ctx.GetString("eleman_tipi", "kolon").ToLowerInvariant();

            if (!ConcreteClasses.TryGetValue(beton, out var bData))
                return ErrResult($"Bilinmeyen beton: '{beton}'. TS 500 sınıfları: " +
                    string.Join(", ", ConcreteClasses.Keys));

            // Min beton sınıfı belirleme
            string minSinif = kullanim switch
            {
                "deprem_yuksek" => "C25",
                "deprem_orta"   => "C20",
                "perde"         => "C25",
                "prefabrik"     => "C30",
                "zemin_temasi"  => "C25",
                _               => deprem ? "C25" : "C20",
            };

            // Eleman tipine göre ek kural
            if (eleman == "perde" && deprem) minSinif = "C25";

            if (!ConcreteClasses.TryGetValue(minSinif, out var minData))
                minData = ConcreteClasses["C20"];

            bool uygun = bData.fck >= minData.fck;
            string durum = uygun ? "UYGUN" : "SINIF_YETERSIZ";

            // Uyarılar
            var uyarilar = new List<string>();
            if (!uygun)
                uyarilar.Add($"{beton} (fck={bData.fck}MPa) < {minSinif} (fck={minData.fck}MPa) — min gereksinim karşılanmıyor");
            if (bData.fck > 50)
                uyarilar.Add("C50 üzeri yüksek dayanımlı beton — özel karışım tasarımı gerekir");
            if (deprem && bData.fck < 25)
                uyarilar.Add("TBDY §3.3.2: Deprem bölgelerinde min C25 zorunlu");

            ctx.Log($"  struct_concrete_class_qa: {beton} (fck={bData.fck}) min={minSinif} → {durum}");

            return new()
            {
                ["beton_sinifi"]   = beton,
                ["fck_mpa"]        = bData.fck,
                ["fcd_mpa"]        = bData.fcd,
                ["fctd_mpa"]       = bData.fctd,
                ["fbd_mpa"]        = bData.fbd,
                ["kullanim_yeri"]  = kullanim,
                ["eleman_tipi"]    = eleman,
                ["min_sinif"]      = minSinif,
                ["min_fck_mpa"]    = minData.fck,
                ["uyarilar"]       = uyarilar,
                ["durum"]          = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 5 — struct_beam_depth_ratio
        // Kiriş derinlik/açıklık oranı (TS 500 §9.1)
        //
        // TS 500 §9.1.2:
        //   Basit kiriş:    h ≥ L/12
        //   Sürekli kiriş:  h ≥ L/15
        //   Konsol kiriş:   h ≥ L/6
        //   Min kiriş genişliği: b ≥ 250mm veya h/4
        //   TBDY §7.4: b ≥ 250mm ve b ≥ h_kolon/2
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_beam_depth_ratio",
            RequiresTransaction = true,
            Description =
                "Kiris derinlik/aciklik orani kontrolu (TS 500 §9.1 / TBDY §7.4).\n\n" +
                "params:\n" +
                "  kiriş_tipi     — basit|surekli|konsol (default surekli)\n" +
                "  min_genislik_mm— min kiriş genişliği mm (default 250)\n" +
                "  write_back     — EG_KirisUygunluk parametresine yaz\n\n" +
                "Revit: Framing elemanlarından aciklik+kesit okunur.\n" +
                "Cikti: eleman_id, aciklik_mm, yukseklik_mm, h_l_orani, min_h_mm, durum",
            Category = "Yapısal")]
        public static List<Dictionary<string, object?>> BeamDepthRatio(OpContext ctx)
        {
            var rctx   = RequireRevit(ctx);
            string tip = ctx.GetString("kiriş_tipi", "surekli").ToLowerInvariant();
            int minB   = ctx.GetInt("min_genislik_mm", 250);
            bool wb    = ctx.GetBool("write_back", false);

            // Min h/L katsayısı
            double hlMin = tip switch
            {
                "basit"   => 1.0 / 12.0,
                "konsol"  => 1.0 / 6.0,
                _         => 1.0 / 15.0, // sürekli
            };

            var framings = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .Where(e => (e.LookupParameter("Structural Usage")?.AsInteger() ?? e.LookupParameter("Yapısal Kullanım")?.AsInteger() ?? 1) == 1) // Beam
                .ToList();

            if (framings.Count == 0)
                return ErrRows("Modelde kiriş (Structural Framing - Beam) bulunamadı.");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(rctx.Doc, "Kiriş H/L QA", rctx.IsAtomicMode);

            foreach (var fr in framings)
            {
                long eid = Rv.GetId(fr.Id);

                // Açıklık (Span Length veya eleman uzunluğu)
                double aciklikFt = fr.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble()
                                ?? fr.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble()
                                ?? 0;
                double aciklikMm = aciklikFt * 304.8;

                // Yükseklik (d parametresinden)
                double yukseklikMm = (fr.LookupParameter("b")?.AsDouble()
                                   ?? fr.LookupParameter("d")?.AsDouble()
                                   ?? fr.LookupParameter("h")?.AsDouble()
                                   ?? 0) * 304.8;

                // Genişlik
                double genislikMm = (fr.LookupParameter("b")?.AsDouble()
                                  ?? fr.LookupParameter("bf")?.AsDouble()
                                  ?? 0) * 304.8;

                if (aciklikMm <= 0 || yukseklikMm <= 0)
                {
                    rows.Add(BeamRow(eid, 0, 0, 0, 0, 0, "VERI_YOK"));
                    continue;
                }

                double minH    = hlMin * aciklikMm;
                double hlOrani = yukseklikMm / aciklikMm;

                string hDurum  = yukseklikMm >= minH ? "UYGUN" : "YUKSEKLIK_YETERSIZ";
                string bDurum  = genislikMm >= minB || genislikMm <= 0 ? "UYGUN" : "GENISLIK_YETERSIZ";

                string durum = (hDurum == "UYGUN" && bDurum == "UYGUN") ? "UYGUN" :
                               hDurum != "UYGUN" ? hDurum : bDurum;

                if (wb) SetS(fr, "EG_KirisUygunluk", durum);
                rows.Add(BeamRow(eid, aciklikMm, yukseklikMm, genislikMm, hlOrani, minH, durum));
            }

            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  struct_beam_depth_ratio: {ok}/{rows.Count} kiriş UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 6 — struct_wall_slenderness
        // Perde duvar narinlik kontrolü (TBDY 2018 §7.6.2)
        //
        // TBDY §7.6.2: h/t ≤ 25  (kat yüksekliği / duvar kalınlığı)
        // TS 500 §11.1.5: h/t ≤ 20 (dışmerkezlik varsa)
        // Min kalınlık: max(200mm, h/25)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_wall_slenderness",
            RequiresTransaction = true,
            Description =
                "Perde duvar narinlik kontrolu (TBDY 2018 §7.6.2 / TS 500 §11).\n\n" +
                "params:\n" +
                "  max_narinlik   — max h/t oranı (default 25, TBDY)\n" +
                "  min_kalinlik_mm— min duvar kalınlığı mm (default 200)\n" +
                "  write_back     — EG_PerdeUygunluk'a yaz (default false)\n\n" +
                "Revit: Structural Wall elemanlarından yükseklik+kalınlık okunur.\n" +
                "Cikti: duvar_id, yukseklik_mm, kalinlik_mm, narinlik, durum",
            Category = "Yapısal")]
        public static List<Dictionary<string, object?>> WallSlenderness(OpContext ctx)
        {
            var rctx    = RequireRevit(ctx);
            double maxN = ctx.GetDouble("max_narinlik", 25.0);
            int minK    = ctx.GetInt("min_kalinlik_mm", 200);
            bool wb     = ctx.GetBool("write_back", false);

            var walls = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .ToList();

            // Structural Walls
            var sWalls = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Where(w => w.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT)?.AsInteger() == 1)
                .ToList();

            if (sWalls.Count == 0)
                return ErrRows("Modelde yapısal perde duvar bulunamadı. " +
                               "Structural Wall olarak işaretlenmiş eleman gerekli.");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(rctx.Doc, "Perde Narinlik QA", rctx.IsAtomicMode);

            foreach (var wall in sWalls)
            {
                long wid = Rv.GetId(wall.Id);

                // Kalınlık (Width)
                double kalinlikMm = (wall.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM)?.AsDouble() ?? 0) * 304.8;

                // Yükseklik (kat yüksekliği)
                double yukseklikMm = (wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0) * 304.8;

                if (kalinlikMm <= 0 || yukseklikMm <= 0)
                {
                    rows.Add(WallRow(wid, 0, 0, 0, maxN, "VERI_YOK"));
                    continue;
                }

                double narinlik  = yukseklikMm / kalinlikMm;
                string nDurum    = narinlik <= maxN ? "UYGUN" : "NARLIK_ASIMI";
                string kDurum    = kalinlikMm >= minK ? "UYGUN" : "KALINLIK_YETERSIZ";
                string durum     = nDurum == "UYGUN" && kDurum == "UYGUN" ? "UYGUN" :
                                   nDurum != "UYGUN" ? nDurum : kDurum;

                if (wb) SetS(wall, "EG_PerdeUygunluk", durum);
                rows.Add(WallRow(wid, yukseklikMm, kalinlikMm, narinlik, maxN, durum));
            }

            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  struct_wall_slenderness: {ok}/{rows.Count} perde UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 7 — struct_slab_thickness
        // Döşeme kalınlık kontrolü (TS 500 §12)
        //
        // TS 500 §12.2:
        //   Tek yönlü döşeme: t ≥ L/35 (mesnetli her iki taraf)
        //   Çift yönlü döşeme: t ≥ L/45 (kısa kenar)
        //   Konsol döşeme: t ≥ L/12
        //   Min kalınlık: 80mm (tüm döşemeler)
        //   Prefabrik: t ≥ 50mm (ayrı kural)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_slab_thickness",
            RequiresTransaction = false,
            Description =
                "Doseme kalinlik kontrolu (TS 500 §12.2).\n\n" +
                "params:\n" +
                "  aciklik_mm       — döşeme açıklığı mm (tek yönlü=uzun kenar, çift=kısa)\n" +
                "  doseme_tipi      — tek_yonlu|cift_yonlu|konsol|mantar|waffle\n" +
                "  mevcut_kalinlik_mm — modeldeki kalınlık mm (0=sadece hesap)\n\n" +
                "Cikti: min_kalinlik_mm, mevcut_kalinlik_mm, h_l_orani, durum",
            Category = "Yapısal")]
        public static Dictionary<string, object?> SlabThickness(OpContext ctx)
        {
            double aciklik = ctx.GetDouble("aciklik_mm", 0);
            string tip     = ctx.GetString("doseme_tipi", "cift_yonlu").ToLowerInvariant();
            double mevcut  = ctx.GetDouble("mevcut_kalinlik_mm", 0);

            if (aciklik <= 0) return ErrResult("aciklik_mm > 0 olmalidir.");

            // Min kalınlık katsayısı (TS 500 §12.2)
            double katsayi = tip switch
            {
                "tek_yonlu"  => 1.0 / 35.0,
                "konsol"     => 1.0 / 12.0,
                "mantar"     => 1.0 / 40.0,
                "waffle"     => 1.0 / 40.0,
                _            => 1.0 / 45.0, // çift yönlü
            };

            double minHesap  = aciklik * katsayi;
            double minKalinlik = Math.Max(minHesap, 80.0); // absolüt min 80mm
            minKalinlik = Math.Ceiling(minKalinlik / 10.0) * 10.0; // 10mm basamak

            double hlOrani = mevcut > 0 ? mevcut / aciklik : 0;

            string durum = mevcut <= 0 ? "HESAP_SONUCU" :
                           mevcut >= minKalinlik ? "UYGUN" : "KALINLIK_YETERSIZ";

            ctx.Log($"  struct_slab_thickness: {tip} L={aciklik}mm → " +
                    $"min={minKalinlik}mm mevcut={mevcut}mm → {durum}");

            return new()
            {
                ["aciklik_mm"]          = aciklik,
                ["doseme_tipi"]         = tip,
                ["hesap_min_mm"]        = Math.Round(minHesap, 0),
                ["min_kalinlik_mm"]     = minKalinlik,
                ["mevcut_kalinlik_mm"]  = mevcut > 0 ? mevcut : (object?)"—",
                ["h_l_orani"]           = hlOrani > 0 ? Math.Round(hlOrani, 4) : (object?)"—",
                ["standart"]            = "TS 500:2000 §12.2",
                ["durum"]               = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 8 — struct_foundation_bearing
        // Temel taban basıncı (TS 500 §15 / Zemin emniyet gerilmesi)
        //
        // q = N / (B × L)   [tekil temel]
        // q = N / A_temel   [radye]
        // Kontrol: q ≤ q_emn (zemin emniyet gerilmesi)
        // Dışmerkezlik: e = M / N ≤ B/6 (çekip göçmeme koşulu)
        // Kolon altı: min temel boyutu = kolon kesiti + 150mm her taraf
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_foundation_bearing",
            RequiresTransaction = false,
            Description =
                "Temel taban basinci kontrolu (TS 500 §15).\n\n" +
                "params:\n" +
                "  eksenel_yuk_kn    — toplam düşey yük kN (N)\n" +
                "  temel_genislik_m  — temel genişliği B m\n" +
                "  temel_uzunluk_m   — temel uzunluğu L m (tekil için)\n" +
                "  zemin_emniyet_kpa — zemin emniyet gerilmesi kPa (default 150)\n" +
                "  moment_knm        — devrilme momenti kN.m (default 0)\n" +
                "  temel_tipi        — tekil|serit|radye (default tekil)\n\n" +
                "Cikti: taban_basinci_kpa, emniyet_kpa, dismerkezlik_m, durum",
            Category = "Yapısal")]
        public static Dictionary<string, object?> FoundationBearing(OpContext ctx)
        {
            double N    = ctx.GetDouble("eksenel_yuk_kn", 0);
            double B    = ctx.GetDouble("temel_genislik_m", 0);
            double L    = ctx.GetDouble("temel_uzunluk_m", 0);
            double qEmn = ctx.GetDouble("zemin_emniyet_kpa", 150.0);
            double M    = ctx.GetDouble("moment_knm", 0);
            string tip  = ctx.GetString("temel_tipi", "tekil").ToLowerInvariant();

            if (N <= 0) return ErrResult("eksenel_yuk_kn > 0 olmalidir.");
            if (B <= 0) return ErrResult("temel_genislik_m > 0 olmalidir.");
            if (tip != "serit" && L <= 0) return ErrResult("temel_uzunluk_m > 0 olmalidir (tekil/radye için).");

            double alan = tip == "serit" ? B * 1.0 : B * L; // şerit için birim uzunluk
            double q    = N / alan; // kPa (kN/m²)

            // Dışmerkezlik
            double e    = M > 0 ? M / N : 0; // m
            double eMax = B / 6.0;             // çekip göçmeme

            // Dışmerkezlik ile q_max
            double qMax = q * (1.0 + 6.0 * e / B);

            bool qUygun = qMax <= qEmn;
            bool eUygun = e <= eMax;

            string durum = (!qUygun || !eUygun) ? "UYGUN_DEGIL" : "UYGUN";

            var mesajlar = new List<string>();
            if (!qUygun) mesajlar.Add($"q_max={qMax:F1}kPa > q_emn={qEmn}kPa — temel büyütülmeli");
            if (!eUygun) mesajlar.Add($"e={e:F3}m > B/6={eMax:F3}m — dışmerkezlik sınırı aşıldı");
            if (qMax > qEmn * 0.8) mesajlar.Add("Kapasite kullanımı >%80 — kontrol önerilir");

            ctx.Log($"  struct_foundation_bearing: N={N}kN B={B}m L={L}m → " +
                    $"q={q:F1}kPa (emn={qEmn}) e={e:F3}m → {durum}");

            return new()
            {
                ["eksenel_yuk_kn"]    = N,
                ["temel_genislik_m"]  = B,
                ["temel_uzunluk_m"]   = tip == "serit" ? (object?)"—" : L,
                ["temel_alani_m2"]    = Math.Round(alan, 2),
                ["taban_basinci_kpa"] = Math.Round(q, 2),
                ["dismerkezlik_m"]    = Math.Round(e, 3),
                ["e_max_m"]           = Math.Round(eMax, 3),
                ["q_max_kpa"]         = Math.Round(qMax, 2),
                ["zemin_emniyet_kpa"] = qEmn,
                ["kapasite_pct"]      = Math.Round(qMax / qEmn * 100, 1),
                ["mesajlar"]          = mesajlar,
                ["durum"]             = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 9 — struct_steel_bolt_type_check
        // Çelik yapı bulon tipi QA (AISC 360 / ASTM / RCSC)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_steel_bolt_type_check",
            RequiresTransaction = false,
            Description =
                "Celik yapi bulon tipi QA (AISC 360 / ASTM F3125 / RCSC).\n\n" +
                "params:\n" +
                "  bulon_tipi      — N|X|T|SC\n" +
                "  baglanti_turu   — kesme_kritik|kayma_kritik|gerilme|kombine\n" +
                "  titresim_var    — bool (SC önerilir titreşimde)\n" +
                "  dinamik_yuk     — bool (SC zorunlu)\n\n" +
                "N: dişler kesme düzleminde (düşük); X: dişler dışında (yüksek);\n" +
                "T: tam dişli; SC: kayma kritik (sürtünme).\n" +
                "Cikti: bulon_tipi, tanim, kesme, standartlar, oneri, durum",
            Category = "Yapısal")]
        public static Dictionary<string, object?> SteelBoltTypeCheck(OpContext ctx)
        {
            string tip     = ctx.GetString("bulon_tipi", "N").ToUpper();
            string bagTur  = ctx.GetString("baglanti_turu", "kesme_kritik").ToLowerInvariant();
            bool titresim  = ctx.GetBool("titresim_var", false);
            bool dinamik   = ctx.GetBool("dinamik_yuk", false);

            if (!BoltTypes.TryGetValue(tip, out var bData))
                return ErrResult($"Bilinmeyen bulon tipi: '{tip}'. N|X|T|SC desteklenir.");

            // Öneri belirleme
            string oneriTip;
            string oneriNeden;
            if (dinamik || bagTur == "kayma_kritik")
            {
                oneriTip    = "SC";
                oneriNeden  = "Dinamik yük veya kayma kritik bağlantı → SC zorunlu";
            }
            else if (titresim)
            {
                oneriTip    = "SC";
                oneriNeden  = "Titreşimli ortam → SC önerilir";
            }
            else if (bagTur == "kesme_kritik")
            {
                oneriTip    = "X";
                oneriNeden  = "Kesme kritik → X-Bulon (daha yüksek kesme dayanımı)";
            }
            else
            {
                oneriTip    = "N";
                oneriNeden  = "Genel bağlantı → N-Bulon yeterli";
            }

            bool uygun = tip == oneriTip ||
                         (tip == "SC" && (dinamik || titresim)) ||
                         (tip == "X"  && bagTur == "kesme_kritik") ||
                         (tip == "T"  && bagTur != "kayma_kritik");

            string durum = uygun ? "UYGUN" : "BULON_GOZDEN_GECIR";

            ctx.Log($"  struct_steel_bolt_type_check: {tip} {bagTur} → öneri={oneriTip} → {durum}");

            return new()
            {
                ["bulon_tipi"]     = tip,
                ["tanim"]          = bData.tanim,
                ["kesme_kapasitesi"]= bData.kesme,
                ["standartlar"]    = bData.standartlar,
                ["not"]            = bData.not,
                ["baglanti_turu"]  = bagTur,
                ["titresim_var"]   = titresim,
                ["dinamik_yuk"]    = dinamik,
                ["oneri_tip"]      = oneriTip,
                ["oneri_neden"]    = oneriNeden,
                ["durum"]          = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 10 — struct_formwork_type_select
        // Kalıp sistemi seçimi (9 tip — maliyet/hız/tekrar kullanım tablosu)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("struct_formwork_type_select",
            RequiresTransaction = false,
            Description =
                "Kalip sistemi secimi ve karsilastirma (9 tip).\n\n" +
                "params:\n" +
                "  bina_tipi       — konut|yuksek_katli|ticari|sanayi|altyapi\n" +
                "  kat_sayisi      — toplam kat sayisi (yüksek kat → flying/tünel)\n" +
                "  tekrar_oncelik  — bool (tekrar kullanım öncelikli mi)\n" +
                "  hiz_oncelik     — bool (hız öncelikli mi)\n" +
                "  mevcut_tip      — ahsap|celik|moduler|flying_table|tunel|\n" +
                "                    aluminyum|plastik|waffle|uboot\n\n" +
                "Cikti: oneri_tip, karsilastirma_tablosu, durum",
            Category = "Yapısal")]
        public static Dictionary<string, object?> FormworkTypeSelect(OpContext ctx)
        {
            string binaTip    = ctx.GetString("bina_tipi", "konut").ToLowerInvariant();
            int    katSayisi  = ctx.GetInt("kat_sayisi", 5);
            bool   tekrarOnc  = ctx.GetBool("tekrar_oncelik", false);
            bool   hizOnc     = ctx.GetBool("hiz_oncelik", false);
            string mevcutTip  = ctx.GetString("mevcut_tip", "").ToLowerInvariant();

            // Öneri mantığı
            string oneriTip;
            string oneriNeden;

            if (katSayisi >= 20 || binaTip == "yuksek_katli")
            {
                oneriTip   = hizOnc ? "flying_table" : "tunel";
                oneriNeden = $"{katSayisi} katlı → yüksek hız kalıpları";
            }
            else if (katSayisi >= 8 && hizOnc)
            {
                oneriTip   = "moduler";
                oneriNeden = "8+ kat + hız önceliği → modüler";
            }
            else if (binaTip == "konut" && katSayisi >= 4 && tekrarOnc)
            {
                oneriTip   = "aluminyum";
                oneriNeden = "Tekrarlayan konut → alüminyum (yüksek tekrar)";
            }
            else if (binaTip == "ticari" || binaTip == "sanayi")
            {
                oneriTip   = "waffle";
                oneriNeden = "Geniş açıklık → waffle veya mantar döşeme";
            }
            else if (katSayisi <= 3)
            {
                oneriTip   = "ahsap";
                oneriNeden = "Küçük ölçekli → ahşap ekonomik";
            }
            else
            {
                oneriTip   = "celik";
                oneriNeden = "Genel kullanım → çelik kalıp dengeli seçim";
            }

            // Tüm sistemlerin karşılaştırma tablosu
            var karsilastirma = FormworkTypes.Select(kv => new Dictionary<string, object?>
            {
                ["tip"]             = kv.Key,
                ["ilk_maliyet"]     = kv.Value.ilkMaliyet,
                ["uygulama_hizi"]   = kv.Value.uygulamaHizi,
                ["tekrar_kullanim"] = kv.Value.tekrarKullanim,
                ["kullanim_alani"]  = kv.Value.kullanımAlanı,
                ["oneri"]           = kv.Key == oneriTip ? "★ ÖNERİLEN" : "",
            }).ToList<object?>();

            // Mevcut tip değerlendirmesi
            string mevcutDurum = "HESAP_SONUCU";
            if (!string.IsNullOrEmpty(mevcutTip))
            {
                mevcutDurum = mevcutTip == oneriTip ? "UYGUN" :
                              FormworkTypes.ContainsKey(mevcutTip) ? "ALTERNATIF_DEGERLENDIRIN" :
                              "BILINMEYEN_TIP";
            }

            ctx.Log($"  struct_formwork_type_select: {binaTip} {katSayisi}kat → öneri={oneriTip}");

            return new()
            {
                ["bina_tipi"]         = binaTip,
                ["kat_sayisi"]        = katSayisi,
                ["oneri_tip"]         = oneriTip,
                ["oneri_neden"]       = oneriNeden,
                ["mevcut_tip"]        = string.IsNullOrEmpty(mevcutTip) ? (object?)"—" : mevcutTip,
                ["mevcut_durum"]      = mevcutDurum,
                ["karsilastirma"]     = karsilastirma,
                ["durum"]             = mevcutDurum == "HESAP_SONUCU" ? "OK" : mevcutDurum,
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═════════════════════════════════════════════════════════════════════

        private static Dictionary<string, object?> BeamRow(
            long id, double aciklik, double yukseklik, double genislik,
            double hlOrani, double minH, string durum)
            => new()
            {
                ["eleman_id"]    = id.ToString(),
                ["aciklik_mm"]   = Math.Round(aciklik, 0),
                ["yukseklik_mm"] = Math.Round(yukseklik, 0),
                ["genislik_mm"]  = Math.Round(genislik, 0),
                ["h_l_orani"]    = Math.Round(hlOrani, 3),
                ["min_h_mm"]     = Math.Round(minH, 0),
                ["durum"]        = durum,
            };

        private static Dictionary<string, object?> WallRow(
            long id, double yukseklik, double kalinlik,
            double narinlik, double maxN, string durum)
            => new()
            {
                ["duvar_id"]      = id.ToString(),
                ["yukseklik_mm"]  = Math.Round(yukseklik, 0),
                ["kalinlik_mm"]   = Math.Round(kalinlik, 0),
                ["narinlik_h_t"]  = Math.Round(narinlik, 1),
                ["max_narinlik"]  = maxN,
                ["durum"]         = durum,
            };

        private static void SetS(Element e, string name, string v)
        {
            var p = e.LookupParameter(name);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String) p.Set(v);
        }

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit baglami gerektirir.");

        private static Dictionary<string, object?> ErrResult(string msg)
            => new() { ["durum"] = "HATA", ["mesaj"] = msg };

        private static List<Dictionary<string, object?>> ErrRows(string msg)
            => new() { new() { ["durum"] = "HATA", ["mesaj"] = msg } };
    }
}
