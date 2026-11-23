#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ترمیم صفاتِ بد-جایگزین‌شده: attr="'@HrT.T("TEXT")'" → attr='@HrT.T("TEXT")'"""
import glob, re, os

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")
bad = re.compile(r"([\w-]+)=\"'@HrT\.T\(\"([^\"]*)\"\)'")
fixed = 0
files = 0
for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    new, n = bad.subn(r"\1='@HrT.T(\"\2\")'", src)
    if n:
        open(p, "w", encoding="utf-8").write(new)
        fixed += n
        files += 1
print(f"files={files} attrs_repaired={fixed}")
# باقی‌ماندهٔ فرم بد؟
left = 0
for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    left += len(bad.findall(open(p, encoding="utf-8").read()))
print("remaining_bad:", left)
