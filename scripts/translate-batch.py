#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""رانندهٔ ترجمهٔ انبوه با CLI z-ai — دستهٔ ۴۰تایی + چک‌پوینت + retry"""
import json, subprocess, os, sys, time

TODO = json.load(open("/tmp/translate_todo.json", encoding="utf-8"))
OUT = "/tmp/en_translations.json"
done = json.load(open(OUT, encoding="utf-8")) if os.path.exists(OUT) else {}

GLOSSARY = """گیت=Gate؛ منطقه=Zone؛ کارت=Card؛ پرسنل=Employee؛ واحد سازمانی=Department؛ پست=Position؛
تردد=Attendance؛ شیفت=Shift؛ نوبت‌کاری=Rotation shift؛ اقماری=Cycle shift؛ مرخصی=Leave؛ مأموریت=Mission؛
اضافه‌کاری=Overtime؛ حقوق و دستمزد=Payroll؛ فیش حقوقی=Payslip؛ لیست حقوق=Payroll run؛ اقلام=Pay items؛
بیمه=Insurance؛ مالیات=Tax؛ ریال=Rials؛ راهبند=Barrier؛ پلاک‌خوان=Plate reader (ANPR)؛ دوربین=Camera؛
بازدیدکننده=Visitor؛ پیمانکار=Contractor؛ هشدار=Alert؛ حسابرسی=Audit log؛ نقش=Role؛
مجوز (دسترسی)=Permission؛ مجوز نصب=License؛ درب=Door؛ کنترل تردد=Access control؛ حضور و غیاب=Time & Attendance؛
دستگاه=Device؛ قالب هویتی=Biometric template؛ اثر انگشت=Fingerprint؛ چهره=Face؛ آنت‌ی‌پس‌بک=Anti-passback؛
شمارش اضطراری=Emergency muster؛ تعطیلی=Holiday؛ گیت‌وی=Gateway؛ ضربان=Ping؛ دستور=Command؛
انبار=Stock؛ ابطال=Revoke؛ مفقودی=Lost؛ در انتظار=Pending؛ تأیید=Approve؛ رد=Reject؛ نهایی‌سازی=Finalize؛
چارت سازمانی=Org chart؛ برند=Brand؛ سریال=Serial؛ گزارش=Report؛ تنظیمات=Settings؛ ظاهر=Appearance؛
پالت=Palette؛ تقویم=Calendar؛ شمسی=Jalali؛ قمری=Hijri lunar؛ میلادی=Gregorian."""

RULES = """Rules:
- Return ONLY a valid JSON object {"persian": "english", ...} with every input key exactly once.
- Keep placeholders {0} {1} {2} exactly.
- Keep emojis/symbols (✓ ✗ 🔓 ⬅ ➡ · — 🔴 🟢 ✔ ⏳ 💾 📄 📊 ⛔ 📹 🅿 etc.).
- Keep technical tokens untranslated: ADMS, TCP, PUSH, SQL Server, SQLite, SMTP, API, API Key, IP, PDF, PNG, JPG, WEBP, SVG, RTSP, HTTP, ACK, poll, poller, CommKey, QR, RFID, localhost, SN, JWT, Bearer, PNG.
- Brand names unchanged (Hikvision, Dahua, ZKTeco, UNV, Edimax, Suprema, Anviz, Virdi, Timmy).
- Keep HTML fragments inside strings unchanged.
- Concise professional en-US software UI wording."""

def build_prompt(batch):
    return ("Translate each Persian UI string of an HR/attendance/access-control web app to concise professional English.\n\n"
            + RULES + "\n\nGlossary:\n" + GLOSSARY
            + "\n\nStrings (JSON array):\n" + json.dumps(batch, ensure_ascii=False))

def extract_json(text):
    t = text.replace("```json", "").replace("```", "").strip()
    s, e = t.find("{"), t.rfind("}")
    if s < 0 or e < 0:
        raise ValueError("no json")
    return json.loads(t[s:e + 1])

def extract_content(txt):
    """خروجی CLI = سرخط‌های emoji + پاکت کامل API → choices[0].message.content"""
    s, e = txt.find("{"), txt.rfind("}")
    if s < 0 or e < 0:
        raise ValueError("no envelope braces")
    env = json.loads(txt[s:e + 1])
    content = env["choices"][0]["message"]["content"]
    return content.replace("```json", "").replace("```", "").strip()

def main():
    todo = [k for k in TODO if k not in done]
    print(f"todo={len(todo)} already={len(done)}", flush=True)
    SIZE = 40
    fails = []
    for i in range(0, len(todo), SIZE):
        batch = todo[i:i + SIZE]
        ok = False
        for attempt in range(1, 4):
            try:
                p = subprocess.run(
                    ["z-ai", "chat", "-p", build_prompt(batch),
                     "-s", "You are a professional Persian-to-English UI translator. Output only valid JSON."],
                    capture_output=True, text=True, timeout=240)
                inner = extract_content(p.stdout)
                s, e = inner.find("{"), inner.rfind("}")
                if s < 0 or e < 0:
                    raise ValueError("no braces in content: " + inner[:120])
                map_ = json.loads(inner[s:e + 1])
                got = 0
                for k in batch:
                    v = map_.get(k)
                    if isinstance(v, str) and v.strip():
                        done[k] = v.strip()
                        got += 1
                if got == 0:
                    raise ValueError("empty mapping")
                json.dump(done, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
                print(f"[{min(i+SIZE,len(todo))}/{len(todo)}] got {got}", flush=True)
                ok = True
                break
            except Exception as e:
                print(f"batch {i} try {attempt} error: {str(e)[:140]}", flush=True)
                time.sleep(2 * attempt)
        if not ok:
            fails.extend(batch)
    json.dump(done, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    miss = [k for k in TODO if k not in done]
    print(f"DONE translated={len(done)} failed={len(miss)}")
    if miss:
        print(json.dumps(miss[:40], ensure_ascii=False, indent=1))

if __name__ == "__main__":
    main()
