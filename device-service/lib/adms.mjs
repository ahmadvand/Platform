// ============================================================
// سرور پل ADMS/PUSH (پروتکل استاندارد PUSH دستگاه‌های ZKTeco)
// دستگاه‌ها به این سرور متصل می‌شوند: /iclock/cdata?SN=...
// پل: دستگاه ⇄ این سرویس ⇄ سامانه جامع
// ============================================================
import http from "node:http";
import { URL } from "node:url";

/**
 * قالب دستورهای متنی (بسته به فریمور — پیش‌فرض‌های رایج):
 *  بازکردن درب:  C:{id}:CONTROL DEVICE 01
 *  ری‌استارت:    C:{id}:RESTART
 *  تنظیم ساعت:   C:{id}:SET OPTIONS DateTime=YYYY-MM-DD HH:MM:SS
 *  پاک‌سازی:     C:{id}:CLEAR DATA
 *  ارسال کاربر:  C:{id}:DATA UPDATE USERINFO PIN={pin}\tName={name}\tPrivilege=0\tCard={card}
 *  حذف کاربر:    C:{id}:DATA DELETE USERINFO PIN={pin}
 *  به‌روزرسانی:  C:{id}:REFRESH
 */
export function commandToAdms(cmd) {
  const id = cmd.id.slice(-8); // شناسه کوتاه برای ACK
  switch (cmd.type) {
    case "unlock":
      return `C:${id}:CONTROL DEVICE 01`;
    case "restart":
      return `C:${id}:RESTART`;
    case "sync_time": {
      const dt = cmd.payload?.datetime ? new Date(cmd.payload.datetime) : new Date();
      const p2 = (n) => String(n).padStart(2, "0");
      const s = `${dt.getFullYear()}-${p2(dt.getMonth() + 1)}-${p2(dt.getDate())} ${p2(dt.getHours())}:${p2(dt.getMinutes())}:${p2(dt.getSeconds())}`;
      return `C:${id}:SET OPTIONS DateTime=${s}`;
    }
    case "clear_data":
      return `C:${id}:CLEAR DATA`;
    case "refresh_data":
      return `C:${id}:REFRESH`;
    case "push_user": {
      const u = cmd.payload ?? {};
      return `C:${id}:DATA UPDATE USERINFO PIN=${u.pin}\tName=${encodeURIComponent(u.name ?? "")}\tPrivilege=0\tVerify=1`;
    }
    case "delete_user":
      return `C:${id}:DATA DELETE USERINFO PIN=${cmd.payload?.pin}`;
    default:
      return null; // دستورهای ناشناخته از مسیر TCP/HTTP اختصاصی اجرا می‌شوند
  }
}

/** تجزیه خطوط ATTLOG دستگاه: pin\ttimestamp\tstatus\tverify */
export function parseAttlog(body) {
  const punches = [];
  for (const line of body.split("\n")) {
    const t = line.trim();
    if (!t || !/^\d+\t/.test(t)) continue;
    const [pin, ts, status] = t.split("\t");
    punches.push({ pin, timestamp: ts, status });
  }
  return punches;
}

/** تجزیه ACK خط فرمان: C:{id}:\t{result} یا C:{id}:OK */
export function parseAck(body) {
  const acks = [];
  for (const line of body.split("\n")) {
    const m = line.trim().match(/^C:([A-Za-z0-9]+):\s*(.*)$/);
    if (m) acks.push({ shortId: m[1], result: m[2] || "OK" });
  }
  return acks;
}

export class AdmsServer {
  constructor({ port, api, deviceTokens, pendingMap, logger }) {
    // deviceTokens: Map<serial, webhookToken> — pendingMap: Map<shortId, commandId>
    this.port = port;
    this.api = api;
    this.deviceTokens = deviceTokens;
    this.pendingMap = pendingMap;
    this.log = logger;
    this.server = http.createServer((req, res) => this.handle(req, res));
  }

  start() {
    return new Promise((resolve, reject) => {
      this.server.once("error", reject);
      this.server.listen(this.port, () => {
        this.log(`✓ سرور پل ADMS روی پورت ${this.port} آماده است — آدرس اتصال دستگاه‌ها: http://SERVER_IP:${this.port}/iclock/cdata`);
        resolve();
      });
    });
  }

  stop() {
    this.server?.close();
  }

  async handle(req, res) {
    const url = new URL(req.url, "http://localhost");
    if (!url.pathname.startsWith("/iclock/cdata")) {
      res.writeHead(404);
      return res.end("OK");
    }
    const sn = url.searchParams.get("SN");
    if (!sn) {
      res.writeHead(400);
      return res.end("BAD REQUEST");
    }

    try {
      if (req.method === "GET") {
        // handshake + دریافت دستورات
        const out = [];
        const cmds = await this.api.pullCommands(sn);
        for (const c of cmds.commands ?? []) {
          const text = commandToAdms(c);
          if (text) {
            out.push(text);
            this.pendingMap.set(text.slice(2, 10), c.id);
          }
        }
        res.writeHead(200, { "Content-Type": "text/plain; charset=utf-8" });
        return res.end(out.length ? out.join("\n") + "\n" : "OK");
      }

      // POST — داده‌های دستگاه
      let body = "";
      req.on("data", (chunk) => (body += chunk.toString()));
      req.on("end", async () => {
        try {
          const acks = parseAck(body);
          for (const a of acks) {
            const commandId = this.pendingMap.get(a.shortId);
            if (commandId) {
              await this.api.postEvent(sn, "ack", {
                commandId,
                ok: !/error|fail/i.test(a.result),
                result: a.result,
              });
              this.pendingMap.delete(a.shortId);
            }
          }
          const punches = parseAttlog(body);
          if (punches.length) {
            const r = await this.api.postEvent(sn, "records", { punches });
            this.log(`دستگاه ${sn}: ${punches.length} رکورد تردد ارسال شد (${r.ok ?? 0} ثبت)`);
          }
          res.writeHead(200, { "Content-Type": "text/plain" });
          res.end("OK");
        } catch (e) {
          this.log("خطای پردازش داده دستگاه:", e.message);
          res.writeHead(200);
          res.end("OK");
        }
      });
    } catch (e) {
      this.log("خطای ADMS:", e.message);
      res.writeHead(200);
      res.end("OK");
    }
  }
}
