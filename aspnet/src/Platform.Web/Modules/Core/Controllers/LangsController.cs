using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

// ============================================================
// فاز ۷۷ — پنل ادمین زبان‌ها (/Langs)
// ----------------------------------------------------------------------
// زبان جدید بدون تغییر کد و بدون ری‌استارت، با «بستهٔ ترجمه» (خروجی
// TranslationStudio) از همین صفحه افزوده می‌شود — به‌جای کپی دستی فایل.
// قالب بسته (json — یا zip حاوی json):
//   {"format":"hr-lang-pack","version":1,"code":"de","title":"Deutsch",
//    "dir":"ltr","entries":{"متن فارسی مبدأ":"ترجمه", ...}}
// درون‌ریزی = ادغام (کلیدهای موجود به‌روز، کلیدهای جدید اضافه؛ هیچ‌چیز حذف نمی‌شود).
// «fa» مبدأ است و بسته نمی‌پذیرد؛ «en» بسته می‌پذیرد (ترجمه‌هایش اولویت دارند).
// ============================================================
public class LangsController(AppDbContext db, ICurrentUser me, HrLangStore langs)
    : MvcBase(db, me)
{
    private const string Format = "hr-lang-pack";
    private const int Version = 1;

    [HttpGet]
    [HasPerm("settings.view")]
    public async Task<IActionResult> Index()
    {
        var all = langs.All();
        var counts = await Db.HrLangEntries.AsNoTracking().GroupBy(e => e.LangCode)
            .Select(g => new { Code = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Code, x => x.Count);
        ViewBag.Counts = counts;
        ViewBag.Langs = all;
        ViewBag.CanManage = Me.Get()!.Has("settings.manage");
        return View();
    }

    /// <summary>مودال درون‌ریزی بستهٔ ترجمه (فایل .json یا .zip حاوی json).</summary>
    [HttpGet]
    [HasPerm("settings.manage")]
    public IActionResult ImportForm() => PartialView("_ImportForm");

    /// <summary>درون‌ریزی بستهٔ ترجمه — ادغام با بستهٔ موجود (اگر کد قبلاً باشد) یا ایجاد زبان جدید.</summary>
    [HttpPost]
    [HasPerm("settings.manage")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return Fail(HrT.T("فایل بستهٔ ترجمه را انتخاب کنید"));
        if (file.Length > 12 * 1024 * 1024)
            return Fail(HrT.T("حجم بستهٔ ترجمه بیش از ۱۲ مگابایت است"));

        string json;
        var name = file.FileName.ToLowerInvariant();
        try
        {
            if (name.EndsWith(".zip"))
            {
                using var archive = new System.IO.Compression.ZipArchive(file.OpenReadStream());
                var entry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                    !e.FullName.Contains("__MACOSX")) ?? archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                if (entry is null) return Fail(HrT.T("داخل zip هیچ فایل json ترجمه‌ای نبود"));
                using var reader = new StreamReader(entry.Open());
                json = await reader.ReadToEndAsync();
            }
            else
            {
                using var reader = new StreamReader(file.OpenReadStream());
                json = await reader.ReadToEndAsync();
            }
        }
        catch (Exception ex)
        {
            return Fail(HrT.T("خواندن فایل ممکن نشد — ") + ex.Message);
        }

        // تجزیهٔ بسته
        string code, title, dir;
        Dictionary<string, string> entries;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            code = (r.TryGetProperty("code", out var c) ? c.GetString() : null) ?? "";
            title = (r.TryGetProperty("title", out var t) ? t.GetString() : null) ?? "";
            dir = (r.TryGetProperty("dir", out var d) ? d.GetString() : null) ?? "ltr";
            entries = new(StringComparer.Ordinal);
            if (r.TryGetProperty("entries", out var en) && en.ValueKind == JsonValueKind.Object)
                foreach (var p in en.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String && p.Name.Trim().Length > 0)
                        entries[p.Name] = p.Value.GetString() ?? "";
        }
        catch (Exception ex)
        {
            return Fail(HrT.T("قالب بستهٔ ترجمه معتبر نیست (JSON نامعتبر) — ") + ex.Message);
        }

        code = code.Trim().ToLowerInvariant();
        if (code is "fa") return Fail(HrT.T("«fa» زبان مبدأ است و بستهٔ ترجمه نمی‌پذیرد"));
        if (code.Length < 2 || code.Length > 8 || !code.All(char.IsAsciiLetterOrDigit))
            return Fail(HrT.T("کد زبان باید ۲ تا ۸ نویسهٔ لاتین باشد (مثل de، ar، tr)"));
        if (title.Trim().Length < 2) return Fail(HrT.T("نام نمایشی زبان (title) را در بسته بگنجانید"));
        if (dir is not ("ltr" or "rtl")) dir = "ltr";

        // زبان موجود یا جدید
        var def = await Db.HrLangDefs.FirstOrDefaultAsync(l => l.Code == code);
        if (def is null)
        {
            def = new HrLangDef { Code = code, Title = title.Trim(), Dir = dir, IsActive = true };
            Db.HrLangDefs.Add(def);
        }
        else
        {
            def.Title = title.Trim();
            def.Dir = dir;
        }

        // ادغام ترجمه‌ها — کلید = رشتهٔ فارسی مبدأ
        var existing = await Db.HrLangEntries.Where(e => e.LangCode == code)
            .ToDictionaryAsync(e => e.Key, e => e, StringComparer.Ordinal);
        int updated = 0, added = 0;
        foreach (var (k, v) in entries)
        {
            if (existing.TryGetValue(k, out var row))
            {
                if (row.Value != v) { row.Value = v; updated++; }
            }
            else
            {
                Db.HrLangEntries.Add(new HrLangEntry { LangCode = code, Key = k, Value = v });
                added++;
            }
        }
        await Db.SaveChangesAsync();
        langs.Bump();   // کش ترجمهٔ زنده بی‌اعتبار می‌شود — بلافاصله در نشست‌های فعال

        var total = existing.Count + added;
        await Audit("import", "lang", code, new { title, dir, added, updated, total });
        var msg = HrT.F("بستهٔ زبان «{0}» درون‌ریزی شد — {1} ترجمهٔ جدید، {2} به‌روزرسانی (مجموع {3} کلید)",
            title.Trim(), HrT.Num(added), HrT.Num(updated), HrT.Num(total));
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Langs");
    }

    /// <summary>فعال/غیرفعال کردن نمایش زبان در سوئیچر — fa و en غیرقابل غیرفعال‌شدن نیستند؛ en بله.</summary>
    [HttpPost]
    [HasPerm("settings.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string id)
    {
        var def = await Db.HrLangDefs.FirstOrDefaultAsync(l => l.Id == id);
        if (def is null) return NotFound();
        if (def.Code == "fa") return Fail(HrT.T("زبان مبدأ (fa) همیشه فعال است"));
        def.IsActive = !def.IsActive;
        await Db.SaveChangesAsync();
        langs.Bump();
        await Audit("toggle", "lang", def.Code, new { active = def.IsActive });
        var msg = def.IsActive
            ? HrT.F("زبان «{0}» فعال شد", def.Title)
            : HrT.F("زبان «{0}» از سوئیچر پنهان شد", def.Title);
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Langs");
    }

    /// <summary>حذف زبان پویا — fa حذف نمی‌شود؛ en فقط ترجمه‌هایش پاک می‌شود (دیکشنری استاتیک می‌ماند).</summary>
    [HttpPost]
    [HasPerm("settings.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var def = await Db.HrLangDefs.FirstOrDefaultAsync(l => l.Id == id);
        if (def is null) return NotFound();
        if (def.Code == "fa") return Fail(HrT.T("زبان مبدأ (fa) قابل حذف نیست"));
        var code = def.Code;
        Db.HrLangEntries.RemoveRange(await Db.HrLangEntries.Where(e => e.LangCode == code).ToListAsync());
        Db.HrLangDefs.Remove(def);
        await Db.SaveChangesAsync();
        langs.Bump();
        await Audit("delete", "lang", code, null);
        var msg = HrT.F("زبان «{0}» و همهٔ ترجمه‌هایش حذف شد", code);
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Langs");
    }

    /// <summary>خروجی بستهٔ ترجمه — همان قالبی که TranslationStudio باز می‌کند. fa = بستهٔ مبدأ (ترجمه‌ها خالی).</summary>
    [HttpGet]
    [HasPerm("settings.view")]
    public async Task<IActionResult> Export(string code)
    {
        code = (code ?? "").Trim().ToLowerInvariant();
        var title = langs.TitleOf(code);
        if (title is null) return NotFound();
        var entries = code == "fa"
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await Db.HrLangEntries.AsNoTracking().Where(e => e.LangCode == code)
                .ToDictionaryAsync(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var pack = new { format = Format, version = Version, code, title, dir = langs.DirOf(code), entries };
        var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
        await Audit("export", "lang", code, new { count = entries.Count });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json",
            $"hr-lang-{code}.json");
    }

    private IActionResult Fail(string msg)
    {
        ToastErr(msg);
        if (Ajax) return AjaxResult(false, msg);
        return Redirect("/Langs");
    }
}
