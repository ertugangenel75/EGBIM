# EGBIMOTO — Mimari Genel Bakış

EGBIMOTO iki katmanlı bir mimariye sahiptir: Revit'ten bağımsız çekirdek
(`EGBIMOTO.Core`) ve Revit API'sine bağlı katman (`EGBIMOTO.Addin`).

## Katmanlar

### EGBIMOTO.Core (Revit bağımsız)

Revit API referansı içermez — yalnızca .NET 8 BCL kullanır. Bu sayede Revit
kurulu olmadan derlenebilir ve birim testleri çalıştırılabilir.

- **DAG/** — Manifest yürütme motoru
  - `DagPlanner` — Manifest adımlarından bağımlılık grafı üretir (Kahn topolojik sıralama, döngü tespiti). `from`, `from_many`, `depends_on` ve `$ref` (params + condition) bağımlılıklarını çözer.
  - `DagExecutor` — Planı çalıştırır. Önbellek, koşullu atlama, Preview/Schedule gate intercept, atomik commit/rollback ve telemetri (süre, önbellek/atlama sayıları) yönetir.
  - `DagModel` — `PlannedStep`, `DagPlan`, `DagRunResult`, `DagTraceRow` veri modelleri.
- **Manifest/** — `EgManifest`, `EgStep` modelleri; `ManifestLoader`, `ManifestRunner`, `ManifestLinter` (5 katmanlı semantik denetim).
- **AI/** — `ManifestValidator` (op_contracts karşısında doğrulama), `PatternEngine` (API'siz şablon eşleştirme), `ManifestGenerator` (Claude API ile üretim + otomatik düzeltme).
- **Ops/** — `OpRegistry` (attribute tabanlı op keşfi), `OpContext` (op'lara geçilen bağlam), `EgOpAttribute`.
- **Cache/** — `NodeCacheService` (thread-safe `ConcurrentDictionary`, TTL'li önbellek).
- **Events/** — `InMemoryEventStore` (adım telemetri olayları).
- **Ledger/** — `FileBasedLedger` (atomik commit'lerin değişmez kaydı).
- **Preview/**, **Schedule/** — Preview-Confirm ve 4D/5D için DTO'lar.

### EGBIMOTO.Addin (Revit bağlı)

Revit API'sini kullanan her şey burada. WPF UI, op implementasyonları, Revit
sürüm adaptörü.

- **Ops/** — 373 op'un tamamı. Her op `[EgOp("isim")]` ile işaretli static metod.
  Op'lar `OpContext`'i `RevitOpContext`'e cast ederek `Doc`/`UiDoc`/`UiApp`'e erişir.
- **Host/**
  - `RevitOpContext` — `OpContext`'i Revit Document/UIApplication ile genişletir.
  - `RevitVersionAdapter` (Rv) — Revit 2024/2025/2026 API kırılmalarını tek noktada yönetir (`ElementId.IntegerValue` ↔ `.Value` gibi).
  - `EgBootstrap` — İleride engine ayrımı için `AssemblyDependencyResolver` altyapısı (rst-c'den uyarlandı).
  - `EgAddinScanner` — Sistemdeki Revit add-in'lerini tarar/devre dışı bırakır.
- **UI/** — WPF pencereleri: `ManifestBrowserWindow` (ana arayüz), `ManifestGeneratorWindow` (AI/Pattern), `PreviewGateWindow`, `FourDFiveDWindow`, `ManifestInputDialog`.
- **EgbimotoApp** — Uygulama girişi. Op'ları tarar, manifest klasörlerini hazırlar, manifest çalıştırma orkestrasyon.

## Çalışma Akışı

```
Kullanıcı manifest seçer (Browser)
        ↓
ManifestLoader → EgManifest
        ↓
EgbimotoApp.RunManifest
        ↓
DagPlanner.Build → bağımlılık grafı + topolojik seviyeler
        ↓
DagExecutor.Run → her adımı sırayla çalıştır
        │
        ├─ OpRegistry.Execute(op, ctx) → static metod invoke
        ├─ Cache kontrolü (cache=true ise)
        ├─ Condition kontrolü (atla/çalıştır)
        ├─ Preview/Schedule gate (intercept)
        └─ Atomik commit veya rollback
        ↓
DagRunResult → sonuç + trace + telemetri
        ↓
Rapor/Tablo açılır
```

## Tasarım İlkeleri

- **Tek doğruluk kaynağı (SSoT):** `op_contracts.json` op tanımlarının, `EgParamNames` parametre adlarının tek kaynağıdır.
- **Engine/UI ayrımı:** Hesaplama (Core) Revit'ten (Addin) ayrıdır. Core test edilebilir.
- **Kural + veri tabanlı otomasyon:** İş akışları manuel kod değil, JSON manifest + Excel/CSV veriyle sürülür.
- **Sürüm dayanıklılığı:** Revit API kırılmaları `RevitVersionAdapter`'da izole edilir; op dosyalarına dokunulmaz.

## Test

`tests/EGBIMOTO.Core.Tests` — Revit bağımsız birim testler. `dotnet test` ile
Revit kurulu olmadan çalışır. DagExecutor, DagPlanner, NodeCacheService ve
ManifestValidator kapsanır.
