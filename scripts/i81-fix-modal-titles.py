#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۸۱ (Task E) — ترجمهٔ پیشوند استاتیک فارسی در data-modal-title ویوها.
الگو:  data-modal-title="عنوان فارسی — @داینامیک"
تبدیل: data-modal-title="@HrT.T("عنوان فارسی") — @داینامیک"
مقادیر کاملاً استاتیک → data-modal-title="@HrT.T("کل متن")"
"""
import re, os

ROOT = "/home/z/my-project/aspnet/src/"
FA = re.compile(r'[\u0600-\u06FF]')

# (file, line, old_substr, new_substr) — صریح و بازبینی‌شده
FIXES = [
    ("modules/HrModules.Payroll/Views/Payroll/Params.cshtml", 31,
     'data-modal-title="پارامتر جدید — @HrT.Num(year)"',
     'data-modal-title="@HrT.T("پارامتر جدید") — @HrT.Num(year)"'),
    ("modules/HrModules.Payroll/Views/Payroll/Params.cshtml", 62,
     'data-modal-title="ویرایش پارامتر — @p.Title"',
     'data-modal-title="@HrT.T("ویرایش پارامتر") — @p.Title"'),
    ("modules/HrModules.Payroll/Views/Payroll/Items.cshtml", 90,
     'data-modal-title="ویرایش قلم — @i.Title"',
     'data-modal-title="@HrT.T("ویرایش قلم") — @i.Title"'),
    ("modules/HrModules.Payroll/Views/Payroll/Index.cshtml", 140,
     'data-modal-title="جزئیات فیش — @p.Employee.FullName"',
     'data-modal-title="@HrT.T("جزئیات فیش") — @p.Employee.FullName"'),
    ("modules/HrModules.Payroll/Views/Payroll/_SlipLines.cshtml", 9,
     'data-modal-title="فیش حقوقی — @p.Employee?.FullName (@(p.Run?.Company?.Name ?? ""))"',
     'data-modal-title="@HrT.T("فیش حقوقی") — @p.Employee?.FullName (@(p.Run?.Company?.Name ?? ""))"'),
    ("modules/HrModules.Access/Views/Access/_DoorStateForm.cshtml", 4,
     'data-modal-title="حالت درب — @gate.Name"',
     'data-modal-title="@HrT.T("حالت درب") — @gate.Name"'),
    ("modules/HrModules.Access/Views/Access/_GateScheduleForm.cshtml", 4,
     'data-modal-title="برنامه زمانی گیت — @gate.Name"',
     'data-modal-title="@HrT.T("برنامه زمانی گیت") — @gate.Name"'),
    ("modules/HrModules.Access/Views/Access/Index.cshtml", 127,
     'data-modal-title="برنامه زمانی گیت — @g.Name"',
     'data-modal-title="@HrT.T("برنامه زمانی گیت") — @g.Name"'),
    ("modules/HrModules.Access/Views/Access/Index.cshtml", 128,
     'data-modal-title="حالت درب — @g.Name"',
     'data-modal-title="@HrT.T("حالت درب") — @g.Name"'),
    ("modules/HrModules.Devices/Views/Devices/Index.cshtml", 133,
     'data-modal-title="ویرایش دستگاه — @d.Name"',
     'data-modal-title="@HrT.T("ویرایش دستگاه") — @d.Name"'),
    ("modules/HrModules.Org/Views/Org/_DeptEditForm.cshtml", 4,
     'data-modal-title="ویرایش واحد — @dept.Name"',
     'data-modal-title="@HrT.T("ویرایش واحد") — @dept.Name"'),
    ("modules/HrModules.Org/Views/Org/_PosEditForm.cshtml", 5,
     'data-modal-title="ویرایش پست — @pos.Title"',
     'data-modal-title="@HrT.T("ویرایش پست") — @pos.Title"'),
    ("modules/HrModules.Org/Views/Org/_DeptForm.cshtml", 5,
     'data-modal-title="واحد سازمانی جدید — @(company?.Name ?? "")"',
     'data-modal-title="@HrT.T("واحد سازمانی جدید") — @(company?.Name ?? "")"'),
    ("modules/HrModules.Org/Views/Org/_DeptMoveForm.cshtml", 6,
     'data-modal-title="انتقال واحد — @dept.Name"',
     'data-modal-title="@HrT.T("انتقال واحد") — @dept.Name"'),
    ("modules/HrModules.Org/Views/Org/_PosForm.cshtml", 5,
     'data-modal-title="پست سازمانی جدید — @(company?.Name ?? "")"',
     'data-modal-title="@HrT.T("پست سازمانی جدید") — @(company?.Name ?? "")"'),
    ("modules/HrModules.Org/Views/Org/Index.cshtml", 73,
     'data-modal-title="واحد سازمانی جدید — @(company?.Name ?? "")"',
     'data-modal-title="@HrT.T("واحد سازمانی جدید") — @(company?.Name ?? "")"'),
    ("modules/HrModules.Org/Views/Org/Index.cshtml", 95,
     'data-modal-title="ویرایش واحد — @d.Name"',
     'data-modal-title="@HrT.T("ویرایش واحد") — @d.Name"'),
    ("modules/HrModules.Org/Views/Org/Index.cshtml", 96,
     'data-modal-title="انتقال واحد — @d.Name"',
     'data-modal-title="@HrT.T("انتقال واحد") — @d.Name"'),
    ("modules/HrModules.Org/Views/Org/Index.cshtml", 142,
     'data-modal-title="پست سازمانی جدید — @(company?.Name ?? "")"',
     'data-modal-title="@HrT.T("پست سازمانی جدید") — @(company?.Name ?? "")"'),
    ("modules/HrModules.Org/Views/Org/Index.cshtml", 159,
     'data-modal-title="ویرایش پست — @p.Title"',
     'data-modal-title="@HrT.T("ویرایش پست") — @p.Title"'),
    ("modules/HrModules.Org/Views/Employees/_EditForm.cshtml", 11,
     'data-modal-title="ویرایش پرسنل — @e.FullName"',
     'data-modal-title="@HrT.T("ویرایش پرسنل") — @e.FullName"'),
    ("modules/HrModules.Org/Views/Employees/Index.cshtml", 106,
     'data-modal-title="ویرایش پرسنل — @e.FullName"',
     'data-modal-title="@HrT.T("ویرایش پرسنل") — @e.FullName"'),
    ("modules/HrModules.Attendance/Views/Shifts/Index.cshtml", 74,
     'data-modal-title="ویرایش شیفت — @s.Name"',
     'data-modal-title="@HrT.T("ویرایش شیفت") — @s.Name"'),
    ("HrAttendance.Web/Views/Users/Index.cshtml", 109,
     'data-modal-title="اتصال پرسنل — @u.Username"',
     'data-modal-title="@HrT.T("اتصال پرسنل") — @u.Username"'),
    ("HrAttendance.Web/Views/Users/Index.cshtml", 112,
     'data-modal-title="عکس پروفایل — @u.Username"',
     'data-modal-title="@HrT.T("عکس پروفایل") — @u.Username"'),
    ("HrAttendance.Web/Views/Users/_PhotoForm.cshtml", 2,
     'data-modal-title="عکس پروفایل — @Model.FullName (@Model.Username)"',
     'data-modal-title="@HrT.T("عکس پروفایل") — @Model.FullName (@Model.Username)"'),
    ("HrAttendance.Web/Views/Account/_MyPhotoForm.cshtml", 4,
     'data-modal-title="پروفایل من — @Model.FullName"',
     'data-modal-title="@HrT.T("پروفایل من") — @Model.FullName"'),
    ("HrAttendance.Web/Views/Account/_MyPasswordForm.cshtml", 4,
     'data-modal-title="تغییر رمز عبور — @Model.Username"',
     'data-modal-title="@HrT.T("تغییر رمز عبور") — @Model.Username"'),
    ("HrAttendance.Web/Views/Companies/_CompanyEditForm.cshtml", 4,
     'data-modal-title="ویرایش شرکت — @company.Name"',
     'data-modal-title="@HrT.T("ویرایش شرکت") — @company.Name"'),
    ("HrAttendance.Web/Views/Companies/Index.cshtml", 70,
     'data-modal-title="ویرایش شرکت — @c.Name"',
     'data-modal-title="@HrT.T("ویرایش شرکت") — @c.Name"'),
]

ok = err = 0
by_file = {}
for f, ln, old, new in FIXES:
    if not FA.search(old) or not FA.search(new):
        err += 1; print("!! no-fa:", f, ln); continue
    by_file.setdefault(f, []).append((ln, old, new))

for f, items in by_file.items():
    p = ROOT + f
    lines = open(p, encoding="utf-8").read().split("\n")
    for ln, old, new in items:
        idx = ln - 1
        if idx >= len(lines) or old not in lines[idx]:
            err += 1; print("!! not-found:", f, ln, old[:60]); continue
        lines[idx] = lines[idx].replace(old, new, 1)
        ok += 1
    open(p, "w", encoding="utf-8").write("\n".join(lines))

print(f"done: {ok} replaced, {err} errors")
