namespace HrModules.Attendance.Domain;

using System.ComponentModel.DataAnnotations;

// ============================================================
// فاز ۱۳۵ (مورد ۹ ROADMAP-ADDITIONS) — گردش‌کار تأیید چندمرحله‌ای + جانشین‌پذیری
// هر گردش‌کار چند مرحله دارد؛ هر مرحله با یک «مجوز» تعریف می‌شود. تصمیم‌گیرهای
// ماژول (مرخصی/مأموریت/مجوز اضافه‌کاری/اصلاح تردد) از ApprovalEngine عبور
// می‌کنند: تصمیمِ هر مرحله ثبت و به مرحلهٔ بعد می‌رود؛ فقط تصمیمِ آخرین مرحله
// اثر اصلی (تأیید نهایی درخواست) را اعمال می‌کند — پس منطق موجود دست‌نخورده می‌ماند.
// جانشین‌پذیری: کاربرِ دارندهٔ مجوز می‌تواند برای بازه‌ای کوتاه، تأیید همان مجوز
// را به کاربر دیگری بسپارد (ApprovalDelegation) — در حالت مرخصی/سفر رسمی.
// ============================================================

/// <summary>گردش‌کار تأیید — برای یک نوع درخواست؛ CompanyId فقط برای جریان شرکتی (فعلاً جریان مشترک استفاده می‌شود).</summary>
public class ApprovalFlow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>نوع درخواست: اصلاح تردد | مرخصی | مأموریت | مجوز اضافه‌کاری</summary>
    public string ReqType { get; set; } = "مرخصی";
    public string? CompanyId { get; set; }
    public bool Active { get; set; } = true;
    public List<ApprovalFlowStep> Steps { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>مرحلهٔ گردش‌کار — ترتیب + مجوزِ مجاز به تأیید این مرحله.</summary>
public class ApprovalFlowStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FlowId { get; set; } = "";
    public ApprovalFlow? Flow { get; set; }
    public int Order { get; set; } // ۱ = اولین مرحله
    public string Perm { get; set; } = "attendance.approve";
    public string Title { get; set; } = ""; // مثلاً «سرشیفت» / «حراست» / «مدیر منابع انسانی»
}

/// <summary>رونوشت گردش‌کار برای یک درخواست مشخص — از شروع تا تأیید نهایی/رد.</summary>
public class ApprovalInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FlowId { get; set; } = "";
    public ApprovalFlow? Flow { get; set; }
    public string ReqType { get; set; } = "";
    public string ReqId { get; set; } = "";
    public string Title { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    /// <summary>شمارهٔ مرحلهٔ جاری (۱-مبنا)</summary>
    public int CurrentStep { get; set; } = 1;
    /// <summary>در گردش | تأیید نهایی | رد شده</summary>
    public string Status { get; set; } = "در گردش";
    public List<ApprovalAction> Actions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>تصمیم ثبت‌شده در یک مرحله از گردش‌کار.</summary>
public class ApprovalAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string InstanceId { get; set; } = "";
    public ApprovalInstance? Instance { get; set; }
    public int StepOrder { get; set; }
    public string StepTitle { get; set; } = "";
    public string ByUser { get; set; } = "";
    /// <summary>تأیید | رد</summary>
    public string Decision { get; set; } = "تأیید";
    public string? Note { get; set; }
    public DateTime At { get; set; } = DateTime.Now;
}

/// <summary>جانشینی — سپردن موقتِ تأییدِ یک مجوز به کاربر دیگر در بازهٔ مشخص.</summary>
public class ApprovalDelegation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>نام کامل سپردهنده (شناسهٔ کاربر در سامانه)</summary>
    public string FromUser { get; set; } = "";
    /// <summary>نام کامل جانشین</summary>
    public string ToUser { get; set; } = "";
    /// <summary>مجوزی که سپرده می‌شود — مراحلِ دارای همین مجوز به جانشین باز می‌شود.</summary>
    public string Perm { get; set; } = "attendance.approve";
    public DateTime FromDate { get; set; } = DateTime.Now;
    public DateTime ToDate { get; set; } = DateTime.Now.AddDays(7);
    public bool Revoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
