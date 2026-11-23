using Platform.Web.Data;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Platform.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

public class AuditController : MvcBase
{
    public AuditController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    [HttpGet]
    [HasPerm("audit.view")]
    public async Task<IActionResult> Index(string? action, string? entity, string? q, int take = 200)
    {
        var query = Db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(action)) query = query.Where(a => a.Action == action);
        if (!string.IsNullOrEmpty(entity)) query = query.Where(a => a.Entity == entity);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.ActorName!.Contains(q) || a.EntityId!.Contains(q));

        var logs = await query.OrderByDescending(a => a.At).Take(Math.Clamp(take, 50, 500)).ToListAsync();
        ViewBag.Logs = logs;
        ViewBag.Action = action;
        ViewBag.Entity = entity;
        ViewBag.Q = q;
        ViewBag.Actions = await Db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Entities = await Db.AuditLogs.Select(a => a.Entity).Distinct().OrderBy(x => x).ToListAsync();
        return View();
    }

    // ============================================================
    // فاز ۱۱۷ — راستی‌آزمایی زنجیرهٔ ضد دستکاری (درخواست کاربر لیست۳-۵):
    // کل زنجیرهٔ ممیزی بازمحاسبه و مقایسه می‌شود؛ هر ویرایش/حذف/درج خارج از
    // ترتیب در پایگاه، اولین نقطهٔ شکست را گزارش می‌کند. ردیف‌های قدمتی
    // (پیش از فاز ۱۱۷ — بدون هش) خارج از زنجیره شمرده می‌شوند.
    // ============================================================
    [HttpGet]
    [HasPerm("audit.view")]
    public async Task<IActionResult> VerifyChain()
    {
        var r = await AuditChain.VerifyAsync(Db.AuditLogs);
        await Audit("verify_chain", "audit", r.Ok ? "ok" : (r.BrokenId ?? "?"),
            new { r.Ok, r.Checked, r.Legacy, r.Total, r.BrokenSeq, r.Reason });
        return Json(new
        {
            ok = r.Ok,
            checkedRows = r.Checked,
            legacy = r.Legacy,
            total = r.Total,
            brokenSeq = r.BrokenSeq,
            brokenId = r.BrokenId,
            reason = r.Reason,
            message = r.Ok
                ? HrT.F("زنجیرهٔ ممیزی سالم است — {0} ردیف بررسی شد", Jalali.Num(r.Checked))
                : HrT.F("زنجیره در ردیف {0} می‌شکند: {1}", Jalali.Num(r.BrokenSeq ?? 0), r.Reason),
        });
    }
}
