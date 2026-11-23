# -*- coding: utf-8 -*-
# فاز ۹۲ — بخش ۲: دیکشنری JS (HrL10n.cs) + تزریق در _Layout + ترجمه‌های انگلیسی جدید در HrLang.DictEn
import os, re, json

SRC = "/home/z/my-project/aspnet/src"

# ---------- ۱) استخراج کلیدهای JS از فراخوانی‌های hrT/hrF ----------
def js_keys(path):
    s = open(path, encoding='utf-8').read()
    keys = set()
    for m in re.finditer(r"\bhr[TF]\(\s*('((?:[^'\\\n]|\\.)*)'|\"((?:[^\"\\\n]|\\.)*)\")", s):
        raw = m.group(2) if m.group(2) is not None else m.group(3)
        k = raw.replace("\\'", "'").replace('\\"', '"').replace('\\\\', '\\')
        keys.add(k)
    return keys

KS = js_keys(os.path.join(SRC, 'Platform.Web/wwwroot/js/site.js')) | \
     js_keys(os.path.join(SRC, 'Platform.Web/wwwroot/js/formula-builder.js'))
print('JS keys extracted:', len(KS))

# ---------- ۲) ترجمهٔ انگلیسی همهٔ کلیدهای جدید (نماها + JS) ----------
EN = {
 # ---- وضعیت‌ها / گیت‌ها / حراست / پارکینگ ----
 "باز دائم": "Open (forced)", "قفل دائم": "Locked (forced)", "نرمال": "Normal", "خراب": "Faulty",
 "فوق‌حساس": "Ultra-sensitive", "حساس": "Sensitive",
 "ورود": "In", "خروج": "Out", "ورود و خروج": "In/Out", "دوطرفه": "Two-way",
 "بحرانی": "Critical", "بالا": "High", "متوسط": "Medium", "پایین": "Low",
 # ---- بیومتریک / دستگاه‌ها ----
 "آخرین دریافت:": "Last fetch:",
 "شامل قالب‌های هویتی (اثر انگشت و چهره)": "Include biometric templates (fingerprint & face)",
 "کاربر": "user(s)",
 "✎ ویرایش": "✎ Edit", "پرسنل *": "Employees *",
 "افزودن به (مقصدها)": "Add to (targets)", "حذف از (مبدأها)": "Remove from (sources)",
 "بازکردن درب": "Unlock door", "همگام‌سازی ساعت": "Sync time", "ری‌استارت دستگاه": "Restart device",
 "ارسال کاربر به دستگاه": "Push user to device", "حذف کاربر از دستگاه": "Delete user from device",
 "دریافت اطلاعات کاربر": "Fetch user info", "دریافت فهرست کاربران": "Fetch user list",
 "دریافت اطلاعات هویتی از دستگاه": "Fetch biometrics from device",
 "ارسال کاربر + قالب‌های هویتی به دستگاه": "Push user + biometric templates to device",
 "به‌روزرسانی داده‌ها": "Refresh data", "پاک‌سازی داده‌ها": "Clear data",
 "پاک‌سازی رکوردهای تردد": "Clear attendance records",
 "فعال‌سازی دستگاه": "Enable device", "غیرفعال‌سازی دستگاه": "Disable device",
 "اثر انگشت + کارت + رمز": "Fingerprint + card + PIN", "چهره + کارت": "Face + card",
 "اثر انگشت + چهره + کارت": "Fingerprint + face + card",
 "اثر انگشت/چهره + کارت + کنترل درب": "Fingerprint/face + card + door control",
 "فقط کارت (بدون بایومتریک)": "Card only (no biometrics)",
 "ترمینال اثر انگشت": "Fingerprint terminal", "ترمینال چهره": "Face terminal",
 "ترمینال چندبیومتریک": "Multi-biometric terminal", "کنترلر دسترسی": "Access controller",
 "ترمینال کارتی": "Card terminal",
 # ---- مرخصی‌ها ----
 "استحقاقی": "Annual leave", "استعلاجی": "Sick leave", "ساعتی": "Hourly",
 "بدون‌حقوق": "Unpaid", "بدون حقوق": "Unpaid", "زایمان": "Maternity",
 # ---- پالت رنگ ----
 "برند و کنش‌ها": "Brand & actions", "رنگ اصلی *": "Primary color *", "لینک‌ها": "Links",
 "منوی کناری": "Sidebar", "پس‌زمینه منو *": "Sidebar background *", "متن منو": "Menu text",
 "زمینه و کارت‌ها": "Page & cards", "پس‌زمینه صفحه *": "Page background *", "کارت‌ها/فرم‌ها": "Cards/forms",
 "جدول — سطر و ستون": "Table — rows & header", "سربرگ جدول (ستون‌ها) *": "Table header *",
 "سطر یک‌درمیان": "Alt rows", "سطر هاور": "Hover row", "خط و متن": "Borders & text",
 "حاشیه‌ها": "Borders", "متن اصلی *": "Main text *", "متن ثانویه": "Secondary text",
 "وضعیت‌ها": "Status colors", "موفق": "Success", "هشدار": "Warning", "خطا": "Error", "اطلاع": "Info",
 "پیش‌نمایش زنده": "Live preview", "نام پالت": "Palette name",
 "مثلاً آبی سازمانی شعبه تهران": "e.g. corporate blue — Tehran branch",
 "الزامی": "Required", "خالی = از رنگ‌های پایه مشتق می‌شود": "empty = derived from base colors",
 # ---- اعلان‌ها ----
 "مورد": "item(s)", "شدت": "Severity", "نوع": "Type", "پیام": "Message", "منبع": "Source", "زمان": "Time",
 "رفتن به صفحهٔ مربوطه ←": "Go to related page ←",
 "برای علامت‌گذاری به‌عنوان خوانده‌شده، همان هشدار را از زنگ نوار بالایی باز کنید یا از صفحهٔ کنترل تردد آن را حل کنید.":
   "To mark as read, open the alert from the top-bar bell or resolve it on the access control page.",
 # ---- تنظیمات ----
 "نام شرکت شما": "Your Company Name",
 "مثال: دقیقه_اضافه_کاری / 60 * ساعتی * 1.4": "Example: overtime_minutes / 60 * hourly * 1.4",
 # ---- JS: دیالوگ‌ها ----
 "تأیید": "Confirm", "این عملیات حساس است": "This operation is sensitive", "تأیید عملیات": "Confirm action",
 "بله، انجام بده": "Yes, do it", "انصراف": "Cancel", "این واحد": "this unit", "مقصد": "destination",
 "محل قبلی": "previous location", "جابجایی همراه پرسنل": "Move with employees",
 "جابجایی بدون پرسنل": "Move without employees", "جابجایی واحد سازمانی": "Move organizational unit",
 "واحد «{0}» به «{1}» منتقل شود؟\nپرسنل مستقیم این واحد: {2} نفر\n": "Move unit «{0}» to «{1}»?\nDirect employees of this unit: {2}\n",
 "در حالت «بدون پرسنل»، این {0} نفر در «{1}» باقی می‌مانند.": "In «without employees» mode, these {0} employees remain in «{1}».",
 "این واحد پرسنل مستقیمی ندارد؛ زیرمجموعه‌ها و پرسنل آن‌ها همراه واحد می‌آیند.":
   "This unit has no direct employees; its sub-units and staff move along with it.",
 # ---- JS: اعتبارسنجی ----
 "تکمیل این فیلد الزامی است": "This field is required", "نشانی ایمیل معتبر نیست": "Invalid email address",
 "نشانی وب معتبر نیست": "Invalid URL", "قالب مقدار واردشده معتبر نیست": "Invalid value format",
 "مقدار واردشده خیلی کوتاه است": "Value is too short", "مقدار واردشده خیلی بلند است": "Value is too long",
 "مقدار از حداقل مجاز کمتر است": "Value is below the allowed minimum",
 "مقدار از حداکثر مجاز بیشتر است": "Value is above the allowed maximum",
 "مقدار خارج از گام مجاز است": "Value skips the allowed step", "مقدار واردشده معتبر نیست": "Invalid value",
 # ---- JS: توست‌ها/مودال‌ها ----
 "انجام شد": "Done", "ارسال ناموفق بود: ": "Submission failed: ",
 "⏳ دستور «{0}» به دستگاه «{1}» تحویل شد — در انتظار اجرا…": "⏳ Command «{0}» delivered to device «{1}» — awaiting execution…",
 "✅ «{0}» روی «{1}» اجرا شد": "✅ «{0}» executed on «{1}»",
 "⚠️ «{0}» روی «{1}» ناموفق بود": "⚠️ «{0}» failed on «{1}»",
 "بستن": "Close", "در حال بارگذاری…": "Loading…", "فرم": "Form",
 "بارگذاری فرم ناموفق بود — ": "Failed to load form — ",
 "نتیجه‌ای یافت نشد": "No results found", "در حال جستجو…": "Searching…", "ایران": "Iran",
 # ---- JS: فرمول‌ساز ----
 "عدد «{0}» نامعتبر است": "Invalid number «{0}»",
 "رشته بسته نشده است — علامت ' جا افتاده": "Unterminated string — missing ' quote",
 "کاراکتر غیرمجاز «{0}» در فرمول": "Illegal character «{0}» in formula",
 "فرمول ناقص است — انتهای فرمول نامنتظره": "Incomplete formula — unexpected end",
 "پرانتز بسته نشده است": "Unclosed parenthesis",
 "پرانتز تابع «{0}» بسته نشده است": "Function «{0}» parenthesis not closed",
 "عبارت نامعتبر: «{0}»": "Invalid expression: «{0}»",
 "عبارت اضافه پس از پایان فرمول: «{0}»": "Extra expression after end of formula: «{0}»",
 "🧩 سازندهٔ بصری فرمول — بلوک را بکشید و در بوم رها کنید (یا فقط کلیک کنید)":
   "🧩 Visual formula builder — drag a block onto the canvas (or just click it)",
 "✍️ نوشتن دستی": "✍️ Manual writing", "↩️ برگشت": "↩️ Undo", "؟ راهنما": "? Help", "🧹 پاک کردن": "🧹 Clear",
 "متغیرها": "Variables", "توابع": "Functions", "اقلام": "Items", "عملگرها": "Operators",
 "عدد/متن": "Number/text", "نمونه‌ها": "Templates", "جستجو…": "Search…", "فرمول: ": "Formula: ",
 "؟ راهنمای سریع فرمول‌ساز": "? Formula builder quick help",
 "بلوک اضافه کنید:": "Add a block:",
 "از تب‌های سمت راست (متغیرها، توابع، اقلام، عملگرها، عدد/متن، نمونه‌ها) بلوک را با موس روی بوم بکشید — یا فقط روی آن کلیک کنید تا به انتهای فرمول اضافه شود.":
   "From the right-hand tabs (variables, functions, items, operators, number/text, templates) drag a block onto the canvas with the mouse — or just click it to append it to the formula.",
 "چیدمان را تنظیم کنید:": "Arrange the layout:",
 "ترتیب محاسبه مثل ریاضی است؛ برای اولویت از «( پرانتز باز» استفاده کنید. با کشیدن جابه‌جا و با ✕ حذف کنید (انداختن بلوک روی پالت هم حذف می‌کند).":
   "Evaluation order works like math; use the «( open paren» block for precedence. Drag to reorder and ✕ to delete (dropping a block on the palette deletes it too).",
 "مقدارها را بدهید:": "Enter the values:",
 "روی بلوک‌های عدد/متن کلیک کنید تا مقدارشان ویرایش شود؛ دکمهٔ «＋» داخل هر تابع آرگومان جدید می‌آورد.":
   "Click the number/text blocks to edit their value; the «＋» button inside each function adds a new argument.",
 "کنترل و ذخیره:": "Check & save:",
 "فرمول متنی زنده ساخته می‌شود؛ خط «💬 به فارسی» معنی فرمول را نشان می‌دهد و اگر فرمول ناقص باشد خطای دقیق همان‌جا می‌آید. اشتباه کردید؟ «↩️ برگشت» — حتی «پاک کردن» هم برگشت‌پذیر است.":
   "The text formula is built live; the «💬 plain words» line shows its meaning, and an exact error appears there if the formula is incomplete. Made a mistake? «↩️ Undo» — even «Clear» is reversible.",
 "فهمیدم، شروع می‌کنم": "Got it, let's start",
 "چیزی برای برگشت نیست — همین حالا چیزی حذف یا اضافه نکرده‌اید": "Nothing to undo — nothing has been added or removed yet",
 "منفی ({0})": "negative ({0})", "نقیض ({0})": "NOT ({0})",
 " به‌علاوه ": " plus ", " منها ": " minus ", " ضربدر ": " times ", " تقسیم بر ": " divided by ",
 "باقیماندهٔ {0} بر {1}": "remainder of {0} over {1}", " به‌توان ": " to the power of ",
 " مساوی ": " equals ", " مخالف ": " differs from ", " کوچکتر از ": " less than ",
 " کوچکتر یا مساوی ": " less than or equal to ", " بزرگتر از ": " greater than ",
 " بزرگتر یا مساوی ": " greater than or equal to ",
 "( {0} ) و ( {1} )": "( {0} ) AND ( {1} )", "( {0} ) یا ( {1} )": "( {0} ) OR ( {1} )",
 "مبلغ قلم {0}": "item amount {0}", "مالیات پلکانی {0}": "progressive tax {0}",
 "{0} گرد شده با {1} رقم اعشار": "{0} rounded to {1} decimal places", "{0} گرد شده": "{0} rounded",
 "کمینهٔ {0}": "minimum of {0}", "بیشینهٔ {0}": "maximum of {0}", "قدرمطلق {0}": "absolute value of {0}",
 "{0} گرد شده به بالا": "{0} rounded up", "{0} گرد شده به پایین": "{0} rounded down",
 "اگر ( {0} ) آنگاه {1} وگرنه {2}": "if ( {0} ) then {1} else {2}",
 "فراخوانی {0}({1})": "call {0}({1})", "، ": ", ",
 "💬 به فارسی: فرمول هنوز خالی است — بلوکی از پالت اضافه کنید تا معنی‌اش را همین‌جا ببینید.":
   "💬 In plain words: the formula is still empty — add a block from the palette to see its meaning here.",
 "💬 به فارسی: ": "💬 In plain words: ",
 "💬 به فارسی: ✖ فرمول ناقص/نامعتبر است — ": "💬 In plain words: ✖ incomplete/invalid formula — ",
 "حذف بلوک": "Delete block", "آرگومان": "Argument", "افزودن آرگومان": "Add argument", "'کد'": "'code'",
 "مبلغ قلم «{0}»": "Item amount «{0}»",
 "مبلغ قلم «{0}» در فرمول‌های قبلی": "Item amount «{0}» from previous items",
 "برای این شرکت هنوز قلمی تعریف نشده — با «قلم جدید» اولین قلم را بسازید.":
   "No items defined for this company yet — create the first one with «new item».",
 "جمع": "Add", "منها": "Subtract", "ضرب": "Multiply", "تقسیم": "Divide", "باقیمانده تقسیم": "Modulo",
 "توان": "Power", "پرانتز باز": "Open paren", "پرانتز بسته": "Close paren", "کوچکتر": "Less than",
 "کوچکتر یا مساوی": "Less than or equal", "بزرگتر": "Greater than", "بزرگتر یا مساوی": "Greater than or equal",
 "مساوی": "Equal", "نامساوی": "Not equal", "و منطقی — هر دو شرط": "Logical AND — both conditions",
 "یا منطقی — یکی از شرط‌ها": "Logical OR — either condition", "نقیض": "Negation",
 "مثلاً 1.4": "e.g. 1.4", "+ عدد": "+ Number", "کد قلم — مثلاً masken": "item code — e.g. masken",
 "+ متن '…'": "+ Text '…'",
 "عدد برای ضرایب (مثل نرخ ۱٫۴) و متن برای آرگومان تابع قلم('کد') استفاده می‌شود.":
   "Numbers are for coefficients (e.g. rate 1.4); text is for the item('code') function argument.",
 "اضافه‌کاری ۱٫۴ برابر": "Overtime ×1.4", "نوع پیشنهادی: مزایا — غیرمستمر مشمول بیمه": "Suggested type: benefit — non-recurring, insurable",
 "اضافه‌کاری از مجوز تأییدشده": "Overtime from approved leave request", "وقتی مبنای شرکت «مجوزهای تأییدشده» است": "When the company basis is «approved requests»",
 "حق شب‌کاری (۲۵٪)": "Night-shift allowance (25%)", "نرخ را با کلیک روی بلوک عدد می‌توانید عوض کنید": "Change the rate by clicking the number block",
 "مأموریت روزانه": "Daily mission", "نرخ روزانه × روزهای مأموریت": "Daily rate × mission days",
 "حق اولاد": "Child allowance", "۳٪ حقوق پایه به‌ازای هر فرزند": "3% of base salary per child",
 "عیدی به‌نسبت کارکرد": "New-year bonus by service ratio", "دوازدهم پایه × نسبت روزهای خدمت سال": "Base ÷ 12 × service-days ratio",
 "کسر غیبت": "Absence deduction", "نوع پیشنهادی: کسور": "Suggested type: deduction",
 "کسر تأخیر (دقیقه‌ای)": "Late deduction (per minute)", "هر دقیقه تأخیر = نرخ ساعتی ÷ ۶۰": "Each late minute = hourly rate ÷ 60",
 "کسر مرخصی بدون حقوق": "Unpaid-leave deduction", "بیمه سهم پرسنل (۷٪)": "Insurance — employee share (7%)",
 "روی درآمد مشمول بیمهٔ همین ماه": "On this month's insurable income", "مالیات پلکانی": "Progressive tax",
 "از پارامتر «پلکان_مالیات» شرکت": "From the company's «tax_bracket» parameter",
 "خالص پرداختی تا این قلم": "Net paid up to this line", "برای قلم‌های جمع‌بندی": "For summary items",
 "مزایا": "Benefit", "کسور": "Deduction", "جمع‌بندی": "Summary",
 "عدد نامعتبر — فقط رقم و نقطه": "Invalid number — digits and dot only", "متن خالی است": "Text is empty",
 "بلوک": "Block", "بلوک‌ها را از پالت اینجا بکشید — یا روی بلوک پالت فقط کلیک کنید": "Drag blocks from the palette here — or just click a palette block",
 "✔ فرمول آماده است — با ذخیرهٔ قلم، اعتبارسنجی نهایی خودکار انجام می‌شود": "✔ Formula is ready — final validation runs automatically when the item is saved",
 "⚠ {0} تابع، آرگومان خالی دارد — جای خالی فعلاً ۰ حساب می‌شود؛ آن را پر کنید": "⚠ {0} function(s) have empty arguments — empty slots count as 0 for now; please fill them",
 "گروه‌بندی": "Grouping", "متغیر زمینه — شرح در کارت «متغیرها»": "Context variable — described in the «variables» card",
 "✍️ نوشتن دستی فرمول — برای بازگشت به سازندهٔ بصری، دکمهٔ «🧩 سازندهٔ بصری» را بزنید":
   "✍️ Manual formula writing — to return to the visual builder, press the «🧩 Visual builder» button",
 "🧩 سازندهٔ بصری": "🧩 Visual builder",
 "✖ {0} — در حالت نوشتن بمانید یا فرمول را اصلاح کنید": "✖ {0} — stay in manual mode or fix the formula",
 "✖ {0} — در حالت نوشتن ادامه دهید": "✖ {0} — continue in manual mode",
 "کلیک یا کشیدن روی بوم = افزودن قالب (بلوک‌ها بعد از آن قابل ویرایش‌اند)": "Click or drag onto the canvas = add the template (blocks stay editable afterwards)",
}

# ---------- ۳) HrLang.DictEn — افزودن کلیدهای غایب ----------
hp = os.path.join(SRC, 'Platform.Core/Services/HrLang.cs')
src = open(hp, encoding='utf-8').read()
existing = set(re.findall(r'\[\s*"((?:[^"\\]|\\.)*)"\s*\]\s*=', src))
missing = []
for k in sorted(set(EN) | KS):
    if k not in existing:
        missing.append(k)
def csharp_escape(s):
    return s.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n')
block = '\n        // ---- فاز ۹۲ — ممیزی کامل چندزبانی: وضعیت‌ها/دستگاه‌ها/پالت/فرمول‌ساز/JS ----\n'
for k in missing:
    en = EN.get(k, k)  # کلید بدون ترجمهٔ مشخص = کلید انگلیسی خودش (مثلاً کدها)
    block += '        ["%s"] = "%s",\n' % (csharp_escape(k), csharp_escape(en))
marker = '\n    };\n}'
idx = src.rfind(marker)
assert idx > 0, 'DictEn closing brace not found'
src = src[:idx] + '\n' + block.rstrip('\n') + src[idx:]
open(hp, 'w', encoding='utf-8', newline='').write(src)
print('DictEn: added', len(missing), 'keys;', len(EN) + len(KS), 'candidates,', len(existing), 'already present')

# ---------- ۴) HrL10n.cs ----------
def csharp_str(s):
    return '"' + csharp_escape(s) + '"'
lines = ',\n        '.join(csharp_str(k) for k in sorted(KS))
l10n = '''using System.Collections.Generic;
using System.Linq;

namespace Platform.Web.Services;

/// <summary>
/// فاز ۹۲ — رشته‌های جاوااسکریپت (site.js و formula-builder.js) سمت کلاینت با همین کلیدها
/// ترجمه می‌شوند: در _Layout به‌صورت window.__hrL10n تزریق می‌شود و توابع hrT/hrF از آن
/// می‌خوانند. کلید = دقیقاً همان رشتهٔ فارسی مبدأ (قرارداد HrT.T). تولید خودکار با
/// scripts/i18n-js-92.py از فراخوانی‌های hrT/hrF — بعد از تغییر JS دوباره اجرا شود.
/// </summary>
public static class HrL10n
{
    public static readonly string[] Keys = new[]
    {
        %s
    };

    /// <summary>دیکشنری کلید ← ترجمهٔ زبان فعلی (برای تزریق JSON در _Layout).</summary>
    public static Dictionary<string, string> Dict() => Keys.ToDictionary(k => k, k => HrT.T(k));
}
''' % lines
open(os.path.join(SRC, 'Platform.Web/Services/HrL10n.cs'), 'w', encoding='utf-8', newline='').write(l10n)
print('HrL10n.cs written:', len(KS), 'keys')

# ---------- ۵) تزریق در _Layout ----------
lp = os.path.join(SRC, 'Platform.Web/Views/Shared/_Layout.cshtml')
L = open(lp, encoding='utf-8').read()
if '__hrL10n' not in L:
    anchor = '    <script src="~/js/site.js" asp-append-version="true"></script>'
    inject = ('    @* فاز ۹۲ — رشته‌های JS (site.js/formula-builder.js) با دیکشنری سرور ترجمه می‌شوند؛\n'
              '       کلید = رشتهٔ فارسی مبدأ، مقدار = ترجمهٔ زبان فعلی (HrT.T) — کلید غایب = فارسی (رفتار امن) *@\n'
              '    <script>window.__hrL10n = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Platform.Web.Services.HrL10n.Dict()))</script>\n')
    assert anchor in L, 'site.js anchor not found in _Layout'
    L = L.replace(anchor, inject + anchor, 1)
    open(lp, 'w', encoding='utf-8', newline='').write(L)
    print('_Layout patched with __hrL10n injection')
else:
    print('_Layout already has __hrL10n')
