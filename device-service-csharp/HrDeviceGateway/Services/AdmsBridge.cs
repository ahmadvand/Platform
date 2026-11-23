using System.Globalization;
using System.Text;
using HrDeviceGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HrDeviceGateway.Services;

/// <summary>
/// پل ADMS/PUSH — endpoint های Kestrel برای دستگاه‌های ZKTeco با پروتکل PUSH:
///   GET  /iclock/cdata?SN=...  → handshake + تحویل دستورات متنی (C:{id}:…)
///   POST /iclock/cdata?SN=...  → ATTLOG / USERINFO / ACK های دستگاه
/// پورت مستقیم adms.mjs نسخه Node.
/// </summary>
public static class AdmsBridge
{
    /// <summary>نگاشت شناسه کوتاه دستور (8 کاراکتر) به شناسه کامل — برای تطبیق ACK.</summary>
    public sealed class PendingMap
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, string> _map = new();

        public void Set(string shortId, string commandId)
        {
            lock (_lock) { _map[shortId] = commandId; }
        }

        public string? Take(string shortId)
        {
            lock (_lock)
            {
                if (_map.Remove(shortId, out var id)) return id;
                return null;
            }
        }

        public int Count { get { lock (_lock) { return _map.Count; } } }
    }

    // ---------- تبدیل دستور سرور به متن پروتکل ADMS ----------

    public static string? CommandToAdms(CommandDto cmd)
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

    // ---------- تجزیه داده‌های ارسالی دستگاه ----------

    /// <summary>تجزیه خطوط ATTLOG: pin\ttimestamp\tstatus\tverify</summary>
    public static List<RawPunch> ParseAttlog(string body)
    {
        var punches = new List<RawPunch>();
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim('\r', ' ');
            if (line.Length == 0) continue;
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;
            if (!parts[0].All(char.IsDigit) || parts[0].Length == 0) continue;
            punches.Add(new RawPunch
            {
                Pin = parts[0],
                Timestamp = parts[1],
                Status = parts.Length > 2 ? parts[2] : null,
            });
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
            // الگو: C:{id}: ... — با tab یا هر متن پس از دونقطه
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
    public static List<DeviceUser> ParseUsers(string body)
    {
        var users = new List<DeviceUser>();
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
            if (!string.IsNullOrEmpty(pin)) users.Add(new DeviceUser { Pin = pin, Name = name });
        }
        return users;
    }

    // ---------- ثبت endpoint ها ----------

    public static void MapEndpoints(WebApplication app, ApiClient api, PendingMap pending, ILoggerFactory lf)
    {
        var log = lf.CreateLogger("AdmsBridge");

        app.MapGet("/iclock/cdata", async (HttpContext ctx) =>
        {
            string? sn = ctx.Request.Query["SN"].FirstOrDefault();
            if (string.IsNullOrEmpty(sn)) return Results.BadRequest("BAD REQUEST");

            // handshake + تحویل دستورات صف‌شده
            var cmds = await api.PullCommandsAsync(sn);
            var outLines = new List<string>();
            foreach (var c in cmds)
            {
                string? text = CommandToAdms(c);
                if (text is null)
                {
                    // دستور قابل ترجمه به ADMS نیست (مثلاً انتقال قالب هویتی) —
                    // بی‌اطلاع رها نمی‌شود: ack ناموفق با پیام روشن تا کاربر بداند چرا اجرا نشد
                    await api.PostAckAsync(sn, c.Id, false,
                        "این دستور در حالت ADMS قابل اجرا نیست — انتقال قالب هویتی فقط با اتصال TCP مستقیم ممکن است (نحوه اتصال دستگاه را در ویرایش دستگاه تغییر دهید)");
                    log.LogWarning("دستور {Type} برای {Serial} قابل ترجمه به ADMS نیست — ack ناموفق ارسال شد", c.Type, sn);
                    continue;
                }
                outLines.Add(text);
                pending.Set(text[2..Math.Min(10, text.Length)], c.Id);
            }
            if (outLines.Count > 0)
                log.LogInformation("تحویل {Count} دستور به دستگاه {Serial}", outLines.Count, sn);

            return Results.Text(outLines.Count > 0 ? string.Join("\n", outLines) + "\n" : "OK",
                "text/plain", Encoding.UTF8);
        });

        app.MapPost("/iclock/cdata", async (HttpContext ctx) =>
        {
            string? sn = ctx.Request.Query["SN"].FirstOrDefault();
            if (string.IsNullOrEmpty(sn)) return Results.BadRequest("BAD REQUEST");

            string body;
            using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync();

            try
            {
                // 1) ACK دستورات
                foreach (var (shortId, result) in ParseAck(body))
                {
                    string? commandId = pending.Take(shortId);
                    if (commandId is not null)
                    {
                        bool ok = !System.Text.RegularExpressions.Regex.IsMatch(result, "error|fail",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        await api.PostAckAsync(sn, commandId, ok, result);
                        log.LogInformation("ACK دستور {Command} از دستگاه {Serial}: {Result}", commandId, sn, result);
                    }
                }

                // 2) رکوردهای تردد
                var punches = ParseAttlog(body);
                if (punches.Count > 0)
                {
                    bool sent = await api.PostPunchesAsync(sn, punches);
                    if (sent) log.LogInformation("دستگاه {Serial}: {Count} رکورد تردد ارسال شد", sn, punches.Count);
                }

                // 3) کاربران (push از دستگاه)
                var users = ParseUsers(body);
                if (users.Count > 0)
                {
                    await api.PostUsersAsync(sn, users);
                    log.LogInformation("دستگاه {Serial}: {Count} کاربر ارسال شد", sn, users.Count);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "خطای پردازش داده دستگاه {Serial}", sn);
            }
            return Results.Text("OK", "text/plain");
        });
    }
}
