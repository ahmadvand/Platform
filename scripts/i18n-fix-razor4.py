#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Repair pass: @HrT.T("...@expr...")  ->  @HrT.T("...")@expr@HrT.T("...")"""
import re, os

FA = re.compile(r'[\u0600-\u06FF]')
ROOT = "/home/z/my-project/aspnet/src"

def tokenize(s: str):
    """split into [('txt', s)] and [('expr', e)] by @(...) or @ident(.chain)?(call)?"""
    parts = []; buf = []; i = 0; n = len(s)
    ident = re.compile(r'[A-Za-z_]\w*')
    while i < n:
        if s[i] == '@' and i + 1 < n:
            rest = s[i+1:]
            mparen = None
            if rest[0] == '(':
                depth = 0; j = i + 1
                while j < n:
                    if s[j] == '(': depth += 1
                    elif s[j] == ')':
                        depth -= 1
                        if depth == 0: break
                    j += 1
                mparen = (i + 2, j)  # expr inside outer parens
            else:
                m = ident.match(rest)
                if m:
                    j = i + 1 + m.end()
                    # dotted chain / null-conditional / optional call
                    while j < n:
                        if s[j] == '.' :
                            m2 = re.match(r'[\w]+\??', s[j+1:])
                            if m2: j += 1 + m2.end(); continue
                            break
                        if s[j] == '?':
                            if j + 1 < n and s[j+1] == '.':
                                m2 = re.match(r'[\w]+', s[j+2:])
                                if m2: j += 2 + m2.end(); continue
                            break
                        if s[j] == '(':
                            depth = 0; k = j
                            while k < n:
                                if s[k] == '(': depth += 1
                                elif s[k] == ')':
                                    depth -= 1
                                    if depth == 0: break
                                k += 1
                            j = k + 1; break
                        break
                    mparen = (i + 1, j)
            if mparen:
                if buf: parts.append(('txt', ''.join(buf))); buf = []
                parts.append(('expr', s[mparen[0]:mparen[1]]))
                i = mparen[1] + (1 if mparen[1] < n and s[mparen[1]] == ')' else 0)
                continue
        buf.append(s[i]); i += 1
    if buf: parts.append(('txt', ''.join(buf)))
    return parts

def repair(m):
    inner = m.group(1)
    if '@' not in inner: return m.group(0)
    parts = tokenize(inner)
    out = []
    for kind, s in parts:
        if kind == 'expr':
            out.append('@(' + s + ')')
        else:
            if s == '': continue
            out.append('@HrT.T("' + s + '")')
    return ''.join(out)

pat = re.compile(r'@HrT\.T\("((?:[^"\\]|\\.)*)"\)')

tot = 0
for root, ds, fs in os.walk(ROOT):
    if '/wwwroot' in root or '/dl/' in root or '/bin/' in root or '/obj/' in root: continue
    for fn in fs:
        if not fn.endswith('.cshtml'): continue
        p = os.path.join(root, fn)
        s = open(p, encoding='utf-8').read()
        new, n = pat.subn(repair, s)
        if new != s:
            open(p, 'w', encoding='utf-8').write(new)
            print(f"[{n}] {os.path.relpath(p, ROOT)}"); tot += n
print("TOTAL:", tot)
