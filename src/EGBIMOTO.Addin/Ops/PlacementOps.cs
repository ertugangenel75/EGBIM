using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Yerleştirme Op'ları (PlacementOps)
    ///
    /// Op'lar:
    ///   collect_doors_in_room    — Odanın boundary'sindeki kapıları bulur
    ///   get_door_wall_clearances — Kapının sol/sağ duvar mesafesi (mm)
    ///   calc_placement_point     — Offset + yükseklik → XYZ dict üretir
    ///   place_family_on_wall     — Duvara host family yerleştirir [Transaction]
    ///   get_room_ceiling_center  — Oda tavan merkez XYZ listesi
    ///   place_family_on_ceiling  — Tavana face-based family yerleştirir [Transaction]
    ///
    /// Yerleştirme op'ları gerçek NewFamilyInstance çağrıları yapar; yazma işlemleri
    /// RevitWriteScope ile sarılır (atomik modda dış transaction'a katılır).
    /// </summary>
    public static class PlacementOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // P01: collect_doors_in_room
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("collect_doors_in_room",
            Description = "Odanın boundary segmentlerinden host duvarlarını bulur, o duvarlardaki kapıları döner",
            Category    = "Yerleştirme")]
        public static List<Element> CollectDoorsInRoom(OpContext ctx)
        {
            var rctx = RequireRevit(ctx);
            var rooms = ctx.InputAsOrDefault<List<Element>>();

            var result = new List<Element>();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                // Oda boundary duvarlarını topla
                var opts     = new SpatialElementBoundaryOptions();
                var segments = room.GetBoundarySegments(opts);
                var wallIds  = new HashSet<ElementId>();

                foreach (var loop in segments)
                foreach (var seg in loop)
                {
                    var hostId = seg.ElementId;
                    if (hostId != ElementId.InvalidElementId)
                        wallIds.Add(hostId);
                }

                // Bu duvarları host eden kapıları bul
                var doors = new FilteredElementCollector(rctx.Doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi =>
                    {
                        var hostId = fi.Host?.Id;
                        return hostId != null && wallIds.Contains(hostId);
                    })
                    .Cast<Element>();

                result.AddRange(doors);
            }

            // Tekrar edenleri kaldır
            result = result
                .GroupBy(e => Rv.GetId(e.Id))
                .Select(g => g.First())
                .ToList();

            ctx.Log($"  collect_doors_in_room: {rooms.Count} oda → {result.Count} kapı");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // P02: get_door_wall_clearances
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("get_door_wall_clearances",
            Description = "Her kapı için duvardaki sol/sağ serbest mesafeyi mm olarak döner",
            Category    = "Yerleştirme")]
        public static List<Dictionary<string, object?>> GetDoorWallClearances(OpContext ctx)
        {
            var rctx  = RequireRevit(ctx);
            var doors = ctx.InputAsOrDefault<List<Element>>();
            var rows  = new List<Dictionary<string, object?>>();

            foreach (var el in doors)
            {
                if (el is not FamilyInstance fi) continue;
                if (fi.Host is not Wall wall)    continue;

                // Kapı genişliği (ft → mm)
                double doorWidthMm = 0;
                var widthParam = fi.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                if (widthParam != null)
                    doorWidthMm = widthParam.AsDouble() * 304.8;

                // Duvar uzunluğu (LocationCurve → ft → mm)
                double wallLengthMm = 0;
                if (wall.Location is LocationCurve lc)
                    wallLengthMm = lc.Curve.Length * 304.8;

                // Kapı konumu duvar boyunca (projeksiyon)
                double leftMm  = 0;
                double rightMm = 0;

                if (fi.Location is LocationPoint lp && wall.Location is LocationCurve wallCurve)
                {
                    var pt       = lp.Point;
                    var line     = wallCurve.Curve;
                    double param = line.Project(pt).Parameter;   // normalize edilmemiş ft
                    double distFromStart = param * 304.8;        // mm

                    leftMm  = distFromStart - doorWidthMm / 2.0;
                    rightMm = wallLengthMm - distFromStart - doorWidthMm / 2.0;
                }

                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"]    = Rv.IdStr(el.Id),
                    ["wall_id"]       = Rv.IdStr(wall.Id),
                    ["left_mm"]       = Math.Round(leftMm,  1),
                    ["right_mm"]      = Math.Round(rightMm, 1),
                    ["door_width_mm"] = Math.Round(doorWidthMm, 1),
                    ["wall_length_mm"]= Math.Round(wallLengthMm, 1),
                });
            }

            ctx.Log($"  get_door_wall_clearances: {rows.Count} kapı işlendi");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // P03: calc_placement_point
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("calc_placement_point",
            Description = "Girdi elemanının origin'inden offset_x/y_mm + height_mm ile XYZ dict üretir",
            Category    = "Yerleştirme")]
        public static Dictionary<string, object?> CalcPlacementPoint(OpContext ctx)
        {
            var rctx     = RequireRevit(ctx);
            var elements = ctx.InputAsOrDefault<List<Element>>();

            double offsetXft = ctx.GetDouble("offset_x_mm", 0) / 304.8;
            double offsetYft = ctx.GetDouble("offset_y_mm", 0) / 304.8;
            double heightFt  = ctx.GetDouble("height_mm",   0) / 304.8;

            var first = elements.FirstOrDefault();
            XYZ origin = XYZ.Zero;
            ElementId levelId = ElementId.InvalidElementId;

            if (first?.Location is LocationPoint lp)
                origin = lp.Point;
            else if (first?.Location is LocationCurve lcv)
                origin = lcv.Curve.Evaluate(0.5, true);

            // Level bul
            var lvlParam = first?.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                        ?? first?.get_Parameter(BuiltInParameter.ROOM_LEVEL_ID);
            if (lvlParam != null)
                levelId = lvlParam.AsElementId();

            var target = new XYZ(
                origin.X + offsetXft,
                origin.Y + offsetYft,
                origin.Z + heightFt);

            ctx.Log($"  calc_placement_point: ({target.X:F3}, {target.Y:F3}, {target.Z:F3}) ft");
            return new Dictionary<string, object?>
            {
                ["x"]        = target.X,
                ["y"]        = target.Y,
                ["z"]        = target.Z,
                ["level_id"] = levelId.Value.ToString(),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // P04: get_room_ceiling_center
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("get_room_ceiling_center",
            Description = "Her odanın tavan düzeyindeki merkez XYZ koordinatını döner",
            Category    = "Yerleştirme")]
        public static List<Dictionary<string, object?>> GetRoomCeilingCenter(OpContext ctx)
        {
            var rctx  = RequireRevit(ctx);
            var rooms = ctx.InputAsOrDefault<List<Element>>();
            var rows  = new List<Dictionary<string, object?>>();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                // Oda üst yüzey yüksekliği: alt kotu + tavan yüksekliği (ft)
                double floorElev   = room.Level?.Elevation ?? 0;
                double roomHeightFt = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT)
                                         ?.AsDouble() ?? 0;
                double ceilingZ    = floorElev + roomHeightFt;

                // Oda merkezi: Location veya bounding box XY ortası
                double cx = 0, cy = 0;
                if (room.Location is LocationPoint lp)
                {
                    cx = lp.Point.X;
                    cy = lp.Point.Y;
                }
                else
                {
                    var bb = room.get_BoundingBox(null);
                    if (bb != null)
                    {
                        cx = (bb.Min.X + bb.Max.X) / 2.0;
                        cy = (bb.Min.Y + bb.Max.Y) / 2.0;
                    }
                }

                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.IdStr(room.Id),
                    ["room_name"]  = room.Name,
                    ["x"]          = Math.Round(cx,        6),
                    ["y"]          = Math.Round(cy,        6),
                    ["z"]          = Math.Round(ceilingZ,  6),
                });
            }

            ctx.Log($"  get_room_ceiling_center: {rows.Count} oda");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // P05: place_family_on_wall   (GERÇEK — duvar host'lu yerleştirme)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("place_family_on_wall",
            RequiresTransaction = true,
            Description =
                "Duvara host'lu aile yerleştirir (kapı/pencere/aydınlatma vb.). Transaction gerektirir.\n" +
                "Input: List<Element> (duvarlar). " +
                "params: family_name, type_name (zorunlu), offset_mm (default 1200, duvar başından ilk nokta), " +
                "spacing_mm (default 0 = tek eleman; >0 ise aralıklı çoklu), sill_mm (default 0, yerleştirme yüksekliği).\n" +
                "Çıktı: List<Dictionary> — host_id, placed_count, wall_length_m.",
            Category    = "Yerleştirme")]
        public static List<Dictionary<string, object?>> PlaceFamilyOnWall(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var walls      = ctx.InputAsOrDefault<List<Element>>();
            var familyName = ctx.GetString("family_name");
            var typeName   = ctx.GetString("type_name");
            double offsetMm   = ctx.GetDouble("offset_mm",  1200);  // duvar başından ilk yerleştirme mesafesi
            double spacingMm  = ctx.GetDouble("spacing_mm", 0);     // 0 = duvar başına tek eleman (offset noktasında)
            double sillMm     = ctx.GetDouble("sill_mm",    0);     // yerleştirme yüksekliği (level'dan)

            var results = new List<Dictionary<string, object?>>();

            // ── FamilySymbol bul ──────────────────────────────────────────────
            var symbol = FindFamilySymbol(rctx.Doc, familyName, typeName);
            if (symbol == null)
            {
                results.Add(new Dictionary<string, object?>
                {
                    ["status"]  = "FAMILY_NOT_FOUND",
                    ["message"] = $"'{familyName}/{typeName}' tipi projede bulunamadı."
                });
                ctx.Log($"  place_family_on_wall: '{familyName}/{typeName}' tipi bulunamadı → 0 yerleştirme");
                return results;
            }

            if (!symbol.IsActive)
                symbol.Activate();

            double offsetFt  = offsetMm  / 304.8;
            double spacingFt = spacingMm / 304.8;
            double sillFt    = sillMm    / 304.8;

            using var scope = new RevitWriteScope(rctx.Doc, "Duvara Aile Yerleştir", rctx.IsAtomicMode);

            foreach (var el in walls)
            {
                if (el is not Wall wall) continue;
                if (wall.Location is not LocationCurve lc || lc.Curve == null) continue;

                var curve = lc.Curve;
                double wallLenFt = curve.Length;
                if (wallLenFt <= offsetFt) continue; // offset duvardan uzun → atla

                // Yerleştirme mesafeleri: offset'ten başla, spacing aralıklarla ilerle.
                var distances = new List<double>();
                if (spacingFt > 1e-6)
                {
                    for (double d = offsetFt; d <= wallLenFt - 1e-6; d += spacingFt)
                        distances.Add(d);
                }
                else
                {
                    distances.Add(offsetFt); // tek eleman
                }

                // Host duvarın level'ı (yerleştirme yüksekliği için referans)
                var level = GetElementLevel(rctx.Doc, wall);

                int placedHere = 0;
                foreach (var dist in distances)
                {
                    double t = wallLenFt > 0 ? dist / wallLenFt : 0;
                    XYZ pt;
                    try { pt = curve.Evaluate(t, true); }
                    catch { continue; }

                    if (Math.Abs(sillFt) > 1e-9)
                        pt = new XYZ(pt.X, pt.Y, pt.Z + sillFt);

                    try
                    {
                        // Host'lu yerleştirme: family duvara bind edilir (kapı/pencere/aydınlatma vb.)
                        FamilyInstance? inst = level != null
                            ? rctx.Doc.Create.NewFamilyInstance(
                                pt, symbol, wall, level, StructuralType.NonStructural)
                            : rctx.Doc.Create.NewFamilyInstance(
                                pt, symbol, wall, StructuralType.NonStructural);

                        if (inst != null) placedHere++;
                    }
                    catch (Exception ex)
                    {
                        ctx.Log($"  place_family_on_wall: yerleştirme hatası ({Rv.IdStr(wall.Id)}): {ex.Message}");
                    }
                }

                results.Add(new Dictionary<string, object?>
                {
                    ["host_id"]      = Rv.GetId(wall.Id),
                    ["placed_count"] = placedHere,
                    ["wall_length_m"]= Math.Round(wallLenFt * 0.3048, 2),
                });
            }

            scope.Commit();
            int total = results.Sum(r => r.TryGetValue("placed_count", out var v) && v is int c ? c : 0);
            ctx.Log($"  place_family_on_wall: {walls.Count} duvar → {total} aile yerleştirildi.");
            return results;
        }

        // ─────────────────────────────────────────────────────────────────────
        // P06: place_family_on_ceiling   (GERÇEK — face-based + nokta fallback)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("place_family_on_ceiling",
            RequiresTransaction = true,
            Description =
                "Tavan yüzeyine aile yerleştirir (aydınlatma/duman dedektörü/difüzör vb.). Transaction gerektirir.\n" +
                "Face-based aile ise tavan alt yüzüne host'lar; değilse tavan merkez noktasına yerleştirir.\n" +
                "Input: List<Element> (tavanlar). " +
                "params: family_name, type_name (zorunlu), offset_x_mm (default 0), offset_y_mm (default 0).\n" +
                "Çıktı: List<Dictionary> — host_id, placed (bool), mode (face|point|fail).",
            Category    = "Yerleştirme")]
        public static List<Dictionary<string, object?>> PlaceFamilyOnCeiling(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            var ceilings   = ctx.InputAsOrDefault<List<Element>>();
            var familyName = ctx.GetString("family_name");
            var typeName   = ctx.GetString("type_name");
            double offsetXmm = ctx.GetDouble("offset_x_mm", 0);
            double offsetYmm = ctx.GetDouble("offset_y_mm", 0);

            var results = new List<Dictionary<string, object?>>();

            var symbol = FindFamilySymbol(doc, familyName, typeName);
            if (symbol == null)
            {
                results.Add(new Dictionary<string, object?>
                {
                    ["status"]  = "FAMILY_NOT_FOUND",
                    ["message"] = $"'{familyName}/{typeName}' tipi projede bulunamadı."
                });
                ctx.Log($"  place_family_on_ceiling: '{familyName}/{typeName}' tipi bulunamadı → 0 yerleştirme");
                return results;
            }

            if (!symbol.IsActive)
                symbol.Activate();

            double offsetXft = offsetXmm / 304.8;
            double offsetYft = offsetYmm / 304.8;

            // Family face-based mi? (host gerektiren yerleştirme için)
            bool isFaceBased = symbol.Family?.FamilyPlacementType
                               == FamilyPlacementType.WorkPlaneBased;

            // Alt yüzü bulmak için geometri referansları gerekli
            var geomOpts = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine,
            };

            using var scope = new RevitWriteScope(doc, "Tavana Aile Yerleştir", rctx.IsAtomicMode);

            foreach (var el in ceilings)
            {
                if (el is not Ceiling ceiling) continue;

                var bb = ceiling.get_BoundingBox(null);
                if (bb == null) continue;

                double cx = (bb.Min.X + bb.Max.X) / 2.0 + offsetXft;
                double cy = (bb.Min.Y + bb.Max.Y) / 2.0 + offsetYft;

                bool placed = false;
                string mode = "fail";

                // ── 1) Face-based aile → tavanın alt yüzüne host'la ──────────
                if (isFaceBased)
                {
                    var bottomFace = FindBottomFace(ceiling, geomOpts);
                    if (bottomFace != null && bottomFace.Reference != null)
                    {
                        try
                        {
                            // Alt yüz üzerinde merkeze en yakın UV noktasını projeksiyonla
                            var faceCenter = new XYZ(cx, cy, bb.Min.Z);
                            var proj = bottomFace.Project(faceCenter);
                            var facePt = proj?.XYZPoint ?? faceCenter;

                            // Yönelim: face normaline dik bir referans doğrultu (X ekseni)
                            var refDir = XYZ.BasisX;

                            var inst = doc.Create.NewFamilyInstance(
                                bottomFace.Reference, facePt, refDir, symbol);
                            placed = inst != null;
                            mode   = "face";
                        }
                        catch (Exception ex)
                        {
                            ctx.Log($"  place_family_on_ceiling: face yerleştirme hatası ({Rv.IdStr(ceiling.Id)}): {ex.Message}");
                        }
                    }
                }

                // ── 2) Fallback: nokta-bazlı yerleştirme (alt yüz Z'sinde) ───
                if (!placed)
                {
                    try
                    {
                        var pt = new XYZ(cx, cy, bb.Min.Z);
                        var level = GetElementLevel(doc, ceiling);
                        var inst = level != null
                            ? doc.Create.NewFamilyInstance(pt, symbol, level, StructuralType.NonStructural)
                            : doc.Create.NewFamilyInstance(pt, symbol, StructuralType.NonStructural);
                        placed = inst != null;
                        mode   = "point";
                    }
                    catch (Exception ex)
                    {
                        ctx.Log($"  place_family_on_ceiling: nokta yerleştirme hatası ({Rv.IdStr(ceiling.Id)}): {ex.Message}");
                    }
                }

                results.Add(new Dictionary<string, object?>
                {
                    ["host_id"] = Rv.GetId(ceiling.Id),
                    ["placed"]  = placed,
                    ["mode"]    = mode,
                });
            }

            scope.Commit();
            int total = results.Count(r => r.TryGetValue("placed", out var v) && v is true);
            ctx.Log($"  place_family_on_ceiling: {ceilings.Count} tavan → {total} aile yerleştirildi.");
            return results;
        }

        // ─────────────────────────────────────────────────────────────────────
        // P07: place_family_along_mep   (GERÇEK — askı/destek yerleştirme)
        //
        // Boru/kanal/kablo taşıyıcı hatları boyunca, belirtilen aralıkla
        // (spacing_mm) verilen aileyi (askı, destek, etiket vb.) yerleştirir.
        // Manuel family + manuel aralık → manifest ile tam kontrol.
        //
        // Hat boyunca yürüme: her segment LocationCurve'ü uçtan uca,
        // uç boşluğu (end_setback_mm) bırakarak spacing aralıklarla bölünür.
        //
        // input : List<Element>  (Pipe/Duct/CableTray segmentleri)
        // params: family_name      String  zorunlu (askı ailesi)
        //         type_name        String  zorunlu (aile tipi)
        //         spacing_mm       Double  default=2000  (askı arası mesafe)
        //         end_setback_mm   Double  default=300   (hat ucundan boşluk)
        //         vertical_offset_mm Double default=0    (hat altına/üstüne kaydırma)
        //
        // output: List<Dictionary> — host_id, placed_count, spacing_mm, run_length_m
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("place_family_along_mep",
            RequiresTransaction = true,
            Description =
                "Boru/kanal/kablo taşıyıcı hatları boyunca belirtilen aralıkla (spacing_mm) " +
                "verilen aileyi (askı, destek, etiket) yerleştirir. Manuel family + manuel aralık.\n" +
                "Input: List<Element> (MEP hat segmentleri). " +
                "params: family_name, type_name (zorunlu), spacing_mm (default 2000), " +
                "end_setback_mm (default 300), vertical_offset_mm (default 0).",
            Category = "Yerleştirme")]
        public static List<Dictionary<string, object?>> PlaceFamilyAlongMep(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            var runs       = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var familyName = ctx.GetString("family_name", "");
            var typeName   = ctx.GetString("type_name", "");
            double spacingMm   = ctx.GetDouble("spacing_mm",        2000);
            double endSetbackMm= ctx.GetDouble("end_setback_mm",    300);
            double vOffsetMm   = ctx.GetDouble("vertical_offset_mm", 0);

            var results = new List<Dictionary<string, object?>>();

            if (string.IsNullOrWhiteSpace(familyName) || string.IsNullOrWhiteSpace(typeName))
            {
                results.Add(new Dictionary<string, object?>
                {
                    ["status"]  = "PARAM_MISSING",
                    ["message"] = "family_name ve type_name zorunludur."
                });
                return results;
            }

            var symbol = FindFamilySymbol(doc, familyName, typeName);
            if (symbol == null)
            {
                results.Add(new Dictionary<string, object?>
                {
                    ["status"]  = "FAMILY_NOT_FOUND",
                    ["message"] = $"'{familyName}/{typeName}' tipi projede bulunamadı."
                });
                return results;
            }

            if (!symbol.IsActive) symbol.Activate();

            double spacingFt = spacingMm    / 304.8;
            double setbackFt = endSetbackMm / 304.8;
            double vOffsetFt = vOffsetMm    / 304.8;

            if (spacingFt <= 0) spacingFt = 2000 / 304.8;

            using var scope = new RevitWriteScope(doc, "MEP Hattı Boyunca Aile Yerleştir", rctx.IsAtomicMode);

            foreach (var run in runs)
            {
                if (run.Location is not LocationCurve lc || lc.Curve == null) continue;

                var curve   = lc.Curve;
                double lenFt = curve.Length;
                if (lenFt <= 2 * setbackFt) { continue; } // çok kısa segment

                // Yerleştirilebilir bölge: setback'ten setback'e
                double startFt = setbackFt;
                double endFt   = lenFt - setbackFt;
                double usableLen = endFt - startFt;

                int count = Math.Max(1, (int)Math.Floor(usableLen / spacingFt) + 1);

                // Hattın bağlı olduğu level (varsa)
                var level = GetElementLevel(doc, run);

                int placedHere = 0;
                for (int i = 0; i < count; i++)
                {
                    double dist = startFt + i * spacingFt;
                    if (dist > endFt + 1e-6) break;

                    // Eğri üzerinde normalize parametre (0..1)
                    double t = lenFt > 0 ? dist / lenFt : 0;
                    XYZ pt;
                    try { pt = curve.Evaluate(t, true); }
                    catch { continue; }

                    if (Math.Abs(vOffsetFt) > 1e-9)
                        pt = new XYZ(pt.X, pt.Y, pt.Z + vOffsetFt);

                    try
                    {
                        FamilyInstance? inst;
                        if (level != null)
                            inst = doc.Create.NewFamilyInstance(
                                pt, symbol, level, StructuralType.NonStructural);
                        else
                            inst = doc.Create.NewFamilyInstance(
                                pt, symbol, StructuralType.NonStructural);

                        if (inst != null) placedHere++;
                    }
                    catch (Exception ex)
                    {
                        ctx.Log($"  place_family_along_mep: yerleştirme hatası ({Rv.IdStr(run.Id)}): {ex.Message}");
                    }
                }

                results.Add(new Dictionary<string, object?>
                {
                    ["host_id"]      = Rv.GetId(run.Id),
                    ["category"]     = run.Category?.Name ?? "?",
                    ["placed_count"] = placedHere,
                    ["spacing_mm"]   = spacingMm,
                    ["run_length_m"] = Math.Round(lenFt * 0.3048, 2),
                });
            }

            scope.Commit();
            int total = results.Sum(r => r.TryGetValue("placed_count", out var v) && v is int c ? c : 0);
            ctx.Log($"  place_family_along_mep: {runs.Count} hat → {total} aile yerleştirildi.");
            return results;
        }

        // Elemanın bağlı olduğu Level'ı bulur (null olabilir).
        private static Level? GetElementLevel(Document doc, Element el)
        {
            try
            {
                var lvlId = el.LevelId;
                if (lvlId != null && lvlId != ElementId.InvalidElementId)
                    return doc.GetElement(lvlId) as Level;

                // Yedek: RBS_START_LEVEL_PARAM (MEP eğrileri için)
                var p = el.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
                if (p != null && p.AsElementId() != ElementId.InvalidElementId)
                    return doc.GetElement(p.AsElementId()) as Level;
            }
            catch { }
            return null;
        }

        // Tavan (veya benzeri yatay eleman) geometrisinden en ALTtaki yatay
        // PlanarFace'i döner (face-based aile host'lamak için). Bulunamazsa null.
        // Normali aşağı bakan (-Z'ye yakın) ve en düşük Z'li yüz seçilir.
        private static PlanarFace? FindBottomFace(Element el, Options geomOpts)
        {
            try
            {
                var geom = el.get_Geometry(geomOpts);
                if (geom == null) return null;

                PlanarFace? best = null;
                double bestZ = double.MaxValue;

                void ScanSolid(Solid solid)
                {
                    if (solid == null || solid.Faces.Size == 0) return;
                    foreach (Face f in solid.Faces)
                    {
                        if (f is not PlanarFace pf) continue;

                        // Aşağı bakan yüz: normalin Z bileşeni belirgin şekilde negatif
                        if (pf.FaceNormal.Z > -0.7) continue;

                        double z = pf.Origin.Z;
                        if (z < bestZ)
                        {
                            bestZ = z;
                            best  = pf;
                        }
                    }
                }

                foreach (var go in geom)
                {
                    switch (go)
                    {
                        case Solid s:
                            ScanSolid(s);
                            break;
                        case GeometryInstance gi:
                            foreach (var ig in gi.GetInstanceGeometry())
                                if (ig is Solid igs) ScanSolid(igs);
                            break;
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı metodlar
        // ─────────────────────────────────────────────────────────────────────

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

        private static FamilySymbol? FindFamilySymbol(Document doc, string familyName, string typeName)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    s.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                    s.Name.Equals(typeName,          StringComparison.OrdinalIgnoreCase));
    }
}
