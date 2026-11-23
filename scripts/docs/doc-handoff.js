// doc-handoff.js — سند ۱: تحویل به هوش مصنوعی توسعه‌دهنده
const H = require("./helpers");
const { PALETTES, p, h1, h2, h3, bullet, code, note, tbl } = H;
const pal = PALETTES["DM-1"];

const body = [];

body.push(h1("۱. هدف سند و قواعد طلایی", pal));
body.push(p("این سند مرز میان تیم فعلی و هر عامل توسعه‌دهندهٔ آینده — انسانی یا هوش مصنوعی — است. خواننده فرض می‌شود با مفاهیم پایهٔ ASP.NET Core آشناست اما دربارهٔ این مخزن هیچ دانش قبلی ندارد. هدف: توانایی افزودن فیچر جدید با همان معماری، قراردادها و کیفیت موجود، بدون شکستن هیچ قابلیت فعلی. هر نامی که در این سند آمده، عیناً در سورس قابل جستجو است؛ اگر توضیحی کوتاه بود، پاسخ کامل در همان فایل سورس است."));
body.push(h2("قواعد طلایی — پیش از هر تغییر", pal));
body.push(bullet("هیچ فیچری حذف نمی‌شود؛ حذف یا «ساده‌سازی» رفتار موجود ممنوع است. فقط افزودن و ارتقا."));
body.push(bullet("فقط نسخه‌های جدید فناوری؛ هیچ وابستگی منسوخ یا پروژهٔ legacy بازنمی‌گردد."));
body.push(bullet([H.t("هرگز پروژه‌های قدیمی "), H.t("Hr.* / Hr.Web", { rtl: false }), H.t(" بازنسازی یا رفرنس نشوند؛ مهاجرت به "), H.t("Platform.sln", { rtl: false }), H.t(" کامل است.")]));
body.push(bullet("کلید ترجمه = خودِ متن فارسی است؛ تغییر متن‌های فارسی موجود، ترجمه‌ها را می‌شکند."));
body.push(bullet("هر ماژول باید بدون DLL ماژول‌های دیگر هم بالا بیاید (قرارداد fallback صفر — بخش ۴)."));
body.push(bullet("پیام کاربر همیشه از درون HrT.T صدا زده شود و فونت Vazirmatn و جهت RTL دست‌نخورده بماند."));
body.push(bullet("فایل‌های PowerShell داخل installer/ باید UTF-8 با BOM ذخیره شوند وگرنه PowerShell 5.1 ویندوز فارسی خطای ParserError می‌دهد."));
body.push(note("معیار پذیرش هر تغییر: بیلد صفر خطا، سبز شدن اسکریپت‌های e2e، سالم ماندن حالت بدون ماژول، و سالم بودن RTL/LTR. چک‌لیست کامل در بخش ۱۲.", pal, { bold: true }));

body.push(h1("۲. نمای کلی و نقشهٔ مخزن", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۱ — مشخصات فنی سامانه (نسخهٔ 2.7.11)",
  headers: ["مؤلفه", "مقدار"],
  widths: [32, 68],
  rows: [
    ["Runtime", "ASP.NET Core 8 (LTS) — MVC + Razor"],
    ["ORM", "EF Core 8 با EnsureCreated + خودترمیم اسکیما (بدون EF Migrations)"],
    ["بانک اطلاعاتی", "SQLite (پیش‌فرض، فایل hr.db) یا SQL Server (سازمانی)"],
    ["احراز هویت", "JWT + کوکی hr_at (۶۰ دقیقه) و hr_rt چرخشی (۷ روز) + PBKDF2-SHA256"],
    ["لاگ", "Serilog — کنسول + logs/log-YYYYMMDD.txt با نگهداری ۳۱ روز"],
    ["تقویم و ارز", "Jalali.cs و Money.cs — نمایش شمسی/قمری/میلادی سوییچ‌پذیر"],
    ["رابط کاربری", "فارسی RTL، فونت Vazirmatn، Chart.js لوکال"],
    ["پورت پیش‌فرض", "3000 (متغیر ASPNETCORE_URLS)"],
  ],
}));
body.push(...tbl({
  palette: pal, caption: "جدول ۲ — پروژه‌های Platform.sln و نقش هر یک",
  headers: ["پروژه", "نقش"],
  widths: [38, 62],
  rows: [
    ["Platform.Contracts", "قراردادهای مشترک ماژول‌ها: ModuleSystem.cs، GatewayContracts.cs، PeopleContracts.cs، HrContracts.cs، AttCalc.cs، DbResolver.cs"],
    ["Platform.Core", "سرویس‌های مشترک: AuthCore، RbacService، Security، Jalali، Money، PdfReport، SettingsService، HrLang/HrLangStore، DeviceOps، BioSyncCore"],
    ["Platform.Web", "هاست: Program.cs، CoreModule، Infrastructure (DbConfig/DbInit/LicenseGate/UpdateApplier/ModuleGate/…) و Views هسته"],
    ["HrModules.Org", "چارت سازمانی، پرسنل، تعطیلات (موجودیت‌ها از طریق ComposeModel)"],
    ["HrModules.Devices", "تعریف دستگاه، بیومتریک، نمای زنده"],
    ["HrModules.DeviceGateway", "worker داخلی + پل ADMS/ANPR درون اپ"],
    ["HrModules.Attendance (+ .Data)", "تردد روزانه، شیفت‌ها، درخواست‌های مرخصی/مأموریت/مجوز/اصلاح"],
    ["HrModules.Access (+ .Data)", "مناطق، گیت‌ها، کارت‌ها، رویدادها، شمارش اضطراری"],
    ["HrModules.Payroll (+ .Data)", "لیست حقوق، فرمول‌ساز اقلام، پارامترها، عیدی و تسویه"],
    ["Platform.Modules.Parking (+ .Data)", "پارکینگ، دوربین پلاک‌خوان، مجوز خودرو"],
    ["Platform.Modules.Visitors (+ .Data)", "ارباب‌رجوع و پیمانکاران"],
    ["tools/Platform.Migrate", "ابزار انتقال داده SQLite به SQL Server (HrMigrate)"],
    ["tools/LicenseGen + LicenseStudio", "صدور مجوز نصب (CLI و ویندوزی)"],
  ],
}));
body.push(p("بیرون از Solution: پوشهٔ device-service-csharp (سرویس ویندوزی/سیستم‌دی پل دستگاه‌ها، نسخهٔ توصیه‌شده)، device-service (همان پل با Node.js برای سازگاری قدیمی)، database/migrations (اسکریپت‌های دستی SQL Server)، installer (نصب‌کنندهٔ ویندوز)، scripts (عملیات و تست) و worklog.md (تاریخچهٔ فازها که باید در پایان هر فاز یک خط به آن اضافه شود)."));

body.push(h1("۳. معماری ماژولار — مهم‌ترین قرارداد", pal));
body.push(h2("۳.۱ دو رابط اصلی ماژول", pal));
body.push(p([H.t("هر قابلیت، دو پروژهٔ جدا دارد: پروژهٔ داده با پیاده‌سازی "), H.t("IHrDataModule", { rtl: false }), H.t(" و پروژهٔ UI با پیاده‌سازی "), H.t("IHrUiModule", { rtl: false }), H.t(". هر دو اینترفیس در Platform.Contracts تعریف شده‌اند و هاست فقط با این قرارداد کار می‌کند.")]));
body.push(...tbl({
  palette: pal, caption: "جدول ۳ — اعضای کلیدی IHrDataModule و IHrUiModule",
  headers: ["اینترفیس", "عضو", "شرح"],
  widths: [18, 34, 48],
  rows: [
    ["IHrDataModule", "Code / Title / Icon / Description", "هویت ماژول — Code باید یکتا و حروف کوچک باشد"],
    ["IHrDataModule", "SeedOrder", "ترتیب سید؛ سازمان 10 و تجهیزات 20؛ بقیه ۱۰۰ به بالا"],
    ["IHrDataModule", "FamilyTypes[]", "مالکیت اسکیما؛ تعیین می‌کند چه موجودیت‌هایی به این ماژول تعلق دارند"],
    ["IHrDataModule", "ConfigureDataServices", "ثبت DbContext و سرویس‌های DI ماژول"],
    ["IHrDataModule", "ComposeModel + ComposeCrossModule", "ساخت مدل EF در دو پاس (مدل خودی، سپس FKهای بین‌ماژولی)"],
    ["IHrDataModule", "SeedAsync / PreSeedAsync", "سید idempotent با HrSeedContext (دادهٔ دمو یا پایه)"],
    ["IHrDataModule", "OnCompanyDeleteAsync / CompanyCountsAsync", "حذف آبشاری داده‌های شرکت و آمار برای صفحهٔ شرکت‌ها"],
    ["IHrUiModule", "Controllers[]", "مانیفست نام کنترلرها برای گیت ماژولی (حروف کوچک)"],
    ["IHrUiModule", "OwnedActions[]", "کنترلرهای هاست که این ماژول مالک آن‌هاست"],
    ["IHrUiModule", "NavGroups[]", "ساختار منو: HrNavGroup(Name, Sort, HrNavItem(Href, Icon, Label, Perm, Ctrl))"],
    ["IHrUiModule", "IsCore", "فقط برای core مقدار true — همیشه فعال و غیرقابل حذف"],
  ],
}));
body.push(h2("۳.۲ کشف و ثبت خودکار", pal));
body.push(p([H.t("هاست فقط "), H.t("CoreModule", { rtl: false }), H.t(" را ثبت می‌کند؛ بقیهٔ ماژول‌ها با بازتاب از DLLهای "), H.t("HrModules.*.dll", { rtl: false }), H.t(" و "), H.t("Platform.Modules.*.dll", { rtl: false }), H.t(" (کنار اپ یا پوشهٔ modules/) کشف می‌شوند. کلاس "), H.t("HrModuleRuntime", { rtl: false }), H.t(" در GatewayContracts.cs نقطهٔ مرکزی است: ComposeModel دوپاسه با رد شدن خانواده‌های غایب (بدون جدول شبح)، تخصیص schema به هر جدول (SQL Server)، سید ماژول‌ها و ثبت سرویس‌ها. هیچ فهرست هاردکدی از ماژول‌ها وجود ندارد — افزودن ماژول یعنی اضافه کردن دو csproj به Solution و رفرنس از Platform.Web.")]));
body.push(h2("۳.۳ منو، گیت و وضعیت ماژول", pal));
body.push(bullet([H.t("منوی سایدبار از "), H.t("ModuleCatalog.BuildNav", { rtl: false }), H.t(" ساخته می‌شود: ادغام گروه‌های هم‌نام با Sort کمینه، حذف آیتم‌های بدون دسترسی یا ماژولِ خاموش.")]));
body.push(bullet([H.t("روشن/خاموش ماژول در فایل "), H.t("App_Data/modules.json", { rtl: false }), H.t(" ذخیره می‌شود (ModuleStateService) — عمداً بیرون بانک تا با مهاجرت از بین نرود. مانیفست سالم در "), H.t("App_Data/modules.manifest.json", { rtl: false }), H.t(" نگه‌داری می‌شود.")]));
body.push(bullet([H.t("ModuleGate", { rtl: false }), H.t(" (middleware): صفحهٔ ماژول خاموش به "), H.t("/Home/ModuleDisabled", { rtl: false }), H.t(" ریدایرکت می‌شود؛ AJAX بازگشت 403 JSON؛ مسیرهای api بازگشت 503 JSON.")]));
body.push(bullet([H.t("اگر DLL ماژولی نباشد، "), H.t("ModuleFallbacks", { rtl: false }), H.t(" پیاده‌سازی‌های Null را ثبت می‌کند تا داشبورد صفر ببیند و اپ کرش نکند — «آخرین ثبت برنده است»، پس Nullها قبل از پلاگین‌های واقعی ثبت می‌شوند.")]));
body.push(h2("۳.۴ قراردادهای بین‌ماژولی", pal));
body.push(p("ماژول‌ها همدیگر را مستقیم صدا نمی‌زنند؛ همه از اینترفیس‌های DI استفاده می‌کنند: IPeopleDirectory (پرسنل، واحد، ورود پرتال کارکنان)، IAttendanceGateway/Insights/ReportSource، IAccessGateway، IVisitorsGateway، IDevicesGateway، IDeviceGatewayChannel (هاست به گیت‌وی)، IParkingGatewaySink و IAuditSink. DTOهای اشتراکی کنار همین اینترفیس‌ها هستند."));
body.push(h2("۳.۵ قانون آهنین «نبودِ ماژول»", pal));
body.push(note("هر فیچر جدید که به سرویس ماژول دیگری نیاز دارد باید: از اینترفیس Contracts تزریق شود، نتیجهٔ خالی را بپذیرد، و هرگز مستقیم به DLL ماژول دیگر رفرنس ندهد. فقط پروژهٔ Data خودش و Contracts/Core مجازند.", pal, { bold: true }));

body.push(h1("۴. جریان درخواست در Program.cs", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۴ — ترتیب دقیق middleware (دست‌نخورده نگه داشته شود)",
  headers: ["مرحله", "مؤلفه", "کارکرد"],
  widths: [10, 30, 60],
  rows: [
    ["۱", "UpdateApplier.RunIfPending", "نصب بستهٔ به‌روزرسانی در صف، قبل از هر چیز"],
    ["۲", "SetupSecrets.Source", "بارگذاری تنظیمات رمزشدهٔ hrsettings.bin"],
    ["۳", "SecurityHeadersMiddleware", "هدرهای امنیتی + CSP — قبل از فایل‌های استاتیک"],
    ["۴", "UseStaticFiles + مسیر /dl", "استاتیک و مرکز دانلود (فقط اگر پوشهٔ dl هنگام استارت موجود باشد)"],
    ["۵", "UseRateLimiter", "لاگین ۶۰ درخواست بر دقیقه، کل ۶۰۰ burst؛ پاسخ 429"],
    ["۶", "CultureMiddleware", "کوکی hr_lang؛ زبان و جهت و تقویم — Culture عمداً عوض نمی‌شود"],
    ["۷", "Setup Gate", "بدون پیکربندی: همه‌چیز جز Setup/health/Ui به /Setup؛ api بازگشت 503"],
    ["۸", "Authentication + RbacRefreshMiddleware", "بازسازی claims و دسترسی‌ها در هر ریکوئست"],
    ["۹", "LicenseGate سپس ModuleGate", "قفل مجوز نصب، سپس گیت ماژول‌های خاموش"],
    ["۱۰", "Routing + Authorization + /health", "نقشهٔ MVC، MapControllers و endpoint سلامت"],
    ["۱۱", "HrModuleRuntime.ConfigureApp", "Map مسیرهای /iclock/cdata و /anpr"],
    ["۱۲", "DbInit.InitializeAsync", "ساخت/ترمیم اسکیما و سید — خطا به RepairMode می‌رود نه کرش"],
  ],
}));
body.push(...tbl({
  palette: pal, caption: "جدول ۵ — کوکی‌ها و ثابت‌های امنیتی",
  headers: ["نام", "مقدار", "شرح"],
  widths: [22, 30, 48],
  rows: [
    ["hr_at", "JWT، ۶۰ دقیقه", "کوکی دسترسی (HttpOnly، Secure)"],
    ["hr_rt", "توکن چرخشی، ۷ روز", "رفرش توکن با هش Sha256 در جدول RefreshTokens"],
    ["hr_af", "Cookie AntiForgery", "SameSite=None برای نصب iframe"],
    ["hr_lang", "کد زبان", "یک سال اعتبار — سوییچ از /Ui/Lang"],
    ["Issuer/Audience", "hr-attendance / hr-attendance-clients", "اعتبارسنجی JWT با ClockSkew صفر"],
  ],
}));

body.push(h1("۵. لایهٔ داده", pal));
body.push(h2("۵.۱ Contextها و قواعد ستون‌ها", pal));
body.push(p([H.t("پنج DbContext وجود دارد: "), H.t("AppDbContext", { rtl: false }), H.t(" ترکیبی در هاست (هسته + ComposeModel همهٔ ماژول‌های حاضر)، "), H.t("HrBaseDbContext", { rtl: false }), H.t(" فقط هسته، و context اختصاصی Attendance/Access/Visitors/Payroll/Parking. ماژول‌های Org/Devices context ندارند؛ موجودیت‌هایشان با FamilyTypes به مدل ترکیبی اضافه و از "), H.t("AppDbContext.Set<T>()", { rtl: false }), H.t(" استفاده می‌شود. قواعد: شناسه‌های رشته‌ای با سقف ۶۴ نویسه، اعشار 18/2، و ذخیرهٔ تاریخ در SQL Server به‌صورت epoch-seconds (مهاجرت 0017 و EpochSecondsConverter) — هرگز مستقیم DateTime خام ننویسید.")]));
body.push(h2("۵.۲ ساخت و ارتقای اسکیما — خودترمیم", pal));
body.push(p([H.t("روند "), H.t("DbInit.InitializeAsync", { rtl: false }), H.t(" در هر استارت: EnsureCreated، ساخت schemaهای ماژولی در SQL Server، CreateMissingTables (تفاوت مدل EF با بانک و ساخت فقط جدول‌های جدید)، PatchSchema، PatchMissingModelColumns، سید زبان‌ها و بسته‌های داخلی، EnsureRbac، سید دمو، کاربران پیش‌فرض، تنظیمات و پالت‌ها و در پایان همهٔ IModuleSeederها. یعنی تغییر مدل EF کافی است؛ در بوت بعدی خودش را می‌سازد. اسکریپت‌های database/migrations فقط تعمیر موضعی SQL Server هستند و دستی اجرا می‌شوند. ابزار Platform.Migrate نیز انتقال دادهٔ SQLite به SQL Server است — مهاجرت اسکیما نیست.")]));
body.push(...tbl({
  palette: pal, caption: "جدول ۶ — مهاجرت‌های دستی موجود در database/migrations",
  headers: ["فایل", "شرح"],
  widths: [34, 66],
  rows: [
    ["0004_rbac.sql", "جدول‌های نقش/دسترسی در schema داخلی SQL Server"],
    ["0009_permissions_catalog.sql", "کاتالوگ اولیهٔ کلیدهای دسترسی"],
    ["0010…0013", "قفل ورود، تاریخچه و بازنشانی رمز، RefreshToken"],
    ["0014…0016", "جدول حسابرسی، صف دستور دستگاه، آخرین دریافت و توکن webhook"],
    ["0017_unix_datetime_sqlserver.sql", "تبدیل همهٔ ستون‌های تاریخ به epoch-seconds bigint"],
  ],
}));
body.push(h2("۵.۳ اتصال بانک", pal));
body.push(p([H.t("DbConfig از کلیدهای "), H.t("Database:Provider", { rtl: false }), H.t(" و "), H.t("Database:ConnectionString", { rtl: false }), H.t(" درون hrsettings.bin می‌خواند (رمزشده با DataProtection؛ کلیدها در پوشهٔ aspnet/keys) و در نبود آن به ConnectionStrings برمی‌گردد. گزینه‌های DbContext per-scope ساخته می‌شوند تا تغییر اتصال از صفحهٔ تنظیمات بدون ری‌استارت اعمال شود. SQLite فایل hr.db کنار پروژهٔ وب است.")]));

body.push(h1("۶. امنیت، ورود و RBAC", pal));
body.push(bullet("هش رمز: PBKDF2-SHA256 با صد هزار تکرار؛ قالب pbkdf2-sha256$iter$saltB64$hashB64 و تطبیق با FixedTimeEquals."));
body.push(bullet("سیاست رمز: حداقل ۸ نویسه شامل حرف و عدد، عدم تکرار ۳ رمز آخر، انقضای ۹۰ روزه با PasswordExpiresAt."));
body.push(bullet("قفل ورود: ۵ خطا یعنی قفل ۱۵ دقیقه با پاسخ 423 و حسابرسی account_locked؛ محدودیت دوم ۱۰ تلاش در ۵ دقیقه به تفکیک IP و نام کاربری."));
body.push(bullet("کاربران سیدشده: admin با Admin@1234 (مدیر ارشد)، hr با Hr@123456، operator با Oper@1234، guard با Guard@1234، viewer با View@1234 و svc_bridge با Bridge@1234 — در استقرار واقعی بلافاصله رمز admin عوض شود."));
body.push(bullet([H.t("کلیدهای دسترسی قالب "), H.t("module.area.action", { rtl: false }), H.t(" دارند؛ تطبیق در RbacService.Matches به ترتیب دقیق، ستاره، wildcard و والد است. نقش‌ها ارث‌بری والد تا ۱۲ سطح دارند و [HasPerm] روی کنترلر/اکشن اعمال می‌شود.")]));
body.push(bullet("دامنهٔ داده: RoleScope با مقادیر all/company/dept/self؛ اجتماع نقش‌ها تقاطع با سقف شرکت کاربر؛ در کنترلر با ScopeAsync و فیلترهای ScopeQuery به SQL ترجمه می‌شود."));
body.push(bullet("حسابرسی: AuditLog شامل actor، action، entity، entityId، detail (JSON) و IP؛ عملیات نوشتاری و ورود/خروج و عملیات آپدیت/پشتیبان همیشه ثبت شوند."));

body.push(h1("۷. مجوز نصب", pal));
body.push(p("سامانه بدون مجوز معتبر قفل است و حالت تریال وجود ندارد. فرمت فایل مجوز: payload و امضا به‌صورت base64(JSON payload).base64(RSA-SHA256) با کلید RSA-2048. payload شامل product، licensee، machine (اثر انگشت سخت‌افزار ۲۵ نویسه‌ای)، تاریخ صدور و انقضا اختیاری، سقف پرسنل اختیاری، فهرست ماژول‌های مجاز اختیاری و lockMode است. فایل مجوز در App_Data/license.dat و جفت‌کلید هر نصب در App_Data/license-keys نگه‌داری می‌شود؛ کلید خصوصی با DataProtection رمز شده است. صدور با ابزار LicenseGen (CLI) یا LicenseStudio (ویندوز) از فایل درخواست .hrreq انجام می‌شود. مسیرهای همیشه آزاد: License، Auth، health، Setup، api، iclock و anpr؛ حالت lock صفحات را به /License/Notice می‌فرستد."));

body.push(h1("۸. به‌روزرسانی و بسته‌بندی", pal));
body.push(bullet([H.t("بستهٔ آپدیت: zip شامل "), H.t("manifest.json", { rtl: false }), H.t(" ریشه با format برابر hr-update-pack و نسخهٔ بزرگ‌تر از جاری، به‌همراه bin و wwwroot (بدون dl) و اختیاری web.config یا appsettings.")]));
body.push(bullet("اعمال از صفحهٔ Updates با دسترسی updates.manage: اعتبارسنجی و محافظت zip-slip، پوشهٔ staging در App_Data/updates، پشتیبان zip کامل در App_Data/backups، سپس پرچم apply.json و کپی فایل‌ها؛ در ویندوز اجرا در بوت بعدی یا Task Scheduler انجام می‌شود. پوشه‌های App_Data و logs هرگز بازنویسی نمی‌شوند."));
body.push(bullet([H.t("بستهٔ مشتری: "), H.t("build-customer.sh", { rtl: false }), H.t(" با آرگومان ماژول‌های حذف‌شده، نسخه و نام تولیدکننده — DLLهای ماژول‌های حذف‌شده فیزیکی از خروجی publish پاک می‌شوند و ماژول‌های پایه (core/org/devices) حذف‌نشدنی‌اند.")]));
body.push(bullet("نصب‌کنندهٔ ویندوز در پوشهٔ installer با Inno Setup و اسکریپت PowerShell است که IIS، فایروال و Task آپدیت‌کننده را تنظیم می‌کند. فایل‌های ps1 باید UTF-8 با BOM باشند."));

body.push(bullet([H.t("ساخت نصب‌کنندهٔ ویندوزی: همیشه از "), H.t("installer\\build-installer.bat", { rtl: false }), H.t(" استفاده کنید — اگر ps1 را مستقیم اجرا کنید و ویندوز خطای «File ... is not digitally signed / PSSecurityException / UnauthorizedAccess» داد، علت Execution Policy است؛ راه‌حل‌ها در installer/README-FA.md: bat با Bypass، یا powershell -ExecutionPolicy Bypass -File، یا Set-ExecutionPolicy -Scope Process Bypass، یا Unblock-File.")]));
body.push(h1("۹. اجرا، تست و عیب‌یابی محلی", pal));
body.push(...code([
  "# بیلد کامل",
  "cd aspnet && ~/.dotnet/dotnet build Platform.sln",
  "",
  "# اجرا با سوپروایزر خودترمیم (پورت 3000)",
  "bash scripts/start-web.sh",
  "python3 scripts/daemonize.py /bin/bash scripts/start-web.sh   # حالت دیتشد",
  "",
  "# سلامت و کوئری سریع بانک",
  "curl -s http://localhost:3000/health",
  "python3 scripts/dbq.py \"SELECT COUNT(*) FROM Employees\"",
  "",
  "# پشتیبان و بازیابی",
  "bash scripts/backup-db.sh --sqlite",
  "bash scripts/restore-db.sh backups/hr-sqlite-XXXX.db.gz --check",
  "",
  "# تست‌های E2E — ورود admin / Admin@1234",
  "bash scripts/smoke.sh",
  "bash scripts/e2e-devices-ajax.sh",
]));
body.push(p("دام‌های محیط: بیلد سنگین هم‌زمان با اپ در محیط کم‌حافظه می‌تواند اپ را با OOM بیندازد (سوپروایزر جبران می‌کند اما بیلدها را مدیریت کنید)؛ پروسه‌های پس‌زمینه بین فراخوانی‌ها کشته می‌شوند پس از daemonize.py با دابل‌فورک استفاده کنید؛ پرچم‌های emoji در فایرفاکس رندر نمی‌شوند پس آیکون‌ها SVG باشند (HrFlags.Svg)."));

body.push(h1("۱۰. i18n، تقویم، عدد و ارز", pal));
body.push(bullet([H.t("زبان مبدأ فارسی است؛ "), H.t("HrT.T", { rtl: false }), H.t(" زنجیرهٔ بستهٔ داینامیک بانک، دیکشنری ثابت انگلیسی و در نهایت خودِ متن فارسی را طی می‌کند — پس متن فارسی، کلید است و تغییر آن ممنوع.")]));
body.push(bullet("زبان فعال: کوکی hr_lang، سپس تنظیم پیش‌فرض، سپس Ui:Language و در نهایت fa. جهت، ارقام و تاریخ خودکار تعیین می‌شوند؛ رشته‌های سمت JS از window.__hrL10n می‌آیند."));
body.push(bullet([H.t("بستهٔ زبان قالب JSON با format برابر hr-lang-pack و فیلد entries (متن فارسی به ترجمه) دارد؛ ایمپورت از صفحهٔ Langs فقط merge می‌کند و fa رد می‌شود. فایل‌های embedded در "), H.t("App_Data/langpacks/", { rtl: false }), H.t(" در هر بوت merge می‌شوند.")]));
body.push(bullet("مبالغ همیشه با Money.Fmt و تاریخ‌ها همیشه با Jalali — ذخیرهٔ بانک میلادی/epoch است و فقط نمایش تغییر می‌کند."));
body.push(bullet([H.t("فاز ۹۶ — تقویم قمری: مبنا "), H.t("UmAlQuraCalendar", { rtl: false }), H.t(" است؛ چون ام‌القری معمولاً یک روز جلوتر از تقویم رسمی ایران/افغانستان است، تنظیم "), H.t("AppSettings.HijriAdjust (پیش‌فرض -1)", { rtl: false }), H.t(" در CultureMiddleware روی "), H.t("Jalali.HijriAdjustDays", { rtl: false }), H.t(" تزریق می‌شود و در Fmt/FmtFa (نمایش)، "), H.t("Jalali.FmtHijri/ToHijri (قالب برون‌بری)", { rtl: false }), H.t(" و سمت مرورگر با اتریبیوت "), H.t("data-hijri روی <html>", { rtl: false }), H.t(" برای پیکر تاریخ (Intl islamic-umalqura) اعمال می‌شود — فقط نمایش؛ داده‌ها دست‌نخورده.")]));
body.push(bullet([H.t("فاز ۹۶ — محافظ صفحه: js/hr-screensaver.js با پیکربندی از تنظیمات (AppSettings.ScreenSaverEnabled/Minutes/RequirePassword) در _Layout؛ بیدارسازی رمزدار با POST "), H.t("/Auth/VerifyWakePassword", { rtl: false }), H.t(" (PBKDF2 + محدودسازی ۵ خطا/۵ دقیقه + حسابرسی). ملاک زمان، اختلاف واقعی Date.now() از آخرین رخداد ورودیِ isTrusted است — نه شمارش تیک interval.")]));
body.push(bullet([H.t("فاز ۹۶ — برون‌بری قالب‌محور تردد: موجودیت "), H.t("AttendanceExportTemplate", { rtl: false }), H.t(" (ستون‌ها JSON + جداکننده + سرستون) در ماژول attendance با endpointهای "), H.t("/Attendance/Exports، /Attendance/SaveTemplate، /Attendance/DeleteTemplate، /Attendance/ExportByTemplate", { rtl: false }), H.t(" — قالب تاریخ: iso/linux/jalali/gregorian/hijri و قالب زمان: hhmm/hhmmss/linux/fullها؛ خروجی CSV با UTF-8 BOM.")]));
body.push(bullet("فاز ۹۶ — ویزارد راه‌اندازی سه‌تبی شد (زبان / دیتابیس / مجوز) — فقط جابجایی پنل‌ها؛ هیچ اکشن سروری تغییر نکرده است."));
body.push(bullet([H.t("فاز ۹۸ — ثانیه در محافظ صفحه: "), H.t("AppSettings.ScreenSaverSeconds (0..59)", { rtl: false }), H.t(" در کنار دقایق؛ فرم تنظیمات فیلد ssSeconds دارد، _Layout اتریبیوت data-ss-seconds می‌دهد و آستانهٔ JS = دقیقه×۶۰+ثانیه؛ ستون با خودترمیم اسکیما روی بانک‌های قدیمی ساخته می‌شود.")]));
body.push(bullet([H.t("فاز ۹۸ — برون‌ریزی اکسل در خود صفحات: دکمهٔ «برون‌ریزی اکسل» با گارد "), H.t("reports.export", { rtl: false }), H.t(" روی صفحهٔ پرسنل (/Export/Employees) و تردد روزانه (/Export/Attendance?date=…). باگ OrderBy روی نوع ساخته‌شده در ExportDayAsync رفع شد (مرتب‌سازی روی anonymous projection). درون‌ریزی گروهی پرسنل (/Employees/Import) و درون‌ریزی/برون‌ریزی بستهٔ ترجمه (/Langs) فعال‌اند.")]));

body.push(h1("۱۱. قراردادهای رابط کاربری", pal));
body.push(bullet("Layout مشترک با سایدبار ماژول‌محور، جستجوی سراسری، breadcrumb، ساعت و تاریخ زنده، سوییچ زبان با پرچم SVG، زنگ اعلان و منوی کاربر. جابجایی صفحات با data-refresh انجام می‌شود."));
body.push(bullet("فرم‌ها: مودال AJAX با data-modal، ارسال با data-ajax، توست ToastOk/ToastErr و تأیید data-confirm؛ هر فرم دارای anti-forgery است."));
body.push(bullet("دراپ‌داون‌ها به‌جای select بومی، از کامپوننت سفارشی site.js استفاده می‌کنند تا فونت Vazirmatn داشته باشند؛ پنل باز باید دقیقاً زیر تریگر بنشیند و در مودال‌ها با overflow هم درست بایستد. ویجت جدید حتماً از همین کامپوننت استفاده کند."));
body.push(bullet("دو خطای UI شناخته‌شده و در حال تعمیر: جابه‌جایی پنل دراپ‌داون در صفحهٔ تعریف دستگاه جدید و فاصله‌گذاری ناکافی بعضی فرم‌ها — در کارهای جدید تکرار نشوند."));
body.push(bullet("آیکون‌ها emoji درون متن است؛ پرچم‌ها فقط SVG. جدول‌ها zebra و اعداد با فونت خود سامانه؛ ورودی تاریخ میلادی است و نمایش شمسی در سلول‌ها."));

body.push(h1("۱۲. دستورالعمل‌های پیاده‌سازی", pal));
body.push(h2("دستورالعمل الف — افزودن ماژول کامل", pal));
body.push(bullet([H.t("پروژهٔ دادهٔ "), H.t("HrModules.X.Data", { rtl: false }), H.t(" بسازید (classlib با net8.0 و رفرنس به Contracts و Core): موجودیت‌ها با قراردادها، سپس "), H.t("XDataModule : IHrDataModule", { rtl: false }), H.t(" شامل Code، FamilyTypes، SeedOrder، ComposeModel و ComposeCrossModule و SeedAsync و OnCompanyDeleteAsync.")]));
body.push(bullet([H.t("در صورت نیاز به سید خودترمیم، "), H.t("XSeeder : IModuleSeeder", { rtl: false }), H.t(" با ModuleCode برابر code ماژول بنویسید — در هر بوت idempotent اجرا می‌شود.")]));
body.push(bullet([H.t("پروژهٔ UI "), H.t("HrModules.X", { rtl: false }), H.t(" با کنترلر مشتق از HrModuleControllerBase و ویژگی [HasPerm]، ویوهای Razor با Layout مشترک، و "), H.t("XUiModule : IHrUiModule", { rtl: false }), H.t(" شامل Controllers و NavGroups.")]));
body.push(bullet("افزودن هر دو csproj به Platform.sln و رفرنس از Platform.Web به UI و Data؛ سپس کلیدهای دسترسی جدید به کاتالوگ Permission و متن‌های جدید به دیکشنری انگلیسی."));
body.push(bullet("بیلد، اجرا، نوشتن اسکریپت e2e تازه، و ثبت یک خط در worklog.md با پیشوند فاز."));
body.push(h2("دستورالعمل ب — افزودن صفحه به ماژول موجود", pal));
body.push(p("اکشن جدید با ویژگی HasPerm و کلید مناسب، ویو در همان پوشه، و دکمه یا آیتم منو با همان کلید. اگر به دادهٔ ماژول دیگر نیاز است، از اینترفیس Contracts تزریق کنید و خالی بودن نتیجه را بپذیرید."));
body.push(h2("دستورالعمل ج — تغییر اسکیما", pal));
body.push(p("مدل EF را تغییر دهید و اپ را اجرا کنید؛ DbInit ستون و جدول جدید را خودکار می‌سازد. مهاجرت داده را در IModuleSeeder یا patch خودترمیمِ idempotent بنویسید. برای SQL Server در صورت لزوم، اسکریپت دستی با هدر هشدار FULL BACKUP به database/migrations اضافه شود. تاریخ‌ها epoch-seconds هستند."));
body.push(h2("دستورالعمل د — کلید دسترسی جدید", pal));
body.push(p("قالب module.area.action با والد manage برای عملیات نوشتاری؛ ثبت در کاتالوگ، و در صورت نیاز تعریف والد جدید در PermParents. صفحهٔ نقش‌ها خودکار کلید تازه را نشان می‌دهد."));
body.push(h2("دستورالعمل ه — endpoint جدید برای دستگاه", pal));
body.push(p("پل دستگاه‌ها یا در ماژول DeviceGateway (درون اپ) یا در سرویس مستقل device-service-csharp پیاده می‌شود؛ احراز یا با JWT کاربر svc_bridge است یا با توکن webhook دستگاه در هدر X-Device-Token. دادهٔ ورودی همیشه از مسیر DeviceOps.IngestPunchesAsync یا BioSync یا IngestPlateAsync به بانک برود."));

body.push(h1("۱۳. تعریف «تمام‌شده» و مسیرهای مرجع", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۷ — چک‌لیست پذیرش هر فیچر",
  headers: ["ردیف", "شرط پذیرش"],
  widths: [12, 88],
  rows: [
    ["۱", "بیلد Platform.sln بدون خطا و اخطار جدید"],
    ["۲", "اپ بدون DLL ماژول‌های دیگر بالا می‌آید و داشبورد صفر و بدون کرش است"],
    ["۳", "همهٔ متن‌ها از HrT.T و سالم در RTL و LTR"],
    ["۴", "کلید دسترسی در کاتالوگ، تخصیص نقش و تست رفتار 401/403"],
    ["۵", "حسابرسی عملیات نوشتاری و رعایت دامنهٔ داده"],
    ["۶", "smoke.sh و e2e تازه سبز"],
    ["۷", "RTL سالم در عرض‌های 1280 و 1024 با فونت Vazirmatn"],
    ["۸", "یک خط در worklog.md با پیشوند فاز"],
  ],
}));
body.push(...tbl({
  palette: pal, caption: "جدول ۸ — مسیرهای کلیدی سامانه",
  headers: ["مسیر", "کارکرد"],
  widths: [42, 58],
  rows: [
    ["/Auth/Login", "ورود یکپارچه: مدیر با نام کاربری و رمز؛ پرسنل با کد پرسنلی و کد ملی"],
    ["/", "داشبورد شاخص‌ها و چارت‌ها و دستگاه‌ها"],
    ["/Employees ، /Org ، /Shifts ، /Holidays", "منابع انسانی"],
    ["/Attendance ، /Requests ، /Self", "تردد، درخواست‌ها و پرتال من"],
    ["/Devices ، /Biometrics ، /Gateway ، /DeviceGateway", "دستگاه‌ها و گیت‌وی"],
    ["/Access ، /Cards ، /Access/Muster", "کنترل تردد و حراست"],
    ["/Parking ، /Visitors", "پارکینگ و ارباب‌رجوع"],
    ["/Payroll", "حقوق و دستمزد و اقلام و پارامترها"],
    ["/Reports ، /Reports/Custom ، /Reports/Parking", "گزارش‌ساز تحلیلی"],
    ["/Users ، /Companies ، /Settings ، /Langs ، /License ، /Updates ، /Audit", "مدیریت سیستم"],
    ["/iclock/cdata و /anpr/plate و /api/devices/webhook", "پل دستگاه‌ها و دوربین‌ها"],
    ["/api/auth/login و /api/gateway/config", "API سرویس پل دستگاه‌ها"],
    ["/Setup و /health", "ویزارد راه‌اندازی و سلامت"],
  ],
}));

module.exports = { file: "/home/z/my-project/download/docs/HR-AI-Handoff-FA.docx",
  palette: pal,
  cover: { title: "سند تحویل به هوش مصنوعی توسعه‌دهنده", subtitle: "سامانه یکپارچه منابع انسانی — نقشهٔ معماری، قراردادها و دستورالعمل افزودن فیچر", englishLabel: "AI DEVELOPER HANDOFF", metaLines: ["سامانه: Platform.sln — نسخهٔ 2.7.11", "پلتفرم: ASP.NET Core 8 + EF Core — بانک SQLite/SQL Server", "همراه سورس بخوانید؛ نام‌ها عیناً در سورس قابل جستجو هستند"], footerRight: "سند تحویل فنی — محرمانه", footerLeft: "نسخهٔ 1.0" },
  headerTitle: "سند تحویل به هوش مصنوعی توسعه‌دهنده — سامانه منابع انسانی v2.7.11",
  bodyChildren: body };
