using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using EGBIMOTO.Core.Manifest;
using EGBIMOTO.Core.DAG;

namespace EGBIMOTO.Addin.Server
{
    /// <summary>
    /// EGBIMOTO MCP Server — localhost HTTP sunucusu.
    ///
    /// Dış AI ajanlarının (Claude Desktop, Python MCP köprüsü üzerinden) EGBIMOTO'nun
    /// op kataloğunu okumasını ve manifest çalıştırmasını sağlar.
    ///
    /// Endpoint'ler:
    ///   GET  /health    → server durumu + aktif doküman
    ///   GET  /ops       → op_contracts.json (ajan yetenek katalogu)
    ///   POST /run       → gövdedeki manifest JSON'unu DagExecutor ile çalıştırır
    ///   POST /validate  → manifesti çalıştırmadan doğrular
    ///
    /// GÜVENLİK: Yalnızca localhost (127.0.0.1) dinler — dışarıdan erişilemez.
    /// İsteğe bağlı token: X-EGBIMOTO-Token başlığı (ayarlanmışsa kontrol edilir).
    ///
    /// Revit erişimi: tüm model işlemleri RevitDispatcher üzerinden ana thread'e
    /// marshal edilir (HttpListener ayrı thread'de çalışır).
    /// </summary>
    public sealed class EgbimotoMcpServer
    {
        private readonly int _port;
        private readonly string? _token;
        private readonly RevitDispatcher _dispatcher;
        private readonly Func<string> _contractsPathProvider;
        private readonly Func<string, RevitDispatcher, string> _runManifestHandler;

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Thread? _listenThread;

        public bool IsRunning { get; private set; }
        public int Port => _port;

        /// <param name="dispatcher">Ana thread marshalling köprüsü (Initialize edilmiş).</param>
        /// <param name="contractsPathProvider">op_contracts.json yolunu döndüren fonksiyon.</param>
        /// <param name="runManifestHandler">Manifest JSON + dispatcher alıp sonuç JSON döndüren fonksiyon.</param>
        /// <param name="port">Dinlenecek port (varsayılan 5577).</param>
        /// <param name="token">İsteğe bağlı erişim anahtarı (null = kontrol yok).</param>
        public EgbimotoMcpServer(
            RevitDispatcher dispatcher,
            Func<string> contractsPathProvider,
            Func<string, RevitDispatcher, string> runManifestHandler,
            int port = 5577,
            string? token = null)
        {
            _dispatcher            = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _contractsPathProvider = contractsPathProvider ?? throw new ArgumentNullException(nameof(contractsPathProvider));
            _runManifestHandler    = runManifestHandler ?? throw new ArgumentNullException(nameof(runManifestHandler));
            _port                  = port;
            _token                 = string.IsNullOrWhiteSpace(token) ? null : token;
        }

        public void Start()
        {
            if (IsRunning) return;

            _listener = new HttpListener();
            // Yalnızca localhost — dışarıdan erişilemez
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Prefixes.Add($"http://localhost:{_port}/");

            try
            {
                _listener.Start();
            }
            catch (HttpListenerException ex)
            {
                throw new InvalidOperationException(
                    $"MCP Server port {_port} açılamadı. Port kullanımda olabilir " +
                    $"veya yetki gerekebilir. Hata: {ex.Message}", ex);
            }

            _cts = new CancellationTokenSource();
            IsRunning = true;

            _listenThread = new Thread(() => ListenLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "EGBIMOTO-MCP-Listener",
            };
            _listenThread.Start();
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
        }

        private void ListenLoop(CancellationToken ct)
        {
            while (IsRunning && !ct.IsCancellationRequested && _listener != null)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext(); // bloklar — istek gelene kadar bekler
                }
                catch
                {
                    break; // listener kapatıldı
                }

                // Her isteği havuz thread'inde işle (server bloklamasın)
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            string method = ctx.Request.HttpMethod.ToUpperInvariant();
            string path   = ctx.Request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(path)) path = "/";

            try
            {
                // Token kontrolü (ayarlanmışsa)
                if (_token != null)
                {
                    var provided = ctx.Request.Headers["X-EGBIMOTO-Token"];
                    if (provided != _token)
                    {
                        WriteJson(ctx, 401, new { error = "Gecersiz veya eksik token." });
                        return;
                    }
                }

                // Router
                if (method == "GET" && path == "/health")
                    HandleHealth(ctx);
                else if (method == "GET" && path == "/ops")
                    HandleOps(ctx);
                else if (method == "POST" && path == "/run")
                    HandleRun(ctx);
                else if (method == "POST" && path == "/validate")
                    HandleValidate(ctx);
                else
                    WriteJson(ctx, 404, new { error = $"Bilinmeyen endpoint: {method} {path}" });
            }
            catch (Exception ex)
            {
                try { WriteJson(ctx, 500, new { error = ex.Message }); } catch { }
            }
        }

        // ── GET /health ──────────────────────────────────────────────────────
        private void HandleHealth(HttpListenerContext ctx)
        {
            // Aktif dokümanı ana thread'den oku
            string docTitle = "?";
            try
            {
                var r = _dispatcher.Invoke(app =>
                    app.ActiveUIDocument?.Document?.Title ?? "(doküman yok)", 10000);
                docTitle = r as string ?? "?";
            }
            catch (Exception ex)
            {
                docTitle = $"(okunamadı: {ex.Message})";
            }

            WriteJson(ctx, 200, new
            {
                status = "ok",
                server = "EGBIMOTO MCP Server",
                version = "1.0",
                port = _port,
                active_document = docTitle,
            });
        }

        // ── GET /ops ─────────────────────────────────────────────────────────
        private void HandleOps(HttpListenerContext ctx)
        {
            var path = _contractsPathProvider();
            if (!File.Exists(path))
            {
                WriteJson(ctx, 500, new { error = $"op_contracts.json bulunamadi: {path}" });
                return;
            }
            // Doğrudan dosya içeriğini serve et (zaten geçerli JSON)
            var json = File.ReadAllText(path, Encoding.UTF8);
            WriteRaw(ctx, 200, json);
        }

        // ── POST /run ────────────────────────────────────────────────────────
        private void HandleRun(HttpListenerContext ctx)
        {
            string body = ReadBody(ctx);
            if (string.IsNullOrWhiteSpace(body))
            {
                WriteJson(ctx, 400, new { error = "Bos govde. Manifest JSON bekleniyor." });
                return;
            }

            // runManifestHandler tüm işi ana thread'e marshal eder ve sonuç JSON döndürür
            try
            {
                string resultJson = _runManifestHandler(body, _dispatcher);
                WriteRaw(ctx, 200, resultJson);
            }
            catch (Exception ex)
            {
                WriteJson(ctx, 500, new { success = false, error = ex.Message });
            }
        }

        // ── POST /validate ───────────────────────────────────────────────────
        private void HandleValidate(HttpListenerContext ctx)
        {
            string body = ReadBody(ctx);
            if (string.IsNullOrWhiteSpace(body))
            {
                WriteJson(ctx, 400, new { error = "Bos govde. Manifest JSON bekleniyor." });
                return;
            }

            try
            {
                // Sadece deserialize + op varlığı kontrolü (Revit gerektirmez)
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var manifest = JsonSerializer.Deserialize<EgManifest>(body, opts);
                if (manifest == null)
                {
                    WriteJson(ctx, 200, new { valid = false, errors = new[] { "Manifest parse edilemedi." } });
                    return;
                }

                var contractsPath = _contractsPathProvider();
                var knownOps = LoadOpNames(contractsPath);
                var errors = new List<string>();
                foreach (var step in manifest.Steps)
                {
                    if (string.IsNullOrWhiteSpace(step.Op))
                        errors.Add($"Adim '{step.Id}': op bos.");
                    else if (knownOps.Count > 0 && !knownOps.Contains(step.Op))
                        errors.Add($"Adim '{step.Id}': bilinmeyen op '{step.Op}'.");
                }

                WriteJson(ctx, 200, new
                {
                    valid = errors.Count == 0,
                    title = manifest.Title,
                    step_count = manifest.Steps.Count,
                    errors = errors,
                });
            }
            catch (Exception ex)
            {
                WriteJson(ctx, 200, new { valid = false, errors = new[] { ex.Message } });
            }
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static HashSet<string> LoadOpNames(string contractsPath)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (!File.Exists(contractsPath)) return set;
                using var doc = JsonDocument.Parse(File.ReadAllText(contractsPath, Encoding.UTF8));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    set.Add(prop.Name);
            }
            catch { }
            return set;
        }

        private static string ReadBody(HttpListenerContext ctx)
        {
            using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static void WriteJson(HttpListenerContext ctx, int status, object payload)
        {
            var opts = new JsonSerializerOptions { WriteIndented = false };
            WriteRaw(ctx, status, JsonSerializer.Serialize(payload, opts));
        }

        private static void WriteRaw(HttpListenerContext ctx, int status, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }
    }
}
