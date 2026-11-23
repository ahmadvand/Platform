using System.Globalization;

namespace Platform.Web.Services;

/// <summary>
/// فاز ۹۱ — سامانهٔ ارز (درخواست کاربر): «هر کشوری ارز متفاوتی دارد» — ارز فعال در
/// «تنظیمات سامانه» انتخاب می‌شود و هر جا مبلغ پولی نمایش داده می‌شود (وب، فیش، PDF، اکسل)
/// با همان ارز قالب می‌گیرد: نشان ارز + جداکنندهٔ هزارگان + ارقام بومی زبان جاری.
///
/// طراحی امن:
/// • داده‌ها دست‌نخورده می‌مانند — ارز فقط «قالب‌بندی نمایش» است؛ محاسبات حقوق/پارکینگ/فرمول‌ساز
///   همچنان با واحد پایه انجام می‌شود (صفر ریسک روی اعداد ذخیره‌شده).
/// • «نرخ تبدیل نمایشی» اختیاری است (پیش‌فرض ۱ = بدون تبدیل) — فقط خروجی نمایشی را ضرب می‌کند.
/// • CultureMiddleware در هر درخواست Configure را از AppSettings صدا می‌زند؛ پیش از آن
///   پیش‌فرض ریال ایران (IRR) است — هیچ‌وقت استثنا نمی‌دهد.
/// </summary>
public static class Money
{
    /// <summary>یک ارز کاتالوگ — نشان، عنوان دوزبانه و تعداد رقم اعشار نمایشی.</summary>
    public sealed record Cur(string Code, string Sym, string Fa, string En, int Dec = 0, bool Prefix = false);

    /// <summary>کاتالوگ ارزهای رایج — ارزهای کشورهای زبان‌های سامانه در صدم گروه هستند:
    /// fa=ریال ایران، prs/ps=افغانی، ka=لاری، ky=سوم قرقیزستان، uz=سوم ازبکستان،
    /// tg=سمانی تاجیکستان، tk=منات ترکمنستان + ارزهای پرکاربرد منطقه و جهانی.</summary>
    public static readonly List<Cur> Catalog =
    [
        // ---------- ارز کشورهای زبان‌های سامانه ----------
        new("IRR",  "ریال",   "ریال ایران",              "Iranian Rial"),
        new("IRT",  "تومان",  "تومان ایران",             "Iranian Toman"),
        new("AFN",  "؋",      "افغانی افغانستان",         "Afghan Afghani"),
        new("GEL",  "₾",      "لاری گرجستان",            "Georgian Lari"),
        new("KGS",  "сом",    "سوم قرقیزستان",           "Kyrgyzstani Som"),
        new("UZS",  "soʻm",   "سوم ازبکستان",            "Uzbekistani Som"),
        new("TJS",  "смн",    "سمانی تاجیکستان",          "Tajikistani Somoni"),
        new("TMT",  "TMT",    "منات ترکمنستان",           "Turkmenistani Manat"),
        // ---------- خاورمیانه و آسیای مرکزی ----------
        new("AZN",  "₼",      "منات آذربایجان",           "Azerbaijani Manat"),
        new("TRY",  "₺",      "لیر ترکیه",               "Turkish Lira"),
        new("AED",  "د.إ",    "درهم امارات",             "UAE Dirham"),
        new("SAR",  "﷼",      "ریال عربستان",            "Saudi Riyal"),
        new("QAR",  "﷼",      "ریال قطر",                "Qatari Riyal"),
        new("KWD",  "د.ك",    "دینار کویت",              "Kuwaiti Dinar", 3),
        new("BHD",  "د.ب",    "دینار بحرین",             "Bahraini Dinar", 3),
        new("OMR",  "﷼",      "ریال عمان",               "Omani Rial", 3),
        new("IQD",  "ع.د",    "دینار عراق",              "Iraqi Dinar"),
        new("PKR",  "₨",      "روپیه پاکستان",           "Pakistani Rupee"),
        new("INR",  "₹",      "روپیه هند",               "Indian Rupee", 2),
        new("CNY",  "¥",      "یوان چین",                "Chinese Yuan", 2),
        new("RUB",  "₽",      "روبل روسیه",              "Russian Ruble", 2),
        // ---------- جهانی ----------
        new("USD",  "$",      "دلار آمریکا",             "US Dollar", 2, Prefix: true),
        new("EUR",  "€",      "یورو",                    "Euro", 2, Prefix: true),
        new("GBP",  "£",      "پوند بریتانیا",           "British Pound", 2, Prefix: true),
        new("CHF",  "Fr",     "فرانک سوئیس",             "Swiss Franc", 2, Prefix: true),
        new("JPY",  "¥",      "ین ژاپن",                 "Japanese Yen", 0, Prefix: true),
        new("CAD",  "C$",     "دلار کانادا",             "Canadian Dollar", 2, Prefix: true),
        new("AUD",  "A$",     "دلار استرالیا",           "Australian Dollar", 2, Prefix: true),
        new("SEK",  "kr",     "کرون سوئد",               "Swedish Krona", 2),
        new("NOK",  "kr",     "کرون نروژ",               "Norwegian Krone", 2),
    ];

    /// <summary>کد ارز فعال — از AppSettings.Currency (پیش‌فرض: ریال ایران).</summary>
    public static string Code = "IRR";

    /// <summary>نرخ تبدیل نمایشی — پیش‌فرض ۱ (بدون تبدیل). فقط خروجی نمایش را ضرب می‌کند.</summary>
    public static decimal Rate = 1m;

    /// <summary>پیکربندی از تنظیمات ادمین — CultureMiddleware در هر درخواست صدا می‌زند؛ امن و بدون استثنا.</summary>
    public static void Configure(string? code, string? rate)
    {
        Code = Find(code) is null ? "IRR" : code!.Trim().ToUpperInvariant();
        Rate = decimal.TryParse(rate, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) && r > 0 ? r : 1m;
    }

    public static Cur? Find(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null
        : Catalog.FirstOrDefault(c => c.Code == code.Trim().ToUpperInvariant());

    /// <summary>ارز فعال — اگر کد تنظیم‌شده در کاتالوگ نباشد، ریال ایران (سطر اول).</summary>
    public static Cur Current => Find(Code) ?? Catalog[0];

    /// <summary>نشان/نماد ارز فعال — مثل «ریال»، «؋»، «$». برای زبان‌های LTR، ارزهای
    /// بدون نماد لاتین (ریال/تومان) با کد بین‌المللی نشان داده می‌شوند (IRR/IRT).</summary>
    public static string Sym() =>
        !HrT.Rtl && Current.Code is "IRR" or "IRT" ? Current.Code : Current.Sym;

    /// <summary>عنوان ارز فعال به زبان جاری رابط — برای سرستون‌ها و برچسب‌ها («مبلغ (ریال ایران)»).</summary>
    public static string CurTitle() => HrT.Rtl ? Current.Fa : Current.En;

    /// <summary>قالب‌بندی مبلغ با ارز فعال: نرخ نمایشی × عدد + جداکنندهٔ هزارگان + ارقام بومی + نشان ارز.
    /// withSym=false فقط عدد (برای خانه‌های جدول/PDF که نشان در سرستون است).</summary>
    public static string Fmt(decimal v, bool withSym = true)
    {
        var x = v * Rate;
        var dec = Current.Dec;
        var s = dec == 0
            ? HrT.Num(Math.Round(x, 0))
            : HrT.Digits(Math.Round(x, dec).ToString("N" + dec, CultureInfo.InvariantCulture));
        return withSym ? (Current.Prefix ? Sym() + " " + s : s + " " + Sym()) : s;
    }

    public static string Fmt(long v, bool withSym = true) => Fmt((decimal)v, withSym);
    public static string Fmt(int v, bool withSym = true) => Fmt((decimal)v, withSym);

    /// <summary>ارز پیشنهادی هر زبان — برای پیش‌فرض‌گذاری هوشمند هنگام تغییر زبان در تنظیمات
    /// (fa=ریال، دری/پشتو=افغانی، گرجی=لاری، قرقیزی=سوم، ازبکی=سوم، تاجیکی=سمانی، ترکمنی=منات).</summary>
    public static string SuggestForLang(string? lang) => lang switch
    {
        "prs" or "ps" => "AFN",
        "ka" => "GEL",
        "ky" => "KGS",
        "uz" => "UZS",
        "tg" => "TJS",
        "tk" => "TMT",
        "en" => "USD",
        _ => "IRR",
    };
}
