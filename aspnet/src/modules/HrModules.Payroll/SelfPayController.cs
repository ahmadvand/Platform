using Platform.Web.Controllers;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Payroll.Data;
using HrModules.Payroll.Domain;
using Platform.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Payroll;

// ======================================================================
// فاز ۶۸ — بخش حقوقِ پرتال «من» — از SelfController هاست به همین ماژول منتقل شد.
// مسیرها عیناً همان قبلی‌اند ([Route("Self")] + Payslips/Payslip/PayslipPdf)؛
// ویوها (Views/SelfPay) و موتور PDF (PayrollPdf در لایهٔ دیتای همین ماژول) هم اینجایند.
// ======================================================================
[Route("Self")]
public class SelfPayController : MvcBase
{
    private readonly IWebHostEnvironment _env;

    public SelfPayController(AppDbContext db, ICurrentUser me, IWebHostEnvironment env) : base(db, me)
        => _env = env;

    private string WebRoot() => _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");

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

    // ================= فیش حقوقی من =================

    [HttpGet("Payslips")]
    [HasPerm("self.payslips")]
    public async Task<IActionResult> Payslips()
    {
        var emp = await LoadMyEmployeeAsync();
        if (emp is null) return NoLink();
        ViewBag.Emp = emp;
        ViewBag.Slips = Db.Set<Payslip>()
            .Include(p => p.Run)
            .Where(p => p.EmployeeId == emp.Id && p.Run!.Status == "نهایی")
            .OrderByDescending(p => p.Run!.JYear).ThenByDescending(p => p.Run!.JMonth)
            .ToList();
        return View();
    }

    [HttpGet("Payslip/{id?}")]
    [HasPerm("self.payslips")]
    public async Task<IActionResult> Payslip(string id)
    {
        var emp = await LoadMyEmployeeAsync();
        if (emp is null) return NoLink();
        var slip = Db.Set<Payslip>().Include(p => p.Run).Include(p => p.Employee)
            .FirstOrDefault(p => p.Id == id && p.EmployeeId == emp.Id && p.Run!.Status == "نهایی");
        if (slip is null) return NotFound();
        ViewBag.Emp = emp;
        ViewBag.Slip = slip;
        return View();
    }

    /// <summary>دانلود فیش PDF رسمی — فقط فیش نهاییِ خود پرسنل (مالکیت‌سنجی کامل).</summary>
    [HttpGet("PayslipPdf")]
    [HasPerm("self.payslips")]
    public async Task<IActionResult> PayslipPdf(string id)
    {
        var emp = await LoadMyEmployeeAsync();
        if (emp is null) return NoLink();
        var slip = Db.Set<Payslip>()
            .Include(p => p.Lines)
            .Include(p => p.Employee).ThenInclude(e => e!.Department)
            .Include(p => p.Employee).ThenInclude(e => e!.Position)
            .Include(p => p.Run).ThenInclude(r => r!.Company)
            .FirstOrDefault(p => p.Id == id && p.EmployeeId == emp.Id && p.Run!.Status == "نهایی");
        if (slip is null) return NotFound();
        await ApplyPdfThemeAsync();
        var pdf = PayrollPdf.BuildSlip(slip, WebRoot());
        await Audit("pdf", "payslip_self", id, null);
        return File(pdf, "application/pdf",
            $"Payslip-{emp.PersonnelCode}-{slip.Run!.JYear}-{slip.Run.JMonth:00}.pdf");
    }
}
