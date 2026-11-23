namespace LicenseStudio;

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
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvwA13EkTRJzJ2Tup" +
        "efxsIHYniJkhSJ1ZTpRh47BA/wbcyDt+FxgNpZSBFVjXdP1Gdi8e7tKVoMcA" +
        "dA/e+aDysK2d976ypbDE/I0nJ3j1/oXEnCvMWV+IauplV1j43LaQi3J2IrCs" +
        "mlIsq8gD0Wj+g6miY76EQelx5Q5xPodAS3q1zz2+NH2kh2l0MHKPFJ7SjLtk" +
        "eZF/fYXnkI6tvqiHkIN3PA82JnAlyPYQjmRP8KpSVs9M0E/ce1EEbLK4SMSk" +
        "Yec5i9i6FSxmSRAbD3cBuJiTYVHHGEVCnDnHsmzJUG0BLBU6l2qtDD8coLRy" +
        "yOhPNKqPtNTtcrN1YuLe9MsqfwIDAQAB";

}
