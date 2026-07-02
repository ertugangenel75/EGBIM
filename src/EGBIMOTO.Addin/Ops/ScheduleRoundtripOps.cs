// Copyright 2026 Ertuğan Genel
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// ─────────────────────────────────────────────────────────────────────────────
// NOTICE: Bu dosyadaki schedule round-trip mimarisi — özellikle "display pass"
// (GetCellText ile birebir okuma) + "rolled-back UID anchor" deseni — açık kaynak
// Transom projesinin (Dave5264/transom-revit) tasarım dokümanlarından (SPEC.md,
// IMPLEMENTATION_PLAN.md) öğrenilen mimari yaklaşıma dayanır. Kod kopyalanmamış;
// EGBIMOTO'nun manifest/DAG op modeline ÖZGÜN olarak, çekirdek round-trip
// kapsamında (gruplu olmayan itemized schedule'lar) yeniden yazılmıştır.
//
// Transom'un ürün-detayı özellikleri (NPOI stil interning, renk sözleşmesi,
// gruplu schedule tip-parametre çakışması, MCP köprüsü) bilinçli olarak KAPSAM
// DIŞI bırakılmıştır — EGBIMOTO'nun mevcut export_xlsx + preview-confirm
// altyapısı kullanılır.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Addin.Host;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ScheduleRoundtripOps  —  EGBIMOTO v10
    //
    //  Revit schedule'larını (cetvel) Excel'e birebir çıkarıp, düzenlenmiş
    //  değerleri güvenle modele geri yazmak için round-trip op'ları.
    //
    //  Çekirdek mimari (Transom'dan öğrenilen):
    //   • DISPLAY PASS — hücreler ViewSchedule.GetCellText ile okunur, yani
    //     Revit ne gösteriyorsa birebir o (hesaplanmış/birleşik/ara-toplam dahil).
    //   • ANCHOR PASS — her eleman satırına UniqueId damgalanır. Bu, geçici bir
    //     schedule alanı ekleyip doc.Regenerate() yapıp OKUYUP ardından RollBack()
    //     ederek yapılır → model KALICI olarak değişmez.
    //   • IMPORT — satır↔eleman eşleşmesi UniqueId üzerinden; sadece yazılabilir
    //     alanlar, per-Set doğrulamayla yazılır.
    //
    //  Round-trip GÜVENLİ değilse (material takeoff, embedded, linked, anahtar
    //  olmayan çok-değerli) yalnızca display export yapılır, yazma engellenir.
    //
    //  Op'lar:
    //    schedule_export_anchored  → schedule'ı UID-anchor'lı List<Dictionary>'e çıkar
    //    schedule_roundtrip_diff   → düzenlenmiş satırları model ile karşılaştır (RAPOR)
    //    schedule_roundtrip_apply  → değişen değerleri güvenle modele yaz (YAZMA)
    //
    //  Manifest örneği:
    //    { "id": "cikar",  "op": "schedule_export_anchored",
    //      "params": { "schedule_name": "Kapı Tablosu" } }
    //    { "id": "excel",  "op": "export_xlsx", "from": "cikar",
    //      "params": { "sheet_name": "Kapi_Tablosu" } }
    //    --- kullanıcı Excel'i düzenler, CSV/dict olarak geri verir ---
    //    { "id": "fark",   "op": "schedule_roundtrip_diff",  "from": "duzenlenmis" }
    //    { "id": "uygula", "op": "schedule_roundtrip_apply",  "from": "fark" }
    // ═══════════════════════════════════════════════════════════════════════════

    public static class ScheduleRoundtripOps
    {
        // Anchor sütun başlığı — import bu sentinel'i ARAR, sütun index'ine güvenmez.
        private const string ANCHOR_SENTINEL = "__egbimoto_uid__";
        // Geçici damgalama için kullanılacak built-in (anlamlı bir param ezilmez)
        // Comments yerine COMMENTS kullanılır ama rollback ile geri alınır.

        // ─────────────────────────────────────────────────────────────────────
        //  OP 1: schedule_export_anchored   (RAPOR — kalıcı yazma yok)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("schedule_export_anchored",
            Description =
                "Bir Revit schedule'ını birebir görsel sadakatle (GetCellText) okur ve her\n" +
                "eleman satırına UniqueId anchor ekleyerek List<Dictionary> üretir.\n" +
                "Anchor, geçici alan + doc.Regenerate + RollBack ile alınır — MODEL KALICI DEĞİŞMEZ.\n" +
                "Çıktı export_xlsx ile zincirlenebilir. Round-trip güvenli değilse uyarı satırı eklenir.\n\n" +
                "params :\n" +
                "  schedule_name  — çıkarılacak schedule adı (zorunlu)\n" +
                "  include_anchor — 'true' (default). false ise sadece görünür sütunlar (round-trip kapalı).\n\n" +
                "Çıktı: List<Dictionary> — schedule satırları + (varsa) '" + ANCHOR_SENTINEL + "' sütunu.\n" +
                "İlk satır meta bilgisi taşımaz; her veri satırı görünür hücreleri + anchor'ı içerir.",
            Category = "Raporlama")]
        public static List<Dictionary<string, object?>> ScheduleExportAnchored(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[schedule_export_anchored] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            string schedName = ctx.GetString("schedule_name", "");
            bool includeAnchor = !ctx.GetString("include_anchor", "true")
                                     .Equals("false", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(schedName))
                throw new InvalidOperationException("[schedule_export_anchored] schedule_name zorunludur.");

            var vs = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(s => !s.IsTemplate
                                  && string.Equals(s.Name, schedName, StringComparison.OrdinalIgnoreCase));

            if (vs == null)
                throw new InvalidOperationException(
                    $"[schedule_export_anchored] '{schedName}' adlı schedule bulunamadı.");

            // Round-trip güvenli mi? (kind gating)
            bool roundTrippable = IsRoundTrippable(vs, out string kindNote);
            bool wantAnchor = includeAnchor && roundTrippable;

            ctx.Log($"[ScheduleRoundtrip] '{schedName}' — kind: {kindNote}, " +
                    $"round-trip: {(roundTrippable ? "EVET" : "HAYIR")}");

            // ── DISPLAY PASS: görünür hücreleri oku ──────────────────────────
            // ── ANCHOR PASS: geçici UID sütunu ekle, oku, rollback ──────────
            List<Dictionary<string, object?>> rows;

            if (wantAnchor)
                rows = ReadWithAnchor(doc, vs, ctx);
            else
                rows = ReadDisplayOnly(vs);

            // Round-trip kapalıysa kullanıcıyı bilgilendiren bir not (ilk satıra meta değil,
            // log + her satıra düşmeden tek seferlik uyarı log'u yeterli).
            if (!roundTrippable)
                ctx.Log($"[ScheduleRoundtrip] UYARI: Bu schedule round-trip için uygun değil " +
                        $"({kindNote}). Yalnızca görüntü çıktısı üretildi, geri yazma yapılamaz.");

            ctx.Log($"[ScheduleRoundtrip] {rows.Count} satır çıkarıldı" +
                    (wantAnchor ? " (anchor dahil)." : "."));
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OP 2: schedule_roundtrip_diff   (RAPOR — yazma yok)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("schedule_roundtrip_diff",
            Description =
                "Düzenlenmiş schedule satırlarını (anchor'lı) canlı model ile karşılaştırır.\n" +
                "Her satırdaki UniqueId ile elemanı bulur, hücre değerini model değeriyle kıyaslar.\n" +
                "Yazma yapmaz — değişiklik listesi üretir. Uygulamak için schedule_roundtrip_apply.\n\n" +
                "Input  : schedule_export_anchored çıktısı + kullanıcı düzenlemeleri (List<Dictionary>).\n" +
                "params :\n" +
                "  ignore_fields  — karşılaştırılmayacak alanlar (virgül, opsiyonel)\n\n" +
                "Çıktı: List<Dictionary> — her satır bir değişiklik adayı:\n" +
                "  uid, field, old_value, new_value, writable, binding, note",
            Category = "Raporlama")]
        public static List<Dictionary<string, object?>> ScheduleRoundtripDiff(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[schedule_roundtrip_diff] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            var editedRows = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>(
                new List<Dictionary<string, object?>>());
            var ignoreFields = ParseCsvSet(ctx.GetString("ignore_fields", ""));

            var changes = new List<Dictionary<string, object?>>();

            foreach (var row in editedRows)
            {
                // Anchor'ı bul (sentinel başlık)
                if (!row.TryGetValue(ANCHOR_SENTINEL, out var uidObj)) continue;
                string uid = uidObj?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(uid)) continue; // grup/toplam satırı

                Element? elem = null;
                try { elem = doc.GetElement(uid); } catch { }
                if (elem == null)
                {
                    changes.Add(MakeChange(uid, "?", "", "", false, "?",
                        "Eleman bulunamadı (silinmiş veya elle eklenmiş satır)."));
                    continue;
                }

                foreach (var kv in row)
                {
                    string field = kv.Key;
                    if (field == ANCHOR_SENTINEL) continue;
                    if (ignoreFields.Contains(field)) continue;

                    string newVal = kv.Value?.ToString() ?? "";

                    // Modeldeki mevcut değer (instance önce, sonra type)
                    var (param, binding) = ResolveWritableParam(doc, elem, field);
                    if (param == null)
                    {
                        // Yazılamaz alan — değişiklik adayı değil, sadece bilgi
                        continue;
                    }

                    string oldVal = ParamValueAsString(param);
                    bool writable = !param.IsReadOnly;

                    if (!string.Equals(oldVal.Trim(), newVal.Trim(), StringComparison.Ordinal))
                    {
                        changes.Add(MakeChange(uid, field, oldVal, newVal, writable, binding,
                            writable ? "" : "Salt-okunur — atlanacak."));
                    }
                }
            }

            ctx.Log($"[ScheduleRoundtrip] {changes.Count} değişiklik adayı tespit edildi.");
            return changes;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OP 3: schedule_roundtrip_apply   (YAZMA)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("schedule_roundtrip_apply",
            Description =
                "schedule_roundtrip_diff çıktısındaki değişiklikleri güvenle modele yazar.\n" +
                "Her yazımdan sonra değeri yeniden okuyarak doğrular (sessiz Set hatalarına karşı).\n" +
                "DİKKAT: Model değişikliği yapar. Tip parametresi yazımı tüm tipi etkiler.\n\n" +
                "Input  : schedule_roundtrip_diff çıktısı (List<Dictionary>) — from ile bağlanır.\n" +
                "params :\n" +
                "  apply_type_params  — 'true' (default). false ise tip parametreleri atlanır (güvenli mod).\n\n" +
                "Çıktı: List<Dictionary> — uid, field, old_value, new_value, status (ok|skip|fail), message",
            Category = "Raporlama",
            RequiresTransaction = true)]
        public static List<Dictionary<string, object?>> ScheduleRoundtripApply(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[schedule_roundtrip_apply] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            var changes = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>(
                new List<Dictionary<string, object?>>());
            bool applyTypeParams = !ctx.GetString("apply_type_params", "true")
                                      .Equals("false", StringComparison.OrdinalIgnoreCase);

            var results = new List<Dictionary<string, object?>>();
            int applied = 0;

            using var scope = new RevitWriteScope(doc, "Schedule Round-trip Yaz", rctx.IsAtomicMode);

            foreach (var ch in changes)
            {
                string uid     = ch.GetValueOrDefault("uid")?.ToString() ?? "";
                string field   = ch.GetValueOrDefault("field")?.ToString() ?? "";
                string newVal  = ch.GetValueOrDefault("new_value")?.ToString() ?? "";
                string binding = ch.GetValueOrDefault("binding")?.ToString() ?? "";
                bool writable  = ToBool(ch.GetValueOrDefault("writable"));

                var res = new Dictionary<string, object?>
                {
                    ["uid"]       = uid,
                    ["field"]     = field,
                    ["old_value"] = ch.GetValueOrDefault("old_value"),
                    ["new_value"] = newVal,
                    ["status"]    = "skip",
                    ["message"]   = "",
                };

                if (!writable)
                {
                    res["message"] = "Salt-okunur alan.";
                    results.Add(res); continue;
                }
                if (binding == "type" && !applyTypeParams)
                {
                    res["message"] = "Tip parametresi (güvenli mod: atlandı).";
                    results.Add(res); continue;
                }

                try
                {
                    var elem = doc.GetElement(uid);
                    if (elem == null)
                    {
                        res["message"] = "Eleman bulunamadı.";
                        results.Add(res); continue;
                    }

                    var (param, _) = ResolveWritableParam(doc, elem, field);
                    if (param == null || param.IsReadOnly)
                    {
                        res["message"] = "Yazılabilir parametre bulunamadı.";
                        results.Add(res); continue;
                    }

                    bool setOk = SetParamFromString(param, newVal);
                    if (!setOk)
                    {
                        res["status"]  = "fail";
                        res["message"] = "Set başarısız (tip uyumsuz veya ayrıştırılamadı).";
                        results.Add(res); continue;
                    }

                    // ── DOĞRULAMA: yeniden oku ───────────────────────────────
                    string confirm = ParamValueAsString(param);
                    if (string.Equals(confirm.Trim(), newVal.Trim(), StringComparison.Ordinal)
                        || ValuesNumericallyEqual(confirm, newVal))
                    {
                        res["status"]  = "ok";
                        res["message"] = "Yazıldı ve doğrulandı.";
                        applied++;
                    }
                    else
                    {
                        res["status"]  = "fail";
                        res["message"] = $"Yazıldı ama doğrulama uyuşmadı (model: {confirm}).";
                    }
                }
                catch (Exception ex)
                {
                    res["status"]  = "fail";
                    res["message"] = ex.Message;
                }

                results.Add(res);
            }

            scope.Commit();
            ctx.Log($"[ScheduleRoundtrip] {applied} değişiklik yazıldı ve doğrulandı.");
            return results;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  DISPLAY PASS + ANCHOR PASS
        // ═══════════════════════════════════════════════════════════════════════

        // Geçici UID alanı ekleyip doc.Regenerate ile tabloyu yeniler, görünür
        // hücreleri + UID sütununu AYNI display sırasında okur, sonra RollBack eder.
        // Model kalıcı olarak değişmez.
        private static List<Dictionary<string, object?>> ReadWithAnchor(
            Document doc, ViewSchedule vs, OpContext ctx)
        {
            var rows = new List<Dictionary<string, object?>>();

            // Anchor mekanizması: her satır elemanına geçici olarak UniqueId'sini
            // bir built-in metin parametresine (ALL_MODEL_INSTANCE_COMMENTS) yazıp,
            // o alanı schedule'a ekleyip okuruz; transaction rollback ile geri alınır.
            // NOT: Comments anlamlı veri taşıyabilir — bu yüzden ROLLBACK kritiktir.

            var def = vs.Definition;
            BuiltInParameter stampBip = BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS;

            // Schedule'daki eleman satırlarını topla (display sırasında)
            var elements = new FilteredElementCollector(doc, vs.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            // Eleman bazlı UID haritası (Comments'a damgalanacak)
            var idToUid = new Dictionary<ElementId, string>();
            foreach (var el in elements)
                idToUid[el.Id] = el.UniqueId;

            // Geçici transaction: damgala + alan ekle + regenerate + oku + ROLLBACK
            using (var t = new Transaction(doc, "EGBIMOTO: schedule anchor (geçici)"))
            {
                t.Start();
                try
                {
                    // 1) Her elemanın Comments'ına UID damgala (orijinali rollback geri alır)
                    foreach (var el in elements)
                    {
                        var p = el.get_Parameter(stampBip);
                        if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                            p.Set("EGUID:" + el.UniqueId);
                    }

                    // 2) Comments alanını schedule'a ekle (zaten varsa tekrar eklenmez)
                    //    AddField(SchedulableField) doğru imzadır.
                    var commentsSf = FindSchedulableField(def, stampBip);
                    if (commentsSf != null && !FieldAlreadyShown(def, commentsSf.ParameterId))
                    {
                        def.AddField(commentsSf);
                    }

                    // 3) Regenerate — tablo yeni sütunu ve değerleri yansıtsın
                    doc.Regenerate();

                    // 4) Görünür hücreleri + UID sütununu oku (aynı display sırası)
                    rows = ReadTableWithUidColumn(vs);

                    // 5) ROLLBACK — model dokunulmamış kalır
                }
                catch (Exception ex)
                {
                    ctx.Log($"[ScheduleRoundtrip] Anchor okuma hatası: {ex.Message}. " +
                            "Display-only çıktıya düşülüyor.");
                    t.RollBack();
                    return ReadDisplayOnly(vs);
                }
                t.RollBack(); // her durumda geri al
            }

            return rows;
        }

        // Sadece görünür hücreleri okur (round-trip yok).
        private static List<Dictionary<string, object?>> ReadDisplayOnly(ViewSchedule vs)
        {
            var rows = new List<Dictionary<string, object?>>();
            var td = vs.GetTableData();
            var body = td.GetSectionData(SectionType.Body);
            if (body == null) return rows;

            int nRows = body.NumberOfRows;
            int nCols = body.NumberOfColumns;
            if (nRows < 1 || nCols < 1) return rows;

            // Row 0 = sütun başlıkları
            var headers = new List<string>();
            for (int c = 0; c < nCols; c++)
                headers.Add(SafeCellText(vs, SectionType.Body, 0, c, $"Sütun{c + 1}"));

            for (int r = 1; r < nRows; r++)
            {
                var dict = new Dictionary<string, object?>();
                for (int c = 0; c < nCols; c++)
                    dict[UniqueHeader(headers, c)] = SafeCellText(vs, SectionType.Body, r, c, "");
                rows.Add(dict);
            }
            return rows;
        }

        // Anchor sütununu (Comments=EGUID:...) tespit edip ayrı sentinel sütununa taşır.
        private static List<Dictionary<string, object?>> ReadTableWithUidColumn(ViewSchedule vs)
        {
            var rows = new List<Dictionary<string, object?>>();
            var td = vs.GetTableData();
            var body = td.GetSectionData(SectionType.Body);
            if (body == null) return rows;

            int nRows = body.NumberOfRows;
            int nCols = body.NumberOfColumns;
            if (nRows < 1 || nCols < 1) return rows;

            var headers = new List<string>();
            for (int c = 0; c < nCols; c++)
                headers.Add(SafeCellText(vs, SectionType.Body, 0, c, $"Sütun{c + 1}"));

            for (int r = 1; r < nRows; r++)
            {
                var dict = new Dictionary<string, object?>();
                string uid = "";

                for (int c = 0; c < nCols; c++)
                {
                    string cell = SafeCellText(vs, SectionType.Body, r, c, "");
                    // UID damgası bu hücrede mi?
                    if (cell.StartsWith("EGUID:", StringComparison.Ordinal))
                    {
                        uid = cell.Substring("EGUID:".Length);
                        // Bu sütunu görünür çıktıya KOYMA (geçici anchor)
                        continue;
                    }
                    dict[UniqueHeader(headers, c)] = cell;
                }

                // Anchor sentinel sütununu ekle (boşsa grup/toplam satırı)
                dict[ANCHOR_SENTINEL] = uid;
                rows.Add(dict);
            }
            return rows;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  KIND GATING — round-trip güvenli mi?
        // ═══════════════════════════════════════════════════════════════════════

        private static bool IsRoundTrippable(ViewSchedule vs, out string note)
        {
            var def = vs.Definition;

            if (def.IsMaterialTakeoff)
            { note = "Malzeme metrajı (satırlar eleman değil)"; return false; }

            try
            {
                if (def.IsKeySchedule)
                { note = "Anahtar schedule (round-trip uygun)"; return true; }
            }
            catch { }

            // Embedded schedule
            try
            {
                if (def.GetType().GetProperty("EmbeddedDefinition")?.GetValue(def) != null)
                { note = "Gömülü schedule (collector erişemez)"; return false; }
            }
            catch { }

            // Non-itemized → satır başına eleman yok
            try
            {
                if (!def.IsItemized)
                { note = "Itemized değil (satır başına tek eleman yok)"; return false; }
            }
            catch { }

            note = "Standart itemized (round-trip uygun)";
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PARAMETRE ÇÖZÜMLEME — type vs instance + writability
        // ═══════════════════════════════════════════════════════════════════════

        // Alan adına göre instance veya type parametresini bulur. Building Coder
        // tekniği: aynı isim bir ailede instance, başkasında type olabilir → her
        // eleman için ayrı çözümlenir.
        private static (Parameter? param, string binding) ResolveWritableParam(
            Document doc, Element elem, string fieldName)
        {
            // 1) Instance parametresi
            var inst = elem.LookupParameter(fieldName);
            if (inst != null && !inst.IsReadOnly)
                return (inst, "instance");

            // 2) Type parametresi
            var typeId = elem.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var typeElem = doc.GetElement(typeId);
                var tp = typeElem?.LookupParameter(fieldName);
                if (tp != null && !tp.IsReadOnly)
                    return (tp, "type");
            }

            // 3) Salt-okunur instance bulunduysa onu döndür (writable=false işaretlenir)
            if (inst != null) return (inst, "instance");
            return (null, "?");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PARAMETRE OKUMA / YAZMA
        // ═══════════════════════════════════════════════════════════════════════

        private static string ParamValueAsString(Parameter p)
        {
            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        return p.AsString() ?? "";
                    case StorageType.Integer:
                        return p.AsInteger().ToString();
                    case StorageType.Double:
                        // Görünür birimde değil ama karşılaştırma için ham değer.
                        // AsValueString varsa onu tercih et (görünür biçim).
                        var vs = p.AsValueString();
                        return !string.IsNullOrEmpty(vs) ? vs : p.AsDouble().ToString();
                    case StorageType.ElementId:
                        var id = p.AsElementId();
                        return id != null ? id.ToString() : "";
                    default:
                        return "";
                }
            }
            catch { return ""; }
        }

        private static bool SetParamFromString(Parameter p, string val)
        {
            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        return p.Set(val);
                    case StorageType.Integer:
                        if (int.TryParse(val, out var iv)) return p.Set(iv);
                        // "Yes/No" / "Evet/Hayır" → 1/0
                        if (val.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                            || val.Equals("Evet", StringComparison.OrdinalIgnoreCase)) return p.Set(1);
                        if (val.Equals("No", StringComparison.OrdinalIgnoreCase)
                            || val.Equals("Hayır", StringComparison.OrdinalIgnoreCase)) return p.Set(0);
                        return false;
                    case StorageType.Double:
                        // Önce görünür biçimle dene (birim ayrıştırma), olmazsa ham double.
                        if (p.SetValueString(val)) return true;
                        if (double.TryParse(val, out var dv)) return p.Set(dv);
                        return false;
                    case StorageType.ElementId:
                        // Eleman id'si string olarak gelirse — güvenli değil, atla.
                        return false;
                    default:
                        return false;
                }
            }
            catch { return false; }
        }

        private static bool ValuesNumericallyEqual(string a, string b)
        {
            // Birim sonekli değerler ("3000 mm" vs "3000") için kaba sayısal kıyas.
            double na = ExtractFirstNumber(a);
            double nb = ExtractFirstNumber(b);
            if (double.IsNaN(na) || double.IsNaN(nb)) return false;
            return Math.Abs(na - nb) < 1e-6;
        }

        private static double ExtractFirstNumber(string s)
        {
            var cleaned = new string(s.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-' || ch == ',').ToArray())
                          .Replace(",", ".");
            return double.TryParse(cleaned, out var v) ? v : double.NaN;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SCHEDULE ALAN YARDIMCILARI
        // ═══════════════════════════════════════════════════════════════════════

        private static SchedulableField? FindSchedulableField(ScheduleDefinition def, BuiltInParameter bip)
        {
            try
            {
                // BuiltInParameter → ElementId: Revit API'de BuiltInParameter için
                // özel new ElementId(BuiltInParameter) overload'ı tüm sürümlerde geçerli.
                var bipId = new ElementId(bip);
                foreach (var sf in def.GetSchedulableFields())
                {
                    if (sf.ParameterId == bipId)
                        return sf;
                }
            }
            catch { }
            return null;
        }

        private static bool FieldAlreadyShown(ScheduleDefinition def, ElementId paramId)
        {
            try
            {
                for (int i = 0; i < def.GetFieldCount(); i++)
                {
                    var f = def.GetField(i);
                    if (f.ParameterId == paramId) return true;
                }
            }
            catch { }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  GENEL YARDIMCILAR
        // ═══════════════════════════════════════════════════════════════════════

        private static string SafeCellText(ViewSchedule vs, SectionType sec, int r, int c, string fallback)
        {
            try
            {
                string t = vs.GetCellText(sec, r, c);
                return t ?? fallback;
            }
            catch { return fallback; }
        }

        // Aynı isimli sütun başlıklarını teklersin (Excel/dict anahtarı çakışmasın)
        private static string UniqueHeader(List<string> headers, int col)
        {
            string h = string.IsNullOrWhiteSpace(headers[col]) ? $"Sütun{col + 1}" : headers[col];
            int dupCount = 0;
            for (int i = 0; i < col; i++)
                if (string.Equals(headers[i], headers[col], StringComparison.Ordinal)) dupCount++;
            return dupCount == 0 ? h : $"{h}_{dupCount + 1}";
        }

        private static Dictionary<string, object?> MakeChange(
            string uid, string field, string oldVal, string newVal,
            bool writable, string binding, string note)
            => new Dictionary<string, object?>
            {
                ["uid"]       = uid,
                ["field"]     = field,
                ["old_value"] = oldVal,
                ["new_value"] = newVal,
                ["writable"]  = writable,
                ["binding"]   = binding,
                ["note"]      = note,
            };

        private static HashSet<string> ParseCsvSet(string csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                set.Add(raw.Trim());
            return set;
        }

        private static bool ToBool(object? o)
        {
            if (o is bool b) return b;
            var s = o?.ToString();
            return s != null && (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1");
        }
    }
}
