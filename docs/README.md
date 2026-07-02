# EGBIMOTO — Dokümantasyon

EGBIMOTO, Revit için manifest-tabanlı BIM otomasyon platformu. Türk AEC
standartlarına (TR BIM, ÇŞB 2026, TS500, TBDY 2018, IFC/IDS) odaklı.

## Kılavuzlar

| Doküman | İçerik |
|---------|--------|
| [HIZLI_BASLANGIC.md](HIZLI_BASLANGIC.md) | Kurulum, ilk manifest çalıştırma, AI üretici, MCP Server |
| [KURULUM_DAGITIM.md](KURULUM_DAGITIM.md) | İki kurulum yöntemi, MSI installer, çoklu Revit sürümü, MCP Server kurulumu |
| [MANIFEST_YAZIM_REHBERI.md](MANIFEST_YAZIM_REHBERI.md) | Manifest JSON yapısı, DAG mantığı, örnekler |
| [OP_REFERANSI.md](OP_REFERANSI.md) | 400 op'un kategoriye göre tam referansı |
| [MIMARI.md](MIMARI.md) | Katmanlar, çalışma akışı, tasarım ilkeleri |

MCP Server + Python köprüsünün ayrıntıları için: [`../mcp_bridge/README.md`](../mcp_bridge/README.md).

## Hızlı Bakış

- **400 operasyon**, 48 kategori
- **322 hazır manifest**, 31 klasör
- **3 proje:** `EGBIMOTO.Core` (Revit bağımsız) + `EGBIMOTO.Addin` (Revit bağlı, MCP Server dahil) + `EGBIMOTO.Bootstrap` (dağıtım thunk'ı)
- **3 Revit sürümü:** 2024, 2025, 2026
- **AI + Pattern** manifest üretimi
- **MCP Server:** Claude Desktop ↔ Revit köprüsü (localhost:5577)

## Değişiklik Notları

Sürüm geçmişi için kök dizindeki `CHANGELOG_v*.md` dosyalarına bakın.
En güncel: `CHANGELOG_v10.md`.
