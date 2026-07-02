using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace EGBIMOTO.Core.Validation
{
    // ── Sonuç modelleri ───────────────────────────────────────────────────────

    public sealed class ValidationResult
    {
        public string RuleId      { get; set; } = "";
        public string ElementId   { get; set; } = "";
        public string Category    { get; set; } = "";
        public string CheckType   { get; set; } = "";
        public bool   Passed      { get; set; }
        public string Message     { get; set; } = "";
        public string Severity    { get; set; } = "ERROR"; // ERROR | WARNING | INFO
    }

    public sealed class ValidationReport
    {
        public string ManifestTitle { get; set; } = "";
        public int    TotalChecks   { get; set; }
        public int    Passed        { get; set; }
        public int    Failed        { get; set; }
        public int    Warnings      { get; set; }
        public List<ValidationResult> Results { get; set; } = new();
        public bool   IsValid => Failed == 0;
        public string Summary => $"{Passed}/{TotalChecks} geçti, {Failed} hata, {Warnings} uyarı";
    }

    // ── IDS Parser (basit XML tabanlı) ────────────────────────────────────────

    public sealed class IdsRule
    {
        public string Id                        { get; set; } = "";
        public string Name                      { get; set; } = "";
        public string Description               { get; set; } = "";
        public string Applicability             { get; set; } = ""; // IfcEntityFacet adı
        public string ApplicabilityPredefinedType { get; set; } = ""; // IfcEntityFacet predefinedType
        public List<IdsRequirement> Requirements { get; set; } = new();
    }

    public sealed class IdsRequirement
    {
        public string Type        { get; set; } = ""; // entity | attribute | property | material | classification
        public string Name        { get; set; } = "";
        public string Value       { get; set; } = "";
        public string PropertySet { get; set; } = ""; // IfcPropertyFacet için pset adı
        public string DataType    { get; set; } = ""; // IfcPropertyFacet için veri tipi
        public bool   Required    { get; set; } = true;
        public string Severity    { get; set; } = "ERROR";
    }

    /// <summary>
    /// IDS 1.0 (Information Delivery Specification) tam parser.
    /// Desteklenen facet'ler:
    ///   - IfcEntityFacet    (entity/name, entity/predefinedType)
    ///   - IfcAttributeFacet (attribute/name, attribute/value)
    ///   - IfcPropertyFacet  (property/propertySetName, property/baseName, property/value)
    ///   - IfcMaterialFacet  (material/value)
    ///   - IfcClassificationFacet (classification/system, classification/value)
    /// </summary>
    public static class IdsParser
    {
        public static List<IdsRule> ParseFile(string idsPath)
        {
            if (!File.Exists(idsPath))
                throw new FileNotFoundException($"IDS dosyası bulunamadı: {idsPath}");

            var rules = new List<IdsRule>();
            try
            {
                var doc = XDocument.Load(idsPath);
                var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;

                foreach (var spec in doc.Descendants(ns + "specification"))
                {
                    var rule = new IdsRule
                    {
                        Id          = spec.Attribute("identifier")?.Value ?? Guid.NewGuid().ToString(),
                        Name        = spec.Attribute("name")?.Value ?? "",
                        Description = spec.Element(ns + "description")?.Value ?? "",
                    };

                    // ── Applicability — IfcEntityFacet ───────────────────────
                    var appEntity = spec
                        .Element(ns + "applicability")?
                        .Descendants(ns + "entity")
                        .FirstOrDefault();

                    rule.Applicability = appEntity?.Element(ns + "name")
                        ?.Element(ns + "simpleValue")?.Value
                        ?? appEntity?.Element(ns + "name")?.Value
                        ?? "";

                    rule.ApplicabilityPredefinedType = appEntity?.Element(ns + "predefinedType")
                        ?.Element(ns + "simpleValue")?.Value ?? "";

                    // ── Requirements ─────────────────────────────────────────
                    var reqsRoot = spec.Element(ns + "requirements");
                    if (reqsRoot == null) { rules.Add(rule); continue; }

                    // 1. IfcEntityFacet
                    foreach (var facet in reqsRoot.Elements(ns + "entity"))
                    {
                        rule.Requirements.Add(new IdsRequirement
                        {
                            Type     = "entity",
                            Name     = facet.Element(ns + "name")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "name")?.Value ?? "",
                            Value    = facet.Element(ns + "predefinedType")?.Element(ns + "simpleValue")?.Value ?? "",
                            Required = (facet.Attribute("minOccurs")?.Value ?? "1") != "0",
                            Severity = (facet.Attribute("minOccurs")?.Value ?? "1") == "0" ? "WARNING" : "ERROR"
                        });
                    }

                    // 2. IfcAttributeFacet
                    foreach (var facet in reqsRoot.Elements(ns + "attribute"))
                    {
                        rule.Requirements.Add(new IdsRequirement
                        {
                            Type     = "attribute",
                            Name     = facet.Element(ns + "name")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "name")?.Value ?? "",
                            Value    = facet.Element(ns + "value")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "value")?.Value ?? "",
                            Required = (facet.Attribute("minOccurs")?.Value ?? "1") != "0",
                            Severity = (facet.Attribute("minOccurs")?.Value ?? "1") == "0" ? "WARNING" : "ERROR"
                        });
                    }

                    // 3. IfcPropertyFacet
                    foreach (var facet in reqsRoot.Elements(ns + "property"))
                    {
                        var psetName = facet.Element(ns + "propertySetName")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "propertySetName")?.Value ?? "";
                        var propName = facet.Element(ns + "baseName")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "baseName")?.Value
                                       ?? facet.Element(ns + "name")?.Value ?? "";
                        var propVal  = facet.Element(ns + "value")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "value")?.Value ?? "";

                        rule.Requirements.Add(new IdsRequirement
                        {
                            Type        = "property",
                            PropertySet = psetName,
                            Name        = propName,
                            Value       = propVal,
                            DataType    = facet.Element(ns + "dataType")?.Value ?? "",
                            Required    = (facet.Attribute("minOccurs")?.Value ?? "1") != "0",
                            Severity    = (facet.Attribute("minOccurs")?.Value ?? "1") == "0" ? "WARNING" : "ERROR"
                        });
                    }

                    // 4. IfcMaterialFacet
                    foreach (var facet in reqsRoot.Elements(ns + "material"))
                    {
                        rule.Requirements.Add(new IdsRequirement
                        {
                            Type     = "material",
                            Name     = "Material",
                            Value    = facet.Element(ns + "value")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "value")?.Value ?? "",
                            Required = (facet.Attribute("minOccurs")?.Value ?? "1") != "0",
                            Severity = (facet.Attribute("minOccurs")?.Value ?? "1") == "0" ? "WARNING" : "ERROR"
                        });
                    }

                    // 5. IfcClassificationFacet
                    foreach (var facet in reqsRoot.Elements(ns + "classification"))
                    {
                        rule.Requirements.Add(new IdsRequirement
                        {
                            Type     = "classification",
                            Name     = facet.Element(ns + "system")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "system")?.Value ?? "",
                            Value    = facet.Element(ns + "value")?.Element(ns + "simpleValue")?.Value
                                       ?? facet.Element(ns + "value")?.Value ?? "",
                            Required = (facet.Attribute("minOccurs")?.Value ?? "1") != "0",
                            Severity = (facet.Attribute("minOccurs")?.Value ?? "1") == "0" ? "WARNING" : "ERROR"
                        });
                    }

                    rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"IDS parse hatası: {ex.Message}", ex);
            }

            return rules;
        }
    }


    // ── QA Rule Engine ────────────────────────────────────────────────────────

    public static class QaRuleEngine
    {
        /// <summary>
        /// JSON tabanlı QA kurallarını eleman listesine uygular.
        /// Kural formatı: { "rule_id", "category", "param_name", "operator", "value", "severity" }
        /// </summary>
        public static ValidationReport RunQaRules(
            List<Dictionary<string, object?>> elements,
            List<Dictionary<string, object?>> rules,
            string reportTitle = "QA Doğrulama")
        {
            var report = new ValidationReport { ManifestTitle = reportTitle };

            foreach (var rule in rules)
            {
                var ruleId    = rule.TryGetValue("rule_id",    out var ri) ? ri?.ToString() ?? "" : "";
                var category  = rule.TryGetValue("category",   out var ca) ? ca?.ToString() ?? "" : "";
                var paramName = rule.TryGetValue("param_name", out var pn) ? pn?.ToString() ?? "" : "";
                var op        = rule.TryGetValue("operator",   out var op2)? op2?.ToString() ?? "exists" : "exists";
                var expected  = rule.TryGetValue("value",      out var vl) ? vl?.ToString() ?? "" : "";
                var severity  = rule.TryGetValue("severity",   out var sv) ? sv?.ToString() ?? "ERROR" : "ERROR";

                var targets = string.IsNullOrEmpty(category)
                    ? elements
                    : elements.Where(e =>
                        e.TryGetValue("kategori", out var k) &&
                        k?.ToString()?.Contains(category, StringComparison.OrdinalIgnoreCase) == true
                    ).ToList();

                foreach (var el in targets)
                {
                    var elId = el.TryGetValue("element_id", out var eid) ? eid?.ToString() ?? "" : "";
                    var elCat = el.TryGetValue("kategori", out var ec) ? ec?.ToString() ?? "" : "";
                    var actual = el.TryGetValue(paramName, out var av) ? av?.ToString() : null;

                    bool passed = op.ToLower() switch
                    {
                        "exists"     => actual != null,
                        "not_empty"  => !string.IsNullOrWhiteSpace(actual),
                        "eq"         => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                        "ne"         => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                        "contains"   => actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true,
                        "starts_with"=> actual?.StartsWith(expected, StringComparison.OrdinalIgnoreCase) == true,
                        "gt" when double.TryParse(actual, out var a) && double.TryParse(expected, out var e2) => a > e2,
                        "lt" when double.TryParse(actual, out var a) && double.TryParse(expected, out var e2) => a < e2,
                        _ => actual != null
                    };

                    report.Results.Add(new ValidationResult
                    {
                        RuleId    = ruleId,
                        ElementId = elId,
                        Category  = elCat,
                        CheckType = $"{paramName} {op} {expected}",
                        Passed    = passed,
                        Severity  = severity,
                        Message   = passed
                            ? $"OK: {paramName}={actual}"
                            : $"HATA: {paramName} beklenen={expected}, gerçek={actual ?? "null"}"
                    });
                }
            }

            report.TotalChecks = report.Results.Count;
            report.Passed      = report.Results.Count(r => r.Passed);
            report.Failed      = report.Results.Count(r => !r.Passed && r.Severity == "ERROR");
            report.Warnings    = report.Results.Count(r => !r.Passed && r.Severity == "WARNING");

            return report;
        }
    }
}
