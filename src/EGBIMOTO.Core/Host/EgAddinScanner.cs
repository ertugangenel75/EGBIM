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
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace EGBIMOTO.Core.Host
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EgAddinScanner  —  EGBIMOTO v9
    //
    //  rst-c / AddinDirectoryScanner + AddinDisabler portudur.
    //  Revit'in standart add-in arama yollarını tarar, .addin manifest
    //  dosyalarını parse eder, devre dışı bırakma/geri yükleme yapar.
    //
    //  Kaynak: rst-c/src/RST.Core/AddIns/
    //    AddinDirectoryScanner.cs, AddinDisabler.cs, AddinManifestParser.cs
    //
    //  EGBIMOTO uyarlamaları:
    //    • Serilog kaldırıldı → Action<string> log callback
    //    • RST.Core.Profiles bağımlılığı kaldırıldı → string[] requiredFiles
    //    • System.Management (WMI) bağımlılığı yok — Core katmanında kalır
    //    • Tüm tipler EGBIMOTO.Core.Host namespace altında
    //
    //  Revit arama sırası (Revit Load Order ile özdeş):
    //    1. %AppData%\Autodesk\Revit\Addins\<ver>\
    //    2. %ProgramData%\Autodesk\Revit\Addins\<ver>\
    //    3. %AppData%\Autodesk\ApplicationPlugins\
    //    4. %ProgramData%\Autodesk\ApplicationPlugins\
    //    5. %ProgramFiles%\Autodesk\Revit <ver>\  (read-only)
    //
    //  Kullanım (op'lardan):
    //    var results = EgAddinScanner.Scan("2026");
    //    var disabled = EgAddinDisabler.DisableNonRequired("2026", new[]{"pyRevit.addin"});
    // ═══════════════════════════════════════════════════════════════════════════

    // ── Arama yolu ────────────────────────────────────────────────────────────

    public enum EgAddinPathKind
    {
        UserAddins,              // %AppData%\Autodesk\Revit\Addins\<ver>
        MachineAddins,           // %ProgramData%\Autodesk\Revit\Addins\<ver>
        UserApplicationPlugins,  // %AppData%\Autodesk\ApplicationPlugins
        MachineApplicationPlugins,
        RevitInstall,            // %ProgramFiles%\Autodesk\Revit <ver>  (read-only)
    }

    public sealed record EgAddinSearchPath(
        string           Path,
        EgAddinPathKind  Kind,
        bool             ReadOnly);

    // ── Manifest modeli ───────────────────────────────────────────────────────

    public sealed class EgAddinEntry
    {
        /// <summary>.addin XML içindeki <Name> değeri</summary>
        public string Name     { get; set; } = "";
        /// <summary><AddInId> (GUID)</summary>
        public string AddinId  { get; set; } = "";
        /// <summary><Assembly> yolu</summary>
        public string Assembly { get; set; } = "";
        /// <summary>Application veya Command</summary>
        public string Type     { get; set; } = "";
    }

    public sealed class EgAddinManifest
    {
        /// <summary>Disk üzerindeki tam yol (.addin veya .addin.EGdisabled)</summary>
        public string              FilePath   { get; set; } = "";
        /// <summary>Sadece dosya adı (yol olmadan)</summary>
        public string              FileName   { get; set; } = "";
        /// <summary>Klasör</summary>
        public string              Directory  { get; set; } = "";
        /// <summary>true → .EGdisabled uzantısıyla devre dışı</summary>
        public bool                IsDisabled { get; set; }
        /// <summary>XML içindeki tüm AddIn girişleri</summary>
        public List<EgAddinEntry>  Entries    { get; set; } = new();
    }

    // ── Tarayıcı ──────────────────────────────────────────────────────────────

    public static class EgAddinScanner
    {
        public const string DisabledSuffix = ".EGdisabled";

        // ── Arama yolları ────────────────────────────────────────────────────

        public static IReadOnlyList<EgAddinSearchPath> GetSearchPaths(string revitVersion)
        {
            var ver   = (revitVersion ?? "").Trim();
            var roots = new List<EgAddinSearchPath>();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                var userAddins = System.IO.Path.Combine(appData, "Autodesk", "Revit", "Addins", ver);
                if (Directory.Exists(userAddins))
                    roots.Add(new EgAddinSearchPath(userAddins, EgAddinPathKind.UserAddins, ReadOnly: false));

                var userPlugins = System.IO.Path.Combine(appData, "Autodesk", "ApplicationPlugins");
                if (Directory.Exists(userPlugins))
                    roots.Add(new EgAddinSearchPath(userPlugins, EgAddinPathKind.UserApplicationPlugins, ReadOnly: false));
            }

            var progData = Environment.GetEnvironmentVariable("PROGRAMDATA")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(progData))
            {
                var machineAddins = System.IO.Path.Combine(progData, "Autodesk", "Revit", "Addins", ver);
                if (Directory.Exists(machineAddins))
                    roots.Add(new EgAddinSearchPath(machineAddins, EgAddinPathKind.MachineAddins, ReadOnly: false));

                var machinePlugins = System.IO.Path.Combine(progData, "Autodesk", "ApplicationPlugins");
                if (Directory.Exists(machinePlugins))
                    roots.Add(new EgAddinSearchPath(machinePlugins, EgAddinPathKind.MachineApplicationPlugins, ReadOnly: false));
            }

            var progFiles = Environment.GetEnvironmentVariable("PROGRAMFILES")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(progFiles) && !string.IsNullOrEmpty(ver))
            {
                var revitInstall = System.IO.Path.Combine(progFiles, "Autodesk", $"Revit {ver}");
                if (Directory.Exists(revitInstall))
                    roots.Add(new EgAddinSearchPath(revitInstall, EgAddinPathKind.RevitInstall, ReadOnly: true));
            }

            return roots;
        }

        // ── Tarama ──────────────────────────────────────────────────────────

        /// <summary>
        /// Verilen Revit versiyonu için tüm arama yollarını tarar.
        /// Aynı canonical yol birden fazla bulunursa tekrar döndürülmez.
        /// </summary>
        public static IReadOnlyList<EgAddinManifest> Scan(
            string revitVersion,
            Action<string, Exception>? onSkip = null)
        {
            var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<EgAddinManifest>();

            foreach (var searchPath in GetSearchPaths(revitVersion))
            foreach (var manifest in ParseDirectory(searchPath.Path, onSkip))
            {
                var key = NormalizePath(manifest.FilePath);
                if (seen.Add(key))
                    result.Add(manifest);
            }

            return result;
        }

        /// <summary>Kaynak yoluyla birlikte tarama (disable policy için).</summary>
        public static IReadOnlyList<(EgAddinManifest Manifest, EgAddinSearchPath Source)> ScanWithSource(
            string revitVersion,
            Action<string, Exception>? onSkip = null)
        {
            var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<(EgAddinManifest, EgAddinSearchPath)>();

            foreach (var searchPath in GetSearchPaths(revitVersion))
            foreach (var manifest in ParseDirectory(searchPath.Path, onSkip))
            {
                var key = NormalizePath(manifest.FilePath);
                if (seen.Add(key))
                    result.Add((manifest, searchPath));
            }

            return result;
        }

        // ── XML parse ───────────────────────────────────────────────────────

        internal static IEnumerable<EgAddinManifest> ParseDirectory(
            string dirPath,
            Action<string, Exception>? onSkip = null)
        {
            if (!Directory.Exists(dirPath)) yield break;

            // .bundle klasörleri de dahil — recursive walk
            foreach (var file in Directory.EnumerateFiles(dirPath, "*.addin*", SearchOption.AllDirectories))
            {
                var ext = System.IO.Path.GetExtension(file);

                bool isActive   = ext.Equals(".addin",      StringComparison.OrdinalIgnoreCase);
                bool isDisabled = file.EndsWith(".addin" + DisabledSuffix,  StringComparison.OrdinalIgnoreCase)
                               || file.EndsWith(".addin.RSTdisabled",        StringComparison.OrdinalIgnoreCase); // rst-c uyumu

                if (!isActive && !isDisabled) continue;

                EgAddinManifest? manifest = null;
                try { manifest = ParseFile(file, isDisabled); }
                catch (Exception ex) { onSkip?.Invoke(file, ex); }

                if (manifest != null) yield return manifest;
            }
        }

        private static EgAddinManifest ParseFile(string filePath, bool isDisabled)
        {
            var doc     = XDocument.Load(filePath);
            var entries = new List<EgAddinEntry>();

            foreach (var addIn in doc.Descendants("AddIn"))
            {
                entries.Add(new EgAddinEntry
                {
                    Type     = addIn.Attribute("type")?.Value ?? "",
                    Name     = addIn.Element("Name")?.Value    ?? "",
                    AddinId  = addIn.Element("AddInId")?.Value ?? "",
                    Assembly = addIn.Element("Assembly")?.Value ?? "",
                });
            }

            // Gerçek dosya adı: .addin.EGdisabled → .addin
            var displayName = System.IO.Path.GetFileName(filePath);
            if (displayName.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                displayName = displayName.Substring(0, displayName.Length - DisabledSuffix.Length);
            if (displayName.EndsWith(".RSTdisabled", StringComparison.OrdinalIgnoreCase))
                displayName = displayName.Substring(0, displayName.Length - ".RSTdisabled".Length);

            return new EgAddinManifest
            {
                FilePath   = filePath,
                FileName   = displayName,
                Directory  = System.IO.Path.GetDirectoryName(filePath) ?? "",
                IsDisabled = isDisabled,
                Entries    = entries,
            };
        }

        private static string NormalizePath(string p)
        {
            try { return System.IO.Path.GetFullPath(p); }
            catch { return p; }
        }
    }

    // ── Devre dışı bırakma / geri yükleme ────────────────────────────────────

    public sealed record EgDisableResult(
        int                     DisabledCount,
        int                     SkippedReadOnly,
        int                     SkippedAlreadyDisabled,
        int                     Failed,
        IReadOnlyList<string>   DisabledFiles,
        IReadOnlyList<string>   FailedFiles);

    public sealed record EgRestoreResult(
        int                     RestoredCount,
        int                     Failed,
        IReadOnlyList<string>   RestoredFiles,
        IReadOnlyList<string>   FailedFiles);

    public static class EgAddinDisabler
    {
        /// <summary>
        /// requiredFiles listesinde olmayan tüm .addin dosyalarını
        /// .addin.EGdisabled olarak yeniden adlandırır.
        /// ReadOnly yollar (RevitInstall) atlanır.
        /// </summary>
        /// <param name="requiredFiles">Korunacak .addin dosya adları (büyük/küçük harf duyarsız)</param>
        public static EgDisableResult DisableNonRequired(
            string          revitVersion,
            IEnumerable<string> requiredFiles,
            Action<string>? log = null)
        {
            var required  = new HashSet<string>(requiredFiles, StringComparer.OrdinalIgnoreCase);
            var disabled  = new List<string>();
            var failed    = new List<string>();
            int skipRO    = 0;
            int skipAlr   = 0;

            var scan = EgAddinScanner.ScanWithSource(revitVersion);

            // EGBIMOTO'nun kendi .addin'ini asla devre dışı bırakma
            required.Add("EGBIMOTO.addin");

            foreach (var (manifest, source) in scan)
            {
                if (source.ReadOnly) { skipRO++; continue; }
                if (manifest.IsDisabled) { skipAlr++; continue; }
                if (required.Contains(manifest.FileName)) continue;

                var disabledPath = manifest.FilePath + EgAddinScanner.DisabledSuffix;
                try
                {
                    File.Move(manifest.FilePath, disabledPath);
                    disabled.Add(manifest.FileName);
                    log?.Invoke($"  Devre dışı: {manifest.FileName}");
                }
                catch (Exception ex)
                {
                    failed.Add(manifest.FileName);
                    log?.Invoke($"  HATA ({manifest.FileName}): {ex.Message}");
                }
            }

            return new EgDisableResult(disabled.Count, skipRO, skipAlr, failed.Count, disabled, failed);
        }

        /// <summary>
        /// Tüm .addin.EGdisabled ve .addin.RSTdisabled dosyalarını geri yükler.
        /// </summary>
        public static EgRestoreResult RestoreAll(
            string          revitVersion,
            Action<string>? log = null)
        {
            var restored = new List<string>();
            var failed   = new List<string>();

            var scan = EgAddinScanner.ScanWithSource(revitVersion);

            foreach (var (manifest, source) in scan)
            {
                if (!manifest.IsDisabled) continue;
                if (source.ReadOnly) continue;

                // .addin.EGdisabled → .addin
                // .addin.RSTdisabled → .addin
                string originalPath;
                if (manifest.FilePath.EndsWith(EgAddinScanner.DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                    originalPath = manifest.FilePath.Substring(0, manifest.FilePath.Length - EgAddinScanner.DisabledSuffix.Length);
                else
                    originalPath = manifest.FilePath.Substring(0, manifest.FilePath.Length - ".RSTdisabled".Length);

                try
                {
                    File.Move(manifest.FilePath, originalPath);
                    restored.Add(manifest.FileName);
                    log?.Invoke($"  Geri yüklendi: {manifest.FileName}");
                }
                catch (Exception ex)
                {
                    failed.Add(manifest.FileName);
                    log?.Invoke($"  HATA ({manifest.FileName}): {ex.Message}");
                }
            }

            return new EgRestoreResult(restored.Count, failed.Count, restored, failed);
        }

        /// <summary>
        /// Belirli bir add-in'i tek başına geri yükler (tabName veya fileName ile eşleş).
        /// </summary>
        public static bool RestoreSingle(string revitVersion, string addinFileName, Action<string>? log = null)
        {
            var scan = EgAddinScanner.ScanWithSource(revitVersion);
            foreach (var (manifest, source) in scan)
            {
                if (!manifest.IsDisabled) continue;
                if (source.ReadOnly) continue;
                if (!string.Equals(manifest.FileName, addinFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string originalPath = manifest.FilePath.EndsWith(EgAddinScanner.DisabledSuffix, StringComparison.OrdinalIgnoreCase)
                    ? manifest.FilePath.Substring(0, manifest.FilePath.Length - EgAddinScanner.DisabledSuffix.Length)
                    : manifest.FilePath.Substring(0, manifest.FilePath.Length - ".RSTdisabled".Length);

                try
                {
                    File.Move(manifest.FilePath, originalPath);
                    log?.Invoke($"  Geri yüklendi: {manifest.FileName}");
                    return true;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"  HATA: {ex.Message}");
                    return false;
                }
            }
            return false;
        }
    }
}
