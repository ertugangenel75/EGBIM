// ============================================================
// EGBIMOTO — ManifestInputService  (v11)
//
// v11: Revit veri çekimi async yapıldı.
//   • PrepareManifest() — eski sync API korundu (geriye dönük)
//   • PrepareManifestAsync() — dialog açılır, arka planda
//     Task.Run(() => RevitLiveData) ile Level/View/FamilyType
//     çekilir; UI thread donmaz.
//
// NOT: FilteredElementCollector Revit API'sine ait ve
// ana thread gerektirir. Task.Run içinde Revit API çağrısı
// yapamazsınız. Bu yüzden veri çekimi Dispatcher.Invoke
// ile ana thread'e gönderilir, ancak dialog zaten açık
// olduğundan kullanıcı bunu hissetmez.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using EGBIMOTO.Core.Input;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Addin.UI
{
    public sealed class ManifestInputService
    {
        private readonly Document   _doc;
        private readonly Dispatcher _dispatcher;
        private readonly ManifestInputScanner _scanner = new();

        public ManifestInputService(Document doc, Dispatcher? dispatcher = null)
        {
            _doc        = doc;
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        // ── Eski sync API (geriye dönük uyumluluk) ───────────────────────────

        /// <summary>
        /// $INPUT token yoksa manifest'i değiştirmeden döner.
        /// Kullanıcı iptal ederse null döner.
        /// </summary>
        public EgManifest? PrepareManifest(EgManifest manifest)
        {
            var defs = _scanner.Scan(manifest);
            if (defs.Count == 0) return manifest;

            var dialog = new ManifestInputDialog(manifest, defs)
            {
                // Sync: koleksiyonlar anında doldurulur (büyük modelde donma riski)
                AvailableLevels         = GetLevels(),
                AvailableViews          = GetViews(),
                AvailableSheets         = GetSheets(),
                AvailableFamilyTypes    = GetFamilyTypes(),
                AvailableParameterNames = GetParameterNames(),
                // v11: async path kullanılmıyor
                LoadRevitDataAsync      = null,
            };

            if (dialog.ShowDialog() != true || !dialog.Confirmed) return null;
            return _scanner.ApplyResolved(manifest, defs);
        }

        // ── v11: Async API ────────────────────────────────────────────────────

        /// <summary>
        /// Dialog anında açılır, Revit veri çekimi arka planda çalışır.
        /// UI thread donmaz. Kullanıcı iptal ederse null döner.
        /// </summary>
        public EgManifest? PrepareManifestAsync(EgManifest manifest)
        {
            var defs = _scanner.Scan(manifest);
            if (defs.Count == 0) return manifest;

            // Revit API ana thread gerektirir — Task.Run içinde
            // Dispatcher.Invoke ile ana thread'e geçiriz.
            var dialog = new ManifestInputDialog(manifest, defs)
            {
                LoadRevitDataAsync = () => Task.Run(() =>
                {
                    // Dispatcher.Invoke: Revit API çağrıları ana thread'de
                    return _dispatcher.Invoke(() => new RevitLiveData
                    {
                        Levels         = GetLevels(),
                        Views          = GetViews(),
                        Sheets         = GetSheets(),
                        FamilyTypes    = GetFamilyTypes(),
                        ParameterNames = GetParameterNames(),
                    });
                })
            };

            if (dialog.ShowDialog() != true || !dialog.Confirmed) return null;
            return _scanner.ApplyResolved(manifest, defs);
        }

        // ── Revit veri çekici yardımcılar ────────────────────────────────────

        private List<string> GetLevels()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(l => l.Name)
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        private List<string> GetViews()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(View)).Cast<View>()
                    .Where(v => !v.IsTemplate && v.ViewType != ViewType.Schedule)
                    .OrderBy(v => v.Name)
                    .Select(v => v.Name)
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        private List<string> GetSheets()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                    .OrderBy(s => s.SheetNumber)
                    .Select(s => $"{s.SheetNumber} — {s.Name}")
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        private List<string> GetFamilyTypes()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                    .OrderBy(fs => fs.FamilyName).ThenBy(fs => fs.Name)
                    .Select(fs => $"{fs.FamilyName} : {fs.Name}")
                    .Distinct()
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        private List<string> GetParameterNames()
        {
            try
            {
                var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ParameterElement pe in new FilteredElementCollector(_doc).OfClass(typeof(ParameterElement)))
                    names.Add(pe.Name);
                foreach (SharedParameterElement sp in new FilteredElementCollector(_doc).OfClass(typeof(SharedParameterElement)))
                    names.Add(sp.Name);
                return names.OrderBy(n => n).ToList();
            }
            catch { return new List<string>(); }
        }
    }

    // v11: Revit canlı veri taşıyıcı — Task.Run içinde Dispatcher.Invoke ile dolduruluyor
    internal sealed class RevitLiveData
    {
        public List<string> Levels         { get; set; } = new();
        public List<string> Views          { get; set; } = new();
        public List<string> Sheets         { get; set; } = new();
        public List<string> FamilyTypes    { get; set; } = new();
        public List<string> ParameterNames { get; set; } = new();
    }

}
