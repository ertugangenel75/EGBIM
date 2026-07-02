# EGBIMOTO v8 — Değişiklik Günlüğü

## v8.0.0 (2026-06)

### 🔴 Kritik Düzeltmeler

#### FIX-1: Thread Safety — `_scriptCache` ConcurrentDictionary'e Taşındı
**Dosya:** `src/EGBIMOTO.Addin/Ops/ScriptOps.cs`

`static readonly Dictionary<string, Script<object>> _scriptCache` yapısı
thread-safe değildi. CommandQueue ile birden fazla manifest paralel
çalıştırıldığında race condition oluşabiliyordu.

**Değişiklik:**
- `Dictionary` → `ConcurrentDictionary`
- Cache yazımı `_scriptCache[key] = value` → `_scriptCache.GetOrAdd(key, compiled)`
- İki thread aynı anda derlese bile yalnızca biri cache'e yazılır (atomic)

#### FIX-2: `goto` Kaldırıldı — flag + break Kullanıldı
**Dosya:** `src/EGBIMOTO.Core/DAG/DagExecutor.cs`

`StopAfterGate` tetiklendiğinde iç içe foreach'ten çıkmak için `goto phase_done`
kullanılıyordu. C# standartlarına aykırı, okunması zor.

**Değişiklik:**
- `goto phase_done` → `stopRequested = true; break`
- Dış foreach'in her iterasyonu başında `if (stopRequested) break` kontrolü
- `phase_done:` etiketi tamamen kaldırıldı

#### FIX-3: Cache Key — İçerik Hash'i Eklendi
**Dosya:** `src/EGBIMOTO.Core/DAG/DagExecutor.cs`

Önceden koleksiyon cache key'i yalnızca `col:Count:TypeName` içeriyordu.
Aynı sayıda ama farklı elemanlı iki liste (örn. farklı filtre sonuçları)
aynı cache key üretiyordu → yanlış cache hit riski.

**Değişiklik:**
- `BuildCollectionHash()` private metodu eklendi
- Her elemanın `GetHashCode()` değeri sıra duyarlı XOR ile birleştirilir
- Büyük koleksiyonlarda ilk 200 eleman örneklenir (performans)
- Yeni format: `col:{Count}:{TypeName}:{contentHash}`

#### FIX-4: Unit Test Projesi Eklendi
**Klasör:** `tests/EGBIMOTO.Core.Tests/`

EGBIMOTO.Core Revit API gerektirmediğinden Revit kurulmadan çalışır.

**Eklenen test dosyaları:**
- `DagExecutorTests.cs` — 12 test (boş manifest, from bağlantısı, condition,
  required flag, döngü tespiti, cache, from_many, StopAfterGate, hash fix)
- `ManifestValidatorTests.cs` — 10 test (geçerli manifest, eksik alan,
  bilinmeyen op, from referansı, duplicate id, fix prompt)
- `CacheAndPlannerTests.cs` — 9 test (TTL, invalidate, thread safety,
  bağımsız seviyeler, bağımlılık sırası, from_many)

**Çalıştır:**
```
dotnet test tests/EGBIMOTO.Core.Tests
```

### Değişmeyen Şeyler
- 343 op — tümü v7 ile aynı
- 199 manifest — tümü v7 ile aynı
- op_contracts.json — değişmedi
- API uyumluluğu — tam geriye dönük uyumlu
