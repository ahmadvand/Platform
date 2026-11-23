#!/bin/bash
# فاز ۱۰۶ب — راستی‌آزمایی زندهٔ کامل فازهای ۱۰۴–۱۰۹ روی اپ منتشرشده
cd /home/z/my-project || exit 1
CK=/tmp/ver106-cookies.txt
rm -f "$CK"; mkdir -p /tmp/ver106
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); echo "  ✗ $1"; }

get_token() { curl -s -b "$CK" -c "$CK" "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//'; }
post() { curl -s -b "$CK" -c "$CK" -o /dev/null -w "%{http_code}" -X POST "$1" "${@:2}"; }
fetch() { C=$(curl -s -b "$CK" -b "hr_lang=$1" -o "$3" -w "%{http_code}" "http://127.0.0.1:3000$2"); python3 -c "
import html,sys
p=sys.argv[1]
t=html.unescape(open(p,encoding='utf-8',errors='ignore').read())
open(p,'w',encoding='utf-8').write(t)" "$3" 2>/dev/null; echo "$C"; }

echo "== ورود =="
T=$(get_token http://127.0.0.1:3000/Auth/Login)
C=$(post http://127.0.0.1:3000/Auth/Login --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" --data-urlencode "__RequestVerificationToken=$T")
[ "$C" = "302" ] || [ "$C" = "200" ] && ok "login ($C)" || bad "login ($C)"
T=$(get_token http://127.0.0.1:3000/License/Notice)
C=$(post http://127.0.0.1:3000/License/Ack --data-urlencode "__RequestVerificationToken=$T")
[ "$C" = "302" ] || [ "$C" = "200" ] && ok "license ack ($C)" || bad "license ack ($C)"

echo "== پرچم و استاتیک =="
C=$(curl -s -o /tmp/ver106/ru.svg -w "%{http_code}" http://127.0.0.1:3000/img/flags/ru.svg)
[ "$C" = "200" ] && ok "GET /img/flags/ru.svg ($C, $(wc -c </tmp/ver106/ru.svg)B)" || bad "ru.svg ($C)"
grep -q "flagcdn\|<rect\|<path" /tmp/ver106/ru.svg && ok "ru.svg real content" || bad "ru.svg content"

echo "== صفحات روسی (شمارش کیریلیک) =="
for pg in "/:dash" "/Audit:audit" "/Langs:langs" "/Biometrics:bio" "/Settings:settings" "/Org:org" "/Devices:dev" "/Payroll:pay" "/Users/Roles:roles" "/Reports:rep" "/Access:acc" "/Updates:upd"; do
  url="${pg%%:*}"; name="${pg##*:}"
  code=$(fetch ru "$url" "/tmp/ver106/ru-$name.html")
  CY=$(python3 -c "
import html,re
t=open('/tmp/ver106/ru-$name.html',encoding='utf-8',errors='ignore').read()
t=html.unescape(t)
print(len(re.findall('[А-Яа-яЁё]', t)))" 2>/dev/null)
  if [ "$code" = "200" ] && [ "$CY" -gt 40 ] 2>/dev/null; then ok "$url ru 200 cyr=$CY"; else bad "$url ru code=$code cyr=$CY"; fi
done

echo "== ممیزی (فاز ۱۰۶) =="
fetch fa "/Audit" /tmp/ver106/fa-audit.html >/dev/null
fetch ru "/Audit" /tmp/ver106/ru-audit.html >/dev/null
grep -q "ممیزی" /tmp/ver106/fa-audit.html && ok "fa Audit page says «ممیزی»" || bad "fa Audit page ممیزی"
grep -q "حسابرسی" /tmp/ver106/fa-audit.html && bad "fa Audit page still says «حسابرسی»" || ok "fa Audit page no «حسابرسی»"
grep -q "Аудит" /tmp/ver106/ru-audit.html && ok "ru Audit says Аудит" || bad "ru Audit Аудит"

echo "== زنجیرهٔ ممیزی (فاز ۱۰۹) =="
fetch fa "/Audit/ChainHealth" /tmp/ver106/chain.json >/dev/null
python3 -c "
import json;d=json.load(open('/tmp/ver106/chain.json'))
print('  chain json keys:', sorted(d.keys())[:8])" 2>/dev/null && ok "GET /Audit/ChainHealth" || bad "ChainHealth"
grep -q "بررسی سلامت زنجیره" /tmp/ver106/fa-audit.html && ok "chain button on audit page" || bad "chain button"

echo "== انتخابگر پرسنل (فاز ۱۰۷) =="
fetch fa "/Biometrics/SearchPersonnel?q=&skip=0&take=5" /tmp/ver106/sp.json >/dev/null
python3 -c "
import json;d=json.load(open('/tmp/ver106/sp.json'))
ks=sorted(d.keys());print('  sp keys:',ks[:8])" 2>/dev/null && ok "GET /Biometrics/SearchPersonnel" || bad "SearchPersonnel"
fetch fa "/Biometrics" /tmp/ver106/fa-bio.html >/dev/null
grep -q "SearchPersonnel" /tmp/ver106/fa-bio.html && ok "bio view uses SearchPersonnel" || bad "bio view SearchPersonnel"

echo "== پاکسازی متون توسعه (فاز ۱۰۸) =="
fetch fa "/Settings" /tmp/ver106/fa-settings.html >/dev/null
grep -q "dotnet publish" /tmp/ver106/fa-settings.html && bad "settings still shows dotnet publish" || ok "settings no dotnet publish"
P=$(grep -ro "فاز ۱۰[0-9][^<\"]*" /tmp/ver106/ru-dash.html /tmp/ver106/fa-settings.html /tmp/ver106/fa-audit.html 2>/dev/null | head -3)
[ -z "$P" ] && ok "no phase numbers in UI texts" || bad "phase numbers leak: $P"

echo "== صفحات fa/en سالم =="
code=$(fetch fa "/" /tmp/ver106/fa-dash.html); [ "$code" = "200" ] && ok "fa dashboard 200" || bad "fa dashboard $code"
code=$(fetch en "/" /tmp/ver106/en-dash.html); [ "$code" = "200" ] && ok "en dashboard 200" || bad "en dashboard $code"

echo "== درون‌ریزی زبان‌ها =="
fetch ru "/Langs" /tmp/ver106/ru-langs.html >/dev/null
grep -q "Русский" /tmp/ver106/ru-langs.html && ok "Langs page shows Русский" || bad "Langs page Русский"
grep -q "img/flags/ru.svg" /tmp/ver106/ru-langs.html && ok "Langs page uses ru.svg" || bad "Langs ru.svg"
grep -q "img/flags/ru.svg" /tmp/ver106/ru-dash.html && ok "topbar switcher uses ru.svg" || bad "topbar ru.svg"

echo ""
echo "RESULT: PASS=$PASS FAIL=$FAIL"
exit $([ "$FAIL" -eq 0 ] && echo 0 || echo 1)
