# -*- coding: utf-8 -*-
# فاز ۱۰۴ن — راستی‌آزمایی نهایی: همهٔ کلیدهای T("...") سورسِ فعال vs بستهٔ روسی
import json, re, glob, os

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/App_Data/langpacks/", "/obj/", "/bin/")

# کلیدسازها: HrT.T("...") / HrT.F("...", ...) / T("...") / L("...")
pat = re.compile(r'(?:HrT\.)?[TF]\(\s*"((?:[^"\\]|\\.)*)"')
keys = set()
files = 0
for ext in ("*.cshtml", "*.cs"):
    for p in glob.glob(f"{ROOT}/**/{ext}", recursive=True):
        if any(x in p for x in EXCLUDE):
            continue
        files += 1
        try:
            src = open(p, encoding="utf-8").read()
        except Exception:
            continue
        for m in pat.finditer(src):
            k = m.group(1)
            # فقط کلیدهای حاوی نویسهٔ غیرلاتین (متن فارسی) — کلیدهای انگلیسی/متنی هم بگذار
            if re.search(r"[\u0600-\u06FF]", k) or len(k) > 3:
                keys.add(k)
print(f"scanned {files} files, extracted {len(keys)} candidate keys")

# کلیدهای DB (نظیر جاافتاده‌های سیدِشده)
import sqlite3
con = sqlite3.connect("file:/home/z/my-project/aspnet/sc-run/hr.db?mode=ro", uri=True)
dbk = {r[0] for r in con.execute("SELECT DISTINCT Key FROM HrLangEntries")}
print("db keys:", len(dbk))

universe = keys | dbk
ru = json.load(open("/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-ru.json", encoding="utf-8"))["entries"]
BRANDS = {"ZKTeco","Suprema","Anviz","Virdi","Timmy","ZK","SP","AV","VR","TM","GEN","GEN1"}

miss = sorted(k for k in universe if k not in ru)
real = [k for k in miss if k not in BRANDS]
print(f"universe={len(universe)} ru={len(ru)} missing={len(miss)} (brands={len(miss)-len(real)})")
print("== REAL missing ==")
for k in real:
    print("  ", repr(k[:120]))
json.dump(real, open("/tmp/ru_real_missing.json", "w"), ensure_ascii=False)
