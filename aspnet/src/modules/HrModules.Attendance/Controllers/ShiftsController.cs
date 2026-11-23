using System.Globalization;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Attendance.Data;
using Platform.Contracts;
using HrModules.Attendance.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// مدیریت شیفت‌ها و نوبت‌کاری (ATT-02) — CRUD کامل شیفت‌ها که تا امروز فقط از Seeder
/// ساخته می‌شدند و در پنل دیده نمی‌شدند. موتور حضور (AttEngine) از همین تعریف‌ها برای
/// دیرکرد/تعجیل/اضافه‌کاری استفاده می‌کند.
/// فاز ۶۱ — روی لایهٔ دیتای مستقل ماژول (AttendanceDbContext)؛ حسابرسی از IAuditSink.
/// </summary>
public class ShiftsController : HrModuleControllerBase
{
    public static readonly string[] ShiftTypes = { "عادی", "نوبت‌کاری", "چرخشی", "۱۲ساعته", "اقماری" };

    private readonly AttendanceDbContext Db;

    public ShiftsController(AttendanceDbContext db, ICurrentUser me, IAuditSink audits) : base(me, audits) => Db = db;

    [HttpGet]
    [HasPerm("shifts.view")]
    public async Task<IActionResult> Index()
    {
        // فاز ۷۴ — دامنهٔ رکوردی شیفت‌ها: شیفت‌های شرکت‌های مجاز؛ شیفت مشترک (بدون شرکت) برای همه
        var scope = await this.ScopeAsync();
        var shiftsQ = Db.Shifts
            .Include(s => s.Company)
            .Include(s => s.Employees)
            .AsQueryable();
        if (!scope.All && scope.Companies.Count > 0)
            shiftsQ = shiftsQ.Where(s => s.CompanyId == null || scope.Companies.Contains(s.CompanyId));
        var shifts = await shiftsQ
            .OrderByDescending(s => s.Active).ThenBy(s => s.Name)
            .ToListAsync();
        ViewBag.ActiveCount = shifts.Count(s => s.Active);
        ViewBag.AssignedCount = shifts.Sum(s => s.Employees.Count(e => e.Status == "فعال"));
        ViewBag.NoShiftCount = await Db.Employees.CountAsync(e => e.ShiftId == null && e.Status == "فعال");
        return View(shifts);
    }

    // ---------- فرم‌های مودال ----------

    [HttpGet]
    [HasPerm("shifts.create")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        return FormView("_ShiftForm", new Shift());
    }

    [HttpGet]
    [HasPerm("shifts.edit")]
    public async Task<IActionResult> Edit(string id)
    {
        var s = await Db.Shifts.FindAsync(id);
        if (s is null) return NotFound();
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        ViewBag.IsEdit = true;
        return FormView("_ShiftForm", s);
    }

    // ---------- ذخیره ----------

    [HttpPost]
    [HasPerm("shifts.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string companyId, string name, string type, string startTime, string endTime,
        int breakMinutes, int toleranceLate, string? cyclePattern, bool active)
    {
        var err = await Validate(name, type, startTime, endTime, breakMinutes, toleranceLate, cyclePattern, null);
        if (err is not null) return ToastDone(false, err, "/Shifts");

        var shift = new Shift
        {
            CompanyId = string.IsNullOrEmpty(companyId) ? null : companyId,
            Name = name.Trim(),
            Type = type,
            StartTime = startTime.Trim(),
            EndTime = endTime.Trim(),
            BreakMinutes = breakMinutes,
            ToleranceLate = toleranceLate,
            CyclePattern = NormCycle(cyclePattern),
            Active = active,
        };
        Db.Shifts.Add(shift);
        await Db.SaveChangesAsync();
        await Audit("create", "shift", shift.Id, new { shift.Name, shift.Type, shift.StartTime, shift.EndTime, shift.BreakMinutes, shift.CyclePattern, shift.Active });
        return ToastDone(true, HrT.F("شیفت «{0}» تعریف شد", shift.Name), "/Shifts");
    }

    [HttpPost]
    [HasPerm("shifts.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string companyId, string name, string type, string startTime, string endTime,
        int breakMinutes, int toleranceLate, string? cyclePattern, bool active)
    {
        var shift = await Db.Shifts.FindAsync(id);
        if (shift is null) return NotFound();

        var err = await Validate(name, type, startTime, endTime, breakMinutes, toleranceLate, cyclePattern, id);
        if (err is not null) return ToastDone(false, err, "/Shifts");

        var old = shift.Name;
        shift.CompanyId = string.IsNullOrEmpty(companyId) ? null : companyId;
        shift.Name = name.Trim();
        shift.Type = type;
        shift.StartTime = startTime.Trim();
        shift.EndTime = endTime.Trim();
        shift.BreakMinutes = breakMinutes;
        shift.ToleranceLate = toleranceLate;
        shift.CyclePattern = NormCycle(cyclePattern);
        shift.Active = active;
        await Db.SaveChangesAsync();
        await Audit("update", "shift", shift.Id, new { old, name = shift.Name, shift.Type, shift.StartTime, shift.EndTime, shift.Active });
        return ToastDone(true, HrT.F("شیفت «{0}» به‌روزرسانی شد", shift.Name), "/Shifts");
    }

    [HttpPost]
    [HasPerm("shifts.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var shift = await Db.Shifts.Include(s => s.Employees).FirstOrDefaultAsync(s => s.Id == id);
        if (shift is null) return NotFound();

        var assigned = shift.Employees.Count;
        if (assigned > 0)
            return ToastDone(false,
                HrT.F("شیفت «{0}» به {1} پرسنل متصل است — ابتدا شیفت آن‌ها را در صفحه پرسنل تغییر دهید", shift.Name, HrT.Num(assigned)), "/Shifts");

        Db.Shifts.Remove(shift);
        await Db.SaveChangesAsync();
        await Audit("delete", "shift", shift.Id, new { shift.Name, shift.Type });
        return ToastDone(true, HrT.F("شیفت «{0}» حذف شد", shift.Name), "/Shifts");
    }

    // ======================================================================
    // فاز ۱۳۵ (مورد ۱۶ ROADMAP-ADDITIONS) — تقویم آینده‌نگر شیت اقماری/چرخشی
    // الگوی «کار/استراحت» (مثل 14/14) از CyclePattern و روزِ شروع چرخه از
    // CycleAnchor؛ روزها: (تاریخ − لنگرگاه) mod (کار+استراحت) → کارکرد/استراحت.
    // ======================================================================

    [HttpPost]
    [HasPerm("shifts.view")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAnchor(string id, DateTime cycleAnchor)
    {
        var shift = await Db.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        if (shift is null) return NotFound();
        if (string.IsNullOrWhiteSpace(shift.CyclePattern))
            return ToastDone(false, HrT.T("این شیفت الگوی چرخش ندارد — ابتدا در فرم شیفت الگو را ثبت کنید"), "/Shifts");
        shift.CycleAnchor = cycleAnchor.Date;
        await Db.SaveChangesAsync();
        await Audit("update", "shift-cycle-anchor", shift.Id, new { shift.Name, anchor = cycleAnchor.Date });
        return ToastDone(true, HrT.F("روز شروع چرخهٔ «{0}» ثبت شد", shift.Name), "/Shifts/CycleCalendar");
    }

    [HttpGet]
    [HasPerm("shifts.view")]
    public async Task<IActionResult> CycleCalendar(int days = 30, string? shiftId = null)
    {
        if (days is < 7 or > 90) days = 30;
        var scope = await this.ScopeAsync();

        var shiftsQ = Db.Shifts.AsNoTracking().Where(s => s.Active
            && s.CyclePattern != null && (s.Type == "اقماری" || s.Type == "چرخشی" || s.Type == "نوبت‌کاری"));
        shiftsQ = shiftsQ.WhereCoScope(scope, s => s.CompanyId);
        var shifts = await shiftsQ.OrderBy(s => s.Name).ToListAsync();
        if (!string.IsNullOrWhiteSpace(shiftId)) shifts = shifts.Where(s => s.Id == shiftId).ToList();

        var empQ = Db.Employees.AsNoTracking().Where(e => e.Status == "فعال" && e.ShiftId != null);
        empQ = empQ.WhereCoScope(scope, e => e.CompanyId);
        var emps = await empQ.OrderBy(e => e.PersonnelCode).ToListAsync();
        var byShift = emps.GroupBy(e => e.ShiftId!).ToDictionary(g => g.Key, g => g.ToList());

        var start = DateTime.Today;
        var dayList = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToList();
        var rows = new List<CycleCalendarRow>();
        foreach (var s in shifts)
        {
            var (work, rest) = ParseCycle(s.CyclePattern);
            if (work is null || rest is null || s.CycleAnchor is null) continue;
            var anchor = s.CycleAnchor.Value.Date;
            var cycle = work.Value + rest.Value;
            var people = byShift.TryGetValue(s.Id, out var p) ? p : new List<Employee>();
            var nameOf = (Employee e) => $"{e.FirstName} {e.LastName}".Trim();
            rows.Add(new CycleCalendarRow(
                s.Name, s.CyclePattern!, people.Count,
                dayList.Select(d =>
                {
                    var off = (int)((d - anchor).TotalDays % cycle);
                    if (off < 0) off += cycle;
                    return off < work.Value;
                }).ToList(),
                people.Select(nameOf).ToList()));
        }

        ViewBag.Days = dayList;
        ViewBag.DaysCount = days;
        ViewBag.Shifts = shifts;
        ViewBag.ShiftId = shiftId;
        ViewBag.TodayOnDuty = rows.Sum(r => r.Cells[0] ? 1 : 0);
        return View(rows);
    }

    /// <summary>تجزیهٔ الگو «14/14» → (کار، استراحت).</summary>
    private static (int?, int?) ParseCycle(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return (null, null);
        var parts = pattern.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var w) || !int.TryParse(parts[1].Trim(), out var r)
            || w < 1 || r < 0) return (null, null);
        return (w, r);
    }

    // ---------- اعتبارسنجی مشترک ----------

    private async Task<string?> Validate(string? name, string? type, string? startTime, string? endTime,
        int breakMinutes, int toleranceLate, string? cyclePattern, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
            return "نام شیفت باید حداقل ۲ حرف باشد";
        var dup = await Db.Shifts.AnyAsync(s => s.Name.Trim() == name.Trim() && (excludeId == null || s.Id != excludeId));
        if (dup) return HrT.F("شیفتی با نام «{0}» قبلاً تعریف شده است", name.Trim());
        if (!ShiftTypes.Contains(type)) return "نوع شیفت نامعتبر است";
        if (!ValidHm(startTime)) return "ساعت شروع باید به قالب ۲۴ساعته HH:MM باشد";
        if (!ValidHm(endTime)) return "ساعت پایان باید به قالب ۲۴ساعته HH:MM باشد";
        if (Hm(endTime) == Hm(startTime)) return "ساعت شروع و پایان نمی‌توانند یکسان باشند";
        if (breakMinutes < 0 || breakMinutes > 480) return "استراحت باید بین ۰ تا ۴۸۰ دقیقه باشد";
        if (toleranceLate < 0 || toleranceLate > 120) return "رواداری دیرکرد باید بین ۰ تا ۱۲۰ دقیقه باشد";
        if (type == "اقماری" && string.IsNullOrWhiteSpace(cyclePattern))
            return "برای شیفت اقماری الگوی چرخش (مثال 14/14) الزامی است";
        if (!string.IsNullOrWhiteSpace(cyclePattern) && !System.Text.RegularExpressions.Regex.IsMatch(cyclePattern.Trim(), @"^\d{1,3}/\d{1,3}$"))
            return "الگوی چرخش باید به شکل روزکار/استراحت باشد (مثال: 14/14)";
        if (!string.IsNullOrWhiteSpace(cyclePattern) && int.Parse(cyclePattern.Split('/')[0]) == 0)
            return "تعداد روزهای کار در الگوی چرخش نمی‌تواند صفر باشد";
        return null;
    }

    private static bool ValidHm(string? t) =>
        !string.IsNullOrWhiteSpace(t) && System.Text.RegularExpressions.Regex.IsMatch(t.Trim(), @"^([01]\d|2[0-3]):[0-5]\d$");

    private static int Hm(string t)
    {
        var p = t.Trim().Split(':');
        return int.Parse(p[0], CultureInfo.InvariantCulture) * 60 + int.Parse(p[1], CultureInfo.InvariantCulture);
    }

    private static string? NormCycle(string? c)
    {
        c = c?.Trim();
        return string.IsNullOrEmpty(c) ? null : c;
    }
}
