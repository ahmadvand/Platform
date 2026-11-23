using Platform.Web.Controllers;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Attendance.Domain;
using Platform.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Attendance;

// ======================================================================
// فاز ۶۸ — بخش حضورِ پرتال «من» — از SelfController هاست به همین ماژول منتقل شد.
// مسیرها عیناً همان قبلی‌اند ([Route("Self")] + اکشن‌ها)؛ ویوها در همین اسمبلی
// کامپایل می‌شوند (Views/SelfAtt) و Layout در زمان اجرا از هاست پیدا می‌شود.
// ماژول غایب = این کنترلر اصلاً کشف نمی‌شود (بخش UI پلاگین) — گیت هاست صفحهٔ
// دوستانه می‌دهد (مانیفست self/requests در OwnedActions).
// ======================================================================
[Route("Self")]
public class SelfAttController : MvcBase
{
    private readonly IAttendanceGateway _att;

    public SelfAttController(AppDbContext db, ICurrentUser me, IAttendanceGateway att) : base(db, me)
        => _att = att;

    private async Task<Employee?> LoadMyEmployeeAsync()
    {
        if (Sess is null) return null;
        IQueryable<Employee> q = Db.Set<Employee>().Include(e => e.Department).Include(e => e.Position);
        if (Sess.Username.StartsWith("emp:"))
        {
            var code = Sess.Username[4..];   // بیرون از expression tree
            return await q.FirstOrDefaultAsync(e => e.PersonnelCode == code && e.Status == "فعال");
        }
        var empId = await Db.Users.Where(u => u.Id == Sess.Id).Select(u => u.EmployeeId).FirstOrDefaultAsync();
        if (empId is null) return null;
        return await q.FirstOrDefaultAsync(e => e.Id == empId && e.Status == "فعال");
    }

    private IActionResult NoLink() => View("NoLink");

    // ================= درخواست‌های من =================

    [HttpGet("Requests")]
    [HasPerm("self.requests")]
    public async Task<IActionResult> Requests()
    {
        var emp = await LoadMyEmployeeAsync();
        if (emp is null) return NoLink();
        ViewBag.Emp = emp;
        ViewBag.Leaves = Db.Set<LeaveRequest>().Where(x => x.EmployeeId == emp.Id)
            .OrderByDescending(x => x.CreatedAt).ToList();
        ViewBag.Missions = Db.Set<Mission>().Where(x => x.EmployeeId == emp.Id)
            .OrderByDescending(x => x.CreatedAt).ToList();
        ViewBag.Corrections = Db.Set<CorrectionRequest>().Where(x => x.EmployeeId == emp.Id)
            .OrderByDescending(x => x.CreatedAt).ToList();
        return View();
    }

    [HttpPost("CreateLeave")]
    [HasPerm("self.requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLeave(string type, string fromDate, string toDate, decimal days, int? hours, string? reason)
    {
        var emp = await LoadMyEmployeeAsync();
        if (emp is null) return ToastDone(false, "حساب شما به پرسنلی متصل نیست.", "/Self");
        if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate) || days <= 0)
            return ToastDone(false, "تاریخ و مدت مرخصی را درست وارد کنید.", "/Self/Requests");
        Db.Set<LeaveRequest>().Add(new LeaveRequest
        {
            EmployeeId = emp.Id, Type = type, FromDate = fromDate, ToDate = toDate,
            Days = days, Hours = hours, Reason = reason?.Trim(), Status = "در انتظار تأیید",
        });
        await Db.SaveChangesAsync();
        await Audit("ess_create", "leave", emp.Id, new { emp.PersonnelCode, type, fromDate, toDate });
        return ToastDone(true, "درخواست مرخصی ثبت شد و در انتظار تأیید سرپرست است.", "/Self/Requests");
    }

    [HttpPost("CreateMission")]
    [HasPerm("self.requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMission(string type, string destination, string fromDate, string toDate, string? purpose)
    {
        var emp = await LoadMyEmployeeAsync();
        if (emp is null) return ToastDone(false, "حساب شما به پرسنلی متصل نیست.", "/Self");
        if (string.IsNullOrWhiteSpace(destination) || string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            return ToastDone(false, "مقصد و بازه مأموریت را وارد کنید.", "/Self/Requests");
        Db.Set<Mission>().Add(new Mission
        {
            EmployeeId = emp.Id, Type = type, Destination = destination.Trim(),
            FromDate = fromDate, ToDate = toDate, Purpose = purpose?.Trim(), Status = "در انتظار تأیید",
        });
        await Db.SaveChangesAsync();
        await Audit("ess_create", "mission", emp.Id, new { emp.PersonnelCode, type, destination });
        return ToastDone(true, "درخواست مأموریت ثبت شد و در انتظار تأیید سرپرست است.", "/Self/Requests");
    }

    [HttpPost("CreateCorrection")]
    [HasPerm("self.requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCorrection(string date, string requestedIn, string requestedOut, string reason)
    {
        var emp = await LoadMyEmployeeAsync();
        if (emp is null) return ToastDone(false, "حساب شما به پرسنلی متصل نیست.", "/Self");
        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(reason))
            return ToastDone(false, "تاریخ و دلیل اصلاح تردد الزامی است.", "/Self/Requests");
        Db.Set<CorrectionRequest>().Add(new CorrectionRequest
        {
            EmployeeId = emp.Id, Date = date,
            RequestedIn = ParseTime(date, requestedIn),
            RequestedOut = ParseTime(date, requestedOut),
            Reason = reason.Trim(), Status = "در انتظار تأیید",
        });
        await Db.SaveChangesAsync();
        await Audit("ess_create", "correction", emp.Id, new { emp.PersonnelCode, date, requestedIn, requestedOut });
        return ToastDone(true, "درخواست اصلاح تردد ثبت شد و در انتظار تأیید سرپرست است.", "/Self/Requests");
    }

    // ================= کارکرد من =================

    [HttpGet("Attendance")]
    [HasPerm("self.view")]
    public async Task<IActionResult> Attendance(int? y, int? m)
    {
        var emp = await LoadMyEmployeeAsync();
        if (emp is null) return NoLink();
        var (jy, jm, _) = Jalali.ToJalali(DateTime.Now);
        jy = y ?? jy; jm = Math.Clamp(m ?? jm, 1, 12);
        var (start, end) = Jalali.MonthRange(jy, jm);
        var fromKey = Jalali.DateKey(start);
        var toKey = Jalali.DateKey(end.AddDays(-1));

        ViewBag.Emp = emp;
        ViewBag.Records = Db.Set<AttendanceRecord>()
            .Where(r => r.EmployeeId == emp.Id && string.Compare(r.Date, fromKey) >= 0 && string.Compare(r.Date, toKey) <= 0)
            .OrderBy(r => r.Date)
            .ToList();
        ViewBag.JY = jy; ViewBag.JM = jm;
        return View();
    }

    private static DateTime? ParseTime(string dateKey, string? hhmm)
    {
        if (string.IsNullOrWhiteSpace(hhmm)) return null;
        hhmm = EssAuth.NormDigits(hhmm);
        var p = hhmm.Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m)) return null;
        if (h is < 0 or > 23 || m is < 0 or > 59) return null;
        return Jalali.ParseDateKey(dateKey).AddHours(h).AddMinutes(m);
    }
}
