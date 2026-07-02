using System;
using Autodesk.Revit.DB;

namespace EGBIMOTO.Addin.Host
{
    /// <summary>
    /// Yazma op'ları için Transaction / SubTransaction soyutlama katmanı.
    ///
    /// Normal mod  (IsAtomicMode=false) → Revit Transaction açar.
    ///   • Her op ayrı undo kaydı oluşturur.
    ///   • Kullanıcı Ctrl+Z ile adım adım geri alır.
    ///
    /// Atomic mod  (IsAtomicMode=true)  → SubTransaction kullanır.
    ///   • EgbimotoApp'in açtığı outer Transaction içinde çalışır.
    ///   • Herhangi bir op başarısız olursa tüm manifest rollback edilir.
    ///   • Kullanıcı tek Ctrl+Z ile tüm manifest'i geri alır.
    ///
    /// Kullanım (her yazma op'u):
    ///   var rctx = (RevitOpContext)ctx;
    ///   using var scope = new RevitWriteScope(rctx.Doc, "PozNo Ata", rctx.IsAtomicMode);
    ///   // ... model değişiklikleri
    ///   scope.Commit();
    ///   // Dispose: commit edilmediyse otomatik rollback
    /// </summary>
    internal sealed class RevitWriteScope : IDisposable
    {
        private readonly Transaction?    _tx;
        private readonly SubTransaction? _sub;
        private bool _committed;
        private bool _disposed;

        public RevitWriteScope(Document doc, string operationName, bool atomic)
        {
            if (atomic)
            {
                _sub = new SubTransaction(doc);
                var status = _sub.Start();
                if (status != TransactionStatus.Started)
                    throw new InvalidOperationException(
                        $"[RevitWriteScope] SubTransaction başlatılamadı: {status}. " +
                        "Outer atomic Transaction açık mı?");
            }
            else
            {
                _tx = new Transaction(doc, $"EGBIMOTO: {operationName}");
                var status = _tx.Start();
                if (status != TransactionStatus.Started)
                    throw new InvalidOperationException(
                        $"[RevitWriteScope] Transaction başlatılamadı: {status}.");
            }
        }

        public bool IsAtomic => _sub is not null;

        public void Commit()
        {
            if (_committed) return;
            _committed = true;
            _tx?.Commit();
            _sub?.Commit();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_committed)
            {
                try
                {
                    if (_tx?.GetStatus()  == TransactionStatus.Started) _tx.RollBack();
                    if (_sub?.GetStatus() == TransactionStatus.Started) _sub.RollBack();
                }
                catch { /* rollback sırasında exception bastır */ }
            }
            _tx?.Dispose();
            _sub?.Dispose();
        }
    }
}
