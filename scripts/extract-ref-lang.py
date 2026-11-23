#!/usr/bin/env python3
# ======================================================================
# استخراج دیکشنری مرجع en از HrLang.cs → resources/ref-en.json
# خروجی: {"fa key": "en value", ...} — مرتب‌سازی ordinal، انکودینگ UTF-8
# مصرف:  python3 scripts/extract-ref-lang.py   (پس از هر تغییر DictEn در سرور)
# ======================================================================
import json, re, os

SRC = "/home/z/my-project/aspnet/src/HrPlatform.Core/Services/HrLang.cs"
DST = "/home/z/my-project/tools/TranslationStudio/Resources/ref-en.json"

text = open(SRC, encoding="utf-8").read()
m = re.search(r"DictEn\s*=\s*new\(StringComparer\.Ordinal\)\s*\{(.*?)\n    \};", text, re.S)
if not m:
    raise SystemExit("DictEn block not found")
body = m.group(1)

# خطوط به شکل ["کلید فارسی"] = "مقدار انگلیسی",
pat = re.compile(r'^\s*\["((?:[^"\\]|\\.)*)"\]\s*=\s*"((?:[^"\\]|\\.)*)"\s*,\s*$', re.M)
entries = {}
for mm in pat.finditer(body):
    k, v = mm.group(1), mm.group(2)
    k = k.replace('\\"', '"').replace("\\\\", "\\")
    v = v.replace('\\"', '"').replace("\\\\", "\\")
    if k in entries and entries[k] != v:
        print(f"WARN duplicate key with different value: {k}")
    entries[k] = v

if not entries:
    raise SystemExit("no entries parsed — pattern mismatch")

os.makedirs(os.path.dirname(DST), exist_ok=True)
ordered = {k: entries[k] for k in sorted(entries.keys())}
with open(DST, "w", encoding="utf-8") as f:
    json.dump(ordered, f, ensure_ascii=False, indent=1, sort_keys=False)
    f.write("\n")
print(f"OK — {len(ordered)} entries -> {DST}")
