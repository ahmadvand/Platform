#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""unify ارقام JS: fa-IR ثابت → آگاه از HrDateCfg.digits"""
import glob, re, os

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")

pat_def = re.compile(r"function\s+faNum\s*\(\s*\w+\s*\)\s*\{\s*return\s+Number\(([^)]*)\)\.toLocaleString\('fa-IR'\);\s*\}")
pat_inline = re.compile(r"\.toLocaleString\('fa-IR'\)")

n1 = n2 = 0
for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    before = src
    src, a = pat_def.subn(lambda m: f"var faNum = window.HrNum; // ارقام متناسب با زبان (HrDateCfg)", src)
    src, b = pat_inline.subn(".toLocaleString(HrNumLocale())", src)
    n1 += a; n2 += b
    if src != before:
        open(p, "w", encoding="utf-8").write(src)
print(f"faNum_defs={n1} inline_faIR={n2}")
