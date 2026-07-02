# EGBIMOTO v6 — Değişiklik Günlüğü

Yayın tarihi: Haziran 2026

## Genel Bakış

v6'nın tek odağı **RevitVersionAdapter (Rv) soyutlama katmanı**dır.
Yeni özellik eklenmemiş; mevcut tüm Op'lar Revit API'ye doğrudan
değil, `Rv.*` üzerinden bağlanacak şekilde yeniden yazılmıştır.

---

## Yeni Dosya: `Host/RevitVersionAdapter.cs`

```
EGBIMOTO.Addin/Host/RevitVersionAdapter.cs   ← YENİ
```

`Rv` static sınıfı — 169 satır, 8 yöntem:

| Yöntem                        | Açıklama                                      |
|-------------------------------|-----------------------------------------------|
| `Rv.GetId(ElementId)`         | `long` id — 2024: `.IntegerValue`, 2025+: `.Value` |
| `Rv.IdStr(ElementId)`         | Loglama için string id                        |
| `Rv.MakeElementId(long)`      | long → ElementId                             |
| `Rv.MakeElementId(int)`       | int → ElementId (BIC, view id gibi)           |
| `Rv.GetCategoryId(Element)`   | BuiltInCategory int — null-safe              |
| `Rv.GetWorksetIntId(WorksetId)`| Workset int id                              |
| `Rv.SetWorksetParam(Parameter?, WorksetId)` | ELEM_PARTITION_PARAM set |
| `Rv.GetParamDataType(Definition)` | 2024: `ParameterType`, 2025+: `GetDataType()` |
| `Rv.InvalidId`                | `ElementId.InvalidElementId` kısayolu        |
| `Rv.IsInvalid(ElementId?)`    | Null ve invalid kontrolü                     |

---

## Güncellenen: `EGBIMOTO.Addin.csproj`

- **Version:** `5.0.0` → `6.0.0`
- **Yeni:** `RevitVersion` MSBuild property (`2024` | `2025` | `2026`)
- **Yeni:** `DefineConstants` per-version (`REVIT2024`, `REVIT2025`, `REVIT2026`)
- **Yeni:** `AssemblyVersion` per-version (`6.0.0.2024`, `6.0.0.2025`, `6.0.0.2026`)
- **Yeni:** Otomatik `RevitApiPath` per-version

### Derleme komutları

```bash
# Revit 2026 (varsayılan)
dotnet build

# Revit 2025
dotnet build -p:RevitVersion=2025

# Revit 2024
dotnet build -p:RevitVersion=2024
```

---

## Değiştirilen Op Dosyaları (12 dosya)

| Dosya                  | Değişiklik                                                 |
|------------------------|------------------------------------------------------------|
| `ModelingOps.cs`       | `target.Id.IntegerValue` → `Rv.SetWorksetParam(...)`       |
| `KalipOps.cs`          | `SafeId()` ve `GetBic()` try/catch → `Rv.GetId`, `Rv.GetCategoryId` |
| `CostOps_PozPatch.cs`  | `SafeEid()` ve bic bloğu → `Rv.GetId`, `Rv.GetCategoryId`  |
| `ViewOps.cs`           | `new ElementId(vid)` (long×2), `new ElementId(paramId.Value)` (×3) → `Rv.MakeElementId` |
| `TraceOps.cs`          | `new ElementId(id)` (long×3) → `Rv.MakeElementId`          |
| `ParamOps.cs`          | `new ElementId(id)` (long×1), `GetParamTypeName` → `Rv.GetParamDataType` |
| `CollectionOps.cs`     | `new ElementId((long)hostId)` → `Rv.MakeElementId`         |
| `PreviewOps.cs`        | `new ElementId(idVal)` (long) → `Rv.MakeElementId`          |
| `RoomOps.cs`           | `new ElementId(wallId)` (long) → `Rv.MakeElementId`         |
| `CostOps_PozPatch.cs`  | `new ElementId(eid)` (long) → `Rv.MakeElementId`           |

**Değiştirilmeyen çağrılar (kasıtlı):**
- `new ElementId(bic)` — `BuiltInCategory` enum overload, tüm versiyonlarda stabil
- `WorksetId.IntegerValue` — 2025'te deprecated değil; adapter üzerinden ama aynen

---

## Sonraki Versiyonda Hedef

Revit 2027 API kırılması geldiğinde yapılacak tek şey:
```csharp
// RevitVersionAdapter.cs — tek dosya
#if REVIT2027
    // yeni API
#else
    // mevcut
#endif
```
35+ Op dosyasına dokunmak gerekmez.


---

## v6.1 — Tam Yayılım Güncellemesi

### Düzeltilen: `CostOps.cs` partial class hatası

```csharp
// ÖNCE (compile hatası)
public static class CostOps

// SONRA
public static partial class CostOps
```

### Tamamlanan: Tüm Op dosyalarına Rv. yayılımı

| Metrik                        | v6.0  | v6.1  |
|-------------------------------|-------|-------|
| Ham `.Id.Value` kullanımı     | 133   | **0** |
| Ham `new ElementId(long/int)` | 1     | **0** |
| Rv. kullanan dosya sayısı     | 9     | **24** |
| Toplam Rv. çağrısı            | 17    | **152** |

### Değiştirilen pattern'ler

| Pattern                        | Dönüşüm                    |
|--------------------------------|----------------------------|
| `el.Id.Value.ToString()`       | `Rv.IdStr(el.Id)`          |
| `el.Id.Value`                  | `Rv.GetId(el.Id)`          |
| `x.Category?.Id.Value == (long)BIC` | `Rv.GetCategoryId(x) == (int)BIC` |
| `new ElementId(longVal)`       | `Rv.MakeElementId(longVal)` |
| `new ElementId(bic)` (BuiltInCategory) | Değiştirilmedi — stabil overload |

