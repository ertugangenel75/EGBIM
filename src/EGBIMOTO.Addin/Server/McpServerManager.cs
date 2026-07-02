using System;
using Autodesk.Revit.UI;
using EGBIMOTO.Core.DAG;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Server
{
    /// <summary>
    /// EGBIMOTO MCP Server — Yaşam Döngüsü Yöneticisi.
    ///
    /// Server'ı EGBIMOTO'nun geri kalanına bağlayan TEK giriş noktası. Ribbon düğmesinden
    /// (veya istenirse OnStartup'tan) çağrılır. RevitDispatcher'ı ana thread'de başlatır,
    /// HTTP server'ı kurar ve EgbimotoApp.Registry + ContractsPath ile bağlar.
    ///
    /// KULLANIM (ribbon komutundan):
    ///     McpServerManager.Instance.Toggle();           // başlat/durdur
    ///     McpServerManager.Instance.Start(port: 5577);  // belirli portta başlat
    ///     bool aktif = McpServerManager.Instance.IsRunning;
    ///
    /// ÖNEMLİ: Start() ana thread'den (Revit komutu içinden) çağrılmalıdır — çünkü
    /// ExternalEvent.Create yalnızca ana thread'de çalışır.
    /// </summary>
    public sealed class McpServerManager
    {
        private static readonly Lazy<McpServerManager> _instance = new(() => new McpServerManager());
        public static McpServerManager Instance => _instance.Value;

        private RevitDispatcher? _dispatcher;
        private EgbimotoMcpServer? _server;
        private McpManifestRunner? _runner;

        private McpServerManager() { }

        public bool IsRunning => _server?.IsRunning ?? false;
        public int Port => _server?.Port ?? 0;

        /// <summary>
        /// Server'ı başlatır (ana thread'den çağrılmalı). Zaten çalışıyorsa hiçbir şey yapmaz.
        /// </summary>
        /// <param name="registry">Op registry (varsayılan: EgbimotoApp.Registry).</param>
        /// <param name="contractsPathProvider">op_contracts.json yolu (varsayılan: EgbimotoApp.ContractsPath).</param>
        /// <param name="port">Dinlenecek port (varsayılan 5577).</param>
        /// <param name="token">İsteğe bağlı erişim anahtarı.</param>
        public void Start(
            OpRegistry registry,
            Func<string> contractsPathProvider,
            int port = 5577,
            string? token = null)
        {
            if (IsRunning) return;

            // 1) Dispatcher'ı ana thread'de başlat (ExternalEvent.Create burada)
            _dispatcher = new RevitDispatcher();
            _dispatcher.Initialize();

            // 2) Manifest runner (DagExecutor köprüsü)
            _runner = new McpManifestRunner(registry);

            // 3) HTTP server
            _server = new EgbimotoMcpServer(
                _dispatcher,
                contractsPathProvider,
                (manifestJson, dispatcher) => _runner.Run(manifestJson, dispatcher),
                port,
                token);

            _server.Start();
        }

        public void Stop()
        {
            _server?.Stop();
            _server = null;
            _dispatcher = null;
            _runner = null;
        }

        /// <summary>Çalışıyorsa durdurur, durmuşsa başlatır. Sonuç durumunu döndürür.</summary>
        public bool Toggle(OpRegistry registry, Func<string> contractsPathProvider,
            int port = 5577, string? token = null)
        {
            if (IsRunning) { Stop(); return false; }
            Start(registry, contractsPathProvider, port, token);
            return true;
        }
    }
}
