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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Core.Host;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Ops
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SystemOps  —  EGBIMOTO v9
    //
    //  Sistem sağlığı ve add-in yönetimi op'ları.
    //  rst-c / HealthScanner + AddinDisabler'dan uyarlama.
    //
    //  Kaynak: rst-c/src/RST.UI/Health/HealthScanner.cs
    //          rst-c/src/RST.Core/AddIns/AddinDisabler.cs
    //
    //  Op'lar:
    //    eg_health_snapshot      → Sistem + Revit sağlık raporu (HTML/JSON)
    //    eg_addin_scan           → Yüklü add-in listesi
    //    eg_addin_disable_unused → Zorunlu olmayan add-in'leri devre dışı bırak
    //    eg_addin_restore_all    → Tüm devre dışı add-in'leri geri yükle
    //    eg_addin_restore_single → Tek add-in'i geri yükle
    //
    //  Manifest örneği:
    //    { "id": "sys01", "op": "eg_health_snapshot",
    //      "inputs": { "format": "html", "open": true } }
    //
    //    { "id": "sys02", "op": "eg_addin_disable_unused",
    //      "inputs": { "keep": ["pyRevit.addin","EGBIMOTO.addin"] } }
    // ═══════════════════════════════════════════════════════════════════════════

    public static class SystemOps
    {
        // ────────────────────────────────────────────────────────────────────
        //  1. eg_health_snapshot
        // ────────────────────────────────────────────────────────────────────

        [EgOp("eg_health_snapshot",
            Description = "Sistem + Revit sağlık raporu üretir (RAM/CPU/Disk/OS/Revit/Warnings). " +
                          "format=html|json|text | open=true/false | out_path=<dosya>",
            Category    = "Sistem")]
        public static string EgHealthSnapshot(OpContext ctx)
        {
            var format   = ctx.GetString("format",   "html").ToLowerInvariant();
            var open     = ctx.GetBool("open",        false);
            var outPath  = ctx.GetString("out_path",  "");

            var rctx  = ctx as Host.RevitOpContext;
            var doc   = rctx?.Doc;
            var uiapp = rctx?.UiApp;

            // ── Veri topla ─────────────────────────────────────────────────
            var snap = CaptureSnapshot(doc, uiapp);

            // ── Biçimlendir ────────────────────────────────────────────────
            string content;
            string ext;
            switch (format)
            {
                case "json":
                    content = SnapToJson(snap);
                    ext     = ".json";
                    break;
                case "text":
                    content = SnapToText(snap);
                    ext     = ".txt";
                    break;
                default: // html
                    content = SnapToHtml(snap);
                    ext     = ".html";
                    break;
            }

            // ── Kaydet ────────────────────────────────────────────────────
            if (string.IsNullOrEmpty(outPath))
            {
                var tmp = Path.GetTempPath();
                outPath = Path.Combine(tmp, $"egbimoto_health_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
            }

            File.WriteAllText(outPath, content, Encoding.UTF8);
            ctx.Log($"[SystemOps] Sağlık raporu kaydedildi: {outPath}");

            if (open)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(outPath) { UseShellExecute = true }); }
                catch { ctx.Log("[SystemOps] Dosya açılamadı."); }
            }

            return outPath;
        }

        // ────────────────────────────────────────────────────────────────────
        //  2. eg_addin_scan
        // ────────────────────────────────────────────────────────────────────

        [EgOp("eg_addin_scan",
            Description = "Kurulu Revit add-in'leri listeler. " +
                          "revit_version=2026 (varsayılan: çalışan Revit) | include_disabled=true/false",
            Category    = "Sistem")]
        public static List<Dictionary<string, object?>> EgAddinScan(OpContext ctx)
        {
            var revitVer      = ctx.GetString("revit_version", GetRevitVersion(ctx));
            var inclDisabled  = ctx.GetBool("include_disabled", true);

            var manifests = EgAddinScanner.Scan(revitVer);
            var result    = new List<Dictionary<string, object?>>();

            foreach (var m in manifests)
            {
                if (!inclDisabled && m.IsDisabled) continue;

                var firstEntry = m.Entries.FirstOrDefault();
                result.Add(new Dictionary<string, object?>
                {
                    ["fileName"]   = m.FileName,
                    ["disabled"]   = m.IsDisabled,
                    ["directory"]  = m.Directory,
                    ["name"]       = firstEntry?.Name    ?? "",
                    ["addinId"]    = firstEntry?.AddinId ?? "",
                    ["type"]       = firstEntry?.Type    ?? "",
                    ["entryCount"] = m.Entries.Count,
                });
            }

            ctx.Log($"[SystemOps] {result.Count} add-in bulundu (Revit {revitVer}).");
            return result;
        }

        // ────────────────────────────────────────────────────────────────────
        //  3. eg_addin_disable_unused
        // ────────────────────────────────────────────────────────────────────

        [EgOp("eg_addin_disable_unused",
            Description = "Zorunlu olmayan add-in'leri .EGdisabled olarak yeniden adlandırır. " +
                          "keep=[\"pyRevit.addin\",\"EGBIMOTO.addin\"] — korunacak dosya adları. " +
                          "Revit yeniden başlatılana kadar etkili olmaz.",
            Category    = "Sistem",
            RequiresTransaction = false)]
        public static Dictionary<string, object?> EgAddinDisableUnused(OpContext ctx)
        {
            var revitVer = ctx.GetString("revit_version", GetRevitVersion(ctx));
            var keepRaw  = ctx.GetStringList("keep");

            // Kullanıcı listesi + EGBIMOTO her zaman korunur
            var keep = new List<string>(keepRaw) { "EGBIMOTO.addin", "EGBIMOTO.Bootstrap.addin" };

            ctx.Log($"[SystemOps] Add-in devre dışı bırakma başlıyor (Revit {revitVer})...");
            ctx.Log($"  Korunan: {string.Join(", ", keep)}");

            var result = EgAddinDisabler.DisableNonRequired(revitVer, keep,
                log: msg => ctx.Log(msg));

            return new Dictionary<string, object?>
            {
                ["disabled_count"]          = result.DisabledCount,
                ["skipped_readonly"]        = result.SkippedReadOnly,
                ["skipped_already_disabled"]= result.SkippedAlreadyDisabled,
                ["failed_count"]            = result.Failed,
                ["disabled_files"]          = result.DisabledFiles,
                ["failed_files"]            = result.FailedFiles,
                ["restart_required"]        = result.DisabledCount > 0,
            };
        }

        // ────────────────────────────────────────────────────────────────────
        //  4. eg_addin_restore_all
        // ────────────────────────────────────────────────────────────────────

        [EgOp("eg_addin_restore_all",
            Description = "Tüm .EGdisabled ve .RSTdisabled add-in'leri geri yükler. " +
                          "Revit yeniden başlatılana kadar etkili olmaz.",
            Category    = "Sistem",
            RequiresTransaction = false)]
        public static Dictionary<string, object?> EgAddinRestoreAll(OpContext ctx)
        {
            var revitVer = ctx.GetString("revit_version", GetRevitVersion(ctx));

            ctx.Log($"[SystemOps] Add-in geri yükleme başlıyor (Revit {revitVer})...");

            var result = EgAddinDisabler.RestoreAll(revitVer,
                log: msg => ctx.Log(msg));

            if (result.RestoredCount > 0)
                TaskDialog.Show("EGBIMOTO — Add-in Geri Yükleme",
                    $"{result.RestoredCount} add-in geri yüklendi.\n" +
                    $"Değişikliklerin geçerli olması için Revit'i yeniden başlatın.\n\n" +
                    string.Join("\n", result.RestoredFiles));

            return new Dictionary<string, object?>
            {
                ["restored_count"]  = result.RestoredCount,
                ["failed_count"]    = result.Failed,
                ["restored_files"]  = result.RestoredFiles,
                ["failed_files"]    = result.FailedFiles,
                ["restart_required"]= result.RestoredCount > 0,
            };
        }

        // ────────────────────────────────────────────────────────────────────
        //  5. eg_addin_restore_single
        // ────────────────────────────────────────────────────────────────────

        [EgOp("eg_addin_restore_single",
            Description = "Tek bir add-in'i geri yükler. " +
                          "inputs: { \"addin_file\": \"pyRevit.addin\" }",
            Category    = "Sistem",
            RequiresTransaction = false)]
        public static bool EgAddinRestoreSingle(OpContext ctx)
        {
            var revitVer   = ctx.GetString("revit_version", GetRevitVersion(ctx));
            var addinFile  = ctx.GetString("addin_file",   "");

            if (string.IsNullOrEmpty(addinFile))
                throw new InvalidOperationException("[eg_addin_restore_single] 'addin_file' parametresi zorunludur.");

            ctx.Log($"[SystemOps] Geri yükleniyor: {addinFile} (Revit {revitVer})");

            var ok = EgAddinDisabler.RestoreSingle(revitVer, addinFile,
                log: msg => ctx.Log(msg));

            if (ok)
                TaskDialog.Show("EGBIMOTO — Add-in Geri Yükleme",
                    $"{addinFile} geri yüklendi.\nRevit'i yeniden başlatın.");
            else
                TaskDialog.Show("EGBIMOTO — Hata",
                    $"{addinFile} bulunamadı veya geri yüklenemedi.");

            return ok;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Snapshot — Veri toplama
        // ═══════════════════════════════════════════════════════════════════

        private sealed class EgSnapshot
        {
            public string Timestamp    { get; set; } = "";
            public string MachineName  { get; set; } = "";
            public string UserName     { get; set; } = "";

            // RAM
            public long?  RamTotalMB   { get; set; }
            public long?  RamUsedMB    { get; set; }
            public int?   RamUsedPct   { get; set; }

            // CPU
            public string CpuName      { get; set; } = "";
            public int    CpuCores     { get; set; }
            public int?   CpuUsedPct   { get; set; }

            // Disk C:
            public double? DiskTotalGB  { get; set; }
            public double? DiskFreeGB   { get; set; }
            public double? DiskUsedPct  { get; set; }

            // OS
            public string OsVersion    { get; set; } = "";
            public string OsBuild      { get; set; } = "";

            // Revit
            public string RevitVersion  { get; set; } = "";
            public string RevitBuild    { get; set; } = "";
            public string ModelName     { get; set; } = "";
            public string ModelPath     { get; set; } = "";
            public double ModelSizeMb   { get; set; }
            public int    WarningCount  { get; set; }

            // Junk (temp dosyaları)
            public long   TempFilesCount { get; set; }
            public double TempSizeMb     { get; set; }
        }

        private static EgSnapshot CaptureSnapshot(Document? doc, UIApplication? uiapp)
        {
            var s = new EgSnapshot
            {
                Timestamp   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                MachineName = Environment.MachineName,
                UserName    = Environment.UserName,
                OsVersion   = Environment.OSVersion.VersionString,
            };

            // OS build
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                s.OsBuild = key?.GetValue("CurrentBuild") as string ?? "";
            }
            catch { }

            // CPU
            s.CpuCores = Environment.ProcessorCount;
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                s.CpuName = ((key?.GetValue("ProcessorNameString") as string) ?? "").Trim();
            }
            catch { }
            s.CpuUsedPct = SampleCpuPercent();

            // RAM — kernel32 GlobalMemoryStatusEx
            try
            {
                var ms = new MemoryStatusEx();
                if (GlobalMemoryStatusEx(ms))
                {
                    long total   = (long)(ms.ullTotalPhys / 1_048_576.0);
                    long avail   = (long)(ms.ullAvailPhys / 1_048_576.0);
                    s.RamTotalMB = total;
                    s.RamUsedMB  = total - avail;
                    s.RamUsedPct = (int)ms.dwMemoryLoad;
                }
            }
            catch { }

            // Disk C:
            try
            {
                var di = new DriveInfo("C");
                if (di.IsReady)
                {
                    double tot  = di.TotalSize / 1_073_741_824.0;
                    double free = di.AvailableFreeSpace / 1_073_741_824.0;
                    s.DiskTotalGB  = Math.Round(tot, 1);
                    s.DiskFreeGB   = Math.Round(free, 1);
                    s.DiskUsedPct  = tot > 0 ? Math.Round((1 - free / tot) * 100, 1) : 0;
                }
            }
            catch { }

            // Revit bilgisi
            if (uiapp != null)
            {
                try
                {
                    var app = uiapp.Application;
                    s.RevitVersion = app.VersionNumber;
                    s.RevitBuild   = app.VersionBuild;
                }
                catch { }
            }

            // Aktif model
            if (doc != null && !doc.IsDetached)
            {
                try
                {
                    s.ModelName     = doc.Title;
                    s.ModelPath     = doc.IsWorkshared ? doc.GetWorksharingCentralModelPath()?.CentralServerPath ?? doc.PathName : doc.PathName;
                    s.WarningCount  = doc.GetWarnings().Count;
                    if (!string.IsNullOrEmpty(s.ModelPath) && File.Exists(s.ModelPath))
                        s.ModelSizeMb = new FileInfo(s.ModelPath).Length / 1_048_576.0;
                }
                catch { }
            }

            // Temp klasör boyutu
            try
            {
                var tmp = Path.GetTempPath();
                var files = Directory.GetFiles(tmp, "*", SearchOption.TopDirectoryOnly);
                s.TempFilesCount = files.Length;
                s.TempSizeMb     = Math.Round(files.Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } }) / 1_048_576.0, 1);
            }
            catch { }

            return s;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Formatlama
        // ═══════════════════════════════════════════════════════════════════

        private static string SnapToJson(EgSnapshot s)
        {
            // System.Text.Json — manual serialize (Core'da Newtonsoft yok)
            var sb = new StringBuilder();
            sb.AppendLine("{");
            W(sb, "timestamp",      s.Timestamp);
            W(sb, "machine",        s.MachineName);
            W(sb, "user",           s.UserName);
            W(sb, "os_version",     s.OsVersion);
            W(sb, "os_build",       s.OsBuild);
            W(sb, "cpu_name",       s.CpuName);
            WN(sb, "cpu_cores",     s.CpuCores);
            WN(sb, "cpu_used_pct",  s.CpuUsedPct);
            WN(sb, "ram_total_mb",  s.RamTotalMB);
            WN(sb, "ram_used_mb",   s.RamUsedMB);
            WN(sb, "ram_used_pct",  s.RamUsedPct);
            WN(sb, "disk_total_gb", s.DiskTotalGB);
            WN(sb, "disk_free_gb",  s.DiskFreeGB);
            WN(sb, "disk_used_pct", s.DiskUsedPct);
            W(sb,  "revit_version", s.RevitVersion);
            W(sb,  "revit_build",   s.RevitBuild);
            W(sb,  "model_name",    s.ModelName);
            W(sb,  "model_path",    s.ModelPath);
            WN(sb, "model_size_mb", (double?)s.ModelSizeMb);
            WN(sb, "warning_count", (int?)s.WarningCount);
            WN(sb, "temp_files",    (long?)s.TempFilesCount);
            WN(sb, "temp_size_mb",  (double?)s.TempSizeMb, last: true);
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void W(StringBuilder sb, string k, string v, bool last = false)
            => sb.AppendLine($"  \"{k}\": \"{v.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"{(last ? "" : ",")}");
        private static void WN(StringBuilder sb, string k, object? v, bool last = false)
            => sb.AppendLine($"  \"{k}\": {v?.ToString() ?? "null"}{(last ? "" : ",")}");

        private static string SnapToText(EgSnapshot s)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"EGBIMOTO — Sistem Sağlık Raporu");
            sb.AppendLine($"Tarih     : {s.Timestamp}");
            sb.AppendLine($"Makine    : {s.MachineName}  Kullanıcı: {s.UserName}");
            sb.AppendLine();
            sb.AppendLine($"İŞLETİM SİSTEMİ");
            sb.AppendLine($"  Sürüm   : {s.OsVersion}  Build: {s.OsBuild}");
            sb.AppendLine();
            sb.AppendLine($"CPU");
            sb.AppendLine($"  {s.CpuName}  Çekirdek: {s.CpuCores}  Kullanım: {s.CpuUsedPct?.ToString() ?? "?"} %");
            sb.AppendLine();
            sb.AppendLine($"RAM");
            sb.AppendLine($"  Toplam: {s.RamTotalMB?.ToString() ?? "?"} MB  Kullanılan: {s.RamUsedMB?.ToString() ?? "?"} MB  ({s.RamUsedPct?.ToString() ?? "?"} %)");
            sb.AppendLine();
            sb.AppendLine($"DİSK (C:)");
            sb.AppendLine($"  Toplam: {s.DiskTotalGB?.ToString("F1") ?? "?"} GB  Boş: {s.DiskFreeGB?.ToString("F1") ?? "?"} GB  Kullanılan: {s.DiskUsedPct?.ToString("F1") ?? "?"} %");
            sb.AppendLine();
            sb.AppendLine($"REVİT");
            sb.AppendLine($"  Sürüm: {s.RevitVersion}  Build: {s.RevitBuild}");
            sb.AppendLine($"  Model: {s.ModelName}");
            if (!string.IsNullOrEmpty(s.ModelPath)) sb.AppendLine($"  Yol  : {s.ModelPath}");
            sb.AppendLine($"  Boyut: {s.ModelSizeMb:F1} MB  Uyarılar: {s.WarningCount}");
            sb.AppendLine();
            sb.AppendLine($"TEMP KLASÖRÜ");
            sb.AppendLine($"  Dosya: {s.TempFilesCount}  Boyut: {s.TempSizeMb:F1} MB");
            return sb.ToString();
        }

        private static string SnapToHtml(EgSnapshot s)
        {
            var ram  = s.RamUsedPct ?? 0;
            var cpu  = s.CpuUsedPct ?? 0;
            var disk = (int)(s.DiskUsedPct ?? 0);

            string Bar(int pct, string color)
                => $"<div style='background:#2a2a3a;border-radius:4px;height:12px;width:100%;'>" +
                   $"<div style='background:{color};width:{pct}%;height:12px;border-radius:4px;'></div></div>";

            return $@"<!DOCTYPE html>
<html lang=""tr""><head><meta charset=""utf-8"">
<title>EGBIMOTO — Sistem Sağlık Raporu</title>
<style>
  body{{font-family:Segoe UI,sans-serif;background:#1a1a2e;color:#e0e0e0;margin:0;padding:20px;}}
  h1{{color:#4fc3f7;font-size:18px;border-bottom:1px solid #333;padding-bottom:8px;}}
  h2{{color:#81c784;font-size:13px;margin:16px 0 6px;text-transform:uppercase;letter-spacing:1px;}}
  .grid{{display:grid;grid-template-columns:1fr 1fr;gap:12px;}}
  .card{{background:#23233a;border-radius:8px;padding:14px;}}
  .label{{color:#9e9e9e;font-size:11px;}}
  .value{{font-size:15px;font-weight:600;color:#ffffff;margin-bottom:4px;}}
  .warn{{color:#ffb74d;}} .ok{{color:#81c784;}} .info{{color:#4fc3f7;}}
  .bar{{margin-top:6px;}}
  table{{width:100%;border-collapse:collapse;font-size:12px;}}
  td,th{{padding:4px 8px;border-bottom:1px solid #2a2a3a;}}
  th{{color:#9e9e9e;text-align:left;}}
</style></head><body>
<h1>⚙ EGBIMOTO — Sistem Sağlık Raporu</h1>
<p class=""label"">📅 {s.Timestamp} | 💻 {s.MachineName} | 👤 {s.UserName}</p>

<div class=""grid"">

<div class=""card"">
  <h2>RAM</h2>
  <div class=""value {(ram > 85 ? "warn" : "ok")}"">Toplam: {s.RamTotalMB?.ToString() ?? "?"} MB</div>
  <div class=""label"">Kullanılan: {s.RamUsedMB?.ToString() ?? "?"} MB ({ram} %)</div>
  <div class=""bar"">{Bar(ram, ram > 85 ? "#ff7043" : "#66bb6a")}</div>
</div>

<div class=""card"">
  <h2>CPU</h2>
  <div class=""value"">{(s.CpuName.Length > 40 ? s.CpuName.Substring(0, 40) + "…" : s.CpuName)}</div>
  <div class=""label"">Çekirdek: {s.CpuCores} | Kullanım: {cpu} %</div>
  <div class=""bar"">{Bar(cpu, cpu > 90 ? "#ff7043" : "#42a5f5")}</div>
</div>

<div class=""card"">
  <h2>Disk (C:)</h2>
  <div class=""value {(disk > 85 ? "warn" : "ok")}"">Toplam: {s.DiskTotalGB?.ToString("F1") ?? "?"} GB</div>
  <div class=""label"">Boş: {s.DiskFreeGB?.ToString("F1") ?? "?"} GB ({disk} % dolu)</div>
  <div class=""bar"">{Bar(disk, disk > 85 ? "#ff7043" : "#ffa726")}</div>
</div>

<div class=""card"">
  <h2>İşletim Sistemi</h2>
  <div class=""value"">{s.OsVersion}</div>
  <div class=""label"">Build: {s.OsBuild}</div>
</div>

</div>

<div class=""card"" style=""margin-top:12px;"">
  <h2>Revit</h2>
  <table>
    <tr><th>Sürüm</th><td class=""info"">{s.RevitVersion}</td><th>Build</th><td>{s.RevitBuild}</td></tr>
    <tr><th>Model</th><td colspan=""3"">{s.ModelName}</td></tr>
    <tr><th>Yol</th><td colspan=""3"" style=""font-size:10px;color:#9e9e9e"">{s.ModelPath}</td></tr>
    <tr><th>Model Boyutu</th><td>{s.ModelSizeMb:F1} MB</td>
        <th>Uyarılar</th><td class=""{(s.WarningCount > 0 ? "warn" : "ok")}"">
          {s.WarningCount} {(s.WarningCount > 0 ? "⚠" : "✓")}</td></tr>
  </table>
</div>

<div class=""card"" style=""margin-top:12px;"">
  <h2>Temp Klasörü</h2>
  <div class=""label"">Dosya sayısı: <b>{s.TempFilesCount}</b> | Boyut: <b>{s.TempSizeMb:F1} MB</b></div>
</div>

<p style=""color:#555;font-size:10px;margin-top:16px;"">EGBIMOTO v9 — EGBIM © {DateTime.Now.Year}</p>
</body></html>";
        }

        // ═══════════════════════════════════════════════════════════════════
        //  P/Invoke — GlobalMemoryStatusEx (kernel32)
        // ═══════════════════════════════════════════════════════════════════

        [StructLayout(LayoutKind.Sequential)]
        private sealed class MemoryStatusEx
        {
            public uint  dwLength       = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            public uint  dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

        // ── CPU kullanım örneği ───────────────────────────────────────────

        private static int? SampleCpuPercent()
        {
            // GetSystemTimes ile iki noktalı ölçüm
            try
            {
                GetSystemTimes(out long idle1, out long kernel1, out long user1);
                System.Threading.Thread.Sleep(200);
                GetSystemTimes(out long idle2, out long kernel2, out long user2);

                long idleDelta   = idle2   - idle1;
                long kernelDelta = kernel2 - kernel1;
                long userDelta   = user2   - user1;
                long total = kernelDelta + userDelta;
                if (total <= 0) return null;
                int pct = (int)((total - idleDelta) * 100L / total);
                return Math.Max(0, Math.Min(100, pct));
            }
            catch { return null; }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(
            out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        // ── Yardımcılar ───────────────────────────────────────────────────

        private static string GetRevitVersion(OpContext ctx)
        {
            try { return (ctx as Host.RevitOpContext)?.UiApp?.Application?.VersionNumber ?? "2026"; }
            catch { return "2026"; }
        }
    }
}
