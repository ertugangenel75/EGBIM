# EGBIMOTO v10 — Değişiklik Notları

## Aşama 1 — Kritik Düzeltmeler

### SystemOps.cs derleme hatası giderildi
`eg_health_snapshot` ve `GetRevitVersion` op'ları `ctx.Doc` / `ctx.UiApp`'e doğrudan
`OpContext` üzerinden erişiyordu. Bu property'ler `RevitOpContext`'te tanımlı olduğu
için proje derlenmiyordu. Diğer 40+ op dosyasıyla aynı `ctx as RevitOpContext` pattern'i
uygulandı.

### op_contracts.json kod ile senkronlandı
- **35 op eklendi** — kodda `[EgOp]` ile kayıtlı ama contracts'ta eksik olanlar:
  FacadeOps (8), StructuralCheckOps (6), RoomFinishOps (6), FamilyCreateOps (5),
  OpeningCoordOps (2), WallTypeOps (2), KalipOps (2), SlopeAnalysisOps (2),
  RebarOps (1), ModelingOps (1).
- **12 ölü kayıt silindi** — contracts'ta yazılı ama kodda olmayan op'lar:
  kalip_beam, kalip_foundation, curve_offset, point_by_coordinates, line_by_points,
  bbox_from_elements, face_sample_uv_grid, points_grid, curve_divide_by_count,
  curve_divide_by_spacing, curve_project_to_level, curve_clean_short_segments.

Sonuç: **kod ve contracts %100 senkron** (373 op = 373 op). ManifestValidator artık
yanlış "OP_UNKNOWN" hatası üretmiyor; AI üretici tüm gerçek op'ları önerebiliyor.

## Aşama 2 — Metadata + Profesyonel Manifestler

### Metadata toplu dolduruldu
- 100 manifest'e `description` eklendi
- 213 manifest'e kategori bazlı `tags` eklendi
- ManifestBrowser arama/filtreleme artık tam çalışıyor

### 4 yeni profesyonel manifest (mevcut op'larla)
- `qa/eg_model_audit.json` — Model uyarıları + çift eleman denetimi
- `qa/eg_standart_denetimi.json` — Workset + ÇŞB 2026 isimlendirme uyumu
- `ifc/eg_ifc_teslim_paketi.json` — IDS doğrula → IFC4 aktar → Excel BOQ tek akış
- `mimari/eg_pafta_uretim.json` — Görünüm şablonu + otomatik pafta yerleşimi

## Aşama 3 — Yeni Op + Gelişmiş İş Akışı

### ClashOps.cs (yeni)
Disiplinler arası çakışma tespiti — Navisworks Clash Detective'in temel mantığını
Revit içinde sunar. OpeningCoordOps'taki BBox + ElementIntersectsElementFilter
altyapısı temel alındı.
- `clash_detect_matrix` — İki kategori grubu (A vs B) arasında hard clash, kesişim
  hacmi ve disiplin çiftine göre gruplandırma
- `clash_severity_sort` — Bulguları kesişim hacmine göre önceliklendirme (KRİTİK/YÜKSEK/ORTA/DÜŞÜK)

### Yeni manifest
- `koordinasyon/eg_clash_matrisi.json` — MEP × Yapısal hard clash raporu (HTML + Excel)

## Özet
- Op sayısı: 348 → **373** (kod ile senkron)
- Manifest sayısı: 217 → **222**
- Tüm manifest doğrulaması: bozuk JSON 0, tanımsız op 0, kırık referans 0
- Versiyon: 8.0.0 → **10.0.0**

---

# v10.1 — Sağlamlaştırma + Dokümantasyon

## Öncelik 1 — Sağlamlaştırma

### Kırık testler düzeltildi
`ManifestValidatorTests.cs` Aşama 1'deki `ValidationError` API değişikliğiyle
derlenmiyordu (`e.Contains()` çağrısı string varsayıyordu). 6 assert
`e.ToString().Contains()` olarak güncellendi — `ValidationError.ToString()`
artık `[$.path] Code: Message` döndürdüğü için tüm doğrulamalar geçiyor.

### Yeni testler eklendi (T15–T17)
- **T15** — `DagRunResult` telemetry alanları (`TotalSteps`, `TotalDuration`, `CachedSteps`, `SkippedSteps`) doğru doldurulur
- **T16** — Atlanan koşullu adım `SkippedSteps` sayacına yansır
- **T17** — *(regresyon koruması)* Condition'daki `$ref` bağımlılık yaratır; v10 bug fix'ini kilitler

### Tags altyapısı tamamlandı (yarım iş kapatıldı)
Aşama 2'de 213 manifest'e `tags` eklenmişti ama bağlı değildi:
- `EgManifest`'e `Tags` property eklendi → JSON'daki etiketler artık okunuyor
- `ManifestBrowserWindow` araması etiketlerde de filtreliyor

### op_contracts param formatı normalize edildi
2 op'ta (`kalip_all`, `read_param_with_fallback`) parametreler string array
formatındaydı; object array formatına çevrildi. Artık tüm 373 op tutarlı.

## Öncelik 2 — Dokümantasyon

`docs/` klasörü oluşturuldu:
- **OP_REFERANSI.md** — 373 op'un kategoriye göre tam referansı (op_contracts.json'dan otomatik üretildi, 2113 satır, 16 kategori)
- **MANIFEST_YAZIM_REHBERI.md** — Manifest JSON yapısı, DAG mantığı, `from`/`from_many`/`$ref`/`condition`, transaction politikaları, tam örnek
- **HIZLI_BASLANGIC.md** — Kurulum, ilk manifest çalıştırma, Preview/Atomic modlar, AI üretici
- **MIMARI.md** — Katman yapısı, çalışma akışı, tasarım ilkeleri
- **README.md** — Doküman indeksi

## Özet (v10.1)
- Test: kırık → **çalışır** (+3 yeni test, regresyon koruması)
- Tags: eklendi ama bağlı değildi → **tam çalışıyor**
- Dokümantasyon: yok → **5 kılavuz, 2400+ satır**

---

# v10.2 — UI/UX İyileştirme (Manifest Browser)

ManifestBrowserWindow dört yönde geliştirildi. Sıra: C → A → B → D.

## C — Canlı Progress
`DagExecutor.OnStepCompleted` callback'i artık UI'a bağlı (önceden sadece Debug'a
yazıyordu). `RunManifest`'e opsiyonel `onStep` parametresi eklendi; UI bunu
geçirir.
- Her adım bittiğinde adım satırı anlık güncellenir: `○` → `✓`/`✗`, satır rengi
  yeşil/kırmızı, sağda süre (`42ms`).
- Üstte bir progress bar adım sayısına göre dolar (`3/7`).

## A — Kategori Filtre Çubuğu
Arama kutusunun altına tıklanabilir kategori çipleri eklendi.
- "Tümü (222)" + manifest sayısına göre azalan kategoriler (`metraj (19)`,
  `koordinasyon (15)`...).
- Tek seçim; arama ile birlikte çalışır (kategori + metin AND'lenir).

## B — Tag Çipleri
Detay panelinde seçili manifestin etiketleri mor çip olarak gösterilir.
- Çipe tıklayınca o etiketle arama yapılır — keşif kolaylaşır.
- Etiketsiz manifestlerde bölüm gizlenir.

## D — Sonuç Paneli
Çalıştırma bitince alt çubukta:
- **Telemetri rozeti** — `7 adım · 1240ms · ⚡2 önbellek · ⏭1 atlandı`
  (`ManifestRunResult`'a `TotalSteps`/`CachedSteps`/`SkippedSteps`/`DurationMs`
  eklendi, `DagExecutor` telemetrisi taşınıyor).
- **📄 Raporu Aç butonu** — export adımı bir `.html`/`.xlsx`/`.pdf`/`.csv`
  ürettiyse otomatik belirir, tıklayınca dosyayı açar.
- Durum metni başarı/iptal/hata'ya göre renklenir (yeşil/turuncu/kırmızı).

## Özet (v10.2)
- Browser: düz liste → **kategori navigasyonu + tag çipleri**
- Çalıştırma: tek seferlik log → **canlı adım adım progress**
- Sonuç: sadece log → **telemetri rozeti + rapor aç butonu**
- Pencere: 940×660 → 980×720

---

# v10.3 — Dağıtım: Bootstrap Engine Ayrımı + MSI Installer (Hibrit)

Çalışan tek-DLL yapısı bozulmadan, esnek güncelleme için Bootstrap + ayrık engine
mimarisi ve MSI installer eklendi.

## EGBIMOTO.Bootstrap projesi (yeni)
Küçük, sabit bir thunk DLL. `.addin` bunu yükler; engine'i
`%AppData%\EGBIMOTO\R<ver>\app\` altından `AssemblyDependencyResolver` ile yükleyip
`IExternalApplication` çağrılarını ona yönlendirir.
- `EgBootstrapApplication` — `IExternalApplication` thunk, engine yükleyici
- `BootLog` — engine yüklenmeden çalışan bağımsız erken-aşama logger
  (`%AppData%\EGBIMOTO\logs\bootstrap_*.log`)

**Hibrit fallback:** Engine `app\` altında yoksa Bootstrap kendi yanındaki
`EGBIMOTO.Addin.dll`'i arar. Yani tek-DLL kopyalama yöntemi de çalışmaya devam eder.

## İki .addin varyantı
- `EGBIMOTO.addin` — basit, doğrudan `EGBIMOTO.Addin.App` (tek-DLL)
- `EGBIMOTO.Bootstrap.addin` — gelişmiş, Bootstrap üzerinden (engine ayrımı)

## WiX v5 MSI installer (per-user)
- `EGBIMOTO.Installer.wixproj` + `EGBIMOTO.Installer.wxs` — yönetici hakkı
  gerektirmeyen per-user kurulum
- Bootstrap → Revit add-in dizinine, engine + manifests/data → `%AppData%\EGBIMOTO`
- `MajorUpgrade` ile temiz güncelleme; uninstall'da klasör temizliği
- Çoklu Revit sürümü: R24/R25/R26 yan yana, çakışmadan

## Build otomasyonu
- `deploy/stage.sh` — Bootstrap + Engine'i Release derler, çıktıyı `stage/R<short>/`
  altına `addin/` ve `app/` olarak dizer
- `deploy/generate_components.py` — manifests/ ve data/ için WiX component listesi
  üretir (HeatWave harvest yerine saf Python; CI'da güvenilir). 222 manifest test
  edildi, geçerli XML üretiyor.

## Temizlik
- `manifests/` içindeki hatalı boş klasör (`{metraj,maliyet,...}` — eski brace
  expansion artifact'i) silindi. 222 manifest korundu.

## Özet (v10.3)
- Kurulum: sadece manuel kopyalama → **manuel + MSI installer**
- Mimari: tek-DLL → **tek-DLL VEYA Bootstrap+engine (hibrit, ikisi de çalışır)**
- Güncelleme: tüm DLL değiştir → **sadece app\ klasörü (MSI ile)**
- Yeni: 1 proje, 2 .addin, WiX installer, 2 build script, 1 kurulum kılavuzu

---

# v10.4 — Apache 2.0 Lisansı

Proje Apache License 2.0 altında açık kaynak lisanslandı.

## Eklenenler
- `LICENSE` — Apache License 2.0 tam metni (201 satır), telif: Ertuğan Genel
- `NOTICE` — telif bildirimi + üçüncü taraf mimari atıfları (rst-c, pyRevit
  yalnızca tasarım etkisi olarak; kod kopyalanmadı) + runtime bağımlılık lisansları
- `README.md` — proje ana sayfası (özellikler, kurulum, lisans özeti, yazar)
- 10 ana kaynak dosyaya Apache 2.0 dosya başlığı (giriş noktaları + yeni op'lar)
- 3 csproj'a paket metadata: `Authors`, `Company`, `Copyright`,
  `PackageLicenseExpression=Apache-2.0`

## Yazar
Ertuğan Genel — EGBIM

## Özet (v10.4)
- Lisans: yok → **Apache License 2.0**
- Telif: **Copyright 2026 Ertuğan Genel**

## v10 — AnnotationOps eklendi (align-tag uyarlaması)

Kaynak: github.com/simonmoreau/align-tag (MIT, Simon Moreau)

Yeni op dosyası: `src/EGBIMOTO.Addin/Ops/AnnotationOps.cs`
- `align_tags` — tag/yazı elemanlarını hizala veya eşit dağıt
  (mode: left|right|top|bottom|center|middle|distribute_h|distribute_v|untangle_h|untangle_v)
- `arrange_tags` — leader'lı tag'leri view kenarlarına otomatik diz, leader çaprazlarını çöz

Uyarlama notları:
- Revit 2024+ tek API yolu (GetTaggedReferences / SetLeaderEnd / SetLeaderElbow / GetLeaderEnd).
  Orijinaldeki #if Version2019..2021 dalları kaldırıldı.
- RevitWriteScope ile atomic-mode-aware Transaction/SubTransaction.
- Hedef tipler: IndependentTag, TextNote, SpatialElementTag (RoomTag/SpaceTag/AreaTag).

op_contracts.json: 384 → 386 op.
Yeni manifestler: manifests/mimari/a11_etiket_hizalama.json, a12_kapi_etiket_duzenle.json

## v10 — AnnotationOps DÜZELTME (denetim sonrası)

Denetimde tespit edilip düzeltildi:
- KRİTİK: İlk sürüm tag yüksekliğini ölçmek için leader'ları kaldırıp geri
  koymuyordu (RevitWriteScope'ta rollback yok → leader'lar kalıcı siliniyordu).
  Yeniden tasarlandı: leader'lara HİÇ dokunulmaz, hizalama TagHeadPosition /
  TextNote.Coord üzerinden yapılır (etiket başları hizalanır). Leader uçları
  host'a bağlı kalır.
- Hizalama artık view RightDirection/UpDirection eksenine 1D izdüşüm ile yapılır
  (CropBox.Transform.Inverse karmaşası kaldırıldı). align_tags için CropBox gerekmez.
- untangle_h / untangle_v modları kaldırıldı (leader-kaldırma gerektiriyordu).
  Kalan modlar: left|right|top|bottom|center|middle|distribute_h|distribute_v.
- a12 manifesti: işlevsiz collect_doors adımı kaldırıldı (align_tags zaten
  görünümdeki tüm etiketleri tarar).

Revit API çağrıları resmi Autodesk 2024+ dokümanına karşı doğrulandı
(GetTaggedReferences, GetLeaderEnd, SetLeaderElbow, TagHeadPosition, TextNote.Coord).

## v10 — MepHvacOps eklendi (AdnRme HVAC uyarlamasi)

Kaynak: github.com/jeremytammik/AdnRme (MIT, Jeremy Tammik / Autodesk ADN)

Yeni op dosyasi: src/EGBIMOTO.Addin/Ops/MepHvacOps.cs (3 write op)
- assign_flow_to_terminals — mahal supply air flow'unu terminallere esit bolup yazar
- resize_diffuser_by_flow  — debiye gore diffuzor tipini esik tablosundan secip uygular
- populate_space_param     — mahallere hesaplanmis parametre yazar (CFM/SF vb.)

Uyarlama notlari:
- RevitWriteScope (atomic-mode-aware) ile sarildi, [EgOp] reflection kaydi.
- AdnRme.Util notu korundu: terminal 'Flow' parametresi read-only built-in DEGIL,
  isimle aranan yazilabilir parametredir.
- KRITIK DUZELTME: Space toplama icin OfClass(typeof(Space/SpatialElement)) KULLANILMADI
  (Revit'te native-olmayan sinif exception atar; TBC/Jeremy dogruluyor). Yalniz
  OfCategory(OST_MEPSpaces) + OfType<Space>() kullanildi.
- Birim: supply air ic birimi ft3/s, CFM = x60. ROOM_CALCULATED_SUPPLY_AIRFLOW_PARAM okunur.

op_contracts.json: 386 -> 389 op.
Yeni manifestler: manifests/mekanik/m_hvac_terminal_debi.json, m_diffuzor_boyutlandir.json

## v10 — ElecCircuitDiffOps eklendi (elektrik devre degisiklik takibi)

Cozulen alan-sikintisi (Autodesk Revit Ideas, MEP toplulugu, 10 yildir acik):
"Elektrikciye hangi devrelerin kaldirilmasi/degistirilmesi gerektigini gosterememe."
Revit revizyonlar arasi devre-bazli net delta uretmiyor.

Yeni op dosyasi: src/EGBIMOTO.Addin/Ops/ElecCircuitDiffOps.cs (2 op)
- elec_circuit_snapshot — devrelerin durumunu JSON snapshot'a yazar (onayli tasarim ani)
- elec_circuit_diff     — mevcut modeli baseline ile karsilastirir, saha icin
                          EKLENEN/SILINEN/DEGISEN devre + degisen alan raporu (HTML/JSON)

Tasarim notlari:
- Devreler UniqueId ile eslestirilir (worksharing/senkron sonrasi dayanikli).
- KAPSAM SINIRI: Wire size / panel schedule hesapli alanlari Revit'te READ-ONLY'dir;
  eklenti YAZAMAZ (Autodesk API kisiti, TBC dogruluyor). Bu op'lar yazma degil,
  DEGISIKLIK RAPORLAMA cozer.
- Voltage Volt'a cevrildi (Revit ic birimi standart degil, x 0.3048^2 - TBC/Jeremy).
- Kanitsiz 'Rating' property'si kullanilmadi; sadece dogrulanan alanlar (BaseEquipment,
  CircuitNumber, ApparentLoad, PolesNumber, Voltage, Elements).

op_contracts.json: 389 -> 391 op.
Yeni manifestler: manifests/elektrik/el04_devre_snapshot_al.json,
                  el05_devre_degisiklik_raporu.json

## v10 — ElecConduitIecOps eklendi (TS/IEC 60364 conduit kablo hesabi)

Cozulen: Revit Panel Schedule read-only kisiti. Profesyonel araclarin (eVolve,
ElectroBIM) yaklasimi: conduit uzerine Shared Parameter yazilir, Autodesk'in
kilitli alanlarina dokunulmaz.

Yeni shared param dosyasi: data/mapping/EGBIM_ElektrikParams.txt (24 param, SABIT GUID)
Yeni op dosyasi: src/EGBIMOTO.Addin/Ops/ElecConduitIecOps.cs (3 op)
- elec_setup_conduit_params — elektrik parametrelerini yukler (Conduit+ElectricalCircuit)
- elec_conduit_calc_iec     — IEC 60364 kablo secimi (ampacity+gerilim dusumu+kisa
                              devre BIRLIKTE) + conduit fill, conduit'e yazar
- elec_conduit_schedule     — From-To kablo cetveli (HTML/CSV)

Standart/metodoloji (arastirmadan dogrulandi):
- IEC 60364-5-52: It >= IN/(Ca*Cg), en kucuk kablo UC kontrolu birden gecmeli
- Gerilim dusumu: Vd = b*IB*L*(R*cosphi + X*sinphi), b=sqrt(3)/2; sinir %5 guc/%3 aydinlatma
- Direnc calisma sicakliginda (70C PVC / 90C XLPE), alpha duzeltmesi
- Conduit fill: IEC 522.8 %40 siniri, IEC 61386 ic caplari
- Kisa devre k: Cu PVC 115 / XLPE 143, Al PVC 76 / XLPE 94

HIBRIT TABLO: Gomulu ampacity referans degerleri (resmi IEC tablosu telifli);
kullanici ampacity_table_path ile kendi resmi TS/IEC tablosunu yukleyebilir.
SORUMLULUK: Motor muhendislik kararina yardimci olur, yerini almaz.

op_contracts.json: 391 -> 394 op.
Yeni manifest: manifests/elektrik/el06_conduit_iec_hesap.json
