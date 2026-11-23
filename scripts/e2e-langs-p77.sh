#!/bin/bash
# ============================================================
# E2E فاز ۷۷ — زبان‌های پویا: پنل ادمین /Langs + بستهٔ ترجمه (import/export)
# ============================================================
B=http://localhost:3000
J=/tmp/l77.jar
rm -f "$J" /tmp/de-pack.json
code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "FAIL login ($code)"; exit 1; }
echo "PASS login"

tok() { curl -s -b "$J" -c "$J" "$B$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
T=$(tok /Langs)
[ -n "$T" ] || { echo "FAIL token"; exit 1; }

# ---- ۱) صفحهٔ زبان‌ها
n=$(curl -s -b "$J" "$B/Langs" | grep -c "زبان‌های سامانه")
[ "$n" -ge 1 ] && echo "PASS langs page renders" || { echo "FAIL langs page"; exit 1; }

# ---- ۲) ساخت بستهٔ آلمانی (یک کلید ترجمه‌شده + یک کلید عمداً جاافتاده)
python3 - << 'PY'
import json
pack = {
  "format": "hr-lang-pack", "version": 1, "code": "de", "title": "Deutsch", "dir": "ltr",
  "entries": {
    "کاربران و نقش‌ها": "Benutzer & Rollen",
    "شرکت‌ها": "Unternehmen",
    "داشبورد": "Dashboard"
  }
}
json.dump(pack, open('/tmp/de-pack.json', 'w', encoding='utf-8'), ensure_ascii=False)
print("  pack written")
PY

# ---- ۳) درون‌ریزی
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  -F "__RequestVerificationToken=$T" -F "file=@/tmp/de-pack.json;type=application/json" \
  "$B/Langs/Import")
echo "$r" | grep -q '"ok":true' && echo "PASS import de package" || { echo "FAIL import: $r"; exit 1; }

# ---- ۴) سوئیچر حالا آلمانی دارد؟
n=$(curl -s -b "$J" "$B/" | grep -c "Deutsch")
[ "$n" -ge 1 ] && echo "PASS de appears in switcher" || { echo "FAIL switcher missing de"; exit 1; }

# ---- ۵) زبان de: کلید ترجمه‌شده → آلمانی، کلید جاافتاده → فارسی (fallback)
page=$(curl -s -b "$J" -H "Cookie: hr_lang=de" "$B/Users")
echo "$page" | grep -q "Benutzer &amp; Rollen" && echo "PASS translated key renders German" || { echo "FAIL de translation missing"; exit 1; }
echo "$page" | grep -q "تنظیمات سامانه" && echo "PASS fallback to Persian for missing key" || { echo "FAIL fallback broken"; exit 1; }
echo "$page" | grep -q 'dir="ltr"' && echo "PASS html dir=ltr for de" || echo "WARN dir check"

# ---- ۶) ادغام دوباره (همان بسته) — بدون دوستان‌سازی
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  -F "__RequestVerificationToken=$T" -F "file=@/tmp/de-pack.json;type=application/json" \
  "$B/Langs/Import")
echo "$r" | grep -q '"ok":true' && echo "PASS re-import merges" || { echo "FAIL re-import: $r"; exit 1; }

# ---- ۷) بستهٔ fa پذیرفته نمی‌شود
python3 -c "
import json
json.dump({'format':'hr-lang-pack','version':1,'code':'fa','title':'فارسی','dir':'rtl','entries':{}}, open('/tmp/fa-pack.json','w',encoding='utf-8'), ensure_ascii=False)
"
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  -F "__RequestVerificationToken=$T" -F "file=@/tmp/fa-pack.json;type=application/json" \
  "$B/Langs/Import")
echo "$r" | grep -q '"ok":false' && echo "PASS fa package rejected" || { echo "FAIL fa accepted: $r"; exit 1; }

# ---- ۸) خروجی بستهٔ en — باید ۵۲۵ کلید داشته باشد
curl -s -b "$J" "$B/Langs/Export?code=en" -o /tmp/en-export.json
en_count=$(python3 -c "import json;print(len(json.load(open('/tmp/en-export.json',encoding='utf-8'))['entries']))")
[ "$en_count" -ge 500 ] && echo "PASS export en ($en_count keys)" || { echo "FAIL export en ($en_count)"; exit 1; }

# ---- ۹) حذف زبان de
DE_ID=$(python3.13 /home/z/my-project/scripts/dbq.py --json "SELECT Id FROM HrLangDefs WHERE Code='de'" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" --data "id=$DE_ID" "$B/Langs/Delete")
echo "$r" | grep -q '"ok":true' && echo "PASS delete de" || { echo "FAIL delete: $r"; exit 1; }
n=$(curl -s -b "$J" "$B/" | grep -c "Deutsch")
[ "$n" = "0" ] && echo "PASS de gone from switcher" || { echo "FAIL de still there"; exit 1; }

echo "ALL DONE"
