using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

/// <summary>
/// پیاده‌سازی کانال دروازه روی پلتفرم (فاز ۶۳) — عین همان مسیرهایی که
/// webhook سرویس ویندوزی طی می‌کرد (DeviceOps/BioSync/GatewayConfig) ولی
/// درون‌پردازشی: دروازهٔ درون‌اپی فراخوانِ مورد اعتماد خود اپ است و به
/// توکن دستگاه/نشست svc_bridge نیازی ندارد. دستگاه ناشناس = بی‌عمل (no-op).
/// </summary>
public sealed class DeviceGatewayChannel : IDeviceGatewayChannel
{
    private readonly HrBaseDbContext _base;
    private readonly AppDbContext _db;
    private readonly IServiceProvider _sp;
    private readonly ILogger<DeviceGatewayChannel> _log;

    // فاز ۶۷ — گیت‌وی حضور فقط با لایهٔ دیتای ماژول حضور ثبت می‌شود؛ حل تنبل در نقطهٔ استفاده
    private IAttendanceGateway? Att => _sp.GetService<IAttendanceGateway>();

    public DeviceGatewayChannel(HrBaseDbContext baseDb, AppDbContext db,
        IServiceProvider sp, ILogger<DeviceGatewayChannel> log)
    { _base = baseDb; _db = db; _sp = sp; _log = log; }

    // ---------- تنظیمات (همان ردیف default پنل تنظیمات گیت‌وی) ----------

    public async Task<GatewaySettingsDto> GetSettingsAsync()
    {
        var s = await _db.Set<GatewaySetting>().FirstOrDefaultAsync(x => x.Id == "default");
        if (s is null)
        {
            s = new GatewaySetting { Id = "default" };
            _db.Set<GatewaySetting>().Add(s);
            await _db.SaveChangesAsync();
        }
        // نشانگر سلامت پنل تنظیمات گیت‌وی — دروازهٔ درون‌اپی هم «دریافت کانفیگ» را ثبت می‌کند
        if (s.LastFetchedAt is null || (DateTime.Now - s.LastFetchedAt.Value).TotalSeconds > 10)
        {
            s.LastFetchedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }
        return new GatewaySettingsDto(
            s.Enabled, s.TcpTimeoutMs, s.PollIntervalSec, s.SweeperIntervalSec,
            s.ConfigRefreshSec, s.DefaultDevicePort, s.DefaultCommKey,
            s.ConfigVersion, s.LogLevel, s.ListenPort);
    }

    // ---------- دستگاه‌ها (با sweep آفلاین‌ها — مثل GET api/devices) ----------

    public async Task<List<GatewayDeviceCfg>> GetDevicesAsync()
    {
        await DeviceOps.SweepOfflineAsync(_db);
        return await _db.Set<Device>()
            .Where(d => d.Active)
            .OrderBy(d => d.Name)
            .Select(d => new GatewayDeviceCfg(
                d.Serial, d.Name, d.Transport, d.Ip, d.DevicePort, d.CommKey,
                d.PollingInterval > 0 ? d.PollingInterval : 60))
            .ToListAsync();
    }

    // ---------- دستورات صف (مثل GET webhook) ----------

    public async Task<List<GatewayCommandDto>> PullCommandsAsync(string serial, bool consume = true)
    {
        var device = await FindAsync(serial);
        if (device is null) return new List<GatewayCommandDto>();

        await DeviceOps.MarkSeenAsync(_db, device);
        device.LastPulledAt = DateTime.Now;
        await _db.SaveChangesAsync();

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

    // ---------- رویدادها (مثل POST webhook) ----------

    public async Task PostStatusAsync(string serial, string? firmware, int? userCount, bool online)
    {
        var device = await FindAsync(serial);
        if (device is null) return;
        if (online)
            await DeviceOps.MarkSeenAsync(_db, device, firmware, userCount);
        // online=false → هیچ؛ سوئیپر خودش آفلاین را از LastSeenAt قدیمی می‌یابد
    }

    public async Task<int> PostPunchesAsync(string serial, List<GatewayPunchDto> punches)
    {
        var device = await FindAsync(serial);
        if (device is null) return 0;
        await DeviceOps.MarkSeenAsync(_db, device);
        var att = Att;
        if (att is null)
        {
            _log.LogWarning("دروازه: پانچ دستگاه {Serial} نادیده شد — ماژول حضور در این نسخه نصب نیست", serial);
            return 0;
        }
        var raw = punches.Select(p => new RawPunch(p.Pin, p.Timestamp, p.Status)).ToList();
        var r = await DeviceOps.IngestPunchesAsync(_db, att, device.Id, raw);
        if (r.UnknownPins.Count > 0)
            _log.LogWarning("دروازه: {Count} پانچ با پین ناشناس از دستگاه {Serial}", r.UnknownPins.Count, serial);
        return r.Ok;
    }

    public async Task PostUsersAsync(string serial, List<GatewayUserDto> users)
    {
        var device = await FindAsync(serial);
        if (device is null) return;
        await DeviceOps.MarkSeenAsync(_db, device);
        var raw = users.Select(u => new RawDeviceUser(
            u.Uid, u.Pin,
            string.IsNullOrEmpty(u.Name) ? u.Pin : u.Name,
            u.Privilege, u.Card, u.Password, u.Group)).ToList();
        await BioSync.UpsertUsersAsync(_db, device, raw);
    }

    public async Task PostBiosAsync(string serial, List<GatewayBioDto> bios)
    {
        var device = await FindAsync(serial);
        if (device is null) return;
        await DeviceOps.MarkSeenAsync(_db, device);
        var raw = bios.Select(b => new RawDeviceBio(
            b.Pin, b.Type, b.Index, b.Valid, b.Quality, b.Data, b.Length)).ToList();
        await BioSync.UpsertBiosAsync(_db, device, raw);
    }

    public async Task PostAckAsync(string serial, string commandId, bool ok, string? result)
    {
        var device = await FindAsync(serial);
        if (device is null) return;
        var cmd = await _db.Set<DeviceCommand>().FirstOrDefaultAsync(c => c.Id == commandId);
        if (cmd is null || cmd.DeviceId != device.Id) return;
        cmd.Status = ok ? "acked" : "failed";
        cmd.AckedAt = DateTime.Now;
        cmd.Result = result ?? (ok ? "با موفقیت اجرا شد" : "دستگاه اجرای دستور را رد کرد");
        await _db.SaveChangesAsync();
    }

    // ---------- پلاک (به ماژول پارکینگ — وقتی نصب است) ----------

    public async Task<(int Status, string? Body)> PostPlateAsync(string serial, object payload)
    {
        var sink = _sp.GetService<IParkingGatewaySink>();
        if (sink is null)
            return (503, JsonSerializer.Serialize(new { ok = false, error = "ماژول پارکینگ در این نسخه نصب نیست" }));
        return await sink.IngestPlateAsync(serial, payload);
    }

    private async Task<Device?> FindAsync(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        return await _db.Set<Device>().FirstOrDefaultAsync(d => d.Serial == serial && d.Active);
    }
}
