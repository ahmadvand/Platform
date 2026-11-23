using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Devices;

/// <summary>
/// پنل مدیریت تنظیمات سرویس گیت‌وی C# (HrDeviceGateway) — فاز ۲۱.
/// تنظیمات در دیتابیس مرکزی ذخیره و توسط گیت‌وی از api/gateway/config به‌صورت پویا خوانده می‌شود.
/// </summary>
[HasPerm("gateway.view")]
public class GatewayController : MvcBase
{
    public GatewayController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index()
    {
        var s = await GatewayConfig.GetAsync(Db);
        ViewBag.CanEdit = Sess?.Has("gateway.edit") ?? false;

        // دستگاه‌ها بر اساس نحوه اتصال — همان چیزی که گیت‌وی از api/gateway/config دریافت می‌کند
        var devices = await Db.Set<Device>()
            .Where(d => d.Active)
            .OrderBy(d => d.Transport)
            .ThenBy(d => d.Name)
            .Select(d => new { d.Name, d.Serial, d.Transport, d.Ip, d.DevicePort, d.CommKey, d.PollingInterval })
            .ToListAsync();
        ViewBag.Devices = devices;

        return View(s);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("gateway.edit")]
    public async Task<IActionResult> Save(GatewaySetting form)
    {
        var s = await GatewayConfig.GetAsync(Db);
        s.Enabled = form.Enabled;
        s.ListenPort = form.ListenPort;
        s.TcpTimeoutMs = form.TcpTimeoutMs;
        s.PollIntervalSec = form.PollIntervalSec;
        s.SweeperIntervalSec = form.SweeperIntervalSec;
        s.ConfigRefreshSec = form.ConfigRefreshSec;
        s.LogLevel = (form.LogLevel ?? "Information").Trim();
        s.DefaultDevicePort = form.DefaultDevicePort;
        s.DefaultCommKey = form.DefaultCommKey;

        var err = GatewayConfig.Validate(s);
        if (err is not null)
        {
            ToastErr(err);
            if (Ajax) return AjaxResult(false, err);
            return RedirectToAction(nameof(Index));
        }

        s.ConfigVersion++;      // گیت‌وی با مقایسه نسخه، تغییر را اعمال می‌کند
        s.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("edit_gateway_settings", "GatewaySetting", s.Id,
            new { s.Enabled, s.ListenPort, s.PollIntervalSec, s.ConfigVersion });
        var msg = "تنظیمات گیت‌وی ذخیره شد — سرویس حداکثر در چرخه بعدی اعمال می‌کند";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg, new { configVersion = s.ConfigVersion });
        return RedirectToAction(nameof(Index));
    }

    /// <summary>اجرای فوری فرمان «دریافت تنظیمات» توسط گیت‌وی را شبیه‌سازی/آزمون می‌کند (فقط پیام راهنما).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("gateway.edit")]
    public async Task<IActionResult> ResetFetchIndicator()
    {
        var s = await GatewayConfig.GetAsync(Db);
        s.LastFetchedAt = null;
        await Db.SaveChangesAsync();
        await Audit("reset_gateway_fetch_indicator", "GatewaySetting", s.Id, null);
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return RedirectToAction(nameof(Index));
    }
}
