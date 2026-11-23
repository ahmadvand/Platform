// doc-core.js — سند ۲: راهنمای ساخت پروژهٔ هسته‌محور از صفر
const H = require("./helpers");
const { PALETTES, p, h1, h2, h3, bullet, code, note, tbl } = H;
const pal = PALETTES["GO-1"];

const body = [];

body.push(h1("۱. هدف و دامنه — هسته یعنی چه؟", pal));
body.push(p("این سند نقشهٔ ساخت همان سامانه از صفر است، اما فقط با هستهٔ اصلی: احراز هویت و کاربران و نقش‌ها، شرکت‌ها و چارت سازمانی و پرسنل، تجهیزات تردد و پل دستگاه‌ها، و زیرساخت مشترک (ویزارد راه‌اندازی، مجوز نصب، به‌روزرسانی، گزارش‌ساز پایه، زبان‌ها). ماژول‌های حضور و غیاب، کنترل تردد و حراست، حقوق و دستمزد، پارکینگ و ارباب‌رجوع در این دامنه نیستند — ولی معماری طوری ساخته می‌شود که بعداً به‌صورت ماژول به آن افزوده شوند، بدون تغییر هاست."));
body.push(...tbl({
  palette: pal, caption: "جدول ۱ — اجزای هسته در برابر ماژول‌های الحاقی",
  headers: ["وضعیت", "اجزا"],
  widths: [24, 76],
  rows: [
    ["هستهٔ الزامی", "قرارداد ماژول‌ها، هاست و CoreModule (شرکت‌ها، کاربران/نقش‌ها/دسترسی‌ها، تنظیمات، زبان‌ها، اعلان‌ها، حسابرسی، مجوز، به‌روزرسانی)، ماژول سازمان (چارت/پست/پرسنل/تعطیلات)، ماژول تجهیزات و دروازهٔ دستگاه‌ها"],
    ["الصاقی در آینده", "حضور و غیاب، کنترل تردد و حراست، حقوق و دستمزد، پارکینگ، ارباب‌رجوع — هرکدام دو پروژهٔ X و X.Data با همان قرارداد"],
  ],
}));
body.push(note("اصل طراحی: هاست چیزی دربارهٔ ماژول‌ها نمی‌داند. کشف بازتابی DLL، گیت بر مانیفست کنترلرها، و اینترفیس‌های Contracts — همین سه ضلع باعث می‌شود حذف یا افزودن ماژول، هیچ تغییری در هاست نخواهد داشت.", pal, { bold: true }));

body.push(h1("۲. پیش‌نیازها و ساختار اولیه", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۲ — پیش‌نیازهای محیط توسعه و استقرار",
  headers: ["پیش‌نیاز", "نسخه/مقدار", "توضیح"],
  widths: [30, 24, 46],
  rows: [
    [".NET SDK", "8.0 LTS", "dotnet از مسیر رسمی — روی سرور فقط Runtime کافی است"],
    ["بانک داده", "SQLite 3 یا SQL Server 2019+", "SQLite بدون نصب؛ SQL Server برای سازمان"],
    ["مرورگر", "نسخه‌های جدید Chrome/Firefox/Edge", "رابط فارسی RTL با فونت Vazirmatn"],
    ["پورت‌ها", "3000 (وب)، 8081 (پل ADMS)، 4370 (دستگاه)", "در فایروال باز شوند"],
  ],
}));
body.push(...code([
  "# ساخت Solution و پروژه‌ها",
  "dotnet new sln -n Platform",
  "dotnet new classlib -o src/Platform.Contracts -f net8.0",
  "dotnet new classlib -o src/Platform.Core      -f net8.0",
  "dotnet new web      -o src/Platform.Web       -f net8.0",
  "dotnet new classlib -o src/modules/HrModules.Org.Data        -f net8.0",
  "dotnet new classlib -o src/modules/HrModules.Org             -f net8.0",
  "dotnet new classlib -o src/modules/HrModules.Devices         -f net8.0",
  "dotnet new classlib -o src/modules/HrModules.DeviceGateway   -f net8.0",
  "dotnet sln add (ls -r **/*.csproj)",
  "",
  "# زنجیرهٔ رفرنس",
  "# Contracts: بدون وابستگی",
  "# Core     -> Contracts",
  "# Web      -> Core + Contracts + همهٔ پروژه‌های UI ماژول‌ها",
  "# X.Data   -> Contracts + Core    # X (UI) -> X.Data + Core + Contracts",
], { caption: "گام ۰ — دستورات شکل‌گیری Solution" }));

body.push(h1("۳. قرارداد ماژول‌ها — پلتفرم قراردادها", pal));
body.push(p("ابتدا دو اینترفیس اصلی را در Contracts بنویسید؛ همهٔ بقیهٔ سامانه به همین دو قرارداد تکیه دارد. تعریف کامل اعضا در سند معماری آمده؛ اینجا اسکلت لازم برای هسته:"));
body.push(...code([
  "// IHrDataModule — پروژه‌های *.Data پیاده می‌کنند",
  "public interface IHrDataModule {",
  "  string Code { get; } string Title { get; } string Icon { get; } string Description { get; }",
  "  int SeedOrder => 100;",
  "  Type[] FamilyTypes => [];                       // مالکیت اسکیما",
  "  void ConfigureDataServices(IServiceCollection s, IConfiguration c,",
  "                            Action<DbContextOptionsBuilder>? useDb);",
  "  void ComposeModel(ModelBuilder mb);             // پاس ۱",
  "  void ComposeCrossModule(ModelBuilder mb);       // پاس ۲ — FK بین‌ماژولی",
  "  Task SeedAsync(HrSeedContext ctx);              // idempotent",
  "  Task OnCompanyDeleteAsync(DbContext db, string companyId);",
  "}",
  "",
  "// IHrUiModule — پروژه‌های UI ماژول پیاده می‌کنند",
  "public interface IHrUiModule {",
  "  string Code { get; } string Title { get; } string Icon { get; }",
  "  bool IsCore => false;",
  "  string[] Controllers { get; }                   // مانیفست برای ModuleGate",
  "  HrNavGroup[] NavGroups => [];                   // آیتم‌های منو با Perm",
  "}",
], { caption: "اسکلت قرارداد — ساده‌شده از ModuleSystem.cs واقعی" }));
body.push(bullet([H.t("ماژول‌یاب مرکزی ("), H.t("HrModuleRuntime", { rtl: false }), H.t("): کشف DLLها، فراخوانی ComposeModel و اجرای Seed همهٔ ماژول‌ها")]));
body.push(bullet([H.t("گیت ماژول ("), H.t("ModuleGate", { rtl: false }), H.t("): مسدودسازی دوستانهٔ مسیرهای ماژولِ خاموش یا نصب‌نشده")]));
body.push(bullet("پیاده‌سازی‌های خالی (Null-Fallback): پاسخ امن هنگام نبودِ یک ماژول و اینترفیس‌های بین‌ماژولی آن"));
body.push(p("در هستهٔ صفر فقط CoreModule به‌صورت داخلی ثبت می‌شود و ماژول‌یاب چیز دیگری پیدا نمی‌کند — سامانه باید دقیقاً همین‌طور سالم بالا بیاید."));

body.push(h1("۴. موجودیت‌های هسته و لایهٔ داده", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۳ — موجودیت‌های هسته (HrBaseDbContext)",
  headers: ["گروه", "موجودیت‌ها"],
  widths: [26, 74],
  rows: [
    ["سازمانی", "Company، Department (خودارجاع با ParentId)، Position، Employee، Contract، Decree"],
    ["هویت و دسترسی", "User، Role، Permission، UserRole، RolePermission، RoleScope (kind: all/company/dept/self)"],
    ["توکن و رمز", "RefreshToken، PasswordHistory، PasswordReset"],
    ["پایش و محتوا", "AuditLog، AppSetting، ColorPalette، SecurityAlert، HrLangDef، HrLangEntry، NotificationRule/Log/Setting"],
    ["تجهیزات (ماژول Devices)", "Device، DeviceCommand، DeviceUser، DeviceBiometric، EmployeeBiometric، DeviceOfflinePunch، DevicePunchLog، GatewaySetting، Holiday"],
  ],
}));
body.push(bullet("قواعد ستون از روز اول: شناسه رشته‌ای سقف ۶۴ نویسه، اعشار 18/2، ایندکس یکتا روی Username و Code و Key و TokenHash."));
body.push(bullet("برای چند-بانکی بودن، از ابتدا دو Context بسازید: HrBaseDbContext (فقط هسته) و AppDbContext ترکیبی که ComposeModel ماژول‌های حاضر را اضافه می‌کند."));
body.push(bullet("دو Provider را از ابتدا پشتیبانی کنید: UseSqlite و UseSqlServer با انتخاب از Database:Provider؛ در SQL Server نام schema هر جدول = کد ماژول (core، org، devices...)."));
body.push(bullet("به‌جای EF Migrations، سازوکار خودترمیم بنویسید: EnsureCreated، سپس مقایسهٔ مدل با بانک و ساخت جدول‌ها/ستون‌های جدید در هر استارت — الگوی DbInit واقعی سامانه."));

body.push(h1("۵. امنیت: هش، توکن و کوکی", pal));
body.push(...code([
  "// PasswordService — PBKDF2-SHA256",
  "// قالب ذخیره: pbkdf2-sha256$<iterations>$<saltB64>$<hashB64>",
  "Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);",
  "verify: CryptographicOperations.FixedTimeEquals(hash, computed);",
  "",
  "// JWT + کوکی‌ها",
  "Issuer=hr-attendance  Audience=hr-attendance-clients  ClockSkew=Zero",
  "hr_at : access JWT  (60 min, HttpOnly, Secure)",
  "hr_rt : refresh token rotating (7 days, hash Sha256 در بانک)",
  "hr_af : antiforgery (SameSite=None برای نصب درون iframe)",
], { caption: "قراردادهای امنیتی هسته" }));
body.push(bullet("قفل ورود: ۵ خطا = قفل ۱۵ دقیقه (HTTP 423) + محدودیت ۱۰ تلاش در ۵ دقیقه به تفکیک IP و نام کاربری."));
body.push(bullet("سیاست رمز: حداقل ۸ نویسه شامل حرف و عدد، ممنوعیت تکرار ۳ رمز آخر، انقضای ۹۰ روزه."));
body.push(bullet("کلید امضای JWT نباید مقدار پیش‌فرض بماند: اگر خالی بود، کلید تصادفی بسازید و در فایل جدا (خارج از بانک) ذخیره کنید."));

body.push(h1("۶. RBAC و دامنهٔ داده", pal));
body.push(p("سه جدول Role و Permission و نقشه‌های UserRole/RolePermission، به‌علاوه RoleScope برای دامنهٔ رکوردی. کلید دسترسی قالب module.area.action دارد؛ تطبیق به ترتیب: دقیق، ستاره، wildcard نیمه (a.b.*)، سپس والد. نقش‌ها با ParentRoleId ارث می‌برند (گارد چرخه تا ۱۲ سطح). نقش‌های پایهٔ هسته: superadmin (ستاره)، hr_manager، operator، security، viewer، device_agent و employee (پرتال). دامنهٔ داده با RoleScope: اجتماع دامنهٔ نقش‌های کاربر، تقاطع با سقف شرکت کاربر، گسترش زیرشاخه‌های واحد با BFS؛ در کنترلر با ScopeAsync به شرط‌های SQL ترجمه می‌شود."));
body.push(p("میان‌افزار RbacRefresh پس از Authentication، claims کاربر را در هر ریکوئست بازسازی می‌کند تا تغییر دسترسی بدون ورود مجدد اثر کند — هزینهٔ یک کوئری کش‌دار در برابر امنیت درست، بجاست."));

body.push(h1("۷. هاست و Program.cs", pal));
body.push(...code([
  "// ترتیب middleware (خلاصهٔ ضروری)",
  "ExceptionHandler / Hsts",
  "UseMiddleware<SecurityHeaders>          // CSP: default-src 'self' ...",
  "UseStaticFiles",
  "UseRateLimiter                          // لاگین ۶۰/دقیقه، کل ۶۰۰ burst",
  "UseMiddleware<Culture>                  // کوکی hr_lang",
  "SetupGate                               // بدون پیکربندی: همه‌چیز به /Setup",
  "UseRouting + UseAuthentication",
  "UseMiddleware<RbacRefresh>",
  "UseMiddleware<LicenseGate>",
  "UseMiddleware<ModuleGate>",
  "UseAuthorization",
  "MapGet(\"/health\") + MapControllerRoute",
  "HrModuleRuntime.ConfigureApp(app)       // Map پل دستگاه‌ها",
  "DbInit.InitializeAsync                  // خودترمیم + سید (خطا = RepairMode)",
], { caption: "اسکلت pipeline هسته" }));
body.push(bullet("DataProtection با مسیر ثابت کلیدها (پوشهٔ keys) و ApplicationName ثابت — وگرنه کوکی‌ها بعد از استقرار مجدد بی‌اعتبار می‌شوند."));
body.push(bullet("endpoint /health بدون گیت: اجرای SELECT 1 و بازگشت 200/503 بدون افشای خطا."));
body.push(bullet("Serilog از ابتدا: کنسول + فایل روزانه با نگهداری ۳۱ روز؛ سطح Microsoft و EF روی Warning."));

body.push(h1("۸. ویزارد راه‌اندازی و تنظیمات رمزشده", pal));
body.push(p("تا وقتی پیکربندی نشده، همهٔ مسیرها به /Setup می‌روند (به‌جز health و سوییچ زبان؛ api پاسخ 503). ویزارد دو گام دارد: انتخاب زبان و بانک (SQL Server با فیلدهای سرور/پورت/بانک/احراز/SSL و دکمهٔ تست اتصال، یا SQLite با مسیر فایل) و گام مجوز نصب. نتیجه در فایل hrsettings.bin با DataProtection رمز و با نوشتن اتمی ذخیره می‌شود؛ حذف این فایل ویزارد دوباره فعال می‌شود. پس از ذخیره: ساخت اسکیما، سید اولیه، و ریدایرکت به صفحهٔ ورود."));

body.push(h1("۹. مجوز نصب — امضای RSA", pal));
body.push(p("فایل مجوز = base64(payload JSON).base64(امضای RSA-SHA256). payload شامل licensee، machine (اثر انگشت سخت‌افزار از MachineGuid یا machine-id با SHA-256 و الگوی ۵گروهی)، تاریخ انقضا اختیاری، سقف پرسنل، فهرست ماژول‌های مجاز و lockMode است. در هاست یک LicenseService با کلید عمومی embedded بنویسید؛ جفت‌کلید هر نصب را هم در App_Data/license-keys نگه دارید (خصوصی رمزشده با DataProtection) تا بدون ابزار فروشنده هم بتوان مجوز صادر کرد. LicenseGate: مسیرهای آزاد (License/Auth/health/Setup/api/پل‌ها)، بقیه در حالت lock به /License/Notice و در حالت warn فقط بنر هشدار."));

body.push(h1("۱۰. ماژول سازمان — اولین ماژول واقعی", pal));
body.push(bullet("Org.Data: موجودیت‌های Company/Department/Position/Employee/Contract/Decree با FamilyTypes و SeedOrder=10؛ سید یک شرکت نمونه و چارت پایه."));
body.push(bullet("Org UI: کنترلرهای Org (درخت واحد با افزودن/ویرایش/انتقال/حذف)، Employees (فهرست با فیلتر، فرم کامل پرسنل، ورود گروهی اکسل) و Holidays (تقویم شمسی ماهانه)؛ NavGroups با گروه «منابع انسانی» Sort=20 و کلیدهای employees/org/holidays."));
body.push(bullet("تعامل از قرارداد: هاست برای پرکردن کاربران به IPeopleDirectory نیاز دارد؛ پیاده‌سازی واقعی در Org و پیاده‌سازی NullPeopleDirectory در هاست — هسته حتی بدون Org بالا می‌آید."));

body.push(h1("۱۱. ماژول تجهیزات و دروازهٔ دستگاه‌ها", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۴ — اجزای اتصال دستگاه",
  headers: ["جزء", "شرح", "مقدار پیش‌فرض"],
  widths: [26, 48, 26],
  rows: [
    ["پل ADMS (PUSH)", "دستگاه به سرور وصل می‌شود: GET/POST /iclock/cdata?SN=", "پورت اختصاصی 8081 + پورت وب"],
    ["حالت TCP", "سرور به دستگاه وصل می‌شود (پروتکل ZK)", "پورت دستگاه 4370، فاصلهٔ poll ۶۰ ثانیه"],
    ["پل ANPR", "دوربین پلاک‌خوان: POST /anpr/plate?SN= و GET /anpr/ping", "ضربان ۳۰ ثانیه"],
    ["سرویس مستقل", "device-service-csharp به‌صورت Windows Service/ systemd", "پیکربندی زنده از /api/gateway/config"],
    ["احراز سرویس", "کاربر فنی svc_bridge با JWT", "کلید دستگاه در X-Device-Token"],
  ],
}));
body.push(bullet("جریان داده همیشه از اینترفیس IDeviceGatewayChannel می‌گذرد: تنظیمات و دستگاه‌ها خوانده، دستورها pull و mark می‌شوند و ترددها از DeviceOps.IngestPunchesAsync وارد بانک می‌شوند — هیچ insert مستقیم از پل به جدول‌های کسب‌وکار."));
body.push(bullet("صف دستور دستگاه: DeviceCommand با وضعیت pending/delivered/acked/failed و سقف ۲۰ دستور در هر pull."));

body.push(h1("۱۲. زیرساخت تحویل: به‌روزرسانی، بسته‌سازی، پشتیبان", pal));
body.push(bullet("UpdateApplier از روز اول: بستهٔ zip با manifest.json (format=hr-update-pack)؛ استیجینگ، پشتیبان zip، پرچم apply.json و اعمال در بوت بعدی؛ پوشه‌های App_Data و logs هرگز بازنویسی نشوند."));
body.push(bullet("بسته‌سازی مشتری: اسکریپت publish با حذف فیزیکی DLL ماژول‌های حذف‌شده — ماژول‌های پایه همیشه می‌مانند."));
body.push(bullet("پشتیبان: SQLite با VACUUM INTO و فشرده‌سازی، SQL Server با BACKUP DATABASE؛ نگهداری ۱۴ نسخهٔ آخر و بازیابی با تست integrity."));

body.push(h1("۱۳. فهرست تحویل و تست پذیرش هسته", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۵ — معیارهای پذیرش هستهٔ مینیمال",
  headers: ["ردیف", "سناریوی پذیرش"],
  widths: [12, 88],
  rows: [
    ["۱", "استارت تمیز: ویزارد دوبار گام را کامل می‌کند و به ورود می‌رسد؛ hrsettings.bin رمزشده ساخته می‌شود"],
    ["۲", "ورود admin و تغییر رمز؛ قفل پس از ۵ خطا؛ بازگشایی از پنل کاربران"],
    ["۳", "ساخت شرکت، چارت، پست، پرسنل و ورود گروهی اکسل با اعتبارسنجی کد ملی"],
    ["۴", "تعریف دستگاه و مشاهدهٔ وضعیت آنلاین؛ ثبت تردد آزمایشی و ورود آن در فهرست"],
    ["۵", "ایجاد نقش با دامنهٔ واحد سازمانی و بررسی رفتار 403 در خارج از دامنه"],
    ["۶", "حذف DLL یک ماژول الحاقی: استارت سالم، منوی ماژول مخفی، مسیرش پیام دوستانه"],
    ["۷", "آپلود بستهٔ به‌روزرسانی نمونه: پشتیبان ساخته و پس از ری‌استارت نسخهٔ جدید فعال"],
    ["۸", "ورود بدون مجوز: قفل صفحات با پیام /License/Notice و آزادی مسیرهای پل"],
    ["۹", "smoke سراسری سبز: ورود، داشبورد، خروجی اکسل/PDF، سلامت /health"],
  ],
}));
body.push(note("نقطهٔ شروع پیشنهادی ترتیب پیاده‌سازی: Contracts و سپس Core (امنیت + RBAC + DbInit) و سپس هاست و ویزارد و سپس UI پایه (Layout فارسی RTL با Vazirmatn) و سپس مجوز و به‌روزرسانی و در پایان ماژول‌های Org و Devices+Gateway. هر گام باید قابل اجرا و تست باشد؛ سامانه در هیچ مرحله‌ای «نیمه‌شکسته» نماند.", pal, { bold: true }));

module.exports = { file: "/home/z/my-project/download/docs/HR-Core-Rebuild-Guide-FA.docx",
  palette: pal,
  cover: { title: "راهنمای ساخت پروژهٔ هسته‌محور از صفر", subtitle: "نقشهٔ کامل ساخت نسخهٔ مینیمال سامانه — فقط هسته، با معماری آمادهٔ توسعهٔ ماژولی", englishLabel: "CORE-ONLY REBUILD GUIDE", metaLines: ["دامنه: هسته + سازمان + تجهیزات (بدون ماژول‌های الحاقی)", "پلتفرم: ASP.NET Core 8 + EF Core — SQLite/SQL Server", "همسو با سند معماری و سند تحویل هوش مصنوعی"], footerRight: "مستند مهندسی — نسخهٔ هسته", footerLeft: "نسخهٔ 1.0" },
  headerTitle: "راهنمای ساخت هستهٔ مینیمال از صفر — سامانه منابع انسانی",
  bodyChildren: body };
