using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Modules.Visitors.Data;

// ======================================================================
// فاز ۷۰ — مشارکت داشبورد ماژول ارباب‌رجوع
// ----------------------------------------------------------------------
// KPI «مهمان داخل سازمان» از قرارداد IVisitorsGateway خود ماژول؛
// ماژول غایب = این KPI هم در داشبورد نیست.
// ======================================================================
public sealed class VisitorsDashContributor : IDashContributor
{
    public string Module => "visitors";

    public async Task<DashContribution> ContributeAsync(IServiceProvider services)
    {
        var vis = services.GetRequiredService<IVisitorsGateway>();
        var inside = await vis.InsideCountAsync();

        var kpis = new List<DashKpi>
        {
            new("vis-inside", "🪪", "مهمان داخل سازمان", Jalali.Num(inside),
                "ارباب رجوع ←", "/Visitors", "p", 80),
        };

        return new DashContribution(kpis, [], [], []);
    }
}
