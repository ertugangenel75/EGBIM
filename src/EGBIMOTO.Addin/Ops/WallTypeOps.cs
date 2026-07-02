using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// EGBIMOTO Duvar Tipi Kütüphanesi Operasyonları — v9
    ///
    /// pyrevit-wall-library-builder (Elif Bilge Bulut, MIT) projesinden
    /// C# .NET 8 EGBIMOTO op paradigmasına tam port.
    ///
    /// Orijinal Python mantığı korundu, EGBIMOTO'ya özgü eklemeler:
    ///   — TR BIM parametre namespace'i (EG_ öneki desteği)
    ///   — Türkçe Function adları (Yapı, Kaplama1, Yalıtım vb.)
    ///   — manifest cache: aynı session'da WallType cache'i yeniden kullanılır
    ///   — ConcurrentDictionary (thread-safe, v8 fix uyumlu)
    ///
    /// Op listesi:
    ///   wall_type_from_csv  — CSV'den duvar tiplerini oluştur / güncelle
    ///   wall_type_export_csv— Projedeki mevcut duvar tiplerini CSV'ye aktar
    /// </summary>
    public static class WallTypeOps
    {
        // ── Function adı → MaterialFunctionAssignment eşlemesi ───────────────
        // Türkçe + İngilizce + kısaltmalar desteklenir.
        private static readonly Dictionary<string, MaterialFunctionAssignment> FunctionMap =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // İngilizce
            { "structure",        MaterialFunctionAssignment.Structure   },
            { "structural",       MaterialFunctionAssignment.Structure   },
            { "substrate",        MaterialFunctionAssignment.Substrate   },
            { "insulation",       MaterialFunctionAssignment.Insulation  },
            { "thermalair",       MaterialFunctionAssignment.Insulation  },
            { "thermalairlayer",  MaterialFunctionAssignment.Insulation  },
            { "thermal/airlayer", MaterialFunctionAssignment.Insulation  },
            { "thermal air layer",MaterialFunctionAssignment.Insulation  },
            { "finish1",          MaterialFunctionAssignment.Finish1     },
            { "finish 1",         MaterialFunctionAssignment.Finish1     },
            { "finish2",          MaterialFunctionAssignment.Finish2     },
            { "finish 2",         MaterialFunctionAssignment.Finish2     },
            { "membrane",         MaterialFunctionAssignment.Membrane    },
            { "membranelayer",    MaterialFunctionAssignment.Membrane    },
            { "membrane layer",   MaterialFunctionAssignment.Membrane    },
            // Türkçe
            { "yapı",             MaterialFunctionAssignment.Structure   },
            { "yapi",             MaterialFunctionAssignment.Structure   },
            { "taşıyıcı",         MaterialFunctionAssignment.Structure   },
            { "tasiyici",         MaterialFunctionAssignment.Structure   },
            { "alt katman",       MaterialFunctionAssignment.Substrate   },
            { "altkatman",        MaterialFunctionAssignment.Substrate   },
            { "yalıtım",          MaterialFunctionAssignment.Insulation  },
            { "yalitim",          MaterialFunctionAssignment.Insulation  },
            { "ısı yalıtımı",     MaterialFunctionAssignment.Insulation  },
            { "kaplama1",         MaterialFunctionAssignment.Finish1     },
            { "kaplama 1",        MaterialFunctionAssignment.Finish1     },
            { "kaplama2",         MaterialFunctionAssignment.Finish2     },
            { "kaplama 2",        MaterialFunctionAssignment.Finish2     },
            { "membran",          MaterialFunctionAssignment.Membrane    },
        };

        // ── BuiltInParameter alias çözücü (orijinal Python OPTIONAL_BUILTIN_PARAM_CANDIDATES) ─
        private static readonly Dictionary<string, BuiltInParameter> ParamAliasMap =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "Type Comments",        BuiltInParameter.ALL_MODEL_TYPE_COMMENTS   },
            { "TypeComments",         BuiltInParameter.ALL_MODEL_TYPE_COMMENTS   },
            { "Description",          BuiltInParameter.ALL_MODEL_DESCRIPTION     },
            { "Fire Rating",          BuiltInParameter.FIRE_RATING               },
            { "FireRating",           BuiltInParameter.FIRE_RATING               },
            { "Keynote",              BuiltInParameter.KEYNOTE_PARAM             },
            // "Assembly Code" — BIP removed in Revit 2026, use fallback string lookup
            // "AssemblyCode" — BIP removed in Revit 2026, use fallback string lookup
            { "Assembly Description", BuiltInParameter.ALL_MODEL_DESCRIPTION     },
            { "AssemblyDescription",  BuiltInParameter.ALL_MODEL_DESCRIPTION     },
            { "Manufacturer",         BuiltInParameter.ALL_MODEL_MANUFACTURER    },
            { "Model",                BuiltInParameter.ALL_MODEL_MODEL           },
            { "Cost",                 BuiltInParameter.ALL_MODEL_COST            },
            { "URL",                  BuiltInParameter.ALL_MODEL_URL             },
            // TR BIM ekleri
            { "EG_YangınSınıfı",      BuiltInParameter.FIRE_RATING               },
            // "EG_MontagKodu" — BIP removed in Revit 2026, use fallback string lookup
        };

        // ── Session cache (thread-safe) ───────────────────────────────────────
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ElementId>
            _matCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ElementId>
            _wallTypeCache = new(StringComparer.OrdinalIgnoreCase);
        private static bool _cacheBuilt = false;
        private static readonly object _cacheLock = new();

        // ─────────────────────────────────────────────────────────────────────
        // OP 1: wall_type_from_csv
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("wall_type_from_csv",
            Description =
                "CSV dosyasından Revit Basic Wall tipleri oluşturur veya günceller.\n" +
                "pyrevit-wall-library-builder (Elif Bilge Bulut) projesinden C# portudur.\n\n" +
                "CSV yapısı (zorunlu sütunlar):\n" +
                "  TypeName, LayerOrder, Function, MaterialName, Thickness_mm\n\n" +
                "CSV yapısı (opsiyonel):\n" +
                "  IsCore, TypeComments, Description, FireRating, Keynote,\n" +
                "  AssemblyCode, AssemblyDescription, Manufacturer, Model, Cost, URL\n\n" +
                "Function değerleri (TR/EN):\n" +
                "  Structure/Yapı, Substrate/AltKatman, Insulation/Yalıtım,\n" +
                "  Finish1/Kaplama1, Finish2/Kaplama2, Membrane/Membran\n\n" +
                "params:\n" +
                "  csv_path        — CSV dosyası yolu (zorunlu)\n" +
                "  mode            — create | update | skip | rename (default: create)\n" +
                "                    create: yeni oluştur, varsa atla\n" +
                "                    update: varsa güncelle\n" +
                "                    skip:   varsa atla\n" +
                "                    rename: varsa yeni isimle kopyala\n" +
                "  rename_suffix   — rename modunda ek (default: ' - Import')\n" +
                "  base_wall_name  — şablon duvar tipi adı (default: ilk Basic Wall)\n" +
                "  delimiter       — CSV ayırıcı (default: auto)\n" +
                "  dry_run         — true ise işlem yapmaz, sadece doğrular (default: false)\n\n" +
                "Çıktı: List<Dictionary> — her satır bir duvar tipi sonucunu temsil eder\n" +
                "  type_name, action (CREATED/UPDATED/SKIPPED/RENAMED/ERROR),\n" +
                "  katman_adet, toplam_kalinlik_mm, uyarilar, mesaj",
            Category = "Duvar",
            RequiresTransaction = true)]
        public static List<Dictionary<string, object?>> WallTypeFromCsv(OpContext ctx)
        {
            var rctx    = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] wall_type_from_csv Revit bağlamı gerektirir.");
            var doc     = rctx.Doc;

            var csvPath     = ctx.RequireString("csv_path");
            var mode        = ctx.GetString("mode", "create").ToLowerInvariant();
            var renameSuffix= ctx.GetString("rename_suffix", " - Import");
            var baseWallName= ctx.GetString("base_wall_name", "");
            var delimiter   = ctx.GetString("delimiter", "auto");
            bool dryRun     = ctx.GetBool("dry_run", false);

            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"CSV dosyası bulunamadı: {csvPath}");

            // ── Cache yenile ──────────────────────────────────────────────────
            lock (_cacheLock) { BuildCache(doc); }

            // ── Şablon duvar tipi bul ─────────────────────────────────────────
            var templateWall = FindTemplateWall(doc, baseWallName);
            if (templateWall == null)
                throw new InvalidOperationException(
                    "Projede Basic Wall tipi bulunamadı. Önce el ile en az bir Basic Wall tipi oluşturun.");

            // ── CSV oku ve grupla ─────────────────────────────────────────────
            var (headers, rows) = ReadCsv(csvPath, delimiter);
            ctx.Log($"  → CSV: {rows.Count} satır, {headers.Count} sütun");

            var grouped = GroupByWallType(rows, headers);
            ctx.Log($"  → {grouped.Count} farklı duvar tipi tespit edildi");

            // ── Validation ────────────────────────────────────────────────────
            var validationResults = grouped.ToDictionary(
                kv => kv.Key,
                kv => ValidateGroup(kv.Key, kv.Value, doc));

            var results = new List<Dictionary<string, object?>>();

            if (dryRun)
            {
                foreach (var kv in grouped.OrderBy(k => k.Key))
                {
                    var (errors, warnings) = validationResults[kv.Key];
                    double totalMm = kv.Value.Sum(r => ParseDouble(r.GetValueOrDefault("Thickness_mm")));
                    results.Add(new Dictionary<string, object?>
                    {
                        ["type_name"]           = kv.Key,
                        ["action"]              = errors.Any() ? "ERROR" : "DRY_RUN_OK",
                        ["katman_adet"]         = kv.Value.Count,
                        ["toplam_kalinlik_mm"]  = Math.Round(totalMm, 1),
                        ["uyarilar"]            = string.Join("; ", warnings),
                        ["mesaj"]               = errors.Any()
                            ? string.Join("; ", errors)
                            : $"{kv.Value.Count} katman hazır",
                    });
                }
                ctx.Log($"  → Dry-run tamamlandı: {results.Count} tip kontrol edildi");
                return results;
            }

            // ── Transaction ───────────────────────────────────────────────────
            using var tx = new Transaction(doc, "EGBIMOTO: Duvar Tipi Kütüphanesi");
            tx.Start();

            int created = 0, updated = 0, skipped = 0, renamed = 0, errorCount = 0;

            foreach (var kv in grouped.OrderBy(k => k.Key))
            {
                var typeName   = kv.Key;
                var layerRows  = kv.Value;
                var (errors, warnings) = validationResults[typeName];
                double totalMm = layerRows.Sum(r => ParseDouble(r.GetValueOrDefault("Thickness_mm")));

                if (errors.Any())
                {
                    errorCount++;
                    results.Add(MakeResult(typeName, "ERROR",
                        layerRows.Count, totalMm, warnings,
                        string.Join("; ", errors)));
                    ctx.Log($"  ✗ '{typeName}': {errors[0]}");
                    continue;
                }

                try
                {
                    var (action, finalName) = ApplyWallType(
                        doc, typeName, layerRows, templateWall,
                        mode, renameSuffix, warnings, ctx);

                    switch (action)
                    {
                        case "CREATED":  created++;  break;
                        case "UPDATED":  updated++;  break;
                        case "SKIPPED":  skipped++;  break;
                        case "RENAMED":  renamed++;  break;
                    }

                    results.Add(MakeResult(finalName, action,
                        layerRows.Count, totalMm, warnings, ""));
                }
                catch (Exception ex)
                {
                    errorCount++;
                    results.Add(MakeResult(typeName, "ERROR",
                        layerRows.Count, totalMm, warnings, ex.Message));
                    ctx.Log($"  ✗ '{typeName}': {ex.Message}");
                }
            }

            tx.Commit();

            ctx.Log($"  → Tamamlandı: {created} oluşturuldu, {updated} güncellendi, " +
                    $"{skipped} atlandı, {renamed} yeniden adlandırıldı, {errorCount} hata");
            return results;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OP 2: wall_type_export_csv
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("wall_type_export_csv",
            Description =
                "Projedeki mevcut Basic Wall tiplerini wall_type_from_csv uyumlu CSV formatına aktarır.\n" +
                "Oluşturulan CSV doğrudan wall_type_from_csv ile tekrar yüklenebilir.\n\n" +
                "params:\n" +
                "  output_path     — CSV çıktı dosyası yolu (zorunlu)\n" +
                "  filter_name     — tip adı filtresi, regex (opsiyonel)\n" +
                "  include_params  — opsiyonel parametreler dahil edilsin mi (default: true)\n\n" +
                "Çıktı: Dictionary — output_path, wall_type_count, row_count",
            Category = "Duvar")]
        public static Dictionary<string, object?> WallTypeExportCsv(OpContext ctx)
        {
            var rctx       = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] wall_type_export_csv Revit bağlamı gerektirir.");
            var doc        = rctx.Doc;
            var outputPath = ctx.RequireString("output_path");
            var filterName = ctx.GetString("filter_name", "");
            bool incParams = ctx.GetBool("include_params", true);

            // ── Tüm Basic Wall tiplerini topla ────────────────────────────────
            var wallTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .Where(wt => wt.Kind == WallKind.Basic)
                .OrderBy(wt => wt.Name)
                .ToList();

            // İsim filtresi
            if (!string.IsNullOrEmpty(filterName))
            {
                try
                {
                    var rx = new System.Text.RegularExpressions.Regex(
                        filterName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    wallTypes = wallTypes.Where(wt => rx.IsMatch(wt.Name)).ToList();
                }
                catch
                {
                    wallTypes = wallTypes
                        .Where(wt => wt.Name.Contains(filterName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            ctx.Log($"  → {wallTypes.Count} Basic Wall tipi dışa aktarılıyor");

            // ── CSV başlık ────────────────────────────────────────────────────
            var optCols = incParams
                ? new[] { "TypeComments", "Description", "FireRating", "Keynote",
                          "AssemblyCode", "AssemblyDescription", "Manufacturer", "Model", "Cost", "URL" }
                : Array.Empty<string>();

            var header = new List<string>
            {
                "TypeName", "LayerOrder", "Function", "MaterialName", "Thickness_mm", "IsCore"
            };
            header.AddRange(optCols);

            var csvRows = new List<string[]> { header.ToArray() };
            int totalRows = 0;

            foreach (var wt in wallTypes)
            {
                CompoundStructure? cs = null;
                try { cs = wt.GetCompoundStructure(); } catch { }
                if (cs == null) continue;

                var layers = cs.GetLayers().ToList();
                int exteriorShells = 0;
                try { exteriorShells = cs.GetNumberOfShellLayers(ShellLayerType.Exterior); } catch { }

                // Opsiyonel parametre değerlerini al
                var optVals = new Dictionary<string, string>();
                if (incParams)
                {
                    foreach (var col in optCols)
                    {
                        optVals[col] = GetParamValue(wt, col);
                    }
                }

                for (int i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    var mat = doc.GetElement(layer.MaterialId) as Material;
                    double thickMm = Math.Round(
                        UnitUtils.ConvertFromInternalUnits(layer.Width, UnitTypeId.Millimeters), 2);

                    bool isCore = i >= exteriorShells &&
                                  i < layers.Count - (cs.GetNumberOfShellLayers(ShellLayerType.Interior));

                    string funcStr = layer.Function switch
                    {
                        MaterialFunctionAssignment.Structure  => "Structure",
                        MaterialFunctionAssignment.Substrate  => "Substrate",
                        MaterialFunctionAssignment.Insulation => "Insulation",
                        MaterialFunctionAssignment.Finish1    => "Finish1",
                        MaterialFunctionAssignment.Finish2    => "Finish2",
                        MaterialFunctionAssignment.Membrane   => "Membrane",
                        _                                     => "Substrate",
                    };

                    var row = new List<string>
                    {
                        wt.Name,
                        (i + 1).ToString(),
                        funcStr,
                        mat?.Name ?? "",
                        thickMm.ToString("F2"),
                        isCore ? "Yes" : "",
                    };

                    if (incParams)
                    {
                        // Sadece ilk katmanda opsiyonel parametreler yazılır
                        foreach (var col in optCols)
                            row.Add(i == 0 ? optVals.GetValueOrDefault(col, "") : "");
                    }

                    csvRows.Add(row.ToArray());
                    totalRows++;
                }
            }

            // ── CSV yaz ───────────────────────────────────────────────────────
            var sb = new StringBuilder();
            foreach (var row in csvRows)
                sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);

            ctx.Log($"  → {wallTypes.Count} tip, {totalRows} katman satırı → {outputPath}");
            return new Dictionary<string, object?>
            {
                ["output_path"]      = outputPath,
                ["wall_type_count"]  = wallTypes.Count,
                ["row_count"]        = totalRows,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // İç motorlar (Python mantığının C# karşılığı)
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildCache(Document doc)
        {
            if (_cacheBuilt) return;

            _matCache.Clear();
            foreach (var mat in new FilteredElementCollector(doc)
                .OfClass(typeof(Material)).Cast<Material>())
            {
                _matCache.TryAdd(mat.Name, mat.Id);
            }

            _wallTypeCache.Clear();
            foreach (var wt in new FilteredElementCollector(doc)
                .OfClass(typeof(WallType)).Cast<WallType>()
                .Where(w => w.Kind == WallKind.Basic))
            {
                _wallTypeCache.TryAdd(wt.Name, wt.Id);
            }

            _cacheBuilt = true;
        }

        private static WallType? FindTemplateWall(Document doc, string name)
        {
            if (!string.IsNullOrEmpty(name) && _wallTypeCache.TryGetValue(name, out var id))
                return doc.GetElement(id) as WallType;

            // İlk Basic Wall tipini döndür
            return _wallTypeCache.Values
                .Select(id2 => doc.GetElement(id2) as WallType)
                .FirstOrDefault(wt => wt != null && wt.Kind == WallKind.Basic);
        }

        /// <summary>
        /// CSV okuma — delimiter otomatik veya manual.
        /// Python read_csv_raw() karşılığı.
        /// </summary>
        private static (List<string> Headers, List<Dictionary<string, string>> Rows)
            ReadCsv(string path, string delimiter)
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0) return (new(), new());

            char sep = delimiter.ToLowerInvariant() == "auto"
                ? DetectDelimiter(lines[0])
                : (delimiter.Length > 0 ? delimiter[0] : ',');

            var headers = SplitCsv(lines[0], sep).Select(h => h.Trim()).ToList();
            var rows    = new List<Dictionary<string, string>>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var vals = SplitCsv(line, sep);
                var row  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < headers.Count; j++)
                    row[headers[j]] = j < vals.Count ? vals[j].Trim() : "";
                rows.Add(row);
            }

            return (headers, rows);
        }

        private static char DetectDelimiter(string firstLine)
        {
            // Virgül > noktalı virgül > tab > boru
            foreach (char c in new[] { ',', ';', '\t', '|' })
                if (firstLine.Contains(c)) return c;
            return ',';
        }

        private static List<string> SplitCsv(string line, char sep)
        {
            var result = new List<string>();
            bool inQuote = false;
            var current  = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '"') { inQuote = !inQuote; continue; }
                if (c == sep && !inQuote) { result.Add(current.ToString()); current.Clear(); continue; }
                current.Append(c);
            }
            result.Add(current.ToString());
            return result;
        }

        /// <summary>
        /// TypeName'e göre satırları grupla, LayerOrder'a göre sırala.
        /// Python group_mapped_rows() karşılığı.
        /// </summary>
        private static Dictionary<string, List<Dictionary<string, string>>>
            GroupByWallType(List<Dictionary<string, string>> rows, List<string> headers)
        {
            // TypeName sütununu bul (auto-mapping)
            var typeCol = FindColumn(headers,
                new[] { "TypeName", "Type Name", "WallType", "Wall Type", "AssemblyName" });
            var orderCol = FindColumn(headers,
                new[] { "LayerOrder", "Layer Order", "Order", "LayerNo" });

            var grouped = new Dictionary<string, List<Dictionary<string, string>>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var name = typeCol != null ? row.GetValueOrDefault(typeCol, "").Trim() : "";
                if (string.IsNullOrEmpty(name)) continue;

                if (!grouped.ContainsKey(name))
                    grouped[name] = new List<Dictionary<string, string>>();
                grouped[name].Add(row);
            }

            // LayerOrder'a göre sırala
            if (orderCol != null)
            {
                foreach (var key in grouped.Keys.ToList())
                    grouped[key] = grouped[key]
                        .OrderBy(r => ParseDouble(r.GetValueOrDefault(orderCol)))
                        .ToList();
            }

            return grouped;
        }

        /// <summary>
        /// Bir duvar tipi grubunu doğrular.
        /// Python validate_group() karşılığı — aynı kurallar.
        /// </summary>
        private static (List<string> Errors, List<string> Warnings)
            ValidateGroup(string typeName, List<Dictionary<string, string>> rows, Document doc)
        {
            var errors   = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrEmpty(typeName))
                errors.Add("Duvar tipi adı boş");

            if (!rows.Any())
            {
                errors.Add("Katman satırı bulunamadı");
                return (errors, warnings);
            }

            bool hasStructure = false;
            bool hasCore      = false;
            var  orderVals    = new HashSet<double>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int rowNo = i + 2;

                // Thickness
                var thkStr = FindValue(row, "Thickness_mm", "Thickness mm", "Width_mm", "Width");
                if (!double.TryParse(thkStr?.Replace(",", "."), out double thk))
                {
                    errors.Add($"Satır {rowNo}: kalınlık sayısal değil");
                }
                else
                {
                    var fn = NormKey(FindValue(row, "Function", "Layer Function") ?? "");
                    bool isMembrane = fn.Contains("membrane") || fn.Contains("membran");
                    if (thk <= 0 && !isMembrane)
                        errors.Add($"Satır {rowNo}: kalınlık 0'dan büyük olmalı");
                    if (isMembrane && thk != 0)
                        warnings.Add($"Satır {rowNo}: Membran katmanı kalınlığı 0'a zorlanacak");
                }

                // LayerOrder tekrar kontrolü
                var ordStr = FindValue(row, "LayerOrder", "Layer Order", "Order");
                if (double.TryParse(ordStr, out double ord))
                {
                    if (!orderVals.Add(ord))
                        warnings.Add($"Satır {rowNo}: tekrarlanan katman sırası ({ord})");
                }

                // Function
                var fnStr = FindValue(row, "Function", "Layer Function") ?? "";
                var fnKey = NormKey(fnStr);
                if (string.IsNullOrEmpty(fnStr))
                    warnings.Add($"Satır {rowNo}: Function boş — Substrate kullanılacak");

                if (fnKey.Contains("struct") || fnKey.Contains("yapı") || fnKey.Contains("tasıyıcı"))
                    hasStructure = true;

                // IsCore
                var isCore = FindValue(row, "IsCore", "Is Core", "Core") ?? "";
                if (IsYes(isCore)) hasCore = true;

                // Material
                var matName = FindValue(row, "MaterialName", "Material Name", "Material") ?? "";
                if (string.IsNullOrEmpty(matName))
                    warnings.Add($"Satır {rowNo}: Malzeme boş");
                else if (!_matCache.ContainsKey(matName))
                    warnings.Add($"Satır {rowNo}: Malzeme bulunamadı: '{matName}'");
            }

            if (!hasStructure) warnings.Add("Structure/Yapı katmanı tespit edilmedi");
            if (!hasCore)      warnings.Add("Core (çekirdek) katmanı tanımlanmamış");

            return (errors, warnings);
        }

        /// <summary>
        /// Duvar tipini oluştur / güncelle / atla / yeniden adlandır.
        /// Python create_or_update_wall_type() karşılığı.
        /// </summary>
        private static (string Action, string FinalName) ApplyWallType(
            Document doc,
            string typeName,
            List<Dictionary<string, string>> rows,
            WallType templateWall,
            string mode,
            string renameSuffix,
            List<string> warnings,
            OpContext ctx)
        {
            bool exists = _wallTypeCache.ContainsKey(typeName);

            if (exists && mode == "skip")
                return ("SKIPPED", typeName);

            WallType? target;

            if (exists && mode == "update")
            {
                target = doc.GetElement(_wallTypeCache[typeName]) as WallType
                    ?? throw new InvalidOperationException($"'{typeName}' bulunamadı");
            }
            else if (exists && mode == "rename")
            {
                // Benzersiz isim oluştur
                string newName = typeName + renameSuffix;
                int n = 1;
                while (_wallTypeCache.ContainsKey(newName))
                    newName = $"{typeName}{renameSuffix} {++n:D2}";

                target = templateWall.Duplicate(newName) as WallType
                    ?? throw new InvalidOperationException($"Duplicate başarısız: '{newName}'");
                _wallTypeCache.TryAdd(newName, target.Id);
                ctx.Log($"  → Rename: '{typeName}' → '{newName}'");
                SetCompoundStructure(target, rows);
                ApplyOptionalParams(target, rows);
                return ("RENAMED", newName);
            }
            else
            {
                // mode == "create" veya mevcut değil
                target = templateWall.Duplicate(typeName) as WallType
                    ?? throw new InvalidOperationException($"Duplicate başarısız: '{typeName}'");
                _wallTypeCache.TryAdd(typeName, target.Id);
            }

            SetCompoundStructure(target!, rows);
            ApplyOptionalParams(target!, rows);

            return (exists && mode == "update" ? "UPDATED" : "CREATED", typeName);
        }

        /// <summary>
        /// CompoundStructure oluştur ve duvar tipine ata.
        /// Python set_wall_compound_structure() karşılığı.
        /// </summary>
        private static void SetCompoundStructure(
            WallType wallType,
            List<Dictionary<string, string>> rows)
        {
            if (wallType.Kind != WallKind.Basic)
                throw new InvalidOperationException("Yalnızca Basic Wall tipleri düzenlenebilir.");

            var layers = new List<CompoundStructureLayer>();
            var coreIndices = new List<int>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                var fnStr   = NormKey(FindValue(row, "Function", "Layer Function") ?? "");
                var matName = FindValue(row, "MaterialName", "Material Name", "Material") ?? "";
                var thkStr  = FindValue(row, "Thickness_mm", "Thickness mm", "Width_mm") ?? "0";
                var coreStr = FindValue(row, "IsCore", "Is Core", "Core") ?? "";

                var func = FunctionMap.TryGetValue(fnStr, out var f)
                    ? f
                    : MaterialFunctionAssignment.Substrate;

                _matCache.TryGetValue(matName, out var matId);
                matId ??= ElementId.InvalidElementId;

                bool isMembrane = func == MaterialFunctionAssignment.Membrane;
                double widthFt  = isMembrane ? 0 : ParseDouble(thkStr) / 304.8;

                layers.Add(new CompoundStructureLayer(widthFt, func, matId));

                if (IsYes(coreStr)) coreIndices.Add(i);
            }

            if (!layers.Any()) return;

            var cs = CompoundStructure.CreateSimpleCompoundStructure(layers);

            // Core sınırlarını ayarla (Python SetNumberOfShellLayers mantığı)
            if (coreIndices.Any())
            {
                int firstCore = coreIndices.Min();
                int lastCore  = coreIndices.Max();
                try
                {
                    cs.SetNumberOfShellLayers(ShellLayerType.Exterior, firstCore);
                    cs.SetNumberOfShellLayers(ShellLayerType.Interior, layers.Count - lastCore - 1);
                }
                catch { /* Core ayarı opsiyonel — hata durumunda devam */ }
            }

            wallType.SetCompoundStructure(cs);
        }

        /// <summary>
        /// Opsiyonel parametreleri yaz (TypeComments, FireRating, vb.)
        /// Python apply_optional_parameters() + set_parameter_from_text() karşılığı.
        /// </summary>
        private static void ApplyOptionalParams(
            WallType wallType,
            List<Dictionary<string, string>> rows)
        {
            // İlk satırdaki opsiyonel değerleri al
            var paramCols = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Type Comments",        new[] { "TypeComments", "Type Comments", "Comments" } },
                { "Description",          new[] { "Description" } },
                { "Fire Rating",          new[] { "FireRating", "Fire Rating" } },
                { "Keynote",              new[] { "Keynote" } },
                { "Assembly Code",        new[] { "AssemblyCode", "Assembly Code" } },
                { "Assembly Description", new[] { "AssemblyDescription", "Assembly Description" } },
                { "Manufacturer",         new[] { "Manufacturer" } },
                { "Model",                new[] { "Model" } },
                { "Cost",                 new[] { "Cost" } },
                { "URL",                  new[] { "URL" } },
            };

            foreach (var kv in paramCols)
            {
                string? val = null;
                foreach (var row in rows)
                {
                    val = FindValue(row, kv.Value);
                    if (!string.IsNullOrEmpty(val)) break;
                }
                if (string.IsNullOrEmpty(val)) continue;

                SetWallTypeParam(wallType, kv.Key, val);
            }
        }

        /// <summary>
        /// BuiltInParameter alias → fallback ad araması.
        /// Python get_param_by_name() + OPTIONAL_BUILTIN_PARAM_CANDIDATES karşılığı.
        /// </summary>
        private static void SetWallTypeParam(WallType wallType, string paramName, string value)
        {
            Parameter? p = null;

            // 1) BuiltInParameter alias
            if (ParamAliasMap.TryGetValue(paramName, out var bip))
            {
                try { p = wallType.get_Parameter(bip); } catch { }
            }

            // 2) Tam ad araması
            if (p == null)
                p = wallType.Parameters.Cast<Parameter>()
                    .FirstOrDefault(pp => string.Equals(
                        pp.Definition?.Name, paramName, StringComparison.OrdinalIgnoreCase));

            // 3) Normalize ad araması
            if (p == null)
            {
                var normTarget = NormKey(paramName);
                p = wallType.Parameters.Cast<Parameter>()
                    .FirstOrDefault(pp => NormKey(pp.Definition?.Name ?? "") == normTarget);
            }

            if (p == null || p.IsReadOnly) return;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(value);
                        break;
                    case StorageType.Double:
                        if (double.TryParse(value.Replace(",", "."), out double d))
                            p.Set(d);
                        break;
                    case StorageType.Integer:
                        if (IsYes(value)) p.Set(1);
                        else if (int.TryParse(value, out int iv)) p.Set(iv);
                        break;
                    case StorageType.ElementId:
                        try { p.SetValueString(value); } catch { }
                        break;
                }
            }
            catch { }
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static string? FindColumn(List<string> headers, string[] candidates)
        {
            foreach (var c in candidates)
            {
                var match = headers.FirstOrDefault(h =>
                    string.Equals(h.Trim(), c, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            // Normalize karşılaştırma
            foreach (var c in candidates)
            {
                var nc = NormKey(c);
                var match = headers.FirstOrDefault(h => NormKey(h) == nc);
                if (match != null) return match;
            }
            return null;
        }

        private static string? FindValue(
            Dictionary<string, string> row, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (row.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            // Normalize arama
            foreach (var k in keys)
            {
                var nk = NormKey(k);
                var pair = row.FirstOrDefault(kv => NormKey(kv.Key) == nk);
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    return pair.Value.Trim();
            }
            return null;
        }

        /// <summary>Python norm_key() karşılığı — küçük harf + tüm ayırıcıları kaldır.</summary>
        private static string NormKey(string? s)
            => (s ?? "").ToLowerInvariant()
                .Replace(" ", "").Replace("_", "").Replace("-", "")
                .Replace("/", "").Replace("(", "").Replace(")", "");

        private static bool IsYes(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            var n = v.Trim().ToLowerInvariant();
            return n is "yes" or "y" or "true" or "1" or "x" or "evet" or "core" or "ç";
        }

        private static double ParseDouble(string? s, double def = 0)
        {
            if (string.IsNullOrWhiteSpace(s)) return def;
            return double.TryParse(s.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : def;
        }

        private static string GetParamValue(WallType wt, string colName)
        {
            if (!ParamAliasMap.TryGetValue(colName.Replace(" ", ""), out var bip))
                return "";
            try
            {
                var p = wt.get_Parameter(bip);
                return p?.AsString() ?? p?.AsValueString() ?? "";
            }
            catch { return ""; }
        }

        private static string EscapeCsv(string s)
        {
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        private static Dictionary<string, object?> MakeResult(
            string typeName, string action, int katmanAdet,
            double toplamMm, List<string> warnings, string mesaj)
        => new()
        {
            ["type_name"]          = typeName,
            ["action"]             = action,
            ["katman_adet"]        = katmanAdet,
            ["toplam_kalinlik_mm"] = Math.Round(toplamMm, 1),
            ["uyarilar"]           = string.Join("; ", warnings),
            ["mesaj"]              = mesaj,
        };
    }
}
