#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۸۱ — مبدل خودکار پیام‌های فارسی کنترلرها به HrT.T / HrT.F.
الگوها:
  1) ToastOk/ToastErr/ToastWarn/ToastInfo/Ok/BadRequest/NotFound("...")  → HrT.T("...")
  2) همین‌ها با $"..."  → HrT.F("...{0}...", expr, ...)
  3) message = "..." / message = $"..."  → همین تبدیل
محدودیت‌های ایمنی:
  - سوراخ‌های میان‌یابی دارای " (کوتیشن تو در تو) یا لیترال فارسی → دست نخورده (اصلاح دستی)
  - رشتهٔ بدون فارسی → دست نخورده
"""
import re, sys, glob

FA = re.compile(r'[\u0600-\u06FF]')
CALLS = ["ToastOk(", "ToastErr(", "ToastWarn(", "ToastInfo(",
         "ToastDone(true, ", "ToastDone(false, ", "ToastDone(true,", "ToastDone(false,",
         "AjaxResult(true, ", "AjaxResult(false, ",
         "AjaxResult(true,", "AjaxResult(false,",
         'Done(', 'Fail(', 'CreateErr(', 'RoleErr(', 'msg = ', 'Err(']

def parse_literal(src, j):
    """از j (اولین کاراکتر پس از ( یا مساوی) لیتِرال رشته‌ای بخوان.
    خروجی: (interpolated, text_with_numbered_holes, holes, end_index) یا None."""
    n = len(src)
    k = j
    while k < n and src[k] in ' \t\r\n':
        k += 1
    interpolated = False
    if k < n and src[k] == '$':
        interpolated = True
        k += 1
    if k >= n or src[k] != '"':
        return None
    k += 1
    buf, holes, cur, depth = [], [], [], 0
    while k < n:
        c = src[k]
        if depth == 0 and c == '"':
            break
        if depth == 0 and c == '\\':
            if k + 1 < n:
                buf.append(src[k:k+2]); k += 2; continue
            return None
        if c == '{':
            if k + 1 < n and src[k+1] == '{':
                buf.append('{{'); k += 2; continue
            depth += 1; cur = []; k += 1; continue
        if c == '}' and depth > 0:
            depth -= 1
            if depth == 0:
                hole = ''.join(cur).strip()
                if '"' in hole or FA.search(hole) or not hole:
                    return None  # پیچیده → دستی
                holes.append(hole)
                buf.append('{%d}' % (len(holes) - 1))
            else:
                cur.append(c)
            k += 1; continue
        if depth > 0:
            cur.append(c); k += 1; continue
        buf.append(c); k += 1
    if k >= n or depth != 0:
        return None
    return interpolated, ''.join(buf), holes, k + 1

def is_call_text(src, idx, call):
    # آیا idx شروع نام تابع است (حرف قبلش غیرشناسه‌ای)
    if idx > 0 and (src[idx-1].isalnum() or src[idx-1] in '_.'):
        return False
    return True

def transform(src):
    out, i, n, changed = [], 0, len(src), 0
    while True:
        # نزدیک‌ترین فراخوانی از i
        best = None
        for call in CALLS:
            p = src.find(call, i)
            if p != -1 and is_call_text(src, p, call) and (best is None or p < best[0]):
                best = (p, call)
        # message = "..." / message = $"..."
        pm = src.find('message = ', i)
        if pm != -1 and (best is None or pm < best[0]):
            best = (pm, 'message = ')
        if best is None:
            out.append(src[i:]); break
        p, call = best
        j = p + len(call)
        lit = parse_literal(src, j)
        if lit is None:
            # تلاش برای فراخوانی بعدی از انتهای همین نام فراخوانی (پیشروی تضمینی)
            out.append(src[i:p + len(call)]); i = p + len(call)
            continue
        interpolated, text, holes, endk = lit
        if not FA.search(text):
            out.append(src[i:p + len(call)]); i = p + len(call)
            continue
        if interpolated:
            repl = 'HrT.F("%s"%s)' % (text, ''.join(', ' + h for h in holes))
        else:
            repl = 'HrT.T("%s")' % text
        out.append(src[i:p])
        out.append(call + repl)
        i = endk
        changed += 1
    return ''.join(out), changed

files = sorted(set(
    glob.glob('/home/z/my-project/aspnet/src/**/Controllers/**/*.cs', recursive=True)
    + ['/home/z/my-project/aspnet/src/modules/HrModules.Attendance/SelfAttController.cs']))
total = 0
for f in files:
    src = open(f, encoding='utf-8').read()
    res, ch = transform(src)
    if ch:
        open(f, 'w', encoding='utf-8').write(res)
        print('%3d  %s' % (ch, f.replace('/home/z/my-project/aspnet/src/', '')))
        total += ch
print('TOTAL CHANGED:', total, 'FILES:', len(files))
