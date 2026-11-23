using Platform.Web.Domain;
using Platform.Web.Data;
using HrModules.Access.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Access.Data;

/// <summary>کانتکست دیتای ماژول کنترل تردد (فاز ۶۱) — جدول‌های خود ماژول + پایه‌های navigationدار.</summary>
public class AccessDbContext : DbContext
{
    public AccessDbContext(DbContextOptions<AccessDbContext> options) : base(options) { }

    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Gate> Gates => Set<Gate>();
    public DbSet<AccessPermission> AccessPermissions => Set<AccessPermission>();
    public DbSet<AccessEvent> AccessEvents => Set<AccessEvent>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<AccessGroup> AccessGroups => Set<AccessGroup>();
    public DbSet<AccessGroupMember> AccessGroupMembers => Set<AccessGroupMember>();
    public DbSet<AccessGroupGate> AccessGroupGates => Set<AccessGroupGate>();
    // دادهٔ پایه (همان جدول‌ها — مالک اسکیما: هاست)
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Zone>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<Gate>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<Card>().HasIndex(x => x.CardNo).IsUnique();
        mb.Entity<Gate>().HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Card>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);

        HrModelDefaults.Normalize(mb);
        mb.Entity<Zone>().Property(x => x.Code).HasMaxLength(100);
        mb.Entity<Gate>().Property(x => x.Code).HasMaxLength(100);
    }
}

/// <summary>
/// پیاده‌سازی قرارداد IAccessGateway — مصرف ماژول‌های دیگر (پارکینگ/بازدیدکننده).
/// منطق مجوز بازدیدکننده عیناً از VisitorsController قبلی منتقل شده (صفر تغییر رفتار).
/// </summary>
public class AccessGateway(AccessDbContext db) : IAccessGateway
{
    public async Task<List<GateBriefDto>> GatesAsync(string? companyId = null)
    {
        var q = db.Gates.AsNoTracking().Include(g => g.Zone).AsQueryable();
        if (!string.IsNullOrEmpty(companyId)) q = q.Where(g => g.CompanyId == companyId);
        return await q.OrderBy(g => g.Name)
            .Select(g => new GateBriefDto(g.Id, g.Name, g.Zone != null ? g.Zone.Name : null, null))
            .ToListAsync();
    }

    public async Task<string?> FindZoneIdByCodeAsync(string code) =>
        await db.Zones.AsNoTracking()
            .Where(z => z.Code == code)
            .Select(z => z.Id)
            .FirstOrDefaultAsync();

    public async Task<bool> HasVisitorPermissionAsync(string visitorId, string zoneId) =>
        await db.AccessPermissions.AsNoTracking()
            .AnyAsync(p => p.VisitorId == visitorId && p.ZoneId == zoneId);

    public async Task GrantVisitorPermissionAsync(string visitorId, string zoneId, string type, DateTime? validUntil)
    {
        db.AccessPermissions.Add(new AccessPermission
        {
            VisitorId = visitorId, ZoneId = zoneId, Type = type,
            ValidFrom = DateTime.Now, ValidUntil = validUntil,
            Status = "فعال",
        });
        await db.SaveChangesAsync();
    }

    public async Task<int> ExpireVisitorPermissionsAsync(string visitorId)
    {
        var perms = await db.AccessPermissions.Where(p => p.VisitorId == visitorId).ToListAsync();
        foreach (var p in perms) { p.Status = "منقضی"; p.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
        return perms.Count;
    }

    // ==================== فاز ۶۸ — قلاب‌های جایگزین coreهای #if هاست ====================

    /// <summary>شمارش گیت‌های هر دستگاه (داشبورد تجهیزات هاست).</summary>
    public async Task<Dictionary<string, int>> GateCountsByDeviceAsync() =>
        await db.Gates.AsNoTracking()
            .Where(g => g.DeviceId != null)
            .GroupBy(g => g.DeviceId!)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

    /// <summary>KPI کنترل تردد داشبورد — رویدادهای امروز + توزیع منطقه‌ای + آخرین رویدادها.</summary>
    public async Task<HrAccessDashDto> DashboardAsync()
    {
        var todayStart = DateTime.Now.Date;
        var evs = db.AccessEvents.AsNoTracking().Where(e => e.Timestamp >= todayStart);
        var todayEvents = await evs.CountAsync();
        var zoneDist = await db.AccessEvents.AsNoTracking()
            .Where(e => e.Timestamp >= todayStart && e.Gate != null && e.Gate.Zone != null)
            .GroupBy(e => e.Gate!.Zone!.Name)
            .Select(g => new HrZoneCount(g.Key, g.Count()))
            .ToListAsync();
        var recent = await db.AccessEvents.AsNoTracking().OrderByDescending(e => e.Timestamp).Take(8)
            .Select(e => new HrAccessRecentEvent(
                e.Timestamp, e.Direction, e.Method, e.Result, e.Reason,
                e.Gate != null ? e.Gate.Name : "—",
                e.EmployeeId,
                e.VisitorId)).ToListAsync();
        return new HrAccessDashDto(todayEvents, zoneDist,
            recent.ToList(),
            recent.Select(r => (string?)r.VisitorId).ToList());
    }
}
