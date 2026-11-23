using Platform.Web.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Platform.Web.Infrastructure;

/// <summary>
/// فاز ۴۵ — حل‌وفصل پیکربندی بانک داده به‌صورت زمان اجرا.
/// منبع حقیقت: کلیدهای Database:Provider و Database:ConnectionString (فایل رمزشدهٔ hrsettings.bin —
/// فاز ۵۱، SetupSecrets — که ویزارد راه‌اندازی/صفحه تنظیمات می‌نویسد) و در غیاب آن‌ها، رشته‌های نام‌دار
/// ConnectionStrings در appsettings.json (سازگاری کامل با نصب‌های قبلی).
/// با این ساختار، تغییر اتصال پس از Reload IConfiguration بدون ری‌استارت برنامه اعمال می‌شود.
/// </summary>
public static class DbConfig
{

    public static bool IsSqlite(string provider) => provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

    /// <summary>خواندن وضعیت پیکربندی — فقط مقدار صریح false ویزارد را نشان می‌دهد؛
    /// کلید غایب (نصب‌های قدیمی که appsettings دستی دارند) = پیکربندی‌شده.</summary>
    public static bool IsConfigured(IConfiguration cfg)
    {
        var v = cfg["Setup:Configured"];
        return v is null || !v.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// فاز ۴۸ — آیا سامانه باید وارد ویزارد راه‌اندازی شود؟
    /// علاوه بر کلید Setup:Configured=false، اگر رشته اتصال مؤثر (hrsettings.bin یا
    /// ConnectionStrings در appsettings.json) خالی/غایب باشد هم «نیازمند راه‌اندازی» است؛
    /// در غیر این صورت استارتاپ با «The ConnectionString property has not been initialized»
    /// سقوط می‌کرد. نصب‌های سالم با رشته اتصال کامل: رفتار قبلی بدون تغییر.
    /// </summary>
    public static bool NeedsSetup(IConfiguration cfg)
    {
        // فاز ۸۳ — اگر فایل رمزشدهٔ hrsettings.bin در استارتاپ وجود داشت و حالا حذف شده،
        // سامانه باید به ویزارد راه‌اندازی برگردد (نه اینکه با پیکربندی کهنهٔ حافظه به
        // بانکی خاموش وصل شود و با SqlException صفحهٔ زرد بدهد).
        if (SetupSecrets.BinDeletedAfterStart) return true;
        if (!IsConfigured(cfg)) return true;
        var (_, cs) = Resolve(cfg);
        return string.IsNullOrWhiteSpace(cs);
    }

    public static (string Provider, string? ConnectionString) Resolve(IConfiguration cfg)
    {
        // فاز ۸۳ — پیش‌فرض موتور بانک Sqlite است (نصب تازه/کلید غایب)؛ SQL Server فقط با انتخاب صریح
        var provider = cfg["Database:Provider"] ?? "Sqlite";
        var cs = cfg["Database:ConnectionString"];
        if (string.IsNullOrWhiteSpace(cs))
            cs = cfg.GetConnectionString(IsSqlite(provider) ? "Sqlite" : "SqlServer");
        return (provider, string.IsNullOrWhiteSpace(cs) ? null : cs);
    }

    /// <summary>گزینه‌های DbContext بر پایه پیکربندی جاری — در هر Scope تازه ساخته می‌شود
    /// تا تغییر اتصال از طریق فرم‌ها بلافاصله (بدون ری‌استارت) اعمال شود.</summary>
    public static DbContextOptionsBuilder<AppDbContext> BuildOptions(IConfiguration cfg)
    {
        var (provider, cs) = Resolve(cfg);
        var ob = new DbContextOptionsBuilder<AppDbContext>();
        if (IsSqlite(provider)) ob.UseSqlite(cs);
        else ob.UseSqlServer(cs);
        return ob;
    }

    /// <summary>فاز ۶۱ — همان گزینه‌ها برای کانتکست‌های دیگر (پایه/ماژول‌ها) روی همان اتصال.
    /// تغییر اتصال از طریق ویزارد/تنظیمات بلافاصله روی همهٔ کانتکست‌ها اعمال می‌شود.</summary>
    public static DbContextOptionsBuilder<TCtx> BuildOptionsFor<TCtx>(IConfiguration cfg) where TCtx : DbContext
    {
        var (provider, cs) = Resolve(cfg);
        var ob = new DbContextOptionsBuilder<TCtx>();
        if (IsSqlite(provider)) ob.UseSqlite(cs);
        else ob.UseSqlServer(cs);
        return ob;
    }

    // ---------- ساخت رشته اتصال از پارامترهای فرم ----------

    public sealed class SqlParams
    {
        public string Server { get; set; } = "";
        public string Port { get; set; } = "1433";
        public string Database { get; set; } = "HrAttendance";
        public bool WindowsAuth { get; set; }
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public bool Ssl { get; set; }
        public bool CreateIfMissing { get; set; } = true;
    }

    public static string BuildSqlServer(SqlParams p)
    {
        var sb = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(p.Port) ? p.Server.Trim() : $"{p.Server.Trim()},{p.Port.Trim()}",
            InitialCatalog = p.Database.Trim(),
            MultipleActiveResultSets = true,
            Encrypt = p.Ssl,
            TrustServerCertificate = true,
            ConnectTimeout = 8,
        };
        if (p.WindowsAuth) sb.IntegratedSecurity = true;
        else { sb.UserID = p.User.Trim(); sb.Password = p.Password ?? ""; }
        return sb.ConnectionString;
    }

    public static string BuildSqlite(string path)
    {
        var b = new SqliteConnectionStringBuilder { DataSource = string.IsNullOrWhiteSpace(path) ? "hr.db" : path.Trim() };
        return b.ToString();
    }

    /// <summary>تست اتصال SQL Server + ساخت بانک در صورت نیاز — پیام فارسی برمی‌گرداند.</summary>
    public static async Task<(bool Ok, string Message)> TestSqlServerAsync(SqlParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Server)) return (false, "آدرس سرور را وارد کنید");
        if (!p.WindowsAuth && string.IsNullOrWhiteSpace(p.User)) return (false, "نام کاربری SQL را وارد کنید");
        try
        {
            var masterCs = new SqlConnectionStringBuilder(BuildSqlServer(p)) { InitialCatalog = "master" }.ConnectionString;
            await using var con = new SqlConnection(masterCs);
            await con.OpenAsync();
            var dbName = p.Database.Trim();
            bool exists;
            await using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @n";
                cmd.Parameters.AddWithValue("@n", dbName);
                exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            }
            if (!exists && p.CreateIfMissing)
            {
                await using var mk = con.CreateCommand();
                mk.CommandText = $"CREATE DATABASE [{dbName.Replace("]", "]]")}]";
                await mk.ExecuteNonQueryAsync();
                return (true, $"اتصال موفق بود و بانک «{dbName}» ساخته شد ✓");
            }
            if (!exists)
                return (true, $"اتصال به سرور موفق بود؛ بانک «{dbName}» هنوز وجود ندارد (در زمان ذخیره ساخته می‌شود)");
            return (true, $"اتصال به بانک «{dbName}» موفق بود ✓");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return (false, "اتصال ناموفق: " + msg);
        }
    }

    /// <summary>تست مسیر SQLite + ساخت پوشه — پیام فارسی برمی‌گرداند.</summary>
    public static (bool Ok, string Message, string ResolvedPath) TestSqlite(string path, string contentRoot)
    {
        var cs = BuildSqlite(path);
        var file = new SqliteConnectionStringBuilder(cs).DataSource;
        if (!Path.IsPathRooted(file)) file = Path.GetFullPath(Path.Combine(contentRoot, file));
        try
        {
            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return (true, $"مسیر بانک معتبر است: {file}", file);
        }
        catch (Exception ex)
        {
            return (false, "مسیر بانک معتبر نیست: " + ex.Message, file);
        }
    }

    /// <summary>
    /// فاز ۵۱ — نوشتن تنظیمات نصب به‌صورت «رمزشده» در hrsettings.bin (Data Protection —
    /// SetupSecrets)؛ نسخهٔ متنی hrsettings.json در صورت وجود، خودکار حذف می‌شود.
    /// فقط کلیدهای پیکربندی نصب — بدون دست‌زدن به بقیه.
    /// </summary>
    public static async Task WriteSettingsFileAsync(IWebHostEnvironment env, Dictionary<string, object?> data)
    {
        await SetupSecrets.WriteAsync(env.ContentRootPath, data);
    }

    /// <summary>استخراج سرور/نام بانک از رشته اتصال SQL Server (برای نمایش در تنظیمات) — خطا = null.</summary>
    public static (string Server, string Database)? ParseSqlServer(string? cs)
    {
        try
        {
            var sb = new SqlConnectionStringBuilder(cs);
            return (sb.DataSource, string.IsNullOrWhiteSpace(sb.InitialCatalog) ? "—" : sb.InitialCatalog);
        }
        catch { return null; }
    }

    /// <summary>استخراج مسیر فایل از رشته اتصال SQLite.</summary>
    public static string ParseSqlite(string? cs)
    {
        try { return new SqliteConnectionStringBuilder(cs).DataSource; }
        catch { return "—"; }
    }

    /// <summary>فاز ۹۴ — رشتهٔ اتصال «امن برای نمایش» در تب پایگاه دادهٔ تنظیمات:
    /// رمزها (Password/Pwd) با ●●●● جایگزین می‌شوند و بقیهٔ پارامترها واقعی نمایش داده می‌شوند.</summary>
    public static string MaskConnectionString(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return "—";
        const string mask = "●●●●●●●●";
        try
        {
            if (IsSqlite(cs) || cs.Contains("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new SqliteConnectionStringBuilder(cs);
                if (!string.IsNullOrEmpty(sb.Password)) sb.Password = mask;
                return sb.ToString();
            }
            var msb = new SqlConnectionStringBuilder(cs);
            if (!string.IsNullOrEmpty(msb.Password)) msb.Password = mask;
            return msb.ConnectionString;
        }
        catch
        {
            // تجزیه ناموفق — حداقل رمز را با الگو بپوشان
            return System.Text.RegularExpressions.Regex.Replace(
                cs, @"(?i)(password|pwd)\s*=\s*[^;]*", "$1=" + mask);
        }
    }

    /// <summary>
    /// فاز ۴۸ — تجزیه کامل رشته اتصال SQL Server برای پیش‌پرکردن فرم ویزارد/تنظیمات
    /// (سرور، پورت، بانک، نوع احراز هویت و کاربر) — رشته نامعتبر/خالی = null.
    /// </summary>
    public static (string Server, string Port, string Database, bool WindowsAuth, string User)? ParseSqlFull(string? cs)
    {
        try
        {
            var sb = new SqlConnectionStringBuilder(cs);
            var ds = sb.DataSource ?? "";
            string server = ds, port = "";
            var i = ds.LastIndexOf(',');
            if (i > 0) { server = ds[..i].Trim(); port = ds[(i + 1)..].Trim(); }
            return (server, port, sb.InitialCatalog ?? "", sb.IntegratedSecurity, sb.UserID ?? "");
        }
        catch { return null; }
    }

    /// <summary>
    /// راه‌اندازی اولیه بانک تازه (اسکیما + خودترمیم + سیدرها) روی یک پروایدر مستقل از DI اصلی.
    /// هم ویزارد /Setup و هم صفحه تنظیمات از این می‌گذرند؛ ناموفق = هیچ فایلی نوشته نمی‌شود.
    /// فاز ۶۲ — کانتکست‌های ماژول‌ها + قراردادها + سیدِ ماژول‌ها (IModuleSeeder) هم ثبت
    /// می‌شوند تا «اولین اجرا» از مسیر ویزارد هم داده‌های اولیهٔ هر ماژول را بریزد.
    /// فاز ۸۸ — رفع خطای «No service for type 'IHostEnvironment' has been registered»:
    /// DbInit.InitializeAsync (از فاز ۸۷ب، درون‌ریزی بسته‌های زبان تعبیه‌شده از
    /// ContentRootPath) محیط میزبان را از پروایدر می‌خواهد؛ پروایدر مستقل ویزارد آن را
    /// نداشت → ساخت بانک با وجود اتصال سالم شکست می‌خورد. محیط واقعی میزبان (همان
    /// IWebHostEnvironment کنترلر فراخوان) به‌عنوان IHostEnvironment ثبت می‌شود تا
    /// مسیر ویزارد دقیقاً مانند بوت عادی رفتار کند.
    /// </summary>
    public static async Task<(bool Ok, string? Error)> InitNewDatabaseAsync(string provider, string cs, bool seedDemo, Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        try
        {
            var sc = new ServiceCollection();
            IConfiguration mem = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SeedDemo"] = seedDemo ? "true" : "false" })
                .Build();
            sc.AddSingleton(mem);
            sc.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(env);
            sc.AddLogging();
            void UseDb(DbContextOptionsBuilder o)
            {
                if (IsSqlite(provider)) o.UseSqlite(cs);
                else o.UseSqlServer(cs);
            }
            sc.AddDbContext<AppDbContext>(UseDb);
            // فاز ۶۲ — کانتکست پایه + فاز ۶۸ — پلاگین‌های دیتای کشف‌شده روی همان اتصال
            sc.AddDbContext<HrBaseDbContext>(UseDb);
            // فاز ۶۸ — جایگزین‌های بی‌اثر اول؛ پلاگین‌های حاضر بازنویسی می‌کنند (آخرین ثبت برنده)
            Modules.HrModuleFallbacks.RegisterAttendance(sc);
            Modules.HrModuleFallbacks.RegisterVisitors(sc);
            Modules.HrModuleFallbacks.RegisterAccess(sc);
            Modules.HrModuleFallbacks.RegisterParking(sc);
            // فاز ۶۹ — جایگزین‌های بی‌خطر org/devices هم در پروایدر ویزارد (پرسنل/دستگاه ∈ پلاگین)
            Modules.HrModuleFallbacks.RegisterPeople(sc);
            Modules.HrModuleFallbacks.RegisterDevices(sc);
            Platform.Contracts.HrModuleRuntime.ConfigureDataServices(sc, mem, UseDb);
            await using var sp = sc.BuildServiceProvider();
            await DbInit.InitializeAsync(sp);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.InnerException?.Message ?? ex.Message);
        }
    }
}
