using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LicenseStudio;

// ======================================================================
// موتور مشترک استودیو مجوز نصب (فاز ۴۷) — بدون وابستگی به WinForms
// تا در لینوکس هم به‌صورت هارنس کنسولی راستی‌آزمایی شود.
// قرارداد فایل مجوز دقیقاً همان سرور (Services/LicenseService.cs):
//   محتوا = base64(JSON بدنه camelCase) + "." + base64(امضای RSA-SHA256/PKCS1)
//   بدنه: product, licensee, machine, issuedAtUtc, expiresAtUtc?, maxEmployees?, notes?,
//         lockMode? ("warn" | "lock" — غایب = پیش‌فرض سرور),
//         modules? (["attendance", …] — غایب/خالی = همهٔ ماژول‌ها)
// ======================================================================

public sealed record LicenseDraft(
    string Machine,
    string Licensee,
    DateTime? ExpiresAtUtc,
    int? MaxEmployees,
    string? Notes,
    string? LockMode = null,
    string[]? Modules = null);

// ----------------------------------------------------------------------
// فاز ۸۲ — انتخاب ماژول‌های مجاز (بازگردانی به درخواست کاربر — از ویزارد حذف شده بود):
//   هیچ ماژولی انتخاب نشود = null/خالی → «همهٔ ماژول‌ها» (سازگار با مجوزهای قدیمی)
//   ماژول‌های انتخابی = فقط همان‌ها روی سرور مشتری فعال می‌مانند
//   (سمت سرور: ModuleStateService.LicenseAllows — ماژول بیرون از فهرست قفل می‌شود)
// کد ماژول‌ها باید دقیقاً همان کدهای سمت سرور باشد (ModuleCatalog):
//   org/devices/attendance/payroll/visitors/access/parking — «core» همیشه فعال است و در فهرست نمی‌آید.
// ----------------------------------------------------------------------
public static class LicenseModules
{
    /// <summary>ماژول‌های قابل‌انتخاب — کد + برچسب فارسی (قرینهٔ ModuleCatalog سرور).</summary>
    public static readonly (string Code, string Title)[] All =
    {
        ("org", "سازمان و پرسنل"),
        ("devices", "تجهیزات و گیت‌وی"),
        ("attendance", "حضور و غیاب"),
        ("payroll", "حقوق و دستمزد"),
        ("visitors", "بازدیدکنندگان"),
        ("access", "کنترل تردد"),
        ("parking", "پارکینگ (پلاک‌خوان)"),
    };

    /// <summary>فقط کدهای معتبر را نگه می‌دارد (ترتیب فهرست رسمی حفظ شود).</summary>
    public static string[]? Clean(string[]? codes)
    {
        if (codes is null || codes.Length == 0) return null;
        var set = codes.Where(c => !string.IsNullOrWhiteSpace(c))
                       .Select(c => c.Trim().ToLowerInvariant()).ToHashSet();
        var picked = All.Where(m => set.Contains(m.Code)).Select(m => m.Code).ToArray();
        return picked.Length == 0 ? null : picked;
    }

    /// <summary>نمایش فارسی برای بررسی/تاریخچه — خالی = همهٔ ماژول‌ها.</summary>
    public static string Display(string[]? mods)
    {
        if (mods is null || mods.Length == 0) return "همهٔ ماژول‌ها";
        var titles = All.Where(m => mods.Contains(m.Code, StringComparer.OrdinalIgnoreCase))
                        .Select(m => m.Title).ToArray();
        return titles.Length == 0
            ? "همهٔ ماژول‌ها"
            : Fingerprint.ToPersianDigits(titles.Length.ToString()) + " ماژول — " + string.Join("، ", titles);
    }
}

// ----------------------------------------------------------------------
// حالت اجرای قفل (انتخاب صریح صادرکنندهٔ مجوز) — فاز ۷۸
//   null/خالی = پیش‌فرض سرور (سرور مشتری طبق License:Mode خودش رفتار می‌کند)
//   warn      = فقط هشدار — حتی اگر سرور سخت‌گیرانه پیکربندی شده باشد
//   lock      = قفل سخت — صفحه‌های سامانه تا رفع وضعیت مجوز بسته می‌شوند
// ----------------------------------------------------------------------
public static class LicenseLockMode
{
    public const string Warn = "warn";
    public const string Lock = "lock";

    /// <summary>برچسب‌های کمبوباکس استودیو — ایندکس ۰ = پیش‌فرض سرور.</summary>
    public static readonly string[] Labels =
    {
        "پیش‌فرض سرور (طبق تنظیمات مشتری)",
        "فقط هشدار — سامانه باز می‌ماند",
        "قفل سخت — صفحه‌ها تا رفع مجوز بسته می‌شوند",
    };

    public static string? FromIndex(int index) => index switch
    {
        1 => Warn,
        2 => Lock,
        _ => null,
    };

    /// <summary>نمایش فارسی برای جدول بررسی/تاریخچه.</summary>
    public static string Display(string? lockMode) => lockMode switch
    {
        Warn => "فقط هشدار",
        Lock => "قفل سخت",
        _ => "پیش‌فرض سرور",
    };

    /// <summary>نرمال‌سازی مقدار — فقط warn/lock معتبرند؛ بقیه = null (پیش‌فرض سرور).</summary>
    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Warn => Warn,
        Lock => Lock,
        _ => null,
    };
}

/// <summary>نتیجهٔ بررسی یک فایل/متن مجوز.</summary>
public sealed class InspectResult
{
    public bool SignatureOk { get; init; }
    public bool ProductOk { get; init; }
    public string? Error { get; init; }
    public LicensePayloadInfo? Payload { get; init; }

    /// <summary>اگر اثر انگشت مرجع داده شود — تطابق با سرور مقصد.</summary>
    public bool? MachineMatch { get; init; }
    public bool? Expired { get; init; }
}

public sealed class LicensePayloadInfo
{
    public string Product { get; set; } = "";
    public string Licensee { get; set; } = "";
    public string Machine { get; set; } = "";
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public int? MaxEmployees { get; set; }
    public string? Notes { get; set; }

    /// <summary>فاز ۷۸ — حالت اجرای قفل درج‌شده در مجوز؛ null = پیش‌فرض سرور.</summary>
    public string? LockMode { get; set; }

    /// <summary>فاز ۸۲ — ماژول‌های مجاز درج‌شده در مجوز؛ null/خالی = همهٔ ماژول‌ها.</summary>
    public string[]? Modules { get; set; }
}

public static class LicenseProduct
{
    public const string Name = "HrAttendance";
}

// ----------------------------------------------------------------------
// اثر انگشت سخت‌افزار — نرمال‌سازی و اعتبارسنجی
// ----------------------------------------------------------------------
public static class Fingerprint
{
    public const string Pattern = "XXXXX-XXXXX-XXXXX-XXXXX-XXXXX";

    /// <summary>
    /// پاک‌سازی ورودی کاربر: ارقام فارسی/عربی → لاتین، حذف فاصله‌ها،
    /// بزرگ‌سازی حروف لاتین و چیدن خط‌تیره‌ها هر ۵ نویسه.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is >= '\u06F0' and <= '\u06F9') sb.Append((char)('0' + (ch - '\u06F0')));      // ۰..۹
            else if (ch is >= '\u0660' and <= '\u0669') sb.Append((char)('0' + (ch - '\u0660'))); // ٠..٩
            else if (char.IsWhiteSpace(ch) || ch is '\u200C' or '\u200F' or '\u200E' or '\u0640') continue; // نیم‌فاصله/کنترل
            else if (char.IsAsciiLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
            // خط‌تیره و هر نویسهٔ دیگر حذف — در پایان دوباره می‌چینیم
        }
        var flat = sb.ToString();
        return Regroup(flat);
    }

    /// <summary>چیدن ۲۵ نویسه به قالب ۵×۵ با خط‌تیره.</summary>
    public static string Regroup(string flat25)
    {
        var flat = flat25.Replace("-", "");
        if (flat.Length != 25) return flat; // نامعتبر — اعتبارسنجی گزارش می‌دهد
        var sb = new StringBuilder(29);
        for (var i = 0; i < 5; i++)
        {
            if (i > 0) sb.Append('-');
            sb.Append(flat, i * 5, 5);
        }
        return sb.ToString();
    }

    public static bool IsValid(string? fp)
    {
        if (string.IsNullOrWhiteSpace(fp)) return false;
        var flat = fp.Replace("-", "");
        return flat.Length == 25 && flat.All(char.IsAsciiLetterOrDigit);
    }

    /// <summary>پیام خطای فارسی برای ورودی نامعتبر.</summary>
    public static string? ValidationError(string? fp)
    {
        if (string.IsNullOrWhiteSpace(fp)) return "اثر انگشت سرور را وارد کنید";
        var flat = fp.Replace("-", "");
        if (flat.Length != 25)
            return $"طول اثر انگشت باید ۲۵ نویسه باشد — الان {ToPersianDigits(flat.Length.ToString())} نویسه است";
        if (!flat.All(char.IsAsciiLetterOrDigit))
            return "اثر انگشت فقط باید شامل حروف و ارقام لاتین باشد (بدون ۰/۱/I/O)";
        return null;
    }

    public static string ToPersianDigits(string s) => new(s.Select(c => c is >= '0' and <= '9'
        ? (char)('\u06F0' + (c - '0')) : c).ToArray());
}

// ----------------------------------------------------------------------
// صدور مجوز با کلید خصوصی تولیدکننده
// ----------------------------------------------------------------------
public static class LicenseIssuer
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>امضای بدنه و ساخت محتوای فایل مجوز. در خطا null با پیام فارسی برمی‌گردد.</summary>
    public static string? Issue(string privateKeyPem, LicenseDraft draft, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(draft.Licensee)) { error = "نام دارندهٔ مجوز را وارد کنید"; return null; }
        if (!Fingerprint.IsValid(draft.Machine)) { error = Fingerprint.ValidationError(draft.Machine) ?? "اثر انگشت نامعتبر است"; return null; }

        RSA? rsa = null;
        try
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);

            var payload = new Dictionary<string, object?>
            {
                ["product"] = LicenseProduct.Name,
                ["licensee"] = draft.Licensee.Trim(),
                ["machine"] = draft.Machine.Trim().ToUpperInvariant(),
                ["issuedAtUtc"] = DateTime.UtcNow,
                ["expiresAtUtc"] = draft.ExpiresAtUtc,
                ["maxEmployees"] = draft.MaxEmployees,
                ["notes"] = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes.Trim(),
            };

            // فاز ۷۸ — حالت اجرای قفل: فقط وقتی صادرکننده صریحاً انتخاب کرده باشد درج می‌شود
            // (مجوزهای قدیمی بدون این فیلد = پیش‌فرض سرور، پشتیبانی کامل)
            var lockMode = LicenseLockMode.Normalize(draft.LockMode);
            if (lockMode is not null) payload["lockMode"] = lockMode;

            // فاز ۸۲ — انتخاب ماژول‌های مجاز: فقط وقتی حداقل یک ماژول تیک خورده باشد درج می‌شود؛
            // بدون فهرست = همهٔ ماژول‌ها (همان رفتار مجوزهای قدیمی)
            var modules = LicenseModules.Clean(draft.Modules);
            if (modules is not null) payload["modules"] = modules;

            var json = JsonSerializer.Serialize(payload, JsonOpts);
            var body = Encoding.UTF8.GetBytes(json);
            var sig = rsa.SignData(body, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(body) + "." + Convert.ToBase64String(sig);
        }
        catch (Exception ex)
        {
            error = "امضای مجوز ممکن نشد — کلید خصوصی معتبر نیست (" + RootMessage(ex) + ")";
            return null;
        }
        finally { rsa?.Dispose(); }
    }

    private static string RootMessage(Exception ex)
    {
        var m = ex.Message;
        while (ex.InnerException is { } inner) { ex = inner; m = inner.Message; }
        return m;
    }
}

// ----------------------------------------------------------------------
// خواندن/بررسی مجوز (همان منطق VerifyContent سرور)
// ----------------------------------------------------------------------
public static class LicenseInspector
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static InspectResult Inspect(string content, string publicKeySpkiOrPem, string? referenceFingerprint = null)
    {
        try
        {
            var parts = (content ?? "").Trim().Split('.', 2);
            if (parts.Length != 2)
                return new() { SignatureOk = false, Error = "قالب فایل مجوز درست نیست (باید payload.signature باشد)" };

            var body = Convert.FromBase64String(parts[0]);
            var sig = Convert.FromBase64String(parts[1]);

            using var rsa = RSA.Create();
            ImportPublicKey(rsa, publicKeySpkiOrPem);
            var sigOk = rsa.VerifyData(body, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!sigOk)
                return new() { SignatureOk = false, Error = "امضای دیجیتال تأیید نشد — فایل دستکاری شده یا متعلق به این محصول نیست" };

            var info = JsonSerializer.Deserialize<LicensePayloadInfo>(body, JsonOpts);
            if (info is null)
                return new() { SignatureOk = true, Error = "بدنهٔ مجوز خوانده نشد" };

            var productOk = string.Equals(info.Product, LicenseProduct.Name, StringComparison.Ordinal);
            bool? machineMatch = referenceFingerprint is null
                ? null
                : string.Equals(info.Machine, Fingerprint.Normalize(referenceFingerprint), StringComparison.OrdinalIgnoreCase);
            bool? expired = info.ExpiresAtUtc is { } exp ? exp < DateTime.UtcNow : null;

            return new()
            {
                SignatureOk = true,
                ProductOk = productOk,
                Payload = info,
                MachineMatch = machineMatch,
                Expired = expired,
                Error = productOk ? null : "این مجوز برای محصول دیگری صادر شده است",
            };
        }
        catch (Exception ex)
        {
            return new() { SignatureOk = false, Error = "خواندن فایل مجوز ممکن نشد (" + ex.Message + ")" };
        }
    }

    private static void ImportPublicKey(RSA rsa, string key)
    {
        var trimmed = key.Trim();
        if (trimmed.StartsWith("-----BEGIN", StringComparison.Ordinal))
        {
            rsa.ImportFromPem(trimmed);
            return;
        }
        // Base64 خام (SPKI DER) — همان قالب جاسازی‌شده در برنامهٔ وب
        var normalized = new string(trimmed.Where(c => !char.IsWhiteSpace(c)).ToArray());
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(normalized), out _);
    }
}

// ----------------------------------------------------------------------
// تاریخ شمسی — برای فرم صدور و نمایش فارسی
// ----------------------------------------------------------------------
public static class JalaliDates
{
    private static readonly PersianCalendar Pc = new();

    public static readonly string[] MonthNames =
    {
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    };

    public static (int Y, int M, int D) ToJalali(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return (Pc.GetYear(local), Pc.GetMonth(local), Pc.GetDayOfMonth(local));
    }

    public static bool IsLeap(int y) => Pc.IsLeapYear(y);

    public static int DaysInMonth(int y, int m) => m <= 6 ? 31 : m <= 11 ? 30 : (IsLeap(y) ? 30 : 29);

    /// <summary>ساخت UTC از تاریخ شمسی — null یعنی تاریخ نامعتبر.</summary>
    public static DateTime? FromJalali(int y, int m, int d)
    {
        if (m is < 1 or > 12 || d < 1 || d > DaysInMonth(y, m) || y is < 1300 or > 1500) return null;
        try
        {
            var g = Pc.ToDateTime(y, m, d, 12, 0, 0, 0); // ظهر روز — امن برای تبدیل منطقه‌ای
            return g.ToUniversalTime();
        }
        catch { return null; }
    }

    /// <summary>انتهای روز شمسی انتخابی (۲۳:۵۹:۵۹ UTC همان روز میلادی) — «اعتبار تا پایان روز انتخابی».</summary>
    public static DateTime? EndOfDayFromJalali(int y, int m, int d)
    {
        var mid = FromJalali(y, m, d);
        if (mid is null) return null;
        var g = mid.Value.ToLocalTime().Date;
        return g.AddDays(1).AddSeconds(-1).ToUniversalTime();
    }

    /// <summary>نمایش شمسی با ارقام فارسی: ۱۴۰۶/۰۸/۰۳</summary>
    public static string Fmt(DateTime utc)
    {
        var (y, m, d) = ToJalali(utc);
        return Fingerprint.ToPersianDigits($"{y:0000}/{m:00}/{d:00}");
    }

    /// <summary>نمایش بلند: ۳ آبان ۱۴۰۶</summary>
    public static string FmtLong(DateTime utc)
    {
        var (y, m, d) = ToJalali(utc);
        return Fingerprint.ToPersianDigits(d.ToString()) + " " + MonthNames[m - 1] + " " + Fingerprint.ToPersianDigits(y.ToString());
    }
}
