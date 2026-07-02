# EGBIMOTO v9 — Değişiklik Günlüğü

## v8 → v9 Geliştirmeleri

### 🐛 Bug Düzeltmeleri (3 kritik)

#### FIX-1: DagExecutorTests — MakeMethod/OpWrapper TargetException
**Dosya:** `tests/EGBIMOTO.Core.Tests/DagExecutorTests.cs`

v8'de `OpWrapper.Execute` bir **instance metodu** idi.
`OpRegistry.Execute` → `method.Invoke(null, ...)` çağrısı `null` target ile
**static metod** bekler. Instance metod olunca `TargetException` fırlatıyordu.

**Çözüm:** `AssemblyBuilder + TypeBuilder + MethodBuilder` ile runtime'da gerçek
static metodlar üretildi. Her test için benzersiz assembly oluşturulur.

**Yeni testler:** T13 (EvalCondition >= sayısal), T14 (depends_on sırası) eklendi.

---

#### FIX-2: BuildCollectionHash — string.GetHashCode() Non-Determinizm
**Dosya:** `src/EGBIMOTO.Core/DAG/DagExecutor.cs`

.NET 5+ ile `string.GetHashCode()` her process başlatmada farklı seed kullanır.
Bu cache key'lerinin process yeniden başlatmada geçersiz kalmasına yol açıyordu.

**Çözüm:** String elemanlar için `StringComparer.Ordinal.GetHashCode(s)` kullanıldı.
Bu comparer randomization'dan etkilenmez, cross-process deterministik.

---

#### FIX-3: EvalCondition — Yeni Kelime Operatörleri
**Dosya:** `src/EGBIMOTO.Core/DAG/DagExecutor.cs`

v8'de yalnızca `==`, `!=`, `>=`, `<=`, `>`, `<` destekleniyordu.

**Eklenen operatörler:**
- `contains` / `not_contains` — string içerme + koleksiyon üyeliği
- `in` / `not_in` — virgüllü liste üyeliği: `"$kategori in [Walls,Floors]"`
- `starts_with` / `ends_with` — ön/son ek kontrolü
- `matches` — regex eşleşme

**Manifest kullanım örnekleri:**
```json
{ "condition": "$isim contains Duvar" }
{ "condition": "$kategori in [Walls,Floors,Ceilings]" }
{ "condition": "$param_adi starts_with EG_" }
{ "condition": "$kodu matches ^16\\.0" }
```

---

### ✨ Yeni Operasyonlar (25 op)

#### FacadeOps.cs — 8 Cephe Operasyonu (Grup 45)
| Op | Açıklama |
|---|---|
| `collect_curtain_panels` | Curtain panel elementlerini topla |
| `facade_system_params` | TR BIM cephe parametrelerini toplu yaz |
| `facade_panel_matrix` | Grid bazlı panel matrisi oluştur |
| `facade_joint_validate` | Derz genişlik/tip doğrulama |
| `facade_area_by_type` | Panel tipine göre alan metrajı |
| `facade_opening_ratio` | Saydamlık oranı (TS 825) |
| `facade_u_value_check` | U değeri TS 825 kontrolü |
| `facade_export_schedule` | HTML metraj raporu export |

#### RoomFinishOps.cs — 6 Oda/Kaplama Operasyonu (Grup 46)
| Op | Açıklama |
|---|---|
| `room_finish_assign` | Regex kural bazlı kaplama tipi atama |
| `room_finish_validate` | Kaplama parametresi doğrulama |
| `room_finish_matrix` | Oda-kaplama tablosu |
| `room_area_breakdown` | Taban/duvar/tavan alan dökümü |
| `room_naming_normalize` | Oda ismi normalize (EGBIM standardı) |
| `room_to_ifc_space` | IFC Space tipi etiketleme |

#### FamilyCreateOps.cs — 5 Aile Oluşturma Operasyonu (Grup 47)
| Op | Açıklama |
|---|---|
| `family_open_template` | .rft/.rfa şablon aç |
| `family_add_param` | Aileye parametrik boyut ekle |
| `family_load_to_project` | Projeye aile yükle/güncelle |
| `family_type_create` | Aile tipi oluştur/güncelle |
| `family_batch_load` | Klasörden toplu aile yükle |

#### StructuralCheckOps.cs — 6 Yapısal Doğrulama Operasyonu (Grup 48)
| Op | Açıklama |
|---|---|
| `structural_collect_all` | Tüm yapısal elementleri topla |
| `structural_ts500_section` | TS500 minimum kesit kontrolü |
| `structural_tbdy_params` | TBDY 2018 parametre atama |
| `structural_continuity_check` | Kolon kat sürekliliği kontrolü |
| `structural_level_summary` | Kat bazlı yapısal özet |
| `structural_material_check` | Beton/çelik sınıf doğrulama |

---

### 📋 Yeni Manifest Örnekleri (3 adet)

- `manifests/facade/eg_cephe_tam_pipeline.json` — 11 adım, preview+IFC
- `manifests/oda/eg_oda_kaplama_pipeline.json` — 8 adım, 8 kural, atomic
- `manifests/yapisal/eg_yapisal_ts500_tbdy.json` — 10 adım, TS500+TBDY

---

### 📊 Özet

| Metrik | v8 | v9 | Fark |
|---|---|---|---|
| Toplam op | 336 | 361 | +25 |
| Op dosyası | 35 | 39 | +4 |
| Test sayısı | 12 | 14 | +2 |
| Manifest örneği | (mevcut) | +3 | +3 |
| Bug düzeltmesi | 0 | 3 | +3 |
