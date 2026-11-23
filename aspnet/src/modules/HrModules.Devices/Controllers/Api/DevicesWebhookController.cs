using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Devices.Api;

/// <summary>
/// پل ارتباط دستگاه‌ها (الگوی ADMS/PUSH — PLAN P2.3) — قرارداد کامل سرویس C# گیت‌وی.
/// GET  ?SN=... با Bearer سرویس پل یا X-Device-Token → تحویل دستورات صف‌شده + heartbeat
/// POST { SN, event: "ping|records|users|ack|status", data }
/// </summary>
[ApiController]
[Route("api/devices")]
public class DevicesWebhookController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HrBaseDbContext Base;
    private readonly IAttendanceGateway _att;
    private readonly ICurrentUser _cu;

    public DevicesWebhookController(AppDbContext db, HrBaseDbContext baseDb, IAttendanceGateway att, ICurrentUser cu) { _db = db; Base = baseDb; _att = att; _cu = cu; }

    // ---------- GET: تحویل دستورات صف‌شده ----------
    [HttpGet("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCommands([FromQuery] string? SN, [FromQuery] bool consume = true)
    {
        var auth = await AuthenticateAsync(SN);
        if (auth.Device is null) return ApiError(auth.Status);
        var device = auth.Device;

        // فاز ۱۳۲ (مورد ۱۳ ROADMAP-ADDITIONS) — att پاس می‌شود تا گذار آفلاین→آنلاین
        // حافظهٔ آفلاین دستگاه را خودکار به موتور تردد بریزد (تسویهٔ خودکار)
        await DeviceOps.MarkSeenAsync(_db, device, att: _att);
        device.LastPulledAt = DateTime.Now;
        await _db.SaveChangesAsync();

        // دستورات در انتظار (الگوی ACK پروتکل ZK)
        // consume=false → فقط نمایش (برای sweeper گزارش‌گیر) — بدون تغییر وضعیت؛
        // وگرنه pending → delivered (مصرف واقعی توسط poller/پل ADMS)
        var pending = await _db.Set<DeviceCommand>()
            .Where(c => c.DeviceId == device.Id && c.Status == "pending")
            .OrderBy(c => c.CreatedAt)
            .Take(20)
            .ToListAsync();
        if (consume && pending.Count > 0)
        {
            foreach (var c in pending) { c.Status = "delivered"; c.DeliveredAt = DateTime.Now; }
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            ok = true,
            time = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss"),
            commands = pending.Select(c =>
            {
                object? payload = string.IsNullOrEmpty(c.Payload)
                    ? null
                    : (object?)JsonDocument.Parse(c.Payload).RootElement;
                return new { id = c.Id, type = c.Type, payload };
            }),
        });
    }

    // ---------- POST: رویدادهای دستگاه ----------
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PostEvent([FromBody] JsonElement body)
    {
        try
        {
            string? sn = body.TryGetProperty("SN", out var snEl) ? snEl.GetString() : null;
            string? @event = body.TryGetProperty("event", out var evEl) ? evEl.GetString() : null;
            var data = body.TryGetProperty("data", out var dataEl) ? dataEl : default;

            var auth = await AuthenticateAsync(sn);
            if (auth.Device is null) return ApiError(auth.Status);
            var device = auth.Device;
            // فاز ۱۳۲ (مورد ۱۳ ROADMAP-ADDITIONS) — att پاس می‌شود تا گذار آفلاین→آنلاین
        // حافظهٔ آفلاین دستگاه را خودکار به موتور تردد بریزد (تسویهٔ خودکار)
        await DeviceOps.MarkSeenAsync(_db, device, att: _att);

            switch (@event)
            {
                case "ping":
                case "status":
                {
                    string? firmware = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("firmware", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                    int? userCount = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("userCount", out var u) && u.ValueKind == JsonValueKind.Number ? u.GetInt32() : null;
                    await DeviceOps.MarkSeenAsync(_db, device, firmware, userCount, att: _att);
                    return Ok(new { ok = true });
                }

                case "records":
                case "offline-records":
                {
                    // «records»: ترددهای زنده | «offline-records» (فاز ۳۰): ارسال دسته‌ای حافظه آفلاین
                    // دستگاه پس از وصل‌شدن دوباره — هر دو با همان موتور ورود داده پردازش می‌شوند
                    // و تاریخ/ساعت اصلیِ روی دستگاه حفظ می‌شود.
                    var punches = new List<RawPunch>();
                    if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("punches", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var p in arr.EnumerateArray())
                        {
                            punches.Add(new RawPunch(
                                p.TryGetProperty("pin", out var pin) ? pin.GetString() ?? "" : "",
                                p.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? "" : "",
                                p.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null));
                        }
                    }
                    var r = await DeviceOps.IngestPunchesAsync(_db, _att, device.Id, punches);
                    return Ok(new { ok = true, okCount = r.Ok, failed = r.Failed, unknownPins = r.UnknownPins });
                }

                case "users":
                {
                    // دریافت پرسنل دستگاه — ثبت در دیتابیس + پیوند/ساخت پرسنل
                    var users = new List<RawDeviceUser>();
                    if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("users", out var uarr) && uarr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var u in uarr.EnumerateArray())
                        {
                            string pin = Str(u, "pin");
                            if (pin.Length == 0) pin = Str(u, "userId");
                            users.Add(new RawDeviceUser(
                                Int(u, "uid"), pin,
                                Str(u, "name").Length > 0 ? Str(u, "name") : pin,
                                Int(u, "privilege"),
                                Str(u, "card"), Str(u, "password"), Str(u, "group")));
                        }
                    }
                    var r = await BioSync.UpsertUsersAsync(_db, device, users);
                    return Ok(new { ok = true, count = r.Total, created = r.Created, updated = r.Updated, linked = r.Linked, newEmployees = r.NewEmployeeCodes });
                }

                case "bios":
                {
                    // دریافت قالب‌های هویتی (اثر انگشت/چهره/کارت/رمز) — ثبت per-device + کپی مرجع
                    var bios = new List<RawDeviceBio>();
                    if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("bios", out var barr) && barr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var b in barr.EnumerateArray())
                        {
                            string pin = Str(b, "pin");
                            if (pin.Length == 0) continue;
                            string type = Str(b, "type").Length > 0 ? Str(b, "type") : "fp";
                            string? dt = null; int len = 0;
                            if (b.TryGetProperty("data", out var dEl) && dEl.ValueKind == JsonValueKind.String)
                            {
                                dt = dEl.GetString();
                                len = type == "card" ? (dt?.Length ?? 0)
                                    : SafeB64Len(dt);
                            }
                            bios.Add(new RawDeviceBio(
                                pin, type, Int(b, "index"),
                                b.TryGetProperty("valid", out var vEl) && vEl.ValueKind == JsonValueKind.Number ? vEl.GetInt32() : 1,
                                b.TryGetProperty("quality", out var qEl) && qEl.ValueKind == JsonValueKind.Number ? qEl.GetInt32() : null,
                                dt, len));
                        }
                    }
                    var rb = await BioSync.UpsertBiosAsync(_db, device, bios);
                    return Ok(new { ok = true, count = rb.Total, deviceCreated = rb.DeviceCreated, deviceUpdated = rb.DeviceUpdated, employees = rb.EmployeeIds.Count });
                }

                case "ack":
                {
                    string? commandId = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("commandId", out var cid) ? cid.GetString() : null;
                    bool ok = data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("ok", out var okEl) || okEl.ValueKind != JsonValueKind.False;
                    string? result = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("result", out var resEl) ? resEl.GetString() : null;
                    var cmd = await _db.Set<DeviceCommand>().FirstOrDefaultAsync(c => c.Id == commandId);
                    if (cmd is null || cmd.DeviceId != device.Id)
                        return NotFound(new { error = "دستور یافت نشد" });
                    cmd.Status = ok ? "acked" : "failed";
                    cmd.AckedAt = DateTime.Now;
                    cmd.Result = result ?? (ok ? "با موفقیت اجرا شد" : "دستگاه اجرای دستور را رد کرد");
                    await _db.SaveChangesAsync();
                    return Ok(new { ok = true });
                }

                default:
                    return BadRequest(new { error = "رویداد نامعتبر" });
            }
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "خطای پردازش داده دستگاه" });
        }
    }

    // ---------- GET /api/devices: فهرست دستگاه‌ها (برای sweeper سرویس پل) ----------
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List()
    {
        await DeviceOps.SweepOfflineAsync(_db);
        var devices = await _db.Set<Device>()
            .Where(d => d.Active)
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id, d.Serial, d.Name, d.Ip, d.Location, d.Mode, d.Purpose,
                d.Brand, d.LastSeenAt, d.PollingInterval, d.Firmware, d.UserCount,
            })
            .ToListAsync();
        return Ok(new
        {
            devices = devices.Select(d => new
            {
                d.Id, d.Serial, d.Name, d.Ip,
                online = DeviceOps.IsOnline(new Device
                {
                    LastSeenAt = d.LastSeenAt, PollingInterval = d.PollingInterval,
                }),
                mode = d.LastSeenAt is not null &&
                       (DateTime.Now - d.LastSeenAt.Value).TotalSeconds < 180 ? d.Mode : "آفلاین",
            }),
        });
    }

    // ---------- helpers ----------

    private static string Str(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString() ?? "" : "";

    private static int Int(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.Number
            ? e.GetInt32() : 0;

    private static int SafeB64Len(string? b64)
    {
        if (string.IsNullOrEmpty(b64)) return 0;
        try { return Convert.FromBase64String(b64).Length; }
        catch { return 0; }
    }

    private async Task<(Platform.Web.Domain.Device? Device, int Status)> AuthenticateAsync(string? sn)
    {
        var deviceToken = Request.Headers["X-Device-Token"].FirstOrDefault();
        var (device, err) = await DeviceOps.AuthenticateDeviceAsync(_db, sn, _cu.Get(), deviceToken);
        return (device, err ?? 200);
    }

    private IActionResult ApiError(int status) => status switch
    {
        400 => BadRequest(new { error = "SN دستگاه الزامی است" }),
        401 => Unauthorized(new { error = "توکن دستگاه نامعتبر است" }),
        _ => NotFound(new { error = "دستگاه یافت نشد یا غیرفعال است" }),
    };
}
