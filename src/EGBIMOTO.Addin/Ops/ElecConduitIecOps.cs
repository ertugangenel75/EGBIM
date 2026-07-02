using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO — Elektrik Conduit IEC Hesap Motoru (ElecConduitIecOps) — v10
    ///
    /// Standart: TS HD 60364 / IEC 60364-5-52 (kablo seçimi, gerilim düşümü),
    ///           IEC 60364-4-43 (kısa devre), IEC 61386 (conduit iç çapları),
    ///           IEC 60364-5-52 Clause 522.8 (conduit doluluk %40).
    ///
    /// MİMARİ (Autodesk read-only kısıtını aşan, profesyonel araçların — eVolve,
    /// ElectroBIM — kullandığı yaklaşım):
    ///   Conduit (kablo borusu) bilgi taşıyıcısıdır. Autodesk'in kilitli Panel
    ///   Schedule alanlarına DOKUNULMAZ; bunun yerine Conduit üzerine Shared
    ///   Parameter yazılır. Devre ↔ Conduit bağı Revit'te API'de YOKTUR; bu yüzden
    ///   kullanıcı conduit'e EG_DevreNo yazar, motor o devreyi ElectricalSystem
    ///   CircuitNumber ile eşleştirir.
    ///
    /// HİBRİT TABLO: Gömülü ampacity/çap tabloları IEC referans değerleridir
    /// (resmi IEC tablosu telifli olduğundan yaygın mühendislik referans değerleri
    /// kullanıldı). Kullanıcı kendi resmi TS/IEC tablosunu JSON ile override edebilir
    /// (calc op'ta ampacity_table_path parametresi). Conduit iç çapları IEC 61386'dan.
    ///
    /// ⚠️ SORUMLULUK: Bu motor mühendislik kararlarına YARDIMCI olur, yerini ALMAZ.
    /// Sonuçlar sorumlu elektrik mühendisi tarafından doğrulanmalıdır.
    ///
    /// Op listesi:
    ///   elec_setup_conduit_params — EGBIM_ElektrikParams.txt'i projeye yükler (sabit
    ///                               GUID), Conduit + ElectricalCircuit kategorilerine bind.
    ///   elec_conduit_calc_iec     — Conduit uzunluğunu okur, EG_DevreNo ile devreyi
    ///                               eşleştirir, IEC kablo seçimi + gerilim düşümü +
    ///                               conduit fill + kısa devre hesabı yapar, çıktı
    ///                               parametrelerini conduit'e yazar.
    ///   elec_conduit_schedule     — Conduit hesap sonuçlarından HTML/CSV cetvel üretir
    ///                               (From-To-Devre-Kesit-Uzunluk-GerilimDüşümü-Fill).
    /// </summary>
    public static class ElecConduitIecOps
    {
        // Standart kablo kesitleri (mm²) — IEC E3 serisi, 1.5'ten 300'e
        private static readonly double[] StdSizes =
            { 1.5, 2.5, 4, 6, 10, 16, 25, 35, 50, 70, 95, 120, 150, 185, 240, 300 };

        // Standart koruma anma akımları (A) — IEC 60898 / 60269
        private static readonly double[] StdBreakers =
            { 6, 10, 13, 16, 20, 25, 32, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400 };

        // IEC 61386 conduit iç çapları (mm) — sert PVC. designating size → ID
        private static readonly Dictionary<int, double> ConduitInnerDia = new()
        {
            { 16, 14.1 }, { 20, 18.3 }, { 25, 23.4 }, { 32, 30.3 },
            { 40, 38.0 }, { 50, 47.4 }, { 63, 59.5 },
        };

        // ── Gömülü ampacity tablosu (A) — IEC 60364-5-52 referans değerleri ──
        // Anahtar: "{yalitim}_{iletken}_{metod}" → kesit(mm²) → akım(A)
        // Method C = duvara klipsli/boru içinde, 30°C, en yaygın. B = boru içinde duvar.
        // NOT: Bunlar yaygın mühendislik referans değerleridir; resmi tablo değildir.
        // Kullanıcı kendi tablosunu JSON ile yükleyebilir.
        private static Dictionary<string, Dictionary<double, double>> BuildAmpacity()
        {
            // PVC bakır, Method C (3 yüklü iletken, 30°C)
            var pvcCuC = new Dictionary<double, double>
            {
                {1.5,19.5},{2.5,27},{4,36},{6,46},{10,61},{16,80},{25,101},{35,126},
                {50,153},{70,196},{95,238},{120,276},{150,319},{185,364},{240,430},{300,497},
            };
            // PVC bakır, Method B (boru içinde, biraz daha düşük)
            var pvcCuB = new Dictionary<double, double>
            {
                {1.5,17.5},{2.5,24},{4,32},{6,41},{10,57},{16,76},{25,96},{35,119},
                {50,144},{70,184},{95,223},{120,259},{150,299},{185,341},{240,403},{300,464},
            };
            // XLPE bakır, Method C (90°C, ~%25 daha yüksek)
            var xlpeCuC = new Dictionary<double, double>
            {
                {1.5,26},{2.5,36},{4,49},{6,63},{10,86},{16,115},{25,149},{35,185},
                {50,225},{70,289},{95,352},{120,410},{150,473},{185,542},{240,641},{300,741},
            };
            // XLPE bakır, Method B
            var xlpeCuB = new Dictionary<double, double>
            {
                {1.5,23},{2.5,31},{4,42},{6,54},{10,75},{16,100},{25,127},{35,158},
                {50,192},{70,246},{95,298},{120,346},{150,399},{185,456},{240,538},{300,621},
            };
            // Alüminyum: bakırın ~%78'i (yaklaşık)
            Dictionary<double, double> Al(Dictionary<double, double> cu)
                => cu.ToDictionary(k => k.Key, v => Math.Round(v.Value * 0.78, 0));

            return new Dictionary<string, Dictionary<double, double>>(StringComparer.OrdinalIgnoreCase)
            {
                { "PVC_Cu_C",  pvcCuC },  { "PVC_Cu_B",  pvcCuB },
                { "XLPE_Cu_C", xlpeCuC }, { "XLPE_Cu_B", xlpeCuB },
                { "PVC_Al_C",  Al(pvcCuC) },  { "PVC_Al_B",  Al(pvcCuB) },
                { "XLPE_Al_C", Al(xlpeCuC) }, { "XLPE_Al_B", Al(xlpeCuB) },
            };
        }

        // Kablo dış çapı tahmini (mm) — conduit fill için. Tek damar, yalıtım dahil.
        // IEC yaklaşık: d_dış ≈ k·√kesit. PVC için k≈2.3, XLPE k≈2.2 (referans).
        private static double CableOuterDia(double sizeMm2, string yalitim)
        {
            double k = yalitim.Equals("XLPE", StringComparison.OrdinalIgnoreCase) ? 2.2 : 2.4;
            // Çekirdek çapı + yalıtım; küçük kesitlerde oransal olarak daha kalın
            return k * Math.Sqrt(sizeMm2) + 1.2;
        }

        // İletken çalışma sıcaklığı (°C)
        private static double OperTemp(string yalitim)
            => yalitim.Equals("XLPE", StringComparison.OrdinalIgnoreCase) ? 90 : 70;

        // 20°C özdirenç (Ω·mm²/m)
        private static double Rho20(string iletken)
            => iletken.Equals("Al", StringComparison.OrdinalIgnoreCase) ? 0.0282 : 0.0175;

        // Sıcaklık katsayısı (1/°C)
        private static double Alpha(string iletken)
            => iletken.Equals("Al", StringComparison.OrdinalIgnoreCase) ? 0.00403 : 0.00393;

        // Kısa devre k sabiti (IEC 60364-4-43)
        private static double ScK(string yalitim, string iletken)
        {
            bool xlpe = yalitim.Equals("XLPE", StringComparison.OrdinalIgnoreCase);
            bool al   = iletken.Equals("Al", StringComparison.OrdinalIgnoreCase);
            // Cu: PVC 115, XLPE 143 | Al: PVC 76, XLPE 94
            return al ? (xlpe ? 94 : 76) : (xlpe ? 143 : 115);
        }

        // Ortam sıcaklığı düzeltme faktörü Ca (IEC B.52.14, yaklaşık)
        private static double CaFactor(double ambientC, string yalitim)
        {
            // Referans 30°C → 1.0. Basit lineer yaklaşım (resmi tablo step'li).
            bool xlpe = yalitim.Equals("XLPE", StringComparison.OrdinalIgnoreCase);
            double maxT = xlpe ? 90 : 70;
            if (ambientC <= 30) return 1.0 + (30 - ambientC) * 0.005; // soğukta küçük bonus
            // (maxT - ambient)/(maxT - 30) karekök yaklaşımı
            double f = Math.Sqrt(Math.Max(0.0, (maxT - ambientC) / (maxT - 30.0)));
            return Math.Max(0.3, Math.Round(f, 2));
        }

        // Gruplama faktörü Cg (IEC B.52.17 — boru/demet içinde, yaklaşık)
        private static double CgFactor(int n)
        {
            if (n <= 1) return 1.0;
            // Yaygın değerler: 2→0.80, 3→0.70, 4→0.65, 5→0.60, 6→0.57, 7-9→0.54...
            return n switch
            {
                2 => 0.80, 3 => 0.70, 4 => 0.65, 5 => 0.60, 6 => 0.57,
                7 => 0.54, 8 => 0.52, 9 => 0.50,
                _ => Math.Max(0.38, 0.50 - (n - 9) * 0.01),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 1  elec_setup_conduit_params
        //
        // params: spf_path  String  default="mapping/EGBIM_ElektrikParams.txt"
        // output: Dict {added, skipped, spf_path}
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_setup_conduit_params",
            RequiresTransaction = true,
            Description =
                "EGBIM elektrik hesap shared parametrelerini (sabit GUID) projeye yukler ve\n" +
                "Conduit + ElectricalCircuit kategorilerine instance binding yapar.\n\n" +
                "params:\n" +
                "  spf_path — SPF yolu (opsiyonel, default: mapping/EGBIM_ElektrikParams.txt)\n\n" +
                "Bu op hesaptan ONCE bir kez calistirilir (altyapi kurulumu).\n" +
                "Cikti: added, skipped, spf_path",
            Category = "MEP-Elektrik")]
        public static Dictionary<string, object?> SetupConduitParams(OpContext ctx)
        {
            var rctx = RequireRevit(ctx);
            var spfPathRaw = ctx.GetString("spf_path", "");

            // Varsayılan: data/mapping altında
            string spfPath;
            if (!string.IsNullOrEmpty(spfPathRaw))
            {
                spfPath = Path.IsPathRooted(spfPathRaw)
                    ? spfPathRaw
                    : Path.Combine(EgbimotoData.DataRoot, spfPathRaw);
            }
            else
            {
                spfPath = Path.Combine(EgbimotoData.DataRoot, "mapping", "EGBIM_ElektrikParams.txt");
            }

            if (!File.Exists(spfPath))
                throw new FileNotFoundException($"Elektrik SPF dosyasi bulunamadi: {spfPath}");

            var app     = rctx.UiApp.Application;
            var prevSpf = app.SharedParametersFilename;
            app.SharedParametersFilename = spfPath;
            var spFile = app.OpenSharedParameterFile()
                ?? throw new InvalidOperationException("Elektrik paylasimli parametre dosyasi acilamadi.");

            // Bind hedefi: Conduit + ElectricalCircuit
            var cats = new[]
            {
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_ElectricalCircuit,
            };

            int added = 0, skipped = 0;
            using var scope = new RevitWriteScope(rctx.Doc, "Elektrik Parametre Ekle", rctx.IsAtomicMode);
            foreach (DefinitionGroup group in spFile.Groups)
            {
                foreach (Definition def in group.Definitions)
                {
                    try
                    {
                        var catSet = new CategorySet();
                        foreach (var bic in cats)
                        {
                            var c = TryGetCategory(rctx.Doc, bic);
                            if (c != null) catSet.Insert(c);
                        }
                        if (catSet.Size == 0) { skipped++; continue; }

                        var binding = app.Create.NewInstanceBinding(catSet);
                        var bindMap = rctx.Doc.ParameterBindings;
                        if (!bindMap.Contains(def))
                        {
                            bindMap.Insert(def, binding, GroupTypeId.Electrical);
                            added++;
                        }
                        else skipped++;
                    }
                    catch { skipped++; }
                }
            }
            scope.Commit();
            app.SharedParametersFilename = prevSpf;

            ctx.Log($"  elec_setup_conduit_params: {added} eklendi, {skipped} atlandi → {spfPath}");
            return new() { ["added"] = added, ["skipped"] = skipped, ["spf_path"] = spfPath };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2  elec_conduit_calc_iec
        //
        // input : List<Element> (Conduit) opsiyonel — boşsa tüm OST_Conduit
        // params: voltage           Double default=400 (hat gerilimi V; 1-faz için 230)
        //         vdrop_limit_pct   Double default=5   (IEC: aydınlatma 3, güç 5)
        //         max_fill_pct      Double default=40  (IEC 522.8)
        //         ampacity_table_path String "" — kullanıcı JSON ampacity tablosu (override)
        //         only_with_circuit Bool   default=true (EG_DevreNo boş conduit atla)
        //
        // output: List<Dict> conduit başına hesap sonucu
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_conduit_calc_iec",
            RequiresTransaction = true,
            Description =
                "Conduit uzunlugunu okur, EG_DevreNo ile ElectricalSystem'i eslestirir,\n" +
                "IEC 60364 kablo secimi + gerilim dusumu + conduit fill + kisa devre hesabi\n" +
                "yapar ve cikti Shared Parameter'larini conduit'e yazar.\n\n" +
                "params:\n" +
                "  voltage             — hat gerilimi V (default 400; tek faz icin 230)\n" +
                "  vdrop_limit_pct     — gerilim dusumu siniri % (default 5; aydinlatma 3)\n" +
                "  max_fill_pct        — conduit doluluk siniri % (default 40, IEC 522.8)\n" +
                "  ampacity_table_path — kullanici JSON ampacity tablosu (opsiyonel override)\n" +
                "  only_with_circuit   — EG_DevreNo bos conduit'leri atla (default true)\n\n" +
                "GIRDI parametreleri conduit'ten okunur (EG_KurulumMetodu, EG_Yalitim,\n" +
                "EG_Iletken, EG_OrtamSicaklik, EG_GruplamaAdet, EG_GucFaktoru, EG_YapmaPayi).\n" +
                "Bossa makul varsayilanlar (C, PVC, Cu, 30C, 1, 0.8, 0) kullanilir.\n\n" +
                "Cikti: conduit_id, devre, uzunluk_m, akim_a, kesit_mm2, gerilim_dusumu_pct,\n" +
                "       doluluk_pct, sigorta_a, durum",
            Category = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> ConduitCalcIec(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            double voltage = ctx.GetDouble("voltage", 400.0);
            double vdLimit = ctx.GetDouble("vdrop_limit_pct", 5.0);
            double maxFill = ctx.GetDouble("max_fill_pct", 40.0);
            var tablePath  = ctx.GetString("ampacity_table_path", "");
            bool onlyCirc  = ctx.GetBool("only_with_circuit", true);
            bool threePhase = voltage > 300; // 400V → 3 faz, 230V → 1 faz

            // Ampacity tablosu: gömülü + opsiyonel override
            var ampacity = BuildAmpacity();
            if (!string.IsNullOrEmpty(tablePath))
                MergeUserAmpacity(ampacity, tablePath, ctx);

            // Conduit'leri topla
            var conduits = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .Where(e => Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_Conduit)
                .ToList();
            if (conduits.Count == 0)
                conduits = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Conduit)
                    .WhereElementIsNotElementType()
                    .ToList();

            if (conduits.Count == 0)
            {
                ctx.Log("  elec_conduit_calc_iec: model'de conduit bulunamadi");
                return ErrRow("Conduit (kablo borusu) bulunamadi.");
            }

            // Devre haritası: CircuitNumber → ApparentLoad (VA)
            var circuitMap = BuildCircuitMap(doc);

            ctx.Log($"  elec_conduit_calc_iec: {conduits.Count} conduit, {circuitMap.Count} devre, " +
                    $"{(threePhase ? "3-faz" : "1-faz")} {voltage}V, vd≤%{vdLimit}");

            var rows = new List<Dictionary<string, object?>>();
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            using var scope = new RevitWriteScope(doc, "Conduit IEC Hesap", rctx.IsAtomicMode);

            foreach (var con in conduits)
            {
                long cid = Rv.GetId(con.Id);
                string devre = con.LookupParameter("EG_DevreNo")?.AsString() ?? "";

                if (onlyCirc && string.IsNullOrWhiteSpace(devre))
                {
                    rows.Add(CalcRow(cid, "", 0, 0, "", 0, 0, 0, "DEVRE_YOK"));
                    continue;
                }

                // Girdi parametreleri (boşsa varsayılan)
                string metod   = OrDefault(con.LookupParameter("EG_KurulumMetodu")?.AsString(), "C");
                string yalitim = OrDefault(con.LookupParameter("EG_Yalitim")?.AsString(), "PVC");
                string iletken = OrDefault(con.LookupParameter("EG_Iletken")?.AsString(), "Cu");
                double ambient = ParamD(con, "EG_OrtamSicaklik", 30.0);
                int    grupAd  = ParamI(con, "EG_GruplamaAdet", 1);
                double cosphi  = ParamD(con, "EG_GucFaktoru", 0.8);
                double yapma   = ParamD(con, "EG_YapmaPayi", 0.0);

                // Conduit uzunluğu (Revit iç birimi feet → metre)
                double lenFt = con.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0.0;
                double modelLen = lenFt * 0.3048;
                double kabloLen = modelLen + yapma;

                // Devre yükü → akım IB
                double loadVa = circuitMap.TryGetValue(devre.Trim(), out var va) ? va : 0.0;
                double ib = ComputeCurrent(loadVa, voltage, threePhase, cosphi);

                if (ib <= 1e-6)
                {
                    WriteOutputs(con, modelLen, kabloLen, 0, "", 0, 0, 0, 0, 0, 0, "YUK_YOK", now);
                    rows.Add(CalcRow(cid, devre, modelLen, 0, "", 0, 0, 0, "YUK_YOK"));
                    continue;
                }

                // Düzeltme faktörleri
                double ca = CaFactor(ambient, yalitim);
                double cg = CgFactor(grupAd);
                double itRequired = ib / (ca * cg); // gerekli tablo akımı

                // Ampacity tablosundan kesit seç (1. kontrol: akım taşıma)
                string key = $"{yalitim}_{iletken}_{(metod.StartsWith("B") ? "B" : "C")}";
                if (!ampacity.TryGetValue(key, out var ampTable))
                    ampTable = ampacity["PVC_Cu_C"]; // fallback

                // IEC prosedürü: en küçük kablo, ampacity VE gerilim düşümü VE kısa devre
                // kontrollerini BİRLİKTE geçmeli. Ampacity'den başlayıp, gerilim düşümü
                // sınırı aşılırsa bir üst standart kesite çıkarak iterasyon yapılır.
                double sinphi = Math.Sqrt(Math.Max(0, 1 - cosphi * cosphi));
                double b = threePhase ? 1.732 : 2.0;

                double chosen = -1, vDropV = 0, vDropPct = 0;
                bool vdLimited = false;
                foreach (var s in StdSizes)
                {
                    if (!ampTable.TryGetValue(s, out var amp)) continue;
                    if (amp < itRequired) continue; // ampacity yetmiyor → bir üst kesit

                    // Bu kesitle gerilim düşümü
                    double rOper = Rho20(iletken) / s
                                   * (1 + Alpha(iletken) * (OperTemp(yalitim) - 20)); // Ω/m
                    double xReact = 0.00008; // Ω/m yaklaşık
                    double vV = b * ib * kabloLen * (rOper * cosphi + xReact * sinphi);
                    double vP = voltage > 0 ? vV / voltage * 100.0 : 0;

                    chosen = s; vDropV = vV; vDropPct = vP;
                    if (vP <= vdLimit) { vdLimited = false; break; } // her iki kontrol OK
                    vdLimited = true; // ampacity OK ama Vd aşıldı → bir üst kesit dene
                }

                if (chosen <= 0)
                {
                    WriteOutputs(con, modelLen, kabloLen, ib, "", 0, 0, 0, 0, ca, cg, "KESIT_BULUNAMADI", now);
                    rows.Add(CalcRow(cid, devre, modelLen, ib, "", 0, 0, 0, "KESIT_BULUNAMADI"));
                    continue;
                }

                // Kısa devre min kesit (basit: IB'nin ~10 katı arıza, 0.4s)
                double iFault = ib * 10; // yaklaşık (gerçek değer trafo empedansından)
                double sMin = iFault * Math.Sqrt(0.4) / ScK(yalitim, iletken);

                // Conduit fill
                double fillPct = ComputeFill(con, chosen, threePhase, yalitim);

                // Sigorta önerisi (IB ≤ In ≤ Iz)
                double iz = SelectAmpacity(ampTable, chosen) * ca * cg;
                double inBreaker = SelectBreaker(ib, iz);

                // Durum — gerilim düşümü en büyük kesitte bile aşıldıysa uyar
                string durum = "UYGUN";
                if (vdLimited && vDropPct > vdLimit) durum = "GERILIM_ASILDI";
                else if (fillPct > maxFill) durum = "DOLULUK_ASILDI";
                else if (sMin > chosen) durum = "KISA_DEVRE_YETERSIZ";

                // Kesit etiketi
                string kesitTxt = FormatCableSize(chosen, threePhase);

                WriteOutputs(con, modelLen, kabloLen, ib, kesitTxt, chosen,
                             vDropPct, vDropV, inBreaker, fillPct, sMin, durum, now);

                rows.Add(CalcRow(cid, devre, modelLen, ib, kesitTxt, vDropPct, fillPct, inBreaker, durum));
            }

            scope.Commit();
            int uygun = rows.Count(r => (string?)r["durum"] == "UYGUN");
            ctx.Log($"  elec_conduit_calc_iec: {uygun}/{rows.Count} conduit UYGUN");
            return rows;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 3  elec_conduit_schedule
        //
        // input : List<Element> (Conduit) opsiyonel — boşsa tüm OST_Conduit
        // params: output_path  String  zorunlu  (.html veya .csv)
        //         only_calculated Bool default=true (EG_HesapDurumu dolu olanlar)
        //
        // output: List<Dict> cetvel satırları
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("elec_conduit_schedule",
            RequiresTransaction = false,
            Description =
                "Conduit hesap sonuclarindan From-To-Devre-Kesit-Uzunluk-GerilimDusumu-Fill\n" +
                "cetveli uretir (HTML veya CSV).\n\n" +
                "params:\n" +
                "  output_path     — cikti yolu, .html veya .csv (zorunlu)\n" +
                "  only_calculated — yalniz hesaplanmis conduit'ler (default true)\n\n" +
                "Cikti: devre, kaynak, hedef, kesit, uzunluk_m, gerilim_dusumu_pct,\n" +
                "       doluluk_pct, sigorta_a, durum (+ dosya)",
            Category = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> ConduitSchedule(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            var outputPath = ctx.RequireString("output_path");
            bool onlyCalc  = ctx.GetBool("only_calculated", true);

            var conduits = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .Where(e => Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_Conduit)
                .ToList();
            if (conduits.Count == 0)
                conduits = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Conduit)
                    .WhereElementIsNotElementType()
                    .ToList();

            var rows = new List<Dictionary<string, object?>>();
            foreach (var con in conduits)
            {
                string durum = con.LookupParameter("EG_HesapDurumu")?.AsString() ?? "";
                if (onlyCalc && string.IsNullOrWhiteSpace(durum)) continue;

                rows.Add(new Dictionary<string, object?>
                {
                    ["devre"]              = con.LookupParameter("EG_DevreNo")?.AsString() ?? "",
                    ["kaynak"]             = con.LookupParameter("EG_Kaynak")?.AsString() ?? "",
                    ["hedef"]              = con.LookupParameter("EG_Hedef")?.AsString() ?? "",
                    ["kesit"]              = con.LookupParameter("EG_KabloKesiti")?.AsString() ?? "",
                    ["uzunluk_m"]          = Round(con, "EG_KabloUzunluk", 2),
                    ["akim_a"]             = Round(con, "EG_HesapAkim", 1),
                    ["gerilim_dusumu_pct"] = Round(con, "EG_GerilimDusumu", 2),
                    ["doluluk_pct"]        = Round(con, "EG_DolulukYuzde", 1),
                    ["sigorta_a"]          = Round(con, "EG_SigortaOneri", 0),
                    ["durum"]              = durum,
                });
            }

            if (rows.Count == 0)
            {
                ctx.Log("  elec_conduit_schedule: cetvele yazilacak hesaplanmis conduit yok");
                return ErrRow("Hesaplanmis conduit bulunamadi. Once elec_conduit_calc_iec calistirin.");
            }

            // Sırala: devre adına göre
            rows = rows.OrderBy(r => (r["devre"] as string) ?? "").ToList();

            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                if (outputPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(outputPath, BuildCsv(rows), Encoding.UTF8);
                else
                    File.WriteAllText(outputPath, BuildScheduleHtml(rows, doc.Title), Encoding.UTF8);
                ctx.Log($"  elec_conduit_schedule: {rows.Count} satir → {outputPath}");
            }
            catch (Exception ex)
            {
                ctx.Log($"  elec_conduit_schedule: dosya yazilamadi — {ex.Message}");
                return ErrRow($"Cetvel yazilamadi: {ex.Message}");
            }

            return rows;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Hesap yardımcıları
        // ═════════════════════════════════════════════════════════════════════

        private static Dictionary<string, double> BuildCircuitMap(Document doc)
        {
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var systems = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>();
            foreach (var es in systems)
            {
                string cn = "";
                try { cn = es.CircuitNumber ?? ""; } catch { }
                if (string.IsNullOrWhiteSpace(cn)) continue;
                double va = 0;
                try { va = es.ApparentLoad; } catch { }
                map[cn.Trim()] = va; // aynı no varsa son kazanır
            }
            return map;
        }

        private static double ComputeCurrent(double loadVa, double voltage, bool threePhase, double cosphi)
        {
            if (loadVa <= 0 || voltage <= 0) return 0;
            // Görünür güç VA → akım. cosφ akımı etkilemez (VA zaten görünür güç).
            return threePhase
                ? loadVa / (1.732 * voltage)
                : loadVa / voltage;
        }

        private static double SelectAmpacity(Dictionary<double, double> ampTable, double size)
            => ampTable.TryGetValue(size, out var a) ? a : 0;

        private static double SelectBreaker(double ib, double iz)
        {
            // IB ≤ In ≤ Iz koşulunu sağlayan en küçük standart sigorta
            foreach (var br in StdBreakers)
                if (br >= ib && br <= iz)
                    return br;
            // Iz çok düşükse IB'den büyük en küçük breaker (uyarı durumu zaten yakalanır)
            foreach (var br in StdBreakers)
                if (br >= ib) return br;
            return StdBreakers.Last();
        }

        private static double ComputeFill(Element con, double sizeMm2, bool threePhase, string yalitim)
        {
            // Conduit iç çapı (Revit param, feet → mm)
            double diaFt = con.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM)?.AsDouble() ?? 0;
            double innerDiaMm = diaFt * 304.8;
            if (innerDiaMm <= 0)
            {
                // Param yoksa nominal boyuttan IEC 61386 tablosu (yaklaşık)
                innerDiaMm = 20; // varsayılan 20mm
            }
            double innerArea = Math.PI * Math.Pow(innerDiaMm / 2.0, 2); // mm²

            // İletken sayısı: 3 faz → 3F+N+PE=5, 1 faz → F+N+PE=3
            int conductorCount = threePhase ? 5 : 3;
            double dCable = CableOuterDia(sizeMm2, yalitim);
            double cableArea = Math.PI * Math.Pow(dCable / 2.0, 2) * conductorCount;

            return innerArea > 0 ? cableArea / innerArea * 100.0 : 0;
        }

        private static string FormatCableSize(double sizeMm2, bool threePhase)
        {
            // PE kesiti: faz ≤16 → PE=faz; >16 → PE=faz/2 (IEC 60364-5-54 basit)
            double pe = sizeMm2 <= 16 ? sizeMm2 : sizeMm2 / 2.0;
            string sz = sizeMm2 % 1 == 0 ? $"{sizeMm2:0}" : $"{sizeMm2:0.0}";
            string pz = pe % 1 == 0 ? $"{pe:0}" : $"{pe:0.0}";
            return threePhase ? $"4x{sz}+{pz}" : $"3x{sz}";
        }

        private static void WriteOutputs(Element con, double modelLen, double kabloLen,
            double ib, string kesitTxt, double fazKesit, double vdPct, double vdV,
            double sigorta, double fill, double scMin, string durum, string tarih)
        {
            SetD(con, "EG_ModelUzunluk", modelLen);
            SetD(con, "EG_KabloUzunluk", kabloLen);
            SetD(con, "EG_HesapAkim", ib);
            SetS(con, "EG_KabloKesiti", kesitTxt);
            SetD(con, "EG_FazKesit_mm2", fazKesit);
            SetD(con, "EG_GerilimDusumu", vdPct);
            SetD(con, "EG_GerilimDusumuV", vdV);
            SetD(con, "EG_SigortaOneri", sigorta);
            SetD(con, "EG_DolulukYuzde", fill);
            SetD(con, "EG_KisaDevreKesiti", scMin);
            SetS(con, "EG_HesapDurumu", durum);
            SetS(con, "EG_HesapTarihi", tarih);
        }

        private static void MergeUserAmpacity(
            Dictionary<string, Dictionary<double, double>> table, string path, OpContext ctx)
        {
            try
            {
                if (!File.Exists(path)) { ctx.Log($"  ampacity override bulunamadi: {path}"); return; }
                var json = File.ReadAllText(path, Encoding.UTF8);
                var user = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, Dictionary<string, double>>>(json);
                if (user == null) return;
                foreach (var kv in user)
                {
                    var inner = kv.Value.ToDictionary(
                        x => double.Parse(x.Key, CultureInfo.InvariantCulture), x => x.Value);
                    table[kv.Key] = inner; // kullanıcı tablosu o anahtarı tamamen değiştirir
                }
                ctx.Log($"  ampacity override yuklendi: {user.Count} anahtar ({path})");
            }
            catch (Exception ex)
            {
                ctx.Log($"  ampacity override okunamadi — {ex.Message} (gomulu tablo kullanilir)");
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Cetvel çıktı
        // ═════════════════════════════════════════════════════════════════════

        private static string BuildScheduleHtml(List<Dictionary<string, object?>> rows, string projName)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'>");
            sb.Append("<title>Elektrik Kablo/Conduit Cetveli</title><style>");
            sb.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#222}");
            sb.Append("h1{font-size:20px}table{border-collapse:collapse;width:100%;margin-top:12px}");
            sb.Append("th,td{border:1px solid #ddd;padding:6px 9px;font-size:12px;text-align:left}");
            sb.Append("th{background:#1f3a5f;color:#fff}");
            sb.Append("tr:nth-child(even){background:#f7f9fc}");
            sb.Append(".GERILIM_ASILDI,.DOLULUK_ASILDI,.KISA_DEVRE_YETERSIZ{background:#ffe0e0}");
            sb.Append(".UYGUN td:last-child{color:#0a0;font-weight:bold}");
            sb.Append("</style></head><body>");
            sb.Append($"<h1>Elektrik Kablo / Conduit Hesap Cetveli — {Esc(projName)}</h1>");
            sb.Append("<p>TS HD / IEC 60364-5-52. Sonuclar sorumlu elektrik muhendisi tarafindan dogrulanmalidir.</p>");
            sb.Append("<table><tr>");
            foreach (var h in new[] { "Devre", "Kaynak", "Hedef", "Kesit", "Uzunluk(m)",
                                      "Akim(A)", "GerilimDusumu(%)", "Doluluk(%)", "Sigorta(A)", "Durum" })
                sb.Append($"<th>{h}</th>");
            sb.Append("</tr>");
            foreach (var r in rows)
            {
                string durum = (r.GetValueOrDefault("durum") as string) ?? "";
                sb.Append($"<tr class='{Esc(durum)}'>");
                sb.Append($"<td>{Esc(Str(r,"devre"))}</td>");
                sb.Append($"<td>{Esc(Str(r,"kaynak"))}</td>");
                sb.Append($"<td>{Esc(Str(r,"hedef"))}</td>");
                sb.Append($"<td>{Esc(Str(r,"kesit"))}</td>");
                sb.Append($"<td>{Str(r,"uzunluk_m")}</td>");
                sb.Append($"<td>{Str(r,"akim_a")}</td>");
                sb.Append($"<td>{Str(r,"gerilim_dusumu_pct")}</td>");
                sb.Append($"<td>{Str(r,"doluluk_pct")}</td>");
                sb.Append($"<td>{Str(r,"sigorta_a")}</td>");
                sb.Append($"<td>{Esc(durum)}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></body></html>");
            return sb.ToString();
        }

        private static string BuildCsv(List<Dictionary<string, object?>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Devre;Kaynak;Hedef;Kesit;Uzunluk_m;Akim_A;GerilimDusumu_pct;Doluluk_pct;Sigorta_A;Durum");
            foreach (var r in rows)
                sb.AppendLine(string.Join(";", new[]
                {
                    Str(r,"devre"), Str(r,"kaynak"), Str(r,"hedef"), Str(r,"kesit"),
                    Str(r,"uzunluk_m"), Str(r,"akim_a"), Str(r,"gerilim_dusumu_pct"),
                    Str(r,"doluluk_pct"), Str(r,"sigorta_a"), Str(r,"durum"),
                }));
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Düşük seviyeli yardımcılar
        // ═════════════════════════════════════════════════════════════════════

        private static Category? TryGetCategory(Document doc, BuiltInCategory bic)
        {
            try { return doc.Settings.Categories.get_Item(bic); } catch { return null; }
        }

        private static string OrDefault(string? v, string d)
            => string.IsNullOrWhiteSpace(v) ? d : v.Trim();

        private static double ParamD(Element e, string name, double d)
        {
            var p = e.LookupParameter(name);
            if (p == null || !p.HasValue) return d;
            double v = p.AsDouble();
            return v == 0 ? d : v;
        }

        private static int ParamI(Element e, string name, int d)
        {
            var p = e.LookupParameter(name);
            if (p == null || !p.HasValue) return d;
            int v = p.AsInteger();
            return v == 0 ? d : v;
        }

        private static void SetD(Element e, string name, double v)
        {
            var p = e.LookupParameter(name);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) p.Set(v);
        }

        private static void SetS(Element e, string name, string v)
        {
            var p = e.LookupParameter(name);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String) p.Set(v);
        }

        private static double Round(Element e, string name, int dig)
        {
            var p = e.LookupParameter(name);
            return p != null && p.HasValue ? Math.Round(p.AsDouble(), dig) : 0;
        }

        private static string Str(Dictionary<string, object?> r, string k)
            => r.GetValueOrDefault(k)?.ToString() ?? "";

        private static Dictionary<string, object?> CalcRow(
            long cid, string devre, double len, double ib, string kesit,
            double vdPct, double fill, double sigorta, string durum)
            => new()
            {
                ["conduit_id"]         = cid.ToString(),
                ["devre"]              = devre,
                ["uzunluk_m"]          = Math.Round(len, 2),
                ["akim_a"]             = Math.Round(ib, 1),
                ["kesit_mm2"]          = kesit,
                ["gerilim_dusumu_pct"] = Math.Round(vdPct, 2),
                ["doluluk_pct"]        = Math.Round(fill, 1),
                ["sigorta_a"]          = Math.Round(sigorta, 0),
                ["durum"]              = durum,
            };

        private static List<Dictionary<string, object?>> ErrRow(string msg)
            => new() { new() { ["durum"] = "HATA", ["mesaj"] = msg } };

        private static string Esc(string? s)
            => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit baglami gerektirir.");
    }
}
