using Platform.Modules.Parking.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Modules.Parking.Data;

// ======================================================================
// فاز ۶۸ — پلاگین دیتای ماژول پارکینگ (منتقل‌شده از هاست؛ رفتار همان)
// ======================================================================
public sealed class ParkingDataModule : IHrDataModule
{
    public string Code => "parking";

    // فاز ۶۹ — خانوادهٔ نوع‌های این ماژول (مالکیت اسکیما) — داده‌محور برای گارد Ignore
    public Type[] FamilyTypes => [typeof(ParkingLot), typeof(ParkingGate), typeof(Vehicle), typeof(ParkingPermit),
        typeof(ParkingEvent), typeof(AnprCamera), typeof(AnprCommand)];

    // فاز ۱۲۲ — اعلام خانواده‌های داده برای مرکز درون‌ریزی/برون‌ریزی
    public HrDataExchangeDecl[] DataExchange =>
    [
        new("Vehicle", "خودروهای پارکینگ", "پارکینگ", true, "PlateKey"),
        new("ParkingLot", "پارکینگ‌ها", "پارکینگ", true, "Name"),
        new("ParkingGate", "گیت‌های پارکینگ", "پارکینگ", true, "Name"),
        new("ParkingPermit", "مجوزهای پارکینگ", "پارکینگ", true),
        new("AnprCamera", "دوربین‌های پلاک‌خوان", "پارکینگ", true, "Name"),
        new("ParkingEvent", "رویدادهای پارکینگ", "پارکینگ", false),
        new("AnprCommand", "دستورهای پلاک‌خوان", "پارکینگ", false),
    ];
    public string Title => "پارکینگ (پلاک‌خوان)";
    public string Icon => "🅿️";
    public string Description => "پارکینگ‌ها و دروازه‌ها، خودروها، مجوزهای عبور و رخدادهای ANPR";

    public void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb)
    {
        services.AddScoped<ParkingDbContext>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return new ParkingDbContext(HrDbResolver.OptionsFor<ParkingDbContext>(useDb, cfg).Options);
        });
        services.AddScoped<IParkingReportSource, ParkingReportSource>();
        services.AddScoped<IModuleSeeder, ParkingSeeder>();
    }

    public void ComposeModel(ModelBuilder mb)
    {
        mb.Entity<ParkingLot>().ToTable("ParkingLots");
        mb.Entity<ParkingGate>().ToTable("ParkingGates");
        mb.Entity<Vehicle>().ToTable("Vehicles");
        mb.Entity<ParkingPermit>().ToTable("ParkingPermits");
        mb.Entity<ParkingEvent>().ToTable("ParkingEvents");
        mb.Entity<AnprCamera>().ToTable("AnprCameras");
        mb.Entity<AnprCommand>().ToTable("AnprCommands");
        // کنترل پارکینگ (فاز ۴۰) — کد/پلاک به‌ازای هر شرکت یکتا (چندشرکتی)
        mb.Entity<ParkingLot>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<ParkingGate>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<Vehicle>().HasIndex(x => new { x.CompanyId, x.PlateKey }).IsUnique();
        mb.Entity<ParkingPermit>().HasIndex(x => x.VehicleId);
        mb.Entity<ParkingEvent>().HasIndex(x => x.Timestamp);
        mb.Entity<ParkingEvent>().HasIndex(x => x.PlateKey);
        // دوربین پلاک‌خوان (فاز ۴۱) — SN و توکن یکتا همانند دستگاه‌های تردد
        mb.Entity<AnprCamera>().HasIndex(x => x.Serial).IsUnique();
        mb.Entity<AnprCamera>().HasIndex(x => x.WebhookToken).IsUnique();
        mb.Entity<AnprCommand>().HasIndex(x => new { x.CameraId, x.Status });
        // حذف پارکینگ گیت‌ها/مجوزها را آبشاری پاک می‌کند؛ حذف پرسنل خودروها را آبشاری پاک می‌کند؛
        // با حذف خودرو/دوربین رویدادها فقط بی‌صاحب (SetNull) می‌شوند تا سابقه پلاک بماند.
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
        // فاز ۴۹ — رویداد پارکینگ فقط از یک مسیر آبشاری تغذیه می‌شود (GateId = NO ACTION)
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
    }

    // ======================================================================
    // فاز ۶۸ — پاک‌سازی/شمارش‌های شرکت (پورت از DbSeeder/CompaniesController هاست)
    // ======================================================================

    /// <summary>پاک‌سازی نمونه‌های پارکینگ — پورت ClearParkingDemo (ترتیب وابستگی: رویداد → مجوز → خودرو → گیت → پارکینگ).</summary>
    public Task ClearDemoAsync(DbContext db)
    {
        db.Set<ParkingEvent>().RemoveRange(db.Set<ParkingEvent>().ToList());
        db.Set<ParkingPermit>().RemoveRange(db.Set<ParkingPermit>().ToList());
        db.Set<Vehicle>().RemoveRange(db.Set<Vehicle>().ToList());
        db.Set<ParkingGate>().RemoveRange(db.Set<ParkingGate>().ToList());
        db.Set<ParkingLot>().RemoveRange(db.Set<ParkingLot>().ToList());
        db.SaveChanges();
        return Task.CompletedTask;
    }

    /// <summary>شمارش پارکینگ‌ها/خودروها به‌ازای شرکت — پورت LotCounts/LotCountFor/VehicleCountFor.</summary>
    public Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string kind) => kind switch
    {
        "lots" => db.Set<ParkingLot>().Where(l => l.CompanyId != null).GroupBy(l => l.CompanyId!)
            .ToDictionaryAsync(g => g.Key, g => g.Count()),
        "vehicles" => db.Set<Vehicle>().Where(v => v.CompanyId != null).GroupBy(v => v.CompanyId!)
            .ToDictionaryAsync(g => g.Key, g => g.Count()),
        _ => Task.FromResult(new Dictionary<string, int>()),
    };

    public Task<int> CompanyCountAsync(DbContext db, string kind, string companyId) => kind switch
    {
        "lots" => db.Set<ParkingLot>().CountAsync(l => l.CompanyId == companyId),
        "vehicles" => db.Set<Vehicle>().CountAsync(v => v.CompanyId == companyId),
        _ => Task.FromResult(0),
    };
}
