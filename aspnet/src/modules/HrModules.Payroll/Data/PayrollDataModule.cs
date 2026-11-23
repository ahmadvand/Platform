using HrModules.Payroll.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Payroll.Data;

// ======================================================================
// فاز ۶۸ — پلاگین دیتای ماژول حقوق و دستمزد (منتقل‌شده از هاست؛ رفتار همان)
// ======================================================================
public sealed class PayrollDataModule : IHrDataModule
{
    public string Code => "payroll";

    // فاز ۶۹ — خانوادهٔ نوع‌های این ماژول (مالکیت اسکیما) — داده‌محور برای گارد Ignore
    public Type[] FamilyTypes => [typeof(PayrollRun), typeof(Payslip), typeof(PayItem), typeof(PayrollParameter),
        typeof(PayrollSetting), typeof(PayslipLine), typeof(PayrollBonusRow), typeof(PayrollSettlement)];

    // فاز ۱۲۲ — اعلام خانواده‌های داده برای مرکز درون‌ریزی/برون‌ریزی
    public HrDataExchangeDecl[] DataExchange =>
    [
        new("PayItem", "اقلام فرمول‌ساز حقوق", "حقوق و دستمزد", true, "Code"),
        new("PayrollParameter", "پارامترهای سالانه حقوق", "حقوق و دستمزد", true, "Code"),
        new("PayrollRun", "لیست‌های حقوق", "حقوق و دستمزد", false),
        new("Payslip", "فیش‌های حقوق", "حقوق و دستمزد", false),
        new("PayslipLine", "سطرهای فیش حقوق", "حقوق و دستمزد", false),
        new("PayrollBonusRow", "عیدی و پایه سنوات", "حقوق و دستمزد", false),
        new("PayrollSettlement", "تسویه‌حساب‌ها", "حقوق و دستمزد", false),
        new("PayrollSetting", "تنظیمات محاسبه حقوق", "حقوق و دستمزد", false),
    ];
    public string Title => "حقوق و دستمزد";
    public string Icon => "💰";
    public string Description => "فرمول‌ساز اقلام، پارامترهای سالانه، اجرای لیست حقوق، فیش حقوقی، عیدی و تسویه";

    public void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb)
    {
        services.AddScoped<PayrollDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new PayrollDbContext(HrDbResolver.OptionsFor<PayrollDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<IModuleSeeder, PayrollSeeder>();
    }

    public void ComposeModel(ModelBuilder mb)
    {
        mb.Entity<PayrollRun>().ToTable("PayrollRuns");
        mb.Entity<Payslip>().ToTable("Payslips");
        mb.Entity<PayItem>().ToTable("PayItems");
        mb.Entity<PayrollParameter>().ToTable("PayrollParameters");
        mb.Entity<PayrollSetting>().ToTable("PayrollSettings");
        mb.Entity<PayslipLine>().ToTable("PayslipLines");
        mb.Entity<PayrollBonusRow>().ToTable("PayrollBonusRows");
        mb.Entity<PayrollSettlement>().ToTable("PayrollSettlements");
        mb.Entity<PayrollRun>().HasIndex(x => new { x.CompanyId, x.JYear, x.JMonth }).IsUnique();
        mb.Entity<Payslip>().HasIndex(x => new { x.RunId, x.EmployeeId }).IsUnique();
        mb.Entity<PayItem>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<PayrollParameter>().HasIndex(x => new { x.CompanyId, x.JYear, x.Code }).IsUnique();
        mb.Entity<PayrollSetting>().HasIndex(x => x.CompanyId).IsUnique();
        mb.Entity<PayslipLine>().HasIndex(x => x.PayslipId);
        // عیدی/سنوات سالانه و تسویه (فاز ۲۵)
        mb.Entity<PayrollBonusRow>().HasIndex(x => new { x.CompanyId, x.JYear, x.EmployeeId }).IsUnique();
        mb.Entity<PayrollSettlement>().HasIndex(x => x.EmployeeId);
        // موتور حقوق داینامیک (فاز ۲۴)
        mb.Entity<PayslipLine>().HasOne(x => x.Payslip).WithMany(p => p.Lines)
            .HasForeignKey(x => x.PayslipId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PayItem>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PayrollParameter>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PayrollSetting>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        // مسیر آبشاری دوم از شرکت نباید باشد (نقض «چند مسیر آبشاری» در SQL Server)؛
        // حذف شرکت از مسیر Employee (آبشاری) این ردیف‌ها را پاک می‌کند و NO ACTION در پایان دستور بررسی می‌شود
        mb.Entity<PayrollBonusRow>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.NoAction);
        mb.Entity<PayrollBonusRow>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PayrollSettlement>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.NoAction);
        mb.Entity<PayrollSettlement>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PayItem>().Property(x => x.Code).HasMaxLength(100);
        mb.Entity<PayrollParameter>().Property(x => x.Code).HasMaxLength(100);
    }

    // ======================================================================
    // فاز ۶۸ — پاک‌سازی/آبشار شرکت/شمارش‌ها (پورت از DbSeeder/CompaniesController هاست)
    // ======================================================================

    /// <summary>پاک‌سازی فیش‌ها و لیست‌های حقوق — پورت ClearPayrollDemo.</summary>
    public Task ClearDemoAsync(DbContext db)
    {
        db.Set<Payslip>().RemoveRange(db.Set<Payslip>().ToList());
        db.Set<PayrollRun>().RemoveRange(db.Set<PayrollRun>().ToList());
        db.SaveChanges();
        return Task.CompletedTask;
    }

    /// <summary>آبشار حذف شرکت — پیکربندی حقوق شرکت (پورت CleanupPayrollConfig هاست؛
    /// فیش‌ها/اجراها از مسیر آبشاری پرسنل پاک می‌شوند).</summary>
    public Task OnCompanyDeleteAsync(DbContext db, string companyId)
    {
        var settings = db.Set<PayrollSetting>().Where(s => s.CompanyId == companyId).ToList();
        db.Set<PayrollSetting>().RemoveRange(settings);
        var items = db.Set<PayItem>().Where(i => i.CompanyId == companyId).ToList();
        db.Set<PayItem>().RemoveRange(items);
        var parameters = db.Set<PayrollParameter>().Where(p => p.CompanyId == companyId).ToList();
        db.Set<PayrollParameter>().RemoveRange(parameters);
        return Task.CompletedTask;
    }

    /// <summary>شرکت‌های دارای پیکربندی حقوق — پورت PayrollCompanyIds.</summary>
    public Task<List<string>> CompaniesWithConfigAsync(DbContext db) =>
        db.Set<PayrollSetting>().Select(s => s.CompanyId).ToListAsync();

    /// <summary>شمارش لیست‌های حقوق به‌ازای شرکت — پورت RunCounts/RunCountFor.</summary>
    public Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string kind) =>
        kind != "runs"
            ? Task.FromResult(new Dictionary<string, int>())
            : db.Set<PayrollRun>().Where(r => r.CompanyId != null).GroupBy(r => r.CompanyId!)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

    public Task<int> CompanyCountAsync(DbContext db, string kind, string companyId) =>
        kind != "runs" ? Task.FromResult(0)
            : db.Set<PayrollRun>().CountAsync(r => r.CompanyId == companyId);
}
