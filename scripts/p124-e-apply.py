#!/usr/bin/env python3
# فاز ۱۲۴-ه (E-ب) — اعمال توکن .kvr روی پنل‌های جزئیات منتخب (فقط بصری، بدون تغییر منطق):
# ۱) License: حذف تکرار سبک محلی lic-row → استفاده از توکن مشترک + نگه‌داشتن فقط «حاشیهٔ نقطه‌چین»
# ۲) Self: جدول «اطلاعات من» → ردیف‌های kvr (همهٔ عبارت‌های Razor عیناً حفظ می‌شوند)
import io, sys

def sub(path, old, new, expect=1):
    with io.open(path, encoding="utf-8") as f:
        src = f.read()
    if src.count(old) == 0 and src.count(new) >= expect:
        print(f"SKIP (قبلاً انجام شده): {path} ← {new[:52]}…")
        return
    n = src.count(old)
    if n != expect:
        print(f"ABORT — «{old[:60]}…» یگانه نیست ({n}x، انتظار {expect}): {path}")
        sys.exit(1)
    with io.open(path, "w", encoding="utf-8") as f:
        f.write(src.replace(old, new))
    print(f"OK: {path} ← {old[:52]}…")

LIC = "/home/z/my-project/aspnet/src/Platform.Web/Views/License/Index.cshtml"
SELF = "/home/z/my-project/aspnet/src/Platform.Web/Views/Self/Index.cshtml"

# ---------- License ----------
sub(LIC, """    .lic-rows { display: grid; gap: 9px; margin-bottom: 18px; }
    .lic-row { display: flex; justify-content: space-between; gap: 14px; border: 1px dashed var(--line, #e3e6f0);
        border-radius: 11px; padding: 9px 13px; font-size: 12.5px; align-items: center; }
    .lic-row .k { color: var(--ink-3, #889); flex: none; }
    .lic-row .v { color: var(--ink, #223); font-weight: 600; text-align: left; word-break: break-word; }""",
         """    .lic-rows { margin-bottom: 18px; } /* فاز ۱۲۴-ه — بقیه از توکن مشترک .kvr/.kvs می‌آید */
    .lic-row { border-style: dashed; }""")
sub(LIC, 'class="lic-rows"', 'class="kvs lic-rows"', 2)
sub(LIC, 'class="lic-row"', 'class="kvr lic-row"', 9)

# ---------- Self: جدول اطلاعات من → kvr ----------
sub(SELF, '''            <table class="tbl">
                <tbody>
                    <tr><td>@HrT.T("نام و نام خانوادگی")</td><td><b>@emp!.FullName</b></td></tr>
                    <tr><td>@HrT.T("کد پرسنلی")</td><td class="num">@Jalali.ToPersianDigits(emp.PersonnelCode)</td></tr>
                    <tr><td>@HrT.T("واحد سازمانی")</td><td>@(string.IsNullOrEmpty(emp.Department) ? "—" : emp.Department)</td></tr>
                    <tr><td>@HrT.T("سمت")</td><td>@(string.IsNullOrEmpty(emp.Position) ? "—" : emp.Position)</td></tr>
                    <tr><td>@HrT.T("نوع استخدام")</td><td>@emp.EmploymentType</td></tr>
                    <tr><td>@HrT.T("مانده مرخصی استحقاقی سال")</td><td class="num">@(HrT.F("{0} روز", Jalali.Num(emp.LeaveRemaining)))</td></tr>
                    <tr><td>@HrT.T("مانده مرخصی استعلاجی سال")</td><td class="num">@(HrT.F("{0} روز", Jalali.Num(emp.SickLeaveRemaining)))</td></tr>
                </tbody>
            </table>''',
         '''            <div class="kvs">
                <div class="kvr"><span class="k">@HrT.T("نام و نام خانوادگی")</span><span class="v"><b>@emp!.FullName</b></span></div>
                <div class="kvr"><span class="k">@HrT.T("کد پرسنلی")</span><span class="v num">@Jalali.ToPersianDigits(emp.PersonnelCode)</span></div>
                <div class="kvr"><span class="k">@HrT.T("واحد سازمانی")</span><span class="v">@(string.IsNullOrEmpty(emp.Department) ? "—" : emp.Department)</span></div>
                <div class="kvr"><span class="k">@HrT.T("سمت")</span><span class="v">@(string.IsNullOrEmpty(emp.Position) ? "—" : emp.Position)</span></div>
                <div class="kvr"><span class="k">@HrT.T("نوع استخدام")</span><span class="v">@emp.EmploymentType</span></div>
                <div class="kvr"><span class="k">@HrT.T("مانده مرخصی استحقاقی سال")</span><span class="v num">@(HrT.F("{0} روز", Jalali.Num(emp.LeaveRemaining)))</span></div>
                <div class="kvr"><span class="k">@HrT.T("مانده مرخصی استعلاجی سال")</span><span class="v num">@(HrT.F("{0} روز", Jalali.Num(emp.SickLeaveRemaining)))</span></div>
            </div>''')
