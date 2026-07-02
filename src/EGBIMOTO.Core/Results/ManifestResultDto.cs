using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EGBIMOTO.Core.Results
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — ManifestResultDto  (v13.5)
    //
    //  Revit bağımsız saf veri sınıfı. Bir manifest çalıştırmasının kullanıcıya
    //  gösterilecek SONUCUNU tek tip bir yapıda taşır — "sonuç tipografisi"
    //  (metin dökülen TaskDialog yerine yapılandırılmış, tıklanabilir sonuç).
    //
    //  Şu an iki üretici var (bkz. ManifestResultAdapter):
    //    • ValidationReport  → FromValidationReport()  (Doğrulama sonuçları)
    //    • List<Dictionary>  → FromRows()               (Tablo/metraj sonuçları)
    //
    //  Addin/UI/Results/ManifestResultWindow bu DTO'yu render eder:
    //    • DataGrid (Columns/Rows)
    //    • Özet çubuğu (Summary)
    //    • "Modelde Göster" → ElementIds ile uidoc.Selection + zoom
    //    • CSV dışa aktarım
    //
    //  Genişletme noktası: Kind alanına göre ileride özel renderer'lar
    //  (Takeoff, Schedule) eklenebilir — bkz. IManifestResultRenderer.
    // ═══════════════════════════════════════════════════════════════════════════

    public enum ManifestResultKind
    {
        Generic    = 0,
        Validation = 1,
        Table      = 2,
    }

    public sealed class ManifestResultDto
    {
        [JsonPropertyName("kind")]
        public ManifestResultKind Kind { get; set; } = ManifestResultKind.Generic;

        [JsonPropertyName("title")]
        public string Title { get; set; } = "EGBIMOTO Sonuç";

        /// <summary>DataGrid sütun sırası. Boşsa Rows[0].Keys kullanılır.</summary>
        [JsonPropertyName("columns")]
        public List<string> Columns { get; set; } = new();

        /// <summary>Her satır bir Dictionary — anahtar=sütun adı. "element_id" özel anlamlıdır (highlight).</summary>
        [JsonPropertyName("rows")]
        public List<Dictionary<string, object?>> Rows { get; set; } = new();

        /// <summary>Üst bilgi çubuğu — örn. {"Toplam":"127","Hata":"3","Uyarı":"5"}.</summary>
        [JsonPropertyName("summary")]
        public Dictionary<string, string> Summary { get; set; } = new();

        /// <summary>"Modelde Göster" butonu için — rows içindeki element_id'lerden bağımsız, tüm sonucun ID kümesi.</summary>
        [JsonPropertyName("element_ids")]
        public List<long> ElementIds { get; set; } = new();

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new();
    }
}
