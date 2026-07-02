# EGBIMOTO v13 — Required Semantiği Ayrıştırması

## Sorun
op_contracts.json üreticisi, default'u olmayan her parametreyi `required:true`
işaretliyordu; çalışma zamanında ise hiçbir şey bunu zorlamıyordu. Sonuç:
451 sahte "zorunlu param eksik" uyarısı ve gerçekte kırılacak manifestlerin
gözden kaçması. Ayrıca sayaçlar legacy `"params"` alias'ını okumadığı için
fiilen verilen girdiler "eksik" görünüyordu.

## Yeni Semantik (SSoT: kod)
| presence | Kod kalıbı | Statik kontrol | Çalışma zamanı |
|---|---|---|---|
| `required` | `ctx.RequireX("k")` | PARAM_REQUIRED_MISSING hatası | Exception (net mesaj + step id) |
| `optional` | `ctx.GetX("k", default)` | — (default kontratta) | Default kullanılır |
| `recommended` | `ctx.GetX("k")` | INFO | Boş/0 tolere edilir (pipeline/registry) |

Geriye dönük uyumluluk: `required` bool alanı korunur (`presence=="required"`).

## Değişiklikler
- **OpContext**: `RequireInt`, `RequireDouble`, `RequireList<T>`, `RequireStringList`
  eklendi (mevcut `RequireString`'in yanına). Yeni op yazarken zorunlu paramlar
  için daima `Require*` kullanılmalı.
- **deploy/generate_op_contracts.py (YENİ)**: Eksik SSoT aracı. `[EgOp]` metotlarını
  tarar (yorumlar soyulur — doc örnekleri hayalet op üretmez), param okuma
  kalıplarından presence/tip/default türetir, tek seviye delegasyonu çözer
  (`precheck_element_exists → ModelHasElements` gibi), registry okuma/yazmalarını
  çıkarır, koddan türetilemeyen alanları (out_fields, category...) mevcut kontrattan
  korur ve 356 manifest korpusuna karşı `usage_rate` hesaplar.
- **Kontrat şeması**: her param `presence` + `usage_rate` kazandı; kodda
  doğrulanamayan legacy paramlar `derived:"legacy"` ile işaretli.
- **Veri güdümlü migrasyon**: Korpusta %100 verilen 16 `recommended` param
  `Require*`'a taşındı (mevcut hiçbir manifest kırılmaz) — `collect_by_phase.phase_name`,
  `family_ensure_loaded.family_path`, `pivot_table.row/col/value_field`,
  `filter_rows.field`, `where.field/value`, `smart_check_mep_no_opening.host/mep_categories`
  vb. Bilinçli hariç tutulanlar: `table_validate_schema.optional_fields` (semantik
  olarak opsiyonel), `schedule_gate.title` (DagExecutor intercept'inde okunuyor).
  Kalan adaylar: `deploy/required_migration_onerisi.md`.
- **ManifestValidator**: 7. kontrol eklendi — `presence:"required"` param step
  inputs'ta yoksa `PARAM_REQUIRED_MISSING` hatası. Statik kontrol artık runtime
  davranışıyla birebir örtüşüyor. Kontrat cache'i (ops + required set) genişletildi.
- **generate_op_referansi.py**: üç seviyeli presence gösterimi (zorunlu / önerilen /
  varsayılan: ...). OP_REFERANSI.md yeniden üretildi.

## Sonuç: 451 sahte uyarı → 6 gerçek hata → 0
Yeni semantikle korpus denetimi 6 GERÇEK gizli bug ortaya çıkardı (bugün
çalıştırılsalar runtime'da patlayacaklardı) ve düzeltildi:
- `list_zip` 3 manifestte yanlış `from_many` kalıbıyla çağrılıyordu →
  op kontratına uygun `from` + `second_key`'e çevrildi (72/73/75_uretim).
- `structural_tbdy_params` 2 manifestte `deprem_bolgesi`'siz → `DD-2` (TBDY 2018
  standart tasarım deprem düzeyi) eklendi (46_yapisal, 13_ts500_tbdy).
- `create_view_filter` 1 manifestte `rule_value`'suz → manifest amacına uygun
  `"Undefined"` (tanımsız sistem tespiti) eklendi (69_view_filter_kalite).

Son denetim: 356 manifest, 3013 adım — 0 hata, 0 sahte uyarı.
Param dağılımı: 82 required / 726 optional / 99 recommended.
