using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// ======================================================================
// ابزار تولید مجوز نصب — «سامانه یکپارچه منابع انسانی» (فاز ۴۶)
// فقط نزد تولیدکننده اجرا می‌شود؛ کلید خصوصی هرگز با مشتری منتشر نمی‌شود.
// فرمت فایل مجوز:  base64(JSON بدنه)  .  base64(امضای RSA-SHA256)
// بدنه (camelCase): product, licensee, machine, issuedAtUtc,
//                   expiresAtUtc?, maxEmployees?, notes?
// ======================================================================

if (args.Length == 0)
{
    Console.WriteLine("""
        استفاده:
          LicenseGen keygen  <دایرکتوری کلید>
          LicenseGen issue   --key <private.pem> --req <license-request.hrreq>
                             [--licensee <دارنده>] [--machine <امضا>] [--expires yyyy-MM-dd]
                             [--max-employees N] [--notes "..."] [--out license.dat]
                             [--modules core,org,devices,attendance,access,visitors,payroll,parking]
                             [--lock-mode warn|lock]   (فاز ۷۸ — غایب = پیش‌فرض سرور)
          LicenseGen show    <license.dat> [--pub <public.pem>]

        نکته (فاز ۵۰): --req فایل درخواست مشتری است که از صفحهٔ «مجوز نصب» سرور (/License)
        دانلود می‌شود؛ امضای سرور و نام شرکت از آن خوانده شده و --machine/--licensee را اختیاری می‌کند.

        مثال:
          LicenseGen issue --key keys/private.pem --req pars.hrreq \
            --expires 2027-03-20 --max-employees 500 --out pars.dat
        """);
    return 0;
}

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "keygen": return CmdKeygen(args.Skip(1).ToArray());
        case "issue": return CmdIssue(args.Skip(1).ToArray());
        case "show": return CmdShow(args.Skip(1).ToArray());
        default:
            Console.Error.WriteLine("فرمان نامعلوم: " + args[0]);
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine("خطا: " + ex.Message);
    return 1;
}

static int CmdKeygen(string[] a)
{
    var dir = a.FirstOrDefault() ?? "./keys";
    Directory.CreateDirectory(dir);
    using var rsa = RSA.Create(2048);
    var privPem = rsa.ExportPkcs8PrivateKeyPem();
    var pubPem = rsa.ExportSubjectPublicKeyInfoPem();
    var privPath = Path.Combine(dir, "private.pem");
    var pubPath = Path.Combine(dir, "public.pem");
    File.WriteAllText(privPath, privPem);
    File.WriteAllText(pubPath, pubPem);
    try { File.SetUnixFileMode(privPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { /* ویندوز */ }
    Console.WriteLine("کلید خصوصی: " + Path.GetFullPath(privPath) + "  ⚠️ محرمانه — منتشر نشود");
    Console.WriteLine("کلید عمومی: " + Path.GetFullPath(pubPath) + "  (کلید عمومی داخل برنامه جاسازی می‌شود)");
    return 0;
}

static int CmdIssue(string[] a)
{
    string? key = null, licensee = null, machine = null, notes = null, expires = null, outPath = null, reqPath = null, modsRaw = null, lockMode = null;
    int? maxEmp = null;
    for (var i = 0; i < a.Length; i++)
    {
        switch (a[i])
        {
            case "--key": key = a[++i]; break;
            case "--licensee": licensee = a[++i]; break;
            case "--machine": machine = a[++i].Trim().ToUpperInvariant(); break;
            case "--expires": expires = a[++i]; break;
            case "--max-employees": maxEmp = int.Parse(a[++i]); break;
            case "--notes": notes = a[++i]; break;
            case "--out": outPath = a[++i]; break;
            case "--req": reqPath = a[++i]; break;
            case "--modules": modsRaw = a[++i]; break; // فاز ۵۴ — سقف ماژول‌ها
            case "--lock-mode": lockMode = a[++i]; break; // فاز ۷۸ — حالت اجرای قفل
        }
    }

    // فاز ۵۰ — فایل درخواست مشتری (.hrreq): امضا + نام شرکت خودکار خوانده می‌شود
    if (!string.IsNullOrWhiteSpace(reqPath))
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(reqPath));
            var r = doc.RootElement;
            if (string.IsNullOrWhiteSpace(machine) &&
                r.TryGetProperty("machine", out var m) && m.ValueKind == JsonValueKind.String)
                machine = m.GetString()!.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(licensee) &&
                r.TryGetProperty("company", out var c) && c.ValueKind == JsonValueKind.String)
                licensee = c.GetString();
        }
        catch
        {
            Console.Error.WriteLine("فایل درخواست (--req) JSON معتبر نیست — از صفحهٔ /License سرور مشتری دانلود کنید");
            return 1;
        }
    }

    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(licensee) || string.IsNullOrWhiteSpace(machine))
    {
        Console.Error.WriteLine("--key و (--req یا --licensee + --machine) الزامی هستند");
        return 1;
    }
    if (machine.Replace("-", "").Length != 25 || !machine.Replace("-", "").All(char.IsAsciiLetterOrDigit))
    {
        Console.Error.WriteLine("اثر انگشت نامعتبر — قالب درست: XXXXX-XXXXX-XXXXX-XXXXX-XXXXX (۲۵ نویسه)");
        return 1;
    }

    using var rsa = RSA.Create();
    rsa.ImportFromPem(File.ReadAllText(key));

    var payload = new Dictionary<string, object?>
    {
        ["product"] = "HrAttendance",
        ["licensee"] = licensee,
        ["machine"] = machine,
        ["issuedAtUtc"] = DateTime.UtcNow,
        ["expiresAtUtc"] = null,
        ["maxEmployees"] = maxEmp,
        ["notes"] = notes,
    };
    if (!string.IsNullOrWhiteSpace(expires))
    {
        if (!DateTime.TryParse(expires, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var exp))
        {
            Console.Error.WriteLine("تاریخ نامعتبر — قالب: yyyy-MM-dd (میلادی)");
            return 1;
        }
        payload["expiresAtUtc"] = exp; // ۰۰:۰۰ UTC همان روز به معنای پایان اعتبار است؛ برای اعتبار کامل روز، روز بعد را بدهید
    }

    // فاز ۵۴ — سقف ماژول‌ها: فهرست کدها مثل payroll,parking — خالی/حذف‌شده = همهٔ ماژول‌ها مجاز
    var moduleList = (modsRaw ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => x.Trim().ToLowerInvariant())
        .Where(x => x.Length > 0)
        .Distinct()
        .ToArray();
    if (moduleList.Length > 0) payload["modules"] = moduleList;

    // فاز ۷۸ — حالت اجرای قفل: فقط warn/lock درج می‌شود؛ مقدار نامعتبر/خالی = حذف فیلد (پیش‌فرض سرور)
    var lm = (lockMode ?? "").Trim().ToLowerInvariant();
    if (lm is "warn" or "lock") payload["lockMode"] = lm;
    else if (lm.Length > 0)
    {
        Console.Error.WriteLine("--lock-mode فقط warn یا lock می‌پذیرد (بدون مقدار = پیش‌فرض سرور)");
        return 1;
    }

    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    var body = Encoding.UTF8.GetBytes(json);
    var sig = rsa.SignData(body, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var content = Convert.ToBase64String(body) + "." + Convert.ToBase64String(sig);

    outPath ??= "license.dat";
    File.WriteAllText(outPath, content);
    Console.WriteLine("مجوز صادر شد: " + Path.GetFullPath(outPath));
    Console.WriteLine("دارنده: " + licensee + " | ماشین: " + machine +
        (expires is null ? " | دائمی" : " | اعتبار تا: " + expires) +
        (maxEmp is null ? "" : " | سقف پرسنل: " + maxEmp) +
        (moduleList.Length == 0 ? "" : " | ماژول‌ها: " + string.Join(", ", moduleList)) +
        (lm is "warn" or "lock" ? " | حالت قفل: " + lm : ""));
    return 0;
}

static int CmdShow(string[] a)
{
    var file = a.FirstOrDefault(f => !f.StartsWith("--")) ?? "license.dat";
    var pub = "./public.pem";
    for (var i = 0; i < a.Length - 1; i++)
        if (a[i] == "--pub") pub = a[i + 1];
    if (!File.Exists(pub))
    {
        Console.Error.WriteLine("کلید عمومی یافت نشد: " + pub + " (با --pub مسیر بدهید)");
        return 1;
    }

    var content = File.ReadAllText(file).Trim();
    var parts = content.Split('.', 2);
    if (parts.Length != 2) { Console.Error.WriteLine("قالب فایل مجوز درست نیست"); return 1; }
    var body = Convert.FromBase64String(parts[0]);
    var sig = Convert.FromBase64String(parts[1]);

    using var rsa = RSA.Create();
    rsa.ImportFromPem(File.ReadAllText(pub));
    var ok = rsa.VerifyData(body, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    Console.WriteLine("امضا: " + (ok ? "معتبر ✓" : "نامعتبر ✗"));

    using var doc = JsonDocument.Parse(body);
    Console.WriteLine(JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
    return ok ? 0 : 2;
}
