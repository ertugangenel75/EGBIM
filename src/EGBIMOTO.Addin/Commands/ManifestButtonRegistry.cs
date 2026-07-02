// ============================================================
// EGBIMOTO — ManifestButtonRegistry  (v11.1 — GERÇEK FIX)
// Buton "slot" indeksi → manifest dosya yolu eşlemesi.
//
// NEDEN v10.7'DEKİ ÇÖZÜM ÇALIŞMIYORDU:
//   v10.7'de Register(btnName, path) doğru çağrılıyordu, ama
//   ManifestRibbonCommand.Execute() btnName'i HÂLÂ
//   commandData.JournalData["EGBIMOTO_BTN"] üzerinden okumaya
//   çalışıyordu. Revit normal (replay olmayan) kullanımda
//   JournalData'yı doldurmaz — bu yüzden btnName her zaman null
//   geliyor, Resolve(null) her zaman null dönüyor, TÜM manifest
//   butonları "Manifest yolu bulunamadı" hatası veriyordu.
//   Sadece paylaşılan komut sınıfını kullanmayan butonlar
//   (Manifest Browser, MCP Server — kendi özel komut sınıflarına
//   sahipler) etkilenmiyordu.
//
// GERÇEK ÇÖZÜM:
//   Revit API'de aynı IExternalCommand sınıfını paylaşan birden
//   fazla PushButtonData arasında "hangisine basıldı" bilgisini
//   JournalData DIŞINDA almanın yolu yok. Bu yüzden JournalData'ya
//   güvenmek yerine, HER BUTON İÇİN AYRI, ince bir komut sınıfı
//   kullanıyoruz (bkz. ManifestRibbonCommandSlots.g.cs — sabit,
//   önceden üretilmiş SLOT_COUNT adet sınıf, her biri kendi index'ini
//   base constructor'a sabit olarak geçiyor). RibbonBuilder buton
//   oluştururken bir sonraki boş slot'u alır, PushButtonData'nın
//   className'ini o slot'un sınıf adına ayarlar ve
//   Register(slot, manifestPath) çağırır. Execute() zamanında hangi
//   sınıf instance'ı çalışıyorsa slot zaten kendi alanında sabit —
//   JournalData'ya hiç ihtiyaç yok.
// ============================================================

using System;
using System.Collections.Concurrent;

namespace EGBIMOTO.Addin.Commands
{
    /// <summary>
    /// Thread-safe, uygulama ömrü boyunca geçerli slot→manifest eşleme kaydı.
    /// </summary>
    public static class ManifestButtonRegistry
    {
        /// <summary>
        /// ManifestRibbonCommandSlots.g.cs içindeki üretilmiş sınıf sayısı.
        /// Ribbon'daki toplam manifest butonu (push + split içindeki her
        /// öğe) bu sayıyı aşarsa Allocate() İstisna fırlatır — sessizce
        /// buton kaybı yerine derleme/çalışma zamanında açık hata.
        /// Artırmak için ManifestRibbonCommandSlots.g.cs'i yeniden üretin.
        /// </summary>
        public const int SlotCount = 500;

        private const string SlotClassNamePrefix = "EGBIMOTO.Addin.Commands.ManifestRibbonCommand_";

        private static readonly ConcurrentDictionary<int, string> _pathBySlot
            = new ConcurrentDictionary<int, string>();

        private static readonly ConcurrentDictionary<int, string> _nameBySlot
            = new ConcurrentDictionary<int, string>();

        private static int _nextSlot = -1; // Interlocked.Increment ile 0'dan başlar

        /// <summary>
        /// RibbonBuilder çağırır — bir sonraki boş slot'u ayırır, manifest
        /// yolunu ve (log/debug için) buton adını kaydeder, o slot'a
        /// karşılık gelen tam sınıf adını döner (PushButtonData'nın
        /// className parametresinde doğrudan kullanılır).
        /// </summary>
        public static string Allocate(string manifestPath, string btnName)
        {
            int slot = System.Threading.Interlocked.Increment(ref _nextSlot);
            if (slot >= SlotCount)
                throw new InvalidOperationException(
                    $"ManifestButtonRegistry: slot havuzu doldu (SlotCount={SlotCount}). " +
                    $"Buton '{btnName}' için slot ayrılamadı. " +
                    "ManifestRibbonCommandSlots.g.cs'teki SLOT_COUNT'u artırıp yeniden üretin.");

            _pathBySlot[slot] = manifestPath;
            _nameBySlot[slot] = btnName;
            return $"{SlotClassNamePrefix}{slot:D4}";
        }

        /// <summary>
        /// ManifestRibbonCommandBase.Execute() çağırır — kendi sabit
        /// slot'una karşılık gelen manifest yolunu alır.
        /// </summary>
        public static string? Resolve(int slot)
            => _pathBySlot.TryGetValue(slot, out var path) ? path : null;

        /// <summary>Hata mesajlarında okunabilirlik için — slot'un hangi butona ait olduğu.</summary>
        public static string? ResolveButtonName(int slot)
            => _nameBySlot.TryGetValue(slot, out var name) ? name : null;

        /// <summary>Kaç slot kullanıldığını raporlar (tanılama/log amaçlı).</summary>
        public static int UsedSlotCount => Math.Max(0, _nextSlot + 1);
    }
}
