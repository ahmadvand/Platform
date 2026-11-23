using Platform.Web.Data;
using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

/// <summary>
/// سرویس تنظیمات برندینگ/ظاهر (فاز ۳۱) — بارگیری ردیف تک‌تنظیمات، اعتبارسنجی رنگ‌ها و
/// تولید بلوک override متغیرهای CSS. با هر تغییر از پنل ادمین، لایه درخواست بعدی
/// خودکار پالت جدید را روی کل نرم‌افزار اعمال می‌کند (بدون کش — پرس‌وجوی تک‌ردیفی ارزان است).
/// </summary>
public class SettingsService(HrBaseDbContext db)
{
    /// <summary>پالت‌های آماده قدیمی — دکمه‌های انتخاب سریع دو رنگ (لایه سازگاری — فاز ۳۱).</summary>
    public static readonly (string Name, string Accent, string Sidebar)[] PalettePresets =
    {
        ("نیلی درخشان (پیش‌فرض)",       "#6366f1", "#1b1746"),
        ("فیروزه‌ای سازمانی",          "#0d9488", "#0d242c"),
        ("آبی دریایی",                 "#1d4ed8", "#0b1e3f"),
        ("سبز جنگل",                   "#16a34a", "#0c2216"),
        ("بنفش سلطنتی",                "#7c3aed", "#1e1338"),
        ("شفق نارنجی",                 "#ea580c", "#2a1508"),
        ("سرخابی",                     "#db2777", "#2b0d1c"),
        ("گرافیت",                     "#475569", "#111827"),
    };

    /// <summary>بارگیری تنظیمات با محافظت — اگر بانک در دسترس نباشد مقادیر پیش‌فرض برمی‌گردد
    /// تا layout هرگز به‌خاطر تنظیمات بشکند.</summary>
    public async Task<AppSettings> GetSafeAsync()
    {
        try
        {
            var s = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == "default");
            if (s is not null) return s;
        }
        catch { /* بانک هنوز آماده نیست */ }
        return new AppSettings { Id = "default" };
    }

    /// <summary>دریافت ردیف قابل‌ویرایش (برای کنترلر) — در صورت نبود می‌سازد.</summary>
    public async Task<AppSettings> GetEditableAsync()
    {
        var s = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == "default");
        if (s is null)
        {
            // فاز ۱۱۰ — ردیف نو از پیش علامت خورده: هرگز وارد ترمیم یک‌بارهٔ جبران قمری نمی‌شود
            s = new AppSettings { Id = "default", HijriAdjustMigrated = true };
            db.AppSettings.Add(s);
            await db.SaveChangesAsync();
        }
        return s;
    }

    /// <summary>عکس پروفایل کاربر جاری برای نوار بالایی (فاز ۴۵) — خطا = بدون عکس.</summary>
    public async Task<string?> GetUserPhotoAsync(string userId)
    {
        try
        {
            return await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.PhotoPath)
                .FirstOrDefaultAsync();
        }
        catch { return null; }
    }

    /// <summary>داده‌های زنگ اعلان نوار بالایی (فاز ۴۵) — هشدارهای باز + آخرین‌ها. خطا = خالی.
    /// فاز ۹۰ — هر آیتم شناسهٔ هشدار و «نشانی صفحهٔ مربوطه» هم دارد تا در زنگ قابل کلیک باشد:
    /// کلیک → رفتن به صفحهٔ مربوطه + علامت‌گذاری همان هشدار به‌عنوان خوانده‌شده (Resolved) تا
    /// دیگر در فهرست هشدارهای باز نمایش داده نشود.</summary>
    public async Task<(int Count, List<(string Id, string Severity, string Type, string Message, string Time, string Url)> Items)> GetBellAsync()
    {
        try
        {
            var open = db.SecurityAlerts.Where(a => !a.Resolved && !a.Expired);
            var count = await open.CountAsync();
            var items = await open.OrderByDescending(a => a.CreatedAt).Take(6)
                .Select(a => new { a.Id, a.Severity, a.Type, a.Message, a.Source, a.CreatedAt })
                .ToListAsync();
            return (count, items.Select(i => (i.Id, i.Severity, i.Type, i.Message, Jalali.FmtTime(i.CreatedAt), AlertUrl(i.Type, i.Source))).ToList());
        }
        catch { return (0, new()); }
    }

    /// <summary>فاز ۹۰ — نگاشت هشدار به صفحهٔ مربوطه (برای کلیک‌پذیر بودن آیتم‌های زنگ).
    /// بر اساس منبع/نوع هشدار به ماژول صاحب آن هدایت می‌شود؛ ناشناخته = صفحهٔ اعلان‌ها.</summary>
    public static string AlertUrl(string type, string source)
    {
        if (source == "کنترل پارکینگ") return "/Parking";
        if (type == "قطع دستگاه" || source == "دستگاه‌های تردد") return "/Devices";
        if (type == "کارت گمشده") return "/Cards";
        if (source == "ارباب رجوع") return "/Visitors";
        if (source == "حضور و غیاب") return "/Attendance";
        return "/Access?tab=alerts";
    }

    // ===================== فاز ۳۲ — پالت رنگ استاندارد چندنقشی =====================

    /// <summary>پالت فعال را برمی‌گرداند — یا null اگر ادمین پالتی را اعمال نکرده باشد.</summary>
    public async Task<ColorPalette?> GetActivePaletteAsync()
    {
        var s = await GetSafeAsync();
        if (s.ActivePaletteId is null) return null;
        try
        {
            return await db.ColorPalettes.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == s.ActivePaletteId);
        }
        catch { return null; }
    }

    /// <summary>
    /// بلوک استایل پالت استاندارد — نقش‌های ۱۶گانه ادمین + نقش‌های مشتق‌شده که همگی
    /// «یک رنگ ثابت تخت» هستند (بدون هیچ گرادیان/طیف). هر نقش متغیر CSS سراسری را
    /// بازنویسی می‌کند: لینک‌ها، دکمه‌ها، سربرگ/سطر جدول، منو، متن، پس‌زمینه، وضعیت‌ها…
    /// </summary>
    public static string BuildPaletteCss(ColorPalette p)
    {
        if (p is null) return "";
        var pri = NormalizeHex(p.PrimaryColor) ?? "#6366f1";
        var link = NormalizeHex(p.LinkColor) ?? pri;
        var sb = NormalizeHex(p.SidebarColor) ?? "#1b1746";
        var sbt = NormalizeHex(p.SidebarTextColor) ?? "#c3c6ec";
        var page = NormalizeHex(p.PageBgColor) ?? "#f4f5fb";
        var surf = NormalizeHex(p.SurfaceColor) ?? "#ffffff";
        var th = NormalizeHex(p.TableHeaderColor) ?? "#f2f3fc";
        var rowAlt = NormalizeHex(p.RowAltColor) ?? "#f9f9fe";
        var rowHov = NormalizeHex(p.RowHoverColor) ?? "#f4f5fe";
        var border = NormalizeHex(p.BorderColor) ?? "#e3e5f3";
        var ink = NormalizeHex(p.TextColor) ?? "#1d1e3a";
        var muted = NormalizeHex(p.MutedTextColor) ?? "#565b7e";
        var ok = NormalizeHex(p.SuccessColor) ?? "#059669";
        var warn = NormalizeHex(p.WarningColor) ?? "#d97706";
        var err = NormalizeHex(p.DangerColor) ?? "#e11d48";
        var info = NormalizeHex(p.InfoColor) ?? "#0284c7";

        var deep = Mix(pri, "#000000", 0.14);
        var vars = new List<string>(44)
        {
            // برند و کنش
            $"--accent:{pri}",
            $"--accent-2:{Mix(pri, "#ffffff", 0.16)}",
            $"--accent-deep:{deep}",
            $"--accent-ink:{deep}",
            $"--accent-soft:{Mix(pri, "#ffffff", 0.90)}",
            $"--accent-border:{Mix(pri, "#ffffff", 0.72)}",
            $"--accent-3:{Mix(pri, "#ffffff", 0.30)}",
            $"--accent-4:{Mix(pri, "#000000", 0.22)}",
            $"--accent-soft-text:{Mix(pri, "#ffffff", 0.78)}",
            $"--accent-rgb:{RgbTriplet(pri)}",
            // گرادیان‌های برند (فاز ۳۳) — از نقش‌های پالت مشتق می‌شوند
            $"--grad-brand:linear-gradient(135deg, {Mix(pri, "#ffffff", 0.16)} 0%, {pri} 45%, {Mix(pri, "#000000", 0.22)} 100%)",
            $"--grad-btn:linear-gradient(135deg, {pri} 0%, {deep} 100%)",
            // لینک‌ها
            $"--link:{link}",
            // منوی کناری
            $"--sidebar:{sb}",
            $"--sidebar-2:{Mix(sb, "#ffffff", 0.10)}",
            $"--sidebar-rgb:{RgbTriplet(sb)}",
            $"--sidebar-line:{RgbTriplet(Mix(sb, "#ffffff", 0.45))}",
            $"--sidebar-text:{sbt}",
            $"--sidebar-muted:{Mix(sbt, sb, 0.40)}",
            $"--sidebar-title:{Mix(sbt, "#ffffff", 0.75)}",
            // زمینه
            $"--bg:{page}",
            $"--card:{surf}",
            $"--card-rgb:{RgbTriplet(surf)}",
            $"--line:{border}",
            $"--line-soft:{Mix(border, "#ffffff", 0.45)}",
            // متن
            $"--ink:{ink}",
            $"--ink-rgb:{RgbTriplet(ink)}",
            $"--ink-2:{muted}",
            $"--ink-3:{Mix(muted, "#ffffff", 0.35)}",
            $"--ink-soft:{Mix(ink, pri, 0.28)}",
            // جدول — سربرگ/سطر یک‌درمیان/سطر هاور
            $"--th-bg:{th}",
            $"--row-alt:{rowAlt}",
            $"--row-hover:{rowHov}",
            // دکمه‌های خنثی
            $"--btn-hover-bg:{Mix(pri, "#ffffff", 0.94)}",
            $"--btn-hover-border:{Mix(pri, "#ffffff", 0.78)}",
            // وضعیت‌ها (پس‌زمینه/خط هر وضعیت هم رنگ تخت مشتق‌شده است)
            $"--ok:{ok}", $"--ok-bg:{Mix(ok, "#ffffff", 0.88)}", $"--ok-border:{Mix(ok, "#ffffff", 0.70)}",
            $"--warn:{warn}", $"--warn-bg:{Mix(warn, "#ffffff", 0.88)}", $"--warn-border:{Mix(warn, "#ffffff", 0.70)}",
            $"--err:{err}", $"--err-bg:{Mix(err, "#ffffff", 0.88)}", $"--err-border:{Mix(err, "#ffffff", 0.70)}",
            $"--info:{info}", $"--info-bg:{Mix(info, "#ffffff", 0.88)}", $"--info-border:{Mix(info, "#ffffff", 0.70)}",
            // اسکرول‌بار و اوورلی — از نقش‌های پالت
            $"--scroll-thumb:{Mix(border, muted, 0.45)}",
            $"--scroll-thumb-hover:{Mix(border, muted, 0.30)}",
            $"--overlay:rgba({RgbTriplet(sb)}, .46)",
        };
        return ":root{" + string.Join(";", vars) + ";}";
    }

    /// <summary>رنگ PDFها را هم‌راستا با پالت فعال/لایه قدیمی می‌کند (فاز ۳۲).</summary>
    public async Task ApplyPdfThemeAsync()
    {
        try
        {
            var s = await GetSafeAsync();
            string? accent = null;
            if (s.ActivePaletteId is not null)
                accent = (await db.ColorPalettes.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == s.ActivePaletteId))?.PrimaryColor;
            PdfReport.ApplyPalette(accent ?? s.AccentColor);
        }
        catch { /* بانک هنوز آماده نیست — رنگ پیش‌فرض PDF */ }
    }

    /// <summary>اعتبارسنجی و نرمال‌سازی رنگ #rrggbb — نامعتبر/خالی → null (پیش‌فرض).</summary>
    public static string? NormalizeHex(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var h = input.Trim().TrimStart('#');
        if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
        if (h.Length != 6 || !h.All(Uri.IsHexDigit)) return null;
        return "#" + h.ToLowerInvariant();
    }

    /// <summary>بلوک استایل override — فقط وقتی حداقل یک رنگ تعیین شده باشد خروجی می‌دهد.
    /// پس از لینک site.css تزریق می‌شود و کل نرم‌افزار (دکمه‌ها، منو، لینک‌ها، هشدارها، گرادیان‌ها)
    /// از پالت ادمین پیروی می‌کند.</summary>
    public static string BuildCssOverrides(AppSettings? s)
    {
        if (s is null) return "";
        var accent = NormalizeHex(s.AccentColor);
        var sidebar = NormalizeHex(s.SidebarColor);
        if (accent is null && sidebar is null) return "";

        var vars = new List<string>();
        if (accent is not null)
        {
            var rgb = RgbTriplet(accent);
            var accent2 = Mix(accent, "#ffffff", 0.16);
            var deep = Mix(accent, "#000000", 0.14);
            vars.Add($"--accent:{accent}");
            vars.Add($"--accent-2:{accent2}");
            vars.Add($"--accent-deep:{deep}");
            vars.Add($"--accent-ink:{deep}");
            vars.Add($"--accent-soft:{Mix(accent, "#ffffff", 0.90)}");
            vars.Add($"--accent-border:{Mix(accent, "#ffffff", 0.72)}");
            vars.Add($"--accent-rgb:{rgb}");
        }
        if (sidebar is not null)
        {
            vars.Add($"--sidebar:{sidebar}");
            vars.Add($"--sidebar-2:{Mix(sidebar, "#ffffff", 0.10)}");
            vars.Add($"--sidebar-line:{RgbTriplet(Mix(sidebar, "#ffffff", 0.45))}");
        }
        return ":root{" + string.Join(";", vars) + ";}";
    }

    // ---------- ریاضی رنگ ----------

    private static (int R, int G, int B) ParseHex(string hex)
    {
        var h = hex.TrimStart('#');
        return (Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..6], 16));
    }

    private static string ToHex(int r, int g, int b) =>
        $"#{Math.Clamp(r, 0, 255):x2}{Math.Clamp(g, 0, 255):x2}{Math.Clamp(b, 0, 255):x2}";

    /// <summary>ترکیب خطی دو رنگ — t=0 رنگ اول، t=1 رنگ دوم</summary>
    public static string Mix(string hex1, string hex2, double t)
    {
        var (r1, g1, b1) = ParseHex(hex1);
        var (r2, g2, b2) = ParseHex(hex2);
        return ToHex(
            (int)Math.Round(r1 + (r2 - r1) * t),
            (int)Math.Round(g1 + (g2 - g1) * t),
            (int)Math.Round(b1 + (b2 - b1) * t));
    }

    /// <summary>"r,g,b" برای استفاده در rgba(var(--x), .2)</summary>
    public static string RgbTriplet(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return $"{r},{g},{b}";
    }

    /// <summary>نسخه روشن‌تر/تیره‌تر بر اساس فضای HSL (برای حالت‌های hover/active)</summary>
    public static string AdjustLightness(string hex, double factor)
    {
        var (r, g, b) = ParseHex(hex);
        double rn = r / 255.0, gn = g / 255.0, bn = b / 255.0;
        var max = Math.Max(rn, Math.Max(gn, bn));
        var min = Math.Min(rn, Math.Min(gn, bn));
        var l = (max + min) / 2;
        var d = max - min;
        double h = 0, s = 0;
        if (d > 0.00001)
        {
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == rn) h = ((gn - bn) / d + (gn < bn ? 6 : 0));
            else if (max == gn) h = (bn - rn) / d + 2;
            else h = (rn - gn) / d + 4;
            h /= 6;
        }
        var l2 = Math.Clamp(l * factor, 0, 1);
        // HSL→RGB
        if (d < 0.00001) return ToHex(r, g, b);
        double q = l2 < 0.5 ? l2 * (1 + s) : l2 + s - l2 * s;
        double p = 2 * l2 - q;
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1 / 2.0) return q;
            if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6;
            return p;
        }
        return ToHex(
            (int)Math.Round(Hue(p, q, h + 1 / 3.0) * 255),
            (int)Math.Round(Hue(p, q, h) * 255),
            (int)Math.Round(Hue(p, q, h - 1 / 3.0) * 255));
    }
}
