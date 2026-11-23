using ClosedXML.Excel;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Payroll.Data;
using HrModules.Payroll.Domain;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// حقوق و دستمزد داینامیک چندشرکتی (فاز ۲۴):
/// لیست حقوق به تفکیک شرکت، فرمول‌ساز اقلام (PayItem)، پارامترهای سالانه،
/// تنظیمات روش محاسبه (ماهانه/کارکردی + اولویت منبع تردد) و آزمایش فرمول.
/// فاز ۶۱ — تفکیک لایهٔ دیتا: PayrollDbContext اختصاصی ماژول + HrBaseDbContext (دادهٔ پایه)
/// + IAttendanceGateway (دادهٔ کارکرد/شیفت از ماژول حضور) + IAuditSink (حسابرسی هاست).
/// </summary>
public class PayrollController : HrModuleControllerBase
{
    public static readonly string[] ItemKinds = { "مزیت", "کسور" };
    public static readonly string[] ScopeTypes = { "همه", "دپارتمان", "پست", "شیفت", "پرسنل", "استخدام" };
    private static readonly string[] TaxJsonSample =
        { "[{\"to\":800000000,\"rate\":10},{\"to\":1000000000,\"rate\":15},{\"to\":0,\"rate\":30}]" };

    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment Env;
    private readonly PayrollDbContext Db;
    // فاز ۷۳/۷۴ — Department/Position ∈ خانوادهٔ org؛ فقط در مدل «مرکب» (AppDbContext) حاضرند —
    // HrBaseDbContext فاقد آن‌هاست و Set<Department>() روی آن می‌شکند. MINIMAL بدون org = لیست خالی.
    private readonly AppDbContext Host;
    private readonly IAttendanceGateway Att;

    public PayrollController(PayrollDbContext db, IAttendanceGateway att, AppDbContext host,
        ICurrentUser me, IAuditSink audits, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env) : base(me, audits)
    {
        Db = db;
        Att = att;
        Host = host;
        Env = env;
    }

    private string WebRoot() => Env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");

    // ===================== فاز ۷۴ — دامنهٔ رکوردی حقوق و دستمزد =====================
    // company/id درخواستی بیرون دامنهٔ کاربر = نامعتبر؛ فیش‌ها/ردیف‌ها با دامنهٔ پرسنل فیلتر می‌شوند.

    /// <summary>شناسهٔ شرکت معتبر از دید کاربر — null = بیرون دامنه (یا کاربر بدون محدودیت شرکت و id null).</summary>
    private static string? ScopedCompany(string? companyId, UserScope scope) =>
        companyId is null || scope.All || scope.Companies.Contains(companyId) ? companyId : null;

    /// <summary>فهرست شرکت‌های مجاز کاربر (برای dropdown ها).</summary>
    private static List<Company> AllowedCompanies(List<Company> all, UserScope scope) =>
        scope.All || scope.Companies.Count == 0 ? all : all.Where(c => scope.Companies.Contains(c.Id)).ToList();

    /// <summary>آیا شرکت در دامنهٔ مجاز کاربر است؟ (عملیات نوشتن/خواندن رکورد یک شرکت)</summary>
    private async Task<bool> CompanyAllowedAsync(string? companyId)
    {
        var s = await this.ScopeAsync();
        return s.CoAllowed(companyId);
    }

    /// <summary>حساب محدود به واحد/شخص است؟ — عملیات کل-شرکتی (محاسبه/نهایی‌سازی سالانه) برای او مجاز نیست.</summary>
    private static bool EmpLimited(UserScope s) =>
        !s.All && (s.Depts.Count > 0 || s.EmpIds is { Count: > 0 } || s.SelfEmpId != "");

    // ===================== لیست حقوق =====================

    [HttpGet]
    [HasPerm("payroll.view")]
    public async Task<IActionResult> Index(string? runId, string? companyId)
    {
        // فاز ۷۴ — دامنهٔ رکوردی: لیست‌های شرکت‌های مجاز + فیش‌های محدودهٔ پرسنل کاربر
        var scope = await this.ScopeAsync();
        companyId = ScopedCompany(companyId, scope);
        ViewBag.Companies = AllowedCompanies(await Db.Companies.OrderBy(c => c.Code).ToListAsync(), scope);
        ViewBag.CompanyId = companyId;
        var runs = await Db.PayrollRuns
            .Include(r => r.Company)
            .Include(r => r.Payslips)
            .OrderByDescending(r => r.JYear).ThenByDescending(r => r.JMonth)
            .ToListAsync();
        if (!string.IsNullOrEmpty(companyId)) runs = runs.Where(r => r.CompanyId == companyId).ToList();
        if (!scope.All) runs = runs
            .Where(r => scope.Companies.Contains(r.CompanyId ?? ""))
            .Select(r => { r.Payslips = r.Payslips.Where(p => scope.EmpAllowed(p.EmployeeId)).ToList(); r.TotalNet = r.Payslips.Sum(p => p.NetPay); return r; })
            .Where(r => !(!scope.All && scope.EmpIds is { Count: 0 } && r.Payslips.Count == 0) || true)
            .ToList();
        ViewBag.Runs = runs;
        ViewBag.RunId = runId;
        var run = runId is null ? null : await Db.PayrollRuns
            .Include(r => r.Company)
            .Include(r => r.Payslips).ThenInclude(p => p.Employee)
            .FirstOrDefaultAsync(r => r.Id == runId);
        ViewBag.Run = run;
        var now = Jalali.ToJalali(DateTime.Now);
        ViewBag.DefaultY = now.Y;
        ViewBag.DefaultM = now.M;
        return View();
    }

    /// <summary>محاسبه لیست حقوق یک شرکت برای یک ماه جلالی — موتور داینامیک با اقلام فرمول‌محور.</summary>
    [HttpPost]
    [HasPerm("payroll.runs.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string companyId, int jYear, int jMonth, string? note)
    {
        var scope = await this.ScopeAsync();
        if (!scope.CoAllowed(companyId))
            return ToastDone(false, "این شرکت خارج از دامنهٔ دسترسی شماست", "/Payroll");
        var company = await Db.Companies.FindAsync(companyId);
        if (company is null)
            return ToastDone(false, "شرکت انتخاب‌شده نامعتبر است", "/Payroll");
        if (jMonth < 1 || jMonth > 12)
            return ToastDone(false, "ماه جلالی نامعتبر است", "/Payroll");
        if (await Db.PayrollRuns.AnyAsync(r => r.CompanyId == companyId && r.JYear == jYear && r.JMonth == jMonth))
            return ToastDone(false, HrT.F("برای «{0}» در {1} {2} قبلاً لیست حقوق ایجاد شده است", company.Name, Jalali.MonthName(jMonth), HrT.Digits(jYear.ToString())), "/Payroll");

        try
        {
            var (run, slips) = await PayrollEngine.CalculateCompanyAsync(Db, Att, company, jYear, jMonth, Sess!.FullName ?? "");
            run.Note = note;
            Db.PayrollRuns.Add(run);
            Db.Payslips.AddRange(slips);
            await Db.SaveChangesAsync();
            await Audit("create", "payroll_run", run.Id,
                new { company = company.Name, jYear, jMonth, count = slips.Count, net = run.TotalNet });
            return ToastDone(true,
                HrT.F("لیست حقوق {0} {1} «{2}» برای {3} پرسنل محاسبه شد", HrT.Rtl ? Jalali.MonthName(jMonth) : HrT.Num(jMonth), HrT.Num(jYear), company.Name, HrT.Num(slips.Count)),
                $"/Payroll?runId={run.Id}");
        }
        catch (PayFormulaException ex)
        {
            return ToastDone(false, HrT.F("محاسبه متوقف شد: {0}", ex.Message), "/Payroll");
        }
        catch (Exception ex)
        {
            return ToastDone(false, HrT.F("خطا در محاسبه: {0}", ex.Message), "/Payroll");
        }
    }

    [HttpPost]
    [HasPerm("payroll.runs.finalize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalize(string id)
    {
        var run = await Db.PayrollRuns.FindAsync(id);
        if (run is null) return NotFound();
        if (!await CompanyAllowedAsync(run.CompanyId))
            return ToastDone(false, "این لیست حقوق خارج از دامنهٔ دسترسی شماست", "/Payroll");
        run.Status = "نهایی";
        run.FinalizedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("finalize", "payroll_run", id, new { run.JYear, run.JMonth });
        return ToastDone(true, HrT.T("لیست حقوق نهایی شد"), $"/Payroll?runId={id}");
    }

    [HttpPost]
    [HasPerm("payroll.runs.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var run = await Db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Id == id);
        if (run is null) return NotFound();
        if (!await CompanyAllowedAsync(run.CompanyId))
            return ToastDone(false, "این لیست حقوق خارج از دامنهٔ دسترسی شماست", "/Payroll");
        if (run.Status == "نهایی")
            return ToastDone(false, "لیست نهایی‌شده قابل حذف نیست", "/Payroll");
        Db.PayrollRuns.Remove(run);
        await Db.SaveChangesAsync();
        await Audit("delete", "payroll_run", id, new { run.JYear, run.JMonth });
        return ToastDone(true, "لیست حقوق حذف شد", "/Payroll");
    }

    /// <summary>جزئیات خطی یک فیش — مودال AJAX (اقلام مزایا و کسور).</summary>
    [HttpGet]
    [HasPerm("payroll.view")]
    public async Task<IActionResult> Slip(string id)
    {
        var slip = await Db.Payslips
            .Include(p => p.Lines)
            .Include(p => p.Employee)
            .Include(p => p.Run).ThenInclude(r => r!.Company)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (slip is null) return NotFound();
        var slipScope = await this.ScopeAsync();
        if (!slipScope.EmpAllowed(slip.EmployeeId))
            return ToastDone(false, "این فیش خارج از دامنهٔ دسترسی شماست", "/Payroll");
        return PartialView("_SlipLines", slip);
    }

    /// <summary>خروجی اکسل فیش‌های حقوقی (ClosedXML — خوداتکا).</summary>
    [HttpGet]
    [HasPerm("reports.export")]
    public async Task<IActionResult> Export(string runId)
    {
        var run = await Db.PayrollRuns.Include(r => r.Company)
            .Include(r => r.Payslips).ThenInclude(p => p.Employee)
            .FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null) return NotFound();
        var xScope = await this.ScopeAsync();
        if (!xScope.CoAllowed(run.CompanyId))
            return ToastDone(false, "این لیست حقوق خارج از دامنهٔ دسترسی شماست", "/Payroll");
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Payslips");
        ws.RightToLeft = true;
        var headers = new[] { "شرکت", "کد پرسنلی", "نام و نام خانوادگی", "حقوق پایه", "اضافه‌کاری", "مزایا", "کسور غیبت", "بیمه", "مالیات", "خالص پرداختی", "کارکرد (دقیقه)", "اضافه‌کاری (دقیقه)", "غیبت (روز)" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        int row = 2;
        foreach (var p in run.Payslips.Where(x => xScope.EmpAllowed(x.EmployeeId)).OrderBy(x => x.Employee!.PersonnelCode))
        {
            ws.Cell(row, 1).Value = run.Company?.Name ?? "—";
            ws.Cell(row, 2).Value = p.Employee!.PersonnelCode;
            ws.Cell(row, 3).Value = p.Employee.FullName;
            ws.Cell(row, 4).Value = (double)p.BaseSalary;
            ws.Cell(row, 5).Value = (double)p.OvertimePay;
            ws.Cell(row, 6).Value = (double)p.Allowances;
            ws.Cell(row, 7).Value = (double)p.AbsentDeduct;
            ws.Cell(row, 8).Value = (double)p.Insurance;
            ws.Cell(row, 9).Value = (double)p.Tax;
            ws.Cell(row, 10).Value = (double)p.NetPay;
            ws.Cell(row, 11).Value = p.WorkedMin;
            ws.Cell(row, 12).Value = p.OvertimeMin;
            ws.Cell(row, 13).Value = p.AbsentDays;
            row++;
        }
        ws.Columns().AdjustToContents();
        var fileName = $"Payroll-{run.JYear}-{run.JMonth:00}.xlsx";
        await Audit("export", "payroll_run", runId, new { fileName });
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // ===================== فرمول‌ساز اقلام =====================

    private async Task<List<Company>> CompaniesAsync() =>
        await Db.Companies.OrderBy(c => c.Code).ToListAsync();

    // ===================== فاز ۷۳ — دسترسی امن به چارت سازمانی (خانوادهٔ org) =====================
    // Department/Position فقط در مدل مرکب حاضرند؛ اگر ماژول org نصب نباشد لیست خالی برمی‌گردد.
    private async Task<List<Department>> DeptsAsync() =>
        Host.Model.FindEntityType(typeof(Department)) is null
            ? new List<Department>()
            : await Host.Set<Department>().OrderBy(d => d.Name).ToListAsync();

    private async Task<List<Position>> PositionsAsync(bool withDept)
    {
        if (Host.Model.FindEntityType(typeof(Position)) is null) return new List<Position>();
        var q = Host.Set<Position>().AsQueryable();
        if (withDept) q = q.Include(p => p.Department);
        return await (withDept ? q.OrderBy(p => p.Title) : q.OrderBy(p => p.Title)).ToListAsync();
    }

    private async Task FillItemFormBagsAsync(string companyId, PayItem? item)
    {
        var bagScope = await this.ScopeAsync();
        ViewBag.Companies = AllowedCompanies(await CompaniesAsync(), bagScope);
        ViewBag.CompanyId = companyId;
        ViewBag.Depts = await DeptsAsync();
        ViewBag.Positions = await PositionsAsync(withDept: true);
        ViewBag.Shifts = (await Att.AllShiftsAsync()).OrderBy(s => s.Name).ToList();
        ViewBag.Emps = await Db.Employees
            .WhereEmpTableScope(bagScope)
            .OrderBy(e => e.PersonnelCode).ToListAsync();
        // فاز ۴۴ — کدهای اقلام همین شرکت برای پالت «اقلام» سازندهٔ بصری فرمول (قلم('کد'))
        ViewBag.ItemCodes = await Db.PayItems.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Kind).ThenBy(x => x.Order).ThenBy(x => x.Code)
            .Select(x => new { c = x.Code, t = x.Title })
            .ToListAsync();
        ViewBag.IsEdit = item is not null;
    }

    [HttpGet]
    [HasPerm("payroll.view")]
    public async Task<IActionResult> Items(string? companyId)
    {
        // فاز ۷۴ — دامنهٔ رکوردی: شرکت‌ها و پرسنلِ فقط از محدودهٔ مجاز
        var scope = await this.ScopeAsync();
        var companies = AllowedCompanies(await CompaniesAsync(), scope);
        ViewBag.Companies = companies;
        companyId = ScopedCompany(companyId, scope) ?? companies.FirstOrDefault()?.Id;
        ViewBag.CompanyId = companyId;
        ViewBag.Depts = await DeptsAsync();
        ViewBag.Positions = await PositionsAsync(withDept: false);
        ViewBag.Shifts = (await Att.AllShiftsAsync()).OrderBy(s => s.Name).ToList();
        ViewBag.Emps = await Db.Employees
            .WhereEmpTableScope(scope)
            .OrderBy(e => e.PersonnelCode).ToListAsync();
        var items = await Db.PayItems
            .Where(i => i.CompanyId == companyId)
            .OrderBy(i => i.Kind).ThenBy(i => i.Order).ThenBy(i => i.Code)
            .ToListAsync();
        return View(items);
    }

    /// <summary>فهرست پرسنل یک شرکت — سلکت آزمایش فرمول در فرمول‌ساز.</summary>
    [HttpGet("/api/payroll/employees")]
    [HasPerm("payroll.view")]
    public async Task<IActionResult> EmployeesOf(string? companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return Json(new object[0]);
        var ofScope = await this.ScopeAsync();
        if (!ofScope.CoAllowed(companyId)) return Json(new object[0]);
        var list = await Db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Status == "فعال")
            .WhereEmpTableScope(ofScope)
            .OrderBy(e => e.PersonnelCode)
            .Select(e => new { id = e.Id, label = e.PersonnelCode + " — " + e.FullName })
            .ToListAsync();
        return Json(list);
    }

    [HttpGet]
    [HasPerm("payroll.items.edit")]
    public async Task<IActionResult> ItemCreate(string companyId)
    {
        await FillItemFormBagsAsync(companyId ?? "", null);
        return FormView("_ItemForm", new PayItem { CompanyId = companyId ?? "", Kind = "مزیت", ScopeType = "همه", Order = 100 });
    }

    [HttpGet]
    [HasPerm("payroll.items.edit")]
    public async Task<IActionResult> ItemEdit(string id)
    {
        var item = await Db.PayItems.FindAsync(id);
        if (item is null) return NotFound();
        await FillItemFormBagsAsync(item.CompanyId, item);
        return FormView("_ItemForm", item);
    }

    [HttpPost]
    [HasPerm("payroll.items.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ItemSave(string? id, string companyId, string code, string title,
        string kind, string formula, bool taxable, bool insurable, bool continuous,
        string scopeType, string? scopeId, int priority, int order, bool active)
    {
        if (!await CompanyAllowedAsync(companyId))
            return ToastDone(false, "این شرکت خارج از دامنهٔ دسترسی شماست", "/Payroll/Items");
        var company = await Db.Companies.FindAsync(companyId);
        if (company is null) return ToastDone(false, "شرکت نامعتبر است", "/Payroll/Items");

        code = (code ?? "").Trim().Replace('‌', '_').Replace(' ', '_');
        title = (title ?? "").Trim();
        if (code.Length < 2) return ToastDone(false, "کد قلم باید حداقل ۲ کاراکتر باشد", "/Payroll/Items");
        if (string.IsNullOrWhiteSpace(title)) return ToastDone(false, "عنوان قلم الزامی است", "/Payroll/Items");
        if (!ItemKinds.Contains(kind)) return ToastDone(false, "نوع قلم نامعتبر است", "/Payroll/Items");
        if (!ScopeTypes.Contains(scopeType)) return ToastDone(false, "هدف‌گیری نامعتبر است", "/Payroll/Items");
        if (scopeType != "همه" && string.IsNullOrWhiteSpace(scopeId))
            return ToastDone(false, "برای این هدف‌گیری، انتخاب هدف الزامی است", "/Payroll/Items");

        var syntaxErr = PayrollFormula.SyntaxCheck(formula);
        if (syntaxErr is not null) return ToastDone(false, HrT.F("خطای فرمول: {0}", syntaxErr), "/Payroll/Items");

        var isEdit = !string.IsNullOrEmpty(id);
        PayItem item;
        if (isEdit)
        {
            item = await Db.PayItems.FindAsync(id);
            if (item is null) return NotFound();
            if (item.IsSystem && item.Code != code)
                return ToastDone(false, "کد اقلام سیستمی قابل تغییر نیست", "/Payroll/Items");
            if (await Db.PayItems.AnyAsync(i => i.CompanyId == companyId && i.Code == code && i.Id != id))
                return ToastDone(false, HrT.F("قلمی با کد «{0}» در این شرکت قبلاً تعریف شده است", code), "/Payroll/Items");
            item.UpdatedAt = DateTime.Now;
        }
        else
        {
            if (await Db.PayItems.AnyAsync(i => i.CompanyId == companyId && i.Code == code))
                return ToastDone(false, HrT.F("قلمی با کد «{0}» در این شرکت قبلاً تعریف شده است", code), "/Payroll/Items");
            item = new PayItem { CompanyId = companyId, CreatedAt = DateTime.Now };
            Db.PayItems.Add(item);
        }
        item.Code = code;
        item.Title = title;
        item.Kind = kind;
        item.Formula = formula.Trim();
        item.Taxable = taxable;
        item.Insurable = insurable;
        item.Continuous = continuous;
        item.ScopeType = scopeType;
        item.ScopeId = scopeType == "همه" ? null : scopeId?.Trim();
        item.Priority = priority;
        item.Order = order;
        item.Active = active;
        await Db.SaveChangesAsync();
        await Audit(isEdit ? "update" : "create", "pay_item", item.Id,
            new { company = company.Name, item.Code, item.Title, item.Kind, item.ScopeType, item.Formula });
        return ToastDone(true, HrT.F("قلم «{0}» ذخیره شد", title), $"/Payroll/Items?companyId={companyId}");
    }

    [HttpPost]
    [HasPerm("payroll.items.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ItemDelete(string id)
    {
        var item = await Db.PayItems.FindAsync(id);
        if (item is null) return NotFound();
        if (!await CompanyAllowedAsync(item.CompanyId))
            return ToastDone(false, "این قلم خارج از دامنهٔ دسترسی شماست", "/Payroll/Items");
        if (item.IsSystem)
            return ToastDone(false, HrT.F("قلم سیستمی «{0}» قابل حذف نیست — می‌توانید فرمول یا فعال بودنش را تغییر دهید", item.Title), $"/Payroll/Items?companyId={item.CompanyId}");
        Db.PayItems.Remove(item);
        await Db.SaveChangesAsync();
        await Audit("delete", "pay_item", id, new { item.Code, item.Title });
        return ToastDone(true, HrT.F("قلم «{0}» حذف شد", item.Title), $"/Payroll/Items?companyId={item.CompanyId}");
    }

    /// <summary>آزمایش فرمول در زمینه واقعی یک پرسنل و ماه — قلب فرمول‌ساز.</summary>
    [HttpPost]
    [HasPerm("payroll.view")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestFormula(string employeeId, int jYear, int jMonth, string formula)
    {
        var (ok, value, error, vars) = await PayrollEngine.TestFormulaAsync(Db, Att, employeeId, jYear, jMonth, formula);
        if (!ok) return AjaxResult(false, error ?? "خطای نامشخص در فرمول");
        var varList = vars.OrderBy(kv => kv.Key)
            .Select(kv => new { code = kv.Key, value = kv.Value }).ToList();
        return AjaxResult(true, "فرمول بدون خطا ارزیابی شد", new { value, vars = varList });
    }

    // ===================== پارامترهای سالانه =====================

    [HttpGet]
    [HasPerm("payroll.view")]
    public async Task<IActionResult> Params(string? companyId, int? year)
    {
        var pScope = await this.ScopeAsync();
        var companies = AllowedCompanies(await CompaniesAsync(), pScope);
        ViewBag.Companies = companies;
        companyId = ScopedCompany(companyId, pScope) ?? companies.FirstOrDefault()?.Id;
        ViewBag.CompanyId = companyId;
        var now = Jalali.ToJalali(DateTime.Now);
        year ??= now.Y;
        ViewBag.Year = year;
        var rows = await Db.PayrollParameters
            .Where(p => p.CompanyId == companyId && p.JYear == year)
            .OrderBy(p => p.Code)
            .ToListAsync();
        return View(rows);
    }

    [HttpGet]
    [HasPerm("payroll.params.edit")]
    public async Task<IActionResult> ParamCreate(string companyId, int year)
    {
        ViewBag.CompanyId = companyId;
        ViewBag.Year = year;
        ViewBag.IsEdit = false;
        return FormView("_ParamForm", new PayrollParameter { CompanyId = companyId, JYear = year });
    }

    [HttpGet]
    [HasPerm("payroll.params.edit")]
    public async Task<IActionResult> ParamEdit(string id, string companyId, int year)
    {
        var p = await Db.PayrollParameters.FindAsync(id);
        if (p is null) return NotFound();
        ViewBag.CompanyId = p.CompanyId;
        ViewBag.Year = p.JYear;
        ViewBag.IsEdit = true;
        return FormView("_ParamForm", p);
    }

    [HttpPost]
    [HasPerm("payroll.params.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ParamSave(string? id, string companyId, int jYear,
        string code, string title, decimal value, string? strValue)
    {
        if (!await CompanyAllowedAsync(companyId))
            return ToastDone(false, "این شرکت خارج از دامنهٔ دسترسی شماست", "/Payroll/Params");
        code = (code ?? "").Trim().Replace('‌', '_').Replace(' ', '_');
        title = (title ?? "").Trim();
        if (code.Length < 2) return ToastDone(false, HrT.T("کد پارامتر باید حداقل ۲ کاراکتر باشد"), $"/Payroll/Params?companyId={companyId}&year={jYear}");
        if (string.IsNullOrWhiteSpace(title)) return ToastDone(false, HrT.T("عنوان پارامتر الزامی است"), $"/Payroll/Params?companyId={companyId}&year={jYear}");

        // پلکان مالیات — مقدار متنی JSON
        string? sv = null;
        var isBracket = code == "پلکان_مالیات";
        if (isBracket)
        {
            sv = (strValue ?? "").Trim();
            if (sv.Length == 0) return ToastDone(false, HrT.F("پلکان مالیات به JSON نیاز دارد — نمونه: {0}", TaxJsonSample[0]), $"/Payroll/Params?companyId={companyId}&year={jYear}");
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(sv);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (!el.TryGetProperty("to", out _) || !el.TryGetProperty("rate", out _))
                        return ToastDone(false, HrT.T("هر پله باید «to» و «rate» داشته باشد"), $"/Payroll/Params?companyId={companyId}&year={jYear}");
                }
            }
            catch
            {
                return ToastDone(false, HrT.F("JSON پلکان مالیات نامعتبر است — نمونه: {0}", TaxJsonSample[0]), $"/Payroll/Params?companyId={companyId}&year={jYear}");
            }
        }

        var isEdit = !string.IsNullOrEmpty(id);
        PayrollParameter p;
        if (isEdit)
        {
            p = await Db.PayrollParameters.FindAsync(id);
            if (p is null) return NotFound();
            if (await Db.PayrollParameters.AnyAsync(x => x.CompanyId == companyId && x.JYear == jYear && x.Code == code && x.Id != id))
                return ToastDone(false, HrT.F("پارامتری با کد «{0}» برای این شرکت/سال قبلاً ثبت شده است", code), $"/Payroll/Params?companyId={companyId}&year={jYear}");
            p.UpdatedAt = DateTime.Now;
        }
        else
        {
            if (await Db.PayrollParameters.AnyAsync(x => x.CompanyId == companyId && x.JYear == jYear && x.Code == code))
                return ToastDone(false, HrT.F("پارامتری با کد «{0}» برای این شرکت/سال قبلاً ثبت شده است", code), $"/Payroll/Params?companyId={companyId}&year={jYear}");
            p = new PayrollParameter { CompanyId = companyId, JYear = jYear, CreatedAt = DateTime.Now };
            Db.PayrollParameters.Add(p);
        }
        p.Code = code;
        p.Title = title;
        p.Value = value;
        p.StrValue = isBracket ? sv : null;
        await Db.SaveChangesAsync();
        await Audit(isEdit ? "update" : "create", "payroll_parameter", p.Id, new { p.Code, p.Title, p.Value, jYear });
        return ToastDone(true, HrT.F("پارامتر «{0}» ذخیره شد", title), $"/Payroll/Params?companyId={companyId}&year={jYear}");
    }

    [HttpPost]
    [HasPerm("payroll.params.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ParamDelete(string id, string companyId, int jYear)
    {
        var p = await Db.PayrollParameters.FindAsync(id);
        if (p is null) return NotFound();
        if (!await CompanyAllowedAsync(p.CompanyId))
            return ToastDone(false, "این پارامتر خارج از دامنهٔ دسترسی شماست", "/Payroll/Params");
        Db.PayrollParameters.Remove(p);
        await Db.SaveChangesAsync();
        await Audit("delete", "payroll_parameter", id, new { p.Code, p.JYear });
        return ToastDone(true, HrT.F("پارامتر «{0}» حذف شد", p.Title), $"/Payroll/Params?companyId={companyId}&year={jYear}");
    }

    // ===================== عیدی و سنوات سالانه + تسویه حساب (فاز ۲۵) =====================

    /// <summary>تجزیه تاریخ جلالی متنی (۱۴۰۵/۰۷/۲۸ با ارقام فارسی یا لاتین) — خطای فارسی در صورت نامعتبر بودن.</summary>
    private static (DateTime Date, string? Error) ParseJalaliDate(string? input)
    {
        var s = EssAuth.NormDigits((input ?? "").Trim()).Replace('-', '/').Replace('.', '/');
        var p = s.Split('/');
        if (p.Length != 3 || !int.TryParse(p[0], out var y) || !int.TryParse(p[1], out var m) || !int.TryParse(p[2], out var d))
            return (default, "تاریخ را به شکل ۱۴۰۵/۰۷/۲۸ وارد کنید");
        if (y is < 1300 or > 1500 || m is < 1 or > 12 || d is < 1 or > 31)
            return (default, "تاریخ جلالی نامعتبر است");
        try { return (Jalali.FromJalali(y, m, d), null); }
        catch { return (default, "تاریخ جلالی نامعتبر است"); }
    }

    [HttpGet]
    [HasPerm("payroll.bonus")]
    public async Task<IActionResult> Bonus(string? companyId, int? year)
    {
        // فاز ۷۴ — دامنهٔ رکوردی: ردیف‌ها فقط از محدودهٔ پرسنلی مجاز؛ شرکت‌های مجاز در dropdown
        var bScope = await this.ScopeAsync();
        var companies = AllowedCompanies(await CompaniesAsync(), bScope);
        ViewBag.Companies = companies;
        companyId = ScopedCompany(companyId, bScope) ?? companies.FirstOrDefault()?.Id;
        ViewBag.CompanyId = companyId;
        var nowJ = Jalali.ToJalali(DateTime.Now);
        year ??= nowJ.Y;
        ViewBag.Year = year;
        ViewBag.Years = new[] { nowJ.Y - 1, nowJ.Y, nowJ.Y + 1 };
        ViewBag.Rows = await Db.PayrollBonusRows
            .Include(r => r.Employee)
            .Where(r => r.CompanyId == companyId && r.JYear == year)
            .WhereEmpScope(bScope, r => r.EmployeeId)
            .OrderBy(r => r.Employee!.PersonnelCode)
            .ToListAsync();
        ViewBag.Settlements = await Db.PayrollSettlements
            .Include(s => s.Employee)
            .Include(s => s.Company)
            .WhereEmpScope(bScope, s => s.EmployeeId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(15)
            .ToListAsync();
        // فاز ۱۲۱ — بارگذاری لیست کامل پرسنل حذف شد (فرم تسویه با /Ui/EmpSearch کار می‌کند)
        return View();
    }

    /// <summary>محاسبه عیدی/سنوات سالانه — پیش‌نویس‌های همان سال بازنویسی و ردیف‌های نهایی حفظ می‌شوند.</summary>
    [HttpPost]
    [HasPerm("payroll.bonus.compute")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BonusCompute(string companyId, int jYear)
    {
        var bcScope = await this.ScopeAsync();
        if (!bcScope.CoAllowed(companyId))
            return ToastDone(false, "این شرکت خارج از دامنهٔ دسترسی شماست", "/Payroll/Bonus");
        if (EmpLimited(bcScope))
            return ToastDone(false, "محاسبهٔ عیدی/سنوات کل شرکت است — حساب شما به واحد/شخص مشخص محدود شده است", "/Payroll/Bonus");
        var company = await Db.Companies.FindAsync(companyId);
        if (company is null) return ToastDone(false, "شرکت نامعتبر است", "/Payroll/Bonus");
        if (jYear < 1300 || jYear > 1500) return ToastDone(false, "سال جلالی نامعتبر است", "/Payroll/Bonus");
        try
        {
            var computed = await PayrollEngine.ComputeBonusAsync(Db, Att, company, jYear);
            var existing = await Db.PayrollBonusRows
                .Where(r => r.CompanyId == companyId && r.JYear == jYear)
                .ToListAsync();
            var finals = existing.Where(r => r.Status == "نهایی").ToDictionary(r => r.EmployeeId);
            Db.PayrollBonusRows.RemoveRange(existing.Where(r => r.Status != "نهایی"));
            await Db.SaveChangesAsync();
            foreach (var c in computed)
            {
                if (finals.ContainsKey(c.Emp.Id)) continue;   // ردیف نهایی دست نمی‌خورد
                Db.PayrollBonusRows.Add(new PayrollBonusRow
                {
                    CompanyId = companyId, EmployeeId = c.Emp.Id, JYear = jYear,
                    Days = c.Days, Base = c.Base, Eid = c.Eid, Seniority = c.Seniority,
                });
            }
            await Db.SaveChangesAsync();
            await Audit("compute", "payroll_bonus", companyId,
                new { company = company.Name, jYear, count = computed.Count, keptFinal = finals.Count });
            return ToastDone(true,
                HrT.F("عیدی و سنوات {0} «{1}» برای {2} پرسنل محاسبه شد", HrT.Num(jYear), company.Name, HrT.Num(computed.Count)) +
                (finals.Count > 0 ? HrT.F(" — {0} ردیف نهایی حفظ شد", HrT.Num(finals.Count)) : ""),
                $"/Payroll/Bonus?companyId={companyId}&year={jYear}");
        }
        catch (PayFormulaException ex)
        {
            return ToastDone(false, HrT.F("محاسبه متوقف شد: {0}", ex.Message), $"/Payroll/Bonus?companyId={companyId}&year={jYear}");
        }
    }

    [HttpPost]
    [HasPerm("payroll.bonus.compute")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BonusFinalize(string companyId, int jYear)
    {
        var bfScope = await this.ScopeAsync();
        if (!bfScope.CoAllowed(companyId) || EmpLimited(bfScope))
            return ToastDone(false, "نهایی‌سازی کل شرکت خارج از دامنهٔ دسترسی شماست", "/Payroll/Bonus");
        var rows = await Db.PayrollBonusRows
            .Where(r => r.CompanyId == companyId && r.JYear == jYear && r.Status != "نهایی")
            .ToListAsync();
        if (rows.Count == 0)
            return ToastDone(false, HrT.T("ردیف پیش‌نویسی برای نهایی‌سازی وجود ندارد — ابتدا محاسبه کنید"), $"/Payroll/Bonus?companyId={companyId}&year={jYear}");
        foreach (var r in rows)
        {
            r.Status = "نهایی";
            r.FinalizedBy = Sess?.FullName;
            r.FinalizedAt = DateTime.Now;
        }
        await Db.SaveChangesAsync();
        await Audit("finalize", "payroll_bonus", companyId, new { companyId, jYear, count = rows.Count });
        return ToastDone(true, HrT.F("{0} ردیف عیدی/سنوات نهایی شد", Jalali.Num(rows.Count)), $"/Payroll/Bonus?companyId={companyId}&year={jYear}");
    }

    [HttpPost]
    [HasPerm("payroll.bonus.compute")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BonusDeleteDraft(string companyId, int jYear)
    {
        var bdScope = await this.ScopeAsync();
        if (!bdScope.CoAllowed(companyId) || EmpLimited(bdScope))
            return ToastDone(false, "حذف پیش‌نویس کل شرکت خارج از دامنهٔ دسترسی شماست", "/Payroll/Bonus");
        var rows = await Db.PayrollBonusRows
            .Where(r => r.CompanyId == companyId && r.JYear == jYear && r.Status != "نهایی")
            .ToListAsync();
        if (rows.Count == 0)
            return ToastDone(false, HrT.T("ردیف پیش‌نویسی برای حذف وجود ندارد"), $"/Payroll/Bonus?companyId={companyId}&year={jYear}");
        Db.PayrollBonusRows.RemoveRange(rows);
        await Db.SaveChangesAsync();
        await Audit("delete_draft", "payroll_bonus", companyId, new { companyId, jYear, count = rows.Count });
        return ToastDone(true, HrT.T("پیش‌نویس‌های عیدی/سنوات حذف شد"), $"/Payroll/Bonus?companyId={companyId}&year={jYear}");
    }

    [HttpGet]
    [HasPerm("payroll.bonus.export")]
    public async Task<IActionResult> BonusExport(string companyId, int jYear)
    {
        var beScope = await this.ScopeAsync();
        if (!beScope.CoAllowed(companyId)) return NotFound();
        var company = await Db.Companies.FindAsync(companyId);
        if (company is null) return NotFound();
        var rows = await Db.PayrollBonusRows.Include(r => r.Employee)
            .Where(r => r.CompanyId == companyId && r.JYear == jYear)
            .WhereEmpScope(beScope, r => r.EmployeeId)
            .OrderBy(r => r.Employee!.PersonnelCode)
            .ToListAsync();
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Bonus");
        ws.RightToLeft = true;
        var headers = new[] { "شرکت", "کد پرسنلی", "نام و نام خانوادگی", "روزهای خدمت", $"پایه مبنا ({Money.CurTitle()})", $"عیدی ({Money.CurTitle()})", $"پایه سنوات ({Money.CurTitle()})", $"جمع ({Money.CurTitle()})", "وضعیت" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        int row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = company.Name;
            ws.Cell(row, 2).Value = r.Employee!.PersonnelCode;
            ws.Cell(row, 3).Value = r.Employee.FullName;
            ws.Cell(row, 4).Value = (double)r.Days;
            ws.Cell(row, 5).Value = (double)r.Base;
            ws.Cell(row, 6).Value = (double)r.Eid;
            ws.Cell(row, 7).Value = (double)r.Seniority;
            ws.Cell(row, 8).Value = (double)r.Total;
            ws.Cell(row, 9).Value = r.Status;
            row++;
        }
        ws.Columns().AdjustToContents();
        await Audit("export", "payroll_bonus", companyId, new { jYear, count = rows.Count });
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Bonus-{company.Code}-{jYear}.xlsx");
    }

    /// <summary>محاسبه تسویه‌حساب — ردیف پیش‌نویس ذخیره و برای نهایی‌سازی نمایش داده می‌شود.</summary>
    [HttpPost]
    [HasPerm("payroll.settlement")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettlementCompute(string employeeId, string endDate)
    {
        var emp = await Db.Employees.Include(e => e.Company).FirstOrDefaultAsync(e => e.Id == employeeId);
        if (emp is null) return ToastDone(false, "پرسنل یافت نشد", "/Payroll/Bonus");
        if (!(await this.ScopeAsync()).EmpAllowed(emp.Id))
            return ToastDone(false, "این پرسنل خارج از دامنهٔ دسترسی شماست", "/Payroll/Bonus");
        if (emp.Status != "فعال")
            return ToastDone(false, HrT.F("وضعیت پرسنل «{0}» فعال نیست (وضعیت فعلی: {1})", emp.FullName, HrT.T(emp.Status)), "/Payroll/Bonus");
        var (date, err) = ParseJalaliDate(endDate);
        if (err is not null) return ToastDone(false, err, "/Payroll/Bonus");
        if (date < emp.HireDate)
            return ToastDone(false, "تاریخ پایان همکاری نمی‌تواند قبل از تاریخ استخدام باشد", "/Payroll/Bonus");

        try
        {
            var r = await PayrollEngine.ComputeSettlementAsync(Db, Att, emp, date);
            // پیش‌نویس قبلی همان پرسنل جایگزین می‌شود
            var old = await Db.PayrollSettlements
                .Where(s => s.EmployeeId == employeeId && s.Status != "نهایی").ToListAsync();
            Db.PayrollSettlements.RemoveRange(old);
            var st = new PayrollSettlement
            {
                CompanyId = emp.CompanyId, EmployeeId = emp.Id, EndDate = date,
                Days = r.Days, LeaveBalance = r.LeaveBalance, LeavePay = r.LeavePay,
                Daily = r.Daily,
                Eid = r.Eid, Seniority = r.Seniority, CreatedBy = Sess?.FullName,
            };
            Db.PayrollSettlements.Add(st);
            await Db.SaveChangesAsync();
            await Audit("compute", "payroll_settlement", st.Id,
                new { emp = emp.FullName, endDate = Jalali.Fmt(date), st.Total });
            return ToastDone(true,
                HrT.F("تسویه‌حساب «{0}» محاسبه شد — جمع {1}", emp.FullName, Money.Fmt(st.Total)),
                "/Payroll/Bonus#settlement");
        }
        catch (PayFormulaException ex)
        {
            return ToastDone(false, HrT.F("محاسبه متوقف شد: {0}", ex.Message), "/Payroll/Bonus");
        }
    }

    /// <summary>ثبت نهایی تسویه — وضعیت پرسنل «تسویه‌شده» و تاریخ پایان همکاری ثبت می‌شود.</summary>
    [HttpPost]
    [HasPerm("payroll.settlement")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettlementFinalize(string id)
    {
        var st = await Db.PayrollSettlements.Include(s => s.Employee).FirstOrDefaultAsync(s => s.Id == id);
        if (st is null) return NotFound();
        if (!(await this.ScopeAsync()).EmpAllowed(st.EmployeeId))
            return ToastDone(false, "این تسویه‌حساب خارج از دامنهٔ دسترسی شماست", "/Payroll/Bonus");
        if (st.Status == "نهایی") return ToastDone(false, "این تسویه‌حساب قبلاً نهایی شده است", "/Payroll/Bonus");
        st.Status = "نهایی";
        st.FinalizedBy = Sess?.FullName;
        st.FinalizedAt = DateTime.Now;
        if (st.Employee is not null)
        {
            st.Employee.Status = "تسویه‌شده";
            st.Employee.EndDate = st.EndDate;
        }
        await Db.SaveChangesAsync();
        await Audit("finalize", "payroll_settlement", id,
            new { emp = st.Employee?.FullName, st.Total, end = Jalali.Fmt(st.EndDate) });
        return ToastDone(true,
            HrT.F("تسویه‌حساب «{0}» نهایی شد و وضعیت پرسنل به «تسویه‌شده» تغییر کرد", st.Employee?.FullName ?? "—"),
            "/Payroll/Bonus#settlement");
    }

    [HttpPost]
    [HasPerm("payroll.settlement")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettlementDelete(string id)
    {
        var st = await Db.PayrollSettlements.FindAsync(id);
        if (st is null) return NotFound();
        if (!(await this.ScopeAsync()).EmpAllowed(st.EmployeeId))
            return ToastDone(false, "این تسویه‌حساب خارج از دامنهٔ دسترسی شماست", "/Payroll/Bonus");
        if (st.Status == "نهایی") return ToastDone(false, "تسویه‌حساب نهایی‌شده قابل حذف نیست", "/Payroll/Bonus");
        Db.PayrollSettlements.Remove(st);
        await Db.SaveChangesAsync();
        await Audit("delete", "payroll_settlement", id, null);
        return ToastDone(true, "پیش‌نویس تسویه‌حساب حذف شد", "/Payroll/Bonus#settlement");
    }

    // ===================== اسناد PDF رسمی (فاز ۲۵) =====================

    /// <summary>گزارش رسمی لیست حقوق — PDF فارسی با جمع کل و محل امضا.</summary>
    [HttpGet]
    [HasPerm("payroll.pdf")]
    public async Task<IActionResult> RunPdf(string runId)
    {
        var run = await Db.PayrollRuns.Include(r => r.Company)
            .Include(r => r.Payslips).ThenInclude(p => p.Employee).ThenInclude(e => e!.Department)
            .FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null) return NotFound();
        var pdfScope = await this.ScopeAsync();
        if (!pdfScope.CoAllowed(run.CompanyId))
            return ToastDone(false, "این لیست حقوق خارج از دامنهٔ دسترسی شماست", "/Payroll");
        run.Payslips = run.Payslips.Where(p => pdfScope.EmpAllowed(p.EmployeeId)).ToList();
        await ApplyPdfThemeAsync();
        var pdf = PayrollPdf.BuildPayrollRun(run, WebRoot());
        await Audit("pdf", "payroll_run", runId, new { run.JYear, run.JMonth });
        return File(pdf, "application/pdf",
            $"Payroll-List-{run.JYear}-{run.JMonth:00}.pdf");
    }

    /// <summary>فیش حقوقی رسمی یک پرسنل — PDF فارسی با اقلام خطی.</summary>
    [HttpGet]
    [HasPerm("payroll.pdf")]
    public async Task<IActionResult> SlipPdf(string id)
    {
        var slip = await Db.Payslips
            .Include(p => p.Lines)
            .Include(p => p.Employee).ThenInclude(e => e!.Department)
            .Include(p => p.Employee).ThenInclude(e => e!.Position)
            .Include(p => p.Run).ThenInclude(r => r!.Company)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (slip is null) return NotFound();
        if (!(await this.ScopeAsync()).EmpAllowed(slip.EmployeeId))
            return ToastDone(false, "این فیش خارج از دامنهٔ دسترسی شماست", "/Payroll");
        await ApplyPdfThemeAsync();
        var pdf = PayrollPdf.BuildSlip(slip, WebRoot());
        await Audit("pdf", "payslip", id, null);
        return File(pdf, "application/pdf",
            $"Payslip-{slip.Employee!.PersonnelCode}-{slip.Run!.JYear}-{slip.Run.JMonth:00}.pdf");
    }

    /// <summary>برگ تسویه‌حساب رسمی — PDF فارسی.</summary>
    [HttpGet]
    [HasPerm("payroll.pdf")]
    public async Task<IActionResult> SettlementPdf(string id)
    {
        var st = await Db.PayrollSettlements
            .Include(s => s.Employee).ThenInclude(e => e!.Department)
            .Include(s => s.Company)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (st is null) return NotFound();
        if (!(await this.ScopeAsync()).EmpAllowed(st.EmployeeId))
            return ToastDone(false, "این تسویه‌حساب خارج از دامنهٔ دسترسی شماست", "/Payroll/Bonus");
        await ApplyPdfThemeAsync();
        var pdf = PayrollPdf.BuildSettlement(st, WebRoot());
        await Audit("pdf", "payroll_settlement", id, null);
        return File(pdf, "application/pdf",
            $"Settlement-{st.Employee!.PersonnelCode}-{Jalali.Fmt(st.EndDate).Replace('/', '-')}.pdf");
    }

    // ===================== تنظیمات محاسبه =====================

    [HttpGet]
    [HasPerm("payroll.view")]
    public async Task<IActionResult> Settings()
    {
        var sScope = await this.ScopeAsync();
        ViewBag.Companies = AllowedCompanies(await CompaniesAsync(), sScope);
        var settings = await Db.PayrollSettings.ToListAsync();
        ViewBag.Settings = settings.ToDictionary(s => s.CompanyId);
        return View();
    }

    [HttpPost]
    [HasPerm("payroll.settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettingsSave(string companyId, string calcMethod,
        int workSource, int monthDays, int hourBase, int roundUnit)
    {
        if (!await CompanyAllowedAsync(companyId))
            return ToastDone(false, "این شرکت خارج از دامنهٔ دسترسی شماست", "/Payroll/Settings");
        var company = await Db.Companies.FindAsync(companyId);
        if (company is null) return ToastDone(false, "شرکت نامعتبر است", "/Payroll/Settings");
        if (calcMethod != "ماهانه" && calcMethod != "کارکردی")
            return ToastDone(false, "روش محاسبه نامعتبر است", "/Payroll/Settings");
        if (workSource is < 1 or > 3)
            return ToastDone(false, "اولویت منبع تردد باید ۱، ۲ یا ۳ باشد", "/Payroll/Settings");
        if (monthDays is < 28 or > 31) return ToastDone(false, "روزهای ماه باید بین ۲۸ تا ۳۱ باشد", "/Payroll/Settings");
        if (hourBase is < 120 or > 260) return ToastDone(false, "ساعت پایه ماه باید بین ۱۲۰ تا ۲۶۰ باشد", "/Payroll/Settings");
        if (roundUnit < 0) return ToastDone(false, "واحد رند نمی‌تواند منفی باشد", "/Payroll/Settings");

        var s = await Db.PayrollSettings.FirstOrDefaultAsync(x => x.CompanyId == companyId);
        var isNew = s is null;
        if (isNew)
        {
            s = new PayrollSetting { CompanyId = companyId };
            Db.PayrollSettings.Add(s);
        }
        s!.CalcMethod = calcMethod;
        s.WorkSource = workSource;
        s.MonthDays = monthDays;
        s.HourBase = hourBase;
        s.RoundUnit = roundUnit;
        s.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("update", "payroll_setting", companyId, new { company.Name, calcMethod, workSource, monthDays, hourBase, roundUnit });
        return ToastDone(true, HrT.F("تنظیمات محاسبه «{0}» ذخیره شد", company.Name), "/Payroll/Settings");
    }
}
