using System.Text;
using System.Text.Json;
using HrModules.DeviceGateway.Runtime;
using Platform.Contracts;

// ======================================================================
// فاز ۶۳ — پل دوربین‌های پلاک‌خوان (ANPR) درون‌پردازشی (پورت AnprBridge)
// ----------------------------------------------------------------------
// دوربین‌هایی که POST سفارشی دارند، خوانش پلاک را به این مسیرها پوش می‌کنند؛
// پل نرمال می‌کند و از مسیر قراردادی (IDeviceGatewayChannel.PostPlateAsync →
// ماژول پارکینگ) به موتور مجوز می‌رساند. پاسخ (مجاز/غیرمجاز + راهبند + مالک)
// عیناً به فرستنده برمی‌گردد. شکل‌های پذیرفته‌شده مثل سرویس ویندوزی:
//   POST /anpr/plate?SN=…  |  POST /anpr/{sn}/plate
//        body: {"plate":"12ب345","direction":"in","confidence":92}
//        یا  {"reads":[{…},{…}]} یا form-urlencoded یا متن خام (کل body = پلاک)
//   GET  /anpr/ping?SN=…   |  GET /anpr/{sn}/ping  (ضربان صرف + شمار دستورات)
// ======================================================================
namespace HrModules.DeviceGateway.Bridge;

public static class AnprEndpoints
{
    public static void Map(WebApplication app)
    {
        // ---------- POST پلاک (تکی یا دسته‌ای) ----------
        void MapPlate(string pattern)
        {
            app.MapPost(pattern, async (HttpContext ctx) =>
            {
                var rt = ctx.RequestServices.GetRequiredService<GatewayRuntime>();
                var avail = ctx.RequestServices.GetRequiredService<IModuleAvailability>();
                if (!rt.Config.FullyEnabled || !avail.Enabled(GatewayWorker.ModuleCode))
                    return Results.Json(new { ok = false, error = "دروازهٔ دستگاه‌ها فعال نیست" }, statusCode: 503);

                string? sn = ctx.Request.Query["SN"].FirstOrDefault()
                          ?? ctx.Request.Query["sn"].FirstOrDefault()
                          ?? (ctx.GetRouteValue("sn") as string);
                if (string.IsNullOrEmpty(sn))
                    return Results.BadRequest(new { error = "SN دوربین الزامی است — ?SN=… یا /anpr/{sn}/plate" });

                var reads = await ParseReadsAsync(ctx);
                if (reads.Count == 0)
                    return Results.BadRequest(new { error = "پلاکی در درخواست دیده نشد — body JSON/form یا متن خام یا ?plate=" });

                // نرمال‌سازی جهت به فارسی — موتور پارکینگ «ورود»/«خروج» می‌خواهد
                foreach (var r in reads) r.Direction = NormalizeDirection(r.Direction);

                // تکی یا دسته‌ای یکجا — قرارداد GatewayPlateRead/Batch؛ سرور هر دو شکل را می‌پذیرد
                object payload = reads.Count == 1
                    ? new GatewayPlateRead(reads[0].Plate, reads[0].Direction, reads[0].Timestamp, reads[0].Confidence)
                    : new GatewayPlateBatch(reads.Select(r => new GatewayPlateRead(r.Plate, r.Direction, r.Timestamp, r.Confidence)).ToList());
                var channel = ctx.RequestServices.GetRequiredService<IDeviceGatewayChannel>();
                var (status, body) = await channel.PostPlateAsync(sn, payload);
                rt.Log("INFO", sn, $"خوانش پلاک ({reads.Count} ردیف) → پاسخ {status}");

                if (body is not null) return Results.Text(body, "application/json", Encoding.UTF8, status);
                return Results.StatusCode(status >= 400 ? status : 502);
            });
        }
        MapPlate("/anpr/plate");
        MapPlate("/anpr/{sn}/plate");

        // ---------- GET ضربان ----------
        void MapPing(string pattern)
        {
            app.MapGet(pattern, async (HttpContext ctx) =>
            {
                var rt = ctx.RequestServices.GetRequiredService<GatewayRuntime>();
                var avail = ctx.RequestServices.GetRequiredService<IModuleAvailability>();
                if (!rt.Config.FullyEnabled || !avail.Enabled(GatewayWorker.ModuleCode))
                    return Results.Json(new { ok = false, error = "دروازهٔ دستگاه‌ها فعال نیست" }, statusCode: 503);

                string? sn = ctx.Request.Query["SN"].FirstOrDefault()
                          ?? ctx.Request.Query["sn"].FirstOrDefault()
                          ?? (ctx.GetRouteValue("sn") as string);
                if (string.IsNullOrEmpty(sn))
                    return Results.BadRequest(new { error = "SN دوربین الزامی است" });

                // ضربان = فقط نگاه به صف (consume=false) — مصرف واقعی دستورات با حلقهٔ
                // دوربین در GatewayWorker است که اجرا و ACK می‌کند؛ وگرنه پینگ دستور را می‌دزدد
                int pending = 0;
                var parking = ctx.RequestServices.GetService<IParkingGatewaySink>();
                if (parking is not null)
                    pending = (await parking.PullCameraCommandsAsync(sn, consume: false)).Count;
                var live = rt.LiveOf(sn, null, "anpr");
                live.LastPoll = DateTime.Now;
                live.Transport = "anpr";
                return Results.Json(new { ok = true, pending, time = DateTime.Now });
            });
        }
        MapPing("/anpr/ping");
        MapPing("/anpr/{sn}/ping");
    }

    /// <summary>استخراج خوانش‌ها از JSON / form / متن خام / query — پورت دقیق AnprBridge.</summary>
    private static async Task<List<PlateReadData>> ParseReadsAsync(HttpContext ctx)
    {
        var reads = new List<PlateReadData>();

        // query ?plate=… (برای ساده‌ترین دوربین‌ها)
        string? qPlate = ctx.Request.Query["plate"].FirstOrDefault()
                      ?? ctx.Request.Query["Plate"].FirstOrDefault()
                      ?? ctx.Request.Query["PLATE"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(qPlate))
        {
            reads.Add(FromParts(qPlate,
                ctx.Request.Query["direction"].FirstOrDefault(),
                ctx.Request.Query["timestamp"].FirstOrDefault(),
                ctx.Request.Query["confidence"].FirstOrDefault()));
            return reads;
        }

        string contentType = ctx.Request.ContentType ?? "";
        string body;
        using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync();

        if (body.Length == 0) return reads;

        if (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in root.EnumerateArray())
                        AddIfAny(reads, FromJson(el));
                    return reads;
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // دسته‌ای: {"reads":[…]} یا {"data":{"reads":[…]}}
                    JsonElement arr = default;
                    if (root.TryGetProperty("reads", out var r1)) arr = r1;
                    else if (root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("reads", out var r2)) arr = r2;
                    if (arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in arr.EnumerateArray())
                            AddIfAny(reads, FromJson(el));
                        return reads;
                    }

                    // نام‌های رایج سازنده‌های مختلف برای فیلد پلاک
                    string? plate = Str(root, "plate", "Plate", "PLATE", "plateNo", "plateNumber", "PlateNo", "plate_number", "carNumber", "carNo");
                    if (!string.IsNullOrWhiteSpace(plate))
                    {
                        reads.Add(new PlateReadData
                        {
                            Plate = plate.Trim(),
                            Direction = Str(root, "direction", "Direction", "dir", "lane", "laneType"),
                            Timestamp = Str(root, "timestamp", "Timestamp", "time", "Time", "dateTime", "captureTime"),
                            Confidence = IntOrNull(root, "confidence", "Confidence", "score", "Score", "similarity"),
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // JSON خراب — همان متن خام را پلاک فرض می‌کنیم (پایین)
            }
        }
        else if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var form = System.Web.HttpUtility.ParseQueryString(body);
            string? plate = form["plate"] ?? form["Plate"] ?? form["PLATE"] ?? form["plateNo"] ?? form["plateNumber"];
            if (!string.IsNullOrWhiteSpace(plate))
            {
                reads.Add(FromParts(plate, form["direction"] ?? form["dir"], form["timestamp"] ?? form["time"], form["confidence"] ?? form["score"]));
            }
        }

        // متن خام — کل body یک پلاک (برای ساده‌ترین قالب‌های HTTP دوربین)
        if (reads.Count == 0)
        {
            var raw = body.Trim().Trim('"', '\n', '\r');
            if (raw.Length >= 4 && raw.Length <= 40 && !raw.Contains('<'))  // نه XML/HTML — پلاک واقعی
                reads.Add(FromParts(raw, null, null, null));
        }
        return reads;
    }

    /// <summary>خوانش پلاک — قرارداد درونی پل (پورت PlateReadData سرویس ویندوزی).</summary>
    public sealed class PlateReadData
    {
        public string Plate { get; set; } = "";
        public string? Direction { get; set; }
        public string? Timestamp { get; set; }
        public int? Confidence { get; set; }
    }

    private static PlateReadData FromJson(JsonElement el)
    {
        string? plate = Str(el, "plate", "Plate", "plateNo", "plateNumber", "plate_number");
        return new PlateReadData
        {
            Plate = (plate ?? "").Trim(),
            Direction = Str(el, "direction", "Direction", "dir", "lane", "laneType"),
            Timestamp = Str(el, "timestamp", "Timestamp", "time", "Time", "dateTime", "captureTime"),
            Confidence = IntOrNull(el, "confidence", "Confidence", "score", "Score", "similarity"),
        };
    }

    private static PlateReadData FromParts(string? plate, string? dir, string? ts, string? conf) => new()
    {
        Plate = (plate ?? "").Trim(),
        Direction = dir,
        Timestamp = ts,
        Confidence = int.TryParse(conf, out var c) ? c : null,
    };

    private static void AddIfAny(List<PlateReadData> list, PlateReadData r)
    {
        if (!string.IsNullOrWhiteSpace(r.Plate)) list.Add(r);
    }

    private static string? Str(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        return null;
    }

    private static int? IntOrNull(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
                return i;
        }
        return null;
    }

    /// <summary>جهت به فارسی — in/enter/ورودی → «ورود»، out/exit/خروجی → «خروج»، بقیه → ورود (پیش‌فرض).</summary>
    public static string NormalizeDirection(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return "ورود";
        var d = dir.Trim().ToLowerInvariant();
        if (d is "out" or "exit" or "خروج" or "خروجی" or "leave") return "خروج";
        return "ورود";
    }
}
