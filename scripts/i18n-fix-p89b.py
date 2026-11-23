#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# فاز ۸۹ — دستهٔ دوم i18n: فرم دستگاه، گیت‌وی، گزینه‌های استخدام حقوق
import io

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

patch("modules/HrModules.Devices/Views/Devices/_DeviceForm.cshtml", [
    ('<div class="fld fld-full"><label>نام دستگاه *</label>\n                <input name="name" id="fName" required value="@d.Name" placeholder="مثال: ترمینال اثر انگشت ورودی اصلی" />',
     '<div class="fld fld-full"><label>@HrT.T("نام دستگاه") *</label>\n                <input name="name" id="fName" required value="@d.Name" placeholder="@HrT.T("مثال: ترمینال اثر انگشت ورودی اصلی")" />'),
    ('<div class="fld"><label>سریال *</label>', '<div class="fld"><label>@HrT.T("سریال") *</label>'),
    ('title="تولید سریال جدید بر اساس برند و مدل">⟳ تولید</button>',
     'title="@HrT.T("تولید سریال جدید بر اساس برند و مدل")">@HrT.T("⟳ تولید")</button>'),
    ('<div class="fld"><label>آدرس IP @(isEdit ? "" : "(پیشنهاد خودکار)")</label>',
     '<div class="fld"><label>@HrT.T("آدرس IP") @(isEdit ? "" : HrT.T("(پیشنهاد خودکار)"))</label>'),
    ('<div class="fld"><label>محل نصب</label>\n                <input name="location" id="fLocation" value="@d.Location" placeholder="مثال: ورودی اصلی سایت" />',
     '<div class="fld"><label>@HrT.T("محل نصب")</label>\n                <input name="location" id="fLocation" value="@d.Location" placeholder="@HrT.T("مثال: ورودی اصلی سایت")" />'),
    ('<div class="fld"><label>کارکرد *</label>', '<div class="fld"><label>@HrT.T("کارکرد") *</label>'),
    ('<div class="fld"><label>حالت اتصال *</label>', '<div class="fld"><label>@HrT.T("حالت اتصال") *</label>'),
    ('<div class="fld"><label>پورت پروتکل دستگاه</label>', '<div class="fld"><label>@HrT.T("پورت پروتکل دستگاه")</label>'),
    ('placeholder="پیش‌فرض گیت‌وی (4370)"', 'placeholder="@HrT.T("پیش‌فرض گیت‌وی (4370)")"'),
    ('placeholder="پیش‌فرض گیت‌وی (0)"', 'placeholder="@HrT.T("پیش‌فرض گیت‌وی (0)")"'),
    ('<div class="fld"><label>کلید ارتباطی (CommKey)</label>', '<div class="fld"><label>@HrT.T("کلید ارتباطی (CommKey)")</label>'),
    ('<button class="btn btn-primary">@(isEdit ? "ذخیره تغییرات" : "ثبت دستگاه")</button>',
     '<button class="btn btn-primary">@(isEdit ? HrT.T("ذخیره تغییرات") : HrT.T("ثبت دستگاه"))</button>'),
])

patch("modules/HrModules.Devices/Views/Gateway/Index.cshtml", [
    ('ViewBag.Title = "تنظیمات گیت‌وی";', 'ViewBag.Title = HrT.T("تنظیمات گیت‌وی");'),
    ('if (Model.LastFetchedAt is null) return "هنوز گیت‌وی تنظیمات را دریافت نکرده است";',
     'if (Model.LastFetchedAt is null) return HrT.T("هنوز گیت‌وی تنظیمات را دریافت نکرده است");'),
    ('return mins < 1 ? "همین حالا" : $"{Jalali.Num((int)mins)} دقیقه پیش";',
     'return mins < 1 ? HrT.T("همین حالا") : HrT.F("{0} دقیقه پیش", Jalali.Num((int)mins));'),
    ('@(FetchFresh ? "● متصل" : "○ بدون گزارش") — آخرین دریافت کانفیگ: @FetchHint()',
     '@(FetchFresh ? HrT.T("● متصل") : HrT.T("○ بدون گزارش")) — @HrT.T("آخرین دریافت کانفیگ:") @FetchHint()'),
    ('@(Model.Enabled ? "فعال" : "متوقف")</div><div class="l">وضعیت پایش</div>',
     '@(Model.Enabled ? HrT.T("فعال") : HrT.T("متوقف"))</div><div class="l">@HrT.T("وضعیت پایش")</div>'),
    ('<div class="l">پورت پل ADMS</div>', '<div class="l">@HrT.T("پورت پل ADMS")</div>'),
    ('@(d.DevicePort?.ToString() ?? "پیش‌فرض")', '@(d.DevicePort is null ? HrT.T("پیش‌فرض") : d.DevicePort?.ToString())'),
    ('@(d.CommKey?.ToString() ?? "پیش‌فرض")', '@(d.CommKey is null ? HrT.T("پیش‌فرض") : d.CommKey?.ToString())'),
])

patch("modules/HrModules.Payroll/Views/Payroll/_ItemForm.cshtml", [
    ('<option value="@t" selected="@(i.ScopeId == t)">@t</option>',
     '<option value="@t" selected="@(i.ScopeId == t)">@HrT.T(t)</option>'),
])

print("\nFILES FIXED:", len(fixed))
