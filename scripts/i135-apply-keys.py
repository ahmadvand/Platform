#!/usr/bin/env python3.13
# -*- coding: utf-8 -*-
# فاز ۱۱۳ — اعمال ترجمه‌های کلیدهای فاز ۱۱۲/۱۱۳ روی همهٔ بسته‌های زبان
# قواعد: فقط کلیدهای «غایب» پر می‌شوند — ترجمهٔ موجود هیچ‌گاه بازنویسی نمی‌شود؛
# مقدار خالی یا کلید نامعلوم نادیده گرفته می‌شود. سورس ترجمه‌ساز (TranslationStudio) دست‌نخورده.
import json, pathlib, re, importlib.util, shutil

ROOT = pathlib.Path("/home/z/my-project")
LP = ROOT / "aspnet/src/Platform.Web/App_Data/langpacks"
SCRUN = ROOT / "aspnet/sc-run/App_Data/langpacks"
CACHE = ROOT / "scripts/langpacks/cache2"

def load_tr(name):
    spec = importlib.util.spec_from_file_location(f"tr_{name}", ROOT / f"scripts/phase113-tr/lang-{name}.py")
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return {k: v for k, v in getattr(m, "TR").items() if v}   # مقدار خالی = نادیده

# دیکشنری i127 (فاز ۱۱۲) برای بازاستفادهٔ en/ru
i127 = (ROOT / "aspnet/src/Platform.Web/wwwroot/dl/source/scripts/i127-addkeys-phase112.py").read_text(encoding="utf-8")
m = re.search(r"NEW = \{(.*?)\n\}\n", i127, re.S)
D112 = eval("{" + m.group(1) + "}", {"re": re})   # کلید → {"en":…, "ru":…}

TOTAL_ADDED = {}
for p in sorted(LP.glob("hr-lang-*.json")):
    code = p.stem.replace("hr-lang-", "")
    if code == "fa":
        continue
    pack = json.loads(p.read_text(encoding="utf-8"))
    entries = pack.setdefault("entries", {})
    before = len(entries)
    # ۱) بازاستفاده از i127 برای کلیدهای غایب (en/ru در دسترس است)
    if code in ("en", "ru"):
        for k, tr in D112.items():
            v = tr.get(code)
            if v and k not in entries:
                entries[k] = v
    # ۲) ترجمه‌های دستی فاز ۱۱۳ + پرکردن گپ‌های هر زبان
    trs = load_tr(code)
    added = []
    for k, v in trs.items():
        if k not in entries:
            entries[k] = v
            added.append(k)
    after = len(entries)
    p.write_text(json.dumps(pack, ensure_ascii=False, indent=1), encoding="utf-8")
    dst = SCRUN / f"hr-lang-{code}.json"
    dst.write_text(json.dumps(pack, ensure_ascii=False, indent=1), encoding="utf-8")
    CACHE.mkdir(parents=True, exist_ok=True)
    (CACHE / f"{code}.json").write_text(json.dumps(pack, ensure_ascii=False, indent=1), encoding="utf-8")
    TOTAL_ADDED[code] = after - before
    print(f"{code}: {after - before} کلید افزوده شد ({before} → {after})")

print("\n=== گپ باقی‌مانده نسبت به کلیدهای فاز ۱۱۲+۱۱۳ ===")
i112keys = list(D112.keys())
K113 = []
for f in sorted((ROOT / "scripts/phase113-tr").glob("lang-*.py")):
    K113 = list(load_tr(f.stem.replace("lang-", "")).keys())
    break
ALL = list(dict.fromkeys(i112keys + K113))
for p in sorted(LP.glob("hr-lang-*.json")):
    code = p.stem.replace("hr-lang-", "")
    if code == "fa":
        continue
    e = json.loads(p.read_text(encoding="utf-8")).get("entries", {})
    miss = [k for k in ALL if k not in e]
    print(f"{code}: مجموع={len(e)} — غایب از مجموع ۱۱۲/۱۱۳: {len(miss)}")
