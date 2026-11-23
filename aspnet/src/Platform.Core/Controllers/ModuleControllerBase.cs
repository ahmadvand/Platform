using System.Text.Json;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۶۱ — کنترلر پایهٔ ماژول‌ها (بدون DbContext هاست).
/// ماژول‌ها به‌جای composite AppDbContext از کانتکست دیتای خودشان و
/// BaseDbContext (دادهٔ پایه) استفاده می‌کنند؛ حسابرسی هم از IAuditSink
/// (پیاده‌سازی هاست) عبور می‌کند تا هیچ ارجاع مستقیمی به هاست/ماژول دیگر نباشد.
/// همهٔ کمک‌متدهای UX با همان امضای MvcBase حفظ شده‌اند.
/// </summary>
public abstract class ModuleControllerBase : Controller
{
    protected readonly ICurrentUser Me;
    protected readonly IAuditSink Audits;

    protected ModuleControllerBase(ICurrentUser me, IAuditSink audits) { Me = me; Audits = audits; }

    protected SessionUser? Sess => Me.Get();
    protected string Ip => AuthCore.IpOf(HttpContext) ?? "";

    protected Task Audit(string action, string entity, string? entityId, object? detail) =>
        Audits.Audit(Sess is null ? null : new SessionUserDto(Sess.Id, Sess.FullName), action, entity, entityId, detail, Ip);

    protected void ToastOk(string msg) => TempData["ToastOk"] = msg;
    protected void ToastErr(string msg) => TempData["ToastErr"] = msg;

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

    /// <summary>پاسخ استاندارد AJAX — پیام بهست زنده در فرانت نمایش داده می‌شود.</summary>
    protected IActionResult AjaxResult(bool ok, string message, object? extra = null) =>
        Json(extra is null ? new { ok, message } : Merge(new { ok, message }, extra));

    /// <summary>
    /// پایان دوحالته مشترک عملیات (تسک ۲۳): حالت AJAX پیام را مستقیم در JSON برمی‌گرداند
    /// و TempData ست نمی‌شود (صفر ریسک بنر شبح در ناوبری بعدی)؛ حالت عادی بنر TempData + ریدایرکت.
    /// </summary>
    protected IActionResult ToastDone(bool ok, string msg, string redirectUrl)
    {
        if (Ajax) return AjaxResult(ok, msg);
        TempData[ok ? "ToastOk" : "ToastErr"] = msg;
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
