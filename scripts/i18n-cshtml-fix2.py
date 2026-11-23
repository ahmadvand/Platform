#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۸۱ — موج دوم Razor: optionها، سه‌تایی تک‌طرفه، برچسب‌های tuple (PaletteForm)."""
import os, re, glob

FA = re.compile(r'[\u0600-\u06FF]')
# A) option display
OPT_A = re.compile(r'(<option value="([^"]*)"([^>]*)>)([^<]*[\u0600-\u06FF][^<]*)(</option>)')
# B) تک‌طرفه‌ها
B1 = re.compile(r'(\?\s*)"([^"\n]*[\u0600-\u06FF][^"\n]*)"(\s*[:?])')      # ? "fa" :   یا  ? "fa" ?
B2 = re.compile(r'(:\s*)"([^"\n]*[\u0600-\u06FF][^"\n]*)"(\s*[)\];,])')    # : "fa")
B3 = re.compile(r'(\?\?\s*)"([^"\n]*[\u0600-\u06FF][^"\n]*)"(\s*[):;])')   # ?? "fa")

def safe(s):
    return s.replace('\\', '\\\\')

def fix_line(ln):
    orig = ln
    # A) option — فقط متن نمایشی (گروه ۴)
    def opt_repl(m):
        disp = m.group(4)
        if '@' in disp or 'HrT.T(' in disp:
            return m.group(0)
        return f'{m.group(1)}@HrT.T("{safe(disp.strip())}"){m.group(5)}'
    ln = OPT_A.sub(opt_repl, ln)
    # B) تک‌طرفه — فقط اگر قبلش == / != نباشد (مقایسه داده)
    def b_repl_factory(pattern):
        def repl(m):
            pre = m.group(1)
            lit = m.group(2)
            # اگر بلافاصله قبل از pre عملگر مقایسه باشد → داده است
            idx = m.start()
            before = ln[max(0, idx-12):idx]
            if re.search(r'(==|!=|is|case|Contains|StartsWith)\s*$', before):
                return m.group(0)
            return f'{pre}HrT.T("{safe(lit)}"){m.group(3)}'
        return repl
    ln = B3.sub(b_repl_factory(B3), ln)
    ln = B1.sub(b_repl_factory(B1), ln)
    ln = B2.sub(b_repl_factory(B2), ln)
    # C) tuple labelها — خط شونده با («کلید فنی», «برچسب فارسی»)
    if re.match(r'^\s*\(\s*"', ln):
        def tup_repl(m, line=ln):
            whole = m.group(0)
            if 'HrT.T(' in line:
                return whole
            return whole
        # همهٔ literalهای فارسی این خط را بپوش مگر داخل HrT.T
        out, i, n = [], 0, len(ln)
        while True:
            m = re.compile(r'"([^"\n]+)"').search(ln, i)
            if not m:
                out.append(ln[i:]); break
            out.append(ln[i:m.start()])
            lit = m.group(1)
            before = ln[max(0, m.start()-8):m.start()]
            if FA.search(lit) and 'HrT.T(' not in before and 'T("' not in before:
                out.append(f'HrT.T("{safe(lit)}")')
            else:
                out.append(m.group(0))
            i = m.end()
        ln = ''.join(out)
    return ln

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
        if '<script' in s: in_script = True
        if '</script' in s:
            in_script = False; out.append(ln); continue
        if in_script:
            out.append(ln); continue
        new = fix_line(ln)
        if new != ln: changed += 1
        out.append(new)
    if changed:
        open(f, 'w', encoding='utf-8').write('\n'.join(out))
        print('%3d %s' % (changed, f.replace('/home/z/my-project/aspnet/src/', '')))
        tot += changed
print('TOTAL LINES:', tot)
