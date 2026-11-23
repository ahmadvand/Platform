using System.Globalization;
using System.Text.Json;
using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Services;
using Platform.Contracts;
using HrModules.Payroll.Domain;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Payroll.Data;

// ============================================================
// موتور حقوق و دستمزد داینامیک چندشرکتی (فاز ۲۴)
// هر شرکت: اقلام فرمول‌محور (PayItem) + پارامترهای سالانه + تنظیمات روش محاسبه.
// منبع کارکرد پارامتری است (پیش‌فرض اولویت ۲ = روزشمار با وضعیت‌های تأییدشده).
// مبالغ همگی ریال.
// ============================================================

/// <summary>مقادیر تجمیع‌شده کارکرد یک پرسنل در یک بازه — خروجی لایه تردد.</summary>
public sealed class WorkAgg
{
    public decimal WorkDays;      // روزهای حضور واقعی (ورود ثبت‌شده)
    public decimal AbsentDays;    // غیبت غیرموجه
    public decimal PaidLeaveDays; // مرخصی با حقوق (استحقاقی/ساعتی/زایمان)
    public decimal SickDays;      // استعلاجی
    public decimal UnpaidDays;    // مرخصی بدون حقوق
    public decimal MissionDays;   // مأموریت تأییدشده
    public decimal HolidayDays;   // جمعه/تعطیل رسمی/روز استراحت چرخشی
    public int WorkMin, OtMin, LateMin, EarlyMin, NightMin, PermitOtMin;

    /// <summary>روز پرداختی کامل — حضور + مأموریت + مرخصی باحقوق + استعلاجی.</summary>
    public decimal PayableDays => WorkDays + MissionDays + PaidLeaveDays + SickDays;
}

/// <summary>فاز ۱۳۵ (مورد ۱۵) — حق پارکینگ ماهانهٔ یک پرسنل از جداول مشترک ماژول پارکینگ.</summary>
public sealed record ParkFeeRow(string EmpId, decimal Fee);

/// <summary>پیکربندی محاسبه یک شرکت برای یک سال جلالی — تنظیمات + پارامترها + پلکان مالیات.</summary>
public sealed class PayCompanyConfig
{
    public required PayrollSetting Setting { get; init; }
    public required Dictionary<string, decimal> Params { get; init; }
    public List<(decimal UpTo, decimal Rate)> TaxBrackets { get; } = new();

    public static async Task<PayCompanyConfig> LoadAsync(PayrollDbContext db, string companyId, int jy)
    {
        var setting = await db.PayrollSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId)
            ?? new PayrollSetting { CompanyId = companyId };
        var rows = await db.PayrollParameters.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.JYear == jy).ToListAsync();
        var dict = rows.ToDictionary(p => p.Code, p => p.Value);

        var cfg = new PayCompanyConfig { Setting = setting, Params = dict };
        var pol = rows.FirstOrDefault(p => p.Code == "پلکان_مالیات");
        if (!string.IsNullOrWhiteSpace(pol?.StrValue))
        {
            try
            {
                using var doc = JsonDocument.Parse(pol.StrValue);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var to = el.TryGetProperty("to", out var t) ? t.GetDecimal() : 0m;
                    var rate = el.TryGetProperty("rate", out var r) ? r.GetDecimal() : 0m;
                    cfg.TaxBrackets.Add((to, rate));
                }
            }
            catch { /* پلکان نامعتبر — مالیات() خطای فارسی می‌دهد اگر خالی بماند */ }
        }
        return cfg;
    }
}

public static class PayrollEngine
{
    // ---------- ۱) تجمیع کارکرد شرکت ----------

    /// <summary>تجمیع کارکرد همه پرسنل برای یک بازه میلادی — سه منبع کارکرد:
    /// ۱=جمع فیلدهای آماده رکورد، ۲=روزشمار با وضعیت‌های تأییدشده (پیش‌فرض)، ۳=روزشمار + اضافه‌کاری فقط مجوز.</summary>
    public static async Task<Dictionary<string, WorkAgg>> AggregateCompanyAsync(
        IAttendanceGateway att, List<Employee> emps, DateTime start, DateTime endExclusive,
        int workSource, bool countAbsence = true)
    {
        var startKey = Jalali.DateKey(start);
        var endKey = Jalali.DateKey(endExclusive);
        var ids = emps.Select(e => e.Id).ToList();

        // فاز ۶۱ — دادهٔ کارکرد (رکورد/مرخصی/مأموریت/مجوز/تعطیلات/شیفت) از قرارداد ماژول حضور
        var data = ids.Count == 0
            ? new AttWorkData(new(), new(), new(), new(), new(), new())
            : await att.FetchWorkDataAsync(ids, startKey, endKey);
        var records = data.Records;
        var leaves = data.Leaves;
        var missions = data.Missions;
        var permits = data.Permits;
        var holidays = data.Holidays;

        var recByEmp = records.GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Date));
        var leavesByEmp = leaves.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var missionsByEmp = missions.GroupBy(m => m.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var permitsByEmp = permits.GroupBy(p => p.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<string, WorkAgg>();
        foreach (var emp in emps)
        {
            var agg = new WorkAgg();
            var shiftInfo = data.ShiftByEmployee.GetValueOrDefault(emp.Id);
            var shiftParams = shiftInfo is null ? null : new ShiftParams
            {
                StartTime = shiftInfo.StartTime, EndTime = shiftInfo.EndTime,
                BreakMinutes = shiftInfo.BreakMinutes, ToleranceLate = shiftInfo.ToleranceLate,
            };
            var cycle = shiftInfo?.CyclePattern;
            // لنگر الگوی چرخش: روز استخدام
            var anchor = Jalali.DateKey(emp.HireDate);

            var empLeaves = leavesByEmp.GetValueOrDefault(emp.Id) ?? new();
            var empMissions = missionsByEmp.GetValueOrDefault(emp.Id) ?? new();
            var empPermits = permitsByEmp.GetValueOrDefault(emp.Id) ?? new();

            // اضافه‌کاری مجوزدار — همیشه محاسبه می‌شود (منبع ۳ + متغیر مستقل فرمول)
            foreach (var p in empPermits)
            {
                if (string.CompareOrdinal(p.Date, startKey) < 0 || string.CompareOrdinal(p.Date, endKey) >= 0) continue;
                agg.PermitOtMin += PermitMinutes(p.FromTime, p.ToTime);
            }

            if (workSource == 1)
            {
                // منبع ۱ — سطح رکورد: جمع فیلدهای آماده هر رکورد
                foreach (var r in (recByEmp.GetValueOrDefault(emp.Id) ?? new()).Values)
                {
                    if (r.CheckIn != null) agg.WorkDays += 1;
                    agg.WorkMin += r.WorkMinutes;
                    agg.OtMin += r.OvertimeMinutes;
                    agg.LateMin += r.LateMinutes;
                    agg.EarlyMin += r.EarlyMinutes;
                    agg.NightMin += NightMinutes(r.CheckIn, r.CheckOut);
                    switch (r.Status)
                    {
                        case "غایب" when r.CheckIn == null: if (countAbsence) agg.AbsentDays += 1; break;
                        case "مرخصی" when r.CheckIn == null: agg.PaidLeaveDays += 1; break;
                        case "مأموریت" when r.CheckIn == null: agg.MissionDays += 1; break;
                        case "تعطیل" when r.CheckIn == null: agg.HolidayDays += 1; break;
                    }
                }
                FillGapsFromApprovals(emp, recByEmp.GetValueOrDefault(emp.Id) ?? new(), empLeaves, empMissions,
                    start, endExclusive, holidays, cycle, anchor, agg, countAbsence);
            }
            else
            {
                // منبع ۲ و ۳ — روزشمار با بازمحاسبه موتور حضور
                var recs = recByEmp.GetValueOrDefault(emp.Id) ?? new Dictionary<string, AttWorkRecord>();
                for (var d = start.Date; d < endExclusive; d = d.AddDays(1))
                {
                    var key = Jalali.DateKey(d);
                    var onDuty = cycle is null || AttCalc.IsOnDutyForCycle(key, cycle, anchor);

                    if (recs.TryGetValue(key, out var r))
                    {
                        if (r.CheckIn != null)
                        {
                            var wr = AttCalc.ComputeWork(shiftParams, r.CheckIn, r.CheckOut, key);
                            agg.WorkDays += 1;
                            agg.WorkMin += wr.WorkMinutes;
                            agg.LateMin += wr.LateMinutes;
                            agg.EarlyMin += wr.EarlyMinutes;
                            agg.NightMin += NightMinutes(r.CheckIn, r.CheckOut);
                            agg.OtMin += wr.OvertimeMinutes;
                        }
                        else
                        {
                            switch (r.Status)
                            {
                                case "غایب": if (countAbsence) agg.AbsentDays += 1; break;
                                case "مرخصی": agg.PaidLeaveDays += 1; break;
                                case "مأموریت": agg.MissionDays += 1; break;
                                case "تعطیل": agg.HolidayDays += 1; break;
                                default: if (countAbsence) agg.AbsentDays += 1; break;
                            }
                        }
                        continue;
                    }

                    // بدون رکورد: اولویت مرخصی/مأموریت تأییدشده، بعد تعطیل، بعد غیبت
                    var (paid, sick, unpaid) = LeaveCover(empLeaves, key);
                    if (paid + sick + unpaid > 0)
                    {
                        agg.PaidLeaveDays += paid; agg.SickDays += sick; agg.UnpaidDays += unpaid;
                        continue;
                    }
                    if (MissionCover(empMissions, key)) { agg.MissionDays += 1; continue; }
                    if (IsRestDay(d, holidays, onDuty)) { agg.HolidayDays += 1; continue; }
                    if (countAbsence) agg.AbsentDays += 1;
                }
            }

            result[emp.Id] = agg;
        }
        return result;
    }

    /// <summary>پر کردن روزهای بدون رکورد از مرخصی/مأموریت تأییدشده — فقط برای منبع ۱.</summary>
    private static void FillGapsFromApprovals(Employee emp, Dictionary<string, AttWorkRecord> recs,
        List<AttApproval> leaves, List<AttApproval> missions, DateTime start, DateTime endExclusive,
        HashSet<string> holidays, string? cycle, string anchor, WorkAgg agg, bool countAbsence)
    {
        for (var d = start.Date; d < endExclusive; d = d.AddDays(1))
        {
            var key = Jalali.DateKey(d);
            if (recs.ContainsKey(key)) continue;
            var (paid, sick, unpaid) = LeaveCover(leaves, key);
            if (paid + sick + unpaid > 0)
            {
                agg.PaidLeaveDays += paid; agg.SickDays += sick; agg.UnpaidDays += unpaid;
                continue;
            }
            if (MissionCover(missions, key)) { agg.MissionDays += 1; continue; }
            var onDuty = cycle is null || AttCalc.IsOnDutyForCycle(key, cycle, anchor);
            if (IsRestDay(d, holidays, onDuty)) { agg.HolidayDays += 1; continue; }
            if (countAbsence) agg.AbsentDays += 1;
        }
    }

    private static bool IsRestDay(DateTime d, HashSet<string> holidays, bool onDuty) =>
        holidays.Contains(Jalali.DateKey(d)) || d.DayOfWeek == DayOfWeek.Friday || !onDuty;

    private static (decimal paid, decimal sick, decimal unpaid) LeaveCover(List<AttApproval> leaves, string key)
    {
        decimal paid = 0, sick = 0, unpaid = 0;
        foreach (var l in leaves)
        {
            if (string.IsNullOrEmpty(l.FromDate) || string.IsNullOrEmpty(l.ToDate)) continue;
            if (key.CompareTo(l.FromDate) < 0 || key.CompareTo(l.ToDate) > 0) continue;
            var f = l.Type == "ساعتی" && l.Hours is > 0 && l.Hours <= 8
                ? Math.Min(1m, l.Hours.Value / 8m) : 1m;
            switch (l.Type)
            {
                case "استعلاجی": sick += f; break;
                case "بدون‌حقوق": unpaid += f; break;
                default: paid += f; break; // استحقاقی | ساعتی | زایمان
            }
        }
        return (Math.Min(paid, 1m), Math.Min(sick, 1m), Math.Min(unpaid, 1m));
    }

    private static bool MissionCover(List<AttApproval> missions, string key) =>
        missions.Any(m => !string.IsNullOrEmpty(m.FromDate) && !string.IsNullOrEmpty(m.ToDate)
                          && key.CompareTo(m.FromDate) >= 0 && key.CompareTo(m.ToDate) <= 0);

    private static int PermitMinutes(string? from, string? to)
    {
        if (!TimeSpan.TryParse(from, CultureInfo.InvariantCulture, out var f)) return 0;
        if (!TimeSpan.TryParse(to, CultureInfo.InvariantCulture, out var t)) return 0;
        var dur = (int)(t - f).TotalMinutes;
        return dur <= 0 ? dur + 1440 : dur;
    }

    /// <summary>دقیقه هم‌پوشانی بازه تردد با پنجره شب‌کاری ۲۲–۶.</summary>
    private static int NightMinutes(DateTime? inT, DateTime? outT)
    {
        if (inT is null || outT is null || outT <= inT) return 0;
        var day = inT.Value.Date;
        return Overlap(inT.Value, outT.Value, day.AddDays(-1).AddHours(22), day.AddHours(6))
             + Overlap(inT.Value, outT.Value, day.AddHours(22), day.AddDays(1).AddHours(6));
    }

    private static int Overlap(DateTime s1, DateTime e1, DateTime s2, DateTime e2)
    {
        var s = s1 > s2 ? s1 : s2;
        var e = e1 < e2 ? e1 : e2;
        return e <= s ? 0 : (int)(e - s).TotalMinutes;
    }

    // ---------- ۲) متغیرهای زمینه فرمول ----------

    public static readonly (string Code, string Title)[] VarDocs =
    {
        ("پایه_مبنا", "حقوق پایه قرارداد پرسنل (ریال)"),
        ("پایه_محاسبه", "پایه پس از روش محاسبه — ماهانه ثابت یا کارکردی روزشمار"),
        ("روزانه", "پایه محاسبه ÷ روزهای ماه"),
        ("ساعتی", "پایه مبنا ÷ ساعت پایه ماه"),
        ("ماه_روزها", "مبنای روز ماه از تنظیمات (پیش‌فرض ۳۰)"),
        ("ساعت_پایه_ماه", "ساعت پایه ماه از تنظیمات (پیش‌فرض ۱۹۲)"),
        ("روش_ماهانه", "۱ اگر روش شرکت «ماهانه» باشد وگرنه ۰"),
        ("روش_کارکردی", "۱ اگر روش شرکت «کارکردی» باشد وگرنه ۰"),
        ("کارکرد_روز", "روزهای حضور واقعی"),
        ("پرداختی_روز", "حضور + مأموریت + مرخصی باحقوق + استعلاجی"),
        ("غیبت_روز", "غیبت غیرموجه"),
        ("مرخصی_روز", "مرخصی با حقوق"),
        ("استعلاجی_روز", "مرخصی استعلاجی"),
        ("مرخصی_بدون_حقوق_روز", "مرخصی بدون حقوق"),
        ("مأموریت_روز", "روزهای مأموریت تأییدشده"),
        ("تعطیل_روز", "جمعه + تعطیل رسمی + روز استراحت چرخشی"),
        ("دقیقه_کارکرد", "جمع دقیقه کارکرد خالص"),
        ("دقیقه_اضافه_کاری", "دقیقه اضافه‌کاری مطابق منبع کارکرد شرکت"),
        ("دقیقه_اضافه_کاری_رکورد", "دقیقه اضافه‌کاری از رکورد تردد"),
        ("دقیقه_اضافه_کاری_مجوز", "دقیقه اضافه‌کاری از مجوزهای تأییدشده"),
        ("دقیقه_تأخیر", "جمع دقیقه دیرکرد"),
        ("دقیقه_تعجیل", "جمع دقیقه خروج قبل از موعد"),
        ("دقیقه_شب_کاری", "دقیقه کار در بازه ۲۲ تا ۶"),
        ("کارکرد_از_سال_روز", "روزهای خدمت از ابتدای سال جلالی (مبنای عیدی)"),
        ("متأهل", "۱ متأهل / ۰ مجرد — از پرونده پرسنل"),
        ("تعداد_فرزند", "تعداد فرزندان — از پرونده پرسنل"),
        ("سال_خدمت", "سابقه خدمت به سال (یک اعشار)"),
        ("سن", "سن پرسنل (بر اساس تاریخ تولد)"),
        ("شیفت_نوبتکاری", "۱ اگر شیفت پرسنل «نوبت‌کاری» باشد"),
        ("شیفت_شبکاری", "۱ اگر شیفت از نیمه‌شب عبور کند"),
        ("شیفت_اقماری", "۱ اگر شیفت «اقماری» باشد"),
        ("شیفت_ساعات", "طول شیفت به ساعت"),
        ("جمع_مزایا", "جمع مزایای محاسبه‌شده تا این قلم"),
        ("جمع_کسور", "جمع کسور محاسبه‌شده تا این قلم"),
        ("درآمد_مشمول_مالیات", "جمع مزایای مستمر مشمول مالیات تا این قلم"),
        ("درآمد_مشمول_بیمه", "جمع مزایای مشمول بیمه تا این قلم"),
        ("جمع_غیرمستمر_مشمول", "جمع مزایای غیرمستمر مشمول (نرخ مقطوع) تا این قلم"),
    };

    public static readonly (string Name, string Sig, string Desc)[] FunctionDocs =
    {
        ("round", "round(مبلغ، اعشار؟)", "گرد کردن — round(x) تا ریال، round(x,2) تا دو اعشار"),
        ("min", "min(a, b, …)", "کمینه چند مقدار"),
        ("max", "max(a, b, …)", "بیشینه چند مقدار"),
        ("abs", "abs(x)", "قدرمطلق"),
        ("ceil", "ceil(x)", "گرد به بالا"),
        ("floor", "floor(x)", "گرد به پایین"),
        ("if", "if(شرط، آری، خیر)", "شرط — هر مقدار غیرصفر یعنی درست"),
        ("قلم", "قلم('کد')", "مبلغ قلم محاسبه‌شدهٔ قبلی همین فیش — ۰ اگر وجود نداشته باشد"),
        ("مالیات", "مالیات(مبلغ)", "مالیات پلکانی از پارامتر «پلکان_مالیات» شرکت"),
    };

    public static Dictionary<string, decimal> BuildVars(Employee emp, AttShiftInfo? shift, WorkAgg agg, PayCompanyConfig cfg,
        int jy, decimal workedSinceYear, decimal parkingFee = 0)
    {
        var s = cfg.Setting;
        var kar = s.CalcMethod == "کارکردی";
        var basePay = kar
            ? RoundTo(emp.BaseSalary / s.MonthDays * agg.PayableDays, 0)
            : emp.BaseSalary;

        var v = new Dictionary<string, decimal>
        {
            ["پایه_مبنا"] = emp.BaseSalary,
            ["پایه_محاسبه"] = basePay,
            ["روزانه"] = s.MonthDays > 0 ? RoundTo(basePay / s.MonthDays, 0) : 0,
            ["ساعتی"] = s.HourBase > 0 ? RoundTo(emp.BaseSalary / s.HourBase, 0) : 0,
            ["ماه_روزها"] = s.MonthDays,
            ["ساعت_پایه_ماه"] = s.HourBase,
            ["روش_ماهانه"] = kar ? 0 : 1,
            ["روش_کارکردی"] = kar ? 1 : 0,
            ["کارکرد_روز"] = agg.WorkDays,
            ["پرداختی_روز"] = agg.PayableDays,
            ["غیبت_روز"] = agg.AbsentDays,
            ["مرخصی_روز"] = agg.PaidLeaveDays,
            ["استعلاجی_روز"] = agg.SickDays,
            ["مرخصی_بدون_حقوق_روز"] = agg.UnpaidDays,
            ["مأموریت_روز"] = agg.MissionDays,
            ["تعطیل_روز"] = agg.HolidayDays,
            ["دقیقه_کارکرد"] = agg.WorkMin,
            ["دقیقه_اضافه_کاری"] = s.WorkSource == 3 ? agg.PermitOtMin : agg.OtMin,
            ["دقیقه_اضافه_کاری_رکورد"] = agg.OtMin,
            ["دقیقه_اضافه_کاری_مجوز"] = agg.PermitOtMin,
            ["دقیقه_تأخیر"] = agg.LateMin,
            ["دقیقه_تعجیل"] = agg.EarlyMin,
            ["دقیقه_شب_کاری"] = agg.NightMin,
            ["کارکرد_از_سال_روز"] = workedSinceYear,
            ["متأهل"] = emp.MaritalStatus == "متأهل" ? 1 : 0,
            ["تعداد_فرزند"] = emp.ChildCount,
            ["سال_خدمت"] = Math.Round((decimal)(DateTime.Now - emp.HireDate).TotalDays / 365.25m, 1),
            ["سن"] = AgeOf(emp.BirthDate),
            ["شیفت_نوبتکاری"] = shift?.Type == "نوبت‌کاری" ? 1 : 0,
            ["شیفت_اقماری"] = shift?.Type == "اقماری" ? 1 : 0,
            // فاز ۱۳۵ (مورد ۱۵ ROADMAP-ADDITIONS) — حق پارکینگ ماهانهٔ پرسنل
            // (جمع MonthlyFee مجوزهای فعال روی پارکینگ‌های با حق ماهانه) — در فرمول
            // قلمِ کسور قابل استفاده است؛ مثال: قلمِ کسور «پارکینگ» با فرمول پارکینگ_مبلغ
            ["پارکینگ_مبلغ"] = parkingFee,
            ["پارکینگ_دارد"] = parkingFee > 0 ? 1 : 0,
        };

        // شیفت شب: پایان ≤ شروع (عبور از نیمه‌شب)
        if (shift != null)
        {
            var from = HmOf(shift.StartTime);
            var to = HmOf(shift.EndTime);
            var len = to > from ? to - from : to + 1440 - from;
            v["شیفت_شبکاری"] = to <= from ? 1 : 0;
            v["شیفت_ساعات"] = Math.Round(len / 60m, 2);
        }
        else
        {
            v["شیفت_شبکاری"] = 0;
            v["شیفت_ساعات"] = 0;
        }

        foreach (var p in cfg.Params) v[p.Key] = p.Value;
        return v;
    }

    private static decimal AgeOf(string? birth)
    {
        if (string.IsNullOrWhiteSpace(birth)) return 0;
        var p = birth.Split('/');
        if (p.Length < 3 || !int.TryParse(p[0], out var by)) return 0;
        var (ny, nm, nd) = Jalali.ToJalali(DateTime.Now);
        if (!int.TryParse(p[1], out var bm) || !int.TryParse(p[2], out var bd)) return ny - by;
        var age = ny - by;
        if (nm < bm || (nm == bm && nd < bd)) age--;
        return age;
    }

    private static int HmOf(string t)
    {
        var p = t.Split(':');
        return int.TryParse(p[0], out var h) && int.TryParse(p[1], out var m) ? h * 60 + m : 0;
    }

    private static decimal RoundTo(decimal x, int digits) =>
        Math.Round(x, digits, MidpointRounding.AwayFromZero);

    // ---------- ۳) حل هدف‌گیری اقلام ----------

    /// <summary>اقلام فعالِ متناظر با پرسنل — برای هر کد، هدف اختصاصی‌تر با اولویت بالاتر برنده است.</summary>
    public static List<PayItem> ResolveItems(List<PayItem> items, Employee emp)
    {
        static int Spec(PayItem it) => it.ScopeType switch
        {
            "پرسنل" => 5, "شیفت" => 4, "پست" => 3, "دپارتمان" => 2, "استخدام" => 2, _ => 0,
        };
        bool Match(PayItem it) => it.ScopeType switch
        {
            "دپارتمان" => it.ScopeId == emp.DepartmentId,
            "پست" => it.ScopeId == emp.PositionId,
            "شیفت" => it.ScopeId == emp.ShiftId,
            "پرسنل" => it.ScopeId == emp.Id,
            "استخدام" => it.ScopeId == emp.EmploymentType,
            _ => true,
        };
        return items.Where(i => i.Active && Match(i))
            .GroupBy(i => i.Code)
            .Select(g => g.OrderByDescending(Spec).ThenByDescending(i => i.Priority)
                          .ThenBy(i => i.Order).First())
            .OrderBy(i => i.Order).ThenBy(i => i.Code)
            .ToList();
    }

    // ---------- ۴) محاسبه خطوط فیش ----------

    public static decimal RoundAmount(decimal x, int unit) =>
        unit <= 0 ? Math.Round(x, 0, MidpointRounding.AwayFromZero)
                  : Math.Round(x / unit, MidpointRounding.AwayFromZero) * unit;

    public static (List<PayslipLine> Lines, decimal Gross, decimal Deduct, Dictionary<string, decimal> Map)
        ComputeLines(List<PayItem> items, Dictionary<string, decimal> vars, PayCompanyConfig cfg)
    {
        var lines = new List<PayslipLine>();
        var map = new Dictionary<string, decimal>();
        decimal gross = 0, deduct = 0, taxable = 0, insurable = 0, nonContinuous = 0;
        var order = 0;
        // ابتدا همه مزایا (تا بیمه/مالیات جمع کامل را ببینند) سپس کسور
        foreach (var kind in new[] { "مزیت", "کسور" })
        {
            foreach (var it in items.Where(i => i.Kind == kind))
            {
                var running = new Dictionary<string, decimal>(vars)
                {
                    ["جمع_مزایا"] = gross,
                    ["جمع_کسور"] = deduct,
                    ["درآمد_مشمول_مالیات"] = taxable,
                    ["درآمد_مشمول_بیمه"] = insurable,
                    ["جمع_غیرمستمر_مشمول"] = nonContinuous,
                };
                decimal amount;
                try
                {
                    amount = PayrollFormula.Evaluate(it.Formula, new PayFormulaContext
                    {
                        Vars = running,
                        Lines = map,
                        TaxBrackets = cfg.TaxBrackets,
                    });
                }
                catch (PayFormulaException ex)
                {
                    throw new PayFormulaException($"قلم «{it.Title}» ({it.Code}): {ex.Message}");
                }
                amount = Math.Max(0, RoundAmount(amount, cfg.Setting.RoundUnit));
                lines.Add(new PayslipLine
                {
                    Code = it.Code, Title = it.Title, Kind = kind, Amount = amount,
                    Taxable = it.Taxable, Insurable = it.Insurable, Order = order++,
                });
                map[it.Code] = amount;
                if (kind == "مزیت")
                {
                    gross += amount;
                    if (it.Taxable)
                    {
                        if (it.Continuous) taxable += amount;
                        else nonContinuous += amount;
                    }
                    if (it.Insurable) insurable += amount;
                }
                else deduct += amount;
            }
        }
        return (lines, gross, deduct, map);
    }

    // ---------- ۵) محاسبه لیست حقوق یک شرکت ----------

    public static async Task<(PayrollRun Run, List<Payslip> Slips)> CalculateCompanyAsync(
        PayrollDbContext db, IAttendanceGateway att, Company company, int jy, int jm, string createdBy)
    {
        var cfg = await PayCompanyConfig.LoadAsync(db, company.Id, jy);
        var (start, end) = Jalali.MonthRange(jy, jm);

        var emps = await db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == company.Id && e.Status == "فعال")
            .ToListAsync();
        var shifts = await att.ShiftsByEmployeeAsync(emps.Select(e => e.Id).ToList());
        AttShiftInfo? shiftOf(string id) => shifts.GetValueOrDefault(id);

        var aggs = await AggregateCompanyAsync(att, emps, start, end, cfg.Setting.WorkSource, true);
        // روزهای خدمت از ابتدای سال جلالی — مبنای عیدی/پایه سنوات (غیبت‌ها شمرده نمی‌شوند)
        var yearStart = Jalali.FromJalali(jy, 1, 1);
        var ytd = await AggregateCompanyAsync(att, emps, yearStart, end, cfg.Setting.WorkSource, false);

        var items = await db.PayItems.AsNoTracking()
            .Where(i => i.CompanyId == company.Id)
            .ToListAsync();
        var resolved = emps.ToDictionary(e => e.Id, e => ResolveItems(items, e));

        // فاز ۱۳۵ (مورد ۱۵ ROADMAP-ADDITIONS) — حق پارکینگ ماهانه به تفکیک پرسنل
        // (مجوز فعال روی پارکینگ‌های با MonthlyFee > 0 در بازهٔ ماه). جدول‌های
        // پارکینگ از همان پایگاه‌دادهٔ مشترک می‌آیند (بدون ارجاع کامپایل‌زمانی).
        var parkingByEmp = new Dictionary<string, decimal>();
        try
        {
            var parkRows = await db.Database.SqlQuery<ParkFeeRow>($@"
                SELECT v.EmployeeId AS EmpId, SUM(l.MonthlyFee) AS Fee
                FROM ParkingPermits pp
                JOIN Vehicles v ON pp.VehicleId = v.Id
                JOIN ParkingLots l ON pp.LotId = l.Id
                WHERE pp.Status = 'فعال' AND l.MonthlyFee > 0
                  AND pp.ValidFrom < {end} AND (pp.ValidUntil IS NULL OR pp.ValidUntil >= {start})
                GROUP BY v.EmployeeId").ToListAsync();
            parkingByEmp = parkRows.ToDictionary(r => r.EmpId, r => r.Fee);
        }
        catch { /* ماژول پارکینگ نصب نیست — متغیر صفر می‌ماند */ }

        var run = new PayrollRun
        {
            CompanyId = company.Id, JYear = jy, JMonth = jm,
            Status = "پیش‌نویس", CreatedBy = createdBy,
        };
        decimal tGross = 0, tNet = 0;
        var slips = new List<Payslip>();
        foreach (var emp in emps.OrderBy(e => e.PersonnelCode))
        {
            var agg = aggs.GetValueOrDefault(emp.Id, new WorkAgg());
            var yy = ytd.GetValueOrDefault(emp.Id, new WorkAgg());
            var vars = BuildVars(emp, shiftOf(emp.Id), agg, cfg, jy, yy.WorkDays + yy.MissionDays + yy.PaidLeaveDays + yy.SickDays,
                parkingByEmp.GetValueOrDefault(emp.Id));
            List<PayslipLine> lines;
            decimal gross, deduct;
            Dictionary<string, decimal> map;
            try
            {
                (lines, gross, deduct, map) = ComputeLines(resolved.GetValueOrDefault(emp.Id) ?? new(), vars, cfg);
            }
            catch (PayFormulaException ex)
            {
                throw new PayFormulaException(
                    $"{ex.Message} — پرسنل «{emp.FullName}» (کد {emp.PersonnelCode})");
            }

            var slip = new Payslip
            {
                EmployeeId = emp.Id,
                BaseSalary = vars.GetValueOrDefault("پایه_محاسبه"),
                OvertimePay = map.GetValueOrDefault("اضافه_کاری"),
                Allowances = Math.Max(0, gross - vars.GetValueOrDefault("پایه_محاسبه") - map.GetValueOrDefault("اضافه_کاری")),
                AbsentDeduct = lines.Where(l => l.Kind == "کسور" && l.Code.StartsWith("کسور")).Sum(l => l.Amount),
                Insurance = lines.Where(l => l.Kind == "کسور" && l.Code.StartsWith("بیمه")).Sum(l => l.Amount),
                Tax = lines.Where(l => l.Kind == "کسور" && l.Code.StartsWith("مالیات")).Sum(l => l.Amount),
                NetPay = gross - deduct,
                WorkedMin = agg.WorkMin,
                OvertimeMin = cfg.Setting.WorkSource == 3 ? agg.PermitOtMin : agg.OtMin,
                AbsentDays = (int)agg.AbsentDays,
                Lines = lines,
                Detail = JsonSerializer.Serialize(new
                {
                    engine = "dynamic",
                    method = cfg.Setting.CalcMethod,
                    workSource = cfg.Setting.WorkSource,
                    monthDays = cfg.Setting.MonthDays,
                    hourBase = cfg.Setting.HourBase,
                    lines = lines.Count,
                }),
            };
            slip.Run = run;
            slips.Add(slip);
            tGross += gross;
            tNet += slip.NetPay;
        }
        run.TotalGross = tGross;
        run.TotalNet = tNet;
        run.EmployeeCount = slips.Count;
        return (run, slips);
    }

    // ---------- ۶) عیدی و پایه سنوات سالانه (فاز ۲۵) ----------

    /// <summary>ردیف محاسبه‌شده عیدی/سنوات یک پرسنل.</summary>
    public sealed record BonusRow(Employee Emp, decimal Days, decimal Base, decimal Eid, decimal Seniority);

    /// <summary>
    /// محاسبه عیدی و پایه سنوات سالانه یک شرکت برای یک سال جلالی — کارکرد از ابتدای سال تا پایان همان سال.
    /// مبالغ از اقلام فرمول‌محور سیستمی «عیدی_سالانه» و «سنوات_سالانه» هر شرکت ارزیابی می‌شوند
    /// (هر شرکت می‌تواند فرمول خود را در فرمول‌ساز ویرایش کند).
    /// </summary>
    public static async Task<List<BonusRow>> ComputeBonusAsync(
        PayrollDbContext db, IAttendanceGateway att, Company company, int jy)
    {
        var cfg = await PayCompanyConfig.LoadAsync(db, company.Id, jy);
        var emps = await db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == company.Id && e.Status == "فعال")
            .ToListAsync();
        var shifts = await att.ShiftsByEmployeeAsync(emps.Select(e => e.Id).ToList());
        AttShiftInfo? shiftOf(string id) => shifts.GetValueOrDefault(id);

        var yearStart = Jalali.FromJalali(jy, 1, 1);
        var yearEndExclusive = Jalali.FromJalali(jy + 1, 1, 1);
        var ytd = await AggregateCompanyAsync(att, emps, yearStart, yearEndExclusive, cfg.Setting.WorkSource, false);

        var items = await db.PayItems.AsNoTracking()
            .Where(i => i.CompanyId == company.Id)
            .ToListAsync();
        var eidiItem = items.FirstOrDefault(i => i.Code == "عیدی_سالانه");
        var senItem = items.FirstOrDefault(i => i.Code == "سنوات_سالانه");
        var eidiFormula = eidiItem?.Formula ?? "round(min(پایه_مبنا * 2, حداقل_ماهانه * 3) * کارکرد_از_سال_روز / 365)";
        var senFormula = senItem?.Formula ?? "round(پایه_مبنا * کارکرد_از_سال_روز / 365)";

        var rows = new List<BonusRow>();
        foreach (var emp in emps.OrderBy(e => e.PersonnelCode))
        {
            var yy = ytd.GetValueOrDefault(emp.Id, new WorkAgg());
            var payable = yy.PayableDays;
            var vars = BuildVars(emp, shiftOf(emp.Id), yy, cfg, jy, payable);
            vars["جمع_مزایا"] = 0;
            vars["جمع_کسور"] = 0;
            vars["درآمد_مشمول_مالیات"] = 0;
            vars["درآمد_مشمول_بیمه"] = 0;
            vars["جمع_غیرمستمر_مشمول"] = 0;
            decimal EidOf(string formula, string label)
            {
                try
                {
                    return Math.Max(0, RoundAmount(PayrollFormula.Evaluate(formula, new PayFormulaContext
                    {
                        Vars = vars,
                        Lines = new Dictionary<string, decimal>(),
                        TaxBrackets = cfg.TaxBrackets,
                    }), cfg.Setting.RoundUnit));
                }
                catch (PayFormulaException ex)
                {
                    throw new PayFormulaException($"{ex.Message} — پرسنل «{emp.FullName}» (کد {emp.PersonnelCode}) [{label}]");
                }
            }
            var eid = EidOf(eidiFormula, "عیدی_سالانه");
            var sen = EidOf(senFormula, "سنوات_سالانه");
            rows.Add(new BonusRow(emp, payable, emp.BaseSalary, eid, sen));
        }
        return rows;
    }

    // ---------- ۷) تسویه‌حساب (فاز ۲۵) ----------

    public sealed record SettlementRow(decimal Days, decimal LeaveBalance, decimal LeavePay, decimal Eid, decimal Seniority, decimal Daily);

    /// <summary>
    /// محاسبه تسویه‌حساب یک پرسنل تا تاریخ پایان همکاری: بهای مانده مرخصی استحقاقی + عیدی نسبتی
    /// سال جاری + سنوات نسبتی. فرمول‌های عیدی/سنوات همان اقلام سیستمی شرکت هستند.
    /// </summary>
    public static async Task<SettlementRow> ComputeSettlementAsync(
        PayrollDbContext db, IAttendanceGateway att, Employee emp, DateTime endDate)
    {
        var shifts = await att.ShiftsByEmployeeAsync(new List<string> { emp.Id });
        AttShiftInfo? shiftOf(string id) => shifts.GetValueOrDefault(id);
        var (jy, _, _) = Jalali.ToJalali(endDate);
        var cfg = await PayCompanyConfig.LoadAsync(db, emp.CompanyId, jy);

        var yearStart = Jalali.FromJalali(jy, 1, 1);
        var endExclusive = endDate.Date.AddDays(1);
        var ytd = await AggregateCompanyAsync(att, new List<Employee> { emp },
            yearStart, endExclusive, cfg.Setting.WorkSource, false);
        var yy = ytd.GetValueOrDefault(emp.Id, new WorkAgg());
        var payable = yy.PayableDays;

        var items = await db.PayItems.AsNoTracking()
            .Where(i => i.CompanyId == emp.CompanyId)
            .ToListAsync();
        var eidiFormula = items.FirstOrDefault(i => i.Code == "عیدی_سالانه")?.Formula
            ?? "round(min(پایه_مبنا * 2, حداقل_ماهانه * 3) * کارکرد_از_سال_روز / 365)";
        var senFormula = items.FirstOrDefault(i => i.Code == "سنوات_سالانه")?.Formula
            ?? "round(پایه_مبنا * کارکرد_از_سال_روز / 365)";

        var vars = BuildVars(emp, shiftOf(emp.Id), yy, cfg, jy, payable);
        vars["جمع_مزایا"] = 0;
        vars["جمع_کسور"] = 0;
        vars["درآمد_مشمول_مالیات"] = 0;
        vars["درآمد_مشمول_بیمه"] = 0;
        vars["جمع_غیرمستمر_مشمول"] = 0;

        decimal Eval(string formula, string label)
        {
            try
            {
                return Math.Max(0, RoundAmount(PayrollFormula.Evaluate(formula, new PayFormulaContext
                {
                    Vars = vars,
                    Lines = new Dictionary<string, decimal>(),
                    TaxBrackets = cfg.TaxBrackets,
                }), cfg.Setting.RoundUnit));
            }
            catch (PayFormulaException ex)
            {
                throw new PayFormulaException($"{ex.Message} [{label}]");
            }
        }

        var leaveBalance = Math.Max(0, emp.AnnualLeaveEnt - emp.AnnualLeaveUsed);
        var daily = cfg.Setting.MonthDays > 0 ? Math.Round(emp.BaseSalary / cfg.Setting.MonthDays, 0) : 0;
        return new SettlementRow(
            payable, leaveBalance,
            RoundAmount(leaveBalance * daily, cfg.Setting.RoundUnit),
            Eval(eidiFormula, "عیدی_سالانه"), Eval(senFormula, "سنوات_سالانه"), daily);
    }

    // ---------- ۸) آزمایش فرمول در زمینه واقعی ----------

    public static async Task<(bool Ok, decimal Value, string? Error, Dictionary<string, decimal> Vars)>
        TestFormulaAsync(PayrollDbContext db, IAttendanceGateway att, string employeeId, int jy, int jm, string formula)
    {
        var emp = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId);
        if (emp is null) return (false, 0, "پرسنل یافت نشد", new Dictionary<string, decimal>());
        var shifts1 = await att.ShiftsByEmployeeAsync(new List<string> { emp.Id });
        var shift1 = shifts1.GetValueOrDefault(emp.Id);

        var cfg = await PayCompanyConfig.LoadAsync(db, emp.CompanyId, jy);
        var (start, end) = Jalali.MonthRange(jy, jm);
        var aggs = await AggregateCompanyAsync(att, new List<Employee> { emp }, start, end, cfg.Setting.WorkSource, true);
        var ytd = await AggregateCompanyAsync(att, new List<Employee> { emp }, Jalali.FromJalali(jy, 1, 1), end, cfg.Setting.WorkSource, false);
        var agg = aggs.GetValueOrDefault(emp.Id, new WorkAgg());
        var yy = ytd.GetValueOrDefault(emp.Id, new WorkAgg());
        var vars = BuildVars(emp, shift1, agg, cfg, jy, yy.WorkDays + yy.MissionDays + yy.PaidLeaveDays + yy.SickDays);
        vars["جمع_مزایا"] = 0;
        vars["جمع_کسور"] = 0;
        vars["درآمد_مشمول_مالیات"] = 0;
        vars["درآمد_مشمول_بیمه"] = 0;
        vars["جمع_غیرمستمر_مشمول"] = 0;

        try
        {
            var val = PayrollFormula.Evaluate(formula, new PayFormulaContext
            {
                Vars = vars,
                Lines = new Dictionary<string, decimal>(),
                TaxBrackets = cfg.TaxBrackets,
            });
            return (true, val, null, vars);
        }
        catch (PayFormulaException ex)
        {
            return (false, 0, ex.Message, vars);
        }
    }
}
