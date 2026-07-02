using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Preview;
using EGBIMOTO.Core.Schedule;
using EGBIMOTO.Core.DAG;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — ScheduleOps  (v2.0)
    //
    //  4D/5D canlı önizleme ve parametre yazma için dört op:
    //
    //  1. schedule_collect_4d
    //     List<Element> → FourDFiveDDto  (geometry + schedule_items)
    //     Revit geometrisini triangulate eder, her elemana program bilgisi atar.
    //     Program bilgisi öncelik sırası:
    //       P1: Element parametreleri (EG_BaslangicTarihi, EG_BitisTarihi, EG_FazAdi)
    //       P2: Manifest param "schedule_map" (kategori → {start, end, phase})
    //       P3: Kat bazlı otomatik dağıtım (project_start + kat_sure_hafta)
    //     Log çıktısında P1+P2 (explicit) / toplam ayrımı gösterilir.
    //
    //  2. schedule_collect_5d
    //     List<Element> + önceki calc_cost çıktısı → FourDFiveDDto + cost_items
    //     Maliyet kaynağı öncelik sırası:
    //       P1: inputs.cost_step ile belirtilen adımın Vars çıktısı
    //           (List<Dictionary<string,object?>> — calc_cost dönüşü)
    //       P2: EgbimotoData.Registry["cost_rows"]
    //
    //  3. schedule_gate  (STUB — DagExecutor intercept eder)
    //     Kayıt amacıyla var; çağrılırsa exception fırlatır.
    //     DagExecutor bu op'u intercept ederek FourDFiveDWindow'u açar,
    //     vars'a "confirmed" | "cancelled" yazar.
    //
    //  4. set_param_from_schedule
    //     FourDFiveDDto → List<Element> parametrelerine 4D/5D verisi yazar.
    //     EG_BaslangicTarihi, EG_BitisTarihi, EG_FazAdi, EG_WbsKodu (4D)
    //     EG_PozNo, EG_BirimMaliyet, EG_ToplamMaliyet vb.           (5D, opsiyonel)
    //
    //  Manifest kullanımı (4D — tam akış):
    //    { "id":"s1", "op":"collect_multi",
    //      "inputs":{"categories":["OST_StructuralColumns","OST_StructuralFraming",...]} }
    //
    //    { "id":"s2", "op":"schedule_collect_4d", "from":"s1",
    //      "inputs":{ "project_start":"2025-03-01", "project_end":"2025-09-30",
    //                 "schedule_map": { "Kolon": {"start":"2025-03-01","end":"2025-03-25","phase":"Betonarme"},
    //                                   "Kiriş": {"start":"2025-03-20","end":"2025-04-10","phase":"Betonarme"} } } }
    //
    //    { "id":"s3", "op":"schedule_gate", "from":"s2" }
    //
    //    { "id":"s4", "op":"set_param_from_schedule", "from":"s1",
    //      "inputs":{"schedule_step":"s2"},
    //      "condition":"$s3 == confirmed" }
    //
    //  Manifest kullanımı (5D — maliyet pipeline):
    //    { "id":"s7", "op":"calc_cost",           "from":"s6" }
    //    { "id":"s8", "op":"schedule_collect_5d", "from":"s1",
    //      "inputs":{"cost_step":"s7", "project_start":"2025-03-01", ...} }
    //    { "id":"s9", "op":"schedule_gate",       "from":"s8" }
    //    { "id":"s10","op":"set_param_from_schedule","from":"s1",
    //      "inputs":{"schedule_step":"s8","write_cost":"true"},
    //      "condition":"$s9 == confirmed" }
    // ═══════════════════════════════════════════════════════════════════════════

    public static class ScheduleOps
    {
        // ── Birim sabitleri ───────────────────────────────────────────────────
        //  Revit feet → mm dönüşümü.
        //  Three.js Y-up: Revit (X,Y,Z) feet → mm (X, Z, -Y).
        private const double FtToMm    = 304.8;
        // Tessellation yoğunluğu: 0=kaba, 1=ince.
        // 0.5 performans/kalite dengesi için yeterli (önizleme kalitesi).
        private const double TessLevel = 0.5;

        // ─────────────────────────────────────────────────────────────────────
        //  1. schedule_collect_4d
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("schedule_collect_4d",
            Description = "Element listesinden FourDFiveDDto üretir: geometry + 4D program bilgisi. " +
                          "inputs: project_start (ISO date), project_end (ISO date), " +
                          "schedule_map (JSON obj: kategori → {start,end,phase}), " +
                          "kat_sure_hafta (int default:3), max_elements (int default:500), " +
                          "operation_name (string).",
            Category    = "4D/5D",
            RequiresTransaction = false)]
        public static FourDFiveDDto CollectSchedule4D(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] schedule_collect_4d Revit bağlamı gerektirir.");

            var elements     = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var opName       = ctx.GetString("operation_name", "4D Yapım Simülasyonu");
            var projStart    = ctx.GetString("project_start",  DateTime.Today.ToString("yyyy-MM-dd"));
            var projEnd      = ctx.GetString("project_end",    DateTime.Today.AddMonths(9).ToString("yyyy-MM-dd"));
            var maxElements  = ctx.GetInt("max_elements",  500);
            var katSureHafta = ctx.GetInt("kat_sure_hafta",  3);
            var schedMap     = ParseScheduleMap(ctx, "schedule_map");

            if (elements.Count > maxElements)
            {
                ctx.Log($"  ⚠ schedule_collect_4d: {elements.Count} eleman, max {maxElements} ile sınırlandırıldı.");
                elements = elements.Take(maxElements).ToList();
            }

            var dto = new FourDFiveDDto
            {
                OperationName = opName,
                Description   = $"{elements.Count} eleman — 4D program önizlemesi",
                ElementCount  = elements.Count,
                ProjectStart  = projStart,
                ProjectEnd    = projEnd,
            };

            // Geometri topla
            var (meshes, edges, bbox, stats) = CollectGeometry(elements, rctx);
            dto.Meshes = meshes;
            dto.Edges  = edges;
            dto.BBox   = bbox;
            dto.Stats  = stats;

            // Her mesh'e program bilgisi ata (P1 → P2 → P3)
            int explicitCount = 0; // P1 + P2 kaynaklı (parametre / schedule_map)
            for (int i = 0; i < dto.Meshes.Count; i++)
            {
                var mesh      = dto.Meshes[i];
                var element   = i < elements.Count ? elements[i] : null;
                var catName   = mesh.Category;
                var elemIdStr = element != null ? Rv.IdStr(element.Id) : "";

                // P1: Elementin kendi parametrelerinden oku
                if (element != null &&
                    TryReadElementSchedule(element, out var ep1, out var ep2, out var efaz))
                {
                    dto.ScheduleItems.Add(new ScheduleItem
                    {
                        MeshId        = mesh.Id,
                        ElementId     = elemIdStr,
                        TaskName      = BuildTaskName(catName, element, efaz),
                        StartDate     = ep1!,
                        EndDate       = ep2!,
                        Phase         = efaz ?? catName,
                        OriginalColor = mesh.Color
                    });
                    explicitCount++;
                    continue;
                }

                // P2: Manifest schedule_map (kategori eşleşmesi, normalize edilmiş anahtar)
                var mapKey = NormalizeCat(catName);
                if (schedMap.TryGetValue(mapKey, out var mapEntry))
                {
                    dto.ScheduleItems.Add(new ScheduleItem
                    {
                        MeshId        = mesh.Id,
                        ElementId     = elemIdStr,
                        TaskName      = BuildTaskName(catName, element, mapEntry.Phase),
                        StartDate     = mapEntry.Start,
                        EndDate       = mapEntry.End,
                        Phase         = mapEntry.Phase,
                        OriginalColor = mesh.Color
                    });
                    explicitCount++;
                    continue;
                }

                // P3: Kat bazlı otomatik dağıtım (bilgi kaynağı yok)
                var autoEntry = AutoScheduleByLevel(element, projStart, katSureHafta, catName);
                dto.ScheduleItems.Add(new ScheduleItem
                {
                    MeshId        = mesh.Id,
                    ElementId     = elemIdStr,
                    TaskName      = BuildTaskName(catName, element, catName),
                    StartDate     = autoEntry.Start,
                    EndDate       = autoEntry.End,
                    Phase         = catName,
                    OriginalColor = mesh.Color
                });
                // P3 explicitCount'a dahil edilmez — uyarı BuildWarnings içinde üretilir
            }

            // ProjectEnd'i gerçek max bitiş tarihine genişlet
            if (dto.ScheduleItems.Count > 0)
            {
                var maxEnd = dto.ScheduleItems
                    .Select(s => TryParseDate(s.EndDate))
                    .Where(d => d.HasValue)
                    .Max();
                if (maxEnd.HasValue && maxEnd.Value > TryParseDate(projEnd))
                    dto.ProjectEnd = maxEnd.Value.ToString("yyyy-MM-dd");
            }

            dto.Warnings.AddRange(BuildWarnings(dto.ScheduleItems, elements.Count));
            ctx.Log($"  ✓ schedule_collect_4d: {dto.Meshes.Count} mesh, " +
                    $"{explicitCount} explicit (P1/P2) + {dto.ScheduleItems.Count - explicitCount} auto (P3)");
            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  2. schedule_collect_5d
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("schedule_collect_5d",
            Description = "Element listesinden FourDFiveDDto üretir: geometry + 4D program + 5D maliyet. " +
                          "inputs: schedule_collect_4d ile aynı parametreler + " +
                          "cost_step (önceki calc_cost adımının ID'si). " +
                          "cost_step boşsa EgbimotoData.Registry['cost_rows'] aranır.",
            Category    = "4D/5D",
            RequiresTransaction = false)]
        public static FourDFiveDDto CollectSchedule5D(OpContext ctx)
        {
            // 4D geometri + program verisini topla
            var dto4d = CollectSchedule4D(ctx);

            // Maliyet satırlarını bul
            List<Dictionary<string, object?>>? costRows = null;

            // P1: inputs.cost_step ile belirtilen adımın Vars çıktısı
            var costStepKey = ctx.GetString("cost_step", "");
            if (!string.IsNullOrEmpty(costStepKey) &&
                ctx.Vars.TryGetValue(costStepKey, out var rawCost) &&
                rawCost is List<Dictionary<string, object?>> rows1)
            {
                costRows = rows1;
                ctx.Log($"  ✓ schedule_collect_5d: maliyet → Vars['{costStepKey}'] ({costRows.Count} satır)");
            }

            // P2: EgbimotoData.Registry["cost_rows"] (önceki pipeline'da registry'e yazıldıysa)
            if (costRows == null)
            {
                try
                {
                    var regVal = EgbimotoData.Registry.Get("cost_rows");
                    if (regVal is List<Dictionary<string, object?>> rows2)
                    {
                        costRows = rows2;
                        ctx.Log($"  ✓ schedule_collect_5d: maliyet → Registry['cost_rows'] ({costRows.Count} satır)");
                    }
                }
                catch { /* registry erişim hatası — atla */ }
            }

            if (costRows == null || costRows.Count == 0)
            {
                dto4d.Warnings.Add(
                    "5D: Maliyet verisi bulunamadı. " +
                    "inputs.cost_step ile calc_cost adımını belirtin ya da " +
                    "Registry['cost_rows']'un dolu olduğundan emin olun.");
                ctx.Log("  ⚠ schedule_collect_5d: maliyet yok — 4D döndürülüyor");
                return dto4d;
            }

            // element_id → List<row> indeksi
            var costIndex = costRows
                .GroupBy(row => GetString(row, "element_id") ?? GetString(row, "id") ?? "",
                         StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Her schedule_item → maliyet kalemleri
            double total   = 0;
            int    matched = 0;

            foreach (var si in dto4d.ScheduleItems)
            {
                if (!costIndex.TryGetValue(si.ElementId, out var elemRows)) continue;

                foreach (var row in elemRows)
                {
                    var toplam = ToDouble(row, "toplam") ?? ToDouble(row, "total") ?? 0;
                    dto4d.CostItems.Add(new CostItem5D
                    {
                        MeshId     = si.MeshId,
                        ElementId  = si.ElementId,
                        PozNo      = GetString(row, "poz_no")     ?? "",
                        PozAdi     = GetString(row, "poz_adi")    ?? "",
                        Miktar     = ToDouble(row, "miktar")      ?? ToDouble(row, "quantity")  ?? 0,
                        Birim      = GetString(row, "birim")      ?? "",
                        BirimFiyat = ToDouble(row, "birim_fiyat") ?? ToDouble(row, "unit_cost") ?? 0,
                        Toplam     = toplam
                    });
                    total += toplam;
                    matched++;
                }
            }

            dto4d.TotalCost     = total;
            dto4d.OperationName = dto4d.OperationName.Replace("4D", "4D/5D");
            dto4d.Description   = $"{dto4d.ElementCount} eleman — 4D/5D | Toplam: {dto4d.Currency}{total:N0}";

            ctx.Log($"  ✓ schedule_collect_5d: {matched} maliyet satırı, toplam {dto4d.Currency}{total:N0}");
            return dto4d;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  3. schedule_gate  (STUB — DagExecutor intercept eder)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("schedule_gate",
            Description = "DagExecutor tarafından intercept edilir — FourDFiveDWindow modal açar. " +
                          "Bu metod ÇAĞRILMAZ; sadece OpRegistry kaydı içindir. " +
                          "Çıktı: vars[stepId] = 'confirmed' | 'cancelled'.",
            Category    = "4D/5D",
            RequiresTransaction = false)]
        public static object ScheduleGate(OpContext ctx)
        {
            // DagExecutor, UserScheduleGateCallback null ise bu metodu çağırabilir.
            // Normal çalışmada bu satıra ulaşılmamalı.
            throw new InvalidOperationException(
                $"[{ctx.CurrentStepId}] schedule_gate doğrudan çağrıldı. " +
                "DagExecutor.UserScheduleGateCallback set edilmemiş veya DagExecutor eski sürüm.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  4. set_param_from_schedule
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("set_param_from_schedule",
            Description = "schedule_collect_4d/5d çıktısını element parametrelerine yazar. " +
                          "inputs: schedule_step (FourDFiveDDto adım ID'si, zorunlu), " +
                          "start_param (default: EG_BaslangicTarihi), " +
                          "end_param   (default: EG_BitisTarihi), " +
                          "phase_param (default: EG_FazAdi), " +
                          "wbs_param   (default: EG_WbsKodu), " +
                          "write_cost  (bool, default: true — 5D maliyet param'larını da yazar). " +
                          "Dönüş: yazılan element sayısı.",
            Category    = "4D/5D",
            RequiresTransaction = true)]
        public static int SetParamFromSchedule(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] set_param_from_schedule Revit bağlamı gerektirir.");

            var elements = ctx.InputAsOrDefault<List<Element>>(new List<Element>());

            // Hedef parametre adları — manifest'ten override edilebilir, EgParamNames varsayılan
            var startParam = ctx.GetString("start_param", EgParamNames.BaslangicTarihi);
            var endParam   = ctx.GetString("end_param",   EgParamNames.BitisTarihi);
            var phaseParam = ctx.GetString("phase_param", EgParamNames.FazAdi);
            var wbsParam   = ctx.GetString("wbs_param",   EgParamNames.WbsKodu);
            var writeCost  = ctx.GetBool("write_cost",    true);

            // FourDFiveDDto'yu Vars'tan al
            var scheduleStep = ctx.GetString("schedule_step", "");
            FourDFiveDDto? dto = null;

            if (!string.IsNullOrEmpty(scheduleStep) &&
                ctx.Vars.TryGetValue(scheduleStep, out var rawDto))
                dto = rawDto as FourDFiveDDto;

            if (dto == null)
            {
                ctx.Log($"  ⚠ [{ctx.CurrentStepId}] schedule_step='{scheduleStep}' → FourDFiveDDto yok — atlandı.");
                return 0;
            }

            // element_id → ScheduleItem
            // GroupBy + First: aynı ElementId'ye birden fazla mesh varsa ilkini al
            var schedIdx = dto.ScheduleItems
                .Where(si => !string.IsNullOrEmpty(si.ElementId))
                .GroupBy(si => si.ElementId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // element_id → CostItem5D (ilk eşleşen)
            var costIdx = dto.CostItems
                .Where(ci => !string.IsNullOrEmpty(ci.ElementId))
                .GroupBy(ci => ci.ElementId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int written = 0, skipped = 0;

            using var scope = new Host.RevitWriteScope(
                rctx.Doc, "4D/5D Parametre Yaz", rctx.IsAtomicMode);

            foreach (var element in elements)
            {
                var eid = Rv.IdStr(element.Id);

                if (!schedIdx.TryGetValue(eid, out var si))
                {
                    skipped++;
                    continue;
                }

                bool any = false;

                // 4D — tarih + faz
                any |= WriteText(element, startParam, si.StartDate);
                any |= WriteText(element, endParam,   si.EndDate);
                any |= WriteText(element, phaseParam, si.Phase);

                if (!string.IsNullOrEmpty(si.WbsCode))
                    any |= WriteText(element, wbsParam, si.WbsCode);

                // 5D — maliyet (opsiyonel, write_cost=true ise)
                if (writeCost && costIdx.TryGetValue(eid, out var ci))
                {
                    any |= WriteText  (element, EgParamNames.PozNo,         ci.PozNo);
                    any |= WriteText  (element, EgParamNames.PozAdi,        ci.PozAdi);
                    any |= WriteText  (element, EgParamNames.PozBirim,      ci.Birim);
                    any |= WriteNumber(element, EgParamNames.PozMiktar,     ci.Miktar);
                    any |= WriteNumber(element, EgParamNames.BirimMaliyet,  ci.BirimFiyat);
                    any |= WriteNumber(element, EgParamNames.ToplamMaliyet, ci.Toplam);
                }

                if (any) written++;
            }

            scope.Commit();
            ctx.Log($"  ✓ [{ctx.CurrentStepId}] set_param_from_schedule: " +
                    $"{written} yazıldı, {skipped} eşleşmedi / {elements.Count} toplam");
            return written;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Geometri toplama
        //  PreviewOps ile aynı mantık; bağımlılık yaratmamak için kopya.
        //  İki op aynı anda aktifse PreviewOps'u çağırmak yerine bağımsız kalmak
        //  tercih edildi (farklı renk/opacity mantığı ileride ayrışabilir).
        // ═════════════════════════════════════════════════════════════════════

        private static (List<PreviewMesh> meshes,
                        List<PreviewEdge> edges,
                        PreviewBBox?      bbox,
                        Dictionary<string, string> stats)
            CollectGeometry(List<Element> elements, RevitOpContext rctx)
        {
            var meshes    = new List<PreviewMesh>();
            var edges     = new List<PreviewEdge>();   // 4D'de edge kullanılmıyor; ileride eklenebilir
            var stats     = new Dictionary<string, string>();
            var catCounts = new Dictionary<string, int>();

            float bMinX = float.MaxValue, bMinY = float.MaxValue, bMinZ = float.MaxValue;
            float bMaxX = float.MinValue, bMaxY = float.MinValue, bMaxZ = float.MinValue;
            bool  hasBBox = false;

            var geomOptions = new Options
            {
                ComputeReferences        = false,
                IncludeNonVisibleObjects = false,
                DetailLevel              = ViewDetailLevel.Medium
            };

            int meshIdx = 0;
            foreach (var element in elements)
            {
                var catName = CategoryName(element);
                catCounts.TryGetValue(catName, out int cnt);
                catCounts[catName] = cnt + 1;

                GeometryElement? geomEl;
                try   { geomEl = element.get_Geometry(geomOptions); }
                catch { continue; }

                if (geomEl == null) continue;

                var vertices = new List<float>();
                var indices  = new List<int>();
                TraverseGeometry(geomEl, vertices, indices);

                if (vertices.Count == 0) continue;

                // BBox güncelle: i'inci vertex (X=i, Y=i+1, Z=i+2), adım 3
                for (int i = 0; i + 2 < vertices.Count; i += 3)
                {
                    float vx = vertices[i], vy = vertices[i + 1], vz = vertices[i + 2];
                    if (vx < bMinX) bMinX = vx; if (vx > bMaxX) bMaxX = vx;
                    if (vy < bMinY) bMinY = vy; if (vy > bMaxY) bMaxY = vy;
                    if (vz < bMinZ) bMinZ = vz; if (vz > bMaxZ) bMaxZ = vz;
                    hasBBox = true;
                }

                meshes.Add(new PreviewMesh
                {
                    Id         = $"m_{meshIdx++}",
                    Category   = catName,
                    Label      = BuildLabel(element, catName),
                    Color      = PreviewColors.ForCategory(catName),
                    Opacity    = 0.75f,
                    Vertices   = vertices,
                    Indices    = indices,
                    Properties = BuildProperties(element)
                });
            }

            foreach (var kv in catCounts)
                stats[kv.Key] = kv.Value.ToString();

            PreviewBBox? bbox = hasBBox
                ? new PreviewBBox { MinX = bMinX, MinY = bMinY, MinZ = bMinZ,
                                    MaxX = bMaxX, MaxY = bMaxY, MaxZ = bMaxZ }
                : null;

            return (meshes, edges, bbox, stats);
        }

        private static void TraverseGeometry(GeometryElement geomEl,
            List<float> vertices, List<int> indices)
        {
            foreach (var obj in geomEl)
            {
                switch (obj)
                {
                    case Solid solid:
                        TessellateSolid(solid, vertices, indices);
                        break;
                    case GeometryInstance inst:
                        // Instance'ın kendi koordinat sistemine çevrilmiş geometrisini al
                        TraverseGeometry(inst.GetInstanceGeometry(), vertices, indices);
                        break;
                }
            }
        }

        private static void TessellateSolid(Solid solid,
            List<float> vertices, List<int> indices)
        {
            // Sıfır hacimli solid (anahtar, düzlemsel yüzey vb.) atla
            if (solid.Volume < 1e-9) return;

            foreach (Face face in solid.Faces)
            {
                try
                {
                    var mesh = face.Triangulate(TessLevel);
                    if (mesh == null) continue;

                    // Mevcut vertex sayısına göre base index hesapla
                    int baseIdx = vertices.Count / 3;

                    foreach (var v in mesh.Vertices)
                    {
                        // Revit (X,Y,Z) feet → Three.js Y-up mm: (X*k, Z*k, -Y*k)
                        vertices.Add((float)(v.X *  FtToMm));
                        vertices.Add((float)(v.Z *  FtToMm));
                        vertices.Add((float)(-v.Y * FtToMm));
                    }

                    for (int t = 0; t < mesh.NumTriangles; t++)
                    {
                        var tri = mesh.get_Triangle(t);
                        indices.Add(baseIdx + (int)tri.get_Index(0));
                        indices.Add(baseIdx + (int)tri.get_Index(1));
                        indices.Add(baseIdx + (int)tri.get_Index(2));
                    }
                }
                catch { /* yüz tessellation hatası — sessizce atla */ }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Program yardımcıları
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Elementin kendi parametrelerinden başlangıç/bitiş/faz okur.
        /// EgParamNames.BaslangicAliases / BitisAliases / FazAliases öncelik sırasıyla aranır.
        /// Her iki tarih parametresi de dolu değilse false döner.
        /// </summary>
        private static bool TryReadElementSchedule(Element el,
            out string? start, out string? end, out string? faz)
        {
            start = end = faz = null;
            try
            {
                Parameter? p1 = null, p2 = null, pf = null;

                foreach (var alias in EgParamNames.BaslangicAliases)
                {
                    p1 = el.LookupParameter(alias);
                    if (p1 != null) break;
                }
                foreach (var alias in EgParamNames.BitisAliases)
                {
                    p2 = el.LookupParameter(alias);
                    if (p2 != null) break;
                }
                foreach (var alias in EgParamNames.FazAliases)
                {
                    pf = el.LookupParameter(alias);
                    if (pf != null) break;
                }

                if (p1 != null && p2 != null)
                {
                    var s  = p1.AsString();
                    var e2 = p2.AsString();
                    if (!string.IsNullOrWhiteSpace(s) && !string.IsNullOrWhiteSpace(e2))
                    {
                        start = s.Trim();
                        end   = e2.Trim();
                        faz   = pf?.AsString()?.Trim();
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Elementin kat numarasına ve kategori tipine göre otomatik program hesaplar (P3).
        /// Kat numarası: Level/Base Level parametresindeki ilk rakam karakter.
        /// Kategori ofseti: kolon=0, kiriş=+5gün, döşeme=+10gün, duvar=+15gün vb.
        /// Sonuç: katNo × katSureHafta hafta + catOffset gün başlangıç.
        /// </summary>
        private static ScheduleMapEntry AutoScheduleByLevel(
            Element? element, string projStart, int katSureHafta, string catName)
        {
            int katNo = 0;
            try
            {
                if (element != null)
                {
                    var lvlParam = element.LookupParameter("Base Level")
                                ?? element.LookupParameter("Level")
                                ?? element.LookupParameter("Kat");
                    if (lvlParam != null)
                    {
                        var lvlStr = lvlParam.AsValueString() ?? "";
                        // "Z01" → 0, "Zemin Kat" → 0, "1. Kat" → 1, "2. Kat" → 2
                        // İlk rakam karakteri alınır; yoksa 0
                        foreach (var ch in lvlStr)
                            if (char.IsDigit(ch)) { katNo = ch - '0'; break; }
                    }
                }
            }
            catch { }

            // Kategori ofseti: üst yapı sırasına göre (kolon döker, kiriş döker, döşeme döker…)
            // NormalizeCat sonucu kullanılmalı — Türkçe karakter dönüşümü zaten yapıldı
            int catOffset = NormalizeCat(catName) switch
            {
                "kolon"    => 0,
                "kiris"    => 1,  // "kiriş" → NormalizeCat → "kiris"
                "doseme"   => 2,  // "döşeme" → NormalizeCat → "doseme"
                "duvar"    => 3,
                "merdiven" => 4,
                "temel"    => -1, // temel kolon'dan önce
                _          => 0
            };

            if (!DateTime.TryParse(projStart, out var pStart))
                pStart = DateTime.Today;

            var startDate = pStart.AddDays(katNo * katSureHafta * 7 + catOffset * 5);
            var endDate   = startDate.AddDays(katSureHafta * 7 - 1);

            return new ScheduleMapEntry
            {
                Start = startDate.ToString("yyyy-MM-dd"),
                End   = endDate.ToString("yyyy-MM-dd"),
                Phase = catName
            };
        }

        /// <summary>
        /// Manifest schedule_map parametresini ayrıştırır.
        /// Giriş: JSON object veya JsonElement (DagExecutor ResolveParams çıktısı).
        /// Anahtar: NormalizeCat ile normalize edilmiş kategori adı.
        /// </summary>
        private static Dictionary<string, ScheduleMapEntry> ParseScheduleMap(
            OpContext ctx, string paramName)
        {
            var result = new Dictionary<string, ScheduleMapEntry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!ctx.Params.TryGetValue(paramName, out var rawMap) || rawMap == null)
                    return result;

                // DagExecutor parametreyi JsonElement olarak iletebilir; string de gelebilir
                string json = rawMap is string s ? s : JsonSerializer.Serialize(rawMap);

                var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var key   = NormalizeCat(prop.Name);
                    string start = "", end = "", phase = prop.Name;

                    if (prop.Value.TryGetProperty("start", out var sv)) start = sv.GetString() ?? "";
                    if (prop.Value.TryGetProperty("end",   out var ev)) end   = ev.GetString() ?? "";
                    if (prop.Value.TryGetProperty("phase", out var pv)) phase = pv.GetString() ?? prop.Name;

                    result[key] = new ScheduleMapEntry { Start = start, End = end, Phase = phase };
                }
            }
            catch { }
            return result;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Element yardımcıları
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Revit kategori adını EGBIMOTO standart adına çevirir.
        /// "OST_StructuralColumns" → "Yapısal Kolonlar" → "Kolon"
        /// </summary>
        private static string CategoryName(Element el)
        {
            try
            {
                var name = el.Category?.Name ?? "";
                // Revit'in "Yapısal " / "Structural " öneklerini at, ardından standartlaştır
                return name.Replace("Yapısal ", "").Replace("Structural ", "").Trim() switch
                {
                    "Columns"  or "Sütunlar"  or "Kolonlar"  => "Kolon",
                    "Framing"  or "Kirişler"                 => "Kiriş",
                    "Floors"   or "Döşemeler"                => "Döşeme",
                    "Walls"    or "Duvarlar"                 => "Duvar",
                    "Foundations" or "Temeller"              => "Temel",
                    "Roofs"    or "Çatılar"                  => "Çatı",
                    "Stairs"   or "Merdivenler"              => "Merdiven",
                    _                                        => el.Category?.Name ?? "Genel"
                };
            }
            catch { return "Genel"; }
        }

        /// <summary>
        /// Kategori adını schedule_map anahtar karşılaştırması için normalize eder.
        /// Küçük harf + Türkçe karakter dönüşümü (büyük İ dahil).
        /// Örnek: "Kiriş" → "kiris", "Döşeme" → "doseme", "İSKELE" → "iskele"
        /// </summary>
        private static string NormalizeCat(string? cat)
        {
            if (cat == null) return "";
            return cat.ToLowerInvariant()
                      .Replace("İ", "i")   // Büyük I-noktalı → i (invariant kültür dönüştürmez)
                      .Replace("ı", "i")
                      .Replace("ş", "s")
                      .Replace("ğ", "g")
                      .Replace("ç", "c")
                      .Replace("ü", "u")
                      .Replace("ö", "o")
                      .Trim();
        }

        private static string BuildTaskName(string catName, Element? el, string? faz)
        {
            var lvl = "";
            try { lvl = el?.LookupParameter("Level")?.AsValueString() ?? ""; }
            catch { }

            // Kat bilgisi varsa "Kolon — Z01" biçiminde göster
            if (!string.IsNullOrEmpty(lvl)) return $"{catName} — {lvl}";
            // Faz farklıysa "Kolon (Betonarme)" biçiminde göster
            if (!string.IsNullOrEmpty(faz) && faz != catName) return $"{catName} ({faz})";
            return catName;
        }

        private static string BuildLabel(Element el, string catName)
        {
            try
            {
                var typeId = el.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    var type = el.Document?.GetElement(typeId);
                    if (type != null) return $"{catName} — {type.Name}";
                }
            }
            catch { }
            return catName;
        }

        private static Dictionary<string, string> BuildProperties(Element el)
        {
            var props = new Dictionary<string, string>();
            try
            {
                props["ID"] = Rv.IdStr(el.Id);    // Rv.IdStr: REVIT2024=IntegerValue, 2025+=Value
                var typeId = el.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    var type = el.Document?.GetElement(typeId);
                    if (type != null) props["Tip"] = type.Name;
                }
                var lvlParam = el.LookupParameter("Level");
                if (lvlParam != null) props["Kat"] = lvlParam.AsValueString() ?? "";
            }
            catch { }
            return props;
        }

        private static List<string> BuildWarnings(List<ScheduleItem> items, int totalElements)
        {
            var w = new List<string>();
            int noDate = items.Count(s => string.IsNullOrEmpty(s.StartDate));
            if (noDate > 0)
                w.Add($"{noDate} elemanda tarih bilgisi yok — P3 kat bazlı otomatik dağıtım uygulandı");
            if (totalElements > 400)
                w.Add($"{totalElements} eleman — 400+ elemanda viewer performansı düşebilir");
            return w;
        }

        private static DateTime? TryParseDate(string? s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return DateTime.TryParse(s, out var d) ? d : (DateTime?)null;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Row dictionary yardımcıları (schedule_collect_5d için)
        // ═════════════════════════════════════════════════════════════════════

        private static string? GetString(Dictionary<string, object?> row, string key)
        {
            row.TryGetValue(key, out var val);
            return val?.ToString();
        }

        private static double? ToDouble(Dictionary<string, object?> row, string key)
        {
            if (!row.TryGetValue(key, out var val)) return null;
            if (val is double d) return d;
            if (val is float  f) return (double)f;
            if (val is int    i) return (double)i;
            if (val is long   l) return (double)l;
            if (double.TryParse(val?.ToString(), out var parsed)) return parsed;
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Parametre yazma yardımcıları (set_param_from_schedule için)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// TEXT storage'lı bir parametreye string değer yazar.
        /// Parametre bulunamazsa veya readonly ise false döner.
        /// </summary>
        private static bool WriteText(Element el, string paramName, string? value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            try
            {
                var p = el.LookupParameter(paramName);
                if (p == null || p.IsReadOnly) return false;
                if (p.StorageType == StorageType.String)
                {
                    p.Set(value);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// NUMBER/DOUBLE storage'lı bir parametreye sayısal değer yazar.
        /// Integer storage → yuvarlama. String storage → "G" formatında metin.
        /// Birim dönüşümü YAPILMAZ: EG_ parametreleri birimsiz NUMBER türünde tanımlıdır.
        /// </summary>
        private static bool WriteNumber(Element el, string paramName, double value)
        {
            try
            {
                var p = el.LookupParameter(paramName);
                if (p == null || p.IsReadOnly) return false;
                switch (p.StorageType)
                {
                    case StorageType.Double:
                        p.Set(value);
                        return true;
                    case StorageType.Integer:
                        p.Set((int)Math.Round(value));
                        return true;
                    case StorageType.String:
                        p.Set(value.ToString("G"));
                        return true;
                }
            }
            catch { }
            return false;
        }
    }

    // ── İç yardımcı kayıt ────────────────────────────────────────────────────

    internal sealed class ScheduleMapEntry
    {
        public string Start { get; set; } = "";
        public string End   { get; set; } = "";
        public string Phase { get; set; } = "";
    }
}
