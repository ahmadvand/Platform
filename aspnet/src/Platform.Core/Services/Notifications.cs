using Platform.Web.Data;
using Platform.Contracts;
using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Platform.Web.Services;

// ============================================================
// موتور اعلان‌ها (PLAN Phase 13)
// قواعد: تأخیر، غیبت، انقضای قرارداد، دستگاه آفلاین، درخواست در انتظار
// کانال‌ها: ایمیل SMTP (System.Net.Mail) + پیامک HTTP (کاوه‌نگار/ملی‌پیامک/generic)
// بدون SMTP/سرویس پیامک → حالت «شبیه‌سازی» با ثبت در لاگ
// ============================================================
public static class NotificationEngine
{
    public static readonly (string Type, string Title)[] RuleTypes =
    {
        ("late", "تأخیر پرسنل امروز"),
        ("absence", "غیبت پرسنل امروز"),
        ("contract_expiry", "انقضای قرارداد"),
        ("device_offline", "دستگاه آفلاین"),
        ("pending_request", "درخواست‌های در انتظار تأیید"),
    };

    /// <summary>اجرای همه قواعد فعال. خروجی: تعداد اعلان ثبت‌شده.
    /// فاز ۶۹ — «انقضای قرارداد» ∈ ماژول org و «دستگاه آفلاین» ∈ ماژول devices؛
    /// هر دو از قرارداد می‌آیند (IPeopleDirectory / IDevicesGateway) — ماژول غایب = صفر hit.</summary>
    public static async Task<int> RunAllAsync(DbContext db, IAttendanceInsights att,
        IPeopleDirectory? people = null, IDevicesGateway? devices = null, ILogger? log = null)
    {
        var settings = await GetSettingsAsync(db);
        var rules = await db.Set<NotificationRule>().Where(r => r.Enabled).ToListAsync();
        var today = DateTime.Now.Date;
        var todayStr = today.ToString("yyyy-MM-dd");
        var total = 0;

        foreach (var rule in rules)
        {
            try
            {
                // ضداسپم: یک اعلان از هر قاعده در هر ۶ ساعت
                var last = await db.Set<NotificationLog>()
                    .Where(l => l.RuleName == rule.Name && l.CreatedAt > DateTime.Now.AddHours(-6))
                    .AnyAsync();
                if (last) continue;

                var (title, body, hits) = rule.Type switch
                {
                    "late" => await BuildLateAsync(att, rule, todayStr),
                    "absence" => await BuildAbsenceAsync(att, rule, todayStr),
                    "contract_expiry" => await BuildContractExpiryAsync(people, rule),
                    "device_offline" => await BuildDeviceOfflineAsync(devices, rule),
                    "pending_request" => await BuildPendingAsync(att, rule),
                    _ => ($"قاعده نامشخص: {rule.Type}", "", 0),
                };
                if (hits == 0)
                {
                    rule.LastRunAt = DateTime.Now;
                    continue;
                }

                if (rule.ViaEmail && settings.EnabledEmail)
                    total += await SendAsync(db, rule, settings, "email", rule.RecipientEmail ?? settings.AdminEmails, title, body, log);
                else if (rule.ViaEmail)
                    total += await LogAsync(db, rule, "email", rule.RecipientEmail ?? settings.AdminEmails, title, body, "simulated", null);

                if (rule.ViaSms)
                {
                    var phone = rule.RecipientPhone ?? "";
                    if (!string.IsNullOrWhiteSpace(phone))
                    {
                        if (settings.EnabledSms && !string.IsNullOrWhiteSpace(settings.SmsApiUrl))
                            total += await SendAsync(db, rule, settings, "sms", phone, title, Shorten(body), log);
                        else
                            total += await LogAsync(db, rule, "sms", phone, title, Shorten(body), "simulated", null);
                    }
                }

                // فاز ۱۳۵ (مورد ۱۸) — کانال وب‌هوک: JSON POST با امضای HMAC اختیاری
                if (rule.ViaWebhook)
                {
                    if (!string.IsNullOrWhiteSpace(settings.WebhookUrl))
                        total += await SendWebhookAsync(db, rule, settings.WebhookUrl, settings.WebhookSecret, title, body, log);
                    else
                        total += await LogAsync(db, rule, "webhook", "(unset)", title, Shorten(body), "simulated", null);
                }
                rule.LastRunAt = DateTime.Now;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "خطای اجرای قاعده اعلان {Rule}", rule.Name);
            }
        }
        await db.SaveChangesAsync();
        return total;
    }

    private static string Shorten(string s) => s.Length <= 300 ? s : s[..300] + "…";

    // ---------- فاز ۱۳۵ (مورد ۱۸) — کانال وب‌هوک ----------
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static async Task<int> SendWebhookAsync(DbContext db, NotificationRule rule, string url, string secret,
        string title, string body, ILogger? log)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            rule = rule.Name,
            type = rule.Type,
            title,
            body,
            at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        });
        var status = "sent";
        string? err = null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            if (!string.IsNullOrWhiteSpace(secret))
            {
                using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
                req.Headers.Add("X-Hr-Signature", Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant());
            }
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) { status = "failed"; err = "HTTP " + (int)resp.StatusCode; }
        }
        catch (Exception ex)
        {
            status = "failed";
            err = ex.Message;
            log?.LogWarning(ex, "خطای ارسال وب‌هوک قاعده {Rule}", rule.Name);
        }
        await LogAsync(db, rule, "webhook", url, title, Shorten(body), status, err);
        return status == "sent" ? 1 : 0;
    }

    // ---------- قواعد ----------
    private static async Task<(string, string, int)> BuildLateAsync(IAttendanceInsights att, NotificationRule r, string today)
    {
        var rows = await att.LateTodayAsync(today, r.Threshold);
        if (rows.Count == 0) return ("", "", 0);
        var sb = new StringBuilder($"اعلام تأخیر ({rows.Count} نفر) — {DateTime.Now:yyyy/MM/dd HH:mm}\n");
        foreach (var x in rows.Take(20))
            sb.AppendLine($"• {x.FullName} ({x.PersonnelCode}) — {x.LateMinutes} دقیقه تأخیر");
        return ($"تأخیر {rows.Count} نفر", sb.ToString(), rows.Count);
    }

    private static async Task<(string, string, int)> BuildAbsenceAsync(IAttendanceInsights att, NotificationRule r, string today)
    {
        var rows = await att.AbsentTodayAsync(today);
        if (rows.Count == 0) return ("", "", 0);
        var sb = new StringBuilder($"اعلام غیبت ({rows.Count} نفر) — {DateTime.Now:yyyy/MM/dd HH:mm}\n");
        foreach (var x in rows.Take(20))
            sb.AppendLine($"• {x.FullName} ({x.PersonnelCode})");
        return ($"غیبت {rows.Count} نفر", sb.ToString(), rows.Count);
    }

    private static async Task<(string, string, int)> BuildContractExpiryAsync(IPeopleDirectory? people, NotificationRule r)
    {
        if (people is null) return ("", "", 0);
        var days = r.Threshold <= 0 ? 30 : r.Threshold;
        var until = DateTime.Now.Date.AddDays(days);
        var rows = await people.ContractExpiryAsync(days);
        if (rows.Count == 0) return ("", "", 0);
        var sb = new StringBuilder($"قراردادهای در حال انقضا تا {until:yyyy/MM/dd} ({rows.Count} مورد):\n");
        foreach (var x in rows.Take(20))
            sb.AppendLine($"• {x.FirstName} {x.LastName} ({x.PersonnelCode}) — قرارداد {x.ContractNo} — تا {x.EndDate:yyyy/MM/dd}");
        return ($"{rows.Count} قرارداد در حال انقضا", sb.ToString(), rows.Count);
    }

    private static async Task<(string, string, int)> BuildDeviceOfflineAsync(IDevicesGateway? devices, NotificationRule r)
    {
        if (devices is null) return ("", "", 0);
        var minutes = r.Threshold <= 0 ? 60 : r.Threshold;
        var rows = await devices.OfflineDevicesAsync(minutes);
        if (rows.Count == 0) return ("", "", 0);
        var sb = new StringBuilder($"دستگاه‌های بدون ارتباط بیش از {minutes} دقیقه ({rows.Count} دستگاه):\n");
        foreach (var x in rows.Take(20))
            sb.AppendLine($"• {x.Name} ({x.Ip}) — آخرین ارتباط: {(x.LastSeenAt?.ToString("yyyy/MM/dd HH:mm") ?? "هرگز")}");
        return ($"{rows.Count} دستگاه آفلاین", sb.ToString(), rows.Count);
    }

    private static async Task<(string, string, int)> BuildPendingAsync(IAttendanceInsights att, NotificationRule r)
    {
        var c = await att.PendingCountsAsync();
        var hits = c.Leaves + c.Missions + c.Corrections;
        if (hits == 0) return ("", "", 0);
        var body = $"درخواست‌های در انتظار تأیید:\n• مرخصی: {c.Leaves}\n• مأموریت: {c.Missions}\n• اصلاح تردد: {c.Corrections}";
        return ($"{hits} درخواست در انتظار", body, hits);
    }

    // ---------- ارسال/ثبت ----------
    private static async Task<int> LogAsync(DbContext db, NotificationRule rule, string channel, string recipient, string title, string body, string status, string? error)
    {
        db.Set<NotificationLog>().Add(new NotificationLog
        {
            RuleName = rule.Name, Type = rule.Type, Channel = channel,
            Recipient = recipient ?? "", Title = title, Body = body,
            Status = status, Error = error,
        });
        await db.SaveChangesAsync();
        return 1;
    }

    private static async Task<int> SendAsync(DbContext db, NotificationRule rule, NotificationSetting s, string channel, string recipient, string title, string body, ILogger? log)
    {
        try
        {
            if (channel == "email")
            {
                if (string.IsNullOrWhiteSpace(recipient) || string.IsNullOrWhiteSpace(s.SmtpHost))
                    return await LogAsync(db, rule, channel, recipient ?? "", title, body, "simulated", "SMTP تنظیم نشده — ارسال شبیه‌سازی شد");
                using var client = new SmtpClient(s.SmtpHost, s.SmtpPort)
                {
                    EnableSsl = s.SmtpSsl,
                    Credentials = string.IsNullOrWhiteSpace(s.SmtpUser) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(s.SmtpUser, s.SmtpPass),
                };
                foreach (var to in recipient.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    using var msg = new MailMessage(s.FromEmail ?? s.SmtpUser, to, title, body) { BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8 };
                    await client.SendMailAsync(msg);
                }
                return await LogAsync(db, rule, channel, recipient, title, body, "sent", null);
            }
            else // sms
            {
                if (string.IsNullOrWhiteSpace(s.SmsApiUrl))
                    return await LogAsync(db, rule, channel, recipient, title, body, "simulated", "سرویس پیامک تنظیم نشده — ارسال شبیه‌سازی شد");
                var url = s.SmsApiUrl
                    .Replace("{phone}", Uri.EscapeDataString(recipient))
                    .Replace("{message}", Uri.EscapeDataString(body))
                    .Replace("{apikey}", Uri.EscapeDataString(s.SmsApiKey ?? ""))
                    .Replace("{sender}", Uri.EscapeDataString(s.SmsSender ?? ""));
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var resp = await http.GetAsync(url);
                var ok = resp.IsSuccessStatusCode;
                return await LogAsync(db, rule, channel, recipient, title, body, ok ? "sent" : "failed", ok ? null : $"HTTP {(int)resp.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "خطای ارسال اعلان {Rule}", rule.Name);
            return await LogAsync(db, rule, channel, recipient, title, body, "failed", ex.Message);
        }
    }

    public static async Task<NotificationSetting> GetSettingsAsync(DbContext db)
    {
        var s = await db.Set<NotificationSetting>().FirstOrDefaultAsync(x => x.Id == "default");
        if (s is null)
        {
            s = new NotificationSetting();
            db.Set<NotificationSetting>().Add(s);
            await db.SaveChangesAsync();
        }
        return s;
    }
}

/// <summary>سرویس پس‌زمینه: اجرای دوره‌ای موتور اعلان‌ها (هر ۳۰ دقیقه).</summary>
public class NotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scope;
    private readonly ILogger<NotificationWorker> _log;
    public NotificationWorker(IServiceScopeFactory scope, ILogger<NotificationWorker> log) { _scope = scope; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // صبر اولیه تا دیتابیس آماده شود
        try { await Task.Delay(TimeSpan.FromMinutes(2), ct); } catch { return; }
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scope.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HrBaseDbContext>();
                var att = scope.ServiceProvider.GetRequiredService<IAttendanceInsights>();
                var people = scope.ServiceProvider.GetService<IPeopleDirectory>();
                var devices = scope.ServiceProvider.GetService<IDevicesGateway>();
                var any = await db.Set<NotificationRule>().AnyAsync(r => r.Enabled);
                if (any)
                {
                    var n = await NotificationEngine.RunAllAsync(db, att, people, devices, _log);
                    if (n > 0) _log.LogInformation("موتور اعلان‌ها: {N} اعلان ارسال/ثبت شد", n);
                }
            }
            catch (Exception ex) { _log.LogWarning(ex, "خطای دوره اعلان‌ها"); }
            try { await Task.Delay(TimeSpan.FromMinutes(30), ct); } catch { return; }
        }
    }
}
