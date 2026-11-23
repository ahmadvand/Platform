using Platform.Contracts;

namespace HrModules.Payroll;

// فاز ۶۸ — معرفی خودِ ماژول حقوق و دستمزد به هاست
public sealed class PayrollUiModule : IHrUiModule
{
    public string Code => "payroll";
    public string Title => "حقوق و دستمزد";
    public string Icon => "💰";
    public string Description => "فرمول‌ساز اقلام، پارامترهای سالانه، اجرای لیست حقوق، فیش حقوقی، عیدی و تسویه";

    public string[] Controllers => ["payroll"];

    public (string Ctrl, string Act)[] OwnedActions =>
    [
        ("self", "payslips"), ("self", "payslip"), ("self", "payslippdf"),
        ("ess", "payslips"), ("ess", "payslip"),
    ];

    public HrNavGroup[] NavGroups =>
    [
        new("حقوق و دستمزد", 60,
        [
            new HrNavItem("/Payroll", "💰", "لیست حقوق", "payroll.view", "Payroll", "", Sort: 10),
            new HrNavItem("/Payroll/Items", "🧮", "فرمول‌ساز اقلام", "payroll.view", "Payroll", "Items", Sort: 20),
            new HrNavItem("/Payroll/Params", "📐", "پارامترهای سالانه", "payroll.view", "Payroll", "Params", Sort: 30),
            new HrNavItem("/Payroll/Bonus", "🎁", "عیدی و تسویه", "payroll.bonus", "Payroll", "Bonus", Sort: 40),
            new HrNavItem("/Payroll/Settings", "⚙️", "تنظیمات محاسبه", "payroll.view", "Payroll", "Settings", Sort: 50),
        ]),
        new("پرتال من", 80,
        [
            new HrNavItem("/Self/Payslips", "💰", "فیش حقوقی من", "self.payslips", "Self", "Payslips", Sort: 40),
        ]),
    ];
}
