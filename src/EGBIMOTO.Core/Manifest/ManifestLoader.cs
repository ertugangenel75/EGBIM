using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EGBIMOTO.Core.Manifest
{
    public static class ManifestLoader
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive  = true,
            ReadCommentHandling          = JsonCommentHandling.Skip,
            AllowTrailingCommas          = true,
            DefaultIgnoreCondition       = JsonIgnoreCondition.WhenWritingNull,
        };

        // ── Tek dosya yükle ────────────────────────────────────────────────────

        public static EgManifest Load(string filePath, ManifestSource source = ManifestSource.BuiltIn)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Manifest bulunamadı: {filePath}");

            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var manifest = JsonSerializer.Deserialize<EgManifest>(json, _opts)
                ?? throw new InvalidOperationException($"Manifest parse edilemedi: {filePath}");

            // Runtime meta
            manifest.FilePath   = filePath;
            manifest.FolderName = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");
            manifest.Source     = source;

            // Category yoksa klasörden türet
            if (string.IsNullOrWhiteSpace(manifest.Category))
                manifest.Category = manifest.FolderName;

            // Step params içindeki JsonElement'leri unwrap et
            foreach (var step in manifest.Steps)
                if (step.Params is not null)
                    step.Params = UnwrapDict(step.Params);

            return manifest;
        }


        // ── JSON string'ten yükle (AI generator için) ─────────────────────────

        public static EgManifest LoadFromJson(string jsonText)
        {
            var manifest = JsonSerializer.Deserialize<EgManifest>(jsonText, _opts)
                ?? throw new InvalidOperationException("Manifest parse edilemedi");

            foreach (var step in manifest.Steps)
                if (step.Params is not null)
                    step.Params = UnwrapDict(step.Params);

            return manifest;
        }

        // ── Klasör tara ────────────────────────────────────────────────────────

        /// <summary>
        /// Belirtilen klasördeki tüm *.json dosyalarını özyinelemeli tarar.
        /// Browser bu listeyi kullanır.
        ///
        /// Gizleme kuralları (öncelik sırası):
        ///   1. Klasör adı "_" ile başlıyorsa → atlanır  (_draft, _archive)
        ///   2. Klasör adı ".disabled" ile bitiyorsa → atlanır
        ///   3. excludeFolders listesinde varsa → atlanır ("legacy_samples" default)
        ///
        /// Default excludeFolders = ["legacy_samples"] — geliştirme örnekleri gizlenir.
        /// Tümünü görmek için: LoadFolder(root, excludeFolders: null)
        /// source: yüklenen tüm manifest'ler bu kaynakla etiketlenir (Browser badge için).
        /// </summary>
        public static List<EgManifest> LoadFolder(
            string rootPath,
            IEnumerable<string>? excludeFolders = null,
            ManifestSource source = ManifestSource.BuiltIn)
        {
            if (!Directory.Exists(rootPath)) return new();

            // Varsayılan: legacy_samples gizli
            var excluded = new HashSet<string>(
                excludeFolders ?? new[] { "legacy_samples" },
                StringComparer.OrdinalIgnoreCase);

            return Directory
                .GetFiles(rootPath, "*.json", SearchOption.AllDirectories)
                .Where(f =>
                {
                    // Dosya yolundaki TÜM segment'leri kontrol et
                    var parts = f.Split(Path.DirectorySeparatorChar,
                                        Path.AltDirectorySeparatorChar);
                    foreach (var part in parts)
                    {
                        if (part.StartsWith("_"))             return false; // _draft klasörü
                        if (part.EndsWith(".disabled"))       return false; // klasör.disabled
                        if (excluded.Contains(part))          return false; // legacy_samples
                    }
                    return true;
                })
                .OrderBy(f => f)
                .Select(f =>
                {
                    try   { return Load(f, source); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ManifestLoader] Parse hatası: {Path.GetFileName(f)}\n{ex.Message}");
                        return null;
                    }
                })
                .Where(m => m is not null)
                .Cast<EgManifest>()
                .ToList();
        }

        // ── Çok kaynaklı yükleme (Built-in + User + Project) ──────────────────

        /// <summary>
        /// Üç kaynağı tek listede birleştirir ve çakışmaları çözer.
        /// Öncelik: Project &gt; User &gt; Built-in (aynı DisplayId varsa üst kaynak kazanır).
        /// userRoot / projectRoot null veya mevcut değilse atlanır.
        /// </summary>
        public static List<EgManifest> LoadAllSources(
            string builtInRoot,
            string? userRoot,
            string? projectRoot,
            IEnumerable<string>? excludeFolders = null)
        {
            var builtIn = LoadFolder(builtInRoot, excludeFolders, ManifestSource.BuiltIn);
            var user    = string.IsNullOrWhiteSpace(userRoot)
                          ? new List<EgManifest>()
                          : LoadFolder(userRoot, excludeFolders, ManifestSource.User);
            var project = string.IsNullOrWhiteSpace(projectRoot)
                          ? new List<EgManifest>()
                          : LoadFolder(projectRoot, excludeFolders, ManifestSource.Project);

            // Önce düşük öncelikli, sonra yüksek öncelikli ekle ⇒ üst kaynak override eder
            var byId = new Dictionary<string, EgManifest>(StringComparer.OrdinalIgnoreCase);
            void Merge(IEnumerable<EgManifest> list)
            {
                foreach (var m in list) byId[m.DisplayId] = m;
            }
            Merge(builtIn);   // taban
            Merge(user);      // user, built-in'i ezer
            Merge(project);   // project en üstte

            return byId.Values
                       .OrderBy(m => m.Category, StringComparer.OrdinalIgnoreCase)
                       .ThenBy(m => m.DisplayId, StringComparer.OrdinalIgnoreCase)
                       .ToList();
        }

        // ── Kullanıcı manifest ekleme / silme ─────────────────────────────────

        /// <summary>
        /// Bir JSON dosyasını hedef klasöre kopyalar. Çağıran taraf önce
        /// ManifestLinter ile doğrulamalı. Aynı isimde dosya varsa _2, _3 ekler.
        /// Yeni dosyanın tam yolunu döner.
        /// </summary>
        public static string ImportManifestFile(string sourceFilePath, string targetRoot)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Kaynak dosya yok: {sourceFilePath}");

            Directory.CreateDirectory(targetRoot);

            var baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var ext      = ".json";
            var dest     = Path.Combine(targetRoot, baseName + ext);

            int i = 2;
            while (File.Exists(dest))
            {
                dest = Path.Combine(targetRoot, $"{baseName}_{i}{ext}");
                i++;
            }

            File.Copy(sourceFilePath, dest, overwrite: false);
            return dest;
        }

        /// <summary>
        /// Manifest dosyasını siler. Yalnızca CanDelete=true (User/Project) için izin verilir.
        /// Built-in silinmeye çalışılırsa InvalidOperationException atar.
        /// </summary>
        public static void DeleteManifest(EgManifest manifest)
        {
            if (!manifest.CanDelete)
                throw new InvalidOperationException(
                    "Built-in manifest silinemez. Yalnızca User ve Project manifest'leri kaldırılabilir.");

            if (string.IsNullOrWhiteSpace(manifest.FilePath) || !File.Exists(manifest.FilePath))
                throw new FileNotFoundException($"Manifest dosyası bulunamadı: {manifest.FilePath}");

            File.Delete(manifest.FilePath);
        }

        // ── JsonElement → native C# ────────────────────────────────────────────

        private static Dictionary<string, object?> UnwrapDict(Dictionary<string, object?> dict)
            => dict.ToDictionary(kv => kv.Key, kv => Unwrap(kv.Value));

        private static object? Unwrap(object? val)
        {
            if (val is not JsonElement je) return val;

            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.TryGetInt64(out var l)  ? (object?)l
                                      : je.TryGetDouble(out var d) ? d
                                      : je.GetRawText(),
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.Null   => null,
                JsonValueKind.Array  => je.EnumerateArray()
                                          .Select(e => Unwrap(e))
                                          .ToList(),
                JsonValueKind.Object => je.EnumerateObject()
                                          .ToDictionary(p => p.Name, p => Unwrap(p.Value)),
                _                   => null
            };
        }
    }
}
