using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace Platform.Web.Data;

// ======================================================================
// فاز ۶۹ — کانتکست مرکب «هستهٔ پلتفرم + پلاگین‌های دیتای کشف‌شده»
// ----------------------------------------------------------------------
// فاز ۶۷: اسکیمای هر ماژول فقط با لایهٔ دیتای حاضرش ساخته می‌شد.
// فاز ۶۸: DbSetهای ماژول‌های سیستم حذف و ComposeModel هر پلاگین مدل را تکمیل کرد.
// فاز ۶۹: پایه‌های سابق org/devices هم پلاگین‌اند — DbSetها و پیکربندی اسکیمای
//   پرسنل/دستگاه از این کانتکست به HrModules.Org.Data / HrModules.Devices.Data
//   منتقل شد (IHrDataModule.ComposeModel). فقط «هستهٔ پلتفرم» (core) ذات این کانتکست است.
//
// گارد Ignore (پایان OnModelCreating): وقتی ماژول org/devices در اجرا حاضر نیست،
// navigationهای باقی‌مانده (مثلاً Record.Employee در ماژول حضور یا User.EmployeeId)
// نوعِ خانوادهٔ غایب را «ترانزیتیو» به مدل می‌کشند — این‌جا صریحاً Ignore می‌شوند تا
// هیچ جدولی برای ماژول حذف‌شده ساخته نشود (EF Ignore = حذف entity + FKهای اشاره‌کننده).
// اسمبلی هرگز به نوع‌های موجودیت غایب کامپایل‌شده برنمی‌گردد (types در پلتفرم مشترک‌اند
// و همیشه حاضرند — مالکیت «اسکیما» است که ماژول‌محور شد).
// ======================================================================
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ---------- موجودیت‌های هستهٔ پلتفرم (همیشه حاضر) ----------
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
    // فاز ۱۰۵ — بسته‌های محافظ صفحه (انتقال خودِ اسکرین‌سیور؛ اجرای سندباکس‌شده)
    public DbSet<SsPackage> SsPackages => Set<SsPackage>();
    public DbSet<SecurityAlert> SecurityAlerts => Set<SecurityAlert>();
    // فاز ۷۷ — زبان‌های پویا (استودیو ترجمه + پنل ادمین)
    public DbSet<HrLangDef> HrLangDefs => Set<HrLangDef>();
    public DbSet<HrLangEntry> HrLangEntries => Set<HrLangEntry>();
    // فاز ۱۳۵ (مورد ۱۹) — توکن‌های API عمومی
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // فاز ۱۳۵ (مورد ۱۹) — جستجوی توکن با هش باید سریع و یکتا باشد
        mb.Entity<ApiToken>().HasIndex(x => x.TokenHash).IsUnique();
        // ---------- کلیدهای ترکیبی ----------
        mb.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        mb.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        // فاز ۷۴ — جلوگیری از تکرار سطر دامنهٔ یکسان برای یک نقش
        mb.Entity<RoleScope>().HasIndex(x => new { x.RoleId, x.Kind, x.RefId });

        // ---------- ایندکس‌های یکتای هسته (هم‌ارز @unique در Prisma) ----------
        mb.Entity<Company>().HasIndex(x => x.Code).IsUnique();
        mb.Entity<User>().HasIndex(x => x.Username).IsUnique();
        // فیلتر پیش‌فرض SQL Server (IS NOT NULL) حفظ شود تا چند کاربر بدون پرسنل مجاز باشد؛
        // در SQLite چند NULL در ایندکس یکتا همیشه مجاز است.
        // (ستون EmployeeId هسته است؛ FK آن به پرسنل مشروط ∈ ماژول org)
        mb.Entity<User>().HasIndex(x => x.EmployeeId).IsUnique();
        mb.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        mb.Entity<Permission>().HasIndex(x => x.Key).IsUnique();
        mb.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        mb.Entity<PasswordReset>().HasIndex(x => x.TokenHash).IsUnique();
        mb.Entity<ColorPalette>().HasIndex(x => x.Name).IsUnique();

        // ---------- فاز ۶۹ — اسکیمای پلاگین‌های دیتای کشف‌شده در بوت ----------
        // خانواده‌های پرسنل (org) و تجهیزات (devices) هم از همین مسیر مدل می‌شوند؛
        // هاست هیچ دانشی از موجودیت ماژول‌ها ندارد — فقط قرارداد IHrDataModule.
        Platform.Contracts.HrModuleRuntime.ComposeModules(mb);

        // فاز ۶۹ — گارد Ignore خانواده‌های غایب، درون ComposeModules (داده‌محور):
        // انواع هسته + خانواده‌های اعلامی ماژول‌های حاضر در مدل می‌مانند؛ هر نوع دیگری که
        // از navigation ماژول‌های حاضر ترانزیتیو کشیده شده (خانوادهٔ ماژول غایب) Ignore
        // می‌شود — صفر جدول فانتوم. هاست هیچ نام/نوع ماژولی را در کد خود ندارد.

        // دقت صریح اعشارها — حذف هشدارهای SQL Server و جلوگیری از کوتاه‌شدن بی‌صدای مقادیر
        // (مبالغ ریالی و روزهای مرخصی؛ دقت پیش‌فرض EF نیز همین 18,2 است پس برای دیتابیس‌های
        // موجود تغییری در اسکیما رخ نمی‌دهد، فقط هشدارها حذف می‌شوند)
        foreach (var entityType in mb.Model.GetEntityTypes())
            foreach (var prop in entityType.GetProperties())
                if (prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?))
                {
                    prop.SetPrecision(18);
                    prop.SetScale(2);
                }

        // فاز ۴۹ — طول ستون‌های شناسه‌ای: Id/کلیدهای خارجی رشته‌ای Guid «N» (۳۲ کاراکتر) هستند؛
        // پیش‌فرض EF برای کلید/ایندکس nvarchar(450) است و در ایندکس‌های ترکیبی از سقف ۱۷۰۰ بایت
        // ایندکس غیرخوشه‌ای SQL Server رد می‌شد (خطای ۱۹۴۶ — فاز بعدی کرش ویزارد).
        // سقف ۶۴ (هم‌طول برای PK و FK) همه ایندکس‌های ترکیبی را زیر سقف می‌برد؛
        // NationalId (کد ملی ۱۰ رقمی) و همه شناسه‌های Guid هم جا می‌شوند.
        // در SQLite طول ستون بی‌معنی است و جدول‌های موجود دست‌نخورده می‌مانند — فقط دیتابیس‌های تازه.
        foreach (var entityType in mb.Model.GetEntityTypes())
            foreach (var prop in entityType.GetProperties())
                if (prop.ClrType == typeof(string) && (prop.Name == "Id" || prop.Name.EndsWith("Id")))
                    prop.SetMaxLength(64);
    }

}
