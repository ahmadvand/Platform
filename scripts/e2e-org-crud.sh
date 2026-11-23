#!/bin/bash
# ============================================================
# E2E: چارت سازمانی — ویرایش/جابجایی/حذف واحد و پست (فاز چارت) — v2
# ============================================================
B=http://localhost:3000
J=/tmp/org-e2e.jar
rm -f "$J"

code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "LOGIN FAILED ($code)"; exit 1; }
echo "PASS login"

token() {
  curl -s -b "$J" -c "$J" "$B/Org" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'
}
T=$(token)
[ -n "$T" ] || { echo "NO ANTIFORGERY TOKEN"; exit 1; }

post() { # $1=path  rest: -d data
  curl -s -b "$J" -c "$J" -L -H "RequestVerificationToken: $T" --data-urlencode "__RequestVerificationToken=$T" "${@:2}" "$B$1"
}

idof() { # python-based id extraction: $1=anchor text (HTML-escaped as in page)
  python3 -c "
import re
html = open('/tmp/org-page.html', encoding='utf-8').read()
i = html.find('''$1''')
seg = html[i:i+3000] if i >= 0 else ''
m = re.search(r'[a-f0-9]{32}', seg)
print(m.group(0) if m else '')
"
}

# --- پاکسازی اجراهای قبلی (idempotent) ---
curl -s -b "$J" "$B/Org" -o /tmp/org-page.html
OLD_PARENT=$(idof 'TESTQ9')
OLD_CHILD=$(idof 'TESTC9')
OLD_POS=$(idof 'TESTP9')
[ -n "$OLD_POS" ] && post /Org/DeletePos -d "id=$OLD_POS" > /dev/null
[ -n "$OLD_CHILD" ] && post /Org/DeleteDept -d "id=$OLD_CHILD" > /dev/null
[ -n "$OLD_PARENT" ] && post /Org/DeleteDept -d "id=$OLD_PARENT" > /dev/null

# --- دکمه‌های جدید در صفحه ---
HTML=$(curl -s -b "$J" "$B/Org")
echo "$HTML" | grep -q 'data-toggle="de-' && echo "PASS edit buttons in tree" || echo "FAIL edit buttons"
echo "$HTML" | grep -q 'data-toggle="dm-' && echo "PASS move buttons in tree" || echo "FAIL move buttons"
echo "$HTML" | grep -q '/Org/DeleteDept' && echo "PASS delete-dept forms" || echo "FAIL delete-dept"
echo "$HTML" | grep -q '/Org/DeletePos' && echo "PASS delete-pos forms" || echo "FAIL delete-pos"
echo "$HTML" | grep -q '/Org/EditPos' && echo "PASS edit-pos rows" || echo "FAIL edit-pos"

# --- ساخت واحدهای آزمایشی (هر دو در ریشه) ---
post /Org/CreateDept -d "name=تست-E2E-والد&code=TESTQ9&parentId=" > /dev/null
post /Org/CreateDept -d "name=تست-E2E-فرزند&code=TESTC9&parentId=" > /dev/null
HTML=$(curl -s -b "$J" "$B/Org")
PARENT_ID=$(echo "$HTML" | grep -o 'value="[a-f0-9]\{32\}"[^>]*>تست-E2E-والد' | grep -o '[a-f0-9]\{32\}' | head -1)
CHILD_ID=$(echo "$HTML" | grep -o 'value="[a-f0-9]\{32\}"[^>]*>تست-E2E-فرزند' | grep -o '[a-f0-9]\{32\}' | head -1)
[ -n "$PARENT_ID" ] && [ -n "$CHILD_ID" ] && echo "PASS create scratch depts" || { echo "FAIL create scratch ($PARENT_ID / $CHILD_ID)"; exit 1; }

# --- جابجایی فرزند زیر والد ---
post /Org/MoveDept -d "id=$CHILD_ID&parentId=$PARENT_ID" | grep -q "منتقل شد" && echo "PASS move child under parent" || echo "FAIL move child"

# --- ویرایش واحد ---
post /Org/EditDept -d "id=$PARENT_ID&name=تست-E2E-والد-ویرایش‌شده&code=TESTQ9" | grep -q "ویرایش شد" && echo "PASS edit dept" || echo "FAIL edit dept"
curl -s -b "$J" "$B/Org" | grep -q "تست-E2E-والد-ویرایش‌شده" && echo "PASS edit visible in tree" || echo "FAIL edit visible"

# --- تلاش برای حلقه: والد زیر فرزند خودش (فرزند الان زیر والد است) ---
post /Org/MoveDept -d "id=$PARENT_ID&parentId=$CHILD_ID" | grep -q "جابجایی مجاز نیست" && echo "PASS cycle rejected" || echo "FAIL cycle"

# --- گارد حذف: والد دارای فرزند ---
post /Org/DeleteDept -d "id=$PARENT_ID" | grep -q "حذف ممکن نیست" && echo "PASS delete guard (children)" || echo "FAIL delete guard"

# --- جابجایی فرزند به ریشه ---
post /Org/MoveDept -d "id=$CHILD_ID&parentId=" | grep -q "ریشه چارت" && echo "PASS move to root" || echo "FAIL move to root"

# --- پست آزمایشی: ساخت/ویرایش/حذف (والد هنوز زنده است) ---
post /Org/CreatePos -d "title=پست-تست-E2E&code=TESTP9&departmentId=$PARENT_ID&grade=" > /dev/null
curl -s -b "$J" "$B/Org" -o /tmp/org-page.html
POS_ID=$(idof 'TESTP9</td>')
[ -n "$POS_ID" ] && echo "PASS create pos" || echo "FAIL create pos"
post /Org/EditPos -d "id=$POS_ID&title=پست-تست-ویرایش&code=TESTP9&departmentId=$PARENT_ID&grade=A" | grep -q "ویرایش شد" && echo "PASS edit pos" || echo "FAIL edit pos"
post /Org/DeletePos -d "id=$POS_ID" | grep -q "حذف شد" && echo "PASS delete pos" || echo "FAIL delete pos"

# --- پاکسازی: فرزند بعد والد ---
post /Org/DeleteDept -d "id=$CHILD_ID" | grep -q "حذف شد" && echo "PASS cleanup child" || echo "FAIL cleanup child"
post /Org/DeleteDept -d "id=$PARENT_ID" | grep -q "حذف شد" && echo "PASS cleanup parent" || echo "FAIL cleanup parent"
curl -s -b "$J" "$B/Org" | grep -c "TESTQ9\|TESTC9\|TESTP9" | grep -q '^0$' && echo "PASS no leftovers" || echo "FAIL leftovers"

echo "=== done ==="
