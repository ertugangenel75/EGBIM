using System;
using Autodesk.Revit.DB;

namespace EGBIMOTO.Addin.Host
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  RevitVersionAdapter  (Rv)  —  EGBIMOTO v6
    //
    //  Revit API sürüm kırılmalarını tek noktada yönetir.
    //  Tüm Op dosyaları Revit API'ye DOĞRUDAN dokunmak yerine Rv.* çağırır.
    //
    //  Derleme sabitleri (csproj'dan gelir):
    //    REVIT2024  → Revit 2024 API  (IntegerValue, new ElementId(int), ParameterType)
    //    REVIT2025  → Revit 2025 API  (.Value(long), new ElementId(long), GetDataType())
    //    REVIT2026  → Revit 2026 API  (2025 ile özdeş + olası yeni ekler)
    //
    //  Yeni Revit versiyonu geldiğinde YALNIZCA bu dosya güncellenir.
    //  Op dosyalarına (35+ .cs) dokunmak gerekmez.
    //
    //  Kullanım:
    //    long id        = Rv.GetId(element.Id);
    //    ElementId eid  = Rv.MakeElementId(longVal);
    //    int  bic       = Rv.GetCategoryId(element);
    //    string dtype   = Rv.GetParamDataType(definition);
    // ═══════════════════════════════════════════════════════════════════════════

    public static class Rv
    {
        // ── ElementId OKUMA ──────────────────────────────────────────────────
        //
        //  Revit 2024: ElementId.IntegerValue  (int)   ← deprecated in 2025
        //  Revit 2025: ElementId.Value          (long)  ← yeni standart
        //
        /// <summary>
        /// ElementId değerini long olarak döner.
        /// REVIT2024: .IntegerValue (int→long), REVIT2025+: .Value (long)
        /// </summary>
        public static long GetId(ElementId eid)
        {
#if REVIT2024
            return eid.IntegerValue;
#else
            return eid.Value;
#endif
        }

        /// <summary>ElementId → string (loglama, sözlük anahtarı).</summary>
        public static string IdStr(ElementId eid)
        {
            try { return GetId(eid).ToString(); }
            catch { return "?"; }
        }

        // ── ElementId OLUŞTURMA ──────────────────────────────────────────────
        //
        //  Revit 2024: new ElementId(int)  — long → (int) cast gerekir
        //  Revit 2025: new ElementId(long) — doğrudan long
        //
        /// <summary>
        /// long değerden ElementId üretir.
        /// REVIT2024: (int) cast ile, REVIT2025+: long overload ile.
        /// </summary>
        public static ElementId MakeElementId(long val)
        {
#if REVIT2024
            return new ElementId((int)val);
#else
            return new ElementId(val);
#endif
        }

        /// <summary>
        /// int değerden ElementId üretir (BIC, view ID gibi int kaynaklar).
        /// Tüm sürümlerde güvenli.
        /// </summary>
        public static ElementId MakeElementId(int val)
        {
#if REVIT2024
            return new ElementId(val);
#else
            return new ElementId((long)val);
#endif
        }

        // ── CATEGORY ID ──────────────────────────────────────────────────────
        //
        //  BuiltInCategory int değeri isteniyor — BIC karşılaştırmaları için.
        //  Revit 2024: .Category.Id.IntegerValue  (int)
        //  Revit 2025: (int)(Category.Id.Value)   (long→int cast)
        //
        /// <summary>
        /// Elemanın BuiltInCategory int değerini döner. Null-safe.
        /// REVIT2024: .IntegerValue, REVIT2025+: (int).Value
        /// </summary>
        public static int GetCategoryId(Element el)
        {
            if (el?.Category == null) return -1;
            try
            {
#if REVIT2024
                return el.Category.Id.IntegerValue;
#else
                return (int)el.Category.Id.Value;
#endif
            }
            catch { return -1; }
        }

        // ── WORKSET ID ───────────────────────────────────────────────────────
        //
        //  WorksetId.IntegerValue — Revit 2025'te kaldırılmamış, ancak adapter
        //  üzerinden çağırmak gelecekteki kırılmaları tek noktada yönetir.
        //
        /// <summary>
        /// WorksetId → int. ELEM_PARTITION_PARAM.Set(int) için kullanılır.
        /// Revit 2024/2025/2026: .IntegerValue (henüz değişmedi).
        /// </summary>
        public static int GetWorksetIntId(WorksetId wid)
        {
            // WorksetId.IntegerValue tüm desteklenen sürümlerde geçerlidir.
            // Autodesk bu property'yi deprecate ederse yalnızca burayı güncelle.
            return wid.IntegerValue;
        }

        /// <summary>
        /// ELEM_PARTITION_PARAM'ı verilen WorksetId ile günceller.
        /// Null ve read-only koruması dahil.
        /// </summary>
        public static void SetWorksetParam(Parameter? p, WorksetId wid)
        {
            if (p == null || p.IsReadOnly) return;
            p.Set(GetWorksetIntId(wid));
        }

        // ── PARAMETER DATA TYPE ──────────────────────────────────────────────
        //
        //  Revit 2024: ExternalDefinition.ParameterType (enum)  ← deprecated
        //  Revit 2025: Definition.GetDataType().TypeId   (string) ← yeni standart
        //
        /// <summary>
        /// Parametre veri tipini string olarak döner.
        /// REVIT2024: ParameterType.ToString(), REVIT2025+: GetDataType().TypeId
        /// </summary>
        public static string GetParamDataType(Definition def)
        {
            if (def == null) return "Unknown";
            try
            {
#if REVIT2024
                if (def is ExternalDefinition ed)
                    return ed.ParameterType.ToString();
                return "Unknown";
#else
                return def.GetDataType().TypeId;
#endif
            }
            catch { return "Unknown"; }
        }

        // ── InvalidElementId KISAYOLU ────────────────────────────────────────

        /// <summary>Platform-bağımsız InvalidElementId erişimi.</summary>
        public static ElementId InvalidId => ElementId.InvalidElementId;

        /// <summary>Verilen ElementId'nin geçersiz olup olmadığını kontrol eder.</summary>
        public static bool IsInvalid(ElementId? eid)
            => eid == null || eid == ElementId.InvalidElementId;
    }
}
