// حذف سرویس ویندوزی — npm run uninstall-service
const path = require("path");
const { Service } = require("node-windows");

const svc = new Service({
  name: "HrDeviceBridge",
  script: path.join(__dirname, "..", "service.mjs"),
});

svc.on("uninstall", () => console.log("✓ سرویس حذف شد"));
svc.uninstall();
