// شبیه‌ساز دستگاه تردد ZKTeco — اتصال به پل ADMS برای تست خط لوله کامل
// device → bridge(:8081) → سامانه مرکزی
const SERIAL = process.argv[2] || "ZK-FD7-0311";
const BRIDGE = process.argv[3] || "http://localhost:8081";

async function handshake() {
  const res = await fetch(`${BRIDGE}/iclock/cdata?SN=${SERIAL}&pushver=3.1.41&language=83`);
  const text = await res.text();
  console.log(`[device] handshake → ${text.trim().slice(0, 200)}`);
  return text;
}

async function sendAttlog() {
  const now = new Date();
  const p2 = (n) => String(n).padStart(2, "0");
  const ts = `${now.getFullYear()}-${p2(now.getMonth() + 1)}-${p2(now.getDate())} ${p2(now.getHours())}:${p2(now.getMinutes())}:${p2(now.getSeconds())}`;
  // pin=1012 (کد پرسنلی امیر محمدی) — قالب استاندارد ATTLOG: pin\ttime\tstatus\tverify
  const body = `1012\t${ts}\t0\t1`;
  const res = await fetch(`${BRIDGE}/iclock/cdata?SN=${SERIAL}`, {
    method: "POST",
    headers: { "Content-Type": "text/plain" },
    body,
  });
  console.log(`[device] ATTLOG ارسال شد: ${body} → ${await res.text()}`);
}

(async () => {
  await handshake();
  await sendAttlog();
  await handshake(); // بعد از ارسال داده، دستورات جدید را بگیر
})();
