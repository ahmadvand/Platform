using Platform.Web.Domain;
using Platform.Web.Data;
using HrModules.Payroll.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Payroll.Data;

/// <summary>کانتکست دیتای ماژول حقوق و دستمزد (فاز ۶۱).</summary>
public class PayrollDbContext : DbContext
{
    public PayrollDbContext(DbContextOptions<PayrollDbContext> options) : base(options) { }

    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<PayItem> PayItems => Set<PayItem>();
    public DbSet<PayrollParameter> PayrollParameters => Set<PayrollParameter>();
    public DbSet<PayrollSetting> PayrollSettings => Set<PayrollSetting>();
    public DbSet<PayslipLine> PayslipLines => Set<PayslipLine>();
    public DbSet<PayrollBonusRow> PayrollBonusRows => Set<PayrollBonusRow>();
    public DbSet<PayrollSettlement> PayrollSettlements => Set<PayrollSettlement>();
    // دادهٔ پایه (پرسنل/شرکت/تعطیلات برای محاسبه) — همان جدول‌ها؛ مالک اسکیما: هاست
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Holiday> Holidays => Set<Holiday>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<PayrollRun>().HasIndex(x => new { x.CompanyId, x.JYear, x.JMonth }).IsUnique();
        mb.Entity<Payslip>().HasIndex(x => new { x.RunId, x.EmployeeId }).IsUnique();
        mb.Entity<PayItem>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<PayrollParameter>().HasIndex(x => new { x.CompanyId, x.JYear, x.Code }).IsUnique();
        mb.Entity<PayrollSetting>().HasIndex(x => x.CompanyId).IsUnique();
        mb.Entity<PayslipLine>().HasIndex(x => x.PayslipId);
        mb.Entity<PayrollBonusRow>().HasIndex(x => new { x.CompanyId, x.JYear, x.EmployeeId }).IsUnique();
        mb.Entity<PayrollSettlement>().HasIndex(x => x.EmployeeId);
        mb.Entity<PayslipLine>().HasOne(x => x.Payslip).WithMany(p => p.Lines)
            .HasForeignKey(x => x.PayslipId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PayItem>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PayrollParameter>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PayrollSetting>().HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
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

        HrModelDefaults.Normalize(mb);
    }
}
