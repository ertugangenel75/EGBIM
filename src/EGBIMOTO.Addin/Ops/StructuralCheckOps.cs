using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Rebar;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO Yapısal Doğrulama Operasyonları — v9
    ///
    /// TS 500 ve TBDY 2018 standartlarına göre yapısal parametre kontrolleri.
    /// Hesap motoru değil, BIM veri doğrulama motorudur.
    ///
    /// Op listesi:
    ///   structural_collect_all      — Tüm yapısal elementleri topla
    ///   structural_ts500_section    — TS500 minimum kesit kontrolü
    ///   structural_tbdy_params      — TBDY 2018 parametrelerini ata
    ///   structural_continuity_check — Kat geçişi süreklilik kontrolü
    ///   structural_level_summary    — Kat bazlı yapısal özet
    ///   structural_material_check   — Beton/çelik malzeme parametresi doğrulama
    /// </summary>
    public static class StructuralCheckOps
    {
        // TS 500 minimum boyutlar (mm)
        private const double MIN_KOLON_B     = 250;
        private const double MIN_KOLON_H     = 250;
        private const double MIN_KIRIŞ_B     = 200;
        private const double MIN_KIRIŞ_H     = 300;
        private const double MIN_PERDE_T     = 200;
        private const double MIN_DÖŞEME_T    = 120;

        // EGBIMOTO yapısal parametre isimleri
        private const string PARAM_BETON_SINIF = "EG_BetonSinif";
        private const string PARAM_CELIK_SINIF = "EG_CelikSinif";
        private const string PARAM_DEPREM_BOLGESI = "EG_DepremBolgesi";
        private const string PARAM_LOD             = "EG_LOD";
        private const string PARAM_TBDY_DUKTILITE  = "EG_TbdyDuktilite";

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("structural_collect_all",
            Description =
                "Tüm yapısal elementleri (kolon, kiriş, döşeme, perde, temel) toplar.\n" +
                "params: level (opsiyonel), categories (opsiyonel — filtre listesi).\n" +
                "Çıktı: List<Element>.",
            Category = "Yapısal")]
        public static List<Element> StructuralCollectAll(OpContext ctx)
        {
            var rctx       = (RevitOpContext)ctx;
            var doc        = rctx.Doc;
            var levelName  = ctx.GetString("level", "");
            var categories = ctx.GetList<string>("categories");

            var builtInCats = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_StructuralFoundation,
            };

            // Kategori filtresi
            if (categories.Any())
            {
                builtInCats = builtInCats
                    .Where(c => categories.Any(n =>
                        c.ToString().Contains(n, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var catFilter = new ElementMulticategoryFilter(builtInCats);
            var collector = new FilteredElementCollector(doc)
                .WherePasses(catFilter)
                .WhereElementIsNotElementType();

            // Level filtresi
            if (!string.IsNullOrEmpty(levelName))
            {
                var level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault(l => string.Equals(l.Name, levelName,
                                                       StringComparison.OrdinalIgnoreCase));
                if (level != null)
                    collector = collector.WherePasses(new ElementLevelFilter(level.Id));
            }

            var result = collector.ToElements().ToList();
            ctx.Log($"  → {result.Count} yapısal element toplandı");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("structural_ts500_section",
            Description =
                "TS 500 Madde 7 minimum kesit boyutlarını kontrol eder.\n" +
                "Kolon: min 25×25cm, Kiriş: min 20×30cm, Perde: min 20cm, Döşeme: min 12cm.\n" +
                "params: override_min_kolon_b, override_min_kolon_h (opsiyonel, mm).\n" +
                "Input: yapısal elementler (List<Element>).\n" +
                "Çıktı: List<Dictionary> — ihlal listesi.",
            Category = "Yapısal")]
        public static List<Dictionary<string, object?>> StructuralTs500Section(OpContext ctx)
        {
            var rctx     = (RevitOpContext)ctx;
            var doc      = rctx.Doc;
            var elements = ctx.InputAs<List<Element>>();

            double minKolonB = ctx.GetDouble("override_min_kolon_b", MIN_KOLON_B);
            double minKolonH = ctx.GetDouble("override_min_kolon_h", MIN_KOLON_H);

            var ihlaller = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                var catId = el.Category?.Id.Value ?? 0;
                var bb    = el.get_BoundingBox(null);
                if (bb == null) continue;

                double b = UnitUtils.ConvertFromInternalUnits(
                    bb.Max.X - bb.Min.X, UnitTypeId.Millimeters);
                double h = UnitUtils.ConvertFromInternalUnits(
                    bb.Max.Y - bb.Min.Y, UnitTypeId.Millimeters);
                double t = UnitUtils.ConvertFromInternalUnits(
                    bb.Max.Z - bb.Min.Z, UnitTypeId.Millimeters);

                string? sorun = null;
                string  kural = "";

                if (catId == (long)BuiltInCategory.OST_StructuralColumns)
                {
                    if (b < minKolonB || h < minKolonH)
                    {
                        sorun = $"Kolon {b:F0}×{h:F0}mm < min {minKolonB:F0}×{minKolonH:F0}mm";
                        kural = "TS500 Md.7.2";
                    }
                }
                else if (catId == (long)BuiltInCategory.OST_StructuralFraming)
                {
                    if (b < MIN_KIRIŞ_B || h < MIN_KIRIŞ_H)
                    {
                        sorun = $"Kiriş {b:F0}×{h:F0}mm < min {MIN_KIRIŞ_B:F0}×{MIN_KIRIŞ_H:F0}mm";
                        kural = "TS500 Md.8.1";
                    }
                }
                else if (catId == (long)BuiltInCategory.OST_Floors)
                {
                    if (t < MIN_DÖŞEME_T)
                    {
                        sorun = $"Döşeme kalınlığı {t:F0}mm < min {MIN_DÖŞEME_T:F0}mm";
                        kural = "TS500 Md.9.1";
                    }
                }
                else if (catId == (long)BuiltInCategory.OST_Walls)
                {
                    // Perde duvar kalınlığı = min boyut
                    double minDim = Math.Min(b, h);
                    if (minDim < MIN_PERDE_T)
                    {
                        sorun = $"Perde kalınlığı {minDim:F0}mm < min {MIN_PERDE_T:F0}mm";
                        kural = "TS500 Md.11.1";
                    }
                }

                if (sorun != null)
                    ihlaller.Add(new Dictionary<string, object?>
                    {
                        ["element_id"] = el.Id.Value,
                        ["kategori"]   = el.Category?.Name ?? "?",
                        ["sorun"]      = sorun,
                        ["kural"]      = kural,
                        ["seviye"]     = "HATA",
                    });
            }

            ctx.Log($"  → {ihlaller.Count} TS500 kesit ihlali ({elements.Count} element kontrol edildi)");
            return ihlaller;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("structural_tbdy_params",
            Description =
                "Yapısal elementlere TBDY 2018 parametrelerini toplu yazar.\n" +
                "params: deprem_bolgesi (zorunlu: DD-1..DD-4),\n" +
                "        duktilite_sinifi (opsiyonel: YSBD/YSK, default:YSBD),\n" +
                "        beton_sinif (opsiyonel: C25, C30 vb.),\n" +
                "        celik_sinif (opsiyonel: B420C, S235 vb.).\n" +
                "Input: yapısal elementler.\n" +
                "Çıktı: Dictionary — yazilan_count.",
            Category = "Yapısal",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> StructuralTbdyParams(OpContext ctx)
        {
            var rctx     = (RevitOpContext)ctx;
            var doc      = rctx.Doc;
            var elements = ctx.InputAs<List<Element>>();

            var depremBolgesi  = ctx.RequireString("deprem_bolgesi");
            var duktiliteSinifi = ctx.GetString("duktilite_sinifi", "YSBD");
            var betonSinif     = ctx.GetString("beton_sinif", "");
            var celikSinif     = ctx.GetString("celik_sinif", "");

            // DD-1..DD-4 validasyonu
            var gecerliDd = new[] { "DD-1", "DD-2", "DD-3", "DD-4" };
            if (!gecerliDd.Any(d => string.Equals(d, depremBolgesi, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(
                    $"[{ctx.CurrentStepId}] Geçersiz deprem_bolgesi '{depremBolgesi}'. " +
                    $"Geçerli: {string.Join(", ", gecerliDd)}");

            int yazilan = 0;
            using var tx = new Transaction(doc, "EGBIMOTO: TBDY 2018 Parametreleri");
            tx.Start();

            foreach (var el in elements)
            {
                WriteParam(el, PARAM_DEPREM_BOLGESI,  depremBolgesi.ToUpperInvariant());
                WriteParam(el, PARAM_TBDY_DUKTILITE,  duktiliteSinifi.ToUpperInvariant());
                if (!string.IsNullOrEmpty(betonSinif)) WriteParam(el, PARAM_BETON_SINIF, betonSinif);
                if (!string.IsNullOrEmpty(celikSinif)) WriteParam(el, PARAM_CELIK_SINIF, celikSinif);
                yazilan++;
            }

            tx.Commit();
            ctx.Log($"  → {yazilan} yapısal elemana TBDY 2018 parametreleri yazıldı");
            return new Dictionary<string, object?>
            {
                ["yazilan_count"]    = yazilan,
                ["deprem_bolgesi"]   = depremBolgesi,
                ["duktilite_sinifi"] = duktiliteSinifi,
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("structural_continuity_check",
            Description =
                "Kolonların kat geçişlerinde Z ekseninde sürekli olup olmadığını kontrol eder.\n" +
                "Her katın kolon pozisyonlarını üst katla karşılaştırır.\n" +
                "Input: collect_structural_columns çıktısı.\n" +
                "Çıktı: List<Dictionary> — süreklilik kırıkları.",
            Category = "Yapısal")]
        public static List<Dictionary<string, object?>> StructuralContinuityCheck(OpContext ctx)
        {
            var rctx     = (RevitOpContext)ctx;
            var doc      = rctx.Doc;
            var elements = ctx.InputAs<List<Element>>();
            var tolerans = ctx.GetDouble("tolerans_mm", 50.0); // mm

            double tolFt = UnitUtils.ConvertToInternalUnits(tolerans, UnitTypeId.Millimeters);

            // Kolonları kat bazında grupla
            var katGruplari = elements
                .Where(e => e.LevelId != ElementId.InvalidElementId)
                .GroupBy(e => e.LevelId.Value)
                .OrderBy(g => g.Key)
                .ToList();

            var ihlaller = new List<Dictionary<string, object?>>();

            for (int i = 0; i < katGruplari.Count - 1; i++)
            {
                var altKat  = katGruplari[i].ToList();
                var ustKat  = katGruplari[i + 1].ToList();
                var altKatIsmi = (doc.GetElement(Rv.MakeElementId(katGruplari[i].Key)) as Level)?.Name ?? "?";
                var ustKatIsmi = (doc.GetElement(Rv.MakeElementId(katGruplari[i + 1].Key)) as Level)?.Name ?? "?";

                foreach (var altKolon in altKat)
                {
                    var altBb = altKolon.get_BoundingBox(null);
                    if (altBb == null) continue;
                    var altMerkez = (altBb.Max + altBb.Min) / 2;

                    // Üst katta aynı pozisyonda kolon var mı?
                    bool bulundu = ustKat.Any(ustKolon =>
                    {
                        var ustBb = ustKolon.get_BoundingBox(null);
                        if (ustBb == null) return false;
                        var ustMerkez = (ustBb.Max + ustBb.Min) / 2;
                        return Math.Abs(altMerkez.X - ustMerkez.X) < tolFt &&
                               Math.Abs(altMerkez.Y - ustMerkez.Y) < tolFt;
                    });

                    if (!bulundu)
                        ihlaller.Add(new Dictionary<string, object?>
                        {
                            ["element_id"]  = altKolon.Id.Value,
                            ["alt_kat"]     = altKatIsmi,
                            ["ust_kat"]     = ustKatIsmi,
                            ["sorun"]       = $"Kolon {altKatIsmi} katında var, {ustKatIsmi} katında devam etmiyor",
                            ["seviye"]      = "UYARI",
                        });
                }
            }

            ctx.Log($"  → {ihlaller.Count} kolon süreksizliği tespit edildi");
            return ihlaller;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("structural_level_summary",
            Description =
                "Kat bazlı yapısal element özeti üretir.\n" +
                "Her kat için: kolon_adet, kiriş_adet, perde_m2, döşeme_m2.\n" +
                "Input: structural_collect_all çıktısı.\n" +
                "Çıktı: List<Dictionary>.",
            Category = "Yapısal")]
        public static List<Dictionary<string, object?>> StructuralLevelSummary(OpContext ctx)
        {
            var rctx     = (RevitOpContext)ctx;
            var doc      = rctx.Doc;
            var elements = ctx.InputAs<List<Element>>();

            var grouped = elements
                .Where(e => e.LevelId != ElementId.InvalidElementId)
                .GroupBy(e => e.LevelId.Value);

            var rows = new List<Dictionary<string, object?>>();

            foreach (var g in grouped.OrderBy(g => g.Key))
            {
                var level = doc.GetElement(Rv.MakeElementId(g.Key)) as Level;
                var elems = g.ToList();

                double perdeM2 = 0, dosemeM2 = 0;

                foreach (var el in elems)
                {
                    var areaParam = el.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam == null) continue;
                    double m2 = UnitUtils.ConvertFromInternalUnits(areaParam.AsDouble(), UnitTypeId.SquareMeters);
                    var catId = el.Category?.Id.Value ?? 0;
                    if (catId == (long)BuiltInCategory.OST_Walls)  perdeM2  += m2;
                    if (catId == (long)BuiltInCategory.OST_Floors)          dosemeM2 += m2;
                }

                rows.Add(new Dictionary<string, object?>
                {
                    ["kat"]           = level?.Name ?? $"LevelId:{g.Key}",
                    ["kolon_adet"]    = elems.Count(e => e.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns),
                    ["kiris_adet"]    = elems.Count(e => e.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming),
                    ["perde_m2"]      = Math.Round(perdeM2,  3),
                    ["doseme_m2"]     = Math.Round(dosemeM2, 3),
                    ["toplam_element"] = elems.Count,
                });
            }

            ctx.Log($"  → {rows.Count} kat yapısal özeti");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("structural_material_check",
            Description =
                "Yapısal elementlerin beton/çelik sınıf parametrelerinin dolu olup olmadığını kontrol eder.\n" +
                "params: beton_required (opsiyonel, default:true), celik_required (opsiyonel, default:false).\n" +
                "Input: yapısal elementler.\n" +
                "Çıktı: List<Dictionary> — eksik malzeme parametreli elementler.",
            Category = "Yapısal")]
        public static List<Dictionary<string, object?>> StructuralMaterialCheck(OpContext ctx)
        {
            var elements      = ctx.InputAs<List<Element>>();
            var betonRequired = ctx.GetBool("beton_required", true);
            var celikRequired = ctx.GetBool("celik_required", false);

            var eksikler = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                var sorunlar = new List<string>();

                if (betonRequired)
                {
                    var p = el.LookupParameter(PARAM_BETON_SINIF);
                    if (p == null || string.IsNullOrWhiteSpace(p.AsString()))
                        sorunlar.Add($"{PARAM_BETON_SINIF} boş");
                }
                if (celikRequired)
                {
                    var p = el.LookupParameter(PARAM_CELIK_SINIF);
                    if (p == null || string.IsNullOrWhiteSpace(p.AsString()))
                        sorunlar.Add($"{PARAM_CELIK_SINIF} boş");
                }

                if (sorunlar.Any())
                    eksikler.Add(new Dictionary<string, object?>
                    {
                        ["element_id"] = el.Id.Value,
                        ["kategori"]   = el.Category?.Name ?? "?",
                        ["sorunlar"]   = string.Join(", ", sorunlar),
                        ["seviye"]     = "UYARI",
                    });
            }

            ctx.Log($"  → {eksikler.Count} malzeme parametresi eksikliği");
            return eksikler;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static void WriteParam(Element el, string paramName, string value)
        {
            var p = el.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                p.Set(value);
        }
    }
}
