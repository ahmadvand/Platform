using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Platform.Web.Services;

namespace Platform.Web.Infrastructure;

// ============================================================
// فاز ۷۴/۷۵ — تازه‌سازی ادعاهای دسترسی و superadmin در هر درخواست
// ----------------------------------------------------------------------
// تا فاز ۷۳ دسترسی‌ها در لحظهٔ ورود داخل JWT منجمد می‌شدند و تغییر نقش
// فقط «در ورود بعدی» اعمال می‌شد. این میان‌افزار (درست پس از
// UseAuthentication) ادعای «perms» را با خروجی زندهٔ RbacService (کش
// نسخه‌دار) جایگزین می‌کند؛ فاز ۷۵ ادعای «sa» را هم زنده کرد. نتیجه:
//   • تغییر دسترسی نقش / حذف نقش کاربر / ساخت نقش جدید → «بلافاصله»
//     برای همهٔ نشست‌های فعال اعمال می‌شود (بدون خروج/ورود مجدد).
//   • سلب/اعطای superadmin هم بلافاصله اعمال می‌شود (فاز ۷۵ — پیش‌تر
//     به ورود مجدد موکول بود).
//   • هزینهٔ مسیر گرم: دو lookup در ConcurrentDictionary (بدون کوئری DB)؛
//     کوئری فقط در اولین لمس هر کاربر یا بعد از هر Bump نسخه اجرا می‌شود.
//   • سایر ادعاها (sub/username/name) دست‌نخورده می‌مانند.
// ============================================================
public class RbacRefreshMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, RbacService rbac)
    {
        var user = ctx.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var idStr = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(idStr))
            {
                var uid = idStr;
                // فاز ۸۳ — بانک در دسترس نیست (مثلاً hrsettings.bin حذف شده و گیت ویزارد هنوز
                // فعال نشده، یا سرور SQL موقتاً خاموش است)؟ ادعاهای کهنه حفظ می‌شوند و درخواست
                // با صفحهٔ زرد SqlException نمی‌ترکد؛ گیت راه‌اندازی/صفحات خودشان مسیر را هدایت می‌کنند.
                try
                {
                    var sa = await rbac.IsSuperadminAsync(uid);   // فاز ۷۵ — زنده از DB (کش نسخه‌دار)
                    var perms = await rbac.GetPermsAsync(uid, sa);

                    // بازسازی Principal با ادعاهای تازهٔ perms/sa (بقیهٔ ادعاها حفظ می‌شوند)
                    var old = user.Claims
                        .Where(c => c.Type != "perms" && c.Type != "sa")
                        .Select(c => new Claim(c.Type, c.Value));
                    var identity = new ClaimsIdentity(old, user.Identity!.AuthenticationType);
                    identity.AddClaim(new Claim("sa", sa ? "1" : "0"));
                    if (perms.Count > 0)
                        identity.AddClaim(new Claim("perms", string.Join(' ', perms)));
                    ctx.User = new ClaimsPrincipal(identity);
                }
                catch (Exception)
                {
                    // شکست تازه‌سازی = ادامه با ادعاهای لحظهٔ ورود (شکست باز — فاز ۴۸)
                }
            }
        }
        await next(ctx);
    }
}
