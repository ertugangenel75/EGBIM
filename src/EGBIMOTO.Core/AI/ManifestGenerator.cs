using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Core.AI
{
    /// <summary>
    /// API ile manifest üretir. op_contracts.json runtime'da okunur → op eklense otomatik güncellenir.
    /// Validation loop: max 2 retry, her hata için düzeltme promptu gönderilir.
    /// </summary>
    public sealed class ManifestGenerator
    {
        private const string ApiUrl    = "https://api.anthropic.com/v1/messages";
        private const string Model     = "claude-sonnet-4-20250514";
        private const int    MaxTokens = 4096;
        private const int    MaxRetry  = 2;

        private readonly string            _apiKey;
        private readonly string            _contractsPath;
        private readonly ManifestValidator _validator;
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            // Revit process'inde proxy otomatik algılansın, SSL doğrulama açık kalsın
            UseProxy                   = true,
            ServerCertificateCustomValidationCallback = null,
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        public ManifestGenerator(string apiKey, string contractsPath)
        {
            _apiKey        = apiKey;
            _contractsPath = contractsPath;
            _validator     = new ManifestValidator(contractsPath);
        }

        public async Task<ManifestGenerateResult> GenerateAsync(string userDescription)
        {
            var opNames      = LoadOpNames();
            var systemPrompt = BuildSystemPrompt(opNames);
            var userMsg      = $"Şu isteği manifest JSON olarak yaz:\n\n{userDescription}";

            string rawJson = "";
            ValidationResult? lastVr = null;

            for (int attempt = 0; attempt <= MaxRetry; attempt++)
            {
                try
                {
                    var msgs = attempt == 0
                        ? new object[] { new { role = "user", content = userMsg } }
                        : new object[]
                          {
                              new { role = "user",      content = userMsg },
                              new { role = "assistant", content = rawJson },
                              new { role = "user",      content = ManifestValidator.BuildFixPrompt(rawJson, lastVr!) }
                          };
                    // ConfigureAwait(false) — Revit WPF SynchronizationContext deadlock'unu önler
                    rawJson = await CallAnthropicAsync(systemPrompt, msgs).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return new ManifestGenerateResult { Success = false, ErrorMessage = $"API ({attempt+1}): {ex.Message}" };
                }

                lastVr = _validator.Validate(rawJson);
                if (lastVr.IsValid)
                    return new ManifestGenerateResult
                    {
                        Success      = true,
                        Manifest     = lastVr.Manifest,
                        RawJson      = FormatJson(rawJson),
                        Warnings     = lastVr.Warnings,
                        AttemptCount = attempt + 1
                    };

                if (attempt == MaxRetry) break;
            }

            return new ManifestGenerateResult
            {
                Success          = false,
                ErrorMessage     = $"{MaxRetry+1} denemede doğrulama başarısız:\n{lastVr?.ErrorSummary}",
                RawJson          = rawJson,
                ValidationErrors = lastVr?.Errors.Select(e => $"[{e.JsonPath}] {e.Code}: {e.Message}").ToList()
            };
        }

        private string BuildSystemPrompt(List<string> opNames)
        {
            var opList = string.Join(", ", opNames);
            return $@"Sen EGBIMOTO BIM otomasyon platformu için manifest JSON yazan asistansın.

## EN ÖNEMLİ KURAL
Sadece aşağıdaki {opNames.Count} op adını kullan. ASLA başka op adı uydurma.

## KULLANILABILIR OP'LAR ({opNames.Count} adet)
{opList}

## MANIFEST ŞEMASI
{{""title"":""Türkçe başlık"",""description"":""Açıklama"",""category"":""kategori"",""version"":""1.0.0"",
""pre_checks"":[{{""type"":""MODEL_HAS_ELEMENTS"",""categories"":[""OST_Walls""],""min_count"":1,""on_fail"":""ABORT"",""message"":""Eleman bulunamadı""}}],
""steps"":[
  {{""id"":""s1"",""op"":""op_adi""}},
  {{""id"":""s2"",""op"":""op_adi"",""from"":""s1""}},
  {{""id"":""s3"",""op"":""op_adi"",""from_many"":[""s1"",""s2""]}},
  {{""id"":""s4"",""op"":""export_xlsx"",""from"":""s3"",""required"":false,""params"":{{""sheet_name"":""Rapor""}}}}
]}}

## 8 WORKFLOW FAZI
PRECHECK → COLLECT → NORMALIZE → VALIDATE/TRANSFORM → AGGREGATE → REPORT → EXPORT → TRACE

## 8 PATTERN
QA_PATTERN: collect → param_*_check → merge_validation_reports → validation_summary → validation_to_rows → export_validation_report + export_xlsx
BOQ_PATTERN: load_poz_data → collect → kalip_all/element_area → poz_match → calc_cost → cost_summary → export_xlsx
MEP_PATTERN: collect_ducts/collect_pipes → mep_by_system → mep_total_length → show_table → export_xlsx
CALC_PATTERN: calc_lap_length/calc_anchorage_length (params ile) → show_result
ROOM_PATTERN: collect_rooms → check_unplaced_rooms+check_overlapping_rooms → merge_validation_reports → validation_summary
RENAME_PATTERN: collect → rename_preview → show_table → rename_apply
IFC_PATTERN: load_ifc_mapping → collect → validate_required_params → map_to_ifc → ifc_export
SCRIPT_PATTERN: collect (opsiyonel) → run_csharp_script (script_path ile) → show_table/export_xlsx

## run_csharp_script KULLANIMI
Kullanıcı özel .cs dosyası belirttiğinde veya standart op'larla yapılamayan işlem istediğinde kullan.
params: script_path (zorunlu — .cs dosyasının yolu), cache (opsiyonel, default:true)
Script globals: uiapp, doc, inputs (diğer params), input (from bağlantısı)
Örnek: {{""id"":""hesap"",""op"":""run_csharp_script"",""from"":""collect"",""params"":{{""script_path"":""scripts/ozel_hesap.cs""}}}}

## 10 KURAL
1. Sadece verilen op listesindeki adlar — uydurma yok.
2. Step id'leri benzersiz (snake_case).
3. from/from_many sadece daha önce tanımlı step id'ye bağlanmalı.
4. Her manifestte pre_checks olmalı.
5. Kalıcı yazan op'lardan önce required:false veya preview adımı.
6. QA manifestlerinde: validation_summary + validation_to_rows + export_validation_report.
7. Export adımları required:false.
8. Başlık ve rapor adları Türkçe.
9. SADECE geçerli JSON döndür — markdown blok, açıklama, ön söz yazma.
10. $INPUT:tip:etiket token'ı kullanıcı girişi gerektiren parametreler için kullanılabilir.

## ÖRNEK — QA
{{""title"":""Parametre QA"",""description"":""EGBIM_PozNo doluluk kontrolü"",""category"":""dogrulama"",""version"":""1.0.0"",
""pre_checks"":[{{""type"":""MODEL_HAS_ELEMENTS"",""categories"":[""OST_Walls"",""OST_StructuralColumns""],""min_count"":1,""on_fail"":""ABORT"",""message"":""Eleman bulunamadı""}}],
""steps"":[
  {{""id"":""duvarlar"",""op"":""collect_walls""}},
  {{""id"":""kolonlar"",""op"":""collect_columns""}},
  {{""id"":""qa_d"",""op"":""param_filled_check"",""from"":""duvarlar"",""params"":{{""param_name"":""EGBIM_PozNo"",""severity"":""ERROR""}}}},
  {{""id"":""qa_k"",""op"":""param_filled_check"",""from"":""kolonlar"",""params"":{{""param_name"":""EGBIM_PozNo"",""severity"":""ERROR""}}}},
  {{""id"":""birlesik"",""op"":""merge_validation_reports"",""from_many"":[""qa_d"",""qa_k""],""params"":{{""title"":""Parametre QA""}}}},
  {{""id"":""ozet"",""op"":""validation_summary"",""from"":""birlesik""}},
  {{""id"":""satirlar"",""op"":""validation_to_rows"",""from"":""birlesik""}},
  {{""id"":""html"",""op"":""export_validation_report"",""from"":""birlesik"",""required"":false,""params"":{{""title"":""Param QA""}}}},
  {{""id"":""xlsx"",""op"":""export_xlsx"",""from"":""satirlar"",""required"":false,""params"":{{""sheet_name"":""Param QA""}}}}
]}}";
        }

        private async Task<string> CallAnthropicAsync(string systemPrompt, object[] messages)
        {
            var body = JsonSerializer.Serialize(new { model = Model, max_tokens = MaxTokens, system = systemPrompt, messages });
            using var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(req).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Anthropic API bağlantısı kurulamadı. İnternet bağlantısını ve firewall ayarlarını kontrol edin.\n" +
                    $"Detay: {ex.Message}", ex);
            }
            catch (TaskCanceledException)
            {
                throw new InvalidOperationException("Anthropic API isteği zaman aşımına uğradı (60 saniye). Tekrar deneyin.");
            }

            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var hint = resp.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized    => "API anahtarı geçersiz veya eksik.",
                    System.Net.HttpStatusCode.TooManyRequests => "Rate limit aşıldı. Birkaç saniye bekleyip tekrar deneyin.",
                    System.Net.HttpStatusCode.BadRequest      => "İstek formatı hatalı.",
                    _ => $"HTTP {(int)resp.StatusCode}"
                };
                throw new InvalidOperationException($"{hint}\nYanıt: {errBody[..Math.Min(200, errBody.Length)]}");
            }

            var respJson = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(respJson);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            return CleanJson(text);
        }

        private List<string> LoadOpNames()
        {
            try
            {
                if (!File.Exists(_contractsPath)) return new List<string>();
                using var doc = JsonDocument.Parse(File.ReadAllText(_contractsPath, Encoding.UTF8));
                return doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();
            }
            catch { return new List<string>(); }
        }

        private static string CleanJson(string raw)
        {
            var s = raw.Trim();
            if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) s = s[7..];
            else if (s.StartsWith("```")) s = s[3..];
            if (s.EndsWith("```")) s = s[..^3];
            return s.Trim();
        }

        private static string FormatJson(string json)
        {
            try { return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json), new JsonSerializerOptions { WriteIndented = true }); }
            catch { return json; }
        }
    }

    public sealed class ManifestGenerateResult
    {
        public bool          Success          { get; init; }
        public EgManifest?   Manifest         { get; init; }
        public string?       RawJson          { get; init; }
        public string?       ErrorMessage     { get; init; }
        public List<string>? Warnings         { get; init; }
        public List<string>? ValidationErrors { get; init; }
        public int           AttemptCount     { get; init; } = 1;
    }
}
