using System.Globalization;
using System.Text;
using HrModules.DeviceGateway.Runtime;
using Platform.Contracts;

// ======================================================================
// فاز ۶۳ — پل ADMS/PUSH درون‌پردازشی (پورت AdmsBridge سرویس ویندوزی)
// ----------------------------------------------------------------------
// دستگاه‌های ZKTeco با پروتکل PUSH به همان مسیر آشنا وصل می‌شوند؛ تفاوت
// فقط این است که پورت، پورت خودِ وب‌اپ است و داده بی‌واسطه (بدون HTTP
// میانی و احراز svc_bridge) به IDeviceGatewayChannel می‌رسد:
//   GET  /iclock/cdata?SN=...  → handshake + تحویل دستورات متنی (C:{id}:…)
//   POST /iclock/cdata?SN=...  → ATTLOG / USERINFO / ACK های دستگاه
// ======================================================================
namespace HrModules.DeviceGateway.Bridge;

public static class AdmsEndpoints
{
    // ---------- تبدیل دستور سرور به متن پروتکل ADMS (پورت دقیق) ----------

    public static string? CommandToAdms(GatewayCommandDto cmd)
    {
        string id = cmd.Id.Length <= 8 ? cmd.Id : cmd.Id[^8..];
        switch (cmd.Type)
        {
            case "unlock":
                return $"C:{id}:CONTROL DEVICE 01";
            case "restart":
                return $"C:{id}:RESTART";
            case "sync_time":
            {
                var dt = ParsePayloadDate(cmd.Payload) ?? DateTime.Now;
                string s = dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                return $"C:{id}:SET OPTIONS DateTime={s}";
            }
            case "clear_data":
                return $"C:{id}:CLEAR DATA";
            case "refresh_data":
                return $"C:{id}:REFRESH";
            case "push_user":
            {
                string pin = Str(cmd.Payload, "pin");
                string name = Uri.EscapeDataString(Str(cmd.Payload, "name"));
                return $"C:{id}:DATA UPDATE USERINFO PIN={pin}\tName={name}\tPrivilege=0\tVerify=1";
            }
            case "delete_user":
                return $"C:{id}:DATA DELETE USERINFO PIN={Str(cmd.Payload, "pin")}";
            case "get_user":
            {
                // پاسخ دستگاه با POST USERINFO برمی‌گردد و توسط ParseUsers پردازش می‌شود
                string pinQ = Str(cmd.Payload, "pin");
                return string.IsNullOrEmpty(pinQ)
                    ? null
                    : $"C:{id}:DATA QUERY USERINFO PIN={pinQ}";
            }
            case "get_users":
                // PIN=* → همه کاربران با POST USERINFO
                return $"C:{id}:DATA QUERY USERINFO PIN=*";
            case "clear_attendance":
                return $"C:{id}:CLEAR LOG";
            case "enable_device":
                return $"C:{id}:ENABLE DEVICE";
            case "disable_device":
                return $"C:{id}:DISABLE DEVICE";
            default:
                // دستورهای باینری (قالب هویتی) از مسیر ADMS قابل ارسال نیستند — ack ناموفق در GET
                return null;
        }
    }

    private static string Str(Dictionary<string, object?>? p, string key) =>
        p is not null && p.TryGetValue(key, out var v) && v is not null ? v.ToString() ?? "" : "";

    private static DateTime? ParsePayloadDate(Dictionary<string, object?>? p)
    {
        if (p is null || !p.TryGetValue("datetime", out var v) || v is null) return null;
        return DateTime.TryParse(v.ToString(), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt) ? dt : null;
    }

    // ---------- تجزیه داده‌های ارسالی دستگاه (پورت دقیق) ----------

    /// <summary>تجزیه خطوط ATTLOG: pin\ttimestamp\tstatus\tverify</summary>
    public static List<GatewayPunchDto> ParseAttlog(string body)
    {
        var punches = new List<GatewayPunchDto>();
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim('\r', ' ');
            if (line.Length == 0) continue;
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;
            if (!parts[0].All(char.IsDigit) || parts[0].Length == 0) continue;
            punches.Add(new GatewayPunchDto(
                parts[0], parts[1], parts.Length > 2 ? parts[2] : null));
        }
        return punches;
    }

    /// <summary>تجزیه ACK خط فرمان: C:{id}:\t{result} یا C:{id}:OK</summary>
    public static List<(string ShortId, string Result)> ParseAck(string body)
    {
        var acks = new List<(string, string)>();
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim('\r', ' ');
            if (line.StartsWith("C:", StringComparison.Ordinal))
            {
                int secondColon = line.IndexOf(':', 2);
                if (secondColon > 2)
                {
                    string id = line[2..secondColon];
                    string result = line[(secondColon + 1)..].Trim('\t', ' ');
                    if (id.Length > 0) acks.Add((id, result.Length > 0 ? result : "OK"));
                }
            }
        }
        return acks;
    }

    /// <summary>تجزیه خطوط USERINFO (push کاربر از دستگاه): USERINFO PIN=…\tName=…</summary>
    public static List<GatewayUserDto> ParseUsers(string body)
    {
        var users = new List<GatewayUserDto>();
        foreach (var raw in body.Split('\n', '\r'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("USERINFO", StringComparison.OrdinalIgnoreCase)) continue;
            string? pin = null, name = null;
            foreach (var kv in line.Split('\t'))
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0) continue;
                string k = kv[..eq].Trim();
                string v = kv[(eq + 1)..].Trim();
                if (k.Equals("PIN", StringComparison.OrdinalIgnoreCase)) pin = v;
                else if (k.Equals("Name", StringComparison.OrdinalIgnoreCase)) name = Uri.UnescapeDataString(v);
            }
            if (!string.IsNullOrEmpty(pin))
                users.Add(new GatewayUserDto(pin, name, 0, 0, null, null, null));
        }
        return users;
    }

    // ---------- نگاشت endpointها ----------

    public static void Map(WebApplication app)
    {
        app.MapGet("/iclock/cdata", async (HttpContext ctx) =>
        {
            var rt = ctx.RequestServices.GetRequiredService<GatewayRuntime>();
            var avail = ctx.RequestServices.GetRequiredService<IModuleAvailability>();
            if (!rt.Config.FullyEnabled || !avail.Enabled(GatewayWorker.ModuleCode))
                return Results.Text("OK", "text/plain", Encoding.UTF8); // ماژول خاموش — پاسخ بی‌اثر

            string? sn = ctx.Request.Query["SN"].FirstOrDefault();
            if (string.IsNullOrEmpty(sn)) return Results.BadRequest("BAD REQUEST");

            var channel = ctx.RequestServices.GetRequiredService<IDeviceGatewayChannel>();
            var live = rt.LiveOf(sn, null, "adms");
            live.LastPoll = DateTime.Now;

            // handshake + تحویل دستورات صف‌شده
            var cmds = await channel.PullCommandsAsync(sn);
            var outLines = new List<string>();
            foreach (var c in cmds)
            {
                string? text = CommandToAdms(c);
                if (text is null)
                {
                    // دستور قابل ترجمه به ADMS نیست (مثلاً انتقال قالب هویتی) —
                    // بی‌اطلاع رها نمی‌شود: ack ناموفق با پیام روشن تا کاربر بداند چرا اجرا نشد
                    await channel.PostAckAsync(sn, c.Id, false,
                        "این دستور در حالت ADMS قابل اجرا نیست — انتقال قالب هویتی فقط با اتصال TCP مستقیم ممکن است (نحوه اتصال دستگاه را در ویرایش دستگاه تغییر دهید)");
                    rt.Log("WARN", sn, $"دستور {c.Type} قابل ترجمه به ADMS نیست — ack ناموفق ثبت شد");
                    continue;
                }
                outLines.Add(text);
                rt.Pending.Set(text[2..Math.Min(10, text.Length)], c.Id);
                live.PendingCommands++;
            }
            if (outLines.Count > 0)
                rt.Log("INFO", sn, $"تحویل {outLines.Count} دستور به دستگاه (ADMS handshake)");

            return Results.Text(outLines.Count > 0 ? string.Join("\n", outLines) + "\n" : "OK",
                "text/plain", Encoding.UTF8);
        });

        app.MapPost("/iclock/cdata", async (HttpContext ctx) =>
        {
            var rt = ctx.RequestServices.GetRequiredService<GatewayRuntime>();
            var avail = ctx.RequestServices.GetRequiredService<IModuleAvailability>();
            if (!rt.Config.FullyEnabled || !avail.Enabled(GatewayWorker.ModuleCode))
                return Results.Text("OK", "text/plain", Encoding.UTF8);

            string? sn = ctx.Request.Query["SN"].FirstOrDefault();
            if (string.IsNullOrEmpty(sn)) return Results.BadRequest("BAD REQUEST");

            string body;
            using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync();

            var channel = ctx.RequestServices.GetRequiredService<IDeviceGatewayChannel>();
            var live = rt.LiveOf(sn, null, "adms");
            live.LastPoll = DateTime.Now;

            try
            {
                // ۱) ACK دستورات
                foreach (var (shortId, result) in ParseAck(body))
                {
                    string? commandId = rt.Pending.Take(shortId);
                    if (commandId is not null)
                    {
                        bool ok = !System.Text.RegularExpressions.Regex.IsMatch(result, "error|fail",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        await channel.PostAckAsync(sn, commandId, ok, result);
                        if (ok) Interlocked.Increment(ref live.OkCommands); else Interlocked.Increment(ref live.FailedCommands);
                        live.PendingCommands = Math.Max(0, live.PendingCommands - 1);
                        rt.Log("INFO", sn, $"ACK دستور: {result}");
                    }
                }

                // ۲) رکوردهای تردد
                var punches = ParseAttlog(body);
                if (punches.Count > 0)
                {
                    int sent = await channel.PostPunchesAsync(sn, punches);
                    Interlocked.Add(ref live.PunchesTotal, sent);
                    rt.Log("INFO", sn, $"{sent} رکورد تردد از مسیر ADMS ورود شد");
                }

                // ۳) کاربران (push از دستگاه)
                var users = ParseUsers(body);
                if (users.Count > 0)
                {
                    await channel.PostUsersAsync(sn, users);
                    rt.Log("INFO", sn, $"{users.Count} کاربر از دستگاه ثبت شد");
                }
            }
            catch (Exception ex)
            {
                rt.Log("ERROR", sn, "خطای پردازش داده دستگاه: " + ex.Message);
            }
            return Results.Text("OK", "text/plain", Encoding.UTF8);
        });
    }
}
