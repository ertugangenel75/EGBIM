# EGBIMOTO — Mimari Genel Bakış

EGBIMOTO üç projeden oluşur: Revit'ten bağımsız çekirdek (`EGBIMOTO.Core`),
Revit API'sine bağlı katman (`EGBIMOTO.Addin`) ve dağıtım için versiyonsuz
thunk (`EGBIMOTO.Bootstrap`).

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
sürüm adaptörü, MCP Server.

- **Ops/** — 400 op'un tamamı. Her op `[EgOp("isim")]` ile işaretli static metod.
  Op'lar `OpContext`'i `RevitOpContext`'e cast ederek `Doc`/`UiDoc`/`UiApp`'e erişir.
- **Server/** — MCP Server (Claude Desktop ↔ Revit köprüsü, localhost:5577).
  - `EgbimotoMcpServer` — `HttpListener` tabanlı server; `/health`, `/ops`, `/run`, `/validate` endpoint'leri.
  - `RevitDispatcher` — `IExternalEventHandler`. HTTP thread'inden gelen işleri Revit ana thread'ine marshal eder (Revit API yalnızca ana thread'den çağrılabilir).
  - `McpManifestRunner` — Gelen manifest JSON'unu ana thread'de `DagExecutor` ile çalıştırır (headless: onay kapıları otomatik onaylanır, atomik politika korunur).
  - `McpServerManager` — Yaşam döngüsü (başlat/durdur), tekil giriş noktası.
- **Host/**
  - `RevitOpContext` — `OpContext`'i Revit Document/UIApplication ile genişletir.
  - `RevitVersionAdapter` (Rv) — Revit 2024/2025/2026 API kırılmalarını tek noktada yönetir (`ElementId.IntegerValue` ↔ `.Value` gibi).
  - `RevitWriteScope` — Transaction sarmalayıcı; atomik modda dış transaction'a katılır, değilse kendi transaction'ını açar.
  - `EgBootstrap` — `%AppData%` dizin yapısı (engine/log/manifest klasörleri) ve `AssemblyDependencyResolver` altyapısı.
  - `EgAddinScanner` — Sistemdeki Revit add-in'lerini tarar/devre dışı bırakır.
- **Commands/** — Ribbon komutları (`IExternalCommand`): IFC, IDS, Parametre, Poz, Cost, Kalıp, Manifest Browser, MCP Server toggle.
- **UI/** — WPF pencereleri: `ManifestBrowserWindow` (ana arayüz), `ManifestGeneratorWindow` (AI/Pattern), `PreviewGateWindow`, `FourDFiveDWindow`, `ManifestInputDialog`.
- **EgbimotoApp** — Uygulama girişi. Op'ları tarar, manifest klasörlerini hazırlar, manifest çalıştırma orkestrasyonu.

### EGBIMOTO.Bootstrap (dağıtım thunk'ı)

`.addin` dosyasının yüklediği küçük, versiyonsuz `IExternalApplication`. Tek görevi
engine'i (`EGBIMOTO.Addin.dll`) bulup yüklemek ve `OnStartup`/`OnShutdown` çağrılarını
ona yönlendirmektir.

- Engine'i önce `%AppData%\EGBIMOTO\R<sürüm>\app\` altında arar (MSI kurulumu).
- Bulunamazsa kendi yanındaki `EGBIMOTO.Addin.dll`'i kullanır (tek-DLL fallback).
- `AssemblyDependencyResolver` ile tüm bağımlılıkları (WebView2, Roslyn, native DLL'ler) çözer.

**Avantaj:** Engine güncellemek için sadece `app\` klasörü değiştirilir; Bootstrap.dll
ve `.addin` asla değişmez. Ayrıntı: `KURULUM_DAGITIM.md`.

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

## MCP Server Akışı (Claude Desktop ↔ Revit)

```
Claude Desktop ──MCP/stdio──► Python köprüsü ──HTTP/localhost──► EgbimotoMcpServer
(manifest üretir)             (mcp_bridge)      (:5577)          (HttpListener, ayrı thread)
                                                                          │
                                                                  RevitDispatcher
                                                                  (ExternalEvent → ana thread)
                                                                          ▼
                                                                  DagExecutor + 400 op → Model
```

Manifest üretimini **Claude Desktop'taki Claude yapar** — `/ops` endpoint'inden op
katalogunu okur ve uygun op'lardan manifest oluşturur. Ek API anahtarı veya ikinci
LLM çağrısı gerekmez. Ayrıntı: `../mcp_bridge/README.md`.

## Tasarım İlkeleri

- **Tek doğruluk kaynağı (SSoT):** Koddaki `[EgOp]` attribute'ları op tanımlarının
  kaynağıdır. `op_contracts.json` bunlardan türetilir; `docs/OP_REFERANSI.md` ise
  `op_contracts.json`'dan `deploy/generate_op_referansi.py` ile üretilir.
  `EgParamNames` parametre adlarının tek kaynağıdır.
- **Engine/UI ayrımı:** Hesaplama (Core) Revit'ten (Addin) ayrıdır. Core test edilebilir.
- **Kural + veri tabanlı otomasyon:** İş akışları manuel kod değil, JSON manifest + Excel/CSV veriyle sürülür.
- **Sürüm dayanıklılığı:** Revit API kırılmaları `RevitVersionAdapter`'da izole edilir; op dosyalarına dokunulmaz.

## Test

`tests/EGBIMOTO.Core.Tests` — Revit bağımsız birim testler. `dotnet test` ile
Revit kurulu olmadan çalışır. DagExecutor, DagPlanner, NodeCacheService ve
ManifestValidator kapsanır.
