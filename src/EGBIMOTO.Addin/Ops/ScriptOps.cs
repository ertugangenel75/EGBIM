using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO Script Op'ları — Roslyn C# runtime script desteği.
    ///
    /// run_csharp_script:
    ///   Revit Orchestrator'ın RunCSharpScriptCommand'ından uyarlanmıştır.
    ///   Derleme gerektirmeden .cs dosyalarını runtime'da çalıştırır.
    ///
    ///   Script globals:
    ///     uiapp   : UIApplication — çalışan Revit UI
    ///     doc     : Document      — aktif döküman
    ///     inputs  : Dictionary<string,object?> — manifest params
    ///     input   : object?       — önceki adımın çıktısı (from bağlantısı)
    ///
    ///   Script, Dictionary<string, object?> döndürmelidir.
    ///   Örnek:
    ///     var count = new FilteredElementCollector(doc).OfClass(typeof(Wall)).GetElementCount();
    ///     return new Dictionary<string, object?> { ["wall_count"] = count };
    ///
    ///   Manifest kullanımı:
    ///     { "id": "hesap", "op": "run_csharp_script",
    ///       "params": { "script_path": "tools/ozel_hesap.cs" } }
    ///
    ///   Önkoşul: Microsoft.CodeAnalysis.CSharp.Scripting NuGet paketi.
    ///   csproj'a ekle:
    ///     <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Scripting" Version="4.9.2" />
    /// </summary>
    public static class ScriptOps
    {
        // Script seçenekleri — yeniden kullanılabilir, statik
        private static readonly ScriptOptions _baseOptions = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,                           // System.Private.CoreLib
                typeof(Enumerable).Assembly,                       // System.Linq
                typeof(Dictionary<,>).Assembly,                    // System.Collections
                typeof(System.IO.File).Assembly,                   // System.IO.FileSystem
                typeof(System.Text.Json.JsonDocument).Assembly,    // System.Text.Json
                typeof(Document).Assembly,                         // RevitAPI
                typeof(UIApplication).Assembly)                    // RevitAPIUI
            .WithImports(
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.IO",
                "Autodesk.Revit.DB",
                "Autodesk.Revit.UI");

        // Thread-safe script cache — aynı dosya aynı session'da tekrar derlenmez.
        // v8 FIX: Dictionary → ConcurrentDictionary (CommandQueue paralel çalışma güvenliği)
        // key: "dosyaYolu|lastWriteTime"
        private static readonly ConcurrentDictionary<string, Script<object>> _scriptCache = new();

        [EgOp("run_csharp_script",
            Description =
                "Bir .cs dosyasını runtime'da Roslyn ile derler ve çalıştırır — rebuild gerekmez.\n" +
                "Script globals: uiapp (UIApplication), doc (Document), " +
                "inputs (Dictionary<string,object?> — params), input (object? — from bağlantısı).\n" +
                "Script Dictionary<string,object?> döndürmelidir.\n" +
                "params: script_path (zorunlu), cache (opsiyonel, default:true — aynı session'da yeniden derleme yapmaz).",
            Category = "Script")]
        public static Dictionary<string, object?> RunCSharpScript(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] run_csharp_script Revit bağlamı gerektirir.");

            // ── Parametreler ──────────────────────────────────────────────────
            var scriptPath  = ctx.GetString("script_path", "").Trim();
            var useCache    = ctx.GetParam<bool>("cache", true);

            if (string.IsNullOrEmpty(scriptPath))
                throw new ArgumentException($"[{ctx.CurrentStepId}] run_csharp_script: 'script_path' parametresi zorunlu.");

            // Göreli path → AddinDir'e göre çözümle
            if (!Path.IsPathRooted(scriptPath))
                scriptPath = Path.Combine(EgbimotoData.DataRoot, "..", scriptPath);
            scriptPath = Path.GetFullPath(scriptPath);

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException(
                    $"[{ctx.CurrentStepId}] Script dosyası bulunamadı: {scriptPath}");

            // ── Script yükle / cache'ten al ───────────────────────────────────
            var lastWrite  = File.GetLastWriteTimeUtc(scriptPath).ToString("o");
            var cacheKey   = $"{scriptPath}|{lastWrite}";
            Script<object>? script;

            if (useCache && _scriptCache.TryGetValue(cacheKey, out script))
            {
                ctx.Log?.Invoke($"  ⚡ [{ctx.CurrentStepId}] run_csharp_script: cache hit ({Path.GetFileName(scriptPath)})");
            }
            else
            {
                string code;
                try { code = File.ReadAllText(scriptPath); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"[{ctx.CurrentStepId}] Script okunamadı '{scriptPath}': {ex.Message}", ex);
                }

                var options = _baseOptions.WithFilePath(scriptPath);
                Script<object> compiled;
                try
                {
                    compiled = CSharpScript.Create<object>(code, options, typeof(ScriptGlobals));
                    // Compile — hataları erken yakala
                    var diag = compiled.Compile();
                    var errors = diag.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
                    if (errors.Count > 0)
                        throw new InvalidOperationException(
                            $"[{ctx.CurrentStepId}] Derleme hatası ({Path.GetFileName(scriptPath)}):\n" +
                            string.Join("\n  ", errors.Select(e => e.ToString())));
                }
                catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException cex)
                {
                    throw new InvalidOperationException(
                        $"[{ctx.CurrentStepId}] C# derleme hataları ({Path.GetFileName(scriptPath)}):\n" +
                        string.Join("\n  ", cex.Diagnostics.Select(d => d.ToString())), cex);
                }

                // v8 FIX: GetOrAdd ile atomic — iki thread aynı anda derlese bile
                // yalnızca biri cache'e yazılır, ikincisi derlenmiş ama atılır (güvenli)
                if (useCache)
                    script = _scriptCache.GetOrAdd(cacheKey, compiled);
                else
                    script = compiled;

                ctx.Log?.Invoke($"  📝 [{ctx.CurrentStepId}] run_csharp_script: derlendi ({Path.GetFileName(scriptPath)})");
            }

            // ── Globals hazırla ───────────────────────────────────────────────
            var inputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in ctx.Params)
            {
                if (kv.Key == "script_path" || kv.Key == "cache") continue;
                inputs[kv.Key] = kv.Value;
            }

            var globals = new ScriptGlobals
            {
                uiapp  = rctx.UiApp,
                doc    = rctx.Doc,
                inputs = inputs,
                input  = ctx.Input,
            };

            // ── Çalıştır ──────────────────────────────────────────────────────
            object? returnValue;
            try
            {
                var state = script.RunAsync(globals).GetAwaiter().GetResult();
                returnValue = state.ReturnValue;
            }
            catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException cex)
            {
                throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Script derleme hatası: {string.Join("; ", cex.Diagnostics)}", cex);
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Script çalışma hatası ({Path.GetFileName(scriptPath)}): " +
                    $"{inner.GetType().Name}: {inner.Message}", inner);
            }

            return CoerceResult(returnValue);
        }

        /// <summary>
        /// Script cache'ini temizler. Dosyayı değiştirip önbellekten çalıştırmak
        /// istemediğinizde manifestten çağırabilirsiniz.
        /// </summary>
        [EgOp("clear_script_cache",
            Description = "Roslyn script derleme önbelleğini temizler. " +
                          "Script dosyasını değiştirip yeniden derlenmesini istediğinizde kullanın.",
            Category = "Script")]
        public static Dictionary<string, object?> ClearScriptCache(OpContext ctx)
        {
            var count = _scriptCache.Count;
            _scriptCache.Clear();
            ctx.Log?.Invoke($"  🗑 [{ctx.CurrentStepId}] clear_script_cache: {count} script temizlendi");
            return new Dictionary<string, object?> { ["cleared"] = count };
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        /// <summary>
        /// Script dönüş değerini Dictionary<string, object?> formatına çevirir.
        /// Revit Element'leri JSON-safe özete dönüştürür.
        /// </summary>
        private static Dictionary<string, object?> CoerceResult(object? value)
        {
            if (value is Dictionary<string, object?> dn)  return dn;
            if (value is Dictionary<string, object>  dnn) return dnn.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            if (value is IDictionary<string, object?> idn) return new Dictionary<string, object?>(idn);
            if (value is IDictionary<string, object>  idn2) return idn2.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

            // Tek değer → wrap
            return new Dictionary<string, object?> { ["result"] = SummarizeValue(value) };
        }

        private static object? SummarizeValue(object? value) => value switch
        {
            null                                              => null,
            string or bool or int or long or double or float => value,
            Element e => new Dictionary<string, object?>
            {
                ["element_id"] = Rv.GetId(e.Id),
                ["name"]       = e.Name,
                ["category"]   = e.Category?.Name,
                ["type_name"]  = e.GetType().Name,
            },
            ElementId eid                                     => Rv.GetId(eid),
            IEnumerable<object> list                          => list.Select(SummarizeValue).ToList(),
            _                                                 => value.ToString(),
        };

        /// <summary>
        /// Script'lerin erişebildiği global değişkenler.
        /// Public field isimler script içinde doğrudan kullanılır.
        /// </summary>
        public sealed class ScriptGlobals
        {
            /// <summary>Revit UI uygulaması</summary>
            public UIApplication uiapp = null!;
            /// <summary>Aktif Revit dökümanı</summary>
            public Document doc = null!;
            /// <summary>Manifest params'tan gelen parametreler (script_path ve cache hariç)</summary>
            public Dictionary<string, object?> inputs = new();
            /// <summary>Önceki adımın çıktısı (from bağlantısı)</summary>
            public object? input;
        }
    }
}
