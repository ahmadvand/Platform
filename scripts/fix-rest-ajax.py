#!/usr/bin/env python3.13
# تبدیل عمومی کنترلرهای باقی‌مانده به حالت دوگانه AJAX/Redirect
# - گارد same-line → درج AjaxResult(false, msg) داخل بلاک
# - هر ریدایرکت خالی ۸-فاصله → بلوک هوشمند TempData (ok/msg از ToastOk/ToastErr)
import re

BASE = '/home/z/my-project/aspnet/src/HrAttendance.Web/Controllers/'
FILES = ['RequestsController', 'VisitorsController', 'NotificationsController',
         'PayrollController', 'HolidaysController', 'AttendanceController', 'GatewayController']

GUARD_RE = re.compile(r'\{ ToastErr\((\$?".*?")\); (return (?:Redirect|RedirectToAction)\(.*\); \})')
SMART = [
    '        if (Ajax)',
    '        {',
    '            var isOk = TempData["ToastErr"] is null;',
    '            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");',
    '            return AjaxResult(isOk, msg);',
    '        }',
]

for name in FILES:
    F = BASE + name + '.cs'
    lines = open(F, encoding='utf-8').read().split('\n')
    out, guards, finals = [], 0, 0
    for ln in lines:
        m = GUARD_RE.search(ln)
        if m and 'if (Ajax) return AjaxResult' not in ln:
            ln = GUARD_RE.sub(
                lambda g: '{ ToastErr(%s); if (Ajax) return AjaxResult(false, %s); %s' % (g.group(1), g.group(1), g.group(2)),
                ln)
            guards += 1
            out.append(ln)
            continue
        # ریدایرکت خالی ۸-فاصله (بدون if (Ajax) قبلی در همان متد) → بلوک هوشمند
        if re.match(r'^        return (?:Redirect\("|RedirectToAction\()', ln) \
                and 'if (Ajax) return AjaxResult' not in out[-1] if out else False:
            pass
        if re.match(r'^        return (?:Redirect\(\$?"|Redirect\("|RedirectToAction\()', ln):
            recent = '\n'.join(out[-8:])
            if 'var isOk = TempData' in recent:
                out.append(ln)  # قبلاً تبدیل شده (idempotent)
                continue
            out.extend(SMART)
            out.append(ln)
            finals += 1
            continue
        out.append(ln)
    open(F, 'w', encoding='utf-8').write('\n'.join(out))
    print(f'{name:26} guards={guards} finals={finals}')
