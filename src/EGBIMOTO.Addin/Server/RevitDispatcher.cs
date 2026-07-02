using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace EGBIMOTO.Addin.Server
{
    /// <summary>
    /// EGBIMOTO MCP Server — Revit Dispatcher (ana thread marshalling)
    ///
    /// SORUN: Revit API'si YALNIZCA ana UI thread'inden çağrılabilir. HttpListener ise
    /// ayrı bir thread'de dinler. Bu yüzden gelen HTTP isteği doğrudan Revit API'sini
    /// çağıramaz — ana thread'e marshal edilmelidir.
    ///
    /// ÇÖZÜM: IExternalEventHandler + ExternalEvent. HTTP thread bir iş (Func) kuyruğa
    /// koyar ve ExternalEvent.Raise() çağırır; Revit uygun olduğunda Execute()'u ana
    /// thread'de çalıştırır. HTTP thread sonucu bir ManualResetEventSlim ile bekler.
    /// (RevitMCPGraphQL RevitDispatcher deseni; tek-handler + iş kuyruğu yaklaşımı.)
    ///
    /// Bu sınıf thread-safe bir "ana thread'de çalıştır ve sonucu bekle" köprüsüdür.
    /// </summary>
    public sealed class RevitDispatcher : IExternalEventHandler
    {
        // Bekleyen iş: ana thread'de çalışacak fonksiyon + sonucu taşıyan kutu
        private sealed class PendingJob
        {
            public Func<UIApplication, object?> Work = null!;
            public object? Result;
            public Exception? Error;
            public readonly ManualResetEventSlim Done = new(false);
        }

        private readonly object _gate = new();
        private readonly Queue<PendingJob> _queue = new();
        private ExternalEvent? _externalEvent;

        /// <summary>EGBIMOTO başlatılırken (ana thread'de) bir kez çağrılır.</summary>
        public void Initialize()
        {
            // ExternalEvent.Create yalnızca ana thread'de çağrılabilir.
            _externalEvent = ExternalEvent.Create(this);
        }

        public string GetName() => "EGBIMOTO MCP Dispatcher";

        /// <summary>
        /// HTTP thread'inden çağrılır. İşi kuyruğa koyar, ana thread'i uyandırır,
        /// sonucu bekler (timeout ile) ve döndürür. Revit API erişimi Work içinde olur.
        /// </summary>
        /// <param name="work">Ana thread'de çalışacak fonksiyon (UIApplication alır).</param>
        /// <param name="timeoutMs">Maksimum bekleme süresi (ms).</param>
        public object? Invoke(Func<UIApplication, object?> work, int timeoutMs = 120000)
        {
            if (_externalEvent == null)
                throw new InvalidOperationException(
                    "RevitDispatcher.Initialize() çağrılmadı (ExternalEvent null).");

            var job = new PendingJob { Work = work };
            lock (_gate) { _queue.Enqueue(job); }

            // Ana thread'i uyandır — Revit uygun olduğunda Execute() çağrılacak
            _externalEvent.Raise();

            // Sonucu bekle
            if (!job.Done.Wait(timeoutMs))
                throw new TimeoutException(
                    $"Revit ana thread {timeoutMs}ms içinde yanıt vermedi " +
                    "(Revit meşgul olabilir veya bir dialog açık olabilir).");

            if (job.Error != null)
                throw new InvalidOperationException(
                    $"Revit işlemi başarısız: {job.Error.Message}", job.Error);

            return job.Result;
        }

        /// <summary>
        /// Revit tarafından ANA THREAD'de çağrılır. Kuyruktaki tüm işleri çalıştırır.
        /// </summary>
        public void Execute(UIApplication app)
        {
            // Kuyruğu boşalt — Raise() birden çok iş için bir kez tetiklenebilir
            while (true)
            {
                PendingJob? job;
                lock (_gate)
                {
                    if (_queue.Count == 0) break;
                    job = _queue.Dequeue();
                }

                try
                {
                    job.Result = job.Work(app);
                }
                catch (Exception ex)
                {
                    job.Error = ex;
                }
                finally
                {
                    job.Done.Set(); // HTTP thread'i serbest bırak
                }
            }
        }
    }
}
