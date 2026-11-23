#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""اسکن نهایی: هر متن فارسی استاتیک بین > و < (بدون @ و HrT.T) در ویوها"""
import glob, re
from collections import Counter

FA = re.compile(r"[\u0600-\u06FF]")
# متن بین تگ بسته و باز — قطعهٔ استاتیک خالص
rx = re.compile(r">([^<>@{}]*[\u0600-\u06FF][^<>@{}]*)<")
skip_ctx = re.compile(r"<(script|style)\b", re.I)

rows = []
tagc = Counter()
for p in sorted(glob.glob("/home/z/my-project/aspnet/src/**/*.cshtml", recursive=True)):
    pp = p.replace("\\", "/")
    if "/wwwroot/dl/" in pp or "/bin/" in pp or "/obj/" in pp:
        continue
    src = open(p, encoding="utf-8").read()
    if skip_ctx.search(src):
        pass
    lines = src.split("\n")
    for i, ln in enumerate(lines, 1):
        if "<script" in ln or "</script" in ln:
            continue
        for m in rx.finditer(ln):
            text = m.group(1)
            if len(text.strip()) < 2:
                continue
            # تگ مالک — برای آمار
            before = ln[: m.start()]
            tg = re.findall(r"<(\w+)", before)
            tag = tg[-1] if tg else "?"
            tagc[tag] += 1
            rows.append(f'{pp.split("/aspnet/src/")[1]}:{i} [{tag}] {text.strip()[:120]}')

print("TAG_COUNTS:", dict(tagc.most_common(20)))
print("TOTAL:", len(rows))
for r in rows:
    print(r)
