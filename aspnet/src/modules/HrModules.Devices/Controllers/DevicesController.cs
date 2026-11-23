using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace HrModules.Devices;

public class DevicesController : MvcBase
{
    public DevicesController(AppDbContext db, HrBaseDbContext baseDb, ICurrentUser me) : base(db, me) => Base = baseDb;

    private readonly HrBaseDbContext Base;
    // فاز ۶۷ — گیت‌وی حضور فقط با لایهٔ دیتای ماژول حضور ثبت می‌شود؛ بدون آن null
    private IAttendanceGateway? Att => HttpContext.RequestServices.GetService<IAttendanceGateway>();

    // فاز ۶۷ — شمارش گیت‌های هر دستگاه ∈ ماژول کنترل تردد؛ بدون لایهٔ دیتای آن خالی
    /// <summary>فاز ۶۸ — شمارش گیت‌های هر دستگاه ∈ پلاگین کنترل تردد (قرارداد IAccessGateway).</summary>
    private Task<Dictionary<string, int>> GateCountsSafeAsync(AppDbContext db) =>
        HttpContext.RequestServices.GetRequiredService<Platform.Contracts.IAccessGateway>().GateCountsByDeviceAsync();

    [HttpGet]
    [HasPerm("devices.view")]
    public async Task<IActionResult> Index(string? co)
    {
        await DeviceOps.SweepOfflineAsync(Db);
        // فاز ۷۴ — دامنهٔ رکوردی دستگاه‌ها: فقط دستگاه‌های شرکت‌های مجاز
        var scope = await this.ScopeAsync();
        var devices = Db.Set<Device>()
            .Include(d => d.Company)
            .AsQueryable();
        if (!string.IsNullOrEmpty(co)) devices = devices.Where(d => d.CompanyId == co);
        devices = devices.WhereCoScope(scope, d => d.CompanyId);
        var list = await devices.OrderBy(d => d.Name).ToListAsync();
        // فاز ۶۱ — navigation پایه→ماژول (Device.Gates) حذف شد؛ شمارش گیت‌ها با FK DeviceId
        // فاز ۶۷ — شمارش گیت‌ها فقط با لایهٔ دیتای ماژول کنترل تردد
        ViewBag.GateCounts = await GateCountsSafeAsync(Db);
        var pending = await Db.Set<DeviceCommand>()
            .Where(c => c.Status == "pending" || c.Status == "delivered")
            .GroupBy(c => c.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeviceId, x => x.Count);
        var commands = await Db.Set<DeviceCommand>()
            .Include(c => c.Device)
            .OrderByDescending(c => c.CreatedAt)
            .Take(50)
            .ToListAsync();

        ViewBag.Pending = pending;
        ViewBag.Commands = commands;
        // فاز ۱۱۴ — لیست کامل پرسنل حذف شد؛ انتخاب با جستجوی زندهٔ /Ui/EmpSearch (مقیاس ده‌ها هزار نفر)
        ViewBag.Online = list.Count(DeviceOps.IsOnline);
        ViewBag.Buffer = await DeviceOps.OfflineBufferCountsAsync(Db);   // حافظه آفلاین (فاز ۳۰)
        ViewBag.Companies = (await Db.Companies.OrderBy(c => c.Code).ToListAsync())
            .Where(c => scope.All || scope.Companies.Count == 0 || scope.Companies.Contains(c.Id)).ToList();
        ViewBag.Co = co;
        return View(list);
    }

    /// <summary>صف دستور برای دستگاه — اجرا توسط سرویس ویندوزی C# (HrDeviceGateway).</summary>
    [HttpPost]
    [HasPerm("devices.commands")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Command(string deviceId, string type, int seconds = 5)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();
        object? payload = type switch
        {
            "unlock" => new { seconds },
            "sync_time" => new { datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
            _ => null,
        };
        var cmd = await DeviceOps.QueueCommandAsync(Db, deviceId, type, payload, Sess);
        await Audit("device_command", "device", deviceId, new { type, cmdId = cmd.Id });
        var msg = $"دستور «{DeviceOps.CommandLabels.GetValueOrDefault(type, type)}» در صف {device.Name} ثبت شد — تحویل با سرویس گیت‌وی";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg, new { cmdId = cmd.Id });
        return Redirect("/Devices");
    }

    /// <summary>
    /// وضعیت زنده دستگاه‌ها + لاگ دستورات — منبع poll AJAX فرانت (هر چند ثانیه).
    /// فرانت با مقایسه وضعیت دستورها، رسیدن نتیجه (تحویل/اجرا/شکست) را بهست اطلاع می‌دهد.
    /// </summary>
    [HttpGet("/Devices/Live")]
    [HasPerm("devices.view")]
    public async Task<IActionResult> Live()
    {
        await DeviceOps.SweepOfflineAsync(Db);
        var devices = await Db.Set<Device>().OrderBy(d => d.Name).ToListAsync();
        var gateCounts = await GateCountsSafeAsync(Db);
        var pending = await Db.Set<DeviceCommand>()
            .Where(c => c.Status == "pending" || c.Status == "delivered")
            .GroupBy(c => c.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeviceId, x => x.Count);
        var commands = await Db.Set<DeviceCommand>()
            .Include(c => c.Device)
            .OrderByDescending(c => c.CreatedAt)
            .Take(50)
            .ToListAsync();
        int online = devices.Count(DeviceOps.IsOnline);
        return Json(new
        {
            serverTime = DateTime.Now,
            chips = new { total = devices.Count, online, offline = devices.Count - online },
            devices = devices.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                active = d.Active,
                online = DeviceOps.IsOnline(d),
                lastSeenText = d.LastSeenAt is null ? "—" : Jalali.FmtLong(d.LastSeenAt.Value),
                firmware = d.Firmware,
                userCount = d.UserCount,
                gates = gateCounts.GetValueOrDefault(d.Id),
                pending = pending.GetValueOrDefault(d.Id, 0),
            }),
            commands = commands.Select(c => new
            {
                id = c.Id,
                device = c.Device?.Name,
                type = c.Type,
                typeLabel = HrT.T(DeviceOps.CommandLabels.GetValueOrDefault(c.Type, c.Type)),
                createdBy = c.CreatedBy,
                status = c.Status,
                statusText = c.Status switch
                {
                    // فاز ۹۰ — چندزبانه (برچسب‌های وضعیت در لاگ زنده)
                    "acked" => HrT.T("اجرا شد"), "failed" => HrT.T("ناموفق"), "delivered" => HrT.T("تحویل شد"), _ => HrT.T("در صف"),
                },
                result = c.Result,
                createdText = Jalali.FmtLong(c.CreatedAt),
            }),
        });
    }

    // ============================================================
    // نمای زنده حراست (فاز ۴۳) — پنل رصد لحظه‌ای ترددهای دستگاه‌ها
    // هر اسکن: نام فرد + کد پرسنلی + دستگاه + جهت به‌صورت نوتیفیکیشن زنده
    // الگوبرداری از نمای زنده پارکینگ: دیوار دستگاه‌ها + تیکر + poll JSON
    // ============================================================

    /// <summary>صفحه نمای زنده حراست — دیوار دستگاه‌ها + تیکر ترددها + استک نوتیفیکیشن.</summary>
    [HttpGet("/Devices/LiveView")]
    [HasPerm("devices.view")]
    public async Task<IActionResult> LiveView(string? co)
    {
        await DeviceOps.SweepOfflineAsync(Db);
        var devices = Db.Set<Device>().Include(d => d.Company).OrderBy(d => d.Name).AsQueryable();
        if (!string.IsNullOrEmpty(co)) devices = devices.Where(d => d.CompanyId == co);
        var list = await devices.ToListAsync();

        ViewBag.DevicesList = list;
        ViewBag.Online = list.Count(DeviceOps.IsOnline);
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        ViewBag.Co = co;
        return View();
    }

    /// <summary>
    /// خوراک JSON نمای زنده حراست — poll هر ۶ ثانیه از فرانت.
    /// شامل چیپ‌های KPI، وضعیت دیوار دستگاه‌ها (شمارش امروز + آخرین تردد هر دستگاه)
    /// و ۸۰ تردد آخر با اطلاعات کامل فرد؛ فرانت با مقایسه Id، ترددهای تازه را
    /// به‌صورت نوتیفیکیشن نمایش می‌دهد.
    /// </summary>
    [HttpGet("/Devices/PunchFeed")]
    [HasPerm("devices.view")]
    public async Task<IActionResult> PunchFeed(string? co)
    {
        await DeviceOps.SweepOfflineAsync(Db);

        var devicesQ = Db.Set<Device>().Include(d => d.Company).AsQueryable();
        if (!string.IsNullOrEmpty(co)) devicesQ = devicesQ.Where(d => d.CompanyId == co);
        var devices = await devicesQ.OrderBy(d => d.Name).ToListAsync();
        var devIds = devices.Select(d => d.Id).ToList();

        // خوراک ترددها — ۸۰ مورد آخر (فیلتر چندشرکتی روی DeviceIdها)
        // فاز ۶۹ — پرسنل ∈ ماژول org (navigation بین‌خانواده‌ای حذف) — نام/واحد/عکس با یک کوئری
        var feedQ = Db.Set<DevicePunchLog>()
            .Include(l => l.Device)
            .AsQueryable();
        if (!string.IsNullOrEmpty(co)) feedQ = feedQ.Where(l => devIds.Contains(l.DeviceId ?? ""));
        var feed = await feedQ.OrderByDescending(l => l.Timestamp).Take(80).ToListAsync();
        var empFeedIds = feed.Where(l => l.EmployeeId is not null).Select(l => l.EmployeeId!).Distinct().ToList();
        var empInfo = await Db.Set<Employee>().Where(e => empFeedIds.Contains(e.Id))
            .Select(e => new { e.Id, e.FullName, e.PhotoPath, Dept = e.Department != null ? e.Department.Name : null, Co = e.Company != null ? e.Company.Name : null })
            .ToDictionaryAsync(e => e.Id);
        (string? FullName, string? Photo, string? Dept, string? Co) EmpOf(string? id) =>
            id is not null && empInfo.TryGetValue(id, out var x) ? (x.FullName, x.PhotoPath, x.Dept, x.Co) : (null, null, null, null);

        // شمارش امروز + آخرین تردد هر دستگاه (از خود خوراک — یک پرس‌وجو)
        var todayStart = DateTime.Now.Date;
        var todayCounts = await Db.Set<DevicePunchLog>()
            .Where(l => l.Timestamp >= todayStart)
            .GroupBy(l => l.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeviceId ?? "", x => x.Count);
        var lastByDev = new Dictionary<string, DevicePunchLog>();
        foreach (var l in feed.Where(l => l.DeviceId is not null))
            lastByDev.TryAdd(l.DeviceId!, l);

        int todayTotal = await Db.Set<DevicePunchLog>()
            .CountAsync(l => l.Timestamp >= todayStart && devIds.Contains(l.DeviceId ?? ""));
        int unknownToday = await Db.Set<DevicePunchLog>()
            .CountAsync(l => l.Timestamp >= todayStart && l.Kind == "ناشناس" && devIds.Contains(l.DeviceId ?? ""));

        static string TimeText(DateTime t) => t.Date == DateTime.Today
            ? Jalali.FmtTime(t)
            : $"{Jalali.FmtFa(t)} — {Jalali.FmtTime(t)}";

        return Json(new
        {
            serverTime = DateTime.Now,
            chips = new
            {
                devices = devices.Count,
                online = devices.Count(DeviceOps.IsOnline),
                todayTotal,
                unknownToday,
                lastText = feed.Count == 0 ? "—" : TimeText(feed[0].Timestamp),
            },
            devices = devices.Select(d =>
            {
                var last = lastByDev.TryGetValue(d.Id, out var x) ? x : null;
                return new
                {
                    id = d.Id,
                    name = d.Name,
                    brand = d.Brand,
                    ip = d.Ip ?? "—",
                    location = d.Location ?? "—",
                    company = d.Company?.Name ?? "—",
                    online = d.Active && DeviceOps.IsOnline(d),
                    active = d.Active,
                    lastSeenText = d.LastSeenAt is null ? "—" : Jalali.FmtLong(d.LastSeenAt.Value),
                    todayCount = todayCounts.GetValueOrDefault(d.Id, 0),
                    last = last is null ? null : new
                    {
                        person = EmpOf(last.EmployeeId).FullName ?? "ناشناس",
                        code = last.Pin,
                        kind = last.Kind,
                        time = TimeText(last.Timestamp),
                        photo = EmpOf(last.EmployeeId).Photo,
                    },
                };
            }),
            punches = feed.Select(l =>
            {
                var em = EmpOf(l.EmployeeId);
                return new
                {
                    id = l.Id,
                    ts = ((DateTimeOffset)l.Timestamp).ToUnixTimeMilliseconds(),
                    time = TimeText(l.Timestamp),
                    person = em.FullName ?? "فرد ناشناس",
                    code = l.Pin,
                    dept = em.Dept,
                    device = l.Device?.Name ?? "دستگاه حذف‌شده",
                    kind = l.Kind,
                    note = l.Note,
                    company = em.Co,
                    photo = em.Photo,
                };
            }),
        });
    }

    /// <summary>انتقال کاربر بین دستگاه‌ها: push_user به مقصدها + delete_user از مبدأها.</summary>
    [HttpPost]
    [HasPerm("devices.transfer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferUser(string employeeId, string[] toDeviceIds, string[] fromDeviceIds)
    {
        var emp = await Db.Set<Employee>().FindAsync(employeeId);
        if (emp is null) return NotFound();
        // پاکسازی: مقادیر خالی فرم (رشته تهی) نباید به‌عنوان شناسه دستگاه صف شوند
        toDeviceIds = (toDeviceIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        fromDeviceIds = (fromDeviceIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        int queued = 0;
        foreach (var d in toDeviceIds)
        {
            await DeviceOps.QueueCommandAsync(Db, d, "push_user",
                new { pin = emp.PersonnelCode, name = emp.FullName, card = 0 }, Sess);
            queued++;
        }
        foreach (var d in fromDeviceIds)
        {
            await DeviceOps.QueueCommandAsync(Db, d, "delete_user",
                new { pin = emp.PersonnelCode }, Sess);
            queued++;
        }
        await Audit("transfer_user", "device", employeeId,
            new { to = toDeviceIds, from = fromDeviceIds, pin = emp.PersonnelCode });
        var msg = $"{queued} دستور انتقال کاربر «{emp.FullName}» (پین {emp.PersonnelCode}) در صف قرار گرفت";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Devices");
    }

    // ---------- حافظه آفلاین + شبیه‌ساز تردد (فاز ۳۰) ----------

    /// <summary>
    /// شبیه‌ساز اسکن روی دستگاه (بدون نیاز به گیت‌وی): دستگاه آنلاین → تردد مستقیم وارد سامانه؛
    /// دستگاه آفلاین → تردد در «حافظه آفلاین دستگاه» صف می‌شود تا با دریافت دسته‌ای وارد شود.
    /// </summary>
    [HttpPost]
    [HasPerm("devices.simulate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimPunch(string deviceId, string pin)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();
        if (string.IsNullOrWhiteSpace(pin))
            return AjaxResult(false, "کد پرسنلی (پین) الزامی است");
        pin = pin.Trim();
        var online = DeviceOps.IsOnline(device);
        if (!online)
        {
            Db.Set<DeviceOfflinePunch>().Add(new DeviceOfflinePunch
            {
                DeviceId = device.Id, Pin = pin, Timestamp = DateTime.Now, Source = "شبیه‌ساز",
            });
            await Db.SaveChangesAsync();
            var bufMsg = $"دستگاه «{device.Name}» آفلاین است — تردد پین {pin} در حافظه داخلی دستگاه صف شد (در انتظار دریافت دسته‌ای)";
            ToastOk(bufMsg);
            return Ajax ? AjaxResult(true, bufMsg) : Redirect("/Devices");
        }
        var att = Att;
        if (att is null)
        {
            const string noAtt = "موتور تردد در این نسخه نصب نیست (ماژول حضور در بسته نیست)";
            ToastErr(noAtt);
            return Ajax ? AjaxResult(false, noAtt) : Redirect("/Devices");
        }
        var r = await DeviceOps.IngestPunchesAsync(Db, att, device.Id, new List<RawPunch> { new(pin, DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss")) });
        if (r.Ok == 0)
        {
            var why = r.UnknownPins.Count > 0 ? HrT.F("پین {0} به پرسنلی تعلق ندارد", pin) : HrT.T("زمان نامعتبر");
            ToastErr(HrT.F("تردد ثبت نشد — {0}", why));
            return Ajax ? AjaxResult(false, HrT.F("تردد ثبت نشد — {0}", why)) : Redirect("/Devices");
        }
        var msg = HrT.F("تردد پین {0} روی دستگاه آنلاین «{1}» مستقیماً وارد سامانه شد", pin, device.Name);
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Devices");
    }

    /// <summary>دریافت دسته‌ای ترددهای آفلاین: کل صف حافظه دستگاه وارد سامانه می‌شود (زمان اصلی حفظ می‌شود).</summary>
    [HttpPost]
    [HasPerm("devices.commands")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FlushOffline(string deviceId)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();
        var queued = await Db.Set<DeviceOfflinePunch>().CountAsync(p => p.DeviceId == device.Id && p.ImportedAt == null);
        if (queued == 0)
        {
            ToastErr("حافظه آفلاین این دستگاه خالی است");
            return Ajax ? AjaxResult(false, "حافظه آفلاین این دستگاه خالی است") : Redirect("/Devices");
        }
        var att = Att;
        if (att is null)
        {
            const string noAtt = "موتور تردد در این نسخه نصب نیست (ماژول حضور در بسته نیست)";
            ToastErr(noAtt);
            return Ajax ? AjaxResult(false, noAtt) : Redirect("/Devices");
        }
        var r = await DeviceOps.FlushOfflineBufferAsync(Db, att, device);
        await Audit("flush_offline", "device", device.Id, new { queued, ok = r.Ok, failed = r.Failed });
        var msg = $"دریافت دسته‌ای: {Jalali.Num(r.Ok)} تردد از حافظه «{device.Name}» وارد سامانه شد" +
                  (r.Failed > 0 ? $" — {Jalali.Num(r.Failed)} ناموفق (پین ناشناس: {string.Join("، ", r.UnknownPins)})" : "");
        if (r.Ok == 0) { ToastErr(msg); return Ajax ? AjaxResult(false, msg) : Redirect("/Devices"); }
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Devices");
    }

    /// <summary>شبیه‌سازی ضربان دستگاه (تست بدون گیت‌وی): LastSeenAt تازه می‌شود و هشدار قطع برطرف می‌گردد.</summary>
    [HttpPost]
    [HasPerm("devices.simulate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimHeartbeat(string deviceId)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();
        await DeviceOps.MarkSeenAsync(Db, device);
        await Audit("sim_heartbeat", "device", device.Id, null);
        var msg = $"ضربان شبیه‌سازی شد — «{device.Name}» آنلاین در نظر گرفته می‌شود (تا {Jalali.Num(Math.Max(3 * (device.PollingInterval == 0 ? 60 : device.PollingInterval), 180))} ثانیه)";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Devices");
    }

    /// <summary>چرخش توکن webhook دستگاه (امنیت).</summary>
    [HttpPost]
    [HasPerm("devices.register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateSecret(string deviceId)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();
        device.WebhookToken = Guid.NewGuid().ToString("N");
        await Db.SaveChangesAsync();
        await Audit("rotate_secret", "device", deviceId, null);
        var msg = $"توکن اتصال «{device.Name}» چرخش شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Devices");
    }

    // ---------- تعریف و ویرایش دستگاه (کاتالوگ DeviceDefaults) ----------

    /// <summary>فرم تعریف دستگاه — فیلدها بر اساس نوع دستگاه/برند پیش‌فرض می‌شوند.</summary>
    [HttpGet]
    [HasPerm("devices.register")]
    public async Task<IActionResult> Create()
    {
        var model = new Device
        {
            Kind = DeviceDefaults.KindKeys[0],
            Brand = "ZKTeco",
            Mode = "آنلاین",
            PollingInterval = DeviceDefaults.Kinds[DeviceDefaults.KindKeys[0]].Polling,
            Active = true,
        };
        ViewBag.SuggestedIp = await DeviceDefaults.NextFreeIpAsync(Db);
        ViewBag.IsEdit = false;
        ViewBag.Companies = await Db.Companies.Where(c => c.Active).OrderBy(c => c.Code).ToListAsync();
        return FormView("Form", model);
    }

    /// <summary>ثبت دستگاه جدید — اعتبارسنجی یکتایی سریال/IP + مقادیر کاتالوگ.</summary>
    [HttpPost]
    [HasPerm("devices.register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string kind, string brand, string serial,
        string? ip, string? location, string purpose, string mode, int pollingInterval,
        string transport = "adms", int? devicePort = null, int? commKey = null, bool active = true,
        string? companyId = null)
    {
        var (err, purpose2, mode2, polling2) = await ValidateDeviceAsync(null, name, kind, brand, serial, ip, purpose, mode, pollingInterval);
        if (err is not null)
        {
            ToastErr(err);
            if (Ajax) return AjaxResult(false, err);
            return Redirect("/Devices/Create");
        }
        var company = await Db.Companies.FindAsync(companyId ?? "")
                      ?? await Db.Companies.Where(c => c.Active).OrderBy(c => c.Code).FirstOrDefaultAsync();
        if (company is null)
        {
            const string m = "ابتدا یک شرکت تعریف کنید (صفحه شرکت‌ها)";
            ToastErr(m);
            if (Ajax) return AjaxResult(false, m);
            return Redirect("/Devices/Create");
        }
        var device = new Device
        {
            Name = name.Trim(), Kind = kind, Brand = brand, Serial = serial.Trim(),
            Ip = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Purpose = purpose2, Mode = mode2,
            PollingInterval = polling2, Active = active,
            Transport = transport == "tcp" ? "tcp" : "adms",
            DevicePort = devicePort is > 0 && devicePort <= 65535 ? devicePort : null,
            CommKey = commKey is >= 0 ? commKey : null,
            CompanyId = company.Id,
        };
        Db.Set<Device>().Add(device);
        await Db.SaveChangesAsync();
        await Audit("create_device", "device", device.Id, new { name = device.Name, kind, brand, serial = device.Serial, ip = device.Ip, transport = device.Transport, company = company.Name });
        await BumpGatewayConfigAsync();   // سرویس گیت‌وی تا ۳۰ ثانیه دیگر دستگاه را می‌شناسد
        var msg = $"دستگاه «{device.Name}» ({device.Kind} {device.Brand}) در شرکت «{company.Name}» ثبت شد — گیت‌وی C# به‌صورت خودکار آن را مدیریت می‌کند";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg, new { redirect = "/Devices" });
        return Redirect("/Devices");
    }

    /// <summary>فرم ویرایش دستگاه.</summary>
    [HttpGet]
    [HasPerm("devices.register")]
    public async Task<IActionResult> Edit(string id)
    {
        var device = await Db.Set<Device>().FindAsync(id);
        if (device is null) return NotFound();
        ViewBag.SuggestedIp = null;
        ViewBag.IsEdit = true;
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        return FormView("Form", device);
    }

    /// <summary>ذخیره ویرایش دستگاه.</summary>
    [HttpPost]
    [HasPerm("devices.register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string name, string kind, string brand, string serial,
        string? ip, string? location, string purpose, string mode, int pollingInterval,
        string transport = "adms", int? devicePort = null, int? commKey = null, bool active = true,
        string? companyId = null)
    {
        var device = await Db.Set<Device>().FindAsync(id);
        if (device is null) return NotFound();
        var (err, purpose2, mode2, polling2) = await ValidateDeviceAsync(device.Id, name, kind, brand, serial, ip, purpose, mode, pollingInterval);
        if (err is not null)
        {
            ToastErr(err);
            if (Ajax) return AjaxResult(false, err);
            return Redirect($"/Devices/Edit/{id}");
        }
        var oldCompany = device.Company?.Name;
        if (!string.IsNullOrWhiteSpace(companyId) && companyId != device.CompanyId)
        {
            var nc = await Db.Companies.FindAsync(companyId);
            if (nc is not null) device.CompanyId = nc.Id;
        }
        var oldKind = device.Kind;
        device.Name = name.Trim();
        device.Kind = kind;
        device.Brand = brand;
        device.Serial = serial.Trim();
        device.Ip = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim();
        device.Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        device.Purpose = purpose2;
        device.Mode = mode2;
        device.PollingInterval = polling2;
        device.Active = active;
        device.Transport = transport == "tcp" ? "tcp" : "adms";
        device.DevicePort = devicePort is > 0 && devicePort <= 65535 ? devicePort : null;
        device.CommKey = commKey is >= 0 ? commKey : null;
        await Db.SaveChangesAsync();
        await Audit("edit_device", "device", device.Id,
            new { name = device.Name, kind, oldKind, brand, serial = device.Serial, ip = device.Ip, transport = device.Transport, oldCompany });
        await BumpGatewayConfigAsync();   // تغییر IP/پورت/فاصله بدون ری‌استارت سرویس اعمال می‌شود
        var msg = $"مشخصات دستگاه «{device.Name}» ذخیره شد — کانفیگ گیت‌وی به‌روز شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg, new { redirect = "/Devices" });
        return Redirect("/Devices");
    }

    /// <summary>توقف/فعال‌سازی دستگاه (حذف نرم — داده‌های تردد حفظ می‌شود).</summary>
    [HttpPost]
    [HasPerm("devices.register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string deviceId)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();
        device.Active = !device.Active;
        await Db.SaveChangesAsync();
        await Audit("toggle_device", "device", deviceId, new { active = device.Active });
        await BumpGatewayConfigAsync();   // poller دستگاه متوقف‌شده حذف و فعال‌شده استارت می‌خورد
        var msg = device.Active
            ? $"دستگاه «{device.Name}» فعال شد — poller گیت‌وی تا ۳۰ ثانیه دیگر استارت می‌خورد"
            : $"دستگاه «{device.Name}» متوقف شد (داده‌های تردد حفظ می‌شود) — poller گیت‌وی متوقف می‌شود";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg, new { active = device.Active });
        return Redirect("/Devices");
    }

    /// <summary>
    /// بالا بردن نسخه کانفیگ گیت‌وی — بعد از هر تغییر کاتالوگ دستگاه.
    /// گیت‌وی با مقایسه نسخه، فهرست دستگاه‌ها/تنظیمات را دوباره می‌گیرد (فاز ۲۱).
    /// </summary>
    private async Task BumpGatewayConfigAsync()
    {
        var s = await GatewayConfig.GetAsync(Db);
        s.ConfigVersion++;
        s.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
    }

    /// <summary>اعتبارسنجی مشترک فرم دستگاه — خطا یا مقادیر نرمال‌شده را برمی‌گرداند.</summary>
    private async Task<(string? Error, string Purpose, string Mode, int Polling)> ValidateDeviceAsync(
        string? selfId, string? name, string? kind, string? brand, string? serial, string? ip,
        string purpose, string mode, int polling)
    {
        if (string.IsNullOrWhiteSpace(name)) return ("نام دستگاه الزامی است", purpose, mode, polling);
        if (!DeviceDefaults.ValidKind(kind))
            return (HrT.F("نوع دستگاه نامعتبر است — انواع مجاز: {0}", string.Join("، ", DeviceDefaults.KindKeys)), purpose, mode, polling);
        if (!DeviceDefaults.ValidBrand(brand))
            return (HrT.F("برند نامعتبر است — برندهای مجاز: {0}", string.Join("، ", DeviceDefaults.BrandKeys)), purpose, mode, polling);
        if (string.IsNullOrWhiteSpace(serial)) return ("سریال دستگاه الزامی است", purpose, mode, polling);
        purpose = purpose == "کنترل تردد" ? "کنترل تردد" : "حضور و غیاب";
        mode = mode == "آفلاین" ? "آفلاین" : "آنلاین";
        polling = Math.Clamp(polling, 10, 1440);
        var serialNorm = serial.Trim();
        if (await Db.Set<Device>().AnyAsync(d => d.Serial == serialNorm && (selfId == null || d.Id != selfId)))
            return ($"سریال «{serialNorm}» قبلاً برای دستگاه دیگری ثبت شده است", purpose, mode, polling);
        if (!string.IsNullOrWhiteSpace(ip) && await Db.Set<Device>().AnyAsync(d => d.Ip == ip.Trim() && (selfId == null || d.Id != selfId)))
            return ($"آدرس IP «{ip.Trim()}» قبلاً برای دستگاه دیگری ثبت شده است", purpose, mode, polling);
        return (null, purpose, mode, polling);
    }
}
