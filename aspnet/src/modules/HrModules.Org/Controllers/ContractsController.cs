using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Org;

/// <summary>
/// فاز ۱۳۲ (مورد ۱۰ ROADMAP-ADDITIONS) — صفحهٔ قراردادها و احکام پرسنل.
/// مدل داده (Contract/Decree) از فازهای اولیه در هستهٔ مشترک بود و PreSeed هم
/// دادهٔ نمونه داشت، ولی هیچ صفحهٔ مدیریتی نداشت — با همان الگوی EmployeesController
/// (MvcBase روی AppDbContext مرکب هاست) و اسکوپ رکوردیِ دامنهٔ پرسنل.
/// </summary>
public class ContractsController(AppDbContext db, ICurrentUser me) : MvcBase(db, me)
{
    public static readonly string[] ContractTypes = { "دائم", "موقت", "پروژه‌ای", "آزمایشی" };
    public static readonly string[] DecreeTypes = { "استخدام", "ترفیع", "انتقال", "تعلیق", "بازگشت", "اصلاحیه" };

    [HttpGet]
    [HasPerm("employees.view")]
    public async Task<IActionResult> Index(string? q)
    {
        // فاز ۷۴ — دامنهٔ رکوردی: قرارداد/حکم فقط از پرسنلِ محدودهٔ مجاز کاربر
        var scope = await this.ScopeAsync();
        var contractsQ = Db.Set<Contract>().Include(c => c.Employee).ThenInclude(e => e!.Department)
            .WhereEmpScope(scope, c => c.EmployeeId).AsQueryable();
        var decreesQ = Db.Set<Decree>().Include(d => d.Employee)
            .WhereEmpScope(scope, d => d.EmployeeId).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            contractsQ = contractsQ.Where(c => c.Employee!.FullName.Contains(t)
                || c.Employee.PersonnelCode.Contains(t) || c.ContractNo.Contains(t));
            decreesQ = decreesQ.Where(d => d.Employee!.FullName.Contains(t)
                || d.Employee.PersonnelCode.Contains(t) || d.DecreeNo.Contains(t));
        }
        ViewBag.Contracts = await contractsQ.OrderByDescending(c => c.StartDate).ToListAsync();
        ViewBag.Decrees = await decreesQ.OrderByDescending(d => d.Date).ToListAsync();
        ViewBag.Q = q ?? "";
        return View();
    }

    /// <summary>فرم مودال قرارداد جدید.</summary>
    [HttpGet]
    [HasPerm("employees.edit")]
    public IActionResult CreateContract() => PartialView("_ContractForm");

    [HttpPost]
    [HasPerm("employees.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContract(string employeeId, string type, string contractNo, string startDate, string? endDate, decimal salary)
    {
        var emp = await Db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (emp is null)
        { ToastErr("پرسنل انتخاب‌شده یافت نشد"); if (Ajax) return AjaxResult(false, "پرسنل انتخاب‌شده یافت نشد"); return Redirect("/Org/Contracts"); }
        if (!DateTime.TryParse(startDate, out var s))
        { ToastErr("تاریخ شروع نامعتبر است"); if (Ajax) return AjaxResult(false, "تاریخ شروع نامعتبر است"); return Redirect("/Org/Contracts"); }
        DateTime? e2 = null;
        if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var en)) e2 = en;
        var c = new Contract
        {
            EmployeeId = emp.Id, Type = type, ContractNo = contractNo.Trim(),
            StartDate = s, EndDate = e2, Salary = salary, Status = "فعال",
        };
        Db.Set<Contract>().Add(c);
        await Db.SaveChangesAsync();
        await Audit("create", "contract", c.Id, new { emp.PersonnelCode, c.Type, c.ContractNo, salary });
        ToastOk(HrT.F("قرارداد «{0}» برای «{1}» ثبت شد", c.ContractNo, emp.FullName));
        if (Ajax) return AjaxResult(true, HrT.F("قرارداد «{0}» ثبت شد", c.ContractNo));
        return Redirect("/Org/Contracts");
    }

    /// <summary>فرم مودال حکم جدید.</summary>
    [HttpGet]
    [HasPerm("employees.edit")]
    public IActionResult CreateDecree() => PartialView("_DecreeForm");

    [HttpPost]
    [HasPerm("employees.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDecree(string employeeId, string type, string decreeNo, string date, string? content)
    {
        var emp = await Db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (emp is null)
        { ToastErr("پرسنل انتخاب‌شده یافت نشد"); if (Ajax) return AjaxResult(false, "پرسنل انتخاب‌شده یافت نشد"); return Redirect("/Org/Contracts"); }
        if (!DateTime.TryParse(date, out var dt))
        { ToastErr("تاریخ حکم نامعتبر است"); if (Ajax) return AjaxResult(false, "تاریخ حکم نامعتبر است"); return Redirect("/Org/Contracts"); }
        var d = new Decree
        {
            EmployeeId = emp.Id, Type = type, DecreeNo = decreeNo.Trim(),
            Date = dt, Content = string.IsNullOrWhiteSpace(content) ? null : content.Trim(),
            Acknowledged = false,
        };
        Db.Set<Decree>().Add(d);
        await Db.SaveChangesAsync();
        await Audit("create", "decree", d.Id, new { emp.PersonnelCode, d.Type, d.DecreeNo });
        ToastOk(HrT.F("حکم «{0}» برای «{1}» ثبت شد", d.DecreeNo, emp.FullName));
        if (Ajax) return AjaxResult(true, HrT.F("حکم «{0}» ثبت شد", d.DecreeNo));
        return Redirect("/Org/Contracts");
    }

    /// <summary>اقرار پرسنل به ابلاغ حکم — پرچم Acknowledged.</summary>
    [HttpPost]
    [HasPerm("employees.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AckDecree(string id)
    {
        var d = await Db.Set<Decree>().Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (!d.Acknowledged)
        {
            d.Acknowledged = true;
            await Db.SaveChangesAsync();
            await Audit("ack", "decree", d.Id, new { employee = d.Employee?.FullName, no = d.DecreeNo });
        }
        ToastOk(HrT.F("اقرار حکم «{0}» ثبت شد", d.DecreeNo));
        if (Ajax) return AjaxResult(true, HrT.F("اقرار حکم «{0}» ثبت شد", d.DecreeNo));
        return Redirect("/Org/Contracts");
    }
}
