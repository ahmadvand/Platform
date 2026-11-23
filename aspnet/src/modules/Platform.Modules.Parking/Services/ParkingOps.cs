using System.Globalization;
using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Modules.Parking.Data;
using Platform.Modules.Parking.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

/// <summary>پلاک خوانده‌شده خام از دوربین پلاک‌خوان (ANPR).</summary>
public record RawPlateRead(string Plate, string? Direction = null, string? Timestamp = null, int? Confidence = null);

/// <summary>نتیجه ثبت خودکار یک خوانش پلاک.</summary>
public record PlateIngestResult(
    int Ok, int Failed, List<string> UnknownPlates, List<string> Denied,
    ParkingEvent? LastEvent = null, Vehicle? Vehicle = null);

/// <summary>پیامد یک عبور — رویداد ذخیره‌شده + خودرو + نتیجه ارزیابی.</summary>
public record PassOutcome(ParkingEvent Ev, Vehicle? Vehicle, bool Allowed, string? Reason);

// ============================================================
// عملیات پارکینگ و دوربین‌های پلاک‌خوان (فاز ۴۰/۴۱)
// موتور ارزیابی مجوز (مشترک بین شبیه‌ساز دستی و پل دوربین‌ها)،
// احراز دوربین، صف دستور، ورود خودکار خوانش پلاک و وضعیت آنلاین.
// فاز ۶۱ — دیتای پارکینگ روی ParkingDbContext؛ SecurityAlert روی HrBaseDbContext
// (پارامتر دوم متدهایی که هشدار امنیتی می‌نویسند/می‌خوانند).
// ============================================================
public static class ParkingOps
{
    public static readonly string[] CommandTypes = { "open_barrier", "close_barrier", "ping", "restart" };

    public static readonly Dictionary<string, string> CommandLabels = new()
    {
        ["open_barrier"] = "بازکردن راهبند",
        ["close_barrier"] = "بستن راهبند",
        ["ping"] = "ضربان سنجش اتصال",
        ["restart"] = "ری‌استارت دوربین",
    };

    // ---------- صف دستور دوربین (همان الگوی DeviceOps.QueueCommandAsync) ----------
    public static async Task<AnprCommand> QueueCommandAsync(
        ParkingDbContext db, string cameraId, string type, object? payload, SessionUser? actor)
    {
        var cmd = new AnprCommand
        {
            CameraId = cameraId,
            Type = type,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload),
            CreatedByUserId = actor?.Id,
            CreatedBy = actor?.FullName ?? "سرویس پل",
        };
        db.AnprCommands.Add(cmd);
        await db.SaveChangesAsync();
        return cmd;
    }

    /// <summary>احراز دوربین برای webhook: نشست سرویس پل با دسترسی devices.poll
    /// یا هدر X-Device-Token برابر WebhookToken دوربین (همان الگوی دستگاه‌های تردد).</summary>
    public static async Task<(AnprCamera? Camera, int? ErrorStatus)> AuthenticateCameraAsync(
        ParkingDbContext db, string? sn, SessionUser? session, string? deviceTokenHeader)
    {
        if (string.IsNullOrEmpty(sn)) return (null, 400);
        var cam = await db.AnprCameras.FirstOrDefaultAsync(c => c.Serial == sn);
        if (cam is null || !cam.Active) return (null, 404);

        if (session is not null && session.Has("devices.poll"))
            return (cam, null);

        if (string.IsNullOrEmpty(deviceTokenHeader) || deviceTokenHeader != cam.WebhookToken)
            return (null, 401);
        return (cam, null);
    }

    public static async Task MarkSeenAsync(ParkingDbContext db, HrBaseDbContext baseDb, AnprCamera camera)
    {
        camera.LastSeenAt = DateTime.Now;
        camera.Mode = "آنلاین";
        // رفع خودکار هشدارهای «قطع دوربین» همین دوربین — SecurityAlert روی کانتکست پایه (فاز ۶۱)
        var open = await baseDb.SecurityAlerts
            .Where(a => !a.Resolved && a.Type == "قطع دوربین" && a.Source == "کنترل پارکینگ" && a.RefId == camera.Id)
            .ToListAsync();
        foreach (var a in open) { a.Resolved = true; a.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
        if (baseDb.ChangeTracker.HasChanges()) await baseDb.SaveChangesAsync();
    }

    // ---------- وضعیت آنلاین ----------
    public static bool IsOnline(AnprCamera c)
    {
        if (c.LastSeenAt is null) return false;
        var threshold = TimeSpan.FromSeconds(Math.Max(3 * (c.HeartbeatInterval == 0 ? 60 : c.HeartbeatInterval), 180));
        return DateTime.Now - c.LastSeenAt.Value < threshold;
    }

    /// <summary>اسکن تنبلی دوربین‌ها — دیده‌نشده‌ها آفلاین + هشدار «قطع دوربین» بدون تکرار؛
    /// دستورهای تحویل‌شده معطل (۵ دقیقه بدون ack) دوباره صف می‌شوند.</summary>
    public static async Task<int> SweepOfflineAsync(ParkingDbContext db, HrBaseDbContext baseDb)
    {
        var cameras = await db.AnprCameras
            .Where(c => c.Active && c.Mode == "آنلاین")
            .ToListAsync();
        int n = 0;
        foreach (var c in cameras)
        {
            if (!IsOnline(c))
            {
                c.Mode = "آفلاین";
                n++;
                var hasOpen = await baseDb.SecurityAlerts.AnyAsync(a =>
                    !a.Resolved && a.Type == "قطع دوربین" && a.Source == "کنترل پارکینگ" && a.RefId == c.Id);
                if (!hasOpen)
                {
                    baseDb.SecurityAlerts.Add(new SecurityAlert
                    {
                        Type = "قطع دوربین",
                        Severity = "بالا",
                        Message = $"دوربین پلاک‌خوان «{c.Name}» ({c.Brand} — {c.Serial}) از دسترس خارج شد. آخرین اتصال: " +
                                  (c.LastSeenAt is null ? "بدون سابقه" : Jalali.FmtLong(c.LastSeenAt.Value)),
                        Source = "کنترل پارکینگ",
                        RefId = c.Id,
                    });
                }
            }
        }
        n += await RequeueStaleDeliveredAsync(db);
        if (n > 0) await db.SaveChangesAsync();
        if (baseDb.ChangeTracker.HasChanges()) await baseDb.SaveChangesAsync();
        return n;
    }

    /// <summary>تور ایمنی صف دستور دوربین — delivered معطلِ ۵ دقیقه دوباره pending می‌شود.</summary>
    public static async Task<int> RequeueStaleDeliveredAsync(ParkingDbContext db)
    {
        var cutoff = DateTime.Now.AddMinutes(-5);
        var stale = await db.AnprCommands
            .Where(c => c.Status == "delivered" && c.DeliveredAt != null && c.DeliveredAt < cutoff)
            .ToListAsync();
        foreach (var c in stale) { c.Status = "pending"; c.DeliveredAt = null; }
        return stale.Count;
    }

    // ---------- موتور ارزیابی عبور (مشترک: شبیه‌ساز + دوربین‌ها) ----------
    /// <summary>
    /// ارزیابی کامل یک عبور خودرو از گیت: گیت فعال/حالت راهبند/هم‌خوانی جهت + خودرو + مجوز معتبر +
    /// آنتی‌پس‌بک + ظرفیت. رویداد (مجاز یا غیرمجاز با دلیل) را ذخیره می‌کند و برای ردهای واقعی
    /// هشدار امنیتی ثبت می‌کند. برگشتی حاوی رویداد ذخیره‌شده و پیامد آن است.
    /// </summary>
    public static async Task<PassOutcome> TryPassAsync(
        ParkingDbContext db, HrBaseDbContext baseDb, ParkingGate gate, string plateText, string direction, string method,
        string? cameraId = null, int? confidence = null, DateTime? at = null, string? plateKey = null)
    {
        var now = at ?? DateTime.Now;
        var plate = (plateText ?? "").Trim();
        var key = plateKey ?? PlateFmt.CanonicalKey(plate);

        async Task<PassOutcome> DenyAsync(string? reason, string alertType, bool alert = true)
        {
            var ev = new ParkingEvent
            {
                GateId = gate.Id, CameraId = cameraId, PlateText = plate, PlateKey = key,
                Direction = direction, Method = method, Result = "غیرمجاز",
                Reason = reason, Confidence = confidence, Timestamp = now,
            };
            db.ParkingEvents.Add(ev);
            if (alert)
                baseDb.SecurityAlerts.Add(new SecurityAlert
                {
                    Type = alertType, Severity = alertType == "ورود غیرمجاز" ? "بالا" : "متوسط",
                    Message = $"{reason} — پلاک «{plate}» در {gate.Name} ({gate.Lot?.Name})",
                    Source = "کنترل پارکینگ", RefId = ev.Id,
                });
            await db.SaveChangesAsync();
            if (alert) await baseDb.SaveChangesAsync();
            return new PassOutcome(ev, null, false, reason);
        }

        // گیت
        if (gate.Status != "فعال") return await DenyAsync("گیت غیرفعال است", "ورود غیرمجاز", alert: false);
        if (gate.OpenState == "قفل دائم") return await DenyAsync("گیت قفل دائم است", "ورود غیرمجاز", alert: false);
        if (gate.Direction != "دوطرفه" && gate.Direction != (direction == "ورود" ? "ورودی" : "خروجی"))
            return await DenyAsync("جهت عبور با گیت هم‌خوانی ندارد", "ورود غیرمجاز", alert: false);

        // خودرو
        var vehicle = await db.Vehicles.Include(v => v.Employee)
            .FirstOrDefaultAsync(v => v.PlateKey == key && v.CompanyId == gate.CompanyId);
        if (vehicle is null) return await DenyAsync("خودرو فاقد مجوز پارکینگ است", "ورود غیرمجاز");
        if (!vehicle.Active) return await DenyAsync("خودرو غیرفعال است (توقیف/خارج از سرویس)", "ورود غیرمجاز");

        // مجوز
        var permit = await db.ParkingPermits
            .Where(p => p.VehicleId == vehicle.Id && p.LotId == gate.LotId && p.Status == "فعال")
            .FirstOrDefaultAsync(p => p.ValidFrom <= now && (p.ValidUntil == null || p.ValidUntil >= now));
        if (permit is null)
        {
            var any = await db.ParkingPermits
                .FirstOrDefaultAsync(p => p.VehicleId == vehicle.Id && p.LotId == gate.LotId);
            return await DenyAsync(any is null
                ? "مجوز فعال برای این پارکینگ ندارد"
                : (any.Status == "لغوشده" ? "مجوز پارکینگ لغو شده است" : "مجوز پارکینگ منقضی است"),
                "مجوز منقضی");
        }

        // آنتی‌پس‌بک — عبور تکراری بدون ثبت عبور مقابل
        var last = await db.ParkingEvents
            .Where(e => e.VehicleId == vehicle.Id && e.Gate!.LotId == gate.LotId && e.Result == "مجاز")
            .OrderByDescending(e => e.Timestamp).FirstOrDefaultAsync();
        if (last is not null && last.Direction == direction)
            return await DenyAsync(direction == "ورود"
                ? "ورود تکراری — خروج قبلی ثبت نشده است"
                : "خروج تکراری — ورود قبلی ثبت نشده است", "ورود غیرمجاز");

        // ظرفیت پارکینگ (فقط برای ورود)
        if (direction == "ورود" && gate.Lot is not null)
        {
            var inside = await InsideCountAsync(db, gate.LotId);
            if (inside >= gate.Lot.Capacity)
                return await DenyAsync("ظرفیت پارکینگ تکمیل است", "ورود غیرمجاز");
        }

        // مجاز
        var okEv = new ParkingEvent
        {
            GateId = gate.Id, CameraId = cameraId, VehicleId = vehicle.Id, PlateText = plate, PlateKey = key,
            Direction = direction, Method = method, Result = "مجاز", Confidence = confidence, Timestamp = now,
        };
        db.ParkingEvents.Add(okEv);
        await db.SaveChangesAsync();
        return new PassOutcome(okEv, vehicle, true, null);
    }

    /// <summary>تعداد خودروهای داخل پارکینگ — تفسیر زنده رویدادهای مجاز (ورود/خروج به‌ترتیب زمان).</summary>
    public static async Task<int> InsideCountAsync(ParkingDbContext db, string lotId)
    {
        var lotIds = new HashSet<string> { lotId };
        var passes = await db.ParkingEvents
            .Where(e => e.VehicleId != null && e.Result == "مجاز" && lotIds.Contains(e.Gate!.LotId))
            .OrderBy(e => e.Timestamp)
            .Select(e => new { e.VehicleId, e.Direction })
            .ToListAsync();
        var inside = new HashSet<string>();
        foreach (var p in passes)
        { if (p.Direction == "ورود") inside.Add(p.VehicleId!); else inside.Remove(p.VehicleId!); }
        return inside.Count;
    }

    // ---------- ورود خودکار خوانش پلاک (فاز ۴۱) ----------
    private static DateTime? ParseTs(string? ts)
    {
        if (string.IsNullOrWhiteSpace(ts)) return null;
        var s = ts.Trim();
        if (!s.Contains('T')) s = s.Replace(" ", "T");
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    /// <summary>
    /// ثبت خودکار یک یا چند خوانش پلاک از یک دوربین — پلاک نرمال می‌شود، آستانه اطمینان چک
    /// می‌گردد و موتور TryPassAsync ورود/خروج را ثبت و (در صورت مجاز بودن) باز شدن راهبند را برمی‌گرداند.
    /// خوانده‌های با اطمینان پایین یا پلاک خالی شکست محسوب می‌شوند؛ پلاک ناشناس هم رویداد غیرمجاز ثبت می‌کند.
    /// </summary>
    public static async Task<PlateIngestResult> IngestPlateReadsAsync(
        ParkingDbContext db, HrBaseDbContext baseDb, AnprCamera camera, List<RawPlateRead> reads)
    {
        int ok = 0, failed = 0;
        var unknownPlates = new List<string>();
        var denied = new List<string>();
        ParkingEvent? lastEv = null; Vehicle? lastVeh = null;

        var gate = await db.ParkingGates.Include(g => g.Lot)
            .FirstOrDefaultAsync(g => g.Id == camera.GateId);
        if (gate is null)
            return new PlateIngestResult(0, reads.Count, unknownPlates, denied);

        foreach (var r in reads)
        {
            var plate = (r.Plate ?? "").Trim();
            var ts = ParseTs(r.Timestamp);
            if (plate.Length < 4)
            {
                failed++;
                continue;
            }
            // آستانه اطمینان دوربین — خوانش غیرقابل‌اتکا ثبت نمی‌شود
            if (r.Confidence is int conf && conf < camera.ConfidenceThreshold)
            {
                failed++;
                continue;
            }
            // جهت: اگر دوربین جهت خود را دارد، همان ملاک است؛ وگرنه جهت پست‌شده
            var dir = camera.Direction switch
            {
                "ورودی" => "ورود",
                "خروجی" => "خروج",
                _ => r.Direction == "خروج" ? "خروج" : "ورود",
            };

            var outcome = await TryPassAsync(db, baseDb, gate, plate, dir, "پلاک‌خوان",
                cameraId: camera.Id, confidence: r.Confidence, at: ts);

            lastEv = outcome.Ev;
            lastVeh = outcome.Vehicle;
            if (outcome.Allowed)
            {
                ok++;
            }
            else
            {
                failed++;
                if (outcome.Reason == "خودرو فاقد مجوز پارکینگ است" && !unknownPlates.Contains(plate))
                    unknownPlates.Add(plate);
                else if (!denied.Contains(plate)) denied.Add(plate);
            }
        }
        return new PlateIngestResult(ok, failed, unknownPlates, denied, lastEv, lastVeh);
    }
}
