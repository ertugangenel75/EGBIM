using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO V4 — Mekanik MEP Op'ları (Grup 4)
    /// Hava terminali, kanal boyut/eğim/hız, basınç düşüm, bölge ataması
    /// </summary>
    public static class MechanicalOps
    {
        [EgOp("mep_validate_duct_velocity",
            Description = "Havalandırma kanalı hız değerini max sınırla karşılaştırır",
            Category    = "MEP-Mekanik")]
        public static ValidationReport ValidateDuctVelocity(OpContext ctx)
        {
            var ducts    = ctx.InputAsOrDefault<List<Element>>();
            var maxVel   = ctx.GetDouble("max_velocity_m_s", 6.0);
            var results  = new List<ValidationResult>();

            foreach (var el in ducts)
            {
                if (el is not Duct duct) continue;
                // Revit 2026: RBS_VELOCITY_MAX BIP kaldırıldı — LookupParameter kullan
                var velParam = duct.LookupParameter("Velocity");
                double vel   = (velParam?.AsDouble() ?? 0) * 0.3048; // ft/s → m/s
                bool ok      = vel <= maxVel || vel < 0.01;
                results.Add(new ValidationResult
                {
                    RuleId    = "ME02-HIZ",
                    ElementId = Rv.IdStr(duct.Id),
                    Category  = "Kanal",
                    CheckType = "Hız Kontrolü",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "WARNING",
                    Message   = $"Hız: {vel:F1} m/s" + (ok ? " ✓" : $" > {maxVel} m/s")
                });
            }
            ctx.Log($"  mep_validate_duct_velocity: {results.Count(r => !r.Passed)} kanal hız aşımı");
            return MakeReport("Kanal Hız Kontrolü", results);
        }

        [EgOp("mep_validate_duct_size",
            Description = "Kanal boyutunun sistem tipine göre minimum sınırı sağladığını kontrol eder",
            Category    = "MEP-Mekanik")]
        public static ValidationReport ValidateDuctSize(OpContext ctx)
        {
            var ducts   = ctx.InputAsOrDefault<List<Element>>();
            var minSize = ctx.GetDouble("min_size_mm", 100.0) / 304.8;
            var results = new List<ValidationResult>();

            foreach (var el in ducts)
            {
                if (el is not Duct duct) continue;
                var wParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)
                          ?? duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                double w   = wParam?.AsDouble() ?? 0;
                bool ok    = w >= minSize;
                results.Add(new ValidationResult
                {
                    RuleId    = "ME03-BOYUT",
                    ElementId = Rv.IdStr(duct.Id),
                    Category  = "Kanal",
                    CheckType = "Minimum Boyut",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "WARNING",
                    Message   = $"Boyut: {w * 304.8:F0} mm" + (ok ? " ✓" : $" < {minSize * 304.8:F0} mm")
                });
            }
            return MakeReport("Kanal Boyut Kontrolü", results);
        }

        [EgOp("mep_validate_space_hvac_zone",
            Description = "Mekânların HVAC bölge ataması olup olmadığını kontrol eder",
            Category    = "MEP-Mekanik")]
        public static ValidationReport ValidateSpaceHvacZone(OpContext ctx)
        {
            var spaces   = ctx.InputAsOrDefault<List<Element>>();
            var results  = new List<ValidationResult>();

            foreach (var el in spaces)
            {
                if (el is not Space space) continue;
                var zone   = space.Zone;
                bool hasZone = zone != null;
                results.Add(new ValidationResult
                {
                    RuleId    = "ME06-ZONE",
                    ElementId = Rv.IdStr(space.Id),
                    Category  = "Mekân",
                    CheckType = "HVAC Zone",
                    Passed    = hasZone,
                    Severity  = "WARNING",
                    Message   = hasZone ? $"Zone: {zone!.Name}" : "Zone atanmamış"
                });
            }
            ctx.Log($"  mep_validate_space_hvac_zone: {results.Count(r => !r.Passed)} mekânda zone yok");
            return MakeReport("HVAC Zone Kontrolü", results);
        }

        [EgOp("mep_air_terminal_space_map",
            Description = "Hava terminallerini bağlı oldukları mekân ile eşleştirir",
            Category    = "MEP-Mekanik")]
        public static List<Dictionary<string, object?>> AirTerminalSpaceMap(OpContext ctx)
        {
            var terminals = ctx.InputAsOrDefault<List<Element>>();
            var results   = new List<Dictionary<string, object?>>();

            foreach (var el in terminals)
            {
                if (el is not FamilyInstance fi) continue;
                var space = fi.Space;
                results.Add(new Dictionary<string, object?>
                {
                    ["element_id"]   = Rv.IdStr(fi.Id),
                    ["tip"]          = fi.Symbol.Name,
                    ["mekan_id"]     = Rv.IdStr(space!.Id) ?? "",
                    ["mekan_adi"]    = space?.Name ?? "MEKÂNSİZ",
                    ["sistem"]       = fi.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? "",
                    ["durum"]        = space != null ? "ATANMIŞ" : "MEKANSIZ"
                });
            }
            ctx.Log($"  mep_air_terminal_space_map: {results.Count} terminal işlendi");
            return results;
        }

        [EgOp("mep_validate_duct_slope",
            Description = "Pissu/yoğuşma kanalı eğim değerini kontrol eder",
            Category    = "MEP-Mekanik")]
        public static ValidationReport ValidateDuctSlope(OpContext ctx)
        {
            var ducts    = ctx.InputAsOrDefault<List<Element>>();
            var minSlope = ctx.GetDouble("min_slope_pct", 0.5) / 100.0;
            var results  = new List<ValidationResult>();

            foreach (var el in ducts)
            {
                var slopeParam = el.get_Parameter(BuiltInParameter.RBS_PIPE_SLOPE)
                            ?? el.LookupParameter("Slope")
                              ?? el.LookupParameter("Slope")
                              ?? el.LookupParameter("Eğim");
                double slope   = slopeParam?.AsDouble() ?? 0;
                bool ok        = slope >= minSlope || slope < 0.0001;
                results.Add(new ValidationResult
                {
                    RuleId    = "ME07-EGIM",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "Eğim",
                    Passed    = ok,
                    Severity  = "WARNING",
                    Message   = $"Eğim: {slope * 100:F2}%" + (ok ? "" : $" < min {minSlope * 100:F1}%")
                });
            }
            return MakeReport("Kanal Eğim Kontrolü", results);
        }

        private static ValidationReport MakeReport(string t, List<ValidationResult> r)
            => new() { ManifestTitle = t, TotalChecks = r.Count, Passed = r.Count(x => x.Passed),
                       Failed = r.Count(x => !x.Passed && x.Severity == "ERROR"),
                       Warnings = r.Count(x => !x.Passed && x.Severity == "WARNING"), Results = r };
    }

    /// <summary>
    /// EGBIMOTO V4 — Elektrik Op'ları (Grup 5)
    /// </summary>
    public static class ElectricalOps
    {
        [EgOp("elec_validate_lux_level",
            Description = "Aydınlatma fikstürlerinin lux parametresini minimum değerle karşılaştırır",
            Category    = "MEP-Elektrik")]
        public static ValidationReport ValidateLuxLevel(OpContext ctx)
        {
            var fixtures = ctx.InputAsOrDefault<List<Element>>();
            var minLux   = ctx.GetDouble("min_lux", 300.0);
            var results  = new List<ValidationResult>();

            foreach (var el in fixtures)
            {
                var luxParam = el.LookupParameter("Calculated Illuminance")
                            ?? el.LookupParameter("Aydınlık Düzeyi")
                            ?? el.LookupParameter("Lux");
                double lux  = luxParam?.AsDouble() ?? 0;
                bool ok     = lux >= minLux || lux < 0.001;
                results.Add(new ValidationResult
                {
                    RuleId    = "EL04-LUX",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "Aydınlatma",
                    CheckType = "Lux Seviyesi",
                    Passed    = ok,
                    Severity  = "WARNING",
                    Message   = $"Lux: {lux:F0}" + (ok ? " ✓" : $" < {minLux}")
                });
            }
            ctx.Log($"  elec_validate_lux_level: {results.Count(r => !r.Passed)} yetersiz lux");
            return MakeReport("Lux Seviyesi Kontrolü", results);
        }

        [EgOp("elec_check_circuit_assigned",
            Description = "Elektrik elemanlarının bir devreye bağlı olup olmadığını kontrol eder",
            Category    = "MEP-Elektrik")]
        public static ValidationReport CheckCircuitAssigned(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var results  = new List<ValidationResult>();

            foreach (var el in elements)
            {
                var circuitParam = el.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER)
                                ?? el.LookupParameter("Circuit Number")
                                ?? el.LookupParameter("Devre No");
                bool assigned    = circuitParam?.AsString() is { Length: > 0 };
                results.Add(new ValidationResult
                {
                    RuleId    = "EL02-DEVRE",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "Devre Ataması",
                    Passed    = assigned,
                    Severity  = "WARNING",
                    Message   = assigned ? $"Devre: {circuitParam?.AsString()}" : "Devre atanmamış"
                });
            }
            ctx.Log($"  elec_check_circuit_assigned: {results.Count(r => !r.Passed)} devresiz eleman");
            return MakeReport("Devre Ataması Kontrolü", results);
        }

        [EgOp("elec_validate_panel_load",
            Description = "Panel yük kapasitesini kontrol eder",
            Category    = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> ValidatePanelLoad(OpContext ctx)
        {
            var panels  = ctx.InputAsOrDefault<List<Element>>();
            var maxLoad = ctx.GetDouble("max_load_kva", 100.0);
            var results = new List<Dictionary<string, object?>>();

            foreach (var el in panels)
            {
                var loadParam = el.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_TOTALLOAD_PARAM)
                             ?? el.LookupParameter("Total Estimated Load");
                double load   = loadParam?.AsDouble() ?? 0;
                double loadKva = load / 1000.0;
                results.Add(new Dictionary<string, object?>
                {
                    ["panel_adi"]  = el.Name,
                    ["element_id"] = Rv.IdStr(el.Id),
                    ["yuk_kva"]    = Math.Round(loadKva, 2),
                    ["max_kva"]    = maxLoad,
                    ["durum"]      = loadKva <= maxLoad ? "OK" : "KAPASİTE_AŞIMI"
                });
            }
            ctx.Log($"  elec_validate_panel_load: {results.Count(r => r["durum"]?.ToString() == "KAPASİTE_AŞIMI")} panel aşımda");
            return results;
        }

        [EgOp("elec_check_emergency_lighting",
            Description = "Acil çıkış gerektiren alanlarda acil aydınlatma varlığını kontrol eder",
            Category    = "MEP-Elektrik")]
        public static ValidationReport CheckEmergencyLighting(OpContext ctx)
        {
            var rooms       = ctx.InputAsOrDefault<List<Element>>();
            var fixtures    = ctx.GetParam<List<Element>>("fixtures") ?? new();
            var emergencyKw = ctx.GetList<string>("emergency_keywords");
            var results     = new List<ValidationResult>();

            var emergencyIds = new HashSet<ElementId>(
                fixtures.OfType<FamilyInstance>()
                    .Where(f => emergencyKw.Any(k =>
                        f.Symbol.Name.ToLowerInvariant().Contains(k.ToLowerInvariant()) ||
                        f.Symbol.Family.Name.ToLowerInvariant().Contains(k.ToLowerInvariant())))
                    .Select(f => f.Room?.Id).Where(id => id != null).Select(id => id!));

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;
                bool hasEmergency = emergencyIds.Contains(room.Id);
                results.Add(new ValidationResult
                {
                    RuleId    = "EL05-ACİL",
                    ElementId = Rv.IdStr(room.Id),
                    Category  = "Oda",
                    CheckType = "Acil Aydınlatma",
                    Passed    = hasEmergency,
                    Severity  = "ERROR",
                    Message   = hasEmergency ? "Acil aydınlatma mevcut ✓" : "Acil aydınlatma YOK"
                });
            }
            ctx.Log($"  elec_check_emergency_lighting: {results.Count(r => !r.Passed)} odada acil aydınlatma eksik");
            return MakeReport("Acil Aydınlatma Kontrolü", results);
        }

        [EgOp("elec_generate_panel_schedule",
            Description = "Panel yük dökümünü satır tablosuna dönüştürür (Excel export için)",
            Category    = "MEP-Elektrik")]
        public static List<Dictionary<string, object?>> GeneratePanelSchedule(OpContext ctx)
        {
            var panels  = ctx.InputAsOrDefault<List<Element>>();
            var results = new List<Dictionary<string, object?>>();
            int sira    = 1;

            foreach (var el in panels)
            {
                var loadP  = el.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_TOTALLOAD_PARAM);
                var voltP  = el.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE)
                            ?? el.LookupParameter("Voltage");
                var phaseP = el.get_Parameter(BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES);
                results.Add(new Dictionary<string, object?>
                {
                    ["sira"]       = sira++,
                    ["panel_adi"]  = el.Name,
                    ["element_id"] = Rv.IdStr(el.Id),
                    ["gerilim_v"]  = voltP?.AsDouble() ?? 0,
                    ["faz"]        = phaseP?.AsInteger() ?? 0,
                    ["yuk_w"]      = loadP?.AsDouble() ?? 0,
                    ["yuk_kva"]    = Math.Round((loadP?.AsDouble() ?? 0) / 1000.0, 2),
                    ["kat"]        = el.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)?.AsValueString() ?? ""
                });
            }
            ctx.Log($"  elec_generate_panel_schedule: {results.Count} panel listelendi");
            return results;
        }

        private static ValidationReport MakeReport(string t, List<ValidationResult> r)
            => new() { ManifestTitle = t, TotalChecks = r.Count, Passed = r.Count(x => x.Passed),
                       Failed = r.Count(x => !x.Passed && x.Severity == "ERROR"),
                       Warnings = r.Count(x => !x.Passed && x.Severity == "WARNING"), Results = r };
    }

    /// <summary>
    /// EGBIMOTO V4 — Sıhhi Tesisat Op'ları (Grup 6)
    /// </summary>
    public static class PlumbingOps
    {
        [EgOp("plumbing_validate_pipe_slope",
            Description = "Pissu borularında minimum %1 eğim kontrolü yapar",
            Category    = "MEP-Sıhhi")]
        public static ValidationReport ValidatePipeSlope(OpContext ctx)
        {
            var pipes    = ctx.InputAsOrDefault<List<Element>>();
            var minSlope = ctx.GetDouble("min_slope_pct", 1.0) / 100.0;
            var results  = new List<ValidationResult>();

            foreach (var el in pipes)
            {
                if (el is not Pipe pipe) continue;
                var slopeParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_SLOPE)
                            ?? pipe.LookupParameter("Slope");
                double slope   = Math.Abs(slopeParam?.AsDouble() ?? 0);
                var sysName    = pipe.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? "";
                bool isPissu   = sysName.ToLowerInvariant().Contains("pis") ||
                                 sysName.ToLowerInvariant().Contains("drain") ||
                                 sysName.ToLowerInvariant().Contains("waste");
                if (!isPissu) continue;

                bool ok = slope >= minSlope;
                results.Add(new ValidationResult
                {
                    RuleId    = "PL01-EGIM",
                    ElementId = Rv.IdStr(pipe.Id),
                    Category  = "Boru",
                    CheckType = "Pissu Eğim",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "ERROR",
                    Message   = $"Eğim: {slope * 100:F2}%" + (ok ? " ✓" : $" < %{minSlope * 100:F1}")
                });
            }
            ctx.Log($"  plumbing_validate_pipe_slope: {results.Count(r => !r.Passed)} yetersiz eğim");
            return MakeReport("Pissu Eğim Kontrolü", results);
        }

        [EgOp("plumbing_validate_connector_diameter",
            Description = "Tesisat armatürü bağlantı çapını standart değerle karşılaştırır",
            Category    = "MEP-Sıhhi")]
        public static List<Dictionary<string, object?>> ValidateConnectorDiameter(OpContext ctx)
        {
            var fixtures   = ctx.InputAsOrDefault<List<Element>>();
            var expectedDm = ctx.GetDouble("expected_diameter_mm", 50.0) / 304.8;
            var tolerance  = ctx.GetDouble("tolerance_mm", 5.0) / 304.8;
            var results    = new List<Dictionary<string, object?>>();

            foreach (var el in fixtures)
            {
                if (el is not FamilyInstance fi) continue;
                var connMgr = fi.MEPModel?.ConnectorManager;
                if (connMgr == null) continue;

                foreach (Connector conn in connMgr.Connectors)
                {
                    if (conn.ConnectorType != ConnectorType.End) continue;
                    double dia = conn.Radius * 2 * 304.8;
                    bool ok    = Math.Abs(dia - expectedDm * 304.8) <= tolerance * 304.8;
                    results.Add(new Dictionary<string, object?>
                    {
                        ["element_id"] = Rv.IdStr(fi.Id),
                        ["tip"]        = fi.Symbol.Name,
                        ["cap_mm"]     = Math.Round(dia, 1),
                        ["beklenen_mm"]= Math.Round(expectedDm * 304.8, 1),
                        ["durum"]      = ok ? "OK" : "CAP_UYUMSUZ"
                    });
                }
            }
            ctx.Log($"  plumbing_validate_connector_diameter: {results.Count(r => r["durum"]?.ToString() == "CAP_UYUMSUZ")} uyumsuz");
            return results;
        }

        [EgOp("plumbing_check_fixture_room_assigned",
            Description = "Tesisat armatürlerinin bir odaya atanıp atanmadığını kontrol eder",
            Category    = "MEP-Sıhhi")]
        public static ValidationReport CheckFixtureRoomAssigned(OpContext ctx)
        {
            var fixtures = ctx.InputAsOrDefault<List<Element>>();
            var results  = new List<ValidationResult>();

            foreach (var el in fixtures)
            {
                if (el is not FamilyInstance fi) continue;
                var room    = fi.Room;
                bool hasRoom = room != null;
                results.Add(new ValidationResult
                {
                    RuleId    = "PL07-ODA",
                    ElementId = Rv.IdStr(fi.Id),
                    Category  = "Sıhhi Armatür",
                    CheckType = "Oda Ataması",
                    Passed    = hasRoom,
                    Severity  = "WARNING",
                    Message   = hasRoom ? $"Oda: {room!.Name}" : "Oda ataması yok"
                });
            }
            ctx.Log($"  plumbing_check_fixture_room_assigned: {results.Count(r => !r.Passed)} atasız armatür");
            return MakeReport("Armatür Oda Ataması", results);
        }

        [EgOp("plumbing_validate_system_separation",
            Description = "Sıcak/soğuk su hatlarının sistem tipine göre ayrıldığını kontrol eder",
            Category    = "MEP-Sıhhi")]
        public static List<Dictionary<string, object?>> ValidateSystemSeparation(OpContext ctx)
        {
            var pipes   = ctx.InputAsOrDefault<List<Element>>();
            var results = new List<Dictionary<string, object?>>();

            foreach (var el in pipes)
            {
                var sysParam = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
                var sysName  = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? "";
                var sysType  = sysParam?.AsValueString() ?? "Bilinmiyor";
                results.Add(new Dictionary<string, object?>
                {
                    ["element_id"]   = Rv.IdStr(el.Id),
                    ["sistem_tipi"]  = sysType,
                    ["sistem_adi"]   = sysName,
                    ["durum"]        = string.IsNullOrEmpty(sysType) || sysType == "Bilinmiyor" ? "SİSTEM_YOK" : "OK"
                });
            }
            ctx.Log($"  plumbing_validate_system_separation: {results.Count} boru analiz edildi");
            return results;
        }

        [EgOp("plumbing_calc_flow_rate",
            Description = "Tesisat armatürleri için toplam debi hesabı yapar",
            Category    = "MEP-Sıhhi")]
        public static List<Dictionary<string, object?>> CalcFlowRate(OpContext ctx)
        {
            var fixtures = ctx.InputAsOrDefault<List<Element>>();
            var results  = new List<Dictionary<string, object?>>();

            foreach (var el in fixtures)
            {
                var flowParam = el.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM)
                             ?? el.LookupParameter("Flow")
                             ?? el.LookupParameter("Debi");
                double flow = flowParam?.AsDouble() ?? 0;
                double flowLs = flow * 0.4719; // CFM → L/s approx

                results.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.IdStr(el.Id),
                    ["tip"]        = el.Name,
                    ["debi_l_s"]   = Math.Round(flowLs, 3),
                    ["sistem"]     = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? ""
                });
            }
            ctx.Log($"  plumbing_calc_flow_rate: {results.Count} armatür hesaplandı");
            return results;
        }

        private static ValidationReport MakeReport(string t, List<ValidationResult> r)
            => new() { ManifestTitle = t, TotalChecks = r.Count, Passed = r.Count(x => x.Passed),
                       Failed = r.Count(x => !x.Passed && x.Severity == "ERROR"),
                       Warnings = r.Count(x => !x.Passed && x.Severity == "WARNING"), Results = r };
    }

    /// <summary>
    /// EGBIMOTO V4 — Yangın Sistemi Op'ları (Grup 7)
    /// </summary>
    public static partial class FireProtectionOps
    {
        [EgOp("fa_classify_room_detector",
            Description = "Oda adına göre beklenen dedektör tipini belirler (Duman/Isı)",
            Category    = "Yangın")]
        public static List<Dictionary<string, object?>> ClassifyRoomDetector(OpContext ctx)
        {
            var rooms          = ctx.InputAsOrDefault<List<Element>>();
            var heatKeywords   = ctx.GetList<string>("heat_detector_keywords");
            var results        = new List<Dictionary<string, object?>>();

            foreach (var el in rooms)
            {
                if (el is not Room room) continue;
                var name    = room.Name.ToLowerInvariant();
                bool isHeat = heatKeywords.Any(k => name.Contains(k.ToLowerInvariant()));
                results.Add(new Dictionary<string, object?>
                {
                    ["element_id"]     = Rv.IdStr(room.Id),
                    ["oda_adi"]        = room.Name,
                    ["numara"]         = room.Number,
                    ["kat"]            = room.get_Parameter(BuiltInParameter.LEVEL_NAME)?.AsString() ?? "",
                    ["beklenen_tip"]   = isHeat ? "Isı Dedektörü" : "Duman Dedektörü"
                });
            }
            ctx.Log($"  fa_classify_room_detector: {results.Count} oda sınıflandırıldı");
            return results;
        }

        [EgOp("fa_validate_device_in_room",
            Description = "Her oda için FA cihazı varlığını ve tipini doğrular",
            Category    = "Yangın")]
        public static ValidationReport ValidateDeviceInRoom(OpContext ctx)
        {
            var roomMap  = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>();
            var devices  = ctx.GetParam<List<Element>>("fa_devices") ?? new();
            var smokeKws = ctx.GetList<string>("smoke_keywords");
            var heatKws  = ctx.GetList<string>("heat_keywords");
            var results  = new List<ValidationResult>();

            var devicesByRoom = new Dictionary<string, List<string>>();
            foreach (var el in devices.OfType<FamilyInstance>())
            {
                var room = el.Room;
                if (room == null) continue;
                var key = Rv.IdStr(room.Id);
                if (!devicesByRoom.ContainsKey(key)) devicesByRoom[key] = new();
                var tname = (el.Symbol.Name + " " + el.Symbol.Family.Name).ToLowerInvariant();
                string dtype = smokeKws.Any(k => tname.Contains(k.ToLowerInvariant())) ? "Duman Dedektörü"
                             : heatKws.Any(k => tname.Contains(k.ToLowerInvariant()))  ? "Isı Dedektörü"
                             : "Diğer";
                devicesByRoom[key].Add(dtype);
            }

            foreach (var row in roomMap)
            {
                var id       = row.GetValueOrDefault("element_id")?.ToString() ?? "";
                var expected = row.GetValueOrDefault("beklenen_tip")?.ToString() ?? "";
                var odaAdi   = row.GetValueOrDefault("oda_adi")?.ToString() ?? "";
                var devicesInRoom = devicesByRoom.GetValueOrDefault(id, new());
                bool hasExpected  = devicesInRoom.Contains(expected);
                results.Add(new ValidationResult
                {
                    RuleId    = "FP04-DEDEKTÖR",
                    ElementId = id,
                    Category  = "Oda",
                    CheckType = "FA Cihaz Kontrolü",
                    Passed    = hasExpected,
                    Severity  = "ERROR",
                    Message   = hasExpected
                        ? $"{odaAdi}: {expected} mevcut ✓"
                        : $"{odaAdi}: {expected} EKSİK (mevcut: {string.Join(",", devicesInRoom.DefaultIfEmpty("YOK"))})"
                });
            }
            ctx.Log($"  fa_validate_device_in_room: {results.Count(r => !r.Passed)} odada cihaz eksik");
            return MakeReport("FA Oda Cihaz Kontrolü", results);
        }

        [EgOp("fa_validate_mounting_height",
            Description = "FA cihazlarının montaj yüksekliğini (AFF) kontrol eder",
            Category    = "Yangın")]
        public static ValidationReport ValidateMountingHeight(OpContext ctx)
        {
            var devices = ctx.InputAsOrDefault<List<Element>>();
            var minAff  = ctx.GetDouble("min_aff_mm", 2000.0) / 304.8;
            var maxAff  = ctx.GetDouble("max_aff_mm", 2400.0) / 304.8;
            var filter  = ctx.GetString("device_filter", "");
            var results = new List<ValidationResult>();

            foreach (var el in devices)
            {
                if (el is not FamilyInstance fi) continue;
                if (!string.IsNullOrEmpty(filter) &&
                    !filter.Split('|').Any(f => fi.Symbol.Name.ToLowerInvariant().Contains(f.ToLowerInvariant())))
                    continue;

                var bbox      = fi.get_BoundingBox(null);
                if (bbox == null) continue;
                var levelP    = fi.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                double levelZ = 0;
                if (levelP?.AsElementId() is { } lid && lid != ElementId.InvalidElementId)
                {
                    var lvl = (ctx as RevitOpContext ?? throw new InvalidOperationException($"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.")).Document.GetElement(lid) as Level;
                    levelZ  = lvl?.Elevation ?? 0;
                }
                double aff = ((bbox.Min.Z + bbox.Max.Z) / 2) - levelZ;
                bool ok    = aff >= minAff && aff <= maxAff;
                results.Add(new ValidationResult
                {
                    RuleId    = "FP05-AFF",
                    ElementId = Rv.IdStr(fi.Id),
                    Category  = "FA Cihaz",
                    CheckType = "Montaj Yüksekliği",
                    Passed    = ok,
                    Severity  = ok ? "INFO" : "WARNING",
                    Message   = $"{fi.Symbol.Name}: {aff * 304.8:F0} mm AFF" +
                                (ok ? " ✓" : $" ({minAff * 304.8:F0}–{maxAff * 304.8:F0} mm arası olmalı)")
                });
            }
            ctx.Log($"  fa_validate_mounting_height: {results.Count(r => !r.Passed)} yükseklik hatası");
            return MakeReport("FA Montaj Yüksekliği", results);
        }

        [EgOp("fa_validate_circuit_assigned",
            Description = "FA cihazlarının bir loop/devreye atanıp atanmadığını kontrol eder",
            Category    = "Yangın")]
        public static ValidationReport ValidateCircuitAssigned(OpContext ctx)
        {
            var devices = ctx.InputAsOrDefault<List<Element>>();
            var results = new List<ValidationResult>();

            foreach (var el in devices)
            {
                var sysParam  = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)
                             ?? el.LookupParameter("Circuit")
                             ?? el.LookupParameter("Loop");
                bool assigned = sysParam?.AsString() is { Length: > 0 };
                results.Add(new ValidationResult
                {
                    RuleId    = "FP06-LOOP",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = "FA Cihaz",
                    CheckType = "Devre/Loop",
                    Passed    = assigned,
                    Severity  = "WARNING",
                    Message   = assigned ? $"Loop: {sysParam?.AsString()}" : "Devre atanmamış"
                });
            }
            ctx.Log($"  fa_validate_circuit_assigned: {results.Count(r => !r.Passed)} devresiz cihaz");
            return MakeReport("FA Devre Ataması", results);
        }

        [EgOp("fp_validate_sprinkler_coverage",
            Description = "Sprinkler kapsama alanını hesaplar ve maksimum sınırla karşılaştırır",
            Category    = "Yangın")]
        public static List<Dictionary<string, object?>> ValidateSprinklerCoverage(OpContext ctx)
        {
            var sprinklers = ctx.InputAsOrDefault<List<Element>>();
            var maxArea    = ctx.GetDouble("max_coverage_m2", 12.0);
            var results    = new List<Dictionary<string, object?>>();

            foreach (var el in sprinklers)
            {
                var areaParam = el.LookupParameter("Coverage Area")
                             ?? el.LookupParameter("Kapsama Alanı");
                double areaM2 = (areaParam?.AsDouble() ?? 0) * 0.0929;
                bool ok       = areaM2 <= maxArea || areaM2 < 0.01;
                results.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.IdStr(el.Id),
                    ["tip"]        = el.Name,
                    ["alan_m2"]    = Math.Round(areaM2, 2),
                    ["max_m2"]     = maxArea,
                    ["durum"]      = ok ? "OK" : "MAX_AŞIMI"
                });
            }
            ctx.Log($"  fp_validate_sprinkler_coverage: {results.Count(r => r["durum"]?.ToString() == "MAX_AŞIMI")} aşım");
            return results;
        }

        private static ValidationReport MakeReport(string t, List<ValidationResult> r)
            => new() { ManifestTitle = t, TotalChecks = r.Count, Passed = r.Count(x => x.Passed),
                       Failed = r.Count(x => !x.Passed && x.Severity == "ERROR"),
                       Warnings = r.Count(x => !x.Passed && x.Severity == "WARNING"), Results = r };
    }

    /// <summary>
    /// EGBIMOTO V4 — Koordinasyon Op'ları (Grup 9)
    /// </summary>
    public static class CoordinationOps
    {
        [EgOp("coord_check_clearance",
            Description = "İki eleman grubu arasında minimum temizlik mesafesini kontrol eder",
            Category    = "Koordinasyon")]
        public static List<Dictionary<string, object?>> CheckClearance(OpContext ctx)
        {
            var primary   = ctx.InputAsOrDefault<List<Element>>();
            var secondary = ctx.GetParam<List<Element>>("secondary_elements") ?? new();
            var minDist   = ctx.GetDouble("min_clearance_mm", 50.0) / 304.8;
            var results   = new List<Dictionary<string, object?>>();

            foreach (var a in primary)
            {
                var bboxA = a.get_BoundingBox(null);
                if (bboxA == null) continue;
                var cA = (bboxA.Min + bboxA.Max) * 0.5;

                foreach (var b in secondary)
                {
                    if (a.Id == b.Id) continue;
                    var bboxB = b.get_BoundingBox(null);
                    if (bboxB == null) continue;
                    var cB   = (bboxB.Min + bboxB.Max) * 0.5;
                    double d = cA.DistanceTo(cB) * 304.8;
                    if (d < minDist * 304.8)
                        results.Add(new Dictionary<string, object?>
                        {
                            ["element_a"]  = Rv.IdStr(a.Id),
                            ["element_b"]  = Rv.IdStr(b.Id),
                            ["kategori_a"] = a.Category?.Name ?? "",
                            ["kategori_b"] = b.Category?.Name ?? "",
                            ["mesafe_mm"]  = Math.Round(d, 1),
                            ["min_mm"]     = Math.Round(minDist * 304.8, 1),
                            ["durum"]      = "ÇAKIŞMA"
                        });
                }
            }
            ctx.Log($"  coord_check_clearance: {results.Count} mesafe ihlali");
            return results;
        }

        [EgOp("coord_validate_penetration_firestop",
            Description = "Yangın bölgesi geçişlerinde mühürleme parametresini kontrol eder",
            Category    = "Koordinasyon")]
        public static ValidationReport ValidatePenetrationFirestop(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var results  = new List<ValidationResult>();

            foreach (var el in elements)
            {
                var firestopParam = el.LookupParameter("Firestop")
                                 ?? el.LookupParameter("Yangın Mühürü")
                                 ?? el.LookupParameter("FireStop Required");
                bool ok = firestopParam?.AsInteger() == 1 ||
                          firestopParam?.AsString()?.ToLower() is "yes" or "evet" or "true";
                results.Add(new ValidationResult
                {
                    RuleId    = "C06-MÜHÜR",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "Yangın Mühürü",
                    Passed    = ok,
                    Severity  = "ERROR",
                    Message   = ok ? "Mühür tanımlı ✓" : "Yangın mühürü parametresi eksik"
                });
            }
            ctx.Log($"  coord_validate_penetration_firestop: {results.Count(r => !r.Passed)} mühür eksik");
            return MakeReport("Yangın Geçiş Mühürü", results);
        }

        [EgOp("coord_validate_level_consistency",
            Description = "MEP elemanlarının mimari seviyelerle tutarlılığını kontrol eder",
            Category    = "Koordinasyon")]
        public static List<Dictionary<string, object?>> ValidateLevelConsistency(OpContext ctx)
        {
            var elements   = ctx.InputAsOrDefault<List<Element>>();
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var levels     = new FilteredElementCollector(doc)
                                .OfClass(typeof(Level)).Cast<Level>()
                                .OrderBy(l => l.Elevation).ToList();
            var results    = new List<Dictionary<string, object?>>();

            foreach (var el in elements)
            {
                var bbox = el.get_BoundingBox(null);
                if (bbox == null) continue;
                double elev = (bbox.Min.Z + bbox.Max.Z) / 2;
                var nearest = levels.OrderBy(l => Math.Abs(l.Elevation - elev)).FirstOrDefault();
                var assigned = el.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)?.AsValueString()
                            ?? el.get_Parameter(BuiltInParameter.LEVEL_PARAM)?.AsValueString() ?? "";
                bool ok = nearest == null || nearest.Name == assigned || string.IsNullOrEmpty(assigned);
                results.Add(new Dictionary<string, object?>
                {
                    ["element_id"]      = Rv.IdStr(el.Id),
                    ["kategori"]        = el.Category?.Name ?? "",
                    ["atanan_kat"]      = assigned,
                    ["geometrik_kat"]   = nearest?.Name ?? "",
                    ["durum"]           = ok ? "TUTARLI" : "TUTARSIZ"
                });
            }
            ctx.Log($"  coord_validate_level_consistency: {results.Count(r => r["durum"]?.ToString() == "TUTARSIZ")} tutarsız");
            return results;
        }

        private static ValidationReport MakeReport(string t, List<ValidationResult> r)
            => new() { ManifestTitle = t, TotalChecks = r.Count, Passed = r.Count(x => x.Passed),
                       Failed = r.Count(x => !x.Passed && x.Severity == "ERROR"),
                       Warnings = r.Count(x => !x.Passed && x.Severity == "WARNING"), Results = r };
    }

    /// <summary>
    /// EGBIMOTO V4 — Proje Yönetimi Op'ları (Grup 10)
    /// </summary>
    public static class ProjectMgmtOps
    {
        [EgOp("pm_validate_lod",
            Description = "Elemanların LOD parametre doluluk durumunu kontrol eder",
            Category    = "Proje Yönetimi")]
        public static ValidationReport ValidateLod(OpContext ctx)
        {
            var elements  = ctx.InputAsOrDefault<List<Element>>();
            var lodLevel  = ctx.GetString("lod_level", "LOD300");
            var required  = ctx.GetList<string>("required_params");
            var results   = new List<ValidationResult>();

            foreach (var el in elements)
            {
                var missing = required.Where(p =>
                {
                    var param = el.LookupParameter(p);
                    return param == null || string.IsNullOrWhiteSpace(param.AsValueString() ?? param.AsString() ?? "");
                }).ToList();
                bool ok = !missing.Any();
                results.Add(new ValidationResult
                {
                    RuleId    = $"PM01-{lodLevel}",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = $"LOD Kontrolü ({lodLevel})",
                    Passed    = ok,
                    Severity  = "ERROR",
                    Message   = ok ? $"{lodLevel} tamamlandı ✓"
                                   : $"Eksik: {string.Join(", ", missing)}"
                });
            }
            ctx.Log($"  pm_validate_lod: {results.Count(r => !r.Passed)} eleman {lodLevel} tamamlanmadı");
            return MakeReport($"LOD Kontrolü — {lodLevel}", results);
        }

        [EgOp("pm_get_project_info",
            Description = "Proje bilgi kartını toplar (isim, müellif, tarih, konum)",
            Category    = "Proje Yönetimi")]
        public static Dictionary<string, object?> GetProjectInfo(OpContext ctx)
        {
            var rctx_doc = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] Bu op Revit bağlamı gerektirir.");
            var doc = rctx_doc.Document;
            var info  = doc.ProjectInformation;
            return new Dictionary<string, object?>
            {
                ["proje_adi"]      = info?.Name ?? "",
                ["proje_no"]       = info?.Number ?? "",
                ["musteri"]        = info?.ClientName ?? "",
                ["adres"]          = info?.Address ?? "",
                ["yazar"]          = info?.Author ?? "",
                ["durum"]          = info?.Status ?? "",
                ["dosya_adi"]      = System.IO.Path.GetFileName(doc.PathName),
                ["revit_versiyonu"]= doc.Application.VersionNumber,
                ["olusturma_tarihi"] = DateTime.Now.ToString("yyyy-MM-dd")
            };
        }

        [EgOp("pm_validate_naming_convention",
            Description = "Eleman isimlerinin BEP/proje standardına uygunluğunu kontrol eder",
            Category    = "Proje Yönetimi")]
        public static ValidationReport ValidateNamingConvention(OpContext ctx)
        {
            var elements = ctx.InputAsOrDefault<List<Element>>();
            var pattern  = ctx.GetString("name_pattern", "");
            var results  = new List<ValidationResult>();

            foreach (var el in elements)
            {
                var name  = el.Name ?? "";
                bool ok   = string.IsNullOrEmpty(pattern) ||
                            System.Text.RegularExpressions.Regex.IsMatch(name, pattern);
                results.Add(new ValidationResult
                {
                    RuleId    = "PM08-ISIMLENDIRME",
                    ElementId = Rv.IdStr(el.Id),
                    Category  = el.Category?.Name ?? "",
                    CheckType = "İsimlendirme Standardı",
                    Passed    = ok,
                    Severity  = "WARNING",
                    Message   = ok ? $"{name} ✓" : $"{name} — pattern uyumsuz: {pattern}"
                });
            }
            ctx.Log($"  pm_validate_naming_convention: {results.Count(r => !r.Passed)} uyumsuz isim");
            return MakeReport("İsimlendirme Standardı Kontrolü", results);
        }

        [EgOp("pm_model_delta_summary",
            Description = "İki koleksiyon arasındaki farkı (eklenen/silinen) raporlar",
            Category    = "Proje Yönetimi")]
        public static List<Dictionary<string, object?>> ModelDeltaSummary(OpContext ctx)
        {
            var current  = (ctx.InputAsOrDefault<List<Element>>()).Select(e => Rv.GetId(e.Id)).ToHashSet();
            var previous = ctx.GetList<long>("previous_ids");
            var prevSet  = previous.ToHashSet();

            var added   = current.Except(prevSet).ToList();
            var removed = prevSet.Except(current).ToList();

            var results = new List<Dictionary<string, object?>>
            {
                new() { ["degisim"] = "Eklenen",  ["adet"] = added.Count,   ["ids"] = string.Join(",", added.Take(20)) },
                new() { ["degisim"] = "Silinen",  ["adet"] = removed.Count, ["ids"] = string.Join(",", removed.Take(20)) },
                new() { ["degisim"] = "Mevcut",   ["adet"] = current.Count, ["ids"] = "" }
            };
            ctx.Log($"  pm_model_delta_summary: +{added.Count} / -{removed.Count}");
            return results;
        }

        private static ValidationReport MakeReport(string t, List<ValidationResult> r)
            => new() { ManifestTitle = t, TotalChecks = r.Count, Passed = r.Count(x => x.Passed),
                       Failed = r.Count(x => !x.Passed && x.Severity == "ERROR"),
                       Warnings = r.Count(x => !x.Passed && x.Severity == "WARNING"), Results = r };
    }
}
