using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace Platform.Web.Infrastructure;

/// <summary>
/// مسیر مشترک keyring ی DataProtection (فاز ۵۱).
/// هم ثبت سرویس در Program.cs (کوکی‌های ورود/آنتی‌فورجری) و هم رمزنگاری فایل
/// تنظیمات راه‌اندازی از همین کلیدها استفاده می‌کنند تا یک keyring واحد باشد.
/// </summary>
public static class DataKeys
{
    public static string Path { get; } = "/home/z/my-project/aspnet/keys";
}

/// <summary>
/// رمزنگاری تنظیمات نصب (فاز ۵۱) — درخواست کاربر:
/// «اطلاعاتی که هنگام راه‌اندازی می‌گیری را رمز شده نگه دار؛ بودن رشتهٔ اتصال
/// (رمز SA و …) در یک فایل JSON متنی از نظر امنیتی درست نیست.»
/// ----------------------------------------------------------------------
/// فایل جدید:  hrsettings.bin  =  JSON {نقش‌ها} رمز‌شده با Data Protection
/// (AES-256-CBC + HMAC به‌صورت پیش‌فرض چارچوب، کلید از keyring مشترک DataKeys.Path
/// با نام برنامهٔ Platform.Web و purpose مجزا "HrAttendance.SetupSettings").
///   • مهاجرت خودکار: اگر hrsettings.json متنی قدیمی بود، خوانده می‌شود، نسخهٔ
///     رمز ساخته و فایل متنی حذف می‌گردد (در صورت هر خطا، فایل متنی حفظ می‌شود
///     تا داده از دست نرود و فقط لاگ می‌شود).
///   • گم‌شدن کلیدها (کپی برنامه بدون پوشهٔ keys): فایل رمز خوانده نمی‌شود →
///     سامانه مثل نصب تازه وارد ویزارد راه‌اندازی می‌شود؛ هرگز کرش نمی‌کند.
///   • نوشتن: فقط از DbConfig.WriteSettingsFileAsync — serialize → protect →
///     نوشتن اتمی (tmp + move) → حذف نسخهٔ متنی قدیمی.
/// </summary>
public static class SetupSecrets
{
    public const string EncryptedFile = "hrsettings.bin";
    public const string LegacyFile = "hrsettings.json";
    public const string Purpose = "HrAttendance.SetupSettings";
    private static readonly object Iolock = new();

    private static readonly Lazy<IDataProtector> _protector = new(() =>
        DataProtectionProvider.Create(
            new DirectoryInfo(DataKeys.Path),
            o => o.SetApplicationName("HrAttendance.Web"))
            .CreateProtector(Purpose));

    private static IDataProtector Protector() => _protector.Value;

    // ------------------------------------------------------------------
    // منبع پیکربندی — جایگزین AddJsonFile("hrsettings.json") در Program.cs
    // ------------------------------------------------------------------
    public sealed class Source : Microsoft.Extensions.Configuration.IConfigurationSource
    {
        private readonly string _contentRoot;
        public Source(string contentRoot) => _contentRoot = contentRoot;
        public Microsoft.Extensions.Configuration.IConfigurationProvider Build(IConfigurationBuilder builder)
            => new Provider(_contentRoot);
    }

    public sealed class Provider : Microsoft.Extensions.Configuration.ConfigurationProvider
    {
        private readonly string _root;
        public Provider(string root) => _root = root;

        public override void Load()
        {
            try
            {
                var encPath = System.IO.Path.Combine(_root, EncryptedFile);
                var legacyPath = System.IO.Path.Combine(_root, LegacyFile);

                if (File.Exists(encPath))
                {
                    // فاز ۸۳ — به‌خاطرسپاری: فایل رمزشده در استارتاپ «بوده» است؛ اگر بعداً حذف شود
                    // (BinDeletedAfterStart) گیت راه‌اندازی، سامانه را به ویزارد برمی‌گرداند.
                    _binPathAtStart = encPath;
                    _hadBinAtStart = true;
                    Data = ReadEncrypted(encPath);
                    return;
                }
                if (File.Exists(legacyPath))
                {
                    // نصب قدیمی — خواندن نسخهٔ متنی (سازگاری) و تلاش برای مهاجرت به نسخهٔ رمز
                    Data = ReadLegacy(legacyPath);
                    TryMigrate(_root, encPath, legacyPath);
                    return;
                }
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                // هرگز اجازه نده خواندن تنظیمات، استارتاپ را بشکند — ویزارد دوباره شروع می‌شود
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    // فاز ۸۳ — ردِ پای hrsettings.bin در استارتاپ (برای تشخیص حذف دستی در زمان اجرا)
    private static volatile bool _hadBinAtStart;
    private static string _binPathAtStart = "";

    /// <summary>true = فایل رمزشدهٔ پیکربندی در استارتاپ وجود داشت و اکنون از دیسک حذف شده است
    /// ← NeedsSetup برمی‌گرداند و همهٔ مسیرها به ویزارد راه‌اندازی می‌روند (بدون ری‌استارت).</summary>
    public static bool BinDeletedAfterStart =>
        _hadBinAtStart && !System.IO.File.Exists(_binPathAtStart);

    // ------------------------------------------------------------------
    // خواندن/نوشتن
    // ------------------------------------------------------------------
    internal static Dictionary<string, string?> ReadEncrypted(string path)
    {
        var cipher = File.ReadAllBytes(path);
        var plain = Protector().Unprotect(cipher);
        return Flatten(Encoding.UTF8.GetString(plain));
    }

    internal static Dictionary<string, string?> ReadLegacy(string path)
    {
        var json = File.ReadAllText(path);
        return Flatten(json);
    }

    /// <summary>نوشتن رمز — اتمی؛ سپس حذف نسخهٔ متنی قدیمی (به‌بهترین‌وجه).</summary>
    public static Task WriteAsync(string contentRoot, Dictionary<string, object?> data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var encPath = System.IO.Path.Combine(contentRoot, EncryptedFile);
        var legacyPath = System.IO.Path.Combine(contentRoot, LegacyFile);
        var cipher = Protector().Protect(Encoding.UTF8.GetBytes(json));

        // نوشتن اتمی (tmp + move) — فایل کوچک است، همگام داخل قفل؛ حذف نسخهٔ متنی قدیمی
        lock (Iolock)
        {
            var tmp = encPath + ".tmp";
            File.WriteAllBytes(tmp, cipher);
            if (File.Exists(encPath)) File.Replace(tmp, encPath, null);
            else File.Move(tmp, encPath);
            TryDelete(legacyPath);
        }
        return Task.CompletedTask;
    }

    /// <summary>مهاجرت نصب قدیمی: ساخت نسخهٔ رمز از JSON متنی و حذف فایل متنی.</summary>
    internal static void TryMigrate(string root, string encPath, string legacyPath)
    {
        try
        {
            var json = File.ReadAllText(legacyPath);
            var cipher = Protector().Protect(Encoding.UTF8.GetBytes(json));
            lock (Iolock)
            {
                var tmp = encPath + ".tmp";
                File.WriteAllBytes(tmp, cipher);
                if (File.Exists(encPath)) File.Replace(tmp, encPath, null);
                else File.Move(tmp, encPath);
                TryDelete(legacyPath);
            }
        }
        catch
        {
            // مهاجرت ناموفق — فایل متنی حفظ شد؛ در ذخیرهٔ بعدی (ویزارد/تنظیمات) دوباره تلاش می‌شود
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* در استفادهٔ بعدی تلاش می‌شود */ }
    }

    // ------------------------------------------------------------------
    // JSON تودرتو → کلیدهای تخت IConfiguration («Database:Provider» و …)
    // ------------------------------------------------------------------
    private static Dictionary<string, string?> Flatten(string json)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;
        using var doc = JsonDocument.Parse(json);
        Walk(doc.RootElement, "", result);
        return result;
    }

    private static void Walk(JsonElement el, string prefix, Dictionary<string, string?> into)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                    Walk(p.Value, prefix.Length == 0 ? p.Name : prefix + ConfigurationPath.KeyDelimiter + p.Name, into);
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in el.EnumerateArray())
                    Walk(item, prefix + ConfigurationPath.KeyDelimiter + i++, into);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                into[prefix] = null;
                break;
            default:
                into[prefix] = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
                break;
        }
    }
}
