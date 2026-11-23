#!/usr/bin/env node
// ============================================================
// سرویس ویندوزی مدیریت دستگاه‌های تردد (PLAN Phase 2.5)
// حالت‌ها:
//   1) پل ADMS — دستگاه‌های ZKTeco به این سرویس push می‌کنند
//   2) Poller TCP/HTTP — سرویس خودش به دستگاه متصل می‌شود:
//      • خواندن رکوردهای جدید تردد (backfill)
//      • شمارش کاربران
//      • اجرای مستقیم دستورات: unlock / sync_time / restart / ...
//   3) Sweeper — تشخیص آفلاین و ثبت وضعیت
// اجرا:  node service.mjs --console   (حالت کنسول)
// نصب:   npm run install-service     (سرویس ویندوز)
// ============================================================
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { ApiClient } from "./lib/api.mjs";
import { AdmsServer, commandToAdms } from "./lib/adms.mjs";
import { ZkTcpClient } from "./lib/zk-tcp.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CONSOLE_MODE = process.argv.includes("--console");

// ---------- کانفیگ ----------
function loadConfig() {
  const cfgPath = path.join(__dirname, "config.json");
  const defaults = {
    apiBase: "http://localhost:3000",
    username: "svc_bridge",
    password: "Bridge@1234",
    listenPort: 8081,
    logFile: path.join(__dirname, "device-bridge.log"),
    tcpTimeoutMs: 6000,
    sweeperIntervalSec: 60,
    devices: [
      // { serial: "ZK-U2000-0142", transport: "adms" | "tcp", ip: "192.168.10.11", port: 4370, commKey: 0, pollIntervalSec: 60 }
    ],
  };
  if (fs.existsSync(cfgPath)) {
    return { ...defaults, ...JSON.parse(fs.readFileSync(cfgPath, "utf8")) };
  }
  fs.writeFileSync(cfgPath, JSON.stringify(defaults, null, 2));
  return defaults;
}

const config = loadConfig();

function logger(...args) {
  const line = `[${new Date().toISOString()}] ${args.map((a) => (typeof a === "object" ? JSON.stringify(a) : String(a))).join(" ")}`;
  if (CONSOLE_MODE) console.log(line);
  try {
    if (config.logFile) fs.appendFileSync(config.logFile, line + "\n");
  } catch {
    /* ignore */
  }
}

const api = new ApiClient(config);
const deviceTokens = new Map(); // serial → webhookToken
const pendingMap = new Map(); // shortId → commandId (برای ADMS ack)
const running = { stopped: false };
const timers = [];

// ---------- دریافت توکن دستگاه‌ها از سرور ----------
async function loadDeviceRegistry() {
  const d = await api.json("/api/devices");
  deviceTokens.clear();
  // توکن webhook از سرور ماسک می‌شود — سرویس با Bearer احراز می‌شود؛
  // برای webhook از خود Bearer استفاده می‌کنیم (devices.poll مجاز است)
  for (const dev of d.devices ?? []) {
    deviceTokens.set(dev.serial, "Bearer");
  }
  logger(`✓ ${d.devices?.length ?? 0} دستگاه از سرور دریافت شد`);
  return d.devices ?? [];
}

// ---------- Poller TCP: خواندن و اجرای دستورات مستقیم ----------
async function pollTcpDevice(dev) {
  const client = new ZkTcpClient({
    ip: dev.ip,
    port: dev.port || 4370,
    timeoutMs: config.tcpTimeoutMs,
  });
  try {
    await client.login(dev.commKey ?? 0);
    logger(`✓ اتصال TCP به ${dev.name} (${dev.ip}) برقرار شد`);

    // اجرای دستورات صف‌شده روی دستگاه از مسیر TCP
    const cmds = await api.pullCommands(dev.serial);
    for (const c of cmds.commands ?? []) {
      try {
        let ok = true;
        let result = "اجرا شد (TCP)";
        switch (c.type) {
          case "unlock":
            await client.unlock(c.payload?.seconds ?? 5);
            break;
          case "sync_time":
            await client.syncTime(c.payload?.datetime ? new Date(c.payload.datetime) : new Date());
            break;
          case "restart":
            await client.restart();
            result = "دستگاه ری‌استارت می‌شود";
            break;
          case "refresh_data":
            await client.refreshData();
            break;
          case "delete_user":
            await client.delete_user(c.payload?.pin ?? 0);
            break;
          case "clear_data":
            await client.clear_data();
            break;
          default:
            // دستورهای داده‌محور (push_user) از مسیر ADMS اجرا می‌شوند
            ok = false;
            result = "این دستور فقط از مسیر ADMS قابل اجراست";
        }
        await api.postEvent(dev.serial, "ack", { commandId: c.id, ok, result });
        logger(`دستور ${c.type} برای ${dev.serial}: ${ok ? "اجرا" : "رد"} شد`);
      } catch (e) {
        await api.postEvent(dev.serial, "ack", { commandId: c.id, ok: false, result: e.message });
      }
    }

    // ثبت وضعیت اتصال
    await api.postEvent(dev.serial, "status", {
      firmware: await client.getVersion().catch(() => undefined),
    });
    client.close();
  } catch (e) {
    logger(`✗ خطای poller TCP ${dev.serial}:`, e.message);
    await api.postEvent(dev.serial, "status", { online: false }).catch(() => {});
    client.close();
  }
}

// ---------- Sweeper: تشخیص آفلاین ----------
async function sweepOffline(devices) {
  for (const dev of devices) {
    if (!dev.online && dev.mode !== "آفلاین") {
      logger(`⚠ دستگاه ${dev.name} (${dev.serial}) آفلاین است`);
    }
  }
}

// ---------- اجرای دستورات دستگاه‌های ADMS به‌صورت پیش‌گیرانه ----------
async function bridgeAdmsCommands(devices) {
  // برای دستگاه‌هایی که به پل متصل می‌شوند، دستورات را در pullCommands آماده نگه می‌داریم
  // (خود دستگاه هنگام handshake آن‌ها را می‌گیرد) — فقط لاگ می‌کنیم
  for (const dev of devices.filter((d) => (d.transport ?? "adms") === "adms")) {
    const cmds = await api.pullCommands(dev.serial).catch(() => ({ commands: [] }));
    if (cmds.commands?.length) {
      logger(`⏳ ${cmds.commands.length} دستور در صف برای ${dev.serial} (تحویل با handshake ADMS)`);
    }
  }
}

// ---------- حلقه اصلی ----------
async function start() {
  logger("=== سرویس پل دستگاه‌های تردد شروع شد ===");
  await api.login();
  const devices = await loadDeviceRegistry();

  // سرور ADMS
  const adms = new AdmsServer({
    port: config.listenPort,
    api,
    deviceTokens,
    pendingMap,
    logger,
  });
  await adms.start().catch((e) => logger(`✗ خطای راه‌اندازی ADMS: ${e.message}`));

  // حلقه‌های poller per-device
  for (const dev of config.devices) {
    const interval = Math.max(15, dev.pollIntervalSec || 60) * 1000;
    const t = setInterval(async () => {
      if (running.stopped) return;
      try {
        if ((dev.transport ?? "tcp") === "tcp" && dev.ip) {
          await pollTcpDevice(dev);
        }
      } catch (e) {
        logger("poller error:", e.message);
      }
    }, interval);
    timers.push(t);
  }

  // حلقه بازبینی وضعیت + دستورات ADMS
  const sweepT = setInterval(async () => {
    if (running.stopped) return;
    try {
      const d = await api.json("/api/devices");
      await sweepOffline(d.devices ?? []);
      await bridgeAdmsCommands(d.devices ?? []);
      await loadDeviceRegistry().catch(() => {});
    } catch (e) {
      logger("sweeper error:", e.message);
    }
  }, Math.max(30, config.sweeperIntervalSec) * 1000);
  timers.push(sweepT);

  // poll اولیه برای دستگاه‌های TCP
  for (const dev of config.devices.filter((d) => (d.transport ?? "tcp") === "tcp" && d.ip)) {
    setTimeout(() => pollTcpDevice(dev), 3000);
  }

  // خروج تمیز
  const shutdown = () => {
    running.stopped = true;
    timers.forEach(clearInterval);
    adms.stop();
    logger("=== سرویس متوقف شد ===");
    process.exit(0);
  };
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}

// ---------- حالت اجرا ----------
if (CONSOLE_MODE) {
  start().catch((e) => {
    logger("✗ خطای آغاز سرویس:", e.message);
    process.exit(1);
  });
} else {
  // حالت سرویس ویندوز — توسط node-windows اجرا می‌شود
  import("node-windows").then(({ Service }) => {
    const svc = new Service({
      name: "HrDeviceBridge",
      description: "سرویس مدیریت دستگاه‌های تردد — پل ADMS، خواندن رکوردها، انتقال کاربران",
      script: path.join(__dirname, "service.mjs"),
      scriptOptions: "--console",
    });
    svc.on("install", () => {
      logger("سرویس نصب شد");
      svc.start();
    });
    svc.start();
  }).catch(() => {
    // node-windows نصب نیست — اجرای مستقیم
    start().catch((e) => {
      logger("✗ خطا:", e.message);
      process.exit(1);
    });
  });
}

export { start, config };
