using Platform.Contracts;

namespace Platform.Web.Modules;

/// <summary>
/// پیاده‌سازی قرارداد در دسترس بودن ماژول (فاز ۶۳) — همان سه لایهٔ کنترل
/// فاز ۵۴–۵۸: نصب در بیلد (HrBuildModules) + کلید مدیر (modules.json) +
/// سقف مجوز (LicensePayload.Modules). دروازهٔ دستگاه‌ها از همین مسیر
/// می‌فهمد که باید pollerها را نگه دارد یا متوقف کند.
/// </summary>
public sealed class ModuleAvailability : IModuleAvailability
{
    private readonly ModuleStateService _state;
    public ModuleAvailability(ModuleStateService state) { _state = state; }

    public bool Enabled(string code) => _state.Enabled(code);
}
