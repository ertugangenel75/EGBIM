# EGBIMOTO v10 — Manifest Kataloğu
Toplam **70 manifest**, 7 blok. Gerçek dünya AEC ihtiyaçlarına göre tasarlanmıştır.
Her manifest mevcut op'ları Lego gibi birleştirerek tam iş akışları üretir.

## 📋 Blok 1 — Hakediş (10)

| # | Dosya | Başlık | Adım | Açıklama |
|---|-------|--------|------|----------|
| 01 | `01_kalip_hakedis.json` | Kalıp Hakediş | 15 | Yapısal elemanların kalıp metrajını hesaplar, ÇŞB 2026 poz koduyla eşler, kat ba... |
| 02 | `02_yapi_hakedis.json` | Yapısal Hakediş | 16 | Kolon, kiriş, döşeme, temel ve yapısal duvarların metrajını çıkarır, ÇŞB 2026 po... |
| 03 | `03_mep_hakedis.json` | MEP Hakediş | 15 | Kanal, boru, kablo taşıyıcı ve kondüit uzunluk metrajını çıkarır, poz eşler, dis... |
| 04 | `04_donati_hakedis.json` | Donatı Hakediş | 11 | Modeldeki tüm donatıları toplar, çap bazında ağırlık hesaplar, ÇŞB 2026 poz kodu... |
| 05 | `05_beton_hakedis.json` | Beton Metraj Hakediş | 13 | Kolon, kiriş, döşeme ve temel elemanların beton hacmini m³ cinsinden hesaplar, b... |
| 06 | `06_cephe_hakedis.json` | Cephe Hakediş | 11 | Giydirme cephe panellerinin tip ve alanını çıkarır, poz eşler, m² bazlı cephe ha... |
| 07 | `07_kat_bazli_hakedis.json` | Kat Bazlı Toplam Hakediş | 13 | Tüm yapısal ve mimari kategorileri kat bazında gruplar, WBS kodu atar ve kat baz... |
| 08 | `08_insaat_ilerleme_hakedis.json` | İnşaat İlerleme Hakediş | 12 | Faz bazında tamamlanan imalatı tespit eder, WBS kodu atar ve ara hakediş raporu ... |
| 09 | `09_merdiven_merdivenlik_hakedis.json` | Merdiven ve Rampa Hakediş | 11 | Merdiven, rampa ve korkuluk elemanlarının metrajını çıkarır, poz eşler ve hakedi... |
| 10 | `10_tugla_dolgu_metraj.json` | Tuğla ve Dolgu Duvar Metrajı | 11 | Mimari duvarların alanını hesaplar, tuğla miktarını calc_brick_quantity ile çıka... |

## ✅ Blok 2 — Teslim Kalite (10)

| # | Dosya | Başlık | Adım | Açıklama |
|---|-------|--------|------|----------|
| 11 | `11_csb2026_teslim_kontrol.json` | ÇŞB 2026 Teslim Kontrolü | 16 | ÇŞB 2026 zorunlu parametrelerini kontrol eder, IDS doğrulaması yapar ve IFC tesl... |
| 12 | `12_model_sagligi.json` | Model Sağlığı Kontrolü | 12 | Model uyarılarını, duplike elemanları, koordinat doğruluğunu ve boyut sınırların... |
| 13 | `13_ts500_tbdy_kontrol.json` | TS500 / TBDY 2018 Yapısal Kontrol | 13 | Yapısal elemanların TS500 kesit gereksinimlerini, TBDY 2018 deprem parametreleri... |
| 14 | `14_ifc_teslim.json` | IFC Teslim Paketi | 12 | IDS doğrulaması yapar, IFC4 TR BIM formatında dışa aktarır ve BOQ Excel raporu ü... |
| 15 | `15_lod_kontrol.json` | LOD Kontrol | 9 | Modeldeki elemanların LOD seviyelerini doğrular, eksik veya hatalı LOD atamasını... |
| 16 | `16_isimlendirme_standart.json` | İsimlendirme Standardı Kontrolü | 11 | Workset isimlendirme kuralları, ÇŞB 2026 isimlendirme konvansiyonları ve TR BIM ... |
| 17 | `17_faz_tutarlilik.json` | Faz Tutarlılık Kontrolü | 9 | Modeldeki tüm elemanların faz atamalarını kontrol eder, faz tutarsızlıklarını ve... |
| 18 | `18_aile_saglik_kontrol.json` | Aile Sağlık Kontrolü | 8 | Projede kullanılan Revit ailelerinin sağlığını kontrol eder: onaylı liste uyumu,... |
| 19 | `19_workset_dagitim.json` | Workset Dağılım Raporu | 10 | Modeldeki elemanları workset'e göre dağıtır, kat bazında workset atar ve workset... |
| 20 | `20_delta_versiyon_rapor.json` | Delta / Versiyon Karşılaştırma Raporu | 10 | Önceki çalıştırma izlerini okur, mevcut modelle karşılaştırır ve ne değişti rapo... |

## 🔧 Blok 3 — MEP Koordinasyon (10)

| # | Dosya | Başlık | Adım | Açıklama |
|---|-------|--------|------|----------|
| 21 | `21_mep_gecis_deligi.json` | MEP Geçiş Deliği Pipeline | 9 | MEP-yapısal kesişimlerini tespit eder, kullanıcı onayı alır (preview_gate) ve bo... |
| 22 | `22_mep_cakisma_raporu.json` | MEP Çakışma Matrisi Raporu | 12 | MEP ve yapısal elemanlar arasındaki hard clash'leri tespit eder, şiddet sıralama... |
| 23 | `23_yangin_muhur_kontrol.json` | Yangın Mühür Kontrolü | 11 | MEP elemanlarının duvar ve döşeme geçişlerindeki yangın mühür uyumunu kontrol ed... |
| 24 | `24_kanal_hiz_boyut_kontrol.json` | Kanal Hız ve Boyut Kontrolü | 10 | HVAC kanallarının hava hızını ve boyut sınırlarını doğrular, en-boy oranını kont... |
| 25 | `25_panel_yuk_denge.json` | Panel Yük ve Faz Denge Kontrolü | 9 | Elektrik panellerinin yük kapasitesini ve faz dengesini kontrol eder, aşırı yükl... |
| 26 | `26_devre_atama_kontrol.json` | Devre Atama Kontrolü | 11 | Aydınlatma armatürlerinin ve elektrik cihazlarının devre atamalarını kontrol ede... |
| 27 | `27_sprinkler_kapsama.json` | Sprinkler Kapsama ve Tasarım Yoğunluğu | 11 | Sprinkler başlıklarının kapsama alanını doğrular, Hazen-Williams ile basınç hesa... |
| 28 | `28_hvac_zone_oda.json` | HVAC Bölge ve Oda Eşleme | 11 | Hava terminallerini odalara eşler, HVAC bölge atamalarını doğrular ve kat bazlı ... |
| 29 | `29_boru_egim_cap_kontrol.json` | Boru Eğim ve Çap Kontrolü | 10 | Pis su ve yağmur suyu borularının eğimini ve çap uyumunu doğrular, sistemlerin a... |
| 30 | `30_kablo_tava_doluluk.json` | Kablo Taşıyıcı Doluluk Kontrolü | 10 | Kablo taşıyıcı ve kondüitlerin doluluk oranlarını kontrol eder, kapasiteye yakla... |

## 🏛 Blok 4 — Mimari (10)

| # | Dosya | Başlık | Adım | Açıklama |
|---|-------|--------|------|----------|
| 31 | `31_oda_alan_kontrol.json` | Oda Alan ve İsimlendirme Kontrolü | 13 | Oda alanlarını minimum gereksinime karşı doğrular, isimlendirme kurallarını kont... |
| 32 | `32_pafta_uretim.json` | Otomatik Pafta Üretimi | 8 | Görünüm şablonlarını uygular, kat planları için otomatik pafta (sheet) oluşturur... |
| 33 | `33_malzeme_atama.json` | Malzeme ve Kaplama Atama | 13 | Oda kaplamalarını atar, malzeme keynote eşlemesi yapar ve finish schedule üretir... |
| 34 | `34_erisebilirlik_kontrol.json` | Erişebilirlik Kontrolü | 11 | Kapı genişliklerini, ramp eğimlerini ve engelli erişim gereksinimlerini TSE 9111... |
| 35 | `35_tavan_yukseklik_kontrol.json` | Tavan Yüksekliği Kontrolü | 9 | Oda tavan yüksekliklerini minimum gereksinimle karşılaştırır, kat bazında ihlall... |
| 36 | `36_yangin_bolge_surekliligi.json` | Yangın Bölge Sürekliliği Kontrolü | 10 | Yangın kesme bölgelerinin sürekliliğini kontrol eder, kapı yangın sınıflarını du... |
| 37 | `37_kapi_numaralama.json` | Kapı Numaralama ve Eldiveni Tespiti | 9 | Kapıları oda numarasına göre numaralandırır, kulpun hangi tarafta olduğunu tespi... |
| 38 | `38_pencere_gun_isigi.json` | Pencere ve Gün Işığı Kontrolü | 10 | Penceresiz oda tespiti yapar, pencere-oda alan oranlarını kontrol eder ve gün ış... |
| 39 | `39_oda_kapi_mekan_grafigi.json` | Oda-Kapı Mekan Grafiği | 10 | Bina içi mekan bağlantı grafiğini oluşturur, oda-kapı ilişkilerini haritalandırı... |
| 40 | `40_cephe_u_deger.json` | Cephe U-Değeri ve Açıklık Oranı | 11 | Giydirme cephe U-değerini kontrol eder, panel başına açıklık oranını hesaplar ve... |

## 🏗 Blok 5 — Yapısal (10)

| # | Dosya | Başlık | Adım | Açıklama |
|---|-------|--------|------|----------|
| 41 | `41_donati_agirlik.json` | Donatı Ağırlık Raporu | 8 | Modeldeki tüm donatıları toplar, çap ve kat bazında ağırlık hesaplar, bükme list... |
| 42 | `42_beton_metraj.json` | Beton Hacim Metrajı | 11 | Kolon, kiriş, döşeme ve temel elemanlarının beton hacmini m³ cinsinden kat ve el... |
| 43 | `43_kalip_qa.json` | Kalıp QA ve TS500 Kontrol | 13 | Kalıp metrajını hesaplar, TS500 minimum kesit gereksinimlerini doğrular ve hatal... |
| 44 | `44_kolon_kiris_rapor.json` | Kolon ve Kiriş Boyut Raporu | 11 | Kolon ve kiriş boyutlarını doğrular, hizalama kontrolü yapar ve PDF boyut raporu... |
| 45 | `45_bindirme_ankraj.json` | Bindirme ve Ankraj Uzunluğu Hesabı | 10 | TS500'e göre donatı bindirme uzunluğu, ankraj uzunluğu ve minimum aralık hesabı ... |
| 46 | `46_yapisal_malzeme_sinif.json` | Yapısal Malzeme Sınıfı Kontrolü | 9 | Yapısal elemanların beton ve çelik malzeme sınıflarını doğrular, TBDY 2018 depre... |
| 47 | `47_kolon_boyut_kontrol.json` | Kolon Boyut ve Simetri Kontrolü | 10 | Kolonların minimum boyut kurallarına uygunluğunu kontrol eder, kat bazında boyut... |
| 48 | `48_temel_siniflandirma.json` | Temel Sınıflandırma ve Metrajı | 9 | Temel elemanlarını tipine göre sınıflandırır (tekil/sürekli/radye), hacim metraj... |
| 49 | `49_4d_yapisal_takvim.json` | 4D Yapısal İnşaat Takvimi | 7 | Yapısal elemanları inşaat takvimine bağlar, schedule_gate ile onay alır ve 4D si... |
| 50 | `50_egim_analiz.json` | Çatı ve Zemin Eğim Analizi | 12 | Çatı ve ıslak hacim zeminlerinin eğim değerlerini analiz eder, minimum eğim gere... |

## 📊 Blok 6 — ETL / Veri (10)

| # | Dosya | Başlık | Adım | Açıklama |
|---|-------|--------|------|----------|
| 51 | `51_model_excel_export.json` | Model Tam Excel Export | 9 | Modeldeki tüm ana kategorileri tek seferde toplar ve çok sayfalı Excel dosyasına... |
| 52 | `52_param_toplu_yaz.json` | Parametre Toplu Yazma (Excel → Revit) | 8 | Excel/CSV dosyasından parametre değerlerini okur ve Revit elemanlarına toplu yaz... |
| 53 | `53_powerbi_hazirla.json` | Power BI Veri Hazırlama | 10 | WBS kodu atar, maliyet hesaplar, ilerleme verisiyle bağlar ve Power BI için CSV ... |
| 54 | `54_shared_param_kur.json` | Shared Parametre Kurulum ve Doğrulama | 9 | EGBIM_SharedParams.txt dosyasını Revit projesine yükler, grup filtresi uygular v... |
| 55 | `55_pivot_maliyet_analiz.json` | Pivot Maliyet Analizi | 10 | Maliyet verilerini kat × kategori pivot tablosunda gösterir, en yüksek maliyetli... |
| 56 | `56_kat_bazli_ilerleme.json` | Kat Bazlı İlerleme ve WBS Bağlantısı | 11 | Kat bazında eleman sayısı, hacim ve maliyet verilerini WBS koduyla bağlar ve ile... |
| 57 | `57_malzeme_ifc_esleme.json` | Malzeme ve IFC Semantik Eşleme | 9 | Elemanları IFC tipine eşler, canonical sınıf çözümler ve malzeme keynote raporu ... |
| 58 | `58_lux_hesap_rapor.json` | Lux Hesabı ve Aydınlatma Raporu | 10 | Oda bazında lux değeri hesaplar, minimum aydınlatma gereksinimlerini doğrular ve... |
| 59 | `59_hava_degisim_hesap.json` | Hava Değişim Sayısı Hesabı | 10 | Odaların hacmine ve kullanım tipine göre gereken hava değişim sayısını hesaplar,... |
| 60 | `60_csv_model_import.json` | CSV'den Model Eleman Güncelleme | 9 | CSV dosyasından okunan verileri model elemanlarına yazar. Farklı bir sistemden g... |

## ⚙️ Blok 7 — Sistem (10)

| # | Dosya | Başlık | Adım | Açıklama |
|---|-------|--------|------|----------|
| 61 | `61_proje_baslangic.json` | Proje Başlangıç Kurulumu | 9 | Yeni Revit projesinde EGBIM shared parametrelerini yükler, workset yapısını kura... |
| 62 | `62_model_snapshot.json` | Model Snapshot ve Checksum | 10 | Modelin anlık görüntüsünü alır, checksum üretir, eleman istatistiklerini çıkarır... |
| 63 | `63_addin_saglik.json` | Add-in Sağlık ve Op Registry Kontrolü | 9 | Yüklü Revit add-in'lerini tarar, devre dışı bırakılmış eklentileri listeler ve E... |
| 64 | `64_haftalik_rapor.json` | Haftalık BIM İlerleme Raporu | 12 | Proje bilgisini, eleman istatistiklerini, maliyet özetini ve WBS ilerlemesini bi... |
| 65 | `65_trace_delta_analiz.json` | Trace Delta Analizi ve Temizleme | 10 | Önceki trace kayıtlarını bulur, mevcut model ile karşılaştırır, delta raporu üre... |
| 66 | `66_csharp_script_runner.json` | C# Script Çalıştırıcı | 5 | Roslyn ile custom C# scriptini manifest içinden çalıştırır, cache kontrolü yapar... |
| 67 | `67_grid_seviye_kur.json` | Grid ve Seviye Kurulumu (CSV'den) | 10 | CSV/Excel dosyasından grid koordinatlarını ve seviye elevasyonlarını okuyarak Re... |
| 68 | `68_aile_toplu_yukle.json` | Aile Toplu Yükleme ve Sağlık Kontrolü | 9 | Belirtilen klasörden Revit ailelerini toplu yükler, sağlık kontrolü yapar ve yük... |
| 69 | `69_view_filter_kalite.json` | View Filtre ve Etiket Kalite Kontrolü | 9 | Görünümlerde tanımsız sistem elemanlarını tespit eder, etiketsiz elemanları list... |
| 70 | `70_spatial_mekan_grafigi.json` | Bina Mekan Bağlantı Grafiği | 11 | Binadaki tüm odaları ve kapıları kullanarak mekan bağlantı grafiği oluşturur, er... |

