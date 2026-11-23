using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Platform.Web.Infrastructure;

// ======================================================================
// فاز ۶۷ — ثبت مشروط لایه‌های دیتای ماژول‌ها (DbContext + قراردادهای بین‌ماژولی + سیدر)
// ----------------------------------------------------------------------
// تا فاز ۶۶ این ثبت‌ها مستقیم در Program.cs/DbConfig بودند؛ یعنی JIT بدنهٔ
// Program با هر نوعِ DLL غایب سقوط می‌کرد (تجربهٔ مشتری: FileNotFoundException
// روی HrModules.Access.Data در Program.<Main>). الگوی اینجا:
//   • هر ماژول یک متد AddX با [MethodImpl(NoInlining)] — بدنه حاوی انواع ماژول است؛
//   • فراخوانی فقط پس از گارد «#if» (بیلد مشتری) و «HrModulePresence» (حذف دستی DLL)؛
//   • امضای متدها فقط نوع‌های همیشه‌حاضر (ServiceCollection/IConfiguration) دارند
//     تا JIT فراخواننده هرگز به نوع غایب نرسد.
// ======================================================================
internal static class ModuleRegistration
{
    // ---------- ماژول حضور (Attendance) ----------
#if HR_MODULE_ATTENDANCE
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AddAttendance(IServiceCollection services, Action<DbContextOptionsBuilder>? useDb = null)
    {
        services.AddScoped<AttendanceDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new AttendanceDbContext(ResolveOptions<AttendanceDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<Platform.Contracts.IAttendanceGateway, AttendanceGateway>();
        services.AddScoped<Platform.Contracts.IAttendanceInsights, AttendanceGateway>();
        services.AddScoped<Platform.Contracts.IAttendanceReportSource, AttendanceGateway>();
        services.AddScoped<Platform.Contracts.IModuleSeeder, AttendanceSeeder>();
    }
#endif

    // ---------- ماژول کنترل تردد/دسترسی (Access) ----------
#if HR_MODULE_ACCESS
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AddAccess(IServiceCollection services, Action<DbContextOptionsBuilder>? useDb = null)
    {
        services.AddScoped<AccessDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new AccessDbContext(ResolveOptions<AccessDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<Platform.Contracts.IAccessGateway, AccessGateway>();
        services.AddScoped<Platform.Contracts.IModuleSeeder, AccessSeeder>();
    }
#endif

    // ---------- ماژول ارباب‌رجوع (Visitors) ----------
#if HR_MODULE_VISITORS
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AddVisitors(IServiceCollection services, Action<DbContextOptionsBuilder>? useDb = null)
    {
        services.AddScoped<VisitorsDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new VisitorsDbContext(ResolveOptions<VisitorsDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<Platform.Contracts.IVisitorsGateway, VisitorsGateway>();
        services.AddScoped<Platform.Contracts.IModuleSeeder, VisitorsSeeder>();
    }
#endif

    // ---------- ماژول حقوق و دستمزد (Payroll) ----------
#if HR_MODULE_PAYROLL
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AddPayroll(IServiceCollection services, Action<DbContextOptionsBuilder>? useDb = null)
    {
        services.AddScoped<PayrollDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new PayrollDbContext(ResolveOptions<PayrollDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<Platform.Contracts.IModuleSeeder, PayrollSeeder>();
    }
#endif

    // ---------- ماژول پارکینگ (Parking) ----------
#if HR_MODULE_PARKING
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AddParking(IServiceCollection services, Action<DbContextOptionsBuilder>? useDb = null)
    {
        services.AddScoped<ParkingDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new ParkingDbContext(ResolveOptions<ParkingDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<Platform.Contracts.IParkingReportSource, ParkingReportSource>();
        services.AddScoped<Platform.Contracts.IModuleSeeder, ParkingSeeder>();
    }
#endif

    /// <summary>گزینه‌های کانتکست: یا همان UseDb ویزارد (اتصال جدید در حال آزمایش) یا پیکربندی جاری نصب.</summary>
    private static DbContextOptionsBuilder<TCtx> ResolveOptions<TCtx>(
        Action<DbContextOptionsBuilder>? useDb, IConfiguration cfg) where TCtx : DbContext
    {
        var ob = new DbContextOptionsBuilder<TCtx>();
        if (useDb is not null)
        {
            useDb(ob);
            return ob;
        }
        var (provider, cs) = DbConfig.Resolve(cfg);
        if (DbConfig.IsSqlite(provider)) ob.UseSqlite(cs);
        else ob.UseSqlServer(cs);
        return ob;
    }

    /// <summary>ثبت همهٔ ماژول‌های حاضر — فراخوانی از Program.cs (پیکربندی جاری) و
    /// DbConfig.InitNewDatabaseAsync (اتصال جدید ویزارد با UseDb).</summary>
    internal static void AddPresentModules(IServiceCollection services, Action<DbContextOptionsBuilder>? useDb = null)
    {
#if HR_MODULE_ATTENDANCE
        if (Platform.Contracts.HrModulePresence.DataInstalled("attendance")) AddAttendance(services, useDb);
#endif
#if HR_MODULE_ACCESS
        if (Platform.Contracts.HrModulePresence.DataInstalled("access")) AddAccess(services, useDb);
#endif
#if HR_MODULE_VISITORS
        if (Platform.Contracts.HrModulePresence.DataInstalled("visitors")) AddVisitors(services, useDb);
#endif
#if HR_MODULE_PAYROLL
        if (Platform.Contracts.HrModulePresence.DataInstalled("payroll")) AddPayroll(services, useDb);
#endif
#if HR_MODULE_PARKING
        if (Platform.Contracts.HrModulePresence.DataInstalled("parking")) AddParking(services, useDb);
#endif
    }
}
