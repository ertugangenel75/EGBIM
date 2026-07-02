using System;
using System.Collections.Generic;
using EGBIMOTO.Core.Cache;
using EGBIMOTO.Core.DAG;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.Ops;
using Xunit;

namespace EGBIMOTO.Core.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  NodeCacheServiceTests — EGBIMOTO v8
    //  v8 ile ConcurrentDictionary'e geçildi — thread safety testi dahil.
    // ═══════════════════════════════════════════════════════════════════════════

    public class NodeCacheServiceTests
    {
        [Fact(DisplayName = "C01 — Set sonrası TryGet aynı değeri döner")]
        public void SetAndGet_ReturnsSameValue()
        {
            var cache = new NodeCacheService(TimeSpan.FromMinutes(5));
            cache.Set("key1", new List<string> { "A", "B" });

            var found = cache.TryGet("key1", out var output);
            Assert.True(found);
            var list = Assert.IsType<List<string>>(output);
            Assert.Equal(2, list.Count);
        }

        [Fact(DisplayName = "C02 — TTL süresi dolmuş kayıt TryGet'te false döner")]
        public void ExpiredEntry_ReturnsFalse()
        {
            var cache = new NodeCacheService(TimeSpan.FromMilliseconds(1));
            cache.Set("key_expire", 99);
            System.Threading.Thread.Sleep(10);  // TTL geç

            var found = cache.TryGet("key_expire", out _);
            Assert.False(found);
        }

        [Fact(DisplayName = "C03 — Invalidate sonrası TryGet false döner")]
        public void Invalidate_RemovesEntry()
        {
            var cache = new NodeCacheService(TimeSpan.FromMinutes(5));
            cache.Set("key_inv", "değer");
            cache.Invalidate("key_inv");

            Assert.False(cache.TryGet("key_inv", out _));
        }

        [Fact(DisplayName = "C04 — Clear sonrası Count sıfır olur")]
        public void Clear_EmptiesCache()
        {
            var cache = new NodeCacheService(TimeSpan.FromMinutes(5));
            cache.Set("k1", 1); cache.Set("k2", 2); cache.Set("k3", 3);
            cache.Clear();
            Assert.Equal(0, cache.Count);
        }

        [Fact(DisplayName = "C05 — Paralel yazma/okuma thread-safe çalışır (ConcurrentDictionary fix)")]
        public void ConcurrentAccess_NoException()
        {
            var cache = new NodeCacheService(TimeSpan.FromMinutes(5));
            var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            // 20 thread aynı anda yazar ve okur
            var tasks = new System.Threading.Tasks.Task[20];
            for (int i = 0; i < 20; i++)
            {
                var idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        cache.Set($"key_{idx}", idx * 10);
                        cache.TryGet($"key_{idx}", out _);
                        cache.TryGet($"key_{idx % 5}", out _);  // başka thread'in yazdığı
                    }
                    catch (Exception ex) { errors.Add(ex); }
                });
            }
            System.Threading.Tasks.Task.WaitAll(tasks);
            Assert.Empty(errors);  // exception yok
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DagPlannerTests — EGBIMOTO v8
    // ═══════════════════════════════════════════════════════════════════════════

    public class DagPlannerTests
    {
        private static OpRegistry EmptyRegistry()
        {
            var r = new OpRegistry();
            r.Register("op", typeof(DagPlannerTests)
                .GetMethod(nameof(FakeOp), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!,
                "op");
            return r;
        }
        private static object? FakeOp(OpContext ctx) => null;

        private static EgManifest ManifestOf(params EgStep[] steps) =>
            new() { Title = "Test", Steps = new(steps) };

        [Fact(DisplayName = "P01 — Bağımsız adımlar aynı Level'da yer alır")]
        public void IndependentSteps_SameLevel()
        {
            var planner = new DagPlanner(EmptyRegistry());
            var plan = planner.Build(ManifestOf(
                new EgStep { Id = "a", Op = "op" },
                new EgStep { Id = "b", Op = "op" }));

            Assert.True(plan.Success);
            Assert.Single(plan.Levels);  // tek level
            Assert.Equal(2, plan.Levels[0].Count);
        }

        [Fact(DisplayName = "P02 — A→B bağımlılığı iki ayrı Level üretir")]
        public void DependentSteps_TwoLevels()
        {
            var planner = new DagPlanner(EmptyRegistry());
            var plan = planner.Build(ManifestOf(
                new EgStep { Id = "a", Op = "op" },
                new EgStep { Id = "b", Op = "op", From = "a" }));

            Assert.True(plan.Success);
            Assert.Equal(2, plan.Levels.Count);
            Assert.Equal("a", plan.Levels[0][0].Step.Id);
            Assert.Equal("b", plan.Levels[1][0].Step.Id);
        }

        [Fact(DisplayName = "P03 — Var olmayan from referansı DagPlan.Success=false döner")]
        public void InvalidFrom_PlanFails()
        {
            var planner = new DagPlanner(EmptyRegistry());
            var plan = planner.Build(ManifestOf(
                new EgStep { Id = "a", Op = "op", From = "yok_bu" }));

            Assert.False(plan.Success);
            Assert.Contains("yok_bu", plan.Error);
        }

        [Fact(DisplayName = "P04 — from_many üç kaynak doğru bağımlılık kurar")]
        public void FromMany_ThreeSources_Resolved()
        {
            var planner = new DagPlanner(EmptyRegistry());
            var plan = planner.Build(ManifestOf(
                new EgStep { Id = "a", Op = "op" },
                new EgStep { Id = "b", Op = "op" },
                new EgStep { Id = "c", Op = "op" },
                new EgStep { Id = "birle", Op = "op",
                    FromMany = new List<string> { "a", "b", "c" } }));

            Assert.True(plan.Success);
            // birle son seviyede olmalı
            var lastLevel = plan.Levels[^1];
            Assert.Contains(lastLevel, ps => ps.Step.Id == "birle");
        }
    }
}
