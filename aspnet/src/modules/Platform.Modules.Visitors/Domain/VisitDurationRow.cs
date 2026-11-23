namespace Platform.Modules.Visitors.Domain;

/// <summary>
/// فاز ۱۳۳ (مورد ۷ ROADMAP-ADDITIONS) — ردیف «مدت ملاقات مهمان»:
/// جفت‌کردن لاگ ورود/خروج هر بازدیدکننده و محاسبهٔ مدت؛ خروجِ ثبت‌نشده = ملاقات در جریان.
/// </summary>
public sealed record VisitDurationRow(
    string Name, string HostName, string Company,
    DateTime Entry, DateTime? Exit, int Minutes, bool Ongoing);
