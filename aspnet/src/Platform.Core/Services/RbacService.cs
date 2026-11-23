using System.Collections.Concurrent;
using Platform.Web.Data;
using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

// ============================================================
// فاز ۷۴ — سرویس RBAC نسخه‌دار (تک‌نمونه)
// ----------------------------------------------------------------------
// چرا؟ تا فاز ۷۳ دسترسی‌ها فقط در JWT (ادعای perms) ذخیره می‌شدند و هر
// تغییر نقش/دسترسی به «ورود بعدی» موکول می‌شد. این سرویس منبع حقیقت
// زندهٔ دسترسی‌هاست:
//   • Version — با هر تغییر نقش/دسترسی/دامنه Bump می‌شود؛ کش هر کاربر
//     به نسخهٔ لحظهٔ ساخت وابسته است (تغییر = بی‌اعتبار شدن کش همه).
//   • GetPermsAsync — یک کوئری ایندکسی UserRoles⋈RolePermissions⋈Permissions
//     فقط در اولین لمس یا پس از Bump؛ بقیهٔ درخواست‌ها lookup حافظه‌ای.
//   • Matches — تطبیق کلید دسترسی با پشتیبانی wildcard (a.b.*) و
//     «کلید والد» (کلید ریز ۷۴ مثل payroll.runs.create با داشتن
//     payroll.manage هم پاس می‌شود — سازگاری کامل با نقش‌های قدیمی).
// ادعای perms در JWT صرفاً «بوت‌استرپ» است؛ میان‌افزار RbacRefresh آن را
// با خروجی همین سرویس جایگزین می‌کند (اعمال فوری بدون ورود مجدد).
// ============================================================
public sealed class RbacService(IServiceScopeFactory scopes)
{
    private long _version = 1;
    public long Version => Interlocked.Read(ref _version);

    /// <summary>با هر تغییر در نقش‌ها/دسترسی‌ها/دامنهٔ داده صدا زده شود — کش همهٔ کاربران بی‌اعتبار می‌شود.</summary>
    public void Bump() => Interlocked.Increment(ref _version);

    private sealed record Entry(long Ver, DateTime At, bool Sa, HashSet<string> Perms);
    private readonly ConcurrentDictionary<string, Entry> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    /// <summary>دسترسی‌های مؤثر کاربر — از نقش‌ها؛ superadmin همهٔ کاتالوگ. کش نسخه‌دار.
    /// فاز ۷۵: ورودی کش به وضعیت sa هم گره خورده تا چرخهٔ superadmin→عادی بلافاصله
    /// مجموعهٔ دسترسی را بازسازی کند (بدون انتظار برای Bump/TTL).</summary>
    public async Task<HashSet<string>> GetPermsAsync(string userId, bool isSuperadmin)
    {
        var ver = Version;
        if (_cache.TryGetValue(userId, out var e) && e.Ver == ver && e.Sa == isSuperadmin && DateTime.UtcNow - e.At < Ttl)
            return e.Perms;

        HashSet<string> perms;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrBaseDbContext>();
            if (isSuperadmin)
            {
                perms = [.. await db.Permissions.Select(p => p.Key).AsNoTracking().ToListAsync()];
            }
            else
            {
                var roleIds = await db.UserRoles.AsNoTracking()
                    .Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync();
                if (roleIds.Count == 0) perms = [];
                else
                {
                    // فاز ۷۷ — سلسله‌مراتب نقش‌ها: نقش فرزند، دسترسی‌های نقش والد و همهٔ
                    // اجدادش را به ارث می‌برد؛ پس زنجیرهٔ Parents تا ریشه باز می‌شود
                    // (سقف ۱۲ سطح — حلقهٔ معیوب هم با مجموعهٔ دیده‌شده‌ها قطع می‌شود).
                    var chain = new HashSet<string>(roleIds);
                    var frontier = roleIds;
                    for (var depth = 0; depth < 12 && frontier.Count > 0; depth++)
                    {
                        var parents = await db.Roles.AsNoTracking()
                            .Where(r => frontier.Contains(r.Id) && r.ParentRoleId != null)
                            .Select(r => r.ParentRoleId!).Distinct().ToListAsync();
                        frontier = parents.Where(p => chain.Add(p)).ToList();
                    }
                    perms = [.. await db.RolePermissions.AsNoTracking()
                        .Where(rp => chain.Contains(rp.RoleId))
                        .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Key)
                        .Distinct().ToListAsync()];
                }
            }
        }
        _cache[userId] = new Entry(ver, DateTime.UtcNow, isSuperadmin, perms);
        return perms;
    }

    /// <summary>کاربر حذف/غیرفعال شده — فقط همین کاربر از کش دسترسی خارج می‌شود (sa بی‌کش است).</summary>
    public void Drop(string userId) => _cache.TryRemove(userId, out _);

    // ---------- پرچم superadmin زنده (فاز ۷۵) ----------

    /// <summary>
    /// فاز ۷۵ — وضعیت زندهٔ superadmin، بدون کش: یک SELECT روی کلید اصلی Users در هر درخواست
    /// (هزینهٔ ناچیز در برابر سربار خود درخواست). تا فاز ۷۴ ادعای «sa» در لحظهٔ ورود منجمد می‌شد
    /// و گرفتن دسترسی superadmin به «ورود بعدی» موکول بود؛ حالا میان‌افزار RbacRefresh آن را در
    /// هر درخواست از DB می‌خواند — سلب/اعطای فوری حتی با تغییر مستقیم DB، بدون نیاز به Bump.
    /// </summary>
    public async Task<bool> IsSuperadminAsync(string userId)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrBaseDbContext>();
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.IsSuperadmin)
            .FirstOrDefaultAsync();
    }

    // ---------- تطبیق کلید دسترسی ----------

    /// <summary>
    /// والدین کلیدهای ریز فاز ۷۴ — کلید ریز با داشتن کلید درشتِ والد هم پاس می‌شود
    /// تا نقش‌های موجود (با کلیدهای درشت) بدون هیچ مهاجرتی سر جایشان بمانند.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> PermParents = new Dictionary<string, string[]>
    {
        // حقوق و دستمزد — تفکیک عملیات لیست حقوق / فرمول‌ساز اقلام / پارامترها
        ["payroll.runs.create"] = ["payroll.manage"],
        ["payroll.runs.finalize"] = ["payroll.manage"],
        ["payroll.runs.delete"] = ["payroll.manage"],
        ["payroll.runs.export"] = ["payroll.manage", "reports.export"],
        ["payroll.items.manage"] = ["payroll.manage"],
        ["payroll.params.manage"] = ["payroll.manage"],
        // حضور و غیاب — ثبت/اصلاح دستی تردد
        ["attendance.records.manage"] = ["attendance.manage"],
        // کاربران/نقش‌ها
        ["roles.manage"] = ["users.manage"],
        ["roles.create"] = ["users.manage", "roles.manage"],
        // منابع انسانی — تفکیک ایجاد/ویرایش/حذف پرسنل
        ["employees.create"] = ["employees.manage"],
        ["employees.edit"] = ["employees.manage"],
        ["employees.delete"] = ["employees.manage"],
        // فاز ۷۴ — کلیدهای کاتالوگِ تفکیک‌شدهٔ این فاز: نقش درشتِ والد هم پاس می‌شود
        ["payroll.items.edit"] = ["payroll.manage"],
        ["payroll.params.edit"] = ["payroll.manage"],
        ["payroll.pdf"] = ["payroll.manage"],
        ["employees.import"] = ["employees.manage"],
        ["devices.simulate"] = ["devices.control", "biometrics.manage", "parking.control"],
        // فاز ۷۵ — کلیدهای ریز پارکینگ
        ["parking.lots.manage"] = ["parking.manage"],
        ["parking.vehicles.manage"] = ["parking.manage"],
        ["parking.cameras.manage"] = ["parking.manage"],
        ["parking.barrier"] = ["parking.control"],
        // اسکنِ خودرو قبلاً ذیل parking.manage بود — والد دوم برای صفر-رگرسیون نقش‌های قدیمی
        ["parking.scan"] = ["parking.control", "parking.manage"],
        // فاز ۷۵ — کلیدهای ریز کنترل تردد / کارت / ارباب‌رجوع
        ["access.zones.manage"] = ["access.manage"],
        ["access.gates.manage"] = ["access.manage"],
        ["access.perms.manage"] = ["access.manage"],
        ["access.alerts.manage"] = ["access.manage"],
        ["access.scan"] = ["access.manage"],
        ["cards.issue"] = ["cards.manage"],
        ["cards.state"] = ["cards.manage"],
        ["visitors.register"] = ["visitors.manage"],
        ["visitors.flow"] = ["visitors.manage"],
        // فاز ۷۵ — کلیدهای ریز دستگاه / بایومتریک
        ["devices.register"] = ["devices.manage"],
        ["devices.transfer"] = ["devices.manage"],
        ["devices.commands"] = ["devices.control", "devices.manage"],
        ["biometrics.fetch"] = ["biometrics.manage"],
        ["biometrics.transfer"] = ["biometrics.manage"],
        // فاز ۷۵ — کلیدهای ریز چارت سازمانی
        ["org.depts.manage"] = ["org.manage"],
        ["org.positions.manage"] = ["org.manage"],
        // فاز ۷۵ — کلیدهای ریز کاربر / نقش / شرکت
        ["users.edit"] = ["users.manage"],
        ["users.reset"] = ["users.manage"],
        ["users.delete"] = ["users.manage"],
        // مشاهدهٔ نقش‌ها با کلید درشت هم پاس می‌شود — فرم نقش برای نقش‌های قدیمی users.manage باز بماند
        ["roles.view"] = ["roles.manage", "users.manage"],
        ["roles.edit"] = ["roles.manage", "users.manage"],
        ["roles.delete"] = ["roles.manage", "users.manage"],
        ["companies.create"] = ["companies.manage"],
        ["companies.edit"] = ["companies.manage"],
        ["companies.delete"] = ["companies.manage"],
        // فاز ۷۵ — کلیدهای ریز شیفت/تعطیلی/درخواست‌ها (ادغام)
        ["shifts.create"] = ["shifts.manage"],
        ["shifts.edit"] = ["shifts.manage"],
        ["shifts.delete"] = ["shifts.manage"],
        ["holidays.create"] = ["holidays.manage"],
        ["holidays.delete"] = ["holidays.manage"],
        ["attendance.requests.create"] = ["attendance.manage"],
        ["attendance.leaves.approve"] = ["attendance.approve"],
        ["attendance.missions.approve"] = ["attendance.approve"],
        ["attendance.permits.approve"] = ["attendance.approve"],
        // فاز ۷۵ — گروه‌های تردد و نقشه
        ["access.groups.manage"] = ["access.manage"],
        ["access.map.manage"] = ["access.manage"],
        // فاز ۷۵ — عیدی/تسویه
        ["payroll.bonus.compute"] = ["payroll.bonus"],
        ["payroll.bonus.export"] = ["payroll.bonus", "reports.export"],
        ["payroll.settlement"] = ["payroll.bonus"],
        ["payroll.settings"] = ["payroll.manage"],
        // فاز ۹۳ — به‌روزرسانی بسته‌ای: با settings.manage هم پاس می‌شود (سازگاری نقش‌های قدیمی)
        ["updates.manage"] = ["settings.manage"],
    };

    /// <summary>
    /// تطبیق کلید خواسته‌شده با مجموعهٔ دسترسی‌ها:
    /// دقیق ← «*» ← wildcard پوشه‌ای (a.b.* برای a.b.c) ← کلید والد.
    /// </summary>
    public static bool Matches(ICollection<string> perms, string key)
    {
        if (perms.Count == 0) return false;
        if (perms.Contains(key)) return true;
        if (perms.Contains("*")) return true;

        // wildcard پوشه‌ای: دسترسی «payroll.*» هر کلید زیرمجموعه‌اش را پوشش می‌دهد
        var parts = key.Split('.');
        for (var i = parts.Length - 1; i >= 1; i--)
            if (perms.Contains(string.Join('.', parts.Take(i)) + ".*"))
                return true;

        // کلید والد (سازگاری نقش‌های قدیمی با کلیدهای ریز فاز ۷۴)
        if (PermParents.TryGetValue(key, out var parents))
            foreach (var p in parents)
                if (perms.Contains(p)) return true;
        return false;
    }
}
