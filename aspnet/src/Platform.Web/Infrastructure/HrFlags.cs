using Microsoft.AspNetCore.Html;

namespace Platform.Web.Infrastructure;

/// <summary>
/// فاز ۹۵ — پرچم «واقعی» زبان‌ها (درخواست کاربر: «از پرچم‌های واقعی استفاده کن، خودت تولید نکن»).
/// تا فاز ۹۳ پرچم‌ها SVG دست‌سازِ درون‌خطی بودند (ایران ساده‌شده و یونیون‌جک فشرده) — حالا
/// فایل SVG رسمی هر پرچم (مأخذ: flagcdn.com — بازتاب مستقیم استاندارد ISO 3166) در
/// wwwroot/img/flags/*.svg نگهداری می‌شود و سوئیچرها همان را با &lt;img&gt; نشان می‌دهند؛
/// نسبت ابعاد از خود فایل می‌آید (بدون کشیدگی) و در همهٔ مرورگر/سیستم‌عامل یکسان رندر می‌شود.
/// نگاشت کد زبان ← کشور پرچم:
///   fa=ایران(ir) · en=بریتانیا(gb) · ar=عربستان(sa) · ky=قرقیزستان(kg) ·
///   ps/prs=افغانستان(af) — هر دو زبان رسمی افغانستان · ka=گرجستان(ge) ·
///   tk=ترکمنستان(tm) · tg=تاجیکستان(tj) · uz=ازبکستان(uz) · ru=روسیه(ru) — فاز ۹۹ (بازسازی).
/// کد ناشناخته (زبان پویای آینده بدون نگاشت) = آیکن کرهٔ زمین — مثل قبل.
/// </summary>
public static class HrFlags
{
    /// <summary>کد زبان ← نام فایل پرچم در wwwroot/img/flags (بدون پسوند).</summary>
    public static readonly Dictionary<string, string> Files = new(StringComparer.Ordinal)
    {
        ["fa"] = "ir",
        ["en"] = "gb",
        ["ar"] = "sa",
        ["ky"] = "kg",
        ["ps"] = "af",
        ["prs"] = "af",
        ["ka"] = "ge",
        ["tk"] = "tm",
        ["tg"] = "tj",
        ["uz"] = "uz",
        ["ru"] = "ru",
    };

    /// <summary>SVG پرچم زبان — height بر حسب پیکسل؛ عرض خودکار با نسبت واقعی پرچم.</summary>
    public static IHtmlContent Svg(string? code, int height = 14)
    {
        code = (code ?? "").Trim().ToLowerInvariant();
        if (Files.TryGetValue(code, out var file))
        {
            return new HtmlString(
                $"<img class='hr-flag' src='/img/flags/{file}.svg' alt='' height='{height}' width='auto' " +
                "style='border-radius:2px;box-shadow:0 0 0 1px rgba(15,23,42,.14);vertical-align:-2px;flex:none;display:inline-block;object-fit:contain' " +
                "aria-hidden='true' focusable='false' loading='lazy' />");
        }

        // ناشناخته — کرهٔ زمین (بدون شناسهٔ تکرارپذیر تا چندبار فراخوانی در یک صفحه تداخلی نسازد)
        var inner = "<circle cx='9' cy='6' r='4.9' fill='#eff6ff' stroke='#64748b' stroke-width='1.2'/>" +
                    "<path d='M4.1 6h9.8M9 1.1c-1.9 1.7-1.9 8.1 0 9.8M9 1.1c1.9 1.7 1.9 8.1 0 9.8' fill='none' stroke='#64748b' stroke-width='.9'/>";
        var w = Math.Round(height * 18.0 / 12);
        return new HtmlString(
            $"<svg class='hr-flag' viewBox='0 0 18 12' width='{w}' height='{height}' " +
            "style='border-radius:2px;box-shadow:0 0 0 1px rgba(15,23,42,.14);vertical-align:-2px;flex:none' " +
            $"aria-hidden='true' focusable='false'>{inner}</svg>");
    }
}
