// ======================================================================
// فاز ۶۳ — قراردادهای دروازهٔ دستگاه‌ها (ماژولِ جایگزین سرویس ویندوزی)
// ----------------------------------------------------------------------
// سرویس ویندوزی مستقل (HrDeviceGateway) تا امروز با HTTP + حساب svc_bridge
// با سامانه حرف می‌زد. با این فاز همان کارکردها «درون» وب‌اپ به‌صورت ماژول
// اجرا می‌شوند: حلقه‌های poll دستگاه‌های TCP، پل ADMS (/iclock/cdata)،
// پل دوربین‌های پلاک‌خوان (/anpr/…) و حلقه ضربان/دستورات راهبند.
//
// قانون معماری (ادامهٔ فاز ۶۱): ماژول دروازه هیچ ارجاعی به هاست یا ماژول
// دیگر ندارد — داده/کارکرد فقط از طریق همین اینترفیس‌ها:
//   • IDeviceGatewayChannel  → پیاده‌سازی در هاست (پلتفرم؛ DeviceOps/BioSync)
//   • IParkingGatewaySink    → پیاده‌سازی در ماژول پارکینگ (وقتی نصب است)
//   • IModuleAvailability    → پیاده‌سازی در هاست (نصب/مجوز/کلید مدیر)
// هاست این پیاده‌سازی‌ها را بدون ارجاع ایستا از طریق IHrModuleBootstrapper
// (بازتاب در HrModuleRuntime) پیدا و راه می‌اندازد.
// ======================================================================
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Contracts;

// ---------------------------- DTOها ----------------------------

/// <summary>تنظیمات زندهٔ دروازه — از جدول GatewaySetting (همان منبع سرویس خارجی).
/// فاز ۶۴ — ListenPort: پورت اختصاصی پل ADMS؛ ماژول درون‌اپی اگر با پورت خود اپ فرق کند
/// یک relay شفاف TCP روی همین پورت می‌نشیند تا دستگاه‌های قدیمی (که به سرویس خارجی روی
/// همین پورت وصل بودند) بدون هیچ تغییر کانفیگی به همان آدرس برسند.</summary>
public sealed record GatewaySettingsDto(
    bool Enabled, int TcpTimeoutMs, int PollIntervalSec, int SweeperIntervalSec,
    int ConfigRefreshSec, int DefaultDevicePort, int DefaultCommKey,
    int ConfigVersion, string LogLevel, int ListenPort = 0);

/// <summary>دستگاه فعال از دید پلتفرم — همان چیزی که سرویس خارجی از api/gateway/config می‌گرفت.</summary>
public sealed record GatewayDeviceCfg(
    string Serial, string? Name, string Transport, string? Ip,
    int? Port, int? CommKey, int PollIntervalSec);

/// <summary>دستور صف‌شده برای دستگاه/دوربین — Payload همان JSON آزاد قبلی.</summary>
public sealed record GatewayCommandDto(string Id, string Type, Dictionary<string, object?>? Payload);

/// <summary>یک پانچ خام دستگاه (ATTLOG یا TCP).</summary>
public sealed record GatewayPunchDto(string Pin, string Timestamp, string? Status = null);

/// <summary>کاربر ثبت‌شده روی دستگاه (رویداد users).</summary>
public sealed record GatewayUserDto(
    string Pin, string? Name, int Uid, int Privilege, string? Card, string? Password, string? Group);

/// <summary>قالب هویتی خام (fp/face/card/pin) — Data: base64 یا شماره کارت.</summary>
public sealed record GatewayBioDto(
    string Pin, string Type, int Index, int Valid, int? Quality, string? Data, int Length);

/// <summary>دوربین پلاک‌خوان فعال از دید ماژول پارکینگ.</summary>
public sealed record GatewayCameraCfg(
    string Serial, string? Name, string? Ip, string? Brand, string? GateId, int HeartbeatSec);

/// <summary>خوانش تکی پلاک — قرارداد پل ANPR (نرمال‌شدهٔ جهت به فارسی توسط پل).</summary>
public sealed record GatewayPlateRead(string Plate, string? Direction, string? Timestamp, int? Confidence);

/// <summary>خوانش دسته‌ای پلاک — همان شکل webhook ({"reads":[…]}).</summary>
public sealed record GatewayPlateBatch(IReadOnlyList<GatewayPlateRead> Reads);

// ------------------------ کانال دروازه (هاست) ------------------------

/// <summary>
/// کانال ارتباط دروازه با پلتفرم — پیاده‌سازی در هاست؛ عین همان مسیرهایی که
/// webhook سرویس خارجی طی می‌کند (DeviceOps/BioSync) ولی درون‌پردازشی و
/// بدون احراز هویت HTTP (فراخوان فقط خود اپ است).
/// </summary>
public interface IDeviceGatewayChannel
{
    /// <summary>تنظیمات زندهٔ گیت‌وی (ردیف default جدول GatewaySetting — idempotent).</summary>
    Task<GatewaySettingsDto> GetSettingsAsync();

    /// <summary>دستگاه‌های فعال — قبل از آن sweep آفلاین‌ها هم اجرا می‌شود.</summary>
    Task<List<GatewayDeviceCfg>> GetDevicesAsync();

    /// <summary>دستورهای در انتظار دستگاه؛ consume=true → pending به delivered تغییر می‌کند.</summary>
    Task<List<GatewayCommandDto>> PullCommandsAsync(string serial, bool consume = true);

    /// <summary>گزارش وضعیت — online=false فقط ثبت نمی‌شود (سوئیپر خودش آفلاین را می‌یابد).</summary>
    Task PostStatusAsync(string serial, string? firmware, int? userCount, bool online);

    /// <summary>ورود پانچ‌ها با همان موتور webhook — خروجی: تعداد موفق.</summary>
    Task<int> PostPunchesAsync(string serial, List<GatewayPunchDto> punches);

    /// <summary>ثبت/به‌روزرسانی پرسنل دستگاه (BioSync.UpsertUsersAsync).</summary>
    Task PostUsersAsync(string serial, List<GatewayUserDto> users);

    /// <summary>ثبت قالب‌های هویتی (BioSync.UpsertBiosAsync).</summary>
    Task PostBiosAsync(string serial, List<GatewayBioDto> bios);

    /// <summary>نتیجه اجرای دستور روی دستگاه (acked/failed + متن نتیجه).</summary>
    Task PostAckAsync(string serial, string commandId, bool ok, string? result);

    /// <summary>خوانش پلاک — به ماژول پارکینگ می‌رسد (وقتی نصب است)؛ Body: JSON پاسخ webhook.</summary>
    Task<(int Status, string? Body)> PostPlateAsync(string serial, object payload);
}

// ---------------------- خدمات دوربین (ماژول پارکینگ) ----------------------

/// <summary>
/// خدمات دوربین‌های پلاک‌خوان — پیاده‌سازی در ماژول پارکینگ (AnprCamera/AnprCommands).
/// اگر پارکینگ در بسته نباشد، حلقه‌های دوربین در دروازه با پاسخ دوستانه از کار می‌افتند.
/// </summary>
public interface IParkingGatewaySink
{
    /// <summary>دوربین‌های فعال (شامل sweep آفلاین‌ها).</summary>
    Task<List<GatewayCameraCfg>> GetCamerasAsync();

    /// <summary>ضربان + دستورهای صف‌شدهٔ دوربین (consume=true → delivered).</summary>
    Task<List<GatewayCommandDto>> PullCameraCommandsAsync(string serial, bool consume = true);

    /// <summary>نتیجه اجرای دستور راهبند/ری‌استارت روی دوربین.</summary>
    Task AckCameraCommandAsync(string serial, string commandId, bool ok, string? result);

    /// <summary>خوانش پلاک → موتور مجوز پارکینگ؛ Body: JSON پاسخ webhook (ok/barrier/owner/…).</summary>
    Task<(int Status, string? Body)> IngestPlateAsync(string serial, object payload);
}

// ------------------------ در دسترس بودن ماژول ------------------------

/// <summary>
/// آیا ماژول با کد داده‌شده «فعال» است؟ — ترکیب سه لایهٔ کنترل فاز ۵۴–۵۸:
/// نصب در بیلد + کلید مدیر (modules.json) + سقف مجوز. پیاده‌سازی در هاست.
/// </summary>
public interface IModuleAvailability
{
    bool Enabled(string code);
}

// ------------------- بوت‌استرپ ماژول‌های دارای فرآیند -------------------

/// <summary>
/// ماژول‌هایی که علاوه بر کنترلر/ویو، سرویس پس‌زمینه یا endpoint مستقل دارند
/// (اکنون: دروازهٔ دستگاه‌ها — و ثبت خدمات دوربین در ماژول پارکینگ) این را
/// پیاده می‌کنند. هاست «بدون ارجاع ایستا» (بازتاب در HrModuleRuntime) این
/// کلاس‌ها را می‌یابد؛ در بسته‌ای که DLL ماژول حذف شده، کلاس وجود ندارد و
/// چیزی ثبت/نگاشت نمی‌شود — همان الگوی حذف فیزیکی فاز ۵۷.
/// </summary>
public interface IHrModuleBootstrapper
{
    /// <summary>کد ماژول مالک (برای لاگ و مرتب‌سازی).</summary>
    string ModuleCode { get; }

    /// <summary>ثبت سرویس‌های DI ماژول (singleton/scoped/hosted).</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>نگاشت endpointها (minimal API) روی اپ — پس از Build و پیش از Run.</summary>
    void ConfigureApp(WebApplication app);
}

/// <summary>یک AssemblyLoadContext مشترک برای همهٔ پلاگین‌ها (چیدمان تخت + پوشهٔ modules/).
/// فقط اسمبلی‌های «HrModules.*» از پوشه حل می‌شوند (وابستگی‌های بین‌ماژولی X → X.Data)؛
/// هرچه غیر از این باشد (Contracts/Core/هاست/EF/…) به Default ALC برمی‌گردد تا «هویت تایپ»
/// تک‌نسخه بماند — اگر Contracts در ALC هم لود شود، IHrDataModuleِ ماژول با اینترفیسِ هاست
/// از جنس متفاوت می‌شود و کشف همیشه صفر برمی‌گرداند (باگ واقعی نشست ۶۸).</summary>
internal sealed class HrPluginLoadContext : AssemblyLoadContext
{
    private readonly string[] _dirs;
    public HrPluginLoadContext(string[] dirs) : base("HrPlugins") => _dirs = dirs;

    protected override Assembly? Load(AssemblyName an)
    {
        if (an.Name is null || !an.Name.StartsWith("HrModules.", StringComparison.Ordinal)) return null;
        foreach (var d in _dirs)
        {
            try
            {
                var p = Path.Combine(d, an.Name + ".dll");
                if (File.Exists(p)) return LoadFromAssemblyPath(p);
            }
            catch { /* نادیده */ }
        }
        return null; // → Default ALC
    }
}

/// <summary>
/// رجیستری رانتایم ماژول‌ها — فاز ۶۸: هاست هیچ فهرست ایستایی از ماژول‌ها ندارد؛
/// هر چیزی که «وجود داشته باشد» همین‌جا در بوت کشف می‌شود.
/// دو محل کشف: کنار اپ (چیدمان تخت — بسته‌های فعلی) و پوشهٔ modules/ کنار اپ
/// (چیدمان پلاگینی — هر ماژول و لایهٔ دیتایش یک فایل جدا).
/// سه خانوادهٔ کشف‌شونده:
///   • IHrDataModule   → ثبت DbContext/قراردادها/سیدر + اسکیمای EF
///   • IHrUiModule     → بخش MVC + مانیفست کنترلر/منو/اکشن
///   • IHrModuleBootstrapper → سرویس‌های پس‌زمینه/endpoint (دروازهٔ دستگاه‌ها)
/// </summary>
public static class HrModuleRuntime
{
    private static readonly object Lock = new();
    private static bool _loaded;
    private static readonly List<Assembly> PluginAssemblies = new();
    private static List<IHrModuleBootstrapper>? _boots;
    private static List<IHrDataModule>? _data;
    private static List<IHrUiModule>? _ui;

    // فاز ۶۹ — ماژول‌های «ذاتیِ» هاست (فقط هستهٔ پلتفرم): هاست خودش را بدون نامِ
    // هیچ ماژول دیگری ثبت می‌کند؛ کشفِ بقیه از DLLها. _ui هنگام ثبت باطل می‌شود.
    private static readonly List<IHrUiModule> _intrinsic = new();

    /// <summary>ثبت ماژول ذاتی هاست (CoreModule) — در آغاز Program.cs، پیش از نخستین مصرف.
    /// این تنها «خودشناسیِ» هاست است؛ هیچ نام ماژول دیگری از این مسیر رد نمی‌شود.</summary>
    public static void RegisterIntrinsic(params IHrUiModule[] mods)
    {
        lock (Lock)
        {
            foreach (var m in mods)
                if (!_intrinsic.Any(x => x.Code.Equals(m.Code, StringComparison.OrdinalIgnoreCase)))
                    _intrinsic.Add(m);
            _ui = null; // کشف مجدد با ترکیب جدید
        }
    }

    // ------------------------ کشف و لود ------------------------

    /// <summary>پوشه‌های کاندید پلاگین — فقط موجودها.</summary>
    private static string[] PluginDirs()
    {
        var list = new List<string>();
        try
        {
            var baseDir = AppContext.BaseDirectory;
            list.Add(baseDir);
            var sub = Path.Combine(baseDir, "modules");
            if (Directory.Exists(sub)) list.Add(sub);
        }
        catch { /* نادیده */ }
        return list.ToArray();
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        var dirs = PluginDirs();
        // فاز ۶۸ — هاست دیگر ارجاع کامپایل‌زمانی به ماژول‌ها ندارد ⇒ اسمبلی‌های HrModules.*
        // در deps.json هاست نیستند و «Assembly.Load با نام» در Default ALC آن‌ها را نمی‌بیند
        // (FileNotFoundException — تجربهٔ 404 ماژول‌ها). پس همهٔ پلاگین‌ها از «مسیر فایل»
        // با ALC اختصاصی لود می‌شوند؛ وابستگی‌هایشان (Contracts/EF/هاست) از همان پوشه‌ها یا
        // Default ALC حل می‌شود — بدون ثبت دوبارهٔ تایپ‌ها (DLL در deps نیست ⇒ دوباره‌لودی نداریم).
        var alc = new HrPluginLoadContext(dirs);

        foreach (var dir in dirs)
        {
            string[] files;
            try
            {
                // فاز ۸۷ — ماژول‌های Platform.Modules.* (پارکینگ/ارباب‌رجوع) هم کشف شوند
                files = Directory.GetFiles(dir, "HrModules.*.dll")
                    .Concat(Directory.GetFiles(dir, "Platform.Modules.*.dll"))
                    .ToArray();
            }
            catch { continue; }
            foreach (var dll in files)
            {
                try
                {
                    var asm = alc.LoadFromAssemblyPath(dll);
                    if (!PluginAssemblies.Any(a => string.Equals(a.FullName, asm.FullName, StringComparison.Ordinal)))
                        PluginAssemblies.Add(asm);
                }
                catch (Exception ex)
                {
                    // فاز ۶۸ — تشخیص: DLL ناقص/ناخوانا نباید بی‌صدا رد شود (تجربهٔ 404 ماژول‌ها)
                    Console.Error.WriteLine($"[HrModuleRuntime] لود پلاگین ناموفق: {Path.GetFileName(dll)} — {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        Console.WriteLine($"[HrModuleRuntime] کشف پلاگین از {dirs.Length} پوشه — {PluginAssemblies.Count} اسمبلی: {string.Join(", ", PluginAssemblies.Select(a => a.GetName().Name))}");
    }

    /// <summary>اسمبلی‌های کشف‌شدهٔ پلاگین (X + X.Data + X.Views) — برای بخش‌های MVC هاست.</summary>
    public static IReadOnlyList<Assembly> PluginAsms() { lock (Lock) { EnsureLoaded(); return PluginAssemblies; } }

    // ------------------------ بوت‌استرپ‌ها (فرآیند/endpoint) ------------------------

    /// <summary>
    /// کشف بوت‌استرپ‌ها (یک‌بار) — زنجیرهٔ ارجاع اسمبلی ورودی + DLLهای پلاگین.
    /// در بستهٔ مشتریِ بدون ماژول، DLL در پوشه نیست و چیزی کشف نمی‌شود.
    /// </summary>
    public static IReadOnlyList<IHrModuleBootstrapper> Discover()
    {
        lock (Lock)
        {
            if (_boots is not null) return _boots;
            _boots = new List<IHrModuleBootstrapper>();
            ScanFor<IHrModuleBootstrapper>(_boots);
            _boots.Sort((a, b) => string.CompareOrdinal(a.ModuleCode, b.ModuleCode));
            return _boots;
        }
    }

    // ------------------------ لایه‌های دیتا ------------------------

    /// <summary>لایه‌های دیتای کشف‌شده — ملاک «نصب» ماژول در زمان اجرا.</summary>
    public static IReadOnlyList<IHrDataModule> DataModules()
    {
        lock (Lock)
        {
            if (_data is not null) return _data;
            _data = new List<IHrDataModule>();
            ScanFor<IHrDataModule>(_data);
            _data.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));
            return _data;
        }
    }

    /// <summary>ماژول با کد داده‌شده در این اجرا «نصب» است؟ — لایهٔ دیتا یا UI آن کشف شده باشد
    /// (ماژول‌های ذاتی مثل core از RegisterIntrinsic همین‌جا حاضرند). ماژول غایب = false
    /// — از این فاز برای «همهٔ» ماژول‌ها از جمله پایه‌های سابق org/devices یک معنا دارد.</summary>
    public static bool Installed(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return true;
        var c = code.Trim().ToLowerInvariant();
        return _intrinsic.Any(m => m.Code.Equals(c, StringComparison.OrdinalIgnoreCase))
            || DataModules().Any(m => m.Code.Equals(c, StringComparison.OrdinalIgnoreCase))
            || UiModules().Any(m => m.Code.Equals(c, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>ثبت سرویس‌های دیتای همهٔ ماژول‌های حاضر — Program.cs (پیکربندی جاری)
    /// و DbConfig (اتصال جدید ویزارد با useDb).</summary>
    public static void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb = null)
    {
        foreach (var m in DataModules())
        {
            try { m.ConfigureDataServices(services, configuration, useDb); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HrModuleRuntime] خطای ConfigureDataServices ماژول {m.Code}: {ex.Message}");
            }
        }
    }

    /// <summary>ترکیب اسکیمای همهٔ ماژول‌های حاضر در مدل EF هاست (AppDbContext.OnModelCreating)
    /// — دو گذر: اول مدل هر ماژول، بعد FKهای بین‌ماژولی (نوع هر دو طرف باید در مدل باشد).
    /// ----------------------------------------------------------------------
    /// فاز ۶۹ — گارد Ignore «داده‌محور» خانواده‌های غایب، درون همین متد:
    ///   ۱) قبل از هر ComposeModel، انواع موجود = «هستهٔ پلتفرم» (مرز منطقی هسته/ماژول)؛
    ///   ۲) خانواده‌های اعلامی ماژول‌های حاضر = IHrDataModule.FamilyTypes خودشان؛
    ///   ۳) پایان کار، هر نوعی در مدل که نه هسته است نه به خانوادهٔ ماژول حاضر
    ///      تعلق دارد، از navigation ماژول‌های حاضر «ترانزیتیو» کشیده شده (خانوادهٔ
    ///      ماژولِ غایب) → صریحاً Ignore تا صفر جدول فانتوم ساخته شود
    ///      (EF Ignore = حذف entity + FKهای اشاره‌کننده؛ آزمون‌شده — scripts/efignore-test).
    /// هاست هیچ نام/نوع/کد ماژولی را در کد خود ندارد؛ نصب تازه‌ای که هرگز ماژولی
    /// را نداشته هم بدون هیچ فایل حافظه‌ای درست رفتار می‌کند.</summary>
    public static void ComposeModules(ModelBuilder mb)
    {
        var coreTypes = new HashSet<Type>(mb.Model.GetEntityTypes().Select(e => e.ClrType));
        var mods = DataModules();
        var familyTypes = new HashSet<Type>(mods.SelectMany(m => m.FamilyTypes));

        foreach (var m in mods)
        {
            try { m.ComposeModel(mb); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HrModuleRuntime] خطای ComposeModel ماژول {m.Code}: {ex.Message}");
            }
        }
        foreach (var m in mods)
        {
            try { m.ComposeCrossModule(mb); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HrModuleRuntime] خطای ComposeCrossModule ماژول {m.Code}: {ex.Message}");
            }
        }

        foreach (var et in mb.Model.GetEntityTypes().ToList())
        {
            var t = et.ClrType;
            if (coreTypes.Contains(t) || familyTypes.Contains(t)) continue;
            mb.Ignore(t);
        }

        // فاز ۸۲ — اسکیمای دیتابیس هر ماژول (درخواست کاربر): خانوادهٔ نوع‌های هر ماژول
        // در اسکیمای خودش (کد ماژول) و هستهٔ پلتفرم در اسکیمای core می‌نشیند.
        // روی SQL Server اسکیمای واقعی ساخته/جابه‌جا می‌شود (DbInit)؛ SQLite اسکیما ندارد
        // و EF آن را نادیده می‌گیرد (پروب‌شده — DDL/کوئری بدون اسکیما، مثل قبل).
        ApplySchemas(mb);
    }

    /// <summary>فاز ۸۲ — نگاشت اسکیمای ماژول‌محور روی مدل داده‌شده: هر نوعِ خانوادهٔ
    /// ماژول حاضر ← کد ماژول؛ بقیه (هستهٔ پلتفرم + ترانزیتیوها) ← core.
    /// کانتکست‌های دیتای ماژول‌ها (HrModelDefaults.Normalize) هم از همین نگاشت استفاده
    /// می‌کنند تا با مدل مرکب هاست هم‌نام و هم‌اسکیما باشند (قرارداد واحد — صفر واگرایی).</summary>
    public static void ApplySchemas(ModelBuilder mb)
    {
        var byType = new Dictionary<Type, string>();
        foreach (var m in DataModules())
            foreach (var t in m.FamilyTypes)
                if (!byType.ContainsKey(t)) byType[t] = m.Code;
        foreach (var et in mb.Model.GetEntityTypes())
            et.SetSchema(byType.TryGetValue(et.ClrType, out var code) ? code : "core");
    }

    /// <summary>سید همهٔ ماژول‌های حاضر — دو فاز: PreSeed (دادهٔ مرجع مثل شیفت‌ها) و Seed
    /// (دادهٔ اولیه/نمایشی). جایگزین بلوک‌های #if داخل DbSeeder هاست.
    /// preSeed=false یعنی فاز Seed — ترتیب با (SeedOrder, Code): فاز ۶۹ — org=10 پرسنل می‌سازد،
    /// devices=20 دستگاه‌ها؛ بعد ماژول‌های سیستم (تردد/کنترل تردد/…) با ctx.Employees/DeviceIds پرشده کار می‌کنند.</summary>
    public static async Task SeedModulesAsync(HrSeedContext ctx, bool preSeed)
    {
        var mods = DataModules().OrderBy(m => preSeed ? 0 : m.SeedOrder).ThenBy(m => m.Code, StringComparer.Ordinal).ToList();
        foreach (var m in mods)
        {
            try
            {
                if (preSeed) await m.PreSeedAsync(ctx);
                else await m.SeedAsync(ctx);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HrModuleRuntime] خطای {(preSeed ? "PreSeed" : "Seed")} ماژول {m.Code}: {ex.Message}");
            }
        }
    }

    /// <summary>پاک‌کردن داده‌های نمایشی همهٔ ماژول‌های حاضر (جایگزین ClearXxxDemo هاست).</summary>
    public static async Task ClearDemoModulesAsync(DbContext db)
    {
        foreach (var m in DataModules())
        {
            try { await m.ClearDemoAsync(db); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HrModuleRuntime] خطای ClearDemo ماژول {m.Code}: {ex.Message}");
            }
        }
    }

    /// <summary>آبشار حذف شرکت روی داده‌های همهٔ ماژول‌های حاضر (جایگزین بلوک‌های #if CompaniesController).
    /// هاست اول جدول‌های پایه را پاک می‌کند و بعد این قلاب را صدا می‌زند.</summary>
    public static async Task CascadeCompanyDeleteAsync(DbContext db, string companyId)
    {
        foreach (var m in DataModules())
        {
            try { await m.OnCompanyDeleteAsync(db, companyId); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HrModuleRuntime] خطای حذف آبشاری شرکت در ماژول {m.Code}: {ex.Message}");
            }
        }
    }

    /// <summary>شمارش‌های صفحهٔ شرکت‌ها از پلاگین دیتای ماژول داده‌شده (kind = نوع شمارش مثل shifts/zones/runs).</summary>
    public static async Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string mod, string kind)
    {
        var m = DataModules().FirstOrDefault(x => x.Code.Equals(mod.Trim(), StringComparison.OrdinalIgnoreCase));
        if (m is null) return new Dictionary<string, int>();
        try { return await m.CompanyCountsAsync(db, kind); }
        catch (Exception ex) { Console.Error.WriteLine($"[HrModuleRuntime] خطای CompanyCounts ماژول {mod}: {ex.Message}"); return new(); }
    }

    /// <summary>شمارش برای یک شرکت از پلاگین دیتای ماژول داده‌شده.</summary>
    public static async Task<int> CompanyCountAsync(DbContext db, string mod, string kind, string companyId)
    {
        var m = DataModules().FirstOrDefault(x => x.Code.Equals(mod.Trim(), StringComparison.OrdinalIgnoreCase));
        if (m is null) return 0;
        try { return await m.CompanyCountAsync(db, kind, companyId); }
        catch (Exception ex) { Console.Error.WriteLine($"[HrModuleRuntime] خطای CompanyCount ماژول {mod}: {ex.Message}"); return 0; }
    }

    /// <summary>شرکت‌های دارای پیکربندی در «هر» پلاگین دیتا (اجتماع).</summary>
    public static async Task<List<string>> CompaniesWithConfigAsync(DbContext db)
    {
        var all = new List<string>();
        foreach (var m in DataModules())
        {
            try { all.AddRange(await m.CompaniesWithConfigAsync(db)); }
            catch (Exception ex) { Console.Error.WriteLine($"[HrModuleRuntime] خطای CompaniesWithConfig ماژول {m.Code}: {ex.Message}"); }
        }
        return all;
    }

    // ------------------------ بخش‌های UI ------------------------

    /// <summary>ماژول‌های UI کشف‌شده — مانیفست کنترلر/منو/اکشن هاست از همین‌ها ساخته می‌شود.
    /// فاز ۶۹ — ماژول ذاتی هاست (core) هم در همین فهرست است؛ هاست «همهٔ» مانیفست‌ها
    /// را از یک مسیر می‌خواند و هیچ نام ماژولی نمی‌شناسد.</summary>
    public static IReadOnlyList<IHrUiModule> UiModules()
    {
        lock (Lock)
        {
            if (_ui is not null) return _ui;
            _ui = new List<IHrUiModule>(_intrinsic);
            ScanFor<IHrUiModule>(_scan);
            foreach (var m in _scan)
                if (!_ui.Any(x => x.Code.Equals(m.Code, StringComparison.OrdinalIgnoreCase)))
                    _ui.Add(m);
            _scan.Clear();
            _ui.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));
            return _ui;
        }
    }
    private static readonly List<IHrUiModule> _scan = new();

    // ------------------------ موتور پیمایش ------------------------

    /// <summary>پیمایش اسمبلی‌ها (پلاگین‌ها + زنجیرهٔ ارجاع ورودی + AppDomain) و
    /// جمع‌کردن پیاده‌سازی‌های T — مقاوم به ReflectionTypeLoadException.</summary>
    private static void ScanFor<T>(List<T> into) where T : class
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Assembly>();

        // ۱) پلاگین‌های کشف‌شده (X / X.Data / X.Views)
        foreach (var a in PluginAsms()) queue.Enqueue(a);

        // ۲) زنجیرهٔ ارجاع اسمبلی ورودی + هرچه تا الان لود شده (سازگاری فازهای قبل)
        var entry = Assembly.GetEntryAssembly();
        if (entry is not null) queue.Enqueue(entry);
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            if (!seen.Contains(a.FullName ?? string.Empty)) queue.Enqueue(a);

        while (queue.Count > 0)
        {
            var asm = queue.Dequeue();
            if (!seen.Add(asm.FullName ?? string.Empty)) continue;
            foreach (var rn in asm.GetReferencedAssemblies())
            {
                try
                {
                    var loaded = Assembly.Load(rn);
                    if (!seen.Contains(loaded.FullName ?? string.Empty)) queue.Enqueue(loaded);
                }
                catch { /* اسمبلی اختیاری — نادیده */ }
            }

            Type?[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle)
            {
                types = rtle.Types ?? Array.Empty<Type?>();
            }
            catch { types = Array.Empty<Type?>(); }
            foreach (var t in types)
            {
                try
                {
                    if (t is null || !t.IsClass || t.IsAbstract || t.IsGenericTypeDefinition) continue;
                    if (!typeof(T).IsAssignableFrom(t)) continue;
                    if (Activator.CreateInstance(t) is T inst) into.Add(inst);
                }
                catch { /* نوع ناقص/سازندهٔ خصوصی — نادیده */ }
            }
        }
    }

    // ------------------------ چرخهٔ حیات (فراخوانی هاست) ------------------------

    /// <summary>هاست در فاز builder صدا می‌زند — ثبت سرویس‌های بوت‌استرپ‌ها.</summary>
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        foreach (var b in Discover())
        {
            try { b.ConfigureServices(services, configuration); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HrModuleRuntime] خطای ConfigureServices ماژول {b.ModuleCode}: {ex.Message}");
            }
        }
    }

    /// <summary>هاست پس از Build و پیش از Run صدا می‌زند — نگاشت endpointهای ماژول‌ها.</summary>
    public static void ConfigureApp(WebApplication app)
    {
        foreach (var b in Discover())
        {
            try { b.ConfigureApp(app); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HrModuleRuntime] خطای ConfigureApp ماژول {b.ModuleCode}: {ex.Message}");
            }
        }
    }
}
