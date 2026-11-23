using System.ComponentModel.DataAnnotations;

namespace HrModules.Attendance.Domain;

// ============================================================
// لایهٔ دیتای مستقل ماژول Attendance (فاز ۶۱) — entityهای اختصاصی این ماژول
// ارتباط با ماژول‌های دیگر فقط از طریق Platform.Contracts
// ============================================================

public class Shift
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>مالکیت شرکتی شیفت (محیط چندشرکتی) — null یعنی مشترک بین شرکت‌ها.</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string Type { get; set; } = "عادی"; // عادی | نوبت‌کاری | چرخشی | ۱۲ساعته | اقماری
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "16:00";
    public int BreakMinutes { get; set; } = 60;
    public string? CyclePattern { get; set; } // 14/14 | 21/7 | 28/28
    /// <summary>فاز ۱۳۵ (مورد ۱۶) — روز شروع چرخهٔ جاری؛ مبنای تقویم آینده‌نگر کارکرد/استراحت.</summary>
    public DateTime? CycleAnchor { get; set; }
    public int ToleranceLate { get; set; } = 10;
    public bool Active { get; set; } = true;
    public List<Employee> Employees { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AttendanceRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public string? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string Date { get; set; } = ""; // "2026-10-18"
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public int WorkMinutes { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public string Status { get; set; } = "حاضر"; // حاضر | غایب | مرخصی | مأموریت | تعطیل | تأخیر
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class LeaveRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public string Type { get; set; } = "استحقاقی"; // استحقاقی | استعلاجی | بدون‌حقوق | ساعتی | زایمان
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public decimal Days { get; set; } = 1;
    public int? Hours { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "در انتظار تأیید"; // در انتظار تأیید | تأیید شده | رد شده
    public string? Approver { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class Mission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public string Type { get; set; } = "داخلی"; // داخلی | خارجی
    public string Destination { get; set; } = "";
    public string? Purpose { get; set; }
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public string Status { get; set; } = "در انتظار تأیید";
    public string? Approver { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class OvertimePermit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public string Type { get; set; } = "اضافه‌کاری"; // اضافه‌کاری | کار در تعطیل | خروج غیرمجاز
    public string Date { get; set; } = "";
    public string FromTime { get; set; } = "";
    public string ToTime { get; set; } = "";
    public string? Reason { get; set; }
    public string Status { get; set; } = "در انتظار تأیید";
    public string? Approver { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class CorrectionRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public string Date { get; set; } = "";
    public DateTime? RequestedIn { get; set; }
    public DateTime? RequestedOut { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "در انتظار تأیید"; // در انتظار تأیید | تأیید شده | رد شده
    public string? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// فاز ۹۶ (درخواست کاربر) — قالب برون‌بری «رکوردهای تردد (تاریخچه)».
/// نرم‌افزارهای مختلف فرمت‌های مختلفی برای تاریخ/زمان می‌خواهند؛ ادمین برای هر مقصد
/// یک قالب می‌سازد: ترتیب ستون‌ها + قالب هر ستون تاریخ/زمان (لینوکسی linuxtimestamp،
/// شمسی، میلادی، قمری، ISO، HH:mm) + جداکننده + سطر سرستون.
/// ColumnsJson: [{"f":"<field>","fmt":"<format>","h":"<optional header override>"}]
/// </summary>
public class AttendanceExportTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>نام قالب — مثل «قالب نرم‌افزار انبار».</summary>
    public string Name { get; set; } = "";
    /// <summary>توضیح اختیاری (مقصد/یادداشت).</summary>
    public string? Note { get; set; }
    /// <summary>ستون‌ها — آرایهٔ JSON از {f: فیلد, fmt: قالب, h: سرستون اختیاری}.</summary>
    public string ColumnsJson { get; set; } = "[]";
    /// <summary>جداکنندهٔ فایل خروجی — "," ";" "\t" "|" (پیش‌فرض ",")</summary>
    public string Delimiter { get; set; } = ",";
    /// <summary>سطر سرستون در خروجی باشد؟</summary>
    public bool IncludeHeader { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
