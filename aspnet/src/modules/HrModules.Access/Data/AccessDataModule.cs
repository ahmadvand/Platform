using Platform.Web.Domain;
using HrModules.Access.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Access.Data;

// ======================================================================
// فاز ۶۸ — پلاگین دیتای ماژول کنترل تردد (منتقل‌شده از هاست؛ رفتار همان)
// ======================================================================
public sealed class AccessDataModule : IHrDataModule
{
    public string Code => "access";

    // فاز ۶۹ — خانوادهٔ نوع‌های این ماژول (مالکیت اسکیما) — داده‌محور برای گارد Ignore
    public Type[] FamilyTypes => [typeof(Zone), typeof(Gate), typeof(AccessPermission), typeof(AccessEvent),
        typeof(Card), typeof(AccessGroup), typeof(AccessGroupMember), typeof(AccessGroupGate)];

    // فاز ۱۲۲ — اعلام خانواده‌های داده برای مرکز درون‌ریزی/برون‌ریزی
    public HrDataExchangeDecl[] DataExchange =>
    [
        new("Card", "کارت‌های شناسایی", "کنترل تردد", true, "CardNo"),
        new("Zone", "مناطق کنترل تردد", "کنترل تردد", true, "Name"),
        new("Gate", "گیت‌ها", "کنترل تردد", true, "Name"),
        new("AccessGroup", "گروه‌های تردد", "کنترل تردد", true, "Name"),
        new("AccessGroupMember", "اعضای گروه تردد", "کنترل تردد", false),
        new("AccessGroupGate", "گیت‌های گروه تردد", "کنترل تردد", false),
        new("AccessPermission", "مجوزهای تردد پرسنل", "کنترل تردد", false),
        new("AccessEvent", "رویدادهای کنترل تردد", "کنترل تردد", false),
    ];
    public string Title => "کنترل تردد";
    public string Icon => "🚪";
    public string Description => "مناطق و گیت‌ها، مجوزهای عبور، گروه‌های دسترسی و کارت‌های شناسایی";

    public void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb)
    {
        services.AddScoped<AccessDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new AccessDbContext(HrDbResolver.OptionsFor<AccessDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<IAccessGateway, AccessGateway>();
        services.AddScoped<IModuleSeeder, AccessSeeder>();
    }

    public void ComposeModel(ModelBuilder mb)
    {
        mb.Entity<Zone>().ToTable("Zones");
        mb.Entity<Gate>().ToTable("Gates");
        mb.Entity<AccessPermission>().ToTable("AccessPermissions");
        mb.Entity<AccessEvent>().ToTable("AccessEvents");
        mb.Entity<Card>().ToTable("Cards");
        mb.Entity<AccessGroup>().ToTable("AccessGroups");
        mb.Entity<AccessGroupMember>().ToTable("AccessGroupMembers");
        mb.Entity<AccessGroupGate>().ToTable("AccessGroupGates");
        mb.Entity<Zone>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<Gate>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<Card>().HasIndex(x => x.CardNo).IsUnique();
        mb.Entity<Gate>().HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Card>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Zone>().Property(x => x.Code).HasMaxLength(100);
        mb.Entity<Gate>().Property(x => x.Code).HasMaxLength(100);
    }

    // فاز ۶۸ — FK جدول‌های تردد → بازدیدکننده (بازدیدکننده ∈ ماژول ارباب‌رجوع)؛
    // فقط وقتی «هر دو» ماژول حاضرند (گذر دوم ComposeModules — Visitor آن‌وقت در مدل است).
    // بدون ماژول ارباب‌رجوع، VisitorId ستونی معمولی می‌ماند — بدون FK — و کد تردد مستقل کار می‌کند.
    public void ComposeCrossModule(ModelBuilder mb)
    {
        if (!HrModuleRuntime.Installed("visitors")) return;
        var model = (Microsoft.EntityFrameworkCore.Metadata.IMutableModel)mb.Model;
        var dep = model.FindEntityType(typeof(AccessPermission));
        var prin = model.FindEntityType("Platform.Modules.Visitors.Domain.Visitor");
        if (dep is null || prin is null) return;
        var prop = dep.FindProperty("VisitorId");
        if (prop is null) return;
        if (dep.GetForeignKeys().Any(f => f.PrincipalEntityType == prin && f.Properties.Contains(prop))) return;
        var pk = prin.FindPrimaryKey();
        if (pk is null) return;
        var fk = dep.AddForeignKey(prop, pk, prin);
        fk.DeleteBehavior = DeleteBehavior.Cascade;
    }

    // ======================================================================
    // فاز ۶۸ — سید نمایشی/پاک‌سازی/شمارش شرکت (پورت از DbSeeder/CompaniesController هاست)
    // ======================================================================

    /// <summary>Seed — مناطق/گیت‌ها/مجوزها/رویدادهای ۲ روز اخیر + هشدارهای امنیتی (پورت SeedAccessDemoAsync).</summary>
    public async Task SeedAsync(HrSeedContext ctx)
    {
        if (!ctx.DemoData) return;
        var db = ctx.Db;
        var today = DateTime.Now.Date;
        var empIds = ctx.Employees;
        var empNames = ctx.EmployeeNames;
        var devIds = ctx.DeviceIds;
        var dev1 = new { Id = devIds[0] };
        var dev2 = new { Id = devIds[1] };
        var dev3 = new { Id = devIds[2] };
        var dev4 = new { Id = devIds[3] };
        var dev6 = new { Id = devIds[5] };
        var zMain = new Zone { Name = "ورودی اصلی سایت", Code = "Z-MAIN", RiskLevel = "عادی", Description = "ورود و خروج کلیه پرسنل و مراجعین" };
        var zProd = new Zone { Name = "منطقه تولید", Code = "Z-PRD", RiskLevel = "حساس", Description = "واحدهای فرآیندی الفین و پلی‌اتیلن" };
        var zStore = new Zone { Name = "انبار محصول", Code = "Z-STR", RiskLevel = "حساس", Description = "انبار محصول نهایی و سکوی بارگیری" };
        var zOffice = new Zone { Name = "دفتر مرکزی", Code = "Z-OFC", RiskLevel = "عادی", Description = "ساختمان اداری" };
        var zChem = new Zone { Name = "انبار مواد شیمیایی", Code = "Z-CHM", RiskLevel = "فوق‌حساس", Description = "انبار کاتالیست و مواد شیمیایی — دسترسی محدود" };
        db.Set<Zone>().AddRange(zMain, zProd, zStore, zOffice, zChem);
        await db.SaveChangesAsync();

        var gMain = new Gate { Name = "گیت اصلی ورودی", Code = "G-01", ZoneId = zMain.Id, DeviceId = dev1.Id, Type = "ورودی‌گردان", Direction = "ورودی" };
        var gMainOut = new Gate { Name = "گیت اصلی خروجی", Code = "G-02", ZoneId = zMain.Id, DeviceId = dev1.Id, Type = "ورودی‌گردان", Direction = "خروجی" };
        var gEast = new Gate { Name = "گیت شرقی", Code = "G-03", ZoneId = zMain.Id, DeviceId = dev2.Id, Type = "راهبند", Direction = "دوطرفه" };
        var gOlefin = new Gate { Name = "درب واحد الفین", Code = "G-04", ZoneId = zProd.Id, DeviceId = dev6.Id, Type = "درب برقی", Direction = "دوطرفه" };
        var gPoly = new Gate { Name = "درب واحد پلی‌اتیلن", Code = "G-05", ZoneId = zProd.Id, DeviceId = dev6.Id, Type = "درب برقی", Direction = "دوطرفه" };
        var gStore = new Gate { Name = "گیت انبار محصول", Code = "G-06", ZoneId = zStore.Id, DeviceId = dev4.Id, Type = "گیت میله‌ای", Direction = "دوطرفه" };
        var gOffice = new Gate { Name = "درب برقی دفتر مرکزی", Code = "G-07", ZoneId = zOffice.Id, DeviceId = dev3.Id, Type = "درب برقی", Direction = "دوطرفه" };
        var gChem = new Gate { Name = "درب انبار مواد شیمیایی", Code = "G-08", ZoneId = zChem.Id, DeviceId = dev6.Id, Type = "درب برقی", Direction = "دوطرفه" };
        db.Set<Gate>().AddRange(gMain, gMainOut, gEast, gOlefin, gPoly, gStore, gOffice, gChem);
        await db.SaveChangesAsync();
        gStore.Status = "خراب";
        await db.SaveChangesAsync();

        const string daysWeek = "0,1,2,3,4";
        const string allWeek = "0,1,2,3,4,5,6";
        void MkPerm(int empIdx, Zone zone, string type, string days, string? tf, string? tu)
        {
            db.Set<AccessPermission>().Add(new AccessPermission
            {
                EmployeeId = empIds[empIdx].Id, ZoneId = zone.Id, Type = type, Days = days,
                TimeFrom = tf, TimeUntil = tu,
                ValidFrom = today.AddDays(-30),
                ValidUntil = type == "موقت" ? today.AddDays(ctx.Rint(5, 60)) : null,
                Status = "فعال",
            });
        }
        for (int i = 0; i < empIds.Count; i++) MkPerm(i, zMain, "دائم", allWeek, null, null);
        var officeSkip = new HashSet<int> { 4, 5, 6, 7, 9, 11, 12, 13, 14, 15, 16, 17, 22, 23 };
        for (int i = 0; i < empIds.Count; i++)
        {
            if (officeSkip.Contains(i)) continue;
            MkPerm(i, zOffice, "دائم", daysWeek, "07:00", "19:00");
        }
        MkPerm(9, zProd, "دائم", allWeek, "06:00", "16:00");
        MkPerm(10, zProd, "دائم", allWeek, "14:00", "23:59");
        foreach (var i in new[] { 11, 12, 13, 16, 17 }) MkPerm(i, zProd, "دائم", allWeek, null, null);
        foreach (var i in new[] { 14, 15 }) MkPerm(i, zProd, "دائم", allWeek, "06:30", "19:30");
        foreach (var i in new[] { 17, 22, 23 }) MkPerm(i, zStore, "دائم", daysWeek, "07:00", "17:00");
        MkPerm(22, zChem, "دائم", daysWeek, "08:00", "16:00");
        MkPerm(26, zChem, "دائم", daysWeek, "08:00", "16:00");
        MkPerm(27, zChem, "موقت", daysWeek, "09:00", "14:00");
        MkPerm(0, zProd, "دائم", allWeek, null, null);
        MkPerm(0, zChem, "دائم", allWeek, null, null);
        MkPerm(0, zStore, "دائم", allWeek, null, null);
        await db.SaveChangesAsync();

        // ---------- رویدادهای دسترسی ۲ روز اخیر + هشدارها ----------
        var gatesList = new[] { gMain, gMainOut, gEast, gOlefin, gPoly, gOffice, gChem };
        var methods = new[] { "کارت", "اثر انگشت", "چهره", "QR" };
        for (int off = -1; off <= 0; off++)
        {
            var day = today.AddDays(off);
            var baseHour = off == 0 ? DateTime.Now.Hour : 18;
            for (int k = 0; k < 55; k++)
            {
                var empIdx = ctx.Rint(0, empIds.Count - 1);
                var gate = ctx.Pick(gatesList);
                var dir = ctx.Rand() > 0.5 ? "ورود" : "خروج";
                var method = ctx.Pick(methods);
                var denied = ctx.Rand() < 0.07;
                var t = day.AddHours(Math.Max(1, ctx.Rint(1, Math.Max(2, baseHour))))
                    .AddMinutes(ctx.Rint(0, 59)).AddSeconds(ctx.Rint(0, 59));

                var result = "مجاز";
                string? reason = null;
                var employeeId = empIds[empIdx].Id;
                if (denied)
                {
                    if (ctx.Rand() < 0.5) { result = "غیرمجاز"; reason = "فاقد مجوز دسترسی به این منطقه"; }
                    else { result = "غیرمجاز"; reason = "خارج از ساعات مجاز"; }
                }
                var ev = new AccessEvent
                {
                    EmployeeId = employeeId, GateId = gate.Id, Direction = dir,
                    Method = method, Result = result, Reason = reason, Timestamp = t,
                };
                db.Set<AccessEvent>().Add(ev);
                if (result == "غیرمجاز" && off == 0)
                {
                    db.Set<SecurityAlert>().Add(new SecurityAlert
                    {
                        Type = "ورود غیرمجاز",
                        Severity = reason == "فاقد مجوز دسترسی به این منطقه" ? "بحرانی" : "بالا",
                        Message = $"تلاش {dir} غیرمجاز توسط {empNames.GetValueOrDefault(employeeId, "")} در {gate.Name} ({reason})",
                        Source = "کنترل تردد",
                        RefId = ev.Id,
                    });
                }
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>پاک‌سازی نمونه‌های کنترل تردد — پورت ClearAccessDemo.</summary>
    public Task ClearDemoAsync(DbContext db)
    {
        db.Set<AccessEvent>().RemoveRange(db.Set<AccessEvent>().ToList());
        db.Set<AccessPermission>().RemoveRange(db.Set<AccessPermission>().ToList());
        db.Set<Gate>().RemoveRange(db.Set<Gate>().ToList());
        db.Set<Zone>().RemoveRange(db.Set<Zone>().ToList());
        db.SaveChanges();
        return Task.CompletedTask;
    }

    /// <summary>شمارش مناطق به‌ازای شرکت — پورت ZoneCounts/ZoneCountFor.</summary>
    public Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string kind) =>
        kind != "zones"
            ? Task.FromResult(new Dictionary<string, int>())
            : db.Set<Zone>().Where(z => z.CompanyId != null).GroupBy(z => z.CompanyId!)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

    public Task<int> CompanyCountAsync(DbContext db, string kind, string companyId) =>
        kind != "zones" ? Task.FromResult(0)
            : db.Set<Zone>().CountAsync(z => z.CompanyId == companyId);
}
