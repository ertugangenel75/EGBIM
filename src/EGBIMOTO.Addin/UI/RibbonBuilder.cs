// ============================================================
// EGBIMOTO — RibbonBuilder  (v11.1)
// Manifest JSON'larından dinamik Revit ribbon inşa eder.
//
// Düzeltmeler:
//   SORUN 1 — JournalData çelişkisi (v11.1'de GERÇEKTEN çözüldü):
//     v10.7'de ManifestButtonRegistry.Register(btnName, path) doğru
//     çağrılıyordu ama ManifestRibbonCommand.Execute() btnName'i hâlâ
//     JournalData'dan okumaya çalışıyordu (normal kullanımda hep boş
//     gelir) — bu yüzden TÜM manifest butonları "bulunamadı" hatası
//     veriyordu. v11.1'de her buton kendi ayrı, sabit slot'lu komut
//     sınıfını kullanıyor (bkz. ManifestRibbonCommandSlots.g.cs,
//     ManifestRibbonCommandBase.cs, ManifestButtonRegistry.Allocate).
//     JournalData'ya artık hiç ihtiyaç yok.
//
//   SORUN 2 — Config'den SplitButton label = ham manifest ID:
//     RibbonGroupConfig'e DisplayName alanı eklendi.
//     Manifest listesi artık { id, display_name } nesnesi olarak
//     ribbon_config.json'a yazılabilir; geriye dönük uyumluluk
//     için düz string de kabul edilir (display_name = id).
//
// ribbon_config.json yeni formatı:
// {
//   "panels": [{
//     "name": "MEP Hesap",
//     "groups": [{
//       "label": "Sıhhi",
//       "icon": "sihhi.png",
//       "manifests": [
//         { "id": "pl16_tam_sihhi_sistem_hesap", "display_name": "Tam Sıhhi Hesap" },
//         { "id": "pl11_gunluk_su_talebi_depo",  "display_name": "Su Talebi / Depo" }
//       ]
//     }]
//   }]
// }
// Düz string de çalışır (geriye dönük): "manifests": ["pl16_tam_sihhi_sistem_hesap"]
//
// Revit kısıtları:
//   • SplitButton: dropdown ile birden fazla manifest
//   • PushButton: tek manifest
//   • Her buton için benzersiz 'name' gerekli
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.UI;
using EGBIMOTO.Addin.Commands;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Addin.UI
{
    public static class RibbonBuilder
    {
        private const string TAB_NAME    = "EGBIMOTO";
        private const string CONFIG_FILE = "ribbon_config.json";
        private const string DEFAULT_ICON= "egbimoto_icon.png";

        public static void Build(UIControlledApplication app, string addinDir)
        {
            var asm     = Assembly.GetExecutingAssembly().Location;
            var iconDir = Path.Combine(addinDir, "Resources", "Icons");
            var config  = LoadConfig(addinDir);

            // Sabit paneller
            BuildStaticPanels(app, asm, iconDir);

            // Dinamik paneller
            if (config != null)
                BuildDynamicPanels(app, asm, iconDir, addinDir, config);
            else
                BuildDefaultDynamicPanels(app, asm, iconDir, addinDir);
        }

        // ── Sabit Paneller ────────────────────────────────────────────────────

        private static void BuildStaticPanels(
            UIControlledApplication app, string asm, string iconDir)
        {
            var pBimVeri = GetOrCreatePanel(app, "BIM Veri");
            AddPush(pBimVeri, asm, "EG_IFC",   "IFC\nDışa Aktar", "EGBIMOTO.Addin.Commands.IfcExportCommand",
                "Modeli IFC formatında dışa aktarır.", iconDir, "ifc.png");
            AddPush(pBimVeri, asm, "EG_IDS",   "IDS\nDoğrula",    "EGBIMOTO.Addin.Commands.IdsValidateCommand",
                "IDS kurallarına göre model doğrular.", iconDir, "ids.png");
            AddPush(pBimVeri, asm, "EG_PARAM", "Parametre\nEkle", "EGBIMOTO.Addin.Commands.ParamAddCommand",
                "EGBIM paylaşımlı parametrelerini modele ekler.", iconDir, "param.png");

            var pHesap = GetOrCreatePanel(app, "Hesap");
            AddPush(pHesap, asm, "EG_POZ",   "Poz\nEşle",      "EGBIMOTO.Addin.Commands.PozMatchCommand",
                "Elemanları ÇŞB 2026 poz kodlarıyla eşleştirir.", iconDir, "poz.png");
            AddPush(pHesap, asm, "EG_COST",  "Maliyet\nHesap", "EGBIMOTO.Addin.Commands.CostCalcCommand",
                "Seçili elemanlara göre maliyet hesabı yapar.", iconDir, "cost.png");
            AddPush(pHesap, asm, "EG_KALIP", "Kalıp\nKontrol", "EGBIMOTO.Addin.Commands.KalipControlCommand",
                "Kalıp yüzey metrajı hesaplar.", iconDir, "kalip.png");

            var pOto = GetOrCreatePanel(app, "Otomasyon");
            AddPush(pOto, asm, "EG_BROWSER", "Manifest\nBrowser", "EGBIMOTO.Addin.Commands.ManifestBrowserCommand",
                "Tüm manifest iş akışlarını listeler ve çalıştırır.", iconDir, "browser.png");
            AddPush(pOto, asm, "EG_INSPECTOR", "Eleman\nİncele", "EGBIMOTO.Addin.Commands.ToggleElementInspectorCommand",
                "Seçili elemanın TR_BIM parametre sağlığını ve ilgili manifestleri gösteren paneli aç/kapat.",
                iconDir, "inspector.png");
            AddPush(pOto, asm, "EG_FAMLIB", "Aile\nKütüphanesi", "EGBIMOTO.Addin.Commands.FamilyLibraryCommand",
                "Bir klasördeki aileleri tarar, TR_/EG_ paylaşımlı parametre GUID uyumluluğunu denetler.",
                iconDir, "family.png");
            // v11: MCP butonu — AvailabilityClassName ile ribbon state yansıması
            {
                var mcpData = new PushButtonData("EG_MCP", "MCP\nServer", asm,
                    "EGBIMOTO.Addin.Commands.McpServerToggleCommand")
                {
                    ToolTip               = "EGBIMOTO MCP Server'ı başlatır/durdurur (port 5577).",
                    AvailabilityClassName = "EGBIMOTO.Addin.Commands.McpServerAvailability",
                };
                ApplyIcons(mcpData, iconDir, "mcp.png");
                try { pOto.AddItem(mcpData); } catch { }
            }
        }

        // ── Varsayılan Dinamik Paneller (config yoksa) ────────────────────────

        private static void BuildDefaultDynamicPanels(
            UIControlledApplication app, string asm, string iconDir, string addinDir)
        {
            var manifestsDir = Path.Combine(addinDir, "manifests");

            var pSik = GetOrCreatePanel(app, "Sık Kullanılan");
            var sikManifests = new (string id, string label, string tooltip, string icon)[]
            {
                ("pl16_tam_sihhi_sistem_hesap",  "Sıhhi\nTam",    "Tam sıhhi tesisat hesabı (6 adım)",   "sihhi.png"),
                ("fp17_tam_yangin_koruma_hesap", "Yangın\nTam",   "Tam yangın koruma hesabı (13 adım)",  "yangin.png"),
                ("el16_tam_elektrik_hesap",      "Elektrik\nTam", "Tam elektrik hesabı (12 adım)",       "elektrik.png"),
                ("me17_tam_mek_hesap",           "Mekanik\nTam",  "Tam mekanik HVAC hesabı (10 adım)",   "mekanik.png"),
                ("s15_tam_yapisal_hesap",        "Yapısal\nTam",  "Tam yapısal hesap (10 adım)",         "yapisal.png"),
                ("mep05_tam_bosluk_workflow",    "MEP\nBoşluk",   "MEP boşluk tam workflow",              "mep.png"),
            };
            foreach (var (id, label, tooltip, icon) in sikManifests)
                AddManifestPush(pSik, asm, manifestsDir, id, label, tooltip, iconDir, icon);

            var pMep = GetOrCreatePanel(app, "MEP Hesap");
            AddManifestSplit(pMep, asm, manifestsDir, "MEP_SIHHI", "Sıhhi", iconDir, "sihhi.png", new[]
            {
                ("pl16_tam_sihhi_sistem_hesap", "Tam Sıhhi Hesap"),
                ("pl11_gunluk_su_talebi_depo",  "Su Talebi / Depo"),
                ("pl12_pompa_hp_pik_talep",      "Pompa HP"),
                ("pl13_yuksek_bina_basinc_zonu", "Basınç Zonu"),
                ("pl14_boru_hiz_kontrol",         "Boru Hız"),
            });
            AddManifestSplit(pMep, asm, manifestsDir, "MEP_ELEKTRIK", "Elektrik", iconDir, "elektrik.png", new[]
            {
                ("el16_tam_elektrik_hesap",   "Tam Elektrik"),
                ("el11_gerilim_dusumu_hesap", "Gerilim Düşümü"),
                ("el12_panel_guc_analiz",      "Panel Güç"),
                ("el15_yedek_guc_sistemi",     "Jeneratör/UPS"),
                ("el14_kablo_tava_qa",         "Kablo Tava QA"),
            });
            AddManifestSplit(pMep, asm, manifestsDir, "MEP_YANGIN", "Yangın", iconDir, "yangin.png", new[]
            {
                ("fp17_tam_yangin_koruma_hesap", "Tam Yangın"),
                ("fp12_standpipe_sistem",         "Standpipe"),
                ("fp13_yangin_pompasi",            "Pompa"),
                ("fp14_sprinkler_hidrolik",        "Sprinkler"),
                ("fp16_tahliye_mimari_qa",         "Tahliye QA"),
            });
            AddManifestSplit(pMep, asm, manifestsDir, "MEP_MEKANIK", "Mekanik", iconDir, "mekanik.png", new[]
            {
                ("me17_tam_mek_hesap",   "Tam Mekanik"),
                ("me12_isi_yuku_ahu",     "Isı Yükü/AHU"),
                ("me14_basinc_hepa_qa",  "Basınç/HEPA"),
                ("me15_ach_zon_denge",   "ACH/Zon"),
                ("me16_chiller_cop",     "Chiller COP"),
            });
            AddManifestSplit(pMep, asm, manifestsDir, "MEP_BOSLUK", "MEP Boşluk", iconDir, "mep.png", new[]
            {
                ("mep05_tam_bosluk_workflow", "Tam Workflow"),
                ("mep01_bosluk_tespit",        "Boşluk Tespit"),
                ("mep02_bosluk_validate_ec2",  "EC-2 Doğrulama"),
                ("mep03_bcf_export",            "BCF İhraç"),
                ("mep04_lento_yer",             "Lento Ekle"),
            });

            var pYap = GetOrCreatePanel(app, "Yapısal");
            AddManifestSplit(pYap, asm, manifestsDir, "YAP_DONATI", "Donatı", iconDir, "donati.png", new[]
            {
                ("s11_donati_bindirme_ankraj", "Bindirme/Ankraj"),
                ("s12_beton_sinifi_kesit_qa",  "Beton/Kiriş/Perde"),
            });
            AddManifestSplit(pYap, asm, manifestsDir, "YAP_TEMEL", "Döşeme/Temel", iconDir, "temel.png", new[]
            {
                ("s13_doseme_temel_hesap", "Döşeme/Temel"),
                ("s14_celik_bulon_kalip",  "Bulon/Kalıp"),
                ("s15_tam_yapisal_hesap",  "Tam Yapısal"),
            });

            var pQa = GetOrCreatePanel(app, "QA/Rapor");
            AddManifestSplit(pQa, asm, manifestsDir, "QA_MODEL", "Model QA", iconDir, "qa.png", new[]
            {
                ("model_sagligi",       "Model Sağlığı"),
                ("eg_model_audit",      "Model Audit"),
                ("m06_model_uyarilari", "Uyarı Raporu"),
            });
            AddManifestSplit(pQa, asm, manifestsDir, "QA_TESLIM", "Teslim", iconDir, "teslim.png", new[]
            {
                ("11_csb2026_teslim_kontrol", "ÇŞB 2026 Teslim"),
                ("pm07_teslim_kontrol",         "BIM Teslim"),
                ("ifc_readiness_qa",            "IFC Hazırlık"),
            });
        }

        // ── Config Dosyasından Dinamik Paneller ───────────────────────────────

        private static void BuildDynamicPanels(
            UIControlledApplication app, string asm, string iconDir,
            string addinDir, RibbonConfig config)
        {
            var manifestsDir = Path.Combine(addinDir, "manifests");

            foreach (var panel in config.Panels ?? new())
            {
                var rPanel = GetOrCreatePanel(app, panel.Name ?? "Panel");
                foreach (var grp in panel.Groups ?? new())
                {
                    var entries = grp.ManifestEntries ?? new();
                    if (entries.Count == 0) continue;

                    if (entries.Count == 1)
                    {
                        var e = entries[0];
                        AddManifestPush(rPanel, asm, manifestsDir,
                            e.Id, e.DisplayName ?? e.Id,
                            grp.Tooltip ?? "", iconDir, grp.Icon ?? DEFAULT_ICON);
                    }
                    else
                    {
                        var splitName = $"SPLIT_{panel.Name}_{grp.Label}"
                                            .Replace(" ", "_")
                                            .Replace("\n", "_");
                        AddManifestSplit(rPanel, asm, manifestsDir,
                            splitName, grp.Label ?? "Grup", iconDir, grp.Icon ?? DEFAULT_ICON,
                            entries.Select(e => (e.Id, e.DisplayName ?? e.Id)).ToArray());
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tek manifest → PushButton.
        ///
        /// FIX v11.1 (gerçek çözüm): JournalData yerine, bu butona özel
        /// AYRI bir komut sınıfı (slot) ManifestButtonRegistry.Allocate()
        /// ile ayrılır ve PushButtonData'nın className'i o slot'a
        /// ayarlanır — Execute() artık "hangi buton" sorusunu hiç sormaya
        /// gerek kalmadan kendi sabit slot'undan cevaplar.
        /// </summary>
        public static void AddManifestPush(
            RibbonPanel panel,
            string      assemblyPath,
            string      manifestsDir,
            string      manifestId,
            string      label,
            string      tooltip,
            string      iconDir,
            string      iconFile = "")
        {
            var btnName      = $"EGBIM_{manifestId.Replace("-","_")}";
            var manifestPath = ResolveManifestPath(manifestsDir, manifestId);

            // ─── FIX: JournalData değil, slot bazlı özel komut sınıfı ───────
            var slotClassName = ManifestButtonRegistry.Allocate(manifestPath, btnName);

            var data = new PushButtonData(btnName, label, assemblyPath, slotClassName)
            {
                ToolTip            = tooltip,
                LongDescription = $"Manifest: {manifestId}",
            };
            ApplyIcons(data, iconDir, iconFile);

            try
            {
                panel.AddItem(data);
            }
            catch { /* duplicate buton — atla (slot yine de ayrıldı, zararsız) */ }
        }

        /// <summary>
        /// Birden fazla manifest → SplitButton.
        ///
        /// FIX v11.1: Her PushButton için ayrı slot (ManifestButtonRegistry.Allocate)
        /// ayrılır — JournalData'ya bağımlılık tamamen kaldırıldı.
        /// </summary>
        public static void AddManifestSplit(
            RibbonPanel              panel,
            string                   assemblyPath,
            string                   manifestsDir,
            string                   splitName,
            string                   label,
            string                   iconDir,
            string                   iconFile,
            (string id, string displayName)[] manifests)
        {
            if (manifests.Length == 0) return;

            var splitData = new SplitButtonData($"SPLIT_{splitName}", label);

            var pushDatas = manifests.Select(m =>
            {
                var btnName      = $"EGBIM_{m.id.Replace("-","_")}";
                var manifestPath = ResolveManifestPath(manifestsDir, m.id);

                // ─── FIX: JournalData değil, slot bazlı özel komut sınıfı ───
                var slotClassName = ManifestButtonRegistry.Allocate(manifestPath, btnName);

                var pd = new PushButtonData(
                    btnName,
                    m.displayName,   // FIX Sorun 2: ham ID değil, okunabilir ad
                    assemblyPath,
                    slotClassName)
                {
                    ToolTip            = m.displayName,
                    LongDescription = $"Manifest: {m.id}",
                };
                ApplyIcons(pd, iconDir, iconFile);
                return pd;
            }).ToList();

            try
            {
                var split = panel.AddItem(splitData) as SplitButton;
                if (split == null) return;

                foreach (var pd in pushDatas)
                {
                    try { split.AddPushButton(pd); }
                    catch { /* duplicate — atla (slot yine de ayrıldı, zararsız) */ }
                }
            }
            catch { /* panel full — atla */ }
        }

        /// <summary>
        /// Manifest ID'yi dosya yoluna çevirir.
        /// Önce tam yolu arar, yoksa manifests/ altını recursive tarar.
        /// </summary>
        private static string ResolveManifestPath(string manifestsDir, string manifestId)
        {
            // 1. Doğrudan alt klasörde ara (bilinen pattern: kategori/id.json)
            if (Directory.Exists(manifestsDir))
            {
                foreach (var f in Directory.EnumerateFiles(
                    manifestsDir, $"{manifestId}.json", SearchOption.AllDirectories))
                    return f;
            }
            // 2. Bulunamazsa placeholder — ManifestLoader yüklemede uyarı verir
            return Path.Combine(manifestsDir, $"{manifestId}.json");
        }

        // Statik IExternalCommand türleri için klasik AddButton
        internal static void AddPush(
            RibbonPanel panel, string asm, string name, string text,
            string commandTypeName, string tooltip, string iconDir, string iconFile = "")
        {
            var data = new PushButtonData(name, text, asm, commandTypeName)
            {
                ToolTip = tooltip,
            };
            ApplyIcons(data, iconDir, iconFile);
            try { panel.AddItem(data); } catch { }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string name)
        {
            try { return app.CreateRibbonPanel(TAB_NAME, name); }
            catch
            {
                return app.GetRibbonPanels(TAB_NAME)
                          .FirstOrDefault(p => p.Name == name)
                       ?? app.CreateRibbonPanel(TAB_NAME, name + "_");
            }
        }

        private static System.Windows.Media.ImageSource? LoadIcon(string iconDir, string iconFile)
        {
            if (string.IsNullOrEmpty(iconFile)) return null;
            try
            {
                var path = Path.Combine(iconDir, iconFile);
                if (!File.Exists(path)) return null;
                var img = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                img.UriSource   = new Uri(path);
                img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.EndInit();
                return img;
            }
            catch { return null; }
        }

        /// <summary>
        /// v12.1: LargeImage (32px, "ad.png") + Image (16px, "ad_16.png") birlikte
        /// atanır; ikon yoksa DEFAULT_ICON'a düşer. Küçük varyant bulunamazsa
        /// yalnızca LargeImage set edilir (Revit kendisi ölçekler).
        /// </summary>
        private static void ApplyIcons(ButtonData data, string iconDir, string iconFile)
        {
            var large = LoadIcon(iconDir, iconFile) ?? LoadIcon(iconDir, DEFAULT_ICON);
            if (large != null) data.LargeImage = large;

            if (!string.IsNullOrEmpty(iconFile))
            {
                var smallName = Path.GetFileNameWithoutExtension(iconFile) + "_16.png";
                var small = LoadIcon(iconDir, smallName);
                if (small != null) data.Image = small;
            }
        }

        private static RibbonConfig? LoadConfig(string addinDir)
        {
            var cfgPath = Path.Combine(addinDir, CONFIG_FILE);
            if (!File.Exists(cfgPath)) return null;
            try
            {
                var json = File.ReadAllText(cfgPath, System.Text.Encoding.UTF8);
                return JsonSerializer.Deserialize<RibbonConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { return null; }
        }
    }

    // ── Config Modeli ─────────────────────────────────────────────────────────

    public sealed class RibbonConfig
    {
        public List<RibbonPanelConfig>? Panels { get; set; }
    }

    public sealed class RibbonPanelConfig
    {
        public string?                  Name   { get; set; }
        public List<RibbonGroupConfig>? Groups { get; set; }
    }

    public sealed class RibbonGroupConfig
    {
        public string? Label   { get; set; }
        public string? Tooltip { get; set; }
        public string? Icon    { get; set; }

        /// <summary>
        /// FIX Sorun 2: Manifests artık { id, display_name } nesnesi.
        /// Geriye dönük uyumluluk: düz string de kabul edilir.
        /// JSON deserializer için özel converter kullanılır.
        /// </summary>
        [JsonConverter(typeof(ManifestEntryListConverter))]
        public List<ManifestEntry>? Manifests { get; set; }

        // Kod içinden kullanım için helper
        public List<ManifestEntry> ManifestEntries => Manifests ?? new();
    }

    /// <summary>
    /// Manifest listesi için tek birim.
    /// JSON'da hem düz string hem nesne kabul edilir:
    ///   "pl16_tam_sihhi_sistem_hesap"
    ///   { "id": "pl16_tam_sihhi_sistem_hesap", "display_name": "Tam Sıhhi Hesap" }
    /// </summary>
    public sealed class ManifestEntry
    {
        public string  Id          { get; set; } = "";
        public string? DisplayName { get; set; }
    }

    /// <summary>
    /// Karma JSON dizisini (string | object) ManifestEntry listesine çevirir.
    /// </summary>
    public sealed class ManifestEntryListConverter : JsonConverter<List<ManifestEntry>>
    {
        public override List<ManifestEntry> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var list = new List<ManifestEntry>();
            if (reader.TokenType != JsonTokenType.StartArray)
                return list;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    // Düz string: "pl16_tam_sihhi"
                    list.Add(new ManifestEntry { Id = reader.GetString() ?? "" });
                }
                else if (reader.TokenType == JsonTokenType.StartObject)
                {
                    // Nesne: { "id": "...", "display_name": "..." }
                    var entry = new ManifestEntry();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType != JsonTokenType.PropertyName) continue;
                        var propName = reader.GetString()?.ToLowerInvariant();
                        reader.Read();
                        switch (propName)
                        {
                            case "id":           entry.Id          = reader.GetString() ?? ""; break;
                            case "display_name": entry.DisplayName = reader.GetString();        break;
                            default: reader.Skip(); break;
                        }
                    }
                    list.Add(entry);
                }
                else
                {
                    reader.Skip();
                }
            }
            return list;
        }

        public override void Write(
            Utf8JsonWriter writer,
            List<ManifestEntry> value,
            JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var e in value)
            {
                if (string.IsNullOrEmpty(e.DisplayName))
                    writer.WriteStringValue(e.Id);
                else
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", e.Id);
                    writer.WriteString("display_name", e.DisplayName);
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();
        }
    }
}
