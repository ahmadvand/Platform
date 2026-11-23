using System.ComponentModel.DataAnnotations;

namespace HrModules.Payroll.Domain;

// ============================================================
// لایهٔ دیتای مستقل ماژول Payroll (فاز ۶۱) — entityهای اختصاصی این ماژول
// ارتباط با ماژول‌های دیگر فقط از طریق Platform.Contracts
// ============================================================

public class PayrollRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>مالک لیست حقوق در محیط چندشرکتی — هر شرکت لیست مستقل خودش را می‌سازد.</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public int JYear { get; set; }
    public int JMonth { get; set; }
    public string Status { get; set; } = "پیش‌نویس"; // پیش‌نویس | نهایی
    public string? Note { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
    public int EmployeeCount { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public List<Payslip> Payslips { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class Payslip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RunId { get; set; } = "";
    public PayrollRun? Run { get; set; }
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal Allowances { get; set; }
    public decimal AbsentDeduct { get; set; }
    public decimal Insurance { get; set; }
    public decimal Tax { get; set; }
    public decimal NetPay { get; set; }
    public int WorkedMin { get; set; }
    public int OvertimeMin { get; set; }
    public int AbsentDays { get; set; }
    public string? Detail { get; set; } // JSON
    public List<PayslipLine> Lines { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ===================== موتور حقوق داینامیک (فاز ۲۴ — فرمول‌ساز چندشرکتی) =====================

/// <summary>
/// قلم حقوق — فرمول کاملاً داینامیک که هر شرکت خودش می‌سازد.
/// هدف‌گیری: همه پرسنل شرکت | یک دپارتمان | یک پست سازمانی | یک شیفت | یک پرسنل | نوع استخدام.
/// در کدهای تکراری، هدف اختصاصی‌تر (پرسنل &gt; شیفت &gt; پست &gt; دپارتمان &gt; همه) با اولویت بالاتر برنده است.
/// </summary>

public class PayItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CompanyId { get; set; } = "";
    public Company? Company { get; set; }
    public string Code { get; set; } = "";          // یکتا در هر شرکت — در فرمول‌های دیگر با قلم("کد") قابل ارجاع است
    public string Title { get; set; } = "";
    /// <summary>مزیت (درآمد) | کسور (کسر)</summary>
    public string Kind { get; set; } = "مزیت";
    /// <summary>فرمول داینامیک — مثال: پایه_محاسبه / ماه_روزها * کارکرد_روز</summary>
    public string Formula { get; set; } = "";
    /// <summary>مشمول مالیات (به درآمد مشمول مالیات اضافه می‌شود)</summary>
    public bool Taxable { get; set; }
    /// <summary>مشمول بیمه (مبنای سهم کارگر/کارفرما تا سقف روزانه)</summary>
    public bool Insurable { get; set; }
    /// <summary>مستمر = خوراک پلکان مالیات؛ غیرمستمر (اضافه‌کاری/رفاهی…) = نرخ مقطوع</summary>
    public bool Continuous { get; set; } = true;
    /// <summary>هدف‌گیری: همه | دپارتمان | پست | شیفت | پرسنل | استخدام</summary>
    public string ScopeType { get; set; } = "همه";
    /// <summary>شناسه هدف (بسته به ScopeType) یا مقدار نوع استخدام</summary>
    public string? ScopeId { get; set; }
    /// <summary>اولویت در حل کدهای تکراری — بالاتر برنده است</summary>
    public int Priority { get; set; }
    /// <summary>ترتیب محاسبه و نمایش</summary>
    public int Order { get; set; }
    /// <summary>اقلام سیستمی قابل حذف نیستند ولی فرمولشان قابل ویرایش است</summary>
    public bool IsSystem { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>پارامتر سالانه حقوق (حداقل‌ها، بن‌ها، ضرایب، سقف‌ها…) — به ازای هر شرکت و سال جلالی.
/// در فرمول‌ها با کد پارامتر قابل ارجاع‌اند؛ پلکان مالیات در StrValue به‌صورت JSON ذخیره می‌شود.</summary>

public class PayrollParameter
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CompanyId { get; set; } = "";
    public Company? Company { get; set; }
    public int JYear { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal Value { get; set; }
    /// <summary>مقدار متنی برای پارامترهای خاص — مثلاً پلکان_مالیات: [{"to":800000000,"rate":10},…]</summary>
    public string? StrValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>تنظیمات محاسبه حقوق هر شرکت — روش محاسبه (هر دو امکان موجود است، شرکت انتخاب می‌کند)
/// و اولویت منبع مقادیر تردد.</summary>

public class PayrollSetting
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CompanyId { get; set; } = "";
    public Company? Company { get; set; }
    /// <summary>ماهانه (پایه ثابت مستقل از کارکرد) | کارکردی (پایه روزشمار بر اساس کارکرد پرداختی)</summary>
    public string CalcMethod { get; set; } = "ماهانه";
    /// <summary>منبع مقادیر تردد: ۱=سطح رکورد (جمع فیلدهای آماده) | ۲=روزشمار با وضعیت‌های تأییدشده (پیش‌فرض)
    /// | ۳=روزشمار + اضافه‌کاری فقط از مجوزهای تأییدشده</summary>
    public int WorkSource { get; set; } = 2;
    /// <summary>مبنای روز ماه (۳۰ = رویه رایج حقوق)</summary>
    public int MonthDays { get; set; } = 30;
    /// <summary>ساعت پایه ماه برای تبدیل ساعتی (۱۹۲ = ۲۴ روز × ۸ ساعت)</summary>
    public int HourBase { get; set; } = 192;
    /// <summary>رند مبلغ هر خط به مضرب (۰=نزدیک‌ترین ریال، ۱۰۰۰=هزار ریال…)</summary>
    public int RoundUnit { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>خط فیش حقوقی — هر قلم محاسبه‌شده یک خط (جایگزین فیلدهای تخت؛ ستون‌های قدیمی برای سازگاری جمع می‌شوند).</summary>

public class PayslipLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PayslipId { get; set; } = "";
    public Payslip? Payslip { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>مزیت | کسور</summary>
    public string Kind { get; set; } = "مزیت";
    public decimal Amount { get; set; }
    public bool Taxable { get; set; }
    public bool Insurable { get; set; }
    public int Order { get; set; }
}

// ===================== عیدی/سنوات سالانه و تسویه حساب (فاز ۲۵) =====================

/// <summary>
/// ردیف عیدی و پایه سنوات سالانه یک پرسنل — مبلغ از فرمول‌ساز (اقلام سیستمی «عیدی_سالانه» و
/// «سنوات_سالانه» هر شرکت) با متغیر سالانه «کارکرد_از_سال_روز» محاسبه می‌شود.
/// نهایی‌شده قفل است؛ بازمحاسبه فقط پیش‌نویس‌ها را بازنویسی می‌کند.
/// </summary>

public class PayrollBonusRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CompanyId { get; set; } = "";
    public Company? Company { get; set; }
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public int JYear { get; set; }
    /// <summary>روزهای خدمت از ابتدای سال جلالی (پرداختی: حضور + مأموریت + مرخصی باحقوق + استعلاجی).</summary>
    public decimal Days { get; set; }
    /// <summary>پایه مبنا هنگام محاسبه (ریال).</summary>
    public decimal Base { get; set; }
    /// <summary>عیدی محاسبه‌شده (ریال).</summary>
    public decimal Eid { get; set; }
    /// <summary>پایه سنوات محاسبه‌شده (ریال).</summary>
    public decimal Seniority { get; set; }
    public decimal Total => Eid + Seniority;
    public string Status { get; set; } = "پیش‌نویس"; // پیش‌نویس | نهایی
    public string? FinalizedBy { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// تسویه‌حساب پرسنل — مانده مرخصی × روزانه + عیدی نسبتی + سنوات نسبتی از تاریخ
/// استخدام/ابتدای سال تا تاریخ پایان همکاری. پس از ثبت نهایی، وضعیت پرسنل «تسویه‌شده» می‌شود.
/// </summary>

public class PayrollSettlement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CompanyId { get; set; } = "";
    public Company? Company { get; set; }
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>روزهای خدمت محاسبه‌شده در سال جاری (پایه عیدی نسبتی).</summary>
    public decimal Days { get; set; }
    /// <summary>مانده مرخصی استحقاقی (روز).</summary>
    public decimal LeaveBalance { get; set; }
    /// <summary>بهای مانده مرخصی (ریال).</summary>
    public decimal LeavePay { get; set; }
    /// <summary>روزانه حقوق هنگام محاسبه (ریال) — برای نمایش برگ تسویه.</summary>
    public decimal Daily { get; set; }
    /// <summary>عیدی نسبتی سال جاری (ریال).</summary>
    public decimal Eid { get; set; }
    /// <summary>سنوات نسبتی سال جاری (ریال).</summary>
    public decimal Seniority { get; set; }
    public decimal Total => LeavePay + Eid + Seniority;
    public string Status { get; set; } = "پیش‌نویس"; // پیش‌نویس | نهایی
    public string? CreatedBy { get; set; }
    public string? FinalizedBy { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ===================== Notifications (Phase 13) =====================
