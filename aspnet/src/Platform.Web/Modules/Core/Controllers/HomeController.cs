using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

public class HomeController : MvcBase
{
    public HomeController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    // ---------- رکوردهای سبک داشبورد (فاز ۶۷ — امضای امن؛ بدون نوع ماژول) ----------
    public sealed record AttDashKpi(int Present, int Late, int Absent, int OnLeave, int Pending, List<object> Week);
    public sealed record AccessDashKpi(int TodayEvents, List<object> ZoneDist, List<HrAccessRecentEvent> RecentEvents, List<string?> RecentVisitorIds);
    public sealed record VisitorsDashKpi(int Inside);

    // فاز ۵۴ — صفحهٔ دوستانهٔ «ماژول غیرفعال است» (مقصد ریدایرکت ModuleGate)
    [HttpGet]
    public IActionResult ModuleDisabled(string? code)
    {
        if (Sess is null) return Redirect("/Auth/Login");
        ViewBag.Module = Platform.Web.Modules.ModuleCatalog.Find(code)
            // فاز ۶۹ — ماژول حذف‌شده از «حافظهٔ مانیفست» (داده، نه کد هاست)
            ?? Platform.Web.Modules.ModuleManifestStore.Find(code);
        // فاز ۵۷ — تمایز «خاموش» از «اصلاً در این بسته نصب نیست» (بیلد مشتری بدون ماژول)
        ViewBag.Installed = Platform.Web.Modules.HrBuildModules.Installed(code);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // گیت دستی: کارمندِ فقط-پرتالی به‌جای صفحه «دسترسی کافی ندارید» مستقیم به پرتال من می‌رود
        if (Sess is null) return Redirect("/Auth/Login");
        if (!Sess.Has("dashboard.view"))
        {
            if (Sess.Has("self.view")) return Redirect("/Self");
            return Redirect("/Auth/Denied?perm=dashboard.view");
        }

        // فاز ۶۹ — پرسنل/تجهیزات ∈ پلاگین‌های org/devices (قرارداد؛ غایب = صفر/خالی).
        // sweep آفلاین دستگاه‌ها هم داخل قرارداد دروازهٔ تجهیزات اجرا می‌شود.
        var people = HttpContext.RequestServices.GetRequiredService<IPeopleDirectory>();
        var devg = HttpContext.RequestServices.GetRequiredService<IDevicesGateway>();
        var today = Jalali.DateKey(DateTime.Now);

        // ---------- بخش‌های ماژولی داشبورد (فاز ۶۷) ----------
        var att = await AttDashSafeAsync(Db, today);
        var acc = await AccessDashSafeAsync(Db);
        var vis = await VisitorsInsideSafeAsync(Db);
        var dev = await devg.DashboardAsync();

        var openAlerts = await Db.SecurityAlerts.CountAsync(a => !a.Resolved);
        var activeEmployees = await people.ActiveCountAsync();
        var empTypes = await people.EmploymentTypeCountsAsync();

        // ---------- فاز ۱۲۵ — پیشخوان یکپارچه (طرح Figma صفحهٔ ۶) ----------
        // کارکرد ثبت‌شدهٔ امروز + رویداد پارکینگ امروز + وضعیت مجوز + شرکت‌ها + دستگاه‌های آفلاین؛
        // هر شاخص ماژولی با گارد «قرارداد اختیاری» — DLL غایب = شاخص صفر، بدون استثنا.
        int workMinutes = 0;
        try
        {
            var attSrc = HttpContext.RequestServices.GetService<IAttendanceReportSource>();
            if (attSrc is not null)
                workMinutes = (await attSrc.EmployeeSummaryAsync(null, today, today)).Sum(r => r.WorkMinutes);
        }
        catch { /* ماژول حضور غایب یا خطای منبع — شاخص صفر می‌ماند */ }
        int parkingToday = 0;
        try
        {
            var psrc = HttpContext.RequestServices.GetService<IParkingReportSource>();
            if (psrc is not null)
                parkingToday = (await psrc.ParkingEventsAsync(today, today, null, null, null, null, null, 2000)).Count;
        }
        catch { /* ماژول پارکینگ غایب — شاخص صفر می‌ماند */ }
        var lic = HttpContext.RequestServices.GetService<LicenseService>()?.Current;
        var companies = await Db.Companies.CountAsync();

        // ---------- آخرین رویدادها و هشدارها ----------
        // نام بازدیدکننده‌ها از قرارداد ماژول ارباب‌رجوع؛ بدون ماژول، نام‌ها خالی
        var recentEventIds = acc.RecentVisitorIds;
        var visitors = HttpContext.RequestServices.GetService<IVisitorsGateway>();
        ViewBag.VisitorNames = (visitors is not null && recentEventIds.Any(i => i != null))
            ? await visitors.FullNamesAsync(recentEventIds.Where(i => i != null).Select(i => i!).ToList())
            : new Dictionary<string, string>();
        // فاز ۷۵ — نام پرسنلِ رویدادها از قرارداد IPeopleDirectory (بدون وابستگی به نوع ماژول)
        var empIds = acc.RecentEvents.Select(e => e.EmployeeId).Where(i => !string.IsNullOrEmpty(i)).Select(i => i!).Distinct().ToList();
        var people2 = HttpContext.RequestServices.GetRequiredService<IPeopleDirectory>();
        var briefs = empIds.Count > 0 ? await people2.BriefsAsync(empIds) : new Dictionary<string, HrPersonBrief>();
        ViewBag.EmployeeNames = briefs.ToDictionary(kv => kv.Key, kv => kv.Value.FullName);

        ViewBag.Kpi = new
        {
            present = att.Present, late = att.Late, absent = att.Absent, onLeave = att.OnLeave,
            pending = att.Pending, onlineDevices = dev.Online, totalDevices = dev.Total,
            todayEvents = acc.TodayEvents, openAlerts, visitorsInside = vis, activeEmployees,
        };
        ViewBag.Week = JsonOf(att.Week);
        ViewBag.ZoneDist = JsonOf(acc.ZoneDist);
        ViewBag.EmpTypes = JsonOf(empTypes.Select(kv => new { Type = kv.Key, Count = kv.Value }).ToList());
        ViewBag.RecentEvents = acc.RecentEvents;
        ViewBag.RecentAlerts = await Db.SecurityAlerts
            .Where(a => !a.Resolved)
            .OrderByDescending(a => a.CreatedAt).Take(6).ToListAsync();
        ViewBag.DevList = dev.Devices;
        ViewBag.DevOnline = dev.Online;
        // فاز ۱۲۵ — داده‌های پیشخوان یکپارچه
        ViewBag.WorkMinutes = workMinutes;
        ViewBag.ParkingToday = parkingToday;
        ViewBag.Lic = lic;
        ViewBag.Companies = companies;
        ViewBag.OfflineDevices = dev.Devices.Where(d => !d.Online).Select(d => d.Name).ToList();
        return View();
    }

    [HttpGet("Home/Error")]
    public IActionResult Error()
    {
        ViewBag.Code = Response.StatusCode;
        return View();
    }

    /// <summary>
    /// جستجوی سراسری نوار بالایی (فاز ۴۵) — پرسنل، دستگاه‌ها، خودروهای پارکینگ و کاربران.
    /// فاز ۶۹ — پرسنل/دستگاه‌ها از قراردادها (org/devices)؛ نتایج هر بخش با گارد
    /// دسترسی همان ماژول برمی‌گردد؛ پاسخ JSON برای دراپ‌داون جستجو.
    /// </summary>
    [HttpGet("/Home/Search")]
    public async Task<IActionResult> Search(string? q)
    {
        if (Sess is null) return Json(new { groups = Array.Empty<object>() });
        q = (q ?? "").Trim();
        if (q.Length < 2) return Json(new { groups = Array.Empty<object>() });
        var sess = Sess;
        var groups = new List<object>();

        // پرسنل — پلاگین org (قرارداد)
        if (sess.Has("employees.view"))
        {
            var people = HttpContext.RequestServices.GetRequiredService<IPeopleDirectory>();
            var emps = await people.SearchAsync(q, 6);
            if (emps.Count > 0)
                groups.Add(new { title = "پرسنل", icon = "👤", items = emps.Select(e => new { text = HomeSearchFmt.ToPersianDigitsIn(e.Title), href = e.Href }) });
        }
        // دستگاه‌ها — پلاگین devices (قرارداد)
        if (sess.Has("devices.view"))
        {
            var devg = HttpContext.RequestServices.GetRequiredService<IDevicesGateway>();
            var devs = await devg.SearchAsync(q, 5);
            if (devs.Count > 0)
                groups.Add(new { title = "دستگاه‌های تردد", icon = "🖥️", items = devs.Select(d => new { text = d.Title, href = d.Href }) });
        }
        // خودروهای پارکینگ — فقط با لایهٔ دیتای ماژول پارکینگ
        if (sess.Has("parking.view"))
        {
            var cars = await SearchVehiclesSafeAsync(Db, q);
            if (cars.Count > 0)
                groups.Add(new { title = "خودروهای پارکینگ", icon = "🚗", items = cars.Select(v => new { text = v.Text, href = "/Parking?tab=vehicles" }) });
        }
        // کاربران — هسته
        if (sess.Has("users.view"))
        {
            var usrs = await Db.Users
                .Where(u => u.Username.Contains(q) || u.FullName.Contains(q))
                .OrderBy(u => u.Username).Take(5)
                .Select(u => new { u.Username, u.FullName })
                .ToListAsync();
            if (usrs.Count > 0)
                groups.Add(new { title = "کاربران", icon = "🛡️", items = usrs.Select(u => new { text = $"{u.Username} — {u.FullName}", href = "/Users" }) });
        }

        return Json(new { groups });
    }

    // ======================================================================
    // فاز ۶۷ — بدنه‌های ماژولی داشبورد؛ هر یک فقط پس از گارد «حضور فایل DLL» JIT می‌شود
    // ======================================================================

    private async Task<AttDashKpi> AttDashSafeAsync(AppDbContext db, string today)
    {
        var att = HttpContext.RequestServices.GetRequiredService<IAttendanceGateway>();
        var d = await att.DashboardAsync(today);
        var week = d.Week.Select(p =>
        {
            var dt = Jalali.ParseDateKey(p.DateKey);
            return (object)new
            {
                label = $"{Jalali.ToPersianDigits(Jalali.ToJalali(dt).D)} {Jalali.MonthName(Jalali.ToJalali(dt).M)}",
                present = p.Present,
                late = p.Late,
                absent = p.Absent,
            };
        }).ToList();
        return new AttDashKpi(d.Present, d.Late, d.Absent, d.OnLeave, d.PendingRequests, week);
    }

    private async Task<AccessDashKpi> AccessDashSafeAsync(AppDbContext db)
    {
        var acc = HttpContext.RequestServices.GetRequiredService<IAccessGateway>();
        var d = await acc.DashboardAsync();
        return new AccessDashKpi(d.TodayEvents,
            d.ZoneDist.Select(z => (object)new { Zone = z.Zone, Count = z.Count }).ToList(),
            d.RecentEvents.ToList(), d.RecentVisitorIds.ToList());
    }

    private Task<int> VisitorsInsideSafeAsync(AppDbContext db) =>
        HttpContext.RequestServices.GetRequiredService<IVisitorsGateway>().InsideCountAsync();

    public sealed record VehicleHit(string PlateNo, string? Brand, string Text);
    private async Task<List<VehicleHit>> SearchVehiclesSafeAsync(AppDbContext db, string q)
    {
        var hits = await HttpContext.RequestServices.GetRequiredService<IParkingReportSource>().VehicleSearchAsync(q);
        return hits.Select(h => new VehicleHit(h.PlateNo, h.Brand, h.Text)).ToList();
    }
}

/// <summary>کمک نمایشی — ارقام فارسی فقط روی بخش عددی متن (برای نتایج جست‌وجوی قراردادی).</summary>
public static class HomeSearchFmt
{
    public static string ToPersianDigitsIn(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
            sb.Append(char.IsAsciiDigit(ch) ? (char)('۰' + (ch - '0')) : ch);
        return sb.ToString();
    }
}
