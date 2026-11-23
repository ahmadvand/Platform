# -*- coding: utf-8 -*-
# فاز ۹۶ — افزودن کلیدهای جدید (محافظ صفحه / جبران قمری / قالب برون‌بری تردد / ویزارد سه‌تبی)
# به DictEn در HrLang.cs — همان قالب ردیف‌های موجود؛ بدون دست‌زدن به کلیدهای فعلی.
import re, io

P = "/home/z/my-project/aspnet/src/Platform.Core/Services/HrLang.cs"

NEW = {
    # ---- محافظ صفحه (اسکرین‌سیور باران ماتریکس + بیدارسازی با رمز) ----
    "محافظ صفحه": "Screen saver",
    "محافظ صفحه (باران ماتریکس)": "Screen saver (matrix rain)",
    "وضعیت محافظ صفحه": "Screen saver state",
    "روشن — پس از بی‌حرکتی اجرا شود": "On — runs after idle time",
    "خاموش — هرگز اجرا نشود": "Off — never runs",
    "دقایق بی\u200cحرکتی تا اجرا (۱ تا ۱۲۰)": "Idle minutes before start (1–120)",
    "بیدارسازی با رمز (درخواست کاربر)": "Wake up with password",
    "بله — بازگشت به حالت عادی فقط با ورود رمز کاربر": "Yes — returning to normal requires the user's password",
    "خیر — با اولین حرکت بدون رمز جمع شود": "No — collapses on first movement without password",
    "ذخیرهٔ تنظیمات محافظ صفحه": "Save screen saver settings",
    "پس از بی\u200cحرکتیِ تعیین\u200cشده، افکت باران ماتریکس تمام\u200cصفحه روشن می\u200cشود و ساعت جاری را نشان می\u200cدهد. با اولین حرکت ماوس یا فشردن کلید، محافظ بیدار می\u200cشود — در حالت «ورود رمز»، بیدارسازی فقط با رمز همان کاربرِ واردشده ممکن است.":
        "After the configured idle time, a full-screen matrix-rain effect starts and shows the current clock. The first mouse move or key press wakes it up — in \u201crequire password\u201d mode, waking up is only possible with the current signed-in user's password.",
    "نکتهٔ امنیتی: محافظ صفحه یک قفل نمایشی است — پس از بیدارسازی، نشست همان کاربر ادامه می\u200cیابد و رمز فقط هویت همان کاربر دوباره تأیید می\u200cشود؛ برای قفل کامل، کاربر را از سامانه خارج کنید.":
        "Security note: the screen saver is a display lock — after waking up, the same session continues and the password only re-verifies that user's identity; for a full lock, sign the user out.",
    "محافظ صفحه فعال است": "Screen saver is active",
    "برای بازگشت به سامانه، ماوس را حرکت دهید یا کلیدی بزنید": "Move the mouse or press a key to return",
    "برای بازگشت به سامانه، رمز کاربر جاری را وارد کنید": "Enter the current user's password to return",
    "تأیید و بازگشت": "Confirm & return",
    "مدت بی\u200cحرکتی": "Idle for",
    "رمز تأیید شد": "Password verified",
    "رمز نادرست است — دوباره تلاش کنید": "Wrong password — try again",
    "نشست یافت نشد — دوباره وارد شوید": "Session not found — please sign in again",
    "کاربر یافت نشد — دوباره وارد شوید": "User not found — please sign in again",
    "نشست منقضی شده است — در حال انتقال به صفحهٔ ورود…": "Session expired — redirecting to the login page…",
    "تلاش\u200cهای ناموفق زیاد است — {0} ثانیه دیگر دوباره تلاش کنید": "Too many failed attempts — try again in {0} seconds",
    "ذخیرهٔ زبان، تقویم و ارز": "Save language, calendar & currency",
    # ---- جبران تقویم قمری ----
    "جبران روز تقویم قمری": "Hijri calendar day offset",
    "تطبیق نمایش قمری با تقویم رسمی کشور": "Align lunar display with the country's official calendar",
    "۲ روز جلوتر": "2 days ahead",
    "۱ روز جلوتر": "1 day ahead",
    "بدون جبران — دقیقاً ام\u200cالقری (عربستان)": "No offset — exactly Umm al-Qura (Saudi)",
    "۱ روز عقب\u200cتر — رسمی ایران/افغانستان (پیشنهادی)": "1 day behind — official Iran/Afghanistan (recommended)",
    "۲ روز عقب\u200cتر": "2 days behind",
    "مبنای قمریِ سامانه تقویم «ام\u200cالقری» است که برای سال\u200cهای جاری معمولاً یک روز جلوتر از تقویم قمری رسمی ایران/افغانستان (رؤیت هلال محلی) است؛ مثلاً ۱۵ شعبان در نمایش بدون جبران یک روز عقب\u200cتر از تقویم دیواری دیده می\u200cشود. با «۱ روز عقب\u200cتر» نمایش قمری، date picker و قالب برون\u200cبری همه با تقویم رسمی یکی می\u200cشوند. فقط نمایش عوض می\u200cشود؛ داده\u200cها دست\u200cنخورده می\u200cمانند.":
        "The system's lunar base is the Umm al-Qura calendar, which for current years usually runs one day ahead of the official lunar calendar of Iran/Afghanistan (local moon sighting); e.g. 15 Sha'ban appears one day behind the wall calendar when no offset is set. With \u201c1 day behind\u201d, the lunar display, the date picker and the export template all match the official calendar. Only the display changes; stored data stays untouched.",
    # ---- قالب برون‌بری تردد ----
    "قالب برون\u200cبری تردد": "Traffic export template",
    "قالب\u200cهای ذخیره\u200cشده": "Saved templates",
    "هنوز قالبی ساخته نشده — از ویرایشگر کنار، قالب اول را بسازید": "No template yet — build your first one with the editor on the side",
    "برون\u200cبری تاریخچهٔ تردد با قالب": "Export traffic history with a template",
    "قالب *": "Template *",
    "انتخاب قالب": "Select template",
    "از تاریخ (میلادی ISO)": "From date (Gregorian ISO)",
    "تا تاریخ (میلادی ISO)": "To date (Gregorian ISO)",
    "خروجی CSV با کدگذاری UTF-8 (سازگار با Excel) طبق ستون\u200cها و قالب تاریخ/زمانِ قالب انتخابی — شامل قالب لینوکسی (Linux timestamp)، شمسی، میلادی و قمری. بازه پیش\u200cفرض ۳۰ روز اخیر است.":
        "CSV output in UTF-8 (Excel-friendly) using the selected template's columns and date/time formats — including Linux timestamp, Jalali, Gregorian and Hijri. Default range: last 30 days.",
    "دریافت فایل برون\u200cبری": "Download export file",
    "قالب جدید": "New template",
    "ویرایش قالب: {0}": "Edit template: {0}",
    "نام قالب *": "Template name *",
    "مثلاً قالب نرم\u200cافزار انبار": "e.g. Warehouse software template",
    "یادداشت (اختیاری)": "Note (optional)",
    "مقصد یا توضیح قالب": "Destination or description",
    "جداکنندهٔ فایل": "File delimiter",
    " (کاما)": " (comma)",
    " (نقطه\u200cویرگول)": " (semicolon)",
    "تب (Tab)": "Tab",
    "سطر سرستون": "Header row",
    "داشته باشد": "Include",
    "بدون سرستون": "No header row",
    "ستون\u200cهای خروجی": "Output columns",
    "برای هر ستون تاریخ/زمان، قالب نمایشی را انتخاب کنید: لینوکسی (Linux timestamp)، شمسی، میلادی، قمری یا ISO — چون نرم\u200cافزارهای مختلف فرمت\u200cهای مختلفی می\u200cخواهند. متن سرستون را هم می\u200cتوانید سفارشی کنید.":
        "For each date/time column choose the display format: Linux timestamp, Jalali, Gregorian, Hijri or ISO — different software expects different formats. The header text is customizable too.",
    "فیلد": "Field",
    "قالب تاریخ/زمان": "Date/time format",
    "سرستون سفارشی": "Custom header",
    "حذف ستون": "Remove column",
    "افزودن ستون": "Add column",
    "ذخیرهٔ قالب": "Save template",
    "نام قالب الزامی است": "Template name is required",
    "حداقل یک ستون لازم است": "At least one column is required",
    "قالب «{0}» ذخیره شد": "Template \u201c{0}\u201d saved",
    "قالب ذخیره شد": "Template saved",
    "قالب ذخیره نشد: {0}": "Template not saved: {0}",
    "قالب حذف شد": "Template deleted",
    "قالب یافت نشد": "Template not found",
    "قالب معتبر نیست: {0}": "Template is invalid: {0}",
    "{0} ستون": "{0} columns",
    "ابتدا قالب را انتخاب کنید": "Select a template first",
    "ساختار ستون\u200cها معتبر نیست": "Columns structure is invalid",
    "فیلد ناشناخته در ستون\u200cها: ": "Unknown field in columns: ",
    "بیشتر از ۲۰ ستون مجاز نیست": "More than 20 columns is not allowed",
    # فیلدهای قالب
    "تاریخ تردد": "Traffic date",
    "زمان خروج": "Check-out time",
    "دیرکرد (دقیقه)": "Late (minutes)",
    "تعجیل (دقیقه)": "Early leave (minutes)",
    # قالب‌های تاریخ/زمان
    "ISO میلادی — 2026-01-24": "Gregorian ISO — 2026-01-24",
    "لینوکسی (Linux timestamp — ثانیه)": "Linux (Linux timestamp — seconds)",
    "شمسی (جلالی) — 1404/11/04": "Jalali (Solar Hijri) — 1404/11/04",
    "میلادی — 2026/01/24": "Gregorian — 2026/01/24",
    "قمری (هجری قمری) — 1447/08/05": "Hijri (Lunar) — 1447/08/05",
    "فقط ساعت — 08:03": "Time only — 08:03",
    "ساعت با ثانیه — 08:03:22": "Time with seconds — 08:03:22",
    "شمسی کامل — 1404/11/04 08:03:22": "Jalali full — 1404/11/04 08:03:22",
    "میلادی کامل — 2026/01/24 08:03:22": "Gregorian full — 2026/01/24 08:03:22",
    "قمری کامل — 1447/08/05 08:03:22": "Hijri full — 1447/08/05 08:03:22",
    # ---- ویزارد سه‌تبی ----
    "انتخاب زبان": "Language selection",
    "زبان رابط کاربری نصب": "Installation UI language",
    "اطلاعات دیتابیس": "Database information",
    "نوع بانک و اتصال": "Database type & connection",
    "اطلاعات مجوز": "License information",
    "کلید امضا و فایل مجوز": "Signing key & license file",
    "ادامه به بانک داده ←": "Continue to database \u2190",
    "ادامه به مجوز ←": "Continue to license \u2190",
    "بازگشت به زبان": "\u2192 Back to language",
}

s = io.open(P, encoding="utf-8").read()
added, dup = 0, 0
# نقطهٔ درج: انتهای دیکشنری DictEn (آخرین ردیف ["…"] = "…";) — پیدا کردن آخرین بلاک DictEn
marker = "        [\"ذخیرهٔ زبان، تقویم و ارز\"] = "
idx = s.find(marker)
if idx == -1:
    raise SystemExit("marker not found")
end = s.find("\n", idx)
lines = "".join('        ["%s"] = "%s",\n' % (k.replace('"', '\\"'), v.replace('"', '\\"')) for k, v in NEW.items())
s = s[:end + 1] + lines + s[end + 1:]
io.open(P, "w", encoding="utf-8").write(s)
print("added", len(NEW), "keys")
