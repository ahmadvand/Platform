using Microsoft.Extensions.Configuration;

namespace Platform.Web.Infrastructure;

// ============================================================
// فاز ۸۵ — OWASP Top 10:2025 (A02: Security Misconfiguration)
// ----------------------------------------------------------------------
// هدرهای امنیتی استاندارد روی همهٔ پاسخ‌ها (استاتیک + داینامیک):
//   • X-Content-Type-Options: nosniff          — جلوگیری از MIME-sniffing
//   • Referrer-Policy                          — افشای URL داخلی به بیرون نمی‌رود
//   • Permissions-Policy                       — دوربین/میکروفون/موقعیت به اسکریپت‌ها واگذار نمی‌شود
//     (پلتفرم هیچ‌جا getUserMedia ندارد — دوربین‌های پلاک‌خوان سخت‌افزاری‌اند؛ صفر ریسک)
//   • X-Permitted-Cross-Domain-Policies: none  — سیاست‌های Flash/PDF قدیمی بسته می‌شود
//   • Cross-Origin-Opener-Policy: same-origin  — ایزوله‌سازی پنجره‌ها (اپ هیچ window.open ای ندارد)
//   • Content-Security-Policy                  — از پیکربندی Security:Headers (پیش‌فرض: منابع فقط خودِ
//     سامانه + inline برای اسکریپت/استایل موجود ویوها؛ frame-ancestors عمداً تعیین نمی‌شود
//     چون جاسازی در iframe پنل پیش‌نمایش باید کار کند — همان قرارداد Security:AllowEmbedding)
//     فاز ۱۰۵ — اگر endpoint خودش CSP ست کرده باشد (مثل /Settings/ScreensaverRuntime که
//     «sandbox allow-scripts» برای سندباکس کد غریبه می‌نویسد)، همان محترم شمرده می‌شود
//     و بازنویسی نمی‌شود (هیچ endpoint دیگری CSP ست نمی‌کند — رفتار بقیهٔ سامانه دست‌نخورده).
// همهٔ هدرها پیکربندی‌پذیرند و با Security:Headers:Enabled=false کل میان‌افزار خاموش می‌شود
// (هیچ قابلیتی از بین نمی‌رود — فقط پیش‌فرض امن‌تر).
// نکتهٔ ترتیب: callbackهای OnStarting به‌ترتیب LIFO اجرا می‌شوند؛ این میان‌افزار هیچ‌وقت
// X-Frame-Options نمی‌نویسد، پس با میان‌افزار جاسازی (که آن را حذف می‌کند) تداخلی ندارد.
// ============================================================
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, IConfiguration cfg)
    {
        if (cfg.GetValue("Security:Headers:Enabled", true))
        {
            ctx.Response.OnStarting(() =>
            {
                var h = ctx.Response.Headers;

                h["X-Content-Type-Options"] = "nosniff";
                h["Referrer-Policy"] = cfg["Security:Headers:ReferrerPolicy"] ?? "strict-origin-when-cross-origin";
                h["X-Permitted-Cross-Domain-Policies"] = "none";
                h["Cross-Origin-Opener-Policy"] = "same-origin";

                var permissions = cfg["Security:Headers:PermissionsPolicy"];
                if (!string.IsNullOrWhiteSpace(permissions) && permissions != "off")
                    h["Permissions-Policy"] = permissions;

                var csp = cfg["Security:Headers:ContentSecurityPolicy"];
                if (!string.IsNullOrWhiteSpace(csp) && csp != "off" &&
                    !h.ContainsKey("Content-Security-Policy"))   // فاز ۱۰۵ — CSP اعلان‌شدهٔ endpoint مقدم است
                    h["Content-Security-Policy"] = csp;

                return Task.CompletedTask;
            });
        }
        await next(ctx);
    }
}
