using System.Security.Cryptography;
using System.Text.Json;
using LicenseStudio;

// ======================================================================
// هارنس تست لینوکسی موتور استودیو مجوز — همان کدی که GUI ویندوز اجرا می‌کند
// ======================================================================

var fail = 0;
void Check(string name, bool ok, string extra = "")
{
    Console.WriteLine((ok ? "  OK   " : "  FAIL ") + name + (extra == "" ? "" : "  — " + extra));
    if (!ok) fail++;
}

var keysDir = "/home/z/my-project/tools/keys"; // فاز ۵۱ — جفت‌کلید مرجع در tools/keys
for (var i = 0; i < args.Length - 1; i++)
    if (args[i] == "--keys") keysDir = args[i + 1];

// حالت صدور از خط فرمان — دقیقاً با همان موتور GUI (برای تست E2E سرور واقعی)
if (args.Length >= 2 && args[0] == "--issue")
{
    string? machine = null, licensee = null, expJ = null, outPath = null; int? maxEmp = null; string? notes = null;
    for (var i = 1; i < args.Length - 1; i++)
    {
        switch (args[i])
        {
            case "--machine": machine = args[i + 1]; break;
            case "--licensee": licensee = args[i + 1]; break;
            case "--expires-jalali": expJ = args[i + 1]; break;   // قالب 1406/08/03
            case "--max-employees": maxEmp = int.Parse(args[i + 1]); break;
            case "--notes": notes = args[i + 1]; break;
            case "--out": outPath = args[i + 1]; break;
        }
    }
    DateTime? expires = null;
    if (!string.IsNullOrWhiteSpace(expJ))
    {
        var seg = expJ.Split('/');
        expires = JalaliDates.EndOfDayFromJalali(int.Parse(seg[0]), int.Parse(seg[1]), int.Parse(seg[2]));
        if (expires is null) { Console.Error.WriteLine("تاریخ شمسی نامعتبر: " + expJ); return 1; }
    }
    var priv = File.ReadAllText(Path.Combine(keysDir, "private.pem"));
    var issued = LicenseIssuer.Issue(priv, new LicenseDraft(
        Fingerprint.Normalize(machine), licensee ?? "بدون نام", expires, maxEmp, notes), out var issueErr);
    if (issued is null) { Console.Error.WriteLine("خطا: " + issueErr); return 1; }
    if (!string.IsNullOrWhiteSpace(outPath)) File.WriteAllText(outPath, issued);
    Console.WriteLine(issued);
    return 0;
}

var privPem = File.ReadAllText(Path.Combine(keysDir, "private.pem"));
var pubPem = File.ReadAllText(Path.Combine(keysDir, "public.pem"));

Console.WriteLine("== راستی‌آزمایی موتور استودیو مجوز ==");

// ۱) کلید خصوصی با کلید عمومی جاسازی‌شده در برنامهٔ وب جور است؟
using (var rsaPriv = RSA.Create())
{
    rsaPriv.ImportFromPem(privPem);
    var spki = Convert.ToBase64String(rsaPriv.ExportSubjectPublicKeyInfo());
    Check("تطابق جفت‌کلید با PublicKeySpki جاسازی‌شده", spki == EmbeddedKeys.PublicKeySpki);
}
using (var rsaPub = RSA.Create())
{
    rsaPub.ImportFromPem(pubPem);
    var spki = Convert.ToBase64String(rsaPub.ExportSubjectPublicKeyInfo());
    Check("تطابق public.pem با PublicKeySpki جاسازی‌شده", spki == EmbeddedKeys.PublicKeySpki);
}

// ۲) صدور → بررسی round-trip (کلید عمومی جاسازی‌شده، مثل سرور)
var fp = "XL3YV-W36XZ-FMF9N-7ZQB4-ZW78P";
var exp = JalaliDates.EndOfDayFromJalali(1406, 8, 3);
var content = LicenseIssuer.Issue(privPem, new LicenseDraft(fp, "شرکت پارس جنوبی", exp, 500, "مجوز سالانه"), out var err);
Check("صدور مجوز", content != null, err);
var insp = LicenseInspector.Inspect(content!, EmbeddedKeys.PublicKeySpki, fp);
Check("بررسی امضا با کلید عمومی جاسازی‌شده", insp.SignatureOk && insp.Error == null, insp.Error ?? "");
Check("دارنده/سقف/یادداشت حفظ شده", insp.Payload!.Licensee == "شرکت پارس جنوبی" && insp.Payload.MaxEmployees == 500 && insp.Payload.Notes == "مجوز سالانه");
Check("تطابق ماشین", insp.MachineMatch == true);
Check("منقضی نشده (آبان ۱۴۰۶)", insp.Expired == false);
var (jy, jm, jd) = JalaliDates.ToJalali(insp.Payload!.ExpiresAtUtc!.Value);
Check("انقضا = ۳ آبان ۱۴۰۶ (پایان روز)", (jy, jm, jd) == (1406, 8, 3), $"{jy}/{jm}/{jd}");

// ۳) مجوز دائمی بدون سقف
var perm = LicenseIssuer.Issue(privPem, new LicenseDraft(fp, "شرکت دوم", null, null, null), out err);
var inspPerm = LicenseInspector.Inspect(perm!, EmbeddedKeys.PublicKeySpki);
Check("مجوز دائمی (بدون انقضا/سقف)", inspPerm.SignatureOk && inspPerm.Payload!.ExpiresAtUtc == null && inspPerm.Payload.MaxEmployees == null);

// ۴) دستکاری بدنه → امضا رد
var parts = content!.Split('.', 2);
var body = Convert.FromBase64String(parts[0]);
var json = JsonSerializer.Deserialize<Dictionary<string, object>>(body)!;
json["maxEmployees"] = 999999;
var tampered = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(json)) + "." + parts[1];
var inspBad = LicenseInspector.Inspect(tampered, EmbeddedKeys.PublicKeySpki);
Check("دستکاری بدنه → امضا رد می‌شود", !inspBad.SignatureOk && inspBad.Error!.Contains("امضا"));

// ۵) ماشین اشتباه
var inspWrong = LicenseInspector.Inspect(content, EmbeddedKeys.PublicKeySpki, "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE");
Check("اثر انگشت متفاوت → عدم تطابق", inspWrong.MachineMatch == false);

// ۶) مجوز تاریخ‌گذشته (دی ۱۴۰۳)
var past = LicenseIssuer.Issue(privPem, new LicenseDraft(fp, "قدیمی", JalaliDates.EndOfDayFromJalali(1403, 10, 15), null, null), out err);
var inspPast = LicenseInspector.Inspect(past!, EmbeddedKeys.PublicKeySpki);
Check("مجوز گذشته → منقضی", inspPast.Expired == true);

// ۷) نرمال‌سازی اثر انگشت: ارقام فارسی، فاصله، حروف کوچک
var n1 = Fingerprint.Normalize("xl3yv w36xz  fmf9n-7zqb4-zw78p");
Check("نرمال‌سازی: حروف کوچک/فاصله", n1 == fp, n1);
var n2 = Fingerprint.Normalize("XL3YVW36XZFMF9N7ZQB4ZW78P");
Check("نرمال‌سازی: بدون خط‌تیره", n2 == fp, n2);
var n3 = Fingerprint.Normalize("XL3YV-W36XZ-FMF9N-7ZQB4-ZW۷۸P".Replace('۷', '\u06F7').Replace('۸', '\u06F8'));
Check("نرمال‌سازی: ارقام فارسی", n3 == fp, n3);
Check("اعتبارسنجی طول کوتاه", !Fingerprint.IsValid("ABC"));

// ۸) تاریخ شمسی: ۳ آبان ۱۴۰۶ میلادی معادل صحیح؟ (2027-10-25)
var g = JalaliDates.FromJalali(1406, 8, 3);
Check("۳ آبان ۱۴۰۶ → ۲۰۲۷-۱۰-۲۵", g?.ToLocalTime().Date == new DateTime(2027, 10, 25), g?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "");

// ۹) حالت اجرای قفل (فاز ۷۸) — درج در payload و خواندن در بررسی
var lockLic = LicenseIssuer.Issue(privPem, new LicenseDraft(fp, "شرکت قفل", null, null, null, LicenseLockMode.Lock), out err);
Check("صدور مجوز با قفل سخت (lock)", lockLic is not null, err);
var inspLock = LicenseInspector.Inspect(lockLic!, EmbeddedKeys.PublicKeySpki);
Check("خواندن حالت قفل سخت در بررسی", inspLock.SignatureOk && inspLock.Payload!.LockMode == LicenseLockMode.Lock, inspLock.Payload?.LockMode ?? "null");
var warnLic = LicenseIssuer.Issue(privPem, new LicenseDraft(fp, "شرکت هشدار", null, null, null, LicenseLockMode.Warn), out err);
var inspWarn = LicenseInspector.Inspect(warnLic!, EmbeddedKeys.PublicKeySpki);
Check("خواندن حالت فقط‌هشدار (warn) در بررسی", inspWarn.SignatureOk && inspWarn.Payload!.LockMode == LicenseLockMode.Warn);
var defLic = LicenseIssuer.Issue(privPem, new LicenseDraft(fp, "بدون انتخاب حالت", null, null, null), out err);
var defJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Convert.FromBase64String(defLic!.Split('.', 2)[0]));
Check("بدون انتخاب حالت = فیلد lockMode درج نمی‌شود", defJson is { } dj && !dj.ContainsKey("lockMode"));
Check("نرمال‌سازی حالت: نامعتبر=null / حاشیه‌ها پاک می‌شود", LicenseLockMode.Normalize("HARD") is null && LicenseLockMode.Normalize("  warn ") == "warn");
Check("برچسب فارسی حالت‌ها", LicenseLockMode.Display("lock") == "قفل سخت" && LicenseLockMode.Display("warn") == "فقط هشدار" && LicenseLockMode.Display(null) == "پیش‌فرض سرور");

// ۱۰) خروجی با LicenseGen CLI سازگار است؟ (امضای CLI → بررسی استودیو)
Console.WriteLine();
Console.WriteLine(fail == 0 ? "همهٔ چک‌ها سبز ✓" : $"{fail} چک قرمز ✗");
return fail == 0 ? 0 : 1;
