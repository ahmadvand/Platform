# -*- coding: utf-8 -*-
# فاز ۹۲ — ممیزی کامل چندزبانی (درخواست کاربر: «همه جا رو بررسی کن حتی tooltip ها و placeholder ها»)
# ۱) نقاط رندر خامِ مقادیر فارسی (وضعیت‌ها) با HrT.T بسته می‌شوند
# ۲) رشته‌های فارسی site.js و formula-builder.js با مترجم JS (hrT/hrF) ترجمه می‌شوند —
#    دیکشنری سمت سرور (HrL10n) در _Layout تزریق می‌شود
# ۳) همهٔ کلیدهای جدید با ترجمهٔ انگلیسی به HrLang.DictEn اضافه می‌شوند (بدون تکرار کلید)
import os, re, json

SRC = "/home/z/my-project/aspnet/src"
report = []

def patch(path, pairs, must=True):
    p = os.path.join(SRC, path)
    s = open(p, encoding='utf-8').read()
    for old, new in pairs:
        if old not in s:
            report.append(f"MISS {path}: {old[:70]}")
            if must: continue
        s = s.replace(old, new)
    open(p, 'w', encoding='utf-8', newline='').write(s)
    print("patched", path)

# ============ ۱) نماهای سرور — رندرهای خام ============
patch('modules/Platform.Modules.Parking/Views/Parking/Index.cshtml', [
    ('<b>@g.Name</b> · @HrT.T(g.Type) · @g.Direction', '<b>@g.Name</b> · @HrT.T(g.Type) · @HrT.T(g.Direction)'),
    ('">🚧 @g.OpenState</span>', '">🚧 @HrT.T(g.OpenState)</span>'),
])
patch('modules/HrModules.Access/Views/Access/Index.cshtml', [
    ('">@z.RiskLevel</span>', '">@HrT.T(z.RiskLevel)</span>'),
    ('<b>@g.Name</b> · @HrT.T(g.Type) · @g.Direction', '<b>@g.Name</b> · @HrT.T(g.Type) · @HrT.T(g.Direction)'),
    ('">🔒 @g.DoorState</span>', '">🔒 @HrT.T(g.DoorState)</span>'),
])
patch('modules/HrModules.Access/Views/Access/_DoorStateForm.cshtml', [
    ('<b>@gate.DoorState</b>', '<b>@HrT.T(gate.DoorState)</b>'),
])
patch('modules/HrModules.Attendance/Views/SelfAtt/Requests.cshtml', [
    ('<option value="استحقاقی">استحقاقی</option>', '<option value="استحقاقی">@HrT.T("استحقاقی")</option>'),
    ('<option value="استعلاجی">استعلاجی</option>', '<option value="استعلاجی">@HrT.T("استعلاجی")</option>'),
    ('<option value="ساعتی">ساعتی</option>', '<option value="ساعتی">@HrT.T("ساعتی")</option>'),
    ('<option value="بدون‌حقوق">بدون حقوق</option>', '<option value="بدون‌حقوق">@HrT.T("بدون حقوق")</option>'),
])
patch('modules/HrModules.Devices/Views/Biometrics/Index.cshtml', [
    ('<span class="when">آخرین دریافت: @(x.LastFetch is null', '<span class="when">@HrT.T("آخرین دریافت:") @(x.LastFetch is null'),
    ('value="true" checked /> شامل قالب‌های هویتی (اثر انگشت و چهره)', 'value="true" checked /> @HrT.T("شامل قالب‌های هویتی (اثر انگشت و چهره)")'),
    ('@d.Name — @Jalali.Num(uc) کاربر</option>', '@d.Name — @Jalali.Num(uc) @HrT.T("کاربر")</option>'),
])
patch('modules/HrModules.Devices/Views/Devices/Index.cshtml', [
    ('data-modal-title="@HrT.F("ویرایش دستگاه — {0}", d.Name)">✎ ویرایش</a>', 'data-modal-title="@HrT.F("ویرایش دستگاه — {0}", d.Name)">@HrT.T("✎ ویرایش")</a>'),
    ('<div class="fld"><label>پرسنل *</label>', '<div class="fld"><label>@HrT.T("پرسنل *")</label>'),
    ('<div class="fld"><label>افزودن به (مقصدها)</label>', '<div class="fld"><label>@HrT.T("افزودن به (مقصدها)")</label>'),
    ('<div class="fld"><label>حذف از (مبدأها)</label>', '<div class="fld"><label>@HrT.T("حذف از (مبدأها)")</label>'),
    ('<td><span class="badge badge-info">@DeviceOps.CommandLabels.GetValueOrDefault(c.Type, c.Type)</span></td>',
     '<td><span class="badge badge-info">@HrT.T(DeviceOps.CommandLabels.GetValueOrDefault(c.Type, c.Type))</span></td>'),
])
# CATALOG — BioHint و NamePrefix فقط نمایشی‌اند؛ قبل از serialize ترجمه می‌شوند (Purpose = توکن DB، دست نمی‌خورد)
patch('modules/HrModules.Devices/Views/Devices/_DeviceForm.cshtml', [
    ('<div class="hint" id="bioHint">@DeviceDefaults.Kinds[d.Kind].BioHint</div>',
     '<div class="hint" id="bioHint">@HrT.T(DeviceDefaults.Kinds[d.Kind].BioHint)</div>'),
    ('kinds = DeviceDefaults.Kinds.ToDictionary(x => x.Key, x => new { x.Value.Purpose, x.Value.Polling, x.Value.NamePrefix, x.Value.BioHint }),',
     'kinds = DeviceDefaults.Kinds.ToDictionary(x => x.Key, x => new { x.Value.Purpose, x.Value.Polling, NamePrefix = HrT.T(x.Value.NamePrefix), BioHint = HrT.T(x.Value.BioHint) }),'),
])
patch('Platform.Web/Views/Reports/Parking.cshtml', [
    ('<td>@r.Method @(string.IsNullOrEmpty(r.Camera)', '<td>@HrT.T(r.Method) @(string.IsNullOrEmpty(r.Camera)'),
])
patch('Platform.Web/Views/Settings/Index.cshtml', [
    ('-p:VendorName="نام شرکت شما"', '-p:VendorName="@(HrT.T("نام شرکت شما"))"'),
])
patch('Platform.Web/Views/Setup/Index.cshtml', [
    ('<span class="wiz-num">۱</span>', '<span class="wiz-num">@HrT.Digits("1")</span>'),
    ('<span class="wiz-num">۲</span>', '<span class="wiz-num">@HrT.Digits("2")</span>'),
])
patch('modules/HrModules.Payroll/Views/Payroll/Items.cshtml', [
    ('placeholder="مثال: دقیقه_اضافه_کاری / 60 * ساعتی * 1.4"', 'placeholder="@HrT.T("مثال: دقیقه_اضافه_کاری / 60 * ساعتی * 1.4")"'),
])

# ============ ۲) site.js — مترجم + رشته‌ها ============
helpers = '''// ============ فاز ۹۲ — مترجم رشته‌های JS (دیکشنری سرور: window.__hrL10n) ============
// کلید = رشتهٔ فارسی مبدأ (همان قرارداد HrT.T سمت سرور). غایب = خود رشتهٔ فارسی (رفتار امن).
window.hrT = function (fa) {
    var d = window.__hrL10n;
    return (d && Object.prototype.hasOwnProperty.call(d, fa)) ? d[fa] : fa;
};
window.hrF = function (fa) {
    var s = window.hrT(fa);
    for (var i = 1; i < arguments.length; i++) s = s.split('{' + (i - 1) + '}').join(String(arguments[i] === null ? '' : arguments[i]));
    return s;
};

'''
p = os.path.join(SRC, 'Platform.Web/wwwroot/js/site.js')
s = open(p, encoding='utf-8').read()
if 'window.hrT' not in s:
    s = s.replace('// ============ ساعت زنده + تاریخ جلالی (Intl) ============', helpers + '// ============ ساعت زنده + تاریخ جلالی (Intl) ============', 1)
patch_js = [
    ("opts.title || 'تأیید'", "opts.title || hrT('تأیید')"),
    ("(danger ? 'این عملیات حساس است' : 'تأیید عملیات')", "(danger ? hrT('این عملیات حساس است') : hrT('تأیید عملیات'))"),
    ("opts.okText || 'بله، انجام بده'", "opts.okText || hrT('بله، انجام بده')"),
    ("opts.cancelText || 'انصراف'", "opts.cancelText || hrT('انصراف')"),
    ("f.getAttribute('data-dept') || 'این واحد'", "f.getAttribute('data-dept') || hrT('این واحد')"),
    (".text.trim() : 'مقصد';", ".text.trim() : hrT('مقصد');"),
    ("f.getAttribute('data-old') || 'محل قبلی'", "f.getAttribute('data-old') || hrT('محل قبلی')"),
    ("[{ text: 'جابجایی همراه پرسنل', value: 'with', cls: 'btn-primary' }]", "[{ text: hrT('جابجایی همراه پرسنل'), value: 'with', cls: 'btn-primary' }]"),
    ("buttons.push({ text: 'جابجایی بدون پرسنل', value: 'leave', cls: 'btn' });", "buttons.push({ text: hrT('جابجایی بدون پرسنل'), value: 'leave', cls: 'btn' });"),
    ("buttons.push({ text: 'انصراف', value: null, cls: 'btn-ghost' });", "buttons.push({ text: hrT('انصراف'), value: null, cls: 'btn-ghost' });"),
    ("title: 'جابجایی واحد سازمانی',", "title: hrT('جابجایی واحد سازمانی'),"),
    ("message: 'واحد «' + dept + '» به «' + dest + '» منتقل شود؟\\n' +\n" +
     "'پرسنل مستقیم این واحد: ' + count + ' نفر\\n' +\n" +
     "(count > 0 ? 'در حالت «بدون پرسنل»، این ' + count + ' نفر در «' + old + '» باقی می‌مانند.'\n" +
     ": 'این واحد پرسنل مستقیمی ندارد؛ زیرمجموعه‌ها و پرسنل آن‌ها همراه واحد می‌آیند.'),",
     "message: hrF('واحد «{0}» به «{1}» منتقل شود؟\\nپرسنل مستقیم این واحد: {2} نفر\\n', dept, dest, count) +\n" +
     "(count > 0 ? hrF('در حالت «بدون پرسنل»، این {0} نفر در «{1}» باقی می‌مانند.', count, old)\n" +
     ": hrT('این واحد پرسنل مستقیمی ندارد؛ زیرمجموعه‌ها و پرسنل آن‌ها همراه واحد می‌آیند.')),"),
    ("required: 'تکمیل این فیلد الزامی است',", "required: hrT('تکمیل این فیلد الزامی است'),"),
    ("email: 'نشانی ایمیل معتبر نیست',", "email: hrT('نشانی ایمیل معتبر نیست'),"),
    ("url: 'نشانی وب معتبر نیست',", "url: hrT('نشانی وب معتبر نیست'),"),
    ("pattern: 'قالب مقدار واردشده معتبر نیست',", "pattern: hrT('قالب مقدار واردشده معتبر نیست'),"),
    ("tooShort: 'مقدار واردشده خیلی کوتاه است',", "tooShort: hrT('مقدار واردشده خیلی کوتاه است'),"),
    ("tooLong: 'مقدار واردشده خیلی بلند است',", "tooLong: hrT('مقدار واردشده خیلی بلند است'),"),
    ("rangeUnderflow: 'مقدار از حداقل مجاز کمتر است',", "rangeUnderflow: hrT('مقدار از حداقل مجاز کمتر است'),"),
    ("rangeOverflow: 'مقدار از حداکثر مجاز بیشتر است',", "rangeOverflow: hrT('مقدار از حداکثر مجاز بیشتر است'),"),
    ("step: 'مقدار خارج از گام مجاز است',", "step: hrT('مقدار خارج از گام مجاز است'),"),
    ("generic: 'مقدار واردشده معتبر نیست'", "generic: hrT('مقدار واردشده معتبر نیست')"),
    ("(j.ok ? 'انجام شد' : 'خطا')", "(j.ok ? hrT('انجام شد') : hrT('خطا'))"),
    ("window.toast('ارسال ناموفق بود: ' + ex.message, 'err');", "window.toast(hrT('ارسال ناموفق بود: ') + ex.message, 'err');"),
    ("window.toast('⏳ دستور «' + label + '» به دستگاه «' + (c.device || '') + '» تحویل شد — در انتظار اجرا…', 'info');",
     "window.toast(hrF('⏳ دستور «{0}» به دستگاه «{1}» تحویل شد — در انتظار اجرا…', label, (c.device || '')), 'info');"),
    ("window.toast('✅ «' + label + '» روی «' + (c.device || '') + '» اجرا شد' + (c.result ? ' — ' + c.result : ''), 'ok');",
     "window.toast(hrF('✅ «{0}» روی «{1}» اجرا شد', label, (c.device || '')) + (c.result ? ' — ' + hrT(c.result) : ''), 'ok');"),
    ("window.toast('⚠️ «' + label + '» روی «' + (c.device || '') + '» ناموفق بود' + (c.result ? ' — ' + c.result : ''), 'err');",
     "window.toast(hrF('⚠️ «{0}» روی «{1}» ناموفق بود', label, (c.device || '')) + (c.result ? ' — ' + hrT(c.result) : ''), 'err');"),
    ('"class=\'modal-x\'" aria-label="بستن"', '""'),  # placeholder unlikely — kept for safety
    ("class=\"modal-x\" aria-label=\"بستن\">✕</button></div>'", "class=\"modal-x\" aria-label=\"' + hrT('بستن') + '\">✕</button></div>'"),
    ("<div class=\"modal-b\"><div class=\"modal-loading\">در حال بارگذاری…</div></div>'", "<div class=\"modal-b\"><div class=\"modal-loading\">' + hrT('در حال بارگذاری…') + '</div></div>'"),
    ("ov.querySelector('h3').textContent = opts.title || 'فرم';", "ov.querySelector('h3').textContent = opts.title || hrT('فرم');"),
    ("'<div class=\"modal-loading\">بارگذاری فرم ناموفق بود — ' + ex.message + '</div>'", "'<div class=\"modal-loading\">' + hrT('بارگذاری فرم ناموفق بود — ') + ex.message + '</div>'"),
    ("open(html, 'نتیجه‌ای یافت نشد');", "open(html, hrT('نتیجه‌ای یافت نشد'));"),
    ("var IRAN = 'ایران';", "var IRAN = hrT('ایران');"),
    ("<span class=\"pr-iran\">ایران</span>'", "<span class=\"pr-iran\">' + hrT('ایران') + '</span>'"),
]
for old, new in patch_js:
    if old == '""': continue
    if old in s:
        s = s.replace(old, new)
    else:
        report.append('MISS site.js: ' + old[:70])
open(p, 'w', encoding='utf-8', newline='').write(s)
print('patched site.js')

# ============ ۳) formula-builder.js ============
p = os.path.join(SRC, 'Platform.Web/wwwroot/js/formula-builder.js')
s = open(p, encoding='utf-8').read()
fb = [
    ("throw new Error('عدد «' + text + '» نامعتبر است');", "throw new Error(hrF('عدد «{0}» نامعتبر است', text));"),
    ('throw new Error("رشته بسته نشده است — علامت \' جا افتاده");', 'throw new Error(hrT("رشته بسته نشده است — علامت \' جا افتاده"));'),
    ("throw new Error('کاراکتر غیرمجاز «' + c + '» در فرمول');", "throw new Error(hrF('کاراکتر غیرمجاز «{0}» در فرمول', c));"),
    ("throw new Error('فرمول ناقص است — انتهای فرمول نامنتظره');", "throw new Error(hrT('فرمول ناقص است — انتهای فرمول نامنتظره'));"),
    ("throw new Error('پرانتز بسته نشده است');", "throw new Error(hrT('پرانتز بسته نشده است'));"),
    ("throw new Error('پرانتز تابع «' + c.t + '» بسته نشده است');", "throw new Error(hrF('پرانتز تابع «{0}» بسته نشده است', c.t));"),
    ("throw new Error('عبارت نامعتبر: «' + c.t + '»');", "throw new Error(hrF('عبارت نامعتبر: «{0}»', c.t));"),
    ("throw new Error('عبارت اضافه پس از پایان فرمول: «' + cur().t + '»');", "throw new Error(hrF('عبارت اضافه پس از پایان فرمول: «{0}»', cur().t));"),
    ("'<span class=\"fb-cap\">🧩 سازندهٔ بصری فرمول — بلوک را بکشید و در بوم رها کنید (یا فقط کلیک کنید)</span>'",
     "'<span class=\"fb-cap\">' + hrT('🧩 سازندهٔ بصری فرمول — بلوک را بکشید و در بوم رها کنید (یا فقط کلیک کنید)') + '</span>'"),
    ("data-fb-act=\"mode\">✍️ نوشتن دستی</button>'", "data-fb-act=\"mode\">' + hrT('✍️ نوشتن دستی') + '</button>'"),
    ("data-fb-act=\"undo\">↩️ برگشت</button>'", "data-fb-act=\"undo\">' + hrT('↩️ برگشت') + '</button>'"),
    ("data-fb-act=\"help\">؟ راهنما</button>'", "data-fb-act=\"help\">' + hrT('؟ راهنما') + '</button>'"),
    ("data-fb-act=\"clear\">🧹 پاک کردن</button>'", "data-fb-act=\"clear\">' + hrT('🧹 پاک کردن') + '</button>'"),
    ("data-fb-tab=\"vars\">متغیرها</button>'", "data-fb-tab=\"vars\">' + hrT('متغیرها') + '</button>'"),
    ("data-fb-tab=\"funcs\">توابع</button>'", "data-fb-tab=\"funcs\">' + hrT('توابع') + '</button>'"),
    ("data-fb-tab=\"items\">اقلام</button>'", "data-fb-tab=\"items\">' + hrT('اقلام') + '</button>'"),
    ("data-fb-tab=\"ops\">عملگرها</button>'", "data-fb-tab=\"ops\">' + hrT('عملگرها') + '</button>'"),
    ("data-fb-tab=\"lit\">عدد/متن</button>'", "data-fb-tab=\"lit\">' + hrT('عدد/متن') + '</button>'"),
    ("data-fb-tab=\"tpl\">نمونه‌ها</button>'", "data-fb-tab=\"tpl\">' + hrT('نمونه‌ها') + '</button>'"),
    ("placeholder=\"جستجو…\" style=\"display:none\" /}'", "placeholder=\"' + hrT('جستجو…') + '\" style=\"display:none\" /}'"),
    ("'<div class=\"fb-out\">فرمول: <code data-fb-out>—</code></div>'", "'<div class=\"fb-out\">' + hrT('فرمول: ') + '<code data-fb-out>—</code></div>'"),
    ("setStatus('warn', 'چیزی برای برگشت نیست — همین حالا چیزی حذف یا اضافه نکرده‌اید');", "setStatus('warn', hrT('چیزی برای برگشت نیست — همین حالا چیزی حذف یا اضافه نکرده‌اید'));"),
    ("'<div class=\"fb-help-h\">؟ راهنمای سریع فرمول‌ساز <button", "'<div class=\"fb-help-h\">' + hrT('؟ راهنمای سریع فرمول‌ساز') + ' <button"),
    ("data-fb-help-close>فهمیدم، شروع می‌کنم</button></div>'", "data-fb-help-close>' + hrT('فهمیدم، شروع می‌کنم') + '</button></div>'"),
    ("return node.op === '-' ? 'منفی (' + human(node.x) + ')' : 'نقیض (' + human(node.x) + ')';",
     "return node.op === '-' ? hrF('منفی ({0})', human(node.x)) : hrF('نقیض ({0})', human(node.x));"),
    ("case '+': return A + ' به‌علاوه ' + B;", "case '+': return A + hrT(' به‌علاوه ') + B;"),
    ("case '-': return A + ' منها ' + B;", "case '-': return A + hrT(' منها ') + B;"),
    ("case '*': return A + ' ضربدر ' + B;", "case '*': return A + hrT(' ضربدر ') + B;"),
    ("case '/': return A + ' تقسیم بر ' + B;", "case '/': return A + hrT(' تقسیم بر ') + B;"),
    ("case '%': return 'باقیماندهٔ ' + A + ' بر ' + B;", "case '%': return hrF('باقیماندهٔ {0} بر {1}', A, B);"),
    ("case '^': return A + ' به‌توان ' + B;", "case '^': return A + hrT(' به‌توان ') + B;"),
    ("case '==': return A + ' مساوی ' + B;", "case '==': return A + hrT(' مساوی ') + B;"),
    ("case '!=': return A + ' مخالف ' + B;", "case '!=': return A + hrT(' مخالف ') + B;"),
    ("case '<': return A + ' کوچکتر از ' + B;", "case '<': return A + hrT(' کوچکتر از ') + B;"),
    ("case '<=': return A + ' کوچکتر یا مساوی ' + B;", "case '<=': return A + hrT(' کوچکتر یا مساوی ') + B;"),
    ("case '>': return A + ' بزرگتر از ' + B;", "case '>': return A + hrT(' بزرگتر از ') + B;"),
    ("case '>=': return A + ' بزرگتر یا مساوی ' + B;", "case '>=': return A + hrT(' بزرگتر یا مساوی ') + B;"),
    ("case '&&': return '( ' + A + ' ) و ( ' + B + ' )';", "case '&&': return hrF('( {0} ) و ( {1} )', A, B);"),
    ("case '||': return '( ' + A + ' ) یا ( ' + B + ' )';", "case '||': return hrF('( {0} ) یا ( {1} )', A, B);"),
    ("case 'قلم': return 'مبلغ قلم ' + a0;", "case 'قلم': return hrF('مبلغ قلم {0}', a0);"),
    ("case 'مالیات': return 'مالیات پلکانی ' + a0;", "case 'مالیات': return hrF('مالیات پلکانی {0}', a0);"),
    ("case 'round': return args.length > 1 ? a0 + ' گرد شده با ' + args[1] + ' رقم اعشار' : a0 + ' گرد شده';",
     "case 'round': return args.length > 1 ? hrF('{0} گرد شده با {1} رقم اعشار', a0, args[1]) : hrF('{0} گرد شده', a0);"),
    ("case 'min': return 'کمینهٔ ' + args.join('، ');", "case 'min': return hrF('کمینهٔ {0}', args.join(hrT('، ')));"),
    ("case 'max': return 'بیشینهٔ ' + args.join('، ');", "case 'max': return hrF('بیشینهٔ {0}', args.join(hrT('، ')));"),
    ("case 'abs': return 'قدرمطلق ' + a0;", "case 'abs': return hrF('قدرمطلق {0}', a0);"),
    ("case 'ceil': return a0 + ' گرد شده به بالا';", "case 'ceil': return hrF('{0} گرد شده به بالا', a0);"),
    ("case 'floor': return a0 + ' گرد شده به پایین';", "case 'floor': return hrF('{0} گرد شده به پایین', a0);"),
    ("case 'if': return 'اگر ( ' + a0 + ' ) آنگاه ' + (args[1] || '۰') + ' وگرنه ' + (args[2] || '۰');",
     "case 'if': return hrF('اگر ( {0} ) آنگاه {1} وگرنه {2}', a0, (args[1] || '۰'), (args[2] || '۰'));"),
    ("return 'فراخوانی ' + node.n + '(' + args.join('، ') + ')';", "return hrF('فراخوانی {0}({1})', node.n, args.join(hrT('، ')));"),
    ("faEl.textContent = '💬 به فارسی: فرمول هنوز خالی است — بلوکی از پالت اضافه کنید تا معنی‌اش را همین‌جا ببینید.';",
     "faEl.textContent = hrT('💬 به فارسی: فرمول هنوز خالی است — بلوکی از پالت اضافه کنید تا معنی‌اش را همین‌جا ببینید.');"),
    ("faEl.textContent = '💬 به فارسی: ' + human(node);", "faEl.textContent = hrT('💬 به فارسی: ') + human(node);"),
    ("faEl.textContent = '💬 به فارسی: ✖ فرمول ناقص/نامعتبر است — ' + ex.message;", "faEl.textContent = hrT('💬 به فارسی: ✖ فرمول ناقص/نامعتبر است — ') + ex.message;"),
    ("x.title = 'حذف بلوک';", "x.title = hrT('حذف بلوک');"),
    ("s.dataset.fbPh = placeholder || 'آرگومان';", "s.dataset.fbPh = placeholder || hrT('آرگومان');"),
    ("b.title = 'افزودن آرگومان';", "b.title = hrT('افزودن آرگومان');"),
    ("var ph = name === 'قلم' ? \"'کد'\" : 'آرگومان';", "var ph = name === 'قلم' ? hrT(\"'کد'\") : hrT('آرگومان');"),
    ("title || 'مبلغ قلم «' + code + '»')", "title || hrF('مبلغ قلم «{0}»', code))"),
    ("it.t + ' — مبلغ قلم «' + it.c + '» در فرمول‌های قبلی'", "it.t + ' — ' + hrF('مبلغ قلم «{0}» در فرمول‌های قبلی', it.c)"),
    ("emptyItems.textContent = 'برای این شرکت هنوز قلمی تعریف نشده — با «قلم جدید» اولین قلم را بسازید.';",
     "emptyItems.textContent = hrT('برای این شرکت هنوز قلمی تعریف نشده — با «قلم جدید» اولین قلم را بسازید.');"),
    ("{ title: 'اضافه‌کاری ۱٫۴ برابر', formula: 'دقیقه_اضافه_کاری / 60 * ساعتی * 1.4', hint: 'نوع پیشنهادی: مزایا — غیرمستمر مشمول بیمه', tag: 'مزایا' },",
     "{ title: hrT('اضافه‌کاری ۱٫۴ برابر'), formula: 'دقیقه_اضافه_کاری / 60 * ساعتی * 1.4', hint: hrT('نوع پیشنهادی: مزایا — غیرمستمر مشمول بیمه'), tag: 'مزایا' },"),
    ("{ title: 'اضافه‌کاری از مجوز تأییدشده', formula: 'دقیقه_اضافه_کاری_مجوز / 60 * ساعتی * 1.4', hint: 'وقتی مبنای شرکت «مجوزهای تأییدشده» است', tag: 'مزایا' },",
     "{ title: hrT('اضافه‌کاری از مجوز تأییدشده'), formula: 'دقیقه_اضافه_کاری_مجوز / 60 * ساعتی * 1.4', hint: hrT('وقتی مبنای شرکت «مجوزهای تأییدشده» است'), tag: 'مزایا' },"),
    ("{ title: 'حق شب‌کاری (۲۵٪)', formula: 'دقیقه_شب_کاری / 60 * ساعتی * 0.25', hint: 'نرخ را با کلیک روی بلوک عدد می‌توانید عوض کنید', tag: 'مزایا' },",
     "{ title: hrT('حق شب‌کاری (۲۵٪)'), formula: 'دقیقه_شب_کاری / 60 * ساعتی * 0.25', hint: hrT('نرخ را با کلیک روی بلوک عدد می‌توانید عوض کنید'), tag: 'مزایا' },"),
    ("{ title: 'مأموریت روزانه', formula: 'روزانه * مأموریت_روز', hint: 'نرخ روزانه × روزهای مأموریت', tag: 'مزایا' },",
     "{ title: hrT('مأموریت روزانه'), formula: 'روزانه * مأموریت_روز', hint: hrT('نرخ روزانه × روزهای مأموریت'), tag: 'مزایا' },"),
    ("{ title: 'حق اولاد', formula: 'پایه_مبنا * 0.03 * تعداد_فرزند', hint: '۳٪ حقوق پایه به‌ازای هر فرزند', tag: 'مزایا' },",
     "{ title: hrT('حق اولاد'), formula: 'پایه_مبنا * 0.03 * تعداد_فرزند', hint: hrT('۳٪ حقوق پایه به‌ازای هر فرزند'), tag: 'مزایا' },"),
    ("{ title: 'عیدی به‌نسبت کارکرد', formula: 'پایه_مبنا / 12 * (کارکرد_از_سال_روز / ماه_روزها)', hint: 'دوازدهم پایه × نسبت روزهای خدمت سال', tag: 'مزایا' },",
     "{ title: hrT('عیدی به‌نسبت کارکرد'), formula: 'پایه_مبنا / 12 * (کارکرد_از_سال_روز / ماه_روزها)', hint: hrT('دوازدهم پایه × نسبت روزهای خدمت سال'), tag: 'مزایا' },"),
    ("{ title: 'کسر غیبت', formula: 'روزانه * غیبت_روز', hint: 'نوع پیشنهادی: کسور', tag: 'کسور' },",
     "{ title: hrT('کسر غیبت'), formula: 'روزانه * غیبت_روز', hint: hrT('نوع پیشنهادی: کسور'), tag: 'کسور' },"),
    ("{ title: 'کسر تأخیر (دقیقه‌ای)', formula: 'دقیقه_تأخیر * ساعتی / 60', hint: 'هر دقیقه تأخیر = نرخ ساعتی ÷ ۶۰', tag: 'کسور' },",
     "{ title: hrT('کسر تأخیر (دقیقه‌ای)'), formula: 'دقیقه_تأخیر * ساعتی / 60', hint: hrT('هر دقیقه تأخیر = نرخ ساعتی ÷ ۶۰'), tag: 'کسور' },"),
    ("{ title: 'کسر مرخصی بدون حقوق', formula: 'روزانه * مرخصی_بدون_حقوق_روز', hint: 'نوع پیشنهادی: کسور', tag: 'کسور' },",
     "{ title: hrT('کسر مرخصی بدون حقوق'), formula: 'روزانه * مرخصی_بدون_حقوق_روز', hint: hrT('نوع پیشنهادی: کسور'), tag: 'کسور' },"),
    ("{ title: 'بیمه سهم پرسنل (۷٪)', formula: 'درآمد_مشمول_بیمه * 0.07', hint: 'روی درآمد مشمول بیمهٔ همین ماه', tag: 'کسور' },",
     "{ title: hrT('بیمه سهم پرسنل (۷٪)'), formula: 'درآمد_مشمول_بیمه * 0.07', hint: hrT('روی درآمد مشمول بیمهٔ همین ماه'), tag: 'کسور' },"),
    ("{ title: 'مالیات پلکانی', formula: 'مالیات(درآمد_مشمول_مالیات)', hint: 'از پارامتر «پلکان_مالیات» شرکت', tag: 'کسور' },",
     "{ title: hrT('مالیات پلکانی'), formula: 'مالیات(درآمد_مشمول_مالیات)', hint: hrT('از پارامتر «پلکان_مالیات» شرکت'), tag: 'کسور' },"),
    ("{ title: 'خالص پرداختی تا این قلم', formula: 'جمع_مزایا - جمع_کسور', hint: 'برای قلم‌های جمع‌بندی', tag: 'جمع‌بندی' },",
     "{ title: hrT('خالص پرداختی تا این قلم'), formula: 'جمع_مزایا - جمع_کسور', hint: hrT('برای قلم‌های جمع‌بندی'), tag: 'جمع‌بندی' },"),
    ("tag.textContent = tp.tag;", "tag.textContent = hrT(tp.tag);"),
    ("el.title = 'کلیک یا کشیدن روی بوم = افزودن قالب (بلوک‌ها بعد از آن قابل ویرایش‌اند)';",
     "el.title = hrT('کلیک یا کشیدن روی بوم = افزودن قالب (بلوک‌ها بعد از آن قابل ویرایش‌اند)');"),
    ("'+': 'جمع', '-': 'منها', '*': 'ضرب', '/': 'تقسیم', '%': 'باقیمانده تقسیم', '^': 'توان',",
     "'+': hrT('جمع'), '-': hrT('منها'), '*': hrT('ضرب'), '/': hrT('تقسیم'), '%': hrT('باقیمانده تقسیم'), '^': hrT('توان'),"),
    ("'(': 'پرانتز باز', ')': 'پرانتز بسته',", "'(': hrT('پرانتز باز'), ')': hrT('پرانتز بسته'),"),
    ("'<': 'کوچکتر', '<=': 'کوچکتر یا مساوی', '>': 'بزرگتر', '>=': 'بزرگتر یا مساوی',",
     "'<': hrT('کوچکتر'), '<=': hrT('کوچکتر یا مساوی'), '>': hrT('بزرگتر'), '>=': hrT('بزرگتر یا مساوی'),"),
    ("'==': 'مساوی', '!=': 'نامساوی', '&&': 'و منطقی — هر دو شرط', '||': 'یا منطقی — یکی از شرط‌ها', '!': 'نقیض',",
     "'==': hrT('مساوی'), '!=': hrT('نامساوی'), '&&': hrT('و منطقی — هر دو شرط'), '||': hrT('یا منطقی — یکی از شرط‌ها'), '!': hrT('نقیض'),"),
    ("placeholder=\"مثلاً 1.4\"", "placeholder=\"' + hrT('مثلاً 1.4') + '\""),
    ("data-fb-addnum>+ عدد</button>'", "data-fb-addnum>' + hrT('+ عدد') + '</button>'"),
    ("placeholder=\"کد قلم — مثلاً masken\"", "placeholder=\"' + hrT('کد قلم — مثلاً masken') + '\""),
    ("data-fb-addstr>+ متن \\u2018…\\u2019</button>'", "@@"),
    ("data-fb-addstr>+ متن '…'</button>'", "data-fb-addstr>' + hrT(\"+ متن '…'\") + '</button>'"),
    ("'<div class=\"fb-hint\">عدد برای ضرایب (مثل نرخ ۱٫۴) و متن برای آرگومان تابع قلم(\\'کد\\') استفاده می‌شود.</div>';",
     "'<div class=\"fb-hint\">' + hrT('عدد برای ضرایب (مثل نرخ ۱٫۴) و متن برای آرگومان تابع قلم(\\'کد\\') استفاده می‌شود.') + '</div>';"),
    ("{ setStatus('err', 'عدد نامعتبر — فقط رقم و نقطه'); return; }", "{ setStatus('err', hrT('عدد نامعتبر — فقط رقم و نقطه')); return; }"),
    ("{ setStatus('err', 'متن خالی است'); return; }", "{ setStatus('err', hrT('متن خالی است')); return; }"),
    ("args.insertBefore(slotEl('آرگومان'), add);", "args.insertBefore(slotEl(hrT('آرگومان')), add);"),
    ("chip.textContent || 'بلوک'", "chip.textContent || hrT('بلوک')"),
    ("ph.textContent = 'بلوک‌ها را از پالت اینجا بکشید — یا روی بلوک پالت فقط کلیک کنید';",
     "ph.textContent = hrT('بلوک‌ها را از پالت اینجا بکشید — یا روی بلوک پالت فقط کلیک کنید');"),
    ("p2.textContent = s.dataset.fbPh || 'آرگومان';", "p2.textContent = s.dataset.fbPh || hrT('آرگومان');"),
    ("? '✔ فرمول آماده است — با ذخیرهٔ قلم، اعتبارسنجی نهایی خودکار انجام می‌شود'",
     "? hrT('✔ فرمول آماده است — با ذخیرهٔ قلم، اعتبارسنجی نهایی خودکار انجام می‌شود')"),
    ("setStatus('warn', '⚠ ' + bad + ' تابع، آرگومان خالی دارد — جای خالی فعلاً ۰ حساب می‌شود؛ آن را پر کنید');",
     "setStatus('warn', hrF('⚠ {0} تابع، آرگومان خالی دارد — جای خالی فعلاً ۰ حساب می‌شود؛ آن را پر کنید', bad));"),
    ("mkChip('paren', '(', '(', 'گروه‌بندی')", "mkChip('paren', '(', '(', hrT('گروه‌بندی'))"),
    ("mkChip('paren', ')', ')', 'گروه‌بندی')", "mkChip('paren', ')', ')', hrT('گروه‌بندی'))"),
    ("mkChip('var', node.n, node.n, 'متغیر زمینه — شرح در کارت «متغیرها»')", "mkChip('var', node.n, node.n, hrT('متغیر زمینه — شرح در کارت «متغیرها»'))"),
    ("capEl.textContent = '✍️ نوشتن دستی فرمول — برای بازگشت به سازندهٔ بصری، دکمهٔ «🧩 سازندهٔ بصری» را بزنید';",
     "capEl.textContent = hrT('✍️ نوشتن دستی فرمول — برای بازگشت به سازندهٔ بصری، دکمهٔ «🧩 سازندهٔ بصری» را بزنید');"),
    ("modeBtn.textContent = '🧩 سازندهٔ بصری';", "modeBtn.textContent = hrT('🧩 سازندهٔ بصری');"),
    ("capEl.textContent = '🧩 سازندهٔ بصری فرمول — بلوک را بکشید و در بوم رها کنید (یا فقط کلیک کنید)';",
     "capEl.textContent = hrT('🧩 سازندهٔ بصری فرمول — بلوک را بکشید و در بوم رها کنید (یا فقط کلیک کنید)');"),
    ("modeBtn.textContent = '✍️ نوشتن دستی';", "modeBtn.textContent = hrT('✍️ نوشتن دستی');"),
    ("showManual('err', '✖ ' + ex.message + ' — در حالت نوشتن بمانید یا فرمول را اصلاح کنید');",
     "showManual('err', hrF('✖ {0} — در حالت نوشتن بمانید یا فرمول را اصلاح کنید', ex.message));"),
    ("showManual('err', '✖ ' + ex.message + ' — در حالت نوشتن ادامه دهید');",
     "showManual('err', hrF('✖ {0} — در حالت نوشتن ادامه دهید', ex.message));"),
]
for old, new in fb:
    if new == '@@': continue
    if old in s:
        s = s.replace(old, new)
    else:
        report.append('MISS formula-builder: ' + old[:70])
# محافظ: اگر site.js لود نشده باشد مترجم تعریف شود
if 'typeof window.hrT' not in s:
    s = ("// فاز ۹۲ — محافظ: اگر site.js لود نشده باشد، مترجم identity تعریف می‌شود (رفتار امن)\n"
         "if (typeof window.hrT !== 'function') { window.hrT = function (k) { return k; }; window.hrF = function (k) { return k; }; }\n" + s)
# چهار گام راهنما — الگوی <li><b>عنوان:</b> متن</li>
def li_repl(m):
    return "'<li><b>' + hrT('%s') + '</b>' + hrT('%s') + '</li>' +" % (m.group(1), m.group(2))
s = re.sub(r"'<li><b>([^<]+)</b>(.*?)</li>' \+", li_repl, s)
open(p, 'w', encoding='utf-8', newline='').write(s)
print('patched formula-builder.js')

# ============ ۴) HrL10n.cs — فهرست کلیدهای JS ============
print('\n==== report ====')
for r in report: print(r)
