using Platform.Contracts;

namespace HrModules.Devices;

// ======================================================================
// فاز ۶۹ — معرفی خودِ ماژول تجهیزات و گیت‌وی به هاست
// تا فاز ۶۸ این مانیفست (کنترلرها + آیتم‌های منو) در رجیستری هاست بود؛
// از این فاز ماژول «خودش را» معرفی می‌کند. حذف DLL = غیبت کامل (نه خاموشی).
// پل‌های API دستگاه (authapi/gatewayconfig/deviceswebhook) هم همین‌جایند.
// ======================================================================
public sealed class DevicesUiModule : IHrUiModule
{
    public string Code => "devices";
    public string Title => "تجهیزات و گیت‌وی";
    public string Icon => "🖥️";
    public string Description => "دستگاه‌های تردد، داده‌های هویتی/بیومتریک، تنظیمات گیت‌وی و وب‌هوک‌ها — تغذیه‌کنندهٔ حضور، کنترل تردد و پارکینگ";

    public string[] Controllers => ["devices", "biometrics", "gateway", "authapi", "gatewayconfig", "deviceswebhook"];

    public (string Ctrl, string Act)[] OwnedActions => [];

    public HrNavGroup[] NavGroups =>
    [
        new("کنترل تردد و حراست", 40,
        [
            new HrNavItem("/Devices", "🖥️", "دستگاه‌های تردد", "devices.view", "Devices", "", Sort: 10),
            new HrNavItem("/Devices/LiveView", "📡", "نمای زنده حراست", "devices.view", "Devices", "LiveView", Sort: 30),
            new HrNavItem("/Biometrics", "🧬", "داده‌های هویتی", "biometrics.view", "Biometrics", "", Sort: 70),
        ]),
        new("مدیریت سیستم", 90,
        [
            new HrNavItem("/Gateway", "🖧", "تنظیمات گیت‌وی", "gateway.view", "Gateway", "", Sort: 50),
        ]),
    ];
}
