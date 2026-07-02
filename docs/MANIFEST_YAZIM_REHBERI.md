# EGBIMOTO — Manifest Yazım Rehberi

Manifest, bir BIM iş akışını tanımlayan JSON dosyasıdır. EGBIMOTO bunu bir DAG
(yönlü asiklik graf) olarak çözer: adımlar arası bağımlılıklar otomatik bulunur,
topolojik sıraya dizilir ve çalıştırılır.

## Temel Yapı

```json
{
  "title": "Türkçe başlık",
  "description": "Bu iş akışı ne yapar",
  "category": "metraj",
  "version": "1.0",
  "tags": ["metraj", "duvar", "boq"],
  "transaction_policy": "none",
  "steps": [
    { "id": "topla",  "op": "collect_walls" },
    { "id": "tablo",  "op": "show_table", "from": "topla" }
  ]
}
```

### Üst Düzey Alanlar

| Alan | Zorunlu | Açıklama |
|------|---------|----------|
| `title` | Evet | Kullanıcıya görünen başlık |
| `description` | Hayır | Browser'da gösterilen açıklama |
| `category` | Hayır | Manifest klasör adı (`metraj`, `qa`, `ifc`...) |
| `tags` | Hayır | Arama için etiketler |
| `version` | Hayır | Manifest sürümü |
| `transaction_policy` | Hayır | `none` / `atomic` / `preview` (varsayılan `none`) |
| `steps` | Evet | İş akışı adımları |

## Adım (Step) Yapısı

Her adımın bir `id` (benzersiz) ve bir `op` (operasyon adı) olmalıdır.

```json
{
  "id": "benzersiz_id",
  "op": "op_adi",
  "from": "onceki_adim_id",
  "from_many": ["adim1", "adim2"],
  "depends_on": ["adim3"],
  "condition": "$sayac > 0",
  "required": false,
  "cache": true,
  "params": { "anahtar": "deger" }
}
```

| Alan | Açıklama |
|------|----------|
| `id` | Benzersiz adım kimliği (zorunlu) |
| `op` | Çalıştırılacak operasyon — `OP_REFERANSI.md`'deki adlardan biri (zorunlu) |
| `from` | Bu adımın girdisi, belirtilen adımın çıktısıdır |
| `from_many` | Birden çok adımın çıktısını liste olarak alır |
| `depends_on` | Veri akışı olmadan sıralama bağımlılığı |
| `condition` | Koşul sağlanmazsa adım atlanır |
| `required` | `false` ise adım başarısız olsa da akış durmaz (varsayılan `true`) |
| `cache` | `true` ise sonuç önbelleğe alınır, tekrar hesaplanmaz |
| `params` | Op'a geçilen parametreler |

## Veri Akışı: `from` ve `$ref`

Bir adımın çıktısı sonraki adıma iki şekilde geçer:

**1. `from` ile (girdi olarak):**
```json
{ "id": "topla", "op": "collect_walls" },
{ "id": "filtre", "op": "filter_by_param", "from": "topla",
  "params": { "param_name": "EGBIM_PozNo", "operator": "exists" } }
```
`filtre` adımı, `topla` adımının çıktısını (duvar listesi) girdi olarak alır.

**2. `$ref` ile (parametre içinde):**
```json
{ "id": "say", "op": "show_count", "from": "topla" },
{ "id": "rapor", "op": "echo",
  "params": { "value": "Toplam: $say adet" } }
```
`$say`, `say` adımının çıktısına çözülür. Bu otomatik bir bağımlılık yaratır —
`say` her zaman `rapor`'dan önce çalışır.

> **Önemli (v10 fix):** `condition` içindeki `$ref`'ler de bağımlılık yaratır.
> `"condition": "$sayac > 0"` yazan adım, `sayac` adımına bağımlı sayılır.

## Birden Çok Girdi: `from_many`

Birkaç adımın çıktısını birleştirmek için:
```json
{ "id": "duvarlar", "op": "collect_walls" },
{ "id": "kolonlar", "op": "collect_columns" },
{ "id": "birlesik", "op": "merge_lists", "from_many": ["duvarlar", "kolonlar"] }
```

## Koşullu Çalıştırma: `condition`

```json
{ "id": "say", "op": "show_count", "from": "topla" },
{ "id": "uyari", "op": "show_result", "condition": "$say == 0",
  "params": { "title": "Uyarı: hiç eleman yok" } }
```

Desteklenen operatörler: `==`, `!=`, `>`, `<`, `>=`, `<=`, ve kelime operatörleri
`contains`, `not_contains`, `in`, `not_in`, `starts_with`, `ends_with`, `matches`.

## Transaction Politikası

**`none`** — Salt okuma / rapor üreten iş akışları. Modeli değiştirmez.

**`atomic`** — Tüm yazma adımları tek transaction. Bir zorunlu adım başarısız
olursa hepsi geri alınır. Poz atama, parametre doldurma gibi toplu yazmalarda kullanın.

**`preview`** — Önce 3D önizleme gösterir, kullanıcı onaylarsa atomik uygular.
Geometri oluşturan/değiştiren iş akışlarında (boşluk açma, donatı yerleştirme) kullanın.

## İyi Bir Manifest İçin İpuçları

1. **Export adımı ekleyin** — `export_xlsx`, `export_html_report` gibi. Linter
   bunu eksikse uyarır (`WARN_NO_EXPORT`).
2. **Bir özet/tablo adımı ekleyin** — `show_table` veya `validation_summary`.
3. **Export'ları `required: false` yapın** — dosya kilitliyse akış durmasın.
4. **Op adlarını `OP_REFERANSI.md`'den doğrulayın** — bilinmeyen op `OP_UNKNOWN`
   hatası verir.
5. **`id`'leri anlamlı tutun** — `topla_duvar`, `poz_esle` gibi. Hata mesajlarında
   bu id'ler görünür.

## Tam Örnek: Kalıp Metraj + Maliyet

```json
{
  "title": "Duvar Kalıp Metraj ve Maliyet",
  "description": "Tüm duvarların kalıp yüzey metrajını çıkarır, poz eşler ve maliyetlendirir.",
  "category": "kalip",
  "version": "1.0",
  "tags": ["kalip", "metraj", "maliyet", "duvar"],
  "transaction_policy": "none",
  "steps": [
    { "id": "duvarlar", "op": "collect_walls" },
    { "id": "kalip", "op": "kalip_all", "from": "duvarlar",
      "params": { "include_edges": "true" } },
    { "id": "poz", "op": "poz_match_keynote_aware", "from": "kalip" },
    { "id": "maliyet", "op": "calc_cost", "from": "poz",
      "params": { "quantity_field": "kalip_m2" } },
    { "id": "ozet", "op": "cost_summary", "from": "maliyet" },
    { "id": "tablo", "op": "show_table", "from": "ozet",
      "params": { "title": "Kalıp Maliyet Özeti" } },
    { "id": "excel", "op": "export_xlsx", "from": "maliyet", "required": false,
      "params": { "sheet_name": "Kalip_Maliyet" } }
  ]
}
```

Manifest'i `manifests/kalip/` klasörüne kaydedin; Browser otomatik bulur.

## Param Presence Semantiği (v13)

Her op parametresi `op_contracts.json`'da üç seviyeden birindedir:

- **`required`** — manifest'te verilmesi ZORUNLU. Kodda `ctx.RequireX("key")` ile
  okunur; eksikse çalışma zamanında step id'li net hata fırlatılır, ManifestValidator
  da statik olarak `PARAM_REQUIRED_MISSING` üretir.
- **`optional`** — verilmezse kontrattaki `default` kullanılır.
- **`recommended`** — kod yokluğu tolere eder (değer genellikle pipeline `from`
  zincirinden veya registry'den gelir), ancak açık vermek okunabilirliği artırır.

Yeni op yazarken kural: parametre gerçekten vazgeçilmezse `Require*` kullanın;
kontrat üretici (`deploy/generate_op_contracts.py`) presence'ı DOĞRUDAN koddan türetir.
Kontratı elle düzenlemeyin — kod değişince şu komutla yeniden üretin:

```bash
python3 deploy/generate_op_contracts.py
python3 deploy/generate_op_referansi.py
```

## İnteraktif Seçim — `selection_gate` (v13.5)

Toplu (kategori bazlı) işlemlerin yetmediği durumlar için: kullanıcı modelde
elle eleman seçer, sonuç pipeline'a devam eder.

```json
{ "id": "s1", "op": "selection_gate",
  "inputs": { "prompt": "Kontrol edilecek duvarları seçin",
              "mode": "multiple", "categories": ["OST_Walls"],
              "min_count": 1 } },
{ "id": "s2", "op": "selection_to_elements", "from": "s1",
  "condition": "$s1 == confirmed" },
{ "id": "s3", "op": "filter_by_param", "from": "s2", ... }
```

**Parametreler:** `prompt` (durum çubuğu mesajı), `mode` (`single`|`multiple`,
varsayılan `multiple`), `categories` (OST_ listesi — boşsa tüm kategoriler
seçilebilir), `min_count`/`max_count` (0 = sınırsız), `allow_linked`
(bağlantılı model elemanlarına izin, varsayılan `false`).

**Davranış:** `preview_gate`/`schedule_gate` ile aynı intercept deseni —
`selection_gate` normal bir op olarak ÇALIŞTIRILMAZ, `DagExecutor` onu
yakalayıp `UserSelectionCallback`'i çağırır. Kullanıcı Esc'e basarsa veya
`min_count` sağlanmazsa `vars[step.Id]` "cancelled" olur (`condition` ile
kontrol edin). Headless/test modda (callback null) seçim YAPILMAZ — otomatik
onay yerine boş/iptal sonuç döner; bu bilinçli bir tasarım kararıdır
(preview_gate'in aksine, rastgele eleman "seçmek" anlamsızdır).

`selection_to_elements`, `SelectionResultDto`'daki ID'leri gerçek `Element`
listesine çevirir — silinmiş/geçersiz ID'ler sessizce atlanır, loglanır.

## Sonuç Görselleştirme — `ManifestResultDto` (v13.5)

`show_table` ve `validation_summary` artık TaskDialog metin dökümü yerine
sıralanabilir WPF tablo penceresi açar: özet çubuğu, severity renk kodlama
(doğrulama sonuçlarında), seçili satırları modelde vurgulama ("Modelde
Göster" — `element_id` sütunu üzerinden), CSV dışa aktarım.

Yeni bir op yazarken kendi sonucunuzu bu pencerede göstermek isterseniz:

```csharp
var dto = ManifestResultAdapter.FromRows("Başlık", rows);   // List<Dictionary<string,object?>>
// veya: ManifestResultAdapter.FromValidationReport(report);
var uidoc = (ctx as RevitOpContext)?.UiDoc;
ManifestResultRendererRegistry.Show(uidoc, dto);
```

`rows` içinde `"element_id"` anahtarı varsa (mevcut konvansiyon — bkz.
`Rv.IdStr`), o satırlar otomatik olarak "Modelde Göster" ile ilişkilendirilir.

Genişletme: Kind'e özel bir görselleştirme (örn. Takeoff için pivot tablo)
eklemek isteyen biri `IManifestResultRenderer` uygulayıp
`ManifestResultRendererRegistry.Register(...)` ile kaydeder — `show_table`
gibi çağrı noktalarına dokunmaya gerek yoktur.
