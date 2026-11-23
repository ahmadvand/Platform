using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Services;
using Platform.Modules.Parking.Data;
using Platform.Modules.Parking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers.Api;

/// <summary>
/// پل ارتباط دوربین‌های پلاک‌خوان (فاز ۴۱) — همان قرارداد پل دستگاه‌های تردد:
/// GET  api/anpr/webhook?SN=... با Bearer سرویس پل یا X-Device-Token → ضربان + تحویل دستورات صف‌شده
/// POST api/anpr/webhook { SN, event: "ping|status|plate|ack", data }
/// رویداد «plate» = پلاک خوانده‌شده → ثبت خودکار ورود/خروج با موتور مجوز پارکینگ.
/// فاز ۶۱ — کانتکست دیتای پارکینگ + HrBaseDbContext (هشدارهای SecurityAlert در MarkSeen/Sweep).
/// </summary>
[ApiController]
[Route("api/anpr")]
public class AnprWebhookController : ControllerBase
{
    private readonly ParkingDbContext _db;
    private readonly HrBaseDbContext _baseDb;
    private readonly ICurrentUser _cu;

    public AnprWebhookController(ParkingDbContext db, HrBaseDbContext baseDb, ICurrentUser cu)
    { _db = db; _baseDb = baseDb; _cu = cu; }

    // ---------- GET: ضربان + تحویل دستورات صف‌شده ----------
    [HttpGet("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCommands([FromQuery] string? SN, [FromQuery] bool consume = true)
    {
        var (cam, status) = await ParkingOps.AuthenticateCameraAsync(_db, SN, _cu.Get(),
            Request.Headers["X-Device-Token"].FirstOrDefault());
        if (cam is null) return ApiError(status ?? 404);

        await ParkingOps.MarkSeenAsync(_db, _baseDb, cam);

        // دستورات در انتظار — consume=false فقط نمایش (گزارش‌گیر)، وگرنه مصرف واقعی
        var pending = await _db.AnprCommands
            .Where(c => c.CameraId == cam.Id && c.Status == "pending")
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
            gate = cam.GateId,
            commands = pending.Select(c =>
            {
                object? payload = string.IsNullOrEmpty(c.Payload)
                    ? null
                    : (object?)JsonDocument.Parse(c.Payload).RootElement;
                return new { id = c.Id, type = c.Type, payload };
            }),
        });
    }

    // ---------- POST: رخدادهای دوربین ----------
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PostEvent([FromBody] JsonElement body)
    {
        try
        {
            string? sn = body.TryGetProperty("SN", out var snEl) ? snEl.GetString() : null;
            string? @event = body.TryGetProperty("event", out var evEl) ? evEl.GetString() : null;
            var data = body.TryGetProperty("data", out var dataEl) ? dataEl : default;

            var (cam, status) = await ParkingOps.AuthenticateCameraAsync(_db, sn, _cu.Get(),
                Request.Headers["X-Device-Token"].FirstOrDefault());
            if (cam is null) return ApiError(status ?? 404);
            await ParkingOps.MarkSeenAsync(_db, _baseDb, cam);

            switch (@event)
            {
                case "ping":
                case "status":
                {
                    await ParkingOps.MarkSeenAsync(_db, _baseDb, cam);
                    return Ok(new { ok = true });
                }

                case "plate":
                {
                    // پلاک خوانده‌شده — ثبت خودکار ورود/خروج؛ خوانده‌های دسته‌ای هم پشتیبانی می‌شوند
                    var reads = new List<RawPlateRead>();
                    if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("reads", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var p in arr.EnumerateArray())
                        {
                            reads.Add(new RawPlateRead(
                                p.TryGetProperty("plate", out var pl) ? pl.GetString() ?? "" : "",
                                p.TryGetProperty("direction", out var dr) && dr.ValueKind == JsonValueKind.String ? dr.GetString() : null,
                                p.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String ? ts.GetString() : null,
                                p.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number ? cf.GetInt32() : null));
                        }
                    }
                    else if (data.ValueKind == JsonValueKind.Object)
                    {
                        reads.Add(new RawPlateRead(
                            data.TryGetProperty("plate", out var pl) ? pl.GetString() ?? "" : "",
                            data.TryGetProperty("direction", out var dr) && dr.ValueKind == JsonValueKind.String ? dr.GetString() : null,
                            data.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String ? ts.GetString() : null,
                            data.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number ? cf.GetInt32() : null));
                    }
                    var r = await ParkingOps.IngestPlateReadsAsync(_db, _baseDb, cam, reads);
                    var ev = r.LastEvent;
                    return Ok(new
                    {
                        ok = true,
                        okCount = r.Ok,
                        failed = r.Failed,
                        unknownPlates = r.UnknownPlates,
                        denied = r.Denied,
                        result = ev?.Result,
                        reason = ev?.Reason,
                        direction = ev?.Direction,
                        barrier = ev is { Result: "مجاز" } ? "open" : "closed",
                        owner = r.Vehicle?.Employee?.FullName,
                    });
                }

                case "ack":
                {
                    string? commandId = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("commandId", out var cid) ? cid.GetString() : null;
                    bool ok = data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("ok", out var okEl) || okEl.ValueKind != JsonValueKind.False;
                    string? result = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("result", out var resEl) ? resEl.GetString() : null;
                    var cmd = await _db.AnprCommands.FirstOrDefaultAsync(c => c.Id == commandId);
                    if (cmd is null || cmd.CameraId != cam.Id)
                        return NotFound(new { error = "دستور یافت نشد" });
                    cmd.Status = ok ? "acked" : "failed";
                    cmd.AckedAt = DateTime.Now;
                    cmd.Result = result ?? (ok ? "با موفقیت اجرا شد" : "دوربین اجرای دستور را رد کرد");
                    await _db.SaveChangesAsync();
                    return Ok(new { ok = true });
                }

                default:
                    return BadRequest(new { error = "رویداد نامعتبر — ping | status | plate | ack" });
            }
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "خطای پردازش داده دوربین" });
        }
    }

    // ---------- GET api/anpr: فهرست دوربین‌ها (برای sweeper سرویس پل) ----------
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List()
    {
        await ParkingOps.SweepOfflineAsync(_db, _baseDb);
        var cams = await _db.AnprCameras
            .Where(c => c.Active)
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Serial, c.Name, c.Ip, c.Location, c.Brand, c.GateId,
                c.Mode, c.HeartbeatInterval, c.LastSeenAt, c.ConfidenceThreshold,
            })
            .ToListAsync();
        return Ok(new
        {
            cameras = cams.Select(c => new
            {
                c.Id, c.Serial, c.Name, c.Ip, c.GateId,
                online = ParkingOps.IsOnline(new AnprCamera
                {
                    LastSeenAt = c.LastSeenAt, HeartbeatInterval = c.HeartbeatInterval,
                }),
                mode = c.LastSeenAt is not null &&
                       (DateTime.Now - c.LastSeenAt.Value).TotalSeconds < 180 ? c.Mode : "آفلاین",
            }),
        });
    }

    private IActionResult ApiError(int status) => status switch
    {
        400 => BadRequest(new { error = "SN دوربین الزامی است" }),
        401 => Unauthorized(new { error = "توکن دوربین نامعتبر است" }),
        _ => NotFound(new { error = "دوربین یافت نشد یا غیرفعال است" }),
    };
}
