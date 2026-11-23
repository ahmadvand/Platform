# -*- coding: utf-8 -*-
# فاز ۱۰۴ — تکمیل ۲۶ کلید جاافتاده (دراپ‌داون‌ها) به ۹ زبان
# ترجمه با z-ai → ادغام در cache2/<code>.json و فایل بستهٔ هر زبان (بدون از دست رفتن کلید موجود)
import json, importlib.util, time

spec = importlib.util.spec_from_file_location("m", "/home/z/my-project/scripts/i101-langpack-ru.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

CACHE = "/home/z/my-project/scripts/langpacks/cache2"
PACKS = "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks"
AUD = json.load(open("/home/z/my-project/scripts/i110-audit.json", encoding="utf-8"))

BRANDS = {"ZKTeco", "Suprema", "Anviz", "Virdi", "Timmy", "ZK", "SP", "AV", "VR", "TM", "GEN", "GEN1"}
KEYS = [k for k in AUD["union_missing"] if k not in BRANDS]
assert len(KEYS) == 26, len(KEYS)

DICTEN = m.extract_dicten()
LANGS = [
    {"code": "ar",  "title": "العربية",   "dir": "rtl", "name": "Arabic"},
    {"code": "ka",  "title": "ქართული",   "dir": "ltr", "name": "Georgian"},
    {"code": "ky",  "title": "Кыргызча",  "dir": "ltr", "name": "Kyrgyz"},
    {"code": "prs", "title": "دری",        "dir": "rtl", "name": "Dari (Afghan Persian)"},
    {"code": "ps",  "title": "پښتو",      "dir": "rtl", "name": "Pashto"},
    {"code": "tg",  "title": "Тоҷикӣ",    "dir": "ltr", "name": "Tajik"},
    {"code": "tk",  "title": "Türkmençe", "dir": "ltr", "name": "Turkmen"},
    {"code": "uz",  "title": "Oʻzbekcha", "dir": "ltr", "name": "Uzbek"},
    {"code": "ru",  "title": "Русский",   "dir": "ltr", "name": "Russian"},
]

items = [{"i": i, "fa": k, "en": DICTEN.get(k, "")} for i, k in enumerate(KEYS)]
payload = json.dumps(items, ensure_ascii=False)

for lang in LANGS:
    code = lang["code"]
    cache_path = f"{CACHE}/{code}.json"
    cache = json.load(open(cache_path, encoding="utf-8"))
    todo = [k for k in KEYS if k not in cache]
    if not todo:
        print(f"[{code}] nothing to do", flush=True)
        continue
    got_all = {}
    for attempt in range(10):
        try:
            sub = [{"i": KEYS.index(k), "fa": k, "en": DICTEN.get(k, "")} for k in todo]
            content = m.call_llm(json.dumps(sub, ensure_ascii=False), lang["name"])
            got = m.parse_lines(content, todo)
            if len(got) >= len(todo) - 2:   # تقریباً کامل
                got_all.update(got)
                break
            got_all.update(got)
            todo = [k for k in todo if k not in got_all]
            print(f"[{code}] partial {len(got_all)}/{len(KEYS)} — retry", flush=True)
        except Exception as e:
            print(f"[{code}] retry {attempt + 1}: {e}", flush=True)
            time.sleep(6 * (attempt + 1))
    missing = [k for k in KEYS if k not in got_all]
    if missing:
        print(f"[{code}] STILL MISSING: {missing}", flush=True)
        continue
    cache.update(got_all)
    m2 = json.dump(cache, open(cache_path, "w", encoding="utf-8"), ensure_ascii=False)  # noqa
    json.dump(cache, open(cache_path, "w", encoding="utf-8"), ensure_ascii=False)
    # ادغام در فایل بستهٔ موجود
    ppath = f"{PACKS}/hr-lang-{code}.json"
    pack = json.load(open(ppath, encoding="utf-8"))
    pack["entries"].update(got_all)
    json.dump(pack, open(ppath, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    print(f"[{code}] pack now {len(pack['entries'])} entries", flush=True)
print("i104 COMPLETE", flush=True)
