using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EGBIMOTO.Core.Ops;

namespace EGBIMOTO.Addin.Host
{
    /// <summary>
    /// Revit-bağımlı OpContext.
    /// Op'lar bu tipe cast ederek doc/uidoc/atomic durumuna erişir.
    ///
    /// Temel kullanım (op içinde):
    ///   var rctx = (RevitOpContext)ctx;
    ///   var doc  = rctx.Doc;
    ///
    /// Yazma op'u kullanımı (v3.1 — atomic aware):
    ///   using var scope = new RevitWriteScope(rctx.Doc, "PozNo Ata", rctx.IsAtomicMode);
    ///   // ... yazma işlemleri ...
    ///   scope.Commit();
    /// </summary>
    public sealed class RevitOpContext : OpContext
    {
        // ── Revit API erişimi ─────────────────────────────────────────────────
        public Document      Doc   { get; set; } = null!;
        public UIDocument    UiDoc { get; set; } = null!;
        public UIApplication UiApp { get; set; } = null!;

        /// <summary>
        /// Doc property'sinin alias'ı — bazı op'lar .Document ile erişiyor.
        /// Her ikisi de aynı nesneyi döndürür.
        /// </summary>
        public Document Document => Doc;

        // ── v3.1: Atomic Transaction Desteği ─────────────────────────────────

        /// <summary>
        /// true → manifest "transaction_policy": "atomic" ile çalışıyor.
        /// Yazma op'ları Transaction yerine SubTransaction açmalı.
        /// RevitWriteScope bu kararı otomatik verir.
        ///
        /// EgbimotoApp.RunManifest() tarafından set edilir.
        /// Op'lar bunu doğrudan değiştirmez.
        /// </summary>
        public bool IsAtomicMode { get; set; } = false;

        /// <summary>
        /// Atomic modda EgbimotoApp'in açtığı outer Transaction referansı.
        /// Op'ların bunu doğrudan kullanmasına gerek yok — RevitWriteScope yönetir.
        /// Sadece ileri seviye introspection için erişilebilir.
        /// </summary>
        public Transaction? OuterTransaction { get; set; }
    }
}
