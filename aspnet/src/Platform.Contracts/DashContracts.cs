// ======================================================================
// فاز ۷۰ — قراردادهای مشارکت داشبورد (داشبورد داده‌محور)
// ----------------------------------------------------------------------
// خواستهٔ کاربر: «داشبورد باید از ماژول‌ها خوانده شود؛ اگر ماژولی وجود ندارد،
// نباید چیزی از آن ماژول در داشبورد باشد و به محض اضافه‌شدن ماژول، مواردش
// در داشبورد نمایش داده شود.»
//
// ادامهٔ همان قرارداد فاز ۶۸/۶۹: هاست هیچ بخش ماژولی را در داشبورد
// هاردکد نمی‌کند؛ هر ماژول «مشارکت داشبورد» خودش را با IDashContributor
// (ساکن HrModules.X(.Data).dll) اعلام می‌کند و هاست فقط تجمیع/رندر می‌کند:
//   • ماژول غایب → مشارکتش در بوت کشف نمی‌شود → هیچ KPI/نمودار/جدول/پنلی
//     از آن ماژول در داشبورد نیست (صفر ویجت خالیِ صفر).
//   • ماژول تازه‌نصب → مشارکتش در بوت بعدی کشف می‌شود → آیتم‌هایش خودکار
//     در داشبورد ظاهر می‌شوند — بدون هیچ تغییری در کد هاست.
//   • ماژول خاموش (مدیر یا مجوز) → هاست مشارکتش را فیلتر می‌کند.
//
// مسئولیت محتوا: رشته‌های نمایشی (مقدار KPI، سلول جدول، HTML پنل) را «خود
// ماژول» می‌سازد و نسبت به Encode محتوای بانکی (نام‌ها و…) خودش مسئول است —
// ویو هاست این رشته‌ها را Raw رندر می‌کند. Sortها «سراسری»اند تا ترتیب
// کارت‌ها مثل فازهای قبل بماند (حاضران، تأخیر، غایب، دستگاه، تردد گیت،
// درخواست، هشدار، مهمان).
// ======================================================================
namespace Platform.Contracts;

/// <summary>کارت KPI داشبورد (شکل ps-card قالب پارسان). Tone رنگ پاستلی:
/// t (فیروزه‌ای) / o (نارنجی) / r (قرمز) / b (آبی) / p (بنفش). Value می‌تواند
/// HTML سبک باشد (مثل «X / Y» با span کوچک) — ساخت و Encode با خود ماژول.</summary>
public sealed record DashKpi(string Key, string Icon, string Label, string Value,
    string? Sub = null, string? Href = null, string Tone = "t", int Sort = 100);

/// <summary>نمودار داشبورد — DataJson آبجکت {labels,datasets} آمادهٔ Chart.js؛
/// OptionsJson اختیاری (override پیش‌فرض: responsive + legend پایین rtl).</summary>
public sealed record DashChart(string Key, string Type, string Title, string DataJson,
    string? Hint = null, string? OptionsJson = null, int Sort = 100);

/// <summary>جدول داشبورد — Columns خالی = رندر «فهرستی» (هر ردیف یک سلول HTML،
/// مثل کارت‌های هشدار). سلول‌ها HTML آمادهٔ ماژول‌اند (Encode با خود ماژول).</summary>
public sealed record DashTable(string Key, string Title, IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string? Link = null, string? LinkText = null, string? Empty = null, int Sort = 100);

/// <summary>پنل تمام‌عرض داشبورد (مثل نوار دستگاه‌های متصل) — Html آمادهٔ ماژول.</summary>
public sealed record DashPanel(string Key, string Title, string Html,
    string? Link = null, string? LinkText = null, int Sort = 100);

/// <summary>بستهٔ مشارکت یک ماژول در داشبورد.</summary>
public sealed record DashContribution(
    IReadOnlyList<DashKpi> Kpis,
    IReadOnlyList<DashChart> Charts,
    IReadOnlyList<DashTable> Tables,
    IReadOnlyList<DashPanel> Panels)
{
    public static readonly DashContribution None = new([], [], [], []);
}

/// <summary>
/// مشارکت داشبورد یک ماژول — پیاده‌سازی در لایهٔ دیتای همان ماژول
/// (HrModules.X.Data.dll). هاست با پیمایش اسمبلی‌های پلاگین کشف می‌کند
/// (ModuleRuntime.DashContributors) — ماژول غایب = صفر مشارکت.
/// services اسکوپ جاری است: قراردادهای بین‌ماژولی و DbContext ماژول از همین.
/// </summary>
public interface IDashContributor
{
    /// <summary>کد ماژول مالک مشارکت (لاگ/ردیابی/فیلتر خاموش‌ها).</summary>
    string Module { get; }

    /// <summary>مشارکت ماژول — هر خطا نباید کل داشبورد را بشکند؛ هاست contributor
    /// خطاخورده را فقط از داشبورد این نوبت حذف می‌کند و لاگ می‌زند.</summary>
    Task<DashContribution> ContributeAsync(IServiceProvider services);
}
