#!/usr/bin/env python3
"""خروجی dbq.py را به یک مقدار اسکالر تبدیل می‌کند — JSON آرایه‌ای یا مقدار خام."""
import sys, json
s = sys.stdin.read().strip()
try:
    d = json.loads(s)
    print(d[0][0] if isinstance(d, list) else d)
except Exception:
    print(s)
