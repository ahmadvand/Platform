// پورت‌شده از سرویس ویندوزی HrDeviceGateway (فاز ۶۳) — منطق دست‌نخورده
namespace HrModules.DeviceGateway.Protocol;

/// <summary>
/// ثابت‌های پروتکل ZKTeco — مطابق مرجع رسمی zkemsdk.c / pyzk (const.py)
/// نکته مهم: CMD_CONNECT = 1000 است (نه 2000 — عدد 2000 مقدار CMD_ACK_OK است).
/// </summary>
public static class ZkConsts
{
    // ---------- دستورها ----------
    public const ushort CmdDbRrq          = 7;    // خواندن داده بافر
    public const ushort CmdUserWrq        = 8;    // نوشتن کاربر
    public const ushort CmdUserTempRrq    = 9;    // خواندن کاربران/قالب‌ها
    public const ushort CmdUserTempWrq    = 10;   // نوشتن قالب اثر انگشت
    public const ushort CmdOptionsRrq     = 11;   // خواندن پارامتر تنظیمات
    public const ushort CmdOptionsWrq     = 12;   // نوشتن پارامتر تنظیمات
    public const ushort CmdAttlogRrq      = 13;   // خواندن رکوردهای تردد
    public const ushort CmdClearData      = 14;   // پاک‌سازی کامل داده‌ها
    public const ushort CmdClearAttlog    = 15;   // پاک‌سازی رکوردهای تردد
    public const ushort CmdClearOperlog   = 16;
    public const ushort CmdDeleteUser     = 18;   // حذف کاربر
    public const ushort CmdDeleteUserTemp = 19;
    public const ushort CmdUnlock         = 31;   // بازکردن درب
    public const ushort CmdGetFreeSizes   = 50;   // ظرفیت/شمارش‌ها
    public const ushort CmdStateRrq       = 64;   // وضعیت دستگاه
    public const ushort CmdGetTime        = 201;  // خواندن ساعت دستگاه
    public const ushort CmdSetTime        = 202;  // تنظیم ساعت دستگاه
    public const ushort CmdRegEvent       = 500;  // ثبت رویدادهای لحظه‌ای
    public const ushort CmdConnect        = 1000; // اتصال
    public const ushort CmdExit           = 1001; // قطع اتصال
    public const ushort CmdEnableDevice   = 1002;
    public const ushort CmdDisableDevice  = 1003;
    public const ushort CmdRestart        = 1004; // ری‌استارت
    public const ushort CmdPoweroff       = 1005;
    public const ushort CmdRefreshData    = 1013; // نوسازی داده‌های داخلی
    public const ushort CmdRefreshOption  = 1014;
    public const ushort CmdTestVoice      = 1017;
    public const ushort CmdGetVersion     = 1100; // نسخه فریمور
    public const ushort CmdChangeSpeed    = 1101;
    public const ushort CmdAuth           = 1102; // احراز با CommKey
    public const ushort CmdPrepareData    = 1500; // اعلام حجم داده
    public const ushort CmdData           = 1501; // بسته داده
    public const ushort CmdFreeData       = 1502; // آزادسازی بافر
    public const ushort CmdPrepareBuffer  = 1503; // (بدون مستند) آغاز خواندن بافر
    public const ushort CmdReadBuffer     = 1504; // (بدون مستند) خواندن تکه بافر

    // ---------- پاسخ‌ها ----------
    public const ushort CmdAckOk       = 2000;
    public const ushort CmdAckError    = 2001;
    public const ushort CmdAckData     = 2002;
    public const ushort CmdAckRetry    = 2003;
    public const ushort CmdAckRepeat   = 2004;
    public const ushort CmdAckUnauth   = 2005;
    public const ushort CmdAckUnknown  = 0xffff;
    public const ushort CmdAckErrorCmd = 0xfffd;

    // ---------- سرصفحه TCP ----------
    public const ushort MachinePrepareData1 = 0x5050; // "PP"
    public const ushort MachinePrepareData2 = 0x7d82; // بایت‌ها روی سیم: 50 50 82 7d

    public const ushort UshrtMax = 65535;

    // ---------- پرچم‌های رویداد لحظه‌ای ----------
    public const uint EfAttlog     = 1;
    public const uint EfFinger     = 1 << 1;
    public const uint EfEnrollUser = 1 << 2;
    public const uint EfUnlock     = 1 << 5;
    public const uint EfVerify     = 1 << 7;
    public const uint EfAlarm      = 1 << 9;

    // ---------- انواع داده در بافر ----------
    public const byte FctUser      = 5;
    public const byte FctFingerTmp = 2;
    public const byte FctAttlog    = 1;

    // ---------- سطوح دسترسی کاربر ----------
    public const int UserDefault = 0;
    public const int UserEnroller = 2;
    public const int UserManager  = 6;
    public const int UserAdmin    = 14;
}
