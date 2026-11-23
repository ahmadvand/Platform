// ======================================================================
// فاز ۶۷ → فاز ۶۸/۶۹ — حضور واقعی ماژول در زمان اجرا
// ----------------------------------------------------------------------
// تا فاز ۶۷ ملاک، «وجود فایل HrModules.*.Data.dll کنار اپ» بود.
// از فاز ۶۹ ملاکِ یکتا برای «همهٔ» ماژول‌ها، کشف رانتایم (HrModuleRuntime) است —
// بدون هیچ فهرست نام ماژول در اینجا؛ ماژول ذاتی (core) هم از RegisterIntrinsic
// در همین کشف حاضر است.
// ======================================================================
namespace Platform.Contracts;

public static class HrModulePresence
{
    /// <summary>ماژول با کد داده‌شده در این بسته/اجرا حاضر است؟ — کشف رانتایم یکتا.
    /// (نام کلاس/امضا برای سازگاری فراخوان‌های فازهای قبل حفظ شده است.)</summary>
    public static bool DataInstalled(string? code) => HrModuleRuntime.Installed(code);

    /// <summary>حضور یک DLL کنار اپ (AppContext.BaseDirectory) — با کش.
    /// فاز ۶۸: فقط برای سناریوهای خاص هنوز عمومی است؛ ملاک نصب ماژول = کشف رانتایم.</summary>
    public static bool Has(string dllName)
    {
        try { return File.Exists(Path.Combine(AppContext.BaseDirectory, dllName)); }
        catch { return false; }
    }
}
