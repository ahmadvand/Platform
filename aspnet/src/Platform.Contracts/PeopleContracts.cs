// ======================================================================
// فاز ۶۹ — قراردادهای «منابع انسانی» و «تجهیزات» (پایه‌های قابل‌حذف)
// ----------------------------------------------------------------------
// تا فاز ۶۸ ماژول‌های org/devices «پایهٔ توکار» بودند: کدشان در هاست،
// مانیفستشان در ModuleCatalog و جدول‌هایشان همیشه در اسکیما. از این فاز
// آن‌ها هم پلاگین‌اند (HrModules.Org / HrModules.Devices) و فقط «هستهٔ
// پلتفرم» (core) ضروری است — بقیهٔ ماژول‌ها با نبودن DLL حذف می‌شوند.
//
// هاست (کنترلرهای core مثل Users/Home/Auth/Companies/Self/Export/Reports)
// دیگر هیچ کوئری مستقیمی به پرسنل/دستگاه نمی‌زند؛ هرچه لازم دارد از همین
// قراردادها می‌گیرد. پیاده‌سازی در لایهٔ دیتای خود ماژول (HrModules.*.Data)
// و «جایگزین بی‌خطر» در هاست (HrModuleFallbacks — مجموعهٔ خالی/null).
// DTOها POCO خالص‌اند — بدون EF و بدون هیچ نوع ماژولی.
// ======================================================================
namespace Platform.Contracts;

// ============================ منابع انسانی ============================

/// <summary>خلاصهٔ پرسنل برای نمایش/انتخاب — شکل سبک برای dropdown و اتصال کاربر.</summary>
public sealed record HrPersonOption(string Id, string PersonnelCode, string FullName, string CompanyId);

/// <summary>واحد سازمانی برای dropdown گزارش‌ساز (فاز ۶۹ — مالکش ماژول org است).</summary>
public sealed record HrDeptOption(string Id, string Name);

/// <summary>پرسنل برای نمایش در صفحات دیگر (کاربران، کارت‌ها، مجوزها…).</summary>
public sealed record HrPersonBrief(string Id, string PersonnelCode, string FullName, string CompanyId);

/// <summary>ردیف خروجی اکسل پرسنل — ساخت فایل در هاست می‌ماند (ClosedXML ∈ هسته).</summary>
public sealed record HrPersonExportRow(string PersonnelCode, string FirstName, string LastName,
    string NationalId, string Department, string Position, string EmploymentType, string Shift,
    decimal BaseSalary, decimal LeaveRemaining, string Status, string HireDate);

/// <summary>پروفایل «من» در پرتال — همان فیلدهایی که ویو خانهٔ پرتال می‌خواند.</summary>
public sealed record HrSelfProfile(string Id, string PersonnelCode, string FullName,
    string Department, string Position, string EmploymentType,
    decimal LeaveRemaining, decimal SickLeaveRemaining);

/// <summary>نتیجهٔ ورود پرتال پرسنل (کد پرسنلی + کد ملی) — null = اطلاعات نادرست.</summary>
public sealed record HrPersonAuth(string Id, string PersonnelCode, string FullName, string CompanyId);

/// <summary>ردیف قرارداد در حال انقضا — مصرف موتور اعلان‌ها (پلتفرم).</summary>
public sealed record HrContractExpiryRow(string FirstName, string LastName, string PersonnelCode,
    DateTime EndDate, string ContractNo);

/// <summary>
/// دایرکتوری پرسنل — پیاده‌سازی در HrModules.Org.Data؛ جایگزین همهٔ دسترسی‌های
/// مستقیم هاست/پلتفرم به DbSet پرسنل. ماژول غایب = جایگزین Null (خالی/null).
/// </summary>
public interface IPeopleDirectory
{
    /// <summary>پرسنل فعال برای dropdown گزارش‌ساز/جست‌وجو.</summary>
    Task<List<HrPersonOption>> ActiveOptionsAsync();

    /// <summary>واحدهای سازمانی برای dropdown گزارش‌ساز — org غایب = خالی.</summary>
    Task<List<HrDeptOption>> DepartmentsAsync();

    /// <summary>پرسنل بدون حساب کاربری (فرم ایجاد/اتصال کاربر).</summary>
    Task<List<HrPersonOption>> UnlinkedAsync();

    /// <summary>یافتن پرسنل با شناسه — null = یافت نشد.</summary>
    Task<HrPersonBrief?> FindAsync(string employeeId);

    /// <summary>نام/کد پرسنل به‌ازای شناسه‌ها — جایگزین Include(Employee) در ماژول‌های سیستم.</summary>
    Task<Dictionary<string, HrPersonBrief>> BriefsAsync(IReadOnlyCollection<string> employeeIds);

    /// <summary>شناسهٔ پرسنل با نام — برای اتصال کاربران پیش‌فرض سیدر (null = یافت نشد).</summary>
    Task<string?> FindIdByNameAsync(string firstName, string lastName);

    /// <summary>رديف‌های خروجی اکسل پرسنل (شیفت با نام).</summary>
    Task<List<HrPersonExportRow>> ExportRowsAsync();

    /// <summary>پروفایل «من» — کاربر emp:کد یا کاربر متصل؛ null = بدون اتصال پرسنل.</summary>
    Task<HrSelfProfile?> SelfProfileAsync(string? personnelCode, string? linkedEmployeeId);

    /// <summary>احراز پرتال پرسنل (کد پرسنلی + کد ملی، فقط فعال) — null = نادرست.</summary>
    Task<HrPersonAuth?> AuthenticateAsync(string personnelCode, string nationalId);

    /// <summary>جست‌وجوی سریع پرسنل (نوار بالایی).</summary>
    Task<List<HrSearchHit>> SearchAsync(string q, int limit);

    /// <summary>تعداد پرسنل فعال (داشبورد).</summary>
    Task<int> ActiveCountAsync();

    /// <summary>توزیع نوع اشتغال پرسنل فعال (داشبورد).</summary>
    Task<Dictionary<string, int>> EmploymentTypeCountsAsync();

    /// <summary>شمارش پرسنل به‌ازای شرکت (صفحهٔ شرکت‌ها).</summary>
    Task<Dictionary<string, int>> CountsByCompanyAsync();

    /// <summary>شمارش پرسنل یک شرکت (گارد حذف شرکت).</summary>
    Task<int> CountForCompanyAsync(string companyId);

    /// <summary>قراردادهای در حال انقضا تا N روز — مصرف موتور اعلان‌ها.</summary>
    Task<List<HrContractExpiryRow>> ContractExpiryAsync(int days);
}

// ============================ تجهیزات و گیت‌وی ============================

/// <summary>یک دستگاه برای نوار داشبورد — همهٔ فیلدهای نمایشی از پیش محاسبه‌شده.</summary>
public sealed record HrDeviceStripItem(string Id, string Name, string Kind, string Brand,
    string? Ip, string? Location, string? CompanyName, bool Online,
    int TodayPunches, string? LastPunchPerson, string? LastPunchTime);

/// <summary>KPI تجهیزات داشبورد.</summary>
public sealed record HrDevicesDashDto(int Total, int Online, IReadOnlyList<HrDeviceStripItem> Devices);

/// <summary>وضعیت دستگاه برای اعلان آفلاین — مصرف موتور اعلان‌ها.</summary>
public sealed record HrDeviceStatusRow(string Name, string? Ip, DateTime? LastSeenAt);

/// <summary>
/// دروازهٔ تجهیزات — پیاده‌سازی در HrModules.Devices.Data؛ جایگزین دسترسی مستقیم
/// هاست به DbSet دستگاه‌ها/لاگ تردد خام. ماژول غایب = جایگزین Null.
/// (کانال دروازهٔ زنده = IDeviceGatewayContract جدا در GatewayContracts.)
/// </summary>
public interface IDevicesGateway
{
    /// <summary>دادهٔ نوار دستگاه‌های داشبورد + شمار آنلاین‌ها (sweep آفلاین هم اینجاست).</summary>
    Task<HrDevicesDashDto> DashboardAsync();

    /// <summary>جست‌وجوی سریع دستگاه (نوار بالایی).</summary>
    Task<List<HrSearchHit>> SearchAsync(string q, int limit);

    /// <summary>شمارش دستگاه به‌ازای شرکت (صفحهٔ شرکت‌ها).</summary>
    Task<Dictionary<string, int>> CountsByCompanyAsync();

    /// <summary>شمارش دستگاه یک شرکت (گارد حذف شرکت).</summary>
    Task<int> CountForCompanyAsync(string companyId);

    /// <summary>دستگاه‌های آنلاینِ بدون ارتباط بیش از N دقیقه — مصرف موتور اعلان‌ها.</summary>
    Task<List<HrDeviceStatusRow>> OfflineDevicesAsync(int minutes);
}

/// <summary>نتیجهٔ جست‌وجوی سراسری نوار بالایی (پرسنل/دستگاه/خودرو/…).</summary>
public sealed record HrSearchHit(string Title, string Href);
