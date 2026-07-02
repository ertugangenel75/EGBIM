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
    /// EGBIMOTO V4 — Oluşturma Op'ları  (Tier 3)
    ///
    ///   create_wall   — Çizgi / 2-çizgi / oda sınırından duvar üret (3 mod)
    ///   create_floor  — Nokta listesinden döşeme
    ///   create_room   — Noktaya oda yerleştir
    ///   create_level  — Kat ekle
    ///   create_grid   — Aks ekle
    ///
    /// Tüm op'lar RequiresTransaction = true.
    /// RevitWriteScope → atomic + normal mod desteği.
    /// </summary>
    public static class CreationOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // C01  create_wall
        //
        // mode = "from_lines"    → Seçili çizgiler boyunca duvar
        // mode = "between_lines" → 2 paralel çizgi arasına duvar (merkez = orta)
        // mode = "from_room"     → Oda boundary'sinden duvar
        //
        // Ortak params:
        //   type_name      String  zorunlu
        //   level_name     String  zorunlu
        //   height_mm      Double  default=3000
        //   reference      String  centerline|exterior_face|interior_face
        //                         finish_face_exterior|finish_face_interior
        //                         default=centerline
        //   flip           Bool    default=false
        //   structural     Bool    default=false
        //   skip_existing  Bool    default=true   (from_room: zaten duvar olan segment)
        //
        // returns: List<Element> (oluşturulan Wall'lar)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_wall",
            RequiresTransaction = true,
            Description = "Çizgi / 2-çizgi / oda sınırından duvar oluşturur. params: mode, type_name, level_name, height_mm, reference",
            Category    = "Oluşturma")]
        public static List<Element> CreateWall(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var input     = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var mode      = ctx.GetString("mode", "from_lines").ToLowerInvariant();
            var typeName  = ctx.RequireString("type_name");
            var levelName = ctx.RequireString("level_name");
            double heightFt = ctx.GetDouble("height_mm", 3000) / 304.8;
            bool   flip     = ctx.GetBool("flip", false);
            bool   structural = ctx.GetBool("structural", false);
            bool   skipExisting = ctx.GetBool("skip_existing", true);
            var    refStr   = ctx.GetString("reference", "centerline");

            var wallType = FindWallType(rctx.Doc, typeName);
            if (wallType == null)
            {
                ctx.Log($"  create_wall: '{typeName}' duvar tipi bulunamadı → []");
                return new List<Element>();
            }

            var level = FindLevel(rctx.Doc, levelName);
            if (level == null)
            {
                ctx.Log($"  create_wall: '{levelName}' katı bulunamadı → []");
                return new List<Element>();
            }

            // WallLocationLine enum
            WallLocationLine locLine = refStr.ToLowerInvariant() switch
            {
                "exterior_face"          => WallLocationLine.CoreExterior,
                "interior_face"          => WallLocationLine.CoreInterior,
                "finish_face_exterior"   => WallLocationLine.FinishFaceExterior,
                "finish_face_interior"   => WallLocationLine.FinishFaceInterior,
                _                        => WallLocationLine.WallCenterline,
            };

            // Curve listesi moda göre hazırlanır
            List<Curve> curves = mode switch
            {
                "from_lines"     => ExtractCurves(input),
                "between_lines"  => ExtractMidCurve(input, ctx),
                "from_room"      => ExtractRoomBoundaryCurves(rctx.Doc, input, skipExisting),
                _                => ExtractCurves(input),
            };

            if (curves.Count == 0)
            {
                ctx.Log($"  create_wall [{mode}]: curve bulunamadı → []");
                return new List<Element>();
            }

            var created = new List<Element>();
            using var scope = new RevitWriteScope(rctx.Doc, $"Duvar Oluştur [{mode}]", rctx.IsAtomicMode);

            foreach (var curve in curves)
            {
                try
                {
                    var wall = Wall.Create(rctx.Doc, curve, wallType.Id, level.Id,
                                          heightFt, 0.0, flip, structural);
                    wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM)?.Set((int)locLine);
                    created.Add(wall);
                }
                catch (Exception ex)
                {
                    ctx.Log($"  create_wall: curve atlandı — {ex.Message}");
                }
            }

            scope.Commit();
            ctx.Log($"  create_wall [{mode}]: {created.Count}/{curves.Count} duvar oluşturuldu");
            return created;
        }

        // ─────────────────────────────────────────────────────────────────────
        // C02  create_floor
        // input : boş (params'tan nokta al) veya List<Element> (Rooms)
        // params: type_name, level_name,
        //         points = "x1,y1;x2,y2;x3,y3;..." mm (input boşsa zorunlu)
        //         offset_mm = 0  (döşeme oturma ofseti)
        // returns: List<Element>
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_floor",
            RequiresTransaction = true,
            Description = "Nokta listesi veya oda boundary'sinden döşeme oluşturur",
            Category    = "Oluşturma")]
        public static List<Element> CreateFloor(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var input     = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var typeName  = ctx.RequireString("type_name");
            var levelName = ctx.RequireString("level_name");
            double offsetFt = ctx.GetDouble("offset_mm", 0) / 304.8;

            var floorType = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

            if (floorType == null)
            {
                ctx.Log($"  create_floor: '{typeName}' döşeme tipi bulunamadı → []");
                return new List<Element>();
            }

            var level = FindLevel(rctx.Doc, levelName);
            if (level == null)
            {
                ctx.Log($"  create_floor: '{levelName}' katı bulunamadı → []");
                return new List<Element>();
            }

            // Curve loop listesi oluştur
            var loopSets = new List<CurveLoop>();

            if (input.Any(e => e is Room))
            {
                // Oda boundary'sinden curve loop
                var opts = new SpatialElementBoundaryOptions();
                foreach (var el in input.OfType<Room>())
                {
                    foreach (var loop in el.GetBoundarySegments(opts))
                    {
                        var cl = new CurveLoop();
                        foreach (var seg in loop)
                            cl.Append(seg.GetCurve());
                        loopSets.Add(cl);
                    }
                }
            }
            else
            {
                // Nokta string'inden: "x1,y1;x2,y2;x3,y3"
                var pointsStr = ctx.GetString("points", "");
                if (!string.IsNullOrEmpty(pointsStr))
                {
                    var cl = ParsePointsToLoop(pointsStr);
                    if (cl != null) loopSets.Add(cl);
                }
            }

            if (loopSets.Count == 0)
            {
                ctx.Log("  create_floor: loop bulunamadı → []");
                return new List<Element>();
            }

            var created = new List<Element>();
            using var scope = new RevitWriteScope(rctx.Doc, "Döşeme Oluştur", rctx.IsAtomicMode);

            foreach (var loop in loopSets)
            {
                try
                {
                    // Revit 2022+ API: Floor.Create(doc, curveLoops, floorTypeId, levelId)
                    var floor = Floor.Create(rctx.Doc,
                        new List<CurveLoop> { loop },
                        floorType.Id, level.Id);

                    if (Math.Abs(offsetFt) > 1e-6)
                        floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(offsetFt);

                    created.Add(floor);
                }
                catch (Exception ex)
                {
                    ctx.Log($"  create_floor: loop atlandı — {ex.Message}");
                }
            }

            scope.Commit();
            ctx.Log($"  create_floor: {created.Count}/{loopSets.Count} döşeme oluşturuldu");
            return created;
        }

        // ─────────────────────────────────────────────────────────────────────
        // C03  create_room
        // params: x_mm, y_mm, level_name, name (default="Oda"), number (default="")
        // returns: List<Element> (Room)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_room",
            RequiresTransaction = true,
            Description = "Belirtilen noktaya oda yerleştirir. params: x_mm, y_mm, level_name, name, number",
            Category    = "Oluşturma")]
        public static List<Element> CreateRoom(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            double xFt    = ctx.GetDouble("x_mm", 0) / 304.8;
            double yFt    = ctx.GetDouble("y_mm", 0) / 304.8;
            var levelName = ctx.RequireString("level_name");
            var roomName  = ctx.GetString("name",   "Oda");
            var roomNum   = ctx.GetString("number", "");

            var level = FindLevel(rctx.Doc, levelName);
            if (level == null)
            {
                ctx.Log($"  create_room: '{levelName}' katı bulunamadı → []");
                return new List<Element>();
            }

            using var scope = new RevitWriteScope(rctx.Doc, $"Oda: {roomName}", rctx.IsAtomicMode);

            var uv   = new UV(xFt, yFt);
            var room = rctx.Doc.Create.NewRoom(level, uv);
            room.Name   = roomName;
            room.Number = roomNum;

            scope.Commit();
            ctx.Log($"  create_room: '{roomNum} {roomName}' → ({xFt*304.8:F0},{yFt*304.8:F0}) mm");
            return new List<Element> { room };
        }

        // ─────────────────────────────────────────────────────────────────────
        // C04  create_level
        // params: elevation_mm (zorunlu), name (opsiyonel)
        // returns: List<Element> (Level)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_level",
            RequiresTransaction = true,
            Description = "Belirtilen kota kat oluşturur. params: elevation_mm, name",
            Category    = "Oluşturma")]
        public static List<Element> CreateLevel(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            double elevFt  = ctx.GetDouble("elevation_mm", 0) / 304.8;
            var name       = ctx.GetString("name", $"Kat {elevFt*304.8/1000:F2}m");

            using var scope = new RevitWriteScope(rctx.Doc, $"Kat: {name}", rctx.IsAtomicMode);

            var level = Level.Create(rctx.Doc, elevFt);
            level.Name = name;

            scope.Commit();
            ctx.Log($"  create_level: '{name}' @ {elevFt*304.8:F0} mm");
            return new List<Element> { level };
        }

        // ─────────────────────────────────────────────────────────────────────
        // C05  create_grid
        // params: x1_mm,y1_mm,x2_mm,y2_mm (zorunlu), name (opsiyonel)
        // returns: List<Element> (Grid)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_grid",
            RequiresTransaction = true,
            Description = "İki nokta arasına aks oluşturur. params: x1_mm,y1_mm,x2_mm,y2_mm,name",
            Category    = "Oluşturma")]
        public static List<Element> CreateGrid(OpContext ctx)
        {
            var rctx  = RequireRevit(ctx);
            double x1 = ctx.GetDouble("x1_mm", 0) / 304.8;
            double y1 = ctx.GetDouble("y1_mm", 0) / 304.8;
            double x2 = ctx.GetDouble("x2_mm", 1000) / 304.8;
            double y2 = ctx.GetDouble("y2_mm", 0) / 304.8;
            var    name = ctx.GetString("name", "");

            // Aynı nokta kontrolü
            if (Math.Abs(x2 - x1) < 1e-6 && Math.Abs(y2 - y1) < 1e-6)
            {
                ctx.Log("  create_grid: başlangıç ve bitiş aynı nokta → []");
                return new List<Element>();
            }

            var line = Line.CreateBound(new XYZ(x1, y1, 0), new XYZ(x2, y2, 0));

            using var scope = new RevitWriteScope(rctx.Doc, $"Aks: {name}", rctx.IsAtomicMode);

            var grid = Grid.Create(rctx.Doc, line);
            if (!string.IsNullOrEmpty(name)) grid.Name = name;

            scope.Commit();
            ctx.Log($"  create_grid: '{grid.Name}' oluşturuldu");
            return new List<Element> { grid };
        }

        // ─────────────────────────────────────────────────────────────────────
        // create_wall yardımcı metodları
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Model/Detail line listesinden Curve çıkarır.</summary>
        private static List<Curve> ExtractCurves(List<Element> elements)
        {
            var curves = new List<Curve>();
            foreach (var el in elements)
            {
                Curve? c = el switch
                {
                    CurveElement ce => ce.GeometryCurve,
                    Wall w          => (w.Location as LocationCurve)?.Curve,
                    _               => null,
                };
                if (c != null) curves.Add(c);
            }
            return curves;
        }

        /// <summary>
        /// 2 paralel çizgi arasından orta çizgiyi hesaplar.
        /// Tam olarak 2 eleman beklenir.
        /// </summary>
        private static List<Curve> ExtractMidCurve(List<Element> elements, OpContext ctx)
        {
            if (elements.Count != 2)
            {
                ctx.Log($"  create_wall [between_lines]: 2 çizgi bekleniyor, {elements.Count} geldi → atlandı");
                return new List<Curve>();
            }

            var c1 = (elements[0] as CurveElement)?.GeometryCurve;
            var c2 = (elements[1] as CurveElement)?.GeometryCurve;

            if (c1 == null || c2 == null) return new List<Curve>();

            // Her iki çizginin başlangıç ve bitiş noktalarının ortası
            var midStart = (c1.GetEndPoint(0) + c2.GetEndPoint(0)) / 2.0;
            var midEnd   = (c1.GetEndPoint(1) + c2.GetEndPoint(1)) / 2.0;

            if (midStart.DistanceTo(midEnd) < 1e-4)
            {
                ctx.Log("  create_wall [between_lines]: orta çizgi sıfır uzunluklu → atlandı");
                return new List<Curve>();
            }

            return new List<Curve> { Line.CreateBound(midStart, midEnd) };
        }

        /// <summary>
        /// Oda boundary segmentlerinden curve çıkarır.
        /// skip_existing=true ise zaten duvar olan segmentleri atlar.
        /// </summary>
        private static List<Curve> ExtractRoomBoundaryCurves(
            Document doc, List<Element> elements, bool skipExisting)
        {
            var curves  = new List<Curve>();
            var opts    = new SpatialElementBoundaryOptions();

            // Mevcut duvar curve'lerini cache'le (skip_existing için)
            HashSet<string>? existingCurveKeys = null;
            if (skipExisting)
            {
                existingCurveKeys = new HashSet<string>();
                var walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>();
                foreach (var w in walls)
                {
                    if (w.Location is LocationCurve lc)
                        existingCurveKeys.Add(CurveKey(lc.Curve));
                }
            }

            foreach (var el in elements)
            {
                if (el is not Room room) continue;

                foreach (var loop in room.GetBoundarySegments(opts))
                foreach (var seg in loop)
                {
                    var curve = seg.GetCurve();
                    if (curve == null) continue;

                    if (skipExisting && existingCurveKeys!.Contains(CurveKey(curve)))
                        continue;

                    curves.Add(curve);
                }
            }

            return curves;
        }

        /// <summary>Curve için tekrar tespiti anahtarı (başlangıç/bitiş koordinat özeti).</summary>
        private static string CurveKey(Curve c)
        {
            var s = c.GetEndPoint(0);
            var e = c.GetEndPoint(1);
            return $"{s.X:F2},{s.Y:F2}|{e.X:F2},{e.Y:F2}";
        }

        /// <summary>"x1,y1;x2,y2;..." formatındaki string'den CurveLoop üretir.</summary>
        private static CurveLoop? ParsePointsToLoop(string pointsStr)
        {
            try
            {
                var pts = pointsStr.Split(';')
                    .Select(p => p.Split(','))
                    .Where(a => a.Length >= 2)
                    .Select(a => new XYZ(
                        double.Parse(a[0].Trim()) / 304.8,
                        double.Parse(a[1].Trim()) / 304.8,
                        a.Length > 2 ? double.Parse(a[2].Trim()) / 304.8 : 0))
                    .ToList();

                if (pts.Count < 3) return null;

                var loop = new CurveLoop();
                for (int i = 0; i < pts.Count; i++)
                {
                    var from = pts[i];
                    var to   = pts[(i + 1) % pts.Count];
                    if (from.DistanceTo(to) < 1e-4) continue;
                    loop.Append(Line.CreateBound(from, to));
                }
                return loop;
            }
            catch { return null; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ortak yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

        private static WallType? FindWallType(Document doc, string typeName)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

        private static Level? FindLevel(Document doc, string levelName)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
    }
}
