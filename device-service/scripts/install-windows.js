// نصب سرویس ویندوزی (node-windows) — روی ویندوز اجرا کنید: npm run install-service
const path = require("path");
const { Service } = require("node-windows");

const svc = new Service({
  name: "HrDeviceBridge",
  displayName: "HR Device Bridge Service",
  description:
    "سرویس ویندوزی مدیریت دستگاه‌های تردد — پل ADMS، خواندن رکوردهای حضور، انتقال کاربران بین دستگاه‌ها و اجرای دستورات کنترلی",
  script: path.join(__dirname, "..", "service.mjs"),
  scriptOptions: "--console",
  nodeOptions: ["--harmony", "--max_old_space_size=4096"],
});

svc.on("install", () => {
  console.log("✓ سرویس با موفقیت نصب شد — در حال راه‌اندازی...");
  svc.start();
  console.log("✓ سرویس آغاز شد. وضعیت: services.msc → HrDeviceBridge");
});

svc.on("alreadyinstalled", () => console.log("سرویس قبلاً نصب شده است"));
svc.on("error", (e) => console.error("خطای نصب:", e));

svc.install();
