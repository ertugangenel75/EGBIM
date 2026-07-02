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
//
// ─────────────────────────────────────────────────────────────────────────────
// NOTICE: Bu dosyadaki dikdörtgen kanal eş-kesit dönüşümü fikri, açık kaynak
// MEP araç kataloğundan (460707300-tech/Arbitrary3D, MIT — "风管截面转换")
// ilham alınarak EGBIMOTO mimarisine ÖZGÜN olarak yazılmıştır. Kod kopyalanmamış;
// ASHRAE eşdeğer-çap (equivalent diameter) formülleri temel alınarak sıfırdan
// geliştirilmiştir.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Addin.Host;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  DuctSectionOps  —  EGBIMOTO v10
    //
    //  Dikdörtgen havalandırma kanallarının EŞ-KESİT dönüşümü.
    //  Bir kanalın bir boyutu (genişlik veya yükseklik) bir kısıt nedeniyle
    //  sabitlendiğinde (örn. tavan yüksekliği, kiriş altı geçiş), aynı
    //  AERODİNAMİK karakteristiği korumak için diğer boyut yeniden hesaplanır.
    //
    //  Korunan büyüklük: ASHRAE eşdeğer çapı (De).
    //    De = 1.30 * (a*b)^0.625 / (a+b)^0.250        [a, b: kanal kenarları]
    //  Aynı De → aynı sürtünme kaybı ve hız karakteristiği (yaklaşık).
    //
    //  Op'lar:
    //    duct_section_convert_preview  → Yeni boyutları HESAPLA (yazma yok, RAPOR)
    //    duct_section_convert_apply    → Hesaplanan boyutları modele YAZ
    //
    //  Manifest örneği:
    //    { "id": "hesap", "op": "duct_section_convert_preview",
    //      "params": { "fix_dimension": "height", "fixed_value_mm": 300,
    //                  "round_to_mm": 50, "max_aspect_ratio": 4.0 } }
    //    { "id": "uygula", "op": "duct_section_convert_apply", "from": "hesap" }
    // ═══════════════════════════════════════════════════════════════════════════

    public static class DuctSectionOps
    {
        private const double FT_PER_MM = 1.0 / 304.8;
        private const double MM_PER_FT = 304.8;

        // ─────────────────────────────────────────────────────────────────────
        //  OP 1: duct_section_convert_preview   (RAPOR — yazma yok)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("duct_section_convert_preview",
            Description =
                "Dikdörtgen kanalları eş-kesit (eşdeğer çap korumalı) yeniden boyutlandırır — HESAP.\n" +
                "Bir boyut sabitlenir, diğeri aynı ASHRAE eşdeğer çapını koruyacak şekilde hesaplanır.\n" +
                "Yazma yapmaz. Uygulamak için duct_section_convert_apply kullanın.\n\n" +
                "Input  : List<Element> (Duct) opsiyonel — verilmezse tüm dikdörtgen kanallar taranır.\n" +
                "params :\n" +
                "  fix_dimension     — sabitlenecek boyut: 'width' | 'height' (default: height)\n" +
                "  fixed_value_mm    — sabit boyutun yeni değeri mm (zorunlu, >0)\n" +
                "  round_to_mm       — hesaplanan boyutu yuvarlama adımı mm (default: 50)\n" +
                "  max_aspect_ratio  — izin verilen max en/boy oranı; aşılırsa uyarı (default: 4.0)\n" +
                "  only_round_ducts  — 'false' (default). true ise yuvarlak kanallar da dikdörtgene çevrilir.\n\n" +
                "Çıktı: List<Dictionary> — her satır bir kanal:\n" +
                "  duct_id, system_name, old_w_mm, old_h_mm, old_de_mm,\n" +
                "  new_w_mm, new_h_mm, new_de_mm, aspect_ratio, de_error_pct, warning",
            Category = "MEP-Mekanik")]
        public static List<Dictionary<string, object?>> DuctSectionConvertPreview(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[duct_section_convert_preview] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            string fixDim     = ctx.GetString("fix_dimension", "height").ToLowerInvariant();
            double fixedMm    = ctx.GetDouble("fixed_value_mm", 0);
            double roundMm    = ctx.GetDouble("round_to_mm", 50);
            double maxAspect  = ctx.GetDouble("max_aspect_ratio", 4.0);

            if (fixedMm <= 0)
                throw new InvalidOperationException(
                    "[duct_section_convert_preview] fixed_value_mm > 0 zorunludur.");
            if (fixDim != "width" && fixDim != "height")
                throw new InvalidOperationException(
                    "[duct_section_convert_preview] fix_dimension 'width' veya 'height' olmalı.");
            if (roundMm <= 0) roundMm = 50;

            var inputElems = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var ducts = inputElems.Count > 0
                ? inputElems.OfType<Duct>().ToList()
                : new FilteredElementCollector(doc)
                    .OfClass(typeof(Duct))
                    .Cast<Duct>()
                    .ToList();

            ctx.Log($"[DuctSection] {ducts.Count} kanal incelenecek (sabit: {fixDim}={fixedMm}mm).");

            double fixedFt = fixedMm * FT_PER_MM;
            var rows = new List<Dictionary<string, object?>>();

            foreach (var duct in ducts)
            {
                // Mevcut dikdörtgen boyutları (yoksa atla)
                double oldWFt = GetDuctDim(duct, "Width");
                double oldHFt = GetDuctDim(duct, "Height");
                if (oldWFt <= 0 || oldHFt <= 0) continue; // yuvarlak/oval kanal — atla

                double oldDeFt = EquivalentDiameterFt(oldWFt, oldHFt);

                // Sabit boyut hangisiyse, diğerini eşdeğer çapı koruyacak şekilde çöz
                double newWFt, newHFt;
                if (fixDim == "height")
                {
                    newHFt = fixedFt;
                    newWFt = SolveSideForEquivDiameter(oldDeFt, fixedFt);
                }
                else
                {
                    newWFt = fixedFt;
                    newHFt = SolveSideForEquivDiameter(oldDeFt, fixedFt);
                }

                // Yuvarlama (mm cinsinden)
                newWFt = RoundFt(newWFt, roundMm);
                newHFt = RoundFt(newHFt, roundMm);

                double newDeFt = EquivalentDiameterFt(newWFt, newHFt);
                double dePct   = oldDeFt > 0 ? (newDeFt - oldDeFt) / oldDeFt * 100.0 : 0;

                double aspect = Math.Max(newWFt, newHFt) / Math.Max(Math.Min(newWFt, newHFt), 1e-9);

                string warning = "";
                if (aspect > maxAspect)
                    warning = $"En/boy oranı {aspect:F1} > {maxAspect:F1} (basınç kaybı artar)";
                else if (Math.Abs(dePct) > 5.0)
                    warning = $"Eşdeğer çap sapması %{dePct:F1} (yuvarlama nedeniyle)";

                rows.Add(new Dictionary<string, object?>
                {
                    ["duct_id"]      = Rv.GetId(duct.Id),
                    ["system_name"]  = GetSystemName(duct),
                    ["old_w_mm"]     = Math.Round(oldWFt * MM_PER_FT, 0),
                    ["old_h_mm"]     = Math.Round(oldHFt * MM_PER_FT, 0),
                    ["old_de_mm"]    = Math.Round(oldDeFt * MM_PER_FT, 0),
                    ["new_w_mm"]     = Math.Round(newWFt * MM_PER_FT, 0),
                    ["new_h_mm"]     = Math.Round(newHFt * MM_PER_FT, 0),
                    ["new_de_mm"]    = Math.Round(newDeFt * MM_PER_FT, 0),
                    ["aspect_ratio"] = Math.Round(aspect, 2),
                    ["de_error_pct"] = Math.Round(dePct, 1),
                    ["warning"]      = warning,
                });
            }

            ctx.Log($"[DuctSection] {rows.Count} dikdörtgen kanal için yeni kesit hesaplandı.");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OP 2: duct_section_convert_apply   (YAZMA)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("duct_section_convert_apply",
            Description =
                "duct_section_convert_preview çıktısındaki yeni kanal boyutlarını modele yazar.\n" +
                "DİKKAT: Model değişikliği yapar. Bağlı kanal parçalarının (fitting) yeniden\n" +
                "boyutlanması Revit tarafından otomatik denenir; bazı fitting'ler manuel düzeltme isteyebilir.\n\n" +
                "Input  : duct_section_convert_preview çıktısı (List<Dictionary>) — from ile bağlanır.\n" +
                "params :\n" +
                "  skip_warnings  — 'true' ise warning dolu satırları atla (default: false)\n\n" +
                "Çıktı: List<Dictionary> — duct_id, status (ok|skip|error), message, new_w_mm, new_h_mm",
            Category = "MEP-Mekanik",
            RequiresTransaction = true)]
        public static List<Dictionary<string, object?>> DuctSectionConvertApply(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[duct_section_convert_apply] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            var rows = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>(
                new List<Dictionary<string, object?>>());
            bool skipWarnings = ctx.GetString("skip_warnings", "false")
                                   .Equals("true", StringComparison.OrdinalIgnoreCase);

            var results = new List<Dictionary<string, object?>>();
            int applied = 0;

            using var scope = new RevitWriteScope(doc, "Kanal Kesit Dönüşümü", rctx.IsAtomicMode);

            foreach (var row in rows)
            {
                long id = ToLong(row.GetValueOrDefault("duct_id"));
                double newWmm = ToDouble(row.GetValueOrDefault("new_w_mm"));
                double newHmm = ToDouble(row.GetValueOrDefault("new_h_mm"));
                string warning = row.GetValueOrDefault("warning") as string ?? "";

                var res = new Dictionary<string, object?>
                {
                    ["duct_id"]  = id,
                    ["status"]   = "skip",
                    ["message"]  = "",
                    ["new_w_mm"] = newWmm,
                    ["new_h_mm"] = newHmm,
                };

                if (skipWarnings && !string.IsNullOrEmpty(warning))
                {
                    res["message"] = $"Uyarı nedeniyle atlandı: {warning}";
                    results.Add(res);
                    continue;
                }

                try
                {
                    var el = doc.GetElement(Rv.MakeElementId(id));
                    if (el is not Duct duct)
                    {
                        res["message"] = "Kanal bulunamadı.";
                        results.Add(res);
                        continue;
                    }

                    bool wOk = SetDuctDim(duct, "Width",  newWmm * FT_PER_MM);
                    bool hOk = SetDuctDim(duct, "Height", newHmm * FT_PER_MM);

                    if (wOk && hOk)
                    {
                        res["status"]  = "ok";
                        res["message"] = string.IsNullOrEmpty(warning) ? "Uygulandı." : $"Uygulandı ({warning})";
                        applied++;
                    }
                    else
                    {
                        res["message"] = "Boyut parametresi salt-okunur veya yazılamadı.";
                    }
                }
                catch (Exception ex)
                {
                    res["status"]  = "error";
                    res["message"] = ex.Message;
                }

                results.Add(res);
            }

            scope.Commit();
            ctx.Log($"[DuctSection] {applied} kanal yeniden boyutlandırıldı.");
            return results;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  EŞDEĞER ÇAP MATEMATİĞİ  (ASHRAE)
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ASHRAE dikdörtgen→yuvarlak eşdeğer çap (aynı sürtünme + debi):
        ///   De = 1.30 * (a*b)^0.625 / (a+b)^0.250
        /// a, b ve dönen De aynı birimde (ft).
        /// </summary>
        private static double EquivalentDiameterFt(double a, double b)
        {
            if (a <= 0 || b <= 0) return 0;
            return 1.30 * Math.Pow(a * b, 0.625) / Math.Pow(a + b, 0.250);
        }

        /// <summary>
        /// Verilen eşdeğer çap (De) ve sabit kenar (fixed) için diğer kenarı bulur.
        /// De = 1.30 * (x*fixed)^0.625 / (x+fixed)^0.250 denklemini x için
        /// sayısal olarak (bisection) çözer. Kapalı form yok — monoton artan
        /// fonksiyon olduğu için bisection güvenli ve hızlı yakınsar.
        /// </summary>
        private static double SolveSideForEquivDiameter(double targetDe, double fixedSide)
        {
            if (targetDe <= 0 || fixedSide <= 0) return fixedSide;

            // De, x'e göre monoton artar. Geniş bir aralıkta bisection.
            double lo = fixedSide * 0.05;   // çok dar
            double hi = fixedSide * 50.0;   // çok geniş üst sınır
            double fLo = EquivalentDiameterFt(lo, fixedSide) - targetDe;
            double fHi = EquivalentDiameterFt(hi, fixedSide) - targetDe;

            // Hedef aralık dışındaysa en yakın sınırı döndür
            if (fLo > 0) return lo;
            if (fHi < 0) return hi;

            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = EquivalentDiameterFt(mid, fixedSide) - targetDe;
                if (Math.Abs(fMid) < 1e-6) return mid;
                if (fMid < 0) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        private static double RoundFt(double valFt, double stepMm)
        {
            double valMm = valFt * MM_PER_FT;
            double rounded = Math.Round(valMm / stepMm) * stepMm;
            if (rounded < stepMm) rounded = stepMm; // sıfıra düşmesin
            return rounded * FT_PER_MM;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  KANAL PARAMETRE ERİŞİMİ
        // ═══════════════════════════════════════════════════════════════════════

        // Width / Height built-in parametreleri (yuvarlak kanalda bulunmaz → 0 döner)
        private static double GetDuctDim(Duct duct, string which)
        {
            BuiltInParameter bip = which == "Width"
                ? BuiltInParameter.RBS_CURVE_WIDTH_PARAM
                : BuiltInParameter.RBS_CURVE_HEIGHT_PARAM;
            try
            {
                var p = duct.get_Parameter(bip);
                if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                    return p.AsDouble(); // feet
            }
            catch { }
            // Yedek: isimle ara
            try
            {
                var p = duct.LookupParameter(which);
                if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                    return p.AsDouble();
            }
            catch { }
            return 0;
        }

        private static bool SetDuctDim(Duct duct, string which, double valFt)
        {
            BuiltInParameter bip = which == "Width"
                ? BuiltInParameter.RBS_CURVE_WIDTH_PARAM
                : BuiltInParameter.RBS_CURVE_HEIGHT_PARAM;
            try
            {
                var p = duct.get_Parameter(bip);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                {
                    p.Set(valFt);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string GetSystemName(Element duct)
        {
            try
            {
                var p = duct.LookupParameter("System Name")
                        ?? duct.LookupParameter("Sistem Adı");
                if (p != null && p.HasValue) return p.AsString() ?? "?";
            }
            catch { }
            return "?";
        }

        // ── Tip dönüşüm yardımcıları ─────────────────────────────────────────

        private static long ToLong(object? o)
        {
            if (o == null) return 0;
            if (o is long l) return l;
            if (o is int i)  return i;
            if (o is double d) return (long)d;
            return long.TryParse(o.ToString(), out var v) ? v : 0;
        }

        private static double ToDouble(object? o)
        {
            if (o == null) return 0;
            if (o is double d) return d;
            if (o is int i)    return i;
            if (o is long l)   return l;
            return double.TryParse(o.ToString(), out var v) ? v : 0;
        }
    }
}
