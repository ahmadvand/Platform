using System.Globalization;
using System.Text.Json;
using HrModules.DeviceGateway.Protocol;
using Platform.Contracts;

// ======================================================================
// فاز ۶۳ — چرخهٔ poll TCP یک دستگاه (پورت دقیق DeviceWorker سرویس ویندوزی)
// ----------------------------------------------------------------------
// تنها تفاوت با سرویس مستقل: به‌جای ApiClient (HTTP + حساب svc_bridge)
// همان داده از IDeviceGatewayChannel درون‌پردازشی می‌گذرد — یعنی همان
// مسیر webhook (DeviceOps.IngestPunchesAsync / BioSync / ack) بدون شبکه.
// ======================================================================
namespace HrModules.DeviceGateway.Runtime;

public static class ZkPoller
{
    /// <summary>یک چرخهٔ کامل poll: اتصال → اجرای دستورات → خواندن رکوردها → گزارش وضعیت.</summary>
    public static async Task PollAsync(GatewayDeviceCfg dev, GatewayRuntime rt,
        IDeviceGatewayChannel channel, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(dev.Ip)) return;
        int port = dev.Port is > 0 ? dev.Port.Value : rt.Config.DefaultDevicePort;
        int commKey = dev.CommKey is >= 0 ? dev.CommKey.Value : rt.Config.DefaultCommKey;
        var live = rt.LiveOf(dev.Serial, dev.Name, dev.Transport);

        using var client = new ZkTcpClient(Math.Max(2000, rt.Config.TcpTimeoutMs));
        try
        {
            await client.ConnectAsync(dev.Ip!, port, ct);
            await client.LoginAsync(commKey, ct);
            live.Online = true;
            live.LastError = null;
            live.LastPoll = DateTime.Now;
            rt.Log("INFO", dev.Serial, $"اتصال TCP برقرار شد ({dev.Ip}:{port})");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            live.Online = false;
            live.LastError = ex.Message;
            live.LastPoll = DateTime.Now;
            rt.Log("WARN", dev.Serial, "اتصال TCP ناموفق: " + ex.Message);
            try { await channel.PostStatusAsync(dev.Serial, null, null, false); } catch { /* چرخهٔ بعدی */ }
            return;
        }

        try
        {
            // ۱) اجرای دستورات صف‌شدهٔ پنل
            var cmds = await channel.PullCommandsAsync(dev.Serial);
            live.PendingCommands = 0;
            foreach (var c in cmds)
                await ExecuteCommandAsync(dev, client, c, channel, rt, live, ct);

            // ۲) خواندن رکوردهای جدید تردد (backfill)
            await PullRecordsAsync(dev, client, channel, rt, live, ct);

            // ۳) شمارش کاربران و گزارش وضعیت
            string? firmware = null;
            int? userCount = null;
            try { var sizes = await client.ReadSizesAsync(ct); userCount = sizes.Users; }
            catch { /* اختیاری */ }
            try { firmware = await client.GetVersionAsync(ct); }
            catch { /* اختیاری */ }
            await channel.PostStatusAsync(dev.Serial, firmware, userCount, true);
            live.Online = true;
            live.LastPoll = DateTime.Now;
        }
        finally
        {
            try { await client.DisconnectAsync(CancellationToken.None); } catch { /* بستن اتصال */ }
        }
    }

    /// <summary>اجرای یک دستور پنل روی دستگاه از مسیر TCP — همان شاخه‌های سرویس ویندوزی.</summary>
    public static async Task ExecuteCommandAsync(GatewayDeviceCfg dev, ZkTcpClient client,
        GatewayCommandDto c, IDeviceGatewayChannel channel, GatewayRuntime rt,
        DeviceLiveState live, CancellationToken ct)
    {
        bool ok = true;
        string result = "اجرا شد (TCP)";
        try
        {
            switch (c.Type)
            {
                case "unlock":
                    int seconds = GetInt(c.Payload, "seconds", 5);
                    await client.UnlockAsync(seconds, ct);
                    result = $"درب به مدت {seconds} ثانیه باز شد";
                    break;

                case "sync_time":
                    DateTime? dt = GetDate(c.Payload, "datetime");
                    await client.SetTimeAsync(dt, ct);
                    result = "ساعت دستگاه با سرور همگام شد";
                    break;

                case "restart":
                    await client.RestartAsync(ct);
                    result = "دستگاه ری‌استارت می‌شود";
                    break;

                case "refresh_data":
                    await client.RefreshDataAsync(ct);
                    result = "داده‌های داخلی دستگاه نوسازی شد";
                    break;

                case "get_users":
                {
                    var users = await client.GetUsersAsync(ct);
                    await channel.PostUsersAsync(dev.Serial, users.Select(u => new GatewayUserDto(
                        u.UserId, u.Name, u.Uid, u.Privilege,
                        u.Card != 0 ? u.Card.ToString(CultureInfo.InvariantCulture) : null,
                        string.IsNullOrEmpty(u.Password) ? null : u.Password,
                        u.GroupId)).ToList());
                    result = $"{users.Count} کاربر از دستگاه خوانده و به سامانه ثبت شد";
                    break;
                }

                case "get_user":
                {
                    string pinQ = GetStr(c.Payload, "pin");
                    var allUsers = await client.GetUsersAsync(ct);
                    var hits = string.IsNullOrEmpty(pinQ)
                        ? new List<ZkUser>()
                        : allUsers.Where(u => u.UserId == pinQ).ToList();
                    if (hits.Count > 0)
                    {
                        await channel.PostUsersAsync(dev.Serial, hits.Select(u => new GatewayUserDto(
                            u.UserId, u.Name, u.Uid, u.Privilege,
                            u.Card != 0 ? u.Card.ToString(CultureInfo.InvariantCulture) : null,
                            string.IsNullOrEmpty(u.Password) ? null : u.Password,
                            u.GroupId)).ToList());
                        result = $"کاربر {pinQ} از دستگاه خوانده شد ({hits.Count} رکورد)";
                    }
                    else
                    {
                        result = $"کاربر {pinQ} روی دستگاه یافت نشد";
                    }
                    break;
                }

                case "enable_device":
                    await client.EnableDeviceAsync(ct);
                    result = "دستگاه فعال شد — پذیرش تردد از سر گرفته شد";
                    break;

                case "disable_device":
                    await client.DisableDeviceAsync(ct);
                    result = "دستگاه غیرفعال شد (حالت تعمیرات — صفحه قفل است)";
                    break;

                case "get_user_bios":
                {
                    var users = await client.GetUsersAsync(ct);
                    var pins = GetPinFilter(c.Payload);
                    if (pins.Count > 0) users = users.Where(u => pins.Contains(u.UserId)).ToList();

                    var bios = new List<GatewayBioDto>();
                    foreach (var u in users)
                    {
                        // اثر انگشت — اسلات ۰ و ۱
                        for (int idx = 0; idx < 2; idx++)
                        {
                            try
                            {
                                var t = await client.GetUserTemplateAsync(u.Uid, u.UserId, idx, 0, ct);
                                if (t is not null && t.Template.Length > 0)
                                    bios.Add(new GatewayBioDto(u.UserId, "fp", idx, t.Valid, null,
                                        Convert.ToBase64String(t.Template), t.Template.Length));
                            }
                            catch { /* قالبی در این اسلات ثبت نشده */ }
                        }
                        // چهره — اسلات ۰ (دستگاه‌های بدون چهره خطا می‌دهند و رد می‌شود)
                        try
                        {
                            var f = await client.GetUserTemplateAsync(u.Uid, u.UserId, 0, 1, ct);
                            if (f is not null && f.Template.Length > 0)
                                bios.Add(new GatewayBioDto(u.UserId, "face", 0, f.Valid, null,
                                    Convert.ToBase64String(f.Template), f.Template.Length));
                        }
                        catch { /* پشتیبانی چهره ندارد */ }
                        // کارت و رمز — از رکورد کاربر
                        if (u.Card != 0)
                        {
                            string card = u.Card.ToString(CultureInfo.InvariantCulture);
                            bios.Add(new GatewayBioDto(u.UserId, "card", 0, 1, null, card, card.Length));
                        }
                        if (!string.IsNullOrEmpty(u.Password))
                            bios.Add(new GatewayBioDto(u.UserId, "pin", 0, 1, null, u.Password, u.Password.Length));
                    }
                    await channel.PostBiosAsync(dev.Serial, bios);
                    result = $"{bios.Count} قالب هویتی برای {users.Count} کاربر خوانده و ثبت شد";
                    break;
                }

                case "push_user_bios":
                {
                    var users = JsonList(c.Payload, "users");
                    var bios = JsonList(c.Payload, "bios");
                    if (users.Count == 0) { result = "هیچ کاربری برای ارسال تعیین نشده بود"; break; }

                    var existing = await client.GetUsersAsync(ct);
                    int nextUid = existing.Count > 0 ? existing.Max(u => u.Uid) + 1 : 1;
                    int pushed = 0, tplCount = 0;
                    foreach (var ju in users)
                    {
                        string pin = JStr(ju, "pin");
                        if (pin.Length == 0) continue;
                        string name = JStr(ju, "name");
                        uint card = uint.TryParse(JStr(ju, "card"), NumberStyles.Any, CultureInfo.InvariantCulture, out var cc) ? cc : 0;
                        string password = JStr(ju, "password");
                        string group = JStr(ju, "group");
                        if (group.Length == 0) group = "1";

                        var ex = existing.FirstOrDefault(x => x.UserId == pin);
                        int uid = ex?.Uid ?? nextUid++;
                        await client.SetUserAsync(uid, name.Length > 0 ? name : $"NN-{pin}", pin,
                            ZkConsts.UserDefault, password, group, card, ct);
                        pushed++;

                        foreach (var jb in bios.Where(b => JStr(b, "pin") == pin))
                        {
                            string type = JStr(jb, "type");
                            if (type is not ("fp" or "face")) continue;
                            string data = JStr(jb, "data");
                            if (data.Length == 0) continue;
                            byte[] tpl;
                            try { tpl = Convert.FromBase64String(data); }
                            catch { continue; }
                            try
                            {
                                await client.SetUserTemplateAsync(uid, pin, JInt(jb, "index"),
                                    JInt(jb, "valid") == 0 ? 0 : 1, type == "face" ? 1 : 0, tpl, ct);
                                tplCount++;
                            }
                            catch (Exception ex2)
                            {
                                rt.Log("WARN", dev.Serial, $"نوشتن قالب {type}[{JInt(jb, "index")}] کاربر {pin} ناموفق: {ex2.Message}");
                            }
                        }
                    }
                    result = $"{pushed} کاربر + {tplCount} قالب هویتی روی دستگاه ثبت شد";
                    break;
                }

                case "push_user":
                {
                    string pin = GetStr(c.Payload, "pin");
                    string name = GetStr(c.Payload, "name");
                    uint card = (uint)GetInt(c.Payload, "card", 0);
                    var users = await client.GetUsersAsync(ct);
                    var existing = users.FirstOrDefault(u => u.UserId == pin);
                    int uid = existing?.Uid ?? (users.Count > 0 ? users.Max(u => u.Uid) + 1 : 1);
                    await client.SetUserAsync(uid, string.IsNullOrEmpty(name) ? $"NN-{pin}" : name, pin,
                        ZkConsts.UserDefault, card: card, ct: ct);
                    result = $"کاربر {pin} روی دستگاه ثبت شد (uid={uid})";
                    break;
                }

                case "delete_user":
                {
                    string pin = GetStr(c.Payload, "pin");
                    bool removed = await client.DeleteUserByPinAsync(pin, ct);
                    ok = removed;
                    result = removed ? $"کاربر {pin} از دستگاه حذف شد" : $"کاربر {pin} روی دستگاه یافت نشد";
                    break;
                }

                case "clear_attendance":
                    await client.ClearAttendanceAsync(ct);
                    result = "رکوردهای تردد دستگاه پاک شد";
                    break;

                case "clear_data":
                    await client.ClearDataAsync(ct);
                    result = "کل داده‌های دستگاه پاک شد";
                    break;

                default:
                    ok = false;
                    result = $"نوع دستور {c.Type} در حالت TCP پشتیبانی نمی‌شود";
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ok = false;
            result = ex.Message;
            rt.Log("ERROR", dev.Serial, $"خطای اجرای دستور {c.Type}: {ex.Message}");
        }

        if (ok) Interlocked.Increment(ref live.OkCommands); else Interlocked.Increment(ref live.FailedCommands);
        try { await channel.PostAckAsync(dev.Serial, c.Id, ok, result); } catch { /* صف بعدی */ }
        rt.Log(ok ? "INFO" : "WARN", dev.Serial, $"دستور {c.Type}: {result}");
    }

    /// <summary>خواندن رکوردهای تردد از حافظه دستگاه و ورود به سامانه.</summary>
    private static async Task PullRecordsAsync(GatewayDeviceCfg dev, ZkTcpClient client,
        IDeviceGatewayChannel channel, GatewayRuntime rt, DeviceLiveState live, CancellationToken ct)
    {
        List<ZkAttendance> records;
        try
        {
            records = await client.GetAttendanceAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            rt.Log("WARN", dev.Serial, "خواندن رکوردهای تردد ناموفق: " + ex.Message);
            return;
        }
        if (records.Count == 0) return;

        var punches = records.Select(r => new GatewayPunchDto(
            r.UserId,
            r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            r.Punch > 0 ? "out" : "in")).ToList();

        int sent;
        try { sent = await channel.PostPunchesAsync(dev.Serial, punches); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            rt.Log("WARN", dev.Serial, "ورود رکوردها به سامانه ناموفق: " + ex.Message);
            return;
        }
        Interlocked.Add(ref live.PunchesTotal, sent);
        rt.Log("INFO", dev.Serial, $"{sent} رکورد تردد از دستگاه ورود شد ({punches.Count} خوانده‌شده)");
    }

    // ---------- استخراج payload (پورت دقیق DeviceWorker) ----------

    private static string GetStr(Dictionary<string, object?>? p, string key) =>
        p is not null && p.TryGetValue(key, out var v) && v is not null ? v.ToString() ?? "" : "";

    private static int GetInt(Dictionary<string, object?>? p, string key, int def) =>
        int.TryParse(GetStr(p, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;

    private static DateTime? GetDate(Dictionary<string, object?>? p, string key)
    {
        string s = GetStr(p, key);
        if (s.Length == 0) return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    private static List<JsonElement> JsonList(Dictionary<string, object?>? p, string key)
    {
        if (p is null || !p.TryGetValue(key, out var v) || v is not JsonElement je || je.ValueKind != JsonValueKind.Array)
            return new List<JsonElement>();
        return je.EnumerateArray().ToList();
    }

    private static string JStr(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString() ?? "" : "";

    private static int JInt(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.Number
            ? e.GetInt32() : 0;

    private static List<string> GetPinFilter(Dictionary<string, object?>? p)
    {
        if (p is null || !p.TryGetValue("pins", out var v) || v is not JsonElement je || je.ValueKind != JsonValueKind.Array)
            return new List<string>();
        return je.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();
    }
}
