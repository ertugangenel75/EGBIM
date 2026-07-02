using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO — HVAC Yazma Op'ları (MepHvacOps) — v10
    ///
    /// Kaynak mantık: github.com/jeremytammik/AdnRme (MIT, Jeremy Tammik / Autodesk ADN).
    /// EGBIMOTO'ya uyarlandı:
    ///   • RevitWriteScope (atomic-mode-aware Transaction/SubTransaction) ile sarıldı.
    ///   • [EgOp] reflection kaydı, OpContext params/log konvansiyonu, Türkçe çıktı alanları.
    ///   • AdnRme'nin Util.GetTerminalFlowParameter notu korundu: terminal "Flow" parametresi
    ///     read-only BuiltInParameter DEĞİL — isimle aranan, yazılabilir instance parametresidir.
    ///
    /// Revit iç birimleri:
    ///   • Hava debisi iç birimi: ft³/s. CFM = ft³/s × 60.
    ///   • Space.CalculatedSupplyAirFlow = BuiltInParameter.ROOM_CALCULATED_SUPPLY_AIRFLOW_PARAM (ft³/s)
    ///   • Alan iç birimi: ft². m² = ft² × 0.092903.
    ///
    /// Op listesi:
    ///   assign_flow_to_terminals — her mahalin hesaplanan supply air flow'unu, içindeki
    ///                              supply hava terminallerine eşit bölüp yazar (Flow parametresi).
    ///   resize_diffuser_by_flow  — terminalin debisine göre uygun diffüzör tipini (FamilySymbol)
    ///                              eşik tablosundan seçip ChangeTypeId ile uygular.
    ///   populate_space_param     — mahallere hesaplanmış bir parametre değeri yazar
    ///                              (örn. "CFM per SF" = supply_cfm / alan_ft²).
    ///
    /// Hedef kategori: OST_DuctTerminal (supply air), MEPSpace.
    /// </summary>
    public static class MepHvacOps
    {
        // Sabitler (AdnRme Const.cs muadili)
        private const double SecondsPerMinute   = 60.0;     // ft³/s → CFM
        private const double SqFeetToSqMeter    = 0.09290304;
        private const double DefaultRoundCfmTo  = 5.0;      // terminal debisi yuvarlama adımı

        // ─────────────────────────────────────────────────────────────────────
        // H01  assign_flow_to_terminals
        //
        // input : List<Element> (Space) opsiyonel — boşsa modeldeki tüm MEPSpace taranır
        // params: round_cfm_to       Double  default=5    (terminal debisi yuvarlama adımı, CFM)
        //         flow_param_name    String  default="Flow"  (terminal yazılacak parametre adı)
        //         system_type_filter String  default="Supply Air" (terminal sistem türü)
        //         only_with_flow     Bool    default=true (hesaplanan debisi 0 olan mahalleri atla)
        //
        // output: List<Dict>
        //   space_id, space_name, space_number, terminal_count,
        //   supply_cfm, cfm_per_terminal, status
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("assign_flow_to_terminals",
            RequiresTransaction = true,
            Description =
                "Her mahalin hesaplanan supply air flow degerini, icindeki supply hava\n" +
                "terminallerine esit bolup yazar (terminal Flow parametresi).\n\n" +
                "params:\n" +
                "  round_cfm_to       — terminal debisi yuvarlama adimi, CFM (default: 5)\n" +
                "  flow_param_name    — terminale yazilacak parametre adi (default: 'Flow')\n" +
                "  system_type_filter — terminal sistem turu (default: 'Supply Air')\n" +
                "  only_with_flow     — debisi 0 olan mahalleri atla (default: true)\n\n" +
                "Revit ic birimi ft3/s; CFM = ft3/s x 60. Space.CalculatedSupplyAirFlow okunur.\n" +
                "AdnRme mantigi: terminal 'Flow' parametresi isimle aranan yazilabilir parametredir.\n\n" +
                "Input: List<Element> (Space) veya bos (tum MEPSpace taranir).\n" +
                "Cikti: space_id, space_name, space_number, terminal_count, supply_cfm,\n" +
                "       cfm_per_terminal, status",
            Category = "MEP HVAC")]
        public static List<Dictionary<string, object?>> AssignFlowToTerminals(OpContext ctx)
        {
            var rctx        = RequireRevit(ctx);
            var doc         = rctx.Doc;
            double roundTo  = ctx.GetDouble("round_cfm_to", DefaultRoundCfmTo);
            var flowParam   = ctx.GetString("flow_param_name", "Flow");
            var sysFilter   = ctx.GetString("system_type_filter", "Supply Air");
            bool onlyFlow   = ctx.GetBool("only_with_flow", true);

            if (roundTo <= 0) roundTo = DefaultRoundCfmTo;

            // Mahalleri topla
            var spaces = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .OfType<Space>().ToList();
            if (spaces.Count == 0)
            {
                // NOT: OfClass(typeof(Space/SpatialElement)) Revit'te exception atar
                // (native olmayan sinif). Yalniz OfCategory + OfType<Space> kullanilir.
                spaces = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_MEPSpaces)
                    .WhereElementIsNotElementType()
                    .OfType<Space>()
                    .ToList();
            }

            if (spaces.Count == 0)
            {
                ctx.Log("  assign_flow_to_terminals: model'de MEPSpace bulunamadi");
                return ErrRow("MEPSpace bulunamadi. Mahaller (Spaces) tanimli mi?");
            }

            // Supply hava terminallerini topla ve mahale göre grupla
            var terminals = GetSupplyAirTerminals(doc, sysFilter);
            var perSpace = new Dictionary<long, List<FamilyInstance>>();
            foreach (var t in terminals)
            {
                Space? sp = null;
                try { sp = t.Space; } catch { sp = null; }
                if (sp == null) continue;
                long key = Rv.GetId(sp.Id);
                if (!perSpace.TryGetValue(key, out var lst))
                {
                    lst = new List<FamilyInstance>();
                    perSpace[key] = lst;
                }
                lst.Add(t);
            }

            ctx.Log($"  assign_flow_to_terminals: {spaces.Count} mahal, " +
                    $"{terminals.Count} supply terminal, '{flowParam}' parametresine yazilacak");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(doc, "Terminal Debi Ata", rctx.IsAtomicMode);

            foreach (var space in spaces)
            {
                long sid = Rv.GetId(space.Id);
                string sName = space.Name ?? "";
                string sNum  = SpaceNumber(space);

                perSpace.TryGetValue(sid, out var spaceTerminals);
                int n = spaceTerminals?.Count ?? 0;

                // Hesaplanan supply air flow (ft³/s)
                double supplyFps = GetCalculatedSupplyAirFlow(space);
                double supplyCfm = supplyFps * SecondsPerMinute;

                if (n == 0)
                {
                    rows.Add(Row(sid, sName, sNum, 0, supplyCfm, 0, "TERMINAL_YOK"));
                    continue;
                }
                if (onlyFlow && supplyFps <= 1e-9)
                {
                    rows.Add(Row(sid, sName, sNum, n, 0, 0, "DEBI_SIFIR_ATLANDI"));
                    continue;
                }

                // CFM/terminal → yuvarla → ft³/s'ye geri çevir
                double cfmPerOutlet        = supplyCfm / n;
                double cfmPerOutletRounded = RoundTo(cfmPerOutlet, roundTo);
                double fpsPerOutlet        = cfmPerOutletRounded / SecondsPerMinute;

                string status = "OK";
                int written = 0;
                foreach (var term in spaceTerminals!)
                {
                    try
                    {
                        var p = GetTerminalFlowParameter(term, flowParam);
                        if (p == null || p.IsReadOnly)
                        {
                            status = $"PARAM_YAZILAMADI({flowParam})";
                            continue;
                        }
                        p.Set(fpsPerOutlet);
                        written++;
                    }
                    catch (Exception ex)
                    {
                        status = $"HATA: {ex.Message}";
                        ctx.Log($"  assign_flow_to_terminals: [{Rv.IdStr(term.Id)}] — {ex.Message}");
                    }
                }
                if (written == 0 && status == "OK") status = "YAZILAMADI";

                rows.Add(Row(sid, sName, sNum, n, supplyCfm, cfmPerOutletRounded, status));
            }

            scope.Commit();
            int ok = rows.Count(r => (string?)r["status"] == "OK");
            ctx.Log($"  assign_flow_to_terminals: {ok}/{rows.Count} mahalde debi atandi");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // H02  resize_diffuser_by_flow
        //
        // input : List<Element> (FamilyInstance / DuctTerminal) opsiyonel —
        //         boşsa modeldeki tüm supply hava terminalleri taranır
        // params: family_name        String  zorunlu  (diffüzör family adı)
        //         flow_param_name    String  default="Flow"
        //         thresholds         String  zorunlu  — "cfm:type" çiftleri, virgülle:
        //                            "100:150x150,200:200x200,400:300x300"
        //                            (debi ≤ cfm olan ilk eşiğin type'ı seçilir;
        //                             hiçbiri uymazsa en büyük eşik)
        //         system_type_filter String  default="Supply Air"
        //
        // output: List<Dict>
        //   terminal_id, current_cfm, old_type, new_type, changed, status
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("resize_diffuser_by_flow",
            RequiresTransaction = true,
            Description =
                "Terminalin mevcut debisine gore uygun diffuzor tipini (FamilySymbol) esik\n" +
                "tablosundan secip ChangeTypeId ile uygular.\n\n" +
                "params:\n" +
                "  family_name        — diffuzor family adi (zorunlu)\n" +
                "  flow_param_name    — debi okunacak parametre adi (default: 'Flow')\n" +
                "  thresholds         — 'cfm:type' ciftleri, virgulle (zorunlu).\n" +
                "                       Orn: '100:150x150,200:200x200,400:300x300'.\n" +
                "                       Debi <= cfm olan ILK esigin type'i secilir; uymazsa en buyuk.\n" +
                "  system_type_filter — terminal sistem turu (default: 'Supply Air')\n\n" +
                "Input: List<Element> (terminal) veya bos (tum supply terminaller).\n" +
                "Cikti: terminal_id, current_cfm, old_type, new_type, changed, status",
            Category = "MEP HVAC")]
        public static List<Dictionary<string, object?>> ResizeDiffuserByFlow(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            var familyName = ctx.RequireString("family_name");
            var flowParam  = ctx.GetString("flow_param_name", "Flow");
            var threshStr  = ctx.RequireString("thresholds");
            var sysFilter  = ctx.GetString("system_type_filter", "Supply Air");

            // Eşik tablosu: (cfm, typeName) artan sıralı
            var thresholds = ParseThresholds(threshStr);
            if (thresholds.Count == 0)
            {
                ctx.Log("  resize_diffuser_by_flow: gecerli esik tablosu yok");
                return ErrRow("thresholds bos/gecersiz. Ornek: '100:150x150,200:200x200'");
            }

            // Terminalleri topla
            var terminals = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .OfType<FamilyInstance>().ToList();
            if (terminals.Count == 0)
                terminals = GetSupplyAirTerminals(doc, sysFilter);

            if (terminals.Count == 0)
            {
                ctx.Log("  resize_diffuser_by_flow: supply terminal bulunamadi");
                return ErrRow("Supply hava terminali bulunamadi.");
            }

            // Gerekli tüm tip sembollerini önceden çöz (FamilySymbol cache)
            var symbolCache = new Dictionary<string, FamilySymbol?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, typeName) in thresholds)
            {
                if (!symbolCache.ContainsKey(typeName))
                    symbolCache[typeName] =
                        ModelingOps.FindFamilySymbol(doc, familyName, typeName);
            }

            ctx.Log($"  resize_diffuser_by_flow: {terminals.Count} terminal, " +
                    $"family='{familyName}', {thresholds.Count} esik");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(doc, "Diffuzor Boyutlandir", rctx.IsAtomicMode);

            foreach (var term in terminals)
            {
                long tid = Rv.GetId(term.Id);
                string oldType = (doc.GetElement(term.GetTypeId()) as ElementType)?.Name ?? "";

                // Debi oku (CFM)
                double cfm;
                try
                {
                    var p = GetTerminalFlowParameter(term, flowParam);
                    double fps = p?.AsDouble() ?? 0.0;
                    cfm = fps * SecondsPerMinute;
                }
                catch
                {
                    rows.Add(ResizeRow(tid, 0, oldType, "", false, $"DEBI_OKUNAMADI({flowParam})"));
                    continue;
                }

                // Eşik seç: debi ≤ cfm olan ilk eşik; yoksa en büyük
                string targetType = thresholds.FirstOrDefault(t => cfm <= t.cfm).typeName
                                    ?? thresholds.Last().typeName;

                if (string.Equals(targetType, oldType, StringComparison.OrdinalIgnoreCase))
                {
                    rows.Add(ResizeRow(tid, cfm, oldType, targetType, false, "DEGISIM_YOK"));
                    continue;
                }

                var sym = symbolCache.TryGetValue(targetType, out var s) ? s : null;
                if (sym == null)
                {
                    rows.Add(ResizeRow(tid, cfm, oldType, targetType, false, $"TIP_BULUNAMADI({targetType})"));
                    continue;
                }

                string status = "OK";
                bool changed = false;
                try
                {
                    if (!sym.IsActive) sym.Activate();
                    term.ChangeTypeId(sym.Id);
                    changed = true;
                }
                catch (Exception ex)
                {
                    status = $"HATA: {ex.Message}";
                    ctx.Log($"  resize_diffuser_by_flow: [{Rv.IdStr(term.Id)}] — {ex.Message}");
                }

                rows.Add(ResizeRow(tid, cfm, oldType, targetType, changed, status));
            }

            scope.Commit();
            int changedCount = rows.Count(r => (bool?)r["changed"] == true);
            ctx.Log($"  resize_diffuser_by_flow: {changedCount}/{rows.Count} terminal tipi degisti");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // H03  populate_space_param
        //
        // input : List<Element> (Space) opsiyonel — boşsa tüm MEPSpace
        // params: target_param   String  zorunlu  — yazılacak mahal parametresi adı (örn "CFM per SF")
        //         source         String  default="cfm_per_sf"
        //                        cfm_per_sf  → supply_cfm / alan_ft²
        //                        supply_cfm  → hesaplanan supply air (CFM)
        //                        constant    → params.value sabiti
        //         value          Double  (source=constant ise zorunlu)
        //         skip_zero_area Bool    default=true
        //
        // output: List<Dict> {space_id, space_name, space_number, value, status}
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("populate_space_param",
            RequiresTransaction = true,
            Description =
                "Mahallere hesaplanmis bir parametre degeri yazar (orn. 'CFM per SF').\n\n" +
                "params:\n" +
                "  target_param   — yazilacak mahal parametresi adi (zorunlu)\n" +
                "  source         — cfm_per_sf | supply_cfm | constant (default: cfm_per_sf)\n" +
                "                   cfm_per_sf = supply_cfm / alan_ft2\n" +
                "                   supply_cfm = hesaplanan supply air (CFM)\n" +
                "                   constant   = params.value sabiti\n" +
                "  value          — source=constant ise yazilacak sabit (zorunlu)\n" +
                "  skip_zero_area — alani 0 olan mahalleri atla (default: true)\n\n" +
                "Input: List<Element> (Space) veya bos (tum MEPSpace).\n" +
                "Cikti: space_id, space_name, space_number, value, status",
            Category = "MEP HVAC")]
        public static List<Dictionary<string, object?>> PopulateSpaceParam(OpContext ctx)
        {
            var rctx        = RequireRevit(ctx);
            var doc         = rctx.Doc;
            var targetParam = ctx.RequireString("target_param");
            var source      = ctx.GetString("source", "cfm_per_sf").ToLowerInvariant();
            bool skipZero   = ctx.GetBool("skip_zero_area", true);
            double constVal = ctx.GetDouble("value", 0.0);

            var spaces = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .OfType<Space>().ToList();
            if (spaces.Count == 0)
            {
                // NOT: OfClass(typeof(Space/SpatialElement)) Revit'te exception atar
                // (native olmayan sinif). Yalniz OfCategory + OfType<Space> kullanilir.
                spaces = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_MEPSpaces)
                    .WhereElementIsNotElementType()
                    .OfType<Space>()
                    .ToList();
            }

            if (spaces.Count == 0)
            {
                ctx.Log("  populate_space_param: MEPSpace bulunamadi");
                return ErrRow("MEPSpace bulunamadi.");
            }

            ctx.Log($"  populate_space_param: {spaces.Count} mahal, " +
                    $"'{targetParam}' <- source='{source}'");

            var rows = new List<Dictionary<string, object?>>();
            using var scope = new RevitWriteScope(doc, "Mahal Parametre Yaz", rctx.IsAtomicMode);

            foreach (var space in spaces)
            {
                long sid = Rv.GetId(space.Id);
                string sName = space.Name ?? "";
                string sNum  = SpaceNumber(space);

                double areaFt2 = space.get_Parameter(BuiltInParameter.ROOM_AREA)?.AsDouble() ?? 0.0;
                double supplyCfm = GetCalculatedSupplyAirFlow(space) * SecondsPerMinute;

                double value;
                switch (source)
                {
                    case "constant":
                        value = constVal;
                        break;
                    case "supply_cfm":
                        value = supplyCfm;
                        break;
                    default: // cfm_per_sf
                        if (skipZero && areaFt2 <= 1e-9)
                        {
                            rows.Add(SpaceParamRow(sid, sName, sNum, 0, "ALAN_SIFIR_ATLANDI"));
                            continue;
                        }
                        value = areaFt2 > 1e-9 ? supplyCfm / areaFt2 : 0.0;
                        break;
                }

                string status = "OK";
                try
                {
                    var p = space.LookupParameter(targetParam);
                    if (p == null)
                        status = $"PARAM_YOK({targetParam})";
                    else if (p.IsReadOnly)
                        status = $"PARAM_READONLY({targetParam})";
                    else if (p.StorageType == StorageType.Double)
                        p.Set(value);
                    else if (p.StorageType == StorageType.String)
                        p.Set(value.ToString("0.###"));
                    else
                        status = $"PARAM_TIP_UYUMSUZ({p.StorageType})";
                }
                catch (Exception ex)
                {
                    status = $"HATA: {ex.Message}";
                    ctx.Log($"  populate_space_param: [{Rv.IdStr(space.Id)}] — {ex.Message}");
                }

                rows.Add(SpaceParamRow(sid, sName, sNum, value, status));
            }

            scope.Commit();
            int ok = rows.Count(r => (string?)r["status"] == "OK");
            ctx.Log($"  populate_space_param: {ok}/{rows.Count} mahale yazildi");
            return rows;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Yardımcılar (AdnRme Util.cs muadili)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Supply hava terminallerini getirir: OST_DuctTerminal + FamilyInstance +
        /// System Type = sysFilter. AdnRme.GetSupplyAirTerminals uyarlaması.
        /// Parametre filtresi yerine, sürüm-bağımsızlık için bellek tarafı filtre.
        /// </summary>
        private static List<FamilyInstance> GetSupplyAirTerminals(Document doc, string sysFilter)
        {
            var all = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DuctTerminal)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            if (string.IsNullOrWhiteSpace(sysFilter))
                return all;

            var filtered = new List<FamilyInstance>();
            foreach (var fi in all)
            {
                var p = fi.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM)
                      ?? fi.LookupParameter("System Classification")
                      ?? fi.LookupParameter("System Type");
                string sys = p?.AsString() ?? p?.AsValueString() ?? "";
                if (sys.IndexOf(sysFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    filtered.Add(fi);
            }
            // Hiç eşleşme yoksa (parametre okunamadıysa) tümünü döndür — kullanıcı filtreyi gevşetebilir
            return filtered.Count > 0 ? filtered : all;
        }

        /// <summary>
        /// Terminalin yazılabilir "Flow" parametresi. AdnRme notu: built-in Flow read-only,
        /// asıl yazılacak parametre isimle aranır.
        /// </summary>
        private static Parameter? GetTerminalFlowParameter(FamilyInstance terminal, string name)
        {
            var p = terminal.LookupParameter(name);
            if (p != null) return p;
            // Yedek: built-in RBS_DUCT_FLOW_PARAM (genelde read-only ama okuma için kullanılabilir)
            return terminal.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
        }

        /// <summary>Space.CalculatedSupplyAirFlow (ft³/s). 0 dönerse parametre yok/boş demektir.</summary>
        private static double GetCalculatedSupplyAirFlow(Space space)
        {
            var p = space.get_Parameter(BuiltInParameter.ROOM_CALCULATED_SUPPLY_AIRFLOW_PARAM)
                  ?? space.LookupParameter("Calculated Supply Airflow")
                  ?? space.LookupParameter("Calculated Supply Air Flow");
            return p?.AsDouble() ?? 0.0;
        }

        private static string SpaceNumber(Element space)
            => space.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString()
               ?? space.LookupParameter("Number")?.AsString() ?? "";

        private static double RoundTo(double a, double step)
        {
            if (step <= 0) return a;
            return Math.Round(a / step, 0, MidpointRounding.AwayFromZero) * step;
        }

        /// <summary>"100:150x150,200:200x200" → [(100,"150x150"),(200,"200x200")] artan cfm sıralı.</summary>
        private static List<(double cfm, string typeName)> ParseThresholds(string s)
        {
            var list = new List<(double, string)>();
            foreach (var part in s.Split(','))
            {
                var kv = part.Split(new[] { ':' }, 2);
                if (kv.Length != 2) continue;
                if (!double.TryParse(kv[0].Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double cfm))
                    continue;
                var typeName = kv[1].Trim();
                if (typeName.Length == 0) continue;
                list.Add((cfm, typeName));
            }
            return list.OrderBy(x => x.Item1).ToList();
        }

        // ── Satır kurucular ──────────────────────────────────────────────────

        private static Dictionary<string, object?> Row(
            long sid, string name, string num, int termCount,
            double supplyCfm, double cfmPerTerm, string status)
            => new()
            {
                ["space_id"]         = sid.ToString(),
                ["space_name"]       = name,
                ["space_number"]     = num,
                ["terminal_count"]   = termCount,
                ["supply_cfm"]       = Math.Round(supplyCfm, 1),
                ["cfm_per_terminal"] = Math.Round(cfmPerTerm, 1),
                ["status"]           = status,
            };

        private static Dictionary<string, object?> ResizeRow(
            long tid, double cfm, string oldType, string newType, bool changed, string status)
            => new()
            {
                ["terminal_id"] = tid.ToString(),
                ["current_cfm"] = Math.Round(cfm, 1),
                ["old_type"]    = oldType,
                ["new_type"]    = newType,
                ["changed"]     = changed,
                ["status"]      = status,
            };

        private static Dictionary<string, object?> SpaceParamRow(
            long sid, string name, string num, double value, string status)
            => new()
            {
                ["space_id"]     = sid.ToString(),
                ["space_name"]   = name,
                ["space_number"] = num,
                ["value"]        = Math.Round(value, 4),
                ["status"]       = status,
            };

        private static List<Dictionary<string, object?>> ErrRow(string msg)
            => new() { new() { ["status"] = "HATA", ["mesaj"] = msg } };

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit baglami gerektirir.");
    }
}
