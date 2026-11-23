using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Access.Data;
using Platform.Contracts;
using HrModules.Access.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// مخزن کارت‌های شناسایی پرسنل (فاز ۲۹) — چرخه کامل کارت:
/// ثبت در انبار → تخصیص به پرسنل (دائم/موقت با انقضا) → عودت / اعلام مفقودی / ابطال → تعویض کارت گمشده.
/// اسکن با روش «کارت» در کنترل تردد از همین مخزن به پرسنل نگاشت می‌شود.
/// فاز ۶۱ — کارت‌ها و پرسنل/شرکت از AccessDbContext (کانتکست خود ماژول)،
/// هشدار امنیتی (SecurityAlert) از HrBaseDbContext — بدون DbContext مرکب هاست.
/// </summary>
public class CardsController : HrModuleControllerBase
{
    private readonly AccessDbContext Db;
    private readonly HrBaseDbContext Base;

    public CardsController(AccessDbContext db, HrBaseDbContext baseDb, ICurrentUser me, IAuditSink audits) : base(me, audits)
    { Db = db; Base = baseDb; }

    public static readonly string[] CardTypes = { "Mifare", "UHF", "QR", "RFID" };
    public static readonly string[] CardStatuses = { "آزاد", "فعال", "موقت", "گمشده", "باطل" };

    [HttpGet]
    [HasPerm("cards.view")]
    public async Task<IActionResult> Index(string? status, string? type, string? co, string? q)
    {
        // فاز ۷۴ — دامنهٔ رکوردی کارت‌ها: فقط کارت‌های پرسنلِ محدودهٔ مجاز (دامنهٔ پرسنل)
        var scope = await this.ScopeAsync();
        var cardsQ = Db.Cards.Include(c => c.Employee).AsQueryable();
        if (!string.IsNullOrEmpty(status)) cardsQ = cardsQ.Where(c => c.Status == status);
        if (!string.IsNullOrEmpty(type)) cardsQ = cardsQ.Where(c => c.Type == type);
        if (!string.IsNullOrEmpty(co)) cardsQ = cardsQ.Where(c => c.Employee!.CompanyId == co);
        cardsQ = cardsQ.WhereEmpScope(scope, c => c.EmployeeId!);
        if (!string.IsNullOrEmpty(q))
            cardsQ = cardsQ.Where(c => c.CardNo.Contains(q)
                || (c.Employee != null && (c.Employee.FullName.Contains(q) || c.Employee.PersonnelCode.Contains(q))));
        var cards = await cardsQ
            .OrderBy(c => c.Status == "آزاد" ? 0 : c.Status == "باطل" ? 2 : 1)
            .ThenBy(c => c.CardNo)
            .ToListAsync();

        // شمارنده وضعیت‌ها (کل مخزن — مستقل از فیلتر)
        ViewBag.Counts = await Db.Cards.GroupBy(c => c.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        ViewBag.Total = await Db.Cards.CountAsync();

        ViewBag.Types = CardTypes;
        ViewBag.Statuses = CardStatuses;
        ViewBag.Companies = (await Db.Companies.OrderBy(c => c.Code).ToListAsync())
            .Where(c => scope.All || scope.Companies.Count == 0 || scope.Companies.Contains(c.Id)).ToList();
        // فاز ۱۱۴ — لیست کامل پرسنل حذف شد؛ انتخاب با جستجوی زندهٔ /Ui/EmpSearch (مقیاس ده‌ها هزار نفر)
        ViewBag.Status = status; ViewBag.Type = type; ViewBag.Co = co; ViewBag.Q = q;
        return View(cards);
    }

    // ======================================================================
    // فاز ۱۳۲ (مورد ۶ ROADMAP-ADDITIONS) — گزارش کارت‌های نیازمند رسیدگی
    // چرخهٔ کارت (فاز ۲۹) تا امروز هیچ صفحه‌ای «کارت‌هایی که باید جمع شود» را
    // نشان نمی‌داد: منقضی و در گردش، گمشدهٔ باز، باطلِ جمع‌نشده، به‌زودی منقضی.
    // ======================================================================

    private static string CategoryOf(Card c, DateTime today) =>
        c.Status == "گمشده" ? "گمشدهٔ باز"
        : c.Status == "باطل" ? "باطلِ جمع‌نشده"
        : c.ExpiresAt != null && c.ExpiresAt.Value.Date < today ? "منقضی و در گردش"
        : "به‌زودی منقضی (۷ روز)";

    private static int CatOrder(string cat) =>
        cat == "منقضی و در گردش" ? 0 : cat == "گمشدهٔ باز" ? 1 : cat == "باطلِ جمع‌نشده" ? 2 : 3;

    [HttpGet]
    [HasPerm("cards.view")]
    public async Task<IActionResult> Report()
    {
        var scope = await this.ScopeAsync();
        var today = DateTime.Today;
        var soon = today.AddDays(7);

        var cards = await Db.Cards.AsNoTracking()
            .Include(c => c.Employee).ThenInclude(e => e!.Department)
            .Where(c => c.EmployeeId != null)
            .WhereEmpScope(scope, c => c.EmployeeId!)
            .ToListAsync();

        var rows = cards
            .Where(c => c.Status == "گمشده"
                || (c.Status == "باطل" && c.ReturnedAt == null)
                || (c.ExpiresAt != null && (c.Status == "فعال" || c.Status == "موقت")
                    && c.ExpiresAt.Value.Date <= soon))
            .Select(c => new CardIssueRow(
                CategoryOf(c, today), c.CardNo, c.Type,
                c.Employee?.FullName ?? "-", c.Employee?.Department?.Name ?? "-",
                c.ExpiresAt, c.Status))
            .OrderBy(r => CatOrder(r.Category)).ThenBy(r => r.ExpiresAt)
            .ToList();

        ViewBag.Expired = rows.Count(r => r.Category == "منقضی و در گردش");
        ViewBag.Lost = rows.Count(r => r.Category == "گمشدهٔ باز");
        ViewBag.NotReturned = rows.Count(r => r.Category == "باطلِ جمع‌نشده");
        ViewBag.Soon = rows.Count(r => r.Category == "به‌زودی منقضی (۷ روز)");
        return View(rows);
    }

    /// <summary>ثبت کارت جدید در انبار (بدون دارنده — آزاد).</summary>
    [HttpPost]
    [HasPerm("cards.issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string cardNo, string type, string? note)
    {
        cardNo = (cardNo ?? "").Trim();
        if (cardNo.Length == 0) return AjaxResult(false, "شماره کارت الزامی است");
        if (await Db.Cards.AnyAsync(c => c.CardNo == cardNo))
            return AjaxResult(false, "این شماره کارت قبلاً ثبت شده است");
        var card = new Card
        {
            CardNo = cardNo,
            Type = CardTypes.Contains(type) ? type : "Mifare",
            Status = "آزاد",
            Note = note,
        };
        Db.Cards.Add(card);
        await Db.SaveChangesAsync();
        await Audit("create", "card", card.Id, new { cardNo, type });
        var msg = $"کارت «{Jalali.ToPersianDigits(cardNo)}» در انبار ثبت شد (آزاد)";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Cards");
    }

    /// <summary>تخصیص کارت به پرسنل — days > 0 یعنی کارت موقت با انقضا.</summary>
    [HttpPost]
    [HasPerm("cards.issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(string id, string employeeId, int? days)
    {
        var card = await Db.Cards.FindAsync(id);
        if (card is null) return NotFound();
        var emp = await Db.Employees.FindAsync(employeeId);
        if (emp is null) return AjaxResult(false, "پرسنل یافت نشد");
        if (card.Status == "گمشده") return AjaxResult(false, "کارت مفقود قابل تخصیص نیست — ابتدا کارت جدید صادر کنید");
        if (card.Status == "باطل") return AjaxResult(false, "کارت باطل قابل تخصیص نیست");
        // کارتِ تخصیص‌یافته فعلی همین پرسنل است؟ بازتخصیص ساده مجاز است
        var activeCount = await Db.Cards.CountAsync(c => c.EmployeeId == employeeId && (c.Status == "فعال" || c.Status == "موقت") && c.Id != id);
        card.EmployeeId = employeeId;
        card.ReturnedAt = null;
        card.UpdatedAt = DateTime.Now;
        if (days is > 0)
        {
            card.Status = "موقت";
            card.ExpiresAt = DateTime.Now.AddDays(days.Value);
        }
        else
        {
            card.Status = "فعال";
            card.ExpiresAt = null;
        }
        await Db.SaveChangesAsync();
        await Audit("issue", "card", card.Id, new { emp.PersonnelCode, card.Status, days });
        var msg = $"کارت «{Jalali.ToPersianDigits(card.CardNo)}» به {emp.FullName} تخصیص یافت" +
                  (days is > 0 ? $" — موقت تا {Jalali.FmtLong(card.ExpiresAt!.Value)}" : "");
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Cards");
    }

    /// <summary>عودت کارت به انبار — آزاد می‌شود (دارنده حذف می‌شود).</summary>
    [HttpPost]
    [HasPerm("cards.state")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(string id)
    {
        var card = await Db.Cards.FindAsync(id);
        if (card is null) return NotFound();
        var holder = card.Employee?.FullName;
        card.Status = "آزاد";
        card.EmployeeId = null;
        card.ExpiresAt = null;
        card.ReturnedAt = DateTime.Now;
        card.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("return", "card", card.Id, new { holder });
        var msg = $"کارت «{Jalali.ToPersianDigits(card.CardNo)}» به انبار عودت داده شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Cards");
    }

    /// <summary>اعلام مفقودی کارت — بلافاصله غیرقابل استفاده در کنترل تردد + هشدار امنیتی.</summary>
    [HttpPost]
    [HasPerm("cards.state")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkLost(string id)
    {
        var card = await Db.Cards.FindAsync(id);
        if (card is null) return NotFound();
        card.Status = "گمشده";
        card.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        Base.SecurityAlerts.Add(new SecurityAlert
        {
            Type = "کارت گمشده", Severity = "بالا",
            Message = $"کارت «{card.CardNo}» به‌عنوان مفقود اعلام شد" +
                      (card.Employee is null ? "" : $" — دارنده: {card.Employee.FullName}") + "؛ اسکن این کارت رد می‌شود",
            Source = "کنترل تردد",
        });
        await Base.SaveChangesAsync();
        await Audit("mark-lost", "card", card.Id, null);
        var msg = $"کارت «{Jalali.ToPersianDigits(card.CardNo)}» مفقود اعلام شد و در کنترل تردد مسدود است";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Cards");
    }

    /// <summary>ابطال کارت (مثلاً کارت خروجی از سازمان) — بدون حذف تاریخچه.</summary>
    [HttpPost]
    [HasPerm("cards.state")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(string id)
    {
        var card = await Db.Cards.FindAsync(id);
        if (card is null) return NotFound();
        card.Status = "باطل";
        card.EmployeeId = null;
        card.ExpiresAt = null;
        card.ReturnedAt = null;
        card.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("void", "card", card.Id, null);
        var msg = $"کارت «{Jalali.ToPersianDigits(card.CardNo)}» باطل شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Cards");
    }

    /// <summary>فعال‌سازی مجدد کارت (بازگشت از ابطال/مفقودی — مثلاً پیدا شدن کارت).</summary>
    [HttpPost]
    [HasPerm("cards.issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string id)
    {
        var card = await Db.Cards.FindAsync(id);
        if (card is null) return NotFound();
        if (card.EmployeeId is null) return AjaxResult(false, "کارت فاقد دارنده است — ابتدا تخصیص دهید");
        card.Status = "فعال";
        card.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("activate", "card", card.Id, null);
        var msg = $"کارت «{Jalali.ToPersianDigits(card.CardNo)}» مجدداً فعال شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Cards");
    }

    /// <summary>ویرایش مشخصات کارت (نوع/انقضا/یادداشت).</summary>
    [HttpPost]
    [HasPerm("cards.issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string type, string? expiresAt, string? note)
    {
        var card = await Db.Cards.FindAsync(id);
        if (card is null) return NotFound();
        card.Type = CardTypes.Contains(type) ? type : card.Type;
        card.Note = note;
        if (!string.IsNullOrEmpty(expiresAt))
        {
            if (DateTime.TryParse(expiresAt, out var dt))
            {
                card.ExpiresAt = dt;
                if (card.Status == "فعال" && dt < DateTime.Now) card.Status = "موقت";
            }
        }
        else card.ExpiresAt = null;
        card.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("edit", "card", card.Id, new { type, expiresAt });
        var msg = $"مشخصات کارت «{Jalali.ToPersianDigits(card.CardNo)}» به‌روزرسانی شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Cards");
    }

    /// <summary>حذف کارت از مخزن — فقط کارت بدون دارنده (آزاد/باطل).</summary>
    [HttpPost]
    [HasPerm("cards.state")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var card = await Db.Cards.FindAsync(id);
        if (card is null) return NotFound();
        if (card.EmployeeId is not null)
            return AjaxResult(false, "کارت دارای دارنده است — ابتدا عودت یا ابطال کنید");
        Db.Cards.Remove(card);
        await Db.SaveChangesAsync();
        await Audit("delete", "card", id, new { card.CardNo });
        var msg = $"کارت «{Jalali.ToPersianDigits(card.CardNo)}» از مخزن حذف شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Cards");
    }
}
