# EGBIMOTO v14 — Element Inspector Paneli + Aile Kütüphanesi

Kapsam notu: "platform" hedefinin iki kalan temel taşı — kullanıcının seçtiği
elemanı anında anlaması (Element Inspector) ve aile kütüphanesinin TR_BIM
GUID standardına uyumunu topluca denetlemesi (Family Library). İkisi de v12.1
GUID standardizasyonu ve v13.5 sonuç görselleştirme temeli üzerine kurulu —
yeni bir veri modeli icat edilmedi, mevcut SSoT'ler (`param_guid_map.json`,
`trbim_category_param_index.json`, manifest korpusu) yeniden kullanıldı.

## 1. Element Inspector Paneli (dockable)

**Sorun:** Bir manifest çalıştırıp sonucu görmek dışında, kullanıcının
modelde tıkladığı elemanın "sağlığını" (TR_ parametreleri dolu mu?) ve
"bu elemanla ne yapabilirim?" (hangi manifestler bu kategoriyi hedefliyor?)
anında görmesinin bir yolu yoktu.

**Çözüm:**
- `EGBIMOTO.Addin/Inspector/TrBimCategoryParamIndex.cs` — 46 Revit kategorisini
  (data/test/trbim_category_param_index.json) dil-bağımsız `BuiltInCategory`
  anahtarlı bir sözlüğe çevirir. **Kritik detay:** `Element.Category.Name`
  Revit UI diline göre yerelleşir (Türkçe Revit'te "Walls" değil "Duvarlar"
  döner) — bu yüzden İngilizce görünen ad yerine `BuiltInCategory` enum'ı
  üzerinden eşleştirme yapılır. 46 kategorinin tamamı Python ile çapraz
  doğrulandı (haritalanmamış: 0).
- `EGBIMOTO.Addin/Inspector/ManifestDisciplineIndex.cs` — 357 manifesti
  collect_* op'larına göre tarar, kategori→manifest indeksi kurar.
- `EGBIMOTO.Addin/UI/Inspector/ElementInspectorPane.xaml(.cs)` — dockable
  UserControl: seçili elemanın TR_ parametre listesi (✓ dolu/yeşil,
  ✗ eksik/kırmızı nokta), doluluk özeti, ilgili manifest listesi.
- `ElementInspectorHook.cs` — Revit'in standart "UIApplication'a erişim"
  deseni: `UIControlledApplication.Idling`'e abone olup ilk tetiklemede
  `sender`'ı `UIApplication`'a cast ederek `SelectionChanged`'e bağlanır,
  kendi Idling aboneliğini kaldırır.
- `ElementInspectorPaneProvider.cs` (`IDockablePaneProvider`) — `App.OnStartup`'ta
  sabit GUID'li `DockablePaneId` ile kaydedilir.
- Ribbon: "Eleman İncele" toggle butonu (Otomasyon paneli).

**Bilinçli sınırlar (dokümante):**
- "İlgili Manifestler" linkleri belirli bir manifesti DOĞRUDAN çalıştırmaz —
  dockable pane bağlamında gerçek bir `ExternalCommandData` yoktur. Bunun yerine
  `RevitCommandId.LookupCommandId` + `UIApplication.PostCommand` ile Manifest
  Browser'ı açar ve manifest başlığını panoya kopyalar.
- İndeks yalnızca built-in + user manifest kökleriyle kurulur (proje-özel
  manifestler dahil değil — bu bir öneri listesidir, tam arama sonucu değildir).

## 2. Aile Kütüphanesi Penceresi

**Sorun:** v12.1'de 308 sahte GUID deterministik UUID'lerle değiştirildi
(SSoT: `param_guid_map.json`), ama bu SSoT'in aile dosyalarındaki GERÇEK
paylaşımlı parametrelerle uyumlu olup olmadığını denetleyen hiçbir araç
yoktu. Aynı isimli ama farklı GUID'li bir parametre projeye yüklenirse Revit
"duplicate parameter" birleştirmesi yapar — sessiz, geç fark edilen bir hata sınıfı.

**Çözüm:**
- `EGBIMOTO.Addin/Family/FamilyLibraryScanner.cs` — bir klasördeki tüm `.rfa`
  dosyalarını (alt klasörler dahil) `Application.OpenDocumentFile` ile TEK TEK
  açar (bu codebase'de zaten kanıtlanmış desen — `FamilyCreateOps.cs`),
  `FamilyManager.GetParameters()` ile okur, `param_guid_map.json` ile
  karşılaştırır, kaydetmeden kapatır. Hiçbir Transaction açmaz — salt okunur.
  Dört durum: `Ok` (uyumlu), `GuidConflict` (aynı isim farklı GUID — kırmızı
  uyarı), `NotSharedButLooksTr` (TR_/EG_ isimli ama paylaşımlı değil),
  `UnknownShared` (paylaşımlı ama SSoT'te yok).
- `EGBIMOTO.Addin/UI/Family/DispatcherFrameHelper.cs` — **kritik mimari not:**
  Revit API çağrıları yalnızca ana thread'de çalışabildiği için tarama döngüsü
  `Task.Run`/`BackgroundWorker` İLE ÇALIŞTIRILAMAZ. Bunun yerine WPF'in
  `Dispatcher.PushFrame` ile "DoEvents" taklidi yapılır — her dosyadan sonra
  UI mesajları pompalanır, böylece ilerleme çubuğu güncellenir ve İptal
  butonu tepki verir, tüm işlem yine de senkron ve ana thread'de kalır.
- `EGBIMOTO.Addin/UI/Family/FamilyLibraryWindow.xaml(.cs)` — klasör seç
  (.NET 8 `Microsoft.Win32.OpenFolderDialog`) → tara (300+ dosyada onay istenir)
  → master/detail (soldan aile listesi + durum noktası, sağda seçili ailenin
  parametre detayı, çakışma/uyarı üstte sıralı) → CSV dışa aktarım.
- Ribbon: "Aile Kütüphanesi" butonu (Otomasyon paneli).

## Diğer

- İki yeni ikon: `inspector.png` (büyüteç+eleman), `family.png` (klasör+aile) —
  mevcut 20 ikonla aynı üretim script'i ve tasarım dili.
- Manifest korpusu değişmedi (357 dosya, 3018 adım, 0 hata) — bu sürüm yalnızca
  Addin katmanına yeni pencere/panel ekliyor, manifest şemasına dokunmuyor.

## Doğrulama ve dürüst sınır

Bu ortamda Revit SDK/`dotnet build` yok — statik doğrulama (brace/paren dengesi,
XAML geçerliliği, XAML↔code-behind x:Name ve event handler çapraz kontrolü,
kategori haritalama tam doğrulaması) yapıldı, gerçek derleme yapılamadı.

Yüksek güvenle doğrulanan API kullanımları (bu codebase'in KENDİ çalışan
koduyla birebir karşılaştırıldı):
- `Application.OpenDocumentFile(path)` + `FamilyManager.GetParameters()` —
  `FamilyCreateOps.cs`'de zaten üretimde.
- `ExternalDefinition.GUID` — `ParamOps.ListSharedParams`'ta zaten üretimde.

Canlı Revit'te ayrıca doğrulanması gereken noktalar (dokümante edildi, kodda
işaretli): `RevitCommandId.LookupCommandId` ile custom command adresleme,
`Microsoft.Win32.OpenFolderDialog` (.NET 8 WPF), `UIControlledApplication.Idling`
sender'ının `UIApplication`'a cast edilebilirliği. Bunların hepsi standart,
yaygın kullanılan Revit API idiyomları ama bu oturumda gerçek bir Revit
oturumuna karşı çalıştırılamadı.

## Sıradaki Adımlar (bu sürüme dahil değil)
- Element Inspector'da "İlgili Manifestler" için gerçek tek-tık çalıştırma
  (ManifestBrowserWindow'a bir `initialFilter` parametresi eklenmesini gerektirir).
- Family Library'de eksik (SSoT'te olup ailede olmayan) parametrelerin
  raporlanması — şu an yalnızca ailede VAR OLAN parametreler denetleniyor.
- Gerçek Revit 2026'da derleme + üç özelliğin (interaktif seçim, sonuç
  penceresi, Inspector, Family Library) uçtan uca canlı testi.

## v14 Hotfix — Statik inceleme düzeltmeleri (7 adet)

Derleme hataları (REVIT2026 varsayılan build):
1. `ElementInspectorPane.xaml.cs` — `Color` CS0104 belirsizliği → `using Color = System.Windows.Media.Color;` alias'ı.
2. `ElementInspectorPane.xaml.cs` — `el.Category.Id.IntegerValue` (Revit 2025+'ta kaldırıldı, 2 yer) → `Rv.GetCategoryId(el)`; -1 dönüşü `(BuiltInCategory)(-1) == INVALID` olduğu için ayrı null dalı kaldırıldı.
3. `ElementInspectorHook.cs` — `EGBIMOTO.Addin.Inspector` using'i eksikti (CS0103: `ManifestDisciplineIndex`).
4. `ElementInspectorHook.cs` — `e.Document` diye bir property yok → `e.GetDocument()`.
5. `FamilyLibraryWindow.xaml.cs` — `Application` CS0104 belirsizliği → `RvtApp` alias'ı.

Mantık hataları:
6. `TrBimCategoryParamIndex.Initialize()` hiçbir yerden çağrılmıyordu — panel her elemanda "beklenti tanımlı değil" gösterirdi. Artık `ElementInspectorHook.BuildManifestIndexOnce()` içinde çağrılıyor.
7. `FamilyLibraryScanner` — `fp.Definition is ExternalDefinition` FamilyManager parametrelerinde asla tutmaz (Definition paylaşımlıda bile internal'dır); GUID karşılaştırması hiç çalışmıyor, `GuidConflict` asla tetiklenmiyordu. Doğru kaynak: `FamilyParameter.GUID` (IsShared iken geçerli).

Açık kalan (bilinçli, canlı Revit doğrulaması gerektirir): `OpenManifestBrowser()` içindeki
`RevitCommandId.LookupCommandId("EGBIMOTO.Addin.Commands.ManifestBrowserCommand")` büyük
olasılıkla null döner — addin komutlarının aranabilir kimliği sınıf adı değil, ribbon'un
ürettiği `CustomCtrl_%CustomCtrl_%EGBIMOTO%Otomasyon%EG_BROWSER` biçimindeki iç kimliktir.
Kod null'u sessizce yutar (buton işlevsiz görünür). Ayrıca küçükler: Commands.cs'te
yinelenen 7b yorum bloğu, tarama sırasında pencere kapatılırsa döngünün sürmesi
(OnClosing'de iptal önerilir), CSV alan kaçışı yok, FilterBox Tag placeholder'ı render edilmiyor.

## v14 Hotfix 2 — İlk gerçek derleme sonuçları (Core ✓, Addin'de 27 hata → 0 kök neden: 4)

1. **DagExecutor.cs — eksik `try`** (Core'un 10 hatası): v13.5 selection gate bloğu
   eklenirken op çalıştırma bloğunu saran `try` anahtar kelimesi silinmiş, çıplak
   `{ }` bloğu kalmıştı; 412/422'deki `catch`'ler geçersizdi. Süslü parantez dengesi
   bozulmadığı için statik sayımdan kaçtı — tree-sitter parse'ı birebir aynı
   satır/kolonları işaretledi. `try` geri kondu.
2. **`EGBIMOTO.Addin.Family` namespace'i Revit'in `Family` TİPİNİ gölgeliyordu**
   (CS0118 ×13: PreCheckOps, QaOps, ParamOps, FamilyCreateOps, FamilyOps,
   CollectionOps): `EGBIMOTO.Addin.*` içindeki her dosyada nitelenmemiş `Family`
   artık tipe değil yeni namespace'e çözümleniyordu. Namespace
   `EGBIMOTO.Addin.FamilyLibrary` / `EGBIMOTO.Addin.UI.FamilyLibrary` olarak
   yeniden adlandırıldı (klasör adları aynı kaldı); XAML `x:Class` ve
   `Commands.cs` referansı güncellendi. Ders: Revit API'nin kök tip adlarıyla
   (Family, Level, View, Material...) çakışan namespace segmenti açma.
3. **`Visibility` CS0104→CS0176 zinciri** (ElementInspectorPane ×11):
   `Autodesk.Revit.DB.Visibility` tipi `System.Windows.Visibility` ile çakışınca
   tip anlamı düşüyor, `Visibility.Visible` bu kez instance property üzerinden
   enum erişimi sayılıp CS0176 veriyordu. Hotfix 1'de `Color` için eklenen alias
   deseni `Visibility` için de eklendi. Aynı `Color` alias'ı v13.5 dosyası
   `ManifestResultWindow.xaml.cs`'e de gerekiyordu (CS0104 ×2).
4. **`SelectionPickerService.cs` — `ElementId.IntegerValue`** (CS1061, v13.5
   dosyası): Revit 2025+'ta kaldırıldı → `Rv.GetCategoryId(elem)`.

Repo geneli tarama: 140 .cs dosyası tree-sitter ile parse edildi (0 sözdizimi
hatası; EgAddinScanner'daki tuple-deconstruction işareti parser sınırı, Core
zaten derleniyor), `IntegerValue` yalnızca RevitVersionAdapter'ın `#if REVIT2024`
dallarında kaldı, eski `Addin.Family`/`UI.Family` referansı kalmadı.
