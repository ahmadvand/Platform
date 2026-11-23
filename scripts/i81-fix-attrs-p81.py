#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۸۱ — ترجمهٔ بخش ثابت صفات مرکب Razor:
  data-modal-title="ویرایش واحد — @d.Name"  →  data-modal-title="@T("ویرایش واحد") — @d.Name"
فقط وقتی بخش ثابت فارسی دارد و خط هنوز @T/@HrT.T برای آن بخش ندارد.
الگوها: data-modal-title= / title= با ساختار «STATIC@...» یا «STATIC — @...»
"""
import os, re

ROOT = "/home/z/my-project/aspnet/src"
SKIP_DIRS = {"bin", "obj", "wwwroot", "node_modules", ".git"}
FA = re.compile(r'[\u0600-\u06FF]')

ATTR = re.compile(r'(data-modal-title|title|data-confirm|placeholder)="([^"@]*?[\u0600-\u06FF][^"@]*?)\s*(@)')

def walk():
    for dp, dns, fns in os.walk(ROOT):
        dns[:] = [d for d in dns if d not in SKIP_DIRS]
        for fn in fns:
            if fn.endswith(".cshtml"):
                yield os.path.join(dp, fn)

def fix_line(ln):
    # فقط صفات هدف؛ بخش ثابت فارسی بلافاصله قبل از @dاینامیک
    def rep(m):
        attr, static, at = m.group(1), m.group(2), m.group(3)
        if "T(" in ln and attr == "title" and "HrT.Num" in ln:
            return m.group(0)  # ترکیب‌های خاص دستی می‌روند
        return f'{attr}="@(HrT.T("{static.strip()}") + " "){at}' if False else f'{attr}="@HrT.T("{static.strip()}"){at}'
    return ATTR.sub(rep, ln)

changed = {}
for p in walk():
    src = open(p, encoding="utf-8").read()
    lines = src.split("\n")
    n = 0
    for i, ln in enumerate(lines):
        if "@" not in ln or not FA.search(ln):
            continue
        if 'HrT.T(' in ln or '@T(' in ln:
            continue  # قبلاً ترجمهٔ بخشی از خط
        new = fix_line(ln)
        if new != ln:
            lines[i] = new
            n += 1
    if n:
        open(p, "w", encoding="utf-8").write("\n".join(lines))
        changed[os.path.relpath(p, ROOT)] = n

for f, n in sorted(changed.items(), key=lambda x: -x[1]):
    print(f"  {n:3d}  {f}")
print(f"TOTAL: {sum(changed.values())} attrs in {len(changed)} files")
