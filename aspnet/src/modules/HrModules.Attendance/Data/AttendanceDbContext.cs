using Platform.Web.Domain;
using Platform.Web.Data;
using Platform.Web.Services;          // Jalali
using HrModules.Attendance.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Attendance.Data;

/// <summary>
/// کانتکست دیتای ماژول حضور و غیاب (فاز ۶۱) — فقط جدول‌های این ماژول +
/// entityهای پایه که navigation دارند (Employee/Device/Company به‌صورت فقط-نقشه).
/// ساخت اسکیما با Composite AppDbContext هاست است؛ این کانتکست هرگز EnsureCreated نمی‌شود.
/// </summary>
public class AttendanceDbContext : DbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : base(options) { }

    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<OvertimePermit> OvertimePermits => Set<OvertimePermit>();
    public DbSet<CorrectionRequest> CorrectionRequests => Set<CorrectionRequest>();
    // فاز ۹۶ — قالب‌های برون‌بری رکوردهای تردد (تاریخچه)
    public DbSet<AttendanceExportTemplate> ExportTemplates => Set<AttendanceExportTemplate>();
    // فاز ۱۳۵ (مورد ۹) — گردش‌کار تأیید چندمرحله‌ای + جانشین‌پذیری
    public DbSet<ApprovalFlow> ApprovalFlows => Set<ApprovalFlow>();
    public DbSet<ApprovalFlowStep> ApprovalFlowSteps => Set<ApprovalFlowStep>();
    public DbSet<ApprovalInstance> ApprovalInstances => Set<ApprovalInstance>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    // دادهٔ پایه که navigation/کوئری ماژول به آن نیاز دارد (همان جدول‌ها — مالک اسکیما: هاست)
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<AttendanceRecord>().HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        mb.Entity<AttendanceRecord>().Property(x => x.Date).HasMaxLength(20);
        mb.Entity<AttendanceRecord>().HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AttendanceRecord>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Shift>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.ClientSetNull);

        // فاز ۹۶ — قالب‌های برون‌بری تردد (هم‌سو با ComposeModel ماژول — جدول AttendanceExportTemplates)
        mb.Entity<AttendanceExportTemplate>().ToTable("AttendanceExportTemplates");
        mb.Entity<AttendanceExportTemplate>().Property(x => x.ColumnsJson).HasMaxLength(4000);
        mb.Entity<AttendanceExportTemplate>().Property(x => x.Name).HasMaxLength(120);
        mb.Entity<AttendanceExportTemplate>().Property(x => x.Note).HasMaxLength(400);

        // فاز ۱۳۵ (مورد ۹) — گردش‌کار تأیید چندمرحله‌ای
        mb.Entity<ApprovalFlow>().HasMany(f => f.Steps).WithOne(s => s.Flow!)
            .HasForeignKey(s => s.FlowId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ApprovalInstance>().HasMany(i => i.Actions).WithOne(a => a.Instance!)
            .HasForeignKey(a => a.InstanceId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ApprovalInstance>().HasIndex(x => new { x.ReqType, x.ReqId, x.Status });

        HrModelDefaults.Normalize(mb);
    }
}

/// <summary>
/// فاز ۶۱ — پیاده‌سازی قراردادهای ماژول حضور: دروازهٔ کارکرد (حقوق/دستگاه‌ها/اکسل)،
/// بینش‌های اعلانی، منبع گزارش تردد. ورودی/خروجی فقط DTO قرارداد — بدون افشای entity.
/// </summary>
public class AttendanceGateway(
    AttendanceDbContext db) : IAttendanceGateway, IAttendanceInsights, IAttendanceReportSource
{
    // ==================== IAttendanceGateway ====================

    public async Task<AttWorkData> FetchWorkDataAsync(
        IReadOnlyCollection<string> employeeIds, string fromDateKey, string toDateKeyExclusive)
    {
        var ids = employeeIds.ToList();
        var records = ids.Count == 0 ? new List<AttWorkRecord>() : await db.AttendanceRecords.AsNoTracking()
            .Where(a => ids.Contains(a.EmployeeId)
                        && a.Date.CompareTo(fromDateKey) >= 0 && a.Date.CompareTo(toDateKeyExclusive) < 0)
            .Select(a => new AttWorkRecord(a.EmployeeId, a.Date, a.CheckIn, a.CheckOut,
                a.WorkMinutes, a.LateMinutes, a.EarlyMinutes, a.OvertimeMinutes, a.Status))
            .ToListAsync();
        var leaves = ids.Count == 0 ? new List<AttApproval>() : await db.LeaveRequests.AsNoTracking()
            .Where(l => ids.Contains(l.EmployeeId) && l.Status == "تأیید شده")
            .Select(l => new AttApproval(l.EmployeeId, l.Type, l.Status, l.FromDate, l.ToDate, null, "", "", (int?)l.Hours, l.Days))
            .ToListAsync();
        var missions = ids.Count == 0 ? new List<AttApproval>() : await db.Missions.AsNoTracking()
            .Where(m => ids.Contains(m.EmployeeId) && m.Status == "تأیید شده")
            .Select(m => new AttApproval(m.EmployeeId, m.Type, m.Status, m.FromDate, m.ToDate, null, "", "", null, 0))
            .ToListAsync();
        var permits = ids.Count == 0 ? new List<AttApproval>() : await db.OvertimePermits.AsNoTracking()
            .Where(p => ids.Contains(p.EmployeeId) && p.Status == "تأیید شده")
            .Select(p => new AttApproval(p.EmployeeId, p.Type, p.Status, "", "", p.Date, p.FromTime, p.ToTime, null, 0))
            .ToListAsync();
        var holidays = (await db.Set<Holiday>().AsNoTracking().Select(h => h.Date).ToListAsync()).ToHashSet();
        var shiftByEmp = await ShiftsByEmployeeAsync(ids);
        return new AttWorkData(records, leaves, missions, permits, holidays, shiftByEmp);
    }

    public async Task<PunchUpsertResult> UpsertPunchAsync(string employeeId, string? deviceId, string dateKey, DateTime ts)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId)
            ?? throw new InvalidOperationException($"پرسنل {employeeId} یافت نشد");
        var shift = employee.ShiftId is null
            ? null
            : await db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == employee.ShiftId);

        var record = await db.AttendanceRecords
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == dateKey);
        if (record is null)
        {
            record = new AttendanceRecord
            {
                EmployeeId = employeeId, DeviceId = deviceId, Date = dateKey, Status = "حاضر",
            };
            db.AttendanceRecords.Add(record);
            await db.SaveChangesAsync();
        }

        // ورود: اولین رکورد روز؛ خروج: آخرین رکورد روز
        var isFirstIn = record.CheckIn is null || ts < record.CheckIn;
        var isLastOut = record.CheckOut is null || ts > record.CheckOut;
        var checkIn = isFirstIn ? ts : record.CheckIn!.Value;
        var checkOut = isLastOut ? ts : record.CheckOut!.Value;
        var sp = shift is null ? null : new ShiftParams
{
    StartTime = shift.StartTime, EndTime = shift.EndTime,
    BreakMinutes = shift.BreakMinutes, ToleranceLate = shift.ToleranceLate,
};
var w = AttCalc.ComputeWork(sp, checkIn, checkOut, dateKey);
        record.CheckIn = checkIn;
        record.CheckOut = checkOut;
        record.DeviceId = deviceId;
        record.WorkMinutes = w.WorkMinutes;
        record.LateMinutes = w.LateMinutes;
        record.EarlyMinutes = w.EarlyMinutes;
        record.OvertimeMinutes = w.OvertimeMinutes;
        if (record.Status == "غایب" || string.IsNullOrEmpty(record.Status))
            record.Status = AttCalc.StatusFromPunch(w.LateMinutes, true);
        await db.SaveChangesAsync();

        return new PunchUpsertResult(checkIn, checkOut, isFirstIn, isLastOut,
            w.WorkMinutes, w.LateMinutes, w.EarlyMinutes, w.OvertimeMinutes);
    }

    public async Task<Dictionary<string, AttShiftInfo>> ShiftsByEmployeeAsync(IReadOnlyCollection<string> employeeIds)
    {
        var ids = employeeIds.ToList();
        if (ids.Count == 0) return new();
        var rows = await db.Employees.AsNoTracking()
            .Where(e => ids.Contains(e.Id) && e.ShiftId != null)
            .Join(db.Shifts.AsNoTracking(), e => e.ShiftId, s => s.Id,
                (e, s) => new { e.Id, Shift = s })
            .ToListAsync();
        return rows.ToDictionary(x => x.Id, x => ToDto(x.Shift));
    }

    public async Task<List<AttShiftInfo>> ActiveShiftsAsync() =>
        await db.Shifts.AsNoTracking().Where(s => s.Active)
            .Select(s => ToDto(s))
            .ToListAsync();

    public async Task<List<AttShiftInfo>> AllShiftsAsync() =>
        await db.Shifts.AsNoTracking()
            .Select(s => ToDto(s))
            .ToListAsync();

    private static AttShiftInfo ToDto(Shift s) =>
        new(s.Id, s.Name, s.Type, s.StartTime, s.EndTime, s.BreakMinutes, s.CyclePattern, s.ToleranceLate, s.CompanyId);

    // ==================== IAttendanceInsights ====================

    public async Task<List<AttPersonRow>> LateTodayAsync(string todayKey, int thresholdMinutes) =>
        await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date == todayKey && a.Status == "تأخیر" && a.LateMinutes >= thresholdMinutes)
            .Join(db.Employees.AsNoTracking(), a => a.EmployeeId, e => e.Id,
                (a, e) => new AttPersonRow(e.PersonnelCode, e.FirstName + " " + e.LastName, a.LateMinutes))
            .ToListAsync();

    public async Task<List<AttPersonRow>> AbsentTodayAsync(string todayKey) =>
        await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date == todayKey && a.Status == "غایب")
            .Join(db.Employees.AsNoTracking(), a => a.EmployeeId, e => e.Id,
                (a, e) => new AttPersonRow(e.PersonnelCode, e.FirstName + " " + e.LastName, 0))
            .ToListAsync();

    public async Task<AttPendingCounts> PendingCountsAsync() => new(
        await db.LeaveRequests.CountAsync(x => x.Status == "در انتظار تأیید"),
        await db.Missions.CountAsync(x => x.Status == "در انتظار تأیید"),
        await db.CorrectionRequests.CountAsync(x => x.Status == "در انتظار تأیید"));

    // ==================== IAttendanceReportSource ====================
    // (منطق گزارش‌ها عیناً از ReportService قبلی منتقل شده — صفر تغییر رفتار)

    public async Task<List<AttMonthPoint>> MonthlyTrendAsync(int months,
        IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null)
    {
        var (jy, jm, _) = Jalali.ToJalali(DateTime.Now);
        var result = new List<AttMonthPoint>();
        var spans = new List<(int Y, int M, string From, string To, string Label)>();
        for (var i = months - 1; i >= 0; i--)
        {
            var m = jm - i; var y = jy;
            while (m <= 0) { m += 12; y--; }
            var (start, end) = Jalali.MonthRange(y, m);
            spans.Add((y, m, Jalali.DateKey(start), Jalali.DateKey(end.AddDays(-1)), $"{Jalali.MonthName(m)} {y % 100}"));
        }

        var firstFrom = spans[0].From;
        var lastTo = spans[^1].To;
        // فاز ۷۴ — دامنهٔ رکوردی: با فیلتر شرکت/واحد/خودم، روند از JOIN پرسنل می‌گذرد (SQL-side)
        var coF = companyIds is { Count: > 0 };
        var dpF = deptIds is { Count: > 0 };
        var sfF = !string.IsNullOrEmpty(selfEmployeeId);
        var all = new List<(string Date, string Status, int LateMinutes, int OvertimeMinutes)>();
        var rangeQ = db.AttendanceRecords.AsNoTracking()
            .Where(r => string.Compare(r.Date, firstFrom) >= 0 && string.Compare(r.Date, lastTo) <= 0);
        if (coF || dpF || sfF)
        {
            all = await rangeQ
                .Join(db.Employees.AsNoTracking(), r => r.EmployeeId, e => e.Id, (r, e) => new { r, e })
                .Where(x => (!coF || companyIds!.Contains(x.e.CompanyId))
                         && (!dpF || (x.e.DepartmentId != null && deptIds!.Contains(x.e.DepartmentId)))
                         && (!sfF || x.r.EmployeeId == selfEmployeeId))
                .Select(x => ValueTuple.Create(x.r.Date, x.r.Status, x.r.LateMinutes, x.r.OvertimeMinutes))
                .ToListAsync();
        }
        else
        {
            all = await rangeQ
                .Select(r => ValueTuple.Create(r.Date, r.Status, r.LateMinutes, r.OvertimeMinutes))
                .ToListAsync();
        }

        foreach (var (y, m, from, to, label) in spans)
        {
            var rows = all.Where(r => string.Compare(r.Date, from) >= 0 && string.Compare(r.Date, to) <= 0).ToList();
            result.Add(new AttMonthPoint(
                label,
                rows.Count(r => r.Status is "حاضر" or "تأخیر"),
                rows.Count(r => r.Status == "غایب"),
                rows.Count(r => r.Status is "مرخصی" or "مأموریت"),
                rows.Sum(r => r.LateMinutes),
                rows.Sum(r => r.OvertimeMinutes)));
        }
        return result;
    }

    public async Task<List<AttDeptKpi>> DeptKpisAsync(string? fromKey, string? toKey,
        IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null)
    {
        var from = fromKey ?? "0000-00-00";
        var to = toKey ?? "9999-99-99";
        // فاز ۷۴ — دامنهٔ رکوردی: KPI فقط از پرسنلِ شرکت‌ها/واحدهای مجاز (یا فقط خودِ کاربر)
        var coF = companyIds is { Count: > 0 };
        var dpF = deptIds is { Count: > 0 };
        var sfF = !string.IsNullOrEmpty(selfEmployeeId);

        var rows = await db.AttendanceRecords.AsNoTracking()
            .Where(r => string.Compare(r.Date, from) >= 0 && string.Compare(r.Date, to) <= 0)
            .Join(db.Employees, r => r.EmployeeId, e => e.Id, (r, e) => new { r, e })
            .Where(x => (!coF || companyIds!.Contains(x.e.CompanyId))
                     && (!dpF || (x.e.DepartmentId != null && deptIds!.Contains(x.e.DepartmentId)))
                     && (!sfF || x.e.Id == selfEmployeeId))
            .Select(x => new
            {
                x.e.DepartmentId,
                Dept = x.e.Department != null ? x.e.Department.Name : "بدون واحد",
                x.r.Status,
                x.r.LateMinutes,
                x.r.OvertimeMinutes,
            })
            .ToListAsync();

        var hcQ = db.Employees.AsNoTracking().Where(e => e.Status == "فعال");
        if (coF) hcQ = hcQ.Where(e => companyIds!.Contains(e.CompanyId));
        if (dpF) hcQ = hcQ.Where(e => e.DepartmentId != null && deptIds!.Contains(e.DepartmentId));
        if (sfF) hcQ = hcQ.Where(e => e.Id == selfEmployeeId);
        var headcount = await hcQ
            .GroupBy(e => e.DepartmentId)
            .Select(g => new { DeptId = g.Key, N = g.Count() })
            .ToListAsync();
        var deptNames = await db.Set<Department>().AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name);

        var byDept = rows.GroupBy(r => r.DepartmentId ?? "")
            .Select(g => new
            {
                Dept = g.Key == "" ? "بدون واحد" : deptNames.TryGetValue(g.Key, out var n) ? n : "بدون واحد",
                Present = g.Count(x => x.Status is "حاضر" or "تأخیر"),
                Absent = g.Count(x => x.Status == "غایب"),
                Late = g.Sum(x => x.LateMinutes),
                Ot = g.Sum(x => x.OvertimeMinutes),
            })
            .ToList();

        var hc = headcount.ToDictionary(x => x.DeptId ?? "", x => x.N);
        return byDept
            .Select(x => new AttDeptKpi(
                x.Dept,
                hc.TryGetValue("", out var _) && x.Dept == "بدون واحد" ? hc.Where(h => h.Key == "").Sum(h => h.Value) :
                    hc.FirstOrDefault(h => deptNames.TryGetValue(h.Key, out var n) && n == x.Dept).Value,
                x.Present, x.Absent, x.Late, x.Ot,
                x.Present + x.Absent == 0 ? 100 : Math.Round(100.0 * x.Present / (x.Present + x.Absent), 1)))
            .OrderByDescending(x => x.Headcount)
            .ToList();
    }

    public async Task<List<AttCustomRow>> CustomQueryAsync(string? deptId, string? employeeId, string? fromKey, string? toKey, string? status, int limit,
        IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null)
    {
        var from = fromKey ?? "0000-00-00";
        var to = toKey ?? "9999-99-99";

        var q = db.AttendanceRecords.AsNoTracking()
            .Where(r => string.Compare(r.Date, from) >= 0 && string.Compare(r.Date, to) <= 0)
            .Include(r => r.Employee).ThenInclude(e => e!.Department)
            .AsQueryable();

        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(r => r.EmployeeId == employeeId);
        if (!string.IsNullOrEmpty(deptId)) q = q.Where(r => r.Employee != null && r.Employee.DepartmentId == deptId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        // فاز ۷۴ — دامنهٔ رکوردی: فقط شرکت‌ها/واحدهای مجاز یا فقط خودِ کاربر (SQL-side روی ستون‌های ایندکس‌دار)
        if (companyIds is { Count: > 0 }) q = q.Where(r => r.Employee != null && companyIds.Contains(r.Employee.CompanyId));
        if (deptIds is { Count: > 0 }) q = q.Where(r => r.Employee != null && r.Employee.DepartmentId != null && deptIds.Contains(r.Employee.DepartmentId));
        if (!string.IsNullOrEmpty(selfEmployeeId)) q = q.Where(r => r.EmployeeId == selfEmployeeId);

        var rows = await q.OrderBy(r => r.Date).Take(limit)
            .Select(r => new AttCustomRow(
                r.Employee!.PersonnelCode,
                r.Employee.FirstName + " " + r.Employee.LastName,
                r.Employee.Department != null ? r.Employee.Department.Name : "بدون واحد",
                r.Date, r.Status,
                r.CheckIn, r.CheckOut, r.WorkMinutes, r.LateMinutes, r.OvertimeMinutes))
            .ToListAsync();
        return rows;
    }

    public async Task<List<AttEmpSummary>> EmployeeSummaryAsync(string? deptId, string? fromKey, string? toKey,
        IReadOnlyCollection<string>? companyIds = null, IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null)
    {
        var from = fromKey ?? "0000-00-00";
        var to = toKey ?? "9999-99-99";
        var eq = db.Employees.AsNoTracking().Include(e => e.Department).AsQueryable();
        if (!string.IsNullOrEmpty(deptId)) eq = eq.Where(e => e.DepartmentId == deptId);
        // فاز ۷۴ — دامنهٔ رکوردی: خلاصهٔ پرسنل فقط از شرکت‌ها/واحدهای مجاز یا فقط خودِ کاربر
        if (companyIds is { Count: > 0 }) eq = eq.Where(e => companyIds.Contains(e.CompanyId));
        if (deptIds is { Count: > 0 }) eq = eq.Where(e => e.DepartmentId != null && deptIds.Contains(e.DepartmentId));
        if (!string.IsNullOrEmpty(selfEmployeeId)) eq = eq.Where(e => e.Id == selfEmployeeId);
        var emps = await eq.ToListAsync();
        var ids = emps.Select(e => e.Id).ToList();

        var recs = await db.AttendanceRecords.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && string.Compare(r.Date, from) >= 0 && string.Compare(r.Date, to) <= 0)
            .GroupBy(r => r.EmployeeId)
            .Select(g => new
            {
                EmpId = g.Key,
                Present = g.Count(x => x.Status == "حاضر" || x.Status == "تأخیر"),
                Absent = g.Count(x => x.Status == "غایب"),
                Leave = g.Count(x => x.Status == "مرخصی" || x.Status == "مأموریت"),
                Late = g.Sum(x => x.LateMinutes),
                Ot = g.Sum(x => x.OvertimeMinutes),
                Work = g.Sum(x => x.WorkMinutes),
            })
            .ToListAsync();
        var byId = recs.ToDictionary(x => x.EmpId);

        return emps
            .Select(e => (e, byId.TryGetValue(e.Id, out var s) ? s : null))
            .Select(t =>
            {
                var s = t.Item2;
                return new AttEmpSummary(
                    t.e.PersonnelCode, t.e.FullName,
                    t.e.Department?.Name ?? "بدون واحد",
                    s?.Present ?? 0, s?.Absent ?? 0, s?.Leave ?? 0,
                    s?.Late ?? 0, s?.Ot ?? 0, s?.Work ?? 0);
            })
            .Where(x => x.Present + x.Absent + x.LeaveDays > 0)
            .OrderBy(x => x.PersonnelCode)
            .ToList();
    }

    // ==================== فاز ۶۸ — قلاب‌های جایگزین coreهای #if هاست ====================
    // (هاست بدون شناخت انواع ماژول، فقط از این قرارداد مصرف می‌کند)

    /// <summary>KPI تردد داشبورد — ۷ روز اخیر + امروز.</summary>
    public async Task<HrAttDashDto> DashboardAsync(string todayKey)
    {
        var fromKey = Jalali.DateKey(DateTime.Now.Date.AddDays(-6));
        var days = await db.AttendanceRecords.AsNoTracking()
            .Where(r => string.Compare(r.Date, fromKey) >= 0 && string.Compare(r.Date, todayKey) <= 0)
            .GroupBy(r => r.Date)
            .Select(g => new HrDashPoint(g.Key,
                g.Count(x => x.Status == "حاضر" || x.Status == "تأخیر" || x.Status == "مأموریت"),
                g.Count(x => x.Status == "غایب"),
                g.Sum(x => x.LateMinutes),
                g.Sum(x => x.OvertimeMinutes)))
            .ToListAsync();
        var week = new List<HrDashPoint>();
        for (var d = DateTime.Now.Date.AddDays(-6); d <= DateTime.Now.Date; d = d.AddDays(1))
        {
            var k = Jalali.DateKey(d);
            week.Add(days.FirstOrDefault(x => x.DateKey == k) ?? new HrDashPoint(k, 0, 0, 0, 0));
        }
        var today = await db.AttendanceRecords.AsNoTracking().Where(r => r.Date == todayKey).ToListAsync();
        var pending = await PendingCountsAsync();
        return new HrAttDashDto(
            today.Count(x => x.Status is "حاضر" or "تأخیر" or "مأموریت"),
            today.Count(x => x.Status == "تأخیر"),
            today.Count(x => x.Status == "غایب"),
            today.Count(x => x.Status == "مرخصی"),
            pending.Leaves + pending.Missions + pending.Corrections,
            week);
    }

    /// <summary>ردیف‌های خروجی اکسل یک روز (فایل‌سازی با ClosedXML در هاست می‌ماند).</summary>
    // فاز ۹۸ — رفع خطای ۵۰۰: OrderBy باید پیش از ساخت نوع نهایی روی anonymous type باشد تا EF بتواند
    // به ORDER BY ترجمه کند (مرتب‌سازی روی پراپرتی نوع ساخته‌شده قابل ترجمه نبود).
    public async Task<List<HrAttExportRow>> ExportDayAsync(string dateKey) =>
        await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.Date == dateKey)
            .Join(db.Employees.AsNoTracking(), r => r.EmployeeId, e => e.Id,
                (r, e) => new { Rec = r, e.PersonnelCode, e.FirstName, e.LastName })
            .OrderBy(x => x.PersonnelCode)
            .Select(x => new HrAttExportRow(x.PersonnelCode, x.FirstName + " " + x.LastName, x.Rec.Date,
                x.Rec.CheckIn, x.Rec.CheckOut, x.Rec.WorkMinutes, x.Rec.LateMinutes,
                x.Rec.EarlyMinutes, x.Rec.OvertimeMinutes, x.Rec.Status))
            .ToListAsync();

    /// <summary>نام شیفت‌ها به‌ازای شناسه.</summary>
    public async Task<Dictionary<string, string>> ShiftNamesAsync() =>
        await db.Shifts.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);

    /// <summary>ثبت ردیف‌های ورود اکسل — db همان کانتکست مرکب هاست است (Set&lt;T&gt; همان جدول‌ها).</summary>
    public async Task<(int Inserted, int Skipped)> ImportRowsAsync(DbContext compositeDb, IReadOnlyList<HrAttImportRow> rows)
    {
        int inserted = 0, skipped = 0;
        foreach (var row in rows)
        {
            var set = compositeDb.Set<AttendanceRecord>();
            var record = await set.FirstOrDefaultAsync(r => r.EmployeeId == row.EmployeeId && r.Date == row.Date);
            if (record is null)
            {
                record = new AttendanceRecord
                {
                    EmployeeId = row.EmployeeId, Date = row.Date, Status = row.Status,
                    WorkMinutes = row.WorkMinutes, LateMinutes = row.LateMinutes,
                    EarlyMinutes = row.EarlyMinutes, OvertimeMinutes = row.OvertimeMinutes,
                };
                compositeDb.Add(record);
                inserted++;
            }
            else
            {
                record.CheckIn = row.CheckIn ?? record.CheckIn;
                record.CheckOut = row.CheckOut ?? record.CheckOut;
                record.WorkMinutes = row.WorkMinutes;
                record.LateMinutes = row.LateMinutes;
                record.EarlyMinutes = row.EarlyMinutes;
                record.OvertimeMinutes = row.OvertimeMinutes;
                if (!string.IsNullOrWhiteSpace(row.Status)) record.Status = row.Status;
                skipped++;
            }
        }
        await compositeDb.SaveChangesAsync();
        return (inserted, skipped);
    }

    /// <summary>KPI خانهٔ پرتال من — پورت AttHomeCore هاست (کارکرد ماه + درخواست‌های در انتظار + ۷ رکورد اخیر).</summary>
    public async Task<HrSelfHomeDto> SelfHomeAsync(DbContext compositeDb, string employeeId, string fromKey, string toKey)
    {
        var recs = await compositeDb.Set<AttendanceRecord>().AsNoTracking()
            .Where(r => r.EmployeeId == employeeId && string.Compare(r.Date, fromKey) >= 0 && string.Compare(r.Date, toKey) <= 0)
            .OrderBy(r => r.Date)
            .ToListAsync();
        var pending =
            await compositeDb.Set<LeaveRequest>().CountAsync(x => x.EmployeeId == employeeId && x.Status == "در انتظار تأیید") +
            await compositeDb.Set<Mission>().CountAsync(x => x.EmployeeId == employeeId && x.Status == "در انتظار تأیید") +
            await compositeDb.Set<CorrectionRequest>().CountAsync(x => x.EmployeeId == employeeId && x.Status == "در انتظار تأیید");
        return new HrSelfHomeDto(
            recs.Count(r => r.Status is "حاضر" or "تأخیر"),
            recs.Sum(r => r.LateMinutes),
            recs.Count(r => r.Status == "غایب"),
            recs.Count(r => r.Status is "مرخصی" or "مأموریت"),
            recs.Sum(r => r.WorkMinutes),
            recs.Sum(r => r.OvertimeMinutes),
            pending,
            recs.TakeLast(7).Reverse().ToList());
    }

    /// <summary>فاز ۱۲۶ — درخواست‌های اخیر خانهٔ پرتال من — ادغام مرخصی/مأموریت/اصلاح تردد و ۸ تازه‌ترین؛ فقط نمایش.</summary>
    public async Task<HrSelfRequestsDto> SelfRequestsAsync(DbContext compositeDb, string employeeId)
    {
        var leaves = await compositeDb.Set<LeaveRequest>().AsNoTracking()
            .Where(x => x.EmployeeId == employeeId).OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync();
        var missions = await compositeDb.Set<Mission>().AsNoTracking()
            .Where(x => x.EmployeeId == employeeId).OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync();
        var corrections = await compositeDb.Set<CorrectionRequest>().AsNoTracking()
            .Where(x => x.EmployeeId == employeeId).OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync();

        var rows = new List<HrSelfRequestRow>();
        foreach (var x in leaves)
            rows.Add(new HrSelfRequestRow("مرخصی", x.Type, x.FromDate, x.ToDate,
                x.Hours is null ? $"{x.Days:0.#} روز" : $"{x.Days:0.#} روز · {x.Hours} ساعت", x.Status, x.Approver ?? "—"));
        foreach (var x in missions)
        {
            var dest = string.IsNullOrWhiteSpace(x.Purpose) ? x.Destination : $"{x.Destination} · {x.Purpose}";
            rows.Add(new HrSelfRequestRow("مأموریت", x.Type, x.FromDate, x.ToDate, dest, x.Status, x.Approver ?? "—"));
        }
        foreach (var x in corrections)
            rows.Add(new HrSelfRequestRow("اصلاح تردد", "اصلاح", x.Date, "", x.Reason, x.Status, x.ReviewedBy ?? "—"));

        return new HrSelfRequestsDto(rows
            .OrderByDescending(r => string.IsNullOrEmpty(r.ToKey) ? r.FromKey : r.ToKey)
            .Take(8).ToList());
    }
}
