// کاتالوگ متمرکز دسترسی‌ها (PLAN Phase 0 — P0.3)
// Single source of truth بین seed، API و فرانت‌اند.

export interface PermDef {
  key: string;
  title: string;
  grp: string;
}

export const PERMISSIONS: PermDef[] = [
  { key: "dashboard.view", title: "مشاهده داشبورد", grp: "عمومی" },

  { key: "employees.view", title: "مشاهده پرسنل", grp: "منابع انسانی" },
  { key: "employees.manage", title: "مدیریت پرسنل و قراردادها", grp: "منابع انسانی" },
  { key: "org.view", title: "مشاهده چارت سازمانی", grp: "منابع انسانی" },
  { key: "org.manage", title: "مدیریت ساختار سازمانی", grp: "منابع انسانی" },

  { key: "attendance.view", title: "مشاهده حضور و غیاب", grp: "حضور و غیاب" },
  { key: "attendance.manage", title: "ثبت و اصلاح تردد", grp: "حضور و غیاب" },
  { key: "attendance.approve", title: "تأیید درخواست‌های تردد", grp: "حضور و غیاب" },
  { key: "corrections.view", title: "مشاهده اصلاح تردد", grp: "حضور و غیاب" },
  { key: "corrections.approve", title: "تأیید اصلاح تردد", grp: "حضور و غیاب" },
  { key: "holidays.view", title: "مشاهده تقویم تعطیلات", grp: "حضور و غیاب" },
  { key: "holidays.manage", title: "مدیریت تعطیلات", grp: "حضور و غیاب" },

  { key: "access.view", title: "مشاهده کنترل تردد", grp: "کنترل تردد" },
  { key: "access.manage", title: "مدیریت مناطق و مجوزها", grp: "کنترل تردد" },
  { key: "visitors.view", title: "مشاهده ارباب رجوع", grp: "کنترل تردد" },
  { key: "visitors.manage", title: "مدیریت مهمانان و کارت‌ها", grp: "کنترل تردد" },

  { key: "devices.view", title: "مشاهده دستگاه‌ها", grp: "دستگاه‌های تردد" },
  { key: "devices.control", title: "کنترل دستگاه‌ها (بازکردن درب، زمان، ری‌استارت)", grp: "دستگاه‌های تردد" },
  { key: "devices.manage", title: "مدیریت دستگاه‌ها و انتقال کاربران", grp: "دستگاه‌های تردد" },
  { key: "devices.poll", title: "خواندن تردد از دستگاه‌ها (سرویس پل)", grp: "دستگاه‌های تردد" },

  { key: "payroll.view", title: "مشاهده حقوق و دستمزد", grp: "حقوق و دستمزد" },
  { key: "payroll.manage", title: "ایجاد و نهایی‌سازی لیست حقوق", grp: "حقوق و دستمزد" },
  { key: "reports.export", title: "خروجی اکسل و گزارش‌ها", grp: "حقوق و دستمزد" },

  { key: "users.view", title: "مشاهده کاربران و نقش‌ها", grp: "مدیریت سیستم" },
  { key: "users.manage", title: "مدیریت کاربران، نقش‌ها و بازنشانی رمز", grp: "مدیریت سیستم" },
  { key: "audit.view", title: "مشاهده گزارش حسابرسی", grp: "مدیریت سیستم" },
];

export const PERM_KEYS = PERMISSIONS.map((p) => p.key);

// ---------- نقش‌های پیش‌فرض ----------
export const ROLES: { name: string; title: string; description: string; perms: string[] | "*" }[] = [
  {
    name: "superadmin",
    title: "مدیر ارشد سیستم",
    description: "دسترسی کامل به همه ماژول‌ها و مدیریت کاربران",
    perms: "*",
  },
  {
    name: "hr_manager",
    title: "مدیر منابع انسانی",
    description: "پرسنل، قراردادها، حضور و غیاب، درخواست‌ها، حقوق و دستمزد",
    perms: [
      "dashboard.view", "employees.view", "employees.manage", "org.view", "org.manage",
      "attendance.view", "attendance.manage", "attendance.approve",
      "corrections.view", "corrections.approve", "holidays.view", "holidays.manage",
      "payroll.view", "payroll.manage", "reports.export",
      "users.view",
    ],
  },
  {
    name: "att_operator",
    title: "اپراتور تردد",
    description: "ثبت تردد، عملیات دستگاه‌ها و مانیتورینگ",
    perms: [
      "dashboard.view", "employees.view", "attendance.view", "attendance.manage",
      "corrections.view", "holidays.view",
      "devices.view", "devices.control", "devices.manage",
      "access.view", "reports.export",
    ],
  },
  {
    name: "security",
    title: "حراست و نگهبانی",
    description: "کنترل تردد، ارباب رجوع و هشدارهای امنیتی",
    perms: [
      "dashboard.view", "employees.view", "access.view", "access.manage",
      "visitors.view", "visitors.manage", "devices.view", "devices.control",
    ],
  },
  {
    name: "device_agent",
    title: "سرویس پل دستگاه‌ها",
    description: "حساب سرویس ویندوزی برای اتصال دستگاه‌ها (بدون ورود تعاملی)",
    perms: ["devices.view", "devices.control", "devices.manage", "attendance.manage", "devices.poll"],
  },
  {
    name: "viewer",
    title: "مشاهده‌گر",
    description: "دسترسی فقط‌خواندنی به ماژول‌های عملیاتی",
    perms: [
      "dashboard.view", "employees.view", "org.view", "attendance.view",
      "access.view", "visitors.view", "devices.view", "payroll.view",
      "corrections.view", "holidays.view",
    ],
  },
];
