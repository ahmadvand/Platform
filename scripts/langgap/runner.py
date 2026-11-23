#!/usr/bin/env python3
# فاز ۱۱۳ (مرحلهٔ دوم) — رانر ترجمهٔ انبوه بسته‌های پایه با z-ai chat
# هر دسته ۱۰۰ کلید؛ اعتبارسنجی: JSON، کلیدها، جای‌نگه‌دارها، الفبای هدف؛ تلاش مجدد ×۲
import json, os, re, subprocess, sys, time

GAP = '/home/z/my-project/scripts/langgap/langgap'
OUT = os.path.join(GAP, 'results')
os.makedirs(OUT, exist_ok=True)
LOG = os.path.join(GAP, 'runner.log')

LANGS = {
    'ka':  ('Georgian (ქართული)', 'latin-georgian'),
    'ky':  ('Kyrgyz (Кыргызча, Cyrillic)', 'cyrillic'),
    'prs': ('Dari Persian (دری افغانستان)', 'arabic-script'),
    'ps':  ('Pashto (پښتو)', 'arabic-script'),
    'tg':  ('Tajik (тоҷикӣ, Cyrillic)', 'cyrillic'),
    'tk':  ('Turkmen (Türkmençe, Latin)', 'latin'),
    'uz':  ('Uzbek (Oʻzbekcha, Latin)', 'latin'),
}
ARABIC = re.compile(r'[\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFF]')
PH = re.compile(r'\{\d\}')
BATCH = 30  # خروجی مدل محدود است — دستهٔ کوچک + شکست تطبیقی

def log(msg):
    line = f'[{time.strftime("%H:%M:%S")}] {msg}'
    print(line, flush=True)
    with open(LOG, 'a', encoding='utf-8') as f:
        f.write(line + '\n')

def call_zai(system, user, outp):
    try:
        r = subprocess.run(['z-ai', 'chat', '-s', system, '-p', user, '-o', outp],
                           capture_output=True, text=True, timeout=180)
        if r.returncode != 0:
            return None, f'exit={r.returncode}: {r.stderr[-200:]}'
        d = json.load(open(outp, encoding='utf-8'))
        return d['choices'][0]['message']['content'], None
    except Exception as e:
        return None, str(e)[:200]

def parse_json_text(t):
    t = t.strip()
    if t.startswith('```'):
        t = re.sub(r'^```[a-zA-Z]*\n?', '', t)
        t = re.sub(r'\n?```$', '', t).strip()
    return json.loads(t)

def validate(code, keys, mapping):
    fam = LANGS[code][1]
    bad = []
    for k in keys:
        v = mapping.get(k)
        if not isinstance(v, str) or not v.strip():
            bad.append((k, 'empty'))
            continue
        if len(v) > 3 * len(k) + 40:
            bad.append((k, 'too long'))
            continue
        if PH.findall(k) and sorted(PH.findall(k)) != sorted(PH.findall(v)):
            bad.append((k, 'placeholder'))
            continue
        if fam in ('latin', 'cyrillic', 'latin-georgian') and ARABIC.search(v):
            bad.append((k, 'arabic-script leak'))
            continue
    return bad

def sysprompt(name):
    return (f'You are a professional software localizer for an HR/attendance system. '
            f'Translate Persian (Farsi) UI strings into {name}. '
            f'Input is a JSON object: key = Persian source string, value = English reference. '
            f'Output ONLY a valid JSON object mapping each Persian input string to its translation in {name.split(" (")[0]}. '
            f'Rules: keep placeholders like {{0}} {{1}} {{2}} exactly; keep real newlines where the source has them; '
            f'keep emojis; do not translate technical tokens (Excel, PDF, CSV, JSON, IP, MAC, ID, LDAP, PIN, NTP, SHA, USB, SSD, SMTP, SMS, ONVIF, RTSP); '
            f'use the standard terminology of {name} office/HR software; write natural, concise UI text; '
            f'no markdown fences, no comments, output JSON only.')

def call_zai_retry(system, user, outp):
    """فراخوانی با تحمل 429 — انتظار تطبیقی و تلاش مجدد روی همان دسته (بدون شکستن)"""
    delays = (15, 30, 60, 90, 120, 180, 240, 300, 300)
    err = None
    for d in delays:
        content, err = call_zai(system, user, outp)
        if err is None:
            return content, None
        if '429' in err or 'Too many' in err:
            time.sleep(d)
            continue
        return None, err   # خطای غیر 429 — به لایهٔ بالاتر
    return None, err

def attempt(code, name, ref, tmp, extra=''):
    payload = json.dumps(ref, ensure_ascii=False)
    content, err = call_zai_retry(sysprompt(name) + extra, payload, tmp)
    if err:
        return None, err
    try:
        mapping = parse_json_text(content)
    except Exception as e:
        return None, f'parse: {str(e)[:100]}'
    bad = validate(code, list(ref.keys()), mapping)
    if bad:
        for k, why in bad:
            mapping.pop(k, None)
        return mapping, f'{len(bad)} bad: {sorted({w for _, w in bad})}'
    return mapping, None

def translate_chunk(code, name, chunk, tmp, depth=0):
    """یک دسته را ترجمه می‌کند؛ شکست = نصف کردن تطبیقی. خروجی: (mapping, باقی‌مانده)"""
    keys = [c[0] for c in chunk]
    ref = {c[0]: c[1] for c in chunk}
    mapping, err = attempt(code, name, ref, tmp)
    got = {k: v for k, v in (mapping or {}).items() if isinstance(v, str) and v.strip()}
    if err is None:
        return got, []
    # تلاش اصلاحی برای بدِها (فقط وقتی پاسخ جزئی آمده)
    if got and len(got) < len(keys):
        rest = {k: ref[k] for k in keys if k not in got}
        m2, err2 = attempt(code, name, rest, tmp,
                           extra=' NOTE: previous attempt was rejected. Keep placeholders exactly; '
                                 'use ONLY the target language script (no Persian/Arabic script).')
        got.update({k: v for k, v in (m2 or {}).items() if isinstance(v, str) and v.strip()})
    still = [k for k in keys if k not in got]
    if not still:
        return got, []
    if len(still) <= 2 or depth >= 3:
        return got, still
    # شکست تطبیقی: نصف کردن
    half = len(still) // 2
    a = [c for c in chunk if c[0] in still[:half]]
    b = [c for c in chunk if c[0] in still[half:]]
    ga, la = translate_chunk(code, name, a, tmp, depth + 1)
    gb, lb = translate_chunk(code, name, b, tmp, depth + 1)
    ga.update(gb)
    return got | ga, la + lb

def translate_lang(code):
    name, fam = LANGS[code]
    work = json.load(open(os.path.join(GAP, f'work-{code}.json'), encoding='utf-8'))
    res_path = os.path.join(OUT, f'result-{code}.json')
    results = {}
    if os.path.exists(res_path):
        results = json.load(open(res_path, encoding='utf-8'))
    todo = [w for w in work if w[0] not in results]
    log(f'{code}: {len(todo)} باقی‌مانده از {len(work)}')
    tmp = f'/tmp/zai-batch-{os.getpid()}.json'  # هر پروسهٔ موازی فایل خودش
    nb = 0
    for bi in range(0, len(todo), BATCH):
        chunk = todo[bi:bi + BATCH]
        nb += 1
        got, lost = translate_chunk(code, name, chunk, tmp)
        results.update(got)
        json.dump(results, open(res_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
        if lost:
            log(f'{code} batch {nb}: {len(got)}/{len(chunk)} OK — lost {len(lost)}: {lost[:2]}')
        time.sleep(1.5)   # پایی میان دسته‌ها — مهربان‌تر با سهمیهٔ API
        if nb % 10 == 0:
            log(f'{code}: پیشرفت {len(results)}/{len(work)}')
    log(f'{code}: finished — {len(results)}/{len(work)}')

if __name__ == '__main__':
    only = sys.argv[1:] if len(sys.argv) > 1 else list(LANGS)
    for code in only:
        translate_lang(code)
    log('ALL DONE')
