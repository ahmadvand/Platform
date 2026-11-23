using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Platform.Web.Services;

/// <summary>
/// نمایش قطعی پلاک ایرانی (فاز ۴۵) — و از فاز ۵۱: پارسر «کانونیال» ساختار پلاک.
/// ----------------------------------------------------------------------
/// متن خام پلاک بسته به منبع (دستگاه پلاک‌خوان، ورود دستی، سیدر) قالب‌های متفاوتی دارد:
///   «36 ن 436 ایران 11»   — قالب استاندارد انسانی
///   «36ن436ایران11»       — خوانش فشردهٔ پلاک‌خوان
///   «36 436 ایران ن 11»   — خوانش دستگاه‌هایی که حرف پلاک را کنار کد استان می‌فرستند
///   «ایران 11 36 ن 436»   — بلوک استان در ابتدا
/// پارسر کانونیال از هر قالب، اجزای استاندارد پلاک ایرانی را بازسازی می‌کند:
///   [دو‌رقم سمت راست] [حرف] [سه‌رقم سمت چپ] + [ایران | کد استان]
/// تا نمایش همیشه مثل پلاک فلزی باشد و دیگر حرفِ وسط به اشتباه داخل کادر
/// «ایران» نمایش داده نشود (باگ «ایران ن11» گزارش کاربر — فاز ۵۱).
/// هر توکن در span جداگانهٔ ایزوله است و هیچ الگوریتم bidi نمی‌تواند ترتیب را عوض کند.
/// </summary>
public static partial class PlateFmt
{
    private static readonly Regex Tokens =
        new(@"([0-9]+)|([\u0600-\u06ff\u200c]+)", RegexOptions.Compiled);

    private const string IranWord = "ایران";

    /// <summary>نرمال‌سازی ارقام فارسی/عربی به لاتین برای تشخیص الگو.</summary>
    private static string Lat(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(ch switch
            {
                >= '۰' and <= '۹' => (char)(ch - '۰' + '0'),
                >= '٠' and <= '٩' => (char)(ch - '٠' + '0'),
                _ => ch,
            });
        return sb.ToString();
    }

    /// <summary>اجزای بازسازی‌شدهٔ پلاک — null یعنی ساختار استاندارد تشخیص داده نشد.</summary>
    public sealed record IranianPlate(string Left, string Letter, string Right, string Province);

    /// <summary>
    /// پارسر کانونیال (فاز ۵۱) — از متن خام، (دورقم، حرف، سه‌رقم، کد استان) را استخراج می‌کند.
    /// حرف می‌تواند قبل یا بعد از «ایران» باشد؛ اعداد هم به ترتیب/طول تشخیص داده می‌شوند
    /// (دو‌رقمی = سمت راست پلاک، سه‌چهاررقمی = سمت چپ).
    /// </summary>
    public static IranianPlate? Parse(string? plateText)
    {
        var raw = Lat((plateText ?? "").Trim());
        if (raw.Length == 0) return null;

        string pre = raw, post = "";
        var iranIdx = raw.IndexOf(IranWord, StringComparison.Ordinal);
        if (iranIdx >= 0)
        {
            pre = raw[..iranIdx];
            post = raw[(iranIdx + IranWord.Length)..];
        }

        // توکن‌های بعد از «ایران»: اولین رشتهٔ رقم = کد استان؛ اولین رشتهٔ حرف = حرفِ پلاکِ گم‌شده
        string? province = null, letterFromPost = null;
        var postDigits = new List<string>();
        foreach (var m in Tokens.Matches(post))
        {
            var t = m.ToString() ?? string.Empty;
            if (t.Length == 0) continue;
            var isDigit = char.IsAsciiDigit(t[0]);
            if (isDigit)
            {
                postDigits.Add(t);
                province ??= t;
            }
            else letterFromPost ??= t.Trim('\u200c');
        }

        // توالی اصلی: توکن‌های قبل از ایران (+ حرف بازیافتی از بعدِ ایران در انتهای آن‌ها)
        var seq = new List<(bool Digit, string Text)>();
        foreach (var m in Tokens.Matches(pre))
        {
            var t = m.ToString() ?? string.Empty;
            if (t.Length == 0) continue;
            seq.Add((char.IsAsciiDigit(t[0]), t));
        }
        if (letterFromPost is not null) seq.Add((false, letterFromPost));

        // کاندیدهای عدد: ارقام قبل از ایران + ارقام اضافیِ بعد از ایران (غیر از اولین رقم = کد استان)
        for (var i = 1; i < postDigits.Count; i++) seq.Add((true, postDigits[i]));

        var letterIdx = seq.FindIndex(t => !t.Digit);
        var digitIdxs = new List<int>();
        for (var i = 0; i < seq.Count; i++) if (seq[i].Digit) digitIdxs.Add(i);

        if (letterIdx >= 0 && digitIdxs.Count >= 2)
        {
            var before = digitIdxs.Where(i => i < letterIdx).ToList();
            var after = digitIdxs.Where(i => i > letterIdx).ToList();
            string? left = null, right = null;

            if (before.Count > 0 && after.Count > 0)
            {
                left = seq[before[^1]].Text; right = seq[after[0]].Text;
            }
            else if (before.Count >= 2 && after.Count == 0)
            {
                // «36 436 ن» — حرف در انتها: دو‌رقمی=چپِ پلاک (سمت راست تصویری)، بلند=سه‌رقم
                (left, right) = PickPair(before.Select(i => seq[i].Text).ToList());
            }
            else if (after.Count >= 2 && before.Count == 0)
            {
                // «ن 36 436» — حرف در ابتدا
                (left, right) = PickPair(after.Select(i => seq[i].Text).ToList());
            }

            if (left is not null && right is not null &&
                left.Length is 1 or 2 && right.Length is >= 2 and <= 4)
                return new IranianPlate(left, seq[letterIdx].Text.Trim('\u200c'), right, province ?? "");
        }

        return null;
    }

    /// <summary>انتخاب زوج (دو‌رقمی، بلند) از میان چند رشتهٔ عدد — بر پایهٔ طول.</summary>
    private static (string, string) PickPair(List<string> nums)
    {
        var left = nums.FirstOrDefault(n => n.Length is 1 or 2) ?? nums[0];
        var right = nums.LastOrDefault(n => n.Length is >= 2 and <= 4 && n != left) ?? nums[^1];
        if (left == right) return ("", "");
        return (left, right);
    }

    // ------------------------------------------------------------------
    // رندر HTML
    // ------------------------------------------------------------------
    public static IHtmlContent Render(string? plateText, bool mini = false)
    {
        var html = Build(plateText, mini);
        return new HtmlString(html);
    }

    private static string Build(string? plateText, bool mini)
    {
        var raw = (plateText ?? "—").Trim();
        var cls = "plate-real" + (mini ? " mini" : "");

        var sb = new System.Text.StringBuilder();
        sb.Append("<span class=\"").Append(cls).Append("\">");
        sb.Append("<span class=\"pr-main\" dir=\"ltr\">");
        sb.Append(MainTokens(raw));
        sb.Append("</span>");

        // بلوک استان — فقط وقتی «ایران» در متن خام بوده (پلاک‌های بدون بلوک استان دست‌نخورده می‌مانند)
        if (raw.Contains(IranWord, StringComparison.Ordinal))
        {
            var p = Parse(raw);
            var code = p is not null ? p.Province : "";
            if (code.Length == 0)
            {
                // سازگاری با رفتار قبلی: اولین ارقام بعد از «ایران»
                var iranIdx = raw.IndexOf(IranWord, StringComparison.Ordinal);
                var post = Lat(raw[(iranIdx + IranWord.Length)..]);
                var m2 = Regex.Match(post, "[0-9]+");
                code = m2.Success ? m2.Value : "";
            }
            sb.Append("<span class=\"pr-region\" dir=\"rtl\">");
            sb.Append("<span class=\"pr-iran\">").Append(IranWord).Append("</span>");
            sb.Append("<span class=\"pr-code\">").Append(System.Net.WebUtility.HtmlEncode(code)).Append("</span>");
            sb.Append("</span>");
        }
        sb.Append("</span>");
        return sb.ToString();
    }

    /// <summary>سریال پلاک: از پارسر کانونیال چیدمان صریح سه توکن ساخته می‌شود؛
    /// در نبود ساختار استاندارد، توکن‌سازی به ترتیب منطقی (رفتار فاز ۴۵) انجام می‌شود.</summary>
    private static string MainTokens(string raw)
    {
        System.Func<string, string> enc = System.Net.WebUtility.HtmlEncode;

        var p = Parse(raw);
        if (p is not null)
            return $"<span class=\"pr-tok\">{enc(p.Left)}</span>" +
                   $"<span class=\"pr-tok pr-l\">{enc(p.Letter)}</span>" +
                   $"<span class=\"pr-tok\">{enc(p.Right)}</span>";

        var mainRaw = raw;
        var iranIdx = raw.IndexOf(IranWord, StringComparison.Ordinal);
        if (iranIdx >= 0) mainRaw = raw[..iranIdx].Trim();
        if (string.IsNullOrWhiteSpace(mainRaw)) return "<span class=\"pr-tok\">—</span>";

        var norm = Lat(mainRaw).Replace(" ", "");
        var sb = new System.Text.StringBuilder();
        foreach (Match t in Tokens.Matches(norm))
        {
            var kind = char.IsAsciiDigit(t.Value[0]) ? "" : " pr-l";
            sb.Append("<span class=\"pr-tok").Append(kind).Append("\">").Append(enc(t.Value)).Append("</span>");
        }
        return sb.Length == 0
            ? $"<span class=\"pr-tok\">{enc(mainRaw)}</span>"
            : sb.ToString();
    }

    // ------------------------------------------------------------------
    // کلید کانونیال تطبیق پلاک (فاز ۵۱)
    // ------------------------------------------------------------------
    /// <summary>
    /// کلید یکسان‌سازی‌شده برای تطبیق پلاک ثبت‌شده با خوانش دستگاه: از هر قالبی که
    /// اجزای پلاک بیاید، کلید «left+letter+right+province» ساخته می‌شود تا
    /// «36 ن 436 ایران 11» و «36 436 ایران ن 11» یکی شوند؛ در نبود ساختار
    /// استاندارد، مثل قبل حذف «ایران»/فاصله‌ها انجام می‌شود (سازگاری کامل).
    /// </summary>
    public static string CanonicalKey(string? plateText)
    {
        var raw = (plateText ?? "").Trim();
        if (raw.Length == 0) return "";
        var p = Parse(raw);
        if (p is not null)
            return p.Left + p.Letter + p.Right + p.Province;
        return Lat(raw).Replace(IranWord, "").Replace(" ", "").Replace("\u200c", "").Trim();
    }
}
