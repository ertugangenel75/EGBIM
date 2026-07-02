using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Core.AI
{
    /// <summary>
    /// API key olmadan çalışan deterministik manifest üretici.
    /// Anahtar kelime analizi → en uygun şablonu bul → kişiselleştir.
    /// Güven skoru: %65-85 (basit işlerde %85, karmaşıkta %55).
    /// </summary>
    public sealed class PatternEngine
    {
        private readonly string            _manifestsRoot;
        private readonly ManifestValidator _validator;

        public PatternEngine(string manifestsRoot, string contractsPath)
        {
            _manifestsRoot = manifestsRoot;
            _validator     = new ManifestValidator(contractsPath);
        }

        public PatternEngineResult Generate(string userText)
        {
            var ctx      = Analyze(userText);
            var template = FindBestTemplate(ctx);

            EgManifest manifest;
            string     templateFile;

            if (template is not null)
            {
                manifest     = PersonalizeTemplate(template.Manifest, ctx, userText);
                templateFile = template.File;
            }
            else
            {
                manifest     = BuildFromScratch(ctx, userText);
                templateFile = "(sıfırdan)";
            }

            var json = FormatJson(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            var vr   = _validator.Validate(json);

            return new PatternEngineResult
            {
                Success      = true,
                RawJson      = json,
                Manifest     = vr.Manifest ?? manifest,
                Confidence   = template is not null ? ctx.Confidence : Math.Max(ctx.Confidence - 15, 40),
                PatternUsed  = ctx.PatternType,
                TemplateFile = templateFile,
                Warnings     = vr.Warnings
            };
        }

        // ── Analiz ───────────────────────────────────────────────────────────

        private ManifestContext Analyze(string text)
        {
            var t   = text.ToLowerInvariant();
            var ctx = new ManifestContext { OriginalText = text };

            if (Has(t,"duvar","wall"))              { ctx.CollectOps.Add("collect_walls");        ctx.Categories.Add("OST_Walls"); }
            if (Has(t,"kolon","column"))             { ctx.CollectOps.Add("collect_columns");      ctx.Categories.Add("OST_StructuralColumns"); }
            if (Has(t,"kiriş","kiris","beam"))       { ctx.CollectOps.Add("collect_beams");        ctx.Categories.Add("OST_StructuralFraming"); }
            if (Has(t,"döşeme","doseme","floor"))    { ctx.CollectOps.Add("collect_floors");       ctx.Categories.Add("OST_Floors"); }
            if (Has(t,"temel","foundation"))         { ctx.CollectOps.Add("collect_foundations");  ctx.Categories.Add("OST_StructuralFoundation"); }
            if (Has(t,"oda","room"))                 { ctx.CollectOps.Add("collect_rooms");        ctx.Categories.Add("OST_Rooms"); }
            if (Has(t,"kapı","kapi","door"))          { ctx.CollectOps.Add("collect_doors");        ctx.Categories.Add("OST_Doors"); }
            if (Has(t,"pencere","window"))            { ctx.CollectOps.Add("collect_windows");      ctx.Categories.Add("OST_Windows"); }
            if (Has(t,"boru","pipe","sıhhi","sihhi")) { ctx.CollectOps.Add("collect_pipes");        ctx.Categories.Add("OST_PipeCurves"); }
            if (Has(t,"kanal","duct","havalandırma")) { ctx.CollectOps.Add("collect_ducts");        ctx.Categories.Add("OST_DuctCurves"); }
            if (Has(t,"kablo tava","cable tray"))    { ctx.CollectOps.Add("collect_cable_trays");  ctx.Categories.Add("OST_CableTray"); }
            if (Has(t,"sprinkler"))                   { ctx.CollectOps.Add("collect_sprinklers");   ctx.Categories.Add("OST_Sprinklers"); }
            if (Has(t,"donatı","donati","rebar"))     { ctx.CollectOps.Add("collect_rebar");        ctx.Categories.Add("OST_Rebar"); }
            if (Has(t,"yangın alarm","yangin alarm","dedektör")) { ctx.CollectOps.Add("collect_fire_alarm_devices"); ctx.Categories.Add("OST_FireAlarmDevices"); }

            if (ctx.CollectOps.Count == 0 && Has(t,"yapısal","yapisal","structural"))
            { ctx.CollectOps.AddRange(new[]{"collect_walls","collect_columns","collect_beams","collect_floors"}); ctx.Categories.AddRange(new[]{"OST_Walls","OST_StructuralColumns","OST_StructuralFraming","OST_Floors"}); }
            if (ctx.CollectOps.Count == 0 && Has(t,"mep"))
            { ctx.CollectOps.AddRange(new[]{"collect_ducts","collect_pipes","collect_cable_trays"}); ctx.Categories.AddRange(new[]{"OST_DuctCurves","OST_PipeCurves","OST_CableTray"}); }

            ctx.PatternType = Has(t,"kalıp","kalip","cofr")  ? "BOQ_KALIP"
                : Has(t,"maliyet","cost","bütçe","poz")      ? "BOQ"
                : Has(t,"metraj","alan","hacim","sayım")      ? "METRAJ"
                : Has(t,"parametre","param","qa","kontrol")   ? "QA"
                : Has(t,"ts500","tbdy","bindirme","ankraj")   ? "CALC"
                : Has(t,"ifc","ids","export")                 ? "IFC"
                : Has(t,"boru","pipe","kanal","duct","mep")   ? "MEP"
                : Has(t,"yangın","yangin","sprinkler")        ? "YANGIN"
                : Has(t,"elektrik","aydınlatma","panel")      ? "ELEKTRIK"
                : Has(t,"wbs","iş kırılım")                   ? "WBS"
                : "GENEL";

            ctx.NeedsPoz     = Has(t,"poz","maliyet","cost");
            ctx.NeedsCost    = Has(t,"maliyet","cost","fiyat","tutar");
            ctx.GroupByLevel = Has(t,"kat","level","bazlı");
            ctx.GroupByType  = Has(t,"tip","type","sistem");

            int conf = 50;
            if (ctx.CollectOps.Count > 0)   conf += 15;
            if (ctx.PatternType != "GENEL") conf += 10;
            if (ctx.GroupByLevel || ctx.GroupByType) conf += 5;
            if (ctx.NeedsPoz)   conf += 5;
            ctx.Confidence = Math.Min(conf, 85);
            return ctx;
        }

        // ── Şablon arama ─────────────────────────────────────────────────────

        private TemplateInfo? FindBestTemplate(ManifestContext ctx)
        {
            var folders = ctx.PatternType switch
            {
                "BOQ_KALIP" => new[]{"kalip","maliyet","metraj"},
                "BOQ"       => new[]{"maliyet","wbs","metraj"},
                "METRAJ"    => new[]{"metraj","maliyet","raporlama"},
                "QA"        => new[]{"dogrulama","qa","raporlama"},
                "CALC"      => new[]{"yapisal","yapisal_v4","dogrulama"},
                "IFC"       => new[]{"ifc","ids","etl"},
                "MEP"       => new[]{"mep","mekanik","sihhi_tesisat"},
                "YANGIN"    => new[]{"yangin","koordinasyon","mep"},
                "ELEKTRIK"  => new[]{"elektrik","mep"},
                "WBS"       => new[]{"wbs","maliyet","proje_yonetimi"},
                _           => new[]{"raporlama","metraj","dogrulama"}
            };

            foreach (var folder in folders)
            {
                var dir = Path.Combine(_manifestsRoot, folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var fpath in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
                {
                    try
                    {
                        var json = File.ReadAllText(fpath, Encoding.UTF8);
                        var m    = JsonSerializer.Deserialize<EgManifest>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (m?.Steps?.Count >= 4)
                            return new TemplateInfo { File = fpath, Manifest = m, RawJson = json };
                    }
                    catch { }
                }
            }
            return null;
        }

        // ── Kişiselleştir ────────────────────────────────────────────────────

        private EgManifest PersonalizeTemplate(EgManifest tpl, ManifestContext ctx, string userText)
        {
            var json = JsonSerializer.Serialize(tpl, new JsonSerializerOptions { WriteIndented = false });
            var m    = JsonSerializer.Deserialize<EgManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            m.Title       = GenerateTitle(ctx);
            m.Description = userText.Length > 100 ? userText[..100] + "..." : userText;

            if (ctx.CollectOps.Count > 0)
            {
                var collectSteps = m.Steps.Where(s => s.Op?.StartsWith("collect_") == true).ToList();
                for (int i = 0; i < Math.Min(ctx.CollectOps.Count, collectSteps.Count); i++)
                    collectSteps[i].Op = ctx.CollectOps[i];
            }
            return m;
        }

        // ── Sıfırdan üret ────────────────────────────────────────────────────

        private EgManifest BuildFromScratch(ManifestContext ctx, string userText)
        {
            var m = new EgManifest
            {
                Title = GenerateTitle(ctx), Description = userText,
                Category = PatternToCategory(ctx.PatternType),
                Version = "1.0.0",
                PreChecks = ctx.Categories.Count > 0 ? new() { new EgPreCheck
                {
                    Type = "MODEL_HAS_ELEMENTS", Categories = ctx.Categories.Take(3).ToList(),
                    MinCount = 1, OnFail = "ABORT", Message = "Eleman bulunamadı"
                }} : null
            };

            var steps = new List<EgStep>();
            var collectIds = new List<string>();

            if (ctx.NeedsPoz) steps.Add(new EgStep { Id = "poz_yukle", Op = "load_poz_data" });

            foreach (var (cop, i) in ctx.CollectOps.Distinct().Select((c, i) => (c, i)))
            {
                var cid = $"s{i+1:00}_{cop.Replace("collect_", "")}";
                steps.Add(new EgStep { Id = cid, Op = cop });
                collectIds.Add(cid);
            }
            if (collectIds.Count == 0) { steps.Add(new EgStep { Id = "s01_elemanlar", Op = "collect_elements" }); collectIds.Add("s01_elemanlar"); }

            var lastId = collectIds.Last();
            if (collectIds.Count > 1) { steps.Add(new EgStep { Id = "s_birlestir", Op = "merge_lists", FromMany = collectIds }); lastId = "s_birlestir"; }

            switch (ctx.PatternType)
            {
                case "BOQ_KALIP":
                    steps.Add(new EgStep { Id = "s_kalip",   Op = "kalip_all",               From = lastId });
                    steps.Add(new EgStep { Id = "s_poz",     Op = "poz_match_keynote_aware",  From = "s_kalip" });
                    if (ctx.NeedsCost) steps.Add(new EgStep { Id = "s_maliyet", Op = "calc_cost", From = "s_poz", Params = new(){{ "quantity_field","kalip_m2" }} });
                    lastId = ctx.NeedsCost ? "s_maliyet" : "s_poz";
                    break;
                case "BOQ":
                    steps.Add(new EgStep { Id = "s_poz",     Op = "poz_match",   From = lastId });
                    steps.Add(new EgStep { Id = "s_maliyet", Op = "calc_cost",   From = "s_poz" });
                    lastId = "s_maliyet";
                    break;
                case "QA":
                    var qaId = "s_qa";
                    steps.Add(new EgStep { Id = qaId, Op = "validate_required_params", From = lastId, Params = new(){{ "required_params","EGBIM_PozNo" }} });
                    steps.Add(new EgStep { Id = "s_birlesik_qa", Op = "merge_validation_reports", FromMany = new(){ qaId }, Params = new(){{ "title", m.Title }} });
                    lastId = "s_birlesik_qa";
                    break;
                case "MEP":
                    steps.Add(new EgStep { Id = "s_sistem",  Op = "mep_by_system",   From = lastId });
                    steps.Add(new EgStep { Id = "s_uzunluk", Op = "mep_total_length", From = lastId });
                    lastId = "s_sistem";
                    break;
            }

            var aggOp = ctx.GroupByType ? "group_elements_by_type" : "group_elements_by_level";
            var hasAgg = steps.Any(s => s.Op is "merge_validation_reports" or "group_elements_by_level" or "group_elements_by_type" or "mep_by_system" or "cost_summary");
            if (!hasAgg) { steps.Add(new EgStep { Id = "s_grup", Op = aggOp, From = lastId }); lastId = "s_grup"; }

            if (ctx.PatternType == "QA")
            {
                steps.Add(new EgStep { Id = "s_ozet",    Op = "validation_summary",   From = "s_birlesik_qa" });
                steps.Add(new EgStep { Id = "s_satirlar",Op = "validation_to_rows",   From = "s_birlesik_qa" });
                steps.Add(new EgStep { Id = "s_html",    Op = "export_validation_report", From = "s_birlesik_qa", Required = false, Params = new(){{ "title", m.Title }} });
            }
            else
            {
                steps.Add(new EgStep { Id = "s_tablo",   Op = "show_table", From = lastId, Params = new(){{ "title", m.Title }} });
                steps.Add(new EgStep { Id = "s_satirlar",Op = "elements_to_rows_with_params", From = collectIds.First() });
            }

            steps.Add(new EgStep { Id = "s_xlsx", Op = "export_xlsx", From = "s_satirlar", Required = false,
                Params = new(){{ "sheet_name", m.Title[..Math.Min(m.Title.Length,28)] }} });

            m.Steps = steps;
            return m;
        }

        private static string GenerateTitle(ManifestContext ctx)
        {
            var prefix = ctx.PatternType switch
            {
                "BOQ_KALIP" => "Kalıp Metraj", "BOQ" => "Maliyet", "METRAJ" => "Metraj",
                "QA" => "QA", "CALC" => "Hesap", "MEP" => "MEP",
                "YANGIN" => "Yangın", "ELEKTRIK" => "Elektrik", "WBS" => "WBS", _ => "Rapor"
            };
            var cats = ctx.CollectOps.Count > 0
                ? " — " + string.Join("+", ctx.CollectOps.Select(o => o.Replace("collect_","")).Take(2))
                : "";
            return prefix + cats;
        }

        private static string PatternToCategory(string p) => p switch
        {
            "BOQ_KALIP" => "kalip", "BOQ" => "maliyet", "METRAJ" => "metraj",
            "QA" => "dogrulama", "CALC" => "yapisal", "IFC" => "ifc",
            "MEP" => "mep", "YANGIN" => "yangin", "ELEKTRIK" => "elektrik",
            "WBS" => "wbs", _ => "genel"
        };

        private static bool Has(string text, params string[] kws)
            => kws.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

        private static string FormatJson(string json)
        {
            try { return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json), new JsonSerializerOptions { WriteIndented = true }); }
            catch { return json; }
        }

        private sealed class ManifestContext
        {
            public string       OriginalText { get; set; } = "";
            public string       PatternType  { get; set; } = "GENEL";
            public List<string> CollectOps   { get; set; } = new();
            public List<string> Categories   { get; set; } = new();
            public bool NeedsPoz, NeedsCost, GroupByLevel, GroupByType;
            public int  Confidence;
        }

        private sealed class TemplateInfo
        {
            public string File = ""; public EgManifest Manifest = new(); public string RawJson = "";
        }
    }

    public sealed class PatternEngineResult
    {
        public bool          Success      { get; init; }
        public EgManifest?   Manifest     { get; init; }
        public string?       RawJson      { get; init; }
        public int           Confidence   { get; init; }
        public string        PatternUsed  { get; init; } = "";
        public string        TemplateFile { get; init; } = "";
        public List<string>? Warnings     { get; init; }
        public string?       ErrorMessage { get; init; }
    }
}
