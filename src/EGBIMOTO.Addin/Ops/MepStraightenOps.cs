// Copyright 2026 Ertuğan Genel
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// ─────────────────────────────────────────────────────────────────────────────
// NOTICE: Bu dosyadaki çift-dirsek (S-bend / 翻弯) tespit + düzleştirme algoritması,
// MIT lisanslı açık kaynak projeden uyarlanmıştır:
//   460707300-tech/MEPStraighten  (https://github.com/460707300-tech/MEPStraighten)
// Orijinal tek seferlik PickObject komutu, EGBIMOTO manifest/DAG op modeline
// (toplu tespit + rapor + opsiyonel düzleştirme) dönüştürülmüştür.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Addin.Host;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MepStraightenOps  —  EGBIMOTO v10
    //
    //  MEP hatlarındaki gereksiz "çift dirsek sapması" (S-bend / 翻弯) tespiti
    //  ve düzleştirilmesi. Bir hatta iki ardışık dirsek + aradaki segment
    //  bir paralel ofset oluşturuyorsa, dirsekler ve ara segment kaldırılıp
    //  ana hat düz uzatılır.
    //
    //  Boru (Pipe), kanal (Duct) ve kablo taşıyıcı (CableTray) destekler.
    //
    //  Op'lar:
    //    mep_straighten_scan   → Model/seçim genelinde S-bend tespiti (RAPOR, yazma yok)
    //    mep_straighten_apply  → Tespit edilen S-bendleri düzleştir (YAZMA)
    //
    //  Manifest örneği (önce tara, sonra düzleştir):
    //    { "id": "tara",  "op": "mep_straighten_scan",
    //      "params": { "categories": "OST_DuctCurves,OST_PipeCurves",
    //                  "min_offset_mm": 5, "max_offset_mm": 600 } }
    //    { "id": "duzelt", "op": "mep_straighten_apply",
    //      "input_from": "tara",
    //      "params": { "max_apply": 0 } }
    // ═══════════════════════════════════════════════════════════════════════════

    public static class MepStraightenOps
    {
        private const double FT_PER_MM = 1.0 / 304.8;
        private const double MM_PER_FT = 304.8;

        // Dirsek tespit eşiği: iki konektör yönü arasındaki açı.
        // |dot| < 0.985  → ~10°'den fazla sapma → dirsek.
        private const double ELBOW_DOT_THRESHOLD = 0.985;

        // ─────────────────────────────────────────────────────────────────────
        //  OP 1: mep_straighten_scan   (RAPOR — yazma yok)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("mep_straighten_scan",
            Description =
                "MEP hatlarındaki çift-dirsek sapmalarını (S-bend / 翻弯) tespit eder.\n" +
                "Yazma yapmaz — yalnızca rapor üretir. Düzleştirmek için mep_straighten_apply kullanın.\n\n" +
                "Input  : List<Element> (opsiyonel) — verilmezse categories ile toplanır.\n" +
                "params :\n" +
                "  categories     — taranacak kategoriler (virgül, default: OST_DuctCurves,OST_PipeCurves,OST_CableTray)\n" +
                "  min_offset_mm  — minimum ofset (bundan küçük sapmalar yok sayılır, default: 5)\n" +
                "  max_offset_mm  — maksimum ofset (bundan büyükse kasıtlı sayılır, default: 600)\n" +
                "  max_results    — maksimum bulgu (default: 2000)\n\n" +
                "Çıktı: List<Dictionary> — her satır bir S-bend:\n" +
                "  elbow_a_id, elbow_b_id, middle_ids, offset_mm, system_name,\n" +
                "  category, anchor_id, mover_id, center_x, center_y, center_z",
            Category = "MEP Koordinasyon")]
        public static List<Dictionary<string, object?>> MepStraightenScan(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[mep_straighten_scan] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            var cats        = ParseCategories(ctx.GetString("categories",
                                  "OST_DuctCurves,OST_PipeCurves,OST_CableTray"));
            double minOffFt = ctx.GetInt("min_offset_mm", 5)   * FT_PER_MM;
            double maxOffFt = ctx.GetInt("max_offset_mm", 600) * FT_PER_MM;
            int    maxRes   = ctx.GetInt("max_results", 2000);

            // Input önceliği: verilen eleman listesi, yoksa kategori taraması
            var inputElems = ctx.InputAsOrDefault<List<Element>>(new List<Element>());
            var lineSegments = inputElems.Count > 0
                ? inputElems.Where(IsLineSegment).ToList()
                : CollectByCategories(doc, cats).Where(IsLineSegment).ToList();

            ctx.Log($"[MepStraighten] Taranan hat segmenti: {lineSegments.Count}");

            var findings = new List<Dictionary<string, object?>>();
            var consumedElbows = new HashSet<long>();

            foreach (var seg in lineSegments)
            {
                if (findings.Count >= maxRes) break;

                var result = AnalyzeFromSegment(seg, consumedElbows);
                if (result == null) continue;

                double offFt = ComputeOffset(result);
                if (offFt < minOffFt || offFt > maxOffFt) continue;

                // Aynı dirsek çiftini iki kez raporlama
                long ea = Rv.GetId(result.ElbowA.Id);
                long eb = Rv.GetId(result.ElbowB.Id);
                if (consumedElbows.Contains(ea) || consumedElbows.Contains(eb)) continue;
                consumedElbows.Add(ea);
                consumedElbows.Add(eb);

                var center = MidpointOf(result.ElbowA, result.ElbowB);
                var sysName = GetSystemName(seg);

                findings.Add(new Dictionary<string, object?>
                {
                    ["elbow_a_id"]  = ea,
                    ["elbow_b_id"]  = eb,
                    ["middle_ids"]  = string.Join(",", result.MiddleElements.Select(m => Rv.GetId(m.Id))),
                    ["offset_mm"]   = Math.Round(offFt * MM_PER_FT, 1),
                    ["system_name"] = sysName,
                    ["category"]    = seg.Category?.Name ?? "?",
                    ["anchor_id"]   = result.OuterA != null ? Rv.GetId(result.OuterA.Id) : (object?)null,
                    ["mover_id"]    = result.OuterB != null ? Rv.GetId(result.OuterB.Id) : (object?)null,
                    ["center_x"]    = Math.Round(center.X, 4),
                    ["center_y"]    = Math.Round(center.Y, 4),
                    ["center_z"]    = Math.Round(center.Z, 4),
                });
            }

            ctx.Log($"[MepStraighten] {findings.Count} adet S-bend tespit edildi.");
            return findings;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OP 2: mep_straighten_apply   (YAZMA)
        // ─────────────────────────────────────────────────────────────────────

        [EgOp("mep_straighten_apply",
            Description =
                "Tespit edilen S-bendleri düzleştirir (dirsekleri + ara segmenti siler, ana hattı uzatır).\n" +
                "DİKKAT: Model değişikliği yapar. Önce mep_straighten_scan ile inceleyin.\n\n" +
                "Input  : mep_straighten_scan çıktısı (List<Dictionary>) — input_from ile bağlanır.\n" +
                "params :\n" +
                "  max_apply  — uygulanacak maksimum düzeltme (0 = sınırsız, default: 0)\n\n" +
                "Çıktı: List<Dictionary> — her satır bir uygulama sonucu:\n" +
                "  elbow_a_id, elbow_b_id, status (ok|skip|error), message",
            Category = "MEP Koordinasyon",
            RequiresTransaction = true)]
        public static List<Dictionary<string, object?>> MepStraightenApply(OpContext ctx)
        {
            var rctx = ctx as RevitOpContext
                ?? throw new InvalidOperationException("[mep_straighten_apply] RevitOpContext gerekli.");
            var doc = rctx.Doc;

            var findings = ctx.InputAsOrDefault<List<Dictionary<string, object?>>>(
                new List<Dictionary<string, object?>>());
            int maxApply = ctx.GetInt("max_apply", 0);

            var results = new List<Dictionary<string, object?>>();
            int applied = 0;

            using var scope = new RevitWriteScope(doc, "MEP S-bend Düzleştir", rctx.IsAtomicMode);

            foreach (var row in findings)
            {
                if (maxApply > 0 && applied >= maxApply) break;

                long eaId = ToLong(row.GetValueOrDefault("elbow_a_id"));
                long ebId = ToLong(row.GetValueOrDefault("elbow_b_id"));

                var res = new Dictionary<string, object?>
                {
                    ["elbow_a_id"] = eaId,
                    ["elbow_b_id"] = ebId,
                    ["status"]     = "skip",
                    ["message"]    = "",
                };

                try
                {
                    var elbowA = doc.GetElement(Rv.MakeElementId(eaId));
                    var elbowB = doc.GetElement(Rv.MakeElementId(ebId));
                    if (elbowA == null || elbowB == null)
                    {
                        res["message"] = "Dirsek bulunamadı (silinmiş olabilir).";
                        results.Add(res);
                        continue;
                    }

                    // Scan ile aynı analiz mantığını yeniden kur (taze geometri)
                    var anchor = ResolveElement(doc, row.GetValueOrDefault("anchor_id"));
                    var mover  = ResolveElement(doc, row.GetValueOrDefault("mover_id"));
                    var middleIds = ParseIdList(row.GetValueOrDefault("middle_ids") as string);

                    bool ok = ExecuteStraighten(doc, elbowA, elbowB, anchor, mover, middleIds);
                    if (ok)
                    {
                        res["status"]  = "ok";
                        res["message"] = "Düzleştirildi.";
                        applied++;
                    }
                    else
                    {
                        res["message"] = "Geometri uyumsuz — atlandı.";
                    }
                }
                catch (Exception ex)
                {
                    res["status"]  = "error";
                    res["message"] = ex.Message;
                }

                results.Add(res);
            }

            scope.Commit();
            ctx.Log($"[MepStraighten] {applied} adet S-bend düzleştirildi.");
            return results;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ANALİZ — segmentten başlayarak çift dirsek + ara segmenti bul
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class StraightenResult
        {
            public Element ElbowA = null!;
            public Element ElbowB = null!;
            public List<Element> MiddleElements = new();
            public Element? OuterA;
            public Element? OuterB;
        }

        private static StraightenResult? AnalyzeFromSegment(Element start, HashSet<long> consumed)
        {
            var conns = GetConnectors(start);
            if (conns.Count != 2) return null;

            var n1 = GetSingleNeighbor(start, conns[0]);
            var n2 = GetSingleNeighbor(start, conns[1]);
            if (n1 == null || n2 == null) return null;

            var leftPath  = WalkChain(n1, start.Id, 15);
            var rightPath = WalkChain(n2, start.Id, 15);

            leftPath.Reverse();
            var fullPath = new List<Element>();
            fullPath.AddRange(leftPath);
            fullPath.Add(start);
            fullPath.AddRange(rightPath);

            int idxStart = fullPath.FindIndex(e => e.Id == start.Id);

            var elbowIndices = new List<int>();
            for (int i = 0; i < fullPath.Count; i++)
                if (IsElbow(fullPath[i])) elbowIndices.Add(i);

            if (elbowIndices.Count < 2) return null;

            // Start segmentini saran en yakın iki dirsek
            int leftElbow = -1, rightElbow = -1;
            foreach (int ei in elbowIndices)
            {
                if (ei < idxStart && (leftElbow < 0 || ei > leftElbow))  leftElbow = ei;
                if (ei > idxStart && (rightElbow < 0 || ei < rightElbow)) rightElbow = ei;
            }
            if (leftElbow < 0 || rightElbow < 0) return null;
            if (rightElbow - leftElbow < 2) return null;

            var elbowA = fullPath[leftElbow];
            var elbowB = fullPath[rightElbow];

            // Ara elemanlarda dallanma varsa düzleştirme güvenli değil
            for (int i = leftElbow + 1; i < rightElbow; i++)
                if (CountTotalConnections(fullPath[i]) > 2) return null;

            var middle = fullPath.Skip(leftElbow + 1).Take(rightElbow - leftElbow - 1).ToList();

            var outerA = FindOuterLine(elbowA, fullPath[leftElbow + 1].Id);
            var outerB = FindOuterLine(elbowB, fullPath[rightElbow - 1].Id);

            // İki dış hat paralel mi (gerçek S-bend) yoksa köşe mi?
            if (outerA != null && outerB != null)
            {
                var dA = GetLineDirection(outerA);
                var dB = GetLineDirection(outerB);
                if (dA != null && dB != null && Math.Abs(dA.DotProduct(dB)) < ELBOW_DOT_THRESHOLD)
                    return null; // dış hatlar paralel değil → köşe, S-bend değil
            }

            return new StraightenResult
            {
                ElbowA = elbowA,
                ElbowB = elbowB,
                MiddleElements = middle,
                OuterA = outerA,
                OuterB = outerB,
            };
        }

        // İki dış hat arası dik ofset (S-bend yüksekliği)
        private static double ComputeOffset(StraightenResult r)
        {
            if (r.OuterA == null || r.OuterB == null) return 0;
            var dir = GetLineDirection(r.OuterA);
            if (dir == null) return 0;

            var pA = GetConnectorPosition(r.OuterA, r.ElbowA.Id);
            var pB = GetConnectorPosition(r.OuterB, r.ElbowB.Id);
            var gap = pA - pB;
            // dir bileşenini çıkar → dik ofset
            var perp = gap - dir * dir.DotProduct(gap);
            return perp.GetLength();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  UYGULAMA — dirsekleri + ara segmenti sil, ana hattı uzat
        // ═══════════════════════════════════════════════════════════════════════

        private static bool ExecuteStraighten(
            Document doc, Element elbowA, Element elbowB,
            Element? anchor, Element? mover, List<long> middleIds)
        {
            bool hasBoth = anchor != null && mover != null
                           && IsLineSegment(anchor) && IsLineSegment(mover);

            XYZ finalTarget = XYZ.Zero, anchorFar = XYZ.Zero;
            bool canAlign = false;

            if (hasBoth)
            {
                var anchorNear = GetConnectorPosition(anchor!, elbowA.Id);
                anchorFar = GetEndpointAwayFrom(anchor!, elbowA.Id);
                var anchorDir = (anchorNear - anchorFar).Normalize();

                var moverFar = GetEndpointAwayFrom(mover!, elbowB.Id);
                double dt = anchorDir.DotProduct(moverFar - anchorFar);
                finalTarget = anchorFar + dt * anchorDir;
                canAlign = true;
            }

            var moverFarFitting = mover != null ? FindFarFitting(mover, elbowB.Id) : null;

            // Silinecekler: iki dirsek + ara elemanlar + mover + mover'ın uzak fitting'i
            var protectedIds = new HashSet<long>();
            if (anchor != null) protectedIds.Add(Rv.GetId(anchor.Id));

            var toDelete = new List<ElementId> { elbowA.Id, elbowB.Id };
            foreach (var mid in middleIds)
            {
                if (protectedIds.Contains(mid)) continue;
                var el = doc.GetElement(Rv.MakeElementId(mid));
                if (el != null) toDelete.Add(el.Id);
            }
            if (mover != null && !protectedIds.Contains(Rv.GetId(mover.Id)))
                toDelete.Add(mover.Id);
            if (moverFarFitting != null)
                toDelete.Add(moverFarFitting.Id);

            foreach (var id in toDelete.Distinct())
            {
                try { doc.Delete(id); } catch { /* zaten silinmiş olabilir */ }
            }

            if (canAlign && anchor != null)
            {
                var line = CreateLineToEndpoint(anchorFar, finalTarget);
                if (line != null && anchor.Location is LocationCurve lc)
                {
                    try { lc.Curve = line; } catch { return false; }
                    return true;
                }
            }
            return false;
        }

        private static Line? CreateLineToEndpoint(XYZ far, XYZ near)
        {
            if (far.DistanceTo(near) < 0.001) return null; // < 1mm
            return Line.CreateBound(far, near);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  TİP KONTROLLERİ
        // ═══════════════════════════════════════════════════════════════════════

        private static bool IsLineSegment(Element e)
        {
            if (e is Pipe || e is Duct) return true;
            var cat = e?.Category;
            if (cat == null) return false;
            return Rv.GetCategoryId(e!) == (int)BuiltInCategory.OST_CableTray;
        }

        private static bool IsFitting(Element e)
        {
            if (e is not FamilyInstance) return false;
            int id = Rv.GetCategoryId(e);
            return id == (int)BuiltInCategory.OST_PipeFitting
                || id == (int)BuiltInCategory.OST_DuctFitting
                || id == (int)BuiltInCategory.OST_CableTrayFitting;
        }

        private static bool IsElbow(Element e)
        {
            if (!IsFitting(e)) return false;
            var conns = GetConnectors(e);
            return conns.Count == 2 && ChangesDirection(conns);
        }

        private static bool ChangesDirection(IList<Connector> conns)
        {
            if (conns.Count < 2) return false;
            var d1 = conns[0].CoordinateSystem.BasisZ.Normalize();
            var d2 = conns[1].CoordinateSystem.BasisZ.Normalize();
            return Math.Abs(d1.DotProduct(d2)) < ELBOW_DOT_THRESHOLD;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  KONEKTÖR / GEOMETRİ YARDIMCILARI
        // ═══════════════════════════════════════════════════════════════════════

        private static IList<Connector> GetConnectors(Element e)
        {
            if (e is FamilyInstance fi && fi.MEPModel?.ConnectorManager != null)
                return fi.MEPModel.ConnectorManager.Connectors.Cast<Connector>().ToList();
            if (e is Pipe pipe && pipe.ConnectorManager != null)
                return pipe.ConnectorManager.Connectors.Cast<Connector>().ToList();
            if (e is Duct duct && duct.ConnectorManager != null)
                return duct.ConnectorManager.Connectors.Cast<Connector>().ToList();

            // CableTray ve diğer MEP elemanları — reflection ile ConnectorManager
            var cm = e.GetType().GetProperty("ConnectorManager")?.GetValue(e);
            if (cm != null)
            {
                var connSet = cm.GetType().GetProperty("Connectors")?.GetValue(cm);
                if (connSet is System.Collections.IEnumerable ie)
                    return ie.Cast<Connector>().ToList();
            }
            return new List<Connector>();
        }

        private static int CountTotalConnections(Element e)
        {
            int count = 0;
            foreach (var conn in GetConnectors(e))
                foreach (Connector rc in conn.AllRefs)
                    if (rc.Owner != null && rc.Owner.Id != e.Id) count++;
            return count;
        }

        private static XYZ? GetLineDirection(Element line)
        {
            var curve = (line.Location as LocationCurve)?.Curve;
            if (curve == null) return null;
            return (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
        }

        private static Element? FindFarFitting(Element pipe, ElementId towardElbowId)
        {
            foreach (var conn in GetConnectors(pipe))
                foreach (Connector rc in conn.AllRefs)
                    if (rc.Owner != null
                        && rc.Owner.Id != pipe.Id
                        && rc.Owner.Id != towardElbowId
                        && IsFitting(rc.Owner))
                        return rc.Owner;
            return null;
        }

        private static XYZ GetConnectorPosition(Element elem, ElementId towardId)
        {
            foreach (var conn in GetConnectors(elem))
                foreach (Connector rc in conn.AllRefs)
                    if (rc.Owner?.Id == towardId)
                        return conn.Origin;
            var curve = (elem.Location as LocationCurve)?.Curve;
            return curve?.GetEndPoint(0) ?? XYZ.Zero;
        }

        private static XYZ GetEndpointAwayFrom(Element line, ElementId towardId)
        {
            var curve = (line.Location as LocationCurve)?.Curve;
            if (curve == null) return XYZ.Zero;
            var e0 = curve.GetEndPoint(0);
            var e1 = curve.GetEndPoint(1);
            var towardPt = GetConnectorPosition(line, towardId);
            return e0.DistanceTo(towardPt) < e1.DistanceTo(towardPt) ? e1 : e0;
        }

        private static Element? FindOuterLine(Element fitting, ElementId excludeId)
        {
            foreach (var conn in GetConnectors(fitting))
                foreach (Connector rc in conn.AllRefs)
                {
                    if (rc.Owner == null || rc.Owner.Id == excludeId) continue;
                    if (IsLineSegment(rc.Owner)) return rc.Owner;
                }
            return null;
        }

        private static XYZ MidpointOf(Element a, Element b)
        {
            var pa = (a.Location as LocationPoint)?.Point
                     ?? a.get_BoundingBox(null)?.Min ?? XYZ.Zero;
            var pb = (b.Location as LocationPoint)?.Point
                     ?? b.get_BoundingBox(null)?.Min ?? XYZ.Zero;
            return 0.5 * (pa + pb);
        }

        private static string GetSystemName(Element seg)
        {
            try
            {
                var p = seg.LookupParameter("System Name")
                        ?? seg.LookupParameter("Sistem Adı");
                if (p != null && p.HasValue) return p.AsString() ?? "?";
            }
            catch { }
            return "?";
        }

        // ── Zincir yürüme ────────────────────────────────────────────────────

        private static List<Element> WalkChain(Element current, ElementId cameFromId, int maxSteps)
        {
            var path = new List<Element>();
            var cur = current;
            var from = cameFromId;
            for (int i = 0; i < maxSteps; i++)
            {
                path.Add(cur);
                if (IsElbow(cur)) break;
                var next = GetNextInChain(cur, from);
                if (next == null) break;
                from = cur.Id;
                cur = next;
            }
            return path;
        }

        private static Element? GetSingleNeighbor(Element owner, Connector conn)
        {
            foreach (Connector rc in conn.AllRefs)
                if (rc.Owner != null && rc.Owner.Id != owner.Id)
                    return rc.Owner;
            return null;
        }

        private static Element? GetNextInChain(Element current, ElementId cameFromId)
        {
            foreach (var conn in GetConnectors(current))
                foreach (Connector rc in conn.AllRefs)
                    if (rc.Owner != null
                        && rc.Owner.Id != current.Id
                        && rc.Owner.Id != cameFromId)
                        return rc.Owner;
            return null;
        }

        // ── Kategori / id yardımcıları ───────────────────────────────────────

        private static List<BuiltInCategory> ParseCategories(string csv)
        {
            var list = new List<BuiltInCategory>();
            foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = raw.Trim();
                if (Enum.TryParse<BuiltInCategory>(name, out var bic))
                    list.Add(bic);
            }
            return list;
        }

        private static List<Element> CollectByCategories(Document doc, List<BuiltInCategory> cats)
        {
            var result = new List<Element>();
            foreach (var bic in cats)
            {
                try
                {
                    var col = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();
                    result.AddRange(col);
                }
                catch { /* geçersiz kategori atla */ }
            }
            return result;
        }

        private static Element? ResolveElement(Document doc, object? idObj)
        {
            if (idObj == null) return null;
            long id = ToLong(idObj);
            if (id == 0) return null;
            try { return doc.GetElement(Rv.MakeElementId(id)); }
            catch { return null; }
        }

        private static List<long> ParseIdList(string? csv)
        {
            var list = new List<long>();
            if (string.IsNullOrWhiteSpace(csv)) return list;
            foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (long.TryParse(raw.Trim(), out var v)) list.Add(v);
            return list;
        }

        private static long ToLong(object? o)
        {
            if (o == null) return 0;
            if (o is long l) return l;
            if (o is int i)  return i;
            if (o is double d) return (long)d;
            return long.TryParse(o.ToString(), out var v) ? v : 0;
        }
    }
}
