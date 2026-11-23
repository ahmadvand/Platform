using System.Collections.Concurrent;
using Platform.Contracts;

// ======================================================================
// فاز ۶۳ — وضعیت زندهٔ دروازه (singleton درون ماژول)
// ----------------------------------------------------------------------
// معادل GatewayRuntimeConfig سرویس ویندوزی + لایهٔ «حالت زندهٔ هر دستگاه»
// و «لاگ فعالیت» برای پنل مدیریت. همه‌چیز در حافظه است — صفر جدول جدید،
// صفر تغییر DDL (بانک عیناً مثل قبل می‌ماند).
// ======================================================================
namespace HrModules.DeviceGateway.Runtime;

/// <summary>قالب HTTP راهبند یک دوربین (vendor-specific) — از IConfiguration: Gateway:Anpr.</summary>
public sealed class CameraTemplate
{
    public string Serial { get; set; } = "";
    public string? OpenUrl { get; set; }
    public string? CloseUrl { get; set; }
    public string? RestartUrl { get; set; }
    public string? HttpUser { get; set; }
    public string? HttpPass { get; set; }
}

/// <summary>تنظیمات bootstrap دوربین‌ها — مقادیر اولیه از appsettings هاست (همان شکل سرویس ویندوزی).</summary>
public sealed class AnprTemplateOptions
{
    public bool Enabled { get; set; } = true;
    public int HeartbeatSec { get; set; } = 30;
    public List<CameraTemplate> Cameras { get; set; } = new();

    public CameraTemplate? Find(string serial) =>
        Cameras.FirstOrDefault(c => c.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase));
}

/// <summary>یک ردیف لاگ فعالیت دروازه — برای پنل مدیریت.</summary>
public sealed record GatewayActivity(DateTime Time, string Level, string Source, string Message);

/// <summary>حالت زندهٔ یک دستگاه (پنل مدیریت + تصمیم‌های supervisor).</summary>
public sealed class DeviceLiveState
{
    public string Serial = "";
    public string? Name;
    public string Transport = "tcp";
    public volatile bool Online;
    public DateTime? LastPoll;
    public string? LastError;
    public long OkCommands, FailedCommands, PunchesTotal;
    public volatile int PendingCommands;
}

/// <summary>نگاشت شناسه کوتاه دستور (۸ کاراکتر) به شناسه کامل — تطبیق ACK پروتکل ADMS.</summary>
public sealed class AdmsPendingMap
{
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _map = new();

    public void Set(string shortId, string commandId)
    {
        lock (_lock) { _map[shortId] = commandId; }
    }

    public string? Take(string shortId)
    {
        lock (_lock)
        {
            if (_map.Remove(shortId, out var id)) return id;
            return null;
        }
    }

    public int Count { get { lock (_lock) { return _map.Count; } } }
}

/// <summary>پیکربندی زنده — معادل GatewayRuntimeConfig سرویس ویندوزی.</summary>
public sealed class GatewayRuntimeConfig
{
    public volatile bool ModuleOn = true;      // لایهٔ ۱–۳: نصب/مجوز/کلید مدیر (IModuleAvailability)
    public volatile bool Enabled = true;       // کلید GatewaySetting.Enabled (لایهٔ تنظیمات)
    public volatile int TcpTimeoutMs = 6000;
    public volatile int PollIntervalSec = 60;
    public volatile int SweeperIntervalSec = 60;
    public volatile int ConfigRefreshSec = 30;
    public volatile int DefaultDevicePort = 4370;
    public volatile int DefaultCommKey = 0;
    public volatile int ConfigVersion;
    public volatile bool AnprEnabled = true;   // کلید Gateway:Anpr:Enabled از appsettings

    /// <summary>دستگاه‌های فعال — کلید: سریال.</summary>
    public readonly ConcurrentDictionary<string, GatewayDeviceCfg> Devices = new();

    /// <summary>پولرهای در حال اجرا — کلید: سریال.</summary>
    public readonly ConcurrentDictionary<string, (CancellationTokenSource Cts, Task Loop)> Pollers = new();

    /// <summary>دوربین‌های فعال + قالب HTTP راهبند — کلید: سریال.</summary>
    public readonly ConcurrentDictionary<string, (GatewayCameraCfg Cam, CameraTemplate? Tpl, int HeartbeatSec)> Cameras = new();

    /// <summary>حلقه‌های ضربان دوربین‌ها — کلید: سریال.</summary>
    public readonly ConcurrentDictionary<string, (CancellationTokenSource Cts, Task Loop)> CameraLoops = new();

    public bool FullyEnabled => ModuleOn && Enabled;
    public int PollerCount => Pollers.Count;
    public int CameraCount => Cameras.Count;
}

/// <summary>
/// ظرف وضعیت دروازه — singleton ماژول؛ پنل /DeviceGateway از همین می‌خواند و
/// worker/پل‌ها در همین می‌نویسند. صفر وابستگی به بانک.
/// فاز ۶۴ — Relay: پورت اختصاصی ADMS (شفاف به پورت اپ).
/// </summary>
public sealed class GatewayRuntime
{
    public readonly GatewayRuntimeConfig Config = new();
    public readonly AdmsPendingMap Pending = new();
    public readonly AdmsRelay Relay = new();
    public readonly ConcurrentDictionary<string, DeviceLiveState> Live = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _logLock = new();
    private readonly List<GatewayActivity> _log = new();
    private const int LogCap = 300;

    public DeviceLiveState LiveOf(string serial, string? name, string transport)
    {
        return Live.GetOrAdd(serial, _ => new DeviceLiveState { Serial = serial, Name = name, Transport = transport });
    }

    public void Log(string level, string source, string message)
    {
        lock (_logLock)
        {
            _log.Add(new GatewayActivity(DateTime.Now, level, source, message));
            if (_log.Count > LogCap) _log.RemoveRange(0, _log.Count - LogCap);
        }
    }

    public List<GatewayActivity> Recent(int n = 150)
    {
        lock (_logLock)
            return _log.Count <= n ? new List<GatewayActivity>(_log) : _log.GetRange(_log.Count - n, n);
    }

    public void ClearLog()
    {
        lock (_logLock) _log.Clear();
    }
}
