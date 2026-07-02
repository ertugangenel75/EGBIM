using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Selection;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — SelectionOps  (v13.5)
    //
    //  İnteraktif seçim katmanı — iki op:
    //
    //  1. selection_gate
    //     DagExecutor tarafından intercept edilir (preview_gate/schedule_gate
    //     ile aynı desen) — bu metod normal akışta ÇAĞRILMAZ. Kayıt burada
    //     yalnızca OpRegistry/op_contracts.json/ManifestBrowserWindow'un op'u
    //     tanıması içindir (bkz. ManifestLinter kontrol #1).
    //
    //  2. selection_to_elements
    //     Gerçek op — selection_gate'in ürettiği SelectionResultDto'daki
    //     ElementId'leri (from: gate_step_id) Document'tan gerçek Element
    //     nesnelerine çevirir. Silinmiş/geçersiz ID'ler sessizce atlanır ve
    //     loglanır (worksharing senkron gecikmesi gibi durumlar için toleranslı).
    //
    //  Manifest kullanımı:
    //    { "id":"s1","op":"selection_gate",
    //      "inputs":{"prompt":"Kontrol edilecek duvarları seçin",
    //                 "categories":["OST_Walls"],"min_count":1} }
    //    { "id":"s2","op":"selection_to_elements","from":"s1",
    //      "condition":"$s1 == confirmed" }
    //    { "id":"s3","op":"filter_by_param","from":"s2", ... }
    // ═══════════════════════════════════════════════════════════════════════════
    public static class SelectionOps
    {
        [EgOp("selection_gate",
            Description = "DagExecutor tarafından intercept edilir — uidoc.Selection.PickObject(s) çalıştırır. " +
                          "Bu metod ÇAĞRILMAZ; sadece OpRegistry kaydı içindir. " +
                          "params: prompt, mode(single|multiple), categories(OST_ listesi), " +
                          "min_count, max_count, allow_linked. " +
                          "Çıktı: vars[stepId] = SelectionResultDto (ToString() → 'confirmed'|'cancelled').",
            Category    = "Seçim",
            RequiresTransaction = false)]
        public static object SelectionGate(OpContext ctx)
        {
            throw new InvalidOperationException(
                $"[{ctx.CurrentStepId}] selection_gate doğrudan çağrıldı. " +
                "DagExecutor.UserSelectionCallback set edilmemiş veya DagExecutor eski sürüm. " +
                "Bkz. EgbimotoApp.RunManifest() → SelectionPickerService.CreateCallback(uidoc).");
        }

        [EgOp("selection_to_elements",
            Description = "selection_gate çıktısındaki (SelectionResultDto) ElementId'leri gerçek " +
                          "Element listesine çevirir. Silinmiş/geçersiz ID'ler atlanır. " +
                          "Input: SelectionResultDto (from: selection_gate step id). " +
                          "Çıktı: List<Element>.",
            Category    = "Seçim")]
        public static List<Element> SelectionToElements(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] selection_to_elements Revit bağlamı gerektirir.");

            var sel = ctx.InputAs<SelectionResultDto>();
            var result = new List<Element>();

            if (sel.Cancelled)
            {
                ctx.Log($"  selection_to_elements: seçim iptal edildi — boş liste döndürülüyor");
                return result;
            }

            int skipped = 0;
            foreach (var idVal in sel.ElementIds)
            {
                var el = rctx.Doc.GetElement(Rv.MakeElementId(idVal));
                if (el != null) result.Add(el);
                else skipped++;
            }

            if (skipped > 0)
                ctx.Log($"  ⚠ selection_to_elements: {skipped} ID modelde bulunamadı (silinmiş olabilir)");
            ctx.Log($"  selection_to_elements: {result.Count} eleman çözüldü");

            return result;
        }
    }
}
