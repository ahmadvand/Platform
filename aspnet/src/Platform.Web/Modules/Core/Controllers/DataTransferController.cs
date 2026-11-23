using Microsoft.AspNetCore.Mvc;
using Platform.Web.Data;
using Platform.Web.Infrastructure;
using Platform.Web.Services;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۹۹ (بازسازی) — مرکز یکپارچهٔ درون‌ریزی/برون‌ریزی (/DataTransfer)
/// ----------------------------------------------------------------------
/// یک صفحهٔ واحد برای همهٔ عملیات ورود/خروج دادهٔ سامانه:
///   • پرسنل: درون‌ریزی اکسل (ورود گروهی) + قالب + برون‌ریزی اکسل
///   • تردد: برون‌ریزی سریع یک روز + قالب‌های برون‌بری پیشرفته
///   • بستهٔ زبان: برون‌ریزی/درون‌ریزی ترجمه‌ها
///   • به‌روزرسانی و پشتیبان‌گیری پایگاه داده
/// صفحهٔ «مرکز» خودش منطق داده ندارد — فقط لینک/فرم به اندپوینت‌های موجود است و
/// هر کارت فقط با داشتن دسترسیِ همان عملیات رندر می‌شود (بدون دور زدن RBAC).
/// </summary>
public class DataTransferController : MvcBase
{
    public DataTransferController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    [HttpGet("/DataTransfer")]
    public IActionResult Index()
    {
        var sess = Sess;
        if (sess is null) return Redirect("/Auth/Login");

        // کارت زبان: فهرست زبان‌های فعال برای انتخاب برون‌ریزی
        var langs = HttpContext.RequestServices.GetRequiredService<HrLangStore>().All();
        ViewBag.Langs = langs;
        // فاز ۱۲۲ — کاتالوگ اعلامی ماژول‌ها (فقط ماژول‌های حاضر و فعال — همان ملاک منو)
        var modsvc = HttpContext.RequestServices.GetRequiredService<Platform.Web.Modules.ModuleStateService>();
        ViewBag.Catalog = Platform.Web.Services.DataExchange.Catalog(Db, code => modsvc.Enabled(code));
        return View();
    }
}
