using System.Collections.Concurrent;
using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Services;
using Platform.Core.Services;   // فاز ۱۱۷ — AuditChain
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

// ============================================================
// هسته احراز هویت — پورت lib/auth.ts + مسیرهای auth API
// قفل حساب، محدودسازی نرخ ورود، توکن چرخشی DB، حسابرسی
// ============================================================
public static class AuthPolicy
{
    public const int MaxLoginAttempts = 5;
    public const int LockMinutes = 15;
    public const int RateLimitMax = 10;      // تلاش در پنجره
    public static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(5);
}

public class LoginOutcome
{
    public User? User { get; set; }
    public string? AccessToken { get; set; }
    public DateTime AccessExpires { get; set; }
    public string? RefreshToken { get; set; }
    public string? Error { get; set; }
    public int? ErrorStatus { get; set; }
    public bool PasswordExpired { get; set; }
    public List<string> Perms { get; set; } = new();
}

public static class LoginRateLimiter
{
    private static readonly ConcurrentDictionary<string, List<DateTime>> Hits = new();

    public static bool Allow(string key)
    {
        var now = DateTime.UtcNow;
        var list = Hits.GetOrAdd(key, _ => new List<DateTime>());
        lock (list)
        {
            list.RemoveAll(t => now - t > AuthPolicy.RateWindow);
            if (list.Count >= AuthPolicy.RateLimitMax) return false;
            list.Add(now);
            return true;
        }
    }
}

public static class AuthCore
{
    /// <summary>دسترسی‌های مؤثر کاربر از نقش‌ها (superadmin → همه).</summary>
    public static async Task<List<string>> GetPermissionsAsync(HrBaseDbContext db, User user)
    {
        if (user.IsSuperadmin)
            return await db.Permissions.Select(p => p.Key).ToListAsync();
        var roles = await db.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToListAsync();
        if (roles.Count == 0) return new List<string>();
        return await db.RolePermissions
            .Where(rp => roles.Contains(rp.RoleId))
            .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Key)
            .Distinct()
            .ToListAsync();
    }

    public static async Task<HashSet<string>> GetPermissionSetAsync(HrBaseDbContext db, User user) =>
        new(await GetPermissionsAsync(db, user));

    /// <summary>منطق ورود مشترک (API + وب): اعتبارسنجی، قفل، محدودسازی نرخ، حسابرسی.</summary>
    public static async Task<LoginOutcome> LoginAsync(
        HrBaseDbContext db, JwtService jwt, string? username, string? password, string ip, string userAgent)
    {
        username = (username ?? "").Trim();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return Fail(HrT.T("نام کاربری و رمز عبور الزامی است"), 400);

        if (!LoginRateLimiter.Allow($"{ip}:{username}"))
            return Fail(HrT.T("تلاش‌های ورود بیش از حد مجاز — چند دقیقه بعد دوباره امتحان کنید"), 429);

        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null || !PasswordService.Verify(password, user.PasswordHash))
        {
            if (user is not null)
            {
                var attempts = user.FailedLoginAttempts + 1;
                var lockAccount = attempts >= AuthPolicy.MaxLoginAttempts;
                user.FailedLoginAttempts = lockAccount ? 0 : attempts;
                if (lockAccount) user.LockedUntil = DateTime.Now.AddMinutes(AuthPolicy.LockMinutes);
                await db.SaveChangesAsync();
                await AuditAsync(db, null, lockAccount ? "account_locked" : "login_failed",
                    "user", username, JsonSerializer.Serialize(new { attempts }), ip);
                if (lockAccount)
                    return Fail(HrT.F("حساب به دلیل تلاش‌های ناموفق قفل شد — {0} دقیقه بعد تلاش کنید", HrT.Num(AuthPolicy.LockMinutes)), 423);
            }
            else
            {
                await AuditAsync(db, null, "login_failed", "user", username, null, ip);
            }
            return Fail(HrT.T("نام کاربری یا رمز عبور اشتباه است"), 401);
        }

        if (!user.IsActive) return Fail(HrT.T("حساب کاربری غیرفعال است"), 403);
        if (user.LockedUntil is not null && user.LockedUntil > DateTime.Now)
            return Fail(HrT.T("حساب موقتاً قفل است — بعداً تلاش کنید"), 423);

        var perms = await GetPermissionsAsync(db, user);
        var (accessToken, accessExpires) = jwt.Sign(user, perms);

        // توکن بازخوانی چرخشی مبتنی بر DB
        var rt = JwtService.RandomToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = JwtService.Sha256Hex(rt),
            ExpiresAt = DateTime.Now.AddDays(7),
            DeviceInfo = string.IsNullOrEmpty(userAgent) ? null : userAgent[..Math.Min(200, userAgent.Length)],
            Ip = ip,
        });
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.Now;
        await db.SaveChangesAsync();

        await AuditAsync(db, new SessionUser
        {
            Id = user.Id, Username = user.Username, FullName = user.FullName, IsSuperadmin = user.IsSuperadmin,
        }, "login", "user", user.Username,
            JsonSerializer.Serialize(new { roles = await db.UserRoles.Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role!.Name).ToListAsync() }), ip);

        return new LoginOutcome
        {
            User = user,
            AccessToken = accessToken,
            AccessExpires = accessExpires,
            RefreshToken = rt,
            Perms = perms,
            PasswordExpired = user.PasswordExpiresAt is not null && user.PasswordExpiresAt < DateTime.Now,
        };
    }

    private static LoginOutcome Fail(string msg, int status) =>
        new() { Error = msg, ErrorStatus = status };

    // ---------- حسابرسی ----------
    public static async Task AuditAsync(HrBaseDbContext db, SessionUser? actor,
        string action, string entity, string? entityId, string? detail, string? ip = null)
    {
        var log = new AuditLog
        {
            ActorUserId = actor?.Id,
            ActorName = actor?.FullName ?? "سیستم",
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Detail = detail,
            Ip = ip,
            At = DateTime.Now,
        };
        // فاز ۱۱۷ — الحاق به زنجیرهٔ ضد دستکاری (Seq/PrevHash/Hash) پیش از ذخیره
        await AuditChain.AppendAsync(db.AuditLogs, log);
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    public static string? IpOf(HttpContext http) =>
        http.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? http.Connection.RemoteIpAddress?.ToString();
}
