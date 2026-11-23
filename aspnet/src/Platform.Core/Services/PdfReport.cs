using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Platform.Web.Domain;
using Platform.Web.Services;
using Platform.Contracts;
using MoneySvc = Platform.Web.Services.Money; // فاز ۹۱ — تداخل نام با متد PdfReport.Money

namespace Platform.Web.Services;

/// <summary>
/// تولید PDF گزارش تحلیلی با QuestPDF (فاز ۱۵) — فونت وزیرمتن جاسازی‌شده،
/// چیدمان راست‌به‌چپ، جدول KPI واحد سازمانی و خلاصه پرسنل.
/// </summary>
public static class PdfReport
{
    private static bool _fontsRegistered;
    private static readonly object RegLock = new();

    // ---------- تم PDF (فاز ۳۲) — هم‌راستا با پالت فعال ادمین؛ پیش‌فرض = نیلی درخشان ----------
    /// <summary>رنگ اصلی برند در PDFها — از پالت فعال ادمین پر می‌شود (رنگ تخت).</summary>
    public static string Accent { get; private set; } = "#6366f1";
    /// <summary>نسخه تیره‌تر رنگ اصلی — متن عنوان‌ها.</summary>
    public static string AccentDeep { get; private set; } = "#4f46e5";
    /// <summary>زمینه ملایم هم‌خانواده رنگ اصلی — سربرگ سلول‌ها.</summary>
    public static string AccentSoft { get; private set; } = "#eef2ff";

    /// <summary>هم‌راستاسازی تم PDF با پالت ادمین — در هر تولید PDF فراخوانی می‌شود (رنگ تخت، بدون گرادیان).
    /// بدون پالت → بازگشت قطعی به رنگ‌های پیش‌فرض (حالت ثابت نباید از درخواست قبلی بماند).</summary>
    public static void ApplyPalette(string? accent)
    {
        var hex = SettingsService.NormalizeHex(accent);
        if (hex is null)
        {
            Accent = "#6366f1";
            AccentDeep = "#4f46e5";
            AccentSoft = "#eef2ff";
            return;
        }
        Accent = hex;
        AccentDeep = SettingsService.Mix(hex, "#000000", 0.14);
        AccentSoft = SettingsService.Mix(hex, "#ffffff", 0.94);
    }

    public static void EnsureFonts(string webRoot)
    {
        if (_fontsRegistered) return;
        lock (RegLock)
        {
            if (_fontsRegistered) return;
            var dir = Path.Combine(webRoot, "fonts", "ttf");
            QuestPDF.Settings.License = LicenseType.Community;
            FontManager.RegisterFont(File.OpenRead(Path.Combine(dir, "Vazirmatn-Regular.ttf")));
            FontManager.RegisterFont(File.OpenRead(Path.Combine(dir, "Vazirmatn-Bold.ttf")));
            _fontsRegistered = true;
        }
    }

    // فاز ۸۱ — ارقام/زمان متناسب با زبان جاری (fa: ارقام فارسی + شمسی، en: لاتین + میلادی)
    public static string Fa(long n) => HrT.Num(n);
    private static string FaHm(int minutes) => HrT.Rtl
        ? Jalali.MinutesToHm(minutes)
        : $"{minutes / 60}:{minutes % 60:00}";
    private static string T(string fa) => HrT.T(fa);
    /// <summary>تاریخ سربرگ PDF — fa: شمسی با ارقام فارسی، en: میلادی لاتین.</summary>
    private static string HeaderDate(DateTime now) => HrT.TopDate(now);

    public static byte[] BuildAnalytics(string title, string period, AttDeptKpi[] depts, AttEmpSummary[] emps, string webRoot)
    {
        EnsureFonts(webRoot);
        const string fa = "Vazirmatn";

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                // فاز ۸۱ — جهت صفحه از زبان جاری (fa: راست‌به‌چپ، en: چپ‌به‌راست)
                page.DefaultTextStyle(x => HrT.Rtl
                    ? x.FontFamily(fa).FontSize(9.5f).DirectionFromRightToLeft()
                    : x.FontFamily(fa).FontSize(9.5f));

                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().AStart().Text(T("سامانه یکپارچه منابع انسانی")).Bold().FontSize(13).FontColor(AccentDeep);
                        r.RelativeItem().AEnd().Text(HeaderDate(DateTime.Now)).FontSize(8.5f).FontColor("#6b7280");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().AStart().Text(title).Bold().FontSize(11);
                        r.RelativeItem().AEnd().Text(period).FontSize(9).FontColor("#6b7280");
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Accent);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // ---------- KPI واحد سازمانی ----------
                    col.Item().Text(T("KPI واحدهای سازمانی")).Bold().FontSize(11).FontColor(AccentDeep);
                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.2f);   // واحد
                            c.RelativeColumn(1f);     // تعداد
                            c.RelativeColumn(1.1f);   // حاضر
                            c.RelativeColumn(1f);     // غایب
                            c.RelativeColumn(1.4f);   // تأخیر
                            c.RelativeColumn(1.4f);   // اضافه‌کاری
                            c.RelativeColumn(1.2f);   // نرخ حضور
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(Th).Text(T("واحد سازمانی")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("پرسنل")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("روز حاضر")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("غایب")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("تأخیر")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("اضافه‌کاری")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("نرخ حضور")).FontColor("#ffffff").Bold().FontSize(8.5f);
                        });
                        foreach (var d in depts)
                        {
                            t.Cell().Element(Td).Text(d.Dept);
                            t.Cell().Element(Td).Text(Fa(d.Headcount));
                            t.Cell().Element(Td).Text(Fa(d.Present));
                            t.Cell().Element(Td).Text(Fa(d.Absent));
                            t.Cell().Element(Td).Text(FaHm(d.LateMinutes));
                            t.Cell().Element(Td).Text(FaHm(d.OvertimeMinutes));
                            t.Cell().Element(Td).Text(Fa((long)d.AttendanceRate) + "٪").FontColor(
                                d.AttendanceRate >= 90 ? "#0e9f6e" : d.AttendanceRate >= 75 ? "#b45309" : "#dc2648");
                        }
                    });

                    // ---------- خلاصه پرسنل ----------
                    col.Spacing(14);
                    col.Item().Text(T("خلاصه کارکرد پرسنل")).Bold().FontSize(11).FontColor(AccentDeep);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1f);    // کد
                            c.RelativeColumn(2.2f);  // نام
                            c.RelativeColumn(2f);    // واحد
                            c.RelativeColumn(1f);    // حاضر
                            c.RelativeColumn(1f);    // غایب
                            c.RelativeColumn(1.2f);  // مرخصی
                            c.RelativeColumn(1.4f);  // تأخیر
                            c.RelativeColumn(1.5f);  // کارکرد
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(Th).Text(T("کد")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("نام و نام خانوادگی")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("واحد")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("حاضر")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("غایب")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("مرخصی")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("تأخیر")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("کارکرد")).FontColor("#ffffff").Bold().FontSize(8.5f);
                        });
                        foreach (var e in emps)
                        {
                            t.Cell().Element(Td).Text(e.PersonnelCode);
                            t.Cell().Element(Td).Text(e.FullName);
                            t.Cell().Element(Td).Text(e.Dept);
                            t.Cell().Element(Td).Text(Fa(e.Present));
                            t.Cell().Element(Td).Text(e.Absent > 0 ? Fa(e.Absent) : "—").FontColor(e.Absent > 0 ? "#dc2648" : "#6b7280");
                            t.Cell().Element(Td).Text(Fa(e.LeaveDays));
                            t.Cell().Element(Td).Text(e.LateMinutes > 0 ? FaHm(e.LateMinutes) : "—");
                            t.Cell().Element(Td).Text(FaHm(e.WorkMinutes));
                        }
                        if (emps.Length == 0)
                            t.Cell().ColumnSpan(8).Element(Td).AlignCenter().Text(T("داده‌ای در این بازه یافت نشد")).FontColor("#9ca3af");
                    });
                });

                page.Footer().Row(r =>
                {
                    r.RelativeItem().AStart().Text(T("تولیدشده توسط سامانه یکپارچه منابع انسانی — فاز ۱۵")).FontSize(8).FontColor("#9ca3af");
                    r.RelativeItem().AEnd().Text(txt => { txt.Span(T("صفحه ")).FontSize(8); txt.CurrentPageNumber().FontSize(8); txt.Span(T(" از ")).FontSize(8); txt.TotalPages().FontSize(8); });
                });
            });
        }).GeneratePdf();

        static IContainer Th(IContainer c) => c.Background(AccentDeep).Padding(5);
        static IContainer Td(IContainer c) => c.BorderBottom(0.5f).BorderColor("#e5e7eb").Padding(4.5f);
    }

    // ===================== اسناد رسمی حقوق و دستمزد (فاز ۲۵) =====================

    /// <summary>سربرگ مشترک اسناد رسمی — نام سامانه، شرکت، عنوان سند و تاریخ.</summary>
    public static void DocHeader(ColumnDescriptor col, string company, string title, string subtitle)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().AStart().Text(company).Bold().FontSize(13).FontColor(AccentDeep);
            r.RelativeItem().AEnd().Text(T("سامانه یکپارچه منابع انسانی")).FontSize(8.5f).FontColor("#6b7280");
        });
        col.Item().PaddingTop(4).Row(r =>
        {
            r.RelativeItem().AStart().Text(title).Bold().FontSize(11);
            r.RelativeItem().AEnd().Text(subtitle).FontSize(9).FontColor("#6b7280");
        });
        col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Accent);
    }

    public static IContainer DocFooter(IContainer f)
    {
        f.Row(r =>
        {
            r.RelativeItem().AStart().Text(txt =>
            {
                txt.Span(T("تولیدشده توسط ")).FontSize(8).FontColor("#9ca3af");
                txt.Span(BuildInfo.Product).FontSize(8).FontColor("#9ca3af");
                txt.Span(HrT.F(" · نسخه {0}", BuildInfo.Version)).FontSize(8).FontColor("#9ca3af");
                txt.Span($" · {BuildInfo.Vendor}").FontSize(8).FontColor("#9ca3af");
            });
            r.RelativeItem().AEnd().Text(txt =>
            {
                txt.Span(T("صفحه ")).FontSize(8);
                txt.CurrentPageNumber().FontSize(8);
                txt.Span(T(" از ")).FontSize(8);
                txt.TotalPages().FontSize(8);
            });
        });
        return f;
    }

    /// <summary>فاز ۹۱ — قالب‌بندی مبلغ با ارز فعال تنظیمات (نرخ نمایشی + ارقام بومی زبان)؛
    /// نشان ارز در سرستون/برچسب‌هاست، پس عدد بدون نشان.</summary>
    public static string Money(decimal v) => MoneySvc.Fmt(v, withSym: false);

    /// <summary>گزارش رسمی «لیست حقوق و دستمزد» یک لیست — جدول پرسنل + جمع کل + محل امضا.</summary>
        public record TrafficDeviceRow(
        string Device, string Serial, string Company, int Gates,
        int InCount, int OutCount, int Allowed, int Denied);

    public static byte[] BuildTrafficByDevice(string title, string period, List<TrafficDeviceRow> rows, string webRoot)
    {
        EnsureFonts(webRoot);
        const string fa = "Vazirmatn";

        long totalIn = rows.Sum(r => r.InCount), totalOut = rows.Sum(r => r.OutCount);
        long totalOk = rows.Sum(r => r.Allowed), totalNo = rows.Sum(r => r.Denied);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(36);
                page.DefaultTextStyle(x => HrT.Rtl
                    ? x.FontFamily(fa).FontSize(9.5f).DirectionFromRightToLeft()
                    : x.FontFamily(fa).FontSize(9.5f));

                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().AStart().Text(T("سامانه یکپارچه منابع انسانی — کنترل تردد")).Bold().FontSize(13).FontColor(AccentDeep);
                        r.RelativeItem().AEnd().Text(HeaderDate(DateTime.Now)).FontSize(8.5f).FontColor("#6b7280");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().AStart().Text(title).Bold().FontSize(11);
                        r.RelativeItem().AEnd().Text(period).FontSize(9).FontColor("#6b7280");
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Accent);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // ---------- KPI کلی ----------
                    col.Item().Row(r =>
                    {
                        void Kpi(string label, string value, string color) =>
                            r.RelativeItem().PaddingHorizontal(4).Background(AccentSoft).Border(1).BorderColor("#b5e3d8")
                                .Padding(8).Column(c =>
                                {
                                    c.Item().AlignCenter().Text(value).Bold().FontSize(13).FontColor(color);
                                    c.Item().AlignCenter().Text(label).FontSize(8.5f).FontColor("#456068");
                                });
                        Kpi(T("کل رویدادها"), Fa(totalIn + totalOut), AccentDeep);
                        Kpi(T("ورود"), Fa(totalIn), "#0369a1");
                        Kpi(T("خروج"), Fa(totalOut), "#0369a1");
                        Kpi(T("مجاز"), Fa(totalOk), "#0e9f6e");
                        Kpi(T("غیرمجاز"), Fa(totalNo), "#dc2648");
                        Kpi(T("تعداد دستگاه"), Fa(rows.Count), "#b45309");
                    });

                    col.Spacing(12);

                    // ---------- جدول تفکیک دستگاه ----------
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(0.5f);  // ردیف
                            c.RelativeColumn(2.4f);  // دستگاه
                            c.RelativeColumn(1.6f);  // سریال
                            c.RelativeColumn(2f);    // شرکت
                            c.RelativeColumn(0.8f);  // گیت‌ها
                            c.RelativeColumn(0.9f);  // ورود
                            c.RelativeColumn(0.9f);  // خروج
                            c.RelativeColumn(0.9f);  // مجاز
                            c.RelativeColumn(0.9f);  // غیرمجاز
                            c.RelativeColumn(0.9f);  // جمع
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(Th).Text("#").FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("دستگاه")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("سریال")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("شرکت")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("گیت")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("ورود")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("خروج")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("مجاز")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("غیرمجاز")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(T("جمع")).FontColor("#ffffff").Bold().FontSize(8.5f);
                        });
                        int i = 0;
                        foreach (var d in rows)
                        {
                            i++;
                            t.Cell().Element(Td).Text(Fa(i));
                            t.Cell().Element(Td).Text(d.Device);
                            t.Cell().Element(Td).Text(d.Serial);
                            t.Cell().Element(Td).Text(d.Company);
                            t.Cell().Element(Td).Text(Fa(d.Gates));
                            t.Cell().Element(Td).Text(Fa(d.InCount)).FontColor("#0369a1");
                            t.Cell().Element(Td).Text(Fa(d.OutCount)).FontColor("#0369a1");
                            t.Cell().Element(Td).Text(Fa(d.Allowed)).FontColor("#0e9f6e");
                            t.Cell().Element(Td).Text(Fa(d.Denied)).FontColor(d.Denied > 0 ? "#dc2648" : "#6b7280");
                            t.Cell().Element(Td).Text(Fa((long)d.InCount + d.OutCount)).Bold();
                        }
                        // ردیف جمع کل
                        t.Cell().Element(Tf).Text("");
                        t.Cell().Element(Tf).Text(T("جمع کل")).Bold();
                        t.Cell().Element(Tf).Text("");
                        t.Cell().Element(Tf).Text("");
                        t.Cell().Element(Tf).Text("");
                        t.Cell().Element(Tf).Text(Fa(totalIn)).Bold();
                        t.Cell().Element(Tf).Text(Fa(totalOut)).Bold();
                        t.Cell().Element(Tf).Text(Fa(totalOk)).Bold();
                        t.Cell().Element(Tf).Text(Fa(totalNo)).Bold();
                        t.Cell().Element(Tf).Text(Fa(totalIn + totalOut)).Bold();
                    });

                    if (rows.Count == 0)
                    {
                        col.Spacing(12);
                        col.Item().AlignCenter().PaddingVertical(20)
                            .Text(T("در این بازه رویداد ترددی ثبت نشده است")).FontSize(10).FontColor("#6b7280");
                    }

                    col.Spacing(16);
                    col.Item().Text(T("این گزارش به‌صورت خودکار از رویدادهای گیت‌های متصل به دستگاه‌ها تولید شده است."))
                        .FontSize(8.5f).FontColor("#6b7280");
                });

                page.Footer().Row(r =>
                {
                    r.RelativeItem().AStart().Text(T("تولید شده توسط سامانه یکپارچه منابع انسانی")).FontSize(8).FontColor("#8aa0a5");
                    r.RelativeItem().AEnd().Text(T("گزارش تفکیک دستگاه‌های تردد")).FontSize(8).FontColor("#8aa0a5");
                });
            });
        }).GeneratePdf();

        static IContainer Th(IContainer c) => c.Background(AccentDeep).Padding(5);
        static IContainer Td(IContainer c) => c.BorderBottom(0.5f).BorderColor("#e5e7eb").Padding(4.5f);
        static IContainer Tf(IContainer c) => c.Background(AccentSoft).BorderTop(1).BorderColor(Accent).Padding(5);
    }

    // ===================== گزارش ورود/خروج خودروها — کنترل پارکینگ (فاز ۴۲) =====================

    public record ParkingKpi(int Total, int InCount, int OutCount, int Allowed, int Denied, int AutoReads);

    /// <summary>PDF گزارش ورود/خروج خودروهای پارکینگ — KPI کلی + جدول ریز رویدادها.</summary>
    public static byte[] BuildParking(string title, string period, List<AttParkingRow> rows, string webRoot)
    {
        EnsureFonts(webRoot);
        const string fa = "Vazirmatn";

        var kpi = new ParkingKpi(
            rows.Count,
            rows.Count(r => r.Direction == "ورود"),
            rows.Count(r => r.Direction == "خروج"),
            rows.Count(r => r.Result == "مجاز"),
            rows.Count(r => r.Result != "مجاز"),
            rows.Count(r => r.Method == "پلاک‌خوان"));

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(36);
                page.DefaultTextStyle(x => HrT.Rtl
                    ? x.FontFamily(fa).FontSize(9f).DirectionFromRightToLeft()
                    : x.FontFamily(fa).FontSize(9f));

                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().AStart().Text(T("سامانه یکپارچه منابع انسانی — کنترل پارکینگ")).Bold().FontSize(13).FontColor(AccentDeep);
                        r.RelativeItem().AEnd().Text(HeaderDate(DateTime.Now)).FontSize(8.5f).FontColor("#6b7280");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().AStart().Text(title).Bold().FontSize(11);
                        r.RelativeItem().AEnd().Text(period).FontSize(9).FontColor("#6b7280");
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Accent);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // ---------- KPI کلی ----------
                    col.Item().Row(r =>
                    {
                        void Kpi(string label, string value, string color) =>
                            r.RelativeItem().PaddingHorizontal(4).Background(AccentSoft).Border(1).BorderColor(Accent)
                                .Padding(8).Column(c =>
                                {
                                    c.Item().AlignCenter().Text(value).Bold().FontSize(13).FontColor(color);
                                    c.Item().AlignCenter().Text(label).FontSize(8.5f).FontColor("#456068");
                                });
                        Kpi(T("کل رویدادها"), Fa(kpi.Total), AccentDeep);
                        Kpi(T("ورود"), Fa(kpi.InCount), "#0369a1");
                        Kpi(T("خروج"), Fa(kpi.OutCount), "#0369a1");
                        Kpi(T("مجاز"), Fa(kpi.Allowed), "#0e9f6e");
                        Kpi(T("غیرمجاز"), Fa(kpi.Denied), "#dc2648");
                        Kpi(T("خوانش خودکار پلاک"), Fa(kpi.AutoReads), "#b45309");
                    });

                    // ---------- ریز رویدادها ----------
                    col.Spacing(12);
                    col.Item().Text(T("ریز رویدادهای ورود/خروج خودروها")).Bold().FontSize(11).FontColor(AccentDeep);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.6f);  // زمان
                            c.RelativeColumn(1.6f);  // پلاک
                            c.RelativeColumn(2f);    // صاحب خودرو
                            c.RelativeColumn(1.8f);  // شرکت
                            c.RelativeColumn(1.8f);  // پارکینگ
                            c.RelativeColumn(1.5f);  // گیت
                            c.RelativeColumn(0.9f);  // جهت
                            c.RelativeColumn(1.8f);  // روش / دوربین
                            c.RelativeColumn(1f);    // اطمینان
                            c.RelativeColumn(1f);    // نتیجه
                            c.RelativeColumn(2f);    // دلیل
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(Th).Text(T("زمان")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("پلاک")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("صاحب خودرو")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("شرکت")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("پارکینگ")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("گیت")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("جهت")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("روش / دوربین")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("اطمینان")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("نتیجه")).FontColor("#ffffff").Bold().FontSize(8f);
                            h.Cell().Element(Th).Text(T("دلیل رد")).FontColor("#ffffff").Bold().FontSize(8f);
                        });
                        foreach (var e in rows)
                        {
                            t.Cell().Element(Td).Text($"{Jalali.Fmt(e.Time)} - {Jalali.FmtTime(e.Time)}").FontSize(7.5f);
                            t.Cell().Element(Td).Text(e.Plate).Bold().FontSize(8f);
                            t.Cell().Element(Td).Text(e.Owner).FontSize(8f);
                            t.Cell().Element(Td).Text(e.Company).FontSize(8f);
                            t.Cell().Element(Td).Text(e.Lot).FontSize(8f);
                            t.Cell().Element(Td).Text(e.Gate).FontSize(8f);
                            t.Cell().Element(Td).Text(T(e.Direction)).FontColor(e.Direction == "ورود" ? "#0369a1" : "#6b7280").FontSize(8f);
                            t.Cell().Element(Td).Text(e.Camera is null ? T(e.Method) : $"{T(e.Method)} ({e.Camera})").FontSize(7.5f);
                            t.Cell().Element(Td).Text(e.Confidence is null ? "—" : Fa((long)e.Confidence.Value) + "٪").FontSize(8f);
                            t.Cell().Element(Td).Text(T(e.Result)).FontColor(e.Result == "مجاز" ? "#0e9f6e" : "#dc2648").Bold().FontSize(8f);
                            t.Cell().Element(Td).Text(e.Reason ?? "—").FontColor(e.Reason is null ? "#9ca3af" : "#dc2648").FontSize(7.5f);
                        }
                        if (rows.Count == 0)
                            t.Cell().ColumnSpan(11).Element(Td).AlignCenter().Text(T("در این بازه رویداد ورود/خروج خودرویی ثبت نشده است")).FontColor("#9ca3af");
                    });

                    col.Spacing(14);
                    col.Item().Text(T("روش «پلاک‌خوان» یعنی ثبت خودکار توسط دوربین‌های پلاک‌خوان (ANPR) بدون دخالت انسان؛ روش‌های «کارت» و «دستی» ثبت نگهبان/گیت‌وی هستند."))
                        .FontSize(8.5f).FontColor("#6b7280");
                });

                page.Footer().Row(r =>
                {
                    r.RelativeItem().AStart().Text(T("تولید شده توسط سامانه یکپارچه منابع انسانی")).FontSize(8).FontColor("#8aa0a5");
                    r.RelativeItem().AEnd().Text(T("گزارش ورود/خروج خودروها — کنترل پارکینگ")).FontSize(8).FontColor("#8aa0a5");
                });
            });
        }).GeneratePdf();

        static IContainer Th(IContainer c) => c.Background(AccentDeep).Padding(5);
        static IContainer Td(IContainer c) => c.BorderBottom(0.5f).BorderColor("#e5e7eb").Padding(4.5f);
    }
}

/// <summary>
/// فاز ۸۱ — کمکی‌های چیدمان PDF چندزبانه: نقطهٔ شروع/پایان سطر بر اساس زبان جاری
/// (fa: راست/چپ — en: چپ/راست) تا سربرگ و پانوشت در هر دو زبان درست بنشیند.
/// </summary>
public static class PdfI18n
{
    public static IContainer AStart(this IContainer c) => HrT.Rtl ? c.AlignRight() : c.AlignLeft();
    public static IContainer AEnd(this IContainer c) => HrT.Rtl ? c.AlignLeft() : c.AlignRight();
}
