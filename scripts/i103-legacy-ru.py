# -*- coding: utf-8 -*-
# فاز ۱۰۳ — کلیدهای میراث (خارج از DictEn فعلی) که بسته‌های هشت‌گانهٔ قبلی
# دارند؛ برای هم‌سطح شدن روسی با آن‌ها ترجمه و به بسته اضافه می‌شوند.
import json, importlib.util, time

spec = importlib.util.spec_from_file_location("m", "/home/z/my-project/scripts/i101-langpack-ru.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

CACHE = "/home/z/my-project/scripts/langpacks/cache2/ru.json"
PACKS = "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks"

dicten = m.extract_dicten()
legacy = {}
db_legacy = json.load(open("/home/z/my-project/scripts/langpacks/legacy-keys.json", encoding="utf-8"))
# مرجع معنا برای کلیدهای میراث: ترجمهٔ هر زبان موجود در DB — از خود DB می‌خوانیم
import sqlite3
db = sqlite3.connect("/home/z/my-project/aspnet/sc-run/hr.db")
refs = {}
for k, v in db.execute("SELECT Key, Value FROM HrLangEntries WHERE LangCode IN ('uz','prs','en') AND Value != ''"):
    refs.setdefault(k, v)
for k in db_legacy:
    legacy[k] = refs.get(k, "")

cache = json.load(open(CACHE, encoding="utf-8"))
todo = [k for k in legacy if k not in cache]
print(f"legacy keys: {len(legacy)}  todo: {len(todo)}", flush=True)

chunks = [todo[i:i + 30] for i in range(0, len(todo), 30)]
for ch in chunks:
    for attempt in range(8):
        try:
            # مرجع معنا: ترجمهٔ انگلیسی DictEn اگر هست، وگرنه ترجمهٔ ازبکی/عربی بستهٔ موجود
            items = [{"i": i, "fa": k, "en": dicten.get(k) or legacy.get(k, "")}
                     for i, k in enumerate(ch)]
            content = m.call_llm(json.dumps(items, ensure_ascii=False), m.LANG["name"])
            got = m.parse_lines(content, ch)
            if got:
                cache.update(got)
                m.save_cache(cache)
                break
            raise ValueError("no lines parsed")
        except Exception as e:
            print(f"  retry {attempt + 1}: {e}", flush=True)
            time.sleep(5 * (attempt + 1))

cache = json.load(open(CACHE, encoding="utf-8"))
# بازسازی بسته: کلیدهای DictEn + کلیدهای میراث
entries = {k: cache[k] for k in dicten if cache.get(k)}
for k in legacy:
    if cache.get(k):
        entries[k] = cache[k]
pack = {"format": "hr-lang-pack", "version": 1, "code": "ru",
        "title": "Русский", "dir": "ltr", "entries": entries}
json.dump(pack, open("/home/z/my-project/scripts/langpacks/out/hr-lang-ru.json", "w",
                     encoding="utf-8"), ensure_ascii=False, indent=0)
missing = [k for k in legacy if k not in cache]
print(f"pack entries: {len(entries)}  legacy missing: {len(missing)}", flush=True)
for k in missing[:10]:
    print("  STILL:", repr(k[:70]), flush=True)
print("i103 COMPLETE", flush=True)
