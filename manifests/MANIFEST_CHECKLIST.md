# EGBIMOTO V4 — Manifest Kataloğu

## Toplam: 191 Manifest | 17 Op Dosyası | .NET 8 | Revit 2025/2026

---

## GRUP 1: QA/QC (10 manifest)
| ID | Dosya | Açıklama |
|---|---|---|
| M01 | qa/m01_bos_parametre_tarama | Boş parametre tespiti |
| M02 | qa/m02_duplicate_tespiti | Duplicate element |
| M03 | qa/m03_seviye_atama | Seviye ataması |
| M04 | qa/m04_faz_tutarlilik | Faz tutarlılığı |
| M05 | qa/m05_workset_kontrol | Workset kontrolü |
| M06 | qa/m06_model_uyarilari | Model uyarıları |
| M07 | qa/m07_oda_kontrol | Oda varlık kontrolü |
| M08 | qa/m08_aile_kontrol | Onaylı aile listesi |
| M09 | qa/m09_koordinat_kontrol | Koordinat sistemi |
| M10 | qa/m10_model_boyut | Model boyut analizi |

## GRUP 2: MİMARİ (10 manifest)
| ID | Dosya | Açıklama |
|---|---|---|
| A01 | mimari/a01_sheet_uretimi | Sheet üretimi |
| A02 | mimari/a02_view_template | View template |
| A03 | mimari/a03_oda_alan_dogrulama | Oda alan kontrolü |
| A04 | mimari/a04_kapi_numaralama | Kapı numaralama |
| A05 | mimari/a05_tavan_yuksekligi | Tavan yüksekliği |
| A06 | mimari/a06_erisebilirlik | Erişilebilirlik |
| A07 | mimari/a07_malzeme_atama | Malzeme ataması |
| A08 | mimari/a08_yangin_bolge | Yangın bölgesi |
| A09 | mimari/a09_oda_isimlendirme | Oda isimlendirme |
| A10 | mimari/a10_pencere_kontrol | Penceresiz oda |

## GRUP 3: YAPISAL / TS500-TBDY (10 manifest)
| ID | Dosya | Açıklama |
|---|---|---|
| S01 | yapisal_v4/s01_kolon_boyut | Kolon boyut (TS500) |
| S02 | yapisal_v4/s02_kiris_hizalama | Kiriş aks hizalama |
| S03 | yapisal_v4/s03_doseme_kalinlik | Döşeme kalınlık |
| S04 | yapisal_v4/s04_temel_siniflandirma | Temel türü |
| S05 | yapisal_v4/s05_tbdy_deprem | TBDY 2018 bölgesi |
| S06 | yapisal_v4/s06_donati_param | Donatı parametresi |
| S07 | yapisal_v4/s07_kalip_hesabi | Kalıp metraj |
| S08 | yapisal_v4/s08_beton_hacim | Beton hacmi |
| S09 | yapisal_v4/s09_donati_agirlik | Donatı ağırlık |
| S10 | yapisal_v4/s10_yapisal_ts500 | TS500 bindirme/ankraj |

## GRUP 4: MEKANİK MEP (10 manifest)
## GRUP 5: ELEKTRİK (10 manifest)
## GRUP 6: SIHHI TESİSAT (10 manifest)
## GRUP 7: YANGIN SİSTEMLERİ (10 manifest)
## GRUP 8: ETL / VERİ (10 manifest)
## GRUP 9: KOORDİNASYON (10 manifest)
## GRUP 10: PROJE YÖNETİMİ (10 manifest)

---

## Yeni Op Dosyaları (V4)

| Dosya | Sınıf | Op Sayısı | Grup |
|---|---|---|---|
| QaOps.cs | QaOps | 10 | Grup 1 |
| ArchOps.cs | ArchOps | 10 | Grup 2 |
| MepAndPmOps.cs | MechanicalOps | 5 | Grup 4 |
| MepAndPmOps.cs | ElectricalOps | 5 | Grup 5 |
| MepAndPmOps.cs | PlumbingOps | 5 | Grup 6 |
| MepAndPmOps.cs | FireProtectionOps | 5 | Grup 7 |
| MepAndPmOps.cs | CoordinationOps | 3 | Grup 9 |
| MepAndPmOps.cs | ProjectMgmtOps | 4 | Grup 10 |

**Toplam yeni op: ~47 | Toplam op (V3+V4): ~220+**

---

## Versiyon Geçmişi
- V1: İlk prototip
- V2: DAG engine, poz/maliyet
- V3: Typed contracts, ManifestLinter, 193 op, 115 manifest
- **V4: +47 op, +100 manifest, 10 disiplin, Faz 1→4 tam kapsam**
