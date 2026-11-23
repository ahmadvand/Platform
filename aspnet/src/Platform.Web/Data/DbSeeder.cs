using System.Globalization;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Web.Data;

// ============================================================
// سیدر — پورت کامل src/lib/seed.ts (بازبینی فاز ۶۹)
// هستهٔ پلتفرم: RBAC (کاتالوگ دسترسی‌ها، نقش‌ها، کاربران) + شرکت نمونه
// + تنظیمات برندینگ/پالت + هشدارهای امنیتی متنی.
// پرسنل/چارت/قرارداد/احکام/تعطیلات ∈ ماژول org (HrModules.Org.Data) و
// دستگاه‌ها/ثبت‌های هویتی ∈ ماژول devices (HrModules.Devices.Data) —
// با SeedOrder در زنجیرهٔ HrModuleRuntime.SeedModulesAsync سید می‌شوند؛
// بدون آن ماژول‌ها هیچ‌کدام ساخته نمی‌شوند (کاربران پیش‌فرض بدون پیوند پرسنل).
// ============================================================
public static class DbSeeder
{
    // ---------- RNG قطعی (mulberry32) ----------
    private static Func<double> _rand = Mulberry32(1404);
    private static double Rand() => _rand();

    private static Func<double> Mulberry32(int seed)
    {
        uint a = (uint)seed;
        return () =>
        {
            a += 0x6D2B79F5;
            uint t = a;
            t = (t ^ (t >> 15)) * (1u | t);
            t ^= t + ((t ^ (t >> 7)) * (61u | t));
            return (t ^ (t >> 14)) / 4294967296.0;
        };
    }

    // ============================================================
    // کاتالوگ دسترسی‌ها (PLAN Phase 0) — تک‌منبع حقیقت
    // ============================================================
    public static readonly (string Key, string Title, string Grp)[] PermissionsCatalog =
    {
        ("dashboard.view", "مشاهده داشبورد", "عمومی"),

        ("companies.view", "مشاهده شرکت‌ها", "مدیریت سیستم"),
        ("companies.manage", "تعریف و مدیریت شرکت‌ها (چندشرکتی)", "مدیریت سیستم"),

        ("employees.view", "مشاهده پرسنل", "منابع انسانی"),
        ("employees.manage", "مدیریت پرسنل و قراردادها", "منابع انسانی"),
        ("employees.import", "ورود گروهی پرسنل از فایل اکسل (فاز ۷۴)", "منابع انسانی"),
        // فاز ۷۴ — کلیدهای ریز پرسنل (والد: employees.manage)
        ("employees.create", "[ریز] ثبت پرسنل جدید", "منابع انسانی — ریز"),
        ("employees.edit", "[ریز] ویرایش پرسنل", "منابع انسانی — ریز"),
        ("employees.delete", "[ریز] حذف پرسنل", "منابع انسانی — ریز"),
        ("org.view", "مشاهده چارت سازمانی", "منابع انسانی"),
        ("org.manage", "مدیریت ساختار سازمانی", "منابع انسانی"),
        // فاز ۷۵ — کلیدهای ریز چارت سازمانی (والد: org.manage)
        ("org.depts.manage", "[ریز] واحدهای سازمانی (ایجاد/ویرایش/جابه‌جایی)", "منابع انسانی — ریز"),
        ("org.positions.manage", "[ریز] پست‌های سازمانی", "منابع انسانی — ریز"),

        ("attendance.view", "مشاهده حضور و غیاب", "حضور و غیاب"),
        ("attendance.manage", "ثبت و اصلاح تردد", "حضور و غیاب"),
        // فاز ۷۴ — کلید ریز ثبت/اصلاح دستی تردد (والد: attendance.manage)
        ("attendance.records.manage", "[ریز] ثبت و اصلاح دستی رکورد تردد", "حضور و غیاب — ریز"),
        ("attendance.approve", "تأیید درخواست‌های تردد", "حضور و غیاب"),
        ("shifts.view", "مشاهده شیفت‌ها و نوبت‌کاری", "حضور و غیاب"),
        ("shifts.manage", "تعریف و مدیریت شیفت‌ها", "حضور و غیاب"),
        ("corrections.view", "مشاهده اصلاح تردد", "حضور و غیاب"),
        ("corrections.approve", "تأیید اصلاح تردد", "حضور و غیاب"),
        ("holidays.view", "مشاهده تقویم تعطیلات", "حضور و غیاب"),
        ("holidays.manage", "مدیریت تعطیلات", "حضور و غیاب"),

        ("access.view", "مشاهده کنترل تردد", "کنترل تردد"),
        ("access.manage", "مدیریت مناطق و مجوزها", "کنترل تردد"),
        // فاز ۷۵ — کلیدهای ریز کنترل تردد (والد: access.manage)
        ("access.zones.manage", "[ریز] مناطق، آنتی‌پس‌بک و وضعیت اضطراری", "کنترل تردد — ریز"),
        ("access.gates.manage", "[ریز] گیت‌ها، برنامهٔ زمانی و فرمان درب", "کنترل تردد — ریز"),
        ("access.perms.manage", "[ریز] مجوزهای تردد پرسنل", "کنترل تردد — ریز"),
        ("access.alerts.manage", "[ریز] رسیدگی به هشدارهای کنترل تردد", "کنترل تردد — ریز"),
        ("access.scan", "[ریز] ثبت تردد دستی در گیت", "کنترل تردد — ریز"),
        ("cards.view", "مشاهده کارت‌های شناسایی", "کنترل تردد"),
        ("cards.manage", "صدور و مدیریت کارت‌های شناسایی (تخصیص/عودت/مفقودی)", "کنترل تردد"),
        // فاز ۷۵ — کلیدهای ریز کارت (والد: cards.manage)
        ("cards.issue", "[ریز] صدور و تخصیص کارت به پرسنل", "کنترل تردد — ریز"),
        ("cards.state", "[ریز] عودت/مفقودی/ابطال و حذف کارت", "کنترل تردد — ریز"),
        ("visitors.view", "مشاهده ارباب رجوع", "کنترل تردد"),
        ("visitors.manage", "مدیریت مهمانان و کارت‌ها", "کنترل تردد"),
        // فاز ۷۵ — کلیدهای ریز ارباب‌رجوع (والد: visitors.manage)
        ("visitors.register", "[ریز] ثبت و تأیید مهمان/پیمانکار", "کنترل تردد — ریز"),
        ("visitors.flow", "[ریز] صدور کارت مهمان و ثبت ورود/خروج", "کنترل تردد — ریز"),

        ("parking.view", "مشاهده کنترل پارکینگ", "کنترل پارکینگ"),
        ("parking.manage", "مدیریت پارکینگ‌ها، گیت‌ها، خودروها و مجوزها", "کنترل پارکینگ"),
        ("parking.control", "ثبت ورود و خروج خودرو و کنترل راهبند", "کنترل پارکینگ"),
        // فاز ۷۵ — کلیدهای ریز پارکینگ (والد: parking.manage / parking.control)
        ("parking.lots.manage", "[ریز] پارکینگ‌ها و گیت‌های پارکینگ", "کنترل پارکینگ — ریز"),
        ("parking.vehicles.manage", "[ریز] خودروها و مجوزهای پارکینگ", "کنترل پارکینگ — ریز"),
        ("parking.cameras.manage", "[ریز] دوربین‌های پلاک‌خوان (ANPR)", "کنترل پارکینگ — ریز"),
        ("parking.barrier", "[ریز] فرمان راهبند و وضعیت گیت", "کنترل پارکینگ — ریز"),
        ("parking.scan", "[ریز] ثبت دستی ورود/خروج خودرو", "کنترل پارکینگ — ریز"),

        ("devices.view", "مشاهده دستگاه‌ها", "دستگاه‌های تردد"),
        ("devices.control", "کنترل دستگاه‌ها (بازکردن درب، زمان، ری‌استارت)", "دستگاه‌های تردد"),
        ("devices.manage", "مدیریت دستگاه‌ها و انتقال کاربران", "دستگاه‌های تردد"),
        // فاز ۷۵ — کلیدهای ریز دستگاه/بایومتریک (والد: devices.manage / biometrics.manage)
        ("devices.register", "[ریز] تعریف، ویرایش و فعال/غیرفعال‌سازی دستگاه", "دستگاه‌های تردد — ریز"),
        ("devices.transfer", "[ریز] انتقال کاربران بین دستگاه‌ها", "دستگاه‌های تردد — ریز"),
        ("devices.simulate", "ابزارهای شبیه‌سازی تردد و دستگاه‌ها (فاز ۷۴)", "دستگاه‌های تردد"),
        ("devices.poll", "خواندن تردد از دستگاه‌ها (سرویس پل)", "دستگاه‌های تردد"),
        ("gateway.view", "مشاهده تنظیمات گیت‌وی C#", "دستگاه‌های تردد"),
        ("gateway.edit", "ویرایش تنظیمات گیت‌وی C#", "دستگاه‌های تردد"),
        ("biometrics.view", "مشاهده داده‌های هویتی و بایومتریک", "دستگاه‌های تردد"),
        ("biometrics.manage", "دریافت و انتقال داده‌های هویتی بین دستگاه‌ها", "دستگاه‌های تردد"),
        ("biometrics.fetch", "[ریز] دریافت و مدیریت دادهٔ هویتی دستگاه", "دستگاه‌های تردد — ریز"),
        ("biometrics.transfer", "[ریز] ارسال و انتقال قالب‌ها بین دستگاه‌ها", "دستگاه‌های تردد — ریز"),

        ("payroll.view", "مشاهده حقوق و دستمزد", "حقوق و دستمزد"),
        ("payroll.manage", "ایجاد و نهایی‌سازی لیست حقوق", "حقوق و دستمزد"),
        ("payroll.items.edit", "ایجاد و ویرایش اقلام فرمول‌ساز حقوق (فاز ۷۴)", "حقوق و دستمزد"),
        ("payroll.params.edit", "ایجاد و ویرایش پارامترهای سالانه حقوق (فاز ۷۴)", "حقوق و دستمزد"),
        ("payroll.pdf", "چاپ و خروجی PDF لیست حقوق و فیش‌ها (فاز ۷۴)", "حقوق و دستمزد"),
        // فاز ۷۴ — کلیدهای ریز حقوق (با داشتن کلید درشتِ والد payroll.manage هم پاس می‌شوند)
        ("payroll.runs.create", "[ریز] ایجاد لیست حقوق", "حقوق و دستمزد — ریز"),
        ("payroll.runs.finalize", "[ریز] نهایی‌سازی لیست حقوق", "حقوق و دستمزد — ریز"),
        ("payroll.runs.delete", "[ریز] حذف لیست حقوق", "حقوق و دستمزد — ریز"),
        ("payroll.runs.export", "[ریز] خروجی اکسل/PDF لیست حقوق", "حقوق و دستمزد — ریز"),
        ("payroll.items.manage", "[ریز] فرمول‌ساز اقلام حقوق (ایجاد/ویرایش/حذف)", "حقوق و دستمزد — ریز"),
        ("payroll.params.manage", "[ریز] پارامترهای سالانهٔ حقوق", "حقوق و دستمزد — ریز"),
        ("payroll.bonus", "عیدی و پایه سنوات سالانه و تسویه حساب", "حقوق و دستمزد"),
        ("reports.export", "خروجی اکسل و گزارش‌ها", "حقوق و دستمزد"),
        ("reports.view", "گزارش‌ساز و داشبورد تحلیلی", "حقوق و دستمزد"),

        ("users.view", "مشاهده کاربران و نقش‌ها", "مدیریت سیستم"),
        ("users.create", "ایجاد کاربر جدید", "مدیریت سیستم"),
        ("users.manage", "مدیریت کاربران، نقش‌ها و بازنشانی رمز", "مدیریت سیستم"),
        ("companies.create", "[ریز] ایجاد شرکت", "مدیریت سیستم — ریز"),
        ("companies.edit", "[ریز] ویرایش و فعال/غیرفعال‌سازی شرکت", "مدیریت سیستم — ریز"),
        ("companies.delete", "[ریز] حذف شرکت", "مدیریت سیستم — ریز"),
        ("roles.manage", "ایجاد و مدیریت نقش‌ها و محدودیت رکوردی (فاز ۷۴)", "مدیریت سیستم"),
        // فاز ۷۴ — نقش‌ساز (والد roles.manage = users.manage) + فاز ۷۵ — کلیدهای ریز نقش
        ("roles.view", "[ریز] مشاهده نقش‌ها و دسترسی‌ها", "مدیریت سیستم — ریز"),
        ("roles.create", "[ریز] ایجاد نقش جدید", "مدیریت سیستم — ریز"),
        ("roles.edit", "[ریز] ویرایش نقش و ماتریس دسترسی", "مدیریت سیستم — ریز"),
        ("roles.delete", "[ریز] حذف نقش", "مدیریت سیستم — ریز"),
        // (توجه: roles.manage درشت بالاتر تعریف شد — کلید تکراری سیدر را می‌شکند)
        ("audit.view", "مشاهده گزارش ممیزی", "مدیریت سیستم"),

        ("notifications.view", "مشاهده اعلان‌ها", "مدیریت سیستم"),
        ("notifications.manage", "مدیریت قواعد و تنظیمات اعلان", "مدیریت سیستم"),

        // فاز ۹۳ — به‌روزرسانی بسته‌ای سامانه (والد: settings.manage — سازگاری نقش‌های قدیمی)
        ("updates.manage", "مدیریت به‌روزرسانی بسته‌ای سامانه و پشتیبان‌ها (آپلود/نصب/بازگردانی)", "مدیریت سیستم"),

        // فاز ۷۵ — کلیدهای ریز کاربران (والد: users.manage)
        ("users.edit", "[ریز] ویرایش کاربر و نقش‌های او", "مدیریت سیستم — ریز"),
        ("users.reset", "[ریز] بازنشانی رمز و بازکردن قفل کاربر", "مدیریت سیستم — ریز"),
        ("users.delete", "[ریز] حذف کاربر", "مدیریت سیستم — ریز"),

        ("settings.view", "مشاهده تنظیمات سامانه (شرکت/لوگو/پالت/نسخه)", "مدیریت سیستم"),
        ("settings.manage", "ویرایش اطلاعات شرکت، لوگو و پالت رنگ سامانه", "مدیریت سیستم"),

        ("self.view", "پرتال من — خانه و کارکرد من", "پرتال من"),
        ("self.requests", "ثبت درخواست‌های من (مرخصی/مأموریت/اصلاح تردد)", "پرتال من"),
        ("self.payslips", "فیش‌های حقوقی من", "پرتال من"),

        // ============================================================
        // فاز ۷۵ — RBAC ریز سراسری (ادغام دو جریان: کلیدهای درون‌بخشیِ بالا +
        // کلیدهای خالصِ جدید این بلاک). سازگاری صفر-مهاجرت: کلید درشتِ والد
        // (RbacService.PermParents) هم پاس می‌شود.
        // ============================================================
        ("shifts.create", "[ریز] ایجاد شیفت", "حضور و غیاب — ریز"),
        ("shifts.edit", "[ریز] ویرایش شیفت", "حضور و غیاب — ریز"),
        ("shifts.delete", "[ریز] حذف شیفت", "حضور و غیاب — ریز"),
        ("holidays.create", "[ریز] ثبت تعطیلی", "حضور و غیاب — ریز"),
        ("holidays.delete", "[ریز] حذف تعطیلی", "حضور و غیاب — ریز"),
        ("attendance.requests.create", "[ریز] ثبت درخواست مرخصی/مأموریت/مجوز", "حضور و غیاب — ریز"),
        ("attendance.leaves.approve", "[ریز] تأیید/رد مرخصی", "حضور و غیاب — ریز"),
        ("attendance.missions.approve", "[ریز] تأیید/رد مأموریت", "حضور و غیاب — ریز"),
        ("attendance.permits.approve", "[ریز] تأیید/رد مجوز خروج ساعتی", "حضور و غیاب — ریز"),
        ("access.groups.manage", "[ریز] گروه‌های تردد، عضویت و گیتِ گروه", "کنترل تردد — ریز"),
        ("access.map.manage", "[ریز] ذخیرهٔ چیدمان نقشهٔ مناطق", "کنترل تردد — ریز"),
        ("devices.commands", "[ریز] فرمان‌های دستگاه (درب/زمان/ری‌استارت/پاک‌سازی آفلاین)", "دستگاه‌های تردد — ریز"),
        ("payroll.bonus.compute", "[ریز] محاسبه/نهایی‌سازی/حذف پیش‌نویس عیدی و سنوات", "حقوق و دستمزد — ریز"),
        ("payroll.bonus.export", "[ریز] خروجی عیدی و سنوات", "حقوق و دستمزد — ریز"),
        ("payroll.settlement", "[ریز] محاسبه/نهایی‌سازی/حذف تسویه‌حساب", "حقوق و دستمزد — ریز"),
        ("payroll.settings", "[ریز] تنظیمات محاسبهٔ حقوق (روش/منبع/گردها)", "حقوق و دستمزد — ریز"),
    };

    public static readonly (string Name, string Title, string Desc, string[] Perms)[] RolesCatalog =
    {
        ("superadmin", "مدیر ارشد سیستم", "دسترسی کامل به همه ماژول‌ها و مدیریت کاربران", new[] { "*" }),
        ("hr_manager", "مدیر منابع انسانی", "پرسنل، قراردادها، حضور و غیاب، درخواست‌ها، حقوق و دستمزد",
            new[] { "dashboard.view", "companies.view", "employees.view", "employees.manage", "employees.import", "org.view", "org.manage",
                    "attendance.view", "attendance.manage", "attendance.approve",
                    "shifts.view", "shifts.manage",
                    "corrections.view", "corrections.approve", "holidays.view", "holidays.manage",
                    "payroll.view", "payroll.manage", "payroll.items.edit", "payroll.params.edit", "payroll.pdf", "payroll.bonus", "reports.export", "reports.view", "users.view", "roles.manage", "biometrics.view",
                    "notifications.view", "notifications.manage", "updates.manage", "cards.view", "parking.view" }),
        ("att_operator", "اپراتور تردد", "ثبت تردد، عملیات دستگاه‌ها و مانیتورینگ",
            new[] { "dashboard.view", "employees.view", "attendance.view", "attendance.manage",
                    "shifts.view",
                    "corrections.view", "holidays.view", "devices.view", "devices.control", "devices.manage", "devices.simulate",
                    "gateway.view", "gateway.edit",
                    "biometrics.view", "biometrics.manage",
                    "access.view", "cards.view", "cards.manage", "reports.export", "reports.view",
                    "parking.view", "parking.control", "parking.manage" }),
        ("security", "حراست و نگهبانی", "کنترل تردد، ارباب رجوع و هشدارهای امنیتی",
            new[] { "dashboard.view", "employees.view", "access.view", "access.manage",
                    "cards.view", "cards.manage",
                    "visitors.view", "visitors.manage", "devices.view", "devices.control", "biometrics.view",
                    "parking.view", "parking.control", "parking.manage" }),
        ("device_agent", "سرویس پل دستگاه‌ها", "حساب سرویس ویندوزی برای اتصال دستگاه‌ها (بدون ورود تعاملی)",
            new[] { "devices.view", "devices.control", "devices.manage", "attendance.manage", "devices.poll",
                    "biometrics.view", "biometrics.manage", "parking.control" }),
        ("viewer", "مشاهده‌گر", "دسترسی فقط‌خواندنی به ماژول‌های عملیاتی",
            new[] { "dashboard.view", "companies.view", "employees.view", "org.view", "attendance.view",
                    "shifts.view",
                    "access.view", "cards.view", "visitors.view", "devices.view", "payroll.view", "payroll.pdf",
                    "corrections.view", "holidays.view", "biometrics.view", "reports.view", "parking.view" }),
        ("employee", "پرسنل", "پرتال شخصی کارکنان — کارکرد، درخواست‌ها و فیش حقوقی خود",
            new[] { "self.view", "self.requests", "self.payslips" }),
    };

    /// <summary>
    /// RBAC (کاتالوگ دسترسی‌ها + نقش‌ها) — idempotent؛ کاربران در EnsureDefaultUsersAsync.
    /// فاز ۷۴ — هم‌ترازی نقش‌ها «افزودنی» شد: دسترسی‌های حذف‌شده توسط ادمین در بوت بعدی
    /// برنگردانده می‌شوند (تنها کلیدهای تازهٔ کاتالوگ به نقش‌های داخلی اضافه می‌شوند)؛
    /// سطح محدودیت رکوردی (DataScope) نقش‌های موجود هم بازنویسی نمی‌شود.
    /// </summary>
    public static async Task EnsureRbacAsync(AppDbContext db)
    {
        // کاتالوگ دسترسی‌ها — فاز ۷۴: مقاوم به کلید تکراری در کاتالوگ (FirstOrDefault در DB
        // ردیف‌های Add‌شدهٔ همان بوت را نمی‌بیند → Add دوباره = UNIQUE constraint)؛
        // ردیابی محلی + به‌روزرسانی به‌جای Add تکراری
        var tracked = new Dictionary<string, Permission>();
        foreach (var (key, title, grp) in PermissionsCatalog)
        {
            if (!tracked.TryGetValue(key, out var p))
            {
                p = await db.Permissions.FirstOrDefaultAsync(x => x.Key == key);
                if (p is null)
                {
                    p = new Permission { Key = key };
                    db.Permissions.Add(p);
                }
                tracked[key] = p;
            }
            p.Title = title;
            p.Grp = grp;
        }
        await db.SaveChangesAsync();

        var allPerms = await db.Permissions.ToListAsync();
        var permIdByKey = allPerms.ToDictionary(p => p.Key, p => p.Id);

        // نقش‌ها + اتصال دسترسی‌ها (افزودنی — فاز ۷۴)
        foreach (var (name, title, desc, perms) in RolesCatalog)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == name);
            if (role is null)
            {
                role = new Role { Name = name, Title = title, Description = desc, IsBuiltIn = true };
                db.Roles.Add(role);
                await db.SaveChangesAsync();
            }
            else
            {
                role.Title = title; role.Description = desc; role.IsBuiltIn = true;
                await db.SaveChangesAsync();
            }

            var existingIds = await db.RolePermissions.Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId).ToListAsync();
            var have = existingIds.ToHashSet();
            var keys = perms.Contains("*") ? permIdByKey.Keys.ToArray() : perms;
            foreach (var k in keys)
                if (permIdByKey.TryGetValue(k, out var pid) && !have.Contains(pid))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// کاربران پیش‌فرض — idempotent. پیوند پرسنل (admin→کامران رستمی و…) از قرارداد
    /// IPeopleDirectory می‌آید (پرسنل ∈ ماژول org؛ غایب = کاربر بدون پیوند — سامانه سالم).
    /// بعد از سید ماژول‌ها اجرا می‌شود تا پرسنل نمونه حاضر باشد.
    /// </summary>
    public static async Task EnsureDefaultUsersAsync(AppDbContext db, IPeopleDirectory people)
    {
        var users = new (string Username, string Password, string FullName, string[] Roles, bool Sa, string? EmployeeName)[]
        {
            ("admin", "Admin@1234", "مدیر ارشد سیستم", new[] { "superadmin" }, true, "کامران رستمی"),
            ("hr", "Hr@123456", "مریم تهرانی", new[] { "hr_manager" }, false, "مریم تهرانی"),
            ("operator", "Oper@1234", "رضا کریمی", new[] { "att_operator" }, false, "رضا کریمی"),
            ("guard", "Guard@1234", "حسین بهرامی", new[] { "security" }, false, "حسین بهرامی"),
            ("viewer", "View@1234", "کارشناس مشاهده‌گر", new[] { "viewer" }, false, null),
            ("svc_bridge", "Bridge@1234", "سرویس پل دستگاه‌ها", new[] { "device_agent" }, false, null),
        };
        var roleIdByName = (await db.Roles.ToListAsync()).ToDictionary(r => r.Name, r => r.Id);
        foreach (var u in users)
        {
            if (await db.Users.AnyAsync(x => x.Username == u.Username)) continue;
            string? employeeId = null;
            if (u.EmployeeName is not null)
            {
                var parts = u.EmployeeName.Split(' ');
                var fn = parts[0];
                var ln = string.Join(' ', parts.Skip(1));
                employeeId = await people.FindIdByNameAsync(fn, ln);
            }
            var hash = PasswordService.Hash(u.Password);
            var user = new User
            {
                Username = u.Username, PasswordHash = hash, FullName = u.FullName,
                IsSuperadmin = u.Sa, EmployeeId = employeeId,
            };
            user.Roles = u.Roles.Where(roleIdByName.ContainsKey)
                .Select(n => new UserRole { RoleId = roleIdByName[n] }).ToList();
            user.PasswordHistory.Add(new PasswordHistory { PasswordHash = hash });
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>دادهٔ نمایشی هسته + زنجیرهٔ سید ماژول‌ها — فقط اگر هیچ شرکتی نباشد (نصب تازه).</summary>
    public static async Task EnsureSeedAsync(AppDbContext db)
    {
        _rand = Mulberry32(1404);
        if (await db.Companies.AnyAsync()) return;

        // ---------- شرکت نمونه (هسته) ----------
        var company = new Company { Name = "شرکت پتروشیمی پارس آسیا", Code = "PAC-01" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        // ---------- زنجیرهٔ سید ماژول‌های حاضر ----------
        // PreSeed (شیفت‌ها ∈ حضور) → Seed به ترتیب SeedOrder:
        //   org=10 (چارت/پرسنل/قرارداد/احکام/تعطیلات) ← devices=20 (دستگاه‌ها/هویتی)
        //   ← attendance/access/visitors/parking/payroll (با ctx.Employees/DeviceIds پرشده)
        var ctx = new HrSeedContext(null, db, true, null, null, () => _rand(), company.Id);
        await Platform.Contracts.HrModuleRuntime.SeedModulesAsync(ctx, preSeed: true);
        await Platform.Contracts.HrModuleRuntime.SeedModulesAsync(ctx, preSeed: false);

        // ---------- هشدارهای امنیتی متنی (هسته) ----------
        db.SecurityAlerts.Add(new SecurityAlert { Type = "تأخیر خروج", Severity = "متوسط", Message = "مهمان «کوروش جهانگیری» بیش از ۸ ساعت داخل سایت است — بررسی خروج لازم است", Source = "ارباب رجوع" });
        db.SecurityAlerts.Add(new SecurityAlert { Type = "مجوز منقضی", Severity = "بالا", Message = "اعتبار قرارداد شرکت نگین تعمیرات صنعتی منقضی شده است (CTR-103)", Source = "ارباب رجوع" });
        db.SecurityAlerts.Add(new SecurityAlert { Type = "کارت گمشده", Severity = "متوسط", Message = "کارت شماره VC-48213 به‌عنوان مفقود اعلام شد — ابطال گردید", Source = "ارباب رجوع" });
        await db.SaveChangesAsync();
    }

    public static async Task ReseedAsync(AppDbContext db, IPeopleDirectory people)
    {
        _rand = Mulberry32(1404);
        // پاک‌سازی دادهٔ نمایشی همهٔ ماژول‌های حاضر (org/devices هم — با ترتیب الفباییِ
        // سازگار با FKهای آبشاری: attendance/access/devices/… قبل از org پاک می‌شوند)
        await Platform.Contracts.HrModuleRuntime.ClearDemoModulesAsync(db);
        db.SecurityAlerts.RemoveRange(await db.SecurityAlerts.ToListAsync());
        db.Companies.RemoveRange(await db.Companies.ToListAsync());
        await db.SaveChangesAsync();
        await EnsureSeedAsync(db);
        await EnsureDefaultUsersAsync(db, people);
    }

    // ===================== فاز ۳۱ — تنظیمات برندینگ/پالت =====================

    /// <summary>ردیف تنظیمات سامانه — خودترمیم: اگر وجود نداشت با مقادیر پیش‌فرض ساخته می‌شود.</summary>
    public static async Task EnsureSettingsSeedAsync(AppDbContext db)
    {
        if (await db.AppSettings.AnyAsync()) return;
        db.AppSettings.Add(new AppSettings
        {
            Id = "default",
            // فاز ۱۱۰ — ردیف نو از پیش علامت خورده: هرگز وارد ترمیم یک‌بارهٔ جبران قمری نمی‌شود
            HijriAdjustMigrated = true,
            CompanyName = "شرکت پتروشیمی پارس آسیا",
            Description = "حضور و غیاب · کنترل تردد · ارباب رجوع",
            UpdatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
    }

    // ===================== فاز ۳۲ — پالت‌های رنگ استاندارد =====================

    /// <summary>
    /// پالت‌های داخلی نرم‌افزار — پالت پیش‌فرض «نیلی درخشان» (فاز ۳۳، نیلی/بنفش مدرن) +
    /// گزینه‌های هم‌راستا با نرم‌افزارهای کنترل تردد (ZKTeco BioTime، Suprema BioStar، Hikvision).
    /// خودترمیم: پالت‌های داخلی در هر بوت با تعریف جدید هم‌تراز می‌شوند (پالت‌های سفارشی ادمین دست‌نخورده).
    /// </summary>
    public static readonly ColorPalette[] BuiltInPalettes =
    {
        new()
        {
            Name = "نیلی درخشان (پیش‌فرض)", IsBuiltIn = true, SortOrder = 1,
            PrimaryColor = "#6366f1", LinkColor = "#4f46e5",
            SidebarColor = "#1b1746", SidebarTextColor = "#c3c6ec",
            PageBgColor = "#f4f5fb", SurfaceColor = "#ffffff",
            TableHeaderColor = "#f2f3fc", RowAltColor = "#f9f9fe", RowHoverColor = "#f4f5fe",
            BorderColor = "#e3e5f3", TextColor = "#1d1e3a", MutedTextColor = "#565b7e",
            SuccessColor = "#059669", WarningColor = "#d97706", DangerColor = "#e11d48", InfoColor = "#0284c7",
        },
        new()
        {
            // سبک رایج نرم‌افزارهای تردد (BioTime/Hikvision): آبی سازمانی + منوی سرمه‌ای
            Name = "آبی سازمانی (سبک BioTime)", IsBuiltIn = true, SortOrder = 2,
            PrimaryColor = "#1668dc", LinkColor = "#1257b8",
            SidebarColor = "#14273f", SidebarTextColor = "#b8c7dd",
            PageBgColor = "#f2f6fc", SurfaceColor = "#ffffff",
            TableHeaderColor = "#eaf1fb", RowAltColor = "#f6f9fe", RowHoverColor = "#ecf3fd",
            BorderColor = "#dbe4f1", TextColor = "#17212f", MutedTextColor = "#4d5f75",
            SuccessColor = "#059669", WarningColor = "#d97706", DangerColor = "#e11d48", InfoColor = "#0284c7",
        },
        new()
        {
            // سبک BioStar (Suprema): سرمه‌ای عمیق + آبی روشن
            Name = "سرمه‌ای امنیتی (سبک BioStar)", IsBuiltIn = true, SortOrder = 3,
            PrimaryColor = "#2f6fed", LinkColor = "#2556bd",
            SidebarColor = "#1b2436", SidebarTextColor = "#b6c1d6",
            PageBgColor = "#f1f4f9", SurfaceColor = "#ffffff",
            TableHeaderColor = "#ecf0f7", RowAltColor = "#f7f9fc", RowHoverColor = "#edf2fa",
            BorderColor = "#dae0ea", TextColor = "#1a2233", MutedTextColor = "#56637a",
            SuccessColor = "#059669", WarningColor = "#d97706", DangerColor = "#e11d48", InfoColor = "#0284c7",
        },
        new()
        {
            // سبز — هم‌راستا با چراغ سبز گیت‌ها و حالت «مجاز»
            Name = "سبز دسترسی", IsBuiltIn = true, SortOrder = 4,
            PrimaryColor = "#16a34a", LinkColor = "#15803d",
            SidebarColor = "#122a1c", SidebarTextColor = "#b3d2c0",
            PageBgColor = "#f2f8f4", SurfaceColor = "#ffffff",
            TableHeaderColor = "#ebf6ef", RowAltColor = "#f6fbf8", RowHoverColor = "#ecf7f1",
            BorderColor = "#d9e9e0", TextColor = "#14251c", MutedTextColor = "#4c6355",
            SuccessColor = "#059669", WarningColor = "#d97706", DangerColor = "#e11d48", InfoColor = "#0284c7",
        },
        new()
        {
            // سبک Hikvision: گرافیت خنثی صنعتی
            Name = "خاکستری صنعتی (سبک Hikvision)", IsBuiltIn = true, SortOrder = 5,
            PrimaryColor = "#40607a", LinkColor = "#3a566e",
            SidebarColor = "#232b33", SidebarTextColor = "#b5bfc9",
            PageBgColor = "#f3f5f7", SurfaceColor = "#ffffff",
            TableHeaderColor = "#eef1f4", RowAltColor = "#f8fafb", RowHoverColor = "#eff3f6",
            BorderColor = "#dce1e6", TextColor = "#1d2630", MutedTextColor = "#5a6a78",
            SuccessColor = "#059669", WarningColor = "#d97706", DangerColor = "#e11d48", InfoColor = "#0284c7",
        },
        new()
        {
            // قالب «پارسان» (فاز ۴۳) — برداشت مستقیم از داشبورد مرجع کاربر:
            // منوی سرمه‌ای تیره + اکسنت فیروزه‌ای + زمینه روشن آبی‌خاکستری + وضعیت‌های پرانرژی
            Name = "پارسان (قالب داشبورد مدرن)", IsBuiltIn = true, SortOrder = 6,
            PrimaryColor = "#14b8a6", LinkColor = "#0d9488",
            SidebarColor = "#17293a", SidebarTextColor = "#a9bccb",
            PageBgColor = "#eef2f6", SurfaceColor = "#ffffff",
            TableHeaderColor = "#eff4f7", RowAltColor = "#f9fbfc", RowHoverColor = "#eef6f5",
            BorderColor = "#e1e8ee", TextColor = "#1b2833", MutedTextColor = "#5e7181",
            SuccessColor = "#10b981", WarningColor = "#f59e0b", DangerColor = "#ef4444", InfoColor = "#3b82f6",
        },
    };

    /// <summary>پالت‌های داخلی — خودترمیم دوطرفه: پالت جاافتاده ساخته می‌شود و پالت داخلی موجود
    /// با تعریف جدید هم‌تراز می‌شود (ارتقای خودکار دیتابیس‌های موجود به پالت فاز ۳۳).
    /// پالت‌های سفارشی ادمین (IsBuiltIn=false) دست‌نخورده می‌مانند.
    /// هم‌ترازی بر اساس SortOrder است تا تغییر نام پالت‌ها ردیف تکراری نسازد.</summary>
    public static async Task EnsurePalettesSeedAsync(AppDbContext db)
    {
        var builtIns = await db.ColorPalettes.Where(p => p.IsBuiltIn).ToListAsync();
        var touched = false;
        foreach (var src in BuiltInPalettes)
        {
            var row = builtIns.FirstOrDefault(p => p.SortOrder == src.SortOrder)
                   ?? builtIns.FirstOrDefault(p => p.Name == src.Name);
            if (row is null)
            {
                db.ColorPalettes.Add(src);
                touched = true;
                continue;
            }
            // هم‌ترازی کامل با تعریف فعلی سورس — EF فقط فیلدهای تغییرکرده را به‌روزرسانی می‌کند
            row.Name = src.Name;
            row.PrimaryColor = src.PrimaryColor;
            row.LinkColor = src.LinkColor;
            row.SidebarColor = src.SidebarColor;
            row.SidebarTextColor = src.SidebarTextColor;
            row.PageBgColor = src.PageBgColor;
            row.SurfaceColor = src.SurfaceColor;
            row.TableHeaderColor = src.TableHeaderColor;
            row.RowAltColor = src.RowAltColor;
            row.RowHoverColor = src.RowHoverColor;
            row.BorderColor = src.BorderColor;
            row.TextColor = src.TextColor;
            row.MutedTextColor = src.MutedTextColor;
            row.SuccessColor = src.SuccessColor;
            row.WarningColor = src.WarningColor;
            row.DangerColor = src.DangerColor;
            row.InfoColor = src.InfoColor;
            row.SortOrder = src.SortOrder;
            touched = true;
        }
        if (touched) await db.SaveChangesAsync();

        // فاز ۴۳ — پالت «پارسان» پالت فعال پیش‌فرض می‌شود؛ فقط وقتی ادمین پالتی انتخاب نکرده باشد
        // (ActivePaletteId null) تا انتخاب دستی ادمین روی نصب‌های موجود برچسب نخورد.
        var parsan = await db.ColorPalettes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SortOrder == 6 && p.IsBuiltIn);
        if (parsan is not null)
        {
            var st = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == "default");
            if (st is not null && st.ActivePaletteId is null)
            {
                st.ActivePaletteId = parsan.Id;
                st.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
            }
        }
    }
}
