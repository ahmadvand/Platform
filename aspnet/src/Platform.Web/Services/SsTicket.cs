using System.Security.Cryptography;
using System.Text;

namespace Platform.Web.Services;

/// <summary>
/// فاز ۱۰۶ — تیکت کوتاه‌عمر برای سرو Runtime سندباکس محافظ صفحه.
/// ----------------------------------------------------------------------
/// مشکل: iframe محافظ با sandbox="allow-scripts" (بدون allow-same-origin) مبدأ
/// opaque دارد و مرورگر کوکی SameSite=Lax (hr_at) را در آن درخواست نمی‌فرستد —
/// درخواست Runtime ناشناس می‌رسد و گارد [HasPerm] کلاس SettingsController آن را
/// به صفحهٔ ورود ریدایرکت می‌کرد (iframe به‌جای صحنه، صفحهٔ ورود نشان می‌داد).
///
/// راه‌حل: صفحهٔ والد (که نشست معتبر دارد) یک تیکت امضاشدهٔ HMAC در اتریبیوت
/// data-ss-ticket رندر می‌کند؛ hr-screensaver.js آن را به src اضافه می‌کند و
/// ScreensaverRuntime تیکت را تأیید می‌کند. تیکت:
///   • فقط با کلید JWT سرور قابل جعل نیست (HMAC-SHA256).
///   • پنجرهٔ ۴ ساعته (±۱ پنجره) — بیشینهٔ بی‌حرکتی مجاز ۱۲۰ دقیقه است؛ صفحهٔ
///     دیرفعال هم کار می‌کند و پس از آن به‌هرحال نشست JWT خودش می‌سورد.
///   • محتوا فقط uid کاربر است — هیچ داده‌ای لو نمی‌دهد؛ کد بسته هم به شبکه
///     دسترسی ندارد (connect-src 'none') تا تیکت جایی فرستاده نشود.
/// </summary>
public static class SsTicket
{
    private const long WindowSeconds = 3600;          // پنجره‌های ۱ ساعته
    private const int AllowedBackWindows = 4;         // ۴ ساعت اعتبار
    private const string Purpose = "ssrt";

    private static byte[] Key(string secret) => Encoding.UTF8.GetBytes(secret ?? "");

    private static string Sig(string uid, long exp, string secret)
    {
        return Convert.ToHexString(HMACSHA256.HashData(Key(secret), Encoding.UTF8.GetBytes($"{Purpose}|{uid}|{exp}")))
                      .ToLowerInvariant();
    }

    /// <summary>ساخت تیکت برای رندر در _Layout — uid + پنجرهٔ جاری + امضا.</summary>
    public static string New(string? uid, string secret)
    {
        var exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / WindowSeconds;
        return $"{uid ?? "0"}.{exp}.{Sig(uid ?? "0", exp, secret)}";
    }

    /// <summary>تأیید تیکت — پنجرهٔ جاری یا تا ۴ پنجرهٔ قبل؛ مقایسهٔ زمان‌ثابت امضا.</summary>
    public static bool Valid(string? ticket, string secret)
    {
        if (string.IsNullOrWhiteSpace(ticket) || string.IsNullOrWhiteSpace(secret)) return false;
        var parts = ticket.Split('.');
        if (parts.Length != 3 || !long.TryParse(parts[1], out var exp)) return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / WindowSeconds;
        if (exp > now + 1 || exp < now - AllowedBackWindows) return false;
        var expected = Sig(parts[0], exp, secret);
        var given = parts[2];
        if (given.Length != expected.Length) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(expected));
    }
}
