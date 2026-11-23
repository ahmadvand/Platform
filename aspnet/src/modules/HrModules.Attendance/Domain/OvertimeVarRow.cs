namespace HrModules.Attendance.Domain;

/// <summary>
/// فاز ۱۳۲ (مورد ۳ ROADMAP-ADDITIONS) — ردیف «واریانس اضافه‌کاری مجاز/واقعی».
/// مجاز = جمع مدت مجوزهای تأییدشدهٔ همان ماه؛ واقعی = جمع دقایق اضافه‌کاری
/// محاسبه‌شدهٔ موتور کارکرد؛ واریانس مثبت = کار بدون مجوز، منفی = مجوز بلااستفاده.
/// </summary>
public sealed record OvertimeVarRow(
    string Code, string Name, string Dept,
    decimal AllowedHours, decimal ActualHours, decimal VarianceHours, int PermitCount);
