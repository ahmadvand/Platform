using System.Text;
using Platform.Contracts;

// ======================================================================
// فاز ۶۳ — حلقهٔ اصلی دروازهٔ درون‌اپی (پورت DeviceWorker سرویس ویندوزی)
// ----------------------------------------------------------------------
// 1) supervisor: هر ConfigRefreshSec کانفیگ را از کانال قراردادی می‌خواند
//    (همان جدول GatewaySetting + Devices که سرویس خارجی از HTTP می‌گرفت) →
//    شروع/توقف پولرها و حلقه‌های دوربین
// 2) یک poller برای هر دستگاه TCP فعال — فاصله لحظه‌ای از کانفیگ
// 3) sweeper: گزارش دستگاه‌های آفلاین + شمار دستورهای در انتظار
// 4) فاز ۶۴ — پورت اختصاصی ADMS: اگر GatewaySetting.ListenPort با پورت خود
//    اپ فرق کند، relay شفاف TCP روی آن پورت می‌نشیند (بدون ری‌استارت، با
//    تغییر پورت جابه‌جا می‌شود) — دستگاه‌های قدیمی سرویس خارجی بی‌تغییر می‌مانند.
// دستگاه‌های ADMS نیازی به poller ندارند — endpoint درون‌پردازشی سرویس‌شان می‌دهد.
// تفاوت کلیدی با سرویس خارجی: بدون لاگین HTTP و بدون شبکه میانی — کانال مستقیم.
// ======================================================================
namespace HrModules.DeviceGateway.Runtime;

public sealed class GatewayWorker : BackgroundService
{
    public const string ModuleCode = "gateway";

    private readonly GatewayRuntime _rt;
    private readonly AnprTemplateOptions _anpr;
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _cfg;
    private readonly ILogger<GatewayWorker> _log;

    public GatewayWorker(GatewayRuntime rt, AnprTemplateOptions anpr, IServiceScopeFactory scopes,
        IConfiguration cfg, ILogger<GatewayWorker> log)
    { _rt = rt; _anpr = anpr; _scopes = scopes; _cfg = cfg; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // راه‌اندازی ماژول تا آماده‌شدن بانک (مثلاً اولین بوت پس از ویزارد) سقوط نمی‌کند —
        // هر چرخهٔ supervisor دوباره تلاش می‌کند (خودترمیم، الگوی DbInit فاز ۶۲).
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (await ApplyConfigAsync(ct)) break;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning("دروازه: بانک هنوز آماده نیست ({Message}) — ۵ ثانیه دیگر", ex.Message);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { return; }
        }

        _rt.Log("INFO", "gateway", "دروازهٔ دستگاه‌ها (ماژول درون‌اپی) فعال شد");
        _log.LogInformation("=== دروازهٔ دستگاه‌ها (فاز ۶۳) شروع شد — حالت ماژول درون‌اپی ===");

        StartMissingPollers(ct);
        StartMissingCameraLoops(ct);

        try { await Task.WhenAll(SupervisorLoopAsync(ct), SweeperLoopAsync(ct)); }
        catch (OperationCanceledException) { /* خاموشی تمیز */ }

        StopAllPollers();
        StopAllCameraLoops();
        _rt.Relay.Stop("خاموشی دروازه");
        _log.LogInformation("=== دروازهٔ دستگاه‌ها متوقف شد ===");
    }

    // ---------- supervisor: کانفیگ پویا از کانال پلتفرم ----------

    private async Task<bool> ApplyConfigAsync(CancellationToken ct)
    {
        bool parkingSink;
        GatewaySettingsDto s;
        List<GatewayDeviceCfg> devices;
        List<GatewayCameraCfg> cams;

        using (var scope = _scopes.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var channel = sp.GetRequiredService<IDeviceGatewayChannel>();
            var avail = sp.GetRequiredService<IModuleAvailability>();
            var parking = sp.GetService<IParkingGatewaySink>(); // اختیاری — ماژول پارکینگ

            _rt.Config.ModuleOn = avail.Enabled(ModuleCode);
            s = await channel.GetSettingsAsync();
            devices = await channel.GetDevicesAsync();
            cams = parking is null ? new List<GatewayCameraCfg>() : await parking.GetCamerasAsync();
            parkingSink = parking is not null;
        }

        var cfg = _rt.Config;
        bool wasEnabled = cfg.FullyEnabled;
        cfg.Enabled = s.Enabled;
        cfg.TcpTimeoutMs = Math.Clamp(s.TcpTimeoutMs, 1000, 60000);
        cfg.PollIntervalSec = Math.Clamp(s.PollIntervalSec, 15, 3600);
        cfg.SweeperIntervalSec = Math.Clamp(s.SweeperIntervalSec, 30, 3600);
        cfg.ConfigRefreshSec = Math.Clamp(s.ConfigRefreshSec, 10, 3600);
        cfg.DefaultDevicePort = s.DefaultDevicePort > 0 ? s.DefaultDevicePort : 4370;
        cfg.DefaultCommKey = Math.Max(0, s.DefaultCommKey);
        cfg.ConfigVersion = s.ConfigVersion;
        cfg.AnprEnabled = _anpr.Enabled;

        // دستگاه‌ها — جایگزین کامل فهرست قبلی
        var remoteTcp = new HashSet<string>();
        foreach (var d in devices)
        {
            cfg.Devices[d.Serial] = new GatewayDeviceCfg(
                d.Serial, d.Name,
                string.IsNullOrWhiteSpace(d.Transport) ? "adms" : d.Transport.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(d.Ip) ? null : d.Ip.Trim(),
                d.Port is > 0 ? d.Port : null,
                d.CommKey is >= 0 ? d.CommKey : null,
                d.PollIntervalSec > 0 ? d.PollIntervalSec : 60);
            if (cfg.Devices[d.Serial].Transport == "tcp") remoteTcp.Add(d.Serial);
            var live = _rt.LiveOf(d.Serial, d.Name, cfg.Devices[d.Serial].Transport);
            live.Name = d.Name;
            live.PendingCommands = await CountPendingAsync(d.Serial, ct);
        }
        foreach (var stale in cfg.Devices.Keys.Where(k => !devices.Any(d => d.Serial == k)).ToList())
            cfg.Devices.TryRemove(stale, out _);

        // دوربین‌ها — فهرست از ماژول پارکینگ؛ قالب‌های HTTP راهبند از appsettings (فاز ۴۴)
        if (parkingSink && _anpr.Enabled)
        {
            foreach (var c in cams)
            {
                var tpl = _anpr.Find(c.Serial);
                cfg.Cameras[c.Serial] = (c, tpl, c.HeartbeatSec > 0 ? c.HeartbeatSec : _anpr.HeartbeatSec);
            }
            var remoteCams = cams.Select(c => c.Serial).ToHashSet();
            foreach (var serial in cfg.CameraLoops.Keys.Where(k => !remoteCams.Contains(k)).ToList())
                StopCameraLoop(serial, "از فهرست دوربین‌ها خارج شد");
            foreach (var stale in cfg.Cameras.Keys.Where(k => !remoteCams.Contains(k)).ToList())
                cfg.Cameras.TryRemove(stale, out _);
        }
        else
        {
            foreach (var serial in cfg.CameraLoops.Keys.ToList())
                StopCameraLoop(serial, parkingSink ? "پایش دوربین‌ها از تنظیمات خاموش است" : "ماژول پارکینگ نصب نیست");
            cfg.Cameras.Clear();
        }

        // توقف پولر دستگاه‌هایی که دیگر TCP فعال نیستند
        foreach (var serial in cfg.Pollers.Keys.Where(k => !remoteTcp.Contains(k)).ToList())
            StopPoller(serial, "از فهرست دستگاه‌های TCP خارج شد");

        // فاز ۶۴ — پورت اختصاصی ADMS (relay شفاف به پورت خود اپ)
        ApplyRelayConfig(s.ListenPort);

        if (cfg.FullyEnabled != wasEnabled)
        {
            string why = !cfg.ModuleOn ? "ماژول خاموش است (کلید مدیر/مجوز/نصب)" : (cfg.Enabled ? "" : "پایش از تنظیمات گیت‌وی متوقف شده");
            _rt.Log("INFO", "gateway", cfg.FullyEnabled
                ? "پایش دستگاه‌ها فعال شد"
                : $"پایش دستگاه‌ها متوقف شد — {why}");
        }
        return true;
    }

    // ---------- فاز ۶۴: پورت اختصاصی ADMS (relay شفاف) ----------

    /// <summary>پورت HTTP خود اپ — از ASPNETCORE_URLS/urls (اولین آدرس http)؛ پیدا نشد → ۰.</summary>
    internal int ResolveUpstreamPort()
    {
        var urls = _cfg["ASPNETCORE_URLS"] ?? _cfg["urls"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        if (string.IsNullOrWhiteSpace(urls)) urls = Environment.GetEnvironmentVariable("ASPNETCORE_PORT");
        foreach (var raw in (urls ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.All(char.IsDigit) && int.TryParse(raw, out var p)) return p;
            if (Uri.TryCreate(raw, UriKind.Absolute, out var u) && u.Port > 0) return u.Port;
        }
        return 0;
    }

    /// <summary>هم‌ترازی وضعیت relay با تنظیمات: فعال فقط وقتی ماژول روشن + ListenPort معتبر ≠ پورت اپ.</summary>
    private void ApplyRelayConfig(int listenPort)
    {
        var upstream = ResolveUpstreamPort();
        var relay = _rt.Relay;

        // شرط خاموشی: ماژول/پایش خاموش، پورت نامعتبر، یا همان پورت خود اپ (نیازی به relay نیست)
        if (!_rt.Config.FullyEnabled || listenPort <= 0 || upstream <= 0 || listenPort == upstream)
        {
            if (relay.Running)
            {
                string why = !_rt.Config.FullyEnabled ? "پایش متوقف است"
                    : listenPort <= 0 ? "پورت اختصاصی در تنظیمات خالی است"
                    : listenPort == upstream ? "پورت تنظیم‌شده همان پورت خود اپ است — endpoint مستقیم سرویس می‌دهد"
                    : "پورت اپ شناسایی نشد";
                relay.Stop(why, m => _rt.Log("INFO", "gateway", m));
            }
            return;
        }

        if (relay.Running && relay.Port == listenPort && relay.UpstreamPort == upstream) return; // هم‌وضعیت
        if (relay.Start(listenPort, upstream, m => _rt.Log("INFO", "gateway", m)))
        {
            // پیام شروع داخل خود Start لاگ شده است
        }
        else if (relay.LastError is not null)
        {
            _rt.Log("WARN", "gateway", relay.LastError);
        }
    }

    private async Task<int> CountPendingAsync(string serial, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var channel = scope.ServiceProvider.GetRequiredService<IDeviceGatewayChannel>();
            return (await channel.PullCommandsAsync(serial, consume: false)).Count;
        }
        catch { return 0; }
    }

    private async Task<bool> WaitAsync(CancellationToken ct)
    {
        int sec = Math.Clamp(_rt.Config.ConfigRefreshSec, 10, 3600);
        try { await Task.Delay(TimeSpan.FromSeconds(sec), ct); }
        catch (OperationCanceledException) { return false; }
        return true;
    }

    private async Task SupervisorLoopAsync(CancellationToken ct)
    {
        while (await WaitAsync(ct))
        {
            try
            {
                await ApplyConfigAsync(ct);
                StartMissingPollers(ct);
                StartMissingCameraLoops(ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning("خطای supervisor دروازه: {Message}", ex.Message);
                _rt.Log("WARN", "gateway", "خطای همگام‌سازی کانفیگ: " + ex.Message);
            }
        }
    }

    // ---------- pollerها ----------

    private void StartMissingPollers(CancellationToken ct)
    {
        if (!_rt.Config.FullyEnabled) return;
        foreach (var dev in _rt.Config.Devices.Values.Where(d => d.Transport == "tcp" && !string.IsNullOrEmpty(d.Ip)))
        {
            if (_rt.Config.Pollers.ContainsKey(dev.Serial)) continue;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loop = Task.Run(() => PollLoopAsync(dev.Serial, cts.Token), cts.Token);
            _rt.Config.Pollers[dev.Serial] = (cts, loop);
            _rt.Log("INFO", dev.Serial, $"Poller راه‌اندازی شد — {dev.Ip}:{(dev.Port is > 0 ? dev.Port : _rt.Config.DefaultDevicePort)}");
        }
    }

    private void StopPoller(string serial, string reason)
    {
        if (_rt.Config.Pollers.TryRemove(serial, out var p))
        {
            p.Cts.Cancel();
            _rt.Log("INFO", serial, "Poller متوقف شد — " + reason);
        }
    }

    private void StopAllPollers()
    {
        foreach (var serial in _rt.Config.Pollers.Keys.ToList()) StopPoller(serial, "خاموشی دروازه");
    }

    private async Task PollLoopAsync(string serial, CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch (OperationCanceledException) { return; }

        while (true)
        {
            if (!_rt.Config.FullyEnabled)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
                continue;
            }
            // همیشه تازه‌ترین کانفیگ دستگاه (تغییر IP/پورت/فاصله بدون ری‌استارت)
            if (!_rt.Config.Devices.TryGetValue(serial, out var dev))
                return; // دستگاه حذف شده

            try
            {
                using var scope = _scopes.CreateScope();
                var channel = scope.ServiceProvider.GetRequiredService<IDeviceGatewayChannel>();
                await ZkPoller.PollAsync(dev, _rt, channel, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "خطای poller دستگاه {Serial}", serial);
                _rt.Log("WARN", serial, "خطای چرخهٔ poll: " + ex.Message);
            }

            int intervalSec = Math.Max(15, dev.PollIntervalSec > 0 ? dev.PollIntervalSec : _rt.Config.PollIntervalSec);
            try { await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    // ---------- دوربین‌های پلاک‌خوان (فاز ۴۴ — پورت کامل) ----------

    private void StartMissingCameraLoops(CancellationToken ct)
    {
        if (!_rt.Config.FullyEnabled || !_rt.Config.AnprEnabled) return;
        foreach (var (serial, (_, _, _)) in _rt.Config.Cameras)
        {
            if (_rt.Config.CameraLoops.ContainsKey(serial)) continue;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loop = Task.Run(() => CameraLoopAsync(serial, cts.Token), cts.Token);
            _rt.Config.CameraLoops[serial] = (cts, loop);
            _rt.Log("INFO", serial, "حلقهٔ دوربین پلاک‌خوان راه‌اندازی شد");
        }
    }

    private void StopCameraLoop(string serial, string reason)
    {
        if (_rt.Config.CameraLoops.TryRemove(serial, out var p))
        {
            p.Cts.Cancel();
            _rt.Log("INFO", serial, "حلقهٔ دوربین متوقف شد — " + reason);
        }
    }

    private void StopAllCameraLoops()
    {
        foreach (var serial in _rt.Config.CameraLoops.Keys.ToList()) StopCameraLoop(serial, "خاموشی دروازه");
    }

    /// <summary>
    /// حلقهٔ هر دوربین: ضربان + تحویل/اجرا/تایید دستورات راهبند. «آنلاین» بودن در پنل از
    /// ضربان همین حلقه می‌آید؛ خوانش پلاک هم از مسیر /anpr/plate به پارکینگ می‌رسد.
    /// </summary>
    private async Task CameraLoopAsync(string serial, CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch (OperationCanceledException) { return; }

        while (true)
        {
            if (!_rt.Config.FullyEnabled || !_rt.Config.AnprEnabled)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
                continue;
            }
            if (!_rt.Config.Cameras.TryGetValue(serial, out var entry))
                return; // دوربین از فهرست حذف شده
            var (cam, tpl, heartbeatSec) = entry;

            try
            {
                using var scope = _scopes.CreateScope();
                var parking = scope.ServiceProvider.GetService<IParkingGatewaySink>();
                if (parking is null) { await Task.Delay(TimeSpan.FromSeconds(30), ct); continue; }

                var cmds = await parking.PullCameraCommandsAsync(serial, consume: true);
                foreach (var c in cmds)
                    await ExecuteCameraCommandAsync(serial, cam, tpl, c, parking, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "خطای حلقهٔ دوربین {Serial}", serial);
                _rt.Log("WARN", serial, "خطای حلقهٔ دوربین: " + ex.Message);
            }

            int intervalSec = Math.Clamp(heartbeatSec > 0 ? heartbeatSec : 30, 10, 600);
            try { await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>اجرای دستور سرور روی دوربین — ping همیشه ممکن؛ راهبند/ری‌استارت فقط با قالب HTTP پیکربندی‌شده.</summary>
    private async Task ExecuteCameraCommandAsync(string serial, GatewayCameraCfg cam, CameraTemplate? tpl,
        GatewayCommandDto c, IParkingGatewaySink parking, CancellationToken ct)
    {
        switch (c.Type)
        {
            case "ping":
                await parking.AckCameraCommandAsync(serial, c.Id, true, "ضربان دوربین سالم است");
                _rt.Log("INFO", serial, "ضربان دوربین سالم");
                break;

            case "open_barrier":
            case "close_barrier":
            case "restart":
            {
                string? template = c.Type switch
                {
                    "open_barrier" => tpl?.OpenUrl,
                    "close_barrier" => tpl?.CloseUrl,
                    _ => tpl?.RestartUrl,
                };
                if (string.IsNullOrWhiteSpace(template))
                {
                    string hint = c.Type == "restart"
                        ? "قالب HTTP ری‌استارت دوربین در appsettings (Gateway:Anpr:Cameras) پیکربندی نشده است"
                        : "قالب HTTP راهبند در appsettings (Gateway:Anpr:Cameras) پیکربندی نشده — آدرس کنترل راهبند سازنده را با {ip} وارد کنید";
                    await parking.AckCameraCommandAsync(serial, c.Id, false, hint);
                    _rt.Log("WARN", serial, $"دستور {c.Type} قابل اجرا نیست — قالب HTTP تنظیم نشده");
                    break;
                }
                string url = template.Replace("{ip}", string.IsNullOrWhiteSpace(cam.Ip) ? "" : cam.Ip.Trim());
                var (ok, detail) = await RelayCameraHttpAsync(url, tpl, ct);
                await parking.AckCameraCommandAsync(serial, c.Id, ok,
                    ok ? "دستور با موفقیت به دوربین ارسال شد" : ("اجرای دستور روی دوربین ناموفق بود — " + detail));
                _rt.Log(ok ? "INFO" : "WARN", serial, $"دستور {c.Type}: {(ok ? "اجرا شد" : detail)}");
                break;
            }

            default:
                await parking.AckCameraCommandAsync(serial, c.Id, false,
                    $"دستور «{c.Type}» برای دوربین پلاک‌خوان قابل اجرا نیست");
                break;
        }
    }

    /// <summary>فراخوانی HTTP خود دوربین (راهبند/ری‌استارت) — Basic اختیاری؛ موفق = 2xx.</summary>
    private async Task<(bool Ok, string Detail)> RelayCameraHttpAsync(string url, CameraTemplate? tpl, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(tpl?.HttpUser))
            {
                var cred = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{tpl.HttpUser}:{tpl.HttpPass}"));
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", cred);
            }
            using var relay = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            using var res = await relay.SendAsync(req, ct);
            return res.IsSuccessStatusCode
                ? (true, ((int)res.StatusCode).ToString())
                : (false, $"HTTP {(int)res.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    // ---------- بازبین (sweeper) ----------

    private async Task SweeperLoopAsync(CancellationToken ct)
    {
        while (true)
        {
            int sec = Math.Max(30, _rt.Config.SweeperIntervalSec);
            try { await Task.Delay(TimeSpan.FromSeconds(sec), ct); }
            catch (OperationCanceledException) { return; }

            if (!_rt.Config.FullyEnabled) continue;
            try
            {
                // آفلاین‌ها از دید pollerها — گزارش در لاگ فعالیت پنل
                foreach (var live in _rt.Live.Values.Where(l => l.Transport == "tcp" && !l.Online && l.LastPoll is not null))
                    _rt.Log("WARN", live.Serial, $"دستگاه آفلاین است (آخرین poll: {live.LastPoll:HH:mm:ss})");
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning("خطای sweeper: {Message}", ex.Message);
            }
        }
    }
}
