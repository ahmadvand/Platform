using Microsoft.AspNetCore.Mvc;
using Platform.Web.Infrastructure;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۷۴ — سوییچر زبان
/// لینک‌های «فارسی / English» در ویزارد راه‌اندازی، صفحه ورود و نوار بالای سامانه
/// همه به همین‌جا می‌آیند: کوکی یک‌سالهٔ hr_lang ثبت و به «همان صفحه» برگردانده
/// می‌شود (back فقط URL محلی پذیرفته می‌شود). GET ساده و بی‌اثر است (تغییر زبان
/// عمل داده‌ای نیست) و چون گیت ویزارد هم باید بگذرد، در Program.cs کنار /Setup
/// و /health مجاز شده است.
/// </summary>
[Route("Lang")]
public class LangController : Controller
{
    [HttpGet("Set")]
    public IActionResult Set(string? c, string? back)
    {
        if (T.IsValid(c))
        {
            Response.Cookies.Append(T.CookieName, c!.Trim().ToLowerInvariant(), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
            });
        }
        // فقط مسیر محلی — جلوگیری از open redirect
        return LocalRedirect(Url.IsLocalUrl(back) && back!.StartsWith('/') ? back : "/");
    }
}
