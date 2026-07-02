using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Core.Input
{
    public enum ManifestInputType
    {
        String, Number, Bool,
        FilePath, FolderPath, OutputFolder, PozFile,
        Level, View, Sheet, FamilyType, ParameterName, Enum
    }

    public sealed class ManifestInputDef
    {
        public string             StepId       { get; init; } = "";
        public string             ParamKey     { get; init; } = "";
        public string             UniqueKey    { get; init; } = "";
        public ManifestInputType  InputType    { get; init; }
        public string             Label        { get; init; } = "";
        public string?            Options      { get; init; }
        public double?            NumMin       { get; init; }
        public double?            NumMax       { get; init; }
        public string?            DefaultValue { get; init; }
        public string?            ResolvedValue { get; set; }
    }

    public sealed class ManifestInputScanner
    {
        private const string PREFIX = "$INPUT:";

        public List<ManifestInputDef> Scan(EgManifest manifest)
        {
            var defs = new List<ManifestInputDef>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var step in manifest.Steps)
            {
                if (step.Params is null) continue;
                foreach (var kv in step.Params)
                {
                    var raw = ExtractString(kv.Value);
                    if (raw is null || !raw.StartsWith(PREFIX, StringComparison.OrdinalIgnoreCase)) continue;
                    var def = Parse(raw, step.Id, kv.Key);
                    if (def is not null && seen.Add(def.UniqueKey)) defs.Add(def);
                }
            }
            return defs;
        }

        public EgManifest ApplyResolved(EgManifest manifest, List<ManifestInputDef> defs)
        {
            var lookup = defs
                .Where(d => d.ResolvedValue is not null)
                .ToDictionary(d => BuildToken(d), d => d.ResolvedValue!, StringComparer.OrdinalIgnoreCase);

            foreach (var step in manifest.Steps)
            {
                if (step.Params is null) continue;
                var updated = new Dictionary<string, object?>();
                foreach (var kv in step.Params)
                {
                    var raw = ExtractString(kv.Value);
                    updated[kv.Key] = raw is not null ? ResolveToken(raw, lookup, kv.Value) : kv.Value;
                }
                step.Params.Clear();
                foreach (var kv in updated) step.Params[kv.Key] = kv.Value;
            }
            return manifest;
        }

        /// <summary>
        /// Bir param değerini çözer. Normal token ise lookup'tan değeri döner.
        /// "family_type ... >>family_name" / ">>type_name" soneki varsa, çözülen
        /// "Aile : Tip" değerini " : " ile bölüp ilgili parçayı döner.
        /// Token değilse veya çözülemezse orijinal değeri döner.
        /// </summary>
        private static object? ResolveToken(
            string raw,
            Dictionary<string, string> lookup,
            object? original)
        {
            if (!raw.StartsWith(PREFIX, StringComparison.OrdinalIgnoreCase)) return original;

            // ">>" split soneki var mı?
            var splitIdx = raw.IndexOf(">>", StringComparison.Ordinal);
            if (splitIdx < 0)
                return lookup.TryGetValue(raw, out var v) ? v : original;

            var baseToken = raw[..splitIdx].TrimEnd();
            var part      = raw[(splitIdx + 2)..].Trim().ToLowerInvariant();

            if (!lookup.TryGetValue(baseToken, out var resolved)) return original;

            // "Aile : Tip" → parçala
            var sepIdx = resolved.IndexOf(" : ", StringComparison.Ordinal);
            if (sepIdx < 0) return resolved; // beklenen format değilse ham değeri ver

            var familyName = resolved[..sepIdx].Trim();
            var typeName   = resolved[(sepIdx + 3)..].Trim();

            return part switch
            {
                "family_name" => familyName,
                "type_name"   => typeName,
                _             => resolved
            };
        }

        private static ManifestInputDef? Parse(string raw, string stepId, string paramKey)
        {
            var body  = raw[PREFIX.Length..];
            var parts = body.Split(':', 4);
            if (parts.Length < 2) return null;

            var typeStr = parts[0].Trim().ToLowerInvariant();
            var label   = parts[1].Trim();
            var options = parts.Length > 2 ? parts[2].Trim() : null;

            // family_type split soneki: "Etiket>>family_name" / "Etiket>>type_name"
            // Sonek UniqueKey'e dahil EDİLMEZ — böylece iki yarım tek dropdown'a düşer.
            // Sonek son segmentte (label veya options) olabilir; ilk ">>" yeterli.
            int lblSplit = label.IndexOf(">>", StringComparison.Ordinal);
            if (lblSplit >= 0) label = label[..lblSplit].TrimEnd();
            if (options is not null)
            {
                int optSplit = options.IndexOf(">>", StringComparison.Ordinal);
                if (optSplit >= 0) options = options[..optSplit].TrimEnd();
            }

            var inputType = typeStr switch
            {
                "string"         => ManifestInputType.String,
                "number"         => ManifestInputType.Number,
                "bool"           => ManifestInputType.Bool,
                "file_path"      => ManifestInputType.FilePath,
                "folder_path"    => ManifestInputType.FolderPath,
                "output_folder"  => ManifestInputType.OutputFolder,
                "poz_file"       => ManifestInputType.PozFile,
                "level"          => ManifestInputType.Level,
                "view"           => ManifestInputType.View,
                "sheet"          => ManifestInputType.Sheet,
                "family_type"    => ManifestInputType.FamilyType,
                "parameter_name" => ManifestInputType.ParameterName,
                "enum"           => ManifestInputType.Enum,
                _                => ManifestInputType.String
            };

            double? numMin = null, numMax = null;
            if (inputType == ManifestInputType.Number && options is not null)
            {
                var np = options.Split(':');
                if (np.Length >= 1 && double.TryParse(np[0], out var mn)) numMin = mn;
                if (np.Length >= 2 && double.TryParse(np[1], out var mx)) numMax = mx;
                options = null;
            }
            if (inputType == ManifestInputType.FilePath && options is null) options = "*.xlsx;*.json";
            if (inputType == ManifestInputType.PozFile) options = "*.xlsx";

            return new ManifestInputDef
            {
                StepId    = stepId, ParamKey = paramKey,
                UniqueKey = $"{typeStr}:{label}",
                InputType = inputType, Label = label,
                Options = options, NumMin = numMin, NumMax = numMax
            };
        }

        private static string BuildToken(ManifestInputDef d)
        {
            var ts = d.InputType switch
            {
                ManifestInputType.String        => "string",
                ManifestInputType.Number        => "number",
                ManifestInputType.Bool          => "bool",
                ManifestInputType.FilePath      => "file_path",
                ManifestInputType.FolderPath    => "folder_path",
                ManifestInputType.OutputFolder  => "output_folder",
                ManifestInputType.PozFile       => "poz_file",
                ManifestInputType.Level         => "level",
                ManifestInputType.View          => "view",
                ManifestInputType.Sheet         => "sheet",
                ManifestInputType.FamilyType    => "family_type",
                ManifestInputType.ParameterName => "parameter_name",
                ManifestInputType.Enum          => "enum",
                _                              => "string"
            };
            return d.Options is not null ? $"{PREFIX}{ts}:{d.Label}:{d.Options}" : $"{PREFIX}{ts}:{d.Label}";
        }

        private static string? ExtractString(object? value)
        {
            if (value is string s) return s;
            if (value is JsonElement je && je.ValueKind == JsonValueKind.String) return je.GetString();
            return null;
        }
    }
}
