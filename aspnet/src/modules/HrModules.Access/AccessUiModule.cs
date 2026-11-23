using Platform.Contracts;

namespace HrModules.Access;

// فاز ۶۸ — معرفی خودِ ماژول کنترل تردد به هاست
public sealed class AccessUiModule : IHrUiModule
{
    public string Code => "access";
    public string Title => "کنترل تردد";
    public string Icon => "🚪";
    public string Description => "مناطق و گیت‌ها، مجوزهای عبور، گروه‌های دسترسی و کارت‌های شناسایی";

    public string[] Controllers => ["access", "cards"];

    public (string Ctrl, string Act)[] OwnedActions => [];

    public HrNavGroup[] NavGroups =>
    [
        new("کنترل تردد و حراست", 40,
        [
            new HrNavItem("/Access", "🚪", "مناطق و گیت‌ها", "access.view", "Access", "", Sort: 40),
            new HrNavItem("/Cards", "💳", "کارت‌های شناسایی", "cards.view", "Cards", "", Sort: 50),
            new HrNavItem("/Cards/Report", "🪪", "کارت‌های نیازمند رسیدگی", "cards.view", "Cards", "Report", Sort: 55),
        ]),
    ];
}
