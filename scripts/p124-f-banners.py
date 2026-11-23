#!/usr/bin/env python3
# فاز ۱۲۴-ف — بنرهای notice صفحات «دستگاه‌ها» و «شیفت‌ها» مطابق طرح Figma
# دادهٔ بنر کاملاً واقعی است: صفحهٔ دستگاه‌ها = شمار آفلاین (devices.Count - online)؛
# صفحهٔ شیفت‌ها = شمار «بدون شیفت» (noShiftCount). بعد از اسکریپت فاز D اجرا شود.
import io, sys

ROOT = "/home/z/my-project/aspnet"

def insert(path, anchor, block):
    with io.open(path, encoding="utf-8") as f:
        src = f.read()
    if "n-ic" in src:
        print(f"SKIP (بنر موجود): {path}")
        return
    n = src.count(anchor)
    if n != 1:
        print(f"ABORT — لنگر یگانه نیست ({n}x): {path}")
        sys.exit(1)
    out = src.replace(anchor, block + anchor, 1)
    with io.open(path, "w", encoding="utf-8") as f:
        f.write(out)
    print(f"OK: {path}")

# ---------- دستگاه‌ها: بنر وضعیت اتصال (قرمز اگر آفلاین هست، سبز اگر همه متصل‌اند) ----------
insert(f"{ROOT}/src/modules/HrModules.Devices/Views/Devices/Index.cshtml",
       '<div class="chips">',
       '''@* فاز ۱۲۴-ف — بنر وضعیت اتصال دستگاه‌ها مطابق طرح Figma (دادهٔ واقعی) *@
@if (devices.Count > 0 && devices.Count - online > 0)
{
    <div class="notice notice-bad"><span class="n-ic">📡</span><span>@HrT.F("{0} دستگاه از {1} دستگاه در وضعیت آفلاین است؛ وضعیت اتصال را از «دروازهٔ دستگاه‌ها» بررسی کنید.", Jalali.Num(devices.Count - online), Jalali.Num(devices.Count))</span></div>
}
else if (devices.Count > 0)
{
    <div class="notice notice-ok"><span class="n-ic">📡</span><span>@HrT.F("همهٔ {0} دستگاه ثبت‌شده در وضعیت متصل است.", Jalali.Num(devices.Count))</span></div>
}
''')

# ---------- شیفت‌ها: بنر اطلاع تخصیص (زرد اگر پرسنل بدون شیفت هست، سبز اگر کامل است) ----------
insert(f"{ROOT}/src/modules/HrModules.Attendance/Views/Shifts/Index.cshtml",
       '<div class="chips">',
       '''@* فاز ۱۲۴-ف — بنر وضعیت تخصیص شیفت مطابق طرح Figma (دادهٔ واقعی) *@
@if (noShiftCount > 0)
{
    <div class="notice notice-warn"><span class="n-ic">🕒</span><span>@HrT.F("{0} پرسنل بدون شیفت ثبت شده است؛ تخصیص شیفت از همین صفحه انجام می‌شود و بر رویدادهای حضور اثر می‌گذارد.", Jalali.Num(noShiftCount))</span></div>
}
else
{
    <div class="notice notice-ok"><span class="n-ic">🕒</span><span>@HrT.T("تخصیص شیفت‌ها کامل است؛ هیچ پرسنلی بدون شیفت نمانده است.")</span></div>
}
''')
