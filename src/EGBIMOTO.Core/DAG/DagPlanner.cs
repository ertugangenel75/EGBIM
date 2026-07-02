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
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Core.DAG
{
    /// <summary>
    /// Manifest adımlarından yönlü asiklik graf (DAG) üretir.
    ///
    /// Bağımlılık kaynakları:
    ///   1. step.from / step.from_many  → açık bağımlılık
    ///   2. step.params içindeki "$varName" referansları → örtük bağımlılık
    ///   3. step.condition içindeki "$varName" referansları → örtük bağımlılık (FIX v2.0)
    ///   4. step.depends_on listesi → manuel bağımlılık
    ///
    /// FIX v2.0:
    ///   - CollectReads artık step.Condition'daki $ref'leri de tarıyor.
    ///     Condition'da $varName olan adım, o değişkeni üreten adıma bağımlı sayılır.
    ///     Önceki versiyonda bu bağımlılık eksikti → race condition riski vardı.
    ///   - PlannedStep.CacheKey Build() içinde hesaplanıyor (DagExecutor'a taşınmıştı, geri alındı).
    /// </summary>
    public sealed class DagPlanner
    {
        // \p{L} — Unicode letter: Türkçe step id ve $ref'leri destekler.
        // Örn: "$döşeme", "$tüm_hacim", "$donatı_sayısı"
        private static readonly Regex VarRef = new(
            @"\$([A-Za-z_\p{L}][A-Za-z0-9_\p{L}]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly OpRegistry _registry;

        public DagPlanner(OpRegistry registry)
            => _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        public DagPlan Build(EgManifest manifest)
        {
            if (manifest is null) return Fail("Manifest null.");
            var steps = manifest.Steps ?? new List<EgStep>();
            if (steps.Count == 0) return new DagPlan { Success = true };

            // ── ID haritası ──────────────────────────────────────────────────
            var idMap = new Dictionary<string, EgStep>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in steps)
            {
                if (string.IsNullOrWhiteSpace(s.Id)) return Fail("Bir adımın id'si boş.");
                if (idMap.ContainsKey(s.Id))         return Fail($"Tekrar eden adım id: '{s.Id}'.");
                idMap[s.Id] = s;
            }

            // ── Her adım kendi id'sini yazar ─────────────────────────────────
            var writerOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in steps)
                writerOf[s.Id] = s.Id;

            // ── Bağımlılık seti ──────────────────────────────────────────────
            var deps = steps.ToDictionary(
                s => s.Id,
                _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            var planned = new Dictionary<string, PlannedStep>(StringComparer.OrdinalIgnoreCase);

            foreach (var step in steps)
            {
                // FIX v2.0: Condition'daki $ref'ler de toplanıyor
                var reads  = CollectReads(step).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var writes = new List<string> { step.Id };

                // from → açık bağımlılık
                if (!string.IsNullOrWhiteSpace(step.From))
                {
                    if (!idMap.ContainsKey(step.From!))
                        return Fail($"Adım '{step.Id}': from='{step.From}' bulunamadı.");
                    deps[step.Id].Add(step.From!);
                }

                // from_many → açık bağımlılık
                if (step.FromMany is { Count: > 0 })
                    foreach (var dep in step.FromMany)
                    {
                        if (!idMap.ContainsKey(dep))
                            return Fail($"Adım '{step.Id}': from_many içinde '{dep}' bulunamadı.");
                        deps[step.Id].Add(dep);
                    }

                // depends_on → manuel bağımlılık
                if (step.DependsOn is { Count: > 0 })
                    foreach (var dep in step.DependsOn)
                    {
                        if (!idMap.ContainsKey(dep))
                            return Fail($"Adım '{step.Id}': depends_on içinde '{dep}' bulunamadı.");
                        deps[step.Id].Add(dep);
                    }

                // $ref → örtük bağımlılık (params + condition)
                foreach (var read in reads)
                    if (writerOf.TryGetValue(read, out var writer) &&
                        !string.Equals(writer, step.Id, StringComparison.OrdinalIgnoreCase))
                        deps[step.Id].Add(writer);

                var parallelSafe = !_registry.RequiresTransaction(step.Op);

                // FIX v2.0: CacheKey Build() içinde hesaplanıyor
                planned[step.Id] = new PlannedStep
                {
                    Step         = step,
                    Reads        = reads,
                    Writes       = writes,
                    ParallelSafe = parallelSafe,
                    CacheKey     = step.Cache == true ? BuildStaticCacheKey(step) : null
                };
            }

            // ── Kahn's algoritması ile topolojik sıralama ────────────────────
            var plan      = new DagPlan { Success = true };
            var remaining = new HashSet<string>(idMap.Keys, StringComparer.OrdinalIgnoreCase);
            var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int level     = 0;

            while (remaining.Count > 0)
            {
                var ready = remaining
                    .Where(id => deps[id].All(completed.Contains))
                    .OrderBy(id => steps.FindIndex(s =>
                        string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (ready.Count == 0)
                {
                    var cycle = string.Join(", ", remaining.OrderBy(x => x));
                    return Fail($"Döngü veya çözülemeyen bağımlılık: [{cycle}]");
                }

                var batch = new List<PlannedStep>();
                foreach (var id in ready)
                {
                    remaining.Remove(id);
                    completed.Add(id);
                    planned[id].Level = level;
                    batch.Add(planned[id]);
                    plan.Steps.Add(planned[id]);
                }
                plan.Levels.Add(batch);

                var txFlag = batch.Any(x => !x.ParallelSafe) ? " [TX]" : "";
                plan.Diagnostics.Add(
                    $"Level {level}{txFlag}: {string.Join(" → ", batch.Select(x => x.Step.Id))}");
                level++;
            }

            return plan;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        /// <summary>
        /// FIX v2.0: Hem params hem de condition'daki $ref'leri toplar.
        /// Önceki versiyon sadece params'ı tarıyordu;
        /// condition'da "$x == 'foo'" gibi ifadeler dependency yaratmıyordu.
        /// </summary>
        private static IEnumerable<string> CollectReads(EgStep step)
        {
            // Params
            if (step.Params is not null)
                foreach (var val in step.Params.Values)
                    foreach (var r in ReadsInValue(val)) yield return r;

            // Condition — FIX v2.0
            if (!string.IsNullOrWhiteSpace(step.Condition))
                foreach (var r in ReadsInValue(step.Condition))
                    yield return r;
        }

        private static IEnumerable<string> ReadsInValue(object? value)
        {
            if (value is null) yield break;
            if (value is string s)
            {
                if (s.StartsWith("$") && s.Length > 1) yield return s[1..];
                foreach (Match m in VarRef.Matches(s))
                    yield return m.Groups[1].Value;
                yield break;
            }
            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.String)
                    foreach (var r in ReadsInValue(je.GetString())) yield return r;
                else if (je.ValueKind == JsonValueKind.Array)
                    foreach (var item in je.EnumerateArray())
                        foreach (var r in ReadsInValue(item)) yield return r;
                else if (je.ValueKind == JsonValueKind.Object)
                    foreach (var prop in je.EnumerateObject())
                        foreach (var r in ReadsInValue(prop.Value)) yield return r;
            }
        }

        /// <summary>
        /// Cache=true olan adımlar için statik cache key.
        /// Params deterministik olmayan değer ($ref) içeriyorsa cache etkisiz kalır;
        /// bu intentional — runtime'da DagExecutor dinamik key hesaplar.
        /// </summary>
        private static string BuildStaticCacheKey(EgStep step)
        {
            var ps = step.Params is null ? "" : JsonSerializer.Serialize(step.Params);
            return $"{step.Op}|{ps}|static";
        }

        private static DagPlan Fail(string error) => new() { Success = false, Error = error };
    }
}
