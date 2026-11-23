using System.Globalization;
using System.Text.Json;
using Platform.Web.Domain;
using Platform.Web.Data;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

public record RawPunch(string Pin, string Timestamp, string? Status = null);

public record IngestResult(int Ok, int Failed, List<string> UnknownPins);

// ============================================================
// عملیات دستگاه‌های تردد — پورت دقیق src/lib/device-ops.ts
// صف دستورات، احراز دستگاه (webhook)، ورود داده تردد، وضعیت آنلاین
// فاز ۶۹ — جدول‌های دستگاه/پرسنل ∈ ماژول‌های devices/org؛ دسترسی با Set<T>()
// (کانتکست پایه فقط هستهٔ پلتفرم را می‌شناسد) — این متدها فقط از مسیرهای
// خود خانوادهٔ devices صدا زده می‌شوند؛ بدون ماژول، هیچ مسیری این‌جا نمی‌رسد.
// ============================================================
public static class DeviceOps
{
    public static readonly string[] CommandTypes =
    {
        "unlock", "sync_time", "restart", "push_user", "delete_user", "get_user", "get_users",
        "get_user_bios", "push_user_bios", "refresh_data", "clear_data", "clear_attendance",
        "enable_device", "disable_device",
    };

    public static readonly Dictionary<string, string> CommandLabels = new()
    {
        ["unlock"] = "بازکردن درب",
        ["sync_time"] = "همگام‌سازی ساعت",
        ["restart"] = "ری‌استارت دستگاه",
        ["push_user"] = "ارسال کاربر به دستگاه",
        ["delete_user"] = "حذف کاربر از دستگاه",
        ["get_user"] = "دریافت اطلاعات کاربر",
        ["get_users"] = "دریافت فهرست کاربران",
        ["get_user_bios"] = "دریافت اطلاعات هویتی از دستگاه",
        ["push_user_bios"] = "ارسال کاربر + قالب‌های هویتی به دستگاه",
        ["refresh_data"] = "به‌روزرسانی داده‌ها",
        ["clear_data"] = "پاک‌سازی داده‌ها",
        ["clear_attendance"] = "پاک‌سازی رکوردهای تردد",
        ["enable_device"] = "فعال‌سازی دستگاه",
        ["disable_device"] = "غیرفعال‌سازی دستگاه",
    };

    // ---------- صف دستورات ----------
    public static async Task<DeviceCommand> QueueCommandAsync(
        DbContext db, string deviceId, string type, object? payload, SessionUser? actor)
    {
        var cmd = new DeviceCommand
        {
            DeviceId = deviceId,
            Type = type,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload),
            CreatedByUserId = actor?.Id,
            CreatedBy = actor?.FullName ?? "سرویس پل",
        };
        db.Set<DeviceCommand>().Add(cmd);
        await db.SaveChangesAsync();
        return cmd;
    }

    /// <summary>
    /// احراز دستگاه برای webhook: نشست سرویس پل با دسترسی devices.poll
    /// یا هدر X-Device-Token برابر webhookToken دستگاه.
    /// </summary>
    public static async Task<(Device? Device, int? ErrorStatus)> AuthenticateDeviceAsync(
        DbContext db, string? sn, SessionUser? session, string? deviceTokenHeader)
    {
        if (string.IsNullOrEmpty(sn)) return (null, 400);
        var device = await db.Set<Device>().FirstOrDefaultAsync(d => d.Serial == sn);
        if (device is null || !device.Active) return (null, 404);

        if (session is not null && session.Has("devices.poll"))
            return (device, null);

        if (string.IsNullOrEmpty(deviceTokenHeader) || deviceTokenHeader != device.WebhookToken)
            return (null, 401);
        return (device, null);
    }

    public static async Task MarkSeenAsync(DbContext db, Device device,
        string? firmware = null, int? userCount = null, IAttendanceGateway? att = null)
    {
        // فاز ۱۳۲ (مورد ۱۳ ROADMAP-ADDITIONS) — گذار آفلاین→آنلاین برای تسویهٔ خودکار حافظهٔ آفلاین
        var wasOffline = device.Mode != "آنلاین";
        device.LastSeenAt = DateTime.Now;
        device.LastSync = DateTime.Now;
        device.Mode = "آنلاین";
        if (firmware is not null) device.Firmware = firmware;
        if (userCount is not null) device.UserCount = userCount;
        // رفع خودکار هشدارهای «قطع دستگاه» همین دستگاه (فاز ۲۹)
        var open = await db.Set<SecurityAlert>()
            .Where(a => !a.Resolved && a.Type == "قطع دستگاه" && a.Source == "دستگاه‌های تردد" && a.RefId == device.Id)
            .ToListAsync();
        foreach (var a in open) { a.Resolved = true; a.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();

        // فاز ۱۳۲ (مورد ۱۳) — تسویهٔ خودکار پانچ آفلاین: هر بار دستگاهی از آفلاین به
        // آنلاین می‌رسد، صف DeviceOfflinePunch همان دستگاه خودکار وارد موتور تردد
        // می‌شود (تا امروز فقط با دکمهٔ دستی «دریافت دسته‌ای» انجام می‌شد و پانچ‌های
        // آفلاین تا کلیک ادمین در کارکرد و حقوق نمی‌آمدند). خطای موتور هرگز ضربان
        // دستگاه را خراب نمی‌کند؛ صفحهٔ دستگاه‌ها شمارندهٔ حافظهٔ آفلاین را نشان می‌دهد.
        if (wasOffline && att is not null)
        {
            try
            {
                var queued = await db.Set<DeviceOfflinePunch>()
                    .CountAsync(p => p.DeviceId == device.Id && p.ImportedAt == null);
                if (queued > 0)
                    await FlushOfflineBufferAsync(db, att, device);
            }
            catch { /* تسویهٔ خودکار نباید heartbeat را خراب کند — دفعهٔ بعد تلاش می‌شود */ }
        }
    }

    // ---------- ورود رکوردهای تردد ----------
    private static DateTime? ParseTs(string? ts)
    {
        if (string.IsNullOrWhiteSpace(ts)) return null;
        var s = ts.Trim();
        if (!s.Contains('T')) s = s.Replace(" ", "T");
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    public static async Task<IngestResult> IngestPunchesAsync(DbContext db, IAttendanceGateway att, string deviceId, List<RawPunch> punches)
    {
        int ok = 0, failed = 0;
        var unknownPins = new List<string>();
        // فاز ۴۳ — شرکت میزبان برای لاگ تردد خام (یک پرس‌وجوی سبک برای کل دسته)
        var deviceCompanyId = await db.Set<Device>()
            .Where(d => d.Id == deviceId)
            .Select(d => d.CompanyId)
            .FirstOrDefaultAsync();

        foreach (var p in punches)
        {
            var ts = ParseTs(p.Timestamp);
            if (ts is null || string.IsNullOrEmpty(p.Pin)) { failed++; continue; }

            // فاز ۶۹ — پرسنل ∈ ماژول org؛ بدون آن هر پین «ناشناس» ثبت می‌شود (دادهٔ خام لاگ می‌ماند)
            Employee? employee = null;
            if (HrModuleRuntime.Installed("org"))
                employee = await db.Set<Employee>()
                    .FirstOrDefaultAsync(e => e.PersonnelCode == p.Pin);
            if (employee is null)
            {
                if (!unknownPins.Contains(p.Pin)) unknownPins.Add(p.Pin);
                failed++;
                // فاز ۴۳ — اسکن ناشناس هم در لاگ زنده حراست می‌آید (امنیت باید آن را ببیند)
                db.Set<DevicePunchLog>().Add(new DevicePunchLog
                {
                    DeviceId = deviceId,
                    CompanyId = deviceCompanyId,
                    Pin = p.Pin,
                    Timestamp = ts.Value,
                    Kind = "ناشناس",
                    Note = "کد پرسنلی در سامانه یافت نشد",
                });
                await db.SaveChangesAsync();
                continue;
            }

            var dk = Jalali.DateKey(ts.Value);
            // فاز ۶۱ — ثبت رکورد حضور از مسیر قرارداد IAttendanceGateway (لایهٔ دیتای ماژول حضور)
            // شامل محاسبهٔ AttEngine و قانون «اولین ورود / آخرین خروج» — بدون ارجاع به entityهای ماژول
            var w = await att.UpsertPunchAsync(employee.Id, deviceId, dk, ts.Value);
            var isFirstIn = w.IsFirstIn;
            var isLastOut = w.IsLastOut;
            var checkIn = w.CheckIn;
            var checkOut = w.CheckOut;

            // فاز ۴۳ — لاگ تردد خام برای نمای زنده حراست: هر اسکن یک ردیف با جهت استنباط‌شده
            // (ورود = تازه‌ترین اسکنِ اولِ روز، خروج = تازه‌ترین اسکنِ آخرِ روز، بقیه = اسکن میانی)
            db.Set<DevicePunchLog>().Add(new DevicePunchLog
            {
                DeviceId = deviceId,
                CompanyId = employee.CompanyId ?? deviceCompanyId,
                Pin = p.Pin,
                EmployeeId = employee.Id,
                Timestamp = ts.Value,
                Kind = isFirstIn ? "ورود" : (isLastOut && checkIn != checkOut ? "خروج" : "تردد"),
            });
            await db.SaveChangesAsync();
            ok++;
        }
        return new IngestResult(ok, failed, unknownPins);
    }

    // ---------- وضعیت آنلاین ----------
    public static bool IsOnline(Device d)
    {
        if (d.LastSeenAt is null) return false;
        var threshold = TimeSpan.FromSeconds(Math.Max(3 * (d.PollingInterval == 0 ? 60 : d.PollingInterval), 180));
        return DateTime.Now - d.LastSeenAt.Value < threshold;
    }

    /// <summary>اسکن تنبلی: دستگاه‌های دیده‌نشده را آفلاین کند + دستورهای تحویل‌شدهٔ معطل را دوباره صف کند.
    /// برای هر دستگاهِ تازه‌آفلاین یک هشدار «قطع دستگاه» (بدون تکرار هشدار باز قبلی) ثبت می‌شود (فاز ۲۹).</summary>
    public static async Task<int> SweepOfflineAsync(DbContext db)
    {
        var devices = await db.Set<Device>()
            .Where(d => d.Active && d.Mode == "آنلاین")
            .ToListAsync();
        int n = 0;
        foreach (var d in devices)
        {
            if (!IsOnline(d))
            {
                d.Mode = "آفلاین";
                n++;
                // هشدار قطع دستگاه — بدون تکرار: اگر هشدار باز دیگری برای همین دستگاه هست، ثبت نمی‌شود
                var hasOpen = await db.Set<SecurityAlert>().AnyAsync(a =>
                    !a.Resolved && a.Type == "قطع دستگاه" && a.Source == "دستگاه‌های تردد" && a.RefId == d.Id);
                if (!hasOpen)
                {
                    db.Set<SecurityAlert>().Add(new SecurityAlert
                    {
                        Type = "قطع دستگاه",
                        Severity = d.Purpose == "کنترل تردد" ? "بالا" : "متوسط",
                        Message = $"دستگاه «{d.Name}» ({d.Brand} — {d.Serial}) از دسترس خارج شد. آخرین اتصال: " +
                                  (d.LastSeenAt is null ? "بدون سابقه" : Jalali.FmtLong(d.LastSeenAt.Value)),
                        Source = "دستگاه‌های تردد",
                        RefId = d.Id,
                    });
                }
            }
        }
        n += await RequeueStaleDeliveredAsync(db);
        if (n > 0) await db.SaveChangesAsync();
        return n;
    }

    /// <summary>
    /// تور ایمنی: دستورهایی که «تحویل شد» شده‌اند ولی تا ۵ دقیقه ack نگرفته‌اند
    /// (اتصال دستگاه قطع شده / ack گم شده) دوباره pending می‌شوند تا در دست‌بعدی تحویل بگیرند.
    /// آستانه ۵ دقیقه از هر چرخه ack گیت‌وی (حداکثر ~۲ دقیقه) بلندتر است.
    /// </summary>
    public static async Task<int> RequeueStaleDeliveredAsync(DbContext db)
    {
        var cutoff = DateTime.Now.AddMinutes(-5);
        var stale = await db.Set<DeviceCommand>()
            .Where(c => c.Status == "delivered" && c.DeliveredAt != null && c.DeliveredAt < cutoff)
            .ToListAsync();
        foreach (var c in stale)
        {
            c.Status = "pending";
            c.DeliveredAt = null;
        }
        return stale.Count;
    }

    // ---------- انقضای خودکار هشدارها — TTL (فاز ۳۰) ----------
    /// <summary>
    /// هشدارهای بازِ قدیمی‌تر از TTL تنظیم‌شده (NotificationSetting.AlertTtlDays) منقضی می‌شوند.
    /// صفر = غیرفعال (پیش‌فرض — هیچ هشداری خودبه‌خود منقضی نمی‌شود).
    /// خروجی: تعداد هشدارهای تازه‌منقضی‌شده.
    /// </summary>
    public static async Task<int> SweepAlertTtlAsync(DbContext db)
    {
        var ttl = (await NotificationEngine.GetSettingsAsync(db)).AlertTtlDays;
        if (ttl <= 0) return 0;
        var cutoff = DateTime.Now.AddDays(-ttl);
        var stale = await db.Set<SecurityAlert>()
            .Where(a => !a.Resolved && !a.Expired && a.CreatedAt < cutoff)
            .ToListAsync();
        if (stale.Count == 0) return 0;
        foreach (var a in stale) { a.Expired = true; a.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
        return stale.Count;
    }

    // ---------- حافظه آفلاین دستگاه (فاز ۳۰) ----------
    /// <summary>تعداد ترددهای صف‌شده در حافظه آفلاین هر دستگاه (ImportedAt == null).</summary>
    public static async Task<Dictionary<string, int>> OfflineBufferCountsAsync(DbContext db)
    {
        return await db.Set<DeviceOfflinePunch>()
            .Where(p => p.ImportedAt == null)
            .GroupBy(p => p.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeviceId, x => x.Count);
    }

    /// <summary>
    /// دریافت دسته‌ای ترددهای آفلاین: همه ردیف‌های صف‌شده دستگاه وارد موتور تردد می‌شوند
    /// (تاریخ/ساعت اصلیِ روی دستگاه حفظ می‌شود) و سپس صف تخلیه می‌شود.
    /// </summary>
    public static async Task<IngestResult> FlushOfflineBufferAsync(DbContext db, IAttendanceGateway att, Device device)
    {
        var queued = await db.Set<DeviceOfflinePunch>()
            .Where(p => p.DeviceId == device.Id && p.ImportedAt == null)
            .OrderBy(p => p.Timestamp)
            .ToListAsync();
        if (queued.Count == 0) return new IngestResult(0, 0, new List<string>());

        var punches = queued.Select(p => new RawPunch(p.Pin, p.Timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss"))).ToList();
        var result = await IngestPunchesAsync(db, att, device.Id, punches);
        var now = DateTime.Now;
        foreach (var p in queued)
        {
            p.ImportedAt = now;
            p.Note = result.UnknownPins.Contains(p.Pin) ? "کد پرسنلی در سامانه یافت نشد" : p.Note;
        }
        await db.SaveChangesAsync();
        return result;
    }
}
