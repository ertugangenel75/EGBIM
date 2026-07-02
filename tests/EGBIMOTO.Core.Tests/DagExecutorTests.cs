using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EGBIMOTO.Core.Cache;
using EGBIMOTO.Core.DAG;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.Ops;
using Xunit;

namespace EGBIMOTO.Core.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  DagExecutorTests — EGBIMOTO v9
    //
    //  v8 → v9 FIX:
    //    MakeMethod / OpWrapper yaklaşımı TargetException atıyordu.
    //    OpRegistry.Execute → method.Invoke(null, ...) çağrısı yapar.
    //    null = "no instance" yani STATIC metod beklenir.
    //    OpWrapper.Execute ise instance metoduydu → TargetException.
    //
    //    Çözüm: AssemblyBuilder + TypeBuilder ile her test için gerçek
    //    static metodlar üret. Bu metotlar thread-local bir Action<OpContext>
    //    slot üzerinden lambda'yı çağırır.
    //
    //  Test listesi (T01–T14):
    //    T01 — Boş manifest başarıyla tamamlanır
    //    T02 — Tek adım çalıştırma, çıktı vars'a yazılır
    //    T03 — from bağlantısı: önceki adım çıktısı ctx.Input'a gelir
    //    T04 — condition=false adım atlanır (SKIPPED)
    //    T05 — condition=true adım çalışır
    //    T06 — required=false hatalı adım manifesti durdurmaz
    //    T07 — required=true hatalı adım manifesti durdurur
    //    T08 — Döngü tespiti: DagPlan.Success=false döner
    //    T09 — Cache hit: aynı adım ikinci çalıştırmada cache'ten gelir
    //    T10 — from_many: iki adım çıktısı ctx.Input listesi olarak gelir
    //    T11 — StopAfterGate=true: preview_gate sonrası adımlar çalışmaz
    //    T12 — BuildCollectionHash: farklı içerik farklı cache key üretir
    //    T13 — EvalCondition: >= operatörü sayısal karşılaştırma yapar
    //    T14 — Depends_on: açık bağımlılık DAG sırasını etkiler
    // ═══════════════════════════════════════════════════════════════════════════

    public class DagExecutorTests
    {
        // ── Dinamik static metod fabrikası ───────────────────────────────────
        //
        // Her çağrıda benzersiz bir AssemblyBuilder + TypeBuilder + MethodBuilder
        // üretir. Oluşturulan static metod bir [ThreadStatic] slot üzerinden
        // lambda'yı çağırır — OpRegistry.Execute(null, ...) ile invoke edilebilir.

        private static int _typeCounter = 0;

        [ThreadStatic]
        private static Func<OpContext, object?>? _currentFn;

        private static MethodInfo MakeStaticOp(string opName, Func<OpContext, object?> fn)
        {
            // Her op için benzersiz assembly + tip ismi
            int id = System.Threading.Interlocked.Increment(ref _typeCounter);
            var asmName = new AssemblyName($"EgTestOps_{id}");
            var asm     = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var mod     = asm.DefineDynamicModule("Module");
            var tb      = mod.DefineType($"TestOp_{id}_{opName}",
                              TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            // ThreadStatic alanı tanımla: _fn
            var fnField = tb.DefineField("_fn",
                typeof(Func<OpContext, object?>),
                FieldAttributes.Public | FieldAttributes.Static);

            // static Execute(OpContext ctx) → _fn(ctx) metodunu emit et
            var mb = tb.DefineMethod("Execute",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(object),
                new[] { typeof(OpContext) });

            var il = mb.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, fnField);  // _fn'i stack'e yükle
            il.Emit(OpCodes.Ldarg_0);           // ctx'i stack'e yükle
            il.Emit(OpCodes.Callvirt,           // _fn.Invoke(ctx)
                typeof(Func<OpContext, object?>).GetMethod("Invoke")!);
            il.Emit(OpCodes.Ret);

            var builtType = tb.CreateType()!;

            // Lambda'yı static field'a ata
            builtType.GetField("_fn")!.SetValue(null, fn);

            return builtType.GetMethod("Execute")!;
        }

        // ── Test altyapısı ────────────────────────────────────────────────────

        private static (OpRegistry registry, DagExecutor executor) Build(
            params (string name, Func<OpContext, object?> fn)[] ops)
        {
            var registry = new OpRegistry();
            foreach (var (name, fn) in ops)
            {
                var method = MakeStaticOp(name, fn);
                registry.Register(name, method, name);
            }

            var executor = new DagExecutor(
                registry,
                () => new OpContext(),
                new NodeCacheService(TimeSpan.FromMinutes(5)));

            return (registry, executor);
        }

        private static EgManifest Manifest(params EgStep[] steps) =>
            new() { Title = "Test Manifest", Steps = steps.ToList() };

        private static EgStep Step(string id, string op,
            string? from = null, string? condition = null,
            bool required = true, bool cache = false,
            List<string>? fromMany = null,
            List<string>? dependsOn = null) =>
            new()
            {
                Id        = id,
                Op        = op,
                From      = from,
                Condition = condition,
                Required  = required,
                Cache     = cache,
                FromMany  = fromMany,
                DependsOn = dependsOn,
            };

        // ── TESTLER ──────────────────────────────────────────────────────────

        [Fact(DisplayName = "T01 — Boş manifest başarıyla tamamlanır")]
        public void T01_EmptyManifest_Succeeds()
        {
            var (_, exec) = Build();
            var result = exec.Run(new EgManifest { Title = "Boş", Steps = new() });
            Assert.True(result.Success);
            Assert.Empty(result.Trace);
        }

        [Fact(DisplayName = "T02 — Tek adım: çıktı vars'a yazılır")]
        public void T02_SingleStep_OutputStoredInVars()
        {
            var (_, exec) = Build(("topla", ctx => new List<string> { "DuvarA", "DuvarB" }));

            var result = exec.Run(Manifest(Step("topla", "topla")));

            Assert.True(result.Success);
            var output = Assert.IsType<List<string>>(result.Vars["topla"]);
            Assert.Equal(2, output.Count);
        }

        [Fact(DisplayName = "T03 — from bağlantısı: önceki çıktı ctx.Input'a gelir")]
        public void T03_FromBinding_PreviousOutputBecomesInput()
        {
            object? capturedInput = null;
            var (_, exec) = Build(
                ("topla", ctx => new List<int> { 1, 2, 3 }),
                ("say",   ctx => { capturedInput = ctx.Input; return (ctx.Input as List<int>)?.Count ?? 0; }));

            var result = exec.Run(Manifest(
                Step("topla", "topla"),
                Step("say",   "say", from: "topla")));

            Assert.True(result.Success);
            Assert.IsType<List<int>>(capturedInput);
            Assert.Equal(3, (int)result.Vars["say"]!);
        }

        [Fact(DisplayName = "T04 — condition=false → adım SKIPPED olur")]
        public void T04_ConditionFalse_StepSkipped()
        {
            int callCount = 0;
            var (_, exec) = Build(("yazma", ctx => { callCount++; return "yazıldı"; }));

            var result = exec.Run(Manifest(Step("yazma", "yazma", condition: "$x == evet")));

            Assert.True(result.Success);
            Assert.Equal(0, callCount);
            Assert.Equal("SKIPPED", result.Trace[0].Status);
        }

        [Fact(DisplayName = "T05 — condition=true → adım çalışır")]
        public void T05_ConditionTrue_StepExecutes()
        {
            var (_, exec) = Build(
                ("flag",  ctx => "evet"),
                ("yazma", ctx => "yazıldı"));

            var result = exec.Run(Manifest(
                Step("flag",  "flag"),
                Step("yazma", "yazma", from: "flag", condition: "$flag == evet")));

            Assert.True(result.Success);
            Assert.Equal("OK", result.Trace[1].Status);
            Assert.Equal("yazıldı", result.Vars["yazma"]);
        }

        [Fact(DisplayName = "T06 — required=false hatalı adım manifesti durdurmaz")]
        public void T06_RequiredFalse_ErrorDoesNotStopManifest()
        {
            var (_, exec) = Build(
                ("hata",  ctx => throw new InvalidOperationException("kasıtlı hata")),
                ("devam", ctx => "tamamlandı"));

            var result = exec.Run(Manifest(
                Step("hata",  "hata",  required: false),
                Step("devam", "devam")));

            Assert.True(result.Success);
            Assert.Equal("FAILED", result.Trace[0].Status);
            Assert.Equal("OK",     result.Trace[1].Status);
        }

        [Fact(DisplayName = "T07 — required=true hatalı adım manifesti durdurur")]
        public void T07_RequiredTrue_ErrorStopsManifest()
        {
            int devamCalled = 0;
            var (_, exec) = Build(
                ("hata",  ctx => throw new InvalidOperationException("zorunlu hata")),
                ("devam", ctx => { devamCalled++; return "tamamlandı"; }));

            var result = exec.Run(Manifest(
                Step("hata",  "hata",  required: true),
                Step("devam", "devam")));

            Assert.False(result.Success);
            Assert.Equal("hata", result.ErrorStep);
            Assert.Equal(0, devamCalled);
        }

        [Fact(DisplayName = "T08 — Döngü tespiti: DagPlan.Success=false")]
        public void T08_CyclicDependency_PlanFails()
        {
            var registry = new OpRegistry();
            registry.Register("op", MakeStaticOp("op_cycle", ctx => (object?)null), "op");
            var executor = new DagExecutor(registry, () => new OpContext());

            var manifest = new EgManifest
            {
                Title = "Döngü",
                Steps = new()
                {
                    new EgStep { Id = "A", Op = "op", From = "B" },
                    new EgStep { Id = "B", Op = "op", From = "A" },
                }
            };

            var result = executor.Run(manifest);
            Assert.False(result.Success);
            Assert.Contains("Döngü", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "T09 — Cache hit: ikinci çalışmada op yeniden çağrılmaz")]
        public void T09_CacheHit_StepNotReExecuted()
        {
            int callCount = 0;
            var (_, exec) = Build(("hesap", ctx => { callCount++; return 42; }));

            var step     = Step("hesap", "hesap", cache: true);
            var manifest = Manifest(step);

            exec.Run(manifest);
            callCount = 0;        // sayacı sıfırla
            exec.Run(manifest);   // cache hit bekleniyor

            Assert.Equal(0, callCount);
        }

        [Fact(DisplayName = "T10 — from_many: iki adım çıktısı ctx.Input listesi olarak gelir")]
        public void T10_FromMany_MultipleInputsMerged()
        {
            object? capturedInput = null;
            var (_, exec) = Build(
                ("a",     ctx => new List<string> { "X1" }),
                ("b",     ctx => new List<string> { "X2" }),
                ("birle", ctx => { capturedInput = ctx.Input; return "ok"; }));

            var result = exec.Run(Manifest(
                Step("a",     "a"),
                Step("b",     "b"),
                Step("birle", "birle", fromMany: new List<string> { "a", "b" })));

            Assert.True(result.Success);
            var inputs = Assert.IsType<List<object?>>(capturedInput);
            Assert.Equal(2, inputs.Count);
        }

        [Fact(DisplayName = "T11 — StopAfterGate: preview_gate sonrası adımlar çalışmaz (goto fix doğrulama)")]
        public void T11_StopAfterGate_PostGateStepsNotExecuted()
        {
            int postGateCalled = 0;
            var (_, exec) = Build(
                ("topla", ctx => new List<string> { "eleman" }),
                ("yazma", ctx => { postGateCalled++; return "yazıldı"; }));

            exec.StopAfterGate = true;

            var manifest = new EgManifest
            {
                Title = "Preview Test",
                Steps = new()
                {
                    new EgStep { Id = "topla", Op = "topla" },
                    new EgStep { Id = "gate",  Op = "preview_gate", From = "topla" },
                    new EgStep { Id = "yazma", Op = "yazma",  From = "topla",
                                 Condition = "$gate == confirmed" },
                }
            };

            var result = exec.Run(manifest);

            Assert.Equal(0, postGateCalled);
            var gateTrace = result.Trace.FirstOrDefault(t => t.StepId == "gate");
            Assert.NotNull(gateTrace);
            Assert.StartsWith("GATE_", gateTrace!.Status);
        }

        [Fact(DisplayName = "T12 — BuildCollectionHash: farklı içerik → farklı hash (non-determinism fix doğrulama)")]
        public void T12_CollectionHash_DifferentContentDifferentKey()
        {
            // BuildCollectionHash private static — reflection ile test
            var method = typeof(DagExecutor).GetMethod(
                "BuildCollectionHash",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            var list1 = new List<string> { "DuvarA", "DuvarB" };
            var list2 = new List<string> { "DuvarC", "DuvarD" };  // aynı Count=2, farklı içerik

            // NOT: string.GetHashCode() .NET 5+ ile process bazlı rastgele seed kullanır.
            // Bu yüzden hash1 != hash2 garantisi için içerik farklı olmalı VE
            // BuildCollectionHash içerik bazlı çalışmalıdır.
            // Test, aynı process içinde çalışır → seed sabit → sonuçlar deterministik.
            var hash1 = method!.Invoke(null, new object[] { list1 }) as string;
            var hash2 = method!.Invoke(null, new object[] { list2 }) as string;

            Assert.NotNull(hash1);
            Assert.NotNull(hash2);
            Assert.NotEqual(hash1, hash2);
        }

        [Fact(DisplayName = "T13 — EvalCondition: >= operatörü sayısal karşılaştırma yapar")]
        public void T13_EvalCondition_NumericComparison()
        {
            int yazmaCallCount = 0;
            var (_, exec) = Build(
                ("say",   ctx => 5),
                ("yazma", ctx => { yazmaCallCount++; return "büyük"; }));

            // $say >= 3 → true → yazma çalışmalı
            var result = exec.Run(Manifest(
                Step("say",   "say"),
                Step("yazma", "yazma", from: "say", condition: "$say >= 3")));

            Assert.True(result.Success);
            Assert.Equal(1, yazmaCallCount);

            // Reset
            yazmaCallCount = 0;

            // $say >= 10 → false → yazma çalışmamalı
            var result2 = exec.Run(Manifest(
                Step("say",   "say"),
                Step("yazma", "yazma", from: "say", condition: "$say >= 10")));

            Assert.True(result2.Success);
            Assert.Equal(0, yazmaCallCount);
        }

        [Fact(DisplayName = "T14 — depends_on: açık bağımlılık DAG sırasını belirler")]
        public void T14_DependsOn_ExplicitOrderEnforced()
        {
            var order = new List<string>();
            var (_, exec) = Build(
                ("a", ctx => { order.Add("a"); return "a_done"; }),
                ("b", ctx => { order.Add("b"); return "b_done"; }),
                ("c", ctx => { order.Add("c"); return "c_done"; }));

            // c depends_on a ve b — a ve b'den önce çalışmamalı
            var result = exec.Run(new EgManifest
            {
                Title = "DependsOn Test",
                Steps = new()
                {
                    new EgStep { Id = "a", Op = "a" },
                    new EgStep { Id = "b", Op = "b" },
                    new EgStep { Id = "c", Op = "c", DependsOn = new() { "a", "b" } },
                }
            });

            Assert.True(result.Success);
            // c, a ve b'den sonra gelmeli
            Assert.True(order.IndexOf("c") > order.IndexOf("a"));
            Assert.True(order.IndexOf("c") > order.IndexOf("b"));
        }

        // ── v10 EKLENEN TESTLER ───────────────────────────────────────────────

        [Fact(DisplayName = "T15 — DagRunResult telemetry alanları doldurulur")]
        public void Telemetry_FieldsArePopulated()
        {
            var (_, exec) = Build(
                ("a", _ => new List<int> { 1, 2, 3 }),
                ("b", ctx => ctx.Input));

            var result = exec.Run(Manifest(
                Step("a", "a"),
                Step("b", "b", from: "a")));

            Assert.True(result.Success);
            Assert.Equal(2, result.TotalSteps);
            Assert.True(result.TotalDuration >= TimeSpan.Zero);
            // Hiç cache kullanılmadı, hiç adım atlanmadı
            Assert.Equal(0, result.CachedSteps);
            Assert.Equal(0, result.SkippedSteps);
        }

        [Fact(DisplayName = "T16 — Atlanan koşullu adım SkippedSteps sayacına yansır")]
        public void Telemetry_SkippedCountReflectsConditionSkip()
        {
            var (_, exec) = Build(
                ("flag", _ => "no"),
                ("hep",  _ => "calisti"));

            // condition "$flag == yes" sağlanmaz → hep adımı atlanır
            var result = exec.Run(Manifest(
                Step("flag", "flag"),
                Step("hep", "hep", condition: "$flag == yes")));

            Assert.True(result.Success);
            Assert.Equal(1, result.SkippedSteps);
        }

        [Fact(DisplayName = "T17 — FIX: Condition'daki $ref bağımlılık olarak çözülür")]
        public void ConditionRef_CreatesDependency()
        {
            // 'kontrol' adımı condition'da $sayac kullanıyor.
            // DagPlanner v2.0 fix: condition'daki $ref bağımlılık yaratmalı,
            // yani 'sayac' her zaman 'kontrol'den ÖNCE çalışmalı.
            var order = new List<string>();
            var (_, exec) = Build(
                ("sayac",   ctx => { order.Add("sayac"); return 5; }),
                ("kontrol", ctx => { order.Add("kontrol"); return "ok"; }));

            // Manifest'te kontrol ÖNCE yazılmış ama condition bağımlılığı sıralamayı düzeltmeli
            var result = exec.Run(new EgManifest
            {
                Title = "Condition Ref Test",
                Steps = new()
                {
                    new EgStep { Id = "kontrol", Op = "kontrol", Condition = "$sayac >= 3" },
                    new EgStep { Id = "sayac",   Op = "sayac" },
                }
            });

            Assert.True(result.Success);
            // sayac, kontrol'den önce çalışmalı (condition bağımlılığı sayesinde)
            Assert.True(order.IndexOf("sayac") < order.IndexOf("kontrol"),
                "Condition'daki $sayac referansı bağımlılık yaratmalı — sayac önce çalışmalı");
        }
    }
}
