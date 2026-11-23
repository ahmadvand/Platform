#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""اسکن موارد ترجمه‌نشده در ویوها — فاز جهانی‌سازی (گزینه‌های دراپ‌داون + رشته‌های JS)"""
import re, glob, os, json, sys

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/", "/node_modules/")

def files():
    for base in ["HrAttendance.Web/Views", "modules"]:
        for p in glob.glob(os.path.join(ROOT, base, "**", "*.cshtml"), recursive=True):
            pp = p.replace("\\", "/")
            if any(x in pp for x in EXCLUDE):
                continue
            yield pp

FA = re.compile(r'[\u0600-\u06FF]')

def has_fa(s): return bool(FA.search(s))

# آیا خط، متن فارسی داخل HrT.T(...) یا T(...) یا ارجاع C# است؟
wrap_pat = re.compile(r'(?:HrT\.T|[^A-Za-z]T)\s*\(\s*"[^"]*"')

report = {}
for p in files():
    src = open(p, encoding="utf-8").read()
    lines = src.splitlines()
    items = []
    for i, ln in enumerate(lines, 1):
        s = ln.strip()
        # ۱) تگ option با متن فارسی
        if "<option" in s:
            # متن بین > و </option> یا برچسب پس از >
            m = re.search(r'<option[^>]*>(.*)$', s)
            if m and has_fa(m.group(1)):
                frag = m.group(1)
                if not re.search(r'@?\(?(?:HrT\.)?T\(', frag):
                    items.append(("option", i, frag[:110]))
        # ۲) برچسب <label> با متن فارسی ثابت
        m = re.match(r'<label[^>]*>([^<]{2,})</label>', s)
        if m and has_fa(m.group(1)) and not wrap_pat.search(s):
            items.append(("label", i, m.group(1)[:110]))
        # ۳) رشتهٔ فارسی در JS (textContent / innerHTML / new Option / alert)
        if re.search(r"(textContent|innerHTML|innerText|new Option|alert\(|confirm\()", s) and has_fa(s):
            # فقط رشته‌های ثابت تک‌کوتیشن
            for st in re.findall(r"'([^']*[\u0600-\u06FF][^']*)'", s):
                items.append(("js-str", i, st[:110]))
    if items:
        report[os.path.relpath(p, ROOT)] = items

print(f"FILES_WITH_GAPS={len(report)}")
for f, items in sorted(report.items()):
    print(f"\n== {f} ({len(items)})")
    for kind, ln, frag in items:
        print(f"  [{kind}:{ln}] {frag}")
