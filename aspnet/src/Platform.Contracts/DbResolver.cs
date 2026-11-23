// ======================================================================
// فاز ۶۸ — حل‌وفصل پروایدر/رشتهٔ اتصال برای کانتکست‌های دیتای ماژول‌ها
// (همان قواعد DbConfig هاست؛ ماژول‌ها به هاست ارجاع ندارند پس نسخهٔ قراردادی اینجا است)
// ======================================================================
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Platform.Contracts;

public static class HrDbResolver
{
    public static bool IsSqlite(string provider) => provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

    public static (string Provider, string? ConnectionString) Resolve(IConfiguration cfg)
    {
        var provider = cfg["Database:Provider"] ?? "SqlServer";
        var cs = cfg["Database:ConnectionString"];
        if (string.IsNullOrWhiteSpace(cs))
            cs = cfg.GetConnectionString(IsSqlite(provider) ? "Sqlite" : "SqlServer");
        return (provider, string.IsNullOrWhiteSpace(cs) ? null : cs);
    }

    /// <summary>گزینه‌های کانتکست ماژول: یا همان useDb ویزارد (اتصال جدید در حال آزمایش) یا پیکربندی جاری نصب.</summary>
    public static DbContextOptionsBuilder<TCtx> OptionsFor<TCtx>(
        Action<DbContextOptionsBuilder>? useDb, IConfiguration cfg) where TCtx : DbContext
    {
        var ob = new DbContextOptionsBuilder<TCtx>();
        if (useDb is not null) { useDb(ob); return ob; }
        var (provider, cs) = Resolve(cfg);
        if (IsSqlite(provider)) ob.UseSqlite(cs);
        else ob.UseSqlServer(cs);
        return ob;
    }
}
