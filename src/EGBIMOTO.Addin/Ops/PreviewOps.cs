using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EGBIMOTO.Addin.Host;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Core.Preview;
using EGBIMOTO.Core.DAG;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — PreviewOps  (v1.0)
    //
    //  Preview-Confirm akışı için iki op:
    //
    //  1. preview_collect_geometry
    //     List<Element> → PreviewGeometryDto
    //     Revit geometrisini triangulate eder, mm koordinatlarına çevirir.
    //     Transaction açmaz — saf okuma op'u.
    //
    //  2. preview_gate  (STUB)
    //     DagExecutor tarafından intercept edilir — bu metod çağrılmaz.
    //     ManifestLinter'ın "Op kayıtlı değil" hatasını önlemek için kayıtlı.
    //     Çağrılırsa açık hata fırlatır (hata ayıklama için).
    //
    //  Manifest kullanımı:
    //    { "id": "geom",  "op": "preview_collect_geometry", "from": "duvarlar",
    //                     "params": { "operation_name": "Duvar Poz Ataması",
    //                                 "include_labels": true } }
    //    { "id": "gate",  "op": "preview_gate", "from": "geom",
    //                     "params": { "title": "Duvar Poz Ataması Önizleme" } }
    //    { "id": "yaz",   "op": "assign_poz",   "from": "duvarlar",
    //                     "condition": "$gate == confirmed" }
    // ═══════════════════════════════════════════════════════════════════════════

    public static class PreviewOps
    {
        // ── Revit feet → mm sabitleri ─────────────────────────────────────────
        private const double FtToMm  = 304.8;
        private const double Ft2ToMm2 = FtToMm * FtToMm;

        // ── Tessellation hassasiyeti (0 = kaba, 1 = ince) ─────────────────────
        private const double TessLevel = 0.5;

        // ═════════════════════════════════════════════════════════════════════
        //  1. preview_collect_geometry
        // ═════════════════════════════════════════════════════════════════════

        [EgOp("preview_collect_geometry",
            Description = "Element listesinden Three.js uyumlu PreviewGeometryDto üretir. " +
                          "params: operation_name (string), include_labels (bool, default:true), " +
                          "max_elements (int, default:500)",
            Category    = "Önizleme",
            RequiresTransaction = false)]
        public static PreviewGeometryDto CollectGeometry(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] preview_collect_geometry Revit bağlamı gerektirir.");

            var elements      = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var opName        = ctx.GetString("operation_name", "Önizleme");
            var includeLabels = ctx.GetBool("include_labels", true);
            var maxElements   = ctx.GetInt("max_elements", 500);

            if (elements.Count > maxElements)
            {
                ctx.Log($"  ⚠ preview_collect_geometry: {elements.Count} eleman, " +
                        $"max {maxElements} ile sınırlandırıldı.");
                elements = elements.Take(maxElements).ToList();
            }

            var dto = new PreviewGeometryDto
            {
                OperationName = opName,
                Description   = $"{elements.Count} eleman önizlenmektedir.",
                ElementCount  = elements.Count,
            };

            // ── Kategori sayım istatistikleri ────────────────────────────────
            var catCounts = new Dictionary<string, int>();

            // ── BBox hesabı ───────────────────────────────────────────────────
            float bMinX = float.MaxValue, bMinY = float.MaxValue, bMinZ = float.MaxValue;
            float bMaxX = float.MinValue, bMaxY = float.MinValue, bMaxZ = float.MinValue;
            bool  hasBBox = false;

            var geomOptions = new Options
            {
                ComputeReferences    = false,
                IncludeNonVisibleObjects = false,
                DetailLevel          = ViewDetailLevel.Medium
            };

            int meshIdx = 0;
            foreach (var element in elements)
            {
                var catName = CategoryName(element);

                // Sayım
                if (!catCounts.ContainsKey(catName)) catCounts[catName] = 0;
                catCounts[catName]++;

                var color = PreviewColors.ForCategory(catName);

                // ── Geometri toplama ─────────────────────────────────────────
                try
                {
                    var geom = element.get_Geometry(geomOptions);
                    if (geom == null)
                    {
                        // Solid yoksa BBox fallback
                        AddBBoxMesh(dto, element, $"mesh_{meshIdx++}", catName, color,
                                    ref bMinX, ref bMinY, ref bMinZ,
                                    ref bMaxX, ref bMaxY, ref bMaxZ, ref hasBBox);
                        continue;
                    }

                    var solids = CollectSolids(geom).Where(s => s.Volume > 1e-6).ToList();
                    if (solids.Count == 0)
                    {
                        AddBBoxMesh(dto, element, $"mesh_{meshIdx++}", catName, color,
                                    ref bMinX, ref bMinY, ref bMinZ,
                                    ref bMaxX, ref bMaxY, ref bMaxZ, ref hasBBox);
                        continue;
                    }

                    foreach (var solid in solids)
                    {
                        var mesh = SolidToMesh(solid, $"mesh_{meshIdx++}", catName, color, element);
                        if (mesh == null) continue;
                        dto.Meshes.Add(mesh);

                        // BBox güncelle
                        UpdateBBox(mesh, ref bMinX, ref bMinY, ref bMinZ,
                                         ref bMaxX, ref bMaxY, ref bMaxZ, ref hasBBox);
                    }

                    // ── Wireframe edge (BBox) ─────────────────────────────────
                    var bb = element.get_BoundingBox(null);
                    if (bb != null)
                        dto.Edges.Add(BBoxToEdge(bb));
                }
                catch (Exception ex)
                {
                    ctx.Log($"  ⚠ preview_collect_geometry [{Rv.GetId(element.Id)}]: {ex.Message}");
                    dto.Warnings.Add($"#{Rv.GetId(element.Id)} geometri okunamadı: {ex.Message}");
                }

                // ── Label ─────────────────────────────────────────────────────
                if (includeLabels)
                {
                    var bb = element.get_BoundingBox(null);
                    if (bb != null)
                    {
                        var cx = RevitToMmX(bb.Min.X + (bb.Max.X - bb.Min.X) / 2);
                        var cy = RevitToMmY(bb.Min.Z + (bb.Max.Z - bb.Min.Z) / 2);
                        var cz = RevitToMmZ(bb.Min.Y + (bb.Max.Y - bb.Min.Y) / 2);
                        dto.Labels.Add(new PreviewLabel
                        {
                            X    = cx, Y = cy, Z = cz,
                            Text = $"#{Rv.GetId(element.Id)}",
                            Color = "#FFDD00"
                        });
                    }
                }
            }

            // ── BBox ─────────────────────────────────────────────────────────
            if (hasBBox)
                dto.BBox = new PreviewBBox
                {
                    MinX = bMinX, MinY = bMinY, MinZ = bMinZ,
                    MaxX = bMaxX, MaxY = bMaxY, MaxZ = bMaxZ
                };

            // ── Stats ─────────────────────────────────────────────────────────
            foreach (var kv in catCounts)
                dto.Stats[kv.Key] = kv.Value.ToString();

            dto.Description = BuildDescription(catCounts, elements.Count);

            ctx.Log($"  ✓ preview_collect_geometry: {dto.Meshes.Count} mesh, " +
                    $"{dto.Edges.Count} edge, {elements.Count} eleman");
            return dto;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  2. preview_gate — STUB (DagExecutor intercept eder)
        // ═════════════════════════════════════════════════════════════════════

        [EgOp("preview_gate",
            Description = "Kullanıcı onay kapısı. DagExecutor tarafından intercept edilir; " +
                          "doğrudan çağrılmamalı. params: title (string)",
            Category    = "Önizleme",
            RequiresTransaction = false)]
        public static string PreviewGateStub(OpContext ctx)
        {
            // Bu metod normalde çağrılmaz — DagExecutor intercept eder.
            // Eğer çağrılıyorsa DagExecutor konfigürasyonu eksik.
            throw new InvalidOperationException(
                $"[{ctx.CurrentStepId}] preview_gate op'u DagExecutor tarafından intercept " +
                "edilmelidir. DagExecutor.UserGateCallback set edildi mi? " +
                "EgbimotoApp.RunPreviewManifest() kullanıldı mı?");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Yardımcı metodlar — Revit geometri
        // ═════════════════════════════════════════════════════════════════════

        private static IEnumerable<Solid> CollectSolids(GeometryElement geom)
        {
            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid && solid.Volume > 0)
                    yield return solid;
                else if (obj is GeometryInstance inst)
                    foreach (var s in CollectSolids(inst.GetInstanceGeometry()))
                        yield return s;
            }
        }

        private static PreviewMesh? SolidToMesh(Solid solid, string id,
            string category, string color, Element element)
        {
            var mesh = new PreviewMesh
            {
                Id       = id,
                Category = category,
                Label    = $"#{Rv.GetId(element.Id)}",
                Color    = color,
                Opacity  = 0.65f,
            };

            // Eleman parametrelerini properties'e ekle
            var typeParam = element.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM);
            if (typeParam != null)
                mesh.Properties["tip"] = typeParam.AsValueString() ?? "";

            var levelParam = element.get_Parameter(BuiltInParameter.LEVEL_PARAM);
            if (levelParam != null)
                mesh.Properties["kat"] = levelParam.AsValueString() ?? "";

            int vertIdx = 0;

            foreach (Face face in solid.Faces)
            {
                Mesh faceMesh;
                try { faceMesh = face.Triangulate(TessLevel); }
                catch { continue; }

                if (faceMesh == null || faceMesh.NumTriangles == 0) continue;

                for (int t = 0; t < faceMesh.NumTriangles; t++)
                {
                    var tri = faceMesh.get_Triangle(t);
                    for (int v = 0; v < 3; v++)
                    {
                        var pt = tri.get_Vertex(v);
                        mesh.Vertices.Add(RevitToMmX(pt.X));
                        mesh.Vertices.Add(RevitToMmY(pt.Z));   // Z→Y (Y-up)
                        mesh.Vertices.Add(RevitToMmZ(-pt.Y));  // -Y→Z
                        mesh.Indices.Add(vertIdx++);
                    }
                }
            }

            if (mesh.Vertices.Count == 0) return null;
            return mesh;
        }

        private static void AddBBoxMesh(PreviewGeometryDto dto, Element element,
            string id, string catName, string color,
            ref float bMinX, ref float bMinY, ref float bMinZ,
            ref float bMaxX, ref float bMaxY, ref float bMaxZ, ref bool hasBBox)
        {
            var bb = element.get_BoundingBox(null);
            if (bb == null) return;

            var mesh = BBoxToMesh(bb, id, catName, color);
            dto.Meshes.Add(mesh);
            UpdateBBox(mesh, ref bMinX, ref bMinY, ref bMinZ,
                             ref bMaxX, ref bMaxY, ref bMaxZ, ref hasBBox);
        }

        private static PreviewMesh BBoxToMesh(BoundingBoxXYZ bb, string id,
            string category, string color)
        {
            // 8 köşe → 12 üçgen (6 yüz × 2 üçgen)
            float x0 = RevitToMmX(bb.Min.X), y0 = RevitToMmY(bb.Min.Z), z0 = RevitToMmZ(-bb.Max.Y);
            float x1 = RevitToMmX(bb.Max.X), y1 = RevitToMmY(bb.Max.Z), z1 = RevitToMmZ(-bb.Min.Y);

            var verts = new float[]
            {
                // 8 köşe (Three.js Y-up)
                x0,y0,z0,  x1,y0,z0,  x1,y1,z0,  x0,y1,z0,  // front
                x0,y0,z1,  x1,y0,z1,  x1,y1,z1,  x0,y1,z1,  // back
            };

            var faces = new[]
            {
                0,1,2, 0,2,3,   // front
                4,6,5, 4,7,6,   // back
                0,3,7, 0,7,4,   // left
                1,5,6, 1,6,2,   // right
                3,2,6, 3,6,7,   // top
                0,4,5, 0,5,1    // bottom
            };

            var mesh = new PreviewMesh { Id = id, Category = category, Color = color, Opacity = 0.4f };
            foreach (var v in verts) mesh.Vertices.Add(v);
            foreach (var i in faces) mesh.Indices.Add(i);
            return mesh;
        }

        private static PreviewEdge BBoxToEdge(BoundingBoxXYZ bb)
        {
            float x0 = RevitToMmX(bb.Min.X), y0 = RevitToMmY(bb.Min.Z), z0 = RevitToMmZ(-bb.Max.Y);
            float x1 = RevitToMmX(bb.Max.X), y1 = RevitToMmY(bb.Max.Z), z1 = RevitToMmZ(-bb.Min.Y);

            // 12 kenar, her biri 2 nokta = 24 XYZ çifti = 72 float
            var pts = new[]
            {
                x0,y0,z0, x1,y0,z0,  x1,y0,z0, x1,y0,z1,  x1,y0,z1, x0,y0,z1,  x0,y0,z1, x0,y0,z0, // alt
                x0,y1,z0, x1,y1,z0,  x1,y1,z0, x1,y1,z1,  x1,y1,z1, x0,y1,z1,  x0,y1,z1, x0,y1,z0, // üst
                x0,y0,z0, x0,y1,z0,  x1,y0,z0, x1,y1,z0,  x1,y0,z1, x1,y1,z1,  x0,y0,z1, x0,y1,z1  // dikey
            };

            var edge = new PreviewEdge { Color = "#AAAAAA", Opacity = 0.5f };
            foreach (var p in pts) edge.Points.Add(p);
            return edge;
        }

        private static void UpdateBBox(PreviewMesh mesh,
            ref float bMinX, ref float bMinY, ref float bMinZ,
            ref float bMaxX, ref float bMaxY, ref float bMaxZ, ref bool hasBBox)
        {
            for (int i = 0; i + 2 < mesh.Vertices.Count; i += 3)
            {
                float x = mesh.Vertices[i], y = mesh.Vertices[i + 1], z = mesh.Vertices[i + 2];
                if (x < bMinX) bMinX = x; if (x > bMaxX) bMaxX = x;
                if (y < bMinY) bMinY = y; if (y > bMaxY) bMaxY = y;
                if (z < bMinZ) bMinZ = z; if (z > bMaxZ) bMaxZ = z;
                hasBBox = true;
            }
        }

        // ── Koordinat dönüşümü: Revit feet → mm, Z-up → Y-up ─────────────────
        private static float RevitToMmX(double v) => (float)(v * FtToMm);
        private static float RevitToMmY(double v) => (float)(v * FtToMm);  // Z→Y
        private static float RevitToMmZ(double v) => (float)(v * FtToMm);  // -Y→Z

        // ── Kategori adı ──────────────────────────────────────────────────────
        private static string CategoryName(Element e)
        {
            if (e.Category == null) return "Diğer";
            return e.Category.BuiltInCategory switch
            {
                BuiltInCategory.OST_Walls           => "Duvar",
                BuiltInCategory.OST_Floors          => "Döşeme",
                BuiltInCategory.OST_Columns         => "Kolon",
                BuiltInCategory.OST_StructuralColumns => "Kolon",
                BuiltInCategory.OST_StructuralFraming => "Kiriş",
                BuiltInCategory.OST_StructuralFoundation => "Temel",
                BuiltInCategory.OST_Roofs           => "Çatı",
                BuiltInCategory.OST_Stairs          => "Merdiven",
                _                                   => e.Category.Name
            };
        }

        private static string BuildDescription(Dictionary<string, int> counts, int total)
        {
            var parts = counts.Select(kv => $"{kv.Value} {kv.Key}");
            return $"{total} eleman: {string.Join(", ", parts)}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PreviewSupportOps — preview_samples manifest'lerinin kullandığı ek op'lar
    //
    //  • collect_structural_columns  — collect_columns alias (dil bağımsız)
    //  • assign_poz_number           — sıralı poz numarası atama (EGBIM_PozNo)
    //  • export_row_report           — satır listesini HTML rapora dönüştür
    //  • write_row_param             — satır listesindeki her elemana değer yaz
    //
    //  Bu op'lar preview_samples manifest'leriyle çalışır ve mevcut altyapıyla
    //  tam uyumludur. İleride daha kapsamlı implementasyon ile değiştirilebilir.
    // ═══════════════════════════════════════════════════════════════════════════

    public static class PreviewSupportOps
    {
        // ── collect_structural_columns ────────────────────────────────────────
        [EgOp("collect_structural_columns",
            Description = "Yapısal kolonları toplar (collect_columns alias — dil bağımsız BIC tabanlı).",
            Category    = "Toplama",
            RequiresTransaction = false)]
        public static List<Element> CollectStructuralColumns(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] collect_structural_columns Revit bağlamı gerektirir.");

            var cols = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<Element>()
                .ToList();

            // OST_Columns da dahil et (mimari kolonlar)
            var archCols = new FilteredElementCollector(rctx.Doc)
                .OfCategory(BuiltInCategory.OST_Columns)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<Element>();

            cols.AddRange(archCols);

            ctx.Log($"  ✓ collect_structural_columns: {cols.Count} kolon");
            return cols;
        }

        // ── assign_poz_number ─────────────────────────────────────────────────
        [EgOp("assign_poz_number",
            Description = "Eleman listesine sıralı poz numarası atar. " +
                          "params: param_name (default:EGBIM_PozNo), prefix (default:\"\"), start_from (default:1).",
            Category    = "Parametre",
            RequiresTransaction = true)]
        public static int AssignPozNumber(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] assign_poz_number Revit bağlamı gerektirir.");

            var elements  = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var paramName = ctx.GetString("param_name", "EGBIM_PozNo");
            var prefix    = ctx.GetString("prefix", "");
            var startFrom = ctx.GetInt("start_from", 1);

            if (elements.Count == 0)
            {
                ctx.Log($"  ⚠ assign_poz_number: eleman listesi boş.");
                return 0;
            }

            int count = 0;
            using var scope = new Host.RevitWriteScope(
                rctx.Doc, $"assign_poz_number {paramName}", rctx.IsAtomicMode);

            for (int i = 0; i < elements.Count; i++)
            {
                var el    = elements[i];
                var poz   = $"{prefix}{startFrom + i}";
                var param = el.LookupParameter(paramName);

                if (param is null || param.IsReadOnly) continue;
                if (param.StorageType == StorageType.String)
                {
                    param.Set(poz);
                    count++;
                }
            }

            scope.Commit();
            ctx.Log($"  ✓ assign_poz_number '{paramName}': {count} eleman güncellendi (prefix='{prefix}', başlangıç={startFrom})");
            return count;
        }

        // ── export_row_report ────────────────────────────────────────────────
        [EgOp("export_row_report",
            Description = "Satır listesini veya eleman listesini Desktop'ta HTML rapor olarak kaydeder. " +
                          "params: title, fields (opsiyonel — satır anahtarları listesi).",
            Category    = "Çıktı",
            RequiresTransaction = false)]
        public static string ExportRowReport(OpContext ctx)
        {
            var title  = ctx.GetString("title", "EGBIMOTO Raporu");
            var fields = ctx.GetList<string>("fields"); // boş → tüm alanlar

            // Input: List<Dictionary> veya List<Element>
            var rows = new List<Dictionary<string, object?>>();

            if (ctx.Input is List<Dictionary<string, object?>> dictList)
            {
                rows = dictList;
            }
            else if (ctx.Input is List<Element> elements)
            {
                // Element listesi → minimal satır
                rows = elements.Select(e => new Dictionary<string, object?>
                {
                    ["element_id"] = Rv.GetId(e.Id),
                    ["kategori"]   = e.Category?.Name ?? "",
                    ["tip"]        = e.Name
                }).ToList();
            }
            else if (ctx.Input is int countResult)
            {
                ctx.Log($"  ⚠ export_row_report: input int ({countResult}) — rapor atlandı.");
                return "";
            }

            if (rows.Count == 0)
            {
                ctx.Log($"  ⚠ export_row_report: satır yok — rapor atlandı.");
                return "";
            }

            // Sütun başlıkları
            var headers = fields.Count > 0
                ? fields
                : rows[0].Keys.ToList();

            // HTML üret
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine($"<title>{title}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:'Segoe UI',sans-serif;margin:24px;background:#f5f5f5}");
            sb.AppendLine("h2{color:#2a2a3e}");
            sb.AppendLine("table{border-collapse:collapse;width:100%;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.1)}");
            sb.AppendLine("th{background:#2a2a3e;color:#fff;padding:10px;text-align:left;font-size:13px}");
            sb.AppendLine("td{border:1px solid #e0e0e0;padding:8px;font-size:12px}");
            sb.AppendLine("tr:nth-child(even){background:#f9f9f9}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h2>{title}</h2>");
            sb.AppendLine($"<p style='color:#888'>{rows.Count} kayıt — {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            sb.Append("<table><tr>");
            foreach (var h in headers)
                sb.Append($"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>");
            sb.AppendLine("</tr>");
            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var h in headers)
                {
                    var val = row.TryGetValue(h, out var v) ? v?.ToString() ?? "" : "";
                    sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(val)}</td>");
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table></body></html>");

            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"EGBIMOTO_{SanitizeTitle(title)}_{DateTime.Now:yyyyMMdd_HHmm}.html");

            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = path, UseShellExecute = true });

            ctx.Log($"  ✓ export_row_report: {path} ({rows.Count} satır)");
            return path;
        }

        // ── write_row_param ──────────────────────────────────────────────────
        [EgOp("write_row_param",
            Description = "Satır listesindeki her elemana params.value_key alanını params.param_name parametresine yazar. " +
                          "params: param_name, value_key (satırdaki alan adı). " +
                          "Input: List<Dictionary<string,object?>> — element_id ve value_key içermeli.",
            Category    = "Parametre",
            RequiresTransaction = true)]
        public static int WriteRowParam(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException(
                    $"[{ctx.CurrentStepId}] write_row_param Revit bağlamı gerektirir.");

            var rows      = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>(
                            new List<Dictionary<string, object?>>());
            var paramName = ctx.GetString("param_name");
            var valueKey  = ctx.GetString("value_key");

            if (string.IsNullOrWhiteSpace(paramName) || string.IsNullOrWhiteSpace(valueKey))
            {
                ctx.Log($"  ⚠ write_row_param: param_name veya value_key boş.");
                return 0;
            }

            int count = 0;
            using var scope = new Host.RevitWriteScope(
                rctx.Doc, $"write_row_param {paramName}", rctx.IsAtomicMode);

            foreach (var row in rows)
            {
                // element_id — long veya ElementId.Value
                if (!row.TryGetValue("element_id", out var eid)) continue;
                if (!long.TryParse(eid?.ToString(), out var idVal)) continue;

                var el = rctx.Doc.GetElement(Rv.MakeElementId(idVal));  // v6
                if (el is null) continue;

                var param = el.LookupParameter(paramName);
                if (param is null || param.IsReadOnly) continue;

                var value = row.TryGetValue(valueKey, out var v) ? v?.ToString() ?? "" : "";

                switch (param.StorageType)
                {
                    case StorageType.String:
                        param.Set(value); count++; break;
                    case StorageType.Integer:
                        if (int.TryParse(value, out var iv)) { param.Set(iv); count++; } break;
                    case StorageType.Double:
                        if (double.TryParse(value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var dv))
                        { param.Set(dv); count++; }
                        break;
                }
            }

            scope.Commit();
            ctx.Log($"  ✓ write_row_param '{paramName}'←'{valueKey}': {count} eleman güncellendi");
            return count;
        }

        // ── Yardımcı ──────────────────────────────────────────────────────────
        private static string SanitizeTitle(string title)
            => System.Text.RegularExpressions.Regex.Replace(title, @"[^\w\-]", "_");
    }
}
