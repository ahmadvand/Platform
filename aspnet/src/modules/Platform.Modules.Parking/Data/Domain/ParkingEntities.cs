using System.ComponentModel.DataAnnotations;

namespace Platform.Modules.Parking.Domain;

// ============================================================
// لایهٔ دیتای مستقل ماژول Parking (فاز ۶۱) — entityهای اختصاصی این ماژول
// ارتباط با ماژول‌های دیگر فقط از طریق Platform.Contracts
// ============================================================

public class ParkingLot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    /// <summary>شرکت مالک پارکینگ (چندشرکتی) — گیت‌ها و مجوزها باید همین شرکت را داشته باشند.</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    /// <summary>ظرفیت کل جایگاه‌های خودرو.</summary>
    public int Capacity { get; set; } = 50;
    /// <summary>حق پارکینگ ماهانه (ریال) — ۰ یعنی رایگان.</summary>
    public decimal MonthlyFee { get; set; }
    public string? Location { get; set; }
    public bool Active { get; set; } = true;
    public List<ParkingGate> Gates { get; set; } = new();
    public List<ParkingPermit> Permits { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>گیت پارکینگ — راهبند/پلاک‌خوان متصل به یک پارکینگ؛ می‌تواند به یک دستگاه کنترل تردد وصل شود.</summary>

public class ParkingGate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    /// <summary>شرکت مالک گیت (چندشرکتی) — همیشه با شرکت پارکینگ هم‌گام است.</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string LotId { get; set; } = "";
    public ParkingLot? Lot { get; set; }
    public string? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string Type { get; set; } = "راهبند"; // راهبند | پلاک‌خوان | کارتی | درب برقی
    public string Direction { get; set; } = "ورودی"; // ورودی | خروجی | دوطرفه
    public string Status { get; set; } = "فعال"; // فعال | غیرفعال | خراب
    /// <summary>حالت کنترلی راهبند: نرمال = تصمیم موتور مجوز | باز دائم = عبور آزاد (تخلیه/سرویس) | قفل دائم = مسدود کامل.</summary>
    public string OpenState { get; set; } = "نرمال";
    public List<ParkingEvent> Events { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>خودروی پرسنل — پلاک‌محور؛ PlateKey نسخه نرمال‌شده پلاک برای یافتن یکتا و جلوگیری از ثبت تکراری است.</summary>

public class Vehicle
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>شرکت مالک خودرو — از پرسنل گرفته می‌شود (چندشرکتی).</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    /// <summary>شماره پلاک نمایشی — مثل «۱۲ع۴۷۱ ایران ۳۶» (مطابق ورودی کاربر ذخیره می‌شود).</summary>
    public string PlateNo { get; set; } = "";
    /// <summary>کلید نرمال پلاک — بدون فاصله و کلمه «ایران» (مثل «۱۲ع۴۷۱۳۶»)؛ مبنای تطبیق پلاک‌خوان و یکتایی.</summary>
    public string PlateKey { get; set; } = "";
    public string Type { get; set; } = "سواری"; // سواری | وانت | ون | موتورسیکلت
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public bool Active { get; set; } = true;
    public List<ParkingPermit> Permits { get; set; } = new();
    public List<ParkingEvent> Events { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>مجوز پارکینگ — حق تردد یک خودرو به یک پارکینگ (دائم/موقت) با جایگاه اختصاصی اختیاری.</summary>

public class ParkingPermit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string VehicleId { get; set; } = "";
    public Vehicle? Vehicle { get; set; }
    public string LotId { get; set; } = "";
    public ParkingLot? Lot { get; set; }
    public string Type { get; set; } = "دائم"; // دائم | موقت
    public DateTime ValidFrom { get; set; } = DateTime.Now;
    public DateTime? ValidUntil { get; set; }
    /// <summary>شماره جایگاه اختصاصی — خالی یعنی پارک آزاد.</summary>
    public string? SpotNo { get; set; }
    public string Status { get; set; } = "فعال"; // فعال | منقضی | لغوشده
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>رویداد پارکینگ — هر عبور خودرو از گیت (ورود/خروج) با نتیجه ارزیابی موتور مجوز؛
/// پلاک در لحظه عبور هم به‌صورت متن ذخیره می‌شود تا حتی اگر خودرو حذف/بی‌صاحب شد سابقه معتبر بماند.</summary>

public class ParkingEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string GateId { get; set; } = "";
    public ParkingGate? Gate { get; set; }
    /// <summary>دوربین پلاک‌خوان ثبت‌کننده — null یعنی ثبت دستی/کارت.</summary>
    public string? CameraId { get; set; }
    public AnprCamera? Camera { get; set; }
    public string? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    /// <summary>پلاک ثبت‌شده در لحظه عبور (متن خام پلاک‌خوان یا ورودی نگهبان).</summary>
    public string PlateText { get; set; } = "";
    /// <summary>کلید نرمال پلاک در لحظه عبور — مبنای جستجو/تطبیق گزارش‌ها.</summary>
    public string? PlateKey { get; set; }
    public string Direction { get; set; } = "ورود"; // ورود | خروج
    public string Method { get; set; } = "پلاک‌خوان"; // پلاک‌خوان | کارت | دستی
    public string Result { get; set; } = "مجاز";     // مجاز | غیرمجاز
    /// <summary>دلیل رد — بدون مجوز | مجوز منقضی | مجوز لغوشده | ورود تکراری | گیت قفل است | ظرفیت تکمیل | ناهم‌خوانی جهت.</summary>
    public string? Reason { get; set; }
    /// <summary>درصد اطمینان پلاک‌خوان در شناسایی (۱۰۰ = قطعی) — فقط روش پلاک‌خوان.</summary>
    public int? Confidence { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

// ============================================================
// فاز ۴۱ — دوربین پلاک‌خوان (ANPR) — هم‌ساختار دستگاه‌های تردد:
// احراز با SN + توکن webhook، صف دستور (بازکردن راهبند/ضربان)،
// ارسال خودکار پلاک خوانده‌شده به پل و ثبت رویداد بدون دخالت انسان.
// ============================================================

/// <summary>دوربین پلاک‌خوان — متصل به یک گیت پارکینگ؛ پلاک خوانده‌شده را به پل می‌فرستد
/// و موتور مجوز به‌طور خودکار ورود/خروج را ثبت و راهبند را باز می‌کند.</summary>

public class AnprCamera
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>شرکت مالک دوربین — همیشه هم‌شرکت گیت است (چندشرکتی).</summary>
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string GateId { get; set; } = "";
    public ParkingGate? Gate { get; set; }
    public string Brand { get; set; } = "Hikvision"; // Hikvision | Dahua | ZKTeco | UNV | Edimax
    public string? Model { get; set; }
    /// <summary>شماره سریال دوربین (SN) — شناسه احراز در پل همانند دستگاه‌های تردد.</summary>
    public string Serial { get; set; } = "";
    public string? Ip { get; set; }
    public string? Location { get; set; }
    /// <summary>جهت لاین دوربین — ورودی | خروجی | دوطرفه؛ باید با گیت هم‌خوان باشد.</summary>
    public string Direction { get; set; } = "دوطرفه";
    /// <summary>حداقل اطمینان شناسایی پلاک (٪) — خوانده‌های زیر این آستانه نادیده گرفته می‌شوند.</summary>
    public int ConfidenceThreshold { get; set; } = 70;
    /// <summary>آدرس جریان تصویر (RTSP/HTTP) برای مشاهده زنده.</summary>
    public string? StreamUrl { get; set; }
    public string Mode { get; set; } = "آنلاین"; // آنلاین | آفلاین
    public int HeartbeatInterval { get; set; } = 60;
    public DateTime? LastSeenAt { get; set; }
    public string WebhookToken { get; set; } = Guid.NewGuid().ToString("N");
    public bool Active { get; set; } = true;
    public List<ParkingEvent> Events { get; set; } = new();
    public List<AnprCommand> Commands { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>دستور صف‌شده برای دوربین پلاک‌خوان — همان الگوی صف DeviceCommand:
/// بازکردن/بستن راهبند از راه دور، ضربان، ری‌استارت.</summary>

public class AnprCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CameraId { get; set; } = "";
    public AnprCamera? Camera { get; set; }
    public string Type { get; set; } = ""; // open_barrier | close_barrier | ping | restart
    public string? Payload { get; set; }   // JSON
    public string Status { get; set; } = "pending"; // pending | delivered | acked | failed
    public string? CreatedByUserId { get; set; }
    public string? CreatedBy { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? AckedAt { get; set; }
}
