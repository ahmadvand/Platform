using Platform.Web.Services;

namespace Platform.Web.Infrastructure;

// ============================================================
// فاز ۷۶ — میان‌افزار تعیین زبان رابط کاربری (i18n)
// ------------------------------------------------------------
// اولویت: کوکی hr_lang (انتخاب کاربر) ← زبان پیش‌فرض ادمین از
// «تنظیمات سامانه» (AppSettings.DefaultLang — فاز ۸۳) ← کلید
// Ui:Language پیکربندی (زبان انتخاب‌شده هنگام نصب اولیه توسط
// ویزارد /Setup) ← «fa».
// جایگاه در خط لوله: بلافاصله پس از فایل‌های استاتیک و «پیش از» گیت
// راه‌اندازی/مجوز/ماژول — تا همهٔ پیام‌های میانی هم قابلیت ترجمه داشته باشند.
// عمداً CultureInfo رشته تغییر نمی‌کند (صفر رگرسیون روی binding/parse
// اعداد و تاریخ‌ها)؛ فقط HrT.Cur برای رندر Razor تعیین می‌شود.
// ============================================================
public sealed class CultureMiddleware(RequestDelegate next)
{
    public const string CookieName = "hr_lang";

    public async Task Invoke(HttpContext ctx, IConfiguration cfg, HrLangStore langs, SettingsService settings)
    {
        var code = ctx.Request.Cookies[CookieName];
        // فاز ۷۷ — زبان‌های پویای پنل ادمین هم پذیرفته می‌شوند (fa/en استاتیک + بسته‌های ترجمه)
        if (!HrLang.Supported(code) && !langs.Supported(code))
        {
            // فاز ۸۳ — زبان پیش‌فرض ادمین (تنظیمات سامانه). GetSafeAsync خودش امن است
            // (بانک در دسترس نبود = پیش‌فرض بدون DefaultLang) — گیرکردن راه‌اندازی هم نمی‌شکند.
            var d = (await settings.GetSafeAsync()).DefaultLang;
            if (!string.IsNullOrWhiteSpace(d) && (HrLang.Supported(d) || langs.Supported(d)))
                code = d;
            if (!HrLang.Supported(code) && !langs.Supported(code)) code = HrLang.FromConfig(cfg);
        }
        HrT.Set(code);

        // فاز ۸۶ + ۹۱ — تنظیمات نمایشی از ادمین: نوع تقویم + ارز. GetSafeAsync خودش امن است
        // (بانک در دسترس نبود = شمسی/IRR پیش‌فرض) — هیچ‌وقت زنجیرهٔ درخواست را نمی‌شکند.
        var s = await settings.GetSafeAsync();
        Jalali.DisplayMode = s.Calendar switch
        {
            "gregorian" => HrCalendarMode.Gregorian,
            "hijri" => HrCalendarMode.Hijri,
            _ => HrCalendarMode.Jalali,
        };
        // فاز ۹۶ — جبران روز تقویم قمری (صلح با تقویم رسمی ایران/افغانستان؛ ام‌القری یک روز جلوتر است)
        Jalali.HijriAdjustDays = Math.Clamp(s.HijriAdjust, -2, 2);
        // فاز ۹۱ — ارز نمایشی همهٔ مبالغ پولی (درخواست کاربر: هر کشور ارز خودش را دارد) —
        // فقط «نمایش» مبالغ را قالب می‌دهد؛ اعداد ذخیره‌شده و محاسبات دست‌نخورده می‌مانند.
        Money.Configure(s.Currency, s.CurrencyRate);

        await next(ctx);
    }
}
