using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// v12 — Kategori-agnostik kesişim/host raporu + rapor satırından eleman seçme.
    ///
    /// PCH_BWIC_reporter (Kamila Milewska) eklentisindeki "linkify" + host-bulma
    /// desenin EGBIMOTO'ya genelleştirilmiş portu. KalipOps.IntersectingElements
    /// ile aynı 2 katmanlı prefilter (gerçek geometri → bbox yedek) kullanılır,
    /// ancak kalıba özel değildir: herhangi bir kaynak/hedef kategori kombinasyonu
    /// için (MEP-yapısal BWIC, donatı-açıklık, tesisat çakışması, vb.) çalışır.
    ///
    /// kalip_all'dan FARKI: host bulunamayan elemanlar sessizce atılmaz,
    /// "no_host" = true olarak satıra eklenir (include_no_host=true varsayılan) —
    /// böylece hata ayıklama sırasında "neden az satır geldi" sorusu raporun
    /// kendisinden cevaplanabilir.
    /// </summary>
    public static class IntersectReportOps
    {
        // ════════════════════════════════════════════════════════════════════
        // INTERSECT_REPORT
        // ════════════════════════════════════════════════════════════════════
        [EgOp("intersect_report",
            Description = "Kaynak kategori(ler)deki her eleman için hedef kategori(ler)de " +
                          "kesişen 'host' elemanları bulur ve satır listesi üretir. " +
                          "params: source_categories (string[]), target_categories (string[]), " +
                          "include_no_host (bool, default true). " +
                          "'from' ile bir önceki step'in eleman listesi kaynak olarak da kullanılabilir.",
            Category    = "Toplama")]
        public static List<Dictionary<string, object?>> IntersectReport(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            bool includeNoHost = ctx.GetBool("include_no_host", true);

            // Kaynak elemanlar: önce input (from), yoksa source_categories'ten topla
            List<Element> sourceElems = ctx.InputAsOrDefault<List<Element>>() ?? new();
            if (sourceElems.Count == 0)
            {
                var srcCats = ctx.GetStringList("source_categories");
                sourceElems = CollectByCategories(rctx.Doc, srcCats);
            }

            var targetCats = ctx.GetStringList("target_categories");
            var targetElems = CollectByCategories(rctx.Doc, targetCats);

            if (sourceElems.Count == 0)
            {
                ctx.Log("  ⚠ intersect_report: kaynak eleman bulunamadı");
                return new();
            }
            if (targetElems.Count == 0)
            {
                ctx.Log("  ⚠ intersect_report: hedef eleman bulunamadı (target_categories kontrol edin)");
            }

            var result = new List<Dictionary<string, object?>>();
            int withHost = 0, noHost = 0;

            foreach (var src in sourceElems)
            {
                List<Element> hits;
                try { hits = IntersectingElements(rctx.Doc, src, targetElems); }
                catch (Exception ex)
                {
                    ctx.Log($"  ⚠ intersect_report id={Rv.GetId(src.Id)}: {ex.Message}");
                    hits = new();
                }

                if (hits.Count > 0)
                {
                    foreach (var host in hits)
                    {
                        result.Add(BuildRow(rctx.Doc, src, host, noHostFlag: false));
                        withHost++;
                    }
                }
                else if (includeNoHost)
                {
                    result.Add(BuildRow(rctx.Doc, src, null, noHostFlag: true));
                    noHost++;
                }
            }

            ctx.Log($"  intersect_report: {sourceElems.Count} kaynak, {withHost} eşleşme, {noHost} host bulunamadı");
            return result;
        }

        private static Dictionary<string, object?> BuildRow(
            Document doc, Element src, Element? host, bool noHostFlag)
        {
            string srcType = "", hostType = "", hostCat = "";
            try
            {
                var t = doc.GetElement(src.GetTypeId()) as ElementType;
                if (t != null) srcType = $"{t.FamilyName} : {t.Name}";
            }
            catch { }

            if (host != null)
            {
                try
                {
                    var ht = doc.GetElement(host.GetTypeId()) as ElementType;
                    if (ht != null) hostType = $"{ht.FamilyName} : {ht.Name}";
                }
                catch { }
                hostCat = host.Category?.Name ?? "";
            }

            return new Dictionary<string, object?>
            {
                ["element_id"]    = Rv.GetId(src.Id),
                ["kategori"]      = src.Category?.Name ?? "",
                ["tip"]           = srcType,
                ["host_id"]       = host != null ? Rv.GetId(host.Id) : (object?)null,
                ["host_kategori"] = noHostFlag ? "Host bulunamadı" : hostCat,
                ["host_tip"]      = noHostFlag ? "—" : hostType,
                ["no_host"]       = noHostFlag,
            };
        }

        // ── Kesişim prefilter — KalipOps.IntersectingElements ile aynı desen ──
        // (Bağımsız tutuldu: KalipOps'a sıkı bağımlılık istenmedi, bu op tek
        //  başına da kullanılabilir olmalı.)
        private static List<Element> IntersectingElements(
            Document doc, Element host, List<Element> candidates)
        {
            if (candidates.Count == 0) return new();

            var candSet = new HashSet<long>(candidates.Select(c => Rv.GetId(c.Id)));
            var hitIds  = new HashSet<long>();

            try
            {
                var geoFilter = new ElementIntersectsElementFilter(host);
                foreach (var e in new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(geoFilter))
                {
                    long id = Rv.GetId(e.Id);
                    if (candSet.Contains(id)) hitIds.Add(id);
                }
            }
            catch { }

            if (hitIds.Count == 0)
            {
                try
                {
                    var hbb = host.get_BoundingBox(null);
                    if (hbb != null)
                    {
                        const double pad = 0.05;
                        var outline = new Outline(
                            new XYZ(hbb.Min.X - pad, hbb.Min.Y - pad, hbb.Min.Z - pad),
                            new XYZ(hbb.Max.X + pad, hbb.Max.Y + pad, hbb.Max.Z + pad));
                        var bbFilter = new BoundingBoxIntersectsFilter(outline);
                        foreach (var e in new FilteredElementCollector(doc)
                            .WhereElementIsNotElementType()
                            .WherePasses(bbFilter))
                        {
                            long id = Rv.GetId(e.Id);
                            if (candSet.Contains(id)) hitIds.Add(id);
                        }
                    }
                }
                catch { }
            }

            return candidates.Where(c => hitIds.Contains(Rv.GetId(c.Id))).ToList();
        }

        private static List<Element> CollectByCategories(Document doc, List<string> catNames)
        {
            var result = new List<Element>();
            foreach (var name in catNames)
            {
                if (!EgCategoryResolver.TryResolve(name, out var bic))
                {
                    continue;
                }
                try
                {
                    result.AddRange(new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements());
                }
                catch { }
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        // SELECT_BY_ID — rapor satırından eleman seç + zoom ("linkify" karşılığı)
        // ════════════════════════════════════════════════════════════════════
        [EgOp("select_by_id",
            Description = "Verilen element id'ler(i) Revit'te seçer ve görünüme zoom yapar. " +
                          "params: element_id (tek id) veya ids (liste, virgülle ayrılmış string ya da array).",
            Category    = "UI")]
        public static Dictionary<string, object?> SelectById(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

            var ids = new List<long>();

            var single = ctx.GetString("element_id", "");
            if (!string.IsNullOrWhiteSpace(single) && long.TryParse(single, out var sId))
                ids.Add(sId);

            var listParam = ctx.GetStringList("ids");
            foreach (var s in listParam)
                if (long.TryParse(s.Trim(), out var lId)) ids.Add(lId);

            if (ids.Count == 0)
            {
                ctx.Log("  ⚠ select_by_id: geçerli element_id/ids verilmedi");
                return new() { ["selected"] = 0 };
            }

            var eids = new List<ElementId>();
            foreach (var id in ids)
            {
                var eid = Rv.MakeElementId(id);
                if (rctx.Doc.GetElement(eid) != null) eids.Add(eid);
            }

            if (eids.Count == 0)
            {
                ctx.Log("  ⚠ select_by_id: verilen id'lerden hiçbiri dokümanda bulunamadı");
                return new() { ["selected"] = 0 };
            }

            var net = new System.Collections.Generic.List<ElementId>(eids);
            rctx.UiDoc.Selection.SetElementIds(net);
            try { rctx.UiDoc.ShowElements(net); } catch { /* aktif view 3B olmayabilir vb. */ }

            ctx.Log($"  select_by_id: {eids.Count} eleman seçildi");
            return new() { ["selected"] = eids.Count };
        }
    }
}
