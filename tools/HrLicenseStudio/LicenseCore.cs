using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HrLicenseStudio;

// ======================================================================
// موتور مشترک استودیو مجوز نصب (فاز ۴۷) — بدون وابستگی به WinForms
// تا در لینوکس هم به‌صورت هارنس کنسولی راستی‌آزمایی شود.
// قرارداد فایل مجوز دقیقاً همان سرور (Services/LicenseService.cs):
//   محتوا = base64(JSON بدنه camelCase) + "." + base64(امضای RSA-SHA256/PKCS1)
//   بدنه: product, licensee, machine, issuedAtUtc, expiresAtUtc?, maxEmployees?, notes?
// ======================================================================

// فاز ۷۲ — فهرست ماژول‌های خریداری‌شده هم در پیش‌نویس می‌آید؛
// null/خالی = همهٔ ماژول‌ها (سازگار با مجوزهای قدیمی بدون فیلد modules).
public sealed record LicenseDraft(
    string Machine,
    string Licensee,
    DateTime? ExpiresAtUtc,
    int? MaxEmployees,
    string? Notes,
    string[]? Modules = null);

// ----------------------------------------------------------------------
// فاز ۷۲ — کاتالوگ ماژول‌های محصول — همان کدهای مانیفست IUiModule سمت سرور.
// هر ماژولِ خریداری‌نشده در مجوز، حتی اگر DLL آن روی سرور مشتری حاضر باشد،
// از منو حذف و همهٔ صفحه‌ها/API آن بسته می‌شود (گیت ماژول + سقف مجوز).
// ----------------------------------------------------------------------
public static class LicenseModules
{
    public sealed record Def(string Code, string Title, string Icon, string Description);

    public static readonly Def[] All =
    {
        new("org",        "سازمان و پرسنل",      "🏢", "چارت سازمانی، پرسنل، قراردادها، تعطیلات و ورود اکسل — پایهٔ مشترک همهٔ ماژول‌ها"),
        new("attendance", "حضور و غیاب",         "🕒", "شیفت‌ها و نوبت‌کاری، تردد روزانه، مرخصی/ماموریت و محاسبهٔ کارکرد"),
        new("access",     "کنترل تردد",          "🚪", "مناطق و گیت‌ها، مجوزهای عبور، گروه‌های دسترسی و کارت‌های شناسایی"),
        new("payroll",    "حقوق و دستمزد",       "💰", "فرمول‌ساز اقلام، اجرای لیست حقوق، فیش حقوقی، عیدی و تسویه"),
        new("devices",    "تجهیزات و گیت‌وی",     "🖥", "دستگاه‌های تردد، داده‌های بیومتریک و تنظیمات گیت‌وی — تغذیه‌کنندهٔ حضور و کنترل تردد"),
        new("gateway",    "دروازهٔ دستگاه‌ها",     "🖧", "پایش درون‌اپی دستگاه‌های تردد و دوربین‌های پلاک‌خوان (پل ADMS/ANPR)"),
        new("parking",    "پارکینگ (پلاک‌خوان)",  "🅿", "پارکینگ‌ها و دروازه‌ها، خودروها، مجوزهای عبور و رخدادهای ANPR"),
        new("visitors",   "بازدیدکنندگان",        "🪪", "ارباب رجوع، کارت بازدید، پیمانکاران و هشدارهای امنیتی"),
    };

    /// <summary>عنوان فارسی کد ماژول — کد ناشناخته خودش برمی‌گردد (مجوز آینده‌سازگار).</summary>
    public static string TitleOf(string code) =>
        All.FirstOrDefault(m => m.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Title ?? code;

    /// <summary>متن خوانا برای نمایش در فرم/بررسی/تاریخچه — خالی = همهٔ ماژول‌ها.</summary>
    public static string Describe(string[]? modules)
    {
        if (modules is null || modules.Length == 0) return "همهٔ ماژول‌ها";
        return string.Join("، ", modules.Select(TitleOf));
    }

    /// <summary>حذف کدهای تکراری/نامعتبر و یکدست‌سازی حروف — ورودی فرم.</summary>
    public static string[]? Normalize(string[]? modules)
    {
        if (modules is null) return null;
        var clean = modules
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        return clean.Length == 0 ? null : clean;
    }
}

/// <summary>نتیجهٔ بررسی یک فایل/متن مجوز.</summary>
public sealed class InspectResult
{
    public bool SignatureOk { get; init; }
    public bool ProductOk { get; init; }
    public string? Error { get; init; }
    public LicensePayloadInfo? Payload { get; init; }

    /// <summary>فاز ۷۸ — برچسب کلیدی که امضا با آن تأیید شد (کلید رسمی محصول / کلید بارگذاری‌شده / …).
    /// ریشهٔ خطای «مجوز ساخته‌شده با خود استودیو در تب بررسی رد می‌شد» همین بود: بررسی فقط با
    /// کلید جاسازی‌شده انجام می‌شد؛ اگر مجوز با کلید محلی (مثلاً private.pem دانلودشده از سرور)
    /// امضا شده باشد، باید با نیمهٔ عمومی همان کلید هم امتحان شود و نام کلیدِ تأییدکننده نمایش یابد.</summary>
    public string? VerifiedWith { get; init; }

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

    // فاز ۷۲ — فهرست اختیاری ماژول‌های مجاز؛ خالی/غایب = همهٔ ماژول‌ها
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

            // فاز ۷۲ — سقف ماژول‌ها: فقط وقتی «انتخاب محدود» با حداقل یک ماژول فعال باشد
            var mods = LicenseModules.Normalize(draft.Modules);
            if (mods is not null) payload["modules"] = mods;

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

    /// <summary>سازگاری با فراخوانی‌های قدیمی — تک‌کلید.</summary>
    public static InspectResult Inspect(string content, string publicKeySpkiOrPem, string? referenceFingerprint = null)
        => Inspect(content, new[] { ("", publicKeySpkiOrPem) }, referenceFingerprint);

    /// <summary>
    /// فاز ۷۸ — بررسی امضا با چند کلید کاندید به ترتیب؛ اولین کلیدی که امضا را تأیید کند
    /// برچسبش در VerifiedWith می‌آید. اگر هیچ‌کدام تأیید نکردند، پیام خطا راهنمای علت رایج
    /// (ناهمخوانی کلید) را هم می‌دهد تا دیگر «رد شدن مجوز خودی» برای کاربر مرموز نباشد.
    /// </summary>
    public static InspectResult Inspect(string content, (string Label, string Key)[] publicKeys, string? referenceFingerprint = null)
    {
        try
        {
            var parts = (content ?? "").Trim().Split('.', 2);
            if (parts.Length != 2)
                return new() { SignatureOk = false, Error = "قالب فایل مجوز درست نیست (باید payload.signature باشد)" };

            var body = Convert.FromBase64String(parts[0]);
            var sig = Convert.FromBase64String(parts[1]);

            string? verifiedWith = null;
            foreach (var (label, key) in publicKeys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                try
                {
                    using var rsa = RSA.Create();
                    ImportPublicKey(rsa, key);
                    if (rsa.VerifyData(body, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    {
                        verifiedWith = label;
                        break;
                    }
                }
                catch { /* کلید ناسالم/فرمت غلط — کاندید بعدی */ }
            }

            if (verifiedWith is null)
            {
                var known = publicKeys.Count(k => !string.IsNullOrWhiteSpace(k.Label));
                var hint = known > 0
                    ? " — امضا با هیچ‌یک از کلیدهای شناخته‌شدهٔ این استودیو تأیید نشد؛ اگر مجوز با کلید دیگری امضا شده، فایل private.pem همان کلید را با «انتخاب کلید خصوصی…» بارگذاری کنید"
                    : "";
                return new() { SignatureOk = false, Error = "امضای دیجیتال تأیید نشد — فایل دستکاری شده یا با کلید دیگری امضا شده" + hint };
            }

            var info = JsonSerializer.Deserialize<LicensePayloadInfo>(body, JsonOpts);
            if (info is null)
                return new() { SignatureOk = true, VerifiedWith = verifiedWith, Error = "بدنهٔ مجوز خوانده نشد" };

            var productOk = string.Equals(info.Product, LicenseProduct.Name, StringComparison.Ordinal);
            bool? machineMatch = referenceFingerprint is null
                ? null
                : string.Equals(info.Machine, Fingerprint.Normalize(referenceFingerprint), StringComparison.OrdinalIgnoreCase);
            bool? expired = info.ExpiresAtUtc is { } exp ? exp < DateTime.UtcNow : null;

            return new()
            {
                SignatureOk = true,
                ProductOk = productOk,
                VerifiedWith = verifiedWith,
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
