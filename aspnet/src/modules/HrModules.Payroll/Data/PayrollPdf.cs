using Platform.Web.Domain;
using Platform.Web.Services;
using HrModules.Payroll.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrModules.Payroll.Data;

/// <summary>
/// فاز ۶۱ — سازنده‌های PDF حقوق (قبلاً PdfReport در Core) — به لایهٔ دیتای ماژول حقوق منتقل شد
/// چون ورودی‌شان entityهای ماژول حقوق است. تم/رنگ از PdfReport پلتفرم (ApplyPalette) گرفته می‌شود
/// تا هم‌راستاسازی پالت ادمین بدون تغییر کار کند.
/// </summary>
public static class PayrollPdf
{
public static byte[] BuildPayrollRun(PayrollRun run, string webRoot)
    {
        PdfReport.EnsureFonts(webRoot);
        const string fa = "Vazirmatn";
        var company = run.Company?.Name ?? "—";
        // فاز ۸۱ — دورهٔ حقوق: fa «دوره مهر ۱۴۰۴» / en «Period 7 1404»
        var period = HrT.F("دوره {0} {1}",
            HrT.Rtl ? Jalali.MonthName(run.JMonth) : HrT.Num(run.JMonth), HrT.Num(run.JYear));

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => HrT.Rtl
                    ? x.FontFamily(fa).FontSize(8.5f).DirectionFromRightToLeft()
                    : x.FontFamily(fa).FontSize(8.5f));

                page.Header().Column(col => PdfReport.DocHeader(col, company, HrT.T("لیست حقوق و دستمزد"), period));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(0.6f);   // ردیف
                            c.RelativeColumn(1.2f);   // کد
                            c.RelativeColumn(2.4f);   // نام
                            c.RelativeColumn(1.8f);   // پایه
                            c.RelativeColumn(1.5f);   // اضافه‌کاری
                            c.RelativeColumn(2f);     // مزایا
                            c.RelativeColumn(1.4f);   // کسور غیبت
                            c.RelativeColumn(1.4f);   // بیمه
                            c.RelativeColumn(1.3f);   // مالیات
                            c.RelativeColumn(2f);     // خالص
                        });
                        t.Header(h =>
                        {
                            foreach (var (txt, _) in new[] {
                                (HrT.T("ردیف"), 0), (HrT.T("کد پرسنلی"), 0), (HrT.T("نام و نام خانوادگی"), 0), (HrT.T("حقوق پایه"), 0),
                                (HrT.T("اضافه‌کاری"), 0), (HrT.T("مزایا"), 0), (HrT.T("کسور غیبت"), 0), (HrT.T("بیمه"), 0), (HrT.T("مالیات"), 0), (HrT.T("خالص پرداختی"), 0) })
                                h.Cell().Element(Th).Text(txt).FontColor("#ffffff").Bold().FontSize(8);
                        });
                        int i = 0;
                        decimal tBase = 0, tOt = 0, tAll = 0, tAb = 0, tIns = 0, tTax = 0, tNet = 0;
                        foreach (var p in run.Payslips.OrderBy(x => x.Employee!.PersonnelCode))
                        {
                            i++;
                            tBase += p.BaseSalary; tOt += p.OvertimePay; tAll += p.Allowances;
                            tAb += p.AbsentDeduct; tIns += p.Insurance; tTax += p.Tax; tNet += p.NetPay;
                            t.Cell().Element(Td).Text(PdfReport.Fa(i));
                            t.Cell().Element(Td).Text(p.Employee!.PersonnelCode);
                            t.Cell().Element(Td).Text(p.Employee.FullName);
                            t.Cell().Element(Td).Text(PdfReport.Money(p.BaseSalary));
                            t.Cell().Element(Td).Text(PdfReport.Money(p.OvertimePay));
                            t.Cell().Element(Td).Text(PdfReport.Money(p.Allowances));
                            t.Cell().Element(Td).Text(p.AbsentDeduct > 0 ? PdfReport.Money(p.AbsentDeduct) : "—");
                            t.Cell().Element(Td).Text(p.Insurance > 0 ? PdfReport.Money(p.Insurance) : "—");
                            t.Cell().Element(Td).Text(p.Tax > 0 ? PdfReport.Money(p.Tax) : "—");
                            t.Cell().Element(Td).Text(PdfReport.Money(p.NetPay)).Bold();
                        }
                        if (i == 0)
                            t.Cell().ColumnSpan(10).Element(Td).AlignCenter().Text(HrT.T("فیشی برای این لیست ثبت نشده است")).FontColor("#9ca3af");
                        // جمع کل
                        IContainer TF(IContainer c) => c.Background(PdfReport.AccentSoft).BorderTop(1.2f).BorderColor(PdfReport.Accent).Padding(4.5f);
                        t.Cell().Element(TF).Text("—");
                        t.Cell().Element(TF).Text("—");
                        t.Cell().Element(TF).Text(HrT.T("جمع کل")).Bold();
                        t.Cell().Element(TF).Text(PdfReport.Money(tBase)).Bold();
                        t.Cell().Element(TF).Text(PdfReport.Money(tOt)).Bold();
                        t.Cell().Element(TF).Text(PdfReport.Money(tAll)).Bold();
                        t.Cell().Element(TF).Text(PdfReport.Money(tAb)).Bold();
                        t.Cell().Element(TF).Text(PdfReport.Money(tIns)).Bold();
                        t.Cell().Element(TF).Text(PdfReport.Money(tTax)).Bold();
                        t.Cell().Element(TF).Text(PdfReport.Money(tNet)).Bold().FontColor(PdfReport.AccentDeep);
                    });

                    col.Spacing(18);
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(HrT.T("مهر و امضای مدیر منابع انسانی")).FontSize(8.5f).FontColor("#374151");
                            c.Item().PaddingTop(26).LineHorizontal(0.7f).LineColor("#9ca3af");
                        });
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(HrT.T("مهر و امضای مدیر مالی")).FontSize(8.5f).FontColor("#374151");
                            c.Item().PaddingTop(26).LineHorizontal(0.7f).LineColor("#9ca3af");
                        });
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(HrT.T("تأیید مدیریت عامل")).FontSize(8.5f).FontColor("#374151");
                            c.Item().PaddingTop(26).LineHorizontal(0.7f).LineColor("#9ca3af");
                        });
                    });
                });

                page.Footer().Element(PdfReport.DocFooter);
            });
        }).GeneratePdf();

        static IContainer Th(IContainer c) => c.Background(PdfReport.AccentDeep).Padding(5);
        static IContainer Td(IContainer c) => c.BorderBottom(0.5f).BorderColor("#e5e7eb").Padding(4.5f);
    }

    /// <summary>فیش حقوقی رسمی یک پرسنل — مشخصات + جدول خطی اقلام + خالص + امضا.</summary>
    public static byte[] BuildSlip(Payslip slip, string webRoot)
    {
        PdfReport.EnsureFonts(webRoot);
        const string fa = "Vazirmatn";
        var emp = slip.Employee!;
        var run = slip.Run!;
        var dept = emp.Department?.Name ?? "—";
        var pos = emp.Position?.Title ?? "—";

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => HrT.Rtl
                    ? x.FontFamily(fa).FontSize(9.5f).DirectionFromRightToLeft()
                    : x.FontFamily(fa).FontSize(9.5f));

                page.Header().Column(col => PdfReport.DocHeader(col,
                    run.Company?.Name ?? "—", HrT.T("فیش حقوقی"),
                    HrT.F("دوره {0} {1}", HrT.Rtl ? Jalali.MonthName(run.JMonth) : HrT.Num(run.JMonth), HrT.Num(run.JYear)) +
                    (run.Status == "نهایی" ? " — " + HrT.T("نهایی") : " — " + HrT.T("پیش‌نویس"))));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // مشخصات پرسنل
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        t.Cell().Element(Inf).Text(txt => { txt.Span(HrT.T("کد پرسنلی: ")).FontColor("#6b7280"); txt.Span(emp.PersonnelCode).Bold(); });
                        t.Cell().Element(Inf).Text(txt => { txt.Span(HrT.T("نام و نام خانوادگی: ")).FontColor("#6b7280"); txt.Span(emp.FullName).Bold(); });
                        t.Cell().Element(Inf).Text(txt => { txt.Span(HrT.T("واحد: ")).FontColor("#6b7280"); txt.Span(dept).Bold(); });
                        t.Cell().Element(Inf).Text(txt => { txt.Span(HrT.T("پست: ")).FontColor("#6b7280"); txt.Span(pos).Bold(); });
                    });

                    col.Spacing(10);

                    // اقلام — مزایا و کسور
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(0.6f);
                            c.RelativeColumn(2.6f);   // عنوان
                            c.RelativeColumn(1.2f);   // نوع
                            c.RelativeColumn(2f);     // مبلغ
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(Th).Text(HrT.T("ردیف")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(HrT.T("شرح قلم")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(HrT.T("نوع")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(HrT.F("مبلغ ({0})", Money.CurTitle())).FontColor("#ffffff").Bold().FontSize(8.5f);
                        });
                        int i = 0;
                        foreach (var l in slip.Lines.OrderBy(x => x.Order))
                        {
                            i++;
                            t.Cell().Element(Td).Text(PdfReport.Fa(i));
                            t.Cell().Element(Td).Text(l.Title);
                            t.Cell().Element(Td).Text(HrT.T(l.Kind)).FontColor(l.Kind == "کسور" ? "#dc2648" : "#0e9f6e");
                            t.Cell().Element(Td).Text(PdfReport.Money(l.Amount));
                        }
                        if (i == 0) t.Cell().ColumnSpan(4).Element(Td).AlignCenter().Text(HrT.T("قلمی محاسبه نشده است")).FontColor("#9ca3af");
                    });

                    col.Spacing(12);
                    // خالص پرداختی
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(150).AStart().Background(PdfReport.AccentSoft).Border(1.2f).BorderColor(PdfReport.Accent).Padding(8)
                            .Text(txt =>
                            {
                                txt.Span(HrT.T("خالص پرداختی: ")).FontSize(11).Bold();
                                txt.Span(PdfReport.Money(slip.NetPay) + " " + Money.Sym()).FontSize(12).Bold().FontColor(PdfReport.AccentDeep);
                            });
                    });

                    col.Spacing(20);
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(HrT.T("امضای دریافت‌کننده")).FontSize(8.5f).FontColor("#374151");
                            c.Item().PaddingTop(26).LineHorizontal(0.7f).LineColor("#9ca3af");
                        });
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(HrT.T("مهر و امضای کارپردازی")).FontSize(8.5f).FontColor("#374151");
                            c.Item().PaddingTop(26).LineHorizontal(0.7f).LineColor("#9ca3af");
                        });
                    });
                });

                page.Footer().Element(PdfReport.DocFooter);
            });
        }).GeneratePdf();

        static IContainer Th(IContainer c) => c.Background(PdfReport.AccentDeep).Padding(5);
        static IContainer Td(IContainer c) => c.BorderBottom(0.5f).BorderColor("#e5e7eb").Padding(4.5f);
        static IContainer Inf(IContainer c) => c.PaddingVertical(3).PaddingHorizontal(4);
    }

    /// <summary>برگ تسویه‌حساب رسمی — ریز محاسبات پایان همکاری + امضاها.</summary>
    public static byte[] BuildSettlement(PayrollSettlement st, string webRoot)
    {
        PdfReport.EnsureFonts(webRoot);
        const string fa = "Vazirmatn";
        var emp = st.Employee!;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => HrT.Rtl
                    ? x.FontFamily(fa).FontSize(9.5f).DirectionFromRightToLeft()
                    : x.FontFamily(fa).FontSize(9.5f));

                page.Header().Column(col => PdfReport.DocHeader(col,
                    st.Company?.Name ?? "—", HrT.T("برگ تسویه‌حساب"),
                    HrT.F("تاریخ پایان همکاری: {0}",
                        HrT.Rtl ? Jalali.FmtFa(st.EndDate) : st.EndDate.ToString("yyyy-MM-dd"))));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        t.Cell().Element(Inf).Text(txt => { txt.Span(HrT.T("کد پرسنلی: ")).FontColor("#6b7280"); txt.Span(emp.PersonnelCode).Bold(); });
                        t.Cell().Element(Inf).Text(txt => { txt.Span(HrT.T("نام و نام خانوادگی: ")).FontColor("#6b7280"); txt.Span(emp.FullName).Bold(); });
                        t.Cell().Element(Inf).Text(txt => { txt.Span(HrT.T("واحد: ")).FontColor("#6b7280"); txt.Span(emp.Department?.Name ?? "—").Bold(); });
                        t.Cell().Element(Inf).Text(txt => { txt.Span(HrT.T("نوع اشتغال: ")).FontColor("#6b7280"); txt.Span(HrT.T(emp.EmploymentType)).Bold(); });
                    });

                    col.Spacing(10);

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(0.6f); c.RelativeColumn(4f); c.RelativeColumn(2.4f); });
                        t.Header(h =>
                        {
                            h.Cell().Element(Th).Text(HrT.T("ردیف")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(HrT.T("شرح")).FontColor("#ffffff").Bold().FontSize(8.5f);
                            h.Cell().Element(Th).Text(HrT.F("مبلغ ({0})", Money.CurTitle())).FontColor("#ffffff").Bold().FontSize(8.5f);
                        });
                        var rows = new (string Desc, decimal Val)[]
                        {
                            (HrT.F("بهای {0} روز مانده مرخصی استحقاقی (روزانه {1})", HrT.Num((int)st.LeaveBalance), PdfReport.Money(st.Daily)), st.LeavePay),
                            (HrT.F("عیدی نسبتی سال جاری ({0} روز خدمت)", HrT.Num((int)st.Days)), st.Eid),
                            (HrT.T("پایه سنوات نسبتی سال جاری"), st.Seniority),
                        };
                        int i = 0;
                        foreach (var (desc, val) in rows)
                        {
                            i++;
                            t.Cell().Element(Td).Text(PdfReport.Fa(i));
                            t.Cell().Element(Td).Text(desc);
                            t.Cell().Element(Td).Text(PdfReport.Money(val));
                        }
                    });

                    col.Spacing(12);
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(170).AStart().Background(PdfReport.AccentSoft).Border(1.2f).BorderColor(PdfReport.Accent).Padding(8)
                            .Text(txt =>
                            {
                                txt.Span(HrT.T("جمع تسویه‌حساب: ")).FontSize(11).Bold();
                                txt.Span(PdfReport.Money(st.Total) + " " + Money.Sym()).FontSize(12).Bold().FontColor(PdfReport.AccentDeep);
                            });
                    });

                    col.Spacing(20);
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(HrT.T("امضای کارمند (تسویه تمام‌وکمال است)")).FontSize(8.5f).FontColor("#374151");
                            c.Item().PaddingTop(26).LineHorizontal(0.7f).LineColor("#9ca3af");
                        });
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(HrT.T("مهر و امضای مدیر منابع انسانی")).FontSize(8.5f).FontColor("#374151");
                            c.Item().PaddingTop(26).LineHorizontal(0.7f).LineColor("#9ca3af");
                        });
                    });
                });

                page.Footer().Element(PdfReport.DocFooter);
            });
        }).GeneratePdf();

        static IContainer Th(IContainer c) => c.Background(PdfReport.AccentDeep).Padding(5);
        static IContainer Td(IContainer c) => c.BorderBottom(0.5f).BorderColor("#e5e7eb").Padding(4.5f);
        static IContainer Inf(IContainer c) => c.PaddingVertical(3).PaddingHorizontal(4);
    }

    // ================= گزارش تردد به تفکیک دستگاه (فاز ۳۰) =================


    /// <summary>
    /// گزارش PDF «تردد به تفکیک دستگاه»: جدول جمع‌بندی هر دستگاه (ورود/خروج/مجاز/غیرمجاز)
    /// + KPI کلی + ردیف جمع کل — چیدمان راست‌به‌چپ با فونت وزیرمتن.
    /// </summary>
    
}
