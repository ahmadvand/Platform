using Platform.Web.Data;
using Platform.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Platform.Web.Infrastructure;

// ======================================================================
// مقداردهی اولیهٔ بانک (فاز ۵۷ — از Infrastructure/HasPerm.cs جدا شد تا
// فیلتر مجوز HasPerm به پلتفرم مشترک Platform.Core منتقل شود؛ این کلاس
// چون به DbSeeder (هاست) وابسته است در هاست می‌ماند. رفتار بدون تغییر.)
// ======================================================================

/// <summary>مقداردهی اولیه بانک داده: ساخت اسکیما + RBAC + داده نمایشی.</summary>
public static class DbInit
{
    /// <summary>
    /// فاز ۷۷ — سید زبان‌های پویا: ردیف‌های fa/en + همگام‌سازی بستهٔ پویای en با
    /// دیکشنری استاتیک HrLang.DictEn (کلیدهای جاافتاده هر بوت درج می‌شوند — خودترمیم).
    /// زبان‌های بعدی فقط از پنل ادمین (/Langs) با بستهٔ ترجمه افزوده می‌شوند.
    /// </summary>
    private static async Task SeedLanguagesAsync(AppDbContext db, ILogger logger, Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        try
        {
            var changed = false;
            if (!await db.HrLangDefs.AnyAsync(l => l.Code == "fa"))
            {
                db.HrLangDefs.Add(new Platform.Web.Domain.HrLangDef
                { Code = "fa", Title = "فارسی", Dir = "rtl", IsActive = true, IsDefault = true });
                changed = true;
            }
            if (!await db.HrLangDefs.AnyAsync(l => l.Code == "en"))
            {
                db.HrLangDefs.Add(new Platform.Web.Domain.HrLangDef
                { Code = "en", Title = "English", Dir = "ltr", IsActive = true, IsDefault = false });
                changed = true;
            }
            if (changed) await db.SaveChangesAsync();

            var enCount = await db.HrLangEntries.CountAsync(e => e.LangCode == "en");
            if (enCount < HrLang.DictEn.Count)
            {
                var have = (await db.HrLangEntries.Where(e => e.LangCode == "en")
                    .Select(e => e.Key).ToListAsync()).ToHashSet(StringComparer.Ordinal);
                var missing = HrLang.DictEn.Where(kv => !have.Contains(kv.Key)).ToList();
                foreach (var kv in missing)
                    db.HrLangEntries.Add(new Platform.Web.Domain.HrLangEntry
                    { LangCode = "en", Key = kv.Key, Value = kv.Value });
                if (missing.Count > 0)
                {
                    await db.SaveChangesAsync();
                    logger.LogInformation("بستهٔ زبان en همگام شد — {Count} کلید افزوده شد", missing.Count);
                }
            }

            // فاز ۸۷ — بسته‌های زبان تعبیه‌شده (App_Data/langpacks/*.json) — همان قالب
            // درون‌ریزی پنل ادمین؛ هر بوت ادغام می‌شود (کلید موجود به‌روز، کلید جدید افزوده —
            // هیچ‌چیز حذف نمی‌شود). نصب تازه: زبان‌ها از همان اول حاضرند؛ نصب موجود: خودترمیم.
            await ImportEmbeddedLangPacksAsync(db, logger, env);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "سید زبان‌های پویا ناموفق بود — سامانه با fa/en استاتیک ادامه می‌دهد");
        }
    }

    /// <summary>فاز ۸۷ — درون‌ریزی بسته‌های زبان تعبیه‌شده در App_Data/langpacks (ادغام؛ بدون حذف).</summary>
    private static async Task ImportEmbeddedLangPacksAsync(AppDbContext db, ILogger logger, Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        // مسیر محتوا: ContentRoot (سورس در توسعه؛ پوشهٔ انتشار در publish) + پشتیبان BaseDirectory
        var dir = Path.Combine(env.ContentRootPath, "App_Data", "langpacks");
        if (!Directory.Exists(dir))
            dir = Path.Combine(AppContext.BaseDirectory, "App_Data", "langpacks");
        if (!Directory.Exists(dir)) return;
        string[] files;
        try { files = Directory.GetFiles(dir, "*.json"); } catch { return; }
        foreach (var file in files)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
                var r = doc.RootElement;
                var code = (r.TryGetProperty("code", out var c) ? c.GetString() : null) ?? "";
                var title = (r.TryGetProperty("title", out var t) ? t.GetString() : null) ?? code;
                var dirRtl = (r.TryGetProperty("dir", out var d) ? d.GetString() : null) ?? "ltr";
                if (code.Length == 0 || code == "fa" || !r.TryGetProperty("entries", out var en)
                    || en.ValueKind != System.Text.Json.JsonValueKind.Object) continue;

                if (!await db.HrLangDefs.AnyAsync(l => l.Code == code))
                {
                    db.HrLangDefs.Add(new Platform.Web.Domain.HrLangDef
                    { Code = code, Title = title, Dir = dirRtl, IsActive = true, IsDefault = false });
                    await db.SaveChangesAsync();
                    logger.LogInformation("زبان «{Title}» ({Code}) از بستهٔ تعبیه‌شده افزوده شد", title, code);
                }

                var have = (await db.HrLangEntries.Where(e => e.LangCode == code)
                    .Select(e => e.Key).ToListAsync()).ToHashSet(StringComparer.Ordinal);
                var added = 0;
                foreach (var prop in en.EnumerateObject())
                {
                    if (prop.Value.ValueKind != System.Text.Json.JsonValueKind.String) continue;
                    var key = prop.Name.Trim();
                    // فاز ۹۵ — به‌روزرسانی فوری «have» در همان حلقه: دو واریانت یک کلید در یک فایل
                    // (مثلاً با/بدون فاصلهٔ انتهایی) قبلاً دو ردیف هم‌کلید می‌ساخت و LoadPack را می‌شکست.
                    if (key.Length == 0 || !have.Add(key)) continue;
                    db.HrLangEntries.Add(new Platform.Web.Domain.HrLangEntry
                    { LangCode = code, Key = key, Value = prop.Value.GetString() ?? "" });
                    added++;
                }
                if (added > 0)
                {
                    await db.SaveChangesAsync();
                    logger.LogInformation("بستهٔ زبان {Code} همگام شد — {Count} کلید افزوده شد", code, added);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "درون‌ریزی بستهٔ زبان {File} ناموفق بود — رد شد", file);
            }
        }
    }

    public static async Task InitializeAsync(IServiceProvider sp)
    {
        var env = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInit");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("اسکیما آماده شد ({Ms}ms)", sw.ElapsedMilliseconds);

        // فاز ۸۲ — اسکیمای ماژول‌محور (درخواست کاربر): ساخت اسکیماهای غایب و انتقال
        // جدول‌های پایگاه‌های قدیمی (dbo.*) به اسکیمای خودشان — فقط SQL Server؛
        // SQLite اسکیما ندارد و این مرحله آنجا no-op است.
        await EnsureModuleSchemasAsync(db, logger);

        // خودترمیم اسکیما: EnsureCreated وقتی پایگاه از قبل موجود باشد — حتی اگر کاملاً خالی باشد
        // (مثلاً دستی در SSMS ساخته شده باشد) — هیچ جدولی نمی‌سازد و سیدر با «Invalid object name» می‌شکند.
        // اینجا فقط جدول‌های غایبِ مدل EF ساخته می‌شوند؛ جدول‌ها و داده‌های موجود دست‌نخورده می‌مانند.
        var (createdTables, preservedCount) = await CreateMissingTablesAsync(db);
        if (createdTables.Count > 0)
            logger.LogInformation(
                "پایگاه موجود ناقص بود — {Created} جدول غایب ساخته شد ({Tables}) و {Preserved} جدول موجود (داده‌ها) دست‌نخورده ماند ({Ms}ms)",
                createdTables.Count, string.Join("، ", createdTables), preservedCount, sw.ElapsedMilliseconds);

        // وصال اسکیما برای دیتابیس‌های قدیمی‌تر — EnsureCreated ستون‌های جدید را اضافه نمی‌کند.
        await PatchSchemaAsync(db, logger);

        // فاز ۷۸b — وصال عمومی ستون‌های غایب از روی مدل EF (هر دو پروایدر) —
        // هر ستونی که در مدل باشد و در جدول موجودِ پایگاه غایب باشد، خودکار افزوده می‌شود
        // (مثل Users.DepartmentId فاز ۷۴ که در وصال‌های دستی پوشش داده نشده بود و
        // روی پایگاه‌های قدیمی SQL Server خطای «Invalid column name 'DepartmentId'» می‌داد).
        await PatchMissingModelColumnsAsync(db, logger);

        // فاز ۱۱۰ — ترمیم یک‌بارهٔ جبران قمری ردیف تنظیمات: ستون HijriAdjust در پایگاه‌های قدیمی‌تر
        // توسط وصال عمومی با DEFAULT 0 افزوده شده بود، در حالی که پیش‌فرض محصول ‎-۱ است (ام‌القری
        // نسبت به تقویم قمری رسمی ایران/افغانستان یک روز اختلاف دارد)؛ نتیجه‌اش نمایش مناسبت‌های
        // قمری مثل ۱۵ شعبان یک روز عقب بود (درخواست کاربر: «۱۵ شعبان یکشنبه/۴ بهمن ۱۴۰۵ است ولی
        // سامانه شنبه نشان می‌دهد»). فقط ردیف‌های علامت‌نخورده ترمیم می‌شوند؛ انتخاب صریحِ بعدی
        // ادمین (حتی صفر = دقیقاً ام‌القری) دست‌نخورده می‌ماند.
        await PatchHijriAdjustDefaultAsync(db, logger);

        // فاز ۷۷ — سید زبان‌های پویا: fa (مبدأ) + en (بستهٔ پویا = دیکشنری استاتیک فاز ۷۶) — خودترمیم
        await SeedLanguagesAsync(db, logger, env);

        await DbSeeder.EnsureRbacAsync(db);
        logger.LogInformation("RBAC آماده شد ({Ms}ms)", sw.ElapsedMilliseconds);

        if (cfg.GetValue("SeedDemo", true))
        {
            await DbSeeder.EnsureSeedAsync(db);
            logger.LogInformation("داده نمایشی آماده شد ({Ms}ms)", sw.ElapsedMilliseconds);
        }

        // فاز ۶۹ — کاربران پیش‌فرض بعد از سید ماژول‌ها (پیوند پرسنل از قرارداد
        // IPeopleDirectory؛ org غایب = کاربران بدون پیوند — سامانه سالم)
        await DbSeeder.EnsureDefaultUsersAsync(db,
            scope.ServiceProvider.GetRequiredService<Platform.Contracts.IPeopleDirectory>());
        logger.LogInformation("کاربران پیش‌فرض آماده شدند");

        // فاز ۲۴ — موتور حقوق داینامیک: تنظیمات/اقلام/پارامترها + شرکت دوم + لیست نمونه (خودترمیم)
        // فاز ۶۲ — سیدِ داده‌های اولیهٔ هر ماژول به لایهٔ دیتای خود ماژول منتقل شد (IModuleSeeder)
        await DbSeeder.EnsureSettingsSeedAsync(db);
        logger.LogInformation("تنظیمات سامانه آماده شد ({Ms}ms)", sw.ElapsedMilliseconds);

        // فاز ۳۲ — پالت‌های رنگ استاندارد (خودترمیم)
        await DbSeeder.EnsurePalettesSeedAsync(db);
        logger.LogInformation("پالت‌های رنگ آماده شد ({Ms}ms)", sw.ElapsedMilliseconds);

        // فاز ۶۲ — سیدِ ماژول‌ها: هر ماژول داده‌های خودش را (خودترمیم) می‌ریزد —
        // حضور (سرپرستی شیفت + شرکت دوم + backfill لاگ تردد)، دسترسی (کارت‌ها)،
        // حقوق (پیکربندی + لیست نمونه)، پارکینگ (دادهٔ نمایشی + دوربین‌ها).
        // اجرا در اولین اجرا و هر بوت (خودترمیم) — هم بوت عادی و هم ویزارد راه‌اندازی اولیه.
        var seeders = sp.GetServices<Platform.Contracts.IModuleSeeder>()
            .OrderBy(s => s.Order).ToList();
        foreach (var seeder in seeders)
        {
            var swMod = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await seeder.SeedAsync(sp, cfg.GetValue("SeedDemo", true));
                logger.LogInformation("سید ماژول {Module} آماده شد ({Ms}ms)", seeder.ModuleCode, swMod.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // خطای سید یک ماژول هرگز نباید راه‌اندازی کل سامانه را متوقف کند؛
                // در بوت بعدی همان سید دوباره (خودترمیم) تلاش می‌کند.
                logger.LogError(ex, "سید ماژول {Module} ناموفق بود — در بوت بعدی دوباره تلاش می‌شود", seeder.ModuleCode);
            }
        }
    }

    /// <summary>
    /// ساخت فقط جدول‌های غایبِ مدل EF روی پایگاه موجود (خالی یا ناقص) — هر دو پروایدر.
    /// از تفاضل مدل EF با «مدل خالی» استفاده می‌کند (همان مکانیزم مهاجرت‌های EF)؛
    /// جدول‌های موجود و داده‌هایشان دست‌نخورده می‌مانند.
    /// </summary>
    private static async Task<(List<string> Created, int Skipped)> CreateMissingTablesAsync(AppDbContext db)
    {
        // فاز ۸۲ — کلیدهای وجود: SQLite نام سادهٔ جدول؛ SQL Server «schema.table»
        // (جدول‌های مدل اکنون اسکیمای ماژول دارند — core/org/devices/…)
        var isSqlite = db.Database.IsSqlite();
        var existing = await GetExistingTablesAsync(db);

        string KeyOf(string? schema, string? table)
        {
            if (string.IsNullOrEmpty(table)) return "";
            if (isSqlite || string.IsNullOrEmpty(schema) || string.Equals(schema, "dbo", StringComparison.Ordinal)) return table;
            return schema + "." + table;
        }

        var modelTables = db.Model.GetEntityTypes()
            .Select(et => KeyOf(et.GetSchema(), et.GetTableName()))
            .Where(n => n != "")
            .Distinct()
            .ToList();

        if (modelTables.All(existing.Contains)) return (new List<string>(), 0);

        var differ = db.GetService<IMigrationsModelDiffer>();
        var generator = db.GetService<IMigrationsSqlGenerator>();

        // تفاضل با مدل خالی → همه عملیات Create به ترتیب وابستگی (اسکیماها، جدول‌ها، ایندکس‌ها/کلیدها)
        // دقت: تفاضل‌گر به مدل design-time نیاز دارد (مدل بهینه‌شده حاشیه‌نویسی‌های مهاجرت را ندارد)
        var designModel = db.GetService<IDesignTimeModel>().Model;
        var ops = differ.GetDifferences(null, designModel.GetRelationalModel());
        var missing = ops.Where(op => op switch
        {
            CreateTableOperation t => !existing.Contains(KeyOf(t.Schema, t.Name)),
            CreateIndexOperation i => !existing.Contains(KeyOf(i.Schema, i.Table)),
            AddForeignKeyOperation f => !existing.Contains(KeyOf(f.Schema, f.Table)),
            _ => true,
        }).ToList();

        var commands = generator.Generate(missing, db.Model);
        foreach (var cmd in commands)
            await db.Database.ExecuteSqlRawAsync(cmd.CommandText);

        return (modelTables.Where(t => !existing.Contains(t)).ToList(), existing.Count);
    }

    /// <summary>نام جدول‌های موجود در پایگاه داده (هر دو پروایدر) — فاز ۸۲:
    /// روی SQL Server کلید «schema.table» است؛ روی SQLite نام سادهٔ جدول.</summary>
    private static async Task<HashSet<string>> GetExistingTablesAsync(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = db.Database.IsSqlite()
            ? "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"
            : "SELECT s.name + N'.' + t.name FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) set.Add(r.GetString(0));
        return set;
    }

    // ==================================================================
    // فاز ۸۲ — اسکیمای دیتابیس هر ماژول (درخواست کاربر: «هر ماژول توی دیتابیس
    // اسکیمای خودش»). روی SQL Server اسکیمای واقعی: core/org/devices/attendance/
    // payroll/visitors/access/parking — روی SQLite EF اسکیما را نادیده می‌گیرد
    // (پروب‌شده) و همه‌چیز مثل قبل در فایل تکی می‌ماند.
    // ==================================================================

    /// <summary>اسکیمای هدف یک جدول از مدل EF — نبود = dbo (جدول مدل‌نشده).</summary>
    private static string? TableSchema(AppDbContext db, string table) => db.Model.GetEntityTypes()
        .Where(et => string.Equals(et.GetTableName(), table, StringComparison.OrdinalIgnoreCase))
        .Select(et => et.GetSchema())
        .FirstOrDefault();

    /// <summary>نام مرجع جدول برای SQLهای وصال — SQL Server: «[schema].[table]»؛ SQLite: «table».
    /// اسکیماهای هدف پیش از این مرحله با EnsureModuleSchemasAsync ساخته شده‌اند.</summary>
    private static string Q(AppDbContext db, bool isSqlite, string table)
    {
        if (isSqlite) return $"[{table}]";
        var schema = TableSchema(db, table);
        return $"[{(string.IsNullOrEmpty(schema) ? "dbo" : schema)}].[{table}]";
    }

    /// <summary>ساخت اسکیماهای غایب + انتقال جدول‌های پایگاه‌های قدیمی به اسکیمای مالکشان
    /// (ALTER SCHEMA … TRANSFER — داده‌ها و ایندکس‌ها همراه جدول می‌آیند). SQLite: no-op.</summary>
    private static async Task EnsureModuleSchemasAsync(AppDbContext db, ILogger logger)
    {
        if (db.Database.IsSqlite()) return;
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        // جدول → اسکیمای هدف (از مدل EF — همان نگاشت HrModuleRuntime.ApplySchemas)
        var targets = db.Model.GetEntityTypes()
            .Where(et => et.GetTableName() is not null)
            .GroupBy(et => et.GetTableName()!, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Table: g.Key, Schema: g.First().GetSchema() ?? "dbo"))
            .ToList();

        // ۱) هر اسکیمای مدل اگر نبود ساخته می‌شود (EnsureCreated روی پایگاه نو هم همین را می‌زند)
        foreach (var s in targets.Select(t => t.Schema).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"IF SCHEMA_ID(N'{s}') IS NULL EXEC(N'CREATE SCHEMA [{s}]');";
            await cmd.ExecuteNonQueryAsync();
        }

        // ۲) وضعیت فعلی: «schema.table» → انتقال به خانهٔ جدید
        var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT s.name, t.name FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) current.Add(r.GetString(0) + "." + r.GetString(1));
        }

        var moved = 0;
        foreach (var (table, schema) in targets)
        {
            if (string.Equals(schema, "dbo", StringComparison.Ordinal)) continue; // هدف dbo = جابه‌جایی ندارد
            if (current.Contains(schema + "." + table)) continue;                 // همین حالا در خانهٔ درست است
            var from = current.FirstOrDefault(x => x.EndsWith("." + table, StringComparison.OrdinalIgnoreCase));
            if (from is null) continue; // جدول هنوز ساخته نشده — CreateMissingTablesAsync در اسکیمای درست می‌سازد
            var fromSchema = from[..from.LastIndexOf('.')];
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER SCHEMA [{schema}] TRANSFER [{fromSchema}].[{table}]";
            await cmd.ExecuteNonQueryAsync();
            current.Remove(from);
            current.Add(schema + "." + table);
            moved++;
        }
        if (moved > 0)
            logger.LogInformation("اسکیمای ماژول‌محور: {Count} جدول به اسکیمای خودشان منتقل شد (فاز ۸۲)", moved);
    }

    /// <summary>افزودن جدول/ستون‌های افزوده‌شده در نسخه‌های بعدی به دیتابیس موجود (هر دو پروایدر).</summary>
    /// <summary>فاز ۶۷ — وصال اسکیما فقط برای لایه‌های دیتای حاضر؛
    /// ماژول کامپایل‌نشده در این بیلد = false؛ DLL حذف‌شده در اجرا هم = false.</summary>
    private static bool ModOk(string code) =>
        Platform.Contracts.HrModuleRuntime.Installed(code);

    private static async Task PatchSchemaAsync(AppDbContext db, ILogger logger)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        var isSqlite = db.Database.IsSqlite();

        // فاز ۸۲ — مرجع اسکیما-آگاه جدول برای وصال‌ها (SQL Server: [schema].[table] — SQLite: [table])
        string T(string table) => Q(db, isSqlite, table);

        async Task<bool> TableExistsAsync(string table)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'"
                : $"SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name='{table}' AND s.name='{(TableSchema(db, table) ?? "dbo")}'";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        // Devices.Kind (نسخه‌های قدیمی SQLite)
        if (isSqlite && await TableExistsAsync("Devices"))
        {
            var cols = new HashSet<string>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Devices)";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) cols.Add(r.GetString(1));
            }
            if (!cols.Contains("Kind"))
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "ALTER TABLE Devices ADD COLUMN Kind TEXT NOT NULL DEFAULT 'ترمینال اثر انگشت'";
                await cmd.ExecuteNonQueryAsync();
                logger.LogInformation("ستون Devices.Kind به دیتابیس موجود اضافه شد");
            }
        }

        // ستون‌های اتصال گیت‌وی (فاز ۲۱): Devices.Transport / DevicePort / CommKey — هر دو پروایدر
        if (await TableExistsAsync("Devices"))
        {
            async Task<bool> ColExistsAsync(string col)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = isSqlite
                    ? $"SELECT COUNT(*) FROM pragma_table_info('Devices') WHERE name='{col}'"
                    : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T("Devices")}') AND name='{col}'";
                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            }
            if (!await ColExistsAsync("Transport"))
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = isSqlite
                    ? "ALTER TABLE Devices ADD COLUMN Transport TEXT NOT NULL DEFAULT 'adms'"
                    : $"ALTER TABLE {T("Devices")} ADD Transport nvarchar(450) NOT NULL DEFAULT 'adms'";
                await cmd.ExecuteNonQueryAsync();
                logger.LogInformation("ستون Devices.Transport اضافه شد");
            }
            if (!await ColExistsAsync("DevicePort"))
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = isSqlite
                    ? "ALTER TABLE Devices ADD COLUMN DevicePort INTEGER NULL"
                    : $"ALTER TABLE {T("Devices")} ADD DevicePort int NULL";
                await cmd.ExecuteNonQueryAsync();
            }
            if (!await ColExistsAsync("CommKey"))
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = isSqlite
                    ? "ALTER TABLE Devices ADD COLUMN CommKey INTEGER NULL"
                    : $"ALTER TABLE {T("Devices")} ADD CommKey int NULL";
                await cmd.ExecuteNonQueryAsync();
                logger.LogInformation("ستون‌های اتصال گیت‌وی (Transport/DevicePort/CommKey) اضافه شدند");
            }
        }

        // فاز ۷۷ — سلسله‌مراتب نقش‌ها: Roles.ParentRoleId (نقش فرزند دسترسی‌های والد را به ارث می‌برد)
        if (await TableExistsAsync("Roles"))
        {
            async Task<bool> RoleColAsync(string col)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = isSqlite
                    ? $"SELECT COUNT(*) FROM pragma_table_info('Roles') WHERE name='{col}'"
                    : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T("Roles")}') AND name='{col}'";
                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            }
            if (!await RoleColAsync("ParentRoleId"))
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = isSqlite
                    ? "ALTER TABLE Roles ADD COLUMN ParentRoleId TEXT NULL"
                    : $"ALTER TABLE {T("Roles")} ADD ParentRoleId nvarchar(64) NULL";
                await cmd.ExecuteNonQueryAsync();
                logger.LogInformation("ستون Roles.ParentRoleId اضافه شد (سلسله‌مراتب نقش‌ها)");
            }
        }

        // جدول تنظیمات گیت‌وی (فاز ۲۱) — برای دیتابیس‌های ساخته‌شده قبل از این فاز
        // فاز ۶۹ — فقط با ماژول devices حاضر (غایب = هیچ جدولی از خانواده‌اش ساخته نمی‌شود)
        if (ModOk("devices") && !await TableExistsAsync("GatewaySettings"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? @"CREATE TABLE GatewaySettings (
                        Id TEXT NOT NULL CONSTRAINT PK_GatewaySettings PRIMARY KEY,
                        Enabled INTEGER NOT NULL, ListenPort INTEGER NOT NULL,
                        TcpTimeoutMs INTEGER NOT NULL, PollIntervalSec INTEGER NOT NULL,
                        SweeperIntervalSec INTEGER NOT NULL, ConfigRefreshSec INTEGER NOT NULL,
                        LogLevel TEXT NOT NULL, DefaultDevicePort INTEGER NOT NULL,
                        DefaultCommKey INTEGER NOT NULL, LastFetchedAt TEXT NULL,
                        ConfigVersion INTEGER NOT NULL, UpdatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("GatewaySettings")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_GatewaySettings PRIMARY KEY,
                        [Enabled] bit NOT NULL, [ListenPort] int NOT NULL,
                        [TcpTimeoutMs] int NOT NULL, [PollIntervalSec] int NOT NULL,
                        [SweeperIntervalSec] int NOT NULL, [ConfigRefreshSec] int NOT NULL,
                        [LogLevel] nvarchar(max) NOT NULL, [DefaultDevicePort] int NOT NULL,
                        [DefaultCommKey] int NOT NULL, [LastFetchedAt] datetime2 NULL,
                        [ConfigVersion] int NOT NULL, [UpdatedAt] datetime2 NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("جدول GatewaySettings به دیتابیس موجود اضافه شد");
        }

        // ---- فاز ۱۷: کنترل دسترسی پیشرفته ----
        // ستون‌های جدید: Zones.AntiPassback + Gates.ScheduleDays/ScheduleFrom/ScheduleTo
        if (await TableExistsAsync("Zones"))
        {
            async Task<bool> ZoneColExistsAsync(string col)
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? $"SELECT COUNT(*) FROM pragma_table_info('Zones') WHERE name='{col}'"
                    : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T("Zones")}') AND name='{col}'";
                return Convert.ToInt32(await c2.ExecuteScalarAsync()) > 0;
            }
            if (!await ZoneColExistsAsync("AntiPassback"))
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? "ALTER TABLE Zones ADD COLUMN AntiPassback INTEGER NOT NULL DEFAULT 0"
                    : $"ALTER TABLE {T("Zones")} ADD AntiPassback bit NOT NULL DEFAULT 0";
                await c2.ExecuteNonQueryAsync();
                logger.LogInformation("ستون Zones.AntiPassback اضافه شد (فاز ۱۷)");
            }
        }
        if (await TableExistsAsync("Gates"))
        {
            async Task<bool> GateColExistsAsync(string col)
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? $"SELECT COUNT(*) FROM pragma_table_info('Gates') WHERE name='{col}'"
                    : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T("Gates")}') AND name='{col}'";
                return Convert.ToInt32(await c2.ExecuteScalarAsync()) > 0;
            }
            if (!await GateColExistsAsync("ScheduleDays"))
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? "ALTER TABLE Gates ADD COLUMN ScheduleDays TEXT NULL"
                    : $"ALTER TABLE {T("Gates")} ADD ScheduleDays nvarchar(max) NULL";
                await c2.ExecuteNonQueryAsync();
            }
            if (!await GateColExistsAsync("ScheduleFrom"))
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? "ALTER TABLE Gates ADD COLUMN ScheduleFrom TEXT NULL"
                    : $"ALTER TABLE {T("Gates")} ADD ScheduleFrom nvarchar(max) NULL";
                await c2.ExecuteNonQueryAsync();
            }
            if (!await GateColExistsAsync("ScheduleTo"))
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? "ALTER TABLE Gates ADD COLUMN ScheduleTo TEXT NULL"
                    : $"ALTER TABLE {T("Gates")} ADD ScheduleTo nvarchar(max) NULL";
                await c2.ExecuteNonQueryAsync();
                logger.LogInformation("ستون‌های برنامه زمانی گیت‌ها اضافه شدند (فاز ۱۷)");
            }
            // فاز ۱۳۱ (مورد ۱۲ ROADMAP-ADDITIONS) — پرچم «تغذیهٔ حضور و غیاب» گیت
            if (!await GateColExistsAsync("FeedsAttendance"))
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? "ALTER TABLE Gates ADD COLUMN FeedsAttendance INTEGER NOT NULL DEFAULT 0"
                    : $"ALTER TABLE {T("Gates")} ADD FeedsAttendance bit NOT NULL DEFAULT 0";
                await c2.ExecuteNonQueryAsync();
                logger.LogInformation("ستون Gates.FeedsAttendance اضافه شد (فاز ۱۳۱)");
            }
        }
        // جدول‌های گروه دسترسی (فاز ۱۷)
        if (ModOk("access") && !await TableExistsAsync("AccessGroups"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE AccessGroups (
                        Id TEXT NOT NULL CONSTRAINT PK_AccessGroups PRIMARY KEY,
                        Name TEXT NOT NULL, Note TEXT NULL, CreatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("AccessGroups")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_AccessGroups PRIMARY KEY,
                        [Name] nvarchar(max) NOT NULL, [Note] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL)";
            await c2.ExecuteNonQueryAsync();
            logger.LogInformation("جدول AccessGroups اضافه شد (فاز ۱۷)");
        }
        if (ModOk("access") && !await TableExistsAsync("AccessGroupMembers"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE AccessGroupMembers (
                        Id TEXT NOT NULL CONSTRAINT PK_AccessGroupMembers PRIMARY KEY,
                        GroupId TEXT NOT NULL, EmployeeId TEXT NOT NULL, CreatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("AccessGroupMembers")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_AccessGroupMembers PRIMARY KEY,
                        [GroupId] nvarchar(450) NOT NULL, [EmployeeId] nvarchar(450) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL)";
            await c2.ExecuteNonQueryAsync();
        }
        if (ModOk("access") && !await TableExistsAsync("AccessGroupGates"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE AccessGroupGates (
                        Id TEXT NOT NULL CONSTRAINT PK_AccessGroupGates PRIMARY KEY,
                        GroupId TEXT NOT NULL, GateId TEXT NOT NULL,
                        Days TEXT NULL, TimeFrom TEXT NULL, TimeUntil TEXT NULL, CreatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("AccessGroupGates")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_AccessGroupGates PRIMARY KEY,
                        [GroupId] nvarchar(450) NOT NULL, [GateId] nvarchar(450) NOT NULL,
                        [Days] nvarchar(max) NULL, [TimeFrom] nvarchar(max) NULL,
                        [TimeUntil] nvarchar(max) NULL, [CreatedAt] datetime2 NOT NULL)";
            await c2.ExecuteNonQueryAsync();
            logger.LogInformation("جدول‌های گروه دسترسی اضافه شدند (فاز ۱۷)");
        }

        // جدول‌های موتور اعلان‌ها (فاز ۱۳) — برای دیتابیس‌های ساخته‌شده قبل از این فاز
        if (!await TableExistsAsync("NotificationRules"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? @"CREATE TABLE NotificationRules (
                        Id TEXT NOT NULL CONSTRAINT PK_NotificationRules PRIMARY KEY,
                        Name TEXT NOT NULL, Type TEXT NOT NULL,
                        Enabled INTEGER NOT NULL, Threshold INTEGER NOT NULL,
                        RecipientEmail TEXT NULL, RecipientPhone TEXT NULL,
                        ViaEmail INTEGER NOT NULL, ViaSms INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL, LastRunAt TEXT NULL)"
                : $@"CREATE TABLE {T("NotificationRules")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_NotificationRules PRIMARY KEY,
                        [Name] nvarchar(max) NOT NULL, [Type] nvarchar(max) NOT NULL,
                        [Enabled] bit NOT NULL, [Threshold] int NOT NULL,
                        [RecipientEmail] nvarchar(max) NULL, [RecipientPhone] nvarchar(max) NULL,
                        [ViaEmail] bit NOT NULL, [ViaSms] bit NOT NULL,
                        [CreatedAt] datetime2 NOT NULL, [LastRunAt] datetime2 NULL)";
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("جدول NotificationRules به دیتابیس موجود اضافه شد");
        }
        // فاز ۱۳۵ (مورد ۱۸ ROADMAP-ADDITIONS) — ستون‌های کانال وب‌هوک برای نصب‌های قدیمی‌تر
        if (await TableExistsAsync("NotificationRules"))
        {
            await P24AddColumnAsync("NotificationRules", "ViaWebhook", "INTEGER NOT NULL DEFAULT 0", "bit NOT NULL DEFAULT 0");
            await P24AddColumnAsync("NotificationSettings", "WebhookUrl", "TEXT NOT NULL DEFAULT ''", "nvarchar(max) NOT NULL DEFAULT N''");
            await P24AddColumnAsync("NotificationSettings", "WebhookSecret", "TEXT NOT NULL DEFAULT ''", "nvarchar(max) NOT NULL DEFAULT N''");
        }
        if (!await TableExistsAsync("NotificationLogs"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? @"CREATE TABLE NotificationLogs (
                        Id TEXT NOT NULL CONSTRAINT PK_NotificationLogs PRIMARY KEY,
                        RuleName TEXT NOT NULL, Type TEXT NOT NULL, Channel TEXT NOT NULL,
                        Recipient TEXT NOT NULL, Title TEXT NOT NULL, Body TEXT NOT NULL,
                        Status TEXT NOT NULL, Error TEXT NULL, CreatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("NotificationLogs")} (
                        [Id] int NOT NULL IDENTITY CONSTRAINT PK_NotificationLogs PRIMARY KEY,
                        [RuleName] nvarchar(max) NOT NULL, [Type] nvarchar(max) NOT NULL,
                        [Channel] nvarchar(max) NOT NULL, [Recipient] nvarchar(max) NOT NULL,
                        [Title] nvarchar(max) NOT NULL, [Body] nvarchar(max) NOT NULL,
                        [Status] nvarchar(max) NOT NULL, [Error] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("جدول NotificationLogs به دیتابیس موجود اضافه شد");
        }
        if (!await TableExistsAsync("NotificationSettings"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? @"CREATE TABLE NotificationSettings (
                        Id TEXT NOT NULL CONSTRAINT PK_NotificationSettings PRIMARY KEY,
                        EnabledEmail INTEGER NOT NULL, EnabledSms INTEGER NOT NULL,
                        SmtpHost TEXT NOT NULL, SmtpPort INTEGER NOT NULL, SmtpSsl INTEGER NOT NULL,
                        SmtpUser TEXT NOT NULL, SmtpPass TEXT NOT NULL, FromEmail TEXT NOT NULL,
                        AdminEmails TEXT NOT NULL, SmsApiUrl TEXT NOT NULL, SmsApiKey TEXT NOT NULL,
                        SmsSender TEXT NOT NULL, UpdatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("NotificationSettings")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_NotificationSettings PRIMARY KEY,
                        [EnabledEmail] bit NOT NULL, [EnabledSms] bit NOT NULL,
                        [SmtpHost] nvarchar(max) NOT NULL, [SmtpPort] int NOT NULL, [SmtpSsl] bit NOT NULL,
                        [SmtpUser] nvarchar(max) NOT NULL, [SmtpPass] nvarchar(max) NOT NULL,
                        [FromEmail] nvarchar(max) NOT NULL, [AdminEmails] nvarchar(max) NOT NULL,
                        [SmsApiUrl] nvarchar(max) NOT NULL, [SmsApiKey] nvarchar(max) NOT NULL,
                        [SmsSender] nvarchar(max) NOT NULL, [UpdatedAt] datetime2 NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("جدول NotificationSettings به دیتابیس موجود اضافه شد");
        }

        // ---- فاز ۲۴: موتور حقوق داینامیک چندشرکتی ----
        // ستون‌های جدید: Employees.MaritalStatus/ChildCount، Shifts.CompanyId، PayrollRuns.CompanyId
        async Task<bool> P24ColExistsAsync(string table, string col)
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{col}'"
                : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T(table)}') AND name='{col}'";
            return Convert.ToInt32(await c2.ExecuteScalarAsync()) > 0;
        }
        async Task P24AddColumnAsync(string table, string col, string sqliteType, string sqlType)
        {
            if (!await TableExistsAsync(table) || await P24ColExistsAsync(table, col)) return;
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? $"ALTER TABLE {table} ADD COLUMN {col} {sqliteType}"
                : $"ALTER TABLE {T(table)} ADD {col} {sqlType}";
            await c2.ExecuteNonQueryAsync();
            logger.LogInformation("ستون {Table}.{Col} اضافه شد (فاز ۲۴)", table, col);
        }
        if (await TableExistsAsync("Employees"))
        {
            await P24AddColumnAsync("Employees", "MaritalStatus", "TEXT NOT NULL DEFAULT 'مجرد'", "nvarchar(50) NOT NULL DEFAULT N'مجرد'");
            await P24AddColumnAsync("Employees", "ChildCount", "INTEGER NOT NULL DEFAULT 0", "int NOT NULL DEFAULT 0");
        }
        // فاز ۸۶ — نوع تقویم نمایشی (شمسی/میلادی/قمری) برای دیتابیس‌های قدیمی‌تر؛ دیتابیس نو با EnsureCreated ساخته می‌شود
        if (await TableExistsAsync("AppSettings"))
        {
            await P24AddColumnAsync("AppSettings", "Calendar", "TEXT NOT NULL DEFAULT 'jalali'", "nvarchar(20) NOT NULL DEFAULT N'jalali'");
            // فاز ۹۱ — ارز نمایشی + نرخ تبدیل نمایشی (درخواست کاربر: هر کشور ارز خودش را دارد)
            await P24AddColumnAsync("AppSettings", "Currency", "TEXT NOT NULL DEFAULT 'IRR'", "nvarchar(10) NOT NULL DEFAULT N'IRR'");
            await P24AddColumnAsync("AppSettings", "CurrencyRate", "TEXT NULL", "nvarchar(20) NULL");
            // فاز ۹۸ (درخواست کاربر) — ثانیه‌های بی‌حرکتی محافظ صفحه در کنار دقیقه‌ها؛ دیتابیس‌های قدیمی‌تر خودکار کامل می‌شوند
            await P24AddColumnAsync("AppSettings", "ScreenSaverSeconds", "INTEGER NOT NULL DEFAULT 0", "int NOT NULL DEFAULT 0");
            // فاز ۱۳۵ (مورد ۲۰ ROADMAP-ADDITIONS) — پشتیبان‌گیری زمان‌بندی‌شدهٔ دیتابیس
            await P24AddColumnAsync("AppSettings", "BackupEnabled", "INTEGER NOT NULL DEFAULT 0", "bit NOT NULL DEFAULT 0");
            await P24AddColumnAsync("AppSettings", "BackupTime", "TEXT NOT NULL DEFAULT '03:00'", "nvarchar(5) NOT NULL DEFAULT N'03:00'");
            await P24AddColumnAsync("AppSettings", "BackupRetention", "INTEGER NOT NULL DEFAULT 7", "int NOT NULL DEFAULT 7");
            await P24AddColumnAsync("AppSettings", "BackupLastDate", "TEXT NULL", "nvarchar(10) NULL");
        }
        if (ModOk("attendance") && await TableExistsAsync("Shifts"))
        {
            await P24AddColumnAsync("Shifts", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
            // فاز ۱۳۵ (مورد ۱۶ ROADMAP-ADDITIONS) — لنگرگاه چرخه برای تقویم آینده‌نگر اقماری/چرخشی
            await P24AddColumnAsync("Shifts", "CycleAnchor", "TEXT NULL", "datetime2 NULL");
        }
        if (ModOk("payroll") && await TableExistsAsync("PayrollRuns"))
        {
            await P24AddColumnAsync("PayrollRuns", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
            // ایندکس یکتا از (JYear,JMonth) به (CompanyId,JYear,JMonth) تغییر کرد
            await using (var c2 = conn.CreateCommand())
            {
                c2.CommandText = isSqlite
                    ? "DROP INDEX IF EXISTS IX_PayrollRuns_JYear_JMonth"
                    : $@"DECLARE @i nvarchar(200) = (SELECT name FROM sys.indexes WHERE object_id=OBJECT_ID('{T("PayrollRuns")}') AND is_unique_constraint=1 AND name LIKE '%JYear%');
                        IF @i IS NOT NULL EXEC('DROP INDEX ['+@i+'] ON {T("PayrollRuns")}')";
                await c2.ExecuteNonQueryAsync();
                c2.CommandText = isSqlite
                    ? "CREATE UNIQUE INDEX IF NOT EXISTS IX_PayrollRuns_CompanyId_JYear_JMonth ON PayrollRuns(CompanyId, JYear, JMonth)"
                    : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PayrollRuns_CompanyId_JYear_JMonth' AND object_id=OBJECT_ID('{T("PayrollRuns")}')) CREATE UNIQUE INDEX IX_PayrollRuns_CompanyId_JYear_JMonth ON {T("PayrollRuns")}([CompanyId],[JYear],[JMonth])";
                await c2.ExecuteNonQueryAsync();
            }
        }
        // جدول‌های موتور حقوق — برای دیتابیس‌های قدیمی‌تر؛ دیتابیس نو با EnsureCreated ساخته می‌شود
        if (ModOk("payroll") && !await TableExistsAsync("PayItems"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE PayItems (
                        Id TEXT NOT NULL CONSTRAINT PK_PayItems PRIMARY KEY,
                        CompanyId TEXT NOT NULL, Code TEXT NOT NULL, Title TEXT NOT NULL,
                        Kind TEXT NOT NULL, Formula TEXT NOT NULL,
                        Taxable INTEGER NOT NULL, Insurable INTEGER NOT NULL, Continuous INTEGER NOT NULL,
                        ScopeType TEXT NOT NULL, ScopeId TEXT NULL,
                        Priority INTEGER NOT NULL, " + "\"Order\"" + @" INTEGER NOT NULL,
                        IsSystem INTEGER NOT NULL, Active INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("PayItems")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_PayItems PRIMARY KEY,
                        [CompanyId] nvarchar(450) NOT NULL, [Code] nvarchar(450) NOT NULL,
                        [Title] nvarchar(max) NOT NULL, [Kind] nvarchar(max) NOT NULL,
                        [Formula] nvarchar(max) NOT NULL,
                        [Taxable] bit NOT NULL, [Insurable] bit NOT NULL, [Continuous] bit NOT NULL,
                        [ScopeType] nvarchar(max) NOT NULL, [ScopeId] nvarchar(max) NULL,
                        [Priority] int NOT NULL, [Order] int NOT NULL,
                        [IsSystem] bit NOT NULL, [Active] bit NOT NULL,
                        [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NOT NULL)";
            await c2.ExecuteNonQueryAsync();
            logger.LogInformation("جدول PayItems اضافه شد (فاز ۲۴)");
        }
        if (ModOk("payroll") && !await TableExistsAsync("PayrollParameters"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE PayrollParameters (
                        Id TEXT NOT NULL CONSTRAINT PK_PayrollParameters PRIMARY KEY,
                        CompanyId TEXT NOT NULL, JYear INTEGER NOT NULL,
                        Code TEXT NOT NULL, Title TEXT NOT NULL,
                        Value TEXT NOT NULL, StrValue TEXT NULL,
                        CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("PayrollParameters")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_PayrollParameters PRIMARY KEY,
                        [CompanyId] nvarchar(450) NOT NULL, [JYear] int NOT NULL,
                        [Code] nvarchar(450) NOT NULL, [Title] nvarchar(max) NOT NULL,
                        [Value] decimal(18,2) NOT NULL, [StrValue] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NOT NULL)";
            await c2.ExecuteNonQueryAsync();
        }
        if (ModOk("payroll") && !await TableExistsAsync("PayrollSettings"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE PayrollSettings (
                        Id TEXT NOT NULL CONSTRAINT PK_PayrollSettings PRIMARY KEY,
                        CompanyId TEXT NOT NULL, CalcMethod TEXT NOT NULL, WorkSource INTEGER NOT NULL,
                        MonthDays INTEGER NOT NULL, HourBase INTEGER NOT NULL, RoundUnit INTEGER NOT NULL,
                        UpdatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("PayrollSettings")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_PayrollSettings PRIMARY KEY,
                        [CompanyId] nvarchar(450) NOT NULL, [CalcMethod] nvarchar(max) NOT NULL,
                        [WorkSource] int NOT NULL, [MonthDays] int NOT NULL,
                        [HourBase] int NOT NULL, [RoundUnit] int NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL)";
            await c2.ExecuteNonQueryAsync();
        }
        if (ModOk("payroll") && !await TableExistsAsync("PayslipLines"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE PayslipLines (
                        Id TEXT NOT NULL CONSTRAINT PK_PayslipLines PRIMARY KEY,
                        PayslipId TEXT NOT NULL, Code TEXT NOT NULL, Title TEXT NOT NULL,
                        Kind TEXT NOT NULL, Amount TEXT NOT NULL,
                        Taxable INTEGER NOT NULL, Insurable INTEGER NOT NULL, " + "\"Order\"" + @" INTEGER NOT NULL)"
                : $@"CREATE TABLE {T("PayslipLines")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_PayslipLines PRIMARY KEY,
                        [PayslipId] nvarchar(450) NOT NULL, [Code] nvarchar(450) NOT NULL,
                        [Title] nvarchar(max) NOT NULL, [Kind] nvarchar(max) NOT NULL,
                        [Amount] decimal(18,2) NOT NULL,
                        [Taxable] bit NOT NULL, [Insurable] bit NOT NULL, [Order] int NOT NULL)";
            await c2.ExecuteNonQueryAsync();
            await using var c4 = conn.CreateCommand();
            c4.CommandText = isSqlite
                ? "CREATE INDEX IF NOT EXISTS IX_PayslipLines_PayslipId ON PayslipLines(PayslipId)"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PayslipLines_PayslipId' AND object_id=OBJECT_ID('{T("PayslipLines")}')) CREATE INDEX IX_PayslipLines_PayslipId ON {T("PayslipLines")}([PayslipId])";
            await c4.ExecuteNonQueryAsync();
            logger.LogInformation("جدول‌های موتور حقوق داینامیک اضافه شدند (فاز ۲۴)");
        }

        // ---- فاز ۲۵: عیدی/سنوات سالانه + تسویه حساب ----
        if (ModOk("payroll") && !await TableExistsAsync("PayrollBonusRows"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE PayrollBonusRows (
                        Id TEXT NOT NULL CONSTRAINT PK_PayrollBonusRows PRIMARY KEY,
                        CompanyId TEXT NOT NULL, EmployeeId TEXT NOT NULL, JYear INTEGER NOT NULL,
                        Days TEXT NOT NULL, Base TEXT NOT NULL, Eid TEXT NOT NULL, Seniority TEXT NOT NULL,
                        Status TEXT NOT NULL, FinalizedBy TEXT NULL, FinalizedAt TEXT NULL,
                        CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("PayrollBonusRows")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_PayrollBonusRows PRIMARY KEY,
                        [CompanyId] nvarchar(450) NOT NULL, [EmployeeId] nvarchar(450) NOT NULL, [JYear] int NOT NULL,
                        [Days] decimal(18,2) NOT NULL, [Base] decimal(18,2) NOT NULL,
                        [Eid] decimal(18,2) NOT NULL, [Seniority] decimal(18,2) NOT NULL,
                        [Status] nvarchar(max) NOT NULL, [FinalizedBy] nvarchar(max) NULL, [FinalizedAt] datetime2 NULL,
                        [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NOT NULL)";
            await c2.ExecuteNonQueryAsync();
            await using var c3 = conn.CreateCommand();
            c3.CommandText = isSqlite
                ? "CREATE UNIQUE INDEX IF NOT EXISTS IX_PayrollBonusRows_CompanyId_JYear_EmployeeId ON PayrollBonusRows(CompanyId, JYear, EmployeeId)"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PayrollBonusRows_CompanyId_JYear_EmployeeId' AND object_id=OBJECT_ID('{T("PayrollBonusRows")}')) CREATE UNIQUE INDEX IX_PayrollBonusRows_CompanyId_JYear_EmployeeId ON {T("PayrollBonusRows")}([CompanyId],[JYear],[EmployeeId])";
            await c3.ExecuteNonQueryAsync();
            logger.LogInformation("جدول PayrollBonusRows اضافه شد (فاز ۲۵)");
        }
        if (ModOk("payroll") && !await TableExistsAsync("PayrollSettlements"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? @"CREATE TABLE PayrollSettlements (
                        Id TEXT NOT NULL CONSTRAINT PK_PayrollSettlements PRIMARY KEY,
                        CompanyId TEXT NOT NULL, EmployeeId TEXT NOT NULL,
                        EndDate TEXT NOT NULL, Days TEXT NOT NULL, LeaveBalance TEXT NOT NULL,
                        LeavePay TEXT NOT NULL, Daily TEXT NOT NULL, Eid TEXT NOT NULL, Seniority TEXT NOT NULL,
                        Status TEXT NOT NULL, CreatedBy TEXT NULL, FinalizedBy TEXT NULL, FinalizedAt TEXT NULL,
                        CreatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("PayrollSettlements")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_PayrollSettlements PRIMARY KEY,
                        [CompanyId] nvarchar(450) NOT NULL, [EmployeeId] nvarchar(450) NOT NULL,
                        [EndDate] datetime2 NOT NULL, [Days] decimal(18,2) NOT NULL, [LeaveBalance] decimal(18,2) NOT NULL,
                        [LeavePay] decimal(18,2) NOT NULL, [Daily] decimal(18,2) NOT NULL,
                        [Eid] decimal(18,2) NOT NULL, [Seniority] decimal(18,2) NOT NULL,
                        [Status] nvarchar(max) NOT NULL, [CreatedBy] nvarchar(max) NULL, [FinalizedBy] nvarchar(max) NULL,
                        [FinalizedAt] datetime2 NULL, [CreatedAt] datetime2 NOT NULL)";
            await c2.ExecuteNonQueryAsync();
            await using var c3 = conn.CreateCommand();
            c3.CommandText = isSqlite
                ? "CREATE INDEX IF NOT EXISTS IX_PayrollSettlements_EmployeeId ON PayrollSettlements(EmployeeId)"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PayrollSettlements_EmployeeId' AND object_id=OBJECT_ID('{T("PayrollSettlements")}')) CREATE INDEX IX_PayrollSettlements_EmployeeId ON {T("PayrollSettlements")}([EmployeeId])";
            await c3.ExecuteNonQueryAsync();
            logger.LogInformation("جدول PayrollSettlements اضافه شد (فاز ۲۵)");
        }

        // ---- فاز ۲۸: معماری چندشرکتی سراسری ----
        // CompanyId برای Positions/Devices/Zones/Gates/AccessGroups/Contractors/Visitors/Users
        await P24AddColumnAsync("Positions", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
        await P24AddColumnAsync("Devices", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
        await P24AddColumnAsync("Zones", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
        await P24AddColumnAsync("Gates", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
        await P24AddColumnAsync("AccessGroups", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
        await P24AddColumnAsync("Contractors", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
        await P24AddColumnAsync("Visitors", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");
        await P24AddColumnAsync("Users", "CompanyId", "TEXT NULL", "nvarchar(450) NULL");

        if (await TableExistsAsync("Companies"))
        {
            // Backfill پست‌های سازمانی از واحد سازمانی والد
            if (await TableExistsAsync("Positions") && await P24ColExistsAsync("Positions", "CompanyId"))
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? "UPDATE Positions SET CompanyId = (SELECT d.CompanyId FROM Departments d WHERE d.Id = Positions.DepartmentId) WHERE CompanyId IS NULL"
                    : $"UPDATE p SET p.CompanyId = d.CompanyId FROM {T("Positions")} p JOIN {T("Departments")} d ON d.Id = p.DepartmentId WHERE p.CompanyId IS NULL";
                await c2.ExecuteNonQueryAsync();
            }

            // سایر کاتالوگ‌ها → اولین شرکت (مالکیت پیش‌فرض داده‌های موجود)
            var firstCompany = "";
            await using (var c2 = conn.CreateCommand())
            {
                c2.CommandText = "SELECT Id FROM Companies ORDER BY CreatedAt LIMIT 1";
                if (!isSqlite) c2.CommandText = $"SELECT TOP 1 Id FROM {T("Companies")} ORDER BY CreatedAt";
                var r = await c2.ExecuteScalarAsync();
                firstCompany = r?.ToString() ?? "";
            }
            if (!string.IsNullOrEmpty(firstCompany))
            {
                foreach (var t in new[] { "Devices", "Zones", "Gates", "AccessGroups", "Contractors", "Visitors" })
                {
                    if (!await TableExistsAsync(t) || !await P24ColExistsAsync(t, "CompanyId")) continue;
                    await using var c3 = conn.CreateCommand();
                    c3.CommandText = $"UPDATE {T(t)} SET CompanyId = '{firstCompany}' WHERE CompanyId IS NULL";
                    await c3.ExecuteNonQueryAsync();
                }
            }

            // کاربران: حساب‌های خودکار پرسنل (emp:*) شرکت پرسنل خود را می‌گیرند (تطبیق با کد پرسنلی در نام کاربری)؛
            // بقیه حساب‌ها NULL می‌مانند (همه شرکت‌ها)
            // فاز ۶۹ — patch به جدول Employees ارجاع دارد؛ فقط با حضور خانوادهٔ org (ماژول غایب = نه جدول، نه کوئری)
            if (await TableExistsAsync("Employees") && await TableExistsAsync("Users") && await P24ColExistsAsync("Users", "CompanyId"))
            {
                await using var c4 = conn.CreateCommand();
                // emp:1001 → کد 1001؛ SQLite substr از ایندکس ۱، پس کد از کاراکتر ۵ است
                c4.CommandText = isSqlite
                    ? "UPDATE Users SET CompanyId = (SELECT e.CompanyId FROM Employees e WHERE e.PersonnelCode = substr(Users.Username, 5)) WHERE CompanyId IS NULL AND Username LIKE 'emp:%'"
                    : $"UPDATE u SET u.CompanyId = e.CompanyId FROM {T("Users")} u JOIN {T("Employees")} e ON e.PersonnelCode = SUBSTRING(u.Username, 5, 4000) WHERE u.CompanyId IS NULL AND u.Username LIKE 'emp:%'";
                await c4.ExecuteNonQueryAsync();
            }
        }

        // ایندکس‌های یکتا: کدها به‌ازای هر شرکت یکتا می‌شوند (قبلاً سراسری بود)
        if (await TableExistsAsync("Departments") || await TableExistsAsync("Positions")
            || await TableExistsAsync("Zones") || await TableExistsAsync("Gates"))
        {
            async Task RecodeIndexAsync(string table, string oldIndex, string newIndex)
            {
                if (!await TableExistsAsync(table)) return;
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? $"DROP INDEX IF EXISTS \"{oldIndex}\""
                    : $"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='{oldIndex}' AND object_id=OBJECT_ID('{T(table)}')) DROP INDEX [{oldIndex}] ON {T(table)}";
                await c2.ExecuteNonQueryAsync();
                await using var c3 = conn.CreateCommand();
                c3.CommandText = isSqlite
                    ? $"CREATE UNIQUE INDEX IF NOT EXISTS \"{newIndex}\" ON \"{table}\" (\"CompanyId\", \"Code\")"
                    : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='{newIndex}' AND object_id=OBJECT_ID('{T(table)}')) CREATE UNIQUE INDEX [{newIndex}] ON {T(table)}([CompanyId],[Code])";
                await c3.ExecuteNonQueryAsync();
            }
            await RecodeIndexAsync("Departments", "IX_Departments_Code", "IX_Departments_CompanyId_Code");
            await RecodeIndexAsync("Positions", "IX_Positions_Code", "IX_Positions_CompanyId_Code");
            // فاز ۶۷ — ایندکس‌های ماژول تردد فقط با لایهٔ دیتای حاضر
            if (ModOk("access"))
            {
                await RecodeIndexAsync("Zones", "IX_Zones_Code", "IX_Zones_CompanyId_Code");
                await RecodeIndexAsync("Gates", "IX_Gates_Code", "IX_Gates_CompanyId_Code");
            }
            logger.LogInformation("ایندکس‌های یکتای شرکت‌دار کاتالوگ‌ها بازسازی شدند (فاز ۲۸)");
        }

        // ---- فاز ۲۹: مخزن کارت‌های شناسایی + حالت درب ----
        // جدول Cards (برای دیتابیس‌های ساخته‌شده قبل از این فاز)
        if (ModOk("access") && !await TableExistsAsync("Cards"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? @"CREATE TABLE Cards (
                        Id TEXT NOT NULL CONSTRAINT PK_Cards PRIMARY KEY,
                        CardNo TEXT NOT NULL,
                        EmployeeId TEXT NULL,
                        Type TEXT NOT NULL DEFAULT 'Mifare',
                        Status TEXT NOT NULL DEFAULT 'آزاد',
                        IssuedAt TEXT NOT NULL,
                        ExpiresAt TEXT NULL,
                        ReturnedAt TEXT NULL,
                        Note TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL)"
                : $@"CREATE TABLE {T("Cards")} (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_Cards PRIMARY KEY,
                        [CardNo] nvarchar(450) NOT NULL,
                        [EmployeeId] nvarchar(450) NULL,
                        [Type] nvarchar(max) NOT NULL DEFAULT 'Mifare',
                        [Status] nvarchar(max) NOT NULL DEFAULT N'آزاد',
                        [IssuedAt] datetime2 NOT NULL,
                        [ExpiresAt] datetime2 NULL,
                        [ReturnedAt] datetime2 NULL,
                        [Note] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
            await using var idx = conn.CreateCommand();
            idx.CommandText = isSqlite
                ? "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Cards_CardNo\" ON \"Cards\" (\"CardNo\")"
                : $"CREATE UNIQUE INDEX [IX_Cards_CardNo] ON {T("Cards")}([CardNo])";
            await idx.ExecuteNonQueryAsync();
            logger.LogInformation("جدول Cards ساخته شد (فاز ۲۹)");
        }
        // ستون Gates.DoorState
        if (ModOk("access") && await TableExistsAsync("Gates"))
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? "SELECT COUNT(*) FROM pragma_table_info('Gates') WHERE name='DoorState'"
                : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T("Gates")}') AND name='DoorState'";
            if (Convert.ToInt32(await c2.ExecuteScalarAsync()) == 0)
            {
                await using var c3 = conn.CreateCommand();
                c3.CommandText = isSqlite
                    ? "ALTER TABLE Gates ADD COLUMN DoorState TEXT NOT NULL DEFAULT 'نرمال'"
                    : $"ALTER TABLE {T("Gates")} ADD DoorState nvarchar(max) NOT NULL DEFAULT N'نرمال'";
                await c3.ExecuteNonQueryAsync();
                logger.LogInformation("ستون Gates.DoorState اضافه شد (فاز ۲۹)");
            }
        }

        // ---- فاز ۳۰: نقشه سایت + دوعاملی اختیاری + حافظه آفلاین + TTL هشدارها ----
        async Task<bool> P30ColExistsAsync(string table, string col)
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{col}'"
                : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T(table)}') AND name='{col}'";
            return Convert.ToInt32(await c2.ExecuteScalarAsync()) > 0;
        }
        async Task P30AddColumnAsync(string table, string col, string sqliteType, string sqlType)
        {
            if (!await TableExistsAsync(table) || await P30ColExistsAsync(table, col)) return;
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? $"ALTER TABLE {table} ADD COLUMN {col} {sqliteType}"
                : $"ALTER TABLE {T(table)} ADD {col} {sqlType}";
            await c2.ExecuteNonQueryAsync();
            logger.LogInformation("ستون {Table}.{Col} اضافه شد (فاز ۳۰)", table, col);
        }
        if (ModOk("access") && await TableExistsAsync("Zones"))
        {
            await P30AddColumnAsync("Zones", "TwoFactor", "INTEGER NOT NULL DEFAULT 0", "bit NOT NULL DEFAULT 0");
            await P30AddColumnAsync("Zones", "TwoFactorWindowSec", "INTEGER NOT NULL DEFAULT 30", "int NOT NULL DEFAULT 30");
        }
        if (ModOk("access") && await TableExistsAsync("Gates"))
        {
            await P30AddColumnAsync("Gates", "MapX", "REAL NULL", "float NULL");
            await P30AddColumnAsync("Gates", "MapY", "REAL NULL", "float NULL");
        }
        if (await TableExistsAsync("SecurityAlerts"))
        {
            await P30AddColumnAsync("SecurityAlerts", "Expired", "INTEGER NOT NULL DEFAULT 0", "bit NOT NULL DEFAULT 0");
        }
        if (await TableExistsAsync("NotificationSettings"))
        {
            await P30AddColumnAsync("NotificationSettings", "AlertTtlDays", "INTEGER NOT NULL DEFAULT 0", "int NOT NULL DEFAULT 0");
        }
        // جدول DeviceOfflinePunches (حافظه آفلاین دستگاه)
        // فاز ۶۹ — فقط با ماژول devices حاضر (غایب = هیچ جدولی از خانواده‌اش ساخته نمی‌شود)
        if (ModOk("devices") && !await TableExistsAsync("DeviceOfflinePunches"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? @"CREATE TABLE DeviceOfflinePunches (
                        Id TEXT NOT NULL CONSTRAINT PK_DeviceOfflinePunches PRIMARY KEY,
                        DeviceId TEXT NOT NULL,
                        Pin TEXT NOT NULL,
                        Timestamp TEXT NOT NULL,
                        Source TEXT NOT NULL DEFAULT 'شبیه‌ساز',
                        ImportedAt TEXT NULL,
                        Note TEXT NULL,
                        CreatedAt TEXT NOT NULL)"
                : @"CREATE TABLE [DeviceOfflinePunches] (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_DeviceOfflinePunches PRIMARY KEY,
                        [DeviceId] nvarchar(450) NOT NULL,
                        [Pin] nvarchar(max) NOT NULL,
                        [Timestamp] datetime2 NOT NULL,
                        [Source] nvarchar(max) NOT NULL DEFAULT N'شبیه‌ساز',
                        [ImportedAt] datetime2 NULL,
                        [Note] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
            await using var idx = conn.CreateCommand();
            idx.CommandText = isSqlite
                ? "CREATE INDEX IF NOT EXISTS \"IX_DeviceOfflinePunches_DeviceId_ImportedAt\" ON \"DeviceOfflinePunches\" (\"DeviceId\", \"ImportedAt\")"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DeviceOfflinePunches_DeviceId_ImportedAt' AND object_id=OBJECT_ID('{T("DeviceOfflinePunches")}')) CREATE INDEX [IX_DeviceOfflinePunches_DeviceId_ImportedAt] ON {T("DeviceOfflinePunches")}([DeviceId],[ImportedAt])";
            await idx.ExecuteNonQueryAsync();
            logger.LogInformation("جدول DeviceOfflinePunches ساخته شد (فاز ۳۰)");
        }

        // ---- فاز ۳۱: تنظیمات برندینگ/پالت — جدول AppSettings ----
        if (!await TableExistsAsync("AppSettings"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? @"CREATE TABLE AppSettings (
                        Id TEXT NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY,
                        CompanyName TEXT NOT NULL,
                        CompanyLegalName TEXT NULL,
                        CompanyNationalId TEXT NULL,
                        CompanyPhone TEXT NULL,
                        CompanyEmail TEXT NULL,
                        CompanyWebsite TEXT NULL,
                        CompanyAddress TEXT NULL,
                        Description TEXT NULL,
                        LogoPath TEXT NULL,
                        AccentColor TEXT NULL,
                        SidebarColor TEXT NULL,
                        UpdatedAt TEXT NOT NULL,
                        UpdatedBy TEXT NULL)"
                : @"CREATE TABLE [AppSettings] (
                        [Id] nvarchar(450) NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY,
                        [CompanyName] nvarchar(max) NOT NULL,
                        [CompanyLegalName] nvarchar(max) NULL,
                        [CompanyNationalId] nvarchar(max) NULL,
                        [CompanyPhone] nvarchar(max) NULL,
                        [CompanyEmail] nvarchar(max) NULL,
                        [CompanyWebsite] nvarchar(max) NULL,
                        [CompanyAddress] nvarchar(max) NULL,
                        [Description] nvarchar(max) NULL,
                        [LogoPath] nvarchar(max) NULL,
                        [AccentColor] nvarchar(max) NULL,
                        [SidebarColor] nvarchar(max) NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        [UpdatedBy] nvarchar(max) NULL)";
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("جدول AppSettings ساخته شد (فاز ۳۱)");
        }

        // ---- فاز ۳۲: پالت‌های رنگ استاندارد — جدول ColorPalettes + ستون AppSettings.ActivePaletteId ----
        if (!await TableExistsAsync("ColorPalettes"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = isSqlite
                ? @"CREATE TABLE ColorPalettes (
                        Id TEXT NOT NULL CONSTRAINT PK_ColorPalettes PRIMARY KEY,
                        Name TEXT NOT NULL,
                        PrimaryColor TEXT NOT NULL,
                        LinkColor TEXT NOT NULL,
                        SidebarColor TEXT NOT NULL,
                        SidebarTextColor TEXT NOT NULL,
                        PageBgColor TEXT NOT NULL,
                        SurfaceColor TEXT NOT NULL,
                        TableHeaderColor TEXT NOT NULL,
                        RowAltColor TEXT NOT NULL,
                        RowHoverColor TEXT NOT NULL,
                        BorderColor TEXT NOT NULL,
                        TextColor TEXT NOT NULL,
                        MutedTextColor TEXT NOT NULL,
                        SuccessColor TEXT NOT NULL,
                        WarningColor TEXT NOT NULL,
                        DangerColor TEXT NOT NULL,
                        InfoColor TEXT NOT NULL,
                        IsBuiltIn INTEGER NOT NULL,
                        SortOrder INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL)"
                : @"CREATE TABLE [ColorPalettes] (
                        [Id] int NOT NULL IDENTITY(1,1) CONSTRAINT PK_ColorPalettes PRIMARY KEY,
                        [Name] nvarchar(100) NOT NULL,
                        [PrimaryColor] nvarchar(7) NOT NULL,
                        [LinkColor] nvarchar(7) NOT NULL,
                        [SidebarColor] nvarchar(7) NOT NULL,
                        [SidebarTextColor] nvarchar(7) NOT NULL,
                        [PageBgColor] nvarchar(7) NOT NULL,
                        [SurfaceColor] nvarchar(7) NOT NULL,
                        [TableHeaderColor] nvarchar(7) NOT NULL,
                        [RowAltColor] nvarchar(7) NOT NULL,
                        [RowHoverColor] nvarchar(7) NOT NULL,
                        [BorderColor] nvarchar(7) NOT NULL,
                        [TextColor] nvarchar(7) NOT NULL,
                        [MutedTextColor] nvarchar(7) NOT NULL,
                        [SuccessColor] nvarchar(7) NOT NULL,
                        [WarningColor] nvarchar(7) NOT NULL,
                        [DangerColor] nvarchar(7) NOT NULL,
                        [InfoColor] nvarchar(7) NOT NULL,
                        [IsBuiltIn] bit NOT NULL,
                        [SortOrder] int NOT NULL,
                        [CreatedAt] datetime2 NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
            await using var uidx = conn.CreateCommand();
            uidx.CommandText = isSqlite
                ? "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ColorPalettes_Name\" ON \"ColorPalettes\" (\"Name\")"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ColorPalettes_Name' AND object_id=OBJECT_ID('{T("ColorPalettes")}')) CREATE UNIQUE INDEX [IX_ColorPalettes_Name] ON {T("ColorPalettes")}([Name])";
            await uidx.ExecuteNonQueryAsync();
            logger.LogInformation("جدول ColorPalettes ساخته شد (فاز ۳۲)");
        }

        if (await TableExistsAsync("AppSettings"))
        {
            async Task<bool> AspColAsync(string col)
            {
                await using var c = conn.CreateCommand();
                c.CommandText = isSqlite
                    ? $"SELECT COUNT(*) FROM pragma_table_info('AppSettings') WHERE name='{col}'"
                    : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T("AppSettings")}') AND name='{col}'";
                return Convert.ToInt32(await c.ExecuteScalarAsync()) > 0;
            }
            if (!await AspColAsync("ActivePaletteId"))
            {
                await using var c = conn.CreateCommand();
                c.CommandText = isSqlite
                    ? "ALTER TABLE AppSettings ADD COLUMN ActivePaletteId INTEGER NULL"
                    : $"ALTER TABLE {T("AppSettings")} ADD [ActivePaletteId] int NULL";
                await c.ExecuteNonQueryAsync();
                logger.LogInformation("ستون AppSettings.ActivePaletteId اضافه شد (فاز ۳۲)");
            }
        }

        // ---- فاز ۴۱: دوربین پلاک‌خوان — ستون‌های جدید رویداد پارکینگ ----
        // (جدول‌های AnprCameras/AnprCommands جدید را CreateMissingTablesAsync می‌سازد؛
        //  اینجا فقط ستون‌های افزوده‌شده به جدول موجود ParkingEvents پچ می‌شوند)
        async Task<bool> P41ColExistsAsync(string col)
        {
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? $"SELECT COUNT(*) FROM pragma_table_info('ParkingEvents') WHERE name='{col}'"
                : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T("ParkingEvents")}') AND name='{col}'";
            return Convert.ToInt32(await c2.ExecuteScalarAsync()) > 0;
        }
        async Task P41AddColumnAsync(string col, string sqliteType, string sqlType)
        {
            if (!await TableExistsAsync("ParkingEvents") || await P41ColExistsAsync(col)) return;
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? $"ALTER TABLE ParkingEvents ADD COLUMN {col} {sqliteType}"
                : $"ALTER TABLE {T("ParkingEvents")} ADD {col} {sqlType}";
            await c2.ExecuteNonQueryAsync();
            logger.LogInformation("ستون ParkingEvents.{Col} اضافه شد (فاز ۴۱)", col);
        }
        if (ModOk("parking") && await TableExistsAsync("ParkingEvents"))
        {
            await P41AddColumnAsync("CameraId", "TEXT NULL", "nvarchar(450) NULL");
            await P41AddColumnAsync("PlateKey", "TEXT NULL", "nvarchar(max) NULL");
            await P41AddColumnAsync("Confidence", "INTEGER NULL", "int NULL");
        }

        // ---- فاز ۴۵: عکس پروفایل کاربران و پرسنل — ستون‌های PhotoPath ----
        async Task P45AddPhotoAsync(string table)
        {
            if (!await TableExistsAsync(table)) return;
            async Task<bool> colAsync()
            {
                await using var c2 = conn.CreateCommand();
                c2.CommandText = isSqlite
                    ? $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='PhotoPath'"
                    : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T(table)}') AND name='PhotoPath'";
                return Convert.ToInt32(await c2.ExecuteScalarAsync()) > 0;
            }
            if (await colAsync()) return;
            await using var c2 = conn.CreateCommand();
            c2.CommandText = isSqlite
                ? $"ALTER TABLE {table} ADD COLUMN PhotoPath TEXT NULL"
                : $"ALTER TABLE {T(table)} ADD PhotoPath nvarchar(400) NULL";
            await c2.ExecuteNonQueryAsync();
            logger.LogInformation("ستون {Table}.PhotoPath اضافه شد (فاز ۴۵)", table);
        }
        await P45AddPhotoAsync("Users");
        await P45AddPhotoAsync("Employees");

        // ---- فاز ۷۴: محدودیت رکوردی (Data Scope) — جدول RoleScopes با CreateMissingTablesAsync
        //      ساخته می‌شود (entity در مدل مرکب هست)؛ ستون Role.IsBuiltIn برای دیتابیس‌های موجود ----
        if (await TableExistsAsync("Roles"))
        {
            async Task<bool> RoleColAsync(string col)
            {
                await using var c = conn.CreateCommand();
                c.CommandText = isSqlite
                    ? $"SELECT COUNT(*) FROM pragma_table_info('Roles') WHERE name='{col}'"
                    : $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{T("Roles")}') AND name='{col}'";
                return Convert.ToInt32(await c.ExecuteScalarAsync()) > 0;
            }
            if (!await RoleColAsync("IsBuiltIn"))
            {
                await using var c = conn.CreateCommand();
                c.CommandText = isSqlite
                    ? "ALTER TABLE Roles ADD COLUMN IsBuiltIn INTEGER NOT NULL DEFAULT 0"
                    : $"ALTER TABLE {T("Roles")} ADD [IsBuiltIn] bit NOT NULL DEFAULT 0";
                await c.ExecuteNonQueryAsync();
                logger.LogInformation("ستون Roles.IsBuiltIn اضافه شد (فاز ۷۴)");
            }
        }
        // ایندکس‌های پشتیبان فیلترهای رکوردی — سرعت گزارش‌ها (فاز ۷۴)
        async Task P74IndexAsync(string table, string index, string cols)
        {
            if (!await TableExistsAsync(table)) return;
            await using var c = conn.CreateCommand();
            c.CommandText = isSqlite
                ? $"CREATE INDEX IF NOT EXISTS \"{index}\" ON \"{table}\" ({cols})"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='{index}' AND object_id=OBJECT_ID('{T(table)}')) CREATE INDEX [{index}] ON {T(table)} ({cols})";
            await c.ExecuteNonQueryAsync();
        }
        await P74IndexAsync("Employees", "IX_Employees_DepartmentId", "\"DepartmentId\"");
        await P74IndexAsync("Employees", "IX_Employees_CompanyId", "\"CompanyId\"");
        await P74IndexAsync("Payslips", "IX_Payslips_EmployeeId", "\"EmployeeId\"");
        await P74IndexAsync("DevicePunchLogs", "IX_DevicePunchLogs_CompanyId", "\"CompanyId\"");
        await P74IndexAsync("AttendanceRecords", "IX_AttendanceRecords_Date", "\"Date\"");
    }

    /// <summary>
    /// فاز ۱۱۰ — ترمیم یک‌بارهٔ مقدار جبران قمری ردیف تنظیمات (توضیح در محل فراخوانی).
    /// پس از ترمیم، ردیف علامت می‌خورد (HijriAdjustMigrated) تا انتخاب بعدی ادمین بازنویسی نشود.
    /// پایگاه نو با پیش‌فرض ‎-۱ سید می‌شود و اصلاً وارد این مسیر نمی‌شود.
    /// </summary>
    private static async Task PatchHijriAdjustDefaultAsync(AppDbContext db, ILogger logger)
    {
        try
        {
            var rows = await db.AppSettings.Where(s => !s.HijriAdjustMigrated).ToListAsync();
            if (rows.Count == 0) return;
            foreach (var s in rows)
            {
                if (s.HijriAdjust == 0) s.HijriAdjust = -1; // هم‌ترازی با تقویم قمری رسمی — پیش‌فرض محصول
                s.HijriAdjustMigrated = true;
            }
            await db.SaveChangesAsync();
            logger.LogInformation("جبران قمری ردیف‌های قدیمی به پیش‌فرض ‎-۱ ترمیم شد — {Count} ردیف", rows.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ترمیم جبران قمری انجام نشد — در اجرای بعدی دوباره تلاش می‌شود");
        }
    }

    /// <summary>
    /// فاز ۷۸b — وصال عمومی ستون‌های غایب از روی مدل EF (هر دو پروایدر SQLite/SQL Server).
    /// EnsureCreated روی پایگاه موجود هیچ ستونی اضافه نمی‌کند و وصال‌های دستی قبلی
    /// (P21/P24/P45/P74/P77) فقط ستون‌های شناخته‌شدهٔ زمان نوشتن را پوشش می‌دادند؛
    /// نتیجه: پایگاه‌های ساخته‌شده در نسخه‌های قدیمی‌تر هنگام کوئری ستون تازه
    /// «SqlException: Invalid column name» می‌گرفتند (مورد واقعی Users.DepartmentId فاز ۷۴).
    /// اینجا «فقط افزودنی» است: ستون‌های موجود و داده‌ها دست‌نخورده می‌مانند.
    /// </summary>
    private static async Task PatchMissingModelColumnsAsync(AppDbContext db, ILogger logger)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        var isSqlite = db.Database.IsSqlite();

        // ستون‌های واقعی هر جدول موجود در پایگاه — فاز ۸۲: کلید = «schema.table» روی SQL Server
        var dbTables = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = isSqlite
                ? "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"
                : "SELECT s.name + N'.' + t.name AS tbl, c.name AS col FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id JOIN sys.columns c ON c.object_id = t.object_id";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var t = r.GetString(0);
                if (!dbTables.TryGetValue(t, out var cols))
                    dbTables[t] = cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                cols.Add(isSqlite ? r.GetString(0) : r.GetString(1)); // SQLite: ستون از pragma می‌آید
            }
        }
        if (isSqlite)
        {
            foreach (var t in dbTables.Keys.ToList())
            {
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info(\"{t}\")";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) cols.Add(r.GetString(1));
                dbTables[t] = cols;
            }
        }

        var added = 0;
        foreach (var et in db.Model.GetEntityTypes())
        {
            var table = et.GetTableName();
            if (table is null || et.GetViewName() is not null) continue;
            // فاز ۸۲ — کلید جدول: SQLite نام ساده؛ SQL Server «schema.table» (اسکیمای ماژول)
            var key = isSqlite || string.IsNullOrEmpty(et.GetSchema())
                ? table
                : et.GetSchema() + "." + table;
            if (!dbTables.TryGetValue(key, out var cols)) continue; // جدول غایب = CreateMissingTablesAsync می‌سازد

            foreach (var p in et.GetProperties())
            {
                var col = p.GetColumnName(StoreObjectIdentifier.Table(table, et.GetSchema()));
                if (col is null || cols.Contains(col)) continue;
                // ستون‌هایی که با ALTER امن ساخته نمی‌شوند — هشدار و ادامه (شکست استارت‌آپ ممنوع)
                if (p.IsPrimaryKey() || ReferenceEquals(p, et.FindDiscriminatorProperty()) ||
                    p.GetComputedColumnSql() is not null || p.GetDefaultValueSql() is not null)
                {
                    logger.LogWarning("ستون {Table}.{Col} در پایگاه غایب است اما با ALTER امن ساخته نمی‌شود (PK/computed) — دستی برطرف شود", table, col);
                    continue;
                }

                var (sqlType, suffix) = SafeAddType(p, isSqlite);
                try
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = isSqlite
                        ? $"ALTER TABLE \"{table}\" ADD COLUMN \"{col}\" {sqlType}{suffix}"
                        : $"ALTER TABLE {Q(db, isSqlite, table)} ADD [{col}] {sqlType}{suffix}";
                    await cmd.ExecuteNonQueryAsync();
                    added++;
                    logger.LogInformation("ستون {Table}.{Col} افزوده شد (وصال عمومی اسکیما — فاز ۷۸b)", table, col);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "افزودن ستون {Table}.{Col} ناموفق بود — ادامهٔ استارت‌آپ", table, col);
                }
            }
        }
        if (added > 0)
            logger.LogInformation("وصال عمومی اسکیما: {Count} ستون غایب به جدول‌های موجود افزوده شد", added);
    }

    /// <summary>نوع و قید امن برای ALTER TABLE ADD COLUMN — مقدار پیش‌فرض برای ستون‌های غیرنال‌پذیر مدل.</summary>
    private static (string SqlType, string Suffix) SafeAddType(Microsoft.EntityFrameworkCore.Metadata.IReadOnlyProperty p, bool isSqlite)
    {
        var type = p.GetColumnType() ?? (isSqlite ? "TEXT" : "nvarchar(max)");

        if (p.IsNullable) return (type, isSqlite ? "" : " NULL");   // نال‌پذیر مدل — SQLite: بدون suffix = نال‌پذیر

        // ستون غیرنال‌پذیر در مدل — DEFAULT لازم است (SQLite: ADD COLUMN با NOT NULL فقط همراه DEFAULT مجاز است)
        var t = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
        if (t == typeof(bool)) return (type, " NOT NULL DEFAULT 0");
        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) ||
            t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort))
            return (type, " NOT NULL DEFAULT 0");
        if (t == typeof(decimal) || t == typeof(double) || t == typeof(float))
            return (type, " NOT NULL DEFAULT 0");
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset))
            return (type, isSqlite ? " NOT NULL DEFAULT '0001-01-01 00:00:00'" : " NOT NULL DEFAULT '0001-01-01'");
        if (t == typeof(Guid))
            return (type, isSqlite ? " NOT NULL DEFAULT ''" : " NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'");
        if (t == typeof(string))
            return (type, isSqlite ? " NOT NULL DEFAULT ''" : " NOT NULL DEFAULT N''");
        if (t == typeof(TimeSpan))
            return (type, isSqlite ? " NOT NULL DEFAULT '00:00:00'" : " NOT NULL DEFAULT '00:00:00'");

        return (type, isSqlite ? "" : " NULL"); // نوع ناشناخته — نال‌پذیر (امن‌ترین ALTER)
    }
}
