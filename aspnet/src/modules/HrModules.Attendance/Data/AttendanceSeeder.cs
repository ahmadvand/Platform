using System.Globalization;
using Platform.Web.Data;
using Platform.Web.Services;
using HrModules.Attendance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Attendance.Data;

// ======================================================================
// فاز ۶۲ — سیدِ داده‌های اولیهٔ ماژول حضور و غیاب (IModuleSeeder)
// ----------------------------------------------------------------------
// هر بخش خودترمیم است (idempotent) و در اولین اجرا و هر بوت توسط هاست اجرا می‌شود:
//   ۱) سرپرستی شیفت‌ها — مالکیت شیفت بر اساس اکثریت پرسنل متصل (ترمیم دیتابیس‌های قدیمی)
//   ۲) شرکت نمایشی دوم «آریا سازه» (ASC-02) — ساختار سازمانی (پایه) + پرسنل + تردد/مرخصی/
//      مأموریت/اضافه‌کاری از ابتدای ماه جاری — مبنای روش محاسبهٔ «کارکردی» موتور حقوق
//   ۳) backfill لاگ تردد خام (DevicePunchLog) از رکوردهای سه روز اخیر — نمای زنده حراست
// دسترسی به دادهٔ پایه فقط از طریق HrBaseDbContext (پلتفرم) — بدون ارجاع به ماژول دیگر.
// جریان تصادفی قطعی (Mulberry32(2405)) — همان جریان سیدر قبلی؛ دادهٔ نمایشی تولیدشده
// با نسخه‌های قبل بایت‌به‌بایت یکسان می‌ماند.
// ======================================================================
public sealed class AttendanceSeeder : IModuleSeeder
{
    public string ModuleCode => "attendance";
    public int Order => 10;

    public async Task SeedAsync(IServiceProvider scopedServices, bool demoData, CancellationToken ct = default)
    {
        var db = scopedServices.GetRequiredService<AttendanceDbContext>();
        // فاز ۶۹ — دادهٔ org/devices (دپارتمان/پست/پرسنل/لاگ تردد) فقط در مدل «مرکب» است؛
        // HrBaseDbContext صرفاً هسته است. کانتکست مرکب بدون ارجاع به هاست از میان
        // DbContextهای ثبت‌شده با «وجود نوع خانواده در مدل» پیدا می‌شود (داده‌محور).
        AppDbContextLike(scopedServices, typeof(Platform.Web.Domain.Department), out var orgDb);
        AppDbContextLike(scopedServices, typeof(Platform.Web.Domain.DevicePunchLog), out var devDb);

        // ---------- ۱) خودترمیم: مالکیت شیفت‌ها بر اساس اکثریت پرسنل متصل ----------
        var companies = await db.Companies.OrderBy(c => c.Code).ToListAsync();
        if (companies.Count > 0)
        {
            var oldest = companies.OrderBy(c => c.CreatedAt).First();
            var allShifts = await db.Shifts.Include(s => s.Employees).ToListAsync();
            foreach (var sh in allShifts)
            {
                var owner = sh.Employees.Select(e => e.CompanyId)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .GroupBy(x => x).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key
                    ?? sh.CompanyId ?? oldest.Id;
                if (sh.CompanyId != owner) sh.CompanyId = owner;
            }
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        }

        // ---------- ۲) شرکت نمایشی دوم «آریا سازه» — سازمان + پرسنل + تردد ----------
        // فاز ۶۹ — ماژول org غایب = هیچ ساختار سازمانی نمایشی (بخش سازمانی خودکار رد می‌شود)
        if (orgDb is not null)
            await SeedAriaSazeAsync(db, orgDb, ct);

        // ---------- ۳) backfill لاگ تردد خام (نمای زنده حراست) ----------
        // فاز ۶۹ — ماژول devices غایب = لاگ تردد خام هم معنا ندارد
        if (devDb is not null)
            await BackfillPunchLogsAsync(db, devDb, ct);
    }

    // ======================================================================
    // شرکت نمایشی دوم (روش کارکردی) — بخش سازمانی/ترددیِ سیدر قبلی حقوق؛
    // پیکربندی حقوق در PayrollSeeder (ماژول حقوق) انجام می‌شود.
    // ======================================================================
    private static async Task SeedAriaSazeAsync(AttendanceDbContext db, DbContext cmpDb, CancellationToken ct)
    {
        var nowJ = Jalali.ToJalali(DateTime.Now);
        var c2 = await db.Companies.FirstOrDefaultAsync(x => x.Code == "ASC-02");
        if (c2 is null)
        {
            c2 = new Company { Name = "شرکت ساختمانی آریا سازه", Code = "ASC-02" };
            db.Companies.Add(c2);
            await db.SaveChangesAsync(ct);
        }
        var dHq = await cmpDb.Set<Department>().FirstOrDefaultAsync(d => d.Code == "ASC-HQ")
                  ?? (await cmpDb.Set<Department>().AddAsync(new Department { Name = "دفتر مرکزی آریا سازه", Code = "ASC-HQ", CompanyId = c2.Id })).Entity;
        var dSite = await cmpDb.Set<Department>().FirstOrDefaultAsync(d => d.Code == "ASC-SITE")
                  ?? (await cmpDb.Set<Department>().AddAsync(new Department { Name = "کارگاه مرکزی", Code = "ASC-SITE", CompanyId = c2.Id })).Entity;
        var pPm = await cmpDb.Set<Position>().FirstOrDefaultAsync(p => p.Code == "ASC-PM")
                  ?? (await cmpDb.Set<Position>().AddAsync(new Position { Title = "مدیر کارگاه", Code = "ASC-PM", DepartmentId = dSite.Id })).Entity;
        var pOp = await cmpDb.Set<Position>().FirstOrDefaultAsync(p => p.Code == "ASC-OP")
                  ?? (await cmpDb.Set<Position>().AddAsync(new Position { Title = "اپراتور ساختمانی", Code = "ASC-OP", DepartmentId = dSite.Id })).Entity;
        var pAcc = await cmpDb.Set<Position>().FirstOrDefaultAsync(p => p.Code == "ASC-ACC")
                  ?? (await cmpDb.Set<Position>().AddAsync(new Position { Title = "حسابدار", Code = "ASC-ACC", DepartmentId = dHq.Id })).Entity;
        await cmpDb.SaveChangesAsync(ct);
        var shDay = await db.Shifts.FirstOrDefaultAsync(s => s.Name == "شیفت روز کارگاه")
                  ?? (await db.Shifts.AddAsync(new Shift { Name = "شیفت روز کارگاه", CompanyId = c2.Id, Type = "عادی", StartTime = "07:00", EndTime = "19:00", BreakMinutes = 60, ToleranceLate = 10 })).Entity;
        var shNight = await db.Shifts.FirstOrDefaultAsync(s => s.Name == "شیفت شب کارگاه")
                  ?? (await db.Shifts.AddAsync(new Shift { Name = "شیفت شب کارگاه", CompanyId = c2.Id, Type = "نوبت‌کاری", StartTime = "19:00", EndTime = "07:00", BreakMinutes = 45, ToleranceLate = 5 })).Entity;
        await db.SaveChangesAsync(ct);

        if (await db.Employees.AnyAsync(e => e.CompanyId == c2.Id, ct)) return;

        var specs2 = new (string F, string L, Department D, Position P, string T, Shift? S, decimal Sal, int Y, string M, int C)[]
        {
            ("محمود", "کریمی", dSite, pPm, "قراردادی", shDay, 520_000_000m, 6, "متأهل", 2),
            ("اکبر", "ملکی", dSite, pOp, "پیمانکاری", shDay, 190_000_000m, 1, "متأهل", 1),
            ("رضوان", "صادقی", dSite, pOp, "پیمانکاری", shNight, 195_000_000m, 1, "مجرد", 0),
            ("حبیب", "دهقان", dSite, pOp, "اقماری", shDay, 210_000_000m, 2, "متأهل", 3),
            ("نرگس", "امینی", dHq, pAcc, "قراردادی", shDay, 300_000_000m, 3, "متأهل", 1),
            ("یوسف", "بهجت", dSite, pOp, "پیمانکاری", shNight, 190_000_000m, 1, "مجرد", 0),
        };
        var day1 = Jalali.FromJalali(nowJ.Y, nowJ.M, 1);
        var from = day1 > DateTime.Now.Date.AddDays(-25) ? day1 : DateTime.Now.Date.AddDays(-25);
        var spDay = new ShiftParams { StartTime = shDay.StartTime, EndTime = shDay.EndTime, BreakMinutes = shDay.BreakMinutes, ToleranceLate = shDay.ToleranceLate };
        var spNight = new ShiftParams { StartTime = shNight.StartTime, EndTime = shNight.EndTime, BreakMinutes = shNight.BreakMinutes, ToleranceLate = shNight.ToleranceLate };
        var nidBase = 1_400_000_000 + (int)(DateTime.Now.Ticks % 500_000_000);
        for (int i = 0; i < specs2.Length; i++)
        {
            var s = specs2[i];
            var e = new Employee
            {
                PersonnelCode = (2001 + i).ToString(CultureInfo.InvariantCulture),
                FirstName = s.F, LastName = s.L,
                NationalId = (nidBase + i * 3_777).ToString(CultureInfo.InvariantCulture),
                BirthDate = $"13{Rint(50, 75)}/0{Rint(1, 9)}/{Rint(10, 28):00}",
                Phone = $"0913{Rint(1000000, 9999999)}",
                Email = $"emp{2001 + i}@aryasaze.ir",
                EmploymentType = s.T, Status = "فعال",
                HireDate = DateTime.Now.AddDays(-365 * s.Y - Rint(0, 120)),
                CompanyId = c2.Id, DepartmentId = s.D.Id, PositionId = s.P.Id,
                BaseSalary = s.Sal, ShiftId = s.S?.Id,
                MaritalStatus = s.M, ChildCount = s.C,
                AnnualLeaveEnt = s.T == "رسمی" ? 26 : 22,
                AnnualLeaveUsed = Rint(0, 6),
            };
            db.Employees.Add(e);
            await db.SaveChangesAsync(ct);

            // تردد از ابتدای ماه جلالی تا امروز (روزهای کار) — مبنای لیست نمونهٔ حقوق
            for (var d = from.Date; d <= DateTime.Now.Date; d = d.AddDays(1))
            {
                var dk = Jalali.DateKey(d);
                if (d.DayOfWeek == DayOfWeek.Friday) continue;
                if (e.ShiftId is null) continue;
                if (i == 1 && dk == Jalali.DateKey(DateTime.Now.Date.AddDays(-2)))
                {
                    db.AttendanceRecords.Add(new AttendanceRecord { EmployeeId = e.Id, Date = dk, Status = "مرخصی", Note = "استحقاقی" });
                    continue;
                }
                if (i == 2 && Rand() < 0.12) { db.AttendanceRecords.Add(new AttendanceRecord { EmployeeId = e.Id, Date = dk, Status = "غایب" }); continue; }
                var sp = e.ShiftId == shNight.Id ? spNight : spDay;
                var sh = int.Parse(sp.StartTime[..2]);
                var sm = int.Parse(sp.StartTime[3..]);
                var eh = int.Parse(sp.EndTime[..2]);
                var em = int.Parse(sp.EndTime[3..]);
                var inD = d.AddHours(sh).AddMinutes(sm + Rint(-5, 15));
                var outD = d.AddHours(eh).AddMinutes(em + Rint(-8, 60));
                if (eh < sh) outD = outD.AddDays(1);
                var w = AttCalc.ComputeWork(sp, inD, outD, dk);
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    EmployeeId = e.Id, Date = dk, CheckIn = inD, CheckOut = outD,
                    WorkMinutes = w.WorkMinutes, LateMinutes = w.LateMinutes,
                    EarlyMinutes = w.EarlyMinutes, OvertimeMinutes = w.OvertimeMinutes,
                    Status = w.LateMinutes > 0 ? "تأخیر" : "حاضر",
                });
            }

            db.LeaveRequests.Add(new LeaveRequest
            {
                EmployeeId = e.Id, Type = "استحقاقی",
                FromDate = Jalali.DateKey(DateTime.Now.Date.AddDays(3)),
                ToDate = Jalali.DateKey(DateTime.Now.Date.AddDays(4)),
                Days = 2, Reason = "امور خانوادگی", Status = "در انتظار تأیید",
            });
            if (i == 0)
            {
                db.Missions.Add(new Mission
                {
                    EmployeeId = e.Id, Type = "داخلی", Destination = "پروژه برج نگین",
                    FromDate = Jalali.DateKey(DateTime.Now.Date.AddDays(-1)),
                    ToDate = Jalali.DateKey(DateTime.Now.Date),
                    Purpose = "نظارت اجرایی", Status = "تأیید شده", Approver = "مدیرعامل",
                });
            }
            if (i == 2)
            {
                db.OvertimePermits.Add(new OvertimePermit
                {
                    EmployeeId = e.Id, Type = "اضافه‌کاری",
                    Date = Jalali.DateKey(DateTime.Now.Date.AddDays(-1)),
                    FromTime = "19:00", ToTime = "22:30",
                    Reason = "بتن‌ریزی شبانه", Status = "تأیید شده", Approver = "محمود کریمی",
                });
            }
            await db.SaveChangesAsync(ct);
        }
        await db.SaveChangesAsync(ct);
    }

    // ======================================================================
    // backfill لاگ تردد خام از رکوردهای روزانه — فقط وقتی جدول خالی است (اولین ارتقا).
    // از CheckIn/CheckOut رکوردهای ۳ روز اخیر اسکن ورود/خروج می‌سازد تا «نمای زنده حراست»
    // روی دیتابیس‌های موجود هم از همان ابتدا پر و قابل استفاده باشد. Idempotent و بدون تصادف.
    // ======================================================================
    private static async Task BackfillPunchLogsAsync(AttendanceDbContext db, DbContext cmpDb, CancellationToken ct)
    {
        if (await cmpDb.Set<DevicePunchLog>().AnyAsync(ct)) return;

        var days = new List<string>();
        for (int off = -2; off <= 0; off++)
            days.Add(Jalali.DateKey(DateTime.Now.Date.AddDays(off)));

        var recs = await db.AttendanceRecords
            .Where(r => days.Contains(r.Date))
            .Include(r => r.Employee)
            .OrderBy(r => r.Date).ThenBy(r => r.CheckIn)
            .Take(600)
            .ToListAsync(ct);
        if (recs.Count == 0) return;

        foreach (var r in recs)
        {
            if (r.CheckIn is DateTime ci)
                cmpDb.Set<DevicePunchLog>().Add(new DevicePunchLog
                {
                    DeviceId = r.DeviceId, CompanyId = r.Employee?.CompanyId,
                    Pin = r.Employee?.PersonnelCode ?? "", EmployeeId = r.EmployeeId,
                    Timestamp = ci, Kind = "ورود",
                });
            if (r.CheckOut is DateTime co && co != r.CheckIn)
                cmpDb.Set<DevicePunchLog>().Add(new DevicePunchLog
                {
                    DeviceId = r.DeviceId, CompanyId = r.Employee?.CompanyId,
                    Pin = r.Employee?.PersonnelCode ?? "", EmployeeId = r.EmployeeId,
                    Timestamp = co, Kind = "خروج",
                });
        }
        await cmpDb.SaveChangesAsync(ct);
    }

    // ---------- RNG قطعی (mulberry32 — همان الگوی سیدر پلتفرم) ----------
    private static Func<double> _rand = Mulberry32(2405);
    private static double Rand() => _rand();
    private static int Rint(int a, int b) => a + (int)(Rand() * (b - a + 1));

    private static Func<double> Mulberry32(int seed)
    {
        uint a = (uint)seed;
        return () =>
        {
            a += 0x6D2B79F5;
            uint t = a;
            t = (t ^ (t >> 15)) * (1u | t);
            t ^= t + ((t ^ (t >> 7)) * (61u | t));
            return (t ^ (t >> 14)) / 4294967296.0;
        };
    }

    /// <summary>فاز ۶۹ — یافتن کانتکست «مرکب» (مالک اسکیمای همهٔ ماژول‌های حاضر) از میان
    /// DbContextهای ثبت‌شده — بدون ارجاع به هاست؛ ملاک: وجود نوع خانوادهٔ داده‌شده در مدل.</summary>
    private static void AppDbContextLike(IServiceProvider sp, Type familyType, out DbContext? composite)
    {
        composite = sp.GetServices<DbContext>()
            .FirstOrDefault(d => d.Model.FindEntityType(familyType) is not null);
    }
}
