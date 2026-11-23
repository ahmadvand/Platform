using System.ComponentModel.DataAnnotations;

// ============================================================
// فاز ۱۳۵ (مورد ۱۹ ROADMAP-ADDITIONS) — توکن API عمومی برای سامانه‌های دیگر
// توکن فقط یک‌بار هنگام ساخت نمایش داده می‌شود؛ در دیتابیس فقط SHA-256 ذخیره
// می‌شود. هر توکن می‌تواند به یک شرکت محدود شود (ScopeCompanyId) تا سامانهٔ
// مصرف‌کننده فقط همان شرکت را ببیند.
// ============================================================

/// <summary>توکن دسترسی API عمومی (GET /api/v1/* با هدر X-Api-Token)</summary>
public class ApiToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(120)]
    public string Name { get; set; } = "";          // مصرف‌کننده: «سامانهٔ سرویس غذایی» و...

    [MaxLength(64)]
    public string TokenHash { get; set; } = "";     // SHA-256 hex — بدون توکن خام

    [MaxLength(20)]
    public string Prefix { get; set; } = "";        // ۸ نویسهٔ اول برای شناسایی در UI

    /// <summary>محدودیت شرکتی — null یعنی همهٔ شرکت‌های در دسترس نصب</summary>
    public string? ScopeCompanyId { get; set; }

    public bool Revoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string CreatedBy { get; set; } = "";
    public DateTime? LastUsedAt { get; set; }
    public int RequestCount { get; set; }
}
