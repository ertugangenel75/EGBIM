// ============================================================
// EGBIMOTO — MEP Mekanik Hesap Motoru (MepHvacCalcOps) — v10.4
// Apache 2.0 — EGBIM / Ertugan Gocer
// ============================================================
// Op listesi (10 adet):
//   1.  mep_hvac_heat_load_calc   — Isı yükü hesabı (TS 825 / ASHRAE yöntemi)
//   2.  mep_ahu_selection         — AHU seçim kriterleri (debi, soğutma, ısıtma)
//   3.  mep_cooling_load_room     — Oda bazlı soğutma yükü (güneş + iç yük + infiltrasyon)
//   4.  mep_static_pressure_calc  — Kanal statik basınç ve fan ESP hesabı
//   5.  mep_fresh_air_rate_check  — Taze hava oranı kontrolü (kişi başı + alan bazlı)
//   6.  mep_pressurization_check  — Basınçlandırma kontrolü (ameliyathane/temiz oda/mutfak)
//   7.  mep_hvac_zone_balance     — HVAC zon debi denge kontrolü
//   8.  mep_hepa_filter_qa        — HEPA H14 filtre uygunluk QA
//   9.  mep_ach_by_room_type      — Oda tipine göre ACH tablosu (referans + doğrulama)
//  10.  mep_chiller_cop_check     — Chiller COP / enerji verimliliği kontrolü
//
// Standartlar:
//   TS 825           — Binalarda Isı Yalıtım Kuralları
//   ASHRAE 62.1-2022 — Ventilation for Acceptable Indoor Air Quality
//   ASHRAE 90.1      — Energy Standard for Buildings
//   EN 16798-1       — Indoor Environmental Input Parameters
//   EN 12831         — Isıtma sistemi ısı yükü hesabı
//   ISO 14644        — Temiz oda sınıflandırması
//   TR Sağlık Bakanlığı — Sağlık Yapıları Asgari Tasarım Standartları
//
// ⚠️ Sorumluluk: Sonuçlar sorumlu makine mühendisi tarafından doğrulanmalıdır.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    public static class MepHvacCalcOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // TABLOLAR
        // ─────────────────────────────────────────────────────────────────────

        // Oda tipi → ACH (hava değişim sayısı/saat) referans değerleri
        // Kaynak: ASHRAE 62.1, EN 16798-1, TR Sağlık Bakanlığı
        private static readonly Dictionary<string, (double minAch, double maxAch, double tipikAch, string standart)>
            AchReferenceTable = new(StringComparer.OrdinalIgnoreCase)
        {
            // Sağlık yapıları (TR Sağlık Bak. + ASHRAE 170)
            { "ameliyathane",    (20, 25, 20,  "TR Sağlık Bak. / ASHRAE 170") },
            { "yogun_bakim",     (12, 20, 15,  "ASHRAE 170") },
            { "hasta_odasi",     (6,  12, 8,   "ASHRAE 170") },
            { "muayene_odasi",   (6,  10, 8,   "ASHRAE 170") },
            { "temiz_koridor",   (4,  8,  6,   "ASHRAE 170") },
            { "laboratuvar",     (6,  15, 10,  "ASHRAE 62.1") },
            { "eczane",          (4,  8,  6,   "ASHRAE 170") },
            // Ofis / konut
            { "ofis",            (4,  8,  6,   "ASHRAE 62.1 / EN 16798") },
            { "konferans",       (6,  12, 8,   "ASHRAE 62.1") },
            { "konut",           (3,  6,  4,   "EN 16798-1") },
            { "mutfak",          (15, 30, 20,  "ASHRAE 62.1") },
            { "banyo",           (6,  12, 8,   "EN 16798-1") },
            { "depo",            (2,  4,  3,   "ASHRAE 62.1") },
            // Özel
            { "boya_odasi",      (20, 30, 25,  "OSHA / NFPA 33") },
            { "jenerator",       (12, 20, 15,  "NFPA 37") },
            { "otopark_kapali",  (4,  8,  6,   "ASHRAE 62.1 §6.2.5") },
            { "mutfak_ticari",   (30, 60, 40,  "ASHRAE 62.1") },
            { "sunucu_odasi",    (10, 20, 15,  "ASHRAE TC9.9") },
            { "temiz_oda_iso8",  (20, 40, 25,  "ISO 14644") },
            { "temiz_oda_iso7",  (60, 90, 70,  "ISO 14644") },
            { "temiz_oda_iso6",  (150,240,180, "ISO 14644") },
        };

        // Basınçlandırma tipleri (Pa) — referans basınç değerleri
        private static readonly Dictionary<string, (double basinc_pa, string tip, string standart)>
            PressureRef = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ameliyathane",     (15,  "POZİTİF", "TR Sağlık Bak. / EN 16798") },
            { "steril_koridor",   (10,  "POZİTİF", "ASHRAE 170") },
            { "on_hazirlik",      (5,   "POZİTİF", "ASHRAE 170") },
            { "genel_koridor",    (0,   "NÖTR",    "ASHRAE 170") },
            { "kirli_alan",       (-10, "NEGATİF", "ASHRAE 170") },
            { "banyo_tuvalet",    (-10, "NEGATİF", "EN 16798-1") },
            { "mutfak",           (-15, "NEGATİF", "ASHRAE 62.1") },
            { "izolasyon_odasi",  (-10, "NEGATİF", "ASHRAE 170 §7.2") },
            { "temiz_oda",        (35,  "POZİTİF", "ISO 14644-4") },
        };

        // Chiller COP referans değerleri (ASHRAE 90.1-2022)
        private static readonly Dictionary<string, (double minCop, double iyi, string teknoloji)>
            ChillerCopRef = new(StringComparer.OrdinalIgnoreCase)
        {
            { "hava_sogutmali",    (2.8, 3.5, "Air-cooled scroll/screw") },
            { "su_sogutmali_vida", (4.5, 6.0, "Water-cooled screw") },
            { "su_sogutmali_turbo",(5.5, 7.0, "Water-cooled centrifugal") },
            { "vrf",               (3.0, 4.5, "VRF/VRV") },
            { "isi_pompasi",       (3.0, 4.5, "Heat pump") },
            { "absorpsiyonlu",     (0.7, 1.2, "Absorption chiller (COP <1 normal)") },
        };

        // ─────────────────────────────────────────────────────────────────────
        // OP 1 — mep_hvac_heat_load_calc
        // Basit ısı yükü hesabı (TS 825 / ASHRAE yöntemi)
        //
        // Q_toplam = Q_kabuk + Q_ic_yuk + Q_infiltrasyon + Q_taze_hava
        //
        // Q_kabuk = U × A × ΔT  (duvar/çatı/döşeme/cam)
        // Q_ic_yuk = (kisi × 80W) + (ekipman W/m² × alan) + (aydinlatma W/m² × alan)
        // Q_taze_hava = rho × Cp × Q_taze × ΔT
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_hvac_heat_load_calc",
            RequiresTransaction = false,
            Description =
                "Basit isi yuku hesabi (TS 825 / ASHRAE). Kabuk + ic yuk + infiltrasyon + taze hava.\n\n" +
                "params:\n" +
                "  alan_m2           — kondisyonlu alan m2\n" +
                "  tavan_yuksekligi_m — tavan yuksekligi m (default 3.0)\n" +
                "  dis_sicaklik_c    — dis tasarim sicakligi C (yaz)\n" +
                "  ic_sicaklik_c     — ic tasarim sicakligi C (default 24)\n" +
                "  u_deger_ortalama  — ortalama U degeri W/m2K (default 0.5)\n" +
                "  ic_yuk_w_m2       — ic yuk yogunlugu W/m2 (default 30)\n" +
                "  kisi_sayisi       — kisi sayisi (default 0)\n" +
                "  taze_hava_m3h     — taze hava debisi m3/saat (default 0)\n\n" +
                "Cikti: toplam_w, sogutma_kw, sogutma_tr, kabuk_w, ic_yuk_w, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> HvacHeatLoadCalc(OpContext ctx)
        {
            double alan    = ctx.GetDouble("alan_m2", 0);
            double tavanH  = ctx.GetDouble("tavan_yuksekligi_m", 3.0);
            double disSic  = ctx.GetDouble("dis_sicaklik_c", 36.0); // Türkiye yaz
            double icSic   = ctx.GetDouble("ic_sicaklik_c", 24.0);
            double uOrtalama= ctx.GetDouble("u_deger_ortalama", 0.5);
            double icYukYog= ctx.GetDouble("ic_yuk_w_m2", 30.0);
            int    kisi    = ctx.GetInt("kisi_sayisi", 0);
            double tazeM3h = ctx.GetDouble("taze_hava_m3h", 0);

            if (alan <= 0) return ErrResult("alan_m2 > 0 olmalidir.");

            double deltaT  = Math.Abs(disSic - icSic);
            double hacimM3 = alan * tavanH;

            // Kabuk yükü: U × A_kabuk × ΔT
            // Kabuk alanı tahmini: tavan + yan duvarlar (4√alan × yükseklik)
            double aKabuk  = alan + 4.0 * Math.Sqrt(alan) * tavanH;
            double qKabuk  = uOrtalama * aKabuk * deltaT;

            // İç yük: ekipman + aydınlatma + kişi
            double qIcEkip = icYukYog * alan;
            double qKisi   = kisi * 80.0; // 80W/kişi (ASHRAE duyulur ısı)
            double qIcYuk  = qIcEkip + qKisi;

            // İnfiltrasyon (0.5 ACH tahmini)
            double qInfil  = 0.5 * hacimM3 / 3600.0 * 1.2 * 1006 * deltaT;

            // Taze hava yükü
            double qTaze   = tazeM3h > 0
                ? tazeM3h / 3600.0 * 1.2 * 1006 * deltaT
                : 0;

            double toplamW = qKabuk + qIcYuk + qInfil + qTaze;
            double toplamKw = toplamW / 1000.0;
            double toplamTr = toplamKw / 3.517; // 1 TR = 3.517 kW

            // Güvenlik faktörü %10
            double tasarimKw = toplamKw * 1.10;
            double tasarimTr = tasarimKw / 3.517;

            ctx.Log($"  mep_hvac_heat_load_calc: {alan}m² ΔT={deltaT}°C → " +
                    $"Q={toplamKw:F1}kW ({toplamTr:F1}TR) → tasarım={tasarimKw:F1}kW");

            return new()
            {
                ["alan_m2"]           = alan,
                ["delta_t_c"]         = deltaT,
                ["kabuk_w"]           = Math.Round(qKabuk, 0),
                ["ic_yuk_w"]          = Math.Round(qIcYuk, 0),
                ["infiltrasyon_w"]    = Math.Round(qInfil, 0),
                ["taze_hava_w"]       = Math.Round(qTaze, 0),
                ["toplam_w"]          = Math.Round(toplamW, 0),
                ["sogutma_kw"]        = Math.Round(toplamKw, 2),
                ["sogutma_tr"]        = Math.Round(toplamTr, 2),
                ["tasarim_kw"]        = Math.Round(tasarimKw, 2),
                ["tasarim_tr"]        = Math.Round(tasarimTr, 2),
                ["durum"]             = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2 — mep_ahu_selection
        // AHU (Hava İşleme Ünitesi) seçim kriterleri
        //
        // AHU seçim parametreleri:
        //   Hava debisi: Q_toplam = taze hava + sirkülasyon
        //   Soğutma kapasitesi: ısı yükünden
        //   Isıtma kapasitesi: kış yükünden
        //   Fan gücü: Q × ΔP / (ηfan × ηmotor)
        //   Filtre sınıfı: alan tipine göre (F5-H14)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_ahu_selection",
            RequiresTransaction = false,
            Description =
                "AHU secim kriterleri: debi, sogutma/isitma kapasitesi, fan gucu, filtre sinifi.\n\n" +
                "params:\n" +
                "  toplam_debi_m3h   — AHU toplam hava debisi m3/saat\n" +
                "  sogutma_kw        — sogutma kapasitesi kW\n" +
                "  isitma_kw         — isitma kapasitesi kW (default 0)\n" +
                "  esp_pa            — external static pressure Pa (default 300)\n" +
                "  filtre_sinifi     — F5|F7|F9|H13|H14 (default F7)\n" +
                "  alan_tipi         — ofis|hastane|ameliyathane|temiz_oda|genel\n\n" +
                "Cikti: fan_kw, ahu_boyutu, filtre_oneri, sogutma_kapasitesi, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> AhuSelection(OpContext ctx)
        {
            double debi    = ctx.GetDouble("toplam_debi_m3h", 0);
            double sogKw   = ctx.GetDouble("sogutma_kw", 0);
            double isitKw  = ctx.GetDouble("isitma_kw", 0);
            double espPa   = ctx.GetDouble("esp_pa", 300.0);
            string filtre  = ctx.GetString("filtre_sinifi", "F7").ToUpper();
            string alanTip = ctx.GetString("alan_tipi", "genel").ToLowerInvariant();

            if (debi <= 0) return ErrResult("toplam_debi_m3h > 0 olmalidir.");

            // Fan gücü: P = Q × ΔP / (ηfan × ηmotor)
            // Q m³/s, ΔP Pa → P W
            double debiM3s = debi / 3600.0;
            double etaFan  = 0.70; // tipik AHU fan verimi
            double etaMot  = 0.92; // motor verimi
            double fanW    = debiM3s * espPa / (etaFan * etaMot);
            double fanKw   = fanW / 1000.0;

            // Alan tipine göre filtre önerisi
            string filtreOneri = alanTip switch
            {
                "ameliyathane" => "H14 (HEPA)",
                "temiz_oda"    => "H14 (HEPA)",
                "hastane"      => "H13 (HEPA)",
                "laboratuvar"  => "F9",
                "ofis"         => "F7",
                _              => "F7",
            };

            // Filtre uygunluk kontrolü
            var filtreSira = new[] { "F5","F6","F7","F8","F9","H10","H11","H12","H13","H14" };
            int mevcutIdx  = Array.IndexOf(filtreSira, filtre);
            int oneriIdx   = Array.IndexOf(filtreSira, filtreOneri.Split(' ')[0]);
            string filtreDurum = mevcutIdx >= oneriIdx ? "UYGUN" :
                                 mevcutIdx < 0         ? "BILINMEYEN_SINIF" : "FILTRE_YETERSIZ";

            // AHU boyut tahmini (m²/m3h: tipik 0.03-0.05 m² kesit/1000 m3h)
            double kesitM2 = debi / 1000.0 * 0.04;
            string ahuBoyut = $"~{Math.Sqrt(kesitM2) * 1000:F0}×{Math.Sqrt(kesitM2) * 1000:F0}mm kesit";

            string genelDurum = filtreDurum == "UYGUN" ? "OK" : filtreDurum;

            ctx.Log($"  mep_ahu_selection: {debi}m³/h {espPa}Pa → " +
                    $"Fan={fanKw:F2}kW Filtre={filtre}({filtreDurum})");

            return new()
            {
                ["toplam_debi_m3h"]  = debi,
                ["esp_pa"]           = espPa,
                ["fan_kw"]           = Math.Round(fanKw, 2),
                ["fan_m3s"]          = Math.Round(debiM3s, 4),
                ["sogutma_kw"]       = sogKw,
                ["isitma_kw"]        = isitKw,
                ["filtre_mevcut"]    = filtre,
                ["filtre_oneri"]     = filtreOneri,
                ["filtre_durum"]     = filtreDurum,
                ["ahu_boyut_tahmini"]= ahuBoyut,
                ["alan_tipi"]        = alanTip,
                ["durum"]            = genelDurum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 3 — mep_cooling_load_room
        // Oda bazlı soğutma yükü (güneş + iç yük + infiltrasyon)
        //
        // ASHRAE CLTD/CLF yöntemi basitleştirilmiş versiyonu:
        //   Q_güneş = A_cam × U_cam × ΔT_etkin + A_cam × SC × SHGC × güneş_şiddeti
        //   Q_duvar = U_duvar × A_duvar × CLTD
        //   Q_iç    = kişi × sensible + ekipman + aydınlatma
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_cooling_load_room",
            RequiresTransaction = false,
            Description =
                "Oda bazli sogutma yuku hesabi (ASHRAE CLTD basitlestirilmis).\n\n" +
                "params:\n" +
                "  alan_m2          — oda alani m2\n" +
                "  cam_alani_m2     — toplam cam alani m2\n" +
                "  yon              — kuzey|guney|dogu|bati|cati (default guney)\n" +
                "  kisi_sayisi      — kisi sayisi\n" +
                "  ekipman_w        — ekipman gucu W\n" +
                "  aydinlatma_w_m2  — aydinlatma yogunlugu W/m2 (default 12)\n" +
                "  dis_sicaklik_c   — dis tasarim sicakligi C (default 36)\n" +
                "  ic_sicaklik_c    — ic tasarim sicakligi C (default 24)\n\n" +
                "Cikti: gunes_yuku_w, ic_yuk_w, toplam_sogutma_kw, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> CoolingLoadRoom(OpContext ctx)
        {
            double alan     = ctx.GetDouble("alan_m2", 0);
            double camAlan  = ctx.GetDouble("cam_alani_m2", 0);
            string yon      = ctx.GetString("yon", "guney").ToLowerInvariant();
            int    kisi     = ctx.GetInt("kisi_sayisi", 0);
            double ekipmanW = ctx.GetDouble("ekipman_w", 0);
            double aydW_m2  = ctx.GetDouble("aydinlatma_w_m2", 12.0);
            double disSic   = ctx.GetDouble("dis_sicaklik_c", 36.0);
            double icSic    = ctx.GetDouble("ic_sicaklik_c", 24.0);

            if (alan <= 0) return ErrResult("alan_m2 > 0 olmalidir.");

            double dT = disSic - icSic;

            // Güneş şiddeti katsayısı yöne göre (W/m² — Türkiye ortalama yaz)
            double gunesKats = yon switch
            {
                "guney" => 350,
                "bati"  => 420,
                "dogu"  => 380,
                "cati"  => 500,
                _       => 150, // kuzey
            };

            // Cam güneş yükü: SHGC × güneş şiddeti × cam alanı
            double shgc = 0.4; // tipik çift cam (low-e)
            double uCam = 2.0; // W/m²K
            double qCamGunes = camAlan * shgc * gunesKats;
            double qCamIletim= camAlan * uCam * dT;
            double qGunes = qCamGunes + qCamIletim;

            // Duvar iletim yükü (kalan yüzey, U=0.45 tipik)
            double aKalan = alan * 0.5; // yaklaşık
            double qDuvar = 0.45 * aKalan * dT;

            // İç yükler
            double qKisi   = kisi * 75.0; // 75W sensible/kişi (oturarak)
            double qEkipman= ekipmanW * 0.8; // CLF 0.8
            double qAyd    = aydW_m2 * alan * 0.85;
            double qIcYuk  = qKisi + qEkipman + qAyd;

            double toplamW  = qGunes + qDuvar + qIcYuk;
            double toplamKw = toplamW / 1000.0;

            ctx.Log($"  mep_cooling_load_room: {alan}m² {yon} " +
                    $"cam={camAlan}m² {kisi}kişi → {toplamKw:F2}kW");

            return new()
            {
                ["alan_m2"]         = alan,
                ["cam_alani_m2"]    = camAlan,
                ["yon"]             = yon,
                ["gunes_yuku_w"]    = Math.Round(qGunes, 0),
                ["duvar_iletim_w"]  = Math.Round(qDuvar, 0),
                ["ic_yuk_w"]        = Math.Round(qIcYuk, 0),
                ["toplam_w"]        = Math.Round(toplamW, 0),
                ["toplam_sogutma_kw"]= Math.Round(toplamKw, 3),
                ["yogunluk_w_m2"]   = Math.Round(toplamW / alan, 1),
                ["durum"]           = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 4 — mep_static_pressure_calc
        // Kanal statik basınç ve fan ESP hesabı
        //
        // ESP = Σ(R × L) + Σ(Z_fitting) + ΔP_filtre + ΔP_serpantin + ΔP_terminal
        // R = sürtünme kaybı (Pa/m) — Darcy-Weisbach
        // Alternatif: R = 0.9 Pa/m (sabit katsayı yöntemi, ASHRAE tavsiyesi)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_static_pressure_calc",
            RequiresTransaction = false,
            Description =
                "Kanal statik basinc ve fan ESP hesabi.\n\n" +
                "params:\n" +
                "  ana_hat_uzunlugu_m   — ana kanal hatti uzunlugu m\n" +
                "  debi_m3h             — kanal debisi m3/saat\n" +
                "  boyut_mm             — kanal buyuk boyutu mm (kare/dikdortgen)\n" +
                "  dp_filtre_pa         — filtre basınc kaybi Pa (default 150)\n" +
                "  dp_serpantin_pa      — serpantin basınc kaybi Pa (default 200)\n" +
                "  dp_terminal_pa       — terminal birim basınc kaybi Pa (default 50)\n" +
                "  boru_katsayi_pa_m    — kanal surtuenme katsayisi Pa/m (default 1.0)\n\n" +
                "ASHRAE: Ana hat R=0.8-1.2 Pa/m. ESP=hat+filtre+serpantin+terminal.\n" +
                "Cikti: r_pa_m, hat_kaybi_pa, toplam_esp_pa, fan_kw, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> StaticPressureCalc(OpContext ctx)
        {
            double hatUz   = ctx.GetDouble("ana_hat_uzunlugu_m", 0);
            double debiM3h = ctx.GetDouble("debi_m3h", 0);
            double boyutMm = ctx.GetDouble("boyut_mm", 400);
            double dpFiltre= ctx.GetDouble("dp_filtre_pa", 150.0);
            double dpSerp  = ctx.GetDouble("dp_serpantin_pa", 200.0);
            double dpTerm  = ctx.GetDouble("dp_terminal_pa", 50.0);
            double rKats   = ctx.GetDouble("boru_katsayi_pa_m", 1.0);

            if (hatUz <= 0)   return ErrResult("ana_hat_uzunlugu_m > 0 olmalidir.");
            if (debiM3h <= 0) return ErrResult("debi_m3h > 0 olmalidir.");

            // Hız hesabı (kanalda)
            double debiM3s = debiM3h / 3600.0;
            double kesitM2 = Math.Pow(boyutMm / 1000.0, 2); // kare kanal
            double hizMs   = kesitM2 > 0 ? debiM3s / kesitM2 : 5.0;

            // Sürtünme kaybı (Darcy yaklaşımı ya da sabit katsayı)
            // R = λ/D × ρv²/2  ya da kullanıcı katsayısı
            double rPaM    = rKats; // Pa/m
            double hatKaybi= rPaM * hatUz;

            // Bağlantı parçaları (hat kaybının %50 kuralı — ASHRAE)
            double fittingKaybi = hatKaybi * 0.5;

            double toplamEsp = hatKaybi + fittingKaybi + dpFiltre + dpSerp + dpTerm;

            // Fan gücü: P = Q × ESP / (ηfan × ηmotor)
            double fanW  = debiM3s * toplamEsp / (0.70 * 0.92);
            double fanKw = fanW / 1000.0;

            // Hız kontrolü (ASHRAE tavsiyesi: ana hat 6-8 m/s, dal 4-6 m/s)
            string hizDurum = hizMs < 4 ? "HIZ_DUSUK" :
                              hizMs > 10 ? "HIZ_YUKSEK" : "UYGUN";

            ctx.Log($"  mep_static_pressure_calc: {hatUz}m hat {debiM3h}m³/h " +
                    $"→ ESP={toplamEsp:F0}Pa Fan={fanKw:F2}kW");

            return new()
            {
                ["ana_hat_uzunlugu_m"]  = hatUz,
                ["debi_m3h"]            = debiM3h,
                ["hiz_m_s"]             = Math.Round(hizMs, 2),
                ["hiz_durum"]           = hizDurum,
                ["r_pa_m"]              = rPaM,
                ["hat_kaybi_pa"]        = Math.Round(hatKaybi, 1),
                ["fitting_kaybi_pa"]    = Math.Round(fittingKaybi, 1),
                ["dp_filtre_pa"]        = dpFiltre,
                ["dp_serpantin_pa"]     = dpSerp,
                ["dp_terminal_pa"]      = dpTerm,
                ["toplam_esp_pa"]       = Math.Round(toplamEsp, 1),
                ["fan_kw"]              = Math.Round(fanKw, 3),
                ["durum"]               = hizDurum == "UYGUN" ? "OK" : hizDurum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 5 — mep_fresh_air_rate_check
        // Taze hava oranı kontrolü (ASHRAE 62.1 / EN 16798-1)
        //
        // ASHRAE 62.1 Yöntemi (Bölüm 6.2.2):
        //   Vbz = Rp × Pz + Ra × Az
        //   Rp = kişi başı taze hava (L/s/kişi)
        //   Ra = alan bazlı taze hava (L/s/m²)
        //   Pz = kişi sayısı
        //   Az = alan (m²)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_fresh_air_rate_check",
            RequiresTransaction = false,
            Description =
                "Taze hava orani kontrolu (ASHRAE 62.1 / EN 16798-1).\n\n" +
                "params:\n" +
                "  alan_m2       — kondisyonlu alan m2\n" +
                "  kisi_sayisi   — tasarim kisi sayisi\n" +
                "  alan_tipi     — ofis|sinif|restoran|hastane|konut|toplanma\n" +
                "  mevcut_m3h    — modeldeki taze hava debisi m3/saat (0=hesap)\n\n" +
                "ASHRAE 62.1 Rp+Ra yontemi. Cikti: gerekli_l_s, gerekli_m3h, kisi_basi_l_s, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> FreshAirRateCheck(OpContext ctx)
        {
            double alan    = ctx.GetDouble("alan_m2", 0);
            int    kisi    = ctx.GetInt("kisi_sayisi", 0);
            string alanTip = ctx.GetString("alan_tipi", "ofis").ToLowerInvariant();
            double mevcutM3h= ctx.GetDouble("mevcut_m3h", 0);

            if (alan <= 0) return ErrResult("alan_m2 > 0 olmalidir.");

            // ASHRAE 62.1 Table 6-1 — Rp (L/s/kişi) + Ra (L/s/m²)
            var (rp, ra, standart) = alanTip switch
            {
                "ofis"       => (10.0, 0.3, "ASHRAE 62.1 Tablo 6-1"),
                "sinif"      => (5.0,  0.6, "ASHRAE 62.1"),
                "restoran"   => (10.0, 0.9, "ASHRAE 62.1"),
                "hastane"    => (10.0, 0.6, "ASHRAE 170"),
                "konut"      => (7.5,  0.3, "ASHRAE 62.2"),
                "toplanma"   => (7.5,  0.6, "ASHRAE 62.1"),
                "depo"       => (0.0,  0.3, "ASHRAE 62.1"),
                "otopark"    => (0.0,  7.6, "ASHRAE 62.1 §6.2.5"),
                _            => (10.0, 0.3, "ASHRAE 62.1"),
            };

            double gerekliLs  = rp * kisi + ra * alan;
            double gerekliM3h = gerekliLs * 3.6;
            double kisiBasiLs = kisi > 0 ? gerekliLs / kisi : 0;

            string durum = mevcutM3h <= 0 ? "HESAP_SONUCU" :
                           mevcutM3h * (1000.0/3600.0) >= gerekliLs ? "UYGUN" : "TAZE_HAVA_YETERSIZ";

            ctx.Log($"  mep_fresh_air_rate_check: {alan}m² {kisi}kişi {alanTip} → " +
                    $"{gerekliM3h:F0}m³/h gerekli → {durum}");

            return new()
            {
                ["alan_m2"]        = alan,
                ["kisi_sayisi"]    = kisi,
                ["alan_tipi"]      = alanTip,
                ["rp_l_s_kisi"]    = rp,
                ["ra_l_s_m2"]      = ra,
                ["gerekli_l_s"]    = Math.Round(gerekliLs, 1),
                ["gerekli_m3h"]    = Math.Round(gerekliM3h, 1),
                ["kisi_basi_l_s"]  = Math.Round(kisiBasiLs, 1),
                ["mevcut_m3h"]     = mevcutM3h > 0 ? mevcutM3h : (object?)"—",
                ["standart"]       = standart,
                ["durum"]          = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 6 — mep_pressurization_check
        // Basınçlandırma kontrolü (ameliyathane / temiz oda / mutfak)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_pressurization_check",
            RequiresTransaction = false,
            Description =
                "Oda basinclandirma kontrolu (TR Saglik Bak. / ASHRAE 170 / EN 16798).\n\n" +
                "params:\n" +
                "  oda_tipi         — ameliyathane|steril_koridor|on_hazirlik|\n" +
                "                     genel_koridor|kirli_alan|banyo_tuvalet|\n" +
                "                     mutfak|izolasyon_odasi|temiz_oda\n" +
                "  mevcut_basinc_pa — modeldeki basinc Pa (pozitif/negatif)\n" +
                "  tolerans_pa      — kabul toleransi Pa (default 2)\n\n" +
                "Cikti: hedef_basinc_pa, mevcut_basinc_pa, tip, standart, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> PressurizationCheck(OpContext ctx)
        {
            string odaTip  = ctx.GetString("oda_tipi", "genel_koridor").ToLowerInvariant();
            double mevcut  = ctx.GetDouble("mevcut_basinc_pa", double.NaN);
            double tol     = ctx.GetDouble("tolerans_pa", 2.0);

            if (!PressureRef.TryGetValue(odaTip, out var pRef))
                return ErrResult($"Bilinmeyen oda tipi: '{odaTip}'. " +
                    "Desteklenen: " + string.Join(", ", PressureRef.Keys));

            var (hedef, tip, standart) = pRef;

            string durum;
            if (double.IsNaN(mevcut))
                durum = "PARAMETRE_YOK";
            else if (Math.Abs(mevcut - hedef) <= tol)
                durum = "UYGUN";
            else if (tip == "POZİTİF" && mevcut < hedef - tol)
                durum = "BASINC_YETERSIZ";
            else if (tip == "NEGATİF" && mevcut > hedef + tol)
                durum = "NEGATIF_BASINC_YETERSIZ";
            else
                durum = "TOLERANS_DISINDA";

            ctx.Log($"  mep_pressurization_check: {odaTip} hedef={hedef}Pa " +
                    $"mevcut={mevcut}Pa → {durum}");

            return new()
            {
                ["oda_tipi"]           = odaTip,
                ["hedef_basinc_pa"]    = hedef,
                ["mevcut_basinc_pa"]   = double.IsNaN(mevcut) ? (object?)"—" : mevcut,
                ["basinclandirma_tipi"]= tip,
                ["tolerans_pa"]        = tol,
                ["standart"]           = standart,
                ["durum"]              = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 7 — mep_hvac_zone_balance
        // HVAC zon debi denge kontrolü
        //
        // Her HVAC zonunda:
        //   Toplam terminal debisi = Zone tasarım debisi
        //   Sapma toleransı: ±10% (ASHRAE 111)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_hvac_zone_balance",
            RequiresTransaction = true,
            Description =
                "HVAC zon debi denge kontrolu (ASHRAE 111). Her zonda terminal\n" +
                "debilerinin toplamini zon tasarim debiyle karsilastirir.\n\n" +
                "params:\n" +
                "  tolerans_pct — kabul sapma yuzde (default 10)\n" +
                "  debi_param   — terminal debi parametresi adi (default EG_Debi_m3h)\n\n" +
                "Revit: Space/Zone bazinda hava terminalleri toplanir.\n" +
                "Cikti: zon_id, zon_adi, tasarim_m3h, toplam_terminal_m3h, sapma_pct, durum",
            Category = "MEP-Mekanik")]
        public static List<Dictionary<string, object?>> HvacZoneBalance(OpContext ctx)
        {
            var rctx    = RequireRevit(ctx);
            double tol  = ctx.GetDouble("tolerans_pct", 10.0);
            string dpNm = ctx.GetString("debi_param", "EG_Debi_m3h");

            // Space (odalar/zonlar) topla
            var spaces = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces)
                .WhereElementIsNotElementType()
                .ToList();

            if (spaces.Count == 0)
                return ErrRows("Modelde MEP Space bulunamadı. " +
                               "Oda/zon basarinca tanımlandıktan sonra çalıştırın.");

            var rows = new List<Dictionary<string, object?>>();
            foreach (var space in spaces)
            {
                long sid    = Rv.GetId(space.Id);
                string isim = space.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";

                // Zon tasarım debisi (EG_TasarimDebi_m3h veya Space tasarım debisi)
                double tasarimM3h = space.LookupParameter("EG_TasarimDebi_m3h")?.AsDouble() ?? 0;
                if (tasarimM3h <= 0)
                    tasarimM3h = space.get_Parameter(BuiltInParameter.ROOM_DESIGN_SUPPLY_AIRFLOW_PARAM)
                                      ?.AsDouble() ?? 0;
                // Revit CFM → m³/h
                tasarimM3h *= 1.699; // CFM to m3/h

                // Space'te terminal debi toplamı
                double termToplamM3h = space.LookupParameter("EG_TerminalToplam_m3h")?.AsDouble() ?? 0;

                if (tasarimM3h <= 0 && termToplamM3h <= 0)
                {
                    rows.Add(ZoneRow(sid, isim, 0, 0, 0, "VERI_YOK"));
                    continue;
                }

                double sapma = tasarimM3h > 0
                    ? (termToplamM3h - tasarimM3h) / tasarimM3h * 100.0
                    : 0;

                string durum = Math.Abs(sapma) <= tol ? "UYGUN" :
                               Math.Abs(sapma) <= tol * 2 ? "DIKKAT" : "DENGE_BOZUK";

                rows.Add(ZoneRow(sid, isim, tasarimM3h, termToplamM3h, sapma, durum));
            }

            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  mep_hvac_zone_balance: {ok}/{rows.Count} zon dengeli");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 8 — mep_hepa_filter_qa
        // HEPA H14 filtre uygunluk QA
        //
        // EN 1822-1 / ISO 29463:
        //   H13: ≥99.95% verimlilik (MPPS'de)
        //   H14: ≥99.995% verimlilik (MPPS'de)
        //   Baskı düşümü: temiz halde ≤250 Pa (tipik)
        //   Montaj: gel-seal veya DOP testi zorunlu
        //   Yedek: 2 yıl veya ΔP > 2 × temiz basınç (hangisi önce)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_hepa_filter_qa",
            RequiresTransaction = false,
            Description =
                "HEPA filtre uygunluk QA (EN 1822-1 / ISO 29463).\n\n" +
                "params:\n" +
                "  filtre_sinifi     — H13|H14\n" +
                "  alan_tipi         — ameliyathane|temiz_oda|yogun_bakim|hastane\n" +
                "  mevcut_dp_pa      — mevcut basınc dususu Pa (0=kontrol atla)\n" +
                "  montaj_tipi       — gel_seal|mekanik|dop_test\n" +
                "  son_test_tarihi   — DOP/PAO test tarihi (YYYY-MM, kontrol icin)\n\n" +
                "Cikti: gerekli_sinif, mevcut_sinif, dp_durum, montaj_durum, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> HepaFilterQa(OpContext ctx)
        {
            string filtreS  = ctx.GetString("filtre_sinifi", "H14").ToUpper();
            string alanTip  = ctx.GetString("alan_tipi", "ameliyathane").ToLowerInvariant();
            double mevcutDp = ctx.GetDouble("mevcut_dp_pa", 0);
            string montaj   = ctx.GetString("montaj_tipi", "gel_seal").ToLowerInvariant();
            string testTar  = ctx.GetString("son_test_tarihi", "");

            // Alan tipine göre gerekli sınıf
            string gerekliS = alanTip switch
            {
                "ameliyathane" => "H14",
                "temiz_oda"    => "H14",
                "yogun_bakim"  => "H13",
                "hastane"      => "H13",
                _              => "H13",
            };

            // Sınıf kontrolü
            bool sinifUygun = filtreS == gerekliS ||
                              (filtreS == "H14" && gerekliS == "H13");
            string sinifDurum = sinifUygun ? "UYGUN" : "SINIF_YETERSIZ";

            // Basınç düşümü kontrolü (max 2× temiz durum ≈ 500 Pa)
            string dpDurum = mevcutDp <= 0 ? "KONTROL_EDILMEDI" :
                             mevcutDp <= 250 ? "UYGUN" :
                             mevcutDp <= 500 ? "DIKKAT_DEGISIM_YAKLASIK" :
                             "FILTER_DEGISIM_GEREKLI";

            // Montaj kontrolü — ameliyathane için gel-seal zorunlu
            string montajDurum = alanTip == "ameliyathane" && montaj != "gel_seal"
                ? "MONTAJ_UYUMSUZ" : "UYGUN";

            // Test tarihi kontrolü (2 yılda bir)
            string testDurum = "KONTROL_EDILMEDI";
            if (!string.IsNullOrEmpty(testTar))
            {
                try
                {
                    var tarih = DateTime.Parse(testTar + "-01");
                    var fark = DateTime.Now - tarih;
                    testDurum = fark.TotalDays > 730 ? "TEST_SURESI_DOLMUS" : "UYGUN";
                }
                catch { testDurum = "TARIH_FORMATI_HATALI"; }
            }

            string genelDurum = (sinifUygun && dpDurum != "FILTER_DEGISIM_GEREKLI"
                && montajDurum == "UYGUN") ? "UYGUN" : "KONTROL_GEREKLI";

            ctx.Log($"  mep_hepa_filter_qa: {filtreS} {alanTip} → {genelDurum}");

            return new()
            {
                ["alan_tipi"]       = alanTip,
                ["gerekli_sinif"]   = gerekliS,
                ["mevcut_sinif"]    = filtreS,
                ["sinif_durum"]     = sinifDurum,
                ["dp_pa"]           = mevcutDp > 0 ? mevcutDp : (object?)"—",
                ["dp_durum"]        = dpDurum,
                ["montaj_tipi"]     = montaj,
                ["montaj_durum"]    = montajDurum,
                ["test_tarihi"]     = string.IsNullOrEmpty(testTar) ? (object?)"—" : testTar,
                ["test_durum"]      = testDurum,
                ["durum"]           = genelDurum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 9 — mep_ach_by_room_type
        // Oda tipine göre ACH tablosu ve Revit model doğrulaması
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_ach_by_room_type",
            RequiresTransaction = true,
            Description =
                "Oda tipine gore ACH referans tablosu ve Revit oda dogrulamasi.\n\n" +
                "params:\n" +
                "  oda_tipi         — ameliyathane|ofis|hastane|mutfak|banyo vb.\n" +
                "  mevcut_ach       — modeldeki ACH degeri (0=hesap modu)\n" +
                "  oda_alani_m2     — oda alani (dogrulama icin)\n" +
                "  oda_yuksekligi_m — oda yuksekligi m\n" +
                "  write_back       — EG_ACH_Oneri'ye yaz (default false)\n\n" +
                "Kaynak: ASHRAE 62.1, ASHRAE 170, EN 16798, TR Saglik Bak.\n" +
                "Cikti: min_ach, max_ach, tipik_ach, gerekli_m3h, standart, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> AchByRoomType(OpContext ctx)
        {
            string odaTip  = ctx.GetString("oda_tipi", "ofis").ToLowerInvariant();
            double mevcutAch= ctx.GetDouble("mevcut_ach", 0);
            double alan    = ctx.GetDouble("oda_alani_m2", 0);
            double yuksek  = ctx.GetDouble("oda_yuksekligi_m", 3.0);
            bool   wb      = ctx.GetBool("write_back", false);

            if (!AchReferenceTable.TryGetValue(odaTip, out var achData))
                return ErrResult($"Bilinmeyen oda tipi: '{odaTip}'. " +
                    "Desteklenen: " + string.Join(", ", AchReferenceTable.Keys));

            var (minAch, maxAch, tipikAch, standart) = achData;

            // Gerekli debi
            double hacimM3     = alan * yuksek;
            double gerekliM3h  = tipikAch * hacimM3;

            string durum = mevcutAch <= 0 ? "REFERANS_TABLOSU" :
                           mevcutAch >= minAch && mevcutAch <= maxAch ? "UYGUN" :
                           mevcutAch < minAch ? "ACH_YETERSIZ" : "ACH_FAZLA";

            ctx.Log($"  mep_ach_by_room_type: {odaTip} → " +
                    $"min={minAch} tipik={tipikAch} max={maxAch} ACH → {durum}");

            return new()
            {
                ["oda_tipi"]       = odaTip,
                ["min_ach"]        = minAch,
                ["tipik_ach"]      = tipikAch,
                ["max_ach"]        = maxAch,
                ["mevcut_ach"]     = mevcutAch > 0 ? mevcutAch : (object?)"—",
                ["alan_m2"]        = alan > 0 ? alan : (object?)"—",
                ["hacim_m3"]       = hacimM3 > 0 ? Math.Round(hacimM3, 1) : (object?)"—",
                ["gerekli_m3h"]    = gerekliM3h > 0 ? Math.Round(gerekliM3h, 0) : (object?)"—",
                ["standart"]       = standart,
                ["durum"]          = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 10 — mep_chiller_cop_check
        // Chiller COP ve enerji verimliliği kontrolü (ASHRAE 90.1)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_chiller_cop_check",
            RequiresTransaction = false,
            Description =
                "Chiller COP ve enerji verimliligi kontrolu (ASHRAE 90.1-2022).\n\n" +
                "params:\n" +
                "  chiller_tipi     — hava_sogutmali|su_sogutmali_vida|\n" +
                "                     su_sogutmali_turbo|vrf|isi_pompasi|absorpsiyonlu\n" +
                "  mevcut_cop       — chillerin COP degeri\n" +
                "  sogutma_kapasitesi_kw — nominal sogutma kW\n" +
                "  calisma_saati    — yillik calisma saati (default 2000)\n" +
                "  elektrik_birim_fiyat — TL/kWh (default 3.5)\n\n" +
                "Cikti: min_cop, iyi_cop, mevcut_cop, yillik_maliyet, tasarruf_pct, durum",
            Category = "MEP-Mekanik")]
        public static Dictionary<string, object?> ChillerCopCheck(OpContext ctx)
        {
            string tip       = ctx.GetString("chiller_tipi", "hava_sogutmali").ToLowerInvariant();
            double mevcutCop = ctx.GetDouble("mevcut_cop", 0);
            double kapKw     = ctx.GetDouble("sogutma_kapasitesi_kw", 0);
            double calSaat   = ctx.GetDouble("calisma_saati", 2000.0);
            double birimFiy  = ctx.GetDouble("elektrik_birim_fiyat", 3.5);

            if (mevcutCop <= 0) return ErrResult("mevcut_cop > 0 olmalidir.");

            if (!ChillerCopRef.TryGetValue(tip, out var copData))
                return ErrResult($"Bilinmeyen chiller tipi: '{tip}'. " +
                    "Desteklenen: " + string.Join(", ", ChillerCopRef.Keys));

            var (minCop, iyiCop, teknoloji) = copData;

            string copDurum = mevcutCop >= iyiCop ? "IYI" :
                              mevcutCop >= minCop  ? "UYGUN" : "COP_YETERSIZ";

            // Yıllık enerji maliyeti
            double elektrikKw = kapKw > 0 ? kapKw / mevcutCop : 0;
            double yillikMaliyet = elektrikKw * calSaat * birimFiy;

            // İyi COP ile karşılaştırma (tasarruf potansiyeli)
            double elektrikIyiKw = kapKw > 0 ? kapKw / iyiCop : 0;
            double yillikMaliyetIyi = elektrikIyiKw * calSaat * birimFiy;
            double tasarrufPct = yillikMaliyet > 0
                ? (yillikMaliyet - yillikMaliyetIyi) / yillikMaliyet * 100.0 : 0;

            ctx.Log($"  mep_chiller_cop_check: {tip} COP={mevcutCop} " +
                    $"(min={minCop} iyi={iyiCop}) → {copDurum}");

            return new()
            {
                ["chiller_tipi"]      = tip,
                ["teknoloji"]         = teknoloji,
                ["mevcut_cop"]        = mevcutCop,
                ["min_cop"]           = minCop,
                ["iyi_cop"]           = iyiCop,
                ["cop_durum"]         = copDurum,
                ["elektrik_kw"]       = kapKw > 0 ? Math.Round(elektrikKw, 1) : (object?)"—",
                ["yillik_maliyet_tl"] = yillikMaliyet > 0 ? Math.Round(yillikMaliyet, 0) : (object?)"—",
                ["tasarruf_pct"]      = Math.Round(tasarrufPct, 1),
                ["standart"]          = "ASHRAE 90.1-2022",
                ["durum"]             = copDurum == "COP_YETERSIZ" ? "UYGUN_DEGIL" : "UYGUN",
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═════════════════════════════════════════════════════════════════════

        private static Dictionary<string, object?> ZoneRow(
            long id, string isim, double tasarim, double terminal, double sapma, string durum)
            => new()
            {
                ["zon_id"]               = id.ToString(),
                ["zon_adi"]              = isim,
                ["tasarim_m3h"]          = Math.Round(tasarim, 1),
                ["toplam_terminal_m3h"]  = Math.Round(terminal, 1),
                ["sapma_pct"]            = Math.Round(sapma, 1),
                ["durum"]                = durum,
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
