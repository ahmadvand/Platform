// ======================================================================
// فاز ۶۸ — قرارداد پلاگین واقعی: هاست هیچ شناختی از ماژول‌ها ندارد
// ----------------------------------------------------------------------
// تا فاز ۶۷ هاست (csproj + ModuleCatalog + ModuleRegistration + AppDbContext)
// «فهرست» ماژول‌ها را می‌شناخت (ارجاع ایستا + ثابت کامپایل + مانیفست محلی).
// از این فاز هاست فقط این قراردادها را می‌شناسد؛ هر ماژول خودش را معرفی
// می‌کند و HrModuleRuntime در بوت، DLLهای HrModules.* را کشف و لود می‌کند:
//   • IHrDataModule  (HrModules.X.Data.dll)  → ثبت DbContext/قراردادها/سیدر
//     + ترکیب اسکیمای EF + سید داده‌ها + آبشار حذف شرکت
//     (بدون آن ماژول: نه ثبت DI، نه جدول، نه سید، نه مسیر)
//   • IHrUiModule    (HrModules.X.dll)       → بخش MVC + مانیفست کنترلرها
//     + آیتم‌های منو + مالکیت اکشن‌های چند-ماژولی (پرتال من/گزارش/خروجی)
// ماژول‌های پایه (core/org/devices) کدشان در خود هاست است و به‌صورت
// «ماژول توکار» در رجیستری هاست ثبت می‌شوند — نه از طریق DLL.
// ======================================================================
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Contracts;

/// <summary>آیتم منوی ماژول — همان شکل NavItem قدیمی رجیستری هاست (فاز ۵۴).
/// Act اختیاری (اکشن پیش‌فرض کنترلر)؛ Module اختیاری (مالکِ آیتم — پیش‌فرض خود ماژول؛
/// برای آیتم‌های چند-ماژولی مثل «تنظیمات گیت‌وی» زیر گروه devices مقداردهی می‌شود).
/// Sort برای بازتولید دقیق ترتیب آیتم‌ها در گروه‌های ادغام‌شده (پیش‌فرض ۱۰۰).</summary>
public sealed record HrNavItem(string Href, string Icon, string Label, string Perm, string Ctrl,
    string? Act = null, string? Module = null, int Sort = 100);

/// <summary>
/// فاز ۶۹ — گروه منوی ماژول «همراه با ترتیب خودش»: هر ماژول خودش اعلام می‌کند
/// گروه‌های منویش کدام‌اند و در چه جایگاهی می‌نشینند — هاست هیچ سوئیچ ترتیب
/// گروهی با نام گروه‌ها/ماژول‌ها ندارد (سوابق GroupOrder حذف شد). ادغام گروه‌های
/// هم‌نام چند ماژول: ترتیب گروه = کوچک‌ترین Sort اعلام‌شده برای آن نام.
/// </summary>
public sealed record HrNavGroup(string Name, int Sort, HrNavItem[] Items);

/// <summary>
/// فاز ۱۲۲ — اعلام یک خانوادهٔ داده برای «مرکز درون‌ریزی و برون‌ریزی».
/// هر ماژول خودش اعلام می‌کند کدام خانواده‌های داده‌اش در هاب بیایند، با چه
/// عنوان و گروه نمایشی، آیا درون‌ریزی مجاز است و کلید تجاری هم‌آوری چیست.
/// هاست هیچ فهرست ایستایی از خانواده‌ها ندارد؛ خانوادهٔ اعلام‌نشده در هاب
/// رندر نمی‌شود و ماژول غایب/خاموش = هیچ ردیفی از او در هاب نیست.
/// </summary>
/// <param name="Entity">نام کلاس موجودیت (ClrName) — باید از FamilyTypes همین ماژول باشد.</param>
/// <param name="Title">عنوان فارسی نمایشی در هاب.</param>
/// <param name="Group">گروه نمایشی (سرستون جدول هاب).</param>
/// <param name="CanImport">درون‌ریزی مجاز؟ (false = فقط برون‌بری — لاگ/محاسباتی/تک‌ردیفی).</param>
/// <param name="BusinessKey">کلید تجاری هم‌آوری برای upsert درون‌ریزی (غایب = فقط با Id).</param>
public sealed record HrDataExchangeDecl(
    string Entity, string Title, string Group, bool CanImport = true, string? BusinessKey = null);

/// <summary>
/// لایهٔ دیتای یک ماژول — ساکن HrModules.X.Data.dll. کشف آن = «نصب» ماژول:
/// ثبت DbContext و قراردادهای بین‌ماژولی و سیدر + ساخت اسکیمای آن ماژول
/// + سید داده‌های اولیه/نمایشی + آبشار حذف شرکت.
/// ماژول غایب = هیچ‌کدام (صفر ریسک، صفر جدول).
/// </summary>
public interface IHrDataModule
{
    string Code { get; }
    string Title { get; }
    string Icon { get; }
    string Description { get; }

    /// <summary>ترتیب فاز سید نمایشی (فاز ۶۹) — ماژول‌های پایه زودتر: org=10 (پرسنل)،
    /// devices=20 (دستگاه‌ها)؛ ماژول‌های سیستم بعد از آن‌ها دادهٔ خودشان را می‌سازند.
    /// مرتب‌سازی: (SeedOrder, Code) — پیش‌فرض ۱۰۰.</summary>
    int SeedOrder => 100;

    /// <summary>فاز ۶۹ — انواع موجودیتِ «خانوادهٔ» این ماژول (مالکیت اسکیما). هر ماژول
    /// خودش اعلام می‌کند چه نوع‌هایی متعلق به اوست؛ هاست هیچ فهرست ایستایی از
    /// خانواده‌ها ندارد. از این داده دو کار می‌آید:
    ///   ۱) سپردن به حافظهٔ داده‌محور در بوت (HrModuleRuntime.RememberFamilies)؛
    ///   ۲) گارد Ignore ماژول غایب (HrModuleRuntime.IgnoreAbsentFamilies) — وقتی DLL
    ///   ماژول حذف شود، نوع‌هایش اگر از مسیر navigation ماژول دیگر به مدل مرکب کشیده
    ///   شوند، بر اساس همین دادهٔ به‌یادمانده از مدل حذف می‌شوند (صفر جدول فانتوم).</summary>
    Type[] FamilyTypes => [];

    /// <summary>فاز ۱۲۲ — اعلام خانواده‌های دادهٔ این ماژول برای «مرکز درون‌ریزی و
    /// برون‌ریزی» (عنوان/گروه/مجوز درون‌ریزی/کلید تجاری هر خانواده). هر ماژول خودش
    /// اعلام می‌کند؛ هاست فقط ادغام می‌کند. ماژول غایب/خاموش = هیچ ردیفی از او
    /// در هاب رندر نمی‌شود؛ خانوادهٔ اعلام‌نشده هم نه.</summary>
    HrDataExchangeDecl[] DataExchange => [];

    /// <summary>ثبت سرویس‌های دیتای ماژول (DbContext + قراردادهای Contracts + سیدر).
    /// useDb فقط در جریان ویزارد/تست اتصال پاس داده می‌شود.</summary>
    void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb);

    /// <summary>ترکیب اسکیمای ماژول در مدل EF هاست (AppDbContext مرکب) — فقط اگر حاضر.</summary>
    void ComposeModel(ModelBuilder modelBuilder);

    /// <summary>FKهای بین‌ماژولی — در گذر دوم (بعد از مدل همهٔ ماژول‌ها) اعمال می‌شود
    /// تا هر دو طرف رابطه در مدل باشند؛ ماژول غایب طرف رابطه = این قلاب خودش را رد می‌کند.</summary>
    void ComposeCrossModule(ModelBuilder modelBuilder) { }

    /// <summary>قلاب پیش از سید پرسنل نمونه — فرصت ساخت داده‌های مرجع (مثل شیفت‌ها)
    /// که هاست برای ساخت پرسنل نمونه به شناسه‌هایشان نیاز دارد. شناسه‌ها را در
    /// ctx.ShiftIds (قرارداد کلیدهای DemoShift) یا ctx.Bag بگذارد.</summary>
    Task PreSeedAsync(HrSeedContext ctx) => Task.CompletedTask;

    /// <summary>سید داده‌های اولیه/نمایشی ماژول — خودترمیم (idempotent)؛ بعد از ساخت پرسنل نمونه.</summary>
    Task SeedAsync(HrSeedContext ctx) => Task.CompletedTask;

    /// <summary>پاک‌کردن داده‌های نمایشی ماژول (دکمهٔ «ساخت مجدد دادهٔ نمونه»). خودترمیم: اجرای دوباره سید باید حالت سالم بسازد.</summary>
    Task ClearDemoAsync(DbContext db) => Task.CompletedTask;

    /// <summary>آبشار حذف شرکت — داده‌های ماژولِ متعلق به شرکت داده‌شده پاک شود (قرارداد همان رفتار فاز ۶۷).</summary>
    Task OnCompanyDeleteAsync(DbContext db, string companyId) => Task.CompletedTask;

    // ---------- فاز ۶۸ — شمارش‌های صفحهٔ شرکت‌ها (جایگزین coreهای #if CompaniesController) ----------

    /// <summary>شمارش گروه‌بندی‌شده به‌ازای شرکت — kind نوع شمارش است (مثل shifts/zones/visitors/runs/lots/vehicles).
    /// جایگزین ShiftCounts/ZoneCounts/VisitorCounts هاست.</summary>
    Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string kind)
        => Task.FromResult(new Dictionary<string, int>());

    /// <summary>شمارش برای «یک» شرکت — جایگزین XCountForAsync هاست.</summary>
    Task<int> CompanyCountAsync(DbContext db, string kind, string companyId) => Task.FromResult(0);

    /// <summary>شرکت‌های دارای پیکربندی ماژول (مثل PayrollSetting) — جایگزین PayrollCompanyIds هاست.</summary>
    Task<List<string>> CompaniesWithConfigAsync(DbContext db) => Task.FromResult(new List<string>());
}

/// <summary>
/// بخش UI یک ماژول — ساکن HrModules.X.dll. هر ماژول «خودش را معرفی می‌کند»:
/// مانیفست کنترلرها (گیت مسیرها)، مالکیت اکشن‌های چند-ماژولی (پرتال من و…)
/// و آیتم‌های منو. هاست هیچ فهرست ایستایی از ماژول‌ها ندارد.
/// </summary>
public interface IHrUiModule
{
    string Code { get; }
    string Title { get; }
    string Icon { get; }
    string Description { get; }

    /// <summary>فاز ۶۹ — ماژول «ذاتیِ» هاست؟ (ستون فقرات؛ همیشه فعال و غیرقابل خاموش/حذف).
    /// فقط هستهٔ پلتفرم (core) که کدش در خود هاست است true است؛ بقیهٔ ماژول‌ها —
    /// از جمله پایه‌های سابق org/devices — پلاگین‌های حذف‌شدنی‌اند (پیش‌فرض false).</summary>
    bool IsCore => false;

    /// <summary>مانیفست کنترلرهای این ماژول (نام کوتاه lowercase) — مرجع گیت مسیرها.</summary>
    string[] Controllers { get; }

    /// <summary>اکشن‌های (کنترلر، اکشن) متعلق به این ماژول که کنترلرشان در هاست است
    /// (پرتال من/گزارش‌ساز/خروجی) — مرجع گیت مسیرها.</summary>
    (string Ctrl, string Act)[] OwnedActions => Array.Empty<(string, string)>();

    /// <summary>فاز ۶۹ — گروه‌های منوی این ماژول با ترتیب اعلامی خودش — هاست فقط ادغام می‌کند.
    /// (از این فاز «فهرست ایستایی گروه‌های پایه» در هاست وجود ندارد.)</summary>
    HrNavGroup[] NavGroups => Array.Empty<HrNavGroup>();
}

// ======================================================================
// بستر سید ماژول‌ها — فاز ۶۸
// ======================================================================

/// <summary>شناسهٔ شیفت‌های نمونه (قرارداد کلیدهای دیتای نمایشی — ماژول حضور پر می‌کند،
/// هاست برای تخصیص شیفت به پرسنل نمونه می‌خواند؛ ماژول غایب = همه null).</summary>
public static class HrDemoShiftKeys
{
    public const string Admin = "admin";
    public const string Morning = "morning";
    public const string Evening = "evening";
    public const string Night = "night";
    public const string H12 = "h12";
    public const string Cycle = "cycle";
}

/// <summary>پرسنل نمونهٔ ساخته‌شده توسط هاست — ورودی سید ماژول‌ها (سازگار با tuple قدیمی DbSeeder).</summary>
public sealed record HrDemoEmployee(string Id, string DeptId, string? ShiftId, string Type, string PosCode);

/// <summary>بستر سید ماژول‌ها — دادهٔ پایهٔ ساخته‌شده توسط هاست + ابزارهای مشترک.</summary>
public sealed class HrSeedContext
{
    private static readonly IServiceProvider EmptyServices =
        new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();

    public HrSeedContext(IServiceProvider? services, DbContext db, bool demoData,
        IReadOnlyList<HrDemoEmployee>? employees, IReadOnlyList<string>? deviceIds, Func<double> rand,
        string demoCompanyId = "")
    {
        Services = services ?? EmptyServices; Db = db; DemoData = demoData;
        Employees = employees ?? Array.Empty<HrDemoEmployee>();
        DeviceIds = deviceIds ?? Array.Empty<string>();
        Rand = rand;
        DemoCompanyId = demoCompanyId;
    }

    /// <summary>Provider اسکوپ جاری — DbContextهای ماژول/قراردادها از همین گرفته می‌شود.</summary>
    public IServiceProvider Services { get; }

    // فاز ۶۹ — پرسنل/دستگاه‌ها دیگر هاست نمی‌سازد؛ ماژول‌های org/devices در فاز Seed خودشان
    // می‌سازند و همین‌جا می‌گذارند تا ماژول‌های بعدی (تردد/کنترل تردد/پارکینگ/…) مصرف کنند.
    // settable — ترتیب: org (پرسنل) ← devices (دستگاه‌ها) ← ماژول‌های سیستم.
    /// <summary>کانتکست مرکب هاست (AppDbContext) — ماژول با Set&lt;T&gt;() به همهٔ جدول‌های حاضر می‌رسد.</summary>
    public DbContext Db { get; }
    /// <summary>پرچم «دادهٔ نمایشی» (SeedDemo) — بخش‌های نمایشی به آن احترام بگذارند.</summary>
    public bool DemoData { get; }
    /// <summary>پرسنل نمونه (Id, DeptId, ShiftId, Type, PosCode) — ShiftId از PreSeed ماژول حضور.</summary>
    public IReadOnlyList<HrDemoEmployee> Employees { get; set; } = Array.Empty<HrDemoEmployee>();
    /// <summary>نام کامل پرسنل نمونه (Id ← نام) — برای پیام‌های هشدار سید ماژول‌ها.</summary>
    public IReadOnlyDictionary<string, string> EmployeeNames { get; set; } = new Dictionary<string, string>();
    /// <summary>شناسهٔ دستگاه‌های نمونه (همهٔ ۶ دستگاه — ماژول‌ها زیرمجموعهٔ خودشان را برمی‌دارند).</summary>
    public IReadOnlyList<string> DeviceIds { get; set; } = Array.Empty<string>();
    /// <summary>شناسهٔ شرکت نمونه (پارس آسیا) — ساخته‌شده پیش از PreSeed؛ مالکیت داده‌های نمایشی ماژول‌ها.</summary>
    public string DemoCompanyId { get; }
    /// <summary>مولد شبه‌تصادفی مشترک (mulberry32 با seed ثابت هاست) — بازتولیدپذیری دیتای نمایشی.</summary>
    public Func<double> Rand { get; }

    /// <summary>شناسهٔ شیفت‌های نمونه — کلیدها: HrDemoShiftKeys (PreSeed ماژول حضور پر می‌کند).</summary>
    public Dictionary<string, string?> ShiftIds { get; } = new();

    /// <summary>حافظهٔ جانبی بین PreSeed/Seed و ماژول‌ها.</summary>
    public Dictionary<string, object?> Bag { get; } = new();

    // ---------- ابزارهای مشترک (جایگزین helperهای خصوصی DbSeeder — بازهٔ بسته [a,b] مثل هاست) ----------
    /// <summary>عدد صحیح تصادفی در بازهٔ بستهٔ [a,b] — دقیقاً همان معنای DbSeeder.Rint.</summary>
    public int Rint(int a, int b) => a + (int)(Rand() * (b - a + 1));
    /// <summary>انتخاب تصادفی یک عضو — دقیقاً همان معنای DbSeeder.Pick.</summary>
    public T Pick<T>(IReadOnlyList<T> list) => list[(int)(Rand() * list.Count)];
}

// ======================================================================
// DTOهای بین‌ماژولی داشبورد/گزارش — فاز ۶۸ (جایگزین coreهای #if هاست)
// ======================================================================

/// <summary>KPI تردد داشبورد — مصرف HomeController هاست (نگاشت به record نمایشی هاست).</summary>
public sealed record HrAttDashDto(int Present, int Late, int Absent, int OnLeave, int PendingRequests,
    IReadOnlyList<HrDashPoint> Week);

/// <summary>یک نقطهٔ نمودار هفتهٔ داشبورد — برچسب نمایشی در هاست از DateKey ساخته می‌شود (شکل JSON ویو حفظ می‌ماند).</summary>
public sealed record HrDashPoint(string DateKey, int Present, int Absent, int Late, int Overtime);

/// <summary>رویداد اخیر کنترل تردد برای داشبورد — فاز ۷۵: تایپ‌دار (بایندینگ dynamic ویو هاست
/// روی نوع‌های بی‌نام internal ماژول کرش می‌کرد).</summary>
public sealed record HrAccessRecentEvent(DateTime Timestamp, string Direction, string Method, string Result,
    string? Reason, string Gate, string? EmployeeId, string? VisitorId);

/// <summary>KPI کنترل تردد داشبورد.</summary>
public sealed record HrAccessDashDto(int TodayEvents,
    IReadOnlyList<HrZoneCount> ZoneDist, IReadOnlyList<HrAccessRecentEvent> RecentEvents, IReadOnlyList<string?> RecentVisitorIds);

/// <summary>شمارش رویداد به‌ازای منطقه.</summary>
public sealed record HrZoneCount(string Zone, int Count);

/// <summary>نتیجهٔ جست‌وجوی سریع خودرو (داشبورد پارکینگ).</summary>
public sealed record HrVehicleHitDto(string PlateNo, string? Brand, string Text);

/// <summary>فاز ۷۵ — گزینهٔ پارکینگ برای dropdown گزارش‌ساز هاست.
/// تا فاز ۷۴ شکل anonymous درون‌اسمبلیِ ماژول برگردانده می‌شد و بایندینگ dynamic ویو هاست
/// (بدون دسترسی به نوع‌های internal ماژول) روی /Reports/Parking کرش می‌کرد.</summary>
public sealed record HrLotOption(string Id, string Name, string Code, int Gates, string? Company);

/// <summary>ردیف خروجی اکسل تردد — مصرف ExportController هاست (ClosedXML در هاست می‌ماند).</summary>
public sealed record HrAttExportRow(string PersonnelCode, string FullName, string Date,
    DateTime? CheckIn, DateTime? CheckOut, int WorkMinutes, int LateMinutes, int EarlyMinutes,
    int OvertimeMinutes, string Status);

/// <summary>ردیف ورود تردد از اکسل — مصرف ExcelImport هاست (پارس فایل در هاست، commit در ماژول).</summary>
public sealed record HrAttImportRow(string EmployeeId, string Date, DateTime? CheckIn, DateTime? CheckOut,
    int WorkMinutes, int LateMinutes, int EarlyMinutes, int OvertimeMinutes, string Status);
