#!/usr/bin/env python3
# فاز ۸۱ — ترجمهٔ دسته‌ای کلیدهای غایب دیکشنری en با z-ai CLI
import json, subprocess, sys, time, os

SRC = '/tmp/missing-razor.txt'
OUT = '/tmp/razor-translations.json'
BATCH = 90

strings = [l.rstrip('\n') for l in open(SRC, encoding='utf-8') if l.strip()]
done = {}
if os.path.exists(OUT):
    done = json.load(open(OUT, encoding='utf-8'))
todo = [s for s in strings if s not in done]
print(f'total={len(strings)} done={len(done)} todo={len(todo)}', flush=True)

SYS = ("You are a professional fa→en translator for an HR/attendance enterprise web app UI. "
       "Translate each Persian UI string to concise professional English suitable for buttons, labels, table headers, "
       "toasts and hints. Keep {0} {1} placeholders intact. Keep leading/trailing spaces and punctuation mirrors. "
       "Keep emoji as-is. Do NOT add quotes. Output STRICT JSON: a single object mapping each input string to its translation, nothing else.")

def call(batch):
    payload = json.dumps(batch, ensure_ascii=False)
    p = subprocess.run(['z-ai', 'chat', '-p', payload, '-s', SYS, '-o', '/tmp/zai-out.json'],
                       capture_output=True, text=True, timeout=300)
    if p.returncode != 0:
        raise RuntimeError('cli fail: ' + p.stderr[:200])
    raw = open('/tmp/zai-out.json', encoding='utf-8').read()
    # CLI returns {content: "..."} or the raw content file
    try:
        j = json.loads(raw)
        if isinstance(j, dict) and 'content' in j:
            raw = j['content']
        elif isinstance(j, dict) and 'choices' in j:
            raw = j['choices'][0]['message']['content']
    except Exception:
        pass
    raw = raw.strip()
    if raw.startswith('```'):
        raw = raw.split('```')[1]
        if raw.startswith('json'): raw = raw[4:]
    return json.loads(raw)

for i in range(0, len(todo), BATCH):
    batch = todo[i:i+BATCH]
    ok = False
    for attempt in range(3):
        try:
            res = call(batch)
            for k, v in res.items():
                if k in done or not v: continue
                done[k] = str(v)
            ok = True
            break
        except Exception as e:
            print(f'batch@{i} attempt{attempt}: {str(e)[:120]}', flush=True)
            time.sleep(3 * (attempt + 1))
    json.dump(done, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False)
    print(f'progress: {len(done)}', flush=True)
    if not ok: print('batch skipped after retries', flush=True)

print('FINISHED', len(done))
