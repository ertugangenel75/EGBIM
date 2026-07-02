// ============================================================
// EGBIMOTO — Yangın Koruma Hesap Motoru (FireProtectionOps) — v10.3
// Apache 2.0 — EGBIM / Ertugan Gocer
// ============================================================
// Op listesi (13 adet):
//   1.  fp_standpipe_qa            — Standpipe riser DN + Wet/Dry tip kontrolü
//   2.  fp_standpipe_pressure      — Standpipe basınç + PRV hesabı
//   3.  fp_fdc_clearance_check     — FDC konumu ve temizlik mesafesi
//   4.  fp_pump_schedule_validate  — Yangın pompası schedule (Main/Jockey/Diesel)
//   5.  fp_pump_hp_calc            — Yangın pompası HP hesabı
//   6.  fp_sprinkler_hydraulic     — Sprinkler hidrolik: K-faktörü, basınç, debi
//   7.  fp_sprinkler_temp_class    — Sprinkler sıcaklık sınıfı doğrulama
//   8.  fp_detection_coverage      — Dedektör kapsama alanı (NFPA 72 / TR)
//   9.  fp_suppression_agent_qa    — Söndürme ajanı tipi QA
//  10.  fp_evacuation_route_check  — Tahliye yolu genişliği + max mesafe
//  11.  fp_exit_sign_spacing       — Acil çıkış işareti yerleşimi
//  12.  fp_fire_door_rating_check  — Yangın kapısı fire rating + boşluk
//  13.  fp_compartment_area_check  — Yangın kompartıman alanı kontrolü
//
// Standartlar:
//   TR Yangın Yönetmeliği 2015 (Binaların Yangından Korunması)
//   NFPA 13 — Sprinkler sistemleri
//   NFPA 14 — Standpipe ve hortum sistemleri
//   NFPA 72 — Yangın alarm sistemleri
//   TS EN 1838 — Acil aydınlatma / çıkış işaretleri
//   TS EN 12845 — Sabit yangın söndürme sistemleri
//   IFC (International Fire Code)
//
// ⚠️ Sorumluluk: Sonuçlar sorumlu yangın mühendisi tarafından doğrulanmalıdır.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    public static partial class FireProtectionOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // TABLOLAR
        // ─────────────────────────────────────────────────────────────────────

        // Sprinkler K-faktörü → min debi (L/dak) @ min basınç (bar)
        // NFPA 13 / TS EN 12845
        private static readonly Dictionary<string, (double k, double minBar, double minLpm)> KFactorTable =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "K57",  (57,  0.5,  40.3)  },
            { "K80",  (80,  0.5,  56.6)  },
            { "K115", (115, 0.5,  81.3)  },
            { "K160", (160, 0.5,  113.1) },
            { "K202", (202, 0.34, 117.7) },
            { "K242", (242, 0.34, 141.1) },
            { "K320", (320, 0.34, 186.5) },
            { "K363", (363, 0.34, 211.6) },
        };

        // Sprinkler sıcaklık sınıfları (TS EN 12845 / NFPA 13)
        private static readonly Dictionary<string, (int minC, int maxC, string renk)> TempClasses =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "57C",  (0,  38,  "Turuncu/Kırmızı") },
            { "68C",  (0,  49,  "Kırmızı")         },
            { "79C",  (0,  59,  "Sarı")             },
            { "93C",  (0,  74,  "Yeşil")            },
            { "141C", (0,  112, "Mavi")             },
            { "182C", (0,  149, "Mor")              },
            { "204C", (0,  167, "Siyah")            },
            { "260C", (0,  218, "Siyah")            },
        };

        // Standart standpipe boyutları (NFPA 14)
        private static readonly (double maxYukseklikM, int dnMm)[] StandpipeDnTable =
        {
            (30.0, 100),  // <30m → DN100
            (60.0, 125),  // 30-60m → DN125
            (double.MaxValue, 150) // >60m → DN150
        };

        // Yangın kompartıman max alanları — TR Yangın Yönetmeliği Tablo-1
        private static readonly Dictionary<string, (double maxAlanM2, int maxKatSayisi)> KompartımanLimits =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "konut",        (2000, 8)  },
            { "ofis",         (2500, 8)  },
            { "ticaret",      (2000, 4)  },
            { "hastane",      (2000, 2)  },
            { "depo",         (1000, 2)  },
            { "sanayi_dusuk", (2500, 4)  },
            { "sanayi_orta",  (1500, 2)  },
            { "sanayi_yuksek",(500,  1)  },
            { "toplanma",     (2000, 4)  },
            { "yatakhane",    (1000, 4)  },
        };

        // Tahliye yolu min genişliği (TR Yangın Yönetmeliği §49)
        private static readonly Dictionary<string, double> TahliyeMinGenislik = new(StringComparer.OrdinalIgnoreCase)
        {
            { "koridor",      1.20 }, // m
            { "merdiven",     1.20 },
            { "kapi",         0.90 },
            { "rampa",        1.20 },
            { "toplanma_alani", 1.80 },
        };

        // ─────────────────────────────────────────────────────────────────────
        // OP 1 — fp_standpipe_qa
        // Standpipe riser DN ve tip kontrolü (NFPA 14 / TR Yangın Yönetmeliği)
        //
        // Kural:
        //   Bina yüksekliği < 30m → DN100 min
        //   30m ≤ h < 60m         → DN125 min
        //   h ≥ 60m               → DN150 min
        //   Islak standpipe: sürekli basınçlı
        //   Kuru standpipe:  itfaiye bağlantısı ile dolum
        //   >15m binalar: ıslak standpipe zorunlu (TR Yön. §93)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_standpipe_qa",
            RequiresTransaction = false,
            Description =
                "Standpipe riser DN boyutu ve sistem tipi kontrolu (NFPA 14 / TR Yangin Yonetmeligi).\n\n" +
                "params:\n" +
                "  bina_yuksekligi_m — bina toplam yuksekligi m\n" +
                "  mevcut_dn_mm      — modeldeki riser cap mm (0=kontrol atla)\n" +
                "  sistem_tipi       — islak|kuru|kombine (default islak)\n" +
                "  kat_sayisi        — toplam kat sayisi\n\n" +
                "Kural: <30m=DN100, 30-60m=DN125, >60m=DN150. >15m bina islak zorunlu.\n" +
                "Cikti: gerekli_dn_mm, sistem_tipi, gereksinimler, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> StandpipeQa(OpContext ctx)
        {
            double h       = ctx.GetDouble("bina_yuksekligi_m", 0);
            int    mevcutDn= ctx.GetInt("mevcut_dn_mm", 0);
            string tip     = ctx.GetString("sistem_tipi", "islak").ToLowerInvariant();
            int    katSay  = ctx.GetInt("kat_sayisi", 0);

            if (h <= 0 && katSay > 0) h = katSay * 3.0; // kat yüksekliği tahmini
            if (h <= 0) return ErrResult("bina_yuksekligi_m veya kat_sayisi girilmelidir.");

            // Gerekli DN
            int gerekliDn = StandpipeDnTable.First(t => h <= t.maxYukseklikM).dnMm;

            // Sistem tipi kontrolü
            var gereksinimler = new List<string>();
            if (h > 15 && tip == "kuru")
                gereksinimler.Add("⚠️ >15m binalarda ıslak standpipe zorunludur (TR Yangın Yön. §93)");
            if (h > 30)
                gereksinimler.Add("Yüksek zon için PRV gereklidir (max 6.9 bar hortum çıkışı)");
            gereksinimler.Add($"Riser min DN: {gerekliDn}mm");
            gereksinimler.Add("FDC (İtfaiye Bağlantısı) zorunlu — bina girişine yakın");
            gereksinimler.Add("Her katta hortum valfi: 1200mm AFF");
            gereksinimler.Add("Çatı test valfi zorunlu");

            // DN kontrolü
            string dnDurum = mevcutDn <= 0 ? "KONTROL_EDILMEDI" :
                             mevcutDn >= gerekliDn ? "UYGUN" : "DN_YETERSIZ";

            // Islak/kuru tipi
            string tipDurum = (h > 15 && tip == "kuru") ? "TIP_HATASI" : "UYGUN";

            string genelDurum = (dnDurum == "DN_YETERSIZ" || tipDurum == "TIP_HATASI")
                ? "UYGUN_DEGIL" : "UYGUN";

            ctx.Log($"  fp_standpipe_qa: h={h}m → DN{gerekliDn} gerekli, " +
                    $"mevcut DN{mevcutDn} → {genelDurum}");

            return new()
            {
                ["bina_yuksekligi_m"] = h,
                ["kat_sayisi"]        = katSay,
                ["sistem_tipi"]       = tip,
                ["gerekli_dn_mm"]     = gerekliDn,
                ["mevcut_dn_mm"]      = mevcutDn > 0 ? mevcutDn : (object?)"—",
                ["dn_durum"]          = dnDurum,
                ["tip_durum"]         = tipDurum,
                ["gereksinimler"]     = gereksinimler,
                ["durum"]             = genelDurum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2 — fp_standpipe_pressure
        // Standpipe basınç hesabı + PRV gereksinimi
        //
        // NFPA 14 §7.8: Max hortum çıkışı basıncı = 6.9 bar (100 psi)
        // Statik basınç: P = ρ × g × h
        // Yüksek bina: PRV her 10-15 katta
        //
        // params:
        //   bina_yuksekligi_m  — toplam bina yüksekliği
        //   pompa_basinc_bar   — pompa çıkış basıncı bar
        //   kat_yuksekligi_m   — kat yüksekliği (default 3.0)
        //   max_hortum_bar     — max hortum çıkışı bar (default 6.9, NFPA 14)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_standpipe_pressure",
            RequiresTransaction = false,
            Description =
                "Standpipe statik basinc hesabi ve PRV gereksinimi (NFPA 14).\n\n" +
                "params:\n" +
                "  bina_yuksekligi_m — toplam bina yuksekligi m\n" +
                "  pompa_basinc_bar  — pompa cikis basinci bar\n" +
                "  kat_yuksekligi_m  — kat yuksekligi m (default 3.0)\n" +
                "  max_hortum_bar    — max hortum cikisi bar (default 6.9)\n\n" +
                "NFPA 14: max 6.9 bar hortum cikisi. PRV yuksek zonda zorunlu.\n" +
                "Cikti: statik_basinc_bar, prv_sayisi, prv_konumlari, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> StandpipePressure(OpContext ctx)
        {
            double h       = ctx.GetDouble("bina_yuksekligi_m", 0);
            double pPompa  = ctx.GetDouble("pompa_basinc_bar", 0);
            double katYuk  = ctx.GetDouble("kat_yuksekligi_m", 3.0);
            double maxHort = ctx.GetDouble("max_hortum_bar", 6.9);

            if (h <= 0) return ErrResult("bina_yuksekligi_m > 0 olmalidir.");

            // Zemin katta statik basınç (çatıdan besleme varsayımı)
            double statikBar = Math.Round(1000.0 * 9.81 * h / 100_000.0, 3);

            // Toplam basınç (pompa + statik)
            double toplamBar = Math.Round(pPompa + statikBar, 3);

            // PRV hesabı: maxHortum/zon aralığı
            double maxZonYuk = maxHort * 100_000.0 / (1000.0 * 9.81);
            int maxKatPerZon = Math.Max(1, (int)Math.Floor(maxZonYuk / katYuk));
            int katSayisi = (int)Math.Ceiling(h / katYuk);
            int prvSayisi = Math.Max(0, (int)Math.Ceiling((double)katSayisi / maxKatPerZon) - 1);

            var prvKonumlari = new List<int>();
            for (int i = 1; i <= prvSayisi; i++)
                prvKonumlari.Add(i * maxKatPerZon + 1);

            string durum = statikBar > maxHort * 1.5 ? "PRV_ZORUNLU" :
                           statikBar > maxHort       ? "PRV_GEREKLI" : "UYGUN";

            ctx.Log($"  fp_standpipe_pressure: h={h}m → statik={statikBar}bar, " +
                    $"{prvSayisi} PRV gerekli → {durum}");

            return new()
            {
                ["bina_yuksekligi_m"]  = h,
                ["pompa_basinc_bar"]   = pPompa,
                ["statik_basinc_bar"]  = statikBar,
                ["toplam_basinc_bar"]  = toplamBar,
                ["max_hortum_bar"]     = maxHort,
                ["max_kat_per_zon"]    = maxKatPerZon,
                ["prv_sayisi"]         = prvSayisi,
                ["prv_kat_konumlari"]  = prvKonumlari,
                ["durum"]              = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 3 — fp_fdc_clearance_check
        // FDC (İtfaiye Bağlantısı) konumu ve temizlik mesafesi
        //
        // NFPA 14 §6.3 / TR Yangın Yönetmeliği:
        //   FDC bina dışında, yola yakın, erişilebilir konumda
        //   Min AFF: 450mm (NFPA 14 §6.3.1)
        //   FDC bağlantısı önünde min 3000mm temizlik
        //   İtfaiye araç erişimi: min 4.0m genişlik
        //   Check valve zorunlu (geri akış önleme)
        //   İşaretleme zorunlu (FIRE DEPT CONNECTION)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_fdc_clearance_check",
            RequiresTransaction = true,
            Description =
                "FDC (Itfaiye Baglantisi) konumu ve temizlik mesafesi kontrolu (NFPA 14).\n\n" +
                "params:\n" +
                "  min_aff_mm       — min montaj yuksekligi mm (default 450)\n" +
                "  min_temizlik_mm  — on temizlik mesafesi mm (default 3000)\n" +
                "  write_back       — EG_FdcUygunluk parametresine yaz (default false)\n\n" +
                "Revit: EG_FdcYukseklik + EG_FdcTemizlik parametreleri okunur.\n" +
                "Cikti: fdc_id, yukseklik_mm, temizlik_mm, durum",
            Category = "Yangın")]
        public static List<Dictionary<string, object?>> FdcClearanceCheck(OpContext ctx)
        {
            var rctx    = RequireRevit(ctx);
            int minAff  = ctx.GetInt("min_aff_mm", 450);
            int minTem  = ctx.GetInt("min_temizlik_mm", 3000);
            bool wb     = ctx.GetBool("write_back", false);

            // FDC = FireFightingDevices veya GenericModels kategori + EG_FDC parametre
            var elements = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .WhereElementIsNotElementType()
                .Where(e => e.LookupParameter("EG_FdcYukseklik") != null ||
                            (e.Name ?? "").ToLower().Contains("fdc") ||
                            (e.Name ?? "").ToLower().Contains("itfaiye"))
                .ToList();

            if (elements.Count == 0)
                return ErrRows("FDC elemanı bulunamadı. EG_FdcYukseklik parametresi " +
                               "veya 'fdc'/'itfaiye' isimli GenericModel gerekli.");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(rctx.Doc, "FDC QA", rctx.IsAtomicMode);

            foreach (var el in elements)
            {
                long eid = Rv.GetId(el.Id);
                double yuksekMm = (el.LookupParameter("EG_FdcYukseklik")?.AsDouble() ?? 0) * 304.8;
                double temizMm  = (el.LookupParameter("EG_FdcTemizlik")?.AsDouble() ?? 0) * 304.8;

                var kontroller = new List<string>();
                string durum = "UYGUN";

                if (yuksekMm <= 0)  { kontroller.Add("EG_FdcYukseklik parametresi eksik"); durum = "PARAMETRE_YOK"; }
                else if (yuksekMm < minAff) { kontroller.Add($"Yükseklik {yuksekMm:F0}mm < {minAff}mm min"); durum = "UYGUN_DEGIL"; }
                else kontroller.Add($"Yükseklik {yuksekMm:F0}mm ≥ {minAff}mm ✓");

                if (temizMm <= 0)   { kontroller.Add("EG_FdcTemizlik parametresi eksik"); }
                else if (temizMm < minTem) { kontroller.Add($"Temizlik {temizMm:F0}mm < {minTem}mm min"); durum = "UYGUN_DEGIL"; }
                else kontroller.Add($"Temizlik {temizMm:F0}mm ≥ {minTem}mm ✓");

                if (wb) SetS(el, "EG_FdcUygunluk", durum);

                rows.Add(new()
                {
                    ["fdc_id"]      = eid.ToString(),
                    ["yukseklik_mm"]= yuksekMm > 0 ? Math.Round(yuksekMm, 0) : (object?)"—",
                    ["temizlik_mm"] = temizMm > 0  ? Math.Round(temizMm, 0)  : (object?)"—",
                    ["kontroller"]  = kontroller,
                    ["durum"]       = durum,
                });
            }

            scope.Commit();
            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  fp_fdc_clearance_check: {ok}/{rows.Count} FDC UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 4 — fp_pump_schedule_validate
        // Yangın pompası schedule doğrulama
        //
        // Zorunlu pompa tipleri (NFPA 20 / TR Yangın Yön.):
        //   Ana pompa (Electric): tasarım debisi + %150 debide sıfır basınç
        //   Jockey pompası: sızıntıyı karşılayacak küçük debi
        //   Diesel pompa: yedek (elektrik arızasında)
        //
        // params:
        //   ana_pompa_lpm      — ana pompa tasarım debisi L/dak
        //   ana_pompa_bar      — ana pompa tasarım basıncı bar
        //   jockey_lpm         — jockey debi L/dak (default ana×0.01)
        //   diesel_gerekli     — bool (default true if >6 kat)
        //   kat_sayisi         — diesel gereksinim tespiti için
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_pump_schedule_validate",
            RequiresTransaction = false,
            Description =
                "Yangin pompasi schedule dogrulamasi (NFPA 20 / TR Yangin Yon.).\n\n" +
                "params:\n" +
                "  ana_pompa_lpm  — ana pompa tasarim debisi L/dak\n" +
                "  ana_pompa_bar  — ana pompa tasarim basinci bar\n" +
                "  jockey_lpm     — jockey debi L/dak (default ana x 0.01)\n" +
                "  diesel_gerekli — bool (default true >6 kat)\n" +
                "  kat_sayisi     — diesel gereksinim tespiti\n\n" +
                "NFPA 20: Ana x 1.5 debide min basinc, jockey küçük sürekli.\n" +
                "Cikti: ana_pompa, jockey_pompa, diesel_pompa, gereksinimler, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> PumpScheduleValidate(OpContext ctx)
        {
            double anaLpm  = ctx.GetDouble("ana_pompa_lpm", 0);
            double anaBar  = ctx.GetDouble("ana_pompa_bar", 0);
            double jockLpm = ctx.GetDouble("jockey_lpm", -1);
            bool dieselGer = ctx.GetBool("diesel_gerekli", true);
            int katSay     = ctx.GetInt("kat_sayisi", 0);

            if (anaLpm <= 0) return ErrResult("ana_pompa_lpm > 0 olmalidir.");
            if (anaBar <= 0) return ErrResult("ana_pompa_bar > 0 olmalidir.");

            if (jockLpm < 0) jockLpm = Math.Max(10.0, anaLpm * 0.01);
            if (katSay > 6)  dieselGer = true;

            // NFPA 20 performans kriterleri
            double yuzde150Lpm = anaLpm * 1.5;   // %150 debide min %65 basınç
            double yuzde150Bar = anaBar * 0.65;
            double kapamaBar   = anaBar * 1.40;   // 0 debide max %140 basınç

            // Jockey boyutlandırma kuralı: %1-2 ana debi, min 10 L/dak
            double jockMaxLpm = anaLpm * 0.02;
            bool jockUygun = jockLpm >= 10 && jockLpm <= jockMaxLpm;

            var gereksinimler = new List<string>
            {
                $"Ana pompa: {anaLpm}L/dak @ {anaBar}bar",
                $"%150 debi ({yuzde150Lpm:F0}L/dak) @ min {yuzde150Bar:F2}bar",
                $"Kapatma basıncı: max {kapamaBar:F2}bar",
                $"Jockey: {jockLpm:F0}L/dak (tavsiye {10}–{jockMaxLpm:F0}L/dak)",
            };
            if (dieselGer) gereksinimler.Add("Diesel yedek pompa zorunlu (>6 kat / kritik tesis)");

            string durum = jockUygun ? "UYGUN" : "JOCKEY_BOYUTU_KONTROL";

            ctx.Log($"  fp_pump_schedule_validate: Ana={anaLpm}L/dak@{anaBar}bar " +
                    $"Jockey={jockLpm}L/dak → {durum}");

            return new()
            {
                ["ana_pompa_lpm"]      = anaLpm,
                ["ana_pompa_bar"]      = anaBar,
                ["yuzde150_lpm"]       = yuzde150Lpm,
                ["yuzde150_min_bar"]   = Math.Round(yuzde150Bar, 3),
                ["kapatma_max_bar"]    = Math.Round(kapamaBar, 3),
                ["jockey_lpm"]         = Math.Round(jockLpm, 1),
                ["jockey_max_lpm"]     = Math.Round(jockMaxLpm, 1),
                ["jockey_uygun"]       = jockUygun,
                ["diesel_gerekli"]     = dieselGer,
                ["gereksinimler"]      = gereksinimler,
                ["durum"]              = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 5 — fp_pump_hp_calc
        // Yangın pompası HP hesabı
        //
        // Ph = ρ × g × Q × H / 1000  (kW)
        // Toplam yük = statik basınç + sürtünme kaybı + gerekli minimum basınç
        //
        // params:
        //   debi_lpm          — tasarım debisi L/dak
        //   toplam_yuk_m      — toplam manometrik yük (m su kolonu)
        //   pompa_verim       — 0.60-0.80 (default 0.70)
        //   motor_verim       — 0.85-0.95 (default 0.90)
        //   guvenlik_faktor   — 1.15-1.25 (default 1.25 — yangın pompası için)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_pump_hp_calc",
            RequiresTransaction = false,
            Description =
                "Yangin pompasi HP hesabi (NFPA 20). Ph=rho*g*Q*H/1000.\n\n" +
                "params:\n" +
                "  debi_lpm       — tasarim debisi L/dak\n" +
                "  toplam_yuk_m   — toplam manometrik yuk m su kolonu\n" +
                "  pompa_verim    — 0.60-0.80 (default 0.70)\n" +
                "  motor_verim    — 0.85-0.95 (default 0.90)\n" +
                "  guvenlik_faktor — 1.25 (yangin pompasi, NFPA 20)\n\n" +
                "Cikti: ph_kw, pb_kw, pm_kw, oneri_motor_kw, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> PumpHpCalc(OpContext ctx)
        {
            double Q_lpm = ctx.GetDouble("debi_lpm", 0);
            double H     = ctx.GetDouble("toplam_yuk_m", 0);
            double np    = Math.Clamp(ctx.GetDouble("pompa_verim",   0.70), 0.4, 0.9);
            double nm    = Math.Clamp(ctx.GetDouble("motor_verim",   0.90), 0.7, 0.99);
            double sf    = Math.Clamp(ctx.GetDouble("guvenlik_faktor",1.25), 1.0, 1.5);

            if (Q_lpm <= 0) return ErrResult("debi_lpm > 0 olmalidir.");
            if (H <= 0)     return ErrResult("toplam_yuk_m > 0 olmalidir.");

            double Q_m3s = Q_lpm / 60_000.0;
            double ph    = 1000.0 * 9.81 * Q_m3s * H / 1000.0;
            double pb    = ph / np;
            double pm    = pb / nm * sf;

            // Standart motor serileri (kW) — yangın pompası
            double[] std = {1.5,2.2,3,4,5.5,7.5,11,15,18.5,22,30,37,45,55,75,90,110,132,160,200,250,315,400};
            double stdKw = std.FirstOrDefault(m => m >= pm);
            if (stdKw == 0) stdKw = Math.Ceiling(pm / 50.0) * 50;

            ctx.Log($"  fp_pump_hp_calc: Q={Q_lpm}L/dak H={H}m → " +
                    $"Ph={ph:F2}kW Pm={pm:F2}kW → {stdKw}kW");

            return new()
            {
                ["debi_lpm"]       = Q_lpm,
                ["debi_m3s"]       = Math.Round(Q_m3s, 5),
                ["toplam_yuk_m"]   = H,
                ["pompa_verim"]    = np,
                ["motor_verim"]    = nm,
                ["guvenlik_faktor"]= sf,
                ["ph_kw"]          = Math.Round(ph, 3),
                ["pb_kw"]          = Math.Round(pb, 3),
                ["pm_kw"]          = Math.Round(pm, 3),
                ["oneri_motor_kw"] = stdKw,
                ["durum"]          = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 6 — fp_sprinkler_hydraulic
        // Sprinkler hidrolik hesap: K-faktörü, debi, basınç
        //
        // NFPA 13 / TS EN 12845:
        //   Q = K × √P   (L/dak, bar)
        //   Min işletme basıncı K tipine göre tabloda
        //   Tasarım noktası: en uzak/elverişsiz sprinkler
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_sprinkler_hydraulic",
            RequiresTransaction = false,
            Description =
                "Sprinkler hidrolik hesap: K-faktoru, debi, basinc (NFPA 13 / TS EN 12845).\n\n" +
                "params:\n" +
                "  k_faktoru      — K57|K80|K115|K160|K202|K242|K320|K363\n" +
                "  isletme_basinc_bar — sprinkler isletme basinci bar\n" +
                "  sprinkler_sayisi   — hidrolik olarak acik bas sayisi\n" +
                "  tehlike_sinifi     — dusuk|orta_1|orta_2|yuksek_1 (TR Ek-8/B)\n\n" +
                "Q = K * sqrt(P). Cikti: debi_lpm, toplam_debi, min_basinc, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> SprinklerHydraulic(OpContext ctx)
        {
            string kTip  = ctx.GetString("k_faktoru", "K80").ToUpper();
            double P     = ctx.GetDouble("isletme_basinc_bar", 0);
            int    sayisi= ctx.GetInt("sprinkler_sayisi", 1);
            string tehlike= ctx.GetString("tehlike_sinifi", "orta_1").ToLowerInvariant();

            if (!KFactorTable.TryGetValue(kTip, out var kData))
                return ErrResult($"Bilinmeyen K-faktörü: '{kTip}'. " +
                    "Desteklenen: " + string.Join(", ", KFactorTable.Keys));

            if (P <= 0) P = kData.minBar; // Min basınç kullan

            var (k, minBar, minLpm) = kData;

            // Tek başlık debisi: Q = K × √P
            double debiLpm = k * Math.Sqrt(P);

            // Min basınç kontrolü
            bool basincUygun = P >= minBar;
            bool debiUygun   = debiLpm >= minLpm;

            // Toplam debi (tüm açık başlıklar)
            double toplamDebi = debiLpm * sayisi;

            // Tehlike sınıfına göre min debi kontrolü (TR Ek-8/B)
            var tehlikeMap = new Dictionary<string, double>
                { {"dusuk",40},{"orta_1",56},{"orta_2",80},{"orta_3",100},
                  {"yuksek_1",130},{"yuksek_2",160} };
            double minTehlikeDebi = tehlikeMap.TryGetValue(tehlike, out var td) ? td : 56;
            bool tehlikeUygun = debiLpm >= minTehlikeDebi;

            string durum = !basincUygun ? "BASINC_YETERSIZ" :
                           !tehlikeUygun ? "DEBI_YETERSIZ" : "UYGUN";

            ctx.Log($"  fp_sprinkler_hydraulic: {kTip} P={P}bar → " +
                    $"Q={debiLpm:F1}L/dak, toplam={toplamDebi:F1}L/dak → {durum}");

            return new()
            {
                ["k_faktoru"]        = kTip,
                ["k_degeri"]         = k,
                ["isletme_basinc_bar"]= P,
                ["min_basinc_bar"]   = minBar,
                ["debi_lpm"]         = Math.Round(debiLpm, 1),
                ["min_debi_lpm"]     = minLpm,
                ["sprinkler_sayisi"] = sayisi,
                ["toplam_debi_lpm"]  = Math.Round(toplamDebi, 1),
                ["tehlike_sinifi"]   = tehlike,
                ["min_tehlike_debi"] = minTehlikeDebi,
                ["basinc_uygun"]     = basincUygun,
                ["tehlike_uygun"]    = tehlikeUygun,
                ["durum"]            = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 7 — fp_sprinkler_temp_class
        // Sprinkler sıcaklık sınıfı doğrulama
        //
        // TS EN 12845 / NFPA 13:
        //   Montaj ortam sıcaklığı + güvenlik marjı → sıcaklık sınıfı
        //   Genel kural: sprinkler çalışma sıcaklığı ≥ max ortam + 30°C
        //   Kazan dairesi / mutfak: yüksek sınıf gerekli
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_sprinkler_temp_class",
            RequiresTransaction = false,
            Description =
                "Sprinkler sicaklik sinifi dogrulamasi (TS EN 12845 / NFPA 13).\n\n" +
                "params:\n" +
                "  ortam_sicaklik_c  — max beklenen ortam sicakligi C\n" +
                "  mevcut_sinif      — 57C|68C|79C|93C|141C|182C|204C|260C\n" +
                "  guvenlik_marji_c  — min marj C (default 30)\n\n" +
                "Kural: calisma temp >= ortam + guvenlik_marji.\n" +
                "Cikti: gerekli_sinif, mevcut_sinif, renk, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> SprinklerTempClass(OpContext ctx)
        {
            double ortam  = ctx.GetDouble("ortam_sicaklik_c", 0);
            string mevcut = ctx.GetString("mevcut_sinif", "").ToUpper().Replace("°", "");
            double marj   = ctx.GetDouble("guvenlik_marji_c", 30.0);

            if (ortam <= 0) return ErrResult("ortam_sicaklik_c > 0 olmalidir.");

            double gerekliMin = ortam + marj;

            // Gerekli sınıfı bul
            var siniflar = new[] { 57, 68, 79, 93, 141, 182, 204, 260 };
            int gerekliC = siniflar.FirstOrDefault(s => s >= gerekliMin);
            if (gerekliC == 0) gerekliC = 260;
            string gerekliSinif = $"{gerekliC}C";

            if (!TempClasses.TryGetValue(gerekliSinif, out var gerekliData))
                gerekliData = (0, 218, "Siyah");

            // Mevcut sınıf kontrolü
            string durum = "PARAMETRE_YOK";
            string mevcutRenk = "—";
            if (!string.IsNullOrEmpty(mevcut))
            {
                if (!mevcut.EndsWith("C")) mevcut += "C";
                if (TempClasses.TryGetValue(mevcut, out var mevcutData))
                {
                    mevcutRenk = mevcutData.renk;
                    int mevcutC = int.Parse(mevcut.Replace("C", ""));
                    durum = mevcutC >= gerekliC ? "UYGUN" : "SINIF_YETERSIZ";
                }
                else durum = "BILINMEYEN_SINIF";
            }

            ctx.Log($"  fp_sprinkler_temp_class: ortam={ortam}°C → " +
                    $"gerekli≥{gerekliMin}°C → {gerekliSinif} ({durum})");

            return new()
            {
                ["ortam_sicaklik_c"] = ortam,
                ["guvenlik_marji_c"] = marj,
                ["gerekli_min_c"]    = gerekliMin,
                ["gerekli_sinif"]    = gerekliSinif,
                ["gerekli_renk"]     = gerekliData.renk,
                ["mevcut_sinif"]     = string.IsNullOrEmpty(mevcut) ? (object?)"—" : mevcut,
                ["mevcut_renk"]      = mevcutRenk,
                ["durum"]            = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 8 — fp_detection_coverage
        // Dedektör kapsama alanı hesabı (NFPA 72 / TR Yangın Yön.)
        //
        // NFPA 72 Table 17.6.3.1.1 & TR Yangın Yönetmeliği §72:
        //   Duman dedektörü (düz tavan ≤3.5m):  max 60-80 m² / cihaz
        //   Isı dedektörü:                       max 30 m² / cihaz
        //   Tavan yüksekliği > 3.5m: kaplama alanı azalır
        //   Izgara yerleşimi: max kenar mesafesi √(kapsama/2)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_detection_coverage",
            RequiresTransaction = false,
            Description =
                "Dedektör kapsama alani hesabi (NFPA 72 / TR Yangin Yonetmeligi).\n\n" +
                "params:\n" +
                "  oda_alani_m2      — kontrol edilecek alan m2\n" +
                "  dedektör_tipi     — duman|isi|alev|co (default duman)\n" +
                "  tavan_yuksekligi_m — tavan yuksekligi m (default 2.7)\n" +
                "  mevcut_sayi       — modeldeki dedektör sayisi (0=sadece hesap)\n\n" +
                "Cikti: gerekli_sayi, kapsama_per_cihaz_m2, max_aralik_m, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> DetectionCoverage(OpContext ctx)
        {
            double alan    = ctx.GetDouble("oda_alani_m2", 0);
            string tip     = ctx.GetString("dedektör_tipi", "duman").ToLowerInvariant();
            double tavanH  = ctx.GetDouble("tavan_yuksekligi_m", 2.7);
            int    mevcut  = ctx.GetInt("mevcut_sayi", 0);

            if (alan <= 0) return ErrResult("oda_alani_m2 > 0 olmalidir.");

            // Taban kapsama (m²/cihaz) — NFPA 72
            double bazKapsama = tip switch
            {
                "isi"   => 30.0,
                "alev"  => 75.0,
                "co"    => 60.0,
                _       => 60.0  // duman (standart tavan)
            };

            // Tavan yüksekliği düzeltmesi (>3.5m için azaltma)
            if (tavanH > 3.5 && tip == "duman")
            {
                // NFPA 72: her 0.3m artış için %15 azaltma (yaklaşık)
                double fazla = tavanH - 3.5;
                double azaltma = Math.Min(0.5, fazla / 0.3 * 0.15);
                bazKapsama *= (1.0 - azaltma);
            }
            else if (tavanH > 7.5)
            {
                bazKapsama *= 0.5; // Yüksek tavan için yarı kapsama
            }

            int gerekliSayi = (int)Math.Ceiling(alan / bazKapsama);
            double maxAralik = Math.Sqrt(bazKapsama / 2.0); // Izgara yerleşimi

            string durum = mevcut <= 0 ? "HESAP_SONUCU" :
                           mevcut >= gerekliSayi ? "UYGUN" : "DEDEKTÖR_YETERSIZ";

            ctx.Log($"  fp_detection_coverage: {alan}m² {tip} → " +
                    $"{gerekliSayi} gerekli, {bazKapsama:F1}m²/cihaz → {durum}");

            return new()
            {
                ["oda_alani_m2"]       = alan,
                ["dedektör_tipi"]      = tip,
                ["tavan_yuksekligi_m"] = tavanH,
                ["kapsama_m2_per_cihaz"]= Math.Round(bazKapsama, 1),
                ["gerekli_sayi"]       = gerekliSayi,
                ["mevcut_sayi"]        = mevcut > 0 ? mevcut : (object?)"—",
                ["max_aralik_m"]       = Math.Round(maxAralik, 2),
                ["durum"]              = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 9 — fp_suppression_agent_qa
        // Söndürme ajanı tipi QA — alan tipine uygunluk
        //
        // TR Yangın Yönetmeliği + NFPA standartları:
        //   Su: genel kullanım, elektrik tesisi hariç
        //   Gazlı (CO2/FM200/Inergen): sunucu odası, jeneratör, arşiv
        //   Köpük: akaryakıt, uçak hangari, boya depoları
        //   Kuru toz: metal yangını, mutfak
        //   Temiz ajan: insan olan ortamlar
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_suppression_agent_qa",
            RequiresTransaction = false,
            Description =
                "Sondurme ajani tipi ve alan uygunluk kontrolu.\n\n" +
                "params:\n" +
                "  ajan_tipi  — su|kuru_toz|kopuk|co2|fm200|inergen|abc_toz\n" +
                "  alan_tipi  — sunucu_odasi|jenerator|arsiv|mutfak|akaryakit|\n" +
                "               genel_ofis|imalathane|depo|otopark\n\n" +
                "Cikti: ajan_tipi, alan_tipi, uygun_mu, alternatifler, standartlar, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> SuppressionAgentQa(OpContext ctx)
        {
            string ajan = ctx.GetString("ajan_tipi", "").ToLowerInvariant();
            string alan = ctx.GetString("alan_tipi", "").ToLowerInvariant();

            if (string.IsNullOrEmpty(ajan)) return ErrResult("ajan_tipi girilmelidir.");
            if (string.IsNullOrEmpty(alan)) return ErrResult("alan_tipi girilmelidir.");

            // Alan → uygun ajanlar + standartlar
            var alanUygunluk = new Dictionary<string, (string[] uygunAjanlar, string[] standartlar, string notlar)>
            {
                ["sunucu_odasi"]  = (new[]{"fm200","inergen","co2","novec"},
                    new[]{"NFPA 75","ISO 14520"},"CO2 sadece boş alan için"),
                ["jenerator"]     = (new[]{"fm200","inergen","co2","kuru_toz"},
                    new[]{"NFPA 37","NFPA 11"},  "Yakıt yangını riski"),
                ["arsiv"]         = (new[]{"fm200","inergen","su"},
                    new[]{"NFPA 909","ISO 14520"},"Hasar kaygısı düşük ajan"),
                ["mutfak"]        = (new[]{"kuru_toz","abc_toz","su_sis"},
                    new[]{"NFPA 17A","UL 300"},  "Yağ yangını sınıfı F"),
                ["akaryakit"]     = (new[]{"kopuk","kuru_toz","co2"},
                    new[]{"NFPA 11","EN 13565"},  "Sıvı yakıt yangını sınıfı B"),
                ["genel_ofis"]    = (new[]{"su","fm200","inergen"},
                    new[]{"NFPA 13","TS EN 12845"},"İnsan varlığında temiz ajan"),
                ["imalathane"]    = (new[]{"su","kuru_toz","kopuk"},
                    new[]{"NFPA 13","TR Yangın Yön."},"Risk sınıfına göre seç"),
                ["depo"]          = (new[]{"su","kopuk"},
                    new[]{"NFPA 13","TR Ek-8/B"}, "Yüksek raflar için ESFR"),
                ["otopark"]       = (new[]{"su","kopuk","kuru_toz"},
                    new[]{"NFPA 88A","TR Yangın Yön."},"CO2 kapalı otoparkta riskli"),
            };

            if (!alanUygunluk.TryGetValue(alan, out var alanData))
                return ErrResult($"Bilinmeyen alan tipi: '{alan}'. " +
                    "Desteklenen: " + string.Join(", ", alanUygunluk.Keys));

            bool uygunMu = alanData.uygunAjanlar.Contains(ajan);
            string durum = uygunMu ? "UYGUN" : "AJAN_UYUMSUZ";

            var uyarılar = new List<string>();
            if (!uygunMu)
                uyarılar.Add($"'{ajan}' bu alan için önerilmez. " +
                             $"Alternatifler: {string.Join(", ", alanData.uygunAjanlar)}");
            if (ajan == "co2" && (alan == "genel_ofis" || alan == "sunucu_odasi"))
                uyarılar.Add("CO2 insan bulunan ortamlarda tehlikelidir — boşaltma prosedürü zorunlu");
            if (alan == "sunucu_odasi" && ajan == "su")
                uyarılar.Add("Su söndürme sunucu ekipmanına zarar verir");

            uyarılar.AddRange(alanData.standartlar.Select(s => $"Standart: {s}"));
            if (!string.IsNullOrEmpty(alanData.notlar)) uyarılar.Add($"Not: {alanData.notlar}");

            ctx.Log($"  fp_suppression_agent_qa: {ajan} @ {alan} → {durum}");

            return new()
            {
                ["ajan_tipi"]      = ajan,
                ["alan_tipi"]      = alan,
                ["uygun_mu"]       = uygunMu,
                ["alternatifler"]  = alanData.uygunAjanlar,
                ["standartlar"]    = alanData.standartlar,
                ["uyarilar"]       = uyarılar,
                ["durum"]          = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 10 — fp_evacuation_route_check
        // Tahliye yolu genişliği ve maksimum mesafe kontrolü
        //
        // TR Yangın Yönetmeliği §49-57:
        //   Min koridor genişliği: 1.20m
        //   Min merdiven genişliği: 1.20m (100 kişi üzeri +0.6m her 50 kişi)
        //   Min kapı genişliği: 0.90m (kaçış kapıları)
        //   Max çıkmaz koridor: 25m (sprinklersiz), 50m (sprinklerli)
        //   Max çıkış yolu: 60m (sprinklersiz), 90m (sprinklerli)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_evacuation_route_check",
            RequiresTransaction = false,
            Description =
                "Tahliye yolu genisligi ve max mesafe kontrolu (TR Yangin Yon. §49-57).\n\n" +
                "params:\n" +
                "  yol_tipi         — koridor|merdiven|kapi|rampa\n" +
                "  mevcut_genislik_m — modeldeki genislik m\n" +
                "  kisi_sayisi       — bu yolu kullanan kisi sayisi\n" +
                "  cikis_mesafesi_m  — en uzak noktadan cikisa uzaklik m\n" +
                "  sprinkler_var     — bool (mesafe limiti etkiler)\n\n" +
                "Cikti: min_genislik_m, mevcut_genislik_m, max_mesafe_m, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> EvacuationRouteCheck(OpContext ctx)
        {
            string yolTip    = ctx.GetString("yol_tipi", "koridor").ToLowerInvariant();
            double mevcutG   = ctx.GetDouble("mevcut_genislik_m", 0);
            int    kisi      = ctx.GetInt("kisi_sayisi", 0);
            double cikisMes  = ctx.GetDouble("cikis_mesafesi_m", 0);
            bool   sprinkler = ctx.GetBool("sprinkler_var", false);

            // Min genişlik hesabı
            double minG = TahliyeMinGenislik.TryGetValue(yolTip, out var mg) ? mg : 1.20;

            // Merdiven için kişi sayısına göre artış (TR Yön. §54)
            if (yolTip == "merdiven" && kisi > 100)
                minG += Math.Ceiling((kisi - 100.0) / 50.0) * 0.60;

            // Max çıkış mesafesi (sprinkler etkisi)
            double maxMes = yolTip switch
            {
                "koridor" when sprinkler  => 90.0,
                "koridor"                 => 60.0,
                "merdiven"                => 999, // merdiven mesafesi ayrıca değerlendirme
                _                         => sprinkler ? 90.0 : 60.0
            };

            // Çıkmaz koridor
            double maxCikmazMes = sprinkler ? 50.0 : 25.0;

            var kontroller = new List<string>();
            string durum = "UYGUN";

            if (mevcutG > 0)
            {
                if (mevcutG < minG)
                { kontroller.Add($"Genişlik {mevcutG}m < {minG}m min"); durum = "GENISLIK_YETERSIZ"; }
                else kontroller.Add($"Genişlik {mevcutG}m ≥ {minG}m ✓");
            }
            else kontroller.Add("mevcut_genislik_m girilmedi — kontrol atlandı");

            if (cikisMes > 0)
            {
                if (cikisMes > maxMes)
                { kontroller.Add($"Çıkış mesafesi {cikisMes}m > {maxMes}m max"); durum = "MESAFE_ASIMI"; }
                else kontroller.Add($"Çıkış mesafesi {cikisMes}m ≤ {maxMes}m ✓");
            }

            ctx.Log($"  fp_evacuation_route_check: {yolTip} {mevcutG}m genişlik, " +
                    $"{cikisMes}m mesafe → {durum}");

            return new()
            {
                ["yol_tipi"]          = yolTip,
                ["kisi_sayisi"]       = kisi,
                ["min_genislik_m"]    = Math.Round(minG, 2),
                ["mevcut_genislik_m"] = mevcutG > 0 ? mevcutG : (object?)"—",
                ["max_cikis_mesafesi_m"]= maxMes,
                ["max_cikmaz_m"]      = maxCikmazMes,
                ["sprinkler_var"]     = sprinkler,
                ["kontroller"]        = kontroller,
                ["durum"]             = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 11 — fp_exit_sign_spacing
        // Acil çıkış işareti yerleşimi (TS EN 1838 / NFPA 101)
        //
        // TS EN 1838 §4.3:
        //   Max görünürlük mesafesi: işaret yüksekliği × 200
        //   Tipik işaret (150mm): 200 × 150mm = 30m
        //   Her çıkış kapısında, yön değişiminde, çıkmaz koridorda
        //   Montaj yüksekliği: 2.0–2.5m AFF (yönlendirme işareti)
        //   Veya taban seviyesi (duman durumunda görünür, <1m AFF)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_exit_sign_spacing",
            RequiresTransaction = false,
            Description =
                "Acil cikis isareti yerlesimi kontrolu (TS EN 1838 / NFPA 101).\n\n" +
                "params:\n" +
                "  koridor_uzunlugu_m  — toplam koridor uzunlugu m\n" +
                "  isaret_yuksekligi_mm — isaret yuksekligi mm (default 150)\n" +
                "  mevcut_isaret_sayisi — modeldeki isaret sayisi (0=hesap)\n" +
                "  montaj_yuksekligi_m  — montaj yuksekligi m (default 2.2)\n\n" +
                "Cikti: max_gorunurluk_m, gerekli_sayi, mevcut_sayi, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> ExitSignSpacing(OpContext ctx)
        {
            double korLen   = ctx.GetDouble("koridor_uzunlugu_m", 0);
            int    isaretMm = ctx.GetInt("isaret_yuksekligi_mm", 150);
            int    mevcut   = ctx.GetInt("mevcut_isaret_sayisi", 0);
            double montajH  = ctx.GetDouble("montaj_yuksekligi_m", 2.2);

            if (korLen <= 0) return ErrResult("koridor_uzunlugu_m > 0 olmalidir.");

            // TS EN 1838: max görünürlük = işaret yüksekliği × 200
            double maxGorunurluk = isaretMm / 1000.0 * 200.0;

            // Gerekli işaret sayısı
            int gerekliSayi = (int)Math.Ceiling(korLen / maxGorunurluk);

            // Montaj yüksekliği kontrolü
            bool montajUygun = (montajH >= 2.0 && montajH <= 2.5) || montajH < 1.0;
            string montajNot = montajH < 1.0 ? "Taban seviyesi (duman güvenliği için)" :
                               montajUygun ? "Standart montaj yüksekliği" :
                               "⚠️ Montaj yüksekliği 2.0-2.5m veya <1m olmalı (TS EN 1838)";

            string durum = mevcut <= 0 ? "HESAP_SONUCU" :
                           mevcut >= gerekliSayi ? "UYGUN" : "ISARET_YETERSIZ";

            ctx.Log($"  fp_exit_sign_spacing: {korLen}m koridor, {isaretMm}mm işaret " +
                    $"→ maxGör={maxGorunurluk}m, {gerekliSayi} gerekli → {durum}");

            return new()
            {
                ["koridor_uzunlugu_m"]   = korLen,
                ["isaret_yuksekligi_mm"] = isaretMm,
                ["max_gorunurluk_m"]     = maxGorunurluk,
                ["gerekli_sayi"]         = gerekliSayi,
                ["mevcut_sayi"]          = mevcut > 0 ? mevcut : (object?)"—",
                ["montaj_yuksekligi_m"]  = montajH,
                ["montaj_uygun"]         = montajUygun,
                ["montaj_notu"]          = montajNot,
                ["standart"]             = "TS EN 1838 §4.3 / NFPA 101",
                ["durum"]                = durum,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 12 — fp_fire_door_rating_check
        // Yangın kapısı fire rating + boşluk kontrolü
        //
        // TR Yangın Yönetmeliği + TS EN 1634:
        //   EI 30/60/90/120 sınıfı (bölme türüne göre)
        //   Kapı boşluğu (üst/yan): max 4mm
        //   Kapı altı boşluğu: max 8mm (eşiksiz), 3mm (eşikli)
        //   Otomatik kapanma (door closer) zorunlu
        //   Yangın duvarı kapısı: duvar derecesinin min yarısı
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_fire_door_rating_check",
            RequiresTransaction = true,
            Description =
                "Yangin kapisi fire rating ve bosluk kontrolu (TS EN 1634 / TR Yangin Yon.).\n\n" +
                "params:\n" +
                "  min_rating_dak    — min gereken EI suresi dakika (30|60|90|120)\n" +
                "  tolerance_mm      — rating toleransi (default 0 = tam eslesme)\n" +
                "  write_back        — EG_KapiUygunluk'a yaz (default false)\n\n" +
                "Revit: EG_YanginKapiRating + EG_KapiUstBosluk + EG_KapiAltBosluk okunur.\n" +
                "Cikti: kapi_id, mevcut_rating, gerekli_rating, bosluk_durum, durum",
            Category = "Yangın")]
        public static List<Dictionary<string, object?>> FireDoorRatingCheck(OpContext ctx)
        {
            var rctx     = RequireRevit(ctx);
            int minRating= ctx.GetInt("min_rating_dak", 60);
            bool wb      = ctx.GetBool("write_back", false);

            var doors = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .ToList();

            // Yalnızca yangın kapısı parametresi olan kapılar
            var fireDoors = doors
                .Where(d => d.LookupParameter("EG_YanginKapiRating") != null ||
                            d.LookupParameter("Fire Rating") != null)
                .ToList();

            if (fireDoors.Count == 0)
                return ErrRows("EG_YanginKapiRating veya 'Fire Rating' parametresi " +
                               "olan kapı bulunamadı. Önce parametreleri atayın.");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(rctx.Doc, "Yangın Kapı QA", rctx.IsAtomicMode);

            foreach (var door in fireDoors)
            {
                long did = Rv.GetId(door.Id);

                // Rating oku (EG_ önce, yoksa built-in)
                string ratingStr = door.LookupParameter("EG_YanginKapiRating")?.AsString() ??
                                   door.LookupParameter("Fire Rating")?.AsString() ?? "";

                // Rating'i dakikaya çevir (EI60 → 60, "60 dakika" → 60, "2 saat" → 120)
                int ratingDak = ParseRatingDak(ratingStr);

                // Boşluk parametreleri
                double ustBoslukMm = (door.LookupParameter("EG_KapiUstBosluk")?.AsDouble() ?? 0) * 304.8;
                double altBoslukMm = (door.LookupParameter("EG_KapiAltBosluk")?.AsDouble() ?? 0) * 304.8;

                // Kontroller
                string ratingDurum = ratingDak <= 0 ? "RATING_YOK" :
                                     ratingDak >= minRating ? "UYGUN" : "RATING_YETERSIZ";

                string boslukDurum = "PARAMETRE_YOK";
                if (ustBoslukMm > 0 || altBoslukMm > 0)
                {
                    bool ustOk = ustBoslukMm <= 0 || ustBoslukMm <= 4.0;
                    bool altOk = altBoslukMm <= 0 || altBoslukMm <= 8.0;
                    boslukDurum = (ustOk && altOk) ? "UYGUN" : "BOSLUK_ASIMI";
                }

                string genelDurum = (ratingDurum == "UYGUN" &&
                    (boslukDurum == "UYGUN" || boslukDurum == "PARAMETRE_YOK"))
                    ? "UYGUN" : "KONTROL_GEREKLI";

                if (wb) SetS(door, "EG_KapiUygunluk", genelDurum);

                rows.Add(new()
                {
                    ["kapi_id"]        = did.ToString(),
                    ["mevcut_rating"]  = ratingDak > 0 ? $"EI{ratingDak}" : (object?)"—",
                    ["gerekli_rating"] = $"EI{minRating}",
                    ["rating_durum"]   = ratingDurum,
                    ["ust_bosluk_mm"]  = ustBoslukMm > 0 ? Math.Round(ustBoslukMm, 1) : (object?)"—",
                    ["alt_bosluk_mm"]  = altBoslukMm > 0 ? Math.Round(altBoslukMm, 1) : (object?)"—",
                    ["bosluk_durum"]   = boslukDurum,
                    ["durum"]          = genelDurum,
                });
            }

            scope.Commit();
            int ok = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  fp_fire_door_rating_check: {ok}/{rows.Count} kapı UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 13 — fp_compartment_area_check
        // Yangın kompartıman alanı kontrolü
        //
        // TR Yangın Yönetmeliği Tablo-1:
        //   Konut: 2000m² / 8 kat
        //   Ofis: 2500m² / 8 kat
        //   Hastane: 2000m² / 2 kat
        //   Depo: 1000m² / 2 kat
        //   Sprinkler olunca limit 2× artabilir
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("fp_compartment_area_check",
            RequiresTransaction = false,
            Description =
                "Yangin kompartiman alani ve kat sayisi kontrolu (TR Yangin Yon. Tablo-1).\n\n" +
                "params:\n" +
                "  bina_kullanimi   — konut|ofis|ticaret|hastane|depo|sanayi_dusuk|\n" +
                "                     sanayi_orta|sanayi_yuksek|toplanma|yatakhane\n" +
                "  kompartiman_alani_m2 — hesaplanan kompartiman alani m2\n" +
                "  kat_sayisi        — kompartimandaki kat sayisi\n" +
                "  sprinkler_var     — bool (limit 2x artabilir)\n\n" +
                "Cikti: max_alan_m2, mevcut_alan_m2, max_kat, durum",
            Category = "Yangın")]
        public static Dictionary<string, object?> CompartmentAreaCheck(OpContext ctx)
        {
            string kullanim = ctx.GetString("bina_kullanimi", "ofis").ToLowerInvariant();
            double mevcutAlan = ctx.GetDouble("kompartiman_alani_m2", 0);
            int katSay      = ctx.GetInt("kat_sayisi", 1);
            bool sprinkler  = ctx.GetBool("sprinkler_var", false);

            if (mevcutAlan <= 0) return ErrResult("kompartiman_alani_m2 > 0 olmalidir.");

            if (!KompartımanLimits.TryGetValue(kullanim, out var limitData))
                return ErrResult($"Bilinmeyen kullanım: '{kullanim}'. " +
                    "Desteklenen: " + string.Join(", ", KompartımanLimits.Keys));

            double maxAlan = limitData.maxAlanM2;
            int maxKat     = limitData.maxKatSayisi;

            // Sprinkler ile limit artışı
            if (sprinkler)
            {
                maxAlan *= 2.0;
                maxKat  = (int)(maxKat * 1.5);
            }

            bool alanUygun = mevcutAlan <= maxAlan;
            bool katUygun  = katSay <= maxKat;

            string durum = (!alanUygun || !katUygun) ? "LIMIT_ASILDI" : "UYGUN";

            var mesajlar = new List<string>();
            if (!alanUygun) mesajlar.Add($"Alan {mevcutAlan}m² > {maxAlan}m² max");
            if (!katUygun)  mesajlar.Add($"Kat sayısı {katSay} > {maxKat} max");
            if (sprinkler)  mesajlar.Add("Sprinkler nedeniyle limit 2× uygulandı");

            ctx.Log($"  fp_compartment_area_check: {kullanim} {mevcutAlan}m²/{katSay}kat " +
                    $"(max {maxAlan}m²/{maxKat}kat) → {durum}");

            return new()
            {
                ["bina_kullanimi"]       = kullanim,
                ["mevcut_alan_m2"]       = mevcutAlan,
                ["mevcut_kat_sayisi"]    = katSay,
                ["max_alan_m2"]          = maxAlan,
                ["max_kat_sayisi"]       = maxKat,
                ["sprinkler_var"]        = sprinkler,
                ["alan_uygun"]           = alanUygun,
                ["kat_uygun"]            = katUygun,
                ["mesajlar"]             = mesajlar,
                ["standart"]             = "TR Yangın Yönetmeliği Tablo-1",
                ["durum"]                = durum,
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═════════════════════════════════════════════════════════════════════

        private static int ParseRatingDak(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.ToLower().Trim();
            if (s.Contains("120") || s.Contains("4 sa") || s.Contains("2 sa") && s.Contains("120")) return 120;
            if (s.Contains("90"))  return 90;
            if (s.Contains("60") || s.Contains("1 sa")) return 60;
            if (s.Contains("30")) return 30;
            if (int.TryParse(System.Text.RegularExpressions.Regex.Match(s, @"\d+").Value, out int v)) return v;
            return 0;
        }

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
