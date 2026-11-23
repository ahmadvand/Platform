# -*- coding: utf-8 -*-
# فاز ۱۰۲ب — پر کردن کلیدهای جاافتادهٔ بستهٔ روسی با بسته‌های کوچک (۱۰تایی)
# همان قالب i101؛ فقط کلیدهای غایب در cache2/ru.json ترجمه می‌شوند.
import json, importlib.util, time

spec = importlib.util.spec_from_file_location("m", "/home/z/my-project/scripts/i101-langpack-ru.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

CACHE = "/home/z/my-project/scripts/langpacks/cache2/ru.json"

dicten = m.extract_dicten()
cache = json.load(open(CACHE, encoding="utf-8"))
missing = [k for k in dicten if k not in cache]
print(f"missing: {len(missing)}", flush=True)

for size in (10, 5, 2, 1):
    todo = [k for k in dicten if k not in cache]
    if not todo:
        break
    chunks = [todo[i:i + size] for i in range(0, len(todo), size)]
    print(f"pass size={size}: {len(chunks)} chunks", flush=True)
    for ch in chunks:
        for attempt in range(8):
            try:
                items = [{"i": i, "fa": k, "en": dicten.get(k, "")} for i, k in enumerate(ch)]
                content = m.call_llm(json.dumps(items, ensure_ascii=False), m.LANG["name"])
                got = m.parse_lines(content, ch)
                if got:
                    cache.update(got)
                    m.save_cache(cache)
                    break
                raise ValueError("no lines parsed")
            except Exception as e:
                print(f"  chunk retry {attempt + 1}: {e}", flush=True)
                time.sleep(5 * (attempt + 1))
        still = [k for k in ch if k not in cache]
        if still:
            print(f"  chunk still missing {len(still)}: {[k[:30] for k in still][:3]}", flush=True)

cache = json.load(open(CACHE, encoding="utf-8"))
final_missing = [k for k in dicten if k not in cache]
print(f"final missing: {len(final_missing)}", flush=True)
for k in final_missing[:20]:
    print("  STILL:", repr(k[:80]), flush=True)
print("i102b COMPLETE", flush=True)
