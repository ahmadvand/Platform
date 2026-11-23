using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Services;
using Platform.Modules.Parking.Data;
using Platform.Modules.Parking.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers.Api;

/// <summary>
/// پیاده‌سازی خدمات دوربین‌های پلاک‌خوان برای دروازهٔ دستگاه‌ها (فاز ۶۳) —
/// عین همان مسیرهای AnprWebhookController (MarkSeen/صف دستورات/ACK/موتور پلاک)
/// ولی درون‌پردازشی. فقط وقتی ثبت می‌شود که ماژول پارکینگ در بسته باشد
/// (ParkingBootstrapper)؛ در بستهٔ بدون پارکینگ این سرویس وجود ندارد و
/// دروازه با پاسخ دوستانه «ماژول پارکینگ نصب نیست» کنار می‌رود.
/// </summary>
public sealed class ParkingGatewaySink : IParkingGatewaySink
{
    private readonly ParkingDbContext _db;
    private readonly HrBaseDbContext _baseDb;

    public ParkingGatewaySink(ParkingDbContext db, HrBaseDbContext baseDb)
    { _db = db; _baseDb = baseDb; }

    // ---------- فهرست دوربین‌ها (با sweep آفلاین‌ها — مثل GET api/anpr) ----------

    public async Task<List<GatewayCameraCfg>> GetCamerasAsync()
    {
        await ParkingOps.SweepOfflineAsync(_db, _baseDb);
        return await _db.AnprCameras
            .Where(c => c.Active)
            .OrderBy(c => c.Name)
            .Select(c => new GatewayCameraCfg(
                c.Serial, c.Name, c.Ip, c.Brand, c.GateId,
                c.HeartbeatInterval > 0 ? c.HeartbeatInterval : 30))
            .ToListAsync();
    }

    // ---------- ضربان + صف دستورات (مثل GET api/anpr/webhook) ----------

    public async Task<List<GatewayCommandDto>> PullCameraCommandsAsync(string serial, bool consume = true)
    {
        var cam = await FindAsync(serial);
        if (cam is null) return new List<GatewayCommandDto>();

        await ParkingOps.MarkSeenAsync(_db, _baseDb, cam);

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

        return pending.Select(c =>
        {
            Dictionary<string, object?>? payload = null;
            if (!string.IsNullOrEmpty(c.Payload))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.Payload,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { payload = null; }
            }
            return new GatewayCommandDto(c.Id, c.Type, payload);
        }).ToList();
    }

    // ---------- ACK دستور دوربین (مثل رویداد ack webhook) ----------

    public async Task AckCameraCommandAsync(string serial, string commandId, bool ok, string? result)
    {
        var cam = await FindAsync(serial);
        if (cam is null) return;
        var cmd = await _db.AnprCommands.FirstOrDefaultAsync(c => c.Id == commandId);
        if (cmd is null || cmd.CameraId != cam.Id) return;
        cmd.Status = ok ? "acked" : "failed";
        cmd.AckedAt = DateTime.Now;
        cmd.Result = result ?? (ok ? "با موفقیت اجرا شد" : "دوربین اجرای دستور را رد کرد");
        await _db.SaveChangesAsync();
    }

    // ---------- خوانش پلاک (مثل رویداد plate webhook — موتور مجوز) ----------

    public async Task<(int Status, string? Body)> IngestPlateAsync(string serial, object payload)
    {
        var cam = await FindAsync(serial);
        if (cam is null)
            return (404, JsonSerializer.Serialize(new { ok = false, error = "دوربین یافت نشد یا غیرفعال است" }));

        await ParkingOps.MarkSeenAsync(_db, _baseDb, cam);

        // payload از پل ANPR می‌آید: خوانش تکی یا دسته‌ای — قرارداد GatewayPlateRead/Batch
        var reads = new List<RawPlateRead>();
        if (payload is GatewayPlateBatch batch)
        {
            foreach (var r in batch.Reads)
                reads.Add(new RawPlateRead(r.Plate ?? "", r.Direction, r.Timestamp, r.Confidence));
        }
        else if (payload is GatewayPlateRead single)
        {
            reads.Add(new RawPlateRead(single.Plate ?? "", single.Direction, single.Timestamp, single.Confidence));
        }
        else if (payload is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            // مسیر سازگاری — payload خام JSON
            if (je.TryGetProperty("reads", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in arr.EnumerateArray())
                    reads.Add(new RawPlateRead(
                        p.TryGetProperty("plate", out var pl) ? pl.GetString() ?? "" : "",
                        p.TryGetProperty("direction", out var dr) && dr.ValueKind == JsonValueKind.String ? dr.GetString() : null,
                        p.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String ? ts.GetString() : null,
                        p.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number ? cf.GetInt32() : null));
            }
            else
            {
                reads.Add(new RawPlateRead(
                    je.TryGetProperty("plate", out var pl) ? pl.GetString() ?? "" : "",
                    je.TryGetProperty("direction", out var dr) && dr.ValueKind == JsonValueKind.String ? dr.GetString() : null,
                    je.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String ? ts.GetString() : null,
                    je.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number ? cf.GetInt32() : null));
            }
        }

        var res = await ParkingOps.IngestPlateReadsAsync(_db, _baseDb, cam, reads);
        var ev = res.LastEvent;
        var body = JsonSerializer.Serialize(new
        {
            ok = true,
            okCount = res.Ok,
            failed = res.Failed,
            unknownPlates = res.UnknownPlates,
            denied = res.Denied,
            result = ev?.Result,
            reason = ev?.Reason,
            direction = ev?.Direction,
            barrier = ev is { Result: "مجاز" } ? "open" : "closed",
            owner = res.Vehicle?.Employee?.FullName,
        });
        return (200, body);
    }

    private async Task<AnprCamera?> FindAsync(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        return await _db.AnprCameras.FirstOrDefaultAsync(c => c.Serial == serial && c.Active);
    }
}
