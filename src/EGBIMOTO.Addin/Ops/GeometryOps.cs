using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Geometri ve metraj op'ları.
    /// Tüm ölçüler Revit iç birimi (feet) → m/m2/m3 dönüşümü burada yapılır.
    /// </summary>
    public static class GeometryOps
    {
        // ── Sabitler ─────────────────────────────────────────────────────────
        private const double FtToM  = 0.3048;
        private const double Ft2ToM2 = FtToM * FtToM;
        private const double Ft3ToM3 = FtToM * FtToM * FtToM;

        // ── Hacim ─────────────────────────────────────────────────────────────
        [EgOp("wall_volume",
            Description = "Duvar listesinin hacmini m3 olarak hesaplar. {element_id, tip, kat, hacim_m3}",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> WallVolume(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var walls = ctx.InputAsOrDefault<List<Element>>();
            return walls.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                var v = p?.AsDouble() * Ft3ToM3 ?? 0;
                return Row(rctx, e, "hacim_m3", Math.Round(v, 4));
            }).ToList();
        }

        [EgOp("floor_volume",
            Description = "Döşeme listesinin hacmini m3 olarak hesaplar",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> FloorVolume(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var floors = ctx.InputAsOrDefault<List<Element>>();
            return floors.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                var v = p?.AsDouble() * Ft3ToM3 ?? 0;
                return Row(rctx, e, "hacim_m3", Math.Round(v, 4));
            }).ToList();
        }

        [EgOp("column_volume",
            Description = "Kolon listesinin hacmini m3 olarak hesaplar",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> ColumnVolume(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var columns = ctx.InputAsOrDefault<List<Element>>();
            return columns.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                var v = p?.AsDouble() * Ft3ToM3 ?? 0;
                return Row(rctx, e, "hacim_m3", Math.Round(v, 4));
            }).ToList();
        }

        [EgOp("beam_volume",
            Description = "Kiriş listesinin hacmini m3 olarak hesaplar",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> BeamVolume(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var beams = ctx.InputAsOrDefault<List<Element>>();
            return beams.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                var v = p?.AsDouble() * Ft3ToM3 ?? 0;
                return Row(rctx, e, "hacim_m3", Math.Round(v, 4));
            }).ToList();
        }

        [EgOp("foundation_volume",
            Description = "Temel listesinin hacmini m3 olarak hesaplar",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> FoundationVolume(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var foundations = ctx.InputAsOrDefault<List<Element>>();
            return foundations.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                var v = p?.AsDouble() * Ft3ToM3 ?? 0;
                return Row(rctx, e, "hacim_m3", Math.Round(v, 4));
            }).ToList();
        }

        [EgOp("element_volume",
            Description = "Herhangi bir eleman listesinin hacmini m3 olarak hesaplar (HOST_VOLUME_COMPUTED)",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> ElementVolume(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                var v = p?.AsDouble() * Ft3ToM3 ?? 0;
                return Row(rctx, e, "hacim_m3", Math.Round(v, 4));
            }).ToList();
        }

        [EgOp("element_volume_geometry",
            Description = "Eleman geometrisinden hacim hesaplar (Solid union). Daha yavaş ama daha doğru.",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> ElementVolumeGeometry(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var opts     = new Options { DetailLevel = ViewDetailLevel.Fine };
            return elements.Select(e =>
            {
                double vol = 0;
                var geom = e.get_Geometry(opts);
                if (geom != null)
                    foreach (var obj in geom)
                        if (obj is Solid solid && solid.Volume > 0)
                            vol += solid.Volume;
                return Row(rctx, e, "hacim_m3", Math.Round(vol * Ft3ToM3, 4));
            }).ToList();
        }

        // ── Alan ─────────────────────────────────────────────────────────────
        [EgOp("room_area",
            Description = "Oda listesinin alanını m2 olarak hesaplar. {element_id, oda_adi, kat, alan_m2}",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> RoomArea(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var rooms = ctx.InputAsOrDefault<List<Element>>();
            return rooms.Select(e =>
            {
                var room = e as Room;
                var p    = e.get_Parameter(BuiltInParameter.ROOM_AREA);
                var a    = p?.AsDouble() * Ft2ToM2 ?? 0;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["oda_adi"]    = room?.Name ?? e.Name,
                    ["oda_no"]     = room?.Number ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    ["alan_m2"]    = Math.Round(a, 3)
                };
            }).ToList();
        }

        [EgOp("element_area",
            Description = "Eleman listesinin yüzey alanını m2 olarak hesaplar (HOST_AREA_COMPUTED)",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> ElementArea(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                var a = p?.AsDouble() * Ft2ToM2 ?? 0;
                return Row(rctx, e, "alan_m2", Math.Round(a, 3));
            }).ToList();
        }

        [EgOp("wall_net_area",
            Description = "Duvar net alanını hesaplar (brüt - kapı/pencere açıklıkları). m2",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> WallNetArea(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var walls = ctx.InputAsOrDefault<List<Element>>();
            return walls.Select(e =>
            {
                // Revit'te HOST_AREA_COMPUTED zaten net alanı verir
                var p    = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                var net  = p?.AsDouble() * Ft2ToM2 ?? 0;
                var gross = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH) is Parameter lp
                    ? lp.AsDouble() * FtToM *
                      (e.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() * FtToM ?? 0)
                    : net;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    ["brut_m2"]    = Math.Round(gross, 3),
                    ["net_m2"]     = Math.Round(net, 3)
                };
            }).ToList();
        }

        // ── Uzunluk ───────────────────────────────────────────────────────────
        [EgOp("element_length",
            Description = "Eleman listesinin uzunluğunu m olarak hesaplar (CURVE_ELEM_LENGTH)",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> ElementLength(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                var l = p?.AsDouble() * FtToM ?? 0;
                return Row(rctx, e, "uzunluk_m", Math.Round(l, 3));
            }).ToList();
        }

        [EgOp("wall_length",
            Description = "Duvar listesinin uzunluğunu m olarak hesaplar",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> WallLength(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var walls = ctx.InputAsOrDefault<List<Element>>();
            return walls.Select(e =>
            {
                var lp = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                var hp = e.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                var l  = lp?.AsDouble() * FtToM ?? 0;
                var h  = hp?.AsDouble() * FtToM ?? 0;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                    ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                    ["uzunluk_m"]  = Math.Round(l, 3),
                    ["yukseklik_m"]= Math.Round(h, 3)
                };
            }).ToList();
        }

        [EgOp("column_height",
            Description = "Kolon listesinin yüksekliğini m olarak hesaplar",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> ColumnHeight(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var columns = ctx.InputAsOrDefault<List<Element>>();
            return columns.Select(e =>
            {
                var p = e.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM)
                     ?? e.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
                var h = p?.AsDouble() * FtToM ?? 0;
                return Row(rctx, e, "yukseklik_m", Math.Round(h, 3));
            }).ToList();
        }

        // ── Eğri / MEP ───────────────────────────────────────────────────────
        [EgOp("curve_from_element",
            Description = "Eleman listesinden eğri (Curve) geometrisini çıkarır. {element_id, uzunluk_m}",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> CurveFromElement(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements.Select(e =>
            {
                double len = 0;
                if (e.Location is LocationCurve lc)
                    len = lc.Curve.Length * FtToM;
                return Row(rctx, e, "uzunluk_m", Math.Round(len, 3));
            }).ToList();
        }

        [EgOp("mep_summary",
            Description = "MEP eleman listesinin sistem adı ve uzunluk özetini döner",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> MepSummary(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements.Select(e =>
            {
                double len = 0;
                if (e.Location is LocationCurve lc) len = lc.Curve.Length * FtToM;
                var sys = e.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString()
                       ?? e.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsValueString()
                       ?? "—";
                var dia = e.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)?.AsDouble() * FtToM * 1000
                       ?? e.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.AsDouble() * FtToM * 1000
                       ?? 0;
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["kategori"]   = e.Category?.Name ?? "",
                    ["sistem"]     = sys,
                    ["cap_mm"]     = Math.Round(dia, 1),
                    ["uzunluk_m"]  = Math.Round(len, 3)
                };
            }).ToList();
        }

        [EgOp("mep_total_length",
            Description = "MEP eleman listesinin toplam uzunluğunu m olarak döner",
            Category    = "Geometri")]
        public static double MepTotalLength(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            double total = 0;
            foreach (var e in elements)
                if (e.Location is LocationCurve lc)
                    total += lc.Curve.Length * FtToM;
            ctx.Log($"  mep_total_length: {Math.Round(total, 2)} m");
            return Math.Round(total, 2);
        }

        [EgOp("mep_by_system",
            Description = "MEP elemanlarını sistem adına göre gruplar ve toplam uzunluk hesaplar",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> MepBySystem(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements
                .Select(e =>
                {
                    double len = e.Location is LocationCurve lc ? lc.Curve.Length * FtToM : 0;
                    var sys = e.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString()
                           ?? e.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsValueString()
                           ?? "—";
                    return (sys, len, cat: e.Category?.Name ?? "");
                })
                .GroupBy(x => (x.sys, x.cat))
                .Select(g => new Dictionary<string, object?>
                {
                    ["sistem"]       = g.Key.sys,
                    ["kategori"]     = g.Key.cat,
                    ["eleman_sayisi"]= g.Count(),
                    ["toplam_m"]     = Math.Round(g.Sum(x => x.len), 2)
                })
                .OrderByDescending(r => (double)r["toplam_m"]!)
                .ToList();
        }

        // ── Bounding Box ──────────────────────────────────────────────────────
        [EgOp("element_bounding_box",
            Description = "Eleman listesinin bounding box boyutlarını döner. {min_x, min_y, min_z, max_x, max_y, max_z} m",
            Category    = "Geometri")]
        public static List<Dictionary<string, object?>> ElementBoundingBox(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var elements = ctx.InputAsOrDefault<List<Element>>();
            return elements.Select(e =>
            {
                var bb = e.get_BoundingBox(null);
                if (bb is null) return Row(rctx, e, "bb", null);
                return new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["kategori"]   = e.Category?.Name ?? "",
                    ["min_x"]      = Math.Round(bb.Min.X * FtToM, 3),
                    ["min_y"]      = Math.Round(bb.Min.Y * FtToM, 3),
                    ["min_z"]      = Math.Round(bb.Min.Z * FtToM, 3),
                    ["max_x"]      = Math.Round(bb.Max.X * FtToM, 3),
                    ["max_y"]      = Math.Round(bb.Max.Y * FtToM, 3),
                    ["max_z"]      = Math.Round(bb.Max.Z * FtToM, 3)
                };
            }).ToList();
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────
        private static Dictionary<string, object?> Row(RevitOpContext rctx, Element e,
            string valueKey, object? value)
            => new()
            {
                ["element_id"] = Rv.GetId(e.Id),
                ["kategori"]   = e.Category?.Name ?? "",
                ["tip"]        = (rctx.Doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "",
                ["kat"]        = (rctx.Doc.GetElement(e.LevelId) as Level)?.Name ?? "",
                [valueKey]     = value
            };
    }
}
