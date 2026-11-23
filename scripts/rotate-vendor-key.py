#!/usr/bin/env python3
# چرخش کلید مرجع تولیدکننده — هم‌راستاسازی EmbeddedKeys (استودیو) و LicenseService (سرور)
# با tools/keys/public.pem. قالب رشته: قطعات ۶۰ کاراکتری با '+' و تورفتگی مطابق قبل.
import re

ROOT = "/home/z/my-project"
spki = open("/tmp/spki.txt").read().strip()

def chunked(s, n=60):
    return [s[i:i+n] for i in range(0, len(s), n)]

def lit(spki):
    parts = chunked(spki)
    body = ""
    for i, p in enumerate(parts):
        tail = ' +' if i < len(parts) - 1 else ';'
        pre = '        "' if i == 0 else '        "'
        body += f'{pre}{p}"{tail}\n'
    return body

def patch(path, pattern, repl_desc):
    src = open(path, encoding="utf-8").read()
    new, cnt = re.subn(pattern, lambda m: m.group(1) + lit(spki) + m.group(2), src, count=1, flags=re.S)
    assert cnt == 1, f"pattern not found in {path} ({repl_desc})"
    open(path, "w", encoding="utf-8").write(new)
    print(f"patched {repl_desc}: {path}")

# ۱) سرور — internal const string PublicKeySpki = ...;
patch(
    f"{ROOT}/aspnet/src/HrAttendance.Web/Modules/Core/Services/LicenseService.cs",
    r'(internal const string PublicKeySpki =\n).*?(;\n)',
    "server LicenseService.PublicKeySpki",
)
# ۲) استودیو — public const string PublicKeySpki = ...;
patch(
    f"{ROOT}/tools/LicenseStudio/EmbeddedKeys.cs",
    r'(public const string PublicKeySpki =\n).*?(\n\})',
    "studio EmbeddedKeys.PublicKeySpki",
)
print("done — SPKI fingerprint head:", spki[:24])
