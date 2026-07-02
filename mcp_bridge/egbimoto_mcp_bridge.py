#!/usr/bin/env python3
"""
EGBIMOTO MCP Köprüsü (Bridge)
==============================

Claude Desktop (veya MCP uyumlu herhangi bir AI ajanı) ile EGBIMOTO'nun Revit içinde
çalışan HTTP server'ı arasında köprü kurar.

MİMARİ:
    [Claude Desktop] --MCP/stdio--> [bu köprü] --HTTP--> [EGBIMOTO Server (Revit)]

Claude Desktop'ın KENDİ bağlamını kullanır: manifest üretimini Claude Desktop'taki
model yapar (op katalogunu görerek). Bu köprü yalnızca üç araç sunar:

  1. egbimoto_list_ops      — EGBIMOTO'nun yapabileceği tüm işlemlerin katalogu
  2. egbimoto_run_manifest  — verilen manifest'i Revit'te çalıştırır
  3. egbimoto_health        — server/doküman durumu

Claude bu araçları görür, op katalogunu okur, MANIFEST'İ KENDİSİ ÜRETİR ve çalıştırır.
Ek API anahtarı veya ikinci LLM çağrısı gerekmez.

KURULUM:
    pip install mcp httpx
    (Claude Desktop config'ine ekle — README'ye bakın)

ÇALIŞTIRMA:
    Claude Desktop bu betiği otomatik başlatır (config'deki komutla).
    Manuel test:  python egbimoto_mcp_bridge.py
"""

import os
import sys
import json
import asyncio
from typing import Any

import httpx

try:
    from mcp.server import Server
    from mcp.server.stdio import stdio_server
    from mcp.types import Tool, TextContent
except ImportError:
    sys.stderr.write(
        "HATA: 'mcp' kütüphanesi bulunamadı. Kurmak için:\n"
        "    pip install mcp httpx\n"
    )
    sys.exit(1)

# ── Yapılandırma ──────────────────────────────────────────────────────────────
# EGBIMOTO server adresi (ortam değişkeniyle override edilebilir)
EGBIMOTO_HOST = os.environ.get("EGBIMOTO_HOST", "127.0.0.1")
EGBIMOTO_PORT = os.environ.get("EGBIMOTO_PORT", "5577")
EGBIMOTO_TOKEN = os.environ.get("EGBIMOTO_TOKEN", "")  # opsiyonel
BASE_URL = f"http://{EGBIMOTO_HOST}:{EGBIMOTO_PORT}"

# HTTP istek zaman aşımı (manifest çalıştırma uzun sürebilir)
HTTP_TIMEOUT = float(os.environ.get("EGBIMOTO_TIMEOUT", "180"))

app = Server("egbimoto")


def _headers() -> dict:
    h = {"Content-Type": "application/json"}
    if EGBIMOTO_TOKEN:
        h["X-EGBIMOTO-Token"] = EGBIMOTO_TOKEN
    return h


async def _get(path: str) -> tuple[int, str]:
    async with httpx.AsyncClient(timeout=HTTP_TIMEOUT) as client:
        r = await client.get(f"{BASE_URL}{path}", headers=_headers())
        return r.status_code, r.text


async def _post(path: str, body: str) -> tuple[int, str]:
    async with httpx.AsyncClient(timeout=HTTP_TIMEOUT) as client:
        r = await client.post(f"{BASE_URL}{path}", headers=_headers(), content=body.encode("utf-8"))
        return r.status_code, r.text


# ── Araç tanımları ────────────────────────────────────────────────────────────
@app.list_tools()
async def list_tools() -> list[Tool]:
    return [
        Tool(
            name="egbimoto_list_ops",
            description=(
                "EGBIMOTO'nun (Revit BIM otomasyon platformu) yapabileceği TÜM işlemlerin "
                "(op) katalogunu döndürür. Her op'un adı, açıklaması, parametreleri, girdi/çıktı "
                "tipleri ve kategorisi vardır. Bir Revit BIM görevi istendiğinde ÖNCE bunu çağır, "
                "uygun op'ları seç ve bunlardan bir MANIFEST oluştur. Manifest şu yapıdadır: "
                '{"title","description","category","transaction_policy":"atomic|none","steps":'
                '[{"id","op","inputs":{...}}],"tags":[...]}. Adımlar sırayla çalışır; bir adımın '
                "çıktısı sonrakine girdi olabilir."
            ),
            inputSchema={"type": "object", "properties": {}},
        ),
        Tool(
            name="egbimoto_run_manifest",
            description=(
                "Bir EGBIMOTO manifest'ini aktif Revit modelinde ÇALIŞTIRIR. Manifest, "
                "egbimoto_list_ops'tan öğrenilen op'lardan oluşturulmalıdır. Çalıştırma sonucu "
                "(başarı/hata, her adımın durumu, log) döndürülür. transaction_policy='atomic' ise "
                "herhangi bir adım hata verirse tüm değişiklikler geri alınır. Modeli değiştiren "
                "(yazma) işlemlerde dikkatli ol; gerekirse önce kullanıcıya manifest'i göster."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "manifest": {
                        "type": "object",
                        "description": "Çalıştırılacak EGBIMOTO manifest nesnesi (JSON).",
                    }
                },
                "required": ["manifest"],
            },
        ),
        Tool(
            name="egbimoto_health",
            description=(
                "EGBIMOTO server'ının çalışıp çalışmadığını ve aktif Revit dokümanının adını "
                "kontrol eder. Bir göreve başlamadan önce bağlantıyı doğrulamak için kullanılabilir."
            ),
            inputSchema={"type": "object", "properties": {}},
        ),
    ]


# ── Araç çağrı yönlendirme ────────────────────────────────────────────────────
@app.call_tool()
async def call_tool(name: str, arguments: dict[str, Any]) -> list[TextContent]:
    try:
        if name == "egbimoto_health":
            status, text = await _get("/health")
            return [TextContent(type="text", text=_fmt(status, text))]

        if name == "egbimoto_list_ops":
            status, text = await _get("/ops")
            if status != 200:
                return [TextContent(type="text", text=_fmt(status, text))]
            # Katalog büyük olabilir; özet + tam JSON döndür
            return [TextContent(type="text", text=text)]

        if name == "egbimoto_run_manifest":
            manifest = arguments.get("manifest")
            if manifest is None:
                return [TextContent(type="text", text="HATA: 'manifest' parametresi gerekli.")]
            # Manifest dict ise JSON string'e çevir
            body = json.dumps(manifest, ensure_ascii=False) if isinstance(manifest, (dict, list)) else str(manifest)
            status, text = await _post("/run", body)
            return [TextContent(type="text", text=_fmt(status, text))]

        return [TextContent(type="text", text=f"HATA: Bilinmeyen araç '{name}'.")]

    except httpx.ConnectError:
        return [TextContent(type="text", text=(
            f"HATA: EGBIMOTO server'a bağlanılamadı ({BASE_URL}). "
            "Revit açık mı ve EGBIMOTO MCP Server başlatıldı mı? "
            "(EGBIMOTO şeridinden 'MCP Server Başlat' düğmesini kontrol edin.)"
        ))]
    except httpx.TimeoutException:
        return [TextContent(type="text", text=(
            f"HATA: İstek zaman aşımına uğradı ({HTTP_TIMEOUT}s). "
            "İşlem çok büyük olabilir veya Revit bir dialog beklerken takılmış olabilir."
        ))]
    except Exception as e:
        return [TextContent(type="text", text=f"HATA: {e}")]


def _fmt(status: int, text: str) -> str:
    """HTTP sonucunu okunabilir biçime getir."""
    if status == 200:
        return text
    return f"[HTTP {status}] {text}"


async def _main() -> None:
    async with stdio_server() as (read_stream, write_stream):
        await app.run(read_stream, write_stream, app.create_initialization_options())


if __name__ == "__main__":
    asyncio.run(_main())
