using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace HrModules.Devices.Api;

/// <summary>
/// کانفیگ پویای سرویس گیت‌وی C# — فاز ۲۱.
/// GET /api/gateway/config با Bearer حساب سرویس پل (مجوز devices.poll):
///   { settings {…}, devices:[{serial,name,transport,ip,port,commKey,pollIntervalSec,enabled}] }
/// گیت‌وی هر ConfigRefreshSec ثانیه این متد را صدا می‌زند و تغییرات را اعمال می‌کند.
/// </summary>
[ApiController]
[Route("api/gateway")]
public class GatewayConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    public GatewayConfigController(AppDbContext db) => _db = db;

    [HttpGet("config")]
    [Authorize]
    [HasPerm("devices.poll")]
    public async Task<IActionResult> GetConfig()
    {
        var s = await GatewayConfig.GetAsync(_db);
        await GatewayConfig.MarkFetchedAsync(_db); // نشانگر سلامت برای پنل ادمین

        var devices = await _db.Set<Device>()
            .Where(d => d.Active)
            .OrderBy(d => d.Serial)
            .Select(d => new
            {
                serial = d.Serial,
                name = d.Name,
                transport = d.Transport,
                ip = d.Ip,
                port = d.DevicePort,
                commKey = d.CommKey,
                pollIntervalSec = d.PollingInterval,
                enabled = d.Active,
            })
            .ToListAsync();

        // دوربین‌های پلاک‌خوان فعال — فاز ۴۴: گیت‌وی برای هرکدام حلقه ضربان می‌سازد
        // (MarkSeen → «آنلاین» در پنل) و دستورات راهبند صف‌شده را تحویل/تایید می‌کند
        // فاز ۶۷ — دوربین‌های پلاک‌خوان فقط با لایهٔ دیتای ماژول پارکینگ (حضور فایل DLL؛ الگوی NoInlining)
        var cameras = await CamerasSafeAsync(_db);

        return Ok(new
        {
            version = s.ConfigVersion,
            updatedAt = s.UpdatedAt,
            settings = new
            {
                enabled = s.Enabled,
                listenPort = s.ListenPort,
                tcpTimeoutMs = s.TcpTimeoutMs,
                pollIntervalSec = s.PollIntervalSec,
                sweeperIntervalSec = s.SweeperIntervalSec,
                configRefreshSec = s.ConfigRefreshSec,
                logLevel = s.LogLevel,
                defaultDevicePort = s.DefaultDevicePort,
                defaultCommKey = s.DefaultCommKey,
            },
            devices,
            cameras,
        });
    }

    /// <summary>فاز ۶۸ — دوربین‌های ANPR ∈ پلاگین پارکینگ (قرارداد IParkingReportSource؛ شکل JSON همان قرارداد فرانت).</summary>
    private Task<List<object>> CamerasSafeAsync(AppDbContext db) =>
        HttpContext.RequestServices.GetRequiredService<Platform.Contracts.IParkingReportSource>().CamerasAsync();
}
