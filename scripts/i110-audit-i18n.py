# -*- coding: utf-8 -*-
# ممیزی کامل i18n — همهٔ کلیدهای T()/HrT.T()/HrT.F() در سورس + مقایسه با ۹ بستهٔ زبان DB
import re, glob, sqlite3, json

ROOT = "/home/z/my-project/aspnet/src"
DB = "/home/z/my-project/aspnet/sc-run/hr.db"
SKIP = ("/dl/", "/bin/", "/obj/")

# الگوهای فراخوانی: @T("...")، HrT.T("...")، HrT.F("...")، T("...") داخل cshtml/c#
CALL = re.compile(r'(?:@(?<!@@)|HrT\.)\bT\(\s*"((?:[^"\\]|\\.)*)"')
CALLF = re.compile(r'HrT\.F\(\s*"((?:[^"\\]|\\.)*)"')
# کلیدهایی که از طریق متغیر C# پاس می‌شوند (مثل DeviceDefaults) — از کاتالوگ‌ها
CATS = {
    "DeviceDefaults": open(f"{ROOT}/Platform.Web/Services/DeviceDefaults.cs", encoding="utf-8").read(),
}

FA = re.compile(r'[\u0600-\u06FF]')

keys = {}
files = [f for f in glob.glob(f"{ROOT}/**/*.cshtml", recursive=True) if not any(s in f for s in SKIP)]
files += [f for f in glob.glob(f"{ROOT}/**/*.cs", recursive=True) if not any(s in f for s in SKIP)]
files += [f for f in glob.glob(f"{ROOT}/**/*.js", recursive=True) if not any(s in f for s in SKIP)]

for f in files:
    src = open(f, encoding="utf-8").read()
    for m in list(CALL.finditer(src)) + list(CALLF.finditer(src)):
        k = json.loads('"' + m.group(1) + '"')
        if FA.search(k) and len(k.strip()) > 0:
            keys[k] = f

# کلیدهای کاتالوگ DeviceDefaults (نوع/برند/پیشوند/راهنما)
dd = CATS["DeviceDefaults"]
for m in re.finditer(r'\["((?:[^"\\]|\\.)*)"\]\s*=\s*new\("((?:[^"\\]|\\.)*)"', dd):
    keys[json.loads('"' + m.group(1) + '"')] = "DeviceDefaults.cs(kind)"
    keys[json.loads('"' + m.group(2) + '"')] = "DeviceDefaults.cs(purpose)"
for m in re.finditer(r'"((?:[^"\\]|\\.)*)"', dd):
    v = json.loads('"' + m.group(1) + '"')
    if FA.search(v) and len(v.strip()) > 1:
        keys.setdefault(v, "DeviceDefaults.cs(other)")

print(f"T() keys found in source: {len(keys)}")

db = sqlite3.connect(DB)
codes = ['ar', 'ka', 'ky', 'prs', 'ps', 'tg', 'tk', 'uz', 'ru']
packs = {}
for c in codes:
    packs[c] = set(k for (k,) in db.execute("SELECT Key FROM HrLangEntries WHERE LangCode=?", (c,)))

all_missing = {}
for c in codes:
    miss = [k for k in keys if k not in packs[c]]
    all_missing[c] = miss
    print(f"[{c}] missing: {len(miss)}")

union_missing = sorted(set().union(*all_missing.values()))
print(f"\nUNION missing keys (need translation): {len(union_missing)}")
json.dump({"source_keys": sorted(keys.keys()), "union_missing": union_missing,
           "per_lang": all_missing},
          open("/home/z/my-project/scripts/i110-audit.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
# key -> where used (for review)
where = {k: keys[k].split("/src/")[-1] for k in union_missing if k in keys}
for k in union_missing[:40]:
    print(f"  - {k[:70]}  [{where.get(k, '?')[:60]}]")
