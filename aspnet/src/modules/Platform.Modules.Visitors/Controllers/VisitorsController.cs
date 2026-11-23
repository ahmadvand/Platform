using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Platform.Modules.Visitors.Data;
using Platform.Modules.Visitors.Domain;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۶۱ — کنترلر ارباب‌رجوع/بازدیدکننده روی لایهٔ دیتای مستقل ماژول.
/// Db = VisitorsDbContext (شرکت‌ها، بازدیدکنندگان، کارت‌ها، لاگ تردد — مالکیت خود ماژول) و
/// هر تعامل با ماژول کنترل تردد (منطقهٔ اصلی، مجوز دسترسی بازدیدکننده) فقط از طریق
/// قرارداد IAccessGateway انجام می‌شود — بدون هیچ ارجاع مستقیم به entityهای آن ماژول.
/// حسابرسی هم از IAuditSink (HrModuleControllerBase.Audit) عبور می‌کند.
/// </summary>
public class VisitorsController : HrModuleControllerBase
{
    private readonly VisitorsDbContext Db;
    private readonly IAccessGateway Access;

    public VisitorsController(VisitorsDbContext db, IAccessGateway access, ICurrentUser me, IAuditSink audits) : base(me, audits)
    { Db = db; Access = access; }

    // ======================================================================
    // فاز ۱۳۲ (مورد ۱۴ ROADMAP-ADDITIONS) — هشدار مهمانِ تاریخ‌گذشته
    // پرچم «داخل سازمان» با خروجِ ثبت‌شده همیشه هماهنگ نمی‌شود؛ این صفحه
    // مهمان‌هایی را که باید مدت‌هاست خارج شده بودند پیدا می‌کند:
    //   ۱) «داخل سازمان» با کارتِ منقضی (اعتبار کارت گذشته) — هنوز خارج نشده
    //   ۲) کارتِ صادرشده که برنگشته و اعتبارش هم گذشته — ریسک ورود مجدد با کارت بلاتکلیف
    // ======================================================================

    [HttpGet]
    [HasPerm("visitors.view")]
    public async Task<IActionResult> Overdue()
    {
        var scope = await this.ScopeAsync();
        var now = DateTime.Now;

        // مادی‌سازی سپس مرتب‌سازی سمت کلاینت — OrderBy روی ویژگیِ رکوردِ Project شده
        // قابل ترجمهٔ EF نیست (InvalidOperationException در لاگ ۱۸:۲۹)
        var rows = (await Db.Visitors.AsNoTracking()
            .Include(v => v.Card)
            .Include(v => v.Company)
            .Where(v => v.Card != null && v.Card.ValidUntil < now)
            .WhereCoScope(scope, v => v.CompanyId)
            .Where(v => v.Status == "داخل سازمان" || !v.Card!.Returned)
            .Select(v => new VisitorOverdueRow(
                v.Card!.CardNo, v.FirstName + " " + v.LastName, v.HostName, v.HostDept ?? "-",
                v.Company != null ? v.Company.Name : "-", v.Status,
                v.Card.ValidUntil, v.Card.Returned, v.Status == "داخل سازمان"))
            .ToListAsync())
            .OrderBy(v => v.ValidUntil)
            .ToList();

        ViewBag.StillInside = rows.Count(r => r.StillInside);
        ViewBag.CardNotReturned = rows.Count(r => !r.CardReturned);
        return View(rows);
    }

    [HttpGet]
    [HasPerm("visitors.view")]
    public async Task<IActionResult> Index(string tab = "visitors", string? co = null)
    {
        ViewBag.Tab = tab;
        // فاز ۷۴ — دامنهٔ رکوردی ارباب‌رجوع: بازدیدکننده/پیمانکار فقط از شرکت‌های مجاز
        var scope = await this.ScopeAsync();
        if (!scope.All && scope.Companies.Count > 0)
            co = string.IsNullOrEmpty(co) || !scope.Companies.Contains(co) ? null : co;
        ViewBag.Companies = (await Db.Companies.OrderBy(c => c.Code).ToListAsync())
            .Where(c => scope.All || scope.Companies.Count == 0 || scope.Companies.Contains(c.Id)).ToList();
        ViewBag.Co = co;
        var vq = Db.Visitors
            .Include(v => v.Company)
            .Include(v => v.Contractor).Include(v => v.Card).Include(v => v.Logs)
            .AsQueryable();
        var cq = Db.Contractors.Include(c => c.Company).AsQueryable();
        if (!string.IsNullOrEmpty(co))
        {
            vq = vq.Where(v => v.CompanyId == co);
            cq = cq.Where(c => c.CompanyId == co);
        }
        vq = vq.WhereCoScope(scope, v => v.CompanyId);
        cq = cq.WhereCoScope(scope, c => c.CompanyId);
        ViewBag.Visitors = await vq.OrderByDescending(v => v.RequestedAt).ToListAsync();
        ViewBag.Contractors = await cq.OrderByDescending(c => c.CreatedAt).ToListAsync();
        ViewBag.TodayInside = await Db.Visitors.CountAsync(v => v.Status == "داخل سازمان");
        return View();
    }

    // ---------- فرم‌های پاپ‌آپ (مودال AJAX) ----------
    [HttpGet]
    [HasPerm("visitors.register")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        ViewBag.Contractors = await Db.Contractors.OrderBy(c => c.Name).ToListAsync();
        return PartialView("_VisitForm");
    }

    [HttpGet]
    [HasPerm("visitors.register")]
    public async Task<IActionResult> CreateContractor()
    {
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        return PartialView("_ContractorForm");
    }

    [HttpPost]
    [HasPerm("visitors.register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string firstName, string lastName, string nationalId,
        string? phone, string idDocType, string type, string hostName, string? hostDept,
        string purpose, string? contractorId, string? companyId)
    {
        var company = await Db.Companies.FindAsync(companyId ?? "")
                      ?? await Db.Companies.OrderBy(c => c.Code).FirstOrDefaultAsync();
        if (company is null)
        { ToastErr("ابتدا یک شرکت تعریف کنید"); if (Ajax) return AjaxResult(false, "ابتدا یک شرکت تعریف کنید"); return Redirect("/Visitors"); }
        var v = new Visitor
        {
            FirstName = firstName.Trim(), LastName = lastName.Trim(), NationalId = nationalId.Trim(),
            Phone = phone, IdDocType = idDocType, Type = type, HostName = hostName,
            HostDept = hostDept, Purpose = purpose, ContractorId = contractorId,
            CompanyId = company.Id,
            Status = "در انتظار تأیید", RequestedAt = DateTime.Now,
        };
        Db.Visitors.Add(v);
        await Db.SaveChangesAsync();
        await Audit("create", "visitor", v.Id, new { v.FullName, v.Type, company = company.Name });
        ToastOk(HrT.F("درخواست بازدید «{0}» برای شرکت «{1}» ثبت شد", v.FullName, company.Name));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Visitors");
    }

    [HttpPost]
    [HasPerm("visitors.register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(string id, bool approve)
    {
        var v = await Db.Visitors.FindAsync(id);
        if (v is null) return NotFound();
        v.Status = approve ? "تأیید شده" : "رد شده";
        v.ApprovedBy = Sess!.FullName;
        v.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit(approve ? "approve" : "reject", "visitor", id, null);
        ToastOk(HrT.F("بازدید «{0}» {1}", v.FullName, HrT.T(v.Status ?? "")));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Visitors");
    }

    /// <summary>صدور کارت بازدیدکننده (QR/بارکد/RFID).</summary>
    [HttpPost]
    [HasPerm("visitors.flow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueCard(string id, string type)
    {
        var v = await Db.Visitors.Include(x => x.Card).FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();
        if (v.Card is not null) { ToastErr("برای این بازدیدکننده قبلاً کارت صادر شده است"); if (Ajax) return AjaxResult(false, "برای این بازدیدکننده قبلاً کارت صادر شده است"); return Redirect("/Visitors"); }
        v.Card = new VisitorCard
        {
            CardNo = $"VC-{Random.Shared.Next(10000, 99999)}",
            Type = type, VisitorId = v.Id,
            ValidUntil = DateTime.Now.AddHours(8),
        };
        v.Status = "تأیید شده";
        v.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("issue_card", "visitor", id, new { cardType = type });
        ToastOk(HrT.F("کارت {0} برای «{1}» صادر شد", HrT.Num(v.Card.CardNo), v.FullName));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Visitors");
    }

    /// <summary>ثبت ورود بازدیدکننده + مجوز دسترسی موقت به منطقه اصلی.</summary>
    [HttpPost]
    [HasPerm("visitors.flow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(string id)
    {
        var v = await Db.Visitors.Include(x => x.Card).FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();
        if (v.Card is null) { ToastErr("ابتدا کارت صادر کنید"); if (Ajax) return AjaxResult(false, "ابتدا کارت صادر کنید"); return Redirect("/Visitors"); }
        if (v.Card.ValidUntil < DateTime.Now) { ToastErr("اعتبار کارت منقضی شده است"); if (Ajax) return AjaxResult(false, "اعتبار کارت منقضی شده است"); return Redirect("/Visitors"); }

        v.Status = "داخل سازمان";
        v.UpdatedAt = DateTime.Now;
        Db.VisitLogs.Add(new VisitLog { VisitorId = v.Id, Direction = "ورود", GateName = "گیت اصلی ورودی", Timestamp = DateTime.Now });
        // مجوز موقت ورودی اصلی (منطقهٔ کد Z-MAIN) — فقط از طریق قرارداد IAccessGateway؛
        // دادهٔ Zone/AccessPermission مالکیت ماژول کنترل تردد است و اینجا مستقیم لمس نمی‌شود.
        var mainZoneId = await Access.FindZoneIdByCodeAsync("Z-MAIN");
        if (mainZoneId is not null && !await Access.HasVisitorPermissionAsync(v.Id, mainZoneId))
            await Access.GrantVisitorPermissionAsync(v.Id, mainZoneId, "موقت", v.Card!.ValidUntil);
        await Db.SaveChangesAsync();
        await Audit("check_in", "visitor", id, null);
        ToastOk(HrT.F("«{0}» وارد سازمان شد", v.FullName));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Visitors");
    }

    /// <summary>ثبت خروج + استرداد کارت (انقضای مجوز موقت در لایهٔ دیتای کنترل تردد).</summary>
    [HttpPost]
    [HasPerm("visitors.flow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Exit(string id)
    {
        var v = await Db.Visitors.Include(x => x.Card).Include(x => x.Logs)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();
        v.Status = "خارج شده";
        v.UpdatedAt = DateTime.Now;
        if (v.Card is not null) v.Card.Returned = true;
        Db.VisitLogs.Add(new VisitLog { VisitorId = v.Id, Direction = "خروج", GateName = "گیت اصلی خروجی", Timestamp = DateTime.Now });
        // انقضای مجوزهای بازدیدکننده از مسیر قرارداد ماژول کنترل تردد (رفتار عیناً مثل قبل — فاز ۶۱)
        await Access.ExpireVisitorPermissionsAsync(v.Id);
        await Db.SaveChangesAsync();
        await Audit("check_out", "visitor", id, null);
        ToastOk(HrT.F("«{0}» خارج شد و کارت استرداد گردید", v.FullName));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Visitors");
    }

    [HttpPost]
    [HasPerm("visitors.register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContractor(string name, string code, string? contactName,
        string? phone, string? contractNo, DateTime? validUntil, string? companyId)
    {
        var company = await Db.Companies.FindAsync(companyId ?? "")
                      ?? await Db.Companies.OrderBy(c => c.Code).FirstOrDefaultAsync();
        if (company is null)
        { ToastErr("ابتدا یک شرکت تعریف کنید"); if (Ajax) return AjaxResult(false, "ابتدا یک شرکت تعریف کنید"); return Redirect("/Visitors?tab=contractors"); }
        if (await Db.Contractors.AnyAsync(c => c.Code == code && c.CompanyId == company.Id))
        { ToastErr("کد پیمانکار در این شرکت تکراری است"); if (Ajax) return AjaxResult(false, "کد پیمانکار در این شرکت تکراری است"); return Redirect("/Visitors?tab=contractors"); }
        var c = new Contractor
        {
            Name = name.Trim(), Code = code.Trim().ToUpperInvariant(),
            ContactName = contactName, Phone = phone, ContractNo = contractNo, ValidUntil = validUntil,
            CompanyId = company.Id,
        };
        Db.Contractors.Add(c);
        await Db.SaveChangesAsync();
        await Audit("create", "contractor", c.Id, new { c.Name, company = company.Name });
        ToastOk(HrT.F("پیمانکار «{0}» برای شرکت «{1}» ثبت شد", c.Name, company.Name));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Visitors?tab=contractors");
    }

    // ======================================================================
    // فاز ۱۳۳ (مورد ۷ ROADMAP-ADDITIONS) — مدت ملاقات مهمان
    // جفت‌کردن لاگ ورود/خروج هر بازدیدکننده (۳۰ روز اخیر) و محاسبهٔ مدت؛
    // خروجِ ثبت‌نشده = ملاقات در جریان (و همان «مهمان‌های تاریخ‌گذشته» را هم
    // توضیح می‌دهد: ملاقاتِ در جریانی که اعتبار کارتش گذشته). پیمایش از
    // آخرین ورود به عقب — هر ورود، خروجِ بعدیِ همان بازدیدکننده را می‌گیرد.
    // ======================================================================

    [HttpGet]
    [HasPerm("visitors.view")]
    public async Task<IActionResult> Duration()
    {
        var scope = await this.ScopeAsync();
        var since = DateTime.Now.AddDays(-30);

        var logs = await Db.VisitLogs.AsNoTracking()
            .Include(l => l.Visitor).ThenInclude(v => v!.Company)
            .Where(l => l.Timestamp >= since)
            .WhereCoScope(scope, l => l.Visitor!.CompanyId)
            .OrderBy(l => l.Timestamp)
            .Select(l => new { l.VisitorId, l.Direction, l.Timestamp,
                Name = l.Visitor!.FirstName + " " + l.Visitor.LastName,
                Host = l.Visitor.HostName, Company = l.Visitor.Company != null ? l.Visitor.Company.Name : "-" })
            .ToListAsync();

        var open = new Dictionary<string, (DateTime At, string Name, string Host, string Company)>();
        var rows = new List<VisitDurationRow>();
        foreach (var l in logs)
        {
            if (l.Direction == "ورود")
                open[l.VisitorId] = (l.Timestamp, l.Name, l.Host, l.Company);
            else if (open.Remove(l.VisitorId, out var e))
                rows.Add(new VisitDurationRow(e.Name, e.Host, e.Company, e.At, l.Timestamp,
                    (int)(l.Timestamp - e.At).TotalMinutes, false));
        }
        foreach (var e in open.Values)
            rows.Add(new VisitDurationRow(e.Name, e.Host, e.Company, e.At, null,
                (int)(DateTime.Now - e.At).TotalMinutes, true));
        rows = rows.OrderByDescending(r => r.Entry).Take(200).ToList();

        ViewBag.Ongoing = rows.Count(r => r.Ongoing);
        ViewBag.AvgMin = rows.Count(r => !r.Ongoing) > 0
            ? (int)rows.Where(r => !r.Ongoing).Average(r => r.Minutes) : 0;
        ViewBag.MaxMin = rows.Count(r => !r.Ongoing) > 0
            ? rows.Where(r => !r.Ongoing).Max(r => r.Minutes) : 0;

        // فاز ۱۳۴ (تکمیل مورد ۷) — پیک ملاقات: توزیع ساعتیِ ورودها در بازه
        var hourHist = new int[24];
        foreach (var r in rows) hourHist[r.Entry.Hour]++;
        var peakHour = Array.IndexOf(hourHist, hourHist.Max());
        ViewBag.HourHist = hourHist.Select((n, h) => new { Hour = h, Count = n }).ToList();
        ViewBag.PeakHour = hourHist.Max() > 0 ? peakHour : -1;
        ViewBag.PeakCount = hourHist.Max();
        return View(rows);
    }
}
