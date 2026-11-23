using System.Text;
using System.Globalization;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Attendance.Data;
using HrModules.Attendance.Domain;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۶۱ — کنترلر تردد روزانه روی لایهٔ دیتای مستقل ماژول (AttendanceDbContext)؛
/// حسابرسی از IAuditSink (HrModuleControllerBase) و نام شیفت پرسنل از دروازهٔ
/// قراردادی IAttendanceGateway — بدون DbContext مرکب هاست.
/// </summary>
public class AttendanceController : HrModuleControllerBase
{
    private readonly AttendanceDbContext Db;
    private readonly IAttendanceGateway Gateway;

    public AttendanceController(AttendanceDbContext db, IAttendanceGateway gateway, ICurrentUser me, IAuditSink audits)
        : base(me, audits)
    { Db = db; Gateway = gateway; }

    [HttpGet]
    [HasPerm("attendance.view")]
    public async Task<IActionResult> Index(string? date)
    {
        var dk = string.IsNullOrEmpty(date) ? Jalali.DateKey(DateTime.Now) : date;
        // فاز ۷۴ — دامنهٔ رکوردی: کاربر محدودشده فقط ترددهای محدودهٔ مجاز خودش را می‌بیند
        // (فیلتر SQL-side روی ستون‌های ایندکس‌دار — سرعت گزارش‌ها حفظ می‌شود)
        var scope = await this.ScopeAsync();
        var records = await Db.AttendanceRecords
            .Include(r => r.Employee).ThenInclude(e => e!.Department)
            .Include(r => r.Device)
            .Where(r => r.Date == dk)
            .WhereEmpScope(scope, r => r.EmployeeId)
            .OrderBy(r => r.Employee!.PersonnelCode)
            .ToListAsync();

        // فاز ۶۱ — Employee دیگر navigation شیفت ندارد؛ نام شیفت هر پرسنل از دروازهٔ ماژول
        ViewBag.ShiftByEmp = await Gateway.ShiftsByEmployeeAsync(
            records.Select(r => r.EmployeeId).Distinct().ToList());

        ViewBag.Date = dk;
        ViewBag.Devices = await Db.Devices.Where(d => d.Active && d.Mode == "آنلاین").ToListAsync();
        return View(records);
    }

    /// <summary>شبیه‌ساز ثبت تردد (هم‌ارز POST /api/attendance) — با همان موتور computeWork.</summary>
    [HttpPost]
    [HasPerm("attendance.records.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Punch(string personnelCode, string? deviceId, string kind)
    {
        var emp = await Db.Employees
            .FirstOrDefaultAsync(e => e.PersonnelCode == personnelCode.Trim());
        if (emp is null) { ToastErr(HrT.T("کد پرسنلی یافت نشد")); if (Ajax) return AjaxResult(false, HrT.T("کد پرسنلی یافت نشد")); return Redirect("/Attendance"); }

        var now = DateTime.Now;
        var dk = Jalali.DateKey(now);
        var record = await Db.AttendanceRecords
            .FirstOrDefaultAsync(r => r.EmployeeId == emp.Id && r.Date == dk);
        if (record is null)
        {
            record = new AttendanceRecord { EmployeeId = emp.Id, Date = dk, Status = "حاضر" };
            Db.AttendanceRecords.Add(record);
        }
        if (deviceId is not null) record.DeviceId = deviceId;

        if (kind == "in" || kind == "both")
            record.CheckIn = record.CheckIn is null || now < record.CheckIn ? now : record.CheckIn;
        if (kind == "out" || kind == "both")
            record.CheckOut = record.CheckOut is null || now > record.CheckOut ? now : record.CheckOut;

        // فاز ۶۱ — Employee دیگر navigation شیفت ندارد؛ شیفت با ShiftId خوانده می‌شود
        var sid = emp.ShiftId;
        Shift? shift = null;
        if (sid is not null)
            shift = await Db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid);
        var w = AttCalc.ComputeWork(ShiftParamsOfExt.ShiftParamsOf(shift), record.CheckIn, record.CheckOut, dk);
        record.WorkMinutes = w.WorkMinutes;
        record.LateMinutes = w.LateMinutes;
        record.EarlyMinutes = w.EarlyMinutes;
        record.OvertimeMinutes = w.OvertimeMinutes;
        if (record.Status == "غایب" || record.Status == "حاضر")
            record.Status = AttCalc.StatusFromPunch(w.LateMinutes, record.CheckIn is not null);
        await Db.SaveChangesAsync();
        await Audit("punch", "attendance", record.Id,
            new { emp.PersonnelCode, kind, checkIn = record.CheckIn, checkOut = record.CheckOut });
        ToastOk(HrT.F("ثبت {0} برای «{1}» انجام شد", kind == "in" ? HrT.T("ورود") : kind == "out" ? HrT.T("خروج") : HrT.T("ورود و خروج"), emp.FullName));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Attendance");
    }

    // ======================================================================
    // فاز ۱۳۱ (مورد ۸ ROADMAP-ADDITIONS) — صورت‌وضعیت ماندهٔ مرخصی
    // ماندهٔ ذخیره‌شدهٔ پرسنل (حق/مصرف) کنارِ مصرفِ محاسبه‌شده از درخواست‌های
    // تأییدشدهٔ سامانه — تا رانش مانده (تغییر دستی/ورود گروهی بدون درخواست) دیده شود.
    // از این فاز تأیید مرخصی، مانده را خودکار کم می‌کند (RequestsController.DecideLeave).
    // ======================================================================

    [HttpGet]
    [HasPerm("attendance.view")]
    public async Task<IActionResult> LeaveBalances(string? q)
    {
        // فاز ۷۴ — دامنهٔ رکوردی SQL-side همانند سایر صفحات ماژول
        var scope = await this.ScopeAsync();
        var empsQ = Db.Employees
            .Include(e => e.Department)
            .WhereEmpScope(scope, e => e.Id)
            .AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            empsQ = empsQ.Where(e => e.FullName.Contains(term) || e.PersonnelCode.Contains(term));
        }
        var emps = await empsQ.OrderBy(e => e.PersonnelCode).ToListAsync();

        // مصرفِ ثبت‌شده در سامانه از درخواست‌های تأییدشده — تجمیع سمت کلاینت چون
        // SQLite تجمیع decimal را پشتیبانی نمی‌کند (خطای لاگ: NotSupportedException Sum decimal):
        // استحقاقی → روز، ساعتی → ساعت ÷ ۸ (روز کاری)، استعلاجی → روز؛ بقیهٔ نوع‌ها کسر ندارند
        var leaves = await Db.LeaveRequests.AsNoTracking()
            .Where(l => l.Status == "تأیید شده")
            .Select(l => new { l.EmployeeId, l.Type, l.Days, l.Hours })
            .ToListAsync();
        var approved = leaves.GroupBy(l => l.EmployeeId).ToDictionary(
            g => g.Key,
            g => (Annual: g.Sum(x => x.Type == "استحقاقی" ? x.Days
                             : x.Type == "ساعتی" ? (x.Hours ?? 0) / 8m : 0m),
                  Sick: g.Sum(x => x.Type == "استعلاجی" ? x.Days : 0m)));

        var rows = emps.Select(e =>
        {
            var a = approved.TryGetValue(e.Id, out var v) ? v : (Annual: 0m, Sick: 0m);
            return new LeaveBalanceRow(
                e.PersonnelCode, e.FullName, e.Department?.Name ?? "-", e.Status ?? "-",
                e.AnnualLeaveEnt, e.AnnualLeaveUsed, a.Annual,
                e.SickLeaveEnt, e.SickLeaveUsed, a.Sick);
        }).ToList();

        ViewBag.Q = q ?? "";
        ViewBag.TotalAnnualRemain = rows.Sum(r => Math.Max(0m, r.AnnualEnt - r.AnnualUsed));
        ViewBag.TotalSickRemain = rows.Sum(r => Math.Max(0m, r.SickEnt - r.SickUsed));
        ViewBag.DriftCount = rows.Count(r =>
            Math.Abs(r.AnnualUsed - r.AnnualApproved) > 0.01m ||
            Math.Abs(r.SickUsed - r.SickApproved) > 0.01m);
        return View(rows);
    }

    // ======================================================================
    // فاز ۱۳۱ (مورد ۲ ROADMAP-ADDITIONS) — گزارش «پانچ جامانده»
    // روزهایی که ورود ثبت شده ولی خروج ندارد (و مرخصی/مأموریت/تعطیل نیستند) —
    // همین رکوردها محاسبهٔ کارکرد و حقوق را خراب می‌کنند و تا امروز هیچ صفحه‌ای
    // نشان‌شان نمی‌داد. دادهٔ لازم همین الان در AttendanceRecord هست.
    // ======================================================================

    [HttpGet]
    [HasPerm("attendance.view")]
    public async Task<IActionResult> MissedPunch(string? from, string? to)
    {
        var today = DateTime.Now;
        var f = string.IsNullOrWhiteSpace(from) ? Jalali.DateKey(today.AddDays(-7)) : from.Trim();
        var t = string.IsNullOrWhiteSpace(to) ? Jalali.DateKey(today) : to.Trim();
        if (string.CompareOrdinal(f, t) > 0) (f, t) = (t, f);

        var scope = await this.ScopeAsync();
        var rows = await Db.AttendanceRecords
            .Include(r => r.Employee).ThenInclude(e => e!.Department)
            .Include(r => r.Device)
            .Where(r => string.Compare(r.Date, f) >= 0 && string.Compare(r.Date, t) <= 0)
            .Where(r => r.CheckIn != null && r.CheckOut == null)
            .Where(r => r.Status != "مرخصی" && r.Status != "مأموریت" && r.Status != "تعطیل")
            .WhereEmpScope(scope, r => r.EmployeeId)
            .OrderByDescending(r => r.Date).ThenBy(r => r.Employee!.PersonnelCode)
            .AsNoTracking()
            .ToListAsync();

        ViewBag.From = f;
        ViewBag.To = t;
        ViewBag.EmpCount = rows.Select(r => r.EmployeeId).Distinct().Count();
        return View(rows);
    }

    // ======================================================================
    // فاز ۱۳۱ (مورد ۱ ROADMAP-ADDITIONS) — گزارش تأخیر ماهانه با رتبه‌بندی
    // ماهِ شمسی (1405/08) با PersianCalendar به بازهٔ میلادی کلیدهای Date
    // (yyyy-MM-dd) ترجمه می‌شود؛ منبع داده همان LateMinutes است که موتور
    // AttCalc از اسکن دستگاه‌ها تولید می‌کند — داده‌ای چند ساله که تا امروز
    // هیچ گزارش رتبه‌بندی‌ای نداشت.
    // ======================================================================

    [HttpGet]
    [HasPerm("attendance.view")]
    public async Task<IActionResult> Lateness(string? month)
    {
        var pc = new System.Globalization.PersianCalendar();
        var now = DateTime.Now;
        var jy = pc.GetYear(now);
        var jm = pc.GetMonth(now);
        if (!string.IsNullOrWhiteSpace(month))
        {
            var parts = month.Trim().Split('/', '-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m)
                && y is >= 1300 and <= 1500 && m is >= 1 and <= 12)
            {
                jy = y; jm = m;
            }
        }
        var gStart = pc.ToDateTime(jy, jm, 1, 0, 0, 0, 0);
        var gEnd = gStart.AddMonths(1);
        var f = Jalali.DateKey(gStart);
        var t = Jalali.DateKey(gEnd);

        var scope = await this.ScopeAsync();
        var recs = await Db.AttendanceRecords.AsNoTracking()
            .Where(r => string.Compare(r.Date, f) >= 0 && string.Compare(r.Date, t) < 0)
            .Where(r => r.LateMinutes > 0)
            .WhereEmpScope(scope, r => r.EmployeeId)
            .Select(r => new { r.EmployeeId, r.LateMinutes })
            .ToListAsync();

        var emps = await Db.Employees.AsNoTracking()
            .Include(e => e.Department)
            .WhereEmpScope(scope, e => e.Id)
            .ToListAsync();
        var empById = emps.ToDictionary(e => e.Id);

        var rows = recs.GroupBy(r => r.EmployeeId)
            .Select(g =>
            {
                empById.TryGetValue(g.Key, out var e);
                return new LatenessRow(
                    e?.PersonnelCode ?? "?", e?.FullName ?? g.Key, e?.Department?.Name ?? "-",
                    g.Count(), g.Sum(x => x.LateMinutes), g.Max(x => x.LateMinutes),
                    Math.Round((decimal)g.Average(x => x.LateMinutes), 1));
            })
            .OrderByDescending(r => r.TotalMin).ThenByDescending(r => r.LateDays)
            .ToList();

        ViewBag.Month = $"{jy}/{jm:00}";
        ViewBag.EmpCount = rows.Count;
        ViewBag.TotalMin = rows.Sum(r => r.TotalMin);
        ViewBag.TotalDays = rows.Sum(r => r.LateDays);
        return View(rows);
    }

    // ======================================================================
    // فاز ۱۳۲ (مورد ۳ ROADMAP-ADDITIONS) — واریانس اضافه‌کاری مجاز/واقعی
    // مجاز = جمع مدت مجوزهای تأییدشدهٔ «اضافه‌کاری/کار در تعطیل» همان ماه؛
    // واقعی = جمع OvertimeMinutes رکوردهای تردد (خروجی موتور AttCalc).
    // واریانس مثبت = کارِ بدون مجوز (ریسک پرداخت حقوق)، منفی = مجوز بلااستفاده.
    // «خروج غیرمجاز» از مجازِ اضافه‌کاری مستثناست (کسر کار است نه اضافه‌کار).
    // تجمیع سمت کلاینت — SQLite تجمیع decimal را پشتیبانی نمی‌کند.
    // ======================================================================

    [HttpGet]
    [HasPerm("attendance.view")]
    public async Task<IActionResult> OvertimeVar(string? month)
    {
        var pc = new System.Globalization.PersianCalendar();
        var now = DateTime.Now;
        var jy = pc.GetYear(now);
        var jm = pc.GetMonth(now);
        if (!string.IsNullOrWhiteSpace(month))
        {
            var parts = month.Trim().Split('/', '-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m)
                && y is >= 1300 and <= 1500 && m is >= 1 and <= 12)
            {
                jy = y; jm = m;
            }
        }
        var gStart = pc.ToDateTime(jy, jm, 1, 0, 0, 0, 0);
        var gEnd = gStart.AddMonths(1);
        var f = Jalali.DateKey(gStart);
        var t = Jalali.DateKey(gEnd);

        var scope = await this.ScopeAsync();

        var actual = await Db.AttendanceRecords.AsNoTracking()
            .Where(r => string.Compare(r.Date, f) >= 0 && string.Compare(r.Date, t) < 0 && r.OvertimeMinutes > 0)
            .WhereEmpScope(scope, r => r.EmployeeId)
            .Select(r => new { r.EmployeeId, r.OvertimeMinutes })
            .ToListAsync();

        var permits = await Db.OvertimePermits.AsNoTracking()
            .Where(p => p.Status == "تأیید شده"
                && string.Compare(p.Date, f) >= 0 && string.Compare(p.Date, t) < 0
                && (p.Type == "اضافه‌کاری" || p.Type == "کار در تعطیل"))
            .WhereEmpScope(scope, p => p.EmployeeId)
            .Select(p => new { p.EmployeeId, p.FromTime, p.ToTime })
            .ToListAsync();

        static int MinOf(string s) => TimeSpan.TryParse(s, out var ts) ? (int)ts.TotalMinutes : 0;
        var permitMin = permits.GroupBy(p => p.EmployeeId).ToDictionary(
            g => g.Key,
            g => (Minutes: g.Sum(p => Math.Max(0, MinOf(p.ToTime) - MinOf(p.FromTime))), Count: g.Count()));
        var actualMin = actual.GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.OvertimeMinutes));

        var emps = await Db.Employees.AsNoTracking()
            .Include(e => e.Department)
            .WhereEmpScope(scope, e => e.Id)
            .ToListAsync();
        var empById = emps.ToDictionary(e => e.Id);

        var rows = new List<OvertimeVarRow>();
        foreach (var k in actualMin.Keys.Union(permitMin.Keys))
        {
            empById.TryGetValue(k, out var e);
            var allowedMin = permitMin.TryGetValue(k, out var p) ? p.Minutes : 0;
            var permitCount = permitMin.TryGetValue(k, out var pc2) ? pc2.Count : 0;
            var realMin = actualMin.TryGetValue(k, out var v) ? v : 0;
            rows.Add(new OvertimeVarRow(
                e?.PersonnelCode ?? "?", e?.FullName ?? k, e?.Department?.Name ?? "-",
                Math.Round(allowedMin / 60m, 1), Math.Round(realMin / 60m, 1),
                Math.Round((realMin - allowedMin) / 60m, 1), permitCount));
        }
        rows = rows.OrderByDescending(r => r.VarianceHours).ThenByDescending(r => r.ActualHours).ToList();

        ViewBag.Month = $"{jy}/{jm:00}";
        ViewBag.OverCount = rows.Count(r => r.VarianceHours > 0.05m);
        ViewBag.UnusedCount = rows.Count(r => r.VarianceHours < -0.05m);
        ViewBag.TotalAllowed = rows.Sum(r => r.AllowedHours);
        ViewBag.TotalActual = rows.Sum(r => r.ActualHours);
        return View(rows);
    }

    // ======================================================================
    // فاز ۹۶ (درخواست کاربر) — قالب برون‌بری «رکوردهای تردد (تاریخچه)»
    // نرم‌افزارهای مختلف فرمت‌های مختلفی برای تاریخ/زمان می‌خواهند؛ ادمین برای هر
    // مقصد قالب می‌سازد: ستون‌ها + قالب تاریخ (لینوکسی linuxtimestamp / شمسی /
    // میلادی / قمری / ISO) + قالب زمان + جداکننده + سطر سرستون → خروجی CSV.
    // ======================================================================

    /// <summary>فیلدهای قابل انتخاب در قالب — (کلید، عنوان، نوع) — نوع: text/date/datetime/int/status</summary>
    public static readonly (string Key, string Fa, string Kind)[] ExportFields =
    {
        ("personnelCode", "کد پرسنلی", "text"),
        ("fullName", "نام و نام خانوادگی", "text"),
        ("department", "واحد سازمانی", "text"),
        ("device", "دستگاه", "text"),
        ("date", "تاریخ تردد", "date"),
        ("checkin", "زمان ورود", "datetime"),
        ("checkout", "زمان خروج", "datetime"),
        ("workMinutes", "کارکرد (دقیقه)", "int"),
        ("lateMinutes", "دیرکرد (دقیقه)", "int"),
        ("earlyMinutes", "تعجیل (دقیقه)", "int"),
        ("overtimeMinutes", "اضافه‌کاری (دقیقه)", "int"),
        ("status", "وضعیت", "status"),
        ("note", "یادداشت", "text"),
    };

    /// <summary>قالب‌های تاریخ برای ستون «تاریخ تردد» — لینوکسی/شمسی/میلادی/قمری/ISO (درخواست کاربر)</summary>
    public static readonly (string Key, string Fa)[] DateFormatOptions =
    {
        ("iso", "ISO میلادی — 2026-01-24"),
        ("linux", "لینوکسی (Linux timestamp — ثانیه)"),
        ("jalali", "شمسی (جلالی) — 1404/11/04"),
        ("gregorian", "میلادی — 2026/01/24"),
        ("hijri", "قمری (هجری قمری) — 1447/08/05"),
    };

    /// <summary>قالب‌های زمان برای ستون‌های ورود/خروج — لینوکسی/شمسی/میلادی/قمری کامل یا فقط ساعت</summary>
    public static readonly (string Key, string Fa)[] TimeFormatOptions =
    {
        ("hhmm", "فقط ساعت — 08:03"),
        ("hhmmss", "ساعت با ثانیه — 08:03:22"),
        ("linux", "لینوکسی (Linux timestamp — ثانیه)"),
        ("jalali", "شمسی کامل — 1404/11/04 08:03:22"),
        ("gregorian", "میلادی کامل — 2026/01/24 08:03:22"),
        ("hijri", "قمری کامل — 1447/08/05 08:03:22"),
    };

    [HttpGet]
    [HasPerm("attendance.view")]
    public async Task<IActionResult> Exports()
    {
        ViewBag.Templates = await Db.ExportTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        ViewBag.Fields = ExportFields;
        ViewBag.DateFormats = DateFormatOptions;
        ViewBag.TimeFormats = TimeFormatOptions;
        return View();
    }

    /// <summary>اعتبارسنجی و نرمال‌سازی JSON ستون‌های قالب — خروجی: لیست یا پیام خطا.</summary>
    private static (List<(string F, string Fmt, string H)>? Cols, string? Err) ParseColumns(string? json)
    {
        try
        {
            var doc = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json ?? "[]",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (doc is null || doc.Count == 0) return (null, "حداقل یک ستون لازم است");
            if (doc.Count > 20) return (null, "بیشتر از ۲۰ ستون مجاز نیست");
            var dateKeys = DateFormatOptions.Select(x => x.Key).ToHashSet();
            var timeKeys = TimeFormatOptions.Select(x => x.Key).ToHashSet();
            var outCols = new List<(string F, string Fmt, string H)>();
            foreach (var el in doc)
            {
                var f = el.TryGetProperty("f", out var fv) ? fv.GetString() ?? "" : "";
                var def = ExportFields.FirstOrDefault(x => x.Key == f);
                if (def.Key is null) return (null, "فیلد ناشناخته در ستون‌ها: " + f);
                var fmt = el.TryGetProperty("fmt", out var mv) ? (mv.GetString() ?? "") : "";
                if (def.Kind == "date" && !dateKeys.Contains(fmt)) fmt = "iso";
                if (def.Kind == "datetime" && !timeKeys.Contains(fmt)) fmt = "hhmm";
                if (def.Kind is not ("date" or "datetime")) fmt = "";
                var h = el.TryGetProperty("h", out var hv) ? (hv.GetString() ?? "").Trim() : "";
                if (h.Length > 60) h = h[..60];
                outCols.Add((f, fmt, h));
            }
            return (outCols, null);
        }
        catch { return (null, "ساختار ستون‌ها معتبر نیست"); }
    }

    [HttpPost]
    [HasPerm("attendance.records.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTemplate(string? id, string name, string? note, string columns,
        string delimiter, bool includeHeader)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) { ToastErr(HrT.T("نام قالب الزامی است")); if (Ajax) return AjaxResult(false, HrT.T("نام قالب الزامی است")); return Redirect("/Attendance/Exports"); }
        if (name.Length > 100) name = name[..100];
        delimiter = delimiter switch { ";" => ";", "tab" => "\t", "|" => "|", _ => "," };
        var (cols, err) = ParseColumns(columns);
        if (cols is null)
        {
            ToastErr(HrT.F("قالب ذخیره نشد: {0}", err));
            if (Ajax) return AjaxResult(false, HrT.F("قالب ذخیره نشد: {0}", err));
            return Redirect("/Attendance/Exports");
        }
        var t = string.IsNullOrEmpty(id) ? null : await Db.ExportTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) t = Db.ExportTemplates.Add(new AttendanceExportTemplate { CreatedBy = Sess?.Username }).Entity;
        t.Name = name;
        t.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 380)];
        t.ColumnsJson = System.Text.Json.JsonSerializer.Serialize(cols.Select(c => new { f = c.F, fmt = c.Fmt, h = c.H }));
        t.Delimiter = delimiter;
        t.IncludeHeader = includeHeader;
        t.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("save", "attendance_export_template", t.Id, new { t.Name, cols = cols.Count, t.Delimiter, t.IncludeHeader });
        ToastOk(HrT.F("قالب «{0}» ذخیره شد", t.Name));
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "ذخیره شد", new { id = t.Id });
        return Redirect("/Attendance/Exports");
    }

    [HttpPost]
    [HasPerm("attendance.records.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        var t = await Db.ExportTemplates.FindAsync(id);
        if (t is not null)
        {
            Db.ExportTemplates.Remove(t);
            await Db.SaveChangesAsync();
            await Audit("delete", "attendance_export_template", id, new { t.Name });
        }
        ToastOk(HrT.T("قالب حذف شد"));
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "انجام شد");
        return Redirect("/Attendance/Exports");
    }

    /// <summary>برون‌بری CSV طبق قالب — بازهٔ تاریخ (پیش‌فرض: ۳۰ روز اخیر).</summary>
    [HttpGet]
    [HasPerm("attendance.view")]
    public async Task<IActionResult> ExportByTemplate(string id, string? from, string? to)
    {
        var t = await Db.ExportTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) { ToastErr(HrT.T("قالب یافت نشد")); return Redirect("/Attendance/Exports"); }
        var (cols, err) = ParseColumns(t.ColumnsJson);
        if (cols is null) { ToastErr(HrT.F("قالب معتبر نیست: {0}", err)); return Redirect("/Attendance/Exports"); }
        var toKey = NormDateKey(to) ?? Jalali.DateKey(DateTime.Now);
        var fromKey = NormDateKey(from) ?? Jalali.DateKey(DateTime.Now.AddDays(-30));
        if (string.CompareOrdinal(fromKey, toKey) > 0) (fromKey, toKey) = (toKey, fromKey);

        var scope = await this.ScopeAsync();
        var records = await Db.AttendanceRecords
            .Include(r => r.Employee).ThenInclude(e => e!.Department)
            .Include(r => r.Device)
        // فاز ۱۲۲ — رفع 500 برون‌بری: string.CompareOrdinal داخل LINQ برای EF قابل
        // ترجمه نیست؛ مقایسهٔ رشته‌ای CompareTo (همان الگوی AttendanceDbContext) به
        // مقایسهٔ TEXT در SQLite ترجمه می‌شود و چون Date کلید ISO است ترتیب یکسان دارد.
            .Where(r => r.Date.CompareTo(fromKey) >= 0 && r.Date.CompareTo(toKey) <= 0)
            .WhereEmpScope(scope, r => r.EmployeeId)
            .OrderBy(r => r.Date).ThenBy(r => r.Employee!.PersonnelCode)
            .Take(200_000)
            .ToListAsync();

        var fieldOf = ExportFields.ToDictionary(x => x.Key, x => x);
        string Cell(AttendanceRecord r, (string F, string Fmt, string H) c)
        {
            var def = fieldOf[c.F];
            switch (c.F)
            {
                case "personnelCode": return r.Employee?.PersonnelCode ?? "";
                case "fullName": return r.Employee?.FullName ?? "";
                case "department": return r.Employee?.Department?.Name ?? "";
                case "device": return r.Device?.Name ?? "";
                case "date": return FmtDate(TryDate(r.Date) ?? default, c.Fmt);
                case "checkin": return r.CheckIn is null ? "" : FmtStamp(r.CheckIn.Value, c.Fmt);
                case "checkout": return r.CheckOut is null ? "" : FmtStamp(r.CheckOut.Value, c.Fmt);
                case "workMinutes": return r.WorkMinutes.ToString(CultureInfo.InvariantCulture);
                case "lateMinutes": return r.LateMinutes.ToString(CultureInfo.InvariantCulture);
                case "earlyMinutes": return r.EarlyMinutes.ToString(CultureInfo.InvariantCulture);
                case "overtimeMinutes": return r.OvertimeMinutes.ToString(CultureInfo.InvariantCulture);
                case "status": return r.Status;
                case "note": return r.Note ?? "";
                default: return "";
            }
        }
        var dlim = t.Delimiter;
        var sb = new StringBuilder();
        string esc(string s)
        {
            if (s.Contains('"') || s.Contains('\n') || s.Contains('\r') || s.Contains(dlim))
                s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
        if (t.IncludeHeader)
            sb.AppendLine(string.Join(dlim, cols.Select(c =>
                esc(string.IsNullOrWhiteSpace(c.H) ? (fieldOf[c.F].Fa) : c.H))));
        foreach (var r in records)
            sb.AppendLine(string.Join(dlim, cols.Select(c => esc(Cell(r, c)))));

        var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = utf8Bom.Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        await Audit("export_template", "attendance", t.Id,
            new { t.Name, from = fromKey, to = toKey, rows = records.Count });
        var safeName = string.Concat(t.Name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        return File(body, "text/csv; charset=utf-8",
            $"Traffic-{safeName}-{fromKey}_{toKey}.csv");
    }

    private static DateTime? TryDate(string key)
    {
        try { return Jalali.ParseDateKey(key); } catch { return null; }
    }

    /// <summary>
    /// فاز ۱۲۳-ب — نرمال‌سازی ورودی تاریخِ برون‌بری به کلید ISO.
    /// لایهٔ شمسی‌سازی سامانه (site.js) و کپی‌های URL ممکن است تاریخ شمسی با ارقام
    /// فارسی/عربی یا لاتین بفرستند؛ ISO (yyyy-MM-dd) و شمسی (yyyy/MM/dd) هر دو پذیرفته
    /// می‌شوند و خروجی همیشه کلید ISOِ مقایسه‌پذیر با ستون Date است. نامعتبر = null.
    /// </summary>
    internal static string? NormDateKey(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var chars = s.Trim().Select(c => c switch
        {
            >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')), // ارقام فارسی
            >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')), // ارقام عربی
            _ => c
        }).ToArray();
        var t = new string(chars).Replace('-', '/');
        var parts = t.Split('/');
        if (parts.Length == 3 && parts.All(p => p.Length > 0 && p.All(char.IsDigit)))
        {
            var n = parts.Select(int.Parse).ToArray();
            try
            {
                // سال < 1900 یعنی شمسی (یا قمری‌نما)؛ وگرنه خودش میلادی است
                return n[0] < 1900
                    ? Jalali.FromJalali(n[0], n[1], n[2]).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : $"{n[0]:D4}-{n[1]:D2}-{n[2]:D2}";
            }
            catch { return null; }
        }
        return DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>قالب تاریخ ستون «تاریخ تردد» — لینوکسی/شمسی/میلادی/قمری/ISO (ارقام لاتین برای سازگاری نرم‌افزار مقصد)</summary>
    internal static string FmtDate(DateTime d, string fmt) => fmt switch
    {
        "linux" => ((DateTimeOffset)new DateTime(d.Year, d.Month, d.Day, 0, 0, 0)).ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture),
        "jalali" => FmtJalaliDate(d),
        "gregorian" => d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
        "hijri" => Jalali.FmtHijri(d),
        _ => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    /// <summary>قالب زمان ستون‌های ورود/خروج — لینوکسی/شمسی/میلادی/قمری کامل یا فقط ساعت</summary>
    internal static string FmtStamp(DateTime d, string fmt) => fmt switch
    {
        "linux" => ((DateTimeOffset)d).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        "hhmm" => d.ToString("HH:mm", CultureInfo.InvariantCulture),
        "hhmmss" => d.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        "jalali" => FmtJalaliDate(d) + " " + d.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        "gregorian" => d.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture),
        "hijri" => Jalali.FmtHijri(d) + " " + d.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        _ => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
    };

    private static string FmtJalaliDate(DateTime d)
    {
        var (y, m, dd) = Jalali.ToJalali(d);
        return $"{y:0000}/{m:00}/{dd:00}";
    }
}


/// <summary>فاز ۶۱ — ساخت پارامتر شیفت از entity شیفت (جایگزین ShiftParams.From).</summary>
internal static class ShiftParamsOfExt
{
    public static ShiftParams? ShiftParamsOf(HrModules.Attendance.Domain.Shift? s) => s is null ? null : new ShiftParams
    {
        StartTime = s.StartTime, EndTime = s.EndTime,
        BreakMinutes = s.BreakMinutes, ToleranceLate = s.ToleranceLate,
    };
}
