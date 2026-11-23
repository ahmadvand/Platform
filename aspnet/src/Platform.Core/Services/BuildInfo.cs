using System.Reflection;

namespace Platform.Web.Services;

/// <summary>
/// اطلاعات build (فاز ۳۱) — نسخه، تولیدکننده، محصول و زمان ساخت از attributes اسمبلی خوانده می‌شود.
/// این مقادیر در «زمان تولید/انتشار» تزریق می‌شوند (dotnet build/publish -p:AppVersion=… -p:VendorName="…")
/// و در فوتر، صفحه ورود، کارت «درباره» و PDFها نمایش داده می‌شوند.
/// </summary>
public static class BuildInfo
{
    private static readonly Assembly Asm =
        Assembly.GetEntryAssembly() ?? typeof(BuildInfo).Assembly;

    /// <summary>نسخه سه‌جزئی — مثل 1.0.0</summary>
    public static string Version { get; } =
        Asm.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";

    /// <summary>نسخه اطلاعاتی کامل (شامل پسوند در صورت تعریف)</summary>
    public static string InformationalVersion { get; } =
        Asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Version;

    /// <summary>نام محصول</summary>
    public static string Product { get; } =
        Asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "سامانه یکپارچه منابع انسانی";

    /// <summary>تولیدکننده نرم‌افزار — از زمان build</summary>
    public static string Vendor { get; } =
        Asm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "نامشخص";

    /// <summary>حق نشر — از زمان build</summary>
    public static string Copyright { get; } =
        Asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    /// <summary>زمان build (UTC رشته ISO) — از AssemblyMetadata که هنگام کامپایل تزریق می‌شود</summary>
    public static string? BuildTimeUtc { get; } =
        Asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(m => m.Key == "BuildTimestamp")?.Value;

    /// <summary>زمان build به شمسی برای نمایش — در دسترس نبودن → «—»</summary>
    public static string BuildTimeFa
    {
        get
        {
            if (BuildTimeUtc is null || !DateTime.TryParse(BuildTimeUtc,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var utc)) return "—";
            var local = utc.ToLocalTime();
            return $"{Jalali.FmtFa(local)} — {Jalali.ToPersianDigits(local.ToString("HH:mm"))}";
        }
    }

    /// <summary>نسخه Runtime جاری (.NET)</summary>
    public static string RuntimeVersion => Environment.Version.ToString();
}
