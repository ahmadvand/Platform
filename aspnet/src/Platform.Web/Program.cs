using System.Text;
using System.Threading.RateLimiting;
using Platform.Web.Data;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- فاز ۹۲ — اعمال به‌روزرسانی در صف (مکانیزم به‌روزرسانی پنل ادمین) ----------
// اگر apply.json موجود باشد (نصب به‌روزرسانی که در زمان اجرا کامل نشده بود)، فایل‌های
// استیجینگ پیش از ساخت اپ روی نصب کپی می‌شوند — سپس DbInit مهاجرت‌های EF را می‌آورد.
Platform.Web.Infrastructure.UpdateApplier.RunIfPending(builder.Environment.ContentRootPath);

// ---------- فاز ۸۵ — OWASP A02: پنهان‌سازی هویت سرور ----------
// هدر «Server: Kestrel» نساز — نسخه/فناوری سرور به بیرون لو نمی‌رود.
// فاز ۱۰۶ — کاهش ریشهٔ «NetworkError» پنل رمز بیدارسازی: پیش‌فرض KeepAliveTimeout=۱۳۰ثانیه
// بود و پس از idle محافظ، مرورگر POST را روی اتصال کهنه می‌فرستاد و fetch می‌شکست؛ با ۱۰
// دقیقه اتصال بیکار محافظ زنده می‌ماند (سمت کلاینت هم retry شبکه‌ای در hr-screensaver.js).
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
});

// ---------- فاز ۶۹ — خودشناسی هاست: تنها ماژول ذاتی (هستهٔ پلتفرم) ----------
// هاست فقط «خودش» را می‌شناسد؛ همهٔ ماژول‌های دیگر (حتی پایه‌های سابق org/devices)
// پلاگین‌های HrModules.*.dll هستند و در بوت کشف می‌شوند. باید پیش از نخستین
// مصرف HrModuleRuntime/ModuleCatalog ثبت شود.
Platform.Contracts.HrModuleRuntime.RegisterIntrinsic(
    new Platform.Web.Modules.CoreModule());

// ---------- فاز ۴۵ — تنظیمات نصب ----------
// فاز ۵۱ — پیکربندی نصب دیگر در فایل JSON «متنی» نیست؛ ویزارد راه‌اندازی/صفحه تنظیمات آن را
// رمزشده در hrsettings.bin می‌نویسند (Data Protection — SetupSecrets) و این منبع پیکربندی،
// همان کلیدها را (با مهاجرت خودکار از hrsettings.json قدیمی) برای IConfiguration فراهم می‌کند.
// نصب‌های قدیمی این فایل را ندارند → سازگاری کامل: کلید Setup:Configured غایب = پیکربندی‌شده.
((Microsoft.Extensions.Configuration.IConfigurationBuilder)builder.Configuration).Add(
    new Platform.Web.Infrastructure.SetupSecrets.Source(builder.Environment.ContentRootPath));

// ---------- Serilog (فاز ۱۹) — کنسول + فایل چرخشی ----------
// فایل‌ها در logs/log-.txt (روزشده) — نگهداری ۳۱ روز؛ پروداکشن: مسیر/سطح از appsettings.json
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/log-.txt",
        rollingInterval: Serilog.RollingInterval.Day,
        retainedFileCountLimit: 31,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// ---------- انکودر HTML سازگار با فارسی ----------
// انکودر پیش‌فرض همه حروف فارسی را به ‎&#x…;‎ تبدیل می‌کند (حجم صفحه بالا و تست سخت می‌شود)
builder.Services.AddSingleton(System.Text.Encodings.Web.HtmlEncoder.Create(
    System.Text.Unicode.UnicodeRanges.BasicLatin,
    System.Text.Unicode.UnicodeRanges.Arabic,
    System.Text.Unicode.UnicodeRanges.Latin1Supplement,
    System.Text.Unicode.UnicodeRanges.GeneralPunctuation));

// ---------- MVC ----------
// فاز ۶۸ — بخش‌های MVC فقط برای پلاگین‌های «کشف‌شده» در بوت (چیدمان تخت + پوشهٔ modules/).
// PopulateDefaultParts پیش‌فرض برای هر ApplicationPartAttribute (که در زمان کامپایل برای
// ارجاع‌های پروژه تولید می‌شود) Assembly.Load می‌زند؛ هاست دیگر ارجاع کامپایل‌زمانی به
// ماژول‌ها ندارد پس PopulateDefaultParts اجرا نمی‌شود — ثبت به‌صورت «نمونهٔ آماده».
var hrPartManager = new ApplicationPartManager();
var hrEntry = System.Reflection.Assembly.GetEntryAssembly();
// فاز ۶۷ — SafeAssemblyPart: اگر حتی یک نوعِ اسمبلی به DLL غایب وابسته باشد
// (closure/state-machine متدهای async ماژول‌محور)، GetTypes معمولی کل اپ را می‌کشد؛
// شمارش نوع‌به‌نوع فقط انواع ناقص را کنار می‌گذارد (توضیح کامل: Infrastructure/SafeMvc.cs)
if (hrEntry is not null) hrPartManager.ApplicationParts.Add(new SafeAssemblyPart(hrEntry));
foreach (var hrPlugAsm in Platform.Contracts.HrModuleRuntime.PluginAsms())
    hrPartManager.ApplicationParts.Add(new SafeAssemblyPart(hrPlugAsm));
// همان تأمین‌کننده‌های ویژگی عمومی که MVC خودش اضافه می‌کند (کشف کنترلر/تگ‌هلپر/ViewComponent)
hrPartManager.FeatureProviders.Add(new Microsoft.AspNetCore.Mvc.Controllers.ControllerFeatureProvider());
hrPartManager.FeatureProviders.Add(new Microsoft.AspNetCore.Mvc.Razor.TagHelpers.TagHelperFeatureProvider());
hrPartManager.FeatureProviders.Add(new Microsoft.AspNetCore.Mvc.ViewComponents.ViewComponentFeatureProvider());
// تأمین‌کنندهٔ ویوهای Razor در NET8. داخلی است — پس از Build با بازتاب اضافه می‌شود (پایین)
builder.Services.AddSingleton(hrPartManager);
builder.Services.AddControllersWithViews();

// موتور اعلان‌ها — اجرای دوره‌ای پس‌زمینه (فاز ۱۳)
builder.Services.AddHostedService<NotificationWorker>();
// پشتیبان‌گیری زمان‌بندی‌شدهٔ دیتابیس + نگهداشت N نسخه (فاز ۱۳۵ — مورد ۲۰)
builder.Services.AddHostedService<DbBackupWorker>();

// ---------- پایداری کلیدهای DataProtection (تسک ۲۷) ----------
// در سندباکس، محل پیش‌فرض (~/.aspnet) بین ری‌استارت‌ها پاک می‌شود؛ با کلید ناپایدار
// کوکی‌های آنتی‌فورجری پس از هر ری‌استارت نامعتبر می‌شوند (خطای 400 در همه POSTها).
// کلیدها در پوشه پروژه ذخیره می‌شوند تا ری‌استارت سرور نشست‌ها و توکن‌ها را نشکند.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/home/z/my-project/aspnet/keys"))
    .SetApplicationName("HrAttendance.Web");

// ---------- Antiforgery سازگار با iframe پنل پیش‌نمایش ----------
// کوکی پیش‌فرض SameSite=Strict است و در iframe بین‌دامنه‌ای ارسال نمی‌شود → خطای 400 در POST.
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "hr_af";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.None;
    // Always روی HTTP ساده Exception می‌دهد (زنجیره پراکسی داخلی HTTP است)؛
    // ویژگی Secure در میان‌افزار زیر به‌صورت دستی اضافه می‌شود.
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// ---------- فاز ۸۵ — OWASP A04: رمز JWT هرگز نباید مقدار پیش‌فرض/خالی بماند ----------
// قالب appsettings.json عبارت «CHANGE-ME…» دارد؛ اگر نصب آن را عوض نکرده باشد، هر کسی که سورس را
// دیده باشد می‌تواند توکن امضا کند (اشکال رمزنگاری جدی). رفع: اگر Secret خالی یا پیش‌فرض باشد،
// یک کلید تصادفی ۳۸۴ بیتی ساخته و در App_Data/jwt-secret.key ماندگار می‌شود (بدون آن، با هر
// ری‌استارت نشست‌ها می‌شکست). اولویت همیشه با مقدار صریح پیکربندی است؛ حذف فایل = چرخش کلید.
{
    var cfgJwtSecret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(cfgJwtSecret) || cfgJwtSecret.StartsWith("CHANGE-ME"))
    {
        var jwtKeyPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "jwt-secret.key");
        Directory.CreateDirectory(Path.GetDirectoryName(jwtKeyPath)!);
        if (!File.Exists(jwtKeyPath))
        {
            File.WriteAllBytes(jwtKeyPath, System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
            try { File.SetUnixFileMode(jwtKeyPath,
                System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite); } catch { /* ویندوز: بی‌اثر */ }
        }
        builder.Configuration["Jwt:Secret"] = Convert.ToBase64String(File.ReadAllBytes(jwtKeyPath));
    }
}

// ---------- فاز ۸۵ — OWASP A06/A07: محدودسازی نرخ سراسری (دفاع در برابر سیلاب/Brute-force) ----------
// دو لایه مستقل از کنترل موجود ورود (AuthCore: قفل حساب + سقف ۱۰ تلاش/۵ دقیقه به‌ازای IP+نام‌کاربری):
//   • مسیرهای ورود (Auth/Login, Auth/LoginEmployee, api/auth/login, api/auth/refresh):
//     پنجرهٔ ثابت ۶۰ درخواست در دقیقه به‌ازای هر IP — حدس رمز توزیع‌شده را هم گران می‌کند.
//   • بقیهٔ ترافیک داینامیک: توکن‌باکت سخاوتمندانه (انفجار ۶۰۰ + پرشدن ۲۰۰/ثانیه) —
//     ترافیک انسانی/تست هرگز لمس نمی‌شود؛ فقط سیلاب خام بند می‌آید.
// فایل‌های استاتیک و /dl از میان‌افزار بیرون‌اند (جایگاه UseRateLimiter) و گیت‌وی دستگاه‌ها
// (/iclock, /anpr) با فرکانس واقعی خود (~چند درخواست/دقیقه) بی‌ضرر جا می‌شود.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        ctx.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync(
            HrT.T("درخواست‌های شما بیش از حد مجاز است — کمی بعد دوباره تلاش کنید"), ct);
    };
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpCtx =>
    {
        var rl = httpCtx.RequestServices.GetRequiredService<IConfiguration>();
        var path = httpCtx.Request.Path.Value ?? "/";
        // فاز ۸۵ — در استقرار پشت پراکسی، IP واقعی از X-Forwarded-For (رفتار پیش‌فرض هم‌سان با
        // حسابرسی/ورود)؛ در استقرار مستقیم روی Kestrel با Security:RateLimit:TrustForwardedIp=false
        // هدر جعلی مشتری نمی‌تواند پارتیشن محدودسازی نرخ را دور بزند (RemoteIpAddress معیار است).
        var trustXff = rl.GetValue("Security:RateLimit:TrustForwardedIp", true);
        var ip = trustXff
            ? (AuthCore.IpOf(httpCtx) ?? "unknown")
            : (httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        var isLogin = httpCtx.Request.Method == HttpMethods.Post && (
            path.StartsWith("/Auth/Login", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase));
        if (isLogin)
            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rl.GetValue("Security:RateLimit:LoginPermitPerMinute", 60),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
        return RateLimitPartition.GetTokenBucketLimiter(ip, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = rl.GetValue("Security:RateLimit:BurstCapacity", 600),
            TokensPerPeriod = rl.GetValue("Security:RateLimit:TokensPerSecond", 200),
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

// ---------- بانک داده: SQL Server پیش‌فرض | SQLite (سندباکس/توسعه) ----------
// فاز ۴۵: گزینه‌ها در هر Scope از پیکربندی جاری ساخته می‌شوند تا تغییر اتصال از طریق
// ویزارد /Setup یا صفحه تنظیمات، بدون ری‌استارت برنامه در همان لحظه اعمال شود.
// فاز ۴۸: NeedsSetup علاوه بر کلید Setup:Configured، رشتهٔ اتصال مؤثر خالی را هم «نیازمند
// راه‌اندازی» می‌داند تا استارتاپ با «The ConnectionString property has not been initialized» سقوط نکند.
var setupNeeded = Platform.Web.Infrastructure.DbConfig.NeedsSetup(builder.Configuration);
builder.Services.AddScoped<AppDbContext>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new AppDbContext(Platform.Web.Infrastructure.DbConfig.BuildOptions(cfg).Options);
});

// ---------- فاز ۶۱ + فاز ۶۸ — لایهٔ دیتای پایه + پلاگین‌های دیتای کشف‌شده ----------
// کانتکست پایه (هست پلتفرم) همیشه؛ کانتکست‌ها/قراردادها/سیدرهای ماژول‌ها فقط برای
// پلاگین‌های کشف‌شده در بوت (IHrDataModule — بدون هیچ فهرست ایستایی در هاست).
builder.Services.AddScoped<HrBaseDbContext>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new HrBaseDbContext(Platform.Web.Infrastructure.DbConfig.BuildOptionsFor<HrBaseDbContext>(cfg).Options);
});
// فاز ۶۸ — جایگزین‌های بی‌اثر «پیش از» پلاگین‌ها: آخرین ثبت برنده است؛ ماژول حاضر = پیاده‌سازی واقعی، غایب = Null بی‌خطر.
// قراردادها مصرف‌کنندهٔ هاست‌اند (از Contracts) — هیچ نام ماژولی اینجا نیست.
Platform.Web.Modules.HrModuleFallbacks.RegisterAttendance(builder.Services);
Platform.Web.Modules.HrModuleFallbacks.RegisterVisitors(builder.Services);
Platform.Web.Modules.HrModuleFallbacks.RegisterAccess(builder.Services);
Platform.Web.Modules.HrModuleFallbacks.RegisterParking(builder.Services);
// فاز ۶۹ — جایگزین‌های بی‌خطر org/devices (پلاگین حاضر = پیاده‌سازی واقعی؛ غایب = Null)
Platform.Web.Modules.HrModuleFallbacks.RegisterPeople(builder.Services);
Platform.Web.Modules.HrModuleFallbacks.RegisterDevices(builder.Services);
Platform.Contracts.HrModuleRuntime.ConfigureDataServices(builder.Services, builder.Configuration);

// ---------- فاز ۶۳ — دروازهٔ دستگاه‌ها (ماژول جایگزین سرویس ویندوزی) ----------
// کانال قراردادی دروازه + در دسترس بودن ماژول‌ها؛ خود ماژول (حلقه‌ها/endpointها)
// بدون ارجاع ایستا و از طریق بوت‌استرپ بازتابی (IHrModuleBootstrapper) راه می‌افتد —
// در بستهٔ مشتری بدون ماژول دروازه هیچ‌کدام ثبت نمی‌شود (صفر ریسک).
builder.Services.AddScoped<Platform.Contracts.IDeviceGatewayChannel, Platform.Web.Services.DeviceGatewayChannel>();
// فاز ۶۷ — Sink پارکینگ برای دروازه (IAuditSink در هاست است — همیشه؛ IVisitorsGateway/IAccessGateway
// از همین ثبت مشروط بالا می‌آیند و دروازه با IModuleAvailability حاضرها را می‌شناسد)
builder.Services.AddScoped<Platform.Contracts.IAuditSink, AuditSink>();
builder.Services.AddSingleton<Platform.Contracts.IModuleAvailability, Platform.Web.Modules.ModuleAvailability>();
Platform.Contracts.HrModuleRuntime.ConfigureServices(builder.Services, builder.Configuration);

// ---------- JWT ----------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddSingleton<JwtService>();
// فاز ۷۴ — سرویس RBAC نسخه‌دار (اعمال فوری دسترسی‌ها + تطبیق wildcard/والد)
builder.Services.AddSingleton<RbacService>();
// فاز ۷۴ — دامنهٔ دسترسی به رکورد (Row-Level) با کش نسخه‌دار per-user
// (نوع‌های مشترک UserScope/ScopeQuery ∈ Core؛ اینجا فقط پیاده‌سازی provider)
builder.Services.AddSingleton<DataScopeService>();
builder.Services.AddSingleton<IUserScopeProvider>(sp => sp.GetRequiredService<DataScopeService>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
// پرتال خودخدمتی پرسنل (فاز ۱۴) — کوکی امضاشده DataProtection
builder.Services.AddSingleton<EssAuth>();
// تنظیمات برندینگ/پالت (فاز ۳۱) — اطلاعات شرکت، لوگو و رنگ‌های سراسری
builder.Services.AddScoped<SettingsService>();
// مجوز نصب (فاز ۴۶) — قفل سخت‌افزاری + امضای RSA؛ Singleton برای کش وضعیت و اثر انگشت
builder.Services.AddSingleton<LicenseService>();
// فاز ۷۷ — مخزن زبان‌های پویا (پنل ادمین /Langs + بستهٔ ترجمهٔ TranslationStudio)
builder.Services.AddSingleton<HrLangStore>();
// رجیستری ماژول‌ها (فاز ۵۴) — وضعیت فعال/غیرفعال فایل‌پشتیبان + سقف مجوز؛ Singleton برای کش
builder.Services.AddSingleton<Platform.Web.Modules.ModuleStateService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var opt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()!;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opt.Issuer,
            ValidateAudience = true,
            ValidAudience = opt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        };
        // پشتیبانی از کوکی hr_at (مرورگر) علاوه بر هدر Authorization (سرویس پل)
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (string.IsNullOrEmpty(ctx.Token))
                    ctx.Token = ctx.Request.Cookies[JwtService.AccessCookie];
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// فاز ۷۷ — اتصال مخزن زبان‌های پویا به HrT (ترجمهٔ زنده از DB با کش حافظه‌ای)
HrT.Configure(app.Services.GetRequiredService<HrLangStore>());

// ---------- فاز ۶۷ — تأمین‌کنندهٔ ویوهای Razor (در NET8. داخلی است) ----------
// با کارخانهٔ ApplicationPartManager خودمان، MVC دیگر آن را اضافه نمی‌کند؛ ViewsFeature
// تنبل پر می‌شود، پس افزودن پس از Build و پیش از Run کافی است — بدون آن حتی ویوهای خود اپ پیدا نمی‌شدند.
try
{
    var razorAsm = typeof(Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions).Assembly;
    var fpType = razorAsm.GetTypes().FirstOrDefault(t => t.Name == "RazorCompiledItemFeatureProvider");
    if (Activator.CreateInstance(fpType!) is Microsoft.AspNetCore.Mvc.ApplicationParts.IApplicationFeatureProvider razorFp)
        app.Services.GetRequiredService<ApplicationPartManager>().FeatureProviders.Add(razorFp);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "افزودن تأمین‌کنندهٔ ویوهای Razor ناموفق بود");
}

// ---------- فاز ۶۳ — endpointهای ماژول‌های دارای بوت‌استرپ (دروازهٔ دستگاه‌ها) ----------
// نگاشت /iclock/cdata و /anpr/… — در بستهٔ بدون ماژول هیچ endpointی اضافه نمی‌شود.
Platform.Contracts.HrModuleRuntime.ConfigureApp(app);

// ---------- مقداردهی اولیه (فقط پس از پیکربندی کامل) ----------
// نصب تازه / رشته اتصال خالی: DbInit اجرا نمی‌شود؛ گیت میانه همه مسیرها را به ویزارد /Setup می‌فرستد و
// خودِ ویزارد پس از ذخیره پیکربندی، بانک را می‌سازد و مقداردهی می‌کند.
// فاز ۴۸: حتی وقتی پیکربندی هست اما اتصال به هر دلیل ناموفق است (سرور خاموش، رشته غلط،
// نبود دسترسی) برنامه سقوط نمی‌کند — حالت تعمیر (RepairMode) فعال و پیام خطا در /Setup دیده می‌شود.
if (!setupNeeded)
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            await DbInit.InitializeAsync(scope.ServiceProvider);
        }
        SetupState.MarkReady();
    }
    catch (Exception ex)
    {
        SetupState.MarkFailure(SetupState.FriendlyError(ex.InnerException?.Message ?? ex.Message));
        app.Logger.LogError(ex, "راه‌اندازی اولیه بانک در استارتاپ ناموفق بود — سامانه در حالت تعمیر بالا آمد؛ اتصال را از /Setup اصلاح کنید");
    }
}
else
{
    app.Logger.LogInformation("سامانه هنوز پیکربندی نشده است — ویزارد راه‌اندازی اولیه در /Setup در انتظار است");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // ---------- HSTS (فاز ۱۹) — فقط در پروداکشن و با کلید پیکربندی ----------
    // در سندباکس پشت پراکسی HTTP غیرفعال است؛ در استقرار واقعی (HTTPS) Security:EnableHsts=true کنید.
    if (builder.Configuration.GetValue("Security:EnableHsts", false))
    {
        app.UseHsts();
    }
}

// ---------- فاز ۸۵ — OWASP A02: هدرهای امنیتی روی همهٔ پاسخ‌ها (استاتیک + داینامیک) ----------
// پیش از UseStaticFiles تا پاسخ‌های استاتیک هم هدر بگیرند؛ با Security:Headers:Enabled=false خاموش.
app.UseMiddleware<Platform.Web.Infrastructure.SecurityHeadersMiddleware>();

// ---------- اجازه نمایش در iframe پنل پیش‌نمایش پلتفرم ----------
// ۱) سیستم Antiforgery هنگام تولید توکن فرم‌ها «X-Frame-Options: SAMEORIGIN» می‌فرستد
//    و پنل پیش‌نمایش (iframe) را بلاک می‌کند — حذف می‌شود.
// ۲) کوکی‌های SameSite=None به ویژگی Partitioned (CHIPS) نیاز دارند تا Firefox
//    در حالت Private/ETP آن‌ها را در iframe ذخیره کند — .NET 8 این ویژگی را
//    در CookieOptions ندارد، پس دستی به هدر Set-Cookie اضافه می‌شود.
// در پروداکشن می‌توانید با Security:AllowEmbedding=false غیرفعالش کنید.
var allowEmbedding = builder.Configuration.GetValue("Security:AllowEmbedding", true);
if (allowEmbedding)
{
    app.Use(async (ctx, next) =>
    {
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers.Remove("X-Frame-Options");
            var sc = ctx.Response.Headers.SetCookie;
            if (sc.Count > 0)
            {
                var list = new List<string>(sc.Count);
                var modified = false;
                foreach (var raw in sc)
                {
                    var v = raw ?? string.Empty;
                    if (v.Contains("samesite=none", StringComparison.OrdinalIgnoreCase))
                    {
                        // ویژگی‌ها پس از «;» می‌آیند؛ مقدار کوکی هرگز «;» ندارد
                        if (!v.Contains("; secure", StringComparison.OrdinalIgnoreCase))
                        {
                            v += "; Secure";
                            modified = true;
                        }
                        if (!v.Contains("; partitioned", StringComparison.OrdinalIgnoreCase))
                        {
                            v += "; Partitioned";
                            modified = true;
                        }
                    }
                    list.Add(v);
                }
                if (modified)
                    ctx.Response.Headers.SetCookie = new Microsoft.Extensions.Primitives.StringValues(list.ToArray());
            }
            return Task.CompletedTask;
        });
        await next();
    });
}

app.UseStaticFiles();

// ---------- مرکز دانلود و مرورگر سورس (فاز ۵۸ — فاز ۱۰۶: مهاجرت به مسیر جدید به درخواست کاربر) ----------
// پوشهٔ wwwroot/dlc — zipهای بستهٔ مشتری + بسته‌های محافظ صفحه + درخت سورس (پوشه در
// .gitignore است؛ داخل بستهٔ مشتری publish نمی‌شود). مسیر قبلی «/dl» به‌طور کامل حذف
// شد (پوشه + مپینگ) تا هیچ نسخهٔ قدیمی سرو نشود. فایل‌های سورس پسوندهای غیرمتعارف
// دارند (.cs/.csproj/.cshtml) و StaticFiles پیش‌فرض برای پسوند ناشناخته 404 می‌دهد؛
// این گزینه فقط به بازهٔ /dlc محدود است و روی بقیهٔ مسیرها اثری ندارد.
var dlRoot = Path.Combine(app.Environment.WebRootPath
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "dlc");
if (Directory.Exists(dlRoot))
{
    // فاز ۱۰۶ (ترمیم 2.7.19) — درخواست «/dlc» بدون اسلش: میانهٔ StaticFiles برای
    // دایرکتوری‌ها ریدایرکت ۳۰۱ «مطلق» می‌سازد و اسکیم را از خودِ درخواست می‌گیرد؛ پشت
    // لبهٔ https (پروکسی http) لوکیشن «http://…» تولید می‌شد و مرورگر به پورت ۸۰ می‌افتاد
    // و «Connection timed out» می‌خورد. rewrite داخلی بدون هیچ ریدایرکتی مشکل را ریشه‌کن
    // می‌کند: «/dlc» همان‌جا به «/dlc/» تبدیل و index.html مستقیم سرو می‌شود.
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path == "/dlc")
            ctx.Request.Path = "/dlc/";
        await next();
    });
    // نشانی دایرکتوری «/dlc/» (و «/dlc») خودِ index.html را سرو کند —
    // نه فقط «/dlc/index.html».
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        RequestPath = "/dlc",
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dlRoot),
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        RequestPath = "/dlc",
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dlRoot),
        ServeUnknownFileTypes = true,
        DefaultContentType = "application/octet-stream",
    });
}

// ---------- فاز ۸۵ — OWASP A06: محدودسازی نرخ (پس از استاتیک/مرکز دانلود تا دانلودها شمرده نشوند) ----------
app.UseRateLimiter();

// ---------- فاز ۷۶ — تعیین زبان رابط کاربری (چندزبانه) ----------
// کوکی hr_lang ← کلید Ui:Language (انتخاب نصب اولیه در ویزارد) ← fa.
// پیش از همهٔ گیت‌ها تا پیام‌های میانی هم ترجمه‌پذیر باشند.
app.UseMiddleware<Platform.Web.Infrastructure.CultureMiddleware>();

// ---------- گیت ویزارد راه‌اندازی اولیه (فاز ۴۵ + فاز ۴۸) ----------
// بدون پیکربندی کامل یا در حالت تعمیر (شکست اتصال استارتاپ): همه چیز (به‌جز فایل‌های
// استاتیک، /Setup، /health و /api) به /Setup هدایت می‌شود.
// پس از پیکربندی سالم: مسیرهای /Setup دیگر معنا ندارند و به داشبورد برمی‌گردند.
app.Use(async (ctx, next) =>
{
    var cfg = ctx.RequestServices.GetRequiredService<IConfiguration>();
    var path = ctx.Request.Path;
    if (Platform.Web.Infrastructure.DbConfig.NeedsSetup(cfg) || Platform.Web.Infrastructure.SetupState.RepairMode)
    {
        // فاز ۹۲ — /Ui/Lang هم مجاز است: سوییچ زبانِ خود ویزارد (مثل صفحهٔ ورود) از
        // مسیر سروری استفاده می‌کند تا کوکی hr_lang با سیاست درست ست شود (نوشتن کوکی
        // samesite=lax از JS داخل iframe بین‌سایتی توسط مرورگرها نادیده گرفته می‌شد).
        if (path.StartsWithSegments("/Setup") || path.StartsWithSegments("/health") || path.StartsWithSegments("/Ui/Lang"))
        {
            await next();
            return;
        }
        if (path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = 503;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsync("{\"ok\":false,\"error\":\"" + HrT.T("سامانه هنوز راه‌اندازی نشده است") + "\"}");
            return;
        }
        // فاز ۶۳ — پل‌های ماژول گیت‌وی مثل /api: تا پیش از راه‌اندازی، پاسخ صریح (نه ریدایرکت ویزارد)
        if (path.StartsWithSegments("/iclock") || path.StartsWithSegments("/anpr"))
        {
            ctx.Response.StatusCode = 503;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            await ctx.Response.WriteAsync("SERVER NOT CONFIGURED");
            return;
        }
        ctx.Response.Redirect("/Setup");
        return;
    }
    if (path.StartsWithSegments("/Setup"))
    {
        ctx.Response.Redirect("/");
        return;
    }
    await next();
});

// ---------- گیت مجوز نصب (فاز ۴۶ — بازطراحی فاز ۵۱) ----------
// فاز ۸۳ب: سامانه همیشه «فقط با مجوز» است (License:Mode دیگر خوانده نمی‌شود) — بدون مجوز
// معتبر، وضعیت Locked است و ناوبری کامل صفحه به صفحهٔ هشدار /License/Notice می‌رود.
// جایگاه: پس از UseAuthentication (برای دانستن «کاربر وارد‌شده» در ctx.User) و پیش از UseAuthorization.
app.UseRouting();
app.UseAuthentication();
// فاز ۱۰۳ — گیت درخواست‌ها حین قفل محافظ صفحه (درخواست کاربر: «درخواست‌های AJAX حین قفل را هم
// سمت سرور محدود کن»): AJAX/فرم‌پستِ غیرمعاف حین قفل رد می‌شود (423 / ریدایرکت)؛ ناوبری کامل GET
// عبور می‌کند و همان اول قفل رندر می‌شود. مسیرهای معاف: VerifyWakePassword/ScreensaverLock/Logout/health.
app.UseMiddleware<Platform.Web.Infrastructure.ScreensaverLockGate>();
// فاز ۷۴ — تازه‌سازی ادعای perms از RbacService (اعمال فوری تغییر دسترسی‌ها بدون ورود مجدد)
app.UseMiddleware<Platform.Web.Infrastructure.RbacRefreshMiddleware>();
app.UseMiddleware<Platform.Web.Infrastructure.LicenseGate>();
// گیت ماژول‌ها (فاز ۵۴) — مسیرهای ماژول خاموش می‌بندد (صفحه: ریدایرکت دوستانه، API: ۵۰۳، AJAX: ۴۰۳)
app.UseMiddleware<Platform.Web.Infrastructure.ModuleGate>();
app.UseAuthorization();

// ---------- Health endpoint (فاز ۱۹) — برای مانیتورینگ/لودبالانسر ----------
// فاز ۸۵ — OWASP A10: جزئیات خطای دیتابیس به بیرون نمی‌رود (فقط در لاگ Serilog)؛
// مانیتورینگ همان «degraded + ۵۰۳» را می‌بیند و هیچ قابلیتی از دست نمی‌دهد.
app.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        return Results.Ok(new { status = "healthy", db = db.Database.ProviderName ?? "?", time = DateTime.Now });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "بررسی سلامت: بانک داده در دسترس نیست");
        return Results.Json(new { status = "degraded" }, statusCode: 503);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.Run();
