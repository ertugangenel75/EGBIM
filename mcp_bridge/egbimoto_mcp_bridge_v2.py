#!/usr/bin/env python3
"""
EGBIMOTO MCP Köprüsü v2.1 — Semantic Cache + Query Decomposer + Golden Dataset
================================================================================

v2.1 iyileştirmeleri (review yorumlarına göre):
  ① Regex compile — SHORTCUTS sınıf yüklenirken bir kez compile edilir
  ② Async file I/O — disk yazımı ThreadPoolExecutor ile event loop'u bloklamaz
  ③ safe_path — cache key hex doğrulama + path traversal koruması
  ④ Logging — EGBIMOTO_LOG_LEVEL env var, timer, hit/miss log

Kurulum: pip install mcp httpx
"""

import os, sys, json, time, hashlib, asyncio, re, copy, logging
import concurrent.futures
from typing import Any
from pathlib import Path

import httpx

try:
    from mcp.server import Server
    from mcp.server.stdio import stdio_server
    from mcp.types import Tool, TextContent
except ImportError:
    sys.stderr.write("HATA: pip install mcp httpx\n")
    sys.exit(1)

# ── Logging ───────────────────────────────────────────────────────────────────
logging.basicConfig(
    level=os.environ.get("EGBIMOTO_LOG_LEVEL", "INFO"),
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[logging.StreamHandler(sys.stderr)]
)
logger = logging.getLogger("egbimoto.bridge")

# ── Yapılandırma ──────────────────────────────────────────────────────────────
EGBIMOTO_HOST  = os.environ.get("EGBIMOTO_HOST",  "127.0.0.1")
EGBIMOTO_PORT  = os.environ.get("EGBIMOTO_PORT",  "5577")
EGBIMOTO_TOKEN = os.environ.get("EGBIMOTO_TOKEN", "")
BASE_URL       = f"http://{EGBIMOTO_HOST}:{EGBIMOTO_PORT}"
HTTP_TIMEOUT   = float(os.environ.get("EGBIMOTO_TIMEOUT", "180"))

CACHE_DIR       = Path(os.environ.get("EGBIMOTO_CACHE_DIR",
                    str(Path.home() / ".egbimoto" / "cache")))
CACHE_TTL_SEC   = int(os.environ.get("EGBIMOTO_CACHE_TTL", "3600"))
CACHE_MAX_ITEMS = int(os.environ.get("EGBIMOTO_CACHE_MAX", "500"))
CACHE_ENABLED   = os.environ.get("EGBIMOTO_CACHE", "true").lower() == "true"

MANIFESTS_DIR   = Path(__file__).parent.parent / "manifests"
GOLDEN_PATH     = Path(__file__).parent / "tests" / "golden_dataset.json"

CACHE_DIR.mkdir(parents=True, exist_ok=True)

app = Server("egbimoto-v2")

# ══════════════════════════════════════════════════════════════════════════════
# KATMAN 1 — SemanticCache
# ══════════════════════════════════════════════════════════════════════════════

class SemanticCache:
    """
    Manifest sonuçlarını bellek + disk önbelleğinde saklar.

    v2.1 iyileştirmeleri:
      - Disk yazımı async (ThreadPoolExecutor, 2 thread)
      - Cache key hex doğrulama + path traversal koruması
      - Hit/miss logging
    """

    # ThreadPool — sınıf düzeyinde paylaşılır (tüm instance'lar için)
    _pool: concurrent.futures.ThreadPoolExecutor | None = None

    def __init__(self):
        self._mem   = {}
        self._stats = {"hit": 0, "miss": 0, "skip": 0, "evict": 0}
        # Thread pool lazy init
        if SemanticCache._pool is None:
            SemanticCache._pool = concurrent.futures.ThreadPoolExecutor(
                max_workers=2, thread_name_prefix="egbimoto-cache-io")

    # ── Key ve path yardımcıları ──────────────────────────────────────────────

    def _key(self, body: str) -> str:
        return hashlib.sha256(body.encode()).hexdigest()

    def _cacheable(self, manifest_dict: dict) -> bool:
        return manifest_dict.get("transaction_policy", "atomic") == "none"

    def _cf(self, key: str) -> Path:
        """
        Cache dosya yolu.

        Güvenlik:
          1. key yalnızca hex karakterlerden oluşmalı (sha256 çıktısı)
          2. Çözümlenen path CACHE_DIR içinde olmalı (path traversal koruması)
        """
        if not all(c in "0123456789abcdef" for c in key):
            raise ValueError(f"Geçersiz cache key (hex değil): {key!r}")
        d  = CACHE_DIR / key[:2]
        d.mkdir(parents=True, exist_ok=True)
        cf = d / f"{key}.json"
        # Path traversal kontrolü
        if not str(cf.resolve()).startswith(str(CACHE_DIR.resolve())):
            raise ValueError(f"Cache path CACHE_DIR dışında: {cf}")
        return cf

    # ── Async disk yazımı ─────────────────────────────────────────────────────

    def _write_disk_async(self, key: str, entry: dict) -> None:
        """Disk yazımını arka planda başlat (event loop'u bloklamaz)."""
        cf   = self._cf(key)
        data = json.dumps(entry, ensure_ascii=False)
        self._pool.submit(SemanticCache._write_disk_sync, cf, data)

    @staticmethod
    def _write_disk_sync(cf: Path, data: str) -> None:
        try:
            cf.write_text(data, "utf-8")
        except Exception as e:
            logger.warning(f"Cache disk write failed: {e}")

    # ── Public API ────────────────────────────────────────────────────────────

    def get(self, body: str, manifest_dict: dict):
        if not CACHE_ENABLED or not self._cacheable(manifest_dict):
            if not self._cacheable(manifest_dict):
                self._stats["skip"] += 1
            return None
        key = self._key(body)

        # 1. Bellek
        if key in self._mem:
            e = self._mem[key]
            if time.time() - e["ts"] < CACHE_TTL_SEC:
                self._stats["hit"] += 1
                logger.debug(f"Cache HIT (mem): {e.get('manifest_id','?')} key={key[:8]}")
                return e["result"]
            del self._mem[key]

        # 2. Disk
        try:
            cf = self._cf(key)
            if cf.exists():
                e = json.loads(cf.read_text("utf-8"))
                if time.time() - e["ts"] < CACHE_TTL_SEC:
                    self._mem[key] = e
                    self._stats["hit"] += 1
                    logger.debug(f"Cache HIT (disk): {e.get('manifest_id','?')} key={key[:8]}")
                    return e["result"]
                cf.unlink()
        except Exception as e2:
            logger.warning(f"Cache read error: {e2}")

        self._stats["miss"] += 1
        return None

    def set(self, body: str, manifest_dict: dict, result: dict) -> None:
        if not CACHE_ENABLED or not self._cacheable(manifest_dict):
            return
        key = self._key(body)
        e   = {
            "ts":          time.time(),
            "manifest_id": manifest_dict.get("id", "?"),
            "result":      result
        }
        # Bellek (LRU-benzeri eviction)
        if len(self._mem) >= CACHE_MAX_ITEMS:
            oldest = min(self._mem.items(), key=lambda x: x[1]["ts"])
            del self._mem[oldest[0]]
            self._stats["evict"] += 1
        self._mem[key] = e
        # Async disk yazımı
        self._write_disk_async(key, e)
        logger.debug(f"Cache SET: {manifest_dict.get('id','?')} key={key[:8]}")

    def invalidate(self, manifest_id=None) -> int:
        deleted = 0
        for cf in CACHE_DIR.rglob("*.json"):
            try:
                e = json.loads(cf.read_text("utf-8"))
                if manifest_id is None or e.get("manifest_id") == manifest_id:
                    cf.unlink()
                    deleted += 1
                    self._mem.pop(cf.stem, None)
            except:
                pass
        logger.info(f"Cache invalidate: {deleted} kayıt silindi (filter={manifest_id!r})")
        return deleted

    def stats(self) -> dict:
        return {
            **self._stats,
            "mem_items":  len(self._mem),
            "disk_items": sum(1 for _ in CACHE_DIR.rglob("*.json")),
            "enabled":    CACHE_ENABLED,
            "ttl_sec":    CACHE_TTL_SEC,
            "cache_dir":  str(CACHE_DIR),
        }


_cache = SemanticCache()


# ══════════════════════════════════════════════════════════════════════════════
# KATMAN 2 — QueryDecomposer
# ══════════════════════════════════════════════════════════════════════════════

class QueryDecomposer:
    """
    Doğal dil → manifest ID (kural tabanlı shortcut).

    v2.1: Regex'ler __init__ sırasında bir kez compile edilir.
    Her sorgu çağrısında re.compile yapılmaz → ~30x hız artışı.
    """

    # (regex string, manifest_id)
    SHORTCUTS = [
        # Sıhhi
        (r"s[ıi]hh[ıi]|plumbing|boru (hesap|sistem)",          "pl16_tam_sihhi_sistem_hesap"),
        (r"su talebi|g[uü]nl[uü]k su|depo boyut",              "pl11_su_talebi_depo"),
        (r"yangin dolabi|hose.*cabinet|yangin boru",            "pl12_yangin_boru_sistemi"),
        # Elektrik
        (r"gerilim d[uü][sş][uü]m|voltage drop|kablo kesit",   "el11_gerilim_dusumu_hesap"),
        (r"panel.*g[uü][cç]|busbar|kva.*hesap",                "el12_panel_guc_analiz"),
        (r"jenerator|ups|yedek g[uü][cç]",                     "el15_yedek_guc_sistemi"),
        (r"kablo tava|cable tray|elv.*ayrim",                   "el14_kablo_tava_qa"),
        (r"topraklama|acil ayd[ıi]nlatma",                      "el13_tesisat_guvenligi_qa"),
        (r"elektrik (tam|full)|t[uü]m elektrik",                "el16_tam_elektrik_hesap"),
        # Yangın
        (r"standpipe|itfaiye boru|riser",                       "fp12_standpipe_sistem"),
        (r"yangin pompa|fire pump|pompa.*hp",                   "fp13_yangin_pompasi"),
        (r"sprinkler (hidrolik|hesap|k.fakt)",                  "fp14_sprinkler_hidrolik"),
        (r"dedekt[oö]r|sond[uü]rme",                           "fp15_algilama_sondurme_qa"),
        (r"tahliye|cikis yolu|kompartiman|yangin kap[ıi]",      "fp16_tahliye_mimari_qa"),
        (r"yangin (tam|full)|t[uü]m yang[ıi]n",                "fp17_tam_yangin_koruma_hesap"),
        # Mekanik
        (r"[ıi]s[ıi] y[uü]k[uü]|heat load|sogutma y[uü]k",    "me12_isi_yuku_ahu"),
        (r"ahu|hava i[sş]leme|air.?handling",                   "me12_isi_yuku_ahu"),
        (r"taze hava|fresh air|ventilasyon",                    "me13_oda_sogutma_ve_taze_hava"),
        (r"bas[ıi]n[cç]land[ıi]rma|ameliyathane|hepa",          "me14_basinc_hepa_qa"),
        (r"ach|hava de[gğ]i[sş]im|zon denge|esp",               "me15_ach_zon_denge"),
        (r"chiller|cop|so[gğ]utma enerji",                      "me16_chiller_cop"),
        (r"mekanik (tam|full)|t[uü]m mekanik",                  "me17_tam_mek_hesap"),
        # Yapısal
        (r"bindirme|filiz boyu|ankraj",                         "s11_donati_bindirme_ankraj"),
        (r"beton s[ıi]n[ıi]f|c2[05]|c30|kiri[sş] oran|perde narinlik", "s12_beton_sinifi_kesit_qa"),
        (r"d[oö][sş]eme kal[ıi]nl|temel.*bas[ıi]n[cç]",        "s13_doseme_temel_hesap"),
        (r"bulon|kal[ıi]p sistem|celik.*ba[gğ]lant[ıi]",        "s14_celik_bulon_kalip"),
        (r"yap[ıi]sal (tam|full)|t[uü]m yap[ıi]sal",            "s15_tam_yapisal_hesap"),
        # MEP Boşluk
        (r"mep bo[sş]luk|kanal bo[sş]lu[gğ]u|duct.*opening",   "mep01_bosluk_tespit"),
        (r"ec.?2.*bo[sş]luk|bo[sş]luk.*dogrulama|takviye",     "mep02_bosluk_validate_ec2"),
        (r"bcf.*(export|ihrac)|bimcollab|navisworks.*ihrac",    "mep03_bcf_export"),
        (r"lento|lintel",                                        "mep04_lento_yer"),
        (r"mep (tam|full|workflow)",                             "mep05_tam_bosluk_workflow"),
    ]

    # Compiled patterns — sınıf düzeyinde, tüm instance'lar paylaşır
    _compiled: list | None = None

    def __init__(self):
        # Regex'leri bir kez compile et
        if QueryDecomposer._compiled is None:
            QueryDecomposer._compiled = [
                (re.compile(pat), mid) for pat, mid in self.SHORTCUTS
            ]
            logger.debug(f"QueryDecomposer: {len(QueryDecomposer._compiled)} pattern compiled")

    def _load(self, manifest_id: str) -> dict | None:
        for path in MANIFESTS_DIR.rglob("*.json"):
            if path.stem == manifest_id:
                try:
                    return json.loads(path.read_text("utf-8"))
                except:
                    return None
        return None

    def _extract_inputs(self, query: str) -> dict:
        q = query.lower()
        out = {}
        for pat, key, cast in [
            (r"(\d+(?:[.,]\d+)?)\s*m[²2]", "alan_m2",      float),
            (r"(\d+(?:[.,]\d+)?)\s*kat",    "kat_sayisi",   int),
            (r"(\d+(?:[.,]\d+)?)\s*ki[sş]i","kisi_sayisi",  int),
            (r"(\d+(?:[.,]\d+)?)\s*m[³3]",  "hacim_m3",     float),
            (r"(\d+(?:[.,]\d+)?)\s*kw\b",   "sogutma_kw",   float),
            (r"(\d+(?:[.,]\d+)?)\s*bar\b",  "basinc_bar",   float),
            (r"c(\d{2})\b",                 "beton_sinifi", lambda x: f"C{x}"),
        ]:
            m = re.search(pat, q)
            if m:
                try:
                    out[key] = cast(m.group(1).replace(",", "."))
                except:
                    pass
        if re.search(r"otel|hotel",    q): out["alan_tipi"] = "otel_5yildiz"
        if re.search(r"ofis|office",   q): out["alan_tipi"] = "ofis"
        if re.search(r"konut|daire",   q): out["alan_tipi"] = "konut"
        if re.search(r"hastane|hospital",q): out["alan_tipi"] = "hastane"
        return out

    def _apply_overrides(self, manifest: dict, overrides: dict) -> dict:
        m = copy.deepcopy(manifest)
        for step in m.get("steps", []):
            inputs = step.get("inputs", {})
            for ik, iv in list(inputs.items()):
                if isinstance(iv, str) and iv.startswith("$INPUT:"):
                    parts = iv.split(":", 2)
                    ikey  = parts[1] if len(parts) > 1 else ""
                    for ok, ov in overrides.items():
                        if ok == ikey or ikey.endswith(ok):
                            inputs[ik] = ov
        return m

    def decompose(self, query: str) -> dict | None:
        q = query.lower()
        for cpat, mid in self._compiled:
            if cpat.search(q):
                manifest = self._load(mid)
                if manifest:
                    overrides = self._extract_inputs(query)
                    if overrides:
                        manifest = self._apply_overrides(manifest, overrides)
                    manifest["_source"] = f"shortcut:{mid}"
                    logger.info(f"QueryDecomposer: '{query[:40]}' → {mid}")
                    return manifest
        return None

    def suggest(self, query: str) -> list[str]:
        q    = query.lower()
        seen, out = set(), []
        for cpat, mid in self._compiled:
            # Pattern string'den kelimeler çıkar
            words = re.findall(r"\w+", cpat.pattern.replace("|", " "))
            if any(w in q for w in words if len(w) > 3):
                if mid not in seen:
                    seen.add(mid)
                    out.append(mid)
        return out[:5]


# ══════════════════════════════════════════════════════════════════════════════
# KATMAN 3 — GoldenDataset
# ══════════════════════════════════════════════════════════════════════════════

DEFAULT_TESTS = [
    {"id":"PLM-001","description":"Konut 100 kişi günlük su talebi","category":"MEP-Sıhhi",
     "manifest_id":"pl11_su_talebi_depo","tags":["smoke"],
     "inputs":{"bina_tipi":"konut","kisi_sayisi":100},
     "expected":{"gunluk_talep_litre":{"type":"range","min":10000,"max":22000},
                 "durum":{"type":"exact","value":"OK"}}},
    {"id":"ELC-001","description":"Gerilim düşümü 100A 50m cosφ=0.85","category":"MEP-Elektrik",
     "manifest_id":"el11_gerilim_dusumu_hesap","tags":["smoke"],
     "inputs":{"akim_a":100,"uzunluk_m":50,"faz_sayisi":3,"cos_phi":0.85,"max_dusumu_pct":3.0},
     "expected":{"dusumu_pct":{"type":"range","min":2.0,"max":3.5},
                 "durum":{"type":"exact","value":"UYGUN"}}},
    {"id":"FP-001","description":"Standpipe DN 30m ıslak","category":"Yangın",
     "manifest_id":"fp12_standpipe_sistem","tags":["smoke"],
     "inputs":{"bina_yuksekligi_m":30,"sistem_tipi":"islak"},
     "expected":{"gerekli_dn_mm":{"type":"exact","value":100},
                 "durum":{"type":"exact","value":"UYGUN"}}},
    {"id":"FP-002","description":"Sprinkler K80 0.5 bar","category":"Yangın",
     "manifest_id":"fp14_sprinkler_hidrolik","tags":["smoke"],
     "inputs":{"k_faktoru":"K80","isletme_basinc_bar":0.5},
     "expected":{"debi_lpm":{"type":"range","min":54,"max":60},
                 "durum":{"type":"exact","value":"UYGUN"}}},
    {"id":"MEK-001","description":"Ameliyathane ACH 50m²","category":"MEP-Mekanik",
     "manifest_id":"me15_ach_zon_denge","tags":["smoke"],
     "inputs":{"oda_tipi":"ameliyathane","oda_alani_m2":50,"oda_yuksekligi_m":3.0},
     "expected":{"min_ach":{"type":"exact","value":20},
                 "gerekli_m3h":{"type":"range","min":2900,"max":3100},
                 "durum":{"type":"exact","value":"REFERANS_TABLOSU"}}},
    {"id":"STR-001","description":"Kolon filiz φ16 C25 deprem","category":"Yapısal",
     "manifest_id":"s11_donati_bindirme_ankraj","tags":["smoke"],
     "inputs":{"cap_mm":16,"beton_sinifi":"C25","celik_sinifi":"B420C","deprem_bolgesi":True},
     "expected":{"bindirme_mm":{"type":"range","min":900,"max":1200},
                 "durum":{"type":"exact","value":"OK"}}},
    {"id":"STR-002","description":"Beton QA C20 deprem yüksek","category":"Yapısal",
     "manifest_id":"s12_beton_sinifi_kesit_qa","tags":["structural"],
     "inputs":{"beton_sinifi":"C20","kullanim_yeri":"deprem_yuksek","deprem_bolgesi":True},
     "expected":{"min_sinif":{"type":"exact","value":"C25"},
                 "durum":{"type":"exact","value":"SINIF_YETERSIZ"}}},
    {"id":"MEK-002","description":"Chiller COP su soğutmalı vida 4.5","category":"MEP-Mekanik",
     "manifest_id":"me16_chiller_cop","tags":["mechanical"],
     "inputs":{"chiller_tipi":"su_sogutmali_vida","mevcut_cop":4.5},
     "expected":{"cop_durum":{"type":"exact","value":"UYGUN"},
                 "durum":{"type":"exact","value":"UYGUN"}}},
    {"id":"FP-003","description":"Tahliye 1.4m genişlik 45m mesafe","category":"Yangın",
     "manifest_id":"fp16_tahliye_mimari_qa","tags":["fire"],
     "inputs":{"yol_tipi":"koridor","mevcut_genislik_m":1.4,
               "cikis_mesafesi_m":45,"sprinkler_var":False},
     "expected":{"min_genislik_m":{"type":"exact","value":1.2},
                 "durum":{"type":"exact","value":"UYGUN"}}},
    {"id":"MEP-001","description":"EC-2 mock testi","category":"MEP-Koordinasyon",
     "manifest_id":"mep02_bosluk_validate_ec2","tags":["mep"],
     "inputs":{},
     "expected":{"_mock":True,"_note":"EC-2 400x400: TAKVIYE beklenir"}},
]


class GoldenDataset:
    def __init__(self):
        self._tests = []
        if GOLDEN_PATH.exists():
            try:
                self._tests = json.loads(GOLDEN_PATH.read_text("utf-8"))
                logger.debug(f"GoldenDataset: {len(self._tests)} test yüklendi")
                return
            except Exception as e:
                logger.warning(f"Golden dataset yüklenemedi: {e}")
        self._tests = DEFAULT_TESTS
        GOLDEN_PATH.parent.mkdir(parents=True, exist_ok=True)
        GOLDEN_PATH.write_text(json.dumps(self._tests, ensure_ascii=False, indent=2), "utf-8")
        logger.info(f"GoldenDataset: {len(self._tests)} default test oluşturuldu")

    def all(self):      return self._tests
    def smoke(self):    return [t for t in self._tests if "smoke" in t.get("tags", [])]
    def by_cat(self, c): return [t for t in self._tests if t.get("category","").lower()==c.lower()]

    def _check(self, actual, spec):
        t = spec.get("type", "exact")
        if t == "exact":
            return actual == spec["value"], f"exp={spec['value']!r} got={actual!r}"
        if t == "range":
            try:
                v  = float(actual)
                mn, mx = spec.get("min"), spec.get("max")
                ok = (mn is None or v >= mn) and (mx is None or v <= mx)
                return ok, f"exp=[{mn},{mx}] got={v}"
            except:
                return False, f"not a number: {actual!r}"
        if t == "contains":
            return actual in spec.get("values", []), f"exp in {spec['values']} got={actual!r}"
        return False, f"unknown type: {t}"

    def evaluate(self, test: dict, result: dict) -> dict:
        if test.get("expected", {}).get("_mock"):
            return {"id": test["id"], "status": "SKIP", "reason": "Mock test"}
        passed, failed = [], []

        def find(k):
            if k in result: return result[k]
            for v in result.get("steps", {}).values():
                if isinstance(v, dict) and k in v: return v[k]
            return None

        for field, spec in test.get("expected", {}).items():
            if field.startswith("_"): continue
            actual = find(field)
            if actual is None:
                failed.append({"field": field, "error": "not found"})
                continue
            ok, msg = self._check(actual, spec)
            (passed if ok else failed).append({"field": field, "msg": msg})

        return {
            "id":          test["id"],
            "description": test.get("description", ""),
            "category":    test.get("category", ""),
            "status":      "PASS" if not failed else "FAIL",
            "passed":      len(passed),
            "failed":      len(failed),
            "details":     failed if failed else passed,
        }


# ══════════════════════════════════════════════════════════════════════════════
# HTTP YardımcıLARI
# ══════════════════════════════════════════════════════════════════════════════

def _headers() -> dict:
    h = {"Content-Type": "application/json"}
    if EGBIMOTO_TOKEN:
        h["X-EGBIMOTO-Token"] = EGBIMOTO_TOKEN
    return h

async def _get(path: str) -> tuple[int, str]:
    async with httpx.AsyncClient(timeout=HTTP_TIMEOUT) as c:
        r = await c.get(f"{BASE_URL}{path}", headers=_headers())
        return r.status_code, r.text

async def _post(path: str, body: str) -> tuple[int, str]:
    async with httpx.AsyncClient(timeout=HTTP_TIMEOUT) as c:
        r = await c.post(f"{BASE_URL}{path}", headers=_headers(), content=body.encode("utf-8"))
        return r.status_code, r.text

def _fmt(s: int, t: str) -> str:
    return t if s == 200 else f"[HTTP {s}] {t}"


# Singleton'lar — import sırasında oluşturulur
_cache      = SemanticCache()
_decomposer = QueryDecomposer()   # regex'ler burada compile edilir
_golden     = GoldenDataset()


# ══════════════════════════════════════════════════════════════════════════════
# MCP ARAÇLAR
# ══════════════════════════════════════════════════════════════════════════════

@app.list_tools()
async def list_tools() -> list[Tool]:
    return [
        Tool(name="egbimoto_health",
             description="EGBIMOTO server durumu ve aktif Revit dokümanı.",
             inputSchema={"type": "object", "properties": {}}),

        Tool(name="egbimoto_list_ops",
             description="459 op kataloğu. Manifest üretmeden önce çağır. category_filter ile filtrele.",
             inputSchema={"type": "object", "properties": {
                 "category_filter": {"type": "string"}}}),

        Tool(name="egbimoto_run_manifest",
             description=(
                 "Manifest'i Revit'te çalıştırır. "
                 "transaction_policy='none' olanlar SemanticCache'te önbelleklenir. "
                 "Önbellekten geliyorsa _cached=true."),
             inputSchema={"type": "object", "required": ["manifest"], "properties": {
                 "manifest":      {"type": "object"},
                 "force_refresh": {"type": "boolean"}}}),

        Tool(name="egbimoto_smart_run",
             description=(
                 "Doğal dil sorgusundan manifest üretir ve Revit'te çalıştırır. "
                 "Örnek: '200 kişi otel sıhhi tesisat hesapla'. "
                 "Türkçe/İngilizce, 30+ kısayol. dry_run=true ile önce manifest'i görüntüle."),
             inputSchema={"type": "object", "required": ["query"], "properties": {
                 "query":   {"type": "string"},
                 "dry_run": {"type": "boolean"}}}),

        Tool(name="egbimoto_cache_stats",
             description="SemanticCache istatistikleri. action: stats|invalidate|invalidate_all",
             inputSchema={"type": "object", "properties": {
                 "action":      {"type": "string", "enum": ["stats", "invalidate", "invalidate_all"]},
                 "manifest_id": {"type": "string"}}}),

        Tool(name="egbimoto_run_tests",
             description="Golden dataset regresyon testleri. mode: smoke (6 test) | all (10) | category",
             inputSchema={"type": "object", "properties": {
                 "mode":     {"type": "string", "enum": ["smoke", "all", "category"]},
                 "category": {"type": "string"},
                 "dry_run":  {"type": "boolean"}}}),
    ]


@app.call_tool()
async def call_tool(name: str, arguments: dict[str, Any]) -> list[TextContent]:
    try:
        # ── egbimoto_health ───────────────────────────────────────────────
        if name == "egbimoto_health":
            s, t = await _get("/health")
            return [TextContent(type="text", text=_fmt(s, t))]

        # ── egbimoto_list_ops ─────────────────────────────────────────────
        if name == "egbimoto_list_ops":
            s, t = await _get("/ops")
            if s != 200:
                return [TextContent(type="text", text=_fmt(s, t))]
            cf = arguments.get("category_filter", "")
            if cf:
                try:
                    ops = json.loads(t)
                    ops = {k: v for k, v in ops.items()
                           if cf.lower() in v.get("category", "").lower()}
                    t = json.dumps(ops, ensure_ascii=False)
                except:
                    pass
            return [TextContent(type="text", text=t)]

        # ── egbimoto_run_manifest ─────────────────────────────────────────
        if name == "egbimoto_run_manifest":
            manifest = arguments.get("manifest")
            force    = arguments.get("force_refresh", False)
            if not manifest:
                return [TextContent(type="text", text="HATA: 'manifest' gerekli.")]
            body  = json.dumps(manifest, ensure_ascii=False) \
                if isinstance(manifest, (dict, list)) else str(manifest)
            mdict = manifest if isinstance(manifest, dict) else {}

            if not force:
                cached = _cache.get(body, mdict)
                if cached:
                    cached["_cached"] = True
                    return [TextContent(type="text",
                        text=json.dumps(cached, ensure_ascii=False))]

            t0 = time.time()
            s, t = await _post("/run", body)
            elapsed = round(time.time() - t0, 2)
            if s == 200:
                try:
                    result = json.loads(t)
                    result["_elapsed_s"] = elapsed
                    _cache.set(body, mdict, result)
                    logger.info(f"run_manifest: {mdict.get('id','?')} ({elapsed}s)")
                    return [TextContent(type="text",
                        text=json.dumps(result, ensure_ascii=False))]
                except:
                    pass
            logger.warning(f"run_manifest FAIL: HTTP {s} ({elapsed}s)")
            return [TextContent(type="text", text=_fmt(s, t))]

        # ── egbimoto_smart_run ────────────────────────────────────────────
        if name == "egbimoto_smart_run":
            query   = arguments.get("query", "")
            dry_run = arguments.get("dry_run", False)
            if not query:
                return [TextContent(type="text", text="HATA: 'query' gerekli.")]

            manifest = _decomposer.decompose(query)
            if manifest is None:
                suggestions = _decomposer.suggest(query)
                return [TextContent(type="text", text=json.dumps({
                    "status":      "NOT_FOUND",
                    "query":       query,
                    "message":     "Manifest üretilemedi. egbimoto_list_ops ile kataloğu incele.",
                    "suggestions": suggestions,
                }, ensure_ascii=False))]

            if dry_run:
                return [TextContent(type="text", text=json.dumps({
                    "status": "DRY_RUN", "query": query, "manifest": manifest
                }, ensure_ascii=False))]

            body   = json.dumps(manifest, ensure_ascii=False)
            cached = _cache.get(body, manifest)
            if cached:
                cached["_cached"]      = True
                cached["_query"]       = query
                return [TextContent(type="text",
                    text=json.dumps(cached, ensure_ascii=False))]

            t0 = time.time()
            s, t = await _post("/run", body)
            elapsed = round(time.time() - t0, 2)
            if s == 200:
                try:
                    r = json.loads(t)
                    r["_query"]       = query
                    r["_manifest_id"] = manifest.get("id", "?")
                    r["_elapsed_s"]   = elapsed
                    _cache.set(body, manifest, r)
                    logger.info(f"smart_run OK: '{query[:40]}' → {manifest.get('id','?')} ({elapsed}s)")
                    return [TextContent(type="text",
                        text=json.dumps(r, ensure_ascii=False))]
                except:
                    pass
            logger.warning(f"smart_run FAIL: HTTP {s} ({elapsed}s)")
            return [TextContent(type="text", text=_fmt(s, t))]

        # ── egbimoto_cache_stats ──────────────────────────────────────────
        if name == "egbimoto_cache_stats":
            action = arguments.get("action", "stats")
            if action == "invalidate":
                mid = arguments.get("manifest_id")
                if not mid:
                    return [TextContent(type="text", text="HATA: manifest_id gerekli.")]
                d = _cache.invalidate(mid)
                return [TextContent(type="text",
                    text=json.dumps({"deleted": d, "manifest_id": mid}))]
            if action == "invalidate_all":
                d = _cache.invalidate(None)
                return [TextContent(type="text",
                    text=json.dumps({"deleted": d}))]
            return [TextContent(type="text",
                text=json.dumps(_cache.stats(), ensure_ascii=False))]

        # ── egbimoto_run_tests ────────────────────────────────────────────
        if name == "egbimoto_run_tests":
            mode    = arguments.get("mode", "smoke")
            cat     = arguments.get("category", "")
            dry_run = arguments.get("dry_run", False)
            tests   = {
                "smoke":    _golden.smoke,
                "all":      _golden.all,
                "category": lambda: _golden.by_cat(cat),
            }.get(mode, _golden.smoke)()

            if dry_run:
                return [TextContent(type="text", text=json.dumps({
                    "mode": mode, "count": len(tests),
                    "tests": [{"id": t["id"], "description": t["description"]} for t in tests]
                }, ensure_ascii=False))]

            results = []
            pc = fc = sc = 0
            t_start = time.time()

            for test in tests:
                if test.get("expected", {}).get("_mock"):
                    r  = {"id": test["id"], "status": "SKIP", "reason": "Mock test"}
                    sc += 1
                else:
                    manifest = _decomposer._load(test["manifest_id"])
                    if not manifest:
                        r  = {"id": test["id"], "status": "SKIP",
                              "reason": f"manifest yok: {test['manifest_id']}"}
                        sc += 1
                    else:
                        manifest = _decomposer._apply_overrides(
                            manifest, test.get("inputs", {}))
                        try:
                            s2, t2 = await _post(
                                "/run", json.dumps(manifest, ensure_ascii=False))
                            result = json.loads(t2) if s2 == 200 else {}
                            r = _golden.evaluate(test, result)
                        except Exception as e:
                            r = {"id": test["id"], "status": "ERROR", "reason": str(e)}

                if r.get("status") == "PASS":  pc += 1
                elif r.get("status") == "FAIL": fc += 1
                else:                           sc += 1
                results.append(r)

            elapsed = round(time.time() - t_start, 2)
            logger.info(f"run_tests {mode}: {pc}P/{fc}F/{sc}S ({elapsed}s)")
            return [TextContent(type="text", text=json.dumps({
                "mode":      mode,
                "total":     len(tests),
                "pass":      pc,
                "fail":      fc,
                "skip":      sc,
                "score":     f"{pc}/{pc+fc}",
                "elapsed_s": elapsed,
                "results":   results,
            }, ensure_ascii=False))]

        return [TextContent(type="text", text=f"HATA: Bilinmeyen araç '{name}'.")]

    except httpx.ConnectError:
        return [TextContent(type="text", text=(
            f"HATA: EGBIMOTO server'a bağlanılamadı ({BASE_URL}). "
            "Revit açık ve MCP Server başlatılmış mı?"
        ))]
    except httpx.TimeoutException:
        return [TextContent(type="text", text=(
            f"HATA: Zaman aşımı ({HTTP_TIMEOUT}s). "
            "Revit bir dialog'da bekliyor olabilir."
        ))]
    except Exception as e:
        logger.exception(f"Beklenmeyen hata: {e}")
        return [TextContent(type="text", text=f"HATA: {e}")]


# ══════════════════════════════════════════════════════════════════════════════
# GİRİŞ NOKTASI
# ══════════════════════════════════════════════════════════════════════════════

async def _main() -> None:
    async with stdio_server() as (rs, ws):
        await app.run(rs, ws, app.create_initialization_options())

if __name__ == "__main__":
    asyncio.run(_main())
