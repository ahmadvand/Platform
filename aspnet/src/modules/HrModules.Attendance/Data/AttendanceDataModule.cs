using Platform.Web.Domain;
using Platform.Web.Data;
using Platform.Web.Services;
using HrModules.Attendance.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Attendance.Data;

// ======================================================================
// فاز ۶۸ — پلاگین دیتای ماژول حضور و غیاب
// هاست هیچ شناختی از این ماژول ندارد؛ کشف در بوت توسط HrModuleRuntime.
// ثبت DI (کانتکست/قراردادها/سیدر) و اسکیما — منتقل‌شده از ModuleRegistration
// و AppDbContext.ComposeAttendance در هاست (رفتار بایت‌به‌بایت همان).
// ======================================================================
public sealed class AttendanceDataModule : IHrDataModule
{
    public string Code => "attendance";

    // فاز ۶۹ — خانوادهٔ نوع‌های این ماژول (مالکیت اسکیما) — داده‌محور برای گارد Ignore
    // فاز ۱۳۵ (مورد ۹) — انواع گردش‌کار تأیید چندمرحله‌ای هم به خانواده اضافه شدند
    // (بدون این، گارد Ignore مدل مرکب آن‌ها را حذف می‌کرد و جدول‌ها ساخته نمی‌شد)
    public Type[] FamilyTypes => [typeof(Shift), typeof(AttendanceRecord), typeof(LeaveRequest),
        typeof(Mission), typeof(OvertimePermit), typeof(CorrectionRequest), typeof(AttendanceExportTemplate),
        typeof(ApprovalFlow), typeof(ApprovalFlowStep), typeof(ApprovalInstance), typeof(ApprovalAction),
        typeof(ApprovalDelegation)];

    // فاز ۱۲۲ — اعلام خانواده‌های داده برای مرکز درون‌ریزی/برون‌ریزی
    public HrDataExchangeDecl[] DataExchange =>
    [
        new("Shift", "شیفت‌ها", "حضور و غیاب", true, "Name"),
        new("AttendanceExportTemplate", "قالب‌های برون‌بری تردد", "حضور و غیاب", true, "Name"),
        new("AttendanceRecord", "رکوردهای تردد (تاریخچه)", "حضور و غیاب", false),
        new("LeaveRequest", "درخواست‌های مرخصی", "حضور و غیاب", false),
        new("Mission", "مأموریت‌ها", "حضور و غیاب", false),
        new("OvertimePermit", "مجوزهای اضافه‌کاری", "حضور و غیاب", false),
        new("CorrectionRequest", "درخواست‌های اصلاح تردد", "حضور و غیاب", false),
    ];
    public string Title => "حضور و غیاب";
    public string Icon => "🕒";
    public string Description => "شیفت‌ها و نوبت‌کاری، تردد روزانه، درخواست‌های مرخصی/ماموریت/اضافه‌کار و اصلاحات و موتور محاسبهٔ کارکرد";

    public void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb)
    {
        services.AddScoped<AttendanceDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new AttendanceDbContext(HrDbResolver.OptionsFor<AttendanceDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<IAttendanceGateway, AttendanceGateway>();
        services.AddScoped<IAttendanceInsights, AttendanceGateway>();
        services.AddScoped<IAttendanceReportSource, AttendanceGateway>();
        services.AddScoped<IModuleSeeder, AttendanceSeeder>();
    }

    public void ComposeModel(ModelBuilder mb)
    {
        mb.Entity<Shift>().ToTable("Shifts");
        mb.Entity<AttendanceRecord>().ToTable("AttendanceRecords");
        mb.Entity<LeaveRequest>().ToTable("LeaveRequests");
        mb.Entity<Mission>().ToTable("Missions");
        mb.Entity<OvertimePermit>().ToTable("OvertimePermits");
        mb.Entity<CorrectionRequest>().ToTable("CorrectionRequests");
        // فاز ۹۶ — قالب‌های برون‌بری تردد
        mb.Entity<AttendanceExportTemplate>().ToTable("AttendanceExportTemplates");
        mb.Entity<AttendanceExportTemplate>().Property(x => x.ColumnsJson).HasMaxLength(4000);
        mb.Entity<AttendanceExportTemplate>().Property(x => x.Name).HasMaxLength(120);
        mb.Entity<AttendanceExportTemplate>().Property(x => x.Note).HasMaxLength(400);
        // فاز ۱۳۵ (مورد ۹) — گردش‌کار تأیید چندمرحله‌ای + جانشین‌پذیری
        mb.Entity<ApprovalFlow>().ToTable("ApprovalFlows");
        mb.Entity<ApprovalFlowStep>().ToTable("ApprovalFlowSteps");
        mb.Entity<ApprovalInstance>().ToTable("ApprovalInstances");
        mb.Entity<ApprovalAction>().ToTable("ApprovalActions");
        mb.Entity<ApprovalDelegation>().ToTable("ApprovalDelegations");
        // Employee→Shift (شیفت ∈ ماژول حضور؛ مجموعهٔ معکوس Shift.Employees حفظ شده)
        mb.Entity<Employee>().HasOne<Shift>().WithMany(s => s.Employees)
            .HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.ClientSetNull);
        mb.Entity<AttendanceRecord>().HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        // جدول‌های حضور → پرسنل (قراردادِ required+Cascade)
        mb.Entity<AttendanceRecord>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<LeaveRequest>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Mission>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<OvertimePermit>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<CorrectionRequest>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<AttendanceRecord>().HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        // تاریخ میلادی رشته‌ای «2026-10-18»
        mb.Entity<AttendanceRecord>().Property(x => x.Date).HasMaxLength(20);
    }

    // ======================================================================
    // فاز ۶۸ — سید نمایشی و شمارش‌های شرکت (پورت از DbSeeder/CompaniesController هاست)
    // ======================================================================

    /// <summary>PreSeed — شیفت‌های نمونه (ATT-02) پیش از ساخت پرسنل؛ شناسه‌ها در ctx.ShiftIds.</summary>
    public async Task PreSeedAsync(HrSeedContext ctx)
    {
        if (!ctx.DemoData) return;
        var db = ctx.Db;
        var sAdmin = new Shift { Name = "شیفت اداری", CompanyId = ctx.DemoCompanyId, Type = "عادی", StartTime = "08:00", EndTime = "16:00", BreakMinutes = 60, ToleranceLate = 10 };
        var sMorning = new Shift { Name = "نوبت اول (صبح)", CompanyId = ctx.DemoCompanyId, Type = "نوبت‌کاری", StartTime = "07:00", EndTime = "15:00", BreakMinutes = 45, ToleranceLate = 5 };
        var sEvening = new Shift { Name = "نوبت دوم (عصر)", CompanyId = ctx.DemoCompanyId, Type = "نوبت‌کاری", StartTime = "15:00", EndTime = "23:00", BreakMinutes = 45, ToleranceLate = 5 };
        var sNight = new Shift { Name = "نوبت سوم (شب)", CompanyId = ctx.DemoCompanyId, Type = "نوبت‌کاری", StartTime = "23:00", EndTime = "07:00", BreakMinutes = 45, ToleranceLate = 5 };
        var s12 = new Shift { Name = "شیفت ۱۲ ساعته روز", CompanyId = ctx.DemoCompanyId, Type = "۱۲ساعته", StartTime = "07:00", EndTime = "19:00", BreakMinutes = 60, ToleranceLate = 10 };
        var sCycle = new Shift { Name = "شیفت اقماری ۱۴/۱۴", CompanyId = ctx.DemoCompanyId, Type = "اقماری", StartTime = "07:00", EndTime = "19:00", BreakMinutes = 60, CyclePattern = "14/14", ToleranceLate = 15 };
        // شیفت‌ها ∈ شرکت پارس آسیا (همان شرکت دمو) — مثل رفتار قبلی EnsureShiftsAsync
        db.Set<Shift>().AddRange(sAdmin, sMorning, sEvening, sNight, s12, sCycle);
        await db.SaveChangesAsync();
        ctx.ShiftIds[HrDemoShiftKeys.Admin] = sAdmin.Id;
        ctx.ShiftIds[HrDemoShiftKeys.Morning] = sMorning.Id;
        ctx.ShiftIds[HrDemoShiftKeys.Evening] = sEvening.Id;
        ctx.ShiftIds[HrDemoShiftKeys.Night] = sNight.Id;
        ctx.ShiftIds[HrDemoShiftKeys.H12] = s12.Id;
        ctx.ShiftIds[HrDemoShiftKeys.Cycle] = sCycle.Id;
        ctx.Bag["attendance.demoShifts"] = new[] { sAdmin, sMorning, sEvening, sNight, s12, sCycle };
    }

    /// <summary>Seed — تردد ۱۴ روز اخیر + درخواست‌های مرخصی/مأموریت/مجوز/اصلاح (پورت SeedAttendanceDemo).</summary>
    public async Task SeedAsync(HrSeedContext ctx)
    {
        if (!ctx.DemoData) return;
        var db = ctx.Db;
        var today = DateTime.Now.Date;
        var shiftParams = db.Set<Shift>().ToList()
            .ToDictionary(x => x.Id, x => new ShiftParams { StartTime = x.StartTime, EndTime = x.EndTime, BreakMinutes = x.BreakMinutes, ToleranceLate = x.ToleranceLate });
        var empIds = ctx.Employees;
        var devIds = new[] { ctx.DeviceIds[0], ctx.DeviceIds[2], ctx.DeviceIds[4] }; // dev1/dev3/dev5 — همان آرایهٔ قبلی هاست

        var leavePlan = new (int EmpIdx, int FromOffset, int Days, string Type)[]
        {
            (2, -2, 3, "استحقاقی"), (12, -1, 2, "استعلاجی"),
            (19, 1, 4, "استحقاقی"), (25, -4, 5, "بدون‌حقوق"),
        };

        for (int off = -13; off <= 0; off++)
        {
            var day = today.AddDays(off);
            var dk = Jalali.DateKey(day);
            var friday = day.DayOfWeek == DayOfWeek.Friday;
            for (int i = 0; i < empIds.Count; i++)
            {
                var e = empIds[i];
                if (e.ShiftId is null) continue;
                var sp = shiftParams[e.ShiftId];

                var onLeave = leavePlan.FirstOrDefault(l => l.EmpIdx == i && off >= l.FromOffset && off < l.FromOffset + l.Days);
                if (friday)
                {
                    if (ctx.Rand() > 0.75) { /* تعطیل‌کاری برخی واحدهای عملیاتی */ }
                    else continue;
                }
                if (onLeave != default)
                {
                    db.Set<AttendanceRecord>().Add(new AttendanceRecord
                    {
                        EmployeeId = e.Id, Date = dk, Status = "مرخصی", Note = onLeave.Type, WorkMinutes = 0,
                    });
                    continue;
                }
                var roll = ctx.Rand();
                if (roll < 0.03)
                {
                    db.Set<AttendanceRecord>().Add(new AttendanceRecord
                    {
                        EmployeeId = e.Id, Date = dk, Status = "غایب", WorkMinutes = 0,
                    });
                    continue;
                }
                var sh = int.Parse(sp.StartTime[..2]);
                var sm = int.Parse(sp.StartTime[3..]);
                var eh = int.Parse(sp.EndTime[..2]);
                var em = int.Parse(sp.EndTime[3..]);
                var jitterIn = ctx.Rint(-6, 22);
                var jitterOut = ctx.Rint(-14, 85);
                var inD = day.AddHours(sh).AddMinutes(sm + jitterIn).AddSeconds(ctx.Rint(0, 59));
                var outD = day.AddHours(eh).AddMinutes(em + jitterOut).AddSeconds(ctx.Rint(0, 59));
                if (eh < sh) outD = outD.AddDays(1);
                var w = AttCalc.ComputeWork(sp, inD, outD, dk);
                db.Set<AttendanceRecord>().Add(new AttendanceRecord
                {
                    EmployeeId = e.Id, DeviceId = ctx.Pick(devIds), Date = dk,
                    CheckIn = inD, CheckOut = outD,
                    WorkMinutes = w.WorkMinutes, LateMinutes = w.LateMinutes,
                    EarlyMinutes = w.EarlyMinutes, OvertimeMinutes = w.OvertimeMinutes,
                    Status = w.LateMinutes > 0 ? "تأخیر" : "حاضر",
                });
            }
        }
        await db.SaveChangesAsync();

        void MkLeave(int i, string type, int fOff, int tOff, decimal days, string status, string reason)
        {
            db.Set<LeaveRequest>().Add(new LeaveRequest
            {
                EmployeeId = empIds[i].Id, Type = type,
                FromDate = Jalali.DateKey(today.AddDays(fOff)),
                ToDate = Jalali.DateKey(today.AddDays(tOff)),
                Days = days, Reason = reason, Status = status,
                Approver = status != "در انتظار تأیید" ? "مریم تهرانی" : null,
            });
        }
        MkLeave(6, "استحقاقی", 2, 4, 3, "در انتظار تأیید", "سفر خانوادگی");
        MkLeave(13, "ساعتی", 0, 0, 0.5m, "در انتظار تأیید", "مراجعه به بانک");
        MkLeave(20, "استعلاجی", -1, 0, 2, "تأیید شده", "گواهی پزشکی پیوست است");
        MkLeave(24, "استحقاقی", 5, 9, 5, "در انتظار تأیید", "مراسم عروسی");
        MkLeave(9, "بدون‌حقوق", -6, -5, 2, "تأیید شده", "امور شخصی");
        MkLeave(16, "زایمان", 8, 200, 90, "تأیید شده", "مرخصی زایمان طبق قانون کار");

        void MkMission(int i, string type, string dest, int fOff, int tOff, string status, string purpose)
        {
            db.Set<Mission>().Add(new Mission
            {
                EmployeeId = empIds[i].Id, Type = type, Destination = dest,
                FromDate = Jalali.DateKey(today.AddDays(fOff)),
                ToDate = Jalali.DateKey(today.AddDays(tOff)),
                Purpose = purpose, Status = status,
                Approver = status != "در انتظار تأیید" ? "ناصر زارعی" : null,
            });
        }
        MkMission(22, "داخلی", "تهران — شرکت پتروتجارت", 1, 2, "تأیید شده", "مذاکره قرارداد خرید کاتالیست");
        MkMission(25, "خارجی", "دبی — نمایشگاه ADIPEC", 10, 14, "در انتظار تأیید", "شرکت در نمایشگاه نفت و گاز");
        MkMission(17, "داخلی", "بندرعباس — اسکله", -2, -1, "تأیید شده", "هماهنگی حمل محموله");

        void MkPermit(int i, string type, int dOff, string from, string to, string status, string reason)
        {
            db.Set<OvertimePermit>().Add(new OvertimePermit
            {
                EmployeeId = empIds[i].Id, Type = type, Date = Jalali.DateKey(today.AddDays(dOff)),
                FromTime = from, ToTime = to, Reason = reason, Status = status,
                Approver = status != "در انتظار تأیید" ? "فرهاد سلطانی" : null,
            });
        }
        MkPermit(11, "اضافه‌کاری", 0, "16:00", "20:00", "تأیید شده", "توقف خط تولید — تعمیر اضطراری");
        MkPermit(3, "اضافه‌کاری", 1, "16:00", "19:00", "در انتظار تأیید", "بستن پرونده حقوق ماهانه");
        MkPermit(23, "کار در تعطیل", 2, "08:00", "14:00", "در انتظار تأیید", "کالیبراسیون تجهیزات در تعطیل");
        MkPermit(26, "اضافه‌کاری", -3, "16:00", "21:30", "تأیید شده", "آماده‌سازی ممیزی ISO");
        await db.SaveChangesAsync();

        var corrSpecs = new (int Idx, string Reason, string Status)[]
        {
            (11, "فراموشی زدن کارت هنگام ورود صبحگاهی", "در انتظار تأیید"),
            (12, "قطعی برق واحد — ثبت نشدن خروج", "در انتظار تأیید"),
            (21, "کارکرد دستگاه انبار خراب بود", "تأیید شده"),
            (22, "مأموریت اداری خارج از سایت", "رد شده"),
        };
        foreach (var c in corrSpecs)
        {
            var day = today.AddDays(-1);
            db.Set<CorrectionRequest>().Add(new CorrectionRequest
            {
                EmployeeId = empIds[c.Idx].Id,
                Date = Jalali.DateKey(day),
                RequestedIn = day.AddHours(7).AddMinutes(55),
                RequestedOut = day.AddHours(16).AddMinutes(5),
                Reason = c.Reason, Status = c.Status,
                ReviewedBy = c.Status != "در انتظار تأیید" ? "مریم تهرانی" : null,
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>پاک‌سازی نمونه‌های حضور (ReseedAsync) — پورت ClearAttendanceDemo.</summary>
    public Task ClearDemoAsync(DbContext db)
    {
        db.Set<AttendanceRecord>().RemoveRange(db.Set<AttendanceRecord>().ToList());
        db.Set<LeaveRequest>().RemoveRange(db.Set<LeaveRequest>().ToList());
        db.Set<Mission>().RemoveRange(db.Set<Mission>().ToList());
        db.Set<OvertimePermit>().RemoveRange(db.Set<OvertimePermit>().ToList());
        db.Set<CorrectionRequest>().RemoveRange(db.Set<CorrectionRequest>().ToList());
        db.Set<Shift>().RemoveRange(db.Set<Shift>().ToList());
        db.SaveChanges();
        return Task.CompletedTask;
    }

    /// <summary>شمارش شیفت‌ها به‌ازای شرکت — پورت ShiftCounts/ShiftCountFor.</summary>
    public Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string kind) =>
        kind != "shifts"
            ? Task.FromResult(new Dictionary<string, int>())
            : db.Set<Shift>().Where(s => s.CompanyId != null).GroupBy(s => s.CompanyId!)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

    public Task<int> CompanyCountAsync(DbContext db, string kind, string companyId) =>
        kind != "shifts" ? Task.FromResult(0)
            : db.Set<Shift>().CountAsync(s => s.CompanyId == companyId);
}
