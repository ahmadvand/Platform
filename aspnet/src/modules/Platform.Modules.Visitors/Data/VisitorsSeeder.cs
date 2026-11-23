using Platform.Modules.Visitors.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Modules.Visitors.Data;

// ======================================================================
// فاز ۶۵ — سیدِ داده‌های اولیهٔ ماژول ارباب رجوع (IModuleSeeder)
// ----------------------------------------------------------------------
// خودترمیم و مستقل (در هر بوت بررسی می‌شود): پیمانکاران + بازدیدکنندگان
// (سناریوی نمایشی فاز VIS-01/02/04/07 — وضعیت‌های «در انتظار تأیید/تأیید شده/
// داخل سازمان/خارج شده/رد شده» + کارت بازدید + لاگ ورود/خروج).
// همهٔ داده‌ها روی VisitorsDbContext همین ماژول — بدون ارجاع به ماژول دیگر.
// هشدارهای امنیتی متنی مرتبط با ارباب رجوع (SecurityAlert = دادهٔ پایه/تردد)
// در سیدر هاست (نقش ترکیب) باقی مانده‌اند.
// جریان تصادفی قطعی مستقل (Mulberry32(5105)).
// ======================================================================
public sealed class VisitorsSeeder : IModuleSeeder
{
    public string ModuleCode => "visitors";
    public int Order => 50;

    private static Func<double> _rand = Mulberry32(5105);
    private static double Rand() => _rand();
    private static int Rint(int a, int b) => a + (int)(Rand() * (b - a + 1));
    private static T Pick<T>(params T[] items) => items[Rint(0, items.Length - 1)];

    public async Task SeedAsync(IServiceProvider scopedServices, bool demoData, CancellationToken ct = default)
    {
        var db = scopedServices.GetRequiredService<VisitorsDbContext>();
        if (await db.Visitors.AnyAsync(ct)) return;

        var today = DateTime.Now.Date;

        // ---------- پیمانکاران (VIS-03) ----------
        var c1 = new Contractor { Name = "شرکت صنعت‌گستر بوشهر", Code = "CTR-101", ContactName = "عبدالرضا جدی", Phone = "09171112233", ContractNo = "CN-1404-88", ValidUntil = today.AddDays(45) };
        var c2 = new Contractor { Name = "شرکت ایمن‌سازان پارس", Code = "CTR-102", ContactName = "مجید خوش‌نیت", Phone = "09122223344", ContractNo = "CN-1404-91", ValidUntil = today.AddDays(12) };
        var c3 = new Contractor { Name = "شرکت نگین تعمیرات صنعتی", Code = "CTR-103", ContactName = "قادر ملکی", Phone = "09133334455", ContractNo = "CN-1403-77", ValidUntil = today.AddDays(-8) };
        db.Contractors.AddRange(c1, c2, c3);
        await db.SaveChangesAsync(ct);

        // ---------- بازدیدکنندگان (VIS-01/02/04/07) ----------
        async Task<Visitor> MkVisitorAsync((string First, string Last, string Type, string Host, string HostDept,
            string Purpose, string Status, string? ContractorId, string? ApprovedBy, bool Card,
            bool Entered, bool Exited, int HoursIn, string? Doc) o)
        {
            var v = new Visitor
            {
                FirstName = o.First, LastName = o.Last,
                NationalId = Rint(1_000_000_000, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Phone = $"091{Rint(0, 9)}{Rint(1000000, 9999999)}",
                IdDocType = o.Doc ?? "کارت ملی",
                Type = o.Type, HostName = o.Host, HostDept = o.HostDept,
                Purpose = o.Purpose, Status = o.Status,
                ContractorId = o.ContractorId, ApprovedBy = o.ApprovedBy,
                RequestedAt = DateTime.Now.AddMinutes(-Rint(1, 300)),
            };
            db.Visitors.Add(v);
            await db.SaveChangesAsync(ct);
            if (o.Card && (o.Status == "داخل سازمان" || o.Status == "خارج شده"))
            {
                db.VisitorCards.Add(new VisitorCard
                {
                    CardNo = $"VC-{Rint(10000, 99999)}",
                    Type = Pick("QR", "بارکد", "RFID"),
                    VisitorId = v.Id,
                    ValidUntil = DateTime.Now.AddHours(8),
                    Returned = o.Status == "خارج شده",
                });
                await db.SaveChangesAsync(ct);
            }
            if (o.Entered)
            {
                db.VisitLogs.Add(new VisitLog { VisitorId = v.Id, Direction = "ورود", GateName = "گیت اصلی ورودی", Timestamp = DateTime.Now.AddHours(-o.HoursIn) });
                await db.SaveChangesAsync(ct);
            }
            if (o.Exited)
            {
                db.VisitLogs.Add(new VisitLog { VisitorId = v.Id, Direction = "خروج", GateName = "گیت اصلی خروجی", Timestamp = DateTime.Now.AddHours(-o.HoursIn * 0.5) });
                await db.SaveChangesAsync(ct);
            }
            return v;
        }

        await MkVisitorAsync(("اسماعیل", "توکلی", "مراجع", "سعید قاسمی", "منابع انسانی", "پیگیری پرونده بیمه", "در انتظار تأیید", null, null, false, false, false, 0, null));
        await MkVisitorAsync(("هدیه", "اسدی", "مهمان", "مریم تهرانی", "منابع انسانی", "جلسه همکاری آموزشی", "تأیید شده", null, "حسین بهرامی", false, false, false, 0, null));
        await MkVisitorAsync(("کوروش", "جهانگیری", "پیمانکار", "فرهاد سلطانی", "تولید و بهره‌برداری", "سرویس کمپرسور C-201", "داخل سازمان", c1.Id, "حسین بهرامی", true, true, false, 3, null));
        await MkVisitorAsync(("ژیار", "مرادی", "پیمانکار", "فرهاد سلطانی", "تولید و بهره‌برداری", "آچاربند خط لوله", "داخل سازمان", c2.Id, "حسین بهرامی", true, true, false, 2, null));
        await MkVisitorAsync(("نرگس", "خسروی", "مهمان", "مسعود اکبری", "مالی و اداری", "ممیزی حسابداری", "خارج شده", null, "حسین بهرامی", true, true, true, 6, null));
        await MkVisitorAsync(("بهمن", "صالحی", "مراجع", "ناصر زارعی", "فنی و مهندسی", "تحویل نمونه آزمایش", "خارج شده", null, "حسین بهرامی", true, true, true, 5, null));
        await MkVisitorAsync(("روح‌الله", "بدری", "پیمانکار", "بهزاد شریفی", "فنی و مهندسی", "تعمیر فن کلین‌تاور", "تأیید شده", c3.Id, "حسین بهرامی", false, false, false, 0, null));
        await MkVisitorAsync(("شهاب", "لطفی", "مراجع", "سعید قاسمی", "منابع انسانی", "بدون هماهنگی قبلی", "رد شده", null, null, false, false, false, 0, null));
    }

    private static Func<double> Mulberry32(int seed)
    {
        uint a = (uint)seed;
        return () =>
        {
            a += 0x6D2B79F5;
            uint t = a;
            t = (t ^ (t >> 15)) * (1u | t);
            t ^= t + ((t ^ (t >> 7)) * (61u | t));
            return (t ^ (t >> 14)) / 4294967296.0;
        };
    }
}
