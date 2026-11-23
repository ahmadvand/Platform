namespace Platform.Modules.Visitors.Domain;

/// <summary>
/// فاز ۱۳۲ (مورد ۱۴ ROADMAP-ADDITIONS) — ردیف «مهمان‌های تاریخ‌گذشته»:
/// مهمانی که اعتبار کارتش گذشته ولی هنوز خارج نشده یا کارت را برنگردانده.
/// </summary>
public sealed record VisitorOverdueRow(
    string CardNo, string Name, string HostName, string HostDept, string Company,
    string Status, DateTime ValidUntil, bool CardReturned, bool StillInside);
