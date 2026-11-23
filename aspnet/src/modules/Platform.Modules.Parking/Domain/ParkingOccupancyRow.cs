namespace Platform.Modules.Parking.Domain;

/// <summary>
/// فاز ۱۳۳ (مورد ۵ ROADMAP-ADDITIONS) — ردیف «اشغال ظرفیت پارکینگ» به تفکیک پارکینگ:
/// ظرفیت، مجوز فعال، خودروی داخل (آخرین رویداد پلاک = ورود)، ردِ امروز به‌دلیل تکمیل ظرفیت.
/// </summary>
public sealed record ParkingOccupancyRow(
    string LotName, string Company, int Capacity, int PermitsActive,
    int Inside, int DeniedToday, decimal FillPct);
