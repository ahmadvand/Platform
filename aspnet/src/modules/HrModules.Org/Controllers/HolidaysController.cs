using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrModules.Org;

public class HolidaysController : MvcBase
{
    public HolidaysController(AppDbContext db, ICurrentUser me) : base(db, me) { }

    // فاز ۶۹ — DbSet خانوادهٔ تقویم روی کانتکست مرکب (مالکیت اسکیما ∈ OrgDataModule.ComposeModel)
    private DbSet<Holiday> Holidays => Db.Set<Holiday>();

    [HttpGet]
    [HasPerm("holidays.view")]
    public async Task<IActionResult> Index(int? y, int? m)
    {
        // فاز ۹۷ — ماه‌شمار تعطیلات از «تقویم نمایشی» سامانه پیروی می‌کند (شمسی/میلادی/قمری)
        var mode = Jalali.DisplayMode;
        var nowD = Jalali.DisplayNow(mode);
        var cy = y ?? nowD.Y;
        var cm = m ?? nowD.M;
        if (cm < 1 || cm > 12) cm = nowD.M;
        if (mode == HrCalendarMode.Hijri) cy = Math.Clamp(cy, 1318, 1500); // بازهٔ ام‌القری

        var grid = Jalali.DisplayMonthGrid(mode, cy, cm);
        var keys = grid.Where(c => c.Date is not null).Select(c => Jalali.DateKey(c.Date!.Value)).ToList();
        var holidays = await Holidays
            .Where(h => keys.Contains(h.Date) || (mode == HrCalendarMode.Jalali && h.JYear == cy && h.JMonth == cm))
            .ToListAsync();
        var holidayMap = holidays.ToDictionary(h => h.Date);

        ViewBag.Cal = mode;
        ViewBag.Y = cy;
        ViewBag.M = cm;
        ViewBag.MonthLen = Jalali.DisplayMonthInfo(mode, cy, cm).Len;
        ViewBag.Grid = grid;
        ViewBag.HolidayMap = holidayMap;
        var todayKey = Jalali.DateKey(DateTime.Now);
        var allHolidays = await Holidays.ToListAsync(); // جدول کوچک — فیلتر سمت کلاینت
        ViewBag.Upcoming = allHolidays
            .Where(h => string.CompareOrdinal(h.Date, todayKey) >= 0)
            .OrderBy(h => h.Date).Take(12).ToList();
        return View();
    }

    [HttpPost]
    [HasPerm("holidays.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int jy, int jm, int jd, string title)
    {
        try
        {
            // فاز ۹۷ — ورودی فرم طبق تقویم نمایشی تفسیر می‌شود؛ کلید ذخیره همیشه میلادی می‌ماند
            var mode = Jalali.DisplayMode;
            var g = Jalali.FromDisplayDate(mode, jy, jm, jd);
            var key = Jalali.DateKey(g);
            var h = await Holidays.FirstOrDefaultAsync(x => x.Date == key);
            if (h is not null) { h.Title = title; }
            else
            {
                Holidays.Add(new Holiday { Date = key, Title = title, JYear = jy, JMonth = jm, JDay = jd });
            }
            await Db.SaveChangesAsync();
            await Audit("create", "holiday", key, new { cal = Jalali.DisplayModeKey, jy, jm, jd, title });
            ToastOk(HrT.F("تعطیلی «{0}» در تقویم ثبت شد", title));
        }
        catch (Exception ex) { ToastErr(HrT.F("تاریخ نامعتبر است: {0}", ex.Message)); }
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect($"/Holidays?y={jy}&m={jm}");
    }

    [HttpPost]
    [HasPerm("holidays.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, int? y, int? m)
    {
        var h = await Holidays.FindAsync(id);
        if (h is null) return NotFound();
        Holidays.Remove(h);
        await Db.SaveChangesAsync();
        await Audit("delete", "holiday", id, new { h.Date, h.Title });
        ToastOk(HrT.T("تعطیلی حذف شد"));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect($"/Holidays?y={(y ?? h.JYear)}&m={(m ?? h.JMonth)}");
    }
}
