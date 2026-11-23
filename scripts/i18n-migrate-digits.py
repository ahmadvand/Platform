#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""مهاجرت ارقام ویوها به نسخهٔ آگاه-از-زبان: Jalali.Num/Jalali.ToPersianDigits → HrT.Num
(HrT.Num برای string/long/int/decimal بارگذاری دارد و در en ارقام لاتین می‌دهد)
"""
import glob, re, os

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")

changed_files = 0
changed_refs = 0
for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    new = re.sub(r"Jalali\.(?:Num|ToPersianDigits)\b", "HrT.Num", src)
    n = len(re.findall(r"Jalali\.(?:Num|ToPersianDigits)\b", src))
    if new != src:
        open(p, "w", encoding="utf-8").write(new)
        changed_files += 1
        changed_refs += n
print(f"files={changed_files} refs_migrated={changed_refs}")
