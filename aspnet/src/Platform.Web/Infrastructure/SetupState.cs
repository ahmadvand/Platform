namespace Platform.Web.Infrastructure;

/// <summary>
/// فاز ۴۸ — وضعیت راه‌اندازی اولیه بانک داده در حافظهٔ پروسه.
/// هدف: سامانه هرگز به‌خاطر مشکل اتصال بانک سقوط نکند.
///   • پیکربندی خالی/ناقص   → گیت ویزارد (NeedsSetup)
///   • پیکربندی هست اما استارتاپ به هر دلیل (سرور خاموش، رشته غلط، نبود دسترسی)
///     ناموفق بود            → RepairMode=true؛ همهٔ مسیرها به /Setup می‌روند تا
///     مدیر پارامترها را اصلاح کند و پیام خطای واقعی استارتاپ همان‌جا دیده می‌شود.
/// پس از موفقیت ویزارد/تنظیمات، MarkReady همه‌چیز را به حالت عادی برمی‌گرداند.
/// </summary>
public static class SetupState
{
    private static volatile bool _repairMode;
    private static volatile string? _lastError;
    private static readonly object _lock = new();

    /// <summary>true = استارتاپ/آخرین اعمال، بانک را آماده نکرده است → گیت /Setup فعال است.</summary>
    public static bool RepairMode => _repairMode;

    /// <summary>آخرین پیام خطای راه‌اندازی (برای نمایش در ویزارد).</summary>
    public static string? LastError => _lastError;

    /// <summary>فراخوانی پس از موفقیت DbInit (استارتاپ یا ویزارد) — حالت عادی.</summary>
    public static void MarkReady()
    {
        lock (_lock)
        {
            _repairMode = false;
            _lastError = null;
        }
    }

    /// <summary>ثبت شکست راه‌اندازی و ورود به حالت تعمیر.</summary>
    public static void MarkFailure(string error)
    {
        lock (_lock)
        {
            _repairMode = true;
            _lastError = error;
        }
    }

    /// <summary>خطای استارتاپ را به پیام فارسی قابل نمایش تبدیل می‌کند.</summary>
    public static string FriendlyError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "خطای نامشخص در اتصال به بانک داده";
        if (raw.Contains("property has not been initialized", StringComparison.OrdinalIgnoreCase))
            return "رشته اتصال بانک داده تنظیم نشده است";
        if (raw.Contains("network-related", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("error: 40", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("error: 26", StringComparison.OrdinalIgnoreCase))
            return "سرور SQL Server پیدا نشد (آدرس/پورت اشتباه یا سرور خاموش است)";
        if (raw.Contains("login failed", StringComparison.OrdinalIgnoreCase))
            return "نام کاربری/رمز SQL نادرست است";
        if (raw.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "مهلت اتصال به سرور به پایان رسید";
        return raw.Length > 400 ? raw[..400] + "…" : raw;
    }
}
