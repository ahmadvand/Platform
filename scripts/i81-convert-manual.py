#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۸۱ — تبدیل دسته‌ای پیام‌های درون‌یابی‌شدهٔ باقی‌مانده به HrT.F (جایگزینی عینی)"""
import sys

FILES = {
"modules/HrModules.Access/Controllers/AccessController.cs": [
 ('var msg = $"تخلیه اضطراری: {Jalali.Num(gates.Count)} درب منطقه «{zone.Name}» باز شد";',
  'var msg = HrT.F("تخلیه اضطراری: {0} درب منطقه «{1}» باز شد", HrT.Num(gates.Count), zone.Name);'),
 ('ToastOk($"آنت‌ی‌پس‌بک «{zone.Name}» {(zone.AntiPassback ? "فعال" : "غیرفعال")} شد");',
  'ToastOk(HrT.F("آنت‌ی‌پس‌بک «{0}» {1} شد", zone.Name, zone.AntiPassback ? HrT.T("فعال") : HrT.T("غیرفعال")));'),
 ('var msg = $"پنجره دومرحله‌ای «{zone.Name}» روی {Jalali.Num(zone.TwoFactorWindowSec)} ثانیه تنظیم شد";',
  'var msg = HrT.F("پنجره دومرحله‌ای «{0}» روی {1} ثانیه تنظیم شد", zone.Name, HrT.Num(zone.TwoFactorWindowSec));'),
 ('return AjaxResult(true, $"جای‌گذاری {Jalali.Num(saved)} گیت روی نقشه ذخیره شد");',
  'return AjaxResult(true, HrT.F("جای‌گذاری {0} گیت روی نقشه ذخیره شد", HrT.Num(saved)));'),
],
"modules/HrModules.Devices/Controllers/DevicesController.cs": [
 ('var msg = $"دستور «{DeviceOps.CommandLabels.GetValueOrDefault(type, type)}» در صف {device.Name} ثبت شد — تحویل با سرویس گیت‌وی";',
  'var msg = HrT.F("دستور «{0}» در صف {1} ثبت شد — تحویل با سرویس گیت‌وی", HrT.T(DeviceOps.CommandLabels.GetValueOrDefault(type, type)), device.Name);'),
 ('var msg = $"{queued} دستور انتقال کاربر «{emp.FullName}» (پین {emp.PersonnelCode}) در صف قرار گرفت";',
  'var msg = HrT.F("{0} دستور انتقال کاربر «{1}» (پین {2}) در صف قرار گرفت", HrT.Num(queued), emp.FullName, emp.PersonnelCode);'),
 ('var msg = $"دریافت دسته‌ای: {Jalali.Num(r.Ok)} تردد از حافظه «{device.Name}» وارد سامانه شد" +',
  'var msg = HrT.F("دریافت دسته‌ای: {0} تردد از حافظه «{1}» وارد سامانه شد", HrT.Num(r.Ok), device.Name) +'),
 ('var msg = $"ضربان شبیه‌سازی شد — «{device.Name}» آنلاین در نظر گرفته می‌شود (تا {Jalali.Num(Math.Max(3 * (device.PollingInterval == 0 ? 60 : device.PollingInterval), 30))} ثانیه)";',
  'var msg = HrT.F("ضربان شبیه‌سازی شد — «{0}» آنلاین در نظر گرفته می‌شود (تا {1} ثانیه)", device.Name, HrT.Num(Math.Max(3 * (device.PollingInterval == 0 ? 60 : device.PollingInterval), 30)));'),
 ('var msg = $"توکن اتصال «{device.Name}» چرخش شد";',
  'var msg = HrT.F("توکن اتصال «{0}» چرخش شد", device.Name);'),
 ('var msg = $"دستگاه «{device.Name}» ({device.Kind} {device.Brand}) در شرکت «{company.Name}» ثبت شد — گیت‌وی C# به‌صورت خودکار آن را مدیریت می‌کند";',
  'var msg = HrT.F("دستگاه «{0}» ({1} {2}) در شرکت «{3}» ثبت شد — گیت‌وی C# به‌صورت خودکار آن را مدیریت می‌کند", device.Name, device.Kind, device.Brand, company.Name);'),
 ('var msg = $"مشخصات دستگاه «{device.Name}» ذخیره شد — کانفیگ گیت‌وی به‌روز شد";',
  'var msg = HrT.F("مشخصات دستگاه «{0}» ذخیره شد — کانفیگ گیت‌وی به‌روز شد", device.Name);'),
],
"modules/HrModules.Devices/Controllers/BiometricsController.cs": [
 ('var msg = $"دستور «دریافت فهرست کاربران» در صف {device.Name} ثبت شد — پس از تحویل گیت‌وی، پرسنل دستگاه در دیتابیس ذخیره می‌شود";',
  'var msg = HrT.F("دستور «{0}» در صف {1} ثبت شد — پس از تحویل گیت‌وی، پرسنل دستگاه در دیتابیس ذخیره می‌شود", HrT.T("دریافت فهرست کاربران"), device.Name);'),
 ('var msg = $"دستور «دریافت اطلاعات هویتی» در صف {device.Name} ثبت شد — قالب‌ها پس از دریافت، در دیتابیس ذخیره و به پرسنل متصل می‌شوند";',
  'var msg = HrT.F("دستور «{0}» در صف {1} ثبت شد — قالب‌ها پس از دریافت، در دیتابیس ذخیره و به پرسنل متصل می‌شوند", HrT.T("دریافت اطلاعات هویتی"), device.Name);'),
 ('var msg = $"انتقال دستگاه←دستگاه از «{source.Name}»: {Jalali.Num(userCount)} کاربر + {Jalali.Num(bioCount)} قالب هویتی → {Jalali.Num(queued)} دستور در صف";',
  'var msg = HrT.F("انتقال دستگاه←دستگاه از «{0}»: {1} کاربر + {2} قالب هویتی → {3} دستور در صف", source.Name, HrT.Num(userCount), HrT.Num(bioCount), HrT.Num(queued));'),
 ('var msg = $"دستور حذف کاربر «{name}» (پین {Jalali.ToPersianDigits(du.Pin)}) از {du.Device?.Name} ثبت شد و نسخه محلی پاک شد";',
  'var msg = HrT.F("دستور حذف کاربر «{0}» (پین {1}) از {2} ثبت شد و نسخه محلی پاک شد", name, du.Pin, du.Device?.Name);'),
],
"modules/HrModules.Org/Controllers/EmployeesController.cs": [
 ('ToastOk($"ورود گروهی به «{company.Name}» انجام شد — {inserted} پرسنل ثبت شد" + (skipped > 0 ? $" ({skipped} ردیف تکراری نادیده گرفته شد)" : ""));',
  'ToastOk(HrT.F("ورود گروهی به «{0}» انجام شد — {1} پرسنل ثبت شد", company.Name, HrT.Num(inserted)) + (skipped > 0 ? HrT.F(" ({0} ردیف تکراری نادیده گرفته شد)", HrT.Num(skipped)) : ""));'),
 ('if (Ajax) return AjaxResult(true, $"ورود گروهی انجام شد — {inserted} پرسنل ثبت شد" + (skipped > 0 ? $" ({skipped} ردیف تکراری نادیده گرفته شد)" : ""));',
  'if (Ajax) return AjaxResult(true, HrT.F("ورود گروهی انجام شد — {0} پرسنل ثبت شد", HrT.Num(inserted)) + (skipped > 0 ? HrT.F(" ({0} ردیف تکراری نادیده گرفته شد)", HrT.Num(skipped)) : ""));'),
 ('ToastOk($"ورود تردد تاریخی انجام شد — {inserted} رکورد ثبت شد" + (skipped > 0 ? $" ({skipped} تکراری نادیده گرفته شد)" : ""));',
  'ToastOk(HrT.F("ورود تردد تاریخی انجام شد — {0} رکورد ثبت شد", HrT.Num(inserted)) + (skipped > 0 ? HrT.F(" ({0} تکراری نادیده گرفته شد)", HrT.Num(skipped)) : ""));'),
 ('if (Ajax) return AjaxResult(true, $"ورود تردد تاریخی انجام شد — {inserted} رکورد ثبت شد" + (skipped > 0 ? $" ({skipped} تکراری نادیده گرفته شد)" : ""));',
  'if (Ajax) return AjaxResult(true, HrT.F("ورود تردد تاریخی انجام شد — {0} رکورد ثبت شد", HrT.Num(inserted)) + (skipped > 0 ? HrT.F(" ({0} تکراری نادیده گرفته شد)", HrT.Num(skipped)) : ""));'),
],
"modules/HrModules.Attendance/Controllers/AttendanceController.cs": [
 ('ToastOk($"ثبت {(kind == "in" ? "ورود" : kind == "out" ? "خروج" : "ورود و خروج")} برای «{emp.FullName}» انجام شد");',
  'ToastOk(HrT.F("ثبت {0} برای «{1}» انجام شد", kind == "in" ? HrT.T("ورود") : kind == "out" ? HrT.T("خروج") : HrT.T("ورود و خروج"), emp.FullName));'),
],
"modules/HrModules.Parking/Controllers/ParkingController.cs": [
 ('var msg = $"دستور ضربان برای «{cam.Name}» صف شد — در اولین اتصال دوربین تحویل می‌گیرد";',
  'var msg = HrT.F("دستور ضربان برای «{0}» صف شد — در اولین اتصال دوربین تحویل می‌گیرد", cam.Name);'),
 ('var msg = $"ضربان شبیه‌سازی شد — «{cam.Name}» آنلاین در نظر گرفته می‌شود (تا {Jalali.Num(sec)} ثانیه)";',
  'var msg = HrT.F("ضربان شبیه‌سازی شد — «{0}» آنلاین در نظر گرفته می‌شود (تا {1} ثانیه)", cam.Name, HrT.Num(sec));'),
 ('var msg = $"توکن اتصال «{cam.Name}» چرخش شد — توکن قبلی باطل شد";',
  'var msg = HrT.F("توکن اتصال «{0}» چرخش شد — توکن قبلی باطل شد", cam.Name);'),
 ('var msg = $"دستور بازکردن راهبند «{cam.Gate.Name}» برای دوربین «{cam.Name}» صف شد";',
  'var msg = HrT.F("دستور بازکردن راهبند «{0}» برای دوربین «{1}» صف شد", cam.Gate.Name, cam.Name);'),
],
"HrAttendance.Web/Modules/Core/Controllers/CompaniesController.cs": [
 ('var msg = $"شرکت «{company.Name}» با کد {company.Code} ثبت شد — چارت سازمانی و تنظیمات مستقل آن آماده است";',
  'var msg = HrT.F("شرکت «{0}» با کد {1} ثبت شد — چارت سازمانی و تنظیمات مستقل آن آماده است", company.Name, company.Code);'),
 ('var msg = $"مشخصات شرکت «{company.Name}» ذخیره شد";',
  'var msg = HrT.F("مشخصات شرکت «{0}» ذخیره شد", company.Name);'),
 ('var msg = $"شرکت «{company.Name}» حذف شد";',
  'var msg = HrT.F("شرکت «{0}» حذف شد", company.Name);'),
 ('if (empCount > 0) refs.Add($"{Jalali.Num(empCount)} پرسنل");',
  'if (empCount > 0) refs.Add(HrT.F("{0} پرسنل", HrT.Num(empCount)));'),
 ('if (deptCount > 0) refs.Add($"{Jalali.Num(deptCount)} واحد سازمانی");',
  'if (deptCount > 0) refs.Add(HrT.F("{0} واحد سازمانی", HrT.Num(deptCount)));'),
 ('if (shiftCount > 0) refs.Add($"{Jalali.Num(shiftCount)} شیفت");',
  'if (shiftCount > 0) refs.Add(HrT.F("{0} شیفت", HrT.Num(shiftCount)));'),
 ('if (deviceCount > 0) refs.Add($"{Jalali.Num(deviceCount)} دستگاه");',
  'if (deviceCount > 0) refs.Add(HrT.F("{0} دستگاه", HrT.Num(deviceCount)));'),
 ('if (zoneCount > 0) refs.Add($"{Jalali.Num(zoneCount)} منطقه");',
  'if (zoneCount > 0) refs.Add(HrT.F("{0} منطقه", HrT.Num(zoneCount)));'),
 ('if (visitorCount > 0) refs.Add($"{Jalali.Num(visitorCount)} بازدیدکننده");',
  'if (visitorCount > 0) refs.Add(HrT.F("{0} بازدیدکننده", HrT.Num(visitorCount)));'),
 ('if (contractorCount > 0) refs.Add($"{Jalali.Num(contractorCount)} پیمانکار");',
  'if (contractorCount > 0) refs.Add(HrT.F("{0} پیمانکار", HrT.Num(contractorCount)));'),
 ('if (userCount > 0) refs.Add($"{Jalali.Num(userCount)} کاربر");',
  'if (userCount > 0) refs.Add(HrT.F("{0} کاربر", HrT.Num(userCount)));'),
 ('if (runCount > 0) refs.Add($"{Jalali.Num(runCount)} لیست حقوق");',
  'if (runCount > 0) refs.Add(HrT.F("{0} لیست حقوق", HrT.Num(runCount)));'),
 ('if (lotCount > 0) refs.Add($"{Jalali.Num(lotCount)} پارکینگ");',
  'if (lotCount > 0) refs.Add(HrT.F("{0} پارکینگ", HrT.Num(lotCount)));'),
 ('if (vehicleCount > 0) refs.Add($"{Jalali.Num(vehicleCount)} خودرو");',
  'if (vehicleCount > 0) refs.Add(HrT.F("{0} خودرو", HrT.Num(vehicleCount)));'),
 ('var m = $"حذف ممکن نیست: شرکت «{company.Name}» {string.Join("، ", refs)} دارد؛ ابتدا داده‌ها را منتقل یا حذف کنید";',
  'var m = HrT.F("حذف ممکن نیست: شرکت «{0}» {1} دارد؛ ابتدا داده‌ها را منتقل یا حذف کنید", company.Name, string.Join(HrT.T("، "), refs));'),
],
"modules/HrModules.Payroll/Controllers/PayrollController.cs": [
 ('return ToastDone(true, $"قلم «{title}» ذخیره شد", $"/Payroll/Items?companyId={companyId}");',
  'return ToastDone(true, HrT.F("قلم «{0}» ذخیره شد", title), $"/Payroll/Items?companyId={companyId}");'),
 ('return ToastDone(true, $"قلم «{item.Title}» حذف شد", $"/Payroll/Items?companyId={item.CompanyId}");',
  'return ToastDone(true, HrT.F("قلم «{0}» حذف شد", item.Title), $"/Payroll/Items?companyId={item.CompanyId}");'),
 ('return ToastDone(true, $"پارامتر «{title}» ذخیره شد", $"/Payroll/Params?companyId={companyId}&year={jYear}");',
  'return ToastDone(true, HrT.F("پارامتر «{0}» ذخیره شد", title), $"/Payroll/Params?companyId={companyId}&year={jYear}");'),
 ('return ToastDone(true, $"پارامتر «{p.Title}» حذف شد", $"/Payroll/Params?companyId={companyId}&year={jYear}");',
  'return ToastDone(true, HrT.F("پارامتر «{0}» حذف شد", p.Title), $"/Payroll/Params?companyId={companyId}&year={jYear}");'),
 ('return ToastDone(true, $"{Jalali.Num(rows.Count)} ردیف عیدی/سنوات نهایی شد", $"/Payroll/Bonus?companyId={companyId}&year={jYear}");',
  'return ToastDone(true, HrT.F("{0} ردیف عیدی/سنوات نهایی شد", HrT.Num(rows.Count)), $"/Payroll/Bonus?companyId={companyId}&year={jYear}");'),
],
"HrAttendance.Web/Modules/Core/Controllers/UsersController.cs": [
 ('var msg = $"کاربر «{user.Username}» ایجاد شد" +\n                  (roles.Count > 0 ? $" با {Jalali.Num(roles.Count)} نقش" : "") +\n                  (isActive ? "" : " (غیرفعال)");',
  'var msg = HrT.F("کاربر «{0}» ایجاد شد", user.Username) +\n                  (roles.Count > 0 ? HrT.F(" با {0} نقش", HrT.Num(roles.Count)) : "") +\n                  (isActive ? "" : HrT.T(" (غیرفعال)"));'),
 ('var umsg = $"اتصال کاربر «{user.Username}» از پرسنل «{oldEmp?.FullName}» قطع شد";',
  'var umsg = HrT.F("اتصال کاربر «{0}» از پرسنل «{1}» قطع شد", user.Username, oldEmp?.FullName);'),
 ('var msg = $"حساب «{u.Username}» بازگشایی شد";',
  'var msg = HrT.F("حساب «{0}» بازگشایی شد", u.Username);'),
 ('var msg = $"رمز «{u.Username}» بازنشانی شد";',
  'var msg = HrT.F("رمز «{0}» بازنشانی شد", u.Username);'),
 ('var msg = $"حساب «{u.Username}» {u.StatusFa()}";',
  'var msg = HrT.F("حساب «{0}» {1}", u.Username, u.StatusFa());'),
 ('var msg = $"{tokens.Count} نشست فعال لغو شد";',
  'var msg = HrT.F("{0} نشست فعال لغو شد", HrT.Num(tokens.Count));'),
 ('var msg = $"نقش‌های «{u.Username}» به‌روزرسانی شد — بلافاصله برای نشست فعال او اعمال می‌شود";',
  'var msg = HrT.F("نقش‌های «{0}» به‌روزرسانی شد — بلافاصله برای نشست فعال او اعمال می‌شود", u.Username);'),
 ('var msg = $"دسترسی‌های نقش «{role.Title}» به‌روزرسانی شد — بلافاصله برای نشست‌های فعال اعمال شد";',
  'var msg = HrT.F("دسترسی‌های نقش «{0}» به‌روزرسانی شد — بلافاصله برای نشست‌های فعال اعمال شد", role.Title);'),
 ('public static string StatusFa(this User u) => u.IsActive ? "فعال است" : "غیرفعال شد";',
  'public static string StatusFa(this User u) => u.IsActive ? HrT.T("فعال است") : HrT.T("غیرفعال شد");'),
],
"HrAttendance.Web/Modules/Core/Controllers/SettingsController.cs": [
 ('if (Ajax) return AjaxResult(true, $"پالت «{p.Name}» روی کل سامانه اعمال شد", new { css });\n        TempData["ToastOk"] = $"پالت «{p.Name}» روی کل سامانه اعمال شد";',
  'if (Ajax) return AjaxResult(true, HrT.F("پالت «{0}» روی کل سامانه اعمال شد", p.Name), new { css });\n        TempData["ToastOk"] = HrT.F("پالت «{0}» روی کل سامانه اعمال شد", p.Name);'),
 ('if (Ajax) return AjaxResult(true, "پالت فعال کنار گذاشته شد — رنگ‌های اصلی نرم‌افزار بازگشت", new { css = "" });\n        TempData["ToastOk"] = "پالت فعال کنار گذاشته شد — رنگ‌های اصلی نرم‌افزار بازگشت";',
  'if (Ajax) return AjaxResult(true, HrT.T("پالت فعال کنار گذاشته شد — رنگ‌های اصلی نرم‌افزار بازگشت"), new { css = "" });\n        TempData["ToastOk"] = HrT.T("پالت فعال کنار گذاشته شد — رنگ‌های اصلی نرم‌افزار بازگشت");'),
],
"HrAttendance.Web/Modules/Core/Controllers/NotificationsController.cs": [
 ('ToastOk(isNew ? "قاعده اعلان جدید ذخیره شد" : "قاعده اعلان ویرایش شد");',
  'ToastOk(isNew ? HrT.T("قاعده اعلان جدید ذخیره شد") : HrT.T("قاعده اعلان ویرایش شد"));'),
],
"HrAttendance.Web/Modules/Core/Controllers/LicenseController.cs": [
 ('ToastOk($"جفت‌کلید جدید ساخته شد — اثر انگشت: {fpr}. کلید خصوصی را دانلود و در محل امن نگه دارید.");',
  'ToastOk(HrT.F("جفت‌کلید جدید ساخته شد — اثر انگشت: {0}. کلید خصوصی را دانلود و در محل امن نگه دارید.", fpr));'),
],
"HrAttendance.Web/Modules/Core/Controllers/HomeController.cs": [],
"HrAttendance.Web/Modules/Core/Controllers/AccountController.cs": [],
"modules/HrModules.DeviceGateway/Controllers/DeviceGatewayController.cs": [
 ('message = res.IsSuccessStatusCode ? "سرویس خارجی در دسترس است" : $"سرویس خارجی پاسخ HTTP {(int)res.StatusCode} داد",',
  'message = res.IsSuccessStatusCode ? HrT.T("سرویس خارجی در دسترس است") : HrT.F("سرویس خارجی پاسخ HTTP {0} داد", (int)res.StatusCode),'),
],
}

total, missing = 0, 0
for f, reps in FILES.items():
    p = "/home/z/my-project/aspnet/src/" + f
    src = open(p, encoding="utf-8").read()
    for old, new in reps:
        if old in src:
            src = src.replace(old, new); total += 1
        else:
            print("NOT FOUND:", f, "|", old[:80]); missing += 1
    open(p, "w", encoding="utf-8").write(src)
print(f"replaced={total} missing={missing}")
