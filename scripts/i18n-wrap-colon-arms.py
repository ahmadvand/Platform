#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""wrap بازوی : (ternary false-arm) و الحاق‌های + "متن فارسی" با HrT.T"""
import glob, re, os

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")
FA = "[\u0600-\u06FF]"

rx_colon = re.compile(rf'(?<!["\w]):(\s*)"([^"]*{FA}[^"]*)"(?!:)')
rx_concat = re.compile(rf'(\+\s*)"((?:\s)?{FA}[^"]*)"')

n_c = n_k = 0
files = 0
for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    before = src
    def col(m):
        global n_c; n_c += 1
        return f': {m.group(1)}HrT.T("{m.group(2)}")'
    def con(m):
        global n_k; n_k += 1
        return f'{m.group(1)}HrT.T("{m.group(2)}")'
    src = rx_colon.sub(col, src)
    src = rx_concat.sub(con, src)
    if src != before:
        open(p, "w", encoding="utf-8").write(src)
        files += 1
print(f"files={files} colon_arms={n_c} concats={n_k}")
