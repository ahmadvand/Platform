// ======================================================================
// رجیستری ماژول‌ها — فاز ۶۹: تجمیع‌گر خالصِ مانیفست‌های خودتوصیف
// ----------------------------------------------------------------------
// تا فاز ۶۷ این فایل «فهرست ایستایی» همهٔ ماژول‌ها بود (Core/Org/Devices +
// مانیفست کنترلرها + نگاشت اکشن‌ها + گروه‌های منو + ترتیب گروه‌ها — همه دستی).
// از فاز ۶۹ هیچ نام ماژولی اینجا نیست؛ هر ماژول «خودش را» با IHrUiModule
// معرفی می‌کند:
//   • هستهٔ پلتفرم (core) — تنها ماژول ذاتیِ هاست — خودش را با CoreModule
//     (IsCore=true) در آغاز بوت به HrModuleRuntime.RegisterIntrinsic می‌سپارد؛
//   • همهٔ ماژول‌های دیگر — از جمله پایه‌های سابق org/devices — پلاگین‌های
//     HrModules.*.dll هستند و در بوت کشف می‌شوند (IsCore=false، حذف‌شدنی).
// این فایل فقط «ادغام‌کنندهٔ» مانیفست‌های کشف‌شده است: نگاشت کنترلر/اکشن ←
// ماژول (گیت مسیرها) و ساخت منو از گروه‌های اعلامی خود ماژول‌ها
// (HrNavGroup.Sort — بدون سوئیچ ترتیب گروه در هاست).
//
// قواعد استقلال (قراردادی):
//   ۱) وابستگی فقط یک‌طرفه: ماژول ← پلتفرم/هاست؛ ماژول‌ها به هم ارجاع مستقیم ندارند.
//   ۲) نقاط اشتراک (پرسنل/دستگاه/کارت/گزارش/اعلان) در پلتفرم مشترک یا قراردادهاست.
//   ۳) خاموش/حذف ماژول = حذف منو + مسدود شدن صفحه/API آن — داده‌ها دست‌نخورده می‌مانند.
// ======================================================================
using Platform.Contracts;

namespace Platform.Web.Modules;

/// <summary>تعریف نمایشی یک ماژول — پرتابهٔ مانیفست IHrUiModule برای UI مدیریت/گیت.</summary>
public sealed class HrModule
{
    public required string Code { get; init; }
    public required string Title { get; init; }
    public string Icon { get; init; } = "🧩";
    /// <summary>ذاتیِ هاست — ستون فقرات سامانه؛ همیشه فعال و غیرقابل خاموش‌کردن (فقط core).</summary>
    public bool IsCore { get; init; }
    /// <summary>توضیح کوتاه برای کارت مدیریت ماژول‌ها در تنظیمات.</summary>
    public string Description { get; init; } = "";
    /// <summary>مانیفست کنترلرهای این ماژول (نام کوتاه، lowercase).</summary>
    public string[] Controllers { get; init; } = [];
}

/// <summary>آیتم منو + ماژول مالک آن — شکل نهایی رندر منو در _Layout.</summary>
public sealed record NavItem(string Href, string Icon, string Label, string Perm, string Ctrl, string Act, string Module);

public static class ModuleCatalog
{
    // ------------------------------------------------------------------
    // کشف — یک‌بار در نخستین مصرف (بوت)؛ همهٔ خروجی‌های زیر از همین نتیجه ساخته می‌شوند.
    // مانیفست‌ها از HrModuleRuntime.UiModules می‌آیند: ماژول ذاتی هاست (core)
    // + پلاگین‌های کشف‌شده. این فایل هیچ نام ماژولی نمی‌شناسد.
    // ------------------------------------------------------------------
    private static readonly Lazy<HrModule[]> _all = new(BuildAll);
    private static readonly Lazy<Dictionary<string, string>> _ctrlMap = new(BuildCtrlMap);
    private static readonly Lazy<Dictionary<(string Ctrl, string Act), string>> _actMap = new(BuildActMap);
    private static readonly Lazy<(string Group, NavItem[] Items)[]> _navGroups = new(BuildNavGroups);

    private static HrModule[] BuildAll()
    {
        var list = new List<HrModule>();
        foreach (var ui in HrModuleRuntime.UiModules())
        {
            list.Add(new HrModule
            {
                Code = ui.Code,
                Title = ui.Title,
                Icon = ui.Icon,
                IsCore = ui.IsCore,
                Description = ui.Description,
                Controllers = ui.Controllers,
            });
        }
        var all = list.ToArray();
        // فاز ۶۹ — حافظهٔ مانیفست (App_Data/modules.manifest.json): برای گیت دوستانهٔ
        // ماژولی که بعداً DLL‌اش از نصب حذف شود — منبع شناخت = داده، نه کد هاست.
        ModuleManifestStore.Save(all);
        return all;
    }

    public static IReadOnlyList<HrModule> All => _all.Value;

    public static HrModule? Find(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : All.FirstOrDefault(m => m.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------
    // نگاشت (کنترلر، اکشن) ← ماژول — مرجع گیت مسیرها (ModuleGate).
    // کنترلر ناشناخته → null → هرگز مسدود نمی‌شود (شکست باز؛ سازگاری کامل).
    // ------------------------------------------------------------------

    private static Dictionary<string, string> BuildCtrlMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in All)
            foreach (var c in m.Controllers)
            {
                var key = c.Trim().ToLowerInvariant();
                if (!map.TryAdd(key, m.Code))
                    throw new InvalidOperationException($"کنترلر «{key}» به دو ماژول نسبت داده شده: {map[key]} و {m.Code}");
            }
        return map;
    }

    private static Dictionary<(string Ctrl, string Act), string> BuildActMap()
    {
        var map = new Dictionary<(string Ctrl, string Act), string>();
        foreach (var ui in HrModuleRuntime.UiModules())
            foreach (var (ctrl, act) in ui.OwnedActions)
            {
                var key = (ctrl.Trim().ToLowerInvariant(), act.Trim().ToLowerInvariant());
                if (map.TryGetValue(key, out var prev) && prev != ui.Code)
                    throw new InvalidOperationException($"اکشن «{ctrl}.{act}» به دو ماژول نسبت داده شده: {prev} و {ui.Code}");
                map[key] = ui.Code;
            }
        return map;
    }

    /// <summary>کد ماژول مسیر جاری — null یعنی خارج از ماژول‌بندی (مسدود نمی‌شود).</summary>
    public static string? ModuleOf(string? controller, string? action)
    {
        if (string.IsNullOrWhiteSpace(controller)) return null;
        var c = controller.Trim().ToLowerInvariant();
        var a = action?.Trim().ToLowerInvariant();
        if (a is not null && _actMap.Value.TryGetValue((c, a), out var m)) return m;
        return _ctrlMap.Value.TryGetValue(c, out var cm) ? cm : null;
    }

    // ------------------------------------------------------------------
    // منو — ادغام گروه‌های اعلامی همهٔ ماژول‌ها (ذاتی + پلاگین). ترتیب گروه از
    // HrNavGroup.Sort خود ماژول‌ها می‌آید (هم‌نام = کوچک‌ترین Sort) — بدون هیچ
    // فهرست ایستایی در هاست. ترتیب آیتم‌ها با Sort همان ترتیب قدیمی است.
    // ------------------------------------------------------------------

    private static (string Group, NavItem[] Items)[] BuildNavGroups()
    {
        var groups = new Dictionary<string, List<(int Sort, NavItem Item)>>(StringComparer.Ordinal);
        var sorts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var ui in HrModuleRuntime.UiModules())
            foreach (var g in ui.NavGroups)
            {
                // ترتیب گروه = کوچک‌ترین Sort اعلام‌شده برای آن نام بین همهٔ ماژول‌ها
                if (!sorts.TryGetValue(g.Name, out var cur) || g.Sort < cur) sorts[g.Name] = g.Sort;
                foreach (var it in g.Items)
                {
                    if (!groups.TryGetValue(g.Name, out var list))
                    {
                        list = new List<(int, NavItem)>();
                        groups[g.Name] = list;
                    }
                    list.Add((it.Sort, new NavItem(
                        it.Href, it.Icon, it.Label, it.Perm, it.Ctrl, it.Act ?? "", it.Module ?? ui.Code)));
                }
            }

        return groups
            .OrderBy(g => sorts.GetValueOrDefault(g.Key, 1000))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, g.Value.OrderBy(x => x.Sort).Select(x => x.Item).ToArray()))
            .ToArray();
    }

    /// <summary>ساخت منو — آیتم‌های ماژول خاموش حذف می‌شوند؛ گروه خالی رندر نمی‌شود.</summary>
    public static (string Group, (string Href, string Icon, string Label, string Perm, string Ctrl, string Act)[] Items)[]
        BuildNav(Func<string, bool> moduleEnabled)
        => _navGroups.Value
            .Select(g => (g.Group,
                Items: g.Items.Where(i => moduleEnabled(i.Module))
                    .Select(i => (i.Href, i.Icon, i.Label, i.Perm, i.Ctrl, i.Act)).ToArray()))
            .Where(g => g.Items.Length > 0)
            .Select(g => (g.Group, g.Items))
            .ToArray();

    /// <summary>فاز ۵۷ — ماژولِ «نصب‌نشده»ای که این نام کنترلر به آن تعلق دارد (null = مرتبط نیست).
    /// فقط برای مسیرهای بدون endpoint کار می‌برد: وقتی DLL ماژول در بسته نیست، روتینگ اصلاً
    /// endpoint نمی‌سازد و RouteValues خالی می‌ماند؛ این‌جا با مانیفست کنترلرها تشخیص داده
    /// می‌شود تا گیت، صفحهٔ دوستانهٔ «نصب نشده» را نشان دهد (به‌جای ۴۰۴ خام).
    /// فاز ۶۹ — مانیفستِ ماژولِ حذف‌شده از «حافظهٔ» آخرین بوت سالم هم خوانده می‌شود
    /// (ModuleManifestStore) — نصب تازه‌ای که هرگز آن ماژول را نداشته ۴۰۴ طبیعی می‌گیرد.</summary>
    public static string? NotInstalledControllerModule(string controllerLower)
    {
        var cur = All.FirstOrDefault(m => !HrBuildModules.Installed(m.Code)
                && m.Controllers.Contains(controllerLower, StringComparer.Ordinal))?.Code;
        if (cur is not null) return cur;
        // فاز ۶۹ — ماژول حذف‌شده: مانیفستش دیگر در All نیست؛ از حافظهٔ داده‌محور
        // (فقط وقتی آن کد واقعاً نصب نیست — وگرنه ممکن است مانیفست حافظه کهنه باشد)
        var mem = ModuleManifestStore.FindByController(controllerLower);
        return mem is not null && !HrBuildModules.Installed(mem.Code) ? mem.Code : null;
    }
}
