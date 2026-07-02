using System.Collections.Generic;

namespace EGBIMOTO.Addin.FamilyLibrary
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — FamilyScanResult  (v14)
    //
    //  FamilyLibraryScanner'ın ürettiği sonuç modelleri. Revit API'ye
    //  bağımlı olmadığı için (yalnızca string/enum/Guid taşır) WPF binding'de
    //  serbestçe kullanılabilir.
    // ═══════════════════════════════════════════════════════════════════════════

    public enum ParamComplianceStatus
    {
        /// <summary>Paylaşımlı ve GUID, SSoT (param_guid_map.json) ile eşleşiyor.</summary>
        Ok,
        /// <summary>Aynı isimde ama GUID SSoT'ten FARKLI — modele yüklenirse "duplicate parameter" birleşme riski.</summary>
        GuidConflict,
        /// <summary>İsim TR_/EG_ ile başlıyor ama paylaşımlı değil (aile-özel parametre olarak kalmış).</summary>
        NotSharedButLooksTr,
        /// <summary>Paylaşımlı ama SSoT'te bu isim hiç yok (bilinmeyen/eski parametre).</summary>
        UnknownShared,
        /// <summary>TR_/EG_ dışı, paylaşımlı olması beklenmeyen sıradan parametre.</summary>
        Irrelevant,
    }

    public sealed class FamilyParamStatus
    {
        public string Name { get; set; } = "";
        public bool IsShared { get; set; }
        public string Guid { get; set; } = "";
        public ParamComplianceStatus Status { get; set; }
    }

    public enum FamilyOverallStatus { Compliant, Warning, Conflict, OpenFailed }

    public sealed class FamilyScanResult
    {
        public string FilePath { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public List<FamilyParamStatus> Params { get; set; } = new();
        public FamilyOverallStatus Overall { get; set; } = FamilyOverallStatus.Compliant;
        public string? OpenError { get; set; }

        public int ConflictCount => Params.FindAll(p => p.Status == ParamComplianceStatus.GuidConflict).Count;
        public int WarningCount  => Params.FindAll(p =>
            p.Status is ParamComplianceStatus.NotSharedButLooksTr or ParamComplianceStatus.UnknownShared).Count;
    }
}
