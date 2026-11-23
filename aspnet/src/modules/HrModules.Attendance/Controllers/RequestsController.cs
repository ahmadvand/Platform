using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Attendance.Data;
using Platform.Contracts;
using HrModules.Attendance.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۶۱ — درخواست‌ها (مرخصی/مأموریت/مجوز/اصلاح تردد) روی لایهٔ دیتای مستقل ماژول
/// (AttendanceDbContext)؛ حسابرسی از IAuditSink (HrModuleControllerBase). شیفتِ پرسنل
/// برای بازمحاسبهٔ کارکرد با ShiftId خوانده می‌شود (Employee دیگر navigation شیفت ندارد).
/// </summary>
public class RequestsController : HrModuleControllerBase
{
    private readonly AttendanceDbContext Db;

    public RequestsController(AttendanceDbContext db, ICurrentUser me, IAuditSink audits) : base(me, audits) => Db = db;

    [HttpGet]
    [HasPerm("attendance.view")]
    public async Task<IActionResult> Index(string tab = "leaves")
    {
        ViewBag.Tab = tab;
        // فاز ۷۴ — دامنهٔ رکوردی: درخواست‌ها فقط از محدودهٔ مجاز کاربر (SQL-side)
        var scope = await this.ScopeAsync();
        // فاز ۱۱۴ — لیست کامل پرسنل حذف شد؛ انتخاب با جستجوی زندهٔ /Ui/EmpSearch
        ViewBag.CanApprove = Sess!.Has("attendance.approve");
        ViewBag.CanCorrect = Sess!.Has("corrections.approve");

        ViewBag.Leaves = await Db.LeaveRequests.Include(x => x.Employee)
            .WhereEmpScope(scope, x => x.EmployeeId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
        ViewBag.Missions = await Db.Missions.Include(x => x.Employee)
            .WhereEmpScope(scope, x => x.EmployeeId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
        ViewBag.Permits = await Db.OvertimePermits.Include(x => x.Employee)
            .WhereEmpScope(scope, x => x.EmployeeId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
        ViewBag.Corrections = await Db.CorrectionRequests.Include(x => x.Employee)
            .WhereEmpScope(scope, x => x.EmployeeId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
        return View();
    }

    // ---------- فرم‌های پاپ‌آپ (مودال AJAX) ----------
    // فاز ۱۱۴ — ActiveEmployeesAsync حذف شد: فرم‌ها با انتخابگر جستجومحور /Ui/EmpSearch کار می‌کنند

    [HttpGet]
    [HasPerm("attendance.requests.create")]
    public async Task<IActionResult> CreateLeave()
    {
        // فاز ۱۱۴ — انتخابگر جستجومحور: لیست کامل پرسنل حذف شد
        return PartialView("_LeaveForm");
    }

    [HttpGet]
    [HasPerm("attendance.requests.create")]
    public async Task<IActionResult> CreateMission()
    {
        // فاز ۱۱۴ — انتخابگر جستجومحور: لیست کامل پرسنل حذف شد
        return PartialView("_MissionForm");
    }

    [HttpGet]
    [HasPerm("attendance.requests.create")]
    public async Task<IActionResult> CreatePermit()
    {
        // فاز ۱۱۴ — انتخابگر جستجومحور: لیست کامل پرسنل حذف شد
        return PartialView("_PermitForm");
    }

    // ---------- ایجاد درخواست ----------
    [HttpPost]
    [HasPerm("attendance.requests.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLeave(string employeeId, string type, string fromDate, string toDate, decimal days, string? reason)
    {
        Db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employeeId, Type = type, FromDate = fromDate, ToDate = toDate,
            Days = days, Reason = reason, Status = "در انتظار تأیید",
        });
        await Db.SaveChangesAsync();
        await Audit("create", "leave", null, new { employeeId, type, fromDate, toDate });
        ToastOk("درخواست مرخصی ثبت شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? HrT.T("انجام شد") : HrT.T("خطا"));
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Requests?tab=leaves");
    }

    [HttpPost]
    [HasPerm("attendance.requests.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMission(string employeeId, string type, string destination, string fromDate, string toDate, string? purpose)
    {
        Db.Missions.Add(new Mission
        {
            EmployeeId = employeeId, Type = type, Destination = destination,
            FromDate = fromDate, ToDate = toDate, Purpose = purpose, Status = "در انتظار تأیید",
        });
        await Db.SaveChangesAsync();
        await Audit("create", "mission", null, new { employeeId, destination });
        ToastOk("درخواست مأموریت ثبت شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? HrT.T("انجام شد") : HrT.T("خطا"));
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Requests?tab=missions");
    }

    [HttpPost]
    [HasPerm("attendance.requests.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePermit(string employeeId, string type, string date, string fromTime, string toTime, string? reason)
    {
        Db.OvertimePermits.Add(new OvertimePermit
        {
            EmployeeId = employeeId, Type = type, Date = date,
            FromTime = fromTime, ToTime = toTime, Reason = reason, Status = "در انتظار تأیید",
        });
        await Db.SaveChangesAsync();
        await Audit("create", "permit", null, new { employeeId, type, date });
        ToastOk("مجوز ثبت شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? HrT.T("انجام شد") : HrT.T("خطا"));
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Requests?tab=permits");
    }

    // ---------- تأیید / رد ----------
    [HttpPost]
    [HasPerm("attendance.leaves.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideLeave(string id, bool approve)
    {
        var item = await Db.LeaveRequests.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        // فاز ۱۳۵ (مورد ۹) — عبور تصمیم از گردش‌کار چندمرحله‌ای (اگر جریان فعالی تعریف شده باشد)
        var gate = await ApprovalEngine.RegisterDecisionAsync(Db, ApprovalEngine.TLeave, item.Id,
            HrT.F("مرخصی {0} از {1}", item.Employee?.FullName ?? item.EmployeeId, item.FromDate),
            null, Sess!.FullName, Sess.Has, approve);
        if (gate == ApprovalGate.NotMyStep)
        {
            ToastErr(HrT.T("مرحلهٔ جاری گردش‌کار با مجوز شما قابل تصمیم نیست"));
            if (Ajax) return AjaxResult(false, HrT.T("مرحلهٔ جاری گردش‌کار با مجوز شما قابل تصمیم نیست"));
            return Redirect("/Requests");
        }
        if (gate == ApprovalGate.StepRecorded && approve)
        {
            ToastOk(HrT.T("مرحلهٔ گردش‌کار ثبت شد — تأیید نهایی پس از تکمیل مراحل بعدی"));
            if (Ajax) return AjaxResult(true, HrT.T("مرحلهٔ گردش‌کار ثبت شد — تأیید نهایی پس از تکمیل مراحل بعدی"));
            return Redirect("/Requests");
        }
        // فاز ۱۳۱ (مورد ۸ ROADMAP-ADDITIONS) — گارد تأیید دوباره: اگر درخواست قبلاً
        // تأیید شده بود، روزهای مرخصی کارت تردد و کسر مانده دوباره اعمال نمی‌شوند
        var wasApproved = item.Status == "تأیید شده";
        item.Status = approve ? "تأیید شده" : "رد شده";
        item.Approver = Sess!.FullName;
        item.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();

        // تأیید مرخصی → ثبت روزهای مرخصی در کارت تردد
        if (approve)
        {
            var days = (int)Math.Ceiling(item.Days);
            for (int i = 0; i < days; i++)
            {
                var dk = Jalali.AddDaysKey(item.FromDate, i);
                var rec = await Db.AttendanceRecords
                    .FirstOrDefaultAsync(r => r.EmployeeId == item.EmployeeId && r.Date == dk);
                if (rec is null)
                {
                    Db.AttendanceRecords.Add(new AttendanceRecord
                    {
                        EmployeeId = item.EmployeeId, Date = dk, Status = "مرخصی",
                        Note = item.Type, WorkMinutes = 0,
                    });
                }
                else if (rec.Status != "مرخصی")
                {
                    rec.Status = "مرخصی";
                    rec.Note = item.Type;
                }
            }
            // فاز ۱۳۱ (مورد ۸) — کسر خودکار از ماندهٔ مرخصی پرسنل: استحقاقی/ساعتی از
            // Annual، استعلاجی از Sick؛ بدون‌حقوق و زایمان از مانده کسر نمی‌شوند.
            // مرخصی ساعتی با مبنای ۸ ساعت کاری در روز معادل‌سازی می‌شود.
            var empL = item.Employee;
            if (!wasApproved && empL is not null)
            {
                var d = item.Type == "ساعتی" ? (item.Hours ?? 0) / 8m : item.Days;
                if (item.Type is "استحقاقی" or "ساعتی") empL.AnnualLeaveUsed += d;
                else if (item.Type == "استعلاجی") empL.SickLeaveUsed += d;
            }
            await Db.SaveChangesAsync();
        }
        await Audit(approve ? "approve" : "reject", "leave", item.Id, new { employee = item.Employee?.FullName });
        ToastOk(HrT.F("درخواست «{0}» {1}", item.Employee?.FullName ?? "", HrT.T(item.Status ?? "")));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? HrT.T("انجام شد") : HrT.T("خطا"));
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Requests?tab=leaves");
    }

    [HttpPost]
    [HasPerm("attendance.missions.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideMission(string id, bool approve)
    {
        var item = await Db.Missions.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        // فاز ۱۳۵ (مورد ۹) — عبور تصمیم از گردش‌کار چندمرحله‌ای
        var gate = await ApprovalEngine.RegisterDecisionAsync(Db, ApprovalEngine.TMission, item.Id,
            HrT.F("مأموریت {0} به {1}", item.Employee?.FullName ?? item.EmployeeId, item.Destination),
            null, Sess!.FullName, Sess.Has, approve);
        if (gate == ApprovalGate.NotMyStep)
        {
            ToastErr(HrT.T("مرحلهٔ جاری گردش‌کار با مجوز شما قابل تصمیم نیست"));
            if (Ajax) return AjaxResult(false, HrT.T("مرحلهٔ جاری گردش‌کار با مجوز شما قابل تصمیم نیست"));
            return Redirect("/Requests");
        }
        if (gate == ApprovalGate.StepRecorded && approve)
        {
            ToastOk(HrT.T("مرحلهٔ گردش‌کار ثبت شد — تأیید نهایی پس از تکمیل مراحل بعدی"));
            if (Ajax) return AjaxResult(true, HrT.T("مرحلهٔ گردش‌کار ثبت شد — تأیید نهایی پس از تکمیل مراحل بعدی"));
            return Redirect("/Requests");
        }
        item.Status = approve ? "تأیید شده" : "رد شده";
        item.Approver = Sess!.FullName;
        item.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();

        if (approve)
        {
            var days = (Jalali.DiffDays(item.FromDate, item.ToDate) + 1);
            for (int i = 0; i < days; i++)
            {
                var dk = Jalali.AddDaysKey(item.FromDate, i);
                var rec = await Db.AttendanceRecords
                    .FirstOrDefaultAsync(r => r.EmployeeId == item.EmployeeId && r.Date == dk);
                if (rec is null)
                    Db.AttendanceRecords.Add(new AttendanceRecord
                    {
                        EmployeeId = item.EmployeeId, Date = dk, Status = "مأموریت", Note = item.Destination,
                    });
                else { rec.Status = "مأموریت"; rec.Note = item.Destination; }
            }
            await Db.SaveChangesAsync();
        }
        await Audit(approve ? "approve" : "reject", "mission", item.Id, null);
        ToastOk(HrT.F("مأموریت «{0}» {1}", item.Employee?.FullName ?? "", HrT.T(item.Status ?? "")));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? HrT.T("انجام شد") : HrT.T("خطا"));
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Requests?tab=missions");
    }

    [HttpPost]
    [HasPerm("attendance.permits.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecidePermit(string id, bool approve)
    {
        var item = await Db.OvertimePermits.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        // فاز ۱۳۵ (مورد ۹) — عبور تصمیم از گردش‌کار چندمرحله‌ای
        var gate = await ApprovalEngine.RegisterDecisionAsync(Db, ApprovalEngine.TPermit, item.Id,
            HrT.F("مجوز اضافه‌کاری {0} در {1}", item.Employee?.FullName ?? item.EmployeeId, item.Date),
            null, Sess!.FullName, Sess.Has, approve);
        if (gate == ApprovalGate.NotMyStep)
        {
            ToastErr(HrT.T("مرحلهٔ جاری گردش‌کار با مجوز شما قابل تصمیم نیست"));
            if (Ajax) return AjaxResult(false, HrT.T("مرحلهٔ جاری گردش‌کار با مجوز شما قابل تصمیم نیست"));
            return Redirect("/Requests");
        }
        if (gate == ApprovalGate.StepRecorded && approve)
        {
            ToastOk(HrT.T("مرحلهٔ گردش‌کار ثبت شد — تأیید نهایی پس از تکمیل مراحل بعدی"));
            if (Ajax) return AjaxResult(true, HrT.T("مرحلهٔ گردش‌کار ثبت شد — تأیید نهایی پس از تکمیل مراحل بعدی"));
            return Redirect("/Requests");
        }
        item.Status = approve ? "تأیید شده" : "رد شده";
        item.Approver = Sess!.FullName;
        item.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit(approve ? "approve" : "reject", "permit", item.Id, null);
        ToastOk(HrT.F("مجوز «{0}» {1}", item.Employee?.FullName ?? "", HrT.T(item.Status ?? "")));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? HrT.T("انجام شد") : HrT.T("خطا"));
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Requests?tab=permits");
    }

    // ======================================================================
    // فاز ۱۳۲ (مورد ۱۱ ROADMAP-ADDITIONS) — ثبت اصلاحیهٔ تردد از سمت مدیر
    // تا امروز اصلاحیه فقط از سمت پرسنل (پرتال من) ساخته می‌شد؛ گزارش «پانچ
    // جامانده» (مورد ۲) یعنی سرپرست باید بتواند به‌جای پرسنلی که دستگاه را
    // فراموش کرده، اصلاحیه وارد کند — ثبت با همین فرم، تأیید نهایی همان
    // DecideCorrection (بازنویسی واقعی رکورد با موتور computeWork).
    // ======================================================================

    [HttpGet]
    [HasPerm("corrections.approve")]
    public async Task<IActionResult> CreateAdminCorrection()
    {
        return PartialView("_AdminCorrectionForm");
    }

    /// <summary>ثبت اصلاحیهٔ تردد به‌جای پرسنل — با مجوز corrections.approve.</summary>
    [HttpPost]
    [HasPerm("corrections.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdminCorrection(string employeeId, string date, string requestedIn, string requestedOut, string reason)
    {
        var emp = await Db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (emp is null)
        { ToastErr(HrT.T("پرسنل انتخاب‌شده یافت نشد")); if (Ajax) return AjaxResult(false, HrT.T("پرسنل انتخاب‌شده یافت نشد")); return Redirect("/Requests?tab=corrections"); }
        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(reason)
            || (string.IsNullOrWhiteSpace(requestedIn) && string.IsNullOrWhiteSpace(requestedOut)))
        {
            var err = HrT.T("تاریخ، دلیل و حداقل یکی از ورود/خروج اصلاحی الزامی است");
            ToastErr(err); if (Ajax) return AjaxResult(false, err);
            return Redirect("/Requests?tab=corrections");
        }
        Db.CorrectionRequests.Add(new CorrectionRequest
        {
            EmployeeId = emp.Id, Date = date,
            RequestedIn = ParseTime(date, requestedIn),
            RequestedOut = ParseTime(date, requestedOut),
            Reason = reason.Trim(), Status = "در انتظار تأیید",
        });
        await Db.SaveChangesAsync();
        await Audit("create", "correction", emp.Id, new { emp.PersonnelCode, date, requestedIn, requestedOut, byAdmin = true });
        ToastOk(HrT.F("اصلاحیهٔ تردد «{0}» ثبت شد و در انتظار تأیید است", emp.FullName));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? HrT.T("انجام شد") : HrT.T("خطا"));
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Requests?tab=corrections");
    }

    /// <summary>تجزیهٔ "HH:mm" شمسی/لاتین روی کلید تاریخ — همان منطق پرتال من (SelfAtt.ParseTime).</summary>
    private static DateTime? ParseTime(string dateKey, string? hhmm)
    {
        if (string.IsNullOrWhiteSpace(hhmm)) return null;
        hhmm = EssAuth.NormDigits(hhmm);
        var p = hhmm.Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m)) return null;
        if (h is < 0 or > 23 || m is < 0 or > 59) return null;
        return Jalali.ParseDateKey(dateKey).AddHours(h).AddMinutes(m);
    }

    /// <summary>تأیید اصلاح تردد — بازنویسی واقعی رکورد با موتور computeWork (PLAN P4.3).</summary>
    [HttpPost]
    [HasPerm("corrections.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideCorrection(string id, bool approve)
    {
        var item = await Db.CorrectionRequests.Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        // فاز ۱۳۵ (مورد ۹) — عبور تصمیم از گردش‌کار چندمرحله‌ای
        var gate = await ApprovalEngine.RegisterDecisionAsync(Db, ApprovalEngine.TCorrection, item.Id,
            HrT.F("اصلاح تردد {0} در {1}", item.Employee?.FullName ?? item.EmployeeId, item.Date),
            null, Sess!.FullName, Sess.Has, approve);
        if (gate == ApprovalGate.NotMyStep)
        {
            ToastErr(HrT.T("مرحلهٔ جاری گردش‌کار با مجوز شما قابل تصمیم نیست"));
            if (Ajax) return AjaxResult(false, HrT.T("مرحلهٔ جاری گردش‌کار با مجوز شما قابل تصمیم نیست"));
            return Redirect("/Requests");
        }
        if (gate == ApprovalGate.StepRecorded && approve)
        {
            ToastOk(HrT.T("مرحلهٔ گردش‌کار ثبت شد — تأیید نهایی پس از تکمیل مراحل بعدی"));
            if (Ajax) return AjaxResult(true, HrT.T("مرحلهٔ گردش‌کار ثبت شد — تأیید نهایی پس از تکمیل مراحل بعدی"));
            return Redirect("/Requests");
        }
        item.Status = approve ? "تأیید شده" : "رد شده";
        item.ReviewedBy = Sess!.FullName;
        item.UpdatedAt = DateTime.Now;

        if (approve && item.RequestedIn is not null)
        {
            var record = await Db.AttendanceRecords
                .FirstOrDefaultAsync(r => r.EmployeeId == item.EmployeeId && r.Date == item.Date);
            if (record is null)
            {
                record = new AttendanceRecord { EmployeeId = item.EmployeeId, Date = item.Date };
                Db.AttendanceRecords.Add(record);
            }
            record.CheckIn = item.RequestedIn;
            record.CheckOut = item.RequestedOut;
            // فاز ۶۱ — Employee دیگر navigation شیفت ندارد؛ شیفت با ShiftId خوانده می‌شود
            var sid = item.Employee?.ShiftId;
            Shift? empShift = null;
            if (sid is not null)
                empShift = await Db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid);
            var w = AttCalc.ComputeWork(ShiftParamsOfExt.ShiftParamsOf(empShift),
                record.CheckIn, record.CheckOut, item.Date);
            record.WorkMinutes = w.WorkMinutes;
            record.LateMinutes = w.LateMinutes;
            record.EarlyMinutes = w.EarlyMinutes;
            record.OvertimeMinutes = w.OvertimeMinutes;
            record.Status = AttCalc.StatusFromPunch(w.LateMinutes, true);
            record.Note = "اصلاح‌شده (درخواست اصلاح تردد)";
        }
        await Db.SaveChangesAsync();
        await Audit(approve ? "approve" : "reject", "correction", item.Id, new { date = item.Date });
        ToastOk(HrT.F("درخواست اصلاح «{0}» {1}", item.Employee?.FullName ?? "", HrT.T(item.Status ?? "")));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? HrT.T("انجام شد") : HrT.T("خطا"));
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Requests?tab=corrections");
    }
}


