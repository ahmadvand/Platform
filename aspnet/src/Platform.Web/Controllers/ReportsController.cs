using ClosedXML.Excel;
using Platform.Web.Data;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace Platform.Web.Controllers;

/// <summary>گزارش‌ساز و داشبورد تحلیلی (فاز ۱۵) — روند ماهانه، KPI واحدها، گزارش سفارشی + خروجی PDF/اکسل.</summary>
public class ReportsController : MvcBase
{
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment Env;

    // فاز ۶۷ — منابع گزارش فقط در صورت حضور ماژول‌ها حل می‌شوند (گزارش‌ساز برای منبع غایب خالی معتبر برمی‌گرداند)
    public ReportsController(AppDbContext db, ICurrentUser me, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env) : base(db, me)
    {
        Env = env;
    }

    private ReportService? _rpt;
    private ReportService Rpt => _rpt ??= new ReportService(
        HttpContext.RequestServices.GetService<Platform.Contracts.IAttendanceReportSource>(),
        HttpContext.RequestServices.GetService<Platform.Contracts.IParkingReportSource>());

    // ---------- فاز ۷۴ — دامنهٔ رکوردی گزارش‌ها ----------

    // فاز ۶۷ — لیست پارکینگ‌ها ∈ ماژول پارکینگ؛ بدون لایهٔ دیتای آن خالی
    /// <summary>فاز ۶۸ — فهرست پارکینگ‌ها ∈ پلاگین پارکینگ (قرارداد IParkingReportSource) — فاز ۷۵: تایپ‌دار.</summary>
    private Task<List<HrLotOption>> LotsSafeAsync(AppDbContext db) =>
        HttpContext.RequestServices.GetRequiredService<Platform.Contracts.IParkingReportSource>().LotsAsync();

    // ================= داشبورد تحلیلی =================

    /// <summary>فاز ۷۴ — پارامترهای دامنهٔ رکوردی کاربر برای گزارش‌ها (SQL-side در منبع گزارش).
    /// گارد fail-closed: دامنهٔ تعریف‌شده اما بدون هیچ مجازی = شناسهٔ غیرممکن (خروجی خالی).</summary>
    private async Task<(IReadOnlyCollection<string>? Co, IReadOnlyCollection<string>? Depts, string? SelfEmp)>
        ReportScopeAsync()
    {
        var s = await this.ScopeAsync();
        if (s.All) return (null, null, null);
        IReadOnlyCollection<string>? co = s.Companies.Count > 0 ? s.Companies : null;
        IReadOnlyCollection<string>? dp = s.Depts.Count > 0 ? s.Depts : null;
        string? self = !string.IsNullOrEmpty(s.SelfEmpId) ? s.SelfEmpId : null;
        // fail-closed — محدودشده اما نه شرکت، نه واحد، نه خودش: هیچ ردیفی نباید برگردد
        if (co is null && dp is null && self is null) co = ["__scope_denied__"];
        return (co, dp, self);
    }

    [HttpGet]
    [HasPerm("reports.view")]
    public async Task<IActionResult> Index()
    {
        var (co, dp, self) = await ReportScopeAsync();
        ViewBag.Trend = await Rpt.MonthlyTrendAsync(6, co, dp, self);
        var (jy, jm, _) = Jalali.ToJalali(DateTime.Now);
        var (start, end) = Jalali.MonthRange(jy, jm);
        ViewBag.DeptKpis = await Rpt.DeptKpisAsync(Jalali.DateKey(start), Jalali.DateKey(end.AddDays(-1)), co, dp, self);
        ViewBag.Period = HrT.F("ماه {0} {1}", HrT.Rtl ? Jalali.MonthName(jm) : HrT.Num(jm), HrT.Num(jy)) +
            (self is not null ? HrT.T(" — فقط رکوردهای خودم") : dp is not null ? HrT.T(" — واحدهای مجاز من") : co is not null ? HrT.T(" — شرکت‌های مجاز من") : "");
        return View();
    }

    // ================= گزارش سفارشی =================

    // فاز ۷۴ — فیلتر گزینه‌های پرسنل dropdown بر اساس دامنه (شرکت/واحد/خودم) — یک کوئری ایندکس‌دار
    private async Task<List<HrPersonOption>> ScopedPeopleAsync(List<HrPersonOption> all,
        IReadOnlyCollection<string>? co, IReadOnlyCollection<string>? dp, string? self)
    {
        if (self is not null) return all.Where(p => p.Id == self).ToList();
        if ((co is null || co.Count == 0) && (dp is null || dp.Count == 0)) return all;
        // پرسنل ∈ خانوادهٔ org — فقط در مدل مرکب حاضر است (MINIMAL بدون org = dropdown خالی)
        if (Db.Model.FindEntityType(typeof(Platform.Web.Domain.Employee)) is null)
            return new List<HrPersonOption>();
        var eq = Db.Set<Platform.Web.Domain.Employee>().AsNoTracking()
            .Select(e => new { e.Id, e.CompanyId, e.DepartmentId });
        if (co is { Count: > 0 }) eq = eq.Where(e => co.Contains(e.CompanyId));
        if (dp is { Count: > 0 }) eq = eq.Where(e => e.DepartmentId != null && dp.Contains(e.DepartmentId));
        var allowed = await eq.Select(e => e.Id).ToListAsync();
        return all.Where(p => allowed.Contains(p.Id)).ToList();
    }

    [HttpGet]
    [HasPerm("reports.view")]
    public async Task<IActionResult> Custom(string? deptId, string? employeeId, string? from, string? to, string? status)
    {
        // فاز ۶۹ — واحدها/پرسنل ∈ پلاگین org (قرارداد؛ غایب = dropdown خالی)
        var people = HttpContext.RequestServices.GetRequiredService<IPeopleDirectory>();

        // پیش‌فرض: ماه جاری
        var (jy, jm, _) = Jalali.ToJalali(DateTime.Now);
        var (start, end) = Jalali.MonthRange(jy, jm);
        var fromKey = string.IsNullOrEmpty(from) ? Jalali.DateKey(start) : from;
        var toKey = string.IsNullOrEmpty(to) ? Jalali.DateKey(end.AddDays(-1)) : to;

        // فاز ۷۴ — دامنهٔ رکوردی: فیلتر واحد/پرسنل بیرون دامنه به دامنهٔ مجاز برمی‌گردد
        var (co, dp, self) = await ReportScopeAsync();
        if (self is not null) employeeId = self;
        if (dp is not null && (deptId is null || !dp.Contains(deptId))) deptId = null;

        // فاز ۷۴ — dropdownها هم فقط داخل دامنه (جلوگیری از انتخاب پرسنل/واحد بیرون محدوده)
        var sAll = co is null && dp is null && self is null;
        var deptsAll = await people.DepartmentsAsync();
        ViewBag.Departments = sAll ? deptsAll
            : (dp is null ? deptsAll : deptsAll.Where(d => dp.Contains(d.Id)).ToList());
        // فاز ۱۱۴ — پیکر جستجومحور پرسنل: لیست کامل حذف شد؛ فقط شخصِ انتخاب‌شده برای پیش‌انتخاب
        var selEmp = string.IsNullOrEmpty(employeeId) ? null : await people.FindAsync(employeeId);
        ViewBag.SelectedPerson = selEmp;

        ViewBag.From = fromKey; ViewBag.To = toKey;
        ViewBag.DeptId = deptId; ViewBag.EmployeeId = employeeId; ViewBag.Status = status;
        ViewBag.Rows = await Rpt.CustomQueryAsync(deptId, employeeId, fromKey, toKey, status, 1000, co, dp);
        return View();
    }

    // ================= خروجی اکسل گزارش سفارشی =================

    [HttpGet]
    [HasPerm("reports.export")]
    public async Task<IActionResult> CustomXlsx(string? deptId, string? employeeId, string? from, string? to, string? status)
    {
        var (jy, jm, _) = Jalali.ToJalali(DateTime.Now);
        var (start, end) = Jalali.MonthRange(jy, jm);
        var fromKey = string.IsNullOrEmpty(from) ? Jalali.DateKey(start) : from;
        var toKey = string.IsNullOrEmpty(to) ? Jalali.DateKey(end.AddDays(-1)) : to;

        var (co, dp, self) = await ReportScopeAsync();
        if (self is not null) employeeId = self;
        if (dp is not null && (deptId is null || !dp.Contains(deptId))) deptId = null;
        var rows = await Rpt.CustomQueryAsync(deptId, employeeId, fromKey, toKey, status, limit: 100000, co, dp);
        await Audit("export", "report_custom", null, new { fromKey, toKey, deptId, employeeId, status, count = rows.Count });

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(HrT.T("گزارش تردد"));
        ws.RightToLeft = HrT.Rtl; // فاز ۸۱ — جهت شیت از زبان جاری

        string[] heads = [HrT.T("کد پرسنلی"), HrT.T("نام و نام خانوادگی"), HrT.T("واحد سازمانی"), HrT.T("تاریخ"), HrT.T("تاریخ جلالی"), HrT.T("وضعیت"), HrT.T("ورود"), HrT.T("خروج"), HrT.T("کارکرد (دقیقه)"), HrT.T("تأخیر (دقیقه)"), HrT.T("اضافه‌کاری (دقیقه)")];
        for (var i = 0; i < heads.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = heads[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#4f46e5");
            c.Style.Font.FontColor = XLColor.White;
        }
        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.PersonnelCode;
            ws.Cell(r, 2).Value = x.FullName;
            ws.Cell(r, 3).Value = x.Dept;
            ws.Cell(r, 4).Value = x.Date;
            ws.Cell(r, 5).Value = Jalali.FmtFa(Jalali.ParseDateKey(x.Date));
            ws.Cell(r, 6).Value = HrT.T(x.Status);
            ws.Cell(r, 7).Value = x.CheckIn is null ? "—" : Jalali.FmtTime(x.CheckIn.Value);
            ws.Cell(r, 8).Value = x.CheckOut is null ? "—" : Jalali.FmtTime(x.CheckOut.Value);
            ws.Cell(r, 9).Value = x.WorkMinutes;
            ws.Cell(r, 10).Value = x.LateMinutes;
            ws.Cell(r, 11).Value = x.OvertimeMinutes;
            r++;
        }
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"report-{fromKey}-{toKey}.xlsx");
    }

    // ================= خروجی PDF تحلیلی =================

    [HttpGet]
    [HasPerm("reports.export")]
    public async Task<IActionResult> AnalyticsPdf(string? from, string? to)
    {
        var (jy, jm, _) = Jalali.ToJalali(DateTime.Now);
        var (start, end) = Jalali.MonthRange(jy, jm);
        var fromKey = string.IsNullOrEmpty(from) ? Jalali.DateKey(start) : from;
        var toKey = string.IsNullOrEmpty(to) ? Jalali.DateKey(end.AddDays(-1)) : to;

        var (co, dp, self) = await ReportScopeAsync();
        var depts = (await Rpt.DeptKpisAsync(fromKey, toKey, co, dp, self)).ToArray();
        var emps = (await Rpt.EmployeeSummaryAsync(deptId: null, fromKey, toKey, co, dp, self)).ToArray();
        await ApplyPdfThemeAsync();
        var bytes = PdfReport.BuildAnalytics(
            HrT.T("گزارش تحلیلی حضور و غیاب"),
            HrT.F("{0} تا {1}",
                HrT.Rtl ? Jalali.FmtFa(Jalali.ParseDateKey(fromKey)) : Jalali.ParseDateKey(fromKey).ToString("yyyy-MM-dd"),
                HrT.Rtl ? Jalali.FmtFa(Jalali.ParseDateKey(toKey)) : Jalali.ParseDateKey(toKey).ToString("yyyy-MM-dd")),
            depts, emps, WebRootPath());
        await Audit("export", "report_pdf", null, new { fromKey, toKey, depts = depts.Length, emps = emps.Length });
        return File(bytes, "application/pdf", $"analytics-{fromKey}-{toKey}.pdf");
    }

    // ================= گزارش ورود/خروج خودروها — کنترل پارکینگ (فاز ۴۲) =================

    private static (string FromKey, string ToKey) ParkingRange(string? from, string? to)
    {
        var (jy, jm, _) = Jalali.ToJalali(DateTime.Now);
        var (start, end) = Jalali.MonthRange(jy, jm);
        var fromKey = string.IsNullOrEmpty(from) ? Jalali.DateKey(start) : from;
        var toKey = string.IsNullOrEmpty(to) ? Jalali.DateKey(DateTime.Now) : to;
        return (fromKey, toKey);
    }

    /// <summary>فاز ۷۵ — دامنهٔ کامل گزارش پارکینگ: (شرکت‌ها، واحدها، خودِ پرسنل) با الگوی ReportScopeAsync؛
    /// تا فاز ۷۴ فقط شرکت پشتیبانی می‌شد و کاربر محدودشده به واحد، کل رویدادها را می‌دید. fail-closed.</summary>
    private async Task<(IReadOnlyCollection<string>? Co, IReadOnlyCollection<string>? Dp, string? Self)> ParkingScopeAsync()
    {
        var s = await this.ScopeAsync();
        if (s.All) return (null, null, null);
        IReadOnlyCollection<string>? co = s.Companies.Count > 0 ? s.Companies : null;
        IReadOnlyCollection<string>? dp = s.Depts.Count > 0 ? s.Depts : null;
        string? self = !string.IsNullOrEmpty(s.SelfEmpId) ? s.SelfEmpId : null;
        if (co is null && dp is null && self is null) co = ["__scope_denied__"];
        return (co, dp, self);
    }

    private static string Qs(string? fromKey, string? toKey, string? co, string? lotId, string? direction, string? result, string? method) =>
        $"from={fromKey}&to={toKey}&co={Uri.EscapeDataString(co ?? "")}&lotId={Uri.EscapeDataString(lotId ?? "")}" +
        $"&direction={Uri.EscapeDataString(direction ?? "")}&result={Uri.EscapeDataString(result ?? "")}&method={Uri.EscapeDataString(method ?? "")}";

    /// <summary>صفحه گزارش ورود/خروج خودروهای پارکینگ — فیلتر بازه/شرکت/پارکینگ/جهت/نتیجه/روش.</summary>
    [HttpGet]
    [HasPerm("reports.view")]
    public async Task<IActionResult> Parking(string? from, string? to, string? co, string? lotId,
        string? direction, string? result, string? method)
    {
        var (fromKey, toKey) = ParkingRange(from, to);
        // فاز ۷۴/۷۵ — دامنهٔ رکوردی کامل (شرکت/واحد/خودِ پرسنل)؛ انتخاب بیرون دامنه صفر می‌شود
        var (pCo, pDp, pSelf) = await ParkingScopeAsync();
        if (pCo is not null && !string.IsNullOrEmpty(co) && !pCo.Contains(co)) co = null;
        var pScopeAll = await this.ScopeAsync();          // کش‌شده در DataScopeService — برای dropdown شرکت‌ها
        ViewBag.From = fromKey; ViewBag.To = toKey;
        ViewBag.Co = co; ViewBag.LotId = lotId;
        ViewBag.Direction = direction; ViewBag.Result = result; ViewBag.Method = method;
        ViewBag.Companies = (await Db.Companies.OrderBy(c => c.Code).ToListAsync())
            .Where(c => pScopeAll.All || pScopeAll.Companies.Contains(c.Id)).ToList();
        // فاز ۶۷ — لیست پارکینگ‌ها فقط با لایهٔ دیتای ماژول پارکینگ
        ViewBag.Lots = await LotsSafeAsync(Db);
        ViewBag.Qs = Qs(fromKey, toKey, co, lotId, direction, result, method);
        ViewBag.Rows = await Rpt.ParkingEventsAsync(fromKey, toKey, co, lotId, direction, result, method, limit: 500, pCo, pDp, pSelf);
        return View();
    }

    /// <summary>خروجی اکسل گزارش ورود/خروج خودروها — حداکثر ۱۰۰ هزار ردیف.</summary>
    [HttpGet]
    [HasPerm("reports.export")]
    public async Task<IActionResult> ParkingXlsx(string? from, string? to, string? co, string? lotId,
        string? direction, string? result, string? method)
    {
        var (fromKey, toKey) = ParkingRange(from, to);
        var (xCo, xDp, xSelf) = await ParkingScopeAsync();
        if (xCo is not null && !string.IsNullOrEmpty(co) && !xCo.Contains(co)) co = null;
        var rows = await Rpt.ParkingEventsAsync(fromKey, toKey, co, lotId, direction, result, method, limit: 100000, xCo, xDp, xSelf);
        await Audit("export", "report_parking_xlsx", null, new { fromKey, toKey, co, lotId, direction, result, method, count = rows.Count });

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(HrT.T("تردد خودروها"));
        ws.RightToLeft = HrT.Rtl; // فاز ۸۱ — جهت شیت از زبان جاری

        string[] heads = [HrT.T("زمان"), HrT.T("تاریخ جلالی"), HrT.T("ساعت"), HrT.T("پلاک"), HrT.T("صاحب خودرو"), HrT.T("نوع خودرو"), HrT.T("شرکت"),
            HrT.T("پارکینگ"), HrT.T("گیت"), HrT.T("جهت"), HrT.T("روش"), HrT.T("دوربین پلاک‌خوان"), HrT.T("اطمینان (٪)"), HrT.T("نتیجه"), HrT.T("دلیل رد")];
        for (var i = 0; i < heads.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = heads[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#4f46e5");
            c.Style.Font.FontColor = XLColor.White;
        }
        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.Time.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(r, 2).Value = Jalali.Fmt(x.Time);
            ws.Cell(r, 3).Value = Jalali.FmtTime(x.Time);
            ws.Cell(r, 4).Value = x.Plate;
            ws.Cell(r, 5).Value = x.Owner;
            ws.Cell(r, 6).Value = x.VehicleType;
            ws.Cell(r, 7).Value = x.Company;
            ws.Cell(r, 8).Value = x.Lot;
            ws.Cell(r, 9).Value = x.Gate;
            ws.Cell(r, 10).Value = HrT.T(x.Direction);
            ws.Cell(r, 11).Value = HrT.T(x.Method);
            ws.Cell(r, 12).Value = x.Camera ?? "—";
            ws.Cell(r, 13).Value = x.Confidence ?? 0;
            ws.Cell(r, 14).Value = HrT.T(x.Result);
            ws.Cell(r, 15).Value = x.Reason ?? "—";
            r++;
        }
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"parking-{fromKey}-{toKey}.xlsx");
    }

    /// <summary>خروجی PDF گزارش ورود/خروج خودروها — KPI + ریز رویدادها.</summary>
    [HttpGet]
    [HasPerm("reports.export")]
    public async Task<IActionResult> ParkingPdf(string? from, string? to, string? co, string? lotId,
        string? direction, string? result, string? method)
    {
        var (fromKey, toKey) = ParkingRange(from, to);
        var (pdCo, pdDp, pdSelf) = await ParkingScopeAsync();
        if (pdCo is not null && !string.IsNullOrEmpty(co) && !pdCo.Contains(co)) co = null;
        var rows = await Rpt.ParkingEventsAsync(fromKey, toKey, co, lotId, direction, result, method, limit: 20000, pdCo, pdDp, pdSelf);
        await ApplyPdfThemeAsync();
        var bytes = PdfReport.BuildParking(
            HrT.T("گزارش ورود/خروج خودروها (کنترل پارکینگ)"),
            HrT.F("{0} تا {1}",
                HrT.Rtl ? Jalali.FmtFa(Jalali.ParseDateKey(fromKey)) : Jalali.ParseDateKey(fromKey).ToString("yyyy-MM-dd"),
                HrT.Rtl ? Jalali.FmtFa(Jalali.ParseDateKey(toKey)) : Jalali.ParseDateKey(toKey).ToString("yyyy-MM-dd")) +
            (string.IsNullOrEmpty(method) ? "" : method == "پلاک‌خوان" ? " — " + HrT.T("فقط ثبت خودکار پلاک‌خوان") : " — " + HrT.F("روش {0}", HrT.T(method))),
            rows, WebRootPath());
        await Audit("export", "report_parking_pdf", null, new { fromKey, toKey, co, lotId, direction, result, method, count = rows.Count });
        return File(bytes, "application/pdf", $"parking-{fromKey}-{toKey}.pdf");
    }

    private string WebRootPath() =>
        Env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
}
