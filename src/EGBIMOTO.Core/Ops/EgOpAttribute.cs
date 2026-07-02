using System;

namespace EGBIMOTO.Core.Ops
{
    /// <summary>
    /// Bu attribute ile işaretlenen her public static metod,
    /// OpRegistry tarafından otomatik olarak bulunur ve kaydedilir.
    ///
    /// Kullanım:
    ///   [EgOp("collect_walls", Description = "Duvarları toplar", Category = "Toplama")]
    ///   public static List<Element> CollectWalls(OpContext ctx) { ... }
    ///
    ///   // Yazma op'u — DagPlanner paralel çalıştırmaz
    ///   [EgOp("write_param", ..., RequiresTransaction = true)]
    ///   public static int WriteParam(OpContext ctx) { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class EgOpAttribute : Attribute
    {
        public string Name        { get; }
        public string Description { get; init; } = "";
        public string Category    { get; init; } = "Genel";

        /// <summary>
        /// true → Op Revit Transaction veya SubTransaction açar.
        /// DagPlanner bu op'u paralel seviyeye koymaz (ParallelSafe = false).
        /// Revit API thread-safe olmadığından bu flag şu an informational;
        /// gelecekteki parallel executor için hazırlık.
        ///
        /// Yazma op'larında (write_param, assign_egbim_mark vb.) true yapılır.
        /// Okuma ve hesap op'larında false (varsayılan).
        /// </summary>
        public bool RequiresTransaction { get; init; } = false;

        public EgOpAttribute(string name) => Name = name;
    }
}
