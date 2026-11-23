namespace HrModules.Attendance.Domain;

/// <summary>
/// فاز ۱۳۱ (مورد ۱ ROADMAP-ADDITIONS) — ردیف «گزارش تأخیر ماهانه».
/// از LateMinutes رکوردهای تردد همان ماه؛ TotalMin = جمع دقیقه‌ها، MaxMin = بدترین روز.
/// </summary>
public sealed record LatenessRow(
    string Code, string Name, string Dept,
    int LateDays, int TotalMin, int MaxMin, decimal AvgMin);
