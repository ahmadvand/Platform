using HrAttendance.Web.Services;

// تست پارسر کانونیال پلاک (فاز ۵۱) — همهٔ قالب‌های خام واقعی
// خروجی موردانتظار: (دورقم راست، حرف، سه‌رقم چپ، استان) — باگ «ایران ن11» نباید تکرار شود
var cases = new (string Input, string? L, string? Letter, string? R, string? Prov)[]
{
    // باگ گزارش کاربر: حرف «ن» به اشتباه کنار کد استان می‌رفت
    ("36 ن 436 ایران 11",      "36", "ن", "436", "11"),
    ("36ن436ایران11",          "36", "ن", "436", "11"),
    ("36 436 ایران ن 11",      "36", "ن", "436", "11"),   // حرف کنار استان — سناریوی باگ
    ("ایران 11 36 ن 436",      "36", "ن", "436", "11"),
    // ارقام فارسی
    ("۶۳ س ۹۹۹ ایران ۸۷",      "63", "س", "999", "87"),
    ("63س999 ایران 87",        "63", "س", "999", "87"),
    // دوحرفی/بدون استان
    ("12 ب 345 ایران 22",      "12", "ب", "345", "22"),
    ("36 ن 436",               "36", "ن", "436", ""),
    // حرف در انتها
    ("36 436 ن ایران 11",      "36", "ن", "436", "11"),
    // حروف لاتین (پلاک‌خوان انگلیسی)
    ("36 N 436 Iran 11",       null, null, null, null), // «ایران» فارسی نیست → ساختار تشخیص نمی‌شود (رفتار قبلی حفظ)
};

var fail = 0;
foreach (var (input, L, letter, R, prov) in cases)
{
    var p = PlateFmt.Parse(input);
    var ok = (L is null && p is null) ||
             (p is not null && p.Left == L && p.Letter == letter && p.Right == R && p.Province == prov);
    if (!ok) fail++;
    Console.WriteLine((ok ? "  OK   " : "  FAIL ") + "\"" + input + "\"  →  " +
        (p is null ? "null" : $"({p.Left} {p.Letter} {p.Right} | ایران {p.Province})"));
}

// کلید کانونیال — سازگاری با PlateKeyOf قبلی برای قالب استاندارد
var k1 = PlateFmt.CanonicalKey("36 ن 436 ایران 11");
var k2 = PlateFmt.CanonicalKey("36ن436ایران11");
var k3 = PlateFmt.CanonicalKey("36 436 ایران ن 11");
Console.WriteLine("  کلید یکسان از هر سه قالب: " + k1 + " / " + k2 + " / " + k3);
if (k1 != k2 || k1 != k3) fail++;

Console.WriteLine(fail == 0 ? "همهٔ چک‌های پلاک سبز ✓" : fail + " چک رد شد ✗");
return fail == 0 ? 0 : 1;
