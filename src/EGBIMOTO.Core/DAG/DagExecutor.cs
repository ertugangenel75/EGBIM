// Copyright 2026 Ertuğan Genel
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using EGBIMOTO.Core.Cache;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Preview;
using EGBIMOTO.Core.Schedule;
using EGBIMOTO.Core.Selection;

namespace EGBIMOTO.Core.DAG
{
    // ── Ana executor ──────────────────────────────────────────────────────────

    /// <summary>
    /// DAG tabanlı manifest yürütücü.
    ///
    /// v4.1 — Preview-Confirm desteği:
    ///   • UserGateCallback — "preview_gate" op'unu intercept eder
    ///   • "preview_gate" op'u registry'e çağrı YAPILMAZ — callback çağrılır
    ///   • Callback null ise gate otomatik onaylanır (headless/test modu)
    ///   • vars[gate_step_id] = "confirmed" | "cancelled"
    ///
    /// v3.2 özellikleri korunuyor:
    ///   • ctx.CurrentStepId, EgInputTypeMismatchException
    ///   • OnAtomicCommit / OnAtomicRollback
    ///   • RequiresTransaction fix
    ///
    /// Orijinal özellikler:
    ///   • Topolojik sıralama + döngü tespiti
    ///   • "condition", "depends_on", "$varName" params
    ///   • DagTraceRow ms timing
    /// </summary>
    public sealed class DagExecutor
    {
        // ── Gate sabitleri ────────────────────────────────────────────────────
        private const string PREVIEW_GATE_OP   = "preview_gate";
        private const string SCHEDULE_GATE_OP  = "schedule_gate";
        private const string SELECTION_GATE_OP = "selection_gate";   // v13.5

        private readonly OpRegistry       _registry;
        private readonly Func<OpContext>  _contextFactory;
        private readonly NodeCacheService _cache;
        private readonly DagPlanner       _planner;

        // ── Atomic transaction callback'leri ──────────────────────────────────
        public Action? OnAtomicCommit   { get; set; }
        public Action? OnAtomicRollback { get; set; }

        // ── v4.1: Preview-Confirm gate callback ──────────────────────────────
        /// <summary>
        /// "preview_gate" op'u için kullanıcı onay callback'i.
        ///
        /// EgbimotoApp.RunPreviewManifest() tarafından set edilir.
        /// null ise gate otomatik onaylanır (test/headless mod için).
        ///
        /// Parametre : önceki adımın ürettiği PreviewGeometryDto
        ///             (null ise boş dto ile çağrılır)
        /// Dönüş     : true = onaylandı, false = iptal
        ///
        /// Thread    : DagExecutor senkron çalışır → callback da senkron
        ///             WPF ShowDialog() burada bloklama yapar (doğru davranış)
        /// </summary>
        public Func<PreviewGeometryDto, bool>? UserGateCallback { get; set; }

        // ── v6.0: 4D/5D Schedule gate callback ───────────────────────────────
        /// <summary>
        /// "schedule_gate" op'u için kullanıcı onay callback'i.
        ///
        /// EgbimotoApp4D5D.Run4D5DManifest() tarafından set edilir.
        /// null ise gate otomatik onaylanır.
        ///
        /// Parametre : schedule_collect_4d/5d'nin ürettiği FourDFiveDDto
        /// Dönüş     : true = onaylandı, false = iptal
        /// </summary>
        public Func<FourDFiveDDto, bool>? UserScheduleGateCallback { get; set; }

        // ── v13.5: İnteraktif seçim gate callback'i ───────────────────────────
        /// <summary>
        /// "selection_gate" op'u için kullanıcı seçim callback'i.
        ///
        /// EgbimotoApp.RunManifest() / EgbimotoAppPreviewExtension tarafından
        /// SelectionPickerService.CreateCallback(uidoc) ile set edilir.
        ///
        /// Diğer gate'lerden farkı: preview_gate/schedule_gate önceden üretilmiş
        /// bir DTO'yu ONAYLATIR; selection_gate ise DTO'yu BİZZAT ÜRETİR — girdi
        /// olarak SelectionRequestDto alır (step.Params'tan inşa edilir), çıktı
        /// olarak SelectionResultDto döner.
        ///
        /// null ise (headless/test modu) seçim YAPILAMAZ — otomatik onay yerine
        /// Cancelled=true boş sonuç döner (preview_gate'in aksine; rastgele
        /// eleman "seçmek" anlamsız ve tehlikelidir).
        /// </summary>
        public Func<Selection.SelectionRequestDto, Selection.SelectionResultDto>? UserSelectionCallback { get; set; }

        // ── v4.1: Phase 1 gate-stop ───────────────────────────────────────────
        /// <summary>
        /// true → preview_gate tetiklendikten sonra kalan adımları çalıştırma.
        ///
        /// EgbimotoAppPreviewExtension.RunPreviewManifest() Phase 1 executor'una
        /// bu flag'i set eder. Phase 1 sadece gate'e kadar çalışır — gate sonrası
        /// yazma adımları Phase 3'te atomic TX içinde çalışır.
        ///
        /// false (varsayılan) → gate sonrası adımlar condition'ları ile devam eder.
        /// </summary>
        public bool StopAfterGate { get; set; } = false;

        // ── v4.1: Phase 1 → Phase 3 vars aktarımı ────────────────────────────
        /// <summary>
        /// Run() başlamadan önce vars'a eklenen ön-yüklenmiş değişkenler.
        ///
        /// Preview-Confirm akışında:
        ///   Phase 1'in DagRunResult.Vars'ı (gate="confirmed" dahil)
        ///   Phase 3 executor'una buradan aktarılır.
        ///
        ///   Sonuç: Phase 3'te condition="$gate == confirmed" adımlar
        ///   doğrudan geçer; geometri yeniden toplanmaz (step ID'si
        ///   vars'ta zaten var, atlanır ya da cache hit oluşur).
        /// </summary>
        public IReadOnlyDictionary<string, object?>? InitialVars { get; set; }

        public DagExecutor(
            OpRegistry       registry,
            Func<OpContext>  contextFactory,
            NodeCacheService? cache = null)
        {
            _registry       = registry       ?? throw new ArgumentNullException(nameof(registry));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _cache          = cache          ?? new NodeCacheService();
            _planner        = new DagPlanner(_registry);
        }

        // ── Ana çalıştırma ────────────────────────────────────────────────────

        // ── v8.0: Adım bazlı UI progress callback ────────────────────────────
        /// <summary>
        /// Her adım tamamlandığında çağrılır. UI progress güncellemesi için kullanılır.
        /// (stepId, op, durationMs, success) parametreleriyle çağrılır.
        /// CommandQueue pattern'inden ilham: her op sonucu bağımsız raporlanır.
        /// </summary>
        public Action<string, string, long, bool>? OnStepCompleted { get; set; }

        public DagRunResult Run(EgManifest manifest, Action<string>? externalLogger = null)
        {
            var vars          = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var trace         = new List<DagTraceRow>();
            var log           = new List<string>();
            var anyStepFailed = false;

            // ── InitialVars merge (Phase 1 → Phase 3 aktarımı) ───────────────
            if (InitialVars != null)
                foreach (var kv in InitialVars)
                    vars[kv.Key] = kv.Value;

            void WriteLog(string msg)
            {
                log.Add(msg);
                externalLogger?.Invoke(msg);
                System.Diagnostics.Debug.WriteLine($"[DagExecutor] {msg}");
            }

            // ── Plan ─────────────────────────────────────────────────────────
            var plan = _planner.Build(manifest);
            if (!plan.Success)
            {
                WriteLog($"✗ Plan hatası: {plan.Error}");
                return Fail(plan.Error!, null, vars, trace, log);
            }

            var policyStr = manifest.IsAtomic ? " [ATOMIC]" :
                            manifest.IsPreview ? " [PREVIEW]" : "";
            WriteLog($"▶ {manifest.Title} — {plan.Steps.Count} adım, {plan.Levels.Count} seviye{policyStr}");
            foreach (var diag in plan.Diagnostics)
                WriteLog($"  📐 {diag}");

            var runStart = DateTime.UtcNow;
            int cachedCount  = 0;
            int skippedCount = 0;

            // v8 FIX: goto yerine açık flag — iç içe foreach'ten temiz çıkış
            bool stopRequested = false;

            // ── Seviye seviye çalıştır ───────────────────────────────────────
            foreach (var level in plan.Levels)
            {
                if (stopRequested) break;

                foreach (var planned in level)
                {
                    if (stopRequested) break;
                    var step    = planned.Step;
                    var started = DateTime.UtcNow;

                    // ── InitialVars skip (Phase 3 optimizasyonu) ─────────────
                    // Phase 1'den aktarılan sonuçlar vars'ta zaten var.
                    // "preview_gate" ve read adımları tekrar çalıştırılmaz.
                    // Condition'lı yazma adımları condition bloğundan geçip çalışır.
                    if (InitialVars != null &&
                        InitialVars.ContainsKey(step.Id) &&
                        string.IsNullOrWhiteSpace(step.Condition))
                    {
                        WriteLog($"  ⚡ [{step.Id}] {step.Op} (initial_vars hit)");
                        trace.Add(Row(step, true, "INITIAL", ElapsedMs(started)));
                        skippedCount++;
                        continue;
                    }

                    // ── Koşul kontrolü ───────────────────────────────────────
                    if (!string.IsNullOrWhiteSpace(step.Condition))
                    {
                        if (!EvalCondition(step.Condition!, vars))
                        {
                            WriteLog($"  ⏭ [{step.Id}] {step.Op} — koşul sağlanmadı, atlandı");
                            trace.Add(Row(step, true, "SKIPPED", 0));
                            vars[step.Id] = null;
                            skippedCount++;
                            continue;
                        }
                    }

                    // ── Girdi çözümleme ──────────────────────────────────────
                    object? input = ResolveInput(step, vars, WriteLog);

                    // ── Cache kontrolü ───────────────────────────────────────
                    var cacheKey = BuildCacheKey(step, input);
                    if (step.Cache == true && _cache.TryGet(cacheKey, out var cached))
                    {
                        vars[step.Id] = cached;
                        WriteLog($"  ⚡ [{step.Id}] {step.Op} (cache hit)");
                        trace.Add(Row(step, true, "CACHED", ElapsedMs(started)));
                        cachedCount++;
                        continue;
                    }

                    // ── v4.1: PREVIEW GATE INTERCEPT ─────────────────────────
                    if (string.Equals(step.Op, PREVIEW_GATE_OP,
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        var dto = input as PreviewGeometryDto;

                        // Params'tan başlık al
                        if (dto == null)
                        {
                            var titleStr = "Önizleme";
                            if (step.Params != null &&
                                step.Params.TryGetValue("title", out var tv))
                                titleStr = tv?.ToString() ?? "Önizleme";

                            dto = new PreviewGeometryDto { OperationName = titleStr };
                            WriteLog($"  ⚠ [{step.Id}] preview_gate: input PreviewGeometryDto değil — boş dto");
                        }

                        bool confirmed;
                        if (UserGateCallback != null)
                        {
                            WriteLog($"  🔲 [{step.Id}] preview_gate — kullanıcı bekleniyor...");
                            confirmed = UserGateCallback(dto);
                        }
                        else
                        {
                            // Headless / test modu → otomatik onayla
                            confirmed = true;
                            WriteLog($"  ⚠ [{step.Id}] preview_gate: UserGateCallback null — headless onay");
                        }

                        var gateResult = confirmed ? "confirmed" : "cancelled";
                        vars[step.Id]  = gateResult;

                        var status = confirmed ? "GATE_OK" : "GATE_CANCELLED";
                        WriteLog($"  {(confirmed ? "✓" : "✗")} [{step.Id}] preview_gate → {gateResult}");
                        trace.Add(Row(step, true, status, ElapsedMs(started)));

                        // ── Phase 1 stop: gate sonrası adımları çalıştırma ───
                        // StopAfterGate = true ise (Phase 1 modu):
                        //   yazma adımları Phase 3'te atomic TX içinde çalışır.
                        //   Burada devam edilirse yazma adımları TX'siz çalışır!
                        if (StopAfterGate)
                        {
                            WriteLog($"  ⏹ [{step.Id}] StopAfterGate: Phase 1 tamamlandı.");
                            stopRequested = true;
                            break;  // v8 FIX: goto phase_done yerine flag + break
                        }

                        continue;  // registry.Execute() ÇAĞRILMAZ
                    }

                    // ── v6.0: SCHEDULE GATE INTERCEPT (4D/5D) ────────────────
                    if (string.Equals(step.Op, SCHEDULE_GATE_OP,
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        var dto4d = input as FourDFiveDDto;

                        if (dto4d == null)
                        {
                            dto4d = new FourDFiveDDto { OperationName = "4D/5D Önizleme" };
                            WriteLog($"  ⚠ [{step.Id}] schedule_gate: input FourDFiveDDto değil — boş dto");
                        }

                        bool confirmed4d;
                        if (UserScheduleGateCallback != null)
                        {
                            WriteLog($"  📅 [{step.Id}] schedule_gate — 4D/5D kullanıcı bekleniyor...");
                            confirmed4d = UserScheduleGateCallback(dto4d);
                        }
                        else
                        {
                            confirmed4d = true;
                            WriteLog($"  ⚠ [{step.Id}] schedule_gate: UserScheduleGateCallback null — headless onay");
                        }

                        var gateResult4d = confirmed4d ? "confirmed" : "cancelled";
                        vars[step.Id]    = gateResult4d;

                        var status4d = confirmed4d ? "GATE_OK" : "GATE_CANCELLED";
                        WriteLog($"  {(confirmed4d ? "✓" : "✗")} [{step.Id}] schedule_gate → {gateResult4d}");
                        trace.Add(Row(step, true, status4d, ElapsedMs(started)));

                        if (StopAfterGate)
                        {
                            WriteLog($"  ⏹ [{step.Id}] StopAfterGate: 4D/5D Phase 1 tamamlandı.");
                            stopRequested = true;
                            break;  // v8 FIX: goto phase_done yerine flag + break
                        }

                        continue;  // registry.Execute() ÇAĞRILMAZ
                    }

                    // ── v13.5: SELECTION GATE INTERCEPT (interaktif seçim) ───
                    if (string.Equals(step.Op, SELECTION_GATE_OP,
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        // İstek DTO'sunu step.Params'tan inşa et (girdi yok — bu
                        // op önceki bir adımın çıktısına değil, doğrudan
                        // manifest'teki params'a dayanır).
                        var req = new SelectionRequestDto();
                        if (step.Params != null)
                        {
                            if (step.Params.TryGetValue("prompt", out var pv))
                                req.Prompt = pv?.ToString() ?? req.Prompt;
                            if (step.Params.TryGetValue("mode", out var mv))
                                req.Mode = mv?.ToString() ?? req.Mode;
                            if (step.Params.TryGetValue("min_count", out var mnv) &&
                                int.TryParse(mnv?.ToString(), out var mn))
                                req.MinCount = mn;
                            if (step.Params.TryGetValue("max_count", out var mxv) &&
                                int.TryParse(mxv?.ToString(), out var mx))
                                req.MaxCount = mx;
                            if (step.Params.TryGetValue("allow_linked", out var alv) &&
                                bool.TryParse(alv?.ToString(), out var al))
                                req.AllowLinked = al;
                            if (step.Params.TryGetValue("categories", out var catv))
                                req.Categories = ExtractStringList(catv);
                        }

                        SelectionResultDto selResult;
                        if (UserSelectionCallback != null)
                        {
                            WriteLog($"  🖱 [{step.Id}] selection_gate — kullanıcı seçimi bekleniyor... ({req.Prompt})");
                            selResult = UserSelectionCallback(req) ?? new SelectionResultDto { Cancelled = true };
                        }
                        else
                        {
                            selResult = new SelectionResultDto { Cancelled = true, Prompt = req.Prompt };
                            WriteLog($"  ⚠ [{step.Id}] selection_gate: UserSelectionCallback null — " +
                                     "headless modda interaktif seçim yapılamaz, boş/iptal sonuç döndürüldü");
                        }

                        vars[step.Id] = selResult;   // ToString() → confirmed/cancelled (condition için)

                        var selStatus = selResult.Cancelled ? "SELECT_CANCELLED" : "SELECT_OK";
                        WriteLog($"  {(selResult.Cancelled ? "✗" : "✓")} [{step.Id}] selection_gate → " +
                                 $"{selResult.Count} eleman seçildi");
                        trace.Add(Row(step, true, selStatus, ElapsedMs(started)));

                        continue;  // registry.Execute() ÇAĞRILMAZ
                    }

                    try
                    {
                        var ctx = _contextFactory();
                        ctx.CurrentStepId = step.Id;
                        ctx.Input         = input;
                        ctx.Params        = ResolveParams(step.Params, vars);
                        ctx.Vars          = vars;
                        ctx.Log           = WriteLog;

                        var result = _registry.Execute(step.Op, ctx);
                        vars[step.Id] = result;

                        if (step.Cache == true)
                            _cache.Set(cacheKey, result);

                        var ms = ElapsedMs(started);
                        WriteLog($"  ✓ [{step.Id}] {step.Op} ({ms}ms)");
                        trace.Add(Row(step, true, "OK", ms));
                        OnStepCompleted?.Invoke(step.Id, step.Op, ms, true);
                    }
                    catch (EgInputTypeMismatchException ex)
                    {
                        var ms  = ElapsedMs(started);
                        var msg = $"  ✗ [{step.Id}] TİP UYUMSUZLUĞU: {ex.Message}";
                        WriteLog(msg);
                        trace.Add(Row(step, false, "FAILED", ms));
                        OnStepCompleted?.Invoke(step.Id, step.Op, ms, false);
                        OnAtomicRollback?.Invoke();
                        return Fail(msg, step.Id, vars, trace, log);
                    }
                    catch (Exception ex)
                    {
                        var ms  = ElapsedMs(started);
                        var msg = ex.InnerException?.Message ?? ex.Message;
                        WriteLog($"  ✗ [{step.Id}] {step.Op}: {msg}");
                        trace.Add(Row(step, false, "FAILED", ms));
                        OnStepCompleted?.Invoke(step.Id, step.Op, ms, false);

                        if (step.Required)
                        {
                            OnAtomicRollback?.Invoke();
                            return Fail(msg, step.Id, vars, trace, log);
                        }

                        anyStepFailed = true;
                        vars[step.Id] = null;
                    }
                }
            } // foreach level

            var totalMs = ElapsedMs(runStart);

            if (anyStepFailed && manifest.IsAtomic)
            {
                WriteLog($"⚠ Bazı adımlar başarısız (required=false) — atomic modda rollback.");
                OnAtomicRollback?.Invoke();
                return Fail("Atomic manifest: zorunlu olmayan adım(lar) başarısız oldu.",
                            "__atomic_required_false__", vars, trace, log);
            }

            WriteLog($"✅ {manifest.Title} tamamlandı ({totalMs}ms)");
            OnAtomicCommit?.Invoke();
            return new DagRunResult
            {
                Success       = true,
                Vars          = vars,
                Trace         = trace,
                Log           = log,
                TotalDuration = TimeSpan.FromMilliseconds(totalMs),
                TotalSteps    = plan.Steps.Count,
                CachedSteps   = cachedCount,
                SkippedSteps  = skippedCount,
            };
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private static object? ResolveInput(EgStep step, Dictionary<string, object?> vars,
            Action<string> log)
        {
            if (!string.IsNullOrWhiteSpace(step.From))
            {
                if (!vars.TryGetValue(step.From!, out var v))
                    log($"  ⚠ [{step.Id}]: from='{step.From}' bulunamadı — null");
                return v;
            }
            if (step.FromMany is { Count: > 0 })
                return step.FromMany.Select(id => vars.TryGetValue(id, out var v) ? v : null).ToList();
            return null;
        }

        private static IReadOnlyDictionary<string, object?> ResolveParams(
            Dictionary<string, object?>? raw, Dictionary<string, object?> vars)
        {
            if (raw is null) return new Dictionary<string, object?>();
            var resolved = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw)
                resolved[kv.Key] = ResolveValue(kv.Value, vars);
            return resolved;
        }

        private static object? ResolveValue(object? value, Dictionary<string, object?> vars)
        {
            if (value is string s && s.StartsWith("$") && s.Length > 1)
                return vars.TryGetValue(s[1..], out var v) ? v : value;
            if (value is JsonElement je && je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString() ?? "";
                if (str.StartsWith("$") && str.Length > 1)
                    return vars.TryGetValue(str[1..], out var v) ? v : (object?)str;
            }
            return value;
        }

        /// <summary>
        /// step.Params'tan gelen "categories" gibi liste alanlarını (JsonElement
        /// array veya List&lt;object?&gt; olabilir) List&lt;string&gt;'e çevirir.
        /// v13.5 — selection_gate için eklendi.
        /// </summary>
        private static List<string> ExtractStringList(object? raw)
        {
            var result = new List<string>();
            switch (raw)
            {
                case JsonElement je when je.ValueKind == JsonValueKind.Array:
                    foreach (var item in je.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String)
                            result.Add(item.GetString() ?? "");
                    break;
                case List<object?> list:
                    result.AddRange(list.Select(o => o?.ToString() ?? "").Where(x => x.Length > 0));
                    break;
                case string single when single.Length > 0:
                    result.Add(single);
                    break;
            }
            return result;
        }

        // ── Condition değerlendirme ───────────────────────────────────────────
        //
        //  v9 EvalCondition: sembolik + kelime operatörleri
        //
        //  Sembolik: == != >= <= > <
        //  Kelime:   contains | not_contains | in | not_in | starts_with | ends_with | matches
        //
        //  Örnekler:
        //    "$gate == confirmed"
        //    "$count >= 3"
        //    "$isim contains Duvar"
        //    "$kategori in [Walls,Floors]"
        //    "$isim starts_with EG_"
        //    "$isim matches ^[A-Z]"
        //
        private static readonly Regex _condOpRx = new(
            @"(==|!=|>=|<=|>(?!=)|<(?!=))",
            RegexOptions.Compiled);

        private static readonly Regex _condWordOpRx = new(
            @"\b(contains|not_contains|in|not_in|starts_with|ends_with|matches)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool EvalCondition(string condition, Dictionary<string, object?> vars)
        {
            condition = condition.Trim();

            // ── Kelime operatörleri ───────────────────────────────────────────
            var wm = _condWordOpRx.Match(condition);
            if (wm.Success)
            {
                var wordOp = wm.Value.ToLowerInvariant();
                var left   = condition[..wm.Index].Trim();
                var right  = condition[(wm.Index + wm.Length)..].Trim();
                var lv     = ResolveValue(left, vars)?.ToString() ?? "";
                var rvRaw  = right.Trim('[', ']');
                var rv     = right.StartsWith("[") ? rvRaw : (ResolveValue(right, vars)?.ToString() ?? right);

                switch (wordOp)
                {
                    case "contains":
                    {
                        var leftObj = ResolveValue(left, vars);
                        if (leftObj is System.Collections.IEnumerable ie and not string)
                        {
                            foreach (var item in ie)
                                if (string.Equals(item?.ToString(), rv.Trim('"', '\''),
                                                  StringComparison.OrdinalIgnoreCase)) return true;
                            return false;
                        }
                        return lv.Contains(rv.Trim('"', '\''), StringComparison.OrdinalIgnoreCase);
                    }
                    case "not_contains":
                    {
                        var leftObj2 = ResolveValue(left, vars);
                        if (leftObj2 is System.Collections.IEnumerable ie2 and not string)
                        {
                            foreach (var item in ie2)
                                if (string.Equals(item?.ToString(), rv.Trim('"', '\''),
                                                  StringComparison.OrdinalIgnoreCase)) return false;
                            return true;
                        }
                        return !lv.Contains(rv.Trim('"', '\''), StringComparison.OrdinalIgnoreCase);
                    }
                    case "in":
                    {
                        var items = rvRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        return items.Any(i => string.Equals(i.Trim('"', '\''), lv, StringComparison.OrdinalIgnoreCase));
                    }
                    case "not_in":
                    {
                        var items2 = rvRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        return !items2.Any(i => string.Equals(i.Trim('"', '\''), lv, StringComparison.OrdinalIgnoreCase));
                    }
                    case "starts_with":
                        return lv.StartsWith(rv.Trim('"', '\''), StringComparison.OrdinalIgnoreCase);
                    case "ends_with":
                        return lv.EndsWith(rv.Trim('"', '\''), StringComparison.OrdinalIgnoreCase);
                    case "matches":
                        try { return Regex.IsMatch(lv, rv.Trim('"', '\'')); }
                        catch { return false; }
                    default: return false;
                }
            }

            // ── Sembolik operatörler ──────────────────────────────────────────
            var m = _condOpRx.Match(condition);
            if (m.Success)
            {
                var op    = m.Value;
                var left  = condition[..m.Index].Trim();
                var right = condition[(m.Index + op.Length)..].Trim();
                var lv    = ResolveValue(left,  vars)?.ToString() ?? "";
                var rv    = ResolveValue(right, vars)?.ToString() ?? "";

                return op switch
                {
                    "==" => string.Equals(lv, rv, StringComparison.OrdinalIgnoreCase),
                    "!=" => !string.Equals(lv, rv, StringComparison.OrdinalIgnoreCase),
                    ">=" => double.TryParse(lv, out var da)  && double.TryParse(rv, out var db)  && da >= db,
                    "<=" => double.TryParse(lv, out var da2) && double.TryParse(rv, out var db2) && da2 <= db2,
                    ">"  => double.TryParse(lv, out var da3) && double.TryParse(rv, out var db3) && da3 > db3,
                    "<"  => double.TryParse(lv, out var da4) && double.TryParse(rv, out var db4) && da4 < db4,
                    _    => false
                };
            }

            // ── Operatörsüz gerçeklik testi ───────────────────────────────────
            var r = ResolveValue(condition, vars);
            if (r is null)   return false;
            if (r is bool b) return b;
            if (r is string sv) return !string.IsNullOrWhiteSpace(sv) && sv != "false" && sv != "0";
            if (r is int    iv) return iv != 0;
            if (r is double dv) return dv != 0;
            if (r is System.Collections.ICollection col) return col.Count > 0;
            return true;
        }

        private static string BuildCacheKey(EgStep step, object? input)
        {
            var ps = step.Params is null ? "" : JsonSerializer.Serialize(step.Params);

            // v8 FIX: Koleksiyonlarda sadece Count yerine içerik hash'i kullan.
            // Aynı sayıda ama farklı elemanlı iki liste artık farklı cache key üretir.
            // Örn: filter_by_param sonrası [duvar_A, duvar_B] ≠ [duvar_C, duvar_D]
            var ih = input switch
            {
                null                             => "null",
                System.Collections.ICollection col => BuildCollectionHash(col),
                string s                         => $"str:{s.GetHashCode()}",
                _                                => $"{input.GetType().Name}:{input.GetHashCode()}"
            };
            return $"{step.Op}|{ps}|{ih}";
        }

        /// <summary>
        /// Koleksiyonun içerik hash'ini üretir — process-restart'larda deterministik.
        ///
        /// v9 FIX: string.GetHashCode() .NET 5+ ile process başına random seed kullanır
        ///   → aynı string farklı process'lerde farklı hash üretir → cache key instability.
        /// Çözüm: string elemanlar için StringComparer.Ordinal.GetHashCode() kullan.
        ///   Bu comparer randomization'dan etkilenmez, her zaman aynı değeri döner.
        ///
        /// Sıra duyarlı: [A,B] ≠ [B,A] (idx çarpanı ile)
        /// Büyük koleksiyonlarda ilk 200 eleman alınır (performans).
        /// </summary>
        private static string BuildCollectionHash(System.Collections.ICollection col)
        {
            int hash = 17;
            int idx  = 0;
            foreach (var item in col)
            {
                hash = hash * 31 + idx;
                // v9 FIX: string için StringComparer.Ordinal.GetHashCode() — process-deterministik
                hash = hash * 31 + (item is string s
                    ? StringComparer.Ordinal.GetHashCode(s)
                    : (item?.GetHashCode() ?? 0));
                idx++;
                if (idx > 200) break;
            }
            return $"col:{col.Count}:{col.GetType().Name}:{hash}";
        }

        private static DagTraceRow Row(EgStep step, bool ok, string status, long ms)
            => new() { StepId = step.Id, Op = step.Op, Success = ok, Status = status, Ms = ms };

        private static long ElapsedMs(DateTime from)
            => (long)(DateTime.UtcNow - from).TotalMilliseconds;

        private static DagRunResult Fail(string msg, string? stepId,
            Dictionary<string, object?> vars, List<DagTraceRow> trace, List<string> log)
            => new() { Success = false, ErrorMessage = msg, ErrorStep = stepId,
                       Vars = vars, Trace = trace, Log = log };

        public void ClearCache() => _cache.Clear();
    }
}
