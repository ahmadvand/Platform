using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Platform.Core.Services;

/// <summary>
/// فاز ۱۰۹ — زنجیرهٔ ممیزی ضد دستکاری: هر رکورد حسابرسی هشِ SHA-256 خود را
/// از فیلدهایش + هشِ ردیفِ قبل می‌سازد (زمان با دقت ثانیه — سازگار با گردش DateTime در هر دو پروایدر)؛ دستکاری مستقیم پایگاه داده زنجیره را
/// می‌شکند و در «بررسی سلامت زنجیره» آشکار می‌شود. مسیر نوشتن = AuditSink (هاست)
/// و AuthCore.AuditAsync (ورود/خروج) — هر دو از همین هستهٔ مشترک استفاده می‌کنند.
/// فاز ۱۱۷ — پیاده‌سازی کامل: Seq/PrevHash/Hash روی AuditLog + AppendAsync (تخصیص
/// ترتیبی داخل قفل) + VerifyAsync (بازمحاسبهٔ کل زنجیره) + اتصال هر دو مسیر نوشتن.
/// </summary>
public static class AuditChain
{
    /// <summary>سری‌سازی نوشتن زنجیره (نوشته‌های هم‌زمان همان فرآیند) — زنجیره ترتیب می‌خواهد.</summary>
    public static readonly SemaphoreSlim WriteGate = new(1, 1);

    public static string ComputeHash(string? prevHash, string? actorUserId, string? actorName,
        string action, string entity, string? entityId, string? detail, string? ip, DateTime at)
    {
        var payload = string.Join('\x1f',
            prevHash ?? "", actorUserId ?? "", actorName ?? "", action, entity,
            entityId ?? "", detail ?? "", ip ?? "", at.ToString("yyyy-MM-dd HH:mm:ss"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    /// <summary>
    /// فاز ۱۱۷ — الحاق یک ردیف به زنجیره: داخل قفل، آخرین هشِ زنجیره خوانده و Seq/PrevHash/Hash
    /// ردیف تازه پر می‌شود (ذخیرهٔ خود ردیف با فراخواننده است). ردیف‌های قدیمی (بدون هش) مبدأ زنجیرهٔ تازه‌اند.
    /// </summary>
    public static async Task AppendAsync(Microsoft.EntityFrameworkCore.DbSet<Platform.Web.Domain.AuditLog> set,
        Platform.Web.Domain.AuditLog log)
    {
        await WriteGate.WaitAsync();
        try
        {
            var prev = await set.AsNoTracking()
                .Where(x => x.Hash != null)
                .OrderByDescending(x => x.Seq)
                .Select(x => new { x.Seq, x.Hash })
                .FirstOrDefaultAsync();
            log.Seq = (prev?.Seq ?? 0) + 1;
            log.PrevHash = prev?.Hash;
            log.Hash = ComputeHash(log.PrevHash, log.ActorUserId, log.ActorName,
                log.Action, log.Entity, log.EntityId, log.Detail, log.Ip, log.At);
        }
        finally { WriteGate.Release(); }
    }

    /// <summary>نتیجهٔ راستی‌آزمایی زنجیره.</summary>
    public sealed record ChainVerifyResult(bool Ok, long Checked, long Legacy, long Total,
        long? BrokenSeq, string? BrokenId, string? Reason);

    /// <summary>
    /// فاز ۱۱۷ — راستی‌آزمایی سلامت زنجیره: بازمحاسبهٔ هش هر ردیف (به ترتیب Seq) و مقایسه با
    /// مقدار ذخیره‌شده + پیوستگی PrevHash. اولین شکست (ویرایش/حذف/درج خارج از ترتیب) گزارش می‌شود؛
    /// ردیف‌های بدون هش «قدمتی» شمرده می‌شوند و در زنجیره نیستند.
    /// </summary>
    public static async Task<ChainVerifyResult> VerifyAsync(IQueryable<Platform.Web.Domain.AuditLog> logs)
    {
        var total = await logs.LongCountAsync();
        var legacy = await logs.LongCountAsync(x => x.Hash == null);
        var rows = await logs.AsNoTracking()
            .Where(x => x.Hash != null)
            .OrderBy(x => x.Seq)
            .ToListAsync();

        long expectSeq = 0;
        string? prev = null;
        foreach (var r in rows)
        {
            if (r.Seq != expectSeq + 1)
                return new ChainVerifyResult(false, expectSeq, legacy, total, r.Seq, r.Id,
                    "شکستگی ترتیب زنجیره — ردیف حذف یا خارج از ترتیب درج شده است");
            var h = ComputeHash(r.PrevHash, r.ActorUserId, r.ActorName,
                r.Action, r.Entity, r.EntityId, r.Detail, r.Ip, r.At);
            if (!string.Equals(h, r.Hash, StringComparison.Ordinal))
                return new ChainVerifyResult(false, expectSeq, legacy, total, r.Seq, r.Id,
                    "هش ردیف با بازمحاسبه یکسان نیست — محتوای ردیف دستکاری شده است");
            if (prev is not null && !string.Equals(r.PrevHash, prev, StringComparison.Ordinal))
                return new ChainVerifyResult(false, expectSeq, legacy, total, r.Seq, r.Id,
                    "پیوند PrevHash با ردیف قبل یکسان نیست");
            prev = r.Hash;
            expectSeq = r.Seq;
        }
        return new ChainVerifyResult(true, expectSeq, legacy, total, null, null, null);
    }
}
