// ======================================================================
// فاز ۶۷ — پیاده‌سازی‌های «جایگزینِ بی‌اثر» قراردادهای بین‌ماژولی
// ----------------------------------------------------------------------
// وقتی لایهٔ دیتای یک ماژول در بسته/اجرا حاضر نیست (بیلد مشتری با
// -p:HrModuleX=false یا حذف دستی DLL از بیلد کامل)، مصرف‌کننده‌های
// پلتفرمی/بین‌ماژولی (موتور اعلان‌ها، ReportService، DeviceOps، داشبورد،
// کنترلرهای ماژول‌های دیگر که قرارداد تزریق می‌کنند) نباید کرش کنند.
// این کلاس‌ها فقط با Contracts کامپایل می‌شوند (هیچ نوع ماژولی ندارند) و
// در هر بیلدی حاضرند؛ فقط برای «ماژول غایب» ثبت می‌شوند.
//
// معناشناسی: خواندن‌ها = مجموعهٔ خالی / عدد صفر؛ نوشتن‌های ماژول‌محور =
// بی‌عمل (ماژول غایب جدول ندارد و مسیرهای UI آن هم با ModuleGate بسته است).
// ======================================================================
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Modules;

public sealed class NullAttendanceGateway : IAttendanceGateway, IAttendanceInsights, IAttendanceReportSource
{
    public Task<AttWorkData> FetchWorkDataAsync(IReadOnlyCollection<string> employeeIds, string fromDateKey, string toDateKeyExclusive)
        => Task.FromResult(new AttWorkData([], [], [], [], [], new Dictionary<string, AttShiftInfo>()));
    public Task<PunchUpsertResult> UpsertPunchAsync(string employeeId, string? deviceId, string dateKey, DateTime ts)
        => Task.FromResult(new PunchUpsertResult(ts, ts, true, true, 0, 0, 0, 0));
    public Task<Dictionary<string, AttShiftInfo>> ShiftsByEmployeeAsync(IReadOnlyCollection<string> employeeIds)
        => Task.FromResult(new Dictionary<string, AttShiftInfo>());
    public Task<List<AttShiftInfo>> ActiveShiftsAsync() => Task.FromResult(new List<AttShiftInfo>());
    public Task<List<AttShiftInfo>> AllShiftsAsync() => Task.FromResult(new List<AttShiftInfo>());
    public Task<List<AttPersonRow>> LateTodayAsync(string todayKey, int thresholdMinutes) => Task.FromResult(new List<AttPersonRow>());
    public Task<List<AttPersonRow>> AbsentTodayAsync(string todayKey) => Task.FromResult(new List<AttPersonRow>());
    public Task<AttPendingCounts> PendingCountsAsync() => Task.FromResult(new AttPendingCounts(0, 0, 0));
    public Task<List<AttMonthPoint>> MonthlyTrendAsync(int months,
        IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null) => Task.FromResult(new List<AttMonthPoint>());
    public Task<List<AttDeptKpi>> DeptKpisAsync(string? fromKey, string? toKey,
        IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null) => Task.FromResult(new List<AttDeptKpi>());
    public Task<List<AttCustomRow>> CustomQueryAsync(string? deptId, string? employeeId, string? fromKey, string? toKey, string? status, int limit,
        IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null)
        => Task.FromResult(new List<AttCustomRow>());
    public Task<List<AttEmpSummary>> EmployeeSummaryAsync(string? deptId, string? fromKey, string? toKey,
        IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null)
        => Task.FromResult(new List<AttEmpSummary>());

    // ---------- فاز ۶۸ — قلاب‌های جدید (ماژول غایب = خالی/بی‌عمل) ----------
    public Task<HrAttDashDto> DashboardAsync(string todayKey)
        => Task.FromResult(new HrAttDashDto(0, 0, 0, 0, 0, new List<HrDashPoint>()));
    public Task<List<HrAttExportRow>> ExportDayAsync(string dateKey) => Task.FromResult(new List<HrAttExportRow>());
    public Task<Dictionary<string, string>> ShiftNamesAsync() => Task.FromResult(new Dictionary<string, string>());
    public Task<(int Inserted, int Skipped)> ImportRowsAsync(DbContext db, IReadOnlyList<HrAttImportRow> rows)
        => Task.FromResult((0, 0));
    public Task<HrSelfHomeDto> SelfHomeAsync(DbContext db, string employeeId, string fromKey, string toKey)
        => Task.FromResult(new HrSelfHomeDto(0, 0, 0, 0, 0, 0, 0, new List<object>()));
    // فاز ۱۲۶ — ماژول حضور غایب = جدول درخواست‌های اخیر خالی (بدون خطا)
    public Task<HrSelfRequestsDto> SelfRequestsAsync(DbContext db, string employeeId)
        => Task.FromResult(new HrSelfRequestsDto(new List<HrSelfRequestRow>()));
}

public sealed class NullVisitorsGateway : IVisitorsGateway
{
    public Task<Dictionary<string, string>> FullNamesAsync(IReadOnlyCollection<string> visitorIds)
        => Task.FromResult(new Dictionary<string, string>());
    public Task<List<VisitorBriefDto>> SearchAsync(string? query, int limit = 20)
        => Task.FromResult(new List<VisitorBriefDto>());
    public Task<int> InsideCountAsync() => Task.FromResult(0);
}

public sealed class NullAccessGateway : IAccessGateway
{
    public Task<List<GateBriefDto>> GatesAsync(string? companyId = null) => Task.FromResult(new List<GateBriefDto>());
    public Task<string?> FindZoneIdByCodeAsync(string code) => Task.FromResult<string?>(null);
    public Task<bool> HasVisitorPermissionAsync(string visitorId, string zoneId) => Task.FromResult(false);
    public Task GrantVisitorPermissionAsync(string visitorId, string zoneId, string type, DateTime? validUntil) => Task.CompletedTask;
    public Task<int> ExpireVisitorPermissionsAsync(string visitorId) => Task.FromResult(0);
    public Task<Dictionary<string, int>> GateCountsByDeviceAsync() => Task.FromResult(new Dictionary<string, int>());
    public Task<HrAccessDashDto> DashboardAsync()
        => Task.FromResult(new HrAccessDashDto(0, new List<HrZoneCount>(), new List<HrAccessRecentEvent>(), new List<string?>()));
}

public sealed class NullParkingReportSource : IParkingReportSource
{
    public Task<List<AttParkingRow>> ParkingEventsAsync(string? fromKey, string? toKey, string? co,
        string? lotId, string? direction, string? result, string? method, int limit = 1000,
        IReadOnlyCollection<string>? companies = null,
        IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null)
        => Task.FromResult(new List<AttParkingRow>());
    public Task<List<HrLotOption>> LotsAsync() => Task.FromResult(new List<HrLotOption>());
    public Task<List<object>> CamerasAsync() => Task.FromResult(new List<object>());
    public Task<List<HrVehicleHitDto>> VehicleSearchAsync(string q) => Task.FromResult(new List<HrVehicleHitDto>());
}

// ------------------- فاز ۶۹ — جایگزین‌های بی‌خطر org/devices -------------------

/// <summary>ماژول org غایب = هیچ پرسنلی در دسترس نیست (خالی/null — صفحات هسته بی‌کرش).</summary>
public sealed class NullPeopleDirectory : IPeopleDirectory
{
    public Task<List<HrPersonOption>> ActiveOptionsAsync() => Task.FromResult(new List<HrPersonOption>());
    public Task<List<HrDeptOption>> DepartmentsAsync() => Task.FromResult(new List<HrDeptOption>());
    public Task<List<HrPersonOption>> UnlinkedAsync() => Task.FromResult(new List<HrPersonOption>());
    public Task<HrPersonBrief?> FindAsync(string employeeId) => Task.FromResult<HrPersonBrief?>(null);
    public Task<Dictionary<string, HrPersonBrief>> BriefsAsync(IReadOnlyCollection<string> employeeIds)
        => Task.FromResult(new Dictionary<string, HrPersonBrief>());
    public Task<string?> FindIdByNameAsync(string firstName, string lastName) => Task.FromResult<string?>(null);
    public Task<List<HrPersonExportRow>> ExportRowsAsync() => Task.FromResult(new List<HrPersonExportRow>());
    public Task<HrSelfProfile?> SelfProfileAsync(string? personnelCode, string? linkedEmployeeId)
        => Task.FromResult<HrSelfProfile?>(null);
    public Task<HrPersonAuth?> AuthenticateAsync(string personnelCode, string nationalId)
        => Task.FromResult<HrPersonAuth?>(null);
    public Task<List<HrSearchHit>> SearchAsync(string q, int limit) => Task.FromResult(new List<HrSearchHit>());
    public Task<int> ActiveCountAsync() => Task.FromResult(0);
    public Task<Dictionary<string, int>> EmploymentTypeCountsAsync() => Task.FromResult(new Dictionary<string, int>());
    public Task<Dictionary<string, int>> CountsByCompanyAsync() => Task.FromResult(new Dictionary<string, int>());
    public Task<int> CountForCompanyAsync(string companyId) => Task.FromResult(0);
    public Task<List<HrContractExpiryRow>> ContractExpiryAsync(int days) => Task.FromResult(new List<HrContractExpiryRow>());
}

/// <summary>ماژول devices غایب = هیچ تجهیزی در دسترس نیست (خالی — داشبورد/اعلان‌ها بی‌کرش).</summary>
public sealed class NullDevicesGateway : IDevicesGateway
{
    public Task<HrDevicesDashDto> DashboardAsync()
        => Task.FromResult(new HrDevicesDashDto(0, 0, new List<HrDeviceStripItem>()));
    public Task<List<HrSearchHit>> SearchAsync(string q, int limit) => Task.FromResult(new List<HrSearchHit>());
    public Task<Dictionary<string, int>> CountsByCompanyAsync() => Task.FromResult(new Dictionary<string, int>());
    public Task<int> CountForCompanyAsync(string companyId) => Task.FromResult(0);
    public Task<List<HrDeviceStatusRow>> OfflineDevicesAsync(int minutes) => Task.FromResult(new List<HrDeviceStatusRow>());
}

// ======================================================================
// ثبت جایگزین‌های بی‌اثر یک ماژول غایب — در Program.cs و DbConfig (پروایدر ویزارد).
// Nullها استفاده می‌کنند: فهرست‌ها خالی، یافتن‌ها null، شمارش‌ها صفر، اعلان‌ها بی‌اثر.
// ======================================================================

/// <summary>ثبت جایگزین‌های بی‌اثر یک ماژول غایب — در Program.cs و DbConfig (پروایدر ویزارد).</summary>
public static class HrModuleFallbacks
{
    public static void RegisterAttendance(IServiceCollection s)
    {
        s.AddScoped<IAttendanceGateway, NullAttendanceGateway>();
        s.AddScoped<IAttendanceInsights, NullAttendanceGateway>();
        s.AddScoped<IAttendanceReportSource, NullAttendanceGateway>();
    }
    public static void RegisterVisitors(IServiceCollection s) => s.AddScoped<IVisitorsGateway, NullVisitorsGateway>();
    public static void RegisterAccess(IServiceCollection s) => s.AddScoped<IAccessGateway, NullAccessGateway>();
    public static void RegisterParking(IServiceCollection s) => s.AddScoped<IParkingReportSource, NullParkingReportSource>();
    public static void RegisterPeople(IServiceCollection s) => s.AddScoped<IPeopleDirectory, NullPeopleDirectory>();
    public static void RegisterDevices(IServiceCollection s) => s.AddScoped<IDevicesGateway, NullDevicesGateway>();
}
