using Platform.Contracts;

namespace HrModules.DeviceGateway;

// فاز ۶۸ — معرفی خودِ ماژول دروازهٔ دستگاه‌ها به هاست
public sealed class GatewayUiModule : IHrUiModule
{
    public string Code => "gateway";
    public string Title => "دروازهٔ دستگاه‌ها";
    public string Icon => "🖧";
    public string Description => "پایش درون‌اپی دستگاه‌های تردد (poll TCP + پل ADMS) و دوربین‌های پلاک‌خوان (پل ANPR + حلقهٔ راهبند) — جایگزین سرویس ویندوزی مستقل";

    public string[] Controllers => ["devicegateway"];

    public (string Ctrl, string Act)[] OwnedActions => [];

    public HrNavGroup[] NavGroups =>
    [
        new("کنترل تردد و حراست", 40,
        [
            new HrNavItem("/DeviceGateway", "🖧", "دروازهٔ دستگاه‌ها", "devices.view", "DeviceGateway", "", Sort: 20),
        ]),
    ];
}
