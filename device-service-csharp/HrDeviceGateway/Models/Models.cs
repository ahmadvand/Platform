using System.Text.Json.Serialization;

namespace HrDeviceGateway.Models;

// ---------- مدل‌های سرور مرکزی (قرارداد /api) ----------

public sealed class LoginRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

public sealed class DeviceDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("serial")] public string Serial { get; set; } = "";
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("online")] public bool? Online { get; set; }
    [JsonPropertyName("mode")] public string? Mode { get; set; }
    [JsonPropertyName("ip")] public string? Ip { get; set; }
}

public sealed class DeviceListResponse
{
    [JsonPropertyName("devices")] public List<DeviceDto> Devices { get; set; } = new();
}

public sealed class CommandDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("payload")] public Dictionary<string, object?>? Payload { get; set; }
}

public sealed class CommandListResponse
{
    [JsonPropertyName("commands")] public List<CommandDto> Commands { get; set; } = new();
}

/// <summary>رویداد ارسالی به سرور (POST /api/devices/webhook)</summary>
public sealed class DeviceEvent
{
    [JsonPropertyName("SN")] public string Serial { get; set; } = "";
    [JsonPropertyName("event")] public string Event { get; set; } = "";
    [JsonPropertyName("data")] public object? Data { get; set; }
}

/// <summary>یک رکورد تردد خام دستگاه</summary>
public sealed class RawPunch
{
    [JsonPropertyName("pin")] public string Pin { get; set; } = "";
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
    [JsonPropertyName("status")] public string? Status { get; set; }
}

public sealed class PunchesPayload
{
    [JsonPropertyName("punches")] public List<RawPunch> Punches { get; set; } = new();
}

/// <summary>کاربر ثبت‌شده روی دستگاه (رویداد users — کامل: uid/سطح/کارت/رمز/گروه)</summary>
public sealed class DeviceUser
{
    [JsonPropertyName("pin")] public string Pin { get; set; } = "";
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("uid")] public int Uid { get; set; }
    [JsonPropertyName("privilege")] public int Privilege { get; set; }
    [JsonPropertyName("card")] public string? Card { get; set; }
    [JsonPropertyName("password")] public string? Password { get; set; }
    [JsonPropertyName("group")] public string? Group { get; set; }
}

public sealed class UsersPayload
{
    [JsonPropertyName("users")] public List<DeviceUser> Users { get; set; } = new();
}

/// <summary>قالب هویتی خام (اثر انگشت/چهره/کارت/رمز) — رویداد bios</summary>
public sealed class RawBio
{
    [JsonPropertyName("pin")] public string Pin { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "fp"; // fp | face | card | pin
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("valid")] public int Valid { get; set; } = 1;
    [JsonPropertyName("quality")] public int? Quality { get; set; }
    [JsonPropertyName("data")] public string? Data { get; set; } // base64 (کارت: شماره کارت)
}

public sealed class BiosPayload
{
    [JsonPropertyName("bios")] public List<RawBio> Bios { get; set; } = new();
}

public sealed class AckPayload
{
    [JsonPropertyName("commandId")] public string CommandId { get; set; } = "";
    [JsonPropertyName("ok")] public bool Ok { get; set; } = true;
    [JsonPropertyName("result")] public string? Result { get; set; }
}

public sealed class StatusPayload
{
    [JsonPropertyName("firmware")] public string? Firmware { get; set; }
    [JsonPropertyName("userCount")] public int? UserCount { get; set; }
    [JsonPropertyName("online")] public bool? Online { get; set; }
}

/// <summary>پلاک خوانده‌شدهٔ دوربین — قرارداد رویداد plate سرور (فاز ۴۴؛ شکل تکی، دسته‌ای هم پذیرفته می‌شود).</summary>
public sealed class PlateReadData
{
    [JsonPropertyName("plate")] public string Plate { get; set; } = "";

    /// <summary>«ورود» یا «خروج» — موتور سرور مقدار فارسی می‌خواهد؛ پل AnprBridge نرمال می‌کند.</summary>
    [JsonPropertyName("direction")] public string? Direction { get; set; }

    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }

    [JsonPropertyName("confidence")] public int? Confidence { get; set; }
}

// ---------- کانفیگ پویا از پنل ادمین (GET api/gateway/config — فاز ۲۱) ----------

public sealed class RemoteGatewayConfig
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("updatedAt")] public DateTime? UpdatedAt { get; set; }
    [JsonPropertyName("settings")] public RemoteGatewaySettings Settings { get; set; } = new();
    [JsonPropertyName("devices")] public List<RemoteDeviceConfig> Devices { get; set; } = new();

    /// <summary>دوربین‌های پلاک‌خوان فعال — فاز ۴۴ (سرور ملاک است؛ قالب‌های HTTP راهبند از appsettings می‌آید).</summary>
    [JsonPropertyName("cameras")] public List<RemoteCameraConfig> Cameras { get; set; } = new();
}

public sealed class RemoteGatewaySettings
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("listenPort")] public int ListenPort { get; set; } = 8081;
    [JsonPropertyName("tcpTimeoutMs")] public int TcpTimeoutMs { get; set; } = 6000;
    [JsonPropertyName("pollIntervalSec")] public int PollIntervalSec { get; set; } = 60;
    [JsonPropertyName("sweeperIntervalSec")] public int SweeperIntervalSec { get; set; } = 60;
    [JsonPropertyName("configRefreshSec")] public int ConfigRefreshSec { get; set; } = 30;
    [JsonPropertyName("logLevel")] public string LogLevel { get; set; } = "Information";
    [JsonPropertyName("defaultDevicePort")] public int DefaultDevicePort { get; set; } = 4370;
    [JsonPropertyName("defaultCommKey")] public int DefaultCommKey { get; set; } = 0;
}

/// <summary>دستگاه از دید سرور — Transport/Port/CommKey از فرم دستگاه در پنل ادمین می‌آید.</summary>
public sealed class RemoteDeviceConfig
{
    [JsonPropertyName("serial")] public string Serial { get; set; } = "";
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("transport")] public string Transport { get; set; } = "adms";
    [JsonPropertyName("ip")] public string? Ip { get; set; }
    [JsonPropertyName("port")] public int? Port { get; set; }
    [JsonPropertyName("commKey")] public int? CommKey { get; set; }
    [JsonPropertyName("pollIntervalSec")] public int PollIntervalSec { get; set; } = 60;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
}

/// <summary>دوربین پلاک‌خوان از دید سرور — فاز ۴۴ (مطابق پاسخ cameras در api/gateway/config).</summary>
public sealed class RemoteCameraConfig
{
    [JsonPropertyName("serial")] public string Serial { get; set; } = "";
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("ip")] public string? Ip { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("gateId")] public string? GateId { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("heartbeatIntervalSec")] public int HeartbeatIntervalSec { get; set; } = 30;
}

// ---------- مدل‌های پروتکل ZK ----------

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
