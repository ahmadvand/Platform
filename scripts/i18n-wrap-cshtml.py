#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Phase 81 Task E — wrap leftover Persian strings in Razor views.
Rules:
  <script> blocks : 'fa' / "fa" JS strings (no @, not already HrT) -> HrT('fa')
  HTML attributes : title/placeholder/aria-label/data-confirm/data-modal-title/alt
                    with static Persian value -> attr="@HrT.T("fa")"
  HTML text nodes : >TEXT<  -> >@HrT.T("TEXT")<
  C#-ish lines    : plain literals -> HrT.T("fa")   (skips ViewBag.Title / ViewData["Title"])
"""
import re, os, sys

FA = re.compile(r'[\u0600-\u06FF]')
ATTR_NAMES = r'(?:title|placeholder|aria-label|aria-title|aria-placeholder|data-confirm|data-modal-title|data-ajax-confirm|alt|data-empty|aria-pressed-label)'

def wrap_js_line(line: str):
    out = []; i = 0; n = len(line)
    while i < n:
        c = line[i]
        if c in ('"', "'"):
            q = c; j = i + 1; buf = []
            while j < n and line[j] != q:
                if line[j] == '\\' and j + 1 < n: buf.append(line[j:j+2]); j += 2; continue
                buf.append(line[j]); j += 1
            if j < n:
                content = ''.join(buf)
                pre = ''.join(out)[-30:]
                already = pre.rstrip().endswith(('HrT(', 'HrT.T(', 'HrT.F('))
                if FA.search(content) and '@' not in content and not already and '\\' not in content and content.strip():
                    out.append("HrT('" + content.replace("'", "\\'") + "')")
                else:
                    out.append(line[i:j+1])
                i = j + 1; continue
        out.append(c); i += 1
    return ''.join(out)

def wrap_html_line(line: str):
    # lines embedding HTML inside C# strings — text-node rule must not touch them
    if re.search(r'Html\.Raw\(|"[^"]*<\w+[^"]*"', line):
        # still allow pure-attribute wrapping but NOT text nodes
        def attr_repl2(m):
            name, eq, q, val, closeq = m.group(1), m.group(2), m.group(3), m.group(4), m.group(5)
            if FA.search(val) and '@' not in val and '{' not in val and '}' not in val:
                return f'{name}{eq}{q}@HrT.T("{val}"){closeq}'
            return m.group(0)
        return re.sub(rf"((?<!@)\b{ATTR_NAMES})(=)(['\"])([^'\"]*)(\3)", attr_repl2, line)
    # attributes with static Persian values
    def attr_repl(m):
        name, eq, q, val, closeq = m.group(1), m.group(2), m.group(3), m.group(4), m.group(5)
        if FA.search(val) and '@' not in val and '{' not in val and '}' not in val:
            return f'{name}{eq}{q}@HrT.T("{val}"){closeq}'
        return m.group(0)
    line = re.sub(rf"((?<!@)\b{ATTR_NAMES})(=)(['\"])([^'\"]*)(\3)", attr_repl, line)
    # HTML text nodes
    def txt_repl(m):
        a, txt, b = m.group(1), m.group(2), m.group(3)
        t = txt.strip()
        if not FA.search(t): return m.group(0)
        if '@' in txt or '<' in txt or '"' in txt: return m.group(0)
        if t.startswith(('۰','۱','۲','۳','۴','۵','۶','۷','۸','۹')) and len(t) <= 6: return m.group(0)  # numbers
        return a + txt.replace(t, '@HrT.T("' + t + '")', 1) + b
    if '<' in line and '>' in line:
        line = re.sub(r'(>)([^<>@"]+)(<)', txt_repl, line)
    return line

def wrap_cs_line(line: str):
    # skip title assignments (layout translates)
    if re.search(r'(ViewBag\.Title|ViewData\[\"Title\"\])\s*=', line): return line
    out = []; i = 0; n = len(line)
    while i < n:
        c = line[i]
        if c == '"':
            j = i + 1
            while j < n and line[j] != '"':
                if line[j] == '\\': j += 1
                j += 1
            if j < n:
                content = line[i+1:j]
                pre = ''.join(out)[-45:]
                already = pre.rstrip().endswith(('HrT.T(', 'HrT.F('))
                is_cmp = re.search(r'(==|!=)\s*$', pre) or pre.rstrip().endswith('case')
                is_key = re.search(r'\[\s*$', pre)
                if FA.search(content) and not already and not is_cmp and not is_key and '@' not in content:
                    out.append('HrT.T("' + content + '")')
                else:
                    out.append(line[i:j+1])
                i = j + 1; continue
        out.append(c); i += 1
    return ''.join(out)

CS_HINT = re.compile(r'(@\{|^\s*var\s|^\s*const\s|^\s*new\s|\(\"|\),\s*$|=>|^\s*\("|\bstring\[\]|^\s*\{)', )
HTML_HINT = re.compile(r'<\w+|</\w+|/>|@HrT|@T\(')

def process(path: str):
    lines = open(path, encoding='utf-8').read().split('\n')
    in_script = in_style = False
    changed = 0
    for idx, line in enumerate(lines):
        low = line.lower()
        if '<script' in low: in_script = True
        if '</script' in low: in_script = False; continue
        if '<style' in low: in_style = True
        if '</style' in low: in_style = False; continue
        if in_style: continue
        if not FA.search(line): continue
        if '@*' in line or line.strip().startswith('//'): continue
        orig = line
        if in_script:
            new = wrap_js_line(line)
        else:
            new = wrap_html_line(line)
            if new == line and CS_HINT.search(line) and not HTML_HINT.search(line):
                new = wrap_cs_line(line)
        if new != line:
            lines[idx] = new; changed += 1
    if changed:
        open(path, 'w', encoding='utf-8').write('\n'.join(lines))
    return changed

ROOT = "/home/z/my-project/aspnet/src"
targets = sys.argv[1:] if len(sys.argv) > 1 else None
files = []
for root, ds, fs in os.walk(ROOT):
    if '/wwwroot' in root or '/dl/' in root or '/bin/' in root or '/obj/' in root: continue
    for fn in fs:
        if fn.endswith('.cshtml'):
            p = os.path.join(root, fn)
            files.append(os.path.relpath(p, ROOT))
tot = 0
for f in sorted(files):
    n = process(os.path.join(ROOT, f))
    if n: print(f"[{n:3d}] {f}"); tot += n
print("TOTAL lines:", tot)
