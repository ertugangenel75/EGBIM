using System;
using System.Collections.Generic;
using System.Linq;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Core.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — Typed IO Contracts + ManifestLinter  (v3.2)
    //
    //  • IEgOpOutput         → op çıktıları için marker interface
    //  • EgOpContractAttribute → op giriş/çıktı tiplerini tanımlar (ileride linter)
    //  • EgInputTypeMismatchException → yanlış from referansında net hata
    //  • ManifestLinter      → manifest çalıştırılmadan önce 7 statik kontrol
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Tüm typed op çıktıları bu interface'i uygular.</summary>
    public interface IEgOpOutput { }

    /// <summary>
    /// Yanlış tip bağlandığında fırlatılır.
    /// ctx.InputAs&lt;T&gt;() çağrısı başarısız olursa bu exception üretilir.
    /// ManifestRunner / DagExecutor always-critical olarak yakalar.
    /// </summary>
    public sealed class EgInputTypeMismatchException : InvalidOperationException
    {
        public string ExpectedType { get; }
        public string ActualType   { get; }
        public string StepId       { get; }

        public EgInputTypeMismatchException(string expectedType, string actualType, string stepId)
            : base($"[TypeContract] Beklenen giriş tipi: {expectedType}, " +
                   $"gelen: {actualType}.\n" +
                   $"Step: '{stepId}' — manifest'te 'from' referansını kontrol et.")
        {
            ExpectedType = expectedType;
            ActualType   = actualType;
            StepId       = stepId;
        }
    }

    /// <summary>
    /// Op'un beklediği giriş ve ürettiği çıktı tiplerini tanımlar.
    /// ManifestLinter bu metadata'yı tip uyumluluk kontrolü için kullanır.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class EgOpContractAttribute : Attribute
    {
        /// <summary>InputAs&lt;T&gt;() ile beklenen tip. null → herhangi bir girdi kabul edilir.</summary>
        public Type? InputType  { get; init; }
        /// <summary>Op'un döndüğü tip. null → object?</summary>
        public Type? OutputType { get; init; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ManifestLinter — manifest çalıştırılmadan önce statik analiz
    //
    //  Kullanım:
    //    var result = ManifestLinter.Lint(manifest, EgbimotoApp.Registry);
    //    if (!result.IsValid)
    //        TaskDialog.Show("Lint Hatası", result.ToString());
    //
    //  Yapılan 7 kontrol:
    //    1. Op kayıtlı mı?
    //    2. from referansı geçerli bir step ID mi?
    //    3. from_many referansları geçerli mi?
    //    4. depends_on referansları geçerli mi?
    //    5. Duplicate step ID var mı?
    //    6. Boş op adı var mı?
    //    7. EgOpContractAttribute ile tip uyumu (contract tanımlıysa)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Lint sonucu — hata ve uyarı listesi.</summary>
    // ── ManifestTag — Browser rozeti için ─────────────────────────────────────

    /// <summary>
    /// Manifest Browser'da gösterilen durum rozeti.
    /// ManifestLinter.Lint() tarafından hesaplanır; sıra önceliğe göre:
    ///   MissingOp  → hata (kayıt dışı op)
    ///   BadRef     → hata (geçersiz from/depends_on referansı)
    ///   Preview    → bilgi (transaction_policy: preview)
    ///   Atomic     → bilgi (transaction_policy: atomic)
    ///   Warning    → uyarı (required=false gibi)
    ///   OK         → temiz
    /// </summary>
    public enum ManifestTag
    {
        OK         = 0,
        Warning    = 1,
        Atomic     = 2,
        Preview    = 3,
        BadRef     = 4,
        MissingOp  = 5,   // En yüksek öncelik
    }

    public sealed class LintResult
    {
        public bool            IsValid  { get; init; }
        public List<LintIssue> Errors   { get; init; } = new();
        public List<LintIssue> Warnings { get; init; } = new();

        /// <summary>Browser rozetinde gösterilecek durum. Lint() tarafından hesaplanır.</summary>
        public ManifestTag Tag { get; init; } = ManifestTag.OK;

        /// <summary>Rozet rengi (WPF hex string).</summary>
        public string TagColor => Tag switch
        {
            ManifestTag.MissingOp => "#FF5555",   // kırmızı
            ManifestTag.BadRef    => "#FFB86C",   // turuncu
            ManifestTag.Preview   => "#BD93F9",   // mor
            ManifestTag.Atomic    => "#8BE9FD",   // mavi
            ManifestTag.Warning   => "#F1FA8C",   // sarı
            ManifestTag.OK        => "#50FA7B",   // yeşil
            _                    => "#888888"
        };

        /// <summary>Rozet kısa metni.</summary>
        public string TagText => Tag switch
        {
            ManifestTag.MissingOp => "Missing OP",
            ManifestTag.BadRef    => "Bad Ref",
            ManifestTag.Preview   => "Preview",
            ManifestTag.Atomic    => "Atomic",
            ManifestTag.Warning   => "Warning",
            ManifestTag.OK        => "OK",
            _                    => "?"
        };

        public override string ToString()
        {
            var lines = new List<string>();
            lines.Add(IsValid ? "✅ Manifest geçerli" : "❌ Manifest geçersiz");
            foreach (var e in Errors)   lines.Add($"  ✗ [{e.StepId}] {e.Message}");
            foreach (var w in Warnings) lines.Add($"  ⚠ [{w.StepId}] {w.Message}");
            return string.Join("\n", lines);
        }
    }

    public sealed class LintIssue
    {
        public string StepId  { get; init; } = "";
        public string Message { get; init; } = "";
    }

    /// <summary>
    /// Manifest'i çalıştırmadan önce statik analiz yapar.
    /// EgManifest + OpRegistry tiplerini kullanır (EGBIMOTO.Core native).
    /// </summary>
    public static class ManifestLinter
    {
        /// <summary>
        /// Manifest'i 7 kurala göre denetler.
        /// Hata varsa IsValid=false, Errors dolu döner.
        /// </summary>
        public static LintResult Lint(EgManifest manifest, OpRegistry registry)
        {
            var errors   = new List<LintIssue>();
            var warnings = new List<LintIssue>();
            var steps    = manifest.Steps ?? new List<EgStep>();

            // ── Kontrol 5: Duplicate step ID ─────────────────────────────────
            foreach (var dup in steps.GroupBy(s => s.Id)
                                     .Where(g => g.Count() > 1)
                                     .Select(g => g.Key))
                errors.Add(E(dup, $"Duplicate step ID: '{dup}'"));

            var stepIds = new HashSet<string>(
                steps.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var step in steps)
            {
                // ── Kontrol 6: Boş op adı ────────────────────────────────────
                if (string.IsNullOrWhiteSpace(step.Op))
                {
                    errors.Add(E(step.Id, "Op adı boş olamaz"));
                    continue;
                }

                // ── Kontrol 1: Op kayıtlı mı? ────────────────────────────────
                if (!registry.Has(step.Op))
                {
                    errors.Add(E(step.Id, $"Op kayıtlı değil: '{step.Op}'"));
                    continue;
                }

                // ── Kontrol 2: from referansı geçerli mi? ────────────────────
                if (!string.IsNullOrEmpty(step.From) && !stepIds.Contains(step.From!))
                    errors.Add(E(step.Id,
                        $"'from' referansı geçersiz: '{step.From}' adında step yok"));

                // ── Kontrol 3: from_many referansları ────────────────────────
                if (step.FromMany != null)
                    foreach (var fm in step.FromMany.Where(fm => !stepIds.Contains(fm)))
                        errors.Add(E(step.Id,
                            $"'from_many' referansı geçersiz: '{fm}' adında step yok"));

                // ── Kontrol 4: depends_on referansları ───────────────────────
                if (step.DependsOn != null)
                    foreach (var dep in step.DependsOn.Where(d => !stepIds.Contains(d)))
                        errors.Add(E(step.Id,
                            $"'depends_on' referansı geçersiz: '{dep}' adında step yok"));

                // ── Kontrol 7: EgOpContractAttribute tip uyumu ───────────────
                // Çalışma zamanında reflection ile — contract tanımlıysa kontrol eder
                try
                {
                    var method = registry.GetMethod(step.Op);
                    if (method is null) continue;

                    var contract = method
                        .GetCustomAttributes(typeof(EgOpContractAttribute), false)
                        .FirstOrDefault() as EgOpContractAttribute;

                    if (contract?.InputType != null && !string.IsNullOrEmpty(step.From))
                    {
                        var fromStep = steps.FirstOrDefault(s =>
                            string.Equals(s.Id, step.From, StringComparison.OrdinalIgnoreCase));

                        if (fromStep != null && registry.Has(fromStep.Op))
                        {
                            var fromMethod   = registry.GetMethod(fromStep.Op);
                            var fromContract = fromMethod?
                                .GetCustomAttributes(typeof(EgOpContractAttribute), false)
                                .FirstOrDefault() as EgOpContractAttribute;

                            if (fromContract?.OutputType != null &&
                                !contract.InputType.IsAssignableFrom(fromContract.OutputType))
                                errors.Add(E(step.Id,
                                    $"Tip uyumsuzluğu: '{step.Op}' {contract.InputType.Name} bekliyor, " +
                                    $"'{fromStep.Op}' {fromContract.OutputType.Name} üretiyor"));
                        }
                    }
                }
                catch { /* reflection hatası → sessizce atla */ }
            }

            // ── Tag hesapla (Browser rozeti için) ────────────────────────────
            var tag = ManifestTag.OK;
            if (errors.Any(e => e.Message.Contains("kayıtlı değil")))
                tag = ManifestTag.MissingOp;
            else if (errors.Any(e => e.Message.Contains("referans") || e.Message.Contains("bulunamadı")))
                tag = ManifestTag.BadRef;
            else if (errors.Count > 0)
                tag = ManifestTag.BadRef;
            else if (manifest.IsPreview)
                tag = ManifestTag.Preview;
            else if (manifest.IsAtomic)
                tag = ManifestTag.Atomic;
            else if (warnings.Count > 0)
                tag = ManifestTag.Warning;

            return new LintResult
            {
                IsValid  = errors.Count == 0,
                Errors   = errors,
                Warnings = warnings,
                Tag      = tag
            };
        }

        private static LintIssue E(string stepId, string msg)
            => new() { StepId = stepId, Message = msg };
    }
}
