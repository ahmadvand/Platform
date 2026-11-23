using Platform.Web.Domain;
using Platform.Web.Data;
using Platform.Modules.Parking.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Platform.Modules.Parking.Data;

/// <summary>کانتکست دیتای ماژول پارکینگ/پلاک‌خوان (فاز ۶۱) — از فاز ۵۷ کاملاً مستقل.</summary>
public class ParkingDbContext : DbContext
{
    public ParkingDbContext(DbContextOptions<ParkingDbContext> options) : base(options) { }

    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();
    public DbSet<ParkingGate> ParkingGates => Set<ParkingGate>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ParkingPermit> ParkingPermits => Set<ParkingPermit>();
    public DbSet<ParkingEvent> ParkingEvents => Set<ParkingEvent>();
    public DbSet<AnprCamera> AnprCameras => Set<AnprCamera>();
    public DbSet<AnprCommand> AnprCommands => Set<AnprCommand>();
    // دادهٔ پایه (دستگاه/پرسنل/شرکت) — همان جدول‌ها؛ مالک اسکیما: هاست
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<ParkingLot>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<ParkingGate>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<Vehicle>().HasIndex(x => new { x.CompanyId, x.PlateKey }).IsUnique();
        mb.Entity<ParkingPermit>().HasIndex(x => x.VehicleId);
        mb.Entity<ParkingEvent>().HasIndex(x => x.Timestamp);
        mb.Entity<ParkingEvent>().HasIndex(x => x.PlateKey);
        mb.Entity<AnprCamera>().HasIndex(x => x.Serial).IsUnique();
        mb.Entity<AnprCamera>().HasIndex(x => x.WebhookToken).IsUnique();
        mb.Entity<AnprCommand>().HasIndex(x => new { x.CameraId, x.Status });
        mb.Entity<ParkingGate>().HasOne(x => x.Lot).WithMany(l => l.Gates)
            .HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ParkingGate>().HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Vehicle>().HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ParkingPermit>().HasOne(x => x.Vehicle).WithMany(v => v.Permits)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ParkingPermit>().HasOne(x => x.Lot).WithMany(l => l.Permits)
            .HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Cascade);
        // فاز ۴۹ — GateId = NO ACTION (بدون دو مسیر آبشاری روی SQL Server)
        mb.Entity<ParkingEvent>().HasOne(x => x.Gate).WithMany(g => g.Events)
            .HasForeignKey(x => x.GateId).OnDelete(DeleteBehavior.NoAction);
        mb.Entity<ParkingEvent>().HasOne(x => x.Vehicle).WithMany(v => v.Events)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AnprCamera>().HasOne(x => x.Gate).WithMany()
            .HasForeignKey(x => x.GateId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ParkingEvent>().HasOne(x => x.Camera).WithMany(c => c.Events)
            .HasForeignKey(x => x.CameraId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AnprCommand>().HasOne(x => x.Camera).WithMany(c => c.Commands)
            .HasForeignKey(x => x.CameraId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ParkingLot>().Property(x => x.Code).HasMaxLength(100);
        mb.Entity<ParkingGate>().Property(x => x.Code).HasMaxLength(100);
        mb.Entity<Vehicle>().Property(x => x.PlateKey).HasMaxLength(100);
        mb.Entity<ParkingEvent>().Property(x => x.PlateKey).HasMaxLength(100);
        mb.Entity<AnprCommand>().Property(x => x.Status).HasMaxLength(50);

        HrModelDefaults.Normalize(mb);
    }
}

/// <summary>پیاده‌سازی قرارداد IParkingReportSource — گزارش ورود/خروج خودروها (عیناً از ReportService قبلی).</summary>
public class ParkingReportSource(ParkingDbContext db) : IParkingReportSource
{
    public async Task<List<AttParkingRow>> ParkingEventsAsync(string? fromKey, string? toKey, string? co,
        string? lotId, string? direction, string? result, string? method, int limit = 1000,
        IReadOnlyCollection<string>? companies = null,
        IReadOnlyCollection<string>? deptIds = null, string? selfEmployeeId = null)
    {
        var fromDt = fromKey is null ? DateTime.MinValue : DateTime.ParseExact(fromKey, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var toDt = toKey is null ? DateTime.MaxValue : DateTime.ParseExact(toKey, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).AddDays(1);

        var q = db.ParkingEvents.AsNoTracking()
            .Where(e => e.Timestamp >= fromDt && e.Timestamp < toDt)
            .Include(e => e.Gate).ThenInclude(g => g!.Lot)
            .Include(e => e.Vehicle).ThenInclude(v => v!.Employee).ThenInclude(e => e!.Company)
            .Include(e => e.Camera)
            .AsQueryable();

        if (!string.IsNullOrEmpty(co)) q = q.Where(e => e.Gate!.CompanyId == co);
        // فاز ۷۴ — دامنهٔ رکوردی: رویدادهای فقط شرکت‌های مجاز (SQL-ساید روی گیت)
        if (companies is { Count: > 0 })
            q = q.Where(e => e.Gate != null && e.Gate.CompanyId != null && companies.Contains(e.Gate.CompanyId));
        // فاز ۷۵ — دامنهٔ واحد/خودِ پرسنل: از مالک خودرو (SQL-ساید روی ستون‌های ایندکس‌دار پرسنل)
        if (deptIds is { Count: > 0 })
            q = q.Where(e => e.Vehicle != null && e.Vehicle.Employee != null
                          && e.Vehicle.Employee.DepartmentId != null && deptIds.Contains(e.Vehicle.Employee.DepartmentId));
        if (!string.IsNullOrEmpty(selfEmployeeId))
            q = q.Where(e => e.Vehicle != null && e.Vehicle.EmployeeId == selfEmployeeId);
        if (!string.IsNullOrEmpty(lotId)) q = q.Where(e => e.Gate!.LotId == lotId);
        if (!string.IsNullOrEmpty(direction)) q = q.Where(e => e.Direction == direction);
        if (!string.IsNullOrEmpty(result)) q = q.Where(e => e.Result == result);
        if (!string.IsNullOrEmpty(method)) q = q.Where(e => e.Method == method);

        var rows = await q.OrderByDescending(e => e.Timestamp).Take(limit)
            .Select(e => new AttParkingRow(
                e.Timestamp,
                e.PlateText,
                e.Vehicle != null && e.Vehicle.Employee != null ? e.Vehicle.Employee.FirstName + " " + e.Vehicle.Employee.LastName : "مهمان/ناشناس",
                e.Vehicle != null ? e.Vehicle.Type : "—",
                e.Gate != null && e.Gate.Company != null ? e.Gate.Company.Name : "—",
                e.Gate != null && e.Gate.Lot != null ? e.Gate.Lot.Name : "—",
                e.Gate != null ? e.Gate.Name : "—",
                e.Direction,
                e.Method,
                e.Camera != null ? e.Camera.Name : null,
                e.Confidence,
                e.Result,
                e.Reason))
            .ToListAsync();
        return rows;
    }

    // ==================== فاز ۶۸ — قلاب‌های جایگزین coreهای #if هاست ====================

    /// <summary>فهرست پارکینگ‌ها برای dropdown گزارش‌ساز/داشبورد (شکل anonymous قبلی هاست).</summary>
    public async Task<List<HrLotOption>> LotsAsync()
    {
        var lotsRaw = await db.ParkingLots.AsNoTracking().OrderBy(l => l.Name)
            .Select(l => new HrLotOption(l.Id, l.Name, l.Code, l.Gates.Count,
                         l.Company != null ? l.Company.Name : null))
            .ToListAsync();
        return lotsRaw;
    }

    /// <summary>دوربین‌های ANPR برای تنظیمات گیت‌وی (شکل anonymous JSON قبلی هاست).</summary>
    public async Task<List<object>> CamerasAsync()
    {
        var camsRaw = await db.AnprCameras.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Name, c.Serial, c.Ip, c.Active, c.Brand, c.Model, c.Mode,
                Gate = c.Gate != null ? c.Gate.Name : "—",
                LastSeen = c.LastSeenAt,
            })
            .ToListAsync();
        return camsRaw.Select(c => (object)c).ToList();
    }

    /// <summary>جست‌وجوی سریع خودرو (داشبورد پارکینگ) — روی پلاک و مالک.</summary>
    public async Task<List<HrVehicleHitDto>> VehicleSearchAsync(string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return [];
        var like = q.Trim();
        var rows = await db.Vehicles.AsNoTracking()
            .Where(v => v.PlateKey.Contains(like) || v.PlateNo.Contains(like))
            .OrderBy(v => v.PlateKey).Take(12)
            .Select(v => new
            {
                v.PlateNo, v.PlateKey, v.Brand, v.Model,
                Owner = v.Employee != null ? v.Employee.FullName : "—",
            })
            .ToListAsync();
        return rows.Select(r => new HrVehicleHitDto(
            r.PlateNo, r.Brand,
            $"{r.PlateNo} — {r.Owner} ({(r.Brand ?? "")} {r.Model ?? ""})".Trim())).ToList();
    }
}
