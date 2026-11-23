using Platform.Web.Controllers.Api;
using Platform.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// ======================================================================
// فاز ۶۳ — بوت‌استرپ ماژول پارکینگ: ثبت خدمات دوربین برای دروازهٔ دستگاه‌ها
// ----------------------------------------------------------------------
// ماژول دروازه (HrModules.DeviceGateway) حلقهٔ ضربان/دستورات دوربین‌ها و
// خوانش پلاک را از IParkingGatewaySink می‌گیرد. این کلاس (که هاست با بازتاب
// می‌یابد) پیاده‌سازی را فقط وقتی ثبت می‌کند که DLL پارکینگ در بسته باشد —
// در بستهٔ بدون پارکینگ هیچ چیزی ثبت نمی‌شود و دروازه دوستانه کنار می‌رود.
// ======================================================================
namespace Platform.Modules.Parking;

public sealed class ParkingBootstrapper : IHrModuleBootstrapper
{
    public string ModuleCode => "parking";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IParkingGatewaySink, ParkingGatewaySink>();
    }

    public void ConfigureApp(WebApplication app)
    {
        // endpointهای پارکینگ از قبل توسط MVC اکتشاف می‌شوند — کاری ندارد
    }
}
