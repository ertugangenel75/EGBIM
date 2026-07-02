using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EGBIMOTO.Core.Preview;

namespace EGBIMOTO.Core.Schedule
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — FourDFiveDDto  (v1.0)
    //
    //  4D/5D canlı önizleme için veri sözleşmesi.
    //  PreviewGeometryDto'nun geometry katmanını tekrar kullanır (meshes / edges / labels / bbox).
    //  Ek olarak:
    //    schedule_items → 4D (zaman + faz bilgisi, her mesh'e 1:1)
    //    cost_items     → 5D (poz + maliyet, her mesh'e 1:1)
    //
    //  JSON → 4d5d_viewer.html → Three.js zaman animasyonu + S-eğrisi
    //
    //  Kullanım akışı:
    //    1. schedule_collect_4d / schedule_collect_5d op → FourDFiveDDto üretir
    //    2. schedule_gate op → DagExecutor UserScheduleGateCallback çağırır
    //    3. Callback → FourDFiveDWindow.ShowModal(dto) → bool
    //    4. vars["s_gate"] = "confirmed" | "cancelled"
    //    5. Sonraki yazma adımları condition: "$s_gate == confirmed"
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class FourDFiveDDto
    {
        // ── Metadata ──────────────────────────────────────────────────────────

        [JsonPropertyName("operation_name")]
        public string OperationName { get; set; } = "4D/5D Önizleme";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("element_count")]
        public int ElementCount { get; set; }

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new();

        [JsonPropertyName("stats")]
        public Dictionary<string, string> Stats { get; set; } = new();

        // ── Proje zaman aralığı ───────────────────────────────────────────────

        /// <summary>ISO 8601 tarih — "2025-03-01"</summary>
        [JsonPropertyName("project_start")]
        public string ProjectStart { get; set; } = "";

        /// <summary>ISO 8601 tarih — "2025-12-31"</summary>
        [JsonPropertyName("project_end")]
        public string ProjectEnd { get; set; } = "";

        // ── 5D maliyet özeti ──────────────────────────────────────────────────

        [JsonPropertyName("total_cost")]
        public double TotalCost { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "₺";

        // ── Geometry (PreviewGeometryDto ile aynı yapı) ───────────────────────

        [JsonPropertyName("meshes")]
        public List<PreviewMesh> Meshes { get; set; } = new();

        [JsonPropertyName("edges")]
        public List<PreviewEdge> Edges { get; set; } = new();

        [JsonPropertyName("labels")]
        public List<PreviewLabel> Labels { get; set; } = new();

        [JsonPropertyName("bbox")]
        public PreviewBBox? BBox { get; set; }

        // ── 4D: Zaman programı ────────────────────────────────────────────────

        [JsonPropertyName("schedule_items")]
        public List<ScheduleItem> ScheduleItems { get; set; } = new();

        // ── 5D: Maliyet kalemleri ─────────────────────────────────────────────

        [JsonPropertyName("cost_items")]
        public List<CostItem5D> CostItems { get; set; } = new();

        // ── C# tarafı işaret (JSON'a dahil edilmez) ───────────────────────────

        [JsonIgnore]
        public bool Confirmed { get; set; } = false;
    }

    // ── 4D: Eleman program kaydı ──────────────────────────────────────────────

    public sealed class ScheduleItem
    {
        /// <summary>PreviewMesh.Id ile eşleşir.</summary>
        [JsonPropertyName("mesh_id")]
        public string MeshId { get; set; } = "";

        /// <summary>Revit ElementId string.</summary>
        [JsonPropertyName("element_id")]
        public string ElementId { get; set; } = "";

        [JsonPropertyName("task_name")]
        public string TaskName { get; set; } = "";

        [JsonPropertyName("wbs_code")]
        public string WbsCode { get; set; } = "";

        /// <summary>ISO 8601 — "2025-03-01"</summary>
        [JsonPropertyName("start_date")]
        public string StartDate { get; set; } = "";

        /// <summary>ISO 8601 — "2025-03-25"</summary>
        [JsonPropertyName("end_date")]
        public string EndDate { get; set; } = "";

        /// <summary>Faz adı — "Betonarme", "Duvar", "Çatı" vb.</summary>
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = "";

        /// <summary>0.0 – 1.0 tamamlanma oranı (varsayılan: 0)</summary>
        [JsonPropertyName("progress")]
        public double Progress { get; set; } = 0.0;

        /// <summary>Viewer'ın tamamlandı durumunda kullanacağı kategori rengi.</summary>
        [JsonPropertyName("original_color")]
        public string OriginalColor { get; set; } = "#4A90D9";
    }

    // ── 5D: Maliyet kalemi ────────────────────────────────────────────────────

    public sealed class CostItem5D
    {
        /// <summary>PreviewMesh.Id ile eşleşir.</summary>
        [JsonPropertyName("mesh_id")]
        public string MeshId { get; set; } = "";

        [JsonPropertyName("element_id")]
        public string ElementId { get; set; } = "";

        [JsonPropertyName("poz_no")]
        public string PozNo { get; set; } = "";

        [JsonPropertyName("poz_adi")]
        public string PozAdi { get; set; } = "";

        [JsonPropertyName("miktar")]
        public double Miktar { get; set; }

        [JsonPropertyName("birim")]
        public string Birim { get; set; } = "";

        [JsonPropertyName("birim_fiyat")]
        public double BirimFiyat { get; set; }

        [JsonPropertyName("toplam")]
        public double Toplam { get; set; }
    }

    // ── Faz renk paleti ───────────────────────────────────────────────────────

    public static class PhaseColors
    {
        public const string Betonarme  = "#ED7D31";  // turuncu
        public const string Duvar      = "#5B9BD5";  // mavi
        public const string Cerceve    = "#FFC000";  // sarı
        public const string Cati       = "#9E480E";  // kahve
        public const string Mep        = "#70AD47";  // yeşil
        public const string Default    = "#4A90D9";  // açık mavi

        public static string ForPhase(string phase)
        {
            return phase?.ToLowerInvariant() switch
            {
                "betonarme" or "beton" or "döküm" => Betonarme,
                "duvar"     or "blok"              => Duvar,
                "çerçeve"   or "çatı çerçevesi"   => Cerceve,
                "çatı"      or "cati"              => Cati,
                "mep"       or "tesisat"            => Mep,
                _                                  => Default
            };
        }
    }
}
