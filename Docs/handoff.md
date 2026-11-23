# handoff.md — سند تحویل پروژه به هوش مصنوعی توسعه‌دهنده

> **هدف این سند:** هر هوش مصنوعی (یا توسعه‌دهندهٔ انسانی) که این مخزن را تحویل می‌گیرد، باید بتواند **بدون شکستن هیچ فیچر موجودی**، فیچرهای جدید را با همان معماری، قراردادها و قواعد کیفی به سامانه اضافه کند. این سند را همراه سورس بخوانید؛ هر جا ابهام بود، پاسخ در سورس با همین نام‌ها و مسیرها پیدا می‌شود.
>
> **نسخهٔ سامانه:** 2.7.7 · **پلتفرم:** ASP.NET Core 8 (LTS) + EF Core · **زبان رابط:** فارسی (RTL، فونت Vazirmatn) · **پورت پیش‌فرض:** 3000

---

## ۰) قواعد طلایی — قبل از هر تغییری بخوان

1. **هیچ فیچری حذف نمی‌شود.** حذف یا «ساده‌سازی» رفتار موجود ممنوع است؛ فقط افزودن و ارتقا.
2. **فقط نسخه‌های جدید.** هیچ وابستگی منسوخ‌شده یا پروژهٔ legacy بازنمی‌گردد؛ همیشه آخرین نسخهٔ پایدار.
3. **هرگز پروژه‌های قدیمی `Hr.*`/`Hr.Web` را بازنسازید یا رفرنس ندهید** — مهاجرت به `Platform.*` کامل شده است (`Platform.sln`).
4. تغییر نام/حذف کلیدهای ترجمهٔ فارسی ممنوع است؛ کلید ترجمه = خود متن فارسی (بخش i18n).
5. هر ماژول باید **بدون DLLهای ماژول‌های دیگر هم بالا بیاید** (قرارداد fallback صفر — بخش ۳.۵).
6. هر تغییری که رفتار ورود کاربر/دستگاه/حقوق را عوض می‌کند باید با اسکریپت‌های e2e (بخش ۹) تأیید شود.
7. پیام‌های کاربر همیشه فارسی و از درون `HrT.T("متن فارسی")` صدا زده شوند، هرگز string خام.
8. RTL بودن و فونت Vazirmatn دست‌نخورده بماند؛ هر ویجت جدید باید در RTL سالم باشد.

## ۱) نقشهٔ مخزن

```
/home/z/my-project
├── aspnet/
│   ├── Platform.sln                  ← ۱۶ پروژه در دو فولدر solution (src / modules)
│   ├── src/
│   │   ├── Platform.Contracts/       ← قراردادهای مشترک ماژول‌ها (بدون وابستگی)
│   │   │   ├── ModuleSystem.cs       ← IHrDataModule / IHrUiModule / HrSeedContext / HrModuleRuntime
│   │   │   ├── ModuleSeeder.cs       ← IModuleSeeder (سید خودترمیم در هر بوت)
│   │   │   ├── PeopleContracts.cs    ← IPeopleDirectory, IDevicesGateway
│   │   │   ├── HrContracts.cs        ← IAttendanceGateway/Insights/ReportSource, IAccessGateway, IVisitorsGateway, IAuditSink
│   │   │   ├── GatewayContracts.cs   ← IDeviceGatewayChannel, IParkingGatewaySink, IHrModuleBootstrapper, HrModuleRuntime
│   │   │   ├── DbResolver.cs         ← HrDbResolver (انتخاب Provider)
│   │   │   └── AttCalc.cs            ← موتور محاسبهٔ کارکرد (پورت att.ts)
│   │   ├── Platform.Core/            ← سرویس‌های مشترک (AuthCore, RbacService, Security, Jalali, Money, PdfReport…)
│   │   │   ├── Data/HrBaseDbContext.cs + HrModelDefaults.cs
│   │   │   └── Infrastructure/HasPerm.cs, ScopeOf.cs
│   │   ├── Platform.Web/             ← هاست (Program.cs، CoreModule، Infrastructure، Views)
│   │   │   ├── Program.cs            ← کل pipeline (بخش ۴)
│   │   │   ├── Modules/CoreModule.cs ← ماژول همیشه‌فعال هاست
│   │   │   ├── Infrastructure/       ← DbConfig, DbInit, SetupState/Secrets, LicenseGate, UpdateApplier, ModuleGate, SecurityHeaders, CultureMiddleware, RbacRefreshMiddleware, AuditSink…
│   │   │   └── wwwroot/              ← css/site.css, js/site.js, fonts/Vazirmatn-*.woff2
│   │   └── modules/
│   │       ├── HrModules.Org/        ← چارت، پرسنل، تعطیلات (Data: entities در ComposeModel)
│   │       ├── HrModules.Devices/    ← تعریف دستگاه، بیومتریک، LiveView
│   │       ├── HrModules.DeviceGateway/ ← worker داخلی + پل ADMS/ANPR
│   │       ├── HrModules.Attendance(+.Data)/ ← تردد، شیفت، درخواست‌ها
│   │       ├── HrModules.Access(+.Data)/    ← مناطق/گیت‌ها/کارت‌ها/حسابرسی تردد
│   │       ├── HrModules.Payroll(+.Data)/   ← لیست حقوق، فرمول‌ساز، عیدی
│   │       ├── Platform.Modules.Parking(+.Data)/ ← پارکینگ و پلاک‌خوان
│   │       └── Platform.Modules.Visitors(+.Data)/ ← ارباب‌رجوع و پیمانکاران
│   ├── tools/
│   │   ├── Platform.Migrate/         ← ابزار انتقال دادهٔ SQLite→SQL Server (HrMigrate)
│   │   ├── LicenseGen/               ← صدور مجوز CLI (keygen/issue/show)
│   │   └── LicenseStudio/            ← استودیو ویندوزی صدور مجوز
│   └── publish/                      ← خروجی build-customer.sh (gitignored)
├── device-service-csharp/            ← سرویس ویندوزی/سیستم‌دی گیت‌وی (نسخهٔ توصیه‌شده، .NET 8)
├── device-service/                   ← نسخهٔ Node.js همان پل (legacy-پشتیبانی)
├── database/migrations/*.sql         ← اسکریپت‌های دستی SQL Server (0004…0017)
├── installer/                        ← نصب‌کنندهٔ ویندوز (Inno Setup + PowerShell) ⚠️ فایل‌های ps1 باید UTF-8 **با BOM** باشند
├── scripts/                          ← عملیات: start-web.sh, keepalive.sh, build-customer.sh, make-update-package.*, backup-db.sh, e2e-*.sh…
├── download/                         ← خروجی‌های تحویلی (zipهای مشتری) — gitignored
└── worklog.md                        ← تاریخچهٔ فازها؛ در پایان هر فاز یک خط اضافه کنید
```

## ۲) فناوری‌ها و نسخه‌ها

| مؤلفه | مقدار |
|---|---|
| Runtime | .NET 8 (ASP.NET Core MVC + Razor، نه Blazor) |
| ORM | EF Core 8 (`EnsureCreated` + خودترمیمِ اسکیما؛ **EF Migrations استفاده نمی‌شود**) |
| بانک | SQLite (پیش‌فرض، فایل `hr.db` کنار content root) یا SQL Server (سازمانی) |
| احراز هویت | JWT + کوکی‌های `hr_at` (دسترسی ۶۰ دقیقه) و `hr_rt` (رفرش چرخشی ۷ روزه) + PBKDF2-SHA256 (100k) |
| لاگ | Serilog (کنسول + `logs/log-YYYYMMDD.txt`، نگهداری ۳۱ روز) |
| تقویم/عدد/ارز | `Platform.Core/Services/Jalali.cs`, `Money.cs` — نمایش شمسی/قمری/میلادی سوییچ‌پذیر |
| Font / UI | Vazirmatn (woff2 در wwwroot/fonts)، RTL پیش‌فرض، Chart.js (`wwwroot/js/chart.umd.js`) |
| API دستگاه‌ها | JWT Bearer برای سرویس پل (`svc_bridge`)، توکن دستگاه `X-Device-Token` |

## ۳) معماری ماژولار (مهم‌ترین قرارداد)

### ۳.۱ دو رابط اصلی — `Platform.Contracts/ModuleSystem.cs` و `GatewayContracts.cs`

- **`IHrDataModule`** (در پروژهٔ `*.Data`): `Code/Title/Icon/Description`, `SeedOrder`, `FamilyTypes[]` (مالکیت اسکیما), `ConfigureDataServices(IServiceCollection, IConfiguration, Action<DbContextOptionsBuilder>?)`, `ComposeModel(ModelBuilder)` + `ComposeCrossModule(ModelBuilder)` (دو پاس), `PreSeedAsync/SeedAsync(HrSeedContext)`, `ClearDemoAsync`, `OnCompanyDeleteAsync`, `CompanyCountsAsync/CompanyCountAsync/CompaniesWithConfigAsync`.
- **`IHrUiModule`** (در پروژهٔ UI): `Code/Title/Icon`, `IsCore` (فقط core=true), **`string[] Controllers`** (مانیفست مسیرها برای گیت), `OwnedActions[]`, **`HrNavGroup[] NavGroups`** (منو: `HrNavGroup(Name, Sort, Items[])` و `HrNavItem(Href, Icon, Label, Perm, Ctrl, Act?, Module?, Sort)`).
- **`IHrModuleBootstrapper`** (اختیاری): برای worker پس‌زمینه و Map کردن endpoint (نمونه: `GatewayBootstrapper`).
- **`IModuleSeeder`** (`ModuleSeeder.cs`): سید خودترمیم که **در هر بوت** اجرا و idempotent است.

### ۳.۲ کشف و ثبت — `HrModuleRuntime` (in GatewayContracts.cs)

- هاست فقط `CoreModule` را intrinsic ثبت می‌کند (`Program.cs:28`). بقیه با **کشف بازتابی DLLهای `HrModules.*.dll` / `Platform.Modules.*.dll`** از کنار اپ یا پوشهٔ `modules/` پیدا می‌شوند.
- `ComposeModel` دو پاس دارد: پاس ۱ مدل هر ماژول، پاس ۲ FKهای بین‌ماژولی؛ خانواده‌های غایب با `Ignore` رد می‌شوند (بدون جدول شبح). `ApplySchemas` به هر جدول schema کد ماژول می‌دهد (SQL Server)؛ هسته = `core`.
- `HrModuleRuntime.ConfigureDataServices/ConfigureServices/ConfigureApp/SeedModulesAsync` از Program.cs و DbInit صدا زده می‌شوند. **هیچ فهرست هاردکد ماژول وجود ندارد.**

### ۳.۳ منو و گیت

- `ModuleCatalog` (Platform.Web/Modules) منوی ادغام‌شده را از `HrModuleRuntime.UiModules()` می‌سازد (ادغام گروه‌های هم‌نام با `Sort=min`)، مپِ controller→module می‌سازد (تکراری = استثنا) و مانیفست سالم را در `App_Data/modules.manifest.json` می‌نویسد.
- خاموش/روشن کردن ماژول در **فایل** `App_Data/modules.json` (`{"disabled":["payroll"]}`) از طریق `ModuleStateService` است (عمداً بیرون DB، تا با آپدیت/مهاجرت DB پاک نشود). `Enabled(code)` = نصب‌شده ∧ فعال ∧ مجازِ مجوز نصب.
- `ModuleGate` (middleware، بعد از LicenseGate): صفحهٔ ماژول خاموش → ریدایرکت `/Home/ModuleDisabled?code=…`؛ AJAX → 403 JSON؛ `/api/*` → 503 JSON. کنترلر ناشناس = fail-open.
- وقتی DLL ماژولی نباشد، `ModuleFallbacks` پیاده‌سازی‌های `Null*Gateway` را (قبل از پلاگین‌های واقعی — «آخرین ثبت برنده است») ثبت می‌کند تا داشبوردها صفر ببینند و اپ کرش نکند.

### ۳.۴ قراردادهای بین‌ماژولی

ماژول‌ها همدیگر را از DI با اینترفیس‌های Contracts صدا می‌زنند، نه مستقیم: `IPeopleDirectory` (پرسنل/واحد/ورود ESS)، `IAttendanceGateway/Insights/ReportSource`، `IAccessGateway`، `IVisitorsGateway`، `IDevicesGateway`، `IDeviceGatewayChannel` (هاست→گیت‌وی)، `IParkingGatewaySink`، `IAuditSink`. DTOهای اشتراکی در همان فایل‌ها (`HrAttDashDto`, `HrPersonBrief`, …).

### ۳.۵ قانون آهنین «نبودِ ماژول»

هر فیچر جدید که به سرویس ماژول دیگری نیاز دارد باید: (۱) از قرارداد DI استفاده کند، (۲) نتیجهٔ null/خالی را بپذیرد، (۳) هرگز DLL ماژول دیگر را مستقیم رفرنس نکند (به‌جز پروژهٔ `*.Data` خودش و Contracts/Core).

## ۴) Pipeline درخواست — `Platform.Web/Program.cs` (ترتیب دقیق، دست‌نخورده)

1. `UpdateApplier.RunIfPending` — قبل از هر چیز (نصب آپدیت در صف)
2. Kestrel بدون Server header → ثبت CoreModule → `SetupSecrets.Source` (تنظیمات رمزشدهٔ `hrsettings.bin`) → Serilog → HtmlEncoder فارسی‌پسند
3. سرویس‌ها: DataProtection (`aspnet/keys`، AppName `HrAttendance.Web`)، Antiforgery cookie `hr_af`، RateLimiter (لاگین 60/min/IP، کل 600 burst/200 tps، 429)، AppDbContext/HrBaseDbContext (options per-scope!)، fallbackها، `ConfigureDataServices`، `IDeviceGatewayChannel`، `IAuditSink`، RBAC/Scope/Settings/License/HrLangStore/ModuleStateService، JWT (issuer `hr-attendance`، کوکی `hr_at` هم خوانده می‌شود)
4. Middleware به ترتیب: ExceptionHandler/Hsts → **SecurityHeaders** (قبل از استاتیک) → Embedding (CHIPS) → `UseStaticFiles` → static `/dl` (`ServeUnknownFileTypes`) → `UseRateLimiter` → **CultureMiddleware** (کوکی `hr_lang`؛ Culture را عمداً عوض نمی‌کند) → **Setup gate** (`/Setup`,`/health`,`/Ui/Lang` آزاد؛ `/api` → 503؛ `/iclock|/anpr` → 503) → Routing → Authentication → **RbacRefreshMiddleware** (بازسازی claims هر ریکوئست) → **LicenseGate** → **ModuleGate** → Authorization
5. Endpointها: `GET /health` (`SELECT 1` → 200/503) → MVC → `HrModuleRuntime.ConfigureApp` (Map `/iclock/cdata`، `/anpr/*`) → `DbInit.InitializeAsync` (اگر setup آماده؛ خطا = RepairMode، کرش نه)

## ۵) لایهٔ داده

- **Contextها:** `AppDbContext` (ترکیبی: هسته + ComposeModel همهٔ ماژول‌های حاضر) + context هر ماژول داده‌محور (Attendance/Access/Visitors/Payroll/Parking) + `HrBaseDbContext` (فقط هسته). Org/Devices/DeviceGateway context ندارند؛ موجودیت‌هایشان از طریق `FamilyTypes` به مدل ترکیبی اضافه می‌شود و با `AppDbContext.Set<T>()` کار کنید.
- **قرارداد ستون‌ها:** شناسه‌های رشته‌ای `MaxLength(64)`، اعشار 18/2، کلید ترکیبی‌ها/ایندکس‌های یکتا مطابق `HrBaseDbContext`. تاریخ‌ها در SQL Server به‌صورت **epoch-seconds bigint** ذخیره می‌شوند (مهاجرت 0017 + `EpochSecondsConverter`) — هرگز مستقیم `DateTime` خام به ستون‌های قدیمی ننویسید.
- **ساخت/ارتقای اسکیما:** `DbInit.InitializeAsync` = `EnsureCreated` → ساخت schemaهای ماژولی (SQL Server) → `CreateMissingTablesAsync` → `PatchSchemaAsync` → `PatchMissingModelColumnsAsync` → سید زبان‌ها → `EnsureRbacAsync` → seed دموی ماژول‌ها → کاربران پیش‌فرض → تنظیمات/پالت‌ها → همهٔ `IModuleSeeder`s. **یعنی: تغییر مدل EF کافی است؛ جدول/ستون جدید در بوت بعدی خودکار ساخته می‌شود.**
- **اسکریپت‌های دستی:** `database/migrations/NNNN_*.sql` فقط برای تعمیرهای موضعی SQL Server (اجرا دستی در SSMS، بعد از FULL BACKUP).
- **انتقال داده:** `tools/Platform.Migrate` (HrMigrate) — کپی ۴۱ جدول SQLite→SQL Server با `--from/--to/--no-create/--force/--verify`.
- **تنظیم اتصال:** `DbConfig` از `Database:Provider` + `Database:ConnectionString` در `hrsettings.bin` (رمزشده با DataProtection) می‌خواند؛ fallback به `ConnectionStrings:Sqlite|SqlServer`. تغییر اتصال از صفحهٔ تنظیمات بدون ری‌استارت اعمال می‌شود.

## ۶) امنیت، ورود و RBAC

- هش رمز: PBKDF2-SHA256، 100,000 تکرار، فرمت `pbkdf2-sha256$iter$saltB64$hashB64` (تعداد تکرار از خود رشته خوانده می‌شود — قابل ارتقا). سیاست رمز: `Jalali.ValidatePassword` (≥۸ کاراکتر، حرف+عدد، عدم تکرار ۳ رمز آخر، انقضای ۹۰ روزه).
- قفل شدن: ۵ خطا → قفل ۱۵ دقیقه (HTTP 423، audit `account_locked`)؛ محدودیت لایه ۲: ۱۰ تلاش/۵ دقیقه per `ip:username`.
- کاربران پیش‌فرض سیدشده (بخش راه‌اندازی): `admin/Admin@1234` (superadmin)، `hr/Hr@123456`، `operator/Oper@1234`، `guard/Guard@1234`، `viewer/View@1234`، `svc_bridge/Bridge@1234` — در اولین استقرار **رمز admin را فوراً عوض کنید.**
- کلیدهای دسترسی: `module.area.action` (کاتالوگ در جدول `Permissions` + `database/migrations/0009_permissions_catalog.sql`). تطبیق در `RbacService.Matches`: دقیق → `*` → wildcard `a.b.*` → والد. نقش‌ها ارث‌بری والد تا ۱۲ سطح دارند. `HasPermAttribute` روی کنترلر/اکشن؛ 401/403 JSON برای `/api` و ریدایرکت `/Auth/Denied` برای صفحات.
- **دامنهٔ داده:** `RoleScope` با kind های `all/company/dept/self`؛ `DataScopeService.ForUserAsync` اجتماعِ نقش‌ها ∩ سقف شرکت/واحد کاربر؛ در کنترلر: `await this.ScopeAsync()` و فیلترهای `ScopeQuery.Where*` (ترجمهٔ سمت SQL، چانک ۴۰۰ id).
- حسابرسی: `Audit(...)` (AuditExt/AuditSink) → جدول `AuditLogs` (login/logout، عملیات مدیریتی، آپدیت، پشتیبان…). حذف یا دور زدنش ممنوع.
- هدرهای امنیتی + CSP و RateLimiter فعال‌اند (`Security:*` در appsettings)؛ CSP اجازهٔ اسکریپت inline دارد ولی CDN/external ندارد — هر اسکریپت جدید باید لوکال باشد.

## ۷) مجوز نصب (Licensing) — بدون مجوز، سامانه قفل است

- فرمت فایل مجوز: `base64(JSON payload).base64(RSA-SHA256 signature)`؛ کلید **RSA-2048**. Payload: `product(HrAttendance), licensee, machine(اثر انگشت ۲۵کاراکتری), issuedAtUtc, expiresAtUtc?, maxEmployees?, modules[]?, lockMode(warn|lock)`.
- فایل مجوز: `App_Data/license.dat`. جفت‌کلید هر نصب: `App_Data/license-keys/` (کلید خصوصی با DataProtection رمز می‌شود). کلید عمومی فروشنده embedded در `LicenseService.PublicKeySpki`.
- صدور: `tools/LicenseGen` (CLI) یا `tools/LicenseStudio` (ویندوز) با فایل درخواست `.hrreq` از `/License`. تریال وجود **ندارد** (فاز ۸۳ب حذف شد؛ `License:Mode=Strict` همیشه).
- `LicenseGate`: مسیرهای آزاد همیشه `/License, /Auth, /health, /Setup, /api, /iclock, /anpr`؛ حالت lock صفحات → `/License/Notice`؛ سقف ماژول‌های مجوز در `ModuleStateService.LicenseAllows` اعمال می‌شود.

## ۸) به‌روزرسانی و بسته‌بندی

- بستهٔ آپدیت: zip با `manifest.json` ریشه (`format:"hr-update-pack"`, `version` بزرگ‌تر از جاری، `minVersion?`, `includeAppSettings?`, `notes{fa,en}`) + `bin/`, `wwwroot/` (بدون `dl`), اختیاری `web.config`/`appsettings*`. ساخت: `scripts/make-update-package.sh` (لینوکس) یا `.ps1` (ویندوز).
- اعمال: آپلود در `/Updates` (perm `updates.manage`) → اعتبارسنجی + zip-slip guard → staging در `App_Data/updates/` → **پشتیبان zip** در `App_Data/backups/` → پرچم `apply.json` → کپی (لینوکس فوری؛ ویندوز: در بوت بعدی یا Task Scheduler «HR Platform Updater» با `update-runner.ps1`). `App_Data` و `logs` هرگز overwrite نمی‌شوند. بازیابی: از همان صفحهٔ `/Updates` (Restore).
- بستهٔ مشتری: `scripts/build-customer.sh [excluded-modules-csv] [version] [vendor]` → `dotnet publish` Release و **حذف فیزیکی DLLهای ماژول‌های حذف‌شده** (پایهٔ core/org/devices حذف‌نشدنی‌اند). نصب‌کنندهٔ ویندوز: `installer/` (Inno Setup + `Install-HRPlatform.ps1` برای IIS + firewall + Task Updater). ⚠️ همهٔ `.ps1` باید UTF-8 **با BOM** ذخیره شوند وگرنه PowerShell 5.1 ویندوز فارسی ParserError می‌دهد.

## ۹) اجرا، تست و عیب‌یابی محلی

```bash
# بیلد کامل
cd aspnet && ~/.dotnet/dotnet build Platform.sln

# اجرا (پورت 3000، سندباکس: ویزارد رد می‌شود)
bash scripts/start-web.sh            # سوپروایزر خودترمیم با flock + گارد پورت
python3 scripts/daemonize.py /bin/bash scripts/start-web.sh   # اجرای دیتشد
bash scripts/keepalive.sh &          # واچ‌داگ هر ۲۰ثانیه /health-مانند

# سلامت
curl -s http://localhost:3000/health          # {"status":"healthy","db":"Sqlite",...}

# کوئری سریع DB (provider-agnostic)
python3 scripts/dbq.py "SELECT COUNT(*) FROM Employees"
bash scripts/backup-db.sh [--sqlite|--sqlserver] ; bash scripts/restore-db.sh <file.db.gz> [--check]

# تست‌های E2E (ورود admin/Admin@1234؛ خروجی سبز = OK)
bash scripts/smoke.sh                 # ۳۲ مسیر + exportها + webhook auth
bash scripts/e2e-devices-ajax.sh ; bash scripts/e2e-ess-ajax.sh ; bash scripts/parking-test.sh …
```

دام‌های شناخته‌شدهٔ محیط: بیلد سنگین هم‌زمان با اپ روی سندباکس کم‌حافظه → OOM اپ (سوپروایزر جبران می‌کند ولی بیلدها را وقتی اپ بالا است محدود کنید)؛ پروسه‌های بین نوبت‌ها kill می‌شوند → دابل‌فورک `daemonize.py`؛ پرچم‌های emoji در فایرفاکس رندر نمی‌شوند → آیکون‌ها SVG باشند (`HrFlags.Svg`).

## ۱۰) i18n، تقویم، عدد و ارز

- زبان مبدأ = **فارسی**؛ `HrT.T("متن فارسی")` زنجیرهٔ: بستهٔ داینامیک DB (`HrLangStore`) → دیکشنری static انگلیسی (`HrLang`) → خودِ متن فارسی. هرگز متن فارسی را به‌عنوان «کلید» تغییر ندهید — ترجمه‌ها می‌شکنند.
- زبان فعال: کوکی `hr_lang` → تنظیم پیش‌فرض → `Ui:Language` → `fa`. جهت (`HrT.Rtl`) و ارقام (`HrT.Num` → فارسی در RTL) و تاریخ (`HrT.TopDate` بر اساس `Jalali.DisplayMode`) خودکار. رشته‌های JS: `window.__hrL10n` + `hrT()` در site.js.
- بستهٔ زبان: `{"format":"hr-lang-pack","version":1,"code","title","dir","entries":{"متن فارسی":"translation"}}` — ایمپورت از `/Langs` (merge، هیچ‌وقت delete نمی‌کند؛ `fa` رد می‌شود). فایل‌های embedded در `App_Data/langpacks/hr-lang-*.json` در بوت merge می‌شوند.
- **فاز ۹۵ — پوشش کامل زبان‌ها (۱۰ زبان):** fa + en (static) + ar (عربی، rtl) + ky/ps/prs/ka/tk/tg/uz با بسته‌های کامل (~۲۹۰۰ کلید). کلیدهای DictEn = مرجع کامل (اسکریپت `scripts/i91-audit.py` = پوشش ۱۰۰٪) و `scripts/i95-langpacks.py` کلیدهای جاافتادهٔ هر زبان را با z-ai ترجمه و در App_Data/langpacks بسته می‌کند (کش `scripts/langpacks/cache2/`). ⚠️ دو دام که رفع شد: «کلید تکراری در بسته» → `ToDictionary` می‌شکست و کل بسته خالی می‌شد (حالا آخرین می‌برد)؛ ایمپورت هم `have` را درون حلقه به‌روز می‌کند. `HrT.T` را هرگز روی مقادیر آزاد کاربر (نام‌ها، دلیل درخواست‌ها) صدا نزنید — فقط روی واژگان ثابت/شمارشی.
- **پرچم‌ها واقعی‌اند:** `HrFlags.Svg` نگاشت کد←کشور (fa=ir, en=gb, ar=sa, ky=kg, ps/prs=af, ka=ge, tk=tm, tg=tj, uz=uz) و `<img src="/img/flags/*.svg">` (منبع flagcdn — فایل‌ها در `wwwroot/img/flags/`)؛ کد ناشناخته = آیکن کرهٔ زمین. برای زبان پویای جدید، فایل پرچم را همان‌جا بریزید و نگاشت را در `HrFlags.Files` اضافه کنید.
- **درخت دسترسی‌ها (`Views/Users/_RolePerms.cshtml`):** عنوان دسترسی‌های DB از `HrT.T` می‌گذرد و نودهای میانی کلید (`access`, `payroll.runs`…) از نقشهٔ `segMap` برچسب فارسی می‌گیرند — ماژول جدید = یک ردیف به آن نقشه.
- مبالغ: همیشه `Money.Fmt(...)` (واحد/نرخ از تنظیمات). تاریخ‌ها همیشه از `Jalali.*` — ذخیرهٔ DB میلادی/epoch است، فقط نمایش تغییر می‌کند.

## ۱۱) قراردادهای UI (RTL / Vazirmatn / الگوهای صفحات)

- Layout: `Views/Shared/_Layout.cshtml` — سایدبار از `ModuleCatalog.BuildNav`، topbar (جستجوی سراسری `/Home/Search`، breadcrumb، تاریخ زنده، سوییچ زبان با پرچم واقعی SVG (`HrFlags.Svg`)، زنگ اعلان، منوی کاربر). صفحه‌ها pjax-like با `data-refresh`.
- مرکز دانلود `/dl`: فقط اگر پوشهٔ `wwwroot/dl` هنگام بوت موجود باشد مپ می‌شود؛ `/dl/` از فاز ۹۵ خودِ `index.html` را می‌دهد (`UseDefaultFiles`).
- فرم‌ها: مودال‌های AJAX با `data-modal` + فرم‌های `data-ajax` + توست `TempData["ToastOk"/"ToastErr"]` + تأیید `data-confirm`. هر فرم دارای anti-forgery است.
- **دراپ‌داون‌ها:** به‌جای `<select>` بومی از کامپوننت سفارشی site.js استفاده می‌شود (برای فونت Vazirmatn). قاعده: پنلِ باز باید دقیقاً زیر تریگر باشد و در مودال‌ها با overflow هم درست بایستد — هر ویجت جدید دراپ‌داون را با همین کامپوننت بسازید. ⚠️ مشکل شناخته‌شدهٔ در حال تعمیر: جابه‌جایی پنل در صفحهٔ «تعریف دستگاه جدید» + فاصله‌گذاری بعضی فرم‌ها (bugهای UI گزارش‌شدهٔ باز — در کارهای جدید تکرار نشوند).
- آیکون‌ها emoji درون متن + پرچم‌ها SVG. جدول‌ها zebra، فونت اعداد Vazirmatn. تاریخ‌ها input میلادی با نمایش شمسی در سلول‌ها؛ در فرم‌های مرتبط راهنمای شمسی نشان داده شود.

## ۱۲) دستورالعمل‌های پیاده‌سازی (Recipes)

### Recipe A — افزودن ماژول کامل جدید (مثال: `HrModules.Recruitment`)

1. **پروژهٔ داده** `aspnet/src/modules/HrModules.Recruitment.Data` (classlib net8.0، ref به Contracts+Core):
   - موجودیت‌ها با قراردادها (`Id` رشته‌ای ≤64، `CompanyId`، …)؛ `RecruitmentDbContext` (اختیاری ولی توصیه‌شده) یا فقط ComposeModel.
   - `RecruitmentDataModule : IHrDataModule` — `Code="recruitment"`, `FamilyTypes` = همهٔ موجودیت‌ها, `SeedOrder=110`, `ComposeModel` (+`ComposeCrossModule` برای FK به Employees)، `SeedAsync` idempotent، `ClearDemoAsync`، `OnCompanyDeleteAsync` (حذف آبشاری داده‌های شرکت).
   - در صورت سید خودترمیم: کلاس `RecruitmentSeeder : IModuleSeeder` (`ModuleCode="recruitment"`).
2. **پروژهٔ UI** `aspnet/src/modules/HrModules.Recruitment` (ref به Data + Core + Contracts):
   - `RecruitmentController : HrModuleControllerBase` با `[HasPerm("recruitment.candidates.view")]` و الگوی `MvcBase`/`SafeMvc` (حسابرسی خودکار writeها).
   - Views Razor (Layout ماژول‌ها مثل ماژول‌های موجود `_ViewStart` → `~/Views/Shared/_Layout.cshtml`).
   - `RecruitmentUiModule : IHrUiModule` — `Controllers=["recruitment"]`, `NavGroups=[ new("منابع انسانی",20,new HrNavItem("/Recruitment","🎯","جذب","recruitment.candidates.view","recruitment")) ]`.
   - افزودن پروژه‌ها به `Platform.sln` و ProjectReference از `Platform.Web` به UI (و Data).
3. **دسترسی‌ها:** کلیدهای جدید را به کاتالوگ اضافه کنید (seed `Permission` idempotent یا `database/migrations/NNNN_*.sql` برای SQL Server).
4. **ترجمه:** همهٔ متن‌ها با `HrT.T`؛ برای انگلیسی کلید جدید به دیکشنری `HrLang` اضافه کنید (دوسترونگاه: بدون کلید هم فارسی نمایش داده می‌شود).
5. **بیلد + e2e + لاگ فاز:** `dotnet build Platform.sln`، اسکریپت e2e تازه در `scripts/e2e-recruitment.sh` (ورود، بازکردن صفحه، ساخت رکورد، حذف)، یک خط به worklog.md.

### Recipe B — افزودن صفحه/اکشن به ماژول موجود

کنترلر همان ماژول را باز کنید → اکشن با `[HasPerm("area.thing.view")]` → ویو در همان پوشه → آیتم منو/دکمه با همان perm → اگر اکشنِ host-owned است از `OwnedActions` استفاده کنید. اگر صفحه به دادهٔ ماژول دیگری نیاز دارد، از اینترفیس Contracts (بخش ۳.۴) صدا بزنید و fallback خالی را تحمل کنید.

### Recipe C — تغییر اسکیما

مدل EF را تغییر دهید (فیلد/جدول/ایندکس) → بیلد → اجرای اپ: `DbInit` ستون/جدول جدید را خودکار می‌سازد. برای دیتای سنگین مهاجرت داده را در یک `IModuleSeeder`/patch idempotent بنویسید (هر بوت اجرا ولی بی‌اثر پس از بار اول). برای SQL Server سازمانی اگر patch دستی لازم شد: `database/migrations/NNNN_name.sql` با هدر «اجرا: روی بانک HrAttendance در SSMS (پیشنهاد: ابتدا FULL BACKUP)». تاریخ‌ها epoch-seconds (بخش ۵).

### Recipe D — کلید دسترسی جدید

فرمت `module.area.action`؛ «والدِ» کلیدهای مدیریت = `.manage`. در کاتالوگ (seed `Permission`) ثبت، در `RbacService.PermParents` اگر والد تازه لازم شد، و در UI نقش‌ها (`/Users/Roles`) خودکار ظاهر می‌شود.

### Recipe E — endpoint جدید برای دستگاه/سرویس بیرونی

اگر پل دستگاه است: به `HrModules.DeviceGateway` (endpoint درون اپ) یا `device-service-csharp` اضافه شود؛ احراز یا JWT `svc_bridge` یا توکن دستگاه (`X-Device-Token`). هر دادهٔ ورودی از مسیر `DeviceOps.IngestPunchesAsync`/`BioSync.*`/`IParkingGatewaySink.IngestPlateAsync` به DB برود، نه insert مستقیم.

## ۱۳) تعریف «تمام‌شده» برای هر فیچر

- [ ] بیلد صفر خطا/اخطار جدید در `dotnet build Platform.sln`
- [ ] اپ بدون DLLهای ماژول‌های دیگر بالا می‌آید (تست حداقلی: پوشهٔ بدون ماژول)
- [ ] همهٔ رشته‌ها از `HrT.T` و در حالت ان (LTR) و فا (RTL) سالم
- [ ] perm جدید در کاتالوگ + تخصیص نقش + تست 401/403/مجاز
- [ ] حسابرسی عملیات نوشتاری + دادهٔ scope-دار (شرکت/واحد)
- [ ] `scripts/smoke.sh` + e2e تازه سبز
- [ ] RTL سالم در 1280/1024px، فونت Vazirmatn، بدون به‌هم‌ریختگی مودال
- [ ] یک خط در `worklog.md` با پیشوند فاز

## ۱۴) نقشهٔ مسیرهای کلیدی (مرجع سریع)

| مسیر | کار |
|---|---|
| `/Auth/Login` | ورود یکپارچه (مدیر: username+password، پرسنل: کد پرسنلی+کد ملی) |
| `/` | داشبورد (KPI + چارت + دستگاه‌ها + رویدادها) |
| `/Employees`, `/Org`, `/Shifts`, `/Holidays` | منابع انسانی |
| `/Attendance`, `/Requests`, `/Self/*` | تردد، درخواست‌ها، پرتال من |
| `/Devices`, `/Devices/LiveView`, `/Biometrics`, `/Gateway`, `/DeviceGateway` | دستگاه‌ها و گیت‌وی |
| `/Access`, `/Cards`, `/Access/Muster` | کنترل تردد و حراست |
| `/Parking`, `/Visitors` | پارکینگ و ارباب‌رجوع |
| `/Payroll`, `/Payroll/Items`, `/Payroll/Params`, `/Payroll/Bonus` | حقوق و دستمزد |
| `/Reports`, `/Reports/Custom`, `/Reports/Parking` | گزارش‌ساز |
| `/Users`, `/Users/Roles`, `/Companies`, `/Settings`, `/Langs`, `/License`, `/Updates`, `/Audit`, `/Notifications` | مدیریت سیستم |
| `/iclock/cdata?SN=` , `/anpr/plate?SN=` , `/api/devices/webhook` | پل دستگاه‌ها/دوربین‌ها |
| `/api/auth/login|refresh|me` , `/api/gateway/config` | API سرویس پل |
| `/Setup`, `/health` | ویزارد راه‌اندازی و سلامت |
