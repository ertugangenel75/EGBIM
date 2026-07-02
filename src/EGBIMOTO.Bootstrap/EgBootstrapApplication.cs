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

namespace EGBIMOTO.Bootstrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EgBootstrapApplication  —  EGBIMOTO v10
    //
    //  .addin dosyasının yüklediği IExternalApplication thunk'ı.
    //  rst-c / RstBootstrap.cs deseninden uyarlanmıştır.
    //
    //  Görev: engine'i (EGBIMOTO.Addin.dll) bulup yüklemek ve OnStartup/OnShutdown
    //  çağrılarını ona yönlendirmek. Engine içinde gerçek App sınıfı (ribbon,
    //  op'lar, UI) bulunur.
    //
    //  HİBRİT YÜKLEME SIRASI:
    //    1. %AppData%\EGBIMOTO\R<ver>\app\EGBIMOTO.Addin.dll  (MSI kurulumu — tercih)
    //    2. <bootstrap.dll yanındaki>\EGBIMOTO.Addin.dll       (tek-DLL fallback)
    //
    //  Engine bulunduğunda AssemblyDependencyResolver ile tüm bağımlılıklar
    //  (WebView2, Roslyn, native DLL'ler) otomatik çözülür.
    //
    //  Engine içinde aranan tip: "EGBIMOTO.Addin.App" (IExternalApplication).
    // ═══════════════════════════════════════════════════════════════════════════

    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class EgBootstrapApplication : IExternalApplication
    {
        private IExternalApplication? _engine;

        // Engine içindeki gerçek uygulama sınıfı
        private const string EngineTypeName = "EGBIMOTO.Addin.App";
        private const string EngineDllName  = "EGBIMOTO.Addin.dll";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                var revitVersion = application.ControlledApplication.VersionNumber;
                var major = "R" + (revitVersion.Length >= 2
                    ? revitVersion.Substring(revitVersion.Length - 2)
                    : revitVersion);

                BootLog.Info($"EGBIMOTO.Bootstrap başlıyor — Revit {revitVersion} ({major})");
                BootLog.Info($"  bootstrap DLL: {typeof(EgBootstrapApplication).Assembly.Location}");

                var enginePath = ResolveEnginePath(major);
                if (enginePath is null)
                {
                    BootLog.Error("Engine DLL hiçbir konumda bulunamadı.");
                    TaskDialog.Show("EGBIMOTO — Başlatma Hatası",
                        "EGBIMOTO motoru bulunamadı.\n\n" +
                        $"Aranan konumlar:\n" +
                        $"  • %AppData%\\EGBIMOTO\\{major}\\app\\{EngineDllName}\n" +
                        $"  • {Path.GetDirectoryName(typeof(EgBootstrapApplication).Assembly.Location)}\\{EngineDllName}\n\n" +
                        "MSI ile yeniden kurun veya engine DLL'ini bu klasörlerden birine kopyalayın.");
                    return Result.Failed;
                }

                BootLog.Info($"  engine bulundu: {enginePath}");

                // ── AssemblyDependencyResolver ───────────────────────────────
                // EGBIMOTO.Addin.deps.json üzerinden managed + native tüm
                // bağımlılıkları (WebView2, Roslyn, vb.) çözer.
                var resolver = new AssemblyDependencyResolver(enginePath);

                AssemblyLoadContext.Default.Resolving += (ctx, name) =>
                {
                    var p = resolver.ResolveAssemblyToPath(name);
                    if (p is null) return null;
                    BootLog.Info($"  resolve managed: {name.Name} → {p}");
                    return ctx.LoadFromAssemblyPath(p);
                };
                AssemblyLoadContext.Default.ResolvingUnmanagedDll += (asm, name) =>
                {
                    var p = resolver.ResolveUnmanagedDllToPath(name);
                    if (p is null) return IntPtr.Zero;
                    BootLog.Info($"  resolve native: {name} → {p}");
                    return NativeLibrary.Load(p);
                };

                var engineAsm = AssemblyLoadContext.Default.LoadFromAssemblyPath(enginePath);
                BootLog.Info($"  engine yüklendi: {engineAsm.FullName}");

                var engineType = engineAsm.GetType(EngineTypeName, throwOnError: false);
                if (engineType is null)
                {
                    BootLog.Error($"Engine tipi bulunamadı: {EngineTypeName}");
                    return Result.Failed;
                }

                _engine = Activator.CreateInstance(engineType) as IExternalApplication;
                if (_engine is null)
                {
                    BootLog.Error($"{EngineTypeName} IExternalApplication değil.");
                    return Result.Failed;
                }

                BootLog.Info("OnStartup → engine'e yönlendiriliyor");
                var result = _engine.OnStartup(application);
                BootLog.Info($"OnStartup sonuç: {result}");
                return result;
            }
            catch (Exception ex)
            {
                BootLog.Error("OnStartup istisna fırlattı", ex);
                TaskDialog.Show("EGBIMOTO — Başlatma Hatası", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            if (_engine is null)
            {
                BootLog.Info("OnShutdown: engine yüklü değil, atlanıyor.");
                return Result.Succeeded;
            }
            try
            {
                BootLog.Info("OnShutdown → engine'e yönlendiriliyor");
                return _engine.OnShutdown(application);
            }
            catch (Exception ex)
            {
                BootLog.Error("OnShutdown istisna fırlattı", ex);
                return Result.Failed;
            }
        }

        // ── Engine yolu çözümleme (hibrit) ────────────────────────────────────

        /// <summary>
        /// Engine DLL'ini iki konumda arar:
        ///   1. %AppData%\EGBIMOTO\R<ver>\app\  (MSI kurulumu)
        ///   2. Bootstrap DLL'in yanında         (tek-DLL fallback)
        /// İlk bulunanı döner; hiçbiri yoksa null.
        /// </summary>
        private static string? ResolveEnginePath(string major)
        {
            // 1. MSI kurulum konumu
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var msiPath = Path.Combine(appData, "EGBIMOTO", major, "app", EngineDllName);
            if (File.Exists(msiPath))
            {
                BootLog.Info($"  → MSI engine konumu kullanılıyor");
                return msiPath;
            }
            BootLog.Info($"  MSI engine yok: {msiPath}");

            // 2. Bootstrap DLL'in yanı (tek-DLL fallback)
            var sideDir = Path.GetDirectoryName(typeof(EgBootstrapApplication).Assembly.Location);
            if (!string.IsNullOrEmpty(sideDir))
            {
                var sidePath = Path.Combine(sideDir, EngineDllName);
                if (File.Exists(sidePath))
                {
                    BootLog.Info($"  → Yanı (tek-DLL) engine konumu kullanılıyor");
                    return sidePath;
                }
                BootLog.Info($"  Yanı engine yok: {sidePath}");
            }

            return null;
        }
    }
}
