using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;

namespace Platform.Web.Controllers;

// ============================================================
// فاز ۱۳۵ (مورد ۱۷ ROADMAP-ADDITIONS) — رویدادهای زندهٔ داشبورد
// جریان SSE (Server-Sent Events) روی /live/events — هر ۵ ثانیه snapshot شاخص‌های
// امروز (حاضران/تأخیر/غیبت/مرخصی، درخواست‌های در انتظار، هشدار باز، رویداد پارکینگ).
// یادداشت فنی: بستهٔ JS کلاینت SignalR در محیط آفلاین این نصب در دسترس نیست؛
// فشار سرور→کلاینت با SSE (بدون وابستگی، پشتیبانی بومی مرورگر) پیاده شد.
// ============================================================

public sealed record LiveAttRow(int Present, int Late, int Absent, int OnLeave);
public sealed record LivePendingRow(int Leaves, int Missions, int Corrections);
public sealed record LiveCountRow(int N);

public class LiveController : MvcBase
{
    private static readonly JsonSerializerOptions JOpt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public LiveController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    [HttpGet("/live/events")]
    public async Task Events(CancellationToken ct)
    {
        if (Sess is null || !Sess.Has("dashboard.view"))
        {
            Response.StatusCode = 401;
            return;
        }
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var today = DateTime.Today.Date;
        var todayKey = Jalali.DateKey(today);
        var tomorrow = today.AddDays(1);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var att = (await Db.Database.SqlQuery<LiveAttRow>($@"
                    SELECT
                        SUM(CASE WHEN Status = 'حاضر' THEN 1 ELSE 0 END) AS Present,
                        SUM(CASE WHEN Status = 'تأخیر' THEN 1 ELSE 0 END) AS Late,
                        SUM(CASE WHEN Status = 'غایب' THEN 1 ELSE 0 END) AS Absent,
                        SUM(CASE WHEN Status = 'مرخصی' THEN 1 ELSE 0 END) AS OnLeave
                    FROM AttendanceRecords WHERE Date = {todayKey}").ToListAsync())
                    .FirstOrDefault() ?? new LiveAttRow(0, 0, 0, 0);

                var pend = (await Db.Database.SqlQuery<LivePendingRow>($@"
                    SELECT
                        (SELECT COUNT(*) FROM LeaveRequests WHERE Status = 'در انتظار تأیید') AS Leaves,
                        (SELECT COUNT(*) FROM Missions WHERE Status = 'در انتظار تأیید') AS Missions,
                        (SELECT COUNT(*) FROM CorrectionRequests WHERE Status = 'در انتظار تأیید') AS Corrections").ToListAsync())
                    .FirstOrDefault() ?? new LivePendingRow(0, 0, 0);

                var openAlerts = await Db.SecurityAlerts.CountAsync(a => !a.Resolved);

                var parkingToday = (await Db.Database.SqlQuery<LiveCountRow>($@"
                    SELECT COUNT(*) AS N FROM ParkingEvents
                    WHERE Timestamp >= {today} AND Timestamp < {tomorrow}").ToListAsync())
                    .FirstOrDefault()?.N ?? 0;

                var payload = JsonSerializer.Serialize(new
                {
                    present = att.Present,
                    late = att.Late,
                    absent = att.Absent,
                    onLeave = att.OnLeave,
                    pending = pend.Leaves + pend.Missions + pend.Corrections,
                    openAlerts,
                    parkingToday,
                    ts = DateTime.Now.ToString("HH:mm:ss"),
                }, JOpt);

                await Response.WriteAsync($"data: {payload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* خطای موقت — اتصال باز می‌ماند و چرخهٔ بعدی دوباره تلاش می‌کند */ }

            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
