using System.Collections.Concurrent;

namespace Platform.Web.Services;

/// <summary>
/// فاز ۱۰۲ — قفل سرورسمت محافظ صفحه (رفع دورزدن رمز با رفرش — درخواست کاربر)
/// ----------------------------------------------------------------------
/// اشکال گزارش‌شدهٔ کاربر: «وقتی باید کاربر رمز وارد کنه، کافیه صفحه رو رفرش کنه
/// و رمز دور زده میشه!» — چون محافظ فقط سمت مرورگر بود و F5 شمارندهٔ بی‌حرکتی
/// را صفر می‌کرد.
///
/// مکانیسم:
///   ۱) هنگام فعال‌شدن محافظ (فقط در حالت «بیدارسازی با رمز») کلاینت به
///      POST /Auth/ScreensaverLock خبر می‌دهد و اینجا به‌ازای «نشست مرورگر» پرچم
///      قفل ثبت می‌شود.
///   ۲) _Layout با همین کلید وضعیت را می‌سنجد و در صورت قفل بودن، اتریبیوت
///      data-ss-start-locked="true" رندر می‌کند — JS محافظ بلافاصله پس از بارگذاری
///      صفحه (رفرش/تب جدید/ناوبری کامل) را قفل می‌کند و رمز می‌خواهد.
///   ۳) POST /Auth/VerifyWakePassword پس از تأیید رمز، پرچم را برمی‌دارد
///      (Unlock) — یعنی تنها راهِ برداشتن قفلِ سرور، دانستن رمز کاربر است.
///
/// چرا کلید = هش کوکی دسترسی (hr_at)؟
///   • در طول عمر نشست مرورگر ثابت است (هیچ چرخشی روی آن نیست) — پس رفرش همان
///     کلید را می‌آورد و قفل سر جایش می‌ماند؛ تب/پنجرهٔ جدید همان کوکی را دارد.
///   • با ورود مجدد توکن تازه صادر می‌شود → کلید جدید → قفلِ قبلی یتیم می‌شود
///     (کاربر تازه رمز خودش را وارد کرده، پس قفل مجدد بی‌معناست).
///   • با خروج، کوکی حذف می‌شود → کلید یتیم و بی‌اثر.
///   • ردیف‌های یتیم (بیش از MaxAge) فرصت‌طلبانه پاک می‌شوند؛ رستارت اپ هم کل
///     دیکشنری درون‌حافظه‌ای را صفر می‌کند (در آن حالت نشست‌ها هم تازه‌اند).
/// همهٔ متدها null/خالی-امن‌اند؛ نبود کوکی = «قفل نیست» (رفتار امن برای صفحه‌های
/// مهمان که اصلاً محافظ ندارند).
/// </summary>
public static class ScreensaverLock
{
    private static readonly ConcurrentDictionary<string, DateTime> _locked = new();
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    /// <summary>کلید نشست مرورگر = SHA-256 کوکی دسترسی (hr_at).</summary>
    public static string KeyOf(string? accessCookie) =>
        string.IsNullOrEmpty(accessCookie) ? "" : JwtService.Sha256Hex(accessCookie);

    /// <summary>آیا این نشست مرورگر قفل است؟ (کوکی غایب/خالی = خیر)</summary>
    public static bool IsLocked(string? accessCookie)
    {
        var k = KeyOf(accessCookie);
        if (k.Length == 0) return false;
        return _locked.TryGetValue(k, out var at) && DateTime.Now - at < MaxAge;
    }

    /// <summary>ثبت/تمدید قفل نشست جاری — از /Auth/ScreensaverLock (فعال‌شدن محافظ).</summary>
    public static void Lock(string? accessCookie)
    {
        var k = KeyOf(accessCookie);
        if (k.Length == 0) return;
        var now = DateTime.Now;
        _locked[k] = now;
        // پاکسازی فرصت‌طلبانهٔ ردیف‌های یتیم (نشست‌های منقضی/خارج‌شده)
        foreach (var kv in _locked)
            if (now - kv.Value > MaxAge)
                _locked.TryRemove(kv.Key, out _);
    }

    /// <summary>برداشتن قفل نشست جاری — فقط پس از تأیید رمز در /Auth/VerifyWakePassword.</summary>
    public static void Unlock(string? accessCookie)
    {
        var k = KeyOf(accessCookie);
        if (k.Length == 0) return;
        _locked.TryRemove(k, out _);
    }
}
