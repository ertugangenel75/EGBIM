using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Maliyet, kalıp ve POZ hesaplama op'ları.
    /// </summary>
    public static partial class CostOps
    {
        private const double Ft2ToM2 = 0.3048 * 0.3048;
        private const double Ft3ToM3 = 0.3048 * 0.3048 * 0.3048;

        // ── POZ Eşleştirme ────────────────────────────────────────────────────
        [EgOp("poz_match",
            Description = "Satır listesindeki kategori/tip alanlarını POZ veritabanıyla eşleştirir",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> PozMatch(OpContext ctx)
        {
            var rows    = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var catField = ctx.GetString("category_field", "kategori");
            var tipField = ctx.GetString("type_field", "tip");
            var pozData  = EgbimotoData.Registry.Get("poz_data")
                as List<Dictionary<string, object?>> ?? new();
            int matched = 0;
            foreach (var row in rows)
            {
                var cat = row.TryGetValue(catField, out var cv) ? cv?.ToString() ?? "" : "";
                var tip = row.TryGetValue(tipField, out var tv) ? tv?.ToString() ?? "" : "";
                var poz = pozData.FirstOrDefault(p =>
                {
                    var pk = p.TryGetValue("kategori", out var pkv) ? pkv?.ToString() ?? "" : "";
                    var pt = p.TryGetValue("tip", out var ptv) ? ptv?.ToString() ?? "" : "";
                    return pk.Equals(cat, StringComparison.OrdinalIgnoreCase) ||
                           tip.Contains(pt, StringComparison.OrdinalIgnoreCase);
                });
                if (poz is not null)
                {
                    row["poz_no"]   = poz.TryGetValue("poz_no",   out var pn) ? pn : null;
                    row["poz_adi"]  = poz.TryGetValue("poz_adi",  out var pa) ? pa : null;
                    row["birim"]    = poz.TryGetValue("birim",    out var pb) ? pb : null;
                    row["birim_fiyat"] = poz.TryGetValue("birim_fiyat", out var pf) ? pf : null;
                    matched++;
                }
                else
                {
                    row["poz_no"]      = "—";
                    row["poz_adi"]     = "Eşleşme yok";
                    row["birim"]       = "—";
                    row["birim_fiyat"] = 0;
                }
            }
            ctx.Log($"  poz_match: {matched}/{rows.Count} eşleşti");
            return rows;
        }

        [EgOp("poz_match_by_code",
            Description = "Satır listesindeki params.poz_code_field alanını POZ koduna göre eşleştirir",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> PozMatchByCode(OpContext ctx)
        {
            var rows      = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var codeField = ctx.GetString("poz_code_field", "poz_no");
            var pozData   = EgbimotoData.Registry.Get("poz_data")
                as List<Dictionary<string, object?>> ?? new();
            var pozIndex  = pozData.ToDictionary(
                p => p.TryGetValue("poz_no", out var k) ? k?.ToString() ?? "" : "",
                p => p, StringComparer.OrdinalIgnoreCase);
            int matched = 0;
            foreach (var row in rows)
            {
                var code = row.TryGetValue(codeField, out var v) ? v?.ToString() ?? "" : "";
                if (pozIndex.TryGetValue(code, out var poz))
                {
                    row["poz_adi"]     = poz.TryGetValue("poz_adi",     out var pa) ? pa : null;
                    row["birim"]       = poz.TryGetValue("birim",       out var pb) ? pb : null;
                    row["birim_fiyat"] = poz.TryGetValue("birim_fiyat", out var pf) ? pf : null;
                    matched++;
                }
            }
            ctx.Log($"  poz_match_by_code: {matched}/{rows.Count} eşleşti");
            return rows;
        }

        // ── Maliyet Hesaplama ─────────────────────────────────────────────────
        [EgOp("calc_cost",
            Description = "Satır listesindeki miktar x birim_fiyat = toplam_maliyet hesaplar. params: quantity_field",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> CalcCost(OpContext ctx)
        {
            var rows     = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var qtyField = ctx.GetString("quantity_field", "hacim_m3");
            double total = 0;
            foreach (var row in rows)
            {
                double qty   = row.TryGetValue(qtyField, out var qv) &&
                               double.TryParse(qv?.ToString(), out var q) ? q : 0;
                double price = row.TryGetValue("birim_fiyat", out var pv) &&
                               double.TryParse(pv?.ToString(), out var p) ? p : 0;
                double cost  = Math.Round(qty * price, 2);
                row["miktar"]          = qty;
                row["toplam_maliyet"]  = cost;
                total += cost;
            }
            ctx.Log($"  calc_cost: toplam {total:N2} TL");
            return rows;
        }

        [EgOp("cost_summary",
            Description = "Maliyet satırlarını WBS/kategori bazında özetler",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> CostSummary(OpContext ctx)
        {
            var rows     = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var groupBy  = ctx.GetString("group_by", "kategori");
            var result   = rows
                .GroupBy(r => r.TryGetValue(groupBy, out var v) ? v?.ToString() ?? "—" : "—")
                .Select(g =>
                {
                    double totalCost = g.Sum(r =>
                        r.TryGetValue("toplam_maliyet", out var v) &&
                        double.TryParse(v?.ToString(), out var d) ? d : 0);
                    double totalQty  = g.Sum(r =>
                        r.TryGetValue("miktar", out var v) &&
                        double.TryParse(v?.ToString(), out var d) ? d : 0);
                    return new Dictionary<string, object?>
                    {
                        [groupBy]          = g.Key,
                        ["eleman_sayisi"]  = g.Count(),
                        ["toplam_miktar"]  = Math.Round(totalQty, 3),
                        ["toplam_maliyet"] = Math.Round(totalCost, 2)
                    };
                })
                .OrderByDescending(r => (double)r["toplam_maliyet"]!)
                .ToList();
            double grandTotal = result.Sum(r =>
                double.TryParse(r["toplam_maliyet"]?.ToString(), out var d) ? d : 0);
            ctx.Log($"  cost_summary: {result.Count} grup, toplam {grandTotal:N2} TL");
            return result;
        }

        [EgOp("cost_by_level",
            Description = "Maliyet satırlarını kat bazında özetler",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> CostByLevel(OpContext ctx)
        {
            var rows   = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var result = rows
                .GroupBy(r => r.TryGetValue("kat", out var v) ? v?.ToString() ?? "—" : "—")
                .Select(g =>
                {
                    double totalCost = g.Sum(r =>
                        r.TryGetValue("toplam_maliyet", out var v) &&
                        double.TryParse(v?.ToString(), out var d) ? d : 0);
                    return new Dictionary<string, object?>
                    {
                        ["kat"]            = g.Key,
                        ["eleman_sayisi"]  = g.Count(),
                        ["toplam_maliyet"] = Math.Round(totalCost, 2)
                    };
                })
                .OrderBy(r => r["kat"]?.ToString())
                .ToList();
            ctx.Log($"  cost_by_level: {result.Count} kat");
            return result;
        }

        // ── Alan Hesaplama ────────────────────────────────────────────────────
        [EgOp("wall_area",
            Description = "Duvar listesinin net alanını m2 olarak hesaplar (HOST_AREA_COMPUTED)",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> WallArea(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var walls = ctx.InputAsOrDefault<List<Element>>();
            return walls.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                var a = p?.AsDouble() * Ft2ToM2 ?? 0;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    ["alan_m2"]    = Math.Round(a, 3)
                };
            }).ToList();
        }

        // ── Kalıp Hesaplama ───────────────────────────────────────────────────
        [EgOp("kalip_wall",
            Description = "Duvar listesinin kalıp alanını hesaplar (2 x uzunluk x yükseklik). m2",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> KalipWall(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var walls = ctx.InputAsOrDefault<List<Element>>();
            return walls.Select(e =>
            {
                var lp = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                var hp = e.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                double l = lp?.AsDouble() * 0.3048 ?? 0;
                double h = hp?.AsDouble() * 0.3048 ?? 0;
                double kalip = 2 * l * h;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    ["uzunluk_m"]  = Math.Round(l, 3),
                    ["yukseklik_m"]= Math.Round(h, 3),
                    ["kalip_m2"]   = Math.Round(kalip, 3)
                };
            }).ToList();
        }

        [EgOp("kalip_column",
            Description = "Kolon listesinin kalıp alanını hesaplar (çevre x yükseklik). m2",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> KalipColumn(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var columns = ctx.InputAsOrDefault<List<Element>>();
            return columns.Select(e =>
            {
                var hp = e.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM)
                      ?? e.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
                double h = hp?.AsDouble() * 0.3048 ?? 0;
                // Kesit boyutları
                var type = rctx.Doc.GetElement(e.GetTypeId()) as ElementType;
                double b = (type?.LookupParameter("b")?.AsDouble() ?? 0) * 0.3048;
                double d = (type?.LookupParameter("d")?.AsDouble() ?? 0) * 0.3048;
                double cevre = b > 0 && d > 0 ? 2 * (b + d) : 0;
                double kalip  = cevre * h;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["tip"]        = type?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    ["yukseklik_m"]= Math.Round(h, 3),
                    ["cevre_m"]    = Math.Round(cevre, 3),
                    ["kalip_m2"]   = Math.Round(kalip, 3)
                };
            }).ToList();
        }

        [EgOp("kalip_floor",
            Description = "Döşeme listesinin kalıp alanını hesaplar (alt yüzey = HOST_AREA_COMPUTED). m2",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> KalipFloor(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var floors = ctx.InputAsOrDefault<List<Element>>();
            return floors.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                var a = p?.AsDouble() * Ft2ToM2 ?? 0;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    ["kalip_m2"]   = Math.Round(a, 3)
                };
            }).ToList();
        }

        [EgOp("kalip_summary",
            Description = "Kalıp satırlarını kat ve tip bazında özetler",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> KalipSummary(OpContext ctx)
        {
            var rows   = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var groupBy = ctx.GetString("group_by", "kat");
            var result = rows
                .GroupBy(r => r.TryGetValue(groupBy, out var v) ? v?.ToString() ?? "—" : "—")
                .Select(g =>
                {
                    double total = g.Sum(r =>
                        r.TryGetValue("kalip_m2", out var v) &&
                        double.TryParse(v?.ToString(), out var d) ? d : 0);
                    return new Dictionary<string, object?>
                    {
                        [groupBy]         = g.Key,
                        ["eleman_sayisi"] = g.Count(),
                        ["toplam_kalip_m2"] = Math.Round(total, 3)
                    };
                })
                .OrderBy(r => r[groupBy]?.ToString())
                .ToList();
            double grandTotal = result.Sum(r =>
                double.TryParse(r["toplam_kalip_m2"]?.ToString(), out var d) ? d : 0);
            ctx.Log($"  kalip_summary: {result.Count} grup, toplam {grandTotal:N2} m2");
            return result;
        }

        [EgOp("beton_metraj",
            Description = "Yapısal elemanların beton hacmini m3 olarak hesaplar (duvar+döşeme+kolon+kiriş+temel)",
            Category    = "Maliyet")]
        public static List<Dictionary<string, object?>> BetonMetraj(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                var v = p?.AsDouble() * Ft3ToM3 ?? 0;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["kategori"]   = e.Category?.Name ?? "",
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    ["beton_m3"]   = Math.Round(v, 4)
                };
            }).ToList();
        }
    }
}
