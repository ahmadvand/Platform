using System.Drawing.Text;

namespace HrTranslationStudio;

// ======================================================================
// نقطهٔ ورود استودیو ترجمه — همان قالب استودیو مجوز (فونت وزیر جاسازی‌شده،
// فونت پیش‌فرض همهٔ پنجره‌ها؛ مسیر دوم: فونت از حافظه).
// ======================================================================
internal static class Program
{
    private static PrivateFontCollection? _vazir;
    private static PrivateFontCollection? _vazirMem; // مسیر دوم — نباید Dispose شود

    /// <summary>خانوادهٔ فونت وزیر جاسازی‌شده — اگر بارگذاری نشد null (جایگزین: Segoe UI).</summary>
    public static FontFamily? VazirFamily { get; private set; }

    /// <summary>ساخت فونت وزیر با اندازه/استایل دلخواه.</summary>
    public static Font Vazir(float size, FontStyle style = FontStyle.Regular)
        => VazirFamily is { } fam ? new Font(fam, size, style) : new Font("Segoe UI", size, style);

    /// <summary>نسخهٔ برنامه از اسمبلی.</summary>
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

    private static void LoadVazir()
    {
        try
        {
            _vazir = new PrivateFontCollection();
            var dir = Path.Combine(Path.GetTempPath(), "HrTranslationStudio-Fonts");
            Directory.CreateDirectory(dir);
            foreach (var res in FontResources)
            {
                using var s = typeof(Program).Assembly.GetManifestResourceStream(res);
                if (s is null) continue;
                var file = Path.Combine(dir, res["HrTranslationStudio.Fonts.".Length..]);
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
                _vazirMem.AddMemoryFont(ptr, bytes.Length);
            }
            VazirFamily = PickFamily(_vazirMem);
        }
        catch
        {
            VazirFamily = null;
        }
    }

    private static readonly string[] FontResources =
        { "HrTranslationStudio.Fonts.Vazirmatn-Regular.ttf", "HrTranslationStudio.Fonts.Vazirmatn-Bold.ttf" };

    private static FontFamily? PickFamily(PrivateFontCollection col)
        => col.Families.FirstOrDefault(f => f.Name.Contains("Vazir", StringComparison.OrdinalIgnoreCase))
           ?? col.Families.FirstOrDefault();
}

/// <summary>ارقام فارسی برای نمایش شمارنده‌ها.</summary>
public static class FaNum
{
    public static string ToPersianDigits(string s) => new(s.Select(c => c is >= '0' and <= '9'
        ? (char)('\u06F0' + (c - '0')) : c).ToArray());
}
