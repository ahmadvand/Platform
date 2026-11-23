#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۶۷ — بازنویسی بخش ثبت DI ماژول‌ها در Program.cs (گارد دولایه + NoInlining)"""
import io, sys

P = "/home/z/my-project/aspnet/src/HrAttendance.Web/Program.cs"
src = io.open(P, encoding="utf-8").read()
lines = src.split("\n")

# --- یافتن بازهٔ جایگزینی: از هدر «کانتکست لایهٔ دیتای پایه» تا پایان سیدرها (VisitorsSeeder) ---
start = end = None
for i, l in enumerate(lines):
    if start is None and "کانتکست لایهٔ دیتای پایه + لایهٔ دیتای هر ماژول" in l:
        start = i
    if "VisitorsSeeder();" in l:
        end = i  # includes the AddScoped line for VisitorsSeeder
if start is None or end is None or end < start:
    sys.exit(f"anchors not found: start={start} end={end}")

NEW = r'''// ---------- فاز ۶۱ + ۶۷ — کانتکست لایهٔ دیتای پایه (پلتفرم + ماژول‌های پایه) ----------
builder.Services.AddScoped<HrBaseDbContext>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new HrBaseDbContext(HrAttendance.Web.Infrastructure.DbConfig.BuildOptionsFor<HrBaseDbContext>(cfg).Options);
});

// ---------- فاز ۶۷ — لایهٔ دیتای ماژول‌ها: فقط اگر حاضر باشد ----------
// دو لایه گارد:
//   ۱) ثابت کامپایل HR_MODULE_* — بیلدی که از اول با -p:HrModuleX=false ساخته شده اصلاً
//      کد ثبت آن ماژول را ندارد (نه DLL، نه ثبت، نه جدول‌های بانک).
//   ۲) حضور فایل DLL لایهٔ دیتا در زمان اجرا (HrModulePresence) — بیلد کامل که DLL ماژولش
//      دستی حذف شده هم ثبت نمی‌شود؛ به‌جایش «جایگزین بی‌اثر» قراردادهای همان ماژول می‌نشیند
//      تا موتور اعلان‌ها/گزارش‌ساز/داشبورد/مسیر دستگاه‌ها بدون کرش بالا بیایند (رفتار = ماژول
//      نصب نیست؛ مسیرهای UI همان ماژول هم با ModuleGate بسته می‌شود).
// متدهای ثبت NoInlining هستند تا JIT هرگز نوعِ غایب را هنگام بارگذاری Program حل نکند.
#if HR_MODULE_ATTENDANCE
if (HrPlatform.Contracts.HrModulePresence.DataInstalled("attendance"))
    RegisterAttendanceData(builder.Services);
else
    HrAttendance.Web.Modules.HrModuleFallbacks.RegisterAttendance(builder.Services);
#else
HrAttendance.Web.Modules.HrModuleFallbacks.RegisterAttendance(builder.Services);
#endif

#if HR_MODULE_ACCESS
if (HrPlatform.Contracts.HrModulePresence.DataInstalled("access"))
    RegisterAccessData(builder.Services);
else
    HrAttendance.Web.Modules.HrModuleFallbacks.RegisterAccess(builder.Services);
#else
HrAttendance.Web.Modules.HrModuleFallbacks.RegisterAccess(builder.Services);
#endif

#if HR_MODULE_VISITORS
if (HrPlatform.Contracts.HrModulePresence.DataInstalled("visitors"))
    RegisterVisitorsData(builder.Services);
else
    HrAttendance.Web.Modules.HrModuleFallbacks.RegisterVisitors(builder.Services);
#else
HrAttendance.Web.Modules.HrModuleFallbacks.RegisterVisitors(builder.Services);
#endif

#if HR_MODULE_PAYROLL
if (HrPlatform.Contracts.HrModulePresence.DataInstalled("payroll"))
    RegisterPayrollData(builder.Services);
#else
#endif

#if HR_MODULE_PARKING
if (HrPlatform.Contracts.HrModulePresence.DataInstalled("parking"))
    RegisterParkingData(builder.Services);
else
    HrAttendance.Web.Modules.HrModuleFallbacks.RegisterParking(builder.Services);
#else
HrAttendance.Web.Modules.HrModuleFallbacks.RegisterParking(builder.Services);
#endif

// ---------- فاز ۶۱ — حسابرسی (پیاده‌سازی هاست، همیشه حاضر) ----------
builder.Services.AddScoped<IAuditSink, AuditSink>();

// ---------- فاز ۶۲ — سیدِ داده‌های اولیهٔ ماژول‌ها داخل متدهای ثبت بالا ----------
// هاست (نقش ترکیب) در DbInit همهٔ IModuleSeederها را به ترتیب Order اجرا می‌کند —
// فقط سیدِ ماژول‌های حاضر ثبت می‌شود؛ ماژول غایب هیچ سیدی ندارد (چیزی برای ریختن نیست).'''

lines[start:end + 1] = NEW.split("\n")
src = "\n".join(lines)

# --- متدهای ثبت (NoInlining) — بعد از خط «var app = builder.Build();» نمی‌گذاریم؛
#     بلافاصله قبل از بخش JWT اضافه می‌شود (محل خوانا و در دسترس هر دو مسیر) ---
REGFUNCS = r'''
// ==================== فاز ۶۷ — ثبت‌کننده‌های لایهٔ دیتای ماژول‌ها ====================
// هر کدام فقط وقتی صدا زده می‌شوند که DLL ماژول حاضر باشد (NoInlining = JIT امن).

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static void RegisterAttendanceData(IServiceCollection s)
{
    s.AddScoped<AttendanceDbContext>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        return new AttendanceDbContext(HrAttendance.Web.Infrastructure.DbConfig.BuildOptionsFor<AttendanceDbContext>(cfg).Options);
    });
    s.AddScoped<IAttendanceGateway, AttendanceGateway>();
    s.AddScoped<IAttendanceInsights, AttendanceGateway>();
    s.AddScoped<IAttendanceReportSource, AttendanceGateway>();
    s.AddScoped<HrPlatform.Contracts.IModuleSeeder, AttendanceSeeder>();
}

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static void RegisterAccessData(IServiceCollection s)
{
    s.AddScoped<AccessDbContext>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        return new AccessDbContext(HrAttendance.Web.Infrastructure.DbConfig.BuildOptionsFor<AccessDbContext>(cfg).Options);
    });
    s.AddScoped<IAccessGateway, AccessGateway>();
    s.AddScoped<HrPlatform.Contracts.IModuleSeeder, AccessSeeder>();
}

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static void RegisterVisitorsData(IServiceCollection s)
{
    s.AddScoped<VisitorsDbContext>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        return new VisitorsDbContext(HrAttendance.Web.Infrastructure.DbConfig.BuildOptionsFor<VisitorsDbContext>(cfg).Options);
    });
    s.AddScoped<IVisitorsGateway, VisitorsGateway>();
    s.AddScoped<HrPlatform.Contracts.IModuleSeeder, VisitorsSeeder>();
}

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static void RegisterPayrollData(IServiceCollection s)
{
    s.AddScoped<PayrollDbContext>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        return new PayrollDbContext(HrAttendance.Web.Infrastructure.DbConfig.BuildOptionsFor<PayrollDbContext>(cfg).Options);
    });
    s.AddScoped<HrPlatform.Contracts.IModuleSeeder, PayrollSeeder>();
}

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static void RegisterParkingData(IServiceCollection s)
{
    s.AddScoped<ParkingDbContext>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        return new ParkingDbContext(HrAttendance.Web.Infrastructure.DbConfig.BuildOptionsFor<ParkingDbContext>(cfg).Options);
    });
    s.AddScoped<IParkingReportSource, ParkingReportSource>();
    s.AddScoped<HrPlatform.Contracts.IModuleSeeder, ParkingSeeder>();
}
'''

anchor = "// ---------- JWT ----------"
idx = src.find(anchor)
if idx < 0:
    sys.exit("JWT anchor not found")
src = src[:idx] + REGFUNCS + "\n" + src[idx:]

# --- هشدار ترکیب غیرمنصوف: حقوق بدون حضور (بعد از Build) ---
appb = "var app = builder.Build();"
i2 = src.find(appb)
if i2 < 0:
    sys.exit("app build anchor not found")
warn = appb + r'''

// فاز ۶۷ — ترکیب هشدار: ماژول حقوق به دادهٔ ماژول حضور (کارکرد/شیفت) وابسته است.
#if HR_MODULE_PAYROLL && !HR_MODULE_ATTENDANCE
app.Logger.LogWarning("ماژول حقوق در این بیلد بدون ماژول حضور فعال است — دادهٔ کارکرد/شیفت در دسترس نیست و سید حقوق فهرست شیفت‌ها را خالی می‌بیند.");
#endif'''
src = src.replace(appb, warn, 1)

io.open(P, "w", encoding="utf-8").write(src)
print("Program.cs updated OK")
