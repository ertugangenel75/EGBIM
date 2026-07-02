using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO Cephe Operasyonları — v9
    ///
    /// Curtain wall, cephe sistemi, panel yerleştirme ve derz işlemleri.
    /// TR BIM EG_CEPHE_* parametre namespace'i ile entegre çalışır.
    ///
    /// Op listesi:
    ///   collect_curtain_panels  — Curtain panel elementlerini topla
    ///   facade_system_params    — Cephe sistemine TR BIM parametrelerini ata
    ///   facade_panel_matrix     — Grid bazlı panel tipi eşleştirme tablosu oluştur
    ///   facade_joint_validate   — Derz genişlik ve tip doğrulama
    ///   facade_area_by_type     — Panel tipine göre alan metrajı
    ///   facade_opening_ratio    — Saydamlık oranı hesabı (pencere/cephe)
    ///   facade_u_value_check    — Enerji performansı U değeri kontrolü (TS 825)
    ///   facade_export_schedule  — Cephe metraj tablosu dışa aktar
    /// </summary>
    public static class FacadeOps
    {
        // ── Sabitler ─────────────────────────────────────────────────────────
        private const string PARAM_PANEL_TIP       = "EG_CephePanelTip";
        private const string PARAM_DERZ_GENISLIK   = "EG_CepheDerzGenislik";
        private const string PARAM_DERZ_TIP        = "EG_CepheDerzTip";
        private const string PARAM_U_DEGERI        = "EG_CepheUDegeri";
        private const string PARAM_SAYDAM_ALAN     = "EG_CepheSaydamAlan";
        private const string PARAM_OPAK_ALAN       = "EG_CepheOpakAlan";
        private const string PARAM_KAPLAMA_MALZEME = "EG_CepheKaplamaMalzeme";

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("collect_curtain_panels",
            Description =
                "Projedeki tüm curtain panel elementlerini toplar.\n" +
                "params: level (opsiyonel) — belirli kat, workset (opsiyonel) — çalışma seti filtresi.\n" +
                "Çıktı: List<Element> — Panel elementleri.",
            Category = "Cephe",
            RequiresTransaction = false)]
        public static List<Element> CollectCurtainPanels(OpContext ctx)
        {
            var rctx = (RevitOpContext)ctx;
            var doc  = rctx.Doc;

            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                .WhereElementIsNotElementType();

            // Level filtresi
            var levelName = ctx.GetString("level", "");
            if (!string.IsNullOrWhiteSpace(levelName))
            {
                var level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));
                if (level != null)
                    collector = collector.WherePasses(
                        new ElementLevelFilter(level.Id));
            }

            // Workset filtresi
            var worksetName = ctx.GetString("workset", "");
            if (!string.IsNullOrWhiteSpace(worksetName) && doc.IsWorkshared)
            {
                var ws = new FilteredWorksetCollector(doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .FirstOrDefault(w => string.Equals(w.Name, worksetName, StringComparison.OrdinalIgnoreCase));
                if (ws != null)
                    collector = collector.WherePasses(
                        new ElementWorksetFilter(ws.Id, false));
            }

            var result = collector.ToElements().ToList();
            ctx.Log($"  → {result.Count} curtain panel toplandı");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("facade_system_params",
            Description =
                "Cephe duvarlarına EGBIMOTO TR BIM parametrelerini toplu yazar.\n" +
                "params: panel_tip (zorunlu), derz_genislik_mm (opsiyonel, default:20),\n" +
                "        derz_tip (opsiyonel, default:Silikon), u_degeri (opsiyonel),\n" +
                "        kaplama_malzeme (opsiyonel).\n" +
                "Input: collect_curtain_panels çıktısı (List<Element>).\n" +
                "Çıktı: Dictionary — yazılan element sayısı ve log.",
            Category = "Cephe",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> FacadeSystemParams(OpContext ctx)
        {
            var rctx     = (RevitOpContext)ctx;
            var doc      = rctx.Doc;
            var elements = ctx.InputAs<List<Element>>();

            var panelTip      = ctx.RequireString("panel_tip");
            var derzGenislik  = ctx.GetDouble("derz_genislik_mm", 20.0).ToString("F1");
            var derzTip       = ctx.GetString("derz_tip", "Silikon");
            var uDegeri       = ctx.GetString("u_degeri", "");
            var kaplamaMalzeme = ctx.GetString("kaplama_malzeme", "");

            int yazilan = 0, hata = 0;

            using var tx = new Transaction(doc, "EGBIMOTO: Cephe Parametreleri");
            tx.Start();

            foreach (var el in elements)
            {
                try
                {
                    WriteIfExists(el, PARAM_PANEL_TIP,       panelTip);
                    WriteIfExists(el, PARAM_DERZ_GENISLIK,   derzGenislik);
                    WriteIfExists(el, PARAM_DERZ_TIP,        derzTip);
                    if (!string.IsNullOrEmpty(uDegeri))
                        WriteIfExists(el, PARAM_U_DEGERI, uDegeri);
                    if (!string.IsNullOrEmpty(kaplamaMalzeme))
                        WriteIfExists(el, PARAM_KAPLAMA_MALZEME, kaplamaMalzeme);
                    yazilan++;
                }
                catch (Exception ex)
                {
                    hata++;
                    ctx.Log($"  ✗ [{el.Id}] param yazma hatası: {ex.Message}");
                }
            }

            tx.Commit();

            ctx.Log($"  → {yazilan} element yazıldı, {hata} hata");
            return new Dictionary<string, object?>
            {
                ["yazilan_count"] = yazilan,
                ["hata_count"]    = hata,
                ["panel_tip"]     = panelTip,
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("facade_panel_matrix",
            Description =
                "Curtain panellerini tip ve kata göre gruplandırarak matris tablosu oluşturur.\n" +
                "Her satır: panel_id, tip, kat, alan_m2, u_degeri, malzeme.\n" +
                "Input: List<Element> — panel elementleri.\n" +
                "Çıktı: List<Dictionary> — panel matrisi.",
            Category = "Cephe")]
        public static List<Dictionary<string, object?>> FacadePanelMatrix(OpContext ctx)
        {
            var rctx     = (RevitOpContext)ctx;
            var doc      = rctx.Doc;
            var elements = ctx.InputAs<List<Element>>();

            var rows = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                try
                {
                    // Alan hesabı
                    double alanM2 = 0;
                    var areaParam = el.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null)
                        alanM2 = Math.Round(UnitUtils.ConvertFromInternalUnits(
                            areaParam.AsDouble(), UnitTypeId.SquareMeters), 4);

                    // Kat bilgisi
                    var level     = doc.GetElement(el.LevelId) as Level;
                    var levelName = level?.Name ?? "—";

                    // Tip adı
                    var typeEl   = doc.GetElement(el.GetTypeId());
                    var typeName = typeEl?.Name ?? "Tanımsız";

                    // TR BIM parametreleri
                    var panelTip  = ReadParam(el, PARAM_PANEL_TIP) ?? typeName;
                    var uDegeri   = ReadParam(el, PARAM_U_DEGERI)  ?? "";
                    var malzeme   = ReadParam(el, PARAM_KAPLAMA_MALZEME) ?? "";

                    rows.Add(new Dictionary<string, object?>
                    {
                        ["element_id"]  = el.Id.Value,
                        ["panel_tip"]   = panelTip,
                        ["type_name"]   = typeName,
                        ["kat"]         = levelName,
                        ["alan_m2"]     = alanM2,
                        ["u_degeri"]    = uDegeri,
                        ["malzeme"]     = malzeme,
                    });
                }
                catch (Exception ex)
                {
                    ctx.Log($"  ✗ Panel [{el.Id}] satır oluşturma hatası: {ex.Message}");
                }
            }

            ctx.Log($"  → {rows.Count} panel matrisi satırı oluşturuldu");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("facade_joint_validate",
            Description =
                "Curtain panellerin derz parametrelerini doğrular.\n" +
                "params: min_derz_mm (default:10), max_derz_mm (default:40).\n" +
                "Input: List<Element> — panel elementleri.\n" +
                "Çıktı: List<Dictionary> — hatalı panel kayıtları.",
            Category = "Cephe")]
        public static List<Dictionary<string, object?>> FacadeJointValidate(OpContext ctx)
        {
            var elements = ctx.InputAs<List<Element>>();
            var minDerz  = ctx.GetDouble("min_derz_mm", 10.0);
            var maxDerz  = ctx.GetDouble("max_derz_mm", 40.0);

            var hatalar = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                var derzStr = ReadParam(el, PARAM_DERZ_GENISLIK);
                if (string.IsNullOrEmpty(derzStr))
                {
                    hatalar.Add(new Dictionary<string, object?>
                    {
                        ["element_id"] = el.Id.Value,
                        ["sorun"]      = "EG_CepheDerzGenislik parametresi boş",
                        ["seviye"]     = "UYARI",
                    });
                    continue;
                }

                if (double.TryParse(derzStr, out var derzMm))
                {
                    if (derzMm < minDerz || derzMm > maxDerz)
                        hatalar.Add(new Dictionary<string, object?>
                        {
                            ["element_id"]  = el.Id.Value,
                            ["sorun"]       = $"Derz genişliği {derzMm}mm aralık dışı [{minDerz}-{maxDerz}]",
                            ["deger"]       = derzMm,
                            ["seviye"]      = "HATA",
                        });
                }
                else
                {
                    hatalar.Add(new Dictionary<string, object?>
                    {
                        ["element_id"] = el.Id.Value,
                        ["sorun"]      = $"EG_CepheDerzGenislik parse edilemedi: '{derzStr}'",
                        ["seviye"]     = "HATA",
                    });
                }
            }

            ctx.Log($"  → {hatalar.Count} derz hatası tespit edildi ({elements.Count} panel kontrol edildi)");
            return hatalar;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("facade_area_by_type",
            Description =
                "Panel tipine göre toplam cephe alanını hesaplar.\n" +
                "Input: facade_panel_matrix çıktısı (List<Dictionary>).\n" +
                "Çıktı: List<Dictionary> — tip, toplam_alan_m2, panel_adet.",
            Category = "Cephe")]
        public static List<Dictionary<string, object?>> FacadeAreaByType(OpContext ctx)
        {
            var rows = ctx.InputAs<List<Dictionary<string, object?>>>();

            var grouped = rows
                .GroupBy(r => r.TryGetValue("panel_tip", out var pt) ? pt?.ToString() ?? "?" : "?")
                .Select(g => new Dictionary<string, object?>
                {
                    ["panel_tip"]       = g.Key,
                    ["toplam_alan_m2"]  = Math.Round(g.Sum(r =>
                        r.TryGetValue("alan_m2", out var a) && a is double d ? d : 0), 3),
                    ["panel_adet"]      = g.Count(),
                })
                .OrderByDescending(r => (double)(r["toplam_alan_m2"] ?? 0))
                .ToList();

            var toplamAlan = grouped.Sum(r => (double)(r["toplam_alan_m2"] ?? 0));
            ctx.Log($"  → {grouped.Count} panel tipi, toplam {toplamAlan:F2} m² cephe alanı");
            return grouped;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("facade_opening_ratio",
            Description =
                "Cephe saydamlık oranını hesaplar (pencere alanı / toplam cephe alanı).\n" +
                "TS 825 enerji performansı için referans değer.\n" +
                "Input: collect_curtain_walls veya collect_walls çıktısı.\n" +
                "Çıktı: Dictionary — saydam_alan_m2, opak_alan_m2, saydamlik_orani.",
            Category = "Cephe")]
        public static Dictionary<string, object?> FacadeOpeningRatio(OpContext ctx)
        {
            var rctx     = (RevitOpContext)ctx;
            var doc      = rctx.Doc;
            var elements = ctx.InputAs<List<Element>>();

            double saydamAlan = 0, opakAlan = 0;

            foreach (var el in elements)
            {
                // Curtain duvar toplam alanı
                var totalParam = el.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (totalParam == null) continue;

                double totalM2 = UnitUtils.ConvertFromInternalUnits(
                    totalParam.AsDouble(), UnitTypeId.SquareMeters);

                // Açıklık (insert) alanı
                var insertIds = (el as HostObject)?.FindInserts(true, false, false, false)
                    ?? new List<ElementId>();

                double insertM2 = 0;
                foreach (var id in insertIds)
                {
                    var insert = doc.GetElement(id);
                    if (insert == null) continue;
                    var insertArea = insert.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (insertArea != null)
                        insertM2 += UnitUtils.ConvertFromInternalUnits(
                            insertArea.AsDouble(), UnitTypeId.SquareMeters);
                }

                saydamAlan += insertM2;
                opakAlan   += totalM2 - insertM2;
            }

            var toplamAlan = saydamAlan + opakAlan;
            var oran       = toplamAlan > 0 ? Math.Round(saydamAlan / toplamAlan, 4) : 0;

            ctx.Log($"  → Saydamlık oranı: {oran:P1} (saydam {saydamAlan:F2}m², opak {opakAlan:F2}m²)");

            return new Dictionary<string, object?>
            {
                ["saydam_alan_m2"]   = Math.Round(saydamAlan, 3),
                ["opak_alan_m2"]     = Math.Round(opakAlan, 3),
                ["toplam_alan_m2"]   = Math.Round(toplamAlan, 3),
                ["saydamlik_orani"]  = oran,
                ["ts825_uyari"]      = oran > 0.50
                    ? "UYARI: Saydamlık oranı %50 üzerinde — TS 825 enerji kontrolü gerekli"
                    : "OK",
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("facade_u_value_check",
            Description =
                "Cephe panellerinin U değerini TS 825 limitine göre kontrol eder.\n" +
                "params: max_u_value (default:1.8 W/m²K — TS 825 3. bölge pencere limiti).\n" +
                "Input: List<Element> — panel elementleri.\n" +
                "Çıktı: List<Dictionary> — U değeri aşımları.",
            Category = "Cephe")]
        public static List<Dictionary<string, object?>> FacadeUValueCheck(OpContext ctx)
        {
            var elements  = ctx.InputAs<List<Element>>();
            var maxU      = ctx.GetDouble("max_u_value", 1.8);

            var ihlaller = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                var uStr = ReadParam(el, PARAM_U_DEGERI);
                if (string.IsNullOrEmpty(uStr)) continue;

                if (double.TryParse(uStr.Replace(",", "."),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var uVal))
                {
                    if (uVal > maxU)
                        ihlaller.Add(new Dictionary<string, object?>
                        {
                            ["element_id"] = el.Id.Value,
                            ["u_degeri"]   = uVal,
                            ["limit"]      = maxU,
                            ["asim"]       = Math.Round(uVal - maxU, 3),
                            ["sorun"]      = $"U={uVal} > TS825 limiti {maxU} W/m²K",
                        });
                }
            }

            ctx.Log($"  → {ihlaller.Count} U değeri ihlali (limit: {maxU} W/m²K)");
            return ihlaller;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("facade_export_schedule",
            Description =
                "Cephe metraj tablosunu HTML + özet satırıyla dışa aktarır.\n" +
                "params: output_path (zorunlu), title (opsiyonel).\n" +
                "Input: facade_area_by_type çıktısı veya herhangi bir List<Dictionary>.\n" +
                "Çıktı: Dictionary — dosya yolu ve satır sayısı.",
            Category = "Cephe")]
        public static Dictionary<string, object?> FacadeExportSchedule(OpContext ctx)
        {
            var rows       = ctx.InputAs<List<Dictionary<string, object?>>>();
            var outputPath = ctx.RequireString("output_path");
            var title      = ctx.GetString("title", "EGBIMOTO Cephe Metraj Raporu");

            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'>");
            html.AppendLine($"<title>{title}</title>");
            html.AppendLine("<style>body{{font-family:Segoe UI,sans-serif;padding:20px;background:#1a1a2e;color:#eee;}}");
            html.AppendLine("table{{border-collapse:collapse;width:100%;}}");
            html.AppendLine("th{{background:#2d2d44;padding:10px;text-align:left;border:1px solid #444;}}");
            html.AppendLine("td{{padding:8px;border:1px solid #333;}}");
            html.AppendLine("tr:nth-child(even){{background:#222238;}}");
            html.AppendLine(".total{{background:#16213e;font-weight:bold;}}");
            html.AppendLine("h2{{color:#7ec8e3;}}</style></head><body>");
            html.AppendLine($"<h2>{title}</h2>");
            html.AppendLine($"<p>Oluşturma: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            html.AppendLine("<table>");

            // Başlık
            if (rows.Count > 0)
            {
                html.AppendLine("<tr>");
                foreach (var k in rows[0].Keys)
                    html.AppendLine($"<th>{k.Replace("_", " ").ToUpper()}</th>");
                html.AppendLine("</tr>");
            }

            // Satırlar
            double toplamAlan = 0;
            foreach (var row in rows)
            {
                html.AppendLine("<tr>");
                foreach (var v in row.Values)
                    html.AppendLine($"<td>{v?.ToString() ?? "—"}</td>");
                html.AppendLine("</tr>");
                if (row.TryGetValue("toplam_alan_m2", out var a) && a is double d)
                    toplamAlan += d;
            }

            // Toplam satırı
            html.AppendLine("<tr class='total'>");
            if (rows.Count > 0)
            {
                html.AppendLine($"<td colspan='{rows[0].Count - 2}'><strong>TOPLAM</strong></td>");
                html.AppendLine($"<td><strong>{toplamAlan:F3} m²</strong></td>");
                html.AppendLine("<td>—</td>");
            }
            html.AppendLine("</tr>");
            html.AppendLine("</table></body></html>");

            System.IO.File.WriteAllText(outputPath, html.ToString(), System.Text.Encoding.UTF8);

            ctx.Log($"  → Cephe metraj raporu: {outputPath} ({rows.Count} satır, {toplamAlan:F2} m²)");
            return new Dictionary<string, object?>
            {
                ["output_path"] = outputPath,
                ["row_count"]   = rows.Count,
                ["toplam_alan"] = Math.Round(toplamAlan, 3),
            };
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static void WriteIfExists(Element el, string paramName, string value)
        {
            var p = el.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly)
                p.Set(value);
        }

        private static string? ReadParam(Element el, string paramName)
        {
            var p = el.LookupParameter(paramName);
            return p?.AsString() ?? p?.AsValueString();
        }
    }
}
