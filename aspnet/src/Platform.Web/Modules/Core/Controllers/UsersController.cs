using System.Text.RegularExpressions;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

// فاز ۶۹ — پرسنل ∈ ماژول org؛ همهٔ دسترسی‌ها از قرارداد IPeopleDirectory
// (ماژول غایب = فهرست‌های خالی و اتصال ممکن نیست — سامانه سالم).
public class UsersController(AppDbContext db, ICurrentUser me, IWebHostEnvironment env,
    IPeopleDirectory people, RbacService rbac) : MvcBase(db, me)
{
    private readonly IWebHostEnvironment _env = env;
    private readonly RbacService _rbac = rbac;   // فاز ۷۴ — Bump نسخهٔ RBAC پس از تغییر نقش/دسترسی/دامنه

    [HttpGet]
    [HasPerm("users.view")]
    public async Task<IActionResult> Index()
    {
        var users = await Db.Users
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .Include(u => u.Company)
            .OrderBy(u => u.Id).ToListAsync();
        ViewBag.Users = users;
        ViewBag.Roles = await Db.Roles
            .Include(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Id).ToListAsync();
        ViewBag.Perms = await Db.Permissions.OrderBy(p => p.Grp).ThenBy(p => p.Key).ToListAsync();
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        // نام پرسنلِ متصل — از قرارداد (org؛ غایب = ستون خالی)
        var empIds = users.Where(u => !string.IsNullOrEmpty(u.EmployeeId))
            .Select(u => u.EmployeeId!).Distinct().ToList();
        ViewBag.People = empIds.Count == 0
            ? new Dictionary<string, HrPersonBrief>()
            : await people.BriefsAsync(empIds);
        // فاز ۷۴ — واحدها/شرکت‌ها + ردیف‌های دامنهٔ رکوردی نقش‌ها (برچسب و فرم)
        // نقش می‌تواند چند ردیف دامنه داشته باشد (چند شرکت/واحد) — GroupBy تا کرش کلید تکراری نباشد
        ViewBag.Depts = await people.DepartmentsAsync();
        ViewBag.RoleScopes = (await Db.RoleScopes.ToListAsync())
            .GroupBy(rs => rs.RoleId).ToDictionary(g => g.Key, g => g.First());
        return View();
    }

    // ---------- فاز ۷۶ — نمای دسترسی‌های مؤثر کاربر (پیشنهاد ثبت‌شدهٔ فاز ۷۵) ----------
    /// <summary>
    /// مجموعهٔ مؤثرِ واقعی دسترسی‌های کاربر: نقش‌ها + پرچم superadmin + خروجی زندهٔ RbacService
    /// (همان منبعی که میان‌افزار از آن claim می‌سازد) — برای هر کلید کاتالوگ «آیا باز است؟»
    /// و «چرا؟» (مستقیم / wildcard / والد / superadmin) + کلیدهای خام خارج از کاتالوگ.
    /// فقط خواندنی — کلید users.view.
    /// </summary>
    [HttpGet]
    [HasPerm("users.view")]
    public async Task<IActionResult> Perms(string id)
    {
        var u = await Db.Users
            .Include(x => x.Roles).ThenInclude(ur => ur.Role)
            .Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();

        var sa = await _rbac.IsSuperadminAsync(id);
        var granted = await _rbac.GetPermsAsync(id, sa);
        var catalog = await Db.Permissions.AsNoTracking()
            .OrderBy(p => p.Grp).ThenBy(p => p.Key).ToListAsync();
        var catalogKeys = catalog.Select(p => p.Key).ToHashSet();

        // منشأ اثر هر کلید — همان ترتیب تطبیق RbacService.Matches (دقیق ← * ← wildcard ← والد)
        string Reason(string key)
        {
            if (sa) return "superadmin";
            if (granted.Contains(key)) return "مستقیم";
            if (granted.Contains("*")) return "*";
            var parts = key.Split('.');
            for (var i = parts.Length - 1; i >= 1; i--)
                if (granted.Contains(string.Join('.', parts.Take(i)) + ".*")) return "wildcard";
            if (RbacService.PermParents.TryGetValue(key, out var ps))
                foreach (var p in ps)
                    if (granted.Contains(p)) return "والد: " + p;
            return "";
        }

        var rows = new List<(string Grp, string Key, string Reason)>();
        foreach (var p in catalog)
        {
            var r = Reason(p.Key);
            if (r != "") rows.Add((p.Grp, p.Key, r));
        }
        var extra = granted.Where(k => !catalogKeys.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        ViewBag.PUser = u;
        ViewBag.PSa = sa;
        ViewBag.PRows = rows.GroupBy(r => r.Grp).Select(g => (Grp: g.Key, Items: g.ToList())).ToList();
        ViewBag.PExtra = extra;
        ViewBag.PPeople = string.IsNullOrEmpty(u.EmployeeId)
            ? null
            : (await people.BriefsAsync(new[] { u.EmployeeId! })).GetValueOrDefault(u.EmployeeId!);
        return View();
    }

    // ---------- ایجاد کاربر جدید (دسترسی مجزا: users.create) ----------

    /// <summary>فرم ایجاد کاربر جدید — نقش‌ها + پرسنل بدون حساب کاربری. حالت partial برای مودال AJAX.</summary>
    [HttpGet]
    [HasPerm("users.create")]
    public async Task<IActionResult> Create()
    {
        await FillCreateViewBagAsync();
        if (Partial) return PartialView("_CreateForm");
        return View();
    }

    private async Task FillCreateViewBagAsync()
    {
        ViewBag.Roles = await Db.Roles.OrderBy(r => r.Id).ToListAsync();
        ViewBag.Companies = await Db.Companies.Where(c => c.Active).OrderBy(c => c.Code).ToListAsync();
        // پرسنلی که هنوز به هیچ حساب کاربری متصل نشده‌اند — از قرارداد (org)
        ViewBag.FreeEmployees = await people.UnlinkedAsync();
    }

    /// <summary>ثبت کاربر جدید — سیاست رمز، یکتایی نام کاربری، اتصال اختیاری پرسنل و نقش‌ها.</summary>
    [HttpPost]
    [HasPerm("users.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string username, string fullName, string password,
        string password2, string[]? roleIds, string? employeeId, bool isActive = true, string? companyId = null,
        IFormFile? photoFile = null)
    {
        username = (username ?? "").Trim();
        fullName = (fullName ?? "").Trim();

        if (username.Length < 3 || username.Length > 60)
            return await CreateErr("نام کاربری باید بین ۳ تا ۶۰ کاراکتر باشد");
        if (!Regex.IsMatch(username, @"^[A-Za-z0-9._-]+$"))
            return await CreateErr("نام کاربری فقط می‌تواند شامل حروف لاتین، عدد، نقطه، خط تیره و زیرخط باشد");
        if (await Db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            return await CreateErr($"نام کاربری «{username}» قبلاً استفاده شده است");

        if (fullName.Length < 2) return await CreateErr("نام کامل کاربر را وارد کنید");
        if (password != password2) return await CreateErr("تکرار رمز عبور با رمز عبور یکسان نیست");
        var policyErr = Jalali.ValidatePassword(password);
        if (policyErr is not null) return await CreateErr(policyErr);

        string? empId = null;
        string? userCompanyId = null;
        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            // پرسنل ∈ ماژول org (قرارداد) — شناسه/کد پرسنلی/شرکت مالک
            var emp = await people.FindAsync(employeeId.Trim());
            if (emp is null) return await CreateErr("پرسنل انتخاب‌شده یافت نشد");
            if (await Db.Users.AnyAsync(u => u.EmployeeId == emp.Id))
                return await CreateErr($"پرسنل «{emp.FullName}» قبلاً به کاربر دیگری متصل شده است");
            empId = emp.Id;
            userCompanyId = emp.CompanyId;   // چندشرکتی: کاربر متصل به پرسنل، هم‌شرکت پرسنل است
        }
        else if (!string.IsNullOrWhiteSpace(companyId))
        {
            var co = await Db.Companies.FindAsync(companyId);
            if (co is null) return await CreateErr("شرکت انتخاب‌شده یافت نشد");
            userCompanyId = co.Id;
        }

        var wanted = (roleIds ?? Array.Empty<string>()).ToHashSet();
        var roles = await Db.Roles.Where(r => wanted.Contains(r.Id)).ToListAsync();

        var user = new User
        {
            Username = username,
            FullName = fullName,
            PasswordHash = PasswordService.Hash(password),
            PasswordChangedAt = DateTime.Now,
            PasswordExpiresAt = DateTime.Now.AddDays(90),
            MustChangePassword = false,
            IsActive = isActive,
            EmployeeId = empId,
            CompanyId = userCompanyId,
        };
        user.PasswordHistory.Add(new PasswordHistory { PasswordHash = user.PasswordHash });
        foreach (var r in roles) user.Roles.Add(new UserRole { RoleId = r.Id });
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        // عکس پروفایل (فاز ۴۵) — پس از ثبت رکورد تا شناسه در نام فایل استفاده شود
        if (photoFile is not null && photoFile.Length > 0)
        {
            var (photo, photoErr) = await AvatarUpload.SaveAsync(_env, photoFile, $"u{user.Id}", null);
            if (photoErr is not null)
            {
                ToastErr(HrT.F("کاربر ایجاد شد اما بارگذاری عکس ناموفق بود: {0}", photoErr));
            }
            else if (photo is not null)
            {
                user.PhotoPath = photo;
                await Db.SaveChangesAsync();
            }
        }

        await Audit("create", "user", user.Username,
            new { roles = roles.Select(r => r.Name).ToArray(), employeeId = empId, active = isActive, companyId = userCompanyId });
        var msg = $"کاربر «{user.Username}» ایجاد شد" +
                  (roles.Count > 0 ? $" با {Jalali.Num(roles.Count)} نقش" : "") +
                  (isActive ? "" : " (غیرفعال)");
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg, new { redirect = "/Users" });
        return Redirect("/Users");
    }

    /// <summary>خطای اعتبارسنجی ایجاد کاربر — AJAX: JSON + بهست زنده؛ عادی: بازگشت به فرم با پیام.</summary>
    private async Task<IActionResult> CreateErr(string err)
    {
        ToastErr(err);
        if (Ajax) return AjaxResult(false, err);
        await FillCreateViewBagAsync();
        return View();
    }

    // ---------- اتصال دوطرفه پرسنل ↔ کاربر (تسک ۲۷) ----------
    // کلید پیوند روی User.EmployeeId است؛ عملیات واحد همین اکشن است و هر دو صفحه
    // «کاربران» (مودال اتصال) و «پرسنل» (بخش حساب کاربری متصل) از آن استفاده می‌کنند.

    /// <summary>فرم اتصال پرسنل به کاربر — فهرست پرسنل بدون حساب + وضعیت فعلی اتصال.</summary>
    [HttpGet]
    [HasPerm("users.edit")]
    public async Task<IActionResult> LinkEmployee(string userId)
    {
        var user = await Db.Users
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return NotFound();
        // نام پرسنلِ متصل فعلی — از قرارداد (org)
        ViewBag.Current = !string.IsNullOrEmpty(user.EmployeeId)
            ? await people.FindAsync(user.EmployeeId) : null;
        await FillLinkViewBagAsync();
        if (Partial) return PartialView("_LinkForm", user);
        return View(user);
    }

    private async Task FillLinkViewBagAsync()
    {
        // پرسنلی که هنوز به هیچ حساب کاربری متصل نشده‌اند — از قرارداد (org)
        ViewBag.FreeEmployees = await people.UnlinkedAsync();
    }

    /// <summary>
    /// اتصال/تغییر/قطع پیوند کاربر ↔ پرسنل.
    /// employeeId خالی = قطع اتصال؛ addEmployeeRole = نقش «پرسنل» برای دسترسی پرتال من.
    /// گاردها: یکتایی اتصال (هر پرسنل فقط یک کاربر)، منع اتصال کاربر خودکار emp:*
    /// (آن حساب‌ها با ورود کد پرسنلی خودکار ساخته می‌شوند و رمز قابل‌استفاده ندارند).
    /// </summary>
    [HttpPost]
    [HasPerm("users.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkEmployee(string userId, string? employeeId, bool addEmployeeRole = true)
    {
        var user = await Db.Users.Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        { ToastErr("کاربر یافت نشد"); if (Ajax) return AjaxResult(false, "کاربر یافت نشد"); return Redirect("/Users"); }

        // ---------- قطع اتصال ----------
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            if (string.IsNullOrEmpty(user.EmployeeId))
            {
                ToastErr(HrT.F("کاربر «{0}» از قبل به هیچ پرسنلی متصل نیست", user.Username));
                if (Ajax) return AjaxResult(false, HrT.F("کاربر «{0}» از قبل به هیچ پرسنلی متصل نیست", user.Username));
                return Redirect("/Users");
            }
            var oldEmp = await people.FindAsync(user.EmployeeId);
            user.EmployeeId = null;
            user.UpdatedAt = DateTime.Now;
            await Db.SaveChangesAsync();
            await Audit("unlink_employee", "user", user.Username,
                new { employeeId = oldEmp?.Id, personnelCode = oldEmp?.PersonnelCode });
            var umsg = $"اتصال کاربر «{user.Username}» از پرسنل «{oldEmp?.FullName}» قطع شد";
            ToastOk(umsg);
            if (Ajax) return AjaxResult(true, umsg);
            return Redirect("/Users");
        }

        // ---------- اتصال / تغییر اتصال ----------
        if (user.Username.StartsWith("emp:"))
        {
            ToastErr("حساب خودکار پرسنل (emp:…) قابل ویرایش اتصال نیست — این حساب‌ها با ورود کد پرسنلی ساخته می‌شوند");
            if (Ajax) return AjaxResult(false, "حساب خودکار پرسنل (emp:…) قابل ویرایش اتصال نیست");
            return Redirect("/Users");
        }

        // پرسنل ∈ ماژول org (قرارداد)
        var emp = await people.FindAsync(employeeId.Trim());
        if (emp is null)
        { ToastErr("پرسنل انتخاب‌شده یافت نشد"); if (Ajax) return AjaxResult(false, "پرسنل انتخاب‌شده یافت نشد"); return Redirect("/Users"); }

        var other = await Db.Users.FirstOrDefaultAsync(u =>
            u.EmployeeId == emp.Id && u.Id != user.Id);
        if (other is not null)
        {
            ToastErr(HrT.F("پرسنل «{0}» قبلاً به کاربر «{1}» متصل شده است", emp.FullName, other.Username));
            if (Ajax) return AjaxResult(false, HrT.F("پرسنل «{0}» قبلاً به کاربر «{1}» متصل شده است", emp.FullName, other.Username));
            return Redirect("/Users");
        }

        var changed = user.EmployeeId != emp.Id;
        user.EmployeeId = emp.Id;
        user.UpdatedAt = DateTime.Now;

        // نقش «پرسنل» برای دسترسی به پرتال من — فقط اگر صریحاً خواسته شده و موجود نباشد
        var roleAdded = false;
        if (addEmployeeRole)
        {
            var empRole = await Db.Roles.FirstOrDefaultAsync(r => r.Name == "employee");
            if (empRole is not null && user.Roles.All(ur => ur.RoleId != empRole.Id))
            {
                Db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = empRole.Id });
                roleAdded = true;
            }
        }
        await Db.SaveChangesAsync();

        await Audit(changed ? "link_employee" : "update", "user", user.Username,
            new { employeeId = emp.Id, emp.PersonnelCode, roleAdded, changed });
        var msg = changed
            ? $"کاربر «{user.Username}» به پرسنل «{emp.PersonnelCode} — {emp.FullName}» متصل شد" +
              (roleAdded ? " و نقش «پرسنل» افزوده شد" : "")
            : $"اتصال کاربر «{user.Username}» بدون تغییر ماند";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }

    // ---------- عکس پروفایل کاربر (فاز ۴۵) ----------

    /// <summary>فرم بارگذاری/تعویض عکس پروفایل — پاپ‌آپ AJAX از فهرست کاربران.</summary>
    [HttpGet]
    [HasPerm("users.edit")]
    public async Task<IActionResult> Photo(string id)
    {
        var u = await Db.Users.FindAsync(id);
        if (u is null) return NotFound();
        return PartialView("_PhotoForm", u);
    }

    [HttpPost]
    [HasPerm("users.edit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> SetPhoto(string id, IFormFile? photoFile, bool remove = false)
    {
        var u = await Db.Users.FindAsync(id);
        if (u is null) return NotFound();
        if (remove)
        {
            AvatarUpload.Delete(_env, u.PhotoPath);
            u.PhotoPath = null;
            u.UpdatedAt = DateTime.Now;
            await Db.SaveChangesAsync();
            await Audit("remove_photo", "user", u.Username, null);
            return ToastDone(true, HrT.F("عکس پروفایل «{0}» حذف شد", u.Username), "/Users");
        }
        var (photo, err) = await AvatarUpload.SaveAsync(_env, photoFile, $"u{u.Id}", u.PhotoPath);
        if (err is not null) return AjaxResult(false, err);
        if (photo is null) return AjaxResult(false, "فایلی انتخاب نشده است");
        u.PhotoPath = photo;
        u.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("upload_photo", "user", u.Username, new { photo });
        return ToastDone(true, HrT.F("عکس پروفایل «{0}» به‌روزرسانی شد — در نوار بالایی با پویش بعدی اعمال می‌شود", u.Username), "/Users");
    }

    [HttpPost]
    [HasPerm("users.reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var u = await Db.Users.FindAsync(id);
        if (u is null) return NotFound();
        u.FailedLoginAttempts = 0;
        u.LockedUntil = null;
        await Db.SaveChangesAsync();
        await Audit("unlock", "user", u.Username, null);
        var msg = $"حساب «{u.Username}» بازگشایی شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }

    /// <summary>بازنشانی رمز با سیاست رمز و کنترل تاریخچه (PLAN P1.2/P1.4).</summary>
    [HttpPost]
    [HasPerm("users.reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var u = await Db.Users.Include(x => x.PasswordHistory).FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();
        var policyErr = Jalali.ValidatePassword(newPassword);
        if (policyErr is not null) { ToastErr(policyErr); if (Ajax) return AjaxResult(false, policyErr); return Redirect("/Users"); }

        var newHash = PasswordService.Hash(newPassword);
        // جلوگیری از استفاده مجدد ۳ رمز اخیر
        var recent = u.PasswordHistory.OrderByDescending(h => h.CreatedAt).Take(3).ToList();
        if (recent.Any(h => PasswordService.Verify(newPassword, h.PasswordHash)))
        {
            ToastErr("رمز جدید نباید با ۳ رمز اخیر یکسان باشد");
            if (Ajax) return AjaxResult(false, "رمز جدید نباید با ۳ رمز اخیر یکسان باشد");
            return Redirect("/Users");
        }

        u.PasswordHistory.Add(new PasswordHistory { UserId = u.Id, PasswordHash = newHash });
        u.PasswordHash = newHash;
        u.PasswordChangedAt = DateTime.Now;
        u.MustChangePassword = false;
        u.PasswordExpiresAt = DateTime.Now.AddDays(90);
        await Db.SaveChangesAsync();
        await Audit("reset_password", "user", u.Username, null);
        var msg = $"رمز «{u.Username}» بازنشانی شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }

    [HttpPost]
    [HasPerm("users.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var u = await Db.Users.FindAsync(id);
        if (u is null) return NotFound();
        if (u.IsSuperadmin && u.IsActive)
        {
            ToastErr("حساب مدیر ارشد قابل غیرفعال‌سازی نیست");
            if (Ajax) return AjaxResult(false, "حساب مدیر ارشد قابل غیرفعال‌سازی نیست");
            return Redirect("/Users");
        }
        u.IsActive = !u.IsActive;
        await Db.SaveChangesAsync();
        await Audit("update", "user", u.Username, new { active = u.IsActive });
        var msg = $"حساب «{u.Username}» {u.StatusFa()}";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }

    [HttpPost]
    [HasPerm("users.reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSessions(string id)
    {
        var tokens = await Db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync();
        foreach (var t in tokens) t.RevokedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        var u = await Db.Users.FindAsync(id);
        await Audit("revoke_sessions", "user", u?.Username, new { count = tokens.Count });
        var msg = $"{tokens.Count} نشست فعال لغو شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }

    [HttpPost]
    [HasPerm("users.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRoles(string id, string[] roleIds)
    {
        var u = await Db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();
        if (u.IsSuperadmin)
        {
            ToastErr("نقش‌های مدیر ارشد قابل تغییر نیست");
            if (Ajax) return AjaxResult(false, "نقش‌های مدیر ارشد قابل تغییر نیست");
            return Redirect("/Users");
        }
        Db.UserRoles.RemoveRange(u.Roles);
        foreach (var rid in roleIds.Distinct())
            Db.UserRoles.Add(new UserRole { UserId = id, RoleId = rid });
        await Db.SaveChangesAsync();
        _rbac.Bump();   // فاز ۷۴ — اعمال فوری روی نشست‌های فعال (میان‌افزار RbacRefresh)
        await Audit("set_roles", "user", u.Username, new { roleIds });
        var msg = $"نقش‌های «{u.Username}» به‌روزرسانی شد — بلافاصله برای نشست فعال او اعمال می‌شود";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }

    /// <summary>فاز ۷۵ — حذف کاربر (کلید ریز users.delete). گاردها: superadmin و خودِ کاربر حذف‌شدنی نیستند؛
    /// پیوند پرسنل فقط قطع می‌شود (رکورد پرسنل دست‌نخورده) و نشست‌های فعال با حذف توکن‌ها می‌میرند.</summary>
    [HttpPost]
    [HasPerm("users.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var u = await Db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();
        if (u.IsSuperadmin)
        {
            const string err = "حساب مدیر ارشد قابل حذف نیست";
            ToastErr(err);
            if (Ajax) return AjaxResult(false, err);
            return Redirect("/Users");
        }
        if (u.Id == Me.Get()!.Id)
        {
            const string err = "حساب خودتان قابل حذف نیست";
            ToastErr(err);
            if (Ajax) return AjaxResult(false, err);
            return Redirect("/Users");
        }
        var linked = u.EmployeeId;
        Db.UserRoles.RemoveRange(u.Roles);
        Db.RefreshTokens.RemoveRange(await Db.RefreshTokens.Where(t => t.UserId == id).ToListAsync());
        u.EmployeeId = null;                    // قطع پیوند — پرسنل باقی می‌ماند
        Db.Users.Remove(u);
        await Db.SaveChangesAsync();
        _rbac.Drop(id);                         // فاز ۷۵ — تخلیهٔ کش دسترسی همان کاربر
        await Audit("delete", "user", u.Username, new { linkedEmployeeId = linked });
        ToastOk(HrT.F("کاربر «{0}» حذف شد", u.Username));
        if (Ajax) return AjaxResult(true, "کاربر حذف شد");
        return Redirect("/Users");
    }

    // ===================== فاز ۷۴ — CRUD نقش‌ها (roles.manage) =====================

    /// <summary>
    /// فاز ۷۷ — صفحهٔ مدیریت نقش‌ها: جدول صفحه‌بندی‌شده با جستجو (نقش‌ها می‌توانند خیلی زیاد شوند)
    /// + سلسله‌مراتب نقش‌ها (نقش فرزند دسترسی‌های والد را به ارث می‌برد). ماتریس دسترسی‌ها
    /// به مودال درختی منتقل شد (RolePerms) تا صفحه شلوغ نشود.
    /// </summary>
    [HttpGet]
    [HasPerm("roles.view")]
    public async Task<IActionResult> Roles(int page = 1, string? q = null)
    {
        const int pageSize = 10;
        q = (q ?? "").Trim();
        var query = Db.Roles.AsNoTracking().AsQueryable();
        if (q.Length > 0)
            query = query.Where(r => r.Name.Contains(q) || r.Title.Contains(q) || (r.Description ?? "").Contains(q));
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pages);

        var roles = await query.OrderBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(r => r.ParentRole)
            .Include(r => r.Permissions)
            .ToListAsync();

        // نقشهٔ کامل والد→فرزند برای محاسبهٔ دسترسی‌های ارثی (جدول نقش‌ها حتی در بدترین حالت کوچک است)
        var parentMap = await Db.Roles.AsNoTracking().Select(r => new { r.Id, r.ParentRoleId })
            .ToDictionaryAsync(x => x.Id, x => x.ParentRoleId);
        var roleTitles = await Db.Roles.AsNoTracking().Select(r => new { r.Id, r.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title);
        var userCounts = await Db.UserRoles.AsNoTracking().GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.RoleId, x => x.Count);
        var childCounts = await Db.Roles.AsNoTracking().Where(r => r.ParentRoleId != null)
            .GroupBy(r => r.ParentRoleId!).Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count);

        // کلیدهای مستقیم هر نقشِ همین صفحه (برای محاسبهٔ ارثی)
        var ids = roles.Select(r => r.Id).ToList();
        var directKeys = await Db.RolePermissions.AsNoTracking().Where(rp => ids.Contains(rp.RoleId))
            .Join(Db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => new { rp.RoleId, p.Key })
            .ToListAsync();
        var directByRole = directKeys.GroupBy(x => x.RoleId).ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToHashSet());

        // دامنهٔ رکوردی هر نقش (برچسب کوتاه — هم‌قرارداد با صفحهٔ کاربران)
        var scopeRows = await Db.RoleScopes.AsNoTracking().Where(rs => ids.Contains(rs.RoleId)).ToListAsync();
        var coNames = await Db.Companies.AsNoTracking().Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name);
        var deptList = await ScopedDeptsAsync();
        var deptNames = deptList.ToDictionary(d => d.Id, d => d.Name);
        string ScopeBadge(string roleId)
        {
            var row = scopeRows.FirstOrDefault(rs => rs.RoleId == roleId);
            if (row is null) return HrT.T("همه داده‌ها");
            return row.Kind switch
            {
                "company" => row.RefId is not null && coNames.TryGetValue(row.RefId, out var cn) ? HrT.F("شرکت: {0}", cn) : HrT.T("شرکت خودم"),
                "dept" => row.RefId is not null && deptNames.TryGetValue(row.RefId, out var dn) ? HrT.F("واحد: {0}", dn) : HrT.T("واحد خودم"),
                "self" => HrT.T("فقط خودم"),
                _ => HrT.T("همه داده‌ها"),
            };
        }

        var rows = roles.Select(r =>
        {
            // زنجیرهٔ اجداد + اجتماع کلیدهای ارثی
            var ancestors = new List<string>();
            var cur = r.ParentRoleId;
            var seen = new HashSet<string> { r.Id };
            while (cur is not null && seen.Add(cur) && ancestors.Count < 12)
            {
                ancestors.Add(cur);
                cur = parentMap.GetValueOrDefault(cur);
            }
            var inherited = new HashSet<string>();
            foreach (var a in ancestors)
                if (directByRole.TryGetValue(a, out var ks))
                    foreach (var k in ks) inherited.Add(k);
            return new RoleRow(
                r.Id, r.Name, r.Title, r.Description ?? "", r.IsBuiltIn,
                r.ParentRoleId, r.ParentRole?.Title ?? (r.ParentRoleId is null ? "" : "؟"),
                r.Permissions.Count, inherited.Count,
                userCounts.GetValueOrDefault(r.Id), childCounts.GetValueOrDefault(r.Id),
                ancestors.Count, string.Join(" ← ", ancestors.Select(a => roleTitles.GetValueOrDefault(a, "؟"))),
                ScopeBadge(r.Id));
        }).ToList();

        ViewBag.Rows = rows;
        ViewBag.Page = page;
        ViewBag.Pages = pages;
        ViewBag.Total = total;
        ViewBag.Q = q;
        ViewBag.CanRoleCreate = Me.Get()!.Has("roles.create");
        ViewBag.CanRoleEdit = Me.Get()!.Has("roles.edit");
        ViewBag.CanRoleDelete = Me.Get()!.Has("roles.delete");
        return View();
    }

    public sealed record RoleRow(string Id, string Name, string Title, string Description, bool IsBuiltIn,
        string? ParentRoleId, string ParentTitle, int DirectCount, int InheritedCount,
        int UserCount, int ChildCount, int Depth, string InheritedFrom, string ScopeBadge);

    /// <summary>مودال درخت دسترسی‌های نقش — کاتالوگ به‌صورت درختِ کلیدها؛ ارثی‌ها تیکِ قفل دارند.</summary>
    [HttpGet]
    [HasPerm("roles.view")]
    public async Task<IActionResult> RolePerms(string roleId)
    {
        var role = await Db.Roles.Include(r => r.ParentRole).AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return NotFound();
        ViewBag.Role = role;
        ViewBag.Perms = await Db.Permissions.AsNoTracking().OrderBy(p => p.Key).ToListAsync();
        var directList = await Db.RolePermissions.AsNoTracking().Where(rp => rp.RoleId == roleId)
            .Join(Db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Key).ToListAsync();
        ViewBag.DirectKeys = directList.ToHashSet();

        // کلیدهای ارثی از زنجیرهٔ والدین (همان منطق RbacService — برای نمایش «تیکِ قفل‌شده»)
        var inherited = new HashSet<string>();
        var chain = new HashSet<string> { roleId };
        var cur = role.ParentRoleId;
        for (var depth = 0; depth < 12 && cur is not null && chain.Add(cur); depth++)
        {
            var ks = await Db.RolePermissions.AsNoTracking().Where(rp => rp.RoleId == cur)
                .Join(Db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Key).ToListAsync();
            foreach (var k in ks) inherited.Add(k);
            cur = await Db.Roles.AsNoTracking().Where(r => r.Id == cur).Select(r => r.ParentRoleId).FirstOrDefaultAsync();
        }
        ViewBag.InheritedKeys = inherited;
        return PartialView("_RolePerms");
    }

    /// <summary>فرم ایجاد/ویرایش نقش — مودال AJAX. id=null = ایجاد، عدد = ویرایش.
    /// فاز ۷۴ — همراه با دامنهٔ رکوردی (all/company/dept/self + انتخاب شرکت/واحد).
    /// فاز ۷۷ — انتخاب نقش والد (ساختار درختی)؛ خودِ نقش و نسل‌های پایین‌ترش گزینه نیستند.</summary>
    [HttpGet]
    [HasPerm("roles.view")]
    public async Task<IActionResult> RoleForm(string? id)
    {
        Role? role = null;
        if (id is not null)
        {
            role = await Db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
            if (role is null) return NotFound();
        }
        ViewBag.Companies = await Db.Companies.Where(c => c.Active).OrderBy(c => c.Code).ToListAsync();
        ViewBag.Depts = await ScopedDeptsAsync();
        ViewBag.ScopeRows = role is null
            ? new List<RoleScope>()
            : await Db.RoleScopes.Where(rs => rs.RoleId == role.Id).ToListAsync();

        // فاز ۷۷ — گزینه‌های نقش والد: همهٔ نقش‌ها جز خودِ نقش و نسل‌های پایین‌ترش (ضدچرخه)
        var allRoles = await Db.Roles.AsNoTracking().OrderBy(r => r.Title)
            .Select(r => new { r.Id, r.Title, r.ParentRoleId }).ToListAsync();
        var banned = new HashSet<string>();
        if (role is not null)
        {
            banned.Add(role.Id);
            var frontier = new List<string> { role.Id };
            for (var depth = 0; depth < 12 && frontier.Count > 0; depth++)
            {
                var next = allRoles.Where(r => r.ParentRoleId is not null && frontier.Contains(r.ParentRoleId)
                                            && banned.Add(r.Id)).Select(r => r.Id).ToList();
                frontier = next;
            }
        }
        ViewBag.ParentOptions = allRoles.Where(r => !banned.Contains(r.Id)).ToList();
        return PartialView("_RoleForm", role);
    }

    private async Task<List<HrDeptOption>> ScopedDeptsAsync()
    {
        try { return await Db.Set<Department>().OrderBy(d => d.Name)
            .Select(d => new HrDeptOption(d.Id, d.Name)).ToListAsync(); }
        catch { return new List<HrDeptOption>(); }   // org غایب — سامانه سالم
    }

    /// <summary>نوشتن ردیف‌های دامنهٔ رکوردی نقش — kind=all بدون Ref؛ company/dept با RefIdهای انتخابی؛ self بدون Ref.</summary>
    private async Task WriteScopeRowsAsync(string roleId, string? scopeKind, string? scopeRefs)
    {
        var kind = (scopeKind ?? "all").Trim();
        if (kind is not ("company" or "dept" or "self")) kind = "all";
        var refs = (scopeRefs ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0).Distinct().ToList();
        Db.RoleScopes.RemoveRange(await Db.RoleScopes.Where(rs => rs.RoleId == roleId).ToListAsync());
        if (kind == "company")
        {
            var valid = await Db.Companies.Where(c => refs.Contains(c.Id)).Select(c => c.Id).ToListAsync();
            foreach (var id in valid) Db.RoleScopes.Add(new RoleScope { RoleId = roleId, Kind = "company", RefId = id });
            if (valid.Count == 0) Db.RoleScopes.Add(new RoleScope { RoleId = roleId, Kind = "company", RefId = null });
        }
        else if (kind == "dept")
        {
            // واحد ∈ org — فقط شناسه‌های موجود پذیرفته می‌شوند (org غایب = دامنه ذخیره نمی‌شود)
            var orgOk = Db.Model.FindEntityType(typeof(Department)) is not null;
            if (orgOk)
            {
                var validD = await Db.Set<Department>().Where(d => refs.Contains(d.Id)).Select(d => d.Id).ToListAsync();
                foreach (var id in validD) Db.RoleScopes.Add(new RoleScope { RoleId = roleId, Kind = "dept", RefId = id });
            }
        }
        else if (kind == "self")
        {
            Db.RoleScopes.Add(new RoleScope { RoleId = roleId, Kind = "self", RefId = null });
        }
        // kind == "all" → بدون ردیف = بدون محدودیت (سازگاری فاز ۷۴)
        await Db.SaveChangesAsync();
    }

    private IActionResult RoleErr(string err, string? id)
    {
        ToastErr(err);
        if (Ajax) return AjaxResult(false, err);
        return Redirect(id is null ? "/Users/Roles" : $"/Users/RoleForm/{id}");
    }

    /// <summary>اعتبارسنجی والدِ نقش (فاز ۷۷) — باید موجود باشد و در زنجیرهٔ پایین‌دستیِ خود نقش نیفتد (ضدچرخه).</summary>
    private async Task<string?> ValidateParentAsync(string roleId, string? parentId)
    {
        if (parentId is null) return null;
        if (parentId == roleId) return HrT.T("نقش والد نمی‌تواند خودِ نقش باشد");
        if (!await Db.Roles.AnyAsync(r => r.Id == parentId)) return HrT.T("نقش والد یافت نشد");
        var cur = parentId;
        var seen = new HashSet<string>();
        while (cur is not null && seen.Add(cur))
        {
            if (cur == roleId) return HrT.T("انتخاب این والد چرخه می‌سازد — نقش نمی‌تواند نسلِ پایین‌ترِ خودش باشد");
            cur = await Db.Roles.Where(r => r.Id == cur).Select(r => r.ParentRoleId).FirstOrDefaultAsync();
        }
        return null;
    }

    /// <summary>ایجاد نقش جدید — نام لاتین یکتا + دامنهٔ رکوردی اختیاری (فاز ۷۴) + نقش والد (فاز ۷۷).</summary>
    [HttpPost]
    [HasPerm("roles.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RoleCreate(string name, string title, string? description,
        string? scopeKind = "all", string? scopeRefs = null, string? parentRoleId = null)
    {
        name = (name ?? "").Trim();
        title = (title ?? "").Trim();
        if (name.Length < 3 || name.Length > 40 || !Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]*$"))
            return RoleErr(HrT.T("نام سیستمی نقش باید ۳ تا ۴۰ کاراکتر لاتین/عدد/زیرخط باشد"), null);
        if (await Db.Roles.AnyAsync(r => r.Name == name))
            return RoleErr(HrT.F("نام سیستمی «{0}» قبلاً استفاده شده است", name), null);
        if (title.Length < 2) return RoleErr(HrT.T("عنوان نقش را وارد کنید"), null);
        var parentErr = await ValidateParentAsync("", parentRoleId);
        if (parentErr is not null) return RoleErr(parentErr, null);

        var role = new Role
        {
            Name = name, Title = title, Description = description?.Trim(), IsBuiltIn = false,
            ParentRoleId = parentRoleId,
        };
        Db.Roles.Add(role);
        await Db.SaveChangesAsync();
        await WriteScopeRowsAsync(role.Id, scopeKind, scopeRefs);
        _rbac.Bump();
        await Audit("create", "role", role.Name, new { scopeKind, scopeRefs, parentRoleId });
        var msg = HrT.F("نقش «{0}» ایجاد شد — دسترسی‌هایش را از جدول تنظیم کنید", role.Title);
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg, new { redirect = Url.Action("Roles") });
        return Redirect("/Users/Roles");
    }

    /// <summary>ویرایش نقش — عنوان/توضیح/دامنهٔ رکوردی/نقش والد؛ نام نقش داخلی تغییر نمی‌کند.</summary>
    [HttpPost]
    [HasPerm("roles.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RoleEdit(string id, string? name, string title, string? description,
        string? scopeKind = "all", string? scopeRefs = null, string? parentRoleId = null)
    {
        var role = await Db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();
        title = (title ?? "").Trim();
        if (title.Length < 2) return RoleErr(HrT.T("عنوان نقش را وارد کنید"), id);
        var parentErr = await ValidateParentAsync(id, parentRoleId);
        if (parentErr is not null) return RoleErr(parentErr, id);
        if (!role.IsBuiltIn)
        {
            var newName = (name ?? "").Trim();
            if (newName.Length >= 3 && newName.Length <= 40 && newName != role.Name)
            {
                if (!Regex.IsMatch(newName, @"^[A-Za-z][A-Za-z0-9_]*$"))
                    return RoleErr(HrT.T("نام سیستمی نقش فقط می‌تواند حروف لاتین، عدد و زیرخط باشد"), id);
                if (await Db.Roles.AnyAsync(r => r.Name == newName && r.Id != id))
                    return RoleErr(HrT.F("نام سیستمی «{0}» قبلاً استفاده شده است", newName), id);
                role.Name = newName;
            }
        }
        role.Title = title;
        role.Description = description?.Trim();
        role.ParentRoleId = parentRoleId;
        await Db.SaveChangesAsync();
        await WriteScopeRowsAsync(role.Id, scopeKind, scopeRefs);
        _rbac.Bump();   // فاز ۷۷ — ارث‌بری دسترسی ممکن است تغییر کرده باشد؛ کش همه بی‌اعتبار می‌شود
        await Audit("update", "role", role.Name, new { title, scopeKind, scopeRefs, parentRoleId });
        var msg = HrT.F("نقش «{0}» به‌روزرسانی شد — تغییرات بلافاصله اعمال می‌شود", role.Title);
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users/Roles");
    }

    /// <summary>حذف نقش سفارشی — نقش‌های داخلی و نقش دارای کاربر حذف نمی‌شوند.</summary>
    [HttpPost]
    [HasPerm("roles.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RoleDelete(string id)
    {
        var role = await Db.Roles.Include(r => r.Users).Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();
        if (role.IsBuiltIn)
        {
            ToastErr(HrT.T("نقش‌های کاتالوگ سامانه قابل حذف نیستند — می‌توانید دسترسی‌هایشان را کم کنید"));
            if (Ajax) return AjaxResult(false, HrT.T("نقش‌های کاتالوگ سامانه قابل حذف نیستند"));
            return Redirect("/Users/Roles");
        }
        if (role.Users.Count > 0)
        {
            ToastErr(HrT.F("نقش «{0}» به {1} کاربر اختصاص دارد — ابتدا نقش را از کاربران بردارید", role.Title, HrT.Num(role.Users.Count)));
            if (Ajax) return AjaxResult(false, HrT.F("نقش «{0}» به کاربران اختصاص دارد", role.Title));
            return Redirect("/Users/Roles");
        }
        // فاز ۷۷ — نقش دارای فرزند حذف نمی‌شود تا ارث‌بری ساکت از دست نرود
        if (await Db.Roles.AnyAsync(c => c.ParentRoleId == id))
        {
            ToastErr(HrT.F("نقش «{0}» نقشِ فرزند دارد — ابتدا والدِ نقش‌های فرزند را تغییر دهید", role.Title));
            if (Ajax) return AjaxResult(false, HrT.F("نقش «{0}» نقشِ فرزند دارد", role.Title));
            return Redirect("/Users/Roles");
        }
        Db.RolePermissions.RemoveRange(role.Permissions);
        Db.RoleScopes.RemoveRange(await Db.RoleScopes.Where(rs => rs.RoleId == id).ToListAsync());
        Db.Roles.Remove(role);
        await Db.SaveChangesAsync();
        _rbac.Bump();
        await Audit("delete", "role", role.Name, null);
        var msg2 = HrT.F("نقش «{0}» حذف شد", role.Title);
        ToastOk(msg2);
        if (Ajax) return AjaxResult(true, msg2);
        return Redirect("/Users/Roles");
    }

    /// <summary>ویرایش ماتریس نقش ← دسترسی‌ها.</summary>
    [HttpPost]
    [HasPerm("roles.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRolePermissions(string roleId, string permKeys)
    {
        var role = await Db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return NotFound();
        Db.RolePermissions.RemoveRange(role.Permissions);
        var keys = permKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var perms = await Db.Permissions.ToListAsync();
        foreach (var k in keys)
        {
            var p = perms.FirstOrDefault(x => x.Key == k);
            if (p is not null) Db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = p.Id });
        }
        await Db.SaveChangesAsync();
        _rbac.Bump();   // فاز ۷۴ — اعمال فوری بدون ورود مجدد (RbacRefresh)
        await Audit("set_permissions", "role", role.Name, new { count = keys.Length });
        var msg = $"دسترسی‌های نقش «{role.Title}» به‌روزرسانی شد — بلافاصله برای نشست‌های فعال اعمال شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Users");
    }
}

public static class UserStatusExt
{
    public static string StatusFa(this User u) => u.IsActive ? "فعال است" : "غیرفعال شد";
}
