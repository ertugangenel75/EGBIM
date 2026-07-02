using System;
using System.Windows.Threading;

namespace EGBIMOTO.Addin.UI.FamilyLibrary
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — DispatcherFrameHelper  (v14)
    //
    //  Revit API çağrıları (Application.OpenDocumentFile, Document.Close) YALNIZCA
    //  ana thread'de çalışabilir — bu yüzden FamilyLibraryScanner.ScanOne() döngüsü
    //  Task.Run/BackgroundWorker İLE ÇALIŞTIRILAMAZ. Ancak kullanıcının ilerleme
    //  çubuğunu görmesi ve "İptal" butonuna tıklayabilmesi için ana thread'in
    //  döngü sırasında UI mesajlarını işlemesi gerekir.
    //
    //  Standart WPF çözümü: Dispatcher.PushFrame ile iç içe bir mesaj döngüsü
    //  açıp hemen kapatmak (klasik "DoEvents" taklidi). Bu, WinForms'un
    //  Application.DoEvents()'ine WPF karşılığıdır ve uzun senkron Revit API
    //  döngülerinde yaygın kullanılan, güvenli bir idiyomdur.
    // ═══════════════════════════════════════════════════════════════════════════
    public static class DispatcherFrameHelper
    {
        public static void PumpUiMessages()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
