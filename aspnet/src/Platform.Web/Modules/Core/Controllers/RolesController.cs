using System.Text.RegularExpressions;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۷۷ — مدیریت نقش‌ها (درخواست کاربر: «ایجاد نقش جدید هنوز وجود نداره!»)
/// ----------------------------------------------------------------------
/// تا این فاز نقش‌ها فقط از کاتالوگ سیدر می‌آمدند و در صفحهٔ کاربران فقط
/// «ماتریس دسترسی» قابل ویرایش بود. اکنون:
///   • ایجاد نقش سفارشی جدید (نام فنی لاتین + عنوان فارسی + توضیح)
///   • ویرایش عنوان/توضیح نقش (نام فنی تغییرنکردنی است — [HasPerm] و سیدر به آن بسته‌اند)
///   • حذف نقش سفارشی (نقش‌های سیستمی کاتالوگ و superadmin حذف‌شدنی نیستند؛
///     سیدر در هر بوت نقش‌های داخلی را بازمی‌سازد — حذفشان فقط توهم حذف بود)
/// گارد همهٔ عملیات: users.manage (همان گارد ماتریس دسترسی نقش‌ها).
/// </summary>
public class RolesController(AppDbContext db, ICurrentUser me) : MvcBase(db, me)
{
    [HttpGet]
    [HasPerm("users.manage")]
    public IActionResult Create()
    {
        // فرم نقش فقط مودال AJAX است (Views/Roles/_RoleForm)؛ فراخوانی صفحه‌ای کامل → پرتال کاربران
        if (Partial) return PartialView("_RoleForm");
        return Redirect("/Users");
    }

    /// <summary>ثبت نقش جدید — نام فنی یکتا (لاتین/زیرخط)، عنوان فارسی، توضیح اختیاری.</summary>
    [HttpPost]
    [HasPerm("users.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string title, string? description)
    {
        name = (name ?? "").Trim().ToLowerInvariant();
        title = (title ?? "").Trim();
        description = (description ?? "").Trim();

        if (name.Length < 2 || name.Length > 40)
            return await CreateErr("نام فنی نقش باید بین ۲ تا ۴۰ کاراکتر باشد");
        if (!Regex.IsMatch(name, @"^[a-z0-9_]+$"))
            return await CreateErr("نام فنی نقش فقط می‌تواند شامل حروف لاتین کوچک، عدد و زیرخط باشد (مثل: warehouse_manager)");
        if (await Db.Roles.AnyAsync(r => r.Name == name))
            return await CreateErr($"نقشی با نام فنی «{name}» از قبل وجود دارد");
        if (title.Length < 2)
            return await CreateErr("عنوان نقش را وارد کنید");
        if (Role.SystemRoleNames.Contains(name))
            return await CreateErr("این نام برای نقش‌های سیستمی سامانه رزرو شده است");

        var role = new Role { Name = name, Title = title, Description = string.IsNullOrWhiteSpace(description) ? null : description };
        Db.Roles.Add(role);
        await Db.SaveChangesAsync();
        await Audit("create_role", "role", role.Name, new { title });
        var msg = $"نقش «{role.Title}» ایجاد شد — دسترسی‌های آن را از درخت دسترسی‌ها تنظیم کنید";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg, new { redirect = "/Users" });
        return Redirect("/Users");
    }

    private async Task<IActionResult> CreateErr(string err)
    {
        ToastErr(err);
        if (Ajax) return AjaxResult(false, err);
        return Redirect("/Users");
    }

    /// <summary>فرم ویرایش نقش — عنوان/توضیح (مودال AJAX).</summary>
    [HttpGet]
    [HasPerm("users.manage")]
    public async Task<IActionResult> Edit(string id)
    {
        var role = await Db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();
        if (Partial) return PartialView("_RoleForm", role);
        return Redirect("/Users");
    }

    /// <summary>ذخیرهٔ ویرایش نقش — فقط عنوان/توضیح؛ نام فنی تغییرنکردنی است.</summary>
    [HttpPost]
    [HasPerm("users.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string title, string? description)
    {
        var role = await Db.Roles.FindAsync(id);
        if (role is null) return NotFound();
        title = (title ?? "").Trim();
        description = (description ?? "").Trim();
        if (title.Length < 2)
        {
            ToastErr("عنوان نقش را وارد کنید");
            if (Ajax) return AjaxResult(false, "عنوان نقش را وارد کنید");
            return Redirect("/Users");
        }
        role.Title = title;
        role.Description = string.IsNullOrWhiteSpace(description) ? null : description;
        await Db.SaveChangesAsync();
        await Audit("update_role", "role", role.Name, new { title });
        var msg = $"نقش «{role.Title}» به‌روزرسانی شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }

    /// <summary>
    /// حذف نقش سفارشی — گاردها: نقش سیستمی نیست، superadmin نیست، کاربر متصل ندارد.
    /// نقش‌های سیستمی در هر بوت از کاتالوگ بازساخته می‌شوند؛ حذفشان پایدار نیست و رد می‌شود.
    /// </summary>
    [HttpPost]
    [HasPerm("users.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await Db.Roles.Include(r => r.Users).Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();

        var fail = (string msg) =>
        {
            ToastErr(msg);
            if (Ajax) return AjaxResult(false, msg);
            return Redirect("/Users");
        };

        if (role.IsBuiltIn)
            return fail($"نقش «{role.Title}» از نقش‌های سیستمی سامانه است و قابل حذف نیست");
        if (role.Name == "superadmin")
            return fail("نقش مدیر ارشد قابل حذف نیست");
        if (role.Users.Count > 0)
            return fail($"به این نقش {Jalali.Num(role.Users.Count)} کاربر متصل است — ابتدا نقش را از کاربران بردارید");

        Db.UserRoles.RemoveRange(role.Users);
        Db.RolePermissions.RemoveRange(role.Permissions);
        Db.Roles.Remove(role);
        await Db.SaveChangesAsync();
        await Audit("delete_role", "role", role.Name, null);
        var msg = $"نقش «{role.Title}» حذف شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }
}
