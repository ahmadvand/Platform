namespace Platform.Web.Services;

// استاب برای پروب — فقط امکان کامپایل Jalali.cs مستقل از سورس اصلی
public static class HrT
{
    public static bool Rtl => true;
    public static string T(string s) => s;
    public static string F(string s, params object[] a) => string.Format(s, a);
}
