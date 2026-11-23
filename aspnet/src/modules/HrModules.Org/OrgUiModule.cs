using Platform.Contracts;

namespace HrModules.Org;

// ======================================================================
// فاز ۶۹ — معرفی خودِ ماژول سازمان و پرسنل به هاست
// تا فاز ۶۸ این مانیفست (کنترلرها + آیتم‌های منو + مالکیت اکشن خروجی پرسنل)
// در رجیستری هاست (ModuleCatalog) بود؛ از این فاز ماژول «خودش را» معرفی
// می‌کند و هاست فقط از مانیفست‌های کشف‌شده می‌خواند. حذف DLL = غیبت کامل.
// ======================================================================
public sealed class OrgUiModule : IHrUiModule
{
    public string Code => "org";
    public string Title => "سازمان و پرسنل";
    public string Icon => "🏢";
    public string Description => "چارت سازمانی، پرسنل، قراردادها و احکام، تعطیلات رسمی و ورود اکسل — پایهٔ مشترک همهٔ ماژول‌های سیستم";

    public string[] Controllers => ["employees", "org", "holidays"];

    public (string Ctrl, string Act)[] OwnedActions =>
    [
        // خروجی اکسل پرسنل (کنترلر Export هاست) + پیوند کاربر ← پرسنل (کنترلر Users هاست)
        ("export", "employees"),
        ("users", "linkemployee"),
    ];

    public HrNavGroup[] NavGroups =>
    [
        new("منابع انسانی", 20,
        [
            new HrNavItem("/Employees", "👤", "پرسنل", "employees.view", "Employees", "", Sort: 10),
            new HrNavItem("/Contracts", "📜", "قراردادها و احکام", "employees.view", "Contracts", "", Sort: 15),
            new HrNavItem("/Org", "🏢", "چارت سازمانی", "org.view", "Org", "", Sort: 20),
            new HrNavItem("/Holidays", "📅", "تعطیلات رسمی", "holidays.view", "Holidays", "", Sort: 40),
        ]),
    ];
}
