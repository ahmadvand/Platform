#!/usr/bin/env python3.13
# تبدیل AccessController به حالت دوگانه AJAX/Redirect — خط‌محور v2
import re

F = '/home/z/my-project/aspnet/src/HrAttendance.Web/Controllers/AccessController.cs'
lines = open(F, encoding='utf-8').read().split('\n')
out = []
guards = ml_guards = scan_done = finals = 0

GUARD_RE = re.compile(r'\{ ToastErr\((\$?".*?")\); (return Redirect\("[^"]*"\); \})')

for ln in lines:
    # Scan: final خاص — tone قرمز برای اسکن غیرمجاز (رویداد ثبت شده → ok:true)
    if ln == '        return Redirect("/Access?tab=events");' and not scan_done:
        out.append('        if (Ajax) return AjaxResult(true, allowed ? $"اسکن مجاز: {emp.FullName} — {gate.Name}" : $"اسکن غیرمجاز: {emp.FullName} — {reason}",')
        out.append('            new { tone = allowed ? "ok" : "err" });')
        out.append(ln)
        scan_done = 1
        continue
    # گارد same-line — هر جای خط (با یا بدون پیشوند if)
    m = GUARD_RE.search(ln)
    if m:
        ln = GUARD_RE.sub(
            lambda g: '{ ToastErr(%s); if (Ajax) return AjaxResult(false, %s); %s' % (g.group(1), g.group(1), g.group(2)),
            ln)
        guards += 1
        out.append(ln)
        continue
    # گارد چندخطی: return ۱۲-فاصله بلافاصله بعد از ToastErr
    if re.match(r'^            return Redirect\("/Access\?tab=\w+"\);[ \t]*$', ln) and out \
            and 'ToastErr' in out[-1] and 'if (Ajax) return AjaxResult' not in out[-1]:
        out.append('            if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? "خطا");')
        out.append(ln)
        ml_guards += 1
        continue
    # final ساده: ۸-فاصله — پیام از TempData (ToastOk یا ToastErr)
    if re.match(r'^        return Redirect\("/Access[^"]*"\);[ \t]*$', ln):
        out.append('        if (Ajax)')
        out.append('        {')
        out.append('            var isOk = TempData["ToastErr"] is null;')
        out.append('            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");')
        out.append('            return AjaxResult(isOk, msg);')
        out.append('        }')
        out.append(ln)
        finals += 1
        continue
    out.append(ln)

open(F, 'w', encoding='utf-8').write('\n'.join(out))
print(f'guards={guards} ml_guards={ml_guards} scan={scan_done} finals={finals}')
assert guards == 5 and ml_guards == 2 and scan_done == 1 and finals == 13, 'unexpected counts'
