using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.UI;
using EGBIMOTO.Core.DAG;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.Schedule;

namespace EGBIMOTO.Addin
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EgbimotoApp4D5D  (v1.0)
    //
    //  4D/5D manifest runner — EgbimotoAppPreviewExtension pattern.
    //  EgbimotoApp.cs'e dokunulmaz — ayrı static class.
    //
    //  Kullanım (Command içinde):
    //    EgbimotoApp4D5D.Run4D5DManifest(data,
    //        EgbimotoApp.ManifestPath("zaman_maliyet/eg_4d_structural.json"));
    //
    //  Akış (Preview pattern ile aynı):
    //    Phase 1: read + schedule_gate intercept → FourDFiveDWindow.ShowModal()
    //    Phase 3: kullanıcı onayledıysa → atomic TX içinde yazma adımları
    // ═══════════════════════════════════════════════════════════════════════════

    public static class EgbimotoApp4D5D
    {
        public static ManifestRunResult Run4D5DManifest(
            ExternalCommandData cmd, string manifestPath)
        {
            var manifest = ManifestLoader.Load(manifestPath);
            return Run4D5DManifest(cmd, manifest);
        }

        public static ManifestRunResult Run4D5DManifest(
            ExternalCommandData cmd, EgManifest manifest)
        {
            var doc   = cmd.Application.ActiveUIDocument.Document;
            var uidoc = cmd.Application.ActiveUIDocument;
            var uiapp = cmd.Application;

            // ── Phase 1: Read + Gate (transaction YOK) ────────────────────────

            var phase1Exec = new DagExecutor(EgbimotoApp.Registry, () => new Host.RevitOpContext
            {
                Doc          = doc,
                UiDoc        = uidoc,
                UiApp        = uiapp,
                IsAtomicMode = false
            });

            bool userConfirmed = false;

            phase1Exec.UserScheduleGateCallback = (FourDFiveDDto dto) =>
            {
                userConfirmed = FourDFiveDWindow.ShowModal(dto);
                return userConfirmed;
            };

            phase1Exec.StopAfterGate = true;

            var phase1Manifest = CloneAsNone(manifest);

            DagRunResult phase1Result;
            try
            {
                phase1Result = phase1Exec.Run(phase1Manifest);
            }
            catch (Exception ex)
            {
                return Fail($"Phase 1 hatası: {ex.Message}", "__phase1__");
            }

            if (!phase1Result.Success)
                return phase1Result.ToManifestRunResult();

            if (!userConfirmed)
                return new ManifestRunResult
                {
                    Success      = true,
                    ErrorMessage = "4D/5D önizleme kullanıcı tarafından iptal edildi.",
                    ErrorStep    = null,
                    Vars         = phase1Result.Vars,
                    Log          = phase1Result.Log
                };

            // ── Phase 3: Write — Atomic TX ────────────────────────────────────

            Transaction? outerTx = null;
            if (manifest.IsPreview || manifest.IsAtomic)
            {
                outerTx = new Transaction(doc, $"EGBIMOTO 4D/5D: {manifest.Title}");
                var st = outerTx.Start();
                if (st != TransactionStatus.Started)
                {
                    outerTx.Dispose();
                    return Fail($"Phase 3 Transaction başlatılamadı: {st}", "__phase3_tx__");
                }
            }

            var phase3Exec = new DagExecutor(EgbimotoApp.Registry, () => new Host.RevitOpContext
            {
                Doc              = doc,
                UiDoc            = uidoc,
                UiApp            = uiapp,
                IsAtomicMode     = outerTx != null,
                OuterTransaction = outerTx
            });

            phase3Exec.InitialVars = phase1Result.Vars;

            if (outerTx != null)
            {
                phase3Exec.OnAtomicCommit   = () =>
                {
                    if (outerTx.GetStatus() == TransactionStatus.Started)
                        outerTx.Commit();
                };
                phase3Exec.OnAtomicRollback = () =>
                {
                    if (outerTx.GetStatus() == TransactionStatus.Started)
                        outerTx.RollBack();
                };
            }

            DagRunResult phase3Result;
            try
            {
                phase3Result = phase3Exec.Run(manifest);

                if (!phase3Result.Success &&
                    outerTx?.GetStatus() == TransactionStatus.Started)
                    outerTx.RollBack();
            }
            catch (Exception ex)
            {
                outerTx?.RollBack();
                phase3Result = new DagRunResult
                {
                    Success      = false,
                    ErrorMessage = ex.Message,
                    ErrorStep    = "__phase3_runtime__"
                };
            }
            finally
            {
                outerTx?.Dispose();
            }

            var result = phase3Result.ToManifestRunResult();

            if (!result.Success)
                TaskDialog.Show("EGBIMOTO — Hata",
                    $"Adım: {result.ErrorStep}\n{result.ErrorMessage}" +
                    "\n\n[ATOMIC] Değişiklikler geri alındı.");

            return result;
        }

        private static EgManifest CloneAsNone(EgManifest src) => new EgManifest
        {
            Title             = src.Title,
            Description       = src.Description,
            Category          = src.Category,
            TransactionPolicy = "none",
            Steps             = src.Steps,
            FilePath          = src.FilePath,
            FolderName        = src.FolderName
        };

        private static ManifestRunResult Fail(string msg, string step) => new ManifestRunResult
        {
            Success      = false,
            ErrorMessage = msg,
            ErrorStep    = step
        };
    }
}
