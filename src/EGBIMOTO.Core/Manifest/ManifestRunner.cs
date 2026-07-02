using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EGBIMOTO.Core.Cache;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Core.Manifest
{
    public sealed class ManifestRunResult
    {
        public bool                         Success      { get; init; }
        public string?                      ErrorMessage { get; init; }
        public string?                      ErrorStep    { get; init; }
        public Dictionary<string, object?>  Vars         { get; init; } = new();
        public List<string>                 Log          { get; init; } = new();

        // v10 — Telemetri (UI sonuç paneli için). DagExecutor doldurur.
        public int      TotalSteps    { get; init; }
        public int      CachedSteps   { get; init; }
        public int      SkippedSteps  { get; init; }
        public long     DurationMs    { get; init; }
    }

    /// <summary>
    /// Manifest'i adım adım çalıştırır.
    ///
    /// v3.1 eklemeleri:
    ///  • ctx.CurrentStepId set edilir → InputAs&lt;T&gt;() hata mesajları için
    ///  • Çıktı tipi loglanır (IEgOpOutput uygulayan op'lar için)
    ///  • AtomicCommit / AtomicRollback callback desteği (Addin katmanından gelir)
    ///
    /// Kullanım:
    ///   var runner = new ManifestRunner(registry, () => new RevitOpContext { Doc = doc });
    ///   var result = runner.Run(manifest);
    ///
    /// Atomic kullanım (EgbimotoApp tarafından yönetilir):
    ///   runner.OnAtomicCommit  = () => outerTx.Commit();
    ///   runner.OnAtomicRollback = () => outerTx.RollBack();
    /// </summary>
    /// <summary>
    /// v3.2 itibarıyla ManifestRunner yerine DagExecutor kullanın.
    /// DagExecutor: topolojik sıralama, condition, depends_on, $varName param çözümleme.
    /// Bu sınıf geriye uyumluluk için korunmaktadır. EgbimotoApp artık DagExecutor çalıştırır.
    /// </summary>
    [Obsolete("ManifestRunner v3.2'de DagExecutor ile değiştirildi. EgbimotoApp.RunManifest() kullanın.")]
    public sealed class ManifestRunner
    {
        private readonly OpRegistry       _registry;
        private readonly Func<OpContext>  _contextFactory;
        private readonly NodeCacheService _cache;

        /// <summary>
        /// Manifest "atomic" policy ile başarıyla bittiğinde çağrılır.
        /// Addin katmanı outer Transaction'ı commit eder.
        /// </summary>
        public Action? OnAtomicCommit   { get; set; }

        /// <summary>
        /// Manifest "atomic" policy ile hata verdiğinde çağrılır.
        /// Addin katmanı outer Transaction'ı rollback eder.
        /// </summary>
        public Action? OnAtomicRollback { get; set; }

        public ManifestRunner(OpRegistry registry, Func<OpContext> contextFactory,
            NodeCacheService? cache = null)
        {
            _registry       = registry;
            _contextFactory = contextFactory;
            _cache          = cache ?? new NodeCacheService();
        }

        public ManifestRunResult Run(EgManifest manifest, Action<string>? externalLogger = null)
        {
            var vars = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var log  = new List<string>();

            void WriteLog(string msg)
            {
                log.Add(msg);
                externalLogger?.Invoke(msg);
                System.Diagnostics.Debug.WriteLine($"[EGRunner] {msg}");
            }

            var policyStr = manifest.IsAtomic ? " [ATOMIC]" : "";
            WriteLog($"▶ {manifest.Title} başlatılıyor — {manifest.Steps.Count} adım{policyStr}");

            foreach (var step in manifest.Steps)
            {
                // ── Girdi çözümleme ──────────────────────────────────────────
                object? input = null;

                if (!string.IsNullOrEmpty(step.From))
                {
                    if (!vars.TryGetValue(step.From, out input))
                        WriteLog($"  ⚠ '{step.Id}': 'from' referansı '{step.From}' bulunamadı — null kullanılıyor");
                }
                else if (step.FromMany is { Count: > 0 })
                {
                    input = step.FromMany
                        .Select(id => vars.TryGetValue(id, out var v) ? v : null)
                        .ToList();
                }

                // ── Cache kontrolü ───────────────────────────────────────────
                var cacheKey = BuildCacheKey(step, input);
                if (step.Cache == true && _cache.TryGet(cacheKey, out var cached))
                {
                    vars[step.Id] = cached;
                    WriteLog($"  ⚡ [{step.Id}] {step.Op} (cache hit)");
                    continue;
                }

                // ── Op çalıştırma ────────────────────────────────────────────
                try
                {
                    var ctx = _contextFactory();
                    ctx.CurrentStepId = step.Id;          // v3.1: InputAs<T>() için
                    ctx.Input  = input;
                    ctx.Params = step.Params ?? new Dictionary<string, object?>();
                    ctx.Vars   = vars;
                    ctx.Log    = WriteLog;

                    var result = _registry.Execute(step.Op, ctx);
                    vars[step.Id] = result;

                    // v3.1: Typed output loglama
                    var typeTag = result is IEgOpOutput
                        ? $" → {result.GetType().Name}"
                        : "";
                    WriteLog($"  ✓ [{step.Id}] {step.Op}{typeTag}");

                    // Cache'e yaz
                    if (step.Cache == true)
                        _cache.Set(cacheKey, result);
                }
                catch (EgInputTypeMismatchException ex)
                {
                    // Tip uyumsuzluğu — her zaman kritik, required flag'ini atlar
                    var msg = $"  ✗ [{step.Id}] TİP UYUMSUZLUĞU: {ex.Message}";
                    WriteLog(msg);
                    OnAtomicRollback?.Invoke();
                    return new ManifestRunResult
                    {
                        Success      = false,
                        ErrorMessage = msg,
                        ErrorStep    = step.Id,
                        Vars         = vars,
                        Log          = log
                    };
                }
                catch (Exception ex)
                {
                    var msg = $"  ✗ [{step.Id}] {step.Op}: {ex.InnerException?.Message ?? ex.Message}";
                    WriteLog(msg);

                    if (step.Required)
                    {
                        OnAtomicRollback?.Invoke();
                        return new ManifestRunResult
                        {
                            Success      = false,
                            ErrorMessage = msg,
                            ErrorStep    = step.Id,
                            Vars         = vars,
                            Log          = log
                        };
                    }
                }
            }

            WriteLog($"✅ {manifest.Title} tamamlandı");
            OnAtomicCommit?.Invoke();
            return new ManifestRunResult { Success = true, Vars = vars, Log = log };
        }

        private static string BuildCacheKey(EgStep step, object? input)
        {
            var paramStr  = step.Params is null ? "" :
                JsonSerializer.Serialize(step.Params, new JsonSerializerOptions { WriteIndented = false });
            var inputHash = input?.GetHashCode().ToString() ?? "null";
            return $"{step.Op}|{paramStr}|{inputHash}";
        }

        public void ClearCache() => _cache.Clear();
    }
}
