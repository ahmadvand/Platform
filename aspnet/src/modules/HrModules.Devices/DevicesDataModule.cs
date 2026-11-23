using Platform.Web.Domain;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Devices;

// ======================================================================
// فاز ۶۹ — پلاگین دیتای ماژول تجهیزات و گیت‌وی
// ----------------------------------------------------------------------
// مالکیت اسکیما/سید نمایشی/آبشار حذف شرکت/شمارش‌های خانوادهٔ تجهیزات با همین
// IHrDataModule است — ماژول غایب = نه جدول، نه سید، نه DI. قرارداد
// IDevicesGateway (داشبورد/جست‌وجو/شمارش/دستگاه‌های آفلاین) هم همین‌جا
// پیاده‌سازی می‌شود؛ هسته (داشبورد/اعلان‌ها) از قرارداد می‌خواند.
// PreSeed: ردیف ۶ دستگاه نمونه — پیش از سید حضور که به DeviceIds نیاز دارد.
// Seed: ثبت پرسنل روی دستگاه‌ها + قالب‌های هویتی (پورت از DbSeeder هاست).
// ======================================================================
public sealed class DevicesDataModule : IHrDataModule
{
    public string Code => "devices";

    // فاز ۶۹ — خانوادهٔ نوع‌های این ماژول (مالکیت اسکیما) — داده‌محور برای گارد Ignore
    public Type[] FamilyTypes => [typeof(Device), typeof(DeviceCommand), typeof(DeviceUser), typeof(DeviceBiometric),
        typeof(EmployeeBiometric), typeof(DeviceOfflinePunch), typeof(DevicePunchLog), typeof(GatewaySetting)];

    // فاز ۱۲۲ — اعلام خانواده‌های داده برای مرکز درون‌ریزی/برون‌ریزی
    public HrDataExchangeDecl[] DataExchange =>
    [
        new("Device", "دستگاه‌های تردد", "دستگاه‌ها و گیت‌وی", true, "Serial"),
        new("DeviceUser", "کاربران ثبت‌شده روی دستگاه‌ها", "دستگاه‌ها و گیت‌وی", false),
        new("DeviceBiometric", "قالب‌های هویتی دستگاه‌ها", "دستگاه‌ها و گیت‌وی", false),
        new("EmployeeBiometric", "قالب‌های بیومتریک پرسنل", "دستگاه‌ها و گیت‌وی", false),
        new("DeviceCommand", "دستورهای دستگاه", "دستگاه‌ها و گیت‌وی", false),
        new("DevicePunchLog", "لاگ تردد خام دستگاه‌ها", "دستگاه‌ها و گیت‌وی", false),
        new("DeviceOfflinePunch", "حافظهٔ آفلاین دستگاه‌ها", "دستگاه‌ها و گیت‌وی", false),
        new("GatewaySetting", "تنظیمات گیت‌وی", "دستگاه‌ها و گیت‌وی", false),
    ];
    public string Title => "تجهیزات و گیت‌وی";
    public string Icon => "🖥️";
    public string Description => "دستگاه‌های تردد، داده‌های هویتی/بیومتریک، تنظیمات گیت‌وی و وب‌هوک‌ها — تغذیه‌کنندهٔ حضور، کنترل تردد و پارکینگ";

    public void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb)
    {
        services.AddScoped<IDevicesGateway, DevicesGatewayDirectory>();
    }

    // ==================================================================
    // اسکیما — عیناً همان پیکربندی پایهٔ سابق (DDL بایت‌به‌بایت یکسان)
    // ==================================================================
    public void ComposeModel(ModelBuilder mb)
    {
        mb.Entity<Device>().ToTable("Devices");
        mb.Entity<DeviceCommand>().ToTable("DeviceCommands");
        mb.Entity<DeviceUser>().ToTable("DeviceUsers");
        mb.Entity<DeviceBiometric>().ToTable("DeviceBiometrics");
        mb.Entity<EmployeeBiometric>().ToTable("EmployeeBiometrics");
        mb.Entity<DeviceOfflinePunch>().ToTable("DeviceOfflinePunches");
        mb.Entity<DevicePunchLog>().ToTable("DevicePunchLogs");
        mb.Entity<GatewaySetting>().ToTable("GatewaySettings");

        // ایندکس‌ها — قرینهٔ مدل مرکب سابق
        mb.Entity<Device>().HasIndex(x => x.Serial).IsUnique();
        mb.Entity<Device>().HasIndex(x => x.WebhookToken).IsUnique();
        mb.Entity<DeviceUser>().HasIndex(x => new { x.DeviceId, x.Pin }).IsUnique();
        mb.Entity<DeviceUser>().HasIndex(x => x.Pin);
        mb.Entity<DeviceBiometric>().HasIndex(x => new { x.DeviceId, x.Pin, x.Type, x.BioIndex }).IsUnique();
        mb.Entity<EmployeeBiometric>().HasIndex(x => new { x.EmployeeId, x.Type, x.BioIndex }).IsUnique();
        // حافظه آفلاین دستگاه (فاز ۳۰)
        mb.Entity<DeviceOfflinePunch>().HasIndex(x => new { x.DeviceId, x.ImportedAt });
        // لاگ تردد خام (فاز ۴۳)
        mb.Entity<DevicePunchLog>().HasIndex(x => x.Timestamp);
        mb.Entity<DevicePunchLog>().HasIndex(x => new { x.DeviceId, x.Timestamp });

        // روابط درون‌خانواده‌ای — همان رفتار حذف DDL موجود
        mb.Entity<Device>().HasOne(d => d.Company).WithMany() // nav-typed — جلوگیری از FK شادو CompanyId1 (فاز ۶۹)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.ClientSetNull);
        mb.Entity<DeviceCommand>().HasOne(c => c.Device).WithMany(d => d.Commands) // nav-typed — فاز ۶۹
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<DeviceUser>().HasOne(x => x.Device).WithMany(d => d.Users)
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<DeviceBiometric>().HasOne(x => x.Device).WithMany(d => d.Biometrics)
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<EmployeeBiometric>().HasOne(x => x.SourceDevice).WithMany()
            .HasForeignKey(x => x.SourceDeviceId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<DevicePunchLog>().HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);

        // طول ستون‌ها — قرینهٔ پیکربندی پایهٔ سابق
        mb.Entity<DeviceUser>().Property(x => x.Pin).HasMaxLength(50);
        mb.Entity<DeviceBiometric>().Property(x => x.Pin).HasMaxLength(50);
        mb.Entity<DeviceBiometric>().Property(x => x.Type).HasMaxLength(50);
        mb.Entity<EmployeeBiometric>().Property(x => x.Type).HasMaxLength(50);
    }

    /// <summary>FKهای بین‌خانواده‌ای — گذر دوم. پرسنل ∈ ماژول org؛ غایب = ردِ خودِ قلاب.</summary>
    public void ComposeCrossModule(ModelBuilder mb)
    {
        if (mb.Model.FindEntityType(typeof(Employee)) is null) return;
        mb.Entity<DeviceUser>().HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<EmployeeBiometric>().HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<DevicePunchLog>().HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
    }

    // ==================================================================
    // سید نمایشی (پورت SeedAllAsync هاست — بخش تجهیزات)
    // ==================================================================

    /// <summary>PreSeed — شش دستگاه نمونه (ATT-01)؛ شناسه‌ها به بستر سید سپرده می‌شود
    /// (سید حضور به DeviceIds نیاز دارد؛ devices از نظر الفبایی پیش از org می‌آید).</summary>
    public Task PreSeedAsync(HrSeedContext ctx)
    {
        if (!ctx.DemoData) return Task.CompletedTask;
        var db = ctx.Db;
        Device MkDev(string name, string kind, string brand, string serial, string ip, string location, string purpose, string mode, int? minsAgo)
        {
            var d = new Device
            {
                Name = name, Kind = kind, Brand = brand, Serial = serial, Ip = ip, Location = location,
                Purpose = purpose, Mode = mode,
                LastSync = minsAgo is null ? null : DateTime.Now.AddMinutes(-minsAgo.Value),
                LastSeenAt = minsAgo is null ? null : DateTime.Now.AddMinutes(-minsAgo.Value),
            };
            db.Set<Device>().Add(d);
            return d;
        }
        var dev1 = MkDev("ترمینال ورودی اصلی", "ترمینال اثر انگشت", "ZKTeco", "ZK-U2000-0142", "192.168.10.11", "ورودی اصلی سایت", "حضور و غیاب", "آنلاین", 4);
        var dev2 = MkDev("تشخیص چهره گیت شرقی", "ترمینال تشخیص چهره", "ZKTeco", "ZK-FD7-0311", "192.168.10.12", "گیت شرقی", "کنترل تردد", "آنلاین", 7);
        var dev3 = MkDev("دستگاه دفتر مرکزی", "ترمینال ترکیبی چهره و اثر انگشت", "Suprema", "SP-BS3-0907", "192.168.10.13", "دفتر مرکزی", "حضور و غیاب", "آنلاین", 2);
        var dev4 = MkDev("ترمینال انبار محصول", "ترمینال ترکیبی چهره و اثر انگشت", "Anviz", "AV-W1-0455", "192.168.10.14", "انبار محصول", "کنترل تردد", "آفلاین", 2880);
        var dev5 = MkDev("ترمینال کارگاه فنی", "ترمینال اثر انگشت", "Timmy", "TM-A5-0773", "192.168.10.15", "کارگاه فنی", "حضور و غیاب", "آنلاین", 15);
        var dev6 = MkDev("کنترلر منطقه حساس", "کنترلر دسترسی", "Virdi", "VR-AC7-0221", "192.168.10.16", "انبار مواد شیمیایی", "کنترل تردد", "آنلاین", 9);
        db.SaveChanges();
        ctx.DeviceIds = new[] { dev1.Id, dev2.Id, dev3.Id, dev4.Id, dev5.Id, dev6.Id };
        return Task.CompletedTask;
    }

    /// <summary>Seed — ثبت پرسنل روی دستگاه‌ها + قالب‌های اثر انگشت/چهره/کارت/رمز
    /// + کپی مرجع (EmployeeBiometric) برای انتقال بین دستگاه‌ها.</summary>
    public async Task SeedAsync(HrSeedContext ctx)
    {
        if (!ctx.DemoData) return;
        var db = ctx.Db;
        var devs = await db.Set<Device>().OrderBy(d => d.Serial).ToListAsync();
        if (devs.Count < 6) return; // دموی ثبت‌نام به شش دستگاه نمونه تکیه دارد
        var (dev1, dev2, dev3, dev4, dev5, dev6) = (devs[0], devs[1], devs[2], devs[3], devs[4], devs[5]);
        var allEmps = await db.Set<Employee>().OrderBy(e => e.PersonnelCode).ToListAsync();
        if (allEmps.Count == 0) return; // پرسنل ∈ ماژول org — غایب = دموی ثبت‌نام بی‌معنا

        var canonKeys = new HashSet<(string EmpId, string Type, int Idx)>();
        var uidCounters = new Dictionary<string, int>();
        int NextUid(string deviceId) => uidCounters.TryGetValue(deviceId, out var n) ? n + 1 : uidCounters[deviceId] = 1;

        void Enroll(Device dev, Employee emp, bool fp0, bool fp1, bool face, bool withCard, bool withPin)
        {
            var pin = emp.PersonnelCode;
            var fetched = DateTime.Now.AddHours(-ctx.Rint(1, 72));
            var card = withCard ? BioSync.SimCardNo(pin) : null;
            db.Set<DeviceUser>().Add(new DeviceUser
            {
                DeviceId = dev.Id, Uid = NextUid(dev.Id), Pin = pin,
                Name = emp.FullName, Privilege = 0,
                CardNumber = card, Password = withPin ? "1234" : null,
                GroupId = "1", EmployeeId = emp.Id, LastFetchedAt = fetched,
            });
            void AddBio(string type, int idx, int? q, string? data, int len)
            {
                if (data is null) return;
                db.Set<DeviceBiometric>().Add(new DeviceBiometric
                {
                    DeviceId = dev.Id, Pin = pin, Type = type, BioIndex = idx,
                    Valid = 1, Quality = q, TemplateData = data, Length = len, FetchedAt = fetched,
                });
                if (canonKeys.Add((emp.Id, type, idx)))
                {
                    db.Set<EmployeeBiometric>().Add(new EmployeeBiometric
                    {
                        EmployeeId = emp.Id, Type = type, BioIndex = idx,
                        Valid = 1, Quality = q, TemplateData = data, Length = len,
                        SourceDeviceId = dev.Id, CardNumber = type == "card" ? data : null,
                        EnrolledAt = DateTime.Now.AddDays(-ctx.Rint(30, 400)),
                    });
                }
            }
            if (fp0) { var (d, l) = BioSync.FakeTemplate($"fp:{pin}:0"); AddBio("fp", 0, ctx.Rint(62, 99), d, l); }
            if (fp1) { var (d, l) = BioSync.FakeTemplate($"fp:{pin}:1"); AddBio("fp", 1, ctx.Rint(58, 99), d, l); }
            if (face) { var (d, l) = BioSync.FakeTemplate($"face:{pin}:0"); AddBio("face", 0, ctx.Rint(55, 98), d, l); }
            if (withCard) AddBio("card", 0, null, card, card?.Length ?? 0);
            if (withPin) AddBio("pin", 0, null, "1234", 4);
        }

        // dev1 (ورودی اصلی): ۲۲ نفر — دو اثر انگشت + کارت + رمز (کپی مرجع از اینجا)
        for (int i = 0; i < Math.Min(22, allEmps.Count); i++)
            Enroll(dev1, allEmps[i], fp0: true, fp1: i % 3 != 0, face: false, withCard: true, withPin: i % 2 == 0);
        // dev2 (تشخیص چهره گیت شرقی): ۱۲ نفر — قالب چهره + کارت
        for (int i = 3; i < Math.Min(15, allEmps.Count); i++)
            Enroll(dev2, allEmps[i], fp0: false, fp1: false, face: true, withCard: true, withPin: false);
        // dev3 (دفتر مرکزی): ۸ نفر — یک اثر انگشت + کارت
        for (int i = 0; i < Math.Min(8, allEmps.Count); i++)
            Enroll(dev3, allEmps[i], fp0: true, fp1: false, face: false, withCard: true, withPin: false);
        // dev6 (کنترلر منطقه حساس): ۴ نفر — اثر انگشت + کارت + رمز
        foreach (var i in new[] { 9, 10, 22, 26 })
            if (i < allEmps.Count)
                Enroll(dev6, allEmps[i], fp0: true, fp1: false, face: false, withCard: true, withPin: true);
        // dev4 و dev5 عمداً خالی مانده‌اند — سناریوی «دریافت پرسنل از دستگاه» با شبیه‌ساز
        await db.SaveChangesAsync();

        // شمار کاربران هر دستگاه
        var devUserCounts = await db.Set<DeviceUser>().GroupBy(du => du.DeviceId).ToDictionaryAsync(g => g.Key, g => g.Count());
        foreach (var d in new[] { dev1, dev2, dev3, dev4, dev5, dev6 })
            d.UserCount = devUserCounts.GetValueOrDefault(d.Id, 0);
        await db.SaveChangesAsync();
    }

    /// <summary>پاک‌سازی دادهٔ نمایشی تجهیزات (Reseed) — ترتیب فرزند ← والد.
    /// (لاگ تردد خام/حافظهٔ آفلاین مثل قبل پاک نمی‌شوند — FK باملاحظه SetNull/Cascade می‌ماند.)</summary>
    public async Task ClearDemoAsync(DbContext db)
    {
        db.Set<DeviceBiometric>().RemoveRange(await db.Set<DeviceBiometric>().ToListAsync());
        db.Set<DeviceUser>().RemoveRange(await db.Set<DeviceUser>().ToListAsync());
        db.Set<EmployeeBiometric>().RemoveRange(await db.Set<EmployeeBiometric>().ToListAsync());
        db.Set<DeviceCommand>().RemoveRange(await db.Set<DeviceCommand>().ToListAsync());
        db.Set<Device>().RemoveRange(await db.Set<Device>().ToListAsync());
        await db.SaveChangesAsync();
    }

    /// <summary>آبشار حذف شرکت — تجهیزات/کاربران/قالب‌های متعلق به شرکت داده‌شده.</summary>
    public async Task OnCompanyDeleteAsync(DbContext db, string companyId)
    {
        var devIds = await db.Set<Device>().Where(x => x.CompanyId == companyId).Select(x => x.Id).ToListAsync();
        if (devIds.Count == 0) return;
        db.Set<DeviceBiometric>().RemoveRange(await db.Set<DeviceBiometric>().Where(x => devIds.Contains(x.DeviceId)).ToListAsync());
        db.Set<DeviceUser>().RemoveRange(await db.Set<DeviceUser>().Where(x => devIds.Contains(x.DeviceId)).ToListAsync());
        db.Set<DevicePunchLog>().RemoveRange(await db.Set<DevicePunchLog>().Where(x => x.DeviceId != null && devIds.Contains(x.DeviceId)).ToListAsync());
        db.Set<DeviceCommand>().RemoveRange(await db.Set<DeviceCommand>().Where(x => devIds.Contains(x.DeviceId)).ToListAsync());
        db.Set<DeviceOfflinePunch>().RemoveRange(await db.Set<DeviceOfflinePunch>().Where(x => devIds.Contains(x.DeviceId)).ToListAsync());
        db.Set<Device>().RemoveRange(await db.Set<Device>().Where(x => x.CompanyId == companyId).ToListAsync());
        await db.SaveChangesAsync();
    }

    /// <summary>شمارش‌های صفحهٔ شرکت‌ها — kind: devices.</summary>
    public Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string kind) =>
        kind != "devices"
            ? Task.FromResult(new Dictionary<string, int>())
            : db.Set<Device>().Where(d => d.CompanyId != null)
                .GroupBy(d => d.CompanyId!).ToDictionaryAsync(g => g.Key, g => g.Count());

    public Task<int> CompanyCountAsync(DbContext db, string kind, string companyId) =>
        kind != "devices" ? Task.FromResult(0)
            : db.Set<Device>().CountAsync(d => d.CompanyId == companyId);
}

// ======================================================================
// دروازهٔ تجهیزات — پیاده‌سازی IDevicesGateway (داشبورد/جست‌وجو/شمارش/آفلاین)
// ======================================================================
public sealed class DevicesGatewayDirectory(AppDbContext db) : IDevicesGateway
{
    public async Task<HrDevicesDashDto> DashboardAsync()
    {
        // sweep آفلاین‌ها — همان کاری که داشبورد هاست مستقیماً می‌کرد (روی مدل مرکب)
        await DeviceOps.SweepOfflineAsync(db);
        var devices = await db.Set<Device>().Include(d => d.Company).OrderBy(d => d.Name).ToListAsync();
        var lastPunchByDev = await db.Set<DevicePunchLog>()
            .Where(l => l.Timestamp >= DateTime.Now.Date)
            .OrderByDescending(l => l.Timestamp).Take(400).ToListAsync();
        // نام پرسنل آخرین تردد — پرسنل ∈ ماژول org؛ با Set<T> روی مدل مرکب (فقط وقتی در مدل است)
        var empNames = new Dictionary<string, string>();
        if (db.Model.FindEntityType(typeof(Employee)) is not null)
            empNames = await db.Set<Employee>().ToDictionaryAsync(e => e.Id, e => e.FullName);
        var lastPunchMap = new Dictionary<string, (string Person, string Time)>();
        var punchCountMap = new Dictionary<string, int>();
        foreach (var l in lastPunchByDev)
        {
            if (l.DeviceId is null) continue;
            lastPunchMap.TryAdd(l.DeviceId, (
                l.EmployeeId is not null && empNames.TryGetValue(l.EmployeeId, out var n) ? n : "ناشناس",
                Jalali.FmtTime(l.Timestamp)));
            punchCountMap[l.DeviceId] = punchCountMap.GetValueOrDefault(l.DeviceId) + 1;
        }
        var items = devices.Select(d => new HrDeviceStripItem(
            d.Id, d.Name, d.Kind, d.Brand, d.Ip, d.Location,
            d.Company?.Name, DeviceOps.IsOnline(d),
            punchCountMap.GetValueOrDefault(d.Id, 0),
            lastPunchMap.TryGetValue(d.Id, out var lp) ? lp.Person : null,
            lastPunchMap.TryGetValue(d.Id, out var lt) ? lt.Time : null)).ToList();
        return new HrDevicesDashDto(devices.Count, items.Count(i => i.Online), items);
    }

    public Task<List<HrSearchHit>> SearchAsync(string q, int limit) =>
        db.Set<Device>()
            .Where(d => d.Name.Contains(q) || (d.Ip != null && d.Ip.Contains(q)))
            .OrderBy(d => d.Name).Take(limit)
            .Select(d => new HrSearchHit(d.Name + " (" + (d.Ip ?? "—") + ")", "/Devices"))
            .ToListAsync();

    public Task<Dictionary<string, int>> CountsByCompanyAsync() =>
        db.Set<Device>().Where(d => d.CompanyId != null)
            .GroupBy(d => d.CompanyId!).ToDictionaryAsync(g => g.Key, g => g.Count());

    public Task<int> CountForCompanyAsync(string companyId) =>
        db.Set<Device>().CountAsync(d => d.CompanyId == companyId);

    public async Task<List<HrDeviceStatusRow>> OfflineDevicesAsync(int minutes)
    {
        var threshold = DateTime.Now.AddMinutes(-minutes);
        var rows = await db.Set<Device>()
            .Where(d => d.Active && d.LastSeenAt != null && d.LastSeenAt < threshold)
            .Select(d => new HrDeviceStatusRow(d.Name, d.Ip, d.LastSeenAt))
            .ToListAsync();
        return rows;
    }
}
