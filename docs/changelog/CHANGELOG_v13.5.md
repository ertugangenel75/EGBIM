# EGBIMOTO v13.5 — İnteraktif Seçim + Sonuç Görselleştirme Temeli

Kapsam notu: bu sürüm, "Dynamo/pyRevit/DiRoots değil, gerçek bir platform"
hedefinin iki somut temel taşını atar — model ile kullanıcı arasında
interaktif bir katman ve TaskDialog metin dökümünün yerini alan yapılandırılmış
sonuç görselleştirme. İkisi de mevcut `preview_gate`/`schedule_gate` callback
mimarisine doğal uzantı olarak kuruldu; yeni bir kavram icat edilmedi.

## 1. İnteraktif Seçim Katmanı

**Sorun:** Sistemin tek yönü vardı — manifest → kategori koleksiyonu →
filtre → yazma. Kullanıcının modelde elle "şu üç kolonu seç, üstünde işlem
yap" demesinin bir yolu yoktu (`PickObject` çağrısı tek bir yerde, MEP
straighten içinde gömülüydü).

**Çözüm — `selection_gate` / `selection_to_elements`:**
- `EGBIMOTO.Core/Selection/`: `SelectionRequestDto`, `SelectionResultDto`
  (Revit bağımsız, `PreviewGeometryDto` ile aynı desen).
- `DagExecutor`: `SELECTION_GATE_OP` intercept bloğu (preview_gate/schedule_gate
  ile birebir aynı yapı) + `UserSelectionCallback` property.
  `SelectionResultDto.ToString()` → "confirmed"/"cancelled" override sayesinde
  mevcut `EvalCondition` altyapısı hiç değişmeden `"$sec == confirmed"` çalışır.
- `EGBIMOTO.Addin/Revit/SelectionPickerService.cs`: gerçek
  `uidoc.Selection.PickObject(s)` çağrısı, kategori filtreli `ISelectionFilter`,
  Esc/`OperationCanceledException` yönetimi, min/max sayım doğrulaması.
- `EGBIMOTO.Addin/Ops/SelectionOps.cs`: `selection_gate` (defensive — DagExecutor
  intercept eder, doğrudan çağrılırsa throw eder; preview_gate/schedule_gate'in
  aynı deseni) + `selection_to_elements` (gerçek op — ElementId → Element çözümü,
  silinmiş ID'leri tolere eder).
- Callback, `EgbimotoApp.RunManifest()` (ana manifest çalıştırma yolu) ve
  `EgbimotoAppPreviewExtension` Phase 1'de bağlandı — hem normal hem
  preview-confirm akışlarında kullanılabilir.
- Referans manifest: `manifests/interaktif/01_secim_param_yaz.json`
  (selection_gate → selection_to_elements → write_param → classify_elements →
  show_table).

**Kapsam dışı (bilinçli):** Bağlantılı model referanslarında kategori filtresi
uygulanmıyor (yalnızca `allow_linked` bayrağı) — link-aware filtreleme ileri
sürüme bırakıldı, kod içinde açıkça belgelendi.

## 2. Sonuç Görselleştirme Temeli

**Sorun:** `show_table` ve `validation_summary` `TaskDialog.Show()` ile metin
dökülüyordu — 20 satır sınırı, tıklanamaz, modelle bağlantısız, dışa aktarım
yok. "Bazı manifestler çalışınca arayüz çıkmalı" ihtiyacının karşılığı yoktu.

**Çözüm:**
- `EGBIMOTO.Core/Results/ManifestResultDto.cs` — Kind (Generic/Validation/Table),
  Columns, Rows, Summary, ElementIds, Warnings. Revit bağımsız.
- `ManifestResultAdapter` — mevcut `ValidationReport` ve satır listelerini
  yeniden yazmadan bu DTO'ya çevirir (geriye dönük uyum köprüsü).
- `EGBIMOTO.Addin/UI/Results/IManifestResultRenderer` + `ManifestResultRendererRegistry`
  — genişletme noktası: ileride Kind bazlı özel görselleştirme (Takeoff pivot,
  Schedule Gantt) çağrı noktalarına dokunmadan eklenebilir.
- `ManifestResultWindow` — varsayılan renderer: `DataTable` tabanlı otomatik
  sütunlu DataGrid (`ManifestBrowserWindow` ile aynı koyu tema paleti),
  doğrulama sonuçlarında severity renk kodlama (ERROR/WARNING satır arka planı),
  "Modelde Göster" (seçili satır veya tüm `element_id`'ler → `uidoc.Selection` +
  `ShowElements` zoom), CSV dışa aktarım.
- `show_table` ve `validation_summary` bu pencereye yeniden bağlandı — Dict/scalar
  input için eski TaskDialog davranışı (grid'e uygun olmadığından) korundu.

## 3. SSoT Üretici İyileştirmesi (yan bulgu)

`deploy/generate_op_contracts.py` genişletildi:
- Yeni op'ların `Category` alanı artık koddan (`[EgOp(..., Category="...")]`)
  doğru okunuyor — önceden her yeni op sessizce "Genel" kovasına düşüyordu.
  Mevcut 478 op'un curated kategorileri değişmedi (legacy her zaman önceliklidir).
- `Description` ve `RequiresTransaction` alanları da koddan çıkarılıp kontrata
  yazılıyor. **Bunlar `generate_op_referansi.py`'de zaten okunuyordu ama hiç
  doldurulmuyordu** — yani OP_REFERANSI.md'deki 480 op açıklaması ve 🔒
  transaction ikonu bu sürüme kadar hiç render olmuyordu. Düzeltme sonrası
  OP_REFERANSI.md 112KB → 192KB (tüm op'lar artık açıklamalı).

## Sonuç

- Manifest korpusu: 356 → 357 dosya, 3013 → 3018 adım, **0 hata** (yeni
  `selection_gate` intercept edildiği için ManifestValidator'ın required-param
  kontrolüne girmez — normal davranış, preview_gate/schedule_gate ile aynı).
- Yeni dosya: 10 (Core: 4, Addin: 5, manifest: 1). Değiştirilen: 7.
- Yeni NuGet bağımlılığı: yok (WPF/DataGrid, System.Data, Revit Selection API
  — hepsi zaten projede).
- Versiyon: 13.0.0 → 13.5.0.

## Sıradaki Adımlar (bu sürüme dahil değil)
- Element Inspector paneli (dockable, tıklanan elemanın TR_ parametrelerini
  ve ilgili manifestleri gösteren panel).
- Family Library Window (aile kütüphanesi taraması, shared param uyumluluk
  denetimi — `param_guid_map.json` SSoT'i üstüne).
- Kind-özel renderer'lar (`TakeoffResultView`, gerçek `IManifestResultRenderer`
  implementasyonu ile).
- Gerçek Revit 2026'da derleme + selection_gate/ManifestResultWindow canlı testi
  (bu ortamda Revit API mevcut değil — derleme doğrulaması yapılamadı, yalnızca
  statik/mantıksal doğrulama yapıldı).
