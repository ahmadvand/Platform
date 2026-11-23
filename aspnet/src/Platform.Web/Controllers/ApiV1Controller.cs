using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Web.Data;
using Platform.Web.Services;
using System.Text;

namespace Platform.Web.Controllers;

// ============================================================
// فاز ۱۳۵ (مورد ۱۹ ROADMAP-ADDITIONS) — API توکن‌دار عمومی برای سامانه‌های دیگر
// فقط GET و فقط با هدر X-Api-Token (توکن‌ها از /ApiTokens ساخته می‌شوند؛ در DB
// فقط هش می‌نشیند). هاست به ماژول‌ها ارجاع کامپایل‌زمانی ندارد → کوئری‌ها با
// SqlQuery روی جداول مشترک (Employees/AttendanceRecords/LeaveRequests/...)
// بدون نوع ماژول اجرا می‌شوند.
// ============================================================

public record ApiEmpRow(string PersonnelCode, string FullName, string Department, string Company, string Shift, string Status);
public record ApiAttRow(string PersonnelCode, string Date, string? CheckIn, string? CheckOut, int WorkMinutes, int LateMinutes, int OvertimeMinutes, string Status);
public record ApiPendingRow(string Type, int Count);

public class ApiV1Controller : Controller
{
    private readonly AppDbContext Db;

    public ApiV1Controller(AppDbContext db) => Db = db;

    /// <summary>اعتبارسنجی توکن — خروجی null یعنی مجاز؛ در غیر این صورت پاسخ خطا.</summary>
    private async Task<(ApiToken? Token, IActionResult? Error)> AuthorizeAsync()
    {
        var raw = Request.Headers["X-Api-Token"].FirstOrDefault()
                  ?? Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
            return (null, UnauthorizedJson("توکن ارائه نشده — هدر X-Api-Token لازم است"));
        var hash = ApiTokensController.Sha256(raw.Trim());
        var t = await Db.ApiTokens.FirstOrDefaultAsync(x => x.TokenHash == hash);
        if (t is null || t.Revoked)
            return (null, UnauthorizedJson("توکن نامعتبر یا باطل‌شده"));
        t.LastUsedAt = DateTime.Now;
        t.RequestCount++;
        await Db.SaveChangesAsync();
        return (t, null);
    }

    private static IActionResult UnauthorizedJson(string msg)
        => new JsonResult(new { error = msg }) { StatusCode = 401 };

    private string? CoFilter(ApiToken? t, out string companyId)
    {
        companyId = "";
        if (t?.ScopeCompanyId is { } sc && !string.IsNullOrWhiteSpace(sc)) { companyId = sc; return null; }
        return null;
    }

    /// <summary>سلامت API — بدون نیاز به داده، فقط احراز توکن.</summary>
    [HttpGet("/api/v1/ping")]
    public async Task<IActionResult> Ping()
    {
        var (t, err) = await AuthorizeAsync();
        if (err != null) return err;
        return Json(new { ok = true, version = BuildInfo.Version, consumer = t!.Name, now = DateTime.Now });
    }

    /// <summary>فهرست پرسنل فعال (با محدودیت شرکتی توکن).</summary>
    [HttpGet("/api/v1/employees")]
    public async Task<IActionResult> Employees()
    {
        var (t, err) = await AuthorizeAsync();
        if (err != null) return err;
        var co = t!.ScopeCompanyId;
        var q = Db.Database.SqlQuery<ApiEmpRow>($@"
            SELECT e.PersonnelCode AS PersonnelCode,
                   e.FirstName || ' ' || e.LastName AS FullName,
                   IFNULL(d.Name, '') AS Department,
                   IFNULL(c.Name, '') AS Company,
                   IFNULL(s.Name, '') AS Shift,
                   e.Status AS Status
            FROM Employees e
            LEFT JOIN Departments d ON e.DepartmentId = d.Id
            LEFT JOIN Companies c ON e.CompanyId = c.Id
            LEFT JOIN Shifts s ON e.ShiftId = s.Id
            WHERE e.Status = 'فعال'
              AND (NULLIF({co}, '') IS NULL OR e.CompanyId = {co})
            ORDER BY e.PersonnelCode
            LIMIT 5000");
        var rows = await q.ToListAsync();
        return Json(new { count = rows.Count, rows });
    }

    /// <summary>رکوردهای تردد در بازه (کلیدهای میلادی yyyy-MM-dd؛ سقف ۲۰۰۰ ردیف).</summary>
    [HttpGet("/api/v1/attendance")]
    public async Task<IActionResult> Attendance(string? from, string? to, string? personnelCode)
    {
        var (t, err) = await AuthorizeAsync();
        if (err != null) return err;
        var f = from ?? DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
        var tt = to ?? DateTime.Today.ToString("yyyy-MM-dd");
        var co = t!.ScopeCompanyId;
        var pc = personnelCode ?? "";
        var rows = await Db.Database.SqlQuery<ApiAttRow>($@"
            SELECT e.PersonnelCode AS PersonnelCode, a.Date AS Date,
                   a.CheckIn AS CheckIn, a.CheckOut AS CheckOut,
                   a.WorkMinutes AS WorkMinutes, a.LateMinutes AS LateMinutes,
                   a.OvertimeMinutes AS OvertimeMinutes, a.Status AS Status
            FROM AttendanceRecords a
            JOIN Employees e ON a.EmployeeId = e.Id
            WHERE a.Date >= {f} AND a.Date <= {tt}
              AND (NULLIF({pc}, '') IS NULL OR e.PersonnelCode = {pc})
              AND (NULLIF({co}, '') IS NULL OR e.CompanyId = {co})
            ORDER BY a.Date, e.PersonnelCode
            LIMIT 2000").ToListAsync();
        return Json(new { from = f, to = tt, count = rows.Count, rows });
    }

    /// <summary>شمار درخواست‌های در انتظار تأیید به تفکیک نوع.</summary>
    [HttpGet("/api/v1/requests/pending")]
    public async Task<IActionResult> PendingRequests()
    {
        var (t, err) = await AuthorizeAsync();
        if (err != null) return err;
        var rows = await Db.Database.SqlQuery<ApiPendingRow>($@"
            SELECT 'مرخصی' AS Type, COUNT(*) AS Count FROM LeaveRequests WHERE Status = 'در انتظار تأیید'
            UNION ALL SELECT 'مأموریت', COUNT(*) FROM Missions WHERE Status = 'در انتظار تأیید'
            UNION ALL SELECT 'اصلاح تردد', COUNT(*) FROM CorrectionRequests WHERE Status = 'در انتظار تأیید'").ToListAsync();
        return Json(new { rows });
    }
}
