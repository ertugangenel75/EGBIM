// ============================================================
// EGBIMOTO — MEP Boşluk Yönetim Motoru (MepOpeningOps) — v10.6
// Apache 2.0 — EGBIM / Ertugan Gocer
// ============================================================
// Kaynak: script.py (Kanal Boşluğu Açma v8.4.1) +
//         script_py.py (MEP Boşluk Yönetim Sistemi v2.4)
// pyRevit IronPython mantığı → native C# Revit API op'larına dönüştürüldü.
//
// Op listesi (4 adet):
//   1. mep_opening_detect     — MEP eleman-duvar kesişim tespiti + boşluk yerleştirme
//   2. mep_opening_validate   — Geçersiz boşluk + EC-2 boyut sınıflandırması
//   3. mep_opening_bcf_export — BCF 2.1 ihraç (Navisworks/Solibri/BIMcollab)
//   4. mep_lintel_place       — Otomatik lento (lintel) yerleştirme
//
// Parametreler (Shared Param olarak mevcut olmalı):
//   KB_Width, KB_Height, KB_Durum, KB_Kaynak_ID,
//   KB_Disiplin, KB_Aciklama, KB_Alt_Kot, KB_Son_Guncelleme, KB_Duvar_Sinifi
//
// Standartlar:
//   EC-2 (EN 1992-1-1) — Betonarme boşluk takviye kuralları
//   BCF 2.1             — buildingSMART BIM Collaboration Format
//   NFPA / TR Yangın Yön. — Fire-rated duvar geçiş mesafeleri
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    public static class MepOpeningOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // SABİTLER — script.py'deki parametrelerle birebir uyumlu
        // ─────────────────────────────────────────────────────────────────────
        private const string FAM_NAME      = "Kanal_Boslugu";
        private const string P_WIDTH       = "KB_Width";
        private const string P_HEIGHT      = "KB_Height";
        private const string P_DURUM       = "KB_Durum";
        private const string P_KAYNAK_ID   = "KB_Kaynak_ID";
        private const string P_DISIPLIN    = "KB_Disiplin";
        private const string P_ACIKLAMA    = "KB_Aciklama";
        private const string P_ALT_KOT     = "KB_Alt_Kot";
        private const string P_SON_GUN     = "KB_Son_Guncelleme";
        private const string P_DUVAR_SINIF = "KB_Duvar_Sinifi";

        private const string STATUS_APPROVED = "Onaylandi";
        private const string STATUS_PENDING  = "Onay Bekliyor";
        private const string STATUS_MANUAL   = "Manuel Kontrol";

        // Fire-rated tanıma: FIRE, FR, YANGIN, F.R içeren duvar tipleri
        private static readonly string[] FireKeywords = { "FIRE", "FR", "YANGIN", "F.R" };
        private static readonly Regex FireRatingRe     = new Regex(@"^(rei|ei)?\s*\d+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Boşluk aile keyword listesi (get_all_bosluklar mantığı)
        private static readonly string[] BoslukKeywords =
        {
            "boslug", "bosluk", "opening", "gecis", "delik",
            "penetr", "mep", "kanal", "hole", "sleeve", "insert"
        };

        // Temizleme sabiti: mm → feet
        private const double MM_TO_FT = 1.0 / 304.8;
        private const double FT_TO_MM = 304.8;

        // ─────────────────────────────────────────────────────────────────────
        // OP 1 — mep_opening_detect
        // MEP eleman-duvar kesişim tespiti + Kanal_Boslugu ailesi yerleştirme
        //
        // Kaynak: script.py → main() + get_dimensions() + check_overlap()
        //         + group_hits_by_bbox_overlap() + compute_union_opening()
        //
        // Mantık:
        //   1. Duct/Pipe/CableTray elemanlarını topla
        //   2. Her MEP elemanının BoundingBox'ını duvarlarla kesişim kontrolü
        //   3. Fire-rated duvar tespiti → clearance: normal=60mm, fire=100mm
        //   4. Grupla (çakışan MEP → tek union boşluk)
        //   5. Kanal_Boslugu ailesi yerleştir, parametreleri yaz
        //   6. KB_Durum = "Onay Bekliyor"
        //
        // params:
        //   clearance_normal_mm — normal duvar geçiş payı mm (default 60)
        //   clearance_fire_mm   — yangın duvarı geçiş payı mm (default 100)
        //   min_size_mm         — min boşluk boyutu mm, altı atlanır (default 50)
        //   level_filter        — seviye adı filtresi, boş = tüm seviyeler
        //   guncelle_mevcut     — mevcut boşlukları güncelle (default true)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_opening_detect",
            RequiresTransaction = true,
            Description =
                "MEP eleman-duvar kesisim tespiti ve Kanal_Boslugu aile yerlesimi.\n" +
                "Kaynak: script.py (Kanal Boslugu v8.4.1) C#'a donusturuldu.\n\n" +
                "params:\n" +
                "  clearance_normal_mm — normal duvar gecis payi mm (default 60)\n" +
                "  clearance_fire_mm   — yangin duvari gecis payi mm (default 100)\n" +
                "  min_size_mm         — min bosluk boyutu mm (default 50)\n" +
                "  level_filter        — seviye adi filtresi (bos=tumu)\n" +
                "  guncelle_mevcut     — mevcut bosluklari guncelle (default true)\n\n" +
                "Cikti: tespit edilen, yerlestirilen, guncellenen bosluk sayilari",
            Category = "MEP-Koordinasyon")]
        public static Dictionary<string, object?> OpeningDetect(OpContext ctx)
        {
            var rctx         = RequireRevit(ctx);
            double clrNormal = ctx.GetDouble("clearance_normal_mm", 60.0)  * MM_TO_FT;
            double clrFire   = ctx.GetDouble("clearance_fire_mm",   100.0) * MM_TO_FT;
            double minSize   = ctx.GetDouble("min_size_mm",          50.0)  * MM_TO_FT;
            string lvFilter  = ctx.GetString("level_filter", "").ToLower();
            bool   guncelle  = ctx.GetBool("guncelle_mevcut", true);

            var doc = rctx.Doc;

            // Kanal_Boslugu ailesini bul
            var famSym = FindOpeningFamilySymbol(doc);
            if (famSym == null)
                return ErrResult($"'{FAM_NAME}' ailesi projede bulunamadi. " +
                                 "Once Load Family ile yukleyin.");

            // MEP elemanlarını topla: Duct + Pipe + CableTray
            var mepCats = new[]
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_CableTray,
            };

            var mepElems = mepCats.SelectMany(cat =>
                new FilteredElementCollector(doc)
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .ToList()).ToList();

            // Duvarları topla (level filtresi ile)
            var walls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w =>
                {
                    if (string.IsNullOrEmpty(lvFilter)) return true;
                    var lv = doc.GetElement(w.LevelId) as Level;
                    return lv?.Name.ToLower().Contains(lvFilter) == true;
                })
                .ToList();

            if (!walls.Any())
                return ErrResult("Filtre kriterine uyan duvar bulunamadi.");

            // Mevcut boşlukları indeksle (güncelleme için)
            var mevcutBosluklar = CollectAllBosluklar(doc);
            var mevIndeks = mevcutBosluklar.ToDictionary(
                b => b.LookupParameter(P_KAYNAK_ID)?.AsString() ?? "",
                b => b);

            int tespit = 0, yerlestirilen = 0, guncellenenSay = 0;
            var satirlar = new List<Dictionary<string, object?>>();

            using var scope = new RevitWriteScope(doc, "MEP Bosluk Tespit", rctx.IsAtomicMode);

            // Aile sembolünü aktive et
            if (!famSym.IsActive) { famSym.Activate(); doc.Regenerate(); }

            foreach (var wall in walls)
            {
                bool isFireRated = IsFireRated(wall);
                double clearance = isFireRated ? clrFire : clrNormal;
                string wallClass = isFireRated ? "FireRated" : "Normal";
                var wallBb = wall.get_BoundingBox(null);
                if (wallBb == null) continue;

                // Bu duvara çarpan MEP elemanları
                var hits = new List<(Element mep, double w, double h, XYZ pt, string disiplin)>();

                foreach (var mep in mepElems)
                {
                    var mepBb = mep.get_BoundingBox(null);
                    if (mepBb == null) continue;

                    // BoundingBox kesişim
                    if (!BboxIntersects(wallBb, mepBb)) continue;

                    var (w, h) = GetMepDimensions(mep, clearance);
                    if (w < minSize || h < minSize) continue;

                    var pt = BboxCenter(mepBb);
                    string dis = GetDiscipline(mep);
                    hits.Add((mep, w, h, pt, dis));
                    tespit++;
                }

                if (!hits.Any()) continue;

                // Grupla ve union boşluk hesapla
                var groups = GroupByBboxOverlap(hits, wall);

                foreach (var grp in groups)
                {
                    var (unionW, unionH, unionPt, disciplines) = ComputeUnion(grp, clearance);

                    // Mevcut boşluğu güncelle veya yeni yerleştir
                    string srcKey = string.Join("+", grp.Select(g => Rv.GetId(g.mep.Id)));
                    if (guncelle && mevIndeks.TryGetValue(srcKey, out var mevcut))
                    {
                        UpdateOpening(mevcut, unionPt, unionW, unionH, wall, wallClass, disciplines);
                        guncellenenSay++;
                    }
                    else
                    {
                        var level = doc.GetElement(wall.LevelId) as Level;
                        if (level == null) continue;

                        var inst = doc.Create.NewFamilyInstance(
                            unionPt, famSym, wall, level, StructuralType.NonStructural);

                        SetParam(inst, P_WIDTH,       unionW * FT_TO_MM);
                        SetParam(inst, P_HEIGHT,      unionH * FT_TO_MM);
                        SetParam(inst, P_DURUM,       STATUS_PENDING);
                        SetParam(inst, P_KAYNAK_ID,   srcKey);
                        SetParam(inst, P_DISIPLIN,    disciplines);
                        SetParam(inst, P_ACIKLAMA,    wall.WallType?.Name ?? "");
                        SetParam(inst, P_ALT_KOT,     unionPt.Z * FT_TO_MM);
                        SetParam(inst, P_SON_GUN,     DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                        SetParam(inst, P_DUVAR_SINIF, wallClass);

                        yerlestirilen++;
                    }

                    satirlar.Add(new()
                    {
                        ["durum"]     = "TESPIT_EDILDI",
                        ["genislik_mm"]= Math.Round(unionW * FT_TO_MM, 0),
                        ["yukseklik_mm"]= Math.Round(unionH * FT_TO_MM, 0),
                        ["disiplin"]  = disciplines,
                        ["duvar_sinif"]= wallClass,
                    });
                }
            }

            scope.Commit();

            ctx.Log($"  mep_opening_detect: {tespit} kesisim, " +
                    $"{yerlestirilen} yeni, {guncellenenSay} guncellendi");

            return new()
            {
                ["tespit_sayisi"]       = tespit,
                ["yerlestirilen"]       = yerlestirilen,
                ["guncellenen"]         = guncellenenSay,
                ["toplam_bosluk"]       = yerlestirilen + guncellenenSay,
                ["bosluklar"]           = satirlar,
                ["durum"]               = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2 — mep_opening_validate
        // Geçersiz boşluk tespiti + EC-2 boyut sınıflandırması
        //
        // Kaynak: script_py.py → gecersiz_bosluk_tespiti() +
        //         ec2_kural_degerlendirme() + statik_kural_kontrol()
        //
        // EC-2 (EN 1992-1-1) kuralları:
        //   Yuvarlak/Kare (en/boy > 0.85):
        //     d < 350mm         → OK
        //     350 ≤ d ≤ 600mm   → TAKVİYE: 2Ø16 (Abi/Abo) + 2Ø16 (Vbi/Vbo)
        //     d > 600mm         → MÜHENDİS REVİZYONU
        //   Dikdörtgen:
        //     d < 200mm         → OK
        //     200 < d ≤ 400mm   → TAKVİYE: 2Ø16 (Hbi/Hbo) + 2Ø16 (Vbi/Vbo)
        //     400 < d ≤ 600mm   → TAKVİYE+: 2Ø12 + min Ø12/200 (Ub)
        //     d > 600mm         → MÜHENDİS REVİZYONU
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_opening_validate",
            RequiresTransaction = true,
            Description =
                "Gecersiz bosluk tespiti + EC-2 boyut siniflandirmasi.\n" +
                "Kaynak: script_py.py (MEP Bosluk Yonetim v2.4) C#'a donusturuldu.\n\n" +
                "params:\n" +
                "  ec2_kontrol     — EC-2 boyut siniflandirmasi yap (default true)\n" +
                "  kiriş_mesafe_mm — kirişe yakinlik limiti mm (default 300)\n" +
                "  bosluk_arasi_mm — min bosluk arasi mesafe mm (default 200)\n" +
                "  durum_guncelle  — KB_Durum'u otomatik guncelle (default true)\n\n" +
                "EC-2: Yuvarlak d<350=OK, 350-600=TAKVİYE, >600=REVİZYON.\n" +
                "Dikdortgen: d<200=OK, 200-400=TAKVİYE, 400-600=TAKVİYE+, >600=REVİZYON.\n" +
                "Cikti: bosluk_id, genislik_mm, yukseklik_mm, ec2_seviye, kb_durum",
            Category = "MEP-Koordinasyon")]
        public static List<Dictionary<string, object?>> OpeningValidate(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            bool ec2       = ctx.GetBool("ec2_kontrol", true);
            double kirisLim= ctx.GetDouble("kiriş_mesafe_mm", 300.0) * MM_TO_FT;
            double boslukLim= ctx.GetDouble("bosluk_arasi_mm", 200.0) * MM_TO_FT;
            bool guncelleD = ctx.GetBool("durum_guncelle", true);

            var doc = rctx.Doc;
            var bosluklar = CollectAllBosluklar(doc);

            if (!bosluklar.Any())
                return ErrRows("Modelde MEP boslugu bulunamadi. " +
                               "Once mep_opening_detect calistirin.");

            // MEP elemanları — geçersiz boşluk tespiti için
            var mepOutlines = CollectMepOutlines(doc);

            var rows = new List<Dictionary<string, object?>>();
            var durumGuncellenecek = new List<(FamilyInstance inst, string durum)>();

            using var scope = new RevitWriteScope(doc, "MEP Bosluk Validate", rctx.IsAtomicMode);

            // Kirişler — yakınlık kontrolü için
            var kirisler = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var b in bosluklar)
            {
                long bid = Rv.GetId(b.Id);
                var (wMm, hMm) = GetBoslukBoyutlari(b);
                string mevcutDurum = GetParam(b, P_DURUM) ?? "";
                string disiplin    = GetParam(b, P_DISIPLIN) ?? "—";

                // Geçersizlik: MEP elemanı kalmamış mı?
                bool gecersiz = false;
                var bBb = b.get_BoundingBox(null);
                if (bBb != null)
                {
                    const double tol = 0.1;
                    var bOutline = new Outline(
                        new XYZ(bBb.Min.X - tol, bBb.Min.Y - tol, bBb.Min.Z - tol),
                        new XYZ(bBb.Max.X + tol, bBb.Max.Y + tol, bBb.Max.Z + tol));
                    gecersiz = !mepOutlines.Any(mol => bOutline.Intersects(mol, tol));
                }

                // EC-2 sınıflandırma
                string ec2Seviye = "—";
                string ec2Aciklama = "—";
                string? yeniDurum = null;

                if (ec2 && wMm > 0 && hMm > 0)
                {
                    (ec2Seviye, ec2Aciklama, yeniDurum) = Ec2KuralDegerlendirme(wMm, hMm);
                }

                // Kirişe yakınlık
                bool kirisYakini = false;
                if (bBb != null)
                {
                    var bPt = new XYZ(
                        (bBb.Min.X + bBb.Max.X) / 2,
                        (bBb.Min.Y + bBb.Max.Y) / 2,
                        (bBb.Min.Z + bBb.Max.Z) / 2);
                    kirisYakini = kirisler.Any(k =>
                    {
                        var kBb = k.get_BoundingBox(null);
                        if (kBb == null) return false;
                        var kPt = new XYZ(
                            (kBb.Min.X + kBb.Max.X) / 2,
                            (kBb.Min.Y + kBb.Max.Y) / 2,
                            (kBb.Min.Z + kBb.Max.Z) / 2);
                        return bPt.DistanceTo(kPt) < kirisLim;
                    });
                }

                // Durum güncelleme
                if (guncelleD && yeniDurum != null)
                {
                    string mevUpper = mevcutDurum.ToUpper();
                    if (!mevUpper.Contains("ONAY") && !mevUpper.Contains("REVIZYON"))
                        durumGuncellenecek.Add((b, yeniDurum));
                }
                if (guncelleD && gecersiz && !mevcutDurum.ToUpper().Contains("ONAY"))
                    durumGuncellenecek.Add((b, "IPTAL - GECERSIZ"));

                rows.Add(new()
                {
                    ["bosluk_id"]    = bid.ToString(),
                    ["genislik_mm"]  = wMm > 0 ? Math.Round(wMm, 0) : (object?)"—",
                    ["yukseklik_mm"] = hMm > 0 ? Math.Round(hMm, 0) : (object?)"—",
                    ["disiplin"]     = disiplin,
                    ["ec2_seviye"]   = ec2Seviye,
                    ["ec2_aciklama"] = ec2Aciklama,
                    ["kiris_yakini"] = kirisYakini,
                    ["gecersiz"]     = gecersiz,
                    ["kb_durum"]     = mevcutDurum,
                    ["yeni_durum"]   = yeniDurum ?? (object?)"—",
                    ["durum"]        = gecersiz ? "GECERSIZ" :
                                       ec2Seviye == "REVIZYON" ? "REVIZYON" :
                                       ec2Seviye.Contains("TAKVIYE") ? "TAKVIYE" : "OK",
                });
            }

            // Toplu durum güncelleme
            foreach (var (inst, durum) in durumGuncellenecek)
            {
                SetParamStr(inst, P_DURUM, durum);
                SetParamStr(inst, P_SON_GUN, DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
            }

            scope.Commit();

            int ok = rows.Count(r => (string?)r["durum"] == "OK");
            int takviye = rows.Count(r => ((string?)r["durum"])?.Contains("TAKVIYE") == true);
            int revizyon = rows.Count(r => (string?)r["durum"] == "REVIZYON");
            int gecersizSay = rows.Count(r => (bool?)r["gecersiz"] == true);

            ctx.Log($"  mep_opening_validate: {rows.Count} bosluk — " +
                    $"OK:{ok} TAKVİYE:{takviye} REVİZYON:{revizyon} GEÇERSİZ:{gecersizSay}");

            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 3 — mep_opening_bcf_export
        // BCF 2.1 ihraç (Navisworks / Solibri / BIMcollab uyumlu)
        //
        // Kaynak: script_py.py → bcf_export() fonksiyonu
        //
        // BCF 2.1 yapısı:
        //   bcf.version     — sürüm bilgisi
        //   project.bcfp    — proje GUID
        //   topic_NNN_idXXX/
        //     markup.bcf    — topic XML
        //     viewpoint.bcfv — kamera + selection XML
        //     snapshot.png   — 1×1 placeholder (Revit screenshot API yok)
        //   → .bcfzip arşivine paketlenir
        //
        // params:
        //   output_path     — çıktı klasörü
        //   durum_filtresi  — hangi durumlar: "RED,SORUN,GECERSIZ,REVIZYON,TAKVIYE"
        //   sadece_kritik   — sadece REVİZYON ve GEÇERSİZ (default false)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_opening_bcf_export",
            RequiresTransaction = false,
            Description =
                "Sorunlu MEP bosluklar icin BCF 2.1 ihraç.\n" +
                "Kaynak: script_py.py bcf_export() C#'a donusturuldu.\n\n" +
                "params:\n" +
                "  output_path    — cikti klasoru\n" +
                "  durum_filtresi — hangi durumlar dahil (varsayilan: RED,SORUN,GECERSIZ,REVIZYON,TAKVIYE)\n" +
                "  sadece_kritik  — sadece REVIZYON ve GECERSIZ (default false)\n\n" +
                "Cikti: .bcfzip dosya yolu, topic sayisi",
            Category = "MEP-Koordinasyon")]
        public static Dictionary<string, object?> OpeningBcfExport(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            string outDir = ctx.RequireString("output_path");
            bool sadeceKritik = ctx.GetBool("sadece_kritik", false);
            string durumFiltre = ctx.GetString("durum_filtresi",
                "RED,SORUN,GECERSIZ,REVIZYON,TAKVIYE,IPTAL").ToUpper();

            var doc = rctx.Doc;
            var bosluklar = CollectAllBosluklar(doc);

            // Filtrele
            var sorunlular = bosluklar.Where(b =>
            {
                string d = (GetParam(b, P_DURUM) ?? "").ToUpper();
                if (sadeceKritik)
                    return d.Contains("REVIZYON") || d.Contains("GECERSIZ");
                return durumFiltre.Split(',').Any(f => d.Contains(f.Trim()));
            }).ToList();

            if (!sorunlular.Any())
                return ErrResult("BCF'e aktarilacak sorunlu bosluk bulunamadi. " +
                                 "Once mep_opening_validate calistirin.");

            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            string tarih    = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string bcfKlasor = Path.Combine(outDir, $"MEP_Bosluk_BCF_{tarih}");
            Directory.CreateDirectory(bcfKlasor);

            string dtIso = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string projGuid = Guid.NewGuid().ToString();

            // bcf.version
            File.WriteAllText(Path.Combine(bcfKlasor, "bcf.version"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<Version VersionId=\"2.1\" xsi:noNamespaceSchemaLocation=\"version.xsd\" " +
                "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "  <DetailedVersion>2.1</DetailedVersion>\n" +
                "</Version>\n", Encoding.UTF8);

            // project.bcfp
            File.WriteAllText(Path.Combine(bcfKlasor, "project.bcfp"),
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                $"<ProjectExtension xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                $"  <Project ProjectId=\"{projGuid}\">\n" +
                $"    <Name>{doc.Title ?? "MEP Project"}</Name>\n" +
                $"  </Project>\n" +
                $"  <ExtensionSchema></ExtensionSchema>\n" +
                $"</ProjectExtension>\n", Encoding.UTF8);

            // 1×1 PNG placeholder
            byte[] png1x1 = {
                0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,
                0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
                0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,
                0x08,0x02,0x00,0x00,0x00,0x90,0x77,0x53,
                0xDE,0x00,0x00,0x00,0x0C,0x49,0x44,0x41,
                0x54,0x78,0x9C,0x63,0xF8,0x0F,0x00,0x00,
                0x11,0x00,0x01,0x9E,0xD8,0x4E,0xD4,0x00,
                0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
                0x42,0x60,0x82
            };

            int yazilan = 0;
            for (int idx = 0; idx < sorunlular.Count; idx++)
            {
                var b    = sorunlular[idx];
                string d = GetParam(b, P_DURUM) ?? "";
                string mep = GetParam(b, P_DISIPLIN) ?? "—";
                var (wMm, hMm) = GetBoslukBoyutlari(b);
                var bb   = b.get_BoundingBox(null);
                long bid = Rv.GetId(b.Id);

                // Kamera konumu
                double M = 1.0 / 3048.0;
                double cx = 3.0, cy = 3.0, cz = 2.0;
                if (bb != null)
                {
                    cx = ((bb.Min.X + bb.Max.X) / 2) * M + 3.0;
                    cy = ((bb.Min.Y + bb.Max.Y) / 2) * M + 3.0;
                    cz = ((bb.Min.Z + bb.Max.Z) / 2) * M + 2.0;
                }

                string topicGuid = Guid.NewGuid().ToString();
                string vpGuid    = Guid.NewGuid().ToString();
                string cmtGuid   = Guid.NewGuid().ToString();
                string ifcGuid   = GetIfcGuid(doc, b);

                string topicKlasor = Path.Combine(bcfKlasor,
                    $"topic_{idx+1:000}_id{bid}");
                Directory.CreateDirectory(topicKlasor);

                string title = $"ID:{bid} - [{d}]";
                string desc  = $"MEP:{mep} | {d} | W:{wMm:F0}xH:{hMm:F0}mm";

                // markup.bcf
                File.WriteAllText(Path.Combine(topicKlasor, "markup.bcf"),
                    $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    $"<Markup xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n" +
                    $"  xsi:noNamespaceSchemaLocation=\"markup.xsd\">\n" +
                    $"  <Header>\n" +
                    $"    <File isExternal=\"false\">\n" +
                    $"      <Filename>{doc.Title}.rvt</Filename>\n" +
                    $"      <Date>{dtIso}</Date>\n" +
                    $"    </File>\n" +
                    $"  </Header>\n" +
                    $"  <Topic Guid=\"{topicGuid}\" TopicType=\"Issue\" TopicStatus=\"Open\">\n" +
                    $"    <Title>{XmlEscape(title)}</Title>\n" +
                    $"    <Priority>High</Priority>\n" +
                    $"    <CreationDate>{dtIso}</CreationDate>\n" +
                    $"    <CreationAuthor>EGBIMOTO MepOpeningOps</CreationAuthor>\n" +
                    $"    <Description>{XmlEscape(desc)}</Description>\n" +
                    $"  </Topic>\n" +
                    $"  <Comment>\n" +
                    $"    <Guid>{cmtGuid}</Guid>\n" +
                    $"    <Date>{dtIso}</Date>\n" +
                    $"    <Author>EGBIMOTO</Author>\n" +
                    $"    <Comment>{XmlEscape(desc)}</Comment>\n" +
                    $"    <Viewpoint Guid=\"{vpGuid}\"/>\n" +
                    $"  </Comment>\n" +
                    $"  <Viewpoints>\n" +
                    $"    <ViewPoint>\n" +
                    $"      <Guid>{vpGuid}</Guid>\n" +
                    $"      <Viewpoint>viewpoint.bcfv</Viewpoint>\n" +
                    $"      <Snapshot>snapshot.png</Snapshot>\n" +
                    $"    </ViewPoint>\n" +
                    $"  </Viewpoints>\n" +
                    $"</Markup>\n", Encoding.UTF8);

                // viewpoint.bcfv
                File.WriteAllText(Path.Combine(topicKlasor, "viewpoint.bcfv"),
                    $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    $"<VisualizationInfo Guid=\"{vpGuid}\"\n" +
                    $"  xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n" +
                    $"  xsi:noNamespaceSchemaLocation=\"visinfo.xsd\">\n" +
                    $"  <PerspectiveCamera>\n" +
                    $"    <CameraViewPoint><X>{cx:F6}</X><Y>{cy:F6}</Y><Z>{cz:F6}</Z></CameraViewPoint>\n" +
                    $"    <CameraDirection><X>-0.577350</X><Y>-0.577350</Y><Z>-0.577350</Z></CameraDirection>\n" +
                    $"    <CameraUpVector><X>0</X><Y>0</Y><Z>1</Z></CameraUpVector>\n" +
                    $"    <FieldOfView>60</FieldOfView>\n" +
                    $"  </PerspectiveCamera>\n" +
                    $"  <Components>\n" +
                    $"    <Visibility DefaultVisibility=\"true\"/>\n" +
                    $"    <Selection>\n" +
                    $"      <Component IfcGuid=\"{ifcGuid}\">\n" +
                    $"        <OriginatingSystem>EGBIMOTO MepOpeningOps</OriginatingSystem>\n" +
                    $"      </Component>\n" +
                    $"    </Selection>\n" +
                    $"  </Components>\n" +
                    $"</VisualizationInfo>\n", Encoding.UTF8);

                File.WriteAllBytes(Path.Combine(topicKlasor, "snapshot.png"), png1x1);
                yazilan++;
            }

            // .bcfzip arşivi
            string bcfZip = bcfKlasor + ".bcfzip";
            CreateZipArchive(bcfKlasor, bcfZip);

            // Klasörü temizle
            try { Directory.Delete(bcfKlasor, true); } catch { }

            ctx.Log($"  mep_opening_bcf_export: {yazilan} topic → {bcfZip}");

            return new()
            {
                ["bcf_dosyasi"]   = bcfZip,
                ["topic_sayisi"]  = yazilan,
                ["toplam_bosluk"] = bosluklar.Count,
                ["durum"]         = "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 4 — mep_lintel_place
        // Otomatik lento (lintel) yerleştirme
        //
        // Kaynak: script_py.py → lento_modulu() + _lento_yerlesim_noktasi()
        //                        + _lento_cap_mm() + ec2_kural_degerlendirme()
        //
        // Kural:
        //   Boşluk genişliği > min_genislik_mm → lento ekle
        //   Duvar tipi: gazbeton/tuğla/bearing → evet, betonarme/perde → hayır
        //   Lento boyu = boşluk genişliği + 2 × binme_payi_mm
        //   Beton sınıfı: C25 altı → Ø16, C30+ → Ø20 (geniş boşlukta)
        //   Lento alt kotu = boşluk üst kotu
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mep_lintel_place",
            RequiresTransaction = true,
            Description =
                "Genis MEP bosluklar icin otomatik lento (lintel) yerlestirme.\n" +
                "Kaynak: script_py.py lento_modulu() C#'a donusturuldu.\n\n" +
                "params:\n" +
                "  min_genislik_mm  — lento gereken min genislik mm (default 600)\n" +
                "  binme_payi_mm    — her iki taraf binme payi mm (default 200)\n" +
                "  beton_sinifi     — C16|C20|C25|C30|C35|C40 (default C25)\n" +
                "  lento_family_isim— Structural Framing aile adi (default: ilk bulunan)\n" +
                "  mevcut_temizle   — mevcut lentoları sil ve yenile (default false)\n\n" +
                "Cikti: eklenen_lento, atlanan, hata_listesi",
            Category = "MEP-Koordinasyon")]
        public static Dictionary<string, object?> LintelPlace(OpContext ctx)
        {
            var rctx         = RequireRevit(ctx);
            double minGenMm  = ctx.GetDouble("min_genislik_mm", 600.0);
            double binmeMm   = ctx.GetDouble("binme_payi_mm", 200.0);
            string betonSin  = ctx.GetString("beton_sinifi", "C25").ToUpper();
            string famIsim   = ctx.GetString("lento_family_isim", "");
            bool mevTemizle  = ctx.GetBool("mevcut_temizle", false);

            var doc = rctx.Doc;
            var bosluklar = CollectAllBosluklar(doc);

            if (!bosluklar.Any())
                return ErrResult("Modelde bosluk bulunamadi. " +
                                 "Once mep_opening_detect calistirin.");

            // Structural Framing sembolü bul
            var lentoSym = FindLintelSymbol(doc, famIsim);
            if (lentoSym == null)
                return ErrResult("Projede Structural Framing (lento) ailesi bulunamadi. " +
                                 "Insert → Load Family → Structural Framing ile yukleyin.");

            using var scope = new RevitWriteScope(doc, "MEP Lento Ekle", rctx.IsAtomicMode);

            // Mevcut lentoları temizle
            if (mevTemizle)
            {
                var mevLentolar = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .WhereElementIsNotElementType()
                    .Where(e => e.LookupParameter("Bosluk_ID") != null)
                    .ToList();
                foreach (var l in mevLentolar)
                    try { doc.Delete(l.Id); } catch { }
                ctx.Log($"  {mevLentolar.Count} mevcut lento temizlendi");
            }

            if (!lentoSym.IsActive) { lentoSym.Activate(); doc.Regenerate(); }

            int eklenen = 0, atlanan = 0;
            var hatalar = new List<string>();

            // Betonarme duvar keyword'leri (atlanacak)
            var betonKw = new[] { "beton", "perde", "concrete", "rc ", "reinf", "betonarme" };
            // Lento uygun duvar keyword'leri
            var lentKw  = new[] { "gazbeton", "ytong", "tugla", "brick", "masonry",
                                   "bearing", "yuk tasiyici", "briket" };

            foreach (var b in bosluklar)
            {
                var (wMm, hMm) = GetBoslukBoyutlari(b);
                if (wMm < minGenMm) { atlanan++; continue; }

                // Host duvar kontrolü
                var wall = b.Host as Wall ?? FindNearestWall(doc, b);
                if (wall == null) { atlanan++; continue; }

                // Duvar tipi uygunluk kontrolü
                // ÖNEMLİ: Önce lento_kw kontrol et (gazbeton "beton" içeriyor — false positive önle)
                string wallTypeName = (wall.WallType?.Name ?? "").ToLower();
                bool lentoluk = lentKw.Any(kw => wallTypeName.Contains(kw));
                bool betonarme = !lentoluk && betonKw.Any(kw => wallTypeName.Contains(kw));
                if (betonarme) { atlanan++; continue; }

                try
                {
                    var bb = b.get_BoundingBox(null);
                    if (bb == null) { atlanan++; continue; }

                    // Boşluk üst kotu = lento alt kotu
                    double bosUstZ = bb.Max.Z;

                    // Lento yüksekliği (aileden oku, yoksa 200mm)
                    double lentoH  = (lentoSym.LookupParameter("h")?.AsDouble()
                                   ?? lentoSym.LookupParameter("Height")?.AsDouble()
                                   ?? 200.0 * MM_TO_FT);

                    // Duvar ekseni yönü
                    var (origin, yon) = GetLintelPlacement(b, wall, bosUstZ);
                    if (origin == null || yon == null) { atlanan++; continue; }

                    // Lento boyu = genişlik + 2×binme
                    double lentoFt = (wMm + 2 * binmeMm) * MM_TO_FT;
                    double yari    = lentoFt / 2.0;
                    double lentoUstZ = bosUstZ + lentoH;

                    var p1 = new XYZ(origin.X - yon.X * yari,
                                     origin.Y - yon.Y * yari,
                                     lentoUstZ);
                    var p2 = new XYZ(origin.X + yon.X * yari,
                                     origin.Y + yon.Y * yari,
                                     lentoUstZ);
                    var eksen = Line.CreateBound(p1, p2);

                    // Ref level
                    var refLevel = FindNearestLevel(doc, lentoUstZ);
                    if (refLevel == null) { atlanan++; continue; }

                    var lentoInst = doc.Create.NewFamilyInstance(
                        eksen, lentoSym, refLevel, StructuralType.Beam);
                    doc.Regenerate();

                    // Parametreler
                    int cap = LintelBarDiam(betonSin, wMm);
                    SetParamStr(lentoInst, "Lento_Onay",    "BEKLIYOR");
                    SetParamStr(lentoInst, "Beton_Sinifi",  betonSin);
                    SetParamStr(lentoInst, "Demir_Capi",    $"O{cap}");
                    SetParamStr(lentoInst, "Bosluk_ID",     Rv.GetId(b.Id).ToString());

                    eklenen++;
                }
                catch (Exception ex)
                {
                    hatalar.Add($"ID:{Rv.GetId(b.Id)} — {ex.Message[..Math.Min(60, ex.Message.Length)]}");
                }
            }

            scope.Commit();
            ctx.Log($"  mep_lintel_place: {eklenen} lento eklendi, " +
                    $"{atlanan} atlandi, {hatalar.Count} hata");

            return new()
            {
                ["eklenen_lento"] = eklenen,
                ["atlanan"]       = atlanan,
                ["hata_sayisi"]   = hatalar.Count,
                ["hata_listesi"]  = hatalar,
                ["beton_sinifi"]  = betonSin,
                ["durum"]         = eklenen > 0 ? "OK" : "LENTO_EKLENMEDI",
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  EC-2 KURAL DEĞERLENDİRME — script_py.py'den birebir dönüştürüldü
        // ═════════════════════════════════════════════════════════════════════
        private static (string seviye, string aciklama, string? yeniDurum)
            Ec2KuralDegerlendirme(double wMm, double hMm)
        {
            double d    = Math.Max(wMm, hMm);
            double kisa = Math.Min(wMm, hMm);
            double oran = d > 0 ? kisa / d : 1.0;
            bool dairesel = oran > 0.85;

            if (dairesel)
            {
                if (d < 350) return ("OK",
                    $"d={d:F0}mm < 350mm — mudahale gerekmez", null);
                if (d <= 600) return ("TAKVIYE",
                    $"d={d:F0}mm (350-600mm) — 2Ø16 (Abi/Abo) + 2Ø16 (Vbi/Vbo)",
                    "TAKVIYE GEREKLI - 2O16");
                return ("REVIZYON",
                    $"d={d:F0}mm > 600mm — Muhendis revizyonu zorunlu",
                    "MUHENDIS REVIZYONU");
            }
            else
            {
                if (d < 200) return ("OK",
                    $"d={d:F0}mm < 200mm — mudahale gerekmez", null);
                if (d <= 400) return ("TAKVIYE",
                    $"d={d:F0}mm (200-400mm) — 2Ø16 (Hbi/Hbo) + 2Ø16 (Vbi/Vbo)",
                    "TAKVIYE GEREKLI - 2O16");
                if (d <= 600) return ("TAKVIYE_PLUS",
                    $"d={d:F0}mm (400-600mm) — 2Ø12 + min Ø12/200 (Ub)",
                    "TAKVIYE GEREKLI - 2O12+O12/200");
                return ("REVIZYON",
                    $"d={d:F0}mm > 600mm — Muhendis revizyonu zorunlu",
                    "MUHENDIS REVIZYONU");
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ORTAK YARDIMCILAR
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Modeldeki tüm MEP boşluklarını topla (get_all_bosluklar mantığı)</summary>
        private static List<FamilyInstance> CollectAllBosluklar(Document doc)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            var result = new List<FamilyInstance>();
            foreach (var fi in collector)
            {
                try
                {
                    // Yöntem 1: KB_ parametreleri var → kesin boşluk
                    if (fi.LookupParameter(P_WIDTH) != null ||
                        fi.LookupParameter(P_HEIGHT) != null ||
                        fi.LookupParameter(P_DURUM) != null)
                    {
                        result.Add(fi); continue;
                    }
                    // Yöntem 2: Aile adı keyword
                    string fname = (fi.Symbol?.FamilyName ?? "").ToLower()
                        .Replace("ğ","g").Replace("ı","i").Replace("ş","s")
                        .Replace("ü","u").Replace("ö","o").Replace("ç","c");
                    if (BoslukKeywords.Any(k => fname.Contains(k)))
                    {
                        result.Add(fi); continue;
                    }
                    // Yöntem 3: Type adı keyword
                    string tname = (fi.Symbol?.Name ?? "").ToLower();
                    if (BoslukKeywords.Any(k => tname.Contains(k)))
                        result.Add(fi);
                }
                catch { }
            }
            return result;
        }

        private static bool IsFireRated(Wall wall)
        {
            string typeName = (wall.WallType?.Name ?? "").ToUpper();
            if (FireKeywords.Any(kw => typeName.Contains(kw))) return true;
            // Fire rating parametresi
            var p = wall.LookupParameter("Fire Rating") ??
                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
            if (p?.HasValue == true)
            {
                string val = p.AsString() ?? "";
                if (FireRatingRe.IsMatch(val.Trim())) return true;
            }
            return false;
        }

        private static (double w, double h) GetMepDimensions(Element el, double clearance)
        {
            var bb = el.get_BoundingBox(null);
            if (bb == null) return (0, 0);
            double dx = Math.Abs(bb.Max.X - bb.Min.X);
            double dy = Math.Abs(bb.Max.Y - bb.Min.Y);
            double dz = Math.Abs(bb.Max.Z - bb.Min.Z);
            // Yatay boyut (büyük olanı) + clearance
            double w = Math.Max(dx, dy) + 2 * clearance;
            double h = dz + 2 * clearance;
            return (w, h);
        }

        private static string GetDiscipline(Element el)
        {
            var cat = Rv.GetCategoryId(el);
            if (cat == (int)BuiltInCategory.OST_DuctCurves)  return "HVAC";
            if (cat == (int)BuiltInCategory.OST_PipeCurves)   return "Plumbing";
            if (cat == (int)BuiltInCategory.OST_CableTray)    return "Electrical";
            return "MEP";
        }

        private static bool BboxIntersects(BoundingBoxXYZ a, BoundingBoxXYZ b)
            => a.Min.X < b.Max.X && a.Max.X > b.Min.X
            && a.Min.Y < b.Max.Y && a.Max.Y > b.Min.Y
            && a.Min.Z < b.Max.Z && a.Max.Z > b.Min.Z;

        private static XYZ BboxCenter(BoundingBoxXYZ bb)
            => new((bb.Min.X + bb.Max.X) / 2,
                   (bb.Min.Y + bb.Max.Y) / 2,
                   (bb.Min.Z + bb.Max.Z) / 2);

        private static List<List<(Element mep, double w, double h, XYZ pt, string dis)>>
            GroupByBboxOverlap(
                List<(Element mep, double w, double h, XYZ pt, string dis)> hits,
                Wall wall)
        {
            // Basit gruplama: 500mm mesafe içindekiler aynı grup
            const double GAP_FT = 500.0 / 304.8;
            var groups = new List<List<(Element mep, double w, double h, XYZ pt, string dis)>>();
            foreach (var hit in hits)
            {
                bool eklendi = false;
                foreach (var grp in groups)
                {
                    if (grp.Any(g => g.pt.DistanceTo(hit.pt) < GAP_FT))
                    {
                        grp.Add(hit); eklendi = true; break;
                    }
                }
                if (!eklendi) groups.Add(new() { hit });
            }
            return groups;
        }

        private static (double w, double h, XYZ pt, string dis)
            ComputeUnion(
                List<(Element mep, double w, double h, XYZ pt, string dis)> grp,
                double clearance)
        {
            double maxW = grp.Max(g => g.w);
            double maxH = grp.Max(g => g.h);
            double cx   = grp.Average(g => g.pt.X);
            double cy   = grp.Average(g => g.pt.Y);
            double cz   = grp.Min(g => g.pt.Z - g.h / 2) + maxH / 2;
            string dis  = string.Join("+", grp.Select(g => g.dis).Distinct());
            return (maxW, maxH, new XYZ(cx, cy, cz), dis);
        }

        private static void UpdateOpening(
            FamilyInstance inst, XYZ pt, double w, double h,
            Wall wall, string wallClass, string dis)
        {
            SetParam(inst, P_WIDTH,       w * FT_TO_MM);
            SetParam(inst, P_HEIGHT,      h * FT_TO_MM);
            SetParam(inst, P_DUVAR_SINIF, wallClass);
            SetParam(inst, P_DISIPLIN,    dis);
            SetParamStr(inst, P_SON_GUN,  DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
        }

        private static (double wMm, double hMm) GetBoslukBoyutlari(FamilyInstance b)
        {
            double w = GetParamMm(b, P_WIDTH);
            double h = GetParamMm(b, P_HEIGHT);
            // BoundingBox fallback
            if (w <= 0 || h <= 0)
            {
                var bb = b.get_BoundingBox(null);
                if (bb != null)
                {
                    double dx = Math.Abs(bb.Max.X - bb.Min.X) * FT_TO_MM;
                    double dy = Math.Abs(bb.Max.Y - bb.Min.Y) * FT_TO_MM;
                    double dz = Math.Abs(bb.Max.Z - bb.Min.Z) * FT_TO_MM;
                    if (w <= 0) w = Math.Max(dx, dy);
                    if (h <= 0) h = dz;
                }
            }
            return (w, h);
        }

        private static double GetParamMm(Element e, string name)
        {
            var p = e.LookupParameter(name);
            if (p == null || !p.HasValue) return 0;
            if (p.StorageType == StorageType.Double)
            {
                double v = p.AsDouble();
                // feet veya mm ayrımı
                if (v > 0 && v < 50) return v * FT_TO_MM;
                if (v >= 50)         return v;
            }
            return 0;
        }

        private static string? GetParam(Element e, string name)
        {
            var p = e.LookupParameter(name);
            return p?.HasValue == true ? p.AsString() : null;
        }

        private static void SetParam(FamilyInstance e, string name, object val)
        {
            var p = e.LookupParameter(name);
            if (p == null || p.IsReadOnly) return;
            if (val is string s && p.StorageType == StorageType.String)
                p.Set(s);
            else if (val is double d && p.StorageType == StorageType.Double)
                p.Set(d);
        }

        private static void SetParamStr(Element e, string name, string val)
        {
            var p = e.LookupParameter(name);
            if (p?.IsReadOnly == false && p.StorageType == StorageType.String)
                p.Set(val);
        }

        private static List<Outline> CollectMepOutlines(Document doc)
        {
            var cats = new[]
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_CableTray,
            };
            return cats.SelectMany(cat =>
                new FilteredElementCollector(doc)
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .ToList()
                    .Select(el => el.get_BoundingBox(null))
                    .Where(bb => bb != null)
                    .Select(bb => new Outline(bb!.Min, bb!.Max))
            ).ToList();
        }

        private static FamilySymbol? FindOpeningFamilySymbol(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.FamilyName
                    .IndexOf(FAM_NAME, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static FamilySymbol? FindLintelSymbol(Document doc, string famIsim)
        {
            var syms = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .ToList();
            if (!string.IsNullOrEmpty(famIsim))
                return syms.FirstOrDefault(s =>
                    s.FamilyName.IndexOf(famIsim, StringComparison.OrdinalIgnoreCase) >= 0);
            // İlk bulunan
            return syms.FirstOrDefault();
        }

        private static (XYZ? origin, XYZ? yon) GetLintelPlacement(
            FamilyInstance b, Wall wall, double bosUstZ)
        {
            var bb = b.get_BoundingBox(null);
            if (bb == null) return (null, null);
            double cx = (bb.Min.X + bb.Max.X) / 2;
            double cy = (bb.Min.Y + bb.Max.Y) / 2;

            // Duvar ekseni yönü
            if (wall.Location is not LocationCurve lc) return (null, null);
            var p0 = lc.Curve.GetEndPoint(0);
            var p1 = lc.Curve.GetEndPoint(1);
            double dx = p1.X - p0.X, dy = p1.Y - p0.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return (null, null);
            var yon = new XYZ(dx / len, dy / len, 0);

            return (new XYZ(cx, cy, bosUstZ), yon);
        }

        private static int LintelBarDiam(string betonSin, double wMm)
        {
            // EC-2 bazlı: C30+ → Ø20 (geniş boşlukta), diğer → Ø16
            bool guclu = betonSin is "C30" or "C35" or "C40" or "C45" or "C50";
            if (guclu) return wMm > 900 ? 20 : 16;
            return wMm > 1200 ? 20 : 16;
        }

        private static Wall? FindNearestWall(Document doc, FamilyInstance b)
        {
            var bb = b.get_BoundingBox(null);
            if (bb == null) return null;
            var ctr = new XYZ(
                (bb.Min.X + bb.Max.X) / 2,
                (bb.Min.Y + bb.Max.Y) / 2,
                (bb.Min.Z + bb.Max.Z) / 2);

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.Location is LocationCurve)
                .OrderBy(w =>
                {
                    try
                    {
                        var proj = ((LocationCurve)w.Location).Curve.Project(ctr);
                        return proj?.XYZPoint.DistanceTo(ctr) ?? double.MaxValue;
                    }
                    catch { return double.MaxValue; }
                })
                .FirstOrDefault(w =>
                {
                    try
                    {
                        var proj = ((LocationCurve)w.Location).Curve.Project(ctr);
                        return proj != null && proj.XYZPoint.DistanceTo(ctr) < 6.56;
                    }
                    catch { return false; }
                });
        }

        private static Level? FindNearestLevel(Document doc, double zFt)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(lv => Math.Abs(lv.Elevation - zFt))
                .FirstOrDefault();

        private static string GetIfcGuid(Document doc, Element e)
        {
            try { return ExportUtils.GetExportId(doc, e.Id).ToString(); }
            catch { return Guid.NewGuid().ToString(); }
        }

        private static string XmlEscape(string s)
            => System.Security.SecurityElement.Escape(s) ?? s;

        private static void CreateZipArchive(string sourceDir, string zipPath)
        {
            // .NET 4.5+ System.IO.Compression
            System.IO.Compression.ZipFile.CreateFromDirectory(
                sourceDir, zipPath,
                System.IO.Compression.CompressionLevel.Optimal, false);
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
