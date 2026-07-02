using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.UI;
using EGBIMOTO.Core.DAG;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.Preview;

namespace EGBIMOTO.Addin
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EgbimotoAppPreviewExtension  (v1.1)
    //
    //  EgbimotoApp'e Preview-Confirm akışı ekler.
    //  Ayrı static class — EgbimotoApp.cs'e dokunulmaz.
    //
    //  Kullanım (Command içinde):
    //    var result = EgbimotoAppPreviewExtension.RunPreviewManifest(
    //        data, EgbimotoApp.ManifestPath("yapisal/duvar_poz_ata_preview.json"));
    //
    //  v1.1 düzeltmeleri:
    //    • Phase 3: DagExecutor.InitialVars ile Phase 1 vars'ı doğru aktarılır
    //    • Phase 3 manifest'i "none" policy ile çalışır — geometri iki kez toplanmaz
    //    • 'partial' keyword kaldırıldı (tek dosya sınıf)
    // ═══════════════════════════════════════════════════════════════════════════

    public static class EgbimotoAppPreviewExtension
    {
        public static ManifestRunResult RunPreviewManifest(
            ExternalCommandData cmd, string manifestPath)
        {
            var manifest = ManifestLoader.Load(manifestPath);
            return RunPreviewManifest(cmd, manifest);
        }

        public static ManifestRunResult RunPreviewManifest(
            ExternalCommandData cmd, EgManifest manifest)
        {
            var doc   = cmd.Application.ActiveUIDocument.Document;
            var uidoc = cmd.Application.ActiveUIDocument;
            var uiapp = cmd.Application;

            // ── Phase 1+2: Read + Gate (transaction YOK) ─────────────────────

            var phase1Executor = new DagExecutor(EgbimotoApp.Registry, () => new Host.RevitOpContext
            {
                Doc          = doc,
                UiDoc        = uidoc,
                UiApp        = uiapp,
                IsAtomicMode = false
            });

            bool userConfirmed = false;

            phase1Executor.UserGateCallback = (PreviewGeometryDto dto) =>
            {
                userConfirmed = PreviewGateWindow.ShowModal(dto);
                return userConfirmed;
            };

            // v13.5: Preview akışı içinde de "selection_gate" kullanılabilsin
            // (örn. önizlemeden önce hangi elemanların dahil edileceğini
            // kullanıcı seçsin) — Phase 1 UI etkileşiminin yapıldığı yer burası.
            phase1Executor.UserSelectionCallback = Revit.SelectionPickerService.CreateCallback(uidoc);

            // Phase 1: gate'e kadar çalış — gate sonrası yazma adımları durur
            phase1Executor.StopAfterGate = true;

            // Phase 1: "none" policy → transaction açılmaz
            var phase1Manifest = CloneAsNonePolicy(manifest);

            DagRunResult phase1Result;
            try
            {
                phase1Result = phase1Executor.Run(phase1Manifest);
            }
            catch (Exception ex)
            {
                return Fail($"Phase 1 hatası: {ex.Message}", "__phase1__");
            }

            // ── Önce Phase 1 başarısız mı? (gate'e gelmeden hata) ────────────
            // userConfirmed=false olduğunda gerçek hatayı "kullanıcı iptal etti"
            // gibi göstermemek için SUCCESS kontrolü ÖNCE gelir.
            if (!phase1Result.Success)
                return phase1Result.ToManifestRunResult();

            // ── Sonra kullanıcı iptal etti mi? ───────────────────────────────
            if (!userConfirmed)
            {
                return new ManifestRunResult
                {
                    Success      = true,   // hata değil — kullanıcı kararı
                    ErrorMessage = "Önizleme kullanıcı tarafından iptal edildi.",
                    ErrorStep    = null,
                    Vars         = phase1Result.Vars,
                    Log          = phase1Result.Log
                };
            }

            // ── Phase 3: Write — Atomic Transaction ──────────────────────────
            // Phase 1'in vars'ını (gate="confirmed" dahil) Phase 3'e aktar.
            // Geometri yeniden toplanmaz — sadece condition'lı yazma adımları çalışır.

            Transaction? outerTx = null;
            if (manifest.IsPreview || manifest.IsAtomic)
            {
                outerTx = new Transaction(doc, $"EGBIMOTO: {manifest.Title}");
                var st = outerTx.Start();
                if (st != TransactionStatus.Started)
                {
                    outerTx.Dispose();
                    return Fail($"Phase 3 Transaction başlatılamadı: {st}", "__phase3_tx__");
                }
            }

            var phase3Executor = new DagExecutor(EgbimotoApp.Registry, () => new Host.RevitOpContext
            {
                Doc              = doc,
                UiDoc            = uidoc,
                UiApp            = uiapp,
                IsAtomicMode     = outerTx != null,
                OuterTransaction = outerTx
            });

            // Phase 1 vars'ını Phase 3'e aktar — gate="confirmed" dahil
            phase3Executor.InitialVars = phase1Result.Vars;

            if (outerTx != null)
            {
                phase3Executor.OnAtomicCommit = () =>
                {
                    if (outerTx.GetStatus() == TransactionStatus.Started)
                        outerTx.Commit();
                };
                phase3Executor.OnAtomicRollback = () =>
                {
                    if (outerTx.GetStatus() == TransactionStatus.Started)
                        outerTx.RollBack();
                };
            }

            DagRunResult phase3Result;
            try
            {
                // Phase 3 manifest: tüm adımlar çalışır.
                // Condition'ı olmayan read adımları → cache hit (Phase 1'den vars var).
                // Condition="$gate == confirmed" adımlar → Phase 1'den gelen vars ile ✓ geçer.
                // Preview gate → InitialVars'ta "confirmed" olduğu için auto-skip edilir.
                phase3Result = phase3Executor.Run(manifest);

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
            {
                TaskDialog.Show("EGBIMOTO — Hata",
                    $"Adım: {result.ErrorStep}\n{result.ErrorMessage}" +
                    "\n\n[ATOMIC] Değişiklikler geri alındı.");
            }

            return result;
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private static EgManifest CloneAsNonePolicy(EgManifest src) => new EgManifest
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
