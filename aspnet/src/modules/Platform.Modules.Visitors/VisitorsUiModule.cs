using Platform.Contracts;

namespace Platform.Modules.Visitors;

// فاز ۶۸ — معرفی خودِ ماژول ارباب رجوع به هاست
public sealed class VisitorsUiModule : IHrUiModule
{
    public string Code => "visitors";
    public string Title => "بازدیدکنندگان";
    public string Icon => "🎟";
    public string Description => "ارباب رجوع، کارت بازدید، پیمانکاران و هشدارهای امنیتی";

    public string[] Controllers => ["visitors"];

    public (string Ctrl, string Act)[] OwnedActions => [];

    public HrNavGroup[] NavGroups =>
    [
        new("کنترل تردد و حراست", 40,
        [
            new HrNavItem("/Visitors", "🎟", "ارباب رجوع", "visitors.view", "Visitors", "", Sort: 60),
            new HrNavItem("/Visitors/Overdue", "⏰", "مهمان‌های تاریخ‌گذشته", "visitors.view", "Visitors", "Overdue", Sort: 65),
            new HrNavItem("/Visitors/Duration", "⏱️", "مدت ملاقات", "visitors.view", "Visitors", "Duration", Sort: 70),
        ]),
    ];
}
