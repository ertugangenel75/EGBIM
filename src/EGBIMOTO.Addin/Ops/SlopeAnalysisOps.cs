using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO Eğim Analizi Operasyonları — v9
    ///
    /// Revit-API-Lab SlopeAnalysisHandler mantığından türetilmiştir.
    /// Döşeme / çatı / topografya yüzeylerinin eğim açısını hesaplar.
    ///
    /// Türk yapı pratiğinde kullanım alanları:
    ///   — Islak hacim zemin eğimi (min %1.5 → TS 825)
    ///   — Otopark rampası (max %15 → Otopark Yönetmeliği)
    ///   — Çatı drenajı (min %2 → TS 7263)
    ///   — Topografya analizi / yol eğimi
    ///   — Erişilebilirlik rampa kontrolü (max %8 → TS 9111)
    ///
    /// Op listesi:
    ///   slope_analysis         — Yüzeylerin eğimini hesaplar, isteğe bağlı renk override
    ///   slope_validate         — Eğim değerlerini min/max limitlerle karşılaştırır
    /// </summary>
    public static class SlopeAnalysisOps
    {
        // ── Türk standartları — eğim limitleri ───────────────────────────────

        /// <summary>Bilinen eğim limit profilleri — manifest'te limit_profile parametresiyle seçilir.</summary>
        private static readonly Dictionary<string, (double MinPct, double MaxPct, string Kural)>
            LimitProfiles = new(StringComparer.OrdinalIgnoreCase)
        {
            { "zemin_islak",   (1.5,  5.0,  "TS 825 / Islak Hacim Zemin Min %1.5") },
            { "cati_drenaj",   (2.0,  30.0, "TS 7263 / Çatı Min %2") },
            { "otopark_rampa", (0.5,  15.0, "Otopark Yönetmeliği / Max %15") },
            { "erisim_rampa",  (0.0,  8.33, "TS 9111 / Erişilebilirlik Rampası Max %8.33 (1/12)") },
            { "yol_boyuna",    (0.3,  12.0, "Karayolları / Boyuna Eğim Max %12") },
            { "yol_enine",     (1.5,  6.0,  "Karayolları / Enine Eğim %1.5–6") },
        };

        // ─────────────────────────────────────────────────────────────────────
        // OP 1: slope_analysis
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("slope_analysis",
            Description =
                "Döşeme / çatı / topografya yüzeylerinin eğim açısını hesaplar.\n" +
                "İsteğe bağlı olarak aktif görünümde renk override uygular.\n\n" +
                "params:\n" +
                "  categories     — kontrol edilecek kategoriler (opsiyonel)\n" +
                "                   [Floors, Roofs, Topography] — default: hepsi\n" +
                "  unit           — Degrees | Percentage | Radians (default: Percentage)\n" +
                "  apply_color    — görünümde renk override uygula (default: false)\n" +
                "  color_ranges   — eğim eşiği → renk listesi (opsiyonel)\n" +
                "                   [{threshold:2, r:0, g:200, b:0}, {threshold:8, r:255, g:165, b:0}]\n" +
                "  face_sample_uv — face normal örnekleme noktası UV (default: 0.5)\n\n" +
                "Input: yok (tüm modeli tarar) veya List<Element>.\n" +
                "Çıktı: List<Dictionary>\n" +
                "  element_id, kategori, kat, egim_derece, egim_pct,\n" +
                "  maks_egim_derece, maks_egim_pct, yuz_adet, alan_m2",
            Category = "Analiz")]
        public static List<Dictionary<string, object?>> SlopeAnalysis(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] slope_analysis Revit bağlamı gerektirir.");
            var doc = rctx.Doc;

            // ── Parametreler ─────────────────────────────────────────────────
            var categoryNames = ctx.GetList<string>("categories");
            var unit          = ctx.GetString("unit", "Percentage");
            bool applyColor   = ctx.GetBool("apply_color", false);
            double sampleUv   = ctx.GetDouble("face_sample_uv", 0.5);

            var colorRanges   = ParseColorRanges(ctx);

            // ── Hedef kategoriler ─────────────────────────────────────────────
            var builtInCats = ResolveCategories(categoryNames);

            // ── Elemanlar ─────────────────────────────────────────────────────
            List<Element> elements;
            if (ctx.Input is List<Element> inputList && inputList.Any())
            {
                elements = inputList;
            }
            else
            {
                var filter = new ElementMulticategoryFilter(builtInCats);
                elements = new FilteredElementCollector(doc)
                    .WherePasses(filter)
                    .WhereElementIsNotElementType()
                    .ToList();
            }

            ctx.Log($"  → {elements.Count} eleman eğim analizi yapılıyor");

            // ── Solid fill pattern (renk için) ────────────────────────────────
            FillPatternElement? solidFill = null;
            if (applyColor)
            {
                solidFill = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);
            }

            var opts = new Options { DetailLevel = ViewDetailLevel.Fine };
            var results = new List<Dictionary<string, object?>>();

            // ── Transaction (sadece renk override için gerekli) ───────────────
            Transaction? tx = null;
            if (applyColor)
            {
                tx = new Transaction(doc, "EGBIMOTO: Eğim Renk Override");
                tx.Start();
            }

            try
            {
                foreach (var elem in elements)
                {
                    try
                    {
                        var (maxDeg, maxPct, faceCount, totalAreaM2) =
                            ComputeMaxSlope(elem, opts, sampleUv);

                        double egimDeger = unit.ToUpperInvariant() switch
                        {
                            "DEGREES"    => maxDeg,
                            "RADIANS"    => maxDeg * Math.PI / 180.0,
                            _            => maxPct,    // Percentage (default)
                        };

                        var level = doc.GetElement(elem.LevelId) as Level;

                        results.Add(new Dictionary<string, object?>
                        {
                            ["element_id"]      = elem.Id.Value,
                            ["kategori"]        = elem.Category?.Name ?? "?",
                            ["kat"]             = level?.Name ?? "—",
                            ["egim_derece"]     = Math.Round(maxDeg, 2),
                            ["egim_pct"]        = Math.Round(maxPct, 2),
                            ["egim_birim"]      = egimDeger,
                            ["yuz_adet"]        = faceCount,
                            ["alan_m2"]         = Math.Round(totalAreaM2, 3),
                        });

                        // Renk override uygula
                        if (applyColor && solidFill != null && colorRanges.Any())
                        {
                            var color = ResolveColor(maxPct, colorRanges);
                            if (color != null)
                                ApplyColorOverride(doc, elem, color.Value, solidFill.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.Log($"  ✗ [{elem.Id}] eğim hesap hatası: {ex.Message}");
                    }
                }

                if (applyColor) tx?.Commit();
            }
            catch
            {
                if (applyColor) tx?.RollBack();
                throw;
            }
            finally
            {
                tx?.Dispose();
            }

            int sifirEgim  = results.Count(r => (double)(r["egim_pct"] ?? 0) < 0.1);
            double maksEgim = results.Any()
                ? results.Max(r => (double)(r["egim_pct"] ?? 0)) : 0;

            ctx.Log($"  → {results.Count} eleman, maks eğim: {maksEgim:F1}%, düz (<%0.1): {sifirEgim}");
            return results;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2: slope_validate
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("slope_validate",
            Description =
                "Eğim analizi sonuçlarını Türk standartlarına göre doğrular.\n\n" +
                "params:\n" +
                "  limit_profile  — hazır limit profili (opsiyonel)\n" +
                "                   zemin_islak | cati_drenaj | otopark_rampa |\n" +
                "                   erisim_rampa | yol_boyuna | yol_enine\n" +
                "  min_pct        — minimum eğim %  (limit_profile yoksa zorunlu)\n" +
                "  max_pct        — maksimum eğim % (limit_profile yoksa zorunlu)\n" +
                "  kural_adi      — raporda gösterilecek kural adı (opsiyonel)\n\n" +
                "Input: slope_analysis çıktısı (List<Dictionary>).\n" +
                "Çıktı: List<Dictionary> — sadece ihlal eden elemanlar\n" +
                "  element_id, kategori, kat, egim_pct, min_limit, max_limit,\n" +
                "  sorun, kural, seviye",
            Category = "Analiz")]
        public static List<Dictionary<string, object?>> SlopeValidate(OpContext ctx)
        {
            var rows = ctx.InputAs<List<Dictionary<string, object?>>>();

            // ── Limit çözümle ─────────────────────────────────────────────────
            var profile    = ctx.GetString("limit_profile", "");
            double minPct, maxPct;
            string kural;

            if (!string.IsNullOrEmpty(profile) &&
                LimitProfiles.TryGetValue(profile, out var lp))
            {
                (minPct, maxPct, kural) = lp;
            }
            else
            {
                minPct = ctx.GetDouble("min_pct", 0);
                maxPct = ctx.GetDouble("max_pct", 100);
                kural  = ctx.GetString("kural_adi", $"min %{minPct} – max %{maxPct}");
            }

            ctx.Log($"  → {rows.Count} satır kontrol: {kural}");

            var ihlaller = new List<Dictionary<string, object?>>();

            foreach (var row in rows)
            {
                double egimPct = Convert.ToDouble(row.GetValueOrDefault("egim_pct") ?? 0);

                bool altIhlal = egimPct < minPct;
                bool ustIhlal = egimPct > maxPct;

                if (!altIhlal && !ustIhlal) continue;

                ihlaller.Add(new Dictionary<string, object?>
                {
                    ["element_id"]  = row.GetValueOrDefault("element_id"),
                    ["kategori"]    = row.GetValueOrDefault("kategori"),
                    ["kat"]         = row.GetValueOrDefault("kat"),
                    ["egim_pct"]    = Math.Round(egimPct, 2),
                    ["egim_derece"] = row.GetValueOrDefault("egim_derece"),
                    ["min_limit"]   = minPct,
                    ["max_limit"]   = maxPct,
                    ["sorun"]       = altIhlal
                        ? $"Eğim %{egimPct:F1} < min %{minPct} — yetersiz drenaj riski"
                        : $"Eğim %{egimPct:F1} > max %{maxPct} — limit aşımı",
                    ["kural"]       = kural,
                    ["seviye"]      = altIhlal ? "UYARI" : "HATA",
                });
            }

            int hata   = ihlaller.Count(i => i["seviye"]?.ToString() == "HATA");
            int uyari  = ihlaller.Count(i => i["seviye"]?.ToString() == "UYARI");
            ctx.Log($"  → {ihlaller.Count} ihlal: {hata} hata, {uyari} uyarı");
            return ihlaller;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı metodlar
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Elemanın tüm yüzlerini tarar, maksimum eğimi döner.
        /// Revit-API-Lab SlopeAnalysisHandler.GetMaxSlope() mantığından genişletilmiştir:
        ///   — Hem derece hem yüzde döner
        ///   — Yüz sayısı ve toplam alan da döner
        ///   — GeometryInstance (linked/in-place) iç geometriyi de tarar
        /// </summary>
        private static (double MaxDeg, double MaxPct, int FaceCount, double TotalAreaM2)
            ComputeMaxSlope(Element elem, Options opts, double sampleUv)
        {
            double maxDeg   = 0;
            int    faceCount  = 0;
            double totalAreaM2 = 0;
            double uvSafe   = Math.Max(0.01, Math.Min(0.99, sampleUv));

            var geo = elem.get_Geometry(opts);
            if (geo == null) return (0, 0, 0, 0);

            ProcessGeometry(geo, null, uvSafe, ref maxDeg, ref faceCount, ref totalAreaM2);

            double maxPct = Math.Tan(maxDeg * Math.PI / 180.0) * 100.0;
            return (maxDeg, maxPct, faceCount, totalAreaM2);
        }

        private static void ProcessGeometry(
            GeometryElement geo, Transform? xform, double uvSafe,
            ref double maxDeg, ref int faceCount, ref double totalAreaM2)
        {
            foreach (var obj in geo)
            {
                switch (obj)
                {
                    case Solid solid when solid.Volume > 1e-9:
                        foreach (Face face in solid.Faces)
                        {
                            try
                            {
                                XYZ normal = face.ComputeNormal(new UV(uvSafe, uvSafe));
                                if (xform != null)
                                    normal = xform.OfVector(normal).Normalize();

                                // Z ekseninden sapma açısı = eğim açısı
                                double cosZ  = Math.Abs(normal.Z);
                                cosZ         = Math.Max(-1, Math.Min(1, cosZ));
                                double angle = Math.Acos(cosZ) * (180.0 / Math.PI);

                                if (angle > maxDeg) maxDeg = angle;

                                // Yüzey alanı (m²)
                                try
                                {
                                    totalAreaM2 += UnitUtils.ConvertFromInternalUnits(
                                        face.Area, UnitTypeId.SquareMeters);
                                }
                                catch { }

                                faceCount++;
                            }
                            catch { }
                        }
                        break;

                    case GeometryInstance gi:
                        var nested     = gi.GetInstanceGeometry();
                        var nestedXfrm = xform == null
                            ? gi.Transform
                            : xform.Multiply(gi.Transform);
                        ProcessGeometry(nested, nestedXfrm, uvSafe,
                            ref maxDeg, ref faceCount, ref totalAreaM2);
                        break;
                }
            }
        }

        private static void ApplyColorOverride(
            Document doc, Element elem, (byte R, byte G, byte B) color,
            ElementId fillPatternId)
        {
            var ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternColor(new Color(color.R, color.G, color.B));
            ogs.SetSurfaceForegroundPatternId(fillPatternId);
            doc.ActiveView.SetElementOverrides(elem.Id, ogs);
        }

        private static (byte R, byte G, byte B)? ResolveColor(
            double pct,
            List<(double Threshold, byte R, byte G, byte B)> ranges)
        {
            // En yüksek eşiği aşmayan aralığı bul
            var match = ranges
                .OrderByDescending(r => r.Threshold)
                .FirstOrDefault(r => pct >= r.Threshold);

            if (match == default) return null;
            return (match.R, match.G, match.B);
        }

        private static List<(double Threshold, byte R, byte G, byte B)> ParseColorRanges(
            OpContext ctx)
        {
            var raw = ctx.GetList<Dictionary<string, object?>>("color_ranges");
            if (!raw.Any())
            {
                // Varsayılan renk skalası: yeşil→sarı→turuncu→kırmızı
                return new List<(double, byte, byte, byte)>
                {
                    (0,    0,   200, 0),    // yeşil   — düz/çok az
                    (2,    180, 220, 0),    // sarı-yeşil — hafif
                    (5,    255, 165, 0),    // turuncu — orta
                    (15,   255, 50,  0),    // kırmızı-turuncu — dik
                    (30,   200, 0,   0),    // kırmızı — çok dik
                };
            }

            return raw.Select(d => (
                Threshold: Convert.ToDouble(d.GetValueOrDefault("threshold") ?? 0),
                R: Convert.ToByte(d.GetValueOrDefault("r") ?? 128),
                G: Convert.ToByte(d.GetValueOrDefault("g") ?? 128),
                B: Convert.ToByte(d.GetValueOrDefault("b") ?? 128)
            )).OrderBy(r => r.Threshold).ToList();
        }

        private static List<BuiltInCategory> ResolveCategories(List<string> names)
        {
            if (!names.Any())
                return new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_Roofs,
                    BuiltInCategory.OST_Topography,
                };

            var map = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
            {
                { "Floors",     BuiltInCategory.OST_Floors },
                { "Roofs",      BuiltInCategory.OST_Roofs },
                { "Topography", BuiltInCategory.OST_Topography },
                { "Ramps",      BuiltInCategory.OST_Ramps },
                { "Stairs",     BuiltInCategory.OST_Stairs },
                { "Walls",      BuiltInCategory.OST_Walls },
            };

            return names
                .Where(map.ContainsKey)
                .Select(n => map[n])
                .Distinct()
                .ToList();
        }
    }
}
