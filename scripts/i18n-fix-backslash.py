#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ترمیم بک‌اسلش‌های نشسته از re.sub: @HrT.T(\"TEXT\") → @HrT.T("TEXT")"""
import glob, re, os

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")
bad = re.compile(r'@HrT\.T\(\\\\"([^"\\\\]*)\\\\"\)')
fixed = 0
files = 0
for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    if '@HrT.T(\\"' not in src:
        continue
    new, n = bad.subn(r'@HrT.T("\1")', src)
    if n:
        open(p, "w", encoding="utf-8").write(new)
        fixed += n
        files += 1
print(f"files={files} repaired={fixed}")
