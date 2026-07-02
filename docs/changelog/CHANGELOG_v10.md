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

## v10 — PlumbingEnOps eklendi (EN 12056/806 sihhi tesisat hesabi)

Cozulen alan-sikintisi (forum, 10 yil): Revit fixture-unit tabanli boru boyutlandirma
yapamiyor; Hunter egrisi dusuk fixture'larda hatali, schedule'lar "aptal" metin.

Yeni shared param dosyasi: data/mapping/EGBIM_SihhiParams.txt (21 param, SABIT GUID, e6b2 prefix)
Yeni op dosyasi: src/EGBIMOTO.Addin/Ops/PlumbingEnOps.cs (4 op)
- plumbing_setup_params  — sihhi parametreleri yukler (PlumbingFixtures+PipeCurves)
- plumbing_assign_units  — armaturlere DU/LU atar (tip tablosundan)
- plumbing_calc_en       — drenaj Qww + su QD hesabi, cap secimi, dolum/egim/hiz kontrolu
- plumbing_schedule      — hesap cetveli (HTML/CSV)

Standart (arastirmadan dogrulandi):
- EN 12056-2 drenaj: Qww = K×√ΣDU; K=0.5 konut/0.7 sik/1.0 yogun/1.2 ozel
- DU tablosu (System I): lavabo 0.5, wc 2.0, dus 0.6, eviye 0.8 l/s
- DN-Qmax: Tablo 4 (bras) + Tablo 12 (kolon)
- EN 806-3 su: 1 LU=0.1 l/s, QD=0.171×ΣLU^0.473 (referans noktalara fit, ±%11)
- LU tablosu: lavabo 1, kuvet 4, eviye 3, wc 2

HIBRIT TABLO: Gomulu DU/LU/DN referans degerleri (resmi EN tablosu telifli);
kullanici kendi resmi TS/EN tablosunu yukleyebilir.
BASITLESTIRME: Topoloji gezme yerine sistem-bazli yuk toplama (boru sistem adina gore).
SORUMLULUK: Motor muhendislik kararina yardimci olur, yerini almaz.

op_contracts.json: 394 -> 398 op.
Yeni manifest: manifests/sihhi_tesisat/st01_en_tesisat_hesap.json

## v10 — ColumnPresizingOps eklendi (TBDY 2018/TS 500 kolon on boyutlandirma)

Ilham: TR pazarinda yapisal hesap araclari (berzansolmaz.app gibi). Formul KOPYALANMADI;
TBDY 2018/TS 500 resmi yonetmelikten (kamu mali) bagimsiz uygulandi.

Yeni shared param dosyasi: data/mapping/EGBIM_KolonParams.txt (19 param, SABIT GUID, e6b3 prefix)
Yeni op dosyasi: src/EGBIMOTO.Addin/Ops/ColumnPresizingOps.cs (2 op)
- column_setup_params         — kolon parametrelerini yukler (StructuralColumns)
- structural_column_presizing — TBDY/TS500 kontrol + min boyut onerisi, kolona yazar

Standart (arastirmadan dogrulandi, resmi kaynaklar):
- TBDY 7.3.1.1: dik min 300mm, dairesel cap min 350mm
- TBDY 7.3.1.2: Ac ≥ Ndm/(0.40·fck), Ndm=G+Q+E en buyuk eksenel
- TS 500: Nd ≤ 0.9·fcd·Ac (fcd=fck/1.5)
- TBDY 7.4.1.2: kiris/kolon ayrimi Nd ≤ 0.10·Ac·fck
- Oneri: max(min_boyut, √gerekenAc), 50mm yukari yuvarlama

Iki calisma modu: KONTROL (mevcut boyut uygun mu) + ONERI (gereken min kesit).
Ndm girdisi: EG_Ndm (analiz) veya EG_YukAlani×EG_KatSayisi×EG_BirimYuk (tahmin).
fck: EG_BetonSinif ('C30'→30) veya EG_fck_Override veya default.
Simulasyon dogrulandi: 400x400 C30/2000kN→TBDY sinir asiliyor, oneri 450mm.

SORUMLULUK: ON boyutlandirma/kontrol; kesin tasarim (2. mertebe, egilme etkilesimi)
sorumlu yapi muhendisine aittir.

op_contracts.json: 398 -> 400 op.
Yeni manifest: manifests/yapisal/yp01_kolon_on_boyutlandirma.json

## v10 — EGBIMOTO MCP Server + Manifest Üretici Ajan eklendi

AEC 2026 trendi "agentic BIM": Revit yayın katmanı oluyor, zeka etrafına göç ediyor.
EGBIMOTO'nun manifest-DSL + op_contracts + reflection mimarisi buna zaten hazırdı;
eksik halka olan MCP köprüsü eklendi → EGBIMOTO artık AI-erişilebilir BIM platformu.

MİMARİ: [Claude Desktop] --MCP/stdio--> [Python köprü] --HTTP/localhost--> [EGBIMOTO
Server (Revit)] --ExternalEvent--> [DagExecutor + 400 op].

Karar: Manifest üretimini Claude Desktop'taki Claude yapar (op katalogunu görüp);
ek API anahtarı/LLM çağrısı yok. Gömülü ManifestGenerator paralel kalır.

Yeni C# dosyaları (src/EGBIMOTO.Addin/Server/):
- RevitDispatcher.cs       — HTTP thread → Revit ana thread marshalling (IExternalEventHandler)
- EgbimotoMcpServer.cs     — HttpListener, localhost:5577, endpoint router
- McpManifestRunner.cs     — manifest JSON → DagExecutor köprüsü (atomik tx korunur)
- McpServerManager.cs      — yaşam döngüsü (singleton, başlat/durdur)
Commands/McpServerToggleCommand.cs — ribbon düğmesi (toggle)
App.cs — Otomasyon paneline "MCP Server" düğmesi eklendi

HTTP endpoint'leri: GET /health, GET /ops, POST /run, POST /validate
MCP araçları: egbimoto_list_ops, egbimoto_run_manifest, egbimoto_health
Güvenlik: yalnız 127.0.0.1 bind, opsiyonel X-EGBIMOTO-Token

Python köprüsü (mcp_bridge/):
- egbimoto_mcp_bridge.py            — MCP stdio server, HTTP proxy
- claude_desktop_config.example.json — Claude Desktop config örneği
- requirements.txt                  — mcp + httpx
- README.md                         — kurulum + kullanım kılavuzu

KRITIK TASARIM: Revit tek-thread. HttpListener ayrı thread → ExternalEvent ile ana
thread'e marshal (RevitDispatcher). HTTP thread sonucu ManualResetEventSlim ile bekler
(timeout 120s). Headless: onay kapıları otomatik onaylanır (dış ajan akışı).

op sayısı değişmedi (400). Bu bir altyapı/platform katmanı, op değil.

## v10.1 — Sıhhi Tesisat Hesap Motoru 2 (PlumbingCalcOps)

### Yeni Dosya
- `src/EGBIMOTO.Addin/Ops/PlumbingCalcOps.cs`

### Yeni Op'lar (10 adet — op_contracts.json: 400 → 410)
1. `plumbing_demand_lpd`        — Bina tipi × kişi → günlük LPD (EN 806-1, ÇŞB 2026)
2. `plumbing_storage_tank_size` — Suction + OHT depo boyutlandırma
3. `plumbing_pump_hp_calc`      — Pompa Ph/Pb/Pm + IEC motor seçimi
4. `plumbing_peak_demand`       — Qp = birim_pik × N + Hazen-Williams riser DN
5. `plumbing_pressure_zone`     — Yüksek bina basınç zonlama + PRV konumları
6. `plumbing_water_velocity`    — Boru hız kontrolü (EN 806-3 limitleri)
7. `plumbing_static_pressure`   — Statik basınç P=ρgh (sıcaklık bağımlı yoğunluk)
8. `plumbing_fixture_clearance` — Armatür montaj yükseklikleri QA (FFL'den)
9. `plumbing_dead_leg_check`    — Ölü hat kontrolü (ASHRAE 188 / WHO lejyonella)
10. `plumbing_hwc_return`       — HWC sirkülasyon debi + boru boyutu (BS 8558)

### Yeni Manifestler (6 adet)
- `pl11_gunluk_su_talebi_depo.json`  — Talep + depo pipeline
- `pl12_pompa_hp_pik_talep.json`     — Pompa güç + pik talep
- `pl13_yuksek_bina_basinc_zonu.json`— Statik basınç + zon hesabı
- `pl14_boru_hiz_kontrol.json`       — Tüm borular hız QA
- `pl15_olu_hat_ve_hwc.json`         — Lejyonella + HWC
- `pl16_tam_sihhi_sistem_hesap.json` — 10 op tam pipeline

### Standartlar
EN 806-1/2/3 | ASHRAE 188 | WHO Legionella | BS 8558 | TR ÇŞB 2026

## v10.2 — Elektrik Hesap Motoru (ElecCalcOps)

### Yeni Dosya
- `src/EGBIMOTO.Addin/Ops/ElecCalcOps.cs`

### Yeni Op'lar (12 adet — op_contracts.json: 410 → 422)
1.  `elec_voltage_drop_calc`     — Gerilim düşümü + otomatik kesit (IEC 60364-5-52)
2.  `elec_short_circuit_check`   — Kısa devre koordinasyon + ısıl dayanım (IEC 60364-4-43)
3.  `elec_diversity_factor`      — Çeşitlilik faktörü ile talep gücü (IEC 60364 / CIBSE)
4.  `elec_earthing_validation`   — Topraklama sistemi QA TN-S/TT/IT (IEC 60364-4-41)
5.  `elec_busbar_sizing`         — Busbar kesit seçimi (akım yoğunluğu, standart boyutlar)
6.  `elec_power_factor_check`    — Güç faktörü & reaktif güç kompanzasyon önerisi
7.  `elec_elv_device_qa`         — ELV cihaz montaj yükseklikleri QA
8.  `elec_tray_hanger_spacing`   — Kablo tava askı aralığı kontrolü (IEC 61537)
9.  `elec_tray_separation_check` — Güç/ELV tava ayrımı ≥300mm kontrolü
10. `elec_generator_load_calc`   — Jeneratör yük + kVA boyutlandırması
11. `elec_ups_autonomy_check`    — UPS özerklik + batarya kapasitesi (IEC 62040-3)
12. `elec_emergency_circuit_qa`  — Acil aydınlatma devre QA (TS EN 1838)

### Yeni Manifestler (6 adet)
- `el11_gerilim_dusumu_hesap.json`  — Gerilim düşümü + kısa devre pipeline
- `el12_panel_guc_analiz.json`      — Çeşitlilik + güç faktörü + busbar zinciri
- `el13_tesisat_guvenligi_qa.json`  — Topraklama + acil aydınlatma QA
- `el14_kablo_tava_qa.json`         — Tava askı + ELV ayrımı + ELV cihaz QA
- `el15_yedek_guc_sistemi.json`     — Jeneratör + UPS pipeline
- `el16_tam_elektrik_hesap.json`    — 12 op tam pipeline

### Standartlar
IEC 60364-5-52 | IEC 60364-4-43 | IEC 60364-4-41 | IEC 61537
IEC 62040-3 | TS EN 1838 | TS HD 60364 | CIBSE Guide

### Toplam EGBIMOTO v10.2
- Op: 422 (400 mevcut + 10 sıhhi + 12 elektrik)
- Manifest: pl11-pl16 (sıhhi) + el11-el16 (elektrik) = 12 yeni

## v10.3 — Yangın Koruma Hesap Motoru (FireProtectionOps)

### Yeni Dosya
- `src/EGBIMOTO.Addin/Ops/FireProtectionOps.cs`

### Yeni Op'lar (13 adet — op_contracts.json: 422 → 435)
1.  `fp_standpipe_qa`            — Standpipe riser DN + Wet/Dry tip (NFPA 14)
2.  `fp_standpipe_pressure`      — Statik basınç + PRV kat konumları
3.  `fp_fdc_clearance_check`     — FDC temizlik mesafesi (min 450mm AFF, 3000mm ön)
4.  `fp_pump_schedule_validate`  — Main/Jockey/Diesel pompa schedule (NFPA 20)
5.  `fp_pump_hp_calc`            — Yangın pompası HP hesabı (SF=1.25)
6.  `fp_sprinkler_hydraulic`     — K-faktörü hidrolik Q=K×√P (NFPA 13 / TS EN 12845)
7.  `fp_sprinkler_temp_class`    — Sıcaklık sınıfı doğrulama 57C→260C
8.  `fp_detection_coverage`      — Dedektör kapsama: duman 60m², ısı 30m² (NFPA 72)
9.  `fp_suppression_agent_qa`    — Söndürme ajanı × alan uygunluk (9 alan tipi)
10. `fp_evacuation_route_check`  — Tahliye yolu genişlik + max mesafe (TR Yangın Yön.)
11. `fp_exit_sign_spacing`       — Çıkış işareti aralığı (TS EN 1838, h×200)
12. `fp_fire_door_rating_check`  — EI rating + boşluk kontrolü (TS EN 1634)
13. `fp_compartment_area_check`  — Kompartıman alanı (TR Yangın Yön. Tablo-1)

### Yeni Manifestler (6 adet)
- `fp12_standpipe_sistem.json`       — Standpipe DN + basınç + FDC
- `fp13_yangin_pompasi.json`         — Pompa schedule + HP
- `fp14_sprinkler_hidrolik.json`     — Sprinkler K + sıcaklık sınıfı
- `fp15_algilama_sondurme_qa.json`   — Dedektör kapsama + ajan QA
- `fp16_tahliye_mimari_qa.json`      — Tahliye + çıkış + kapı + kompartıman
- `fp17_tam_yangin_koruma_hesap.json`— 13 op tam pipeline

### Standartlar
NFPA 13 | NFPA 14 | NFPA 20 | NFPA 72 | NFPA 101
TS EN 12845 | TS EN 1634 | TS EN 1838
TR Yangın Yönetmeliği 2015 (Tablo-1, §49-57, §72, §93)

### Toplam EGBIMOTO v10.3
- Op: 435 (400 + 10 sıhhi + 12 elektrik + 13 yangın)
- Yeni C# dosyaları: PlumbingCalcOps.cs, ElecCalcOps.cs, FireProtectionOps.cs
- Yeni manifestler: pl11-16 + el11-16 + fp12-17 = 18 yeni

## v10.4 — MEP Mekanik Hesap Motoru (MepHvacCalcOps)

### Yeni Dosya
- `src/EGBIMOTO.Addin/Ops/MepHvacCalcOps.cs`

### Yeni Op'lar (10 adet — op_contracts.json: 435 → 445)
1.  `mep_hvac_heat_load_calc`   — Isı yükü: kabuk+iç yük+infiltrasyon+taze hava (TS 825/ASHRAE)
2.  `mep_ahu_selection`         — AHU fan gücü, filtre sınıfı, boyut tahmini
3.  `mep_cooling_load_room`     — Oda soğutma: güneş+duvar+iç yük (ASHRAE CLTD)
4.  `mep_static_pressure_calc`  — Kanal ESP: hat+bağlantı+filtre+serpantin+terminal
5.  `mep_fresh_air_rate_check`  — Taze hava Vbz=Rp×Pz+Ra×Az (ASHRAE 62.1, 8 alan tipi)
6.  `mep_pressurization_check`  — Basınçlandırma: ameliyathane+15Pa, mutfak-15Pa (ASHRAE 170)
7.  `mep_hvac_zone_balance`     — Zon debi denge ±10% (ASHRAE 111)
8.  `mep_hepa_filter_qa`        — HEPA H13/H14, ΔP, gel-seal, DOP test tarihi (EN 1822-1)
9.  `mep_ach_by_room_type`      — ACH referans tablosu: 19 oda tipi (ASHRAE 62.1/170/EN 16798)
10. `mep_chiller_cop_check`     — COP kontrolü + yıllık maliyet/tasarruf (ASHRAE 90.1-2022)

### Yeni Manifestler (6 adet)
- `me12_isi_yuku_ahu.json`           — Isı yükü + AHU seçimi
- `me13_oda_sogutma_ve_taze_hava.json`— Oda soğutma + taze hava
- `me14_basinc_hepa_qa.json`         — Basınçlandırma + HEPA QA
- `me15_ach_zon_denge.json`          — ACH referans + ESP + zon denge
- `me16_chiller_cop.json`            — Chiller COP + enerji analizi
- `me17_tam_mek_hesap.json`          — 10 op tam pipeline

### Standartlar
TS 825 | ASHRAE 62.1 | ASHRAE 90.1-2022 | ASHRAE 111 | ASHRAE 170
EN 16798-1 | EN 1822-1 | ISO 14644 | TR Sağlık Bakanlığı

### EGBIMOTO v10.4 Toplam
- Op: 445 (400 + 10 sıhhi + 12 elektrik + 13 yangın + 10 mekanik)
- Yeni C# dosyaları: PlumbingCalcOps / ElecCalcOps / FireProtectionOps / MepHvacCalcOps
- Yeni manifestler: pl11-16 + el11-16 + fp12-17 + me12-17 = 24 yeni manifest

## v10.5 — Yapısal Hesap Motoru (StructuralCalcOps)

### Yeni Dosya
- `src/EGBIMOTO.Addin/Ops/StructuralCalcOps.cs`

### Yeni Op'lar (10 adet — op_contracts.json: 445 → 455)
1.  `struct_rebar_lap_kolon`       — Kolon bindirme: lb0=(φ/4)×(fyd/fbd), deprem ×1.25 (TBDY §7.3)
2.  `struct_rebar_lap_perde`       — Perde filiz: yatay max(30φ,300mm), uç bölge max(0.2h,1.5bw,500mm)
3.  `struct_rebar_anchorage`       — Ankraj: düz×1.0, kancalı×0.7, çengelli×0.5, min max(10φ,100mm)
4.  `struct_concrete_class_qa`     — Beton QA: fck/fcd/fctd/fbd, deprem min C25, prefabrik min C30
5.  `struct_beam_depth_ratio`      — Kiriş h/L: basit L/12, sürekli L/15, konsol L/6 (TS 500 §9.1)
6.  `struct_wall_slenderness`      — Perde narinlik h/t ≤ 25, min 200mm (TBDY §7.6.2)
7.  `struct_slab_thickness`        — Döşeme: tek yönlü L/35, çift L/45, konsol L/12 (TS 500 §12.2)
8.  `struct_foundation_bearing`    — Temel: q=N/(B×L), e=M/N ≤ B/6, q_max ≤ q_emn (TS 500 §15)
9.  `struct_steel_bolt_type_check` — Bulon: N/X/T/SC tanımları, kullanım yeri önerisi (AISC/ASTM/RCSC)
10. `struct_formwork_type_select`  — Kalıp: 9 tip karşılaştırma, bina tipi+kat→öneri (ÇŞB 2026)

### Yeni Manifestler (5 adet)
- `s11_donati_bindirme_ankraj.json` — Kolon+perde filiz+ankraj pipeline
- `s12_beton_sinifi_kesit_qa.json`  — Beton QA+kiriş h/L+perde narinlik
- `s13_doseme_temel_hesap.json`     — Döşeme kalınlık+temel taban basıncı
- `s14_celik_bulon_kalip.json`      — Bulon QA+kalıp sistemi seçimi
- `s15_tam_yapisal_hesap.json`      — 10 op tam pipeline

### Standartlar
TS 500:2000 | TBDY 2018 | TS EN 1992-1-1
AISC 360 | ASTM F3125 | RCSC Spec. | ÇŞB 2026

### EGBIMOTO v10.5 Toplam
- Op: 455 (400 + 10 sıhhi + 12 elektrik + 13 yangın + 10 mekanik + 10 yapısal)
- Yeni C# dosyaları: 5 adet (PlumbingCalcOps / ElecCalcOps / FireProtectionOps / MepHvacCalcOps / StructuralCalcOps)
- Yeni manifestler: pl11-16 + el11-16 + fp12-17 + me12-17 + s11-15 = 29 yeni manifest

## v10.6 — MEP Boşluk Yönetim Motoru (MepOpeningOps)

### Kaynak
- script.py (Kanal Boşluğu Açma v8.4.1) → C# op'a dönüştürüldü
- script_py.py (MEP Boşluk Yönetim Sistemi v2.4) → C# op'a dönüştürüldü

### Yeni Dosya
- `src/EGBIMOTO.Addin/Ops/MepOpeningOps.cs`

### Yeni Op'lar (4 adet — op_contracts.json: 455 → 459)
1.  `mep_opening_detect`      — Duct/Pipe/Tray-Duvar kesişim tespiti + Kanal_Boslugu yerleştirme
2.  `mep_opening_validate`    — Geçersiz boşluk + EC-2 boyut sınıflandırması + KB_Durum güncelleme
3.  `mep_opening_bcf_export`  — BCF 2.1 ihraç: markup+viewpoint+snapshot → .bcfzip
4.  `mep_lintel_place`        — Otomatik lento: gazbeton/tuğla evet, betonarme hayır, EC-2 demir çapı

### EC-2 Kuralı (script_py.py'den birebir dönüştürüldü)
Yuvarlak/Kare (oran>0.85): d<350=OK, 350-600=TAKVİYE(2Ø16), >600=REVİZYON
Dikdörtgen: d<200=OK, 200-400=TAKVİYE(2Ø16), 400-600=TAKVİYE+(2Ø12), >600=REVİZYON

### Bug Fix
- Gazbeton duvar tipi: "beton" keyword false positive önlendi
  (Önce lento_kw kontrol → gazbeton doğru algılanıyor)

### Yeni Manifestler (5 adet — yeni dizin: mep_koordinasyon/)
- `mep01_bosluk_tespit.json`        — Tespit + yerleştirme
- `mep02_bosluk_validate_ec2.json`  — EC-2 doğrulama
- `mep03_bcf_export.json`           — BCF 2.1 ihraç
- `mep04_lento_yer.json`            — Lento yerleştirme
- `mep05_tam_bosluk_workflow.json`  — 4 op tam pipeline

### Standartlar
EC-2 (EN 1992-1-1) | BCF 2.1 (buildingSMART) | NFPA / TR Yangın Yön.

### EGBIMOTO v10.6 Toplam
- Op: 459
- Yeni C# dosyaları: 6 (v10.1→v10.6)
- Toplam yeni manifest: 34 (5 dizinde)
- Yeni dizin: mep_koordinasyon/

## v10 — MCP Bridge v2 (Semantic Cache + Query Decomposer + Golden Dataset)

### Yeni Dosya
- `mcp_bridge/egbimoto_mcp_bridge_v2.py` (598 satır)
  - v1 (egbimoto_mcp_bridge.py) korundu — geriye dönük uyumluluk

### Katman 1 — SemanticCache
- SHA-256 hash bazlı bellek + disk önbellek
- transaction_policy='none' manifestler önbelleklenir (write op'lar atlanır)
- TTL: 3600s (env EGBIMOTO_CACHE_TTL ile ayarlanabilir)
- Max: 500 item (EGBIMOTO_CACHE_MAX)
- İnvalidate: manifest_id bazlı veya tümü
- İstatistik: hit/miss/skip/evict/disk_items

### Katman 2 — QueryDecomposer
- 30+ kural tabanlı Türkçe/İngilizce shortcut
  Örnekler:
    "sıhhi tesisat hesapla" → pl16_tam_sihhi_sistem_hesap
    "voltage drop 100A 50m" → el11_gerilim_dusumu_hesap
    "yangın pompası HP"     → fp13_yangin_pompasi
    "MEP boşluk tespiti"    → mep01_bosluk_tespit
- Input extraction: m², kat, kişi, kW, beton sınıfı → $INPUT token'larına yazar
- Alan tipi tespiti: otel/ofis/konut/hastane → alan_tipi parametresi
- Öneri: eşleşme yoksa yakın manifest ID listesi

### Katman 3 — GoldenDataset
- 10 altın test (6 smoke, 4 tam)
- Test tipleri: EXACT | RANGE | CONTAINS | MOCK
- Smoke testler (6): PLM-001, ELC-001, FP-001, FP-002, MEK-001, STR-001
- Disk'e kaydedilir (~/.egbimoto/tests/golden_dataset.json)

### Yeni MCP Araçlar (6 araç → v1'den +3)
Korunan: egbimoto_health, egbimoto_list_ops, egbimoto_run_manifest (+ cache)
Yeni:
  egbimoto_smart_run    — NL sorgu → manifest → Revit (1 mesajda)
  egbimoto_cache_stats  — stats | invalidate | invalidate_all
  egbimoto_run_tests    — smoke | all | category modları

### Bug Fix (v2 içinde)
- 'tüm yangın' Türkçe ı karakteri regex fix
- 'katlı' kat_sayisi extraction  boundary fix

## MCP Bridge v2.1 — Review İyileştirmeleri

### Kaynak
Review yorumları (4 geçerli öneri) uygulandı, 3 abartılı öneri reddedildi.

### Uygulanan İyileştirmeler

**① Regex Compile (performans)**
  SHORTCUTS listesi, QueryDecomposer.__init__ sırasında bir kez compile edilir.
  Her sorgu çağrısında re.compile yapılmaz → ~30x hız artışı (30 pattern × N sorgu).
  _compiled: list | None = None  — sınıf düzeyinde, tüm instance'lar paylaşır.

**② Async File I/O (blocking önleme)**
  Cache disk yazımı ThreadPoolExecutor (max_workers=2) ile arka planda yapılır.
  _write_disk_async() → _pool.submit(_write_disk_sync)
  Event loop bloklanmaz, büyük modellerde gecikme yaşanmaz.

**③ safe_path / Path Traversal Koruması (güvenlik)**
  Cache key hex doğrulama: all(c in '0123456789abcdef' for c in key)
  Path traversal kontrolü: cf.resolve().startswith(CACHE_DIR.resolve())
  Geçersiz key (../../../etc/passwd, not_hex) → ValueError

**④ Logging (gözlemlenebilirlik)**
  import logging — os, sys, ... ile aynı satırda
  EGBIMOTO_LOG_LEVEL env var (INFO varsayılan)
  logger.debug: cache HIT (mem/disk), SET
  logger.info: smart_run OK, run_manifest, run_tests özet, invalidate
  logger.warning: disk write fail, HTTP hata
  logger.exception: beklenmeyen hatalar
  _elapsed_s: tüm /run çağrılarında timing

### Reddedilen Öneriler (neden)
  ✗ FAISS embedding → 30 shortcut %95 karşılıyor, aşırı mühendislik
  ✗ Docker Compose → Revit sadece Windows, container anlamsız
  ✗ Prometheus → altyapı yok, tek müşteri EGBIM
  ✗ Webhook cache invalidation → bridge stdio tabanlı, HTTP server değil
  ✗ PyPI paketi → erken, tek kullanıcı aşaması

### Satır: 598 → 756 (+158 satır)

## v10 — Ribbon Manifest Butonu Sistemi

### Yeni Dosyalar
- `src/EGBIMOTO.Addin/Commands/ManifestRibbonCommand.cs`
  Tek IExternalCommand — tüm manifest butonları bunu çağırır.
  JournalData["EGBIMOTO_MANIFEST_ID"] ile manifest ID iletilir.
  $INPUT token'larını varsayılan değerlerle otomatik çözer.

- `src/EGBIMOTO.Addin/UI/RibbonBuilder.cs`
  Hibrit ribbon inşacısı:
    AddManifestPush()  → tek manifest → PushButton
    AddManifestSplit() → N manifest → SplitButton (dropdown)
  ribbon_config.json varsa oradan, yoksa built-in default'lar.

- `ribbon_config.json`
  Kullanıcı özelleştirme dosyası. Düzenle, Revit'i yeniden başlat.

### App.cs Güncellemesi
  App.cs → RibbonBuilder.Build() çağrısına dönüştürüldü (V2.1)
  Tab oluşturma RibbonBuilder içine taşındı.

### Ribbon Yapısı (EGBIMOTO Tab)
  Panel: BIM Veri       → IFC, IDS, Parametre (statik, değişmedi)
  Panel: Hesap          → Poz, Maliyet, Kalıp (statik, değişmedi)
  Panel: Otomasyon      → Manifest Browser, MCP Server (statik, değişmedi)
  Panel: Sık Kullanılan → 6 PushButton (tam hesap manifestleri) [YENİ]
  Panel: MEP Hesap      → 5 SplitButton: Sıhhi/Elektrik/Yangın/Mekanik/Boşluk [YENİ]
  Panel: Yapısal        → 2 SplitButton: Donatı/Beton, Döşeme/Temel [YENİ]
  Panel: QA/Rapor       → 3 SplitButton: Model QA, Teslim, Koordinasyon [YENİ]

### ribbon_config.json (52 manifest)
  Sık Kullanılan: 6 manifest (pl16, fp17, el16, me17, s15, mep05)
  MEP Hesap: 29 manifest (Sıhhi×6, Elektrik×6, Yangın×6, Mekanik×6, Boşluk×5)
  Yapısal: 5 manifest (s11-s15)
  QA/Rapor: 12 manifest (qa, teslim, koordinasyon)

### Teknik Not — JournalData Kısıtı
  Revit API'de PushButton'a doğrudan parametre geçilemez.
  ToolTipDescription = "MANIFEST_ID:<id>" ile ek veri saklanır.
  Gerçek çözüm için her manifest ayrı IExternalCommand wrapper oluşturulabilir
  (ManifestBrowserCommand hali hazırda DAGExecutor'ı doğrudan çağırıyor).
