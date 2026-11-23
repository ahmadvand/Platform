using System.Text.Json;
using Platform.Web.Services;

namespace Platform.Web.Modules;

// ======================================================================
// وضعیت فعال/غیرفعال ماژول‌ها (فاز ۵۴)
// ----------------------------------------------------------------------
// ذخیره‌گاه: فایل App_Data/modules.json — الگوی همان پیکربندی نصب (hrsettings.bin):
//   { "disabled": ["payroll", ...] }
// عمداً «خارج از بانک» است تا فاز زیرساخت بدون هیچ تغییری در مدل/مهاجرت‌ها
// (صفر ریسک روی SQLite و SQL Server) بالا بیاید؛ داده‌های ماژول‌ها در بانک
// دست‌نخورده می‌مانند و با فعال‌سازی دوباره همه‌چیز برمی‌گردد.
//
// سقف مجوز نصب: اگر فایل مجوز «معتبر» فهرست ماژول داشته باشد (LicensePayload.Modules)
// فقط همان ماژول‌ها قابل فعال‌شدن‌اند؛ مجوز بدون فهرست = همه مجاز (فاز ۸۳ب: بدون مجوز هم همه مجاز)
// (سازگاری کامل با مجوزهای قدیمی).
// ======================================================================

public sealed class ModuleStateService
{
    private readonly IHostEnvironment _env;
    private readonly LicenseService _lic;
    private readonly ILogger<ModuleStateService> _log;

    private readonly object _lock = new();
    private (string Path, DateTime Write)? _stamp;
    private HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ModuleStateService(IHostEnvironment env, LicenseService lic, ILogger<ModuleStateService> log)
    {
        _env = env; _lic = lic; _log = log;
    }

    private string FilePath => Path.Combine(_env.ContentRootPath, "App_Data", "modules.json");

    /// <summary>مجموعهٔ ماژول‌های خاموش — فایل‌پشتیبان با کش بر اساس زمان نوشتن فایل.</summary>
    public IReadOnlySet<string> DisabledSet()
    {
        Load();
        return _disabled;
    }

    /// <summary>ماژول فعال است؟ — پایه‌ها همیشه فعال؛ ملاک: نصب‌بودن در بیلد (فاز ۵۷)
    /// + حضور واقعی فایل DLL دیتا در زمان اجرا (فاز ۶۷) + نبود در فهرست خاموش‌ها + اجازهٔ مجوز.
    /// «نصب نیست» یعنی DLL ماژول اصلاً در بسته نیست (بیلد مشتری بدون آن ماژول)
    /// یا فایل DLL از پوشهٔ اجرا حذف شده است.</summary>
    public bool Enabled(string code)
    {
        var m = ModuleCatalog.Find(code);
        if (m is null || m.IsCore) return true;
        if (!HrBuildModules.Installed(code)) return false; // نصب نشده — نه فقط خاموش
        if (!Platform.Contracts.HrModulePresence.DataInstalled(code)) return false; // فاز ۶۷ — DLL دیتا حاضر نیست
        return !DisabledSet().Contains(code) && LicenseAllows(code);
    }

    /// <summary>سقف مجوز نصب — فقط وقتی مجوز «معتبر» فهرست ماژول داشته باشد؛
    /// مجوز بدون فهرست = همهٔ ماژول‌ها مجاز (سازگاری با مجوزهای قدیمی).</summary>
    public bool LicenseAllows(string code)
    {
        var s = _lic.Current;
        if (s.State != LicenseState.Valid) return true;
        var mods = s.Modules;
        if (mods is null || mods.Length == 0) return true;
        return mods.Contains(code, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>فاز ۶۸ — آیا این کد برای «این نصب» شناخته‌شده است اما DLL آن حاضر نیست؟
    /// (فهرست خاموش‌های مدیر یا مجوز نصب). برای صفحهٔ دوستانهٔ «نصب نشده» بدون هیچ فهرست
    /// ایستایی در هاست — منبع شناخت = دادهٔ پیکربندی/مجوز، نه کد.</summary>
    public bool KnownButNotInstalled(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (HrBuildModules.Installed(code)) return false;
        return DisabledSet().Contains(code) || !LicenseAllows(code);
    }

    /// <summary>وضعیت برای UI مدیریت ماژول‌ها — (فعال؟، قفل‌شده با مجوز؟). پایه‌ها: قفل نمایشی.</summary>
    public (bool Enabled, bool LicenseLocked) StatusOf(string code)
    {
        var m = ModuleCatalog.Find(code);
        if (m is null || m.IsCore) return (true, true);
        return (Enabled(code), !LicenseAllows(code));
    }

    /// <summary>تغییر وضعیت یک ماژول — با گاردهای «پایه خاموش‌نشدنی» و «سقف مجوز».
    /// نوشتن اتمیک (tmp + move) و باطل‌کردن کش بلافاصله؛ داده‌های بانک هرگز دست نمی‌خورد.</summary>
    public (bool Ok, string Message) SetEnabled(string code, bool enabled)
    {
        var m = ModuleCatalog.Find(code);
        if (m is null) return (false, "ماژول شناخته نشده است");
        if (m.IsCore) return (false, "ماژول‌های پایه ستون فقرات سامانه‌اند و قابل خاموش‌کردن نیستند");
        if (!HrBuildModules.Installed(code))
            return (false, "ماژول «" + m.Title + "» در این نسخهٔ نصب‌شده وجود ندارد — برای افزودن آن، بستهٔ شامل این ماژول را نصب کنید");
        if (enabled && !LicenseAllows(code))
            return (false, "این ماژول در مجوز نصب فعال نشده است — برای افزودن آن، مجوز جدید با فهرست ماژول‌ها دریافت کنید");

        lock (_lock)
        {
            Load();
            var next = new HashSet<string>(_disabled, StringComparer.OrdinalIgnoreCase);
            if (enabled) next.Remove(code); else next.Add(code);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var json = JsonSerializer.Serialize(new StateDoc { Disabled = next.OrderBy(x => x).ToArray() }, JsonOpts);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, FilePath, overwrite: true);
                _disabled = next;
                _stamp = null;
                return (true, enabled
                    ? $"ماژول «{m.Title}» فعال شد — منو و صفحه‌های آن از همین لحظه در دسترس است"
                    : $"ماژول «{m.Title}» غیرفعال شد — منو و صفحه‌های آن پنهان است؛ داده‌ها محفوظ می‌مانند");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "نوشتن وضعیت ماژول‌ها ممکن نشد");
                return (false, "ذخیرهٔ وضعیت ماژول ممکن نشد: " + ex.Message);
            }
        }
    }

    private void Load()
    {
        var fp = FilePath;
        DateTime w;
        try { w = File.Exists(fp) ? File.GetLastWriteTimeUtc(fp) : DateTime.MinValue; }
        catch { w = DateTime.MinValue; }
        if (_stamp == (fp, w)) return;
        try
        {
            if (File.Exists(fp))
            {
                var doc = JsonSerializer.Deserialize<StateDoc>(File.ReadAllText(fp), JsonOpts);
                _disabled = new HashSet<string>(
                    (doc?.Disabled ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)),
                    StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "خواندن وضعیت ماژول‌ها ممکن نشد — همهٔ ماژول‌ها فعال فرض می‌شوند");
        }
        _stamp = (fp, w);
    }

    private sealed class StateDoc
    {
        public string[] Disabled { get; set; } = Array.Empty<string>();
    }
}
