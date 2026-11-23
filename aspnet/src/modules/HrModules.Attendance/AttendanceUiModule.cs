using Platform.Contracts;

namespace HrModules.Attendance;

// ======================================================================
// فاز ۶۸ — معرفی خودِ ماژول حضور و غیاب به هاست
// مانیفست کنترلرها + مالکیت اکشن‌های چند-ماژولی (پرتال من/ESS/خروجی) + آیتم‌های منو
// — همه از ModuleCatalog/ActMap قدیمی هاست به «خود ماژول» منتقل شده است.
// ======================================================================
public sealed class AttendanceUiModule : IHrUiModule
{
    public string Code => "attendance";
    public string Title => "حضور و غیاب";
    public string Icon => "🕒";
    public string Description => "شیفت‌ها و نوبت‌کاری، تردد روزانه، درخواست‌های مرخصی/ماموریت/اضافه‌کار و اصلاحات و موتور محاسبهٔ کارکرد";

    public string[] Controllers => ["attendance", "shifts", "requests"];

    public (string Ctrl, string Act)[] OwnedActions =>
    [
        // پرتال من
        ("self", "requests"), ("self", "createleave"), ("self", "createmission"),
        ("self", "createcorrection"), ("self", "attendance"),
        // پرتال پرسنلی (ESS)
        ("ess", "requests"), ("ess", "attendance"),
        // خروجی اکسل تردد
        ("export", "attendance"),
    ];

    public HrNavGroup[] NavGroups =>
    [
        new("منابع انسانی", 20,
        [
            new HrNavItem("/Shifts", "🔁", "شیفت‌ها و نوبت‌کاری", "shifts.view", "Shifts", "", Sort: 30),
            new HrNavItem("/Shifts/CycleCalendar", "📅", "تقویم چرخشی", "shifts.view", "Shifts", "CycleCalendar", Sort: 31), // فاز ۱۳۵ — مورد ۱۶
        ]),
        new("حضور و غیاب", 30,
        [
            new HrNavItem("/Attendance", "🕒", "تردد روزانه", "attendance.view", "Attendance", ""),
            new HrNavItem("/Attendance/Exports", "📤", "قالب برون‌بری تردد", "attendance.view", "Attendance", "Exports", Sort: 10),
            new HrNavItem("/Attendance/LeaveBalances", "🧮", "ماندهٔ مرخصی", "attendance.view", "Attendance", "LeaveBalances", Sort: 15),
            new HrNavItem("/Attendance/MissedPunch", "🕳️", "پانچ جامانده", "attendance.view", "Attendance", "MissedPunch", Sort: 16),
            new HrNavItem("/Attendance/Lateness", "⏱️", "تأخیر ماهانه", "attendance.view", "Attendance", "Lateness", Sort: 17),
            new HrNavItem("/Attendance/OvertimeVar", "📊", "واریانس اضافه‌کاری", "attendance.view", "Attendance", "OvertimeVar", Sort: 18),
            new HrNavItem("/Requests", "📋", "درخواست‌ها", "attendance.view", "Requests", ""),
            new HrNavItem("/Approvals", "⛓️", "گردش‌کار تأیید", "attendance.approve", "Approvals", "", Sort: 19),
        ]),
        new("پرتال من", 80,
        [
            new HrNavItem("/Self/Requests", "📨", "درخواست‌های من", "self.requests", "Self", "Requests", Sort: 20),
            new HrNavItem("/Self/Attendance", "🕒", "کارکرد من", "self.view", "Self", "Attendance", Sort: 30),
        ]),
    ];
}
