using Platform.Web.Domain;
using Platform.Web.Data;
using Platform.Modules.Visitors.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Platform.Modules.Visitors.Data;

/// <summary>کانتکست دیتای ماژول ارباب‌رجوع/بازدیدکننده (فاز ۶۱).</summary>
public class VisitorsDbContext : DbContext
{
    public VisitorsDbContext(DbContextOptions<VisitorsDbContext> options) : base(options) { }

    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<VisitorCard> VisitorCards => Set<VisitorCard>();
    public DbSet<VisitLog> VisitLogs => Set<VisitLog>();
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<VisitorCard>().HasIndex(x => x.CardNo).IsUnique();
        mb.Entity<VisitorCard>().HasIndex(x => x.VisitorId).IsUnique();

        HrModelDefaults.Normalize(mb);
    }
}

/// <summary>پیاده‌سازی قرارداد IVisitorsGateway — مصرف ماژول کنترل تردد (نمایش نام بازدیدکننده).</summary>
public class VisitorsGateway(VisitorsDbContext db) : IVisitorsGateway
{
    public async Task<Dictionary<string, string>> FullNamesAsync(IReadOnlyCollection<string> visitorIds)
    {
        var ids = visitorIds.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await db.Visitors.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new { v.Id, Name = v.FirstName + " " + v.LastName })
            .ToDictionaryAsync(x => x.Id, x => x.Name);
    }

    public async Task<List<VisitorBriefDto>> SearchAsync(string? query, int limit = 20)
    {
        var q = db.Visitors.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(v => v.FirstName.Contains(query) || v.LastName.Contains(query)
                || v.NationalId.Contains(query) || v.HostName.Contains(query));
        return await q.OrderBy(v => v.LastName).Take(limit)
            .Select(v => new VisitorBriefDto(v.Id, v.FirstName + " " + v.LastName, v.NationalId,
                v.Company != null ? v.Company.Name : null, v.Phone))
            .ToListAsync();
    }

    // فاز ۶۸ — تعداد بازدیدکنندگان «داخل سازمان» (داشبورد هاست) — پورت VisitorsInsideAsync هاست.
    public async Task<int> InsideCountAsync() =>
        await db.Visitors.AsNoTracking().CountAsync(v => v.Status == "داخل سازمان");
}
