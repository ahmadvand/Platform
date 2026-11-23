#!/usr/bin/env node
// ============================================================
// شبیه‌ساز دستگاه تردد ZKTeco — پروتکل TCP باینری استاندارد
// (مطابق pyzk/zkemkeeper: [50 50 82 7d][len][cmd][chk][sid][rid][data])
// برای تست E2E گیت‌وی C# — HrDeviceGateway
// اجرا: node fake-tcp-device.mjs [port] [serial]
// ============================================================
import net from "node:net";

const PORT = Number(process.argv[2] || 4370);
const SERIAL = process.argv[3] || "ZK-U2000-0142";

// ---------- ثابت‌های پروتکل (استاندارد pyzk) ----------
const CMD = {
  CONNECT: 1000, EXIT: 1001, RESTART: 1004, REFRESHDATA: 1013, GET_VERSION: 1100,
  AUTH: 1102, PREPARE_DATA: 1500, DATA: 1501, FREE_DATA: 1502, PREPARE_BUFFER: 1503,
  READ_BUFFER: 1504, USER_WRQ: 8, USERTEMP_RRQ: 9, USERTEMP_WRQ: 10, DELETE_USERTEMP: 19,
  OPTIONS_RRQ: 11, ATTLOG: 13, CLEAR_DATA: 14,
  CLEAR_ATTLOG: 15, DELETE_USER: 18, UNLOCK: 31, GET_FREE_SIZES: 50,
  GET_TIME: 201, SET_TIME: 202,
  ACK_OK: 2000, ACK_ERROR: 2001, ACK_DATA: 2002, ACK_UNAUTH: 2005,
};

// ---------- وضعیت دستگاه ----------
// قالب‌های هویتی: کلید "pin:type:index" → { valid, dup, tpl: Buffer }
function mkTemplate(pin, type, index, seed) {
  // قالب نمایشی ۳۹۷ بایتی با هدر متعارف ZK (sZDB)
  const tpl = Buffer.alloc(397);
  tpl.write("sZDB", 0, "ascii");
  tpl.writeUInt32LE(2, 4);
  for (let i = 8; i < tpl.length; i++) tpl[i] = (seed * (i + 1) * 31 + i * 7) & 0xff;
  return { valid: 1, dup: 0, tpl };
}

const state = {
  sessionId: Math.floor(Math.random() * 65535) + 1,
  commKey: 0,
  deviceTime: new Date(),
  version: "VER8.60 ALS10.0 OK V3.1.41",
  users: [
    // قالب 72 بایتی: uid(H) priv(B) pwd(8s) name(24s) card(I) pad grp(7s) pad userId(24s)
    mkUser(1, 0, "امیر محمدی", "1012", 86420145),
    mkUser(2, 0, "سارا کریمی", "1013", 0),
    mkUser(3, 0, "رضا احمدی", "1014", 99001234),
    // پرسنلی که فقط روی دستگاه ثبت شده و در سامانه نیست — سناریوی «دریافت پرسنل از دستگاه»
    mkUser(4, 0, "ناصر رضایی", "9901", 77700123),
  ],
  templates: {
    // اثر انگشت و چهره — مطابق قرارداد USERTEMP گیت‌وی C#
    "1012:fp:0": mkTemplate("1012", "fp", 0, 11),
    "1012:fp:1": mkTemplate("1012", "fp", 1, 12),
    "1012:face:0": mkTemplate("1012", "face", 0, 13),
    "1013:fp:0": mkTemplate("1013", "fp", 0, 21),
    "1014:fp:0": mkTemplate("1014", "fp", 0, 31),
  },
  attlog: [
    // قالب 16 بایتی: userId(I) ts(4) status(B) punch(B) res(2s) workcode(I)
    mkRecord(1012, new Date(Date.now() - 3600 * 1000), 0, 0),
  ],
};

function mkUser(uid, priv, name, userId, card = 0) {
  const b = Buffer.alloc(72);
  b.writeUInt16LE(uid, 0);
  b.writeUInt8(priv, 2);
  Buffer.from(name, "utf8").copy(b, 11, 0, 24);
  b.writeUInt32LE(card >>> 0, 35); // شماره کارت
  Buffer.from("0", "ascii").copy(b, 40, 0, 7);
  Buffer.from(userId, "ascii").copy(b, 48, 0, 24);
  return b;
}

function mkRecord(userId, dt, status, punch) {
  const b = Buffer.alloc(16);
  b.writeUInt32LE(Number(userId), 0);
  b.writeUInt32LE(encodeTime(dt), 4);
  b.writeUInt8(status, 8);
  b.writeUInt8(punch, 9);
  b.writeUInt32LE(0, 12); // workcode
  return b;
}

// ---------- کدگذاری زمان (فرمول zkemsdk.c) ----------
function encodeTime(dt) {
  return ((dt.getFullYear() % 100) * 12 * 31 + (dt.getMonth()) * 31 + dt.getDate() - 1) * 86400
    + (dt.getHours() * 60 + dt.getMinutes()) * 60 + dt.getSeconds();
}
function decodeTime(v) {
  let t = v;
  const sec = t % 60; t = Math.floor(t / 60);
  const min = t % 60; t = Math.floor(t / 60);
  const hr = t % 24; t = Math.floor(t / 24);
  const day = (t % 31) + 1; t = Math.floor(t / 31);
  const month = (t % 12) + 1; t = Math.floor(t / 12);
  const year = t + 2000;
  return new Date(year, month - 1, day, hr, min, sec);
}

// ---------- چک‌سام (مدول 65535 — مطابق zkemsdk.c) ----------
function checksum(buf) {
  let c = 0;
  for (let i = 0; i + 1 < buf.length; i += 2) {
    c += buf[i] | (buf[i + 1] << 8);
    if (c > 65535) c -= 65535;
  }
  if (buf.length % 2) {
    c += buf[buf.length - 1];
    if (c > 65535) c -= 65535;
  }
  let r = ~c;
  while (r < 0) r += 65535;
  return r & 0xffff;
}

function reply(cmd, data, sid, rid) {
  const inner = Buffer.alloc(8 + (data?.length ?? 0));
  inner.writeUInt16LE(cmd, 0);
  inner.writeUInt16LE(0, 2);
  inner.writeUInt16LE(sid & 0xffff, 4);
  inner.writeUInt16LE(rid & 0xffff, 6);
  if (data?.length) data.copy(inner, 8);
  inner.writeUInt16LE(checksum(inner), 2);

  const top = Buffer.alloc(8 + inner.length);
  top.writeUInt16LE(0x5050, 0);
  top.writeUInt16LE(0x7d82, 2);
  top.writeUInt32LE(inner.length, 4);
  inner.copy(top, 8);
  return top;
}

// ---------- بافرهای خواندنی (کاربران/رکوردها) ----------
function usersBuffer() {
  const payload = Buffer.concat(state.users);
  const head = Buffer.alloc(4);
  head.writeUInt32LE(payload.length, 0);
  return Buffer.concat([head, payload]);
}
function attlogBuffer() {
  const payload = Buffer.concat(state.attlog);
  const head = Buffer.alloc(4);
  head.writeUInt32LE(payload.length, 0);
  return Buffer.concat([head, payload]);
}

// ---------- پردازش فریم ----------
function handle(cmd, sid, rid, data, sock) {
  const log = (m) => console.log(`[device] ${m}`);
  switch (cmd) {
    case CMD.CONNECT:
      state.sessionId = Math.floor(Math.random() * 65535) + 1;
      log(`CONNECT → sid=${state.sessionId}`);
      return reply(CMD.ACK_OK, null, state.sessionId, rid);

    case CMD.AUTH:
      log("AUTH (CommKey)");
      return reply(CMD.ACK_OK, null, state.sessionId, rid);

    case CMD.GET_TIME: {
      const b = Buffer.alloc(4);
      b.writeUInt32LE(encodeTime(state.deviceTime), 0);
      return reply(CMD.ACK_OK, b, sid, rid);
    }
    case CMD.SET_TIME:
      state.deviceTime = decodeTime(data.readUInt32LE(0));
      log(`SET_TIME → ${state.deviceTime.toLocaleString("fa-IR")}`);
      return reply(CMD.ACK_OK, null, sid, rid);

    case CMD.GET_VERSION:
      return reply(CMD.ACK_OK, Buffer.from(state.version, "ascii"), sid, rid);

    case CMD.OPTIONS_RRQ: {
      const key = data.toString("ascii").replace(/\0/g, "").replace("~", "");
      const val = key === "SerialNumber" ? SERIAL : key === "DeviceName" ? "FAKE-U2000" : "1";
      log(`OPTIONS_RRQ ${key}=${val}`);
      return reply(CMD.ACK_DATA, Buffer.from(`${key}=${val}\0`, "ascii"), sid, rid);
    }

    case CMD.GET_FREE_SIZES: {
      const fpCount = Object.keys(state.templates).filter((k) => k.includes(":fp:")).length;
      const faceCount = Object.keys(state.templates).filter((k) => k.includes(":face:")).length;
      const b = Buffer.alloc(80);
      b.writeInt32LE(state.users.length, 4 * 4);   // users
      b.writeInt32LE(fpCount + faceCount, 4 * 6);  // fingers (قالب‌های هویتی)
      b.writeInt32LE(state.attlog.length, 4 * 8); // records
      b.writeInt32LE(state.users.filter((u) => u.readUInt32LE(35) !== 0).length, 4 * 12);  // cards
      b.writeInt32LE(3000, 4 * 15); // users_cap
      b.writeInt32LE(100000, 4 * 16); // rec_cap
      return reply(CMD.ACK_OK, b, sid, rid);
    }

    case CMD.PREPARE_BUFFER: {
      // data: [b1=1][u16 command][i32 fct][i32 ext]
      const sub = data.readUInt16LE(1);
      sock._serve = sub === 9 ? usersBuffer() : sub === 13 ? attlogBuffer() : Buffer.alloc(0);
      const sizeBuf = Buffer.alloc(5);
      sizeBuf.writeUInt32LE(sock._serve.length, 1); // مطابق pyzk: size در data[1:5]
      log(`PREPARE_BUFFER command=${sub} size=${sock._serve.length}`);
      return reply(CMD.PREPARE_DATA, sizeBuf, sid, rid);
    }

    case CMD.READ_BUFFER: {
      const start = data.readInt32LE(0);
      const size = data.readInt32LE(4);
      const chunk = sock._serve.subarray(start, start + size);
      log(`READ_BUFFER ${start}:[${size}] → ${chunk.length} بایت`);
      return reply(CMD.DATA, Buffer.from(chunk), sid, rid);
    }

    case CMD.FREE_DATA:
      sock._serve = null;
      return reply(CMD.ACK_OK, null, sid, rid);

    case CMD.USER_WRQ: {
      const userId = data.subarray(48, 72).toString("ascii").replace(/\0.*/s, "");
      const name = data.subarray(11, 35).toString("utf8").replace(/\0.*/s, "");
      const uid = data.readUInt16LE(0);
      const card = data.readUInt32LE(35);
      log(`USER_WRQ uid=${uid} pin=${userId} name=${name} card=${card}`);
      state.users = state.users.filter((u) => u.subarray(48, 72).toString("ascii").replace(/\0.*/s, "") !== userId);
      state.users.push(Buffer.from(data));
      return reply(CMD.ACK_OK, null, sid, rid);
    }

    case CMD.USERTEMP_RRQ: {
      // خواندن یک قالب هویتی — payload [uid:2][index:1][typeFlag:1]
      const uid = data.readUInt16LE(0);
      const index = data[2];
      const typeFlag = data[3] === 1 ? "face" : "fp";
      const u = state.users.find((x) => x.readUInt16LE(0) === uid);
      if (!u) {
        log(`USERTEMP_RRQ uid=${uid} — کاربر یافت نشد`);
        return reply(CMD.ACK_ERROR, null, sid, rid);
      }
      const pin = u.subarray(48, 72).toString("ascii").replace(/\0.*/s, "");
      const t = state.templates[`${pin}:${typeFlag}:${index}`];
      if (!t) {
        log(`USERTEMP_RRQ pin=${pin} ${typeFlag}[${index}] — قالبی ثبت نشده`);
        return reply(CMD.ACK_ERROR, null, sid, rid);
      }
      const out = Buffer.alloc(6 + t.tpl.length);
      out.writeUInt32LE(t.tpl.length, 0);
      out.writeUInt8(t.valid, 4);
      out.writeUInt8(t.dup, 5);
      t.tpl.copy(out, 6);
      log(`USERTEMP_RRQ pin=${pin} ${typeFlag}[${index}] → ${t.tpl.length} بایت`);
      return reply(CMD.ACK_DATA, out, sid, rid);
    }

    case CMD.USERTEMP_WRQ: {
      // نوشتن قالب هویتی — payload [uid:2][index:1][typeFlag:1][valid:1][dup:1][size:4][tpl]
      const uid = data.readUInt16LE(0);
      const index = data[2];
      const typeFlag = data[3] === 1 ? "face" : "fp";
      const valid = data[4];
      const size = data.readUInt32LE(6);
      const tpl = data.subarray(10, 10 + size);
      const u = state.users.find((x) => x.readUInt16LE(0) === uid);
      if (!u) {
        log(`USERTEMP_WRQ uid=${uid} — کاربر یافت نشد`);
        return reply(CMD.ACK_ERROR, null, sid, rid);
      }
      const pin = u.subarray(48, 72).toString("ascii").replace(/\0.*/s, "");
      state.templates[`${pin}:${typeFlag}:${index}`] = { valid, dup: 0, tpl: Buffer.from(tpl) };
      log(`USERTEMP_WRQ pin=${pin} ${typeFlag}[${index}] → ثبت شد (${size} بایت، valid=${valid})`);
      return reply(CMD.ACK_OK, null, sid, rid);
    }

    case CMD.DELETE_USERTEMP: {
      const uid = data.readUInt16LE(0);
      const index = data[2];
      const typeFlag = data[3] === 1 ? "face" : "fp";
      const u = state.users.find((x) => x.readUInt16LE(0) === uid);
      if (u) {
        const pin = u.subarray(48, 72).toString("ascii").replace(/\0.*/s, "");
        delete state.templates[`${pin}:${typeFlag}:${index}`];
        log(`DELETE_USERTEMP pin=${pin} ${typeFlag}[${index}]`);
      }
      return reply(CMD.ACK_OK, null, sid, rid);
    }

    case CMD.DELETE_USER: {
      const uid = data.readUInt16LE(0);
      log(`DELETE_USER uid=${uid}`);
      const removed = state.users.find((u) => u.readUInt16LE(0) === uid);
      if (removed) {
        const pin = removed.subarray(48, 72).toString("ascii").replace(/\0.*/s, "");
        Object.keys(state.templates).forEach((k) => {
          if (k.startsWith(`${pin}:`)) delete state.templates[k];
        });
      }
      state.users = state.users.filter((u) => u.readUInt16LE(0) !== uid);
      return reply(CMD.ACK_OK, null, sid, rid);
    }

    case CMD.UNLOCK: {
      const ticks = data.readUInt32LE(0);
      log(`🔓 UNLOCK → درب ${ticks / 10} ثانیه باز شد`);
      // شبیه‌سازی عبور یک نفر پس از باز شدن درب
      state.attlog.push(mkRecord(1012, new Date(), 0, 0));
      return reply(CMD.ACK_OK, null, sid, rid);
    }

    case CMD.RESTART:
      log("RESTART → دستگاه ری‌استارت می‌شود");
      sock.end(reply(CMD.ACK_OK, null, sid, rid));
      return null;

    case CMD.REFRESHDATA:
      log("REFRESHDATA");
      return reply(CMD.ACK_OK, null, sid, rid);

    case CMD.CLEAR_DATA:
      state.users = [];
      state.templates = {};
      state.attlog = [];
      log("CLEAR_DATA");
      return reply(CMD.ACK_OK, null, sid, rid);

    case CMD.CLEAR_ATTLOG:
      state.attlog = [];
      log("CLEAR_ATTLOG");
      return reply(CMD.ACK_OK, null, sid, rid);

    case CMD.EXIT:
      log("EXIT");
      sock.end(reply(CMD.ACK_OK, null, sid, rid));
      return null;

    default:
      log(`? دستور ناشناخته ${cmd}`);
      return reply(CMD.ACK_ERROR, null, sid, rid);
  }
}

// ---------- سرور TCP ----------
const server = net.createServer((sock) => {
  let acc = Buffer.alloc(0);
  console.log(`[device] اتصال از ${sock.remoteAddress}`);

  sock.on("data", (chunk) => {
    acc = Buffer.concat([acc, chunk]);
    while (acc.length >= 8) {
      if (acc[0] !== 0x50 || acc[1] !== 0x50) { acc = acc.subarray(1); continue; }
      const len = acc.readUInt32LE(4);
      if (acc.length < 8 + len) break;
      const inner = acc.subarray(8, 8 + len);
      const cmd = inner.readUInt16LE(0);
      const sid = inner.readUInt16LE(4);
      const rid = inner.readUInt16LE(6);
      const data = inner.subarray(8);
      const res = handle(cmd, sid, rid, data, sock);
      acc = acc.subarray(8 + len);
      if (res) sock.write(res);
    }
  });

  sock.on("error", () => {});
  sock.on("close", () => console.log("[device] اتصال بسته شد"));
});

server.listen(PORT, () => console.log(`[device] شبیه‌ساز ZK روی پورت ${PORT} — سریال ${SERIAL}`));
