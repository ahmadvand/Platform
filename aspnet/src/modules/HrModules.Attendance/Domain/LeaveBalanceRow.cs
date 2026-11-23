namespace HrModules.Attendance.Domain;

/// <summary>
/// فاز ۱۳۱ (مورد ۸ ROADMAP-ADDITIONS) — ردیف «صورت‌وضعیت ماندهٔ مرخصی».
/// ماندهٔ ذخیره‌شدهٔ پرسنل (Ent/Used) در کنار مصرفِ محاسبه‌شده از درخواست‌های
/// تأییدشدهٔ سامانه (Approved) تا رانش مانده دیده شود.
/// </summary>
public sealed record LeaveBalanceRow(
    string Code, string Name, string Dept, string Status,
    decimal AnnualEnt, decimal AnnualUsed, decimal AnnualApproved,
    decimal SickEnt, decimal SickUsed, decimal SickApproved);
