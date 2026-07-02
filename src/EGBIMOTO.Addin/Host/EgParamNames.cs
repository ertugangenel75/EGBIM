namespace EGBIMOTO.Addin.Host
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EgParamNames  —  EGBIMOTO v7
    //
    //  EG_ önekli paylaşımlı parametre adları için tek doğru kaynak (SSoT).
    //  Tüm op dosyaları literal string yerine bu sabitleri kullanır.
    //
    //  Shared params dosyasındaki eşleşme:
    //    EGBIM_SharedParams.txt  (grup 38)
    //    GROUP 38  TR-4D/5D ZAMAN VE MALİYET
    //
    //  Parametre adı kuralları:
    //    Önek  : EG_
    //    Format: EG_<Alan>  (PascalCase, Türkçe karakter yok)
    //    Tip   : TEXT → tarih, metin  |  NUMBER → sayısal
    //
    //  Neden EG_ (TR_ değil)?
    //    TR_ → proje/standart düzeyinde zorunlu BIM parametreleri (ISO 19650)
    //    EG_ → EGBIMOTO araç setinin oluşturduğu hesaplama/otomasyon parametreleri
    //    İkisi aynı shared params dosyasında birlikte yaşayabilir.
    // ═══════════════════════════════════════════════════════════════════════════

    public static class EgParamNames
    {
        // ── 4D: Yapım Programı ────────────────────────────────────────────────

        /// <summary>
        /// Planlanan başlangıç tarihi — ISO 8601 metin ("2025-03-01").
        /// Revit türü: TEXT  |  Grup 38
        /// </summary>
        public const string BaslangicTarihi = "EG_BaslangicTarihi";

        /// <summary>
        /// Planlanan bitiş tarihi — ISO 8601 metin ("2025-03-25").
        /// Revit türü: TEXT  |  Grup 38
        /// </summary>
        public const string BitisTarihi = "EG_BitisTarihi";

        /// <summary>
        /// İnşaat fazı adı ("Betonarme", "Duvar", "Çatı" vb.).
        /// Revit türü: TEXT  |  Grup 38
        /// </summary>
        public const string FazAdi = "EG_FazAdi";

        /// <summary>
        /// WBS (Work Breakdown Structure) kodu ("1.2.3").
        /// Revit türü: TEXT  |  Grup 38
        /// </summary>
        public const string WbsKodu = "EG_WbsKodu";

        /// <summary>
        /// Tamamlanma yüzdesi — 0.0–100.0 sayısal.
        /// Revit türü: NUMBER  |  Grup 38
        /// </summary>
        public const string TamamlanmaPct = "EG_TamamlanmaPct";

        // ── 5D: Maliyet ───────────────────────────────────────────────────────

        /// <summary>
        /// ÇŞB poz numarası ("16.001/1").
        /// Revit türü: TEXT  |  Grup 38
        /// (Kalıp poz için TR_KalipPozNo [grup 37] tercih edilebilir;
        ///  bu alan imalat pozuna aittir.)
        /// </summary>
        public const string PozNo = "EG_PozNo";

        /// <summary>
        /// Poz adı kısa metni.
        /// Revit türü: TEXT  |  Grup 38
        /// </summary>
        public const string PozAdi = "EG_PozAdi";

        /// <summary>
        /// Birim fiyat (TL/birim).
        /// Revit türü: NUMBER  |  Grup 38
        /// </summary>
        public const string BirimMaliyet = "EG_BirimMaliyet";

        /// <summary>
        /// Toplam tutar = miktar × birim fiyat (TL).
        /// Revit türü: NUMBER  |  Grup 38
        /// </summary>
        public const string ToplamMaliyet = "EG_ToplamMaliyet";

        /// <summary>
        /// Miktar (m², m³, adet vb. — birimsiz sayı, birim PozBirim'de).
        /// Revit türü: NUMBER  |  Grup 38
        /// </summary>
        public const string PozMiktar = "EG_PozMiktar";

        /// <summary>
        /// Birim adı metin ("m²", "m³", "adet").
        /// Revit türü: TEXT  |  Grup 38
        /// </summary>
        public const string PozBirim = "EG_PozBirim";

        // ── Okuma yardımcısı (TryReadElementSchedule için alias dizisi) ───────

        /// <summary>
        /// Başlangıç tarihi için kontrol edilecek parametre adları (öncelik sırasıyla).
        /// </summary>
        public static readonly string[] BaslangicAliases =
        {
            BaslangicTarihi,
            "EG_Start",
            "Başlangıç Tarihi",
            "BaslangicTarihi",
            "Start Date"
        };

        /// <summary>
        /// Bitiş tarihi için kontrol edilecek parametre adları (öncelik sırasıyla).
        /// </summary>
        public static readonly string[] BitisAliases =
        {
            BitisTarihi,
            "EG_End",
            "Bitiş Tarihi",
            "BitisTarihi",
            "End Date"
        };

        /// <summary>
        /// Faz adı için kontrol edilecek parametre adları (öncelik sırasıyla).
        /// </summary>
        public static readonly string[] FazAliases =
        {
            FazAdi,
            "EG_Phase",
            "Faz Adı",
            "FazAdi",
            "Phase"
        };
    }
}
