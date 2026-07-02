using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EGBIMOTO.Core.Selection
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — SelectionResultDto  (v13.5)
    //
    //  vars[selection_gate_step_id] bu tipte saklanır (PreviewGeometryDto'nun
    //  aksine — orada vars sadece "confirmed"/"cancelled" string'i tutuyordu,
    //  çünkü geometriyi ayrı bir op üretiyordu. Burada selection_gate'in
    //  KENDİSİ üretici olduğu için tüm sonucu taşımak gerekiyor).
    //
    //  ToString() override sayesinde mevcut EvalCondition altyapısı hiç
    //  değişmeden çalışır: "condition": "$sec == confirmed" ifadesi
    //  ResolveValue(...).ToString() üzerinden bu override'ı kullanır.
    //
    //  ElementIds: Revit ElementId.Value (long) — Core, Revit API'ye bağımlı
    //  olmadığı için ham long olarak taşınır. Addin'deki "selection_to_elements"
    //  op'u Rv.MakeElementId(long) ile gerçek Element'e çevirir.
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class SelectionResultDto
    {
        [JsonPropertyName("cancelled")]
        public bool Cancelled { get; set; }

        [JsonPropertyName("element_ids")]
        public List<long> ElementIds { get; set; } = new();

        [JsonPropertyName("count")]
        public int Count => ElementIds.Count;

        /// <summary>Kullanıcıya gösterilen orijinal istek — loglama/debug için.</summary>
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        /// <summary>EvalCondition'ın "$step == confirmed" kalıbını çalıştırabilmesi için.</summary>
        public override string ToString() => Cancelled ? "cancelled" : "confirmed";
    }
}
