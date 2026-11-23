using Platform.Web.Services;
using HrModules.Access.Domain;

namespace HrModules.Access.Data;

/// <summary>
/// فاز ۶۱ — ارزیابی مجوز دسترسی (AC-02 — پورت evaluateAccess) — قبلاً در AttEngine (Core) بود؛
/// به لایهٔ دیتای ماژول کنترل تردد منتقل شد چون ورودی‌اش AccessPermission است.
/// </summary>
public static class AccessPermissionEvaluator
{
    private static int Hm(string min)
    {
        var p = min.Split(':');
        return int.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture) * 60
             + int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
    }

    public static (bool Allowed, string? Reason) EvaluateAccess(IEnumerable<AccessPermission> perms, DateTime now)
    {
        var list = perms.ToList();
        if (list.Count == 0)
            return (false, "فاقد مجوز دسترسی به این منطقه");

        var dayIdx = ((int)now.DayOfWeek).ToString();
        var nowMin = now.Hour * 60 + now.Minute;

        foreach (var p in list)
        {
            if (p.Status != "فعال") continue;
            if (p.ValidFrom != default && now < p.ValidFrom) continue;
            if (p.ValidUntil is not null && now > p.ValidUntil) continue;
            if (!string.IsNullOrEmpty(p.Days))
            {
                var allowedDays = p.Days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowedDays.Length > 0 && !allowedDays.Contains(dayIdx)) continue;
            }
            if (!string.IsNullOrEmpty(p.TimeFrom) && !string.IsNullOrEmpty(p.TimeUntil))
            {
                var from = Hm(p.TimeFrom);
                var to = Hm(p.TimeUntil);
                if (from <= to)
                {
                    if (nowMin < from || nowMin > to) continue;
                }
                else
                {
                    // بازه شبانه
                    if (nowMin < from && nowMin > to) continue;
                }
            }
            return (true, null);
        }
        return (false, "خارج از ساعات/روزهای مجاز یا مجوز منقضی");
    }
}
