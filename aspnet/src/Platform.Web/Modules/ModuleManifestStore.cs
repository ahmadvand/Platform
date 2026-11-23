using System.Text.Json;

namespace Platform.Web.Modules;

// ======================================================================
// فاز ۶۹ — حافظهٔ مانیفست ماژول‌ها (داده، نه کد)
// ----------------------------------------------------------------------
// وقتی DLL یک ماژول از نصبِ موجود حذف می‌شود، مانیفستش (IHrUiModule) هم
// دیگر کشف نمی‌شود؛ گیت مسیرها بدون هیچ نشانی از «این مسیر متعلق به کدام
// ماژول بود» فقط ۴۰۴ می‌دهد. این حافظهٔ سبک، مانیفست آخرین بوت سالم را در
// App_Data/modules.manifest.json نگه می‌دارد تا:
//   • مسیرِ ماژول حذف‌شده → صفحهٔ دوستانهٔ «نصب نشده» (نه ۴۰۴ خام)
//   • منبع شناخت = «دادهٔ پیکربندی» — هیچ نام ماژولی در کد هاست نیست؛
//     نصب تازه‌ای که هرگز آن ماژول را نداشته، هیچ اثری از آن ندارد (۴۰۴ طبیعی).
// شکست‌ها بی‌صدا نادیده گرفته می‌شوند — نبود فایل/دسترسی نباید بوت را ببندد.
// ======================================================================
public static class ModuleManifestStore
{
    private sealed record Entry(
        string Code, string Title, string Icon, string Description, string[] Controllers);

    private static readonly object Lock = new();
    private static List<Entry>? _cache;

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "App_Data", "modules.manifest.json");

    /// <summary>ذخیرهٔ مانیفست همهٔ ماژول‌های حاضر در آخرین بوت — از ModuleCatalog.BuildAll.
    /// تغییرنیافته = بازنویسی نمی‌شود (اتمیک tmp+move؛ هرگز استثناء بالا نمی‌دهد).</summary>
    public static void Save(IReadOnlyList<HrModule> mods)
    {
        try
        {
            var entries = mods
                .Select(m => new Entry(m.Code, m.Title, m.Icon, m.Description, m.Controllers))
                .OrderBy(e => e.Code, StringComparer.Ordinal)
                .ToList();
            lock (Lock)
            {
                var path = FilePath;
                if (_cache is not null && Same(_cache, entries)) return;
                if (File.Exists(path) && Same(LoadFile(path), entries)) { _cache = LoadFile(path); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, path, overwrite: true);
                _cache = entries;
            }
        }
        catch { /* نبود دسترسی/دیسک — حافظه اختیاری است */ }
    }

    /// <summary>ماژول با کد داده‌شده از «آخرین مانیفست شناخته‌شده» (برای گیتِ ماژول حذف‌شده).</summary>
    public static HrModule? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        try
        {
            lock (Lock)
            {
                _cache ??= LoadFile(FilePath);
                var e = _cache.FirstOrDefault(x => x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
                return e is null ? null : ToModule(e);
            }
        }
        catch { return null; }
    }

    /// <summary>فاز ۶۹ — ماژولی که این نام کنترلر (سگمنت اول مسیر بدون endpoint) به آن تعلق داشت —
    /// از آخرین مانیفست شناخته‌شده؛ فقط وقتی آن ماژول الان «نصب» نیست معنا دارد.</summary>
    public static HrModule? FindByController(string controllerLower)
    {
        if (string.IsNullOrWhiteSpace(controllerLower)) return null;
        try
        {
            lock (Lock)
            {
                _cache ??= LoadFile(FilePath);
                var e = _cache.FirstOrDefault(x => x.Controllers.Contains(controllerLower, StringComparer.Ordinal));
                return e is null ? null : ToModule(e);
            }
        }
        catch { return null; }
    }

    private static HrModule ToModule(Entry e) => new()
    {
        Code = e.Code, Title = e.Title, Icon = e.Icon,
        Description = e.Description, Controllers = e.Controllers,
    };

    private static List<Entry> LoadFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch { return []; }
    }

    private static bool Same(List<Entry> a, List<Entry> b) =>
        a.Count == b.Count && !a.Zip(b).Any(p =>
            !string.Equals(p.First.Code, p.Second.Code, StringComparison.Ordinal)
            || p.First.Title != p.Second.Title || p.First.Icon != p.Second.Icon
            || p.First.Description != p.Second.Description
            || !p.First.Controllers.SequenceEqual(p.Second.Controllers, StringComparer.Ordinal));
}
