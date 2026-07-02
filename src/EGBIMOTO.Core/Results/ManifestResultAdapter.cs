using System.Collections.Generic;
using System.Linq;
using EGBIMOTO.Core.Validation;

namespace EGBIMOTO.Core.Results
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  EGBIMOTO — ManifestResultAdapter  (v13.5)
    //
    //  Mevcut sonuç tiplerini (ValidationReport, satır listeleri) yeniden
    //  yazmadan ManifestResultDto'ya çevirir. Yeni op'ların bu adaptöre
    //  ihtiyacı yok — doğrudan ManifestResultDto üretebilirler; bu sınıf
    //  YALNIZCA geriye dönük uyum içindir (show_table, validation_summary vb.
    //  mevcut op'ların çıktısını sarmalamak için).
    // ═══════════════════════════════════════════════════════════════════════════

    public static class ManifestResultAdapter
    {
        public static ManifestResultDto FromValidationReport(ValidationReport report)
        {
            var dto = new ManifestResultDto
            {
                Kind  = ManifestResultKind.Validation,
                Title = string.IsNullOrWhiteSpace(report.ManifestTitle)
                    ? "Doğrulama Sonucu" : report.ManifestTitle,
                Columns = new List<string> { "element_id", "category", "severity", "message" },
            };

            dto.Summary["Toplam"] = report.TotalChecks.ToString();
            dto.Summary["Geçti"]  = report.Passed.ToString();
            dto.Summary["Hata"]   = report.Failed.ToString();
            dto.Summary["Uyarı"]  = report.Warnings.ToString();

            foreach (var r in report.Results.Where(r => !r.Passed))
            {
                dto.Rows.Add(new Dictionary<string, object?>
                {
                    ["element_id"] = r.ElementId,
                    ["category"]   = r.Category,
                    ["severity"]   = r.Severity,
                    ["message"]    = r.Message,
                });
                if (long.TryParse(r.ElementId, out var idVal))
                    dto.ElementIds.Add(idVal);
            }
            return dto;
        }

        public static ManifestResultDto FromRows(
            string title, List<Dictionary<string, object?>> rows, int maxColumns = 0)
        {
            var dto = new ManifestResultDto { Kind = ManifestResultKind.Table, Title = title };
            if (rows.Count == 0) return dto;

            dto.Columns = rows[0].Keys.ToList();
            dto.Rows    = rows;
            dto.Summary["Satır"] = rows.Count.ToString();

            foreach (var row in rows)
                if (row.TryGetValue("element_id", out var idv) &&
                    long.TryParse(idv?.ToString(), out var idVal))
                    dto.ElementIds.Add(idVal);

            return dto;
        }
    }
}
