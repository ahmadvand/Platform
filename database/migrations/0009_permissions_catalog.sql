-- 009: کاتالوگ دسترسی‌ها (PLAN P0.3) — seed اولیه؛ منبع اصلی: src/lib/permissions.ts
SET IDENTITY_INSERT [attendance].[Permission] OFF;
IF NOT EXISTS (SELECT 1 FROM [attendance].[Permission] WHERE [key] = N'dashboard.view')
INSERT INTO [attendance].[Permission] ([key], [title], [grp]) VALUES
  (N'dashboard.view', N'مشاهده داشبورد', N'عمومی'),
  (N'employees.view', N'مشاهده پرسنل', N'منابع انسانی'),
  (N'employees.manage', N'مدیریت پرسنل و قراردادها', N'منابع انسانی'),
  (N'org.view', N'مشاهده چارت سازمانی', N'منابع انسانی'),
  (N'org.manage', N'مدیریت ساختار سازمانی', N'منابع انسانی'),
  (N'attendance.view', N'مشاهده حضور و غیاب', N'حضور و غیاب'),
  (N'attendance.manage', N'ثبت و اصلاح تردد', N'حضور و غیاب'),
  (N'attendance.approve', N'تأیید درخواست‌های تردد', N'حضور و غیاب'),
  (N'corrections.view', N'مشاهده اصلاح تردد', N'حضور و غیاب'),
  (N'corrections.approve', N'تأیید اصلاح تردد', N'حضور و غیاب'),
  (N'holidays.view', N'مشاهده تقویم تعطیلات', N'حضور و غیاب'),
  (N'holidays.manage', N'مدیریت تعطیلات', N'حضور و غیاب'),
  (N'access.view', N'مشاهده کنترل تردد', N'کنترل تردد'),
  (N'access.manage', N'مدیریت مناطق و مجوزها', N'کنترل تردد'),
  (N'visitors.view', N'مشاهده ارباب رجوع', N'کنترل تردد'),
  (N'visitors.manage', N'مدیریت مهمانان و کارت‌ها', N'کنترل تردد'),
  (N'devices.view', N'مشاهده دستگاه‌ها', N'دستگاه‌های تردد'),
  (N'devices.control', N'کنترل دستگاه‌ها', N'دستگاه‌های تردد'),
  (N'devices.manage', N'مدیریت دستگاه‌ها و انتقال کاربران', N'دستگاه‌های تردد'),
  (N'devices.poll', N'خواندن تردد از دستگاه‌ها', N'دستگاه‌های تردد'),
  (N'payroll.view', N'مشاهده حقوق و دستمزد', N'حقوق و دستمزد'),
  (N'payroll.manage', N'ایجاد و نهایی‌سازی لیست حقوق', N'حقوق و دستمزد'),
  (N'reports.export', N'خروجی اکسل و گزارش‌ها', N'حقوق و دستمزد'),
  (N'users.view', N'مشاهده کاربران و نقش‌ها', N'مدیریت سیستم'),
  (N'users.manage', N'مدیریت کاربران و نقش‌ها', N'مدیریت سیستم'),
  (N'audit.view', N'مشاهده گزارش حسابرسی', N'مدیریت سیستم');
GO
