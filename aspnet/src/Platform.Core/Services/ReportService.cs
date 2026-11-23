using Platform.Contracts;

namespace Platform.Web.Services;

/// <summary>
/// گزارش‌ساز و داشبورد تحلیلی (فاز ۱۵) — تجمیع تردد برای روند ماهانه،
/// KPI واحد سازمانی و گزارش سفارشی چندبعدی + خروجی اکسل/PDF.
/// فاز ۶۱: این سرویس «نما» (façade) است — پرس‌وجوی تردد از قرارداد
/// IAttendanceReportSource (لایهٔ دیتای ماژول حضور) و رویداد پارکینگ از
/// IParkingReportSource (لایهٔ دیتای ماژول پارکینگ) می‌آید؛ خودش هیچ
/// DbContextای ندارد و رکوردهای خروجی همان شکل قبلی‌اند (قراردادها).
/// </summary>
public class ReportService
{
    // فاز ۶۷ — با بستهٔ بدون ماژول حضور/پارکینگ، منبع مربوطه null است (ثبت DI مشروط)؛
    // متدها خالیِ معتبر برمی‌گردانند تا گزارش‌ساز در هر ترکیب بسته‌ای کار کند.
    private readonly IAttendanceReportSource? Att;
    private readonly IParkingReportSource? Parking;

    public ReportService(IAttendanceReportSource? att, IParkingReportSource? parking)
    {
        Att = att;
        Parking = parking;
    }

    /// <summary>روند ماهانه آخرین n ماه جلالی (شامل ماه جاری) — فاز ۷۴: پارامترهای اختیاری دامنهٔ رکوردی.</summary>
    public Task<List<AttMonthPoint>> MonthlyTrendAsync(int months = 6, IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null) =>
        Att?.MonthlyTrendAsync(months, companyIds, deptIds, selfEmployeeId) ?? Task.FromResult(new List<AttMonthPoint>());

    /// <summary>KPI واحد سازمانی در بازه — نرخ حضور، تأخیر، اضافه‌کاری — فاز ۷۴: با دامنهٔ رکوردی.</summary>
    public Task<List<AttDeptKpi>> DeptKpisAsync(string? fromKey, string? toKey, IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null) =>
        Att?.DeptKpisAsync(fromKey, toKey, companyIds, deptIds, selfEmployeeId) ?? Task.FromResult(new List<AttDeptKpi>());

    /// <summary>گزارش سفارشی: فیلتر واحد/پرسنل/بازه/وضعیت — فاز ۷۴: با دامنهٔ رکوردی.</summary>
    public Task<List<AttCustomRow>> CustomQueryAsync(string? deptId, string? employeeId, string? fromKey, string? toKey, string? status, int limit = 1000, IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null) =>
        Att?.CustomQueryAsync(deptId, employeeId, fromKey, toKey, status, limit, companyIds, deptIds, selfEmployeeId) ?? Task.FromResult(new List<AttCustomRow>());

    /// <summary>
    /// گزارش ورود/خروج خودروهای پارکینگ در بازه — با فیلتر شرکت/پارکینگ/جهت/نتیجه/روش.
    /// روش «پلاک‌خوان» = ثبت خودکار توسط دوربین‌های پلاک‌خوان (ANPR).
    /// </summary>
    public Task<List<AttParkingRow>> ParkingEventsAsync(string? fromKey, string? toKey, string? co,
        string? lotId, string? direction, string? result, string? method, int limit = 1000,
        IReadOnlyCollection<string>? companies = null,
        IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null) =>
        Parking?.ParkingEventsAsync(fromKey, toKey, co, lotId, direction, result, method, limit, companies, deptIds, selfEmployeeId) ?? Task.FromResult(new List<AttParkingRow>());

    /// <summary>خلاصه به‌ازای هر پرسنل در بازه (برای PDF/اکسل) — فاز ۷۴: با دامنهٔ رکوردی.</summary>
    public Task<List<AttEmpSummary>> EmployeeSummaryAsync(string? deptId, string? fromKey, string? toKey, IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null) =>
        Att?.EmployeeSummaryAsync(deptId, fromKey, toKey, companyIds, deptIds, selfEmployeeId) ?? Task.FromResult(new List<AttEmpSummary>());
}
