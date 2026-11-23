using System.ComponentModel.DataAnnotations;

namespace HrModules.Access.Domain;

// ============================================================
// لایهٔ دیتای مستقل ماژول Access (فاز ۶۱) — entityهای اختصاصی این ماژول
// ارتباط با ماژول‌های دیگر فقط از طریق Platform.Contracts
// ============================================================

public class Zone
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    /// <summary>شرکت مالک منطقه (چندشرکتی) — گیت‌های منطقه باید همین شرکت را داشته باشند.</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string RiskLevel { get; set; } = "عادی"; // عادی | حساس | فوق‌حساس
    public string? Description { get; set; }
    /// <summary>آنت‌ی‌پس‌بک (فاز ۱۷): ورود دوباره بدون خروج قبلی از همین منطقه رد می‌شود.</summary>
    public bool AntiPassback { get; set; }
    /// <summary>احراز دومرحله‌ای اختیاری (فاز ۳۰) — پیش‌فرض خاموش؛ فقط برای همین منطقه و فقط اگر مدیر صریحاً فعال کند.
    /// عبور نیازمند دو عامل متمایز (کارت + اثر انگشت/چهره) در بازه زمانی زیر است.</summary>
    public bool TwoFactor { get; set; }
    /// <summary>حداکثر فاصله مجاز بین دو عامل (ثانیه) — پیش‌فرض ۳۰.</summary>
    public int TwoFactorWindowSec { get; set; } = 30;
    public List<Gate> Gates { get; set; } = new();
    public List<AccessPermission> AccessPerms { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Gate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    /// <summary>شرکت مالک گیت (چندشرکتی) — همیشه با شرکت منطقه هم‌گام است.</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string ZoneId { get; set; } = "";
    public Zone? Zone { get; set; }
    public string? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string Type { get; set; } = "درب برقی"; // گیت میله‌ای | راهبند | درب برقی | ورودی‌گردان
    public string Direction { get; set; } = "دوطرفه"; // ورودی | خروجی | دوطرفه
    public string Status { get; set; } = "فعال"; // فعال | غیرفعال | خراب
    /// <summary>حالت کنترلی درب (فاز ۲۹): نرمال = تصمیم موتور دسترسی | باز دائم = عبور آزاد (تخلیه/سرویس) | قفل دائم = مسدود کامل.</summary>
    public string DoorState { get; set; } = "نرمال";
    /// <summary>برنامه زمانی گیت (فاز ۱۷) — روزهای مجاز به‌صورت ایندکس روز هفته (شنبه=0 … جمعه=6)، خالی = همه روزها.</summary>
    public string? ScheduleDays { get; set; }
    /// <summary>ساعت شروع مجاز ("07:30") — با ScheduleTo خالی = بدون محدودیت ساعت.</summary>
    public string? ScheduleFrom { get; set; }
    /// <summary>ساعت پایان مجاز ("18:00").</summary>
    public string? ScheduleTo { get; set; }
    /// <summary>
    /// فاز ۱۳۱ (مورد ۱۲ ROADMAP-ADDITIONS): اسکن مجاز این گیت به‌عنوان تردد حضور و غیاب هم ثبت شود؟
    /// با دروازهٔ قراردادی IAttendanceGateway.UpsertPunchAsync — اولین اسکن روز=ورود، آخرین=خروج
    /// (همان موتور AttCalc که دستگاه‌های حضور استفاده می‌کنند)؛ برای پرسنلی که فقط از گیت رد می‌شوند.
    /// </summary>
    public bool FeedsAttendance { get; set; }
    /// <summary>مختصات روی نقشه سایت (فاز ۳۰) — درصدی از عرض/ارتفاع بوم؛ null = جای‌گذاری نشده.</summary>
    public double? MapX { get; set; }
    public double? MapY { get; set; }
    public List<AccessEvent> AccessEvents { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AccessPermission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string? VisitorId { get; set; }
    public string ZoneId { get; set; } = "";
    public Zone? Zone { get; set; }
    public string Type { get; set; } = "دائم"; // دائم | موقت
    public DateTime ValidFrom { get; set; } = DateTime.Now;
    public DateTime? ValidUntil { get; set; }
    public string? Days { get; set; }    // "0,1,2,3,4"
    public string? TimeFrom { get; set; }
    public string? TimeUntil { get; set; }
    public string Status { get; set; } = "فعال"; // فعال | منقضی | لغوشده
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class AccessEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string? VisitorId { get; set; }
    public string GateId { get; set; } = "";
    public Gate? Gate { get; set; }
    public string Direction { get; set; } = "ورود"; // ورود | خروج
    public string Method { get; set; } = "کارت";     // کارت | اثر انگشت | چهره | QR | RFID
    public string Result { get; set; } = "مجاز";     // مجاز | غیرمجاز
    public string? Reason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class Card
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>شماره سریال چاپ‌شده روی کارت — سراسری یکتا (نگاشت دستگاه هم از همین شماره است).</summary>
    public string CardNo { get; set; } = "";
    public string? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Type { get; set; } = "Mifare";   // Mifare | UHF | QR | RFID
    /// <summary>آزاد | فعال | موقت | گمشده | باطل</summary>
    public string Status { get; set; } = "آزاد";
    public DateTime IssuedAt { get; set; } = DateTime.Now;
    /// <summary>تاریخ انقضا — null = بدون محدودیت (کارت دائم).</summary>
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}


// ===================== Auth & RBAC =====================

public class AccessGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>شرکت مالک گروه دسترسی (چندشرکتی).</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string? Note { get; set; }
    public List<AccessGroupMember> Members { get; set; } = new();
    public List<AccessGroupGate> Gates { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>عضویت پرسنل در گروه دسترسی.</summary>

public class AccessGroupMember
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string GroupId { get; set; } = "";
    public AccessGroup? Group { get; set; }
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>گیت مجاز برای گروه + پنجره زمانی اختصاصی (خالی = بدون محدودیت).
/// روزها: ایندکس هفته ایرانی (شنبه=0 … جمعه=6) با کاما؛ خالی = همه روزها.</summary>

public class AccessGroupGate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string GroupId { get; set; } = "";
    public AccessGroup? Group { get; set; }
    public string GateId { get; set; } = "";
    public Gate? Gate { get; set; }
    public string? Days { get; set; }
    public string? TimeFrom { get; set; }
    public string? TimeUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>تنظیمات برندینگ و ظاهر سامانه (تک‌ردیفی، فاز ۳۱) — اطلاعات شرکت نصب‌شده،
/// لوگو و پالت رنگ که ادمین از «تنظیمات سامانه» تعیین می‌کند و کل نرم‌افزار از آن استفاده می‌کند.
/// نسخه و تولیدکننده از attributes اسمبلی (تزریق‌شده در زمان build) خوانده می‌شود — اینجا ذخیره نمی‌شوند.</summary>
