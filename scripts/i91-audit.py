# -*- coding: utf-8 -*-
# ======================================================================
# فاز ۹۱ — ممیزی کامل چندزبانی (درخواست کاربر): «چند زبانه بودن رو توی
# همه فرم ها و صفحات به صورت کامل بررسی کن»
#   ۱) کلیدهای استفاده‌شده = رشته‌های فارسی در نقاط ترجمه‌شونده:
#      HrT.T / HrT.F / T( در ویوها و کلاس‌ها + ToastOk/ToastErr/AjaxResult/
#      ToastDone که به‌صورت مرکزی ترجمه می‌شوند (HrModuleControllerBase).
#   ۲) پوشش = DictEn (کد) ∪ ردیف‌های en در DB
#   ۳) خروجی: keys-all.json ، missing-en.json ، unwrapped-cshtml.json
# ======================================================================
import os, re, json, sqlite3, subprocess

ROOT = "/home/z/my-project/aspnet/src"
FA = re.compile(r'[\u0600-\u06FF\u200c]')

# ---------- ۱) استخراج کلیدها ----------
KEYCTX = re.compile(r'(?:HrT\.[TF]\s*\(\s*|[^A-Za-z_.]T\s*\(\s*|ToastOk\s*\(\s*|ToastErr\s*\(\s*|AjaxResult\s*\(\s*(?:ok|false|true|j\.ok|res\.ok)?[^,]*,\s*|ToastDone\s*\(\s*[^,]+,\s*)$')

def is_translated_ctx(line, idx):
    before = line[:idx]
    if KEYCTX.search(before.rstrip() + ' '):
        return True
    if KEYCTX.search(before):
        return True
    return False

STR = re.compile(r'"((?:[^"\\\n]|\\.)*)"')
keys = {}
unwrapped = []

for dirpath, dirs, files in os.walk(ROOT):
    if any(x in dirpath for x in ("/obj/", "/bin/", "/node_modules/", "/wwwroot/lib/")):
        continue
    for fn in files:
        if not (fn.endswith(".cshtml") or fn.endswith(".cs")):
            continue
        path = os.path.join(dirpath, fn)
        try:
            src = open(path, encoding="utf-8").read()
        except Exception:
            continue
        rel = path.replace(ROOT + "/", "")
        lines = src.split("\n")
        prev_tail = ""
        for i, line in enumerate(lines):
            s = line.strip()
            if s.startswith("//") or s.startswith("@*") or s.startswith("<!--"):
                prev_tail = ""
                continue
            # ادامهٔ فراخوانی چندخطی: خط قبلی هم در کانتکست ترجمه باشد
            ctx_line = is_translated_ctx(line, 10**9) or bool(prev_tail)
            for m in STR.finditer(line):
                t = m.group(1)
                if not FA.search(t):
                    continue
                if "\\u" in t and not FA.search(t.encode().decode('unicode_escape', errors='ignore')):
                    pass
                # کلید ترجمه‌شده؟
                if ctx_line or is_translated_ctx(line, m.start()):
                    key = t.replace('\\"', '"').replace("\\\\", "\\")
                    keys.setdefault(key, set()).add(rel)
                    prev_tail = "ctx"
                else:
                    unwrapped.append({"file": rel, "line": i + 1, "text": t[:160]})
            # prev_tail فقط وقتی خط «ترجمه‌شده» بود ولی رشته در خط بعد می‌آید
            if KEYCTX.search(line.rstrip()) and not STR.search(line):
                prev_tail = "ctx"
            else:
                prev_tail = ""

# ---------- ۲) پوشش en ----------
dicten = set()
try:
    src = open(os.path.join(ROOT, "Platform.Core/Services/HrLang.cs"), encoding="utf-8").read()
    dicten = set(re.findall(r'\[\s*"((?:[^"\\]|\\.)*)"\s*\]\s*=', src))
except Exception as e:
    print("DictEn read fail:", e)

dbp = "/home/z/my-project/aspnet/src/Platform.Web/hr.db"
if not (os.path.exists(dbp) and os.path.getsize(dbp) > 0):
    dbp = "/home/z/my-project/aspnet/sc-run/hr.db"
db = sqlite3.connect(f"file:{dbp}?mode=ro", uri=True)
db_keys = {r[0] for r in db.execute("select distinct Key from HrLangEntries where LangCode='en'")}
covered = dicten | db_keys
missing = sorted(k for k in keys if k not in covered)

out = "/home/z/my-project/scripts"
json.dump(sorted(keys), open(out + "/i91-keys-all.json", "w"), ensure_ascii=False, indent=1)
json.dump(missing, open(out + "/i91-missing-en.json", "w"), ensure_ascii=False, indent=1)
json.dump(unwrapped, open(out + "/i91-unwrapped.json", "w"), ensure_ascii=False, indent=1)

print("keys used in translated contexts:", len(keys))
print("DictEn:", len(dicten), "| DB en:", len(db_keys))
print("MISSING en:", len(missing))
for k in missing[:60]:
    print("   -", k[:100])
print("unwrapped candidates:", len(unwrapped))
