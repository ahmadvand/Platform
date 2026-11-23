// ============================================================
// کلاینت HTTP برای سامانه جامع (احراز هویت + توکن خودکار)
// ============================================================
import fs from "node:fs";

export class ApiClient {
  constructor(config) {
    this.base = (config.apiBase || "http://localhost:3000").replace(/\/+$/, "");
    this.username = config.username || "svc_bridge";
    this.password = config.password || "";
    this.accessToken = null;
    this.logFile = config.logFile || null;
  }

  log(...args) {
    const line = `[${new Date().toISOString()}] ${args.map((a) => (typeof a === "object" ? JSON.stringify(a) : a)).join(" ")}`;
    console.log(line);
    if (this.logFile) {
      try {
        fs.appendFileSync(this.logFile, line + "\n");
      } catch {
        /* ignore */
      }
    }
  }

  async login() {
    const res = await fetch(`${this.base}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: this.username, password: this.password }),
    });
    if (!res.ok) {
      const j = await res.json().catch(() => ({}));
      throw new Error(`ورود سرویس ناموفق: ${res.status} ${j.error ?? ""}`);
    }
    // توکن‌ها در کوکی Set-Cookie هستند — استخراج دستی
    const cookies = res.headers.getSetCookie?.() ?? [];
    for (const c of cookies) {
      const m = c.match(/hr_at=([^;]+)/);
      if (m) this.accessToken = m[1];
    }
    if (!this.accessToken) throw new Error("توکن دسترسی دریافت نشد");
    this.log("✓ ورود سرویس پل موفق");
  }

  async request(path, { method = "GET", body, retry = true } = {}) {
    if (!this.accessToken) await this.login();
    const res = await fetch(`${this.base}${path}`, {
      method,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${this.accessToken}`,
      },
      body: body ? JSON.stringify(body) : undefined,
    });
    if (res.status === 401 && retry) {
      await this.login();
      return this.request(path, { method, body, retry: false });
    }
    return res;
  }

  async json(path, opts) {
    const res = await this.request(path, opts);
    if (!res.ok) {
      const j = await res.json().catch(() => ({}));
      throw new Error(`${path} → ${res.status} ${j.error ?? ""}`);
    }
    return res.json();
  }

  /** GET handshake — دریافت دستورات صف‌شده برای دستگاه (با Bearer سرویس) */
  async pullCommands(serial) {
    if (!this.accessToken) await this.login();
    const res = await fetch(`${this.base}/api/devices/webhook?SN=${encodeURIComponent(serial)}`, {
      headers: { Authorization: `Bearer ${this.accessToken}` },
    });
    if (!res.ok) return { commands: [] };
    return res.json();
  }

  /** POST رویداد دستگاه به سرور مرکزی (با Bearer سرویس) */
  async postEvent(serial, event, data) {
    if (!this.accessToken) await this.login();
    const res = await fetch(`${this.base}/api/devices/webhook`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${this.accessToken}` },
      body: JSON.stringify({ SN: serial, event, data }),
    });
    return res.ok ? res.json() : { ok: false, error: res.status };
  }
}
