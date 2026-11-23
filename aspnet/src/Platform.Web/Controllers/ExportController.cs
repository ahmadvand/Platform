using ClosedXML.Excel;
using Platform.Web.Data;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>خروجی اکسل (PLAN P4.6) — ClosedXML خوداتکا، بدون CDN.
/// فاز ۶۹ — ردیف‌های پرسنل از قرارداد IPeopleDirectory (پلاگین org) و نام شیفت‌ها از
/// قرارداد IAttendanceGateway (پلاگین حضور)؛ ماژول غایب = ردیف خالی/بدون نام شیفت.</summary>
public class ExportController : MvcBase
{
    public ExportController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    [HttpGet]
    [HasPerm("reports.export")]
    public async Task<IActionResult> Employees()
    {
        var people = HttpContext.RequestServices.GetRequiredService<IPeopleDirectory>();
        var rows = await people.ExportRowsAsync();
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Employees");
        ws.RightToLeft = HrT.Rtl; // فاز ۸۱ — جهت شیت از زبان جاری
        var headers = new[] { HrT.T("کد پرسنلی"), HrT.T("نام"), HrT.T("نام خانوادگی"), HrT.T("کد ملی"), HrT.T("واحد"), HrT.T("پست"), HrT.T("نوع اشتغال"), HrT.T("شیفت"), HrT.T("حقوق پایه"), HrT.T("مانده مرخصی"), HrT.T("وضعیت"), HrT.T("تاریخ استخدام") };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        int row = 2;
        foreach (var e in rows)
        {
            ws.Cell(row, 1).Value = e.PersonnelCode;
            ws.Cell(row, 2).Value = e.FirstName;
            ws.Cell(row, 3).Value = e.LastName;
            ws.Cell(row, 4).Value = e.NationalId;
            ws.Cell(row, 5).Value = e.Department;
            ws.Cell(row, 6).Value = e.Position;
            ws.Cell(row, 7).Value = HrT.T(e.EmploymentType);
            ws.Cell(row, 8).Value = e.Shift;
            ws.Cell(row, 9).Value = (double)e.BaseSalary;
            ws.Cell(row, 10).Value = (double)e.LeaveRemaining;
            ws.Cell(row, 11).Value = HrT.T(e.Status);
            ws.Cell(row, 12).Value = e.HireDate;
            row++;
        }
        ws.Columns().AdjustToContents();
        await Audit("export", "employees", null, new { count = rows.Count });
        return Xlsx(wb, "Employees.xlsx");
    }

    [HttpGet]
    [HasPerm("reports.export")]
    public async Task<IActionResult> Attendance(string? date)
    {
        // خروجی تردد ∈ ماژول حضور (قرارداد؛ غایب = ردیف خالی)
        var att = HttpContext.RequestServices.GetRequiredService<IAttendanceGateway>();
        var dk = string.IsNullOrEmpty(date) ? Jalali.DateKey(DateTime.Now) : date;
        var records = await att.ExportDayAsync(dk);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Attendance");
        ws.RightToLeft = HrT.Rtl; // فاز ۸۱ — جهت شیت از زبان جاری
        var headers = new[] { HrT.T("کد پرسنلی"), HrT.T("نام و نام خانوادگی"), HrT.T("تاریخ"), HrT.T("ورود"), HrT.T("خروج"), HrT.T("کارکرد"), HrT.T("دیرکرد"), HrT.T("تعجیل"), HrT.T("اضافه‌کاری"), HrT.T("وضعیت") };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        int row = 2;
        foreach (var r in records)
        {
            ws.Cell(row, 1).Value = r.PersonnelCode;
            ws.Cell(row, 2).Value = r.FullName;
            ws.Cell(row, 3).Value = dk;
            ws.Cell(row, 4).Value = r.CheckIn?.ToString("HH:mm:ss") ?? "";
            ws.Cell(row, 5).Value = r.CheckOut?.ToString("HH:mm:ss") ?? "";
            ws.Cell(row, 6).Value = r.WorkMinutes;
            ws.Cell(row, 7).Value = r.LateMinutes;
            ws.Cell(row, 8).Value = r.EarlyMinutes;
            ws.Cell(row, 9).Value = r.OvertimeMinutes;
            ws.Cell(row, 10).Value = HrT.T(r.Status);
            row++;
        }
        ws.Columns().AdjustToContents();
        await Audit("export", "attendance", dk, new { count = records.Count });
        return Xlsx(wb, $"Attendance-{dk}.xlsx");
    }

    private IActionResult Xlsx(XLWorkbook wb, string fileName)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
