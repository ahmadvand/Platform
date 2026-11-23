namespace Platform.Modules.Parking.Domain;

/// <summary>
/// فاز ۱۳۴ (مورد ۴ ROADMAP-ADDITIONS — تکمیل تحلیلی) — یک ساعت از منحنی اشغال:
/// «داخل» = نشست‌هایی که آن ساعت را پوشش می‌دهند + ورود/خروج همان ساعت.
/// </summary>
public sealed record ParkingHourRow(int Hour, int Inside, int InCount, int OutCount);

/// <summary>
/// فاز ۱۳۴ (مورد ۵ ROADMAP-ADDITIONS) — آمار هر دوربین پلاک‌خوان در داشبورد کیفیت.
/// </summary>
public sealed record AnprCameraStat(string Camera, int Total, int Ok, decimal OkPct, decimal AvgConf);
