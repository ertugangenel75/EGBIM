# EGBIMOTO MCP Server + Manifest Üretici Ajan

EGBIMOTO'yu **AI-erişilebilir bir BIM platformuna** dönüştüren köprü. Claude Desktop
(veya MCP uyumlu herhangi bir AI ajanı) doğal dilde komut verir, Claude EGBIMOTO'nun
op katalogunu okuyup **manifest'i kendisi üretir** ve Revit'te çalıştırır.

## Mimari

```
┌──────────────────┐   MCP/stdio    ┌──────────────────┐   HTTP/localhost   ┌──────────────────────┐
│  Claude Desktop  │ ─────────────► │  Python Köprüsü  │ ─────────────────► │  EGBIMOTO Server     │
│  (kendi bağlamı) │ ◄───────────── │  (mcp_bridge)    │ ◄───────────────── │  (Revit içinde)      │
└──────────────────┘                └──────────────────┘                    └──────────┬───────────┘
                                                                                        │ ExternalEvent
                                                                                        │ (ana thread marshal)
                                                                                        ▼
                                                                            ┌──────────────────────┐
                                                                            │  DagExecutor + 400 op │
                                                                            │  → Revit Modeli       │
                                                                            └──────────────────────┘
```

**Önemli tasarım kararı:** Manifest üretimini **Claude Desktop'taki Claude yapar** — op
katalogunu (`/ops`) görüp uygun op'lardan manifest oluşturur. Bu yüzden ek API anahtarı
veya ikinci bir LLM çağrısı gerekmez. (EGBIMOTO'nun gömülü `ManifestGenerator`'ı, Claude
Desktop kullanmayan, EGBIMOTO arayüzünden doğrudan üretim isteyenler için paralel kalır.)

## Bileşenler

| Bileşen | Konum | Görev |
|---|---|---|
| `RevitDispatcher` | `src/.../Server/` | HTTP thread → Revit ana thread marshalling (ExternalEvent) |
| `EgbimotoMcpServer` | `src/.../Server/` | HttpListener, endpoint router (localhost:5577) |
| `McpManifestRunner` | `src/.../Server/` | Manifest → DagExecutor köprüsü |
| `McpServerManager` | `src/.../Server/` | Yaşam döngüsü (başlat/durdur) |
| `McpServerToggleCommand` | `src/.../Commands/` | Ribbon düğmesi |
| `egbimoto_mcp_bridge.py` | `mcp_bridge/` | Python MCP köprüsü (Claude Desktop ↔ HTTP) |

## HTTP Endpoint'leri

| Method | Path | Görev |
|---|---|---|
| GET | `/health` | Server durumu + aktif doküman adı |
| GET | `/ops` | op_contracts.json (ajan yetenek katalogu) |
| POST | `/run` | Gövdedeki manifest'i çalıştırır |
| POST | `/validate` | Manifesti çalıştırmadan doğrular |

**Güvenlik:** Yalnızca `127.0.0.1` (localhost) dinler — dışarıdan erişilemez. İsteğe
bağlı `X-EGBIMOTO-Token` başlığı (kurumsal ortam için).

## MCP Araçları (Claude Desktop'ın gördüğü)

| Araç | Görev |
|---|---|
| `egbimoto_list_ops` | EGBIMOTO'nun tüm işlemlerinin katalogu |
| `egbimoto_run_manifest` | Manifest'i Revit'te çalıştırır |
| `egbimoto_health` | Bağlantı/doküman durumu |

## Kurulum

### 1. Python köprüsü
```bash
cd mcp_bridge
pip install -r requirements.txt
```

### 2. Claude Desktop yapılandırması
`claude_desktop_config.example.json`'u Claude Desktop config'ine ekleyin:
- Windows: `%APPDATA%/Claude/claude_desktop_config.json`
- `args` içindeki yolu kendi `egbimoto_mcp_bridge.py` konumunuza göre düzeltin.

```json
{
  "mcpServers": {
    "egbimoto": {
      "command": "python",
      "args": ["C:/EGBIM/mcp_bridge/egbimoto_mcp_bridge.py"],
      "env": { "EGBIMOTO_PORT": "5577" }
    }
  }
}
```
Claude Desktop'ı yeniden başlatın.

### 3. Revit tarafı
- EGBIMOTO yüklü Revit'i açın, bir model açın.
- EGBIMOTO şeridinden **MCP Server Başlat** düğmesine basın.
- "MCP Server başlatıldı (port 5577)" mesajını görün.

## Kullanım

Claude Desktop'ta doğrudan Türkçe konuşun:

> "Açık Revit modelinde tüm kapıları say ve oda bazında bir rapor çıkar."

Claude şunu yapar:
1. `egbimoto_list_ops` çağırır → kapı/oda op'larını bulur (`collect_doors`, `door_number_by_room`...)
2. Uygun op'lardan bir manifest oluşturur
3. `egbimoto_run_manifest` ile çalıştırır
4. Sonucu özetler

Diğer örnekler:
- "Bütün ıslak hacimlerin kaplama matrisini çıkar."
- "Kolonları TBDY'ye göre kontrol et, yetersiz olanları söyle." *(önce kolonlara Ndm girilmeli)*
- "Elektrik conduit'lerinin IEC kablo hesabını yap ve cetvel üret."

## Güvenlik Notları

- Server **modeli değiştirebilir** (yazma op'ları). Claude, yazma yapan manifest'i
  çalıştırmadan önce genellikle size gösterir; emin değilseniz "önce manifest'i göster"
  deyin.
- `transaction_policy: atomic` olan manifest'lerde bir adım hata verirse **tüm
  değişiklikler geri alınır**.
- Server yalnızca siz **MCP Server Başlat** dediğinizde çalışır; Revit'i kapatınca durur.
- Token isterseniz: `McpServerToggleCommand.cs`'de `token` parametresini doldurun ve
  Python köprüsü env'ine `EGBIMOTO_TOKEN` ekleyin.

## Sınırlar

- Server **headless** çalışır: interaktif onay kapıları (preview gate) otomatik onaylanır.
  Görsel önizleme gerektiren işlemler için EGBIMOTO arayüzünü kullanın.
- Aynı anda tek Revit dokümanı (aktif olan) hedeflenir.
- ExternalEvent marshalling: Revit bir dialog beklerken takılırsa istek zaman aşımına
  uğrayabilir (varsayılan 180s).
