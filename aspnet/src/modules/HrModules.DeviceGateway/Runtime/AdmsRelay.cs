using System.Net;
using System.Net.Sockets;

// ======================================================================
// فاز ۶۴ — پورت اختصاصی ADMS (relay شفاف TCP) + گزینه‌های سرویس خارجی
// ----------------------------------------------------------------------
// ۱) AdmsRelay: در فاز ۶۳ پل ADMS روی «پورت خود اپ» سرویس می‌داد؛ استقرارهای
//    موجودی که دستگاه‌هایشان به سرویس ویندوزی روی پورت اختصاصی (مثلاً 8081)
//    وصل بودند باید کانفیگ دستگاه‌ها را عوض می‌کردند. این کلاس یک شنوندهٔ TCP
//    روی GatewaySetting.ListenPort می‌نشیند و بایت‌ها را شفاف به حلقه‌باز
//    خود اپ (همان /iclock/cdata و بقیهٔ مسیرها) حواله می‌دهد — دستگاه و
//    پروتکل هیچ فرقی نمی‌کنند؛ صرفاً پورت ورودی جابه‌جا می‌شود.
//    مدیریت چرخهٔ آن با supervisor داخل GatewayWorker است (تغییر پورت/خاموشی
//    تنظیمات → توقف/جابه‌جایی بدون ری‌استارت اپ).
// ۲) ExternalServiceOptions: برای استقرارهای ترکیبی (ماژول درون‌اپی نصب است
//    ولی سرویس ویندوزی جداگانه هم روی همین بانک کار می‌کند) آدرس سرویس خارجی
//    از appsettings خوانده می‌شود (Gateway:ExternalService:Url) و پنل
//    /DeviceGateway با Endpoint /DeviceGateway/ExternalStatus سلامت آن را
//    نشان می‌دهد — همان endpoint /health سرویس (وضعیت runtime).
// ======================================================================
namespace HrModules.DeviceGateway.Runtime;

public sealed class AdmsRelay
{
    private const int MaxConnections = 64;

    private TcpListener? _listener;
    private Task? _acceptLoop;
    private CancellationTokenSource? _cts;
    private long _active, _total;
    private DateTime? _startedAt;
    private DateTime _lastErrLogUtc = DateTime.MinValue;
    private Action<string>? _log;

    public volatile string? LastError;

    /// <summary>پورتی که relay در حال حاضر روی آن شنونده است (۰ = خاموش).</summary>
    public int Port { get; private set; }

    /// <summary>پورت مقصد (پورت خود اپ) که اتصال‌ها به آن حواله می‌شوند.</summary>
    public int UpstreamPort { get; private set; }

    public bool Running => _listener is not null;
    public int ActiveConnections => (int)Interlocked.Read(ref _active);
    public long TotalAccepted => Interlocked.Read(ref _total);
    public DateTime? StartedAt => _startedAt;

    /// <summary>شروع شنونده روی پورت داده‌شده — اگر پورت اشغال باشد false با LastError.</summary>
    public bool Start(int port, int upstreamPort, Action<string> log)
    {
        if (port <= 0 || upstreamPort <= 0 || port == upstreamPort) return false;
        if (Running && Port == port && UpstreamPort == upstreamPort) return true; // هم‌وضعیت
        if (Running) Stop("جابه‌جایی پورت relay", log);
        _log = log;

        try
        {
            var l = new TcpListener(IPAddress.Any, port);
            l.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            l.Start();
            _listener = l;
            _cts = new CancellationTokenSource();
            Port = port;
            UpstreamPort = upstreamPort;
            _startedAt = DateTime.Now;
            LastError = null;
            _acceptLoop = Task.Run(() => AcceptLoopAsync(l, _cts.Token));
            log($"پورت اختصاصی ADMS روی {port} فعال شد — حواله به پورت اپ ({upstreamPort})");
            return true;
        }
        catch (Exception ex)
        {
            // خطا فقط در LastError — سطح WARN را فراخواننده (worker) ثبت می‌کند
            LastError = $"اتصال به پورت {port} ممکن نشد — {ex.Message}";
            _listener = null; Port = 0;
            return false;
        }
    }

    /// <summary>توقف کامل شنونده و اتصال‌های جاری (اتصال‌های باز به‌آرامی بسته می‌شوند).</summary>
    public void Stop(string reason, Action<string>? log = null)
    {
        var old = Port;
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch { /* خاموشی دوباره نادیده */ }
        _listener = null; _cts = null;
        Port = 0; UpstreamPort = 0; _startedAt = null;
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(3)); } catch { /* نادیده */ }
        _acceptLoop = null;
        if (old > 0) log?.Invoke($"پورت اختصاصی ADMS ({old}) بسته شد — {reason}");
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (SocketException) when (ct.IsCancellationRequested) { return; }
            catch (ObjectDisposedException) { return; }
            catch (Exception) { continue; }

            if (Interlocked.Read(ref _active) >= MaxConnections)
            {
                try { client.Close(); } catch { /* نادیده */ }
                continue;
            }
            Interlocked.Increment(ref _total);
            _ = Task.Run(() => PipeAsync(client, ct), ct);
        }
    }

    private async Task PipeAsync(TcpClient client, CancellationToken ct)
    {
        Interlocked.Increment(ref _active);
        try
        {
            using var c = client;
            using var up = new TcpClient();
            Task connect = up.ConnectAsync(IPAddress.Loopback, UpstreamPort, ct).AsTask();
            var done = await Task.WhenAny(connect, Task.Delay(5000, ct));
            if (done != connect || !up.Connected)
            {
                NoteError("پورت اپ (حلقه‌باز) در دسترس نیست");
                return;
            }

            using var link = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var cs = c.GetStream();
            var us = up.GetStream();
            var t1 = cs.CopyToAsync(us, 65536, link.Token);
            var t2 = us.CopyToAsync(cs, 65536, link.Token);
            await Task.WhenAny(t1, t2);
            link.Cancel();
            try { await Task.WhenAll(t1, t2); } catch { /* بستن سمت دیگر عادی است */ }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            NoteError(ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    /// <summary>ثبت خطا با بنداشت (حداکثر هر ۳۰ ثانیه) — اتصال‌های مکرر دستگاه لاگ را پر نکند.</summary>
    private void NoteError(string msg)
    {
        LastError = msg;
        var now = DateTime.UtcNow;
        if ((now - _lastErrLogUtc).TotalSeconds < 30) return;
        _lastErrLogUtc = now;
        _log?.Invoke("خطای relay پورت ADMS: " + msg);
    }
}

/// <summary>گزینه‌های سرویس گیت‌وی خارجی (استقرار ترکیبی) — از appsettings: Gateway:ExternalService.</summary>
public sealed class ExternalServiceOptions
{
    /// <summary>آدرس پایهٔ سرویس خارجی (مثلاً http://127.0.0.1:8081) — خالی = پیکربندی نشده.</summary>
    public string Url { get; set; } = "";

    /// <summary>سقف زمان انتظار پاسخ /health سرویس خارجی (ثانیه).</summary>
    public int TimeoutSec { get; set; } = 3;

    public bool Configured =>
        !string.IsNullOrWhiteSpace(Url)
        && Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var u)
        && (u.Scheme == "http" || u.Scheme == "https");
}
