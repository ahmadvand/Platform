using System.Security.Cryptography;
using LicenseStudio;

// پروب فاز ۸۲ — رفت‌وبرگشت payload با فهرست ماژول‌ها (صدور → بررسی)
using var rsa = RSA.Create(2048);
var priv = rsa.ExportPkcs8PrivateKeyPem();
var spki = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

var draft = new LicenseDraft("ABCDE-FGHJK-LMNPQ-RSTUV-WXYZ1", "شرکت تست پارس", null, 100, "یادداشت تست", "lock", new[] { "attendance", "payroll" });
var content = LicenseIssuer.Issue(priv, draft, out var err);
Console.WriteLine("Issue: " + (content is null ? "FAILED — " + err : "OK"));

var res = LicenseInspector.Inspect(content!, spki);
Console.WriteLine("SignatureOk=" + res.SignatureOk + " ProductOk=" + res.ProductOk);
Console.WriteLine("Modules=" + string.Join(",", res.Payload?.Modules ?? Array.Empty<string>()));
Console.WriteLine("Display=" + LicenseModules.Display(res.Payload?.Modules));
Console.WriteLine("EmptyDisplay=" + LicenseModules.Display(null));

// بدون ماژول = همهٔ ماژول‌ها (سازگاری با مجوزهای قدیمی)
var draft2 = new LicenseDraft("ABCDE-FGHJK-LMNPQ-RSTUV-WXYZ1", "شرکت تست", null, null, null, null, null);
var content2 = LicenseIssuer.Issue(priv, draft2, out _);
var res2 = LicenseInspector.Inspect(content2!, spki);
Console.WriteLine("NoModules -> " + LicenseModules.Display(res2.Payload?.Modules) + " ; payload modules null = " + (res2.Payload?.Modules is null));

// کد نامعتبر باید فیلتر شود
var draft3 = new LicenseDraft("ABCDE-FGHJK-LMNPQ-RSTUV-WXYZ1", "شرکت تست", null, null, null, null, new[] { "attendance", "bogus" });
var content3 = LicenseIssuer.Issue(priv, draft3, out _);
var res3 = LicenseInspector.Inspect(content3!, spki);
Console.WriteLine("Bogus filtered -> " + string.Join(",", res3.Payload?.Modules ?? Array.Empty<string>()));
