using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EGBIMOTO.Core.Cost
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Poz veri modelleri
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class PozRecord
    {
        public string PozNo      { get; set; } = "";
        public string Tanim      { get; set; } = "";
        public string Birim      { get; set; } = "";
        public double BirimFiyat { get; set; }
        public string Kaynak     { get; set; } = "ÇŞB 2026";
    }

    public sealed class CostLine
    {
        public string ElementId    { get; set; } = "";
        public string Kategori     { get; set; } = "";
        public string KanonikSinif { get; set; } = "";
        public string PozNo        { get; set; } = "";
        public string PozTanim     { get; set; } = "";
        public double Miktar       { get; set; }
        public string Birim        { get; set; } = "";
        public double BirimFiyat   { get; set; }
        public double ToplamFiyat  => Math.Round(Miktar * BirimFiyat, 2);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Poz Loader — JSON dosyası veya yerleşik tablo
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class PozLoader
    {
        private readonly Dictionary<string, PozRecord> _byPozNo =
            new(StringComparer.OrdinalIgnoreCase);

        // canonical_class → poz_no eşleşme tablosu
        private readonly Dictionary<string, string> _canonicalToPoz =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // Yapısal
            ["betonarme_kolon"]   = "23.001",
            ["betonarme_kiri"]    = "23.002",
            ["betonarme_duvar"]   = "23.003",
            ["doseme"]            = "23.004",
            ["temel"]             = "23.005",
            ["radye_temel"]       = "23.006",
            ["kazik_temel"]       = "23.007",
            ["surekli_temel"]     = "23.008",
            ["donati"]            = "23.050",
            // Mimari
            ["kagir_duvar"]       = "16.001",
            ["gazbeton_duvar"]    = "16.002",
            ["briket_duvar"]      = "16.003",
            ["tugla_duvar"]       = "16.004",
            ["duvar"]             = "16.010",
            ["kapi"]              = "27.001",
            ["pencere"]           = "27.002",
            ["merdiven"]          = "23.010",
            // MEP
            ["boru"]              = "42.001",
            ["havalandirma_kanali"]= "43.001",
            ["kablo_tavasi"]      = "45.001",
            // Kalıp (ayrı poz)
            ["kalip_duvar"]       = "21.001",
            ["kalip_kolon"]       = "21.002",
            ["kalip_kiri"]        = "21.003",
            ["kalip_doseme"]      = "21.004",
            ["kalip_temel"]       = "21.005",
        };

        // ── Yerleşik fiyat tablosu (ÇŞB 2026 yaklaşık değerler, TRY) ──────────
        private static readonly List<PozRecord> _builtIn = new()
        {
            new() { PozNo="23.001", Tanim="Betonarme Kolon",             Birim="m³", BirimFiyat=8500  },
            new() { PozNo="23.002", Tanim="Betonarme Kiriş",             Birim="m³", BirimFiyat=8200  },
            new() { PozNo="23.003", Tanim="Betonarme Perde Duvar",       Birim="m³", BirimFiyat=7800  },
            new() { PozNo="23.004", Tanim="Betonarme Döşeme",            Birim="m³", BirimFiyat=7200  },
            new() { PozNo="23.005", Tanim="Betonarme Temel",             Birim="m³", BirimFiyat=6800  },
            new() { PozNo="23.006", Tanim="Radye Temel",                 Birim="m³", BirimFiyat=7000  },
            new() { PozNo="23.007", Tanim="Kazık Temel",                 Birim="m",  BirimFiyat=3200  },
            new() { PozNo="23.008", Tanim="Sürekli Temel",               Birim="m³", BirimFiyat=6500  },
            new() { PozNo="23.050", Tanim="Donatı (B420C)",              Birim="kg", BirimFiyat=28    },
            new() { PozNo="16.001", Tanim="Kagir Duvar",                 Birim="m²", BirimFiyat=1200  },
            new() { PozNo="16.002", Tanim="Gazbeton Duvar",              Birim="m²", BirimFiyat=980   },
            new() { PozNo="16.003", Tanim="Briket Duvar",                Birim="m²", BirimFiyat=850   },
            new() { PozNo="16.004", Tanim="Tuğla Duvar",                 Birim="m²", BirimFiyat=900   },
            new() { PozNo="16.010", Tanim="Duvar (genel)",               Birim="m²", BirimFiyat=750   },
            new() { PozNo="27.001", Tanim="Kapı Montajı",                Birim="adet",BirimFiyat=4500 },
            new() { PozNo="27.002", Tanim="Pencere Montajı",             Birim="adet",BirimFiyat=3800 },
            new() { PozNo="23.010", Tanim="Betonarme Merdiven",          Birim="m³", BirimFiyat=9000  },
            new() { PozNo="21.001", Tanim="Kalıp — Duvar (m²)",          Birim="m²", BirimFiyat=420   },
            new() { PozNo="21.002", Tanim="Kalıp — Kolon (m²)",          Birim="m²", BirimFiyat=480   },
            new() { PozNo="21.003", Tanim="Kalıp — Kiriş (m²)",          Birim="m²", BirimFiyat=460   },
            new() { PozNo="21.004", Tanim="Kalıp — Döşeme (m²)",         Birim="m²", BirimFiyat=380   },
            new() { PozNo="21.005", Tanim="Kalıp — Temel (m²)",          Birim="m²", BirimFiyat=350   },
            new() { PozNo="42.001", Tanim="Boru (dn100)",                Birim="m",  BirimFiyat=320   },
            new() { PozNo="43.001", Tanim="Havalandırma Kanalı",         Birim="m",  BirimFiyat=580   },
            new() { PozNo="45.001", Tanim="Kablo Tavası",                Birim="m",  BirimFiyat=240   },
        };

        // ── Başlatma ───────────────────────────────────────────────────────────

        public PozLoader() => Load(null);

        public PozLoader(string? jsonPath) => Load(jsonPath);

        private void Load(string? jsonPath)
        {
            // Önce yerleşik tabloyu yükle
            foreach (var p in _builtIn) _byPozNo[p.PozNo] = p;

            // JSON dosyası varsa override et
            if (!string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath))
            {
                try
                {
                    var json   = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                    var items  = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (items is not null)
                        foreach (var item in items)
                        {
                            var poz = new PozRecord
                            {
                                PozNo      = GetStr(item, "poz_no"),
                                Tanim      = GetStr(item, "tanim"),
                                Birim      = GetStr(item, "birim"),
                                BirimFiyat = GetDbl(item, "birim_fiyat"),
                                Kaynak     = "ÇŞB 2026"
                            };
                            if (!string.IsNullOrEmpty(poz.PozNo))
                                _byPozNo[poz.PozNo] = poz;
                        }
                }
                catch { /* JSON hatası — yerleşik tabloya devam */ }
            }
        }

        // ── Sorgulama ──────────────────────────────────────────────────────────

        public PozRecord? GetByPozNo(string pozNo)
            => _byPozNo.TryGetValue(pozNo, out var p) ? p : null;

        public PozRecord? GetByCanonicalClass(string canonicalClass)
        {
            if (!_canonicalToPoz.TryGetValue(canonicalClass, out var pozNo)) return null;
            return GetByPozNo(pozNo);
        }

        public string? ResolvePozNo(string canonicalClass)
            => _canonicalToPoz.TryGetValue(canonicalClass, out var n) ? n : null;

        public IReadOnlyList<PozRecord> GetAll() => _byPozNo.Values.ToList();

        // ── Yardımcı ──────────────────────────────────────────────────────────

        private static string GetStr(Dictionary<string, object> d, string k)
        {
            if (!d.TryGetValue(k, out var v)) return "";
            return v is System.Text.Json.JsonElement je ? je.GetString() ?? "" : v?.ToString() ?? "";
        }

        private static double GetDbl(Dictionary<string, object> d, string k)
        {
            if (!d.TryGetValue(k, out var v)) return 0;
            if (v is System.Text.Json.JsonElement je && je.TryGetDouble(out var n)) return n;
            return double.TryParse(v?.ToString(), out var r) ? r : 0;
        }
    }
}