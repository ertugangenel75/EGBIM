# EGBIMOTO — Op Referansı

Toplam **478 operasyon**, 52 kategori.

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

- **Çıktı:** `FourDFiveDDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `operation_name` (String, varsayılan: '4D Yapım Simülasyonu'), `project_start` (String, varsayılan: 'DateTime.Today.ToString("yyyy-MM-dd"'), `project_end` (String, varsayılan: 'DateTime.Today.AddMonths(9'), `max_elements` (Int32, varsayılan: 500), `kat_sure_hafta` (Int32, varsayılan: 3), `schedule_map` (Object, önerilen)
- **Çıktı alanları:** `meshes`, `schedule_items`, `project_start`, `project_end`, `element_count`, `warnings`, `stats`, `bbox`

### `schedule_collect_5d`

- **Çıktı:** `FourDFiveDDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `cost_step` (String, varsayılan: ''), `operation_name` (String, önerilen), `project_start` (String, önerilen), `project_end` (String, önerilen), `kat_sure_hafta` (Int, önerilen), `max_elements` (Int, önerilen), `schedule_map` (Object, önerilen)
- **Okur:** `cost_rows`, `cost_rows (opsiyonel, P2 fallback)`
- **Çıktı alanları:** `meshes`, `schedule_items`, `cost_items`, `total_cost`, `currency`, `project_start`, `project_end`, `element_count`, `warnings`, `stats`, `bbox`

### `schedule_gate`

- **Çıktı:** `string`
- **Girdi:** `FourDFiveDDto`
- **Parametreler:** `title` (String, önerilen)

### `set_param_from_schedule`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `start_param` (String, varsayılan: 'EgParamNames.BaslangicTarihi'), `end_param` (String, varsayılan: 'EgParamNames.BitisTarihi'), `phase_param` (String, varsayılan: 'EgParamNames.FazAdi'), `wbs_param` (String, varsayılan: 'EgParamNames.WbsKodu'), `write_cost` (Boolean, varsayılan: True), `schedule_step` (String, varsayılan: '')


## Aile

### `family_add_param`

- **Çıktı:** `object`
- **Parametreler:** `family_path` (String, zorunlu), `param_name` (String, zorunlu), `param_type` (String, varsayılan: 'Length'), `is_instance` (Boolean, varsayılan: True), `group` (String, varsayılan: 'Dimensions')

### `family_batch_load`

- **Çıktı:** `object`
- **Parametreler:** `folder_path` (String, zorunlu), `pattern` (String, varsayılan: '*.rfa'), `overwrite` (Boolean, varsayılan: True)

### `family_health_check`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `expected_params` (String, varsayılan: ''), `max_types` (Int32, varsayılan: 50), `origin_threshold_m` (Double, varsayılan: 100.0), `check_nested` (Boolean, varsayılan: False)
- **Çıktı alanları:** `family_id`, `family_name`, `category`, `type_count`, `param_count`, `is_in_place`, `has_no_category`, `type_count_ok`, `missing_params`, `has_origin_issue`, `nested_count`, `status`

### `family_load_to_project`

- **Çıktı:** `object`
- **Parametreler:** `family_path` (String, zorunlu), `overwrite` (Boolean, varsayılan: True)

### `family_open_template`

- **Çıktı:** `object`
- **Parametreler:** `template_path` (String, zorunlu)

### `family_type_create`

- **Çıktı:** `object`
- **Parametreler:** `family_path` (String, zorunlu), `type_name` (String, zorunlu), `params` (Dictionary<string, object?>, varsayılan: 'new Dictionary<string, object?>(')


## Analiz

### `slope_analysis`

- **Çıktı:** `object`
- **Parametreler:** `unit` (String, varsayılan: 'Percentage'), `apply_color` (Boolean, varsayılan: False), `face_sample_uv` (Double, varsayılan: 0.5), `categories` (List<string>, önerilen)

### `slope_validate`

- **Çıktı:** `object`
- **Parametreler:** `limit_profile` (String, varsayılan: ''), `min_pct` (Double, varsayılan: 0), `max_pct` (Double, varsayılan: 100), `kural_adi` (String, varsayılan: '$"min %{minPct} – max %{maxPct}"')


## Annotation

### `align_tags`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `mode` (String, varsayılan: 'left'), `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `tip`, `mode`, `tasindi`, `durum`

### `arrange_tags`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `taraf`, `durum`


## Cephe

### `collect_curtain_panels`

- **Çıktı:** `object`
- **Parametreler:** `level` (String, varsayılan: ''), `workset` (String, varsayılan: '')

### `facade_area_by_type`

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_export_schedule`

- **Çıktı:** `object`
- **Parametreler:** `output_path` (String, zorunlu), `title` (String, varsayılan: 'EGBIMOTO Cephe Metraj Raporu')

### `facade_joint_validate`

- **Çıktı:** `object`
- **Parametreler:** `min_derz_mm` (Double, varsayılan: 10.0), `max_derz_mm` (Double, varsayılan: 40.0)

### `facade_opening_ratio`

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_panel_matrix`

- **Çıktı:** `object`
- **Parametreler:** yok

### `facade_system_params`

- **Çıktı:** `object`
- **Parametreler:** `panel_tip` (String, zorunlu), `derz_genislik_mm` (Double, varsayılan: 20.0), `derz_tip` (String, varsayılan: 'Silikon'), `u_degeri` (String, varsayılan: ''), `kaplama_malzeme` (String, varsayılan: '')

### `facade_u_value_check`

- **Çıktı:** `object`
- **Parametreler:** `max_u_value` (Double, varsayılan: 1.8)


## CSV

### `csv_read`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `file_path` (String, zorunlu), `delimiter` (String, varsayılan: ','), `has_header` (Boolean, varsayılan: True), `encoding` (String, varsayılan: 'utf-8'), `skip_rows` (Int32, varsayılan: 0)

### `csv_write`

- **Çıktı:** `string`
- **Girdi:** `List<Dict>`
- **Parametreler:** `file_path` (String, zorunlu), `delimiter` (String, varsayılan: ','), `include_header` (Boolean, varsayılan: True), `encoding` (String, varsayılan: 'utf-8-sig')

### `excel_xml_read`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `file_path` (String, zorunlu), `sheet_name` (String, varsayılan: ''), `has_header` (Boolean, varsayılan: True), `skip_rows` (Int32, varsayılan: 0)

### `table_to_points`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `x_field` (String, varsayılan: 'x'), `y_field` (String, varsayılan: 'y'), `z_field` (String, varsayılan: 'z'), `unit` (String, varsayılan: 'mm')

### `table_validate_schema`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `required_fields` (String, zorunlu), `optional_fields` (String, önerilen)
- **Çıktı alanları:** `valid`, `missing_fields`, `row_count`, `field_count`, `message`


## Donatı

### `calc_anchorage_length`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: 12), `fck` (Double, varsayılan: 25), `fyk` (Double, varsayılan: 420), `cover_mm` (Double, varsayılan: 30), `hook` (Boolean, varsayılan: False)

### `calc_lap_length`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: 12), `fck` (Double, varsayılan: 25), `fyk` (Double, varsayılan: 420), `cover_mm` (Double, varsayılan: 30)

### `calc_min_spacing`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `diameter_mm` (Double, varsayılan: 12), `aggregate_size_mm` (Double, varsayılan: 20)

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

- **Çıktı:** `object`
- **Parametreler:** `factory_length_mm` (Double, varsayılan: 12000), `fck` (Double, varsayılan: 25), `fyk` (Double, varsayılan: 420), `cover_mm` (Double, varsayılan: 30), `apply_overlap` (Boolean, varsayılan: True)

### `rebar_weight_table`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `validate_rebar_ts500`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `fck` (Double, varsayılan: 25), `fyk` (Double, varsayılan: 420), `cover_mm` (Double, varsayılan: 30)


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
- **Parametreler:** `title` (String, varsayılan: 'Birleşik Doğrulama Raporu'), `lists` (Object, varsayılan: '')

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
- **Parametreler:** `param_name` (String, önerilen), `min` (Double, varsayılan: 'double.MinValue'), `max` (Double, varsayılan: 'double.MaxValue'), `severity` (String, varsayılan: 'WARNING')

### `param_value_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen), `expected_value` (String, önerilen), `severity` (String, varsayılan: 'ERROR')

### `validate_ids`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `ids_path` (String, varsayılan: 'ctx.GetString("path", ""')

### `validate_qa`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `rules_path` (String, varsayılan: 'ctx.GetString("path", ""'), `input` (Object, varsayılan: ''), `elements` (Object, varsayılan: '')

### `validation_summary`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `input` (Object, varsayılan: ''), `report` (Object, varsayılan: '')

### `validation_to_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `input` (Object, varsayılan: ''), `report` (Object, varsayılan: '')


## Duvar

### `wall_type_export_csv`

- **Çıktı:** `object`
- **Parametreler:** `output_path` (String, zorunlu), `filter_name` (String, varsayılan: ''), `include_params` (Boolean, varsayılan: True)

### `wall_type_from_csv`

- **Çıktı:** `object`
- **Parametreler:** `csv_path` (String, zorunlu), `mode` (String, varsayılan: 'create'), `rename_suffix` (String, varsayılan: ' - Import'), `base_wall_name` (String, varsayılan: ''), `delimiter` (String, varsayılan: 'auto'), `dry_run` (Boolean, varsayılan: False)


## ETL

### `load_poz_canonical_map`

- **Çıktı:** `Dictionary`
- **Girdi:** `—`
- **Parametreler:** `path` (String, varsayılan: '')
- **Yazar:** `poz_canonical_map`

### `load_poz_section_rules`

- **Çıktı:** `Dictionary`
- **Girdi:** `—`
- **Parametreler:** `path` (String, varsayılan: '')
- **Yazar:** `poz_section_rules`


## Filtre

### `add_column`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, önerilen), `value` (String, önerilen)

### `distinct_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, önerilen)

### `elements_to_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elements_to_rows_with_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (String, önerilen)

### `filter_by_category`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `category` (String, önerilen)

### `filter_by_level`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `level_name` (String, zorunlu)

### `filter_by_level_range`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_level` (String, önerilen), `max_level` (String, önerilen)

### `filter_by_param`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen), `value` (String, önerilen), `operator` (String, varsayılan: 'equals')

### `filter_by_type`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, önerilen)

### `filter_by_workset`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `workset_name` (String, önerilen)

### `filter_empty_param`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `filter_not_empty_param`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `filter_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu), `value` (String, önerilen), `operator` (String, varsayılan: 'eq')

### `filter_rows_multi`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `conditions` (Array, önerilen)

### `group_by`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, önerilen)

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
- **Parametreler:** `key_field` (String, önerilen)

### `merge_lists`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `lists` (Object, varsayılan: '')

### `merge_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `lists` (Object, varsayılan: '')

### `rename_column`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `from` (String, önerilen), `to` (String, önerilen)

### `select_columns`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fields` (String, önerilen)

### `select_field`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fields` (String, önerilen), `rows` (Object, varsayılan: '')

### `skip_n`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `count` (Int32, varsayılan: 0)

### `sort_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, önerilen), `descending` (Boolean, varsayılan: False)

### `take_n`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `count` (Int32, varsayılan: 10)

### `where`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` (String, zorunlu), `value` (String, zorunlu), `op` (String, varsayılan: 'eq'), `rows` (Object, varsayılan: '')


## Geometri

### `beam_volume`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `column_height`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `column_volume`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `curve_from_element`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_area`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_bounding_box`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_length`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_volume`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `element_volume_geometry`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `floor_volume`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `foundation_volume`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_by_system`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_summary`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_total_length`

- **Çıktı:** `double`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `room_area`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `wall_length`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `wall_net_area`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `wall_volume`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok


## Görünüm

### `check_untagged_elements`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `view_id` (String, varsayılan: '')
- **Çıktı alanları:** `element_id`, `category`, `family_type`, `level`, `tag_count`

### `create_view_filter`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `filter_name` (String, zorunlu), `categories` (String, zorunlu), `rule_value` (String, zorunlu), `param_name` (String, varsayılan: 'System Classification'), `rule_operator` (String, varsayılan: 'contains'), `color_r` (Int32, varsayılan: 0), `color_g` (Int32, varsayılan: 0), `color_b` (Int32, varsayılan: 255), `line_weight` (Int32, varsayılan: 4), `fill_pattern` (String, varsayılan: 'Solid Fill'), `overwrite` (Boolean, varsayılan: False)
- **Çıktı alanları:** `view_id`, `view_name`, `filter_id`, `filter_name`, `applied`, `status`

### `detect_undefined_system`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `discipline` (String, varsayılan: 'all')
- **Çıktı alanları:** `element_id`, `category`, `family_type`, `level`, `system_type`, `fix_hint`

### `tag_elements`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `tag_type_name` (String, zorunlu), `view_id` (String, varsayılan: ''), `leader` (Boolean, varsayılan: False), `orientation` (String, varsayılan: 'horizontal')

### `view_color_override`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element> | List<Dictionary<string, object?>>`
- **Parametreler:** `reset` (Boolean, varsayılan: False), `r` (Int32, varsayılan: 255), `g` (Int32, varsayılan: 0), `b` (Int32, varsayılan: 0)
- **Çıktı alanları:** `overridden`

### `view_create_selection_box`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element> | List<Dictionary<string, object?>>`
- **Parametreler:** `padding_m` (Double, varsayılan: 0.3)
- **Çıktı alanları:** `applied`, `view_id`

### `view_isolate_elements`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element> | List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `isolated`

### `view_reset_temporary_mode`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `reset_section_box` (Boolean, varsayılan: False)
- **Çıktı alanları:** `reset`

### `view_temp_hide_elements`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element> | List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `hidden`


## IFC

### `ifc_export`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `output_dir` (String, varsayılan: 'Environment.GetFolderPath(Environment.SpecialFolder.Desktop'), `file_name` (String, varsayılan: '$"{Path.GetFileNameWithoutExtension(rctx.Doc.Title'), `ifc_version` (String, varsayılan: 'IFC2x3'), `export_linked_files` (Boolean, varsayılan: False)


## Kapı

### `door_clearance_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_clear_width_mm` (Double, varsayılan: 850), `min_latch_side_mm` (Double, varsayılan: 300), `severity` (String, varsayılan: 'WARNING')

### `door_fire_rating_from_wall`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `wall_param` (String, varsayılan: 'Yangın Dayanım Süresi'), `door_param` (String, varsayılan: 'EG_YanginDayanim'), `rating_map` (String, varsayılan: '')

### `door_handing_detect`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `handing`, `swing`, `hand_flipped`, `facing_flipped`, `facing_x`, `facing_y`, `hand_x`, `hand_y`

### `door_number_by_room`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'Mark'), `separator` (String, varsayılan: '-'), `start_index` (Int32, varsayılan: 1), `use_room` (String, varsayılan: 'to_room')

### `room_door_relation_map`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `door_count`, `door_ids`, `door_marks`


## Koordinasyon

### `clash_detect_matrix`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `None`
- **Parametreler:** `group_a` (String, varsayılan: ''), `group_b` (String, varsayılan: ''), `tolerance_mm` (Int32, varsayılan: 10), `max_results` (Int32, varsayılan: 1000)
- **Çıktı alanları:** `a_id`, `a_category`, `b_id`, `b_category`, `clash_x`, `clash_y`, `clash_z`, `overlap_volume_m3`, `disiplin_cifti`, `seviye`

### `clash_severity_sort`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok
- **Çıktı alanları:** `sira_no`

### `coord_check_clearance`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_clearance_mm` (Double, varsayılan: 50.0), `secondary_elements` (List<Element>, önerilen)

### `coord_validate_level_consistency`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `coord_validate_penetration_firestop`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `place_opening`

- **Çıktı:** `object`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, varsayılan: 'Standard'), `offset_mm` (Double, varsayılan: 50.0), `param_b` (String, varsayılan: 'Width'), `param_h` (String, varsayılan: 'Height'), `param_sistem` (String, varsayılan: 'EG_Sistem'), `skip_existing` (Boolean, varsayılan: True), `dry_run` (Boolean, varsayılan: False), `use_diameter_table` (Boolean, varsayılan: False)

### `smart_check_mep_no_opening`

- **Çıktı:** `object`
- **Parametreler:** `host_categories` (List<string>, zorunlu), `mep_categories` (List<string>, zorunlu), `tolerance_mm` (Double, varsayılan: 15.0), `check_opening` (Boolean, varsayılan: True), `use_solid_intersection` (Boolean, varsayılan: False), `scan_linked_models` (Boolean, varsayılan: False)


## Liste

### `list_cross_product`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `second_key` (String, zorunlu)

### `list_filter_by_rule`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `field` (String, zorunlu), `operator` (String, varsayılan: 'eq'), `value` (String, varsayılan: '')

### `list_flatten`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `levels` (Int32, varsayılan: 1)

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
- **Parametreler:** `sort_field` (String, zorunlu), `ascending` (Boolean, varsayılan: True)

### `list_take_every_n`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** `n` (Int32, varsayılan: 2), `offset` (Int32, varsayılan: 0)

### `list_transpose`

- **Çıktı:** `List<object?>`
- **Girdi:** `List<object>`
- **Parametreler:** yok

### `list_zip`

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

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Element>`
- **Parametreler:** `include_edges` (Boolean, varsayılan: True)
- **Yazar:** `kalip_traces`

### `kalip_column`

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

- **Çıktı:** `Dictionary`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok
- **Okur:** `poz_data`

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

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Dictionary>`
- **Parametreler:** yok
- **Okur:** `poz_canonical_map`, `poz_data`, `poz_section_rules`

### `wall_area`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `xlsx_import_apply`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Dictionary<string, object?>> (file_path verilmezse kullanılır)`
- **Parametreler:** `key_field` (String, varsayılan: 'element_id'), `only_changed` (Boolean, varsayılan: True), `file_path` (String, varsayılan: ''), `field_param_map` (Dictionary<string, object?>, varsayılan: 'new(')
- **Çıktı alanları:** `written`, `skipped`


## Mekansal

### `build_spatial_graph`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dict>`
- **Parametreler:** `include_exterior` (Boolean, varsayılan: True), `deduplicate` (Boolean, varsayılan: True)
- **Çıktı alanları:** `from_room_id`, `from_room_name`, `from_room_number`, `to_room_id`, `to_room_name`, `to_room_number`, `shared_wall_id`, `shared_wall_type`, `edge_length_mm`, `fire_rating`, `is_exterior`


## MEP Denetim

### `cable_tray_fill_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `fill_param` (String, varsayılan: 'EG_KabloDoluluk'), `max_fill_pct` (Double, varsayılan: 70.0)

### `conduit_fill_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `count_param` (String, varsayılan: 'EG_IletkenSayisi'), `area_param` (String, varsayılan: 'EG_IletkenKesit_mm2'), `max_fill_pct` (Double, varsayılan: 40.0)

### `duct_aspect_ratio_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_ratio` (Double, varsayılan: 4.0), `severity` (String, varsayılan: 'WARNING')

### `fa_device_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `loop_param` (String, varsayılan: 'EG_Loop'), `zone_param` (String, varsayılan: 'EG_Zone'), `circuit_param` (String, varsayılan: 'EG_Circuit')
- **Çıktı alanları:** `device_type`, `level`, `zone`, `loop`, `circuit`, `quantity`

### `lighting_emergency_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `emergency_param` (String, varsayılan: 'EG_AcilAydinlatma'), `emergency_panel_pattern` (String, varsayılan: 'EP')

### `panel_phase_balance_check`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_imbalance_pct` (Double, varsayılan: 10.0)
- **Çıktı alanları:** `panel_id`, `panel_name`, `phase_a_va`, `phase_b_va`, `phase_c_va`, `total_va`, `max_imbalance_pct`, `circuit_count`, `status`

### `sprinkler_head_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `k_factor_param` (String, varsayılan: 'K_Factor'), `coverage_param` (String, varsayılan: 'EG_KapsaAlan_m2'), `zone_param` (String, varsayılan: 'EG_Zone')
- **Çıktı alanları:** `type_name`, `k_factor`, `coverage_m2`, `zone`, `level`, `quantity`

### `valve_type_classify`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `family_name`, `type_name`, `valve_class`, `system_type`, `diameter_mm`


## MEP Hesap

### `calc_ach_airflow`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `ach` (Double, varsayılan: 6.0), `mode` (String, varsayılan: 'normal'), `area_m2` (Double, varsayılan: 0), `height_m` (Double, varsayılan: 0)
- **Çıktı alanları:** `room_id`, `room_name`, `area_m2`, `height_m`, `volume_m3`, `ach`, `airflow_cmh`, `airflow_cfm`, `fan_option_4x_cmh`, `fan_option_6x_cmh`

### `calc_brick_quantity`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `area_m2` (Double, varsayılan: 0), `thickness_cm` (Double, varsayılan: 19), `brick_type` (String, varsayılan: 'tam'), `mortar_ratio` (Double, varsayılan: 0.25), `waste_pct` (Double, varsayılan: 7.5)
- **Çıktı alanları:** `wall_id`, `wall_type`, `area_m2`, `thickness_cm`, `wall_volume_m3`, `mortar_m3`, `net_brick_m3`, `brick_count`, `waste_pct`, `total_with_waste`

### `calc_hazen_williams`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `c_factor` (Double, varsayılan: 120.0), `pipe_length_m` (Double, varsayılan: 1.0), `flow_rate_lpm` (Double, varsayılan: 0), `pipe_diam_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `pipe_id`, `diam_mm`, `flow_lpm`, `c_factor`, `length_m`, `friction_loss_bar_per_m`, `total_loss_bar`, `velocity_m_s`

### `calc_room_lux`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `lumen_param` (String, varsayılan: 'InitialIntensity'), `cu` (Double, varsayılan: 0.6), `mf` (Double, varsayılan: 0.8), `target_lux` (Double, varsayılan: 300.0)
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `area_m2`, `fixture_count`, `total_lumens`, `cu`, `mf`, `avg_lux`, `target_lux`, `status`


## MEP HVAC

### `assign_flow_to_terminals`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Space)`
- **Parametreler:** `round_cfm_to` (Double, varsayılan: 'DefaultRoundCfmTo'), `flow_param_name` (String, varsayılan: 'Flow'), `system_type_filter` (String, varsayılan: 'Supply Air'), `only_with_flow` (Boolean, varsayılan: True)
- **Yazar:** `Flow`
- **Çıktı alanları:** `space_id`, `space_name`, `space_number`, `terminal_count`, `supply_cfm`, `cfm_per_terminal`, `status`

### `populate_space_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Space)`
- **Parametreler:** `target_param` (String, zorunlu), `source` (String, varsayılan: 'cfm_per_sf'), `skip_zero_area` (Boolean, varsayılan: True), `value` (Double, varsayılan: 0.0)
- **Çıktı alanları:** `space_id`, `space_name`, `space_number`, `value`, `status`

### `resize_diffuser_by_flow`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (FamilyInstance/DuctTerminal)`
- **Parametreler:** `family_name` (String, zorunlu), `thresholds` (String, zorunlu), `flow_param_name` (String, varsayılan: 'Flow'), `system_type_filter` (String, varsayılan: 'Supply Air')
- **Okur:** `Flow`
- **Çıktı alanları:** `terminal_id`, `current_cfm`, `old_type`, `new_type`, `changed`, `status`


## MEP Koordinasyon

### `mep_region_count`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `region_category` (String, varsayılan: 'OST_Rooms'), `mep_categories` (String, varsayılan: 'OST_PipeCurves,OST_DuctCurves,OST_CableTray'), `z_tolerance_mm` (Int32, varsayılan: 1500)
- **Çıktı alanları:** `region_id`, `region_name`, `region_number`, `level_name`, `area_m2`, `mep_count`, `total_length_m`, `pipe_count`, `duct_count`, `tray_count`, `by_category`

### `mep_region_tag`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `region_category` (String, varsayılan: 'OST_Rooms'), `mep_categories` (String, varsayılan: 'OST_PipeCurves,OST_DuctCurves,OST_CableTray'), `target_param` (String, varsayılan: 'Comments'), `write_mode` (String, varsayılan: 'name_number'), `z_tolerance_mm` (Int32, varsayılan: 1500)
- **Çıktı alanları:** `region_id`, `region_name`, `tagged_count`, `skipped_count`

### `mep_straighten_apply`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `max_apply` (Int32, varsayılan: 0)
- **Çıktı alanları:** `elbow_a_id`, `elbow_b_id`, `status`, `message`

### `mep_straighten_scan`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `categories` (String, varsayılan: 'OST_DuctCurves,OST_PipeCurves,OST_CableTray'), `min_offset_mm` (Int32, varsayılan: 5), `max_offset_mm` (Int32, varsayılan: 600), `max_results` (Int32, varsayılan: 2000)
- **Çıktı alanları:** `elbow_a_id`, `elbow_b_id`, `middle_ids`, `offset_mm`, `system_name`, `category`, `anchor_id`, `mover_id`, `center_x`, `center_y`, `center_z`


## MEP-Elektrik

### `elec_busbar_sizing`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `akim_a` (Double, varsayılan: 0), `malzeme` (String, varsayılan: 'cu'), `montaj` (String, varsayılan: 'yatay'), `guvenlik_pct` (Double, varsayılan: 20.0), `faz_sayisi` (Int, önerilen)
- **Çıktı alanları:** `akim_a`, `akim_tasarim_a`, `malzeme`, `montaj`, `akim_yogunlugu`, `min_kesit_mm2`, `oneri_genislik_mm`, `oneri_kalinlik_mm`, `oneri_kesit_mm2`, `oneri_boyut`, `durum`

### `elec_check_circuit_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elec_check_emergency_lighting`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `fixtures` (List<Element>, önerilen), `emergency_keywords` (List<string>, önerilen)

### `elec_circuit_diff`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `-`
- **Parametreler:** `baseline_path` (String, zorunlu), `output_path` (String, varsayılan: ''), `panel_filter` (String, varsayılan: ''), `load_tolerance_va` (Double, varsayılan: 1.0)
- **Çıktı alanları:** `change_type`, `panel`, `circuit_number`, `field`, `old_value`, `new_value`, `detail`

### `elec_circuit_snapshot`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (ElectricalSystem)`
- **Parametreler:** `output_path` (String, zorunlu), `panel_filter` (String, varsayılan: '')
- **Çıktı alanları:** `circuit_id`, `panel`, `circuit_number`, `load_va`, `poles`, `status`

### `elec_conduit_calc_iec`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Conduit)`
- **Parametreler:** `voltage` (Double, varsayılan: 400.0), `vdrop_limit_pct` (Double, varsayılan: 5.0), `max_fill_pct` (Double, varsayılan: 40.0), `ampacity_table_path` (String, varsayılan: ''), `only_with_circuit` (Boolean, varsayılan: True)
- **Okur:** `EG_DevreNo`, `EG_GruplamaAdet`, `EG_GucFaktoru`, `EG_Iletken`, `EG_KurulumMetodu`, `EG_OrtamSicaklik`, `EG_Yalitim`, `EG_YapmaPayi`
- **Yazar:** `EG_DolulukYuzde`, `EG_FazKesit_mm2`, `EG_GerilimDusumu`, `EG_GerilimDusumuV`, `EG_HesapAkim`, `EG_HesapDurumu`, `EG_HesapTarihi`, `EG_KabloKesiti`, `EG_KabloUzunluk`, `EG_KisaDevreKesiti`, `EG_ModelUzunluk`, `EG_SigortaOneri`
- **Çıktı alanları:** `conduit_id`, `devre`, `uzunluk_m`, `akim_a`, `kesit_mm2`, `gerilim_dusumu_pct`, `doluluk_pct`, `sigorta_a`, `durum`

### `elec_conduit_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Conduit)`
- **Parametreler:** `output_path` (String, zorunlu), `only_calculated` (Boolean, varsayılan: True)
- **Okur:** `EG_DevreNo`, `EG_DolulukYuzde`, `EG_GerilimDusumu`, `EG_Hedef`, `EG_HesapAkim`, `EG_HesapDurumu`, `EG_KabloKesiti`, `EG_KabloUzunluk`, `EG_Kaynak`, `EG_SigortaOneri`
- **Çıktı alanları:** `devre`, `kaynak`, `hedef`, `kesit`, `uzunluk_m`, `gerilim_dusumu_pct`, `doluluk_pct`, `sigorta_a`, `durum`

### `elec_diversity_factor`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `yukler` (String, varsayılan: '[]'), `cesitlilik_faktoru` (Double, varsayılan: 0.75), `gerilim_v` (Double, varsayılan: 415), `cos_phi` (Double, varsayılan: 0.85)
- **Çıktı alanları:** `yuk_sayisi`, `toplam_kurulu_kw`, `cesitlilik_faktoru`, `cos_phi`, `toplam_talep_kw`, `talep_kva`, `talep_a`, `yukler_detay`, `durum`

### `elec_earthing_validation`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `beklenen_tip` (String, varsayılan: 'TN-S'), `alan_tipi` (String, varsayılan: 'ticari')
- **Çıktı alanları:** `alan_tipi`, `beklenen_tip`, `oneri_tip`, `gereksinimler`, `durum`

### `elec_elv_device_qa`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (Data/Security/Comm elemanları)`
- **Parametreler:** `tolerance_mm` (Int32, varsayılan: 100), `write_back` (Boolean, varsayılan: False)
- **Okur:** `EG_ElvCihazTipi`, `EG_MontajYuksekligi`
- **Yazar:** `EG_MontajYuksekligi_Oneri`
- **Çıktı alanları:** `element_id`, `cihaz_tipi`, `mevcut_mm`, `oneri_mm`, `fark_mm`, `aciklama`, `durum`

### `elec_emergency_circuit_qa`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `alan_tipi` (String, varsayılan: 'genel'), `armatur_aralik_m` (Double, varsayılan: 0), `ozerklik_sure_saat` (Double, varsayılan: 0), `devreye_girme_sn` (Double, varsayılan: 0), `bagimsiz_devre` (Boolean, varsayılan: False)
- **Çıktı alanları:** `alan_tipi`, `min_lux`, `max_aralik_m`, `min_ozerklik_saat`, `max_devreye_sn`, `kontroller`, `durum`

### `elec_generate_panel_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `elec_generator_load_calc`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `acil_yuk_kw` (Double, varsayılan: 0), `standby_yuk_kw` (Double, varsayılan: 0), `motor_baslama_kw` (Double, varsayılan: 0), `guc_faktoru` (Double, varsayılan: 0.8), `guvenlik_pct` (Double, varsayılan: 25.0)
- **Çıktı alanları:** `acil_yuk_kw`, `standby_yuk_kw`, `motor_baslama_kw`, `toplam_kw`, `guc_faktoru`, `guvenlik_pct`, `tasarim_kw`, `tasarim_kva`, `oneri_jenerator_kva`, `durum`

### `elec_power_factor_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `aktif_guc_kw` (Double, varsayılan: 0), `cos_phi_mevcut` (Double, varsayılan: 0.8), `cos_phi_hedef` (Double, varsayılan: 0.95), `gerilim_v` (Double, varsayılan: 415)
- **Çıktı alanları:** `aktif_guc_kw`, `cos_phi_mevcut`, `cos_phi_hedef`, `s_mevcut_kva`, `q_mevcut_kvar`, `s_hedef_kva`, `q_hedef_kvar`, `q_kompanzasyon_kvar`, `akim_mevcut_a`, `akim_hedef_a`, `tasarruf_pct`, `durum`

### `elec_setup_conduit_params`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: '')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `elec_short_circuit_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `kesit_mm2` (Double, varsayılan: 0), `uzunluk_m` (Double, varsayılan: 0), `gerilim_v` (Double, varsayılan: 415), `malzeme` (String, varsayılan: 'cu'), `kaynak_empedans` (Double, varsayılan: 35.0), `sigorta_a` (Double, varsayılan: 0), `sigorta_tip` (String, varsayılan: 'mcb')
- **Çıktı alanları:** `kesit_mm2`, `uzunluk_m`, `z_total_mohm`, `isc_ka`, `kirilma_suresi_ms`, `t_limit_ms`, `isil_dayanim`, `sigorta_a`, `durum`

### `elec_tray_hanger_spacing`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (CableTray)`
- **Parametreler:** `tav_tipi` (String, varsayılan: 'ladder'), `mevcut_aralik_m` (Double, varsayılan: 0)
- **Okur:** `EG_AskiAraligi`, `EG_TavaTipi`
- **Çıktı alanları:** `tray_id`, `tav_tipi`, `mevcut_aralik_m`, `max_aralik_m`, `durum`, `aciklama`

### `elec_tray_separation_check`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (CableTray EG_TavaTuru=guc/elv)`
- **Parametreler:** `min_ayrim_mm` (Int32, varsayılan: 300), `tolerans_mm` (Int32, varsayılan: 50)
- **Okur:** `EG_TavaTuru`
- **Çıktı alanları:** `cift_no`, `guc_tray_id`, `elv_tray_id`, `mesafe_mm`, `min_mm`, `durum`

### `elec_ups_autonomy_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `yuk_kw` (Double, varsayılan: 0), `hedef_sure_dak` (Double, varsayılan: 15.0), `batarya_voltaj` (Double, varsayılan: 240.0), `ups_verim` (Double, varsayılan: 0.94), `batarya_verim` (Double, varsayılan: 0.85), `ups_sinifi` (String, varsayılan: 'VFI')
- **Çıktı alanları:** `yuk_kw`, `hedef_sure_dak`, `e_batarya_wh`, `kapasite_ah`, `batarya_voltaj`, `oneri_batarya_ah`, `ups_sinifi`, `sinif_aciklama`, `durum`

### `elec_validate_lux_level`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_lux` (Double, varsayılan: 300.0)

### `elec_validate_panel_load`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_load_kva` (Double, varsayılan: 100.0)

### `elec_voltage_drop_calc`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `akim_a` (Double, varsayılan: 0), `uzunluk_m` (Double, varsayılan: 0), `kesit_mm2` (Double, varsayılan: 0), `gerilim_v` (Double, varsayılan: 415), `faz_sayisi` (Int32, varsayılan: 3), `malzeme` (String, varsayılan: 'cu'), `cos_phi` (Double, varsayılan: 0.85), `max_dusumu_pct` (Double, varsayılan: 3.0), `yuk_tipi` (String, varsayılan: 'genel')
- **Çıktı alanları:** `akim_a`, `uzunluk_m`, `kesit_mm2`, `faz_sayisi`, `malzeme`, `cos_phi`, `dusumu_pct`, `dusumu_v`, `max_dusumu_pct`, `yuk_tipi`, `durum`


## MEP-Koordinasyon

### `mep_lintel_place`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `min_genislik_mm` (Double, varsayılan: 600.0), `binme_payi_mm` (Double, varsayılan: 200.0), `beton_sinifi` (String, varsayılan: 'C25'), `lento_family_isim` (String, varsayılan: ''), `mevcut_temizle` (Boolean, varsayılan: False)
- **Okur:** `KB_Durum`, `KB_Height`, `KB_Width`
- **Yazar:** `Beton_Sinifi`, `Bosluk_ID`, `Demir_Capi`, `Lento_Onay`
- **Çıktı alanları:** `eklenen_lento`, `atlanan`, `hata_sayisi`, `hata_listesi`, `beton_sinifi`, `durum`

### `mep_opening_bcf_export`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `output_path` (String, zorunlu), `sadece_kritik` (Boolean, varsayılan: False), `durum_filtresi` (String, varsayılan: 'RED,SORUN,GECERSIZ,REVIZYON,TAKVIYE,IPTAL')
- **Okur:** `KB_Disiplin`, `KB_Durum`, `KB_Height`, `KB_Width`
- **Çıktı alanları:** `bcf_dosyasi`, `topic_sayisi`, `toplam_bosluk`, `durum`

### `mep_opening_detect`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `clearance_normal_mm` (Double, varsayılan: 60.0), `clearance_fire_mm` (Double, varsayılan: 100.0), `min_size_mm` (Double, varsayılan: 50.0), `level_filter` (String, varsayılan: ''), `guncelle_mevcut` (Boolean, varsayılan: True)
- **Okur:** `KB_Kaynak_ID`
- **Yazar:** `KB_Aciklama`, `KB_Alt_Kot`, `KB_Disiplin`, `KB_Durum`, `KB_Duvar_Sinifi`, `KB_Height`, `KB_Kaynak_ID`, `KB_Son_Guncelleme`, `KB_Width`
- **Çıktı alanları:** `tespit_sayisi`, `yerlestirilen`, `guncellenen`, `toplam_bosluk`, `bosluklar`, `durum`

### `mep_opening_validate`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (GenericModel KB_* param)`
- **Parametreler:** `ec2_kontrol` (Boolean, varsayılan: True), `kiriş_mesafe_mm` (Double, varsayılan: 300.0), `bosluk_arasi_mm` (Double, varsayılan: 200.0), `durum_guncelle` (Boolean, varsayılan: True)
- **Okur:** `KB_Disiplin`, `KB_Durum`, `KB_Height`, `KB_Kaynak_ID`, `KB_Width`
- **Yazar:** `KB_Durum`, `KB_Son_Guncelleme`
- **Çıktı alanları:** `bosluk_id`, `genislik_mm`, `yukseklik_mm`, `disiplin`, `ec2_seviye`, `ec2_aciklama`, `kiris_yakini`, `gecersiz`, `kb_durum`, `yeni_durum`, `durum`


## MEP-Mekanik

### `duct_section_convert_apply`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `skip_warnings` (String, varsayılan: 'false')
- **Çıktı alanları:** `duct_id`, `status`, `message`, `new_w_mm`, `new_h_mm`

### `duct_section_convert_preview`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `fix_dimension` (String, varsayılan: 'height'), `fixed_value_mm` (Double, varsayılan: 0), `round_to_mm` (Double, varsayılan: 50), `max_aspect_ratio` (Double, varsayılan: 4.0), `only_round_ducts` (String, önerilen)
- **Çıktı alanları:** `duct_id`, `system_name`, `old_w_mm`, `old_h_mm`, `old_de_mm`, `new_w_mm`, `new_h_mm`, `new_de_mm`, `aspect_ratio`, `de_error_pct`, `warning`

### `mep_ach_by_room_type`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `oda_tipi` (String, varsayılan: 'ofis'), `mevcut_ach` (Double, varsayılan: 0), `oda_alani_m2` (Double, varsayılan: 0), `oda_yuksekligi_m` (Double, varsayılan: 3.0), `write_back` (Boolean, varsayılan: False)
- **Yazar:** `EG_ACH_Oneri`
- **Çıktı alanları:** `oda_tipi`, `min_ach`, `tipik_ach`, `max_ach`, `mevcut_ach`, `alan_m2`, `hacim_m3`, `gerekli_m3h`, `standart`, `durum`

### `mep_ahu_selection`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `toplam_debi_m3h` (Double, varsayılan: 0), `sogutma_kw` (Double, varsayılan: 0), `isitma_kw` (Double, varsayılan: 0), `esp_pa` (Double, varsayılan: 300.0), `filtre_sinifi` (String, varsayılan: 'F7'), `alan_tipi` (String, varsayılan: 'genel')
- **Çıktı alanları:** `toplam_debi_m3h`, `esp_pa`, `fan_kw`, `fan_m3s`, `sogutma_kw`, `isitma_kw`, `filtre_mevcut`, `filtre_oneri`, `filtre_durum`, `ahu_boyut_tahmini`, `alan_tipi`, `durum`

### `mep_air_terminal_space_map`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `mep_chiller_cop_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `chiller_tipi` (String, varsayılan: 'hava_sogutmali'), `mevcut_cop` (Double, varsayılan: 0), `sogutma_kapasitesi_kw` (Double, varsayılan: 0), `calisma_saati` (Double, varsayılan: 2000.0), `elektrik_birim_fiyat` (Double, varsayılan: 3.5)
- **Çıktı alanları:** `chiller_tipi`, `teknoloji`, `mevcut_cop`, `min_cop`, `iyi_cop`, `cop_durum`, `elektrik_kw`, `yillik_maliyet_tl`, `tasarruf_pct`, `standart`, `durum`

### `mep_cooling_load_room`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `alan_m2` (Double, varsayılan: 0), `cam_alani_m2` (Double, varsayılan: 0), `yon` (String, varsayılan: 'guney'), `kisi_sayisi` (Int32, varsayılan: 0), `ekipman_w` (Double, varsayılan: 0), `aydinlatma_w_m2` (Double, varsayılan: 12.0), `dis_sicaklik_c` (Double, varsayılan: 36.0), `ic_sicaklik_c` (Double, varsayılan: 24.0)
- **Çıktı alanları:** `alan_m2`, `cam_alani_m2`, `yon`, `gunes_yuku_w`, `duvar_iletim_w`, `ic_yuk_w`, `toplam_w`, `toplam_sogutma_kw`, `yogunluk_w_m2`, `durum`

### `mep_fresh_air_rate_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `alan_m2` (Double, varsayılan: 0), `kisi_sayisi` (Int32, varsayılan: 0), `alan_tipi` (String, varsayılan: 'ofis'), `mevcut_m3h` (Double, varsayılan: 0)
- **Çıktı alanları:** `alan_m2`, `kisi_sayisi`, `alan_tipi`, `rp_l_s_kisi`, `ra_l_s_m2`, `gerekli_l_s`, `gerekli_m3h`, `kisi_basi_l_s`, `mevcut_m3h`, `standart`, `durum`

### `mep_hepa_filter_qa`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `filtre_sinifi` (String, varsayılan: 'H14'), `alan_tipi` (String, varsayılan: 'ameliyathane'), `mevcut_dp_pa` (Double, varsayılan: 0), `montaj_tipi` (String, varsayılan: 'gel_seal'), `son_test_tarihi` (String, varsayılan: '')
- **Çıktı alanları:** `alan_tipi`, `gerekli_sinif`, `mevcut_sinif`, `sinif_durum`, `dp_pa`, `dp_durum`, `montaj_tipi`, `montaj_durum`, `test_tarihi`, `test_durum`, `durum`

### `mep_hvac_heat_load_calc`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `alan_m2` (Double, varsayılan: 0), `tavan_yuksekligi_m` (Double, varsayılan: 3.0), `dis_sicaklik_c` (Double, varsayılan: 36.0), `ic_sicaklik_c` (Double, varsayılan: 24.0), `u_deger_ortalama` (Double, varsayılan: 0.5), `ic_yuk_w_m2` (Double, varsayılan: 30.0), `kisi_sayisi` (Int32, varsayılan: 0), `taze_hava_m3h` (Double, varsayılan: 0)
- **Çıktı alanları:** `alan_m2`, `delta_t_c`, `kabuk_w`, `ic_yuk_w`, `infiltrasyon_w`, `taze_hava_w`, `toplam_w`, `sogutma_kw`, `sogutma_tr`, `tasarim_kw`, `tasarim_tr`, `durum`

### `mep_hvac_zone_balance`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (MEPSpaces)`
- **Parametreler:** `tolerans_pct` (Double, varsayılan: 10.0), `debi_param` (String, varsayılan: 'EG_Debi_m3h')
- **Okur:** `EG_TasarimDebi_m3h`, `EG_TerminalToplam_m3h`
- **Çıktı alanları:** `zon_id`, `zon_adi`, `tasarim_m3h`, `toplam_terminal_m3h`, `sapma_pct`, `durum`

### `mep_pressurization_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `oda_tipi` (String, varsayılan: 'genel_koridor'), `mevcut_basinc_pa` (Double, varsayılan: 'double.NaN'), `tolerans_pa` (Double, varsayılan: 2.0)
- **Çıktı alanları:** `oda_tipi`, `hedef_basinc_pa`, `mevcut_basinc_pa`, `basinclandirma_tipi`, `tolerans_pa`, `standart`, `durum`

### `mep_static_pressure_calc`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `ana_hat_uzunlugu_m` (Double, varsayılan: 0), `debi_m3h` (Double, varsayılan: 0), `boyut_mm` (Double, varsayılan: 400), `dp_filtre_pa` (Double, varsayılan: 150.0), `dp_serpantin_pa` (Double, varsayılan: 200.0), `dp_terminal_pa` (Double, varsayılan: 50.0), `boru_katsayi_pa_m` (Double, varsayılan: 1.0)
- **Çıktı alanları:** `ana_hat_uzunlugu_m`, `debi_m3h`, `hiz_m_s`, `hiz_durum`, `r_pa_m`, `hat_kaybi_pa`, `fitting_kaybi_pa`, `dp_filtre_pa`, `dp_serpantin_pa`, `dp_terminal_pa`, `toplam_esp_pa`, `fan_kw`, `durum`

### `mep_validate_duct_size`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_size_mm` (Double, varsayılan: 100.0)

### `mep_validate_duct_slope`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_slope_pct` (Double, varsayılan: 0.5)

### `mep_validate_duct_velocity`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_velocity_m_s` (Double, varsayılan: 6.0)

### `mep_validate_space_hvac_zone`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok


## MEP-Sıhhi

### `plumbing_assign_units`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (PlumbingFixture)`
- **Parametreler:** `overwrite` (Boolean, varsayılan: False)
- **Okur:** `EG_ArmaturTipi`, `EG_DU`
- **Yazar:** `EG_ArmaturTipi`, `EG_DU`, `EG_LU_Sicak`, `EG_LU_Soguk`
- **Çıktı alanları:** `fixture_id`, `tip`, `du`, `lu_soguk`, `lu_sicak`, `durum`

### `plumbing_calc_en`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Pipe)`
- **Parametreler:** `pipe_role` (String, varsayılan: 'auto'), `min_slope_pct` (Double, varsayılan: 1.0), `max_fill_pct` (Double, varsayılan: 70.0), `capacity_table_path` (String, varsayılan: '')
- **Okur:** `EG_BinaKullanim`, `EG_DU`, `EG_DrenajSistem`, `EG_LU_Sicak`, `EG_LU_Soguk`, `EG_SuSistem`
- **Yazar:** `EG_AkisHizi`, `EG_AtikDebi_Qww`, `EG_BoruKapasite`, `EG_DolulukOrani`, `EG_EgimYuzde`, `EG_HesapDurumu`, `EG_HesapTarihi`, `EG_OneriCap_DN`, `EG_TasarimDebi_QD`, `EG_ToplamDU`, `EG_ToplamLU`
- **Çıktı alanları:** `pipe_id`, `rol`, `sistem`, `toplam_du_lu`, `debi_l_s`, `dn_mm`, `durum`

### `plumbing_calc_flow_rate`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `plumbing_check_fixture_room_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `plumbing_dead_leg_check`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (Pipe) veya boş`
- **Parametreler:** `max_uzunluk_katsayi` (Double, varsayılan: 20.0), `sicak_su_sicaklik_c` (Double, önerilen)
- **Çıktı alanları:** `pipe_id`, `sistem`, `cap_mm`, `uzunluk_mm`, `max_mm`, `oran`, `durum`

### `plumbing_demand_lpd`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_tipi` (String, zorunlu), `kisi_sayisi` (Int32, varsayılan: 0), `lpd_override` (Double, varsayılan: 0)
- **Çıktı alanları:** `bina_tipi`, `kisi_sayisi`, `lpd_min`, `lpd_max`, `lpd_tipik`, `gunluk_talep_lt`, `gunluk_talep_m3`, `durum`

### `plumbing_fixture_clearance`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (tüm PlumbingFixture)`
- **Parametreler:** `tolerance_mm` (Int32, varsayılan: 50), `write_back` (Boolean, varsayılan: False)
- **Okur:** `EG_ArmaturTipi`, `EG_MontajYuksekligi`
- **Yazar:** `EG_MontajYuksekligi_Oneri`
- **Çıktı alanları:** `fixture_id`, `armatur_tipi`, `mevcut_mm`, `oneri_mm`, `fark_mm`, `aciklama`, `durum`

### `plumbing_hwc_return`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `isi_kaybi_w` (Double, varsayılan: 0), `besleme_sicaklik_c` (Double, varsayılan: 65.0), `donus_sicaklik_c` (Double, varsayılan: 55.0), `ic_cap_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `isi_kaybi_w`, `besleme_sicaklik_c`, `donus_sicaklik_c`, `delta_t_c`, `qcirc_l_s`, `qcirc_l_h`, `qcirc_m3_s`, `hiz_m_s`, `hiz_durum`, `oneri_cap_mm`, `lejyonella_durum`, `durum`

### `plumbing_peak_demand`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `daire_sayisi` (Int32, varsayılan: 0), `birim_pik_l_dak` (Double, varsayılan: -1), `hesap_turu` (String, varsayılan: 'konut')
- **Çıktı alanları:** `daire_sayisi`, `birim_pik_l_dak`, `hesap_turu`, `qp_l_dak`, `qp_l_s`, `qp_m3_s`, `riser_dn_mm`, `durum`

### `plumbing_pressure_zone`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `kat_sayisi` (Int32, varsayılan: 0), `kat_yuksekligi_m` (Double, varsayılan: 3.0), `max_basinc_bar` (Double, varsayılan: 3.5), `min_basinc_bar` (Double, önerilen)
- **Çıktı alanları:** `kat_sayisi`, `zon_sayisi`, `max_kat_per_zon`, `toplam_statik_bar`, `zonlar`, `durum`

### `plumbing_pump_hp_calc`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `debi_m3_s` (Double, varsayılan: 0), `toplam_yuk_m` (Double, varsayılan: 0), `pompa_verim` (Double, varsayılan: 0.7), `motor_verim` (Double, varsayılan: 0.9), `servis_faktor` (Double, varsayılan: 1.15)
- **Çıktı alanları:** `debi_m3_s`, `toplam_yuk_m`, `ph_kw_hidrolik`, `pb_kw_fren`, `pm_kw_motor`, `standart_motor_kw`, `durum`

### `plumbing_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `-`
- **Parametreler:** `output_path` (String, zorunlu), `only_calculated` (Boolean, varsayılan: True)
- **Okur:** `EG_AkisHizi`, `EG_AtikDebi_Qww`, `EG_DolulukOrani`, `EG_EgimYuzde`, `EG_HesapDurumu`, `EG_OneriCap_DN`, `EG_TasarimDebi_QD`, `EG_ToplamDU`, `EG_ToplamLU`
- **Çıktı alanları:** `sistem`, `rol`, `debi_l_s`, `dn_mm`, `durum`

### `plumbing_setup_params`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: '')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `plumbing_static_pressure`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `yukseklik_m` (Double, varsayılan: 0), `sicaklik_c` (Double, varsayılan: 20.0), `giris_basinc_bar` (Double, varsayılan: 0)
- **Çıktı alanları:** `yukseklik_m`, `sicaklik_c`, `su_yogunlugu`, `giris_basinc_bar`, `statik_basinc_pa`, `statik_basinc_bar`, `toplam_basinc_bar`, `toplam_basinc_kpa`, `durum`

### `plumbing_storage_tank_size`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `gunluk_talep_m3` (Double, varsayılan: 0), `tampon_katsayi` (Double, varsayılan: 1.15), `suction_oran` (Double, varsayılan: 0.55)
- **Çıktı alanları:** `gunluk_talep_m3`, `tampon_katsayi`, `toplam_depo_m3`, `suction_tank_m3`, `oht_tank_m3`, `oht_efektif_derinlik_m`, `oht_taban_alani_m2`, `suction_not`, `durum`

### `plumbing_validate_connector_diameter`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `expected_diameter_mm` (Double, varsayılan: 50.0), `tolerance_mm` (Double, varsayılan: 5.0)

### `plumbing_validate_pipe_slope`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_slope_pct` (Double, varsayılan: 1.0)

### `plumbing_validate_system_separation`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `plumbing_water_velocity`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `debi_l_s` (Double, varsayılan: 0), `ic_cap_mm` (Double, varsayılan: 0), `boru_tipi` (String, varsayılan: 'soguk')
- **Çıktı alanları:** `debi_l_s`, `ic_cap_mm`, `boru_tipi`, `alan_mm2`, `hiz_m_s`, `v_min_m_s`, `v_max_m_s`, `oneri_cap_mm`, `durum`


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
- **Parametreler:** `windows` (List<Element>, önerilen), `requires_window_keywords` (List<string>, önerilen)

### `arch_renumber_doors`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `prefix` (String, varsayılan: 'K')

### `arch_sheets_from_data`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `arch_validate_accessibility`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_width_mm` (Double, varsayılan: 850.0)

### `arch_validate_ceiling_height`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_height_mm` (Double, varsayılan: 2400.0)

### `arch_validate_room_area`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_area_m2` (Double, varsayılan: 0.0)

### `arch_validate_room_naming`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `allowed_keywords` (List<string>, önerilen)


## Modelleme

### `create_3d_view`

- **Çıktı:** `Element`
- **Girdi:** `List<Element>`
- **Parametreler:** `view_name` (String, varsayılan: '$"EGBIMOTO_3D_{DateTime.Now:HHmmss}"'), `padding_mm` (Double, varsayılan: 500)

### `create_sheet`

- **Çıktı:** `Element`
- **Girdi:** `—`
- **Parametreler:** `sheet_number` (String, zorunlu), `sheet_name` (String, zorunlu), `title_block_name` (String, varsayılan: '')

### `mirror_element`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `axis` (String, varsayılan: 'Y'), `pivot_x_mm` (Double, varsayılan: 0), `pivot_y_mm` (Double, varsayılan: 0), `copy` (Boolean, varsayılan: False)

### `move_element`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `dx_mm` (Double, varsayılan: 0), `dy_mm` (Double, varsayılan: 0), `dz_mm` (Double, varsayılan: 0)

### `place_family`

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, zorunlu), `x_mm` (Double, varsayılan: 0), `y_mm` (Double, varsayılan: 0), `z_mm` (Double, varsayılan: 0), `rotation_deg` (Double, varsayılan: 0)

### `place_view_on_sheet`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `sheet_number` (String, zorunlu), `x_mm` (Double, varsayılan: 0), `y_mm` (Double, varsayılan: 0)

### `rename_element`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'Name'), `value` (String, varsayılan: ''), `prefix` (String, varsayılan: ''), `suffix` (String, varsayılan: '')

### `set_element_type`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu)

### `set_level`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `level_name` (String, zorunlu)

### `set_phase`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `phase_name` (String, zorunlu), `phase_type` (String, varsayılan: 'created')

### `set_workset`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `workset_name` (String, zorunlu)

### `workset_by_level`

- **Çıktı:** `object`
- **Parametreler:** `prefix` (String, varsayılan: ''), `suffix` (String, varsayılan: ''), `dry_run` (Boolean, varsayılan: False)


## Oda

### `room_area_breakdown`

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_boundary_extract`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `include_openings` (Boolean, varsayılan: True), `fire_param` (String, varsayılan: 'Yangın Dayanım Süresi')
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `loop_index`, `segment_index`, `host_wall_id`, `host_wall_type`, `fire_rating`, `is_exterior`, `start_x`, `start_y`, `end_x`, `end_y`, `length_mm`, `direction_deg`, `has_opening`

### `room_finish_assign`

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_finish_matrix`

- **Çıktı:** `object`
- **Parametreler:** yok

### `room_finish_validate`

- **Çıktı:** `object`
- **Parametreler:** `required_params` (List<string>, önerilen)

### `room_naming_normalize`

- **Çıktı:** `object`
- **Parametreler:** `prefix` (String, varsayılan: '')

### `room_to_ifc_space`

- **Çıktı:** `object`
- **Parametreler:** yok


## Oluşturma

### `create_floor`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu), `level_name` (String, zorunlu), `offset_mm` (Double, varsayılan: 0), `points` (String, varsayılan: '')

### `create_grid`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `x1_mm` (Double, varsayılan: 0), `y1_mm` (Double, varsayılan: 0), `x2_mm` (Double, varsayılan: 1000), `y2_mm` (Double, varsayılan: 0), `name` (String, varsayılan: '')

### `create_level`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `elevation_mm` (Double, varsayılan: 0), `name` (String, varsayılan: '$"Kat {elevFt*304.8/1000:F2}m"')

### `create_room`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `level_name` (String, zorunlu), `x_mm` (Double, varsayılan: 0), `y_mm` (Double, varsayılan: 0), `name` (String, varsayılan: 'Oda'), `number` (String, varsayılan: '')

### `create_wall`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** `type_name` (String, zorunlu), `level_name` (String, zorunlu), `mode` (String, varsayılan: 'from_lines'), `height_mm` (Double, varsayılan: 3000), `flip` (Boolean, varsayılan: False), `structural` (Boolean, varsayılan: False), `skip_existing` (Boolean, varsayılan: True), `reference` (String, varsayılan: 'centerline')


## Parametre

### `add_shared_params`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `spf_path` (String, varsayılan: 'ctx.GetString("path", ""'), `group_filter` (String, varsayılan: '')

### `assign_poz_number`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, varsayılan: 'EGBIM_PozNo'), `prefix` (String, varsayılan: ''), `start_from` (Int32, varsayılan: 1)

### `copy_param`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `source_param` (String, önerilen), `target_param` (String, önerilen)

### `family_ensure_loaded`

- **Çıktı:** `bool`
- **Girdi:** `—`
- **Parametreler:** `family_path` (String, zorunlu)

### `generate_ids`

- **Çıktı:** `string`
- **Girdi:** `List<Element>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO IDS'), `output_path` (String, varsayılan: 'Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop')

### `list_shared_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** yok

### `param_exists`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `param_validate_schema`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `schema_path` (String, varsayılan: 'ctx.GetString("path", ""'), `input` (Object, varsayılan: ''), `elements` (Object, varsayılan: '')

### `read_builtin_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `builtin_param` (String, önerilen)

### `read_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `read_param_with_fallback`

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `List<Element>`
- **Parametreler:** `output_field` (String, varsayılan: 'value'), `param_names` (List<string>, varsayılan: 'new List<string>(')

### `read_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (String, önerilen)

### `type_get`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `type_read_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen)

### `validate_required_params`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `required_params` (String, önerilen)

### `write_param`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_name` (String, önerilen), `value` (String, önerilen)

### `write_param_from_rows`

- **Çıktı:** `int`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_name` (String, zorunlu), `value_field` (String, önerilen)

### `write_row_param`

- **Çıktı:** `int`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_name` (String, önerilen), `value_key` (String, önerilen)


## PreCheck

### `precheck_active_document_exists`

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** yok

### `precheck_element_exists`

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `min_count` (Int32, varsayılan: 1), `categories` (List<String>, önerilen)

### `precheck_family_loaded`

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `family_name` (String, varsayılan: '')

### `precheck_file_exists`

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `path` (String, varsayılan: '')

### `precheck_model_has_elements`

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `min_count` (Int32, varsayılan: 1), `categories` (List<String>, önerilen)

### `precheck_param_exists`

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `param` (String, varsayılan: ''), `categories` (List<String>, önerilen)

### `precheck_parameter_bound`

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `param` (String, varsayılan: ''), `categories` (List<String>, önerilen)

### `precheck_registry_key_exists`

- **Çıktı:** `(bool, string?)`
- **Girdi:** `—`
- **Parametreler:** `key` (String, varsayılan: '')


## Proje Yönetimi

### `pm_get_project_info`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** yok

### `pm_model_delta_summary`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `previous_ids` (List<long>, önerilen)

### `pm_validate_lod`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `lod_level` (String, varsayılan: 'LOD300'), `required_params` (List<string>, önerilen)

### `pm_validate_naming_convention`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `name_pattern` (String, varsayılan: '')


## QA/QC

### `qa_check_approved_families`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `approved_family_keywords` (List<string>, önerilen)

### `qa_check_level_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `qa_detect_duplicates`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `tolerance_mm` (Double, varsayılan: 10.0)

### `qa_find_empty_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `param_names` (List<string>, önerilen)

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

- **Çıktı:** `object`
- **Parametreler:** `filename` (String, varsayılan: 'EG_Kalip_BOQ'), `out_dir` (String, varsayılan: '')
- **Okur:** `poz_data`

### `kalip_report`

- **Çıktı:** `object`
- **Parametreler:** `open_browser` (Boolean, varsayılan: True), `out_dir` (String, varsayılan: '')
- **Okur:** `kalip_traces`, `poz_data`

### `schedule_export_anchored`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `None`
- **Parametreler:** `schedule_name` (String, varsayılan: ''), `include_anchor` (String, varsayılan: 'true')
- **Çıktı alanları:** `__egbimoto_uid__`, `(schedule sütunları dinamik)`

### `schedule_roundtrip_apply`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `apply_type_params` (String, varsayılan: 'true')
- **Çıktı alanları:** `uid`, `field`, `old_value`, `new_value`, `status`, `message`

### `schedule_roundtrip_diff`

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
- **Parametreler:** `script_path` (String, varsayılan: ''), `cache` (bool, varsayılan: True)
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

### `classify_elements`

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
- **Parametreler:** `type_name` (String, önerilen), `category` (String, önerilen)

### `resolve_discipline`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `category` (String, önerilen)


## Sistem

### `eg_addin_disable_unused`

- **Çıktı:** `Dictionary`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: 'GetRevitVersion(ctx'), `keep` (List<String>, önerilen)
- **Çıktı alanları:** `disabled_count`, `skipped_readonly`, `failed_count`, `restart_required`

### `eg_addin_restore_all`

- **Çıktı:** `Dictionary`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: 'GetRevitVersion(ctx')
- **Çıktı alanları:** `restored_count`, `failed_count`, `restart_required`

### `eg_addin_restore_single`

- **Çıktı:** `bool`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: 'GetRevitVersion(ctx'), `addin_file` (String, varsayılan: '')

### `eg_addin_scan`

- **Çıktı:** `List<Dictionary>`
- **Girdi:** `None`
- **Parametreler:** `revit_version` (String, varsayılan: 'GetRevitVersion(ctx'), `include_disabled` (Boolean, varsayılan: True)
- **Çıktı alanları:** `fileName`, `disabled`, `directory`, `name`, `addinId`

### `eg_health_snapshot`

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
- **Parametreler:** `type_name` (String, önerilen)

### `collect_by_workset`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `workset_name` (String, önerilen)

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
- **Parametreler:** `phase_name` (String, varsayılan: ''), `categories` (List<string>, varsayılan: 'new List<string>(')

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
- **Parametreler:** `host_id` (Int32, varsayılan: -1)

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

### `intersect_report`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (opsiyonel — yoksa source_categories kullanılır)`
- **Parametreler:** `include_no_host` (Boolean, varsayılan: True), `source_categories` (List<String>, önerilen), `target_categories` (List<String>, önerilen)
- **Çıktı alanları:** `element_id`, `kategori`, `tip`, `host_id`, `host_kategori`, `host_tip`, `no_host`


## Trace

### `compare_run_result`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu)
- **Çıktı alanları:** `element_id`, `status`

### `delete_generated`

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** `trace_key` (String, zorunlu), `confirm` (Boolean, varsayılan: False)

### `trace_find_existing`

- **Çıktı:** `List<Element>`
- **Girdi:** `—`
- **Parametreler:** `trace_key` (String, zorunlu)

### `trace_write`

- **Çıktı:** `int`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu)

### `update_or_create_family`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `trace_key` (String, zorunlu), `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, varsayılan: ''), `update_type` (Boolean, varsayılan: True), `update_location` (Boolean, varsayılan: False)
- **Çıktı alanları:** `element_id`, `index`, `action`, `status`


## UI

### `select_by_id`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `element_id` (String, varsayılan: ''), `ids` (List<String>, önerilen)
- **Çıktı alanları:** `selected`


## Veri

### `assign_wbs_code`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `category_field` (String, varsayılan: 'kategori'), `aktivite_field` (String, varsayılan: '')
- **Okur:** `wbs_mapping`
- **Çıktı alanları:** `wbs_kodu`

### `data_get`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `key` (String, önerilen)

### `data_list_keys`

- **Çıktı:** `List<string>`
- **Girdi:** `—`
- **Parametreler:** yok

### `data_load`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `path` (String, önerilen), `root_key` (String, varsayılan: ''), `cache_bust` (Boolean, varsayılan: False), `key` (String, varsayılan: 'Path.GetFileNameWithoutExtension(fullPath')

### `export_wbs_report`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `output_path` (String, varsayılan: 'Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop')

### `link_quantity_to_wbs`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `quantity_field` (String, varsayılan: 'hacim_m3'), `wbs_field` (String, varsayılan: 'wbs_kodu')

### `load_ifc_mapping`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `ifc_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "mapping", "ifc_mapping.json"')
- **Yazar:** `ifc_mapping`

### `load_poz_data`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `poz_path` (String, varsayılan: 'ctx.GetString("path",\n                ctx.GetString("file",\n                Path.Combine(EgbimotoData.DataRoot, "poz", "csb_poz_2026.json"'), `file` (String, önerilen)
- **Yazar:** `poz_data`
- **Çıktı alanları:** `birim_fiyat`

### `load_qa_matrix`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `qa_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "semantic", "qa_rule_matrix.json"')
- **Yazar:** `qa_matrix`

### `load_rayic`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `—`
- **Parametreler:** `rayic_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "poz", "rayic_2026.json"')
- **Yazar:** `rayic_data`

### `load_shared_param_map`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `map_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "mapping", "shared_param_map.json"')
- **Yazar:** `shared_param_map`

### `load_wbs_mapping`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `wbs_path` (String, varsayılan: 'Path.Combine(EgbimotoData.DataRoot, "mapping", "revit_kategori_wbs.json"')
- **Yazar:** `wbs_mapping`

### `lookup_value`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `key_field` (String, önerilen), `lookup_key` (String, önerilen), `result_field` (String, varsayılan: 'lookup_result')

### `pivot_table`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `row_field` (String, zorunlu), `col_field` (String, zorunlu), `value_field` (String, zorunlu), `func` (String, varsayılan: 'sum')


## Yangın

### `fa_classify_room_detector`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `heat_detector_keywords` (List<string>, önerilen)

### `fa_validate_circuit_assigned`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `fa_validate_device_in_room`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fa_devices` (List<Element>, önerilen), `smoke_keywords` (List<string>, önerilen), `heat_keywords` (List<string>, önerilen)

### `fa_validate_mounting_height`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `min_aff_mm` (Double, varsayılan: 2000.0), `max_aff_mm` (Double, varsayılan: 2400.0), `device_filter` (String, varsayılan: '')

### `fp_compartment_area_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_kullanimi` (String, varsayılan: 'ofis'), `kompartiman_alani_m2` (Double, varsayılan: 0), `kat_sayisi` (Int32, varsayılan: 1), `sprinkler_var` (Boolean, varsayılan: False)
- **Çıktı alanları:** `bina_kullanimi`, `mevcut_alan_m2`, `mevcut_kat_sayisi`, `max_alan_m2`, `max_kat_sayisi`, `sprinkler_var`, `alan_uygun`, `kat_uygun`, `mesajlar`, `standart`, `durum`

### `fp_detection_coverage`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `oda_alani_m2` (Double, varsayılan: 0), `dedektör_tipi` (String, varsayılan: 'duman'), `tavan_yuksekligi_m` (Double, varsayılan: 2.7), `mevcut_sayi` (Int32, varsayılan: 0)
- **Çıktı alanları:** `oda_alani_m2`, `dedektör_tipi`, `tavan_yuksekligi_m`, `kapsama_m2_per_cihaz`, `gerekli_sayi`, `mevcut_sayi`, `max_aralik_m`, `durum`

### `fp_evacuation_route_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `yol_tipi` (String, varsayılan: 'koridor'), `mevcut_genislik_m` (Double, varsayılan: 0), `kisi_sayisi` (Int32, varsayılan: 0), `cikis_mesafesi_m` (Double, varsayılan: 0), `sprinkler_var` (Boolean, varsayılan: False)
- **Çıktı alanları:** `yol_tipi`, `kisi_sayisi`, `min_genislik_m`, `mevcut_genislik_m`, `max_cikis_mesafesi_m`, `max_cikmaz_m`, `sprinkler_var`, `kontroller`, `durum`

### `fp_exit_sign_spacing`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `koridor_uzunlugu_m` (Double, varsayılan: 0), `isaret_yuksekligi_mm` (Int32, varsayılan: 150), `mevcut_isaret_sayisi` (Int32, varsayılan: 0), `montaj_yuksekligi_m` (Double, varsayılan: 2.2)
- **Çıktı alanları:** `koridor_uzunlugu_m`, `isaret_yuksekligi_mm`, `max_gorunurluk_m`, `gerekli_sayi`, `mevcut_sayi`, `montaj_yuksekligi_m`, `montaj_uygun`, `montaj_notu`, `standart`, `durum`

### `fp_fdc_clearance_check`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (GenericModel FDC)`
- **Parametreler:** `min_aff_mm` (Int32, varsayılan: 450), `min_temizlik_mm` (Int32, varsayılan: 3000), `write_back` (Boolean, varsayılan: False)
- **Okur:** `EG_FdcTemizlik`, `EG_FdcYukseklik`
- **Yazar:** `EG_FdcUygunluk`
- **Çıktı alanları:** `fdc_id`, `yukseklik_mm`, `temizlik_mm`, `kontroller`, `durum`

### `fp_fire_door_rating_check`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (Doors)`
- **Parametreler:** `min_rating_dak` (Int32, varsayılan: 60), `write_back` (Boolean, varsayılan: False), `tolerance_mm` (Int, önerilen)
- **Okur:** `EG_KapiAltBosluk`, `EG_KapiUstBosluk`, `EG_YanginKapiRating`
- **Yazar:** `EG_KapiUygunluk`
- **Çıktı alanları:** `kapi_id`, `mevcut_rating`, `gerekli_rating`, `rating_durum`, `ust_bosluk_mm`, `alt_bosluk_mm`, `bosluk_durum`, `durum`

### `fp_pump_hp_calc`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `debi_lpm` (Double, varsayılan: 0), `toplam_yuk_m` (Double, varsayılan: 0), `pompa_verim` (Double, varsayılan: 0.7), `motor_verim` (Double, varsayılan: 0.9), `guvenlik_faktor` (Double, varsayılan: 1.25)
- **Çıktı alanları:** `debi_lpm`, `debi_m3s`, `toplam_yuk_m`, `pompa_verim`, `motor_verim`, `guvenlik_faktor`, `ph_kw`, `pb_kw`, `pm_kw`, `oneri_motor_kw`, `durum`

### `fp_pump_schedule_validate`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `ana_pompa_lpm` (Double, varsayılan: 0), `ana_pompa_bar` (Double, varsayılan: 0), `jockey_lpm` (Double, varsayılan: -1), `diesel_gerekli` (Boolean, varsayılan: True), `kat_sayisi` (Int32, varsayılan: 0)
- **Çıktı alanları:** `ana_pompa_lpm`, `ana_pompa_bar`, `yuzde150_lpm`, `yuzde150_min_bar`, `kapatma_max_bar`, `jockey_lpm`, `jockey_max_lpm`, `jockey_uygun`, `diesel_gerekli`, `gereksinimler`, `durum`

### `fp_sprinkler_hydraulic`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `k_faktoru` (String, varsayılan: 'K80'), `isletme_basinc_bar` (Double, varsayılan: 0), `sprinkler_sayisi` (Int32, varsayılan: 1), `tehlike_sinifi` (String, varsayılan: 'orta_1')
- **Çıktı alanları:** `k_faktoru`, `k_degeri`, `isletme_basinc_bar`, `min_basinc_bar`, `debi_lpm`, `min_debi_lpm`, `sprinkler_sayisi`, `toplam_debi_lpm`, `tehlike_sinifi`, `min_tehlike_debi`, `basinc_uygun`, `tehlike_uygun`, `durum`

### `fp_sprinkler_temp_class`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `ortam_sicaklik_c` (Double, varsayılan: 0), `mevcut_sinif` (String, varsayılan: ''), `guvenlik_marji_c` (Double, varsayılan: 30.0)
- **Çıktı alanları:** `ortam_sicaklik_c`, `guvenlik_marji_c`, `gerekli_min_c`, `gerekli_sinif`, `gerekli_renk`, `mevcut_sinif`, `mevcut_renk`, `durum`

### `fp_standpipe_pressure`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_yuksekligi_m` (Double, varsayılan: 0), `pompa_basinc_bar` (Double, varsayılan: 0), `kat_yuksekligi_m` (Double, varsayılan: 3.0), `max_hortum_bar` (Double, varsayılan: 6.9)
- **Çıktı alanları:** `bina_yuksekligi_m`, `pompa_basinc_bar`, `statik_basinc_bar`, `toplam_basinc_bar`, `max_hortum_bar`, `max_kat_per_zon`, `prv_sayisi`, `prv_kat_konumlari`, `durum`

### `fp_standpipe_qa`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_yuksekligi_m` (Double, varsayılan: 0), `mevcut_dn_mm` (Int32, varsayılan: 0), `sistem_tipi` (String, varsayılan: 'islak'), `kat_sayisi` (Int32, varsayılan: 0)
- **Çıktı alanları:** `bina_yuksekligi_m`, `kat_sayisi`, `sistem_tipi`, `gerekli_dn_mm`, `mevcut_dn_mm`, `dn_durum`, `tip_durum`, `gereksinimler`, `durum`

### `fp_suppression_agent_qa`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `ajan_tipi` (String, varsayılan: ''), `alan_tipi` (String, varsayılan: '')
- **Çıktı alanları:** `ajan_tipi`, `alan_tipi`, `uygun_mu`, `alternatifler`, `standartlar`, `uyarilar`, `durum`

### `fp_validate_sprinkler_coverage`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_coverage_m2` (Double, varsayılan: 12.0)


## Yangın Hesap

### `calc_duct_sheet_weight`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `thickness_mm` (Double, varsayılan: 0.8), `density_kg_m3` (Double, varsayılan: 7850.0), `width_mm` (Double, varsayılan: 0), `height_mm` (Double, varsayılan: 0), `length_m` (Double, varsayılan: 0)
- **Çıktı alanları:** `duct_id`, `width_mm`, `height_mm`, `length_m`, `perimeter_mm`, `thickness_mm`, `sheet_volume_m3`, `sheet_weight_kg`

### `calc_sprinkler_design_density`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `—`
- **Parametreler:** `hazard_class` (String, zorunlu), `system_type` (String, varsayılan: 'islak'), `area_m2` (Double, varsayılan: 0)
- **Çıktı alanları:** `hazard_class`, `design_density_mm_per_min`, `protection_area_m2`, `required_flow_lpm`, `total_pump_flow_lpm`, `reference`

### `fire_hose_cabinet_spacing_check`

- **Çıktı:** `ValidationReport`
- **Girdi:** `List<Element>`
- **Parametreler:** `max_spacing_m` (Double, varsayılan: 30.0), `hose_length_m` (Double, varsayılan: 20.0)


## Yapısal

### `column_setup_params`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `-`
- **Parametreler:** `spf_path` (String, varsayılan: '')
- **Çıktı alanları:** `added`, `skipped`, `spf_path`

### `struct_beam_depth_ratio`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (StructuralFraming-Beam)`
- **Parametreler:** `kiriş_tipi` (String, varsayılan: 'surekli'), `min_genislik_mm` (Int32, varsayılan: 250), `write_back` (Boolean, varsayılan: False)
- **Yazar:** `EG_KirisUygunluk`
- **Çıktı alanları:** `eleman_id`, `aciklik_mm`, `yukseklik_mm`, `genislik_mm`, `h_l_orani`, `min_h_mm`, `durum`

### `struct_concrete_class_qa`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `beton_sinifi` (String, varsayılan: 'C25'), `kullanim_yeri` (String, varsayılan: 'genel'), `deprem_bolgesi` (Boolean, varsayılan: True), `eleman_tipi` (String, varsayılan: 'kolon')
- **Çıktı alanları:** `beton_sinifi`, `fck_mpa`, `fcd_mpa`, `fctd_mpa`, `fbd_mpa`, `kullanim_yeri`, `eleman_tipi`, `min_sinif`, `min_fck_mpa`, `uyarilar`, `durum`

### `struct_formwork_type_select`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bina_tipi` (String, varsayılan: 'konut'), `kat_sayisi` (Int32, varsayılan: 5), `tekrar_oncelik` (Boolean, varsayılan: False), `hiz_oncelik` (Boolean, varsayılan: False), `mevcut_tip` (String, varsayılan: '')
- **Çıktı alanları:** `bina_tipi`, `kat_sayisi`, `oneri_tip`, `oneri_neden`, `mevcut_tip`, `mevcut_durum`, `karsilastirma`, `durum`

### `struct_foundation_bearing`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `eksenel_yuk_kn` (Double, varsayılan: 0), `temel_genislik_m` (Double, varsayılan: 0), `temel_uzunluk_m` (Double, varsayılan: 0), `zemin_emniyet_kpa` (Double, varsayılan: 150.0), `moment_knm` (Double, varsayılan: 0), `temel_tipi` (String, varsayılan: 'tekil')
- **Çıktı alanları:** `eksenel_yuk_kn`, `temel_genislik_m`, `temel_uzunluk_m`, `temel_alani_m2`, `taban_basinci_kpa`, `dismerkezlik_m`, `e_max_m`, `q_max_kpa`, `zemin_emniyet_kpa`, `kapasite_pct`, `mesajlar`, `durum`

### `struct_rebar_anchorage`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `cap_mm` (Int32, varsayılan: 16), `beton_sinifi` (String, varsayılan: 'C25'), `celik_sinifi` (String, varsayılan: 'B420C'), `ankraj_tipi` (String, varsayılan: 'duz'), `basinc_mi` (Boolean, varsayılan: False)
- **Çıktı alanları:** `cap_mm`, `beton_sinifi`, `celik_sinifi`, `fyd_mpa`, `fbd_mpa`, `lb0_mm`, `ankraj_tipi`, `beta`, `lb_mm`, `min_ankraj_mm`, `basinc_mi`, `durum`

### `struct_rebar_lap_kolon`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `cap_mm` (Int32, varsayılan: 16), `beton_sinifi` (String, varsayılan: 'C25'), `celik_sinifi` (String, varsayılan: 'B420C'), `kolon_l_d_orani` (Double, varsayılan: 3.0), `deprem_bolgesi` (Boolean, varsayılan: True), `basinc_mi` (Boolean, varsayılan: True)
- **Çıktı alanları:** `cap_mm`, `beton_sinifi`, `celik_sinifi`, `fyd_mpa`, `fbd_mpa`, `lb0_mm`, `alpha`, `bindirme_mm`, `filiz_min_mm`, `l_d_orani`, `l_d_tipi`, `deprem_bolgesi`, `durum`

### `struct_rebar_lap_perde`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `cap_mm` (Int32, varsayılan: 12), `beton_sinifi` (String, varsayılan: 'C25'), `celik_sinifi` (String, varsayılan: 'B420C'), `donati_yonu` (String, varsayılan: 'dusey'), `perde_kalinlik_mm` (Double, varsayılan: 200), `perde_yukseklik_mm` (Double, varsayılan: 3000)
- **Çıktı alanları:** `cap_mm`, `beton_sinifi`, `donati_yonu`, `lb0_mm`, `bindirme_mm`, `uc_bolge_mm`, `min_donati_orani`, `l_d_orani`, `l_d_notu`, `durum`

### `struct_slab_thickness`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `aciklik_mm` (Double, varsayılan: 0), `doseme_tipi` (String, varsayılan: 'cift_yonlu'), `mevcut_kalinlik_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `aciklik_mm`, `doseme_tipi`, `hesap_min_mm`, `min_kalinlik_mm`, `mevcut_kalinlik_mm`, `h_l_orani`, `standart`, `durum`

### `struct_steel_bolt_type_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `none`
- **Parametreler:** `bulon_tipi` (String, varsayılan: 'N'), `baglanti_turu` (String, varsayılan: 'kesme_kritik'), `titresim_var` (Boolean, varsayılan: False), `dinamik_yuk` (Boolean, varsayılan: False)
- **Çıktı alanları:** `bulon_tipi`, `tanim`, `kesme_kapasitesi`, `standartlar`, `not`, `baglanti_turu`, `titresim_var`, `dinamik_yuk`, `oneri_tip`, `oneri_neden`, `durum`

### `struct_wall_slenderness`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `none (Structural Walls)`
- **Parametreler:** `max_narinlik` (Double, varsayılan: 25.0), `min_kalinlik_mm` (Int32, varsayılan: 200), `write_back` (Boolean, varsayılan: False)
- **Yazar:** `EG_PerdeUygunluk`
- **Çıktı alanları:** `duvar_id`, `yukseklik_mm`, `kalinlik_mm`, `narinlik_h_t`, `max_narinlik`, `durum`

### `structural_collect_all`

- **Çıktı:** `object`
- **Parametreler:** `level` (String, varsayılan: ''), `categories` (List<string>, önerilen)

### `structural_column_presizing`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element> (StructuralColumn)`
- **Parametreler:** `default_fck` (Double, varsayılan: 25), `default_birim_yuk` (Double, varsayılan: 12), `duktilite` (String, varsayılan: 'yuksek'), `round_to_mm` (Double, varsayılan: 50)
- **Okur:** `EG_BetonSinif`, `EG_BirimYuk`, `EG_KatSayisi`, `EG_KolonTipi`, `EG_Ndm`, `EG_YukAlani`, `EG_fck_Override`
- **Yazar:** `EG_EksenelKontrol_TBDY`, `EG_EksenelKontrol_TS500`, `EG_GerekenAc_TBDY`, `EG_HesapTarihi`, `EG_KirisKolonAyrim`, `EG_KolonDurumu`, `EG_KullanilanNdm`, `EG_MevcutAc`, `EG_MevcutB`, `EG_MevcutH`, `EG_MinBoyutKontrol`, `EG_OneriBoyut`
- **Çıktı alanları:** `kolon_id`, `b_mm`, `h_mm`, `ac_mm2`, `fck_mpa`, `ndm_kn`, `gereken_ac`, `oneri_boyut`, `min_boyut`, `tbdy_eksenel`, `ts500`, `durum`

### `structural_continuity_check`

- **Çıktı:** `object`
- **Parametreler:** `tolerans_mm` (Double, varsayılan: 50.0)

### `structural_level_summary`

- **Çıktı:** `object`
- **Parametreler:** yok

### `structural_material_check`

- **Çıktı:** `object`
- **Parametreler:** `beton_required` (Boolean, varsayılan: True), `celik_required` (Boolean, varsayılan: False)

### `structural_tbdy_params`

- **Çıktı:** `object`
- **Parametreler:** `deprem_bolgesi` (String, zorunlu), `duktilite_sinifi` (String, varsayılan: 'YSBD'), `beton_sinif` (String, varsayılan: ''), `celik_sinif` (String, varsayılan: '')

### `structural_ts500_section`

- **Çıktı:** `object`
- **Parametreler:** `override_min_kolon_b` (Double, varsayılan: 'MIN_KOLON_B'), `override_min_kolon_h` (Double, varsayılan: 'MIN_KOLON_H')


## Yapısal Oluşturma

### `create_beam_by_curve`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `level_name` (String, zorunlu), `offset_mm` (Double, varsayılan: 0)

### `create_column_by_point`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `base_level` (String, zorunlu), `top_level` (String, varsayılan: ''), `height_mm` (Double, varsayılan: 3000), `structural` (Boolean, varsayılan: True)

### `create_grid_by_line`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `name_prefix` (String, varsayılan: 'G'), `start_index` (Int32, varsayılan: 1)

### `place_adaptive_component_by_points`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<object>`
- **Parametreler:** `family_name` (String, zorunlu), `type_name` (String, zorunlu), `points_per_item` (Int32, varsayılan: 2)


## Yardımcı

### `assert_not_empty`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `message` (String, varsayılan: 'Beklenen veri bulunamadı.')

### `compute`

- **Çıktı:** `double`
- **Girdi:** `—`
- **Parametreler:** `field` (String, önerilen), `func` (String, varsayılan: 'sum'), `a` (Double, önerilen), `b` (Double, varsayılan: 1.0), `op` (String, varsayılan: 'mul')

### `count_items`

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** yok

### `echo`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `value` (String, varsayılan: ''), `message` (String, önerilen)

### `element_count`

- **Çıktı:** `int`
- **Girdi:** `—`
- **Parametreler:** yok

### `eq`

- **Çıktı:** `bool`
- **Parametreler:** `left` (Object, varsayılan: ''), `right` (Object, varsayılan: '')

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
- **Parametreler:** `value` (Double, önerilen), `format` (String, varsayılan: 'N2'), `unit` (String, varsayılan: '')

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
- **Parametreler:** `value` (Object, varsayılan: ''), `key` (String, önerilen)

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
- **Parametreler:** `message` (String, varsayılan: 'ctx.Input?.ToString('), `file_path` (String, varsayılan: '')


## Yerleştirme

### `calc_placement_point`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `offset_x_mm` (Double, varsayılan: 0), `offset_y_mm` (Double, varsayılan: 0), `height_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `x`, `y`, `z`, `level_id`

### `collect_doors_in_room`

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok

### `get_door_wall_clearances`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `left_mm`, `right_mm`, `wall_length_mm`, `door_width_mm`

### `get_room_ceiling_center`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `room_name`, `x`, `y`, `z`

### `place_family_along_mep`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, varsayılan: ''), `type_name` (String, varsayılan: ''), `spacing_mm` (Double, varsayılan: 2000), `end_setback_mm` (Double, varsayılan: 300), `vertical_offset_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `host_id`, `category`, `placed_count`, `spacing_mm`, `run_length_m`

### `place_family_on_ceiling`

- **Çıktı:** `List<Dictionary<string,object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, önerilen), `type_name` (String, önerilen), `offset_x_mm` (Double, varsayılan: 0), `offset_y_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `host_id`, `placed`, `mode`

### `place_family_on_wall`

- **Çıktı:** `List<Dictionary<string,object?>>`
- **Girdi:** `List<Element>`
- **Parametreler:** `family_name` (String, önerilen), `type_name` (String, önerilen), `offset_mm` (Double, varsayılan: 1200), `spacing_mm` (Double, varsayılan: 0), `sill_mm` (Double, varsayılan: 0)
- **Çıktı alanları:** `host_id`, `placed_count`, `wall_length_m`


## Çizim

### `dimension_continuous_selection`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `dim_type` (String, varsayılan: ''), `categories` (StringList, önerilen)
- **Çıktı alanları:** `created`, `segments`, `skipped`

### `dimension_to_nearest_grid`

- **Çıktı:** `Dictionary<string, object?>`
- **Girdi:** `List<Element>`
- **Parametreler:** `dim_type` (String, varsayılan: ''), `search_radius_m` (Double, varsayılan: 9.0), `categories` (StringList, önerilen)
- **Çıktı alanları:** `created`, `skipped`


## Çıktı

### `element_report`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Eleman Raporu')

### `export_csv`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file_path` (String, varsayılan: ''), `rows` (Object, varsayılan: '')

### `export_html_report`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Rapor'), `rows` (Object, varsayılan: '')

### `export_pdf`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Rapor')

### `export_row_report`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Raporu'), `fields` (List<string>, önerilen)

### `export_validation_report`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Doğrulama Raporu'), `file_path` (String, varsayılan: 'Path.Combine(\n                Environment.GetFolderPath(Environment.SpecialFolder.Desktop'), `rows` (Object, varsayılan: ''), `input` (Object, varsayılan: '')

### `export_xlsx`

- **Çıktı:** `string`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file_path` (String, varsayılan: ''), `sheet_name` (String, varsayılan: 'EGBIMOTO'), `title` (String, varsayılan: 'EGBIMOTO'), `rows` (Object, varsayılan: '')

### `show_table`

- **Çıktı:** `object?`
- **Girdi:** `—`
- **Parametreler:** `title` (String, varsayılan: 'EGBIMOTO Sonuç'), `max_rows` (Int32, varsayılan: 20)

### `xlsx_import_preview`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Dictionary<string, object?>> (opsiyonel — karşılaştırma için orijinal satırlar)`
- **Parametreler:** `file_path` (String, zorunlu), `key_field` (String, varsayılan: 'element_id')
- **Çıktı alanları:** `_status`, `_diff_fields`


## Önizleme

### `preview_collect_geometry`

- **Çıktı:** `PreviewGeometryDto`
- **Girdi:** `List<Element>`
- **Parametreler:** `operation_name` (String, varsayılan: 'Önizleme'), `include_labels` (Boolean, varsayılan: True), `max_elements` (Int32, varsayılan: 500)

### `preview_gate`

- **Çıktı:** `string`
- **Girdi:** `—`
- **Parametreler:** yok
