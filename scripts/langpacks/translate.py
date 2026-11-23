#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ترجمهٔ انبوه رشته‌های رابط کاربری به ۶ زبان با z-ai CLI — بستهٔ زبان hr-lang-pack.

- ورودی: keys.json (استخراج‌شده از HrLang.cs)
- کش: cache/<code>.json → اجرای مجدد از همان‌جا ادامه می‌دهد
- خروجی: out/hr-lang-<code>.json با قالب درون‌ریزی پنل /Langs
استفاده: python3 translate.py [--only ar,tr]
"""
import json
import os
import re
import subprocess
import sys
import tempfile
import time
from concurrent.futures import ThreadPoolExecutor, as_completed

BASE = "/home/z/my-project/scripts/langpacks"
KEYS = json.load(open(f"{BASE}/keys.json", encoding="utf-8"))

LANGS = [
    {"code": "ar", "title": "العربية",  "dir": "rtl", "name": "Arabic"},
    {"code": "tr", "title": "Türkçe",   "dir": "ltr", "name": "Turkish"},
    {"code": "de", "title": "Deutsch",  "dir": "ltr", "name": "German"},
    {"code": "fr", "title": "Français", "dir": "ltr", "name": "French"},
    {"code": "ru", "title": "Русский",  "dir": "ltr", "name": "Russian"},
    {"code": "zh", "title": "简体中文",  "dir": "ltr", "name": "Simplified Chinese"},
]

BATCH = 80
WORKERS = 3
CALL_TIMEOUT = 240
RETRIES = 6
BACKOFF = [3, 8, 15, 25, 40]

SYSTEM = (
    "You are a professional software localizer for an enterprise HR / attendance / payroll "
    "web application used by Iranian companies. Translate Persian (Farsi) UI strings into {name}. "
    "The user message is a JSON array of items: i = index, fa = Persian source string, "
    "en = existing English translation (meaning reference). Rules:\n"
    "1) Natural, concise, professional UI wording in {name}.\n"
    "2) Keep placeholders like {0} {1} and <b> </b> EXACTLY intact.\n"
    "3) Domain terms: حضور و غیاب=attendance, حقوق و دستمزد=payroll, قلم=pay item, "
    "مأموریت=mission, مرخصی=leave, تردد=traffic/log, مجوز=license/permit (context).\n"
    "4) Output EXACTLY one line per item in the form: i|||translation\n"
    "No explanations, no extra lines, same count and order. Never wrap the translation in quotes."
)


def call_llm(payload: str, lang_name: str) -> str:
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as tf:
        outp = tf.name
    try:
        for attempt in range(1, RETRIES + 1):
            try:
                r = subprocess.run(
                    ["z-ai", "chat", "-s", SYSTEM.replace("{name}", lang_name), "-p", payload, "-o", outp],
                    capture_output=True, timeout=CALL_TIMEOUT)
                data = json.load(open(outp, encoding="utf-8"))
                content = data["choices"][0]["message"]["content"]
                if content and content.strip():
                    return content
                raise ValueError("empty content")
            except Exception as e:
                if attempt == RETRIES:
                    raise RuntimeError(f"z-ai failed after {RETRIES} tries: {e} "
                                       f"rc={getattr(r, 'returncode', '?')} "
                                       f"stderr={getattr(r, 'stderr', b'')[-200:]!r}")
                wait = BACKOFF[min(attempt - 1, len(BACKOFF) - 1)]
                time.sleep(wait)
        raise RuntimeError("unreachable")
    finally:
        try:
            os.unlink(outp)
        except OSError:
            pass


LINE_RE = re.compile(r'^\s*(\d+)\s*\|\|\|\s*(.*)$', re.M)


def translate_batch(lang: dict, chunk: list, cache: dict) -> int:
    """یک بسته را ترجمه و در cache می‌نویسد — تعداد ترجمهٔ تازه را برمی‌گرداند."""
    todo = [it for it in chunk if str(it["i"]) not in cache]
    if not todo:
        return 0
    payload = json.dumps(
        [{"i": it["i"], "fa": it["fa"], "en": it["en"]} for it in todo],
        ensure_ascii=False)
    content = call_llm(payload, lang["name"])
    got = {}
    for m in LINE_RE.finditer(content):
        idx, val = m.group(1), m.group(2).strip()
        if val:
            got[idx] = val
    fresh = 0
    for it in todo:
        v = got.get(str(it["i"]))
        if v:
            cache[str(it["i"])] = v
            fresh += 1
    return fresh


def run_lang(lang: dict) -> dict:
    code = lang["code"]
    cache_path = f"{BASE}/cache/{code}.json"
    cache = {}
    if os.path.exists(cache_path):
        cache = json.load(open(cache_path, encoding="utf-8"))
    chunks = [KEYS[i:i + BATCH] for i in range(0, len(KEYS), BATCH)]
    t0 = time.time()
    done = 0
    with ThreadPoolExecutor(max_workers=WORKERS) as ex:
        futs = {ex.submit(translate_batch, lang, ch, cache): ch for ch in chunks}
        for fu in as_completed(futs):
            try:
                done += fu.result()
            except Exception as e:
                print(f"  [{code}] batch failed: {e}", flush=True)
            # ذخیرهٔ دوره‌ای — ادامه از نقطهٔ توقف
            if done and done % (BATCH * WORKERS) < BATCH:
                json.dump(cache, open(cache_path, "w", encoding="utf-8"), ensure_ascii=False)
    json.dump(cache, open(cache_path, "w", encoding="utf-8"), ensure_ascii=False)

    missing = [it["fa"] for it in KEYS if str(it["i"]) not in cache]
    # پاس تعمیر — فقط کلیدهای جاافتاده، بسته‌های کوچک‌تر
    if missing:
        by_i = {it["i"]: it for it in KEYS}
        todo_idx = [it["i"] for it in KEYS if str(it["i"]) not in cache]
        small = [todo_idx[i:i + 25] for i in range(0, len(todo_idx), 25)]
        with ThreadPoolExecutor(max_workers=WORKERS) as ex:
            futs = [ex.submit(translate_batch, lang,
                              [by_i[i] for i in ch], cache) for ch in small]
            for fu in as_completed(futs):
                try:
                    fu.result()
                except Exception:
                    pass
        json.dump(cache, open(cache_path, "w", encoding="utf-8"), ensure_ascii=False)
        missing = [it["fa"] for it in KEYS if str(it["i"]) not in cache]

    entries = {}
    for it in KEYS:
        v = cache.get(str(it["i"]))
        if v:
            entries[it["fa"]] = v
    pack = {"format": "hr-lang-pack", "version": 1, "code": code,
            "title": lang["title"], "dir": lang["dir"], "entries": entries}
    outp = f"{BASE}/out/hr-lang-{code}.json"
    json.dump(pack, open(outp, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    secs = time.time() - t0
    return {"code": code, "total": len(KEYS), "translated": len(entries),
            "missing": len(missing), "secs": round(secs)}


if __name__ == "__main__":
    only = None
    if "--only" in sys.argv:
        only = sys.argv[sys.argv.index("--only") + 1].split(",")
    langs = [l for l in LANGS if not only or l["code"] in only]
    for lang in langs:
        res = run_lang(lang)
        print(f"[{res['code']}] translated={res['translated']}/{res['total']} "
              f"missing={res['missing']} in {res['secs']}s", flush=True)
