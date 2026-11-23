using System.Text.Json;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.DeviceGateway.Runtime;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Web.Controllers;

/// <summary>ردیف ماتریس دستگاه‌ها برای ویو (نوع صریح — بدون dynamic).</summary>
public sealed record GatewayDeviceRow(
    string Serial, string Name, string Transport, string? Ip, int? Port, int? CommKey,
    int PollIntervalSec, bool Polling, bool Online, DateTime? LastPoll, string? LastError,
    int Pending, long OkCmds, long FailedCmds, long Punches, bool Recent);

/// <summary>ردیف جدول دوربین‌ها برای ویو.</summary>
public sealed record GatewayCameraRow(
    string Serial, string Name, string? Ip, string? Brand, string? GateId,
    int HeartbeatSec, bool Loop, bool Barrier, DateTime? LastSeen, bool Recent);

/// <summary>
/// پنل مدیریت «دروازهٔ دستگاه‌ها» (فاز ۶۳) — ماژول درون‌اپی جایگزین سرویس ویندوزی.
/// وضعیت زندهٔ pollerها و حلقه‌های دوربین، ماتریس اتصال دستگاه‌ها، لاگ فعالیت و
/// مسیرهای پل ADMS/ANPR. دادهٔ زنده از GatewayRuntime (حافظهٔ ماژول) +
/// کانال قراردادی (همان منابع سرویس خارجی) می‌آید.
/// </summary>
[HasPerm("devices.view")]
public class DeviceGatewayController : HrModuleControllerBase
{
    private readonly GatewayRuntime _rt;
    private readonly IServiceScopeFactory _scopes;
    private readonly ExternalServiceOptions _ext;
    private readonly IHttpClientFactory _http;

    public DeviceGatewayController(GatewayRuntime rt, IServiceScopeFactory scopes,
        ExternalServiceOptions ext, IHttpClientFactory http,
        ICurrentUser me, IAuditSink audits) : base(me, audits)
    { _rt = rt; _scopes = scopes; _ext = ext; _http = http; }

    // ============================ داشبورد ============================
    public async Task<IActionResult> Index()
    {
        GatewaySettingsDto? s = null;
        List<GatewayDeviceCfg> devices = new();
        List<GatewayCameraCfg> cams = new();
        bool parkingOk = true;
        string? parkingMsg = null;

        try
        {
            using var scope = _scopes.CreateScope();
            var sp = scope.ServiceProvider;
            s = await sp.GetRequiredService<IDeviceGatewayChannel>().GetSettingsAsync();
            devices = await sp.GetRequiredService<IDeviceGatewayChannel>().GetDevicesAsync();
            var parking = sp.GetService<IParkingGatewaySink>();
            if (parking is not null) cams = await parking.GetCamerasAsync();
            else { parkingOk = false; parkingMsg = "ماژول پارکینگ در این نسخه نصب نیست — حلقهٔ دوربین‌های پلاک‌خوان غیرفعال است"; }
        }
        catch (Exception ex)
        {
            parkingMsg = "دریافت کانفیگ از بانک ناموفق بود: " + ex.Message;
        }

        var cfg = _rt.Config;
        var now = DateTime.Now;

        // ماتریس دستگاه‌ها — کانفیگ + حالت زنده
        ViewBag.Devices = devices.Select(d =>
        {
            var live = _rt.Live.TryGetValue(d.Serial, out var l) ? l : null;
            var polling = cfg.Pollers.ContainsKey(d.Serial);
            return new GatewayDeviceRow(
                d.Serial,
                d.Name ?? d.Serial,
                d.Transport,
                d.Ip,
                d.Port is > 0 ? d.Port : null,
                d.CommKey is >= 0 ? d.CommKey : null,
                d.PollIntervalSec,
                polling,
                live?.Online ?? false,
                live?.LastPoll,
                live?.LastError,
                live?.PendingCommands ?? 0,
                live?.OkCommands ?? 0,
                live?.FailedCommands ?? 0,
                live?.PunchesTotal ?? 0,
                live?.LastPoll is not null && (now - live.LastPoll.Value).TotalSeconds < 180);
        }).OrderBy(d => d.Transport).ThenBy(d => d.Name).ToList();

        // دوربین‌های پلاک‌خوان (وقتی پارکینگ نصب است)
        ViewBag.Cameras = cams.Select(c =>
        {
            var loop = cfg.CameraLoops.ContainsKey(c.Serial);
            var live = _rt.Live.TryGetValue(c.Serial, out var l) ? l : null;
            var tpl = cfg.Cameras.TryGetValue(c.Serial, out var e) ? e.Tpl : null;
            return new GatewayCameraRow(
                c.Serial,
                c.Name ?? c.Serial,
                c.Ip,
                c.Brand,
                c.GateId,
                cfg.Cameras.TryGetValue(c.Serial, out var ee) ? ee.HeartbeatSec : c.HeartbeatSec,
                loop,
                tpl?.OpenUrl is not null,
                live?.LastPoll,
                live?.LastPoll is not null && (now - live.LastPoll.Value).TotalSeconds < 180);
        }).OrderBy(c => c.Name).ToList();

        ViewBag.Settings = s;
        ViewBag.Cfg = cfg;
        ViewBag.ParkingOk = parkingOk;
        ViewBag.ParkingMsg = parkingMsg;
        ViewBag.PendingAdms = _rt.Pending.Count;
        ViewBag.Log = _rt.Recent(120);
        ViewBag.CanEdit = Sess?.Has("gateway.edit") ?? false;
        // فاز ۶۴ — کارت سرویس خارجی + وضعیت relay پورت اختصاصی
        ViewBag.ExtConfigured = _ext.Configured;
        ViewBag.ExtUrl = _ext.Url.Trim();
        ViewBag.RelayRunning = _rt.Relay.Running;
        ViewBag.RelayPort = _rt.Relay.Port;
        ViewBag.RelayUpstream = _rt.Relay.UpstreamPort;
        ViewBag.RelayActive = _rt.Relay.ActiveConnections;
        ViewBag.RelayError = _rt.Relay.LastError;
        return View();
    }

    // ================== سرویس خارجی (استقرار ترکیبی — فاز ۶۴) ==================

    /// <summary>
    /// سلامت سرویس گیت‌وی خارجی (HrDeviceGateway ویندوزی) — برای استقرارهای ترکیبی که
    /// علاوه بر ماژول درون‌اپی، سرویس جداگانه هم روی همین بانک کار می‌کند. آدرس از
    /// appsettings (Gateway:ExternalService:Url) می‌آید؛ endpoint /health همان سرویس
    /// خوانده و وضعیت runtime (enabled/configVersion/pollers/restartRequired) برمی‌گردد.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExternalStatus()
    {
        if (!_ext.Configured)
            return Json(new { ok = true, configured = false,
                message = "آدرس سرویس خارجی در appsettings (Gateway:ExternalService:Url) پیکربندی نشده است" });

        var baseUrl = _ext.Url.Trim().TrimEnd('/');
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var client = _http.CreateClient("gw-external");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_ext.TimeoutSec, 1, 15));
            using var res = await client.GetAsync(baseUrl + "/health");
            sw.Stop();
            var body = await res.Content.ReadAsStringAsync();
            string? status = null; int? cfgVersion = null; int? pollers = null;
            bool? enabled = null, restart = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                status = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
                if (doc.RootElement.TryGetProperty("runtime", out var rt))
                {
                    if (rt.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.True) enabled = true;
                    else if (rt.TryGetProperty("enabled", out var en2) && en2.ValueKind == JsonValueKind.False) enabled = false;
                    if (rt.TryGetProperty("configVersion", out var cv) && cv.TryGetInt32(out var cvi)) cfgVersion = cvi;
                    if (rt.TryGetProperty("pollers", out var pl) && pl.TryGetInt32(out var pli)) pollers = pli;
                    if (rt.TryGetProperty("restartRequired", out var rr) && rr.ValueKind == JsonValueKind.True) restart = true;
                }
            }
            catch { /* بدنهٔ غیر JSON — فقط وضعیت HTTP گزارش می‌شود */ }

            return Json(new
            {
                ok = true,
                configured = true,
                url = baseUrl,
                reachable = res.IsSuccessStatusCode,
                httpStatus = (int)res.StatusCode,
                status, enabled, configVersion = cfgVersion, pollers, restartRequired = restart == true,
                elapsedMs = (int)sw.ElapsedMilliseconds,
                message = res.IsSuccessStatusCode ? "سرویس خارجی در دسترس است" : $"سرویس خارجی پاسخ HTTP {(int)res.StatusCode} داد",
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Json(new
            {
                ok = true,
                configured = true,
                url = baseUrl,
                reachable = false,
                elapsedMs = (int)sw.ElapsedMilliseconds,
                message = "سرویس خارجی در دسترس نیست — " + ex.Message,
            });
        }
    }

    // ============================ لاگ زنده (AJAX) ============================
    [HttpGet]
    public IActionResult Log(int take = 120)
    {
        var items = _rt.Recent(Math.Clamp(take, 10, 300));
        return Json(new
        {
            ok = true,
            time = DateTime.Now.ToString("HH:mm:ss"),
            items = items.Select(a => new
            {
                time = a.Time.ToString("HH:mm:ss"),
                level = a.Level,
                source = a.Source,
                message = a.Message,
            }),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("gateway.edit")]
    public IActionResult ClearLog()
    {
        _rt.ClearLog();
        _rt.Log("INFO", "gateway", "لاگ فعالیت پاک شد");
        return ToastDone(true, "لاگ فعالیت پاک شد", "/DeviceGateway");
    }

    /// <summary>توقف فوری همهٔ pollerها — supervisor در چرخهٔ بعدی دوباره راه می‌اندازد (برای پشتیبانی/تست).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("gateway.edit")]
    public IActionResult RestartPollers()
    {
        int n = _rt.Config.Pollers.Count;
        int m = _rt.Config.CameraLoops.Count;
        foreach (var serial in _rt.Config.Pollers.Keys.ToList())
            _rt.Config.Pollers.TryRemove(serial, out _);
        foreach (var serial in _rt.Config.CameraLoops.Keys.ToList())
            _rt.Config.CameraLoops.TryRemove(serial, out _);
        _rt.Log("INFO", "gateway", $"راه‌اندازی مجدد دستی ({n} پولر + {m} حلقهٔ دوربین) — اعمال در چرخهٔ بعدی supervisor");
        return ToastDone(true, "دستور راه‌اندازی مجدد ثبت شد — حداکثر در چرخهٔ بعدی supervisor اعمال می‌شود", "/DeviceGateway");
    }
}
