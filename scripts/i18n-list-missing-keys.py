#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""استخراج همهٔ کلیدهای HrT.T / HrT.F از سورس و مقایسه با DictEn موجود"""
import glob, re, json

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")

rx = re.compile(r'HrT\.[TF]\(\s*"((?:[^"\\]|\\.)*)"')
used = {}
for p in glob.glob(ROOT + "/**/*.cshtml", recursive=True) + glob.glob(ROOT + "/**/*.cs", recursive=True):
    pp = p.replace("\\", "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    for m in rx.finditer(src):
        k = m.group(1).replace('\\"', '"')
        used.setdefault(k, []).append(pp.split("/aspnet/src/")[1])

# دیکشنری موجود
hl = open(ROOT + "/HrPlatform.Core/Services/HrLang.cs", encoding="utf-8").read()
m = re.search(r"DictEn\s*=\s*new\(StringComparer\.Ordinal\)\s*\{(.*?)\n    \};", hl, re.S)
pat = re.compile(r'^\s*\["((?:[^"\\]|\\.)*)"\]\s*=\s*"((?:[^"\\]|\\.)*)"\s*,\s*$', re.M)
existing = set()
for mm in pat.finditer(m.group(1)):
    existing.add(mm.group(1).replace('\\"', '"'))

missing = sorted(k for k in used if k not in existing)
print(f"used={len(used)} existing={len(existing)} MISSING={len(missing)}")
json.dump(missing, open("/tmp/i18n_missing.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
for k in missing:
    print(repr(k))
