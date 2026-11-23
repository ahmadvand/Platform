# -*- coding: utf-8 -*-
# فاز ۹۶ — به‌روزرسانی مستندات: نسخهٔ 2.7.9 + ویزارد سه‌تبی + محافظ صفحه +
# جبران تقویم قمری + قالب برون‌بری تردد + عیب‌یابی Execution Policy.
import io, re

DOCS = "/home/z/my-project/scripts/docs"

def patch(path, pairs):
    s = io.open(path, encoding="utf-8").read()
    for old, new in pairs:
        if old not in s:
            raise SystemExit(f"NOT FOUND in {path}: {old[:90]}...")
        s = s.replace(old, new, 1)
    io.open(path, "w", encoding="utf-8").write(s)
    print("patched", path)

# ================= doc-setup.js =================
old_wiz = '''body.push(h1("۴. ویزارد راه‌اندازی اولیه (/Setup)", pal));
body.push(h2("گام ۱ — بانک داده و زبان", pal));
body.push(bullet("زبان رابط کاربری را انتخاب کنید: فارسی (راست‌به‌چپ و تاریخ شمسی) یا English."));
body.push(bullet("نوع بانک: SQL Server سازمانی یا SQLite فایلی. برای SQL Server فیلدهای آدرس سرور، پورت (پیش‌فرض 1433)، نام بانک (پیش‌فرض HrAttendance)، نوع احراز (کاربر SQL یا ویندوز)، نام کاربری و رمز را پر کنید؛ گزینه‌های ساخت خودکار بانک و SSL موجود است."));
body.push(bullet("برای SQLite فقط مسیر فایل بانک (پیش‌فرض hr.db) لازم است؛ برای سرویس ویندوزی مسیر مطلق توصیه می‌شود."));
body.push(bullet("گزینهٔ «داده‌های نمایشی» برای شروع سریع: پرسنل، دستگاه‌ها و پارکینگ نمونه می‌سازد (در محیط واقعی خاموش بگذارید)."));
body.push(bullet("دکمهٔ تست اتصال را بزنید و سپس ادامهٔ راه‌اندازی — اسکیما و دادهٔ پایه خودکار ساخته می‌شود."));
body.push(h2("گام ۲ — مجوز نصب", pal));'''
new_wiz = '''body.push(h1("۴. ویزارد راه‌اندازی اولیه (/Setup) — سه تب", pal));
body.push(p("ویزارد از فاز ۹۶ به «سه تب» تفکیک شده است: ۱) انتخاب زبان ۲) اطلاعات دیتابیس ۳) اطلاعات مجوز — همهٔ گزینه‌های قبلی حفظ شده‌اند و فقط بین تب‌ها جابجا شده‌اند تا ارتفاع هر صفحه کمتر و مرور گام‌ها واضح‌تر شود. با دکمه‌های «ادامه ←» و «→ بازگشت» بین تب‌ها حرکت کنید؛ تب‌های تکمیل‌شده سبز علامت می‌خورند و می‌توانید با کلیک روی خود تب هم به آن‌ها برگردید."));
body.push(h2("تب ۱ — انتخاب زبان", pal));
body.push(bullet("زبان رابط کاربری را انتخاب کنید: فارسی (راست‌به‌چپ و تاریخ شمسی) یا English — همین انتخاب به‌عنوان زبان پیش‌فرض این نصب ذخیره می‌شود و پس از نصب هم از نوار بالایی قابل تغییر است."));
body.push(h2("تب ۲ — اطلاعات دیتابیس", pal));
body.push(bullet("نوع بانک: SQL Server سازمانی یا SQLite فایلی. برای SQL Server فیلدهای آدرس سرور، پورت (پیش‌فرض 1433)، نام بانک، نوع احراز (کاربر SQL یا ویندوز)، نام کاربری و رمز را پر کنید؛ گزینه‌های ساخت خودکار بانک و SSL موجود است."));
body.push(bullet("برای SQLite فقط مسیر فایل بانک (پیش‌فرض hr.db) لازم است؛ برای سرویس ویندوزی مسیر مطلق توصیه می‌شود."));
body.push(bullet("گزینهٔ «داده‌های نمایشی» برای شروع سریع: پرسنل، دستگاه‌ها و پارکینگ نمونه می‌سازد (در محیط واقعی خاموش بگذارید)."));
body.push(bullet("دکمهٔ تست اتصال را بزنید و سپس «ادامه به مجوز» — ذخیرهٔ نهایی در تب سوم انجام می‌شود."));
body.push(h2("تب ۳ — اطلاعات مجوز", pal));'''
patch(f"{DOCS}/doc-setup.js", [(old_wiz, new_wiz)])

old_s10 = '''body.push(bullet("در تنظیمات سامانه، زبان پیش‌فرض، نوع تقویم (شمسی/میلادی/قمری) و واحد پول و نرخ نمایشی را تعیین کنید؛ همهٔ مبالغ وب و PDF و اکسل از همین تنظیم پیروی می‌کنند."));'''
new_s10 = '''body.push(bullet("در تنظیمات سامانه، زبان پیش‌فرض، نوع تقویم (شمسی/میلادی/قمری) و واحد پول و نرخ نمایشی را تعیین کنید؛ همهٔ مبالغ وب و PDF و اکسل از همین تنظیم پیروی می‌کنند."));
body.push(bullet("جبران روز تقویم قمری (فاز ۹۶): مبنای قمری سامانه ام‌القری است که معمولاً یک روز جلوتر از تقویم رسمی ایران/افغانستان است؛ با «۱ روز عقب‌تر» نمایش قمری، تاریخ‌ها، date picker و قالب برون‌بری با تقویم دیواری رسمی یکی می‌شوند (مثلاً ۱۵ شعبان دقیقاً روی روز درست تقویم می‌نشیند)."));
body.push(bullet("محافظ صفحه (باران ماتریکس) در تب خودش: روشن/خاموش، دقایق بی‌حرکتی (۱ تا ۱۲۰) و «بیدارسازی با رمز» — در حالت رمزدار، بازگشت به سامانه فقط با رمز همان کاربرِ واردشده ممکن است و هر تلاش در حسابرسی ثبت می‌شود."));'''
patch(f"{DOCS}/doc-setup.js", [(old_s10, new_s10)])

old_t12 = '''    ["429 در ورود", "محدودیت نرخ", "بعد از ۶۰ ثانیه تلاش مجدد؛ تنظیم Security:RateLimit"],
  ],
}));'''
new_t12 = '''    ["429 در ورود", "محدودیت نرخ", "بعد از ۶۰ ثانیه تلاش مجدد؛ تنظیم Security:RateLimit"],
    ["اجرای build-installer.ps1: «File ... is not digitally signed» (PSSecurityException)", "سیاست اجرای ویندوز روی Restricted است — فایل موجود است اما ویندوز اجازهٔ اجرا نمی‌دهد", "از installer\\\\build-installer.bat استفاده کنید (خودش PowerShell را با -ExecutionPolicy Bypass صدا می‌زند) یا: powershell -NoProfile -ExecutionPolicy Bypass -File installer\\\\build-installer.ps1 -Version 2.7.9 — یا Set-ExecutionPolicy -Scope Process Bypass در همان نشست؛ اگر فایل از zip آمده: Unblock-File"],
  ],
}));'''
patch(f"{DOCS}/doc-setup.js", [(old_t12, new_t12)])

patch(f"{DOCS}/doc-setup.js", [
    ('"سامانه: Platform.sln — نسخهٔ 2.7.6"', '"سامانه: Platform.sln — نسخهٔ 2.7.9"'),
    ('headerTitle: "راه‌اندازی گام‌به‌گام — سامانه منابع انسانی v2.7.6"', 'headerTitle: "راه‌اندازی گام‌به‌گام — سامانه منابع انسانی v2.7.9"'),
])

# ================= doc-user.js =================
old_u5 = '''body.push(h2("۵-۲: درخواست‌ها و تأییدها", pal));'''
new_u5 = '''body.push(h2("۵-۲: قالب برون‌بری تردد (تاریخچه)", pal));
body.push(p("صفحهٔ «قالب برون‌بری تردد» برای انتقال داده به نرم‌افزارهای دیگر است: برای هر مقصد یک قالب می‌سازید — ستون‌ها را (کد پرسنلی، نام، تاریخ تردد، ورود، خروج، کارکرد، دیرکرد، وضعیت و…) به دلخواه می‌چینید و برای هر ستون تاریخ/زمان قالب نمایشی انتخاب می‌کنید: لینوکسی (Linux timestamp بر حسب ثانیه)، شمسی، میلادی، قمری یا ISO؛ جداکنندهٔ فایل (کاما/نقطه‌ویرگول/تب/خط عمودی) و سطر سرستون هم اختیاری شماست. خروجی CSV با کدگذاری UTF-8 و سازگار با Excel است و بازهٔ خروجی (از/تا تاریخ) در لحظهٔ برون‌بری انتخاب می‌شود."));
body.push(h2("۵-۳: درخواست‌ها و تأییدها", pal));'''
patch(f"{DOCS}/doc-user.js", [(old_u5, new_u5)])

old_u12 = '''body.push(bullet("تنظیمات سامانه با شش تب: شرکت و لوگو؛ زبان و تقویم و ارز؛ ظاهر و پالت رنگی؛ پایگاه داده (وضعیت زندهٔ بانک و تغییر اتصال با تست اتصال و بدون ری‌استارت)؛ ماژول‌ها (روشن و خاموش کردن ماژول‌های الحاقی)؛ دربارهٔ سامانه."));'''
new_u12 = '''body.push(bullet("تنظیمات سامانه با هفت تب: شرکت و لوگو؛ زبان و تقویم و ارز (به‌همراه «جبران روز تقویم قمری» برای تطبیق نمایش قمری با تقویم رسمی کشور)؛ محافظ صفحه (باران ماتریکس: روشن/خاموش، دقایق بی‌حرکتی، بیدارسازی با رمز)؛ ظاهر و پالت رنگی؛ پایگاه داده (وضعیت زندهٔ بانک و تغییر اتصال با تست اتصال و بدون ری‌استارت)؛ ماژول‌ها (روشن و خاموش کردن ماژول‌های الحاقی)؛ دربارهٔ سامانه."));
body.push(bullet("محافظ صفحه: پس از بی‌حرکتیِ تعیین‌شده افکت باران ماتریکس تمام‌صفحه با ساعت و تاریخ روشن می‌شود؛ با اولین حرکت ماوس یا کلید (در حالت رمزدار، پس از ورود رمز همان کاربر) به حالت عادی برمی‌گردید. مدت بی‌حرکتی نمایش‌داده‌شده روی محافظ، ملاک صحت محاسبهٔ زمان است."));'''
patch(f"{DOCS}/doc-user.js", [(old_u12, new_u12)])

patch(f"{DOCS}/doc-user.js", [
    ('"سامانه: Platform.sln — نسخهٔ 2.7.6 — رابط فارسی RTL"', '"سامانه: Platform.sln — نسخهٔ 2.7.9 — رابط فارسی RTL"'),
    ('headerTitle: "راهنمای کاربر — سامانه یکپارچه منابع انسانی v2.7.6"', 'headerTitle: "راهنمای کاربر — سامانه یکپارچه منابع انسانی v2.7.9"'),
])

# ================= doc-handoff.js =================
patch(f"{DOCS}/doc-handoff.js", [
    ('"جدول ۱ — مشخصات فنی سامانه (نسخهٔ 2.7.6)"', '"جدول ۱ — مشخصات فنی سامانه (نسخهٔ 2.7.9)"'),
    ('"سامانه: Platform.sln — نسخهٔ 2.7.6"', '"سامانه: Platform.sln — نسخهٔ 2.7.9"'),
    ('headerTitle: "سند تحویل به هوش مصنوعی توسعه‌دهنده — سامانه منابع انسانی v2.7.6"', 'headerTitle: "سند تحویل به هوش مصنوعی توسعه‌دهنده — سامانه منابع انسانی v2.7.9"'),
])

old_h10 = '''body.push(bullet("مبالغ همیشه با Money.Fmt و تاریخ‌ها همیشه با Jalali — ذخیرهٔ بانک میلادی/epoch است و فقط نمایش تغییر می‌کند."));'''
new_h10 = '''body.push(bullet("مبالغ همیشه با Money.Fmt و تاریخ‌ها همیشه با Jalali — ذخیرهٔ بانک میلادی/epoch است و فقط نمایش تغییر می‌کند."));
body.push(bullet([H.t("فاز ۹۶ — تقویم قمری: مبنا "), H.t("UmAlQuraCalendar", { rtl: false }), H.t(" است؛ چون ام‌القری معمولاً یک روز جلوتر از تقویم رسمی ایران/افغانستان است، تنظیم "), H.t("AppSettings.HijriAdjust (پیش‌فرض -1)", { rtl: false }), H.t(" در CultureMiddleware روی "), H.t("Jalali.HijriAdjustDays", { rtl: false }), H.t(" تزریق می‌شود و در Fmt/FmtFa (نمایش)، "), H.t("Jalali.FmtHijri/ToHijri (قالب برون‌بری)", { rtl: false }), H.t(" و سمت مرورگر با اتریبیوت "), H.t("data-hijri روی <html>", { rtl: false }), H.t(" برای پیکر تاریخ (Intl islamic-umalqura) اعمال می‌شود — فقط نمایش؛ داده‌ها دست‌نخورده.")]));
body.push(bullet([H.t("فاز ۹۶ — محافظ صفحه: js/hr-screensaver.js با پیکربندی از تنظیمات (AppSettings.ScreenSaverEnabled/Minutes/RequirePassword) در _Layout؛ بیدارسازی رمزدار با POST "), H.t("/Auth/VerifyWakePassword", { rtl: false }), H.t(" (PBKDF2 + محدودسازی ۵ خطا/۵ دقیقه + حسابرسی). ملاک زمان، اختلاف واقعی Date.now() از آخرین رخداد ورودیِ isTrusted است — نه شمارش تیک interval.")]));
body.push(bullet([H.t("فاز ۹۶ — برون‌بری قالب‌محور تردد: موجودیت "), H.t("AttendanceExportTemplate", { rtl: false }), H.t(" (ستون‌ها JSON + جداکننده + سرستون) در ماژول attendance با endpointهای "), H.t("/Attendance/Exports، /Attendance/SaveTemplate، /Attendance/DeleteTemplate، /Attendance/ExportByTemplate", { rtl: false }), H.t(" — قالب تاریخ: iso/linux/jalali/gregorian/hijri و قالب زمان: hhmm/hhmmss/linux/fullها؛ خروجی CSV با UTF-8 BOM.")]));
body.push(bullet("فاز ۹۶ — ویزارد راه‌اندازی سه‌تبی شد (زبان / دیتابیس / مجوز) — فقط جابجایی پنل‌ها؛ هیچ اکشن سروری تغییر نکرده است."));'''
patch(f"{DOCS}/doc-handoff.js", [(old_h10, new_h10)])

old_h8 = '''body.push(h1("۹. اجرا، تست و عیب‌یابی محلی", pal));'''
new_h8 = '''body.push(bullet([H.t("ساخت نصب‌کنندهٔ ویندوزی: همیشه از "), H.t("installer\\\\build-installer.bat", { rtl: false }), H.t(" استفاده کنید — اگر ps1 را مستقیم اجرا کنید و ویندوز خطای «File ... is not digitally signed / PSSecurityException / UnauthorizedAccess» داد، علت Execution Policy است؛ راه‌حل‌ها در installer/README-FA.md: bat با Bypass، یا powershell -ExecutionPolicy Bypass -File، یا Set-ExecutionPolicy -Scope Process Bypass، یا Unblock-File.")]));
body.push(h1("۹. اجرا، تست و عیب‌یابی محلی", pal));'''
patch(f"{DOCS}/doc-handoff.js", [(old_h8, new_h8)])

# ================= doc-arch.js =================
patch(f"{DOCS}/doc-arch.js", [
    ('"سامانه: Platform.sln — نسخهٔ 2.7.6 — ASP.NET Core 8 + EF Core"', '"سامانه: Platform.sln — نسخهٔ 2.7.9 — ASP.NET Core 8 + EF Core"'),
    ('headerTitle: "مستندات معماری نرم‌افزار — سامانه منابع انسانی v2.7.6"', 'headerTitle: "مستندات معماری نرم‌افزار — سامانه منابع انسانی v2.7.9"'),
])

old_a5 = '''body.push(h1("۶. امنیت", pal));'''
new_a5 = '''body.push(p("جداول تنظیمات و ابزارهای نمایشی: AppSettings ستون‌های HijriAdjust و ScreenSaverEnabled/Minutes/RequirePassword (فاز ۹۶) را دارد — ستون‌های جدید با وصال عمومی خودترمیم DbInit روی پایگاه‌های موجود ساخته می‌شوند؛ AttendanceExportTemplates جدول قالب‌های برون‌بری تردد در اسکیمای ماژول attendance است."));
body.push(h1("۶. امنیت", pal));'''
patch(f"{DOCS}/doc-arch.js", [(old_a5, new_a5)])

# ================= handoff.md =================
s = io.open("/home/z/my-project/handoff.md", encoding="utf-8").read()
marker = "## ۱۴) نقشهٔ مسیرهای کلیدی (مرجع سریع)"
p96 = """## ۱۳.۵) فاز ۹۶ — قابلیت‌های تازه (نسخهٔ 2.7.9)

- **ویزارد سه‌تبی راه‌اندازی**: `/Setup` اکنون سه تب دارد — ۱) انتخاب زبان ۲) اطلاعات دیتابیس ۳) اطلاعات مجوز؛ فقط جابجایی پنل‌ها، هیچ اکشن سروری تغییر نکرده.
- **جبران روز تقویم قمری**: `AppSettings.HijriAdjust` (پیش‌فرض ‎-۱ = رسمی ایران/افغانستان؛ ام‌القری یک روز جلوتر است) → `Jalali.HijriAdjustDays` در `CultureMiddleware` + اتریبیوت `data-hijri` روی `<html>` برای پیکر تاریخ JS — نمایش قمری/پیکر/قالب برون‌بری همه هم‌سو می‌شوند (مثلاً ۱۵ شعبان دقیقاً روی روز رسمی).
- **محافظ صفحه (باران ماتریکس)**: `js/hr-screensaver.js` + تنظیمات `ScreenSaverEnabled/ScreenSaverMinutes/ScreenSaverRequirePassword`؛ بیدارسازی رمزدار با `POST /Auth/VerifyWakePassword` (PBKDF2، محدودسازی ۵ خطا/۵ دقیقه، حسابرسی `screensaver_unlock*`). محاسبهٔ زمان با اختلاف واقعی `Date.now()` از آخرین رخداد `isTrusted` — نه شمارش تیک interval.
- **قالب برون‌بری رکوردهای تردد (تاریخچه)**: `AttendanceExportTemplate` در ماژول attendance؛ صفحهٔ `/Attendance/Exports`؛ قالب تاریخ `iso|linux|jalali|gregorian|hijri` و قالب زمان `hhmm|hhmmss|linux|jalali|gregorian|hijri`؛ خروجی CSV با UTF-8 BOM + جداکنندهٔ انتخابی + سرستون سفارشی.
- **رفع خطای سازندهٔ نصب ویندوزی**: «is not digitally signed» = Execution Policy؛ `installer/build-installer.bat` خودش با `-ExecutionPolicy Bypass` اجرا می‌کند — راه‌حل‌های دیگر در `installer/README-FA.md`.
- **i18n**: کلیدهای فاز ۹۶ در `HrLang.DictEn` + همهٔ بسته‌های `App_Data/langpacks/hr-lang-*.json` (ar/ka/ky/prs/ps/tg/tk/uz) همگام شده‌اند.

"""
if p96 not in s:
    s = s.replace(marker, p96 + marker, 1)
    io.open("/home/z/my-project/handoff.md", "w", encoding="utf-8").write(s)
    print("patched handoff.md")
else:
    print("handoff.md already patched")

print("ALL DOCS PATCHED")
