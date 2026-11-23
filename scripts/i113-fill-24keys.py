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
    # بسته‌های ۱۲تایی + بک‌آف بلند برای 429
    fail = []
    for i in range(0, len(todo), 12):
        ch = todo[i:i + 12]
        ok = False
        for attempt in range(12):
            try:
                sub = [{"i": j, "fa": k, "en": DICTEN.get(k, "")} for j, k in enumerate(ch)]
                c = m.call_llm(json.dumps(sub, ensure_ascii=False), lang["name"])
                got = m.parse_lines(c, ch)
                if got:
                    cache.update(got)
                    json.dump(cache, open(cache_path, "w", encoding="utf-8"), ensure_ascii=False)
                    ok = len(got) == len(ch)
                    ch = [k for k in ch if k not in got]
                    if not ch:
                        break
            except Exception:
                pass
            time.sleep(45 + 30 * attempt)   # پنجرهٔ 429 — صبر بلند
        if not ok and ch:
            fail.extend(ch)
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
