# EGBIMOTO — Op Referansı

Toplam **480 operasyon**, 53 kategori.

Bu dosya `op_contracts.json`'dan otomatik üretilmiştir (`deploy/generate_op_referansi.py`). Her op bir manifest adımında `"op": "<isim>"` olarak kullanılır. 🔒 işareti yazma (transaction gerektiren) op'ları belirtir.

---

## İçindekiler

- [4D/5D](#4d-5d) — 4 op
- [Aile](#aile) — 6 op
- [Analiz](#analiz) — 2 op
- [Annotation](#annotation) — 2 op
- [Cephe](#cephe) — 8 op
- [CSV](#csv) — 5 op
- [Donatı](#donati) — 9 op
- [Doğrulama](#dogrulama) — 12 op
- [Duvar](#duvar) — 2 op
- [ETL](#etl) — 2 op
- [Filtre](#filtre) — 28 op
- [Geometri](#geometri) — 18 op
- [Görünüm](#gorunum) — 9 op
- [IFC](#ifc) — 1 op
- [Kapı](#kapi) — 5 op
- [Koordinasyon](#koordinasyon) — 7 op
- [Liste](#liste) — 9 op
- [Maliyet](#maliyet) — 15 op
- [Mekansal](#mekansal) — 1 op
- [MEP Denetim](#mep-denetim) — 8 op
- [MEP Hesap](#mep-hesap) — 4 op
- [MEP HVAC](#mep-hvac) — 3 op
- [MEP Koordinasyon](#mep-koordinasyon) — 4 op
- [MEP-Elektrik](#mep-elektrik) — 22 op
- [MEP-Koordinasyon](#mep-koordinasyon) — 4 op
- [MEP-Mekanik](#mep-mekanik) — 17 op
- [MEP-Sıhhi](#mep-sihhi) — 19 op
- [Mimari](#mimari) — 10 op
- [Modelleme](#modelleme) — 12 op
- [Oda](#oda) — 7 op
- [Oluşturma](#olusturma) — 5 op
- [Parametre](#parametre) — 18 op
- [PreCheck](#precheck) — 8 op
- [Proje Yönetimi](#proje-yonetimi) — 4 op
- [QA/QC](#qa-qc) — 10 op
- [Raporlama](#raporlama) — 5 op
- [Script](#script) — 2 op
- [Semantik](#semantik) — 6 op
- [Seçim](#secim) — 2 op
- [Sistem](#sistem) — 5 op
- [Toplama](#toplama) — 57 op
- [Trace](#trace) — 5 op
- [UI](#ui) — 1 op
- [Veri](#veri) — 14 op
- [Yangın](#yangin) — 18 op
- [Yangın Hesap](#yangin-hesap) — 3 op
- [Yapısal](#yapisal) — 18 op
- [Yapısal Oluşturma](#yapisal-olusturma) — 4 op
- [Yardımcı](#yardimci) — 20 op
- [Yerleştirme](#yerlestirme) — 7 op
- [Çizim](#cizim) — 2 op
- [Çıktı](#cikti) — 9 op
- [Önizleme](#onizleme) — 2 op


## 4D/5D

### `schedule_collect_4d`

Element listesinden FourDFiveDDto üretir: geometry + 4D program bilgisi. inputs: project_start (ISO date), project_end (ISO date), schedule_map (JSON obj: kategori → {start,end,phase}), kat_sure_hafta (int default:3), max_elements (int default:500), operation_name (string).

- **Çıktı:** `FourDFiveDDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `operation_name` (String, varsayılan: '4D Yapım Simülasyonu'), `project_start` (String, varsayılan: 'DateTime.Today.ToString("yyyy-MM-dd"'), `project_end` (String, varsayılan: 'DateTime.Today.AddMonths(9'), `max_elements` (Int32, varsayılan: 500), `kat_sure_hafta` (Int32, varsayılan: 3), `schedule_map` (Object, önerilen)
- **Çıktı alanları:** `meshes`, `schedule_items`, `project_start`, `project_end`, `element_count`, `warnings`, `stats`, `bbox`

### `schedule_collect_5d`

Element listesinden FourDFiveDDto üretir: geometry + 4D program + 5D maliyet. inputs: schedule_collect_4d ile aynı parametreler + cost_step (önceki calc_cost adımının ID'si). cost_step boşsa EgbimotoData.Registry['cost_rows'] aranır.

- **Çıktı:** `FourDFiveDDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `cost_step` (String, varsayılan: ''), `operation_name` (String, önerilen), `project_start` (String, önerilen), `project_end` (String, önerilen), `kat_sure_hafta` (Int, önerilen), `max_elements` (Int, önerilen), `schedule_map` (Object, önerilen)
- **Okur:** `cost_rows`, `cost_rows (opsiyonel, P2 fallback)`
- **Çıktı alanları:** `meshes`, `schedule_items`, `cost_items`, `total_cost`, `currency`, `project_start`, `project_end`, `element_count`, `warnings`, `stats`, `bbox`

### `schedule_gate`

DagExecutor tarafından intercept edilir — FourDFiveDWindow modal açar. Bu metod ÇAĞRILMAZ; sadece OpRegistry kaydı içindir. Çıktı: vars[stepId] = 'confirmed' | 'cancelled'.

- **Çıktı:** `string`
- **Girdi:** `FourDFiveDDto`
- **Parametreler:** `title` (String, önerilen)

### `set_param_from_schedule` 🔒

schedule_collect_4d/5d çıktısını element parametrelerine yazar. inputs: schedule_step (FourDFiveDDto adım ID'si, zorunlu), start_param (default: EG_BaslangicTarihi), end_param (default: EG_BitisTarihi), phase_param (default: EG_FazAdi), wbs_param (default: EG_WbsKodu), write_cost (bool, default: true — 5D maliyet param'larını da yazar). Dönüş: yazılan element sayısı.

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `start_param` (String, varsayılan: 'EgParamNames.BaslangicTarihi'), `end_param` (String, varsayılan: 'EgParamNames.BitisTarihi'), `phase_param` (String, varsayılan: 'EgParamNames.FazAdi'), `wbs_param` (String, varsayılan: 'EgParamNames.WbsKodu'), `write_cost` (Boolean, varsayılan: True), `schedule_step` (String, varsayılan: '')


## Aile

### `family_add_param` 🔒

Aktif aile belgesine yeni bir instance veya type parametresi ekler. params: family_path (zorunlu), param_name (zorunlu), param_type (opsiyonel: Length|Area|Volume|Text|Integer|YesNo, default:Length), is_instance (opsiyonel: true=instance, false=type, default:true), group (opsiyonel: Dimensions|Identity|Materials, default:Dimensions). Çıktı: Dictionary — eklenen param adı.

- **Çıktı:** `object`
- **Parametreler:** `family_path` (String, zorunlu), `param_name` (String, zorunlu), `param_type` (String, varsayılan: 'Length'), `is_instance` (Boolean, varsayılan: True), `group` (String, varsayılan: 'Dimensions')

### `family_batch_load` 🔒

Belirtilen klasördeki tüm .rfa dosyalarını projeye toplu yükler. params: folder_path (zorunlu), pattern (opsiyonel, default:*.rfa), overwrite (opsiyonel, default:true). Çıktı: List<Dictionary> — her aile için yükleme sonucu.

- **Çıktı:** `object`
- **Parametreler:** `folder_path` (String, zorunlu), `pattern` (String, varsayılan: '*.rfa'), `overwrite` (Boolean, varsayılan: True)

### `family_health_check`

Yüklü ailelerin kalite kontrolü: kategori, tip sayısı, parametre şeması, in-place tespiti, origin kontrolü.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `expected_params` (String, varsayılan: ''), `max_types` (Int32, varsayılan: 50), `origin_threshold_m` (Double, varsayılan: 100.0), `check_nested` (Boolean, varsayılan: False)
- **Çıktı alanları:** `family_id`, `family_name`, `category`, `type_count`, `param_count`, `is_in_place`, `has_no_category`, `type_count_ok`, `missing_params`, `has_origin_issue`, `nested_count`, `status`

### `family_load_to_project` 🔒

Bir .rfa dosyasını aktif Revit projesine yükler (varsa günceller). params: family_path (zorunlu), overwrite (opsiyonel: true=güncelle, default:true). Çıktı: Dictionary — family_name, action (loaded/updated/skipped).

- **Çıktı:** `object`
- **Parametreler:** `family_path` (String, zorunlu), `overwrite` (Boolean, varsayılan: True)

### `family_open_template`

Bir aile şablonu dosyasını açar veya mevcut aile belgesini döner. params: template_path (zorunlu) — .rft veya .rfa dosyası. Çıktı: Dictionary — family_doc_title, is_new.

- **Çıktı:** `object`
- **Parametreler:** `template_path` (String, zorunlu)

### `family_type_create` 🔒

Bir aile içinde yeni tip oluşturur veya varolan tipin parametrelerini günceller. params: family_path (zorunlu), type_name (zorunlu), params (opsiyonel) — Dictionary<string,object> param adı→değer. Çıktı: Dictionary — type_name, action.

- **Çıktı:** `object`
- **Parametreler:** `family_path` (String, zorunlu), `type_name` (String, zorunlu), `params` (Dictionary<string, object?>, varsayılan: 'new Dictionary<string, object?>(')


## Analiz

### `slope_analysis`

Döşeme / çatı / topografya yüzeylerinin eğim açısını hesaplar. İsteğe bağlı olarak aktif görünümde renk override uygular. params: categories — kontrol edilecek kategoriler (opsiyonel) [Floors, Roofs, Topography] — default: hepsi unit — Degrees | Percentage | Radians (default: Percentage) apply_color — görünümde renk override uygula (default: false) color_ranges — eğim eşiği → renk listesi (opsiyonel) [{threshold:2, r:0, g:200, b:0}, {threshold:8, r:255, g:165, b:0}] face_sample_uv — face normal örnekleme noktası UV (default: 0.5) Input: yok (tüm modeli tarar) veya List<Element>. Çıktı: List<Dictionary> element_id, kategori, kat, egim_derece, egim_pct, maks_egim_derece, maks_egim_pct, yuz_adet, alan_m2

- **Çıktı:** `object`
- **Parametreler:** `unit` (String, varsayılan: 'Percentage'), `apply_color` (Boolean, varsayılan: False), `face_sample_uv` (Double, varsayılan: 0.5), `categories` (List<string>, önerilen)

### `slope_validate`

Eğim analizi sonuçlarını Türk standartlarına göre doğrular. params: limit_profile — hazır limit profili (opsiyonel) zemin_islak | cati_drenaj | otopark_rampa | erisim_rampa | yol_boyuna | yol_enine min_pct — minimum eğim % (limit_profile yoksa zorunlu) max_pct — maksimum eğim % (limit_profile yoksa zorunlu) kural_adi — raporda gösterilecek kural adı (opsiyonel) Input: slope_analysis çıktısı (List<Dictionary>). Çıktı: List<Dictionary> — sadece ihlal eden elemanlar element_id, kategori, kat, egim_pct, min_limit, max_limit, sorun, kural, seviye

- **Çıktı:** `object`
- **Parametreler:** `limit_profile` (String, varsayılan: ''), `min_pct` (Double, varsayılan: 0), `max_pct` (Double, varsayılan: 100), `kural_adi` (String, varsayılan: '$"min %{minPct} – max %{maxPct}"')


## Annotation

### `align_tags` 🔒

Tag/yazi elemanlarinin baslarini (head) hizalar veya esit dagitir. params: mode — left | right | top | bottom | center | middle | distribute_h | distribute_v (default: left) view_id — hedef gorunum ID'si (opsiyonel, bossa aktif gorunum) Hizalama view duzleminde, etiket baslarina gore yapilir. Leader'lara dokunulmaz (uclar host'a bagli kalir). Pinli elemanlar atlanir. Input: List<Element> (tag/textnote) veya bos (aktif view taranir). Cikti: List<Dictionary> — element_id, tip, mode, tasindi, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `mode` (String, varsayılan: 'left'), `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `tip`, `mode`, `tasindi`, `durum`

### `arrange_tags` 🔒

Leader'li IndependentTag'leri gorunum kenarlarina otomatik dizer ve leader cizgilerinin caprazlarini (overlap) konum takasiyla cozer. params: view_id — hedef gorunum ID'si (opsiyonel, bossa aktif gorunum) GEREKSINIM: hedef gorunumde CropBox aktif olmali (kenar referansi). Tag'ler leader ucunun konumuna gore sol/sag gruba ayrilir, kenarlara esit aralikla yerlestirilir, 2 gecirste leader caprazlari cozulur. Input: yok (view taranir). Cikti: List<Dictionary> — element_id, taraf, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `taraf`, `durum`


## Cephe

### `collect_curtain_panels`

Projedeki tüm curtain panel elementlerini toplar. params: level (opsiyonel) — belirli kat, workset (opsiyonel) — çalışma seti filtresi. Çıktı: List<Element> — Panel elementleri.

- **Çıktı:** `object`
- **Parametreler:** `level` (String, varsayılan: ''), `workset` (String, varsayılan: '')

### `facade_area_by_type`

Panel tipine göre toplam cephe alanını hesaplar. Input: facade_panel_matrix çıktısı (List<Dictionary>). Çıktı: List<Dictionary> — tip, toplam_alan_m2, panel_adet.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_export_schedule`

Cephe metraj tablosunu HTML + özet satırıyla dışa aktarır. params: output_path (zorunlu), title (opsiyonel). Input: facade_area_by_type çıktısı veya herhangi bir List<Dictionary>. Çıktı: Dictionary — dosya yolu ve satır sayısı.

- **Çıktı:** `object`
- **Parametreler:** `output_path` (String, zorunlu), `title` (String, varsayılan: 'EGBIMOTO Cephe Metraj Raporu')

### `facade_joint_validate`

Curtain panellerin derz parametrelerini doğrular. params: min_derz_mm (default:10), max_derz_mm (default:40). Input: List<Element> — panel elementleri. Çıktı: List<Dictionary> — hatalı panel kayıtları.

- **Çıktı:** `object`
- **Parametreler:** `min_derz_mm` (Double, varsayılan: 10.0), `max_derz_mm` (Double, varsayılan: 40.0)

### `facade_opening_ratio`

Cephe saydamlık oranını hesaplar (pencere alanı / toplam cephe alanı). TS 825 enerji performansı için referans değer. Input: collect_curtain_walls veya collect_walls çıktısı. Çıktı: Dictionary — saydam_alan_m2, opak_alan_m2, saydamlik_orani.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_panel_matrix`

Curtain panellerini tip ve kata göre gruplandırarak matris tablosu oluşturur. Her satır: panel_id, tip, kat, alan_m2, u_degeri, malzeme. Input: List<Element> — panel elementleri. Çıktı: List<Dictionary> — panel matrisi.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_system_params` 🔒

Cephe duvarlarına EGBIMOTO TR BIM parametrelerini toplu yazar. params: panel_tip (zorunlu), derz_genislik_mm (opsiyonel, default:20), derz_tip (opsiyonel, default:Silikon), u_degeri (opsiyonel), kaplama_malzeme (opsiyonel). Input: collect_curtain_panels çıktısı (List<Element>). Çıktı: Dictionary — yazılan element sayısı ve log.

- **Çıktı:** `object`
- **Parametreler:** `panel_tip` (String, zorunlu), `derz_genislik_mm` (Double, varsayılan: 20.0), `derz_tip` (String, varsayılan: 'Silikon'), `u_degeri` (String, varsayılan: ''), `kaplama_malzeme` (String, varsayılan: '')

### `facade_u_value_check`

Cephe panellerinin U değerini TS 825 limitine göre kontrol eder. params: max_u_value (default:1.8 W/m²K — TS 825 3. bölge pencere limiti). Input: List<Element> — panel elementleri. Çıktı: List<Dictionary> — U değeri aşımları.

- **Çıktı:** `object`
- **Parametreler:** `max_u_value` (Double, varsayılan: 1.8)


## CSV

### `csv_read`

CSV dosyasını okur ve List<Dict> döner. has_header=true → ilk satır alan adı olur.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `file_path` (String, zorunlu), `delimiter` (String, varsayılan: ','), `has_header` (Boolean, varsayılan: True), `encoding` (String, varsayılan: 'utf-8'), `skip_rows` (Int32, varsayılan: 0)

### `csv_write`

List<Dict> → CSV dosyasına yazar. encoding=utf-8-sig Excel'de Türkçe karakter desteği sağlar.

- **Çıktı:** `string`
- **Girdi:** `List<Dict>`
- **Parametreler:** `file_path` (String, zorunlu), `delimiter` (String, varsayılan: ','), `include_header` (Boolean, varsayılan: True), `encoding` (String, varsayılan: 'utf-8-sig')

### `excel_xml_read`

xlsx dosyasını .NET ZipArchive + XML ile okur (openpyxl gerekmez). EGBIMOTO yaklaşımı: standart kütüphane, sıfır bağımlılık.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `file_path` (String, zorunlu), `sheet_name` (String, varsayılan: ''), `has_header` (Boolean, varsayılan: True), `skip_rows` (Int32, varsayılan: 0)

### `table_to_points`

Dict satırlarındaki x/y/z alanlarını Revit XYZ nokta listesine dönüştürür. Geometry Ops ve Create Ops için veri köprüsü.

- **Çıktı:** `List<object?>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `x_field` (String, varsayılan: 'x'), `y_field` (String, varsayılan: 'y'), `z_field` (String, varsayılan: 'z'), `unit` (String, varsayılan: 'mm')

### `table_validate_schema`

Dict listesinin beklenen alanları içerip içermediğini kontrol eder.

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `required_fields` (String, zorunlu), `optional_fields` (String, önerilen)
- **Çıktı alanları:** `valid`, `missing_fields`, `row_count`, `field_count`, `message`


## Donatı

### `calc_anchorage_length`

TS500'e göre ankraj boyu hesaplar. params: diameter_mm, fck, fyk, cover_mm, hook (true/false)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: 12), `fck` (Double, varsayılan: 25), `fyk` (Double, varsayılan: 420), `cover_mm` (Double, varsayılan: 30), `hook` (Boolean, varsayılan: False)

### `calc_lap_length`

TS500'e göre bindirme boyu hesaplar. params: diameter_mm, fck (MPa), fyk (MPa), cover_mm

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: 12), `fck` (Double, varsayılan: 25), `fyk` (Double, varsayılan: 420), `cover_mm` (Double, varsayılan: 30)

### `calc_min_spacing`

TS500'e göre minimum donatı aralığını hesaplar. params: diameter_mm, aggregate_size_mm

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: 12), `aggregate_size_mm` (Double, varsayılan: 20)

### `rebar_summary_by_diameter`

Donatıları çap bazında gruplar ve toplam ağırlık hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `rebar_summary_by_level`

Donatıları kat bazında gruplar ve toplam ağırlık hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `rebar_total_weight`

Donatı listesinin toplam ağırlığını kg olarak döner

- **Çıktı:** `double`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `rebar_weight_calc`

Donatı listesini çap tablosu + fabrika boyu + bindirme katsayısıyla hesaplar. RevitPlugins-master RebarElement.Calculate() mantığından türetilmiştir. Fark: TS500 bindirme uzunlukları (Türk standardı) kullanılır. Fabrika uzunluğunu aşan çubuklarda bindirme katsayısı otomatik uygulanır. params: factory_length_mm — fabrika çubuk boyu (opsiyonel, default: 12000 — Türkiye) fck — beton sınıfı MPa (opsiyonel, default: 25) fyk — çelik sınıfı MPa (opsiyonel, default: 420) cover_mm — pas payı mm (opsiyonel, default: 30) apply_overlap — bindirme katsayısı uygulansın mı (opsiyonel, default: true) Input: collect_rebar çıktısı (List<Element>). Çıktı: List<Dictionary> — element_id, cap_mm, uzunluk_m, kg_per_m, fabrika_adet, bindirme_katsayisi, net_uzunluk_m, agirlik_kg, kat.

- **Çıktı:** `object`
- **Parametreler:** `factory_length_mm` (Double, varsayılan: 12000), `fck` (Double, varsayılan: 25), `fyk` (Double, varsayılan: 420), `cover_mm` (Double, varsayılan: 30), `apply_overlap` (Boolean, varsayılan: True)

### `rebar_weight_table`

Donatı listesinin çap, uzunluk ve ağırlık tablosunu döner. {element_id, cap_mm, uzunluk_m, agirlik_kg}

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `validate_rebar_ts500`

Donatı listesini TS500 kurallarına göre doğrular. params: fck (MPa), fyk (MPa), cover_mm

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `fck` (Double, varsayılan: 25), `fyk` (Double, varsayılan: 420), `cover_mm` (Double, varsayılan: 30)


## Doğrulama

### `check_overlapping_rooms`

Çakışan odaları tespit eder (aynı katta merkezi 0.5m'den yakın odalar). FIX#11: Kat bazında gruplandırma ile O(n²)→O(k×m²) iyileştirildi.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `check_unplaced_rooms`

Yerleştirilmemiş (unplaced) odaları tespit eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `check_zero_volume`

Sıfır hacimli elemanları tespit eder (HOST_VOLUME_COMPUTED == 0)

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `merge_validation_reports`

ValidationReport'ları birleştirir. from_many, inputs.lists veya tekil giriş desteklenir.

- **Çıktı:** `ValidationReport`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'Birleşik Doğrulama Raporu'), `lists` (Object, varsayılan: '')

### `param_exists_check`

Elemanlarda params.param_name parametresinin varlığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu)

### `param_filled_check`

Elemanlarda params.param_name parametresinin dolu olduğunu kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu), `severity` (String, varsayılan: 'ERROR')

### `param_range_check`

Elemanlarda params.param_name değerinin params.min ile params.max arasında olduğunu kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen), `min` (Double, varsayılan: 'double.MinValue'), `max` (Double, varsayılan: 'double.MaxValue'), `severity` (String, varsayılan: 'WARNING')

### `param_value_check`

Elemanlarda params.param_name == params.expected_value kontrolü yapar

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen), `expected_value` (String, önerilen), `severity` (String, varsayılan: 'ERROR')

### `validate_ids`

Eleman listesini params.ids_path IDS dosyasına göre doğrular

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `ids_path` (String, varsayılan: 'ctx.GetString("path", ""')

### `validate_qa`

Eleman listesini params.rules_path QA kural dosyasına göre doğrular

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `rules_path` (String, varsayılan: 'ctx.GetString("path", ""'), `input` (Object, varsayılan: ''), `elements` (Object, varsayılan: '')

### `validation_summary`

ValidationReport'u WPF sonuç penceresinde gösterir (hata/uyarı satırları, renk kodlaması, 'Modelde Göster', CSV dışa aktarım).

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `input` (Object, varsayılan: ''), `report` (Object, varsayılan: '')

### `validation_to_rows`

ValidationReport'u dict satır listesine dönüştürür (export için)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `input` (Object, varsayılan: ''), `report` (Object, varsayılan: '')


## Duvar

### `wall_type_export_csv`

Projedeki mevcut Basic Wall tiplerini wall_type_from_csv uyumlu CSV formatına aktarır. Oluşturulan CSV doğrudan wall_type_from_csv ile tekrar yüklenebilir. params: output_path — CSV çıktı dosyası yolu (zorunlu) filter_name — tip adı filtresi, regex (opsiyonel) include_params — opsiyonel parametreler dahil edilsin mi (default: true) Çıktı: Dictionary — output_path, wall_type_count, row_count

- **Çıktı:** `object`
- **Parametreler:** `output_path` (String, zorunlu), `filter_name` (String, varsayılan: ''), `include_params` (Boolean, varsayılan: True)

### `wall_type_from_csv` 🔒

CSV dosyasından Revit Basic Wall tipleri oluşturur veya günceller. pyrevit-wall-library-builder (Elif Bilge Bulut) projesinden C# portudur. CSV yapısı (zorunlu sütunlar): TypeName, LayerOrder, Function, MaterialName, Thickness_mm CSV yapısı (opsiyonel): IsCore, TypeComments, Description, FireRating, Keynote, AssemblyCode, AssemblyDescription, Manufacturer, Model, Cost, URL Function değerleri (TR/EN): Structure/Yapı, Substrate/AltKatman, Insulation/Yalıtım, Finish1/Kaplama1, Finish2/Kaplama2, Membrane/Membran params: csv_path — CSV dosyası yolu (zorunlu) mode — create | update | skip | rename (default: create) create: yeni oluştur, varsa atla update: varsa güncelle skip: varsa atla rename: varsa yeni isimle kopyala rename_suffix — rename modunda ek (default: ' - Import') base_wall_name — şablon duvar tipi adı (default: ilk Basic Wall) delimiter — CSV ayırıcı (default: auto) dry_run — true ise işlem yapmaz, sadece doğrular (default: false) Çıktı: List<Dictionary> — her satır bir duvar tipi sonucunu temsil eder type_name, action (CREATED/UPDATED/SKIPPED/RENAMED/ERROR), katman_adet, toplam_kalinlik_mm, uyarilar, mesaj

- **Çıktı:** `object`
- **Parametreler:** `csv_path` (String, zorunlu), `mode` (String, varsayılan: 'create'), `rename_suffix` (String, varsayılan: ' - Import'), `base_wall_name` (String, varsayılan: ''), `delimiter` (String, varsayılan: 'auto'), `dry_run` (Boolean, varsayılan: False)


## ETL

### `load_poz_canonical_map`

data/poz/poz_canonical_map.json dosyasını registry'ye yükler. poz_match_keynote_aware için ön koşul.

- **Çıktı:** `Dictionary`
- **Girdi:** `—`
- **Parametreler:** `path` (String, varsayılan: '')
- **Yazar:** `poz_canonical_map`

### `load_poz_section_rules`

data/poz/poz_section_rules.json dosyasını registry'ye yükler. canonical_class → poz prefix listesi. poz_match_keynote_aware için.

- **Çıktı:** `Dictionary`
- **Girdi:** `—`
- **Parametreler:** `path` (String, varsayılan: '')
- **Yazar:** `poz_section_rules`


## Filtre

### `add_column`

Dict listesine params.field adında params.value değerli sabit sütun ekler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, önerilen), `value` (String, önerilen)

### `distinct_rows`

Dict listesinden params.field alanına göre tekrar eden satırları kaldırır

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, önerilen)

### `elements_to_rows`

Eleman listesini dict satır listesine dönüştürür (element_id, kategori, tip, kat)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elements_to_rows_with_params`

Eleman listesini dict satır listesine dönüştürür + params.param_names (virgülle) parametrelerini ekler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (String, önerilen)

### `filter_by_category`

Eleman listesini params.category kategorisine göre filtreler

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `category` (String, önerilen)

### `filter_by_level`

Elemanları params.level_name kattaki elemanlarla filtreler

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `level_name` (String, zorunlu)

### `filter_by_level_range`

Elemanları params.min_level ile params.max_level arasındaki katlara göre filtreler

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_level` (String, önerilen), `max_level` (String, önerilen)

### `filter_by_param`

Elemanları params.param_name == params.value koşuluyla filtreler. operator: equals|contains|not_equals|starts_with|gt|lt

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen), `value` (String, önerilen), `operator` (String, varsayılan: 'equals')

### `filter_by_type`

Elemanları tip adı params.type_name içerenlere göre filtreler

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, önerilen)

### `filter_by_workset`

Eleman listesini params.workset_name workset'ine göre filtreler

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `workset_name` (String, önerilen)

### `filter_empty_param`

params.param_name parametresi boş olan elemanları döner

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `filter_not_empty_param`

params.param_name parametresi dolu olan elemanları döner

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `filter_rows`

Dict listesini params.field operator params.value koşuluyla filtreler. operator: eq|contains|gt|lt|not_eq|starts_with

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu), `value` (String, önerilen), `operator` (String, varsayılan: 'eq')

### `filter_rows_multi`

Dict listesini birden fazla koşulla filtreler. params: conditions [{field,operator,value}]

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `conditions` (Array, önerilen)

### `group_by`

Dict listesini params.field alanına göre gruplar. {key, count, rows} listesi döner

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, önerilen)

### `group_elements_by_category`

Eleman listesini Revit kategorisine göre gruplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `group_elements_by_level`

Eleman listesini katlara göre gruplar. {level, count, elements} listesi döner

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `group_elements_by_type`

Eleman listesini tip adına göre gruplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `join_rows`

İki satır listesini params.key_field alanına göre LEFT JOIN yapar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `key_field` (String, önerilen)

### `merge_lists`

Eleman listelerini birleştirir. inputs.lists veya from_many desteklenir.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `lists` (Object, varsayılan: '')

### `merge_rows`

Dict listelerini birleştirir. inputs.lists ile veya from_many ile kullanılabilir.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `lists` (Object, varsayılan: '')

### `rename_column`

Dict listesinde params.from sütununu params.to olarak yeniden adlandırır

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `from` (String, önerilen), `to` (String, önerilen)

### `select_columns`

Dict listesinden params.fields (virgülle ayrılmış) alanlarını seçer

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fields` (String, önerilen)

### `select_field`

Satır listesinden params.fields (virgülle ayrılmış) alanlarını seçer

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fields` (String, önerilen), `rows` (Object, varsayılan: '')

### `skip_n`

Listeden ilk params.count elemanı atlar

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `count` (Int32, varsayılan: 0)

### `sort_rows`

Dict listesini params.field alanına göre sıralar. params.descending=true ile ters

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, önerilen), `descending` (Boolean, varsayılan: False)

### `take_n`

Listeden ilk params.count elemanı alır

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `count` (Int32, varsayılan: 10)

### `where`

Satır listesini filtreler. params: field, op (eq|neq|gt|lt|contains), value

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu), `value` (String, zorunlu), `op` (String, varsayılan: 'eq'), `rows` (Object, varsayılan: '')


## Geometri

### `beam_volume`

Kiriş listesinin hacmini m3 olarak hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `column_height`

Kolon listesinin yüksekliğini m olarak hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `column_volume`

Kolon listesinin hacmini m3 olarak hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `curve_from_element`

Eleman listesinden eğri (Curve) geometrisini çıkarır. {element_id, uzunluk_m}

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_area`

Eleman listesinin yüzey alanını m2 olarak hesaplar (HOST_AREA_COMPUTED)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_bounding_box`

Eleman listesinin bounding box boyutlarını döner. {min_x, min_y, min_z, max_x, max_y, max_z} m

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_length`

Eleman listesinin uzunluğunu m olarak hesaplar (CURVE_ELEM_LENGTH)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_volume`

Herhangi bir eleman listesinin hacmini m3 olarak hesaplar (HOST_VOLUME_COMPUTED)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_volume_geometry`

Eleman geometrisinden hacim hesaplar (Solid union). Daha yavaş ama daha doğru.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `floor_volume`

Döşeme listesinin hacmini m3 olarak hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `foundation_volume`

Temel listesinin hacmini m3 olarak hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_by_system`

MEP elemanlarını sistem adına göre gruplar ve toplam uzunluk hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_summary`

MEP eleman listesinin sistem adı ve uzunluk özetini döner

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_total_length`

MEP eleman listesinin toplam uzunluğunu m olarak döner

- **Çıktı:** `double`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `room_area`

Oda listesinin alanını m2 olarak hesaplar. {element_id, oda_adi, kat, alan_m2}

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `wall_length`

Duvar listesinin uzunluğunu m olarak hesaplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `wall_net_area`

Duvar net alanını hesaplar (brüt - kapı/pencere açıklıkları). m2

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `wall_volume`

Duvar listesinin hacmini m3 olarak hesaplar. {element_id, tip, kat, hacim_m3}

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok


## Görünüm

### `check_untagged_elements`

Görünümde tag'siz olan elemanları listeler. tag_elements ile birlikte QA pipeline'ı oluşturur.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `category`, `family_type`, `level`, `tag_count`

### `create_view_filter` 🔒

System Classification bazlı ParameterFilterElement oluşturur ve seçili görünümlere renk override ile uygular. Örn: Supply=Mavi, Exhaust=Yeşil, Return=Kırmızı pipeline.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `filter_name` (String, zorunlu), `categories` (String, zorunlu), `rule_value` (String, zorunlu), `param_name` (String, varsayılan: 'System Classification'), `rule_operator` (String, varsayılan: 'contains'), `color_r` (Int32, varsayılan: 0), `color_g` (Int32, varsayılan: 0), `color_b` (Int32, varsayılan: 255), `line_weight` (Int32, varsayılan: 4), `fill_pattern` (String, varsayılan: 'Solid Fill'), `overwrite` (Boolean, varsayılan: False)
- **Çıktı alanları:** `view_id`, `view_name`, `filter_id`, `filter_name`, `applied`, `status`

### `detect_undefined_system`

System Type = Undefined olan MEP elemanlarını tespit eder. Modelleme hatası: fitting/takeoff fiziksel bağlantısı eksik.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `discipline` (String, varsayılan: 'all')
- **Çıktı alanları:** `element_id`, `category`, `family_type`, `level`, `system_type`, `fix_hint`

### `tag_elements` 🔒

Eleman listesine otomatik IndependentTag yerleştirir. params.tag_type_name ile tag family tipi seçilir.

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `tag_type_name` (String, zorunlu), `view_id` (String, varsayılan: ''), `leader` (Boolean, varsayılan: False), `orientation` (String, varsayılan: 'horizontal')

### `view_color_override` 🔒

Verilen elemanlara aktif görünümde renk override uygular (dolu yüzey + kenarlık). params: r,g,b (0-255, default kırmızı 255/0/0), reset (bool, default false — true ise override kaldırılır).

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element> | List<Dictionary<string, object?>>`
- **Parametreler:** `reset` (Boolean, varsayılan: False), `r` (Int32, varsayılan: 255), `g` (Int32, varsayılan: 0), `b` (Int32, varsayılan: 0)
- **Çıktı alanları:** `overridden`

### `view_create_selection_box` 🔒

Verilen elemanların bounding box birleşimini kapsayan bir 3B section box oluşturur ve aktif/uygun 3B görünüme uygular. params: padding_m (default 0.3)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element> | List<Dictionary<string, object?>>`
- **Parametreler:** `padding_m` (Double, varsayılan: 0.3)
- **Çıktı alanları:** `applied`, `view_id`

### `view_isolate_elements` 🔒

Verilen elemanları aktif görünümde geçici olarak izole eder (IsolateElementsTemporary). 'from' ile bir önceki step'in çıktısını kullanır.

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element> | List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `isolated`

### `view_reset_temporary_mode` 🔒

Aktif görünümdeki geçici izole/gizle modunu ve (varsa) section box'ı sıfırlar. params: reset_section_box (bool, default false)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `reset_section_box` (Boolean, varsayılan: False)
- **Çıktı alanları:** `reset`

### `view_temp_hide_elements` 🔒

Verilen elemanları aktif görünümde geçici olarak gizler (HideElementsTemporary). 'from' ile bir önceki step'in çıktısını kullanır.

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element> | List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `hidden`


## IFC

### `ifc_export`

Revit modelini IFC 2x3 / IFC 4 olarak dışa aktarır. params: output_dir, file_name, ifc_version (IFC2x3|IFC4), export_linked_files

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `output_dir` (String, varsayılan: 'Environment.GetFolderPath(Environment.SpecialFolder.Desktop'), `file_name` (String, varsayılan: '$"{Path.GetFileNameWithoutExtension(rctx.Doc.Title'), `ifc_version` (String, varsayılan: 'IFC2x3'), `export_linked_files` (Boolean, varsayılan: False)


## Kapı

### `door_clearance_check`

Kapı temiz açıklık genişliğini ve menteşe karşı duvar boşluğunu TS 9111'e göre kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_clear_width_mm` (Double, varsayılan: 850), `min_latch_side_mm` (Double, varsayılan: 300), `severity` (String, varsayılan: 'WARNING')

### `door_fire_rating_from_wall` 🔒

Host duvarın yangın sınıfını okuyarak kapı parametresine yazar

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `wall_param` (String, varsayılan: 'Yangın Dayanım Süresi'), `door_param` (String, varsayılan: 'EG_YanginDayanim'), `rating_map` (String, varsayılan: '')

### `door_handing_detect`

Kapı el (L/R) ve açılış yönünü (inward/outward) tespit eder

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `handing`, `swing`, `hand_flipped`, `facing_flipped`, `facing_x`, `facing_y`, `hand_x`, `hand_y`

### `door_number_by_room` 🔒

Kapıları oda numarasına göre sıralar ve params.param_name parametresine yazar

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'Mark'), `separator` (String, varsayılan: '-'), `start_index` (Int32, varsayılan: 1), `use_room` (String, varsayılan: 'to_room')

### `room_door_relation_map`

Her oda için boundary'sindeki kapıları haritalar — oda → kapı listesi Dict

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `door_count`, `door_ids`, `door_marks`


## Koordinasyon

### `clash_detect_matrix`

İki kategori grubu (A vs B) arasında hard clash (katı çakışma) tespiti. BBox ön eleme + ElementIntersectsSolidFilter kesin testi ile çalışır. params: group_a — A disiplini kategorileri (virgülle, örn: OST_DuctCurves,OST_PipeCurves) group_b — B disiplini kategorileri (virgülle, örn: OST_StructuralFraming) tolerance_mm — BBox genişletme payı mm (opsiyonel, default: 10) max_results — maksimum bulgu sayısı (opsiyonel, default: 1000) Çıktı: List<Dictionary> — her satır bir çakışma: a_id, a_category, a_name, b_id, b_category, b_name, clash_x, clash_y, clash_z, overlap_volume_m3, disiplin_cifti, seviye

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `None`
- **Parametreler:** `group_a` (String, varsayılan: ''), `group_b` (String, varsayılan: ''), `tolerance_mm` (Int32, varsayılan: 10), `max_results` (Int32, varsayılan: 1000)
- **Çıktı alanları:** `a_id`, `a_category`, `b_id`, `b_category`, `clash_x`, `clash_y`, `clash_z`, `overlap_volume_m3`, `disiplin_cifti`, `seviye`

### `clash_severity_sort`

clash_detect_matrix bulgularını kesişim hacmine göre önceliklendirir. En büyük hacimli çakışmalar (en kritik) en üstte sıralanır. Input: List<Dictionary> (clash_detect_matrix çıktısı) Çıktı: aynı liste, overlap_volume_m3 azalan sıralı + sira_no eklenmiş

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok
- **Çıktı alanları:** `sira_no`

### `coord_check_clearance`

İki eleman grubu arasında minimum temizlik mesafesini kontrol eder

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_clearance_mm` (Double, varsayılan: 50.0), `secondary_elements` (List<Element>, önerilen)

### `coord_validate_level_consistency`

MEP elemanlarının mimari seviyelerle tutarlılığını kontrol eder

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `coord_validate_penetration_firestop`

Yangın bölgesi geçişlerinde mühürleme parametresini kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `place_opening` 🔒

smart_check_mep_no_opening bulgularına göre boşluk aile örnekleri yerleştirir. Boyutları otomatik hesaplar (MEP bbox + offset_mm payı). params: family_name — boşluk aile adı (zorunlu) Örn: 'EG_Rezervasyon_Dikdortgen' veya 'Generic Opening' type_name — tip adı (opsiyonel, default: 'Standard') offset_mm — boyut payı mm (opsiyonel, default: 50) param_b — genişlik parametre adı (opsiyonel, default: 'Width') param_h — yükseklik parametre adı (opsiyonel, default: 'Height') param_sistem — sistem adı parametre adı (opsiyonel, default: 'EG_Sistem') skip_existing — mevcut boşluk yakınındakileri atla (opsiyonel, default: true) use_diameter_table — true ise çap tablosundan opening boyutu seçer (opsiyonel, default: false) dry_run — true ise yerleştirme yapmaz, sadece sayar (opsiyonel, default: false) Input: smart_check_mep_no_opening çıktısı (List<Dictionary>). Çıktı: Dictionary — yerlestirilen, atlanan, hata, family_name.

- **Çıktı:** `object`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, varsayılan: 'Standard'), `offset_mm` (Double, varsayılan: 50.0), `param_b` (String, varsayılan: 'Width'), `param_h` (String, varsayılan: 'Height'), `param_sistem` (String, varsayılan: 'EG_Sistem'), `skip_existing` (Boolean, varsayılan: True), `dry_run` (Boolean, varsayılan: False), `use_diameter_table` (Boolean, varsayılan: False)

### `smart_check_mep_no_opening`

MEP elemanlarının yapısal eleman (duvar/döşeme/kiriş) geçişlerini tarar. Her kesişimde boşluk aile örneği var mı kontrol eder. Yoksa → bulgu üretir (EGBIMOTO koordinasyon hatası). params: host_categories — kontrol edilecek yapısal kategoriler (opsiyonel, default: [Walls, Floors, StructuralFraming]) mep_categories — kontrol edilecek MEP kategoriler (opsiyonel, default: [Pipes, Ducts, CableTray, Conduit]) tolerance_mm — kesişim toleransı mm (opsiyonel, default: 15) check_opening — boşluk kontrolü de yapılsın mı (opsiyonel, default: true) use_solid_intersection— Solid kesişim testi (kesin ama yavaş, default: false) scan_linked_models — linked modellerdeki MEP'leri de tara (default: false) Input: yok (tüm modeli tarar) veya List<Element> MEP elemanları. Çıktı: List<Dictionary> — her satır bir kesişim bulgusunu temsil eder. element_id, element_category, host_id, host_category, kesisim_nokta_x/y/z, boyut_b_mm, boyut_h_mm, opening_var, sorun, seviye

- **Çıktı:** `object`
- **Parametreler:** `host_categories` (List<string>, zorunlu), `mep_categories` (List<string>, zorunlu), `tolerance_mm` (Double, varsayılan: 15.0), `check_opening` (Boolean, varsayılan: True), `use_solid_intersection` (Boolean, varsayılan: False), `scan_linked_models` (Boolean, varsayılan: False)


## Liste

### `list_cross_product`

İki listenin kartezyen çarpımını üretir. A=[1,2], B=[a,b] → [[1,a],[1,b],[2,a],[2,b]]

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `second_key` (String, zorunlu)

### `list_filter_by_rule`

List<object?> içindeki Dict elemanlarını field/operator/value kuralıyla filtreler.

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `field` (String, zorunlu), `operator` (String, varsayılan: 'eq'), `value` (String, varsayılan: '')

### `list_flatten`

İç içe listeyi düzleştirir. levels=1 bir seviye açar.

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `levels` (Int32, varsayılan: 1)

### `list_group_by_key`

Dict listesini params.key_field alanına göre gruplar. Çıktı: [{key_value, items: [...]}]

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `key_field` (String, zorunlu)
- **Çıktı alanları:** `key_field_value`, `items`, `count`

### `list_map`

Her elemana template veya field dönüşümü uygular. Dict listesi için: template='{Mark} - {Level}', output_field='etiket'

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `template` (String, varsayılan: ''), `field` (String, varsayılan: ''), `output_field` (String, varsayılan: '_mapped')

### `list_sort_by`

Dict listesini params.sort_field alanına göre sıralar.

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `sort_field` (String, zorunlu), `ascending` (Boolean, varsayılan: True)

### `list_take_every_n`

Listeden her N. elemanı alır. n=3, offset=0 → [0,3,6,9,...]

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `n` (Int32, varsayılan: 2), `offset` (Int32, varsayılan: 0)

### `list_transpose`

İç içe listeyi transpoze eder. [[1,2],[3,4]] → [[1,3],[2,4]]

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** yok

### `list_zip`

İki listeyi eleman bazlı çiftler olarak birleştirir. params.second_key: manifest'teki ikinci listenin step_id'si.

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `second_key` (String, zorunlu)


## Maliyet

### `beton_metraj`

Yapısal elemanların beton hacmini m3 olarak hesaplar (duvar+döşeme+kolon+kiriş+temel)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `calc_cost`

Satır listesindeki miktar x birim_fiyat = toplam_maliyet hesaplar. params: quantity_field

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `quantity_field` (String, varsayılan: 'hacim_m3')
- **Çıktı alanları:** `miktar`, `toplam_maliyet`

### `cost_by_level`

Maliyet satırlarını kat bazında özetler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `cost_summary`

Maliyet satırlarını WBS/kategori bazında özetler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `group_by` (String, varsayılan: 'kategori')

### `kalip_all`

Tüm yapısal kategorilerin kalıp alanını tek geçişte hesaplar. ElementIntersectsElementFilter ile gerçek geometri prefilter. params: include_edges (bool, default true)

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Element>`
- **Parametreler:** `include_edges` (Boolean, varsayılan: True)
- **Yazar:** `kalip_traces`

### `kalip_column`

Kolon listesinin kalıp alanını hesaplar (çevre x yükseklik). m2

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `kalip_floor`

Döşeme listesinin kalıp alanını hesaplar (alt yüzey = HOST_AREA_COMPUTED). m2

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `kalip_summary`

Kalıp satırlarını kat ve tip bazında özetler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `group_by` (String, varsayılan: 'kat')

### `kalip_wall`

Duvar listesinin kalıp alanını hesaplar (2 x uzunluk x yükseklik). m2

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `kalip_write_back`

Kalıp satırlarını Revit parametrelerine yazar: Formwork_Area, TR_KalipAlani, TR_KalipPozNo, TR_KalipPozAdi, TR_KalipBirimFiyat, TR_KalipToplamTutar

- **Çıktı:** `Dictionary`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok
- **Okur:** `poz_data`

### `poz_match`

Satır listesindeki kategori/tip alanlarını POZ veritabanıyla eşleştirir

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `category_field` (String, varsayılan: 'kategori'), `type_field` (String, varsayılan: 'tip')
- **Okur:** `poz_data`
- **Çıktı alanları:** `poz_no`, `poz_adi`, `birim`, `birim_fiyat`

### `poz_match_by_code`

Satır listesindeki params.poz_code_field alanını POZ koduna göre eşleştirir

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `poz_code_field` (String, varsayılan: 'poz_no')
- **Okur:** `poz_data`
- **Çıktı alanları:** `poz_adi`, `birim`, `birim_fiyat`

### `poz_match_keynote_aware`

Eleman poz'unu üç aşamada çözer: 1. Keynote / TR_CSB_PozNo (instance → type, 8 aday parametre) 2. Keynote eşleşmezse: poz_canonical_map → STA_FORMWORK vb. → section prefix ile arama 3. Hiçbiri yoksa: kategori bazlı varsayılan poz Çıktı: input row listesi — poz_no, poz_adi, birim, birim_fiyat eklenerek döner.

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok
- **Okur:** `poz_canonical_map`, `poz_data`, `poz_section_rules`

### `wall_area`

Duvar listesinin net alanını m2 olarak hesaplar (HOST_AREA_COMPUTED)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `xlsx_import_apply` 🔒

xlsx_import_preview çıktısındaki (veya doğrudan bir .xlsx dosyasındaki) satırları element_id üzerinden eşleştirip belirtilen kolon→parametre eşlemesine göre Revit parametrelerine yazar. params: file_path (file_path verilirse dosyadan okur, yoksa 'from' kullanılır), key_field (default 'element_id'), field_param_map (Dictionary<string,string>, kolon adı → Revit parametre adı), only_changed (bool, default true — _status alanı 'changed'/'new' olmayan satırları atlar).

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Dictionary<string, object?>> (file_path verilmezse kullanılır)`
- **Parametreler:** `key_field` (String, varsayılan: 'element_id'), `only_changed` (Boolean, varsayılan: True), `file_path` (String, varsayılan: ''), `field_param_map` (Dictionary<string, object?>, varsayılan: 'new(')
- **Çıktı alanları:** `written`, `skipped`


## Mekansal

### `build_spatial_graph`

room_boundary_extract çıktısından oda komşuluk grafını edge list olarak üretir. Downstream: yangın bölgesi analizi, kaçış yolu, komşu oda sayımı.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `include_exterior` (Boolean, varsayılan: True), `deduplicate` (Boolean, varsayılan: True)
- **Çıktı alanları:** `from_room_id`, `from_room_name`, `from_room_number`, `to_room_id`, `to_room_name`, `to_room_number`, `shared_wall_id`, `shared_wall_type`, `edge_length_mm`, `fire_rating`, `is_exterior`


## MEP Denetim

### `cable_tray_fill_check`

Kablo tava doluluk oranını kontrol eder (standart: ≤%70). Pre-check: fill_param (default EG_KabloDoluluk) tavada yoksa eklenmesi istenir.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `fill_param` (String, varsayılan: 'EG_KabloDoluluk'), `max_fill_pct` (Double, varsayılan: 70.0)

### `conduit_fill_check`

Boru (conduit) iletken doluluk oranını kontrol eder (NEC/IEC: ≤%40). Pre-check: EG_IletkenSayisi + EG_IletkenKesit_mm2 yoksa eklenmesi istenir.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `count_param` (String, varsayılan: 'EG_IletkenSayisi'), `area_param` (String, varsayılan: 'EG_IletkenKesit_mm2'), `max_fill_pct` (Double, varsayılan: 40.0)

### `duct_aspect_ratio_check`

Dikdörtgen kanalların en/boy oranını kontrol eder (ASHRAE: ≤4:1). Dairesel kanallar atlanır. Çıktı: ValidationReport.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_ratio` (Double, varsayılan: 4.0), `severity` (String, varsayılan: 'WARNING')

### `fa_device_schedule`

Yangın alarm cihazlarını tip/kat/devre/loop bazında gruplar. Pre-check: EG_Loop + EG_Zone parametreleri family'de yoksa eklenmesi istenir.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `loop_param` (String, varsayılan: 'EG_Loop'), `zone_param` (String, varsayılan: 'EG_Zone'), `circuit_param` (String, varsayılan: 'EG_Circuit')
- **Çıktı alanları:** `device_type`, `level`, `zone`, `loop`, `circuit`, `quantity`

### `lighting_emergency_check`

Acil aydınlatma fixture'larının acil devreye bağlı olduğunu doğrular. Pre-check: EG_AcilAydinlatma parametresi fixture family'de yoksa eklenmesi istenir.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `emergency_param` (String, varsayılan: 'EG_AcilAydinlatma'), `emergency_panel_pattern` (String, varsayılan: 'EP')

### `panel_phase_balance_check`

Panel faz dengesizliğini kontrol eder (standart: ≤%10). Pre-check: panoda devre ve yük ataması var mı?

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_imbalance_pct` (Double, varsayılan: 10.0)
- **Çıktı alanları:** `panel_id`, `panel_name`, `phase_a_va`, `phase_b_va`, `phase_c_va`, `total_va`, `max_imbalance_pct`, `circuit_count`, `status`

### `sprinkler_head_schedule`

Sprinkler head'lerden tip/K-faktör/kapsama/kat/zone özeti üretir. Pre-check: K_Factor parametresi family'de yoksa eklenmesi istenir.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `k_factor_param` (String, varsayılan: 'K_Factor'), `coverage_param` (String, varsayılan: 'EG_KapsaAlan_m2'), `zone_param` (String, varsayılan: 'EG_Zone')
- **Çıktı alanları:** `type_name`, `k_factor`, `coverage_m2`, `zone`, `level`, `quantity`

### `valve_type_classify`

Boru fitting/aksesuarlarını isim keyword'üne göre gate/globe/ball/butterfly/check/relief olarak sınıflandırır.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `family_name`, `type_name`, `valve_class`, `system_type`, `diameter_mm`


## MEP Hesap

### `calc_ach_airflow`

ACH yöntemiyle gerekli hava debisini hesaplar. Input: oda listesi veya params (area_m2, height_m). Çıktı: airflow_cmh, airflow_cfm, fan seçenek önerileri.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `ach` (Double, varsayılan: 6.0), `mode` (String, varsayılan: 'normal'), `area_m2` (Double, varsayılan: 0), `height_m` (Double, varsayılan: 0)
- **Çıktı alanları:** `room_id`, `room_name`, `area_m2`, `height_m`, `volume_m3`, `ach`, `airflow_cmh`, `airflow_cfm`, `fan_option_4x_cmh`, `fan_option_6x_cmh`

### `calc_brick_quantity`

Duvar alanından tuğla adedi ve hacim hesabı. Input: duvar listesi veya params (area_m2, thickness_cm). Çıktı: brick_count, wall_volume_m3, mortar_m3, net_brick_m3.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `area_m2` (Double, varsayılan: 0), `thickness_cm` (Double, varsayılan: 19), `brick_type` (String, varsayılan: 'tam'), `mortar_ratio` (Double, varsayılan: 0.25), `waste_pct` (Double, varsayılan: 7.5)
- **Çıktı alanları:** `wall_id`, `wall_type`, `area_m2`, `thickness_cm`, `wall_volume_m3`, `mortar_m3`, `net_brick_m3`, `brick_count`, `waste_pct`, `total_with_waste`

### `calc_hazen_williams`

Hazen-Williams formülü ile boru sürtünme kaybı hesabı. P = 6.05×10⁵ × [Q^1.85 / (C^1.85 × d^4.87)]. Input: List<Pipe> veya params (flow_rate_lpm, pipe_diam_mm).

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `c_factor` (Double, varsayılan: 120.0), `pipe_length_m` (Double, varsayılan: 1.0), `flow_rate_lpm` (Double, varsayılan: 0), `pipe_diam_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `pipe_id`, `diam_mm`, `flow_lpm`, `c_factor`, `length_m`, `friction_loss_bar_per_m`, `total_loss_bar`, `velocity_m_s`

### `calc_room_lux`

Oda ortalama aydınlık seviyesini hesaplar (lux). Pre-check: lumen_param fixture family'de yoksa parametre eklenmesi istenir. Formül: avg_lux = (toplam_lumen × CU × MF) / alan_m2

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `lumen_param` (String, varsayılan: 'InitialIntensity'), `cu` (Double, varsayılan: 0.6), `mf` (Double, varsayılan: 0.8), `target_lux` (Double, varsayılan: 300.0)
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `area_m2`, `fixture_count`, `total_lumens`, `cu`, `mf`, `avg_lux`, `target_lux`, `status`


## MEP HVAC

### `assign_flow_to_terminals` 🔒

Her mahalin hesaplanan supply air flow degerini, icindeki supply hava terminallerine esit bolup yazar (terminal Flow parametresi). params: round_cfm_to — terminal debisi yuvarlama adimi, CFM (default: 5) flow_param_name — terminale yazilacak parametre adi (default: 'Flow') system_type_filter — terminal sistem turu (default: 'Supply Air') only_with_flow — debisi 0 olan mahalleri atla (default: true) Revit ic birimi ft3/s; CFM = ft3/s x 60. Space.CalculatedSupplyAirFlow okunur. AdnRme mantigi: terminal 'Flow' parametresi isimle aranan yazilabilir parametredir. Input: List<Element> (Space) veya bos (tum MEPSpace taranir). Cikti: space_id, space_name, space_number, terminal_count, supply_cfm, cfm_per_terminal, status

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Space)`
- **Parametreler:** `round_cfm_to` (Double, varsayılan: 'DefaultRoundCfmTo'), `flow_param_name` (String, varsayılan: 'Flow'), `system_type_filter` (String, varsayılan: 'Supply Air'), `only_with_flow` (Boolean, varsayılan: True)
- **Yazar:** `Flow`
- **Çıktı alanları:** `space_id`, `space_name`, `space_number`, `terminal_count`, `supply_cfm`, `cfm_per_terminal`, `status`

### `populate_space_param` 🔒

Mahallere hesaplanmis bir parametre degeri yazar (orn. 'CFM per SF'). params: target_param — yazilacak mahal parametresi adi (zorunlu) source — cfm_per_sf | supply_cfm | constant (default: cfm_per_sf) cfm_per_sf = supply_cfm / alan_ft2 supply_cfm = hesaplanan supply air (CFM) constant = params.value sabiti value — source=constant ise yazilacak sabit (zorunlu) skip_zero_area — alani 0 olan mahalleri atla (default: true) Input: List<Element> (Space) veya bos (tum MEPSpace). Cikti: space_id, space_name, space_number, value, status

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Space)`
- **Parametreler:** `target_param` (String, zorunlu), `source` (String, varsayılan: 'cfm_per_sf'), `skip_zero_area` (Boolean, varsayılan: True), `value` (Double, varsayılan: 0.0)
- **Çıktı alanları:** `space_id`, `space_name`, `space_number`, `value`, `status`

### `resize_diffuser_by_flow` 🔒

Terminalin mevcut debisine gore uygun diffuzor tipini (FamilySymbol) esik tablosundan secip ChangeTypeId ile uygular. params: family_name — diffuzor family adi (zorunlu) flow_param_name — debi okunacak parametre adi (default: 'Flow') thresholds — 'cfm:type' ciftleri, virgulle (zorunlu). Orn: '100:150x150,200:200x200,400:300x300'. Debi <= cfm olan ILK esigin type'i secilir; uymazsa en buyuk. system_type_filter — terminal sistem turu (default: 'Supply Air') Input: List<Element> (terminal) veya bos (tum supply terminaller). Cikti: terminal_id, current_cfm, old_type, new_type, changed, status

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (FamilyInstance/DuctTerminal)`
- **Parametreler:** `family_name` (String, zorunlu), `thresholds` (String, zorunlu), `flow_param_name` (String, varsayılan: 'Flow'), `system_type_filter` (String, varsayılan: 'Supply Air')
- **Okur:** `Flow`
- **Çıktı alanları:** `terminal_id`, `current_cfm`, `old_type`, `new_type`, `changed`, `status`


## MEP Koordinasyon

### `mep_region_count`

Kapalı bölge (Room/Area) içindeki MEP elemanlarını sayar ve uzunluk toplar. Yazma yapmaz. Oda-bazlı metraj / hakediş için özet üretir. Input : List<Element> (opsiyonel bölge listesi) — verilmezse region_category ile toplanır. params : region_category — bölge kaynağı (default: OST_Rooms; alternatif: OST_Areas) mep_categories — sayılacak MEP kategorileri (virgül, default: OST_PipeCurves,OST_DuctCurves,OST_CableTray) z_tolerance_mm — dikey tolerans; eleman merkezinin oda yüksekliği ± payı (default: 1500) Çıktı: List<Dictionary> — her satır bir bölge: region_id, region_name, region_number, level_name, area_m2, mep_count, total_length_m, pipe_count, duct_count, tray_count, by_category

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `region_category` (String, varsayılan: 'OST_Rooms'), `mep_categories` (String, varsayılan: 'OST_PipeCurves,OST_DuctCurves,OST_CableTray'), `z_tolerance_mm` (Int32, varsayılan: 1500)
- **Çıktı alanları:** `region_id`, `region_name`, `region_number`, `level_name`, `area_m2`, `mep_count`, `total_length_m`, `pipe_count`, `duct_count`, `tray_count`, `by_category`

### `mep_region_tag` 🔒

Kapalı bölge içindeki MEP elemanlarına bölge adı/numarasını parametre olarak yazar. DİKKAT: Model değişikliği yapar. Input : List<Element> (opsiyonel bölge listesi) — verilmezse region_category ile toplanır. params : region_category — bölge kaynağı (default: OST_Rooms) mep_categories — etiketlenecek MEP kategorileri (virgül, default: OST_PipeCurves,OST_DuctCurves,OST_CableTray) target_param — yazılacak parametre adı (default: Comments / Notlar) write_mode — 'name' | 'number' | 'name_number' (default: name_number) z_tolerance_mm — dikey tolerans (default: 1500) Çıktı: List<Dictionary> — region_id, region_name, tagged_count, skipped_count

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `region_category` (String, varsayılan: 'OST_Rooms'), `mep_categories` (String, varsayılan: 'OST_PipeCurves,OST_DuctCurves,OST_CableTray'), `target_param` (String, varsayılan: 'Comments'), `write_mode` (String, varsayılan: 'name_number'), `z_tolerance_mm` (Int32, varsayılan: 1500)
- **Çıktı alanları:** `region_id`, `region_name`, `tagged_count`, `skipped_count`

### `mep_straighten_apply` 🔒

Tespit edilen S-bendleri düzleştirir (dirsekleri + ara segmenti siler, ana hattı uzatır). DİKKAT: Model değişikliği yapar. Önce mep_straighten_scan ile inceleyin. Input : mep_straighten_scan çıktısı (List<Dictionary>) — input_from ile bağlanır. params : max_apply — uygulanacak maksimum düzeltme (0 = sınırsız, default: 0) Çıktı: List<Dictionary> — her satır bir uygulama sonucu: elbow_a_id, elbow_b_id, status (ok|skip|error), message

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `max_apply` (Int32, varsayılan: 0)
- **Çıktı alanları:** `elbow_a_id`, `elbow_b_id`, `status`, `message`

### `mep_straighten_scan`

MEP hatlarındaki çift-dirsek sapmalarını (S-bend / 翻弯) tespit eder. Yazma yapmaz — yalnızca rapor üretir. Düzleştirmek için mep_straighten_apply kullanın. Input : List<Element> (opsiyonel) — verilmezse categories ile toplanır. params : categories — taranacak kategoriler (virgül, default: OST_DuctCurves,OST_PipeCurves,OST_CableTray) min_offset_mm — minimum ofset (bundan küçük sapmalar yok sayılır, default: 5) max_offset_mm — maksimum ofset (bundan büyükse kasıtlı sayılır, default: 600) max_results — maksimum bulgu (default: 2000) Çıktı: List<Dictionary> — her satır bir S-bend: elbow_a_id, elbow_b_id, middle_ids, offset_mm, system_name, category, anchor_id, mover_id, center_x, center_y, center_z

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `categories` (String, varsayılan: 'OST_DuctCurves,OST_PipeCurves,OST_CableTray'), `min_offset_mm` (Int32, varsayılan: 5), `max_offset_mm` (Int32, varsayılan: 600), `max_results` (Int32, varsayılan: 2000)
- **Çıktı alanları:** `elbow_a_id`, `elbow_b_id`, `middle_ids`, `offset_mm`, `system_name`, `category`, `anchor_id`, `mover_id`, `center_x`, `center_y`, `center_z`


## MEP-Elektrik

### `elec_busbar_sizing`

Busbar kesit secimi (akim yogunlugu yontemi). params: akim_a — surekli akim A malzeme — cu|al (default cu) montaj — yatay|dikey (default yatay) faz_sayisi — 3 (default 3) guvenlik_pct — guvenlik marji % (default 20) Cu yogunluk: yatay 1.4-1.6 A/mm2, dikey 1.6-2.0 A/mm2. Cikti: oneri_kesit_mm2, oneri_boyut, akim_yogunlugu, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `akim_a` (Double, varsayılan: 0), `malzeme` (String, varsayılan: 'cu'), `montaj` (String, varsayılan: 'yatay'), `guvenlik_pct` (Double, varsayılan: 20.0), `faz_sayisi` (Int, önerilen)
- **Çıktı alanları:** `akim_a`, `akim_tasarim_a`, `malzeme`, `montaj`, `akim_yogunlugu`, `min_kesit_mm2`, `oneri_genislik_mm`, `oneri_kalinlik_mm`, `oneri_kesit_mm2`, `oneri_boyut`, `durum`

### `elec_check_circuit_assigned`

Elektrik elemanlarının bir devreye bağlı olup olmadığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elec_check_emergency_lighting`

Acil çıkış gerektiren alanlarda acil aydınlatma varlığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `fixtures` (List<Element>, önerilen), `emergency_keywords` (List<string>, önerilen)

### `elec_circuit_diff`

Mevcut modeli onceki devre snapshot'i ile karsilastirir, saha icin delta uretir: EKLENEN / SILINEN / DEGISEN devreler ve hangi alanin degistigi. params: baseline_path — karsilastirilacak snapshot JSON (zorunlu) output_path — delta raporu yolu, .json veya .html (opsiyonel) panel_filter — yalniz bu panoyu iceren devreler (opsiyonel) load_tolerance_va — VA farki bu esigin altindaysa degismedi say (default: 1) Devreler UniqueId ile eslestirilir (worksharing-dayanikli). Cikti: change_type, panel, circuit_number, field, old_value, new_value, detail.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `-`
- **Parametreler:** `baseline_path` (String, zorunlu), `output_path` (String, varsayılan: ''), `panel_filter` (String, varsayılan: ''), `load_tolerance_va` (Double, varsayılan: 1.0)
- **Çıktı alanları:** `change_type`, `panel`, `circuit_number`, `field`, `old_value`, `new_value`, `detail`

### `elec_circuit_snapshot`

Modeldeki tum elektrik devrelerinin mevcut durumunu JSON snapshot'a yazar. Onayli tasarim 'ani' olarak kullanilir; sonra elec_circuit_diff ile karsilastirilir. params: output_path — snapshot JSON yolu (zorunlu) panel_filter — yalniz bu pano adini iceren devreler (opsiyonel) Okunan: panel, devre no, yuk VA, kutup, gerilim, rating, bagli eleman UniqueId'leri. Cikti: circuit_id, panel, circuit_number, load_va, poles, status (+ JSON dosyasi).

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (ElectricalSystem)`
- **Parametreler:** `output_path` (String, zorunlu), `panel_filter` (String, varsayılan: '')
- **Çıktı alanları:** `circuit_id`, `panel`, `circuit_number`, `load_va`, `poles`, `status`

### `elec_conduit_calc_iec` 🔒

Conduit uzunlugunu okur, EG_DevreNo ile ElectricalSystem'i eslestirir, IEC 60364 kablo secimi + gerilim dusumu + conduit fill + kisa devre hesabi yapar ve cikti Shared Parameter'larini conduit'e yazar. params: voltage — hat gerilimi V (default 400; tek faz icin 230) vdrop_limit_pct — gerilim dusumu siniri % (default 5; aydinlatma 3) max_fill_pct — conduit doluluk siniri % (default 40, IEC 522.8) ampacity_table_path — kullanici JSON ampacity tablosu (opsiyonel override) only_with_circuit — EG_DevreNo bos conduit'leri atla (default true) GIRDI parametreleri conduit'ten okunur (EG_KurulumMetodu, EG_Yalitim, EG_Iletken, EG_OrtamSicaklik, EG_GruplamaAdet, EG_GucFaktoru, EG_YapmaPayi). Bossa makul varsayilanlar (C, PVC, Cu, 30C, 1, 0.8, 0) kullanilir. Cikti: conduit_id, devre, uzunluk_m, akim_a, kesit_mm2, gerilim_dusumu_pct, doluluk_pct, sigorta_a, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Conduit)`
- **Parametreler:** `voltage` (Double, varsayılan: 400.0), `vdrop_limit_pct` (Double, varsayılan: 5.0), `max_fill_pct` (Double, varsayılan: 40.0), `ampacity_table_path` (String, varsayılan: ''), `only_with_circuit` (Boolean, varsayılan: True)
- **Okur:** `EG_DevreNo`, `EG_GruplamaAdet`, `EG_GucFaktoru`, `EG_Iletken`, `EG_KurulumMetodu`, `EG_OrtamSicaklik`, `EG_Yalitim`, `EG_YapmaPayi`
- **Yazar:** `EG_DolulukYuzde`, `EG_FazKesit_mm2`, `EG_GerilimDusumu`, `EG_GerilimDusumuV`, `EG_HesapAkim`, `EG_HesapDurumu`, `EG_HesapTarihi`, `EG_KabloKesiti`, `EG_KabloUzunluk`, `EG_KisaDevreKesiti`, `EG_ModelUzunluk`, `EG_SigortaOneri`
- **Çıktı alanları:** `conduit_id`, `devre`, `uzunluk_m`, `akim_a`, `kesit_mm2`, `gerilim_dusumu_pct`, `doluluk_pct`, `sigorta_a`, `durum`

### `elec_conduit_schedule`

Conduit hesap sonuclarindan From-To-Devre-Kesit-Uzunluk-GerilimDusumu-Fill cetveli uretir (HTML veya CSV). params: output_path — cikti yolu, .html veya .csv (zorunlu) only_calculated — yalniz hesaplanmis conduit'ler (default true) Cikti: devre, kaynak, hedef, kesit, uzunluk_m, gerilim_dusumu_pct, doluluk_pct, sigorta_a, durum (+ dosya)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Conduit)`
- **Parametreler:** `output_path` (String, zorunlu), `only_calculated` (Boolean, varsayılan: True)
- **Okur:** `EG_DevreNo`, `EG_DolulukYuzde`, `EG_GerilimDusumu`, `EG_Hedef`, `EG_HesapAkim`, `EG_HesapDurumu`, `EG_KabloKesiti`, `EG_KabloUzunluk`, `EG_Kaynak`, `EG_SigortaOneri`
- **Çıktı alanları:** `devre`, `kaynak`, `hedef`, `kesit`, `uzunluk_m`, `gerilim_dusumu_pct`, `doluluk_pct`, `sigorta_a`, `durum`

### `elec_diversity_factor`

Cesitlilik faktoru ile panel/trafo talep guc hesabi. params: yukler — JSON liste: [{isim,kurulu_kw,kullanim_faktoru},...] cesitlilik_faktoru — genel cesitlilik (default 0.75) IEC 60364 / CIBSE: Aydinlatma 0.90, priz 0.40-0.60, HVAC 0.80. Cikti: toplam_kurulu_kw, toplam_talep_kw, talep_kva, talep_a, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `yukler` (String, varsayılan: '[]'), `cesitlilik_faktoru` (Double, varsayılan: 0.75), `gerilim_v` (Double, varsayılan: 415), `cos_phi` (Double, varsayılan: 0.85)
- **Çıktı alanları:** `yuk_sayisi`, `toplam_kurulu_kw`, `cesitlilik_faktoru`, `cos_phi`, `toplam_talep_kw`, `talep_kva`, `talep_a`, `yukler_detay`, `durum`

### `elec_earthing_validation`

Topraklama sistemi tipi QA (IEC 60364-4-41). Model elemanlarindaki EG_TopraklamaTipi parametresini okur ve alan tipine gore dogru topraklama sitemini oneriyor/dogruluyor. params: beklenen_tip — TN-S|TN-C-S|TT|IT (default TN-S) alan_tipi — konut|ticari|hastane|sanayi|data_merkezi Cikti: alan_tipi, beklenen_tip, oneri, gereksinimler, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `beklenen_tip` (String, varsayılan: 'TN-S'), `alan_tipi` (String, varsayılan: 'ticari')
- **Çıktı alanları:** `alan_tipi`, `beklenen_tip`, `oneri_tip`, `gereksinimler`, `durum`

### `elec_elv_device_qa` 🔒

ELV cihazlarinin montaj yuksekliklerini (FFL'den mm) kontrol eder. EG_ElvCihazTipi + EG_MontajYuksekligi parametreleri okunur. params: tolerance_mm — kabul toleransi mm (default 100) write_back — oneri degeri yaz (default false) Referans: MEP Electrical ELV Systems montaj standartlari. Cikti: element_id, cihaz_tipi, mevcut_mm, oneri_mm, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (Data/Security/Comm elemanları)`
- **Parametreler:** `tolerance_mm` (Int32, varsayılan: 100), `write_back` (Boolean, varsayılan: False)
- **Okur:** `EG_ElvCihazTipi`, `EG_MontajYuksekligi`
- **Yazar:** `EG_MontajYuksekligi_Oneri`
- **Çıktı alanları:** `element_id`, `cihaz_tipi`, `mevcut_mm`, `oneri_mm`, `fark_mm`, `aciklama`, `durum`

### `elec_emergency_circuit_qa`

Acil aydinlatma devre kalite kontrolu (TS EN 1838 / IEC 60598-2-22). params: alan_tipi — cikis_yolu|toplanma|yuksek_risk|genel armatur_aralik_m — armaturler arasi mesafe m ozerklik_sure_saat — ozerklik suresi saat devreye_girme_sn — devreye girme suresi sn bagimsiz_devre — bool (acil devresi bagimsiz mi) Gereksinim: cikis_yolu min 1lux, ozerklik >=1saat, devreye <=5sn. Cikti: alan_tipi, gereksinimler, kontroller, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `alan_tipi` (String, varsayılan: 'genel'), `armatur_aralik_m` (Double, varsayılan: 0), `ozerklik_sure_saat` (Double, varsayılan: 0), `devreye_girme_sn` (Double, varsayılan: 0), `bagimsiz_devre` (Boolean, varsayılan: False)
- **Çıktı alanları:** `alan_tipi`, `min_lux`, `max_aralik_m`, `min_ozerklik_saat`, `max_devreye_sn`, `kontroller`, `durum`

### `elec_generate_panel_schedule`

Panel yük dökümünü satır tablosuna dönüştürür (Excel export için)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elec_generator_load_calc`

Jenerator yuk hesabi ve kVA boyutlandirmasi. params: acil_yuk_kw — hayat guvenligi yukleri kW standby_yuk_kw — standby yukler kW (default 0) motor_baslama_kw — motor baslama darbesi kW (default 0) guc_faktoru — cos_phi (default 0.8) guvenlik_pct — guvenlik marji % (default 25) Standart: BS 7671, IEC 60034. Hayat guvenligi: %100 dahil. Cikti: toplam_kw, toplam_kva, oneri_jenerator_kva, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `acil_yuk_kw` (Double, varsayılan: 0), `standby_yuk_kw` (Double, varsayılan: 0), `motor_baslama_kw` (Double, varsayılan: 0), `guc_faktoru` (Double, varsayılan: 0.8), `guvenlik_pct` (Double, varsayılan: 25.0)
- **Çıktı alanları:** `acil_yuk_kw`, `standby_yuk_kw`, `motor_baslama_kw`, `toplam_kw`, `guc_faktoru`, `guvenlik_pct`, `tasarim_kw`, `tasarim_kva`, `oneri_jenerator_kva`, `durum`

### `elec_power_factor_check`

Guc faktoru analizi ve reaktif guc kompanzasyon onerisi. params: aktif_guc_kw — toplam aktif guc kW cos_phi_mevcut — mevcut guc faktoru cos_phi_hedef — hedef guc faktoru (default 0.95) gerilim_v — nominal V (default 415) Formul: Q_komp = P*(tan_phi1 - tan_phi2). Cikti: s_kva, q_kvar, q_komp_kvar, tasarruf_pct, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `aktif_guc_kw` (Double, varsayılan: 0), `cos_phi_mevcut` (Double, varsayılan: 0.8), `cos_phi_hedef` (Double, varsayılan: 0.95), `gerilim_v` (Double, varsayılan: 415)
- **Çıktı alanları:** `aktif_guc_kw`, `cos_phi_mevcut`, `cos_phi_hedef`, `s_mevcut_kva`, `q_mevcut_kvar`, `s_hedef_kva`, `q_hedef_kvar`, `q_kompanzasyon_kvar`, `akim_mevcut_a`, `akim_hedef_a`, `tasarruf_pct`, `durum`

### `elec_setup_conduit_params` 🔒

EGBIM elektrik hesap shared parametrelerini (sabit GUID) projeye yukler ve Conduit + ElectricalCircuit kategorilerine instance binding yapar. params: spf_path — SPF yolu (opsiyonel, default: mapping/EGBIM_ElektrikParams.txt) Bu op hesaptan ONCE bir kez calistirilir (altyapi kurulumu). Cikti: added, skipped, spf_path

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: '')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `elec_short_circuit_check`

Kisa devre akimi hesabi ve sigorta koordinasyon kontrolu (IEC 60364-4-43). params: kesit_mm2 — kablo kesiti mm2 uzunluk_m — kablo uzunlugu m gerilim_v — nominal V (default 415) malzeme — cu|al (default cu) kaynak_empedans — sebeke/trafo empedansi mOhm (default 35) sigorta_a — koruma cihazi anma akimi A sigorta_tip — mcb|mccb|fuse (default mcb) Cikti: isc_ka, z_total_mohm, kirilma_suresi_ms, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `kesit_mm2` (Double, varsayılan: 0), `uzunluk_m` (Double, varsayılan: 0), `gerilim_v` (Double, varsayılan: 415), `malzeme` (String, varsayılan: 'cu'), `kaynak_empedans` (Double, varsayılan: 35.0), `sigorta_a` (Double, varsayılan: 0), `sigorta_tip` (String, varsayılan: 'mcb')
- **Çıktı alanları:** `kesit_mm2`, `uzunluk_m`, `z_total_mohm`, `isc_ka`, `kirilma_suresi_ms`, `t_limit_ms`, `isil_dayanim`, `sigorta_a`, `durum`

### `elec_tray_hanger_spacing`

Revit modelindeki kablo tavalarinin aski araligini kontrol eder (IEC 61537). params: tav_tipi — ladder|perforated|solid_bottom|wire_mesh (default ladder) mevcut_aralik_m — modeldeki aski araligi m (0=Revit'ten oku) Limit: ladder 3.0m, perforated/solid 2.0m, wire_mesh 1.5m. Cikti: tray_id, tav_tipi, mevcut_aralik_m, max_aralik_m, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (CableTray)`
- **Parametreler:** `tav_tipi` (String, varsayılan: 'ladder'), `mevcut_aralik_m` (Double, varsayılan: 0)
- **Okur:** `EG_AskiAraligi`, `EG_TavaTipi`
- **Çıktı alanları:** `tray_id`, `tav_tipi`, `mevcut_aralik_m`, `max_aralik_m`, `durum`, `aciklama`

### `elec_tray_separation_check`

Revit modelinde guc ve ELV kablo tavalari arasindaki yatay ayrimi kontrol eder. Min 300mm ayrimi zorunludur (IEC 61537 / ELV separation rules). params: min_ayrim_mm — minimum ayrim mm (default 300) tolerans_mm — tolerans mm (default 50) EG_TavaTuru parametresi: 'guc' veya 'elv' degeri beklenir. Cikti: cift_id, guc_tray, elv_tray, mesafe_mm, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (CableTray EG_TavaTuru=guc/elv)`
- **Parametreler:** `min_ayrim_mm` (Int32, varsayılan: 300), `tolerans_mm` (Int32, varsayılan: 50)
- **Okur:** `EG_TavaTuru`
- **Çıktı alanları:** `cift_no`, `guc_tray_id`, `elv_tray_id`, `mesafe_mm`, `min_mm`, `durum`

### `elec_ups_autonomy_check`

UPS ozerklik suresi ve batarya kapasitesi hesabi (IEC 62040-3). params: yuk_kw — UPS korumali yuk kW hedef_sure_dak — hedef ozerklik dakika batarya_voltaj — batarya bank voltaji V (default 240) ups_verim — UPS verimi (default 0.94) batarya_verim — batarya dolum/bosalim verimi (default 0.85) ups_sinifi — VFI|VI|VFD (default VFI) Cikti: e_batarya_wh, kapasite_ah, oneri_batarya_ah, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `yuk_kw` (Double, varsayılan: 0), `hedef_sure_dak` (Double, varsayılan: 15.0), `batarya_voltaj` (Double, varsayılan: 240.0), `ups_verim` (Double, varsayılan: 0.94), `batarya_verim` (Double, varsayılan: 0.85), `ups_sinifi` (String, varsayılan: 'VFI')
- **Çıktı alanları:** `yuk_kw`, `hedef_sure_dak`, `e_batarya_wh`, `kapasite_ah`, `batarya_voltaj`, `oneri_batarya_ah`, `ups_sinifi`, `sinif_aciklama`, `durum`

### `elec_validate_lux_level`

Aydınlatma fikstürlerinin lux parametresini minimum değerle karşılaştırır

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_lux` (Double, varsayılan: 300.0)

### `elec_validate_panel_load`

Panel yük kapasitesini kontrol eder

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_load_kva` (Double, varsayılan: 100.0)

### `elec_voltage_drop_calc`

Kablo gerilim dusumu hesabi (IEC 60364-5-52). params: akim_a — faz akimi A uzunluk_m — kablo uzunlugu m kesit_mm2 — iletken kesiti mm2 (0=oto sec) gerilim_v — nominal gerilim V (default 415) faz_sayisi — 1 veya 3 (default 3) malzeme — cu|al (default cu) cos_phi — guc faktoru (default 0.85) max_dusumu_pct — izin verilen max % (default 3.0) yuk_tipi — aydinlatma|motor|kritik Limit: aydinlatma %3, motor %5, kritik %2.5 (IEC 60364-5-52). Cikti: kesit_mm2, dusumu_pct, dusumu_v, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `akim_a` (Double, varsayılan: 0), `uzunluk_m` (Double, varsayılan: 0), `kesit_mm2` (Double, varsayılan: 0), `gerilim_v` (Double, varsayılan: 415), `faz_sayisi` (Int32, varsayılan: 3), `malzeme` (String, varsayılan: 'cu'), `cos_phi` (Double, varsayılan: 0.85), `max_dusumu_pct` (Double, varsayılan: 3.0), `yuk_tipi` (String, varsayılan: 'genel')
- **Çıktı alanları:** `akim_a`, `uzunluk_m`, `kesit_mm2`, `faz_sayisi`, `malzeme`, `cos_phi`, `dusumu_pct`, `dusumu_v`, `max_dusumu_pct`, `yuk_tipi`, `durum`


## MEP-Koordinasyon

### `mep_lintel_place` 🔒

Genis MEP bosluklar icin otomatik lento (lintel) yerlestirme. Kaynak: script_py.py lento_modulu() C#'a donusturuldu. params: min_genislik_mm — lento gereken min genislik mm (default 600) binme_payi_mm — her iki taraf binme payi mm (default 200) beton_sinifi — C16|C20|C25|C30|C35|C40 (default C25) lento_family_isim— Structural Framing aile adi (default: ilk bulunan) mevcut_temizle — mevcut lentoları sil ve yenile (default false) Cikti: eklenen_lento, atlanan, hata_listesi

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `min_genislik_mm` (Double, varsayılan: 600.0), `binme_payi_mm` (Double, varsayılan: 200.0), `beton_sinifi` (String, varsayılan: 'C25'), `lento_family_isim` (String, varsayılan: ''), `mevcut_temizle` (Boolean, varsayılan: False)
- **Okur:** `KB_Durum`, `KB_Height`, `KB_Width`
- **Yazar:** `Beton_Sinifi`, `Bosluk_ID`, `Demir_Capi`, `Lento_Onay`
- **Çıktı alanları:** `eklenen_lento`, `atlanan`, `hata_sayisi`, `hata_listesi`, `beton_sinifi`, `durum`

### `mep_opening_bcf_export`

Sorunlu MEP bosluklar icin BCF 2.1 ihraç. Kaynak: script_py.py bcf_export() C#'a donusturuldu. params: output_path — cikti klasoru durum_filtresi — hangi durumlar dahil (varsayilan: RED,SORUN,GECERSIZ,REVIZYON,TAKVIYE) sadece_kritik — sadece REVIZYON ve GECERSIZ (default false) Cikti: .bcfzip dosya yolu, topic sayisi

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `output_path` (String, zorunlu), `sadece_kritik` (Boolean, varsayılan: False), `durum_filtresi` (String, varsayılan: 'RED,SORUN,GECERSIZ,REVIZYON,TAKVIYE,IPTAL')
- **Okur:** `KB_Disiplin`, `KB_Durum`, `KB_Height`, `KB_Width`
- **Çıktı alanları:** `bcf_dosyasi`, `topic_sayisi`, `toplam_bosluk`, `durum`

### `mep_opening_detect` 🔒

MEP eleman-duvar kesisim tespiti ve Kanal_Boslugu aile yerlesimi. Kaynak: script.py (Kanal Boslugu v8.4.1) C#'a donusturuldu. params: clearance_normal_mm — normal duvar gecis payi mm (default 60) clearance_fire_mm — yangin duvari gecis payi mm (default 100) min_size_mm — min bosluk boyutu mm (default 50) level_filter — seviye adi filtresi (bos=tumu) guncelle_mevcut — mevcut bosluklari guncelle (default true) Cikti: tespit edilen, yerlestirilen, guncellenen bosluk sayilari

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `clearance_normal_mm` (Double, varsayılan: 60.0), `clearance_fire_mm` (Double, varsayılan: 100.0), `min_size_mm` (Double, varsayılan: 50.0), `level_filter` (String, varsayılan: ''), `guncelle_mevcut` (Boolean, varsayılan: True)
- **Okur:** `KB_Kaynak_ID`
- **Yazar:** `KB_Aciklama`, `KB_Alt_Kot`, `KB_Disiplin`, `KB_Durum`, `KB_Duvar_Sinifi`, `KB_Height`, `KB_Kaynak_ID`, `KB_Son_Guncelleme`, `KB_Width`
- **Çıktı alanları:** `tespit_sayisi`, `yerlestirilen`, `guncellenen`, `toplam_bosluk`, `bosluklar`, `durum`

### `mep_opening_validate` 🔒

Gecersiz bosluk tespiti + EC-2 boyut siniflandirmasi. Kaynak: script_py.py (MEP Bosluk Yonetim v2.4) C#'a donusturuldu. params: ec2_kontrol — EC-2 boyut siniflandirmasi yap (default true) kiriş_mesafe_mm — kirişe yakinlik limiti mm (default 300) bosluk_arasi_mm — min bosluk arasi mesafe mm (default 200) durum_guncelle — KB_Durum'u otomatik guncelle (default true) EC-2: Yuvarlak d<350=OK, 350-600=TAKVİYE, >600=REVİZYON. Dikdortgen: d<200=OK, 200-400=TAKVİYE, 400-600=TAKVİYE+, >600=REVİZYON. Cikti: bosluk_id, genislik_mm, yukseklik_mm, ec2_seviye, kb_durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (GenericModel KB_* param)`
- **Parametreler:** `ec2_kontrol` (Boolean, varsayılan: True), `kiriş_mesafe_mm` (Double, varsayılan: 300.0), `bosluk_arasi_mm` (Double, varsayılan: 200.0), `durum_guncelle` (Boolean, varsayılan: True)
- **Okur:** `KB_Disiplin`, `KB_Durum`, `KB_Height`, `KB_Kaynak_ID`, `KB_Width`
- **Yazar:** `KB_Durum`, `KB_Son_Guncelleme`
- **Çıktı alanları:** `bosluk_id`, `genislik_mm`, `yukseklik_mm`, `disiplin`, `ec2_seviye`, `ec2_aciklama`, `kiris_yakini`, `gecersiz`, `kb_durum`, `yeni_durum`, `durum`


## MEP-Mekanik

### `duct_section_convert_apply` 🔒

duct_section_convert_preview çıktısındaki yeni kanal boyutlarını modele yazar. DİKKAT: Model değişikliği yapar. Bağlı kanal parçalarının (fitting) yeniden boyutlanması Revit tarafından otomatik denenir; bazı fitting'ler manuel düzeltme isteyebilir. Input : duct_section_convert_preview çıktısı (List<Dictionary>) — from ile bağlanır. params : skip_warnings — 'true' ise warning dolu satırları atla (default: false) Çıktı: List<Dictionary> — duct_id, status (ok|skip|error), message, new_w_mm, new_h_mm

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `skip_warnings` (String, varsayılan: 'false')
- **Çıktı alanları:** `duct_id`, `status`, `message`, `new_w_mm`, `new_h_mm`

### `duct_section_convert_preview`

Dikdörtgen kanalları eş-kesit (eşdeğer çap korumalı) yeniden boyutlandırır — HESAP. Bir boyut sabitlenir, diğeri aynı ASHRAE eşdeğer çapını koruyacak şekilde hesaplanır. Yazma yapmaz. Uygulamak için duct_section_convert_apply kullanın. Input : List<Element> (Duct) opsiyonel — verilmezse tüm dikdörtgen kanallar taranır. params : fix_dimension — sabitlenecek boyut: 'width' | 'height' (default: height) fixed_value_mm — sabit boyutun yeni değeri mm (zorunlu, >0) round_to_mm — hesaplanan boyutu yuvarlama adımı mm (default: 50) max_aspect_ratio — izin verilen max en/boy oranı; aşılırsa uyarı (default: 4.0) only_round_ducts — 'false' (default). true ise yuvarlak kanallar da dikdörtgene çevrilir. Çıktı: List<Dictionary> — her satır bir kanal: duct_id, system_name, old_w_mm, old_h_mm, old_de_mm, new_w_mm, new_h_mm, new_de_mm, aspect_ratio, de_error_pct, warning

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `fix_dimension` (String, varsayılan: 'height'), `fixed_value_mm` (Double, varsayılan: 0), `round_to_mm` (Double, varsayılan: 50), `max_aspect_ratio` (Double, varsayılan: 4.0), `only_round_ducts` (String, önerilen)
- **Çıktı alanları:** `duct_id`, `system_name`, `old_w_mm`, `old_h_mm`, `old_de_mm`, `new_w_mm`, `new_h_mm`, `new_de_mm`, `aspect_ratio`, `de_error_pct`, `warning`

### `mep_ach_by_room_type` 🔒

Oda tipine gore ACH referans tablosu ve Revit oda dogrulamasi. params: oda_tipi — ameliyathane|ofis|hastane|mutfak|banyo vb. mevcut_ach — modeldeki ACH degeri (0=hesap modu) oda_alani_m2 — oda alani (dogrulama icin) oda_yuksekligi_m — oda yuksekligi m write_back — EG_ACH_Oneri'ye yaz (default false) Kaynak: ASHRAE 62.1, ASHRAE 170, EN 16798, TR Saglik Bak. Cikti: min_ach, max_ach, tipik_ach, gerekli_m3h, standart, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `oda_tipi` (String, varsayılan: 'ofis'), `mevcut_ach` (Double, varsayılan: 0), `oda_alani_m2` (Double, varsayılan: 0), `oda_yuksekligi_m` (Double, varsayılan: 3.0), `write_back` (Boolean, varsayılan: False)
- **Yazar:** `EG_ACH_Oneri`
- **Çıktı alanları:** `oda_tipi`, `min_ach`, `tipik_ach`, `max_ach`, `mevcut_ach`, `alan_m2`, `hacim_m3`, `gerekli_m3h`, `standart`, `durum`

### `mep_ahu_selection`

AHU secim kriterleri: debi, sogutma/isitma kapasitesi, fan gucu, filtre sinifi. params: toplam_debi_m3h — AHU toplam hava debisi m3/saat sogutma_kw — sogutma kapasitesi kW isitma_kw — isitma kapasitesi kW (default 0) esp_pa — external static pressure Pa (default 300) filtre_sinifi — F5|F7|F9|H13|H14 (default F7) alan_tipi — ofis|hastane|ameliyathane|temiz_oda|genel Cikti: fan_kw, ahu_boyutu, filtre_oneri, sogutma_kapasitesi, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `toplam_debi_m3h` (Double, varsayılan: 0), `sogutma_kw` (Double, varsayılan: 0), `isitma_kw` (Double, varsayılan: 0), `esp_pa` (Double, varsayılan: 300.0), `filtre_sinifi` (String, varsayılan: 'F7'), `alan_tipi` (String, varsayılan: 'genel')
- **Çıktı alanları:** `toplam_debi_m3h`, `esp_pa`, `fan_kw`, `fan_m3s`, `sogutma_kw`, `isitma_kw`, `filtre_mevcut`, `filtre_oneri`, `filtre_durum`, `ahu_boyut_tahmini`, `alan_tipi`, `durum`

### `mep_air_terminal_space_map`

Hava terminallerini bağlı oldukları mekân ile eşleştirir

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_chiller_cop_check`

Chiller COP ve enerji verimliligi kontrolu (ASHRAE 90.1-2022). params: chiller_tipi — hava_sogutmali|su_sogutmali_vida| su_sogutmali_turbo|vrf|isi_pompasi|absorpsiyonlu mevcut_cop — chillerin COP degeri sogutma_kapasitesi_kw — nominal sogutma kW calisma_saati — yillik calisma saati (default 2000) elektrik_birim_fiyat — TL/kWh (default 3.5) Cikti: min_cop, iyi_cop, mevcut_cop, yillik_maliyet, tasarruf_pct, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `chiller_tipi` (String, varsayılan: 'hava_sogutmali'), `mevcut_cop` (Double, varsayılan: 0), `sogutma_kapasitesi_kw` (Double, varsayılan: 0), `calisma_saati` (Double, varsayılan: 2000.0), `elektrik_birim_fiyat` (Double, varsayılan: 3.5)
- **Çıktı alanları:** `chiller_tipi`, `teknoloji`, `mevcut_cop`, `min_cop`, `iyi_cop`, `cop_durum`, `elektrik_kw`, `yillik_maliyet_tl`, `tasarruf_pct`, `standart`, `durum`

### `mep_cooling_load_room`

Oda bazli sogutma yuku hesabi (ASHRAE CLTD basitlestirilmis). params: alan_m2 — oda alani m2 cam_alani_m2 — toplam cam alani m2 yon — kuzey|guney|dogu|bati|cati (default guney) kisi_sayisi — kisi sayisi ekipman_w — ekipman gucu W aydinlatma_w_m2 — aydinlatma yogunlugu W/m2 (default 12) dis_sicaklik_c — dis tasarim sicakligi C (default 36) ic_sicaklik_c — ic tasarim sicakligi C (default 24) Cikti: gunes_yuku_w, ic_yuk_w, toplam_sogutma_kw, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `alan_m2` (Double, varsayılan: 0), `cam_alani_m2` (Double, varsayılan: 0), `yon` (String, varsayılan: 'guney'), `kisi_sayisi` (Int32, varsayılan: 0), `ekipman_w` (Double, varsayılan: 0), `aydinlatma_w_m2` (Double, varsayılan: 12.0), `dis_sicaklik_c` (Double, varsayılan: 36.0), `ic_sicaklik_c` (Double, varsayılan: 24.0)
- **Çıktı alanları:** `alan_m2`, `cam_alani_m2`, `yon`, `gunes_yuku_w`, `duvar_iletim_w`, `ic_yuk_w`, `toplam_w`, `toplam_sogutma_kw`, `yogunluk_w_m2`, `durum`

### `mep_fresh_air_rate_check`

Taze hava orani kontrolu (ASHRAE 62.1 / EN 16798-1). params: alan_m2 — kondisyonlu alan m2 kisi_sayisi — tasarim kisi sayisi alan_tipi — ofis|sinif|restoran|hastane|konut|toplanma mevcut_m3h — modeldeki taze hava debisi m3/saat (0=hesap) ASHRAE 62.1 Rp+Ra yontemi. Cikti: gerekli_l_s, gerekli_m3h, kisi_basi_l_s, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `alan_m2` (Double, varsayılan: 0), `kisi_sayisi` (Int32, varsayılan: 0), `alan_tipi` (String, varsayılan: 'ofis'), `mevcut_m3h` (Double, varsayılan: 0)
- **Çıktı alanları:** `alan_m2`, `kisi_sayisi`, `alan_tipi`, `rp_l_s_kisi`, `ra_l_s_m2`, `gerekli_l_s`, `gerekli_m3h`, `kisi_basi_l_s`, `mevcut_m3h`, `standart`, `durum`

### `mep_hepa_filter_qa`

HEPA filtre uygunluk QA (EN 1822-1 / ISO 29463). params: filtre_sinifi — H13|H14 alan_tipi — ameliyathane|temiz_oda|yogun_bakim|hastane mevcut_dp_pa — mevcut basınc dususu Pa (0=kontrol atla) montaj_tipi — gel_seal|mekanik|dop_test son_test_tarihi — DOP/PAO test tarihi (YYYY-MM, kontrol icin) Cikti: gerekli_sinif, mevcut_sinif, dp_durum, montaj_durum, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `filtre_sinifi` (String, varsayılan: 'H14'), `alan_tipi` (String, varsayılan: 'ameliyathane'), `mevcut_dp_pa` (Double, varsayılan: 0), `montaj_tipi` (String, varsayılan: 'gel_seal'), `son_test_tarihi` (String, varsayılan: '')
- **Çıktı alanları:** `alan_tipi`, `gerekli_sinif`, `mevcut_sinif`, `sinif_durum`, `dp_pa`, `dp_durum`, `montaj_tipi`, `montaj_durum`, `test_tarihi`, `test_durum`, `durum`

### `mep_hvac_heat_load_calc`

Basit isi yuku hesabi (TS 825 / ASHRAE). Kabuk + ic yuk + infiltrasyon + taze hava. params: alan_m2 — kondisyonlu alan m2 tavan_yuksekligi_m — tavan yuksekligi m (default 3.0) dis_sicaklik_c — dis tasarim sicakligi C (yaz) ic_sicaklik_c — ic tasarim sicakligi C (default 24) u_deger_ortalama — ortalama U degeri W/m2K (default 0.5) ic_yuk_w_m2 — ic yuk yogunlugu W/m2 (default 30) kisi_sayisi — kisi sayisi (default 0) taze_hava_m3h — taze hava debisi m3/saat (default 0) Cikti: toplam_w, sogutma_kw, sogutma_tr, kabuk_w, ic_yuk_w, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `alan_m2` (Double, varsayılan: 0), `tavan_yuksekligi_m` (Double, varsayılan: 3.0), `dis_sicaklik_c` (Double, varsayılan: 36.0), `ic_sicaklik_c` (Double, varsayılan: 24.0), `u_deger_ortalama` (Double, varsayılan: 0.5), `ic_yuk_w_m2` (Double, varsayılan: 30.0), `kisi_sayisi` (Int32, varsayılan: 0), `taze_hava_m3h` (Double, varsayılan: 0)
- **Çıktı alanları:** `alan_m2`, `delta_t_c`, `kabuk_w`, `ic_yuk_w`, `infiltrasyon_w`, `taze_hava_w`, `toplam_w`, `sogutma_kw`, `sogutma_tr`, `tasarim_kw`, `tasarim_tr`, `durum`

### `mep_hvac_zone_balance` 🔒

HVAC zon debi denge kontrolu (ASHRAE 111). Her zonda terminal debilerinin toplamini zon tasarim debiyle karsilastirir. params: tolerans_pct — kabul sapma yuzde (default 10) debi_param — terminal debi parametresi adi (default EG_Debi_m3h) Revit: Space/Zone bazinda hava terminalleri toplanir. Cikti: zon_id, zon_adi, tasarim_m3h, toplam_terminal_m3h, sapma_pct, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (MEPSpaces)`
- **Parametreler:** `tolerans_pct` (Double, varsayılan: 10.0), `debi_param` (String, varsayılan: 'EG_Debi_m3h')
- **Okur:** `EG_TasarimDebi_m3h`, `EG_TerminalToplam_m3h`
- **Çıktı alanları:** `zon_id`, `zon_adi`, `tasarim_m3h`, `toplam_terminal_m3h`, `sapma_pct`, `durum`

### `mep_pressurization_check`

Oda basinclandirma kontrolu (TR Saglik Bak. / ASHRAE 170 / EN 16798). params: oda_tipi — ameliyathane|steril_koridor|on_hazirlik| genel_koridor|kirli_alan|banyo_tuvalet| mutfak|izolasyon_odasi|temiz_oda mevcut_basinc_pa — modeldeki basinc Pa (pozitif/negatif) tolerans_pa — kabul toleransi Pa (default 2) Cikti: hedef_basinc_pa, mevcut_basinc_pa, tip, standart, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `oda_tipi` (String, varsayılan: 'genel_koridor'), `mevcut_basinc_pa` (Double, varsayılan: 'double.NaN'), `tolerans_pa` (Double, varsayılan: 2.0)
- **Çıktı alanları:** `oda_tipi`, `hedef_basinc_pa`, `mevcut_basinc_pa`, `basinclandirma_tipi`, `tolerans_pa`, `standart`, `durum`

### `mep_static_pressure_calc`

Kanal statik basinc ve fan ESP hesabi. params: ana_hat_uzunlugu_m — ana kanal hatti uzunlugu m debi_m3h — kanal debisi m3/saat boyut_mm — kanal buyuk boyutu mm (kare/dikdortgen) dp_filtre_pa — filtre basınc kaybi Pa (default 150) dp_serpantin_pa — serpantin basınc kaybi Pa (default 200) dp_terminal_pa — terminal birim basınc kaybi Pa (default 50) boru_katsayi_pa_m — kanal surtuenme katsayisi Pa/m (default 1.0) ASHRAE: Ana hat R=0.8-1.2 Pa/m. ESP=hat+filtre+serpantin+terminal. Cikti: r_pa_m, hat_kaybi_pa, toplam_esp_pa, fan_kw, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `ana_hat_uzunlugu_m` (Double, varsayılan: 0), `debi_m3h` (Double, varsayılan: 0), `boyut_mm` (Double, varsayılan: 400), `dp_filtre_pa` (Double, varsayılan: 150.0), `dp_serpantin_pa` (Double, varsayılan: 200.0), `dp_terminal_pa` (Double, varsayılan: 50.0), `boru_katsayi_pa_m` (Double, varsayılan: 1.0)
- **Çıktı alanları:** `ana_hat_uzunlugu_m`, `debi_m3h`, `hiz_m_s`, `hiz_durum`, `r_pa_m`, `hat_kaybi_pa`, `fitting_kaybi_pa`, `dp_filtre_pa`, `dp_serpantin_pa`, `dp_terminal_pa`, `toplam_esp_pa`, `fan_kw`, `durum`

### `mep_validate_duct_size`

Kanal boyutunun sistem tipine göre minimum sınırı sağladığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_size_mm` (Double, varsayılan: 100.0)

### `mep_validate_duct_slope`

Pissu/yoğuşma kanalı eğim değerini kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_slope_pct` (Double, varsayılan: 0.5)

### `mep_validate_duct_velocity`

Havalandırma kanalı hız değerini max sınırla karşılaştırır

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_velocity_m_s` (Double, varsayılan: 6.0)

### `mep_validate_space_hvac_zone`

Mekânların HVAC bölge ataması olup olmadığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok


## MEP-Sıhhi

### `plumbing_assign_units` 🔒

Armaturlere EN 12056 DU ve EN 806 LU degerlerini atar (EG_ArmaturTipi'nden tablo eslestirmesiyle). Tip bossa armatur adindan tahmin edilir. params: overwrite — mevcut DU/LU degerlerini ez (default false) Input: List<Element> (PlumbingFixture) veya bos (tum armaturler). Cikti: fixture_id, tip, du, lu_soguk, lu_sicak, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (PlumbingFixture)`
- **Parametreler:** `overwrite` (Boolean, varsayılan: False)
- **Okur:** `EG_ArmaturTipi`, `EG_DU`
- **Yazar:** `EG_ArmaturTipi`, `EG_DU`, `EG_LU_Sicak`, `EG_LU_Soguk`
- **Çıktı alanları:** `fixture_id`, `tip`, `du`, `lu_soguk`, `lu_sicak`, `durum`

### `plumbing_calc_en` 🔒

Boru bolumlerine bagli armaturlerin DU/LU'larini toplar, EN 12056 (drenaj Qww) ve EN 806 (su QD) hesabi yapar, cap secer, drenaj icin dolum/egim/hiz kontrolu yapar ve sonuclari boruya yazar. params: pipe_role — auto|drenaj|su (default auto: sistem adindan tespit) min_slope_pct — drenaj min egim % (default 1.0) max_fill_pct — drenaj dolum siniri % (default 70) capacity_table_path — kullanici DN kapasite tablosu JSON (opsiyonel) Drenaj: Qww=K×√ΣDU (K bina kullanimindan). Su: ΣLU→QD→DN. Cikti: pipe_id, rol, sistem, toplam_du_lu, debi_l_s, dn_mm, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Pipe)`
- **Parametreler:** `pipe_role` (String, varsayılan: 'auto'), `min_slope_pct` (Double, varsayılan: 1.0), `max_fill_pct` (Double, varsayılan: 70.0), `capacity_table_path` (String, varsayılan: '')
- **Okur:** `EG_BinaKullanim`, `EG_DU`, `EG_DrenajSistem`, `EG_LU_Sicak`, `EG_LU_Soguk`, `EG_SuSistem`
- **Yazar:** `EG_AkisHizi`, `EG_AtikDebi_Qww`, `EG_BoruKapasite`, `EG_DolulukOrani`, `EG_EgimYuzde`, `EG_HesapDurumu`, `EG_HesapTarihi`, `EG_OneriCap_DN`, `EG_TasarimDebi_QD`, `EG_ToplamDU`, `EG_ToplamLU`
- **Çıktı alanları:** `pipe_id`, `rol`, `sistem`, `toplam_du_lu`, `debi_l_s`, `dn_mm`, `durum`

### `plumbing_calc_flow_rate`

Tesisat armatürleri için toplam debi hesabı yapar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `plumbing_check_fixture_room_assigned`

Tesisat armatürlerinin bir odaya atanıp atanmadığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `plumbing_dead_leg_check`

Sihhi tesisat borularinda olu hat (dead leg) uzunlugunu kontrol eder. Lejyonella riski: ASHRAE 188 / WHO Legionella Guidelines. params: max_uzunluk_katsayi — max uzunluk = katsayi x DN_m (default 20) sicak_su_sicaklik_c — min sicak su sicakligi C (default 60) Input: List<Element> (Pipe) veya bos (tum pissu/su borulari). Kural: DN15->300mm, DN20->450mm, DN25->600mm, DN32+->900mm. Cikti: pipe_id, sistem, cap_mm, uzunluk_mm, max_mm, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Pipe) veya boş`
- **Parametreler:** `max_uzunluk_katsayi` (Double, varsayılan: 20.0), `sicak_su_sicaklik_c` (Double, önerilen)
- **Çıktı alanları:** `pipe_id`, `sistem`, `cap_mm`, `uzunluk_mm`, `max_mm`, `oran`, `durum`

### `plumbing_demand_lpd`

Bina tipine ve kisi sayisina gore gunluk su talep hesabi (LPD). params: bina_tipi — konut|ofis|hastane|otel_3yildiz|otel_5yildiz| okul|restoran|alisveris|sanayi|spor kisi_sayisi — toplam kullanici sayisi lpd_override — 0=tablo, >0=kullanici girir (L/kisi/gun) Kaynak: EN 806-1, TR CSB 2026. Cikti: gunluk_talep_lt, gunluk_talep_m3, lpd_tipik

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_tipi` (String, zorunlu), `kisi_sayisi` (Int32, varsayılan: 0), `lpd_override` (Double, varsayılan: 0)
- **Çıktı alanları:** `bina_tipi`, `kisi_sayisi`, `lpd_min`, `lpd_max`, `lpd_tipik`, `gunluk_talep_lt`, `gunluk_talep_m3`, `durum`

### `plumbing_fixture_clearance` 🔒

Revit modelindeki sihhi armaturlerin montaj yuksekliklerini (FFL'den mm) referans tabloya gore dogrular. EG_MontajYuksekligi parametresi okunur. params: tolerance_mm — kabul toleransi mm (default 50) write_back — oneri degeri EG_MontajYuksekligi_Oneri'ye yaz (default false) Referans: IMPORTANT INTERIOR DETAILS standart olculeri. Cikti: fixture_id, armatur_tipi, mevcut_mm, oneri_mm, fark_mm, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (tüm PlumbingFixture)`
- **Parametreler:** `tolerance_mm` (Int32, varsayılan: 50), `write_back` (Boolean, varsayılan: False)
- **Okur:** `EG_ArmaturTipi`, `EG_MontajYuksekligi`
- **Yazar:** `EG_MontajYuksekligi_Oneri`
- **Çıktı alanları:** `fixture_id`, `armatur_tipi`, `mevcut_mm`, `oneri_mm`, `fark_mm`, `aciklama`, `durum`

### `plumbing_hwc_return`

Sicak su sirkulasyon (HWC) debi ve donus hatti boru boyutu hesabi. params: isi_kaybi_w — toplam boru isi kaybi W besleme_sicaklik_c — besleme sicakligi C (default 65) donus_sicaklik_c — donus sicakligi C (default 55) ic_cap_mm — donus borusu ic capi mm (dogrulama) Kural: Qcirc=Q/(rho*Cp*DT). Min donus >=50C (lejyonella). Cikti: qcirc_l_s, qcirc_l_h, hiz_m_s, oneri_cap_mm, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `isi_kaybi_w` (Double, varsayılan: 0), `besleme_sicaklik_c` (Double, varsayılan: 65.0), `donus_sicaklik_c` (Double, varsayılan: 55.0), `ic_cap_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `isi_kaybi_w`, `besleme_sicaklik_c`, `donus_sicaklik_c`, `delta_t_c`, `qcirc_l_s`, `qcirc_l_h`, `qcirc_m3_s`, `hiz_m_s`, `hiz_durum`, `oneri_cap_mm`, `lejyonella_durum`, `durum`

### `plumbing_peak_demand`

Konut/birim bazli pik talep hesabi (Qp = birim_pik x N). params: daire_sayisi — toplam daire/birim sayisi birim_pik_l_dak — birim pik debisi L/dak (default 8, konut) hesap_turu — konut|ofis|karma Cikti: qp_l_dak, qp_l_s, riser_dn_mm

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `daire_sayisi` (Int32, varsayılan: 0), `birim_pik_l_dak` (Double, varsayılan: -1), `hesap_turu` (String, varsayılan: 'konut')
- **Çıktı alanları:** `daire_sayisi`, `birim_pik_l_dak`, `hesap_turu`, `qp_l_dak`, `qp_l_s`, `qp_m3_s`, `riser_dn_mm`, `durum`

### `plumbing_pressure_zone`

Yuksek bina basinc zonlarini, PRV konumlarini ve statik basinci hesaplar. params: kat_sayisi — toplam kat sayisi kat_yuksekligi_m — kat yuksekligi m (default 3.0) max_basinc_bar — zon max basinci bar (default 3.5) min_basinc_bar — min servis basinci bar (default 1.0) Kural: max 3.5 bar/zon, her zon max 10-11 kat. PRV zon girisine. Cikti: zon_sayisi, zonlar listesi (kat aralik + PRV + basinc)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `kat_sayisi` (Int32, varsayılan: 0), `kat_yuksekligi_m` (Double, varsayılan: 3.0), `max_basinc_bar` (Double, varsayılan: 3.5), `min_basinc_bar` (Double, önerilen)
- **Çıktı alanları:** `kat_sayisi`, `zon_sayisi`, `max_kat_per_zon`, `toplam_statik_bar`, `zonlar`, `durum`

### `plumbing_pump_hp_calc`

Pompa hidrolik/fren/motor guc hesabi + IEC standart motor secimi. params: debi_m3_s — hacimsel debi m3/s toplam_yuk_m — toplam manometrik yuk metre pompa_verim — 0.60-0.80 (default 0.70) motor_verim — 0.85-0.95 (default 0.90) servis_faktor — 1.15-1.25 (default 1.15) Formul: Ph=rho*g*Q*H/1000, Pb=Ph/np, Pm=Pb/nm*SF. Cikti: ph_kw, pb_kw, pm_kw, standart_motor_kw

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `debi_m3_s` (Double, varsayılan: 0), `toplam_yuk_m` (Double, varsayılan: 0), `pompa_verim` (Double, varsayılan: 0.7), `motor_verim` (Double, varsayılan: 0.9), `servis_faktor` (Double, varsayılan: 1.15)
- **Çıktı alanları:** `debi_m3_s`, `toplam_yuk_m`, `ph_kw_hidrolik`, `pb_kw_fren`, `pm_kw_motor`, `standart_motor_kw`, `durum`

### `plumbing_schedule`

Sihhi tesisat hesap sonuclarindan cetvel uretir (HTML/CSV): sistem, rol, DU/LU, debi, DN, dolum, egim/hiz, durum. params: output_path — cikti yolu, .html veya .csv (zorunlu) only_calculated — yalniz hesaplanmis borular (default true) Cikti: sistem, rol, debi_l_s, dn_mm, durum (+ dosya)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `-`
- **Parametreler:** `output_path` (String, zorunlu), `only_calculated` (Boolean, varsayılan: True)
- **Okur:** `EG_AkisHizi`, `EG_AtikDebi_Qww`, `EG_DolulukOrani`, `EG_EgimYuzde`, `EG_HesapDurumu`, `EG_OneriCap_DN`, `EG_TasarimDebi_QD`, `EG_ToplamDU`, `EG_ToplamLU`
- **Çıktı alanları:** `sistem`, `rol`, `debi_l_s`, `dn_mm`, `durum`

### `plumbing_setup_params` 🔒

EGBIM sihhi tesisat hesap shared parametrelerini (sabit GUID) projeye yukler ve PlumbingFixtures + PipeCurves kategorilerine instance binding yapar. params: spf_path — SPF yolu (opsiyonel, default: mapping/EGBIM_SihhiParams.txt) Hesaptan ONCE bir kez calistirilir (altyapi kurulumu). Cikti: added, skipped, spf_path

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: '')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `plumbing_static_pressure`

Statik basinc hesabi: P = rho * g * h. OHT beslemesi, PRV giriş basinci, bodrum kat max basinc kontrolu icin kullanilir. params: yukseklik_m — su kolonu yuksekligi metre sicaklik_c — su sicakligi C (default 20) giris_basinc_bar — hat giris basinci bar (default 0) Cikti: statik_basinc_pa, statik_basinc_bar, toplam_basinc_bar, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `yukseklik_m` (Double, varsayılan: 0), `sicaklik_c` (Double, varsayılan: 20.0), `giris_basinc_bar` (Double, varsayılan: 0)
- **Çıktı alanları:** `yukseklik_m`, `sicaklik_c`, `su_yogunlugu`, `giris_basinc_bar`, `statik_basinc_pa`, `statik_basinc_bar`, `toplam_basinc_bar`, `toplam_basinc_kpa`, `durum`

### `plumbing_storage_tank_size`

Gunluk talepten suction (yeralti) ve OHT (cati) depo hacmini hesaplar. params: gunluk_talep_m3 — m3/gun tampon_katsayi — 1.1-1.2 (default 1.15) suction_oran — 0.5-0.6 (default 0.55) Kural: Toplam = talep x tampon. OHT efektif derinlik 1.0-1.5m. Cikti: toplam_depo_m3, suction_tank_m3, oht_tank_m3

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `gunluk_talep_m3` (Double, varsayılan: 0), `tampon_katsayi` (Double, varsayılan: 1.15), `suction_oran` (Double, varsayılan: 0.55)
- **Çıktı alanları:** `gunluk_talep_m3`, `tampon_katsayi`, `toplam_depo_m3`, `suction_tank_m3`, `oht_tank_m3`, `oht_efektif_derinlik_m`, `oht_taban_alani_m2`, `suction_not`, `durum`

### `plumbing_validate_connector_diameter`

Tesisat armatürü bağlantı çapını standart değerle karşılaştırır

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `expected_diameter_mm` (Double, varsayılan: 50.0), `tolerance_mm` (Double, varsayılan: 5.0)

### `plumbing_validate_pipe_slope`

Pissu borularında minimum %1 eğim kontrolü yapar

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_slope_pct` (Double, varsayılan: 1.0)

### `plumbing_validate_system_separation`

Sıcak/soğuk su hatlarının sistem tipine göre ayrıldığını kontrol eder

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `plumbing_water_velocity`

Boru ic capina gore su akis hizini hesaplar ve EN 806-3 limitlerini kontrol eder. params: debi_l_s — akis debisi L/s ic_cap_mm — boru ic capi mm boru_tipi — soguk|sicak|atik (default soguk) Sinirlar: soguk max 2.0 m/s, sicak max 1.5 m/s, atik max 3.0 m/s, min 0.6 m/s. Cikti: hiz_m_s, alan_mm2, durum, oneri_cap_mm

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `debi_l_s` (Double, varsayılan: 0), `ic_cap_mm` (Double, varsayılan: 0), `boru_tipi` (String, varsayılan: 'soguk')
- **Çıktı alanları:** `debi_l_s`, `ic_cap_mm`, `boru_tipi`, `alan_mm2`, `hiz_m_s`, `v_min_m_s`, `v_max_m_s`, `oneri_cap_mm`, `durum`


## Mimari

### `arch_apply_view_template` 🔒

İsim deseni eşleşen view'lara template uygular

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `template_name` (String, varsayılan: ''), `view_name_contains` (String, varsayılan: '')

### `arch_check_fire_rating_continuity`

Yangın bölgesi duvarlarında yangın direnci rating'inin sürekliliğini kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_fire_rating` (String, varsayılan: '60')

### `arch_check_material_assigned`

Duvar/döşeme/çatı katmanlarında malzeme atanıp atanmadığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `arch_check_windowless_rooms`

Pencere/doğal aydınlatma gerektiren odalarda pencere varlığını kontrol eder

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `windows` (List<Element>, önerilen), `requires_window_keywords` (List<string>, önerilen)

### `arch_renumber_doors` 🔒

Kapıları kat ve oda sırasına göre yeniden numaralandırır

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `prefix` (String, varsayılan: 'K')

### `arch_sheets_from_data` 🔒

Satır listesinden Revit sheet'leri oluşturur (no/isim/view template)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `arch_validate_accessibility`

Kapı net açıklığını erişilebilirlik standardına göre kontrol eder (min 850mm)

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_width_mm` (Double, varsayılan: 850.0)

### `arch_validate_ceiling_height`

Oda tavan yüksekliklerini minimum değerle karşılaştırır

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_height_mm` (Double, varsayılan: 2400.0)

### `arch_validate_room_area`

Oda alanlarını minimum alan gereksinimi ile karşılaştırır

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_area_m2` (Double, varsayılan: 0.0)

### `arch_validate_room_naming`

Oda isimlerinin belirlenen kurala uygunluğunu kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `allowed_keywords` (List<string>, önerilen)


## Modelleme

### `create_3d_view` 🔒

Eleman listesine fit section box ile 3D view oluşturur

- **Çıktı:** `Element`
- **Girdi:** `List<Element>`
- **Parametreler:** `view_name` (String, varsayılan: '$"EGBIMOTO_3D_{DateTime.Now:HHmmss}"'), `padding_mm` (Double, varsayılan: 500)

### `create_sheet` 🔒

Yeni pafta (ViewSheet) oluşturur

- **Çıktı:** `Element`
- **Girdi:** `—`
- **Parametreler:** `sheet_number` (String, zorunlu), `sheet_name` (String, zorunlu), `title_block_name` (String, varsayılan: '')

### `mirror_element` 🔒

Eleman listesini X veya Y eksenine göre aynalar

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `axis` (String, varsayılan: 'Y'), `pivot_x_mm` (Double, varsayılan: 0), `pivot_y_mm` (Double, varsayılan: 0), `copy` (Boolean, varsayılan: False)

### `move_element` 🔒

Eleman listesini dx/dy/dz_mm kadar taşır

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `dx_mm` (Double, varsayılan: 0), `dy_mm` (Double, varsayılan: 0), `dz_mm` (Double, varsayılan: 0)

### `place_family` 🔒

Serbest noktaya level-based family instance yerleştirir

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, zorunlu), `x_mm` (Double, varsayılan: 0), `y_mm` (Double, varsayılan: 0), `z_mm` (Double, varsayılan: 0), `rotation_deg` (Double, varsayılan: 0)

### `place_view_on_sheet` 🔒

View listesini params.sheet_number paftasına Viewport olarak ekler

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `sheet_number` (String, zorunlu), `x_mm` (Double, varsayılan: 0), `y_mm` (Double, varsayılan: 0)

### `rename_element` 🔒

Eleman listesinin params.param_name alanını günceller (value/prefix/suffix)

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'Name'), `value` (String, varsayılan: ''), `prefix` (String, varsayılan: ''), `suffix` (String, varsayılan: '')

### `set_element_type` 🔒

Eleman listesinin tipini params.type_name olarak değiştirir

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu)

### `set_level` 🔒

Eleman listesinin referans katını params.level_name olarak değiştirir

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `level_name` (String, zorunlu)

### `set_phase` 🔒

Elemanların faz oluşturuldu/yıkıldı parametresini atar

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `phase_name` (String, zorunlu), `phase_type` (String, varsayılan: 'created')

### `set_workset` 🔒

Eleman listesine params.workset_name workset'ini atar

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `workset_name` (String, zorunlu)

### `workset_by_level` 🔒

Her kat (Level) için aynı isimde User Workset oluşturur. Zaten varsa atlar. Model workshared değilse hata döner. Way-Tools WorksetByLevel.py mantığından C# portudur. params: prefix — workset adı öneki (opsiyonel). Örn: 'KAT-' → 'KAT-Zemin' suffix — workset adı soneki (opsiyonel) dry_run — true ise workset oluşturmaz, sadece listeler (default: false) Input: yok (tüm katları otomatik alır). Çıktı: List<Dictionary> — her satır bir workset işlemini temsil eder workset_adi, durum (OLUŞTURULDU / ZATEN_VAR / DRY_RUN), kat_yuksekligi_m

- **Çıktı:** `object`
- **Parametreler:** `prefix` (String, varsayılan: ''), `suffix` (String, varsayılan: ''), `dry_run` (Boolean, varsayılan: False)


## Oda

### `room_area_breakdown`

Oda bazlı taban, duvar ve tavan alanlarını hesaplar. Duvar alanı: oda çevre uzunluğu × kat yüksekliği - kapı/pencere açıklıkları. Input: collect_rooms çıktısı. Çıktı: List<Dictionary> — oda ve alan dökümleri.

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_boundary_extract`

Oda boundary segmentlerini zengin metadata ile List<Dict> olarak döner. Downstream: süpürgelik metrajı, yangın kontrolü, dış cephe filtresi, komşu oda tespiti.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `include_openings` (Boolean, varsayılan: True), `fire_param` (String, varsayılan: 'Yangın Dayanım Süresi')
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `loop_index`, `segment_index`, `host_wall_id`, `host_wall_type`, `fire_rating`, `is_exterior`, `start_x`, `start_y`, `end_x`, `end_y`, `length_mm`, `direction_deg`, `has_opening`

### `room_finish_assign` 🔒

Oda ismi veya fonksiyon parametresine göre kaplama tiplerini toplu atar. params: rules (zorunlu) — List<Dictionary> kural listesi:

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_finish_matrix`

Tüm odaları kaplama bilgileriyle tablo haline getirir. Input: collect_rooms çıktısı. Çıktı: List<Dictionary> — oda, zemin, duvar, tavan, alan.

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_finish_validate`

Odaların kaplama parametrelerinin dolu olup olmadığını kontrol eder. params: required_params (opsiyonel, default: zemin+duvar+tavan tümü). Input: collect_rooms çıktısı. Çıktı: List<Dictionary> — eksik parametreli oda kayıtları.

- **Çıktı:** `object`
- **Parametreler:** `required_params` (List<string>, önerilen)

### `room_naming_normalize` 🔒

Oda isimlerini EGBIM standardına göre normalize eder. Kural: BÜYÜK HARF, Türkçe karakterler korunur, fazla boşluk temizlenir. params: prefix (opsiyonel) — oda adı öneki. Input: collect_rooms çıktısı. Çıktı: Dictionary — degistirilen_count.

- **Çıktı:** `object`
- **Parametreler:** `prefix` (String, varsayılan: '')

### `room_to_ifc_space` 🔒

Oda fonksiyon tipini IFC Space tipine eşler ve EG_IfcSpaceTip parametresini yazar. Mapping: OFIS→IfcOffice, TOPLANTI→IfcMeetingRoom, BANYO→IfcSanitary, vb. Input: collect_rooms çıktısı. Çıktı: Dictionary — eslestirilen_count.

- **Çıktı:** `object`
- **Parametreler:** yok


## Oluşturma

### `create_floor` 🔒

Nokta listesi veya oda boundary'sinden döşeme oluşturur

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu), `level_name` (String, zorunlu), `offset_mm` (Double, varsayılan: 0), `points` (String, varsayılan: '')

### `create_grid` 🔒

İki nokta arasına aks oluşturur. params: x1_mm,y1_mm,x2_mm,y2_mm,name

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `x1_mm` (Double, varsayılan: 0), `y1_mm` (Double, varsayılan: 0), `x2_mm` (Double, varsayılan: 1000), `y2_mm` (Double, varsayılan: 0), `name` (String, varsayılan: '')

### `create_level` 🔒

Belirtilen kota kat oluşturur. params: elevation_mm, name

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `elevation_mm` (Double, varsayılan: 0), `name` (String, varsayılan: '$"Kat {elevFt*304.8/1000:F2}m"')

### `create_room` 🔒

Belirtilen noktaya oda yerleştirir. params: x_mm, y_mm, level_name, name, number

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `level_name` (String, zorunlu), `x_mm` (Double, varsayılan: 0), `y_mm` (Double, varsayılan: 0), `name` (String, varsayılan: 'Oda'), `number` (String, varsayılan: '')

### `create_wall` 🔒

Çizgi / 2-çizgi / oda sınırından duvar oluşturur. params: mode, type_name, level_name, height_mm, reference

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu), `level_name` (String, zorunlu), `mode` (String, varsayılan: 'from_lines'), `height_mm` (Double, varsayılan: 3000), `flip` (Boolean, varsayılan: False), `structural` (Boolean, varsayılan: False), `skip_existing` (Boolean, varsayılan: True), `reference` (String, varsayılan: 'centerline')


## Parametre

### `add_shared_params` 🔒

EGBIM paylaşımlı parametrelerini modele ekler. params: spf_path (opsiyonel), group_filter (opsiyonel)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `spf_path` (String, varsayılan: 'ctx.GetString("path", ""'), `group_filter` (String, varsayılan: '')

### `assign_poz_number` 🔒

Eleman listesine sıralı poz numarası atar.

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'EGBIM_PozNo'), `prefix` (String, varsayılan: ''), `start_from` (Int32, varsayılan: 1)

### `copy_param` 🔒

Elemanlarda params.source_param değerini params.target_param'a kopyalar

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `source_param` (String, önerilen), `target_param` (String, önerilen)

### `family_ensure_loaded` 🔒

params.family_path ailesinin yüklü olduğunu kontrol eder, yoksa yükler

- **Çıktı:** `bool`
- **Girdi:** `—`
- **Parametreler:** `family_path` (String, zorunlu)

### `generate_ids`

Eleman listesinden IDS (Information Delivery Specification) XML üretir. params: output_path, title

- **Çıktı:** `string`
- **Girdi:** `List<Element>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO IDS'), `output_path` (String, varsayılan: 'Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop')

### `list_shared_params`

Modeldeki tüm paylaşımlı parametreleri listeler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `param_exists`

Elemanlarda params.param_name parametresinin varlığını özet dict olarak döner. {found, missing, param_name}

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `param_validate_schema`

Eleman listesini params.schema_path JSON şemasına göre doğrular

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `schema_path` (String, varsayılan: 'ctx.GetString("path", ""'), `input` (Object, varsayılan: ''), `elements` (Object, varsayılan: '')

### `read_builtin_param`

Elemandan BuiltInParameter okur. params: builtin_param (BuiltInParameter enum adı)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `builtin_param` (String, önerilen)

### `read_param`

Eleman listesinden params.param_name parametresini okur. {element_id, kategori, tip, kat, <param>}

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `read_param_with_fallback`

Elemandan öncelikli parametre listesinden ilk dolu değeri okur. params: param_names (string[]), output_field (string). Çıktı: List<Dict> — element_id + output_field değeri.

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Element>`
- **Parametreler:** `output_field` (String, varsayılan: 'value'), `param_names` (List<string>, varsayılan: 'new List<string>(')

### `read_params`

Eleman listesinden params.param_names (virgülle ayrılmış) parametrelerini okur

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (String, önerilen)

### `type_get`

Eleman listesindeki benzersiz tipleri döner. {tip, kategori, tip_id, kullanim_sayisi, aile}

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `type_read_param`

Eleman listesinin TİP parametresini okur (instance değil tip parametresi)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `validate_required_params`

Elemanlarda params.required_params (virgülle) parametrelerinin varlığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `required_params` (String, önerilen)

### `write_param` 🔒

Tüm elemanlara params.param_name = params.value yazar (transaction açar)

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen), `value` (String, önerilen)

### `write_param_from_rows` 🔒

Satır listesindeki element_id'lere göre params.param_name = params.value_field yazar

- **Çıktı:** `int`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_name` (String, zorunlu), `value_field` (String, önerilen)

### `write_row_param` 🔒

Satır listesindeki her elemana params.value_key alanını params.param_name parametresine yazar. params: param_name, value_key (satırdaki alan adı). Input: List<Dictionary<string,object?>> — element_id ve value_key içermeli.

- **Çıktı:** `int`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_name` (String, önerilen), `value_key` (String, önerilen)


## PreCheck

### `precheck_active_document_exists`

Aktif bir Revit dokümanının açık olduğunu kontrol eder

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** yok

### `precheck_element_exists`

MODEL_HAS_ELEMENTS ile aynı kontrolü yapar (eski manifest adlandırması)

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `min_count` (Int32, varsayılan: 1), `categories` (List<String>, önerilen)

### `precheck_family_loaded`

Belirtilen ailenin projeye yüklenmiş olup olmadığını kontrol eder

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `family_name` (String, varsayılan: '')

### `precheck_file_exists`

Belirtilen dosyanın (data/ köküne göre veya mutlak) var olup olmadığını kontrol eder

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `path` (String, varsayılan: '')

### `precheck_model_has_elements`

Modelde belirtilen kategorilerden yeterli sayıda eleman var mı kontrol eder

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `min_count` (Int32, varsayılan: 1), `categories` (List<String>, önerilen)

### `precheck_param_exists`

Belirtilen kategorideki elemanlarda parametrenin tanımlı olup olmadığını kontrol eder

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `param` (String, varsayılan: ''), `categories` (List<String>, önerilen)

### `precheck_parameter_bound`

Parametrenin projeye bind edilip edilmediğini ve belirtilen kategori(ler)e bağlı olup olmadığını doc.ParameterBindings üzerinden kontrol eder (eleman örneği gerekmez). params: param (string), categories (string[])

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `param` (String, varsayılan: ''), `categories` (List<String>, önerilen)

### `precheck_registry_key_exists`

EgbimotoData.Registry içinde belirtilen key'in dolu olup olmadığını kontrol eder

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `key` (String, varsayılan: '')


## Proje Yönetimi

### `pm_get_project_info`

Proje bilgi kartını toplar (isim, müellif, tarih, konum)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `pm_model_delta_summary`

İki koleksiyon arasındaki farkı (eklenen/silinen) raporlar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `previous_ids` (List<long>, önerilen)

### `pm_validate_lod`

Elemanların LOD parametre doluluk durumunu kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `lod_level` (String, varsayılan: 'LOD300'), `required_params` (List<string>, önerilen)

### `pm_validate_naming_convention`

Eleman isimlerinin BEP/proje standardına uygunluğunu kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `name_pattern` (String, varsayılan: '')


## QA/QC

### `qa_check_approved_families`

Modeldeki aileleri onaylı liste ile karşılaştırır

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `approved_family_keywords` (List<string>, önerilen)

### `qa_check_level_assigned`

Elemanların bir seviyeye atanıp atanmadığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_detect_duplicates`

Aynı konumda birden fazla eleman tespiti (BBox karşılaştırma)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `tolerance_mm` (Double, varsayılan: 10.0)

### `qa_find_empty_params`

Belirtilen parametrelerin boş olduğu elemanları tespit eder

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (List<string>, önerilen)

### `qa_find_redundant_rooms`

Alan değeri olmayan ve modele katkısı bulunmayan odaları bulur

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_get_model_warnings`

Model uyarılarını toplar ve kategorize eder

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `qa_model_size_analysis`

Model eleman sayıları ve ağırlık analizi üretir

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `qa_validate_coordinates`

Projenin paylaşımlı koordinat sistemine bağlı olup olmadığını kontrol eder

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `qa_validate_phase_consistency`

Elemanların Creation/Demolition faz tutarlılığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_validate_workset`

Elemanların beklenen workset'e atanıp atanmadığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `expected_workset` (String, varsayılan: '')


## Raporlama

### `kalip_export_xlsx`

Kalıp hesap sonuçlarını Excel BOQ formatında dışa aktarır. 3 sekme: Özet (poz bazlı), Kat Özeti, Eleman Detayı. params: filename (string), out_dir (string)

- **Çıktı:** `object`
- **Parametreler:** `filename` (String, varsayılan: 'EG_Kalip_BOQ'), `out_dir` (String, varsayılan: '')
- **Okur:** `poz_data`

### `kalip_report`

Kalıp hesap sonuçlarını profesyonel HTML raporuna dönüştürür. Özet tablo + element detayları + teknik trace (açılır/kapanır). params: open_browser (bool), out_dir (string)

- **Çıktı:** `object`
- **Parametreler:** `open_browser` (Boolean, varsayılan: True), `out_dir` (String, varsayılan: '')
- **Okur:** `kalip_traces`, `poz_data`

### `schedule_export_anchored`

Bir Revit schedule'ını birebir görsel sadakatle (GetCellText) okur ve her eleman satırına UniqueId anchor ekleyerek List<Dictionary> üretir. Anchor, geçici alan + doc.Regenerate + RollBack ile alınır — MODEL KALICI DEĞİŞMEZ. Çıktı export_xlsx ile zincirlenebilir. Round-trip güvenli değilse uyarı satırı eklenir. params : schedule_name — çıkarılacak schedule adı (zorunlu) include_anchor — 'true' (default). false ise sadece görünür sütunlar (round-trip kapalı). Çıktı: List<Dictionary> — schedule satırları + (varsa) '

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `None`
- **Parametreler:** `schedule_name` (String, varsayılan: ''), `include_anchor` (String, varsayılan: 'true')
- **Çıktı alanları:** `__egbimoto_uid__`, `(schedule sütunları dinamik)`

### `schedule_roundtrip_apply` 🔒

schedule_roundtrip_diff çıktısındaki değişiklikleri güvenle modele yazar. Her yazımdan sonra değeri yeniden okuyarak doğrular (sessiz Set hatalarına karşı). DİKKAT: Model değişikliği yapar. Tip parametresi yazımı tüm tipi etkiler. Input : schedule_roundtrip_diff çıktısı (List<Dictionary>) — from ile bağlanır. params : apply_type_params — 'true' (default). false ise tip parametreleri atlanır (güvenli mod). Çıktı: List<Dictionary> — uid, field, old_value, new_value, status (ok|skip|fail), message

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `apply_type_params` (String, varsayılan: 'true')
- **Çıktı alanları:** `uid`, `field`, `old_value`, `new_value`, `status`, `message`

### `schedule_roundtrip_diff`

Düzenlenmiş schedule satırlarını (anchor'lı) canlı model ile karşılaştırır. Her satırdaki UniqueId ile elemanı bulur, hücre değerini model değeriyle kıyaslar. Yazma yapmaz — değişiklik listesi üretir. Uygulamak için schedule_roundtrip_apply. Input : schedule_export_anchored çıktısı + kullanıcı düzenlemeleri (List<Dictionary>). params : ignore_fields — karşılaştırılmayacak alanlar (virgül, opsiyonel) Çıktı: List<Dictionary> — her satır bir değişiklik adayı: uid, field, old_value, new_value, writable, binding, note

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `ignore_fields` (String, varsayılan: '')
- **Çıktı alanları:** `uid`, `field`, `old_value`, `new_value`, `writable`, `binding`, `note`


## Script

### `clear_script_cache`

Roslyn script derleme önbelleğini temizler. Script dosyasını değiştirip yeniden derlenmesini istediğinizde kullanın.

- **Çıktı:** `Dictionary`
- **Girdi:** `Any`
- **Parametreler:** yok
- **Çıktı alanları:** `cleared`

### `run_csharp_script`

Bir .cs dosyasını runtime'da Roslyn ile derler ve çalıştırır — rebuild gerekmez. Script globals: uiapp (UIApplication), doc (Document), inputs (Dictionary<string,object?> — params), input (object? — from bağlantısı). Script Dictionary<string,object?> döndürmelidir. params: script_path (zorunlu), cache (opsiyonel, default:true — aynı session'da yeniden derleme yapmaz).

- **Çıktı:** `Dictionary`
- **Girdi:** `Any`
- **Parametreler:** `script_path` (String, varsayılan: ''), `cache` (bool, varsayılan: True)
- **Çıktı alanları:** `result`


## Semantik

### `assign_egbim_mark` 🔒

Elemanlara EGBIM_Mark parametresi yazar (canonical_class + element_id). Transaction açar.

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `classify_by_wbs`

Eleman listesini WBS haritalama tablosuna göre sınıflandırır

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Okur:** `wbs_mapping`

### `classify_elements`

Eleman listesine canonical_class ve disiplin atar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `map_to_ifc`

Eleman listesini IFC haritalama tablosuna göre IFC sınıfına eşler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Okur:** `ifc_mapping`

### `resolve_canonical_class`

Revit kategori/tip adından EGBIM canonical class çözer. params: type_name veya category

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `type_name` (String, önerilen), `category` (String, önerilen)

### `resolve_discipline`

Revit kategorisinden disiplin çözer (Mimari/Yapısal/MEP). params: category

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `category` (String, önerilen)


## Seçim

### `selection_gate`

DagExecutor tarafından intercept edilir — uidoc.Selection.PickObject(s) çalıştırır. Bu metod ÇAĞRILMAZ; sadece OpRegistry kaydı içindir. params: prompt, mode(single|multiple), categories(OST_ listesi), min_count, max_count, allow_linked. Çıktı: vars[stepId] = SelectionResultDto (ToString() → 'confirmed'|'cancelled').

- **Çıktı:** `object`
- **Parametreler:** yok

### `selection_to_elements`

selection_gate çıktısındaki (SelectionResultDto) ElementId'leri gerçek Element listesine çevirir. Silinmiş/geçersiz ID'ler atlanır. Input: SelectionResultDto (from: selection_gate step id). Çıktı: List<Element>.

- **Çıktı:** `List<Element>`
- **Girdi:** `SelectionResultDto`
- **Parametreler:** yok


## Sistem

### `eg_addin_disable_unused`

Zorunlu olmayan add-in'leri .EGdisabled olarak yeniden adlandırır.

- **Çıktı:** `Dictionary`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: 'GetRevitVersion(ctx'), `keep` (List<String>, önerilen)
- **Çıktı alanları:** `disabled_count`, `skipped_readonly`, `failed_count`, `restart_required`

### `eg_addin_restore_all`

Tüm .EGdisabled ve .RSTdisabled add-in'leri geri yükler. Revit yeniden başlatılana kadar etkili olmaz.

- **Çıktı:** `Dictionary`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: 'GetRevitVersion(ctx')
- **Çıktı alanları:** `restored_count`, `failed_count`, `restart_required`

### `eg_addin_restore_single`

Tek bir add-in'i geri yükler.

- **Çıktı:** `bool`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: 'GetRevitVersion(ctx'), `addin_file` (String, varsayılan: '')

### `eg_addin_scan`

Kurulu Revit add-in'leri listeler. revit_version=2026 (varsayılan: çalışan Revit) | include_disabled=true/false

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: 'GetRevitVersion(ctx'), `include_disabled` (Boolean, varsayılan: True)
- **Çıktı alanları:** `fileName`, `disabled`, `directory`, `name`, `addinId`

### `eg_health_snapshot`

Sistem + Revit sağlık raporu üretir (RAM/CPU/Disk/OS/Revit/Warnings). format=html|json|text | open=true/false | out_path=<dosya>

- **Çıktı:** `string`
- **Girdi:** `None`
- **Parametreler:** `format` (String, varsayılan: 'html'), `open` (Boolean, varsayılan: False), `out_path` (String, varsayılan: '')
- **Çıktı alanları:** `out_path`


## Toplama

### `collect_air_terminals`

Hava terminallerini toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_beams`

Tüm yapısal kirişleri toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_by_category`

params.category (BuiltInCategory adı) kategorisindeki elemanları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: 'OST_Walls')

### `collect_by_ids`

Dict satırlarındaki ID alan(lar)ından Element listesi toplar. Satır-tabanlı op çıktılarını (clash, scan, diff) Element bekleyen op'lara (move_element, set_param) köprüler. Input: List<Dictionary>. params: id_fields (virgülle çok alan, default 'element_id'), distinct (default 'true').

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `id_fields` (String, varsayılan: 'element_id'), `distinct` (String, varsayılan: 'true')

### `collect_by_phase`

params.phase_name fazında oluşturulan elemanları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `phase_name` (String, zorunlu)

### `collect_by_type_name`

params.type_name içeren tip adına sahip elemanları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `type_name` (String, önerilen)

### `collect_by_workset`

params.workset_name içeren workset'teki elemanları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `workset_name` (String, önerilen)

### `collect_cable_trays`

Tüm kablo tavalarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_casework`

Tüm dolap/mutfak elemanlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_ceilings`

Tüm tavanları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_columns`

Tüm yapısal kolonları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_conduits`

Tüm boru iletkenlerini toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_curtain_walls`

Tüm giydirme cephe duvarlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_doors`

Tüm kapıları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_duct_fittings`

Tüm kanal bağlantı parçalarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_ducts`

Tüm havalandırma kanallarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_electrical_equipment`

Elektrik ekipmanlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_electrical_fixtures`

Elektrik prizlerini/anahtarlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_elements`

Kategori bazlı toplama. params: category (BuiltInCategory adı). V1 uyumluluk alias.

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: 'OST_Walls')

### `collect_families`

Yüklü aileleri toplar. params: category (opsiyonel)

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: '')

### `collect_fire_alarm_devices`

Yangın alarm cihazlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_floors`

Tüm döşemeleri toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_foundations`

Tüm temelleri toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_furniture`

Tüm mobilyaları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_furniture_systems`

Tüm mobilya sistemlerini toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_generic_models`

Tüm genel modelleri toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_grids`

Tüm aks çizgilerini toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_levels`

Tüm katları (Level) yüksekliğe göre sıralı toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_lighting_devices`

Aydınlatma cihazlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_lighting_fixtures`

Aydınlatma elemanlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_linked_elements`

Bağlı (linked) Revit modellerindeki elemanları toplar. params: category

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: 'OST_Walls')

### `collect_mechanical_equipment`

Mekanik ekipmanları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_multi`

Birden fazla kategoriyi paralel olarak toplar. params: categories (string[])

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `phase_name` (String, varsayılan: ''), `categories` (List<string>, varsayılan: 'new List<string>(')

### `collect_parking`

Tüm otopark elemanlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_pipe_accessories`

Tüm boru aksesuarlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_pipe_fittings`

Tüm boru bağlantı parçalarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_pipes`

Tüm boruları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_plumbing_fixtures`

Sıhhi tesisat elemanlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_ramps`

Tüm rampları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_rebar`

Tüm donatıları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_rebar_in_host`

params.host_id element_id'sine sahip elemanın içindeki donatıları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `host_id` (Int32, varsayılan: -1)

### `collect_roofs`

Tüm çatıları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_rooms`

Tüm odaları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_selected`

Revit'te seçili olan elemanları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_sheets`

Tüm çizim paftalarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_site`

Tüm arazi elemanlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_sprinklers`

Tüm sprinkler elemanlarını toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_stairs`

Tüm merdivenleri toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_structural_columns`

Yapısal kolonları toplar (collect_columns alias — dil bağımsız BIC tabanlı).

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_structural_framing`

Yapısal çerçeve elemanlarını toplar (collect_beams ile aynı OST_StructuralFraming — alias)

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_structural_walls`

Yapısal duvarları toplar (bearing)

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_topography`

Tüm topoğrafya yüzeylerini toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_types`

Belirli kategorideki tüm element tiplerini (ElementType) toplar. params: category (BuiltInCategory adı, opsiyonel)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: '')

### `collect_views`

Tüm görünümleri toplar. params: view_type (FloorPlan|Section|Elevation|3D|Schedule|All)

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `view_type` (String, varsayılan: 'All')

### `collect_walls`

Tüm duvarları toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_windows`

Tüm pencereleri toplar

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `intersect_report`

Kaynak kategori(ler)deki her eleman için hedef kategori(ler)de kesişen 'host' elemanları bulur ve satır listesi üretir. params: source_categories (string[]), target_categories (string[]), include_no_host (bool, default true). 'from' ile bir önceki step'in eleman listesi kaynak olarak da kullanılabilir.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (opsiyonel — yoksa source_categories kullanılır)`
- **Parametreler:** `include_no_host` (Boolean, varsayılan: True), `source_categories` (List<String>, önerilen), `target_categories` (List<String>, önerilen)
- **Çıktı alanları:** `element_id`, `kategori`, `tip`, `host_id`, `host_kategori`, `host_tip`, `no_host`


## Trace

### `compare_run_result`

Bu run'ın çıktısını önceki run ile karşılaştırır. Yeni eklenen, korunan ve silinen elemanları raporlar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu)
- **Çıktı alanları:** `element_id`, `status`

### `delete_generated` 🔒

trace_key altında kayıtlı tüm elemanları modelden siler ve trace kaydını temizler. confirm=true zorunlu.

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** `trace_key` (String, zorunlu), `confirm` (Boolean, varsayılan: False)

### `trace_find_existing`

trace_key altında kaydedilmiş elemanları modelden okur. Silinmiş veya geçersiz ID'leri filtreler.

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `trace_key` (String, zorunlu)

### `trace_write` 🔒

Üretilen elemanların ID'lerini trace_key altında modele kaydeder. Bir sonraki run'da trace_find_existing ile geri okunur.

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu)

### `update_or_create_family` 🔒

Trace kaydı varsa elemanı günceller, yoksa yeni oluşturur. Dynamo element binding'in EGBIMOTO karşılığı.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu), `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, varsayılan: ''), `update_type` (Boolean, varsayılan: True), `update_location` (Boolean, varsayılan: False)
- **Çıktı alanları:** `element_id`, `index`, `action`, `status`


## UI

### `select_by_id`

Verilen element id'ler(i) Revit'te seçer ve görünüme zoom yapar. params: element_id (tek id) veya ids (liste, virgülle ayrılmış string ya da array).

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `element_id` (String, varsayılan: ''), `ids` (List<String>, önerilen)
- **Çıktı alanları:** `selected`


## Veri

### `assign_wbs_code`

Satır listesindeki kategori alanına göre WBS kodu atar. params: category_field (default: 'kategori'), aktivite_field (default: '', boşsa sadece kategori bazlı eşleme). Yeni format: revit_kategori_wbs.json → {wbs_kodu, disiplin, canonical_v3} döner.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `category_field` (String, varsayılan: 'kategori'), `aktivite_field` (String, varsayılan: '')
- **Okur:** `wbs_mapping`
- **Çıktı alanları:** `wbs_kodu`

### `data_get`

DataRegistry'den params.key ile kayıtlı veriyi döner

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `key` (String, önerilen)

### `data_list_keys`

DataRegistry'deki tüm kayıtlı anahtarları listeler

- **Çıktı:** `List<string>`
- **Girdi:** `—`
- **Parametreler:** yok

### `data_load`

JSON dosyasını yükler, normalize eder, DataRegistry'ye kaydeder. inputs: path (data/ altı göreli veya tam yol), key (opsiyonel, varsayılan dosya adı), root_key (opsiyonel wrapper anahtarı, örn: 'rules'), cache_bust (opsiyonel bool, true=her seferinde taze oku)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `path` (String, önerilen), `root_key` (String, varsayılan: ''), `cache_bust` (Boolean, varsayılan: False), `key` (String, varsayılan: 'Path.GetFileNameWithoutExtension(fullPath')

### `export_wbs_report`

WBS metraj raporunu JSON olarak dışa aktarır. params: output_path

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `output_path` (String, varsayılan: 'Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop')

### `link_quantity_to_wbs`

Metraj satırlarını WBS koduna göre gruplar ve toplar

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `quantity_field` (String, varsayılan: 'hacim_m3'), `wbs_field` (String, varsayılan: 'wbs_kodu')

### `load_ifc_mapping`

IFC haritalama verilerini yükler

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `ifc_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "mapping", "ifc_mapping.json"')
- **Yazar:** `ifc_mapping`

### `load_poz_data`

POZ verilerini yükler. params: poz_path (opsiyonel, varsayılan data/poz/csb_poz_2026.json)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `poz_path` (String, varsayılan: 'ctx.GetString("path",\n                ctx.GetString("file",\n                Path.Combine(EgbimotoData.DataRoot, "poz", "csb_poz_2026.json"'), `file` (String, önerilen)
- **Yazar:** `poz_data`
- **Çıktı alanları:** `birim_fiyat`

### `load_qa_matrix`

QA kural matrisini yükler. params: qa_path (opsiyonel)

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `qa_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "semantic", "qa_rule_matrix.json"')
- **Yazar:** `qa_matrix`

### `load_rayic`

Rayiç fiyat listesini yükler. params: rayic_path (opsiyonel)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `rayic_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "poz", "rayic_2026.json"')
- **Yazar:** `rayic_data`

### `load_shared_param_map`

Paylaşımlı parametre haritasını yükler

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `map_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "mapping", "shared_param_map.json"')
- **Yazar:** `shared_param_map`

### `load_wbs_mapping`

WBS haritalama verilerini yükler. params: wbs_path (opsiyonel)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `wbs_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "mapping", "revit_kategori_wbs.json"')
- **Yazar:** `wbs_mapping`

### `lookup_value`

Satır listesindeki params.key_field değerini params.lookup_key ile lookup tablosunda arar ve params.result_field ekler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `key_field` (String, önerilen), `lookup_key` (String, önerilen), `result_field` (String, varsayılan: 'lookup_result')

### `pivot_table`

Satır listesini params.row_field x params.col_field pivot tablosuna dönüştürür

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `row_field` (String, zorunlu), `col_field` (String, zorunlu), `value_field` (String, zorunlu), `func` (String, varsayılan: 'sum')


## Yangın

### `fa_classify_room_detector`

Oda adına göre beklenen dedektör tipini belirler (Duman/Isı)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `heat_detector_keywords` (List<string>, önerilen)

### `fa_validate_circuit_assigned`

FA cihazlarının bir loop/devreye atanıp atanmadığını kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `fa_validate_device_in_room`

Her oda için FA cihazı varlığını ve tipini doğrular

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fa_devices` (List<Element>, önerilen), `smoke_keywords` (List<string>, önerilen), `heat_keywords` (List<string>, önerilen)

### `fa_validate_mounting_height`

FA cihazlarının montaj yüksekliğini (AFF) kontrol eder

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_aff_mm` (Double, varsayılan: 2000.0), `max_aff_mm` (Double, varsayılan: 2400.0), `device_filter` (String, varsayılan: '')

### `fp_compartment_area_check`

Yangin kompartiman alani ve kat sayisi kontrolu (TR Yangin Yon. Tablo-1). params: bina_kullanimi — konut|ofis|ticaret|hastane|depo|sanayi_dusuk| sanayi_orta|sanayi_yuksek|toplanma|yatakhane kompartiman_alani_m2 — hesaplanan kompartiman alani m2 kat_sayisi — kompartimandaki kat sayisi sprinkler_var — bool (limit 2x artabilir) Cikti: max_alan_m2, mevcut_alan_m2, max_kat, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_kullanimi` (String, varsayılan: 'ofis'), `kompartiman_alani_m2` (Double, varsayılan: 0), `kat_sayisi` (Int32, varsayılan: 1), `sprinkler_var` (Boolean, varsayılan: False)
- **Çıktı alanları:** `bina_kullanimi`, `mevcut_alan_m2`, `mevcut_kat_sayisi`, `max_alan_m2`, `max_kat_sayisi`, `sprinkler_var`, `alan_uygun`, `kat_uygun`, `mesajlar`, `standart`, `durum`

### `fp_detection_coverage`

Dedektör kapsama alani hesabi (NFPA 72 / TR Yangin Yonetmeligi). params: oda_alani_m2 — kontrol edilecek alan m2 dedektör_tipi — duman|isi|alev|co (default duman) tavan_yuksekligi_m — tavan yuksekligi m (default 2.7) mevcut_sayi — modeldeki dedektör sayisi (0=sadece hesap) Cikti: gerekli_sayi, kapsama_per_cihaz_m2, max_aralik_m, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `oda_alani_m2` (Double, varsayılan: 0), `dedektör_tipi` (String, varsayılan: 'duman'), `tavan_yuksekligi_m` (Double, varsayılan: 2.7), `mevcut_sayi` (Int32, varsayılan: 0)
- **Çıktı alanları:** `oda_alani_m2`, `dedektör_tipi`, `tavan_yuksekligi_m`, `kapsama_m2_per_cihaz`, `gerekli_sayi`, `mevcut_sayi`, `max_aralik_m`, `durum`

### `fp_evacuation_route_check`

Tahliye yolu genisligi ve max mesafe kontrolu (TR Yangin Yon. §49-57). params: yol_tipi — koridor|merdiven|kapi|rampa mevcut_genislik_m — modeldeki genislik m kisi_sayisi — bu yolu kullanan kisi sayisi cikis_mesafesi_m — en uzak noktadan cikisa uzaklik m sprinkler_var — bool (mesafe limiti etkiler) Cikti: min_genislik_m, mevcut_genislik_m, max_mesafe_m, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `yol_tipi` (String, varsayılan: 'koridor'), `mevcut_genislik_m` (Double, varsayılan: 0), `kisi_sayisi` (Int32, varsayılan: 0), `cikis_mesafesi_m` (Double, varsayılan: 0), `sprinkler_var` (Boolean, varsayılan: False)
- **Çıktı alanları:** `yol_tipi`, `kisi_sayisi`, `min_genislik_m`, `mevcut_genislik_m`, `max_cikis_mesafesi_m`, `max_cikmaz_m`, `sprinkler_var`, `kontroller`, `durum`

### `fp_exit_sign_spacing`

Acil cikis isareti yerlesimi kontrolu (TS EN 1838 / NFPA 101). params: koridor_uzunlugu_m — toplam koridor uzunlugu m isaret_yuksekligi_mm — isaret yuksekligi mm (default 150) mevcut_isaret_sayisi — modeldeki isaret sayisi (0=hesap) montaj_yuksekligi_m — montaj yuksekligi m (default 2.2) Cikti: max_gorunurluk_m, gerekli_sayi, mevcut_sayi, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `koridor_uzunlugu_m` (Double, varsayılan: 0), `isaret_yuksekligi_mm` (Int32, varsayılan: 150), `mevcut_isaret_sayisi` (Int32, varsayılan: 0), `montaj_yuksekligi_m` (Double, varsayılan: 2.2)
- **Çıktı alanları:** `koridor_uzunlugu_m`, `isaret_yuksekligi_mm`, `max_gorunurluk_m`, `gerekli_sayi`, `mevcut_sayi`, `montaj_yuksekligi_m`, `montaj_uygun`, `montaj_notu`, `standart`, `durum`

### `fp_fdc_clearance_check` 🔒

FDC (Itfaiye Baglantisi) konumu ve temizlik mesafesi kontrolu (NFPA 14). params: min_aff_mm — min montaj yuksekligi mm (default 450) min_temizlik_mm — on temizlik mesafesi mm (default 3000) write_back — EG_FdcUygunluk parametresine yaz (default false) Revit: EG_FdcYukseklik + EG_FdcTemizlik parametreleri okunur. Cikti: fdc_id, yukseklik_mm, temizlik_mm, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (GenericModel FDC)`
- **Parametreler:** `min_aff_mm` (Int32, varsayılan: 450), `min_temizlik_mm` (Int32, varsayılan: 3000), `write_back` (Boolean, varsayılan: False)
- **Okur:** `EG_FdcTemizlik`, `EG_FdcYukseklik`
- **Yazar:** `EG_FdcUygunluk`
- **Çıktı alanları:** `fdc_id`, `yukseklik_mm`, `temizlik_mm`, `kontroller`, `durum`

### `fp_fire_door_rating_check` 🔒

Yangin kapisi fire rating ve bosluk kontrolu (TS EN 1634 / TR Yangin Yon.). params: min_rating_dak — min gereken EI suresi dakika (30|60|90|120) tolerance_mm — rating toleransi (default 0 = tam eslesme) write_back — EG_KapiUygunluk'a yaz (default false) Revit: EG_YanginKapiRating + EG_KapiUstBosluk + EG_KapiAltBosluk okunur. Cikti: kapi_id, mevcut_rating, gerekli_rating, bosluk_durum, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (Doors)`
- **Parametreler:** `min_rating_dak` (Int32, varsayılan: 60), `write_back` (Boolean, varsayılan: False), `tolerance_mm` (Int, önerilen)
- **Okur:** `EG_KapiAltBosluk`, `EG_KapiUstBosluk`, `EG_YanginKapiRating`
- **Yazar:** `EG_KapiUygunluk`
- **Çıktı alanları:** `kapi_id`, `mevcut_rating`, `gerekli_rating`, `rating_durum`, `ust_bosluk_mm`, `alt_bosluk_mm`, `bosluk_durum`, `durum`

### `fp_pump_hp_calc`

Yangin pompasi HP hesabi (NFPA 20). Ph=rho*g*Q*H/1000. params: debi_lpm — tasarim debisi L/dak toplam_yuk_m — toplam manometrik yuk m su kolonu pompa_verim — 0.60-0.80 (default 0.70) motor_verim — 0.85-0.95 (default 0.90) guvenlik_faktor — 1.25 (yangin pompasi, NFPA 20) Cikti: ph_kw, pb_kw, pm_kw, oneri_motor_kw, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `debi_lpm` (Double, varsayılan: 0), `toplam_yuk_m` (Double, varsayılan: 0), `pompa_verim` (Double, varsayılan: 0.7), `motor_verim` (Double, varsayılan: 0.9), `guvenlik_faktor` (Double, varsayılan: 1.25)
- **Çıktı alanları:** `debi_lpm`, `debi_m3s`, `toplam_yuk_m`, `pompa_verim`, `motor_verim`, `guvenlik_faktor`, `ph_kw`, `pb_kw`, `pm_kw`, `oneri_motor_kw`, `durum`

### `fp_pump_schedule_validate`

Yangin pompasi schedule dogrulamasi (NFPA 20 / TR Yangin Yon.). params: ana_pompa_lpm — ana pompa tasarim debisi L/dak ana_pompa_bar — ana pompa tasarim basinci bar jockey_lpm — jockey debi L/dak (default ana x 0.01) diesel_gerekli — bool (default true >6 kat) kat_sayisi — diesel gereksinim tespiti NFPA 20: Ana x 1.5 debide min basinc, jockey küçük sürekli. Cikti: ana_pompa, jockey_pompa, diesel_pompa, gereksinimler, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `ana_pompa_lpm` (Double, varsayılan: 0), `ana_pompa_bar` (Double, varsayılan: 0), `jockey_lpm` (Double, varsayılan: -1), `diesel_gerekli` (Boolean, varsayılan: True), `kat_sayisi` (Int32, varsayılan: 0)
- **Çıktı alanları:** `ana_pompa_lpm`, `ana_pompa_bar`, `yuzde150_lpm`, `yuzde150_min_bar`, `kapatma_max_bar`, `jockey_lpm`, `jockey_max_lpm`, `jockey_uygun`, `diesel_gerekli`, `gereksinimler`, `durum`

### `fp_sprinkler_hydraulic`

Sprinkler hidrolik hesap: K-faktoru, debi, basinc (NFPA 13 / TS EN 12845). params: k_faktoru — K57|K80|K115|K160|K202|K242|K320|K363 isletme_basinc_bar — sprinkler isletme basinci bar sprinkler_sayisi — hidrolik olarak acik bas sayisi tehlike_sinifi — dusuk|orta_1|orta_2|yuksek_1 (TR Ek-8/B) Q = K * sqrt(P). Cikti: debi_lpm, toplam_debi, min_basinc, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `k_faktoru` (String, varsayılan: 'K80'), `isletme_basinc_bar` (Double, varsayılan: 0), `sprinkler_sayisi` (Int32, varsayılan: 1), `tehlike_sinifi` (String, varsayılan: 'orta_1')
- **Çıktı alanları:** `k_faktoru`, `k_degeri`, `isletme_basinc_bar`, `min_basinc_bar`, `debi_lpm`, `min_debi_lpm`, `sprinkler_sayisi`, `toplam_debi_lpm`, `tehlike_sinifi`, `min_tehlike_debi`, `basinc_uygun`, `tehlike_uygun`, `durum`

### `fp_sprinkler_temp_class`

Sprinkler sicaklik sinifi dogrulamasi (TS EN 12845 / NFPA 13). params: ortam_sicaklik_c — max beklenen ortam sicakligi C mevcut_sinif — 57C|68C|79C|93C|141C|182C|204C|260C guvenlik_marji_c — min marj C (default 30) Kural: calisma temp >= ortam + guvenlik_marji. Cikti: gerekli_sinif, mevcut_sinif, renk, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `ortam_sicaklik_c` (Double, varsayılan: 0), `mevcut_sinif` (String, varsayılan: ''), `guvenlik_marji_c` (Double, varsayılan: 30.0)
- **Çıktı alanları:** `ortam_sicaklik_c`, `guvenlik_marji_c`, `gerekli_min_c`, `gerekli_sinif`, `gerekli_renk`, `mevcut_sinif`, `mevcut_renk`, `durum`

### `fp_standpipe_pressure`

Standpipe statik basinc hesabi ve PRV gereksinimi (NFPA 14). params: bina_yuksekligi_m — toplam bina yuksekligi m pompa_basinc_bar — pompa cikis basinci bar kat_yuksekligi_m — kat yuksekligi m (default 3.0) max_hortum_bar — max hortum cikisi bar (default 6.9) NFPA 14: max 6.9 bar hortum cikisi. PRV yuksek zonda zorunlu. Cikti: statik_basinc_bar, prv_sayisi, prv_konumlari, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_yuksekligi_m` (Double, varsayılan: 0), `pompa_basinc_bar` (Double, varsayılan: 0), `kat_yuksekligi_m` (Double, varsayılan: 3.0), `max_hortum_bar` (Double, varsayılan: 6.9)
- **Çıktı alanları:** `bina_yuksekligi_m`, `pompa_basinc_bar`, `statik_basinc_bar`, `toplam_basinc_bar`, `max_hortum_bar`, `max_kat_per_zon`, `prv_sayisi`, `prv_kat_konumlari`, `durum`

### `fp_standpipe_qa`

Standpipe riser DN boyutu ve sistem tipi kontrolu (NFPA 14 / TR Yangin Yonetmeligi). params: bina_yuksekligi_m — bina toplam yuksekligi m mevcut_dn_mm — modeldeki riser cap mm (0=kontrol atla) sistem_tipi — islak|kuru|kombine (default islak) kat_sayisi — toplam kat sayisi Kural: <30m=DN100, 30-60m=DN125, >60m=DN150. >15m bina islak zorunlu. Cikti: gerekli_dn_mm, sistem_tipi, gereksinimler, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_yuksekligi_m` (Double, varsayılan: 0), `mevcut_dn_mm` (Int32, varsayılan: 0), `sistem_tipi` (String, varsayılan: 'islak'), `kat_sayisi` (Int32, varsayılan: 0)
- **Çıktı alanları:** `bina_yuksekligi_m`, `kat_sayisi`, `sistem_tipi`, `gerekli_dn_mm`, `mevcut_dn_mm`, `dn_durum`, `tip_durum`, `gereksinimler`, `durum`

### `fp_suppression_agent_qa`

Sondurme ajani tipi ve alan uygunluk kontrolu. params: ajan_tipi — su|kuru_toz|kopuk|co2|fm200|inergen|abc_toz alan_tipi — sunucu_odasi|jenerator|arsiv|mutfak|akaryakit| genel_ofis|imalathane|depo|otopark Cikti: ajan_tipi, alan_tipi, uygun_mu, alternatifler, standartlar, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `ajan_tipi` (String, varsayılan: ''), `alan_tipi` (String, varsayılan: '')
- **Çıktı alanları:** `ajan_tipi`, `alan_tipi`, `uygun_mu`, `alternatifler`, `standartlar`, `uyarilar`, `durum`

### `fp_validate_sprinkler_coverage`

Sprinkler kapsama alanını hesaplar ve maksimum sınırla karşılaştırır

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_coverage_m2` (Double, varsayılan: 12.0)


## Yangın Hesap

### `calc_duct_sheet_weight`

Kanal çevre × kalınlık × uzunluk × yoğunluk → sac ağırlığı (kg). Input: List<Duct> veya params (width_mm, height_mm, length_m). Sadece dikdörtgen kanallar hesaplanır.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `thickness_mm` (Double, varsayılan: 0.8), `density_kg_m3` (Double, varsayılan: 7850.0), `width_mm` (Double, varsayılan: 0), `height_mm` (Double, varsayılan: 0), `length_m` (Double, varsayılan: 0)
- **Çıktı alanları:** `duct_id`, `width_mm`, `height_mm`, `length_m`, `perimeter_mm`, `thickness_mm`, `sheet_volume_m3`, `sheet_weight_kg`

### `calc_sprinkler_design_density`

Türk Yangın Yönetmeliği Ek-8/B tablosundan tehlike sınıfına göre tasarım yoğunluğu (mm/dak) ve koruma alanı (m²) döner.

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `hazard_class` (String, zorunlu), `system_type` (String, varsayılan: 'islak'), `area_m2` (Double, varsayılan: 0)
- **Çıktı alanları:** `hazard_class`, `design_density_mm_per_min`, `protection_area_m2`, `required_flow_lpm`, `total_pump_flow_lpm`, `reference`

### `fire_hose_cabinet_spacing_check`

Yangın dolapları arası mesafeyi kontrol eder (≤30m). Türk Yangın Yönetmeliği: her katta, yangın duvarları ile ayrılmış her bölümde, dolaplar arası ≤30m.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_spacing_m` (Double, varsayılan: 30.0), `hose_length_m` (Double, varsayılan: 20.0)


## Yapısal

### `column_setup_params` 🔒

EGBIM kolon on boyutlandirma shared parametrelerini (sabit GUID) projeye yukler ve StructuralColumns kategorisine instance binding yapar. params: spf_path — SPF yolu (opsiyonel, default: mapping/EGBIM_KolonParams.txt) Hesaptan ONCE bir kez calistirilir (altyapi kurulumu). Cikti: added, skipped, spf_path

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: '')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `struct_beam_depth_ratio` 🔒

Kiris derinlik/aciklik orani kontrolu (TS 500 §9.1 / TBDY §7.4). params: kiriş_tipi — basit|surekli|konsol (default surekli) min_genislik_mm— min kiriş genişliği mm (default 250) write_back — EG_KirisUygunluk parametresine yaz Revit: Framing elemanlarından aciklik+kesit okunur. Cikti: eleman_id, aciklik_mm, yukseklik_mm, h_l_orani, min_h_mm, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (StructuralFraming-Beam)`
- **Parametreler:** `kiriş_tipi` (String, varsayılan: 'surekli'), `min_genislik_mm` (Int32, varsayılan: 250), `write_back` (Boolean, varsayılan: False)
- **Yazar:** `EG_KirisUygunluk`
- **Çıktı alanları:** `eleman_id`, `aciklik_mm`, `yukseklik_mm`, `genislik_mm`, `h_l_orani`, `min_h_mm`, `durum`

### `struct_concrete_class_qa`

Beton sinifi QA (TS 500 / TBDY 2018). Kullanim yerine gore min sinif kontrolu. params: beton_sinifi — C20|C25|C28|C30|C32|C35|C40|C45|C50 kullanim_yeri — genel|deprem_yuksek|deprem_orta|perde|prefabrik|zemin_temasi deprem_bolgesi — bool (TBDY min C25, default true) eleman_tipi — kolon|kiriş|perde|döşeme|temel|prefabrik Cikti: fck, fcd, fctd, fbd, min_sinif, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `beton_sinifi` (String, varsayılan: 'C25'), `kullanim_yeri` (String, varsayılan: 'genel'), `deprem_bolgesi` (Boolean, varsayılan: True), `eleman_tipi` (String, varsayılan: 'kolon')
- **Çıktı alanları:** `beton_sinifi`, `fck_mpa`, `fcd_mpa`, `fctd_mpa`, `fbd_mpa`, `kullanim_yeri`, `eleman_tipi`, `min_sinif`, `min_fck_mpa`, `uyarilar`, `durum`

### `struct_formwork_type_select`

Kalip sistemi secimi ve karsilastirma (9 tip). params: bina_tipi — konut|yuksek_katli|ticari|sanayi|altyapi kat_sayisi — toplam kat sayisi (yüksek kat → flying/tünel) tekrar_oncelik — bool (tekrar kullanım öncelikli mi) hiz_oncelik — bool (hız öncelikli mi) mevcut_tip — ahsap|celik|moduler|flying_table|tunel| aluminyum|plastik|waffle|uboot Cikti: oneri_tip, karsilastirma_tablosu, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_tipi` (String, varsayılan: 'konut'), `kat_sayisi` (Int32, varsayılan: 5), `tekrar_oncelik` (Boolean, varsayılan: False), `hiz_oncelik` (Boolean, varsayılan: False), `mevcut_tip` (String, varsayılan: '')
- **Çıktı alanları:** `bina_tipi`, `kat_sayisi`, `oneri_tip`, `oneri_neden`, `mevcut_tip`, `mevcut_durum`, `karsilastirma`, `durum`

### `struct_foundation_bearing`

Temel taban basinci kontrolu (TS 500 §15). params: eksenel_yuk_kn — toplam düşey yük kN (N) temel_genislik_m — temel genişliği B m temel_uzunluk_m — temel uzunluğu L m (tekil için) zemin_emniyet_kpa — zemin emniyet gerilmesi kPa (default 150) moment_knm — devrilme momenti kN.m (default 0) temel_tipi — tekil|serit|radye (default tekil) Cikti: taban_basinci_kpa, emniyet_kpa, dismerkezlik_m, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `eksenel_yuk_kn` (Double, varsayılan: 0), `temel_genislik_m` (Double, varsayılan: 0), `temel_uzunluk_m` (Double, varsayılan: 0), `zemin_emniyet_kpa` (Double, varsayılan: 150.0), `moment_knm` (Double, varsayılan: 0), `temel_tipi` (String, varsayılan: 'tekil')
- **Çıktı alanları:** `eksenel_yuk_kn`, `temel_genislik_m`, `temel_uzunluk_m`, `temel_alani_m2`, `taban_basinci_kpa`, `dismerkezlik_m`, `e_max_m`, `q_max_kpa`, `zemin_emniyet_kpa`, `kapasite_pct`, `mesajlar`, `durum`

### `struct_rebar_anchorage`

Ankraj boyu hesabi (TS 500 §8.4): lb0 = (phi/4) x (fyd/fbd). params: cap_mm — donatı çapı mm beton_sinifi — C20|C25|C30 vb. celik_sinifi — B420C|B500C ankraj_tipi — duz|kancali|cengelli (default duz) basinc_mi — true=basınç bölgesi (default false) Cikti: lb0_mm, lb_mm, min_ankraj_mm, ankraj_tipi, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `cap_mm` (Int32, varsayılan: 16), `beton_sinifi` (String, varsayılan: 'C25'), `celik_sinifi` (String, varsayılan: 'B420C'), `ankraj_tipi` (String, varsayılan: 'duz'), `basinc_mi` (Boolean, varsayılan: False)
- **Çıktı alanları:** `cap_mm`, `beton_sinifi`, `celik_sinifi`, `fyd_mpa`, `fbd_mpa`, `lb0_mm`, `ankraj_tipi`, `beta`, `lb_mm`, `min_ankraj_mm`, `basinc_mi`, `durum`

### `struct_rebar_lap_kolon`

Kolon filiz/bindirme boyu hesabi (TBDY 2018 §7.3 / TS 500 §8.5). params: cap_mm — donatı çapı mm (8-32) beton_sinifi — C20|C25|C30|C35|C40 vb. celik_sinifi — B420C|B500C (default B420C) kolon_l_d_orani — L/D oranı (default 3.0 = dar kolon) deprem_bolgesi — bool (TBDY ek çarpan, default true) basinc_mi — true=basınç, false=çekme bindirmesi Cikti: lb0_mm, bindirme_mm, filiz_min_mm, l_d_tipi, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `cap_mm` (Int32, varsayılan: 16), `beton_sinifi` (String, varsayılan: 'C25'), `celik_sinifi` (String, varsayılan: 'B420C'), `kolon_l_d_orani` (Double, varsayılan: 3.0), `deprem_bolgesi` (Boolean, varsayılan: True), `basinc_mi` (Boolean, varsayılan: True)
- **Çıktı alanları:** `cap_mm`, `beton_sinifi`, `celik_sinifi`, `fyd_mpa`, `fbd_mpa`, `lb0_mm`, `alpha`, `bindirme_mm`, `filiz_min_mm`, `l_d_orani`, `l_d_tipi`, `deprem_bolgesi`, `durum`

### `struct_rebar_lap_perde`

Perde duvar filiz/bindirme boyu hesabi (TBDY 2018 §7.7 / TS 500). params: cap_mm — donatı çapı mm beton_sinifi — C25|C30|C35 vb. celik_sinifi — B420C|B500C donati_yonu — yatay|duseyWATCH (default duseyWATCH) perde_kalinlik_mm — perde kalınlığı mm perde_yukseklik_mm— perde yüksekliği mm Cikti: lb0_mm, bindirme_mm, uc_bolge_mm, min_donati_orani, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `cap_mm` (Int32, varsayılan: 12), `beton_sinifi` (String, varsayılan: 'C25'), `celik_sinifi` (String, varsayılan: 'B420C'), `donati_yonu` (String, varsayılan: 'dusey'), `perde_kalinlik_mm` (Double, varsayılan: 200), `perde_yukseklik_mm` (Double, varsayılan: 3000)
- **Çıktı alanları:** `cap_mm`, `beton_sinifi`, `donati_yonu`, `lb0_mm`, `bindirme_mm`, `uc_bolge_mm`, `min_donati_orani`, `l_d_orani`, `l_d_notu`, `durum`

### `struct_slab_thickness`

Doseme kalinlik kontrolu (TS 500 §12.2). params: aciklik_mm — döşeme açıklığı mm (tek yönlü=uzun kenar, çift=kısa) doseme_tipi — tek_yonlu|cift_yonlu|konsol|mantar|waffle mevcut_kalinlik_mm — modeldeki kalınlık mm (0=sadece hesap) Cikti: min_kalinlik_mm, mevcut_kalinlik_mm, h_l_orani, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `aciklik_mm` (Double, varsayılan: 0), `doseme_tipi` (String, varsayılan: 'cift_yonlu'), `mevcut_kalinlik_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `aciklik_mm`, `doseme_tipi`, `hesap_min_mm`, `min_kalinlik_mm`, `mevcut_kalinlik_mm`, `h_l_orani`, `standart`, `durum`

### `struct_steel_bolt_type_check`

Celik yapi bulon tipi QA (AISC 360 / ASTM F3125 / RCSC). params: bulon_tipi — N|X|T|SC baglanti_turu — kesme_kritik|kayma_kritik|gerilme|kombine titresim_var — bool (SC önerilir titreşimde) dinamik_yuk — bool (SC zorunlu) N: dişler kesme düzleminde (düşük); X: dişler dışında (yüksek); T: tam dişli; SC: kayma kritik (sürtünme). Cikti: bulon_tipi, tanim, kesme, standartlar, oneri, durum

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bulon_tipi` (String, varsayılan: 'N'), `baglanti_turu` (String, varsayılan: 'kesme_kritik'), `titresim_var` (Boolean, varsayılan: False), `dinamik_yuk` (Boolean, varsayılan: False)
- **Çıktı alanları:** `bulon_tipi`, `tanim`, `kesme_kapasitesi`, `standartlar`, `not`, `baglanti_turu`, `titresim_var`, `dinamik_yuk`, `oneri_tip`, `oneri_neden`, `durum`

### `struct_wall_slenderness` 🔒

Perde duvar narinlik kontrolu (TBDY 2018 §7.6.2 / TS 500 §11). params: max_narinlik — max h/t oranı (default 25, TBDY) min_kalinlik_mm— min duvar kalınlığı mm (default 200) write_back — EG_PerdeUygunluk'a yaz (default false) Revit: Structural Wall elemanlarından yükseklik+kalınlık okunur. Cikti: duvar_id, yukseklik_mm, kalinlik_mm, narinlik, durum

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (Structural Walls)`
- **Parametreler:** `max_narinlik` (Double, varsayılan: 25.0), `min_kalinlik_mm` (Int32, varsayılan: 200), `write_back` (Boolean, varsayılan: False)
- **Yazar:** `EG_PerdeUygunluk`
- **Çıktı alanları:** `duvar_id`, `yukseklik_mm`, `kalinlik_mm`, `narinlik_h_t`, `max_narinlik`, `durum`

### `structural_collect_all`

Tüm yapısal elementleri (kolon, kiriş, döşeme, perde, temel) toplar. params: level (opsiyonel), categories (opsiyonel — filtre listesi). Çıktı: List<Element>.

- **Çıktı:** `object`
- **Parametreler:** `level` (String, varsayılan: ''), `categories` (List<string>, önerilen)

### `structural_column_presizing` 🔒

Betonarme kolonlari TBDY 2018 / TS 500 kesit kosullarina gore KONTROL eder ve gereken min kesiti ONERIR. Sonuclari kolona yazar. Kontroller:

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (StructuralColumn)`
- **Parametreler:** `default_fck` (Double, varsayılan: 25), `default_birim_yuk` (Double, varsayılan: 12), `duktilite` (String, varsayılan: 'yuksek'), `round_to_mm` (Double, varsayılan: 50)
- **Okur:** `EG_BetonSinif`, `EG_BirimYuk`, `EG_KatSayisi`, `EG_KolonTipi`, `EG_Ndm`, `EG_YukAlani`, `EG_fck_Override`
- **Yazar:** `EG_EksenelKontrol_TBDY`, `EG_EksenelKontrol_TS500`, `EG_GerekenAc_TBDY`, `EG_HesapTarihi`, `EG_KirisKolonAyrim`, `EG_KolonDurumu`, `EG_KullanilanNdm`, `EG_MevcutAc`, `EG_MevcutB`, `EG_MevcutH`, `EG_MinBoyutKontrol`, `EG_OneriBoyut`
- **Çıktı alanları:** `kolon_id`, `b_mm`, `h_mm`, `ac_mm2`, `fck_mpa`, `ndm_kn`, `gereken_ac`, `oneri_boyut`, `min_boyut`, `tbdy_eksenel`, `ts500`, `durum`

### `structural_continuity_check`

Kolonların kat geçişlerinde Z ekseninde sürekli olup olmadığını kontrol eder. Her katın kolon pozisyonlarını üst katla karşılaştırır. Input: collect_structural_columns çıktısı. Çıktı: List<Dictionary> — süreklilik kırıkları.

- **Çıktı:** `object`
- **Parametreler:** `tolerans_mm` (Double, varsayılan: 50.0)

### `structural_level_summary`

Kat bazlı yapısal element özeti üretir. Her kat için: kolon_adet, kiriş_adet, perde_m2, döşeme_m2. Input: structural_collect_all çıktısı. Çıktı: List<Dictionary>.

- **Çıktı:** `object`
- **Parametreler:** yok

### `structural_material_check`

Yapısal elementlerin beton/çelik sınıf parametrelerinin dolu olup olmadığını kontrol eder. params: beton_required (opsiyonel, default:true), celik_required (opsiyonel, default:false). Input: yapısal elementler. Çıktı: List<Dictionary> — eksik malzeme parametreli elementler.

- **Çıktı:** `object`
- **Parametreler:** `beton_required` (Boolean, varsayılan: True), `celik_required` (Boolean, varsayılan: False)

### `structural_tbdy_params` 🔒

Yapısal elementlere TBDY 2018 parametrelerini toplu yazar. params: deprem_bolgesi (zorunlu: DD-1..DD-4), duktilite_sinifi (opsiyonel: YSBD/YSK, default:YSBD), beton_sinif (opsiyonel: C25, C30 vb.), celik_sinif (opsiyonel: B420C, S235 vb.). Input: yapısal elementler. Çıktı: Dictionary — yazilan_count.

- **Çıktı:** `object`
- **Parametreler:** `deprem_bolgesi` (String, zorunlu), `duktilite_sinifi` (String, varsayılan: 'YSBD'), `beton_sinif` (String, varsayılan: ''), `celik_sinif` (String, varsayılan: '')

### `structural_ts500_section`

TS 500 Madde 7 minimum kesit boyutlarını kontrol eder. Kolon: min 25×25cm, Kiriş: min 20×30cm, Perde: min 20cm, Döşeme: min 12cm. params: override_min_kolon_b, override_min_kolon_h (opsiyonel, mm). Input: yapısal elementler (List<Element>). Çıktı: List<Dictionary> — ihlal listesi.

- **Çıktı:** `object`
- **Parametreler:** `override_min_kolon_b` (Double, varsayılan: 'MIN_KOLON_B'), `override_min_kolon_h` (Double, varsayılan: 'MIN_KOLON_H')


## Yapısal Oluşturma

### `create_beam_by_curve` 🔒

Curve listesi boyunca yapısal kiriş oluşturur. Input: line_by_points veya curve_divide_* çıktısı.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, zorunlu), `offset_mm` (Double, varsayılan: 0)

### `create_column_by_point` 🔒

XYZ nokta listesine yapısal kolon oluşturur. Input: points_grid veya table_to_points çıktısı.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `base_level` (String, zorunlu), `top_level` (String, varsayılan: ''), `height_mm` (Double, varsayılan: 3000), `structural` (Boolean, varsayılan: True)

### `create_grid_by_line` 🔒

Curve listesinden Grid (aks) oluşturur. Input: line_by_points çıktısı. Mevcut create_grid'in curve versiyonu.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `name_prefix` (String, varsayılan: 'G'), `start_index` (Int32, varsayılan: 1)

### `place_adaptive_component_by_points` 🔒

Adaptive family'yi nokta listeleriyle yerleştirir. points_per_item: her instance için kaç nokta tüketilir.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `points_per_item` (Int32, varsayılan: 2)


## Yardımcı

### `assert_not_empty`

Girdi boşsa hata fırlatır. params.message ile özel mesaj.

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `message` (String, varsayılan: 'Beklenen veri bulunamadı.')

### `compute`

Sayısal hesap. Input sayı veya params.a, params.b. op: add|sub|mul|div|percent|round|min|max. func: sum|avg|min|max|count (satır listesi için)

- **Çıktı:** `double`
- **Girdi:** `—`
- **Parametreler:** `field` (String, önerilen), `func` (String, varsayılan: 'sum'), `a` (Double, önerilen), `b` (Double, varsayılan: 1.0), `op` (String, varsayılan: 'mul')

### `count_items`

Herhangi bir liste veya dict'in eleman sayısını döner

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** yok

### `echo`

params.value'yu döner (test/sabit değer için)

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `value` (String, varsayılan: ''), `message` (String, önerilen)

### `element_count`

Input eleman listesinin sayısını döner

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** yok

### `eq`

İki değeri karşılaştırır. params: left, right. Çıktı: bool. Condition yerine manifest adımı olarak da kullanılabilir.

- **Çıktı:** `bool`
- **Parametreler:** `left` (Object, varsayılan: ''), `right` (Object, varsayılan: '')

### `flatten_list`

İç içe listeleri tek düzey listeye düzleştirir

- **Çıktı:** `List<object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `format_message`

params.template içindeki {key} yerlerine vars veya params değerlerini koyar

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `template` (String, varsayılan: '')

### `format_number`

Sayıyı params.format ile biçimlendirir. Örn: 'N2', 'F3'

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `value` (Double, önerilen), `format` (String, varsayılan: 'N2'), `unit` (String, varsayılan: '')

### `log_message`

params.message metnini log'a yazar, input'u geçirir

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `message` (String, varsayılan: '—')

### `model_checksum`

Modeldeki eleman sayısını kategoriye göre özetler

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `noop`

Hiçbir şey yapmaz, input'u geçirir

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** yok

### `op_health_check`

Kayıtlı op sayısını ve kategorilerini döner (sistem sağlık kontrolü)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `pass_through`

Input'u değiştirmeden geçirir (debug/bağlantı için)

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** yok

### `set_var`

params.value sabitini döner. Manifest sabiti tanımlamak için.

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `value` (Object, varsayılan: ''), `key` (String, önerilen)

### `show_count`

Input koleksiyonunun eleman sayısını log'a yazar

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** yok

### `show_result`

Input'u TaskDialog ile gösterir

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO')

### `sum_field`

Dict listesindeki params.field alanlarını toplar

- **Çıktı:** `double`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu)

### `system_info`

Model ve sistem bilgilerini döner (proje adı, yol, Revit versiyonu, eleman sayısı)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `write_trace`

Log mesajını dosyaya yazar. params: message, file_path (opsiyonel). Debug için.

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `message` (String, varsayılan: 'ctx.Input?.ToString('), `file_path` (String, varsayılan: '')


## Yerleştirme

### `calc_placement_point`

Girdi elemanının origin'inden offset_x/y_mm + height_mm ile XYZ dict üretir

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `offset_x_mm` (Double, varsayılan: 0), `offset_y_mm` (Double, varsayılan: 0), `height_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `x`, `y`, `z`, `level_id`

### `collect_doors_in_room`

Odanın boundary segmentlerinden host duvarlarını bulur, o duvarlardaki kapıları döner

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `get_door_wall_clearances`

Her kapı için duvardaki sol/sağ serbest mesafeyi mm olarak döner

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `left_mm`, `right_mm`, `wall_length_mm`, `door_width_mm`

### `get_room_ceiling_center`

Her odanın tavan düzeyindeki merkez XYZ koordinatını döner

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `room_name`, `x`, `y`, `z`

### `place_family_along_mep` 🔒

Boru/kanal/kablo taşıyıcı hatları boyunca belirtilen aralıkla (spacing_mm) verilen aileyi (askı, destek, etiket) yerleştirir. Manuel family + manuel aralık. Input: List<Element> (MEP hat segmentleri). params: family_name, type_name (zorunlu), spacing_mm (default 2000), end_setback_mm (default 300), vertical_offset_mm (default 0).

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, varsayılan: ''), `type_name` (String, varsayılan: ''), `spacing_mm` (Double, varsayılan: 2000), `end_setback_mm` (Double, varsayılan: 300), `vertical_offset_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `host_id`, `category`, `placed_count`, `spacing_mm`, `run_length_m`

### `place_family_on_ceiling` 🔒

Tavan yüzeyine aile yerleştirir (aydınlatma/duman dedektörü/difüzör vb.). Transaction gerektirir. Face-based aile ise tavan alt yüzüne host'lar; değilse tavan merkez noktasına yerleştirir. Input: List<Element> (tavanlar). params: family_name, type_name (zorunlu), offset_x_mm (default 0), offset_y_mm (default 0). Çıktı: List<Dictionary> — host_id, placed (bool), mode (face|point|fail).

- **Çıktı:** `List<Dictionary<string,object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, önerilen), `type_name` (String, önerilen), `offset_x_mm` (Double, varsayılan: 0), `offset_y_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `host_id`, `placed`, `mode`

### `place_family_on_wall` 🔒

Duvara host'lu aile yerleştirir (kapı/pencere/aydınlatma vb.). Transaction gerektirir. Input: List<Element> (duvarlar). params: family_name, type_name (zorunlu), offset_mm (default 1200, duvar başından ilk nokta), spacing_mm (default 0 = tek eleman; >0 ise aralıklı çoklu), sill_mm (default 0, yerleştirme yüksekliği). Çıktı: List<Dictionary> — host_id, placed_count, wall_length_m.

- **Çıktı:** `List<Dictionary<string,object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, önerilen), `type_name` (String, önerilen), `offset_mm` (Double, varsayılan: 1200), `spacing_mm` (Double, varsayılan: 0), `sill_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `host_id`, `placed_count`, `wall_length_m`


## Çizim

### `dimension_continuous_selection` 🔒

Verilen (veya params.categories ile toplanan) birbirine paralel elemanları ortak doğrultuları boyunca sıralayıp tek bir ardışık (continuous) ölçü serisi oluşturur. Donatı/kolon aralıkları, pencere/kapı dizileri vb. için kullanılabilir. params: categories (string[]), dim_type (string, opsiyonel)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `dim_type` (String, varsayılan: ''), `categories` (StringList, önerilen)
- **Çıktı alanları:** `created`, `segments`, `skipped`

### `dimension_to_nearest_grid` 🔒

Verilen (veya params.categories ile toplanan) elemanları aktif görünümde kendine paralel en yakın grid'e ölçülendirir. Farklı kotlardaki elemanlar 2D'ye düzleştirilerek karşılaştırılır. params: categories (string[]), dim_type (string, opsiyonel), search_radius_m (default 9.0)

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `dim_type` (String, varsayılan: ''), `search_radius_m` (Double, varsayılan: 9.0), `categories` (StringList, önerilen)
- **Çıktı alanları:** `created`, `skipped`


## Çıktı

### `element_report`

Eleman veya satır listesini HTML rapor olarak dışa aktarır (export_html_report alias)

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Eleman Raporu')

### `export_csv`

Satır listesini CSV olarak kaydeder. params: file_path (opsiyonel)

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file_path` (String, varsayılan: ''), `rows` (Object, varsayılan: '')

### `export_html_report`

Satır listesini HTML rapor olarak kaydeder ve tarayıcıda açar. params: title

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Rapor'), `rows` (Object, varsayılan: '')

### `export_pdf`

Satır listesini baskı-uyumlu HTML olarak kaydeder ve tarayıcıda açar (Ctrl+P ile PDF kaydet). FIX#10: Doğrudan PDF üretmez, print-to-PDF workflow'u. params: title

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Rapor')

### `export_row_report`

Satır listesini veya eleman listesini Desktop'ta HTML rapor olarak kaydeder. params: title, fields (opsiyonel — satır anahtarları listesi).

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Raporu'), `fields` (List<string>, önerilen)

### `export_validation_report`

ValidationReport'u HTML olarak dışa aktarır. params: title, file_path

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Doğrulama Raporu'), `file_path` (String, varsayılan: 'Path.Combine(\n                Environment.GetFolderPath(Environment.SpecialFolder.Desktop'), `rows` (Object, varsayılan: ''), `input` (Object, varsayılan: '')

### `export_xlsx`

Satır listesini Excel .xlsx dosyası olarak kaydeder. params: file_path (opsiyonel), sheet_name

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file_path` (String, varsayılan: ''), `sheet_name` (String, varsayılan: 'EGBIMOTO'), `title` (String, varsayılan: 'EGBIMOTO'), `rows` (Object, varsayılan: '')

### `show_table`

Satır listesini WPF sonuç penceresinde gösterir (sıralanabilir tablo, 'Modelde Göster', CSV dışa aktarım). Dict/scalar input için TaskDialog'a düşer. params: title, max_rows

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Sonuç'), `max_rows` (Int32, varsayılan: 500)

### `xlsx_import_preview`

Bir .xlsx dosyasını okur ve (varsa) 'from' ile verilen orijinal satır listesiyle key_field üzerinden karşılaştırıp değişen/yeni/eksik satırları işaretler. Hiçbir Transaction açmaz — sadece önizleme üretir. params: file_path (zorunlu), key_field (default 'element_id')

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>> (opsiyonel — karşılaştırma için orijinal satırlar)`
- **Parametreler:** `file_path` (String, zorunlu), `key_field` (String, varsayılan: 'element_id')
- **Çıktı alanları:** `_status`, `_diff_fields`


## Önizleme

### `preview_collect_geometry`

Element listesinden Three.js uyumlu PreviewGeometryDto üretir. params: operation_name (string), include_labels (bool, default:true), max_elements (int, default:500)

- **Çıktı:** `PreviewGeometryDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `operation_name` (String, varsayılan: 'Önizleme'), `include_labels` (Boolean, varsayılan: True), `max_elements` (Int32, varsayılan: 500)

### `preview_gate`

Kullanıcı onay kapısı. DagExecutor tarafından intercept edilir; doğrudan çağrılmamalı. params: title (string)

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** yok
