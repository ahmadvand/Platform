using HrDeviceGateway.Config;
using HrDeviceGateway.Services;
using HrDeviceGateway.Workers;
using Microsoft.Extensions.Logging;

// ============================================================
// HrDeviceGateway — سرویس ویندوزی مدیریت دستگاه‌های تردد
// ASP.NET Core 8: Kestrel (پل ADMS) + Worker Service (poller های TCP)
// تنظیمات از پنل ادمین (api/gateway/config) به‌صورت پویا دریافت می‌شود (فاز ۲۱)؛
// appsettings.json فقط نقش bootstrap (آدرس سرور + حساب سرویس پل + مقادیر اولیه) دارد.
// نصب به‌عنوان Windows Service با scripts/install-service.bat
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// میزبانی: ویندوز سرویس + systemd (لینوکس) — هر کدام که برقرار باشد فعال می‌شود
builder.Host.UseWindowsService(o => o.ServiceName = "HrDeviceGateway");
builder.Host.UseSystemd();

// تنظیمات bootstrap
builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("Gateway"));

// لاگ Event Viewer روی ویندوز (فاز ۱۲): سرویس‌ها کنسول ندارند؛
// منبع «HrDeviceGateway» با install-service.bat از قبل ثبت می‌شود تا پیام‌ها
// در Event Viewer → Windows Logs → Application با منبع اختصاصی دیده شوند.
if (OperatingSystem.IsWindows())
{
    // گارد بالا برای تحلیل‌گر CA1416 کافی است؛ هشدار باقی‌مانده روی setterهای داخل لامبدا خاموش می‌شود
#pragma warning disable CA1416
    builder.Logging.AddEventLog(o =>
    {
        o.SourceName = "HrDeviceGateway";
        o.LogName = "Application";
    });
#pragma warning restore CA1416
}

// پل ADMS به روی پورت پیکربندی‌شده گوش می‌دهد (دستگاه‌های تردد + دوربین‌های پلاک‌خوان — فاز ۴۴)
int listenPort = builder.Configuration.GetValue("Gateway:ListenPort", 8081);
builder.WebHost.UseUrls($"http://0.0.0.0:{listenPort}");

// پیکربندی زنده — مشترک بین DeviceWorker و endpoint سلامت
var runtime = new GatewayRuntimeConfig();
builder.Services.AddSingleton(runtime);
builder.Services.AddSingleton<ApiClient>();
builder.Services.AddHostedService<DeviceWorker>();

var app = builder.Build();

// ---------- endpoint های ADMS ----------
var api = app.Services.GetRequiredService<ApiClient>();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
// نگاشت pending دستورات مشترک بین ADMS و Worker
var pending = new AdmsBridge.PendingMap();
AdmsBridge.MapEndpoints(app, api, pending, loggerFactory);

// ---------- endpoint های دوربین‌های پلاک‌خوان (فاز ۴۴) ----------
AnprBridge.MapEndpoints(app, api, loggerFactory);

// ---------- endpoint سلامت (شامل وضعیت کانفیگ پویا) ----------
app.MapGet("/health", (GatewayRuntimeConfig rt) => Results.Json(new
{
    status = "ok",
    service = "HrDeviceGateway",
    version = "2.1.0",
    time = DateTime.Now,
    admsPort = listenPort,
    runtime = new
    {
        enabled = rt.Enabled,
        configVersion = rt.ConfigVersion,
        pollers = rt.PollerCount,
        cameras = rt.CameraCount,
        pollIntervalSec = rt.PollIntervalSec,
        sweeperIntervalSec = rt.SweeperIntervalSec,
        listenPortWanted = rt.ListenPortWanted,
        restartRequired = rt.ListenPortWanted != listenPort,
        logLevel = rt.LogLevel,
    },
}));

app.Logger.LogInformation("HrDeviceGateway آماده است — پل ADMS + پل دوربین‌های پلاک‌خوان روی پورت {Port} (کانفیگ پویا از پنل ادمین)", listenPort);
app.Run();
