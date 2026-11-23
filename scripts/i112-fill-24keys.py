# -*- coding: utf-8 -*-
# فاز ۱۰۴ب — ترجمهٔ ۲۴ کلید جدید (انتخابگر/ممیزی/ضد دستکاری) به ۹ زبان
import json, importlib.util, time

spec = importlib.util.spec_from_file_location("m", "/home/z/my-project/scripts/i101-langpack-ru.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

CACHE = "/home/z/my-project/scripts/langpacks/cache2"
PACKS = "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks"
AUD = json.load(open("/home/z/my-project/scripts/i110-audit.json", encoding="utf-8"))
BRANDS = {"ZKTeco", "Suprema", "Anviz", "Virdi", "Timmy", "ZK", "SP", "AV", "VR", "TM", "GEN", "GEN1"}
KEYS = [k for k in AUD["union_missing"] if k not in BRANDS]
DICTEN = m.extract_dicten()
print("keys:", len(KEYS), flush=True)

LANGS = [
    {"code": "ar",  "name": "Arabic"},
    {"code": "ka",  "name": "Georgian"},
    {"code": "ky",  "name": "Kyrgyz"},
    {"code": "prs", "name": "Dari (Afghan Persian)"},
    {"code": "ps",  "name": "Pashto"},
    {"code": "tg",  "name": "Tajik"},
    {"code": "tk",  "name": "Turkmen"},
    {"code": "uz",  "name": "Uzbek"},
    {"code": "ru",  "name": "Russian"},
]

for lang in LANGS:
    code = lang["code"]
    cache_path = f"{CACHE}/{code}.json"
    cache = json.load(open(cache_path, encoding="utf-8"))
    todo = [k for k in KEYS if k not in cache]
    # تک‌به‌تک — مقاوم‌ترین حالت (بسته‌های بزرگ گاهی خط می‌انداختند)
    fail = []
    for k in todo:
        ok = False
        for attempt in range(8):
            try:
                c = m.call_llm(json.dumps([{"i": 0, "fa": k, "en": DICTEN.get(k, "")}], ensure_ascii=False), lang["name"])
                got = m.parse_lines(c, [k])
                if got:
                    cache.update(got)
                    json.dump(cache, open(cache_path, "w", encoding="utf-8"), ensure_ascii=False)
                    ok = True
                    break
            except Exception:
                time.sleep(3 * (attempt + 1))
        if not ok:
            fail.append(k)
    # ادغام در فایل بسته
    ppath = f"{PACKS}/hr-lang-{code}.json"
    pack = json.load(open(ppath, encoding="utf-8"))
    add = {k: cache[k] for k in KEYS if k in cache and k not in pack["entries"]}
    pack["entries"].update(add)
    json.dump(pack, open(ppath, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    print(f"[{code}] +{len(add)} pack={len(pack['entries'])} fail={len(fail)}", flush=True)
    for k in fail:
        print("   FAIL:", k[:60], flush=True)
print("i104b COMPLETE", flush=True)
