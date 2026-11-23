using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// پروفایل من (فاز ۵۱) — خودخدمتی برای همهٔ کاربرانِ وارد‌شده بدون نیاز به دسترسی مدیریت کاربران.
/// از نوار بالایی با کلیک روی نام/آواتار باز می‌شود؛ فقط عکس پروفایل «خودِ کاربر جاری»
/// قابل تغییر است (کاربران دیگر همچنان از صفحهٔ کاربران با دسترسی users.manage).
/// </summary>
public class AccountController : MvcBase
{
    private readonly IWebHostEnvironment _env;

    public AccountController(AppDbContext db, ICurrentUser me, IWebHostEnvironment env) : base(db, me)
        => _env = env;

    /// <summary>فرم عکس پروفایل من — پاپ‌آپ AJAX از نوار بالایی.</summary>
    [HttpGet]
    public async Task<IActionResult> Photo()
    {
        var sess = Sess;
        if (sess is null) return Redirect("/Auth/Login");
        var u = await Db.Users.FindAsync(sess.Id);
        if (u is null) return NotFound();
        return PartialView("_MyPhotoForm", u);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> SetPhoto(IFormFile? photoFile, bool remove = false)
    {
        var sess = Sess;
        if (sess is null) return AjaxResult(false, "نشست منقضی شده — دوباره وارد شوید");
        var u = await Db.Users.FindAsync(sess.Id);
        if (u is null) return NotFound();

        if (remove)
        {
            AvatarUpload.Delete(_env, u.PhotoPath);
            u.PhotoPath = null;
            u.UpdatedAt = DateTime.Now;
            await Db.SaveChangesAsync();
            await Audit("remove_photo_self", "user", u.Username, null);
            // photo: null → نوار بالایی بلافاصله به آواتار حرفی برگردد (site.js — hraj:refresh)
            return AjaxResult(true, "عکس پروفایل حذف شد — آواتار حرفی جایگزین می‌شود", new { photo = (string?)null });
        }

        var (photo, err) = await AvatarUpload.SaveAsync(_env, photoFile, $"u{u.Id}", u.PhotoPath);
        if (err is not null) return AjaxResult(false, err);
        if (photo is null) return AjaxResult(false, "فایلی انتخاب نشده است");
        u.PhotoPath = photo;
        u.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("upload_photo_self", "user", u.Username, new { photo });
        // photo: مسیر → نوار بالایی بلافاصله عکس جدید را نشان دهد (بدون رفرش کل صفحه)
        return AjaxResult(true, "عکس پروفایل شما به‌روزرسانی شد ✓", new { photo });
    }

    /// <summary>فرم تغییر رمز عبور خودکاربر (فاز ۵۲) — پاپ‌آپ AJAX از منوی کاربرِ نوار بالایی.</summary>
    [HttpGet]
    public async Task<IActionResult> Password()
    {
        var sess = Sess;
        if (sess is null) return Redirect("/Auth/Login");
        var u = await Db.Users.FindAsync(sess.Id);
        if (u is null) return NotFound();
        return PartialView("_MyPasswordForm", u);
    }

    /// <summary>
    /// تغییر رمز عبور «خودِ کاربر جاری» (فاز ۵۲) — بدون نیاز به دسترسی users.manage.
    /// همان سیاست رمز و کنترل تکرار ۳ رمز اخیرِ بازنشانی مدیر (UsersController.ResetPassword) — PLAN P1.2/P1.4.
    /// رمز فعلی برای تأیید هویت الزامی است؛ نشست پس از تغییر معتبر می‌ماند (قطع جلسه نداریم).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string newPassword2)
    {
        var sess = Sess;
        if (sess is null) return AjaxResult(false, "نشست منقضی شده — دوباره وارد شوید");
        var u = await Db.Users.Include(x => x.PasswordHistory).FirstOrDefaultAsync(x => x.Id == sess.Id);
        if (u is null) return NotFound();

        // ۱) رمز فعلی درست است؟
        if (string.IsNullOrEmpty(currentPassword) || !PasswordService.Verify(currentPassword, u.PasswordHash))
            return AjaxResult(false, "رمز عبور فعلی اشتباه است");

        // ۲) تکرار رمز جدید
        if (newPassword != newPassword2)
            return AjaxResult(false, "تکرار رمز جدید با رمز جدید یکسان نیست");

        // ۳) سیاست رمز (حداقل ۸ کاراکتر، حرف+عدد، بزرگ/کوچک)
        var policyErr = Jalali.ValidatePassword(newPassword);
        if (policyErr is not null) return AjaxResult(false, policyErr);

        // ۴) رمز جدید نباید با رمز فعلی و ۳ رمز اخیر یکسان باشد
        if (PasswordService.Verify(newPassword, u.PasswordHash))
            return AjaxResult(false, "رمز جدید نباید با رمز فعلی یکسان باشد");
        var recent = u.PasswordHistory.OrderByDescending(h => h.CreatedAt).Take(3).ToList();
        if (recent.Any(h => PasswordService.Verify(newPassword, h.PasswordHash)))
            return AjaxResult(false, "رمز جدید نباید با ۳ رمز اخیر یکسان باشد");

        var newHash = PasswordService.Hash(newPassword);
        u.PasswordHistory.Add(new PasswordHistory { UserId = u.Id, PasswordHash = newHash });
        u.PasswordHash = newHash;
        u.PasswordChangedAt = DateTime.Now;
        u.MustChangePassword = false;
        u.PasswordExpiresAt = DateTime.Now.AddDays(90);
        await Db.SaveChangesAsync();
        await Audit("change_password_self", "user", u.Username, null);
        return AjaxResult(true, "رمز عبور شما با موفقیت تغییر کرد ✓");
    }
}
