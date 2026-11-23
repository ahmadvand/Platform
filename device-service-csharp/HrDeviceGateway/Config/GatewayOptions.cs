namespace HrDeviceGateway.Config;

/// <summary>تنظیمات گیت‌وی — معادل config.json نسخه Node، این‌بار از appsettings.json خوانده می‌شود.</summary>
public sealed class GatewayOptions
{
    /// <summary>آدرس پایه سامانه مرکزی (Next.js)</summary>
    public string ApiBase { get; set; } = "http://localhost:3000";

    /// <summary>کاربر سرویس پل (نقش bridge با مجوز devices.poll)</summary>
    public string Username { get; set; } = "svc_bridge";

    public string Password { get; set; } = "Bridge@1234";

    /// <summary>پورت گوش‌دهی پل ADMS — دستگاه‌های PUSH به این پورت متصل می‌شوند</summary>
    public int ListenPort { get; set; } = 8081;

    /// <summary>زمان انتظار (میلی‌ثانیه) برای اتصال/پاسخ TCP دستگاه</summary>
    public int TcpTimeoutMs { get; set; } = 6000;

    /// <summary>فاصله پیش‌فرض poll هر دستگاه TCP (ثانیه)</summary>
    public int PollIntervalSec { get; set; } = 60;

    /// <summary>فاصله بازبینی وضعیت دستگاه‌ها (ثانیه)</summary>
    public int SweeperIntervalSec { get; set; } = 60;

    /// <summary>فهرست دستگاه‌های TCP تحت مدیریت مستقیم (دستگاه‌های ADMS خودکار متصل می‌شوند)</summary>
    public List<DeviceConfig> Devices { get; set; } = new();

    /// <summary>تنظیمات دوربین‌های پلاک‌خوان — فاز ۴۴ (سرور فهرست دوربین‌ها را می‌فرستد؛ اینجا فقط bootstrap و قالب‌های HTTP راهبند)</summary>
    public AnprOptions Anpr { get; set; } = new();
}

/// <summary>تنظیمات بخش دوربین‌های پلاک‌خوان گیت‌وی.</summary>
public sealed class AnprOptions
{
    /// <summary>پایش/ضربان دوربین‌ها فعال باشد؟</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>فاصله پیش‌فرض ضربان هر دوربین با سرور (ثانیه) — اگر دوربین مقدار نداشته باشد</summary>
    public int HeartbeatSec { get; set; } = 30;

    /// <summary>
    /// تنظیمات اختصاصی هر دوربین — کلید: Serial (باید با دوربین ثبت‌شده در پنل یکی باشد).
    /// فهرست دوربین‌های فعال از سرور می‌آید؛ اینجا فقط قالب‌های HTTP راهبند/ری‌استارت سازندهٔ دوربین
    /// (vendor-specific) و اعتبار HTTP آن تنظیم می‌شود — {ip} در قالب با IP دوربین جایگزین می‌شود.
    /// </summary>
    public List<CameraConfig> Cameras { get; set; } = new();
}

/// <summary>قالب HTTP اختیاری یک دوربین برای اجرای دستورات راهبند (open_barrier/close_barrier/restart).</summary>
public sealed class CameraConfig
{
    public string Serial { get; set; } = "";
    public string? Name { get; set; }
    public string? Ip { get; set; }
    public string? Brand { get; set; }
    public bool Enabled { get; set; } = true;
    public int HeartbeatSec { get; set; } = 30;

    /// <summary>قالب HTTP بازکردن راهبند — مثال: http://{ip}/ISAPI/System/IO/outputs/1/trigger</summary>
    public string? OpenUrl { get; set; }

    /// <summary>قالب HTTP بستن راهبند (اختیاری)</summary>
    public string? CloseUrl { get; set; }

    /// <summary>قالب HTTP ری‌استارت دوربین (اختیاری)</summary>
    public string? RestartUrl { get; set; }

    /// <summary>اعتبار Basic HTTP خود دوربین (اختیاری)</summary>
    public string? HttpUser { get; set; }
    public string? HttpPass { get; set; }
}

/// <summary>تعریف یک دستگاه تردد در گیت‌وی.</summary>
public sealed class DeviceConfig
{
    /// <summary>شماره سریال دستگاه — باید با سریال ثبت‌شده در سرور یکی باشد</summary>
    public string Serial { get; set; } = "";

    /// <summary>نوع اتصال: tcp (مستقیم به پورت 4370) یا adms (دستگاه به پل Push می‌کند)</summary>
    public string Transport { get; set; } = "tcp";

    public string? Name { get; set; }

    public string? Ip { get; set; }

    public int Port { get; set; } = 4370;

    /// <summary>کلید ارتباطی دستگاه (CommKey) — پیش‌فرض صفر</summary>
    public int CommKey { get; set; } = 0;

    public int PollIntervalSec { get; set; } = 60;
}
