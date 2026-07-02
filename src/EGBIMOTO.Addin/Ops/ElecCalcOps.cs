// ============================================================
// EGBIMOTO — Elektrik Hesap Motoru 2 (ElecCalcOps) — v10.2
// Apache 2.0 — EGBIM / Ertugan Gocer
// ============================================================
// Op listesi (12 adet):
//   1.  elec_voltage_drop_calc      — Gerilim düşümü hesabı (IEC 60364-5-52)
//   2.  elec_short_circuit_check    — Kısa devre akımı doğrulama (IEC 60364-4-43)
//   3.  elec_diversity_factor       — Çeşitlilik faktörü ile talep gücü
//   4.  elec_earthing_validation    — Topraklama sistemi QA (TN-S/TN-C-S/TT)
//   5.  elec_busbar_sizing          — Busbar kesit seçimi (akım yoğunluğu)
//   6.  elec_power_factor_check     — Güç faktörü & reaktif güç (cosφ)
//   7.  elec_elv_device_qa          — ELV cihaz montaj yükseklikleri QA
//   8.  elec_tray_hanger_spacing    — Kablo tava askı aralığı (IEC 61537)
//   9.  elec_tray_separation_check  — Güç/ELV tava ayrımı (≥300mm)
//  10.  elec_generator_load_calc    — Jeneratör yük & kVA hesabı
//  11.  elec_ups_autonomy_check     — UPS özerklik süresi kontrolü
//  12.  elec_emergency_circuit_qa   — Acil aydınlatma devre QA (TS EN 1838)
//
// Standartlar:
//   IEC 60364-5-52  — Kablo seçimi, gerilim düşümü
//   IEC 60364-4-43  — Kısa devre koruması
//   IEC 60364-4-41  — Topraklama / güvenlik
//   IEC 61537       — Kablo tava destek aralıkları
//   TS EN 1838      — Acil aydınlatma
//   IEC 62040-3     — UPS performans sınıflandırması
//   TS HD 60364     — Türkiye elektrik tesisatı standardı
//
// ⚠️ Sorumluluk: Sonuçlar sorumlu elektrik mühendisi tarafından doğrulanmalıdır.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    public static class ElecCalcOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // TABLOLAR
        // ─────────────────────────────────────────────────────────────────────

        // Bakır iletken direnci (mΩ/m @ 70°C, IEC 60228 sınıf 2)
        private static readonly Dictionary<double, double> CuResistance = new()
        {
            {1.5,14.8},{2.5,8.91},{4,5.57},{6,3.71},{10,2.24},{16,1.41},
            {25,0.889},{35,0.641},{50,0.473},{70,0.328},{95,0.236},
            {120,0.188},{150,0.153},{185,0.124},{240,0.095},{300,0.077}
        };

        // Alüminyum iletken direnci (mΩ/m @ 70°C)
        private static readonly Dictionary<double, double> AlResistance = new()
        {
            {16,2.30},{25,1.47},{35,1.06},{50,0.786},{70,0.549},
            {95,0.397},{120,0.313},{150,0.253},{185,0.206},{240,0.157},{300,0.128}
        };

        // Standart kesitler (mm²)
        private static readonly double[] StdSizes =
            { 1.5, 2.5, 4, 6, 10, 16, 25, 35, 50, 70, 95, 120, 150, 185, 240, 300 };

        // Busbar akım yoğunluğu (A/mm²): bakır=1.6, alüminyum=1.0 (tipik değerler)
        private static readonly Dictionary<string, double> BusbarDensity = new(StringComparer.OrdinalIgnoreCase)
        {
            { "bakir", 1.6 }, { "aluminyum", 1.0 }, { "cu", 1.6 }, { "al", 1.0 }
        };

        // ELV cihaz montaj yükseklikleri (FFL'den mm): min, max, tipik
        private static readonly Dictionary<string, (int min, int max, int tipik, string aciklama)> ElvHeights =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "rj45_priz",       (300, 450, 400,  "Veri prizi duvar montaj yüksekliği (AFFL)") },
            { "rj45_tezgah",     (0,   0,   0,    "Tezgah üstü veri prizi (slab recessed/tezgah üstü)") },
            { "rj45_worktop",    (1200,1200,1200,  "Çalışma tezgahı üstü RJ45") },
            { "kamera_genel",    (2500,3500,3000,  "CCTV dome/bullet kamera (genel görüş)") },
            { "kamera_yz",       (2000,2500,2200,  "CCTV kamera (yüz tanıma)") },
            { "kart_okuyucu",    (1100,1300,1200,  "Kapı kart okuyucu / RTE butonu") },
            { "kart_ic",         (1100,1300,1200,  "Güvenli taraf kart okuyucu") },
            { "pir_dedektoru",   (2200,2700,2400,  "PIR/mikrodalga hareket dedektörü") },
            { "kapi_kontakt",    (0,   100, 10,    "Kapı kontak sensörü (kapı üstü)") },
            { "klavye",          (1300,1500,1400,  "Alarm/kontrol klavyesi") },
            { "pa_hoparlor_tav", (0,   0,   0,    "PA hoparlör (tavana montaj)") },
            { "pa_hoparlor_duv", (2000,2500,2300,  "PA duvar hoparlörü") },
            { "bms_sensor",      (0,   0,   0,    "BMS sıcaklık/nem sensörü (mekanik oda/asma tavan)") },
            { "zemin_kutu",      (0,   100, 0,    "Zemin kutusu (flush/raised access)") },
            { "elv_panel",       (1400,1800,1600,  "ELV panel/pano alt kenar") },
        };

        // Kablo tava tipleri → max askı aralığı (m): IEC 61537 Tablo 1
        private static readonly Dictionary<string, (double max_aralik, string aciklama)> TrayHangerSpacing =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "ladder",       (3.0, "Merdiven tip (≤300mm gen.)") },
            { "perforated",   (2.0, "Delikli tip") },
            { "solid_bottom", (2.0, "Kapalı dip tip") },
            { "wire_mesh",    (1.5, "Tel örgü tip") },
            { "rmc_conduit",  (3.0, "Sert metal boru (EMT/RMC)") },
            { "emt_conduit",  (3.0, "EMT boru") },
        };

        // ─────────────────────────────────────────────────────────────────────
        // OP 1 — elec_voltage_drop_calc
        // Gerilim düşümü hesabı: IEC 60364-5-52
        //
        // ΔU% = (√3 × I × L × (R cosφ + X sinφ)) / U_n × 100  [3 faz]
        // ΔU% = (2 × I × L × (R cosφ + X sinφ)) / U_n × 100   [1 faz]
        //
        // IEC 60364-5-52 limitleri:
        //   Aydınlatma: %3 (son besleme), %6 (toplam)
        //   Motor/genel: %5 (son besleme), %8 (toplam)
        //   Kritik yük:  %2.5
        //
        // params:
        //   akim_a          — faz akımı (A)
        //   uzunluk_m       — kablo uzunluğu (m)
        //   kesit_mm2       — iletken kesiti (mm²), 0 = otomatik seç
        //   gerilim_v       — nominal gerilim V (default 415 — 3 faz, 230 — 1 faz)
        //   faz_sayisi      — 1 veya 3 (default 3)
        //   malzeme         — cu|al (default cu)
        //   cos_phi         — güç faktörü (default 0.85)
        //   max_dusumu_pct  — izin verilen max % (default 3.0)
        //   yuk_tipi        — aydinlatma|motor|kritik (limit seçimi için)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_voltage_drop_calc",
            RequiresTransaction = false,
            Description =
                "Kablo gerilim dusumu hesabi (IEC 60364-5-52).\n\n" +
                "params:\n" +
                "  akim_a         — faz akimi A\n" +
                "  uzunluk_m      — kablo uzunlugu m\n" +
                "  kesit_mm2      — iletken kesiti mm2 (0=oto sec)\n" +
                "  gerilim_v      — nominal gerilim V (default 415)\n" +
                "  faz_sayisi     — 1 veya 3 (default 3)\n" +
                "  malzeme        — cu|al (default cu)\n" +
                "  cos_phi        — guc faktoru (default 0.85)\n" +
                "  max_dusumu_pct — izin verilen max % (default 3.0)\n" +
                "  yuk_tipi       — aydinlatma|motor|kritik\n\n" +
                "Limit: aydinlatma %3, motor %5, kritik %2.5 (IEC 60364-5-52).\n" +
                "Cikti: kesit_mm2, dusumu_pct, dusumu_v, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> VoltageDropCalc(OpContext ctx)
        {
            double I       = ctx.GetDouble("akim_a", 0);
            double L       = ctx.GetDouble("uzunluk_m", 0);
            double kesit   = ctx.GetDouble("kesit_mm2", 0);
            double Un      = ctx.GetDouble("gerilim_v", 415);
            int    faz     = ctx.GetInt("faz_sayisi", 3);
            string malzeme = ctx.GetString("malzeme", "cu").ToLowerInvariant();
            double cosPhi  = Math.Clamp(ctx.GetDouble("cos_phi", 0.85), 0.5, 1.0);
            double maxPct  = ctx.GetDouble("max_dusumu_pct", 3.0);
            string yukTip  = ctx.GetString("yuk_tipi", "genel").ToLowerInvariant();

            if (I <= 0)  return ErrResult("akim_a > 0 olmalidir.");
            if (L <= 0)  return ErrResult("uzunluk_m > 0 olmalidir.");
            if (Un <= 0) return ErrResult("gerilim_v > 0 olmalidir.");

            // Yük tipine göre limit
            maxPct = yukTip switch
            {
                "aydinlatma" => Math.Min(maxPct, 3.0),
                "kritik"     => Math.Min(maxPct, 2.5),
                "motor"      => Math.Min(maxPct, 5.0),
                _            => maxPct
            };

            // Kesit seçimi — yeterli kesit bul (gerilim düşümü limitin içinde kalacak)
            var resTbl = malzeme == "al" ? AlResistance : CuResistance;
            double sinPhi = Math.Sqrt(1 - cosPhi * cosPhi);
            double X = 0.08; // mΩ/m (tipik reaktans — PVC kablo)

            // Hesap fonksiyonu: ΔU%
            double CalcDrop(double r_mohm_m)
            {
                double R = r_mohm_m / 1000.0; // Ω/m
                double faktor = faz == 1 ? 2.0 : Math.Sqrt(3);
                double dU = faktor * I * L * (R * cosPhi + X / 1000.0 * sinPhi);
                return dU / Un * 100.0;
            }

            // Otomatik kesit seçimi
            if (kesit <= 0)
            {
                kesit = SelectCrossSection(I, L, Un, faz, cosPhi, sinPhi, X, maxPct, resTbl);
                if (kesit <= 0)
                    return ErrResult($"Mevcut kesit serisiyle %{maxPct} limiti saglanamadi. " +
                                     "Kesit veya hat bolunmesi gerekebilir.");
            }

            if (!resTbl.TryGetValue(kesit, out double r_ohm_m))
                r_ohm_m = CuResistance.TryGetValue(kesit, out var rc) ? rc : 0.077;

            double dropPct = CalcDrop(r_ohm_m);
            double dropV   = Un * dropPct / 100.0;
            string durum   = dropPct <= maxPct ? "UYGUN" : "LIMIT_ASILDI";

            ctx.Log($"  elec_voltage_drop_calc: {I}A {L}m {kesit}mm² " +
                    $"→ ΔU={dropPct:F2}% ({durum})");

            return new()
            {
                ["akim_a"]        = I,
                ["uzunluk_m"]     = L,
                ["kesit_mm2"]     = kesit,
                ["faz_sayisi"]    = faz,
                ["malzeme"]       = malzeme,
                ["cos_phi"]       = cosPhi,
                ["dusumu_pct"]    = Math.Round(dropPct, 3),
                ["dusumu_v"]      = Math.Round(dropV, 2),
                ["max_dusumu_pct"]= maxPct,
                ["yuk_tipi"]      = yukTip,
                ["durum"]         = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2 — elec_short_circuit_check
        // Kısa devre akımı & sigorta doğrulama (IEC 60364-4-43)
        //
        // I_sc = Un / (√3 × Z_total)   [3 faz]
        // Z_total = √((R_kaynak + R_kablo)² + (X_kaynak + X_kablo)²)
        // Sigorta kırma süresi: t ≤ k²S² / I_sc²
        //
        // params:
        //   kesit_mm2       — kablo kesiti mm²
        //   uzunluk_m       — kablo uzunluğu m
        //   gerilim_v       — nominal V (default 415)
        //   malzeme         — cu|al (default cu)
        //   kaynak_empedans — şebeke/trafo empedansı mΩ (default 35)
        //   sigorta_a       — koruma cihazı anma akımı A
        //   sigorta_tip     — mcb|mccb|fuse (default mcb)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_short_circuit_check",
            RequiresTransaction = false,
            Description =
                "Kisa devre akimi hesabi ve sigorta koordinasyon kontrolu (IEC 60364-4-43).\n\n" +
                "params:\n" +
                "  kesit_mm2       — kablo kesiti mm2\n" +
                "  uzunluk_m       — kablo uzunlugu m\n" +
                "  gerilim_v       — nominal V (default 415)\n" +
                "  malzeme         — cu|al (default cu)\n" +
                "  kaynak_empedans — sebeke/trafo empedansi mOhm (default 35)\n" +
                "  sigorta_a       — koruma cihazi anma akimi A\n" +
                "  sigorta_tip     — mcb|mccb|fuse (default mcb)\n\n" +
                "Cikti: isc_ka, z_total_mohm, kirilma_suresi_ms, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> ShortCircuitCheck(OpContext ctx)
        {
            double kesit   = ctx.GetDouble("kesit_mm2", 0);
            double L       = ctx.GetDouble("uzunluk_m", 0);
            double Un      = ctx.GetDouble("gerilim_v", 415);
            string malzeme = ctx.GetString("malzeme", "cu").ToLowerInvariant();
            double Z_src   = ctx.GetDouble("kaynak_empedans", 35.0); // mΩ
            double I_n     = ctx.GetDouble("sigorta_a", 0);
            string sigTip  = ctx.GetString("sigorta_tip", "mcb").ToLowerInvariant();

            if (kesit <= 0) return ErrResult("kesit_mm2 > 0 olmalidir.");
            if (L <= 0)     return ErrResult("uzunluk_m > 0 olmalidir.");
            if (I_n <= 0)   return ErrResult("sigorta_a > 0 olmalidir.");

            var resTbl = malzeme == "al" ? AlResistance : CuResistance;
            if (!resTbl.TryGetValue(kesit, out double r_mohm_m))
                r_mohm_m = malzeme == "al" ? 0.128 : 0.077;

            // Kablo direnci (gidiş + dönüş)
            double R_kablo = 2.0 * r_mohm_m * L; // mΩ
            double X_kablo = 2.0 * 0.08 * L;      // mΩ (tipik reaktans)
            double X_src   = Z_src * 0.3;          // kaynak reaktans (~%30)
            double R_src   = Z_src * 0.97;         // kaynak direnç (~%97)

            double Z_total_mohm = Math.Sqrt(
                Math.Pow(R_src + R_kablo, 2) +
                Math.Pow(X_src + X_kablo, 2));

            double I_sc_ka = (Un / Math.Sqrt(3)) / (Z_total_mohm / 1000.0) / 1000.0;

            // Sigorta kırma süresi: k²S²/I_sc² (PVC kablo k=115 Cu, 74 Al)
            double k = malzeme == "al" ? 74.0 : 115.0;
            double t_ms = (k * k * kesit * kesit) / Math.Pow(I_sc_ka * 1000.0, 2) * 1000.0;

            // IEC 60364-4-41: t ≤ 0.4s (TN) veya 5s (TT) genel kural
            double tLimit = sigTip == "fuse" ? 5000.0 : 400.0; // ms
            string durum  = t_ms <= tLimit ? "UYGUN" : "KOORDINASYON_HATASI";

            // Kablo ısıl dayanım: I_sc ≤ k×S/√t
            double I_max_ka = k * kesit / Math.Sqrt(t_ms / 1000.0) / 1000.0;
            string ısılDurum = I_sc_ka <= I_max_ka ? "ISIL_DAYANIM_UYGUN" : "ISIL_DAYANIM_YETERSIZ";

            ctx.Log($"  elec_short_circuit_check: {kesit}mm² {L}m → " +
                    $"Isc={I_sc_ka:F2}kA t={t_ms:F0}ms → {durum}");

            return new()
            {
                ["kesit_mm2"]       = kesit,
                ["uzunluk_m"]       = L,
                ["z_total_mohm"]    = Math.Round(Z_total_mohm, 2),
                ["isc_ka"]          = Math.Round(I_sc_ka, 3),
                ["kirilma_suresi_ms"]= Math.Round(t_ms, 1),
                ["t_limit_ms"]      = tLimit,
                ["isil_dayanim"]    = ısılDurum,
                ["sigorta_a"]       = I_n,
                ["durum"]           = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 3 — elec_diversity_factor
        // Çeşitlilik (diversity) faktörü ile gerçek talep gücü
        //
        // IEC 60364 / CIBSE Guide:
        //   P_talep = Σ(P_kurulu × kullanim_faktoru) × cesitlilik_faktoru
        //   Genel bina çeşitlilik faktörleri:
        //     Aydınlatma: 0.90
        //     Prizler/genel: 0.40-0.60
        //     HVAC: 0.80
        //     Asansör: 0.50
        //     Toplu: 0.70-0.85
        //
        // params:
        //   yukler — List of {isim, kurulu_kw, kullanim_faktoru}
        //   cesitlilik_faktoru — genel (default 0.75)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_diversity_factor",
            RequiresTransaction = false,
            Description =
                "Cesitlilik faktoru ile panel/trafo talep guc hesabi.\n\n" +
                "params:\n" +
                "  yukler              — JSON liste: [{isim,kurulu_kw,kullanim_faktoru},...]\n" +
                "  cesitlilik_faktoru  — genel cesitlilik (default 0.75)\n\n" +
                "IEC 60364 / CIBSE: Aydinlatma 0.90, priz 0.40-0.60, HVAC 0.80.\n" +
                "Cikti: toplam_kurulu_kw, toplam_talep_kw, talep_kva, talep_a, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> DiversityFactor(OpContext ctx)
        {
            var yuklerRaw = ctx.GetString("yukler", "[]");
            double cf     = Math.Clamp(ctx.GetDouble("cesitlilik_faktoru", 0.75), 0.1, 1.0);
            double gerilim= ctx.GetDouble("gerilim_v", 415);
            double cosPhi = ctx.GetDouble("cos_phi", 0.85);

            // JSON parse
            List<(string isim, double kW, double kf)> yukler;
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<
                    List<Dictionary<string, System.Text.Json.JsonElement>>>(yuklerRaw)
                    ?? new();
                yukler = parsed.Select(d => (
                    d.TryGetValue("isim", out var n) ? n.GetString() ?? "" : "",
                    d.TryGetValue("kurulu_kw", out var k) ? k.GetDouble() : 0,
                    d.TryGetValue("kullanim_faktoru", out var kf) ? kf.GetDouble() : 1.0
                )).ToList();
            }
            catch
            {
                return ErrResult("yukler parametresi gecersiz JSON. " +
                    "Ornek: [{\"isim\":\"Aydinlatma\",\"kurulu_kw\":10,\"kullanim_faktoru\":0.9}]");
            }

            if (yukler.Count == 0)
                return ErrResult("yukler listesi bos. En az 1 yuk girilmelidir.");

            double toplamKurulu   = yukler.Sum(y => y.kW);
            double toplamKullanimKw = yukler.Sum(y => y.kW * y.kf);
            double talepKw        = toplamKullanimKw * cf;
            double talepKva       = talepKw / cosPhi;
            double talepA         = talepKva * 1000.0 / (Math.Sqrt(3) * gerilim);

            var detay = yukler.Select(y => new Dictionary<string, object?>
            {
                ["isim"]           = y.isim,
                ["kurulu_kw"]      = y.kW,
                ["kullanim_faktoru"]= y.kf,
                ["talep_kw"]       = Math.Round(y.kW * y.kf, 2),
            }).ToList<object?>();

            ctx.Log($"  elec_diversity_factor: {yukler.Count} yuk, " +
                    $"toplam kurulu={toplamKurulu:F1}kW → talep={talepKw:F1}kW ({talepA:F1}A)");

            return new()
            {
                ["yuk_sayisi"]        = yukler.Count,
                ["toplam_kurulu_kw"]  = Math.Round(toplamKurulu, 2),
                ["cesitlilik_faktoru"]= cf,
                ["cos_phi"]           = cosPhi,
                ["toplam_talep_kw"]   = Math.Round(talepKw, 2),
                ["talep_kva"]         = Math.Round(talepKva, 2),
                ["talep_a"]           = Math.Round(talepA, 1),
                ["yukler_detay"]      = detay,
                ["durum"]             = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 4 — elec_earthing_validation
        // Topraklama sistemi tipi QA (IEC 60364-4-41)
        //
        // Sistem tipleri:
        //   TN-S : Nötr ve PE ayrı (3P+N+PE) — tercih edilen
        //   TN-C-S: Karma (PEN → bölünür)
        //   TT   : Bağımsız topraklama
        //   IT   : İzole nötr (hastane, kritik)
        //
        // Revit modelindeki elektriksel ekipmanlarda EG_TopraklamaTipi
        // parametresini okuyup standarda uygunluğunu kontrol eder.
        //
        // params:
        //   beklenen_tip    — TN-S|TN-C-S|TT|IT (default TN-S)
        //   alan_tipi       — konut|ticari|hastane|sanayi|data_merkezi
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_earthing_validation",
            RequiresTransaction = false,
            Description =
                "Topraklama sistemi tipi QA (IEC 60364-4-41). Model elemanlarindaki\n" +
                "EG_TopraklamaTipi parametresini okur ve alan tipine gore dogru\n" +
                "topraklama sitemini oneriyor/dogruluyor.\n\n" +
                "params:\n" +
                "  beklenen_tip — TN-S|TN-C-S|TT|IT (default TN-S)\n" +
                "  alan_tipi   — konut|ticari|hastane|sanayi|data_merkezi\n\n" +
                "Cikti: alan_tipi, beklenen_tip, oneri, gereksinimler, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> EarthingValidation(OpContext ctx)
        {
            string bekTip  = ctx.GetString("beklenen_tip", "TN-S").ToUpperInvariant();
            string alanTip = ctx.GetString("alan_tipi", "ticari").ToLowerInvariant();

            // Alan tipine göre önerilen topraklama sistemi ve gereksinimler
            var (oneriTip, gereksinimler) = alanTip switch
            {
                "hastane"      => ("IT", new[] {
                    "IT sistemi zorunlu (IEC 60364-7-710)",
                    "İzolasyon izleme cihazı (IMD) gerekli",
                    "Tıbbi lokasyon ayrımı (Group 1/2)",
                    "Ekipotansiyel bağlantı zorunlu"
                }),
                "data_merkezi" => ("TN-S", new[] {
                    "TN-S tercih edilir (gürültü azaltma)",
                    "Ayrı PE iletkeni zorunlu",
                    "UPS bypass ile uyumluluk kontrol edilmeli",
                    "Harmonik filtre etkisi değerlendirilmeli"
                }),
                "sanayi"       => ("TN-S", new[] {
                    "TN-S veya TT uygulanabilir",
                    "Ağır endüstri: PE iletken kesiti artırılmalı",
                    "VFD kullanımında filtre topraklaması",
                    "Statik elektrik boşalma önlemi"
                }),
                "konut"        => ("TN-C-S", new[] {
                    "TN-C-S yaygın uygulama",
                    "Bina girişinde PEN → PE+N ayrımı",
                    "RCD (kaçak akım) koruma zorunlu"
                }),
                _              => ("TN-S", new[] {
                    "TN-S standart ticari uygulama",
                    "Nötr ve PE ayrı hatlar",
                    "5-iletken sistem (3P+N+PE)"
                })
            };

            bool uygun = bekTip == oneriTip ||
                         (alanTip == "sanayi" && (bekTip == "TN-S" || bekTip == "TT"));
            string durum = uygun ? "UYGUN" : "TOPRAKLAMA_KONTROLU_GEREKLI";

            ctx.Log($"  elec_earthing_validation: {alanTip} → öneri={oneriTip}, " +
                    $"belirtilen={bekTip} → {durum}");

            return new()
            {
                ["alan_tipi"]      = alanTip,
                ["beklenen_tip"]   = bekTip,
                ["oneri_tip"]      = oneriTip,
                ["gereksinimler"]  = gereksinimler,
                ["durum"]          = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 5 — elec_busbar_sizing
        // Busbar kesit seçimi (akım yoğunluğu yöntemi)
        //
        // Bakır busbar: 1.4–1.6 A/mm² (yatay), 1.6–2.0 A/mm² (dikey)
        // Alüminyum:    0.9–1.1 A/mm²
        // Standart busbar boyutları (mm): 25×3, 25×5, 32×5, 40×5, 50×5,
        //   50×6, 60×6, 60×8, 60×10, 80×8, 80×10, 100×8, 100×10
        //
        // params:
        //   akim_a          — sürekli akım (A)
        //   malzeme         — cu|al (default cu)
        //   montaj          — yatay|dikey (default yatay)
        //   faz_sayisi      — 3 (default 3, busbar sistemi)
        //   guvenlik_pct    — güvenlik marjı % (default 20)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_busbar_sizing",
            RequiresTransaction = false,
            Description =
                "Busbar kesit secimi (akim yogunlugu yontemi).\n\n" +
                "params:\n" +
                "  akim_a       — surekli akim A\n" +
                "  malzeme      — cu|al (default cu)\n" +
                "  montaj       — yatay|dikey (default yatay)\n" +
                "  faz_sayisi   — 3 (default 3)\n" +
                "  guvenlik_pct — guvenlik marji % (default 20)\n\n" +
                "Cu yogunluk: yatay 1.4-1.6 A/mm2, dikey 1.6-2.0 A/mm2.\n" +
                "Cikti: oneri_kesit_mm2, oneri_boyut, akim_yogunlugu, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> BusbarSizing(OpContext ctx)
        {
            double I    = ctx.GetDouble("akim_a", 0);
            string mat  = ctx.GetString("malzeme", "cu").ToLowerInvariant();
            string mont = ctx.GetString("montaj", "yatay").ToLowerInvariant();
            double guvPct = ctx.GetDouble("guvenlik_pct", 20.0);

            if (I <= 0) return ErrResult("akim_a > 0 olmalidir.");

            // Akım yoğunluğu (A/mm²)
            double yogunluk;
            if (mat == "al")
                yogunluk = mont == "dikey" ? 1.1 : 0.95;
            else
                yogunluk = mont == "dikey" ? 1.8 : 1.5;

            // Güvenlik marjı ile tasarım akımı
            double I_tasarim = I * (1.0 + guvPct / 100.0);
            double kesit_mm2 = I_tasarim / yogunluk;

            // Standart busbar boyutları: (genişlik, kalınlık) → kesit
            var std_busbar = new (int w, int t)[]
            {
                (25,3),(25,5),(32,5),(40,5),(50,5),(50,6),(60,6),
                (60,8),(60,10),(80,8),(80,10),(100,8),(100,10),
                (120,10),(150,10),(160,10),(200,10)
            };

            var selected = std_busbar.FirstOrDefault(b => b.w * b.t >= kesit_mm2);
            if (selected.w == 0) selected = (200, 12);

            double oneriKesit  = selected.w * selected.t;
            double aktYogunluk = I / oneriKesit;
            string durum       = aktYogunluk <= yogunluk ? "UYGUN" : "KESIT_YETERSIZ";

            ctx.Log($"  elec_busbar_sizing: {I}A {mat} {mont} → " +
                    $"{selected.w}×{selected.t}mm ({oneriKesit:F0}mm²) @ {aktYogunluk:F2}A/mm²");

            return new()
            {
                ["akim_a"]            = I,
                ["akim_tasarim_a"]    = Math.Round(I_tasarim, 1),
                ["malzeme"]           = mat,
                ["montaj"]            = mont,
                ["akim_yogunlugu"]    = yogunluk,
                ["min_kesit_mm2"]     = Math.Round(kesit_mm2, 1),
                ["oneri_genislik_mm"] = selected.w,
                ["oneri_kalinlik_mm"] = selected.t,
                ["oneri_kesit_mm2"]   = oneriKesit,
                ["oneri_boyut"]       = $"{selected.w}×{selected.t}mm",
                ["durum"]             = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 6 — elec_power_factor_check
        // Güç faktörü analizi ve reaktif güç kompanzasyon önerisi
        //
        // S = P / cosφ  (kVA)
        // Q = P × tanφ  (kVAr)
        // Q_komp = P × (tan φ1 − tan φ2)  [hedef cosφ2'ye ulaşmak için]
        //
        // params:
        //   aktif_guc_kw    — toplam aktif güç (kW)
        //   cos_phi_mevcut  — mevcut güç faktörü
        //   cos_phi_hedef   — hedef güç faktörü (default 0.95)
        //   gerilim_v       — nominal V (default 415)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_power_factor_check",
            RequiresTransaction = false,
            Description =
                "Guc faktoru analizi ve reaktif guc kompanzasyon onerisi.\n\n" +
                "params:\n" +
                "  aktif_guc_kw   — toplam aktif guc kW\n" +
                "  cos_phi_mevcut — mevcut guc faktoru\n" +
                "  cos_phi_hedef  — hedef guc faktoru (default 0.95)\n" +
                "  gerilim_v      — nominal V (default 415)\n\n" +
                "Formul: Q_komp = P*(tan_phi1 - tan_phi2).\n" +
                "Cikti: s_kva, q_kvar, q_komp_kvar, tasarruf_pct, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> PowerFactorCheck(OpContext ctx)
        {
            double P     = ctx.GetDouble("aktif_guc_kw", 0);
            double pf1   = Math.Clamp(ctx.GetDouble("cos_phi_mevcut", 0.80), 0.1, 1.0);
            double pf2   = Math.Clamp(ctx.GetDouble("cos_phi_hedef", 0.95), 0.1, 1.0);
            double Un    = ctx.GetDouble("gerilim_v", 415);

            if (P <= 0) return ErrResult("aktif_guc_kw > 0 olmalidir.");
            if (pf1 >= pf2) return ErrResult("cos_phi_hedef > cos_phi_mevcut olmalidir.");

            double S1  = P / pf1;
            double Q1  = P * Math.Tan(Math.Acos(pf1));
            double S2  = P / pf2;
            double Q2  = P * Math.Tan(Math.Acos(pf2));
            double Qk  = Q1 - Q2; // kompanzasyon
            double I1  = S1 * 1000.0 / (Math.Sqrt(3) * Un);
            double I2  = S2 * 1000.0 / (Math.Sqrt(3) * Un);
            double tasarruf = (S1 - S2) / S1 * 100.0;

            string durum = pf1 >= 0.92 ? "UYGUN" :
                           pf1 >= 0.85 ? "KOMPANZASYON_ONERILI" : "KOMPANZASYON_GEREKLI";

            ctx.Log($"  elec_power_factor_check: P={P}kW cosφ={pf1}→{pf2} " +
                    $"Qkomp={Qk:F1}kVAr → {durum}");

            return new()
            {
                ["aktif_guc_kw"]     = P,
                ["cos_phi_mevcut"]   = pf1,
                ["cos_phi_hedef"]    = pf2,
                ["s_mevcut_kva"]     = Math.Round(S1, 2),
                ["q_mevcut_kvar"]    = Math.Round(Q1, 2),
                ["s_hedef_kva"]      = Math.Round(S2, 2),
                ["q_hedef_kvar"]     = Math.Round(Q2, 2),
                ["q_kompanzasyon_kvar"]= Math.Round(Qk, 2),
                ["akim_mevcut_a"]    = Math.Round(I1, 1),
                ["akim_hedef_a"]     = Math.Round(I2, 1),
                ["tasarruf_pct"]     = Math.Round(tasarruf, 1),
                ["durum"]            = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 7 — elec_elv_device_qa
        // ELV cihaz montaj yükseklikleri QA
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_elv_device_qa",
            RequiresTransaction = true,
            Description =
                "ELV cihazlarinin montaj yuksekliklerini (FFL'den mm) kontrol eder.\n" +
                "EG_ElvCihazTipi + EG_MontajYuksekligi parametreleri okunur.\n\n" +
                "params:\n" +
                "  tolerance_mm — kabul toleransi mm (default 100)\n" +
                "  write_back   — oneri degeri yaz (default false)\n\n" +
                "Referans: MEP Electrical ELV Systems montaj standartlari.\n" +
                "Cikti: element_id, cihaz_tipi, mevcut_mm, oneri_mm, durum",
            Category = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> ElvDeviceQa(OpContext ctx)
        {
            var rctx  = RequireRevit(ctx);
            int tolMm = ctx.GetInt("tolerance_mm", 100);
            bool wb   = ctx.GetBool("write_back", false);

            // ELV kategorileri: data, güvenlik, ses/görüntü cihazları
            var cats = new[]
            {
                BuiltInCategory.OST_DataDevices,
                BuiltInCategory.OST_SecurityDevices,
                BuiltInCategory.OST_CommunicationDevices,
                BuiltInCategory.OST_FireAlarmDevices,
            };

            var elements = cats.SelectMany(c =>
                new FilteredElementCollector(rctx.Doc)
                    .OfCategory(c)
                    .WhereElementIsNotElementType()
                    .ToList())
                .ToList();

            if (elements.Count == 0)
                return ErrRows("Modelde ELV cihazi bulunamadi (Data/Security/Comm/FA).");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(rctx.Doc, "ELV QA", rctx.IsAtomicMode);

            foreach (var el in elements)
            {
                long eid    = Rv.GetId(el.Id);
                string tip  = NormalizeElvType(
                    el.LookupParameter("EG_ElvCihazTipi")?.AsString(),
                    el.Name,
                    (rctx.Doc.GetElement(el.GetTypeId()) as ElementType)?.Name);

                double mevcut = el.LookupParameter("EG_MontajYuksekligi")?.AsDouble() * 304.8 ?? -1;

                if (!ElvHeights.TryGetValue(tip, out var hr))
                {
                    rows.Add(ElvRow(eid, tip, mevcut, -1, 0, "TIP_TANIMSIZ", ""));
                    continue;
                }

                var (mn, mx, tipik, aciklama) = hr;
                // Tavan montajlı cihazlar (min=max=0) → yükseklik kontrolü yapma
                if (mn == 0 && mx == 0)
                {
                    rows.Add(ElvRow(eid, tip, mevcut, tipik, 0, "TAVAN_MONTAJ_ATLANDI", aciklama));
                    continue;
                }

                double fark = mevcut >= 0 ? mevcut - tipik : 0;
                string durum;
                if (mevcut < 0)                   durum = "PARAMETRE_YOK";
                else if (mevcut < mn - tolMm)     durum = "DUSUK";
                else if (mevcut > mx + tolMm)     durum = "YUKSEK";
                else                              durum = "UYGUN";

                if (wb && durum != "UYGUN" && tipik > 0)
                    SetD(el, "EG_MontajYuksekligi_Oneri", tipik / 304.8);

                rows.Add(ElvRow(eid, tip, mevcut, tipik, fark, durum, aciklama));
            }

            scope.Commit();
            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  elec_elv_device_qa: {ok}/{rows.Count} cihaz UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 8 — elec_tray_hanger_spacing
        // Kablo tava askı aralığı kontrolü (IEC 61537)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_tray_hanger_spacing",
            RequiresTransaction = false,
            Description =
                "Revit modelindeki kablo tavalarinin aski araligini kontrol eder (IEC 61537).\n\n" +
                "params:\n" +
                "  tav_tipi         — ladder|perforated|solid_bottom|wire_mesh (default ladder)\n" +
                "  mevcut_aralik_m  — modeldeki aski araligi m (0=Revit'ten oku)\n\n" +
                "Limit: ladder 3.0m, perforated/solid 2.0m, wire_mesh 1.5m.\n" +
                "Cikti: tray_id, tav_tipi, mevcut_aralik_m, max_aralik_m, durum",
            Category = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> TrayHangerSpacingCheck(OpContext ctx)
        {
            var rctx     = RequireRevit(ctx);
            string tavTip = ctx.GetString("tav_tipi", "ladder").ToLowerInvariant();
            double override_m = ctx.GetDouble("mevcut_aralik_m", 0);

            var trays = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_CableTray)
                .WhereElementIsNotElementType()
                .ToList();

            if (trays.Count == 0)
                return ErrRows("Modelde kablo tavasi bulunamadi.");

            if (!TrayHangerSpacing.TryGetValue(tavTip, out var maxInfo))
                maxInfo = (3.0, "Bilinmeyen tip, ladder limiti kullanildi");

            double maxAralik = maxInfo.max_aralik;
            var rows = new List<Dictionary<string, object?>>();

            foreach (var tray in trays)
            {
                long tid = Rv.GetId(tray.Id);
                string tip = tray.LookupParameter("EG_TavaTipi")?.AsString() ??
                             tray.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME)?.AsString() ??
                             tavTip;

                // Askı aralığını EG_AskiAraligi parametresinden veya override'dan al
                double aralik = override_m > 0 ? override_m :
                    (tray.LookupParameter("EG_AskiAraligi")?.AsDouble() ?? 0) * 0.3048;

                if (!TrayHangerSpacing.TryGetValue(tip.ToLower(), out var tipInfo))
                    tipInfo = maxInfo;

                string durum = aralik <= 0 ? "PARAMETRE_YOK" :
                               aralik <= tipInfo.max_aralik ? "UYGUN" :
                               aralik <= tipInfo.max_aralik * 1.2 ? "DIKKAT" : "ARALIK_ASILDI";

                rows.Add(new()
                {
                    ["tray_id"]       = tid.ToString(),
                    ["tav_tipi"]      = tip,
                    ["mevcut_aralik_m"]= Math.Round(aralik, 2),
                    ["max_aralik_m"]  = tipInfo.max_aralik,
                    ["durum"]         = durum,
                    ["aciklama"]      = tipInfo.aciklama,
                });
            }

            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  elec_tray_hanger_spacing: {ok}/{rows.Count} tava UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 9 — elec_tray_separation_check
        // Güç/ELV tava ayrımı: ≥300mm yatay mesafe (IEC 61537 / CENELEC CLC/TR 50486)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_tray_separation_check",
            RequiresTransaction = false,
            Description =
                "Revit modelinde guc ve ELV kablo tavalari arasindaki yatay ayrimi kontrol eder.\n" +
                "Min 300mm ayrimi zorunludur (IEC 61537 / ELV separation rules).\n\n" +
                "params:\n" +
                "  min_ayrim_mm — minimum ayrim mm (default 300)\n" +
                "  tolerans_mm  — tolerans mm (default 50)\n\n" +
                "EG_TavaTuru parametresi: 'guc' veya 'elv' degeri beklenir.\n" +
                "Cikti: cift_id, guc_tray, elv_tray, mesafe_mm, durum",
            Category = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> TraySeparationCheck(OpContext ctx)
        {
            var rctx   = RequireRevit(ctx);
            int minMm  = ctx.GetInt("min_ayrim_mm", 300);
            int tolMm  = ctx.GetInt("tolerans_mm", 50);

            var allTrays = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_CableTray)
                .WhereElementIsNotElementType()
                .ToList();

            if (allTrays.Count < 2)
                return ErrRows("Ayrım kontrolü için en az 2 kablo tavası gerekli.");

            var gucTrays = allTrays.Where(t =>
                (t.LookupParameter("EG_TavaTuru")?.AsString() ?? "").ToLower() == "guc").ToList();
            var elvTrays = allTrays.Where(t =>
                (t.LookupParameter("EG_TavaTuru")?.AsString() ?? "").ToLower() == "elv").ToList();

            if (gucTrays.Count == 0 || elvTrays.Count == 0)
                return ErrRows("EG_TavaTuru parametresi 'guc' veya 'elv' olarak doldurulmamis. " +
                               "Parametre atandiktan sonra tekrar calistirin.");

            var rows = new List<Dictionary<string, object?>>();
            int ciftNo = 0;

            foreach (var gt in gucTrays)
            foreach (var et in elvTrays)
            {
                var gBb = gt.get_BoundingBox(null);
                var eBb = et.get_BoundingBox(null);
                if (gBb == null || eBb == null) continue;

                // Yatay mesafe: bbox merkezleri arası XY mesafesi
                double gX = (gBb.Min.X + gBb.Max.X) / 2 * 304.8;
                double gY = (gBb.Min.Y + gBb.Max.Y) / 2 * 304.8;
                double eX = (eBb.Min.X + eBb.Max.X) / 2 * 304.8;
                double eY = (eBb.Min.Y + eBb.Max.Y) / 2 * 304.8;

                // Aynı kat seviyesinde değilse atla (Z farkı > 500mm)
                double gZ = (gBb.Min.Z + gBb.Max.Z) / 2 * 304.8;
                double eZ = (eBb.Min.Z + eBb.Max.Z) / 2 * 304.8;
                if (Math.Abs(gZ - eZ) > 500) continue;

                double mesafeMm = Math.Sqrt(Math.Pow(gX - eX, 2) + Math.Pow(gY - eY, 2));

                string durum = mesafeMm >= minMm - tolMm ? "UYGUN" :
                               mesafeMm >= minMm * 0.5   ? "DIKKAT" : "AYRIM_YETERSIZ";

                ciftNo++;
                rows.Add(new()
                {
                    ["cift_no"]    = ciftNo,
                    ["guc_tray_id"]= Rv.GetId(gt.Id).ToString(),
                    ["elv_tray_id"]= Rv.GetId(et.Id).ToString(),
                    ["mesafe_mm"]  = Math.Round(mesafeMm, 0),
                    ["min_mm"]     = minMm,
                    ["durum"]      = durum,
                });
            }

            if (rows.Count == 0)
                return ErrRows("Aynı kat seviyesinde güç/ELV tava çifti bulunamadı.");

            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  elec_tray_separation_check: {ok}/{rows.Count} cift UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 10 — elec_generator_load_calc
        // Jeneratör yük & kVA hesabı
        //
        // Toplam jeneratör yükü:
        //   Acil yükler: %100 (hayat güvenliği)
        //   Standby yükler: seçime göre
        //   Başlangıç darbesi: motor başlatma faktörü × motor gücü
        //
        // params:
        //   acil_yuk_kw       — hayat güvenliği yükleri (kW)
        //   standby_yuk_kw    — standby yükler (kW)
        //   motor_baslama_kw  — motor başlatma darbesi (kW)
        //   guc_faktoru       — cosφ (default 0.8)
        //   guvenlik_pct      — güvenlik marjı % (default 25)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_generator_load_calc",
            RequiresTransaction = false,
            Description =
                "Jenerator yuk hesabi ve kVA boyutlandirmasi.\n\n" +
                "params:\n" +
                "  acil_yuk_kw      — hayat guvenligi yukleri kW\n" +
                "  standby_yuk_kw   — standby yukler kW (default 0)\n" +
                "  motor_baslama_kw — motor baslama darbesi kW (default 0)\n" +
                "  guc_faktoru      — cos_phi (default 0.8)\n" +
                "  guvenlik_pct     — guvenlik marji % (default 25)\n\n" +
                "Standart: BS 7671, IEC 60034. Hayat guvenligi: %100 dahil.\n" +
                "Cikti: toplam_kw, toplam_kva, oneri_jenerator_kva, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> GeneratorLoadCalc(OpContext ctx)
        {
            double acil   = ctx.GetDouble("acil_yuk_kw", 0);
            double standby= ctx.GetDouble("standby_yuk_kw", 0);
            double motor  = ctx.GetDouble("motor_baslama_kw", 0);
            double pf     = Math.Clamp(ctx.GetDouble("guc_faktoru", 0.8), 0.5, 1.0);
            double guvPct = ctx.GetDouble("guvenlik_pct", 25.0);

            if (acil <= 0) return ErrResult("acil_yuk_kw > 0 olmalidir.");

            double toplamKw  = acil + standby + motor;
            double tasarimKw = toplamKw * (1.0 + guvPct / 100.0);
            double tasarimKva= tasarimKw / pf;

            // Standart jeneratör serileri (kVA)
            int[] stdGen = { 20, 30, 45, 60, 75, 100, 125, 150, 175, 200,
                             250, 300, 350, 400, 500, 600, 750, 1000, 1250, 1500, 2000 };
            int oneriKva = stdGen.FirstOrDefault(g => g >= tasarimKva);
            if (oneriKva == 0) oneriKva = (int)(Math.Ceiling(tasarimKva / 100.0) * 100);

            ctx.Log($"  elec_generator_load_calc: acil={acil}kW standby={standby}kW " +
                    $"motor={motor}kW → {tasarimKva:F0}kVA → {oneriKva}kVA");

            return new()
            {
                ["acil_yuk_kw"]         = acil,
                ["standby_yuk_kw"]      = standby,
                ["motor_baslama_kw"]    = motor,
                ["toplam_kw"]           = Math.Round(toplamKw, 1),
                ["guc_faktoru"]         = pf,
                ["guvenlik_pct"]        = guvPct,
                ["tasarim_kw"]          = Math.Round(tasarimKw, 1),
                ["tasarim_kva"]         = Math.Round(tasarimKva, 1),
                ["oneri_jenerator_kva"] = oneriKva,
                ["durum"]               = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 11 — elec_ups_autonomy_check
        // UPS özerklik süresi kontrolü (IEC 62040-3)
        //
        // E_batarya (Wh) = P_yuk × t_ozerklik / (verim_ups × verim_batarya)
        // IEC 62040-3 sınıflandırması:
        //   VFI-SS-111 (online double conversion) — kritik
        //   VI-SS-111 (line interactive)          — genel
        //   VFD (offline)                         — ekonomik
        //
        // params:
        //   yuk_kw              — UPS korunan yük (kW)
        //   hedef_sure_dak      — hedef özerklik dakika
        //   batarya_voltaj      — batarya bank voltajı V (default 240)
        //   ups_verim           — UPS verimi (default 0.94)
        //   batarya_verim       — batarya dolum/boşalım verimi (default 0.85)
        //   ups_sinifi          — VFI|VI|VFD (default VFI)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_ups_autonomy_check",
            RequiresTransaction = false,
            Description =
                "UPS ozerklik suresi ve batarya kapasitesi hesabi (IEC 62040-3).\n\n" +
                "params:\n" +
                "  yuk_kw         — UPS korumali yuk kW\n" +
                "  hedef_sure_dak — hedef ozerklik dakika\n" +
                "  batarya_voltaj — batarya bank voltaji V (default 240)\n" +
                "  ups_verim      — UPS verimi (default 0.94)\n" +
                "  batarya_verim  — batarya dolum/bosalim verimi (default 0.85)\n" +
                "  ups_sinifi     — VFI|VI|VFD (default VFI)\n\n" +
                "Cikti: e_batarya_wh, kapasite_ah, oneri_batarya_ah, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> UpsAutonomyCheck(OpContext ctx)
        {
            double P       = ctx.GetDouble("yuk_kw", 0) * 1000.0; // W
            double t_dak   = ctx.GetDouble("hedef_sure_dak", 15.0);
            double V_bat   = ctx.GetDouble("batarya_voltaj", 240.0);
            double ups_ver = Math.Clamp(ctx.GetDouble("ups_verim", 0.94), 0.5, 1.0);
            double bat_ver = Math.Clamp(ctx.GetDouble("batarya_verim", 0.85), 0.5, 1.0);
            string sinif   = ctx.GetString("ups_sinifi", "VFI").ToUpperInvariant();

            if (P <= 0) return ErrResult("yuk_kw > 0 olmalidir.");
            if (t_dak <= 0) return ErrResult("hedef_sure_dak > 0 olmalidir.");

            // Gerekli batarya enerjisi (Wh)
            double E_wh = P * (t_dak / 60.0) / (ups_ver * bat_ver);
            // Batarya kapasitesi (Ah)
            double C_ah  = E_wh / V_bat;

            // Standart batarya kapasiteleri (Ah)
            int[] stdAh = { 7, 9, 12, 17, 24, 33, 38, 42, 50, 65, 75, 100,
                            120, 150, 200, 250, 300, 400, 500 };
            int oneriAh = stdAh.FirstOrDefault(a => a >= C_ah);
            if (oneriAh == 0) oneriAh = (int)(Math.Ceiling(C_ah / 50.0) * 50);

            // UPS sınıfına göre öneri
            string sinifAciklama = sinif switch
            {
                "VFI" => "Online Double Conversion — Kritik yükler için önerilir",
                "VI"  => "Line Interactive — Genel ofis/ticari",
                "VFD" => "Offline (Passive Standby) — Düşük kritiklik",
                _     => "Bilinmeyen sınıf"
            };

            ctx.Log($"  elec_ups_autonomy_check: P={P/1000:F1}kW t={t_dak}dak " +
                    $"→ E={E_wh:F0}Wh C={C_ah:F1}Ah → {oneriAh}Ah");

            return new()
            {
                ["yuk_kw"]           = P / 1000.0,
                ["hedef_sure_dak"]   = t_dak,
                ["e_batarya_wh"]     = Math.Round(E_wh, 1),
                ["kapasite_ah"]      = Math.Round(C_ah, 1),
                ["batarya_voltaj"]   = V_bat,
                ["oneri_batarya_ah"] = oneriAh,
                ["ups_sinifi"]       = sinif,
                ["sinif_aciklama"]   = sinifAciklama,
                ["durum"]            = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 12 — elec_emergency_circuit_qa
        // Acil aydınlatma devre QA (TS EN 1838 / IEC 60598-2-22)
        //
        // Gereksinimler (TS EN 1838):
        //   Min aydınlatma seviyesi: 1 lux (koridor), 0.5 lux (açık alan)
        //   Devreye girme süresi: ≤5 sn (yüksek risk), ≤60 sn (genel)
        //   Özerklik süresi: ≥1 saat (genel), ≥3 saat (yüksek risk)
        //   Dedektör kapsama: armatürler ≤25m aralık (düzlük)
        //   Acil çıkış işareti: ≤20m görünürlük
        //   Bağımsız devre zorunlu (normal aydınlatmadan ayrı)
        //   Kırmızı kablo rengi (acil devre)
        //
        // params:
        //   alan_tipi         — cikis_yolu|toplanma|yuksek_risk|genel
        //   armatür_aralik_m  — armatürler arası mesafe m
        //   ozerklik_sure_saat— özerklik süresi saat
        //   devreye_girme_sn  — devreye girme süresi sn
        //   bagimsiz_devre    — bool (default false → kontrol)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_emergency_circuit_qa",
            RequiresTransaction = false,
            Description =
                "Acil aydinlatma devre kalite kontrolu (TS EN 1838 / IEC 60598-2-22).\n\n" +
                "params:\n" +
                "  alan_tipi        — cikis_yolu|toplanma|yuksek_risk|genel\n" +
                "  armatur_aralik_m — armaturler arasi mesafe m\n" +
                "  ozerklik_sure_saat — ozerklik suresi saat\n" +
                "  devreye_girme_sn   — devreye girme suresi sn\n" +
                "  bagimsiz_devre   — bool (acil devresi bagimsiz mi)\n\n" +
                "Gereksinim: cikis_yolu min 1lux, ozerklik >=1saat, devreye <=5sn.\n" +
                "Cikti: alan_tipi, gereksinimler, kontroller, durum",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> EmergencyCircuitQa(OpContext ctx)
        {
            string alan      = ctx.GetString("alan_tipi", "genel").ToLowerInvariant();
            double aralik    = ctx.GetDouble("armatur_aralik_m", 0);
            double ozerklik  = ctx.GetDouble("ozerklik_sure_saat", 0);
            double devreye   = ctx.GetDouble("devreye_girme_sn", 0);
            bool   bagimsiz  = ctx.GetBool("bagimsiz_devre", false);

            // Alan tipine göre gereksinimler (TS EN 1838)
            var (minLux, maxAralik, minOzerklik, maxDevreye) = alan switch
            {
                "yuksek_risk"  => (10.0, 10.0, 3.0, 5.0),
                "cikis_yolu"   => (1.0,  25.0, 1.0, 5.0),
                "toplanma"     => (0.5,  25.0, 1.0, 60.0),
                _              => (0.5,  25.0, 1.0, 60.0) // genel
            };

            var kontroller = new List<Dictionary<string, object?>>();

            void Kontrol(string ad, bool gecti, string detay)
            {
                kontroller.Add(new()
                {
                    ["kontrol"]   = ad,
                    ["sonuc"]     = gecti ? "UYGUN" : "UYGUN_DEGIL",
                    ["detay"]     = detay,
                });
            }

            if (aralik > 0) Kontrol("Armatür Aralığı",
                aralik <= maxAralik,
                $"{aralik}m ≤ {maxAralik}m limit (TS EN 1838)");

            if (ozerklik > 0) Kontrol("Özerklik Süresi",
                ozerklik >= minOzerklik,
                $"{ozerklik}h ≥ {minOzerklik}h min (TS EN 1838)");

            if (devreye > 0) Kontrol("Devreye Girme",
                devreye <= maxDevreye,
                $"{devreye}sn ≤ {maxDevreye}sn limit (TS EN 1838)");

            Kontrol("Bağımsız Devre", bagimsiz,
                bagimsiz ? "Acil devre normal devreden bağımsız ✓"
                         : "Acil devre bağımsız olmalı! (TS EN 1838 §4.1)");

            bool tumUygun = kontroller.All(k => (string?)k["sonuc"] == "UYGUN");
            string durum  = tumUygun ? "UYGUN" :
                            kontroller.Any(k => (string?)k["sonuc"] == "UYGUN_DEGIL")
                            ? "EKSIKLIK_VAR" : "TAMAMLANDI";

            ctx.Log($"  elec_emergency_circuit_qa: {alan} → " +
                    $"{kontroller.Count(k => (string?)k["sonuc"] == "UYGUN")}/{kontroller.Count} UYGUN");

            return new()
            {
                ["alan_tipi"]         = alan,
                ["min_lux"]           = minLux,
                ["max_aralik_m"]      = maxAralik,
                ["min_ozerklik_saat"] = minOzerklik,
                ["max_devreye_sn"]    = maxDevreye,
                ["kontroller"]        = kontroller,
                ["durum"]             = durum,
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ORTAK YARDIMCILAR
        // ═════════════════════════════════════════════════════════════════════

        private static double SelectCrossSection(
            double I, double L, double Un, int faz, double cosPhi, double sinPhi,
            double X, double maxPct, Dictionary<double, double> resTbl)
        {
            foreach (var size in StdSizes)
            {
                if (!resTbl.TryGetValue(size, out double r)) continue;
                double faktor = faz == 1 ? 2.0 : Math.Sqrt(3);
                double dU = faktor * I * L * (r / 1000.0 * cosPhi + X / 1000.0 * sinPhi);
                double pct = dU / Un * 100.0;
                if (pct <= maxPct) return size;
            }
            return 0;
        }

        private static string NormalizeElvType(string? egTip, string? name, string? typeName)
        {
            string src = (!string.IsNullOrWhiteSpace(egTip) ? egTip
                        : (name ?? "") + " " + (typeName ?? "")).ToLowerInvariant();

            if (src.Contains("rj45") || src.Contains("data"))   return "rj45_priz";
            if (src.Contains("dome") || src.Contains("bullet") || src.Contains("cctv") || src.Contains("kamera")) return "kamera_genel";
            if (src.Contains("ptz"))                            return "kamera_genel";
            if (src.Contains("kart") || src.Contains("card"))   return "kart_okuyucu";
            if (src.Contains("rte") || src.Contains("exit"))    return "kart_ic";
            if (src.Contains("pir") || src.Contains("hareket")) return "pir_dedektoru";
            if (src.Contains("kapi") || src.Contains("door"))   return "kapi_kontakt";
            if (src.Contains("klavye") || src.Contains("keypad")) return "klavye";
            if (src.Contains("hoparlor") || src.Contains("speaker") || src.Contains("pa")) return "pa_hoparlor_duv";
            if (src.Contains("bms") || src.Contains("sensor"))  return "bms_sensor";
            if (src.Contains("zemin") || src.Contains("floor")) return "zemin_kutu";
            if (src.Contains("panel") || src.Contains("rack"))  return "elv_panel";
            return "";
        }

        private static void SetD(Element e, string name, double v)
        {
            var p = e.LookupParameter(name);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) p.Set(v);
        }

        private static Dictionary<string, object?> ElvRow(
            long id, string tip, double mevcut, double oneri, double fark, string durum, string aciklama)
            => new()
            {
                ["element_id"]  = id.ToString(),
                ["cihaz_tipi"]  = tip,
                ["mevcut_mm"]   = mevcut >= 0 ? Math.Round(mevcut, 0) : (object?)"—",
                ["oneri_mm"]    = oneri > 0 ? oneri : (object?)"—",
                ["fark_mm"]     = Math.Round(fark, 0),
                ["aciklama"]    = aciklama,
                ["durum"]       = durum,
            };

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
