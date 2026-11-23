#!/usr/bin/env python3
# ساخت فایل دورهٔ آزمایشی منقضی — همان HMAC سمت سرور (MachineRaw + "|trial|" + Salt)
import hashlib, hmac, base64, json, os, sys
from datetime import datetime, timedelta, timezone

machine_id = open('/etc/machine-id').read().strip()
salt = 'HrAttendance|Licensing|v1'
key = hashlib.sha256((machine_id + '|trial|' + salt).encode()).digest()

first = datetime.now(timezone.utc) - timedelta(days=40)  # ۴۰ روز پیش با TrialDays=30 → منقضی
msg = first.strftime('%Y-%m-%dT%H:%M:%S.%f')[:-1] + 'Z' if False else first.isoformat('T')
# C# DateTime.ToString("O") روی UTC با Z تمام می‌شود؛ فرمت استاندارد ISO با 7 رقم کسری
frac = first.strftime('%f')[:6] + '0'  # 7 رقم
o = first.strftime('%Y-%m-%dT%H:%M:%S.') + frac + 'Z'
mac = base64.b64encode(hmac.new(key, o.encode(), hashlib.sha256).digest()).decode()

out = sys.argv[1]
with open(out, 'w') as f:
    json.dump({'First': o, 'Mac': mac}, f)
print('trial file written:', out, 'First=', o)
