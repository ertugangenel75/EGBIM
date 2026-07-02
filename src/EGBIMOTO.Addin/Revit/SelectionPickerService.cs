using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using EGBIMOTO.Core.Selection;
using EGBIMOTO.Addin.Host;   // Rv — sürümler arası ElementId köprüsü

namespace EGBIMOTO.Addin.Revit
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — SelectionPickerService  (v13.5)
    //
    //  DagExecutor.UserSelectionCallback'in gerçek Revit uygulaması.
    //  PreviewGateWindow'un "callback = gerçek Revit işi" deseniyle aynı yerde
    //  durur — ama modal pencere yerine doğrudan uidoc.Selection.PickObject(s)
    //  kullanır (mevcut ExternalCommand'ın ana thread'inde çalıştığı için
    //  ExternalEvent'e gerek yok — RevitOpContext zaten bu varsayımla kurulu).
    //
    //  Kullanım (Command / EgbimotoApp.RunManifest içinde):
    //    executor.UserSelectionCallback = SelectionPickerService.CreateCallback(uidoc);
    // ═══════════════════════════════════════════════════════════════════════════
    public static class SelectionPickerService
    {
        /// <summary>
        /// DagExecutor.UserSelectionCallback için hazır delegate üretir.
        /// uidoc null ise (headless/test) null döner — DagExecutor bu durumu
        /// zaten "callback null" olarak ele alıp Cancelled sonuç üretir.
        /// </summary>
        public static Func<SelectionRequestDto, SelectionResultDto>? CreateCallback(UIDocument? uidoc)
        {
            if (uidoc == null) return null;
            return req => Pick(uidoc, req);
        }

        private static SelectionResultDto Pick(UIDocument uidoc, SelectionRequestDto req)
        {
            var result = new SelectionResultDto { Prompt = req.Prompt };

            ISelectionFilter? filter = null;
            if (req.Categories is { Count: > 0 })
                filter = new CategorySelectionFilter(req.Categories, req.AllowLinked);

            try
            {
                if (string.Equals(req.Mode, "single", StringComparison.OrdinalIgnoreCase))
                {
                    var picked = filter != null
                        ? uidoc.Selection.PickObject(ObjectType.Element, filter, req.Prompt)
                        : uidoc.Selection.PickObject(ObjectType.Element, req.Prompt);

                    if (picked != null)
                        result.ElementIds.Add(Host.Rv.GetId(picked.ElementId));
                }
                else
                {
                    var refs = filter != null
                        ? uidoc.Selection.PickObjects(ObjectType.Element, filter, req.Prompt)
                        : uidoc.Selection.PickObjects(ObjectType.Element, req.Prompt);

                    result.ElementIds.AddRange(
                        refs.Select(r => Host.Rv.GetId(r.ElementId)).Distinct());
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Kullanıcı Esc'e bastı — bu bir hata değil, normal iptal akışı.
                result.Cancelled = true;
                return result;
            }

            // min/max_count doğrulaması — sağlanmazsa kullanıcıya bilgi verip iptal say.
            if (req.MinCount > 0 && result.ElementIds.Count < req.MinCount)
            {
                TaskDialog.Show("EGBIMOTO — Seçim",
                    $"En az {req.MinCount} eleman seçilmeli. Seçilen: {result.ElementIds.Count}.");
                result.Cancelled = true;
                return result;
            }
            if (req.MaxCount > 0 && result.ElementIds.Count > req.MaxCount)
            {
                TaskDialog.Show("EGBIMOTO — Seçim",
                    $"En fazla {req.MaxCount} eleman seçilebilir. Seçilen: {result.ElementIds.Count}.");
                result.Cancelled = true;
                return result;
            }

            result.Cancelled = result.ElementIds.Count == 0;
            return result;
        }

        // ── Kategori filtresi ────────────────────────────────────────────────
        private sealed class CategorySelectionFilter : ISelectionFilter
        {
            private readonly HashSet<BuiltInCategory> _allowed;
            private readonly bool _allowLinked;

            public CategorySelectionFilter(List<string> categoryNames, bool allowLinked)
            {
                _allowLinked = allowLinked;
                _allowed = new HashSet<BuiltInCategory>();
                foreach (var name in categoryNames)
                    if (Ops.EgCategoryResolver.TryResolve(name, out var bic))
                        _allowed.Add(bic);
            }

            public bool AllowElement(Element elem)
            {
                if (elem?.Category == null) return false;
                // v14 fix: IntegerValue Revit 2025+'ta kaldırıldı → Rv.GetCategoryId
                var bic = (BuiltInCategory)Rv.GetCategoryId(elem);
                return _allowed.Count == 0 || _allowed.Contains(bic);
            }

            /// <summary>
            /// Bağlantılı model referansları için kategori filtresi UYGULANMAZ —
            /// yalnızca AllowLinked bayrağına bakılır. Link içi elemanın kategorisini
            /// çözmek RevitLinkInstance.GetLinkDocument() + host-context dönüşümü
            /// gerektirir; v13.5 kapsamında bilinçli olarak basit tutuldu.
            /// İleri sürümde link-aware filtreleme eklenebilir.
            /// </summary>
            public bool AllowReference(Reference reference, XYZ position)
                => _allowLinked;
        }
    }
}
