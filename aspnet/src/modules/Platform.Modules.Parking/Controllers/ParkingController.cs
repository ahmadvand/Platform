using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Platform.Modules.Parking.Data;
using Platform.Modules.Parking.Domain;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// کنترل پارکینگ (فاز ۴۰) — پارکینگ‌ها و گیت‌ها، خودروهای پرسنل، مجوزهای تردد خودرو
/// و رویدادهای ورود/خروج با موتور ارزیابی مجوز (پلاک‌محور، آنتی‌پس‌بک و کنترل ظرفیت).
/// فاز ۶۱ — کانتکست دیتای اختصاصی ماژول (ParkingDbContext)؛ دادهٔ پایه (شرکت/پرسنل/دستگاه)
/// روی همان کانتکست ماژول و SecurityAlert روی HrBaseDbContext؛ حسابرسی از IAuditSink.
/// </summary>
public class ParkingController : HrModuleControllerBase
{
    private readonly ParkingDbContext Db;
    private readonly HrBaseDbContext Base;

    public ParkingController(ParkingDbContext db, HrBaseDbContext baseDb, ICurrentUser me, IAuditSink audits)
        : base(me, audits)
    { Db = db; Base = baseDb; }

    // ============================================================
    // صفحه اصلی — تابلوی زنده + پارکینگ‌ها + خودروها + مجوزها + رویدادها
    // ============================================================
    [HttpGet]
    [HasPerm("parking.view")]
    public async Task<IActionResult> Index(string tab = "board", string? co = null, string? result = null)
    {
        ViewBag.Tab = tab;
        ViewBag.Co = co;
        ViewBag.ResultFilter = result;
        // فاز ۷۴ — دامنهٔ رکوردی پارکینگ: محدوده‌های شرکت-محور + کاربر محدودشده فقط شرکت‌های مجاز
        var scope = await this.ScopeAsync();
        if (!scope.All && scope.Companies.Count > 0)
            co = string.IsNullOrEmpty(co) || !scope.Companies.Contains(co) ? null : co;
        ViewBag.Companies = (await Db.Companies.OrderBy(c => c.Code).ToListAsync())
            .Where(c => scope.All || scope.Companies.Count == 0 || scope.Companies.Contains(c.Id)).ToList();
        // اسکن تنبلی دوربین‌های پلاک‌خوان — آفلاین‌ها + هشدار قطع دوربین
        await ParkingOps.SweepOfflineAsync(Db, Base);
        ViewBag.Cameras = await Db.AnprCameras.Include(c => c.Gate).ThenInclude(g => g!.Lot)
            .Include(c => c.Company).OrderBy(c => c.Name).ToListAsync();
        ViewBag.PendingCmds = await Db.AnprCommands
            .Where(c => c.Status == "pending" || c.Status == "delivered")
            .Include(c => c.Camera).OrderBy(c => c.CreatedAt).ToListAsync();

        var lotsQ = Db.ParkingLots.Include(l => l.Company).Include(l => l.Gates).Include(l => l.Permits).AsQueryable();
        if (!string.IsNullOrEmpty(co)) lotsQ = lotsQ.Where(l => l.CompanyId == co);
        lotsQ = lotsQ.WhereCoScope(scope, l => l.CompanyId);
        var lots = await lotsQ.OrderBy(l => l.Code).ToListAsync();
        ViewBag.Lots = lots;

        var vehiclesQ = Db.Vehicles.Include(v => v.Employee).Include(v => v.Company)
            .Include(v => v.Permits).ThenInclude(p => p.Lot).AsQueryable();
        if (!string.IsNullOrEmpty(co)) vehiclesQ = vehiclesQ.Where(v => v.CompanyId == co);
        vehiclesQ = vehiclesQ.WhereCoScope(scope, v => v.CompanyId);
        var vehicles = await vehiclesQ.OrderByDescending(v => v.CreatedAt).ToListAsync();
        ViewBag.Vehicles = vehicles;

        var permitsQ = Db.ParkingPermits.Include(p => p.Vehicle).ThenInclude(v => v!.Employee)
            .Include(p => p.Lot).AsQueryable();
        if (!string.IsNullOrEmpty(co)) permitsQ = permitsQ.Where(p => p.Vehicle!.CompanyId == co);
        permitsQ = permitsQ.WhereCoScope(scope, p => p.Vehicle!.CompanyId);
        var permits = await permitsQ.OrderByDescending(p => p.CreatedAt).ToListAsync();
        ViewBag.Permits = permits;

        var eventsQ = Db.ParkingEvents.Include(e => e.Gate).ThenInclude(g => g!.Lot)
            .Include(e => e.Vehicle).ThenInclude(v => v!.Employee).Include(e => e.Camera).AsQueryable();
        if (!string.IsNullOrEmpty(co)) eventsQ = eventsQ.Where(e => e.Gate!.CompanyId == co);
        if (!string.IsNullOrEmpty(result)) eventsQ = eventsQ.Where(e => e.Result == result);
        ViewBag.Events = await eventsQ.OrderByDescending(e => e.Timestamp).Take(100).ToListAsync();
        // گیت‌ها و دوربین‌های فعال برای شبیه‌ساز پلاک‌خوان (تب رویدادها)
        ViewBag.ScanGates = await Db.ParkingGates.Include(g => g.Lot)
            .Where(g => g.Status == "فعال").OrderBy(g => g.Code).ToListAsync();
        ViewBag.ScanCameras = await Db.AnprCameras.Include(c => c.Gate).ThenInclude(g => g.Lot)
            .Where(c => c.Active).OrderBy(c => c.Name).ToListAsync();

        // اشغال فعلی هر پارکینگ — آخرین رویداد مجاز هر خودرو در همان پارکینگ: ورود = داخل
        var occ = new Dictionary<string, int>();
        foreach (var l in lots) occ[l.Id] = 0;
        var lotIds = lots.Select(l => l.Id).ToHashSet();
        var passes = await Db.ParkingEvents
            .Where(e => e.VehicleId != null && e.Result == "مجاز" && lotIds.Contains(e.Gate!.LotId))
            .OrderBy(e => e.Timestamp)
            .Select(e => new { e.VehicleId, LotId = e.Gate!.LotId, e.Direction })
            .ToListAsync();
        var insideByLot = new Dictionary<string, HashSet<string>>();
        foreach (var l in lots) insideByLot[l.Id] = new HashSet<string>();
        foreach (var p in passes)
        {
            var set = insideByLot[p.LotId];
            if (p.Direction == "ورود") set.Add(p.VehicleId!); else set.Remove(p.VehicleId!);
        }
        foreach (var l in lots) occ[l.Id] = insideByLot[l.Id].Count;
        ViewBag.Occupancy = occ;
        ViewBag.InsideTotal = insideByLot.Values.Sum(s => s.Count);
        ViewBag.TodayEvents = await Db.ParkingEvents.CountAsync(e => e.Timestamp >= DateTime.Now.Date);
        ViewBag.TodayAutoReads = await Db.ParkingEvents
            .CountAsync(e => e.Method == "پلاک‌خوان" && e.Timestamp >= DateTime.Now.Date);

        return View();
    }

    // ============================================================
    // نمای زنده (فاز ۴۲) — خوراک JSON دیوار دوربین‌ها و تیکر خوانش‌ها
    // فراخوانی دوره‌ای ۶ ثانیه از تب «نمای زنده»؛ همان داده‌های تابلوی زنده
    // ============================================================
    [HttpGet]
    [HasPerm("parking.view")]
    public async Task<IActionResult> LiveData()
    {
        await ParkingOps.SweepOfflineAsync(Db, Base);

        var lots = await Db.ParkingLots.Where(l => l.Active).ToListAsync();
        var cams = await Db.AnprCameras.Include(c => c.Gate).ThenInclude(g => g!.Lot)
            .OrderBy(c => c.Name).ToListAsync();

        // رویدادهای اخیر — تیکر + آخرین خوانش هر دوربین
        var evs = await Db.ParkingEvents.Include(e => e.Gate).ThenInclude(g => g!.Lot)
            .Include(e => e.Vehicle).ThenInclude(v => v!.Employee).Include(e => e.Camera)
            .OrderByDescending(e => e.Timestamp).Take(250).ToListAsync();

        static string OwnerOf(ParkingEvent e) => e.Vehicle?.Employee?.FullName ?? "خودروی مهمان/ناشناس";
        static string TimeText(DateTime t) => t.Date == DateTime.Today
            ? Jalali.FmtTime(t)
            : $"{Jalali.FmtFa(t)} — {Jalali.FmtTime(t)}";

        var lastByCam = new Dictionary<string, ParkingEvent>();
        foreach (var e in evs.Where(e => e.CameraId is not null))
            lastByCam.TryAdd(e.CameraId!, e);

        var cameras = cams.Select(c =>
        {
            ParkingEvent? le = lastByCam.TryGetValue(c.Id, out var x) ? x : null;
            return new
            {
                c.Id,
                c.Name,
                c.Brand,
                model = c.Model ?? "",
                ip = c.Ip ?? "",
                stream = c.StreamUrl ?? "",
                online = c.Active && ParkingOps.IsOnline(c),
                c.Active,
                gate = c.Gate?.Name ?? "—",
                gateState = c.Gate?.OpenState ?? "نرمال",
                lot = c.Gate?.Lot?.Name ?? "—",
                dir = c.Direction,
                lastSeen = c.LastSeenAt is null ? "بدون سابقه" : Jalali.FmtLong(c.LastSeenAt.Value),
                last = le is null ? null : new
                {
                    time = TimeText(le.Timestamp),
                    plate = le.PlateText,
                    result = le.Result,
                    conf = le.Confidence,
                    owner = OwnerOf(le),
                    dir = le.Direction,
                },
            };
        }).ToList();

        var events = evs.Take(30).Select(e => new
        {
            e.Id,
            time = TimeText(e.Timestamp),
            plate = e.PlateText,
            owner = OwnerOf(e),
            gate = e.Gate?.Name ?? "—",
            lot = e.Gate?.Lot?.Name ?? "—",
            dir = e.Direction,
            e.Method,
            result = e.Result,
            conf = e.Confidence,
            cam = e.Camera?.Name,
        }).ToList();

        // خودروهای داخل پارکینگ — آخرین عبور مجاز هر خودرو در هر پارکینگ (همان منطق تابلو + زمان ورود)
        var lotNames = lots.ToDictionary(l => l.Id, l => l.Name);
        var lotIds = lotNames.Keys.ToHashSet();
        var passes = await Db.ParkingEvents
            .Where(e => e.VehicleId != null && e.Result == "مجاز" && lotIds.Contains(e.Gate!.LotId))
            .OrderBy(e => e.Timestamp)
            .Select(e => new
            {
                e.VehicleId,
                LotId = e.Gate!.LotId,
                e.Direction,
                e.Timestamp,
                Plate = e.PlateText,
                Owner = e.Vehicle != null && e.Vehicle.Employee != null ? e.Vehicle.Employee.FullName : null,
            })
            .ToListAsync();
        var insideMap = new Dictionary<(string, string), (string Plate, DateTime Since, string? Owner)>();
        foreach (var p in passes)
        {
            var key = (p.VehicleId!, p.LotId);
            if (p.Direction == "ورود")
                insideMap[key] = (p.Plate, p.Timestamp, p.Owner);
            else
                insideMap.Remove(key);
        }
        var inside = insideMap.Select(kv => new
        {
            plate = kv.Value.Plate,
            owner = kv.Value.Owner ?? "مهمان",
            lot = lotNames.GetValueOrDefault(kv.Key.Item2, "—"),
            since = TimeText(kv.Value.Since),
        }).OrderBy(x => x.since).ToList();

        var today = DateTime.Now.Date;
        return Json(new
        {
            ok = true,
            kpis = new
            {
                inside = inside.Count,
                cap = lots.Sum(l => l.Capacity),
                today = await Db.ParkingEvents.CountAsync(e => e.Timestamp >= today),
                autoReads = await Db.ParkingEvents.CountAsync(e => e.Method == "پلاک‌خوان" && e.Timestamp >= today),
                online = cams.Count(c => c.Active && ParkingOps.IsOnline(c)),
                cams = cams.Count(c => c.Active),
            },
            cameras,
            events,
            inside,
        });
    }

    // ============================================================
    // فرم‌های مودال
    // ============================================================
    [HttpGet]
    [HasPerm("parking.lots.manage")]
    public async Task<IActionResult> CreateLot()
    {
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        return PartialView("_LotForm");
    }

    [HttpGet]
    [HasPerm("parking.lots.manage")]
    public async Task<IActionResult> CreateGate()
    {
        ViewBag.Lots = await Db.ParkingLots.Include(l => l.Company).OrderBy(l => l.Code).ToListAsync();
        ViewBag.Devices = await Db.Devices.Include(d => d.Company).OrderBy(d => d.Name).ToListAsync();
        return PartialView("_GateForm");
    }

    [HttpGet]
    [HasPerm("parking.vehicles.manage")]
    public async Task<IActionResult> CreateVehicle()
    {
        // فاز ۱۲۱ — بارگذاری مردهٔ لیست کامل پرسنل حذف شد (فرم با /Ui/EmpSearch کار می‌کند)
        return PartialView("_VehicleForm");
    }

    [HttpGet]
    [HasPerm("parking.vehicles.manage")]
    public async Task<IActionResult> CreatePermit()
    {
        ViewBag.Vehicles = await Db.Vehicles.Include(v => v.Employee)
            .Include(v => v.Company).OrderBy(v => v.PlateNo).ToListAsync();
        ViewBag.Lots = await Db.ParkingLots.Include(l => l.Company).OrderBy(l => l.Code).ToListAsync();
        return PartialView("_PermitForm");
    }

    /// <summary>فرم تعریف دوربین پلاک‌خوان (فاز ۴۱) — همان الگوی تعریف دستگاه.</summary>
    [HttpGet]
    [HasPerm("parking.cameras.manage")]
    public async Task<IActionResult> CreateCamera()
    {
        ViewBag.Gates = await Db.ParkingGates.Include(g => g.Lot).OrderBy(g => g.Code).ToListAsync();
        return PartialView("_CameraForm");
    }

    // ============================================================
    // ایجادها
    // ============================================================
    [HttpPost]
    [HasPerm("parking.lots.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLot(string name, string code, int capacity, string? companyId,
        string? location, string? monthlyFee)
    {
        name = (name ?? "").Trim(); code = (code ?? "").Trim().ToUpperInvariant();
        if (capacity < 1) capacity = 1;
        if (await Db.ParkingLots.AnyAsync(l => l.Code == code && l.CompanyId == companyId))
            return Fail("کد پارکینگ در این شرکت تکراری است");
        decimal fee = 0;
        decimal.TryParse((monthlyFee ?? "0").Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture, out fee);
        var lot = new ParkingLot
        {
            Name = name, Code = code, CompanyId = companyId, Capacity = capacity,
            MonthlyFee = fee, Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
        };
        Db.ParkingLots.Add(lot);
        await Db.SaveChangesAsync();
        await Audit("create", "parking_lot", lot.Id, new { lot.Name, lot.Code, lot.Capacity });
        return Done($"پارکینگ «{lot.Name}» با ظرفیت {Jalali.Num(capacity)} جایگاه ایجاد شد");
    }

    [HttpPost]
    [HasPerm("parking.lots.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGate(string name, string code, string lotId, string? deviceId,
        string type, string direction)
    {
        var lot = await Db.ParkingLots.FindAsync(lotId);
        if (lot is null) return Fail("پارکینگ انتخاب‌شده یافت نشد");
        code = (code ?? "").Trim().ToUpperInvariant();
        if (await Db.ParkingGates.AnyAsync(g => g.Code == code && g.CompanyId == lot.CompanyId))
            return Fail("کد گیت در این شرکت تکراری است");
        // شرکت گیت از پارکینگ می‌آید — گیت همیشه هم‌شرکت پارکینگ است
        var gate = new ParkingGate
        {
            Name = name.Trim(), Code = code, LotId = lotId, DeviceId = deviceId,
            Type = type, Direction = direction, CompanyId = lot.CompanyId,
        };
        Db.ParkingGates.Add(gate);
        await Db.SaveChangesAsync();
        await Audit("create", "parking_gate", gate.Id, new { gate.Name, lot = lot.Name });
        return Done($"گیت «{gate.Name}» در پارکینگ «{lot.Name}» ایجاد شد");
    }

    [HttpPost]
    [HasPerm("parking.vehicles.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVehicle(string employeeId, string plateNo, string type,
        string? brand, string? model, string? color)
    {
        var emp = await Db.Employees.FindAsync(employeeId);
        if (emp is null) return Fail("پرسنل انتخاب‌شده یافت نشد");
        plateNo = (plateNo ?? "").Trim();
        if (plateNo.Length < 4) return Fail("شماره پلاک معتبر نیست — مثال: 12ع471 ایران 36");
        var key = PlateFmt.CanonicalKey(plateNo);
        if (await Db.Vehicles.AnyAsync(v => v.PlateKey == key && v.CompanyId == emp.CompanyId))
            return Fail($"خودرویی با پلاک «{plateNo}» برای این شرکت ثبت شده است");
        var vehicle = new Vehicle
        {
            CompanyId = emp.CompanyId, EmployeeId = emp.Id, PlateNo = plateNo, PlateKey = key,
            Type = type, Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
            Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim(),
        };
        Db.Vehicles.Add(vehicle);
        await Db.SaveChangesAsync();
        await Audit("create", "vehicle", vehicle.Id, new { vehicle.PlateNo, emp = emp.FullName });
        return Done($"خودرو «{vehicle.PlateNo}» برای {emp.FullName} ثبت شد");
    }

    [HttpPost]
    [HasPerm("parking.vehicles.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePermit(string vehicleId, string lotId, string type,
        DateTime validFrom, DateTime? validUntil, string? spotNo)
    {
        var vehicle = await Db.Vehicles.FindAsync(vehicleId);
        var lot = await Db.ParkingLots.FindAsync(lotId);
        if (vehicle is null || lot is null) return Fail("خودرو یا پارکینگ انتخاب‌شده یافت نشد");
        if (vehicle.CompanyId != lot.CompanyId)
            return Fail("خودرو و پارکینگ باید هم‌شرکت باشند (چندشرکتی)");
        if (validUntil is not null && validUntil < validFrom)
            return Fail("تاریخ پایان اعتبار نمی‌تواند پیش از تاریخ شروع باشد");
        if (await Db.ParkingPermits.AnyAsync(p => p.VehicleId == vehicleId && p.LotId == lotId && p.Status == "فعال"))
            return Fail($"برای پلاک «{vehicle.PlateNo}» در پارکینگ «{lot.Name}» مجوز فعال وجود دارد");
        var permit = new ParkingPermit
        {
            VehicleId = vehicleId, LotId = lotId, Type = type,
            ValidFrom = validFrom, ValidUntil = validUntil,
            SpotNo = string.IsNullOrWhiteSpace(spotNo) ? null : spotNo.Trim(),
            Status = "فعال",
        };
        Db.ParkingPermits.Add(permit);
        await Db.SaveChangesAsync();
        await Audit("create", "parking_permit", permit.Id,
            new { plate = vehicle.PlateNo, lot = lot.Name, permit.Type });
        return Done($"مجوز پارکینگ «{lot.Name}» برای پلاک «{vehicle.PlateNo}» صادر شد");
    }

    /// <summary>تعریف دوربین پلاک‌خوان (فاز ۴۱) — SN خودکار تولید می‌شود؛ شرکت از گیت می‌آید.</summary>
    [HttpPost]
    [HasPerm("parking.cameras.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCamera(string name, string gateId, string brand,
        string? model, string? ip, string? location, string direction, int confidenceThreshold,
        string? streamUrl, string? serial)
    {
        var gate = await Db.ParkingGates.Include(g => g.Lot).FirstOrDefaultAsync(g => g.Id == gateId);
        if (gate is null) return Fail("گیت انتخاب‌شده یافت نشد");
        name = (name ?? "").Trim();
        if (name.Length < 2) return Fail("نام دوربین را وارد کنید");
        if (await Db.AnprCameras.AnyAsync(c => c.Name == name && c.CompanyId == gate.CompanyId))
            return Fail("دوربین با همین نام در این شرکت ثبت شده است");
        var sn = (serial ?? "").Trim().ToUpperInvariant();
        if (sn.Length == 0)
            sn = $"ANPR-{gate.Code}-{Random.Shared.Next(1000, 9999)}";
        else if (await Db.AnprCameras.AnyAsync(c => c.Serial == sn))
            return Fail("شماره سریال تکراری است");
        var cam = new AnprCamera
        {
            Name = name, GateId = gate.Id, CompanyId = gate.CompanyId, Brand = brand,
            Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim(),
            Ip = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Direction = direction,
            ConfidenceThreshold = Math.Clamp(confidenceThreshold, 0, 100),
            StreamUrl = string.IsNullOrWhiteSpace(streamUrl) ? null : streamUrl.Trim(),
            Serial = sn,
        };
        Db.AnprCameras.Add(cam);
        await Db.SaveChangesAsync();
        await Audit("create", "anpr_camera", cam.Id, new { cam.Name, cam.Serial, gate = gate.Code });
        return Done($"دوربین پلاک‌خوان «{cam.Name}» روی گیت «{gate.Name}» ثبت شد — SN: {cam.Serial}");
    }

    // ============================================================
    // کنش‌ها
    // ============================================================
    [HttpPost]
    [HasPerm("parking.lots.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLot(string id)
    {
        var lot = await Db.ParkingLots.FindAsync(id);
        if (lot is null) return Fail("پارکینگ یافت نشد");
        lot.Active = !lot.Active;
        await Db.SaveChangesAsync();
        await Audit("update", "parking_lot", lot.Id, new { lot.Active });
        return Done(lot.Active ? $"پارکینگ «{lot.Name}» فعال شد" : $"پارکینگ «{lot.Name}» غیرفعال شد");
    }

    [HttpPost]
    [HasPerm("parking.lots.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleGate(string id)
    {
        var gate = await Db.ParkingGates.FindAsync(id);
        if (gate is null) return Fail("گیت یافت نشد");
        gate.Status = gate.Status == "فعال" ? "غیرفعال" : "فعال";
        await Db.SaveChangesAsync();
        await Audit("update", "parking_gate", gate.Id, new { gate.Status });
        return Done($"گیت «{gate.Name}» {gate.Status} شد");
    }

    /// <summary>تغییر حالت راهبند: نرمال | باز دائم (تخلیه/سرویس) | قفل دائم (مسدود کامل).</summary>
    [HttpPost]
    [HasPerm("parking.barrier")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetGateOpenState(string id, string state)
    {
        if (state is not ("نرمال" or "باز دائم" or "قفل دائم")) return Fail("حالت نامعتبر است");
        var gate = await Db.ParkingGates.Include(g => g.Lot).FirstOrDefaultAsync(g => g.Id == id);
        if (gate is null) return Fail("گیت یافت نشد");
        gate.OpenState = state;
        await Db.SaveChangesAsync();
        await Audit("update", "parking_gate", gate.Id, new { gate.OpenState });
        var msg = state switch
        {
            "باز دائم" => $"راهبند «{gate.Name}» باز دائم شد — عبور آزاد",
            "قفل دائم" => $"راهبند «{gate.Name}» قفل دائم شد — مسدود کامل",
            _ => $"راهبند «{gate.Name}» به حالت نرمال برگشت",
        };
        // باز دائم یک رخداد امنیتی است (عبور آزاد) — هشدار ثبت می‌شود؛ قفل دائم اقدام امنیتی است
        if (state == "باز دائم")
            Base.SecurityAlerts.Add(new SecurityAlert
            {
                Type = "تخلیه اضطراری",
                Severity = "بالا",
                Message = $"راهبند پارکینگ «{gate.Name}» ({gate.Lot?.Name}) به حالت «{state}» رفت — عبور آزاد",
                Source = "کنترل پارکینگ", RefId = gate.Id,
            });
        await Base.SaveChangesAsync();
        return Done(msg);
    }

    [HttpPost]
    [HasPerm("parking.vehicles.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleVehicle(string id)
    {
        var vehicle = await Db.Vehicles.FindAsync(id);
        if (vehicle is null) return Fail("خودرو یافت نشد");
        vehicle.Active = !vehicle.Active;
        vehicle.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("update", "vehicle", vehicle.Id, new { vehicle.Active });
        return Done(vehicle.Active
            ? $"خودرو «{vehicle.PlateNo}» فعال شد"
            : $"خودرو «{vehicle.PlateNo}» غیرفعال شد — عبور آن رد می‌شود");
    }

    [HttpPost]
    [HasPerm("parking.vehicles.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePermit(string id)
    {
        var permit = await Db.ParkingPermits.Include(p => p.Vehicle).Include(p => p.Lot)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (permit is null) return Fail("مجوز یافت نشد");
        permit.Status = permit.Status == "فعال" ? "لغوشده" : "فعال";
        permit.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("update", "parking_permit", permit.Id, new { permit.Status });
        return Done(permit.Status == "فعال"
            ? $"مجوز پلاک «{permit.Vehicle?.PlateNo}» دوباره فعال شد"
            : $"مجوز پلاک «{permit.Vehicle?.PlateNo}» لغو شد");
    }

    // ============================================================
    // دوربین‌های پلاک‌خوان (فاز ۴۱) — هم‌الگوی کنش‌های دستگاه‌های تردد
    // ============================================================
    [HttpPost]
    [HasPerm("parking.cameras.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCamera(string id)
    {
        var cam = await Db.AnprCameras.FindAsync(id);
        if (cam is null) return Fail("دوربین یافت نشد");
        cam.Active = !cam.Active;
        if (!cam.Active) cam.Mode = "آفلاین";
        await Db.SaveChangesAsync();
        await Audit("update", "anpr_camera", cam.Id, new { cam.Active });
        return Done(cam.Active
            ? $"دوربین «{cam.Name}» فعال شد — خوانش پلاک دوباره پذیرفته می‌شود"
            : $"دوربین «{cam.Name}» غیرفعال شد — خوانش پلاک نادیده گرفته می‌شود");
    }

    /// <summary>صف‌کردن دستور ping برای دوربین — پل/دوربین در چرخش بعدی تحویل می‌گیرد.</summary>
    [HttpPost]
    [HasPerm("parking.cameras.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PingCamera(string id)
    {
        var cam = await Db.AnprCameras.FindAsync(id);
        if (cam is null) return Fail("دوربین یافت نشد");
        await ParkingOps.QueueCommandAsync(Db, cam.Id, "ping", null, Me.Get());
        await Audit("ping", "anpr_camera", cam.Id, null);
        var msg = $"دستور ضربان برای «{cam.Name}» صف شد — در اولین اتصال دوربین تحویل می‌گیرد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Parking?tab=cameras");
    }

    /// <summary>ضربان شبیه‌سازی‌شده — مخصوص محیط دمو/تست (همان الگوی SimHeartbeat دستگاه‌ها).</summary>
    [HttpPost]
    [HasPerm("devices.simulate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimCameraHeartbeat(string id)
    {
        var cam = await Db.AnprCameras.FindAsync(id);
        if (cam is null) return Fail("دوربین یافت نشد");
        await ParkingOps.MarkSeenAsync(Db, Base, cam);
        await Audit("sim_heartbeat", "anpr_camera", cam.Id, null);
        var sec = Math.Max(3 * (cam.HeartbeatInterval == 0 ? 60 : cam.HeartbeatInterval), 180);
        var msg = $"ضربان شبیه‌سازی شد — «{cam.Name}» آنلاین در نظر گرفته می‌شود (تا {Jalali.Num(sec)} ثانیه)";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Parking?tab=cameras");
    }

    /// <summary>چرخش توکن webhook دوربین — پل‌های متصل باید توکن تازه بگیرند.</summary>
    [HttpPost]
    [HasPerm("parking.cameras.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateCameraToken(string id)
    {
        var cam = await Db.AnprCameras.FindAsync(id);
        if (cam is null) return Fail("دوربین یافت نشد");
        cam.WebhookToken = Guid.NewGuid().ToString("N");
        await Db.SaveChangesAsync();
        await Audit("rotate_secret", "anpr_camera", cam.Id, null);
        var msg = $"توکن اتصال «{cam.Name}» چرخش شد — توکن قبلی باطل شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Parking?tab=cameras");
    }

    /// <summary>بازکردن راهبند از راه دور — دستور در صف دوربین قرار می‌گیرد (فقط گیت‌های پلاک‌خوان).</summary>
    [HttpPost]
    [HasPerm("parking.barrier")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenBarrier(string id)
    {
        var cam = await Db.AnprCameras.Include(c => c.Gate).FirstOrDefaultAsync(c => c.Id == id);
        if (cam is null) return Fail("دوربین یافت نشد");
        if (cam.Gate is null) return Fail("گیت دوربین یافت نشد");
        await ParkingOps.QueueCommandAsync(Db, cam.Id, "open_barrier",
            new { seconds = 10, gate = cam.Gate.Code }, Me.Get());
        await Audit("open_barrier", "anpr_camera", cam.Id, new { gate = cam.Gate.Code });
        var msg = $"دستور بازکردن راهبند «{cam.Gate.Name}» برای دوربین «{cam.Name}» صف شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Parking?tab=cameras");
    }

    // ============================================================
    // شبیه‌ساز پلاک‌خوان — موتور ارزیابی مجوز پارکینگ
    // (گیت فعال/قفل + هم‌خوانی جهت + مجوز معتبر + آنتی‌پس‌بک + ظرفیت)
    // ============================================================
    [HttpGet]
    [HasPerm("parking.scan")]
    public async Task<IActionResult> ScanForm()
    {
        ViewBag.Gates = await Db.ParkingGates.Include(g => g.Lot)
            .Where(g => g.Status == "فعال").OrderBy(g => g.Code).ToListAsync();
        ViewBag.Cameras = await Db.AnprCameras.Include(c => c.Gate)
            .Where(c => c.Active).OrderBy(c => c.Name).ToListAsync();
        return PartialView("_ScanForm");
    }

    [HttpPost]
    [HasPerm("parking.scan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(string plate, string gateId, string direction,
        string method = "پلاک‌خوان", string? cameraId = null)
    {
        var gate = await Db.ParkingGates.Include(g => g.Lot).FirstOrDefaultAsync(g => g.Id == gateId);
        if (gate is null) return Fail("گیت یافت نشد");
        var plateText = (plate ?? "").Trim();
        if (plateText.Length < 4) return Fail("پلاک معتبر نیست — مثال: 12ع471 ایران 36");

        // انتخاب دوربین (اختیاری) — اگر دوربین انتخاب شود، ثبت همان مسیر خودکار دوربین است
        AnprCamera? cam = null;
        int? confidence = null;
        if (method == "پلاک‌خوان" && !string.IsNullOrEmpty(cameraId))
        {
            cam = await Db.AnprCameras.FirstOrDefaultAsync(c => c.Id == cameraId && c.Active);
            if (cam is null) return Fail("دوربین انتخاب‌شده یافت نشد یا غیرفعال است");
            if (cam.GateId != gate.Id) return Fail("دوربین انتخابی به این گیت متصل نیست");
            var rnd = Random.Shared.Next(cam.ConfidenceThreshold, 100);
            confidence = Math.Max(rnd, cam.ConfidenceThreshold);
        }

        // موتور ارزیابی مشترک — همان مسیر پل دوربین‌های پلاک‌خوان (فاز ۴۱)
        var outcome = await ParkingOps.TryPassAsync(Db, Base, gate, plateText, direction, method,
            cameraId: cam?.Id, confidence: confidence, plateKey: PlateFmt.CanonicalKey(plateText));

        if (outcome.Allowed)
        {
            await Audit("scan", "parking_event", outcome.Ev.Id, new
            {
                plate = plateText, gate.Code, allowed = true,
                owner = outcome.Vehicle?.Employee?.FullName, camera = cam?.Name,
            });
            var owner = outcome.Vehicle?.Employee is null ? "" : HrT.F(" ({0})", outcome.Vehicle.Employee.FullName);
            var via = cam is null ? "" : HrT.F(" — دوربین «{0}» (اطمینان {1}٪)", cam.Name, HrT.Num(confidence ?? 100));
            return ToastDone(true, HrT.F("{0} پلاک «{1}»{2} ثبت شد — راهبند باز شد{3}", HrT.T(direction), plateText, owner, via), "/Parking?tab=events");
        }

        await Audit("scan", "parking_event", outcome.Ev.Id,
            new { plate = plateText, gate.Code, allowed = false, reason = outcome.Reason });
        return ToastDone(false, HrT.F("{0}: {1}", HrT.T(outcome.Reason), plateText), "/Parking?tab=events");
    }

    // ---------- پایان مشترک ----------
    private IActionResult Fail(string msg)
    {
        ToastErr(msg);
        if (Ajax) return AjaxResult(false, msg);
        return Redirect("/Parking");
    }

    private IActionResult Done(string msg)
    {
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Parking");
    }

    // ======================================================================
    // فاز ۱۳۳ (مورد ۴ ROADMAP-ADDITIONS) — گزارش تردد وسایل نقلیه
    // سابقهٔ کامل عبور از گیت‌ها با فیلتر بازه/پارکینگ/جهت/نتیجه/پلاک یا دارنده.
    // اسکوپ رکوردی از شرکتِ گیتِ عبور (رویداد خودش CompanyId ندارد).
    // ======================================================================

    [HttpGet]
    [HasPerm("parking.view")]
    public async Task<IActionResult> Events(DateTime? from, DateTime? to, string? lot, string? dir, string? result, string? q)
    {
        var scope = await this.ScopeAsync();
        var f = (from ?? DateTime.Today.AddDays(-6)).Date;
        var t = ((to ?? DateTime.Today).Date).AddDays(1);

        var evQ = Db.ParkingEvents.AsNoTracking()
            .Include(e => e.Gate).ThenInclude(g => g!.Lot)
            .Include(e => e.Vehicle).ThenInclude(v => v!.Employee)
            .Where(e => e.Timestamp >= f && e.Timestamp < t)
            .WhereCoScope(scope, e => e.Gate!.CompanyId);
        if (!string.IsNullOrWhiteSpace(lot)) evQ = evQ.Where(e => e.Gate!.LotId == lot);
        if (!string.IsNullOrWhiteSpace(dir)) evQ = evQ.Where(e => e.Direction == dir);
        if (!string.IsNullOrWhiteSpace(result)) evQ = evQ.Where(e => e.Result == result);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            evQ = evQ.Where(e => e.PlateText.Contains(term)
                || (e.Vehicle != null && (e.Vehicle.PlateNo.Contains(term)
                    || e.Vehicle.Employee.FullName.Contains(term))));
        }

        // سقف ۵۰۰ ردیف برای پاسخ‌خویی صفحه — بازهٔ دقیق‌تر با فیلترها انتخاب شود
        var events = await evQ.OrderByDescending(e => e.Timestamp).Take(500).ToListAsync();
        ViewBag.In = events.Count(e => e.Direction == "ورود");
        ViewBag.Out = events.Count(e => e.Direction == "خروج");
        ViewBag.Deny = events.Count(e => e.Result == "غیرمجاز");
        ViewBag.Total = events.Count;
        ViewBag.Lots = await Db.ParkingLots.AsNoTracking()
            .WhereCoScope(scope, l => l.CompanyId).OrderBy(l => l.Name).ToListAsync();
        ViewBag.F = f; ViewBag.T = t;
        ViewBag.Lot = lot; ViewBag.Dir = dir; ViewBag.Result = result; ViewBag.Q = q;
        return View(events);
    }

    // ======================================================================
    // فاز ۱۳۳ (مورد ۵ ROADMAP-ADDITIONS) — اشغال ظرفیت پارکینگ
    // «داخل» = آخرین رویداد مجاز هر پلاک (۳ روز اخیر) ورود باشد؛ ردِ امروز
    // به‌دلیل «ظرفیت تکمیل» هم به تفکیک پارکینگ شمرده می‌شود.
    // ======================================================================

    [HttpGet]
    [HasPerm("parking.view")]
    public async Task<IActionResult> Occupancy()
    {
        var scope = await this.ScopeAsync();
        var lots = await Db.ParkingLots.AsNoTracking()
            .Include(l => l.Company).Include(l => l.Gates)
            .WhereCoScope(scope, l => l.CompanyId).OrderBy(l => l.Name).ToListAsync();

        var gateLot = lots.SelectMany(l => l.Gates).ToDictionary(g => g.Id, g => g.LotId);

        var since = DateTime.Today.AddDays(-3);
        var evs = await Db.ParkingEvents.AsNoTracking()
            .Where(e => e.Timestamp >= since && e.PlateKey != null)
            .Select(e => new { e.PlateKey, e.Timestamp, e.Direction, e.Result, e.Reason, e.GateId })
            .ToListAsync();

        // آخرین رویداد مجاز هر پلاک (یک پاس زمان‌مرتب) → جهت جاری + ردِ تکمیل‌ظرفیت امروز
        var lastEvent = new Dictionary<string, (string Dir, string LotId)>();
        foreach (var e in evs.Where(e => e.Result == "مجاز" && e.GateId != null && gateLot.ContainsKey(e.GateId))
                    .OrderBy(e => e.Timestamp))
            lastEvent[e.PlateKey!] = (e.Direction, gateLot[e.GateId!]);

        var today = DateTime.Today;
        var deniedToday = evs.Where(e => e.Result == "غیرمجاز" && e.Reason == "ظرفیت تکمیل"
                && e.Timestamp >= today && e.GateId != null && gateLot.ContainsKey(e.GateId))
            .GroupBy(e => gateLot[e.GateId!]).ToDictionary(g => g.Key, g => g.Count());

        var permits = await Db.ParkingPermits.AsNoTracking()
            .Where(p => p.Status == "فعال").Select(p => new { p.LotId })
            .ToListAsync();
        var permitCount = permits.GroupBy(p => p.LotId).ToDictionary(g => g.Key, g => g.Count());

        var insideByLot = lastEvent.Values.Where(v => v.Dir == "ورود")
            .GroupBy(v => v.LotId).ToDictionary(g => g.Key, g => g.Count());

        var rows = lots.Select(l =>
        {
            var inside = insideByLot.TryGetValue(l.Id, out var n) ? n : 0;
            var pa = permitCount.TryGetValue(l.Id, out var p) ? p : 0;
            var fill = l.Capacity > 0 ? Math.Round(inside * 100m / l.Capacity, 0) : 0m;
            return new ParkingOccupancyRow(
                l.Name, l.Company?.Name ?? "-", l.Capacity, pa,
                inside, deniedToday.TryGetValue(l.Id, out var d) ? d : 0, fill);
        }).ToList();

        ViewBag.TotalCap = rows.Sum(r => r.Capacity);
        ViewBag.TotalInside = rows.Sum(r => r.Inside);
        ViewBag.TotalPermits = rows.Sum(r => r.PermitsActive);
        ViewBag.TotalFill = ViewBag.TotalCap > 0 ? Math.Round(ViewBag.TotalInside * 100m / ViewBag.TotalCap, 0) : 0m;
        return View(rows);
    }

    // ======================================================================
    // فاز ۱۳۴ (مورد ۴ ROADMAP-ADDITIONS — تکمیل تحلیلی) — تحلیل اشغال پارکینگ
    // منحنی ساعتی از جفت‌سازی نشست‌ها (ورود مجاز → خروج مجاز بعدی همان پلاک)،
    // پیک اشغال، میانگین مدت توقف نشست‌های کامل‌شده.
    // ======================================================================

    [HttpGet]
    [HasPerm("parking.view")]
    public async Task<IActionResult> Analysis(DateTime? date, string? lot)
    {
        var scope = await this.ScopeAsync();
        var day = (date ?? DateTime.Today).Date;
        var lots = await Db.ParkingLots.AsNoTracking()
            .Include(l => l.Company).Include(l => l.Gates)
            .WhereCoScope(scope, l => l.CompanyId).OrderBy(l => l.Name).ToListAsync();

        var lotIds = string.IsNullOrWhiteSpace(lot)
            ? lots.Select(l => l.Id).ToList()
            : lots.Where(l => l.Id == lot).Select(l => l.Id).ToList();
        var gateLot = lots.SelectMany(l => l.Gates).ToDictionary(g => g.Id, g => g.LotId);

        var raw = await Db.ParkingEvents.AsNoTracking()
            .Where(e => e.Timestamp >= day && e.Timestamp < day.AddDays(1) && e.PlateKey != null)
            .Select(e => new { e.PlateKey, e.Timestamp, e.Direction, e.Result, e.GateId })
            .ToListAsync();
        var evs = raw.Where(e => e.GateId != null && gateLot.TryGetValue(e.GateId, out var gl) && lotIds.Contains(gl)).ToList();

        // نشست‌ها: هر ورود مجاز تا خروج مجاز بعدی همان پلاک؛ بدون خروج = هنوز داخل (تا پایان روز)
        var open = new Dictionary<string, DateTime>();
        var sessions = new List<(DateTime In, DateTime Out)>();
        foreach (var e in evs.Where(e => e.Result == "مجاز").OrderBy(e => e.Timestamp))
        {
            if (e.Direction == "ورود") open[e.PlateKey!] = e.Timestamp;
            else if (open.Remove(e.PlateKey!, out var tin)) sessions.Add((tin, e.Timestamp));
        }
        var stillInside = open.Values.Count;
        foreach (var tin in open.Values) sessions.Add((tin, day.AddHours(24)));

        var dwell = sessions.Where(s => s.Out < day.AddDays(1))
            .Select(s => (int)(s.Out - s.In).TotalMinutes).ToList();
        var hours = new List<ParkingHourRow>();
        for (var h = 0; h < 24; h++)
            hours.Add(new ParkingHourRow(h,
                sessions.Count(s => s.In < day.AddHours(h + 1) && s.Out > day.AddHours(h)),
                evs.Count(e => e.Direction == "ورود" && e.Timestamp.Hour == h),
                evs.Count(e => e.Direction == "خروج" && e.Timestamp.Hour == h)));
        var peak = hours.OrderByDescending(x => x.Inside).First();

        ViewBag.PeakHour = peak.Hour;
        ViewBag.PeakInside = peak.Inside;
        ViewBag.AvgDwell = dwell.Count > 0 ? (int)dwell.Average() : 0;
        ViewBag.MaxDwell = dwell.Count > 0 ? dwell.Max() : 0;
        ViewBag.ClosedSessions = dwell.Count;
        ViewBag.StillInside = stillInside;
        ViewBag.TotalEvents = evs.Count;
        ViewBag.Lots = lots;
        ViewBag.Day = day;
        ViewBag.Lot = lot;
        return View(hours);
    }

    // ======================================================================
    // فاز ۱۳۴ (مورد ۵ ROADMAP-ADDITIONS) — داشبورد کیفیت ANPR و دلایل رد شدن
    // تفکیک دلایل رد، توزیع کانهای اطمینان پلاک‌خوان و آمار هر دوربین.
    // ======================================================================

    [HttpGet]
    [HasPerm("parking.view")]
    public async Task<IActionResult> Quality(int days = 7)
    {
        var scope = await this.ScopeAsync();
        if (days is < 1 or > 90) days = 7;
        var since = DateTime.Now.AddDays(-days);

        var evs = await Db.ParkingEvents.AsNoTracking()
            .Include(e => e.Gate)
            .Where(e => e.Timestamp >= since)
            .WhereCoScope(scope, e => e.Gate!.CompanyId)
            .Select(e => new { e.Method, e.Result, e.Reason, e.Confidence, e.Timestamp, GateName = e.Gate != null ? e.Gate.Name : "" })
            .ToListAsync();

        ViewBag.Reasons = evs.Where(e => e.Result == "غیرمجاز")
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Reason) ? "نامشخص" : e.Reason)
            .OrderByDescending(g => g.Count())
            .Select(g => new { Reason = g.Key, Count = g.Count() }).ToList();

        ViewBag.ConfBuckets = evs.Where(e => e.Method == "پلاک‌خوان" && e.Confidence != null)
            .GroupBy(e => e.Confidence switch
            {
                100 => "۱۰۰ — قطعی",
                >= 90 => "۹۰ تا ۹۹ — بالا",
                >= 70 => "۷۰ تا ۸۹ — متوسط",
                _ => "زیر ۷۰ — پایین"
            })
            .OrderBy(g => g.Key)
            .Select(g => new { Bucket = g.Key, Count = g.Count() }).ToList();

        ViewBag.Cameras = evs.Where(e => e.Method == "پلاک‌خوان")
            .GroupBy(e => string.IsNullOrWhiteSpace(e.GateName) ? "بدون گیت" : e.GateName)
            .Select(g => new AnprCameraStat(
                g.Key, g.Count(), g.Count(x => x.Result == "مجاز"),
                g.Count() > 0 ? Math.Round(g.Count(x => x.Result == "مجاز") * 100m / g.Count(), 0) : 0m,
                g.Count(x => x.Confidence != null) > 0
                    ? Math.Round((decimal)g.Where(x => x.Confidence != null).Average(x => x.Confidence!.Value), 0) : 0m))
            .OrderByDescending(c => c.Total).ToList();

        var plateReads = evs.Where(e => e.Method == "پلاک‌خوان" && e.Confidence != null).ToList();
        ViewBag.Total = evs.Count;
        ViewBag.OkPct = evs.Count > 0 ? Math.Round(evs.Count(e => e.Result == "مجاز") * 100m / evs.Count, 0) : 0m;
        ViewBag.AvgConf = plateReads.Count > 0 ? Math.Round((decimal)plateReads.Average(e => e.Confidence!.Value), 0) : 0m;
        ViewBag.Deny = evs.Count(e => e.Result == "غیرمجاز");
        ViewBag.Days = days;
        return View();
    }

    // ======================================================================
    // فاز ۱۳۵ (مورد ۱۵ ROADMAP-ADDITIONS) — صورتحساب ماهانهٔ پارکینگ
    // حق ماهانهٔ هر خودروی دارای مجوز فعال روی پارکینگ‌های با MonthlyFee > 0
    // در بازهٔ ماه شمسی. همین مبلغ در موتور حقوق به‌عنوان متغیر «پارکینگ_مبلغ»
    // در دسترس قلم‌هاست (کسور با فرمول پارکینگ_مبلغ) — اتصال به حقوق‌ودستمزد.
    // ======================================================================

    [HttpGet]
    [HasPerm("parking.view")]
    public async Task<IActionResult> Billing(int? jy, int? jm)
    {
        var scope = await this.ScopeAsync();
        var (jy2, jm2, _) = Jalali.ToJalali(DateTime.Now);
        var y = jy ?? jy2;
        var m = Math.Clamp(jm ?? jm2, 1, 12);
        var (start, end) = Jalali.MonthRange(y, m);

        var lotsQ = Db.ParkingLots.AsNoTracking().Include(l => l.Company)
            .Where(l => l.MonthlyFee > 0);
        lotsQ = lotsQ.WhereCoScope(scope, l => l.CompanyId);
        var lots = await lotsQ.OrderBy(l => l.Name).ToListAsync();

        var permits = await Db.ParkingPermits.AsNoTracking()
            .Include(p => p.Vehicle).ThenInclude(v => v!.Employee)
            .Include(p => p.Vehicle).ThenInclude(v => v!.Company)
            .Where(p => p.Status == "فعال"
                && p.ValidFrom < end && (p.ValidUntil == null || p.ValidUntil >= start))
            .ToListAsync();

        var lotById = lots.ToDictionary(l => l.Id);
        var rows = permits
            .Where(p => lotById.ContainsKey(p.LotId))
            .Select(p => new ParkingBillingRow(
                p.Vehicle?.Employee?.PersonnelCode ?? "—",
                p.Vehicle?.Employee != null ? $"{p.Vehicle.Employee.FirstName} {p.Vehicle.Employee.LastName}".Trim() : "—",
                p.Vehicle?.Company?.Name ?? lotById[p.LotId].Company?.Name ?? "—",
                lotById[p.LotId].Name,
                p.Vehicle?.PlateNo ?? "—",
                p.Type,
                lotById[p.LotId].MonthlyFee))
            .OrderBy(r => r.Company).ThenBy(r => r.PersonnelCode)
            .ToList();

        ViewBag.Total = rows.Sum(r => r.Fee);
        ViewBag.Count = rows.Count;
        ViewBag.Jy = y;
        ViewBag.Jm = m;
        return View(rows);
    }
}
