namespace Platform.Modules.Parking.Domain;

/// <summary>
/// فاز ۱۳۵ (مورد ۱۵ ROADMAP-ADDITIONS) — ردیف صورتحساب ماهانهٔ پارکینگ:
/// حق ماهانهٔ هر خودرو با مجوز فعال روی پارکینگ‌های دارای حق ماهانه.
/// </summary>
public sealed record ParkingBillingRow(
    string PersonnelCode, string Employee, string Company,
    string LotName, string Plate, string PermitType, decimal Fee);
