using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Core.DAG;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.Ops;
using EGBIMOTO.Addin.Host;

namespace EGBIMOTO.Addin.Server
{
    /// <summary>
    /// EGBIMOTO MCP Server — Manifest çalıştırma köprüsü.
    ///
    /// HTTP'den gelen manifest JSON'unu alır, RevitDispatcher ile ana thread'e marshal
    /// eder, orada DagExecutor ile çalıştırır ve sonucu JSON'a çevirip döndürür.
    ///
    /// EgbimotoApp'ın mevcut manifest-çalıştırma mantığını sarar; ribbon'dan tetiklenen
    /// akışla aynı OpRegistry ve RevitOpContext desenini kullanır (tek SSoT).
    ///
    /// NOT: Bu köprü "headless" çalışır — preview/onay kapıları (UserGateCallback)
    /// otomatik onaylanır, çünkü dış ajan akışında interaktif dialog yoktur. Atomik
    /// transaction politikası korunur (hata → rollback).
    /// </summary>
    public sealed class McpManifestRunner
    {
        private static readonly JsonSerializerOptions _parseOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly OpRegistry _registry;

        public McpManifestRunner(OpRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// EgbimotoMcpServer'a runManifestHandler olarak verilir.
        /// manifestJson → çalıştır → sonuç JSON (string).
        /// </summary>
        public string Run(string manifestJson, RevitDispatcher dispatcher)
        {
            // 1) Manifesti parse et (Revit gerektirmez, HTTP thread'de yapılabilir)
            EgManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<EgManifest>(manifestJson, _parseOpts);
            }
            catch (Exception ex)
            {
                return Err($"Manifest JSON parse edilemedi: {ex.Message}");
            }
            if (manifest == null || manifest.Steps.Count == 0)
                return Err("Manifest bos veya adim yok.");

            // 2) Çalıştırmayı ana thread'e marshal et
            object? boxed = dispatcher.Invoke(app =>
            {
                var uidoc = app.ActiveUIDocument;
                if (uidoc == null)
                    return (object)RunResult.Fail("Aktif Revit dokumani yok.");

                var doc = uidoc.Document;
                Transaction? outerTx = null;

                try
                {
                    // Atomik politika: tek dış transaction (hata → tümü geri alınır)
                    if (manifest.IsAtomic)
                    {
                        outerTx = new Transaction(doc, $"EGBIMOTO MCP: {Trunc(manifest.Title, 40)}");
                        if (outerTx.Start() != TransactionStatus.Started)
                            return (object)RunResult.Fail("Transaction baslatilamadi.");
                    }

                    var executor = new DagExecutor(_registry, () => new RevitOpContext
                    {
                        Doc = doc,
                        UiDoc = uidoc,
                        UiApp = app,
                        IsAtomicMode = manifest.IsAtomic,
                        OuterTransaction = outerTx,
                    });

                    // Headless: onay kapılarını otomatik onayla (dış ajan akışı)
                    executor.UserGateCallback = _ => true;
                    executor.UserScheduleGateCallback = _ => true;

                    if (manifest.IsAtomic && outerTx != null)
                    {
                        executor.OnAtomicCommit = () =>
                        {
                            if (outerTx.GetStatus() == TransactionStatus.Started) outerTx.Commit();
                        };
                        executor.OnAtomicRollback = () =>
                        {
                            if (outerTx.GetStatus() == TransactionStatus.Started) outerTx.RollBack();
                        };
                    }

                    var logLines = new List<string>();
                    DagRunResult result = executor.Run(manifest, line => logLines.Add(line));

                    // Atomik değilse ve hâlâ açık transaction varsa kapat
                    if (outerTx != null && outerTx.GetStatus() == TransactionStatus.Started)
                    {
                        if (result.Success) outerTx.Commit();
                        else outerTx.RollBack();
                    }

                    return (object)RunResult.From(result, logLines);
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (outerTx != null && outerTx.GetStatus() == TransactionStatus.Started)
                            outerTx.RollBack();
                    }
                    catch { }
                    return (object)RunResult.Fail($"Calistirma hatasi: {ex.Message}");
                }
            });

            var rr = boxed as RunResult ?? RunResult.Fail("Bilinmeyen sonuc.");
            return JsonSerializer.Serialize(rr, new JsonSerializerOptions { WriteIndented = false });
        }

        private static string Err(string msg)
            => JsonSerializer.Serialize(RunResult.Fail(msg));

        private static string Trunc(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n));

        /// <summary>HTTP yanıtına serialize edilen sonuç DTO'su.</summary>
        public sealed class RunResult
        {
            public bool success { get; set; }
            public string? error { get; set; }
            public string? error_step { get; set; }
            public int step_count { get; set; }
            public List<StepResult> steps { get; set; } = new();
            public List<string> log { get; set; } = new();

            public static RunResult Fail(string msg) => new() { success = false, error = msg };

            public static RunResult From(DagRunResult r, List<string> logLines)
            {
                var sr = new RunResult
                {
                    success = r.Success,
                    error = r.ErrorMessage,
                    error_step = r.ErrorStep,
                    log = logLines,
                };
                foreach (var t in r.Trace)
                {
                    sr.steps.Add(new StepResult
                    {
                        id = t.StepId,
                        op = t.Op,
                        success = t.Success,
                        ms = t.Ms,
                    });
                }
                sr.step_count = sr.steps.Count;
                return sr;
            }
        }

        public sealed class StepResult
        {
            public string id { get; set; } = "";
            public string op { get; set; } = "";
            public bool success { get; set; }
            public long ms { get; set; }
        }
    }
}
