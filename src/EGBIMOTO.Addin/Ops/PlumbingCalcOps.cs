// ============================================================
// EGBIMOTO — Sıhhi Tesisat Hesap Motoru 2 (PlumbingCalcOps) — v10
// Apache 2.0 — EGBIM / Ertugan Gocer
// ============================================================
// Op listesi (10 adet):
//   1. plumbing_demand_lpd         — Bina tipi × kişi → günlük LPD
//   2. plumbing_storage_tank_size  — Suction + OHT depo boyutlandırma
//   3. plumbing_pump_hp_calc       — Pompa Ph / Pb / Pm + IEC motor seçimi
//   4. plumbing_peak_demand        — Qp = birim_pik × N + riser DN
//   5. plumbing_pressure_zone      — Yüksek bina basınç zonlama + PRV konumları
//   6. plumbing_water_velocity     — Boru su hız kontrolü (0.6–3.0 m/s)
//   7. plumbing_static_pressure    — Statik basınç P = ρ × g × h
//   8. plumbing_fixture_clearance  — Armatür montaj yükseklikleri QA (FFL'den)
//   9. plumbing_dead_leg_check     — Ölü hat uzunluk kontrolü (lejyonella riski)
//  10. plumbing_hwc_return         — Sıcak su sirkülasyon (HWC) boru boyutu
//
// Standartlar:
//   EN 806-1/2/3  — Su besleme tasarımı
//   EN 12056-2    — Drenaj hesabı
//   ASHRAE 188    — Lejyonella riski / ölü hat
//   WHO Legionella Guidelines
//   TR ÇŞB 2026   — Bina su tüketim normları
//   BS 8558       — HWC sirkülasyon tasarımı
//
// ⚠️ Sorumluluk: Sonuçlar sorumlu tesisat mühendisi tarafından doğrulanmalıdır.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    public static class PlumbingCalcOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // TABLOLAR
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Bina tipi → LPD (litre/kişi/gün): min, max, tipik</summary>
        private static readonly Dictionary<string, (double min, double max, double tipik)> LpdTable =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "konut",        (135, 150, 142) },
            { "ofis",         (45,  60,  50)  },
            { "hastane",      (400, 600, 500) },
            { "otel_3yildiz", (250, 350, 300) },
            { "otel_5yildiz", (350, 500, 425) },
            { "okul",         (50,  75,  60)  },
            { "restoran",     (70,  100, 85)  },
            { "alisveris",    (15,  25,  20)  },
            { "sanayi",       (45,  65,  55)  },
            { "spor",         (50,  80,  65)  },
        };

        /// <summary>IEC standart motor dizisi (kW)</summary>
        private static readonly double[] IecMotorSeries =
        {
            0.09, 0.12, 0.18, 0.25, 0.37, 0.55, 0.75,
            1.1, 1.5, 2.2, 3.0, 4.0, 5.5, 7.5, 11, 15,
            18.5, 22, 30, 37, 45, 55, 75, 90, 110, 132,
            160, 200, 250, 315, 400, 500
        };

        /// <summary>Armatür montaj yükseklikleri (FFL'den mm): min, max, tipik</summary>
        private static readonly Dictionary<string, (int min, int max, int tipik, string aciklama)> FixtureHeights =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "dus_bas",        (1600, 2100, 2000, "Duş başlığı yüksekliği") },
            { "dus_kontrol",    (900,  1200, 1100, "Duş kontrol valfi") },
            { "wc_merkez",      (400,  460,  430,  "WC merkezi (yan duvardan)") },
            { "klozet_koltuk",  (390,  430,  410,  "Klozet oturma yüksekliği") },
            { "lavabo_tezgah",  (800,  900,  850,  "Lavabo/tezgah üstü yüksekliği") },
            { "lavabo_tavan",   (120,  150,  135,  "Lavabo-tavan taban arası") },
            { "ayna_merkez",    (1600, 1800, 1700, "Ayna merkez yüksekliği") },
            { "musluк",         (1050, 1100, 1080, "Musluk çıkış yüksekliği") },
            { "kagit_tutucu",   (650,  700,  680,  "Tuvalet kağıtlığı") },
            { "havlu_raf",      (1100, 1200, 1150, "Havluluk yüksekliği") },
            { "pencere_esigi",  (1600, 1800, 1700, "Pencere alt eşiği (banyo)") },
        };

        // ─────────────────────────────────────────────────────────────────────
        // OP 1 — plumbing_demand_lpd
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_demand_lpd",
            RequiresTransaction = false,
            Description =
                "Bina tipine ve kisi sayisina gore gunluk su talep hesabi (LPD).\n\n" +
                "params:\n" +
                "  bina_tipi    — konut|ofis|hastane|otel_3yildiz|otel_5yildiz|\n" +
                "                  okul|restoran|alisveris|sanayi|spor\n" +
                "  kisi_sayisi  — toplam kullanici sayisi\n" +
                "  lpd_override — 0=tablo, >0=kullanici girir (L/kisi/gun)\n\n" +
                "Kaynak: EN 806-1, TR CSB 2026.\n" +
                "Cikti: gunluk_talep_lt, gunluk_talep_m3, lpd_tipik",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> DemandLpd(OpContext ctx)
        {
            string binaTimpi = ctx.RequireString("bina_tipi").ToLowerInvariant().Trim();
            int kisi         = ctx.GetInt("kisi_sayisi", 0);
            double lpd_ovr   = ctx.GetDouble("lpd_override", 0);

            if (kisi <= 0)
                return ErrResult("kisi_sayisi 0'dan buyuk olmalidir.");

            double lpd_min, lpd_max, lpd_tipik;
            if (lpd_ovr > 0)
            {
                lpd_min = lpd_max = lpd_tipik = lpd_ovr;
            }
            else
            {
                if (!LpdTable.TryGetValue(binaTimpi, out var row))
                    return ErrResult($"Bilinmeyen bina tipi: '{binaTimpi}'. " +
                        "Desteklenen: " + string.Join(", ", LpdTable.Keys));
                (lpd_min, lpd_max, lpd_tipik) = row;
            }

            double talep_lt = lpd_tipik * kisi;
            double talep_m3 = talep_lt / 1000.0;

            ctx.Log($"  plumbing_demand_lpd: {binaTimpi} × {kisi} kişi × " +
                    $"{lpd_tipik} L/kişi/gün = {talep_lt:F0} L/gün");

            return new()
            {
                ["bina_tipi"]       = binaTimpi,
                ["kisi_sayisi"]     = kisi,
                ["lpd_min"]         = lpd_min,
                ["lpd_max"]         = lpd_max,
                ["lpd_tipik"]       = lpd_tipik,
                ["gunluk_talep_lt"] = Math.Round(talep_lt, 1),
                ["gunluk_talep_m3"] = Math.Round(talep_m3, 3),
                ["durum"]           = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2 — plumbing_storage_tank_size
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_storage_tank_size",
            RequiresTransaction = false,
            Description =
                "Gunluk talepten suction (yeralti) ve OHT (cati) depo hacmini hesaplar.\n\n" +
                "params:\n" +
                "  gunluk_talep_m3 — m3/gun\n" +
                "  tampon_katsayi  — 1.1-1.2 (default 1.15)\n" +
                "  suction_oran    — 0.5-0.6 (default 0.55)\n\n" +
                "Kural: Toplam = talep x tampon. OHT efektif derinlik 1.0-1.5m.\n" +
                "Cikti: toplam_depo_m3, suction_tank_m3, oht_tank_m3",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> StorageTankSize(OpContext ctx)
        {
            double talep    = ctx.GetDouble("gunluk_talep_m3", 0);
            double tampon   = Math.Clamp(ctx.GetDouble("tampon_katsayi", 1.15), 1.0, 1.5);
            double suctOran = Math.Clamp(ctx.GetDouble("suction_oran", 0.55), 0.4, 0.7);

            if (talep <= 0)
                return ErrResult("gunluk_talep_m3 > 0 olmalidir. Once plumbing_demand_lpd calistirin.");

            double toplam      = talep * tampon;
            double suction     = toplam * suctOran;
            double oht         = toplam * (1.0 - suctOran);
            double ohtDerinlik = 1.25;
            double ohtAlan     = oht / ohtDerinlik;

            ctx.Log($"  plumbing_storage_tank_size: Toplam={toplam:F2}m³ " +
                    $"Suction={suction:F2}m³ OHT={oht:F2}m³");

            return new()
            {
                ["gunluk_talep_m3"]        = Math.Round(talep, 3),
                ["tampon_katsayi"]         = tampon,
                ["toplam_depo_m3"]         = Math.Round(toplam, 2),
                ["suction_tank_m3"]        = Math.Round(suction, 2),
                ["oht_tank_m3"]            = Math.Round(oht, 2),
                ["oht_efektif_derinlik_m"] = ohtDerinlik,
                ["oht_taban_alani_m2"]     = Math.Round(ohtAlan, 2),
                ["suction_not"]            = "Max su seviyesi pompa emis hattindan min 300mm asagi olmali.",
                ["durum"]                  = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 3 — plumbing_pump_hp_calc
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_pump_hp_calc",
            RequiresTransaction = false,
            Description =
                "Pompa hidrolik/fren/motor guc hesabi + IEC standart motor secimi.\n\n" +
                "params:\n" +
                "  debi_m3_s     — hacimsel debi m3/s\n" +
                "  toplam_yuk_m  — toplam manometrik yuk metre\n" +
                "  pompa_verim   — 0.60-0.80 (default 0.70)\n" +
                "  motor_verim   — 0.85-0.95 (default 0.90)\n" +
                "  servis_faktor — 1.15-1.25 (default 1.15)\n\n" +
                "Formul: Ph=rho*g*Q*H/1000, Pb=Ph/np, Pm=Pb/nm*SF.\n" +
                "Cikti: ph_kw, pb_kw, pm_kw, standart_motor_kw",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> PumpHpCalc(OpContext ctx)
        {
            double Q  = ctx.GetDouble("debi_m3_s", 0);
            double H  = ctx.GetDouble("toplam_yuk_m", 0);
            double np = Math.Clamp(ctx.GetDouble("pompa_verim", 0.70), 0.40, 0.90);
            double nm = Math.Clamp(ctx.GetDouble("motor_verim", 0.90), 0.70, 0.99);
            double sf = Math.Clamp(ctx.GetDouble("servis_faktor", 1.15), 1.0, 1.5);

            if (Q <= 0) return ErrResult("debi_m3_s > 0 olmalidir.");
            if (H <= 0) return ErrResult("toplam_yuk_m > 0 olmalidir.");

            double ph  = 1000.0 * 9.81 * Q * H / 1000.0;
            double pb  = ph / np;
            double pm  = pb / nm * sf;
            double std = SelectStandardMotor(pm);

            ctx.Log($"  plumbing_pump_hp_calc: Ph={ph:F3}kW Pb={pb:F3}kW " +
                    $"Pm={pm:F3}kW → Std={std}kW");

            return new()
            {
                ["debi_m3_s"]         = Q,
                ["toplam_yuk_m"]      = H,
                ["pompa_verim"]       = np,
                ["motor_verim"]       = nm,
                ["servis_faktor"]     = sf,
                ["ph_kw_hidrolik"]    = Math.Round(ph, 3),
                ["pb_kw_fren"]        = Math.Round(pb, 3),
                ["pm_kw_motor"]       = Math.Round(pm, 3),
                ["standart_motor_kw"] = std,
                ["durum"]             = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 4 — plumbing_peak_demand
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_peak_demand",
            RequiresTransaction = false,
            Description =
                "Konut/birim bazli pik talep hesabi (Qp = birim_pik x N).\n\n" +
                "params:\n" +
                "  daire_sayisi    — toplam daire/birim sayisi\n" +
                "  birim_pik_l_dak — birim pik debisi L/dak (default 8, konut)\n" +
                "  hesap_turu      — konut|ofis|karma\n\n" +
                "Cikti: qp_l_dak, qp_l_s, riser_dn_mm",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> PeakDemand(OpContext ctx)
        {
            int    N        = ctx.GetInt("daire_sayisi", 0);
            double birimPik = ctx.GetDouble("birim_pik_l_dak", -1);
            string tur      = ctx.GetString("hesap_turu", "konut").ToLowerInvariant();

            if (N <= 0) return ErrResult("daire_sayisi > 0 olmalidir.");

            if (birimPik < 0)
                birimPik = tur switch { "ofis" => 5.0, "karma" => 6.5, _ => 8.0 };

            double qp_l_dak = birimPik * N;
            double qp_l_s   = qp_l_dak / 60.0;
            double qp_m3_s  = qp_l_s / 1000.0;
            int    riserDn  = SelectRiserDn(qp_m3_s);

            ctx.Log($"  plumbing_peak_demand: {N} birim × {birimPik} L/dak " +
                    $"= Qp={qp_l_dak:F1} L/dak → Riser DN{riserDn}");

            return new()
            {
                ["daire_sayisi"]    = N,
                ["birim_pik_l_dak"] = birimPik,
                ["hesap_turu"]      = tur,
                ["qp_l_dak"]        = Math.Round(qp_l_dak, 1),
                ["qp_l_s"]          = Math.Round(qp_l_s, 3),
                ["qp_m3_s"]         = Math.Round(qp_m3_s, 5),
                ["riser_dn_mm"]     = riserDn,
                ["durum"]           = riserDn > 0 ? "OK" : "KAPASITE_ASIMI",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 5 — plumbing_pressure_zone
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_pressure_zone",
            RequiresTransaction = false,
            Description =
                "Yuksek bina basinc zonlarini, PRV konumlarini ve statik basinci hesaplar.\n\n" +
                "params:\n" +
                "  kat_sayisi       — toplam kat sayisi\n" +
                "  kat_yuksekligi_m — kat yuksekligi m (default 3.0)\n" +
                "  max_basinc_bar   — zon max basinci bar (default 3.5)\n" +
                "  min_basinc_bar   — min servis basinci bar (default 1.0)\n\n" +
                "Kural: max 3.5 bar/zon, her zon max 10-11 kat. PRV zon girisine.\n" +
                "Cikti: zon_sayisi, zonlar listesi (kat aralik + PRV + basinc)",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> PressureZone(OpContext ctx)
        {
            int    kat      = ctx.GetInt("kat_sayisi", 0);
            double katYuk   = Math.Clamp(ctx.GetDouble("kat_yuksekligi_m", 3.0), 2.5, 6.0);
            double maxBar   = Math.Clamp(ctx.GetDouble("max_basinc_bar", 3.5), 2.0, 5.0);

            if (kat <= 0) return ErrResult("kat_sayisi > 0 olmalidir.");

            double maxZonYuk  = maxBar * 100_000.0 / (1000.0 * 9.81);
            int    maxKatPerZ = Math.Max(1, (int)Math.Floor(maxZonYuk / katYuk));
            int    zonSayisi  = (int)Math.Ceiling((double)kat / maxKatPerZ);

            var zonlar = new List<Dictionary<string, object?>>();
            for (int z = 0; z < zonSayisi; z++)
            {
                int altKat = z * maxKatPerZ + 1;
                int ustKat = Math.Min((z + 1) * maxKatPerZ, kat);

                double altBar = Math.Round(1000.0 * 9.81 * (kat - altKat + 1) * katYuk / 100_000.0, 2);
                double ustBar = Math.Round(1000.0 * 9.81 * (kat - ustKat + 1) * katYuk / 100_000.0, 2);

                zonlar.Add(new()
                {
                    ["zon_no"]         = z + 1,
                    ["alt_kat"]        = altKat,
                    ["ust_kat"]        = ustKat,
                    ["prv_kat"]        = altKat,
                    ["alt_basinc_bar"] = altBar,
                    ["ust_basinc_bar"] = ustBar,
                    ["zon_durum"]      = altBar <= maxBar ? "UYGUN" : "PRV_GEREKLI",
                });
            }

            double toplamBar = Math.Round(1000.0 * 9.81 * kat * katYuk / 100_000.0, 2);
            string durum     = zonSayisi > 1 ? "COK_ZONLU_PRV_GEREKLI" :
                               toplamBar > maxBar ? "PRV_GEREKLI" : "TEK_ZON_YETERLI";

            ctx.Log($"  plumbing_pressure_zone: {kat} kat → {zonSayisi} zon, " +
                    $"toplam statik {toplamBar} bar");

            return new()
            {
                ["kat_sayisi"]        = kat,
                ["kat_yuksekligi_m"]  = katYuk,
                ["zon_sayisi"]        = zonSayisi,
                ["max_kat_per_zon"]   = maxKatPerZ,
                ["toplam_statik_bar"] = toplamBar,
                ["zonlar"]            = zonlar,
                ["durum"]             = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 6 — plumbing_water_velocity
        // Su hız kontrolü: EN 806-3 limitleri
        //
        // Sınırlar (EN 806-3):
        //   Min: 0.6 m/s (stagnaston önlemek için)
        //   Max soğuk su: 2.0 m/s (gürültü/erozyon)
        //   Max sıcak su: 1.5 m/s (termal genleşme etkisi)
        //   Max atık/pis su: 3.0 m/s
        //   Optimum: 1.0–2.0 m/s
        //
        // params:
        //   debi_l_s   — akış debisi L/s
        //   ic_cap_mm  — iç çap mm
        //   boru_tipi  — soguk|sicak|atik (default soguk)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_water_velocity",
            RequiresTransaction = false,
            Description =
                "Boru ic capina gore su akis hizini hesaplar ve EN 806-3 limitlerini kontrol eder.\n\n" +
                "params:\n" +
                "  debi_l_s  — akis debisi L/s\n" +
                "  ic_cap_mm — boru ic capi mm\n" +
                "  boru_tipi — soguk|sicak|atik (default soguk)\n\n" +
                "Sinirlar: soguk max 2.0 m/s, sicak max 1.5 m/s, atik max 3.0 m/s, min 0.6 m/s.\n" +
                "Cikti: hiz_m_s, alan_mm2, durum, oneri_cap_mm",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> WaterVelocity(OpContext ctx)
        {
            double debi_ls  = ctx.GetDouble("debi_l_s", 0);
            double ic_cap   = ctx.GetDouble("ic_cap_mm", 0);
            string tip      = ctx.GetString("boru_tipi", "soguk").ToLowerInvariant();

            if (debi_ls <= 0) return ErrResult("debi_l_s > 0 olmalidir.");
            if (ic_cap  <= 0) return ErrResult("ic_cap_mm > 0 olmalidir.");

            double debi_m3s = debi_ls / 1000.0;
            double r_m      = (ic_cap / 2.0) / 1000.0;
            double alan_m2  = Math.PI * r_m * r_m;
            double hiz      = debi_m3s / alan_m2;

            // Limit tablosu
            double vMin = 0.6;
            double vMax = tip switch { "sicak" => 1.5, "atik" => 3.0, _ => 2.0 };
            double vOpt_max = tip == "atik" ? 2.5 : 2.0;

            string durum;
            if (hiz < vMin)        durum = "HIZ_DUSUK";
            else if (hiz > vMax)   durum = "HIZ_YUKSEK";
            else                   durum = "UYGUN";

            // Optimum hız için önerilen çap (v = 1.5 m/s hedef)
            double vHedef   = tip == "atik" ? 1.5 : 1.2;
            double alanHedef = debi_m3s / vHedef;
            double capHedef  = 2.0 * Math.Sqrt(alanHedef / Math.PI) * 1000.0;
            int    oneriCap  = SelectNominalDn((int)Math.Ceiling(capHedef));

            ctx.Log($"  plumbing_water_velocity: DN{ic_cap:F0}mm Q={debi_ls}L/s " +
                    $"v={hiz:F2}m/s ({tip}) → {durum}");

            return new()
            {
                ["debi_l_s"]       = debi_ls,
                ["ic_cap_mm"]      = ic_cap,
                ["boru_tipi"]      = tip,
                ["alan_mm2"]       = Math.Round(alan_m2 * 1e6, 1),
                ["hiz_m_s"]        = Math.Round(hiz, 3),
                ["v_min_m_s"]      = vMin,
                ["v_max_m_s"]      = vMax,
                ["oneri_cap_mm"]   = oneriCap,
                ["durum"]          = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 7 — plumbing_static_pressure
        // Statik basınç hesabı: P = ρ × g × h
        //
        // Kullanım alanları:
        //   - OHT'den beslemede zemin kat basıncı
        //   - Pompa çıkış basıncı doğrulama
        //   - PRV giriş basıncı belirleme
        //   - Bodrum kat maksimum basınç kontrolü
        //
        // params:
        //   yukseklik_m     — su kolonunun yüksekliği (m)
        //   sicaklik_c      — su sıcaklığı °C (yoğunluk için, default 20°C)
        //   giris_basinc_bar— pompa çıkışı / hat girişi basıncı (default 0)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_static_pressure",
            RequiresTransaction = false,
            Description =
                "Statik basinc hesabi: P = rho * g * h. OHT beslemesi, PRV giriş basinci,\n" +
                "bodrum kat max basinc kontrolu icin kullanilir.\n\n" +
                "params:\n" +
                "  yukseklik_m      — su kolonu yuksekligi metre\n" +
                "  sicaklik_c       — su sicakligi C (default 20)\n" +
                "  giris_basinc_bar — hat giris basinci bar (default 0)\n\n" +
                "Cikti: statik_basinc_pa, statik_basinc_bar, toplam_basinc_bar, durum",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> StaticPressure(OpContext ctx)
        {
            double h     = ctx.GetDouble("yukseklik_m", 0);
            double T     = ctx.GetDouble("sicaklik_c", 20.0);
            double P_gir = ctx.GetDouble("giris_basinc_bar", 0);

            if (h == 0) return ErrResult("yukseklik_m sifir olamaz.");

            // Su yoğunluğu — sıcaklığa göre yaklaşık (0-100°C arası)
            double rho = WaterDensity(T);
            const double g = 9.81;

            double P_statik_pa  = rho * g * Math.Abs(h);
            double P_statik_bar = P_statik_pa / 100_000.0;
            double P_toplam_bar = P_gir + P_statik_bar;

            // Max servis basıncı kontrolü (EN 806-2: 500 kPa = 5.0 bar)
            string durum = P_toplam_bar > 5.0 ? "BASINC_ASIMI_PRV_GEREKLI" :
                           P_toplam_bar > 3.5 ? "YUKSEK_BASINC_PRV_ONERILI" : "UYGUN";

            ctx.Log($"  plumbing_static_pressure: h={h}m T={T}°C ρ={rho:F1}kg/m³ " +
                    $"P={P_toplam_bar:F3}bar → {durum}");

            return new()
            {
                ["yukseklik_m"]       = h,
                ["sicaklik_c"]        = T,
                ["su_yogunlugu"]      = Math.Round(rho, 2),
                ["giris_basinc_bar"]  = P_gir,
                ["statik_basinc_pa"]  = Math.Round(P_statik_pa, 1),
                ["statik_basinc_bar"] = Math.Round(P_statik_bar, 4),
                ["toplam_basinc_bar"] = Math.Round(P_toplam_bar, 4),
                ["toplam_basinc_kpa"] = Math.Round(P_toplam_bar * 100, 2),
                ["durum"]             = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 8 — plumbing_fixture_clearance
        // Armatür montaj yükseklikleri QA (FFL'den mm)
        //
        // Revit modelindeki PlumbingFixture elemanlarının EG_MontajYuksekligi
        // parametresini okuyup referans tabloya göre doğrular.
        //
        // params:
        //   tolerance_mm — kabul toleransı mm (default 50)
        //   write_back   — önerilen değeri parametreye yaz (default false)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_fixture_clearance",
            RequiresTransaction = true,
            Description =
                "Revit modelindeki sihhi armaturlerin montaj yuksekliklerini (FFL'den mm)\n" +
                "referans tabloya gore dogrular. EG_MontajYuksekligi parametresi okunur.\n\n" +
                "params:\n" +
                "  tolerance_mm — kabul toleransi mm (default 50)\n" +
                "  write_back   — oneri degeri EG_MontajYuksekligi_Oneri'ye yaz (default false)\n\n" +
                "Referans: IMPORTANT INTERIOR DETAILS standart olculeri.\n" +
                "Cikti: fixture_id, armatur_tipi, mevcut_mm, oneri_mm, fark_mm, durum",
            Category = "MEP-Sıhhi")]
        public static List<Dictionary<string, object?>> FixtureClearance(OpContext ctx)
        {
            var rctx  = RequireRevit(ctx);
            int tolMm = ctx.GetInt("tolerance_mm", 50);
            bool wb   = ctx.GetBool("write_back", false);

            var fixtures = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                .WhereElementIsNotElementType()
                .ToList();

            if (fixtures.Count == 0)
                return ErrRows("Modelde PlumbingFixture bulunamadi.");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(rctx.Doc, "Armatur Yukseklik QA", rctx.IsAtomicMode);

            foreach (var fx in fixtures)
            {
                long fid = Rv.GetId(fx.Id);

                // Armatür tipini normalize et
                string tip = NormalizeFixtureTip(
                    fx.LookupParameter("EG_ArmaturTipi")?.AsString(),
                    fx.Name,
                    (rctx.Doc.GetElement(fx.GetTypeId()) as ElementType)?.Name);

                // Montaj yüksekliğini oku (mm)
                double mevcut_mm = fx.LookupParameter("EG_MontajYuksekligi")?.AsDouble() * 304.8 ?? -1;

                if (!FixtureHeights.TryGetValue(tip, out var ref_row))
                {
                    rows.Add(MakeRow(fid, tip, mevcut_mm, -1, 0, "TIP_TANIMSIZ", ""));
                    continue;
                }

                var (minMm, maxMm, tipikMm, aciklama) = ref_row;
                double fark = mevcut_mm >= 0 ? mevcut_mm - tipikMm : 0;

                string durum;
                if (mevcut_mm < 0)              durum = "PARAMETRE_YOK";
                else if (mevcut_mm < minMm - tolMm) durum = "DUSUK";
                else if (mevcut_mm > maxMm + tolMm) durum = "YUKSEK";
                else                            durum = "UYGUN";

                if (wb && durum != "UYGUN")
                    SetD(fx, "EG_MontajYuksekligi_Oneri", tipikMm / 304.8);

                rows.Add(MakeRow(fid, tip, mevcut_mm, tipikMm, fark, durum, aciklama));
            }

            scope.Commit();
            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  plumbing_fixture_clearance: {ok}/{rows.Count} armatur UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 9 — plumbing_dead_leg_check
        // Ölü hat (dead leg) uzunluk kontrolü — Lejyonella riski
        //
        // ASHRAE 188 ve WHO Legionella kılavuzu:
        //   Ölü hat uzunluğu ≤ boru hacmi / (boru çapı × sabit)
        //   Pratik kural: DN'ye göre max uzunluk
        //     DN15 (1/2"): ≤ 300mm
        //     DN20 (3/4"): ≤ 450mm
        //     DN25 (1"):   ≤ 600mm
        //     DN32+:       ≤ 900mm
        //   Sıcak su ≥ 60°C veya soğuk su ≤ 20°C olmazsa risk artar
        //
        // params:
        //   (input: List<Element> pipe segmentleri veya boş → tüm borular)
        //   max_uzunluk_katsayi — DN'ye çarpan (default 20 × DN_m)
        //   sicak_su_sicaklik_c — min sıcak su sıcaklığı °C (default 60)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_dead_leg_check",
            RequiresTransaction = false,
            Description =
                "Sihhi tesisat borularinda olu hat (dead leg) uzunlugunu kontrol eder.\n" +
                "Lejyonella riski: ASHRAE 188 / WHO Legionella Guidelines.\n\n" +
                "params:\n" +
                "  max_uzunluk_katsayi — max uzunluk = katsayi x DN_m (default 20)\n" +
                "  sicak_su_sicaklik_c — min sicak su sicakligi C (default 60)\n\n" +
                "Input: List<Element> (Pipe) veya bos (tum pissu/su borulari).\n" +
                "Kural: DN15->300mm, DN20->450mm, DN25->600mm, DN32+->900mm.\n" +
                "Cikti: pipe_id, sistem, cap_mm, uzunluk_mm, max_mm, durum",
            Category = "MEP-Sıhhi")]
        public static List<Dictionary<string, object?>> DeadLegCheck(OpContext ctx)
        {
            var rctx     = RequireRevit(ctx);
            double katsayi = ctx.GetDouble("max_uzunluk_katsayi", 20.0);

            // DN → max ölü hat uzunluğu (mm) — ASHRAE 188 pratik değerleri
            var maxUzunlukTable = new Dictionary<int, int>
            {
                { 15,  300 }, { 20, 450 }, { 25, 600 },
                { 32,  900 }, { 40, 900 }, { 50, 900 },
            };

            var pipes = ctx.InputAsOrDefault<List<Element>>(new())
                .OfType<Autodesk.Revit.DB.Plumbing.Pipe>().ToList();

            if (pipes.Count == 0)
                pipes = new FilteredElementCollector(rctx.Doc)
                    .OfCategory(BuiltInCategory.OST_PipeCurves)
                    .WhereElementIsNotElementType()
                    .OfType<Autodesk.Revit.DB.Plumbing.Pipe>()
                    .ToList();

            if (pipes.Count == 0)
                return ErrRows("Modelde boru bulunamadi.");

            var rows = new List<Dictionary<string, object?>>();
            foreach (var pipe in pipes)
            {
                long pid = Rv.GetId(pipe.Id);

                // Boru çapı (iç çap, feet → mm)
                double capFt = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_INNER_DIAM_PARAM)?.AsDouble() ?? 0;
                int    capMm = (int)Math.Round(capFt * 304.8);

                // Boru uzunluğu (feet → mm)
                double uzFt  = pipe.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0;
                double uzMm  = uzFt * 304.8;

                string sistem = pipe.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? "";

                // Max ölü hat: tabloda yoksa DN × katsayi
                int maxMm = maxUzunlukTable.TryGetValue(capMm, out var tbl)
                    ? tbl
                    : (int)(capMm * katsayi);

                // Kısa boru = ölü hat adayı (her 2 uçta bağlantı var mı bilinmez,
                // bu kontrol uzunluk bazlı — tam topoloji analizi ayrı op gerektirir)
                string durum = uzMm <= maxMm ? "UYGUN" :
                               uzMm <= maxMm * 1.5 ? "DIKKAT" : "OLU_HAT_RISKI";

                rows.Add(new()
                {
                    ["pipe_id"]   = pid.ToString(),
                    ["sistem"]    = sistem,
                    ["cap_mm"]    = capMm,
                    ["uzunluk_mm"]= Math.Round(uzMm, 1),
                    ["max_mm"]    = maxMm,
                    ["oran"]      = maxMm > 0 ? Math.Round(uzMm / maxMm, 2) : 0,
                    ["durum"]     = durum,
                });
            }

            int risk = rows.Count(r => (string?)r["durum"] == "OLU_HAT_RISKI");
            int dikkat = rows.Count(r => (string?)r["durum"] == "DIKKAT");
            ctx.Log($"  plumbing_dead_leg_check: {rows.Count} boru, " +
                    $"{risk} risk, {dikkat} dikkat");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 10 — plumbing_hwc_return
        // Sıcak su sirkülasyon (HWC) boru boyutu ve dönüş hattı kontrolü
        //
        // Kural (BS 8558 / EN 806):
        //   Sirkülasyon debisi: Qcirc = Isı kaybı / (ρ × Cp × ΔT)
        //   ΔT = 5–10°C (besleme-dönüş sıcaklık farkı)
        //   Min sirkülasyon hızı: 0.2 m/s (stagnasyon önlemek için)
        //   Max dönüş hattı sıcaklığı: ≥ 50°C (lejyonella önleme)
        //   Tüm kullanım noktasına mesafe ≤ 50mm açıldığında 10 sn içinde sıcak su
        //
        // params:
        //   isi_kaybi_w         — toplam boru ısı kaybı W
        //   besleme_sicaklik_c  — besleme sıcaklığı °C (default 65)
        //   donus_sicaklik_c    — dönüş sıcaklığı °C (default 55)
        //   ic_cap_mm           — dönüş borusu iç çapı mm (doğrulama için)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("plumbing_hwc_return",
            RequiresTransaction = false,
            Description =
                "Sicak su sirkulasyon (HWC) debi ve donus hatti boru boyutu hesabi.\n\n" +
                "params:\n" +
                "  isi_kaybi_w        — toplam boru isi kaybi W\n" +
                "  besleme_sicaklik_c — besleme sicakligi C (default 65)\n" +
                "  donus_sicaklik_c   — donus sicakligi C (default 55)\n" +
                "  ic_cap_mm          — donus borusu ic capi mm (dogrulama)\n\n" +
                "Kural: Qcirc=Q/(rho*Cp*DT). Min donus >=50C (lejyonella).\n" +
                "Cikti: qcirc_l_s, qcirc_l_h, hiz_m_s, oneri_cap_mm, durum",
            Category = "MEP-Sıhhi")]
        public static Dictionary<string, object?> HwcReturn(OpContext ctx)
        {
            double Q_w    = ctx.GetDouble("isi_kaybi_w", 0);
            double T_bes  = ctx.GetDouble("besleme_sicaklik_c", 65.0);
            double T_don  = ctx.GetDouble("donus_sicaklik_c", 55.0);
            double capMm  = ctx.GetDouble("ic_cap_mm", 0);

            if (Q_w <= 0)  return ErrResult("isi_kaybi_w > 0 olmalidir.");
            if (T_bes <= T_don) return ErrResult("besleme_sicaklik_c > donus_sicaklik_c olmalidir.");

            double dT     = T_bes - T_don;
            double Cp     = 4186.0;            // J/kg°C (su)
            double rho    = WaterDensity((T_bes + T_don) / 2.0);

            // Sirkülasyon debisi (m³/s)
            double Qcirc_m3s = Q_w / (rho * Cp * dT);
            double Qcirc_ls  = Qcirc_m3s * 1000.0;
            double Qcirc_lh  = Qcirc_ls * 3600.0;

            // Hız kontrolü (mevcut çap varsa)
            double hiz = 0;
            string hizDurum = "CAP_GIRILMEDI";
            if (capMm > 0)
            {
                double r    = (capMm / 2.0) / 1000.0;
                double alan = Math.PI * r * r;
                hiz         = Qcirc_m3s / alan;
                hizDurum    = hiz < 0.2 ? "HIZ_DUSUK" :
                              hiz > 1.5 ? "HIZ_YUKSEK" : "UYGUN";
            }

            // Önerilen çap (v_hedef = 0.5 m/s)
            double alanHed = Qcirc_m3s / 0.5;
            double capHed  = 2.0 * Math.Sqrt(alanHed / Math.PI) * 1000.0;
            int oneriCap   = SelectNominalDn((int)Math.Ceiling(capHed));

            // Lejyonella kontrolü
            string lejDurum = T_don >= 50.0 ? "LEJYONELLA_GUVENLI" : "LEJYONELLA_RISKI";

            string genelDurum = (hizDurum == "UYGUN" || hizDurum == "CAP_GIRILMEDI")
                                && lejDurum == "LEJYONELLA_GUVENLI" ? "OK" : "KONTROL_GEREKLI";

            ctx.Log($"  plumbing_hwc_return: Q_w={Q_w}W dT={dT}°C " +
                    $"Qcirc={Qcirc_ls:F3}L/s DN{oneriCap} → {genelDurum}");

            return new()
            {
                ["isi_kaybi_w"]         = Q_w,
                ["besleme_sicaklik_c"]  = T_bes,
                ["donus_sicaklik_c"]    = T_don,
                ["delta_t_c"]           = dT,
                ["qcirc_l_s"]           = Math.Round(Qcirc_ls, 4),
                ["qcirc_l_h"]           = Math.Round(Qcirc_lh, 2),
                ["qcirc_m3_s"]          = Math.Round(Qcirc_m3s, 6),
                ["hiz_m_s"]             = capMm > 0 ? Math.Round(hiz, 3) : (object?)"—",
                ["hiz_durum"]           = hizDurum,
                ["oneri_cap_mm"]        = oneriCap,
                ["lejyonella_durum"]    = lejDurum,
                ["durum"]               = genelDurum,
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ORTAK YARDIMCILAR
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Su yoğunluğu kg/m³ (0-100°C, yaklaşık)</summary>
        private static double WaterDensity(double T_c)
        {
            // Polinom fit: ρ(T) ≈ 999.83 - 0.0615T - 0.00367T² (0-80°C arası ±0.1%)
            T_c = Math.Clamp(T_c, 0, 100);
            return 999.83 - 0.0615 * T_c - 0.00367 * T_c * T_c;
        }

        /// <summary>IEC standart motoru seç (Pm'den büyük ilk değer)</summary>
        private static double SelectStandardMotor(double pm)
        {
            foreach (var m in IecMotorSeries)
                if (m >= pm) return m;
            return Math.Ceiling(pm / 50.0) * 50;
        }

        /// <summary>Hazen-Williams C=150, v≤2.0 m/s ile riser DN seçimi</summary>
        private static int SelectRiserDn(double qm3s)
        {
            int[] dns = { 25, 32, 40, 50, 65, 80, 100, 125, 150, 200, 250, 300 };
            foreach (int dn in dns)
            {
                double d    = dn / 1000.0;
                double area = Math.PI * (d / 2) * (d / 2);
                if (area * 2.0 >= qm3s) return dn;
            }
            return 0;
        }

        /// <summary>İç çap mm'den nominal DN seçimi (standart seriden)</summary>
        private static int SelectNominalDn(int icCapMm)
        {
            int[] dn = { 15, 20, 25, 32, 40, 50, 65, 80, 100, 125, 150, 200, 250 };
            foreach (int d in dn)
                if (d >= icCapMm) return d;
            return icCapMm;
        }

        private static string NormalizeFixtureTip(string? egTip, string? name, string? typeName)
        {
            string src = (!string.IsNullOrWhiteSpace(egTip) ? egTip
                        : (name ?? "") + " " + (typeName ?? "")).ToLowerInvariant();

            if (src.Contains("dus") || src.Contains("shower"))  return "dus_bas";
            if (src.Contains("klozet") || src.Contains("wc") || src.Contains("toilet")) return "klozet_koltuk";
            if (src.Contains("lavabo") || src.Contains("basin")) return "lavabo_tezgah";
            if (src.Contains("musluk") || src.Contains("tap"))   return "musluk";
            if (src.Contains("ayna") || src.Contains("mirror"))  return "ayna_merkez";
            if (src.Contains("kagit") || src.Contains("paper"))  return "kagit_tutucu";
            if (src.Contains("havlu") || src.Contains("towel"))  return "havlu_raf";
            return "";
        }

        private static void SetD(Element e, string name, double v)
        {
            var p = e.LookupParameter(name);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) p.Set(v);
        }

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit baglami gerektirir.");

        private static Dictionary<string, object?> ErrResult(string msg)
            => new() { ["durum"] = "HATA", ["mesaj"] = msg };

        private static List<Dictionary<string, object?>> ErrRows(string msg)
            => new() { new() { ["durum"] = "HATA", ["mesaj"] = msg } };

        private static Dictionary<string, object?> MakeRow(
            long fid, string tip, double mevcut, double oneri, double fark, string durum, string aciklama)
            => new()
            {
                ["fixture_id"]  = fid.ToString(),
                ["armatur_tipi"]= tip,
                ["mevcut_mm"]   = mevcut >= 0 ? Math.Round(mevcut, 0) : (object?)"—",
                ["oneri_mm"]    = oneri >= 0 ? oneri : (object?)"—",
                ["fark_mm"]     = Math.Round(fark, 0),
                ["aciklama"]    = aciklama,
                ["durum"]       = durum,
            };
    }
}
