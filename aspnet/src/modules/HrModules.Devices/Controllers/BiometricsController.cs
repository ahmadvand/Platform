using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;   // فاز ۱۱۴ — WhereEmpScope ∈ Platform.Web.Services
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Devices;

/// <summary>
/// داده‌های هویتی (بایومتریک) — دریافت اثر انگشت/چهره/کارت/رمز از دستگاه‌ها،
/// ذخیره پرسنل هر دستگاه در دیتابیس و انتقال قالب‌ها بین دستگاه‌ها.
/// </summary>
public class BiometricsController : MvcBase
{
    public BiometricsController(AppDbContext db, HrBaseDbContext baseDb, ICurrentUser me) : base(db, me) => Base = baseDb;

    private readonly HrBaseDbContext Base;

    [HttpGet]
    [HasPerm("biometrics.view")]
    public async Task<IActionResult> Index()
    {
        var devices = await Db.Set<Device>().OrderBy(d => d.Name).ToListAsync();
        var deviceUsers = await Db.Set<DeviceUser>()
            .Include(du => du.Device)
            .OrderBy(du => du.DeviceId).ThenBy(du => du.Uid)
            .ToListAsync();
        // فاز ۶۹ — پرسنل ∈ ماژول org (navigation بین‌خانواده‌ای حذف) — نام/کد با یک کوئری
        var duEmpIds = deviceUsers.Where(du => du.EmployeeId is not null).Select(du => du.EmployeeId!).Distinct().ToList();
        ViewBag.EmpBriefById = await Db.Set<Employee>().Where(e => duEmpIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => (e.FullName, e.PersonnelCode));
        var devBioCounts = await Db.Set<DeviceBiometric>()
            .GroupBy(b => new { b.DeviceId, b.Pin, b.Type })
            .Select(g => new { g.Key.DeviceId, g.Key.Pin, g.Key.Type, Count = g.Count() })
            .ToListAsync();
        var bioCountMap = devBioCounts
            .GroupBy(x => (x.DeviceId, x.Pin))
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Type, x => x.Count));

        var employees = await Db.Set<Employee>()
            .Where(e => e.Status == "فعال")
            .OrderBy(e => e.PersonnelCode)
            .ToListAsync();
        // قالب‌های مرجع پرسنل ∈ همین ماژول — گروه‌بندی به‌جای navigation حذف‌شده
        var bioByEmp = (await Db.Set<EmployeeBiometric>().ToListAsync())
            .GroupBy(b => b.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        ViewBag.BioByEmp = bioByEmp;

        var commands = await Db.Set<DeviceCommand>()
            .Include(c => c.Device)
            .Where(c => c.Type == "get_users" || c.Type == "get_user_bios" ||
                        c.Type == "push_user_bios" || c.Type == "push_user" || c.Type == "delete_user")
            .OrderByDescending(c => c.CreatedAt)
            .Take(40)
            .ToListAsync();

        ViewBag.Devices = devices;
        ViewBag.BioCountMap = bioCountMap;
        ViewBag.Employees = employees;
        ViewBag.Commands = commands;
        ViewBag.CanManage = Sess?.Has("biometrics.manage") ?? false;
        return View(deviceUsers);
    }

    /// <summary>دریافت فهرست پرسنل از دستگاه (دستور get_users — اجرا با گیت‌وی C#).</summary>
    [HttpPost]
    [HasPerm("biometrics.fetch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FetchUsers(string deviceId)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();
        var cmd = await DeviceOps.QueueCommandAsync(Db, deviceId, "get_users", new { }, Sess);
        await Audit("fetch_users", "device", deviceId, new { cmdId = cmd.Id });
        var msg = $"دستور «دریافت فهرست کاربران» در صف {device.Name} ثبت شد — پس از تحویل گیت‌وی، پرسنل دستگاه در دیتابیس ذخیره می‌شود";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Biometrics");
    }

    /// <summary>دریافت اطلاعات هویتی (اثر انگشت/چهره/کارت/رمز) از دستگاه — برای یک کاربر یا همه.</summary>
    [HttpPost]
    [HasPerm("biometrics.fetch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FetchBios(string deviceId, string? pin)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();

        object payload;
        if (!string.IsNullOrWhiteSpace(pin))
            payload = new { pins = new[] { pin } };
        else
        {
            var pins = await Db.Set<DeviceUser>().Where(du => du.DeviceId == deviceId)
                .Select(du => du.Pin).ToListAsync();
            if (pins.Count == 0)
            {
                var em = $"هنوز پرسنلی از «{device.Name}» دریافت نشده — ابتدا «دریافت پرسنل از دستگاه» را اجرا کنید";
                ToastErr(em);
                if (Ajax) return AjaxResult(false, em);
                return Redirect("/Biometrics");
            }
            payload = new { pins };
        }

        var cmd = await DeviceOps.QueueCommandAsync(Db, deviceId, "get_user_bios", payload, Sess);
        await Audit("fetch_bios", "device", deviceId, new { cmdId = cmd.Id, pin });
        var msg = $"دستور «دریافت اطلاعات هویتی» در صف {device.Name} ثبت شد — قالب‌ها پس از دریافت، در دیتابیس ذخیره و به پرسنل متصل می‌شوند";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Biometrics");
    }

    /// <summary>
    /// انتقال پرسنل (و قالب‌های هویتی مرجع) به دستگاه‌های مقصد — دستور push_user_bios.
    /// کارت و رمز روی رکورد کاربر می‌نشینند؛ اثر انگشت/چهره به‌صورت قالب باینری ارسال می‌شود.
    /// فاز ۱۱۴ — allEmployees=true = همهٔ پرسنل فعالِ در دامنهٔ کاربر (انتخابگر emp-picker
    /// در حالت «همه» به‌جای ارسال ده‌ها هزار شناسه فقط این پرچم را می‌فرستد).
    /// </summary>
    [HttpPost]
    [HasPerm("biometrics.transfer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PushToDevices(string[] employeeIds, string[] toDeviceIds, bool includeBios = true, bool allEmployees = false)
    {
        if (!allEmployees && employeeIds.Length == 0 || toDeviceIds.Length == 0)
        {
            var em = "حداقل یک پرسنل و یک دستگاه مقصد انتخاب کنید";
            ToastErr(em);
            if (Ajax) return AjaxResult(false, em);
            return Redirect("/Biometrics");
        }

        List<Employee> emps;
        if (allEmployees)
        {
            var scope = await this.ScopeAsync();
            emps = await Db.Set<Employee>()
                .Where(e => e.Status == "فعال")
                .WhereEmpScope(scope, e => e.Id)
                .ToListAsync();
            if (emps.Count == 0)
            {
                var em2 = "پرسنلی در دامنهٔ دسترسی شما یافت نشد";
                ToastErr(em2);
                if (Ajax) return AjaxResult(false, em2);
                return Redirect("/Biometrics");
            }
        }
        else
        {
            emps = await Db.Set<Employee>()
                .Where(e => employeeIds.Contains(e.Id))
                .ToListAsync();
        }
        if (emps.Count == 0) return NotFound();
        employeeIds = emps.Select(e => e.Id).ToArray();

        // payload یکسان برای همه مقصدها (پرسنل + قالب‌های مرجع)
        var payload = await BioSync.BuildPushPayloadAsync(Db, emps, includeBios);
        int bioCount = includeBios
            ? await Db.Set<EmployeeBiometric>()
                .CountAsync(b => employeeIds.Contains(b.EmployeeId) && (b.Type == "fp" || b.Type == "face"))
            : 0;
        int queued = 0;
        foreach (var d in toDeviceIds)
        {
            await DeviceOps.QueueCommandAsync(Db, d, "push_user_bios", payload, Sess);
            queued++;
        }
        await Audit("push_bios", "device", string.Join(',', toDeviceIds),
            new { employees = emps.Count, bios = bioCount, targets = toDeviceIds, includeBios });
        var msg = $"{Jalali.Num(queued)} دستور انتقال ({Jalali.Num(emps.Count)} پرسنل + {Jalali.Num(bioCount)} قالب هویتی) در صف دستگاه‌های مقصد ثبت شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Biometrics");
    }

    /// <summary>
    /// انتقال گروهی «دستگاه ← دستگاه» — کاربران انتخابیِ دستگاه مبدأ با تمام
    /// داده‌های هویتی (اثر انگشت/چهره/کارت/رمز) به دستگاه‌های مقصد ارسال می‌شود.
    /// </summary>
    [HttpPost]
    [HasPerm("biometrics.transfer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferDeviceUsers(string sourceDeviceId, string[] pins, string[] toDeviceIds, bool includeBios = true)
    {
        if (string.IsNullOrEmpty(sourceDeviceId) || pins.Length == 0 || toDeviceIds.Length == 0)
        {
            var em = "دستگاه مبدأ، حداقل یک کاربر و حداقل یک دستگاه مقصد را انتخاب کنید";
            ToastErr(em);
            if (Ajax) return AjaxResult(false, em);
            return Redirect("/Biometrics");
        }
        if (toDeviceIds.Contains(sourceDeviceId))
        {
            var em2 = "دستگاه مقصد نباید همان دستگاه مبدأ باشد";
            ToastErr(em2);
            if (Ajax) return AjaxResult(false, em2);
            return Redirect("/Biometrics");
        }

        var source = await Db.Set<Device>().FindAsync(sourceDeviceId);
        if (source is null) return NotFound();

        var pinList = pins.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        var (payload, userCount, bioCount) = await BioSync.BuildDevicePushPayloadAsync(Db, source, pinList, includeBios);
        if (userCount == 0)
        {
            var em = "کاربری با پین انتخابی روی دستگاه مبدأ یافت نشد — ابتدا «دریافت پرسنل از دستگاه» را اجرا کنید";
            ToastErr(em);
            if (Ajax) return AjaxResult(false, em);
            return Redirect("/Biometrics");
        }

        int queued = 0;
        foreach (var d in toDeviceIds)
        {
            await DeviceOps.QueueCommandAsync(Db, d, "push_user_bios", payload, Sess);
            queued++;
        }
        await Audit("transfer_device_to_device", "device", string.Join(',', toDeviceIds),
            new { source = source.Name, pins, users = userCount, bios = bioCount, targets = toDeviceIds, includeBios });
        var msg = $"انتقال دستگاه←دستگاه از «{source.Name}»: {Jalali.Num(userCount)} کاربر + {Jalali.Num(bioCount)} قالب هویتی → {Jalali.Num(queued)} دستور در صف دستگاه‌های مقصد ثبت شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Biometrics");
    }

    /// <summary>حذف کاربر از دستگاه (دستور delete_user) + پاک‌سازی نسخه محلی.</summary>
    [HttpPost]
    [HasPerm("biometrics.fetch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDeviceUser(string deviceUserId)
    {
        var du = await Db.Set<DeviceUser>().Include(x => x.Device)
            .FirstOrDefaultAsync(x => x.Id == deviceUserId);
        if (du is null) return NotFound();

        await DeviceOps.QueueCommandAsync(Db, du.DeviceId, "delete_user", new { pin = du.Pin }, Sess);
        var bios = Db.Set<DeviceBiometric>().Where(b => b.DeviceId == du.DeviceId && b.Pin == du.Pin);
        Db.Set<DeviceBiometric>().RemoveRange(bios);
        var name = du.EmployeeId is null ? du.Name
            : await Db.Set<Employee>().Where(e => e.Id == du.EmployeeId).Select(e => (string?)e.FullName).FirstOrDefaultAsync()
              ?? du.Name;
        Db.Set<DeviceUser>().Remove(du);
        await Db.SaveChangesAsync();
        await Audit("delete_device_user", "device", du.DeviceId, new { pin = du.Pin, name });
        var msg = $"دستور حذف کاربر «{name}» (پین {Jalali.ToPersianDigits(du.Pin)}) از {du.Device?.Name} ثبت شد و نسخه محلی پاک شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Biometrics");
    }

    /// <summary>
    /// شبیه‌ساز دستگاه (نمایشی — برای سندباکس بدون سخت‌افزار):
    /// پاسخ دستگاه به get_users/get_user_bios را از همان مسیر پردازش رسمی (BioSync) اعمال می‌کند.
    /// </summary>
    [HttpPost]
    [HasPerm("devices.simulate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimulateFetch(string deviceId, string mode)
    {
        var device = await Db.Set<Device>().FindAsync(deviceId);
        if (device is null) return NotFound();

        // شبیه‌سازی: کاربران از «حافظه دستگاه»
        var roster = await BioSync.SimulateRosterAsync(Db, device);

        if (mode == "users")
        {
            var r = await BioSync.UpsertUsersAsync(Db, device, roster);
            await AckPendingAsync(device.Id, "get_users",
                $"{Jalali.Num(r.Total)} کاربر از دستگاه خوانده شد (نو: {Jalali.Num(r.Created)}، پیوند: {Jalali.Num(r.Linked)})");
            await Audit("simulate_fetch_users", "device", deviceId,
                new { total = r.Total, created = r.Created, newEmployees = r.NewEmployeeCodes });
            var newMsg = r.NewEmployeeCodes.Count > 0
                ? $" — {Jalali.Num(r.NewEmployeeCodes.Count)} پرسنل جدید در دیتابیس ساخته شد ({string.Join("، ", r.NewEmployeeCodes.Select(Jalali.ToPersianDigits))})"
                : "";
            var msg = $"شبیه‌سازی دریافت پرسنل از {device.Name}: {Jalali.Num(r.Total)} کاربر (جدید روی دستگاه: {Jalali.Num(r.Created)}){newMsg}";
            ToastOk(msg);
            if (Ajax) return AjaxResult(true, msg);
        }
        else
        {
            var bios = BioSync.SimulateBios(roster);
            var r = await BioSync.UpsertBiosAsync(Db, device, bios);
            await AckPendingAsync(device.Id, "get_user_bios",
                $"{Jalali.Num(r.Total)} قالب هویتی برای {Jalali.Num(r.EmployeeIds.Count)} پرسنل دریافت شد");
            await Audit("simulate_fetch_bios", "device", deviceId,
                new { total = r.Total, deviceCreated = r.DeviceCreated, canonicalCreated = r.CanonicalCreated });
            var msg = $"شبیه‌سازی دریافت اطلاعات هویتی از {device.Name}: {Jalali.Num(r.Total)} قالب (روی دستگاه: {Jalali.Num(r.DeviceCreated)} جدید، کپی مرجع: {Jalali.Num(r.CanonicalCreated)})";
            ToastOk(msg);
            if (Ajax) return AjaxResult(true, msg);
        }
        return Redirect("/Biometrics");
    }

    private async Task AckPendingAsync(string deviceId, string type, string result)
    {
        var pending = await Db.Set<DeviceCommand>()
            .Where(c => c.DeviceId == deviceId && c.Type == type && (c.Status == "pending" || c.Status == "delivered"))
            .ToListAsync();
        foreach (var c in pending)
        {
            c.Status = "acked";
            c.AckedAt = DateTime.Now;
            c.Result = result + " (شبیه‌ساز)";
        }
        if (pending.Count > 0) await Db.SaveChangesAsync();
    }
}
