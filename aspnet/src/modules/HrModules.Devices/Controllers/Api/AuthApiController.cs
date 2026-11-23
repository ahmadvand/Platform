using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Devices.Api;

public record ApiLoginRequest(string? username, string? password);

[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HrBaseDbContext Base;
    private readonly JwtService _jwt;

    public AuthApiController(AppDbContext db, HrBaseDbContext baseDb, JwtService jwt) { _db = db; Base = baseDb; _jwt = jwt; }

    /// <summary>
    /// ورود — قرارداد سرویس پل C#:
    /// JSON {username,password} → 200 + کوکی hr_at (توکن دسترسی) + hr_rt (بازخوانی).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] ApiLoginRequest req)
    {
        var ip = AuthCore.IpOf(HttpContext) ?? "";
        var ua = Request.Headers.UserAgent.ToString();
        var r = await AuthCore.LoginAsync(Base, _jwt, req.username, req.password, ip, ua);
        if (r.Error is not null)
            return StatusCode(r.ErrorStatus ?? 401, new { error = r.Error });

        Response.Cookies.Append(JwtService.AccessCookie, r.AccessToken!,
            JwtService.AuthCookie((r.AccessExpires - DateTime.UtcNow).TotalSeconds));
        Response.Cookies.Append(JwtService.RefreshCookie, r.RefreshToken!,
            JwtService.AuthCookie(TimeSpan.FromDays(7).TotalSeconds));

        return Ok(new
        {
            user = new
            {
                r.User!.Id,
                r.User.Username,
                r.User.FullName,
                r.User.IsSuperadmin,
                r.User.MustChangePassword,
                passwordExpired = r.PasswordExpired,
                permissions = r.Perms,
            },
        });
    }

    /// <summary>بازخوانی توکن — چرخش توکن بازخوانی مبتنی بر DB.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        var raw = Request.Cookies[JwtService.RefreshCookie];
        if (string.IsNullOrEmpty(raw))
            return Unauthorized(new { error = "توکن بازخوانی یافت نشد" });
        var hash = JwtService.Sha256Hex(raw);
        var rt = await _db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (rt is null || rt.RevokedAt is not null || rt.ExpiresAt < DateTime.Now || rt.User is null)
            return Unauthorized(new { error = "توکن بازخوانی نامعتبر است" });

        // چرخش: توکن قبلی باطل، توکن جدید صادر
        rt.RevokedAt = DateTime.Now;
        var newRt = JwtService.RandomToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = rt.UserId,
            TokenHash = JwtService.Sha256Hex(newRt),
            ExpiresAt = DateTime.Now.AddDays(7),
            DeviceInfo = rt.DeviceInfo,
            Ip = rt.Ip,
        });
        var perms = await AuthCore.GetPermissionsAsync(Base, rt.User);
        var (token, expires) = _jwt.Sign(rt.User, perms);
        await _db.SaveChangesAsync();

        Response.Cookies.Append(JwtService.AccessCookie, token,
            JwtService.AuthCookie(TimeSpan.FromMinutes(60).TotalSeconds));
        Response.Cookies.Append(JwtService.RefreshCookie, newRt,
            JwtService.AuthCookie(TimeSpan.FromDays(7).TotalSeconds));
        return Ok(new { ok = true, expiresAt = expires });
    }

    /// <summary>خروج — ابطال توکن بازخوانی و پاک‌سازی کوکی‌ها.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        var raw = Request.Cookies[JwtService.RefreshCookie];
        if (!string.IsNullOrEmpty(raw))
        {
            var hash = JwtService.Sha256Hex(raw);
            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (rt is not null) { rt.RevokedAt = DateTime.Now; await _db.SaveChangesAsync(); }
        }
        Response.Cookies.Delete(JwtService.AccessCookie, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(JwtService.RefreshCookie, new CookieOptions { Path = "/" });
        return Ok(new { ok = true });
    }

    /// <summary>اطلاعات نشست جاری (برای فرانت‌اند).</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var s = HttpContext.RequestServices.GetRequiredService<ICurrentUser>().Get();
        if (s is null) return Unauthorized();
        return Ok(new
        {
            user = new
            {
                s.Id, s.Username, s.FullName, s.IsSuperadmin,
                permissions = s.Permissions.ToArray(),
            },
        });
    }
}
