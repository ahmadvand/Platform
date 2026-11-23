// ======================================================================
// فاز ۶۱ — قراردادهای بین‌ماژولی (Platform.Contracts)
// ----------------------------------------------------------------------
// قانون معماری: هیچ ماژولی ارجاع مستقیم به پروژهٔ ماژول دیگر ندارد.
// هر داده/کاری که یک ماژول از ماژول دیگر می‌خواهد فقط از طریق این
// اینترفیس‌ها (پیاده‌سازی در لایهٔ دیتای همان ماژول — HrModules.*.Data)
// و DTOهای همین پروژه رد و بدل می‌شود. DTOها POCO خالص‌اند — بدون EF.
// ======================================================================
namespace Platform.Contracts;

using Microsoft.EntityFrameworkCore;

// ============================ حضور و غیاب ============================

/// <summary>ردیف رکورد تردد — قراردادِ مصرف حقوق/گزارش/اعلان.</summary>
public sealed record AttWorkRecord(
    string EmployeeId, string Date, DateTime? CheckIn, DateTime? CheckOut,
    int WorkMinutes, int LateMinutes, int EarlyMinutes, int OvertimeMinutes, string Status);

/// <summary>ردیف تأییدشده (مرخصی/مأموریت/مجوز اضافه‌کاری) — فیلدهای مشترک مصرف‌کننده‌ها.</summary>
public sealed record AttApproval(
    string EmployeeId, string Type, string Status,
    string FromDate, string ToDate, string? Date, string FromTime, string ToTime,
    int? Hours = null, decimal Days = 0);

/// <summary>اطلاعات شیفت برای محاسبه/نمایش — جایگزین navigation پایه→ماژول.
/// فاز ۶۲ — CompanyId اختیاری برای مصرف‌کنندگانی که شیفت را به‌ازای شرکت لازم دارند
/// (سیدِ اقلام شیفت‌محور حقوق)؛ پارامتر اختیاری است تا سازنده‌های موجود نشکنند.</summary>
public sealed record AttShiftInfo(
    string Id, string Name, string Type, string StartTime, string EndTime,
    int BreakMinutes, string? CyclePattern, int ToleranceLate, string? CompanyId = null);

/// <summary>بستهٔ دادهٔ کارکرد یک بازه برای موتور حقوق.</summary>
public sealed record AttWorkData(
    List<AttWorkRecord> Records,
    List<AttApproval> Leaves,
    List<AttApproval> Missions,
    List<AttApproval> Permits,
    HashSet<string> Holidays,
    Dictionary<string, AttShiftInfo> ShiftByEmployee);

/// <summary>نتیجه ثبت/به‌روزرسانی پانچ — همان خروجی AttEngine برای مصرف DeviceOps.</summary>
public sealed record PunchUpsertResult(
    DateTime CheckIn, DateTime CheckOut, bool IsFirstIn, bool IsLastOut,
    int WorkMinutes, int LateMinutes, int EarlyMinutes, int OvertimeMinutes);

/// <summary>ردیف اعلان تأخیر/غیبت امروز.</summary>
public sealed record AttPersonRow(string PersonnelCode, string FullName, int LateMinutes);

/// <summary>شمارنده درخواست‌های در انتظار تأیید.</summary>
public sealed record AttPendingCounts(int Leaves, int Missions, int Corrections);

// ---------- گزارش تردد (سطح ماژول → سرویس گزارش هاست) ----------
public sealed record AttMonthPoint(string Label, int Present, int Absent, int Leave, int LateMinutes, int OvertimeMinutes);
public sealed record AttDeptKpi(string Dept, int Headcount, int Present, int Absent, int LateMinutes, int OvertimeMinutes, double AttendanceRate);
public sealed record AttCustomRow(string PersonnelCode, string FullName, string Dept, string Date, string Status,
    DateTime? CheckIn, DateTime? CheckOut, int WorkMinutes, int LateMinutes, int OvertimeMinutes);
public sealed record AttEmpSummary(string PersonnelCode, string FullName, string Dept, int Present, int Absent, int LeaveDays, int LateMinutes, int OvertimeMinutes, int WorkMinutes);

/// <summary>
/// دروازهٔ ماژول حضور — پیاده‌سازی در HrModules.Attendance.Data.
/// مصرف‌کنندگان: موتور حقوق، DeviceOps، موتور اعلان‌ها، گزارش‌ساز، ورود اکسل.
/// </summary>
public interface IAttendanceGateway
{
    /// <summary>دادهٔ کارکرد بازه برای فهرست پرسنل (رکوردها + تأییدشده‌ها + تعطیلات + شیفت هر نفر).</summary>
    Task<AttWorkData> FetchWorkDataAsync(IReadOnlyCollection<string> employeeIds, string fromDateKey, string toDateKeyExclusive);

    /// <summary>ثبت/به‌روزرسانی رکورد روزانه پانچ با محاسبهٔ AttEngine (اولین ورود / آخرین خروج).</summary>
    Task<PunchUpsertResult> UpsertPunchAsync(string employeeId, string? deviceId, string dateKey, DateTime ts);

    /// <summary>شیفتِ فعال هر پرسنل (به‌ازای EmployeeId) — برای نمایش و محاسبه در بیرون از ماژول.</summary>
    Task<Dictionary<string, AttShiftInfo>> ShiftsByEmployeeAsync(IReadOnlyCollection<string> employeeIds);

    /// <summary>فهرست شیفت‌های فعال (برای فیلترهای UI هاست).</summary>
    Task<List<AttShiftInfo>> ActiveShiftsAsync();

    /// <summary>فهرست همهٔ شیفت‌ها (حتی غیرفعال — برای سازگاری کامل dropdownهای قبلی).</summary>
    Task<List<AttShiftInfo>> AllShiftsAsync();

    // ---------- فاز ۶۸ — قلاب‌های جایگزین coreهای #if هاست ----------

    /// <summary>KPI تردد داشبورد (جایگزین AttDashCore هاست) — بدون ماژول، هاست از صفرها استفاده می‌کند.</summary>
    Task<HrAttDashDto> DashboardAsync(string todayKey);

    /// <summary>ردیف‌های خروجی اکسل یک روز (جایگزین ExportCore هاست) — فایل‌سازی در هاست می‌ماند.</summary>
    Task<List<HrAttExportRow>> ExportDayAsync(string dateKey);

    /// <summary>نام شیفت‌ها به‌ازای شناسه (جایگزین ShiftNamesAsync هاست).</summary>
    Task<Dictionary<string, string>> ShiftNamesAsync();

    /// <summary>ثبت ردیف‌های ورود اکسل تردد (جایگزین CommitAttendanceCore هاست) — پارس فایل در هاست است.</summary>
    Task<(int Inserted, int Skipped)> ImportRowsAsync(DbContext db, IReadOnlyList<HrAttImportRow> rows);

    /// <summary>KPI خانهٔ پرتال من (جایگزین AttHomeCore هاست) — کارکرد ماه پرسنل + رکوردهای اخیر.</summary>
    Task<HrSelfHomeDto> SelfHomeAsync(DbContext db, string employeeId, string fromKey, string toKey);

    /// <summary>فاز ۱۲۶ — درخواست‌های اخیر پرسنل برای خانهٔ پرتال من (مرخصی + مأموریت + اصلاح تردد؛ فقط نمایش، تأیید در صفحهٔ Requests).</summary>
    Task<HrSelfRequestsDto> SelfRequestsAsync(DbContext db, string employeeId);
}

/// <summary>KPI خانهٔ پرتال من — رکوردهای اخیر همان entityهای حضورند (ویو مثل قبل می‌خواند).</summary>
public sealed record HrSelfHomeDto(int Present, int Late, int Absent, int LeaveDays,
    int WorkMinutes, int Overtime, int Pending, IReadOnlyList<object> Recent);

/// <summary>فاز ۱۲۶ — سطر درخواست اخیر پرتال من؛ کلیدهای تاریخ خام هستند و فرمت فارسی در ویو انجام می‌شود.</summary>
public sealed record HrSelfRequestRow(string Kind, string Type, string FromKey, string ToKey, string Detail, string Status, string Approver);

/// <summary>فاز ۱۲۶ — درخواست‌های اخیر خانهٔ پرتال من (تازه‌ترین‌ها، ادغام سه نوع درخواست).</summary>
public sealed record HrSelfRequestsDto(IReadOnlyList<HrSelfRequestRow> Rows);

/// <summary>بینش‌های اعلانی حضور — مصرف موتور اعلان‌ها (پلتفرم).</summary>
public interface IAttendanceInsights
{
    Task<List<AttPersonRow>> LateTodayAsync(string todayKey, int thresholdMinutes);
    Task<List<AttPersonRow>> AbsentTodayAsync(string todayKey);
    Task<AttPendingCounts> PendingCountsAsync();
}

/// <summary>منبع گزارش تردد — مصرف ReportService هاست (همان شکل خروجی قبلی).
/// فاز ۷۴ — پارامترهای اختیاری دامنهٔ رکوردی (Row-Level): همگی null = بدون محدودیت؛
/// فیلترها SQL-ساید روی ستون‌های ایندکس‌دار اعمال می‌شوند (سرعت گزارش‌ها حفظ می‌شود).
/// گارد fail-closed در سمت هاست است: دامنهٔ تهی = خروجی تهی (بدون فراخوانی منبع).</summary>
public interface IAttendanceReportSource
{
    Task<List<AttMonthPoint>> MonthlyTrendAsync(int months, IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null);
    Task<List<AttDeptKpi>> DeptKpisAsync(string? fromKey, string? toKey, IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null);
    Task<List<AttCustomRow>> CustomQueryAsync(string? deptId, string? employeeId, string? fromKey, string? toKey, string? status, int limit, IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null);
    Task<List<AttEmpSummary>> EmployeeSummaryAsync(string? deptId, string? fromKey, string? toKey, IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null);
}

// ============================ بازدیدکننده ============================

/// <summary>خلاصهٔ بازدیدکننده برای نمایش در ماژول‌های دیگر (کنترل تردد).</summary>
public sealed record VisitorBriefDto(string Id, string FullName, string? NationalId, string? CompanyName, string? Phone);

/// <summary>دروازهٔ ماژول بازدیدکننده — پیاده‌سازی در Platform.Modules.Visitors.Data.</summary>
public interface IVisitorsGateway
{
    /// <summary>نام کامل به‌ازای شناسهٔ بازدیدکننده — برای ردیف‌های رویداد/مجوز تردد.</summary>
    Task<Dictionary<string, string>> FullNamesAsync(IReadOnlyCollection<string> visitorIds);

    /// <summary>جست‌وجوی بازدیدکننده برای فرم‌های انتخاب (محدود).</summary>
    Task<List<VisitorBriefDto>> SearchAsync(string? query, int limit = 20);

    /// <summary>تعداد بازدیدکنندگان «داخل سازمان» (داشبورد هاست).</summary>
    Task<int> InsideCountAsync();
}

// ============================ کنترل تردد ============================

/// <summary>خلاصهٔ گیت برای نمایش در ماژول‌های دیگر.</summary>
public sealed record GateBriefDto(string Id, string Name, string? ZoneName, string? LotName);

/// <summary>دروازهٔ ماژول کنترل تردد — پیاده‌سازی در HrModules.Access.Data.</summary>
public interface IAccessGateway
{
    /// <summary>گیت‌های فعال (با نام منطقه) — برای اتصال دوربین/انتخاب در UI ماژول‌های دیگر.</summary>
    Task<List<GateBriefDto>> GatesAsync(string? companyId = null);

    // ---------- خدمات مجوز بازدیدکننده (مصرف ماژول ارباب‌رجوع — فاز ۶۱) ----------

    /// <summary>شناسهٔ منطقه با کد داده‌شده (مثل Z-MAIN) — null = یافت نشد.</summary>
    Task<string?> FindZoneIdByCodeAsync(string code);

    /// <summary>آیا برای این بازدیدکننده مجوز فعالِ این منطقه هست؟</summary>
    Task<bool> HasVisitorPermissionAsync(string visitorId, string zoneId);

    /// <summary>صدور مجوز موقت منطقه برای بازدیدکننده (ValidFrom=اکنون، ValidUntil از کارت بازدیدکننده).</summary>
    Task GrantVisitorPermissionAsync(string visitorId, string zoneId, string type, DateTime? validUntil);

    /// <summary>انقضای همهٔ مجوزهای بازدیدکننده (ثبت خروج) — خروجی: تعداد منقضی‌شده.</summary>
    Task<int> ExpireVisitorPermissionsAsync(string visitorId);

    // ---------- فاز ۶۸ — قلاب‌های جایگزین coreهای #if هاست ----------

    /// <summary>شمارش گیت‌های هر دستگاه (جایگزین GateCountsCore هاست).</summary>
    Task<Dictionary<string, int>> GateCountsByDeviceAsync();

    /// <summary>KPI کنترل تردد داشبورد (جایگزین AccessDashCore هاست).</summary>
    Task<HrAccessDashDto> DashboardAsync();
}

// ============================ حسابرسی (پلتفرم) ============================

/// <summary>دریافت‌کنندهٔ رکورد حسابرسی — پیاده‌سازی در هاست (composite context).
/// ماژول‌ها حسابرسی را از این مسیر می‌نویسند، نه مستقیم از DbContext هاست.</summary>
public interface IAuditSink
{
    Task Audit(SessionUserDto? actor, string action, string entity, string? entityId, object? detail, string? ip);
}

/// <summary>نمای خلاصهٔ نشست کاربر برای قراردادها (بدون وابستگی به هاست).</summary>
public sealed record SessionUserDto(string Id, string FullName);

// ============================ پارکینگ ============================

/// <summary>ردیف گزارش ورود/خروج خودرو — همان شکل قبلی ReportService.ParkingRow.</summary>
public sealed record AttParkingRow(DateTime Time, string Plate, string Owner, string VehicleType, string Company,
    string Lot, string Gate, string Direction, string Method, string? Camera, int? Confidence,
    string Result, string? Reason);

/// <summary>منبع گزارش پارکینگ — پیاده‌سازی در Platform.Modules.Parking.Data؛ مصرف ReportService هاست.</summary>
public interface IParkingReportSource
{
    Task<List<AttParkingRow>> ParkingEventsAsync(string? fromKey, string? toKey, string? co,
        string? lotId, string? direction, string? result, string? method, int limit = 1000,
        IReadOnlyCollection<string>? companies = null,
        IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null);

    // ---------- فاز ۶۸ — قلاب‌های جایگزین coreهای #if هاست ----------

    /// <summary>فهرست پارکینگ‌ها برای dropdown گزارش‌ساز/داشبورد (جایگزین LotsCore هاست) — فاز ۷۵: تایپ‌دار.</summary>
    Task<List<HrLotOption>> LotsAsync();

    /// <summary>دوربین‌های ANPR فعال برای تنظیمات گیت‌وی (جایگزین CamerasCore هاست) — شکل anonymous JSON قبلی.</summary>
    Task<List<object>> CamerasAsync();

    /// <summary>جست‌وجوی سریع خودرو (داشبورد هاست).</summary>
    Task<List<HrVehicleHitDto>> VehicleSearchAsync(string q);
}
