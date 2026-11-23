#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فهرست خطوط شرطی با برچسب فارسی (ternary/switch/CommandLabels) بدون wrap"""
import glob, re

pats = re.compile(r'(?:=>\s*"[^"]*[\u0600-\u06FF][^"]*"|\?\s*"[^"]*[\u0600-\u06FF][^"]*"\s*:\s*"[^"]*[\u0600-\u06FF][^"]*"|CommandLabels\.GetValueOrDefault)')
for p in glob.glob('/home/z/my-project/aspnet/src/**/*.cshtml', recursive=True):
    pp = p.replace('\\', '/')
    if '/wwwroot/dl/' in pp or '/bin/' in pp or '/obj/' in pp:
        continue
    lines = open(p, encoding='utf-8').read().split('\n')
    for i, ln in enumerate(lines, 1):
        if pats.search(ln) and '@HrT.T' not in ln:
            print(f'{pp.split("/aspnet/src/")[1]}:{i}: {ln.strip()[:170]}')
