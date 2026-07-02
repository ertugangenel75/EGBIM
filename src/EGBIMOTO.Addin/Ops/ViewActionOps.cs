using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// v12 — Görünüm üzerinde geçici (kalıcı model değişikliği yapmayan, ama
    /// Transaction gerektiren) aksiyonlar: izolasyon, geçici gizleme, sıfırlama,
    /// renk override, seçim kutusu.
    ///
    /// PCH_FilterMan (Marcin Marek) eklentisindeki view_actions.py'nin
    /// kategoriden bağımsız C# portu. FilterMan'de her aksiyon için ayrı bir
    /// IExternalEventHandler vardı (WPF modeless pencereden çağrıldığı için
    /// gerekliydi); EGBIMOTO'da manifest zaten Revit ana thread'inde
    /// (ribbon komutu → DagExecutor) çalıştığından ExternalEvent katmanına
    /// gerek yoktur — doğrudan RevitWriteScope ile Transaction açılır.
    /// (Çapraz-thread çağrı gerekirse — örn. MCP server üzerinden — mevcut
    /// RevitDispatcher zaten bu işi görür, bu op'lara dokunmadan.)
    ///
    /// Bu op'lar kalıba özel değildir: input olarak HERHANGİ bir manifest
    /// adımının ürettiği element listesini veya element_id alanlı satır
    /// listesini (örn. kalip_all, intersect_report, collect_*) kabul eder.
    /// </summary>
    public static class ViewActionOps
    {
        // ── Ortak: input'tan ElementId listesi çöz ─────────────────────────
        // Kabul edilen girdi tipleri:
        //   List<Element>                              (collect_* çıktısı)
        //   List<Dictionary<string, object?>>          ("element_id" alanlı satırlar)
        private static List<ElementId> ResolveIds(OpContext ctx, Document doc)
        {
            var ids = new List<ElementId>();

            if (ctx.Input is List<Element> elems)
            {
                ids.AddRange(elems.Select(e => e.Id));
                return ids;
            }

            if (ctx.Input is List<Dictionary<string, object?>> rows)
            {
                foreach (var row in rows)
                {
                    if (!row.TryGetValue("element_id", out var v) || v is null) continue;
                    if (long.TryParse(v.ToString(), out var lid))
                    {
                        var eid = Rv.MakeElementId(lid);
                        if (doc.GetElement(eid) != null) ids.Add(eid);
                    }
                }
                return ids;
            }

            return ids;
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEW_ISOLATE_ELEMENTS
        // ════════════════════════════════════════════════════════════════════
        [EgOp("view_isolate_elements",
            Description = "Verilen elemanları aktif görünümde geçici olarak izole eder " +
                          "(IsolateElementsTemporary). 'from' ile bir önceki step'in çıktısını kullanır.",
            Category    = "Görünüm",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> ViewIsolateElements(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var ids = ResolveIds(ctx, rctx.Doc);
            if (ids.Count == 0)
            {
                ctx.Log("  ⚠ view_isolate_elements: izole edilecek eleman yok");
                return new() { ["isolated"] = 0 };
            }

            using var scope = new RevitWriteScope(rctx.Doc, "Eleman İzole Et", rctx.IsAtomicMode);
            rctx.UiDoc.ActiveView.IsolateElementsTemporary(ids);
            scope.Commit();

            ctx.Log($"  view_isolate_elements: {ids.Count} eleman izole edildi");
            return ctx.Input is List<Element> || ctx.Input is List<Dictionary<string, object?>>
                ? new() { ["isolated"] = ids.Count }
                : new() { ["isolated"] = ids.Count };
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEW_TEMP_HIDE_ELEMENTS
        // ════════════════════════════════════════════════════════════════════
        [EgOp("view_temp_hide_elements",
            Description = "Verilen elemanları aktif görünümde geçici olarak gizler " +
                          "(HideElementsTemporary). 'from' ile bir önceki step'in çıktısını kullanır.",
            Category    = "Görünüm",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> ViewTempHideElements(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var ids = ResolveIds(ctx, rctx.Doc);
            if (ids.Count == 0)
            {
                ctx.Log("  ⚠ view_temp_hide_elements: gizlenecek eleman yok");
                return new() { ["hidden"] = 0 };
            }

            using var scope = new RevitWriteScope(rctx.Doc, "Eleman Geçici Gizle", rctx.IsAtomicMode);
            rctx.UiDoc.ActiveView.HideElementsTemporary(ids);
            scope.Commit();

            ctx.Log($"  view_temp_hide_elements: {ids.Count} eleman gizlendi");
            return new() { ["hidden"] = ids.Count };
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEW_RESET_TEMPORARY_MODE
        // ════════════════════════════════════════════════════════════════════
        [EgOp("view_reset_temporary_mode",
            Description = "Aktif görünümdeki geçici izole/gizle modunu ve (varsa) section box'ı sıfırlar. " +
                          "params: reset_section_box (bool, default false)",
            Category    = "Görünüm",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> ViewResetTemporaryMode(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var view = rctx.UiDoc.ActiveView;
            bool resetBox = ctx.GetBool("reset_section_box", false);

            bool hasIsolate = view.IsTemporaryHideIsolateActive();
            bool is3d       = view is View3D;
            bool hasSecBox  = resetBox && is3d && ((View3D)view).IsSectionBoxActive;

            if (!hasIsolate && !hasSecBox)
            {
                ctx.Log("  view_reset_temporary_mode: zaten temiz, yapılacak iş yok");
                return new() { ["reset"] = false };
            }

            using var scope = new RevitWriteScope(rctx.Doc, "Görünüm Modunu Sıfırla", rctx.IsAtomicMode);
            if (hasIsolate)
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            if (hasSecBox)
                ((View3D)view).IsSectionBoxActive = false;
            scope.Commit();

            ctx.Log("  view_reset_temporary_mode: sıfırlandı");
            return new() { ["reset"] = true };
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEW_COLOR_OVERRIDE
        // ════════════════════════════════════════════════════════════════════
        [EgOp("view_color_override",
            Description = "Verilen elemanlara aktif görünümde renk override uygular (dolu yüzey + kenarlık). " +
                          "params: r,g,b (0-255, default kırmızı 255/0/0), reset (bool, default false — true ise override kaldırılır).",
            Category    = "Görünüm",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> ViewColorOverride(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var ids = ResolveIds(ctx, rctx.Doc);
            if (ids.Count == 0)
            {
                ctx.Log("  ⚠ view_color_override: hedef eleman yok");
                return new() { ["overridden"] = 0 };
            }

            bool reset = ctx.GetBool("reset", false);
            int r = ctx.GetInt("r", 255), g = ctx.GetInt("g", 0), b = ctx.GetInt("b", 0);

            var ogs = new OverrideGraphicSettings();
            if (!reset)
            {
                var color = new Color((byte)Math.Clamp(r, 0, 255),
                                       (byte)Math.Clamp(g, 0, 255),
                                       (byte)Math.Clamp(b, 0, 255));
                ogs.SetProjectionLineColor(color);
                try
                {
                    ogs.SetSurfaceForegroundPatternColor(color);
                    ogs.SetSurfaceForegroundPatternVisible(true);
                    var solid = new FilteredElementCollector(rctx.Doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);
                    if (solid != null) ogs.SetSurfaceForegroundPatternId(solid.Id);
                }
                catch { /* bazı Revit sürümlerinde surface pattern API farklı davranabilir */ }
            }

            using var scope = new RevitWriteScope(rctx.Doc, "Renk Override", rctx.IsAtomicMode);
            var view = rctx.UiDoc.ActiveView;
            foreach (var id in ids)
                view.SetElementOverrides(id, ogs);
            scope.Commit();

            ctx.Log($"  view_color_override: {ids.Count} eleman {(reset ? "sıfırlandı" : "renklendirildi")}");
            return new() { ["overridden"] = ids.Count };
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEW_CREATE_SELECTION_BOX
        // ════════════════════════════════════════════════════════════════════
        [EgOp("view_create_selection_box",
            Description = "Verilen elemanların bounding box birleşimini kapsayan bir 3B section box " +
                          "oluşturur ve aktif/uygun 3B görünüme uygular. params: padding_m (default 0.3)",
            Category    = "Görünüm",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> ViewCreateSelectionBox(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var ids = ResolveIds(ctx, rctx.Doc);
            if (ids.Count == 0)
            {
                ctx.Log("  ⚠ view_create_selection_box: hedef eleman yok");
                return new() { ["applied"] = false };
            }

            double padFt = ctx.GetDouble("padding_m", 0.3) / 0.3048;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            bool any = false;

            foreach (var id in ids)
            {
                var el = rctx.Doc.GetElement(id);
                var bb = el?.get_BoundingBox(null);
                if (bb == null) continue;
                any = true;
                minX = Math.Min(minX, bb.Min.X); minY = Math.Min(minY, bb.Min.Y); minZ = Math.Min(minZ, bb.Min.Z);
                maxX = Math.Max(maxX, bb.Max.X); maxY = Math.Max(maxY, bb.Max.Y); maxZ = Math.Max(maxZ, bb.Max.Z);
            }

            if (!any)
            {
                ctx.Log("  ⚠ view_create_selection_box: hiçbir elemanda bounding box hesaplanamadı");
                return new() { ["applied"] = false };
            }

            var box = new BoundingBoxXYZ
            {
                Min = new XYZ(minX - padFt, minY - padFt, minZ - padFt),
                Max = new XYZ(maxX + padFt, maxY + padFt, maxZ + padFt)
            };

            // Hedef 3B görünüm: aktif view 3B (ortografik) ise onu kullan,
            // değilse modeldeki ilk uygun 3B görünümü bul.
            View3D? view3d = rctx.UiDoc.ActiveView as View3D;
            if (view3d == null || view3d.IsTemplate || view3d.IsPerspective)
            {
                view3d = new FilteredElementCollector(rctx.Doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate && !v.IsPerspective);
            }

            if (view3d == null)
            {
                ctx.Log("  ⚠ view_create_selection_box: modelde uygun (ortografik) 3B görünüm bulunamadı");
                return new() { ["applied"] = false };
            }

            using var scope = new RevitWriteScope(rctx.Doc, "Seçim Kutusu Oluştur", rctx.IsAtomicMode);
            view3d.SetSectionBox(box);
            view3d.IsSectionBoxActive = true;
            scope.Commit();

            try
            {
                if (rctx.UiDoc.ActiveView.Id != view3d.Id)
                    rctx.UiDoc.RequestViewChange(view3d);
            }
            catch { /* RequestViewChange bazı bağlamlarda kullanılamayabilir */ }

            ctx.Log($"  view_create_selection_box: {ids.Count} elemanı kapsayan section box uygulandı");
            return new() { ["applied"] = true, ["view_id"] = Rv.GetId(view3d.Id) };
        }
    }
}
