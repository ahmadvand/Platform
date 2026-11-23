using Platform.Web.Data;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Platform.Web.Infrastructure;

/// <summary>
/// فیلتر مجوز بر پایه کاتالوگ دسترسی‌ها — هم‌ارز guard() در نسخه Next.js.
/// برای /api پاسخ JSON و برای صفحات MVC ریدایرکت می‌دهد.
/// </summary>
public class HasPermAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _perm;
    public HasPermAttribute(string perm) => _perm = perm;

    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var http = ctx.HttpContext;
        var isApi = http.Request.Path.StartsWithSegments("/api");
        var user = http.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            ctx.Result = isApi
                ? new JsonResult(new { error = "احراز هویت لازم است" }) { StatusCode = 401 }
                : new RedirectResult("/Auth/Login");
            return;
        }

        var sa = user.FindFirst("sa")?.Value == "1";
        // فاز ۷۴ — ادعای perms با میان‌افزار RbacRefresh همیشه تازه است؛
        // تطبیق با wildcard (a.b.*) و کلید والد (سازگاری با نقش‌های درشت قدیمی)
        var perms = (user.FindFirst("perms")?.Value ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (sa || RbacService.Matches(perms, _perm)) return;

        ctx.Result = isApi
            ? new JsonResult(new { error = "دسترسی کافی ندارید (مجوز لازم: " + _perm + ")" }) { StatusCode = 403 }
            : new RedirectResult("/Auth/Denied?perm=" + Uri.EscapeDataString(_perm));
    }
}

