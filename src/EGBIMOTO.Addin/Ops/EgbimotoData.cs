using System.IO;
using EGBIMOTO.Core.Data;

namespace EGBIMOTO.Addin.Ops
{
    /// <summary>
    /// Addin geneli DataRegistry singleton.
    /// EgbimotoApp.Initialize() tarafından ayarlanır.
    /// Op'lar buradan registry'ye ve data root'a erişir.
    /// </summary>
    public static class EgbimotoData
    {
        public static DataRegistry Registry { get; private set; } = new();
        public static string        DataRoot { get; private set; } = "";

        public static void Initialize(string addinDir)
        {
            DataRoot = Path.Combine(addinDir, "data");
            Registry = new DataRegistry();
            Registry.SetBasePath(DataRoot);
        }
    }
}
