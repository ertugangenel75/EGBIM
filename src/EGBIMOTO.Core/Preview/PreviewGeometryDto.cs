using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EGBIMOTO.Core.Preview
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — PreviewGeometryDto  (v1.0)
    //
    //  Revit bağımsız saf veri sınıfı.
    //  DagExecutor → PreviewGateWindow → Three.js WebView2 köprüsü.
    //
    //  Koordinat sistemi:
    //    Revit feet (X,Y,Z) → mm (X,Z,-Y) → Three.js Y-up
    //    Dönüşüm PreviewGeometryExtractor'da yapılır.
    //
    //  Kullanım akışı:
    //    1. preview_collect_geometry op → PreviewGeometryDto üretir
    //    2. preview_gate op → DagExecutor UserGateCallback çağırır
    //    3. Callback → PreviewGateWindow.ShowModal(dto) → bool
    //    4. vars["s_gate"] = "confirmed" | "cancelled"
    //    5. Sonraki yazma adımları condition: "$s_gate == confirmed"
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class PreviewGeometryDto
    {
        // ── Metadata ──────────────────────────────────────────────────────────

        [JsonPropertyName("operation_name")]
        public string OperationName { get; set; } = "Önizleme";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("element_count")]
        public int ElementCount { get; set; }

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new();

        [JsonPropertyName("stats")]
        public Dictionary<string, string> Stats { get; set; } = new();

        // ── Geometri ──────────────────────────────────────────────────────────

        [JsonPropertyName("meshes")]
        public List<PreviewMesh> Meshes { get; set; } = new();

        [JsonPropertyName("edges")]
        public List<PreviewEdge> Edges { get; set; } = new();

        [JsonPropertyName("labels")]
        public List<PreviewLabel> Labels { get; set; } = new();

        [JsonPropertyName("bbox")]
        public PreviewBBox? BBox { get; set; }

        // ── İşaret (EgbimotoApp için) ─────────────────────────────────────────

        /// <summary>
        /// true → PreviewGateWindow reddedildi veya iptal edildi.
        /// DagExecutor bunu set etmez — PreviewGateWindow callback dönüş değeridir.
        /// </summary>
        [JsonIgnore]
        public bool Confirmed { get; set; } = false;
    }

    // ── Mesh (üçgen yüzey, yarı saydam) ──────────────────────────────────────

    public sealed class PreviewMesh
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        /// <summary>hex renk, örn: "#FF6B35"</summary>
        [JsonPropertyName("color")]
        public string Color { get; set; } = "#4A90D9";

        [JsonPropertyName("opacity")]
        public float Opacity { get; set; } = 0.65f;

        /// <summary>
        /// Flat XYZ array — Three.js BufferGeometry formatı.
        /// Koordinatlar mm, Y-up (Revit'ten dönüştürülmüş).
        /// Her 9 float = 1 üçgen (3 köşe × XYZ).
        /// </summary>
        [JsonPropertyName("vertices")]
        public List<float> Vertices { get; set; } = new();

        [JsonPropertyName("indices")]
        public List<int> Indices { get; set; } = new();

        [JsonPropertyName("properties")]
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    // ── Edge (tel çerçeve) ────────────────────────────────────────────────────

    public sealed class PreviewEdge
    {
        /// <summary>Flat XYZ çiftleri — başlangıç ve bitiş noktaları mm.</summary>
        [JsonPropertyName("points")]
        public List<float> Points { get; set; } = new();

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#FFFFFF";

        [JsonPropertyName("opacity")]
        public float Opacity { get; set; } = 0.9f;
    }

    // ── Label (3D metin ek açıklama) ──────────────────────────────────────────

    public sealed class PreviewLabel
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#FFDD00";
    }

    // ── BBox (kamera odaklanması) ─────────────────────────────────────────────

    public sealed class PreviewBBox
    {
        [JsonPropertyName("min_x")] public float MinX { get; set; }
        [JsonPropertyName("min_y")] public float MinY { get; set; }
        [JsonPropertyName("min_z")] public float MinZ { get; set; }
        [JsonPropertyName("max_x")] public float MaxX { get; set; }
        [JsonPropertyName("max_y")] public float MaxY { get; set; }
        [JsonPropertyName("max_z")] public float MaxZ { get; set; }

        [JsonIgnore] public float CenterX => (MinX + MaxX) / 2f;
        [JsonIgnore] public float CenterY => (MinY + MaxY) / 2f;
        [JsonIgnore] public float CenterZ => (MinZ + MaxZ) / 2f;
        [JsonIgnore] public float Diagonal
        {
            get
            {
                var dx = MaxX - MinX; var dy = MaxY - MinY; var dz = MaxZ - MinZ;
                return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }
    }

    // ── Kategori renk paleti (sabit, önizlemede tutarlılık için) ──────────────

    public static class PreviewColors
    {
        public const string Wall       = "#5B9BD5";  // mavi
        public const string Floor      = "#A5A5A5";  // gri
        public const string Column     = "#ED7D31";  // turuncu
        public const string Beam       = "#FFC000";  // sarı
        public const string Foundation = "#70AD47";  // yeşil
        public const string Roof       = "#9E480E";  // kahve
        public const string Stair      = "#44546A";  // lacivert
        public const string Default    = "#4A90D9";  // açık mavi

        public static string ForCategory(string category)
        {
            return category?.ToLowerInvariant() switch
            {
                "duvar"   or "wall"       => Wall,
                "döşeme"  or "floor"      => Floor,
                "kolon"   or "column"     => Column,
                "kiriş"   or "beam"       => Beam,
                "temel"   or "foundation" => Foundation,
                "çatı"    or "roof"       => Roof,
                "merdiven"or "stair"      => Stair,
                _                        => Default
            };
        }
    }
}
