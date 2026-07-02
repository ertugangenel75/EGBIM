using System;
using System.Collections.Generic;
using System.Linq;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Mekansal Graf Op'ları (SpatialOps)
    ///
    ///   build_spatial_graph — room_boundary_extract çıktısından
    ///                         oda komşuluk grafı üretir (edge list).
    ///
    /// Downstream kullanım:
    ///   → filter_rows (is_exterior=false)    → sadece iç duvarlar
    ///   → filter_rows (fire_rating=EI60)     → yangın bölgesi sınırları
    ///   → group_by   (from_room_number)      → oda başına komşu sayısı
    ///   → export_html_report                 → komşuluk haritası raporu
    ///   → filter_rows (to_room_id=null)      → dış cepheye bakan odalar
    /// </summary>
    public static class SpatialOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // S01  build_spatial_graph
        //
        // input : List<Dict>  (room_boundary_extract çıktısı)
        //         Beklenen alanlar: room_id, room_name, room_number,
        //                           host_wall_id, is_exterior, fire_rating,
        //                           host_wall_type, length_mm
        //
        // params: include_exterior  Bool   default=true
        //                           false → sadece iç komşuluk kenarları
        //         deduplicate       Bool   default=true
        //                           true  → A-B ve B-A kenarlarından biri tutulur
        //
        // output: List<Dict>  — her satır bir kenar (edge):
        //   from_room_id, from_room_name, from_room_number,
        //   to_room_id,   to_room_name,   to_room_number,   (null → dış)
        //   shared_wall_id, shared_wall_type,
        //   edge_length_mm, fire_rating, is_exterior
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("build_spatial_graph",
            Description = "room_boundary_extract çıktısından oda komşuluk grafını edge list olarak üretir. " +
                          "Downstream: yangın bölgesi analizi, kaçış yolu, komşu oda sayımı.",
            Category    = "Mekansal")]
        public static List<Dictionary<string, object?>> BuildSpatialGraph(OpContext ctx)
        {
            var segments       = ctx.InputAs<List<Dictionary<string, object?>>>();
            bool inclExterior  = ctx.GetBool("include_exterior", true);
            bool deduplicate   = ctx.GetBool("deduplicate",      true);

            // ── 1. wall_id → bu duvarı paylaşan oda listesi ──────────────────
            // Her segment: room_id + host_wall_id → wall başına oda kümesi
            var wallRooms = new Dictionary<string, List<RoomRef>>();

            foreach (var seg in segments)
            {
                string wallId   = Val(seg, "host_wall_id");
                string roomId   = Val(seg, "room_id");
                bool isExterior = seg.TryGetValue("is_exterior", out var ie) && ie is true;

                if (string.IsNullOrEmpty(wallId) || string.IsNullOrEmpty(roomId)) continue;

                if (!wallRooms.ContainsKey(wallId))
                    wallRooms[wallId] = new List<RoomRef>();

                // Aynı oda + wall kombinasyonu birden fazla segment olabilir (arc)
                // → distinct room set
                if (!wallRooms[wallId].Any(r => r.RoomId == roomId))
                {
                    wallRooms[wallId].Add(new RoomRef(
                        roomId,
                        Val(seg, "room_name"),
                        Val(seg, "room_number"),
                        isExterior,
                        Val(seg, "host_wall_type"),
                        Val(seg, "fire_rating"),
                        ToDouble(seg, "length_mm")
                    ));
                }
                else
                {
                    // Aynı oda/duvar → length_mm'i biriktir (çoklu segment)
                    var existing = wallRooms[wallId].First(r => r.RoomId == roomId);
                    existing.LengthMm += ToDouble(seg, "length_mm");
                }
            }

            // ── 2. Her duvar için kenar üret ─────────────────────────────────
            var edges    = new List<Dictionary<string, object?>>();
            var seen     = new HashSet<string>(); // deduplicate için

            foreach (var (wallId, rooms) in wallRooms)
            {
                if (rooms.Count == 0) continue;

                if (rooms.Count >= 2)
                {
                    // İç kenar: her oda çifti için
                    for (int i = 0; i < rooms.Count; i++)
                    for (int j = i + 1; j < rooms.Count; j++)
                    {
                        var a = rooms[i];
                        var b = rooms[j];

                        string key = string.CompareOrdinal(a.RoomId, b.RoomId) < 0
                            ? $"{a.RoomId}|{b.RoomId}|{wallId}"
                            : $"{b.RoomId}|{a.RoomId}|{wallId}";

                        if (deduplicate && !seen.Add(key)) continue;

                        double edgeLen = (a.LengthMm + b.LengthMm) / 2.0;

                        edges.Add(MakeEdge(
                            a, b, wallId,
                            edgeLen,
                            a.FireRating.Length > 0 ? a.FireRating : b.FireRating,
                            isExterior: false));
                    }
                }
                else if (inclExterior && rooms.Count == 1)
                {
                    // Dış kenar: tek oda + exterior
                    var a = rooms[0];
                    edges.Add(MakeEdge(
                        a, null, wallId,
                        a.LengthMm,
                        a.FireRating,
                        isExterior: true));
                }
            }

            // ── Log özeti ─────────────────────────────────────────────────────
            int innerEdges = edges.Count(e => e["is_exterior"] is false);
            int outerEdges = edges.Count(e => e["is_exterior"] is true);
            var uniqueRooms = segments
                .Select(s => Val(s, "room_id"))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct().Count();

            ctx.Log($"  build_spatial_graph: {uniqueRooms} node " +
                    $"| {innerEdges} iç kenar " +
                    $"| {outerEdges} dış kenar " +
                    $"| toplam {edges.Count} edge");

            return edges;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        private static Dictionary<string, object?> MakeEdge(
            RoomRef from, RoomRef? to, string wallId,
            double lengthMm, string fireRating, bool isExterior)
            => new()
            {
                ["from_room_id"]     = from.RoomId,
                ["from_room_name"]   = from.RoomName,
                ["from_room_number"] = from.RoomNumber,
                ["to_room_id"]       = to?.RoomId,
                ["to_room_name"]     = to?.RoomName,
                ["to_room_number"]   = to?.RoomNumber,
                ["shared_wall_id"]   = wallId,
                ["shared_wall_type"] = from.WallType,
                ["edge_length_mm"]   = Math.Round(lengthMm, 1),
                ["fire_rating"]      = fireRating,
                ["is_exterior"]      = isExterior,
            };

        private static string Val(Dictionary<string, object?> d, string key)
            => d.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";

        private static double ToDouble(Dictionary<string, object?> d, string key)
        {
            if (!d.TryGetValue(key, out var v)) return 0;
            return v switch
            {
                double dv  => dv,
                float  fv  => fv,
                int    iv  => iv,
                string sv  => double.TryParse(sv, out var r) ? r : 0,
                _          => 0,
            };
        }

        /// <summary>Duvarı paylaşan oda referansı — mutable length için class.</summary>
        private sealed class RoomRef
        {
            public string RoomId     { get; }
            public string RoomName   { get; }
            public string RoomNumber { get; }
            public bool   IsExterior { get; }
            public string WallType   { get; }
            public string FireRating { get; }
            public double LengthMm   { get; set; }

            public RoomRef(string roomId, string roomName, string roomNumber,
                           bool isExterior, string wallType, string fireRating, double lengthMm)
            {
                RoomId     = roomId;
                RoomName   = roomName;
                RoomNumber = roomNumber;
                IsExterior = isExterior;
                WallType   = wallType;
                FireRating = fireRating;
                LengthMm   = lengthMm;
            }
        }
    }
}
