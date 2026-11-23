#!/usr/bin/env bash
# راستی‌آزمایی زندهٔ صفحات درون‌ریزی/برون‌بری/تعطیلات روی اپ در حال اجرا
set -u
BASE=http://localhost:3000
JAR=/tmp/hr-e2e-jar; HTML=/tmp/hr-e2e.html
rm -f "$JAR"

curl -s -c "$JAR" "$BASE/Auth/Login" -o "$HTML"
TOK=$(rg -o 'name="__RequestVerificationToken"[^>]*value="([^"]+)"' -r '$1' "$HTML" | head -1)
[ -z "$TOK" ] && { echo "NO-TOKEN"; exit 1; }
CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" \
  -d "identifier=admin" -d "secret=Admin@1234" \
  -d "__RequestVerificationToken=$TOK" "$BASE/Auth/Login")
echo "LOGIN: $CODE (expect 302)"
[ "$CODE" = "302" ] || exit 1

# گیت مجوز (در صورت نیاز)
curl -s -b "$JAR" -c "$JAR" "$BASE/License/Notice" -o "$HTML"
if rg -q "License/Ack|پذیرش" "$HTML"; then
  TOK2=$(rg -o 'name="__RequestVerificationToken"[^>]*value="([^"]+)"' -r '$1' "$HTML" | head -1)
  [ -n "$TOK2" ] && curl -s -b "$JAR" -c "$JAR" -o /dev/null -d "__RequestVerificationToken=$TOK2" "$BASE/License/Ack"
  echo "LICENSE-ACK: done"
fi

check() { # $1=route $2=pattern $3=label
  curl -s -b "$JAR" "$BASE$1" -o "$HTML"
  c=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" "$BASE$1")
  if [ -n "$2" ] && rg -q "$2" "$HTML"; then echo "PASS $3 ($c) — pattern found"
  elif [ -n "$2" ]; then echo "FAIL $3 ($c) — pattern missing: $2"; else echo "HTTP  $3 ($c)"; fi
}
check "/Attendance/Exports"  "قالب برون‌بری تردد|Linux timestamp" "Attendance/Exports (برون‌بری تردد)"
check "/Employees/Import"    "درون‌ریزی|Excel"                    "Employees/Import (درون‌ریزی پرسنل)"
check "/Employees"           "درون‌ریزی"                          "Employees list (دکمهٔ درون‌ریزی)"
check "/Langs"               "درون‌ریزی"                          "Langs (درون‌ریزی بستهٔ زبان)"
check "/Export/Employees"    ""                                   "Export/Employees (برون‌بری پرسنل)"
check "/Holidays"            "تقویم|افزودن تعطیلی"                "Holidays"

echo "== Holidays page details:"
curl -s -b "$JAR" "$BASE/Holidays" -o "$HTML"
rg -o '<h2>[^<]*</h2>' "$HTML" | head -3
rg -o 'card-h' "$HTML" | head -1 >/dev/null && true
