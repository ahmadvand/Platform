using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

// فاز ۶۹ — پرسنل ∈ ماژول org؛ احراز پرتال پرسنل از قرارداد IPeopleDirectory
// (ماژول غایب = ورود پرتال پرسنل همیشه ناموفق — با پیام دوستانه).
public class AuthController : MvcBase
{
    private readonly JwtService _jwt;
    private readonly IPeopleDirectory _people;

    public AuthController(AppDbContext db, HrBaseDbContext baseDb, ICurrentUser me, JwtService jwt,
        IPeopleDirectory people) : base(db, me) { Base = baseDb; _jwt = jwt; _people = people; }

    private readonly HrBaseDbContext Base;

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        // فاز ۵۱ — هشدار وضعیت مجوز روی صفحهٔ ورود (به‌جای قطع سرویس، فقط اطلاع‌رسانی)
        var lic = HttpContext.RequestServices.GetService(typeof(Platform.Web.Services.LicenseService))
                    as Platform.Web.Services.LicenseService;
        if (lic is not null && !string.Equals(lic.Mode, "Off", StringComparison.OrdinalIgnoreCase))
        {
            var snap = lic.Fresh;
            if (snap.Locked) ViewBag.LicNotice = snap.Message;
        }
        return View();
    }

    /// <summary>
    /// ورود یکپارچه (تسک ۲۵) — «یک فرم برای همه»: راهبران با نام کاربری + رمز عبور
    /// و کارکنان با کد پرسنلی + کد ملی وارد می‌شوند؛ سامانه از روی شناسه تشخیص می‌دهد.
    /// اگر شناسه با نام کاربری موجود تطبیق کند مسیر کاربر وگرنه مسیر پرسنل طی می‌شود —
    /// هیچ fallback ای از مسیر کاربر به پرسنل وجود ندارد (جلوی حدس کد ملی برای حساب‌های مدیریتی).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string identifier, string secret)
    {
        var id = (identifier ?? "").Trim();
        var secretV = secret ?? "";
        if (id.Length == 0 || secretV.Length == 0)
        {
            ViewBag.Error = "نام کاربری / کد پرسنلی و رمز عبور / کد ملی را وارد کنید.";
            ViewBag.Identifier = id;
            return View();
        }

        // ---------- مسیر ۱: کاربر سامانه (نام کاربری + رمز) ----------
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == id)
                   ?? await Db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == id.ToLower());
        if (user is not null)
        {
            var r = await AuthCore.LoginAsync(Base, _jwt, user.Username, secretV, Ip,
                Request.Headers.UserAgent.ToString());
            if (r.Error is not null)
            {
                ViewBag.Error = r.Error;
                ViewBag.Identifier = id;
                return View();
            }
            await SignInAsync(r.User!, r.Perms, r.AccessToken!, r.AccessExpires, r.RefreshToken!);
            Response.Cookies.Delete(Platform.Web.Infrastructure.LicenseGate.AckCookie); // فاز ۵۱ — هشدار مجوز پس از هر ورود دوباره نمایش داده شود
            return Redirect("/");
        }

        // ---------- مسیر ۲: کارمند (کد پرسنلی + کد ملی) ----------
        var empResult = await EmployeeLoginAsync(id, secretV);
        if (empResult is string err)
        {
            ViewBag.Error = err;
            ViewBag.Identifier = id;
            return View();
        }
        Response.Cookies.Delete(Platform.Web.Infrastructure.LicenseGate.AckCookie); // فاز ۵۱
        return Redirect("/Self");
    }

    /// <summary>صدور نشست JWT — کوکی دسترسی + توکن بازخوانی چرخشی (مشترک دو مسیر ورود).</summary>
    private async Task SignInAsync(User user, List<string> perms, string accessToken, DateTime accessExpires, string refresh)
    {
        Response.Cookies.Append(JwtService.AccessCookie, accessToken,
            JwtService.AuthCookie((accessExpires - DateTime.UtcNow).TotalSeconds));
        var rt = refresh;
        if (string.IsNullOrEmpty(rt))
        {
            rt = JwtService.RandomToken();
            Db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = JwtService.Sha256Hex(rt),
                ExpiresAt = DateTime.Now.AddDays(7),
                DeviceInfo = Request.Headers.UserAgent.ToString()[..Math.Min(200, Request.Headers.UserAgent.ToString().Length)],
                Ip = Ip,
            });
            await Db.SaveChangesAsync();
        }
        Response.Cookies.Append(JwtService.RefreshCookie, rt, JwtService.AuthCookie(TimeSpan.FromDays(7).TotalSeconds));
    }

    /// <summary>
    /// ورود کارمند با «کد پرسنلی + کد ملی» — همان منطق تسک ۲۳.
    /// خروجی: string = پیام خطا | null = موفق (نشست صادر شده).
    /// </summary>
    private async Task<string?> EmployeeLoginAsync(string personnelCode, string nationalId)
    {
        var code = EssAuth.NormDigits(personnelCode);
        var nid = EssAuth.NormDigits(nationalId);

        var lockKey = $"ess:{Ip}:{code}";
        if (EssAuth.IsLocked(lockKey))
            return HrT.T("به دلیل تلاش‌های ناموفق، ورود موقتاً قفل شده است — ۱۰ دقیقه بعد تلاش کنید.");
        if (code.Length == 0 || nid.Length == 0)
            return HrT.T("کد پرسنلی و کد ملی را وارد کنید.");

        var emp = await _people.AuthenticateAsync(code, nid);
        if (emp is null)
        {
            EssAuth.RegisterFailure(lockKey);
            await Db.Audit(null, "login_failed", "employee-portal", code, new { ip = Ip }, Ip);
            return HrT.F("اطلاعات ورود نادرست است — {0} تلاش باقی مانده.", HrT.Num(EssAuth.RemainingAttempts(lockKey)));
        }

        // پرسنلی که حساب اختصاصی متصل دارد باید با نام کاربری/رمز وارد شود (جلوی دورزدن دسترسی‌ها)
        // تسک ۲۷: شرط IsActive حذف شد — حتی حساب متصلِ غیرفعال هم باید مسدود کند،
        // وگرنه کاربر غیرفعال‌شده می‌تواند با کد پرسنلی+کد ملی از مسیر حساب خودکار emp: وارد پرتال شود
        // و غیرفعال‌سازی حساب را دور بزند.
        var linked = await Db.Users.FirstOrDefaultAsync(u => u.EmployeeId == emp.Id);
        if (linked is not null)
        {
            await Db.Audit(null, "login_blocked", "employee-portal", code, new { linked.Username }, Ip);
            return "این پرسنل حساب کاربری اختصاصی دارد — با نام کاربری و رمز عبور وارد شوید.";
        }

        EssAuth.ClearFailures(lockKey);

        // کاربر خودکار پرسنل (emp:<کد>) — نام کاربری با «:» از فرم users.create قابل ساخت نیست، بدون تصادم
        var user = await Db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Username == $"emp:{code}");
        if (user is null)
        {
            user = new User
            {
                Username = $"emp:{code}",
                FullName = emp.FullName,
                PasswordHash = PasswordService.Hash(JwtService.RandomToken() + ":" + JwtService.RandomToken()),
                IsActive = true,
                // چندشرکتی: حساب خودکار پرسنل در شرکتِ پرسنل ثبت می‌شود
                CompanyId = emp.CompanyId,
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            await Db.Audit(null, "create", "user", user.Id.ToString(),
                new { synthetic = true, emp.PersonnelCode, note = "ورود خودکار پرتال کارکنان" }, Ip);
        }
        var role = await Db.Roles.FirstOrDefaultAsync(r => r.Name == "employee");
        if (role is not null && user.Roles.All(ur => ur.RoleId != role.Id))
        {
            Db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await Db.SaveChangesAsync();
        }

        var perms = await AuthCore.GetPermissionsAsync(Base, user);
        var (token, expires) = _jwt.Sign(user, perms);
        await SignInAsync(user, perms, token, expires, "");

        await Db.Audit(new SessionUser { Id = user.Id, Username = user.Username, FullName = user.FullName },
            "login", "employee-portal", user.Username, new { emp.PersonnelCode, perms }, Ip);
        return null;
    }

    /// <summary>مسیر قدیمی ورود کارکنان — اکنون فقط میان‌بری به منطق یکپارچه (سازگاری با نشانی‌های قدیمی).</summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginEmployee(string personnelCode, string nationalId)
    {
        var err = await EmployeeLoginAsync(personnelCode ?? "", nationalId ?? "");
        if (err is not null)
        {
            ViewBag.Error = err;
            ViewBag.Identifier = personnelCode;
            return View("Login");
        }
        return Redirect("/Self");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Denied(string? perm)
    {
        ViewBag.Perm = perm;
        return View();
    }

    /// <summary>
    /// حذف کوکی در «هر دو واریانت» (تسک ۲۵ — رفع باگ خروج):
    /// کوکی‌های نشست با SameSite=None; Secure ذخیره می‌شوند و میان‌افزار Embedding
    /// «Partitioned» (CHIPS) را هم می‌افزاید. در پنل پیش‌نمایش iframe، کوکی در
    /// «جار پارتیشن‌شده» ذخیره می‌شود و حذف بدون همین ویژگی‌ها آن را پیدا نمی‌کند
    /// (تطبیق حذف بر اساس name+path+partition است) → کاربر پس از خروج همچنان وارد می‌ماند.
    /// راه‌حل: دو هدر Set-Cookie انقضایی — یکی با SameSite=None; Secure (+Partitioned خودکار)
    /// و یکی ساده — تا هر دو جار در همه مرورگرها و همه زمینه‌ها پاک شوند.
    /// </summary>
    private void DeleteAuthCookie(string name)
    {
        Response.Cookies.Append(name, string.Empty, new CookieOptions
        {
            Path = "/", HttpOnly = true, Secure = true, SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UnixEpoch,
        });
        Response.Cookies.Append(name, string.Empty, new CookieOptions
        {
            Path = "/", HttpOnly = true,
            Expires = DateTimeOffset.UnixEpoch,
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var raw = Request.Cookies[JwtService.RefreshCookie];
        if (!string.IsNullOrEmpty(raw))
        {
            var hash = JwtService.Sha256Hex(raw);
            var rt = await Db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (rt is not null) { rt.RevokedAt = DateTime.Now; await Db.SaveChangesAsync(); }
        }
        DeleteAuthCookie(JwtService.AccessCookie);
        DeleteAuthCookie(JwtService.RefreshCookie);
        await Db.Audit(Sess, "logout", "user", Sess?.Username, null, Ip);
        return Redirect("/Auth/Login");
    }

    // ---------- فاز ۹۶ (درخواست کاربر) — بیدارسازی محافظ صفحه با رمز ----------
    // محافظ صفحه (باران ماتریکس) در حالت «بیدارسازی با رمز»، با اولین حرکت کاربر فرم رمز باز
    // می‌کند؛ رمزِ همان کاربرِ واردشده اینجا دوباره تأیید می‌شود (PBKDF2، بدون افشای هش).
    // محدودسازی نرم: ۵ خطا در هر ۵ دقیقه برای هر کاربر = پیام صبر ۳۰ ثانیه‌ای (بدون قفل حساب).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Fails, DateTime WindowStart, DateTime LockUntil)> _wakeFails = new();

    // ---------- فاز ۱۰۲ (درخواست کاربر) — قفل سرورسمت محافظ صفحه ----------
    // رفع دورزدن رمز با رفرش: تا فاز ۱۰۱ قفل فقط سمت مرورگر بود و F5 آن را صفر می‌کرد.
    // هنگام فعال‌شدن محافظ (حالت رمزدار) JS اینجا را صدا می‌زند تا پرچم قفل به‌ازای
    // نشست مرورگر (هش hr_at) ثبت شود؛ _Layout با همان کلید صفحه را «همان اول قفل»
    // رندر می‌کند و برداشتن پرچم فقط از مسیر تأیید رمز (VerifyWakePassword) ممکن است.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ScreensaverLock()
    {
        if (User.Identity?.IsAuthenticated != true || Sess is null)
            return Json(new { ok = false, relogin = true });
        Platform.Web.Services.ScreensaverLock.Lock(Request.Cookies[JwtService.AccessCookie]);
        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyWakePassword(string password)
    {
        if (User.Identity?.IsAuthenticated != true || Sess is null)
            return Json(new { ok = false, relogin = true, message = HrT.T("نشست یافت نشد — دوباره وارد شوید") });
        var uname = Sess.Username;
        var now = DateTime.Now;
        var st = _wakeFails.GetOrAdd(uname, _ => (0, now, DateTime.MinValue));
        if (st.LockUntil > now)
        {
            var secs = (int)Math.Ceiling((st.LockUntil - now).TotalSeconds);
            return Json(new { ok = false, message = HrT.F("تلاش‌های ناموفق زیاد است — {0} ثانیه دیگر دوباره تلاش کنید", secs) });
        }
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == uname)
                   ?? await Db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == uname.ToLower());
        if (user is null)
            return Json(new { ok = false, relogin = true, message = HrT.T("کاربر یافت نشد — دوباره وارد شوید") });
        var ok = PasswordService.Verify(password ?? "", user.PasswordHash);
        await Db.Audit(Sess, ok ? "screensaver_unlock" : "screensaver_unlock_failed", "user", uname, null, Ip);
        if (ok)
        {
            _wakeFails.TryRemove(uname, out _);
            // فاز ۱۰۲ — تنها مسیر قانونی برداشتن قفل سرورسمت محافظ صفحه (رفع دورزدن با رفرش)
            Platform.Web.Services.ScreensaverLock.Unlock(Request.Cookies[JwtService.AccessCookie]);
            return Json(new { ok = true, message = HrT.T("رمز تأیید شد") });
        }
        var windowStart = (now - st.WindowStart).TotalMinutes > 5 ? now : st.WindowStart;
        var fails = windowStart == now ? 1 : st.Fails + 1;
        var lockUntil = fails >= 5 ? now.AddSeconds(30) : DateTime.MinValue;
        _wakeFails[uname] = (fails, windowStart, lockUntil);
        return Json(new { ok = false, message = HrT.T("رمز نادرست است — دوباره تلاش کنید") });
    }
}
