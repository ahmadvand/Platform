# -*- coding: utf-8 -*-
# افزودن کلیدهای ترجمهٔ انگلیسیِ غایب به DictEn در HrLang.cs — idempotent
import re, io

PATH = "/home/z/my-project/aspnet/src/HrPlatform.Core/Services/HrLang.cs"

NEW = """
        // ---------- تکمیل پوشش پیام‌ها (تسک «پیام چندزبانه» — فقط کلیدهای غایب) ----------
        ["نام کاربری / کد پرسنلی و رمز عبور / کد ملی را وارد کنید."] = "Enter username / personnel ID and password / national ID.",
        ["رمز عبور شما با موفقیت تغییر کرد ✓"] = "Your password changed successfully ✓",
        ["عکس پروفایل شما به‌روزرسانی شد ✓"] = "Your profile photo updated ✓",
        ["عکس پروفایل حذف شد — آواتار حرفی جایگزین می‌شود"] = "Profile photo removed — a letter avatar is shown instead",
        ["کاربر حذف شد"] = "User deleted",
        ["نقش «{0}» به {1} کاربر اختصاص دارد"] = "Role «{0}» is assigned to {1} user(s)",
        ["اطلاعات شرکت ذخیره شد و در سراسر سامانه اعمال شد"] = "Company info saved and applied system-wide",
        ["پالت فعال کنار گذاشته شد — رنگ‌های اصلی نرم‌افزار بازگشت"] = "Active palette set aside — the original software colors are restored",
        ["لوگو بارگذاری شد و در سایدبار، صفحه ورود و عنوان مرورگر اعمال شد"] = "Logo uploaded and applied to the sidebar, sign-in page and browser title",
        ["لوگو حذف شد و نشان پیش‌فرض جایگزین شد"] = "Logo removed — the default mark is restored",
        ["تنظیمات اعلان ذخیره شد"] = "Notification settings saved",
        ["قاعده اعلان حذف شد"] = "Notification rule deleted",
        ["هشدار حل‌شده علامت‌گذاری شد"] = "Alert marked as resolved",
        ["هشدار منقضی شد"] = "Alert expired",
        ["هشدار منقضی شد (از فهرست باز خارج شد — سابقه حفظ شد)"] = "Alert expired (removed from the open list — history kept)",
        ["مجوز ثبت شد"] = "License registered",
        ["جفت‌کلید این نصب حذف شد — فقط مجوزهای امضاشده با کلید مرجع تولیدکننده پذیرفته می‌شوند"] = "This installation's key pair was deleted — only licenses signed with the vendor reference key are accepted",
        ["سامانه از قبل پیکربندی شده است"] = "The system is already configured",
        ["اتصال برقرار شد اما آماده‌سازی بانک ناموفق بود: {0}"] = "Connection succeeded but database preparation failed: {0}",
        ["اتصال برقرار شد اما ساخت بانک ناموفق بود: {0}"] = "Connection succeeded but database creation failed: {0}",
        ["بانک آماده شد اما مجوز پذیرفته نشد — {0}"] = "Database ready but the license was rejected — {0}",
        [" (مجوز: فعال نشد — {0})"] = " (License: not activated — {0})",
        ["پیکربندی بانک داده ذخیره و بلافاصله اعمال شد (بدون نیاز به ری‌استارت)"] = "Database configuration saved and applied immediately (no restart needed)",
        ["واحد سازمانی یافت نشد"] = "Organizational unit not found",
        ["واحد سازمانی انتخاب‌شده یافت نشد"] = "Selected organizational unit not found",
        ["پست سازمانی یافت نشد"] = "Position not found",
        ["پرسنل یافت نشد"] = "Employee not found",
        ["کد واحد سازمانی در این شرکت تکراری است"] = "Unit code already exists in this company",
        ["کد پست سازمانی در این شرکت تکراری است"] = "Position code already exists in this company",
        ["کد ملی تکراری است"] = "National ID already exists",
        ["کد ملی تکراری است (متعلق به پرسنل دیگری است)"] = "National ID already exists (belongs to another employee)",
        ["کد پرسنلی تکراری است"] = "Personnel code already exists",
        ["کد پرسنلی تکراری است (متعلق به پرسنل دیگری است)"] = "Personnel code already exists (belongs to another employee)",
        ["یک واحد نمی‌تواند زیرمجموعه خودش باشد"] = "A unit cannot be a child of itself",
        ["واحد والد باید در همان شرکتِ واحد جدید باشد"] = "The parent unit must belong to the same company as the moved unit",
        ["جابجایی بین شرکت‌ها مجاز نیست — واحد والد باید در همان شرکت باشد"] = "Cross-company moves are not allowed — the parent unit must be in the same company",
        ["جابجایی مجاز نیست: واحد انتخاب‌شده زیرمجموعهٔ همین واحد است (ایجاد حلقه در چارت)"] = "Move not allowed: the selected unit is a descendant of this unit (chart cycle)",
        ["انتقال پست به واحد شرکت دیگر مجاز نیست — پست و واحد باید هم‌شرکت باشند"] = "A position cannot move to another company's unit — position and unit must share the same company",
        ["خطا در ایجاد واحد: {0}"] = "Error creating the unit: {0}",
        ["خطا در ایجاد پست: {0}"] = "Error creating the position: {0}",
        ["خطا در ویرایش واحد: {0}"] = "Error updating the unit: {0}",
        ["خطا در جابجایی واحد: {0}"] = "Error moving the unit: {0}",
        ["خطا در حذف واحد: {0}"] = "Error deleting the unit: {0}",
        ["خطا در ویرایش پست: {0}"] = "Error updating the position: {0}",
        ["خطا در حذف پست: {0}"] = "Error deleting the position: {0}",
        ["خطا در ثبت پرسنل: {0}"] = "Error registering the employee: {0}",
        ["خطا در ذخیره اطلاعات پرسنل: {0}"] = "Error saving employee info: {0}",
        ["تاریخ نامعتبر است: {0}"] = "Invalid date: {0}",
        ["تعطیلی حذف شد"] = "Holiday deleted",
        ["درخواست مرخصی ثبت شد و در انتظار تأیید سرپرست است."] = "Leave request submitted — awaiting supervisor approval.",
        ["درخواست مأموریت ثبت شد و در انتظار تأیید سرپرست است."] = "Mission request submitted — awaiting supervisor approval.",
        ["درخواست اصلاح تردد ثبت شد و در انتظار تأیید سرپرست است."] = "Attendance correction submitted — awaiting supervisor approval.",
        ["داده‌ای برای ثبت نیست"] = "Nothing to record",
        ["داده جای‌گذاری خالی است"] = "Punch data is empty",
        ["داده جای‌گذاری نامعتبر است"] = "Invalid punch data",
        ["داده جای‌گذاری قابل پردازش نبود"] = "Punch data could not be processed",
        ["اولویت منبع تردد باید ۱، ۲ یا ۳ باشد"] = "Attendance source priority must be 1, 2 or 3",
        ["دستور راه‌اندازی مجدد ثبت شد — حداکثر در چرخهٔ بعدی supervisor اعمال می‌شود"] = "Reboot command queued — applied by the supervisor at the latest in its next cycle",
        ["فقط فایل xlsx پشتیبانی می‌شود"] = "Only .xlsx files are supported",
        ["لاگ فعالیت پاک شد"] = "Activity log cleared",
        ["این پرسنل قبلاً به گروه اضافه شده است"] = "This employee is already in the group",
        ["پرسنل به گروه اضافه شد"] = "Employee added to the group",
        ["پرسنل از گروه حذف شد"] = "Employee removed from the group",
        ["گروه حذف شد"] = "Group deleted",
        ["گیت گروه ذخیره شد"] = "Gate groups saved",
        ["گیت از گروه حذف شد"] = "Gate removed from the group",
        ["هر دو ساعت شروع و پایان را وارد کنید یا هر دو را خالی بگذارید"] = "Enter both start and end times, or leave both empty",
        ["منطقه انتخاب‌شده یافت نشد"] = "Selected zone not found",
        ["کد منطقه در این شرکت تکراری است"] = "Zone code already exists in this company",
        ["این شماره کارت قبلاً ثبت شده است"] = "This card number is already registered",
        ["کارت باطل قابل تخصیص نیست"] = "A voided card cannot be assigned",
        ["کارت مفقود قابل تخصیص نیست — ابتدا کارت جدید صادر کنید"] = "A lost card cannot be assigned — issue a new card first",
        ["کارت دارای دارنده است — ابتدا عودت یا ابطال کنید"] = "The card has a holder — return or revoke it first",
        ["کارت فاقد دارنده است — ابتدا تخصیص دهید"] = "The card has no holder — assign it first",
        ["مجوز دسترسی ثبت شد"] = "Access permission recorded",
        ["کارت در مخزن ثبت نشده است"] = "Card is not registered in the card bank",
        ["کارت به‌عنوان مفقود اعلام شده است"] = "Card is declared lost",
        ["کارت باطل شده است"] = "Card is voided",
        ["کارت به پرسنلی تخصیص نیافته است"] = "Card is not assigned to any employee",
        ["کارت فاقد دارنده است"] = "Card has no holder",
        ["ورود غیرمجاز"] = "Unauthorized entry",
        ["کارت گمشده"] = "Lost card",
        ["مجوز منقضی"] = "Expired permission",
        ["سال جلالی نامعتبر است"] = "Invalid Jalali year",
        ["روزهای ماه باید بین ۲۸ تا ۳۱ باشد"] = "Days of month must be between 28 and 31",
        ["واحد رند نمی‌تواند منفی باشد"] = "Rounding unit cannot be negative",
        ["روش محاسبه نامعتبر است"] = "Invalid calculation method",
        ["ساعت پایه ماه باید بین ۱۲۰ تا ۲۶۰ باشد"] = "Monthly base hours must be between 120 and 260",
        ["هر پله باید «to» و «rate» داشته باشد"] = "Each bracket needs “to” and “rate”",
        ["پلکان مالیات به JSON نیاز دارد — نمونه: {0}"] = "The tax bracket table requires JSON — example: {0}",
        ["JSON پلکان مالیات نامعتبر است — نمونه: {0}"] = "Invalid tax bracket JSON — example: {0}",
        ["نوع قلم نامعتبر است"] = "Pay item type is invalid",
        ["کد اقلام سیستمی قابل تغییر نیست"] = "System item codes cannot be changed",
        ["این قلم خارج از دامنهٔ دسترسی شماست"] = "This pay item is outside your access scope",
        ["کد پارامتر باید حداقل ۲ کاراکتر باشد"] = "Parameter code must be at least 2 characters",
        ["این پارامتر خارج از دامنهٔ دسترسی شماست"] = "This parameter is outside your access scope",
        ["لیست حقوق حذف شد"] = "Payroll run deleted",
        ["لیست حقوق نهایی شد"] = "Payroll run finalized",
        ["ردیف پیش‌نویسی برای حذف وجود ندارد"] = "No draft rows to delete",
        ["ردیف پیش‌نویسی برای نهایی‌سازی وجود ندارد — ابتدا محاسبه کنید"] = "No draft rows to finalize — run the calculation first",
        ["این فیش خارج از دامنهٔ دسترسی شماست"] = "This payslip is outside your access scope",
        ["این لیست حقوق خارج از دامنهٔ دسترسی شماست"] = "This payroll run is outside your access scope",
        ["حذف پیش‌نویس کل شرکت خارج از دامنهٔ دسترسی شماست"] = "Deleting the whole company's drafts is outside your access scope",
        ["نهایی‌سازی کل شرکت خارج از دامنهٔ دسترسی شماست"] = "Finalizing the whole company is outside your access scope",
        ["محاسبهٔ عیدی/سنوات کل شرکت است — حساب شما به واحد/شخص مشخص محدود شده است"] = "The bonus/tenure calculation covers the whole company — your account is limited to specific units/people",
        ["این تسویه‌حساب خارج از دامنهٔ دسترسی شماست"] = "This settlement is outside your access scope",
        ["این تسویه‌حساب قبلاً نهایی شده است"] = "This settlement is already finalized",
        ["تسویه‌حساب نهایی‌شده قابل حذف نیست"] = "A finalized settlement cannot be deleted",
        ["پیش‌نویس تسویه‌حساب حذف شد"] = "Settlement draft deleted",
        ["پیش‌نویس‌های عیدی/سنوات حذف شد"] = "Bonus/tenure drafts deleted",
        ["تاریخ پایان همکاری نمی‌تواند قبل از تاریخ استخدام باشد"] = "The termination date cannot be before the hire date",
        ["محاسبه متوقف شد: {0}"] = "Calculation aborted: {0}",
        ["خطا در محاسبه: {0}"] = "Calculation error: {0}",
        ["خطای فرمول: {0}"] = "Formula error: {0}",
        ["نهایی"] = "Finalized",
        ["پیش‌نویس"] = "Draft",
        ["گیت غیرفعال است"] = "Gate is inactive",
        ["گیت قفل دائم است"] = "Gate is permanently locked",
        ["جهت عبور با گیت هم‌خوانی ندارد"] = "Pass direction does not match the gate",
        ["خودرو فاقد مجوز پارکینگ است"] = "Vehicle has no parking permit",
        ["خودرو غیرفعال است (توقیف/خارج از سرویس)"] = "Vehicle is inactive (impounded / out of service)",
        ["ظرفیت پارکینگ تکمیل است"] = "Parking is at full capacity",
        ["ورود"] = "Entry",
        ["خروج"] = "Exit",
        ["داخل سازمان"] = "Inside the organization",
"""

src = io.open(PATH, encoding="utf-8").read()

# کلیدهای موجود (فقط داخل بلوک DictEn — کل فایل همان یک دیکشنری است)
existing = set(re.findall(r'^\s*\["(.+?)"\]\s*=', src, re.M))

added, skipped = 0, []
out = src
for line in NEW.strip("\n").split("\n"):
    m = re.match(r'^\s*\["(.+?)"\]\s*=\s*"(.+)",\s*$', line)
    if not m:
        continue  # خطوط کامنت
    key, val = m.group(1), m.group(2)
    if key in existing:
        skipped.append(key)
        continue
    out = out.replace('        ["مثل 14/14 یا 21/7 یا 28/28 — برای اقماری الزامی است"] = "e.g. 14/14, 21/7 or 28/28 — required for remote shifts",\n    };',
                      '        ["مثل 14/14 یا 21/7 یا 28/28 — برای اقماری الزامی است"] = "e.g. 14/14, 21/7 or 28/28 — required for remote shifts",\n'
                      + line + "\n    };", 1)
    existing.add(key)
    added += 1

# روش جایگزینی بالا فقط اولین بار کار می‌کند؛ برای بقیه خطوط باید همه را یکجا بگذاریم
if added:
    # بازسازی: همهٔ خطوط اضافه‌شده را پشت سر هم قبل از }; بگذار
    missing_lines = []
    src2 = io.open(PATH, encoding="utf-8").read()
    exist2 = set(re.findall(r'^\s*\["(.+?)"\]\s*=', src2, re.M))
    for line in NEW.strip("\n").split("\n"):
        m = re.match(r'^\s*\["(.+?)"\]\s*=\s*"(.+)",\s*$', line)
        if not m:
            continue
        if m.group(1) not in exist2:
            missing_lines.append(line)
    block = "\n".join(missing_lines)
    out2 = src2.replace('        ["مثل 14/14 یا 21/7 یا 28/28 — برای اقماری الزامی است"] = "e.g. 14/14, 21/7 or 28/28 — required for remote shifts",\n    };',
                        '        ["مثل 14/14 یا 21/7 یا 28/28 — برای اقماری الزامی است"] = "e.g. 14/14, 21/7 or 28/28 — required for remote shifts",\n\n'
                        + block + "\n    };", 1)
    if out2 == src2:
        print("ERROR: anchor not found — no change written")
    else:
        io.open(PATH, "w", encoding="utf-8").write(out2)
        print(f"added {len(missing_lines)} entries")
else:
    print("nothing to add")
if skipped:
    print(f"skipped (already present): {len(skipped)}")
