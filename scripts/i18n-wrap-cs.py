#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
i18n auto-wrapper for C# sources (Phase 81, Task D).
Wraps Persian user-visible string literals with HrT.T("...") / HrT.F("...", args)
ONLY in message contexts (return / message= / Errors.Add / throw / switch-arm tuples),
skipping logs, SQL, attributes, dictionary keys, comparisons and already-wrapped calls.
Excluded files contain DB-normalization data, manifests, seeders and logs that are
translated at render time (architecture: source strings stay Persian).
"""
import json, re, sys, os

ROOT = "/home/z/my-project/aspnet/src"
AUDIT = "/home/z/my-project/scripts/audit-i18n-result.json"

EXCLUDE = {
    "HrPlatform.Core/Services/HrLang.cs",
    "HrPlatform.Core/Services/PdfReport.cs",
    "HrPlatform.Core/Services/Jalali.cs",
    "HrPlatform.Core/Services/HrCal.cs",
    "HrPlatform.Core/Services/PlateFmt.cs",
    "HrPlatform.Core/Services/EpochDateTime.cs",
    "HrAttendance.Web/Controllers/ExportController.cs",
    "HrAttendance.Web/Services/ExcelImport.cs",
    "HrAttendance.Web/Data/DbSeeder.cs",
    "HrAttendance.Web/Infrastructure/DbInit.cs",
    "HrPlatform.Core/Domain/Entities.cs",
    "HrPlatform.Contracts/GatewayContracts.cs",
    "HrPlatform.Contracts/PeopleContracts.cs",
    "HrPlatform.Contracts/ModuleSystem.cs",
    "modules/HrModules.Payroll/Data/PayrollEngine.cs",
}
RE_EXCLUDE = re.compile(r"(Module\.cs$|Seeder\.cs$|Entities\.cs$|GatewayWorker\.cs$|ZkPoller\.cs$)")

FA = re.compile(r'[\u0600-\u06FF]')
# contexts in whose prefix a Persian literal is considered a user-visible message
WRAP_CTX = re.compile(r"""
    (?:return\s*\(?\s*)                                   |  # return "...";  return ("..."
    (?:\bmessage\s*[:=]\s*)                               |
    (?:\bMessage\s*=\s*)                                  |
    (?:\bmsg\s*=\s*)                                      |
    (?:\berror\s*[:=]\s*)                                 |
    (?:\bError\s*=\s*)                                    |
    (?:\bdetail\s*[:=]\s*)                                |
    (?:\bhint\s*[:=]\s*)                                  |
    (?:\bHint\s*=\s*)                                     |
    (?:\bnotes\s*[:=]\s*)                                 |
    (?:\bNote\s*=\s*)                                     |
    (?:\bNotes\s*=\s*)                                    |
    (?:\btext\s*[:=]\s*)                                  |
    (?:\bText\s*=\s*)                                     |
    (?:\btitle\s*[:=]\s*)                                 |
    (?:\bTitle\s*=\s*)                                    |
    (?:\.(?:Errors|Warnings|msgs|messages)\s*\.\s*Add\s*\() |
    (?:\.Add\s*\(\s*$)                                    |
    (?:\bToast(?:Ok|Err|Done|Info)\s*\()                  |  # فاز ۸۱ — توست‌های مستقیم
    (?:\bAlert\s*\()                                      |
    (?:throw\s+new\s+\w+(?:Exception|Error)\s*\()         |
    (?:string\.Format\s*\()                               |
    (?:_\s*=>\s*\(?)                                      |  # switch arm tuple
    (?:\?\s*"")                                          # (unused)
""", re.X)

SKIP_LINE = re.compile(r"""
    ^\s*\[                                  |  # attributes
    \.\s*Log(?:Information|Warning|Error|Debug|Critical|Trace)\s*\( |
    (?:Debug|Console)\.(?:WriteLine|Print)\( |
    \bnameof\s*\(                           |
    ALTER\s+TABLE|CREATE\s+TABLE|INSERT\s+INTO|UPDATE\s+\w+\s+SET|TEXT\s+NOT\s+NULL |
    ^\s*//                                  |  # full-line comment
    ^\s*///                                 |
    ^\s*\*
""", re.X | re.I)

def strip_trailing_comment(line: str):
    """remove // comment outside strings; return (code, comment)"""
    in_s = False; interp = False; i = 0
    while i < len(line):
        c = line[i]
        if in_s:
            if c == '\\' and not interp and i + 1 < len(line): i += 2; continue
            if c == '"':
                if interp and i + 1 < len(line) and line[i+1] == '"': i += 2; continue
                in_s = False
        else:
            if c == '"':
                j = i - 1
                interp = False
                while j >= 0 and line[j] == '$': interp = True; j -= 1
                in_s = True
            elif c == '/' and i + 1 < len(line) and line[i+1] == '/':
                return line[:i], line[i:]
        i += 1
    return line, ""

def scan_literal(code: str, start: int):
    """start at first char of a literal: may begin with $ then " .
    returns (kind, end_index, content, interp) where kind in (None,'plain','interp','verbatim')"""
    i = start
    interp = False
    if code[i] == '$':
        interp = True; i += 1
    if i < len(code) and code[i] == '@':
        return ('verbatim', _scan_verbatim(code, i), '', False)
    if i >= len(code) or code[i] != '"':
        return (None, start, '', False)
    j = i + 1
    while j < len(code):
        c = code[j]
        if c == '\\': j += 2; continue
        if c == '"':
            # doubled quote escape inside interpolated string: $"a ""x"" b" is verbatim-style; in interpolated (non-verbatim) "" is just end+start; treat as end
            return ('str', j + 1, code[i+1:j], interp)
        j += 1
    return (None, start, '', False)

def _scan_verbatim(code: str, i: int):
    """code[i]=='@', then quote; returns end index past closing quote"""
    j = i + 1
    if j < len(code) and code[j] == '"': j += 1  # $@"
    else: return i + 1
    while j < len(code):
        if code[j] == '"':
            if j + 1 < len(code) and code[j+1] == '"': j += 2; continue
            return j + 1
        j += 1
    return j

def split_holes(s: str):
    """split interpolated content into (parts, holes):
    parts: list of literal chunks (with {{ }} unescaped NOT done - keep raw), holes: list of expr strings."""
    parts = []; holes = []; buf = []; i = 0; depth = 0; hole_start = -1
    in_q = False
    while i < len(s):
        c = s[i]
        if depth == 0:
            if c == '{':
                if i + 1 < len(s) and s[i+1] == '{':
                    buf.append('{{'); i += 2; continue
                depth = 1; hole_start = i + 1; in_q = False
                parts.append(''.join(buf)); buf = []; i += 1; continue
            buf.append(c); i += 1; continue
        else:
            if in_q:
                if c == '\\': i += 2; continue
                if c == '"': in_q = False
                i += 1; continue
            if c == '"': in_q = True; i += 1; continue
            if c == '{': depth += 1; i += 1; continue
            if c == '}':
                depth -= 1
                if depth == 0:
                    holes.append(s[hole_start:i]); i += 1; continue
            i += 1; continue
    parts.append(''.join(buf))
    return parts, holes

def conv_hole(h: str):
    """{expr,align:fmt} -> {N,align:fmt} ; return (fmt_part, expr)"""
    h = h.strip()
    m = re.match(r'^(.*?)(,[-+0-9]*|,[^:]+)?(:.*)?$', h, re.S)
    expr = m.group(1).strip()
    align = m.group(2) or ''
    fmt = m.group(3) or ''
    return expr, align, fmt

def wrap_line(code: str):
    """wrap Persian literals in one code line; returns (newline, n_plain, n_interp, n_skipped_interp)"""
    out = []; i = 0; n = len(code)
    n_plain = n_interp = n_skip = 0
    while i < n:
        c = code[i]
        if c != '"' and c != '$':
            out.append(c); i += 1; continue
        # candidate literal
        lit_start = i
        kind, end, content, interp = scan_literal(code, i)
        if kind == 'verbatim':
            out.append(code[i:end]); i = end; continue
        if kind is None:
            out.append(c); i += 1; continue
        prefix = ''.join(out)[-60:]
        before = code[max(0, i-3):i]
        is_cmp = before.rstrip().endswith(('==', '!=', '>=', '<='))
        already = prefix.rstrip().endswith(('HrT.T(', 'HrT.F(', 'HrT.Digits(', 'HrT.Num('))
        dkey = re.search(r'\[\s*$', prefix)  # dictionary key position
        has_fa = FA.search(content)
        if kind == 'str' and has_fa and not is_cmp and not already and not dkey and WRAP_CTX.search(prefix):
            if interp:
                parts, holes = split_holes(content)
                if any(FA.search(h) for h in holes):
                    out.append(code[i:end]); i = end; n_skip += 1; continue
                fmt = ''
                args = []
                hi = 0
                # rebuild content with {N}
                rebuilt = ''
                for p_i, part in enumerate(parts):
                    rebuilt += part
                    if p_i < len(holes):
                        expr, align, f = conv_hole(holes[p_i])
                        rebuilt += '{' + str(hi) + align + f + '}'
                        args.append(expr)
                        hi += 1
                if hi == 0:
                    out.append('HrT.T(' + code[i:end] + ')')
                    n_plain += 1
                else:
                    arg_s = ', '.join(a.strip() for a in args)
                    out.append('HrT.F(' + '"' + rebuilt + '", ' + arg_s + ')')
                    n_interp += 1
                i = end
            else:
                out.append('HrT.T(' + code[i:end] + ')')
                n_plain += 1
                i = end
        else:
            out.append(code[i:end])
            if kind == 'str' and has_fa and not already and not is_cmp and not dkey and interp:
                n_skip += 1
            i = end
    return ''.join(out), n_plain, n_interp, n_skip

def process_file(path: str):
    lines = open(path, encoding='utf-8').read().split('\n')
    changed = 0; report = []
    for idx, line in enumerate(lines):
        if not FA.search(line): continue
        code, comment = strip_trailing_comment(line)
        if SKIP_LINE.search(code): continue
        new, np, ni, ns = wrap_line(code)
        if new != code:
            lines[idx] = new + comment
            changed += 1
            report.append((idx + 1, code.strip()[:110], new.strip()[:110]))
    if changed:
        open(path, 'w', encoding='utf-8').write('\n'.join(lines))
    return changed, report

def main():
    audit = json.load(open(AUDIT, encoding='utf-8'))
    targets = []
    for f, arr in audit['cs'].items():
        if f in EXCLUDE or RE_EXCLUDE.search(f): continue
        if not any(len(e) > 2 and e[2] == 'ui' for e in arr): continue
        p = os.path.join(ROOT, f)
        if os.path.exists(p): targets.append(f)
    total = 0; files_changed = 0
    for f in sorted(targets):
        p = os.path.join(ROOT, f)
        n, rep = process_file(p)
        if n:
            files_changed += 1; total += n
            print(f"[{n:3d}] {f}")
            for ln, old, new in rep[:4]:
                print(f"      L{ln}: {old[:70]}")
                print(f"        -> {new[:90]}")
    print(f"\nTOTAL: {total} lines changed in {files_changed} files (of {len(targets)} targets)")

if __name__ == '__main__':
    main()
