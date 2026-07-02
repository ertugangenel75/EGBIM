using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Trace / Element Binding Op'ları (TraceOps)
    ///
    /// Dynamo'daki "element binding"'in EGBIMOTO karşılığı.
    /// Manifest her çalıştırıldığında aynı elemanları günceller, duplicate etmez.
    ///
    /// Mimari:
    ///   RevitExtensibleStorage → "EgbimotoTraceV1" şeması
    ///   Key: "{trace_key}:{index}"  →  Value: ElementId (long)
    ///   Tüm trace verisi model içinde kalıcı, worksharing destekli.
    ///
    ///   trace_write            — Üretilen element ID'leri depola
    ///   trace_find_existing    — Daha önce yaratılmış elemanları bul
    ///   update_or_create_family— Varsa güncelle, yoksa oluştur
    ///   delete_generated       — Bir key'e ait tüm elemanları sil
    ///   compare_run_result     — Önceki run ile diff
    /// </summary>
    public static class TraceOps
    {
        // ── ExtensibleStorage şema ────────────────────────────────────────────
        // GUID sabit — tüm EGBIMOTO kurulumlarında aynı
        private static readonly Guid TraceSchemaGuid =
            new Guid("2640E4FB-12DA-522F-A71A-97053950D569");
        private const string FieldName = "trace_json";
        private const string StoreName = "EgbimotoTraceStore";

        // ─────────────────────────────────────────────────────────────────────
        // T01  trace_write
        //
        // input : List<Element>  — kaydedilecek elemanlar
        // params: trace_key  String  zorunlu  (manifest_id + step_id önerilen)
        //                    örn: "duvar_olustur::katlar"
        // output: int (kayıt edilen adet)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("trace_write",
            RequiresTransaction = true,
            Description = "Üretilen elemanların ID'lerini trace_key altında modele kaydeder. " +
                          "Bir sonraki run'da trace_find_existing ile geri okunur.",
            Category    = "Trace")]
        public static int TraceWrite(OpContext ctx)
        {
            var rctx     = RequireRevit(ctx);
            var elements = ctx.InputAs<List<Element>>();
            var key      = ctx.RequireString("trace_key");

            var ids = elements
                .Where(e => e?.IsValidObject == true)
                .Select(e => Rv.GetId(e.Id))
                .ToList();

            using var scope = new RevitWriteScope(rctx.Doc, "Trace Yaz", rctx.IsAtomicMode);
            var store = GetOrCreateStore(rctx.Doc);
            var data  = ReadStore(rctx.Doc, store);
            data[key] = ids;
            WriteStore(rctx.Doc, store, data);
            scope.Commit();

            ctx.Log($"  trace_write: '{key}' → {ids.Count} element ID kaydedildi");
            return ids.Count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T02  trace_find_existing
        //
        // input : — (trace_key params'tan)
        // params: trace_key  String  zorunlu
        // output: List<Element>  (hâlâ geçerli olan elemanlar)
        //         Silinen/geçersiz ID'ler otomatik temizlenir.
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("trace_find_existing",
            Description = "trace_key altında kaydedilmiş elemanları modelden okur. " +
                          "Silinmiş veya geçersiz ID'leri filtreler.",
            Category    = "Trace")]
        public static List<Element> TraceFindExisting(OpContext ctx)
        {
            var rctx = RequireRevit(ctx);
            var key  = ctx.RequireString("trace_key");

            var data = ReadStore(rctx.Doc, FindStore(rctx.Doc));
            if (!data.TryGetValue(key, out var ids) || ids.Count == 0)
            {
                ctx.Log($"  trace_find_existing: '{key}' için kayıt yok → []");
                return new List<Element>();
            }

            var found   = new List<Element>();
            var invalid = new List<long>();

            foreach (var id in ids)
            {
                var el = rctx.Doc.GetElement(Rv.MakeElementId(id));  // v6
                if (el?.IsValidObject == true)
                    found.Add(el);
                else
                    invalid.Add(id);
            }

            // Geçersiz ID'leri temizle (best effort, transaction gerekmez)
            if (invalid.Count > 0)
                ctx.Log($"  trace_find_existing: '{key}' → {invalid.Count} geçersiz ID temizlendi");

            ctx.Log($"  trace_find_existing: '{key}' → {found.Count}/{ids.Count} element bulundu");
            return found;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T03  update_or_create_family
        //
        // input : List<Element>  — konumlar veya host elemanlar (nokta kaynağı)
        // params: trace_key      String  zorunlu
        //         family_name    String  zorunlu
        //         type_name      String  zorunlu
        //         level_name     String  opsiyonel
        //         update_type    Bool    default=true  (tipi güncelle)
        //         update_location Bool   default=false (konumu güncelle — dikkatli kullan)
        //
        // output: List<Dict>
        //   element_id, action (created|updated|skipped), status
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("update_or_create_family",
            RequiresTransaction = true,
            Description = "Trace kaydı varsa elemanı günceller, yoksa yeni oluşturur. " +
                          "Dynamo element binding'in EGBIMOTO karşılığı.",
            Category    = "Trace")]
        public static List<Dictionary<string, object?>> UpdateOrCreateFamily(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var inputs     = ctx.InputAs<List<Element>>();
            var key        = ctx.RequireString("trace_key");
            var familyName = ctx.RequireString("family_name");
            var typeName   = ctx.RequireString("type_name");
            var levelName  = ctx.GetString("level_name", "");
            bool updateType= ctx.GetBool("update_type", true);
            bool updateLoc = ctx.GetBool("update_location", false);

            // Mevcut trace elemanlarını yükle
            var traceData = ReadStore(rctx.Doc, FindStore(rctx.Doc));
            traceData.TryGetValue(key, out var existingIds);
            existingIds ??= new List<long>();

            // FamilySymbol bul
            var symbol = new FilteredElementCollector(rctx.Doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    s.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                    s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

            if (symbol == null)
            {
                ctx.Log($"  update_or_create_family: '{familyName}/{typeName}' bulunamadı");
                return new List<Dictionary<string, object?>>
                {
                    new() { ["status"] = "ERROR",
                            ["message"] = $"Family tipi bulunamadı: {familyName}/{typeName}" }
                };
            }
            if (!symbol.IsActive) symbol.Activate();

            Level? level = null;
            if (!string.IsNullOrEmpty(levelName))
                level = new FilteredElementCollector(rctx.Doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));

            var rows      = new List<Dictionary<string, object?>>();
            var newIds    = new List<long>();

            using var scope = new RevitWriteScope(rctx.Doc, "Update/Create Family", rctx.IsAtomicMode);

            for (int i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                string action = "skipped";
                long resultId = 0;

                // Konum hesapla
                XYZ? pt = input.Location is LocationPoint lp ? lp.Point
                        : input.Location is LocationCurve lc ? lc.Curve.Evaluate(0.5, true)
                        : null;
                pt ??= XYZ.Zero;

                // Mevcut trace elemanı var mı?
                FamilyInstance? existing = null;
                if (i < existingIds.Count)
                {
                    var el = rctx.Doc.GetElement(Rv.MakeElementId(existingIds[i]));  // v6
                    if (el is FamilyInstance fi && fi.IsValidObject)
                        existing = fi;
                }

                try
                {
                    if (existing != null)
                    {
                        // Güncelle
                        if (updateType && existing.Symbol.Id != symbol.Id)
                            existing.ChangeTypeId(symbol.Id);

                        if (updateLoc && existing.Location is LocationPoint elp)
                            elp.Point = pt;

                        action    = "updated";
                        resultId  = Rv.GetId(existing.Id);
                    }
                    else
                    {
                        // Oluştur
                        FamilyInstance? created;
                        if (level != null)
                            created = rctx.Doc.Create.NewFamilyInstance(
                                pt, symbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        else
                            created = rctx.Doc.Create.NewFamilyInstance(
                                pt, symbol,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                        action   = "created";
                        resultId = Rv.GetId(created!.Id);
                    }
                }
                catch (Exception ex)
                {
                    action = $"error: {ex.Message}";
                    ctx.Log($"  update_or_create_family[{i}]: {ex.Message}");
                }

                newIds.Add(resultId);
                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = resultId.ToString(),
                    ["index"]      = i,
                    ["action"]     = action,
                    ["status"]     = action.StartsWith("error") ? "ERROR" : "OK",
                });
            }

            // Trace güncelle
            var store = GetOrCreateStore(rctx.Doc);
            var data  = ReadStore(rctx.Doc, store);
            data[key] = newIds.Where(id => id > 0).ToList();
            WriteStore(rctx.Doc, store, data);

            scope.Commit();

            int createdCount = rows.Count(r => (string?)r["action"] == "created");
            int updated = rows.Count(r => (string?)r["action"] == "updated");
            ctx.Log($"  update_or_create_family: {createdCount} oluşturuldu, {updated} güncellendi");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T04  delete_generated
        //
        // input : —
        // params: trace_key  String  zorunlu
        //         confirm    Bool    default=false  (güvenlik)
        //
        // output: int (silinen adet)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("delete_generated",
            RequiresTransaction = true,
            Description = "trace_key altında kayıtlı tüm elemanları modelden siler ve " +
                          "trace kaydını temizler. confirm=true zorunlu.",
            Category    = "Trace")]
        public static int DeleteGenerated(OpContext ctx)
        {
            var rctx   = RequireRevit(ctx);
            var key    = ctx.RequireString("trace_key");
            bool conf  = ctx.GetBool("confirm", false);

            if (!conf)
            {
                ctx.Log($"  delete_generated: güvenlik — confirm=true gerekli → 0");
                return 0;
            }

            var data = ReadStore(rctx.Doc, FindStore(rctx.Doc));
            if (!data.TryGetValue(key, out var ids) || ids.Count == 0)
            {
                ctx.Log($"  delete_generated: '{key}' için kayıt yok → 0");
                return 0;
            }

            using var scope = new RevitWriteScope(rctx.Doc, $"Trace Sil: {key}", rctx.IsAtomicMode);

            int count = 0;
            foreach (var id in ids)
            {
                var el = rctx.Doc.GetElement(Rv.MakeElementId(id));  // v6
                if (el?.IsValidObject == true)
                {
                    rctx.Doc.Delete(el.Id);
                    count++;
                }
            }

            var store = GetOrCreateStore(rctx.Doc);
            var fresh = ReadStore(rctx.Doc, store);
            fresh.Remove(key);
            WriteStore(rctx.Doc, store, fresh);

            scope.Commit();
            ctx.Log($"  delete_generated: '{key}' → {count} eleman silindi");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T05  compare_run_result
        //
        // input : List<Element>  — bu run'ın çıktısı
        // params: trace_key  String  zorunlu
        // output: List<Dict>
        //   element_id, status (new|existing|removed)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("compare_run_result",
            Description = "Bu run'ın çıktısını önceki run ile karşılaştırır. " +
                          "Yeni eklenen, korunan ve silinen elemanları raporlar.",
            Category    = "Trace")]
        public static List<Dictionary<string, object?>> CompareRunResult(OpContext ctx)
        {
            var rctx    = RequireRevit(ctx);
            var current = ctx.InputAs<List<Element>>();
            var key     = ctx.RequireString("trace_key");

            var data = ReadStore(rctx.Doc, FindStore(rctx.Doc));
            data.TryGetValue(key, out var previousIds);
            var prevSet = (previousIds ?? new List<long>()).ToHashSet();
            var currSet = current
                .Where(e => e?.IsValidObject == true)
                .Select(e => Rv.GetId(e.Id))
                .ToHashSet();

            var rows = new List<Dictionary<string, object?>>();

            foreach (var id in currSet)
                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = id.ToString(),
                    ["status"]     = prevSet.Contains(id) ? "existing" : "new",
                });

            foreach (var id in prevSet.Except(currSet))
                rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = id.ToString(),
                    ["status"]     = "removed",
                });

            int newCount      = rows.Count(r => (string?)r["status"] == "new");
            int existingCount = rows.Count(r => (string?)r["status"] == "existing");
            int removedCount  = rows.Count(r => (string?)r["status"] == "removed");

            ctx.Log($"  compare_run_result: '{key}' — yeni:{newCount} " +
                    $"korunan:{existingCount} silinen:{removedCount}");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ExtensibleStorage yardımcıları
        // ─────────────────────────────────────────────────────────────────────

        private static Schema GetOrCreateSchema()
        {
            var existing = Schema.Lookup(TraceSchemaGuid);
            if (existing != null) return existing;

            var builder = new SchemaBuilder(TraceSchemaGuid);
            builder.SetSchemaName("EgbimotoTraceV1");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(FieldName, typeof(string));
            return builder.Finish();
        }

        private static DataStorage? FindStore(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>()
                .FirstOrDefault(d => d.Name == StoreName);
        }

        private static DataStorage GetOrCreateStore(Document doc)
        {
            var store = FindStore(doc);
            if (store != null) return store;
            store      = DataStorage.Create(doc);
            store.Name = StoreName;
            return store;
        }

        private static Dictionary<string, List<long>> ReadStore(Document doc, DataStorage? store)
        {
            if (store == null) return new Dictionary<string, List<long>>();

            try
            {
                var schema = GetOrCreateSchema();
                var entity = store.GetEntity(schema);
                if (!entity.IsValid()) return new Dictionary<string, List<long>>();

                var json = entity.Get<string>(FieldName);
                if (string.IsNullOrEmpty(json)) return new Dictionary<string, List<long>>();

                return JsonSerializer.Deserialize<Dictionary<string, List<long>>>(json)
                    ?? new Dictionary<string, List<long>>();
            }
            catch { return new Dictionary<string, List<long>>(); }
        }

        private static void WriteStore(Document doc, DataStorage store,
            Dictionary<string, List<long>> data)
        {
            var schema = GetOrCreateSchema();
            var entity = new Entity(schema);
            entity.Set(FieldName, JsonSerializer.Serialize(data));
            store.SetEntity(entity);
        }

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
    }
}
