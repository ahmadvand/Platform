using System.Globalization;

namespace Platform.Web.Infrastructure;

/// <summary>
/// فاز ۷۴ — انتخاب زبان هر درخواست
/// ----------------------------------------------------------------------
/// ترتیب تصمیم:
///   ۱) پارامتر ‎?lang=fa|en‎ — انتخاب صریح؛ در این حالت کوکی یک‌ساله هم ثبت می‌شود؛
///   ۲) کوکی hr_lang — انتخاب شخصی کاربر (از سوییچر 🌐 نوار بالا / صفحه ورود / ویزارد)؛
///   ۳) کلید «Localization:Default» از تنظیمات نصب (hrsettings.bin) — انتخاب مدیر در
///      ویزارد راه‌اندازی یا صفحه تنظیمات؛
///   ۴) فارسی — مبدأ و پیش‌فرض.
///
/// نکتهٔ حیاتی: فقط CurrentUICulture عوض می‌شود (منبع ترجمه/جهت UI)؛ CurrentCulture
/// روی Invariant باقی می‌ماند تا قالب‌بندی اعداد و تاریخ (اکسل، PDF، دیتابیس،
/// JSON) در هر دو زبان کاملاً یکسان و بی‌سورپرایز بماند — fa-IR تقویم پیش‌فرض
/// شمسی دارد و اگر CurrentCulture می‌شد، همهٔ تاریخ‌های گزارش‌ها بی‌هشدار شمسی می‌شدند.
///
/// جایگاه در زنجیره: پس از فایل‌های استاتیک و پیش از گیت ویزارد — تا خودِ صفحهٔ
/// /Setup هم دوزبانه رندر شود. بدون وابستگی به بانک: حتی پیش از نصب (بدون
/// اتصال بانک) کار می‌کند و پیش‌فرض را از IConfiguration می‌خواند.
/// </summary>
public class HrCultureMiddleware
{
    private readonly RequestDelegate _next;

    public HrCultureMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var code = Resolve(ctx);
        // فاز ۷۸ — فرهنگ UI برای کدهای داخلی ثابت است؛ برای بسته‌های زبان بیرونی (مثل ar)
        // تلاش می‌کنیم CultureInfo همان کد ساخته شود (تا TwoLetterISOLanguageName در T.Lang
        // همان کد را برگرداند)؛ کد ناشناخته → fa-IR (راست‌به‌چپ، امن). CurrentCulture همچنان
        // دست‌نخورده می‌ماند تا قالب‌بندی اعداد/تاریخ زبان‌مستقل بماند.
        CultureInfo ui;
        if (code == T.En) ui = new CultureInfo("en-US");
        else if (code == T.Fa) ui = new CultureInfo("fa-IR");
        else
        {
            try { ui = new CultureInfo(code); }
            catch { ui = new CultureInfo("fa-IR"); }
        }
        CultureInfo.CurrentUICulture = ui;
        await _next(ctx);
    }

    private static string Resolve(HttpContext ctx)
    {
        // ۱) انتخاب صریح ‎?lang=‎ + ثبت کوکی یک‌ساله
        var q = ctx.Request.Query["lang"].ToString();
        if (T.IsValid(q))
        {
            var qc = q!.Trim().ToLowerInvariant();
            ctx.Response.Cookies.Append(T.CookieName, qc, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = ctx.Request.IsHttps,
            });
            return qc;
        }

        // ۲) کوکی انتخاب شخصی
        var c = ctx.Request.Cookies[T.CookieName];
        if (T.IsValid(c)) return c!.Trim().ToLowerInvariant();

        // ۳) زبان پیش‌فرض سامانه (تنظیمات نصب) — ۴) فارسی
        try
        {
            var d = ctx.RequestServices.GetRequiredService<IConfiguration>()["Localization:Default"];
            if (T.IsValid(d)) return d!.Trim().ToLowerInvariant();
        }
        catch { /* پیش از نصب هم نباید شکسته شود */ }

        return "fa";
    }
}
