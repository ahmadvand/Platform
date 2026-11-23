using System.Globalization;

namespace Platform.Contracts;

// ======================================================================
// فاز ۶۱ — موتور محاسبهٔ کارکرد به‌صورت «قرارداد مشترک خالص» (بدون هیچ وابستگی)
// ----------------------------------------------------------------------
// ComputeWork پورت دقیق src/lib/att.ts است (ATT-03): دیرکرد / تعجیل /
// اضافه‌کاری / کارکرد خالص. چون هم ماژول حضور (ثبت پانچ) و هم موتور حقوق
// (کارکرد روزانه) به همین ریاضیات نیاز دارند و قانون معماری «بدون ارجاع
// مستقیم بین ماژول‌ها» است، این توابعِ خالص (بدون I/O و بدون EF) در لایهٔ
// قراردادها زندگی می‌کنند — یک منبع حقیقت واحد برای هر دو مصرف‌کننده.
// تاریخ‌ها رشتهٔ کلید میلادی «yyyy-MM-dd» هستند (همان DateKey جلالی‌سرویس).
// ======================================================================

/// <summary>پارامترهای شیفت برای محاسبه.</summary>
public class ShiftParams
{
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "16:00";
    public int BreakMinutes { get; set; } = 60;
    public int ToleranceLate { get; set; } = 10;
}

public record WorkResult(int LateMinutes, int EarlyMinutes, int WorkMinutes, int OvertimeMinutes);

public static class AttCalc
{
    private static int Hm(string min)
    {
        var p = min.Split(':');
        return int.Parse(p[0], CultureInfo.InvariantCulture) * 60 + int.Parse(p[1], CultureInfo.InvariantCulture);
    }

    private static DateTime ParseDateKey(string s) =>
        DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>زمان مورد انتظار از نیمه‌شب تاریخ — دقیقه‌ها ممکن است از ۱۴۴۰ بگذرند (روز بعد).</summary>
    private static DateTime DateAt(string dateStr, int minutes) =>
        ParseDateKey(dateStr).AddMinutes(minutes);

    public static WorkResult ComputeWork(ShiftParams? shift, DateTime? checkIn, DateTime? checkOut, string dateStr)
    {
        if (checkIn is null)
            return new WorkResult(0, 0, 0, 0);

        if (shift is null)
        {
            var raw = checkOut is null ? 0 : Math.Max(0, (checkOut.Value - checkIn.Value).TotalMinutes);
            return new WorkResult(0, 0, (int)Math.Round(raw), 0);
        }

        var startMin = Hm(shift.StartTime);
        var endMin = Hm(shift.EndTime);
        var crossesMidnight = endMin <= startMin; // شیفت شب
        var expectedEndMin = crossesMidnight ? endMin + 1440 : endMin;

        var expectedIn = DateAt(dateStr, startMin);
        var expectedOut = DateAt(dateStr, expectedEndMin);

        // تأخیر با احتساب رواداری
        var lateRaw = (checkIn.Value - expectedIn).TotalMinutes;
        var lateMinutes = (int)Math.Max(0, Math.Round(lateRaw - shift.ToleranceLate));

        if (checkOut is null)
            return new WorkResult(lateMinutes, 0, 0, 0);

        var rawWork = (checkOut.Value - checkIn.Value).TotalMinutes;
        var netWork = Math.Max(0, rawWork - shift.BreakMinutes);
        var workMinutes = (int)Math.Round(netWork);

        // تعجیل در خروج
        var earlyMinutes = 0;
        if (checkOut.Value < expectedOut)
            earlyMinutes = (int)Math.Max(0, Math.Round((expectedOut - checkOut.Value).TotalMinutes));

        // اضافه‌کاری (خروج بعد از پایان شیفت با ۱۰ دقیقه رواداری)
        var overtimeMinutes = 0;
        if (checkOut.Value > expectedOut.AddMinutes(10))
            overtimeMinutes = (int)Math.Round((checkOut.Value - expectedOut).TotalMinutes);

        return new WorkResult(lateMinutes, earlyMinutes, workMinutes, overtimeMinutes);
    }

    public static string StatusFromPunch(int lateMinutes, bool hasIn)
    {
        if (!hasIn) return "غایب";
        return lateMinutes > 0 ? "تأخیر" : "حاضر";
    }

    /// <summary>آیا این روز برای شیفت اقماری «روز کار» است؟</summary>
    public static bool IsOnDutyForCycle(string dateStr, string? cyclePattern, string anchorDate)
    {
        if (string.IsNullOrEmpty(cyclePattern)) return true;
        var parts = cyclePattern.Split('/');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var workDays) || !int.TryParse(parts[1], out var offDays))
            return true;
        var cycle = workDays + offDays;
        var diff = (int)(ParseDateKey(dateStr) - ParseDateKey(anchorDate)).TotalDays;
        var phase = ((diff % cycle) + cycle) % cycle;
        return phase < workDays;
    }

    public static string MinutesToHm(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
}
