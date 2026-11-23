using System.Collections.Concurrent;
using Platform.Web.Data;
using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

// ============================================================
// فاز ۷۴ — دامنهٔ دسترسی به رکورد (Row-Level Security)
// ----------------------------------------------------------------------
// سؤال طراحی: «دسترسی در سطح رکورد چطوری با سرعت و گزارش‌ها سازگار می‌شود؟»
// پاسخ این پیاده‌سازی (سه اصل):
//   ۱) حل «یک‌بار» نه «هر رکورد»: دامنهٔ نقش‌ها (all/company/dept/self) در
//      ورود منطق به «مجموعهٔ شناسه‌ها» ترجمه می‌شود (شرکت‌ها، واحد های با
//      زیرشاخه‌ها، شناسهٔ پرسنل خود) و per-user کش نسخه‌دار می‌شود؛
//      نسخه با RbacService.Version گره خورده (تغییر نقش/دامنه = بی‌اعتبار).
//   ۲) فیلتر «SQL-ساید» نه «کلاینت-ساید»: مجموعه‌ها به‌صورت predicate
//      ترجمه‌پذیر به EF (IN / OR روی ستون‌های ایندکس‌دار CompanyId/
//      DepartmentId/EmployeeId) به کوئری اضافه می‌شوند — گزارش‌ها همان
//      یک کوئری قبلی می‌مانند و فقط WHEREشان کمی تنگ‌تر می‌شود؛
//      گروه‌بندی/تجمیع قبل از انتقال داده انجام می‌شود (سرعت گزارش‌ها).
//   ۳) لیست‌های بزرگ تکه‌تکه: برای SQL Server (سقف ۲۱۰۰ پارامتر) شناسهٔ
//      پرسنل در تکه‌های ۴۰۰تایی با OR به هم می‌چسبند.
// دامنه‌ها بین نقش‌های یک کاربر «اجتماع» می‌شوند (نقش فقط دست می‌دهد،
// نمی‌گیرد) و با User.CompanyId کاربر «اشتراک» (چندشرکتی).
// نقش بدون سطر دامنه = همهٔ داده‌ها (سازگاری با نصب‌های فعلی).
// نوع‌های UserScope/ScopeQuery/IUserScopeProvider ∈ Platform.Core (UserScope.cs)
// تا ماژول‌های پلاگین هم بدون ارجاع به هاست مصرفشان کنند.
// ============================================================
public sealed class DataScopeService(IServiceScopeFactory scopes, RbacService rbac) : IUserScopeProvider
{
    private sealed record Entry(long Ver, DateTime At, UserScope Scope);
    private readonly ConcurrentDictionary<string, Entry> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    /// <summary>دامنهٔ مؤثر کاربر — کش نسخه‌دار (تغییر نقش/دامنه با Bump نسخهٔ RBAC بی‌اعتبار می‌شود).</summary>
    public async Task<UserScope> ForUserAsync(string userId, bool isSuperadmin)
    {
        if (isSuperadmin) return UserScope.Unrestricted;
        var ver = rbac.Version;
        if (_cache.TryGetValue(userId, out var e) && e.Ver == ver && DateTime.UtcNow - e.At < Ttl)
            return e.Scope;

        var scope = await ResolveAsync(userId);
        _cache[userId] = new Entry(ver, DateTime.UtcNow, scope);
        return scope;
    }

    private async Task<UserScope> ResolveAsync(string userId)
    {
        using var s = scopes.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return new UserScope(false, [], [], [], "", "کاربر ناشناخته");

        var roleIds = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync();
        if (roleIds.Count == 0) return UserScope.Unrestricted;

        var rows = await db.RoleScopes.AsNoTracking()
            .Where(rs => roleIds.Contains(rs.RoleId)).ToListAsync();
        if (rows.Count == 0) return UserScope.Unrestricted;   // سازگاری: نقش بدون دامنه = همهٔ داده‌ها
        if (rows.Any(r => r.Kind == "all")) return UserScope.Unrestricted;

        var companies = rows.Where(r => r.Kind == "company" && r.RefId != null)
            .Select(r => r.RefId!).ToHashSet();
        var depts = rows.Where(r => r.Kind == "dept" && r.RefId != null)
            .Select(r => r.RefId!).ToHashSet();
        var hasSelf = rows.Any(r => r.Kind == "self");

        // فاز ۷۴ — سقف واحد سازمانی صریح کاربر (Users/SetRoles): دامنهٔ واحد نقش‌ها فقط داخل آن
        var userDeptCap = user.DepartmentId;

        // چندشرکتی — شرکتِ کاربر (اگر تعریف شده) سقفِ همیشگی دامنه است
        if (!string.IsNullOrEmpty(user.CompanyId))
        {
            companies.RemoveWhere(c => c != user.CompanyId);
            if (companies.Count == 0 && depts.Count == 0 && !hasSelf)
                return new UserScope(false, [], [], [], "", "بدون دامنهٔ معتبر");
        }

        // زیرشاخه‌های واحد سازمانی — فقط با حضور خانوادهٔ org (ماژول org)
        var orgPresent = db.Model.FindEntityType(typeof(Department)) is not null
                      && db.Model.FindEntityType(typeof(Employee)) is not null;
        if (orgPresent && depts.Count > 0)
        {
            var allDepts = await db.Set<Department>().AsNoTracking()
                .Select(d => new { d.Id, d.ParentId }).ToListAsync();
            var byParent = allDepts.Where(d => d.ParentId != null)
                .GroupBy(d => d.ParentId!).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
            // BFS زیر درخت هر واحد انتخابی
            var queue = new Queue<string>(depts);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (!byParent.TryGetValue(cur, out var kids)) continue;
                foreach (var k in kids)
                    if (depts.Add(k)) queue.Enqueue(k);
            }
        }

        // فاز ۷۴ — اعمال سقف واحد کاربر: واحدهای نقش فقط تا واحد صریحِ کاربر (خارج آن حذف)
        if (!string.IsNullOrEmpty(userDeptCap))
        {
            depts.IntersectWith([userDeptCap]);
            if (depts.Count == 0 && companies.Count == 0 && !hasSelf)
                return new UserScope(false, [], [], [], "", "بدون دامنهٔ معتبر");
        }

        // شناسه‌های پرسنل مجاز — اجتماع شرکت‌ها ∪ واحدها ∪ خودِ کاربر
        HashSet<string>? empIds = null;
        var selfEmpId = hasSelf ? user.EmployeeId ?? "" : "";
        if (orgPresent)
        {
            empIds = [];
            if (hasSelf && !string.IsNullOrEmpty(user.EmployeeId))
                empIds.Add(user.EmployeeId);
            if (companies.Count > 0 || depts.Count > 0)
            {
                var empq = db.Set<Employee>().AsNoTracking().Select(e => new { e.Id, e.CompanyId, e.DepartmentId });
                if (companies.Count > 0 && depts.Count > 0)
                    empq = empq.Where(e => companies.Contains(e.CompanyId) ||
                                           (e.DepartmentId != null && depts.Contains(e.DepartmentId)));
                else if (companies.Count > 0)
                    empq = empq.Where(e => companies.Contains(e.CompanyId));
                else
                    empq = empq.Where(e => e.DepartmentId != null && depts.Contains(e.DepartmentId));
                var ids = await empq.Select(e => e.Id).ToListAsync();
                empIds.UnionWith(ids);
            }
        }

        var parts = new List<string>();
        if (companies.Count > 0) parts.Add($"{companies.Count} شرکت");
        if (depts.Count > 0) parts.Add($"{depts.Count} واحد سازمانی (با زیرشاخه)");
        if (hasSelf) parts.Add("فقط خودم");
        var summary = parts.Count == 0 ? "بدون دامنهٔ معتبر" : string.Join(" + ", parts);

        return new UserScope(false, companies, depts, empIds, selfEmpId, summary);
    }
}
