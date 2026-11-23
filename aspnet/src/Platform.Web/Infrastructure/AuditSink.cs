using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Contracts;
using Platform.Core.Services;

namespace Platform.Web.Infrastructure;

/// <summary>
/// فاز ۶۱ — پیاده‌سازی IAuditSink برای ماژول‌ها: حسابرسی روی کانتکست مرکب هاست
/// با همان منطق قبلی (AuthCore.AuditAsync). ماژول‌ها به composite دسترسی مستقیم ندارند.
/// فاز ۱۱۷ — الحاق به زنجیرهٔ ضد دستکاری (AuditChain) پیش از ذخیره.
/// </summary>
public class AuditSink(AppDbContext db) : IAuditSink
{
    public async Task Audit(SessionUserDto? actor, string action, string entity, string? entityId, object? detail, string? ip)
    {
        var log = new AuditLog
        {
            ActorUserId = actor?.Id,
            ActorName = actor?.FullName ?? "سیستم",
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Detail = detail is null ? null : JsonSerializer.Serialize(detail),
            Ip = ip,
            At = DateTime.Now,
        };
        await AuditChain.AppendAsync(db.AuditLogs, log);
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
