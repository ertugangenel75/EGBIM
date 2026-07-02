# EGBIMOTO v12.1 — Enterprise Temizlik Sürümü

## Ribbon İkonları (YENİ)
- `src/EGBIMOTO.Addin/Resources/Icons/` altına 20 ikon eklendi (32px `ad.png` + 16px `ad_16.png`, toplam 40 PNG).
- Tutarlı tasarım dili: yuvarlatılmış kare zemin, disipline özel renk, beyaz glif.
- Kapsam: egbimoto_icon, ifc, ids, param, poz, cost, kalip, browser, mcp,
  sihhi, yangin, elektrik, mekanik, yapisal, mep, donati, temel, qa, teslim, koord.
- `RibbonBuilder.ApplyIcons()` eklendi: LargeImage (32) + Image (16) birlikte atanır,
  ikon bulunamazsa `egbimoto_icon.png`'a düşer.
- csproj: `Resources\Icons\*.png` → çıktı dizinine `PreserveNewest` ile kopyalanır.

## Kritik Düzeltme: ribbon_config.json dağıtımı
- `CopyAssets` hedefi `ribbon_config.json`'u KOPYALAMIYORDU; `RibbonBuilder.LoadConfig`
  addinDir kökünden okuduğu için özel ribbon konfigürasyonu hiç yüklenmiyor ve
  her zaman varsayılan panellere düşülüyordu. csproj'a kopyalama adımı eklendi.

## GUID Standardizasyonu
- 308 ardışık placeholder GUID (`f1a00001-0001-...`) ve 5 kalıp GUID'i (`a1b2c3d4-...`)
  deterministik UUIDv5 ile değiştirildi — namespace: `uuid5(NAMESPACE_URL, 'egbim.com.tr/shared-params')`.
- Senkron güncellenen dosyalar: `param_guid_map.json`, `shared_param_map.json`,
  `EGBIM_SharedParams.txt`, `manifests/kalip/EG_KALIP_ENSURE_PARAMS.json`.
- Eski→yeni eşleme: `data/mapping/guid_migration_v12.json`.
- DİKKAT: Eski GUID'lerle parametre bağlanmış mevcut projelerde migrasyon gerekir.

## Manifest Normalizasyonu (356 manifest, 3013 adım denetlendi)
- Denetim sonucu: 0 hata — tüm op referansları `op_contracts.json` ile eşleşiyor,
  `input`/`input_ref` anti-pattern'i yok, `from` zinciri tutarlı, tekrarlayan step id yok.
- 188 manifeste eksik `"version": "1.0.0"` alanı eklendi.
- 47 manifeste, ilk collect adımından türetilen kategoriyle `MODEL_HAS_ELEMENTS`
  pre_check eklendi (on_fail: ABORT).
- 99 manifest (sistem/ETL/rapor/script türü) bilinçli olarak pre_check'siz bırakıldı;
  eleman ön koşulu bu akışlar için anlamsız.

## Depo Temizliği
- Silindi: `files1..5.zip` (eski geliştirme anlık görüntüleri — son sürümler src ile birebir
  ya da src'den geride), `deploy2026.bat` (tek satırlık alias),
  `TR_BIM_SharedParameters_MASTER_QA_CSB2026.txt` (EGBIM_SharedParams.txt ile bayt-bazında özdeş).
- Taşındı: CHANGELOG_V6/V8/v9/v10 → `docs/changelog/`, DEPLOY_2026_NOTES.txt → `docs/`.
- `.gitignore` eklendi.

## Bilinen Açık Kalemler (değişmedi)
- 451 "zorunlu param verilmemiş" uyarısı: op_contracts üretici, default'u olmayan her
  parametreyi `required:true` işaretliyor; çoğu girdi pipeline (`from`) veya registry'den
  geliyor. Kontrat üreticide `required` semantiğinin ayrıştırılması önerilir.
- Mimari/cephe op kapsamı MEP'e göre ince (~10 vs 76) — v13 hedefi.
- MSI üretimi sıfır-uyarı build doğrulanana kadar ertelendi.
