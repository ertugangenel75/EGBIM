# EGBIMOTO — Op Referansı

Toplam **400 operasyon**, 48 kategori.

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
- [Görünüm](#gorunum) — 4 op
- [IFC](#ifc) — 1 op
- [Kapı](#kapi) — 5 op
- [Koordinasyon](#koordinasyon) — 7 op
- [Liste](#liste) — 9 op
- [Maliyet](#maliyet) — 14 op
- [Mekansal](#mekansal) — 1 op
- [MEP Denetim](#mep-denetim) — 8 op
- [MEP Hesap](#mep-hesap) — 4 op
- [MEP HVAC](#mep-hvac) — 3 op
- [MEP Koordinasyon](#mep-koordinasyon) — 4 op
- [MEP-Elektrik](#mep-elektrik) — 10 op
- [MEP-Mekanik](#mep-mekanik) — 7 op
- [MEP-Sıhhi](#mep-sihhi) — 9 op
- [Mimari](#mimari) — 10 op
- [Modelleme](#modelleme) — 12 op
- [Oda](#oda) — 7 op
- [Oluşturma](#olusturma) — 5 op
- [Parametre](#parametre) — 18 op
- [Proje Yönetimi](#proje-yonetimi) — 4 op
- [QA/QC](#qa-qc) — 10 op
- [Raporlama](#raporlama) — 5 op
- [Script](#script) — 2 op
- [Semantik](#semantik) — 6 op
- [Sistem](#sistem) — 5 op
- [Toplama](#toplama) — 56 op
- [Trace](#trace) — 5 op
- [Veri](#veri) — 14 op
- [Yangın](#yangin) — 5 op
- [Yangın Hesap](#yangin-hesap) — 3 op
- [Yapısal](#yapisal) — 8 op
- [Yapısal Oluşturma](#yapisal-olusturma) — 4 op
- [Yardımcı](#yardimci) — 20 op
- [Yerleştirme](#yerlestirme) — 7 op
- [Çıktı](#cikti) — 8 op
- [Önizleme](#onizleme) — 2 op


## 4D/5D

### `schedule_collect_4d`

- **Çıktı:** `FourDFiveDDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `operation_name` (String, varsayılan: '4D Yapım Simülasyonu'), `project_start` (String, varsayılan: 'bugün'), `project_end` (String, varsayılan: 'project_start+9ay'), `kat_sure_hafta` (Int, varsayılan: '3'), `max_elements` (Int, varsayılan: '500'), `schedule_map` (Object, varsayılan: '{}')
- **Çıktı alanları:** `meshes`, `schedule_items`, `project_start`, `project_end`, `element_count`, `warnings`, `stats`, `bbox`

### `schedule_collect_5d`

- **Çıktı:** `FourDFiveDDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `operation_name` (String, varsayılan: '4D/5D Maliyet Simülasyonu'), `project_start` (String, varsayılan: 'bugün'), `project_end` (String, varsayılan: 'project_start+9ay'), `kat_sure_hafta` (Int, varsayılan: '3'), `max_elements` (Int, varsayılan: '500'), `schedule_map` (Object, varsayılan: '{}'), `cost_step` (String, varsayılan: '')
- **Okur:** `cost_rows (opsiyonel, P2 fallback)`
- **Çıktı alanları:** `meshes`, `schedule_items`, `cost_items`, `total_cost`, `currency`, `project_start`, `project_end`, `element_count`, `warnings`, `stats`, `bbox`

### `schedule_gate`

DagExecutor intercept — FourDFiveDWindow açar. Çıktı: 'confirmed' | 'cancelled'

- **Çıktı:** `string`
- **Girdi:** `FourDFiveDDto`
- **Parametreler:** `title` (String, varsayılan: '4D/5D Önizleme')

### `set_param_from_schedule` 🔒

Yazma op — transaction gerektirir. Dönüş: yazılan element sayısı.

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `schedule_step` (String, zorunlu), `start_param` (String, varsayılan: 'EG_BaslangicTarihi'), `end_param` (String, varsayılan: 'EG_BitisTarihi'), `phase_param` (String, varsayılan: 'EG_FazAdi'), `wbs_param` (String, varsayılan: 'EG_WbsKodu'), `write_cost` (Bool, varsayılan: 'true')


## Aile

### `family_add_param` 🔒

Aktif aile belgesine yeni bir instance veya type parametresi ekler.

- **Çıktı:** `object`
- **Parametreler:** yok

### `family_batch_load` 🔒

Belirtilen klasördeki tüm .rfa dosyalarını projeye toplu yükler.

- **Çıktı:** `object`
- **Parametreler:** yok

### `family_health_check`

status: OK|WARNING|ERROR. check_nested=true ağır işlem — scope daralt.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `expected_params` (String, varsayılan: ''), `max_types` (Int, varsayılan: '50'), `origin_threshold_m` (Double, varsayılan: '100.0'), `check_nested` (Bool, varsayılan: 'false')
- **Çıktı alanları:** `family_id`, `family_name`, `category`, `type_count`, `param_count`, `is_in_place`, `has_no_category`, `type_count_ok`, `missing_params`, `has_origin_issue`, `nested_count`, `status`

### `family_load_to_project` 🔒

Bir .rfa dosyasını aktif Revit projesine yükler (varsa günceller).

- **Çıktı:** `object`
- **Parametreler:** yok

### `family_open_template`

Bir aile şablonu dosyasını açar veya mevcut aile belgesini döner.

- **Çıktı:** `object`
- **Parametreler:** yok

### `family_type_create` 🔒

Bir aile içinde yeni tip oluşturur veya varolan tipin parametrelerini günceller.

- **Çıktı:** `object`
- **Parametreler:** yok


## Analiz

### `slope_analysis`

Döşeme / çatı / topografya yüzeylerinin eğim açısını hesaplar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `slope_validate`

Eğim analizi sonuçlarını Türk standartlarına göre doğrular.

- **Çıktı:** `object`
- **Parametreler:** yok


## Annotation

### `align_tags` 🔒

Tag/yazı elemanlarının başlarını (head) hizalar veya eşit dağıtır (left|right|top|bottom|center|middle|distribute_h|distribute_v). Leader'lara dokunulmaz, etiket başları hizalanır.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `mode` (String, varsayılan: 'left'), `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `tip`, `mode`, `tasindi`, `durum`

### `arrange_tags` 🔒

Leader'lı IndependentTag'leri görünüm kenarlarına otomatik dizer ve leader çaprazlarını çözer. CropBox aktif olmalı.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `taraf`, `durum`


## Cephe

### `collect_curtain_panels`

Projedeki tüm curtain panel elementlerini toplar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_area_by_type`

Panel tipine göre toplam cephe alanını hesaplar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_export_schedule`

Cephe metraj tablosunu HTML + özet satırıyla dışa aktarır.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_joint_validate`

Curtain panellerin derz parametrelerini doğrular.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_opening_ratio`

Cephe saydamlık oranını hesaplar (pencere alanı / toplam cephe alanı).

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_panel_matrix`

Curtain panellerini tip ve kata göre gruplandırarak matris tablosu oluşturur.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_system_params` 🔒

Cephe duvarlarına EGBIMOTO TR BIM parametrelerini toplu yazar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_u_value_check`

Cephe panellerinin U değerini TS 825 limitine göre kontrol eder.

- **Çıktı:** `object`
- **Parametreler:** yok


## CSV

### `csv_read`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `file_path` (String, zorunlu), `delimiter` (String, varsayılan: ','), `has_header` (Bool, varsayılan: 'true'), `encoding` (String, varsayılan: 'utf-8'), `skip_rows` (Int, varsayılan: '0')

### `csv_write`

- **Çıktı:** `string`
- **Girdi:** `List<Dict>`
- **Parametreler:** `file_path` (String, zorunlu), `delimiter` (String, varsayılan: ','), `include_header` (Bool, varsayılan: 'true'), `encoding` (String, varsayılan: 'utf-8-sig')

### `excel_xml_read`

openpyxl gerektirmez — ZipArchive+XML

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `file_path` (String, zorunlu), `sheet_name` (String, varsayılan: ''), `has_header` (Bool, varsayılan: 'true'), `skip_rows` (Int, varsayılan: '0')

### `table_to_points`

Çıktı: List<XYZ> — Geometry Ops ve Create Ops için köprü

- **Çıktı:** `List<object?>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `x_field` (String, varsayılan: 'x'), `y_field` (String, varsayılan: 'y'), `z_field` (String, varsayılan: 'z'), `unit` (String, varsayılan: 'mm')

### `table_validate_schema`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `required_fields` (String, zorunlu), `optional_fields` (String, varsayılan: '')
- **Çıktı alanları:** `valid`, `missing_fields`, `row_count`, `field_count`, `message`


## Donatı

### `calc_anchorage_length`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: '12'), `fck` (Double, varsayılan: '25'), `fyk` (Double, varsayılan: '420'), `cover_mm` (Double, varsayılan: '30'), `hook` (Bool, varsayılan: 'false')

### `calc_lap_length`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: '12'), `fck` (Double, varsayılan: '25'), `fyk` (Double, varsayılan: '420'), `cover_mm` (Double, varsayılan: '30')

### `calc_min_spacing`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: '12'), `aggregate_size_mm` (Double, varsayılan: '20')

### `rebar_summary_by_diameter`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `rebar_summary_by_level`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `rebar_total_weight`

- **Çıktı:** `double`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `rebar_weight_calc`

Donatı listesini çap tablosu + fabrika boyu + bindirme katsayısıyla hesaplar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `rebar_weight_table`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `validate_rebar_ts500`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `fck` (Double, varsayılan: '25'), `fyk` (Double, varsayılan: '420'), `cover_mm` (Double, varsayılan: '30')


## Doğrulama

### `check_overlapping_rooms`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `check_unplaced_rooms`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `check_zero_volume`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `merge_validation_reports`

- **Çıktı:** `ValidationReport`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'Birleşik Doğrulama Raporu')

### `param_exists_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu)

### `param_filled_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu), `severity` (String, varsayılan: 'ERROR')

### `param_range_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu), `min` (Double, varsayılan: 'double.MinValue'), `max` (Double, varsayılan: 'double.MaxValue'), `severity` (String, varsayılan: 'WARNING')

### `param_value_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu), `expected_value` (String, zorunlu), `severity` (String, varsayılan: 'ERROR')

### `validate_ids`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `ids_path` (String, varsayılan: '')

### `validate_qa`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `rules_path` (String, varsayılan: '')

### `validation_summary`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** yok

### `validation_to_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok


## Duvar

### `wall_type_export_csv`

Projedeki mevcut Basic Wall tiplerini wall_type_from_csv uyumlu CSV formatına aktarır.

- **Çıktı:** `object`
- **Parametreler:** yok

### `wall_type_from_csv` 🔒

CSV dosyasından Revit Basic Wall tipleri oluşturur veya günceller.

- **Çıktı:** `object`
- **Parametreler:** yok


## ETL

### `load_poz_canonical_map`

data/poz/poz_canonical_map.json dosyasını registry'ye yükler. poz_match_keynote_aware için ön koşul.

- **Çıktı:** `Dictionary`
- **Girdi:** `—`
- **Parametreler:** yok

### `load_poz_section_rules`

data/poz/poz_section_rules.json dosyasını registry'ye yükler. canonical_class → poz prefix listesi.

- **Çıktı:** `Dictionary`
- **Girdi:** `—`
- **Parametreler:** yok


## Filtre

### `add_column`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu), `value` (String, zorunlu)

### `distinct_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu)

### `elements_to_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elements_to_rows_with_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (String, zorunlu)

### `filter_by_category`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `category` (String, zorunlu)

### `filter_by_level`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `level_name` (String, zorunlu)

### `filter_by_level_range`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_level` (String, zorunlu), `max_level` (String, zorunlu)

### `filter_by_param`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu), `value` (String, zorunlu), `operator` (String, varsayılan: 'equals')

### `filter_by_type`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu)

### `filter_by_workset`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `workset_name` (String, zorunlu)

### `filter_empty_param`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu)

### `filter_not_empty_param`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu)

### `filter_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu), `value` (String, zorunlu), `operator` (String, varsayılan: 'eq')

### `filter_rows_multi`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `conditions` (Array, zorunlu)

### `group_by`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu)

### `group_elements_by_category`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `group_elements_by_level`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `group_elements_by_type`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `join_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `key_field` (String, zorunlu)

### `merge_lists`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `merge_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `rename_column`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `from` (String, zorunlu), `to` (String, zorunlu)

### `select_columns`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fields` (String, zorunlu)

### `select_field`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fields` (String, zorunlu)

### `skip_n`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `count` (Int, varsayılan: '0')

### `sort_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu), `descending` (Bool, varsayılan: 'false')

### `take_n`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `count` (Int, varsayılan: '10')

### `where`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu), `op` (String, varsayılan: 'eq'), `value` (String, zorunlu)


## Geometri

### `beam_volume`

beam_volume

- **Çıktı:** `object`
- **Parametreler:** yok

### `column_height`

column_height

- **Çıktı:** `object`
- **Parametreler:** yok

### `column_volume`

column_volume

- **Çıktı:** `object`
- **Parametreler:** yok

### `curve_from_element`

curve_from_element

- **Çıktı:** `object`
- **Parametreler:** yok

### `element_area`

element_area

- **Çıktı:** `object`
- **Parametreler:** yok

### `element_bounding_box`

element_bounding_box

- **Çıktı:** `object`
- **Parametreler:** yok

### `element_length`

element_length

- **Çıktı:** `object`
- **Parametreler:** yok

### `element_volume`

element_volume

- **Çıktı:** `object`
- **Parametreler:** yok

### `element_volume_geometry`

element_volume_geometry

- **Çıktı:** `object`
- **Parametreler:** yok

### `floor_volume`

floor_volume

- **Çıktı:** `object`
- **Parametreler:** yok

### `foundation_volume`

foundation_volume

- **Çıktı:** `object`
- **Parametreler:** yok

### `mep_by_system`

mep_by_system

- **Çıktı:** `object`
- **Parametreler:** yok

### `mep_summary`

mep_summary

- **Çıktı:** `object`
- **Parametreler:** yok

### `mep_total_length`

mep_total_length

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_area`

room_area

- **Çıktı:** `object`
- **Parametreler:** yok

### `wall_length`

wall_length

- **Çıktı:** `object`
- **Parametreler:** yok

### `wall_net_area`

wall_net_area

- **Çıktı:** `object`
- **Parametreler:** yok

### `wall_volume`

wall_volume

- **Çıktı:** `object`
- **Parametreler:** yok


## Görünüm

### `check_untagged_elements`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `category`, `family_type`, `level`, `tag_count`

### `create_view_filter` 🔒

Input: List<View>. rule_operator: contains|equals|begins_with

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `filter_name` (String, zorunlu), `categories` (String, zorunlu), `param_name` (String, varsayılan: 'System Classification'), `rule_operator` (String, varsayılan: 'contains'), `rule_value` (String, zorunlu), `color_r` (Int, varsayılan: '0'), `color_g` (Int, varsayılan: '0'), `color_b` (Int, varsayılan: '255'), `line_weight` (Int, varsayılan: '4'), `fill_pattern` (String, varsayılan: 'Solid Fill'), `overwrite` (Bool, varsayılan: 'false')
- **Çıktı alanları:** `view_id`, `view_name`, `filter_id`, `filter_name`, `applied`, `status`

### `detect_undefined_system`

Input boşsa tüm MEP taranır. discipline: hvac|plumbing|electrical|all

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `discipline` (String, varsayılan: 'all')
- **Çıktı alanları:** `element_id`, `category`, `family_type`, `level`, `system_type`, `fix_hint`

### `tag_elements` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `tag_type_name` (String, zorunlu), `view_id` (String, varsayılan: ''), `leader` (Bool, varsayılan: 'false'), `orientation` (String, varsayılan: 'horizontal')


## IFC

### `ifc_export`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `output_dir` (String, varsayılan: 'Environment.GetFolderPath(Environment.Sp'), `file_name` (String, varsayılan: '$"{Path.GetFileNameWithoutExtension(rctx'), `ifc_version` (String, varsayılan: 'IFC2x3'), `export_linked_files` (Bool, varsayılan: 'false')


## Kapı

### `door_clearance_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_clear_width_mm` (Double, varsayılan: '850'), `min_latch_side_mm` (Double, varsayılan: '300'), `severity` (String, varsayılan: 'WARNING')

### `door_fire_rating_from_wall` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `wall_param` (String, varsayılan: 'Yangın Dayanım Süresi'), `door_param` (String, varsayılan: 'EG_YanginDayanim'), `rating_map` (String, varsayılan: '')

### `door_handing_detect`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `handing`, `swing`, `hand_flipped`, `facing_flipped`, `facing_x`, `facing_y`, `hand_x`, `hand_y`

### `door_number_by_room` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'Mark'), `separator` (String, varsayılan: '-'), `start_index` (Int, varsayılan: '1')

### `room_door_relation_map`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `door_count`, `door_ids`, `door_marks`


## Koordinasyon

### `clash_detect_matrix`

İki kategori grubu (A vs B) arasında hard clash tespiti (BBox + Solid)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `None`
- **Parametreler:** `group_a` (String, zorunlu), `group_b` (String, zorunlu), `tolerance_mm` (Int32, varsayılan: 10), `max_results` (Int32, varsayılan: 1000)
- **Çıktı alanları:** `a_id`, `a_category`, `b_id`, `b_category`, `clash_x`, `clash_y`, `clash_z`, `overlap_volume_m3`, `disiplin_cifti`, `seviye`

### `clash_severity_sort`

Clash bulgularını kesişim hacmine göre önceliklendirir

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok
- **Çıktı alanları:** `sira_no`

### `coord_check_clearance`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_clearance_mm` (Double, varsayılan: '50.0')

### `coord_validate_level_consistency`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `coord_validate_penetration_firestop`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `place_opening` 🔒

smart_check_mep_no_opening bulgularına göre boşluk aile örnekleri yerleştirir.

- **Çıktı:** `object`
- **Parametreler:** yok

### `smart_check_mep_no_opening`

MEP elemanlarının yapısal eleman (duvar/döşeme/kiriş) geçişlerini tarar.

- **Çıktı:** `object`
- **Parametreler:** yok


## Liste

### `list_cross_product`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `second_key` (String, zorunlu)

### `list_filter_by_rule`

operator: eq|ne|gt|lt|gte|lte|contains|starts_with

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `field` (String, zorunlu), `operator` (String, zorunlu), `value` (String, zorunlu)

### `list_flatten`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `levels` (Int, varsayılan: '1')

### `list_group_by_key`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `key_field` (String, zorunlu)
- **Çıktı alanları:** `key_field_value`, `items`, `count`

### `list_map`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `template` (String, varsayılan: ''), `field` (String, varsayılan: ''), `output_field` (String, varsayılan: '_mapped')

### `list_sort_by`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `sort_field` (String, zorunlu), `ascending` (Bool, varsayılan: 'true')

### `list_take_every_n`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `n` (Int, zorunlu), `offset` (Int, varsayılan: '0')

### `list_transpose`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** yok

### `list_zip`

second_key: manifest'teki ikinci listenin step_id'si (Vars'tan okunur)

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `second_key` (String, zorunlu)


## Maliyet

### `beton_metraj`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `calc_cost`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `quantity_field` (String, varsayılan: 'hacim_m3')
- **Çıktı alanları:** `miktar`, `toplam_maliyet`

### `cost_by_level`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `cost_summary`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `group_by` (String, varsayılan: 'kategori')

### `kalip_all`

Tüm yapısal kategorilerin kalıp alanını tek geçişte hesaplar (kolon, kiriş, duvar, döşeme, temel)

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Element>`
- **Parametreler:** `include_edges` (String, varsayılan: '')

### `kalip_column`

Kolon kalıp alanı. Ana gövde: (çevre−duvar_temas)×H. Kolon başı: her yüz kiriş/döşeme düşümü.

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `kalip_floor`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `kalip_summary`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `group_by` (String, varsayılan: 'kat')

### `kalip_wall`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `kalip_write_back`

Kalıp satırlarını Revit parametrelerine yazar: Formwork_Area, TR_KalipAlani, TR_KalipPozNo, TR_KalipToplamTutar

- **Çıktı:** `Dictionary`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok

### `poz_match`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `category_field` (String, varsayılan: 'kategori'), `type_field` (String, varsayılan: 'tip')
- **Okur:** `poz_data`
- **Çıktı alanları:** `poz_no`, `poz_adi`, `birim`, `birim_fiyat`

### `poz_match_by_code`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `poz_code_field` (String, varsayılan: 'poz_no')
- **Okur:** `poz_data`
- **Çıktı alanları:** `poz_adi`, `birim`, `birim_fiyat`

### `poz_match_keynote_aware`

Üç aşamalı poz çözümleme: 1.Keynote/TR_CSB_PozNo 2.Semantic canonical map 3.Kategori varsayılanı

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok

### `wall_area`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok


## Mekansal

### `build_spatial_graph`

Input: room_boundary_extract çıktısı. Her satır bir kenar (edge). to_room_id=null → dış cephe.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `include_exterior` (Bool, varsayılan: 'true'), `deduplicate` (Bool, varsayılan: 'true')
- **Çıktı alanları:** `from_room_id`, `from_room_name`, `from_room_number`, `to_room_id`, `to_room_name`, `to_room_number`, `shared_wall_id`, `shared_wall_type`, `edge_length_mm`, `fire_rating`, `is_exterior`


## MEP Denetim

### `cable_tray_fill_check`

Pre-check: EG_KabloDoluluk (%) parametresi yoksa PARAM_MISSING.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `fill_param` (String, varsayılan: 'EG_KabloDoluluk'), `max_fill_pct` (Double, varsayılan: '70')

### `conduit_fill_check`

Pre-check: EG_IletkenSayisi + EG_IletkenKesit_mm2 yoksa PARAM_MISSING.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `count_param` (String, varsayılan: 'EG_IletkenSayisi'), `area_param` (String, varsayılan: 'EG_IletkenKesit_mm2'), `max_fill_pct` (Double, varsayılan: '40')

### `duct_aspect_ratio_check`

Sadece dikdörtgen kanallar. Dairesel kanallar atlanır.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_ratio` (Double, varsayılan: '4.0'), `severity` (String, varsayılan: 'WARNING')

### `fa_device_schedule`

Pre-check: EG_Loop + EG_Zone yoksa PARAM_MISSING.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `loop_param` (String, varsayılan: 'EG_Loop'), `zone_param` (String, varsayılan: 'EG_Zone'), `circuit_param` (String, varsayılan: 'EG_Circuit')
- **Çıktı alanları:** `device_type`, `level`, `zone`, `loop`, `circuit`, `quantity`

### `lighting_emergency_check`

Pre-check: EG_AcilAydinlatma (Yes/No) yoksa PARAM_MISSING. Sadece acil=Yes fixture'lar kontrol edilir.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `emergency_param` (String, varsayılan: 'EG_AcilAydinlatma'), `emergency_panel_pattern` (String, varsayılan: 'EP')

### `panel_phase_balance_check`

Pre-check: devrelere yük atanmış olmalı. status: OK|PARAM_MISSING|WARNING

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_imbalance_pct` (Double, varsayılan: '10.0')
- **Çıktı alanları:** `panel_id`, `panel_name`, `phase_a_va`, `phase_b_va`, `phase_c_va`, `total_va`, `max_imbalance_pct`, `circuit_count`, `status`

### `sprinkler_head_schedule`

Pre-check: K_Factor yoksa PARAM_MISSING. Tip/zone/kat bazlı gruplar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `k_factor_param` (String, varsayılan: 'K_Factor'), `coverage_param` (String, varsayılan: 'EG_KapsaAlan_m2'), `zone_param` (String, varsayılan: 'EG_Zone')
- **Çıktı alanları:** `type_name`, `k_factor`, `coverage_m2`, `zone`, `level`, `quantity`

### `valve_type_classify`

Sınıflar: GATE|GLOBE|BALL|BUTTERFLY|CHECK|RELIEF|NEEDLE_ANGLE|DIAPHRAGM|SOLENOID|STRAINER|UNKNOWN

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `family_name`, `type_name`, `valve_class`, `system_type`, `diameter_mm`


## MEP Hesap

### `calc_ach_airflow`

Input: List<Room> veya boş (params ile). mode: normal(6ACH)|smoke(10ACH)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `area_m2` (Double, varsayılan: '0'), `height_m` (Double, varsayılan: '0'), `ach` (Double, varsayılan: '6'), `mode` (String, varsayılan: 'normal')
- **Çıktı alanları:** `room_id`, `room_name`, `area_m2`, `height_m`, `volume_m3`, `ach`, `airflow_cmh`, `airflow_cfm`, `fan_option_4x_cmh`, `fan_option_6x_cmh`

### `calc_brick_quantity`

brick_type: yarım(10cm)|tam(19cm)|1.5(29cm)|2(39cm)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `area_m2` (Double, varsayılan: '0'), `thickness_cm` (Double, varsayılan: '19'), `brick_type` (String, varsayılan: 'tam'), `mortar_ratio` (Double, varsayılan: '0.25'), `waste_pct` (Double, varsayılan: '7.5')
- **Çıktı alanları:** `wall_id`, `wall_type`, `area_m2`, `thickness_cm`, `wall_volume_m3`, `mortar_m3`, `net_brick_m3`, `brick_count`, `waste_pct`, `total_with_waste`

### `calc_hazen_williams`

Input: List<Pipe> veya params. c_factor: galvaniz=120, PE/PVC=150, çelik=100

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `flow_rate_lpm` (Double, varsayılan: '0'), `pipe_diam_mm` (Double, varsayılan: '0'), `c_factor` (Double, varsayılan: '120'), `pipe_length_m` (Double, varsayılan: '1.0')
- **Çıktı alanları:** `pipe_id`, `diam_mm`, `flow_lpm`, `c_factor`, `length_m`, `friction_loss_bar_per_m`, `total_loss_bar`, `velocity_m_s`

### `calc_room_lux`

Pre-check: lumen_param fixture family'de yoksa PARAM_MISSING döner.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `lumen_param` (String, varsayılan: 'InitialIntensity'), `cu` (Double, varsayılan: '0.60'), `mf` (Double, varsayılan: '0.80'), `target_lux` (Double, varsayılan: '300')
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `area_m2`, `fixture_count`, `total_lumens`, `cu`, `mf`, `avg_lux`, `target_lux`, `status`


## MEP HVAC

### `assign_flow_to_terminals` 🔒

Her mahalin hesaplanan supply air flow degerini, icindeki supply hava terminallerine esit bolup yazar (terminal Flow parametresi). Revit ic birimi ft3/s, CFM=x60.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Space)`
- **Parametreler:** `round_cfm_to` (Double, varsayılan: 5), `flow_param_name` (String, varsayılan: 'Flow'), `system_type_filter` (String, varsayılan: 'Supply Air'), `only_with_flow` (Bool, varsayılan: True)
- **Yazar:** `Flow`
- **Çıktı alanları:** `space_id`, `space_name`, `space_number`, `terminal_count`, `supply_cfm`, `cfm_per_terminal`, `status`

### `populate_space_param` 🔒

Mahallere hesaplanmis bir parametre degeri yazar (orn 'CFM per SF' = supply_cfm/alan_ft2). source: cfm_per_sf|supply_cfm|constant.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Space)`
- **Parametreler:** `target_param` (String, zorunlu), `source` (String, varsayılan: 'cfm_per_sf'), `value` (Double, varsayılan: 0), `skip_zero_area` (Bool, varsayılan: True)
- **Çıktı alanları:** `space_id`, `space_name`, `space_number`, `value`, `status`

### `resize_diffuser_by_flow` 🔒

Terminalin debisine gore uygun diffuzor tipini (FamilySymbol) esik tablosundan secip ChangeTypeId ile uygular. thresholds: 'cfm:type' ciftleri.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (FamilyInstance/DuctTerminal)`
- **Parametreler:** `family_name` (String, zorunlu), `flow_param_name` (String, varsayılan: 'Flow'), `thresholds` (String, zorunlu), `system_type_filter` (String, varsayılan: 'Supply Air')
- **Okur:** `Flow`
- **Çıktı alanları:** `terminal_id`, `current_cfm`, `old_type`, `new_type`, `changed`, `status`


## MEP Koordinasyon

### `mep_region_count`

Kapalı bölge (Room/Area) içindeki MEP elemanlarını sayar ve uzunluk toplar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `region_category` (String, varsayılan: 'OST_Rooms'), `mep_categories` (String, varsayılan: 'OST_PipeCurves,OST_DuctCurves,OST_CableTray'), `z_tolerance_mm` (Int32, varsayılan: 1500)
- **Çıktı alanları:** `region_id`, `region_name`, `region_number`, `level_name`, `area_m2`, `mep_count`, `total_length_m`, `pipe_count`, `duct_count`, `tray_count`, `by_category`

### `mep_region_tag` 🔒

Kapalı bölge içindeki MEP elemanlarına bölge adı/numarasını parametre yazar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `region_category` (String, varsayılan: 'OST_Rooms'), `mep_categories` (String, varsayılan: 'OST_PipeCurves,OST_DuctCurves,OST_CableTray'), `target_param` (String, varsayılan: 'Comments'), `write_mode` (String, varsayılan: 'name_number'), `z_tolerance_mm` (Int32, varsayılan: 1500)
- **Çıktı alanları:** `region_id`, `region_name`, `tagged_count`, `skipped_count`

### `mep_straighten_apply` 🔒

Tespit edilen S-bendleri düzleştirir (dirsek+ara segment siler, ana hattı uzatır).

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `max_apply` (Int32, varsayılan: 0)
- **Çıktı alanları:** `elbow_a_id`, `elbow_b_id`, `status`, `message`

### `mep_straighten_scan`

MEP hatlarındaki çift-dirsek sapmalarını (S-bend) tespit eder. Yalnızca rapor.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `categories` (String, varsayılan: 'OST_DuctCurves,OST_PipeCurves,OST_CableTray'), `min_offset_mm` (Int32, varsayılan: 5), `max_offset_mm` (Int32, varsayılan: 600), `max_results` (Int32, varsayılan: 2000)
- **Çıktı alanları:** `elbow_a_id`, `elbow_b_id`, `middle_ids`, `offset_mm`, `system_name`, `category`, `anchor_id`, `mover_id`, `center_x`, `center_y`, `center_z`


## MEP-Elektrik

### `elec_check_circuit_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elec_check_emergency_lighting`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elec_circuit_diff`

Mevcut modeli onceki devre snapshot'i ile karsilastirir; EKLENEN/SILINEN/DEGISEN devreleri ve degisen alani saha icin raporlar. UniqueId ile eslestirir.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `-`
- **Parametreler:** `baseline_path` (String, zorunlu), `output_path` (String, varsayılan: ''), `panel_filter` (String, varsayılan: ''), `load_tolerance_va` (Double, varsayılan: 1)
- **Çıktı alanları:** `change_type`, `panel`, `circuit_number`, `field`, `old_value`, `new_value`, `detail`

### `elec_circuit_snapshot`

Modeldeki tum elektrik devrelerinin durumunu JSON snapshot'a yazar (onayli tasarim ani). elec_circuit_diff ile karsilastirilir.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (ElectricalSystem)`
- **Parametreler:** `output_path` (String, zorunlu), `panel_filter` (String, varsayılan: '')
- **Çıktı alanları:** `circuit_id`, `panel`, `circuit_number`, `load_va`, `poles`, `status`

### `elec_conduit_calc_iec` 🔒

Conduit uzunlugunu okur, EG_DevreNo ile ElectricalSystem'i eslestirir, IEC 60364 kablo secimi (ampacity+gerilim dusumu+kisa devre) + conduit fill hesabi yapar, cikti parametrelerini conduit'e yazar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Conduit)`
- **Parametreler:** `voltage` (Double, varsayılan: 400), `vdrop_limit_pct` (Double, varsayılan: 5), `max_fill_pct` (Double, varsayılan: 40), `ampacity_table_path` (String, varsayılan: ''), `only_with_circuit` (Bool, varsayılan: True)
- **Okur:** `EG_DevreNo`, `EG_KurulumMetodu`, `EG_Yalitim`, `EG_Iletken`, `EG_OrtamSicaklik`, `EG_GruplamaAdet`, `EG_GucFaktoru`, `EG_YapmaPayi`
- **Yazar:** `EG_ModelUzunluk`, `EG_KabloUzunluk`, `EG_HesapAkim`, `EG_KabloKesiti`, `EG_FazKesit_mm2`, `EG_GerilimDusumu`, `EG_GerilimDusumuV`, `EG_SigortaOneri`, `EG_DolulukYuzde`, `EG_KisaDevreKesiti`, `EG_HesapDurumu`, `EG_HesapTarihi`
- **Çıktı alanları:** `conduit_id`, `devre`, `uzunluk_m`, `akim_a`, `kesit_mm2`, `gerilim_dusumu_pct`, `doluluk_pct`, `sigorta_a`, `durum`

### `elec_conduit_schedule`

Conduit hesap sonuclarindan From-To-Devre-Kesit-Uzunluk-GerilimDusumu-Fill cetveli uretir (HTML/CSV).

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Conduit)`
- **Parametreler:** `output_path` (String, zorunlu), `only_calculated` (Bool, varsayılan: True)
- **Okur:** `EG_DevreNo`, `EG_Kaynak`, `EG_Hedef`, `EG_KabloKesiti`, `EG_KabloUzunluk`, `EG_HesapAkim`, `EG_GerilimDusumu`, `EG_DolulukYuzde`, `EG_SigortaOneri`, `EG_HesapDurumu`
- **Çıktı alanları:** `devre`, `kaynak`, `hedef`, `kesit`, `uzunluk_m`, `gerilim_dusumu_pct`, `doluluk_pct`, `sigorta_a`, `durum`

### `elec_generate_panel_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elec_setup_conduit_params` 🔒

EGBIM elektrik hesap shared parametrelerini (sabit GUID) projeye yukler, Conduit+ElectricalCircuit kategorilerine bind. Hesaptan once bir kez calistirilir.

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: 'mapping/EGBIM_ElektrikParams.txt')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `elec_validate_lux_level`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_lux` (Double, varsayılan: '300.0')

### `elec_validate_panel_load`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_load_kva` (Double, varsayılan: '100.0')


## MEP-Mekanik

### `duct_section_convert_apply` 🔒

duct_section_convert_preview çıktısındaki yeni kanal boyutlarını modele yazar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `skip_warnings` (String, varsayılan: 'false')
- **Çıktı alanları:** `duct_id`, `status`, `message`, `new_w_mm`, `new_h_mm`

### `duct_section_convert_preview`

Dikdörtgen kanalları eş-kesit (eşdeğer çap korumalı) yeniden boyutlandırır. Yalnızca hesap.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `fix_dimension` (String, varsayılan: 'height'), `fixed_value_mm` (Double, zorunlu), `round_to_mm` (Double, varsayılan: 50), `max_aspect_ratio` (Double, varsayılan: 4.0), `only_round_ducts` (String, varsayılan: 'false')
- **Çıktı alanları:** `duct_id`, `system_name`, `old_w_mm`, `old_h_mm`, `old_de_mm`, `new_w_mm`, `new_h_mm`, `new_de_mm`, `aspect_ratio`, `de_error_pct`, `warning`

### `mep_air_terminal_space_map`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_validate_duct_size`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_size_mm` (Double, varsayılan: '100.0')

### `mep_validate_duct_slope`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_slope_pct` (Double, varsayılan: '0.5')

### `mep_validate_duct_velocity`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_velocity_m_s` (Double, varsayılan: '6.0')

### `mep_validate_space_hvac_zone`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok


## MEP-Sıhhi

### `plumbing_assign_units` 🔒

Armaturlere EN 12056 DU ve EN 806 LU degerlerini atar (tip tablosundan). Tip bossa armatur adindan tahmin.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (PlumbingFixture)`
- **Parametreler:** `overwrite` (Bool, varsayılan: False)
- **Okur:** `EG_ArmaturTipi`, `EG_DU`
- **Yazar:** `EG_ArmaturTipi`, `EG_DU`, `EG_LU_Soguk`, `EG_LU_Sicak`
- **Çıktı alanları:** `fixture_id`, `tip`, `du`, `lu_soguk`, `lu_sicak`, `durum`

### `plumbing_calc_en` 🔒

Boru bolumlerine bagli armaturlerin DU/LU'larini toplar, EN 12056 (Qww=K√ΣDU) / EN 806 (QD) hesabi, cap secimi, drenaj dolum/egim/hiz kontrolu; sonuclari boruya yazar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Pipe)`
- **Parametreler:** `pipe_role` (String, varsayılan: 'auto'), `min_slope_pct` (Double, varsayılan: 1.0), `max_fill_pct` (Double, varsayılan: 70), `capacity_table_path` (String, varsayılan: '')
- **Okur:** `EG_DU`, `EG_LU_Soguk`, `EG_LU_Sicak`, `EG_DrenajSistem`, `EG_SuSistem`, `EG_BinaKullanim`
- **Yazar:** `EG_ToplamDU`, `EG_AtikDebi_Qww`, `EG_ToplamLU`, `EG_TasarimDebi_QD`, `EG_OneriCap_DN`, `EG_BoruKapasite`, `EG_DolulukOrani`, `EG_AkisHizi`, `EG_EgimYuzde`, `EG_HesapDurumu`, `EG_HesapTarihi`
- **Çıktı alanları:** `pipe_id`, `rol`, `sistem`, `toplam_du_lu`, `debi_l_s`, `dn_mm`, `durum`

### `plumbing_calc_flow_rate`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `plumbing_check_fixture_room_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `plumbing_schedule`

Sihhi tesisat hesap sonuclarindan cetvel uretir (HTML/CSV): sistem, rol, DU/LU, debi, DN, dolum, egim/hiz, durum.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `-`
- **Parametreler:** `output_path` (String, zorunlu), `only_calculated` (Bool, varsayılan: True)
- **Okur:** `EG_AtikDebi_Qww`, `EG_TasarimDebi_QD`, `EG_ToplamDU`, `EG_ToplamLU`, `EG_OneriCap_DN`, `EG_DolulukOrani`, `EG_EgimYuzde`, `EG_AkisHizi`, `EG_HesapDurumu`
- **Çıktı alanları:** `sistem`, `rol`, `debi_l_s`, `dn_mm`, `durum`

### `plumbing_setup_params` 🔒

EGBIM sihhi tesisat shared parametrelerini (sabit GUID) yukler, PlumbingFixtures+PipeCurves'e bind. Hesaptan once bir kez.

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: 'mapping/EGBIM_SihhiParams.txt')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `plumbing_validate_connector_diameter`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `expected_diameter_mm` (Double, varsayılan: '50.0'), `tolerance_mm` (Double, varsayılan: '5.0')

### `plumbing_validate_pipe_slope`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_slope_pct` (Double, varsayılan: '1.0')

### `plumbing_validate_system_separation`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok


## Mimari

### `arch_apply_view_template`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `template_name` (String, varsayılan: ''), `view_name_contains` (String, varsayılan: '')

### `arch_check_fire_rating_continuity`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_fire_rating` (String, varsayılan: '60')

### `arch_check_material_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `arch_check_windowless_rooms`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `arch_renumber_doors` 🔒

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `prefix` (String, varsayılan: 'K')

### `arch_sheets_from_data` 🔒

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `arch_validate_accessibility`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_width_mm` (Double, varsayılan: '850.0')

### `arch_validate_ceiling_height`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_height_mm` (Double, varsayılan: '2400.0')

### `arch_validate_room_area`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_area_m2` (Double, varsayılan: '0.0')

### `arch_validate_room_naming`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok


## Modelleme

### `create_3d_view` 🔒

- **Çıktı:** `Element`
- **Girdi:** `List<Element>`
- **Parametreler:** `view_name` (String, varsayılan: ''), `padding_mm` (Double, varsayılan: '500')

### `create_sheet` 🔒

- **Çıktı:** `Element`
- **Girdi:** `—`
- **Parametreler:** `sheet_number` (String, zorunlu), `sheet_name` (String, zorunlu), `title_block_name` (String, varsayılan: '')

### `mirror_element` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `axis` (String, varsayılan: 'Y'), `pivot_x_mm` (Double, varsayılan: '0'), `pivot_y_mm` (Double, varsayılan: '0'), `copy` (Bool, varsayılan: 'false')

### `move_element` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `dx_mm` (Double, varsayılan: '0'), `dy_mm` (Double, varsayılan: '0'), `dz_mm` (Double, varsayılan: '0')

### `place_family` 🔒

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, zorunlu), `x_mm` (Double, varsayılan: '0'), `y_mm` (Double, varsayılan: '0'), `z_mm` (Double, varsayılan: '0'), `rotation_deg` (Double, varsayılan: '0')

### `place_view_on_sheet` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `sheet_number` (String, zorunlu), `x_mm` (Double, varsayılan: '0'), `y_mm` (Double, varsayılan: '0')

### `rename_element` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'Name'), `value` (String, varsayılan: ''), `prefix` (String, varsayılan: ''), `suffix` (String, varsayılan: '')

### `set_element_type` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu)

### `set_level` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `level_name` (String, zorunlu)

### `set_phase` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `phase_name` (String, zorunlu), `phase_type` (String, varsayılan: 'created')

### `set_workset`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `workset_name` (String, zorunlu)

### `workset_by_level` 🔒

Her kat (Level) için aynı isimde User Workset oluşturur.

- **Çıktı:** `object`
- **Parametreler:** yok


## Oda

### `room_area_breakdown`

Oda bazlı taban, duvar ve tavan alanlarını hesaplar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_boundary_extract`

loop_index=0 dış sınır, 1+ iç boşluklar (shaft vb). direction_deg: 0=doğu, 90=kuzey.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `include_openings` (Bool, varsayılan: 'true'), `fire_param` (String, varsayılan: 'Yangın Dayanım Süresi')
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `loop_index`, `segment_index`, `host_wall_id`, `host_wall_type`, `fire_rating`, `is_exterior`, `start_x`, `start_y`, `end_x`, `end_y`, `length_mm`, `direction_deg`, `has_opening`

### `room_finish_assign` 🔒

Oda ismi veya fonksiyon parametresine göre kaplama tiplerini toplu atar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_finish_matrix`

Tüm odaları kaplama bilgileriyle tablo haline getirir.

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_finish_validate`

Odaların kaplama parametrelerinin dolu olup olmadığını kontrol eder.

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_naming_normalize` 🔒

Oda isimlerini EGBIM standardına göre normalize eder.

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_to_ifc_space` 🔒

Oda fonksiyon tipini IFC Space tipine eşler ve EG_IfcSpaceTip parametresini yazar.

- **Çıktı:** `object`
- **Parametreler:** yok


## Oluşturma

### `create_floor` 🔒

input: List<Room> veya boş (points param ile)

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu), `level_name` (String, zorunlu), `points` (String, varsayılan: ''), `offset_mm` (Double, varsayılan: '0')

### `create_grid` 🔒

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `x1_mm` (Double, zorunlu), `y1_mm` (Double, zorunlu), `x2_mm` (Double, zorunlu), `y2_mm` (Double, zorunlu), `name` (String, varsayılan: '')

### `create_level` 🔒

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `elevation_mm` (Double, zorunlu), `name` (String, varsayılan: '')

### `create_room` 🔒

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `x_mm` (Double, varsayılan: '0'), `y_mm` (Double, varsayılan: '0'), `level_name` (String, zorunlu), `name` (String, varsayılan: 'Oda'), `number` (String, varsayılan: '')

### `create_wall` 🔒

mode: from_lines | between_lines | from_room

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `mode` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, zorunlu), `height_mm` (Double, varsayılan: '3000'), `reference` (String, varsayılan: 'centerline'), `flip` (Bool, varsayılan: 'false'), `structural` (Bool, varsayılan: 'false'), `skip_existing` (Bool, varsayılan: 'true')


## Parametre

### `add_shared_params` 🔒

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `spf_path` (String, varsayılan: ''), `group_filter` (String, varsayılan: '')

### `assign_poz_number` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'EGBIM_PozNo'), `prefix` (String, varsayılan: ''), `start_from` (Int, varsayılan: '1')

### `copy_param` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `source_param` (String, zorunlu), `target_param` (String, zorunlu)

### `family_ensure_loaded` 🔒

- **Çıktı:** `bool`
- **Girdi:** `—`
- **Parametreler:** `family_path` (String, zorunlu)

### `generate_ids`

- **Çıktı:** `string`
- **Girdi:** `List<Element>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO IDS'), `output_path` (String, varsayılan: 'DataRoot/default')

### `list_shared_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `param_exists`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu)

### `param_validate_schema`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `schema_path` (String, varsayılan: '')

### `read_builtin_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `builtin_param` (String, zorunlu)

### `read_param` 🔒

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu)

### `read_param_with_fallback`

Elemandan öncelikli parametre listesinden ilk dolu değeri okur. params: param_names[], output_field

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (String, varsayılan: ''), `output_field` (String, varsayılan: '')

### `read_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (String, zorunlu)

### `type_get`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `type_read_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu)

### `validate_required_params`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `required_params` (String, zorunlu)

### `write_param`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, zorunlu), `value` (String, zorunlu)

### `write_param_from_rows` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_name` (String, zorunlu), `value_field` (String, zorunlu)

### `write_row_param` 🔒

- **Çıktı:** `int`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_name` (String, zorunlu), `value_key` (String, zorunlu)


## Proje Yönetimi

### `pm_get_project_info`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `pm_model_delta_summary`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `pm_validate_lod`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `lod_level` (String, varsayılan: 'LOD300')

### `pm_validate_naming_convention`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `name_pattern` (String, varsayılan: '')


## QA/QC

### `qa_check_approved_families`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_check_level_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_detect_duplicates`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `tolerance_mm` (Double, varsayılan: '10.0')

### `qa_find_empty_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_find_redundant_rooms`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_get_model_warnings`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `qa_model_size_analysis`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `qa_validate_coordinates`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `qa_validate_phase_consistency`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_validate_workset`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `expected_workset` (String, varsayılan: '')


## Raporlama

### `kalip_export_xlsx`

Kalıp hesap sonuçlarını Excel BOQ formatında dışa aktarır.

- **Çıktı:** `object`
- **Parametreler:** yok

### `kalip_report`

Kalıp hesap sonuçlarını profesyonel HTML raporuna dönüştürür.

- **Çıktı:** `object`
- **Parametreler:** yok

### `schedule_export_anchored`

Schedule'ı GetCellText ile birebir okur + UID anchor ekler (rolled-back). Model değişmez.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `None`
- **Parametreler:** `schedule_name` (String, zorunlu), `include_anchor` (String, varsayılan: 'true')
- **Çıktı alanları:** `__egbimoto_uid__`, `(schedule sütunları dinamik)`

### `schedule_roundtrip_apply` 🔒

schedule_roundtrip_diff değişikliklerini güvenle yazar (per-Set doğrulamalı).

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `apply_type_params` (String, varsayılan: 'true')
- **Çıktı alanları:** `uid`, `field`, `old_value`, `new_value`, `status`, `message`

### `schedule_roundtrip_diff`

Düzenlenmiş schedule satırlarını model ile karşılaştırır (UID eşleşmeli). Yazma yok.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `ignore_fields` (String, varsayılan: '')
- **Çıktı alanları:** `uid`, `field`, `old_value`, `new_value`, `writable`, `binding`, `note`


## Script

### `clear_script_cache`

- **Çıktı:** `Dictionary`
- **Girdi:** `Any`
- **Parametreler:** yok
- **Çıktı alanları:** `cleared`

### `run_csharp_script`

- **Çıktı:** `Dictionary`
- **Girdi:** `Any`
- **Parametreler:** `script_path` (String, zorunlu), `cache` (Boolean, varsayılan: True)
- **Çıktı alanları:** `result`


## Semantik

### `assign_egbim_mark`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `classify_by_wbs`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Okur:** `wbs_mapping`

### `classify_elements` 🔒

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `map_to_ifc`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Okur:** `ifc_mapping`

### `resolve_canonical_class`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `type_name` (String, zorunlu), `category` (String, zorunlu)

### `resolve_discipline`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `category` (String, zorunlu)


## Sistem

### `eg_addin_disable_unused`

Zorunlu olmayan add-inleri .EGdisabled olarak devre dışı bırakır

- **Çıktı:** `Dictionary`
- **Girdi:** `None`
- **Parametreler:** `keep` (List<String>, varsayılan: []), `revit_version` (String, varsayılan: '')
- **Çıktı alanları:** `disabled_count`, `skipped_readonly`, `failed_count`, `restart_required`

### `eg_addin_restore_all`

Tüm .EGdisabled / .RSTdisabled add-inleri geri yükler

- **Çıktı:** `Dictionary`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: '')
- **Çıktı alanları:** `restored_count`, `failed_count`, `restart_required`

### `eg_addin_restore_single`

Tek bir add-ini geri yükler (addin_file: pyRevit.addin)

- **Çıktı:** `bool`
- **Girdi:** `None`
- **Parametreler:** `addin_file` (String, zorunlu), `revit_version` (String, varsayılan: '')

### `eg_addin_scan`

Kurulu Revit add-inleri tarar ve listeler

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: ''), `include_disabled` (Boolean, varsayılan: True)
- **Çıktı alanları:** `fileName`, `disabled`, `directory`, `name`, `addinId`

### `eg_health_snapshot`

RAM/CPU/Disk/OS/Revit/Warnings sağlık raporu üretir (html|json|text)

- **Çıktı:** `string`
- **Girdi:** `None`
- **Parametreler:** `format` (String, varsayılan: 'html'), `open` (Boolean, varsayılan: False), `out_path` (String, varsayılan: '')
- **Çıktı alanları:** `out_path`


## Toplama

### `collect_air_terminals`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_beams`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_by_category`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: 'OST_Walls')

### `collect_by_ids`

Dict satırlarındaki ID alan(lar)ından Element listesi toplar. Satır-tabanlı çıktıları Element bekleyen op'lara köprüler.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `id_fields` (String, varsayılan: 'element_id'), `distinct` (String, varsayılan: 'true')

### `collect_by_phase`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `phase_name` (String, zorunlu)

### `collect_by_type_name`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `type_name` (String, zorunlu)

### `collect_by_workset`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `workset_name` (String, zorunlu)

### `collect_cable_trays`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_casework`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_ceilings`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_columns`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_conduits`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_curtain_walls`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_doors`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_duct_fittings`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_ducts`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_electrical_equipment`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_electrical_fixtures`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_elements`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: 'OST_Walls')

### `collect_families`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: '')

### `collect_fire_alarm_devices`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_floors`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_foundations`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_furniture`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_furniture_systems`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_generic_models`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_grids`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_levels`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_lighting_devices`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_lighting_fixtures`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_linked_elements`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: 'OST_Walls')

### `collect_mechanical_equipment`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_multi`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `categories` (String, zorunlu)

### `collect_parking`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_pipe_accessories`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_pipe_fittings`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_pipes`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_plumbing_fixtures`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_ramps`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_rebar`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_rebar_in_host`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `host_id` (Int, varsayılan: '-1')

### `collect_roofs`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_rooms`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_selected`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_sheets`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_site`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_sprinklers`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_stairs`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_structural_columns`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_structural_framing`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_structural_walls`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_topography`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_types`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `category` (String, varsayılan: '')

### `collect_views`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `view_type` (String, varsayılan: 'All')

### `collect_walls`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok

### `collect_windows`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** yok


## Trace

### `compare_run_result`

status: new|existing|removed

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu)
- **Çıktı alanları:** `element_id`, `status`

### `delete_generated` 🔒

confirm=true zorunlu — güvenlik kilidi.

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** `trace_key` (String, zorunlu), `confirm` (Bool, varsayılan: 'false')

### `trace_find_existing`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `trace_key` (String, zorunlu)

### `trace_write` 🔒

RevitExtensibleStorage ile modele kalıcı yazar.

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu)

### `update_or_create_family` 🔒

Dynamo element binding karşılığı. action: created|updated|skipped|error

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu), `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, varsayılan: ''), `update_type` (Bool, varsayılan: 'true'), `update_location` (Bool, varsayılan: 'false')
- **Çıktı alanları:** `element_id`, `index`, `action`, `status`


## Veri

### `assign_wbs_code`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `category_field` (String, varsayılan: 'kategori')
- **Okur:** `wbs_mapping`
- **Çıktı alanları:** `wbs_kodu`

### `data_get`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `key` (String, zorunlu)

### `data_list_keys`

- **Çıktı:** `List<string>`
- **Girdi:** `—`
- **Parametreler:** yok

### `data_load`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `path` (String, zorunlu), `root_key` (String, varsayılan: ''), `cache_bust` (Bool, varsayılan: 'false'), `key` (String, varsayılan: 'Path.GetFileNameWithoutExtension(fullPat')

### `export_wbs_report`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `output_path` (String, varsayılan: 'DataRoot/default')

### `link_quantity_to_wbs`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `quantity_field` (String, varsayılan: 'hacim_m3'), `wbs_field` (String, varsayılan: 'wbs_kodu')

### `load_ifc_mapping`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `ifc_path` (String, varsayılan: 'DataRoot/default')
- **Yazar:** `ifc_mapping`

### `load_poz_data`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `file` (String, varsayılan: 'DataRoot/default')
- **Yazar:** `poz_data`
- **Çıktı alanları:** `birim_fiyat`

### `load_qa_matrix`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `qa_path` (String, varsayılan: 'DataRoot/default')
- **Yazar:** `qa_matrix`

### `load_rayic`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `rayic_path` (String, varsayılan: 'DataRoot/default')
- **Yazar:** `rayic_data`

### `load_shared_param_map`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `map_path` (String, varsayılan: 'DataRoot/default')
- **Yazar:** `shared_param_map`

### `load_wbs_mapping`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `wbs_path` (String, varsayılan: 'DataRoot/default')
- **Yazar:** `wbs_mapping`

### `lookup_value`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `key_field` (String, zorunlu), `lookup_key` (String, zorunlu), `result_field` (String, varsayılan: 'lookup_result')

### `pivot_table`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `row_field` (String, zorunlu), `col_field` (String, zorunlu), `value_field` (String, zorunlu), `func` (String, varsayılan: 'sum')


## Yangın

### `fa_classify_room_detector`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `fa_validate_circuit_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `fa_validate_device_in_room`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `fa_validate_mounting_height`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_aff_mm` (Double, varsayılan: '2000.0'), `max_aff_mm` (Double, varsayılan: '2400.0'), `device_filter` (String, varsayılan: '')

### `fp_validate_sprinkler_coverage`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_coverage_m2` (Double, varsayılan: '12.0')


## Yangın Hesap

### `calc_duct_sheet_weight`

Input: List<Duct> (dikdörtgen) veya params. Dairesel kanallar atlanır.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `thickness_mm` (Double, varsayılan: '0.8'), `density_kg_m3` (Double, varsayılan: '7850'), `width_mm` (Double, varsayılan: '0'), `height_mm` (Double, varsayılan: '0'), `length_m` (Double, varsayılan: '0')
- **Çıktı alanları:** `duct_id`, `width_mm`, `height_mm`, `length_m`, `perimeter_mm`, `thickness_mm`, `sheet_volume_m3`, `sheet_weight_kg`

### `calc_sprinkler_design_density`

TR Yangın Yönetmeliği Ek-8/B. hazard_class: dusuk|orta_1|orta_2|orta_3|yuksek_1|yuksek_2

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `hazard_class` (String, zorunlu), `system_type` (String, varsayılan: 'islak'), `area_m2` (Double, varsayılan: '0')
- **Çıktı alanları:** `hazard_class`, `design_density_mm_per_min`, `protection_area_m2`, `required_flow_lpm`, `total_pump_flow_lpm`, `reference`

### `fire_hose_cabinet_spacing_check`

En yakın komşuya mesafe × 2 hortum uzunluğu erişim analizi.

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_spacing_m` (Double, varsayılan: '30.0'), `hose_length_m` (Double, varsayılan: '20.0')


## Yapısal

### `column_setup_params` 🔒

EGBIM kolon on boyutlandirma shared parametrelerini (sabit GUID) yukler, StructuralColumns'a bind. Hesaptan once bir kez.

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: 'mapping/EGBIM_KolonParams.txt')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `structural_collect_all`

Tüm yapısal elementleri (kolon, kiriş, döşeme, perde, temel) toplar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `structural_column_presizing` 🔒

Betonarme kolonlari TBDY 2018/TS 500 kesit kosullarina gore KONTROL eder (min boyut 300mm, Ac≥Ndm/0.40fck, Nd≤0.9fcd·Ac, kiris/kolon ayrimi) ve min kesiti ONERIR. Ndm: EG_Ndm veya yuk alani tahmini. Sonuclari kolona yazar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (StructuralColumn)`
- **Parametreler:** `default_fck` (Double, varsayılan: 25), `default_birim_yuk` (Double, varsayılan: 12), `duktilite` (String, varsayılan: 'yuksek'), `round_to_mm` (Double, varsayılan: 50)
- **Okur:** `EG_Ndm`, `EG_YukAlani`, `EG_KatSayisi`, `EG_BirimYuk`, `EG_BetonSinif`, `EG_fck_Override`, `EG_KolonTipi`
- **Yazar:** `EG_MevcutB`, `EG_MevcutH`, `EG_MevcutAc`, `EG_KullanilanNdm`, `EG_GerekenAc_TBDY`, `EG_OneriBoyut`, `EG_MinBoyutKontrol`, `EG_EksenelKontrol_TBDY`, `EG_EksenelKontrol_TS500`, `EG_KirisKolonAyrim`, `EG_KolonDurumu`, `EG_HesapTarihi`
- **Çıktı alanları:** `kolon_id`, `b_mm`, `h_mm`, `ac_mm2`, `fck_mpa`, `ndm_kn`, `gereken_ac`, `oneri_boyut`, `min_boyut`, `tbdy_eksenel`, `ts500`, `durum`

### `structural_continuity_check`

Kolonların kat geçişlerinde Z ekseninde sürekli olup olmadığını kontrol eder.

- **Çıktı:** `object`
- **Parametreler:** yok

### `structural_level_summary`

Kat bazlı yapısal element özeti üretir.

- **Çıktı:** `object`
- **Parametreler:** yok

### `structural_material_check`

Yapısal elementlerin beton/çelik sınıf parametrelerinin dolu olup olmadığını kontrol eder.

- **Çıktı:** `object`
- **Parametreler:** yok

### `structural_tbdy_params` 🔒

Yapısal elementlere TBDY 2018 parametrelerini toplu yazar.

- **Çıktı:** `object`
- **Parametreler:** yok

### `structural_ts500_section`

TS 500 Madde 7 minimum kesit boyutlarını kontrol eder.

- **Çıktı:** `object`
- **Parametreler:** yok


## Yapısal Oluşturma

### `create_beam_by_curve` 🔒

Input: List<Curve>. line_by_points çıktısı ile kullanın.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, zorunlu), `offset_mm` (Double, varsayılan: '0')

### `create_column_by_point` 🔒

Input: List<XYZ>. points_grid veya table_to_points ile kullanın.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `base_level` (String, zorunlu), `top_level` (String, varsayılan: ''), `height_mm` (Double, varsayılan: '3000'), `structural` (Bool, varsayılan: 'true')

### `create_grid_by_line` 🔒

Input: List<Curve>. create_grid'in curve versiyonu.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `name_prefix` (String, varsayılan: 'G'), `start_index` (Int, varsayılan: '1')

### `place_adaptive_component_by_points` 🔒

Input: List<XYZ>. points_per_item nokta başına 1 instance.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `points_per_item` (Int, zorunlu)


## Yardımcı

### `assert_not_empty`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `message` (String, varsayılan: 'Beklenen veri bulunamadı.')

### `compute`

- **Çıktı:** `double`
- **Girdi:** `—`
- **Parametreler:** `field` (String, zorunlu), `func` (String, varsayılan: 'sum'), `a` (Double, zorunlu), `b` (Double, varsayılan: '1.0'), `op` (String, varsayılan: 'mul')

### `count_items`

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** yok

### `echo`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `message` (String, varsayılan: '')

### `element_count`

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** yok

### `eq`

Condition parser zaten == destekler; bu op manifest adımı olarak eşitlik kontrolü gerektiğinde kullanılır.

- **Çıktı:** `bool`
- **Parametreler:** `left` (Object, varsayılan: '$input'), `right` (Object, zorunlu)

### `flatten_list`

- **Çıktı:** `List<object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `format_message`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `template` (String, varsayılan: '')

### `format_number`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `value` (Double, zorunlu), `format` (String, varsayılan: 'N2'), `unit` (String, varsayılan: '')

### `log_message`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `message` (String, varsayılan: '—')

### `model_checksum`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `noop`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** yok

### `op_health_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `pass_through`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** yok

### `set_var`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `key` (String, zorunlu), `value` (String, zorunlu)

### `show_count`

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** yok

### `show_result`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO')

### `sum_field`

- **Çıktı:** `double`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu)

### `system_info`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `write_trace`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `message` (String, varsayılan: ''), `file_path` (String, varsayılan: '')


## Yerleştirme

### `calc_placement_point`

Girdi eleman listesinin ilk elemanının origin'inden offset+yükseklik ile XYZ üretir.

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `offset_x_mm` (Double, varsayılan: '0'), `offset_y_mm` (Double, varsayılan: '0'), `height_mm` (Double, varsayılan: '0')
- **Çıktı alanları:** `x`, `y`, `z`, `level_id`

### `collect_doors_in_room`

Input: List<Room>. Odanın boundary segmentlerinden host duvarlarını bulur, o duvarlardaki kapıları döner.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `get_door_wall_clearances`

Input: List<FamilyInstance door>. Her kapı için duvardaki sol/sağ serbest mesafeyi mm döner.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `left_mm`, `right_mm`, `wall_length_mm`, `door_width_mm`

### `get_room_ceiling_center`

Input: List<Room>. Her odanın tavan düzeyindeki merkez XYZ koordinatını döner.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `room_name`, `x`, `y`, `z`

### `place_family_along_mep` 🔒

Boru/kanal/kablo hattı boyunca belirtilen aralıkla aile (askı/destek) yerleştirir.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `spacing_mm` (Double, varsayılan: 2000), `end_setback_mm` (Double, varsayılan: 300), `vertical_offset_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `host_id`, `category`, `placed_count`, `spacing_mm`, `run_length_m`

### `place_family_on_ceiling` 🔒

Input: List<Ceiling>. Tavan yuzeyine aile yerlestirir. Face-based ise alt yuze host'lar, degilse merkez noktaya. Transaction gerektirir. Cikti: host_id, placed, mode (face|point|fail).

- **Çıktı:** `List<Dictionary<string,object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `offset_x_mm` (Double, varsayılan: '0'), `offset_y_mm` (Double, varsayılan: '0')
- **Çıktı alanları:** `host_id`, `placed`, `mode`

### `place_family_on_wall` 🔒

Input: List<Wall>. Duvara host'lu aile yerleştirir (kapı/pencere/aydınlatma). Transaction gerektirir. Cikti: host_id, placed_count, wall_length_m.

- **Çıktı:** `List<Dictionary<string,object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `offset_mm` (Double, varsayılan: '1200'), `spacing_mm` (Double, varsayılan: '0'), `sill_mm` (Double, varsayılan: '0')
- **Çıktı alanları:** `host_id`, `placed_count`, `wall_length_m`


## Çıktı

### `element_report`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Eleman Raporu')

### `export_csv`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file_path` (String, varsayılan: '')

### `export_html_report`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Rapor')

### `export_pdf`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Rapor')

### `export_row_report`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Raporu')

### `export_validation_report`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Doğrulama Raporu')

### `export_xlsx`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file_path` (String, varsayılan: ''), `sheet_name` (String, varsayılan: 'EGBIMOTO'), `title` (String, varsayılan: 'EGBIMOTO')

### `show_table`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Sonuç'), `max_rows` (Int, varsayılan: '20')


## Önizleme

### `preview_collect_geometry`

- **Çıktı:** `PreviewGeometryDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `operation_name` (String, varsayılan: 'Önizleme'), `include_labels` (Bool, varsayılan: 'true'), `max_elements` (Int, varsayılan: '500')

### `preview_gate`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** yok
