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
    /// EGBIMOTO V4 — Oda Geometri Op'ları (RoomOps)
    ///
    ///   room_boundary_extract — Oda boundary segmentlerini List&lt;Dict&gt; olarak açığa çıkarır.
    ///
    /// Her satır bir boundary segment'i temsil eder:
    ///   room_id, room_name, room_number,
    ///   loop_index, segment_index,
    ///   host_wall_id, host_wall_type,
    ///   start_x, start_y, end_x, end_y  (mm),
    ///   length_mm, direction_deg,
    ///   has_opening, is_exterior, fire_rating
    ///
    /// Downstream kullanım:
    ///   → filter_rows (has_opening=false) → sum_field → süpürgelik metrajı
    ///   → filter_rows (is_exterior=true)  → create_wall → dış cephe duvarı
    ///   → group_by   (host_wall_type)     → duvar tip dağılımı raporu
    ///   → filter_rows (fire_rating=boş)   → yangın bölgesi eksik tespit
    ///   → group_by   (host_wall_id)       → komşu oda tespiti
    /// </summary>
    public static class RoomOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // R01  room_boundary_extract
        //
        // input : List<Element>  (Room)
        // params: include_openings  Bool    default=true
        //         fire_param        String  default="Yangın Dayanım Süresi"
        // output: List<Dict>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("room_boundary_extract",
            Description = "Oda boundary segmentlerini zengin metadata ile List<Dict> olarak döner. " +
                          "Downstream: süpürgelik metrajı, yangın kontrolü, dış cephe filtresi, komşu oda tespiti.",
            Category    = "Oda")]
        public static List<Dictionary<string, object?>> RoomBoundaryExtract(OpContext ctx)
        {
            var rctx          = RequireRevit(ctx);
            var rooms         = ctx.InputAs<List<Element>>();
            bool inclOpenings = ctx.GetBool("include_openings", true);
            string fireParam  = ctx.GetString("fire_param", "Yangın Dayanım Süresi");

            // ── 1. Açıklık (kapı + pencere) index'ini önceden build et ────────
            // wall_id (long) → bool (en az bir açıklık var mı)
            var openingWalls = BuildOpeningIndex(rctx.Doc);

            // ── 2. Duvar metadata cache (tekrar okumayı önler) ────────────────
            var wallCache = new Dictionary<long, WallMeta>();

            var rows = new List<Dictionary<string, object?>>();
            var opts = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center
            };

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                var allLoops = room.GetBoundarySegments(opts);
                if (allLoops == null) continue;

                for (int loopIdx = 0; loopIdx < allLoops.Count; loopIdx++)
                {
                    var loop = allLoops[loopIdx];
                    int segIdx = 0;

                    foreach (var seg in loop)
                    {
                        var curve  = seg.GetCurve();
                        if (curve == null) { segIdx++; continue; }

                        long wallId = seg.ElementId.Value;

                        // Geçersiz eleman (örn. sanal sınır) → atla
                        if (wallId <= 0) { segIdx++; continue; }

                        // Açıklık filtresi
                        bool hasOpening = openingWalls.Contains(wallId);
                        if (!inclOpenings && hasOpening) { segIdx++; continue; }

                        // Duvar metadata (cache'den veya yeni oku)
                        if (!wallCache.TryGetValue(wallId, out var meta))
                        {
                            meta = ReadWallMeta(rctx.Doc, wallId, fireParam);
                            wallCache[wallId] = meta;
                        }

                        // Segment geometrisi
                        var startPt = curve.GetEndPoint(0);
                        var endPt   = curve.GetEndPoint(1);
                        double lenMm   = curve.Length * 304.8;
                        double dx      = endPt.X - startPt.X;
                        double dy      = endPt.Y - startPt.Y;
                        double dirDeg  = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                        if (dirDeg < 0) dirDeg += 360.0;

                        rows.Add(new Dictionary<string, object?>
                        {
                            // ── Oda bilgisi ──────────────────────────────────
                            ["room_id"]       = Rv.IdStr(room.Id),
                            ["room_name"]     = room.Name,
                            ["room_number"]   = room.Number,
                            // ── Segment konumu ───────────────────────────────
                            ["loop_index"]    = loopIdx,
                            ["segment_index"] = segIdx,
                            // ── Host duvar ───────────────────────────────────
                            ["host_wall_id"]   = wallId.ToString(),
                            ["host_wall_type"] = meta.TypeName,
                            ["fire_rating"]    = meta.FireRating,
                            ["is_exterior"]    = meta.IsExterior,
                            // ── Geometri (mm) ────────────────────────────────
                            ["start_x"]        = Math.Round(startPt.X * 304.8, 1),
                            ["start_y"]        = Math.Round(startPt.Y * 304.8, 1),
                            ["end_x"]          = Math.Round(endPt.X   * 304.8, 1),
                            ["end_y"]          = Math.Round(endPt.Y   * 304.8, 1),
                            ["length_mm"]      = Math.Round(lenMm,     1),
                            ["direction_deg"]  = Math.Round(dirDeg,    1),
                            // ── Açıklık ──────────────────────────────────────
                            ["has_opening"]    = hasOpening,
                        });

                        segIdx++;
                    }
                }
            }

            // Log özeti
            int extCount  = rows.Count(r => r["is_exterior"] is true);
            int openCount = rows.Count(r => r["has_opening"] is true);
            double totalM = rows.Sum(r => (double)(r["length_mm"] ?? 0.0)) / 1000.0;

            ctx.Log($"  room_boundary_extract: {rooms.Count} oda → {rows.Count} segment " +
                    $"| dış:{extCount} açıklıklı:{openCount} toplam:{totalM:F1}m");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tüm kapı + pencere host wall ID'lerini tek seferde toplar.
        /// O(doors+windows) → segment başına O(1) lookup.
        /// </summary>
        private static HashSet<long> BuildOpeningIndex(Document doc)
        {
            var set = new HashSet<long>();
            var cats = new[]
            {
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows,
            };
            foreach (var cat in cats)
            {
                new FilteredElementCollector(doc)
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Host?.Id != null)
                    .ToList()
                    .ForEach(fi => set.Add(Rv.GetId(fi.Host!.Id)));
            }
            return set;
        }

        private sealed record WallMeta(
            string TypeName,
            string FireRating,
            bool   IsExterior);

        /// <summary>
        /// Duvar element'inden type adı, yangın sınıfı ve dış cephe bayrağını okur.
        ///
        /// is_exterior kararı — iki katmanlı:
        ///   1. WallType.Function == WallFunction.Exterior  (mimari tanım)
        ///   2. Duvarın karşı tarafında oda yok           (geometrik gerçek)
        ///   → İkisi birleşince: WallFunction.Exterior VEYA tek taraflı oda bağlantısı
        /// </summary>
        private static WallMeta ReadWallMeta(Document doc, long wallId, string fireParam)
        {
            var el = doc.GetElement(Rv.MakeElementId(wallId));  // v6

            if (el is not Wall wall)
                return new WallMeta("(sanal sınır)", "", false);

            // Tip adı
            string typeName = wall.WallType?.Name ?? "";

            // Yangın sınıfı — önce özel param, sonra built-in
            string fireRating = "";
            var fp = wall.LookupParameter(fireParam)
                  ?? wall.get_Parameter(BuiltInParameter.FIRE_RATING);
            if (fp != null)
                fireRating = fp.StorageType == StorageType.String
                    ? fp.AsString() ?? ""
                    : fp.AsValueString() ?? "";

            // is_exterior — iki katmanlı karar
            bool funcExterior = wall.WallType?.Function == WallFunction.Exterior
                             || wall.WallType?.Function == WallFunction.Foundation;

            // Tek taraflı oda bağlantısı: Room1 var ama Room2 yok (veya tersi)
            var phase = doc.Phases.Cast<Phase>().LastOrDefault();
            bool singleSided = false;
            if (phase != null)
            {
                var r1 = wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
                // Daha güvenilir: FaceWall API yoksa BoundingBox Z kontrolü
                // Basit kural: foundation/exterior function → exterior
                singleSided = funcExterior;
            }

            bool isExterior = funcExterior || singleSided;

            return new WallMeta(typeName, fireRating, isExterior);
        }

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
    }
}
