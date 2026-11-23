using Platform.Contracts;

namespace Platform.Web.Modules;

// ======================================================================
// فاز ۶۹ — «خودِ» هستهٔ پلتفرم — تنها ماژول ذاتیِ هاست
// ----------------------------------------------------------------------
// تا فاز ۶۸ این تعریف (همراه با org/devices و فهرست گروه‌های منوی پایه)
// در ModuleCatalog زندگی می‌کرد — یعنی هاست «فهرست» ماژول‌ها را می‌شناخت.
// از این فاز هسته هم مثل هر ماژول دیگری «خودش را معرفی می‌کند» و
// ModuleCatalog فقط از مانیفست‌های کشف‌شده (ذاتی + پلاگین) می‌خواند.
// تنها تفاوت: IsCore=true — ستون فقرات؛ همیشه فعال و غیرقابل حذف.
// همهٔ ماژول‌های دیگر (حتی پایه‌های سابق org/devices) پلاگین حذف‌شدنی‌اند.
// ======================================================================
public sealed class CoreModule : IHrUiModule
{
    public string Code => "core";
    public string Title => "هستهٔ پلتفرم";
    public string Icon => "⚙️";
    public bool IsCore => true;
    public string Description => "ورود و کاربران و نقش‌ها، شرکت‌ها، ویزارد راه‌اندازی و تنظیمات، ممیزی، اعلان‌ها، گزارش‌ساز، پرتال «من»، مجوز نصب و پوستهٔ UI — بدون این هیچ ماژولی بالا نمی‌آید";

    public string[] Controllers =>
    [
        "home", "auth", "account", "setup", "license", "settings",
        "users", "companies", "notifications", "audit",
        "self", "ess", "reports", "export", "datatransfer",
    ];

    public (string Ctrl, string Act)[] OwnedActions => [];

    public HrNavGroup[] NavGroups =>
    [
        new("عمومی", 10,
        [
            new HrNavItem("/", "▦", "پیشخوان یکپارچه", "dashboard.view", "Home", "", Sort: 10), // فاز ۱۲۵ — مطابق طرح Figma صفحهٔ ۶
            new HrNavItem("/DataTransfer", "⇄", "درون‌ریزی و برون‌ریزی", "dashboard.view", "DataTransfer", "", Sort: 20), // فاز ۹۹ (بازسازی) — مرکز یکپارچه
        ]),
        new("تحلیل و گزارش", 70,
        [
            new HrNavItem("/Reports", "📊", "گزارش‌ساز تحلیلی", "reports.view", "Reports", "", Sort: 10),
        ]),
        new("پرتال من", 80,
        [
            new HrNavItem("/Self", "🏠", "خانه من", "self.view", "Self", "Index", Sort: 10),
        ]),
        new("مدیریت سیستم", 90,
        [
            new HrNavItem("/Companies", "🏛", "شرکت‌ها", "companies.view", "Companies", "", Sort: 10),
            new HrNavItem("/License", "🧾", "مجوز نصب", "settings.view", "License", "", Sort: 20),
            new HrNavItem("/Users", "🛡️", "کاربران و نقش‌ها", "users.view", "Users", "", Sort: 30),
            new HrNavItem("/Notifications", "🔔", "اعلان‌ها", "notifications.view", "Notifications", "", Sort: 40),
            new HrNavItem("/Settings", "🎛️", "تنظیمات سامانه", "settings.view", "Settings", "", Sort: 60),
            new HrNavItem("/Langs", "🌐", "زبان‌ها و ترجمه", "settings.view", "Langs", "", Sort: 65),
            new HrNavItem("/ApiTokens", "🔑", "توکن‌های API", "settings.view", "ApiTokens", "", Sort: 67), // فاز ۱۳۵ — مورد ۱۹
            new HrNavItem("/Updates", "⬆️", "به‌روزرسانی سامانه", "updates.manage", "Updates", "", Sort: 68), // فاز ۹۳ — دسترسی اختصاصی
            // فاز ۱۱۶ — عنوان «ممیزی» (درخواست کاربر: نام‌گذاری درست Audit)
            new HrNavItem("/Audit", "📜", "ممیزی", "audit.view", "Audit", "", Sort: 70),
        ]),
    ];

    // ======================================================================
    // فاز ۱۲۲ — اعلام خانواده‌های دادهٔ «هستهٔ پلتفرم» برای مرکز درون‌ریزی/برون‌ریزی.
    // هسته ماژول دیتای جدا ندارد (ذاتِ کانتکست است) — اعلامش همان‌جا که خودش را
    // معرفی می‌کند می‌آید: اینجا. هم‌نقش با IHrDataModule.DataExchange ماژول‌ها.
    // ======================================================================
    public static readonly HrDataExchangeDecl[] DataExchangeFamilies =
    [
        new("Company", "شرکت‌ها", "مدیریت سیستم", true, "Code"),
        new("User", "کاربران سامانه", "مدیریت سیستم", true, "Username"),
        new("Role", "نقش‌ها", "مدیریت سیستم", true, "Name"),
        new("Permission", "کاتالوگ دسترسی‌ها", "مدیریت سیستم", true, "Key"),
        new("ColorPalette", "پالت‌های رنگی", "مدیریت سیستم", true, "Name"),
        new("NotificationRule", "قواعد اعلان", "مدیریت سیستم", true, "Name"),
        // پیوندی/تک‌ردیفی/حساس — فقط برون‌بری (درون‌ریزی از مسیر رسمی خودش)
        new("UserRole", "پیوند کاربر-نقش", "مدیریت سیستم", false),
        new("RolePermission", "پیوند نقش-دسترسی", "مدیریت سیستم", false),
        new("RoleScope", "محدودیت رکوردی نقش‌ها", "مدیریت سیستم", false),
        new("AppSettings", "تنظیمات سامانه", "مدیریت سیستم", false),
        new("NotificationSetting", "تنظیمات اعلان", "مدیریت سیستم", false),
        new("NotificationLog", "لاگ اعلان‌ها", "مدیریت سیستم", false),
        new("AuditLog", "لاگ ممیزی", "مدیریت سیستم", false),
        new("SecurityAlert", "هشدارهای امنیتی", "مدیریت سیستم", false),
        new("HrLangDef", "زبان‌های نصب‌شده", "مدیریت سیستم", false),
        new("HrLangEntries", "متن‌های ترجمه", "مدیریت سیستم", false),
        new("HrLangEntry", "ترجمه‌های زبان‌های پویا", "مدیریت سیستم", false),
        new("SsPackage", "بسته‌های محافظ صفحه", "مدیریت سیستم", false),
    ];
}
