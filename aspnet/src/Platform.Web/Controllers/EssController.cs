using Microsoft.AspNetCore.Mvc;

namespace Platform.Web.Controllers;

/// <summary>
/// پرتال خودخدمتی مستقل در تسک ۲۳ (ادغام ESS در پنل یکپارچه) بازنشسته شد:
/// همه امکانات به «پرتال من» (SelfController) با دسترسی‌های self.* منتقل شده و ورود کارکنان
/// از صفحه ورود اصلی (/Auth/Login → فرم کد پرسنلی + کد ملی) انجام می‌شود.
/// این کنترلر فقط سازگاری بوکمارک‌های قدیمی را با ریدایرکت نگه می‌دارد.
/// </summary>
public class EssController : Controller
{
    [HttpGet] public IActionResult Index() => Redirect("/Self");
    [HttpGet] public IActionResult Login() => Redirect("/Auth/Login");
    [HttpGet] public IActionResult Requests() => Redirect("/Self/Requests");
    [HttpGet] public IActionResult Attendance() => Redirect("/Self/Attendance");
    [HttpGet] public IActionResult Payslips() => Redirect("/Self/Payslips");
    [HttpGet] public IActionResult Payslip(string? id) => Redirect(string.IsNullOrEmpty(id) ? "/Self/Payslips" : $"/Self/Payslip/{id}");

    [HttpPost]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("hr_ess");
        return Redirect("/Auth/Login");
    }
}
