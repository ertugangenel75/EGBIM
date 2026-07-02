using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO — Kolon Ön Boyutlandırma (ColumnPresizingOps) — v10
    ///
    /// Standartlar: TBDY 2018 Bölüm 7.3 (Süneklik Düzeyi Yüksek Kolonlar) + TS 500 Bölüm 7.4.
    ///
    /// Doğrulanmış koşullar (resmi yönetmelik — kamu malı formüller):
    ///   1) Min boyut (TBDY 7.3.1.1): dikdörtgen min enkesit ≥ 300 mm,
    ///      dairesel çap ≥ 350 mm. (TS 500 7.4.1: 250 mm; TBDY daha kısıtlayıcı, esas.)
    ///   2) Deprem eksenel sınır (TBDY 7.3.1.2): Ac ≥ Ndm / (0.40·fck)
    ///      Ndm = G+Q+E ortak etkisi altında en büyük eksenel basınç (TS 498 yük azaltma dahil).
    ///   3) TS 500 sınır (artırılmış düşey yük): Nd ≤ 0.9·fcd·Ac  →  Ac ≥ Nd/(0.9·fcd)
    ///   4) Kiriş/kolon ayrımı (TBDY 7.4.1.2): kiriş ise Nd ≤ 0.10·Ac·fck olmalı;
    ///      aşılırsa eleman kolon olarak boyutlandırılır (bilgi amaçlı kontrol).
    ///   5) Önerilen min kare boyut: max(min_boyut, √(gereken_Ac)) → 50 mm yuvarlama.
    ///
    /// fcd = fck / 1.5 (beton malzeme katsayısı γmc = 1.5).
    ///
    /// MİMARİ: Bu hesabın TEK dış girdisi eksenel kuvvet Ndm (bir analiz sonucu).
    /// İki yol sunulur:
    ///   (A) Kullanıcı EG_Ndm girer (analiz sonucu — kesin).
    ///   (B) EG_YukAlani × EG_KatSayisi × EG_BirimYuk → kaba Ndm tahmini (ön boyutlandırma).
    /// Geometri (b, h) ve beton sınıfı (EG_BetonSinif / EG_fck_Override) modelden okunur.
    ///
    /// ⚠️ SORUMLULUK: ÖN boyutlandırma/kontrol amaçlıdır. Kesin tasarım, ikinci mertebe
    /// analiz ve eğilme etkileşimi sorumlu yapı mühendisi tarafından yapılmalıdır.
    ///
    /// Op listesi:
    ///   column_setup_params       — EGBIM_KolonParams.txt'i yükler (sabit GUID), kolona bind.
    ///   structural_column_presizing — kolonları kontrol eder + min boyut önerir, sonuçları yazar.
    /// </summary>
    public static class ColumnPresizingOps
    {
        private const double GammaMc = 1.5;  // beton malzeme katsayısı (fcd = fck/γmc)

        // ─────────────────────────────────────────────────────────────────────
        // OP 1  column_setup_params
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("column_setup_params",
            RequiresTransaction = true,
            Description =
                "EGBIM kolon on boyutlandirma shared parametrelerini (sabit GUID) projeye yukler\n" +
                "ve StructuralColumns kategorisine instance binding yapar.\n\n" +
                "params:\n" +
                "  spf_path — SPF yolu (opsiyonel, default: mapping/EGBIM_KolonParams.txt)\n\n" +
                "Hesaptan ONCE bir kez calistirilir (altyapi kurulumu).\n" +
                "Cikti: added, skipped, spf_path",
            Category = "Yapısal")]
        public static Dictionary<string, object?> SetupParams(OpContext ctx)
        {
            var rctx = RequireRevit(ctx);
            var spfPathRaw = ctx.GetString("spf_path", "");

            string spfPath = !string.IsNullOrEmpty(spfPathRaw)
                ? (Path.IsPathRooted(spfPathRaw) ? spfPathRaw : Path.Combine(EgbimotoData.DataRoot, spfPathRaw))
                : Path.Combine(EgbimotoData.DataRoot, "mapping", "EGBIM_KolonParams.txt");

            if (!File.Exists(spfPath))
                throw new FileNotFoundException($"Kolon SPF dosyasi bulunamadi: {spfPath}");

            var app     = rctx.UiApp.Application;
            var prevSpf = app.SharedParametersFilename;
            app.SharedParametersFilename = spfPath;
            var spFile = app.OpenSharedParameterFile()
                ?? throw new InvalidOperationException("Kolon paylasimli parametre dosyasi acilamadi.");

            var cats = new[] { BuiltInCategory.OST_StructuralColumns };

            int added = 0, skipped = 0;
            using var scope = new RevitWriteScope(rctx.Doc, "Kolon Parametre Ekle", rctx.IsAtomicMode);
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
                            bindMap.Insert(def, binding, GroupTypeId.StructuralAnalysis);
                            added++;
                        }
                        else skipped++;
                    }
                    catch { skipped++; }
                }
            }
            scope.Commit();
            app.SharedParametersFilename = prevSpf;

            ctx.Log($"  column_setup_params: {added} eklendi, {skipped} atlandi → {spfPath}");
            return new() { ["added"] = added, ["skipped"] = skipped, ["spf_path"] = spfPath };
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2  structural_column_presizing
        //
        // input : List<Element> (StructuralColumn) opsiyonel — boşsa tüm kolonlar
        // params: default_fck     Double default=25  (EG_BetonSinif/Override yoksa)
        //         default_birim_yuk Double default=12 (kN/m², Ndm tahmini)
        //         duktilite       String default="yuksek" (min boyut: yuksek=300/sinirli=300)
        //         round_to_mm     Double default=50  (öneri boyutu yuvarlama adımı)
        //
        // output: List<Dict> kolon başına kontrol + öneri
        // ─────────────────────────────────────────────────────────────────────
        [EgOp("structural_column_presizing",
            RequiresTransaction = true,
            Description =
                "Betonarme kolonlari TBDY 2018 / TS 500 kesit kosullarina gore KONTROL eder ve\n" +
                "gereken min kesiti ONERIR. Sonuclari kolona yazar.\n\n" +
                "Kontroller:\n" +
                "  1) Min boyut (TBDY 7.3.1.1): dik ≥300mm, dairesel cap ≥350mm\n" +
                "  2) Deprem eksenel (TBDY 7.3.1.2): Ac ≥ Ndm/(0.40·fck)\n" +
                "  3) TS 500 sinir: Nd ≤ 0.9·fcd·Ac\n" +
                "  4) Kiris/kolon ayrimi (TBDY 7.4.1.2): Nd ≤ 0.10·Ac·fck\n" +
                "  5) Onerilen min kare boyut\n\n" +
                "Ndm girdisi: EG_Ndm (analiz sonucu) doluysa kullanilir; yoksa\n" +
                "EG_YukAlani × EG_KatSayisi × EG_BirimYuk ile tahmin edilir.\n" +
                "fck: EG_BetonSinif (orn 'C30') veya EG_fck_Override; yoksa default_fck.\n\n" +
                "params:\n" +
                "  default_fck       — varsayilan fck MPa (default 25)\n" +
                "  default_birim_yuk — varsayilan kat birim yuku kN/m² (default 12)\n" +
                "  duktilite         — yuksek|sinirli (default yuksek)\n" +
                "  round_to_mm       — oneri yuvarlama adimi (default 50)\n\n" +
                "Cikti: kolon_id, b_mm, h_mm, ac_mm2, ndm_kn, gereken_ac, oneri_boyut, durum",
            Category = "Yapısal")]
        public static List<Dictionary<string, object?>> ColumnPresizing(OpContext ctx)
        {
            var rctx       = RequireRevit(ctx);
            var doc        = rctx.Doc;
            double defFck  = ctx.GetDouble("default_fck", 25);
            double defBirim = ctx.GetDouble("default_birim_yuk", 12);
            string duktilite = ctx.GetString("duktilite", "yuksek").ToLowerInvariant();
            double roundTo = ctx.GetDouble("round_to_mm", 50);
            if (roundTo <= 0) roundTo = 50;

            // Min boyut: TBDY 7.3.1.1 — dikdörtgen 300mm (yüksek ve sınırli aynı min)
            double minBoyutDik = 300.0;
            double minCapDairesel = 350.0;

            var columns = ctx.InputAsOrDefault<List<Element>>(new List<Element>())
                .Where(e => Rv.GetCategoryId(e) == (int)BuiltInCategory.OST_StructuralColumns)
                .ToList();
            if (columns.Count == 0)
                columns = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .WhereElementIsNotElementType()
                    .ToList();

            if (columns.Count == 0)
            {
                ctx.Log("  structural_column_presizing: kolon bulunamadi");
                return ErrRow("Yapisal kolon (StructuralColumn) bulunamadi.");
            }

            ctx.Log($"  structural_column_presizing: {columns.Count} kolon, default fck={defFck} MPa");

            var rows = new List<Dictionary<string, object?>>();
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            using var scope = new RevitWriteScope(doc, "Kolon On Boyutlandirma", rctx.IsAtomicMode);

            foreach (var col in columns)
            {
                long cid = Rv.GetId(col.Id);

                // Geometri: bounding box'tan b (kısa), h (uzun) — mm
                var bb = col.get_BoundingBox(null);
                double b = 0, h = 0;
                if (bb != null)
                {
                    double dx = UnitUtils.ConvertFromInternalUnits(bb.Max.X - bb.Min.X, UnitTypeId.Millimeters);
                    double dy = UnitUtils.ConvertFromInternalUnits(bb.Max.Y - bb.Min.Y, UnitTypeId.Millimeters);
                    b = Math.Min(dx, dy);  // kısa kenar
                    h = Math.Max(dx, dy);  // uzun kenar
                }

                // Kolon tipi
                string tip = (col.LookupParameter("EG_KolonTipi")?.AsString() ?? "").ToLowerInvariant();
                bool dairesel = tip.Contains("daire") || tip.Contains("circ") ||
                                (Math.Abs(b - h) < 1 && tip.Contains("d"));

                double ac = dairesel
                    ? Math.PI * Math.Pow(b / 2.0, 2)  // dairesel: b = çap kabul
                    : b * h;                          // dikdörtgen

                // fck belirle
                double fck = ResolveFck(col, defFck);
                double fcd = fck / GammaMc;

                // Ndm belirle: EG_Ndm varsa kullan, yoksa yük alanından tahmin
                double ndm = ParamD(col, "EG_Ndm", 0);
                bool ndmEstimated = false;
                if (ndm <= 0)
                {
                    double yukAlani = ParamD(col, "EG_YukAlani", 0);
                    int katSayisi = ParamI(col, "EG_KatSayisi", 0);
                    double birimYuk = ParamD(col, "EG_BirimYuk", defBirim);
                    if (yukAlani > 0 && katSayisi > 0)
                    {
                        ndm = yukAlani * katSayisi * birimYuk; // kN
                        ndmEstimated = true;
                    }
                }

                // ── Kontroller ──

                // 1) Min boyut (TBDY 7.3.1.1)
                bool minOk;
                if (dairesel) minOk = b >= minCapDairesel - 0.5;
                else minOk = b >= minBoyutDik - 0.5;
                string minKontrol = minOk ? "UYGUN" : "YETERSIZ";

                // 2) TBDY eksenel: Ac ≥ Ndm/(0.40·fck)
                //    fck MPa = N/mm², Ndm kN = 1000 N → gereken Ac (mm²)
                double gerekenAcTbdy = ndm > 0 ? (ndm * 1000.0) / (0.40 * fck) : 0;
                string tbdyKontrol = ndm <= 0 ? "NDM_YOK"
                    : (ac >= gerekenAcTbdy ? "UYGUN" : "YETERSIZ");

                // 3) TS 500: Nd ≤ 0.9·fcd·Ac → Ac ≥ Nd/(0.9·fcd)
                double gerekenAcTs500 = ndm > 0 ? (ndm * 1000.0) / (0.9 * fcd) : 0;
                string ts500Kontrol = ndm <= 0 ? "NDM_YOK"
                    : (ac >= gerekenAcTs500 ? "UYGUN" : "YETERSIZ");

                // 4) Kiriş/kolon ayrımı: Nd ≤ 0.10·Ac·fck (bilgi — element kolon ama sınır kontrolü)
                double eksenelOran = (ac > 0 && fck > 0) ? (ndm * 1000.0) / (ac * fck) : 0;
                string kirisAyrim = ndm <= 0 ? "NDM_YOK"
                    : (eksenelOran <= 0.10 ? "DUSUK_EKSENEL(kiris_gibi)" : $"KOLON(N/Acfck={eksenelOran:0.00})");

                // 5) Önerilen min kare boyut: max(min_boyut, √(max gereken Ac))
                double gerekenAc = Math.Max(gerekenAcTbdy, gerekenAcTs500);
                double minBoyut = dairesel ? minCapDairesel : minBoyutDik;
                double oneriBoyut;
                if (gerekenAc > 0)
                {
                    double gerekenKenar = dairesel
                        ? Math.Sqrt(4 * gerekenAc / Math.PI)  // dairesel çap
                        : Math.Sqrt(gerekenAc);               // kare kenar
                    oneriBoyut = Math.Max(minBoyut, gerekenKenar);
                }
                else oneriBoyut = minBoyut;
                oneriBoyut = Math.Ceiling(oneriBoyut / roundTo) * roundTo; // yukarı yuvarla

                // Genel durum
                string durum;
                if (!minOk) durum = "BOYUT_YETERSIZ";
                else if (tbdyKontrol == "YETERSIZ" || ts500Kontrol == "YETERSIZ") durum = "EKSENEL_YETERSIZ";
                else if (ndm <= 0) durum = "NDM_GIRILMELI";
                else durum = "UYGUN";

                // Parametrelere yaz
                WriteOutputs(col, b, h, ac, ndm, gerekenAcTbdy, oneriBoyut,
                    minKontrol, tbdyKontrol, ts500Kontrol, kirisAyrim, durum, now);

                rows.Add(new Dictionary<string, object?>
                {
                    ["kolon_id"]    = cid.ToString(),
                    ["b_mm"]        = Math.Round(b, 0),
                    ["h_mm"]        = Math.Round(h, 0),
                    ["ac_mm2"]      = Math.Round(ac, 0),
                    ["fck_mpa"]     = fck,
                    ["ndm_kn"]      = Math.Round(ndm, 1) + (ndmEstimated ? " (tahmin)" : ""),
                    ["gereken_ac"]  = Math.Round(gerekenAc, 0),
                    ["oneri_boyut"] = Math.Round(oneriBoyut, 0),
                    ["min_boyut"]   = minKontrol,
                    ["tbdy_eksenel"]= tbdyKontrol,
                    ["ts500"]       = ts500Kontrol,
                    ["durum"]       = durum,
                });
            }

            scope.Commit();
            int uygun = rows.Count(r => (string?)r["durum"] == "UYGUN");
            int yetersiz = rows.Count(r => ((string?)r["durum"])?.Contains("YETERSIZ") == true);
            ctx.Log($"  structural_column_presizing: {uygun} UYGUN, {yetersiz} YETERSIZ, {rows.Count} toplam");
            return rows;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Yardımcılar
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>fck belirle: EG_BetonSinif ('C30'→30) > EG_fck_Override > default.</summary>
        private static double ResolveFck(Element col, double defFck)
        {
            // EG_BetonSinif (mevcut EGBIMOTO parametresi): "C30", "C25/30" gibi
            string betonSinif = col.LookupParameter("EG_BetonSinif")?.AsString() ?? "";
            double fckFromClass = ParseFckFromClass(betonSinif);
            if (fckFromClass > 0) return fckFromClass;

            // EG_fck_Override (sayısal)
            double over = col.LookupParameter("EG_fck_Override")?.AsDouble() ?? 0;
            if (over > 0) return over;

            return defFck;
        }

        /// <summary>'C30' / 'C25/30' / 'C 30' → 30 (silindir dayanımı, ilk sayı).</summary>
        private static double ParseFckFromClass(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            // C harfinden sonraki ilk sayıyı al
            var digits = new StringBuilder();
            bool started = false;
            foreach (char ch in s)
            {
                if (char.IsDigit(ch)) { digits.Append(ch); started = true; }
                else if (started) break; // ilk sayı bitti ('/' veya '/' öncesi)
            }
            return double.TryParse(digits.ToString(), out var v) ? v : 0;
        }

        private static void WriteOutputs(Element col, double b, double h, double ac, double ndm,
            double gerekenAcTbdy, double oneriBoyut, string minK, string tbdyK, string ts500K,
            string kirisAyrim, string durum, string tarih)
        {
            SetD(col, "EG_MevcutB", Math.Round(b, 0));
            SetD(col, "EG_MevcutH", Math.Round(h, 0));
            SetD(col, "EG_MevcutAc", Math.Round(ac, 0));
            SetD(col, "EG_KullanilanNdm", Math.Round(ndm, 1));
            SetD(col, "EG_GerekenAc_TBDY", Math.Round(gerekenAcTbdy, 0));
            SetD(col, "EG_OneriBoyut", Math.Round(oneriBoyut, 0));
            SetS(col, "EG_MinBoyutKontrol", minK);
            SetS(col, "EG_EksenelKontrol_TBDY", tbdyK);
            SetS(col, "EG_EksenelKontrol_TS500", ts500K);
            SetS(col, "EG_KirisKolonAyrim", kirisAyrim);
            SetS(col, "EG_KolonDurumu", durum);
            SetS(col, "EG_HesapTarihi", tarih);
        }

        // ── Düşük seviyeli ──

        private static Category? TryGetCategory(Document doc, BuiltInCategory bic)
        { try { return doc.Settings.Categories.get_Item(bic); } catch { return null; } }

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

        private static List<Dictionary<string, object?>> ErrRow(string msg)
            => new() { new() { ["durum"] = "HATA", ["mesaj"] = msg } };

        private static RevitOpContext RequireRevit(OpContext ctx)
            => ctx as RevitOpContext
               ?? throw new InvalidOperationException(
                   $"[{ctx.CurrentStepId}] Bu op Revit baglami gerektirir.");
    }
}
