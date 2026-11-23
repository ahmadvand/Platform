using System.Text.Encodings.Web;
using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۴۵ — ویزارد راه‌اندازی اولیه: به‌جای دستکاری appsettings.json، پارامترهای اتصال
/// به بانک داده در قالب فرم گرفته می‌شود، تست و سپس ذخیره می‌شود (hrsettings.json) و
/// بانک در همان لحظه ساخته و مقداردهی می‌شود — بدون نیاز به ری‌استارت دستی.
/// تا پیش از پیکربندی، گیت میانه (SetupGate) همه مسیرها را به همین‌جا هدایت می‌کند.
/// فاز ۴۸ — حالت تعمیر: اگر استارتاپ به هر دلیل (رشتهٔ اتصال خالی، سرور خاموش، رشتهٔ
/// غلط) ناموفق بود، همین ویزارد با پیام خطا و پارامترهای فعلی باز می‌شود تا مدیر اتصال را
/// اصلاح کند؛ پس از ذخیرهٔ موفق، سامانه بلافاصله (بدون ری‌استارت) به حالت عادی برمی‌گردد.
/// فاز ۶۲ — گام مجوز نصب: ثبت/انتخاب وضعیت مجوز در همان نصب اولیه، تا وقتی نرم‌افزار
/// اجرا شد «تکلیف مجوز» هم مشخص باشد (درخواست کاربر). فایل مجوز (یا محتوای آن) همین‌جا
/// اعتبارسنجی و فعال می‌شود و حالت قفل (Off/Strict) در تنظیمات رمزشدهٔ نصب ذخیره می‌شود.
/// فاز ۸۳ب — «فقط با مجوز» (درخواست کاربر): راه‌اندازی بدون مجوز معتبر ممکن نیست؛
/// حالت Off و فیلد «طول دورهٔ آزمایشی» از ویزارد حذف شد و License:Mode همیشه Strict
/// نوشته می‌شود. اجرای آزمایشی فقط یعنی مجوز دارای تاریخ انقضا (صادره از LicenseStudio).
/// </summary>
public class SetupController(
    IConfiguration cfg,
    IServiceProvider rootSp,
    IWebHostEnvironment env,
    ILogger<SetupController> log,
    LicenseService license) : Controller
{
    private readonly IConfiguration _cfg = cfg;
    private readonly IConfigurationRoot? _root = cfg as IConfigurationRoot;
    private readonly IServiceProvider _rootSp = rootSp;
    private readonly IWebHostEnvironment _env = env;
    private readonly ILogger<SetupController> _log = log;
    private readonly LicenseService _license = license;

    /// <summary>فاز ۴۸ — در حالت تعمیر، ویزارد حتی با پیکربندی موجود باز می‌ماند تا اصلاح شود.</summary>
    private bool Repair => SetupState.RepairMode;

    /// <summary>
    /// فاز ۴۸ — مرجع واحد تصمیم: همان معیاری که گیت میانه استفاده می‌کند.
    /// نکتهٔ حیاتی: Index نباید از IsConfigured خالص استفاده کند — در نصب‌های قدیمی
    /// (کلید Setup غایب = IsConfigured=true) با رشتهٔ اتصال خالی، گیت همه‌چیز را به
    /// /Setup می‌فرستد ولی اینجا به / برمی‌گشت → حلقهٔ ریدایرکت بی‌نهایت در مرورگر.
    /// </summary>
    private bool SetupNeeded => DbConfig.NeedsSetup(_cfg);

    [HttpGet]
    public IActionResult Index()
    {
        if (!SetupNeeded && !Repair) return Redirect("/");
        FillCurrent();
        return View();
    }

    /// <summary>فاز ۴۸ — پیش‌پرکردن فرم از پیکربندی مؤثر جاری (hrsettings.json یا appsettings.json).
    /// فاز ۶۲ — وضعیت مجوز (حالت قفل) و اثر انگشت ماشین برای گام مجوز ویزارد.</summary>
    private void FillCurrent()
    {
        var (provider, cs) = DbConfig.Resolve(_cfg);
        ViewBag.Repair = Repair;
        ViewBag.StartupError = Repair ? SetupState.FriendlyError(SetupState.LastError ?? "") : null;
        ViewBag.CurProvider = provider;
        ViewBag.CurSeedDemo = _cfg.GetValue("SeedDemo", true);
        // فاز ۶۲ — مجوز نصب: حالت فعلی + اثر انگشت سخت‌افزار برای درخواست فایل مجوز
        ViewBag.CurLicenseMode = "Strict"; // فاز ۸۳ب — همیشه فقط با مجوز
        ViewBag.MachineId = _license.MachineId;
        ViewBag.HasLicenseFile = System.IO.File.Exists(_license.LicenseFilePath);
        // فاز ۷۶ — زبان رابط کاربری: انتخاب کاربر در کوکی، وگرنه زبان ذخیره‌شدهٔ نصب
        var cook = HttpContext.Request.Cookies[Infrastructure.CultureMiddleware.CookieName];
        ViewBag.CurUiLanguage = HrLang.Supported(cook) ? cook! : HrLang.FromConfig(_cfg);
        // فاز ۸۱ — خوداتکای مجوز: وجود جفت‌کلید امضا + اثر انگشت آن برای گام صدور مجوز
        ViewBag.HasSigningKey = _license.HasSigningKey;
        ViewBag.SigningFpr = _license.SigningKeyFingerprint;

        if (DbConfig.IsSqlite(provider))
        {
            ViewBag.CurDbPath = cs is null ? "hr.db" : DbConfig.ParseSqlite(cs);
        }
        else
        {
            // نکته: «!» روی nullable value type آن را باز نمی‌کند؛ با الگوی is {} باز می‌کنیم
            if (DbConfig.ParseSqlFull(cs) is { } pp)
            {
                ViewBag.CurServer = string.IsNullOrWhiteSpace(pp.Server) ? "localhost" : pp.Server;
                ViewBag.CurPort = string.IsNullOrWhiteSpace(pp.Port) ? "1433" : pp.Port;
                ViewBag.CurDb = string.IsNullOrWhiteSpace(pp.Database) ? "HrAttendance" : pp.Database;
                ViewBag.CurWindowsAuth = pp.WindowsAuth;
                ViewBag.CurUser = string.IsNullOrWhiteSpace(pp.User) ? "sa" : pp.User;
            }
            else
            {
                ViewBag.CurServer = "localhost";
                ViewBag.CurPort = "1433";
                ViewBag.CurDb = "HrAttendance";
                ViewBag.CurWindowsAuth = false;
                ViewBag.CurUser = "sa";
            }
        }
    }

    /// <summary>تست اتصال از فرم ویزارد — بدون ذخیره.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(
        string? provider, string? server, string? port, string? database,
        string? authMode, string? user, string? password, bool ssl = false, bool createIfMissing = true,
        string? dbPath = null)
    {
        if (!SetupNeeded && !Repair) return Json(new { ok = false, message = HrT.T("سامانه از قبل پیکربندی شده است") });
        var res = await TestAsync(provider, server, port, database, authMode, user, password, ssl, createIfMissing, dbPath);
        return Json(new { ok = res.Ok, message = HrT.T(res.Message) });
    }

    private async Task<(bool Ok, string Message)> TestAsync(
        string? provider, string? server, string? port, string? database,
        string? authMode, string? user, string? password, bool ssl, bool createIfMissing, string? dbPath)
    {
        if (DbConfig.IsSqlite(provider ?? ""))
        {
            var (ok, msg, _) = DbConfig.TestSqlite(dbPath ?? "hr.db", _env.ContentRootPath);
            return (ok, msg);
        }
        var p = new DbConfig.SqlParams
        {
            Server = server ?? "", Port = port ?? "1433", Database = string.IsNullOrWhiteSpace(database) ? "HrAttendance" : database!,
            WindowsAuth = authMode == "windows", User = user ?? "", Password = password ?? "",
            Ssl = ssl, CreateIfMissing = createIfMissing,
        };
        return await DbConfig.TestSqlServerAsync(p);
    }

    /// <summary>ذخیره پیکربندی: مجوز (اجباری) → تست → ساخت و مقداردهی بانک → نوشتن hrsettings.json → Reload → ورود.
    /// فاز ۴۸: در حالت تعمیر هم اجرا می‌شود و پس از موفقیت، سامانه را از حالت تعمیر خارج می‌کند.
    /// فاز ۸۳ب (درخواست کاربر): راه‌اندازی فقط با مجوز معتبر ممکن است — بدون فایل/محتوای مجوز،
    /// هیچ چیزی نوشته نمی‌شود. حالت Off و فیلد دورهٔ آزمایشی حذف شد؛ License:Mode همیشه Strict.
    /// خطای مجوز = هیچ تنظیمی نوشته نمی‌شود (تلاش مجدد امن است — ساخت بانک خودترمیم است).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        string? provider, string? server, string? port, string? database,
        string? authMode, string? user, string? password, bool ssl = false, bool createIfMissing = true,
        string? dbPath = null, bool seedDemo = true,
        string? licenseContent = null,
        string? uiLanguage = null)
    {
        if (!SetupNeeded && !Repair) return Json(new { ok = false, message = HrT.T("سامانه از قبل پیکربندی شده است") });

        // فاز ۸۳ب — راه‌اندازی فقط با مجوز: بدون محتوای مجوز، هیچ چیزی نوشته نمی‌شود
        if (string.IsNullOrWhiteSpace(licenseContent))
            return Json(new { ok = false, message = HrT.T("راه‌اندازی سامانه فقط با مجوز ممکن است — فایل مجوز را انتخاب یا محتوای آن را در کادر وارد کنید") });

        // فاز ۶۲/۸۳ب — اعتبارسنجی و فعال‌سازی مجوز پیش از هر نوشتنی؛ خطای مجوز = هیچ‌چیز نیمه‌کاره نمی‌ماند
        var (licOk, licMsg) = _license.ValidateAndSave(licenseContent);
        if (!licOk)
        {
            _log.LogWarning("مجوز ارسالی ویزارد پذیرفته نشد: {Msg}", licMsg);
            return Json(new { ok = false, message = HrT.F("مجوز پذیرفته نشد — {0}", licMsg) });
        }
        string licenseMsg = licMsg;
        _log.LogInformation("مجوز نصب از ویزارد راه‌اندازی ثبت و فعال شد");

        var isSqlite = DbConfig.IsSqlite(provider ?? "");
        string cs;

        if (isSqlite)
        {
            cs = DbConfig.BuildSqlite(dbPath ?? "hr.db");
        }
        else
        {
            // اول تست — در صورت خطا هیچ چیزی ذخیره نمی‌شود
            var test = await TestAsync(provider, server, port, database, authMode, user, password, ssl, createIfMissing, dbPath);
            if (!test.Ok) return Json(new { ok = false, message = HrT.T(test.Message) });
            cs = DbConfig.BuildSqlServer(new DbConfig.SqlParams
            {
                Server = server ?? "", Port = port ?? "1433",
                Database = string.IsNullOrWhiteSpace(database) ? "HrAttendance" : database!,
                WindowsAuth = authMode == "windows", User = user ?? "", Password = password ?? "",
                Ssl = ssl, CreateIfMissing = createIfMissing,
            });
        }

        // ساخت و مقداردهی کامل بانک (اسکیما + خودترمیم + سیدر + سیدِ ماژول‌ها) — پیش از نوشتن فایل
        _log.LogInformation("ویزارد راه‌اندازی: ساخت و مقداردهی بانک {Provider} …", provider);
        var init = await DbConfig.InitNewDatabaseAsync(isSqlite ? "Sqlite" : "SqlServer", cs, seedDemo, _env); // فاز ۸۸ — محیط واقعی میزبان برای پروایدر مستقل ویزارد
        if (!init.Ok)
        {
            _log.LogError(init.Error, "راه‌اندازی اولیه بانک ناموفق بود");
            // فاز ۴۸ — در حالت تعمیر، شکست باید در گیت هم باقی بماند
            SetupState.MarkFailure("ساخت بانک ناموفق بود: " + (init.Error ?? ""));
            return Json(new { ok = false, message = HrT.F("اتصال برقرار شد اما ساخت بانک ناموفق بود: {0}", init.Error) });
        }

        // فاز ۸۳ب — حالت قفل مجوز همیشه Strict (فقط با مجوز) — انتخاب قبلی فرم حذف شد
        const string effLicenseMode = "Strict";
        // فاز ۷۶ — زبان رابط کاربری نصب: انتخاب صریح فرم؛ در نبود آن وضعیت فعلی حفظ می‌شود.
        // در hrsettings رمزشده نوشته می‌شود و CultureMiddleware از Ui:Language می‌خواند.
        var effUiLanguage = HrLang.Supported(uiLanguage) ? uiLanguage! : HrLang.FromConfig(_cfg);
        if (!string.Equals(effUiLanguage, HrLang.FromConfig(_cfg), StringComparison.Ordinal))
            _log.LogInformation("زبان پیش‌فرض نصب: {Lang}", effUiLanguage);

        await DbConfig.WriteSettingsFileAsync(_env, new Dictionary<string, object?>
        {
            ["Setup"] = new Dictionary<string, object?> { ["Configured"] = true },
            ["Database"] = new Dictionary<string, object?>
            {
                ["Provider"] = isSqlite ? "Sqlite" : "SqlServer",
                ["ConnectionString"] = cs,
            },
            ["SeedDemo"] = seedDemo,
            ["Ui"] = new Dictionary<string, object?>
            {
                ["Language"] = effUiLanguage,
            },
            ["License"] = new Dictionary<string, object?>
            {
                ["Mode"] = effLicenseMode,
            },
        });
        _root?.Reload();
        SetupState.MarkReady(); // فاز ۴۸ — خروج از حالت تعمیر؛ گیت بلافاصله باز می‌شود
        _log.LogInformation("ویزارد راه‌اندازی: پیکربندی رمزشده در {File} ذخیره و اعمال شد (License:Mode={Mode} — فقط با مجوز)", SetupSecrets.EncryptedFile, effLicenseMode);

        var okMsg = Repair
            ? "اتصال اصلاح شد — سامانه آماده ورود است"
            : "راه‌اندازی اولیه کامل شد — سامانه آماده ورود است";
        okMsg += " — " + licenseMsg;

        return Json(new
        {
            ok = true,
            message = okMsg,
            redirect = "/Auth/Login",
        });
    }

    // ==================================================================
    // فاز ۸۱/۸۲ — فقط «ساخت جفت‌کلید امضای مجوز» در همین ویزارد (درخواست کاربر):
    // «قسمتی که در setup امکان ایجاد مجوز رو میده حذف کن! مجوز فقط باید توسط
    // LicenseStudio ساخته بشه! ولی امکان ایجاد جفت کلید در راه اندازی اولیه
    // وجود داشته باشه». صدور/فعال‌سازی مجوز از ویزارد حذف شد — مسیر مجوز فقط:
    // LicenseStudio (امضا با private.pem) → صفحهٔ «مجوز نصب» سرور.
    // ------------------------------------------------------------------
    // نکتهٔ امنیتی: این اکشن با گارد SetupNeeded/Repair محصور است — پس از
    // تکمیل نصب، این مسیر بسته می‌شود و مدیریت کلید فقط از صفحهٔ /License
    // با نشست مدیر ارشد ممکن است.
    // ==================================================================

    /// <summary>ساخت جفت‌کلید امضای مجوز در همین نصب — کلید خصوصی در پاسخ برمی‌گردد
    /// تا مدیر همان لحظه فایل private.pem را دانلود و در محل امن نگه دارد؛
    /// مجوز بعداً فقط با LicenseStudio (یا LicenseGen) با همین کلید امضا و ثبت می‌شود.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("GenerateKeyPair")]
    public IActionResult GenerateKeyPair()
    {
        if (!SetupNeeded && !Repair)
            return Json(new { ok = false, message = HrT.T("سامانه از قبل پیکربندی شده است") });
        try
        {
            var (_, fpr, priv) = _license.GenerateSigningKey();
            _log.LogInformation("ویزارد راه‌اندازی: جفت‌کلید امضای مجوز ساخته شد — {Fpr}", fpr);
            return Json(new
            {
                ok = true,
                fingerprint = fpr,
                privatePem = priv,
                message = HrT.F("جفت‌کلید ساخته شد — اثر انگشت کلید عمومی: {0}", fpr),
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ساخت جفت‌کلید در ویزارد راه‌اندازی ناموفق بود");
            return Json(new { ok = false, message = HrT.F("ساخت جفت‌کلید ناموفق بود — {0}", ex.Message) });
        }
    }

    // ==================================================================
    // فاز ۹۰ — ورود کلید خصوصی ساخته‌شده در جای دیگر (درخواست کاربر:
    // «چطور از private.pem که در جای دیگه ساختم استفاده کنم؟ امکان آپلود بذار»)
    // مدیر فایل private.pem (از LicenseStudio/LicenseGen/نصب دیگر) را انتخاب
    // می‌کند؛ کلید عمومی از دل آن استخراج و مرجع پذیرش امضای همین نصب می‌شود.
    // ==================================================================

    /// <summary>ورود کلید خصوصی PEM در ویزارد راه‌اندازی — همان ذخیره‌سازی ساخت جفت‌کلید،
    /// با این تفاوت که جفت‌کلید از فایل ارسالی استخراج می‌شود (نه تولید جدید).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("ImportKeyPair")]
    public IActionResult ImportKeyPair(string? privatePem)
    {
        if (!SetupNeeded && !Repair)
            return Json(new { ok = false, message = HrT.T("سامانه از قبل پیکربندی شده است") });
        var (ok, fpr, msg) = _license.ImportSigningKey(privatePem ?? "");
        if (ok) _log.LogInformation("ویزارد راه‌اندازی: کلید خصوصی بیرونی وارد شد — {Fpr}", fpr);
        return Json(new { ok, fingerprint = fpr, message = HrT.T(msg) });
    }
}
