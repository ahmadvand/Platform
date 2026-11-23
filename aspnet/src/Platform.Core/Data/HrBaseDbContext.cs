using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Data;

/// <summary>
/// فاز ۶۹ — کانتکست لایهٔ دیتای «هستهٔ پلتفرم» (core).
/// فقط entityهای هسته — پرسنل/دستگاه‌ها ∈ ماژول‌های org/devices (HrModules.*.Data)
/// و از مسیر DbContext خود ماژول یا کانتکست مرکب هاست (AppDbContext) می‌رسند.
/// این کانتکست صرفاً دسترسی دادهٔ هسته برای سرویس‌های پلتفرم (AuthCore/اعلان‌ها/…)
/// و کنترلرهای core هاست است — بدون آن، کوئری پرسنل/دستگاه از این مسیر اصلاً کامپایل نمی‌شود
/// و هر مصرف‌کننده به قرارداد (IPeopleDirectory/IDevicesGateway) منتقل شده است.
/// پیکربندی ایندکس‌های هسته قرینهٔ مدل مرکب است تا رفتار SaveChanges یکسان بماند.
/// </summary>
public class HrBaseDbContext : DbContext
{
    public HrBaseDbContext(DbContextOptions<HrBaseDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    // فاز ۷۴ — دامنهٔ دسترسی به رکورد (Row-Level) نقش‌ها
    public DbSet<RoleScope> RoleScopes => Set<RoleScope>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<ColorPalette> ColorPalettes => Set<ColorPalette>();
    public DbSet<SecurityAlert> SecurityAlerts => Set<SecurityAlert>();
    // فاز ۷۷ — زبان‌های پویا (استودیو ترجمه + پنل ادمین)
    public DbSet<HrLangDef> HrLangDefs => Set<HrLangDef>();
    public DbSet<HrLangEntry> HrLangEntries => Set<HrLangEntry>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // کلیدهای ترکیبی
        mb.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        mb.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        // فاز ۷۴ — جلوگیری از تکرار سطر دامنهٔ یکسان برای یک نقش
        mb.Entity<RoleScope>().HasIndex(x => new { x.RoleId, x.Kind, x.RefId });

        // ایندکس‌های یکتا (قرینهٔ مدل مرکب)
        mb.Entity<Company>().HasIndex(x => x.Code).IsUnique();
        mb.Entity<User>().HasIndex(x => x.Username).IsUnique();
        // فاز ۶۹ — ایندکس یکتا روی User.EmployeeId در هاست می‌ماند (ستون هسته)؛
        // FK آن به پرسنل مشروط ∈ ماژول org (ComposeCrossModule).
        mb.Entity<User>().HasIndex(x => x.EmployeeId).IsUnique();
        mb.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        mb.Entity<Permission>().HasIndex(x => x.Key).IsUnique();
        mb.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        mb.Entity<PasswordReset>().HasIndex(x => x.TokenHash).IsUnique();
        mb.Entity<ColorPalette>().HasIndex(x => x.Name).IsUnique();

        // دقت اعشار / طول شناسه‌ها — قرینهٔ مدل مرکب
        foreach (var entityType in mb.Model.GetEntityTypes())
            foreach (var prop in entityType.GetProperties())
            {
                if (prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?))
                {
                    prop.SetPrecision(18);
                    prop.SetScale(2);
                }
                if (prop.ClrType == typeof(string) && (prop.Name == "Id" || prop.Name.EndsWith("Id")))
                    prop.SetMaxLength(64);
            }

        // فاز ۸۲ — اسکیمای ماژول‌محور (هماهنگ با مدل مرکب هاست)
        Platform.Contracts.HrModuleRuntime.ApplySchemas(mb);
    }
}
