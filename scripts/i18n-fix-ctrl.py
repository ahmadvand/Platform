#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Phase 81 — batch fix of remaining user-visible messages in controllers."""
import io, sys

FIXES = {
"HrAttendance.Web/Modules/Core/Controllers/CompaniesController.cs": [
 ('const string m = "توقف ممکن نیست: این تنها شرکت فعال سامانه است";',
  'const string m = "توقف ممکن نیست: این تنها شرکت فعال سامانه است";\n        return AjaxResult(false, HrT.T(m));'),
],
"HrAttendance.Web/Modules/Core/Controllers/NotificationsController.cs": [
 ('? $"اجرای آزمایشی انجام شد — {n} اعلان ارسال/ثبت شد"',
  '? HrT.F("اجرای آزمایشی انجام شد — {0} اعلان ارسال/ثبت شد", HrT.Num(n))'),
 (': "اجرای آزمایشی انجام شد — مورد قابل اعلانی یافت نشد (یا در ۶ ساعت اخیر ارسال شده)");',
  ': HrT.T("اجرای آزمایشی انجام شد — مورد قابل اعلانی یافت نشد (یا در ۶ ساعت اخیر ارسال شده)"));'),
],
"HrAttendance.Web/Modules/Core/Controllers/SettingsController.cs": [
 ('? "پالت به حالت پیش‌فرض بازگشت"', '? HrT.T("پالت به حالت پیش‌فرض بازگشت")'),
 (': "پالت رنگ ذخیره شد و در کل سامانه اعمال شد");', ': HrT.T("پالت رنگ ذخیره شد و در کل سامانه اعمال شد"));'),
 ('if (p.IsBuiltIn) p = new ColorPalette { Name = "کپی " + p.Name }; // کپی از روی پالت داخلی',
  'if (p.IsBuiltIn) p = new ColorPalette { Name = HrT.T("کپی ") + p.Name }; // کپی از روی پالت داخلی'),
 (': "پالت ویرایش شد", css is null ? null : new { css });',
  ': HrT.T("پالت ویرایش شد"), css is null ? null : new { css });'),
],
"HrAttendance.Web/Modules/Core/Controllers/UsersController.cs": [
 ('? $"کاربر «{user.Username}» به پرسنل «{emp.PersonnelCode} — {emp.FullName}» متصل شد" +\n(roleAdded ? " و نقش «پرسنل» افزوده شد" : "")',
  '? HrT.F("کاربر «{0}» به پرسنل «{1} — {2}» متصل شد", user.Username, emp.PersonnelCode, emp.FullName) +\n(roleAdded ? HrT.T(" و نقش «پرسنل» افزوده شد") : "")'),
 (': $"اتصال کاربر «{user.Username}» بدون تغییر ماند";',
  ': HrT.F("اتصال کاربر «{0}» بدون تغییر ماند", user.Username);'),
 ('const string err = "حساب مدیر ارشد قابل حذف نیست";', 'const string err = "حساب مدیر ارشد قابل حذف نیست";'),
 ('const string err = "حساب خودتان قابل حذف نیست";', 'const string err = "حساب خودتان قابل حذف نیست";'),
],
"modules/HrModules.Access/Controllers/AccessController.cs": [
 ('? $"همه درب‌های منطقه «{zone.Name}» در حالت نرمال هستند"',
  '? HrT.F("همه درب‌های منطقه «{0}» در حالت نرمال هستند", zone.Name)'),
 (':$"{Jalali.Num(gates.Count)} درب منطقه «{zone.Name}» به حالت نرمال بازگشت";',
  ': HrT.F("{0} درب منطقه «{1}» به حالت نرمال بازگشت", HrT.Num(gates.Count), zone.Name);'),
 ('? $"احراز دومرحله‌ای «{zone.Name}» فعال شد (اختیاری — فقط همین منطقه): کارت + اثر انگشت/چهره در {Jalali.Num(zone.TwoFactorWindowSec)} ثانیه"',
  '? HrT.F("احراز دومرحله‌ای «{0}» فعال شد (اختیاری — فقط همین منطقه): کارت + اثر انگشت/چهره در {1} ثانیه", zone.Name, HrT.Num(zone.TwoFactorWindowSec))'),
 (': $"احراز دومرحله‌ای «{zone.Name}» خاموش شد (تک‌عاملی)";',
  ': HrT.F("احراز دومرحله‌ای «{0}» خاموش شد (تک‌عاملی)", zone.Name);'),
 ('? "انقضای خودکار هشدارها غیرفعال شد"', '? HrT.T("انقضای خودکار هشدارها غیرفعال شد")'),
 (': $"هشدارهای باز پس از {Jalali.Num(s.AlertTtlDays)} روز خودکار منقضی می‌شوند" +\n(swept > 0 ? $" — همین حالا {Jalali.Num(swept)} هشدار قدیمی منقضی شد" : "");',
  ': HrT.F("هشدارهای باز پس از {0} روز خودکار منقضی می‌شوند", HrT.Num(s.AlertTtlDays)) +\n(swept > 0 ? HrT.F(" — همین حالا {0} هشدار قدیمی منقضی شد", HrT.Num(swept)) : "");'),
 ('Device: d?.Name ?? "بدون دستگاه (گیت بدون اتصال)",',
  'Device: d?.Name ?? HrT.T("بدون دستگاه (گیت بدون اتصال)"),'),
 ('var period = "۳۰ روز اخیر — " + HrCal.FmtNamed(from) + " تا " + HrCal.FmtNamed(DateTime.Now);',
  'var period = HrT.T("۳۰ روز اخیر — ") + HrCal.FmtNamed(from) + HrT.T(" تا ") + HrCal.FmtNamed(DateTime.Now);'),
 ('var pdf = PdfReport.BuildTrafficByDevice("گزارش تردد به تفکیک دستگاه", period, rows,',
  'var pdf = PdfReport.BuildTrafficByDevice(HrT.T("گزارش تردد به تفکیک دستگاه"), period, rows,'),
],
"modules/HrModules.DeviceGateway/Controllers/DeviceGatewayController.cs": [
 ('parkingMsg = "ماژول پارکینگ در این نسخه نصب نیست — حلقهٔ دوربین‌های پلاک‌خوان غیرفعال است"; }',
  'parkingMsg = HrT.T("ماژول پارکینگ در این نسخه نصب نیست — حلقهٔ دوربین‌های پلاک‌خوان غیرفعال است"); }'),
 ('parkingMsg = "دریافت کانفیگ از بانک ناموفق بود: " + ex.Message;',
  'parkingMsg = HrT.T("دریافت کانفیگ از بانک ناموفق بود: ") + ex.Message;'),
],
"modules/HrModules.Devices/Controllers/BiometricsController.cs": [
 ('var em = $"هنوز پرسنلی از «{device.Name}» دریافت نشده — ابتدا «دریافت پرسنل از دستگاه» را اجرا کنید";',
  'var em = HrT.F("هنوز پرسنلی از «{0}» دریافت نشده — ابتدا «دریافت پرسنل از دستگاه» را اجرا کنید", device.Name);'),
 ('var em = "حداقل یک پرسنل و یک دستگاه مقصد انتخاب کنید";',
  'var em = HrT.T("حداقل یک پرسنل و یک دستگاه مقصد انتخاب کنید");'),
 ('var em = "دستگاه مبدأ، حداقل یک کاربر و حداقل یک دستگاه مقصد را انتخاب کنید";',
  'var em = HrT.T("دستگاه مبدأ، حداقل یک کاربر و حداقل یک دستگاه مقصد را انتخاب کنید");'),
 ('var em2 = "دستگاه مقصد نباید همان دستگاه مبدأ باشد";',
  'var em2 = HrT.T("دستگاه مقصد نباید همان دستگاه مبدأ باشد");'),
 ('var em = "کاربری با پین انتخابی روی دستگاه مبدأ یافت نشد — ابتدا «دریافت پرسنل از دستگاه» را اجرا کنید";',
  'var em = HrT.T("کاربری با پین انتخابی روی دستگاه مبدأ یافت نشد — ابتدا «دریافت پرسنل از دستگاه» را اجرا کنید");'),
 ('"{Jalali.Num(r.Total)} کاربر از دستگاه خوانده شد', '"{0} کاربر از دستگاه خوانده شد'),  # placeholder, fixed below
],
"modules/HrModules.Devices/Controllers/Api/DevicesWebhookController.cs": [
 ('cmd.Result = result ?? (ok ? "با موفقیت اجرا شد" : "دستگاه اجرای دستور را رد کرد");',
  'cmd.Result = result ?? (ok ? HrT.T("با موفقیت اجرا شد") : HrT.T("دستگاه اجرای دستور را رد کرد"));'),
],
"modules/HrModules.Org/Controllers/EmployeesController.cs": [
 ('{ ToastErr("فایلی انتخاب نشده است"); return RedirectToAction(nameof(Import)); }',
  '{ ToastErr(HrT.T("فایلی انتخاب نشده است")); return RedirectToAction(nameof(Import)); }'),
 ('{ ToastErr("فقط فایل xlsx پشتیبانی می‌شود"); return RedirectToAction(nameof(Import)); }',
  '{ ToastErr(HrT.T("فقط فایل xlsx پشتیبانی می‌شود")); return RedirectToAction(nameof(Import)); }'),
 ('{ ToastErr("داده‌ای برای ثبت نیست"); return RedirectToAction(nameof(Import)); }\n        var (inserted, skipped) = ExcelImport.CommitPeople(Db, json, companyId);',
  '{ ToastErr(HrT.T("داده‌ای برای ثبت نیست")); return RedirectToAction(nameof(Import)); }\n        var (inserted, skipped) = ExcelImport.CommitPeople(Db, json, companyId);'),
 ('{ ToastErr("داده‌ای برای ثبت نیست"); return RedirectToAction(nameof(Import)); }\n        var (inserted2, skipped2) = ExcelImport.CommitAttendance(Db, json, HttpContext.RequestServices);',
  '{ ToastErr(HrT.T("داده‌ای برای ثبت نیست")); return RedirectToAction(nameof(Import)); }\n        var (inserted2, skipped2) = ExcelImport.CommitAttendance(Db, json, HttpContext.RequestServices);'),
 ('var okMsg = $"اطلاعات «{emp.FullName}» ذخیره شد" + (pinWarn is null ? "" : " — " + pinWarn);',
  'var okMsg = HrT.F("اطلاعات «{0}» ذخیره شد", emp.FullName) + (pinWarn is null ? "" : HrT.T(" — ") + pinWarn);'),
 ('.Select(du => du.Device!.Name + " — پین " + du.Pin)',
  '.Select(du => du.Device!.Name + HrT.T(" — پین ") + du.Pin)'),
],
"modules/HrModules.Org/Controllers/OrgController.cs": [
 ('ViewBag.OldName = dept.ParentId is null ? "ریشه چارت"',
  'ViewBag.OldName = dept.ParentId is null ? HrT.T("ریشه چارت")'),
 ('? "ریشه چارت"', '? HrT.T("ریشه چارت")'),
 ('var newParent = parentId is null ? "ریشه چارت" : await Departments.Where(d => d.Id == parentId).Select(d => d.Name).FirstOrDefaultAsync() ?? "؟";',
  'var newParent = parentId is null ? HrT.T("ریشه چارت") : await Departments.Where(d => d.Id == parentId).Select(d => d.Name).FirstOrDefaultAsync() ?? HrT.T("؟");'),
 ('? $"واحد «{dept.Name}» به «{newParent}» منتقل شد — {leftBehind} نفر پرسنل مستقیم در «{oldParentName}» باقی ماندند"',
  '? HrT.F("واحد «{0}» به «{1}» منتقل شد — {2} نفر پرسنل مستقیم در «{3}» باقی ماندند", dept.Name, newParent, HrT.Num(leftBehind), oldParentName)'),
 (': $"واحد «{dept.Name}» به همراه پرسنل به «{newParent}» منتقل شد";',
  ': HrT.F("واحد «{0}» به همراه پرسنل به «{1}» منتقل شد", dept.Name, newParent);'),
],
}

def apply(fixes_map):
    total = ok = 0
    for f, pairs in fixes_map.items():
        s = open(f, encoding='utf-8').read()
        for old, new in pairs:
            total += 1
            if old.startswith('"{0} کاربر'):  # placeholder entry — skip
                continue
            if old in s:
                s = s.replace(old, new, 1); ok += 1
            else:
                print(f"MISS {f}: {old[:60]}")
        open(f, 'w', encoding='utf-8').write(s)
    print(f"applied {ok}/{total}")

apply(FIXES)
