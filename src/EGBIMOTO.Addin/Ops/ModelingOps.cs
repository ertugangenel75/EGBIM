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
    /// EGBIMOTO V4 — Modelleme Op'ları  (Tier 1 + Tier 2)
    ///
    /// Tier 1 — Temel yazma:
    ///   set_element_type    — Eleman tipini değiştir
    ///   create_3d_view      — Section box ile 3D view
    ///   place_family        — Serbest nokta family instance
    ///   create_sheet        — Pafta oluştur
    ///   place_view_on_sheet — View'i paftaya ekle
    ///   rename_element      — Eleman adı / mark güncelle
    ///
    /// Tier 2 — Proje yönetimi:
    ///   set_workset         — Workset ata
    ///   set_phase           — Faz ata (oluşturuldu / yıkıldı)
    ///   set_level           — Referans katı değiştir
    ///   mirror_element      — Aynala (X / Y ekseni)
    ///   move_element        — Taşı (dx / dy / dz)
    ///
    /// Tüm op'lar RevitWriteScope kullanır → atomic + normal mod desteği.
    /// </summary>
    public static class ModelingOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // T1-01  set_element_type
        // params: type_name (zorunlu)
        // returns: int (değiştirilen eleman sayısı)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("set_element_type",
            RequiresTransaction = true,
            Description = "Eleman listesinin tipini params.type_name olarak değiştirir",
            Category    = "Modelleme")]
        public static int SetElementType(OpContext ctx)
        {
            var rctx     = RequireRevit(ctx);
            var elements = ctx.InputAs<List<Element>>();
            var typeName = ctx.RequireString("type_name");

            // Önce ilk elemanın kategorisinden tip ara
            var firstCatId = elements.FirstOrDefault()?.Category?.Id;
            ElementType? newType = null;

            if (firstCatId != null)
            {
                newType = new FilteredElementCollector(rctx.Doc)
                    .WhereElementIsElementType()
                    .Cast<ElementType>()
                    .FirstOrDefault(t =>
                        t.Category?.Id == firstCatId &&
                        t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
            }

            // Kategoriden bulunamazsa tüm tiplerde ara
            newType ??= new FilteredElementCollector(rctx.Doc)
                .WhereElementIsElementType()
                .Cast<ElementType>()
                .FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

            if (newType == null)
            {
                ctx.Log($"  set_element_type: '{typeName}' tipi bulunamadı → 0 değişiklik");
                return 0;
            }

            int count = 0;
            using var scope = new RevitWriteScope(rctx.Doc, $"Tip Değiştir → {typeName}", rctx.IsAtomicMode);
            foreach (var el in elements)
            {
                try
                {
                    el.ChangeTypeId(newType.Id);
                    count++;
                }
                catch (Exception ex)
                {
                    ctx.Log($"  set_element_type: {el.Id} değiştirilemedi — {ex.Message}");
                }
            }
            scope.Commit();
            ctx.Log($"  set_element_type: {count}/{elements.Count} eleman → '{typeName}'");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T1-02  create_3d_view
        // input : List<Element> opsiyonel — section box bu elemanlara fit edilir
        // params: view_name, padding_mm (default 500)
        // returns: Element (View3D)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_3d_view",
            RequiresTransaction = true,
            Description = "Eleman listesine fit section box ile 3D view oluşturur",
            Category    = "Modelleme")]
        public static Element Create3dView(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var elements  = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var viewName  = ctx.GetString("view_name", $"EGBIMOTO_3D_{DateTime.Now:HHmmss}");
            double padMm  = ctx.GetDouble("padding_mm", 500);
            double padFt  = padMm / 304.8;

            // ViewFamilyType — 3D
            var vft = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

            if (vft == null)
            {
                ctx.Log("  create_3d_view: 3D ViewFamilyType bulunamadı → null");
                return null!;
            }

            using var scope = new RevitWriteScope(rctx.Doc, $"3D View: {viewName}", rctx.IsAtomicMode);

            var view3d = View3D.CreateIsometric(rctx.Doc, vft.Id);
            view3d.Name = viewName;

            // Section box fit
            if (elements.Count > 0)
            {
                var minX = double.MaxValue; var minY = double.MaxValue; var minZ = double.MaxValue;
                var maxX = double.MinValue; var maxY = double.MinValue; var maxZ = double.MinValue;

                foreach (var el in elements)
                {
                    var bb = el.get_BoundingBox(null);
                    if (bb == null) continue;
                    if (bb.Min.X < minX) minX = bb.Min.X;
                    if (bb.Min.Y < minY) minY = bb.Min.Y;
                    if (bb.Min.Z < minZ) minZ = bb.Min.Z;
                    if (bb.Max.X > maxX) maxX = bb.Max.X;
                    if (bb.Max.Y > maxY) maxY = bb.Max.Y;
                    if (bb.Max.Z > maxZ) maxZ = bb.Max.Z;
                }

                var sectionBox = new BoundingBoxXYZ
                {
                    Min = new XYZ(minX - padFt, minY - padFt, minZ - padFt),
                    Max = new XYZ(maxX + padFt, maxY + padFt, maxZ + padFt)
                };
                view3d.SetSectionBox(sectionBox);
                view3d.IsSectionBoxActive = true;
            }

            scope.Commit();
            ctx.Log($"  create_3d_view: '{viewName}' oluşturuldu (section box: {elements.Count > 0})");
            return view3d;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T1-03  place_family
        // params: family_name, type_name, x_mm, y_mm, z_mm,
        //         level_name, rotation_deg (default 0)
        // returns: int (yerleştirilen adet)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("place_family",
            RequiresTransaction = true,
            Description = "Serbest noktaya level-based family instance yerleştirir",
            Category    = "Modelleme")]
        public static int PlaceFamily(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var familyName = ctx.RequireString("family_name");
            var typeName   = ctx.RequireString("type_name");
            var levelName  = ctx.RequireString("level_name");
            double xFt     = ctx.GetDouble("x_mm", 0) / 304.8;
            double yFt     = ctx.GetDouble("y_mm", 0) / 304.8;
            double zFt     = ctx.GetDouble("z_mm", 0) / 304.8;
            double rotDeg  = ctx.GetDouble("rotation_deg", 0);

            var symbol = FindFamilySymbol(rctx.Doc, familyName, typeName);
            if (symbol == null)
            {
                ctx.Log($"  place_family: '{familyName}/{typeName}' bulunamadı → 0");
                return 0;
            }

            var level = FindLevel(rctx.Doc, levelName);
            if (level == null)
            {
                ctx.Log($"  place_family: '{levelName}' katı bulunamadı → 0");
                return 0;
            }

            if (!symbol.IsActive) symbol.Activate();

            using var scope = new RevitWriteScope(rctx.Doc, $"Family Yerleştir: {familyName}", rctx.IsAtomicMode);

            var point    = new XYZ(xFt, yFt, zFt);
            var instance = rctx.Doc.Create.NewFamilyInstance(
                point, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            // Döndürme
            if (Math.Abs(rotDeg) > 0.001)
            {
                var axis = Line.CreateBound(point, point + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(
                    rctx.Doc, instance.Id, axis, rotDeg * Math.PI / 180.0);
            }

            scope.Commit();
            ctx.Log($"  place_family: '{familyName}/{typeName}' → ({xFt:F2},{yFt:F2},{zFt:F2}) ft");
            return 1;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T1-04  create_sheet
        // params: sheet_number, sheet_name, title_block_name (opsiyonel)
        // returns: Element (ViewSheet)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("create_sheet",
            RequiresTransaction = true,
            Description = "Yeni pafta (ViewSheet) oluşturur",
            Category    = "Modelleme")]
        public static Element CreateSheet(OpContext ctx)
        {
            var rctx           = RequireRevit(ctx);
            var sheetNumber    = ctx.RequireString("sheet_number");
            var sheetName      = ctx.RequireString("sheet_name");
            var titleBlockName = ctx.GetString("title_block_name", "");

            // Title block bul (yoksa InvalidElementId — boş pafta)
            var titleBlockId = ElementId.InvalidElementId;
            if (!string.IsNullOrEmpty(titleBlockName))
            {
                var tb = new FilteredElementCollector(rctx.Doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsElementType()
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.FamilyName.Equals(titleBlockName, StringComparison.OrdinalIgnoreCase)
                                     || s.Name.Equals(titleBlockName,        StringComparison.OrdinalIgnoreCase));
                if (tb != null) titleBlockId = tb.Id;
            }

            using var scope = new RevitWriteScope(rctx.Doc, $"Pafta: {sheetNumber}", rctx.IsAtomicMode);
            var sheet = ViewSheet.Create(rctx.Doc, titleBlockId);
            sheet.SheetNumber = sheetNumber;
            sheet.Name        = sheetName;

            scope.Commit();
            ctx.Log($"  create_sheet: '{sheetNumber} - {sheetName}' oluşturuldu");
            return sheet;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T1-05  place_view_on_sheet
        // input : List<Element> (View'ler)
        // params: sheet_number, x_mm (default 0), y_mm (default 0)
        // returns: int (eklenen viewport sayısı)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("place_view_on_sheet",
            RequiresTransaction = true,
            Description = "View listesini params.sheet_number paftasına Viewport olarak ekler",
            Category    = "Modelleme")]
        public static int PlaceViewOnSheet(OpContext ctx)
        {
            var rctx        = RequireRevit(ctx);
            var views       = ctx.InputAs<List<Element>>();
            var sheetNumber = ctx.RequireString("sheet_number");
            double xFt      = ctx.GetDouble("x_mm", 0) / 304.8;
            double yFt      = ctx.GetDouble("y_mm", 0) / 304.8;

            var sheet = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .FirstOrDefault(s => s.SheetNumber.Equals(sheetNumber, StringComparison.OrdinalIgnoreCase));

            if (sheet == null)
            {
                ctx.Log($"  place_view_on_sheet: '{sheetNumber}' paftası bulunamadı → 0");
                return 0;
            }

            int count = 0;
            double offsetFt = 0; // çakışmayı önlemek için basit y-offset

            using var scope = new RevitWriteScope(rctx.Doc, $"View → Pafta: {sheetNumber}", rctx.IsAtomicMode);
            foreach (var el in views)
            {
                if (el is not View view) continue;
                if (!Viewport.CanAddViewToSheet(rctx.Doc, sheet.Id, view.Id))
                {
                    ctx.Log($"  place_view_on_sheet: '{view.Name}' zaten bir paftada");
                    continue;
                }
                var point = new XYZ(xFt, yFt + offsetFt, 0);
                Viewport.Create(rctx.Doc, sheet.Id, view.Id, point);
                offsetFt += 0.5; // her view için 150mm aşağı
                count++;
            }
            scope.Commit();
            ctx.Log($"  place_view_on_sheet: {count} view → '{sheetNumber}'");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T1-06  rename_element
        // input : List<Element>
        // params: param_name (default "Name"), value | prefix | suffix
        // returns: int
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("rename_element",
            RequiresTransaction = true,
            Description = "Eleman listesinin params.param_name alanını günceller (value/prefix/suffix)",
            Category    = "Modelleme")]
        public static int RenameElement(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var elements  = ctx.InputAs<List<Element>>();
            var paramName = ctx.GetString("param_name", "Name");
            var value     = ctx.GetString("value",  "");
            var prefix    = ctx.GetString("prefix", "");
            var suffix    = ctx.GetString("suffix", "");

            int count = 0;
            using var scope = new RevitWriteScope(rctx.Doc, $"Yeniden Adlandır", rctx.IsAtomicMode);

            foreach (var el in elements)
            {
                try
                {
                    string newVal;
                    if (!string.IsNullOrEmpty(value))
                    {
                        newVal = value;
                    }
                    else
                    {
                        // Mevcut değeri oku
                        string current = "";
                        if (paramName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                            current = el.Name;
                        else
                        {
                            var p = el.LookupParameter(paramName);
                            current = p?.AsString() ?? "";
                        }
                        newVal = prefix + current + suffix;
                    }

                    // Yaz
                    if (paramName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        el.Name = newVal;
                    }
                    else
                    {
                        var p = el.LookupParameter(paramName);
                        if (p != null && !p.IsReadOnly)
                            p.Set(newVal);
                        else continue;
                    }
                    count++;
                }
                catch (Exception ex)
                {
                    ctx.Log($"  rename_element: {el.Id} — {ex.Message}");
                }
            }

            scope.Commit();
            ctx.Log($"  rename_element: {count}/{elements.Count} eleman güncellendi");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T2-01  set_workset
        // input : List<Element>
        // params: workset_name (zorunlu)
        // ─────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────
        // workset_by_level  (Way-Tools WorksetByLevel mantığı — C# port)
        // Her kat için aynı isimde workset oluşturur, varsa atlar.
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("workset_by_level",
            RequiresTransaction = true,
            Description =
                "Her kat (Level) için aynı isimde User Workset oluşturur.\n" +
                "Zaten varsa atlar. Model workshared değilse hata döner.\n" +
                "Way-Tools WorksetByLevel.py mantığından C# portudur.\n\n" +
                "params:\n" +
                "  prefix  — workset adı öneki (opsiyonel). Örn: 'KAT-' → 'KAT-Zemin'\n" +
                "  suffix  — workset adı soneki (opsiyonel)\n" +
                "  dry_run — true ise workset oluşturmaz, sadece listeler (default: false)\n\n" +
                "Input: yok (tüm katları otomatik alır).\n" +
                "Çıktı: List<Dictionary> — her satır bir workset işlemini temsil eder\n" +
                "  workset_adi, durum (OLUŞTURULDU / ZATEN_VAR / DRY_RUN), kat_yuksekligi_m",
            Category = "Modelleme")]
        public static List<Dictionary<string, object?>> WorksetByLevel(OpContext ctx)
        {
            var rctx   = RequireRevit(ctx);
            var doc    = rctx.Doc;
            var prefix = ctx.GetString("prefix", "");
            var suffix = ctx.GetString("suffix", "");
            bool dryRun = ctx.GetBool("dry_run", false);

            if (!doc.IsWorkshared)
                throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] workset_by_level: Model workshared değil. " +
                    "Önce 'Collaborate → Worksets' ile worksharing etkinleştirin.");

            // Mevcut User Workset adlarını topla
            var mevcutWorksetler = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets()
                .Select(w => w.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Tüm katları topla
            var katlar = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            var sonuclar = new List<Dictionary<string, object?>>();

            using var tx = dryRun
                ? null
                : new Transaction(doc, "EGBIMOTO: Kat Bazlı Workset Oluştur");

            if (!dryRun) tx!.Start();

            foreach (var kat in katlar)
            {
                var wsAdi = prefix + kat.Name + suffix;
                double yukseklikM = Math.Round(
                    UnitUtils.ConvertFromInternalUnits(kat.Elevation, UnitTypeId.Meters), 2);

                if (mevcutWorksetler.Contains(wsAdi))
                {
                    sonuclar.Add(new Dictionary<string, object?>
                    {
                        ["workset_adi"]       = wsAdi,
                        ["durum"]             = "ZATEN_VAR",
                        ["kat_yuksekligi_m"]  = yukseklikM,
                    });
                    continue;
                }

                if (dryRun)
                {
                    sonuclar.Add(new Dictionary<string, object?>
                    {
                        ["workset_adi"]      = wsAdi,
                        ["durum"]            = "DRY_RUN",
                        ["kat_yuksekligi_m"] = yukseklikM,
                    });
                    continue;
                }

                Workset.Create(doc, wsAdi);
                mevcutWorksetler.Add(wsAdi); // cache güncelle
                sonuclar.Add(new Dictionary<string, object?>
                {
                    ["workset_adi"]      = wsAdi,
                    ["durum"]            = "OLUŞTURULDU",
                    ["kat_yuksekligi_m"] = yukseklikM,
                });
            }

            if (!dryRun) tx!.Commit();

            int yeni    = sonuclar.Count(r => r["durum"]?.ToString() == "OLUŞTURULDU");
            int mevcut  = sonuclar.Count(r => r["durum"]?.ToString() == "ZATEN_VAR");
            ctx.Log($"  workset_by_level: {yeni} yeni oluşturuldu, {mevcut} zaten vardı ({katlar.Count} kat)");
            return sonuclar;
        }

        [EgOp("set_workset",
            RequiresTransaction = true,
            Description = "Eleman listesine params.workset_name workset'ini atar",
            Category    = "Modelleme")]
        public static int SetWorkset(OpContext ctx)
        {
            var rctx        = RequireRevit(ctx);
            var elements    = ctx.InputAs<List<Element>>();
            var worksetName = ctx.RequireString("workset_name");

            if (!rctx.Doc.IsWorkshared)
            {
                ctx.Log("  set_workset: Model workshared değil → atlandı");
                return 0;
            }

            // Workset bul
            var wsTable  = rctx.Doc.GetWorksetTable();
            var worksets = new FilteredWorksetCollector(rctx.Doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets();
            var target = worksets.FirstOrDefault(w =>
                w.Name.Equals(worksetName, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                ctx.Log($"  set_workset: '{worksetName}' bulunamadı → 0");
                return 0;
            }

            int count = 0;
            using var scope = new RevitWriteScope(rctx.Doc, $"Workset: {worksetName}", rctx.IsAtomicMode);
            foreach (var el in elements)
            {
                var p = el.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (p == null || p.IsReadOnly) continue;
                Rv.SetWorksetParam(p, target.Id);  // v6: Rv adapter
                count++;
            }
            scope.Commit();
            ctx.Log($"  set_workset: {count} eleman → '{worksetName}'");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T2-02  set_phase
        // input : List<Element>
        // params: phase_name (zorunlu), phase_type = "created" | "demolished"
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("set_phase",
            RequiresTransaction = true,
            Description = "Elemanların faz oluşturuldu/yıkıldı parametresini atar",
            Category    = "Modelleme")]
        public static int SetPhase(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var elements  = ctx.InputAs<List<Element>>();
            var phaseName = ctx.RequireString("phase_name");
            var phaseType = ctx.GetString("phase_type", "created").ToLowerInvariant();

            var phase = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .FirstOrDefault(p => p.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase));

            if (phase == null)
            {
                ctx.Log($"  set_phase: '{phaseName}' fazı bulunamadı → 0");
                return 0;
            }

            var bip = phaseType == "demolished"
                ? BuiltInParameter.PHASE_DEMOLISHED
                : BuiltInParameter.PHASE_CREATED;

            int count = 0;
            using var scope = new RevitWriteScope(rctx.Doc, $"Faz: {phaseName}", rctx.IsAtomicMode);
            foreach (var el in elements)
            {
                var p = el.get_Parameter(bip);
                if (p == null || p.IsReadOnly) continue;
                p.Set(phase.Id);
                count++;
            }
            scope.Commit();
            ctx.Log($"  set_phase: {count} eleman → '{phaseName}' ({phaseType})");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T2-03  set_level
        // input : List<Element>
        // params: level_name (zorunlu)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("set_level",
            RequiresTransaction = true,
            Description = "Eleman listesinin referans katını params.level_name olarak değiştirir",
            Category    = "Modelleme")]
        public static int SetLevel(OpContext ctx)
        {
            var rctx      = RequireRevit(ctx);
            var elements  = ctx.InputAs<List<Element>>();
            var levelName = ctx.RequireString("level_name");

            var level = FindLevel(rctx.Doc, levelName);
            if (level == null)
            {
                ctx.Log($"  set_level: '{levelName}' bulunamadı → 0");
                return 0;
            }

            int count = 0;
            using var scope = new RevitWriteScope(rctx.Doc, $"Kat: {levelName}", rctx.IsAtomicMode);
            foreach (var el in elements)
            {
                // Deneme sırasıyla: FAMILY_LEVEL_PARAM, ROOM_LEVEL_ID, WALL_BASE_CONSTRAINT
                var bips = new[]
                {
                    BuiltInParameter.FAMILY_LEVEL_PARAM,
                    BuiltInParameter.ROOM_LEVEL_ID,
                    BuiltInParameter.WALL_BASE_CONSTRAINT,
                    BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM,
                };
                bool set = false;
                foreach (var bip in bips)
                {
                    var p = el.get_Parameter(bip);
                    if (p == null || p.IsReadOnly) continue;
                    p.Set(level.Id);
                    set = true;
                    break;
                }
                if (set) count++;
            }
            scope.Commit();
            ctx.Log($"  set_level: {count} eleman → '{levelName}'");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T2-04  mirror_element
        // input : List<Element>
        // params: axis = "X" | "Y" | "line",
        //         pivot_x_mm, pivot_y_mm (merkez nokta)
        //         copy = false (true → kopyalayarak aynala)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("mirror_element",
            RequiresTransaction = true,
            Description = "Eleman listesini X veya Y eksenine göre aynalar",
            Category    = "Modelleme")]
        public static int MirrorElement(OpContext ctx)
        {
            var rctx    = RequireRevit(ctx);
            var elements= ctx.InputAs<List<Element>>();
            var axis    = ctx.GetString("axis", "Y").ToUpperInvariant();
            double px   = ctx.GetDouble("pivot_x_mm", 0) / 304.8;
            double py   = ctx.GetDouble("pivot_y_mm", 0) / 304.8;
            bool copy   = ctx.GetBool("copy", false);

            var pivot = new XYZ(px, py, 0);
            Plane mirrorPlane = axis switch
            {
                "X" => Plane.CreateByNormalAndOrigin(XYZ.BasisY, pivot),
                "Y" => Plane.CreateByNormalAndOrigin(XYZ.BasisX, pivot),
                _   => Plane.CreateByNormalAndOrigin(XYZ.BasisX, pivot),
            };

            var ids = elements.Select(e => e.Id).ToList();
            using var scope = new RevitWriteScope(rctx.Doc, $"Aynala ({axis})", rctx.IsAtomicMode);

            if (copy)
                ElementTransformUtils.MirrorElements(rctx.Doc, ids, mirrorPlane, true);
            else
                ElementTransformUtils.MirrorElements(rctx.Doc, ids, mirrorPlane, false);

            scope.Commit();
            ctx.Log($"  mirror_element: {ids.Count} eleman aynalanadı (axis={axis}, copy={copy})");
            return ids.Count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T2-05  move_element
        // input : List<Element>
        // params: dx_mm, dy_mm, dz_mm
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("move_element",
            RequiresTransaction = true,
            Description = "Eleman listesini dx/dy/dz_mm kadar taşır",
            Category    = "Modelleme")]
        public static int MoveElement(OpContext ctx)
        {
            var rctx    = RequireRevit(ctx);
            var elements= ctx.InputAs<List<Element>>();
            double dxFt = ctx.GetDouble("dx_mm", 0) / 304.8;
            double dyFt = ctx.GetDouble("dy_mm", 0) / 304.8;
            double dzFt = ctx.GetDouble("dz_mm", 0) / 304.8;

            var translation = new XYZ(dxFt, dyFt, dzFt);
            var ids         = elements.Select(e => e.Id).ToList();

            using var scope = new RevitWriteScope(rctx.Doc, "Taşı", rctx.IsAtomicMode);
            ElementTransformUtils.MoveElements(rctx.Doc, ids, translation);
            scope.Commit();

            ctx.Log($"  move_element: {ids.Count} eleman taşındı ({dxFt*304.8:F0},{dyFt*304.8:F0},{dzFt*304.8:F0} mm)");
            return ids.Count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı
        // ─────────────────────────────────────────────────────────────────────
        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");

        internal static FamilySymbol? FindFamilySymbol(Document doc, string familyName, string typeName)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    s.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                    s.Name.Equals(typeName,          StringComparison.OrdinalIgnoreCase));

        internal static Level? FindLevel(Document doc, string levelName)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
    }
}
