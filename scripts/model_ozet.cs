// model_ozet.cs — Model eleman özeti
// EGBIMOTO run_csharp_script örneği
//
// Globals: uiapp, doc, inputs, input
// Dönüş: Dictionary<string, object?>

var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

var byCategory = collector
    .ToElements()
    .Where(e => e.Category != null)
    .GroupBy(e => e.Category!.Name)
    .OrderByDescending(g => g.Count())
    .Take(20)
    .Select(g => new Dictionary<string, object?>
    {
        ["kategori"] = g.Key,
        ["sayi"]     = g.Count(),
    })
    .ToList();

return new Dictionary<string, object?>
{
    ["toplam_eleman"]  = collector.GetElementCount(),
    ["kategori_ozeti"] = byCategory,
    ["dokuman_adi"]    = doc.Title,
    ["revit_versiyonu"] = uiapp.Application.VersionName,
};
