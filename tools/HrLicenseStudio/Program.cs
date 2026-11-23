using System.Drawing.Text;

namespace HrLicenseStudio;

internal static class Program
{
    private static PrivateFontCollection? _vazir;
    private static PrivateFontCollection? _vazirMem; // مسیر دوم (حافظه) — نباید Dispose شود

    /// <summary>خانوادهٔ فونت وزیر جاسازی‌شده — اگر بارگذاری نشد null (جایگزین: Segoe UI).</summary>
    public static FontFamily? VazirFamily { get; private set; }

    /// <summary>ساخت فونت وزیر با اندازه/استایل دلخواه — فونت پیش‌فرض همهٔ پنجره‌ها (فاز ۵۰).</summary>
    public static Font Vazir(float size, FontStyle style = FontStyle.Regular)
        => VazirFamily is { } fam ? new Font(fam, size, style) : new Font("Segoe UI", size, style);

    /// <summary>نسخهٔ برنامه از اسمبلی — تک‌منبع: csproj (فاز ۵۶؛ قبلاً نسخهٔ دستی در دو نقطه بود).</summary>
    public static string VersionText
    {
        get
        {
            var v = typeof(Program).Assembly.GetName().Version;
            return v is null ? "؟" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        LoadVazir();
        Application.Run(new MainForm());
    }

    /// <summary>
    /// بارگذاری فونت وزیر از TTFهای جاسازی‌شده (فاز ۵۰ + مقاوم‌سازی فاز ۵۶).
    /// مسیر اول: استخراج TTF به پوشهٔ Temp + AddFontFile (مطمئن‌ترین رندر).
    /// مسیر دوم (اگر Temp در دسترس نباشد یا AddFontFile شکست بخورد): AddMemoryFont مستقیم از منبع جاسازی‌شده.
    /// نکته: هر دو collection نباید Dispose شوند وگرنه FontFamily بی‌اعتبار می‌گردد — استاتیک نگه داشته می‌شوند.
    /// </summary>
    private static void LoadVazir()
    {
        try
        {
            _vazir = new PrivateFontCollection();
            var dir = Path.Combine(Path.GetTempPath(), "HrLicenseStudio-Fonts");
            Directory.CreateDirectory(dir);
            foreach (var res in FontResources)
            {
                using var s = typeof(Program).Assembly.GetManifestResourceStream(res);
                if (s is null) continue;
                var file = Path.Combine(dir, res["HrLicenseStudio.Fonts.".Length..]);
                using (var f = File.Create(file)) s.CopyTo(f);
                _vazir.AddFontFile(file);
            }
            VazirFamily = PickFamily(_vazir);
        }
        catch
        {
            VazirFamily = null;
        }

        if (VazirFamily is not null) return;

        // فاز ۵۶ — مسیر دوم: فونت مستقیم از حافظه (بدون نوشتن در Temp)
        try
        {
            _vazirMem = new PrivateFontCollection();
            foreach (var res in FontResources)
            {
                using var s = typeof(Program).Assembly.GetManifestResourceStream(res);
                if (s is null) continue;
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                var bytes = ms.ToArray();
                var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length);
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, bytes.Length);
                _vazirMem.AddMemoryFont(ptr, bytes.Length); // ptr عمداً آزاد نمی‌شود (عمر برنامه)
            }
            VazirFamily = PickFamily(_vazirMem);
        }
        catch
        {
            VazirFamily = null; // Segoe UI جایگزین می‌شود
        }
    }

    private static readonly string[] FontResources =
        { "HrLicenseStudio.Fonts.Vazirmatn-Regular.ttf", "HrLicenseStudio.Fonts.Vazirmatn-Bold.ttf" };

    private static FontFamily? PickFamily(PrivateFontCollection col)
        => col.Families.FirstOrDefault(f => f.Name.Contains("Vazir", StringComparison.OrdinalIgnoreCase))
           ?? col.Families.FirstOrDefault();
}
