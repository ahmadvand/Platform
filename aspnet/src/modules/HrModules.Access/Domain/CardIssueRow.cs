namespace HrModules.Access.Domain;

/// <summary>
/// فاز ۱۳۲ (مورد ۶ ROADMAP-ADDITIONS) — ردیف «گزارش کارت‌های نیازمند رسیدگی».
/// دسته‌ها: منقضی و در گردش / گمشدهٔ باز / باطلِ جمع‌نشده / به‌زودی منقضی (۷ روز).
/// </summary>
public sealed record CardIssueRow(
    string Category, string CardNo, string Type,
    string Holder, string Dept, DateTime? ExpiresAt, string Status);
