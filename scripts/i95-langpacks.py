# -*- coding: utf-8 -*-
# ======================================================================
# فاز ۹۵ — تکمیل بسته‌های زبان پویا (درخواست کاربر: «زبان عربی هم اضافه کن» +
# «بعضی منوها هنوز فارسی می‌مانند» → همهٔ زبان‌ها پوشش کامل بگیرند).
# ----------------------------------------------------------------------
# ورودی کلیدها: استخراج زندهٔ DictEn از HrLang.cs (کلید = متن فارسی دقیق).
# مرجع معنا: ترجمهٔ انگلیسی همان کلید از DictEn.
# کش: cache2/<code>.json (کلید←ترجمه) — اجرای مجدد از همان‌جا ادامه می‌دهد.
# خروجی: aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-<code>.json
#        (همان قالب درون‌ریزی پنل /Langs؛ DbInit در هر بوت ادغام می‌کند).
# مصرف:  python3 scripts/i95-langpacks.py                # همهٔ زبان‌ها
#        python3 scripts/i95-langpacks.py --only ar      # فقط عربی
#        python3 scripts/i95-langpacks.py --status       # فقط گزارش
# ======================================================================
import json, os, re, subprocess, sys, tempfile, time
from concurrent.futures import ThreadPoolExecutor, as_completed

ROOT = "/home/z/my-project"
CS = f"{ROOT}/aspnet/src/Platform.Core/Services/HrLang.cs"
OUTDIR = f"{ROOT}/aspnet/src/Platform.Web/App_Data/langpacks"
CACHE = f"{ROOT}/scripts/langpacks/cache2"
BATCH = 60
WORKERS = 1  # فاز ۹۵ — CLI زد-ای همزمانی را تحمل نمی‌کند (فراخوانی موازی = فایل خالی)؛ سریالی پایدار است
CALL_TIMEOUT = 300
RETRIES = 5
BACKOFF = [3, 8, 15, 25, 40]

LANGS = [
    {"code": "ar",  "title": "العربية",   "dir": "rtl", "name": "Arabic"},
    {"code": "ky",  "title": "Кыргызча",  "dir": "ltr", "name": "Kyrgyz"},
    {"code": "ps",  "title": "پښتو",      "dir": "rtl", "name": "Pashto"},
    {"code": "ka",  "title": "ქართული",   "dir": "ltr", "name": "Georgian"},
    {"code": "tk",  "title": "Türkmençe", "dir": "ltr", "name": "Turkmen"},
    {"code": "tg",  "title": "Тоҷикӣ",    "dir": "ltr", "name": "Tajik"},
    {"code": "prs", "title": "دری",        "dir": "rtl", "name": "Dari (Afghan Persian)"},
    {"code": "uz",  "title": "Oʻzbekcha", "dir": "ltr", "name": "Uzbek"},
]

SYSTEM = (
    "You are a professional software localizer for an enterprise HR / attendance / payroll "
    "web application used by Iranian companies. Translate Persian (Farsi) UI strings into {name}. "
    "The user message is a JSON array of items: i = index, fa = Persian source string, "
    "en = existing English translation (meaning reference — when fa is ambiguous, follow it). Rules:\n"
    "1) Natural, concise, professional UI wording in {name}.\n"
    "2) Keep placeholders like {0} {1} {2}, emojis, and <b> </b> tags EXACTLY intact.\n"
    "3) Do NOT translate to Persian — output must be pure {name}. Keep proper nouns reasonable.\n"
    "4) Domain terms: حضور و غیاب=attendance, حقوق و دستمزد=payroll, قلم=pay item, "
    "مأموریت=mission, مرخصی=leave, تردد=traffic/transaction, مجوز=permit, "
    "شیفت=shift, گیت=gate, واحد سازمانی=organization unit, پست سازمانی=position.\n"
    "5) Output EXACTLY one line per item in the form: i|||translation\n"
    "No explanations, no extra lines, same count and order. Never wrap the translation in quotes."
)

LINE_RE = re.compile(r'^\s*(\d+)\s*\|\|\|\s*(.*)$', re.M)


def extract_dicten():
    """کلیدهای DictEn + ترجمهٔ انگلیسی آن‌ها — مستقیم از سورس C#."""
    src = open(CS, encoding="utf-8").read()
    m = re.search(r'DictEn\s*=\s*new[^(]*\((.*?)\n    \};', src, re.S)
    body = m.group(1) if m else src
    pairs = {}
    for mm in re.finditer(r'\[\s*"((?:[^"\\]|\\.)*)"\s*\]\s*=\s*"((?:[^"\\]|\\.)*)"', body):
        k = mm.group(1).encode().decode("unicode_escape").encode("latin-1", "ignore").decode("utf-8", "ignore")
        v = mm.group(2)
        pairs[k] = v
    # unicode_escape برای متن فارسی خراب است — روش ساده و درست: JSON decode هر کلید
    pairs = {}
    for mm in re.finditer(r'\[\s*("(?:[^"\\]|\\.)*")\s*\]\s*=\s*("(?:[^"\\]|\\.)*")', body):
        try:
            k = json.loads(mm.group(1)); v = json.loads(mm.group(2))
        except Exception:
            continue
        pairs[k] = v
    return pairs


def call_llm(payload: str, lang_name: str) -> str:
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as tf:
        outp = tf.name
    try:
        last = None
        for attempt in range(1, RETRIES + 1):
            try:
                r = subprocess.run(
                    ["z-ai", "chat", "-s", SYSTEM.replace("{name}", lang_name), "-p", payload, "-o", outp],
                    capture_output=True, timeout=CALL_TIMEOUT)
                data = json.load(open(outp, encoding="utf-8"))
                content = (data.get("choices") or [{}])[0].get("message", {}).get("content", "")
                if content and content.strip():
                    return content
                raise ValueError("empty content")
            except Exception as e:
                last = e
                if attempt == RETRIES:
                    raise RuntimeError(f"z-ai failed after {RETRIES} tries: {last}")
                time.sleep(BACKOFF[min(attempt - 1, len(BACKOFF) - 1)])
        raise RuntimeError("unreachable")
    finally:
        try: os.unlink(outp)
        except OSError: pass


def parse_lines(content, keys):
    got = {}
    for m in LINE_RE.finditer(content):
        i = int(m.group(1))
        tr = m.group(2).strip()
        if tr:
            got[i] = tr
    out = {}
    for idx, k in enumerate(keys):
        if idx in got:
            out[k] = got[idx]
    return out


def save_cache(code, cache):
    os.makedirs(CACHE, exist_ok=True)
    tmp = f"{CACHE}/{code}.json.tmp"
    json.dump(cache, open(tmp, "w", encoding="utf-8"), ensure_ascii=False)
    os.replace(tmp, f"{CACHE}/{code}.json")


def load_existing_pack(code):
    """بستهٔ فعلی خروجی (اگر هست) به‌عنوان کش اولیه."""
    p = f"{OUTDIR}/hr-lang-{code}.json"
    if os.path.exists(p):
        try:
            return json.load(open(p, encoding="utf-8")).get("entries", {})
        except Exception:
            return {}
    return {}


def translate_lang(lang, dicten, only_missing_report=False):
    code, name = lang["code"], lang["name"]
    cache_path = f"{CACHE}/{code}.json"
    cache = {}
    if os.path.exists(cache_path):
        try: cache = json.load(open(cache_path, encoding="utf-8"))
        except Exception: cache = {}
    # ادغام بستهٔ فعلی (تولید قبلی) به‌عنوان دانش اولیه
    for k, v in load_existing_pack(code).items():
        cache.setdefault(k, v)
    todo = [k for k in dicten if k not in cache]
    if only_missing_report:
        return code, len(todo), 0
    if not todo:
        return code, 0, len(cache)

    batches = [todo[i:i + BATCH] for i in range(0, len(todo), BATCH)]
    print(f"[{code}] {len(todo)} keys to translate in {len(batches)} batches", flush=True)
    done = 0

    def work(batch):
        items = [{"i": i, "fa": k, "en": dicten.get(k, "")} for i, k in enumerate(batch)]
        payload = json.dumps(items, ensure_ascii=False)
        content = call_llm(payload, name)
        return parse_lines(content, batch)

    with ThreadPoolExecutor(max_workers=WORKERS) as ex:
        futs = {ex.submit(work, b): b for b in batches}
        for fut in as_completed(futs):
            b = futs[fut]
            try:
                got = fut.result()
            except Exception as e:
                print(f"[{code}] batch failed: {e}", flush=True)
                continue
            cache.update(got)
            done += 1
            if done % 5 == 0 or done == len(batches):
                save_cache(code, cache)
                print(f"[{code}] {done}/{len(batches)} batches, cache={len(cache)}", flush=True)
    save_cache(code, cache)

    # ساخت بستهٔ نهایی — کلیدهای بدون ترجمه حذف می‌شوند (fallback فارسی مثل قبل)
    entries = {k: cache[k] for k in dicten if cache.get(k)}
    os.makedirs(OUTDIR, exist_ok=True)
    pack = {"code": code, "title": lang["title"], "dir": lang["dir"], "entries": entries}
    tmp = f"{OUTDIR}/hr-lang-{code}.json.tmp"
    json.dump(pack, open(tmp, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    os.replace(tmp, f"{OUTDIR}/hr-lang-{code}.json")
    return code, len(todo), len(entries)


def main():
    only = None
    status = False
    args = sys.argv[1:]
    if "--only" in args:
        only = args[args.index("--only") + 1].split(",")
    if "--status" in args:
        status = True
    dicten = extract_dicten()
    print(f"DictEn keys: {len(dicten)}", flush=True)
    langs = [l for l in LANGS if not only or l["code"] in only]
    for lang in langs:
        t0 = time.time()
        code, todo, total = translate_lang(lang, dicten, status)
        dt = int(time.time() - t0)
        print(f"[{code}] {'MISSING' if status else 'DONE'}: todo={todo} entries={total} ({dt}s)", flush=True)


if __name__ == "__main__":
    main()
