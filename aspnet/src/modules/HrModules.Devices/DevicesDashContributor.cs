using System.Net;
using System.Text;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Devices;

// ======================================================================
// فاز ۷۰ — مشارکت داشبورد ماژول تجهیزات و گیت‌وی
// ----------------------------------------------------------------------
// KPI «دستگاه آماربَر آنلاین» + پنل تمام‌عرض «دستگاه‌های متصل» — داده از
// قرارداد IDevicesGateway خود ماژول (sweep آفلاین‌ها هم داخل آن است).
// ماژول غایب = نه KPI، نه نوار دستگاه‌ها؛ نصب مجدد = خودکار برمی‌گردد.
// ======================================================================
public sealed class DevicesDashContributor : IDashContributor
{
    public string Module => "devices";

    public async Task<DashContribution> ContributeAsync(IServiceProvider services)
    {
        var devg = services.GetRequiredService<IDevicesGateway>();
        var d = await devg.DashboardAsync();

        // ---------- KPI: دستگاه آماربَر آنلاین ----------
        var kpis = new List<DashKpi>
        {
            new("dev-online", "🖥", "دستگاه آماربَر آنلاین",
                $"{Jalali.Num(d.Online)}<span style=\"font-size:13px;color:var(--ink-2)\"> / {Jalali.Num(d.Devices.Count)}</span>",
                "نمای زنده حراست ←", "/Devices/LiveView", "t", 40),
        };

        // ---------- پنل تمام‌عرض: دستگاه‌های متصل ----------
        var sb = new StringBuilder();
        if (d.Devices.Count == 0)
        {
            sb.Append("<div class=\"empty\">دستگاهی ثبت نشده است</div>");
        }
        else
        {
            sb.Append("<div class=\"dev-grid\">");
            foreach (var dev in d.Devices)
            {
                var isOn = dev.Online;
                var last = dev.LastPunchPerson is null
                    ? ""
                    : $"<span>· آخرین: <b>{Enc(dev.LastPunchPerson)}</b> — {Enc(dev.LastPunchTime ?? "")}</span>";
                sb.Append($"""
                    <div class="dev-card">
                        <div class="head">
                            <div>
                                <h3>{Enc(dev.Name)}</h3>
                                <div class="meta">{Enc(dev.Kind)} · {Enc(dev.Brand)}</div>
                            </div>
                            <span class="badge {(isOn ? "badge-ok" : "badge-mute")}">
                                <span class="dot {(isOn ? "dot-on" : "dot-off")}"></span>{(isOn ? "آنلاین" : "آفلاین")}
                            </span>
                        </div>
                        <div class="meta">
                            IP: <b>{Enc(dev.Ip ?? "—")}</b> · 🏢 {Enc(dev.CompanyName ?? "—")}<br />
                            📍 {Enc(dev.Location ?? "—")}<br />
                            تردد امروز: <b>{Jalali.Num(dev.TodayPunches)}</b>
                            {last}
                        </div>
                        <div class="btn-row" style="margin-top:8px">
                            <a class="btn btn-sm" href="/Devices">⚙️ تنظیمات</a>
                            <a class="btn btn-sm" href="/Devices/LiveView">📡 نمای زنده</a>
                        </div>
                    </div>
                    """);
            }
            sb.Append("</div>");
        }

        var panel = new DashPanel("dev-strip", "دستگاه‌های متصل", sb.ToString(),
            "/Devices", "مدیریت دستگاه‌ها ←", 10);

        return new DashContribution(kpis, [], [], new[] { panel });
    }

    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? "");
}
