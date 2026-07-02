// param_toplu_yaz.cs — Seçili elemanlara parametre yaz
// EGBIMOTO run_csharp_script örneği
//
// inputs["param_adi"]  : yazılacak parametre adı
// inputs["deger"]      : yazılacak değer
// input               : List<Element> — collect_* adımından gelen elemanlar

var paramAdi = inputs.TryGetValue("param_adi", out var p) ? p?.ToString() ?? "" : "";
var deger    = inputs.TryGetValue("deger",     out var d) ? d?.ToString() ?? "" : "";

if (string.IsNullOrEmpty(paramAdi))
    return new Dictionary<string, object?> { ["error"] = "param_adi zorunlu" };

var elements = (input as IEnumerable<object>)?
    .OfType<Autodesk.Revit.DB.Element>()
    .ToList() ?? new List<Autodesk.Revit.DB.Element>();

int yazilan = 0, atlanan = 0;

using var tx = new Transaction(doc, $"EGBIMOTO Script: {paramAdi} yaz");
tx.Start();
foreach (var el in elements)
{
    var param = el.LookupParameter(paramAdi);
    if (param == null || param.IsReadOnly) { atlanan++; continue; }
    try { param.Set(deger); yazilan++; }
    catch { atlanan++; }
}
tx.Commit();

return new Dictionary<string, object?>
{
    ["yazilan"]   = yazilan,
    ["atlanan"]   = atlanan,
    ["param_adi"] = paramAdi,
    ["deger"]     = deger,
};
