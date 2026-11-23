// ============================================================
// فاز ۱۳۵ (مورد ۹ ROADMAP-ADDITIONS) — موتور گردش‌کار تأیید چندمرحله‌ای
// تصمیم‌گیرهای موجود (DecideLeave/Mission/Permit/Correction) تصمیمشان را از این
// موتور عبور می‌دهند؛ منطق اصلی فقط در مرحلهٔ آخر (یا بدون گردش‌کار) اجرا می‌شود.
// ============================================================
namespace HrModules.Attendance.Domain;

using HrModules.Attendance.Data;
using Microsoft.EntityFrameworkCore;

public enum ApprovalGate
{
    /// <summary>گردش‌کار فعالی برای این نوع نیست — منطق عادی ادامه یابد.</summary>
    NoFlow,
    /// <summary>تصمیم به‌عنوان یک مرحله ثبت شد (تأیید میانی) — منطق اصلی اجرا نشود.</summary>
    StepRecorded,
    /// <summary>تصمیم آخرین مرحله بود — منطق اصلی (اثر نهایی) ادامه یابد.</summary>
    FinalStepProceed,
    /// <summary>مرحلهٔ جاری با مجوز/جانشینی این کاربر قابل تصمیم نیست.</summary>
    NotMyStep
}

public static class ApprovalEngine
{
    public const string TCorrection = "اصلاح تردد";
    public const string TLeave = "مرخصی";
    public const string TMission = "مأموریت";
    public const string TPermit = "مجوز اضافه‌کاری";

    /// <summary>جریان فعال برای نوع درخواست — جریان شرکتی مقدم بر مشترک؛ فقط جریانِ دارای مرحله.</summary>
    public static async Task<ApprovalFlow?> GetActiveFlowAsync(AttendanceDbContext db, string reqType, string? companyId)
    {
        var flows = await db.ApprovalFlows.AsNoTracking().Include(f => f.Steps)
            .Where(f => f.Active && f.ReqType == reqType && (f.CompanyId == null || f.CompanyId == companyId))
            .ToListAsync();
        return flows.OrderByDescending(f => f.CompanyId != null)
            .FirstOrDefault(f => f.Steps.Any());
    }

    /// <summary>آیا این کاربر می‌تواند مرحلهٔ جاری را تصمیم بگیرد؟ (مجوز مستقیم یا جانشینی فعال)</summary>
    public static async Task<bool> CanDecideAsync(AttendanceDbContext db, ApprovalFlowStep step,
        string userName, Func<string, bool> hasPerm)
    {
        if (hasPerm(step.Perm)) return true;
        var today = DateTime.Today;
        return await db.ApprovalDelegations.AsNoTracking().AnyAsync(d =>
            !d.Revoked && d.Perm == step.Perm && d.ToUser == userName
            && d.FromDate.Date <= today && d.ToDate.Date >= today);
    }

    /// <summary>عبور تصمیم از گردش‌کار. برگرداندن NoFlow یا FinalStepProceed یعنی منطق اصلی ادامه یابد.</summary>
    public static async Task<ApprovalGate> RegisterDecisionAsync(AttendanceDbContext db,
        string reqType, string reqId, string title, string? companyId,
        string userName, Func<string, bool> hasPerm, bool approve, string? note = null)
    {
        var flow = await GetActiveFlowAsync(db, reqType, companyId);
        if (flow is null || !flow.Steps.Any()) return ApprovalGate.NoFlow;

        var steps = flow.Steps.OrderBy(s => s.Order).ToList();
        var instance = await db.ApprovalInstances
            .FirstOrDefaultAsync(i => i.ReqType == reqType && i.ReqId == reqId && i.Status == "در گردش");
        if (instance is null)
        {
            instance = new ApprovalInstance
            {
                FlowId = flow.Id, ReqType = reqType, ReqId = reqId, Title = title,
                RequestedBy = userName, CurrentStep = steps[0].Order, Status = "در گردش"
            };
            db.ApprovalInstances.Add(instance);
        }

        var step = steps.FirstOrDefault(s => s.Order == instance.CurrentStep) ?? steps[0];
        if (!await CanDecideAsync(db, step, userName, hasPerm)) return ApprovalGate.NotMyStep;

        db.ApprovalActions.Add(new ApprovalAction
        {
            InstanceId = instance.Id, StepOrder = step.Order, StepTitle = step.Title,
            ByUser = userName, Decision = approve ? "تأیید" : "رد", Note = note
        });

        if (!approve)
        {
            instance.Status = "رد شده";
            instance.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();
            return ApprovalGate.StepRecorded; // ردِ هر مرحله نهایی است — منطق اصلی وضعیت «رد شده» را می‌نویسد
        }

        var idx = steps.FindIndex(s => s.Order == step.Order);
        if (idx >= steps.Count - 1)
        {
            instance.Status = "تأیید نهایی";
            instance.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();
            return ApprovalGate.FinalStepProceed;
        }
        instance.CurrentStep = steps[idx + 1].Order;
        instance.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return ApprovalGate.StepRecorded;
    }
}
