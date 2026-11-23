#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۸۱ — مبدل خودکار Razor (.cshtml):
  A) title|placeholder|data-modal-title|aria-label|data-confirm="فارسیِ خالص"  → ="@HrT.T("...")"
  B) سه‌تایی‌های نمایشی:  ? "فارسی" : "فارسی"  → ? HrT.T("...") : HrT.T("...")
  C) ViewData["Title"] = "فارسی"  →  = HrT.T("...")
ایمنی:
  - value= / href= / name= هرگز دست نمی‌خورند
  - سطر دارای @u.X / @( داخل attr → رها (ترکیبی)
  - اگر HrT.T( یا T( قبلاً قبل از همان literal باشد → رها
"""
import os, re, glob

FA = re.compile(r'[\u0600-\u06FF]')
ATTRS = ("title", "placeholder", "data-modal-title", "aria-label", "data-confirm", "data-toast", "data-placeholder")
ATTR_RE = re.compile(r'\b(title|placeholder|data-modal-title|aria-label|data-confirm|data-toast[a-z-]*|data-placeholder)="([^"]*)"')
TERN_RE = re.compile(r'(\?\s*)"([^"\n]*[\u0600-\u06FF][^"\n]*)"\s*(:\s*)"([^"\n]*[\u0600-\u06FF][^"\n]*)"')
TITLE_RE = re.compile(r'(ViewData\["Title"\]\s*=\s*)"([^"\n]*[\u0600-\u06FF][^"\n]*)"')

def fix_line(line):
    orig = line
    # C) ViewData Title
    m = TITLE_RE.search(line)
    if m and 'HrT.T(' not in line:
        line = TITLE_RE.sub(lambda mm: mm.group(1) + 'HrT.T("' + mm.group(2) + '")', line, count=1)
    # A) attributes — فقط مقدار خالص فارسی بدون @ و بدون + و بدون (
    def attr_repl(mm):
        attr, val = mm.group(1), mm.group(2)
        if not FA.search(val):
            return mm.group(0)
        if '@' in val or '+' in val or '(' in val:
            return mm.group(0)  # ترکیبی — دستی
        if attr == 'value':
            return mm.group(0)
        return f'{attr}="@HrT.T("{val.replace(chr(92), chr(92)*2)}")"'
    line = ATTR_RE.sub(attr_repl, line)
    # B) ternary display
    def tern_repl(mm):
        a, l, colon, r = mm.group(1), mm.group(2), mm.group(3), mm.group(4)
        return f'{a}HrT.T("{l}"){colon}HrT.T("{r}")'
    if '@' not in line or True:
        line = TERN_RE.sub(tern_repl, line)
    return line

files = [p for p in glob.glob('/home/z/my-project/aspnet/src/**/*.cshtml', recursive=True)
         if '/obj/' not in p and '/bin/' not in p]
tot = 0
for f in files:
    lines = open(f, encoding='utf-8').read().split('\n')
    changed = 0
    out = []
    in_script = False
    for ln in lines:
        s = ln.strip().lower()
        if '<script' in s:
            in_script = True
        if '</script' in s:
            in_script = False
            out.append(ln); continue
        if in_script:
            out.append(ln); continue  # بلوک JS — دستی
        new = fix_line(ln)
        if new != ln:
            changed += 1
        out.append(new)
    if changed:
        open(f, 'w', encoding='utf-8').write('\n'.join(out))
        print('%3d %s' % (changed, f.replace('/home/z/my-project/aspnet/src/', '')))
        tot += changed
print('TOTAL LINES:', tot)
