using Platform.Web.Modules;

namespace Platform.Web.Infrastructure;

/// <summary>
/// گیت ماژول‌ها (فاز ۵۴) — درخواست به صفحه/API یک ماژول خاموش را می‌بندد:
///   • /api/* و وب‌هوک‌ها → ۵۰۳ JSON («ماژول غیرفعال است») — سازگار با گیت‌وی دستگاه‌ها
///   • فراخوانی AJAX فرانت → ۴۰۳ JSON («message»)
///   • ناوبری عادی → ریدایرکت به صفحهٔ دوستانهٔ /Home/ModuleDisabled?code=…
/// جایگاه در زنجیره: پس از LicenseGate (مجوز نصب اولویت دارد) و پیش از UseAuthorization.
/// ماژول‌های پایه و کنترلرهای خارج از ماژول‌بندی همیشه باز می‌مانند (شکست باز — صفر رگرسیون).
/// </summary>
public sealed class ModuleGate(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, ModuleStateService mods)
    {
        var rv = ctx.Request.RouteValues;
        var ctrl = rv["controller"]?.ToString();
        if (string.IsNullOrEmpty(ctrl))
        {
            // فاز ۵۷ — بدون endpoint (کنترلر در بسته نیست): اگر سگمنت اول نام کنترلرِ
            // یک ماژول نصب‌نشده بود، به‌جای ۴۰۴ خام، پاسخ گیت را بده (صفحهٔ دوستانه/403).
            // /api و فایل‌های استاتیک و هر مسیر بی‌ربط مثل قبل رد می‌شوند.
            var seg = ctx.Request.Path.Value?.Trim('/').Split('/').FirstOrDefault()?.ToLowerInvariant();
            var mcode = string.IsNullOrEmpty(seg) ? null : ModuleCatalog.NotInstalledControllerModule(seg);
            // فاز ۶۸ — ماژولِ فیزیکی-غایب که مانیفستش اینجا نیست: اگر کد آن در پیکربندی مدیر
            // یا مجوز نصب «شناخته‌شده» باشد، باز صفحهٔ دوستانه بده (منبع شناخت = داده، نه کد هاست).
            if (mcode is null && !string.IsNullOrEmpty(seg) && mods.KnownButNotInstalled(seg))
                mcode = seg;
            if (mcode is null) { await next(ctx); return; }
            // فاز ۶۹ — مانیفستِ ماژول حذف‌شده ممکن است از «حافظهٔ» آخرین بوت سالم بیاید
            // (ModuleManifestStore)؛ هیچ‌کدام = ۴۰۴ امن (نه ۵۰۰).
            var mni = ModuleCatalog.Find(mcode) ?? ModuleManifestStore.Find(mcode);
            if (mni is null) { await next(ctx); return; }
            if (ctx.Request.Path.StartsWithSegments("/api")) { await next(ctx); return; } // وب‌هوک غایب = ۴۰۴ واقعی (قرارداد پل)
            if (ctx.Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest")
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                await ctx.Response.WriteAsync("{\"ok\":false,\"message\":\"ماژول «" + mni.Title + "» در این نسخه نصب نشده است\"}");
                return;
            }
            ctx.Response.Redirect("/Home/ModuleDisabled?code=" + Uri.EscapeDataString(mni.Code));
            return;
        }

        var code = ModuleCatalog.ModuleOf(ctrl, rv["action"]?.ToString());
        if (code is null) { await next(ctx); return; }

        var m = ModuleCatalog.Find(code);
        if (m is null || m.IsCore || mods.Enabled(code)) { await next(ctx); return; }

        // ---------- ماژول خاموش یا نصب‌نشده است ----------
        // فاز ۵۷: اگر DLL ماژول اصلاً در بسته نیست (بیلد مشتری)، پیام دقیق‌تر «نصب نیست» است
        var blocked = HrBuildModules.Installed(code)
            ? "ماژول «" + m.Title + "» غیرفعال است"
            : "ماژول «" + m.Title + "» در این نسخه نصب نشده است";
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsync("{\"ok\":false,\"error\":\"" + blocked + "\"}");
            return;
        }
        if (ctx.Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest")
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsync("{\"ok\":false,\"message\":\"" + blocked + "\"}");
            return;
        }
        ctx.Response.Redirect("/Home/ModuleDisabled?code=" + Uri.EscapeDataString(m.Code));
    }
}
