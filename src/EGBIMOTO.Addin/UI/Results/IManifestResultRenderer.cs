using System.Collections.Generic;
using EGBIMOTO.Core.Results;

namespace EGBIMOTO.Addin.UI.Results
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — IManifestResultRenderer  (v13.5)
    //
    //  Genişletme noktası: bugün tek bir genel amaçlı renderer var
    //  (ManifestResultWindow — DataGrid + özet + "Modelde Göster" + CSV).
    //  İleride Kind bazlı özel görselleştirme (örn. Takeoff için pivot tablo,
    //  Schedule için Gantt) eklemek isteyen biri bu arayüzü uygulayıp
    //  ManifestResultRendererRegistry.Register() ile kaydeder — çağrı
    //  noktalarına (show_table, validation_summary vb.) dokunmadan.
    // ═══════════════════════════════════════════════════════════════════════════
    public interface IManifestResultRenderer
    {
        /// <summary>Bu renderer dto'yu gösterebilir mi? Registry sırayla sorar, ilk true kazanır.</summary>
        bool CanRender(ManifestResultDto dto);

        /// <summary>Sonucu göster. uidoc null olabilir (headless) — bu durumda renderer sessizce no-op yapmalı.</summary>
        void Render(Autodesk.Revit.UI.UIDocument? uidoc, ManifestResultDto dto);
    }

    /// <summary>
    /// Kayıtlı renderer'ları Kind/CanRender'a göre sıralı dener; hiçbiri
    /// uygun değilse varsayılan ManifestResultWindow'a düşer.
    /// </summary>
    public static class ManifestResultRendererRegistry
    {
        private static readonly List<IManifestResultRenderer> _renderers = new();

        static ManifestResultRendererRegistry()
        {
            // Varsayılan: her Kind'i gösterebilen genel amaçlı pencere.
            Register(new DefaultManifestResultRenderer());
        }

        public static void Register(IManifestResultRenderer renderer)
            => _renderers.Insert(0, renderer);   // sonradan kaydedilen önceliklidir

        public static void Show(Autodesk.Revit.UI.UIDocument? uidoc, ManifestResultDto dto)
        {
            foreach (var r in _renderers)
            {
                if (r.CanRender(dto))
                {
                    r.Render(uidoc, dto);
                    return;
                }
            }
        }

        private sealed class DefaultManifestResultRenderer : IManifestResultRenderer
        {
            public bool CanRender(ManifestResultDto dto) => true;
            public void Render(Autodesk.Revit.UI.UIDocument? uidoc, ManifestResultDto dto)
                => ManifestResultWindow.Show(uidoc, dto);
        }
    }
}
