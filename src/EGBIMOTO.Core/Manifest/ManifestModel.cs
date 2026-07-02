using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EGBIMOTO.Core.Manifest
{
    /// <summary>
    /// Manifest'in hangi kaynaktan geldiğini belirtir.
    /// Built-in: kurulumla gelen, silinemez.
    /// User: kullanıcının eklediği (%AppData%/EGBIMOTO/user_manifests), silinebilir.
    /// Project: aktif Revit projesi yanındaki /eg_manifests klasörü, silinebilir.
    /// </summary>
    public enum ManifestSource
    {
        BuiltIn = 0,
        User    = 1,
        Project = 2
    }

    public sealed class EgManifest
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("pre_checks")]
        public List<EgPreCheck>? PreChecks { get; set; }

        [JsonPropertyName("steps")]
        public List<EgStep> Steps { get; set; } = new();

        [JsonPropertyName("transaction_policy")]
        public string TransactionPolicy { get; set; } = "none";

        [JsonIgnore] public bool IsAtomic
            => TransactionPolicy.Equals("atomic", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore] public bool IsPreview
            => TransactionPolicy.Equals("preview", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore] public string FilePath   { get; set; } = "";
        [JsonIgnore] public string FolderName { get; set; } = "";

        // ── Runtime: kaynak bilgisi (Browser badge + silme yetkisi için) ──────
        [JsonIgnore] public ManifestSource Source { get; set; } = ManifestSource.BuiltIn;

        /// <summary>Sadece User ve Project manifest'leri silinebilir; Built-in korunur.</summary>
        [JsonIgnore] public bool CanDelete => Source != ManifestSource.BuiltIn;

        /// <summary>
        /// Çakışma çözümü için kimlik. Aynı DisplayId iki kaynakta varsa
        /// Project &gt; User &gt; Built-in önceliği uygulanır.
        /// Title boşsa dosya adına düşer.
        /// </summary>
        [JsonIgnore]
        public string DisplayId =>
            !string.IsNullOrWhiteSpace(Title)
                ? Title.Trim()
                : System.IO.Path.GetFileNameWithoutExtension(FilePath);

        /// <summary>Browser'da kaynak rozeti metni.</summary>
        [JsonIgnore]
        public string SourceLabel => Source switch
        {
            ManifestSource.User    => "User",
            ManifestSource.Project => "Project",
            _                      => "Built-in"
        };

        /// <summary>
        /// v10 yeni katalog manifest'i mi? (manifests/v10_katalog/ altındakiler).
        /// Browser açılışta varsayılan olarak yalnızca bunları gösterir.
        /// </summary>
        [JsonIgnore]
        public bool IsV10Catalog =>
            FilePath.Replace('\\', '/').Contains("/v10_katalog/", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class EgPreCheck
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("categories")]
        public List<string>? Categories { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("min_count")]
        public int MinCount { get; set; } = 1;

        [JsonPropertyName("on_fail")]
        public string OnFail { get; set; } = "ABORT";

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public sealed class EgStep
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("op")]
        public string Op { get; set; } = "";

        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("from_many")]
        public List<string>? FromMany { get; set; }

        [JsonPropertyName("inputs")]
        public Dictionary<string, object?>? Params { get; set; }

        [JsonPropertyName("params")]
        public Dictionary<string, object?>? ParamsLegacy
        {
            get => null;
            set { if (Params == null && value != null) Params = value; }
        }

        [JsonPropertyName("required")]
        public bool Required { get; set; } = true;

        [JsonPropertyName("cache")]
        public bool? Cache { get; set; }

        [JsonPropertyName("depends_on")]
        public List<string>? DependsOn { get; set; }

        [JsonPropertyName("condition")]
        public string? Condition { get; set; }
    }
}
