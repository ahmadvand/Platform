using Platform.Web.Data;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// پرتال من (تسک ۲۳ — ادغام پرتال خودخدمتی در پنل یکپارچه) — خانهٔ پرتال.
/// فاز ۶۹ — پروفایل پرسنلی از قرارداد IPeopleDirectory (پلاگین org؛ غایب = صفحهٔ بدون پیوند)
/// و کارکرد/درخواست‌ها از قرارداد IAttendanceGateway (پلاگین حضور؛ غایب = صفرها).
/// </summary>
public class SelfController : MvcBase
{
    private readonly IAttendanceGateway _att;
    private readonly IPeopleDirectory _people;

    public SelfController(AppDbContext db, ICurrentUser me, IAttendanceGateway att, IPeopleDirectory people)
        : base(db, me) { _att = att; _people = people; }

    // ================= خانه من =================

    [HttpGet]
    [HasPerm("self.view")]
    public async Task<IActionResult> Index()
    {
        if (Sess is null) return Redirect("/Auth/Login");

        // پرسنل متصل به حساب جاری: کاربرهای خودکار پرتال (emp:کد) از روی نام کاربری،
        // کاربرهای عادی از اتصال EmployeeId در صفحه کاربران — هر دو از قرارداد
        string? code = Sess.Username.StartsWith("emp:") ? Sess.Username[4..] : null;   // بیرون از expression tree
        string? linkedId = code is null
            ? await Db.Users.Where(u => u.Id == Sess.Id).Select(u => u.EmployeeId).FirstOrDefaultAsync()
            : null;
        var emp = await _people.SelfProfileAsync(code, linkedId);
        if (emp is null) return View("NoLink");

        var (jy, jm, _) = Jalali.ToJalali(DateTime.Now);
        var (start, end) = Jalali.MonthRange(jy, jm);
        var fromKey = Jalali.DateKey(start);
        var toKey = Jalali.DateKey(end.AddDays(-1));

        // کارکرد/درخواست‌های ماه ∈ پلاگین حضور (قرارداد؛ غایب = صفرها)
        var att = await _att.SelfHomeAsync(Db, emp.Id, fromKey, toKey);

        ViewBag.Emp = emp;
        ViewBag.JMonth = $"{Jalali.MonthName(jm)} {jy}";
        ViewBag.Present = att.Present;
        ViewBag.Late = att.Late;
        ViewBag.Absent = att.Absent;
        ViewBag.LeaveDays = att.LeaveDays;
        ViewBag.WorkMinutes = att.WorkMinutes;
        ViewBag.Overtime = att.Overtime;
        ViewBag.Recent = att.Recent;
        ViewBag.Pending = att.Pending;

        // فاز ۱۲۶ — درخواست‌های اخیر برای جدول خانهٔ پرتال (گیت‌وی حضور؛ ماژول غایب = تهی از fallback)
        var reqs = await _att.SelfRequestsAsync(Db, emp.Id);
        ViewBag.ReqRows = reqs.Rows;
        return View();
    }
}
