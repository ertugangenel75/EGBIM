# EGBIMOTO — Hızlı Başlangıç

EGBIMOTO, Revit için manifest-tabanlı bir BIM otomasyon platformudur. JSON ile
tanımlanan iş akışları (manifest) bir DAG (yönlü asiklik graf) motoruyla çalıştırılır.
Türk AEC standartlarına (TR BIM, ÇŞB 2026, TS500, TBDY 2018, IFC/IDS) odaklanır.

## Kurulum

EGBIMOTO native bir C# .NET 8 Revit add-in'idir. pyRevit, Python veya başka bir
runtime gerektirmez.

1. `dotnet build src/EGBIMOTO.Addin -c Release -p:RevitVersion=2026` ile derleyin
   (2024 ve 2025 için `-p:RevitVersion=2024` / `2025`).
2. Çıktı klasöründeki `EGBIMOTO.Addin.dll` ve yanındaki dosyaları (`manifests/`,
   `data/`, `op_contracts.json`, `categories.json`) Revit add-in dizinine kopyalayın:
   `%AppData%\Autodesk\Revit\Addins\2026\`
3. `.addin` manifest dosyasını aynı dizine yerleştirin.
4. Revit'i başlatın. EGBIMOTO sekmesi şeritte görünecektir.

## İlk Manifest'i Çalıştırma

1. Şeritte **EGBIMOTO → Otomasyon → Manifest Browser** butonuna tıklayın.
2. Açılan pencerede tüm hazır iş akışları kategoriye göre listelenir.
3. Arama kutusuna anahtar kelime yazarak filtreleyin (başlık, kategori, açıklama
   ve etiketlerde arama yapar — örn. "yangın", "kalıp", "ifc").
4. Bir manifest seçin; sağ panelde açıklaması, lint skoru ve adım sayısı görünür.
5. **Çalıştır** butonuna basın. İşlem tamamlandığında sonuç (rapor/tablo) açılır.

## İki Çalıştırma Modu

**Önizle ve Onayla (Preview-Confirm):** Modeli değiştiren manifest'ler önce bir
3D önizleme gösterir. Onaylarsanız değişiklikler tek atomik transaction olarak
uygulanır; iptal ederseniz hiçbir şey değişmez.

**Atomik (Atomic):** Tüm adımlar tek transaction içinde çalışır. Herhangi bir
zorunlu adım başarısız olursa tüm değişiklikler geri alınır (rollback).

## AI ile Manifest Üretme

Şeritte **AI Manifest Üretici** ile doğal dilde iş akışı tanımlayabilirsiniz:

- **Pattern modu** (API'siz, ~100 ms): Anahtar kelime tabanlı şablon eşleştirme.
  "Tüm duvarların kalıp metrajını çıkar ve Excel'e aktar" → hazır manifest.
- **AI modu** (Claude API, anahtar gerekli): 400 op'un tamamını kullanarak
  serbest manifest üretir, otomatik doğrular ve hatalıysa kendini düzeltir.

## Claude Desktop ile Kullanma (MCP Server)

EGBIMOTO, Claude Desktop'ın doğrudan Revit modeline bağlanmasını sağlayan bir MCP
Server içerir. Bağlandığınızda Türkçe doğal dil komutu verir, Claude EGBIMOTO'nun
op katalogunu okuyup manifest'i kendisi üretir ve çalıştırır.

1. Şeritte **EGBIMOTO → Otomasyon → MCP Server** butonuna tıklayın (başlat/durdur).
   Server `localhost:5577`'de yalnızca yerel olarak dinler.
2. Python köprüsünü kurun ve Claude Desktop config'ine ekleyin — adımlar:
   `mcp_bridge/README.md`.
3. Claude Desktop'ta "egbimoto" bağlandıktan sonra örn. *"tüm kapıları say ve rapor
   çıkar"* yazın; Claude uygun op'lardan manifest üretip Revit'te çalıştırır.

Bağlantı durumunu tarayıcıdan da görebilirsiniz: `http://127.0.0.1:5577/health`
(durum + aktif doküman), `http://127.0.0.1:5577/ops` (op katalogu).

## Sonraki Adımlar

- Yeni iş akışı yazmak için → `MANIFEST_YAZIM_REHBERI.md`
- Tüm op'ların referansı için → `OP_REFERANSI.md`
- Versiyon değişiklikleri için → `CHANGELOG_v10.md`
