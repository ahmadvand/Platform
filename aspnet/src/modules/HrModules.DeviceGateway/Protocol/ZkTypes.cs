// ======================================================================
// فاز ۶۳ — انواع پروتکل ZK (پورت از Models.cs سرویس ویندوزی — بدون تغییر)
// ======================================================================
namespace HrModules.DeviceGateway.Protocol;

/// <summary>کاربر خوانده‌شده از حافظه دستگاه.</summary>
public sealed record ZkUser(
    int Uid,
    string Name,
    int Privilege,
    string Password,
    string GroupId,
    string UserId,
    uint Card);

/// <summary>رکورد تردد خوانده‌شده از حافظه دستگاه.</summary>
public sealed record ZkAttendance(
    string UserId,
    DateTime Timestamp,
    int Status,
    int Punch);

/// <summary>قالب هویتی خوانده‌شده از دستگاه (اثر انگشت/چهره).</summary>
public sealed record ZkBio(int Uid, string Pin, int Index, int Valid, byte[] Template);

/// <summary>شمارش ظرفیت دستگاه (CMD_GET_FREE_SIZES).</summary>
public sealed record ZkSizes(int Users, int Fingers, int Records, int Cards, int UsersCap, int RecCap);

/// <summary>خطای پروتکل ZK.</summary>
public sealed class ZkException : Exception
{
    public ZkException(string message) : base(message) { }
    public ZkException(string message, Exception inner) : base(message, inner) { }
}
