using Platform.Web.Services;
using Platform.Modules.Parking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Modules.Parking.Data;

// ======================================================================
// فاز ۶۲ — سیدِ داده‌های اولیهٔ ماژول پارکینگ (IModuleSeeder)
// ----------------------------------------------------------------------
// خودترمیم و مستقل از سیدر دادهٔ کسب‌وکار (در هر بوت بررسی می‌شود):
//   ۱) داده نمایشی پارکینگ (فاز ۴۰) — دو پارکینگ پارس آسیا + یک پارکینگ آریا سازه،
//      گیت‌های ورود/خروج، خودروهای پرسنل با پلاک ایرانی، مجوزهای دائم/موقت/لغوشده/
//      منقضی و رویدادهای ورود/خروج سه روز اخیر (چند خودرو اکنون داخل‌اند).
//   ۲) دوربین‌های پلاک‌خوان (فاز ۴۱) — ثبت روی گیت‌های موجود (اولویت گیت‌های پلاک‌خوان)،
//      انتساب رویدادهای پلاک‌خوانِ بی‌دوربین به دوربین همان گیت + پر کردن کلید پلاک و
//      اطمینان، و ضربان دوربین‌های دمو (یکی آفلاین برای نمایش هشدار).
// همهٔ داده‌ها روی ParkingDbContext همین ماژول — بدون ارجاع به ماژول دیگر.
// جریان تصادفی قطعی مستقل (Mulberry32(4004)).
// ======================================================================
public sealed class ParkingSeeder : IModuleSeeder
{
    public string ModuleCode => "parking";
    public int Order => 40;

    public async Task SeedAsync(IServiceProvider scopedServices, bool demoData, CancellationToken ct = default)
    {
        var db = scopedServices.GetRequiredService<ParkingDbContext>();
        await EnsureParkingSeedAsync(db, ct);
        await EnsureAnprSeedAsync(db, ct);
    }

    /// <summary>نرمال‌سازی پلاک برای تطبیق پلاک‌خوان — کلید کانونیال PlateFmt پلتفرم.</summary>
    private static string PlateKeyOf(string plate) => PlateFmt.CanonicalKey(plate);

    private static async Task EnsureParkingSeedAsync(ParkingDbContext db, CancellationToken ct)
    {
        if (await db.ParkingLots.AnyAsync(ct)) return;

        var pac = await db.Companies.FirstOrDefaultAsync(c => c.Code == "PAC-01", ct);
        var asc = await db.Companies.FirstOrDefaultAsync(c => c.Code == "ASC-02", ct);
        var emps = await db.Employees.OrderBy(e => e.PersonnelCode).ToListAsync(ct);
        if (pac is null || emps.Count == 0) return;

        // ---------- پارکینگ‌ها ----------
        var lot1 = new ParkingLot
        {
            Name = "پارکینگ اصلی ورودی", Code = "PK-01", CompanyId = pac.Id, Capacity = 120,
            MonthlyFee = 0, Location = "جنب ورودی اصلی سایت، سطح همکف",
        };
        var lot2 = new ParkingLot
        {
            Name = "پارکینگ کارکنان شیفت", Code = "PK-02", CompanyId = pac.Id, Capacity = 80,
            MonthlyFee = 500000, Location = "ضلع شرقی سایت، طبقه منفی یک",
        };
        db.ParkingLots.AddRange(lot1, lot2);
        if (asc is not null)
            db.ParkingLots.Add(new ParkingLot
            {
                Name = "پارکینگ دفتر مرکزی آریا سازه", Code = "ASC-PK1", CompanyId = asc.Id, Capacity = 30,
                MonthlyFee = 0, Location = "پشت ساختمان اداری",
            });
        await db.SaveChangesAsync(ct);

        // ---------- گیت‌ها ----------
        var devAnpr = await db.Devices.FirstOrDefaultAsync(d => d.Purpose == "کنترل تردد", ct);
        var g1 = new ParkingGate { Name = "راهبند ورودی اصلی", Code = "PG-IN1", CompanyId = pac.Id, LotId = lot1.Id, DeviceId = devAnpr?.Id, Type = "پلاک‌خوان", Direction = "ورودی" };
        var g2 = new ParkingGate { Name = "راهبند خروجی اصلی", Code = "PG-OUT1", CompanyId = pac.Id, LotId = lot1.Id, Type = "پلاک‌خوان", Direction = "خروجی" };
        var g3 = new ParkingGate { Name = "گیت موتورسیکلت", Code = "PG-BIKE", CompanyId = pac.Id, LotId = lot1.Id, Type = "کارتی", Direction = "دوطرفه" };
        var g4 = new ParkingGate { Name = "راهبند ورودی شیفت", Code = "PG-IN2", CompanyId = pac.Id, LotId = lot2.Id, Type = "راهبند", Direction = "ورودی" };
        var g5 = new ParkingGate { Name = "راهبند خروجی شیفت", Code = "PG-OUT2", CompanyId = pac.Id, LotId = lot2.Id, Type = "راهبند", Direction = "خروجی" };
        db.ParkingGates.AddRange(g1, g2, g3, g4, g5);
        await db.SaveChangesAsync(ct);

        // ---------- خودروهای پرسنل (پلاک ایرانی) ----------
        string[] brands = { "پراید", "پژو ۲۰۶", "سمند", "تیبا", "کوییک", "شاهین", "دنا", "نیسان" };
        string[] colors = { "سفید", "نقره‌ای", "مشکی", "خاکستری", "آبی" };
        string[] letters = { "ع", "ب", "س", "د", "م", "ی" };
        string[] iranCodes = { "36", "22", "68", "87", "11" };
        var vehicles = new List<Vehicle>();
        var usedKeys = new HashSet<string>();
        var carEmps = emps.Where(e => e.Status == "فعال").Take(12).ToList();
        for (int i = 0; i < carEmps.Count; i++)
        {
            var e = carEmps[i];
            string plate = "";
            string key = "";
            // پلاک قطعی و بدون تکرار (بر پایه شماره پرسنلی)
            for (int t = 0; t < 50; t++)
            {
                int left = Rint(11, 89), right = Rint(100, 999);
                var letter = letters[(i + t) % letters.Length];
                var iran = iranCodes[(i + t) % iranCodes.Length];
                plate = $"{left}{letter}{right} ایران {iran}";
                key = PlateKeyOf(plate);
                if (usedKeys.Add(key)) break;
            }
            var type = i == 5 ? "وانت" : i == 9 ? "موتورسیکلت" : "سواری";
            vehicles.Add(new Vehicle
            {
                CompanyId = e.CompanyId, EmployeeId = e.Id, PlateNo = plate, PlateKey = key,
                Type = type, Brand = type == "موتورسیکلت" ? "هوندا" : brands[i % brands.Length],
                Color = colors[i % colors.Length],
            });
        }
        db.Vehicles.AddRange(vehicles);
        await db.SaveChangesAsync(ct);

        // ---------- مجوزها ----------
        for (int i = 0; i < vehicles.Count; i++)
        {
            var v = vehicles[i];
            var lot = i % 3 == 0 ? lot2 : lot1;
            db.ParkingPermits.Add(new ParkingPermit
            {
                VehicleId = v.Id, LotId = lot.Id,
                Type = "دائم",
                ValidFrom = DateTime.Now.AddDays(-90),
                SpotNo = i < 4 ? $"A-{i + 1}" : null,
                Status = "فعال",
            });
        }
        // یک مجوز موقت با انقضا آینده + یک لغوشده + یک منقضی
        db.ParkingPermits.Add(new ParkingPermit
        {
            VehicleId = vehicles[1].Id, LotId = lot2.Id, Type = "موقت",
            ValidFrom = DateTime.Now.AddDays(-5), ValidUntil = DateTime.Now.AddDays(30),
            Status = "فعال", SpotNo = "B-12",
        });
        if (vehicles.Count > 2)
            db.ParkingPermits.Add(new ParkingPermit
            {
                VehicleId = vehicles[2].Id, LotId = lot1.Id, Type = "دائم",
                ValidFrom = DateTime.Now.AddDays(-60), Status = "لغوشده",
            });
        if (vehicles.Count > 3)
            db.ParkingPermits.Add(new ParkingPermit
            {
                VehicleId = vehicles[3].Id, LotId = lot1.Id, Type = "موقت",
                ValidFrom = DateTime.Now.AddDays(-40), ValidUntil = DateTime.Now.AddDays(-10),
                Status = "فعال",
            });
        await db.SaveChangesAsync(ct);

        // ---------- رویدادهای سه روز اخیر ----------
        var inGate1 = g1; var outGate1 = g2;
        for (int day = 3; day >= 1; day--)
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (day == 1 && i >= 6) continue; // امروز فقط بخشی ثبت شده
                var baseTime = DateTime.Now.Date.AddDays(-day).AddHours(7).AddMinutes(Rint(0, 90));
                db.ParkingEvents.Add(new ParkingEvent
                {
                    GateId = (i % 3 == 0 ? g4 : g1).Id, VehicleId = v.Id, PlateText = v.PlateNo,
                    Direction = "ورود", Method = v.Type == "موتورسیکلت" ? "کارت" : "پلاک‌خوان",
                    Result = "مجاز", Timestamp = baseTime,
                });
                db.ParkingEvents.Add(new ParkingEvent
                {
                    GateId = (i % 3 == 0 ? g5 : outGate1).Id, VehicleId = v.Id, PlateText = v.PlateNo,
                    Direction = "خروج", Method = v.Type == "موتورسیکلت" ? "کارت" : "پلاک‌خوان",
                    Result = "مجاز", Timestamp = baseTime.AddHours(9).AddMinutes(Rint(0, 60)),
                });
            }
        }
        // دو خودرو اکنون داخل پارکینگ‌اند (ورود امروز بدون خروج)
        foreach (var i in new[] { 0, 2 })
        {
            var v = vehicles[i];
            db.ParkingEvents.Add(new ParkingEvent
            {
                GateId = g1.Id, VehicleId = v.Id, PlateText = v.PlateNo,
                Direction = "ورود", Method = "پلاک‌خوان", Result = "مجاز",
                Timestamp = DateTime.Now.AddHours(-2).AddMinutes(Rint(0, 50)),
            });
        }
        // عبورهای غیرمجاز — پلاک ناشناس و خودرو بدون مجوز فعال
        db.ParkingEvents.Add(new ParkingEvent
        {
            GateId = g1.Id, PlateText = "43و912 ایران 77", Direction = "ورود",
            Method = "پلاک‌خوان", Result = "غیرمجاز", Reason = "خودرو فاقد مجوز پارکینگ است",
            Timestamp = DateTime.Now.AddHours(-5),
        });
        if (vehicles.Count > 3)
            db.ParkingEvents.Add(new ParkingEvent
            {
                GateId = g1.Id, VehicleId = vehicles[3].Id, PlateText = vehicles[3].PlateNo,
                Direction = "ورود", Method = "پلاک‌خوان", Result = "غیرمجاز",
                Reason = "مجوز منقضی", Timestamp = DateTime.Now.AddHours(-4),
            });
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureAnprSeedAsync(ParkingDbContext db, CancellationToken ct)
    {
        var gates = await db.ParkingGates.OrderBy(g => g.Code).ToListAsync(ct);
        if (gates.Count == 0) return;

        if (!await db.AnprCameras.AnyAsync(ct))
        {
            // انتخاب گیت‌های هدف — اولویت با گیت‌های پلاک‌خوان، سپس هر گیت
            var anprGates = gates.Where(g => g.Type == "پلاک‌خوان").OrderBy(g => g.Code).ToList();
            var targetGates = anprGates.Count >= 2
                ? anprGates.Take(4).ToList()
                : gates.Take(4).ToList();

            string[] brands = { "Hikvision", "Dahua", "ZKTeco", "Dahua" };
            string[] models = { "iDS-TMG418-B", "ITC802-V3", "ANPR-1", "ITC237-V2" };
            string[] locs = { "لاین ورود، ترافیک سنگین", "لاین خروج، درب شمالی", "ورودی پارکینگ شیفت", "خروجی پارکینگ شیفت" };
            int snBase = 1024;
            for (int i = 0; i < targetGates.Count; i++)
            {
                var g = targetGates[i];
                db.AnprCameras.Add(new AnprCamera
                {
                    Name = $"دوربین پلاک‌خوان {g.Name}",
                    GateId = g.Id, CompanyId = g.CompanyId,
                    Brand = brands[i % brands.Length], Model = models[i % models.Length],
                    Serial = $"ANPR-{g.Code}-{snBase + i * 1024}",
                    Ip = $"10.10.{(i < 2 ? 4 : 5)}.{21 + i}",
                    Location = locs[i % locs.Length],
                    Direction = g.Direction == "ورودی" ? "ورودی" : g.Direction == "خروجی" ? "خروجی" : "دوطرفه",
                    ConfidenceThreshold = 70,
                    StreamUrl = $"rtsp://10.10.{(i < 2 ? 4 : 5)}.{21 + i}:554/anpr/ch{i + 1}",
                    // آخرین دوربین آفلاین می‌ماند تا هشدار «قطع دوربین» در دمو قابل مشاهده باشد
                    LastSeenAt = i == targetGates.Count - 1 && targetGates.Count > 1
                        ? DateTime.Now.AddHours(-3) : DateTime.Now,
                    Mode = i == targetGates.Count - 1 && targetGates.Count > 1 ? "آفلاین" : "آنلاین",
                });
            }
            await db.SaveChangesAsync(ct);
        }

        // انتساب رویدادهای پلاک‌خوان بی‌دوربین به دوربین همان گیت + پر کردن کلید پلاک و اطمینان
        var cams = await db.AnprCameras.ToListAsync(ct);
        var orphan = await db.ParkingEvents
            .Where(e => e.Method == "پلاک‌خوان" && e.CameraId == null)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(ct);
        if (orphan.Count > 0)
        {
            foreach (var e in orphan)
            {
                var cam = cams.FirstOrDefault(c => c.GateId == e.GateId);
                if (cam is not null) e.CameraId = cam.Id;
                if (string.IsNullOrEmpty(e.PlateKey)) e.PlateKey = PlateKeyOf(e.PlateText);
                e.Confidence ??= Rint(85, 99);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    // ---------- RNG قطعی (mulberry32 — جریان مستقل ماژول پارکینگ) ----------
    private static Func<double> _rand = Mulberry32(4004);
    private static double Rand() => _rand();
    private static int Rint(int a, int b) => a + (int)(Rand() * (b - a + 1));

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
}
