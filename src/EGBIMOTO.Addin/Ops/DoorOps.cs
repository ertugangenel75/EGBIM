using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Kapı Op'ları (DoorOps)
    ///
    ///   door_handing_detect        — L/R el + inward/outward açılış tespiti
    ///   door_clearance_check       — TS 9111 erişilebilirlik boşluk kontrolü
    ///   door_fire_rating_from_wall — Duvar yangın sınıfını kapıya yaz
    ///   door_number_by_room        — Oda bazlı kapı numaralama
    ///   room_door_relation_map     — Oda → kapı listesi haritası (Dict)
    /// </summary>
    public static class DoorOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // D01  door_handing_detect
        // input : List<Element>  (FamilyInstance — OST_Doors)
        // output: List<Dict>
        //   element_id, handing (L/R), swing (inward/outward/double),
        //   facing_x, facing_y, hand_x, hand_y, hand_flipped, facing_flipped
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("door_handing_detect",
            Description = "Kapı el (L/R) ve açılış yönünü (inward/outward) tespit eder",
            Category    = "Kapı")]
        public static List<Dictionary<string, object?>> DoorHandingDetect(OpContext ctx)
        {
            var doors = ctx.InputAs<List<Element>>();
            var rows  = new List<Dictionary<string, object?>>();

            foreach (var el in doors)
            {
                if (el is not FamilyInstance fi) continue;

                // Revit'te el (handing):
                //   HandFlipped=false → sağ el (R)  → menteşe sağda (dışarıdan bakışta)
                //   HandFlipped=true  → sol el (L)
                //   FacingFlipped     → kapı açılışının yönü (iç/dış)
                bool handFlipped   = fi.HandFlipped;
                bool facingFlipped = fi.FacingFlipped;

                string handing = handFlipped ? "L" : "R";

                // Açılış yönü: FacingOrientation kapı yüzünün dışına bakar.
                // FacingFlipped=false → kapı FacingOrientation'ın karşı tarafına açılır (outward)
                // FacingFlipped=true  → kapı FacingOrientation yönüne açılır (inward)
                string swing;
                var swingParam = fi.Symbol.get_Parameter(BuiltInParameter.DOOR_OPERATION_TYPE);
                if (swingParam != null && swingParam.AsString()?.ToLower().Contains("double") == true)
                    swing = "double";
                else
                    swing = facingFlipped ? "inward" : "outward";

                var facing = fi.FacingOrientation;
                var hand   = fi.HandOrientation;

                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"]     = Rv.IdStr(el.Id),
                    ["handing"]        = handing,
                    ["swing"]          = swing,
                    ["hand_flipped"]   = handFlipped,
                    ["facing_flipped"] = facingFlipped,
                    ["facing_x"]       = Math.Round(facing.X, 4),
                    ["facing_y"]       = Math.Round(facing.Y, 4),
                    ["hand_x"]         = Math.Round(hand.X, 4),
                    ["hand_y"]         = Math.Round(hand.Y, 4),
                });
            }

            ctx.Log($"  door_handing_detect: {rows.Count} kapı işlendi " +
                    $"(L:{rows.Count(r => (string?)r["handing"] == "L")} " +
                    $"R:{rows.Count(r => (string?)r["handing"] == "R")})");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // D02  door_clearance_check
        // input : List<Element>  (FamilyInstance — OST_Doors)
        // params: min_clear_width_mm  (default 850 — TS 9111 engelli erişim)
        //         min_latch_side_mm   (default 300 — menteşe karşı taraf)
        //         severity            (default WARNING)
        // output: ValidationReport
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("door_clearance_check",
            Description = "Kapı temiz açıklık genişliğini ve menteşe karşı duvar boşluğunu TS 9111'e göre kontrol eder",
            Category    = "Kapı")]
        public static ValidationReport DoorClearanceCheck(OpContext ctx)
        {
            var doors        = ctx.InputAs<List<Element>>();
            double minWidthMm  = ctx.GetDouble("min_clear_width_mm", 850);
            double minLatchMm  = ctx.GetDouble("min_latch_side_mm",  300);
            string severity    = ctx.GetString("severity", "WARNING");
            var results        = new List<ValidationResult>();

            foreach (var el in doors)
            {
                if (el is not FamilyInstance fi) continue;

                // ── Temiz açıklık genişliği ───────────────────────────────────
                double doorWidthMm = 0;
                var wp = fi.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                      ?? fi.Symbol.LookupParameter("Width")
                      ?? fi.Symbol.LookupParameter("Genişlik");
                if (wp != null) doorWidthMm = wp.AsDouble() * 304.8;

                // Çerçeve kalınlığı düşüldükten sonra temiz açıklık (yaklaşık %95)
                double clearWidthMm = doorWidthMm * 0.95;
                bool widthOk = clearWidthMm >= minWidthMm;

                results.Add(new ValidationResult
                {
                    RuleId    = "D02-GENISLIK",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "Kapı",
                    CheckType = $"Temiz Açıklık ≥ {minWidthMm:F0}mm",
                    Passed    = widthOk,
                    Severity  = widthOk ? "INFO" : severity,
                    Message   = widthOk
                        ? $"Kapı {el.Id}: {clearWidthMm:F0}mm temiz açıklık ✓"
                        : $"Kapı {el.Id}: {clearWidthMm:F0}mm < {minWidthMm:F0}mm (TS 9111)",
                });

                // ── Menteşe karşı (latch side) duvar boşluğu ─────────────────
                if (fi.Host is Wall wall && wall.Location is LocationCurve lc)
                {
                    double wallLenMm    = lc.Curve.Length * 304.8;
                    double doorLocMm    = 0;

                    if (fi.Location is LocationPoint lp)
                    {
                        var proj    = lc.Curve.Project(lp.Point);
                        doorLocMm   = proj.Parameter * 304.8;
                    }

                    // Menteşe karşı taraf mesafesi (sağ el → sağ taraf, sol el → sol taraf)
                    bool isRight   = !fi.HandFlipped;
                    double latchMm = isRight
                        ? wallLenMm - doorLocMm - doorWidthMm / 2.0
                        : doorLocMm - doorWidthMm / 2.0;

                    bool latchOk = latchMm >= minLatchMm;

                    results.Add(new ValidationResult
                    {
                        RuleId    = "D02-LATCH",
                        ElementId = Rv.IdStr(el.Id),
                        Category  = "Kapı",
                        CheckType = $"Menteşe Karşı Boşluk ≥ {minLatchMm:F0}mm",
                        Passed    = latchOk,
                        Severity  = latchOk ? "INFO" : severity,
                        Message   = latchOk
                            ? $"Kapı {el.Id}: latch={latchMm:F0}mm ✓"
                            : $"Kapı {el.Id}: latch={latchMm:F0}mm < {minLatchMm:F0}mm",
                    });
                }
            }

            int fail = results.Count(r => !r.Passed);
            ctx.Log($"  door_clearance_check: {results.Count} kontrol, {fail} başarısız");
            return MakeReport("Kapı Erişilebilirlik Kontrolü", results);
        }

        // ─────────────────────────────────────────────────────────────────────
        // D03  door_fire_rating_from_wall
        // input : List<Element>  (FamilyInstance — OST_Doors)
        // params: wall_param  (default "Yangın Dayanım Süresi")
        //         door_param  (default "EG_YanginDayanim")
        //         rating_map  opsiyonel: "60:EI60,90:EI90,120:EI120"
        // output: int (güncellenen kapı sayısı)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("door_fire_rating_from_wall",
            RequiresTransaction = true,
            Description = "Host duvarın yangın sınıfını okuyarak kapı parametresine yazar",
            Category    = "Kapı")]
        public static int DoorFireRatingFromWall(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var doors     = ctx.InputAs<List<Element>>();
            var wallParam = ctx.GetString("wall_param", "Yangın Dayanım Süresi");
            var doorParam = ctx.GetString("door_param", "EG_YanginDayanim");
            var mapStr    = ctx.GetString("rating_map", "");

            // rating_map parse: "60:EI60,90:EI90,120:EI120"
            var ratingMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(mapStr))
            {
                foreach (var pair in mapStr.Split(','))
                {
                    var kv = pair.Split(':');
                    if (kv.Length == 2) ratingMap[kv[0].Trim()] = kv[1].Trim();
                }
            }

            int count = 0;
            using var scope = new RevitWriteScope(rctx.Doc, "Kapı Yangın Sınıfı", rctx.IsAtomicMode);

            foreach (var el in doors)
            {
                if (el is not FamilyInstance fi) continue;
                if (fi.Host is not Wall wall)    continue;

                // Duvardan yangın sınıfını oku
                var wp = wall.LookupParameter(wallParam)
                      ?? wall.get_Parameter(BuiltInParameter.FIRE_RATING);
                if (wp == null) continue;

                string rawVal = wp.StorageType == StorageType.String
                    ? wp.AsString() ?? ""
                    : wp.AsValueString() ?? "";

                if (string.IsNullOrEmpty(rawVal)) continue;

                // Map uygula (varsa)
                string writeVal = ratingMap.TryGetValue(rawVal, out var mapped) ? mapped : rawVal;

                // Kapı parametresine yaz
                var dp = fi.LookupParameter(doorParam);
                if (dp == null || dp.IsReadOnly) continue;

                if (dp.StorageType == StorageType.String)
                    dp.Set(writeVal);
                else if (dp.StorageType == StorageType.Double &&
                         double.TryParse(rawVal, out double d))
                    dp.Set(d);
                else continue;

                count++;
            }

            scope.Commit();
            ctx.Log($"  door_fire_rating_from_wall: {count}/{doors.Count} kapı güncellendi");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // D04  door_number_by_room
        // input : List<Element>  (FamilyInstance — OST_Doors)
        // params: param_name   (default "Mark")
        //         separator    (default "-")
        //         start_index  (default 1)
        //         use_room     (default "to_room" | "from_room")
        // output: int
        //
        // Örnek çıktı: oda 101 → K1=101-K1, K2=101-K2
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("door_number_by_room",
            RequiresTransaction = true,
            Description = "Kapıları oda numarasına göre sıralar ve params.param_name parametresine yazar",
            Category    = "Kapı")]
        public static int DoorNumberByRoom(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var doors     = ctx.InputAs<List<Element>>();
            var paramName = ctx.GetString("param_name",  "Mark");
            var separator = ctx.GetString("separator",   "-");
            int startIdx  = ctx.GetInt("start_index", 1);
            var useRoom   = ctx.GetString("use_room",    "to_room").ToLowerInvariant();

            // Oda → kapı listesi grupla
            var roomGroups = new Dictionary<string, List<FamilyInstance>>();

            foreach (var el in doors)
            {
                if (el is not FamilyInstance fi) continue;

                Room? room = useRoom == "from_room" ? fi.FromRoom : fi.ToRoom;
                room ??= fi.ToRoom ?? fi.FromRoom;  // fallback

                string roomNo = room?.Number ?? "XX";

                if (!roomGroups.ContainsKey(roomNo))
                    roomGroups[roomNo] = new List<FamilyInstance>();
                roomGroups[roomNo].Add(fi);
            }

            int count = 0;
            using var scope = new RevitWriteScope(rctx.Doc, "Kapı Numaralama", rctx.IsAtomicMode);

            foreach (var (roomNo, fiList) in roomGroups)
            {
                // Oda içinde X konumuna göre sırala (soldan sağa)
                var sorted = fiList.OrderBy(fi =>
                {
                    var lp = fi.Location as LocationPoint;
                    return lp?.Point.X ?? 0;
                }).ToList();

                for (int i = 0; i < sorted.Count; i++)
                {
                    var fi  = sorted[i];
                    var p   = fi.LookupParameter(paramName);
                    if (p == null || p.IsReadOnly) continue;

                    string mark = $"{roomNo}{separator}K{startIdx + i}";
                    p.Set(mark);
                    count++;
                }
            }

            scope.Commit();
            ctx.Log($"  door_number_by_room: {count} kapı numaralandı " +
                    $"({roomGroups.Count} oda grubu)");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // D05  room_door_relation_map
        // input : List<Element>  (Room)
        // output: List<Dict>
        //   room_id, room_name, room_number, door_count,
        //   door_ids (";"-ayrılmış), door_marks (";"-ayrılmış)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("room_door_relation_map",
            Description = "Her oda için boundary'sindeki kapıları haritalar — oda → kapı listesi Dict",
            Category    = "Kapı")]
        public static List<Dictionary<string, object?>> RoomDoorRelationMap(OpContext ctx)
        {
            var rctx  = RequireRevit(ctx);
            var rooms = ctx.InputAs<List<Element>>();
            var rows  = new List<Dictionary<string, object?>>();
            var opts  = new SpatialElementBoundaryOptions();

            // Tüm kapıları bir kez topla ve host-wall index'i oluştur
            var allDoors = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            // wall_id → kapılar lookup
            var wallDoorMap = new Dictionary<long, List<FamilyInstance>>();
            foreach (var fi in allDoors)
            {
                if (fi.Host?.Id == null) continue;
                long wid = Rv.GetId(fi.Host.Id);
                if (!wallDoorMap.ContainsKey(wid))
                    wallDoorMap[wid] = new List<FamilyInstance>();
                wallDoorMap[wid].Add(fi);
            }

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                var doorsInRoom = new List<FamilyInstance>();
                var segs        = room.GetBoundarySegments(opts);

                foreach (var loop in segs)
                foreach (var seg in loop)
                {
                    long wid = seg.ElementId.Value;
                    if (wallDoorMap.TryGetValue(wid, out var wdoors))
                        doorsInRoom.AddRange(wdoors);
                }

                // Tekrar edenleri kaldır
                doorsInRoom = doorsInRoom
                    .GroupBy(d => Rv.GetId(d.Id))
                    .Select(g => g.First())
                    .ToList();

                string doorIds   = string.Join(";", doorsInRoom.Select(d => Rv.GetId(d.Id)));
                string doorMarks = string.Join(";", doorsInRoom.Select(d =>
                {
                    var mp = d.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    return mp?.AsString() ?? Rv.IdStr(d.Id);
                }));

                rows.Add(new Dictionary<string, object?>
                {
                    ["room_id"]     = Rv.IdStr(room.Id),
                    ["room_name"]   = room.Name,
                    ["room_number"] = room.Number,
                    ["door_count"]  = doorsInRoom.Count,
                    ["door_ids"]    = doorIds,
                    ["door_marks"]  = doorMarks,
                });
            }

            ctx.Log($"  room_door_relation_map: {rooms.Count} oda → " +
                    $"{rows.Sum(r => (int)(r["door_count"] ?? 0))} kapı toplam");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

        private static ValidationReport MakeReport(string title, List<ValidationResult> results)
            => new()
            {
                ManifestTitle = title,
                Results       = results,
                Passed        = results.Count(r => r.Passed),
                Failed        = results.Count(r => !r.Passed),
            };
    }
}
