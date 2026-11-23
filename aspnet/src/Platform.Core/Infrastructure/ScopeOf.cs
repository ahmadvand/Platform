using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Web.Infrastructure;

// ============================================================
// فاز ۷۴ — دسترسی کوتاه به دامنهٔ رکوردی کاربر جاری از داخل کنترلرها
// نمونه: var scope = await this.ScopeAsync();
// قرارداد IUserScopeProvider ∈ Platform.Core (Services/UserScope.cs)؛
// پیاده‌سازی: DataScopeService هاست (composite context + کش نسخه‌دار).
// ============================================================
public static class ScopeOfExt
{
    public static Task<UserScope> ScopeAsync(this Controller c)
    {
        var user = c.HttpContext.User;
        var idStr = user.FindFirst("sub")?.Value
                 ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(idStr))
            return Task.FromResult(UserScope.Unrestricted);
        var uid = idStr;
        var sa = user.FindFirst("sa")?.Value == "1";
        return c.HttpContext.RequestServices.GetRequiredService<IUserScopeProvider>()
            .ForUserAsync(uid, sa);
    }
}
