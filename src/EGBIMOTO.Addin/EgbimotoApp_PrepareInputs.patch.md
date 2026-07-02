## EgbimotoApp.cs — v11 PrepareManifestInputs değişikliği

`PrepareManifestInputs` metodunu şu şekilde değiştir:

```csharp
// ÖNCE (v10 — sync):
public static EgManifest? PrepareManifestInputs(EgManifest manifest, Document doc)
{
    var service = new ManifestInputService(doc);
    return service.PrepareManifest(manifest);
}

// SONRA (v11 — async path):
public static EgManifest? PrepareManifestInputs(EgManifest manifest, Document doc)
{
    var service = new ManifestInputService(doc,
        System.Windows.Threading.Dispatcher.CurrentDispatcher);
    return service.PrepareManifestAsync(manifest);  // ← async path
}
```

Bu tek satır değişikliği dialog'u anında açar, Revit verisi
arka planda yüklenir. Geriye dönük uyumluluk tam korunur.
