using System.Text.Json;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۶۱ — کنترلر پایهٔ ماژول‌ها (بدون DbContext هاست).
/// ماژول‌ها به‌جای composite AppDbContext از کانتکست دیتای خودشان و
/// HrBaseDbContext (دادهٔ پایه) استفاده می‌کنند؛ حسابرسی هم از IAuditSink
/// (پیاده‌سازی هاست) عبور می‌کند تا هیچ ارجاع مستقیمی به هاست/ماژول دیگر نباشد.
/// همهٔ کمک‌متدهای UX با همان امضای MvcBase حفظ شده‌اند.
/// </summary>
public abstract class HrModuleControllerBase : Controller
{
    protected readonly ICurrentUser Me;
    protected readonly IAuditSink Audits;

    protected HrModuleControllerBase(ICurrentUser me, IAuditSink audits) { Me = me; Audits = audits; }

    protected SessionUser? Sess => Me.Get();
    protected string Ip => AuthCore.IpOf(HttpContext) ?? "";

    protected Task Audit(string action, string entity, string? entityId, object? detail) =>
        Audits.Audit(Sess is null ? null : new SessionUserDto(Sess.Id, Sess.FullName), action, entity, entityId, detail, Ip);

    // ------------------------------------------------------------
    // تسک «پیام‌های اعتبارسنجی چندزبانه» — نقطهٔ ترجمهٔ مرکزی:
    // هر پیام شیرین‌شده (Toast/Ajax) هاست و همهٔ ماژول‌ها از این چهار نقطه
    // عبور می‌کند؛ ترجمه در همین‌جا (به زبان جاری کاربر از CultureMiddleware)
    // انجام می‌شود تا هیچ کنترلری لازم نباشد خودش HrT.T بگوید.
    // نبودِ کلید در دیکشنری = خودِ متن فارسی (fallback امن — پوشش تدریجی).
    // ------------------------------------------------------------
    protected void ToastOk(string msg) => TempData["ToastOk"] = HrT.T(msg);
    protected void ToastErr(string msg) => TempData["ToastErr"] = HrT.T(msg);

    /// <summary>هم‌راستاسازی تم PDF با پالت فعال ادمین — پیش از هر تولید PDF (فاز ۳۲).</summary>
    protected Task ApplyPdfThemeAsync() =>
        HttpContext.RequestServices.GetRequiredService<SettingsService>().ApplyPdfThemeAsync();

    /// <summary>درخواست AJAX (fetch فرانت با هدر X-Requested-With) است؟ — پاسخ JSON به‌جای redirect.</summary>
    protected bool Ajax => Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";

    /// <summary>فرم در حالت partial (پاپ‌آپ AJAX) درخواست شده؟ — ویو بدون layout.</summary>
    protected bool Partial => Request.Query["partial"].ToString() == "1";

    /// <summary>
    /// رندر ویو فرم: حالت عادی با layout، حالت partial بدون layout (برای مودال AJAX).
    /// هر دو حالت همان فایل ویو هستند — بدون تکرار markup.
    /// </summary>
    protected IActionResult FormView(string viewName, object? model = null)
    {
        if (Partial) return model is null ? PartialView(viewName) : PartialView(viewName, model);
        return model is null ? View(viewName) : View(viewName, model);
    }

    /// <summary>پاسخ استاندارد AJAX — پیام بهست زنده در فرانت نمایش داده می‌شود (ترجمه‌شده به زبان کاربر).</summary>
    protected IActionResult AjaxResult(bool ok, string message, object? extra = null) =>
        Json(extra is null ? new { ok, message = HrT.T(message) } : Merge(new { ok, message = HrT.T(message) }, extra));

    /// <summary>
    /// پایان دوحالته مشترک عملیات (تسک ۲۳): حالت AJAX پیام را مستقیم در JSON برمی‌گرداند
    /// و TempData ست نمی‌شود (صفر ریسک بنر شبح در ناوبری بعدی)؛ حالت عادی بنر TempData + ریدایرکت.
    /// </summary>
    protected IActionResult ToastDone(bool ok, string msg, string redirectUrl)
    {
        if (Ajax) return AjaxResult(ok, msg);
        TempData[ok ? "ToastOk" : "ToastErr"] = HrT.T(msg);
        return Redirect(redirectUrl);
    }

    private static object Merge(object a, object b)
    {
        var d = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(a));
        foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(b)) ?? new())
            d![kv.Key] = kv.Value;
        return d!;
    }

    protected static string JsonOf(object? o) => o is null ? "null" : JsonSerializer.Serialize(o);
}
