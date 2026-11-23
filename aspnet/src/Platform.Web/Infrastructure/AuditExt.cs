using System.Text.Json;
using Platform.Web.Domain;
using Platform.Web.Data;

namespace Platform.Web.Services;

/// <summary>افزونهٔ حسابرسی روی کانتکست مرکب هاست — فاز ۶۱: از Core به Web منتقل شد
/// (Core دیگر به composite دسترسی ندارد؛ ماژول‌ها حسابرسی را از IAuditSink عبور می‌دهند).
/// منطق ثبت دقیقاً همان AuthCore.AuditAsync است.</summary>
public static class AuditExt
{
    public static async Task Audit(this AppDbContext db, SessionUser? actor,
        string action, string entity, string? entityId, object? detail, string? ip = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor?.Id,
            ActorName = actor?.FullName ?? "سیستم",
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Detail = detail is null ? null : JsonSerializer.Serialize(detail),
            Ip = ip,
            At = DateTime.Now,
        });
        await db.SaveChangesAsync();
    }
}
