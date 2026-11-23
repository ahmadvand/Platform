using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace Platform.Web.Controllers;

/// <summary>
/// مدیریت شرکت‌ها (فاز ۲۸ — معماری چندشرکتی سراسری).
/// تا پیش از این فاز شرکت‌ها فقط در سیدر ساخته می‌شدند؛ حالا تعریف/ویرایش/توقف/حذف
/// با UI کامل و گاردهای مرجع انجام می‌شود. هر شرکت چارت سازمانی، پست‌ها، شیفت‌ها،
/// دستگاه‌ها، مناطق/گیت‌ها، ارباب‌رجوع و پیکربندی حقوق مستقل خودش را دارد.
/// </summary>
public class CompaniesController : MvcBase
{
    public CompaniesController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    [HttpGet]
    [HasPerm("companies.view")]
    public async Task<IActionResult> Index()
    {
        var companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        var ids = companies.Select(c => c.Id).ToList();

        ViewBag.DeptCounts = await Db.Set<Department>().Where(d => d.CompanyId != null)
            .GroupBy(d => d.CompanyId!).ToDictionaryAsync(g => g.Key, g => g.Count());
        ViewBag.PosCounts = await Db.Set<Position>().Where(p => p.CompanyId != null)
            .GroupBy(p => p.CompanyId!).ToDictionaryAsync(g => g.Key, g => g.Count());
        ViewBag.EmpCounts = await Db.Set<Employee>()
            .GroupBy(e => e.CompanyId).ToDictionaryAsync(g => g.Key, g => g.Count());
        ViewBag.ShiftCounts = await CountsSafeAsync(Db, "attendance", "shifts");
        ViewBag.DeviceCounts = await Db.Set<Device>().Where(d => d.CompanyId != null)
            .GroupBy(d => d.CompanyId!).ToDictionaryAsync(g => g.Key, g => g.Count());
        ViewBag.ZoneCounts = await CountsSafeAsync(Db, "access", "zones");
        ViewBag.VisitorCounts = await CountsSafeAsync(Db, "visitors", "visitors");
        ViewBag.UserCounts = await Db.Users.Where(u => u.CompanyId != null)
            .GroupBy(u => u.CompanyId!).ToDictionaryAsync(g => g.Key, g => g.Count());
        ViewBag.HasPayroll = await PayrollCompanyIdsSafeAsync(Db);
        return View(companies);
    }

    // ---------- فرم‌های پاپ‌آپ (مودال AJAX) ----------
    [HttpGet]
    [HasPerm("companies.create")]
    public IActionResult Create() => PartialView("_CompanyForm");

    [HttpGet]
    [HasPerm("companies.edit")]
    public async Task<IActionResult> EditForm(string id)
    {
        var company = await Db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        ViewBag.Company = company;
        return PartialView("_CompanyEditForm");
    }

    /// <summary>ثبت شرکت جدید — یکتایی کد و نام.</summary>
    [HttpPost]
    [HasPerm("companies.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string code)
    {
        name = (name ?? "").Trim();
        code = (code ?? "").Trim().ToUpperInvariant();
        if (name.Length < 2)
            return Err("نام شرکت را وارد کنید (حداقل ۲ کاراکتر)");
        if (code.Length < 2)
            return Err("کد شرکت را وارد کنید (حداقل ۲ کاراکتر)");
        if (await Db.Companies.AnyAsync(c => c.Code == code))
            return Err($"کد شرکت «{code}» قبلاً استفاده شده است");
        if (await Db.Companies.AnyAsync(c => c.Name == name))
            return Err($"نام شرکت «{name}» قبلاً ثبت شده است");

        var company = new Company { Name = name, Code = code };
        Db.Companies.Add(company);
        await Db.SaveChangesAsync();
        await Audit("create", "company", company.Id, new { company.Name, company.Code });
        var msg = $"شرکت «{company.Name}» با کد {company.Code} ثبت شد — چارت سازمانی و تنظیمات مستقل آن آماده است";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Companies");
    }

    /// <summary>ویرایش نام/کد شرکت.</summary>
    [HttpPost]
    [HasPerm("companies.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string name, string code)
    {
        var company = await Db.Companies.FindAsync(id);
        if (company is null) { ToastErr(HrT.T("شرکت یافت نشد")); if (Ajax) return AjaxResult(false, "شرکت یافت نشد"); return Redirect("/Companies"); }
        name = (name ?? "").Trim();
        code = (code ?? "").Trim().ToUpperInvariant();
        if (name.Length < 2) return Err("نام شرکت را وارد کنید");
        if (code.Length < 2) return Err("کد شرکت را وارد کنید");
        if (await Db.Companies.AnyAsync(c => c.Code == code && c.Id != id))
            return Err($"کد شرکت «{code}» برای شرکت دیگری ثبت شده است");
        if (await Db.Companies.AnyAsync(c => c.Name == name && c.Id != id))
            return Err($"نام شرکت «{name}» برای شرکت دیگری ثبت شده است");

        var oldName = company.Name;
        company.Name = name;
        company.Code = code;
        company.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("update", "company", company.Id, new { old = oldName, newName = name, code });
        var msg = $"مشخصات شرکت «{company.Name}» ذخیره شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Companies");
    }

    /// <summary>توقف/فعال‌سازی شرکت — آخرین شرکت فعال قابل توقف نیست.</summary>
    [HttpPost]
    [HasPerm("companies.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var company = await Db.Companies.FindAsync(id);
        if (company is null) { ToastErr(HrT.T("شرکت یافت نشد")); if (Ajax) return AjaxResult(false, "شرکت یافت نشد"); return Redirect("/Companies"); }
        if (company.Active && await Db.Companies.CountAsync(c => c.Active) <= 1)
        {
            const string m = "توقف ممکن نیست: این تنها شرکت فعال سامانه است";
            ToastErr(m);
            if (Ajax) return AjaxResult(false, m);
            return Redirect("/Companies");
        }
        company.Active = !company.Active;
        company.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("toggle", "company", company.Id, new { active = company.Active });
        var msg = company.Active
            ? $"شرکت «{company.Name}» فعال شد"
            : $"شرکت «{company.Name}» متوقف شد (داده‌ها حفظ می‌شود؛ در انتخاب‌کننده‌ها ظاهر نمی‌شود)";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Companies");
    }

    /// <summary>
    /// حذف شرکت — فقط بدون هیچ داده مرتبط مجاز است (گارد تمام ارجاع‌ها).
    /// در صورت داشتن هر داده، حذف رد می‌شود و پیام فارسی شمارنده نشان داده می‌شود.
    /// </summary>
    [HttpPost]
    [HasPerm("companies.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var company = await Db.Companies.FindAsync(id);
        if (company is null) { ToastErr(HrT.T("شرکت یافت نشد")); if (Ajax) return AjaxResult(false, "شرکت یافت نشد"); return Redirect("/Companies"); }

        var empCount = await Db.Set<Employee>().CountAsync(e => e.CompanyId == id);
        var deptCount = await Db.Set<Department>().CountAsync(d => d.CompanyId == id);
        var shiftCount = await CountForSafeAsync(Db, "attendance", "shifts", id);
        var deviceCount = await Db.Set<Device>().CountAsync(d => d.CompanyId == id);
        var zoneCount = await CountForSafeAsync(Db, "access", "zones", id);
        var visitorCount = await CountForSafeAsync(Db, "visitors", "visitors", id);
        var contractorCount = await CountForSafeAsync(Db, "visitors", "contractors", id);
        var userCount = await Db.Users.CountAsync(u => u.CompanyId == id);
        var runCount = await CountForSafeAsync(Db, "payroll", "runs", id);
        var lotCount = await CountForSafeAsync(Db, "parking", "lots", id);
        var vehicleCount = await CountForSafeAsync(Db, "parking", "vehicles", id);

        var refs = new List<string>();
        if (empCount > 0) refs.Add($"{Jalali.Num(empCount)} پرسنل");
        if (deptCount > 0) refs.Add($"{Jalali.Num(deptCount)} واحد سازمانی");
        if (shiftCount > 0) refs.Add($"{Jalali.Num(shiftCount)} شیفت");
        if (deviceCount > 0) refs.Add($"{Jalali.Num(deviceCount)} دستگاه");
        if (zoneCount > 0) refs.Add($"{Jalali.Num(zoneCount)} منطقه");
        if (visitorCount > 0) refs.Add($"{Jalali.Num(visitorCount)} بازدیدکننده");
        if (contractorCount > 0) refs.Add($"{Jalali.Num(contractorCount)} پیمانکار");
        if (userCount > 0) refs.Add($"{Jalali.Num(userCount)} کاربر");
        if (runCount > 0) refs.Add($"{Jalali.Num(runCount)} لیست حقوق");
        if (lotCount > 0) refs.Add($"{Jalali.Num(lotCount)} پارکینگ");
        if (vehicleCount > 0) refs.Add($"{Jalali.Num(vehicleCount)} خودرو");

        if (refs.Count > 0)
        {
            var m = $"حذف ممکن نیست: شرکت «{company.Name}» {string.Join("، ", refs)} دارد؛ ابتدا داده‌ها را منتقل یا حذف کنید";
            ToastErr(m);
            if (Ajax) return AjaxResult(false, m);
            return Redirect("/Companies");
        }

        // پاک‌سازی پیکربندی‌های وابسته (بدون پرسنل، این‌ها هم خالی‌اند یا قابل حذف)
        await CleanupPayrollConfigSafeAsync(Db, id); // فاز ۶۷ — پیکربندی حقوق ∈ ماژول حقوق

        Db.Companies.Remove(company);
        await Db.SaveChangesAsync();
        await Audit("delete", "company", id, new { company.Name, company.Code });
        var msg = $"شرکت «{company.Name}» حذف شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Companies");
    }

    private IActionResult Err(string msg)
    {
        ToastErr(msg);
        if (Ajax) return AjaxResult(false, msg);
        return Redirect("/Companies");
    }
    /// <summary>فاز ۶۸ — شرکت‌های دارای پیکربندی ∈ پلاگین‌های دیتا (اجتماع؛ قرارداد IHrDataModule).</summary>
    private static Task<List<string>> PayrollCompanyIdsSafeAsync(AppDbContext db) =>
        Platform.Contracts.HrModuleRuntime.CompaniesWithConfigAsync(db);

    /// <summary>فاز ۶۸ — شمارش‌های شرکت ∈ پلاگین دیتای همان ماژول (قرارداد IHrDataModule).</summary>
    private static Task<Dictionary<string, int>> CountsSafeAsync(AppDbContext db, string mod, string kind) =>
        Platform.Contracts.HrModuleRuntime.CompanyCountsAsync(db, mod, kind);

#if HR_MODULE_ATTENDANCE
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> ShiftCountForAsync(AppDbContext db, string id) => db.Set<Shift>().CountAsync(s => s.CompanyId == id);
#endif
#if HR_MODULE_ACCESS
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> ZoneCountForAsync(AppDbContext db, string id) => db.Set<Zone>().CountAsync(z => z.CompanyId == id);
#endif
#if HR_MODULE_VISITORS
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> VisitorCountForAsync(AppDbContext db, string id) => db.Set<Visitor>().CountAsync(v => v.CompanyId == id);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> ContractorCountForAsync(AppDbContext db, string id) => db.Set<Contractor>().CountAsync(c => c.CompanyId == id);
#endif
#if HR_MODULE_PAYROLL
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> RunCountForAsync(AppDbContext db, string id) => db.Set<PayrollRun>().CountAsync(r => r.CompanyId == id);
#endif
#if HR_MODULE_PARKING
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> LotCountForAsync(AppDbContext db, string id) => db.Set<ParkingLot>().CountAsync(l => l.CompanyId == id);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> VehicleCountForAsync(AppDbContext db, string id) => db.Set<Vehicle>().CountAsync(v => v.CompanyId == id);
#endif

    /// <summary>فاز ۶۸ — شمارش برای یک شرکت ∈ پلاگین دیتای همان ماژول.</summary>
    private static Task<int> CountForSafeAsync(AppDbContext db, string mod, string kind, string id) =>
        Platform.Contracts.HrModuleRuntime.CompanyCountAsync(db, mod, kind, id);

    /// <summary>فاز ۶۸ — آبشار حذف شرکت ∈ پلاگین‌های دیتا (OnCompanyDeleteAsync قرارداد).</summary>
    private static Task CleanupPayrollConfigSafeAsync(AppDbContext db, string id) =>
        Platform.Contracts.HrModuleRuntime.CascadeCompanyDeleteAsync(db, id);

}
