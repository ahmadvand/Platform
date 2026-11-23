using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Attendance.Data;
using HrModules.Attendance.Domain;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۱۳۵ (مورد ۹ ROADMAP-ADDITIONS) — مدیریت گردش‌کار تأیید چندمرحله‌ای + جانشین‌پذیری.
/// تعریف جریان/مرحله‌ها، مشاهدهٔ پیشرفت رونوشت‌ها و سپردن موقتِ تأیید (جانشینی).
/// تصمیمِ مراحل از طریق دکمه‌های تأیید/رد همان درخواست‌ها انجام می‌شود (بدون منطق موازی).
/// </summary>
public class ApprovalsController : HrModuleControllerBase
{
    private readonly AttendanceDbContext Db;

    public ApprovalsController(AttendanceDbContext db, ICurrentUser me, IAuditSink audits) : base(me, audits)
        => Db = db;

    public static readonly (string Key, string Perm)[] StepPerms =
    {
        ("سرشیفت / مدیر مستقیم", "attendance.approve"),
        ("تأیید مرخصی", "attendance.leaves.approve"),
        ("تأیید مأموریت", "attendance.missions.approve"),
        ("تأیید مجوز اضافه‌کاری", "attendance.permits.approve"),
        ("اصلاح تردد", "corrections.approve"),
    };

    [HttpGet]
    [HasPerm("attendance.approve")]
    public async Task<IActionResult> Index()
    {
        ViewBag.StepPerms = StepPerms;
        ViewBag.Flows = await Db.ApprovalFlows.AsNoTracking().Include(f => f.Steps)
            .OrderByDescending(f => f.Active).ThenBy(f => f.Name).ToListAsync();
        ViewBag.Instances = await Db.ApprovalInstances.AsNoTracking().Include(i => i.Flow).ThenInclude(f => f!.Steps)
            .Include(i => i.Actions).Where(i => i.Status == "در گردش")
            .OrderByDescending(i => i.CreatedAt).Take(60).ToListAsync();
        ViewBag.Delegations = await Db.ApprovalDelegations.AsNoTracking()
            .OrderBy(d => d.Revoked).ThenByDescending(d => d.CreatedAt).Take(60).ToListAsync();
        return View();
    }

    [HttpPost]
    [HasPerm("attendance.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFlow(string name, string reqType)
    {
        if (string.IsNullOrWhiteSpace(name)) return Done("نام گردش‌کار الزامی است", false);
        var steps = new List<ApprovalFlowStep>();
        var titles = Request.Form["stepTitle"].ToArray();
        var perms = Request.Form["stepPerm"].ToArray();
        for (var i = 0; i < Math.Min(titles.Length, perms.Length); i++)
        {
            if (string.IsNullOrWhiteSpace(perms[i])) continue;
            steps.Add(new ApprovalFlowStep
            {
                Order = steps.Count + 1,
                Perm = perms[i].Trim(),
                Title = string.IsNullOrWhiteSpace(titles[i]) ? perms[i] : titles[i].Trim(),
            });
        }
        if (steps.Count == 0) return Done("حداقل یک مرحله با مجوز لازم است", false);
        var flow = new ApprovalFlow { Name = name.Trim(), ReqType = reqType, Steps = steps };
        Db.ApprovalFlows.Add(flow);
        await Db.SaveChangesAsync();
        await Audit("create", "approval-flow", flow.Id, new { flow.Name, flow.ReqType, steps = steps.Count });
        return Done(HrT.F("گردش‌کار «{0}» با {1} مرحله ثبت شد", name.Trim(), steps.Count));
    }

    [HttpPost]
    [HasPerm("attendance.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFlow(string id)
    {
        var f = await Db.ApprovalFlows.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return NotFound();
        f.Active = !f.Active;
        await Db.SaveChangesAsync();
        await Audit("update", "approval-flow", f.Id, new { active = f.Active });
        return Done(f.Active ? HrT.F("گردش‌کار «{0}» فعال شد", f.Name) : HrT.F("گردش‌کار «{0}» غیرفعال شد", f.Name));
    }

    [HttpPost]
    [HasPerm("attendance.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFlow(string id)
    {
        var f = await Db.ApprovalFlows.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return NotFound();
        var running = await Db.ApprovalInstances.AnyAsync(i => i.FlowId == id && i.Status == "در گردش");
        if (running) return Done("گردش‌کارِ در حال اجرا حذف نمی‌شود — اول رونوشت‌ها را ببندید", false);
        Db.ApprovalFlows.Remove(f);
        await Db.SaveChangesAsync();
        await Audit("delete", "approval-flow", id, new { name = f.Name });
        return Done(HrT.F("گردش‌کار «{0}» حذف شد", f.Name));
    }

    [HttpPost]
    [HasPerm("attendance.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDelegation(string toUser, string perm, DateTime fromDate, DateTime toDate)
    {
        if (string.IsNullOrWhiteSpace(toUser)) return Done("نام جانشین الزامی است", false);
        if (toDate < fromDate) return Done("بازهٔ جانشینی نامعتبر است", false);
        var d = new ApprovalDelegation
        {
            FromUser = Sess!.FullName,
            ToUser = toUser.Trim(),
            Perm = perm,
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
        };
        Db.ApprovalDelegations.Add(d);
        await Db.SaveChangesAsync();
        await Audit("create", "approval-delegation", d.Id, new { d.FromUser, d.ToUser, d.Perm });
        return Done(HrT.F("تأییدِ «{0}» تا {1} به {2} سپرده شد", perm, d.ToDate.ToString("yyyy-MM-dd"), d.ToUser));
    }

    [HttpPost]
    [HasPerm("attendance.approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeDelegation(string id)
    {
        var d = await Db.ApprovalDelegations.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        d.Revoked = true;
        await Db.SaveChangesAsync();
        await Audit("update", "approval-delegation", d.Id, new { revoked = true });
        return Done(HrT.T("جانشینی لغو شد"));
    }

    private IActionResult Done(string msg, bool ok = true)
    {
        if (ok) ToastOk(msg); else ToastErr(msg);
        if (Ajax) return AjaxResult(ok, msg);
        return Redirect("/Approvals");
    }
}
