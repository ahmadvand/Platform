using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Platform.Web.Services;

// ======================================================================
// سامانهٔ مجوز نصب (فاز ۴۶) — قفل نرم‌افزاری متصل به سخت‌افزار سرور
// ----------------------------------------------------------------------
// ساختار مجوز (license.dat):  base64(JSON بدنه)  .  base64(امضای RSA-SHA256)
// کلید خصوصی امضا نزد تولیدکننده می‌ماند و هرگز همراه نرم‌افزار منتشر نمی‌شود؛
// در برنامه فقط کلید عمومی جاسازی شده است. هر تغییر دستی در فایل مجوز →
// امضا خراب می‌شود → مجوز رد می‌شود.
// اثر انگشت سخت‌افزار از MachineGuid ویندوز (ریجستری) یا /etc/machine-id لینوکس
// + نام ماشین ساخته می‌شود؛ کپی برنامه روی سرور دیگر → اثر انگشت فرق می‌کند →
// حالت «WrongMachine».
// فاز ۸۳ب — «فقط با مجوز» (درخواست کاربر): حالت Off و دورهٔ آزمایشی خودکارِ بدون مجوز
// حذف شد؛ بدون فایل مجوز معتبر، سامانه قفل است. «اجرای آزمایشی» هم فقط با مجوز
// معنا دارد (مجوز با تاریخ انقضا که LicenseStudio صادر می‌کند). License:Mode دیگر
// خوانده نمی‌شود و همیشه Strict اعمال می‌شود؛ License:File = App_Data/license.dat
// ======================================================================

public enum LicenseState
{
    Valid,         // مجوز معتبر
    Expired,       // مجوز تاریخ‌گذشته
    WrongMachine,  // مجوز متعلق به سرور دیگری است
    Invalid,       // فایل خراب / امضای جعلی
    Missing,       // فایل مجوز ثبت نشده است — سامانه فقط با مجوز اجرا می‌شود
}

/// <summary>وضعیت لحظه‌ای مجوز — برای میان‌افزار قفل، بنر بالای صفحه و صفحهٔ /License.</summary>
public sealed class LicenseSnapshot
{
    public LicenseState State { get; init; }
    public string Message { get; init; } = "";
    public string? Licensee { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public int? MaxEmployees { get; init; }
    public string? Notes { get; init; }

    /// <summary>فاز ۵۴ — فهرست ماژول‌های مجاز در مجوز (اگر درج شده باشد)؛ null = بدون سقف.</summary>
    public string[]? Modules { get; init; }

    /// <summary>فاز ۷۸ — حالت اجرای قفل درج‌شده در مجوز (warn | lock)؛ null = پیش‌فرض سرور.</summary>
    public string? LockMode { get; init; }

    /// <summary>فاز ۸۳ب — بدون مجوز معتبر (حتی Missing) سامانه قفل است.
    /// فقط State=Valid باز است.</summary>
    public bool Locked => State is not LicenseState.Valid;
}

public sealed class LicenseService
{
    /// <summary>کلید عمومی امضای مجوز (SPKI/Base64 DER) — جفت کلید خصوصی فقط نزد تولیدکننده.
    /// فاز ۵۱ — جفت‌کلید جدید (tools/keys) با دکمهٔ «ساخت جفت‌کلید» استودیو قابل بازتولید است؛
    /// با تعویض کلید، این رشته هم باید با کلید عمومی جدید به‌روز شود.</summary>
    internal const string PublicKeySpki =
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvwA13EkTRJzJ2Tup" +
        "efxsIHYniJkhSJ1ZTpRh47BA/wbcyDt+FxgNpZSBFVjXdP1Gdi8e7tKVoMcA" +
        "dA/e+aDysK2d976ypbDE/I0nJ3j1/oXEnCvMWV+IauplV1j43LaQi3J2IrCs" +
        "mlIsq8gD0Wj+g6miY76EQelx5Q5xPodAS3q1zz2+NH2kh2l0MHKPFJ7SjLtk" +
        "eZF/fYXnkI6tvqiHkIN3PA82JnAlyPYQjmRP8KpSVs9M0E/ce1EEbLK4SMSk" +
        "Yec5i9i6FSxmSRAbD3cBuJiTYVHHGEVCnDnHsmzJUG0BLBU6l2qtDD8coLRy" +
        "yOhPNKqPtNTtcrN1YuLe9MsqfwIDAQAB";

    private const string Salt = "HrAttendance|Licensing|v1";
    private const string ProductName = "HrAttendance";
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // بدون ۰/۱/I/O برای خوانایی تلفنی

    private readonly IConfiguration _cfg;
    private readonly IHostEnvironment _env;
    private readonly ILogger<LicenseService> _log;
    private readonly Lazy<string> _machineId;

    private readonly object _lock = new();
    private LicenseSnapshot _snap = new() { State = LicenseState.Missing, Message = "هنوز بررسی نشده" };
    private DateTime _snapAtUtc = DateTime.MinValue;
    private (string Path, DateTime Write)? _stamp;

    public LicenseService(IConfiguration cfg, IHostEnvironment env, ILogger<LicenseService> log)
    {
        _cfg = cfg; _env = env; _log = log;
        _machineId = new Lazy<string>(ComputeMachineId);
    }

    /// <summary>اثر انگشت یکتای سرور جاری — روی صفحهٔ /License نمایش داده می‌شود تا در مجوز درج شود.</summary>
    public string MachineId => _machineId.Value;

    /// <summary>فاز ۸۳ب — حالت اجرا همیشه «فقط با مجوز» (Strict) است؛ License:Mode از
    /// پیکربندی دیگر خوانده نمی‌شود (حالت Off و دورهٔ آزمایشی خودکار حذف شده‌اند).
    /// خاصیت برای سازگاری صفحهٔ /License و متن‌ها نگه داشته شده است.</summary>
    public string Mode => "Strict";

    /// <summary>مسیر فایل مجوز — پیش‌فرض App_Data/license.dat نسبت به ریشهٔ محتوا.</summary>
    public string LicenseFilePath
    {
        get
        {
            var p = _cfg["License:File"];
            if (string.IsNullOrWhiteSpace(p)) p = "App_Data/license.dat";
            return Path.IsPathRooted(p) ? p : Path.Combine(_env.ContentRootPath, p);
        }
    }

    /// <summary>وضعیت فعلی با کش کوتاه (۲۰ ثانیه یا تغییر فایل) — برای هر درخواست ارزان است.</summary>
    public LicenseSnapshot Current => GetSnapshot(force: false);

    /// <summary>بررسی مجدد بلافاصله — پس از ورود/تغییر فایل مجوز.</summary>
    public LicenseSnapshot Fresh => GetSnapshot(force: true);

    // ------------------------------------------------------------------
    // ارزیابی وضعیت
    // ------------------------------------------------------------------
    private LicenseSnapshot GetSnapshot(bool force)
    {
        lock (_lock)
        {
            var fp = LicenseFilePath;
            DateTime w = File.Exists(fp) ? File.GetLastWriteTimeUtc(fp) : DateTime.MinValue;
            var unchanged = _stamp == (fp, w);
            if (!force && unchanged && (DateTime.UtcNow - _snapAtUtc).TotalSeconds < 20)
                return _snap;

            LicenseSnapshot snap;
            try { snap = Evaluate(); }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "خطا در ارزیابی مجوز");
                snap = new LicenseSnapshot { State = LicenseState.Missing, Message = "خطا در خواندن وضعیت مجوز: " + ex.Message };
            }
            _snap = snap; _snapAtUtc = DateTime.UtcNow; _stamp = (fp, w);
            return snap;
        }
    }

    private LicenseSnapshot Evaluate()
    {
        var file = LicenseFilePath;
        if (File.Exists(file))
        {
            var content = File.ReadAllText(file).Trim();
            var (ok, payload, err) = VerifyContent(content);
            if (!ok || payload is null)
                return new() { State = LicenseState.Invalid, Message = HrT.T("فایل مجوز نامعتبر است — ") + err };

            if (!string.Equals(payload.Machine, MachineId, StringComparison.OrdinalIgnoreCase))
                return new()
                {
                    State = LicenseState.WrongMachine,
                    Licensee = payload.Licensee,
                    LockMode = payload.LockMode,
                    Message = HrT.T("این مجوز برای سرور دیگری صادر شده است (اثر انگشت ماشین مطابقت ندارد)")
                };

            if (payload.ExpiresAtUtc is { } exp && exp < DateTime.UtcNow)
                return new()
                {
                    State = LicenseState.Expired,
                    Licensee = payload.Licensee,
                    ExpiresAtUtc = exp,
                    MaxEmployees = payload.MaxEmployees,
                    Notes = payload.Notes,
                    LockMode = payload.LockMode,
                    Message = $"مجوز «{payload.Licensee}» در تاریخ {Jalali.ToPersianDigits(Jalali.FmtFa(exp.ToLocalTime()))} منقضی شده است"
                };

            return new()
            {
                State = LicenseState.Valid,
                Licensee = payload.Licensee,
                ExpiresAtUtc = payload.ExpiresAtUtc,
                MaxEmployees = payload.MaxEmployees,
                Notes = payload.Notes,
                Modules = payload.Modules,
                LockMode = payload.LockMode,
                Message = payload.ExpiresAtUtc is { } e
                    ? $"مجوز فعال — دارنده: {payload.Licensee} — اعتبار تا {Jalali.ToPersianDigits(Jalali.FmtFa(e.ToLocalTime()))}"
                    : $"مجوز دائمی فعال — دارنده: {payload.Licensee}"
            };
        }

        // فاز ۸۳ب — بدون فایل مجوز، دورهٔ آزمایشی خودکار وجود ندارد (درخواست کاربر);
        // «اجرای آزمایشی» فقط یعنی مجوز دارای تاریخ انقضا که LicenseStudio صادر کرده است.
        return new()
        {
            State = LicenseState.Missing,
            Message = HrT.T("مجوز نصب ثبت نشده است — سامانه فقط با مجوز معتبر اجرا می‌شود")
        };
    }

    // ------------------------------------------------------------------
    // متن بنر هشدار بالای صفحه‌ها — وضعیت غیر از «مجوز معتبر» همیشه یادآور می‌شود
    // ------------------------------------------------------------------
    public string? BannerText
    {
        get
        {
            return Current.State switch
            {
                LicenseState.Expired => HrT.T("🔒 مجوز نصب منقضی شده است"),
                LicenseState.WrongMachine => HrT.T("🔒 مجوز روی این سرور معتبر نیست"),
                LicenseState.Invalid => HrT.T("🔒 فایل مجوز نامعتبر است"),
                LicenseState.Missing => HrT.T("🔒 مجوز نصب ثبت نشده است — سامانه فقط با مجوز اجرا می‌شود"),
                _ => null,
            };
        }
    }

    // ------------------------------------------------------------------
    // اعتبارسنجی و ذخیرهٔ فایل مجوز (صفحهٔ /License — مدیر ارشد)
    // ------------------------------------------------------------------
    public (bool Ok, string Message) ValidateAndSave(string content)
    {
        var (ok, payload, err) = VerifyContent(content);
        if (!ok || payload is null) return (false, "مجوز پذیرفته نشد — " + err);

        var path = LicenseFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.Trim());
        var snap = Fresh;
        _log.LogInformation("مجوز نصب ذخیره شد — دارنده: {Licensee}، وضعیت: {State}", payload.Licensee, snap.State);
        if (snap.State == LicenseState.Valid)
            return (true, "مجوز با موفقیت ثبت و فعال شد — " + snap.Message);
        return (false, "مجوز ذخیره شد اما فعال نیست — " + snap.Message);
    }

    internal (bool Ok, LicensePayload? Payload, string? Error) VerifyContent(string content)
    {
        try
        {
            var parts = content.Trim().Split('.', 2);
            if (parts.Length != 2) return (false, null, "قالب فایل مجوز درست نیست (باید payload.signature باشد)");
            var payloadBytes = Convert.FromBase64String(parts[0]);
            var sig = Convert.FromBase64String(parts[1]);

            // فاز ۵۳ — امضا با کلید مرجع جاسازی‌شده یا کلید عمومیِ همین نصب (صفحهٔ مجوز) پذیرفته می‌شود
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeySpki), out _);
            var sigOk = rsa.VerifyData(payloadBytes, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!sigOk)
            {
                var custom = CustomSpki();
                if (custom is null)
                    return (false, null, "امضای دیجیتال تأیید نشد — فایل دستکاری شده یا متعلق به این محصول نیست");
                rsa.ImportSubjectPublicKeyInfo(custom, out _);
                sigOk = rsa.VerifyData(payloadBytes, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!sigOk)
                    return (false, null, "امضای دیجیتال تأیید نشد — فایل دستکاری شده یا با کلید دیگری امضا شده است");
            }

            var payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes, JsonOpts);
            if (payload is null) return (false, null, "بدنهٔ مجوز خوانده نشد");
            if (!string.Equals(payload.Product, ProductName, StringComparison.Ordinal))
                return (false, null, "این مجوز برای محصول دیگری صادر شده است");
            return (true, payload, null);
        }
        catch (Exception ex)
        {
            return (false, null, "خواندن فایل مجوز ممکن نشد (" + ex.Message + ")");
        }
    }

    // ------------------------------------------------------------------
    // فاز ۵۳ — کلید امضای مجوزِ قابل مدیریت از صفحه (درخواست کاربر:
    // «این کلید خصوصی کجا ساخته می‌شود؟ امکان دانلودش را در صفحهٔ مجوز بگذار»)
    // ------------------------------------------------------------------
    // جفت‌کلید RSA-2048 در App_Data/license-keys ساخته می‌شود:
    //   public.pem  = کلید عمومی (SPKI PEM) — مبنای پذیرش امضا در VerifyContent
    //   private.pem = کلید خصوصی (PKCS#8 PEM) که با Data Protection رمز شده نگه داشته می‌شود
    //                 («PROTECTED1:» + Base64) و فقط از طریق دانلودِ صفحهٔ مجوز به‌صورت PEM واقعی
    //                 تحویل می‌شود — برای استفاده در LicenseStudio یا LicenseGen (--key).
    // گم‌شدن keyring (کپی برنامه بدون پوشهٔ keys) → کلید خصوصی خوانده نمی‌شود؛ به‌جای کرش،
    // کاربر به ساخت جفت‌کلید جدید هدایت می‌شود.
    private static readonly Lazy<Microsoft.AspNetCore.DataProtection.IDataProtector> KeyProtector = new(() =>
        Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create(
            new DirectoryInfo(Infrastructure.DataKeys.Path),
            o => o.SetApplicationName("HrAttendance.Web"))
            .CreateProtector("HrAttendance.LicenseSigningKey"));

    private string KeysDir => Path.Combine(_env.ContentRootPath, "App_Data", "license-keys");
    private string PublicKeyPath => Path.Combine(KeysDir, "public.pem");
    private string PrivateKeyPath => Path.Combine(KeysDir, "private.pem");

    /// <summary>آیا جفت‌کلیدِ ساخته‌شده در همین نصب وجود دارد؟</summary>
    public bool HasSigningKey => File.Exists(PublicKeyPath) && File.Exists(PrivateKeyPath);

    /// <summary>اثر انگشت کلید عمومی مرجع (جاسازی‌شده در برنامه) — همیشه قابل اعتماد است.</summary>
    public static string EmbeddedFingerprint { get; } = FingerprintOfSpki(PublicKeySpki);

    /// <summary>اثر انگشت کلید عمومی این نصب (اگر ساخته شده باشد) — برای نمایش در صفحهٔ مجوز.</summary>
    public string? SigningKeyFingerprint
    {
        get
        {
            try
            {
                if (!File.Exists(PublicKeyPath)) return null;
                return FingerprintOfSpki(File.ReadAllText(PublicKeyPath));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "خواندن کلید عمومی مجوز ممکن نشد");
                return null;
            }
        }
    }

    /// <summary>DER (SPKI) کلید عمومی این نصب — برای VerifyContent؛ ندارد → null.</summary>
    private byte[]? CustomSpki()
    {
        try
        {
            if (!File.Exists(PublicKeyPath)) return null;
            return SpkiDerFromPemOrBase64(File.ReadAllText(PublicKeyPath));
        }
        catch { return null; }
    }

    /// <summary>ساخت جفت‌کلید جدید — خروجی: کلید عمومی (PEM)، اثر انگشت و کلید خصوصی (PEM) برای نمایش/دانلود.</summary>
    public (string PublicPem, string Fingerprint, string PrivatePem) GenerateSigningKey()
    {
        using var rsa = RSA.Create(2048);
        var publicPem = rsa.ExportSubjectPublicKeyInfoPem();
        var privatePem = rsa.ExportPkcs8PrivateKeyPem();

        Directory.CreateDirectory(KeysDir);
        File.WriteAllText(PublicKeyPath, publicPem);

        // کلید خصوصی فقط رمز‌شده روی دیسک می‌نشیند (Data Protection — keyring مشترک)
        var cipher = KeyProtector.Value.Protect(Encoding.UTF8.GetBytes(privatePem));
        var tmp = PrivateKeyPath + ".tmp";
        File.WriteAllText(tmp, "PROTECTED1:" + Convert.ToBase64String(cipher));
        File.Move(tmp, PrivateKeyPath, overwrite: true);

        _log.LogInformation("جفت‌کلید امضای مجوز ساخته شد (App_Data/license-keys)");
        return (publicPem, FingerprintOfSpki(publicPem), privatePem);
    }

    // ------------------------------------------------------------------
    // فاز ۸۱ — صدور مجوز خوداتکا در همین نصب (درخواست کاربر:
    // «ایجاد جفت کلید در صفحه راه اندازی اولیه! برای اینکه کاربر بتونه
    // همون موقع کار ایجاد مجوز رو هم انجام بده»)
    // ------------------------------------------------------------------
    // با کلید خصوصیِ همین نصب (App_Data/license-keys/private.pem) بدنهٔ مجوز با
    // RSA-SHA256 امضا می‌شود؛ چون VerifyContent امضای کلید عمومی همین نصب را هم
    // می‌پذیرد (فاز ۵۳)، مجوزِ تولیدشده بلافاصله فعال می‌شود — بدون LicenseStudio،
    // بدون LicenseGen و بدون واسطهٔ تولیدکننده. قالب خروجی دقیقاً همان license.dat
    // است: base64(JSON بدنه) . base64(امضا)
    /// <summary>صدور مجوز برای همین سرور با کلید امضای همین نصب.
    /// validDays = 0 یا null → مجوز دائمی. maxEmployees = سقف اختیاری پرسنل.
    /// خروجی: (موفقیت، محتوای license.dat، پیام)</summary>
    public (bool Ok, string Content, string Message) IssueLicense(
        string licensee, int? validDays = null, int? maxEmployees = null, string? notes = null)
    {
        var pem = GetSigningPrivateKey();
        if (string.IsNullOrWhiteSpace(pem))
            return (false, "", "کلید امضای این نصب در دسترس نیست — ابتدا جفت‌کلید بسازید");
        if (string.IsNullOrWhiteSpace(licensee))
            return (false, "", "نام دارندهٔ مجوز را وارد کنید");

        try
        {
            var der = Pkcs8DerFromPem(pem) ?? Array.Empty<byte>();
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(der, out _);

            var payload = new LicensePayload
            {
                Product = ProductName,
                Licensee = licensee.Trim(),
                Machine = MachineId,
                IssuedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = validDays is > 0 ? DateTime.UtcNow.AddDays(validDays.Value) : null,
                MaxEmployees = maxEmployees is > 0 ? maxEmployees : null,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                Modules = null,   // null = همهٔ ماژول‌ها
                LockMode = null,  // null = پیش‌فرض سرور (License:Mode)
            };
            var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            var sig = rsa.SignData(body, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var content = Convert.ToBase64String(body) + "." + Convert.ToBase64String(sig);

            // راستی‌آزمایی حلقهٔ بسته — مجوز تولیدشده باید دقیقاً مثل فایل ورودی معتبر شود
            var (ok, payload2, err) = VerifyContent(content);
            if (!ok || payload2 is null)
            {
                _log.LogWarning("صحت مجوز صدرفتهٔ همین نصب تأیید نشد — {Err}", err);
                return (false, "", "صحت مجوز تولیدشده تأیید نشد — " + err);
            }
            _log.LogInformation("مجوز خوداتکا برای همین نصب صادر شد — دارنده: {Licensee}، اثر انگشت کلید: {Fpr}",
                payload2.Licensee, SigningKeyFingerprint);
            return (true, content, "مجوز «" + payload2.Licensee + "» صادر شد");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "صدور مجوز خوداتکا ناموفق بود");
            return (false, "", "صدور مجوز ناموفق بود — " + ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // فاز ۹۰ — ورود (import) کلید خصوصی ساخته‌شده در جای دیگر (درخواست کاربر:
    // «چطور از private.pem که در جای دیگه ساختم استفاده کنم؟ امکان آپلود بذار»)
    // ------------------------------------------------------------------
    // کلید خصوصیِ ساخته‌شده در LicenseStudio / LicenseGen / نصب دیگر (PKCS#8 یا
    // PKCS#1 PEM) همین‌جا ثبت می‌شود؛ کلید عمومی از دل همان کلید استخراج و به‌عنوان
    // مرجع پذیرش امضا ذخیره می‌شود — پس مجوزهای امضاشده با آن private.pem از همین
    // لحظه روی این نصب معتبرند. کلید خصوصی مثل GenerateSigningKey فقط رمزشده
    // (Data Protection) روی دیسک می‌نشیند و هرگز به‌صورت خام ذخیره نمی‌شود.
    /// <summary>ورود کلید خصوصی PEM (PKCS#8 «BEGIN PRIVATE KEY» یا PKCS#1 «BEGIN RSA PRIVATE KEY»)
    /// — خروجی: (موفقیت، اثر انگشت کلید عمومی استخراج‌شده، پیام).</summary>
    public (bool Ok, string Fingerprint, string Message) ImportSigningKey(string pemText)
    {
        if (string.IsNullOrWhiteSpace(pemText))
            return (false, "", "محتوای کلید خصوصی خالی است — فایل private.pem را انتخاب یا محتوای آن را بچسبانید");
        var pem = pemText.Trim();
        try
        {
            using var rsa = RSA.Create();
            if (pem.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal))
            {
                var der = Pkcs8DerFromPem(pem) ?? throw new FormatException("PKCS#8 نامعتبر");
                rsa.ImportPkcs8PrivateKey(der, out _);
            }
            else if (pem.Contains("BEGIN RSA PRIVATE KEY", StringComparison.Ordinal))
            {
                var der = Pkcs8DerFromPem(pem) ?? throw new FormatException("PKCS#1 نامعتبر");
                rsa.ImportRSAPrivateKey(der, out _);
            }
            else if (pem.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal) || pem.Contains("BEGIN CERTIFICATE", StringComparison.Ordinal))
            {
                return (false, "", "این فایل کلید «عمومی» یا گواهی است — فایل کلید «خصوصی» (private.pem) لازم است");
            }
            else
            {
                return (false, "", "قالب فایل شناخته نشد — فایل باید PEM کلید خصوصی باشد (BEGIN PRIVATE KEY یا BEGIN RSA PRIVATE KEY)");
            }

            var publicPem = rsa.ExportSubjectPublicKeyInfoPem();
            var privatePem = rsa.ExportPkcs8PrivateKeyPem();

            Directory.CreateDirectory(KeysDir);
            File.WriteAllText(PublicKeyPath, publicPem);
            var cipher = KeyProtector.Value.Protect(Encoding.UTF8.GetBytes(privatePem));
            var tmp = PrivateKeyPath + ".tmp";
            File.WriteAllText(tmp, "PROTECTED1:" + Convert.ToBase64String(cipher));
            File.Move(tmp, PrivateKeyPath, overwrite: true);

            var fpr = FingerprintOfSpki(publicPem);
            lock (_lock) { _snapAtUtc = DateTime.MinValue; } // کش وضعیت بلافاصله باطل شود
            _log.LogInformation("کلید خصوصی امضای مجوز از فایل بیرونی وارد شد — اثر انگشت: {Fpr}", fpr);
            return (true, fpr, "کلید خصوصی وارد شد — از این پس مجوزهای امضاشده با این کلید پذیرفته می‌شود");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "ورود کلید خصوصی مجوز ناموفق بود");
            return (false, "", "کلید خصوصی پذیرفته نشد — " + ex.Message);
        }
    }

    /// <summary>استخراج DER از PEM کلید خصوصی PKCS#8 — مکمل SpkiDerFromPemOrBase64.</summary>
    private static byte[]? Pkcs8DerFromPem(string pem)
    {
        var trimmed = pem.Trim();
        if (trimmed.StartsWith("-----BEGIN", StringComparison.Ordinal))
        {
            var sb = new StringBuilder();
            foreach (var line in trimmed.Split('\n'))
            {
                var l = line.Trim();
                if (l.Length == 0 || l.StartsWith("-----")) continue;
                sb.Append(l);
            }
            return Convert.FromBase64String(sb.ToString());
        }
        return Convert.FromBase64String(trimmed);
    }

    /// <summary>کلید خصوصی ذخیره‌شده به‌صورت PEM واقعی — برای دانلود از صفحهٔ مجوز؛ ندارد/خراب → null.</summary>
    public string? GetSigningPrivateKey()
    {
        try
        {
            if (!HasSigningKey) return null;
            var raw = File.ReadAllText(PrivateKeyPath).Trim();
            const string marker = "PROTECTED1:";
            if (!raw.StartsWith(marker, StringComparison.Ordinal)) return null;
            var plain = KeyProtector.Value.Unprotect(Convert.FromBase64String(raw[marker.Length..]));
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "بازگشایی کلید خصوصی مجوز ممکن نشد (keyring تغییر کرده؟) — ساخت جفت‌کلید جدید توصیه می‌شود");
            return null;
        }
    }

    /// <summary>حذف جفت‌کلید این نصب — مجوزهای امضاشده با آن دیگر پذیرفته نمی‌شوند.
    /// کش وضعیت (۲۰ ثانیه‌ای) بلافاصله باطل می‌شود تا صفحه، ابطال را همان لحظه نشان دهد.</summary>
    public void RemoveSigningKey()
    {
        foreach (var p in new[] { PublicKeyPath, PrivateKeyPath })
        {
            try { if (File.Exists(p)) File.Delete(p); }
            catch (Exception ex) { _log.LogWarning(ex, "حذف {File} ممکن نشد", p); }
        }
        lock (_lock) { _snapAtUtc = DateTime.MinValue; }
    }

    /// <summary>اثر انگشت کوتاه از کلید عمومی (SHA-256 روی DER) — برای نمایش در صفحه.</summary>
    internal static string FingerprintOfSpki(string pemOrBase64)
    {
        var der = SpkiDerFromPemOrBase64(pemOrBase64) ?? Array.Empty<byte>();
        var hash = SHA256.HashData(der);
        var hex = string.Concat(hash.Take(10).Select(b => b.ToString("X2")));
        return string.Join('-', new[] { hex[..8], hex.Substring(8, 8), hex[16..] });
    }

    /// <summary>استخراج DER از PEM یا Base64 خام (هم‌قرارداد با رشتهٔ SPKI جاسازی‌شده).
    /// تجزیهٔ دستی: خطوط بین BEGIN/END بدون فضای سفید — مستقل از تفسیر PemFields.</summary>
    private static byte[]? SpkiDerFromPemOrBase64(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("-----BEGIN", StringComparison.Ordinal))
        {
            var sb = new StringBuilder();
            foreach (var line in trimmed.Split('\n'))
            {
                var l = line.Trim();
                if (l.Length == 0 || l.StartsWith("-----")) continue;
                sb.Append(l);
            }
            return Convert.FromBase64String(sb.ToString());
        }
        return Convert.FromBase64String(trimmed);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------
    // سقف پرسنل در مجوز — فقط وقتی مجوز معتبر با سقف مشخص فعال باشد
    // ------------------------------------------------------------------
    public (bool Allowed, string Message) CheckEmployeeCap(int currentCount)
    {
        var s = Current;
        if (s.State != LicenseState.Valid || s.MaxEmployees is not int max || max <= 0) return (true, "");
        if (currentCount >= max)
            return (false, $"سقف مجوز نصب: حداکثر {Jalali.ToPersianDigits(max.ToString())} پرسنل — برای افزایش ظرفیت با تولیدکننده تماس بگیرید");
        return (true, "");
    }

    // ------------------------------------------------------------------
    // اثر انگشت سخت‌افزار
    // ------------------------------------------------------------------
    private string MachineRaw()
    {
        var id = "";
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // MachineGuid ویندوز — پایدار در برابر نصب مجدد نرم‌افزار (نه فرمت سیستم)
                using (var hklm = Microsoft.Win32.Registry.LocalMachine)
                using (var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    id = key?.GetValue("MachineGuid") as string ?? "";
            }
            else if (File.Exists("/etc/machine-id"))
            {
                id = File.ReadAllText("/etc/machine-id").Trim();
            }
            else if (File.Exists("/var/lib/dbus/machine-id"))
            {
                id = File.ReadAllText("/var/lib/dbus/machine-id").Trim();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "خواند شناسهٔ ماشین ممکن نشد — از عوامل جایگزین استفاده می‌شود");
        }
        return $"{ProductName}|{id}|{Environment.MachineName}|{Environment.OSVersion.Platform}";
    }

    private string ComputeMachineId()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(MachineRaw() + "|" + Salt));
        var chars = new char[25];
        for (var i = 0; i < 25; i++) chars[i] = Alphabet[hash[i] % Alphabet.Length];
        return string.Create(29, chars, (span, src) =>
        {
            for (var i = 0; i < 5; i++)
            {
                if (i > 0) span[i * 6 - 1] = '-';
                src.AsSpan(i * 5, 5).CopyTo(span[(i * 6)..]);
            }
        });
    }

    // ------------------------------------------------------------------
    // فاز ۸۳ب — سازوکار «دورهٔ آزمایشی خودکار» (فایل license-trial.json + HMAC)
    // به‌کلی حذف شد (درخواست کاربر): اجرای آزمایشی فقط با مجوز دارای تاریخ انقضا
    // که LicenseStudio صادر می‌کند معنا دارد؛ بدون مجوز، سامانه قفل است.
    // ------------------------------------------------------------------
}

/// <summary>بدنهٔ فایل مجوز — با کلید خصوصی تولیدکننده امضا می‌شود (ابزار LicenseGen).</summary>
public sealed class LicensePayload
{
    public string Product { get; set; } = "HrAttendance";
    public string Licensee { get; set; } = "";
    public string Machine { get; set; } = "";
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public int? MaxEmployees { get; set; }
    public string? Notes { get; set; }

    // فاز ۵۴ — سقف ماژول‌ها: فهرست اختیاری کدهای ماژول مجاز در این نصب
    // (core/org/devices/attendance/access/visitors/payroll/parking).
    // null/خالی = همهٔ ماژول‌ها مجاز (سازگاری کامل با مجوزهای قدیمی که این فیلد را ندارند).
    public string[]? Modules { get; set; }

    // فاز ۷۸ — حالت اجرای قفل درج‌شده توسط صادرکنندهٔ مجوز: "warn" | "lock".
    // null/غایب = پیش‌فرض سرور (License:Mode در appsettings مشتری) — سازگار با مجوزهای قدیمی.
    public string? LockMode { get; set; }
}
