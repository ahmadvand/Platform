namespace Platform.Web.Domain;

// ============================================================
// مدل‌های دامنهٔ پلتفرم (فاز ۶۱ / بازبینی فاز ۶۹)
// نوع‌ها همه در پلتفرم مشترک‌اند تا ماژول‌های سیستم بدون ارجاع به ماژول دیگر
// کامپایل شوند؛ اما «مالکیت اسکیما/سید/کنترلر» هر خانواده با ماژول خودش است:
//   • هستهٔ پلتفرم (همیشه حاضر): Company/User/Role/…/AuditLog/AppSettings/…
//   • منابع انسانی (HrModules.Org): Department/Position/Employee/Contract/Decree/Holiday
//   • تجهیزات (HrModules.Devices): Device/DeviceUser/DeviceBiometric/…/GatewaySetting
// قاعدهٔ قراردادی (فاز ۶۸): رابطهٔ بین خانواده‌ها فقط ستون FK رشته‌ای — navigation
// بین‌خانواده‌ای ممنوع تا مدل EF هرگز نوع خانوادهٔ غایب را ترانزیتیو نگرفارد؛
// FKهای بین‌خانواده‌ای مشروط در ComposeCrossModule همان ماژول مالک اعمال می‌شوند.
// ============================================================

public class Company
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public bool Active { get; set; } = true;
    // فاز ۶۹ — navigation به خانواده‌های org/devices (Departments/Employees) حذف شد؛ فقط ستون‌های FK رشته‌ای
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class Department
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public Company? Company { get; set; }
    public string? ParentId { get; set; }
    public Department? Parent { get; set; }
    public List<Department> Children { get; set; } = new();
    public List<Position> Positions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class Position
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Grade { get; set; }
    /// <summary>شرکت مالک پست — همیشه با شرکت واحد سازمانی هم‌گام نگه داشته می‌شود (چندشرکتی).</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string DepartmentId { get; set; } = "";
    public Department? Department { get; set; }
    public string? Description { get; set; }
    public List<Employee> Employees { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class Employee
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PersonnelCode { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string? BirthDate { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string EmploymentType { get; set; } = "قراردادی"; // رسمی | قراردادی | پیمانکاری | اقماری | کارآموز
    public string Status { get; set; } = "فعال";             // فعال | غیرفعال | تسویه‌شده
    public DateTime HireDate { get; set; } = DateTime.Now;
    public DateTime? EndDate { get; set; }
    public string CompanyId { get; set; } = "";
    public Company? Company { get; set; }
    public string? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? PositionId { get; set; }
    public Position? Position { get; set; }
    public decimal BaseSalary { get; set; }
    /// <summary>عکس پرسنل (فاز ۴۵) — مسیر نسبی در wwwroot مثل /uploads/avatars/emp-xxxx.png؛ خالی = آواتار حرفی.</summary>
    public string? PhotoPath { get; set; }
    /// <summary>وضعیت تأهل — متغیر فرمول‌ساز حقوق (متأهل/مجرد).</summary>
    public string MaritalStatus { get; set; } = "مجرد"; // مجرد | متأهل
    /// <summary>تعداد فرزندان — ورودی حق اولاد در فرمول‌ساز.</summary>
    public int ChildCount { get; set; }
    public string? ShiftId { get; set; }
    public decimal AnnualLeaveEnt { get; set; } = 26;
    public decimal AnnualLeaveUsed { get; set; }
    public decimal SickLeaveEnt { get; set; } = 2;
    public decimal SickLeaveUsed { get; set; }
    public List<Contract> Contracts { get; set; } = new();
    public List<Decree> Decrees { get; set; } = new();
    // فاز ۶۹ — navigation به خانوادهٔ devices (DeviceUsers/Biometrics) و User حذف شد؛ فقط ستون‌های FK رشته‌ای
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string FullName => $"{FirstName} {LastName}";
}

public class Contract
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public string Type { get; set; } = "دائم"; // دائم | موقت | پروژه‌ای | آزمایشی
    public string ContractNo { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Salary { get; set; }
    public string Status { get; set; } = "فعال"; // فعال | منقضی | فسخ‌شده
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Decree
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public string Type { get; set; } = "استخدام"; // استخدام | ترفیع | انتقال | تعلیق | بازگشت | اصلاحیه
    public string DecreeNo { get; set; } = "";
    public DateTime Date { get; set; }
    public string? Content { get; set; }
    public bool Acknowledged { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    /// <summary>عکس پروفایل کاربر (فاز ۴۵) — در نوار بالایی و فهرست کاربران نمایش داده می‌شود.</summary>
    public string? PhotoPath { get; set; }
    public string? EmployeeId { get; set; }
    // فاز ۶۹ — navigation به خانوادهٔ org (Employee) حذف شد؛ فقط ستون FK رشته‌ای
    /// <summary>شرکتِ کاربر — خالی = همه شرکت‌ها (مدیران ارشد)؛ حساب‌های پرسنلی از شرکت پرسنل پر می‌شود.</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    // فاز ۷۴ — محدودیت رکوردی: واحد سازمانی صریح کاربر (سقف دامنه) — خالی = واحد از پرسنل متصل.
    // برای کاربرانی که پرسنل متصل ندارند اما دسترسی‌شان باید به یک واحد محدود شود (مثلاً سرپرست واحد).
    public string? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSuperadmin { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime PasswordChangedAt { get; set; } = DateTime.Now;
    public DateTime? PasswordExpiresAt { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<UserRole> Roles { get; set; } = new();
    public List<RefreshToken> RefreshTokens { get; set; } = new();
    public List<PasswordHistory> PasswordHistory { get; set; } = new();
    public List<PasswordReset> PasswordResets { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class Role
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";   // superadmin | hr_manager | att_operator | security | device_agent | viewer
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>نقشِ کاتالوگ سامانه — قابل حذف نیست (فاز ۷۴)؛ نقش‌های ادمین-ساخته false.</summary>
    public bool IsBuiltIn { get; set; }
    /// <summary>بازسازی پس از بازیابی سندباکس (ترمیم 2.7.19) — نام‌های رزروشدهٔ کاتالوگ نقش‌ها؛
    /// در هر بوت توسط DbSeeder از کاتالوگ بازساخته می‌شوند و برای نقش‌های جدید مجاز نیستند.</summary>
    public static readonly string[] SystemRoleNames =
        { "superadmin", "hr_manager", "att_operator", "security", "device_agent", "viewer" };
    /// <summary>
    /// فاز ۷۷ — سلسله‌مراتب نقش‌ها: نقش فرزند همهٔ دسترسی‌های نقش والد (و اجدادش) را
    /// به ارث می‌برد و فقط دسترسی‌های خودش به آن اضافه می‌شود. تهی = نقش ریشه.
    /// چرخه در سرویس و کنترلر مسدود می‌شود (والد نمی‌تواند خود نقش یا نسل‌های پایین‌ترش باشد).
    /// </summary>
    public string? ParentRoleId { get; set; }
    public Role? ParentRole { get; set; }
    public List<Role> Children { get; set; } = new();
    // محدودیت رکوردی نقش ∈ RoleScope (ردیف‌های all/company/dept/self — فاز ۷۴)
    public List<UserRole> Users { get; set; } = new();
    public List<RolePermission> Permissions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Permission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Key { get; set; } = "";   // employees.view | devices.control | ...
    public string Title { get; set; } = "";
    public string Grp { get; set; } = "";
    public List<RolePermission> Roles { get; set; } = new();
}

public class UserRole
{
    public string UserId { get; set; } = "";
    public User? User { get; set; }
    public string RoleId { get; set; } = "";
    public Role? Role { get; set; }
}

/// <summary>
/// فاز ۷۴ — دامنهٔ دسترسی به رکورد (Row-Level) برای نقش.
/// Kind: all | company | dept | self — RefId برای company/dept شناسهٔ رکورد است
/// (FK رشته‌ای بدون navigation — قاعدهٔ بین‌خانواده‌ای فاز ۶۸).
/// نقش بدون هیچ سطری = همهٔ داده‌ها (سازگاری با نصب‌های فعلی)؛
/// چند نقش کاربر با هم «اجتماع» می‌شوند و با User.CompanyId کاربر «اشتراک».
/// </summary>
public class RoleScope
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RoleId { get; set; } = "";
    public Role? Role { get; set; }
    public string Kind { get; set; } = "all"; // all | company | dept | self
    public string? RefId { get; set; }        // شناسهٔ شرکت یا واحد سازمانی؛ برای all/self تهی
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class RolePermission
{
    public string RoleId { get; set; } = "";
    public Role? Role { get; set; }
    public string PermissionId { get; set; } = "";
    public Permission? Permission { get; set; }
}

public class RefreshToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public User? User { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class PasswordHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public User? User { get; set; }
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class PasswordReset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public User? User { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = "";   // login | login_failed | logout | create | update | delete | approve | ...
    public string Entity { get; set; } = "";   // user | employee | device | leave | ...
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public DateTime At { get; set; } = DateTime.Now;

    // ---------- فاز ۱۱۷ — زنجیرهٔ ضد دستکاری (AuditChain) ----------
    /// <summary>شمارهٔ ترتیبی درون‌زنجیره (۱ = اولین رکورد زنجیره‌دار) — داخل قفل AuditChain.WriteGate تخصیص می‌یابد؛ ردیف‌های قدیمی = ۰ (خارج از زنجیره).</summary>
    public long Seq { get; set; }
    /// <summary>هش ردیف قبل — اولین ردیف زنجیره = null. دستکاری مستقیم پایگاه، هم‌ترازی زنجیره را می‌شکند.</summary>
    public string? PrevHash { get; set; }
    /// <summary>هش SHA-256 این ردیف از فیلدهایش + PrevHash — در «راستی‌آزمایی زنجیره» بازمحاسبه و مقایسه می‌شود.</summary>
    public string? Hash { get; set; }
}

// ===================== Device Operations =====================

public class Device
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>شرکت مالک دستگاه (چندشرکتی) — پرسنل ساخته‌شده از دستگاه به همین شرکت می‌رود.</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    /// <summary>نوع دستگاه — بر اساس کاتالوگ DeviceDefaults (ترمینال اثر انگشت، چهره، ترکیبی، کنترلر دسترسی، کارتی).</summary>
    public string Kind { get; set; } = "ترمینال اثر انگشت";
    public string Brand { get; set; } = "ZKTeco";
    public string Serial { get; set; } = "";
    public string? Ip { get; set; }
    public string? Location { get; set; }
    public string Purpose { get; set; } = "حضور و غیاب"; // حضور و غیاب | کنترل تردد
    /// <summary>نحوه اتصال گیت‌وی C# به دستگاه: tcp (پول مستقیم) یا adms (دستگاه به پل PUSH می‌کند)</summary>
    public string Transport { get; set; } = "adms";
    /// <summary>پورت پروتکل دستگاه (ZK: 4370) — null یعنی پیش‌فرض گیت‌وی</summary>
    public int? DevicePort { get; set; }
    /// <summary>کلید ارتباطی دستگاه (CommKey) — null یعنی پیش‌فرض گیت‌وی</summary>
    public int? CommKey { get; set; }
    public string Mode { get; set; } = "آنلاین";          // آنلاین | آفلاین
    public DateTime? LastSync { get; set; }
    public bool Active { get; set; } = true;
    public string WebhookToken { get; set; } = Guid.NewGuid().ToString("N");
    public int PollingInterval { get; set; } = 60;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastPulledAt { get; set; }
    public string? Firmware { get; set; }
    public int? UserCount { get; set; }
    public List<DeviceCommand> Commands { get; set; } = new();
    public List<DeviceUser> Users { get; set; } = new();
    public List<DeviceBiometric> Biometrics { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class DeviceCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = "";
    public Device? Device { get; set; }
    public string Type { get; set; } = ""; // unlock | sync_time | restart | push_user | delete_user | get_user | refresh_data | clear_data | enable_device | disable_device | get_users | clear_attendance
    public string? Payload { get; set; }   // JSON
    public string Status { get; set; } = "pending"; // pending | delivered | acked | failed
    public string? CreatedByUserId { get; set; }
    public string? CreatedBy { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? AckedAt { get; set; }
}

// ===================== داده‌های هویتی دستگاه‌ها (بایومتریک) =====================

/// <summary>کاربر ثبت‌شده روی یک دستگاه — هم‌ارز رکورد USER در حافظه دستگاه.</summary>

public class DeviceUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = "";
    public Device? Device { get; set; }
    public int Uid { get; set; }                 // شماره کاربری داخلی دستگاه
    public string Pin { get; set; } = "";         // شناسه کاربر روی دستگاه (کد پرسنلی)
    public string Name { get; set; } = "";
    public int Privilege { get; set; }            // 0 عادی | 2 ثبت‌نام | 6 سرپرست | 14 مدیر
    public string? CardNumber { get; set; }
    public string? Password { get; set; }
    public string? GroupId { get; set; }
    public string? EmployeeId { get; set; }       // پیوند به پرسنل سامانه — FK مشروط ∈ ماژول org
    public DateTime? LastFetchedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>قالب هویتی ذخیره‌شده روی یک دستگاه (اثر انگشت/چهره/کارت/رمز/عکس…).</summary>

public class DeviceBiometric
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = "";
    public Device? Device { get; set; }
    public string Pin { get; set; } = "";
    public string Type { get; set; } = "fp";      // fp | face | card | pin | palm | photo
    public int BioIndex { get; set; }             // شماره انگشت/اسلات
    public int Valid { get; set; } = 1;
    public int? Quality { get; set; }             // 0..100
    public string? TemplateData { get; set; }     // base64 (برای کارت: شماره کارت)
    public int Length { get; set; }               // طول قالب به بایت
    public DateTime FetchedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// حافظه آفلاین دستگاه (فاز ۳۰) — آینه صف ترددها در حافظه داخلی دستگاه هنگام قطعی.
/// ترددهای ثبت‌شده روی دستگاهِ آفلاین اینجا صف می‌شوند و با «دریافت دسته‌ای» وارد سامانه می‌شوند.
/// </summary>

public class DeviceOfflinePunch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = "";
    public Device? Device { get; set; }
    public string Pin { get; set; } = "";              // کد پرسنلی روی دستگاه
    public DateTime Timestamp { get; set; }            // زمان واقعی اسکن روی دستگاه
    public string Source { get; set; } = "شبیه‌ساز";    // شبیه‌ساز | گیت‌وی
    /// <summary>زمان ورود دسته‌ای به سامانه — null = هنوز در حافظه دستگاه.</summary>
    public DateTime? ImportedAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// لاگ تردد خام دستگاه‌ها (فاز ۴۳) — هر اسکن یک ردیف؛ مبنای «نمای زنده حراست».
/// برخلاف AttendanceRecord (رکورد تجمیعی روزانه)، اینجا هر اسکن به‌صورت جداگانه نگه داشته می‌شود
/// تا هر ترددِ لحظه‌ای با اطلاعات فرد (نام، کد پرسنلی، دستگاه، جهت) قابل نمایش باشد.
/// اسکن‌های ناشناس (کد پرسنلی بی‌صاحب) هم با Kind=ناشناس ثبت می‌شوند — برای حراست حیاتی است.
/// </summary>

public class DevicePunchLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>ستون ساده بدون FK — فیلتر چندشرکتی؛ شرکت از مسیر Device هم قابل رسیدن است
    /// (جدول پرترافیک — FK اضافه مسیرهای حذف را پیچیده می‌کند).</summary>
    public string? CompanyId { get; set; }
    public string? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string Pin { get; set; } = "";               // کد پرسنلی خام روی دستگاه
    public string? EmployeeId { get; set; }              // FK مشروط به پرسنل (ماژول org)
    public DateTime Timestamp { get; set; } = DateTime.Now;   // زمان واقعی اسکن روی دستگاه
    /// <summary>ورود | خروج | تردد (اسکن میانی روز) | ناشناس</summary>
    public string Kind { get; set; } = "تردد";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>قالب هویتی مرجع پرسنل — مرکز داده برای انتقال بین دستگاه‌ها.</summary>

public class EmployeeBiometric
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmployeeId { get; set; } = "";         // FK مشروط به پرسنل (ماژول org)
    public string Type { get; set; } = "fp";      // fp | face | card | pin | palm | photo
    public int BioIndex { get; set; }
    public int Valid { get; set; } = 1;
    public int? Quality { get; set; }
    public string? TemplateData { get; set; }     // base64 (برای کارت: شماره کارت)
    public int Length { get; set; }
    public string? SourceDeviceId { get; set; }
    public Device? SourceDevice { get; set; }
    public string? CardNumber { get; set; }       // برای نوع کارت
    public DateTime EnrolledAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

// ===================== Payroll / Corrections / Holidays =====================

public class Holiday
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Date { get; set; } = ""; // "2026-03-21"
    public string Title { get; set; } = "";
    public int JYear { get; set; }
    public int JMonth { get; set; }
    public int JDay { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class NotificationRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Type { get; set; } = "late";   // late | absence | contract_expiry | device_offline | pending_request
    public bool Enabled { get; set; } = true;
    public int Threshold { get; set; } = 15;     // دقیقه (تأخیر) یا روز (انقضا/آفلاین)
    public string? RecipientEmail { get; set; }  // گیرنده ثابت؛ خالی = ایمیل مدیران از تنظیمات
    public string? RecipientPhone { get; set; }
    public bool ViaEmail { get; set; } = true;
    public bool ViaSms { get; set; }
    /// <summary>فاز ۱۳۵ (مورد ۱۸) — ارسال به وب‌هوک (JSON POST به WebhookUrl تنظیمات).</summary>
    public bool ViaWebhook { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastRunAt { get; set; }
}

public class NotificationLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RuleName { get; set; } = "";
    public string Type { get; set; } = "";       // late | absence | ...
    public string Channel { get; set; } = "";    // email | sms
    public string Recipient { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = "sent"; // sent | failed | simulated
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class NotificationSetting
{
    public string Id { get; set; } = "default";
    public bool EnabledEmail { get; set; }
    public bool EnabledSms { get; set; }
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpSsl { get; set; } = true;
    public string SmtpUser { get; set; } = "";
    public string SmtpPass { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string AdminEmails { get; set; } = "";  // جداکننده کاما — گیرندگان پیش‌فرض
    public string SmsApiUrl { get; set; } = "";    // قالب با {phone} {message} {apikey} {sender}
    public string SmsApiKey { get; set; } = "";
    public string SmsSender { get; set; } = "";
    // فاز ۱۳۵ (مورد ۱۸ ROADMAP-ADDITIONS) — کانال وب‌هوک
    /// <summary>URL وب‌هوک خروجی — هر اعلان به‌صورت JSON POST به این نشانی می‌رود؛ خالی = غیرفعال.</summary>
    public string WebhookUrl { get; set; } = "";
    /// <summary>کلید امضا (اختیاری) — هدر X-Hr-Signature با HMAC-SHA256 بدنه ارسال می‌شود.</summary>
    public string WebhookSecret { get; set; } = "";
    /// <summary>انقضای خودکار هشدارهای باز پس از N روز (فاز ۳۰) — صفر = غیرفعال (پیش‌فرض).</summary>
    public int AlertTtlDays { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>تنظیمات سرویس گیت‌وی C# (HrDeviceGateway) — تک‌ردیفی؛ از پنل ادمین ویرایش و
/// توسط گیت‌وی از مسیر api/gateway/config به‌صورت پویا دریافت می‌شود (فاز ۲۱).
/// </summary>

public class GatewaySetting
{
    public string Id { get; set; } = "default";
    /// <summary>توقف/ادامه کل پایش دستگاه‌ها بدون توقف سرویس</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>پورت پل ADMS/PUSH — اعمال پس از ری‌استارت سرویس گیت‌وی</summary>
    public int ListenPort { get; set; } = 8081;
    /// <summary>زمان انتظار اتصال/پاسخ TCP دستگاه (میلی‌ثانیه)</summary>
    public int TcpTimeoutMs { get; set; } = 6000;
    /// <summary>فاصله پیش‌فرض poll دستگاه‌های TCP (ثانیه)</summary>
    public int PollIntervalSec { get; set; } = 60;
    /// <summary>فاصله حلقه بازبینی وضعیت/دستورات (ثانیه)</summary>
    public int SweeperIntervalSec { get; set; } = 60;
    /// <summary>فاصله دریافت تنظیمات از سرور توسط گیت‌وی (ثانیه)</summary>
    public int ConfigRefreshSec { get; set; } = 30;
    /// <summary>سطح لاگ گیت‌وی: Debug | Information | Warning | Error</summary>
    public string LogLevel { get; set; } = "Information";
    /// <summary>پورت پیش‌فرض پروتکل دستگاه (ZK: 4370) — برای دستگاه‌های بدون مقدار</summary>
    public int DefaultDevicePort { get; set; } = 4370;
    /// <summary>کلید ارتباطی پیش‌فرض (CommKey) برای دستگاه‌های بدون مقدار</summary>
    public int DefaultCommKey { get; set; } = 0;
    /// <summary>آخرین باری که گیت‌وی تنظیمات را دریافت کرد — نشانگر سلامت اتصال</summary>
    public DateTime? LastFetchedAt { get; set; }
    /// <summary>نسخه کانفیگ — با هر ذخیره افزایش می‌یابد تا گیت‌وی تغییر را تشخیص دهد</summary>
    public int ConfigVersion { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

// ===================== کنترل دسترسی پیشرفته (فاز ۱۷) =====================

/// <summary>گروه دسترسی — مجموعه پرسنل + گیت‌های مجاز با برنامه زمانی اختصاصی هر گیت.</summary>

public class AppSettings
{
    public string Id { get; set; } = "default";

    // ---------- اطلاعات شرکت نصب‌شده ----------
    /// <summary>نام نمایشی شرکت — در برند سایدبار، صفحه ورود، فوتر و عنوان‌ها</summary>
    public string CompanyName { get; set; } = "شرکت پتروشیمی پارس آسیا";
    /// <summary>نام حقوقی کامل (ثبتی)</summary>
    public string? CompanyLegalName { get; set; }
    /// <summary>شناسه ملی / کد اقتصادی</summary>
    public string? CompanyNationalId { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyEmail { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyAddress { get; set; }
    /// <summary>توضیح کوتاه زیر نام در صفحه ورود</summary>
    public string? Description { get; set; }

    // ---------- لوگو ----------
    /// <summary>مسیر نسبی لوگو در wwwroot — مثل /uploads/logo.png (خالی = نشان پیش‌فرض ◈)</summary>
    public string? LogoPath { get; set; }

    // ---------- پالت رنگ (اختیاری — خالی = پالت پیش‌فرض site.css) ----------
    /// <summary>رنگ اصلی/تأکیدی — مثل #6366f1 (لایه سازگاری قدیمی؛ با پالت فعال بازنویسی می‌شود)</summary>
    public string? AccentColor { get; set; }
    /// <summary>رنگ سایدبار — مثل #1b1746 (لایه سازگاری قدیمی؛ با پالت فعال بازنویسی می‌شود)</summary>
    public string? SidebarColor { get; set; }
    /// <summary>پالت فعال (فاز ۳۲) — کل نرم‌افزار (وب/ورود/PDF) از نقش‌های رنگی این پالت پیروی می‌کند؛
    /// null = بدون پالت (رنگ‌های اصلی site.css و سپس لایه قدیمی دو رنگ)</summary>
    public string? ActivePaletteId { get; set; }

    // ---------- زبان پیش‌فرض سامانه (فاز ۸۳) ----------
    /// <summary>کد زبان پیش‌فرض انتخابی ادمین در «تنظیمات سامانه» — برای کاربران بدون کوکی زبان و
    /// صفحهٔ ورود؛ روی کلید نصب اولیه (Ui:Language در hrsettings.bin) اولویت دارد. null = طبق پیکربندی نصب.</summary>
    public string? DefaultLang { get; set; }

    // ---------- نوع تقویم نمایشی (فاز ۸۶) ----------
    /// <summary>نوع تقویم «نمایشی» تاریخ‌ها در کل رابط کاربری — jalali (شمسی، پیش‌فرض)، gregorian (میلادی)،
    /// hijri (قمری). فقط نحوهٔ نمایش عوض می‌شود؛ داده‌ها و کلیدهای تاریخ (DateKey) دست‌نخورده می‌مانند.</summary>
    public string Calendar { get; set; } = "jalali";

    /// <summary>
    /// فاز ۹۶ (درخواست کاربر: «محاسبات تقویم تعطیلات درست نیست») — جبران روز تقویم قمری (‎-۲ تا ‎+۲).
    /// مبنا ام‌القری است که معمولاً یک روز جلوتر از تقویم قمری رسمی ایران/افغانستان است؛ پیش‌فرض ‎-۱ =
    /// نمایش قمری مطابق تقویم دیواری رسمی (تاریخ میلادی منهای یک روز، سپس ام‌القری). فقط «نمایش/قالب
    /// برون‌بری» را جابجا می‌کند؛ داده‌ها دست‌نخورده.
    /// </summary>
    public int HijriAdjust { get; set; } = -1;
    /// <summary>فاز ۱۱۰ — ترمیم یک‌بارهٔ جبران قمریِ ردیف‌های قدیمی انجام شده؟ علامت می‌خورد تا انتخاب صریحِ بعدی ادمین (حتی صفر = دقیقاً ام‌القری) بازنویسی نشود.</summary>
    public bool HijriAdjustMigrated { get; set; }

    // ---------- محافظ صفحه (فاز ۹۶ — درخواست کاربر) ----------
    /// <summary>محافظ صفحه (باران ماتریکس) بعد از بی‌حرکتی روشن شود؟ پیش‌فرض روشن.</summary>
    public bool ScreenSaverEnabled { get; set; } = true;
    /// <summary>دقایق بی‌حرکتی تا روشن شدن محافظ صفحه — ۱ تا ۱۲۰؛ پیش‌فرض ۵.</summary>
    public int ScreenSaverMinutes { get; set; } = 5;
    /// <summary>
    /// فاز ۹۸ (درخواست کاربر: «اسکرین‌سیور مدت‌زمان ثانیه هم داشته باشد») — ثانیه‌های بی‌حرکتی
    /// تا روشن شدن محافظ صفحه در کنار دقیقه‌ها؛ ۰ تا ۵۹؛ پیش‌فرض ۰. آستانهٔ واقعی =
    /// دقیقه‌ها × ۶۰ + ثانیه‌ها.
    /// </summary>
    public int ScreenSaverSeconds { get; set; } = 0;
    /// <summary>
    /// درخواست کاربر: «وقتی اسکرین‌سیور در حال اجراست و کاربر حرکتی انجام می‌دهد، برای برگشت به حالت
    /// عادی رمز دوباره وارد شود؛ قابل تنظیم در تنظیمات سامانه که اساساً رمز بخواهد یا نه».
    /// true = با اولین حرکت، فرم ورود رمزِ همان کاربرِ واردشده باز می‌شود (برای بیدارسازی)؛
    /// false = با اولین حرکت، محافظ بدون رمز جمع می‌شود. پیش‌فرض true.
    /// </summary>
    public bool ScreenSaverRequirePassword { get; set; } = true;

    // ---------- پشتیبان‌گیری زمان‌بندی‌شدهٔ دیتابیس (فاز ۱۳۵ — مورد ۲۰ ROADMAP-ADDITIONS) ----------
    /// <summary>پشتیبان‌گیری خودکار روزانهٔ فایل‌های دیتابیس فعال باشد؟</summary>
    public bool BackupEnabled { get; set; } = false;
    /// <summary>ساعت اجرای روزانه به‌صورت «HH:mm» (ساعت محلی سرور).</summary>
    public string BackupTime { get; set; } = "03:00";
    /// <summary>نگهداشت N نسخهٔ آخر در backups/db — بقیه خودکار حذف می‌شوند.</summary>
    public int BackupRetention { get; set; } = 7;
    /// <summary>آخرین روز اجرای موفق (yyyy-MM-dd) — برای جلوگیری از اجرای دوباره در همان روز.</summary>
    public string? BackupLastDate { get; set; }

    // ---------- ارز نمایشی (فاز ۹۱) ----------
    /// <summary>ارز نمایشی همهٔ مبالغ پولی سامانه — کد از کاتالوگ Money.Catalog (IRR پیش‌فرض).
    /// هر کشور ارز خودش را دارد؛ انتخاب اینجا روی وب/فیش/PDF/اکسل اعمال می‌شود. قالب‌بندی نمایشی است —
    /// اعداد ذخیره‌شده و محاسبات حقوق دست‌نخورده می‌مانند.</summary>
    public string Currency { get; set; } = "IRR";

    /// <summary>نرخ تبدیل «نمایشی» (پیش‌فرض ۱ = بدون تبدیل) — فقط خروجی نمایشی ضرب می‌شود؛
    /// رشته است تا ورودی آزاد ادمین امن بماند؛ در Money.Configure با InvariantCulture پارس می‌شود.</summary>
    public string? CurrencyRate { get; set; } = "1";

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// فاز ۱۰۵ (درخواست کاربر) — بسته‌های محافظ صفحه: انتقال «خودِ اسکرین‌سیور» نه فقط تنظیماتش.
/// ----------------------------------------------------------------------
/// سناریوی کاربر: «یکی یه اسکرین‌سیور نوشته و من می‌خوام اونو داشته باشم برای خودم» —
/// هر بسته = کد جاوااسکریپت + CSSِ یک محافظ صفحهٔ بصری (به‌همراه متادیتا و تنظیمات
/// همراهش) که با فایل JSON بین نصب‌ها جابه‌جا می‌شود.
///
/// امنیت (مهم): کد درون‌ریزی‌شده «کدِ غریبه» است — هرگز در قالب صفحهٔ سامانه اجرا
/// نمی‌شود؛ فقط داخل iframe سندباکس‌شده (sandbox="allow-scripts" بدون allow-same-origin)
/// با CSP سفت (connect-src 'none' و …) از مسیر /Settings/ScreensaverRuntime سرو می‌شود:
/// به کوکی/نشست/DOM سامانه دسترسی ندارد، هیچ درخواست شبکه‌ای نمی‌تواند بزند و حتی
/// بازکردن مستقیم همان آدرس هم سندباکس است (هدر CSP sandbox). درون‌ریزی هم فقط با
/// settings.manage (مدیر ارشد) و با آنتی‌فرجری و حسابرسی انجام می‌شود.
/// در هر لحظه حداکثر یک بسته «فعال» است (IsActive)؛ بدون بستهٔ فعال = محافظ داخلی (باران ماتریکس).
/// </summary>
public class SsPackage
{
    public long Id { get; set; }
    /// <summary>نام نمایشی بسته — در لیست و پیام‌ها (حداکثر ۱۰۰ نویسه در اعتبارسنجی درون‌ریزی)</summary>
    public string Name { get; set; } = "";
    /// <summary>سازندهٔ بسته (اختیاری)</summary>
    public string? Author { get; set; }
    /// <summary>نسخهٔ بسته (اختیاری)</summary>
    public string? Version { get; set; }
    /// <summary>توضیح کوتاه (اختیاری)</summary>
    public string? Description { get; set; }
    /// <summary>کد جاوااسکریپت محافظ (حداکثر ۲۰۰ هزار نویسه در اعتبارسنجی)</summary>
    public string JsCode { get; set; } = "";
    /// <summary>CSS اختیاری محافظ (حداکثر ۱۰۰ هزار نویسه در اعتبارسنجی)</summary>
    public string CssCode { get; set; } = "";
    /// <summary>تنظیمات همراه بسته (JSON ۴ فیلد محافظ) — در برون‌بری دوباره صادر می‌شود</summary>
    public string? SettingsJson { get; set; }
    /// <summary>اثر انگشت SHA-256 کل فایل بسته — برای تشخیص و ممیزی</summary>
    public string Sha256 { get; set; } = "";
    /// <summary>حجم فایل بسته (بایت)</summary>
    public long SizeBytes { get; set; }
    /// <summary>بستهٔ فعال — حداکثر یک ردیف true (بدون بستهٔ فعال = محافظ داخلی)</summary>
    public bool IsActive { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;
    public string? ImportedBy { get; set; }
}

/// <summary>
/// پالت رنگ استاندارد (فاز ۳۲) — مجموعه نقش‌های رنگی نرم‌افزار بر پایه استانداردهای UI
/// (Bootstrap/Material semantic tokens) و عرف نرم‌افزارهای کنترل تردد (ZKTeco/Hikvision/Suprema):
/// هر نقش «تخت» است و هیچ گرادیان/طیفی تولید نمی‌شود؛ سایه‌روشن‌های hover/soft هم در سرور
/// به‌صورت یک رنگ ثابت تخت محاسبه و تزریق می‌شوند. ادمین پالت‌های سفارشی خودش می‌سازد.
/// </summary>

public class ColorPalette
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>نام پالت — یکتا</summary>
    public string Name { get; set; } = "";

    // ---------- برند و کنش ----------
    /// <summary>رنگ اصلی برند — دکمه اصلی، آیتم فعال منو، تب فعال، حالت checked</summary>
    public string PrimaryColor { get; set; } = "#6366f1";
    /// <summary>رنگ لینک‌ها</summary>
    public string LinkColor { get; set; } = "#4f46e5";

    // ---------- منوی کناری ----------
    /// <summary>پس‌زمینه منوی کناری (و نوار بالای پرتال خودخدمتی)</summary>
    public string SidebarColor { get; set; } = "#1b1746";
    /// <summary>متن آیتم‌های منو</summary>
    public string SidebarTextColor { get; set; } = "#c3c6ec";

    // ---------- زمینه ----------
    /// <summary>پس‌زمینه صفحه</summary>
    public string PageBgColor { get; set; } = "#f4f5fb";
    /// <summary>پس‌زمینه کارت‌ها/فرم‌ها/جدول‌ها</summary>
    public string SurfaceColor { get; set; } = "#ffffff";

    // ---------- جدول (سطر و ستون) ----------
    /// <summary>پس‌زمینه سربرگ جدول (ستون‌ها)</summary>
    public string TableHeaderColor { get; set; } = "#f2f3fc";
    /// <summary>پس‌زمینه سطر یک‌درمیان (zebra)</summary>
    public string RowAltColor { get; set; } = "#f9f9fe";
    /// <summary>پس‌زمینه سطر در حالت هاور</summary>
    public string RowHoverColor { get; set; } = "#f4f5fe";

    // ---------- خط و متن ----------
    /// <summary>حاشیه‌ها و خطوط جداکننده</summary>
    public string BorderColor { get; set; } = "#e3e5f3";
    /// <summary>متن اصلی</summary>
    public string TextColor { get; set; } = "#1d1e3a";
    /// <summary>متن ثانویه/کم‌رنگ</summary>
    public string MutedTextColor { get; set; } = "#565b7e";

    // ---------- دلالت‌های وضعیت ----------
    /// <summary>موفق/آنلاین/مجاز</summary>
    public string SuccessColor { get; set; } = "#059669";
    /// <summary>هشدار/در انتظار</summary>
    public string WarningColor { get; set; } = "#d97706";
    /// <summary>خطا/غیرمجاز/آفلاین</summary>
    public string DangerColor { get; set; } = "#e11d48";
    /// <summary>اطلاع/خنثی</summary>
    public string InfoColor { get; set; } = "#0284c7";

    // ---------- متادیتا ----------
    /// <summary>پالت داخلی نرم‌افزار — قابل حذف/ویرایش نیست (فقط اعمال یا کپی)</summary>
    public bool IsBuiltIn { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ============================================================
// فاز ۴۰ — کنترل پارکینگ (گیت‌های پارکینگ، خودروها، مجوزها و رویدادهای ورود/خروج)
// هم‌ساختار با کنترل تردد: Zone→Gate ≈ ParkingLot→ParkingGate
// ============================================================

/// <summary>پارکینگ (محوطه) — ظرفیت کل، حق پارکینگ ماهانه و شرکت مالک؛ گیت‌ها زیرمجموعه آن هستند.</summary>

// ===================== هشدارهای امنیتی (پلتفرم — فاز ۶۱ از Access به پایه منتقل شد) =====================
public class SecurityAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "ورود غیرمجاز"; // ورود غیرمجاز | تأخیر خروج | کارت گمشده | مجوز منقضی | بیش‌ماندن مهمان | قطع دستگاه | تخلیه اضطراری
    public string Severity { get; set; } = "متوسط";     // بحرانی | بالا | متوسط | پایین
    public string Message { get; set; } = "";
    public string Source { get; set; } = "کنترل تردد";   // کنترل تردد | ارباب رجوع | حضور و غیاب | دستگاه‌های تردد
    public string? RefId { get; set; }
    public bool Resolved { get; set; }
    /// <summary>منقضی‌شده با TTL (فاز ۳۰) — از شمارش هشدارهای باز خارج می‌شود ولی سابقه حفظ می‌شود.</summary>
    public bool Expired { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

// ===================== زبان‌های پویا (فاز ۷۷ — استودیو ترجمه + پنل ادمین) =====================
/// <summary>
/// زبان رابط کاربری — «fa» مبدأ همیشه هست؛ زبان‌های دیگر از پنل ادمین (/Langs) با
/// «بستهٔ ترجمه» (خروجی TranslationStudio) افزوده می‌شوند. IsActive = در سوئیچر زبان دیده شود.
/// </summary>
public class HrLangDef
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Code { get; set; } = "";    // de | ar | tr … (لاتین، ۲ تا ۸ نویسه؛ «fa» رزرو است)
    public string Title { get; set; } = "";   // نام نمایشی — Deutsch, العربية …
    public string Dir { get; set; } = "ltr";  // ltr | rtl
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }       // زبان پیش‌فرض نصب (نمایشی — مرجع اجرایی Ui:Language است)
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>یک ترجمهٔ زبان پویا — کلید = رشتهٔ فارسیِ دقیق مبدأ (هم‌قرارداد HrLang.cs).</summary>
public class HrLangEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string LangCode { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
