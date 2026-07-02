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
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Addin.Host;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ClashOps  —  EGBIMOTO v10
    //
    //  Disiplinler arası çakışma (clash) tespiti ve önceliklendirme.
    //  OpeningCoordOps'taki BBox+Solid intersection altyapısını temel alır,
    //  ancak tek host-MEP yerine GENEL disiplin matrisi üretir.
    //
    //  Navisworks Clash Detective'in temel mantığını Revit içinde sunar:
    //    - İki kategori grubu (A vs B) arasında hard clash tespiti
    //    - Önem sıralaması (kesişim hacmine göre)
    //    - Disiplin çiftine göre gruplandırma
    //
    //  Op'lar:
    //    clash_detect_matrix  → A grubu vs B grubu hard clash listesi
    //    clash_severity_sort  → Bulguları kesişim hacmine göre önceliklendir
    //
    //  Manifest örneği:
    //    { "id": "cakisma", "op": "clash_detect_matrix",
    //      "params": {
    //        "group_a": "OST_DuctCurves,OST_PipeCurves",
    //        "group_b": "OST_StructuralFraming,OST_StructuralColumns",
    //        "tolerance_mm": 10
    //      }}
    // ═══════════════════════════════════════════════════════════════════════════

    public static class ClashOps
    {
        private const double FT_PER_MM = 1.0 / 304.8;

        // ─────────────────────────────────────────────────────────────────────
        //  OP 1: clash_detect_matrix
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("clash_detect_matrix",
            Description =
                "İki kategori grubu (A vs B) arasında hard clash (katı çakışma) tespiti.\n" +
                "BBox ön eleme + ElementIntersectsSolidFilter kesin testi ile çalışır.\n\n" +
                "params:\n" +
                "  group_a       — A disiplini kategorileri (virgülle, örn: OST_DuctCurves,OST_PipeCurves)\n" +
                "  group_b       — B disiplini kategorileri (virgülle, örn: OST_StructuralFraming)\n" +
                "  tolerance_mm  — BBox genişletme payı mm (opsiyonel, default: 10)\n" +
                "  max_results   — maksimum bulgu sayısı (opsiyonel, default: 1000)\n\n" +
                "Çıktı: List<Dictionary> — her satır bir çakışma:\n" +
                "  a_id, a_category, a_name, b_id, b_category, b_name,\n" +
                "  clash_x, clash_y, clash_z, overlap_volume_m3, disiplin_cifti, seviye",
            Category = "Koordinasyon")]
        public static List<Dictionary<string, object?>> ClashDetectMatrix(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[clash_detect_matrix] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            var groupA = ParseCategories(ctx.GetString("group_a", ""));
            var groupB = ParseCategories(ctx.GetString("group_b", ""));
            var tolFt  = ctx.GetInt("tolerance_mm", 10) * FT_PER_MM;
            var maxRes = ctx.GetInt("max_results", 1000);

            if (groupA.Count == 0 || groupB.Count == 0)
                throw new InvalidOperationException(
                    "[clash_detect_matrix] group_a ve group_b kategori listeleri zorunludur.");

            var elemsA = CollectByCategories(doc, groupA);
            var elemsB = CollectByCategories(doc, groupB);

            ctx.Log($"[ClashOps] A grubu: {elemsA.Count} eleman, B grubu: {elemsB.Count} eleman");

            var findings = new List<Dictionary<string, object?>>();

            foreach (var a in elemsA)
            {
                if (findings.Count >= maxRes) break;

                var aBb = a.get_BoundingBox(null);
                if (aBb == null) continue;

                var aMin = new XYZ(aBb.Min.X - tolFt, aBb.Min.Y - tolFt, aBb.Min.Z - tolFt);
                var aMax = new XYZ(aBb.Max.X + tolFt, aBb.Max.Y + tolFt, aBb.Max.Z + tolFt);

                // BBox kesişen B adayları için solid filter
                ElementIntersectsElementFilter? solidFilter = null;
                try { solidFilter = new ElementIntersectsElementFilter(a); }
                catch { solidFilter = null; }

                foreach (var b in elemsB)
                {
                    if (findings.Count >= maxRes) break;
                    if (b.Id == a.Id) continue;

                    var bBb = b.get_BoundingBox(null);
                    if (bBb == null) continue;

                    // 1. Hızlı BBox ön eleme
                    if (!BbOverlap(aMin, aMax, bBb.Min, bBb.Max)) continue;

                    // 2. Kesin Solid testi (mümkünse)
                    bool hardClash = true;
                    if (solidFilter != null)
                    {
                        try { hardClash = solidFilter.PassesFilter(b); }
                        catch { hardClash = true; } // Solid alınamazsa BBox sonucuna güven
                    }
                    if (!hardClash) continue;

                    // Kesişim merkezi (BBox ortalaması)
                    var cx = (Math.Max(aMin.X, bBb.Min.X) + Math.Min(aMax.X, bBb.Max.X)) / 2.0;
                    var cy = (Math.Max(aMin.Y, bBb.Min.Y) + Math.Min(aMax.Y, bBb.Max.Y)) / 2.0;
                    var cz = (Math.Max(aMin.Z, bBb.Min.Z) + Math.Min(aMax.Z, bBb.Max.Z)) / 2.0;

                    var overlapVol = OverlapVolumeM3(aMin, aMax, bBb.Min, bBb.Max);

                    var aCat = a.Category?.Name ?? "?";
                    var bCat = b.Category?.Name ?? "?";

                    findings.Add(new Dictionary<string, object?>
                    {
                        ["a_id"]              = Rv.GetId(a.Id),
                        ["a_category"]        = aCat,
                        ["a_name"]            = a.Name,
                        ["b_id"]              = Rv.GetId(b.Id),
                        ["b_category"]        = bCat,
                        ["b_name"]            = b.Name,
                        ["clash_x"]           = Math.Round(cx * 304.8, 1),
                        ["clash_y"]           = Math.Round(cy * 304.8, 1),
                        ["clash_z"]           = Math.Round(cz * 304.8, 1),
                        ["overlap_volume_m3"] = Math.Round(overlapVol, 4),
                        ["disiplin_cifti"]    = $"{aCat} × {bCat}",
                        ["seviye"]            = ClassifySeverity(overlapVol),
                    });
                }
            }

            ctx.Log($"[ClashOps] Toplam {findings.Count} hard clash tespit edildi.");
            return findings;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OP 2: clash_severity_sort
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("clash_severity_sort",
            Description =
                "clash_detect_matrix bulgularını kesişim hacmine göre önceliklendirir.\n" +
                "En büyük hacimli çakışmalar (en kritik) en üstte sıralanır.\n\n" +
                "Input: List<Dictionary> (clash_detect_matrix çıktısı)\n" +
                "Çıktı: aynı liste, overlap_volume_m3 azalan sıralı + sira_no eklenmiş",
            Category = "Koordinasyon")]
        public static List<Dictionary<string, object?>> ClashSeveritySort(OpContext ctx)
        {
            var input = ctx.Input as List<Dictionary<string, object?>>
                ?? new List<Dictionary<string, object?>>();

            var sorted = input
                .OrderByDescending(r =>
                    r.TryGetValue("overlap_volume_m3", out var v) && v != null
                        ? Convert.ToDouble(v)
                        : 0.0)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
                sorted[i]["sira_no"] = i + 1;

            ctx.Log($"[ClashOps] {sorted.Count} bulgu önceliğe göre sıralandı.");
            return sorted;
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private static List<BuiltInCategory> ParseCategories(string csv)
        {
            var result = new List<BuiltInCategory>();
            foreach (var part in csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse<BuiltInCategory>(part, out var bic))
                    result.Add(bic);
            }
            return result;
        }

        private static List<Element> CollectByCategories(Document doc, List<BuiltInCategory> cats)
        {
            var result = new List<Element>();
            foreach (var cat in cats)
            {
                try
                {
                    var elems = new FilteredElementCollector(doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToElements();
                    result.AddRange(elems);
                }
                catch { /* geçersiz kategori atla */ }
            }
            return result;
        }

        private static bool BbOverlap(XYZ aMin, XYZ aMax, XYZ bMin, XYZ bMax)
            => aMin.X <= bMax.X && aMax.X >= bMin.X &&
               aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
               aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;

        private static double OverlapVolumeM3(XYZ aMin, XYZ aMax, XYZ bMin, XYZ bMax)
        {
            var dx = Math.Max(0, Math.Min(aMax.X, bMax.X) - Math.Max(aMin.X, bMin.X));
            var dy = Math.Max(0, Math.Min(aMax.Y, bMax.Y) - Math.Max(aMin.Y, bMin.Y));
            var dz = Math.Max(0, Math.Min(aMax.Z, bMax.Z) - Math.Max(aMin.Z, bMin.Z));
            var ft3 = dx * dy * dz;
            return ft3 * 0.0283168; // ft³ → m³
        }

        private static string ClassifySeverity(double volM3)
        {
            if (volM3 >= 0.05) return "KRİTİK";
            if (volM3 >= 0.01) return "YÜKSEK";
            if (volM3 >= 0.001) return "ORTA";
            return "DÜŞÜK";
        }
    }
}
