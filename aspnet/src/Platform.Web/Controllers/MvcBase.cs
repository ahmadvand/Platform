using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Web.Controllers;

/// <summary>
/// کنترلر پایهٔ MVC هاست — دسترسی به دیتابیس مرکب (schema-owner)، نشست جاری،
/// حسابرسی و پیام‌های شیرین‌شده. فاز ۶۱: از HrModuleControllerBase ارث می‌برد؛
/// کمک‌متدهای UX همان قبلی‌اند، ماژول‌ها نسخهٔ بدون-DbContext را در Core دارند.
/// </summary>
public abstract class MvcBase : HrModuleControllerBase
{
    protected readonly AppDbContext Db;

    protected MvcBase(AppDbContext db, ICurrentUser me) : base(me, audits: null) { Db = db; }

    // حسابرسی هاست مستقیم روی composite — همان مسیر قبلی (AuditExt)
    protected new Task Audit(string action, string entity, string? entityId, object? detail) =>
        Db.Audit(Sess, action, entity, entityId, detail, Ip);

    /// <summary>هم‌راستاسازی تم PDF با پالت فعال ادمین — پیش از هر تولید PDF (فاز ۳۲).</summary>
    protected new Task ApplyPdfThemeAsync() =>
        HttpContext.RequestServices.GetRequiredService<SettingsService>().ApplyPdfThemeAsync();

    private static object Merge(object a, object b)
    {
        var d = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(a));
        foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(b)) ?? new())
            d![kv.Key] = kv.Value;
        return d!;
    }
}
