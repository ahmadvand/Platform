#!/usr/bin/env python3
# فاز ۱۲۴-د — درج هدر صفحه (page-h) در ۱۵ نمای باقی‌مانده، مطابق طرح Figma
# فقط درج؛ عنوان‌ها (ViewData) دست‌نخورده می‌مانند تا دیکشنری ترجمهٔ چندزبانه نشکند.
# زیرنویس‌ها فقط با دادهٔ واقعی در محدودهٔ همان ویو. لنگر باید دقیقاً یک‌بار یافت شود.
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

H1 = '<div class="page-h">\n    <div>\n        <h1>{}</h1>\n        <div class="page-sub">{}</div>\n    </div>\n</div>\n'

def hdr(title, sub):
    return '@* فاز ۱۲۴-د — هدر صفحه مطابق طرح Figma (زیرنویس با دادهٔ واقعی همین صفحه) *@\n' + H1.format(title, sub)

# ---------- D-۱: گردش و تردد ----------
insert(f"{ROOT}/src/modules/Platform.Modules.Parking/Views/Parking/Index.cshtml",
       '<div class="chips">',
       hdr('@HrT.T("کنترل پارکینگ")',
           '@HrT.F("{0} خودرو · {1} مجوز · {2} پارکینگ ثبت‌شده", Jalali.Num(vehicles.Count), Jalali.Num(permits.Count), Jalali.Num(lots.Count))'))

insert(f"{ROOT}/src/modules/Platform.Modules.Visitors/Views/Visitors/Index.cshtml",
       '<div data-refresh-host>',
       hdr('@HrT.T("ارباب رجوع")',
           '@HrT.F("{0} نفر داخل سایت · {1} رکورد مراجعه · {2} پیمانکار", Jalali.Num(inside), Jalali.Num(visitors.Count), Jalali.Num(contractors.Count))'))

insert(f"{ROOT}/src/modules/HrModules.Access/Views/Cards/Index.cshtml",
       '<div class="chips">',
       hdr('@HrT.T("کارت‌های شناسایی")',
           '@HrT.F("{0} کارت شناسایی ثبت‌شده · توزیع وضعیت بر اساس {1} نوع کارت", Jalali.Num(total), Jalali.Num(types.Length))'))

insert(f"{ROOT}/src/modules/HrModules.Access/Views/Access/Index.cshtml",
       '<div class="tabs">',
       hdr('@HrT.T("کنترل تردد")',
           '@HrT.F("{0} منطقه · {1} قانون دسترسی · {2} رویداد اخیر", Jalali.Num(zones.Count), Jalali.Num(perms.Count), Jalali.Num(events.Count))'))

# ---------- D-۲: بهره‌برداری ----------
insert(f"{ROOT}/src/modules/HrModules.Devices/Views/Devices/Index.cshtml",
       '<div class="chips">',
       hdr('@HrT.T("دستگاه‌های تردد")',
           '@HrT.F("{0} دستگاه ثبت‌شده · وضعیت اتصال و صف فرمان‌ها لحظه‌ای", Jalali.Num(devices.Count))'))

insert(f"{ROOT}/src/modules/HrModules.DeviceGateway/Views/DeviceGateway/Index.cshtml",
       '<div data-refresh-host>',
       hdr('@HrT.T("دروازهٔ دستگاه‌ها")',
           '@HrT.F("{0} دستگاه و {1} دوربین تحت دروازه", Jalali.Num(devices.Count), Jalali.Num(cams.Count))'))

insert(f"{ROOT}/src/Platform.Web/Views/Users/Index.cshtml",
       '<div class="grid two-col" style="grid-template-columns:1.5fr 1fr" data-refresh-host>',
       hdr('@HrT.T("کاربران و نقش‌ها")',
           '@HrT.F("{0} کاربر · {1} نقش دسترسی · مدیریت حساب‌ها و محدودهٔ اختیار", Jalali.Num(users.Count), Jalali.Num(roles.Count))'))

insert(f"{ROOT}/src/Platform.Web/Views/Audit/Index.cshtml",
       '<div class="card">',
       hdr('@HrT.T("ممیزی")',
           '@HrT.F("{0} رویداد ثبت‌شده · زنجیرهٔ ممیزی همهٔ کنش‌های حساس سامانه", Jalali.Num(logs.Count))'))

insert(f"{ROOT}/src/Platform.Web/Views/Reports/Index.cshtml",
       '}\n<div class="card">',
       hdr('@HrT.T("گزارش‌ساز و تحلیل")',
           '@HrT.F("روند حضور {0} ماه اخیر · شاخص {1} واحد سازمانی", Jalali.Num(trend.Count), Jalali.Num(depts.Count))'))

insert(f"{ROOT}/src/Platform.Web/Views/License/Index.cshtml",
       '<div class="lic-wrap">',
       hdr('@HrT.T("مجوز نصب")',
           '@T("وضعیت مجوز، سقف‌ها و حالت قفل") · @HrT.F("شناسهٔ دستگاه: {0}", machineId)'))

# ---------- D-۳: سامانه و منابع انسانی ----------
insert(f"{ROOT}/src/Platform.Web/Views/Settings/Index.cshtml",
       '<div class="chips">',
       hdr('@HrT.T("تنظیمات سامانه")',
           '@HrT.F("عمومی، ظاهر و پوسته، امنیت و پشتیبان‌گیری · {0} پوستهٔ آماده", Jalali.Num(presets.Length))'))

insert(f"{ROOT}/src/modules/HrModules.Org/Views/Org/Index.cshtml",
       '<div class="chips">',
       hdr('@HrT.T("چارت سازمانی")',
           '@HrT.F("{0} سمت سازمانی ثبت‌شده · ساختار واحدها و سمت‌ها", Jalali.Num(positions.Count))'))

insert(f"{ROOT}/src/modules/HrModules.Payroll/Views/Payroll/Index.cshtml",
       '<div data-refresh-host>',
       hdr('@HrT.T("حقوق و دستمزد")',
           '@HrT.F("{0} دورهٔ حقوق ثبت‌شده · محاسبه و تدوین لیست", Jalali.Num(runs.Count))'))

insert(f"{ROOT}/src/modules/HrModules.Attendance/Views/Requests/Index.cshtml",
       '<div data-refresh-host>',
       hdr('@HrT.T("درخواست‌ها")',
           '@HrT.T("ثبت و تأیید مرخصی، مأموریت و اجازه — با گیت تأیید جداگانه")'))

insert(f"{ROOT}/src/Platform.Web/Views/Self/Index.cshtml",
       '<div class="kpis">',
       hdr('@HrT.T("پرتال من")',
           '@HrT.T("خلاصهٔ حضور، مرخصی و کارکرد شخصی من")'))
