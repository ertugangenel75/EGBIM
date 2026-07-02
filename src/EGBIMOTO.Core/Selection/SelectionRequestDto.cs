using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EGBIMOTO.Core.Selection
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — SelectionRequestDto  (v13.5)
    //
    //  Revit bağımsız saf veri sınıfı — PreviewGeometryDto/FourDFiveDDto ile
    //  aynı desen: Core, DagExecutor.UserSelectionCallback aracılığıyla Addin
    //  katmanına bu isteği iletir; Addin gerçek uidoc.Selection.PickObjects
    //  çağrısını yapar ve SelectionResultDto döner.
    //
    //  Akış:
    //    1. Manifest'te "selection_gate" adımı (params: prompt, mode, categories)
    //    2. DagExecutor SELECTION_GATE_OP'u intercept eder → bu DTO'yu üretir
    //    3. UserSelectionCallback(dto) → Addin'de gerçek Revit seçimi
    //    4. Sonuç SelectionResultDto olarak vars[step.Id]'ye yazılır
    //    5. "selection_to_elements" op'u (from: gate_step_id) → List<Element>
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class SelectionRequestDto
    {
        /// <summary>Kullanıcıya gösterilecek durum çubuğu / pencere mesajı.</summary>
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "Eleman seçin";

        /// <summary>"single" | "multiple" — varsayılan: multiple.</summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "multiple";

        /// <summary>
        /// OST_ kategori adlarıyla filtre (boşsa tüm kategoriler seçilebilir).
        /// Örn: ["OST_Walls", "OST_StructuralColumns"]
        /// </summary>
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new();

        /// <summary>Minimum seçim sayısı — sağlanmazsa kullanıcı uyarılır (Addin tarafında).</summary>
        [JsonPropertyName("min_count")]
        public int MinCount { get; set; } = 0;

        /// <summary>Maksimum seçim sayısı — 0 = sınırsız.</summary>
        [JsonPropertyName("max_count")]
        public int MaxCount { get; set; } = 0;

        /// <summary>Bağlantılı model elemanlarına izin verilsin mi (RevitLinkInstance içi).</summary>
        [JsonPropertyName("allow_linked")]
        public bool AllowLinked { get; set; } = false;
    }
}
