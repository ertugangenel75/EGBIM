using System.Collections.Generic;
using EGBIMOTO.Core.AI;
using Xunit;

namespace EGBIMOTO.Core.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ManifestValidatorTests — EGBIMOTO v8
    //
    //  ManifestValidator: AI üretici ve Pattern engine tarafından üretilen
    //  JSON'ları op_contracts.json karşısında doğrular.
    //
    //  Test edilen özellikler:
    //    1. Geçerli manifest → IsValid=true
    //    2. Başlık eksik → hata
    //    3. Steps boş → hata
    //    4. Bilinmeyen op → hata
    //    5. from geçersiz referans → hata
    //    6. Duplicate step id → hata
    //    7. Geçersiz JSON → hata
    //    8. Uyarı: summary adımı yok
    //    9. Uyarı: export adımı yok
    //   10. BuildFixPrompt: hata listesini içerir
    // ═══════════════════════════════════════════════════════════════════════════

    public class ManifestValidatorTests
    {
        private static readonly IEnumerable<string> KnownOps =
            new[] { "collect_walls", "filter_by_param", "show_table", "export_xlsx", "param_filled_check" };

        private static ManifestValidator Validator() => new(KnownOps);

        private const string ValidManifest = @"{
            ""title"": ""Test Manifest"",
            ""steps"": [
                { ""id"": ""topla"", ""op"": ""collect_walls"" },
                { ""id"": ""goster"", ""op"": ""show_table"", ""from"": ""topla"" }
            ]
        }";

        [Fact(DisplayName = "V01 — Geçerli manifest IsValid=true döner")]
        public void ValidJson_IsValid()
        {
            var result = Validator().Validate(ValidManifest);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact(DisplayName = "V02 — Başlık eksik → FIELD_MISSING hatası")]
        public void MissingTitle_Error()
        {
            var json = @"{ ""steps"": [{ ""id"": ""s1"", ""op"": ""collect_walls"" }] }";
            var result = Validator().Validate(json);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ToString().Contains("title"));
        }

        [Fact(DisplayName = "V03 — Steps boş array → FIELD_MISSING hatası")]
        public void EmptySteps_Error()
        {
            var json = @"{ ""title"": ""Test"", ""steps"": [] }";
            var result = Validator().Validate(json);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ToString().Contains("steps"));
        }

        [Fact(DisplayName = "V04 — Bilinmeyen op → OP_UNKNOWN hatası")]
        public void UnknownOp_Error()
        {
            var json = @"{
                ""title"": ""Test"",
                ""steps"": [{ ""id"": ""s1"", ""op"": ""uydurma_op"" }]
            }";
            var result = Validator().Validate(json);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ToString().Contains("OP_UNKNOWN") && e.ToString().Contains("uydurma_op"));
        }

        [Fact(DisplayName = "V05 — from geçersiz referans → FROM_INVALID hatası")]
        public void InvalidFrom_Error()
        {
            var json = @"{
                ""title"": ""Test"",
                ""steps"": [
                    { ""id"": ""s1"", ""op"": ""collect_walls"", ""from"": ""yok"" }
                ]
            }";
            var result = Validator().Validate(json);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ToString().Contains("FROM_INVALID"));
        }

        [Fact(DisplayName = "V06 — Duplicate step id → STEP_ID_DUPLICATE hatası")]
        public void DuplicateStepId_Error()
        {
            var json = @"{
                ""title"": ""Test"",
                ""steps"": [
                    { ""id"": ""s1"", ""op"": ""collect_walls"" },
                    { ""id"": ""s1"", ""op"": ""show_table"" }
                ]
            }";
            var result = Validator().Validate(json);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ToString().Contains("STEP_ID_DUPLICATE") && e.ToString().Contains("s1"));
        }

        [Fact(DisplayName = "V07 — Geçersiz JSON → JSON_PARSE hatası")]
        public void InvalidJson_ParseError()
        {
            var result = Validator().Validate("{ bu geçersiz json }");
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ToString().Contains("JSON_PARSE"));
        }

        [Fact(DisplayName = "V08 — Summary adımı yok → WARN_NO_SUMMARY uyarısı")]
        public void NoSummaryStep_Warning()
        {
            var json = @"{
                ""title"": ""Test"",
                ""steps"": [
                    { ""id"": ""topla"", ""op"": ""collect_walls"" },
                    { ""id"": ""xls"", ""op"": ""export_xlsx"", ""from"": ""topla"" }
                ]
            }";
            var result = Validator().Validate(json);
            Assert.True(result.IsValid);  // uyarı hata değil
            Assert.Contains(result.Warnings, w => w.Contains("WARN_NO_SUMMARY"));
        }

        [Fact(DisplayName = "V09 — Export adımı yok → WARN_NO_EXPORT uyarısı")]
        public void NoExportStep_Warning()
        {
            var json = @"{
                ""title"": ""Test"",
                ""steps"": [
                    { ""id"": ""topla"", ""op"": ""collect_walls"" },
                    { ""id"": ""goster"", ""op"": ""show_table"", ""from"": ""topla"" }
                ]
            }";
            var result = Validator().Validate(json);
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Contains("WARN_NO_EXPORT"));
        }

        [Fact(DisplayName = "V10 — BuildFixPrompt: hata listesi ve orijinal JSON içerir")]
        public void BuildFixPrompt_ContainsErrorsAndJson()
        {
            var json = @"{ ""steps"": [{ ""id"": ""s1"", ""op"": ""uydurma"" }] }";
            var result = Validator().Validate(json);
            var prompt = ManifestValidator.BuildFixPrompt(json, result);

            Assert.Contains("OP_UNKNOWN", prompt);
            Assert.Contains(json,         prompt);
        }
    }
}
