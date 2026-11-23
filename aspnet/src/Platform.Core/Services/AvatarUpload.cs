using Microsoft.AspNetCore.Hosting;

namespace Platform.Web.Services;

/// <summary>
/// فاز ۴۵ — ذخیره‌سازی امن عکس پروفایل کاربران و پرسنل.
/// فاز ۶۹ — به پلتفرم مشترک منتقل شد: هم هسته (عکس کاربران در UsersController) و
/// هم ماژول منابع انسانی (عکس پرسنل) از همین ابزار استفاده می‌کنند — بدون ارجاع به هاست.
/// فایل‌ها در wwwroot/uploads/avatars/ با نام یکتا ذخیره و مسیر نسبی آن در
/// Users.PhotoPath / Employees.PhotoPath ثبت می‌شود. عکس قبلی هنگام تعویض حذف می‌شود.
/// </summary>
public static class AvatarUpload
{
    public static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
    public const long MaxBytes = 2 * 1024 * 1024; // ۲ مگابایت

    /// <summary>ذخیره عکس — در خطا پیام فارسی برمی‌گردد؛ file=null → بدون تغییر (null, null).</summary>
    public static async Task<(string? Path, string? Error)> SaveAsync(IWebHostEnvironment env, IFormFile? file, string prefix, string? oldPath)
    {
        if (file is null || file.Length == 0) return (null, null);
        if (file.Length > MaxBytes)
            return (null, HrT.T("حجم عکس باید کمتر از ۲ مگابایت باشد"));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (null, HrT.T("فقط قالب‌های PNG / JPG / WEBP برای عکس پروفایل پذیرفته می‌شود"));

        var dir = Path.Combine(env.WebRootPath ?? "wwwroot", "uploads", "avatars");
        Directory.CreateDirectory(dir);

        // حذف عکس قبلی (اگر داخل پوشه آواتارهاست — لوگو و سایر فایل‌ها دست‌نخورده)
        if (!string.IsNullOrEmpty(oldPath))
        {
            var oldName = Path.GetFileName(oldPath);
            var oldFile = Path.Combine(dir, oldName);
            if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
        }

        var fileName = $"{prefix}-{Guid.NewGuid():N}{ext}";
        await using (var stream = System.IO.File.Create(Path.Combine(dir, fileName)))
            await file.CopyToAsync(stream);
        return ("/uploads/avatars/" + fileName, null);
    }

    /// <summary>حذف عکس از دیسک (هنگام پاک‌سازی یا حذف رکورد) — بی‌صدا.</summary>
    public static void Delete(IWebHostEnvironment env, string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var name = Path.GetFileName(path);
        var file = Path.Combine(env.WebRootPath ?? "wwwroot", "uploads", "avatars", name);
        if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
    }
}
