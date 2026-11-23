using Platform.Contracts;

namespace Platform.Modules.Parking;

// فاز ۶۸ — معرفی خودِ ماژول پارکینگ به هاست
public sealed class ParkingUiModule : IHrUiModule
{
    public string Code => "parking";
    public string Title => "پارکینگ (پلاک‌خوان)";
    public string Icon => "🅿️";
    public string Description => "پارکینگ‌ها و دروازه‌ها، خودروها، مجوزهای عبور و رخدادهای ANPR";

    public string[] Controllers => ["parking", "anprwebhook"];

    public (string Ctrl, string Act)[] OwnedActions =>
    [
        ("reports", "parking"), ("reports", "parkingxlsx"), ("reports", "parkingpdf"),
    ];

    public HrNavGroup[] NavGroups =>
    [
        new("پارکینگ", 50,
        [
            new HrNavItem("/Parking", "🅿️", "کنترل پارکینگ", "parking.view", "Parking", ""),
            new HrNavItem("/Parking/Events", "🚗", "تردد وسایل نقلیه", "parking.view", "Parking", "Events", Sort: 10),
            new HrNavItem("/Parking/Occupancy", "📈", "اشغال ظرفیت", "parking.view", "Parking", "Occupancy", Sort: 11),
            new HrNavItem("/Parking/Analysis", "📊", "تحلیل اشغال", "parking.view", "Parking", "Analysis", Sort: 12),
            new HrNavItem("/Parking/Quality", "📷", "کیفیت پلاک‌خوان", "parking.view", "Parking", "Quality", Sort: 13),
            new HrNavItem("/Parking/Billing", "💰", "صورتحساب پارکینگ", "parking.view", "Parking", "Billing", Sort: 14),
        ]),
    ];
}
