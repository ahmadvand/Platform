using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Org;

/// <summary>
/// چارت سازمانی چندشرکتی (فاز ۲۸) — هر شرکت چارت و پست‌های مستقل خودش را دارد.
/// انتخاب شرکت با پارامتر co (تب‌های بالای صفحه)؛ همه کوئری‌ها/فرم‌ها به همان شرکت محدود می‌شوند.
/// برای سازگاری با جریان‌های قبلی، companyId خالی در اکشن‌ها به اولین شرکت برمی‌گردد.
/// </summary>
public class OrgController : MvcBase
{
    public OrgController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    // فاز ۶۹ — DbSetهای خانوادهٔ سازمان روی کانتکست مرکب (مالکیت اسکیما ∈ OrgDataModule.ComposeModel)
    private DbSet<Department> Departments => Db.Set<Department>();
    private DbSet<Position> Positions => Db.Set<Position>();
    private DbSet<Employee> Employees => Db.Set<Employee>();

    [HttpGet]
    [HasPerm("org.view")]
    public async Task<IActionResult> Index(string? co)
    {
        var scope = await this.ScopeAsync();   // فاز ۷۴ — دامنهٔ رکوردی چارت سازمانی
        var companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        var company = companies.FirstOrDefault(c => c.Id == co)
                      ?? companies.FirstOrDefault(c => c.Active)
                      ?? companies.FirstOrDefault();
        // کاربر محدودشده به شرکت‌های مشخص — فقط همان شرکت‌ها؛ انتخاب کاربر بیرون دامنه = اولین شرکتِ مجاز
        if (!scope.All && scope.Companies.Count > 0)
        {
            companies = companies.Where(c => scope.Companies.Contains(c.Id)).ToList();
            if (company is null || !scope.Companies.Contains(company.Id))
                company = companies.FirstOrDefault(c => c.Active) ?? companies.FirstOrDefault();
        }
        var cid = company?.Id ?? "";
        var depts = await Departments.Where(d => d.CompanyId == cid)
            .Include(d => d.Positions).OrderBy(d => d.Code).ToListAsync();
        // دامنهٔ واحد — فقط زیردرختهای مجاز (کشف‌شده در DataScopeService)؛ بدون ردیف دامنهٔ واحد = همه
        if (!scope.All && scope.Depts.Count > 0)
            depts = depts.Where(d => scope.Depts.Contains(d.Id)).ToList();
        ViewBag.Companies = companies;
        ViewBag.Company = company;
        // پست‌ها بر اساس شرکت مالک پست؛ پست‌های قدیمی بدون شرکت از واحد سازمانی استخراج می‌شوند
        ViewBag.Positions = await Positions
            .Include(p => p.Department)
            .Where(p => p.CompanyId == cid || (p.CompanyId == null && p.Department!.CompanyId == cid))
            .OrderBy(p => p.Code).ToListAsync();
        var roots = await Departments.Where(d => d.ParentId == null && d.CompanyId == cid).OrderBy(d => d.Code)
            .Include(d => d.Children).Include(d => d.Positions).ToListAsync();
        if (!scope.All && scope.Depts.Count > 0)
            roots = roots.Where(d => scope.Depts.Contains(d.Id)).ToList();
        ViewBag.Roots = roots;
        return View();
    }

    // ---------- فرم‌های پاپ‌آپ (مودال AJAX) ----------
    private async Task<List<Department>> DeptsOfAsync(string? co)
    {
        var companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        var company = companies.FirstOrDefault(c => c.Id == co)
                      ?? companies.FirstOrDefault(c => c.Active)
                      ?? companies.FirstOrDefault();
        ViewBag.Company = company;
        var cid = company?.Id ?? "";
        return await Departments.Where(d => d.CompanyId == cid).OrderBy(d => d.Code).ToListAsync();
    }

    [HttpGet]
    [HasPerm("org.depts.manage")]
    public async Task<IActionResult> CreateDept(string? co)
    {
        ViewBag.AllDepts = await DeptsOfAsync(co);
        return PartialView("_DeptForm");
    }

    [HttpGet]
    [HasPerm("org.positions.manage")]
    public async Task<IActionResult> CreatePos(string? co)
    {
        ViewBag.AllDepts = await DeptsOfAsync(co);
        return PartialView("_PosForm");
    }

    [HttpGet]
    [HasPerm("org.depts.manage")]
    public async Task<IActionResult> EditDeptForm(string id)
    {
        var dept = await Departments.FindAsync(id);
        if (dept is null) return NotFound();
        ViewBag.Dept = dept;
        return PartialView("_DeptEditForm");
    }

    [HttpGet]
    [HasPerm("org.depts.manage")]
    public async Task<IActionResult> MoveDeptForm(string id)
    {
        var dept = await Departments.FindAsync(id);
        if (dept is null) return NotFound();
        var all = await Departments.Where(d => d.CompanyId == dept.CompanyId).OrderBy(d => d.Code).ToListAsync();
        // مقصدهای مجاز = همه واحدهای همان شرکت به‌جز خود واحد و نوادگانش (جلوگیری از حلقه)
        var childrenMap = all.Where(x => x.ParentId != null).GroupBy(x => x.ParentId!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        var excluded = new HashSet<string> { dept.Id };
        var stack = childrenMap.TryGetValue(dept.Id, out var kids) ? new List<string>(kids) : new List<string>();
        while (stack.Count > 0)
        {
            var cur = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            if (excluded.Add(cur) && childrenMap.TryGetValue(cur, out var grand))
                foreach (var g in grand) stack.Add(g);
        }
        ViewBag.Dept = dept;
        ViewBag.MoveTargets = all.Where(x => !excluded.Contains(x.Id)).ToList();
        ViewBag.DeptNames = all.ToDictionary(x => x.Id, x => x.Name);
        ViewBag.OldName = dept.ParentId is null ? "ریشه چارت"
            : (all.FirstOrDefault(x => x.Id == dept.ParentId)?.Name ?? "—");
        ViewBag.EmpCount = await Employees.CountAsync(e => e.DepartmentId == dept.Id);
        return PartialView("_DeptMoveForm");
    }

    [HttpGet]
    [HasPerm("org.positions.manage")]
    public async Task<IActionResult> EditPosForm(string id)
    {
        var pos = await Positions.Include(p => p.Department).FirstOrDefaultAsync(p => p.Id == id);
        if (pos is null) return NotFound();
        var cid = pos.CompanyId ?? pos.Department?.CompanyId ?? "";
        ViewBag.Pos = pos;
        ViewBag.AllDepts = await Departments.Where(d => d.CompanyId == cid).OrderBy(d => d.Code).ToListAsync();
        return PartialView("_PosEditForm");
    }

    [HttpPost]
    [HasPerm("org.depts.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDept(string name, string code, string? parentId, string? companyId)
    {
        try
        {
            var company = await Db.Companies.FindAsync(companyId ?? "")
                          ?? await Db.Companies.OrderBy(c => c.Code).FirstAsync();
            var cid = company.Id;
            if (await Departments.AnyAsync(d => d.Code == code && d.CompanyId == cid))
            { ToastErr("کد واحد سازمانی در این شرکت تکراری است"); if (Ajax) return AjaxResult(false, "کد واحد سازمانی در این شرکت تکراری است"); return Redirect("/Org?co=" + cid); }
            // واحد والد باید از همان شرکت باشد
            Department? parent = null;
            if (!string.IsNullOrEmpty(parentId))
            {
                parent = await Departments.FindAsync(parentId);
                if (parent is null || parent.CompanyId != cid)
                { ToastErr("واحد والد باید در همان شرکتِ واحد جدید باشد"); if (Ajax) return AjaxResult(false, "واحد والد باید در همان شرکتِ واحد جدید باشد"); return Redirect("/Org?co=" + cid); }
            }
            var dept = new Department { Name = name.Trim(), Code = code.Trim().ToUpperInvariant(), CompanyId = cid, ParentId = parent?.Id };
            Departments.Add(dept);
            await Db.SaveChangesAsync();
            await Audit("create", "department", dept.Id, new { dept.Name, dept.Code, company = company.Name });
            ToastOk(HrT.F("واحد «{0}» در «{1}» ایجاد شد", dept.Name, company.Name));
            if (Ajax) return AjaxResult(true, HrT.F("واحد «{0}» در «{1}» ایجاد شد", dept.Name, company.Name));
        }
        catch (Exception ex) { ToastErr(HrT.F("خطا در ایجاد واحد: {0}", ex.Message)); }
        if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? HrT.T("خطا در عملیات"));
        return Redirect("/Org");
    }

    [HttpPost]
    [HasPerm("org.positions.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePos(string title, string code, string departmentId, string? grade)
    {
        try
        {
            var dept = await Departments.FindAsync(departmentId);
            if (dept is null)
            { ToastErr("واحد سازمانی انتخاب‌شده یافت نشد"); if (Ajax) return AjaxResult(false, "واحد سازمانی انتخاب‌شده یافت نشد"); return Redirect("/Org"); }
            if (await Positions.AnyAsync(p => p.Code == code && p.CompanyId == dept.CompanyId))
            { ToastErr("کد پست سازمانی در این شرکت تکراری است"); if (Ajax) return AjaxResult(false, "کد پست سازمانی در این شرکت تکراری است"); return Redirect("/Org?co=" + dept.CompanyId); }
            var pos = new Position { Title = title.Trim(), Code = code.Trim().ToUpperInvariant(), DepartmentId = departmentId, Grade = grade, CompanyId = dept.CompanyId };
            Positions.Add(pos);
            await Db.SaveChangesAsync();
            await Audit("create", "position", pos.Id, new { pos.Title, pos.Code, dept.Name });
            ToastOk(HrT.F("پست «{0}» ایجاد شد", pos.Title));
            if (Ajax) return AjaxResult(true, HrT.F("پست «{0}» ایجاد شد", pos.Title));
        }
        catch (Exception ex) { ToastErr(HrT.F("خطا در ایجاد پست: {0}", ex.Message)); }
        if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? HrT.T("خطا در عملیات"));
        return Redirect("/Org");
    }

    // ---------- ویرایش / جابجایی / حذف واحدهای سازمانی ----------

    [HttpPost]
    [HasPerm("org.depts.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDept(string id, string name, string code)
    {
        try
        {
            var dept = await Departments.FindAsync(id);
            if (dept is null) { ToastErr("واحد سازمانی یافت نشد"); return Redirect("/Org"); }
            var newCode = code.Trim().ToUpperInvariant();
            if (await Departments.AnyAsync(d => d.Code == newCode && d.CompanyId == dept.CompanyId && d.Id != id))
            { ToastErr("کد واحد سازمانی در این شرکت تکراری است"); if (Ajax) return AjaxResult(false, "کد واحد سازمانی در این شرکت تکراری است"); return Redirect("/Org?co=" + dept.CompanyId); }
            var oldName = dept.Name;
            dept.Name = name.Trim();
            dept.Code = newCode;
            await Db.SaveChangesAsync();
            await Audit("update", "department", dept.Id, new { old = oldName, newName = dept.Name, code = dept.Code });
            ToastOk(HrT.F("واحد «{0}» ویرایش شد", dept.Name));
            if (Ajax) return AjaxResult(true, HrT.F("واحد «{0}» ویرایش شد", dept.Name));
        }
        catch (Exception ex) { ToastErr(HrT.F("خطا در ویرایش واحد: {0}", ex.Message)); }
        if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? HrT.T("خطا در عملیات"));
        return Redirect("/Org");
    }

    [HttpPost]
    [HasPerm("org.depts.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveDept(string id, string? parentId, bool leaveEmployees = false)
    {
        try
        {
            var dept = await Departments.FindAsync(id);
            if (dept is null) { ToastErr("واحد سازمانی یافت نشد"); return Redirect("/Org"); }
            var back = "/Org?co=" + dept.CompanyId;

            if (!string.IsNullOrEmpty(parentId))
            {
                if (parentId == id)
                { ToastErr("یک واحد نمی‌تواند زیرمجموعه خودش باشد"); if (Ajax) return AjaxResult(false, "یک واحد نمی‌تواند زیرمجموعه خودش باشد"); return Redirect(back); }

                // گارد چندشرکتی: والد باید از همان شرکت واحد باشد
                var parent = await Departments.FindAsync(parentId);
                if (parent is null || parent.CompanyId != dept.CompanyId)
                { ToastErr("جابجایی بین شرکت‌ها مجاز نیست — واحد والد باید در همان شرکت باشد"); if (Ajax) return AjaxResult(false, "جابجایی بین شرکت‌ها مجاز نیست — واحد والد باید در همان شرکت باشد"); return Redirect(back); }

                // جلوگیری از حلقه: والد جدید نباید از نوادگان خود واحد باشد
                var all = await Departments.Where(d => d.CompanyId == dept.CompanyId).Select(d => new { d.Id, d.ParentId }).ToListAsync();
                var byParent = all.Where(d => d.ParentId != null).GroupBy(d => d.ParentId!).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
                var stack = new List<string>(byParent.TryGetValue(id, out var kids) ? kids : new List<string>());
                var seen = new HashSet<string>(stack);
                while (stack.Count > 0)
                {
                    var cur = stack[^1];
                    stack.RemoveAt(stack.Count - 1);
                    if (cur == parentId)
                    { ToastErr("جابجایی مجاز نیست: واحد انتخاب‌شده زیرمجموعهٔ همین واحد است (ایجاد حلقه در چارت)"); if (Ajax) return AjaxResult(false, "جابجایی مجاز نیست: واحد انتخاب‌شده زیرمجموعهٔ همین واحد است (ایجاد حلقه در چارت)"); return Redirect(back); }
                    if (byParent.TryGetValue(cur, out var grand))
                        foreach (var g in grand) if (seen.Add(g)) stack.Add(g);
                }
            }
            else parentId = null;

            var oldParentId = dept.ParentId;
            var oldParentName = oldParentId is null
                ? "ریشه چارت"
                : await Departments.Where(d => d.Id == oldParentId).Select(d => d.Name).FirstOrDefaultAsync() ?? "؟";

            // گزینه «جابجایی بدون پرسنل»: پرسنل مستقیم این واحد در والد قبلی می‌مانند
            int leftBehind = 0;
            if (leaveEmployees)
            {
                var directs = await Employees.Where(e => e.DepartmentId == id).ToListAsync();
                foreach (var emp in directs) emp.DepartmentId = oldParentId;
                leftBehind = directs.Count;
            }

            dept.ParentId = parentId;
            await Db.SaveChangesAsync();
            var newParent = parentId is null ? "ریشه چارت" : await Departments.Where(d => d.Id == parentId).Select(d => d.Name).FirstOrDefaultAsync() ?? "؟";
            await Audit("move", "department", dept.Id, new { oldParentId, newParentId = parentId, leaveEmployees, leftBehind });
            var okMsg = leaveEmployees
                ? $"واحد «{dept.Name}» به «{newParent}» منتقل شد — {leftBehind} نفر پرسنل مستقیم در «{oldParentName}» باقی ماندند"
                : $"واحد «{dept.Name}» به همراه پرسنل به «{newParent}» منتقل شد";
            ToastOk(okMsg);
            if (Ajax) return AjaxResult(true, okMsg);
        }
        catch (Exception ex) { ToastErr(HrT.F("خطا در جابجایی واحد: {0}", ex.Message)); }
        if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? HrT.T("خطا در عملیات"));
        return Redirect("/Org");
    }

    [HttpPost]
    [HasPerm("org.depts.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDept(string id)
    {
        try
        {
            var dept = await Departments.Include(d => d.Children).Include(d => d.Positions).FirstOrDefaultAsync(d => d.Id == id);
            if (dept is null) { ToastErr("واحد سازمانی یافت نشد"); return Redirect("/Org"); }

            // فاز ۶۹ — navigation حذف‌شدهٔ Department.Employees با شمارش FK جایگزین شد
            var empCount = await Employees.CountAsync(e => e.DepartmentId == dept.Id);
            if (dept.Children.Count > 0)
            { var m = HrT.F("حذف ممکن نیست: این واحد {0} زیرمجموعه دارد؛ اول زیرمجموعه‌ها را حذف یا جابجا کنید", HrT.Num(dept.Children.Count)); ToastErr(m); if (Ajax) return AjaxResult(false, m); return Redirect("/Org?co=" + dept.CompanyId); }
            if (dept.Positions.Count > 0)
            { var m = HrT.F("حذف ممکن نیست: {0} پست سازمانی به این واحد متصل است؛ اول پست‌ها را حذف یا منتقل کنید", HrT.Num(dept.Positions.Count)); ToastErr(m); if (Ajax) return AjaxResult(false, m); return Redirect("/Org?co=" + dept.CompanyId); }
            if (empCount > 0)
            { var m = HrT.F("حذف ممکن نیست: {0} نفر پرسنل در این واحد است؛ اول پرسنل را جابجا کنید", HrT.Num(empCount)); ToastErr(m); if (Ajax) return AjaxResult(false, m); return Redirect("/Org?co=" + dept.CompanyId); }

            Departments.Remove(dept);
            await Db.SaveChangesAsync();
            await Audit("delete", "department", dept.Id, new { dept.Name, dept.Code });
            ToastOk(HrT.F("واحد «{0}» حذف شد", dept.Name));
            if (Ajax) return AjaxResult(true, HrT.F("واحد «{0}» حذف شد", dept.Name));
        }
        catch (Exception ex) { ToastErr(HrT.F("خطا در حذف واحد: {0}", ex.Message)); }
        if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? HrT.T("خطا در عملیات"));
        return Redirect("/Org");
    }

    // ---------- ویرایش / حذف پست‌های سازمانی ----------

    [HttpPost]
    [HasPerm("org.positions.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPos(string id, string title, string code, string? grade, string departmentId)
    {
        try
        {
            var pos = await Positions.FindAsync(id);
            if (pos is null) { ToastErr("پست سازمانی یافت نشد"); return Redirect("/Org"); }
            var newDept = await Departments.FindAsync(departmentId);
            if (newDept is null)
            { ToastErr("واحد سازمانی انتخاب‌شده یافت نشد"); if (Ajax) return AjaxResult(false, "واحد سازمانی انتخاب‌شده یافت نشد"); return Redirect("/Org"); }
            if (newDept.CompanyId != pos.CompanyId)
            { ToastErr("انتقال پست به واحد شرکت دیگر مجاز نیست — پست و واحد باید هم‌شرکت باشند"); if (Ajax) return AjaxResult(false, "انتقال پست به واحد شرکت دیگر مجاز نیست — پست و واحد باید هم‌شرکت باشند"); return Redirect("/Org?co=" + pos.CompanyId); }
            var newCode = code.Trim().ToUpperInvariant();
            if (await Positions.AnyAsync(p => p.Code == newCode && p.CompanyId == pos.CompanyId && p.Id != id))
            { ToastErr("کد پست سازمانی در این شرکت تکراری است"); if (Ajax) return AjaxResult(false, "کد پست سازمانی در این شرکت تکراری است"); return Redirect("/Org?co=" + pos.CompanyId); }
            var oldTitle = pos.Title;
            pos.Title = title.Trim();
            pos.Code = newCode;
            pos.Grade = grade;
            pos.DepartmentId = departmentId;
            pos.UpdatedAt = DateTime.Now;
            await Db.SaveChangesAsync();
            await Audit("update", "position", pos.Id, new { old = oldTitle, newTitle = pos.Title, code = pos.Code });
            ToastOk(HrT.F("پست «{0}» ویرایش شد", pos.Title));
            if (Ajax) return AjaxResult(true, HrT.F("پست «{0}» ویرایش شد", pos.Title));
        }
        catch (Exception ex) { ToastErr(HrT.F("خطا در ویرایش پست: {0}", ex.Message)); }
        if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? HrT.T("خطا در عملیات"));
        return Redirect("/Org");
    }

    [HttpPost]
    [HasPerm("org.positions.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePos(string id)
    {
        try
        {
            var pos = await Positions.Include(p => p.Employees).FirstOrDefaultAsync(p => p.Id == id);
            if (pos is null) { ToastErr("پست سازمانی یافت نشد"); return Redirect("/Org"); }
            if (pos.Employees.Count > 0)
            { var m = HrT.F("حذف ممکن نیست: {0} نفر پرسنل این پست را دارند؛ اول پرسنل را به پست دیگری منتقل کنید", HrT.Num(pos.Employees.Count)); ToastErr(m); if (Ajax) return AjaxResult(false, m); return Redirect("/Org?co=" + pos.CompanyId); }
            Positions.Remove(pos);
            await Db.SaveChangesAsync();
            await Audit("delete", "position", pos.Id, new { pos.Title, pos.Code });
            ToastOk(HrT.F("پست «{0}» حذف شد", pos.Title));
            if (Ajax) return AjaxResult(true, HrT.F("پست «{0}» حذف شد", pos.Title));
        }
        catch (Exception ex) { ToastErr(HrT.F("خطا در حذف پست: {0}", ex.Message)); }
        if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? HrT.T("خطا در عملیات"));
        return Redirect("/Org");
    }
}
