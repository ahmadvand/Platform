using Platform.Web.Services;

namespace Platform.Web.Infrastructure;

/// <summary>
/// فاز ۱۰۳ — گیت سرورسمت درخواست‌ها حین قفل محافظ صفحه (درخواست کاربر:
/// «برای امنیت بیشتر، درخواست‌های AJAX حین قفل را هم سمت سرور محدود کن»).
/// ----------------------------------------------------------------------
/// تا فاز ۱۰۲ قفل سرورسمت فقط «رندر دوبارهٔ صفحهٔ قفل» را تضمین می‌کرد (data-ss-start-locked)
/// ولی یک مهاجم می‌توانست از کنسول مرورگر، اورلی را حذف کند و تا پیش از رفرش بعدی، همان صفحهٔ
/// باز را با آجاکسِ زنده تغذیه کند. از این فاصله جلوگیری می‌شود:
///
///   • حین قفل (پرچم ScreensaverLock برای این نشست مرورگر) «هر» درخواست غیرمعاف رد می‌شود:
///       - AJAX (fetch/XHR/pjax) ← 423 Locked + JSON {"ok":false,"locked":true}
///         (hr-screensaver.js همین 423 را رصد می‌کند و تب‌های دیگر همین نشست را هم بی‌درنگ
///          قفل و رمزدار می‌کند — هم‌سویی همهٔ تب‌ها بدون انتظار برای بی‌حرکتی)
///       - فرم‌پست معمولی (غیر AJAX) ← ریدایرکت به «/» — بارگذاری کامل همان اول قفل رندر می‌شود
///       - ناوبری کامل GET ← عبور؛ صفحه با data-ss-start-locked="true" رندر می‌شود و
///         محافظ بلافاصله با پنل رمز فعال می‌شود (رفتار فاز ۱۰۲ — رفرش دیگر رمز را دور نمی‌زند)
///
///   • مسیرهای معاف: POST /Auth/VerifyWakePassword (تنها راه قانونی برداشتن قفل)،
///     POST /Auth/ScreensaverLock (گزارش قفل/تمدید)، POST /Auth/Logout (خروج اضطراری)
///     و GET /health (مانیتورینگ). فایل‌های استاتیک اصلاً به اینجا نمی‌رسند (UseStaticFiles
///     پیش‌تر در پایپ‌لاین است) — پس CSS/JS لازم برای صفحهٔ قفل همیشه سرو می‌شود.
///
///   • کلید قفل = هش کوکی hr_at؛ درخواستِ بدون این کوکی (دستگاه گیت‌وی /iclock و /anpr،
///     صفحهٔ ورود، ...) هرگز «قفل» تشخیص داده نمی‌شود — گیت فقط برای نشستِ واردشدهٔ قفل‌شده فعال است.
/// </summary>
public sealed class ScreensaverLockGate
{
    private readonly RequestDelegate _next;

    public ScreensaverLockGate(RequestDelegate next) => _next = next;

    /// <summary>مسیرهای معاف حین قفل — فقط POST (به‌جز /health).</summary>
    private static readonly string[] ExemptPostPaths =
    {
        "/Auth/VerifyWakePassword",
        "/Auth/ScreensaverLock",
        "/Auth/Logout",
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        var cookie = ctx.Request.Cookies[JwtService.AccessCookie];
        if (!ScreensaverLock.IsLocked(cookie))
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? "/";
        var method = ctx.Request.Method;

        // مسیرهای معاف — بیدارسازی/گزارش قفل/خروج/سلامت
        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase) ||
            (HttpMethods.IsPost(method) && ExemptPostPaths.Any(
                p => string.Equals(path, p, StringComparison.OrdinalIgnoreCase))))
        {
            await _next(ctx);
            return;
        }

        if (IsAjaxLike(ctx.Request))
        {
            ctx.Response.StatusCode = StatusCodes.Status423Locked;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                ok = false,
                locked = true,
                message = HrT.T("صفحه قفل است — برای ادامه، رمز کاربر جاری را وارد کنید"),
            });
            await ctx.Response.WriteAsync(payload);
            return;
        }

        if (!HttpMethods.IsGet(method))
        {
            // فرم‌پست معمولی حین قفل — به ریشه برگرد؛ بارگذاری کامل همان اول قفل رندر می‌شود
            ctx.Response.Redirect("/");
            return;
        }

        // ناوبری کامل GET — عبور؛ صفحه با data-ss-start-locked رندر می‌شود و فوراً قفل می‌شود
        await _next(ctx);
    }

    /// <summary>
    /// تشخیص AJAX — نشانه‌های مستقل و لایه‌لایه:
    ///   ۱) هدر سنتی X-Requested-With: XMLHttpRequest (jQuery/pjax و fetchهای خود سامانه)
    ///   ۲) هدرهای Sec-Fetch مرورگرهای مدرن — dest=document/iframe/frame فقط ناوبری کامل صفحه است؛
    ///      dest=empty (fetch/XHR) و dest=script/image/… همه «آجاکس‌مانند» حساب می‌شوند.
    ///   ۳) بدون هیچ نشانه‌ای (کلاینت قدیمی) — GET = ناوبری فرض می‌شود، بقیهٔ متدها رد.
    /// ناوبری GET به‌خودی‌خود داده‌ای لو نمی‌دهد: محتوای رندرشده همان اول با اورلی قفل پوشیده
    /// می‌شود و «هر» دادهٔ بعدی (آجاکس) از همین گیت رد می‌شود.
    /// </summary>
    private static bool IsAjaxLike(HttpRequest req)
    {
        if (string.Equals(req.Headers["X-Requested-With"].ToString(), "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase))
            return true;

        var dest = req.Headers["Sec-Fetch-Dest"].ToString();
        if (dest.Length > 0)
            return !dest.Equals("document", StringComparison.OrdinalIgnoreCase)
                && !dest.Equals("iframe", StringComparison.OrdinalIgnoreCase)
                && !dest.Equals("frame", StringComparison.OrdinalIgnoreCase);

        return !HttpMethods.IsGet(req.Method);
    }
}
