using System.Globalization;
using Platform.Web.Services;

// پروب راستی‌آزمایی تقویم — آیتم ۷
// خروجی: تبدیل قمری ام‌القری برای نیمهٔ شعبان ۱۴۴۷/۱۴۴۸، اثر جبران‌های مختلف،
// و بررسی هماهنگی روز هفته در شبکهٔ ماه‌شمار.

var pc = new PersianCalendar();
var uaq = new UmAlQuraCalendar();

Console.WriteLine("=== مرجع شمسی (PersianCalendar) ===");
foreach (var (jy, jm, jd) in new[] { (1404, 11, 4), (1405, 11, 4), (1404, 11, 15) })
{
    var g = pc.ToDateTime(jy, jm, jd, 0, 0, 0, 0);
    Console.WriteLine($"Jalali {jy}/{jm:D2}/{jd:D2} -> Gregorian {g:yyyy-MM-dd ddd} (UTC-ish local)");
}
foreach (var g in new[] { new DateTime(2026, 2, 4), new DateTime(2027, 1, 23), new DateTime(2027, 1, 24), new DateTime(2027, 1, 25) })
{
    var j = Jalali.ToJalali(g);
    Console.WriteLine($"Gregorian {g:yyyy-MM-dd ddd} -> Jalali {j.Y}/{j.M:D2}/{j.D:D2}");
}

Console.WriteLine();
Console.WriteLine("=== قمری ام‌القری: روز ۱۵ ماه شعبان ===");
foreach (var hy in new[] { 1447, 1448 })
{
    var g = uaq.ToDateTime(hy, 8, 15, 0, 0, 0, 0);
    var j = Jalali.ToJalali(g);
    Console.WriteLine($"UAQ {hy}/08/15 -> Gregorian {g:yyyy-MM-dd ddd} -> Jalali {j.Y}/{j.M:D2}/{j.D:D2}");
    // و برعکس: چند روز میلادی پیرامون
    foreach (var off in new[] { -1, 0, 1 })
    {
        var g2 = g.AddDays(off);
        var h2 = uaq.GetYear(g2) == hy ? $"{uaq.GetYear(g2)}/{uaq.GetMonth(g2):00}/{uaq.GetDayOfMonth(g2):00}" : "(سال دیگر)";
        Console.WriteLine($"   Gregorian {g2:yyyy-MM-dd ddd} -> UAQ {h2}");
    }
}

Console.WriteLine();
Console.WriteLine("=== نمایش صفحهٔ تعطیلات (DisplayMonthGrid) برای شعبان ۱۴۴۸ با جبران‌های مختلف ===");
foreach (var adj in new[] { -2, -1, 0, 1, 2 })
{
    Jalali.HijriAdjustDays = adj;
    var (len, first) = Jalali.DisplayMonthInfo(HrCalendarMode.Hijri, 1448, 8);
    var cell15 = first.AddDays(14);
    var j15 = Jalali.ToJalali(cell15);
    var g15raw = uaq.ToDateTime(1448, 8, 15, 0, 0, 0, 0);
    Console.WriteLine($"adj={adj:+0;-0;0}: grid first={first:yyyy-MM-dd ddd}, cell15={cell15:yyyy-MM-dd ddd} -> Jalali {j15.Y}/{j15.M:D2}/{j15.D:D2}  (UAQ raw 15th={g15raw:yyyy-MM-dd})");
}
Jalali.HijriAdjustDays = 0;

Console.WriteLine();
Console.WriteLine("=== سازگاری روز هفته: سلول‌های شبکه ===");
// در همهٔ شبکه‌ها: سلول n باید در ستون Weekday واقعی خودش باشد (شنبه=0)
foreach (var (mode, y, m) in new[] { (HrCalendarMode.Jalali, 1405, 11), (HrCalendarMode.Gregorian, 2027, 1), (HrCalendarMode.Hijri, 1448, 8) })
{
    Jalali.HijriAdjustDays = -1;
    var grid = Jalali.DisplayMonthGrid(mode, y, m);
    bool ok = true; int bad = 0;
    for (int i = 0; i < grid.Count; i++)
    {
        var (d, day) = grid[i];
        if (d is null) continue;
        if (i % 7 != (((int)d.Value.DayOfWeek) + 1) % 7) { ok = false; bad++; }
    }
    Console.WriteLine($"{mode} {y}/{m:D2}: weekday alignment ok={ok} (bad cells={bad}, cells={grid.Count})");
}
Jalali.HijriAdjustDays = 0;

Console.WriteLine();
Console.WriteLine("=== گردش کامل قمری ۱۴۴۸ (روزهای ماه به میلادی) — برای مقایسهٔ طول ماه ===");
int m8 = uaq.GetDaysInMonth(1448, 8);
Console.WriteLine($"Sha'ban 1448 length (UAQ) = {m8}");
var first8 = uaq.ToDateTime(1448, 8, 1, 0, 0, 0, 0);
Console.WriteLine($"1 Sha'ban 1448 (UAQ) = {first8:yyyy-MM-dd ddd}");
int m9 = uaq.GetDaysInMonth(1448, 9);
var first9 = uaq.ToDateTime(1448, 9, 1, 0, 0, 0, 0);
Console.WriteLine($"1 Ramadan 1448 (UAQ) = {first9:yyyy-MM-dd ddd}, Ramadan length={m9}");
var first8y = uaq.ToDateTime(1448, 1, 1, 0, 0, 0, 0);
Console.WriteLine($"1 Muharram 1448 (UAQ) = {first8y:yyyy-MM-dd ddd}");

Console.WriteLine();
Console.WriteLine("=== فاز ۱۱۲ — آیتم ۸: منطق قالب برون‌بری تردد (همان FmtDate/FmtStamp ماژول Attendance) ===");
// بازتولید دقیق منطق FmtDate/FmtStamp در AttendanceController (وابسته به Jalali فقط)
Jalali.HijriAdjustDays = -1; // پیش‌فرض محصول پس از فاز ۱۱۰
string FmtDate(DateTime d, string fmt) => fmt switch
{
    "linux" => ((DateTimeOffset)new DateTime(d.Year, d.Month, d.Day, 0, 0, 0)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
    "jalali" => FmtJalaliDate(d),
    "gregorian" => d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
    "hijri" => Jalali.FmtHijri(d),
    _ => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
};
string FmtStamp(DateTime d, string fmt) => fmt switch
{
    "linux" => ((DateTimeOffset)d).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
    "hhmm" => d.ToString("HH:mm", CultureInfo.InvariantCulture),
    "hhmmss" => d.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
    "jalali" => FmtJalaliDate(d) + " " + d.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
    "gregorian" => d.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture),
    "hijri" => Jalali.FmtHijri(d) + " " + d.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
    _ => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
};
string FmtJalaliDate(DateTime d)
{
    var (y, m, dd) = Jalali.ToJalali(d);
    return $"{y:0000}/{m:00}/{dd:00}";
}
var sample = new DateTime(2027, 1, 24, 8, 3, 22); // یکشنبه ۴ بهمن ۱۴۰۵ — ساعت ۰۸:۰۳:۲۲
Console.WriteLine($"نمونه: {sample:yyyy-MM-dd HH:mm:ss} (local) — Jalali {FmtJalaliDate(sample)}, Hijri {Jalali.FmtHijri(sample)}");
foreach (var f in new[] { "iso", "linux", "jalali", "gregorian", "hijri" })
    Console.WriteLine($"  date[{f}] = {FmtDate(sample.Date, f)}");
foreach (var f in new[] { "hhmm", "hhmmss", "linux", "jalali", "gregorian", "hijri" })
    Console.WriteLine($"  stamp[{f}] = {FmtStamp(sample, f)}");
var epoch = ((DateTimeOffset)sample).ToUnixTimeSeconds();
var back = DateTimeOffset.FromUnixTimeSeconds(epoch).ToLocalTime();
Console.WriteLine($"  گردش linux: {epoch} -> {back:yyyy-MM-dd HH:mm:ss} roundtrip={(back == sample ? "OK" : "FAIL")}");
