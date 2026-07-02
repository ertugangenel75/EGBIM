using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO — Elektrik Devre Değişiklik Takibi (ElecCircuitDiffOps) — v10
    ///
    /// Çözülen alan-sıkıntısı (Autodesk Revit Ideas, MEP topluluğu, 10 yıldır açık):
    ///   "Elektrikçiye hangi spesifik devrelerin kaldırılması veya farklı bir yük için
    ///    değiştirilmesi gerektiğini gösterememe." Revit, tasarım revizyonları arasında
    ///    devre bazında net bir DELTA (eklendi/silindi/değişti) üretmiyor; saha ekibi
    ///    iki revizyonu elle karşılaştırmak zorunda kalıyor.
    ///
    /// EGBIMOTO çözümü — iki op:
    ///   elec_circuit_snapshot — modeldeki tüm elektrik devrelerinin mevcut durumunu
    ///                           (pano, devre no, yük VA, kutup, gerilim, frame/rating,
    ///                            bağlı eleman UniqueId listesi) JSON snapshot'a yazar.
    ///                           Bu "onaylı tasarım ani"dır (referans).
    ///   elec_circuit_diff     — mevcut modeli önceki snapshot ile karşılaştırır ve
    ///                           saha için delta üretir: EKLENEN / SILINEN / DEGISEN
    ///                           devreler + hangi alanın değiştiği (yük, panel, kutup...).
    ///
    /// NOT — Revit API kısıtı (bilinçli kapsam): Wire size / panel schedule hesaplı
    /// alanları Revit'te READ-ONLY'dir; eklenti bunları YAZAMAZ. Bu op'lar yazma değil,
    /// DEĞİŞİKLİK RAPORLAMA çözer — saha ekibine "neyi değiştir" listesi verir.
    /// Snapshot okuma tarafı (UniqueId) kullanır; böylece eleman ID'leri worksharing/
    /// senkron sonrası kaysa bile eşleştirme dayanıklıdır.
    ///
    /// Okunan alanlar ElectricalSystem'den: BaseEquipment (pano), CircuitNumber,
    /// ApparentLoad (VA), PolesNumber, Voltage, Rating, Name, Elements (bağlı elemanlar).
    /// </summary>
    public static class ElecCircuitDiffOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // E01  elec_circuit_snapshot
        //
        // input : (yok — model taranır) veya List<Element> (ElectricalSystem) opsiyonel
        // params: output_path   String  zorunlu  — snapshot JSON yolu
        //         panel_filter  String  default="" — yalnız bu pano adını içeren devreler
        //
        // output: List<Dict> {circuit_id, panel, circuit_number, load_va, poles, status}
        //         + JSON dosyası output_path'e yazılır.
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_circuit_snapshot",
            RequiresTransaction = false,
            Description =
                "Modeldeki tum elektrik devrelerinin mevcut durumunu JSON snapshot'a yazar.\n" +
                "Onayli tasarim 'ani' olarak kullanilir; sonra elec_circuit_diff ile karsilastirilir.\n\n" +
                "params:\n" +
                "  output_path   — snapshot JSON yolu (zorunlu)\n" +
                "  panel_filter  — yalniz bu pano adini iceren devreler (opsiyonel)\n\n" +
                "Okunan: panel, devre no, yuk VA, kutup, gerilim, rating, bagli eleman UniqueId'leri.\n" +
                "Cikti: circuit_id, panel, circuit_number, load_va, poles, status (+ JSON dosyasi).",
            Category = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> CircuitSnapshot(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            var outputPath = ctx.RequireString("output_path");
            var panelFilter= ctx.GetString("panel_filter", "");

            var circuits = CollectCircuits(ctx, doc, panelFilter);
            if (circuits.Count == 0)
            {
                ctx.Log("  elec_circuit_snapshot: model'de elektrik devresi bulunamadi");
                return ErrRow("Elektrik devresi bulunamadi.");
            }

            var snapshot = new List<CircuitRecord>();
            var rows = new List<Dictionary<string, object?>>();

            foreach (var es in circuits)
            {
                var rec = CircuitRecord.From(es);
                snapshot.Add(rec);
                rows.Add(new Dictionary<string, object?>
                {
                    ["circuit_id"]     = rec.CircuitUid,
                    ["panel"]          = rec.Panel,
                    ["circuit_number"] = rec.CircuitNumber,
                    ["load_va"]        = rec.LoadVa,
                    ["poles"]          = rec.Poles,
                    ["status"]         = "OK",
                });
            }

            // JSON yaz (snapshot meta + kayıtlar)
            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var payload = new SnapshotFile
                {
                    SchemaVersion = 1,
                    ProjectName   = doc.Title,
                    CreatedAt     = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    CircuitCount  = snapshot.Count,
                    Circuits      = snapshot,
                };
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, opts), Encoding.UTF8);
                ctx.Log($"  elec_circuit_snapshot: {snapshot.Count} devre → {outputPath}");
            }
            catch (Exception ex)
            {
                ctx.Log($"  elec_circuit_snapshot: dosya yazilamadi — {ex.Message}");
                return ErrRow($"Snapshot yazilamadi: {ex.Message}");
            }

            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // E02  elec_circuit_diff
        //
        // input : (yok — mevcut model taranır)
        // params: baseline_path   String  zorunlu — karşılaştırılacak snapshot JSON
        //         output_path     String  default="" — delta raporu JSON/HTML yolu (boşsa yazılmaz)
        //         panel_filter    String  default=""
        //         load_tolerance_va Double default=1 — VA farkı bu eşiğin altındaysa "değişmedi" say
        //
        // output: List<Dict> — her satır bir değişiklik:
        //   change_type (EKLENDI|SILINDI|DEGISTI), panel, circuit_number,
        //   field, old_value, new_value, detail
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_circuit_diff",
            RequiresTransaction = false,
            Description =
                "Mevcut modeli onceki devre snapshot'i ile karsilastirir, saha icin delta uretir:\n" +
                "EKLENEN / SILINEN / DEGISEN devreler ve hangi alanin degistigi.\n\n" +
                "params:\n" +
                "  baseline_path     — karsilastirilacak snapshot JSON (zorunlu)\n" +
                "  output_path       — delta raporu yolu, .json veya .html (opsiyonel)\n" +
                "  panel_filter      — yalniz bu panoyu iceren devreler (opsiyonel)\n" +
                "  load_tolerance_va — VA farki bu esigin altindaysa degismedi say (default: 1)\n\n" +
                "Devreler UniqueId ile eslestirilir (worksharing-dayanikli).\n" +
                "Cikti: change_type, panel, circuit_number, field, old_value, new_value, detail.",
            Category = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> CircuitDiff(OpContext ctx)
        {
            var rctx         = RequireRevit(ctx);
            var doc          = rctx.Doc;
            var baselinePath = ctx.RequireString("baseline_path");
            var outputPath   = ctx.GetString("output_path", "");
            var panelFilter  = ctx.GetString("panel_filter", "");
            double loadTol   = ctx.GetDouble("load_tolerance_va", 1.0);

            // 1) Baseline yükle
            SnapshotFile? baseline;
            try
            {
                if (!File.Exists(baselinePath))
                {
                    ctx.Log($"  elec_circuit_diff: baseline bulunamadi: {baselinePath}");
                    return ErrRow($"Baseline snapshot bulunamadi: {baselinePath}");
                }
                var json = File.ReadAllText(baselinePath, Encoding.UTF8);
                baseline = JsonSerializer.Deserialize<SnapshotFile>(json);
            }
            catch (Exception ex)
            {
                ctx.Log($"  elec_circuit_diff: baseline okunamadi — {ex.Message}");
                return ErrRow($"Baseline okunamadi: {ex.Message}");
            }
            if (baseline?.Circuits == null)
                return ErrRow("Baseline snapshot bos/gecersiz.");

            // 2) Mevcut durumu çıkar
            var current = CollectCircuits(ctx, doc, panelFilter)
                .Select(CircuitRecord.From)
                .ToList();

            // 3) UniqueId ile eşleştir
            var baseMap = baseline.Circuits
                .GroupBy(c => c.CircuitUid)
                .ToDictionary(g => g.Key, g => g.First());
            var curMap = current
                .GroupBy(c => c.CircuitUid)
                .ToDictionary(g => g.Key, g => g.First());

            var rows = new List<Dictionary<string, object?>>();

            // SILINEN: baseline'da var, mevcutta yok
            foreach (var b in baseline.Circuits)
            {
                if (!curMap.ContainsKey(b.CircuitUid))
                    rows.Add(ChangeRow("SILINDI", b.Panel, b.CircuitNumber,
                        "circuit", $"{b.Panel}/{b.CircuitNumber} ({b.LoadVa:0} VA)", "—",
                        "Devre silinmis veya devre bag kaldirilmis"));
            }

            // EKLENEN: mevcutta var, baseline'da yok
            foreach (var c in current)
            {
                if (!baseMap.ContainsKey(c.CircuitUid))
                    rows.Add(ChangeRow("EKLENDI", c.Panel, c.CircuitNumber,
                        "circuit", "—", $"{c.Panel}/{c.CircuitNumber} ({c.LoadVa:0} VA)",
                        "Yeni devre eklenmis"));
            }

            // DEĞİŞEN: ikisinde de var ama alanlar farklı
            foreach (var c in current)
            {
                if (!baseMap.TryGetValue(c.CircuitUid, out var b)) continue;

                // Panel değişti
                if (!StrEq(b.Panel, c.Panel))
                    rows.Add(ChangeRow("DEGISTI", c.Panel, c.CircuitNumber,
                        "panel", b.Panel, c.Panel, "Devre baska panele tasinmis"));

                // Devre no değişti
                if (!StrEq(b.CircuitNumber, c.CircuitNumber))
                    rows.Add(ChangeRow("DEGISTI", c.Panel, c.CircuitNumber,
                        "circuit_number", b.CircuitNumber, c.CircuitNumber, "Devre numarasi degismis"));

                // Yük değişti (tolerans dışı)
                if (Math.Abs(b.LoadVa - c.LoadVa) > loadTol)
                {
                    string dir = c.LoadVa > b.LoadVa ? "artmis" : "azalmis";
                    rows.Add(ChangeRow("DEGISTI", c.Panel, c.CircuitNumber,
                        "load_va", b.LoadVa.ToString("0.#"), c.LoadVa.ToString("0.#"),
                        $"Yuk {dir} → iletken/koruma yeniden degerlendirilmeli"));
                }

                // Kutup değişti (tek↔üç faz → iletken sayısı değişir)
                if (b.Poles != c.Poles)
                    rows.Add(ChangeRow("DEGISTI", c.Panel, c.CircuitNumber,
                        "poles", b.Poles.ToString(), c.Poles.ToString(),
                        "Kutup sayisi degismis → kablo kesiti/faz duzeni etkilenir"));

                // Gerilim değişti
                if (!StrEq(b.Voltage, c.Voltage))
                    rows.Add(ChangeRow("DEGISTI", c.Panel, c.CircuitNumber,
                        "voltage", b.Voltage, c.Voltage, "Gerilim degismis"));

                // Bağlı eleman kümesi değişti (eklenen/çıkan cihaz)
                var bSet = new HashSet<string>(b.ElementUids ?? new List<string>());
                var cSet = new HashSet<string>(c.ElementUids ?? new List<string>());
                int added   = cSet.Except(bSet).Count();
                int removed = bSet.Except(cSet).Count();
                if (added > 0 || removed > 0)
                    rows.Add(ChangeRow("DEGISTI", c.Panel, c.CircuitNumber,
                        "elements", $"{bSet.Count} cihaz", $"{cSet.Count} cihaz",
                        $"Devredeki cihazlar degismis (+{added} / -{removed})"));
            }

            // Hiç değişiklik yoksa bilgi satırı
            if (rows.Count == 0)
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["change_type"]    = "DEGISIM_YOK",
                    ["panel"]          = "",
                    ["circuit_number"] = "",
                    ["field"]          = "",
                    ["old_value"]      = "",
                    ["new_value"]      = "",
                    ["detail"]         = $"Baseline ile fark yok ({baseline.CircuitCount} devre karsilastirildi)",
                });
            }

            int changeCount = rows.Count(r => (string?)r["change_type"] != "DEGISIM_YOK");
            ctx.Log($"  elec_circuit_diff: {changeCount} degisiklik " +
                    $"(baseline: {baseline.CircuitCount}, mevcut: {current.Count})");

            // Opsiyonel rapor yaz
            if (!string.IsNullOrEmpty(outputPath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    if (outputPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                        File.WriteAllText(outputPath, BuildHtml(rows, baseline, current.Count), Encoding.UTF8);
                    else
                        File.WriteAllText(outputPath,
                            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }),
                            Encoding.UTF8);
                    ctx.Log($"  elec_circuit_diff: rapor → {outputPath}");
                }
                catch (Exception ex)
                {
                    ctx.Log($"  elec_circuit_diff: rapor yazilamadi — {ex.Message}");
                }
            }

            return rows;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Yardımcılar
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Modeldeki (veya input'taki) elektrik devrelerini panel filtresiyle toplar.</summary>
        private static List<ElectricalSystem> CollectCircuits(OpContext ctx, Document doc, string panelFilter)
        {
            var input = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .OfType<ElectricalSystem>().ToList();

            var all = input.Count > 0
                ? input
                : new FilteredElementCollector(doc)
                    .OfClass(typeof(ElectricalSystem))
                    .Cast<ElectricalSystem>()
                    .ToList();

            // Power devrelerine odak (data/telefon/yangın sistemleri hariç tutulabilir)
            // SystemType erişimi sürüm-bağımsızlık için try ile.
            var filtered = new List<ElectricalSystem>();
            foreach (var es in all)
            {
                if (!string.IsNullOrEmpty(panelFilter))
                {
                    string panel = es.BaseEquipment?.Name ?? "";
                    if (panel.IndexOf(panelFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }
                filtered.Add(es);
            }
            return filtered;
        }

        private static bool StrEq(string? a, string? b)
            => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

        private static Dictionary<string, object?> ChangeRow(
            string type, string panel, string circuitNo,
            string field, string oldV, string newV, string detail)
            => new()
            {
                ["change_type"]    = type,
                ["panel"]          = panel,
                ["circuit_number"] = circuitNo,
                ["field"]          = field,
                ["old_value"]      = oldV,
                ["new_value"]      = newV,
                ["detail"]         = detail,
            };

        private static List<Dictionary<string, object?>> ErrRow(string msg)
            => new() { new() { ["change_type"] = "HATA", ["detail"] = msg } };

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit baglami gerektirir.");

        private static string BuildHtml(List<Dictionary<string, object?>> rows,
                                        SnapshotFile baseline, int currentCount)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'>");
            sb.Append("<title>Elektrik Devre Degisiklik Raporu</title><style>");
            sb.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#222}");
            sb.Append("h1{font-size:20px}table{border-collapse:collapse;width:100%;margin-top:12px}");
            sb.Append("th,td{border:1px solid #ddd;padding:6px 10px;font-size:13px;text-align:left}");
            sb.Append("th{background:#f4f4f4}");
            sb.Append(".EKLENDI{background:#e6ffed}.SILINDI{background:#ffeef0}.DEGISTI{background:#fff5e6}");
            sb.Append("</style></head><body>");
            sb.Append("<h1>Elektrik Devre Degisiklik Raporu</h1>");
            sb.Append($"<p>Proje: {Esc(baseline.ProjectName)} | Baseline: {Esc(baseline.CreatedAt)} ");
            sb.Append($"({baseline.CircuitCount} devre) | Mevcut: {currentCount} devre</p>");
            sb.Append("<table><tr><th>Tip</th><th>Panel</th><th>Devre</th><th>Alan</th>");
            sb.Append("<th>Eski</th><th>Yeni</th><th>Aciklama</th></tr>");
            foreach (var r in rows)
            {
                string ct = (r.GetValueOrDefault("change_type")?.ToString()) ?? "";
                sb.Append($"<tr class='{Esc(ct)}'>");
                sb.Append($"<td>{Esc(ct)}</td>");
                sb.Append($"<td>{Esc(r.GetValueOrDefault("panel")?.ToString())}</td>");
                sb.Append($"<td>{Esc(r.GetValueOrDefault("circuit_number")?.ToString())}</td>");
                sb.Append($"<td>{Esc(r.GetValueOrDefault("field")?.ToString())}</td>");
                sb.Append($"<td>{Esc(r.GetValueOrDefault("old_value")?.ToString())}</td>");
                sb.Append($"<td>{Esc(r.GetValueOrDefault("new_value")?.ToString())}</td>");
                sb.Append($"<td>{Esc(r.GetValueOrDefault("detail")?.ToString())}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></body></html>");
            return sb.ToString();
        }

        private static string Esc(string? s)
            => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // ═════════════════════════════════════════════════════════════════════
        //  Snapshot veri modeli (JSON serileştirilebilir)
        // ═════════════════════════════════════════════════════════════════════

        public sealed class SnapshotFile
        {
            public int SchemaVersion { get; set; } = 1;
            public string ProjectName { get; set; } = "";
            public string CreatedAt { get; set; } = "";
            public int CircuitCount { get; set; }
            public List<CircuitRecord> Circuits { get; set; } = new();
        }

        public sealed class CircuitRecord
        {
            public string CircuitUid { get; set; } = "";
            public string Panel { get; set; } = "";
            public string CircuitNumber { get; set; } = "";
            public double LoadVa { get; set; }
            public int Poles { get; set; }
            public string Voltage { get; set; } = "";
            public List<string> ElementUids { get; set; } = new();

            public static CircuitRecord From(ElectricalSystem es)
            {
                var rec = new CircuitRecord
                {
                    CircuitUid    = es.UniqueId,
                    Panel         = Safe(() => es.BaseEquipment?.Name) ?? "",
                    CircuitNumber = Safe(() => es.CircuitNumber) ?? "",
                    LoadVa        = Safe(() => es.ApparentLoad, 0.0),
                    Poles         = Safe(() => es.PolesNumber, 0),
                    // Revit ic voltage birimi standart degil (TBC/Jeremy): gercek voltun
                    // ~10.764 kati. Volt'a cevirmek icin x 0.3048^2. Diff zaten tutarli
                    // olurdu ama rapor okunabilirligi icin Volt'a ceviriyoruz.
                    Voltage       = Safe(() => (es.Voltage * 0.3048 * 0.3048).ToString("0")) ?? "",
                };

                try
                {
                    var uids = new List<string>();
                    foreach (Element el in es.Elements)
                        uids.Add(el.UniqueId);
                    uids.Sort(StringComparer.Ordinal);
                    rec.ElementUids = uids;
                }
                catch { rec.ElementUids = new List<string>(); }

                return rec;
            }

            private static string? Safe(Func<string?> f)
            { try { return f(); } catch { return null; } }
            private static double Safe(Func<double> f, double dflt)
            { try { return f(); } catch { return dflt; } }
            private static int Safe(Func<int> f, int dflt)
            { try { return f(); } catch { return dflt; } }
        }
    }
}
