using System.ComponentModel.DataAnnotations;

namespace Platform.Modules.Visitors.Domain;

// ============================================================
// لایهٔ دیتای مستقل ماژول Visitors (فاز ۶۱) — entityهای اختصاصی این ماژول
// ارتباط با ماژول‌های دیگر فقط از طریق Platform.Contracts
// ============================================================

public class Contractor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    /// <summary>شرکتِ طرف قرارداد پیمانکار (چندشرکتی).</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? ContractNo { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool Active { get; set; } = true;
    public List<Visitor> Visitors { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Visitor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string? Phone { get; set; }
    public string IdDocType { get; set; } = "کارت ملی"; // کارت ملی | گواهینامه | شناسنامه | پاسپورت
    public string Type { get; set; } = "مهمان";          // مهمان | مراجع | پیمانکار
    /// <summary>شرکت میزبان بازدیدکننده (چندشرکتی).</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string HostName { get; set; } = "";
    public string? HostDept { get; set; }
    public string Purpose { get; set; } = "";
    public string? ContractorId { get; set; }
    public Contractor? Contractor { get; set; }
    public string Status { get; set; } = "در انتظار تأیید"; // در انتظار تأیید | تأیید شده | داخل سازمان | خارج شده | رد شده
    public string? ApprovedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public VisitorCard? Card { get; set; }
    public List<VisitLog> Logs { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string FullName => $"{FirstName} {LastName}";
}

public class VisitorCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CardNo { get; set; } = "";
    public string Type { get; set; } = "QR"; // QR | بارکد | RFID
    public string VisitorId { get; set; } = "";
    public Visitor? Visitor { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.Now;
    public DateTime ValidUntil { get; set; } = DateTime.Now.AddHours(8);
    public bool Returned { get; set; }
}

public class VisitLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string VisitorId { get; set; } = "";
    public Visitor? Visitor { get; set; }
    public string Direction { get; set; } = "ورود"; // ورود | خروج
    public string? GateName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

// ===================== مخزن کارت‌های شناسایی پرسنل (فاز ۲۹) =====================

/// <summary>
/// کارت شناسایی فیزیکی پرسنل — مخزن متمرکز صدور/تخصیص/عودت/گمشدی/تعویض.
/// شماره کارت سراسری یکتاست؛ هنگام اسکن با روش «کارت»، موتور دسترسی از این مخزن به پرسنل نگاشت می‌کند.
/// </summary>
