#!/usr/bin/env python3
# فاز ۱۲۴-ب — هدر داشبورد و اعلان‌ها + بنر وضعیت مطابق طرح Figma
# زیرنویس‌ها و بنرها فقط با دادهٔ واقعی در محدودهٔ همان ویو؛ لنگر باید یگانه باشد.
import io, sys

ROOT = "/home/z/my-project/aspnet"

def insert(path, anchor, block):
    with io.open(path, encoding="utf-8") as f:
        src = f.read()
    if "page-h" in src:
        print(f"SKIP (هدر موجود): {path}")
        return
    n = src.count(anchor)
    if n != 1:
        print(f"ABORT — لنگر یگانه نیست ({n}x): {path}")
        sys.exit(1)
    out = src.replace(anchor, block + anchor, 1)
    with io.open(path, "w", encoding="utf-8") as f:
        f.write(out)
    print(f"OK: {path}")

# ---------- داشبورد ----------
insert(f"{ROOT}/src/Platform.Web/Views/Home/Index.cshtml",
       '<div class="ps-grid">',
       '''@* فاز ۱۲۴-ب — هدر داشبورد و بنر وضعیت مطابق طرح Figma (دادهٔ واقعی همین صفحه) *@
<div class="page-h">
    <div>
        <h1>@T("داشبورد")</h1>
        <div class="page-sub">@HrT.F("{0} دستگاه از {1} آنلاین · {2} هشدار امنیتی اخیر", Jalali.Num(devOnline), Jalali.Num(devList.Count), Jalali.Num(recentAlerts.Count))</div>
    </div>
</div>
@if (recentAlerts.Count > 0)
{
    <div class="notice notice-bad"><span class="n-ic">🛡️</span><span>@HrT.F("{0} هشدار امنیتی اخیر ثبت شده است؛ پیگیری در صفحهٔ «کنترل تردد».", Jalali.Num(recentAlerts.Count))</span></div>
}
else
{
    <div class="notice notice-ok"><span class="n-ic">✅</span><span>@T("سامانه در وضعیت عادی است؛ هشدار امنیتی اخیری ثبت نشده است.")</span></div>
}
''')

# ---------- اعلان‌ها ----------
insert(f"{ROOT}/src/Platform.Web/Views/Notifications/Index.cshtml",
       '<div data-refresh-host>',
       '''@* فاز ۱۲۴-ب — هدر اعلان‌ها و بنر توضیح مطابق طرح Figma *@
<div class="page-h">
    <div>
        <h1>@HrT.T("اعلان‌ها")</h1>
        <div class="page-sub">@HrT.F("{0} قاعدهٔ اعلان · {1} رویداد اعلام‌شدهٔ اخیر", Jalali.Num(Model.Count), Jalali.Num(logs.Count))</div>
    </div>
</div>
<div class="notice notice-ok"><span class="n-ic">🔔</span><span>@HrT.T("قواعد اعلان، رویدادهای حساس سامانه را به کانال‌های تعریف‌شده اعلام می‌کنند؛ مجوزهای ارسال در همین صفحه تنظیم می‌شود.")</span></div>
''')
