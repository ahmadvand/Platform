using System.Text;
using System.Text.Json;
using HrDeviceGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HrDeviceGateway.Services;

/// <summary>
/// پل دوربین‌های پلاک‌خوان (ANPR) — فاز ۴۴.
/// دوربین‌های پلاک‌خوان (Hikvision / Dahua / …) که امکان POST سفارشی دارند، خوانش پلاک را
/// به این پل پوش می‌کنند؛ پل نرمال می‌کند و با قرارداد api/anpr/webhook به سامانه مرکزی می‌رساند.
/// پاسخ سرور (مجاز/غیرمجاز + باز/بسته بودن راهبند + مالک خودرو) عیناً به فرستنده برمی‌گردد.
/// شکل‌های پذیرفته‌شده:
///   POST /anpr/plate?SN=…                 body: {"plate":"12ب345","direction":"in","confidence":92}
///                                    یا  {"reads":[{…},{…}]}   (دسته‌ای)
///                                    یا form-urlencoded (plate=…&direction=…)
///                                    یا متن خام (کل body = پلاک)
///   GET  /anpr/ping?SN=…                  ضربان صرف (برای دوربین‌هایی که فقط GET دارند)
/// مسیر جایگزین با شناسه در مسیر هم پذیرفته می‌شود: POST /anpr/{sn}/plate ، GET /anpr/{sn}/ping
/// نکته: SN دوربین باید با سریال ثبت‌شده در پنل ادمین یکی باشد.
/// </summary>
public static class AnprBridge
{
    public static void MapEndpoints(WebApplication app, ApiClient api, ILoggerFactory lf)
    {
        var log = lf.CreateLogger("AnprBridge");

        // ---------- POST پلاک (تکی یا دسته‌ای) ----------
        void MapPlate(string pattern)
        {
            app.MapPost(pattern, async (HttpContext ctx) =>
            {
                string? sn = ctx.Request.Query["SN"].FirstOrDefault()
                          ?? ctx.Request.Query["sn"].FirstOrDefault()
                          ?? (ctx.GetRouteValue("sn") as string);
                if (string.IsNullOrEmpty(sn))
                    return Results.BadRequest(new { error = "SN دوربین الزامی است — ?SN=… یا /anpr/{sn}/plate" });

                var reads = await ParseReadsAsync(ctx);
                if (reads.Count == 0)
                    return Results.BadRequest(new { error = "پلاکی در درخواست دیده نشد — body JSON/form یا متن خام یا ?plate=" });

                // نرمال‌سازی جهت به فارسی — موتور سرور «ورود»/«خروج» می‌خواهد
                foreach (var r in reads) r.Direction = NormalizeDirection(r.Direction);

                // تکی یا دسته‌ای یکجا — سرور هر دو شکل را می‌پذیرد؛ دسته‌ای پاسخ جمع می‌دهد
                object payload = reads.Count == 1 ? reads[0] : (object)new { reads };
                var (status, body) = await api.PostPlateAsync(sn, payload);

                // پاسخ سرور (موفق یا خطا) عیناً برمی‌گردد: ok/okCount/barrier/result/reason/owner یا error
                if (body is not null) return Results.Json(body.Value, statusCode: status);
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
                string? sn = ctx.Request.Query["SN"].FirstOrDefault()
                          ?? ctx.Request.Query["sn"].FirstOrDefault()
                          ?? (ctx.GetRouteValue("sn") as string);
                if (string.IsNullOrEmpty(sn))
                    return Results.BadRequest(new { error = "SN دوربین الزامی است" });

                // ضربان = فقط نگاه به صف (consume=false) — مصرف واقعی دستورات با حلقهٔ Worker
                // است که اجرا و ACK می‌کند؛ وگرنه پینگ دستور را «می‌دزدد» و دستور گم می‌شود
                var cmds = await api.CameraHandshakeAsync(sn, consume: false);
                return Results.Json(new { ok = true, pending = cmds.Count, time = DateTime.Now });
            });
        }
        MapPing("/anpr/ping");
        MapPing("/anpr/{sn}/ping");

        log.LogInformation("پل دوربین‌های پلاک‌خوان فعال است — POST /anpr/plate?SN=… + GET /anpr/ping?SN=…");
    }

    /// <summary>استخراج خوانش‌ها از JSON / form / متن خام / query — حداقل یک خوانش معتبر.</summary>
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
