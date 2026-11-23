using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace HrModules.Org;

public class EmployeesController(AppDbContext db, ICurrentUser me, IWebHostEnvironment env) : MvcBase(db, me)
{
    private readonly IWebHostEnvironment _env = env;

    // فاز ۶۹ — DbSetهای خانوادهٔ سازمان روی کانتکست مرکب (مالکیت اسکیما ∈ OrgDataModule.ComposeModel)
    private DbSet<Employee> Employees => Db.Set<Employee>();
    private DbSet<Department> Departments => Db.Set<Department>();
    private DbSet<Position> Positions => Db.Set<Position>();

    // ---------- فاز ۶۷ — شیفت‌ها ∈ ماژول حضور؛ بدون لایهٔ دیتای آن، لیست/نام شیفت خالی می‌ماند ----------
    /// <summary>فاز ۶۸ — شیفت‌ها ∈ پلاگین حضور (قرارداد IAttendanceGateway؛ غایب = خالی).</summary>
    private async Task LoadShiftsViewBag()
    {
        var att = HttpContext.RequestServices.GetRequiredService<Platform.Contracts.IAttendanceGateway>();
        var shifts = await att.ActiveShiftsAsync();
        ViewBag.Shifts = shifts.Cast<object>().ToList();
        ViewBag.ShiftNameById = shifts.ToDictionary(s => s.Id, s => s.Name);
    }

    [HttpGet]
    [HasPerm("employees.view")]
    public async Task<IActionResult> Index(string? q, string? deptId, string? type, string? co)
    {
        var query = Employees
            .Include(e => e.Department).Include(e => e.Position)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(e => e.FirstName.Contains(q) || e.LastName.Contains(q) || e.PersonnelCode.Contains(q));
        if (!string.IsNullOrEmpty(deptId))
            query = query.Where(e => e.DepartmentId == deptId);
        if (!string.IsNullOrEmpty(type))
            query = query.Where(e => e.EmploymentType == type);
        if (!string.IsNullOrEmpty(co))
            query = query.Where(e => e.CompanyId == co);
        // فاز ۷۴ — دامنهٔ رکوردی: فقط پرسنلِ محدودهٔ مجاز کاربر (شرکت/واحد با زیرشاخه/خودش)
        var scope = await this.ScopeAsync();
        query = query.WhereEmpTableScope(scope);

        var employees = await query.OrderBy(e => e.PersonnelCode).ToListAsync();
        ViewBag.Q = q;
        ViewBag.DeptId = deptId;
        ViewBag.Type = type;
        ViewBag.Co = co;
        ViewBag.Depts = await Departments.OrderBy(d => d.Name).ToListAsync();
        ViewBag.Positions = await Positions.Include(p => p.Department).OrderBy(p => p.Title).ToListAsync();
        // فاز ۶۷ — شیفت‌ها ∈ ماژول حضور؛ بدون لایهٔ دیتای آن، لیست/نام شیفت خالی می‌ماند
        await LoadShiftsViewBag();
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        // نگاشت پرسنل → کاربر متصل (برای ستون «حساب کاربری»)
        var linkedUsers = await Db.Users
            .Where(u => u.EmployeeId != null && u.EmployeeId != "")
            .ToListAsync();
        ViewBag.UserByEmp = linkedUsers.GroupBy(u => u.EmployeeId!)
            .ToDictionary(g => g.Key, g => g.First());
        return View(employees);
    }

    // ---------- فرم پاپ‌آپ ثبت پرسنل (مودال AJAX) ----------
    [HttpGet]
    [HasPerm("employees.create")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Depts = await Departments.OrderBy(d => d.Name).ToListAsync();
        ViewBag.Positions = await Positions.Include(p => p.Department).OrderBy(p => p.Title).ToListAsync();
        await LoadShiftsViewBag();
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        return PartialView("_CreateForm");
    }

    [HttpPost]
    [HasPerm("employees.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string personnelCode, string firstName, string lastName, string nationalId,
        string? phone, string employmentType, string? departmentId, string? positionId,
        string? shiftId, decimal baseSalary, DateTime hireDate,
        string companyId, string maritalStatus, int childCount, IFormFile? photoFile = null)
    {
        try
        {
            if (await Employees.AnyAsync(e => e.PersonnelCode == personnelCode))
            { ToastErr("کد پرسنلی تکراری است"); if (Ajax) return AjaxResult(false, "کد پرسنلی تکراری است"); return Redirect("/Employees"); }
            if (await Employees.AnyAsync(e => e.NationalId == nationalId))
            { ToastErr("کد ملی تکراری است"); if (Ajax) return AjaxResult(false, "کد ملی تکراری است"); return Redirect("/Employees"); }

            // سقف پرسنل در مجوز نصب (فاز ۴۶) — فقط وقتی مجوز معتبر با سقف مشخص فعال باشد
            var lic = HttpContext.RequestServices.GetRequiredService<Platform.Web.Services.LicenseService>();
            var (capOk, capMsg) = lic.CheckEmployeeCap(await Employees.CountAsync());
            if (!capOk)
            { ToastErr(capMsg); if (Ajax) return AjaxResult(false, capMsg); return Redirect("/Employees"); }

            var company = await Db.Companies.FindAsync(companyId)
                          ?? await Db.Companies.OrderBy(c => c.Code).FirstOrDefaultAsync();
            if (company is null)
            { ToastErr("ابتدا یک شرکت تعریف کنید"); if (Ajax) return AjaxResult(false, "ابتدا یک شرکت تعریف کنید"); return Redirect("/Employees"); }
            var emp = new Employee
            {
                PersonnelCode = personnelCode.Trim(), FirstName = firstName.Trim(), LastName = lastName.Trim(),
                NationalId = nationalId.Trim(), Phone = phone, EmploymentType = employmentType,
                DepartmentId = departmentId, PositionId = positionId, ShiftId = shiftId,
                BaseSalary = baseSalary, HireDate = hireDate, CompanyId = company.Id, Status = "فعال",
                MaritalStatus = maritalStatus == "متأهل" ? "متأهل" : "مجرد",
                ChildCount = Math.Max(0, childCount),
                AnnualLeaveEnt = employmentType == "رسمی" ? 26 : 22,
                Email = $"emp{personnelCode.Trim()}@parsasia.ir",
            };
            Employees.Add(emp);
            await Db.SaveChangesAsync();

            // عکس پرسنل (فاز ۴۵)
            if (photoFile is not null && photoFile.Length > 0)
            {
                var (photo, photoErr) = await AvatarUpload.SaveAsync(_env, photoFile, $"e{emp.Id[..Math.Min(8, emp.Id.Length)]}", null);
                if (photoErr is null && photo is not null)
                {
                    emp.PhotoPath = photo;
                    await Db.SaveChangesAsync();
                }
            }

            await Audit("create", "employee", emp.Id, new { emp.PersonnelCode, name = emp.FullName });
            ToastOk(HrT.F("پرسنل «{0}» با کد {1} ثبت شد", emp.FullName, HrT.Num(emp.PersonnelCode)));
            if (Ajax) return AjaxResult(true, HrT.F("پرسنل «{0}» با کد {1} ثبت شد", emp.FullName, HrT.Num(emp.PersonnelCode)));
        }
        catch (Exception ex)
        {
            ToastErr(HrT.F("خطا در ثبت پرسنل: {0}", ex.Message));
            if (Ajax) return AjaxResult(false, HrT.F("خطا در ثبت پرسنل: {0}", ex.Message));
        }
        return Redirect("/Employees");
    }

    [HttpPost]
    [HasPerm("employees.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string id)
    {
        var emp = await Employees.FindAsync(id);
        if (emp is null) return NotFound();
        emp.Status = emp.Status == "فعال" ? "غیرفعال" : "فعال";
        emp.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("update", "employee", emp.Id, new { status = emp.Status });
        ToastOk(HrT.F("وضعیت «{0}» به {1} تغییر یافت", emp.FullName, HrT.T(emp.Status)));
        if (Ajax) return AjaxResult(true, HrT.F("وضعیت «{0}» به {1} تغییر یافت", emp.FullName, HrT.T(emp.Status)));
        return Redirect("/Employees");
    }

    // ---------- ویرایش پرسنل ----------

    [HttpGet]
    [HasPerm("employees.edit")]
    public async Task<IActionResult> Edit(string id)
    {
        var emp = await Employees
            .Include(e => e.Department).Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (emp is null) return NotFound();

        ViewBag.Depts = await Departments.OrderBy(d => d.Name).ToListAsync();
        ViewBag.Positions = await Positions.Include(p => p.Department).OrderBy(p => p.Title).ToListAsync();
        await LoadShiftsViewBag();
        // فاز ۶۹ — دستگاه‌ها ∈ پلاگین devices؛ غایب = فهرست خالی (کوئری فقط با ماژول حاضر)
        ViewBag.Devices = Platform.Contracts.HrModuleRuntime.Installed("devices")
            ? await Db.Set<DeviceUser>()
                .Include(du => du.Device)
                .Where(du => du.EmployeeId == id)
                .Select(du => du.Device!.Name + " — پین " + du.Pin)
                .ToListAsync()
            : new List<string>();
        // بخش «حساب کاربری متصل» — فقط برای دارندگان دسترسی مدیریت کاربران
        var canLink = Me.Get()!.Has("users.manage");
        ViewBag.CanLink = canLink;
        if (canLink)
        {
            ViewBag.LinkedUser = await Db.Users.Include(u => u.Roles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.EmployeeId == id);
            ViewBag.FreeUsers = await Db.Users
                .Where(u => (u.EmployeeId == null || u.EmployeeId == "") && u.IsActive
                            && !u.Username.StartsWith("emp:"))
                .OrderBy(u => u.Username).ToListAsync();
        }
        if (Partial) return PartialView("_EditForm", emp);
        return View(emp);
    }

    [HttpPost]
    [HasPerm("employees.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id, string personnelCode, string firstName, string lastName, string nationalId,
        string? phone, string? email, string employmentType, string status,
        string? departmentId, string? positionId, string? shiftId,
        decimal baseSalary, DateTime hireDate, decimal annualLeaveEnt, decimal sickLeaveEnt,
        DateTime? endDate, string maritalStatus, int childCount, IFormFile? photoFile = null,
        bool removePhoto = false)
    {
        var emp = await Employees.FindAsync(id);
        if (emp is null) return NotFound();
        try
        {
            var code = personnelCode.Trim();
            var nid = nationalId.Trim();
            if (await Employees.AnyAsync(e => e.PersonnelCode == code && e.Id != id))
            { ToastErr("کد پرسنلی تکراری است (متعلق به پرسنل دیگری است)"); if (Ajax) return AjaxResult(false, "کد پرسنلی تکراری است (متعلق به پرسنل دیگری است)"); return Redirect($"/Employees/Edit/{id}"); }
            if (await Employees.AnyAsync(e => e.NationalId == nid && e.Id != id))
            { ToastErr("کد ملی تکراری است (متعلق به پرسنل دیگری است)"); if (Ajax) return AjaxResult(false, "کد ملی تکراری است (متعلق به پرسنل دیگری است)"); return Redirect($"/Employees/Edit/{id}"); }

            var oldCode = emp.PersonnelCode;
            var codeChanged = oldCode != code;
            emp.PersonnelCode = code;
            emp.FirstName = firstName.Trim();
            emp.LastName = lastName.Trim();
            emp.NationalId = nid;
            emp.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            emp.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            emp.EmploymentType = employmentType;
            emp.Status = status;
            emp.DepartmentId = string.IsNullOrEmpty(departmentId) ? null : departmentId;
            emp.PositionId = string.IsNullOrEmpty(positionId) ? null : positionId;
            emp.ShiftId = string.IsNullOrEmpty(shiftId) ? null : shiftId;
            emp.BaseSalary = baseSalary;
            emp.HireDate = hireDate;
            emp.AnnualLeaveEnt = annualLeaveEnt;
            emp.SickLeaveEnt = sickLeaveEnt;
            emp.EndDate = endDate;
            emp.MaritalStatus = maritalStatus == "متأهل" ? "متأهل" : "مجرد";
            emp.ChildCount = Math.Max(0, childCount);

            // عکس پرسنل (فاز ۴۵) — بارگذاری جدید یا حذف صریح
            if (removePhoto)
            {
                AvatarUpload.Delete(_env, emp.PhotoPath);
                emp.PhotoPath = null;
            }
            if (photoFile is not null && photoFile.Length > 0)
            {
                var (photo, photoErr) = await AvatarUpload.SaveAsync(_env, photoFile, $"e{emp.Id[..Math.Min(8, emp.Id.Length)]}", emp.PhotoPath);
                if (photoErr is not null)
                {
                    ToastErr(HrT.F("بارگذاری عکس ناموفق بود: {0}", photoErr));
                }
                else if (photo is not null)
                {
                    emp.PhotoPath = photo;
                }
            }

            emp.UpdatedAt = DateTime.Now;
            await Db.SaveChangesAsync();

            // اگر کد پرسنلی تغییر کرد، پینِ کاربر روی دستگاه‌ها دیگر نمی‌تواند خودکار تغییر کند — هشدار بده
            string? pinWarn = null;
            if (codeChanged)
            {
                // فاز ۶۹ — دستگاه‌ها ∈ پلاگین devices؛ غایب = بدون دستگاه متصل
                var linked = Platform.Contracts.HrModuleRuntime.Installed("devices")
                    ? await Db.Set<DeviceUser>().Include(du => du.Device)
                        .Where(du => du.EmployeeId == id).ToListAsync()
                    : new List<DeviceUser>();
                pinWarn = $"⚠ کد پرسنلی از «{oldCode}» به «{code}» تغییر کرد — پینِ ثبت‌شده روی {Jalali.Num(linked.Count)} دستگاه ({string.Join("، ", linked.Select(l => l.Device?.Name))}) همان پین قبلی است؛ برای همگام‌سازی، اطلاعات پرسنل را با پین جدید به دستگاه‌ها منتقل کنید";
                ToastErr(pinWarn);
            }

            await Audit("update", "employee", emp.Id, new
            {
                emp.PersonnelCode, name = emp.FullName, emp.NationalId, emp.DepartmentId,
                emp.PositionId, emp.ShiftId, emp.BaseSalary, emp.Status, codeChanged,
            });
            var okMsg = $"اطلاعات «{emp.FullName}» ذخیره شد" + (pinWarn is null ? "" : " — " + pinWarn);
            ToastOk(HrT.F("اطلاعات «{0}» ذخیره شد", emp.FullName));
            if (Ajax) return AjaxResult(true, okMsg);
            return Redirect("/Employees");
        }
        catch (Exception ex)
        {
            ToastErr(HrT.F("خطا در ذخیره اطلاعات پرسنل: {0}", ex.Message));
            if (Ajax) return AjaxResult(false, HrT.F("خطا در ذخیره اطلاعات پرسنل: {0}", ex.Message));
            return Redirect($"/Employees/Edit/{id}");
        }
    }
// ===================== ورود گروهی از اکسل (فاز ۱۶) =====================

[HttpGet]
[HasPerm("employees.import")]
public IActionResult Import()
{
    ViewBag.DeptCodes = Departments.OrderBy(d => d.Code).Select(d => d.Code + " — " + d.Name).ToList();
    return View();
}

[HttpGet]
[HasPerm("employees.import")]
public IActionResult ImportTemplate()
{
    return File(ExcelImport.BuildTemplate(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "قالب-ورود-گروهی.xlsx");
}

[HttpPost]
[HasPerm("employees.import")]
[ValidateAntiForgeryToken]
[RequestSizeLimit(8 * 1024 * 1024)]
public async Task<IActionResult> ImportPreview(IFormFile? file)
{
    if (file is null || file.Length == 0)
    { ToastErr("فایلی انتخاب نشده است"); return RedirectToAction(nameof(Import)); }
    if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
    { ToastErr("فقط فایل xlsx پشتیبانی می‌شود"); return RedirectToAction(nameof(Import)); }

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    ms.Position = 0;
    var res = ExcelImport.ParsePeople(ms, Db);
    ms.Position = 0;
    var resAtt = ExcelImport.ParseAttendance(ms, Db);
    ViewBag.Result = res;
    ViewBag.ResultAtt = resAtt;
    ViewBag.RowsJson = System.Text.Json.JsonSerializer.Serialize(res.Rows);
    ViewBag.RowsJsonAtt = System.Text.Json.JsonSerializer.Serialize(resAtt.Rows);
    ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
    ViewBag.DeptCodes = await Departments.OrderBy(d => d.Code).Select(d => d.Code + " — " + d.Name).ToListAsync();
    return View(nameof(Import));
}

[HttpPost]
[HasPerm("employees.import")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ImportCommit(string? rowsJson, string? companyId)
{
    if (string.IsNullOrWhiteSpace(rowsJson))
    { ToastErr(HrT.T("داده‌ای برای ثبت نیست")); return RedirectToAction(nameof(Import)); }
    var company = await Db.Companies.FindAsync(companyId ?? "")
                  ?? await Db.Companies.OrderBy(c => c.Code).FirstAsync();
    var (inserted, skipped) = ExcelImport.CommitPeople(Db, rowsJson, company.Id);
    await Audit("excel_import_people", "Employee", null, new { inserted, skipped, company = company.Name });
    ToastOk(HrT.F("ورود گروهی به «{0}» انجام شد — {1} پرسنل ثبت شد", company.Name, HrT.Num(inserted)) + (skipped > 0 ? HrT.F(" ({0} ردیف تکراری نادیده گرفته شد)", HrT.Num(skipped)) : ""));
    if (Ajax) return AjaxResult(true, HrT.F("ورود گروهی انجام شد — {0} پرسنل ثبت شد", HrT.Num(inserted)) + (skipped > 0 ? HrT.F(" ({0} ردیف تکراری نادیده گرفته شد)", HrT.Num(skipped)) : ""));
    return Redirect("/Employees");
}

[HttpPost]
[HasPerm("employees.import")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ImportAttendanceCommit(string? rowsJsonAtt)
{
    if (string.IsNullOrWhiteSpace(rowsJsonAtt))
    { ToastErr(HrT.T("داده‌ای برای ثبت نیست")); return RedirectToAction(nameof(Import)); }
    var (inserted, skipped) = ExcelImport.CommitAttendance(Db, rowsJsonAtt, HttpContext.RequestServices);
    await Audit("excel_import_attendance", "AttendanceRecord", null, new { inserted, skipped });
    ToastOk(HrT.F("ورود تردد تاریخی انجام شد — {0} رکورد ثبت شد", HrT.Num(inserted)) + (skipped > 0 ? HrT.F(" ({0} تکراری نادیده گرفته شد)", HrT.Num(skipped)) : ""));
    if (Ajax) return AjaxResult(true, HrT.F("ورود تردد تاریخی انجام شد — {0} رکورد ثبت شد", HrT.Num(inserted)) + (skipped > 0 ? HrT.F(" ({0} تکراری نادیده گرفته شد)", HrT.Num(skipped)) : ""));
    return Redirect("/Attendance");
}

}
