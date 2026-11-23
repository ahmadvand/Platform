// doc-arch.js — سند ۵: مستندات معماری نرم‌افزار
const H = require("./helpers");
const { PALETTES, p, h1, h2, h3, bullet, code, note, tbl } = H;
const pal = PALETTES["CM-2"];

const body = [];

body.push(h1("۱. نمای کلی و اصول طراحی", pal));
body.push(p("سامانه یک پلتفرم ماژولار منابع انسانی بر بستر ASP.NET Core 8 است که با سه اصل طراحی شده: نخست، هاست از ماژول‌ها بی‌خبر است و همه‌چیز با قرارداد (Interface) و کشف بازتابی DLL کار می‌کند؛ دوم، نبود هر ماژول باید بدون کرش و با رفتار دوستانه هضم شود؛ سوم، هر داده و هر صفحه زیر سلطهٔ دسترسی‌سنجی و دامنهٔ رکوردی است. نتیجه: یک باینری واحد می‌تواند به‌عنوان نصب کامل پنج‌ماژولی یا نصب حداقلی سه‌ماژولی تحویل شود، بدون هیچ تغییر کدی."));
body.push(...tbl({
  palette: pal, caption: "جدول ۱ — اصول معماری و پیامد عملی آن‌ها",
  headers: ["اصل", "پیامد در کد"],
  widths: [34, 66],
  rows: [
    ["ماژول‌محوری با قرارداد", "IHrDataModule/IHrUiModule در Contracts؛ هاست فقط CoreModule را ثبت می‌کند"],
    ["کشف بازتابی", "DLLهای HrModules.*/Platform.Modules.* کنار اپ یا پوشهٔ modules/ کشف می‌شوند"],
    ["تحمل نبودِ ماژول", "Fallbackهای Null + رد شدن خانواده‌های غایب در مدل EF + گیت دوستانه"],
    ["امنیت پیش‌فرض", "CSP سخت‌گیرانه، RateLimiter، قفل ورود، حسابرسی سراسری، مجوز نصب اجباری"],
    ["خودترمیمی", "اسکیمای EF در هر بوت مقایسه و کامل می‌شود؛ سیدها idempotent و در هر بوت اجرا"],
    ["فارسی اول", "RTL، فونت Vazirmatn، تقویم جلالی، ارقام فارسی — همه با زنجیرهٔ i18n قابل تبدیل"],
  ],
}));

body.push(h1("۲. لایه‌ها و پروژه‌ها", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۲ — لایه‌بندی Solution و جهت وابستگی‌ها",
  headers: ["لایه", "پروژه‌ها", "وابسته به"],
  widths: [22, 44, 34],
  rows: [
    ["قرارداد", "Platform.Contracts", "هیچ — فقط BCL"],
    ["سرویس‌های مشترک", "Platform.Core", "Contracts"],
    ["هاست", "Platform.Web", "Core + Contracts + همهٔ UIهای ماژول"],
    ["دادهٔ ماژول‌ها", "HrModules.*.Data و Platform.Modules.*.Data", "Contracts + Core"],
    ["UI ماژول‌ها", "HrModules.* و Platform.Modules.*", "Data خود + Core + Contracts"],
    ["ابزار", "Platform.Migrate، LicenseGen، LicenseStudio", "Contracts/Core (خارج از Solution)"],
  ],
}));
body.push(p("جهت وابستگی همیشه به سمت Contracts است و هرگز برعکس نمی‌شود؛ به همین دلیل حذف یک لایهٔ UI یا Data، فقط و فقط همان قابلیت را از بین می‌برد و هاست سالم می‌ماند."));

body.push(h1("۳. قرارداد ماژول‌ها (به تفصیل)", pal));
body.push(h2("۳.۱ چرخهٔ حیات داده", pal));
body.push(p([H.t("پیاده‌سازی "), H.t("IHrDataModule", { rtl: false }), H.t(" مالک چرخهٔ کامل دادهٔ ماژول است: ثبت سرویس‌ها و DbContext در ConfigureDataServices، ساخت مدل EF در دو پاس ComposeModel و ComposeCrossModule (پاس دوم برای FKهای بین‌ماژولی بعد از مدل شدن همه)، سید با HrSeedContext (شامل دادهٔ دمو، کارکنان دمو و مولد تصادفی قطعی)، حذف دادهٔ دمو، و تابع‌های آمار و حذف آبشاری شرکت. سیدها بر اساس "), H.t("(SeedOrder, Code)", { rtl: false }), H.t(" مرتب و اجرا می‌شوند تا وابستگی‌ها رعایت شود.")]));
body.push(h2("۳.۲ مانیفست UI و منو", pal));
body.push(p("IHrUiModule با فهرست Controllers (مانیفست مسیرها برای گیت) و OwnedActions (کنترلرهای هاستی که ماژول مالکشان است) و NavGroups اعلام می‌کند کجا دیده شود. ModuleCatalog همهٔ ماژول‌ها را به یک منوی ادغام‌شده تبدیل می‌کند: گروه‌های هم‌نام یکی می‌شوند (Sort کمینه)، آیتم‌ها با Sort مرتب، و ساخت نقشهٔ controller→module با تشخیص تکرار و خطا. مانیفست سالم در App_Data/modules.manifest.json نگه‌داری می‌شود تا برای DLLهای حذف‌شده هم پیام دوستانه نمایش داده شود."));
body.push(h2("۳.۳ گیت‌های سه‌گانه", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۳ — سه لایهٔ تصمیم «این درخواست مجاز است؟»",
  headers: ["گیت", "پرسش", "رفتار رد"],
  widths: [22, 40, 38],
  rows: [
    ["LicenseGate", "مجوز نصب معتبر است و این ماژول در فهرست مجاز است؟", "ریدایرکت به /License/Notice یا 403؛ مسیرهای آزاد معاف"],
    ["ModuleGate", "ماژولِ این کنترلر نصب و فعال است؟", "صفحه: /Home/ModuleDisabled؛ AJAX: 403 JSON؛ API: 503 JSON"],
    ["Authorization", "کاربر کلید دسترسی لازم را دارد و رکورد در دامنهٔ اوست؟", "401/403 یا صفحهٔ Denied؛ فیلتر دامنه در کوئری"],
  ],
}));

body.push(h1("۴. pipeline درخواست — از سوکت تا ویو", pal));
body.push(...code([
  "UpdateApplier.RunIfPending          // نصب آپدیت در صف، قبل از هر چیز",
  "SecurityHeaders (CSP/HSTS/COOP/…)   // روی پاسخ استاتیک هم اعمال می‌شود",
  "Embedding (CHIPS cookies)           // برای نصب درون iframe",
  "UseStaticFiles + /dl static         // مرکز دانلود (نیازمند وجود پوشه هنگام استارت)",
  "UseRateLimiter                      // 60/min login ، 600 burst global ، 429",
  "CultureMiddleware                   // hr_lang + تقویم + ارز (بدون تغییر CultureInfo)",
  "SetupGate                           // ریدایرکت سراسری تا پیکربندی کامل",
  "UseRouting + UseAuthentication",
  "RbacRefreshMiddleware               // بازسازی claims در هر ریکوئست",
  "LicenseGate -> ModuleGate -> UseAuthorization",
  "MapGet(/health) + MVC + ConfigureApp(/iclock، /anpr)",
  "DbInit.InitializeAsync              // خودترمیم اسکیما + سید (خطا=RepairMode)",
], { caption: "ترتیب واقعی middleware در Program.cs" }));
body.push(bullet("DataProtection با مسیر کلید ثابت و ApplicationName ثابت (HrAttendance.Web) تا کوکی‌ها در استقرار مجدد زنده بمانند."));
body.push(bullet("JWT دسترسی در کوکی hr_at هم خوانده می‌شود تا هم API بیر و هم مرورگر با یک مکانیزم سرو شوند؛ Issuer/Audience با ClockSkew صفر."));
body.push(bullet("خطای استارت بانک هرگز اپ را نمی‌کشد: SetupState به RepairMode می‌رود و ویزارد با پیام فارسی دقیق (نبود سرور، خطای لاگین، timeout) باز می‌شود."));

body.push(h1("۵. معماری داده", pal));
body.push(h2("۵.۱ Contextهای چندگانه و مدل ترکیبی", pal));
body.push(p("پنج DbContext وجود دارد: AppDbContext (ترکیبی هاست)، HrBaseDbContext (فقط هسته برای ابزارها و تست) و چهار context اختصاصی Attendance/Access/Visitors/Payroll/Parking. ماژول‌های Org و Devices موجودیت‌هایشان را از طریق FamilyTypes به مدل ترکیبی تزریق می‌کنند. ApplySchemas به هر جدول schemaِ کد ماژول می‌دهد (در SQL Server)؛ جداول هسته زیر schema است. قواعد مدل در HrModelDefaults نرمال می‌شود: نام جدول‌های پایه، دقت اعشار 18/2 و طول شناسه‌ها."));
body.push(h2("۵.۲ خودترمیم اسکیما — جایگزین EF Migrations", pal));
body.push(...code([
  "DbInit.InitializeAsync(db, cfg, demo):",
  "  EnsureCreated()                       // ساخت اولیه",
  "  EnsureModuleSchemas()                 // schema per module + انتقال legacy",
  "  CreateMissingTables()                 // تفاوت مدل EF با بانک واقعی",
  "  PatchSchema() + PatchMissingColumns() // ستون‌های تازه",
  "  SeedLanguages + ImportLangPacks       // fa/en + بسته‌های embedded",
  "  EnsureRbac -> SeedModules(demo) -> DefaultUsers -> Settings -> Palettes",
  "  foreach IModuleSeeder (ordered)       // idempotent در هر بوت",
], { caption: "الگوریتم خودترمیم و سید" }));
body.push(p("پیشامد طراحی: تغییر مدل EF کافی است و استارت بعدی خودش را می‌سازد؛ به همین دلیل مهاجرت‌های سنگین تاریخ (نمونه: تبدیل همهٔ ستون‌های تاریخ به epoch-seconds با مهاجرت 0017 و ValueConverter متناظر) به‌صورت خودکار در بوت اعمال می‌شوند. اسکریپت‌های دستی database/migrations فقط برای تعمیرهای موضعی SQL Server است و ابزار Platform.Migrate تنها کار کپی دادهٔ SQLite به SQL Server را انجام می‌دهد."));
body.push(h2("۵.۳ پیکربندی و رمزنگاری تنظیمات", pal));
body.push(p("اتصال بانک و پیکربندی حساس در فایل hrsettings.bin با DataProtection (AES-256 + HMAC، کلیدریگ در پوشهٔ keys، هدف HrAttendance.SetupSettings) ذخیره می‌شود؛ نوشتن اتمی و مهاجرت خودکار از نسخهٔ JSON قدیمی. گزینه‌های DbContext در هر scope از نو ساخته می‌شوند تا تغییر اتصال از پنل بدون ری‌استارت اثر کند."));

body.push(p("جداول تنظیمات و ابزارهای نمایشی: AppSettings ستون‌های HijriAdjust و ScreenSaverEnabled/Minutes/Seconds/RequirePassword (فاز ۹۶/۹۸) را دارد — ستون‌های جدید با وصال عمومی خودترمیم DbInit روی پایگاه‌های موجود ساخته می‌شوند؛ AttendanceExportTemplates جدول قالب‌های برون‌بری تردد در اسکیمای ماژول attendance است."));
body.push(h1("۶. امنیت", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۴ — لایه‌های امنیتی و مقادیر پیش‌فرض",
  headers: ["لایه", "مکانیزم", "پیش‌فرض"],
  widths: [24, 48, 28],
  rows: [
    ["هویت", "JWT HS256 + کوکی hr_at و hr_rt چرخشی با هش Sha256", "دسترسی ۶۰ دقیقه، رفرش ۷ روز"],
    ["رمز", "PBKDF2-SHA256 صد هزار تکرار + تاریخچه و انقضا", "۹۰ روز، عدم تکرار ۳ رمز"],
    ["قفل ورود", "۵ خطا = قفل ۱۵ دقیقه + ۱۰ تلاش/۵ دقیقه بر IP:username", "پاسخ 423 و حسابرسی"],
    ["نرخ", "FixedWindow لاگین + TokenBucket سراسری", "60/min و 600 burst با 429"],
    ["هدرها", "CSP، nosniff، Referrer-Policy، COOP، Permissions-Policy", "default-src self؛ بدون CDN خارجی"],
    ["حسابرسی", "AuditLog برای ورود/خروج و عملیات نوشتاری و آپدیت/پشتیبان", "IP و جزئیات JSON"],
    ["تنظیمات", "hrsettings.bin رمزشده + کلید JWT تصادفی خودکار", "بدون مقدار پیش‌فرض خطرناک"],
  ],
}));
body.push(p("دسترسی‌سنجی دو لایه دارد: کلیدهای module.area.action با تطبیق ارثی در RbacService (دقیق، ستاره، wildcard، والد) و دامنهٔ رکوردی با RoleScope (all/company/dept/self) که اجتماع نقش‌ها و تقاطع سقف شرکت کاربر است و در کوئری به شرط‌های SQL با چانک ۴۰۰ شناسه ترجمه می‌شود. میان‌افزار RbacRefresh هر ریکوئست claims را از بانک تازه می‌کند تا لغو دسترسی بی‌درنگ اثر کند."));

body.push(h1("۷. مجوز نصب و گیت تجاری", pal));
body.push(p("فایل مجوز payload و امضای RSA-SHA256 است و اثر انگشت سخت‌افزار (MachineGuid یا machine-id + نام دستگاه، هش‌شده و گروه‌بندی ۵تایی) آن را به یک سرور قفل می‌کند. LicenseService وضعیت را با کش ۲۰ ثانیه‌ای می‌خواند؛ ModuleStateService سقف ماژول‌های مجوز را روی وضعیت فعال ماژول‌ها اعمال می‌کند. حالت lockMode به دو شکل warn (فقط بنر و صفحهٔ هشدار یک‌بار در هر ورود) و lock (قفل کامل صفحات به‌جز مسیرهای آزاد) عمل می‌کند. مسیرهای آزاد همیشگی: License، Auth، health، Setup، api و مسیرهای پل دستگاه‌ها."));

body.push(h1("۸. به‌روزرسانی درون‌برنامه‌ای", pal));
body.push(...code([
  "Upload zip (<=300MB) -> zip-slip guard + manifest check",
  "manifest: { format: hr-update-pack, version > current, minVersion?, notes }",
  "staging: App_Data/updates/staging-<ts> + pending.json",
  "Apply : BackupCurrent -> App_Data/backups/backup-<ts>.zip",
  "        write apply.json -> copy bin/wwwroot/web.config",
  "Linux : copy فوری   |   Windows: بوت بعدی یا Task HR Platform Updater",
  "App_Data و logs هرگز بازنویسی نمی‌شوند؛ staging ناموفق -> failed-*",
], { caption: "چرخهٔ به‌روزرسانی" }));
body.push(p("نکتهٔ معماری مهم: وضعیت روشن/خاموش ماژول‌ها فایلی است (App_Data/modules.json) نه بانکی — به همین دلیل آپدیت یا مهاجرت بانک هرگز وضعیت ماژول‌ها را به‌هم نمی‌زند و DLLهای تازه در بوت بعدی خودکار کشف می‌شوند."));

body.push(h1("۹. معماری گیت‌وی دستگاه‌ها", pal));
body.push(...tbl({
  palette: pal, caption: "جدول ۵ — اجزای گیت‌وی و مسئولیت هر جزء",
  headers: ["جزء", "مسئولیت", "پارامتر کلیدی"],
  widths: [24, 48, 28],
  rows: [
    ["GatewayWorker", "حلقهٔ سرپرست: خواندن کانفیگ، بالا/پایین کردن pollerها و دوربین‌ها", "ConfigRefreshSec=30"],
    ["ZkPoller", "اتصال TCP به دستگاه ZK، اجرای دستورها و پول ترددها", "PollIntervalSec=60 (حداقل 15)"],
    ["AdmsEndpoints", "دستگیرهٔ HTTP دستگاه‌های PUSH روی مسیر /iclock/cdata", "SN + دستورهای متنی C:id"],
    ["AnprEndpoints", "دریافت پلاک و ضربان دوربین روی /anpr/*", "HeartbeatSec=30"],
    ["AdmsRelay", "پل شفاف TCP روی پورت اختصاصی به وب (سازگاری قدیمی)", "ListenPort=8081"],
    ["IDeviceGatewayChannel", "پل داده به هاست: تنظیمات، دستگاه، دستور، تردد، کاربر، قالب", "صف دستور ≤20 در هر pull"],
    ["سرویس مستقل C#", "همان پل بیرون از اپ به‌صورت Windows Service/systemd", "کانفیگ زنده از /api/gateway/config"],
  ],
}));
body.push(p("جریان داده: دستگاه یا پل مستقل، رویداد را به channel می‌دهد؛ channel با DeviceOps.IngestPunchesAsync تردد را به موتور حضور می‌دهد (اولین ورود/آخرین خروج و محاسبهٔ AttCalc)، کاربران و قالب‌های بیومتریک با BioSync ذخیره می‌شوند و پلاک‌ها به IParkingGatewaySink می‌روند. وضعیت زندهٔ گیت‌وی عمداً در حافظه است (سقف ۳۰۰ رخداد) و جدول جدید نمی‌سازد."));

body.push(h1("۱۰. i18n و نمایش — فارسی در مرکز", pal));
body.push(p("زبان مبدأ فارسی است و ترجمه به‌صورت نگاشت متن‌فارسی به ترجمه در دو لایه: بانک (HrLangDefs و HrLangEntries) و دیکشنری ثابت انگلیسی. CultureMiddleware فقط زبان و جهت و تقویم و ارز را تعیین می‌کند و عمداً CultureInfo را تغییر نمی‌دهد تا رفتار binding تغییر نکند. نمایش تاریخ (جلالی/میلادی/قمری)، ارقام فارسی در RTL و قالب مبلغ با نرخ تبدیل، همه در لایهٔ نمایش رخ می‌دهد و دادهٔ بانک همیشه میلادی/epoch است. بسته‌های زبان قالب hr-lang-pack دارند و ایمپورت آن‌ها فقط ادغام است."));

body.push(h1("۱۱. موتورهای محاسبه", pal));
body.push(bullet("AttCalc (قرارداد مشترک): محاسبهٔ کارکرد از شیفت و پانچ — عبور از نیمه‌شب، رواداری دیرکرد، کسر استراحت، تعجیل، اضافه‌کاری پس از ۱۰ دقیقه از پایان موردانتظار و وضعیت غایب/تأخیر/حاضر. پیاده‌سازی ناب و بدون وابستگی برای استفاده در هر دو سمت وب و تست."));
body.push(bullet("موتور حقوق: پارسر امن فرمول با شناسه‌های فارسی — متغیرهای کارکرد و پرسنلی، توابع round/min/max/abs/ceil/floor/if، تابع قلم('کد') برای ارجاع بین اقلام و تابع مالیات با پلکان تعریف‌پذیر در پارامترها؛ خطاها پیام فارسی می‌گیرند."));
body.push(bullet("گزارش‌ها: منابع گزارش از اینترفیس‌های ReportSource می‌آیند (حضور، پارکینگ، سفارشی) و خروجی‌ها با PdfReport و اکسل‌نویس مشترک ساخته می‌شوند تا قالب و ارز یکدست بماند."));

body.push(h1("۱۲. نقشهٔ توسعهٔ آینده و مرزهای معماری", pal));
body.push(bullet("افزودن ماژول جدید هرگز هاست را تغییر نمی‌دهد: دو csproj جدید، دو پیاده‌سازی قرارداد، ثبت در Solution — از همین سند و سند تحویل هوش مصنوعی استفاده کنید."));
body.push(bullet("هر قابلیت جدید بین‌ماژولی باید اول به‌صورت اینترفیس در Contracts طراحی شود؛ DTOها آنجا زندگی می‌کنند و پیاده‌سازی خالی برای نبودِ ماژول اجباری است."));
body.push(bullet("قواعد انصراف‌ناپذیر: بدون insert مستقیم از پل به جدول‌های کسب‌وکار، بدون تغییر متن کلیدهای ترجمه، بدون شکستن ترتیب middleware، و بدون حذف فیچر موجود."));
body.push(note("جمع‌بندی: استحکام این سامانه از سه قید می‌آید — قراردادهای کوچک صریح، خودترمیمی به‌جای مهاجرت‌های شکننده، و امنیت پیش‌فرض. هر تصمیم جدید باید با همین سه قید سازگار باشد.", pal, { bold: true }));

module.exports = { file: "/home/z/my-project/download/docs/HR-Architecture-FA.docx",
  palette: pal,
  cover: { title: "مستندات معماری نرم‌افزار", subtitle: "لایه‌ها، قرارداد ماژول‌ها، pipeline امنیتی، لایهٔ داده، گیت‌وی دستگاه‌ها و موتورهای محاسبه", englishLabel: "SOFTWARE ARCHITECTURE", metaLines: ["سامانه: Platform.sln — نسخهٔ 2.7.11 — ASP.NET Core 8 + EF Core", "۱۶ پروژه در دو پوشهٔ solution — بانک SQLite/SQL Server", "مکمل: سند تحویل هوش مصنوعی و راهنمای ساخت هسته"], footerRight: "مستند معماری — نسخهٔ فنی", footerLeft: "نسخهٔ 1.0" },
  headerTitle: "مستندات معماری نرم‌افزار — سامانه منابع انسانی v2.7.11",
  bodyChildren: body };
