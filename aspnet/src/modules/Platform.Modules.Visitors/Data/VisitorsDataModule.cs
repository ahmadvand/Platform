using Platform.Modules.Visitors.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Modules.Visitors.Data;

// ======================================================================
// فاز ۶۸ — پلاگین دیتای ماژول ارباب رجوع (منتقل‌شده از هاست؛ رفتار همان)
// ======================================================================
public sealed class VisitorsDataModule : IHrDataModule
{
    public string Code => "visitors";

    // فاز ۶۹ — خانوادهٔ نوع‌های این ماژول (مالکیت اسکیما) — داده‌محور برای گارد Ignore
    public Type[] FamilyTypes => [typeof(Contractor), typeof(Visitor), typeof(VisitorCard), typeof(VisitLog)];

    // فاز ۱۲۲ — اعلام خانواده‌های داده برای مرکز درون‌ریزی/برون‌ریزی
    public HrDataExchangeDecl[] DataExchange =>
    [
        new("Visitor", "ارباب رجوع (مهمان/مراجع)", "کنترل تردد", true, "NationalId"),
        new("Contractor", "پیمانکاران", "کنترل تردد", true, "Code"),
        new("VisitorCard", "کارت‌های مهمان", "کنترل تردد", false),
        new("VisitLog", "لاگ بازدیدها", "کنترل تردد", false),
    ];
    public string Title => "بازدیدکنندگان";
    public string Icon => "🎟";
    public string Description => "ارباب رجوع، کارت بازدید، پیمانکاران و هشدارهای امنیتی";

    public void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb)
    {
        services.AddScoped<VisitorsDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new VisitorsDbContext(HrDbResolver.OptionsFor<VisitorsDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<IVisitorsGateway, VisitorsGateway>();
        services.AddScoped<IModuleSeeder, VisitorsSeeder>();
    }

    public void ComposeModel(ModelBuilder mb)
    {
        mb.Entity<Contractor>().ToTable("Contractors");
        mb.Entity<Visitor>().ToTable("Visitors");
        mb.Entity<VisitorCard>().ToTable("VisitorCards");
        mb.Entity<VisitLog>().ToTable("VisitLogs");
        mb.Entity<VisitorCard>().HasIndex(x => x.CardNo).IsUnique();
        mb.Entity<VisitorCard>().HasIndex(x => x.VisitorId).IsUnique();
    }

    // ======================================================================
    // فاز ۶۸ — پاک‌سازی/شمارش‌های شرکت (پورت از DbSeeder/CompaniesController هاست)
    // ======================================================================

    /// <summary>پاک‌سازی نمونه‌های ارباب‌رجوع — پورت ClearVisitorsDemo.</summary>
    public Task ClearDemoAsync(DbContext db)
    {
        db.Set<VisitLog>().RemoveRange(db.Set<VisitLog>().ToList());
        db.Set<VisitorCard>().RemoveRange(db.Set<VisitorCard>().ToList());
        db.Set<Visitor>().RemoveRange(db.Set<Visitor>().ToList());
        db.Set<Contractor>().RemoveRange(db.Set<Contractor>().ToList());
        db.SaveChanges();
        return Task.CompletedTask;
    }

    /// <summary>شمارش بازدیدکنندگان/پیمانکاران به‌ازای شرکت — پورت VisitorCounts/VisitorCountFor/ContractorCountFor.</summary>
    public Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string kind) =>
        kind != "visitors"
            ? Task.FromResult(new Dictionary<string, int>())
            : db.Set<Visitor>().Where(v => v.CompanyId != null).GroupBy(v => v.CompanyId!)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

    public Task<int> CompanyCountAsync(DbContext db, string kind, string companyId) => kind switch
    {
        "visitors" => db.Set<Visitor>().CountAsync(v => v.CompanyId == companyId),
        "contractors" => db.Set<Contractor>().CountAsync(c => c.CompanyId == companyId),
        _ => Task.FromResult(0),
    };
}
