using Platform.Web.Data;
using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

/// <summary>
/// کاتالوگ انواع و برندهای دستگاه (ATT-01) — مقادیر پیش‌فرض هر نوع برای «تعریف دستگاه».
/// در فرم تعریف، با انتخاب نوع دستگاه و برند، فیلدهای لازم (کارکرد، فاصله پایش، مدل،
/// سریال، IP آزاد و نام) به‌صورت خودکار پیش‌فرض می‌شوند تا کاربر فقط اصلاح کند.
/// </summary>
public static class DeviceDefaults
{
    /// <summary>پیش‌فرض‌های هر «نوع دستگاه».</summary>
    public sealed record KindPreset(string Purpose, int Polling, string NamePrefix, string BioHint, string Icon);

    /// <summary>پیش‌فرض‌های هر «برند» — پیشوند سریال، مدل‌های رایج، پورت پروتکل و نحوه اتصال.</summary>
    public sealed record BrandPreset(string SerialPrefix, string[] Models, int Port, string Protocol, string Transport);

    public static readonly IReadOnlyDictionary<string, KindPreset> Kinds = new Dictionary<string, KindPreset>
    {
        ["ترمینال اثر انگشت"] = new("حضور و غیاب", 60, "ترمینال اثر انگشت", "اثر انگشت + کارت + رمز", "🧬"),
        ["ترمینال تشخیص چهره"] = new("کنترل تردد", 60, "ترمینال چهره", "چهره + کارت", "🙂"),
        ["ترمینال ترکیبی چهره و اثر انگشت"] = new("حضور و غیاب", 45, "ترمینال چندبیومتریک", "اثر انگشت + چهره + کارت", "🧬"),
        ["کنترلر دسترسی"] = new("کنترل تردد", 30, "کنترلر دسترسی", "اثر انگشت/چهره + کارت + کنترل درب", "🚪"),
        ["ترمینال کارتی"] = new("حضور و غیاب", 60, "ترمینال کارتی", "فقط کارت (بدون بایومتریک)", "💳"),
    };

    public static readonly IReadOnlyDictionary<string, BrandPreset> Brands = new Dictionary<string, BrandPreset>
    {
        ["ZKTeco"]  = new("ZK", new[] { "U2000", "F18", "K40", "MB360", "FD7", "SF300" }, 4370, "Push / ADMS", "adms"),
        ["Suprema"] = new("SP", new[] { "BS3", "FS40", "X-Station2" }, 51211, "TCP", "tcp"),
        ["Anviz"]   = new("AV", new[] { "W1", "W2", "C2", "T5Pro" }, 5010, "TCP", "tcp"),
        ["Virdi"]   = new("VR", new[] { "AC7", "AC5000", "AC2100" }, 5150, "TCP", "tcp"),
        ["Timmy"]   = new("TM", new[] { "A5", "T5", "B7" }, 4370, "TCP", "tcp"),
        ["سایر"]    = new("GEN", new[] { "GEN1" }, 4370, "TCP", "tcp"),
    };

    public static string[] KindKeys => Kinds.Keys.ToArray();
    public static string[] BrandKeys => Brands.Keys.ToArray();

    public static bool ValidKind(string? k) => !string.IsNullOrWhiteSpace(k) && Kinds.ContainsKey(k);
    public static bool ValidBrand(string? b) => !string.IsNullOrWhiteSpace(b) && Brands.ContainsKey(b);

    /// <summary>نام پیشنهادی — «ترمینال چهره ZKTeco FD7 — گیت شرقی».</summary>
    public static string SuggestName(string kind, string brand, string model, string? location)
    {
        var loc = string.IsNullOrWhiteSpace(location) ? "" : $" — {location.Trim()}";
        return $"{Kinds[kind].NamePrefix} {brand} {model}{loc}";
    }

    /// <summary>سریال پیشنهادی — الگوی برند + مدل + ۴ رقم (مثل ZK-U2000-0142).</summary>
    public static string SuggestSerial(string brand, string model) =>
        $"{Brands[brand].SerialPrefix}-{model}-{Random.Shared.Next(1000, 10000)}";

    /// <summary>اولین IP آزاد در زیرشبکه 192.168.10.x بر اساس دستگاه‌های موجود.</summary>
    public static async Task<string?> NextFreeIpAsync(AppDbContext db)
    {
        var ips = await db.Set<Device>().Where(d => d.Ip != null).Select(d => d.Ip!).ToListAsync();
        var used = new HashSet<int>();
        foreach (var ip in ips)
        {
            var parts = ip.Split('.');
            if (parts.Length == 4 && parts[0] == "192" && parts[1] == "168" && parts[2] == "10"
                && int.TryParse(parts[3], out var last))
                used.Add(last);
        }
        for (var x = 11; x <= 250; x++)
            if (!used.Contains(x))
                return $"192.168.10.{x}";
        return null;
    }
}
