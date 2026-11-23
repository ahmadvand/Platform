namespace HrModules.Attendance.Domain;

/// <summary>
/// فاز ۱۳۵ (مورد ۱۶ ROADMAP-ADDITIONS) — ردیف تقویم آینده‌نگر چرخشی:
/// هر شیفت با الگوی کار/استراحت + فهرست پرسنل و سلول‌های کارکرد/استراحت روزهای آینده.
/// </summary>
public sealed record CycleCalendarRow(
    string ShiftName, string Pattern, int People,
    List<bool> Cells, List<string> Names);
