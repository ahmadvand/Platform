#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# فاز ۹۹ (بازسازی) — ترمیم جراحی بستهٔ روسی:
# باتچ‌های انتهای translate.py دچار شیفت خط شده بودند (value(i)=correct(i+14) از اندیس ۲۳۴۱).
# این اسکریپت فقط کلیدهای معیوب را با باتچ کوچک (۱۰تایی) + راستی‌آزمایی placeholder بازترجمه می‌کند،
# کش را به‌روز می‌کند و بسته را بازمی‌سازد. ابزار اصلی translate.py دست‌نخورده می‌ماند (فقط import).
import json, io, os, re, sys, time

BASE = "/home/z/my-project/scripts/langpacks"
sys.path.insert(0, BASE)
from translate import call_llm, LINE_RE, SYSTEM  # ابزار اصلی — بدون تغییر

RU_NAME = "Russian"
KEYS = json.load(open(f"{BASE}/keys.json", encoding="utf-8"))
CACHE_P = f"{BASE}/cache/ru.json"
cache = json.load(open(CACHE_P, encoding="utf-8"))

def ph(s):
    return sorted(re.findall(r'\{\d+\}', s or ''))

# --- ۱) کلیدهای معیوب (فقط برای گزارش) ---
ph_bad = [it["i"] for it in KEYS if cache.get(str(it["i"])) and ph(it["fa"]) != ph(cache[str(it["i"])])]
print("placeholder mismatches:", len(ph_bad), ph_bad[:8])

todo = [it for it in KEYS if 2941 <= it["i"] <= 3031]  # ناحیهٔ دمی — بدون استثنا (شیفت غیر-placeholder)
print("to re-translate:", len(todo))

# --- ۲) بازترجمهٔ باتچ کوچک با راستی‌آزمایی placeholder و retry ---
CHUNK = 10
fixed, failed = 0, []
for ci in range(0, len(todo), CHUNK):
    chunk = todo[ci:ci + CHUNK]
    payload = json.dumps([{"i": it["i"], "fa": it["fa"], "en": it["en"]} for it in chunk], ensure_ascii=False)
    ok = False
    for attempt in range(4):
        try:
            content = call_llm(payload, RU_NAME)
        except Exception as ex:
            print(f"  chunk@{chunk[0]['i']} call failed: {ex}")
            time.sleep(5)
            continue
        got = {m.group(1): m.group(2).strip() for m in LINE_RE.finditer(content) if m.group(2).strip()}
        # راستی‌آزمایی: هر آیتم درخواستی باید ترجمه با همان placeholderها داشته باشد
        good = True
        for it in chunk:
            v = got.get(str(it["i"]))
            if not v or ph(it["fa"]) != ph(v):
                good = False
                break
        if not good:
            print(f"  chunk@{chunk[0]['i']} misaligned/missing — retry {attempt+1}")
            time.sleep(3)
            continue
        for it in chunk:
            cache[str(it["i"])] = got[str(it["i"])]
        fixed += len(chunk)
        ok = True
        break
    if not ok:
        failed.append(chunk[0]["i"])
    # ذخیرهٔ دوره‌ای کش
    json.dump(cache, open(CACHE_P, "w", encoding="utf-8"), ensure_ascii=False)

print("fixed:", fixed, "| failed chunks at:", failed)

# --- ۳) بازسازی بسته از کش ---
entries = {}
for it in KEYS:
    v = cache.get(str(it["i"]))
    if v:
        entries[it["fa"]] = v
pack = {"format": "hr-lang-pack", "version": 1, "code": "ru",
        "title": "Русский", "dir": "ltr", "entries": entries}
outp = f"{BASE}/out/hr-lang-ru.json"
json.dump(pack, open(outp, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

# --- ۴) راستی‌آزمایی نهایی با همان آشکارسازها ---
ph_bad = [it["i"] for it in KEYS if it["fa"] in entries and ph(it["fa"]) != ph(entries[it["fa"]])]
print("pack entries:", len(entries), "| placeholder mismatches now:", len(ph_bad), ph_bad[:10])
