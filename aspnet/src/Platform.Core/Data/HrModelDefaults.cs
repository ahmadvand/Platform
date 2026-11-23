using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Data;

/// <summary>
/// فاز ۶۱ — تنظیمات مشترک مدل برای کانتکست‌های لایهٔ دیتای ماژول‌ها.
/// entityهای پایه به‌صورت ترانزیتیو (از طریق navigation) وارد مدل ماژول می‌شوند و
/// بدون DbSet صریح، نام جدولشان از نام کلاس (مفرد!) گرفته می‌شود — اینجا نام جدول
/// همهٔ entityهای پایه صریحاً هم‌نامِ کانتکست مرکب ( DbSets هاست ) تثبیت می‌شود تا
/// کوئری ماژول‌ها روی همان جدول‌ها برود. مالک ساخت اسکیما همان composite هاست است.
/// </summary>
public static class HrModelDefaults
{
    public static void Normalize(ModelBuilder mb)
    {
        // کلیدهای ترکیبی (کاربر/نقش)
        mb.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        mb.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });

        // نام جدول همهٔ entityهای پایه — دقیقاً مثل DbSetهای composite هاست
        mb.Entity<Company>().ToTable("Companies");
        mb.Entity<Department>().ToTable("Departments");
        mb.Entity<Position>().ToTable("Positions");
        mb.Entity<Employee>().ToTable("Employees");
        mb.Entity<Contract>().ToTable("Contracts");
        mb.Entity<Decree>().ToTable("Decrees");
        mb.Entity<Device>().ToTable("Devices");
        mb.Entity<User>().ToTable("Users");
        mb.Entity<Role>().ToTable("Roles");
        mb.Entity<Permission>().ToTable("Permissions");
        mb.Entity<UserRole>().ToTable("UserRoles");
        mb.Entity<RolePermission>().ToTable("RolePermissions");
        mb.Entity<RefreshToken>().ToTable("RefreshTokens");
        mb.Entity<PasswordHistory>().ToTable("PasswordHistories");
        mb.Entity<PasswordReset>().ToTable("PasswordResets");
        mb.Entity<AuditLog>().ToTable("AuditLogs");
        mb.Entity<DeviceCommand>().ToTable("DeviceCommands");
        mb.Entity<DeviceUser>().ToTable("DeviceUsers");
        mb.Entity<DeviceBiometric>().ToTable("DeviceBiometrics");
        mb.Entity<EmployeeBiometric>().ToTable("EmployeeBiometrics");
        mb.Entity<DeviceOfflinePunch>().ToTable("DeviceOfflinePunches");
        mb.Entity<DevicePunchLog>().ToTable("DevicePunchLogs");
        mb.Entity<Holiday>().ToTable("Holidays");
        mb.Entity<NotificationRule>().ToTable("NotificationRules");
        mb.Entity<NotificationLog>().ToTable("NotificationLogs");
        mb.Entity<NotificationSetting>().ToTable("NotificationSettings");
        mb.Entity<GatewaySetting>().ToTable("GatewaySettings");
        mb.Entity<AppSettings>().ToTable("AppSettings");
        mb.Entity<ColorPalette>().ToTable("ColorPalettes");
        mb.Entity<SecurityAlert>().ToTable("SecurityAlerts");

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

        // فاز ۸۲ — اسکیمای ماژول‌محور: همان نگاشت مدل مرکب هاست (HrModuleRuntime.ApplySchemas)
        // تا کوئری کانتکست ماژول به همان «schema.table» مدل مرکب برود — قرارداد واحد.
        Platform.Contracts.HrModuleRuntime.ApplySchemas(mb);
    }
}
