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
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Autodesk.Revit.UI;

namespace EGBIMOTO.Addin.Host
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EgBootstrap  —  EGBIMOTO v9
    //
    //  rst-c / RstBootstrap.cs portudur.
    //
    //  Amaç:
    //    EGBIMOTO şu an tek DLL olarak çalışıyor (EGBIMOTO.Addin.dll).
    //    Bu sınıf, ileride Bootstrap + Engine ayrımı yapıldığında hazır altyapıyı
    //    sağlar — EGBIMOTO.Bootstrap.dll (küçük, versiyonsuz) + EGBIMOTO.Addin.dll
    //    (engine, versiyonlu, %AppData%\EGBIMOTO\R26\app\ altında).
    //
    //  Mevcut kullanım (v9):
    //    EgBootstrap.TryLoadSideEngine() → şu an no-op, sadece log üretir.
    //    EgBootstrap.ResolveEnginePath() → engine DLL yolunu döner (mevcut assembly).
    //
    //  Gelecek kullanım (v10+):
    //    .addin dosyası sadece EGBIMOTO.Bootstrap.dll'e işaret eder.
    //    EgBootstrap.OnStartup() → engine'i %AppData%\EGBIMOTO\R<ver>\app\'dan yükler.
    //    Güncelleme: Bootstrap.dll değişmez, sadece app\ klasörü güncellenir.
    //    Revit restart gerekmez (cold update: sonraki açılışta devreye girer).
    //
    //  Kaynak: rst-c/src/RST.Bootstrap/RstBootstrap.cs
    //
    //  Dizin yapısı (v10 hedefi):
    //    %AppData%\EGBIMOTO\
    //      R24\app\EGBIMOTO.Addin.dll   ← Revit 2024 engine
    //      R25\app\EGBIMOTO.Addin.dll   ← Revit 2025 engine
    //      R26\app\EGBIMOTO.Addin.dll   ← Revit 2026 engine (varsayılan)
    //      logs\egbimoto_YYYYMMDD.log
    //      manifests\
    //      data\
    // ═══════════════════════════════════════════════════════════════════════════

    public static class EgBootstrap
    {
        // ── Yollar ────────────────────────────────────────────────────────────

        /// <summary>
        /// EGBIMOTO AppData kök dizini.
        /// %AppData%\EGBIMOTO\
        /// </summary>
        public static string AppDataRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EGBIMOTO");

        /// <summary>
        /// Revit versiyonuna göre engine dizini.
        /// %AppData%\EGBIMOTO\R26\app\
        /// </summary>
        public static string EngineDir(string revitVersion)
        {
            var major = RevitMajor(revitVersion);
            return Path.Combine(AppDataRoot, major, "app");
        }

        /// <summary>
        /// Engine DLL yolu (v10 mimarisi).
        /// %AppData%\EGBIMOTO\R26\app\EGBIMOTO.Addin.dll
        /// </summary>
        public static string EnginePath(string revitVersion)
            => Path.Combine(EngineDir(revitVersion), "EGBIMOTO.Addin.dll");

        /// <summary>
        /// Log dizini.
        /// %AppData%\EGBIMOTO\logs\
        /// </summary>
        public static string LogDir =>
            Path.Combine(AppDataRoot, "logs");

        // ── Engine yükleme (v10 hazırlık) ────────────────────────────────────

        /// <summary>
        /// Side-engine yükleme denemesi.
        ///
        /// v9 davranışı:
        ///   %AppData%\EGBIMOTO\R<ver>\app\EGBIMOTO.Addin.dll mevcut değilse
        ///   mevcut assembly yolunu döner ve devam eder.
        ///   Engine DLL bulunursa AssemblyDependencyResolver ile yükler.
        ///
        /// v10 davranışı (hedef):
        ///   Bootstrap.dll her zaman sabit kalır. Engine DLL app\ altında
        ///   güncellenir. Yeni sürüm sonraki Revit açılışında devreye girer.
        ///
        /// null döner → engine yüklenmedi, caller mevcut assembly'i kullanır.
        /// </summary>
        public static Assembly? TryLoadSideEngine(
            string          revitVersion,
            Action<string>? log = null)
        {
            var enginePath = EnginePath(revitVersion);

            if (!File.Exists(enginePath))
            {
                log?.Invoke($"[EgBootstrap] Side-engine bulunamadı: {enginePath}");
                log?.Invoke($"[EgBootstrap] Mevcut assembly kullanılıyor.");
                return null;
            }

            log?.Invoke($"[EgBootstrap] Side-engine bulundu: {enginePath}");

            try
            {
                // AssemblyDependencyResolver → deps.json üzerinden tüm
                // bağımlılıkları (managed + native) otomatik çözer.
                var resolver = new AssemblyDependencyResolver(enginePath);

                AssemblyLoadContext.Default.Resolving += (ctx, name) =>
                {
                    var path = resolver.ResolveAssemblyToPath(name);
                    if (path is null) return null;
                    log?.Invoke($"[EgBootstrap] Managed resolve: {name.Name} → {path}");
                    return ctx.LoadFromAssemblyPath(path);
                };

                AssemblyLoadContext.Default.ResolvingUnmanagedDll += (asm, name) =>
                {
                    var path = resolver.ResolveUnmanagedDllToPath(name);
                    if (path is null) return IntPtr.Zero;
                    log?.Invoke($"[EgBootstrap] Native resolve: {name} → {path}");
                    return NativeLibrary.Load(path);
                };

                var engineAsm = AssemblyLoadContext.Default.LoadFromAssemblyPath(enginePath);
                log?.Invoke($"[EgBootstrap] Engine yüklendi: {engineAsm.FullName}");
                return engineAsm;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[EgBootstrap] Engine yüklenemedi: {ex.Message}");
                return null;
            }
        }

        // ── AppData yapısı ────────────────────────────────────────────────────

        /// <summary>
        /// EGBIMOTO AppData klasör yapısını oluşturur.
        /// Initialize() sırasında çağrılır.
        /// </summary>
        public static void EnsureDirectories(string revitVersion)
        {
            var dirs = new[]
            {
                AppDataRoot,
                EngineDir(revitVersion),
                LogDir,
                Path.Combine(AppDataRoot, "manifests"),
                Path.Combine(AppDataRoot, "data"),
                Path.Combine(AppDataRoot, "profiles"),
            };

            foreach (var d in dirs)
            {
                try { Directory.CreateDirectory(d); }
                catch { /* ignore */ }
            }
        }

        // ── Log ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Basit rolling file logger (Serilog bağımlılığı olmadan).
        /// Her gün yeni dosya: %AppData%\EGBIMOTO\logs\egbimoto_YYYYMMDD.log
        /// </summary>
        public static void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                var logFile = Path.Combine(LogDir, $"egbimoto_{DateTime.Now:yyyyMMdd}.log");
                var line    = $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}";
                File.AppendAllText(logFile, line, System.Text.Encoding.UTF8);

                // Rolling: 7 günden eski log dosyalarını sil
                PruneOldLogs();
            }
            catch { /* log hatası asla throw etmez */ }
        }

        private static DateTime _lastPrune = DateTime.MinValue;

        private static void PruneOldLogs()
        {
            if ((DateTime.Now - _lastPrune).TotalHours < 24) return;
            _lastPrune = DateTime.Now;
            try
            {
                var cutoff = DateTime.Now.AddDays(-7);
                foreach (var f in Directory.GetFiles(LogDir, "egbimoto_*.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(f) < cutoff)
                            File.Delete(f);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ── Versiyon yardımcıları ─────────────────────────────────────────────

        /// <summary>
        /// "2026" → "R26"
        /// "2025" → "R25"
        /// </summary>
        public static string RevitMajor(string revitVersion)
        {
            var v = (revitVersion ?? "").Trim();
            if (v.Length >= 2)
                return "R" + v.Substring(v.Length - 2);
            return "R26"; // fallback
        }

        /// <summary>
        /// Çalışan Revit'in version number'ını UIApplication üzerinden alır.
        /// Başarısız olursa "2026" döner.
        /// </summary>
        public static string GetRunningRevitVersion(UIApplication? uiapp)
        {
            try { return uiapp?.Application?.VersionNumber ?? "2026"; }
            catch { return "2026"; }
        }

        // ── Kurulum bilgisi ───────────────────────────────────────────────────

        /// <summary>
        /// Mevcut engine bilgisini string olarak döner (log/TaskDialog için).
        /// </summary>
        public static string GetEngineInfo(string revitVersion)
        {
            var currentAsm = Assembly.GetExecutingAssembly();
            var ver        = currentAsm.GetName().Version?.ToString() ?? "?";
            var loc        = currentAsm.Location;
            var enginePath = EnginePath(revitVersion);
            var hasSide    = File.Exists(enginePath);

            return $"EGBIMOTO v{ver}\n" +
                   $"Revit: {revitVersion} ({RevitMajor(revitVersion)})\n" +
                   $"Assembly: {loc}\n" +
                   $"AppData: {AppDataRoot}\n" +
                   $"Side-engine: {(hasSide ? enginePath : "(yok — mevcut assembly kullanılıyor)")}";
        }
    }
}
