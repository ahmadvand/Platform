using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HrDeviceGateway.Config;
using HrDeviceGateway.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrDeviceGateway.Services;

/// <summary>
/// کلاینت HTTP به سامانه مرکزی — پورت api.mjs:
/// ورود با Bearer (استخراج hr_at از Set-Cookie)، تلاش مجدد خودکار پس از 401.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly GatewayOptions _opt;
    private readonly ILogger<ApiClient> _log;
    private readonly object _lock = new();
    private volatile string? _accessToken;

    public ApiClient(IOptions<GatewayOptions> opt, ILogger<ApiClient> log)
    {
        _opt = opt.Value;
        _log = log;
        var handler = new SocketsHttpHandler
        {
            UseCookies = false, // کوکی‌ها دستی مدیریت می‌شوند (الگوی سرویس)
            AutomaticDecompression = DecompressionMethods.All,
        };
        _http = new HttpClient(handler) { BaseAddress = new Uri(_opt.ApiBase.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
    }

    // ---------- احراز هویت ----------

    public async Task LoginAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest { Username = _opt.Username, Password = _opt.Password }),
        };
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"ورود سرویس ناموفق: {(int)res.StatusCode} {Truncate(body, 200)}");
        }

        if (!res.Headers.TryGetValues("Set-Cookie", out var cookies))
            throw new HttpRequestException("هدر Set-Cookie در پاسخ ورود یافت نشد");

        string? token = null;
        foreach (var c in cookies)
        {
            // hr_at=...; Path=/; HttpOnly ...
            if (c.StartsWith("hr_at=", StringComparison.OrdinalIgnoreCase))
            {
                token = c.Split(';', 2)[0]["hr_at=".Length..];
            }
        }
        if (string.IsNullOrEmpty(token))
            throw new HttpRequestException("توکن دسترسی (hr_at) دریافت نشد");

        lock (_lock) { _accessToken = token; }
        _log.LogInformation("ورود سرویس پل به سامانه مرکزی موفق بود");
    }

    private string? Token { get { lock (_lock) { return _accessToken; } } }

    /// <summary>درخواست عمومی با Bearer و تلاش مجدد خودکار پس از 401.</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct, bool retry = true)
    {
        if (Token is null) await LoginAsync(ct);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var res = await _http.SendAsync(req, ct);
        if ((int)res.StatusCode == 401 && retry)
        {
            _log.LogWarning("توکن منقضی شد — ورود مجدد…");
            await LoginAsync(ct);
            var retryReq = await CloneRequestAsync(req, ct);
            return await SendAsync(retryReq, ct, retry: false);
        }
        return res;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        if (req.Content is not null)
        {
            string body = await req.Content.ReadAsStringAsync(ct);
            clone.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }
        return clone;
    }

    // ---------- فراخوانی‌های سامانه ----------

    /// <summary>دریافت کانفیگ پویا از پنل ادمین (api/gateway/config) — فاز ۲۱.</summary>
    public async Task<RemoteGatewayConfig?> GetGatewayConfigAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "api/gateway/config");
            using var res = await SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("دریافت کانفیگ پویا ناموفق: {Status}", (int)res.StatusCode);
                return null;
            }
            var opt = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return await res.Content.ReadFromJsonAsync<RemoteGatewayConfig>(opt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "خطای دریافت کانفیگ پویا از سرور");
            return null;
        }
    }

    /// <summary>فهرست دستگاه‌ها از سرور (برای بازبینی وضعیت).</summary>
    public async Task<List<DeviceDto>> GetDevicesAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/devices");
        using var res = await SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var parsed = await res.Content.ReadFromJsonAsync<DeviceListResponse>(cancellationToken: ct);
        return parsed?.Devices ?? new List<DeviceDto>();
    }

    /// <summary>دریافت دستورات صف‌شده برای دستگاه (GET handshake) — مصرف واقعی: pending → delivered.</summary>
    public async Task<List<CommandDto>> PullCommandsAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"api/devices/webhook?SN={Uri.EscapeDataString(serial)}&consume=true");
            using var res = await SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return new List<CommandDto>();
            var parsed = await res.Content.ReadFromJsonAsync<CommandListResponse>(cancellationToken: ct);
            return parsed?.Commands ?? new List<CommandDto>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "خطای دریافت دستورات {Serial}", serial);
            return new List<CommandDto>();
        }
    }

    /// <summary>
    /// فقط نمایش دستورات در انتظار (consume=false) — برای گزارش sweeper؛
    /// وضعیت دستورها را تغییر نمی‌دهد تا دستگاه آفلاین، دستور را از دست ندهد.
    /// </summary>
    public async Task<List<CommandDto>> PeekCommandsAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"api/devices/webhook?SN={Uri.EscapeDataString(serial)}&consume=false");
            using var res = await SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return new List<CommandDto>();
            var parsed = await res.Content.ReadFromJsonAsync<CommandListResponse>(cancellationToken: ct);
            return parsed?.Commands ?? new List<CommandDto>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new List<CommandDto>();
        }
    }

    /// <summary>ارسال رویداد دستگاه به سرور (POST).</summary>
    public async Task<bool> PostEventAsync(string serial, string @event, object? data, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/devices/webhook")
            {
                Content = JsonContent.Create(new DeviceEvent { Serial = serial, Event = @event, Data = data }),
            };
            using var res = await SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                _log.LogWarning("رویداد {Event} برای {Serial} رد شد: {Status}", @event, serial, (int)res.StatusCode);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "خطای ارسال رویداد {Event} برای {Serial}", @event, serial);
            return false;
        }
    }

    public Task<bool> PostStatusAsync(string serial, string? firmware, int? userCount, bool? online, CancellationToken ct = default) =>
        PostEventAsync(serial, "status", new StatusPayload { Firmware = firmware, UserCount = userCount, Online = online }, ct);

    public Task<bool> PostAckAsync(string serial, string commandId, bool ok, string result, CancellationToken ct = default) =>
        PostEventAsync(serial, "ack", new AckPayload { CommandId = commandId, Ok = ok, Result = result }, ct);

    public Task<bool> PostPunchesAsync(string serial, List<RawPunch> punches, CancellationToken ct = default) =>
        PostEventAsync(serial, "records", new PunchesPayload { Punches = punches }, ct);

    public Task<bool> PostUsersAsync(string serial, List<DeviceUser> users, CancellationToken ct = default) =>
        PostEventAsync(serial, "users", new UsersPayload { Users = users }, ct);

    public Task<bool> PostBiosAsync(string serial, List<RawBio> bios, CancellationToken ct = default) =>
        PostEventAsync(serial, "bios", new BiosPayload { Bios = bios }, ct);

    // ---------- دوربین‌های پلاک‌خوان — فاز ۴۴ (قرارداد api/anpr/webhook) ----------

    /// <summary>
    /// ضربان دوربین + تحویل دستورات صف‌شده (GET api/anpr/webhook?SN=…&consume=true).
    /// MarkSeen سمت سرور با همین فراخوانی انجام می‌شود → دوربین در پنل «آنلاین» دیده می‌شود
    /// حتی اگر خود دوربین به گیت‌وی دسترسی مستقیم نداشته باشد.
    /// توجه: مصرف دستورات فقط از حلقهٔ Worker (که اجرا + ACK می‌کند) — جاهای دیگر consume=false.
    /// </summary>
    public async Task<List<CommandDto>> CameraHandshakeAsync(string serial, bool consume = true, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"api/anpr/webhook?SN={Uri.EscapeDataString(serial)}&consume={(consume ? "true" : "false")}");
            using var res = await SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return new List<CommandDto>();
            var parsed = await res.Content.ReadFromJsonAsync<CommandListResponse>(cancellationToken: ct);
            return parsed?.Commands ?? new List<CommandDto>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "خطای ضربان دوربین {Serial}", serial);
            return new List<CommandDto>();
        }
    }

    /// <summary>
    /// ارسال پلاک خوانده‌شده به موتور مجوز پارکینگ (POST api/anpr/webhook — رویداد plate).
    /// data می‌تواند یک خوانش تکی (PlateReadData) یا دسته‌ای (anonymous { reads = [...] }) باشد —
    /// دسته‌ای یکجا می‌رود تا پاسخ سرور جمع کل (okCount/failed/unknownPlates) را بدهد.
    /// خروجی: (کد وضعیت سرور، بدنه JSON) — خطای سرور (مثل 404 دوربین ناشناس) به فرستنده پاس داده می‌شود.
    /// </summary>
    public async Task<(int Status, System.Text.Json.JsonElement? Body)> PostPlateAsync(string serial, object data, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/anpr/webhook")
            {
                Content = JsonContent.Create(new DeviceEvent { Serial = serial, Event = "plate", Data = data }),
            };
            using var res = await SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                string body = await res.Content.ReadAsStringAsync(ct);
                _log.LogWarning("پلاک برای {Serial} پذیرفته نشد: {Status} {Body}", serial, (int)res.StatusCode, Truncate(body, 200));
                try { return ((int)res.StatusCode, System.Text.Json.JsonDocument.Parse(body).RootElement); }
                catch { return ((int)res.StatusCode, null); }
            }
            return (200, await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "خطای ارسال پلاک دوربین {Serial}", serial);
            return (502, null);
        }
    }

    public Task<bool> PostCameraAckAsync(string serial, string commandId, bool ok, string result, CancellationToken ct = default) =>
        PostCameraEventAsync(serial, "ack", new AckPayload { CommandId = commandId, Ok = ok, Result = result }, ct);

    /// <summary>رویدادهای دوربین به قرارداد api/anpr/webhook می‌روند — نه api/devices/webhook (تفکیک فاز ۴۴).</summary>
    public async Task<bool> PostCameraEventAsync(string serial, string @event, object? data, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/anpr/webhook")
            {
                Content = JsonContent.Create(new DeviceEvent { Serial = serial, Event = @event, Data = data }),
            };
            using var res = await SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                _log.LogWarning("رویداد دوربین {Event} برای {Serial} رد شد: {Status}", @event, serial, (int)res.StatusCode);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "خطای ارسال رویداد دوربین {Event} برای {Serial}", @event, serial);
            return false;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
