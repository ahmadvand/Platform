// ترجمهٔ انبوه کلیدهای فارسی به انگلیسی با z-ai-web-dev-sdk — دسته‌ای + چک‌پوینت
import ZAI from 'z-ai-web-dev-sdk';
import fs from 'fs';

const TODO = JSON.parse(fs.readFileSync('/tmp/translate_todo.json', 'utf-8'));
const OUT = '/tmp/en_translations.json';
let done = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf-8')) : {};

const GLOSSARY = `
گیت=Gate؛ منطقه=Zone؛ کارت=Card؛ پرسنل=Employee؛ واحد سازمانی=Department؛ پست=Position/Job position؛
تردد=Attendance؛ شیفت=Shift؛ نوبت‌کاری=Rotation shift؛ اقماری=Cycle shift؛ مرخصی=Leave؛ مأموریت=Mission؛
اضافه‌کاری=Overtime؛ حقوق و دستمزد=Payroll؛ فیش حقوقی=Payslip؛ لیست حقوق=Payroll run؛ اقلام=Pay items؛
بیمه=Insurance؛ مالیات=Tax؛ ریال=Rials؛ راهبند=Barrier؛ پلاک‌خوان=Plate reader (ANPR)؛ دوربین=Camera؛
بازدیدکننده/ارباب رجوع=Visitor؛ پیمانکار=Contractor؛ هشدار=Alert؛ حسابرسی=Audit log؛ نقش=Role؛
مجوز (دسترسی)=Permission؛ مجوز نصب=License؛ درب=Door؛ کنترل تردد=Access control؛ حضور و غیاب=Time & Attendance؛
دستگاه=Device؛ قالب هویتی=Biometric template؛ اثر انگشت=Fingerprint؛ چهره=Face؛ آنت‌ی‌پس‌بک=Anti-passback؛
شمارش اضطراری=Emergency muster؛ تعطیلی=Holiday؛ گیت‌وی=Gateway؛ ضربان/Ping=Ping؛ دستور=Command؛
انبار=Stock/Pool؛ ابطال=Revoke؛ مفقودی=Lost؛ در انتظار=Pending؛ تأیید=Approve؛ رد=Reject؛ نهایی‌سازی=Finalize؛
چارت سازمانی=Org chart؛ برند=Brand؛ سریال=Serial؛ نمودار=Chart؛ گزارش=Report؛ تنظیمات=Settings؛ ظاهر=Appearance؛
پالت=Palette؛ بومی‌سازی/زبان=Language؛ تقویم=Calendar؛ شمسی=Jalali (Solar Hijri)؛ قمری=Hijri (Lunar)؛ میلادی=Gregorian.`;

function buildPrompt(batch) {
  return `Translate each Persian UI string of an HR/attendance/access-control web app to concise professional English (en-US) suitable for a software interface.

Rules:
- Return ONLY a valid JSON object: {"persian string": "english translation", ...} — every input key must appear exactly once.
- Keep placeholders {0} {1} {2} exactly as-is in the translation.
- Keep emojis/symbols (✓ ✗ 🔓 ⬅ ➡ · — 🔴 🟢 ✔ ⏳ 💾 📄 📊 etc.) at the appropriate position.
- Keep technical tokens untranslated: ADMS, TCP, PUSH, ADMS/PUSH, SQL Server, SQLite, SMTP, API, API Key, IP, PDF, PNG, JPG, WEBP, SVG, RTSP, HTTP, ACK, poll, poller, CommKey, QR, RFID, Hikvision, Dahua, ZKTeco, UNV, Edimax, Suprema, Anviz, Virdi, Timmy, localhost, SN, Access-Control-Allow, CORS, JWT, Bearer.
- Keep HTML fragments/tags inside strings unchanged.
- Brand/company names stay as-is. Do NOT translate numbers written in Persian digits (۱۴۰۴ etc.) — keep them or use Western digits naturally.
- Use the glossary for consistent terminology.
${GLOSSARY}

Strings to translate:
${JSON.stringify(batch, null, 0)}`;
}

function extractJson(text) {
  const t = text.replace(/```json|```/g, '').trim();
  const s = t.indexOf('{');
  const e = t.lastIndexOf('}');
  if (s < 0 || e < 0) throw new Error('no json found');
  return JSON.parse(t.slice(s, e + 1));
}

async function translateBatch(zai, batch, tries = 3) {
  for (let a = 1; a <= tries; a++) {
    try {
      const completion = await zai.chat.completions.create({
        messages: [
          { role: 'assistant', content: 'You are a professional fa→en UI translator. Output only valid JSON.' },
          { role: 'user', content: buildPrompt(batch) },
        ],
        thinking: { type: 'disabled' },
      });
      const raw = completion.choices[0]?.message?.content ?? '';
      const map = extractJson(raw);
      const out = {};
      for (const k of batch) if (typeof map[k] === 'string' && map[k].trim()) out[k] = map[k].trim();
      if (Object.keys(out).length === 0) throw new Error('empty mapping');
      return out;
    } catch (e) {
      console.error(`batch error (try ${a}):`, e.message);
      if (a === tries) return {};
      await new Promise(r => setTimeout(r, 1500 * a));
    }
  }
}

async function main() {
  const zai = await ZAI.create();
  const todo = TODO.filter(k => !(k in done));
  console.log(`todo=${todo.length} already=${Object.keys(done).length}`);
  const SIZE = 40;
  for (let i = 0; i < todo.length; i += SIZE) {
    const batch = todo.slice(i, i + SIZE);
    const map = await translateBatch(zai, batch);
    Object.assign(done, map);
    fs.writeFileSync(OUT, JSON.stringify(done, null, 1), 'utf-8');
    console.log(`progress ${Math.min(i + SIZE, todo.length)}/${todo.length} (got ${Object.keys(map).length})`);
  }
  const missingBack = TODO.filter(k => !(k in done));
  console.log(`DONE. translated=${Object.keys(done).length} failed=${missingBack.length}`);
  if (missingBack.length) console.log('FAILED KEYS:', JSON.stringify(missingBack.slice(0, 30), null, 1));
}
main().catch(e => { console.error(e); process.exit(1); });
