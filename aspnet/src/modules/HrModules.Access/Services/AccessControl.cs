using Platform.Web.Domain;
using HrModules.Access.Data;
using HrModules.Access.Domain;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Services;

/// <summary>
/// موتور ارزیابی دسترسی پیشرفته (فاز ۱۷):
/// ۱) برنامه زمانی گیت  ۲) مجوز فردی منطقه (AccessPermissionEvaluator — فاز ۶۱ به لایهٔ دیتای خود ماژول منتقل شد)
/// ۳) گروه دسترسی (گیت + پنجره زمانی گروه)  ۴) آنتی‌پس‌بک منطقه‌ای.
/// فاز ۶۱ — ورودی/خروجی فقط از کانتکست خود ماژول (AccessDbContext) است.
/// </summary>
public static class AccessControl
{
    public record Decision(bool Allowed, string? Reason, string Source);

    public static async Task<Decision> EvaluateAsync(
        AccessDbContext db, Employee emp, Gate gate, string direction, DateTime now, string? method = null)
    {
        // ---------- ۰) حالت کنترلی درب (فاز ۲۹) ----------
        if (gate.DoorState == "قفل دائم")
            return new Decision(false, "درب در حالت قفل دائم است", "door-state");
        if (gate.DoorState == "باز دائم")
            return new Decision(true, null, "door-state"); // عبور آزاد — بدون کنترل مجوز

        // ---------- ۱) برنامه زمانی گیت ----------
        if (!string.IsNullOrEmpty(gate.ScheduleDays) || (!string.IsNullOrEmpty(gate.ScheduleFrom) && !string.IsNullOrEmpty(gate.ScheduleTo)))
        {
            if (!IsWithinWindow(gate.ScheduleDays, gate.ScheduleFrom, gate.ScheduleTo, now))
                return new Decision(false, "گیت خارج از بازه زمانی مجاز است", "schedule");
        }

        // ---------- ۲) مجوز فردی ----------
        var perms = await db.AccessPermissions
            .Where(p => p.EmployeeId == emp.Id && p.ZoneId == gate.ZoneId)
            .ToListAsync();
        var (ok, why) = AccessPermissionEvaluator.EvaluateAccess(perms, now);

        // ---------- ۳) گروه دسترسی ----------
        if (!ok)
        {
            var groupGrant = await db.AccessGroupMembers
                .Where(m => m.EmployeeId == emp.Id)
                .Join(db.AccessGroupGates, m => m.GroupId, gg => gg.GroupId,
                    (m, gg) => new { gg.GateId, gg.Days, gg.TimeFrom, gg.TimeUntil })
                .ToListAsync();
            var grants = groupGrant.Where(g => g.GateId == gate.Id).ToList();
            foreach (var g in grants)
            {
                if (!IsWithinWindow(g.Days, g.TimeFrom, g.TimeUntil, now)) continue;
                ok = true; why = null;
                break;
            }
            if (!ok)
            {
                // اگر گیت در گروه مجاز بوده ولی پنجره زمانی رد کرده، دلیل دقیق‌تر بدهیم
                var reason2 = grants.Count > 0
                    ? "خارج از پنجره زمانی گروه دسترسی"
                    : string.IsNullOrEmpty(why) ? "فاقد مجوز دسترسی به این منطقه" : why;
                return new Decision(false, reason2, "none");
            }
        }

        // ---------- ۴) آنتی‌پس‌بک منطقه‌ای (فقط ورود) ----------
        var zone = gate.Zone ?? await db.Zones.FirstOrDefaultAsync(z => z.Id == gate.ZoneId);
        if (zone is { AntiPassback: true } && direction == "ورود")
        {
            var zoneGateIds = await db.Gates.Where(g => g.ZoneId == zone.Id).Select(g => g.Id).ToListAsync();
            var last = await db.AccessEvents
                .Where(e => e.EmployeeId == emp.Id && zoneGateIds.Contains(e.GateId))
                .OrderByDescending(e => e.Timestamp)
                .Select(e => new { e.Direction, e.Result })
                .FirstOrDefaultAsync();
            if (last is not null && last.Direction == "ورود")
                return new Decision(false, "آنت‌ی‌پس‌بک: ورود مجدد بدون خروج قبلی از منطقه", "antipassback");
        }

        // ---------- ۵) احراز دومرحله‌ای — کاملاً اختیاری (فاز ۳۰) ----------
        // فقط برای منطقه‌ای که مدیر «صریحاً» فعالش کرده (پیش‌فرض همه مناطق خاموش است).
        // عبور نیازمند دو عامل متمایز است: کارت + اثر انگشت/چهره در بازه زمانی کوتاه.
        if (zone is { TwoFactor: true } && direction == "ورود" && !string.IsNullOrEmpty(method))
        {
            var windowSec = zone.TwoFactorWindowSec is > 0 and <= 600 ? zone.TwoFactorWindowSec : 30;
            var isCard = method == "کارت";
            var isBio = method is "اثر انگشت" or "چهره";
            if (isCard || isBio)
            {
                // عامل دوم: رویدادِ هم‌شخص با روش متمایز (کارت ↔ بیومتریک) در پنجره زمانی.
                // رویداد «دوعاملیِ در انتظار» (ردِ مرحله‌ای که فقط عامل دوم کم داشت) هم به‌عنوان
                // «عامل ارائه‌شده» پذیرفته می‌شود — چون همه بررسی‌های دیگر (مجوز/برنامه/درب)
                // پیش از رسیدن به این مرحله قبلاً عبور شده‌اند.
                var secondMethods = isCard ? new[] { "اثر انگشت", "چهره" } : new[] { "کارت" };
                var zoneGateIds2 = await db.Gates.Where(g => g.ZoneId == zone.Id).Select(g => g.Id).ToListAsync();
                var cut = now.AddSeconds(-windowSec);
                var recent = await db.AccessEvents
                    .Where(e => e.EmployeeId == emp.Id &&
                                e.Timestamp >= cut && e.Timestamp <= now &&
                                zoneGateIds2.Contains(e.GateId) &&
                                secondMethods.Contains(e.Method))
                    .OrderByDescending(e => e.Timestamp)
                    .Select(e => new { e.Result, e.Reason })
                    .FirstOrDefaultAsync();
                var hasSecond = recent is not null &&
                    (recent.Result == "مجاز" || (recent.Reason is not null && recent.Reason.Contains("دومرحله‌ای")));
                if (!hasSecond)
                    return new Decision(false,
                        $"احراز دومرحله‌ای این منطقه فعال است (اختیاری — تعیین‌شده توسط مدیر): {method} + " +
                        (isCard ? "اثر انگشت/چهره" : "کارت") + $" در بازه {windowSec} ثانیه لازم است",
                        "twofactor");
            }
        }

        return new Decision(true, null, "individual");
    }

    /// <summary>بررسی پنجره زمانی مشترک (روزهای هفته ایرانی شنبه=0…جمعه=6 + بازه ساعت) — خالی = نامحدود.</summary>
    public static bool IsWithinWindow(string? days, string? from, string? to, DateTime now)
    {
        // روزها — تقویم ایرانی: شنبه = 0 … جمعه = 6
        if (!string.IsNullOrEmpty(days))
        {
            // DayOfWeek: یکشنبه=0 … شنبه=6 → تبدیل به ایندکس ایرانی
            var iranianIdx = ((int)now.DayOfWeek + 1) % 7;
            var allowedDays = days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (allowedDays.Length > 0 && !allowedDays.Contains(iranianIdx.ToString()))
                return false;
        }
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return true;

        var nowMin = now.Hour * 60 + now.Minute;
        var f = Hm(from); var t = Hm(to);
        if (f < 0 || t < 0) return true;
        if (f <= t) return nowMin >= f && nowMin <= t;
        return nowMin >= f || nowMin <= t; // بازه شبانه
    }

    private static int Hm(string s)
    {
        var p = s.Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m)) return -1;
        return h * 60 + m;
    }

    /// <summary>
    /// شمارش اضطراری (muster) — پرسنلِ داخل هر منطقه بر اساس آخرین رویداد:
    /// آخرین رویداد «ورود» (مجاز) بدون «خروج» بعدی = داخل منطقه.
    /// </summary>
    public static async Task<List<(Zone Zone, int Count, List<(Employee Emp, string GateName, DateTime At)> People)>> MusterAsync(AccessDbContext db)
    {
        var zones = await db.Zones.Include(z => z.Gates).OrderBy(z => z.Code).ToListAsync();
        var result = new List<(Zone, int, List<(Employee, string, DateTime)>)>();
        var cut = DateTime.Now.AddDays(-14);

        foreach (var z in zones)
        {
            var gateIds = z.Gates.Select(g => g.Id).ToList();
            if (gateIds.Count == 0) { result.Add((z, 0, new List<(Employee, string, DateTime)>())); continue; }

            var evs = await db.AccessEvents
                .Where(e => gateIds.Contains(e.GateId) && e.Timestamp >= cut)
                .Include(e => e.Employee)
                .OrderBy(e => e.Timestamp)
                .ToListAsync();

            var inside = new Dictionary<string, (Employee Emp, string GateName, DateTime At)>();
            foreach (var e in evs)
            {
                if (e.EmployeeId is null || e.Employee is null) continue;
                if (e.Result != "مجاز") continue;
                if (e.Direction == "ورود") inside[e.EmployeeId] = (e.Employee, e.Gate?.Name ?? "", e.Timestamp);
                else inside.Remove(e.EmployeeId);
            }
            result.Add((z, inside.Count, inside.Values.OrderByDescending(x => x.At).ToList()));
        }
        return result;
    }
}
