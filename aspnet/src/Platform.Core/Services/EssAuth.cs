using System.Globalization;
using Microsoft.AspNetCore.DataProtection;

namespace Platform.Web.Services;

/// <summary>
/// پرتال خودخدمتی پرسنل (فاز ۱۴) — نشست سبک مبتنی بر کوکی امضاشده DataProtection.
/// ورود با «کد پرسنلی + کد ملی» پرسنل فعال؛ کوکی فقط شناسه پرسنل و زمان صدور را
/// به‌صورت رمزگذاری‌شده/امضاشده نگه می‌دارد (بدون افشای اطلاعات).
/// </summary>
public class EssAuth
{
    public const string CookieName = "hr_ess";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(8);

    private readonly IDataProtector _protector;
    private readonly ILogger<EssAuth> _log;

    public EssAuth(IDataProtectionProvider provider, ILogger<EssAuth> log)
    {
        _protector = provider.CreateProtector("HrAttendance.EssAuth.v1");
        _log = log;
    }

    /// <summary>صدور کوکی نشست خودخدمتی برای یک پرسنل.</summary>
    public void SignIn(HttpResponse res, string employeeId)
    {
        var payload = $"{employeeId}|{DateTime.UtcNow:O}";
        var token = _protector.Protect(payload);
        res.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.None,          // سازگار با iframe پیش‌نمایش
            Secure = false,                        // Secure در میان‌افزار پروداکشن اضافه می‌شود
            IsEssential = true,
            MaxAge = Ttl,
        });
    }

    /// <summary>اعتبارسنجی کوکی و بازگرداندن شناسه پرسنل؛ نامعتبر/منقضی → null.</summary>
    public string? Validate(HttpRequest req)
    {
        var token = req.Cookies[CookieName];
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            var payload = _protector.Unprotect(token);
            var parts = payload.Split('|');
            if (parts.Length != 2) return null;
            if (!DateTimeOffset.TryParse(parts[1], null, DateTimeStyles.RoundtripKind, out var issued))
                return null;
            if (DateTimeOffset.UtcNow - issued > Ttl) return null;
            return parts[0];
        }
        catch (Exception ex)
        {
            _log.LogWarning("کوکی خودخدمتی نامعتبر: {Msg}", ex.Message);
            return null;
        }
    }

    public void SignOut(HttpResponse res) =>
        res.Cookies.Delete(CookieName, new CookieOptions { SameSite = SameSiteMode.None, Secure = false });

    /// <summary>نرمال‌سازی ارقام فارسی/عربی به لاتین + حذف فاصله‌ها (ورودی کد پرسنلی/کد ملی).</summary>
    public static string NormDigits(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.Trim())
        {
            sb.Append(ch switch
            {
                '۰' => '0', '۱' => '1', '۲' => '2', '۳' => '3', '۴' => '4',
                '۵' => '5', '۶' => '6', '۷' => '7', '۸' => '8', '۹' => '9',
                '٠' => '0', '١' => '1', '٢' => '2', '٣' => '3', '٤' => '4',
                '٥' => '5', '٦' => '6', '٧' => '7', '٨' => '8', '٩' => '9',
                _ => ch,
            });
        }
        return sb.ToString();
    }

    // ---------- قفل ساده تلاش ناموفق (در حافظه — کافی برای پرتال سبک) ----------
    private static readonly Dictionary<string, (int Count, DateTime Until)> Locks = new();
    private static readonly object LockObj = new();

    public static bool IsLocked(string key) =>
        Locks.TryGetValue(key, out var v) && v.Until > DateTime.Now;

    public static int RemainingAttempts(string key) =>
        Locks.TryGetValue(key, out var v) ? Math.Max(0, 5 - v.Count) : 5;

    public static void RegisterFailure(string key)
    {
        lock (LockObj)
        {
            Locks.TryGetValue(key, out var v);
            var count = v.Count + 1;
            var until = count >= 5 ? DateTime.Now.AddMinutes(10) : v.Until;
            Locks[key] = (count, until);
        }
    }

    public static void ClearFailures(string key)
    {
        lock (LockObj) { Locks.Remove(key); }
    }
}
