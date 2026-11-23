// ============================================================
// آداپتور TCP مستقیم پروتکل ZKTeco (برای دستگاه‌های بدون ADMS)
// NOTE: ثابت‌های پروتکل مطابق مستندات متداول ZK — برای فریمورهای
// خاص ممکن است تنظیم جزئی لازم باشد (فیلد cmdOffset در کانفیگ).
// ============================================================
import net from "node:net";

const CMD = {
  CMD_CONNECT: 2000,
  CMD_EXIT: 2001,
  CMD_ENABLEDEVICE: 2002,
  CMD_DISABLEDEVICE: 2003,
  CMD_RESTART: 2004,
  CMD_GET_TIME: 201,
  CMD_SET_TIME: 202,
  CMD_GET_VERSION: 1100,
  CMD_REFRESHDATA: 2013,
  CMD_GET_USERS: 1520,
  CMD_DELETE_USER: 1522,
  CMD_CLEAR_DATA: 1524,
  CMD_UNLOCK: 1531,
  CMD_ACK_OK: 2000,
  CMD_ACK_ERROR: 2001,
  CMD_ACK_UNAUTH: 2005,
  CMD_ACK_RETRY: 2006,
};

const HEADER = Buffer.from([0x50, 0x50, 0x82, 0x7d]); // ZK TCP magic

function buildPacket(cmd, data = Buffer.alloc(0), sessionId = 0, replyId = 0) {
  const cmdBuf = Buffer.alloc(2);
  cmdBuf.writeUInt16LE(cmd);
  const lenBuf = Buffer.alloc(2);
  const bodyLen = 8 + data.length; // cmd(2) + checksum(2) + data
  lenBuf.writeUInt16LE(bodyLen);

  const sessionIdBuf = Buffer.alloc(2);
  sessionIdBuf.writeUInt16LE(sessionId);
  const replyBuf = Buffer.alloc(2);
  replyBuf.writeUInt16LE(replyId & 0xffff);

  const content = Buffer.concat([cmdBuf, sessionIdBuf, replyBuf, data]);

  // checksum روی محتوای داخلی
  let sum = 0;
  for (let i = 0; i < content.length; i += 2) {
    sum += content.readUInt16LE(i) || content[i];
  }
  while (sum > 0xffff) sum = (sum & 0xffff) + (sum >> 16);
  const chkBuf = Buffer.alloc(2);
  chkBuf.writeUInt16LE(~sum & 0xffff);

  const inner = Buffer.concat([content.slice(0, 4), chkBuf, content.slice(6), data]);
  const sizeBuf = Buffer.alloc(4);
  sizeBuf.writeUInt32LE(inner.length);
  return Buffer.concat([HEADER, sizeBuf, inner]);
}

function decodeTime(buf) {
  // ZK encoded time: 6 bytes
  const v =
    buf[0] + buf[1] * 256 + buf[2] * 65536 + buf[3] * 16777216 + buf[4] * 4294967296 + buf[5] * 1099511627776;
  const sec = v % 60;
  const min = Math.floor(v / 60) % 60;
  const hr = Math.floor(v / 3600) % 24;
  const day = Math.floor(v / 86400) % 32;
  const month = (Math.floor(v / 2764800) % 13) + 1;
  const year = Math.floor(v / 31536000) + 2000;
  return new Date(year, month - 1, day, hr, min, sec);
}

function encodeTime(d) {
  const v =
    d.getSeconds() + d.getMinutes() * 60 + d.getHours() * 3600 + d.getDate() * 86400 +
    (d.getMonth()) * 2764800 + (d.getFullYear() - 2000) * 31536000;
  const buf = Buffer.alloc(6);
  let x = v;
  for (let i = 0; i < 6; i++) {
    buf[i] = x % 256;
    x = Math.floor(x / 256);
  }
  return buf;
}

export class ZkTcpClient {
  constructor({ ip, port = 4370, timeoutMs = 6000 }) {
    this.ip = ip;
    this.port = port;
    this.timeoutMs = timeoutMs;
    this.sessionId = 0;
    this.replyId = 0;
  }

  connect() {
    return new Promise((resolve, reject) => {
      this.socket = net.createConnection({ host: this.ip, port: this.port });
      const timer = setTimeout(() => {
        this.socket?.destroy();
        reject(new Error("timeout اتصال TCP"));
      }, this.timeoutMs);
      this.socket.once("connect", () => {
        clearTimeout(timer);
        resolve();
      });
      this.socket.once("error", (e) => {
        clearTimeout(timer);
        reject(e);
      });
    });
  }

  close() {
    try {
      // CMD_EXIT
      this.send(CMD.CMD_EXIT);
      this.socket?.destroy();
    } catch {
      /* ignore */
    }
  }

  send(cmd, data) {
    this.replyId = (this.replyId + 1) & 0xffff;
    const pkt = buildPacket(cmd, data ?? Buffer.alloc(0), this.sessionId, this.replyId);
    this.socket.write(pkt);
  }

  recv(expectedCmd = null) {
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error("timeout دریافت پاسخ")), this.timeoutMs);
      const onData = (chunk) => {
        clearTimeout(timer);
        this.socket?.off("data", onData);
        try {
          // ساختار پاسخ: header(4) size(4) cmd(2) sid(2) rid(2) chk(2) data...
          if (chunk.length < 16) return resolve({ data: Buffer.alloc(0), cmd: 0 });
          const cmd = chunk.readUInt16LE(8);
          this.sessionId = chunk.readUInt16LE(10);
          const data = chunk.subarray(16);
          resolve({ cmd, data });
        } catch (e) {
          reject(e);
        }
      };
      this.socket.once("data", onData);
    });
  }

  async exec(cmd, data) {
    this.send(cmd, data);
    return this.recv();
  }

  /** اتصال کامل با احراز (کلید دستگاه — پیش‌فرض صفر) */
  async login(commKey = 0) {
    await this.connect();
    const r = await this.exec(CMD.CMD_CONNECT, commKey ? Buffer.from([commKey]) : Buffer.alloc(0));
    if (r.cmd === CMD.CMD_ACK_UNAUTH) throw new Error("دستگاه نیاز به کلید ارتباط (CommKey) دارد");
    return r;
  }

  async unlock(seconds = 5) {
    // unlock با دیتای مدت زمان
    const d = Buffer.alloc(2);
    d.writeUInt16LE(seconds);
    return this.exec(CMD.CMD_UNLOCK, d);
  }

  async syncTime(dt = new Date()) {
    return this.exec(CMD.CMD_SET_TIME, encodeTime(dt));
  }

  async getTime() {
    const r = await this.exec(CMD.CMD_GET_TIME);
    return decodeTime(r.data);
  }

  async restart() {
    return this.exec(CMD.CMD_RESTART);
  }

  async refreshData() {
    return this.exec(CMD.CMD_REFRESHDATA);
  }

  async getVersion() {
    const r = await this.exec(CMD.CMD_GET_VERSION);
    return r.data.toString("ascii");
  }

  async delete_user(pin) {
    const d = Buffer.alloc(4);
    d.writeUInt16LE(Number(pin), 0);
    return this.exec(CMD.CMD_DELETE_USER, d);
  }

  async clear_data() {
    return this.exec(CMD.CMD_CLEAR_DATA);
  }
}
