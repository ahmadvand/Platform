using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Web.Domain;
using Platform.Web.Data;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۷۶ — سوئیچ زبان رابط کاربری: /Ui/Lang?l=en&amp;r=/Auth/Login
/// کوکی hr_lang را یک‌ساله ست می‌کند و به مسیر محلی بازمی‌گرداند.
/// کد زبان نامعتبر نادیده گرفته می‌شود (فقط fa/en)؛ مسیر بازگشت باید
/// محلی باشد (شروع با «/» و نه «//») — در غیر این صورت «/».
/// فاز ۹۳ — به‌جای پرش ۳۰۲ خام، صفحهٔ گذر کوتاه (~۱ ثانیه) با پرچم بزرگِ
/// زبان انتخابی نمایش داده می‌شود (درخواست کاربر: «هنگام تغییر زبان پرچم را
/// نمایش بده») و سپس با JS/meta-refresh به مقصد بازمی‌گردد.
/// فاز ۱۱۴ — جستجوی زندهٔ پرسنل برای emp-picker (مقیاس ده‌ها هزار نفر).
/// </summary>
public class UiController(HrLangStore langs, AppDbContext db) : Controller
{
    [HttpGet("/Ui/Lang")]
    public IActionResult Lang(string? l, string? r)
    {
        // فاز ۷۷ — زبان‌های پویای پنل ادمین هم پذیرفته می‌شوند
        if (HrLang.Supported(l) || langs.Supported(l))
        {
            Response.Cookies.Append(CultureMiddleware.CookieName, l!, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = false,          // ویزارد نصب هم با JS همان کوکی را ست می‌کند
                SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Secure = Request.IsHttps,
                IsEssential = true,
            });
        }
        var ret = !string.IsNullOrEmpty(r) && r.StartsWith('/') && !r.StartsWith("//") ? r : "/";
        var code = (l ?? "fa").Trim().ToLowerInvariant();
        var title = HrT.MenuLanguages().FirstOrDefault(x => x.Code == code).Title ?? code;
        HrT.Set(code);   // صفحهٔ گذر به «زبان جدید» رندر شود (کوکی همین لحظه ست شد)
        Response.Headers.CacheControl = "no-store";
        return View("LangSwitch", (Code: code, Title: title, ReturnUrl: ret));
    }

    // ============================================================
    // فاز ۱۱۴ (درخواست کاربر: «ممکن است ده هزار پرسنل باشد — لیست ۱۰هزارتایی
    // در چند قسمت باعث کندی مرورگر و سختی انتخاب می‌شود») — endpoint جستجوی
    // زندهٔ پرسنل برای انتخابگر emp-picker (فاز ۱۰۴): به‌جای <select> با همهٔ
    // پرسنل (چند مگابایت DOM)، جستجوی سمت سرور با حداکثر ۵۰ نتیجه.
    // q = نام/نام‌خانوادگی/کد پرسنلی (contains)؛ limit حداکثر ۱۰۰؛
    // free=1 = فقط پرسنل بدون کاربر متصل (فرم‌های کاربران)؛
    // bio=1 = خلاصهٔ قالب‌های بیومتریک در sub (صفحهٔ دستگاه‌ها).
    // دامنهٔ رکوردی کاربر جاری (UserScope) همیشه اعمال می‌شود.
    // ============================================================
    [HttpGet("/Ui/EmpSearch")]
    public async Task<IActionResult> EmpSearch(string? q, int limit = 50, bool free = false, bool bio = false)
    {
        if (!(User?.Identity?.IsAuthenticated ?? false)) return Unauthorized();
        limit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 100);
        var term = (q ?? "").Trim();
        var scope = await this.ScopeAsync();

        var emps = db.Set<Employee>().AsNoTracking().Where(e => e.Status == "فعال");
        emps = emps.WhereEmpScope(scope, e => e.Id);
        if (free)
        {
            var linked = db.Set<User>().AsNoTracking()
                .Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!);
            emps = emps.Where(e => !linked.Contains(e.Id));
        }
        if (term.Length > 0)
            emps = emps.Where(e => e.FirstName.Contains(term) || e.LastName.Contains(term)
                                   || e.PersonnelCode.Contains(term));

        var rows = await emps.OrderBy(e => e.PersonnelCode).Take(limit)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.PersonnelCode,
                               Dept = e.Department != null ? e.Department.Name : "",
                               Co = e.Company != null ? e.Company.Name : "" })
            .ToListAsync();

        var bios = new Dictionary<string, string>();
        if (bio && rows.Count > 0)
        {
            var ids = rows.Select(r => r.Id).ToList();
            var b = await db.Set<EmployeeBiometric>().AsNoTracking()
                .Where(x => ids.Contains(x.EmployeeId))
                .GroupBy(x => new { x.EmployeeId, x.Type })
                .Select(g => new { g.Key.EmployeeId, g.Key.Type, n = g.Count() })
                .ToListAsync();
            foreach (var g in b)
            {
                var kind = g.Type switch
                {
                    "fp" => HrT.T("اثر انگشت"),
                    "face" => HrT.T("چهره"),
                    "card" => HrT.T("کارت"),
                    "palm" => HrT.T("کف دست"),
                    _ => HrT.T("رمز"),
                };
                var prev = bios.GetValueOrDefault(g.EmployeeId);
                bios[g.EmployeeId] = (prev.Length > 0 ? prev + " · " : "") + kind + " " + Jalali.ToPersianDigits(g.n);
            }
        }

        var items = rows.Select(r => new
        {
            id = r.Id,
            label = $"{r.FirstName} {r.LastName}".Trim(),
            sub = string.Join(" · ",
                new[] { Jalali.ToPersianDigits(r.PersonnelCode), r.Co, r.Dept, bios.GetValueOrDefault(r.Id) }
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
        }).ToList();
        return Json(new { items });
    }
}
