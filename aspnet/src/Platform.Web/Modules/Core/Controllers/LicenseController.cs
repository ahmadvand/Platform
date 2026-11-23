using Platform.Web.Data;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Web.Controllers;

/// <summary>
/// صفحهٔ مدیریت مجوز نصب (فاز ۴۶) — نمایش وضعیت، اثر انگشت سخت‌افزار و
/// ورود فایل مجوز (.dat) توسط مدیر ارشد. در حالت قفل‌شده این صفحه از
/// میان‌افزار LicenseGate مستثناست تا امکان رفع قفل وجود داشته باشد.
/// </summary>
public class LicenseController : MvcBase
{
    private readonly LicenseService _lic;
    private readonly SettingsService _settings;

    private readonly HrBaseDbContext Base;

    public LicenseController(AppDbContext db, HrBaseDbContext baseDb, ICurrentUser me, LicenseService lic, SettingsService settings) : base(db, me)
    {
        Base = baseDb;
        (_lic, _settings) = (lic, settings);
    }

    [HttpGet("/License")]
    public IActionResult Index()
    {
        var sess = Sess;
        ViewBag.CanManage = sess?.IsSuperadmin ?? false;
        ViewBag.Info = _lic.Current;
        ViewBag.MachineId = _lic.MachineId;
        ViewBag.Mode = _lic.Mode;
        // فاز ۵۳ — مدیریت کلید امضا (ساخت/دانلود private.pem از همین صفحه)
        ViewBag.HasSigningKey = _lic.HasSigningKey;
        ViewBag.SigningFpr = _lic.SigningKeyFingerprint;
        ViewBag.EmbeddedFpr = LicenseService.EmbeddedFingerprint;
        ViewData["Title"] = "مجوز نصب";
        return View();
    }

    /// <summary>
    /// فاز ۵۰ — دانلود «فایل درخواست مجوز» (.hrreq): امضای سرور + نام شرکت + نسخه در یک فایل JSON
    /// تا نصب‌کننده به‌جای تایپ دستی اثر انگشت، فقط فایل را برای تولیدکننده بفرستد و
    /// در LicenseStudio با «بازکردن فایل درخواست» به‌طور خودکار پر شود.
    /// بدون نیاز به نشست — همین اطلاعات در صفحهٔ /License هم بدون ورود نمایش داده می‌شود.
    /// </summary>
    [HttpGet("/License/RequestFile")]
    public async Task<IActionResult> RequestFile()
    {
        var appSet = await _settings.GetSafeAsync();
        var fp = _lic.MachineId ?? "";
        var payload = new Dictionary<string, string?>
        {
            ["product"] = "سامانه یکپارچه منابع انسانی",
            ["version"] = BuildInfo.Version,
            ["machine"] = fp,
            ["company"] = appSet.CompanyName,
            ["createdAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        var slug = fp.Replace("-", "");
        var fname = "license-request-" + (slug.Length >= 5 ? slug[..5] : "server") + ".hrreq";
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fname);
    }

    // ==================================================================
    // فاز ۵۳ — مدیریت کلید امضای مجوز از صفحهٔ مجوز (درخواست کاربر:
    // «این کلید خصوصی کجا ساخته می‌شود؟ امکان دانلودش را در صفحهٔ مجوز بگذار»)
    // جفت‌کلید RSA-2048 سمت سرور ساخته می‌شود؛ کلید خصوصی رمزشده (Data Protection)
    // در App_Data/license-keys می‌ماند و فقط از همین صفحه به‌صورت private.pem
    // واقعی دانلود می‌شود — برای امضا در LicenseStudio یا LicenseGen (--key).
    // امضای مجوز با کلید مرجع جاسازی‌شدهٔ تولیدکننده هم همچنان معتبر می‌ماند.
    // ==================================================================
    private bool KeyManagerGuard()
    {
        var sess = Sess;
        if (sess is null)
        {
            ToastErr("برای مدیریت کلید امضا، ابتدا با حساب مدیر ارشد وارد شوید");
            Response.Redirect("/Auth/Login?returnUrl=%2FLicense");
            return false;
        }
        if (!sess.IsSuperadmin)
        {
            ToastErr("فقط «مدیر ارشد سیستم» می‌تواند کلید امضای مجوز را مدیریت کند");
            Response.Redirect("/License");
            return false;
        }
        return true;
    }

    /// <summary>ساخت جفت‌کلید جدید — کلید عمومی همان لحظه مبنای پذیرش امضا می‌شود.</summary>
    [HttpPost("/License/GenerateKey")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateKey()
    {
        if (!KeyManagerGuard()) return Empty;
        var (_, fpr, _) = _lic.GenerateSigningKey();
        await Audit("license-key-generated", "license", "", new { fingerprint = fpr });
        ToastOk(HrT.F("جفت‌کلید جدید ساخته شد — اثر انگشت: {0}. کلید خصوصی را دانلود و در محل امن نگه دارید.", fpr));
        return Redirect("/License");
    }

    /// <summary>دانلود کلید خصوصی (private.pem) — تنها راه بیرون‌کشیدن کلید از سرور.</summary>
    [HttpGet("/License/PrivateKey")]
    public async Task<IActionResult> PrivateKey()
    {
        var sess = Sess;
        if (sess is null) return Redirect("/Auth/Login?returnUrl=%2FLicense");
        if (!sess.IsSuperadmin)
        {
            ToastErr("فقط «مدیر ارشد سیستم» می‌تواند کلید خصوصی را دانلود کند");
            return Redirect("/License");
        }
        var pem = _lic.GetSigningPrivateKey();
        if (pem is null)
        {
            ToastErr("کلید خصوصی ذخیره‌شده‌ای پیدا نشد — از دکمهٔ «ساخت جفت‌کلید جدید» یکی بسازید");
            return Redirect("/License");
        }
        await Audit("license-key-downloaded", "license", "", null);
        return File(System.Text.Encoding.UTF8.GetBytes(pem), "application/x-pem-file", "private.pem");
    }

    /// <summary>فاز ۹۰ — ورود کلید خصوصی ساخته‌شده در جای دیگر (LicenseStudio/LicenseGen/نصب دیگر) —
    /// کلید عمومی از دل فایل ارسالی استخراج و مرجع پذیرش امضای همین نصب می‌شود.</summary>
    [HttpPost("/License/ImportKey")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportKey(string? privatePem)
    {
        if (!KeyManagerGuard()) return Empty;
        var (ok, fpr, msg) = _lic.ImportSigningKey(privatePem ?? "");
        await Audit("license-key-imported", "license", "", new { ok, fingerprint = fpr });
        if (ok) ToastOk(HrT.F("{0} — اثر انگشت: {1}", msg, fpr));
        else ToastErr(msg);
        return Redirect("/License");
    }

    /// <summary>حذف جفت‌کلید این نصب — مجوزهای امضاشده با آن دیگر پذیرفته نمی‌شوند.</summary>
    [HttpPost("/License/RemoveKey")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveKey()
    {
        if (!KeyManagerGuard()) return Empty;
        var fpr = _lic.SigningKeyFingerprint;
        _lic.RemoveSigningKey();
        await Audit("license-key-removed", "license", "", new { fingerprint = fpr });
        ToastOk("جفت‌کلید این نصب حذف شد — فقط مجوزهای امضاشده با کلید مرجع تولیدکننده پذیرفته می‌شوند");
        return Redirect("/License");
    }

    [HttpPost("/License/Import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file, string? text)
    {
        var sess = Sess;
        if (sess is null)
        {
            ToastErr("برای ورود مجوز، ابتدا با حساب مدیر ارشد وارد شوید");
            return Redirect("/Auth/Login?returnUrl=%2FLicense");
        }
        if (!sess.IsSuperadmin)
        {
            ToastErr("فقط «مدیر ارشد سیستم» می‌تواند مجوز نصب را وارد کند");
            return Redirect("/License");
        }

        string? content = null;
        if (file is { Length: > 0 })
        {
            using var reader = new StreamReader(file.OpenReadStream());
            content = await reader.ReadToEndAsync();
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            content = text;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            ToastErr("فایل مجوز (.dat) یا متن مجوز را وارد کنید");
            return Redirect("/License");
        }

        var (ok, msg) = _lic.ValidateAndSave(content);
        await Audit(ok ? "license-activated" : "license-rejected", "license", "", new { message = msg });
        if (ok)
        {
            ToastOk(msg);
            return Redirect("/");
        }
        ToastErr(msg);
        return Redirect("/License");
    }

    // ==================================================================
    // فاز ۵۱ — هشدار به‌جای قطع: انقضای مجوز دیگر سامانه را از کار نمی‌اندازد؛
    // فقط ناوبری کامل صفحه تا «تایید هشدار» به همین صفحه می‌رسد و پس از دکمهٔ
    // «ادامه به سامانه»، کوکی نشست جاری ست می‌شود. با ورود بعدی کوکی پاک می‌شود
    // (در AuthController) تا هشدار دوباره نمایش داده شود.
    // ==================================================================
    [HttpGet("/License/Notice")]
    public IActionResult Notice()
    {
        var sess = Sess;
        if (sess is null) return Redirect("/Auth/Login");
        var snap = _lic.Fresh;
        if (!snap.Locked) return Redirect("/"); // وضعیت درست شد — نیازی به هشدار نیست
        ViewBag.Info = snap;
        ViewBag.MachineId = _lic.MachineId;
        ViewBag.Mode = _lic.Mode;
        // فاز ۷۸ — «قفل سخت» درج‌شده در مجوز: دکمهٔ تایید نمایش/اثر ندارد
        ViewBag.HardLock = string.Equals(snap.LockMode, "lock", StringComparison.OrdinalIgnoreCase);
        ViewData["Title"] = "هشدار مجوز نصب";
        return View("Notice");
    }

    /// <summary>تایید هشدار مجوز — ادامه به سامانه (کوکی نشست جاری؛ با هر ورود پاک می‌شود).</summary>
    [HttpPost("/License/Ack")]
    [ValidateAntiForgeryToken]
    public IActionResult Ack()
    {
        // فاز ۷۸ — در حالت «قفل سخت» درج‌شده در مجوز، تایید هشدار پذیرفته نیست
        var snap = _lic.Fresh;
        if (snap.Locked && string.Equals(snap.LockMode, "lock", StringComparison.OrdinalIgnoreCase))
            return Redirect("/License/Notice");

        Response.Cookies.Append(Infrastructure.LicenseGate.AckCookie, "1", new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
        });
        return Redirect("/");
    }
}
