# EGBIMOTO

Revit için manifest-tabanlı BIM otomasyon platformu. Türk AEC standartlarına
(TR BIM, ÇŞB 2026, TS500, TBDY 2018, IFC/IDS) odaklı, native C# .NET 8 Revit add-in.

## Özellikler

- **478 operasyon**, 48 kategori — metraj, maliyet, kalıp, yapısal, MEP (HVAC/sıhhi/elektrik/mekanik),
  yangın, koordinasyon, IFC/IDS, çakışma tespiti, donatı, cephe ve daha fazlası
- **356 hazır manifest** — JSON ile tanımlı, DAG motoruyla çalışan iş akışları
- **MCP Server** — Claude Desktop (veya MCP uyumlu ajan) Revit modeline bağlanır,
  op katalogunu okuyup manifest üretir ve çalıştırır (localhost:5577)
- **AI + Pattern manifest üretimi** — doğal dilden iş akışı (Claude API veya
  API'siz şablon eşleştirme)
- **Preview-Confirm + Atomic** transaction modları
- **Çoklu Revit sürümü** — 2024, 2025, 2026

## Hızlı Başlangıç

```bash
dotnet build src/EGBIMOTO.Addin -c Release -p:RevitVersion=2026
```

Çıktıyı `%AppData%\Autodesk\Revit\Addins\2026\`'a kopyalayın ve Revit'i başlatın.
Detaylar için `docs/HIZLI_BASLANGIC.md` ve `docs/KURULUM_DAGITIM.md`.

## Dokümantasyon

Tüm kılavuzlar `docs/` altında:
- `HIZLI_BASLANGIC.md` — kurulum ve ilk çalıştırma
- `KURULUM_DAGITIM.md` — MSI installer, Bootstrap mimarisi, MCP Server kurulumu
- `MANIFEST_YAZIM_REHBERI.md` — manifest JSON yazımı
- `OP_REFERANSI.md` — 400 op'un tam referansı (op_contracts.json'dan üretilir)
- `MIMARI.md` — katman yapısı ve tasarım

MCP Server + Python köprüsü için: `mcp_bridge/README.md`.

## Mimari

Üç proje: `EGBIMOTO.Core` (Revit bağımsız DAG motoru, test edilebilir),
`EGBIMOTO.Addin` (Revit API, op'lar, WPF UI, MCP Server) ve dağıtım için
`EGBIMOTO.Bootstrap` (versiyonsuz thunk; engine'i `%AppData%` altından yükler).
Ayrıntı: `docs/MIMARI.md`.

## Lisans

Apache License 2.0 — bkz. [LICENSE](LICENSE) 

```
Copyright 2026 Ertuğan Genel

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
```

## Yazar

Ertuğan Genel — EGBIM
