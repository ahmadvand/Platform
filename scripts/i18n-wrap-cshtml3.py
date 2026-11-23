#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Phase 81 Task E pass 3 — options display text + confirm attrs with Razor interpolation."""
import re, os, sys

FA = re.compile(r'[\u0600-\u06FF]')
ROOT = "/home/z/my-project/aspnet/src"

def wrap_option(line: str) -> str:
    # <option value="X">X</option>  (display == value, no @) -> display wrapped
    def repl(m):
        lead, val, mid, disp, tail = m.group(1), m.group(2), m.group(3), m.group(4), m.group(5)
        if not FA.search(val): return m.group(0)
        if '@' in disp: return m.group(0)
        disp_clean = disp.strip()
        if disp_clean != val.strip(): return m.group(0)
        return f'{lead}{val}{mid}@HrT.T("{val}"){tail}'
    line = re.sub(r'(<option\s+value=")([^"]+)(">)((?:(?!@)[^<])*)(</option>)', repl, line)
    # <option>X</option> without value attr (X Persian, no @) -> value stays text? keep raw (value becomes text) — wrap display only
    def repl2(m):
        lead, disp, tail = m.group(1), m.group(2), m.group(3)
        if not FA.search(disp) or '@' in disp: return m.group(0)
        return f'{lead}@HrT.T("{disp.strip()}"){tail}'
    line = re.sub(r'(<option(?:\s+value="")?>)((?:(?!@)[^<])*)(</option>)', repl2, line)
    return line

def split_razor(value: str):
    """split attr value into [('txt', s) | ('razor', expr)] by @(...) with nesting"""
    parts = []; buf = []; i = 0; n = len(value)
    while i < n:
        if value[i] == '@' and i + 1 < n and value[i+1] == '(':
            depth = 0; j = i + 1
            while j < n:
                if value[j] == '(': depth += 1
                elif value[j] == ')':
                    depth -= 1
                    if depth == 0: break
                j += 1
            if j < n:
                if buf: parts.append(('txt', ''.join(buf))); buf = []
                parts.append(('razor', value[i+2:j]))
                i = j + 1; continue
        buf.append(value[i]); i += 1
    if buf: parts.append(('txt', ''.join(buf)))
    return parts

def wrap_confirm(line: str) -> str:
    attrs = 'data-confirm|data-ajax-confirm|data-danger-confirm'
    def repl(m):
        name, eq, q, val, closeq = m.group(1), m.group(2), m.group(3), m.group(4), m.group(5)
        if '@' not in val: return m.group(0)
        parts = split_razor(val)
        out = []
        for kind, s in parts:
            if kind == 'txt':
                out.append(s if not FA.search(s) else f'@HrT.T("{s}")')
            else:
                out.append('@(' + s + ')')
        return f'{name}{eq}{q}{"".join(out)}{closeq}'
    line = re.sub(rf'((?<!@)\b(?:{attrs}))(=)(")([^"]*)(")', repl, line)
    # onclick="return confirm('...@(...)...')"
    def repl2(m):
        pre, q, val, closeq = m.group(1), m.group(2), m.group(3), m.group(4)
        if '@' not in val: return m.group(0)
        parts = split_razor(val)
        out = []
        for kind, s in parts:
            if kind == 'txt':
                out.append(s if not FA.search(s) else f'@HrT.T("{s}")')
            else:
                out.append('@(' + s + ')')
        return f'{pre}{q}{"".join(out)}{closeq}'
    line = re.sub(rf"(\bconfirm\()(')([^']*)(')", repl2, line)
    return line

def process(path: str):
    lines = open(path, encoding='utf-8').read().split('\n')
    changed = 0
    for idx, line in enumerate(lines):
        if not FA.search(line): continue
        new = wrap_option(line)
        new = wrap_confirm(new)
        if new != line:
            lines[idx] = new; changed += 1
    if changed:
        open(path, 'w', encoding='utf-8').write('\n'.join(lines))
    return changed

tot = 0
for root, ds, fs in os.walk(ROOT):
    if '/wwwroot' in root or '/dl/' in root or '/bin/' in root or '/obj/' in root: continue
    for fn in fs:
        if fn.endswith('.cshtml'):
            n = process(os.path.join(root, fn))
            if n: print(f"[{n:3d}] {os.path.relpath(os.path.join(root, fn), ROOT)}"); tot += n
print("TOTAL:", tot)
