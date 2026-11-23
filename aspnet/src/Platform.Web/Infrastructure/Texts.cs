using System.Globalization;
using System.Text.Json;

namespace Platform.Web.Infrastructure;

/// <summary>
/// فاز ۷۴ — ماشین چندزبانهٔ سامانه
/// ----------------------------------------------------------------------
/// رویکرد «ترجمه بر اساس متن مبدأ»: کلیدِ هر رشته، خودِ متن فارسی آن است و
/// واژه‌نامهٔ انگلیسی (Infrastructure/i18n-en.json — EmbeddedResource) متن
/// فارسی را به معادل انگلیسی نگاشت می‌کند. مزیت: ویوها/کنترلرهای موجود بدون
/// تغییر ساختار، فقط با پیچیدن رشته‌ها در T.X(…) دوزبانه می‌شوند و هر رشته‌ای
/// که هنوز در واژه‌نامه نباشد «همان فارسی» رندر می‌شود — هیچ رشته‌ای گم نمی‌شود.
///
/// فرهنگ‌ها:
///   • fa-IR — زبان مبدأ و پیش‌فرض (RTL)؛ CurrentCulture دست‌نخورده (Invariant)
///     می‌ماند تا قالب‌بندی اعداد/تاریخ/اکسل و PDF مثل همیشه و ثابت بماند؛
///   • en-US — فقط CurrentUICulture عوض می‌شود (LTR).
///
/// انتخاب زبان هر درخواست (HrCultureMiddleware):
///   ‎?lang=fa|en‎ (و ثبت کوکی) ← کوکی hr_lang ← «Localization:Default» از
///   تنظیمات نصب (ویزارد /Setup یا صفحه تنظیمات می‌نویسد) ← فارسی.
/// </summary>
public static class T
{
    /// <summary>نام کوکی انتخاب زبان (یک‌سال، برای همهٔ مسیرها حتی مهمان).</summary>
    public const string CookieName = "hr_lang";

    /// <summary>کدهای رزرو داخلی — فارسی مبدأ، انگلیسی جاسازی‌شده؛ بسته‌های بیرونی این دو را بازنویسی نمی‌کنند.</summary>
    public const string Fa = "fa", En = "en";

    // --------------------------------------------------------------
    // فاز ۷۸ — بسته‌های زبان بیرونی (App_Data/i18n/*.json) — خروجی «استودیو ترجمه»
    // قالب فایل:
    //   { "meta": { "code":"ar", "name":"العربية", "dir":"rtl", "version":"1.0.0", "author":"…" },
    //     "strings": { "متن مبدأ فارسی": "ترجمه", … } }
    // رشتهٔ بی‌ترجمه = همان فارسی رندر می‌شود؛ فایل خراب/نامعتبر نادیده گرفته می‌شود
    // و هرگز اپ را نمی‌کشد (همان سیاست مقاوم‌سازی واژه‌نامهٔ داخلی).
    // مسیر ریشه هنگام راه‌اندازی از Program.cs ست می‌شود (PackagesRoot).
    // --------------------------------------------------------------
    public sealed record LangPack(string Code, string Name, string Dir, string Version, string Author,
        Dictionary<string, string> Strings);

    /// <summary>ریشهٔ بسته‌های زبان — Program.cs به ContentRoot/App_Data/i18n ست می‌کند.</summary>
    public static string? PackagesRoot { get; set; }

    private static readonly Lazy<List<LangPack>> _packs = new(LoadPacks);
    private static List<LangPack> PackList => _packs.Value;

    private static List<LangPack> LoadPacks()
    {
        var list = new List<LangPack>();
        try
        {
            if (string.IsNullOrEmpty(PackagesRoot) || !Directory.Exists(PackagesRoot)) return list;
            foreach (var file in Directory.GetFiles(PackagesRoot, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file),
                        new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true });
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("meta", out var meta) || !root.TryGetProperty("strings", out var strings)
                        || strings.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                    var code = meta.TryGetProperty("code", out var c) ? c.GetString()?.Trim().ToLowerInvariant() ?? "" : "";
                    if (code is Fa or En || code.Length < 2 || code.Length > 10) continue; // رزروها
                    var name = meta.TryGetProperty("name", out var n) ? n.GetString()?.Trim() ?? code : code;
                    var dir = meta.TryGetProperty("dir", out var d) && d.GetString() == "ltr" ? "ltr" : "rtl";
                    var ver = meta.TryGetProperty("version", out var v) ? v.GetString() ?? "1.0.0" : "1.0.0";
                    var author = meta.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "";
                    var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var prop in strings.EnumerateObject())
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                            dict[prop.Name] = prop.Value.GetString() ?? "";
                    if (dict.Count == 0) continue;
                    list.Add(new LangPack(code, name, dir, ver, author, dict));
                }
                catch { /* فایل خراب — نادیده */ }
            }
        }
        catch { /* پوشهٔ غایب/قفل — بدون بسته بیرونی */ }
        return list;
    }

    /// <summary>همهٔ زبان‌های قابل انتخاب — fa و en همیشه + بسته‌های بیرونی به ترتیب کد.</summary>
    public static string[] Codes
        => PackList.Count == 0 ? new[] { Fa, En } : new[] { Fa, En }.Concat(PackList.Select(p => p.Code)).ToArray();

    /// <summary>نام نمایشی هر زبان به خودِ آن زبان (هرگز ترجمه نمی‌شود)؛ کد ناشناخته خودش برمی‌گردد.</summary>
    public static string NameOf(string code)
    {
        if (code == Fa) return "فارسی";
        if (code == En) return "English";
        return PackList.FirstOrDefault(p => p.Code == code)?.Name ?? code;
    }

    /// <summary>واژه‌نامهٔ داخلی انگلیسی.</summary>
    private static readonly Lazy<Dictionary<string, string>> _en = new(LoadEn);

    private static Dictionary<string, string> LoadEn()
    {
        try
        {
            var asm = typeof(T).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("i18n-en.json", StringComparison.Ordinal));
            if (name is null) return new Dictionary<string, string>();
            using var s = asm.GetManifestResourceStream(name);
            if (s is null) return new Dictionary<string, string>();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(s,
                       new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip })
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            // واژه‌نامه خراب/غایب = همه‌چیز به فارسی رندر می‌شود — هرگز نباید اپ را بکشد
            return new Dictionary<string, string>();
        }
    }

    /// <summary>اعتبار کد زبان — داخلی (fa/en) یا کد یکی از بسته‌های بارگذاری‌شده.</summary>
    public static bool IsValid(string? code) =>
        code is not null && Codes.Contains(code.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>زبان فعلی درخواست — بر اساس CurrentUICulture (که میان‌افزار می‌چیند).
    /// فاز ۷۸ — کد بسته‌های بیرونی هم (مثل ar) از فرهنگ جاری بازسازی می‌شود.</summary>
    public static string Lang
    {
        get
        {
            var t = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return IsValid(t) ? t : Fa;
        }
    }

    public static bool IsEn => Lang == "en";

    /// <summary>جهت متن صفحه برای تگ html — fa: rtl، en: ltr؛ بسته‌های بیرونی از متای خودشان.</summary>
    public static string Dir => DirFor(Lang);

    public static string DirFor(string code)
    {
        if (code == Fa) return "rtl";
        if (code == En) return "ltr";
        return PackList.FirstOrDefault(p => p.Code == code)?.Dir ?? "rtl"; // پیش‌فرض محافظه‌کار: راست‌به‌چپ
    }

    /// <summary>ترجمهٔ رشته — کلید = متن فارسی مبدأ؛ نبود در واژه‌نامه = همان متن مبدأ.</summary>
    public static string X(string? fa)
    {
        if (string.IsNullOrEmpty(fa) || Lang == Fa) return fa ?? "";
        var code = Lang;
        if (code == En) return _en.Value.TryGetValue(fa, out var en) ? en : fa;
        var pack = PackList.FirstOrDefault(p => p.Code == code);
        return pack is not null && pack.Strings.TryGetValue(fa, out var tr) && tr.Length > 0 ? tr : fa;
    }

    /// <summary>قالب‌بندی با آرگومان‌ها: «سلام {0}» → X(قالب، آرگومان‌ها).</summary>
    public static string X(string fa, params object?[] args)
        => string.Format(CultureInfo.InvariantCulture, X(fa), args);

    /// <summary>ارقام: فارسی = ارقام فارسی (مثل قبل)، انگلیسی = ارقام لاتین.</summary>
    public static string Num(string? s)
        => s is null ? "" : (IsEn ? s : Services.Jalali.ToPersianDigits(s));
}
