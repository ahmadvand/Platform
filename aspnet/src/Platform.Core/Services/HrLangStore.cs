using System.Collections.Concurrent;
using Platform.Web.Data;
using Platform.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

// ============================================================
// فاز ۷۷ — مخزن زبان‌های پویا (پنل ادمین /Langs + بستهٔ ترجمهٔ TranslationStudio)
// ----------------------------------------------------------------------
// تا فاز ۷۶ زبان‌ها ثابت بودند (fa مبدأ + en در HrLang.cs). حالا زبان جدید
// بدون تغییر کد و بدون ری‌استارت، با «بستهٔ ترجمه» از پنل ادمین افزوده می‌شود.
// ترتیب حل ترجمه در HrT.T:
//   ۱) بستهٔ پویای زبان جاری (DB — همین سرویس، کش حافظه‌ای)
//   ۲) دیکشنری استاتیک en (سازگاری کامل فاز ۷۶)
//   ۳) خودِ رشتهٔ فارسی (fallback امن — پوشش تدریجی، صفر ریسک)
// همهٔ متدها استثنا-امن‌اند: DB در دسترس نبود → فقط fa/en (سامانه سالم می‌ماند).
// ============================================================
public sealed class HrLangStore(IServiceScopeFactory scopes)
{
    private readonly object _lock = new();
    private long _ver = 1;
    private List<HrLangRow> _langs = new();
    private ConcurrentDictionary<string, Dictionary<string, string>> _packs = new();

    public sealed record HrLangRow(string Id, string Code, string Title, string Dir, bool IsActive, bool IsDefault);

    /// <summary>بی‌اعتبارسازی کش — پس از درون‌ریزی/حذف/تغییر زبان صدا زده شود.</summary>
    public void Bump()
    {
        lock (_lock) { _ver++; _packs = new(); _langs = new(); }
    }

    private (long Ver, List<HrLangRow> Langs) LoadLangs()
    {
        lock (_lock)
        {
            if (_langs.Count > 0) return (_ver, _langs);
        }
        List<HrLangRow> rows;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HrBaseDbContext>();
            rows = db.HrLangDefs.AsNoTracking()
                .OrderBy(l => l.IsDefault ? 0 : 1).ThenBy(l => l.Code)
                .Select(l => new HrLangRow(l.Id, l.Code, l.Title, l.Dir, l.IsActive, l.IsDefault))
                .ToList();
        }
        catch { rows = new List<HrLangRow>(); }
        lock (_lock)
        {
            if (_langs.Count == 0 && rows.Count > 0) { _langs = rows; return (_ver, rows); }
            return (_ver, _langs.Count > 0 ? _langs : rows);
        }
    }

    /// <summary>همهٔ زبان‌های تعریف‌شده (حتی غیرفعال) — صفحهٔ مدیریت /Langs.</summary>
    public List<HrLangRow> All()
    {
        var (ver, langs) = LoadLangs();
        // fa و en همیشه در فهرست‌اند (fa مبدأ؛ en دیکشنری استاتیک فاز ۷۶) — ردیف DB خودشان اولویت دارد
        var list = new List<HrLangRow>();
        var faRow = langs.FirstOrDefault(l => l.Code == "fa");
        list.Add(faRow ?? new HrLangRow("", "fa", "فارسی", "rtl", true, true));
        var enRow = langs.FirstOrDefault(l => l.Code == "en");
        list.Add(enRow ?? new HrLangRow("", "en", "English", "ltr", true, false));
        foreach (var l in langs)
            if (l.Code is not ("fa" or "en"))
                list.Add(l);
        return list;
    }

    /// <summary>زبان‌های فعال برای سوئیچر رابط کاربری — fa همیشه اول.</summary>
    public List<HrLangRow> Active()
    {
        var (ver, langs) = LoadLangs();
        var list = new List<HrLangRow>();
        var faRow = langs.FirstOrDefault(l => l.Code == "fa");
        list.Add(faRow ?? new HrLangRow("", "fa", "فارسی", "rtl", true, true));
        var enRow = langs.FirstOrDefault(l => l.Code == "en");
        if (enRow is null || enRow.IsActive) list.Add(enRow ?? new HrLangRow("", "en", "English", "ltr", true, false));
        foreach (var l in langs)
            if (l.Code is not ("fa" or "en") && l.IsActive)
                list.Add(l);
        return list;
    }

    /// <summary>آیا این کد زبان برای «زبان جاری» پذیرفته می‌شود؟ (fa همیشه؛ en استاتیک؛ بقیه از DB)</summary>
    public bool Supported(string? code)
    {
        var c = (code ?? "").Trim().ToLowerInvariant();
        if (c is "fa" or "en") return true;
        return Active().Any(l => l.Code == c);
    }

    /// <summary>جهت زبان — برای <html dir>.</summary>
    public string DirOf(string code)
    {
        if (code == "fa") return "rtl";
        if (code == "en") return "ltr";
        return All().FirstOrDefault(l => l.Code == code)?.Dir is { } d && d == "rtl" ? "rtl" : "ltr";
    }

    /// <summary>عنوان نمایشی زبان (برای سوئیچر) — ندارد → null.</summary>
    public string? TitleOf(string code) =>
        code == "fa" ? "فارسی" : code == "en" ? "English" : All().FirstOrDefault(l => l.Code == code)?.Title;

    /// <summary>ترجمهٔ رشتهٔ فارسی در بستهٔ پویای زبان — ندارد → null (لایهٔ بعدی: دیکشنری استاتیک).</summary>
    public string? Lookup(string code, string fa)
    {
        if (code == "fa" || fa.Length == 0) return null;
        var pack = _packs.GetOrAdd(code, _ => LoadPack(code));
        return pack.TryGetValue(fa, out var v) && v.Length > 0 ? v : null;
    }

    private Dictionary<string, string> LoadPack(string code)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HrBaseDbContext>();
            // فاز ۹۵ — ToDictionary روی کلید تکراری می‌شکست و کل بسته را خالی می‌کرد
            // (علت ریشه‌ای «منوها فارسی می‌مانند» برای بسته‌های پویا). حالا تکرار = آخری می‌برد.
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in db.HrLangEntries.AsNoTracking()
                         .Where(e => e.LangCode == code && e.Value != "")
                         .Select(e => new { e.Key, e.Value }).ToList())
                dict[e.Key] = e.Value;
            return dict;
        }
        catch { return new Dictionary<string, string>(StringComparer.Ordinal); }
    }
}
