using System.Net;
using System.Text.Json;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Access.Data;

// ======================================================================
// فاز ۷۰ — مشارکت داشبورد ماژول کنترل تردد
// ----------------------------------------------------------------------
// داشبورد از ماژول‌ها خوانده می‌شود (خواستهٔ کاربر): این ماژول خودش KPI/نمودار/
// جدول داشبوردش را می‌سازد؛ هاست فقط تجمیع می‌کند. ماژول غایب = این کلاس هم
// لود نمی‌شود = هیچ ویجتی از کنترل تردد در داشبورد نیست؛ ماژول تازه‌نصب =
// خودکار ظاهر می‌شود. سوراخ‌شدن محتوا (Encode نام‌ها از بانک) همین‌جاست.
// ======================================================================
public sealed class AccessDashContributor : IDashContributor
{
    public string Module => "access";

    public async Task<DashContribution> ContributeAsync(IServiceProvider services)
    {
        var acc = services.GetRequiredService<IAccessGateway>();
        var d = await acc.DashboardAsync();

        // ---------- KPI: تردد امروز (گیت‌ها) ----------
        var kpis = new List<DashKpi>
        {
            new("acc-today", "🚪", "تردد امروز (گیت‌ها)", Jalali.Num(d.TodayEvents),
                "رویدادهای کنترل تردد ←", "/Access?tab=events", "b", 50),
        };

        // ---------- نمودار حلقه‌ای: توزیع منطقه‌ای رویدادهای امروز ----------
        var charts = new List<DashChart>();
        if (d.ZoneDist.Count > 0)
        {
            var data = JsonSerializer.Serialize(new
            {
                labels = d.ZoneDist.Select(z => z.Zone),
                datasets = new[]
                {
                    new
                    {
                        data = d.ZoneDist.Select(z => z.Count),
                        backgroundColor = new[] { "#14b8a6", "#3b82f6", "#8b5cf6", "#f59e0b", "#94a3b8", "#ef4444" },
                        borderWidth = 0,
                    },
                },
            });
            var options = JsonSerializer.Serialize(new
            {
                responsive = true,
                maintainAspectRatio = false,
                cutout = "62%",
                plugins = new { legend = new { position = "bottom", rtl = true } },
            });
            charts.Add(new DashChart("acc-zones", "doughnut", "تردد گیت‌ها بر پایه منطقه (امروز)",
                data, null, options, 20));
        }

        // ---------- جدول: آخرین رویدادهای کنترل تردد ----------
        var rows = new List<IReadOnlyList<string>>();
        if (d.RecentEvents.Count > 0)
        {
            // نام بازدیدکننده‌ها ∈ ماژول ارباب‌رجوع (قرارداد؛ غایب = نام‌ها «—» می‌ماند)
            var visitorIds = d.RecentVisitorIds.Where(i => i != null).Select(i => i!).Distinct().ToList();
            var visitorNames = new Dictionary<string, string>();
            if (visitorIds.Count > 0)
            {
                var vis = services.GetService<IVisitorsGateway>();
                if (vis is not null)
                    visitorNames = await vis.FullNamesAsync(visitorIds);
            }

            foreach (dynamic e in d.RecentEvents)
            {
                string? person = (string?)e.EmployeeName;
                if (string.IsNullOrWhiteSpace(person))
                {
                    string? vid = (string?)e.VisitorId;
                    person = vid is not null && visitorNames.TryGetValue(vid, out var vn) && !string.IsNullOrWhiteSpace(vn)
                        ? vn : null;
                }
                var result = (string)e.Result;
                var badge = result == "مجاز" ? "badge-ok" : "badge-err";
                rows.Add(
                [
                    WebUtility.HtmlEncode(Jalali.FmtTime((DateTime)e.Timestamp)),
                    WebUtility.HtmlEncode(person ?? "—"),
                    WebUtility.HtmlEncode((string)e.Gate),
                    WebUtility.HtmlEncode((string)e.Direction),
                    WebUtility.HtmlEncode((string)e.Method),
                    $"""<span class="badge {badge}">{WebUtility.HtmlEncode(result)}</span>""",
                ]);
            }
        }
        var eventsTable = new DashTable("acc-events", "آخرین رویدادهای کنترل تردد",
            new[] { "زمان", "شخص", "گیت", "جهت", "روش", "نتیجه" }, rows,
            "/Access?tab=events", "همه →", "رویدادی ثبت نشده است", 10);

        return new DashContribution(kpis, charts, new[] { eventsTable }, []);
    }
}
