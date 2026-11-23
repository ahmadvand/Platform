using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

// فاز ۹۲ — پرمیشن‌ها به‌صورت اکشن‌به‌اکشن اعمال می‌شوند تا ReadAlert برای همهٔ کاربران
// احراز هویت‌شده در دسترس باشد (زنگ اعلان برای همه نمایش داده می‌شود و کلیک روی هر آیتم
// باید «خوانده‌شده» را ثبت کند؛ خود ثبت صرفاً از فهرست بازِ همان کاربر خارج می‌شود).
public class NotificationsController : MvcBase
{
    private readonly ILogger<NotificationsController> _log;
    private readonly HrBaseDbContext Base;
    private readonly IAttendanceInsights att;
    public NotificationsController(AppDbContext db, HrBaseDbContext baseDb, IAttendanceInsights att, ICurrentUser me, ILogger<NotificationsController> log) : base(db, me)
    {
        _log = log;
        Base = baseDb;
        this.att = att;
    }

    [HasPerm("notifications.view")]
    public async Task<IActionResult> Index()
    {
        var rules = await Db.NotificationRules.OrderBy(r => r.Type).ThenBy(r => r.Name).ToListAsync();
        var logs = await Db.NotificationLogs.OrderByDescending(l => l.CreatedAt).Take(50).ToListAsync();
        // فاز ۹۲ (اشکال کاربر) — صفحهٔ «مشاهده همه اعلان‌ها» خالی بود: logs در ViewBag
        // قرار نمی‌گرفت. اکنون لاگ‌ها + هشدارهای بازِ زنگ (SecurityAlerts) هر دو نمایش داده می‌شوند.
        ViewBag.Logs = logs;
        ViewBag.Alerts = await Base.SecurityAlerts
            .Where(a => !a.Resolved && !a.Expired)
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .ToListAsync();
        var settings = await NotificationEngine.GetSettingsAsync(Base);
        ViewBag.Settings = settings;
        ViewBag.RuleTypes = NotificationEngine.RuleTypes;
        ViewBag.CanManage = Sess?.Has("notifications.manage") ?? false;
        return View(rules);
    }

    /// <summary>فاز ۹۰ — «خوانده شد» زنگ اعلان نوار بالایی: هشدار باز به‌عنوان حل‌شده علامت
    /// می‌خورد تا دیگر در فهرست «هشدارهای باز» زنگ نمایش داده نشود؛ هدایت به صفحهٔ مربوطه
    /// در سمت مرورگر انجام می‌شود (آیتم زنگ یک لینک است).
    /// فاز ۹۲ — برای همهٔ کاربران احراز هویت‌شده باز است (زنگ برای همه نمایش داده می‌شود و
    /// پیش‌تر برای کاربران بدون پرمیشن، ریدایرکت /Auth/Denied به‌جای ثبت برمی‌گشت و هشدار
    /// باز می‌ماند). «خوانده‌شدن» صرفاً از فهرست باز خارج می‌کند، نه تغییر دادهٔ امنیتی.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAlert(string id)
    {
        if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
        var alert = await Base.SecurityAlerts.FindAsync(id);
        if (alert is null) return Json(new { ok = false, message = HrT.T("هشدار یافت نشد") });
        alert.Resolved = true;
        alert.UpdatedAt = DateTime.Now;
        await Base.SaveChangesAsync();
        await Audit("read_alert_bell", "SecurityAlert", id, new { alert.Type });
        return Json(new { ok = true, message = HrT.T("هشدار به‌عنوان خوانده‌شده علامت خورد") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("notifications.manage")]
    public async Task<IActionResult> SaveRule(string? id, string name, string type, int threshold,
        string? recipientEmail, string? recipientPhone, bool viaEmail, bool viaSms, bool viaWebhook, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(name) || NotificationEngine.RuleTypes.All(t => t.Type != type))
        {
            ToastErr(HrT.T("نام قاعده یا نوع قاعده معتبر نیست"));
            if (Ajax) return AjaxResult(false, "نام قاعده یا نوع قاعده معتبر نیست");
            return RedirectToAction(nameof(Index));
        }
        var isNew = string.IsNullOrEmpty(id);
        var rule = isNew ? new NotificationRule() : await Db.NotificationRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rule is null) return NotFound();

        rule.Name = name.Trim();
        rule.Type = type;
        rule.Threshold = Math.Clamp(threshold, 1, 1440);
        rule.RecipientEmail = string.IsNullOrWhiteSpace(recipientEmail) ? null : recipientEmail.Trim();
        rule.RecipientPhone = string.IsNullOrWhiteSpace(recipientPhone) ? null : recipientPhone.Trim();
        rule.ViaEmail = viaEmail;
        rule.ViaSms = viaSms;
        rule.ViaWebhook = viaWebhook; // فاز ۱۳۵ (مورد ۱۸) — کانال وب‌هوک
        rule.Enabled = enabled;
        if (isNew) Db.NotificationRules.Add(rule);
        await Db.SaveChangesAsync();
        await Audit(isNew ? "create_notification_rule" : "edit_notification_rule", "NotificationRule", rule.Id, new { rule.Name, rule.Type });
        ToastOk(isNew ? HrT.T("قاعده اعلان جدید ذخیره شد") : HrT.T("قاعده اعلان ویرایش شد"));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("notifications.manage")]
    public async Task<IActionResult> ToggleRule(string id)
    {
        var rule = await Db.NotificationRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rule is null) return NotFound();
        rule.Enabled = !rule.Enabled;
        await Db.SaveChangesAsync();
        await Audit("toggle_notification_rule", "NotificationRule", rule.Id, new { rule.Enabled });
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("notifications.manage")]
    public async Task<IActionResult> DeleteRule(string id)
    {
        var rule = await Db.NotificationRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rule is not null)
        {
            Db.NotificationRules.Remove(rule);
            await Db.SaveChangesAsync();
            await Audit("delete_notification_rule", "NotificationRule", rule.Id, new { rule.Name });
            ToastOk(HrT.T("قاعده اعلان حذف شد"));
        }
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("notifications.manage")]
    public async Task<IActionResult> Settings(NotificationSetting model)
    {
        var s = await NotificationEngine.GetSettingsAsync(Base);
        s.EnabledEmail = model.EnabledEmail;
        s.EnabledSms = model.EnabledSms;
        s.SmtpHost = model.SmtpHost?.Trim() ?? "";
        s.SmtpPort = Math.Clamp(model.SmtpPort, 1, 65535);
        s.SmtpSsl = model.SmtpSsl;
        s.SmtpUser = model.SmtpUser?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(model.SmtpPass)) s.SmtpPass = model.SmtpPass; // خالی = بدون تغییر
        s.FromEmail = model.FromEmail?.Trim() ?? "";
        s.AdminEmails = model.AdminEmails?.Trim() ?? "";
        s.SmsApiUrl = model.SmsApiUrl?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(model.SmsApiKey)) s.SmsApiKey = model.SmsApiKey;
        s.SmsSender = model.SmsSender?.Trim() ?? "";
        s.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("edit_notification_settings", "NotificationSetting", s.Id, new { s.EnabledEmail, s.EnabledSms, s.SmtpHost });
        ToastOk(HrT.T("تنظیمات اعلان ذخیره شد"));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>اجرای آزمایشی فوری همه قواعد فعال.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("notifications.manage")]
    public async Task<IActionResult> RunNow()
    {
        // فاز ۶۹ — انقضای قرارداد ∈ org و دستگاه آفلاین ∈ devices (قرارداد؛ غایب = صفر hit)
        var people = HttpContext.RequestServices.GetRequiredService<IPeopleDirectory>();
        var devg = HttpContext.RequestServices.GetRequiredService<IDevicesGateway>();
        var n = await NotificationEngine.RunAllAsync(Base, att, people, devg, _log);
        await Audit("run_notifications", "NotificationRule", null, new { sent = n });
        ToastOk(n > 0
            ? $"اجرای آزمایشی انجام شد — {n} اعلان ارسال/ثبت شد"
            : "اجرای آزمایشی انجام شد — مورد قابل اعلانی یافت نشد (یا در ۶ ساعت اخیر ارسال شده)");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return RedirectToAction(nameof(Index));
    }
}
