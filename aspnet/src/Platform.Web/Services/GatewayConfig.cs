using Platform.Web.Data;
using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

/// <summary>
/// دسترسی به تک‌ردیف تنظیمات گیت‌وی C# (HrDeviceGateway) — فاز ۲۱.
/// ردیف پیش‌فرض در اولین استفاده ساخته می‌شود (idempotent).
/// </summary>
public static class GatewayConfig
{
    public static readonly string[] LogLevels = { "Debug", "Information", "Warning", "Error" };

    public static async Task<GatewaySetting> GetAsync(AppDbContext db)
    {
        var s = await db.Set<GatewaySetting>().FirstOrDefaultAsync(x => x.Id == "default");
        if (s is not null) return s;
        s = new GatewaySetting { Id = "default" };
        db.Set<GatewaySetting>().Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    /// <summary>به‌روزرسانی نشانگر آخرین دریافت کانفیگ توسط گیت‌وی (بدون تغییر نسخه).</summary>
    public static async Task MarkFetchedAsync(AppDbContext db)
    {
        var s = await GetAsync(db);
        s.LastFetchedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    /// <summary>اعتبارسنجی مقادیر فرم — پیام‌های خطای فارسی؛ خروجی null یعنی معتبر.</summary>
    public static string? Validate(GatewaySetting s) =>
        s.ListenPort is < 1 or > 65535 ? "پورت پل ADMS باید بین ۱ تا ۶۵۵۳۵ باشد"
        : s.TcpTimeoutMs is < 1000 or > 60000 ? "زمان انتظار TCP باید بین ۱۰۰۰ تا ۶۰۰۰۰ میلی‌ثانیه باشد"
        : s.PollIntervalSec is < 15 or > 3600 ? "فاصله poll باید بین ۱۵ تا ۳۶۰۰ ثانیه باشد"
        : s.SweeperIntervalSec is < 30 or > 3600 ? "فاصله بازبینی (sweeper) باید بین ۳۰ تا ۳۶۰۰ ثانیه باشد"
        : s.ConfigRefreshSec is < 10 or > 3600 ? "فاصله دریافت تنظیمات باید بین ۱۰ تا ۳۶۰۰ ثانیه باشد"
        : s.DefaultDevicePort is < 1 or > 65535 ? "پورت پیش‌فرض دستگاه باید بین ۱ تا ۶۵۵۳۵ باشد"
        : s.DefaultCommKey < 0 ? "کلید ارتباطی پیش‌فرض نامعتبر است"
        : LogLevels.Contains(s.LogLevel) ? null
        : "سطح لاگ نامعتبر است";
}
