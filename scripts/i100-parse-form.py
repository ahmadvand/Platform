# -*- coding: utf-8 -*-
# فاز ۱۰۰ — پارس generalForm از HTML تنظیمات و ساخت بدنهٔ POST کامل (با جایگزینی فیلدهای محافظ صفحه)
import re, sys, io, urllib.parse

html = io.open(sys.argv[1], encoding="utf-8").read()
SS = {"ssEnabled": "true", "ssMinutes": "0", "ssSeconds": "10", "ssRequirePassword": "true"}
# بازنویسی با متغیر محیطی — مثال: SS_OVERRIDE="ssMinutes=1,ssSeconds=0"
import os
for kv in os.environ.get("SS_OVERRIDE", "").split(","):
    if "=" in kv:
        k, v = kv.split("=", 1)
        SS[k.strip()] = v.strip()

form = html[html.index('action="/Settings/SaveGeneral"'):]
form = form[:form.index("</form>")]
fields = {}
tok = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', form)
if tok: fields["__RequestVerificationToken"] = tok.group(1)

for m in re.finditer(r'<input\b[^>]*>', form):
    tag = m.group(0)
    nm = re.search(r'name="([^"]+)"', tag)
    if not nm: continue
    n = nm.group(1)
    if n in fields or n.startswith("__RequestVerificationToken"): continue
    tm = re.search(r'type="([^"]+)"', tag); typ = tm.group(1) if tm else "text"
    vm = re.search(r'value="([^"]*)"', tag)
    if typ in ("checkbox", "radio"):
        if 'checked' in tag and vm: fields[n] = vm.group(1)
    else:
        fields[n] = vm.group(1) if vm else ""

for m in re.finditer(r'<select\b[^>]*name="([^"]+)"[^>]*>(.*?)</select>', form, re.S):
    n, inner = m.group(1), m.group(2)
    if n in fields: continue
    sel = re.search(r'<option\b[^>]*value="([^"]*)"[^>]*\bselected', inner) or re.search(r'<option\b[^>]*value="([^"]*)"', inner)
    if sel: fields[n] = sel.group(1)

for m in re.finditer(r'<textarea\b[^>]*name="([^"]+)"[^>]*>(.*?)</textarea>', form, re.S):
    if m.group(1) not in fields: fields[m.group(1)] = m.group(2)

assert "companyName" in fields and fields["companyName"], "companyName must exist"
fields.update(SS)
sys.stdout.write(urllib.parse.urlencode(fields))
sys.stderr.write("fields=%d ss=%s+%ss ok\n" % (len(fields), SS.get("ssMinutes"), SS.get("ssSeconds")))
