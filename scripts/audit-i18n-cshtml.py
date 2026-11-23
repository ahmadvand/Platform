#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۸۱ — اسکن Razor: رشته‌های فارسی بدون T(...)/HrT در .cshtml."""
import os, re, json

ROOT = "/home/z/my-project/aspnet/src"
FA = re.compile(r'[\u0600-\u06FF]')

results = []
for dirpath, dirs, files in os.walk(ROOT):
    if any(x in dirpath for x in ("/obj/", "/bin/")):
        continue
    for fn in files:
        if not fn.endswith(".cshtml"):
            continue
        path = os.path.join(dirpath, fn)
        src = open(path, encoding="utf-8").read()
        lines = src.split("\n")
        for i, line in enumerate(lines):
            s = line.strip()
            if s.startswith("@*") or s.startswith("<!--"):
                continue
            for m in re.finditer(r'"([^"\n]*)"', line):
                t = m.group(1)
                if not FA.search(t):
                    continue
                before = line[:m.start()]
                # داخل T( یا HrT.T( / HrT.F( ؟
                if re.search(r'\bT\(\s*$', before) or re.search(r'HrT\.[TF]\([^)]*$', before):
                    continue
                # placeholder/attr data-only مثل data-*
                if 'placeholder="@T(' in line or 'title="@T(' in line:
                    continue
                results.append({"file": path.replace(ROOT + "/", ""), "line": i + 1, "text": t[:110]})
                break

json.dump(results, open("/home/z/my-project/scripts/audit-i18n-cshtml.json", "w"), ensure_ascii=False, indent=1)
by = {}
for r in results:
    by[r["file"]] = by.get(r["file"], 0) + 1
for f, n in sorted(by.items(), key=lambda kv: -kv[1]):
    print(f"{n:4d}  {f}")
print("TOTAL:", len(results))
