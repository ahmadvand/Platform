using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Platform.Web.Controllers;

/// <summary>
/// تنظیمات سامانه (فاز ۳۱) — اطلاعات شرکت نصب‌شده + لوگو + پالت رنگ سراسری + درباره (نسخه/تولیدکننده).
/// فاز ۴۵ — پیکربندی بانک داده (همان موتور ویزارد /Setup) هم به این صفحه اضافه شد.
/// مشاهده: settings.view — ویرایش: settings.manage (فقط مدیر ارشد/دارندگان دسترسی).
/// </summary>
[HasPerm("settings.view")]
public class SettingsController(AppDbContext db, HrBaseDbContext baseDb, ICurrentUser me, IWebHostEnvironment env, IConfiguration cfg,
    Platform.Web.Modules.ModuleStateService mods, HrLangStore langStore)
    : MvcBase(db, me)
{
    private readonly IWebHostEnvironment _env = env;
    // فاز ۶۱ — سرویس تنظیمات روی کانتکست پایه کار می‌کند (پارامتر primary-constructor)
    private readonly HrBaseDbContext Base = baseDb;
    private readonly IConfiguration _cfg = cfg;
    private readonly Platform.Web.Modules.ModuleStateService _mods = mods;
    private readonly HrLangStore _langStore = langStore;

    public async Task<IActionResult> Index()
    {
        var st = new SettingsService(Base);
        ViewBag.S = await st.GetSafeAsync();
        // فاز ۸۳ — زبان پیش‌فرض مؤثر: انتخاب ادمین (DB) وگرنه زبان نصب اولیه (پیکربندی)
        ViewBag.DefaultLang = string.IsNullOrWhiteSpace(ViewBag.S.DefaultLang)
            ? HrLang.FromConfig(_cfg)
            : ViewBag.S.DefaultLang;
        ViewBag.Presets = SettingsService.PalettePresets;
        ViewBag.CanManage = Sess?.Has("settings.manage") ?? false;
        ViewBag.Palettes = await Db.ColorPalettes.AsNoTracking()
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync();
        ViewBag.ActivePaletteId = ViewBag.S.ActivePaletteId;
        // فاز ۴۵ — وضعیت پیکربندی بانک داده برای کارت «پایگاه داده»
        var (dbProvider, dbCs) = DbConfig.Resolve(_cfg);
        ViewBag.DbProvider = dbProvider;
        ViewBag.DbKind = DbConfig.IsSqlite(dbProvider) ? "SQLite" : "SQL Server";
        ViewBag.DbServer = DbConfig.IsSqlite(dbProvider) ? DbConfig.ParseSqlite(dbCs) : (DbConfig.ParseSqlServer(dbCs)?.Server ?? "—");
        ViewBag.DbName = DbConfig.IsSqlite(dbProvider) ? "—" : (DbConfig.ParseSqlServer(dbCs)?.Database ?? "—");
        // فاز ۹۴ — اطلاعات «واقعی» اتصالِ جاری: خواندن زنده از نصب فعال (پروایدر، فایل، حجم،
        // جدول‌ها و ردیف‌ها) برای نمایش در تب «پایگاه داده» — به‌جای مقادیر فرمِ پیش‌فرض.
        ViewBag.DbInfo = await GatherDbStatusAsync(dbProvider, dbCs);
        // فاز ۵۴ — وضعیت ماژول‌ها برای کارت «ماژول‌های سامانه»
        ViewBag.Modules = Platform.Web.Modules.ModuleCatalog.All
            .Select(m => new
            {
                m.Code, m.Title, m.Icon, m.IsCore, m.Description,
                Enabled = _mods.Enabled(m.Code),
                LicenseLocked = !m.IsCore && !_mods.LicenseAllows(m.Code),
                // فاز ۵۷ — نصب‌نشده یعنی DLL ماژول در این بسته نیست (بیلد مشتری بدون آن)
                Installed = Platform.Web.Modules.HrBuildModules.Installed(m.Code),
            })
            .ToList();
        // فاز ۱۰۵ — بسته‌های محافظ صفحه (لیست تب «محافظ صفحه»؛ فعال اول، بعد تازه‌ترین‌ها)
        ViewBag.SsPackages = await Db.SsPackages.AsNoTracking()
            .OrderByDescending(p => p.IsActive).ThenByDescending(p => p.ImportedAt)
            .ToListAsync();
        return View();
    }

    // ---------- فاز ۹۴ — اطلاعات واقعی اتصال بانک داده (تب «پایگاه داده») ----------

    /// <summary>مدل نمایش اطلاعات زندهٔ بانک — همهٔ مقادیر از نصب فعال خوانده می‌شوند، نه از فرم.</summary>
    public sealed class DbStatus
    {
        public string ProviderFa = "";        // SQLite (فایلی) / SQL Server (سازمانی)
        public string Source = "";            // منبع پیکربندی: hrsettings.bin یا appsettings.json
        public string CsMasked = "";          // رشتهٔ اتصال با پوشاندن رمزها
        public bool Connected;                // اتصال زنده برقرار است؟
        public string ConnectError = "";      // خطای اتصال (در صورت وجود)
        public string EngineVersion = "";     // نسخهٔ موتور بانک
        // SQLite
        public string FilePath = "";
        public string FileSize = "";
        public string FileModified = "";
        public string JournalMode = "";
        public string PagesInfo = "";         // تعداد صفحه × اندازه صفحه
        public string FreePages = "";         // صفحات آزاد (فضای بازیافت‌نیافته)
        // SQL Server
        public string Server = "";
        public string DatabaseName = "";
        public string DbSize = "";
        // مشترک
        public int TableCount;
        public long TotalRows;
        public List<KeyValuePair<string, long>> TopTables = new();   // پررکوردترین جدول‌ها
    }

    private static string SzBytes(long b) => b >= 1 << 20
        ? HrT.F("{0} مگابایت", (b / (1024m * 1024m)).ToString("0.#"))
        : HrT.F("{0} کیلوبایت", Math.Max(1m, b / 1024m).ToString("0.#"));

    private static string Scalar(System.Data.Common.DbCommand cmd, string sql)
    {
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    /// <summary>خواندن زندهٔ اطلاعات اتصال جاری — هرگز استثنا نمی‌دهد؛ خطا در ConnectError برمی‌گردد.</summary>
    private async Task<DbStatus> GatherDbStatusAsync(string provider, string? cs)
    {
        var isSqlite = DbConfig.IsSqlite(provider);
        var s = new DbStatus
        {
            ProviderFa = isSqlite ? "SQLite (فایلی)" : "SQL Server (سازمانی)",
            // منبع پیکربندی: فایل رمزشدهٔ تنظیمات نصب در اولویت است (فاز ۵۱)؛ وگرنه appsettings.json
            Source = System.IO.File.Exists(Path.Combine(_env.ContentRootPath, "hrsettings.bin"))
                ? "فایل رمزشدهٔ تنظیمات نصب (hrsettings.bin)"
                : "فایل پیکربندی (appsettings.json)",
            CsMasked = DbConfig.MaskConnectionString(cs),
        };
        if (string.IsNullOrWhiteSpace(cs))
        {
            s.ConnectError = "رشتهٔ اتصال پیکربندی نشده است (ویزارد راه‌اندازی)";
            return s;
        }
        var conn = Db.Database.GetDbConnection();
        try
        {
            await conn.OpenAsync();
            s.Connected = true;
            using var cmd = conn.CreateCommand();
            if (isSqlite)
            {
                // فایل بانک — مسیر مطلق واقعی
                var path = DbConfig.ParseSqlite(cs);
                if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));
                s.FilePath = path;
                if (System.IO.File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    s.FileSize = SzBytes(fi.Length);
                    s.FileModified = Jalali.FmtLong(fi.LastWriteTime);
                }
                else s.FileSize = "فایل یافت نشد";
                s.EngineVersion = "SQLite " + Scalar(cmd, "SELECT sqlite_version()");
                s.JournalMode = Scalar(cmd, "PRAGMA journal_mode");
                var pageSize = long.TryParse(Scalar(cmd, "PRAGMA page_size"), out var ps) ? ps : 0;
                var pageCount = long.TryParse(Scalar(cmd, "PRAGMA page_count"), out var pc) ? pc : 0;
                var freePages = long.TryParse(Scalar(cmd, "PRAGMA freelist_count"), out var fc) ? fc : 0;
                s.PagesInfo = HrT.F("{0} صفحه × {1} بایت", HrT.Digits(pageCount.ToString()), HrT.Digits(pageSize.ToString()));
                s.FreePages = HrT.Digits(freePages.ToString());
                // جدول‌ها + تعداد ردیف واقعی هرکدام
                var tables = new List<string>();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync()) tables.Add(r.GetString(0));
                s.TableCount = tables.Count;
                var rows = new List<KeyValuePair<string, long>>();
                long total = 0;
                foreach (var t in tables)
                {
                    try
                    {
                        var v = Scalar(cmd, $"SELECT COUNT(*) FROM \"{t.Replace("\"", "\"\"")}\"");
                        if (long.TryParse(v, out var n)) { total += n; rows.Add(new(t, n)); }
                    }
                    catch { /* جدول مجازی و… — نادیده */ }
                }
                s.TotalRows = total;
                s.TopTables = rows.OrderByDescending(x => x.Value).Take(12).ToList();
            }
            else
            {
                var p = DbConfig.ParseSqlServer(cs);
                s.Server = p?.Server ?? "—";
                s.DatabaseName = p?.Database ?? "—";
                s.EngineVersion = "SQL Server — " + Scalar(cmd, "SELECT @@VERSION").Split('\n')[0].Trim();
                try
                {
                    var mb = Scalar(cmd, "SELECT CAST(SUM(size)*8/1024 AS INT) FROM sys.database_files WHERE type IN (0, 2)");
                    s.DbSize = HrT.F("{0} مگابایت", HrT.Digits(mb));
                }
                catch { }
                var rows = new List<KeyValuePair<string, long>>();
                cmd.CommandText = @"SELECT t.name, SUM(p.rows) FROM sys.tables t
                                    JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
                                    GROUP BY t.name ORDER BY t.name";
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        rows.Add(new(r.GetString(0), r.IsDBNull(1) ? 0 : r.GetInt64(1)));
                s.TableCount = rows.Count;
                s.TotalRows = rows.Sum(x => x.Value);
                s.TopTables = rows.OrderByDescending(x => x.Value).Take(12).ToList();
            }
        }
        catch (Exception ex)
        {
            s.ConnectError = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            try { if (conn.State == System.Data.ConnectionState.Open) conn.Close(); } catch { }
        }
        return s;
    }

    // ---------- ماژول‌های سامانه (فاز ۵۴) ----------

    /// <summary>فعال/غیرفعال‌کردن ماژول — گاردها در ModuleStateService (پایه خاموش‌نشدنی + سقف مجوز)؛
    /// تغییر بلافاصله در منو/مسیرها اعمال می‌شود و در ممیزی ثبت می‌گردد. داده‌های بانک دست‌نخورده می‌مانند.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> ToggleModule(string? code, bool enabled)
    {
        var res = _mods.SetEnabled(code, enabled);
        if (res.Ok)
            await Audit(enabled ? "module_enable" : "module_disable", "Module", code,
                new { code, enabled });
        return AjaxResult(res.Ok, res.Message);
    }

    // ---------- پایگاه داده (فاز ۴۵) — همان موتور ویزارد /Setup ----------

    private static DbConfig.SqlParams DbParams(string? provider, string? server, string? port, string? database,
        string? authMode, string? user, string? password, bool ssl, bool createIfMissing, string? dbPath) => new()
    {
        Server = server ?? "", Port = port ?? "1433",
        Database = string.IsNullOrWhiteSpace(database) ? "HrAttendance" : database!,
        WindowsAuth = authMode == "windows", User = user ?? "", Password = password ?? "",
        Ssl = ssl, CreateIfMissing = createIfMissing,
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> TestDatabase(string? provider, string? server, string? port, string? database,
        string? authMode, string? user, string? password, bool ssl = false, bool createIfMissing = true, string? dbPath = null)
    {
        if (DbConfig.IsSqlite(provider ?? ""))
        {
            var (ok, msg, _) = DbConfig.TestSqlite(dbPath ?? "hr.db", _env.ContentRootPath);
            return AjaxResult(ok, msg);
        }
        var res = await DbConfig.TestSqlServerAsync(DbParams(provider, server, port, database, authMode, user, password, ssl, createIfMissing, dbPath));
        return AjaxResult(res.Ok, res.Message);
    }

    /// <summary>تغییر اتصال بانک پس از نصب — تست → ساخت/خودترمیم بانک جدید → ذخیره → اعمال فوری بدون ری‌استارت.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> SaveDatabase(string? provider, string? server, string? port, string? database,
        string? authMode, string? user, string? password, bool ssl = false, bool createIfMissing = true,
        string? dbPath = null, bool seedDemo = true)
    {
        var isSqlite = DbConfig.IsSqlite(provider ?? "");
        string cs;
        if (isSqlite)
        {
            cs = DbConfig.BuildSqlite(dbPath ?? "hr.db");
        }
        else
        {
            var test = await DbConfig.TestSqlServerAsync(DbParams(provider, server, port, database, authMode, user, password, ssl, createIfMissing, dbPath));
            if (!test.Ok) return AjaxResult(false, test.Message);
            cs = DbConfig.BuildSqlServer(DbParams(provider, server, port, database, authMode, user, password, ssl, createIfMissing, dbPath));
        }

        var init = await DbConfig.InitNewDatabaseAsync(isSqlite ? "Sqlite" : "SqlServer", cs, seedDemo, _env); // فاز ۸۸ — محیط واقعی میزبان برای پروایدر مستقل
        if (!init.Ok)
            return AjaxResult(false, HrT.F("اتصال برقرار شد اما آماده‌سازی بانک ناموفق بود: {0}", init.Error));

        await DbConfig.WriteSettingsFileAsync(_env, new Dictionary<string, object?>
        {
            ["Setup"] = new Dictionary<string, object?> { ["Configured"] = true },
            ["Database"] = new Dictionary<string, object?>
            {
                ["Provider"] = isSqlite ? "Sqlite" : "SqlServer",
                ["ConnectionString"] = cs,
            },
            ["SeedDemo"] = seedDemo,
        });
        (_cfg as IConfigurationRoot)?.Reload();
        SetupState.MarkReady(); // فاز ۴۸ — اتصال جدید سالم است؛ حالت تعمیر (در صورت وجود) برداشته می‌شود
        await Audit("save_database_config", "hrsettings", null, new { provider = isSqlite ? "Sqlite" : "SqlServer" });
        return AjaxResult(true, "پیکربندی بانک داده ذخیره و بلافاصله اعمال شد (بدون نیاز به ری‌استارت)");
    }

    // ---------- اطلاعات شرکت ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> SaveCompany(string companyName, string? companyLegalName,
        string? companyNationalId, string? companyPhone, string? companyEmail,
        string? companyWebsite, string? companyAddress, string? description)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            ToastErr("نام شرکت الزامی است");
            if (Ajax) return AjaxResult(false, "نام شرکت الزامی است");
            return RedirectToAction(nameof(Index));
        }

        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.CompanyName = companyName.Trim();
        s.CompanyLegalName = NullIfEmpty(companyLegalName);
        s.CompanyNationalId = NullIfEmpty(companyNationalId);
        s.CompanyPhone = NullIfEmpty(companyPhone);
        s.CompanyEmail = NullIfEmpty(companyEmail);
        s.CompanyWebsite = NullIfEmpty(companyWebsite);
        s.CompanyAddress = NullIfEmpty(companyAddress);
        s.Description = NullIfEmpty(description);
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        // فاز ۸۳ب — ردیف تنظیمات با کانتکست Base ردیابی می‌شود (SettingsService(Base))؛
        // ذخیره روی Db عملاً هیچ‌چیز نمی‌نوشت (باگ سکوت: پیام موفق، بدون اثر ماندگار).
        await Base.SaveChangesAsync();
        await Audit("edit_settings_company", "AppSettings", s.Id, new { s.CompanyName });
        ToastOk("اطلاعات شرکت ذخیره شد و در سراسر سامانه اعمال شد");
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "ذخیره شد");
        return RedirectToAction(nameof(Index));
    }

    // ---------- زبان پیش‌فرض سامانه (فاز ۸۳) ----------

    /// <summary>زبان پیش‌فرض سامانه — برای کاربران بدون انتخاب شخصی و صفحهٔ ورود؛
    /// روی زبان نصب اولیه (Ui:Language) اولویت دارد. خالی کردن = بازگشت به زبان نصب اولیه.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> SaveDefaultLang(string? lang)
    {
        lang = string.IsNullOrWhiteSpace(lang) ? null : lang.Trim();
        if (lang is not null && !HrLang.Supported(lang) && !_langStore.Supported(lang))
        {
            ToastErr("کد زبان پشتیبانی نمی‌شود");
            if (Ajax) return AjaxResult(false, "کد زبان پشتیبانی نمی‌شود");
            return RedirectToAction(nameof(Index));
        }
        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.DefaultLang = lang;
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();   // فاز ۸۳ب — ردیابی‌شده با Base (نه Db)
        await Audit("edit_settings_default_lang", "AppSettings", s.Id, new { DefaultLang = lang });
        ToastOk(lang is null
            ? "زبان پیش‌فرض به زبان نصب اولیه بازگشت"
            : "زبان پیش‌فرض سامانه ذخیره شد و برای کاربران بدون انتخاب شخصی اعمال می‌شود");
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "ذخیره شد");
        return RedirectToAction(nameof(Index));
    }

    // ---------- نوع تقویم نمایشی (فاز ۸۶) ----------

    /// <summary>نوع تقویم نمایشی تاریخ‌ها — شمسی (پیش‌فرض)، میلادی، قمری. فقط «نمایش» تاریخ‌ها
    /// در کل سامانه عوض می‌شود؛ داده‌ها و کلیدهای تاریخ دست‌نخورده می‌مانند.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> SaveCalendar(string? calendar)
    {
        var val = (calendar ?? "").Trim().ToLowerInvariant();
        if (val is not ("jalali" or "gregorian" or "hijri"))
        {
            ToastErr("نوع تقویم معتبر نیست");
            if (Ajax) return AjaxResult(false, "نوع تقویم معتبر نیست");
            return RedirectToAction(nameof(Index));
        }
        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.Calendar = val;
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();
        await Audit("edit_settings_calendar", "AppSettings", s.Id, new { Calendar = val });
        ToastOk(val switch
        {
            "gregorian" => "تقویم نمایشی به میلادی تغییر کرد و در کل سامانه اعمال شد",
            "hijri" => "تقویم نمایشی به قمری تغییر کرد و در کل سامانه اعمال شد",
            _ => "تقویم نمایشی به شمسی بازگشت و در کل سامانه اعمال شد",
        });
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "ذخیره شد");
        return RedirectToAction(nameof(Index));
    }

    // ---------- فرم واحد تنظیمات (فاز ۹۱ — درخواست کاربر: همهٔ تنظیمات در یک فرم، نه کارت‌های جدا) ----------
    // شرکت + لوگو + زبان پیش‌فرض + نوع تقویم + ارز — یک POST واحد؛ endpointهای قدیمی بالا برای سازگاری می‌مانند.

    /// <summary>ذخیرهٔ فرم واحد تنظیمات عمومی. لوگو اختیاری است (همان قواعد UploadLogo).
    /// ارز از کاتالوگ Money اعتبارسنجی می‌شود؛ نرخ تبدیل نمایشی آزاد ولی عددی (پیش‌فرض ۱).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> SaveGeneral(string companyName, string? companyLegalName,
        string? companyNationalId, string? companyPhone, string? companyEmail,
        string? companyWebsite, string? companyAddress, string? description,
        string? lang, string? calendar, string? currency, string? currencyRate, IFormFile? logoFile,
        int? hijriAdjust, bool ssEnabled, int? ssMinutes, int? ssSeconds, bool ssRequirePassword)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            ToastErr("نام شرکت الزامی است");
            if (Ajax) return AjaxResult(false, "نام شرکت الزامی است");
            return RedirectToAction(nameof(Index));
        }
        lang = string.IsNullOrWhiteSpace(lang) ? null : lang.Trim();
        if (lang is not null && !HrLang.Supported(lang) && !_langStore.Supported(lang))
        {
            ToastErr("کد زبان پشتیبانی نمی‌شود");
            if (Ajax) return AjaxResult(false, "کد زبان پشتیبانی نمی‌شود");
            return RedirectToAction(nameof(Index));
        }
        var cal = (calendar ?? "").Trim().ToLowerInvariant();
        if (cal is not ("jalali" or "gregorian" or "hijri"))
        {
            ToastErr("نوع تقویم معتبر نیست");
            if (Ajax) return AjaxResult(false, "نوع تقویم معتبر نیست");
            return RedirectToAction(nameof(Index));
        }
        var cur = (currency ?? "").Trim().ToUpperInvariant();
        if (Money.Find(cur) is null)
        {
            ToastErr("ارز انتخابی معتبر نیست");
            if (Ajax) return AjaxResult(false, "ارز انتخابی معتبر نیست");
            return RedirectToAction(nameof(Index));
        }
        var rate = (currencyRate ?? "").Trim().Replace(',', '.');
        if (rate.Length > 0 && (!decimal.TryParse(rate, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var r) || r <= 0 || r > 1_000_000))
        {
            ToastErr("نرخ تبدیل باید عددی بزرگ‌تر از صفر باشد");
            if (Ajax) return AjaxResult(false, "نرخ تبدیل باید عددی بزرگ‌تر از صفر باشد");
            return RedirectToAction(nameof(Index));
        }
        // فاز ۹۶ — جبران روز قمری (‎-۲..+۲) و تنظیمات محافظ صفحه (۱..۱۲۰ دقیقه)
        // فاز ۹۸ (درخواست کاربر) — ثانیه‌های بی‌حرکتی در کنار دقیقه‌ها (۰..۵۹)
        // فاز ۱۰۰ (درخواست کاربر: «حتی چند ثانیه هم بشود تنظیم کرد») — دقیقه می‌تواند ۰ باشد
        //   (۰ دقیقه + N ثانیه)؛ مجموعِ زیر ۵ ثانیه به ۵ ثانیه گرد می‌شود تا محافظ بلافاصله فعال نشود.
        var hAdj = Math.Clamp(hijriAdjust ?? -1, -2, 2);
        var ssMin = Math.Clamp(ssMinutes ?? 5, 0, 120);
        var ssSec = Math.Clamp(ssSeconds ?? 0, 0, 59);
        if (ssMin == 0 && ssSec < 5) ssSec = 5;

        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.CompanyName = companyName.Trim();
        s.CompanyLegalName = NullIfEmpty(companyLegalName);
        s.CompanyNationalId = NullIfEmpty(companyNationalId);
        s.CompanyPhone = NullIfEmpty(companyPhone);
        s.CompanyEmail = NullIfEmpty(companyEmail);
        s.CompanyWebsite = NullIfEmpty(companyWebsite);
        s.CompanyAddress = NullIfEmpty(companyAddress);
        s.Description = NullIfEmpty(description);
        s.DefaultLang = lang;
        s.Calendar = cal;
        s.HijriAdjust = hAdj;
        s.ScreenSaverEnabled = ssEnabled;
        s.ScreenSaverMinutes = ssMin;
        s.ScreenSaverSeconds = ssSec;
        s.ScreenSaverRequirePassword = ssRequirePassword;
        s.Currency = cur;
        s.CurrencyRate = rate.Length == 0 ? "1" : rate;
        // لوگوی اختیاری — همان قواعد UploadLogo (قالب/حجم)؛ خالی = بدون تغییر
        if (logoFile is { Length: > 0 })
        {
            if (logoFile.Length > MaxLogoBytes)
            {
                ToastErr("حجم لوگو باید کمتر از ۲ مگابایت باشد");
                if (Ajax) return AjaxResult(false, "حجم لوگو باید کمتر از ۲ مگابایت باشد");
                return RedirectToAction(nameof(Index));
            }
            var ext = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
            if (!LogoExtensions.Contains(ext))
            {
                ToastErr("فقط قالب‌های PNG / JPG / WEBP / SVG پذیرفته می‌شود");
                if (Ajax) return AjaxResult(false, "قالب فایل مجاز نیست");
                return RedirectToAction(nameof(Index));
            }
            var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsDir);
            foreach (var old in Directory.GetFiles(uploadsDir, "logo.*"))
                System.IO.File.Delete(old);
            var fileName = $"logo{ext}";
            await using (var stream = System.IO.File.Create(Path.Combine(uploadsDir, fileName)))
                await logoFile.CopyToAsync(stream);
            s.LogoPath = "/uploads/" + fileName;
        }
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();
        await Audit("edit_settings_general", "AppSettings", s.Id,
            new { s.CompanyName, s.DefaultLang, s.Calendar, s.HijriAdjust, s.Currency, s.CurrencyRate,
                  s.ScreenSaverEnabled, s.ScreenSaverMinutes, s.ScreenSaverSeconds, s.ScreenSaverRequirePassword, Logo = s.LogoPath });
        ToastOk("تنظیمات ذخیره شد و در کل سامانه اعمال شد");
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "ذخیره شد");
        return RedirectToAction(nameof(Index));
    }

    // ---------- فاز ۱۰۴ — برون‌بری/درون‌ریزی تنظیمات محافظ صفحه (درخواست کاربر) ----------
    // دکمه‌ها در تب «محافظ صفحه» همین صفحه‌اند؛ خروجی/ورودی یک فایل JSON خودتوصیف است
    // (format=hr-screensaver) تا روی نصب‌های دیگر هم قابل اعمال باشد.

    /// <summary>برون‌بری تنظیمات محافظ صفحه — دانلود فایل JSON با ۴ فیلدِ محافظ (وضعیت/دقیقه/ثانیه/رمز).</summary>
    [HttpGet]
    [HasPerm("settings.view")]
    public async Task<IActionResult> ExportScreensaver()
    {
        var st = new SettingsService(Base);
        var s = await st.GetSafeAsync();
        var pack = new
        {
            format = "hr-screensaver",
            version = 1,
            exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            exportedBy = Sess?.Username,
            settings = new
            {
                enabled = s.ScreenSaverEnabled,
                minutes = s.ScreenSaverMinutes,
                seconds = s.ScreenSaverSeconds,
                requirePassword = s.ScreenSaverRequirePassword,
            },
        };
        var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
        await Audit("export_screensaver_settings", "AppSettings", s.Id, null);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "hr-screensaver-settings.json");
    }

    /// <summary>درون‌ریزی تنظیمات محافظ صفحه از فایل برون‌بری‌شده — فقط همین ۴ فیلد را تغییر می‌دهد؛
    /// بقیهٔ تنظیمات سامانه دست‌نخورده می‌ماند. همان قواعد اعتبارسنجی SaveGeneral (کف ۵ ثانیه و …).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> ImportScreensaver(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return ImportFail(HrT.T("فایل تنظیمات محافظ صفحه را انتخاب کنید"));
        if (file.Length > 1024 * 1024)
            return ImportFail(HrT.T("حجم فایل تنظیمات نباید بیش از ۱ مگابایت باشد"));

        string json;
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            json = await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            return ImportFail(HrT.T("خواندن فایل ممکن نشد — ") + ex.Message);
        }

        bool enabled, requirePassword;
        int minutes, seconds;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object ||
                !r.TryGetProperty("format", out var fmt) || fmt.GetString() != "hr-screensaver")
                return ImportFail(HrT.T("این فایل بستهٔ تنظیمات محافظ صفحه نیست"));
            if (!r.TryGetProperty("settings", out var stEl) || stEl.ValueKind != JsonValueKind.Object)
                return ImportFail(HrT.T("بخش settings در فایل یافت نشد"));
            enabled = ReadBool(stEl, "enabled");
            requirePassword = ReadBool(stEl, "requirePassword");
            minutes = ReadInt(stEl, "minutes");
            seconds = ReadInt(stEl, "seconds");
        }
        catch (ImportFormatEx exf)
        {
            return ImportFail(HrT.T(exf.Message));   // پیام اختصاصی: مقادیر نامعتبر/ناقص
        }
        catch (Exception ex)
        {
            return ImportFail(HrT.T("قالب فایل معتبر نیست (JSON نامعتبر) — ") + ex.Message);
        }

        // همان قواعد SaveGeneral — بازهٔ مجاز + کف مجموع ۵ ثانیه (فاز ۱۰۰)
        minutes = Math.Clamp(minutes, 0, 120);
        seconds = Math.Clamp(seconds, 0, 59);
        if (minutes == 0 && seconds < 5) seconds = 5;

        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.ScreenSaverEnabled = enabled;
        s.ScreenSaverMinutes = minutes;
        s.ScreenSaverSeconds = seconds;
        s.ScreenSaverRequirePassword = requirePassword;
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();
        await Audit("import_screensaver_settings", "AppSettings", s.Id,
            new { s.ScreenSaverEnabled, s.ScreenSaverMinutes, s.ScreenSaverSeconds, s.ScreenSaverRequirePassword, File = file.FileName });
        var stateTxt = enabled ? HrT.T("روشن") : HrT.T("خاموش");
        var passTxt = requirePassword ? HrT.T("با رمز") : HrT.T("بدون رمز");
        var timeTxt = (minutes > 0 ? HrT.F("{0} دقیقه", HrT.Num(minutes)) : "")
                    + (minutes > 0 && seconds > 0 ? " " : "")
                    + (seconds > 0 ? HrT.F("{0} ثانیه", HrT.Num(seconds)) : "");
        ToastOk(HrT.F("تنظیمات محافظ صفحه درون‌ریزی شد — وضعیت: {0}، زمان: {1}، بیدارسازی: {2}", stateTxt, timeTxt, passTxt));
        return Redirect("/Settings#tab-screensaver");
    }

    private IActionResult ImportFail(string msg)
    {
        ToastErr(msg);
        return Redirect("/Settings#tab-screensaver");
    }

    /// <summary>خواندن مقدار بولین از بستهٔ درون‌ریزی — بولین JSON یا رشتهٔ true/false؛ غایب/نامعتبر = خطا.</summary>
    private static bool ReadBool(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) throw new ImportFormatEx();
        if (v.ValueKind == JsonValueKind.True) return true;
        if (v.ValueKind == JsonValueKind.False) return false;
        if (v.ValueKind == JsonValueKind.String &&
            bool.TryParse(v.GetString(), out var b)) return b;
        throw new ImportFormatEx();
    }

    /// <summary>خواندن مقدار عددی از بستهٔ درون‌ریزی — عدد JSON یا رشتهٔ رقم‌آسا؛ غایب/نامعتبر = خطا.</summary>
    private static int ReadInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) throw new ImportFormatEx();
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String &&
            int.TryParse(v.GetString(), out var n2)) return n2;
        throw new ImportFormatEx();
    }

    /// <summary>خطای قالب بستهٔ درون‌ریزی — با پیام اختصاصی «کلید نامعتبر» گرفته می‌شود.</summary>
    private sealed class ImportFormatEx : Exception
    {
        public ImportFormatEx() : base("مقادیر وضعیت/دقیقه/ثانیه/رمز در فایل نامعتبر یا ناقص است") { }
    }

    // ---------- فاز ۱۰۵ — بسته‌های محافظ صفحه: انتقال «خودِ اسکرین‌سیور» (درخواست کاربر) ----------
    // سناریو: «یکی یه اسکرین‌سیور نوشته و من می‌خوام اونو داشته باشم» — هر بسته = JS + CSSِ یک
    // محافظ بصری + متادیتا + تنظیمات همراهش، در یک فایل JSON. امنیت: کد غریبه هرگز در قالب
    // سامانه اجرا نمی‌شود؛ فقط در iframe سندباکس‌شده (بدون allow-same-origin) با CSP سفت
    // (connect-src 'none') از /Settings/ScreensaverRuntime — به کوکی/DOM/شبکهٔ سامانه دسترسی ندارد.

    /// <summary>الگوی فایل بستهٔ محافظ صفحه (hr-screensaver-package / spec 1).</summary>
    private sealed class SsBundle
    {
        public string Name = "", Author = "", Version = "", Description = "";
        public string Js = "", Css = "";
        public JsonElement? Settings;   // بخش اختیاری settings (۴ فیلد محافظ)
    }

    /// <summary>درون‌ریزی بستهٔ محافظ صفحه از فایل — فقط settings.manage + آنتی‌فرجری.
    /// اعتبارسنجی سفت: حجم ≤۱MB، format/spec، سقف نویسه‌ها، ردّ دنباله‌های بستنِ تگ
    /// اسکریپت/استایل و باز-کامنتِ HTML در کد (چون در Runtime داخل تگ جاسازی می‌شود)،
    /// و ثبت SHA-256 برای ممیزی. بستهٔ جدید خودکار «فعال» می‌شود؛ تنظیمات همراه بسته اختیاری است.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    [RequestSizeLimit(1_200_000)]
    public async Task<IActionResult> ImportScreensaverPackage(IFormFile? file, bool applySettings)
    {
        const long MaxBytes = 1024 * 1024; // ۱ مگابایت
        if (file is null || file.Length == 0)
            return PkgFail("فایلی انتخاب نشده است");
        if (file.Length > MaxBytes)
            return PkgFail("حجم فایل بسته باید کمتر از ۱ مگابایت باشد");

        byte[] raw;
        using (var ms = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(ms);
            raw = ms.ToArray();
        }
        string text;
        try { text = System.Text.Encoding.UTF8.GetString(raw); }
        catch { return PkgFail("فایل بسته معتبر نیست (UTF-8 نامعتبر)"); }

        SsBundle b;
        try { b = ParseSsBundle(text); }
        catch (SsFormatEx exf) { return PkgFail(exf.Message); }

        // تنظیمات همراه بسته (اختیاری) — همان قواعد SaveGeneral
        bool enabled = true, requirePassword = true; int minutes = 5, seconds = 0; bool hasSettings = false;
        if (b.Settings is { } stEl && applySettings)
        {
            try
            {
                enabled = ReadBool(stEl, "enabled");
                requirePassword = ReadBool(stEl, "requirePassword");
                minutes = Math.Clamp(ReadInt(stEl, "minutes"), 0, 120);
                seconds = Math.Clamp(ReadInt(stEl, "seconds"), 0, 59);
                if (minutes == 0 && seconds < 5) seconds = 5;
                hasSettings = true;
            }
            catch (ImportFormatEx)
            {
                return PkgFail("تنظیمات همراه بسته نامعتبر است — فیلدهای وضعیت/دقیقه/ثانیه/رمز را بررسی کنید");
            }
        }

        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw));
        var pkg = new SsPackage
        {
            Name = b.Name,
            Author = NullIfEmpty(b.Author),
            Version = NullIfEmpty(b.Version),
            Description = NullIfEmpty(b.Description),
            JsCode = b.Js,
            CssCode = b.Css,
            SettingsJson = b.Settings is { } se ? JsonSerializer.Serialize(se) : null,
            Sha256 = sha,
            SizeBytes = raw.LongLength,
            IsActive = true,                      // بستهٔ تازه = فعال
            ImportedAt = DateTime.Now,
            ImportedBy = Sess?.Username,
        };
        // تک‌فعال‌بودن: بسته‌های دیگر غیرفعال می‌شوند (بدون بستهٔ فعال = محافظ داخلی)
        foreach (var other in await Db.SsPackages.Where(p => p.IsActive).ToListAsync())
            other.IsActive = false;
        Db.SsPackages.Add(pkg);
        await Db.SaveChangesAsync();
        await Audit("import_screensaver_package", "SsPackage", pkg.Id.ToString(),
            new { pkg.Name, pkg.Author, pkg.Version, pkg.Sha256, pkg.SizeBytes, AppliedSettings = hasSettings });

        if (hasSettings)
        {
            var st = new SettingsService(Base);
            var s = await st.GetEditableAsync();
            s.ScreenSaverEnabled = enabled;
            s.ScreenSaverMinutes = minutes;
            s.ScreenSaverSeconds = seconds;
            s.ScreenSaverRequirePassword = requirePassword;
            s.UpdatedAt = DateTime.Now;
            s.UpdatedBy = Sess?.Username;
            await Base.SaveChangesAsync();
            await Audit("import_screensaver_settings", "AppSettings", s.Id,
                new { s.ScreenSaverEnabled, s.ScreenSaverMinutes, s.ScreenSaverSeconds, s.ScreenSaverRequirePassword, From = "package:" + pkg.Name });
            var stateTxt = enabled ? HrT.T("روشن") : HrT.T("خاموش");
            var passTxt = requirePassword ? HrT.T("با رمز") : HrT.T("بدون رمز");
            var timeTxt = (minutes > 0 ? HrT.F("{0} دقیقه", HrT.Num(minutes)) : "")
                        + (minutes > 0 && seconds > 0 ? " " : "")
                        + (seconds > 0 ? HrT.F("{0} ثانیه", HrT.Num(seconds)) : "");
            ToastOk(HrT.F("بستهٔ محافظ صفحه «{0}» درون‌ریزی و فعال شد — تنظیمات همراه بسته هم اعمال شد (وضعیت: {1}، زمان: {2}، بیدارسازی: {3})", pkg.Name, stateTxt, timeTxt, passTxt));
        }
        else
        {
            ToastOk(HrT.F("بستهٔ محافظ صفحه «{0}» درون‌ریزی و فعال شد — کد بسته فقط در لایهٔ سندباکس اجرا می‌شود", pkg.Name));
        }
        return Redirect("/Settings#tab-screensaver");
    }

    private IActionResult PkgFail(string msg)
    {
        ToastErr(msg);
        return Redirect("/Settings#tab-screensaver");
    }

    /// <summary>پارس و اعتبارسنجی سفت فایل بستهٔ محافظ صفحه — هر تخلف = SsFormatEx با پیام فارسی.</summary>
    private static SsBundle ParseSsBundle(string text)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch (Exception ex) { throw new SsFormatEx("فایل بسته معتبر نیست (JSON نامعتبر) — " + ex.Message); }
        using (doc)
        {
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object ||
                !r.TryGetProperty("format", out var fmt) || fmt.GetString() != "hr-screensaver-package")
                throw new SsFormatEx("این فایل بستهٔ محافظ صفحه نیست — format باید «hr-screensaver-package» باشد");
            if (!r.TryGetProperty("spec", out var spec) || spec.ValueKind != JsonValueKind.Number || spec.GetInt32() != 1)
                throw new SsFormatEx("نسخهٔ قالب بسته پشتیبانی نمی‌شود — spec باید 1 باشد");

            var b = new SsBundle();
            if (!r.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object)
                throw new SsFormatEx("بخش meta در بسته یافت نشد");
            b.Name = (meta.TryGetProperty("name", out var n) ? n.GetString() : null)?.Trim() ?? "";
            if (b.Name.Length == 0 || b.Name.Length > 100)
                throw new SsFormatEx("نام بسته الزامی است (حداکثر ۱۰۰ نویسه)");
            b.Author = (meta.TryGetProperty("author", out var a) ? a.GetString() : null)?.Trim() ?? "";
            b.Version = (meta.TryGetProperty("version", out var v) ? v.GetString() : null)?.Trim() ?? "";
            b.Description = (meta.TryGetProperty("description", out var d) ? d.GetString() : null)?.Trim() ?? "";
            if (b.Author.Length > 100 || b.Version.Length > 40 || b.Description.Length > 500)
                throw new SsFormatEx("یکی از فیلدهای meta از حد مجاز بلندتر است (نویسنده ۱۰۰ / نسخه ۴۰ / توضیح ۵۰۰ نویسه)");

            if (!r.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Object)
                throw new SsFormatEx("بخش code در بسته یافت نشد");
            b.Js = (code.TryGetProperty("js", out var js) ? js.GetString() : null) ?? "";
            b.Css = (code.TryGetProperty("css", out var css) ? css.GetString() : null) ?? "";
            if (b.Js.Trim().Length == 0)
                throw new SsFormatEx("کد جاوااسکریپت محافظ (code.js) در بسته خالی است");
            if (b.Js.Length > 200_000)
                throw new SsFormatEx("کد جاوااسکریپت بسته بلندتر از ۲۰۰ هزار نویسه است");
            if (b.Css.Length > 100_000)
                throw new SsFormatEx("CSS بسته بلندتر از ۱۰۰ هزار نویسه است");
            // گارد جاسازی: کد داخل تگ‌های <script>/<style> و HTML سندباکس جاسازی می‌شود؛
            // دنباله‌های بستن تگ و کامنت HTML در هر دو فیلد مطلقاً رد می‌شوند.
            foreach (var bad in new[] { "</script", "</style", "<!--" })
            {
                if (b.Js.Contains(bad, StringComparison.OrdinalIgnoreCase) ||
                    b.Css.Contains(bad, StringComparison.OrdinalIgnoreCase))
                    throw new SsFormatEx("کد بسته شامل دنبالهٔ غیرمجاز «" + bad + "» است — برای اجرای امن در سندباکس پذیرفته نمی‌شود");
            }
            if (r.TryGetProperty("settings", out var stEl) && stEl.ValueKind == JsonValueKind.Object)
                b.Settings = stEl.Clone();
            return b;
        }
    }

    private sealed class SsFormatEx : Exception
    {
        public SsFormatEx(string msg) : base(msg) { }
    }

    /// <summary>فعال‌سازی بستهٔ محافظ صفحه — id=0 یعنی بازگشت به محافظ داخلی (باران ماتریکس).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> ActivateScreensaverPackage(long id)
    {
        foreach (var other in await Db.SsPackages.Where(p => p.IsActive).ToListAsync())
            other.IsActive = false;
        string msg;
        if (id == 0)
        {
            await Db.SaveChangesAsync();
            await Audit("activate_screensaver_builtin", "SsPackage", "0", null);
            msg = HrT.T("محافظ داخلی (باران ماتریکس) فعال شد");
        }
        else
        {
            var pkg = await Db.SsPackages.FirstOrDefaultAsync(p => p.Id == id);
            if (pkg is null)
            {
                ToastErr("بستهٔ محافظ صفحه یافت نشد");
                return Redirect("/Settings#tab-screensaver");
            }
            pkg.IsActive = true;
            await Db.SaveChangesAsync();
            await Audit("activate_screensaver_package", "SsPackage", pkg.Id.ToString(), new { pkg.Name });
            msg = HrT.F("بستهٔ محافظ صفحه «{0}» فعال شد", pkg.Name);
        }
        ToastOk(msg);
        return Redirect("/Settings#tab-screensaver");
    }

    /// <summary>حذف بستهٔ محافظ صفحه — حذف بستهٔ فعال یعنی بازگشت خودکار به محافظ داخلی.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> DeleteScreensaverPackage(long id)
    {
        var pkg = await Db.SsPackages.FirstOrDefaultAsync(p => p.Id == id);
        if (pkg is null)
        {
            ToastErr("بستهٔ محافظ صفحه یافت نشد");
            return Redirect("/Settings#tab-screensaver");
        }
        Db.SsPackages.Remove(pkg);
        await Db.SaveChangesAsync();
        await Audit("delete_screensaver_package", "SsPackage", pkg.Id.ToString(), new { pkg.Name, WasActive = pkg.IsActive });
        ToastOk(HrT.F("بستهٔ محافظ صفحه «{0}» حذف شد", pkg.Name));
        return Redirect("/Settings#tab-screensaver");
    }

    /// <summary>برون‌ریزی بستهٔ محافظ صفحه — بدون id: برون‌ریزی «محافظ داخلی» به‌صورت بستهٔ
    /// قابل‌درون‌ریزی در هر نصب دیگر؛ با id: برون‌ریزی همان بستهٔ درون‌ریزی‌شده.</summary>
    [HttpGet]
    [HasPerm("settings.view")]
    public async Task<IActionResult> ExportScreensaverPackage(long? id)
    {
        var s = await new SettingsService(Base).GetSafeAsync();
        object meta; string js, css; JsonElement? settings = null;
        if (id is > 0)
        {
            var pkg = await Db.SsPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (pkg is null)
            {
                ToastErr("بستهٔ محافظ صفحه یافت نشد");
                return Redirect("/Settings#tab-screensaver");
            }
            meta = new { name = pkg.Name, author = pkg.Author ?? "", version = pkg.Version ?? "", description = pkg.Description ?? "" };
            js = pkg.JsCode; css = pkg.CssCode;
            if (!string.IsNullOrWhiteSpace(pkg.SettingsJson))
            {
                try { settings = JsonDocument.Parse(pkg.SettingsJson).RootElement.Clone(); }
                catch { settings = null; }
            }
            await Audit("export_screensaver_package", "SsPackage", pkg.Id.ToString(), new { pkg.Name, pkg.Sha256 });
        }
        else
        {
            var sysName = HrT.T("سامانه یکپارچه منابع انسانی");
            meta = new
            {
                name = HrT.T("باران ماتریکس (محافظ داخلی)"),
                author = sysName,
                version = Platform.Web.Services.BuildInfo.Version,
                description = HrT.T("محافظ صفحهٔ داخلی سامانه: باران ماتریکس با نویسه‌های زبان جاری، فونت وزیرمتن، تاریخ/ساعت ریز و برند شرکت — به‌صورت بستهٔ مستقل برای نصب‌های دیگر."),
            };
            js = await System.IO.File.ReadAllTextAsync(Path.Combine(_env.WebRootPath ?? "wwwroot", "js", "hr-ss-visual.js"));
            css = "html,body{margin:0;height:100%;overflow:hidden;background:#03070a}" +
                  "canvas{position:fixed;inset:0;width:100%;height:100%;display:block;opacity:.85}";
            await Audit("export_screensaver_package", "SsPackage", "builtin", null);
        }
        if (settings is null)
        {
            settings = JsonSerializer.SerializeToElement(new
            {
                enabled = s.ScreenSaverEnabled,
                minutes = s.ScreenSaverMinutes,
                seconds = s.ScreenSaverSeconds,
                requirePassword = s.ScreenSaverRequirePassword,
            });
        }
        var bundle = new
        {
            format = "hr-screensaver-package",
            spec = 1,
            exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            exportedBy = Sess?.Username,
            meta,
            settings,
            code = new { js, css },
        };
        var fileName = "hr-screensaver-package-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".json";
        return File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true })),
            "application/json", fileName);
    }

    /// <summary>سرو کد بستهٔ فعال برای iframe سندباکس‌شدهٔ محافظ صفحه.
    /// چندلایهٔ امنیتی: (۱) فقط کاربر واردشده با settings.view؛ (۲) iframe با
    /// sandbox="allow-scripts" بدون allow-same-origin — مبدأ کدر و بدون کوکی؛
    /// (۳) هدر CSP «sandbox allow-scripts» — حتی ناوبری مستقیم به همین آدرس هم سندباکس است؛
    /// (۴) متا CSP با connect-src 'none' — کد بسته هیچ درخواست شبکه‌ای نمی‌تواند بزند؛
    /// (۵) کد در درون‌ریزی از دنباله‌های بستنِ تگ اسکریپت/استایل و باز-کامنتِ HTML پالایش شده است.</summary>
    [HttpGet]
    public async Task<IActionResult> ScreensaverRuntime()
    {
        var pkg = await Db.SsPackages.AsNoTracking().FirstOrDefaultAsync(p => p.IsActive);
        if (pkg is null)
            return NotFound();
        var lang = (HttpContext.Request.Headers["X-Hr-Lang"].ToString());
        if (string.IsNullOrWhiteSpace(lang))
            lang = HrT.Cur;   // زبان جاری کاربر (کوکی/تنظیمات) — HrT.Cur همیشه کد معتبر می‌دهد
        lang = lang.Trim().ToLowerInvariant();
        var dir = lang is "fa" or "prs" or "ps" or "ar" ? "rtl" : "ltr";
        // فاز ۱۰۶ — تنظیمات همراه بسته (۴ فیلد محافظ + کلیدهای دلخواه بسته مثل speed/density)
        // به کد مهمان داده می‌شود تا رفتار بسته با تنظیمات کاربر هماهنگ شود؛ غایب بودن
        // settings سازگار کامل با بسته‌های قدیمی است (کلید اختیاری).
        object? guestSettings = null;
        if (!string.IsNullOrWhiteSpace(pkg.SettingsJson))
        {
            try { using var sdoc = System.Text.Json.JsonDocument.Parse(pkg.SettingsJson); guestSettings = sdoc.RootElement.Clone(); }
            catch { guestSettings = null; }   // settings خراب → مهمان پیش‌فرض‌های خودش را می‌گذارد
        }
        var dataJson = JsonSerializer.Serialize(new { lang, name = pkg.Name, settings = guestSettings });
        // فاز ۱۰۶ — تزریق فونت وزیرمتن در سطح میزبان: هر بسته‌ای حتی اگر خودش فونت embed نکرده
        // باشد، با ارجاع به 'Vazirmatn' در سندباکس وزیرِ واقعی می‌گیرد و اتصال حروف (شکل‌دهی
        // عربی/فارسی) با جداول OpenType فونت انجام می‌شود — ریشهٔ «مقطع بودن» نوشته‌ها همین
        // نبودن فونت در سندباکس بود. CSP همین صفحه font-src data: را مجاز می‌داند.
        var fontFace = string.Empty;
        try
        {
            var fdir = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "fonts");
            var reg = System.IO.File.ReadAllBytes(System.IO.Path.Combine(fdir, "Vazirmatn-Regular.woff2"));
            var bold = System.IO.File.ReadAllBytes(System.IO.Path.Combine(fdir, "Vazirmatn-Bold.woff2"));
            fontFace = "@font-face{font-family:'Vazirmatn';font-style:normal;font-weight:400;src:url(data:font/woff2;base64," + Convert.ToBase64String(reg) + ") format('woff2');font-display:block}"
                     + "@font-face{font-family:'Vazirmatn';font-style:normal;font-weight:700;src:url(data:font/woff2;base64," + Convert.ToBase64String(bold) + ") format('woff2');font-display:block}";
        }
        catch { /* فونت روی نصب موجود نیست — بسته‌های نسخهٔ ۲ به بعد فونت را درون خود دارند */ }
        var html = new System.Text.StringBuilder();
        html.Append("<!doctype html>\n<html lang=\"").Append(lang).Append("\" dir=\"").Append(dir).Append("\">\n<head>\n")
            .Append("<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n")
            .Append("<meta name=\"referrer\" content=\"no-referrer\">\n")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data:; font-src data:; connect-src 'none'; form-action 'none'; base-uri 'none'\">\n")
            .Append("<title>").Append(System.Text.Encodings.Web.HtmlEncoder.Default.Encode(pkg.Name)).Append("</title>\n")
            .Append("<style>html,body{margin:0;height:100%;overflow:hidden;background:#03070a}")
            .Append(fontFace)
            .Append(pkg.CssCode).Append("</style>\n</head>\n<body>\n")
            .Append("<script id=\"hr-ss-data\" type=\"application/json\">").Append(dataJson).Append("</script>\n")
            .Append("<script>").Append(pkg.JsCode).Append("</script>\n</body>\n</html>\n");
        Response.Headers["Content-Security-Policy"] = "sandbox allow-scripts";
        Response.Headers["X-Robots-Tag"] = "noindex";
        Response.Headers["Cache-Control"] = "no-store";
        return Content(html.ToString(), "text/html; charset=utf-8");
    }

    // ---------- لوگو ----------

    private static readonly string[] LogoExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
    private const long MaxLogoBytes = 2 * 1024 * 1024; // ۲ مگابایت

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> UploadLogo(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            ToastErr("فایلی انتخاب نشده است");
            if (Ajax) return AjaxResult(false, "فایلی انتخاب نشده است");
            return RedirectToAction(nameof(Index));
        }
        if (file.Length > MaxLogoBytes)
        {
            ToastErr("حجم لوگو باید کمتر از ۲ مگابایت باشد");
            if (Ajax) return AjaxResult(false, "حجم لوگو باید کمتر از ۲ مگابایت باشد");
            return RedirectToAction(nameof(Index));
        }
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!LogoExtensions.Contains(ext))
        {
            ToastErr("فقط قالب‌های PNG / JPG / WEBP / SVG پذیرفته می‌شود");
            if (Ajax) return AjaxResult(false, "قالب فایل مجاز نیست");
            return RedirectToAction(nameof(Index));
        }

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);

        // حذف لوگوی قبلی (هر قالبی که بوده)
        foreach (var old in Directory.GetFiles(uploadsDir, "logo.*"))
            System.IO.File.Delete(old);

        var fileName = $"logo{ext}";
        await using (var stream = System.IO.File.Create(Path.Combine(uploadsDir, fileName)))
            await file.CopyToAsync(stream);

        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.LogoPath = "/uploads/" + fileName;
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();   // فاز ۸۳ب — ردیابی‌شده با Base (نه Db)
        await Audit("upload_settings_logo", "AppSettings", s.Id, new { file.FileName, file.Length });
        ToastOk("لوگو بارگذاری شد و در سایدبار، صفحه ورود و عنوان مرورگر اعمال شد");
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "لوگو ثبت شد");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> ResetLogo()
    {
        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
        if (Directory.Exists(uploadsDir))
            foreach (var old in Directory.GetFiles(uploadsDir, "logo.*"))
                System.IO.File.Delete(old);

        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.LogoPath = null;
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();   // فاز ۸۳ب — ردیابی‌شده با Base (نه Db)
        await Audit("reset_settings_logo", "AppSettings", s.Id, null);
        ToastOk("لوگو حذف شد و نشان پیش‌فرض جایگزین شد");
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "انجام شد");
        return RedirectToAction(nameof(Index));
    }

    // ---------- پالت رنگ ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> SavePalette(string? accentColor, string? sidebarColor, string? reset)
    {
        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();

        if (reset == "1")
        {
            s.AccentColor = null;
            s.SidebarColor = null;
        }
        else
        {
            var accent = SettingsService.NormalizeHex(accentColor);
            var sidebar = SettingsService.NormalizeHex(sidebarColor);
            if (accentColor is not null && accent is null)
            {
                ToastErr("قالب رنگ اصلی معتبر نیست (مثل #6366f1)");
                if (Ajax) return AjaxResult(false, "رنگ اصلی معتبر نیست");
                return RedirectToAction(nameof(Index));
            }
            if (sidebarColor is not null && sidebar is null)
            {
                ToastErr("قالب رنگ سایدبار معتبر نیست");
                if (Ajax) return AjaxResult(false, "رنگ سایدبار معتبر نیست");
                return RedirectToAction(nameof(Index));
            }
            s.AccentColor = accent;
            s.SidebarColor = sidebar;
        }
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();   // فاز ۸۳ب — ردیابی‌شده با Base (نه Db)
        await Audit(reset == "1" ? "reset_settings_palette" : "edit_settings_palette", "AppSettings", s.Id,
            new { s.AccentColor, s.SidebarColor });
        ToastOk(reset == "1"
            ? "پالت به حالت پیش‌فرض بازگشت"
            : "پالت رنگ ذخیره شد و در کل سامانه اعمال شد");
        if (Ajax) return AjaxResult(true, (string?)TempData["ToastOk"] ?? "ذخیره شد");
        return RedirectToAction(nameof(Index));
    }

    // ---------- پالت رنگ استاندارد (فاز ۳۲) — کتابخانه پالت‌ها ----------

    /// <summary>فرم ایجاد/ویرایش پالت در پاپ‌آپ AJAX (partial بدون layout).</summary>
    [HasPerm("settings.manage")]
    public async Task<IActionResult> PaletteForm(string? id)
    {
        ColorPalette? p = null;
        if (id is not null)
        {
            p = await Db.ColorPalettes.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return NotFound();
            if (p.IsBuiltIn) p = new ColorPalette { Name = "کپی " + p.Name }; // کپی از روی پالت داخلی
        }
        return FormView("PaletteForm", p ?? new ColorPalette());
    }

    /// <summary>ایجاد/ویرایش پالت (id=0 → ایجاد) — ۱۶ نقش رنگی تخت.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> SavePaletteFull(string id, string name,
        string primaryColor, string? linkColor, string sidebarColor, string? sidebarTextColor,
        string pageBgColor, string? surfaceColor, string tableHeaderColor, string? rowAltColor,
        string? rowHoverColor, string? borderColor, string textColor, string? mutedTextColor,
        string? successColor, string? warningColor, string? dangerColor, string? infoColor)
    {
        if (string.IsNullOrWhiteSpace(name))
            return AjaxResult(false, "نام پالت الزامی است");
        name = name.Trim();
        if (name.Length > 100) name = name[..100];

        // نقش‌های الزامی باید معتبر باشند؛ نقش‌های اختیاری خالی از رنگ‌های پایه مشتق می‌شوند
        var pri = SettingsService.NormalizeHex(primaryColor);
        var sb = SettingsService.NormalizeHex(sidebarColor);
        var page = SettingsService.NormalizeHex(pageBgColor);
        var th = SettingsService.NormalizeHex(tableHeaderColor);
        var ink = SettingsService.NormalizeHex(textColor);
        if (pri is null || sb is null || page is null || th is null || ink is null)
            return AjaxResult(false, "رنگ‌های الزامی معتبر نیستند (قالب #rrggbb)");

        var p = string.IsNullOrEmpty(id) ? new ColorPalette() : await Db.ColorPalettes.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return AjaxResult(false, "پالت یافت نشد");
        if (p.IsBuiltIn) return AjaxResult(false, "پالت‌های داخلی قابل ویرایش نیستند — از دکمه کپی استفاده کنید");
        if (await Db.ColorPalettes.AnyAsync(x => x.Name == name && x.Id != id))
            return AjaxResult(false, "پالتی با این نام از قبل وجود دارد");

        p.Name = name;
        p.PrimaryColor = pri;
        p.LinkColor = SettingsService.NormalizeHex(linkColor) ?? SettingsService.Mix(pri, "#000000", 0.14);
        p.SidebarColor = sb;
        p.SidebarTextColor = SettingsService.NormalizeHex(sidebarTextColor) ?? SettingsService.Mix(sb, "#ffffff", 0.72);
        p.PageBgColor = page;
        p.SurfaceColor = SettingsService.NormalizeHex(surfaceColor) ?? "#ffffff";
        p.TableHeaderColor = th;
        p.RowAltColor = SettingsService.NormalizeHex(rowAltColor) ?? SettingsService.Mix(page, "#ffffff", 0.45);
        p.RowHoverColor = SettingsService.NormalizeHex(rowHoverColor) ?? SettingsService.Mix(th, "#ffffff", 0.35);
        p.BorderColor = SettingsService.NormalizeHex(borderColor) ?? MixLine(page);
        p.TextColor = ink;
        p.MutedTextColor = SettingsService.NormalizeHex(mutedTextColor) ?? SettingsService.Mix(ink, "#ffffff", 0.38);
        p.SuccessColor = SettingsService.NormalizeHex(successColor) ?? "#0e9f6e";
        p.WarningColor = SettingsService.NormalizeHex(warningColor) ?? "#b45309";
        p.DangerColor = SettingsService.NormalizeHex(dangerColor) ?? "#dc2648";
        p.InfoColor = SettingsService.NormalizeHex(infoColor) ?? "#0369a1";
        if (string.IsNullOrEmpty(id)) { p.IsBuiltIn = false; p.SortOrder = 10; Db.ColorPalettes.Add(p); }
        await Db.SaveChangesAsync();
        await Audit(string.IsNullOrEmpty(id) ? "create_palette" : "edit_palette", "ColorPalette", p.Id, new { p.Name });
        // اگر پالت ویرایش‌شده همان پالت فعال باشد، CSS سراسری هم همین حالا تازه شود
        var st = new SettingsService(Base);
        var cur = await st.GetSafeAsync();
        string? css = null;
        if (cur.ActivePaletteId == p.Id)
            css = SettingsService.BuildPaletteCss(await Db.ColorPalettes.AsNoTracking().FirstAsync(x => x.Id == p.Id));
        return AjaxResult(true, string.IsNullOrEmpty(id) ? HrT.T("پالت جدید ساخته شد — از کارت پالت، «اعمال» را بزنید") : HrT.T("پالت ویرایش شد"), css is null ? null : new { css });
    }

    private static string MixLine(string page) => SettingsService.Mix(page, "#000000", 0.06);

    /// <summary>اعمال پالت — کل نرم‌افزار (وب + ورود + PDF) بلافاصله از نقش‌های آن استفاده می‌کند.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> ApplyPalette(string id)
    {
        var p = await Db.ColorPalettes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null)
        {
            if (Ajax) return AjaxResult(false, "پالت یافت نشد");
            return RedirectToAction(nameof(Index));
        }
        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.ActivePaletteId = id;
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();   // فاز ۸۳ب — ردیابی‌شده با Base (نه Db)
        await Audit("apply_palette", "ColorPalette", id.ToString(), new { p.Name });
        var css = SettingsService.BuildPaletteCss(await Db.ColorPalettes.AsNoTracking().FirstAsync(x => x.Id == id));
        if (Ajax) return AjaxResult(true, HrT.F("پالت «{0}» روی کل سامانه اعمال شد", p.Name), new { css });
        TempData["ToastOk"] = $"پالت «{p.Name}» روی کل سامانه اعمال شد";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>جدا کردن پالت — بازگشت به رنگ‌های اصلی نرم‌افزار (بدون پالت).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> DetachPalette()
    {
        var st = new SettingsService(Base);
        var s = await st.GetEditableAsync();
        s.ActivePaletteId = null;
        s.UpdatedAt = DateTime.Now;
        s.UpdatedBy = Sess?.Username;
        await Base.SaveChangesAsync();   // فاز ۸۳ب — ردیابی‌شده با Base (نه Db)
        await Audit("detach_palette", "AppSettings", s.Id, null);
        if (Ajax) return AjaxResult(true, "پالت فعال کنار گذاشته شد — رنگ‌های اصلی نرم‌افزار بازگشت", new { css = "" });
        TempData["ToastOk"] = "پالت فعال کنار گذاشته شد — رنگ‌های اصلی نرم‌افزار بازگشت";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>حذف پالت سفارشی — پالت‌های داخلی حذف نمی‌شوند؛ پالت فعال باید اول کنار گذاشته شود.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("settings.manage")]
    public async Task<IActionResult> DeletePalette(string id)
    {
        var p = await Db.ColorPalettes.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null)
        {
            if (Ajax) return AjaxResult(false, "پالت یافت نشد");
            return RedirectToAction(nameof(Index));
        }
        if (p.IsBuiltIn)
            return AjaxResult(false, "پالت‌های داخلی نرم‌افزار حذف نمی‌شوند");
        var st = new SettingsService(Base);
        var s = await st.GetSafeAsync();
        if (s.ActivePaletteId == id)
            return AjaxResult(false, "این پالت فعال است — ابتدا پالت دیگری را اعمال یا «بدون پالت» را انتخاب کنید");
        Db.ColorPalettes.Remove(p);
        await Db.SaveChangesAsync();
        await Audit("delete_palette", "ColorPalette", id.ToString(), new { p.Name });
        return ToastDone(true, HrT.F("پالت «{0}» حذف شد", p.Name), "/Settings");
    }

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
