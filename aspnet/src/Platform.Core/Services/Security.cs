using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Platform.Web.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Platform.Web.Services;

// ============================================================
// رمزنگاری رمز عبور — PBKDF2-SHA256 (بدون وابستگی خارجی)
// قالب: pbkdf2-sha256$iterations$saltB64$hashB64
// ============================================================
public static class PasswordService
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        try
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256") return false;
            var iter = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }
}

// ============================================================
// JWT HS256 — هم‌ارز lib/jwt.ts
// ============================================================
public class JwtOptions
{
    public const string Section = "Jwt";
    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "hr-attendance";
    public string Audience { get; set; } = "hr-attendance-clients";
    public int AccessTtlMinutes { get; set; } = 60;
    public int RefreshTtlDays { get; set; } = 7;
}

public class JwtService
{
    private readonly JwtOptions _opt;
    public const string AccessCookie = "hr_at";
    public const string RefreshCookie = "hr_rt";

    public JwtService(IOptions<JwtOptions> opt) => _opt = opt.Value;

    /// <summary>
    /// گزینه‌های کوکی احراز هویت سازگار با iframe پنل پیش‌نمایش پلتفرم:
    /// SameSite=None + Secure + Partitioned (استاندارد CHIPS) تا کوکی در محیط
    /// بین‌دامنه‌ای (z.ai → space-z.ai) نیز ارسال شود. در پروداکشن HTTPS همان استاندارد است.
    /// </summary>
    public static CookieOptions AuthCookie(double? maxAgeSeconds = null) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.None,
        Secure = true,
        Path = "/",
        MaxAge = maxAgeSeconds is null ? null : TimeSpan.FromSeconds(maxAgeSeconds.Value),
    };
    // توجه: ویژگی Partitioned (CHIPS) در .NET 8 در CookieOptions نیست —
    // به‌صورت دستی در میان‌افزار Program.cs به هدر Set-Cookie اضافه می‌شود.

    public SigningCredentials Signing =>
        new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret)), SecurityAlgorithms.HmacSha256);

    /// <summary>صدور توکن دسترسی با ادعاهای کاربر و دسترسی‌ها.</summary>
    public (string Token, DateTime Expires) Sign(User user, IEnumerable<string> perms)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_opt.AccessTtlMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("username", user.Username),
            new("name", user.FullName),
            new("sa", user.IsSuperadmin ? "1" : "0"),
        };
        var permKeys = perms as string[] ?? perms.ToArray();
        if (permKeys.Length > 0)
            claims.Add(new Claim("perms", string.Join(" ", permKeys)));

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: Signing);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public ClaimsPrincipal? Validate(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var p = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _opt.Issuer,
                ValidateAudience = true,
                ValidAudience = _opt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            }, out _);
            return p;
        }
        catch { return null; }
    }

    public static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string Sha256Hex(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}

// ============================================================
// نشست کاربر جاری — از ClaimsPrincipal
// ============================================================
public class SessionUser
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsSuperadmin { get; set; }
    public HashSet<string> Permissions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>فاز ۷۴ — تطبیق با wildcard و کلید والد (RbacService.Matches)؛ superadmin = پاس.</summary>
    public bool Has(string perm) => IsSuperadmin || RbacService.Matches(Permissions, perm);
}

public interface ICurrentUser
{
    SessionUser? Get();
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public SessionUser? Get()
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return null;
        var idStr = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                 ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(idStr)) return null;
        var s = new SessionUser
        {
            Id = idStr,
            Username = user.FindFirst("username")?.Value ?? "",
            FullName = user.FindFirst("name")?.Value ?? "",
            IsSuperadmin = user.FindFirst("sa")?.Value == "1",
        };
        var perms = user.FindFirst("perms")?.Value ?? "";
        foreach (var p in perms.Split(' ', StringSplitOptions.RemoveEmptyEntries)) s.Permissions.Add(p);
        return s;
    }
}
