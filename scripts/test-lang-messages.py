# -*- coding: utf-8 -*-
# آزم زندهٔ پیام اعتبارسنجی fa/en — تسک «پیام چندزبانه»
import re, requests

B = "http://localhost:3000"
s = requests.Session()
r = s.post(f"{B}/api/auth/login", json={"username": "admin", "password": "Admin@1234"}, timeout=10)
assert r.ok, r.status_code

def try_create(lang):
    s.cookies.set("hr_lang", lang)
    page = s.get(f"{B}/Companies", timeout=10)
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page.text)
    tok = m.group(1) if m else ""
    r2 = s.post(f"{B}/Companies/Create", headers={"X-Requested-With": "XMLHttpRequest"},
                data={"name": "", "code": "", "__RequestVerificationToken": tok}, timeout=10)
    try:
        return r2.json().get("message", "?")
    except Exception:
        return f"NON-JSON ({r2.status_code}): {r2.text[:120]}"

print("fa:", try_create("fa"))
print("en:", try_create("en"))

# ورود اشتباه از API
r3 = requests.post(f"{B}/api/auth/login", json={"username": "admin", "password": "WRONG"},
                   cookies={"hr_lang": "en"}, timeout=10)
print("en login-fail:", r3.json().get("error"))
r4 = requests.post(f"{B}/api/auth/login", json={"username": "admin", "password": "WRONG"},
                   cookies={"hr_lang": "fa"}, timeout=10)
print("fa login-fail:", r4.json().get("error"))
