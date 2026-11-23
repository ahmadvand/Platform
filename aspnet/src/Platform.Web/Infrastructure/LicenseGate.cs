using Platform.Web.Services;

namespace Platform.Web.Infrastructure;

/// <summary>
/// میان‌افزار مجوز (فاز ۴۶) — بازطراحی فاز ۵۱ بر پایهٔ درخواست صریح کاربر:
/// «نمی‌خوام بعد از گذشت اعتبار، نرم‌افزار از کار بیفته؛ فقط هر بار که کاربر وارد میشه
/// هشدار بده و بعد از تایید ادامه بده».
/// ----------------------------------------------------------------------
/// فاز ۸۳ب — «فقط با مجوز» (درخواست کاربر): حالت Off حذف شد؛ همیشه حداقل هشدار فعال است:
///   • /api و پل دستگاه‌ها و درخواست‌های AJAX/pjax همیشه رد می‌شوند (سامانه زنده می‌ماند)
///   • فقط «ناوبری کامل صفحه» برای کاربر وارد‌شده‌ای که هنوز هشدار این نشست را
///     تأیید نکرده، به صفحهٔ اطلاع /License/Notice می‌رود (دکمهٔ «ادامه به سامانه»
///     کوکی تأیید نشست جاری را می‌گذارد) — و چون ورود بعدی کوکی را پاک می‌کند،
///     هشدار پس از هر ورود دوباره نمایش داده می‌شود.
///   • بنر قرمز بالای صفحه‌ها (LicenseService.BannerText) همچنان وضعیت را یادآور می‌شود.
///   • حالت «قفل سخت» (lock) از خود مجوز (LockMode صادرکننده) همچنان پذیرفته است.
/// مسیرهای آزاد: /License، /Auth، /health، /Setup، /api.
/// </summary>
public sealed class LicenseGate
{
    /// <summary>کوکی تأیید هشدار مجوز — کوکی نشست (بدون انقضا)؛ در هر ورود پاک می‌شود.</summary>
    public const string AckCookie = "hr_lic_ack";

    private readonly RequestDelegate _next;

    public LicenseGate(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, LicenseService lic)
    {
        var snap = lic.Current;

        // فاز ۷۸ — حالت مؤثر اجرای قفل: اولویت با «حالت اجرای قفل» درج‌شده در خود مجوز
        // (انتخاب صریح صادرکننده در LicenseStudio/LicenseGen)؛ در غیاب آن، تنظیمات سرور.
        //   lock  = قفل سخت — صفحه‌های سامانه تا رفع وضعیت بسته می‌مانند
        //   warn  = فقط هشدار (رفتار فاز ۵۱ — صفحهٔ Notice + کوکی تایید)
        //   off   = بدون قفل (تنظیمات Off سرور و بدون انتخاب در مجوز)
        var effective = snap.LockMode?.Trim().ToLowerInvariant() switch
        {
            "lock" => "lock",
            "warn" => "warn",
            _ => string.Equals(lic.Mode, "Strict", StringComparison.OrdinalIgnoreCase) ? "warn" : "off",
        };

        if (effective == "off")
        {
            await _next(ctx);
            return;
        }

        if (!snap.Locked)
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path;
        if (path.StartsWithSegments("/License") || path.StartsWithSegments("/Auth") ||
            path.StartsWithSegments("/health") || path.StartsWithSegments("/Setup") ||
            path.StartsWithSegments("/api") ||
            // فاز ۶۳ — پل‌های ماژول گیت‌وی مثل /api همیشه باز (دستگاه‌ها در قفل مجوز هم زنده بمانند)
            path.StartsWithSegments("/iclock") || path.StartsWithSegments("/anpr"))
        {
            await _next(ctx);
            return;
        }

        // فاز ۵۱ — فقط ناوبری کامل صفحه هشدار می‌گیرد؛ API/دستگاه/AJAX/pjax عبور می‌کنند
        var isXhr = ctx.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    ctx.Request.Headers.ContainsKey("X-Pjax");
        var isPageNav = HttpMethods.IsGet(ctx.Request.Method) && !isXhr;
        var authed = ctx.User?.Identity?.IsAuthenticated == true;
        var acked = ctx.Request.Cookies.ContainsKey(AckCookie);

        // فاز ۷۸ — قفل سخت (انتخاب صریح صادرکنندهٔ مجوز): صفحه‌ها/AJAX/POST برای کاربر واردشده
        // بسته می‌مانند تا وضعیت مجوز درست شود؛ /api و پل دستگاه‌ها (فهرست بالا) باز می‌مانند
        // تا سلامت داده‌ها و دستگاه‌ها حفظ شود.
        if (effective == "lock" && authed)
        {
            if (isPageNav)
            {
                ctx.Response.Redirect("/License/Notice");
                return;
            }
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            if (isXhr)
            {
                ctx.Response.ContentType = "application/json; charset=utf-8";
                await ctx.Response.WriteAsync("{\"ok\":false,\"message\":\"🔒 مجوز سامانه نامعتبر یا منقضی است (قفل سخت) — مجوز جدید را در صفحهٔ مجوز نصب وارد کنید\"}");
            }
            return;
        }

        // حالت هشدار (فاز ۵۱) — فقط ناوبری کامل صفحهٔ کاربر واردشدهٔ تاییدنکرده
        if (authed && !acked && isPageNav)
        {
            ctx.Response.Redirect("/License/Notice");
            return;
        }

        await _next(ctx);
    }
}
