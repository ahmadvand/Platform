using Platform.Web.Data;
using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

// ============================================================
// همگام‌سازی داده‌های هویتی (بایومتریک) بین دستگاه‌ها و دیتابیس
//  — دریافت پرسنل هر دستگاه و ذخیره در سامانه (با ساخت پرسنل جدید)
//  — ذخیره قالب‌های اثر انگشت/چهره/کارت/رمز به‌صورت per-device
//  — کپی مرجع (EmployeeBiometric) به‌عنوان مرکز انتقال بین دستگاه‌ها
// فاز ۶۹ — جدول‌های دستگاه/پرسنل ∈ ماژول‌های devices/org؛ دسترسی با Set<T>()
// روی کانتکست مرکب هاست. ثابت‌ها/رکوردهای خام/توابع قطعی ∈ پلتفرم (BioSyncCore).
// این متدها فقط از مسیرهای خود خانوادهٔ devices صدا زده می‌شوند (کنترلرهای
// ماژول تجهیزات + کانال دروازه) — بدون ماژول، هیچ مسیری این‌جا نمی‌رسد.
// ============================================================

public static class BioSync
{
    // ---------- دریافت پرسنل از دستگاه ----------

    /// <summary>
    /// ثبت/به‌روزرسانی فهرست پرسنل یک دستگاه (رویداد users):
    /// تطبیق با پرسنل سامانه از روی Pin (کد پرسنلی) و در نبودِ پرسنل،
    /// ساخت خودکار رکورد پرسنل در دیتابیس (الزام «ذخیره پرسنل دستگاه در دیتابیس»).
    /// </summary>
    public static async Task<UserUpsertResult> UpsertUsersAsync(
        AppDbContext db, Device device, List<RawDeviceUser> users, bool createMissingEmployees = true)
    {
        var pins = users.Select(u => u.Pin).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        if (pins.Count == 0) return new UserUpsertResult(0, 0, 0, 0, new List<string>());

        // شرکت پرسنل ساخته‌شده از دستگاه = شرکت مالک دستگاه (چندشرکتی)؛ fallback: اولین شرکت
        var company = await db.Companies.FindAsync(device.CompanyId ?? "")
                      ?? await db.Companies.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync();
        var employees = await db.Set<Employee>()
            .Where(e => pins.Contains(e.PersonnelCode))
            .ToDictionaryAsync(e => e.PersonnelCode);
        var existing = await db.Set<DeviceUser>()
            .Where(du => du.DeviceId == device.Id)
            .ToDictionaryAsync(du => du.Pin);

        int created = 0, updated = 0, linked = 0;
        var newCodes = new List<string>();
        var now = DateTime.Now;

        foreach (var u in users)
        {
            if (string.IsNullOrWhiteSpace(u.Pin)) continue;

            DeviceUser du;
            if (existing.TryGetValue(u.Pin, out var found))
            {
                du = found;
                updated++;
            }
            else
            {
                du = new DeviceUser { DeviceId = device.Id, Pin = u.Pin };
                db.Set<DeviceUser>().Add(du);
                existing[u.Pin] = du;
                created++;
            }

            du.Uid = u.Uid;
            du.Name = u.Name;
            du.Privilege = u.Privilege;
            du.CardNumber = string.IsNullOrWhiteSpace(u.Card) || u.Card == "0" ? du.CardNumber : u.Card;
            du.Password = string.IsNullOrWhiteSpace(u.Password) ? du.Password : u.Password;
            du.GroupId = string.IsNullOrWhiteSpace(u.Group) ? du.GroupId : u.Group;
            du.LastFetchedAt = now;
            du.UpdatedAt = now;

            if (employees.TryGetValue(u.Pin, out var emp))
            {
                du.EmployeeId = emp.Id;
                linked++;
            }
            else if (createMissingEmployees && company is not null)
            {
                // پرسنلی که فقط روی دستگاه ثبت شده — ساخت در سامانه
                var parts = (u.Name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                emp = new Employee
                {
                    PersonnelCode = u.Pin,
                    FirstName = parts.Length > 0 ? parts[0] : u.Pin,
                    LastName = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "—",
                    // شناسه ملیplaceholder — یکتا؛ مسئولان باید مقدار واقعی را ثبت کنند
                    NationalId = $"DEV-{device.Serial}-{u.Pin}",
                    EmploymentType = "قراردادی",
                    Status = "فعال",
                    HireDate = now,
                    CompanyId = company.Id,
                    BaseSalary = 0,
                };
                db.Set<Employee>().Add(emp);
                await db.SaveChangesAsync();
                employees[u.Pin] = emp;
                du.EmployeeId = emp.Id;
                linked++;
                newCodes.Add(u.Pin);
            }
        }

        device.UserCount = existing.Count;
        await db.SaveChangesAsync();
        return new UserUpsertResult(users.Count, created, updated, linked, newCodes);
    }

    // ---------- دریافت قالب‌های هویتی از دستگاه ----------

    /// <summary>
    /// ثبت قالب‌های هویتی یک دستگاه (رویداد bios): ذخیره per-device
    /// + کپی مرجع روی پرسنل متصل (EmployeeBiometric) برای انتقال به دستگاه‌های دیگر.
    /// </summary>
    public static async Task<BioUpsertResult> UpsertBiosAsync(AppDbContext db, Device device, List<RawDeviceBio> bios)
    {
        if (bios.Count == 0) return new BioUpsertResult(0, 0, 0, 0, 0, new List<string>());

        var pins = bios.Select(b => b.Pin).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        var deviceUsers = await db.Set<DeviceUser>()
            .Where(du => du.DeviceId == device.Id && pins.Contains(du.Pin))
            .ToDictionaryAsync(du => du.Pin);
        var employeeIds = deviceUsers.Values
            .Where(u => u.EmployeeId is not null).Select(u => u.EmployeeId!).Distinct().ToList();
        var canonical = await db.Set<EmployeeBiometric>()
            .Where(b => employeeIds.Contains(b.EmployeeId))
            .ToListAsync();
        var canonicalMap = canonical.ToDictionary(b => (b.EmployeeId, b.Type, b.BioIndex));
        var devBios = await db.Set<DeviceBiometric>()
            .Where(b => b.DeviceId == device.Id && pins.Contains(b.Pin))
            .ToListAsync();
        var devBioMap = devBios.ToDictionary(b => (b.Pin, b.Type, b.BioIndex));

        int dCreated = 0, dUpdated = 0, cCreated = 0, cUpdated = 0;
        var now = DateTime.Now;
        var touched = new HashSet<string>();

        foreach (var b in bios)
        {
            if (string.IsNullOrWhiteSpace(b.Pin)) continue;

            // ۱) قالب روی دستگاه
            if (!devBioMap.TryGetValue((b.Pin, b.Type, b.Index), out var devBio))
            {
                devBio = new DeviceBiometric { DeviceId = device.Id, Pin = b.Pin, Type = b.Type, BioIndex = b.Index };
                db.Set<DeviceBiometric>().Add(devBio);
                devBioMap[(b.Pin, b.Type, b.Index)] = devBio;
                dCreated++;
            }
            else dUpdated++;
            devBio.Valid = b.Valid;
            devBio.Quality = b.Quality;
            devBio.TemplateData = b.Data;
            devBio.Length = b.Length;
            devBio.FetchedAt = now;

            // ۲) کپی مرجع پرسنل
            if (deviceUsers.TryGetValue(b.Pin, out var du) && du.EmployeeId is not null)
            {
                var ck = (du.EmployeeId, b.Type, b.Index);
                if (!canonicalMap.TryGetValue(ck, out var cb))
                {
                    cb = new EmployeeBiometric
                    {
                        EmployeeId = du.EmployeeId,
                        Type = b.Type,
                        BioIndex = b.Index,
                        EnrolledAt = now,
                    };
                    db.Set<EmployeeBiometric>().Add(cb);
                    canonicalMap[ck] = cb;
                    cCreated++;
                }
                else cUpdated++;

                if (cb.TemplateData != b.Data)
                {
                    cb.TemplateData = b.Data;
                    cb.Length = b.Length;
                    cb.UpdatedAt = now;
                }
                cb.Valid = b.Valid;
                cb.Quality = b.Quality;
                cb.SourceDeviceId = device.Id;
                if (b.Type == "card") cb.CardNumber = b.Data;
                touched.Add(du.EmployeeId);
            }
        }

        await db.SaveChangesAsync();
        return new BioUpsertResult(bios.Count, dCreated, dUpdated, cCreated, cUpdated, touched.ToList());
    }

    // ---------- ساخت payload انتقال به دستگاه ----------

    /// <summary>
    /// ساخت بار دستور push_user_bios — کاربران + قالب‌های مرجع
    /// (اثر انگشت/چهره به‌صورت قالب باینری، کارت/رمز روی رکورد کاربر).
    /// </summary>
    public static async Task<object> BuildPushPayloadAsync(AppDbContext db, List<Employee> employees, bool includeBios)
    {
        var ids = employees.Select(e => e.Id).ToList();
        var bios = includeBios
            ? await db.Set<EmployeeBiometric>().Where(b => ids.Contains(b.EmployeeId)).ToListAsync()
            : new List<EmployeeBiometric>();
        var pinById = employees.ToDictionary(e => e.Id, e => e.PersonnelCode);

        var users = employees.Select(e => new Dictionary<string, object?>
        {
            ["pin"] = e.PersonnelCode,
            ["name"] = e.FullName,
            ["privilege"] = 0,
            ["card"] = bios.FirstOrDefault(b => b.EmployeeId == e.Id && b.Type == "card" && !string.IsNullOrEmpty(b.CardNumber))?.CardNumber ?? "",
            ["password"] = "",
            ["group"] = "1",
        }).ToList();

        var bioList = bios
            .Where(b => b.Type is "fp" or "face" && !string.IsNullOrEmpty(b.TemplateData))
            .Select(b => new Dictionary<string, object?>
            {
                ["pin"] = pinById.GetValueOrDefault(b.EmployeeId, ""),
                ["type"] = b.Type,
                ["index"] = b.BioIndex,
                ["valid"] = b.Valid,
                ["quality"] = b.Quality ?? 0,
                ["data"] = b.TemplateData,
            }).ToList();

        // کارت/رمز جداگانه لازم نیست — روی رکورد کاربر می‌نشیند
        return new { users, bios = bioList };
    }

    /// <summary>
    /// ساخت بار دستور push_user_bios برای «انتقال مستقیم دستگاه ← دستگاه»:
    /// کاربران و قالب‌ها از حافظه ثبت‌شده دستگاه مبدأ خوانده می‌شود؛ برای کاربرانی که
    /// به پرسنل سامانه پیوند دارند، اگر قالبی روی دستگاه مبدأ موجود نباشد اما نسخه مرجع
    /// (EmployeeBiometric) موجود باشد، همان نسخه جایگزین می‌شود — یعنی حتی بدون
    /// «دریافت هویتی» قبلی از دستگاه مبدأ، انتقال کامل انجام می‌شود.
    /// </summary>
    public static async Task<(object Payload, int UserCount, int BioCount)> BuildDevicePushPayloadAsync(
        AppDbContext db, Device source, List<string> pins, bool includeBios)
    {
        var deviceUsers = await db.Set<DeviceUser>()
            .Where(du => du.DeviceId == source.Id && pins.Contains(du.Pin))
            .ToListAsync();
        if (deviceUsers.Count == 0) return (new { users = new List<object>(), bios = new List<object>() }, 0, 0);

        // قالب‌های روی دستگاه مبدأ
        var devBios = includeBios
            ? await db.Set<DeviceBiometric>()
                .Where(b => b.DeviceId == source.Id && pins.Contains(b.Pin))
                .ToListAsync()
            : new List<DeviceBiometric>();

        // نسخه‌های مرجع پرسنلِ پیوندخورده (برای تکمیل قالب‌های ناقص دستگاه مبدأ)
        var employeeIds = deviceUsers
            .Where(u => u.EmployeeId is not null).Select(u => u.EmployeeId!).Distinct().ToList();
        var canonical = includeBios && employeeIds.Count > 0
            ? await db.Set<EmployeeBiometric>().Where(b => employeeIds.Contains(b.EmployeeId)).ToListAsync()
            : new List<EmployeeBiometric>();
        var empPinById = deviceUsers.Where(u => u.EmployeeId is not null)
            .ToDictionary(u => u.EmployeeId!, u => u.Pin);

        var users = deviceUsers.Select(u => new Dictionary<string, object?>
        {
            ["pin"] = u.Pin,
            ["name"] = u.Name,
            ["privilege"] = u.Privilege,
            ["card"] = u.CardNumber ?? "",
            ["password"] = u.Password ?? "",
            ["group"] = u.GroupId ?? "1",
        }).ToList();

        var bioMap = new Dictionary<(string Pin, string Type, int Index), Dictionary<string, object?>>();
        void PutBio(string pin, string type, int index, int valid, int? quality, string? data)
        {
            if (string.IsNullOrEmpty(data)) return;
            bioMap[(pin, type, index)] = new Dictionary<string, object?>
            {
                ["pin"] = pin, ["type"] = type, ["index"] = index,
                ["valid"] = valid, ["quality"] = quality ?? 0, ["data"] = data,
            };
        }

        foreach (var b in devBios.Where(b => b.Type is "fp" or "face"))
            PutBio(b.Pin, b.Type, b.BioIndex, b.Valid, b.Quality, b.TemplateData);

        // تکمیل از نسخه مرجع — فقط اسلات‌هایی که روی دستگاه مبدأ نیستند
        foreach (var cb in canonical.Where(b => b.Type is "fp" or "face"))
        {
            if (!empPinById.TryGetValue(cb.EmployeeId, out var pin)) continue;
            if (bioMap.ContainsKey((pin, cb.Type, cb.BioIndex))) continue;
            PutBio(pin, cb.Type, cb.BioIndex, cb.Valid, cb.Quality, cb.TemplateData);
        }

        return (new { users, bios = bioMap.Values.ToList() }, users.Count, bioMap.Count);
    }

    // ---------- شبیه‌ساز دستگاه (نمایشی — سندباکس بدون سخت‌افزار) ----------

    /// <summary>ساخت فهرست پرسنل شبیه‌سازی‌شده برای یک دستگاه: پرسنل فعال + پرسنلِ فقط-روی-دستگاه.</summary>
    public static async Task<List<RawDeviceUser>> SimulateRosterAsync(AppDbContext db, Device device)
    {
        var emps = await db.Set<Employee>()
            .Where(e => e.Status == "فعال")
            .OrderBy(e => e.PersonnelCode)
            .ToListAsync();

        var list = new List<RawDeviceUser>();
        int uid = 1;
        foreach (var e in emps)
        {
            list.Add(new RawDeviceUser(uid++, e.PersonnelCode, e.FullName,
                uid % 23 == 0 ? 6 : 0, SimCardNo(e.PersonnelCode), "", "1"));
        }

        // چند پرسنل که فقط روی دستگاه ثبت‌شده‌اند (سناریوی «دریافت و ذخیره در دیتابیس»)
        var known = emps.Select(e => e.PersonnelCode).ToHashSet();
        var extras = new[]
        {
            (Pin: "9901", Name: "ناصر رضایی"),
            (Pin: "9902", Name: "مهدی قاسمی"),
            (Pin: "9903", Name: "علی یوسفی"),
        };
        foreach (var (pin, name) in extras)
        {
            if (!known.Contains(pin))
                list.Add(new RawDeviceUser(uid++, pin, name, 0, SimCardNo(pin), "1234", "1"));
        }
        return list;
    }

    /// <summary>ساخت قالب‌های هویتی شبیه‌سازی‌شده برای کاربران یک دستگاه.</summary>
    public static List<RawDeviceBio> SimulateBios(List<RawDeviceUser> users)
    {
        var bios = new List<RawDeviceBio>();
        var rnd = new Random(unchecked(users.Count * 7919 + 13));
        foreach (var u in users)
        {
            // دو اثر انگشت برای همه
            for (int idx = 0; idx < 2; idx++)
            {
                var (data, len) = FakeTemplate($"fp:{u.Pin}:{idx}");
                bios.Add(new RawDeviceBio(u.Pin, "fp", idx, 1, 55 + rnd.Next(45), data, len));
            }
            // چهره برای ۷۰٪
            if (rnd.Next(10) < 7)
            {
                var (data, len) = FakeTemplate($"face:{u.Pin}:0");
                bios.Add(new RawDeviceBio(u.Pin, "face", 0, 1, 50 + rnd.Next(50), data, len));
            }
            // کارت برای ۶۰٪
            if (u.Card is { Length: > 0 } && u.Card != "0")
                bios.Add(new RawDeviceBio(u.Pin, "card", 0, 1, null, u.Card, u.Card.Length));
            // رمز برای ۳۰٪
            if (rnd.Next(10) < 3)
                bios.Add(new RawDeviceBio(u.Pin, "pin", 0, 1, null, "0000", 4));
        }
        return bios;
    }

    /// <summary>شماره کارت قطعی ۸ رقمی از روی پین — ∈ پلتفرم (BioSyncCore؛ لایهٔ دیتای ماژول تجهیزات هم مصرف می‌کند).</summary>
    public static string SimCardNo(string pin) => BioSyncCore.SimCardNo(pin);

    /// <summary>قالب باینری نمایشی (هدر ZK + بایت‌های قطعی) به base64 — ∈ پلتفرم.</summary>
    public static (string Data, int Length) FakeTemplate(string key) => BioSyncCore.FakeTemplate(key);
}
