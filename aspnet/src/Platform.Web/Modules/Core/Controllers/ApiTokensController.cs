using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۱۳۵ (مورد ۱۹ ROADMAP-ADDITIONS) — مدیریت توکن‌های API عمومی.
/// توکن فقط در لحظهٔ ساخت نمایش داده می‌شود؛ در پایگاه‌داده فقط هش آن می‌نشیند.
/// </summary>
public class ApiTokensController : MvcBase
{
    public ApiTokensController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    [HttpGet]
    [HasPerm("settings.view")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Tokens = await Db.ApiTokens.AsNoTracking()
            .OrderBy(t => t.Revoked).ThenByDescending(t => t.CreatedAt).ToListAsync();
        ViewBag.Companies = await Db.Companies.AsNoTracking().OrderBy(c => c.Code).ToListAsync();
        ViewBag.NewToken = TempData["NewToken"] as string;
        return View();
    }

    [HttpPost]
    [HasPerm("settings.view")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? scopeCompanyId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ToastErr(HrT.T("نام مصرف‌کننده الزامی است"));
            return Redirect("/ApiTokens");
        }
        // توکن خام: hrk_ + ۴۸ نویسهٔ تصادفی — فقط هش ذخیره می‌شود
        var raw = "hrk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var t = new ApiToken
        {
            Name = name.Trim(),
            TokenHash = Sha256(raw),
            Prefix = raw[..12],
            ScopeCompanyId = string.IsNullOrWhiteSpace(scopeCompanyId) ? null : scopeCompanyId,
            CreatedBy = Sess?.FullName ?? "",
        };
        Db.ApiTokens.Add(t);
        await Db.SaveChangesAsync();
        await Audit("create", "api-token", t.Id, new { t.Name, t.Prefix, t.ScopeCompanyId });
        ToastOk(HrT.T("توکن ساخته شد — همین حالا کپی کنید؛ دوباره نمایش داده نمی‌شود"));
        TempData["NewToken"] = raw;
        return Redirect("/ApiTokens");
    }

    [HttpPost]
    [HasPerm("settings.view")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(string id)
    {
        var t = await Db.ApiTokens.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.Revoked = true;
        await Db.SaveChangesAsync();
        await Audit("update", "api-token", t.Id, new { revoked = true });
        ToastOk(HrT.F("توکن «{0}» باطل شد", t.Name));
        return Redirect("/ApiTokens");
    }

    public static string Sha256(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
