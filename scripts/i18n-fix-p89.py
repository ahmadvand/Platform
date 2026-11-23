#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# فاز ۸۹ — رفع شکاف‌های واقعی i18n: رندر خام وضعیت‌ها، عنوان صفحه‌ها،
# عنوان مودال‌ها، متن دیالوگ‌ها و تاریخ‌های bypass شده از تقویم.
import io, sys

ROOT = "/home/z/my-project/aspnet/src"
fixed = []

def patch(path, pairs):
    p = f"{ROOT}/{path}"
    src = io.open(p, encoding="utf-8").read()
    orig = src
    for old, new in pairs:
        if old not in src:
            print(f"  !! NOT FOUND in {path}: {old[:60]}")
            continue
        src = src.replace(old, new)
    if src != orig:
        io.open(p, "w", encoding="utf-8").write(src)
        fixed.append(path)
        print(f"  ok {path}")

# ۱) رندرهای خام .Status → HrT.T(...)
patch("modules/Platform.Modules.Visitors/Views/Visitors/Index.cshtml",
      [('<span class="badge @VBadge(v.Status)">@v.Status</span>',
        '<span class="badge @VBadge(v.Status)">@HrT.T(v.Status)</span>')])

patch("modules/HrModules.Attendance/Views/Requests/Index.cshtml",
      [('<span class="badge @StatBadge(l.Status)">@l.Status</span>',
        '<span class="badge @StatBadge(l.Status)">@HrT.T(l.Status)</span>'),
       ('<span class="badge @StatBadge(m.Status)">@m.Status</span>',
        '<span class="badge @StatBadge(m.Status)">@HrT.T(m.Status)</span>'),
       ('<span class="badge @StatBadge(p.Status)">@p.Status</span>',
        '<span class="badge @StatBadge(p.Status)">@HrT.T(p.Status)</span>'),
       ('<span class="badge @StatBadge(c.Status)">@c.Status</span>',
        '<span class="badge @StatBadge(c.Status)">@HrT.T(c.Status)</span>')])

patch("modules/HrModules.Attendance/Views/Attendance/Index.cshtml",
      [('_ => "badge-mute" })">@r.Status</span>', '_ => "badge-mute" })">@HrT.T(r.Status)</span>')])

patch("modules/HrModules.Attendance/Views/SelfAtt/Attendance.cshtml",
      [(' : "badge-info")">@r.Status</span>', ' : "badge-info")">@HrT.T(r.Status)</span>')])

patch("modules/HrModules.Attendance/Views/SelfAtt/Requests.cshtml",
      [(' ? "badge-ok" : l.Status == "رد شده" ? "badge-err" : "badge-warn")">@l.Status</span>',
        ' ? "badge-ok" : l.Status == "رد شده" ? "badge-err" : "badge-warn")">@HrT.T(l.Status)</span>'),
       (' ? "badge-ok" : m.Status == "رد شده" ? "badge-err" : "badge-warn")">@m.Status</span>',
        ' ? "badge-ok" : m.Status == "رد شده" ? "badge-err" : "badge-warn")">@HrT.T(m.Status)</span>'),
       (' ? "badge-ok" : c.Status == "رد شده" ? "badge-err" : "badge-warn")">@c.Status</span>',
        ' ? "badge-ok" : c.Status == "رد شده" ? "badge-err" : "badge-warn")">@HrT.T(c.Status)</span>')])

patch("modules/Platform.Modules.Parking/Views/Parking/Index.cshtml",
      [(' : "badge-mute")">@g.Status</span>', ' : "badge-mute")">@HrT.T(g.Status)</span>')])

patch("modules/HrModules.Org/Views/Employees/Index.cshtml",
      [(' ? "badge-ok" : "badge-mute")">@e.Status</span>', ' ? "badge-ok" : "badge-mute")">@HrT.T(e.Status)</span>')])

patch("modules/HrModules.Access/Views/Cards/Index.cshtml",
      [('<span class="badge @BadgeOf(c.Status)">@c.Status</span>',
        '<span class="badge @BadgeOf(c.Status)">@HrT.T(c.Status)</span>')])

# ۲) عنوان صفحه‌ها
patch("modules/HrModules.Devices/Views/Devices/Index.cshtml", [
    ('ViewData["Title"] = "دستگاه‌های تردد";', 'ViewData["Title"] = HrT.T("دستگاه‌های تردد");'),
    ('data-modal-title="تعریف دستگاه جدید"', 'data-modal-title="@HrT.T("تعریف دستگاه جدید")"'),
    ('title="ترددهای ثبت‌شده روی دستگاه آفلاین — در انتظار دریافت دسته‌ای"',
     'title="@HrT.T("ترددهای ثبت‌شده روی دستگاه آفلاین — در انتظار دریافت دسته‌ای")"'),
    ('data-modal-title="ویرایش دستگاه — @d.Name"', 'data-modal-title="@HrT.F("ویرایش دستگاه — {0}", d.Name)"'),
    ('@(d.Active ? "⏸ توقف دستگاه" : "▶ فعال‌سازی")', '@(d.Active ? HrT.T("⏸ توقف دستگاه") : HrT.T("▶ فعال‌سازی"))'),
    ('data-confirm="توکن اتصال دستگاه جدید شود؟ (اتصال فعلی قطع و دوباره وصل می‌شود)"',
     'data-confirm="@HrT.T("توکن اتصال دستگاه جدید شود؟ (اتصال فعلی قطع و دوباره وصل می‌شود)")"'),
    ('@(c.Status switch { "acked" => "اجرا شد", "failed" => "ناموفق", "delivered" => "تحویل شد", _ => "در صف" })',
     '@(c.Status switch { "acked" => HrT.T("اجرا شد"), "failed" => HrT.T("ناموفق"), "delivered" => HrT.T("تحویل شد"), _ => HrT.T("در صف") })'),
])

patch("modules/HrModules.Devices/Views/Devices/LiveView.cshtml",
      [('ViewData["Title"] = "نمای زنده حراست";', 'ViewData["Title"] = HrT.T("نمای زنده حراست");')])

patch("modules/HrModules.Devices/Views/Biometrics/Index.cshtml",
      [('ViewData["Title"] = "داده‌های هویتی دستگاه‌ها";', 'ViewData["Title"] = HrT.T("داده‌های هویتی دستگاه‌ها");'),
       ('data-confirm="دریافت هویتی همه پرسنل «@d.Name»؟"', 'data-confirm="@HrT.F("دریافت هویتی همه پرسنل «{0}»؟", d.Name)"'),
       ('data-confirm="کاربر «@du.Name» از دستگاه «@du.Device?.Name» حذف شود؟"', 'data-confirm="@HrT.F("کاربر «{0}» از دستگاه «{1}» حذف شود؟", du.Name, du.Device?.Name)"'),
       ('<div class="fld"><label>دستگاه‌های مقصد *</label>', '<div class="fld"><label>@HrT.T("دستگاه‌های مقصد") *</label>'),
       ('onclick="toggleAll(\'empSelect\')">انتخاب/حذف همه پرسنل</button>', 'onclick="toggleAll(\'empSelect\')">@HrT.T("انتخاب/حذف همه پرسنل")</button>')])

# ۳) تاریخ‌های bypass شده در اعلان‌ها → تقویم نمایشی
patch("Platform.Web/Views/Notifications/Index.cshtml", [
    ('@(r.LastRunAt?.ToString("MM/dd HH:mm") ?? "—")',
     '@(r.LastRunAt is null ? HrT.T("—") : Jalali.FmtLong(r.LastRunAt.Value))'),
    ('@l.CreatedAt.ToString("yyyy/MM/dd HH:mm")',
     '@Jalali.FmtLong(l.CreatedAt)'),
])

print("\nFILES FIXED:", len(fixed))
