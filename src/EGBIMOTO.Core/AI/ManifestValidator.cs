using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Core.AI
{
    /// <summary>
    /// Manifest JSON'unu yapısal ve semantik açıdan doğrular.
    ///
    /// v2.0 değişiklikleri:
    ///   - Fail-fast: dosya bulunamazsa FileNotFoundException (sessiz return yerine)
    ///   - ConcurrentDictionary cache: aynı contracts dosyası tekrar parse edilmez
    ///   - JsonPath destekli ValidationError: hata konumu "$. steps[2].from" gibi gösterilir
    ///   - BuildFixPrompt: LLM'e hata konumuyla birlikte özet gönderir
    ///   - ErrorSummaryForLLM: ManifestGenerator retry döngüsü için ayrı property
    /// </summary>
    public sealed class ManifestValidator
    {
        private readonly HashSet<string> _knownOps;

        // v13: op → presence="required" param seti. Kod SSoT'tir: bu set
        // yalnızca ctx.RequireX(...) ile okunan paramları içerir
        // (bkz. deploy/generate_op_contracts.py). Boş sözlük = param kontrolü kapalı
        // (knownOpNames ctor'u ile oluşturulduğunda).
        private readonly Dictionary<string, HashSet<string>> _requiredParams;

        // Aynı contracts dosyası birden fazla instance tarafından okunmaz
        private static readonly ConcurrentDictionary<string, (HashSet<string> Ops, Dictionary<string, HashSet<string>> Required)> _contractsCache
            = new ConcurrentDictionary<string, (HashSet<string>, Dictionary<string, HashSet<string>>)>(StringComparer.OrdinalIgnoreCase);

        public ManifestValidator(string contractsJsonPath)
        {
            var (ops, req) = LoadContractsCached(contractsJsonPath);
            _knownOps = ops;
            _requiredParams = req;
        }

        public ManifestValidator(IEnumerable<string> knownOpNames)
        {
            _knownOps = new HashSet<string>(knownOpNames, StringComparer.OrdinalIgnoreCase);
            _requiredParams = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        // ── Contracts yükleme ─────────────────────────────────────────────────

        private static (HashSet<string>, Dictionary<string, HashSet<string>>) LoadContractsCached(string path)
        {
            return _contractsCache.GetOrAdd(path, p =>
            {
                // Fail-fast: dosya yoksa çalışmaya devam etme
                if (!File.Exists(p))
                    throw new FileNotFoundException(
                        $"Kritik: op_contracts.json bulunamadı: {p}. " +
                        "EgbimotoApp.Initialize() çağrılmadan önce addin dizini doğru ayarlanmış olmalı.");

                try
                {
                    var json = File.ReadAllText(p, System.Text.Encoding.UTF8);
                    using var doc = JsonDocument.Parse(json);

                    var ops = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var req = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        ops.Add(prop.Name);
                        if (!prop.Value.TryGetProperty("params", out var pars) ||
                            pars.ValueKind != JsonValueKind.Array)
                            continue;

                        HashSet<string>? set = null;
                        foreach (var pe in pars.EnumerateArray())
                        {
                            // v13 şema: "presence": "required"; eski şema: "required": true
                            bool isReq =
                                (pe.TryGetProperty("presence", out var pr) &&
                                 pr.ValueKind == JsonValueKind.String &&
                                 string.Equals(pr.GetString(), "required", StringComparison.OrdinalIgnoreCase))
                                ||
                                (!pe.TryGetProperty("presence", out _) &&
                                 pe.TryGetProperty("required", out var rq) &&
                                 rq.ValueKind == JsonValueKind.True);

                            if (isReq && pe.TryGetProperty("key", out var k) &&
                                k.ValueKind == JsonValueKind.String)
                            {
                                set ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                set.Add(k.GetString()!);
                            }
                        }
                        if (set != null) req[prop.Name] = set;
                    }
                    return (ops, req);
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"op_contracts.json bozuk JSON: {ex.Message}", ex);
                }
            });
        }

        // ── Ana doğrulama ────────────────────────────────────────────────────

        public ValidationResult Validate(string json)
        {
            var errors = new List<ValidationError>();

            // 1. JSON parse
            EgManifest? manifest = null;
            try
            {
                manifest = JsonSerializer.Deserialize<EgManifest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError("JSON_PARSE", "$", ex.Message));
                return new ValidationResult(false, errors, null);
            }

            if (manifest is null)
            {
                errors.Add(new ValidationError("JSON_NULL", "$", "Deserialize sonucu null."));
                return new ValidationResult(false, errors, null);
            }

            // 2. Zorunlu üst alanlar
            if (string.IsNullOrWhiteSpace(manifest.Title))
                errors.Add(new ValidationError("FIELD_MISSING", "$.title", "'title' alanı boş veya eksik."));

            if (manifest.Steps == null || manifest.Steps.Count == 0)
                errors.Add(new ValidationError("FIELD_MISSING", "$.steps", "'steps' dizisi boş veya eksik."));

            if (manifest.Steps == null)
                return new ValidationResult(false, errors, manifest);

            // 3. Step id benzersizliği
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manifest.Steps.Count; i++)
            {
                var s = manifest.Steps[i];
                if (string.IsNullOrWhiteSpace(s.Id))
                    errors.Add(new ValidationError("STEP_ID_EMPTY",
                        $"$.steps[{i}].id", $"op='{s.Op}' için id boş."));
                else if (!seenIds.Add(s.Id))
                    errors.Add(new ValidationError("STEP_ID_DUPLICATE",
                        $"$.steps[{i}].id", $"id='{s.Id}' daha önce kullanılmış."));
            }

            // 4. Op kontrolü (contracts ile karşılaştır)
            if (_knownOps.Count > 0)
            {
                for (int i = 0; i < manifest.Steps.Count; i++)
                {
                    var s = manifest.Steps[i];
                    if (!string.IsNullOrWhiteSpace(s.Op) && !_knownOps.Contains(s.Op))
                        errors.Add(new ValidationError("OP_UNKNOWN",
                            $"$.steps[{i}].op",
                            $"'{s.Op}' op_contracts.json'da kayıtlı değil."));
                }
            }

            // 5. from / from_many / depends_on referans bütünlüğü
            var definedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manifest.Steps.Count; i++)
            {
                var s = manifest.Steps[i];

                if (!string.IsNullOrWhiteSpace(s.From) && !definedIds.Contains(s.From))
                    errors.Add(new ValidationError("FROM_INVALID",
                        $"$.steps[{i}].from",
                        $"'{s.From}' bu adımdan önce tanımlanmamış."));

                if (s.FromMany != null)
                    foreach (var fid in s.FromMany)
                        if (!definedIds.Contains(fid))
                            errors.Add(new ValidationError("FROM_MANY_INVALID",
                                $"$.steps[{i}].from_many",
                                $"'{fid}' bu adımdan önce tanımlanmamış."));

                if (s.DependsOn != null)
                    foreach (var dep in s.DependsOn)
                        if (!definedIds.Contains(dep))
                            errors.Add(new ValidationError("DEPENDS_ON_INVALID",
                                $"$.steps[{i}].depends_on",
                                $"'{dep}' bu adımdan önce tanımlanmamış."));

                if (!string.IsNullOrWhiteSpace(s.Id))
                    definedIds.Add(s.Id);
            }

            // 6. v13 — presence="required" param kontrolü
            //    Yalnızca çalışma zamanında Require* ile zorlanan paramlar denetlenir;
            //    "recommended"/"optional" paramlar hata üretmez (pipeline/registry'den
            //    gelebilirler). Böylece statik kontrol ile runtime davranışı birebir örtüşür.
            if (_requiredParams.Count > 0)
            {
                for (int i = 0; i < manifest.Steps.Count; i++)
                {
                    var s = manifest.Steps[i];
                    if (string.IsNullOrWhiteSpace(s.Op) ||
                        !_requiredParams.TryGetValue(s.Op, out var reqKeys))
                        continue;

                    foreach (var key in reqKeys)
                    {
                        if (s.Params == null || !s.Params.ContainsKey(key))
                            errors.Add(new ValidationError("PARAM_REQUIRED_MISSING",
                                $"$.steps[{i}].inputs.{key}",
                                $"op '{s.Op}' zorunlu param '{key}' bekliyor — çalışma zamanında hata verecek."));
                    }
                }
            }

            // 7. Soft uyarılar (hata değil)
            var warnings = new List<string>();
            var hasSummary = manifest.Steps.Any(s =>
                s.Op is "validation_summary" or "show_table" or "show_result");
            var hasExport = manifest.Steps.Any(s =>
                s.Op?.StartsWith("export_") == true);

            if (!hasSummary)
                warnings.Add("WARN_NO_SUMMARY: validation_summary / show_table / show_result eksik.");
            if (!hasExport)
                warnings.Add("WARN_NO_EXPORT: export_xlsx / export_html_report eksik.");

            return new ValidationResult(errors.Count == 0, errors, manifest, warnings);
        }

        // ── LLM fix prompt ───────────────────────────────────────────────────

        /// <summary>
        /// ManifestGenerator retry döngüsünde LLM'e gönderilecek düzeltme promptu.
        /// Hata konumları JsonPath ile belirtilir.
        /// </summary>
        public static string BuildFixPrompt(string originalJson, ValidationResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Aşağıdaki manifest JSON'unda şu hatalar var. Sadece hataları düzelt, amacı değiştirme:");
            sb.AppendLine();
            foreach (var e in result.Errors)
                sb.AppendLine($"- [{e.JsonPath}] {e.Code}: {e.Message}");
            sb.AppendLine();
            sb.AppendLine("Bozuk manifest:");
            sb.AppendLine(originalJson);
            return sb.ToString();
        }
    }

    // ── Hata modeli ───────────────────────────────────────────────────────────

    /// <summary>
    /// v2.0: Code + JsonPath + Message üçlüsü.
    /// JsonPath hata konumunu "$. steps[2].from" gibi gösterir.
    /// </summary>
    public sealed class ValidationError
    {
        public string Code     { get; init; } = "";
        public string JsonPath { get; init; } = "";
        public string Message  { get; init; } = "";

        public ValidationError(string code, string jsonPath, string message)
        { Code = code; JsonPath = jsonPath; Message = message; }

        public override string ToString() => $"[{JsonPath}] {Code}: {Message}";
    }

    // ── Sonuç modeli ──────────────────────────────────────────────────────────

    public sealed class ValidationResult
    {
        public bool                  IsValid   { get; }
        public List<ValidationError> Errors    { get; }
        public List<string>          Warnings  { get; }
        public EgManifest?           Manifest  { get; }

        public ValidationResult(bool ok, List<ValidationError> errors,
            EgManifest? manifest, List<string>? warnings = null)
        {
            IsValid  = ok;
            Errors   = errors;
            Warnings = warnings ?? new List<string>();
            Manifest = manifest;
        }

        /// <summary>ManifestGenerator retry döngüsü için: hataları LLM'e özetler.</summary>
        public string ErrorSummaryForLLM =>
            Errors.Count == 0
                ? "✓ Geçerli"
                : string.Join("\n", Errors.Select(e => $"- [{e.JsonPath}] {e.Code}: {e.Message}"));

        /// <summary>UI ve loglama için kısa özet.</summary>
        public string ErrorSummary => ErrorSummaryForLLM;
    }
}
