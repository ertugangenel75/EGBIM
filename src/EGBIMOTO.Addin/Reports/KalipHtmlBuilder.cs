using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EGBIMOTO.Addin.Reports
{
    /// <summary>
    /// Kalıp metraj HTML raporu.
    /// EGKalıp v25 rapor formatı temel alınarak geliştirildi:
    ///   - Üst özet kartları (toplam m², toplam TL, eleman sayısı)
    ///   - Poz bazlı özet tablo
    ///   - Kat bazlı özet tablo
    ///   - Element detay tablosu (açılır trace)
    ///   - Arama / filtreleme (JS)
    ///   - Baskı stili
    /// </summary>
    public static class KalipHtmlBuilder
    {
        private static readonly Dictionary<string, string> CatTr = new()
        {
            ["Structural Columns"]     = "Kolon",
            ["Structural Framing"]     = "Kiriş",
            ["Walls"]                  = "Duvar",
            ["Floors"]                 = "Döşeme",
            ["Floors_Edge"]            = "Döşeme Kenar",
            ["Structural Foundations"] = "Temel",
        };

        private static string Esc(object? v)
            => (v?.ToString() ?? "")
               .Replace("&", "&amp;").Replace("<", "&lt;")
               .Replace(">", "&gt;").Replace("\"", "&quot;");

        public static string Build(
            List<Dictionary<string, object?>> rows,
            Dictionary<string, List<string>> traces,
            Dictionary<string, Dictionary<string, object?>> pozIndex,
            string projName = "")
        {
            string now  = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            var mainRows = rows.Where(r =>
                r.TryGetValue("kategori", out var c) && c?.ToString() != "Floors_Edge").ToList();

            // ── İstatistikler ─────────────────────────────────────────────
            double totalM2   = rows.Sum(r => ToD(r, "kalip_m2"));
            double totalTL   = 0;
            int    elemCount = mainRows.Count;

            // Poz bazlı özet
            var pozOzet = rows
                .GroupBy(r => r.TryGetValue("poz_no", out var p) ? p?.ToString() ?? "—" : "—")
                .Select(g =>
                {
                    string pozNo = g.Key;
                    double m2    = g.Sum(r => ToD(r, "kalip_m2"));
                    double fiyat = 0.0;
                    string tanim = "—", birim = "m²";
                    if (pozIndex.TryGetValue(pozNo, out var pi))
                    {
                        tanim  = pi.TryGetValue("tanim",      out var ta) ? ta?.ToString() ?? "—" : "—";
                        birim  = pi.TryGetValue("birim",      out var bi) ? bi?.ToString() ?? "m²" : "m²";
                        double.TryParse(pi.TryGetValue("birim_fiyat", out var up) ?
                            up?.ToString() : "0", out fiyat);
                    }
                    double tutar = m2 * fiyat;
                    totalTL += tutar;
                    return new { pozNo, tanim, birim, m2, fiyat, tutar, cnt = g.Count() };
                })
                .OrderByDescending(x => x.tutar)
                .ToList();

            // Kat bazlı özet
            var katOzet = rows
                .GroupBy(r => r.TryGetValue("kat", out var k) ? k?.ToString() ?? "—" : "—")
                .Select(g => new
                {
                    kat  = g.Key,
                    m2   = g.Sum(r => ToD(r, "kalip_m2")),
                    cnt  = g.Count()
                })
                .OrderBy(x => x.kat)
                .ToList();

            // Kategori özeti
            var catOzet = rows
                .GroupBy(r => r.TryGetValue("kategori", out var c) ? c?.ToString() ?? "—" : "—")
                .Select(g => new
                {
                    cat = CatTr.TryGetValue(g.Key, out var tr) ? tr : g.Key,
                    m2  = g.Sum(r => ToD(r, "kalip_m2")),
                    cnt = g.Count()
                })
                .OrderByDescending(x => x.m2)
                .ToList();

            // ── HTML üretimi ──────────────────────────────────────────────
            var sb = new StringBuilder();
            sb.Append(HtmlHead(projName, now));

            // ── Başlık + kartlar ──────────────────────────────────────────
            sb.Append($@"
<body>
<div class='header'>
  <div class='header-left'>
    <div class='logo'>EGBIMOTO</div>
    <div>
      <h1>Kalıp Metraj Raporu</h1>
      <div class='meta'>
        <span>Proje: <strong>{Esc(projName)}</strong></span>
        <span>Tarih: {now}</span>
        <span>Toplam Eleman: {elemCount}</span>
      </div>
    </div>
  </div>
  <div class='header-right'>
    <button onclick='window.print()' class='btn-print'>🖨 Yazdır</button>
  </div>
</div>

<div class='cards'>
  <div class='card card-blue'>
    <div class='card-val'>{totalM2:N2}</div>
    <div class='card-lbl'>Toplam Kalıp (m²)</div>
  </div>
  <div class='card card-green'>
    <div class='card-val'>{totalTL:N0} ₺</div>
    <div class='card-lbl'>Toplam Maliyet (TL)</div>
  </div>
  <div class='card card-gray'>
    <div class='card-val'>{elemCount}</div>
    <div class='card-lbl'>Eleman Sayısı</div>
  </div>
  <div class='card card-gray'>
    <div class='card-val'>{pozOzet.Count}</div>
    <div class='card-lbl'>Poz Adedi</div>
  </div>
</div>
");

            // ── Kategori özeti ────────────────────────────────────────────
            sb.Append("<div class='section'><h2>Kategori Özeti</h2><table class='tbl'>");
            sb.Append("<thead><tr><th>Kategori</th><th>Eleman</th><th class='num'>Alan (m²)</th><th class='num'>Oran (%)</th></tr></thead><tbody>");
            foreach (var r in catOzet)
            {
                double pct = totalM2 > 0 ? r.m2 / totalM2 * 100 : 0;
                sb.Append($"<tr><td>{Esc(r.cat)}</td><td class='num'>{r.cnt}</td>" +
                           $"<td class='num'>{r.m2:N2}</td><td class='num'>{pct:F1}%</td></tr>");
            }
            sb.Append($"<tr class='foot'><td><strong>TOPLAM</strong></td><td class='num'><strong>{elemCount}</strong></td>" +
                      $"<td class='num'><strong>{totalM2:N2}</strong></td><td class='num'>100%</td></tr>");
            sb.Append("</tbody></table></div>");

            // ── Poz özeti ─────────────────────────────────────────────────
            sb.Append("<div class='section'><h2>Poz Bazlı Özet</h2><table class='tbl'>");
            sb.Append("<thead><tr><th>Poz No</th><th>Poz Tanımı</th><th>Birim</th>" +
                      "<th class='num'>Miktar</th><th class='num'>Birim Fiyat (₺)</th>" +
                      "<th class='num'>Tutar (₺)</th></tr></thead><tbody>");
            foreach (var p in pozOzet)
            {
                sb.Append($"<tr><td class='poz-no'>{Esc(p.pozNo)}</td>" +
                           $"<td>{Esc(p.tanim)}</td><td>{Esc(p.birim)}</td>" +
                           $"<td class='num'>{p.m2:N3}</td>" +
                           $"<td class='num'>{p.fiyat:N2}</td>" +
                           $"<td class='num'>{p.tutar:N2}</td></tr>");
            }
            sb.Append($"<tr class='foot'><td colspan='5'><strong>TOPLAM</strong></td>" +
                      $"<td class='num'><strong>{totalTL:N2}</strong></td></tr>");
            sb.Append("</tbody></table></div>");

            // ── Kat özeti ─────────────────────────────────────────────────
            sb.Append("<div class='section'><h2>Kat Özeti</h2><table class='tbl'>");
            sb.Append("<thead><tr><th>Kat</th><th class='num'>Eleman</th><th class='num'>Alan (m²)</th></tr></thead><tbody>");
            foreach (var k in katOzet)
                sb.Append($"<tr><td>{Esc(k.kat)}</td><td class='num'>{k.cnt}</td><td class='num'>{k.m2:N2}</td></tr>");
            sb.Append("</tbody></table></div>");

            // ── Eleman detayları ──────────────────────────────────────────
            sb.Append(@"
<div class='section'>
<h2>Eleman Detayları</h2>
<div class='toolbar'>
  <input type='text' id='srch' placeholder='Ara: ID, tip, kat, poz...' oninput='filterRows()'>
  <button onclick=""togAllTrace(true)"">Tüm Trace Aç</button>
  <button onclick=""togAllTrace(false)"">Tüm Trace Kapat</button>
</div>
<table class='tbl detail-tbl' id='detailTbl'>
<thead>
<tr>
  <th>ID</th><th>Kategori</th><th>Tip</th><th>Kat</th>
  <th class='num'>Alan (m²)</th><th class='num'>Tutar (₺)</th>
  <th>Yöntem</th><th>Poz No</th><th></th>
</tr>
</thead>
<tbody>
");
            foreach (var r in rows)
            {
                string eid  = Esc(r.TryGetValue("element_id", out var ei) ? ei : "");
                string cat  = CatTr.TryGetValue(r.TryGetValue("kategori", out var cv) ?
                    cv?.ToString() ?? "" : "", out var ctr) ? ctr : Esc(cv);
                string tip  = Esc(r.TryGetValue("tip",    out var ti) ? ti : "");
                string kat  = Esc(r.TryGetValue("kat",    out var ka) ? ka : "");
                double m2   = ToD(r, "kalip_m2");
                string pozNo= Esc(r.TryGetValue("poz_no", out var pn) ? pn : "");
                string meth = Esc(r.TryGetValue("method", out var mt) ? mt : "");
                double fiyat= 0;
                string rawPoz = r.TryGetValue("poz_no", out var rp) ? rp?.ToString() ?? "" : "";
                if (pozIndex.TryGetValue(rawPoz, out var pi2))
                    double.TryParse(pi2.TryGetValue("birim_fiyat", out var up2) ?
                        up2?.ToString() : "0", out fiyat);
                double tutar = m2 * fiyat;

                // Trace
                var traceLines = traces.TryGetValue(
                    r.TryGetValue("element_id", out var eidRaw) ? eidRaw?.ToString() ?? "" : "",
                    out var tl) ? tl : new List<string>();
                string traceHtml = traceLines.Count > 0
                    ? $"<div class='trace'><pre>{string.Join("\n", traceLines.Select(Esc))}</pre></div>"
                    : "<em style='color:#888'>Trace yok</em>";

                string rowClass = r.TryGetValue("kategori", out var catRaw) &&
                                  catRaw?.ToString() == "Floors_Edge" ? "edge-row" : "";

                sb.Append($@"
<tr class='data-row {rowClass}' data-search='{eid} {Esc(tip)} {Esc(kat)} {pozNo}'.toLowerCase()>
  <td class='eid'>{eid}</td>
  <td>{cat}</td>
  <td class='tip'>{tip}</td>
  <td>{kat}</td>
  <td class='num'>{m2:N3}</td>
  <td class='num'>{tutar:N2}</td>
  <td class='meth'>{meth}</td>
  <td class='poz-no'>{pozNo}</td>
  <td><button class='btn-trace' onclick=""togTrace('{eid}')"">▶ Detay</button></td>
</tr>
<tr id='trace_{eid}' class='trace-row' style='display:none'>
  <td colspan='9'>{traceHtml}</td>
</tr>
");
            }
            sb.Append("</tbody></table></div>");

            // ── Footer + JS ───────────────────────────────────────────────
            sb.Append(HtmlFoot(projName, now, totalM2, totalTL));
            return sb.ToString();
        }

        private static string HtmlHead(string proj, string now) => $@"<!DOCTYPE html>
<html lang='tr'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>EG Kalıp Raporu — {Esc(proj)}</title>
<style>
:root {{
  --blue:   #1a3a5c;
  --blue2:  #2563eb;
  --green:  #16a34a;
  --gray:   #6b7280;
  --bg:     #f3f4f6;
  --white:  #ffffff;
  --border: #e5e7eb;
  --trace-bg: #1e1e2e;
  --trace-fg: #cdd6f4;
}}
* {{ box-sizing:border-box; margin:0; padding:0; }}
body {{ font-family:'Segoe UI',Arial,sans-serif; font-size:13px;
        background:var(--bg); color:#111; padding:16px; }}

/* HEADER */
.header {{ display:flex; justify-content:space-between; align-items:center;
           background:var(--blue); color:#fff; padding:14px 20px;
           border-radius:8px; margin-bottom:16px; }}
.header-left {{ display:flex; align-items:center; gap:16px; }}
.logo {{ font-size:22px; font-weight:700; letter-spacing:2px;
         border:2px solid rgba(255,255,255,.4); padding:4px 10px;
         border-radius:4px; }}
h1 {{ font-size:17px; font-weight:600; }}
.meta {{ font-size:11px; color:rgba(255,255,255,.7); margin-top:4px; }}
.meta span {{ margin-right:16px; }}
.btn-print {{ background:transparent; border:1px solid rgba(255,255,255,.5);
              color:#fff; padding:6px 14px; border-radius:4px; cursor:pointer; }}
.btn-print:hover {{ background:rgba(255,255,255,.15); }}

/* CARDS */
.cards {{ display:flex; gap:12px; margin-bottom:20px; flex-wrap:wrap; }}
.card {{ flex:1; min-width:160px; background:var(--white); border-radius:8px;
         padding:14px 18px; border:1px solid var(--border);
         box-shadow:0 1px 3px rgba(0,0,0,.06); }}
.card-val {{ font-size:22px; font-weight:700; margin-bottom:4px; }}
.card-lbl {{ font-size:11px; color:var(--gray); text-transform:uppercase;
             letter-spacing:.5px; }}
.card-blue  .card-val {{ color:var(--blue2); }}
.card-green .card-val {{ color:var(--green); }}
.card-gray  .card-val {{ color:var(--gray); }}

/* SECTION */
.section {{ background:var(--white); border:1px solid var(--border);
            border-radius:8px; padding:16px; margin-bottom:16px;
            box-shadow:0 1px 3px rgba(0,0,0,.06); }}
h2 {{ font-size:14px; font-weight:600; color:var(--blue);
      border-bottom:2px solid var(--blue); padding-bottom:6px; margin-bottom:12px; }}

/* TABLE */
.tbl {{ width:100%; border-collapse:collapse; }}
.tbl th {{ background:var(--blue); color:#fff; padding:7px 10px;
           text-align:left; font-size:12px; white-space:nowrap; }}
.tbl td {{ padding:5px 10px; border-bottom:1px solid var(--border);
           vertical-align:top; }}
.tbl tbody tr:hover td {{ background:#eff6ff; }}
.tbl .foot td {{ background:#f8fafc; font-weight:600; border-top:2px solid var(--blue); }}
.num  {{ text-align:right; font-family:'Courier New',monospace; font-size:12px; }}
.eid  {{ font-size:11px; color:#9ca3af; font-family:monospace; }}
.tip  {{ font-size:12px; color:#374151; max-width:220px; overflow:hidden;
         text-overflow:ellipsis; white-space:nowrap; }}
.meth {{ font-size:10px; color:#9ca3af; font-family:monospace; }}
.poz-no {{ font-family:monospace; font-size:12px; font-weight:600; color:var(--blue2); }}
.edge-row td {{ background:#fefce8; }}

/* TRACE */
.trace-row td {{ padding:0 !important; }}
.trace {{ background:var(--trace-bg); border-radius:4px; margin:6px 10px;
          padding:10px 14px; }}
.trace pre {{ color:var(--trace-fg); font-size:11px; font-family:'Courier New',monospace;
              white-space:pre-wrap; line-height:1.5; }}

/* TOOLBAR */
.toolbar {{ display:flex; gap:8px; margin-bottom:10px; align-items:center; }}
.toolbar input {{ flex:1; padding:6px 10px; border:1px solid var(--border);
                  border-radius:4px; font-size:12px; }}
.toolbar button {{ padding:6px 12px; border:1px solid var(--blue); border-radius:4px;
                   background:var(--white); color:var(--blue); cursor:pointer;
                   font-size:12px; }}
.toolbar button:hover {{ background:var(--blue); color:#fff; }}
.btn-trace {{ font-size:11px; padding:3px 8px; border:1px solid var(--blue2);
              border-radius:3px; background:var(--white); color:var(--blue2);
              cursor:pointer; white-space:nowrap; }}
.btn-trace:hover {{ background:var(--blue2); color:#fff; }}

/* FOOTER */
.footer {{ text-align:center; color:var(--gray); font-size:11px;
           padding:12px; margin-top:8px; }}

/* PRINT */
@media print {{
  .btn-print,.toolbar {{ display:none; }}
  .trace-row {{ display:table-row !important; }}
  body {{ background:#fff; padding:0; }}
  .section {{ box-shadow:none; border:1px solid #ccc; page-break-inside:avoid; }}
}}
</style>
</head>
";

        private static string HtmlFoot(string proj, string now, double totalM2, double totalTL) => $@"
<div class='footer'>
  EGBIMOTO — EG Kalıp Metraj Raporu &nbsp;|&nbsp; {Esc(proj)} &nbsp;|&nbsp; {now}
  &nbsp;|&nbsp; Toplam {totalM2:N2} m² &nbsp;|&nbsp; {totalTL:N0} ₺
</div>

<script>
function togTrace(eid) {{
  var r = document.getElementById('trace_' + eid);
  if (!r) return;
  var btn = r.previousElementSibling.querySelector('.btn-trace');
  if (r.style.display === 'none') {{
    r.style.display = 'table-row';
    if (btn) btn.textContent = '▼ Detay';
  }} else {{
    r.style.display = 'none';
    if (btn) btn.textContent = '▶ Detay';
  }}
}}
function togAllTrace(show) {{
  document.querySelectorAll('.trace-row').forEach(function(r) {{
    r.style.display = show ? 'table-row' : 'none';
  }});
  document.querySelectorAll('.btn-trace').forEach(function(b) {{
    b.textContent = show ? '▼ Detay' : '▶ Detay';
  }});
}}
function filterRows() {{
  var q = document.getElementById('srch').value.toLowerCase();
  document.querySelectorAll('#detailTbl .data-row').forEach(function(row) {{
    var s = row.getAttribute('data-search') || '';
    var show = !q || s.toLowerCase().indexOf(q) >= 0;
    row.style.display = show ? '' : 'none';
    var tr = row.nextElementSibling;
    if (tr && tr.classList.contains('trace-row') && !show)
      tr.style.display = 'none';
  }});
}}
</script>
</body></html>
";

        private static double ToD(Dictionary<string, object?> r, string key)
        {
            if (r.TryGetValue(key, out var v) && double.TryParse(v?.ToString(), out var d)) return d;
            return 0.0;
        }
    }
}
