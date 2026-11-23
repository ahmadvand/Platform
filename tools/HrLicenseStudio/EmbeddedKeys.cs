namespace HrLicenseStudio;

// ======================================================================
// کلیدهای جاسازی‌شده در استودیو — ⚠️ فقط کلید عمومی!
// کلید خصوصی (private.pem) عمداً در سورس جاسازی نمی‌شود تا هیچ‌گاه
// وارد گیت سورس یا بستهٔ انتشار محصول نشود؛ برنامه آن را از مسیرهای
// شناخته‌شده (کنار فایل اجرایی یا پوشهٔ کاربر) و یا با انتخابگر فایل می‌خواند.
// این مقدار باید با PublicKeySpki در
//   aspnet/src/HrAttendance.Web/Services/LicenseService.cs
// یکی باشد (پس از چرخش کلید، هر دو به‌روز شوند).
// ======================================================================
internal static class EmbeddedKeys
{
    /// <summary>کلید عمومی امضای مجوز — SPKI/Base64 DER (قالب یکسان با برنامهٔ وب).
    /// فاز ۵۱ — با کلید عمومی جدید (tools/keys/public.pem) هماهنگ شد.</summary>
    public const string PublicKeySpki =
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAyZZ4UzwJHejxsCyG" +
        "8YfaszM7dyporOelak1s6SfeQYpNGBtrIYfvERz0ShoWHHHO3dglXnkxplRc" +
        "EYCQ0p2c2fyuIAiYkXNaALMYbL7YV9pS0k22Z87HyhN3lirEe4fz2xMVVAOm" +
        "lUJcnnmt+XUWEM79Imq6d/9Viqg0FbwcLWJUCNDmHlWX3ldm1IRnwJMjeSDI" +
        "eaWPDUjup1WJSXuE0h0PLB4PGNcigp2/lH2QkUfbWySOcR+cxtxeyVtTtgS9" +
        "8o9fsaP7N2cyNRzs0g4K+1oRAOcTUvKJ3OdOOKWWz7N6z/LCSYh+FDGo5U1Q" +
        "WhU03Qb7UM7g1ndIAK+EAieUgQIDAQAB";
}
