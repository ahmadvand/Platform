using System.Globalization;
using System.Text;

namespace Platform.Web.Services;

/// <summary>
/// فاز ۸۶ — نوع تقویم «نمایشی» انتخابی ادمین در تنظیمات سامانه.
/// فقط نحوهٔ نمایش تاریخ‌ها عوض می‌شود؛ داده‌ها، کلیدهای تاریخ (DateKey)،
/// محاسبات و تعطیلات دست‌نخورده می‌مانند (همه به‌صورت شمسی/میلادی ذخیره می‌شوند).
/// </summary>
public enum HrCalendarMode
{
    /// <summary>شمسی (جلالی) — پیش‌فرض</summary>
    Jalali,
    /// <summary>میلادی</summary>
    Gregorian,
    /// <summary>قمری (هجری قمری — ام‌القری)</summary>
    Hijri,
}

/// <summary>
/// ابزارهای تقویم جلالی و قالب‌بندی فارسی — معادل lib/persian.ts
/// بر پایه System.Globalization.PersianCalendar (داخلی .NET).
/// </summary>
public static class Jalali
{
    private static readonly PersianCalendar Pc = new();
    private static readonly string[] MonthNames =
    {
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    };
    private static readonly string[] WeekdayNames =
        { "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه" };

    // ---------- فاز ۸۶ — تقویم میلادی/قمری فقط برای «نمایش» ----------
    /// <summary>تقویم قمری ام‌القری — برای حالت نمایشی «قمری» (بازهٔ پشتیبانی ۱۳۱۸-۱۵۰۰ قمری).</summary>
    private static readonly UmAlQuraCalendar Hc = new();
    private static readonly string[] MonthNamesEn =
        { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    private static readonly string[] MonthNamesHijri =
        { "محرم", "صفر", "ربیع‌الاول", "ربیع‌الثانی", "جمادی‌الاول", "جمادی‌الثانی",
          "رجب", "شعبان", "رمضان", "شوال", "ذی‌القعده", "ذی‌الحجه" };

    /// <summary>تقویم نمایشی جاری — پیش‌فرض شمسی؛ میدلور فرهنگ در هر درخواست از تنظیمات ادمین پر می‌کند.</summary>
    public static HrCalendarMode DisplayMode = HrCalendarMode.Jalali;

    /// <summary>
    /// فاز ۹۶ (درخواست کاربر: «محاسبات تقویم تعطیلات درست نیست») — جبران روز تقویم قمری.
    /// مبنای قمریِ .NET/ICU «ام‌القری» است که برای سال‌های جاری معمولاً یک روز جلوتر از تقویم
    /// قمری رسمی ایران/افغانستان (رؤیت هلال محلی) است؛ نتیجه: مناسبت‌های قمری مثل ۱۵ شعبان
    /// در نمایش سامانه یک روز عقب‌تر از تقویم دیواری کاربر می‌افتند. با مقدار ‎-۱ (پیش‌فرض این
    /// محصول) نمایش قمری از تاریخ میلادی یک روز کم می‌کند و با تقویم رسمی یکی می‌شود؛ ادمین
    /// در تنظیمات می‌تواند ‎-۲..+۲ انتخاب کند (صفر = دقیقاً ام‌القری). فقط «نمایش/قالب برون‌بری»
    /// جابجا می‌شود؛ داده‌ها و کلیدهای تاریخ دست‌نخورده می‌مانند.
    /// فاز ۱۱۰ — مقدار ذخیره‌شدهٔ ردیف‌های قدیمی (صفرِ DEFAULTِ وصال عمومی) در راه‌اندازی
    /// یک‌باره به همین پیش‌فرض ترمیم می‌شود؛ تا قبل از آن این استاتیک هم همان ‎-۱ را دارد.
    /// </summary>
    public static int HijriAdjustDays = -1;

    /// <summary>تاریخ با جبران قمری (فقط برای تبدیل «نمایشی» قمری استفاده شود).</summary>
    private static DateTime HijriAdj(DateTime dt) => HijriAdjustDays == 0 ? dt : dt.AddDays(HijriAdjustDays);

    /// <summary>کلید رشته‌ای تقویم نمایشی — مثل jalali/gregorian/hijri (برای حسابرسی/آجاکس).</summary>
    public static string DisplayModeKey => DisplayMode switch
    {
        HrCalendarMode.Gregorian => "gregorian",
        HrCalendarMode.Hijri => "hijri",
        _ => "jalali",
    };

    public static (int Y, int M, int D) ToJalali(DateTime dt) =>
        (Pc.GetYear(dt), Pc.GetMonth(dt), Pc.GetDayOfMonth(dt));

    public static DateTime FromJalali(int y, int m, int d) => Pc.ToDateTime(y, m, d, 0, 0, 0, 0);

    /// <summary>کلید تاریخ میلادی YYYY-MM-DD (هم‌ارز dateKey).</summary>
    public static string DateKey(DateTime dt) => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime ParseDateKey(string s) =>
        DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>1404/07/26 — با تقویم نمایشی انتخابی ادمین (شمسی/میلادی/قمری)؛ فقط نمایش، نه داده.</summary>
    public static string Fmt(DateTime dt)
    {
        if (DisplayMode == HrCalendarMode.Gregorian)
            return dt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        if (DisplayMode == HrCalendarMode.Hijri)
        {
            var h = HijriAdj(dt);
            return $"{Hc.GetYear(h):0000}/{Hc.GetMonth(h):00}/{Hc.GetDayOfMonth(h):00}";
        }
        var (y, m, d) = ToJalali(dt);
        return $"{y:0000}/{m:00}/{d:00}";
    }

    public static string FmtTime(DateTime dt) => dt.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string FmtLong(DateTime dt) => $"{Fmt(dt)} - {FmtTime(dt)}";

    /// <summary>۲۶ مهر ۱۴۰۴ — پنجشنبه — با تقویم نمایشی انتخابی ادمین (شمسی/میلادی/قمری).</summary>
    public static string FmtFa(DateTime dt)
    {
        if (DisplayMode == HrCalendarMode.Gregorian)
        {
            return HrT.Rtl
                ? $"{ToPersianDigits(dt.Day)} {MonthNamesEn[dt.Month - 1]} {ToPersianDigits(dt.Year)}"
                : $"{dt.Day:00} {MonthNamesEn[dt.Month - 1]} {dt.Year:0000}";
        }
        if (DisplayMode == HrCalendarMode.Hijri)
        {
            var h = HijriAdj(dt);
            var hd = Hc.GetDayOfMonth(h); var hm = Hc.GetMonth(h); var hy = Hc.GetYear(h);
            return HrT.Rtl
                ? $"{ToPersianDigits(hd)} {MonthNamesHijri[hm - 1]} {ToPersianDigits(hy)}"
                : $"{hd:00} {MonthNamesHijri[hm - 1]} {hy:0000}";
        }
        var (y, m, d) = ToJalali(dt);
        return $"{ToPersianDigits(d)} {MonthNames[m - 1]} {ToPersianDigits(y)}";
    }

    /// <summary>
    /// فاز ۹۶ — تاریخ قمریِ یک لحظهٔ مشخص با جبران تنظیمات — برای قالب برون‌بری تردد
    /// (قمری) و هر جایی که تاریخ قمریِ مستقل از «نمایشی» جاری لازم است.
    /// خروجی: سال/ماه/روز قمری (ام‌القری + جبران ادمین).
    /// </summary>
    public static (int Y, int M, int D) ToHijri(DateTime dt)
    {
        var h = HijriAdj(dt);
        return (Hc.GetYear(h), Hc.GetMonth(h), Hc.GetDayOfMonth(h));
    }

    /// <summary>نمایش قمری ۱۴۴۷/۰۸/۱۵ — با جبران ادمین؛ مستقل از DisplayMode (برای خروجی قالب).</summary>
    public static string FmtHijri(DateTime dt)
    {
        var (y, m, d) = ToHijri(dt);
        return $"{y:0000}/{m:00}/{d:00}";
    }

    public static string FmtFaLong(DateTime dt) => $"{FmtFa(dt)}، {WeekdayName(dt)}";

    public static string MonthName(int m) => MonthNames[Math.Clamp(m, 1, 12) - 1];

    public static string WeekdayName(DateTime dt) => WeekdayNames[((int)dt.DayOfWeek) % 7];

    public static bool IsFriday(DateTime dt) => dt.DayOfWeek == DayOfWeek.Friday;

    /// <summary>
    /// فاز ۸۹ — ارقام بومی: فقط در زبان‌های راست‌به‌چپ (fa/prs/ps/…) ارقام فارسی می‌شوند؛
    /// در زبان‌های چپ‌به‌راست (en/ka/ky/uz/tg/tk) رشتهٔ لاتین دست‌نخورده برمی‌گردد.
    /// این قاعده «یک‌جا» در پایین‌ترین لایه اعمال می‌شود تا همهٔ فراخوانی‌های بی‌شرط
    /// (FmtFa، Num، PersonnelCode و…) هم از قاعدهٔ زبان پیروی کنند (درخواست کاربر:
    /// «وقتی زبان انگلیسی شده، اعداد هم انگلیسی بشن»).
    /// </summary>
    public static string ToPersianDigits(string s)
    {
        if (!HrT.Rtl) return s;
        return new StringBuilder(s.Length)
            .Append(s.Replace('0', '۰').Replace('1', '۱').Replace('2', '۲').Replace('3', '۳').Replace('4', '۴')
                     .Replace('5', '۵').Replace('6', '۶').Replace('7', '۷').Replace('8', '۸').Replace('9', '۹'))
            .ToString();
    }

    public static string ToPersianDigits(int n) => ToPersianDigits(n.ToString(CultureInfo.InvariantCulture));
    public static string ToPersianDigits(long n) => ToPersianDigits(n.ToString(CultureInfo.InvariantCulture));

    public static string Num(long n) => ToPersianDigits(n.ToString("N0", CultureInfo.InvariantCulture));
    public static string Num(int n) => Num((long)n);
    public static string Num(decimal n) => ToPersianDigits(n.ToString("N0", CultureInfo.InvariantCulture));

    /// <summary>نمایش دقیقه به قالب HH:MM.</summary>
    public static string MinutesToHm(int minutes) =>
        $"{minutes / 60:00}:{minutes % 60:00}";

    /// <summary>بازه ماه جلالی: [start, endExclusive) — معادل jalaliMonthRange.</summary>
    public static (DateTime Start, DateTime EndExclusive) MonthRange(int jy, int jm)
    {
        var start = FromJalali(jy, jm, 1);
        var end = jm < 12 ? FromJalali(jy, jm + 1, 1) : FromJalali(jy + 1, 1, 1);
        return (start, end);
    }

    public static int MonthLength(int jy, int jm)
    {
        var (s, e) = MonthRange(jy, jm);
        return (int)(e - s).TotalDays;
    }

    /// <summary>ماتریس شبکه ماه جلالی (هفته‌ها × ۷ روز؛ خانه خالی null). هفته از شنبه شروع می‌شود.</summary>
    public static List<(DateTime? Date, int? JDay)> MonthGrid(int jy, int jm)
    {
        var len = MonthLength(jy, jm);
        var first = FromJalali(jy, jm, 1);
        // DayOfWeek: Sunday=0 ... Saturday=6 — ایندکس هفته فارسی: شنبه=0
        int startCol = ((int)first.DayOfWeek + 1) % 7;
        var cells = new List<(DateTime?, int?)>();
        for (int i = 0; i < startCol; i++) cells.Add((null, null));
        for (int d = 1; d <= len; d++) cells.Add((FromJalali(jy, jm, d), d));
        while (cells.Count % 7 != 0) cells.Add((null, null));
        return cells;
    }

    /// <summary>اختلاف روز بین دو کلید تاریخ.</summary>
    public static int DiffDays(string a, string b) =>
        (int)(ParseDateKey(b) - ParseDateKey(a)).TotalDays;

    // ---------- فاز ۹۷ — ماه‌شمار تعطیلات بر پایهٔ «تقویم نمایشی» ----------
    // درخواست کاربر: «سامانه تقویم قمری انتخاب کرده ولی تقویم تعطیلات شمسی است!» —
    // صفحهٔ تعطیلات باید مثل بقیهٔ سامانه از DisplayMode/HijriAdjustDays پیروی کند؛
    // داده‌ها (کلید میلادی Holiday.Date) دست‌نخورده می‌مانند — فقط نمایش و ورودی فرم.

    /// <summary>نام ماه در تقویم نمایشی (شمسی/میلادی/قمری).</summary>
    public static string DisplayMonthName(HrCalendarMode mode, int m) => mode switch
    {
        HrCalendarMode.Gregorian => MonthNamesEn[Math.Clamp(m, 1, 12) - 1],
        HrCalendarMode.Hijri => MonthNamesHijri[Math.Clamp(m, 1, 12) - 1],
        _ => MonthName(m),
    };

    /// <summary>(سال، ماه) جاری بر پایهٔ تقویم نمایشی (قمری با جبران ادمین).</summary>
    public static (int Y, int M) DisplayNow(HrCalendarMode mode)
    {
        if (mode == HrCalendarMode.Gregorian) return (DateTime.Now.Year, DateTime.Now.Month);
        if (mode == HrCalendarMode.Hijri) { var h = ToHijri(DateTime.Now); return (h.Y, h.M); }
        var j = ToJalali(DateTime.Now);
        return (j.Y, j.M);
    }

    /// <summary>
    /// طول ماه و تاریخ میلادیِ واقعیِ روز اول در تقویم نمایشی.
    /// قمری: تاریخ واقعی = تاریخ ام‌القری منهای جبران (برای آنکه نمایش همان روز با جبران، همان روز ماه شود).
    /// </summary>
    public static (int Len, DateTime First) DisplayMonthInfo(HrCalendarMode mode, int y, int m)
    {
        m = Math.Clamp(m, 1, 12);
        if (mode == HrCalendarMode.Gregorian)
            return (DateTime.DaysInMonth(y, m), new DateTime(y, m, 1));
        if (mode == HrCalendarMode.Hijri)
            return (Hc.GetDaysInMonth(y, m), Hc.ToDateTime(y, m, 1, 0, 0, 0, 0).AddDays(-HijriAdjustDays).Date);
        return (MonthLength(y, m), FromJalali(y, m, 1));
    }

    /// <summary>
    /// ماتریس شبکهٔ ماه تقویم نمایشی (هفته‌ها × ۷؛ هفته از شنبه؛ خانهٔ خالی null).
    /// هر سلول: تاریخ میلادیِ واقعی + شمارهٔ روز در تقویم نمایشی — ستون هفته از DayOfWeek واقعی
    /// نتیجه می‌شود، پس روز هفته در همهٔ تقویم‌ها درست است (رفع باگ ۱۵ شعبان/شنبه).
    /// </summary>
    public static List<(DateTime? Date, int? Day)> DisplayMonthGrid(HrCalendarMode mode, int y, int m)
    {
        var (len, first) = DisplayMonthInfo(mode, y, m);
        int startCol = ((int)first.DayOfWeek + 1) % 7;
        var cells = new List<(DateTime?, int?)>();
        for (int i = 0; i < startCol; i++) cells.Add((null, null));
        for (int d = 1; d <= len; d++) cells.Add((first.AddDays(d - 1), d));
        while (cells.Count % 7 != 0) cells.Add((null, null));
        return cells;
    }

    /// <summary>تبدیل تاریخ واردشده در تقویم نمایشی به میلادی واقعی (ثبت تعطیلی) — قمری با جبران.</summary>
    public static DateTime FromDisplayDate(HrCalendarMode mode, int y, int m, int d)
    {
        if (mode == HrCalendarMode.Gregorian) return new DateTime(y, m, d);
        if (mode == HrCalendarMode.Hijri) return Hc.ToDateTime(y, m, d, 0, 0, 0, 0).AddDays(-HijriAdjustDays).Date;
        return FromJalali(y, m, d);
    }

    public static string AddDaysKey(string key, int days) =>
        DateKey(ParseDateKey(key).AddDays(days));

    /// <summary>اعتبار رمز عبور — خطای فارسی یا null (معادل passwordPolicy)؛ ترجمه به زبان کاربر.</summary>
    public static string? ValidatePassword(string pwd)
    {
        if (string.IsNullOrWhiteSpace(pwd) || pwd.Length < 8)
            return HrT.T("رمز عبور باید حداقل ۸ کاراکتر باشد");
        if (!pwd.Any(char.IsLetter) || !pwd.Any(char.IsDigit))
            return HrT.T("رمز عبور باید شامل حرف و عدد باشد");
        if (!pwd.Any(char.IsUpper) || !pwd.Any(char.IsLower))
            return HrT.T("رمز عبور باید شامل حروف بزرگ و کوچک باشد");
        return null;
    }
}
