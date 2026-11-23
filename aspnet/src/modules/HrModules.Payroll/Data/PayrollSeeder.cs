using Platform.Web.Services;
using HrModules.Payroll.Domain;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Payroll.Data;

// ======================================================================
// فاز ۶۲ — سیدِ داده‌های اولیهٔ ماژول حقوق و دستمزد (IModuleSeeder)
// ----------------------------------------------------------------------
// پیکربندی حقوق هر شرکت (اقلام فرمول‌محور + پارامترهای ۱۴۰۴/۱۴۰۵ + تنظیمات روش محاسبه)
// + لیست حقوق نمونهٔ ماه جاری از موتور داینامیک — در هر بوت و به‌صورت خودترمیم
// (بر اساس شرکت/کد/سال).
// شیفت‌ها (دادهٔ ماژول حضور) فقط از طریق قرارداد IAttendanceGateway خوانده می‌شوند —
// اقلام شیفت‌محور (فوق‌العاده نوبت‌کاری) با AttShiftInfo. کارکرد موتور نیز از همان
// قرارداد (FetchWorkDataAsync) تأمین می‌شود. هیچ ارجاع مستقیم به ماژول دیگر.
// ساختار سازمانی و دادهٔ تردد شرکت نمایشی دوم «آریا سازه» در AttendanceSeeder
// (ماژول حضور) ساخته می‌شود و این سید فقط پیکربندی حقوقش را می‌ریزد.
// ======================================================================
public sealed class PayrollSeeder : IModuleSeeder
{
    public string ModuleCode => "payroll";
    public int Order => 30;

    public async Task SeedAsync(IServiceProvider scopedServices, bool demoData, CancellationToken ct = default)
    {
        var db = scopedServices.GetRequiredService<PayrollDbContext>();
        var att = scopedServices.GetRequiredService<IAttendanceGateway>();

        var nowJ = Jalali.ToJalali(DateTime.Now);
        var companies = await db.Companies.OrderBy(c => c.Code).ToListAsync(ct);
        // فاز ۶۲ — شیفت‌ها فقط از طریق قرارداد ماژول حضور (CompanyId اضافه‌شده به AttShiftInfo)
        var allShifts = await att.AllShiftsAsync();

        foreach (var company in companies)
        {
            // ---------- تنظیمات محاسبه ----------
            if (!await db.PayrollSettings.AnyAsync(s => s.CompanyId == company.Id, ct))
            {
                db.PayrollSettings.Add(new PayrollSetting
                {
                    CompanyId = company.Id,
                    CalcMethod = company.Code == "ASC-02" ? "کارکردی" : "ماهانه",
                    WorkSource = 2,
                });
            }

            // ---------- اقلام سیستمی (خودترمیم به تفکیک کد) ----------
            var shifts = allShifts.Where(s => s.CompanyId == company.Id || s.CompanyId == null)
                .OrderBy(s => s.Name).ToList();
            var nightRates = new (string Key, string Param)[]
            {
                ("صبح", "نرخ_نوبت_صبح"), ("عصر", "نرخ_نوبت_عصر"), ("شب", "نرخ_نوبت_شب"),
            };
            void AddItem(string code, string title, string kind, string formula,
                bool taxable, bool insurable, bool continuous, int order,
                string scopeType = "همه", string? scopeId = null, bool isSystem = true, bool active = true)
            {
                db.PayItems.Add(new PayItem
                {
                    CompanyId = company.Id, Code = code, Title = title, Kind = kind, Formula = formula,
                    Taxable = taxable, Insurable = insurable, Continuous = continuous,
                    ScopeType = scopeType, ScopeId = scopeId, Order = order, IsSystem = isSystem, Active = active,
                });
            }
            var existingItems = await db.PayItems.Where(i => i.CompanyId == company.Id).ToListAsync(ct);
            bool HasItem(string code, string? scopeId = null) =>
                existingItems.Any(i => i.Code == code && (scopeId == null || i.ScopeId == scopeId));
            if (!HasItem("پایه")) AddItem("پایه", "حقوق پایه", "مزیت", "پایه_محاسبه", true, true, true, 10);
            if (!HasItem("اضافه_کاری")) AddItem("اضافه_کاری", "اضافه‌کاری (۴۰٪)", "مزیت", "دقیقه_اضافه_کاری / 60 * ساعتی * ضریب_اضافه_کاری", true, true, false, 20);
            if (!HasItem("حق_مسکن")) AddItem("حق_مسکن", "حق مسکن", "مزیت", "if(کارکرد_روز > 0, حق_مسکن, 0)", true, true, true, 30);
            if (!HasItem("بن_کارگری")) AddItem("بن_کارگری", "بن کارگری (خواروبار)", "مزیت", "if(کارکرد_روز > 0, بن_کارگری, 0)", true, true, true, 40);
            if (!HasItem("حق_اولاد")) AddItem("حق_اولاد", "حق اولاد (تا ۲ فرزند)", "مزیت", "min(تعداد_فرزند, 2) * حق_اولاد", true, true, true, 50);
            if (!HasItem("حق_تأهل")) AddItem("حق_تأهل", "حق تأهل (عائله‌مندی)", "مزیت", "if(متأهل == 1, حق_تأهل_ماهانه, 0)", true, true, true, 60);
            // فاز ۲۸ — رفع باگ دیتابیس تازه: کد قلم به‌ازای هر شرکت یکتاست (UNIQUE CompanyId+Code)؛
            // فقط «اولین» شیفت نوبت‌کاریِ دارای نرخ مطابق، قلم شیفت‌محور می‌گیرد و شیفت‌های بی‌نرخ رد می‌شوند.
            if (!HasItem("فوق_العاده_نوبت_کاری"))
            {
                var nightShift = shifts.FirstOrDefault(x => x.Type == "نوبت‌کاری"
                    && nightRates.Any(n => !string.IsNullOrEmpty(n.Param) && x.Name.Contains(n.Key)));
                if (nightShift is not null)
                {
                    var rate = nightRates.First(n => nightShift.Name.Contains(n.Key)).Param;
                    AddItem("فوق_العاده_نوبت_کاری", $"فوق‌العاده نوبت‌کاری — {nightShift.Name}", "مزیت",
                        $"پایه_محاسبه * {rate} / 100", true, true, true, 70, "شیفت", nightShift.Id, false);
                }
            }
            if (!HasItem("فوق_العاده_شب_کاری")) AddItem("فوق_العاده_شب_کاری", "فوق‌العاده شب‌کاری (۲۲–۶)", "مزیت",
                "دقیقه_شب_کاری / 60 * ساعتی * نرخ_شب_کاری / 100", true, true, true, 80);
            if (!HasItem("پایه_سنوات")) AddItem("پایه_سنوات", "پایه سنوات (با یک سال سابقه)", "مزیت",
                "if(سال_خدمت >= 1, پایه_سنوات_روزانه * 30, 0)", true, true, true, 90);
            // اقلام سالانه عیدی/سنوات — غیرفعال در فیش ماهانه؛ صفحه «عیدی و تسویه» با متغیر کارکرد_از_سال_روز ارزیابی می‌کند
            if (!HasItem("عیدی_سالانه")) AddItem("عیدی_سالانه", "عیدی سالانه (۲ برابر پایه تا سقف ۳ برابر حداقل)", "مزیت",
                "round(min(پایه_مبنا * 2, حداقل_ماهانه * 3) * کارکرد_از_سال_روز / 365)", true, false, true, 15, active: false);
            if (!HasItem("سنوات_سالانه")) AddItem("سنوات_سالانه", "پایه سنوات سالانه (نسبت کارکرد)", "مزیت",
                "round(پایه_مبنا * کارکرد_از_سال_روز / 365)", true, false, true, 95, active: false);
            if (!HasItem("کسور_غیبت")) AddItem("کسور_غیبت", "کسور غیبت و بدون‌حقوق", "کسور", "(غیبت_روز + مرخصی_بدون_حقوق_روز) * روزانه", false, false, true, 100);
            if (!HasItem("بیمه_سهم_کارگر")) AddItem("بیمه_سهم_کارگر", "بیمه سهم کارگر (۷٪)", "کسور",
                "min(درآمد_مشمول_بیمه, سقف_بیمه_روزانه * ماه_روزها) * نرخ_بیمه_کارگر / 100", false, false, true, 110);
            if (!HasItem("مالیات_حقوق")) AddItem("مالیات_حقوق", "مالیات حقوق (پلکانی)", "کسور",
                "مالیات(max(0, درآمد_مشمول_مالیات - معافیت_مالیات_ماهانه))", false, false, true, 120);
            if (!HasItem("مالیات_مقطوع")) AddItem("مالیات_مقطوع", "مالیات مقطوع درآمد غیرمستمر (۱۰٪)", "کسور",
                "جمع_غیرمستمر_مشمول * نرخ_مقطوع / 100", false, false, true, 130);

            // خودترمیم: اقلام شیفت‌محورِ جامانده از بوت‌های ناتمام که شیفتشان متعلق به این شرکت نیست
            var orphanItems = await db.PayItems
                .Where(i => i.CompanyId == company.Id && i.ScopeType == "شیفت" && i.ScopeId != null)
                .ToListAsync(ct);
            foreach (var oi in orphanItems.Where(oi => shifts.All(s => s.Id != oi.ScopeId)))
                db.PayItems.Remove(oi);
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);

            // ---------- پارامترهای سالانه ----------
            foreach (var (jy, minDaily, minMonthly, minHourly, maskan, bon, olad, taahol, seniat, exempt, brackets) in new[]
            {
                (1405, 5_541_850m, 166_255_500m, 756_050m, 30_000_000m, 22_000_000m, 16_625_550m, 5_000_000m,
                    166_667m, 400_000_000m,
                    "[{\"to\":800000000,\"rate\":10},{\"to\":1000000000,\"rate\":15},{\"to\":1200000000,\"rate\":20},{\"to\":1400000000,\"rate\":25},{\"to\":0,\"rate\":30}]"),
                (1404, 3_476_667m, 104_300_000m, 434_583m, 90_000_000m, 14_000_000m, 10_430_000m, 3_000_000m,
                    100_000m, 240_000_000m,
                    "[{\"to\":450000000,\"rate\":10},{\"to\":600000000,\"rate\":15},{\"to\":700000000,\"rate\":20},{\"to\":800000000,\"rate\":25},{\"to\":0,\"rate\":30}]"),
            })
            {
                var existing = await db.PayrollParameters
                    .Where(p => p.CompanyId == company.Id && p.JYear == jy)
                    .Select(p => p.Code).ToListAsync(ct);
                void AddParam(string code, string title, decimal value, string? str = null)
                {
                    if (existing.Contains(code)) return;
                    db.PayrollParameters.Add(new PayrollParameter
                    {
                        CompanyId = company.Id, JYear = jy, Code = code, Title = title, Value = value, StrValue = str,
                    });
                }
                AddParam("حداقل_روزانه", "حداقل دستمزد روزانه", minDaily);
                AddParam("حداقل_ماهانه", "حداقل دستمزد ماهانه (۳۰ روز)", minMonthly);
                AddParam("حداقل_ساعتی", "حداقل دستمزد ساعتی", minHourly);
                AddParam("حق_مسکن", "حق مسکن ماهانه", maskan);
                AddParam("بن_کارگری", "بن کارگری (کمک‌هزینه خواروبار)", bon);
                AddParam("حق_اولاد", "حق اولاد هر فرزند", olad);
                AddParam("حق_تأهل_ماهانه", "حق تأهل (عائله‌مندی) ماهانه", taahol);
                AddParam("پایه_سنوات_روزانه", "پایه سنوات روزانه", seniat);
                AddParam("ضریب_اضافه_کاری", "ضریب قانونی اضافه‌کاری", 1.4m);
                AddParam("نرخ_نوبت_صبح", "نرخ فوق‌العاده نوبت صبح (٪)", 10m);
                AddParam("نرخ_نوبت_عصر", "نرخ فوق‌العاده نوبت عصر (٪)", 15m);
                AddParam("نرخ_نوبت_شب", "نرخ فوق‌العاده نوبت شب (٪)", 22.5m);
                AddParam("نرخ_شب_کاری", "نرخ فوق‌العاده شب‌کاری ۲۲–۶ (٪)", 35m);
                AddParam("نرخ_بیمه_کارگر", "سهم بیمه کارگر (٪)", 7m);
                AddParam("نرخ_بیمه_کارفرما", "سهم بیمه کارفرما (٪)", 23m);
                AddParam("سقف_بیمه_روزانه", "سقف حقوق مشمول بیمه روزانه", Math.Round(minDaily * 7m));
                AddParam("معافیت_مالیات_ماهانه", "معافیت ماهانه مالیات حقوق", exempt);
                AddParam("نرخ_مقطوع", "نرخ مالیات مقطوع غیرمستمر (٪)", 10m);
                AddParam("پلکان_مالیات", "پلکان مالیات حقوق — پله‌های [سقف، نرخ]", 0m, brackets);
            }
            await db.SaveChangesAsync(ct);

            // ---------- لیست حقوق نمونه ماه جاری ----------
            // خودترمیم: لیست پیش‌نویس خالی (مثلاً از بوت ناتمام قبلی) حذف و بازساخت می‌شود
            var emptyRuns = await db.PayrollRuns
                .Include(r => r.Payslips)
                .Where(r => r.CompanyId == company.Id && r.Status == "پیش‌نویس" && r.EmployeeCount == 0)
                .ToListAsync(ct);
            if (emptyRuns.Count > 0)
            {
                foreach (var er in emptyRuns)
                {
                    db.Payslips.RemoveRange(er.Payslips);
                    db.PayrollRuns.Remove(er);
                }
                await db.SaveChangesAsync(ct);
            }
            if (!await db.PayrollRuns.AnyAsync(r => r.CompanyId == company.Id, ct))
            {
                try
                {
                    var (run, slips) = await PayrollEngine.CalculateCompanyAsync(
                        db, att, company, nowJ.Y, nowJ.M, "سیستم");
                    run.Note = "نمونه محاسبه‌شده توسط موتور داینامیک هنگام راه‌اندازی";
                    db.PayrollRuns.Add(run);
                    db.Payslips.AddRange(slips);
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DbInit] لیست نمونه حقوق «{company.Name}» ساخته نشد: {ex.Message}");
                }
            }
        }
    }
}
