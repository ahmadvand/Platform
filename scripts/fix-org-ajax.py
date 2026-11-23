#!/usr/bin/env python3.13
# تبدیل OrgController به حالت دوگانه AJAX/Redirect — نسخه امن خط‌محور (بدون عبور از خط)
import re

F = '/home/z/my-project/aspnet/src/HrAttendance.Web/Controllers/OrgController.cs'
src = open(F, encoding='utf-8').read()

# --- MoveDept: ToastOk چندخطی → var okMsg + ToastOk(okMsg) ---
multi = re.search(r'ToastOk\(leaveEmployees\s*\?\s*\$"(.*?)"\s*:\s*\$"(.*?)"\);', src, re.S)
assert multi, 'MoveDept ToastOk not found'
src = src.replace(
    multi.group(0),
    'var okMsg = leaveEmployees\n'
    f'                ? $"{multi.group(1)}"\n'
    f'                : $"{multi.group(2)}";\n'
    '            ToastOk(okMsg);')

lines = src.split('\n')
out = []
guards = successes = finals = 0
for ln in lines:
    # Rule A: گارد same-line — AjaxResult باید «داخل» بلاک خطا باشد
    m = re.match(r'^(\s*\{ ToastErr\()(\$?".*?")(\); )(return Redirect\("/Org"\); \})\s*$', ln)
    if m:
        out.append(f'{m.group(1)}{m.group(2)}{m.group(3)}if (Ajax) return AjaxResult(false, {m.group(2)}); {m.group(4)}')
        guards += 1
        continue
    # Rule B: ToastOk تک‌خطی → بعدش AjaxResult(true, ...)
    m = re.match(r'^([ \t]+)ToastOk\((.+)\);[ \t]*$', ln)
    if m and 'TempData' not in m.group(2):
        out.append(ln)
        out.append(f'{m.group(1)}if (Ajax) return AjaxResult(true, {m.group(2)});')
        successes += 1
        continue
    # Rule C: ریدایرکت خالی نهایی (۸ فاصله) → مسیر catch
    if ln == '        return Redirect("/Org");':
        out.append('        if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? "خطا در عملیات");')
        out.append(ln)
        finals += 1
        continue
    out.append(ln)

open(F, 'w', encoding='utf-8').write('\n'.join(out))
print(f'guards={guards} successes={successes} finals={finals}')
assert guards >= 10 and successes == 7 and finals == 7, 'unexpected counts'
