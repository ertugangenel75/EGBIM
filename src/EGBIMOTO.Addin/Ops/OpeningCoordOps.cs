using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO MEP Koordinasyon Operasyonları — v9
    ///
    /// BIMaestro ReservationAutoV3 + SmartCheck mantığından türetilmiş,
    /// EGBIMOTO op paradigmasına uyarlanmıştır.
    ///
    /// Üç geliştirme (RevitPlugins-master'dan alınan):
    ///   1. DiameterRange tablosu: MEP çapına göre sleeve/opening boyutu seçimi
    ///   2. ElementIntersectsSolidFilter: iki kademeli filtre (BBox → Solid)
    ///   3. use_solid_intersection param: performans/kesinlik dengesi
    ///
    /// İki temel op:
    ///
    ///   smart_check_mep_no_opening
    ///     → MEP elemanlarının yapısal eleman geçişlerinde boşluk
    ///       aile örneği olmayan kesişimleri tespit eder.
    ///     → Transaction gerektirmez. Sadece analiz.
    ///
    ///   place_opening
    ///     → Tespit edilen kesişim noktalarına boşluk aile örneği yerleştirir.
    ///     → Boyutları otomatik hesaplar (MEP bounding box + offset).
    ///     → Transaction gerektirir.
    ///
    /// Tipik DAG:
    ///   collect_walls + collect_pipes
    ///     → smart_check_mep_no_opening   (kesişim tespiti)
    ///     → preview_gate
    ///     → place_opening                (boşluk yerleştir)
    ///     → export_validation_report
    /// </summary>
    public static class OpeningCoordOps
    {
        // ── Tolerans sabitleri ────────────────────────────────────────────────

        /// <summary>Kesişim tespiti için BoundingBox genişletme payı (ft).</summary>
        private const double INTERSECT_TOLERANCE_FT = 0.05; // ~15 mm

        /// <summary>Boşluk boyutuna eklenen kenar payı (ft).</summary>
        private const double OPENING_OFFSET_FT = 0.164; // ~50 mm

        // ─────────────────────────────────────────────────────────────────────
        // OP 1: smart_check_mep_no_opening
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("smart_check_mep_no_opening",
            Description =
                "MEP elemanlarının yapısal eleman (duvar/döşeme/kiriş) geçişlerini tarar.\n" +
                "Her kesişimde boşluk aile örneği var mı kontrol eder.\n" +
                "Yoksa → bulgu üretir (EGBIMOTO koordinasyon hatası).\n\n" +
                "params:\n" +
                "  host_categories  — kontrol edilecek yapısal kategoriler\n" +
                "                     (opsiyonel, default: [Walls, Floors, StructuralFraming])\n" +
                "  mep_categories   — kontrol edilecek MEP kategoriler\n" +
                "                     (opsiyonel, default: [Pipes, Ducts, CableTray, Conduit])\n" +
                "  tolerance_mm          — kesişim toleransı mm (opsiyonel, default: 15)\n" +
                "  check_opening         — boşluk kontrolü de yapılsın mı (opsiyonel, default: true)\n" +
                "  use_solid_intersection— Solid kesişim testi (kesin ama yavaş, default: false)\n" +
                "  scan_linked_models   — linked modellerdeki MEP'leri de tara (default: false)\n\n" +
                "Input: yok (tüm modeli tarar) veya List<Element> MEP elemanları.\n" +
                "Çıktı: List<Dictionary> — her satır bir kesişim bulgusunu temsil eder.\n" +
                "  element_id, element_category, host_id, host_category,\n" +
                "  kesisim_nokta_x/y/z, boyut_b_mm, boyut_h_mm,\n" +
                "  opening_var, sorun, seviye",
            Category = "Koordinasyon")]
        public static List<Dictionary<string, object?>> SmartCheckMepNoOpening(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] smart_check_mep_no_opening Revit bağlamı gerektirir.");
            var doc = rctx.Doc;

            double toleranceFt = ctx.GetDouble("tolerance_mm", 15.0) / 304.8;

            // ── Yapısal host kategorileri ─────────────────────────────────────
            var hostCatNames  = ctx.RequireList<string>("host_categories");
            var hostBuiltIn   = ResolveHostCategories(hostCatNames);

            // ── MEP kategorileri ──────────────────────────────────────────────
            var mepCatNames   = ctx.RequireList<string>("mep_categories");
            var mepBuiltIn    = ResolveMepCategories(mepCatNames);

            bool checkOpening      = ctx.GetBool("check_opening", true);
            bool useSolidIntersect = ctx.GetBool("use_solid_intersection", false);
            bool scanLinked        = ctx.GetBool("scan_linked_models",     false);

            // ── Elemanları topla ──────────────────────────────────────────────
            var hostElements = CollectByCategories(doc, hostBuiltIn);
            // Yerel MEP elemanları
            var mepElements = ctx.Input as List<Element>
                              ?? CollectByCategories(doc, mepBuiltIn);

            // Linked model MEP elemanları (scan_linked_models=true ise)
            var linkedMepItems = new List<(Element Mep, Transform Transform)>();
            if (scanLinked)
            {
                var links = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var link in links)
                {
                    var linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;

                    var linkTransform = link.GetTotalTransform();
                    var linkedMep = CollectByCategories(linkDoc, mepBuiltIn);
                    foreach (var m in linkedMep)
                        linkedMepItems.Add((m, linkTransform));
                }
                ctx.Log($"  → {linkedMepItems.Count} linked MEP eleman bulundu ({links.Count} link)");
            }

            ctx.Log($"  → {hostElements.Count} host, {mepElements.Count} yerel MEP" +
                    (scanLinked ? $" + {linkedMepItems.Count} linked MEP taranıyor" : " taranıyor"));

            // ── Mevcut boşluk örneklerini hazırla ────────────────────────────
            var existingOpenings = checkOpening
                ? CollectOpeningFamilyInstances(doc)
                : new List<FamilyInstance>();

            ctx.Log($"  → {existingOpenings.Count} mevcut boşluk örneği referans alındı");

            var bulgular = new List<Dictionary<string, object?>>();

            // ── Ana kesişim döngüsü ───────────────────────────────────────────
            foreach (var mep in mepElements)
            {
                var mepBb = mep.get_BoundingBox(null);
                if (mepBb == null) continue;

                // MEP BoundingBox'ını tolerance kadar genişlet
                var mepMin = mepBb.Min - new XYZ(toleranceFt, toleranceFt, toleranceFt);
                var mepMax = mepBb.Max + new XYZ(toleranceFt, toleranceFt, toleranceFt);

                foreach (var host in hostElements)
                {
                    var hostBb = host.get_BoundingBox(null);
                    if (hostBb == null) continue;

                    // BoundingBox çakışma testi
                    if (!BbIntersects(mepMin, mepMax, hostBb.Min, hostBb.Max))
                        continue;

                    // İki kademeli filtre (RevitPlugins-master / RevitFinishing pattern):
                    //   1. BoundingBox overlap (hızlı ön eleme — her zaman çalışır)
                    //   2. ElementIntersectsSolidFilter (kesin — use_solid_intersection=true ise)
                    var intersection = ComputeIntersectionPoint(mep, host, doc);
                    if (intersection == null) continue;

                    // Solid doğrulama (opsiyonel, yanlış pozitif düşürür)
                    if (useSolidIntersect && !SolidIntersects(mep, host, doc))
                        continue;

                    // Boyut hesabı
                    var (bMm, hMm) = ComputeOpeningSize(mep, host);

                    // Boşluk var mı kontrolü
                    bool openingVar = false;
                    if (checkOpening)
                    {
                        openingVar = existingOpenings.Any(op =>
                        {
                            var opBb = op.get_BoundingBox(null);
                            if (opBb == null) return false;
                            var opCenter = (opBb.Min + opBb.Max) / 2;
                            return opCenter.DistanceTo(intersection) < 1.0; // ~30 cm tolerans
                        });
                    }

                    if (!openingVar || !checkOpening)
                    {
                        bulgular.Add(new Dictionary<string, object?>
                        {
                            ["element_id"]       = mep.Id.Value,
                            ["element_category"] = mep.Category?.Name ?? "?",
                            ["element_sistem"]   = GetSystemName(mep),
                            ["host_id"]          = host.Id.Value,
                            ["host_category"]    = host.Category?.Name ?? "?",
                            ["kesisim_x_m"]      = Math.Round(intersection.X * 0.3048, 3),
                            ["kesisim_y_m"]      = Math.Round(intersection.Y * 0.3048, 3),
                            ["kesisim_z_m"]      = Math.Round(intersection.Z * 0.3048, 3),
                            ["boyut_b_mm"]       = Math.Round(bMm, 0),
                            ["boyut_h_mm"]       = Math.Round(hMm, 0),
                            ["opening_var"]      = openingVar,
                            ["sorun"]            = openingVar
                                ? "Boşluk mevcut ✓"
                                : "Geçişte boşluk aile örneği YOK",
                            ["seviye"]           = openingVar ? "BILGI" : "HATA",
                        });
                    }
                }
            }

            // ── Linked model MEP döngüsü ──────────────────────────────────────────
            if (scanLinked && linkedMepItems.Any())
            {
                foreach (var (mep, mepXform) in linkedMepItems)
                {
                    var mepBb = mep.get_BoundingBox(null);
                    if (mepBb == null) continue;

                    foreach (var host in hostElements)
                    {
                        var hostBb = host.get_BoundingBox(null);
                        if (hostBb == null) continue;

                        // 8 köşe dönüşümlü BoundingBox testi (linked model için kritik)
                        if (!BbIntersectsTransformed(mepBb, mepXform, hostBb, toleranceFt))
                            continue;

                        // Solid doğrulama (opsiyonel)
                        if (useSolidIntersect && !SolidIntersectsLinked(mep, mepXform, host, doc))
                            continue;

                        // Kesişim noktası (transform uygulanmış)
                        var mepCenter = mepXform.OfPoint((mepBb.Min + mepBb.Max) / 2);
                        var hostCenter = (hostBb.Min + hostBb.Max) / 2;
                        var intersection = (mepCenter + hostCenter) / 2;

                        // Boşluk var mı?
                        bool openingVar = false;
                        if (checkOpening)
                        {
                            openingVar = existingOpenings.Any(op =>
                            {
                                var opBb = op.get_BoundingBox(null);
                                if (opBb == null) return false;
                                return ((opBb.Min + opBb.Max) / 2).DistanceTo(intersection) < 1.0;
                            });
                        }

                        if (!openingVar || !checkOpening)
                        {
                            var (bMm, hMm) = ComputeOpeningSize(mep, host);
                            bulgular.Add(new Dictionary<string, object?>
                            {
                                ["element_id"]       = mep.Id.Value,
                                ["element_category"] = mep.Category?.Name ?? "?",
                                ["element_sistem"]   = GetSystemName(mep),
                                ["kaynak"]           = "LINKED",
                                ["host_id"]          = host.Id.Value,
                                ["host_category"]    = host.Category?.Name ?? "?",
                                ["kesisim_x_m"]      = Math.Round(intersection.X * 0.3048, 3),
                                ["kesisim_y_m"]      = Math.Round(intersection.Y * 0.3048, 3),
                                ["kesisim_z_m"]      = Math.Round(intersection.Z * 0.3048, 3),
                                ["boyut_b_mm"]       = Math.Round(bMm, 0),
                                ["boyut_h_mm"]       = Math.Round(hMm, 0),
                                ["opening_var"]      = openingVar,
                                ["sorun"]            = openingVar
                                    ? "Boşluk mevcut ✓"
                                    : "Linked MEP geçişinde boşluk YOK",
                                ["seviye"]           = openingVar ? "BILGI" : "HATA",
                            });
                        }
                    }
                }
            }

            int hata   = bulgular.Count(b => b["seviye"]?.ToString() == "HATA");
            int toplam = bulgular.Count;
            ctx.Log($"  → {toplam} kesişim: {hata} boşluksuz geçiş tespit edildi");
            return bulgular;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2: place_opening
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("place_opening",
            Description =
                "smart_check_mep_no_opening bulgularına göre boşluk aile örnekleri yerleştirir.\n" +
                "Boyutları otomatik hesaplar (MEP bbox + offset_mm payı).\n\n" +
                "params:\n" +
                "  family_name   — boşluk aile adı (zorunlu)\n" +
                "                  Örn: 'EG_Rezervasyon_Dikdortgen' veya 'Generic Opening'\n" +
                "  type_name     — tip adı (opsiyonel, default: 'Standard')\n" +
                "  offset_mm     — boyut payı mm (opsiyonel, default: 50)\n" +
                "  param_b       — genişlik parametre adı (opsiyonel, default: 'Width')\n" +
                "  param_h       — yükseklik parametre adı (opsiyonel, default: 'Height')\n" +
                "  param_sistem  — sistem adı parametre adı (opsiyonel, default: 'EG_Sistem')\n" +
                "  skip_existing — mevcut boşluk yakınındakileri atla (opsiyonel, default: true)\n" +
                "  use_diameter_table — true ise çap tablosundan opening boyutu seçer (opsiyonel, default: false)\n" +
                "  dry_run            — true ise yerleştirme yapmaz, sadece sayar (opsiyonel, default: false)\n\n" +
                "Input: smart_check_mep_no_opening çıktısı (List<Dictionary>).\n" +
                "Çıktı: Dictionary — yerlestirilen, atlanan, hata, family_name.",
            Category = "Koordinasyon",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> PlaceOpening(OpContext ctx)
        {
            var rctx       = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] place_opening Revit bağlamı gerektirir.");
            var doc        = rctx.Doc;
            var bulgular   = ctx.InputAs<List<Dictionary<string, object?>>>();

            var familyName  = ctx.RequireString("family_name");
            var typeName    = ctx.GetString("type_name",    "Standard");
            double offsetMm = ctx.GetDouble("offset_mm",   50.0);
            var paramB      = ctx.GetString("param_b",     "Width");
            var paramH      = ctx.GetString("param_h",     "Height");
            var paramSistem = ctx.GetString("param_sistem","EG_Sistem");
            bool skipExist  = ctx.GetBool("skip_existing", true);
            bool dryRun          = ctx.GetBool("dry_run",             false);
            bool useDiamTable    = ctx.GetBool("use_diameter_table", false);

            double offsetFt = offsetMm / 304.8;

            // ── FamilySymbol bul ──────────────────────────────────────────────
            var symbol = FindSymbol(doc, familyName, typeName);
            if (symbol == null)
            {
                ctx.Log($"  ✗ Aile/tip bulunamadı: '{familyName}' / '{typeName}'");
                ctx.Log($"    Projede yüklü aileleri kontrol edin veya family_batch_load op'u kullanın.");
                return new Dictionary<string, object?>
                {
                    ["yerlestirilen"] = 0, ["atlanan"] = 0, ["hata"] = bulgular.Count,
                    ["family_name"]   = familyName,
                    ["hata_mesaji"]   = $"'{familyName}/{typeName}' aile tipi bulunamadı",
                };
            }

            if (!symbol.IsActive)
                symbol.Activate();

            // ── Sadece "HATA" seviyeli bulguları işle ────────────────────────
            var hedefler = bulgular
                .Where(b => b.TryGetValue("seviye", out var s) &&
                            string.Equals(s?.ToString(), "HATA", StringComparison.OrdinalIgnoreCase))
                .ToList();

            ctx.Log($"  → {hedefler.Count} boşluksuz geçiş için yerleştirme yapılacak" +
                    (dryRun ? " [DRY RUN]" : ""));

            int yerlestirilen = 0, atlanan = 0, hata = 0;

            // ── Mevcut boşluklar (skip_existing için) ────────────────────────
            var mevcutlar = skipExist
                ? CollectOpeningFamilyInstances(doc)
                : new List<FamilyInstance>();

            // ── Varsayılan level ──────────────────────────────────────────────
            var defaultLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();

            if (defaultLevel == null)
            {
                ctx.Log($"  ✗ Projede level bulunamadı — yerleştirme iptal");
                return new Dictionary<string, object?>
                    { ["yerlestirilen"] = 0, ["atlanan"] = 0, ["hata"] = hedefler.Count,
                      ["hata_mesaji"] = "Projede Level bulunamadı" };
            }

            using var tx = new Transaction(doc, "EGBIMOTO: Boşluk Yerleştir");
            if (!dryRun) tx.Start();

            foreach (var bulgu in hedefler)
            {
                try
                {
                    // Kesişim noktasını al (metre → feet)
                    double xM = Convert.ToDouble(bulgu.GetValueOrDefault("kesisim_x_m") ?? 0);
                    double yM = Convert.ToDouble(bulgu.GetValueOrDefault("kesisim_y_m") ?? 0);
                    double zM = Convert.ToDouble(bulgu.GetValueOrDefault("kesisim_z_m") ?? 0);
                    var point = new XYZ(xM / 0.3048, yM / 0.3048, zM / 0.3048);

                    // skip_existing kontrolü
                    if (skipExist && mevcutlar.Any(op =>
                    {
                        var bb = op.get_BoundingBox(null);
                        if (bb == null) return false;
                        return ((bb.Min + bb.Max) / 2).DistanceTo(point) < 1.0;
                    }))
                    {
                        atlanan++;
                        continue;
                    }

                    // Boyutlar — tablo modu veya ham boyut
                    double rawBMm = Convert.ToDouble(bulgu.GetValueOrDefault("boyut_b_mm") ?? 200);
                    double rawHMm = Convert.ToDouble(bulgu.GetValueOrDefault("boyut_h_mm") ?? 200);

                    double bMm, hMm;
                    if (useDiamTable)
                    {
                        // Çap tablosu: MEP yuvarlak ise çap=max(b,h), dikdörtgen ise b/h ayrı
                        bool isRound = Math.Abs(rawBMm - rawHMm) < 5;
                        if (isRound)
                        {
                            double resolved = ResolveDiameterFromTable(rawBMm);
                            bMm = resolved;
                            hMm = resolved;
                        }
                        else
                        {
                            bMm = ResolveDiameterFromTable(rawBMm);
                            hMm = ResolveDiameterFromTable(rawHMm);
                        }
                    }
                    else
                    {
                        bMm = rawBMm + offsetMm * 2;
                        hMm = rawHMm + offsetMm * 2;
                    }

                    double bFt = bMm / 304.8;
                    double hFt = hMm / 304.8;

                    if (!dryRun)
                    {
                        // Host element al
                        long hostIdVal = Convert.ToInt64(bulgu.GetValueOrDefault("host_id") ?? 0);
                        var hostEl = hostIdVal > 0
                            ? doc.GetElement(Rv.MakeElementId(hostIdVal))
                            : null;

                        FamilyInstance? inst = null;

                        if (hostEl is Wall wall)
                        {
                            // Duvara host-based yerleştir
                            inst = doc.Create.NewFamilyInstance(
                                point, symbol, wall, defaultLevel,
                                StructuralType.NonStructural);
                        }
                        else if (hostEl is Floor || hostEl is CeilingAndFloor)
                        {
                            // Döşemeye face-based yerleştir
                            inst = doc.Create.NewFamilyInstance(
                                point, symbol, defaultLevel,
                                StructuralType.NonStructural);
                        }
                        else
                        {
                            // Genel yerleştirme
                            inst = doc.Create.NewFamilyInstance(
                                point, symbol, defaultLevel,
                                StructuralType.NonStructural);
                        }

                        if (inst != null)
                        {
                            // Boyut parametrelerini yaz
                            SetParamFt(inst, paramB, bFt);
                            SetParamFt(inst, paramH, hFt);

                            // Sistem adını yaz
                            var sistemAdı = bulgu.GetValueOrDefault("element_sistem")?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(sistemAdı))
                                SetParamStr(inst, paramSistem, sistemAdı);
                        }
                    }

                    yerlestirilen++;
                }
                catch (Exception ex)
                {
                    hata++;
                    ctx.Log($"  ✗ Boşluk yerleştirme hatası [{bulgu.GetValueOrDefault("element_id")}]: {ex.Message}");
                }
            }

            if (!dryRun) tx.Commit();

            ctx.Log($"  → {yerlestirilen} boşluk yerleştirildi, {atlanan} atlandı, {hata} hata" +
                    (dryRun ? " [DRY RUN]" : ""));

            return new Dictionary<string, object?>
            {
                ["yerlestirilen"] = yerlestirilen,
                ["atlanan"]       = atlanan,
                ["hata"]          = hata,
                ["family_name"]   = familyName,
                ["dry_run"]       = dryRun,
                ["toplam_hedef"]  = hedefler.Count,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı metodlar
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ElementIntersectsSolidFilter ile kesin solid kesişim testi.
        /// RevitPlugins-master / RevitFinishing RoomElement mantığından uyarlanmıştır:
        ///   BoundingBoxIntersectsFilter (hızlı) → ElementIntersectsSolidFilter (kesin)
        /// İki kademeli filtre yanlış pozitifi ~%80 azaltır.
        /// </summary>
        /// <summary>
        /// Linked model MEP elemanı için transform uygulanmış solid kesişim testi.
        /// </summary>
        private static bool SolidIntersectsLinked(
            Element mep, Transform mepXform, Element host, Document doc)
        {
            try
            {
                var opts = new Options { DetailLevel = ViewDetailLevel.Fine };

                // MEP solid — transform uygula
                var mepSolids = mep.get_Geometry(opts)
                    ?.OfType<Solid>()
                    .Where(s => s.Volume > 0)
                    .Select(s => SolidUtils.CreateTransformed(s, mepXform))
                    .ToList() ?? new List<Solid>();

                if (!mepSolids.Any()) return true;

                // Host solid
                var hostSolid = host.get_Geometry(opts)
                    ?.OfType<Solid>()
                    .Where(s => s.Volume > 0)
                    .OrderByDescending(s => s.Volume)
                    .FirstOrDefault();

                if (hostSolid == null) return true;

                // Boolean intersection
                foreach (var mepSolid in mepSolids)
                {
                    try
                    {
                        var inter = BooleanOperationsUtils.ExecuteBooleanOperation(
                            mepSolid, hostSolid, BooleanOperationsType.Intersect);
                        if (inter?.Volume > 1e-9) return true;
                    }
                    catch { }
                }
                return false;
            }
            catch { return true; }
        }

        private static bool SolidIntersects(Element mep, Element host, Document doc)
        {
            try
            {
                // Host'un solid geometrisini al
                var opts = new Options { DetailLevel = ViewDetailLevel.Fine };
                var hostSolid = host.get_Geometry(opts)
                    ?.OfType<Solid>()
                    .Where(s => s.Volume > 0)
                    .OrderByDescending(s => s.Volume)
                    .FirstOrDefault();

                if (hostSolid == null) return true; // solid alınamazsa BBox sonucunu koru

                // Solid filter ile MEP elemanını test et
                var solidFilter = new ElementIntersectsSolidFilter(hostSolid);
                var result = new FilteredElementCollector(doc,
                    new List<ElementId> { mep.Id })
                    .WherePasses(solidFilter)
                    .Any();

                return result;
            }
            catch
            {
                return true; // hata durumunda BBox sonucunu koru
            }
        }

        private static List<BuiltInCategory> ResolveHostCategories(List<string> names)
        {
            if (!names.Any())
                return new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_StructuralColumns,
                };

            var map = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
            {
                { "Walls",             BuiltInCategory.OST_Walls },
                { "Floors",            BuiltInCategory.OST_Floors },
                { "StructuralFraming", BuiltInCategory.OST_StructuralFraming },
                { "StructuralColumns", BuiltInCategory.OST_StructuralColumns },
                { "Roofs",             BuiltInCategory.OST_Roofs },
            };

            return names
                .Where(map.ContainsKey)
                .Select(n => map[n])
                .ToList();
        }

        private static List<BuiltInCategory> ResolveMepCategories(List<string> names)
        {
            if (!names.Any())
                return new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_PipeCurves,
                    BuiltInCategory.OST_DuctCurves,
                    BuiltInCategory.OST_CableTray,
                    BuiltInCategory.OST_Conduit,
                    BuiltInCategory.OST_FlexDuctCurves,
                    BuiltInCategory.OST_FlexPipeCurves,
                };

            var map = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
            {
                { "Pipes",      BuiltInCategory.OST_PipeCurves },
                { "Ducts",      BuiltInCategory.OST_DuctCurves },
                { "CableTray",  BuiltInCategory.OST_CableTray },
                { "Conduit",    BuiltInCategory.OST_Conduit },
                { "FlexDuct",   BuiltInCategory.OST_FlexDuctCurves },
                { "FlexPipe",   BuiltInCategory.OST_FlexPipeCurves },
            };

            return names
                .Where(map.ContainsKey)
                .Select(n => map[n])
                .ToList();
        }

        private static List<Element> CollectByCategories(Document doc, List<BuiltInCategory> cats)
        {
            if (!cats.Any()) return new List<Element>();
            return new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(cats))
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        private static List<FamilyInstance> CollectOpeningFamilyInstances(Document doc)
        {
            // Revit'in built-in Opening kategorisi + Generic Model boşluk aileleri
            var openingCats = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_MechanicalEquipment,
            };

            var genericOpenings = new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(openingCats))
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .Where(fi =>
                {
                    var n = (fi.Symbol?.FamilyName ?? "").ToLowerInvariant();
                    return n.Contains("reserv") || n.Contains("réserv") ||
                           n.Contains("opening") || n.Contains("boşluk") ||
                           n.Contains("sleeve")  || n.Contains("eg_rez");
                })
                .ToList();

            // Revit built-in Opening nesneleri
            var builtInOpenings = new FilteredElementCollector(doc)
                .OfClass(typeof(Opening))
                .OfType<FamilyInstance>()
                .ToList();

            return genericOpenings.Concat(builtInOpenings).ToList();
        }

        /// <summary>
        /// İki BoundingBox'ın 3D çakışma testi.
        /// Rotation içermeyen durum için doğrudan Min/Max karşılaştırması.
        /// </summary>
        private static bool BbIntersects(XYZ aMin, XYZ aMax, XYZ bMin, XYZ bMax)
            => aMin.X <= bMax.X && aMax.X >= bMin.X &&
               aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
               aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;

        /// <summary>
        /// Transform içeren (linked model, döndürülmüş eleman) BoundingBox çakışma testi.
        /// Revit-API-Lab GeometryHelper.BoundingBoxesOverlap mantığından uyarlanmıştır.
        ///
        /// SORUN: Min/Max köşelerini transform etmek rotation varsa yanlış AABB verir.
        ///   Örn: 45° döndürülmüş linked file MEP'i → Min/Max transform sonrası küçük kutu
        ///   → asıl geometri çakışmasına rağmen overlap FALSE döner.
        ///
        /// ÇÖZÜM: Tüm 8 köşeyi transform et, sonra yeni AABB hesapla.
        ///   Bu yaklaşım hem yerel hem linked elemanlar için doğru sonuç üretir.
        /// </summary>
        private static bool BbIntersectsTransformed(
            BoundingBoxXYZ mepBb, Transform? mepTransform,
            BoundingBoxXYZ hostBb,
            double toleranceFt = 0)
        {
            XYZ min, max;

            if (mepTransform == null || mepTransform.IsIdentity)
            {
                // Dönüşüm yok — doğrudan kullan (hızlı yol)
                min = mepBb.Min - new XYZ(toleranceFt, toleranceFt, toleranceFt);
                max = mepBb.Max + new XYZ(toleranceFt, toleranceFt, toleranceFt);
            }
            else
            {
                // Tüm 8 köşeyi transform et, yeni AABB hesapla
                var corners = new[]
                {
                    mepTransform.OfPoint(new XYZ(mepBb.Min.X, mepBb.Min.Y, mepBb.Min.Z)),
                    mepTransform.OfPoint(new XYZ(mepBb.Max.X, mepBb.Min.Y, mepBb.Min.Z)),
                    mepTransform.OfPoint(new XYZ(mepBb.Min.X, mepBb.Max.Y, mepBb.Min.Z)),
                    mepTransform.OfPoint(new XYZ(mepBb.Max.X, mepBb.Max.Y, mepBb.Min.Z)),
                    mepTransform.OfPoint(new XYZ(mepBb.Min.X, mepBb.Min.Y, mepBb.Max.Z)),
                    mepTransform.OfPoint(new XYZ(mepBb.Max.X, mepBb.Min.Y, mepBb.Max.Z)),
                    mepTransform.OfPoint(new XYZ(mepBb.Min.X, mepBb.Max.Y, mepBb.Max.Z)),
                    mepTransform.OfPoint(new XYZ(mepBb.Max.X, mepBb.Max.Y, mepBb.Max.Z)),
                };
                min = new XYZ(
                    corners.Min(c => c.X) - toleranceFt,
                    corners.Min(c => c.Y) - toleranceFt,
                    corners.Min(c => c.Z) - toleranceFt);
                max = new XYZ(
                    corners.Max(c => c.X) + toleranceFt,
                    corners.Max(c => c.Y) + toleranceFt,
                    corners.Max(c => c.Z) + toleranceFt);
            }

            return min.X <= hostBb.Max.X && max.X >= hostBb.Min.X &&
                   min.Y <= hostBb.Max.Y && max.Y >= hostBb.Min.Y &&
                   min.Z <= hostBb.Max.Z && max.Z >= hostBb.Min.Z;
        }

        /// <summary>
        /// MEP elemanının merkez çizgisi ile host'un BoundingBox merkezi arasındaki
        /// en yakın noktayı döner. Solid intersection daha kesin ama daha yavaş;
        /// burada BoundingBox overlap merkezini kullanıyoruz.
        /// </summary>
        private static XYZ? ComputeIntersectionPoint(Element mep, Element host, Document doc)
        {
            var mepBb  = mep.get_BoundingBox(null);
            var hostBb = host.get_BoundingBox(null);
            if (mepBb == null || hostBb == null) return null;

            // Overlap bölgesinin merkezi
            double ox = (Math.Max(mepBb.Min.X, hostBb.Min.X) + Math.Min(mepBb.Max.X, hostBb.Max.X)) / 2;
            double oy = (Math.Max(mepBb.Min.Y, hostBb.Min.Y) + Math.Min(mepBb.Max.Y, hostBb.Max.Y)) / 2;
            double oz = (Math.Max(mepBb.Min.Z, hostBb.Min.Z) + Math.Min(mepBb.Max.Z, hostBb.Max.Z)) / 2;

            return new XYZ(ox, oy, oz);
        }

        /// <summary>
        /// MEP elemanının kesit boyutunu döner (mm cinsinden).
        /// Boru/kanal çapı veya bounding box boyutu kullanılır.
        /// </summary>
        private static (double bMm, double hMm) ComputeOpeningSize(Element mep, Element host)
        {
            // Önce çap parametresini dene
            var diaParam = mep.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
                        ?? mep.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);

            if (diaParam != null && diaParam.AsDouble() > 0)
            {
                double diaMm = UnitUtils.ConvertFromInternalUnits(
                    diaParam.AsDouble(), UnitTypeId.Millimeters);
                return (diaMm, diaMm);
            }

            // Kanal genişlik/yükseklik
            var wParam = mep.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
            var hParam = mep.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

            if (wParam != null && hParam != null)
            {
                double wMm = UnitUtils.ConvertFromInternalUnits(wParam.AsDouble(), UnitTypeId.Millimeters);
                double hMm = UnitUtils.ConvertFromInternalUnits(hParam.AsDouble(), UnitTypeId.Millimeters);
                return (wMm > 0 ? wMm : 200, hMm > 0 ? hMm : 200);
            }

            // Fallback: bounding box
            var bb = mep.get_BoundingBox(null);
            if (bb != null)
            {
                double bMm = UnitUtils.ConvertFromInternalUnits(bb.Max.X - bb.Min.X, UnitTypeId.Millimeters);
                double hMm = UnitUtils.ConvertFromInternalUnits(bb.Max.Z - bb.Min.Z, UnitTypeId.Millimeters);
                return (Math.Max(bMm, 50), Math.Max(hMm, 50));
            }

            return (200, 200); // mutlak fallback
        }

        private static string GetSystemName(Element mep)
        {
            var sysParam = mep.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)
                        ?? mep.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
            return sysParam?.AsValueString() ?? "";
        }

        private static FamilySymbol? FindSymbol(Document doc, string familyName, string typeName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    string.Equals(s.FamilyName, familyName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.Name,       typeName,   StringComparison.OrdinalIgnoreCase));
        }

        private static void SetParamFt(FamilyInstance inst, string paramName, double valueFt)
        {
            var p = inst.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                p.Set(valueFt);
        }

        private static void SetParamStr(FamilyInstance inst, string paramName, string value)
        {
            var p = inst.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                p.Set(value);
        }

        /// <summary>
        /// Verilen ham çap değerini standart MEP boru çapı tablosuna göre yukarı yuvarlar.
        /// EN 10255 / TS EN 10255 standart çap serileri kullanılır.
        /// </summary>
        private static double ResolveDiameterFromTable(double rawMm)
        {
            // Standart iç çap serileri (mm) — EN 10255 / TS EN ISO 6708
            double[] table = { 15, 20, 25, 32, 40, 50, 65, 80, 100, 125, 150,
                                200, 250, 300, 350, 400, 450, 500, 600, 700, 800 };
            foreach (var d in table)
                if (d >= rawMm - 1) return d;
            return rawMm; // tabloda yoksa ham değeri döndür
        }

    }
}
