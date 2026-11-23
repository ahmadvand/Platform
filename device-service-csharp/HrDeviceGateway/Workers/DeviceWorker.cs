using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using HrDeviceGateway.Config;
using HrDeviceGateway.Models;
using HrDeviceGateway.Protocol;
using HrDeviceGateway.Services;
using Microsoft.Extensions.Options;

namespace HrDeviceGateway.Workers;

/// <summary>
/// پیکربندی زنده گیت‌وی — از پنل ادمین (api/gateway/config) به‌صورت پویا به‌روزرسانی می‌شود (فاز ۲۱).
/// مقادیر اولیه از appsettings.json (bootstrap) پر و سپس با کانفیگ سرور همگام می‌شود.
/// </summary>
public sealed class GatewayRuntimeConfig
{
    public volatile bool Enabled = true;
    public volatile int TcpTimeoutMs = 6000;
    public volatile int PollIntervalSec = 60;
    public volatile int SweeperIntervalSec = 60;
    public volatile int ConfigRefreshSec = 30;
    public volatile int DefaultDevicePort = 4370;
    public volatile int DefaultCommKey = 0;
    public volatile int ConfigVersion;               // ۰ = هنوز از سرور دریافت نشده
    public volatile int ListenPortWanted;            // پورت ADMS خواسته‌شده از سرور (اعمال با ری‌استارت)
    public string LogLevel = "Information";

    /// <summary>دستگاه‌های فعال از دید سرور — کلید: سریال.</summary>
    public readonly ConcurrentDictionary<string, DeviceConfig> Devices = new();

    /// <summary>پولرهای در حال اجرا — کلید: سریال.</summary>
    public readonly ConcurrentDictionary<string, (System.Threading.CancellationTokenSource Cts, Task Loop)> Pollers = new();

    /// <summary>دوربین‌های پلاک‌خوان فعال از دید سرور — کلید: سریال (فاز ۴۴).</summary>
    public readonly ConcurrentDictionary<string, CameraConfig> Cameras = new();

    /// <summary>حلقه‌های ضربان دوربین‌ها — کلید: سریال (فاز ۴۴).</summary>
    public readonly ConcurrentDictionary<string, (System.Threading.CancellationTokenSource Cts, Task Loop)> CameraLoops = new();

    public int PollerCount => Pollers.Count;

    public int CameraCount => Cameras.Count;
}

/// <summary>
/// حلقه اصلی سرویس — نسخه پویا (فاز ۲۱):
///  1) supervisor: هر ConfigRefreshSec کانفیگ را از پنل ادمین می‌گیرد → شروع/توقف پولرها + اعمال فاصله‌ها
///  2) یک poller برای هر دستگاه TCP فعال (فاصله poll لحظه‌ای از کانفیگ خوانده می‌شود)
///  3) sweeper: بازبینی وضعیت + گزارش دستگاه‌های آفلاین
/// دستگاه‌های ADMS در Kestrel (AdmsBridge) سرویس می‌شوند و نیازی به poller ندارند.
/// </summary>
public sealed class DeviceWorker : BackgroundService
{
    private readonly ApiClient _api;
    private readonly GatewayOptions _opt;
    private readonly GatewayRuntimeConfig _rt;
    private readonly AdmsBridge.PendingMap _pending;
    private readonly ILogger<DeviceWorker> _log;
    private readonly HttpClient _relay = new() { Timeout = TimeSpan.FromSeconds(6) }; // راهبند دوربین‌ها
    private int _startupListenPort;
    private bool _portWarned;

    public DeviceWorker(ApiClient api, IOptions<GatewayOptions> opt, GatewayRuntimeConfig rt, ILogger<DeviceWorker> log)
    {
        _api = api;
        _opt = opt.Value;
        _rt = rt;
        _log = log;
        _pending = new AdmsBridge.PendingMap();
    }

    public AdmsBridge.PendingMap PendingCommands => _pending;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("=== سرویس گیت‌وی دستگاه‌های تردد (C#/.NET 8) شروع شد — حالت کانفیگ پویا ===");

        // مقادیر اولیه از appsettings (bootstrap) تا اولین دریافت موفق از سرور
        SeedFromAppSettings();

        // ورود با تلاش مجدد (سرور ممکن است هنوز بالا نیامده باشد)
        while (!ct.IsCancellationRequested)
        {
            try { await _api.LoginAsync(ct); break; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError("ورود به سامانه ناموفق: {Message} — تلاش مجدد پس از 10 ثانیه", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }

        // اولین همگام‌سازی کانفیگ (اگر سرور در دسترس باشد؛ در غیر این صورت appsettings برقرار می‌ماند)
        await ApplyRemoteConfigAsync(ct, firstSync: true);

        // شروع pollerهای اولیه + حلقه دوربین‌ها
        StartMissingPollers(ct);
        StartMissingCameraLoops(ct);

        var loops = new List<Task> { SupervisorLoopAsync(ct), SweeperLoopAsync(ct) };
        try { await Task.WhenAll(loops); }
        catch (OperationCanceledException) { /* خاموشی تمیز */ }

        StopAllPollers();
        StopAllCameraLoops();
        _log.LogInformation("=== سرویس متوقف شد ===");
    }

    /// <summary>پرکردن مقادیر اولیه از appsettings.json — تا قبل از اولین همگام‌سازی سرور.</summary>
    private void SeedFromAppSettings()
    {
        _rt.Enabled = true;
        _rt.TcpTimeoutMs = _opt.TcpTimeoutMs;
        _rt.PollIntervalSec = _opt.PollIntervalSec;
        _rt.SweeperIntervalSec = _opt.SweeperIntervalSec;
        _rt.ConfigRefreshSec = 30;
        _rt.DefaultDevicePort = 4370;
        _rt.DefaultCommKey = 0;
        _startupListenPort = _opt.ListenPort;
        _rt.ListenPortWanted = _opt.ListenPort;
        foreach (var d in _opt.Devices)
        {
            _rt.Devices[d.Serial] = new DeviceConfig
            {
                Serial = d.Serial, Name = d.Name, Transport = d.Transport,
                Ip = d.Ip, Port = d.Port, CommKey = d.CommKey, PollIntervalSec = d.PollIntervalSec,
            };
        }
        // دوربین‌های پلاک‌خوان — bootstrap (قالب‌های HTTP راهبند فقط همین‌جا تنظیم می‌شوند) — فاز ۴۴
        foreach (var c in _opt.Anpr.Cameras)
        {
            _rt.Cameras[c.Serial] = new CameraConfig
            {
                Serial = c.Serial, Name = c.Name, Ip = c.Ip, Brand = c.Brand,
                Enabled = c.Enabled,
                HeartbeatSec = c.HeartbeatSec > 0 ? c.HeartbeatSec : _opt.Anpr.HeartbeatSec,
                OpenUrl = string.IsNullOrWhiteSpace(c.OpenUrl) ? null : c.OpenUrl,
                CloseUrl = string.IsNullOrWhiteSpace(c.CloseUrl) ? null : c.CloseUrl,
                RestartUrl = string.IsNullOrWhiteSpace(c.RestartUrl) ? null : c.RestartUrl,
                HttpUser = c.HttpUser, HttpPass = c.HttpPass,
            };
        }
        _log.LogInformation("کانفیگ اولیه از appsettings: {Count} دستگاه ({Tcp} TCP) + {Cams} دوربین پلاک‌خوان",
            _opt.Devices.Count, _opt.Devices.Count(d => d.Transport == "tcp"), _rt.Cameras.Count);
    }

    // ---------- حلقه supervisor: دریافت کانفیگ پویا از پنل ادمین ----------

    private async Task SupervisorLoopAsync(CancellationToken ct)
    {
        while (await WaitAsync(ct))
        {
            try
            {
                var applied = await ApplyRemoteConfigAsync(ct, firstSync: false);
                if (applied) { StartMissingPollers(ct); StartMissingCameraLoops(ct); }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning("خطای supervisor کانفیگ: {Message}", ex.Message);
            }
        }
    }

    private async Task<bool> WaitAsync(CancellationToken ct)
    {
        int sec = Math.Clamp(_rt.ConfigRefreshSec, 10, 3600);
        try { await Task.Delay(TimeSpan.FromSeconds(sec), ct); }
        catch (OperationCanceledException) { return false; }
        return true;
    }

    /// <summary>دریافت و اعمال کانفیگ از سرور — true یعنی کانفیگ جدید اعمال شد.</summary>
    private async Task<bool> ApplyRemoteConfigAsync(CancellationToken ct, bool firstSync)
    {
        var cfg = await _api.GetGatewayConfigAsync(ct);
        if (cfg is null)
        {
            if (firstSync) _log.LogWarning("کانفیگ سرور در دسترس نبود — با مقادیر appsettings ادامه می‌دهیم");
            return false;
        }
        if (!firstSync && cfg.Version == _rt.ConfigVersion) return false; // بدون تغییر

        var s = cfg.Settings;
        bool wasEnabled = _rt.Enabled;
        _rt.Enabled = s.Enabled;
        _rt.TcpTimeoutMs = Math.Clamp(s.TcpTimeoutMs, 1000, 60000);
        _rt.PollIntervalSec = Math.Clamp(s.PollIntervalSec, 15, 3600);
        _rt.SweeperIntervalSec = Math.Clamp(s.SweeperIntervalSec, 30, 3600);
        _rt.ConfigRefreshSec = Math.Clamp(s.ConfigRefreshSec, 10, 3600);
        _rt.DefaultDevicePort = s.DefaultDevicePort > 0 ? s.DefaultDevicePort : 4370;
        _rt.DefaultCommKey = Math.Max(0, s.DefaultCommKey);
        _rt.ConfigVersion = cfg.Version;
        _rt.ListenPortWanted = s.ListenPort;
        _rt.LogLevel = string.IsNullOrWhiteSpace(s.LogLevel) ? "Information" : s.LogLevel;

        // دستگاه‌ها از دید سرور — جایگزین کامل فهرست قبلی
        var remoteTcp = new HashSet<string>();
        foreach (var d in cfg.Devices)
        {
            if (!d.Enabled) continue;
            _rt.Devices[d.Serial] = new DeviceConfig
            {
                Serial = d.Serial,
                Name = d.Name,
                Transport = string.IsNullOrWhiteSpace(d.Transport) ? "adms" : d.Transport.Trim().ToLowerInvariant(),
                Ip = string.IsNullOrWhiteSpace(d.Ip) ? null : d.Ip.Trim(),
                Port = d.Port is > 0 ? d.Port.Value : 0,        // ۰ = پیش‌فرض گیت‌وی
                CommKey = d.CommKey is >= 0 ? d.CommKey.Value : -1, // -۱ = پیش‌فرض گیت‌وی
                PollIntervalSec = d.PollIntervalSec > 0 ? d.PollIntervalSec : 60,
            };
            if (_rt.Devices[d.Serial].Transport == "tcp") remoteTcp.Add(d.Serial);
        }

        // دوربین‌های پلاک‌خوان از دید سرور — فاز ۴۴:
        // سرور ملاک فهرست/وضعیت است؛ قالب‌های HTTP راهبند (vendor-specific) از appsettings حفظ می‌شود
        foreach (var c in cfg.Cameras)
        {
            if (!c.Enabled) continue;
            _rt.Cameras.TryGetValue(c.Serial, out var local);
            _rt.Cameras[c.Serial] = new CameraConfig
            {
                Serial = c.Serial,
                Name = c.Name,
                Ip = string.IsNullOrWhiteSpace(c.Ip) ? local?.Ip : c.Ip.Trim(),
                Brand = c.Brand,
                Enabled = true,
                HeartbeatSec = c.HeartbeatIntervalSec > 0 ? c.HeartbeatIntervalSec : (local?.HeartbeatSec ?? _opt.Anpr.HeartbeatSec),
                OpenUrl = local?.OpenUrl, CloseUrl = local?.CloseUrl, RestartUrl = local?.RestartUrl,
                HttpUser = local?.HttpUser, HttpPass = local?.HttpPass,
            };
        }
        var remoteCams = cfg.Cameras.Where(c => c.Enabled).Select(c => c.Serial).ToHashSet();
        foreach (var (serial, (_, _)) in _rt.CameraLoops)
        {
            if (!remoteCams.Contains(serial))
                StopCameraLoop(serial, "از فهرست دوربین‌های سرور خارج شد");
        }
        foreach (var stale in _rt.Cameras.Keys.Where(s => !remoteCams.Contains(s)).ToList())
            _rt.Cameras.TryRemove(stale, out _);

        // توقف پولر دستگاه‌هایی که دیگر TCP فعال نیستند (حذف‌شده/غیرفعال/ADMS شد)
        foreach (var (serial, (_, _)) in _rt.Pollers)
        {
            if (!remoteTcp.Contains(serial))
                StopPoller(serial, "از فهرست دستگاه‌های TCP سرور خارج شد");
        }

        // پورت ADMS تغییر کرده؟ (فقط با ری‌استارت سرویس اعمال می‌شود)
        if (!_portWarned && _rt.ListenPortWanted != _startupListenPort)
        {
            _portWarned = true;
            _log.LogWarning("⚠ پورت پل ADMS در پنل ادمین از {Old} به {New} تغییر کرده — برای اعمال، سرویس باید ری‌استارت شود",
                _startupListenPort, _rt.ListenPortWanted);
        }

        if (_rt.Enabled != wasEnabled)
            _log.LogInformation("پایش دستگاه‌ها {State} شد (از پنل ادمین)", _rt.Enabled ? "فعال" : "متوقف");

        _log.LogInformation("کانفیگ پویا اعمال شد (نسخه {Version}) — {Devices} دستگاه، {Tcp} TCP پول مستقیم، {Cams} دوربین پلاک‌خوان",
            cfg.Version, cfg.Devices.Count, remoteTcp.Count, _rt.Cameras.Count);
        return true;
    }

    /// <summary>شروع poller برای دستگاه‌های TCP فاقد poller.</summary>
    private void StartMissingPollers(CancellationToken ct)
    {
        foreach (var dev in _rt.Devices.Values.Where(d => d.Transport == "tcp" && !string.IsNullOrEmpty(d.Ip)))
        {
            if (_rt.Pollers.ContainsKey(dev.Serial)) continue;
            var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loop = Task.Run(() => PollLoopAsync(dev.Serial, cts.Token), cts.Token);
            _rt.Pollers[dev.Serial] = (cts, loop);
            _log.LogInformation("Poller دستگاه {Name} ({Serial}) راه‌اندازی شد — {Ip}:{Port}",
                dev.Name ?? dev.Serial, dev.Serial, dev.Ip, dev.Port > 0 ? dev.Port : _rt.DefaultDevicePort);
        }
    }

    private void StopPoller(string serial, string reason)
    {
        if (_rt.Pollers.TryRemove(serial, out var p))
        {
            p.Cts.Cancel();
            _log.LogInformation("Poller دستگاه {Serial} متوقف شد — {Reason}", serial, reason);
        }
    }

    private void StopAllPollers()
    {
        foreach (var serial in _rt.Pollers.Keys.ToList()) StopPoller(serial, "خاموشی سرویس");
    }

    // ---------- دوربین‌های پلاک‌خوان — فاز ۴۴ ----------

    /// <summary>شروع حلقه ضربان برای دوربین‌های فاقد حلقه.</summary>
    private void StartMissingCameraLoops(CancellationToken ct)
    {
        if (!_opt.Anpr.Enabled) return;
        foreach (var cam in _rt.Cameras.Values)
        {
            if (_rt.CameraLoops.ContainsKey(cam.Serial)) continue;
            var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loop = Task.Run(() => CameraLoopAsync(cam.Serial, cts.Token), cts.Token);
            _rt.CameraLoops[cam.Serial] = (cts, loop);
            _log.LogInformation("حلقه دوربین پلاک‌خوان {Name} ({Serial}) راه‌اندازی شد", cam.Name ?? cam.Serial, cam.Serial);
        }
    }

    private void StopCameraLoop(string serial, string reason)
    {
        if (_rt.CameraLoops.TryRemove(serial, out var p))
        {
            p.Cts.Cancel();
            _log.LogInformation("حلقه دوربین {Serial} متوقف شد — {Reason}", serial, reason);
        }
    }

    private void StopAllCameraLoops()
    {
        foreach (var serial in _rt.CameraLoops.Keys.ToList()) StopCameraLoop(serial, "خاموشی سرویس");
    }

    /// <summary>
    /// حلقه هر دوربین (فاز ۴۴): ضربان (MarkSeen سرور) + تحویل/اجرا/تایید دستورات راهبند.
    /// دوربین نیازی به دسترسی مستقیم گیت‌وی ندارد — «آنلاین» بودن در پنل از ضربانِ همین حلقه می‌آید؛
    /// خوانش پلاک هم از مسیر AnprBridge (POST /anpr/plate) به سرور می‌رسد.
    /// </summary>
    private async Task CameraLoopAsync(string serial, CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch (OperationCanceledException) { return; }

        while (true)
        {
            if (!_rt.Enabled || !_opt.Anpr.Enabled)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
                continue;
            }
            if (!_rt.Cameras.TryGetValue(serial, out var cam))
                return; // دوربین از سرور حذف شده

            try
            {
                var cmds = await _api.CameraHandshakeAsync(serial, consume: true, ct);
                foreach (var c in cmds)
                    await ExecuteCameraCommandAsync(serial, cam, c, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "خطای حلقه دوربین {Serial}", serial);
            }

            int intervalSec = Math.Clamp(cam.HeartbeatSec > 0 ? cam.HeartbeatSec : 30, 10, 600);
            try { await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>اجرای دستور سرور روی دوربین — ping همیشه ممکن؛ راهبند/ری‌استارت فقط با قالب HTTP پیکربندی‌شده.</summary>
    private async Task ExecuteCameraCommandAsync(string serial, CameraConfig cam, CommandDto c, CancellationToken ct)
    {
        switch (c.Type)
        {
            case "ping":
                await _api.PostCameraAckAsync(serial, c.Id, true, "ضربان دوربین سالم است", ct);
                break;

            case "open_barrier":
            case "close_barrier":
            case "restart":
            {
                string? template = c.Type switch
                {
                    "open_barrier" => cam.OpenUrl,
                    "close_barrier" => cam.CloseUrl,
                    _ => cam.RestartUrl,
                };
                if (string.IsNullOrWhiteSpace(template))
                {
                    string hint = c.Type == "restart"
                        ? "قالب HTTP ری‌استارت دوربین در تنظیمات گیت‌وی (appsettings → Gateway:Anpr:Cameras) پیکربندی نشده است"
                        : "قالب HTTP راهبند دوربین در تنظیمات گیت‌وی (appsettings → Gateway:Anpr:Cameras) پیکربندی نشده است — آدرس کنترل راهبند سازندهٔ دوربین را با {ip} برای IP وارد کنید";
                    await _api.PostCameraAckAsync(serial, c.Id, false, hint, ct);
                    _log.LogWarning("دستور {Type} برای دوربین {Serial} قابل اجرا نیست — قالب HTTP تنظیم نشده", c.Type, serial);
                    break;
                }
                string url = template.Replace("{ip}", string.IsNullOrWhiteSpace(cam.Ip) ? "" : cam.Ip.Trim());
                var (ok, detail) = await RelayCameraHttpAsync(url, cam, ct);
                await _api.PostCameraAckAsync(serial, c.Id, ok,
                    ok ? "دستور با موفقیت به دوربین ارسال شد" : ("اجرای دستور روی دوربین ناموفق بود — " + detail), ct);
                if (ok) _log.LogInformation("دستور {Type} روی دوربین {Serial} اجرا شد ({Url})", c.Type, serial, url);
                else _log.LogWarning("دستور {Type} روی دوربین {Serial} ناموفق: {Detail}", c.Type, serial, detail);
                break;
            }

            default:
                await _api.PostCameraAckAsync(serial, c.Id, false,
                    $"دستور «{c.Type}» برای دوربین پلاک‌خوان قابل اجرا نیست", ct);
                break;
        }
    }

    /// <summary>فراخوانی HTTP خود دوربین (راهبند/ری‌استارت) — Basic اختیاری؛ موفق = 2xx.</summary>
    private async Task<(bool Ok, string Detail)> RelayCameraHttpAsync(string url, CameraConfig cam, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(cam.HttpUser))
            {
                var cred = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cam.HttpUser}:{cam.HttpPass}"));
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", cred);
            }
            using var res = await _relay.SendAsync(req, ct);
            return res.IsSuccessStatusCode
                ? (true, ((int)res.StatusCode).ToString())
                : (false, $"HTTP {(int)res.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    // ---------- حلقه poll هر دستگاه TCP ----------

    private async Task PollLoopAsync(string serial, CancellationToken ct)
    {
        // اجرای اولیه با کمی تأخیر تا سرور بالا بیاید
        try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch (OperationCanceledException) { return; }

        while (true)
        {
            if (!_rt.Enabled)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
                continue;
            }
            // همیشه تازه‌ترین کانفیگ دستگاه از runtime خوانده می‌شود (تغییر IP/پورت/فاصله بدون ری‌استارت)
            if (!_rt.Devices.TryGetValue(serial, out var dev))
                return; // دستگاه از سرور حذف شده
            try { await PollTcpDeviceAsync(dev, ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "خطای poller دستگاه {Serial}", serial);
                try { await _api.PostStatusAsync(serial, null, null, false, ct); }
                catch { /* گزارش وضعیت هم ناموفق — چرخه بعدی */ }
            }

            int intervalSec = Math.Max(15, dev.PollIntervalSec > 0 ? dev.PollIntervalSec : _rt.PollIntervalSec);
            try { await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>یک چرخه کامل poll: اتصال → اجرای دستورات → خواندن رکوردها/کاربران → گزارش وضعیت.</summary>
    private async Task PollTcpDeviceAsync(DeviceConfig dev, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(dev.Ip)) return;
        int port = dev.Port > 0 ? dev.Port : _rt.DefaultDevicePort;
        int commKey = dev.CommKey >= 0 ? dev.CommKey : _rt.DefaultCommKey;

        using var client = new ZkTcpClient(Math.Max(2000, _rt.TcpTimeoutMs));
        try
        {
            await client.ConnectAsync(dev.Ip, port, ct);
            await client.LoginAsync(commKey, ct);
            _log.LogInformation("✓ اتصال TCP به {Name} ({Ip}) برقرار شد", dev.Name ?? dev.Serial, dev.Ip);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning("✗ اتصال به {Serial} ناموفق: {Message}", dev.Serial, ex.Message);
            await _api.PostStatusAsync(dev.Serial, null, null, false, ct);
            return;
        }

        try
        {
            // 1) اجرای دستورات صف‌شده سرور
            var cmds = await _api.PullCommandsAsync(dev.Serial, ct);
            foreach (var c in cmds)
            {
                await ExecuteCommandAsync(dev, client, c, ct);
            }

            // 2) خواندن رکوردهای جدید تردد (backfill)
            await PullRecordsAsync(dev, client, ct);

            // 3) شمارش کاربران و گزارش وضعیت
            string? firmware = null;
            int? userCount = null;
            try
            {
                var sizes = await client.ReadSizesAsync(ct);
                userCount = sizes.Users;
            }
            catch { /* اختیاری */ }
            try { firmware = await client.GetVersionAsync(ct); } catch { /* اختیاری */ }
            await _api.PostStatusAsync(dev.Serial, firmware, userCount, true, ct);
        }
        finally
        {
            await client.DisconnectAsync(ct);
        }
    }

    /// <summary>اجرای یک دستور سرور روی دستگاه از مسیر TCP.</summary>
    private async Task ExecuteCommandAsync(DeviceConfig dev, ZkTcpClient client, CommandDto c, CancellationToken ct)
    {
        bool ok = true;
        string result = "اجرا شد (TCP)";
        try
        {
            switch (c.Type)
            {
                case "unlock":
                    int seconds = GetInt(c.Payload, "seconds", 5);
                    await client.UnlockAsync(seconds, ct);
                    result = $"درب به مدت {seconds} ثانیه باز شد";
                    break;

                case "sync_time":
                    DateTime? dt = GetDate(c.Payload, "datetime");
                    await client.SetTimeAsync(dt, ct);
                    result = "ساعت دستگاه با سرور همگام شد";
                    break;

                case "restart":
                    await client.RestartAsync(ct);
                    result = "دستگاه ری‌استارت می‌شود";
                    break;

                case "refresh_data":
                    await client.RefreshDataAsync(ct);
                    result = "داده‌های داخلی دستگاه نوسازی شد";
                    break;

                case "get_users":
                {
                    var users = await client.GetUsersAsync(ct);
                    await _api.PostUsersAsync(dev.Serial, users.Select(u => new DeviceUser
                    {
                        Pin = u.UserId,
                        Name = u.Name,
                        Uid = u.Uid,
                        Privilege = u.Privilege,
                        Card = u.Card != 0 ? u.Card.ToString(CultureInfo.InvariantCulture) : null,
                        Password = string.IsNullOrEmpty(u.Password) ? null : u.Password,
                        Group = u.GroupId,
                    }).ToList(), ct);
                    result = $"{users.Count} کاربر از دستگاه خوانده و به سرور ارسال شد";
                    break;
                }

                case "get_user":
                {
                    // دریافت یک کاربر مشخص (payload: pin) — همان مسیر get_users با فیلتر پین
                    string pinQ = GetStr(c.Payload, "pin");
                    var allUsers = await client.GetUsersAsync(ct);
                    var hits = string.IsNullOrEmpty(pinQ)
                        ? new List<ZkUser>()
                        : allUsers.Where(u => u.UserId == pinQ).ToList();
                    if (hits.Count > 0)
                    {
                        await _api.PostUsersAsync(dev.Serial, hits.Select(u => new DeviceUser
                        {
                            Pin = u.UserId,
                            Name = u.Name,
                            Uid = u.Uid,
                            Privilege = u.Privilege,
                            Card = u.Card != 0 ? u.Card.ToString(CultureInfo.InvariantCulture) : null,
                            Password = string.IsNullOrEmpty(u.Password) ? null : u.Password,
                            Group = u.GroupId,
                        }).ToList(), ct);
                        result = $"کاربر {pinQ} از دستگاه خوانده شد ({hits.Count} رکورد) و به سرور ارسال شد";
                    }
                    else
                    {
                        result = $"کاربر {pinQ} روی دستگاه یافت نشد";
                    }
                    break;
                }

                case "enable_device":
                    await client.EnableDeviceAsync(ct);
                    result = "دستگاه فعال شد — پذیرش تردد از سر گرفته شد";
                    break;

                case "disable_device":
                    await client.DisableDeviceAsync(ct);
                    result = "دستگاه غیرفعال شد (حالت تعمیرات — صفحه قفل است)";
                    break;

                case "get_user_bios":
                {
                    // دریافت اطلاعات هویتی (اثر انگشت/چهره/کارت/رمز) از حافظه دستگاه
                    var users = await client.GetUsersAsync(ct);
                    var pins = GetPinFilter(c.Payload);
                    if (pins.Count > 0) users = users.Where(u => pins.Contains(u.UserId)).ToList();

                    var bios = new List<RawBio>();
                    foreach (var u in users)
                    {
                        // اثر انگشت — اسلات ۰ و ۱
                        for (int idx = 0; idx < 2; idx++)
                        {
                            try
                            {
                                var t = await client.GetUserTemplateAsync(u.Uid, u.UserId, idx, 0, ct);
                                if (t is not null && t.Template.Length > 0)
                                    bios.Add(new RawBio { Pin = u.UserId, Type = "fp", Index = idx, Valid = t.Valid, Data = Convert.ToBase64String(t.Template) });
                            }
                            catch { /* قالبی در این اسلات ثبت نشده */ }
                        }
                        // چهره — اسلات ۰ (دستگاه‌های بدون چهره خطا می‌دهند و رد می‌شود)
                        try
                        {
                            var f = await client.GetUserTemplateAsync(u.Uid, u.UserId, 0, 1, ct);
                            if (f is not null && f.Template.Length > 0)
                                bios.Add(new RawBio { Pin = u.UserId, Type = "face", Index = 0, Valid = f.Valid, Data = Convert.ToBase64String(f.Template) });
                        }
                        catch { /* پشتیبانی چهره ندارد */ }
                        // کارت و رمز — از رکورد کاربر
                        if (u.Card != 0)
                            bios.Add(new RawBio { Pin = u.UserId, Type = "card", Index = 0, Valid = 1, Data = u.Card.ToString(CultureInfo.InvariantCulture) });
                        if (!string.IsNullOrEmpty(u.Password))
                            bios.Add(new RawBio { Pin = u.UserId, Type = "pin", Index = 0, Valid = 1, Data = u.Password });
                    }
                    await _api.PostBiosAsync(dev.Serial, bios, ct);
                    result = $"{bios.Count} قالب هویتی برای {users.Count} کاربر خوانده و به سرور ارسال شد";
                    break;
                }

                case "push_user_bios":
                {
                    // انتقال پرسنل + قالب‌های هویتی به دستگاه (کارت/رمز روی رکورد کاربر می‌نشیند)
                    var users = JsonList(c.Payload, "users");
                    var bios = JsonList(c.Payload, "bios");
                    if (users.Count == 0) { result = "هیچ کاربری برای ارسال تعیین نشده بود"; break; }

                    var existing = await client.GetUsersAsync(ct);
                    int nextUid = existing.Count > 0 ? existing.Max(u => u.Uid) + 1 : 1;
                    int pushed = 0, tplCount = 0;
                    foreach (var ju in users)
                    {
                        string pin = JStr(ju, "pin");
                        if (pin.Length == 0) continue;
                        string name = JStr(ju, "name");
                        uint card = uint.TryParse(JStr(ju, "card"), NumberStyles.Any, CultureInfo.InvariantCulture, out var cc) ? cc : 0;
                        string password = JStr(ju, "password");
                        string group = JStr(ju, "group");
                        if (group.Length == 0) group = "1";

                        var ex = existing.FirstOrDefault(x => x.UserId == pin);
                        int uid = ex?.Uid ?? nextUid++;
                        await client.SetUserAsync(uid, name.Length > 0 ? name : $"NN-{pin}", pin,
                            ZkConsts.UserDefault, password, group, card, ct);
                        pushed++;

                        foreach (var jb in bios.Where(b => JStr(b, "pin") == pin))
                        {
                            string type = JStr(jb, "type");
                            if (type is not ("fp" or "face")) continue;
                            string data = JStr(jb, "data");
                            if (data.Length == 0) continue;
                            byte[] tpl;
                            try { tpl = Convert.FromBase64String(data); }
                            catch { continue; }
                            try
                            {
                                await client.SetUserTemplateAsync(uid, pin, JInt(jb, "index"),
                                    JInt(jb, "valid") == 0 ? 0 : 1, type == "face" ? 1 : 0, tpl, ct);
                                tplCount++;
                            }
                            catch (Exception ex2)
                            {
                                _log.LogWarning("نوشتن قالب {Type}[{Idx}] کاربر {Pin} روی {Serial} ناموفق: {Msg}",
                                    type, JInt(jb, "index"), pin, dev.Serial, ex2.Message);
                            }
                        }
                    }
                    result = $"{pushed} کاربر + {tplCount} قالب هویتی روی دستگاه ثبت شد";
                    break;
                }

                case "push_user":
                {
                    string pin = GetStr(c.Payload, "pin");
                    string name = GetStr(c.Payload, "name");
                    uint card = (uint)GetInt(c.Payload, "card", 0);
                    // uid: ادامه از بیشینه uid موجود (یا تطابق user_id موجود برای به‌روزرسانی)
                    var users = await client.GetUsersAsync(ct);
                    var existing = users.FirstOrDefault(u => u.UserId == pin);
                    int uid = existing?.Uid ?? (users.Count > 0 ? users.Max(u => u.Uid) + 1 : 1);
                    await client.SetUserAsync(uid, string.IsNullOrEmpty(name) ? $"NN-{pin}" : name, pin,
                        ZkConsts.UserDefault, card: card, ct: ct);
                    result = $"کاربر {pin} روی دستگاه ثبت شد (uid={uid})";
                    break;
                }

                case "delete_user":
                {
                    string pin = GetStr(c.Payload, "pin");
                    bool removed = await client.DeleteUserByPinAsync(pin, ct);
                    ok = removed;
                    result = removed ? $"کاربر {pin} از دستگاه حذف شد" : $"کاربر {pin} روی دستگاه یافت نشد";
                    break;
                }

                case "clear_attendance":
                    await client.ClearAttendanceAsync(ct);
                    result = "رکوردهای تردد دستگاه پاک شد";
                    break;

                case "clear_data":
                    await client.ClearDataAsync(ct);
                    result = "کل داده‌های دستگاه پاک شد";
                    break;

                default:
                    // دستورهای داده‌محور (مثل حالت ADMS) — اینجا پشتیبانی نمی‌شود
                    ok = false;
                    result = $"نوع دستور {c.Type} در حالت TCP پشتیبانی نمی‌شود";
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ok = false;
            result = ex.Message;
            _log.LogError(ex, "خطای اجرای دستور {Type} برای {Serial}", c.Type, dev.Serial);
        }

        await _api.PostAckAsync(dev.Serial, c.Id, ok, result, ct);
        _log.LogInformation("دستور {Type} برای {Serial}: {Result}", c.Type, dev.Serial, result);
    }

    /// <summary>خواندن رکوردهای تردد از حافظه دستگاه و ارسال به سرور.</summary>
    private async Task PullRecordsAsync(DeviceConfig dev, ZkTcpClient client, CancellationToken ct)
    {
        List<ZkAttendance> records;
        try
        {
            records = await client.GetAttendanceAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning("خواندن رکوردهای {Serial} ناموفق: {Message}", dev.Serial, ex.Message);
            return;
        }
        if (records.Count == 0) return;

        var punches = records.Select(r => new RawPunch
        {
            Pin = r.UserId,
            Timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Status = r.Punch > 0 ? "out" : "in",
        }).ToList();

        bool sent = await _api.PostPunchesAsync(dev.Serial, punches, ct);
        if (sent)
            _log.LogInformation("دستگاه {Serial}: {Count} رکورد تردد به سرور ارسال شد", dev.Serial, punches.Count);
    }

    // ---------- حلقه بازبینی (sweeper) ----------

    private async Task SweeperLoopAsync(CancellationToken ct)
    {
        while (true)
        {
            int sec = Math.Max(30, _rt.SweeperIntervalSec);
            try { await Task.Delay(TimeSpan.FromSeconds(sec), ct); }
            catch (OperationCanceledException) { return; }

            if (!_rt.Enabled) continue;
            try
            {
                var devices = await _api.GetDevicesAsync(ct);
                foreach (var d in devices.Where(d => d.Online == false))
                    _log.LogWarning("⚠ دستگاه {Name} ({Serial}) آفلاین است", d.Name ?? d.Serial, d.Serial);

                foreach (var d in devices)
                {
                    // فقط نمایش (peek) — مصرف واقعی با poller/پل ADMS انجام می‌شود؛
                    // وگرنه دستورِ دستگاه آفلاین «تحویل‌شده» علامت می‌خورد و گم می‌شد
                    var cmds = await _api.PeekCommandsAsync(d.Serial, ct);
                    if (cmds.Count > 0)
                        _log.LogInformation("⏳ {Count} دستور در صف برای {Serial} (تحویل با handshake ADMS یا poll بعدی)",
                            cmds.Count, d.Serial);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning("خطای sweeper: {Message}", ex.Message);
            }
        }
    }

    // ---------- استخراج payload ----------

    private static string GetStr(Dictionary<string, object?>? p, string key) =>
        p is not null && p.TryGetValue(key, out var v) && v is not null ? v.ToString() ?? "" : "";

    private static int GetInt(Dictionary<string, object?>? p, string key, int def) =>
        int.TryParse(GetStr(p, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;

    private static DateTime? GetDate(Dictionary<string, object?>? p, string key)
    {
        string s = GetStr(p, key);
        if (s.Length == 0) return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>آرایه JSON از payload (مثلا pins یا users یا bios).</summary>
    private static List<JsonElement> JsonList(Dictionary<string, object?>? p, string key)
    {
        if (p is null || !p.TryGetValue(key, out var v) || v is not JsonElement je || je.ValueKind != JsonValueKind.Array)
            return new List<JsonElement>();
        return je.EnumerateArray().ToList();
    }

    private static string JStr(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString() ?? "" : "";

    private static int JInt(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.Number
            ? e.GetInt32() : 0;

    /// <summary>فیلتر پین‌ها از payload دستور get_user_bios (pins:[...]).</summary>
    private static List<string> GetPinFilter(Dictionary<string, object?>? p)
    {
        if (p is null || !p.TryGetValue("pins", out var v) || v is not JsonElement je || je.ValueKind != JsonValueKind.Array)
            return new List<string>();
        return je.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();
    }
}
