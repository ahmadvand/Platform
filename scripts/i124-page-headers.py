#!/usr/bin/env python3
# فاز ۱۲۴-ج — درج هدر صفحه (page-h) در ۴ نما، مطابق طرح Figma
# فقط درج؛ هیچ خطی حذف/جابه‌جا نمی‌شود. هر لنگر باید دقیقاً یک‌بار یافت شود.
import io, sys

ROOT = "/home/z/my-project/aspnet"

def insert(path, anchor, block):
    with io.open(path, encoding="utf-8") as f:
        src = f.read()
    if "page-h" in src:
        print(f"SKIP (هدر موجود): {path}")
        return
    if src.count(anchor) != 1:
        print(f"ABORT — لنگر یگانه نیست ({src.count(anchor)}x): {path}")
        sys.exit(1)
    out = src.replace(anchor, block + anchor, 1)
    with io.open(path, "w", encoding="utf-8") as f:
        f.write(out)
    print(f"OK: {path}")

# ۱) پیشخوان یکپارچه — زیرنویس با تاریخ واقعی امروز
insert(f"{ROOT}/src/Platform.Web/Views/Home/Index.cshtml",
       '<div class="ps-grid">',
       '''@* فاز ۱۲۴-ج — هدر صفحه مطابق طرح Figma: عنوان + زیرنویس (تاریخ واقعی امروز) *@
<div class="page-h">
    <div>
        <h1>@T("پیشخوان یکپارچه")</h1>
        <div class="page-sub">@T("نمای عملیاتی امروز منابع انسانی، حراست و زیرساخت") · @HrT.TopWeekday(DateTime.Now) @HrT.TopDate(DateTime.Now)</div>
    </div>
</div>
''')

# ۲) پرسنل — زیرنویس با شمار واقعی پرسنل
insert(f"{ROOT}/src/modules/HrModules.Org/Views/Employees/Index.cshtml",
       '<div class="card">',
       '''@* فاز ۱۲۴-ج — هدر صفحه مطابق طرح Figma *@
<div class="page-h">
    <div>
        <h1>@HrT.T("پرسنل")</h1>
        <div class="page-sub">@HrT.F("{0} پرسنل در سامانه · فهرست کامل با وضعیت اشتغال و واحد سازمانی", Jalali.Num(employees.Count))</div>
    </div>
</div>
''')

# ۳) تردد روزانه — زیرنویس با آمار واقعی همان روز
insert(f"{ROOT}/src/modules/HrModules.Attendance/Views/Attendance/Index.cshtml",
       '<div data-refresh-host>',
       '''@* فاز ۱۲۴-ج — هدر صفحه مطابق طرح Figma (آمار واقعی روز انتخابی) *@
<div class="page-h">
    <div>
        <h1>@HrT.T("تردد روزانه")</h1>
        <div class="page-sub">@Jalali.FmtFa(dt) · @HrT.F("حاضر {0} · تأخیر {1} · غایب {2} · مرخصی {3}", Jalali.Num(present), Jalali.Num(late), Jalali.Num(absent), Jalali.Num(leave))</div>
    </div>
</div>
''')

# ۴) اعلان‌ها — زیرنویس با شمار واقعی قوانین/هشدارها/رویدادها
insert(f"{ROOT}/src/Platform.Web/Views/Notifications/Index.cshtml",
       '<div data-refresh-host>',
       '''@* فاز ۱۲۴-ج — هدر صفحه مطابق طرح Figma (شمار واقعی) *@
<div class="page-h">
    <div>
        <h1>@HrT.T("اعلان‌ها")</h1>
        <div class="page-sub">@HrT.F("{0} قانون اعلان · {1} هشدار باز · {2} رویداد اخیر", Jalali.Num(Model.Count), Jalali.Num(alerts.Count), Jalali.Num(logs.Count))</div>
    </div>
</div>
''')
