# EGBIMOTO — Op Referansı

Toplam **373 operasyon**, 16 kategori.

Bu dosya `op_contracts.json`'dan otomatik üretilmiştir. Her op bir manifest adımında `"op": "<isim>"` olarak kullanılır. 🔒 işareti yazma (transaction gerektiren) op'ları belirtir.

---

## İçindekiler

- [Aile](#aile) — 5 op
- [Analiz](#analiz) — 2 op
- [Cephe](#cephe) — 8 op
- [Donatı](#donati) — 1 op
- [Duvar](#duvar) — 2 op
- [ETL](#etl) — 2 op
- [Genel](#genel) — 306 op
- [Koordinasyon](#koordinasyon) — 4 op
- [Maliyet](#maliyet) — 4 op
- [Modelleme](#modelleme) — 1 op
- [Oda](#oda) — 6 op
- [Parametre](#parametre) — 1 op
- [Raporlama](#raporlama) — 2 op
- [Sistem](#sistem) — 5 op
- [Yapısal](#yapisal) — 6 op
- [geometry](#geometry) — 18 op


## Aile

### `family_add_param` 🔒

Aktif aile belgesine yeni bir instance veya type parametresi ekler. 

- **Çıktı:** `object`
- **Parametreler:** yok

### `family_batch_load` 🔒

Belirtilen klasördeki tüm .rfa dosyalarını projeye toplu yükler. 

- **Çıktı:** `object`
- **Parametreler:** yok

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


## Donatı

### `rebar_weight_calc`

Donatı listesini çap tablosu + fabrika boyu + bindirme katsayısıyla hesaplar. 

- **Çıktı:** `object`
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
- **Parametreler:** yok

### `load_poz_section_rules`

data/poz/poz_section_rules.json dosyasını registry'ye yükler. canonical_class → poz prefix listesi.

- **Çıktı:** `Dictionary`
- **Parametreler:** yok


## Genel

### `add_column`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` *(zorunlu)*, `value` *(zorunlu)*

### `add_shared_params`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `spf_path`, `group_filter`

### `arch_apply_view_template`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `template_name`, `view_name_contains`

### `arch_check_fire_rating_continuity`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_fire_rating`

### `arch_check_material_assigned`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `arch_check_windowless_rooms`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `arch_renumber_doors`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `prefix`

### `arch_sheets_from_data`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `arch_validate_accessibility`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_width_mm`

### `arch_validate_ceiling_height`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_height_mm`

### `arch_validate_room_area`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_area_m2`

### `arch_validate_room_naming`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `assert_not_empty`

- **Çıktı:** `object?`
- **Parametreler:** `message`

### `assign_egbim_mark`

- **Çıktı:** `int`
- **Parametreler:** yok

### `assign_poz_number`

- **Çıktı:** `int`
- **Parametreler:** `param_name`, `prefix`, `start_from`

### `assign_wbs_code`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `category_field`
- **Çıktı alanları:** `wbs_kodu`

### `beton_metraj`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `build_spatial_graph`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `include_exterior`, `deduplicate`
- **Çıktı alanları:** `from_room_id`, `from_room_name`, `from_room_number`, `to_room_id`, `to_room_name`, `to_room_number`, `shared_wall_id`, `shared_wall_type`

### `cable_tray_fill_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `fill_param`, `max_fill_pct`

### `calc_ach_airflow`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `area_m2`, `height_m`, `ach`, `mode`
- **Çıktı alanları:** `room_id`, `room_name`, `area_m2`, `height_m`, `volume_m3`, `ach`, `airflow_cmh`, `airflow_cfm`

### `calc_anchorage_length`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `diameter_mm`, `fck`, `fyk`, `cover_mm`, `hook`

### `calc_brick_quantity`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `area_m2`, `thickness_cm`, `brick_type`, `mortar_ratio`, `waste_pct`
- **Çıktı alanları:** `wall_id`, `wall_type`, `area_m2`, `thickness_cm`, `wall_volume_m3`, `mortar_m3`, `net_brick_m3`, `brick_count`

### `calc_cost`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `quantity_field`
- **Çıktı alanları:** `miktar`, `toplam_maliyet`

### `calc_duct_sheet_weight`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `thickness_mm`, `density_kg_m3`, `width_mm`, `height_mm`, `length_m`
- **Çıktı alanları:** `duct_id`, `width_mm`, `height_mm`, `length_m`, `perimeter_mm`, `thickness_mm`, `sheet_volume_m3`, `sheet_weight_kg`

### `calc_hazen_williams`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `flow_rate_lpm`, `pipe_diam_mm`, `c_factor`, `pipe_length_m`
- **Çıktı alanları:** `pipe_id`, `diam_mm`, `flow_lpm`, `c_factor`, `length_m`, `friction_loss_bar_per_m`, `total_loss_bar`, `velocity_m_s`

### `calc_lap_length`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `diameter_mm`, `fck`, `fyk`, `cover_mm`

### `calc_min_spacing`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `diameter_mm`, `aggregate_size_mm`

### `calc_placement_point`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `offset_x_mm`, `offset_y_mm`, `height_mm`
- **Çıktı alanları:** `x`, `y`, `z`, `level_id`

### `calc_room_lux`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `lumen_param`, `cu`, `mf`, `target_lux`
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `area_m2`, `fixture_count`, `total_lumens`, `cu`, `mf`

### `calc_sprinkler_design_density`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `hazard_class` *(zorunlu)*, `system_type`, `area_m2`
- **Çıktı alanları:** `hazard_class`, `design_density_mm_per_min`, `protection_area_m2`, `required_flow_lpm`, `total_pump_flow_lpm`, `reference`

### `check_overlapping_rooms`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `check_unplaced_rooms`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `check_untagged_elements`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `view_id`
- **Çıktı alanları:** `element_id`, `category`, `family_type`, `level`, `tag_count`

### `check_zero_volume`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `classify_by_wbs`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `classify_elements`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `clear_script_cache`

- **Çıktı:** `Dictionary`
- **Parametreler:** yok
- **Çıktı alanları:** `cleared`

### `collect_air_terminals`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_beams`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_by_category`

- **Çıktı:** `List<Element>`
- **Parametreler:** `category`

### `collect_by_phase`

- **Çıktı:** `List<Element>`
- **Parametreler:** `phase_name` *(zorunlu)*

### `collect_by_type_name`

- **Çıktı:** `List<Element>`
- **Parametreler:** `type_name` *(zorunlu)*

### `collect_by_workset`

- **Çıktı:** `List<Element>`
- **Parametreler:** `workset_name` *(zorunlu)*

### `collect_cable_trays`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_casework`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_ceilings`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_columns`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_conduits`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_curtain_walls`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_doors`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_doors_in_room`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_duct_fittings`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_ducts`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_electrical_equipment`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_electrical_fixtures`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_elements`

- **Çıktı:** `List<Element>`
- **Parametreler:** `category`

### `collect_by_ids`

Dict satırlarındaki ID alan(lar)ından `Element` listesi toplar. Satır-tabanlı op çıktılarını (clash_detect_matrix'in a_id/b_id, scan, diff sonuçları) `move_element` / `set_param` gibi Element bekleyen op'lara köprüler. Genel amaçlı bir dönüşüm op'udur.

- **Çıktı:** `List<Element>`
- **Girdi:** `List<Dictionary<string, object?>>`
- **Parametreler:** `id_fields` (virgülle çok alan, örn. `"a_id,b_id"`, default `element_id`), `distinct`

### `collect_families`

- **Çıktı:** `List<Element>`
- **Parametreler:** `category`

### `collect_fire_alarm_devices`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_floors`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_foundations`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_furniture`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_furniture_systems`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_generic_models`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_grids`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_levels`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_lighting_devices`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_lighting_fixtures`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_linked_elements`

- **Çıktı:** `List<Element>`
- **Parametreler:** `category`

### `collect_mechanical_equipment`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_multi`

- **Çıktı:** `List<Element>`
- **Parametreler:** `categories` *(zorunlu)*

### `collect_parking`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_pipe_accessories`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_pipe_fittings`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_pipes`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_plumbing_fixtures`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_ramps`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_rebar`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_rebar_in_host`

- **Çıktı:** `List<Element>`
- **Parametreler:** `host_id`

### `collect_roofs`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_rooms`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_selected`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_sheets`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_site`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_sprinklers`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_stairs`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_structural_columns`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_structural_framing`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_structural_walls`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_topography`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_types`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `category`

### `collect_views`

- **Çıktı:** `List<Element>`
- **Parametreler:** `view_type`

### `collect_walls`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `collect_windows`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `compare_run_result`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `trace_key` *(zorunlu)*
- **Çıktı alanları:** `element_id`, `status`

### `compute`

- **Çıktı:** `double`
- **Parametreler:** `field` *(zorunlu)*, `func`, `a` *(zorunlu)*, `b`, `op`

### `conduit_fill_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `count_param`, `area_param`, `max_fill_pct`

### `coord_check_clearance`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `min_clearance_mm`

### `coord_validate_level_consistency`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `coord_validate_penetration_firestop`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `copy_param`

- **Çıktı:** `int`
- **Parametreler:** `source_param` *(zorunlu)*, `target_param` *(zorunlu)*

### `cost_by_level`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `cost_summary`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `group_by`

### `count_items`

- **Çıktı:** `int`
- **Parametreler:** yok

### `create_3d_view`

- **Çıktı:** `Element`
- **Parametreler:** `view_name`, `padding_mm`

### `create_beam_by_curve` 🔒

- **Çıktı:** `List<Element>`
- **Parametreler:** `family_name` *(zorunlu)*, `type_name` *(zorunlu)*, `level_name` *(zorunlu)*, `offset_mm`

### `create_column_by_point` 🔒

- **Çıktı:** `List<Element>`
- **Parametreler:** `family_name` *(zorunlu)*, `type_name` *(zorunlu)*, `base_level` *(zorunlu)*, `top_level`, `height_mm`, `structural`

### `create_floor`

- **Çıktı:** `List<Element>`
- **Parametreler:** `type_name` *(zorunlu)*, `level_name` *(zorunlu)*, `points`, `offset_mm`

### `create_grid`

- **Çıktı:** `List<Element>`
- **Parametreler:** `x1_mm` *(zorunlu)*, `y1_mm` *(zorunlu)*, `x2_mm` *(zorunlu)*, `y2_mm` *(zorunlu)*, `name`

### `create_grid_by_line` 🔒

- **Çıktı:** `List<Element>`
- **Parametreler:** `name_prefix`, `start_index`

### `create_level`

- **Çıktı:** `List<Element>`
- **Parametreler:** `elevation_mm` *(zorunlu)*, `name`

### `create_room`

- **Çıktı:** `List<Element>`
- **Parametreler:** `x_mm`, `y_mm`, `level_name` *(zorunlu)*, `name`, `number`

### `create_sheet`

- **Çıktı:** `Element`
- **Parametreler:** `sheet_number` *(zorunlu)*, `sheet_name` *(zorunlu)*, `title_block_name`

### `create_view_filter` 🔒

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `filter_name` *(zorunlu)*, `categories` *(zorunlu)*, `param_name`, `rule_operator`, `rule_value` *(zorunlu)*, `color_r`, `color_g`, `color_b`, `line_weight`, `fill_pattern`, `overwrite`
- **Çıktı alanları:** `view_id`, `view_name`, `filter_id`, `filter_name`, `applied`, `status`

### `create_wall`

- **Çıktı:** `List<Element>`
- **Parametreler:** `mode` *(zorunlu)*, `type_name` *(zorunlu)*, `level_name` *(zorunlu)*, `height_mm`, `reference`, `flip`, `structural`, `skip_existing`

### `csv_read`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file_path` *(zorunlu)*, `delimiter`, `has_header`, `encoding`, `skip_rows`

### `csv_write`

- **Çıktı:** `string`
- **Parametreler:** `file_path` *(zorunlu)*, `delimiter`, `include_header`, `encoding`

### `data_get`

- **Çıktı:** `object?`
- **Parametreler:** `key` *(zorunlu)*

### `data_list_keys`

- **Çıktı:** `List<string>`
- **Parametreler:** yok

### `data_load`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `path` *(zorunlu)*, `root_key`, `cache_bust`, `key`

### `delete_generated` 🔒

- **Çıktı:** `int`
- **Parametreler:** `trace_key` *(zorunlu)*, `confirm`

### `detect_undefined_system`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `discipline`
- **Çıktı alanları:** `element_id`, `category`, `family_type`, `level`, `system_type`, `fix_hint`

### `distinct_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` *(zorunlu)*

### `door_clearance_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_clear_width_mm`, `min_latch_side_mm`, `severity`

### `door_fire_rating_from_wall` 🔒

- **Çıktı:** `int`
- **Parametreler:** `wall_param`, `door_param`, `rating_map`

### `door_handing_detect`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `handing`, `swing`, `hand_flipped`, `facing_flipped`, `facing_x`, `facing_y`, `hand_x`

### `door_number_by_room` 🔒

- **Çıktı:** `int`
- **Parametreler:** `param_name`, `separator`, `start_index`

### `duct_aspect_ratio_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `max_ratio`, `severity`

### `echo`

- **Çıktı:** `object?`
- **Parametreler:** `message`

### `elec_check_circuit_assigned`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `elec_check_emergency_lighting`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `elec_generate_panel_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `elec_validate_lux_level`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_lux`

### `elec_validate_panel_load`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `max_load_kva`

### `element_count`

- **Çıktı:** `int`
- **Parametreler:** yok

### `element_report`

- **Çıktı:** `string`
- **Parametreler:** `title`

### `elements_to_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `elements_to_rows_with_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_names` *(zorunlu)*

### `eq`

- **Çıktı:** `bool`
- **Parametreler:** `left`, `right` *(zorunlu)*

### `excel_xml_read`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file_path` *(zorunlu)*, `sheet_name`, `has_header`, `skip_rows`

### `export_csv`

- **Çıktı:** `string`
- **Parametreler:** `file_path`

### `export_html_report`

- **Çıktı:** `string`
- **Parametreler:** `title`

### `export_pdf`

- **Çıktı:** `string`
- **Parametreler:** `title`

### `export_row_report`

- **Çıktı:** `string`
- **Parametreler:** `title`

### `export_validation_report`

- **Çıktı:** `string`
- **Parametreler:** `title`

### `export_wbs_report`

- **Çıktı:** `string`
- **Parametreler:** `output_path`

### `export_xlsx`

- **Çıktı:** `string`
- **Parametreler:** `file_path`, `sheet_name`, `title`

### `fa_classify_room_detector`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `fa_device_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `loop_param`, `zone_param`, `circuit_param`
- **Çıktı alanları:** `device_type`, `level`, `zone`, `loop`, `circuit`, `quantity`

### `fa_validate_circuit_assigned`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `fa_validate_device_in_room`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `fa_validate_mounting_height`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_aff_mm`, `max_aff_mm`, `device_filter`

### `family_ensure_loaded`

- **Çıktı:** `bool`
- **Parametreler:** `family_path` *(zorunlu)*

### `family_health_check`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `expected_params`, `max_types`, `origin_threshold_m`, `check_nested`
- **Çıktı alanları:** `family_id`, `family_name`, `category`, `type_count`, `param_count`, `is_in_place`, `has_no_category`, `type_count_ok`

### `filter_by_category`

- **Çıktı:** `List<Element>`
- **Parametreler:** `category` *(zorunlu)*

### `filter_by_level`

- **Çıktı:** `List<Element>`
- **Parametreler:** `level_name` *(zorunlu)*

### `filter_by_level_range`

- **Çıktı:** `List<Element>`
- **Parametreler:** `min_level` *(zorunlu)*, `max_level` *(zorunlu)*

### `filter_by_param`

- **Çıktı:** `List<Element>`
- **Parametreler:** `param_name` *(zorunlu)*, `value` *(zorunlu)*, `operator`

### `filter_by_type`

- **Çıktı:** `List<Element>`
- **Parametreler:** `type_name` *(zorunlu)*

### `filter_by_workset`

- **Çıktı:** `List<Element>`
- **Parametreler:** `workset_name` *(zorunlu)*

### `filter_empty_param`

- **Çıktı:** `List<Element>`
- **Parametreler:** `param_name` *(zorunlu)*

### `filter_not_empty_param`

- **Çıktı:** `List<Element>`
- **Parametreler:** `param_name` *(zorunlu)*

### `filter_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` *(zorunlu)*, `value` *(zorunlu)*, `operator`

### `filter_rows_multi`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `conditions` *(zorunlu)*

### `fire_hose_cabinet_spacing_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `max_spacing_m`, `hose_length_m`

### `flatten_list`

- **Çıktı:** `List<object?>`
- **Parametreler:** yok

### `format_message`

- **Çıktı:** `string`
- **Parametreler:** `template`

### `format_number`

- **Çıktı:** `string`
- **Parametreler:** `value` *(zorunlu)*, `format`, `unit`

### `fp_validate_sprinkler_coverage`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `max_coverage_m2`

### `generate_ids`

- **Çıktı:** `string`
- **Parametreler:** `title`, `output_path`

### `get_door_wall_clearances`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `left_mm`, `right_mm`, `wall_length_mm`, `door_width_mm`

### `get_room_ceiling_center`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `room_name`, `x`, `y`, `z`

### `group_by`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` *(zorunlu)*

### `group_elements_by_category`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `group_elements_by_level`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `group_elements_by_type`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `ifc_export`

- **Çıktı:** `string`
- **Parametreler:** `output_dir`, `file_name`, `ifc_version`, `export_linked_files`

### `join_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `key_field` *(zorunlu)*

### `kalip_floor`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `kalip_summary`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `group_by`

### `kalip_wall`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `lighting_emergency_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `emergency_param`, `emergency_panel_pattern`

### `link_quantity_to_wbs`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `quantity_field`, `wbs_field`

### `list_cross_product`

- **Çıktı:** `List<object?>`
- **Parametreler:** `second_key` *(zorunlu)*

### `list_filter_by_rule`

- **Çıktı:** `List<object?>`
- **Parametreler:** `field` *(zorunlu)*, `operator` *(zorunlu)*, `value` *(zorunlu)*

### `list_flatten`

- **Çıktı:** `List<object?>`
- **Parametreler:** `levels`

### `list_group_by_key`

- **Çıktı:** `List<object?>`
- **Parametreler:** `key_field` *(zorunlu)*
- **Çıktı alanları:** `key_field_value`, `items`, `count`

### `list_map`

- **Çıktı:** `List<object?>`
- **Parametreler:** `template`, `field`, `output_field`

### `list_shared_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `list_sort_by`

- **Çıktı:** `List<object?>`
- **Parametreler:** `sort_field` *(zorunlu)*, `ascending`

### `list_take_every_n`

- **Çıktı:** `List<object?>`
- **Parametreler:** `n` *(zorunlu)*, `offset`

### `list_transpose`

- **Çıktı:** `List<object?>`
- **Parametreler:** yok

### `list_zip`

- **Çıktı:** `List<object?>`
- **Parametreler:** `second_key` *(zorunlu)*

### `load_ifc_mapping`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `ifc_path`

### `load_poz_data`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `file`
- **Çıktı alanları:** `birim_fiyat`

### `load_qa_matrix`

- **Çıktı:** `object?`
- **Parametreler:** `qa_path`

### `load_rayic`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `rayic_path`

### `load_shared_param_map`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `map_path`

### `load_wbs_mapping`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `wbs_path`

### `log_message`

- **Çıktı:** `object?`
- **Parametreler:** `message`

### `lookup_value`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `key_field` *(zorunlu)*, `lookup_key` *(zorunlu)*, `result_field`

### `map_to_ifc`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `mep_air_terminal_space_map`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `mep_validate_duct_size`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_size_mm`

### `mep_validate_duct_slope`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_slope_pct`

### `mep_validate_duct_velocity`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `max_velocity_m_s`

### `duct_section_convert_preview`

Dikdörtgen kanalları eş-kesit (ASHRAE eşdeğer çap korumalı) yeniden boyutlandırır. Bir boyut sabitlenir, diğeri aynı aerodinamik karakteristiği koruyacak şekilde hesaplanır. Yazma yapmaz.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>` *(Duct — opsiyonel, verilmezse tüm dikdörtgen kanallar)*
- **Parametreler:** `fix_dimension`, `fixed_value_mm` *(zorunlu)*, `round_to_mm`, `max_aspect_ratio`, `only_round_ducts`
- **Çıktı alanları:** `duct_id`, `system_name`, `old_w_mm`, `old_h_mm`, `old_de_mm`, `new_w_mm`, `new_h_mm`, `new_de_mm`, `aspect_ratio`, `de_error_pct`, `warning`

### `duct_section_convert_apply` 🔒

`duct_section_convert_preview` çıktısındaki yeni kanal boyutlarını modele yazar. Model değişikliği yapar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `duct_section_convert_preview` çıktısı *(from ile)*
- **Parametreler:** `skip_warnings`
- **Çıktı alanları:** `duct_id`, `status`, `message`, `new_w_mm`, `new_h_mm`

### `mep_validate_space_hvac_zone`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `merge_lists`

- **Çıktı:** `List<Element>`
- **Parametreler:** yok

### `merge_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `merge_validation_reports`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `title`

### `mirror_element`

- **Çıktı:** `int`
- **Parametreler:** `axis`, `pivot_x_mm`, `pivot_y_mm`, `copy`

### `model_checksum`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `move_element`

- **Çıktı:** `int`
- **Parametreler:** `dx_mm`, `dy_mm`, `dz_mm`

### `noop`

- **Çıktı:** `object?`
- **Parametreler:** yok

### `op_health_check`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** yok

### `panel_phase_balance_check`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `max_imbalance_pct`
- **Çıktı alanları:** `panel_id`, `panel_name`, `phase_a_va`, `phase_b_va`, `phase_c_va`, `total_va`, `max_imbalance_pct`, `circuit_count`

### `param_exists`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `param_name` *(zorunlu)*

### `param_exists_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `param_name` *(zorunlu)*

### `param_filled_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `param_name` *(zorunlu)*, `severity`

### `param_range_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `param_name` *(zorunlu)*, `min`, `max`, `severity`

### `param_validate_schema`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `schema_path`

### `param_value_check`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `param_name` *(zorunlu)*, `expected_value` *(zorunlu)*, `severity`

### `pass_through`

- **Çıktı:** `object?`
- **Parametreler:** yok

### `pivot_table`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `row_field` *(zorunlu)*, `col_field` *(zorunlu)*, `value_field` *(zorunlu)*, `func`

### `place_adaptive_component_by_points` 🔒

- **Çıktı:** `List<Element>`
- **Parametreler:** `family_name` *(zorunlu)*, `type_name` *(zorunlu)*, `points_per_item` *(zorunlu)*

### `place_family`

- **Çıktı:** `int`
- **Parametreler:** `family_name` *(zorunlu)*, `type_name` *(zorunlu)*, `level_name` *(zorunlu)*, `x_mm`, `y_mm`, `z_mm`, `rotation_deg`

### `place_family_on_ceiling` 🔒

- **Çıktı:** `int`
- **Parametreler:** `family_name` *(zorunlu)*, `type_name` *(zorunlu)*, `offset_x_mm`, `offset_y_mm`

### `place_family_on_wall` 🔒

- **Çıktı:** `int`
- **Parametreler:** `family_name` *(zorunlu)*, `type_name` *(zorunlu)*, `offset_mm`, `spacing_mm`

### `place_family_along_mep` 🔒

Boru/kanal/kablo taşıyıcı hatları boyunca, belirtilen aralıkla (spacing_mm) verilen aileyi (askı/destek/etiket) yerleştirir. Her segmentin LocationCurve'ü uçtan uca, uç boşluğu bırakarak spacing aralıklarla bölünür. Manuel family + manuel aralık → manifest ile tam kontrol. Çap bantlı aralık değerleri için `data/validation/mep_aski_aralik.json` referans alınır.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>` (Pipe/Duct/CableTray segmentleri)
- **Parametreler:** `family_name` *(zorunlu)*, `type_name` *(zorunlu)*, `spacing_mm`, `end_setback_mm`, `vertical_offset_mm`
- **Çıktı alanları:** `host_id`, `category`, `placed_count`, `spacing_mm`, `run_length_m`

### `place_view_on_sheet`

- **Çıktı:** `int`
- **Parametreler:** `sheet_number` *(zorunlu)*, `x_mm`, `y_mm`

### `plumbing_calc_flow_rate`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `plumbing_check_fixture_room_assigned`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `plumbing_validate_connector_diameter`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `expected_diameter_mm`, `tolerance_mm`

### `plumbing_validate_pipe_slope`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `min_slope_pct`

### `plumbing_validate_system_separation`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `pm_get_project_info`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** yok

### `pm_model_delta_summary`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `pm_validate_lod`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `lod_level`

### `pm_validate_naming_convention`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `name_pattern`

### `poz_match`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `category_field`, `type_field`
- **Çıktı alanları:** `poz_no`, `poz_adi`, `birim`, `birim_fiyat`

### `poz_match_by_code`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `poz_code_field`
- **Çıktı alanları:** `poz_adi`, `birim`, `birim_fiyat`

### `preview_collect_geometry`

- **Çıktı:** `PreviewGeometryDto`
- **Parametreler:** `operation_name`, `include_labels`, `max_elements`

### `preview_gate`

- **Çıktı:** `string`
- **Parametreler:** yok

### `qa_check_approved_families`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `qa_check_level_assigned`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `qa_detect_duplicates`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `tolerance_mm`

### `qa_find_empty_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `qa_find_redundant_rooms`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `qa_get_model_warnings`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `qa_model_size_analysis`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `qa_validate_coordinates`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** yok

### `qa_validate_phase_consistency`

- **Çıktı:** `ValidationReport`
- **Parametreler:** yok

### `qa_validate_workset`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `expected_workset`

### `read_builtin_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `builtin_param` *(zorunlu)*

### `read_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_name` *(zorunlu)*

### `read_params`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_names` *(zorunlu)*

### `rebar_summary_by_diameter`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `rebar_summary_by_level`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `rebar_total_weight`

- **Çıktı:** `double`
- **Parametreler:** yok

### `rebar_weight_table`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `rename_column`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `from` *(zorunlu)*, `to` *(zorunlu)*

### `rename_element`

- **Çıktı:** `int`
- **Parametreler:** `param_name`, `value`, `prefix`, `suffix`

### `resolve_canonical_class`

- **Çıktı:** `string`
- **Parametreler:** `type_name` *(zorunlu)*, `category` *(zorunlu)*

### `resolve_discipline`

- **Çıktı:** `string`
- **Parametreler:** `category` *(zorunlu)*

### `room_boundary_extract`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `include_openings`, `fire_param`
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `loop_index`, `segment_index`, `host_wall_id`, `host_wall_type`, `fire_rating`

### `room_door_relation_map`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `room_id`, `room_name`, `room_number`, `door_count`, `door_ids`, `door_marks`

### `run_csharp_script`

- **Çıktı:** `Dictionary`
- **Parametreler:** `script_path` *(zorunlu)*, `cache`
- **Çıktı alanları:** `result`

### `schedule_collect_4d`

- **Çıktı:** `FourDFiveDDto`
- **Parametreler:** `operation_name`, `project_start`, `project_end`, `kat_sure_hafta`, `max_elements`, `schedule_map`
- **Çıktı alanları:** `meshes`, `schedule_items`, `project_start`, `project_end`, `element_count`, `warnings`, `stats`, `bbox`

### `schedule_collect_5d`

- **Çıktı:** `FourDFiveDDto`
- **Parametreler:** `operation_name`, `project_start`, `project_end`, `kat_sure_hafta`, `max_elements`, `schedule_map`, `cost_step`
- **Çıktı alanları:** `meshes`, `schedule_items`, `cost_items`, `total_cost`, `currency`, `project_start`, `project_end`, `element_count`

### `schedule_gate`

- **Çıktı:** `string`
- **Parametreler:** `title`

### `select_columns`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fields` *(zorunlu)*

### `select_field`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `fields` *(zorunlu)*

### `set_element_type`

- **Çıktı:** `int`
- **Parametreler:** `type_name` *(zorunlu)*

### `set_level`

- **Çıktı:** `int`
- **Parametreler:** `level_name` *(zorunlu)*

### `set_param_from_schedule`

- **Çıktı:** `int`
- **Parametreler:** `schedule_step` *(zorunlu)*, `start_param`, `end_param`, `phase_param`, `wbs_param`, `write_cost`

### `set_phase`

- **Çıktı:** `int`
- **Parametreler:** `phase_name` *(zorunlu)*, `phase_type`

### `set_var`

- **Çıktı:** `object?`
- **Parametreler:** `key` *(zorunlu)*, `value` *(zorunlu)*

### `set_workset`

- **Çıktı:** `int`
- **Parametreler:** `workset_name` *(zorunlu)*

### `show_count`

- **Çıktı:** `int`
- **Parametreler:** yok

### `show_result`

- **Çıktı:** `object?`
- **Parametreler:** `title`

### `show_table`

- **Çıktı:** `object?`
- **Parametreler:** `title`, `max_rows`

### `skip_n`

- **Çıktı:** `object?`
- **Parametreler:** `count`

### `sort_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` *(zorunlu)*, `descending`

### `sprinkler_head_schedule`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `k_factor_param`, `coverage_param`, `zone_param`
- **Çıktı alanları:** `type_name`, `k_factor`, `coverage_m2`, `zone`, `level`, `quantity`

### `sum_field`

- **Çıktı:** `double`
- **Parametreler:** `field` *(zorunlu)*

### `system_info`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** yok

### `table_to_points`

- **Çıktı:** `List<object?>`
- **Parametreler:** `x_field`, `y_field`, `z_field`, `unit`

### `table_validate_schema`

- **Çıktı:** `Dictionary<string, object?>`
- **Parametreler:** `required_fields` *(zorunlu)*, `optional_fields`
- **Çıktı alanları:** `valid`, `missing_fields`, `row_count`, `field_count`, `message`

### `tag_elements` 🔒

- **Çıktı:** `int`
- **Parametreler:** `tag_type_name` *(zorunlu)*, `view_id`, `leader`, `orientation`

### `take_n`

- **Çıktı:** `object?`
- **Parametreler:** `count`

### `trace_find_existing`

- **Çıktı:** `List<Element>`
- **Parametreler:** `trace_key` *(zorunlu)*

### `trace_write` 🔒

- **Çıktı:** `int`
- **Parametreler:** `trace_key` *(zorunlu)*

### `type_get`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `type_read_param`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `param_name` *(zorunlu)*

### `update_or_create_family` 🔒

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `trace_key` *(zorunlu)*, `family_name` *(zorunlu)*, `type_name` *(zorunlu)*, `level_name`, `update_type`, `update_location`
- **Çıktı alanları:** `element_id`, `index`, `action`, `status`

### `validate_ids`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `ids_path`

### `validate_qa`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `rules_path`

### `validate_rebar_ts500`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `fck`, `fyk`, `cover_mm`

### `validate_required_params`

- **Çıktı:** `ValidationReport`
- **Parametreler:** `required_params` *(zorunlu)*

### `validation_summary`

- **Çıktı:** `string`
- **Parametreler:** yok

### `validation_to_rows`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `valve_type_classify`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `element_id`, `family_name`, `type_name`, `valve_class`, `system_type`, `diameter_mm`

### `wall_area`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok

### `where`

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `field` *(zorunlu)*, `op`, `value` *(zorunlu)*

### `write_param`

- **Çıktı:** `int`
- **Parametreler:** `param_name` *(zorunlu)*, `value` *(zorunlu)*

### `write_param_from_rows`

- **Çıktı:** `int`
- **Parametreler:** `param_name` *(zorunlu)*, `value_field` *(zorunlu)*

### `write_row_param`

- **Çıktı:** `int`
- **Parametreler:** `param_name` *(zorunlu)*, `value_key` *(zorunlu)*

### `write_trace`

- **Çıktı:** `string`
- **Parametreler:** `message`, `file_path`


## Koordinasyon

### `clash_detect_matrix`

İki kategori grubu (A vs B) arasında hard clash tespiti (BBox + Solid)

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `group_a` *(zorunlu)*, `group_b` *(zorunlu)*, `tolerance_mm`, `max_results`
- **Çıktı alanları:** `a_id`, `a_category`, `b_id`, `b_category`, `clash_x`, `clash_y`, `clash_z`, `overlap_volume_m3`

### `clash_severity_sort`

Clash bulgularını kesişim hacmine göre önceliklendirir

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** yok
- **Çıktı alanları:** `sira_no`

## MEP Koordinasyon

### `mep_straighten_scan`

MEP hatlarındaki çift-dirsek sapmalarını (S-bend) tespit eder. Yalnızca rapor — yazma yapmaz. Kanal/boru/kablo taşıyıcı destekler.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>` *(opsiyonel — verilmezse categories ile toplanır)*
- **Parametreler:** `categories`, `min_offset_mm`, `max_offset_mm`, `max_results`
- **Çıktı alanları:** `elbow_a_id`, `elbow_b_id`, `middle_ids`, `offset_mm`, `system_name`, `category`, `anchor_id`, `mover_id`, `center_x`, `center_y`, `center_z`

### `mep_straighten_apply` 🔒

`mep_straighten_scan` bulgularındaki S-bendleri düzleştirir (dirsekleri + ara segmenti siler, ana hattı uzatır). Model değişikliği yapar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `mep_straighten_scan` çıktısı *(input_from ile)*
- **Parametreler:** `max_apply`
- **Çıktı alanları:** `elbow_a_id`, `elbow_b_id`, `status`, `message`

### `mep_region_count`

Kapalı bölge (Room/Area) içindeki MEP elemanlarını sayar ve uzunluk toplar. Oda-bazlı metraj/hakediş için.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>` *(opsiyonel bölge listesi)*
- **Parametreler:** `region_category`, `mep_categories`, `z_tolerance_mm`
- **Çıktı alanları:** `region_id`, `region_name`, `region_number`, `level_name`, `area_m2`, `mep_count`, `total_length_m`, `pipe_count`, `duct_count`, `tray_count`, `by_category`

### `mep_region_tag` 🔒

Kapalı bölge içindeki MEP elemanlarına bölge adı/numarasını parametre olarak yazar. Model değişikliği yapar.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `List<Element>` *(opsiyonel bölge listesi)*
- **Parametreler:** `region_category`, `mep_categories`, `target_param`, `write_mode`, `z_tolerance_mm`
- **Çıktı alanları:** `region_id`, `region_name`, `tagged_count`, `skipped_count`

### `place_opening` 🔒

smart_check_mep_no_opening bulgularına göre boşluk aile örnekleri yerleştirir. 

- **Çıktı:** `object`
- **Parametreler:** yok

### `smart_check_mep_no_opening`

MEP elemanlarının yapısal eleman (duvar/döşeme/kiriş) geçişlerini tarar. 

- **Çıktı:** `object`
- **Parametreler:** yok


## Maliyet

### `kalip_all`

Tüm yapısal kategorilerin kalıp alanını tek geçişte hesaplar (kolon, kiriş, duvar, döşeme, temel)

- **Çıktı:** `List<Dictionary>`
- **Parametreler:** `include_edges`

### `kalip_column`

Kolon kalıp alanı. Ana gövde: (çevre−duvar_temas)×H. Kolon başı: her yüz kiriş/döşeme düşümü.

- **Çıktı:** `List<Dictionary>`
- **Parametreler:** yok

### `kalip_write_back`

Kalıp satırlarını Revit parametrelerine yazar: Formwork_Area, TR_KalipAlani, TR_KalipPozNo, TR_KalipToplamTutar

- **Çıktı:** `Dictionary`
- **Parametreler:** yok

### `poz_match_keynote_aware`

Üç aşamalı poz çözümleme: 1.Keynote/TR_CSB_PozNo 2.Semantic canonical map 3.Kategori varsayılanı

- **Çıktı:** `List<Dictionary>`
- **Parametreler:** yok


## Modelleme

### `workset_by_level` 🔒

Her kat (Level) için aynı isimde User Workset oluşturur. 

- **Çıktı:** `object`
- **Parametreler:** yok


## Oda

### `room_area_breakdown`

Oda bazlı taban, duvar ve tavan alanlarını hesaplar. 

- **Çıktı:** `object`
- **Parametreler:** yok

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


## Parametre

### `read_param_with_fallback`

Elemandan öncelikli parametre listesinden ilk dolu değeri okur. params: param_names[], output_field

- **Çıktı:** `List<Dictionary>`
- **Parametreler:** `param_names`, `output_field`


## Raporlama

### `schedule_export_anchored`

Bir Revit schedule'ını GetCellText ile birebir görsel sadakatle okur ve her eleman satırına gizli UniqueId anchor (`__egbimoto_uid__`) ekler. Anchor, geçici alan + doc.Regenerate + RollBack ile alınır — **model kalıcı olarak değişmez**. Çıktı `export_xlsx` ile zincirlenir. Round-trip uygun değilse (malzeme metrajı, gömülü, bağlı model, non-itemized) yalnızca görüntü çıktısı üretilir.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Parametreler:** `schedule_name` *(zorunlu)*, `include_anchor`
- **Çıktı alanları:** `__egbimoto_uid__` + schedule'ın görünür sütunları (dinamik)

### `schedule_roundtrip_diff`

Düzenlenmiş schedule satırlarını (anchor'lı) canlı model ile karşılaştırır. Her satırı UniqueId ile elemana eşler, hücre değerini model değeriyle kıyaslar. Yazma yapmaz.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `schedule_export_anchored` çıktısı + kullanıcı düzenlemeleri
- **Parametreler:** `ignore_fields`
- **Çıktı alanları:** `uid`, `field`, `old_value`, `new_value`, `writable`, `binding`, `note`

### `schedule_roundtrip_apply` 🔒

`schedule_roundtrip_diff` değişikliklerini güvenle modele yazar. Her yazımdan sonra değeri yeniden okuyarak doğrular (sessiz Set hatalarına karşı). Tip parametresi yazımı tüm tipi etkiler.

- **Çıktı:** `List<Dictionary<string, object?>>`
- **Girdi:** `schedule_roundtrip_diff` çıktısı *(from ile)*
- **Parametreler:** `apply_type_params`
- **Çıktı alanları:** `uid`, `field`, `old_value`, `new_value`, `status`, `message`

### `kalip_export_xlsx`

Kalıp hesap sonuçlarını Excel BOQ formatında dışa aktarır. 

- **Çıktı:** `object`
- **Parametreler:** yok

### `kalip_report`

Kalıp hesap sonuçlarını profesyonel HTML raporuna dönüştürür. 

- **Çıktı:** `object`
- **Parametreler:** yok


## Sistem

### `eg_addin_disable_unused`

Zorunlu olmayan add-inleri .EGdisabled olarak devre dışı bırakır

- **Çıktı:** `Dictionary`
- **Parametreler:** `keep`, `revit_version`
- **Çıktı alanları:** `disabled_count`, `skipped_readonly`, `failed_count`, `restart_required`

### `eg_addin_restore_all`

Tüm .EGdisabled / .RSTdisabled add-inleri geri yükler

- **Çıktı:** `Dictionary`
- **Parametreler:** `revit_version`
- **Çıktı alanları:** `restored_count`, `failed_count`, `restart_required`

### `eg_addin_restore_single`

Tek bir add-ini geri yükler (addin_file: pyRevit.addin)

- **Çıktı:** `bool`
- **Parametreler:** `addin_file` *(zorunlu)*, `revit_version`

### `eg_addin_scan`

Kurulu Revit add-inleri tarar ve listeler

- **Çıktı:** `List<Dictionary>`
- **Parametreler:** `revit_version`, `include_disabled`
- **Çıktı alanları:** `fileName`, `disabled`, `directory`, `name`, `addinId`

### `eg_health_snapshot`

RAM/CPU/Disk/OS/Revit/Warnings sağlık raporu üretir (html|json|text)

- **Çıktı:** `string`
- **Parametreler:** `format`, `open`, `out_path`
- **Çıktı alanları:** `out_path`


## Yapısal

### `structural_collect_all`

Tüm yapısal elementleri (kolon, kiriş, döşeme, perde, temel) toplar. 

- **Çıktı:** `object`
- **Parametreler:** yok

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


## geometry

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
