using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using HrModules.Access.Data;
using HrModules.Access.Domain;
using Platform.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۶۱ — تفکیک لایهٔ دیتا: جداول تردد از AccessDbContext (کانتکست خود ماژول)،
/// دادهٔ پایه (هشدارهای امنیتی/تنظیمات اعلان/دستور دستگاه‌ها) از HrBaseDbContext
/// و نام بازدیدکننده‌ها از دروازهٔ قراردادی IVisitorsGateway — بدون DbContext مرکب هاست.
/// </summary>
public class AccessController : HrModuleControllerBase
{
    private readonly AccessDbContext Db;
    private readonly HrBaseDbContext Base;
    private readonly IVisitorsGateway Visitors;
    // فاز ۱۳۱ (مورد ۱۲ ROADMAP-ADDITIONS) — دروازهٔ حضور و غیاب برای گیت‌های «حضورشمار»
    private readonly IAttendanceGateway Attendance;

    public AccessController(AccessDbContext db, HrBaseDbContext baseDb, IVisitorsGateway visitors, IAttendanceGateway attendance, ICurrentUser me, IAuditSink audits) : base(me, audits)
    { Db = db; Base = baseDb; Visitors = visitors; Attendance = attendance; }

    [HttpGet]
    [HasPerm("access.view")]
    public async Task<IActionResult> Index(string? result, string tab = "zones", string? co = null)
    {
        ViewBag.Tab = tab;
        // فاز ۷۴ — دامنهٔ رکوردی کنترل تردد: منطقه‌ها/گیت‌ها با دامنهٔ شرکت؛
        // مجوزها/رویدادها با اجتماعِ دامنهٔ پرسنل (فیلتر SQL-side)
        var scope = await this.ScopeAsync();
        if (!scope.All && scope.Companies.Count > 0)
            co = string.IsNullOrEmpty(co) || !scope.Companies.Contains(co) ? null : co;
        ViewBag.Companies = (await Db.Companies.OrderBy(c => c.Code).ToListAsync())
            .Where(c => scope.All || scope.Companies.Count == 0 || scope.Companies.Contains(c.Id)).ToList();
        ViewBag.Co = co;
        var zonesQ = Db.Zones.Include(z => z.Company).Include(z => z.Gates).ThenInclude(g => g.Device).AsQueryable();
        if (!string.IsNullOrEmpty(co)) zonesQ = zonesQ.Where(z => z.CompanyId == co);
        zonesQ = zonesQ.WhereCoScope(scope, z => z.CompanyId);
        ViewBag.Zones = await zonesQ.OrderBy(z => z.Code).ToListAsync();
        var permsQ = Db.AccessPermissions
            .Include(p => p.Employee).Include(p => p.Zone).AsQueryable();
        if (!string.IsNullOrEmpty(co)) permsQ = permsQ.Where(p => p.Zone!.CompanyId == co);
        permsQ = permsQ.WhereEmpScope(scope, p => p.EmployeeId!);
        var perms = await permsQ.OrderByDescending(p => p.CreatedAt).ToListAsync();
        ViewBag.Perms = perms;
        var events = Db.AccessEvents
            .Include(e => e.Employee).Include(e => e.Gate).ThenInclude(g => g!.Zone)
            .AsQueryable();
        if (!string.IsNullOrEmpty(co)) events = events.Where(e => e.Gate!.CompanyId == co);
        events = events.WhereEmpScope(scope, e => e.EmployeeId!);
        if (!string.IsNullOrEmpty(result)) events = events.Where(e => e.Result == result);
        var evRows = await events.OrderByDescending(e => e.Timestamp).Take(100).ToListAsync();
        ViewBag.Events = evRows;
        // فاز ۶۱ — navigation بازدیدکننده حذف شد؛ نام‌ها از دروازهٔ ماژول بازدیدکننده (قرارداد IVisitorsGateway)
        var visitorIds = perms.Select(p => p.VisitorId).Concat(evRows.Select(e => e.VisitorId))
            .Where(id => id is not null).Cast<string>().Distinct().ToList();
        ViewBag.VisitorNames = visitorIds.Count > 0
            ? await Visitors.FullNamesAsync(visitorIds)
            : new Dictionary<string, string>();
        ViewBag.Alerts = await Base.SecurityAlerts.OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync();
        ViewBag.ResultFilter = result;
        // فاز ۳۰: انقضای خودکار هشدارها (TTL) — هنگام بازدید صفحه اعمال می‌شود
        await DeviceOps.SweepAlertTtlAsync(Db);
        ViewBag.Alerts = await Base.SecurityAlerts.OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync();
        ViewBag.TtlDays = (await NotificationEngine.GetSettingsAsync(Base)).AlertTtlDays;
        // فاز ۱۱۴ — لیست کامل پرسنل حذف شد؛ انتخاب با جستجوی زندهٔ /Ui/EmpSearch (مقیاس ده‌ها هزار نفر)
        if (tab == "traffic")
        {
            // تحلیل تردد ۳۰ روز اخیر (فاز ۲۹) — تراکم ساعتی، پرترددترین گیت‌ها، جهت/نتیجه/روش
            var from = DateTime.Now.AddDays(-30);
            var evQ = Db.AccessEvents.Where(e => e.Timestamp >= from);
            if (!string.IsNullOrEmpty(co)) evQ = evQ.Where(e => e.Gate!.CompanyId == co);
            var evs = await evQ
                .Select(e => new { e.Timestamp, e.Direction, e.Result, e.Method, GateName = e.Gate != null ? e.Gate.Name : "—" })
                .ToListAsync();
            var hourly = new int[24];
            foreach (var x in evs) hourly[x.Timestamp.Hour]++;
            ViewBag.Hourly = hourly;
            ViewBag.TopGates = evs.GroupBy(x => x.GateName)
                .Select(g => new { Gate = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(8).ToList();
            ViewBag.Methods = evs.GroupBy(x => x.Method)
                .Select(g => new { Method = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ToList();
            ViewBag.DirIn = evs.Count(x => x.Direction == "ورود");
            ViewBag.DirOut = evs.Count(x => x.Direction == "خروج");
            ViewBag.ResOk = evs.Count(x => x.Result == "مجاز");
            ViewBag.ResNo = evs.Count(x => x.Result == "غیرمجاز");
            ViewBag.TrafficTotal = evs.Count;
        }
        if (tab == "map")
        {
            // نقشه سایت گرافیکی (فاز ۳۰) — گیت‌های شرکت(های) انتخابی با مختصات اختیاری
            var mapQ = Db.Gates.Include(g => g.Zone).Include(g => g.Device).AsQueryable();
            if (!string.IsNullOrEmpty(co)) mapQ = mapQ.Where(g => g.CompanyId == co);
            ViewBag.MapGates = await mapQ.OrderBy(g => g.Zone!.Code).ThenBy(g => g.Code).ToListAsync();
        }
        if (tab == "groups")
        {
            var gq = Db.AccessGroups.Include(g => g.Company).AsQueryable();
            if (!string.IsNullOrEmpty(co)) gq = gq.Where(g => g.CompanyId == co);
            gq = gq.WhereCoScope(scope, g => g.CompanyId!);
            var gid = await gq.Select(g => g.Id).ToListAsync();
            ViewBag.Groups = await Db.AccessGroups
                .Include(g => g.Members).ThenInclude(m => m.Employee)
                .Include(g => g.Gates).ThenInclude(gg => gg.Gate)
                .Where(g => gid.Contains(g.Id))
                .OrderBy(g => g.Name).ToListAsync();
        }
        return View();
    }

    // ---------- فرم‌های پاپ‌آپ (مودال AJAX) ----------
    [HttpGet]
    [HasPerm("access.zones.manage")]
    public async Task<IActionResult> CreateZone()
    {
        ViewBag.Companies = await Db.Companies.OrderBy(c => c.Code).ToListAsync();
        return PartialView("_ZoneForm");
    }

    [HttpGet]
    [HasPerm("access.gates.manage")]
    public async Task<IActionResult> CreateGate()
    {
        ViewBag.Zones = await Db.Zones.Include(z => z.Company).OrderBy(z => z.Code).ToListAsync();
        ViewBag.Devices = await Db.Devices.Include(d => d.Company).OrderBy(d => d.Name).ToListAsync();
        return PartialView("_GateForm");
    }

    [HttpGet]
    [HasPerm("access.perms.manage")]
    public async Task<IActionResult> CreatePerm()
    {
        // فاز ۱۲۱ — بارگذاری مردهٔ لیست کامل پرسنل حذف شد (فرم با /Ui/EmpSearch کار می‌کند)
        ViewBag.Zones = await Db.Zones.OrderBy(z => z.Code).ToListAsync();
        return PartialView("_PermForm");
    }

    [HttpGet]
    [HasPerm("access.gates.manage")]
    public async Task<IActionResult> GateScheduleForm(string id)
    {
        var gate = await Db.Gates.Include(g => g.Zone).FirstOrDefaultAsync(g => g.Id == id);
        if (gate is null) return NotFound();
        ViewBag.Gate = gate;
        return PartialView("_GateScheduleForm");
    }

    [HttpGet]
    [HasPerm("access.gates.manage")]
    public async Task<IActionResult> DoorStateForm(string id)
    {
        var gate = await Db.Gates.Include(g => g.Zone).FirstOrDefaultAsync(g => g.Id == id);
        if (gate is null) return NotFound();
        ViewBag.Gate = gate;
        return PartialView("_DoorStateForm");
    }

    /// <summary>
    /// شبیه‌ساز اسکن گیت — ارزیابی کامل (برنامه گیت + مجوز فردی + گروه + آنتی‌پس‌بک) — فاز ۱۷.
    /// فاز ۲۹: با روش «کارت» شماره کارت از مخزن کارت‌ها به پرسنل نگاشت می‌شود (وضعیت/انقضا/گمشدی کنترل می‌شود).
    /// </summary>
    [HttpPost]
    [HasPerm("access.scan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(string personnelCode, string gateId, string direction, string method)
    {
        var gate = await Db.Gates.Include(g => g.Zone).FirstOrDefaultAsync(g => g.Id == gateId);
        if (gate is null) return NotFound();

        var now = DateTime.Now;

        // ثبت رویداد ردِ مرتبط با کارت + هشدار (کمکی محلی)
        async Task<IActionResult> DenyCard(string cardNo, string reason, string alertType)
        {
            var ev = new AccessEvent
            {
                GateId = gateId, Direction = direction, Method = method,
                Result = "غیرمجاز", Reason = reason, Timestamp = now,
            };
            Db.AccessEvents.Add(ev);
            Base.SecurityAlerts.Add(new SecurityAlert
            {
                Type = alertType, Severity = alertType == "کارت گمشده" ? "بالا" : "متوسط",
                Message = $"{reason} — کارت «{cardNo}» در {gate.Name}",
                Source = "کنترل تردد", RefId = ev.Id,
            });
            await Db.SaveChangesAsync();
            await Base.SaveChangesAsync();
            await Audit("scan", "access_event", ev.Id, new { cardNo, gate.Code, allowed = false, reason });
            ToastErr(HrT.F("{0}: {1}", HrT.T(reason), cardNo));
            if (Ajax) return AjaxResult(false, HrT.F("{0}: {1}", HrT.T(reason), cardNo), new { tone = "err" });
            return Redirect("/Access?tab=events");
        }

        Employee? emp;
        if (method == "کارت")
        {
            var cardNo = (personnelCode ?? "").Trim();
            var card = await Db.Cards.Include(c => c.Employee).FirstOrDefaultAsync(c => c.CardNo == cardNo);
            if (card is null)
                return await DenyCard(cardNo, "کارت در مخزن ثبت نشده است", "کارت گمشده");
            if (card.Status == "گمشده")
                return await DenyCard(cardNo, "کارت به‌عنوان مفقود اعلام شده است", "کارت گمشده");
            if (card.Status == "باطل")
                return await DenyCard(cardNo, "کارت باطل شده است", "ورود غیرمجاز");
            if (card.Status == "آزاد")
                return await DenyCard(cardNo, "کارت به پرسنلی تخصیص نیافته است", "ورود غیرمجاز");
            if (card.ExpiresAt is not null && card.ExpiresAt.Value < now)
                return await DenyCard(cardNo, "اعتبار کارت منقضی شده است", "مجوز منقضی");
            if (card.Employee is null)
                return await DenyCard(cardNo, "کارت فاقد دارنده است", "ورود غیرمجاز");
            emp = card.Employee;
        }
        else
        {
            emp = await Db.Employees.FirstOrDefaultAsync(e => e.PersonnelCode == (personnelCode ?? "").Trim());
            if (emp is null) { ToastErr("کد پرسنلی یافت نشد"); if (Ajax) return AjaxResult(false, "کد پرسنلی یافت نشد"); return Redirect("/Access"); }
        }

        var d = await AccessControl.EvaluateAsync(Db, emp, gate, direction, now, method);
        var (allowed, reason) = (d.Allowed, d.Reason);
        var ev = new AccessEvent
        {
            EmployeeId = emp.Id, GateId = gateId, Direction = direction,
            Method = method, Result = allowed ? "مجاز" : "غیرمجاز", Reason = reason, Timestamp = now,
        };
        Db.AccessEvents.Add(ev);
        if (!allowed && !(reason is not null && reason.Contains("دومرحله‌ای")))
        {
            // ردِ عادی → هشدار امنیتی؛ ردِ «در انتظار عامل دوم» وضعیت موقتی عادی جریان دوعاملی است — بدون هشدار
            Base.SecurityAlerts.Add(new SecurityAlert
            {
                Type = reason is not null && reason.Contains("آنت‌ی‌پس‌بک") ? "ورود غیرمجاز" : "ورود غیرمجاز",
                Severity = reason == "فاقد مجوز دسترسی به این منطقه" ? "بحرانی" : "بالا",
                Message = $"تلاش {direction} غیرمجاز توسط {emp.FullName} در {gate.Name} ({reason})",
                Source = "کنترل تردد", RefId = ev.Id,
            });
        }
        await Db.SaveChangesAsync();
        await Base.SaveChangesAsync();
        await Audit("scan", "access_event", ev.Id, new { emp.PersonnelCode, gate.Code, allowed, source = d.Source });

        // فاز ۱۳۱ (مورد ۱۲ ROADMAP-ADDITIONS) — پیوند رویداد دروازه → حضور و غیاب:
        // اسکن مجازِ گیتِ «حضورشمار» با همان موتور دستگاه‌های حضور (UpsertPunch —
        // اولین اسکن روز=ورود، آخرین=خروج + محاسبهٔ AttCalc) در کارت تردد ثبت می‌شود؛
        // خطای ماژول حضور هرگز جواب دسترسی را خراب نمی‌کند (فقط حسابرسی می‌شود).
        if (allowed && gate.FeedsAttendance)
        {
            try
            {
                var pr = await Attendance.UpsertPunchAsync(emp.Id, gate.DeviceId, Jalali.DateKey(now), now);
                await Audit("scan-attendance", "attendance", ev.Id,
                    new { emp.PersonnelCode, gate.Code, in1 = pr.CheckIn, out1 = pr.CheckOut, work = pr.WorkMinutes, late = pr.LateMinutes });
            }
            catch (Exception ex)
            {
                await Audit("scan-attendance-failed", "attendance", ev.Id,
                    new { emp.PersonnelCode, gate.Code, err = ex.Message });
            }
        }

        if (allowed) ToastOk(HrT.F("اسکن مجاز: {0} — {1}", emp.FullName, gate.Name));
        else ToastErr(HrT.F("اسکن غیرمجاز: {0} — {1}", emp.FullName, HrT.T(reason ?? "")));
        if (Ajax) return AjaxResult(true, allowed ? HrT.F("اسکن مجاز: {0} — {1}", emp.FullName, gate.Name) : HrT.F("اسکن غیرمجاز: {0} — {1}", emp.FullName, HrT.T(reason ?? "")),
            new { tone = allowed ? "ok" : "err" });
        return Redirect("/Access?tab=events");
    }

    /// <summary>تغییر حالت کنترلی درب: نرمال | باز دائم | قفل دائم — فاز ۲۹. برای دستگاه متصل دستور متناسب صف می‌شود.</summary>
    [HttpPost]
    [HasPerm("access.gates.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDoorState(string id, string state)
    {
        var valid = new[] { "نرمال", "باز دائم", "قفل دائم" };
        if (!valid.Contains(state)) return AjaxResult(false, "حالت نامعتبر است");
        var gate = await Db.Gates.Include(g => g.Device).FirstOrDefaultAsync(g => g.Id == id);
        if (gate is null) return NotFound();
        var old = gate.DoorState;
        gate.DoorState = state;
        if (gate.DeviceId is not null)
        {
            var cmdType = state switch
            {
                "باز دائم" => "unlock",        // باز کردن درب (یک ساعت)
                "قفل دائم" => "disable_device", // مسدودسازی دستگاه
                _ => "enable_device",           // بازگشت به کنترل عادی
            };
            await DeviceOps.QueueCommandAsync(Db, gate.DeviceId, cmdType,
                state == "باز دائم" ? new { seconds = 3600 } : null, Sess);
        }
        await Db.SaveChangesAsync();
        await Audit("door-state", "gate", gate.Id, new { from = old, to = state });
        ToastOk(HrT.F("حالت درب «{0}» به «{1}» تغییر کرد", gate.Name, HrT.T(state ?? "")));
        if (Ajax) return AjaxResult(true, HrT.F("حالت درب «{0}» به «{1}» تغییر کرد", gate.Name, HrT.T(state ?? "")));
        return Redirect("/Access?tab=zones");
    }

    /// <summary>تخلیه اضطراری: همه درب‌های فعال یک منطقه به حالت باز دائم + دستور unlock به دستگاه‌ها + هشدار بحرانی — فاز ۲۹.</summary>
    [HttpPost]
    [HasPerm("access.zones.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmergencyOpen(string zoneId)
    {
        var zone = await Db.Zones.Include(z => z.Gates).FirstOrDefaultAsync(z => z.Id == zoneId);
        if (zone is null) return NotFound();
        var gates = zone.Gates.Where(g => g.Status == "فعال").ToList();
        foreach (var g in gates)
        {
            g.DoorState = "باز دائم";
            if (g.DeviceId is not null)
                await DeviceOps.QueueCommandAsync(Db, g.DeviceId, "unlock", new { seconds = 3600, reason = "تخلیه اضطراری" }, Sess);
        }
        Base.SecurityAlerts.Add(new SecurityAlert
        {
            Type = "تخلیه اضطراری", Severity = "بحرانی",
            Message = $"تخلیه اضطراری منطقه «{zone.Name}» توسط {Sess.FullName} — {Jalali.Num(gates.Count)} درب به حالت باز دائم رفت",
            Source = "کنترل تردد",
        });
        await Db.SaveChangesAsync();
        await Base.SaveChangesAsync();
        await Audit("emergency-open", "zone", zone.Id, new { gates = gates.Count });
        var msg = $"تخلیه اضطراری: {Jalali.Num(gates.Count)} درب منطقه «{zone.Name}» باز شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Access?tab=zones");
    }

    /// <summary>پایان حالت اضطراری: بازگردانی همه درب‌های منطقه به کنترل عادی — فاز ۲۹.</summary>
    [HttpPost]
    [HasPerm("access.zones.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreNormal(string zoneId)
    {
        var zone = await Db.Zones.Include(z => z.Gates).FirstOrDefaultAsync(z => z.Id == zoneId);
        if (zone is null) return NotFound();
        var gates = zone.Gates.Where(g => g.DoorState != "نرمال").ToList();
        foreach (var g in gates)
        {
            g.DoorState = "نرمال";
            if (g.DeviceId is not null)
                await DeviceOps.QueueCommandAsync(Db, g.DeviceId, "enable_device", null, Sess);
        }
        await Db.SaveChangesAsync();
        await Audit("restore-normal", "zone", zone.Id, new { gates = gates.Count });
        var msg = gates.Count == 0
            ? $"همه درب‌های منطقه «{zone.Name}» در حالت نرمال هستند"
            : $"{Jalali.Num(gates.Count)} درب منطقه «{zone.Name}» به حالت نرمال بازگشت";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Access?tab=zones");
    }

    [HttpPost]
    [HasPerm("access.zones.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateZone(string name, string code, string riskLevel, string? description, string? companyId,
        bool twofactor = false, int? twoFactorWindow = null)
    {
        var company = await Db.Companies.FindAsync(companyId ?? "")
                      ?? await Db.Companies.OrderBy(c => c.Code).FirstOrDefaultAsync();
        if (company is null)
        { ToastErr("ابتدا یک شرکت تعریف کنید"); if (Ajax) return AjaxResult(false, "ابتدا یک شرکت تعریف کنید"); return Redirect("/Access"); }
        if (await Db.Zones.AnyAsync(z => z.Code == code && z.CompanyId == company.Id))
        { ToastErr("کد منطقه در این شرکت تکراری است"); if (Ajax) return AjaxResult(false, "کد منطقه در این شرکت تکراری است"); return Redirect("/Access"); }
        var zone = new Zone
        {
            Name = name.Trim(), Code = code.Trim().ToUpperInvariant(), RiskLevel = riskLevel,
            Description = description, CompanyId = company.Id,
            TwoFactor = twofactor,
            TwoFactorWindowSec = twoFactorWindow is > 0 and <= 600 ? twoFactorWindow.Value : 30,
        };
        Db.Zones.Add(zone);
        await Db.SaveChangesAsync();
        await Audit("create", "zone", zone.Id, new { zone.Name, company = company.Name });
        ToastOk(HrT.F("منطقه «{0}» در شرکت «{1}» ایجاد شد", zone.Name, company.Name));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access");
    }

    [HttpPost]
    [HasPerm("access.gates.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGate(string name, string code, string zoneId, string? deviceId, string type, string direction, bool feedsAttendance = false)
    {
        var zone = await Db.Zones.FindAsync(zoneId);
        if (zone is null)
        { ToastErr("منطقه انتخاب‌شده یافت نشد"); if (Ajax) return AjaxResult(false, "منطقه انتخاب‌شده یافت نشد"); return Redirect("/Access"); }
        if (await Db.Gates.AnyAsync(g => g.Code == code && g.CompanyId == zone.CompanyId))
        { ToastErr("کد گیت در این شرکت تکراری است"); if (Ajax) return AjaxResult(false, "کد گیت در این شرکت تکراری است"); return Redirect("/Access"); }
        // شرکت گیت از منطقه می‌آید — گیت همیشه هم‌شرکت منطقه است
        // فاز ۱۳۱ (مورد ۱۲) — feedsAttendance: اسکن مجاز این گیت به کارت تردد هم برود
        var gate = new Gate { Name = name.Trim(), Code = code.Trim().ToUpperInvariant(), ZoneId = zoneId, DeviceId = deviceId, Type = type, Direction = direction, CompanyId = zone.CompanyId, FeedsAttendance = feedsAttendance };
        Db.Gates.Add(gate);
        await Db.SaveChangesAsync();
        await Audit("create", "gate", gate.Id, new { gate.Name, zone = zone.Name });
        ToastOk(HrT.F("گیت «{0}» در منطقه «{1}» ایجاد شد", gate.Name, zone.Name));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access");
    }

    [HttpPost]
    [HasPerm("access.perms.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePerm(string? employeeId, string zoneId, string type,
        string? validUntil, string? days, string? timeFrom, string? timeUntil)
    {
        var perm = new AccessPermission
        {
            EmployeeId = employeeId, ZoneId = zoneId, Type = type,
            Days = days, TimeFrom = timeFrom, TimeUntil = timeUntil,
            ValidFrom = DateTime.Now,
            ValidUntil = string.IsNullOrEmpty(validUntil) ? null : DateTime.Parse(validUntil),
            Status = "فعال",
        };
        Db.AccessPermissions.Add(perm);
        await Db.SaveChangesAsync();
        await Audit("create", "access_permission", perm.Id, new { employeeId, zoneId });
        ToastOk("مجوز دسترسی ثبت شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=perms");
    }

    [HttpPost]
    [HasPerm("access.perms.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePerm(string id)
    {
        var perm = await Db.AccessPermissions.FindAsync(id);
        if (perm is null) return NotFound();
        perm.Status = perm.Status == "فعال" ? "لغوشده" : "فعال";
        perm.UpdatedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit("update", "access_permission", id, new { perm.Status });
        ToastOk(HrT.F("مجوز {0}", HrT.T(perm.Status ?? "")));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=perms");
    }

    [HttpPost]
    [HasPerm("access.alerts.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveAlert(string id)
    {
        var alert = await Base.SecurityAlerts.FindAsync(id);
        if (alert is null) return NotFound();
        alert.Resolved = true;
        alert.UpdatedAt = DateTime.Now;
        await Base.SaveChangesAsync();
        await Audit("resolve", "security_alert", id, new { alert.Type });
        ToastOk("هشدار حل‌شده علامت‌گذاری شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=alerts");
    }

    // ================= فاز ۱۷: کنترل دسترسی پیشرفته =================

    /// <summary>فعال/غیرفعال‌سازی آنتی‌پس‌بک منطقه.</summary>
    [HttpPost]
    [HasPerm("access.zones.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAntiPassback(string id)
    {
        var zone = await Db.Zones.FindAsync(id);
        if (zone is null) return NotFound();
        zone.AntiPassback = !zone.AntiPassback;
        await Db.SaveChangesAsync();
        await Audit("update", "zone", id, new { antiPassback = zone.AntiPassback });
        ToastOk(HrT.F("آنت‌ی‌پس‌بک «{0}» {1} شد", zone.Name, zone.AntiPassback ? HrT.T("فعال") : HrT.T("غیرفعال")));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=zones");
    }

    /// <summary>ثبت/حذف برنامه زمانی گیت (خالی کردن = بدون محدودیت).</summary>
    [HttpPost]
    [HasPerm("access.gates.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetGateSchedule(string id, string? days, string? from, string? to)
    {
        var gate = await Db.Gates.FindAsync(id);
        if (gate is null) return NotFound();
        if (!string.IsNullOrEmpty(from) != !string.IsNullOrEmpty(to))
        {
            ToastErr("هر دو ساعت شروع و پایان را وارد کنید یا هر دو را خالی بگذارید");
            if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? "خطا");
            return Redirect("/Access?tab=zones");
        }
        gate.ScheduleDays = string.IsNullOrWhiteSpace(days) ? null : days.Trim();
        gate.ScheduleFrom = string.IsNullOrWhiteSpace(from) ? null : from.Trim();
        gate.ScheduleTo = string.IsNullOrWhiteSpace(to) ? null : to.Trim();
        await Db.SaveChangesAsync();
        await Audit("update", "gate", id, new { days, from, to });
        ToastOk(HrT.F("برنامه زمانی گیت «{0}» ذخیره شد", gate.Name));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=zones");
    }

    [HttpPost]
    [HasPerm("access.groups.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(string name, string? note, string? companyId)
    {
        if (string.IsNullOrWhiteSpace(name)) { ToastErr("نام گروه الزامی است"); if (Ajax) return AjaxResult(false, "نام گروه الزامی است"); return Redirect("/Access?tab=groups"); }
        var company = await Db.Companies.FindAsync(companyId ?? "")
                      ?? await Db.Companies.OrderBy(c => c.Code).FirstOrDefaultAsync();
        if (company is null) { ToastErr("ابتدا یک شرکت تعریف کنید"); if (Ajax) return AjaxResult(false, "ابتدا یک شرکت تعریف کنید"); return Redirect("/Access?tab=groups"); }
        var g = new AccessGroup { Name = name.Trim(), Note = note, CompanyId = company.Id };
        Db.AccessGroups.Add(g);
        await Db.SaveChangesAsync();
        await Audit("create", "access_group", g.Id, new { g.Name, company = company.Name });
        ToastOk(HrT.F("گروه «{0}» در شرکت «{1}» ایجاد شد", g.Name, company.Name));
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=groups");
    }

    [HttpPost]
    [HasPerm("access.groups.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(string id)
    {
        var g = await Db.AccessGroups.FindAsync(id);
        if (g is null) return NotFound();
        Db.AccessGroupMembers.RemoveRange(Db.AccessGroupMembers.Where(m => m.GroupId == id));
        Db.AccessGroupGates.RemoveRange(Db.AccessGroupGates.Where(x => x.GroupId == id));
        Db.AccessGroups.Remove(g);
        await Db.SaveChangesAsync();
        await Audit("delete", "access_group", id, new { g.Name });
        ToastOk("گروه حذف شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=groups");
    }

    [HttpPost]
    [HasPerm("access.groups.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGroupMember(string groupId, string employeeId)
    {
        if (await Db.AccessGroupMembers.AnyAsync(m => m.GroupId == groupId && m.EmployeeId == employeeId))
        { ToastErr("این پرسنل قبلاً به گروه اضافه شده است"); if (Ajax) return AjaxResult(false, "این پرسنل قبلاً به گروه اضافه شده است"); return Redirect("/Access?tab=groups"); }
        var m = new AccessGroupMember { GroupId = groupId, EmployeeId = employeeId };
        Db.AccessGroupMembers.Add(m);
        await Db.SaveChangesAsync();
        await Audit("create", "access_group_member", m.Id, new { groupId, employeeId });
        ToastOk("پرسنل به گروه اضافه شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=groups");
    }

    [HttpPost]
    [HasPerm("access.groups.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGroupMember(string id)
    {
        var m = await Db.AccessGroupMembers.FindAsync(id);
        if (m is null) return NotFound();
        Db.AccessGroupMembers.Remove(m);
        await Db.SaveChangesAsync();
        await Audit("delete", "access_group_member", id, null);
        ToastOk("پرسنل از گروه حذف شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=groups");
    }

    /// <summary>افزودن/به‌روزرسانی گیت مجاز گروه با پنجره زمانی اختصاصی.</summary>
    [HttpPost]
    [HasPerm("access.groups.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetGroupGate(string groupId, string gateId, string? days, string? timeFrom, string? timeUntil)
    {
        if (!string.IsNullOrEmpty(timeFrom) != !string.IsNullOrEmpty(timeUntil))
        {
            ToastErr("هر دو ساعت شروع و پایان را وارد کنید یا هر دو را خالی بگذارید");
            if (Ajax) return AjaxResult(false, (string?)TempData["ToastErr"] ?? "خطا");
            return Redirect("/Access?tab=groups");
        }
        var gg = await Db.AccessGroupGates.FirstOrDefaultAsync(x => x.GroupId == groupId && x.GateId == gateId);
        if (gg is null)
        {
            gg = new AccessGroupGate { GroupId = groupId, GateId = gateId };
            Db.AccessGroupGates.Add(gg);
        }
        gg.Days = string.IsNullOrWhiteSpace(days) ? null : days.Trim();
        gg.TimeFrom = string.IsNullOrWhiteSpace(timeFrom) ? null : timeFrom.Trim();
        gg.TimeUntil = string.IsNullOrWhiteSpace(timeUntil) ? null : timeUntil.Trim();
        await Db.SaveChangesAsync();
        await Audit("update", "access_group_gate", gg.Id, new { groupId, gateId, days, timeFrom, timeUntil });
        ToastOk("گیت گروه ذخیره شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=groups");
    }

    [HttpPost]
    [HasPerm("access.groups.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGroupGate(string id)
    {
        var gg = await Db.AccessGroupGates.FindAsync(id);
        if (gg is null) return NotFound();
        Db.AccessGroupGates.Remove(gg);
        await Db.SaveChangesAsync();
        await Audit("delete", "access_group_gate", id, null);
        ToastOk("گیت از گروه حذف شد");
        if (Ajax)
        {
            var isOk = TempData["ToastErr"] is null;
            var msg = (string?)(TempData["ToastOk"] ?? TempData["ToastErr"]) ?? (isOk ? "انجام شد" : "خطا");
            return AjaxResult(isOk, msg);
        }
        return Redirect("/Access?tab=groups");
    }

    /// <summary>شمارش اضطراری (muster) — افراد داخل هر منطقه.</summary>
    [HttpGet]
    [HasPerm("access.view")]
    public async Task<IActionResult> Muster()
    {
        var muster = await AccessControl.MusterAsync(Db);
        ViewBag.Muster = muster;
        ViewBag.Total = muster.Sum(x => x.Count);
        return View();
    }

    // ================= فاز ۳۰: نقشه سایت + دوعاملی اختیاری + TTL هشدار + PDF تردد =================

    /// <summary>
    /// فعال/غیرفعال‌سازی احراز دومرحله‌ای منطقه — کاملاً اختیاری و فقط برای همین منطقه.
    /// پیش‌فرض همه مناطق خاموش است و هیچ الزام سراسری وجود ندارد.
    /// </summary>
    [HttpPost]
    [HasPerm("access.gates.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTwoFactor(string id)
    {
        var zone = await Db.Zones.FindAsync(id);
        if (zone is null) return NotFound();
        zone.TwoFactor = !zone.TwoFactor;
        await Db.SaveChangesAsync();
        await Audit("update", "zone", id, new { twoFactor = zone.TwoFactor, windowSec = zone.TwoFactorWindowSec });
        var msg = zone.TwoFactor
            ? $"احراز دومرحله‌ای «{zone.Name}» فعال شد (اختیاری — فقط همین منطقه): کارت + اثر انگشت/چهره در {Jalali.Num(zone.TwoFactorWindowSec)} ثانیه"
            : $"احراز دومرحله‌ای «{zone.Name}» خاموش شد (تک‌عاملی)";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Access?tab=zones");
    }

    /// <summary>تنظیم پنجره زمانی دوعاملی منطقه (ثانیه).</summary>
    [HttpPost]
    [HasPerm("access.gates.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTwoFactorWindow(string id, int windowSec)
    {
        var zone = await Db.Zones.FindAsync(id);
        if (zone is null) return NotFound();
        zone.TwoFactorWindowSec = Math.Clamp(windowSec, 5, 600);
        await Db.SaveChangesAsync();
        await Audit("update", "zone", id, new { twoFactorWindowSec = zone.TwoFactorWindowSec });
        var msg = $"پنجره دومرحله‌ای «{zone.Name}» روی {Jalali.Num(zone.TwoFactorWindowSec)} ثانیه تنظیم شد";
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Access?tab=zones");
    }

    /// <summary>
    /// ذخیره جای‌گذاری گیت‌ها روی نقشه سایت — مختصات درصدی از JSON فرانت.
    /// فقط گیت‌های موجود به‌روز می‌شوند؛ گیت ناشناس نادیده گرفته می‌شود.
    /// </summary>
    [HttpPost]
    [HasPerm("access.map.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMap(string positions)
    {
        if (string.IsNullOrWhiteSpace(positions))
            return AjaxResult(false, "داده جای‌گذاری خالی است");
        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<MapPos>>(positions,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (items is null || items.Count == 0)
                return AjaxResult(false, "داده جای‌گذاری نامعتبر است");
            int saved = 0;
            foreach (var it in items)
            {
                var gate = await Db.Gates.FirstOrDefaultAsync(g => g.Id == it.Id);
                if (gate is null) continue;
                gate.MapX = Math.Clamp(it.X, 0, 100);
                gate.MapY = Math.Clamp(it.Y, 0, 100);
                saved++;
            }
            await Db.SaveChangesAsync();
            await Audit("save-map", "gate", null, new { saved });
            return AjaxResult(true, HrT.F("جای‌گذاری {0} گیت روی نقشه ذخیره شد", HrT.Num(saved)));
        }
        catch
        {
            return AjaxResult(false, "داده جای‌گذاری قابل پردازش نبود");
        }
    }
    private record MapPos(string Id, double X, double Y);

    /// <summary>انقضای دستی یک هشدار باز (علاوه بر TTL خودکار).</summary>
    [HttpPost]
    [HasPerm("access.alerts.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExpireAlert(string id)
    {
        var alert = await Base.SecurityAlerts.FindAsync(id);
        if (alert is null) return NotFound();
        alert.Expired = true;
        alert.UpdatedAt = DateTime.Now;
        await Base.SaveChangesAsync();
        await Audit("expire", "security_alert", id, new { alert.Type });
        ToastOk("هشدار منقضی شد (از فهرست باز خارج شد — سابقه حفظ شد)");
        if (Ajax) return AjaxResult(true, "هشدار منقضی شد");
        return Redirect("/Access?tab=alerts");
    }

    /// <summary>تنظیم TTL انقضای خودکار هشدارها (روز — صفر = غیرفعال).</summary>
    [HttpPost]
    [HasPerm("access.alerts.manage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAlertTtl(int ttlDays)
    {
        var s = await NotificationEngine.GetSettingsAsync(Base);
        s.AlertTtlDays = Math.Clamp(ttlDays, 0, 365);
        s.UpdatedAt = DateTime.Now;
        await Base.SaveChangesAsync();
        var swept = await DeviceOps.SweepAlertTtlAsync(Db);
        await Audit("update", "notification_setting", s.Id, new { alertTtlDays = s.AlertTtlDays, sweptNow = swept });
        var msg = s.AlertTtlDays == 0
            ? "انقضای خودکار هشدارها غیرفعال شد"
            : $"هشدارهای باز پس از {Jalali.Num(s.AlertTtlDays)} روز خودکار منقضی می‌شوند" +
              (swept > 0 ? $" — همین حالا {Jalali.Num(swept)} هشدار قدیمی منقضی شد" : "");
        ToastOk(msg);
        if (Ajax) return AjaxResult(true, msg);
        return Redirect("/Access?tab=alerts");
    }

    /// <summary>
    /// گزارش PDF تردد به تفکیک دستگاه (فاز ۳۰) — ۳۰ روز اخیر، خروجی QuestPDF راست‌به‌چپ.
    /// </summary>
    [HttpGet]
    [HasPerm("access.view")]
    public async Task<IActionResult> TrafficPdf(string? co = null)
    {
        var from = DateTime.Now.AddDays(-30);
        var evQ = Db.AccessEvents
            .Include(e => e.Gate).ThenInclude(g => g!.Device).ThenInclude(d => d!.Company)
            .Where(e => e.Timestamp >= from);
        if (!string.IsNullOrEmpty(co)) evQ = evQ.Where(e => e.Gate!.CompanyId == co);
        var evs = await evQ.ToListAsync();

        var rows = evs
            .GroupBy(e => e.Gate?.Device)
            .Select(g =>
            {
                var d = g.Key;
                return new PdfReport.TrafficDeviceRow(
                    Device: d?.Name ?? "بدون دستگاه (گیت بدون اتصال)",
                    Serial: d?.Serial ?? "—",
                    Company: d?.Company?.Name ?? "—",
                    Gates: g.Select(x => x.Gate?.Name ?? "—").Distinct().Count(),
                    InCount: g.Count(x => x.Direction == "ورود"),
                    OutCount: g.Count(x => x.Direction == "خروج"),
                    Allowed: g.Count(x => x.Result == "مجاز"),
                    Denied: g.Count(x => x.Result == "غیرمجاز"));
            })
            .OrderByDescending(r => r.InCount + r.OutCount)
            .ToList();

        var period = HrT.F("۳۰ روز اخیر — {0} تا {1}",
            HrT.Rtl ? Jalali.FmtFa(from) : from.ToString("yyyy-MM-dd"),
            HrT.Rtl ? Jalali.FmtFa(DateTime.Now) : DateTime.Now.ToString("yyyy-MM-dd"));
        await ApplyPdfThemeAsync();
        var pdf = PdfReport.BuildTrafficByDevice(HrT.T("گزارش تردد به تفکیک دستگاه"), period, rows,
            Request.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath);
        return File(pdf, "application/pdf", $"traffic-by-device-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
    }
}
