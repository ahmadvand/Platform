using HrModules.DeviceGateway.Bridge;
using HrModules.DeviceGateway.Runtime;
using Platform.Contracts;

// ======================================================================
// فاز ۶۳ — بوت‌استرپ ماژول دروازهٔ دستگاه‌ها
// ----------------------------------------------------------------------
// هاست بدون ارجاع ایستا (بازتاب HrModuleRuntime در Platform.Contracts)
// این کلاس را می‌یابد:
//   • ConfigureServices → وضعیت زنده + BackgroundService حلقهٔ اصلی
//   • ConfigureApp      → endpointهای ADMS (/iclock/cdata) و ANPR (/anpr/…)
// در بستهٔ مشتریِ بدون این ماژول، این کلاس وجود ندارد و هیچ‌کدام ثبت
// نمی‌شود — سرویس ویندوزی مستقل همچنان مسیر جایگزین استقرار جداگانه است.
// ======================================================================
namespace HrModules.DeviceGateway;

public sealed class GatewayBootstrapper : IHrModuleBootstrapper
{
    public string ModuleCode => GatewayWorker.ModuleCode;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // قالب‌های HTTP راهبند دوربین‌ها (vendor-specific) — همان کلیدهای سرویس ویندوزی:
        // "Gateway": { "Anpr": { "Enabled": true, "HeartbeatSec": 30, "Cameras": [ … ] } }
        var anpr = new AnprTemplateOptions();
        configuration.GetSection("Gateway:Anpr").Bind(anpr);

        // فاز ۶۴ — سرویس گیت‌وی خارجی (استقرار ترکیبی):
        // "Gateway": { "ExternalService": { "Url": "http://127.0.0.1:8081", "TimeoutSec": 3 } }
        var ext = new ExternalServiceOptions();
        configuration.GetSection("Gateway:ExternalService").Bind(ext);

        services.AddSingleton(anpr);
        services.AddSingleton(ext);
        services.AddSingleton<GatewayRuntime>();
        services.AddHttpClient(); // فاز ۶۴ — فراخوانی /health سرویس خارجی از پنل
        services.AddHostedService<GatewayWorker>();
    }

    public void ConfigureApp(WebApplication app)
    {
        AdmsEndpoints.Map(app);
        AnprEndpoints.Map(app);
    }
}
