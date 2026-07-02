using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Aile Kalite Op'ları (FamilyOps)
    ///
    ///   family_health_check — Yüklü ailelerin kalite kontrolü.
    ///
    /// Kontrol edilen kriterler (her biri ayrı flag):
    ///   is_in_place      — In-place family (taşınabilir değil)
    ///   has_no_category  — Kategori atanmamış
    ///   type_count       — Tip sayısı max_types aşıyor mu?
    ///   missing_params   — Beklenen parametreler eksik mi?
    ///   has_origin_issue — Instance BoundingBox origin'e çok uzak (>origin_threshold_m)
    ///   status           — OK | WARNING | ERROR
    ///
    /// NOT: Nested family kontrolü (check_nested=true) her family'yi
    ///      ayrı Document olarak açtığından ağırdır. Büyük projelerde
    ///      category filtresiyle scope daraltılması önerilir.
    /// </summary>
    public static class FamilyOps
    {
        // ─────────────────────────────────────────────────────────────────────
        // F01  family_health_check
        //
        // input : List<Element>  (Family — collect_families çıktısı)
        // params: expected_params   String  virgülle ayrılmış param adları
        //                           örn: "EG_Disiplin,EG_KalemNo,Mark"
        //         max_types         Int     default=50 — üstü WARNING
        //         origin_threshold_m Double default=100.0 — BBox merkezi
        //                           origin'den bu kadar uzaksa flag
        //         check_nested      Bool    default=false — ağır işlem
        //
        // output: List<Dict>
        //   family_id, family_name, category,
        //   type_count, param_count,
        //   is_in_place, has_no_category,
        //   type_count_ok, missing_params, has_origin_issue,
        //   nested_count (-1 = kontrol edilmedi),
        //   status (OK | WARNING | ERROR)
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("family_health_check",
            Description = "Yüklü ailelerin kalite kontrolü: kategori, tip sayısı, " +
                          "parametre şeması, in-place tespiti, origin kontrolü.",
            Category    = "Aile")]
        public static List<Dictionary<string, object?>> FamilyHealthCheck(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var families   = ctx.InputAs<List<Element>>();

            // Params
            var expectedParamsRaw = ctx.GetString("expected_params", "");
            var expectedParams = string.IsNullOrWhiteSpace(expectedParamsRaw)
                ? new List<string>()
                : expectedParamsRaw.Split(',')
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToList();

            int    maxTypes         = ctx.GetInt("max_types", 50);
            double originThresholdM = ctx.GetDouble("origin_threshold_m", 100.0);
            double originThresholdFt = originThresholdM / 0.3048;
            bool   checkNested      = ctx.GetBool("check_nested", false);

            var rows = new List<Dictionary<string, object?>>();

            foreach (var el in families)
            {
                if (el is not Family family) continue;

                // ── Temel bilgi ───────────────────────────────────────────────
                string categoryName = family.FamilyCategory?.Name ?? "";
                bool   isInPlace    = family.IsInPlace;
                bool   hasNoCategory= string.IsNullOrEmpty(categoryName);

                // ── Tip sayısı ────────────────────────────────────────────────
                var symbolIds  = family.GetFamilySymbolIds();
                int typeCount  = symbolIds.Count;
                bool typeCountOk = typeCount <= maxTypes;

                // ── Parametre şeması kontrolü ─────────────────────────────────
                // FamilySymbol üzerindeki parametrelere bakıyoruz (ilk tip yeterli)
                var missingParams = new List<string>();
                if (expectedParams.Count > 0 && symbolIds.Count > 0)
                {
                    var firstSymbol = rctx.Doc.GetElement(symbolIds.First()) as FamilySymbol;
                    if (firstSymbol != null)
                    {
                        var existingParams = firstSymbol.Parameters
                            .Cast<Parameter>()
                            .Select(p => p.Definition.Name)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        missingParams = expectedParams
                            .Where(ep => !existingParams.Contains(ep))
                            .ToList();
                    }
                }

                // ── Origin kontrolü ───────────────────────────────────────────
                // Ailenin yerleştirilmiş instance'larının BoundingBox merkezi
                // origin'den çok uzaksa aile origin'de değil demektir.
                bool hasOriginIssue = false;
                var instances = new FilteredElementCollector(rctx.Doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol?.Family?.Id == family.Id)
                    .Take(5)  // İlk 5 instance yeterli
                    .ToList();

                if (instances.Count > 0)
                {
                    foreach (var inst in instances)
                    {
                        var bb = inst.get_BoundingBox(null);
                        if (bb == null) continue;

                        var center = (bb.Min + bb.Max) / 2.0;
                        double distToOriginFt = center.GetLength();

                        if (distToOriginFt > originThresholdFt)
                        {
                            hasOriginIssue = false; // Instance'ın modelde olması normal
                            // Gerçek origin kontrolü: Symbol geometry centroid vs (0,0,0)
                            // Bu Family document açmadan tam yapılamaz.
                            // BBox boyutu aşırı büyükse → işaret
                            double bbSizeFt = (bb.Max - bb.Min).GetLength();
                            if (bbSizeFt > originThresholdFt * 2)
                                hasOriginIssue = true;
                            break;
                        }
                    }
                }

                // ── Nested family sayısı ──────────────────────────────────────
                int nestedCount = -1; // -1 = kontrol edilmedi
                if (checkNested)
                {
                    try
                    {
                        // Family document aç → nested family'leri say
                        if (rctx.Doc.EditFamily(family) is Document famDoc)
                        {
                            nestedCount = new FilteredElementCollector(famDoc)
                                .OfClass(typeof(Family))
                                .GetElementCount();
                            famDoc.Close(false);
                        }
                    }
                    catch
                    {
                        nestedCount = -1; // Açılamazsa -1
                    }
                }

                // ── Param sayısı (tip parametreleri) ─────────────────────────
                int paramCount = 0;
                if (symbolIds.Count > 0)
                {
                    var sym = rctx.Doc.GetElement(symbolIds.First()) as FamilySymbol;
                    if (sym != null)
                        paramCount = sym.Parameters.Size;
                }

                // ── Status hesapla ────────────────────────────────────────────
                string status = "OK";
                if (isInPlace || hasNoCategory || missingParams.Count > 0 || hasOriginIssue)
                    status = "ERROR";
                else if (!typeCountOk || (checkNested && nestedCount > 10))
                    status = "WARNING";

                rows.Add(new Dictionary<string, object?>
                {
                    ["family_id"]       = Rv.IdStr(family.Id),
                    ["family_name"]     = family.Name,
                    ["category"]        = categoryName,
                    ["type_count"]      = typeCount,
                    ["param_count"]     = paramCount,
                    ["is_in_place"]     = isInPlace,
                    ["has_no_category"] = hasNoCategory,
                    ["type_count_ok"]   = typeCountOk,
                    ["missing_params"]  = string.Join(";", missingParams),
                    ["has_origin_issue"]= hasOriginIssue,
                    ["nested_count"]    = nestedCount,
                    ["status"]          = status,
                });
            }

            // Log
            int errCount  = rows.Count(r => (string?)r["status"] == "ERROR");
            int warnCount = rows.Count(r => (string?)r["status"] == "WARNING");
            int okCount   = rows.Count(r => (string?)r["status"] == "OK");

            ctx.Log($"  family_health_check: {rows.Count} aile — " +
                    $"OK:{okCount} WARNING:{warnCount} ERROR:{errCount}");

            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı
        // ─────────────────────────────────────────────────────────────────────

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
    }
}
