using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Core.Manifest
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO ManifestLinter  —  v3.0  (Semantic Layer eklendi)
    //
    //  5 katman halinde değerlendirme:
    //
    //    KATMAN 1 — KRİTİK            : Başlık, step ID, op kayıt, from referansları
    //    KATMAN 2 — ZORUNLU FAZLAR    : PRECHECK / COLLECT / VALIDATE / SUMMARY / EXPORT
    //    KATMAN 3 — GÜVENLİK         : Yazma op'ları preview_gate, export required:false
    //    KATMAN 4 — PATTERN           : QA / BOQ / MEP / IFC / KALIP / WBS / CALC / RENAME
    //    KATMAN 5 — SEMANTİK ← YENİ  : Domain zincir uyumu (Structural/MEP/Architectural/…)
    //
    //  Katman 5 mantığı:
    //    Her op'un bir InputDomain ve OutputDomain'i var.
    //    Bir adımın 'from' referansı verdiği adımın OutputDomain'i,
    //    bu adımın InputDomain'iyle uyumlu olmalı.
    //    Domain.Any her şeyle uyumludur.
    //
    //    Örnek hata:
    //      collect_walls  → OutputDomain: Structural
    //      mep_validate_duct_velocity (from: collect_walls) → InputDomain: MEP
    //      Structural ≠ MEP → SEM-01 hatası
    //
    //    Domain tablosu Op dosyalarına dokunmadan linter içinde tutulur.
    //    Yeni op eklendikçe OpDomains sözlüğüne bir satır yeter.
    // ═══════════════════════════════════════════════════════════════════════════

    // ── STEP 1: Domain Enum ───────────────────────────────────────────────────

    /// <summary>
    /// Op'un tükettiği/ürettiği veri kategorisi.
    /// Domain.Any her domain ile uyumludur (joker).
    /// </summary>
    public enum Domain
    {
        Any,           // Joker — her domain ile uyumlu (collect_elements, utility op'lar)
        Structural,    // Wall, Column, Beam, Floor, Foundation, Rebar
        MEP,           // Pipe, Duct, CableTray, Conduit, Sprinkler, FireAlarm, MepEquipment
        Architectural, // Room, Door, Window, Ceiling, Stair
        Report,        // ValidationReport — check/validate op çıktısı, merge/summary girişi
        Rows,          // List<Dictionary> — tabular data, show_table/export girişi
        Scalar,        // int/double/string — count, length, cost toplamı
        PozData,       // Poz eşleştirme verisi
        KalipResult,   // Kalıp hesap sonucu
        WbsData,       // WBS hiyerarşi verisi
        IfcData,       // IFC mapping verisi
    }

    public static class ManifestLinter
    {
        // ── STEP 2: OpDomains Tablosu ─────────────────────────────────────────
        //
        //  (InputDomain, OutputDomain)
        //  Tabloda olmayan op → (Any, Any) varsayılır → hata üretmez (güvenli).
        //  Yeni op eklendiğinde buraya 1 satır yeterli.

        private static readonly IReadOnlyDictionary<string, (Domain Input, Domain Output)> OpDomains =
            new Dictionary<string, (Domain, Domain)>(StringComparer.OrdinalIgnoreCase)
        {
            // ── COLLECT ──────────────────────────────────────────────────────
            // Yapısal
            ["collect_walls"]               = (Domain.Any, Domain.Structural),
            ["collect_columns"]             = (Domain.Any, Domain.Structural),
            ["collect_beams"]               = (Domain.Any, Domain.Structural),
            ["collect_floors"]              = (Domain.Any, Domain.Structural),
            ["collect_foundations"]         = (Domain.Any, Domain.Structural),
            ["collect_rebar"]               = (Domain.Any, Domain.Structural),
            // MEP
            ["collect_pipes"]               = (Domain.Any, Domain.MEP),
            ["collect_ducts"]               = (Domain.Any, Domain.MEP),
            ["collect_cable_trays"]         = (Domain.Any, Domain.MEP),
            ["collect_conduits"]            = (Domain.Any, Domain.MEP),
            ["collect_lighting_fixtures"]   = (Domain.Any, Domain.MEP),
            ["collect_sprinklers"]          = (Domain.Any, Domain.MEP),
            ["collect_fire_alarm_devices"]  = (Domain.Any, Domain.MEP),
            ["collect_mep_equipment"]       = (Domain.Any, Domain.MEP),
            ["collect_mechanical_equipment"]= (Domain.Any, Domain.MEP),
            ["collect_plumbing_fixtures"]   = (Domain.Any, Domain.MEP),
            // Mimari
            ["collect_rooms"]               = (Domain.Any, Domain.Architectural),
            ["collect_doors"]               = (Domain.Any, Domain.Architectural),
            ["collect_windows"]             = (Domain.Any, Domain.Architectural),
            // Jenerik
            ["collect_elements"]            = (Domain.Any, Domain.Any),
            ["collect_sheets"]              = (Domain.Any, Domain.Any),
            ["collect_views"]               = (Domain.Any, Domain.Any),
            ["collect_schedules"]           = (Domain.Any, Domain.Any),
            ["collect_families"]            = (Domain.Any, Domain.Any),
            ["collect_groups"]              = (Domain.Any, Domain.Any),
            ["collect_levels"]              = (Domain.Any, Domain.Any),
            ["collect_grids"]               = (Domain.Any, Domain.Any),
            ["collect_multi"]               = (Domain.Any, Domain.Any),
            // Yüklemeler
            ["load_poz_data"]               = (Domain.Any, Domain.PozData),
            ["load_wbs_mapping"]            = (Domain.Any, Domain.WbsData),
            ["load_ifc_mapping"]            = (Domain.Any, Domain.IfcData),
            ["load_shared_param_map"]       = (Domain.Any, Domain.Any),
            ["load_canonical_map"]          = (Domain.Any, Domain.Any),
            ["load_rule_set"]               = (Domain.Any, Domain.Any),

            // ── VALIDATE / TRANSFORM ─────────────────────────────────────────
            // Genel param kontrolü (Any → Report)
            ["param_exists_check"]          = (Domain.Any,          Domain.Report),
            ["param_filled_check"]          = (Domain.Any,          Domain.Report),
            ["param_value_check"]           = (Domain.Any,          Domain.Report),
            ["param_range_check"]           = (Domain.Any,          Domain.Report),
            ["validate_required_params"]    = (Domain.Any,          Domain.Report),
            ["validate_ids"]                = (Domain.Any,          Domain.Report),
            ["validate_qa"]                 = (Domain.Any,          Domain.Report),
            // QA genel
            ["qa_find_empty_params"]        = (Domain.Any,          Domain.Report),
            ["qa_detect_duplicates"]        = (Domain.Any,          Domain.Report),
            ["qa_check_level_assigned"]     = (Domain.Any,          Domain.Report),
            ["qa_validate_phase_consistency"]=(Domain.Any,          Domain.Report),
            ["qa_validate_workset"]         = (Domain.Any,          Domain.Report),
            ["qa_get_model_warnings"]       = (Domain.Any,          Domain.Report),
            ["qa_validate_coordinates"]     = (Domain.Any,          Domain.Report),
            ["qa_model_size_analysis"]      = (Domain.Any,          Domain.Report),
            ["check_zero_volume"]           = (Domain.Any,          Domain.Report),
            ["check_overlapping_rooms"]     = (Domain.Architectural,Domain.Report),
            ["check_unplaced_rooms"]        = (Domain.Architectural,Domain.Report),
            ["check_untagged_elements"]     = (Domain.Any,          Domain.Report),
            // Koordinasyon
            ["coord_check_clearance"]       = (Domain.Any,          Domain.Report),
            ["coord_validate_penetration_firestop"]=(Domain.Any,    Domain.Report),
            ["coord_validate_level_consistency"]   =(Domain.Any,    Domain.Report),
            // Hesap (Yapısal → Report/Scalar)
            ["calc_cost"]                   = (Domain.PozData,      Domain.Rows),
            ["calc_lap_length"]             = (Domain.Structural,   Domain.Report),
            ["calc_anchorage_length"]       = (Domain.Structural,   Domain.Report),
            ["calc_min_spacing"]            = (Domain.Structural,   Domain.Report),
            ["calc_placement_point"]        = (Domain.Any,          Domain.Any),
            ["calc_wall_area"]              = (Domain.Structural,   Domain.Scalar),
            ["element_area"]                = (Domain.Any,          Domain.Scalar),
            ["wall_area"]                   = (Domain.Structural,   Domain.Scalar),
            // Kalıp (Yapısal → KalipResult)
            ["kalip_all"]                   = (Domain.Structural,   Domain.KalipResult),
            ["kalip_wall"]                  = (Domain.Structural,   Domain.KalipResult),
            ["kalip_column"]                = (Domain.Structural,   Domain.KalipResult),
            ["kalip_floor"]                 = (Domain.Structural,   Domain.KalipResult),
            ["kalip_beam"]                  = (Domain.Structural,   Domain.KalipResult),
            ["kalip_foundation"]            = (Domain.Structural,   Domain.KalipResult),
            ["kalip_stair"]                 = (Domain.Structural,   Domain.KalipResult),
            // Poz
            ["poz_match"]                   = (Domain.Any,          Domain.PozData),
            ["poz_match_keynote_aware"]     = (Domain.Any,          Domain.PozData),
            ["poz_match_by_code"]           = (Domain.Any,          Domain.PozData),
            ["classify_elements"]           = (Domain.Any,          Domain.Any),
            ["classify_by_wbs"]             = (Domain.Any,          Domain.WbsData),
            // MEP doğrulama (MEP → Report)
            ["mep_validate_duct_velocity"]  = (Domain.MEP,          Domain.Report),
            ["mep_validate_duct_size"]      = (Domain.MEP,          Domain.Report),
            ["mep_validate_space_hvac_zone"]= (Domain.MEP,          Domain.Report),
            ["mep_validate_duct_slope"]     = (Domain.MEP,          Domain.Report),
            ["duct_aspect_ratio_check"]     = (Domain.MEP,          Domain.Report),
            ["cable_tray_fill_check"]       = (Domain.MEP,          Domain.Report),
            ["conduit_fill_check"]          = (Domain.MEP,          Domain.Report),
            // Yangın (MEP → Report)
            ["fa_validate_device_in_room"]  = (Domain.MEP,          Domain.Report),
            ["fa_validate_mounting_height"] = (Domain.MEP,          Domain.Report),
            ["fa_validate_circuit_assigned"]= (Domain.MEP,          Domain.Report),
            // Elektrik (MEP → Report)
            ["elec_validate_lux_level"]     = (Domain.MEP,          Domain.Report),
            ["elec_check_circuit_assigned"] = (Domain.MEP,          Domain.Report),
            ["elec_validate_panel_load"]    = (Domain.MEP,          Domain.Report),
            ["elec_check_emergency_lighting"]=(Domain.MEP,          Domain.Report),
            // Sıhhi tesisat (MEP → Report)
            ["plumbing_validate_pipe_slope"]        = (Domain.MEP,  Domain.Report),
            ["plumbing_validate_connector_diameter"]= (Domain.MEP,  Domain.Report),
            ["plumbing_check_fixture_room_assigned"]= (Domain.MEP,  Domain.Report),
            ["plumbing_validate_system_separation"] = (Domain.MEP,  Domain.Report),
            // Mimari (Arch → Report)
            ["arch_validate_room_area"]     = (Domain.Architectural,Domain.Report),
            ["arch_validate_ceiling_height"]= (Domain.Architectural,Domain.Report),
            ["door_handing_detect"]         = (Domain.Architectural,Domain.Report),
            ["door_clearance_check"]        = (Domain.Architectural,Domain.Report),
            ["door_fire_rating_from_wall"]  = (Domain.Architectural,Domain.Report),
            // PM
            ["pm_validate_lod"]             = (Domain.Any,          Domain.Report),
            ["pm_validate_naming_convention"]=(Domain.Any,          Domain.Report),
            // Rebar (Yapısal → Report)
            ["validate_rebar_ts500"]        = (Domain.Structural,   Domain.Report),
            ["rebar_summary_by_diameter"]   = (Domain.Structural,   Domain.Rows),
            ["rebar_summary_by_level"]      = (Domain.Structural,   Domain.Rows),
            // IFC/IDS
            ["map_to_ifc"]                  = (Domain.Any,          Domain.IfcData),
            ["generate_ids"]                = (Domain.Any,          Domain.Any),
            ["ifc_export"]                  = (Domain.IfcData,      Domain.Scalar),

            // ── AGGREGATE ────────────────────────────────────────────────────
            ["merge_validation_reports"]    = (Domain.Report,       Domain.Report),
            ["merge_rows"]                  = (Domain.Rows,         Domain.Rows),
            ["merge_lists"]                 = (Domain.Any,          Domain.Any),
            ["group_by"]                    = (Domain.Any,          Domain.Rows),
            ["group_elements_by_level"]     = (Domain.Any,          Domain.Rows),
            ["group_elements_by_type"]      = (Domain.Any,          Domain.Rows),
            ["group_elements_by_category"]  = (Domain.Any,          Domain.Rows),
            ["pivot_table"]                 = (Domain.Rows,         Domain.Rows),
            ["cost_summary"]                = (Domain.PozData,      Domain.Report),
            ["cost_by_level"]               = (Domain.PozData,      Domain.Report),
            ["kalip_summary"]               = (Domain.KalipResult,  Domain.Report),
            ["mep_by_system"]               = (Domain.MEP,          Domain.Rows),
            ["mep_total_length"]            = (Domain.MEP,          Domain.Scalar),
            ["assign_wbs_code"]             = (Domain.WbsData,      Domain.WbsData),
            ["link_quantity_to_wbs"]        = (Domain.WbsData,      Domain.Rows),
            ["aggregate_by_param"]          = (Domain.Any,          Domain.Rows),
            ["count_items"]                 = (Domain.Any,          Domain.Scalar),

            // ── SUMMARY ──────────────────────────────────────────────────────
            ["validation_summary"]          = (Domain.Report,       Domain.Report),
            ["show_table"]                  = (Domain.Rows,         Domain.Report),
            ["show_result"]                 = (Domain.Any,          Domain.Report),
            ["show_count"]                  = (Domain.Scalar,       Domain.Report),
            ["display_report"]              = (Domain.Report,       Domain.Report),

            // ── ROWS ─────────────────────────────────────────────────────────
            ["validation_to_rows"]          = (Domain.Report,       Domain.Rows),
            ["elements_to_rows"]            = (Domain.Any,          Domain.Rows),
            ["elements_to_rows_with_params"]= (Domain.Any,          Domain.Rows),
            ["rows_to_csv"]                 = (Domain.Rows,         Domain.Scalar),
            ["rows_to_json"]                = (Domain.Rows,         Domain.Scalar),

            // ── EXPORT ───────────────────────────────────────────────────────
            ["export_xlsx"]                 = (Domain.Any,          Domain.Scalar),
            ["export_validation_report"]    = (Domain.Report,       Domain.Scalar),
            ["export_csv"]                  = (Domain.Rows,         Domain.Scalar),
            ["export_json"]                 = (Domain.Any,          Domain.Scalar),
            ["export_ifc"]                  = (Domain.IfcData,      Domain.Scalar),

            // ── WRITE / MUTATION ─────────────────────────────────────────────
            ["write_param"]                 = (Domain.Any,          Domain.Scalar),
            ["set_param_value"]             = (Domain.Any,          Domain.Scalar),
            ["kalip_write_back"]            = (Domain.KalipResult,  Domain.Scalar),
            ["rename_apply"]                = (Domain.Any,          Domain.Scalar),
            ["rename_element"]              = (Domain.Any,          Domain.Scalar),
            ["tag_elements"]                = (Domain.Architectural,Domain.Scalar),
            ["place_family"]                = (Domain.Any,          Domain.Scalar),
            ["create_wall"]                 = (Domain.Any,          Domain.Structural),
            ["create_floor"]                = (Domain.Any,          Domain.Structural),
            ["create_room"]                 = (Domain.Any,          Domain.Architectural),
            ["create_sheet"]                = (Domain.Any,          Domain.Any),
        };

        // ── Mevcut Op kümeleri (Katman 1-4 için korunuyor) ───────────────────

        private static readonly HashSet<string> CollectOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "collect_walls","collect_columns","collect_beams","collect_floors",
            "collect_foundations","collect_rooms","collect_doors","collect_windows",
            "collect_pipes","collect_ducts","collect_cable_trays","collect_conduits",
            "collect_lighting_fixtures","collect_rebar","collect_sprinklers",
            "collect_fire_alarm_devices","collect_elements","collect_sheets",
            "collect_views","collect_schedules","collect_families","collect_groups",
            "collect_levels","collect_grids","collect_multi","collect_mep_equipment",
            "collect_mechanical_equipment","collect_plumbing_fixtures",
            "load_poz_data","load_wbs_mapping","load_ifc_mapping","load_shared_param_map",
            "load_canonical_map","load_rule_set",
            // CALC kategorisi veri üretici op'ları — collect eşdeğeri
            "rebar_weight_table","load_poz_canonical_map","load_poz_section_rules",
            "load_qa_matrix","load_ids","load_rebar_table"
        };

        private static readonly HashSet<string> ValidateTransformOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "param_exists_check","param_filled_check","param_value_check","param_range_check",
            "validate_required_params","validate_ids","validate_qa",
            "qa_find_empty_params","qa_detect_duplicates","qa_check_level_assigned",
            "qa_validate_phase_consistency","qa_validate_workset","qa_get_model_warnings",
            "qa_validate_coordinates","qa_model_size_analysis",
            "check_zero_volume","check_overlapping_rooms","check_unplaced_rooms","check_untagged_elements",
            "coord_check_clearance","coord_validate_penetration_firestop","coord_validate_level_consistency",
            "calc_cost","calc_lap_length","calc_anchorage_length","calc_min_spacing",
            "calc_placement_point","calc_wall_area","element_area","wall_area",
            "kalip_all","kalip_wall","kalip_column","kalip_floor","kalip_beam","kalip_foundation","kalip_stair",
            "poz_match","poz_match_keynote_aware","poz_match_by_code","classify_elements","classify_by_wbs",
            "mep_validate_duct_velocity","mep_validate_duct_size","mep_validate_space_hvac_zone",
            "mep_validate_duct_slope","duct_aspect_ratio_check","cable_tray_fill_check","conduit_fill_check",
            "fa_validate_device_in_room","fa_validate_mounting_height","fa_validate_circuit_assigned",
            "elec_validate_lux_level","elec_check_circuit_assigned","elec_validate_panel_load","elec_check_emergency_lighting",
            "plumbing_validate_pipe_slope","plumbing_validate_connector_diameter",
            "plumbing_check_fixture_room_assigned","plumbing_validate_system_separation",
            "arch_validate_room_area","arch_validate_ceiling_height",
            "door_handing_detect","door_clearance_check","door_fire_rating_from_wall",
            "pm_validate_lod","pm_validate_naming_convention",
            "validate_rebar_ts500","rebar_summary_by_diameter","rebar_summary_by_level",
            "map_to_ifc","generate_ids","ifc_export",
        };

        private static readonly HashSet<string> AggregateOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "merge_validation_reports","merge_rows","merge_lists","group_by",
            "group_elements_by_level","group_elements_by_type","group_elements_by_category",
            "pivot_table","cost_summary","cost_by_level","kalip_summary",
            "mep_by_system","mep_total_length","assign_wbs_code","link_quantity_to_wbs",
            "rebar_summary_by_diameter","rebar_summary_by_level",
            "aggregate_by_param","count_items"
        };

        private static readonly HashSet<string> SummaryOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "validation_summary","show_table","show_result","show_count",
            "kalip_summary","cost_summary","cost_by_level","display_report"
        };

        private static readonly HashSet<string> RowsOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "validation_to_rows","elements_to_rows","elements_to_rows_with_params",
            "show_table","rows_to_csv","rows_to_json"
        };

        private static readonly HashSet<string> WriteOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "write_param","kalip_write_back","rename_apply","rename_element",
            "tag_elements","place_family","create_wall","create_floor",
            "create_room","create_sheet","set_param_value"
        };

        // ── STEP 3: Yardımcı — domain uyumluluk kontrolü ────────────────────

        /// <summary>
        /// İki domain'in uyumlu olup olmadığını kontrol eder.
        /// Any joker domain her şeyle uyumludur.
        /// </summary>
        private static bool DomainsCompatible(Domain source, Domain target)
            => source == Domain.Any || target == Domain.Any || source == target;

        /// <summary>
        /// Op için domain bilgisini döner.
        /// Tabloda yoksa (Any, Any) döner — bilinmeyen op hata üretmez.
        /// </summary>
        private static (Domain Input, Domain Output) GetDomain(string opName)
            => OpDomains.TryGetValue(opName, out var d) ? d : (Domain.Any, Domain.Any);

        // ── Ana lint metodu ───────────────────────────────────────────────────

        public static LintResult Lint(EgManifest manifest, OpRegistry? registry = null)
        {
            var errors   = new List<string>();
            var warnings = new List<string>();
            var infos    = new List<string>();

            // ──────────────────────────────────────────────────────────────────
            // KATMAN 1 — KRİTİK
            // ──────────────────────────────────────────────────────────────────

            if (string.IsNullOrWhiteSpace(manifest.Title))
                errors.Add("KRT-01: Başlık (title) boş");

            if (manifest.Steps == null || manifest.Steps.Count == 0)
            {
                errors.Add("KRT-02: Steps dizisi boş");
                return BuildResult(errors, warnings, infos, manifest);
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var step in manifest.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Id))
                    errors.Add($"KRT-03: Op '{step.Op}' için step id boş");
                else if (!seenIds.Add(step.Id))
                    errors.Add($"KRT-04: Step id '{step.Id}' tekrar kullanılmış");
            }

            if (registry != null)
            {
                var knownOps = new HashSet<string>(
                    registry.GetAll().Select(t => t.Name),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var step in manifest.Steps)
                    if (!string.IsNullOrWhiteSpace(step.Op) && !knownOps.Contains(step.Op))
                        errors.Add($"KRT-05: Bilinmeyen op '{step.Op}' (id={step.Id})");
            }

            var definedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var step in manifest.Steps)
            {
                if (!string.IsNullOrWhiteSpace(step.From) && !definedIds.Contains(step.From))
                    errors.Add($"KRT-06: '{step.Id}' → from='{step.From}' henüz tanımlanmamış");

                if (step.FromMany != null)
                    foreach (var fid in step.FromMany)
                        if (!definedIds.Contains(fid))
                            errors.Add($"KRT-07: '{step.Id}' → from_many='{fid}' henüz tanımlanmamış");

                if (step.DependsOn != null)
                    foreach (var dep in step.DependsOn)
                        if (!definedIds.Contains(dep))
                            warnings.Add($"UYARI: '{step.Id}' → depends_on='{dep}' tanımlanmamış");

                if (!string.IsNullOrWhiteSpace(step.Id))
                    definedIds.Add(step.Id);
            }

            if (errors.Count > 0)
                return BuildResult(errors, warnings, infos, manifest);

            // ──────────────────────────────────────────────────────────────────
            // KATMAN 2 — ZORUNLU FAZLAR
            // ──────────────────────────────────────────────────────────────────

            var ops     = manifest.Steps.Select(s => s.Op ?? "").ToList();
            var opsSet  = new HashSet<string>(ops, StringComparer.OrdinalIgnoreCase);
            var hasFromMany = manifest.Steps.Any(s => s.FromMany?.Count > 0);

            bool hasPrecheck  = manifest.PreChecks?.Count > 0;
            bool hasCollect   = ops.Any(o => CollectOps.Contains(o));
            bool hasValidate  = ops.Any(o => ValidateTransformOps.Contains(o));
            bool hasAggregate = ops.Any(o => AggregateOps.Contains(o)) || hasFromMany;
            bool hasSummary   = ops.Any(o => SummaryOps.Contains(o));
            bool hasRows      = ops.Any(o => RowsOps.Contains(o));
            bool hasExport    = ops.Any(o => o.StartsWith("export_", StringComparison.OrdinalIgnoreCase));

            // Kategori bazlı FAZ muafiyetleri
            // 4D/5D   : görselleştirme/simülasyon pipeline — VALIDATE/AGGREGATE/SUMMARY/ROWS/EXPORT gerekmez
            // CALC    : hesap sonucu pipeline — ROWS zorunlu değil (tablo yerine parametre yazar)
            var cat2 = (manifest.Category ?? "").ToLowerInvariant();
            bool is4D5D = cat2.Contains("4d") || cat2.Contains("5d") || cat2 == "4d/5d";
            bool isCalc = cat2 == "calc" || opsSet.Contains("calc_lap_length") ||
                          opsSet.Contains("calc_anchorage_length") || opsSet.Contains("rebar_weight_table");

            if (!hasPrecheck)
                errors.Add("FAZ-01: PRECHECK eksik — pre_checks listesi boş");
            if (!hasCollect)
                errors.Add("FAZ-02: COLLECT eksik — collect_* veya load_* op bulunamadı");
            if (!hasValidate && !is4D5D)
                warnings.Add("FAZ-03: VALIDATE/TRANSFORM eksik");
            if (!hasAggregate && !is4D5D)
                warnings.Add("FAZ-04: AGGREGATE eksik");
            if (!hasSummary && !is4D5D)
                errors.Add("FAZ-05: SUMMARY eksik — validation_summary veya show_table yok");
            if (!hasRows && !is4D5D && !isCalc)
                errors.Add("FAZ-06: ROWS eksik — validation_to_rows veya elements_to_rows yok");
            if (!hasExport && !is4D5D)
                errors.Add("FAZ-07: EXPORT eksik — export_xlsx veya export_validation_report yok");

            // ──────────────────────────────────────────────────────────────────
            // KATMAN 3 — GÜVENLİK
            // ──────────────────────────────────────────────────────────────────

            var hasPreviewGate = opsSet.Contains("preview_gate") || opsSet.Contains("schedule_gate");
            foreach (var step in manifest.Steps.Where(s => WriteOps.Contains(s.Op ?? "")))
                if (step.Required && !hasPreviewGate)
                    warnings.Add($"GVN-01: '{step.Id}' ({step.Op}) yazma op'u — required:false veya preview_gate önerilir");

            foreach (var step in manifest.Steps.Where(s =>
                s.Op?.StartsWith("export_", StringComparison.OrdinalIgnoreCase) == true))
                if (step.Required)
                    warnings.Add($"GVN-02: '{step.Id}' ({step.Op}) export adımı required:false olmalı");

            // ──────────────────────────────────────────────────────────────────
            // KATMAN 4 — PATTERN
            // ──────────────────────────────────────────────────────────────────

            var detectedPattern = DetectPattern(ops, manifest.Category);
            infos.Add($"PATTERN: {detectedPattern}");
            RunPatternChecks(detectedPattern, ops, opsSet, hasCollect, warnings);

            // ──────────────────────────────────────────────────────────────────
            // KATMAN 5 — SEMANTİK  ← YENİ
            // ──────────────────────────────────────────────────────────────────
            //
            //  Her adım için:
            //    1. Op'un OutputDomain'i çözülür, step ID'siyle kaydedilir
            //    2. Bu adımın 'from' kaynağının OutputDomain'i alınır
            //    3. Bu adımın InputDomain'i beklentisiyle karşılaştırılır
            //    4. Uyumsuzluk → SEM-01 (hata)  veya  SEM-02 (uyarı, from_many için)
            //
            //  EgOpContractAttribute varsa type-level kontrol de yapılır → SEM-03

            var stepOutputDomains = new Dictionary<string, Domain>(StringComparer.OrdinalIgnoreCase);
            var semanticErrors    = new List<string>();
            var semanticWarnings  = new List<string>();

            foreach (var step in manifest.Steps)
            {
                var opName   = step.Op ?? "";
                var (expectedInput, thisOutput) = GetDomain(opName);

                // ── from referansı domain kontrolü ──────────────────────────
                if (!string.IsNullOrEmpty(step.From) &&
                    stepOutputDomains.TryGetValue(step.From, out var sourceOut))
                {
                    if (!DomainsCompatible(sourceOut, expectedInput))
                    {
                        var sourceOp = manifest.Steps.FirstOrDefault(s => s.Id == step.From)?.Op ?? step.From;
                        semanticErrors.Add(
                            $"SEM-01: '{step.Id}' ({opName}) {expectedInput} kategorisi bekliyor, " +
                            $"'{step.From}' ({sourceOp}) {sourceOut} üretiyor — domain uyumsuzluğu");
                    }
                }

                // ── from_many referansları domain kontrolü ───────────────────
                if (step.FromMany != null)
                {
                    foreach (var fid in step.FromMany)
                    {
                        if (!stepOutputDomains.TryGetValue(fid, out var fOut)) continue;
                        if (!DomainsCompatible(fOut, expectedInput))
                        {
                            var fOp = manifest.Steps.FirstOrDefault(s => s.Id == fid)?.Op ?? fid;
                            semanticWarnings.Add(
                                $"SEM-02: '{step.Id}' from_many='{fid}' ({fOp}) domain uyumsuzluğu: " +
                                $"beklenen {expectedInput}, gelen {fOut}");
                        }
                    }
                }

                // ── EgOpContractAttribute type-level kontrol ─────────────────
                // Registry ve MethodInfo mevcutsa daha kesin tip denetimi
                if (registry != null && !string.IsNullOrEmpty(step.From))
                {
                    var thisMethod = registry.GetMethod(opName);
                    var thisContract = thisMethod?
                        .GetCustomAttributes(typeof(EgOpContractAttribute), false)
                        .FirstOrDefault() as EgOpContractAttribute;

                    if (thisContract?.InputType != null)
                    {
                        var fromStep   = manifest.Steps.FirstOrDefault(s => s.Id == step.From);
                        var fromMethod = fromStep != null ? registry.GetMethod(fromStep.Op ?? "") : null;
                        var fromContract = fromMethod?
                            .GetCustomAttributes(typeof(EgOpContractAttribute), false)
                            .FirstOrDefault() as EgOpContractAttribute;

                        if (fromContract?.OutputType != null &&
                            !thisContract.InputType.IsAssignableFrom(fromContract.OutputType))
                        {
                            semanticErrors.Add(
                                $"SEM-03: '{step.Id}' ({opName}) tip uyumsuzluğu — " +
                                $"{thisContract.InputType.Name} bekliyor, " +
                                $"'{step.From}' ({fromStep?.Op}) {fromContract.OutputType.Name} üretiyor");
                        }
                    }
                }

                // Bu adımın output domain'ini kaydet (sonraki adımlar için)
                if (!string.IsNullOrEmpty(step.Id))
                    stepOutputDomains[step.Id] = thisOutput;
            }

            // Semantik bulguları ana listelere ekle
            errors.AddRange(semanticErrors);
            warnings.AddRange(semanticWarnings);

            // Semantik özet info
            if (semanticErrors.Count == 0 && semanticWarnings.Count == 0)
                infos.Add("SEM: Tüm domain zincirleri uyumlu ✓");
            else
                infos.Add($"SEM: {semanticErrors.Count} domain hatası, {semanticWarnings.Count} uyarı");

            // ── Adım sayısı ────────────────────────────────────────────────────
            if (manifest.Steps.Count < 4)
                warnings.Add($"STEP-01: Çok az adım ({manifest.Steps.Count}) — min 5 önerilir");
            if (manifest.Steps.Count >= 8)
                infos.Add($"STEPS: {manifest.Steps.Count} adım (kapsamlı)");

            return BuildResult(errors, warnings, infos, manifest);
        }

        // ── Katman 4 pattern kontrolleri ─────────────────────────────────────

        private static void RunPatternChecks(
            string pattern, List<string> ops, HashSet<string> opsSet,
            bool hasCollect, List<string> warnings)
        {
            switch (pattern)
            {
                case "QA":
                    if (!opsSet.Contains("merge_validation_reports"))
                        warnings.Add("PAT-01: QA pattern — merge_validation_reports eksik");
                    if (!opsSet.Contains("validation_summary"))
                        warnings.Add("PAT-02: QA pattern — validation_summary eksik");
                    if (!opsSet.Contains("validation_to_rows"))
                        warnings.Add("PAT-03: QA pattern — validation_to_rows eksik");
                    if (!opsSet.Contains("export_validation_report"))
                        warnings.Add("PAT-04: QA pattern — export_validation_report eksik");
                    break;
                case "BOQ":
                    if (!hasCollect) warnings.Add("PAT-05: BOQ — collect adımı yok");
                    if (!opsSet.Contains("poz_match") && !opsSet.Contains("poz_match_keynote_aware"))
                        warnings.Add("PAT-06: BOQ — poz_match* eksik");
                    if (!opsSet.Contains("calc_cost"))
                        warnings.Add("PAT-07: BOQ — calc_cost eksik");
                    if (!opsSet.Contains("cost_summary") && !opsSet.Contains("cost_by_level"))
                        warnings.Add("PAT-08: BOQ — cost_summary veya cost_by_level eksik");
                    break;
                case "BOQ_KALIP":
                    if (!ops.Any(o => o.StartsWith("kalip_", StringComparison.OrdinalIgnoreCase)))
                        warnings.Add("PAT-09: KALIP — kalip_* adımı eksik");
                    break;
                case "MEP":
                    if (!opsSet.Contains("mep_by_system") && !opsSet.Contains("mep_total_length"))
                        warnings.Add("PAT-10: MEP — mep_by_system veya mep_total_length eksik");
                    break;
                case "IFC":
                    if (!opsSet.Contains("load_ifc_mapping"))
                        warnings.Add("PAT-11: IFC — load_ifc_mapping eksik");
                    if (!opsSet.Contains("ifc_export"))
                        warnings.Add("PAT-12: IFC — ifc_export eksik");
                    break;
            }
        }

        // ── Pattern tespiti ───────────────────────────────────────────────────

        private static string DetectPattern(List<string> ops, string? category)
        {
            var cat = category?.ToLowerInvariant() ?? "";
            bool hasKalip  = ops.Any(o => o.StartsWith("kalip_", StringComparison.OrdinalIgnoreCase));
            bool hasCost   = ops.Any(o => o is "calc_cost" or "cost_summary" or "cost_by_level");
            bool hasPoz    = ops.Any(o => o.StartsWith("poz_match", StringComparison.OrdinalIgnoreCase));
            bool hasQa     = ops.Any(o =>
                o.StartsWith("param_", StringComparison.OrdinalIgnoreCase) ||
                o.StartsWith("validate_", StringComparison.OrdinalIgnoreCase) ||
                o.StartsWith("qa_", StringComparison.OrdinalIgnoreCase) ||
                o.StartsWith("check_", StringComparison.OrdinalIgnoreCase));
            bool hasMep    = ops.Any(o => o is "mep_by_system" or "mep_total_length" or
                                          "collect_pipes" or "collect_ducts" or "collect_cable_trays");
            bool hasIfc    = ops.Any(o => o is "ifc_export" or "map_to_ifc" or "load_ifc_mapping");
            bool hasWbs    = ops.Any(o => o is "assign_wbs_code" or "link_quantity_to_wbs" or "load_wbs_mapping");
            bool hasRebar  = ops.Any(o =>
                o.StartsWith("rebar_", StringComparison.OrdinalIgnoreCase) ||
                o.StartsWith("validate_rebar_", StringComparison.OrdinalIgnoreCase));
            bool hasCalc   = ops.Any(o => o is "calc_lap_length" or "calc_anchorage_length" or "calc_min_spacing");
            bool hasRename = ops.Any(o => o is "rename_apply" or "rename_preview" or "rename_element");
            bool hasRoom   = ops.Any(o => o is "collect_rooms" or "check_unplaced_rooms" or "check_overlapping_rooms");

            if (hasKalip && hasCost)  return "BOQ_KALIP";
            if (hasPoz   && hasCost)  return "BOQ";
            if (hasIfc)               return "IFC";
            if (hasWbs)               return "WBS";
            if (hasRebar || hasCalc)  return "CALC";
            if (hasRename)            return "RENAME";
            if (hasRoom  && hasQa)    return "ROOM";
            if (hasMep)               return "MEP";
            if (hasQa)                return "QA";

            return cat switch
            {
                "kalip"    => "BOQ_KALIP",
                "maliyet"  => "BOQ",
                "mep"      => "MEP",
                "yangin"   => "MEP",
                "elektrik" => "MEP",
                "ifc"      => "IFC",
                "wbs"      => "WBS",
                _          => "GENEL"
            };
        }

        // ── STEP 4: BuildResult ───────────────────────────────────────────────

        private static LintResult BuildResult(
            List<string> errors, List<string> warnings, List<string> infos,
            EgManifest manifest)
        {
            // ── Puan hesabı ───────────────────────────────────────────────────
            double score = 10.0;

            var criticalCount = errors.Count(e => e.StartsWith("KRT-"));
            var fazErrors     = errors.Count(e => e.StartsWith("FAZ-"));

            // SEM-01/SEM-03 hataları (domain/type mismatch) kritik sayılır
            var semErrors     = errors.Count(e => e.StartsWith("SEM-"));
            var semWarnings   = warnings.Count(w => w.StartsWith("SEM-"));

            if (criticalCount > 0)
                score = 0;
            else
            {
                score -= fazErrors  * 1.5;
                score -= semErrors  * 1.0;   // SEM hatası her biri -1 puan
                score -= semWarnings * 0.25; // SEM uyarısı -0.25 puan
                score -= (warnings.Count - semWarnings) * 0.35;

                if (manifest.Steps?.Count >= 10) score += 0.5;
                else if (manifest.Steps?.Count < 4) score -= 1.0;

                score = Math.Max(0, Math.Min(10, Math.Round(score, 1)));
            }

            // ── Tier ─────────────────────────────────────────────────────────
            string tier, tagText, tagColor;

            bool hasCritical = criticalCount > 0;
            bool hasSemError = semErrors > 0;

            if (hasCritical)
            { tier = "KRİTİK";  tagText = "KRİTİK";  tagColor = "#FF5555"; }
            else if (hasSemError)
            // SEM hatası ayrı tier — yapısal temiz ama anlam bozuk
            { tier = "SEM-HATA"; tagText = "SEM-HATA"; tagColor = "#FF79C6"; }
            else if (fazErrors == 0 && warnings.Count <= 2 && semWarnings == 0)
            { tier = "TEMIZ";   tagText = "TEMIZ";   tagColor = "#50FA7B"; }
            else if (fazErrors <= 1)
            { tier = "UYARI";   tagText = "UYARI";   tagColor = "#FFB86C"; }
            else if (fazErrors <= 3)
            { tier = "ZAYIF";   tagText = "ZAYIF";   tagColor = "#F1FA8C"; }
            else
            { tier = "KRİTİK";  tagText = "KRİTİK";  tagColor = "#FF5555"; }

            return new LintResult
            {
                IsValid      = !hasCritical && !hasSemError,
                Score        = score,
                Tier         = tier,
                TagText      = tagText,
                TagColor     = tagColor,
                Errors       = errors,
                Warnings     = warnings,
                Infos        = infos,
                Pattern      = infos.FirstOrDefault(i => i.StartsWith("PATTERN:"))
                                    ?.Replace("PATTERN: ", "") ?? "GENEL",
                StepCount    = manifest.Steps?.Count ?? 0,
                HasPrecheck  = manifest.PreChecks?.Count > 0,
                HasCollect   = manifest.Steps?.Any(s => CollectOps.Contains(s.Op ?? "")) == true,
                HasValidate  = manifest.Steps?.Any(s => ValidateTransformOps.Contains(s.Op ?? "")) == true,
                HasAggregate = (manifest.Steps?.Any(s => AggregateOps.Contains(s.Op ?? "")) == true)
                               || (manifest.Steps?.Any(s => s.FromMany?.Count > 0) == true),
                HasSummary   = manifest.Steps?.Any(s => SummaryOps.Contains(s.Op ?? "")) == true,
                HasRows      = manifest.Steps?.Any(s => RowsOps.Contains(s.Op ?? "")) == true,
                HasExport    = manifest.Steps?.Any(s =>
                    s.Op?.StartsWith("export_", StringComparison.OrdinalIgnoreCase) == true) == true,
                SemanticErrorCount   = semErrors,
                SemanticWarningCount = semWarnings,
            };
        }
    }

    // ── STEP 4 (devam): LintResult ───────────────────────────────────────────

    public sealed class LintResult
    {
        public bool         IsValid      { get; init; }
        public double       Score        { get; init; }
        public string       Tier         { get; init; } = "";
        public string       TagText      { get; init; } = "";
        public string       TagColor     { get; init; } = "#888888";
        public string       Pattern      { get; init; } = "";

        public List<string> Errors       { get; init; } = new();
        public List<string> Warnings     { get; init; } = new();
        public List<string> Infos        { get; init; } = new();

        public int  StepCount    { get; init; }
        public bool HasPrecheck  { get; init; }
        public bool HasCollect   { get; init; }
        public bool HasValidate  { get; init; }
        public bool HasAggregate { get; init; }
        public bool HasSummary   { get; init; }
        public bool HasRows      { get; init; }
        public bool HasExport    { get; init; }

        // ── STEP 5: Semantik sayaçlar (Browser badge için) ───────────────────
        public int SemanticErrorCount   { get; init; }
        public int SemanticWarningCount { get; init; }

        public bool HasSemanticIssues => SemanticErrorCount > 0 || SemanticWarningCount > 0;

        public string Summary =>
            Errors.Count == 0 && Warnings.Count == 0
                ? $"✓ {Tier} — {StepCount} adım, {Score}/10"
                : $"{Tier} — {Errors.Count} hata, {Warnings.Count} uyarı, {Score}/10" +
                  (HasSemanticIssues ? $" [{SemanticErrorCount} SEM]" : "");
    }
}
