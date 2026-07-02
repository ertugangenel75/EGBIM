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
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Ops;
using EGBIMOTO.Addin.UI;
using EGBIMOTO.Core.DAG;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.AI;

namespace EGBIMOTO.Addin
{
    public static class EgbimotoApp
    {
        public static OpRegistry Registry      { get; private set; } = new();
        public static string      AddinDir     { get; private set; } = "";
        public static string      ManifestRoot { get; private set; } = "";

        /// <summary>
        /// Kullanıcının eklediği manifest'ler: %AppData%/EGBIMOTO/user_manifests
        /// Built-in'den ayrı tutulur; güncelleme/yeniden kurulumda silinmez.
        /// </summary>
        public static string UserManifestRoot { get; private set; } = "";

        /// <summary>
        /// Aktif Revit projesinin yanındaki /eg_manifests klasörü.
        /// Proje açık değilse veya kaydedilmemişse boş string döner.
        /// Her sorgulamada güncel proje yoluna göre yeniden hesaplanır.
        /// </summary>
        public static string ProjectManifestRoot(Document? doc)
        {
            if (doc is null || doc.IsFamilyDocument) return "";
            var modelPath = doc.PathName;
            if (string.IsNullOrWhiteSpace(modelPath)) return ""; // kaydedilmemiş proje
            var dir = Path.GetDirectoryName(modelPath);
            if (string.IsNullOrWhiteSpace(dir)) return "";
            return Path.Combine(dir, "eg_manifests");
        }

        /// <summary>op_contracts.json yolu — ManifestGenerator ve PatternEngine için</summary>
        public static string ContractsPath => Path.Combine(AddinDir, "op_contracts.json");

        // v2.0 — Hardcoded fallback (categories.json yoksa kullanılır)
        private static readonly string[] _defaultCategories =
        {
            "metraj","maliyet","kalip","yapisal","yapisal_v4","mep","ifc","ids",
            "parametreler","dogrulama","wbs","raporlama","semantik",
            "koordinasyon","mimari","elektrik","mekanik","proje_yonetimi",
            "qa","sihhi_tesisat","yangin","etl","preview_samples",
            "genel","sistem","parametrik","zaman_maliyet"
        };

        public static void Initialize(string addinDir)
        {
            AddinDir     = addinDir;
            ManifestRoot = Path.Combine(addinDir, "manifests");

            // User manifest kökü: %AppData%/EGBIMOTO/user_manifests
            UserManifestRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EGBIMOTO", "user_manifests");
            Directory.CreateDirectory(UserManifestRoot);

            // ── v9: Bootstrap dizin yapısı ve log ─────────────────────────────
            Host.EgBootstrap.EnsureDirectories("2026");
            Host.EgBootstrap.WriteLog($"EGBIMOTO Initialize — addinDir={addinDir}");

            EgbimotoData.Initialize(addinDir);
            Registry.ScanAssembly(Assembly.GetExecutingAssembly());
            OpRegistry.Instance = Registry;

            // v2.0 — Config-driven manifest kategorileri
            // categories.json varsa ondan oku, yoksa _defaultCategories kullan
            var categories = LoadCategoriesFromConfig() ?? _defaultCategories;
            foreach (var sub in categories)
                Directory.CreateDirectory(Path.Combine(ManifestRoot, sub));
        }

        /// <summary>
        /// categories.json'dan manifest klasör listesini yükler.
        /// Dosya yoksa veya parse edilemezse null döner → fallback devreye girer.
        /// </summary>
        private static string[]? LoadCategoriesFromConfig()
        {
            var configPath = Path.Combine(AddinDir, "categories.json");
            if (!File.Exists(configPath)) return null;
            try
            {
                var json = File.ReadAllText(configPath, System.Text.Encoding.UTF8);
                return System.Text.Json.JsonSerializer.Deserialize<string[]>(json);
            }
            catch { return null; }
        }

        // ── Ana çalıştırma ────────────────────────────────────────────────────

        public static ManifestRunResult RunManifest(ExternalCommandData cmd, string manifestPath)
        {
            var manifest = ManifestLoader.Load(manifestPath);
            return RunManifest(cmd, manifest);
        }

        /// <summary>
        /// Manifest'i çalıştırır. onStep verilirse her adım tamamlandığında
        /// (stepId, op, milisaniye, başarılı?) ile çağrılır — UI canlı progress için.
        /// </summary>
        public static ManifestRunResult RunManifest(
            ExternalCommandData cmd,
            EgManifest manifest,
            Action<string, string, long, bool>? onStep)
        {
            _pendingOnStep = onStep;
            try { return RunManifest(cmd, manifest); }
            finally { _pendingOnStep = null; }
        }

        // UI'dan geçirilen adım callback'i — RunManifest(cmd, manifest, onStep) set eder
        [ThreadStatic] private static Action<string, string, long, bool>? _pendingOnStep;

        public static ManifestRunResult RunManifest(ExternalCommandData cmd, EgManifest manifest)
        {
            if (manifest.IsPreview)
                return EgbimotoAppPreviewExtension.RunPreviewManifest(cmd, manifest);

            var doc   = cmd.Application.ActiveUIDocument.Document;
            var uidoc = cmd.Application.ActiveUIDocument;
            var uiapp = cmd.Application;

            // ── $INPUT token çözümleme ────────────────────────────────────────
            var prepared = PrepareManifestInputs(manifest, doc);
            if (prepared is null)
                return new ManifestRunResult { Success = true, ErrorMessage = "Kullanıcı iptal etti." };
            manifest = prepared;

            // ── Atomic transaction ────────────────────────────────────────────
            Transaction? outerTx = null;
            if (manifest.IsAtomic)
            {
                outerTx = new Transaction(doc, $"EGBIMOTO: {manifest.Title}");
                var st = outerTx.Start();
                if (st != TransactionStatus.Started)
                {
                    TaskDialog.Show("EGBIMOTO — Hata", $"Atomic Transaction başlatılamadı: {st}");
                    outerTx.Dispose();
                    return new ManifestRunResult { Success = false, ErrorMessage = $"Transaction başlatılamadı: {st}", ErrorStep = "__init__" };
                }
            }

            // ── DagExecutor ───────────────────────────────────────────────────
            var executor = new DagExecutor(Registry, () => new Host.RevitOpContext
            {
                Doc = doc, UiDoc = uidoc, UiApp = uiapp,
                IsAtomicMode = manifest.IsAtomic, OuterTransaction = outerTx
            });

            if (manifest.IsAtomic && outerTx is not null)
            {
                executor.OnAtomicCommit   = () => { if (outerTx.GetStatus() == TransactionStatus.Started) outerTx.Commit(); };
                executor.OnAtomicRollback = () => { if (outerTx.GetStatus() == TransactionStatus.Started) outerTx.RollBack(); };
            }

            // v13.5: interaktif seçim — "selection_gate" op'u bu callback üzerinden
            // gerçek uidoc.Selection.PickObject(s)'e bağlanır.
            executor.UserSelectionCallback = Revit.SelectionPickerService.CreateCallback(uidoc);

            // ── v7.1 + v10: Adım bazlı progress ──────────────────────────────
            // Her op tamamlandığında hem Debug'a yaz hem de UI callback'i çağır
            var uiOnStep = _pendingOnStep;
            executor.OnStepCompleted = (stepId, op, ms, success) =>
            {
                var icon = success ? "✓" : "✗";
                System.Diagnostics.Debug.WriteLine($"[EGBIMOTO] {icon} {stepId} ({op}) {ms}ms");
                uiOnStep?.Invoke(stepId, op, ms, success);
            };

            DagRunResult dagResult;
            try
            {
                dagResult = executor.Run(manifest);
                if (!dagResult.Success && manifest.IsAtomic && outerTx?.GetStatus() == TransactionStatus.Started)
                    outerTx.RollBack();
            }
            catch (Exception ex)
            {
                if (manifest.IsAtomic && outerTx?.GetStatus() == TransactionStatus.Started) outerTx.RollBack();
                dagResult = new DagRunResult { Success = false, ErrorMessage = ex.Message, ErrorStep = "__runtime__" };
            }
            finally { outerTx?.Dispose(); }

            var result = dagResult.ToManifestRunResult();
            if (!result.Success)
            {
                var note = manifest.IsAtomic ? "\n\n[ATOMIC] Değişiklikler geri alındı." : "";
                TaskDialog.Show("EGBIMOTO — Hata", $"Adım: {result.ErrorStep}\n{result.ErrorMessage}{note}");
            }
            return result;
        }

        // ── $INPUT token çözümleme ────────────────────────────────────────────

        /// <summary>
        /// Manifest'te $INPUT token varsa dialog açar ve değerleri uygular.
        /// Kullanıcı iptal ederse null döner → RunManifest çalıştırmaz.
        /// </summary>
        public static EgManifest? PrepareManifestInputs(EgManifest manifest, Document doc)
        {
            var service = new ManifestInputService(doc);
            return service.PrepareManifest(manifest);
        }

        public static string ManifestPath(string relative)
            => Path.Combine(ManifestRoot, relative);
    }
}
