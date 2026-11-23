using System.Security.Cryptography;

namespace Platform.Web.Services;

// ============================================================
// فاز ۶۹ — هستهٔ خالصِ هویتی دستگاه‌ها (بدون DbContext)
// ثابت‌ها/برچسب‌ها/رکوردهای خام و توابع قطعی نمونه‌سازی (کارت/قالب) در
// پلتفرم مشترک زندگی می‌کنند تا لایهٔ دیتای ماژول تجهیزات (HrModules.Devices.Data)
// — که به هاست ارجاع ندارد — هم از آن‌ها استفاده کند (سید نمایشی ثبت‌های هویتی).
// متدهای دیتابیسی (UpsertUsers/UpsertBios/…) در هاست (BioSync) می‌مانند —
// مصرف‌کننده‌هایشان مسیرهای خود خانوادهٔ devices‌اند (کنترلرهای ماژول/کانال دروازه).
// ============================================================

/// <summary>ثبت‌های هویتی استاندارد — کلید فنی + برچسب فارسی.</summary>
public static class BioTypes
{
    public static readonly string[] All = { "fp", "face", "card", "pin", "palm", "photo" };

    public static readonly Dictionary<string, string> Labels = new()
    {
        ["fp"] = "اثر انگشت",
        ["face"] = "چهره",
        ["card"] = "کارت",
        ["pin"] = "رمز عددی",
        ["palm"] = "کف دست",
        ["photo"] = "عکس چهره",
    };

    public static string Label(string type) => Labels.GetValueOrDefault(type, type);

    private static readonly string[] Fingers =
    {
        "شست راست", "اشاره راست", "وسط راست", "حلقه راست", "خشک راست",
        "شست چپ", "اشاره چپ", "وسط چپ", "حلقه چپ", "خشک چپ",
    };

    /// <summary>برچسب اسلات — مثلا «اثر انگشت اشاره راست».</summary>
    public static string IndexLabel(string type, int index) => type switch
    {
        "fp" or "palm" => $"{Labels.GetValueOrDefault(type, type)} {Fingers[Math.Clamp(index, 0, 9)]}",
        "card" => "شماره کارت",
        "face" => "قالب چهره",
        "pin" => "رمز عددی",
        "photo" => "تصویر چهره",
        _ => $"اسلات {index}",
    };

    public static string PrivilegeLabel(int p) => p switch
    {
        14 => "مدیر دستگاه",
        6 => "سرپرست",
        2 => "ثبت‌نام‌کننده",
        _ => "عادی",
    };
}

/// <summary>کاربر خام خوانده‌شده از دستگاه (رویداد users).</summary>
public record RawDeviceUser(int Uid, string Pin, string Name, int Privilege, string? Card, string? Password, string? Group);

/// <summary>قالب هویتی خام خوانده‌شده از دستگاه (رویداد bios).</summary>
public record RawDeviceBio(string Pin, string Type, int Index, int Valid, int? Quality, string? Data, int Length);

public record UserUpsertResult(int Total, int Created, int Updated, int Linked, List<string> NewEmployeeCodes);

public record BioUpsertResult(int Total, int DeviceCreated, int DeviceUpdated, int CanonicalCreated, int CanonicalUpdated, List<string> EmployeeIds);

/// <summary>توابع قطعی نمونه‌سازی هویتی — بدون دیتابیس؛ همان خروجی‌های نسخهٔ قبلی.</summary>
public static class BioSyncCore
{
    /// <summary>شماره کارت قطعی ۸ رقمی از روی پین.</summary>
    public static string SimCardNo(string pin)
    {
        int h = 0;
        foreach (var ch in pin) h = h * 31 + ch;
        var n = 10_000_000 + (Math.Abs(h) % 89_999_999);
        return n.ToString("D8");
    }

    /// <summary>قالب باینری نمایشی (هدر ZK + بایت‌های قطعی) به base64.</summary>
    public static (string Data, int Length) FakeTemplate(string key)
    {
        byte[] seed = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        var buf = new byte[397]; // طول معمول قالب ZKFinger VX10
        // هدر متعارف قالب ZK
        buf[0] = 0x73; buf[1] = 0x5A; buf[2] = 0x44; buf[3] = 0x42; // "sZDB"
        buf[4] = 0x02; buf[5] = 0x00; buf[6] = 0x00; buf[7] = 0x00;
        for (int i = 8; i < buf.Length; i++)
            buf[i] = (byte)(seed[i % seed.Length] ^ (i * 31 & 0xFF));
        return (Convert.ToBase64String(buf), buf.Length);
    }
}
