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
    /// EGBIMOTO Oda Kaplama Operasyonları — v9
    ///
    /// Oda bazlı kaplama tipi atama, malzeme eşleştirme ve metraj.
    /// EGBIM RuleOS mantığını EGBIMOTO op paradigmasına taşır.
    ///
    /// Op listesi:
    ///   room_finish_assign     — Excel/JSON kuralına göre oda kaplaması ata
    ///   room_finish_validate   — Kaplama parametrelerini doğrula
    ///   room_finish_matrix     — Oda-kaplama matris tablosu oluştur
    ///   room_area_breakdown    — Oda alan dökümü (taban, duvar, tavan)
    ///   room_naming_normalize  — Oda isimlerini standarda göre normalize et
    ///   room_to_ifc_space      — Odaları IFC Space olarak etiketle
    /// </summary>
    public static class RoomFinishOps
    {
        private const string PARAM_ZEMIN_KAPLAMA   = "EG_ZeminKaplama";
        private const string PARAM_DUVAR_KAPLAMA   = "EG_DuvarKaplama";
        private const string PARAM_TAVAN_KAPLAMA   = "EG_TavanKaplama";
        private const string PARAM_ODA_FONKSIYON   = "EG_OdaFonksiyon";
        private const string PARAM_IFC_SPACE_TYPE  = "EG_IfcSpaceTip";

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("room_finish_assign",
            Description =
                "Oda ismi veya fonksiyon parametresine göre kaplama tiplerini toplu atar.\n" +
                "params: rules (zorunlu) — List<Dictionary> kural listesi:\n" +
                "  [{\"oda_pattern\":\"BANYO\",\"zemin\":\"Seramik\",\"duvar\":\"Fayans\",\"tavan\":\"Saten Boya\"}]\n" +
                "  oda_pattern: regex veya tam eşleşme\n" +
                "Input: collect_rooms çıktısı (List<Element>).\n" +
                "Çıktı: Dictionary — yazilan_count, atlanan_count.",
            Category = "Oda",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> RoomFinishAssign(OpContext ctx)
        {
            var rctx  = (RevitOpContext)ctx;
            var doc   = rctx.Doc;
            var rooms = ctx.InputAs<List<Element>>();
            var rules = ctx.GetList<Dictionary<string, object?>>("rules");

            if (!rules.Any())
                throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] 'rules' parametresi boş. " +
                    "En az bir kaplama kuralı tanımlayın.");

            int yazilan = 0, atlanan = 0, hata = 0;

            using var tx = new Transaction(doc, "EGBIMOTO: Oda Kaplama Atama");
            tx.Start();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                var odaIsmi = room.Name ?? "";
                var odaFon  = room.LookupParameter(PARAM_ODA_FONKSIYON)?.AsString() ?? "";
                var araStr  = string.IsNullOrEmpty(odaFon) ? odaIsmi : odaFon;

                // İlk eşleşen kuralı uygula
                bool matched = false;
                foreach (var rule in rules)
                {
                    var pattern = rule.TryGetValue("oda_pattern", out var p) ? p?.ToString() ?? "" : "";
                    if (string.IsNullOrEmpty(pattern)) continue;

                    bool isMatch;
                    try   { isMatch = System.Text.RegularExpressions.Regex.IsMatch(
                                araStr, pattern,
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
                    catch { isMatch = string.Equals(araStr, pattern, StringComparison.OrdinalIgnoreCase); }

                    if (!isMatch) continue;

                    try
                    {
                        WriteRoomFinish(room, PARAM_ZEMIN_KAPLAMA,
                            rule.TryGetValue("zemin", out var z) ? z?.ToString() : null);
                        WriteRoomFinish(room, PARAM_DUVAR_KAPLAMA,
                            rule.TryGetValue("duvar", out var d) ? d?.ToString() : null);
                        WriteRoomFinish(room, PARAM_TAVAN_KAPLAMA,
                            rule.TryGetValue("tavan", out var t) ? t?.ToString() : null);

                        yazilan++;
                        matched = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        ctx.Log($"  ✗ [{room.Id}] '{odaIsmi}' yazma hatası: {ex.Message}");
                        hata++;
                        matched = true;
                        break;
                    }
                }

                if (!matched) atlanan++;
            }

            tx.Commit();

            ctx.Log($"  → {yazilan} oda yazıldı, {atlanan} atlandı (kural yok), {hata} hata");
            return new Dictionary<string, object?>
            {
                ["yazilan_count"]  = yazilan,
                ["atlanan_count"]  = atlanan,
                ["hata_count"]     = hata,
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("room_finish_validate",
            Description =
                "Odaların kaplama parametrelerinin dolu olup olmadığını kontrol eder.\n" +
                "params: required_params (opsiyonel, default: zemin+duvar+tavan tümü).\n" +
                "Input: collect_rooms çıktısı.\n" +
                "Çıktı: List<Dictionary> — eksik parametreli oda kayıtları.",
            Category = "Oda")]
        public static List<Dictionary<string, object?>> RoomFinishValidate(OpContext ctx)
        {
            var rooms          = ctx.InputAs<List<Element>>();
            var requiredParams = ctx.GetList<string>("required_params");
            if (!requiredParams.Any())
                requiredParams = new List<string>
                    { PARAM_ZEMIN_KAPLAMA, PARAM_DUVAR_KAPLAMA, PARAM_TAVAN_KAPLAMA };

            var eksikler = new List<Dictionary<string, object?>>();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                var eksikParamlar = requiredParams
                    .Where(pn =>
                    {
                        var p = room.LookupParameter(pn);
                        return p == null || string.IsNullOrWhiteSpace(p.AsString());
                    })
                    .ToList();

                if (eksikParamlar.Any())
                    eksikler.Add(new Dictionary<string, object?>
                    {
                        ["room_id"]       = room.Id.Value,
                        ["room_name"]     = room.Name,
                        ["room_number"]   = room.Number,
                        ["eksik_paramlar"]= string.Join(", ", eksikParamlar),
                        ["seviye"]        = "UYARI",
                    });
            }

            ctx.Log($"  → {eksikler.Count} odada eksik kaplama parametresi");
            return eksikler;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("room_finish_matrix",
            Description =
                "Tüm odaları kaplama bilgileriyle tablo haline getirir.\n" +
                "Input: collect_rooms çıktısı.\n" +
                "Çıktı: List<Dictionary> — oda, zemin, duvar, tavan, alan.",
            Category = "Oda")]
        public static List<Dictionary<string, object?>> RoomFinishMatrix(OpContext ctx)
        {
            var rctx  = (RevitOpContext)ctx;
            var doc   = rctx.Doc;
            var rooms = ctx.InputAs<List<Element>>();

            var rows = new List<Dictionary<string, object?>>();

            foreach (var el in rooms)
            {
                if (el is not Room room || room.Area < 0.01) continue;

                var areaM2 = Math.Round(
                    UnitUtils.ConvertFromInternalUnits(room.Area, UnitTypeId.SquareMeters), 3);

                var level = doc.GetElement(room.LevelId) as Level;

                rows.Add(new Dictionary<string, object?>
                {
                    ["oda_no"]         = room.Number,
                    ["oda_ismi"]       = room.Name,
                    ["kat"]            = level?.Name ?? "—",
                    ["alan_m2"]        = areaM2,
                    ["zemin_kaplama"]  = room.LookupParameter(PARAM_ZEMIN_KAPLAMA)?.AsString() ?? "",
                    ["duvar_kaplama"]  = room.LookupParameter(PARAM_DUVAR_KAPLAMA)?.AsString() ?? "",
                    ["tavan_kaplama"]  = room.LookupParameter(PARAM_TAVAN_KAPLAMA)?.AsString() ?? "",
                    ["fonksiyon"]      = room.LookupParameter(PARAM_ODA_FONKSIYON)?.AsString() ?? "",
                });
            }

            ctx.Log($"  → {rows.Count} oda kaplama matrisi oluşturuldu");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("room_area_breakdown",
            Description =
                "Oda bazlı taban, duvar ve tavan alanlarını hesaplar.\n" +
                "Duvar alanı: oda çevre uzunluğu × kat yüksekliği - kapı/pencere açıklıkları.\n" +
                "Input: collect_rooms çıktısı.\n" +
                "Çıktı: List<Dictionary> — oda ve alan dökümleri.",
            Category = "Oda")]
        public static List<Dictionary<string, object?>> RoomAreaBreakdown(OpContext ctx)
        {
            var rctx  = (RevitOpContext)ctx;
            var doc   = rctx.Doc;
            var rooms = ctx.InputAs<List<Element>>();

            var rows = new List<Dictionary<string, object?>>();

            foreach (var el in rooms)
            {
                if (el is not Room room || room.Area < 0.01) continue;

                var tabanM2 = UnitUtils.ConvertFromInternalUnits(room.Area, UnitTypeId.SquareMeters);
                var cevre   = UnitUtils.ConvertFromInternalUnits(room.Perimeter, UnitTypeId.Meters);

                // Kat yüksekliği
                var katYuksekligi = room.UnboundedHeight > 0
                    ? UnitUtils.ConvertFromInternalUnits(room.UnboundedHeight, UnitTypeId.Meters)
                    : 2.80; // varsayılan

                var brütDuvarM2 = cevre * katYuksekligi;

                // Kapı ve pencere alanlarını çıkar
                double aciklikM2 = 0;
                var insertIds = room.GetBoundarySegments(new SpatialElementBoundaryOptions())
                    ?.SelectMany(seg => seg)
                    .Where(s => s.ElementId != ElementId.InvalidElementId)
                    .Select(s => doc.GetElement(s.ElementId))
                    .Where(e => e is FamilyInstance fi &&
                           (fi.Category?.Id.Value == (int)BuiltInCategory.OST_Doors ||
                            fi.Category?.Id.Value == (int)BuiltInCategory.OST_Windows))
                    .ToList() ?? new List<Element>();

                foreach (var insert in insertIds)
                {
                    var areaParam = insert.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null)
                        aciklikM2 += UnitUtils.ConvertFromInternalUnits(
                            areaParam.AsDouble(), UnitTypeId.SquareMeters);
                }

                var netDuvarM2  = Math.Max(0, brütDuvarM2 - aciklikM2);
                var tavanM2     = tabanM2; // tavan = taban (düz tavan varsayımı)

                rows.Add(new Dictionary<string, object?>
                {
                    ["oda_no"]           = room.Number,
                    ["oda_ismi"]         = room.Name,
                    ["taban_m2"]         = Math.Round(tabanM2, 3),
                    ["tavan_m2"]         = Math.Round(tavanM2, 3),
                    ["brut_duvar_m2"]    = Math.Round(brütDuvarM2, 3),
                    ["aciklik_m2"]       = Math.Round(aciklikM2, 3),
                    ["net_duvar_m2"]     = Math.Round(netDuvarM2, 3),
                    ["kat_yuksekligi_m"] = Math.Round(katYuksekligi, 2),
                    ["cevre_m"]          = Math.Round(cevre, 3),
                });
            }

            ctx.Log($"  → {rows.Count} oda alan dökümü");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("room_naming_normalize",
            Description =
                "Oda isimlerini EGBIM standardına göre normalize eder.\n" +
                "Kural: BÜYÜK HARF, Türkçe karakterler korunur, fazla boşluk temizlenir.\n" +
                "params: prefix (opsiyonel) — oda adı öneki.\n" +
                "Input: collect_rooms çıktısı.\n" +
                "Çıktı: Dictionary — degistirilen_count.",
            Category = "Oda",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> RoomNamingNormalize(OpContext ctx)
        {
            var rctx   = (RevitOpContext)ctx;
            var doc    = rctx.Doc;
            var rooms  = ctx.InputAs<List<Element>>();
            var prefix = ctx.GetString("prefix", "");

            int degistirilen = 0;

            using var tx = new Transaction(doc, "EGBIMOTO: Oda İsmi Normalize");
            tx.Start();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                var mevcutIsim = room.Name ?? "";
                // Normalize: büyük harf + fazla boşluk temizle
                var yeniIsim   = (prefix + System.Text.RegularExpressions.Regex
                    .Replace(mevcutIsim.ToUpperInvariant().Trim(), @"\s+", " "));

                if (!string.Equals(mevcutIsim, yeniIsim, StringComparison.Ordinal))
                {
                    try
                    {
                        room.Name = yeniIsim;
                        degistirilen++;
                    }
                    catch (Exception ex)
                    {
                        ctx.Log($"  ✗ [{room.Id}] isim değiştirme hatası: {ex.Message}");
                    }
                }
            }

            tx.Commit();
            ctx.Log($"  → {degistirilen} oda ismi normalize edildi");
            return new Dictionary<string, object?> { ["degistirilen_count"] = degistirilen };
        }

        // ─────────────────────────────────────────────────────────────────────

        [EgOp("room_to_ifc_space",
            Description =
                "Oda fonksiyon tipini IFC Space tipine eşler ve EG_IfcSpaceTip parametresini yazar.\n" +
                "Mapping: OFIS→IfcOffice, TOPLANTI→IfcMeetingRoom, BANYO→IfcSanitary, vb.\n" +
                "Input: collect_rooms çıktısı.\n" +
                "Çıktı: Dictionary — eslestirilen_count.",
            Category = "Oda",
            RequiresTransaction = true)]
        public static Dictionary<string, object?> RoomToIfcSpace(OpContext ctx)
        {
            var rctx  = (RevitOpContext)ctx;
            var doc   = rctx.Doc;
            var rooms = ctx.InputAs<List<Element>>();

            // Varsayılan IFC Space mapping tablosu
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "OFİS",          "IfcSpace.OFFICE" },
                { "OFIS",          "IfcSpace.OFFICE" },
                { "TOPLANTI",      "IfcSpace.MEETINGROOM" },
                { "KORIDOR",       "IfcSpace.CORRIDOR" },
                { "BANYO",         "IfcSpace.SANITARY" },
                { "TUVALET",       "IfcSpace.SANITARY" },
                { "WC",            "IfcSpace.SANITARY" },
                { "MUTFAK",        "IfcSpace.KITCHEN" },
                { "DEPO",          "IfcSpace.STORAGE" },
                { "MERDIVEN",      "IfcSpace.STAIRCASE" },
                { "ASANSÖR",       "IfcSpace.LIFT" },
                { "ASANSOR",       "IfcSpace.LIFT" },
                { "SINIF",         "IfcSpace.CLASSROOM" },
                { "TEKNIK",        "IfcSpace.PLANTROOM" },
                { "KAZAN",         "IfcSpace.PLANTROOM" },
                { "LOBİ",          "IfcSpace.LOBBY" },
                { "LOBI",          "IfcSpace.LOBBY" },
                { "OTOPARK",       "IfcSpace.PARKING" },
                { "YATAK ODASI",   "IfcSpace.BEDROOM" },
                { "SALON",         "IfcSpace.LIVINGROOM" },
            };

            // Özel mapping varsa merge et
            var customMapping = ctx.GetList<Dictionary<string, object?>>("custom_mapping");
            foreach (var cm in customMapping)
            {
                if (cm.TryGetValue("oda", out var od) && cm.TryGetValue("ifc", out var ifc))
                    mapping[od?.ToString() ?? ""] = ifc?.ToString() ?? "";
            }

            int eslestirilen = 0, bulunamadi = 0;

            using var tx = new Transaction(doc, "EGBIMOTO: Oda IFC Space Etiketleme");
            tx.Start();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;

                var odaIsmi  = (room.Name ?? "").ToUpperInvariant();
                var fonksiyon = room.LookupParameter(PARAM_ODA_FONKSIYON)?.AsString()?.ToUpperInvariant() ?? "";
                var araStr   = string.IsNullOrEmpty(fonksiyon) ? odaIsmi : fonksiyon;

                // İlk eşleşen mapping'i bul
                string? ifcTip = null;
                foreach (var kv in mapping)
                {
                    if (araStr.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        ifcTip = kv.Value;
                        break;
                    }
                }

                if (ifcTip == null) { bulunamadi++; continue; }

                var p = room.LookupParameter(PARAM_IFC_SPACE_TYPE);
                if (p != null && !p.IsReadOnly)
                {
                    p.Set(ifcTip);
                    eslestirilen++;
                }
            }

            tx.Commit();
            ctx.Log($"  → {eslestirilen} oda IFC Space etiketlendi, {bulunamadi} eşleşme bulunamadı");
            return new Dictionary<string, object?>
            {
                ["eslestirilen_count"] = eslestirilen,
                ["bulunamadi_count"]   = bulunamadi,
            };
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static void WriteRoomFinish(Room room, string paramName, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var p = room.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly)
                p.Set(value);
        }
    }
}
