#!/usr/bin/env python3
# فاز ۱۲۴-د۲ — هدر صفحهٔ شیفت‌ها (جای صریح: قبل از بنر فاز F)
import io, sys

P = "/home/z/my-project/aspnet/src/modules/HrModules.Attendance/Views/Shifts/Index.cshtml"
ANCHOR = '@* فاز ۱۲۴-ف — بنر وضعیت تخصیص شیفت مطابق طرح Figma (دادهٔ واقعی) *@'

with io.open(P, encoding="utf-8") as f:
    src = f.read()
if "page-h" in src:
    print("SKIP (هدر موجود)"); sys.exit(0)
n = src.count(ANCHOR)
if n != 1:
    print(f"ABORT — لنگر یگانه نیست ({n}x)"); sys.exit(1)
BLOCK = '''@* فاز ۱۲۴-د — هدر صفحه مطابق طرح Figma (زیرنویس با دادهٔ واقعی همین صفحه) *@
<div class="page-h">
    <div>
        <h1>@HrT.T("شیفت‌ها و نوبت‌کاری")</h1>
        <div class="page-sub">@HrT.F("{0} شیفت تعریف‌شده · {1} پرسنل شیفت‌دار", Jalali.Num(shifts.Count), Jalali.Num(assignedCount))</div>
    </div>
</div>
'''
with io.open(P, "w", encoding="utf-8") as f:
    f.write(src.replace(ANCHOR, BLOCK + ANCHOR, 1))
print("OK: Shifts/Index.cshtml — هدر قبل از بنر")
