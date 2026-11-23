using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Web.Data;
using Platform.Web.Domain;

namespace Platform.Web.Services;

// ============================================================
// فاز ۱۳۵ (مورد ۲۰ ROADMAP-ADDITIONS) — پشتیبان‌گیری زمان‌بندی‌شدهٔ دیتابیس
// + نگهداشت N نسخهٔ آخر. فایل‌های دیتابیس (hr.db و هم‌خانواده‌های wal/shm +
// هر *.db دیگر در ریشهٔ نصب) روزانه در ساعت تعیین‌شده در backups/db زیپ و
// نگهداشت می‌شوند؛ نسخه‌های مازاد خودکار حذف می‌شوند.
// اجرای دستی هم از صفحهٔ «به‌روزرسانی سامانه» ممکن است.
// ============================================================

public static class DbBackup
{
    public static string BackupsDbRoot(string root) => Path.Combine(root, "backups", "db");

    /// <summary>ساخت زیپ پشتیبان دیتابیس + اعمال نگهداشت N نسخه. خروجی: نام فایل یا خطا.</summary>
    public static (bool Ok, string NameOrMsg, int Removed) CreateBackup(string root, int retention, DbContext? db)
    {
        try
        {
            var bdir = BackupsDbRoot(root);
            Directory.CreateDirectory(bdir);

            // تلاش برای checkpoint WAL تا فایل اصلی کامل باشد (بی‌خطر؛ اگر ممکن نبود ادامه)
            try
            {
                if (db is not null)
                    db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
            }
            catch { /* checkpoint تلاشی است — موانع بسته‌بندی را نمی‌شکنند */ }

            var zipPath = Path.Combine(bdir, "db-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var f in Directory.GetFiles(root, "*.db"))
                    zip.CreateEntryFromFile(f, Path.GetFileName(f), CompressionLevel.Optimal);
                foreach (var f in Directory.GetFiles(root, "*.db-*")) // -wal / -shm
                    zip.CreateEntryFromFile(f, Path.GetFileName(f), CompressionLevel.Optimal);
            }
            var removed = ApplyRetention(bdir, retention);
            return (true, Path.GetFileName(zipPath), removed);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, 0);
        }
    }

    /// <summary>نگهداشت N نسخهٔ جدید؛ خروجی: تعداد حذف‌شده‌ها.</summary>
    public static int ApplyRetention(string bdir, int retention)
    {
        if (retention < 1) retention = 1;
        var files = Directory.GetFiles(bdir, "db-*.zip")
            .OrderByDescending(f => f).ToList();
        var removed = 0;
        foreach (var f in files.Skip(retention))
        {
            try { File.Delete(f); removed++; } catch { /* فایل قفل — نوبت بعدی */ }
        }
        return removed;
    }
}

/// <summary>اجرای دوره‌ای پشتیبان‌گیری زمان‌بندی‌شده — هر یک دقیقه چک می‌کند؛ روزانه یک‌بار.</summary>
public class DbBackupWorker(IServiceScopeFactory scope, IHostEnvironment env, ILogger<DbBackupWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // شروع با تأخیر تا به‌روزرسانی اسکیما (DbInit) تمام شود
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch { return; }
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scp = scope.CreateScope();
                var db = scp.ServiceProvider.GetRequiredService<HrBaseDbContext>();
                var s = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == "default");
                if (s is not null && s.BackupEnabled && !string.IsNullOrWhiteSpace(s.BackupTime)
                    && TimeSpan.TryParse(s.BackupTime, out var at))
                {
                    var now = DateTime.Now;
                    var today = now.ToString("yyyy-MM-dd");
                    if (now.TimeOfDay >= at && s.BackupLastDate != today)
                    {
                        var (ok, name, removed) = DbBackup.CreateBackup(env.ContentRootPath, s.BackupRetention, db);
                        var row = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == "default");
                        if (row is not null) { row.BackupLastDate = today; await db.SaveChangesAsync(); }
                        if (ok) log.LogInformation("پشتیبان خودکار دیتابیس ساخته شد: {Name} ({N} نسخهٔ قدیمی حذف شد)", name, removed);
                        else log.LogWarning("پشتیبان خودکار ناموفق: {Msg}", name);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "خطای چرخهٔ پشتیبان‌گیری خودکار");
            }
            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch { return; }
        }
    }
}
