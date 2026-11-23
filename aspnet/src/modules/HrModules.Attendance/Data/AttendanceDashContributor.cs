using System.Text.Json;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Attendance.Data;

// ======================================================================
// فاز ۷۰ — مشارکت داشبورد ماژول حضور و غیاب
// ----------------------------------------------------------------------
// KPIهای حاضران/تأخیر/غایب/درخواست‌های در انتظار + نمودار روند ۷ روز اخیر —
// همه از قرارداد IAttendanceGateway خود ماژول. ماژول غایب = هیچ‌کدام در
// داشبورد نیست؛ نصب مجدد = خودکار برمی‌گردد (داشبورد داده‌محور).
// زیرنویس «از X پرسنل فعال» از قرارداد org می‌آید (ماژول غایب = ۰ مثل قبل).
// ======================================================================
public sealed class AttendanceDashContributor : IDashContributor
{
    public string Module => "attendance";

    public async Task<DashContribution> ContributeAsync(IServiceProvider services)
    {
        var att = services.GetRequiredService<IAttendanceGateway>();
        var today = Jalali.DateKey(DateTime.Now);
        var d = await att.DashboardAsync(today);

        // زیرنویس حاضران — پرسنل فعال از پلاگین org (قرارداد؛ هر خطا = بدون زیرنویس عددی)
        var active = 0;
        try { active = await services.GetRequiredService<IPeopleDirectory>().ActiveCountAsync(); }
        catch { /* org غایب/خطا — صفر */ }

        var kpis = new List<DashKpi>
        {
            new("att-present", "👥", "حاضران امروز", Jalali.Num(d.Present),
                $"✓ از {Jalali.Num(active)} پرسنل فعال — {Jalali.Num(d.OnLeave)} مرخصی", null, "t", 10),
            new("att-late", "⏰", "تأخیر امروز", Jalali.Num(d.Late),
                "⏱ بر پایه رکوردهای تردد", null, "o", 20),
            new("att-absent", "🚫", "غایب امروز", Jalali.Num(d.Absent),
                "⚑ نیازمند پیگیری", null, "r", 30),
            new("att-pending", "📋", "درخواست در انتظار تأیید", Jalali.Num(d.PendingRequests),
                "بررسی درخواست‌ها ←", "/Requests", "p", 60),
        };

        // ---------- نمودار میله‌ای: روند حضور ۷ روز اخیر ----------
        var points = d.Week.Select(p =>
        {
            var dt = Jalali.ParseDateKey(p.DateKey);
            var j = Jalali.ToJalali(dt);
            return new
            {
                label = $"{Jalali.ToPersianDigits(j.D)} {Jalali.MonthName(j.M)}",
                p.Present, p.Late, p.Absent,
            };
        }).ToList();
        var data = JsonSerializer.Serialize(new
        {
            labels = points.Select(p => p.label),
            datasets = new object[]
            {
                new { label = "حاضر", data = points.Select(p => p.Present), backgroundColor = "#14b8a6", borderRadius = 6 },
                new { label = "تأخیر", data = points.Select(p => p.Late), backgroundColor = "#f59e0b", borderRadius = 6 },
                new { label = "غایب", data = points.Select(p => p.Absent), backgroundColor = "#ef4444", borderRadius = 6 },
            },
        });
        var options = JsonSerializer.Serialize(new
        {
            responsive = true,
            maintainAspectRatio = false,
            scales = new
            {
                x = new { stacked = true },
                y = new { stacked = true, ticks = new { precision = 0 } },
            },
            plugins = new { legend = new { position = "bottom", rtl = true } },
        });
        var chart = new DashChart("att-week", "bar", "روند حضور ۷ روز اخیر", data,
            "بر پایه رکوردهای تردد", options, 10);

        return new DashContribution(kpis, new[] { chart }, [], []);
    }
}
