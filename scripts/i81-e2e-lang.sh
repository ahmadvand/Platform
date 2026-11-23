#!/bin/bash
# فاز ۸۱ — تست دوزبانهٔ زنده (en/fa): ورود، تعویض زبان، بررسی ترجمه در صفحات کلیدی
B=http://localhost:3000
J=/tmp/hr81-cookies.txt
rm -f $J

login() {  # $1 = lang cookie value
  rm -f $J
  curl -s -c $J -o /dev/null "$B/Auth/Login"
  tok=$(curl -s -b $J -c $J "$B/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
  curl -s -b $J -c $J -o /dev/null -w "login=%{http_code} " -X POST "$B/Auth/Login" \
    --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
    --data-urlencode "__RequestVerificationToken=$tok"
}

check_page() { # $1=url $2=persian-gone-needle $3=english-needle $4=label
  html=$(curl -s -b $J -c $J --max-time 20 "$B$1")
  fa=0; en=0
  echo "$html" | grep -q "$2" && fa=1
  echo "$html" | grep -q "$3" && en=1
  printf "%-28s fa(%s)=%s en(%s)=%s\n" "$4" "$2" "$fa" "$3" "$en"
}

echo "== FA (پیش‌فرض) =="
login
curl -s -b $J -c $J -o /dev/null "$B/Ui/Lang?l=fa&r=/"
check_page "/Notifications" "Notifications" "اعلان‌ها" "Notifications fa"
check_page "/Settings" "System Settings" "تنظیمات سامانه" "Settings fa"
check_page "/License" "Install License" "مجوز نصب" "License fa"

echo "== EN =="
curl -s -b $J -c $J -o /dev/null "$B/Ui/Lang?l=en&r=/"
check_page "/Notifications" "اعلان‌ها" "Notifications" "Notifications en"
check_page "/Settings" "تنظیمات سامانه" "System Settings" "Settings en"
check_page "/License" "مجوز نصب" "Install License" "License en"
check_page "/Modules" "ماژول" "Module" "Modules en"
check_page "/Payroll" "حقوق و دستمزد" "Payroll" "Payroll en"
check_page "/Devices" "دستگاه‌های تردد" "Attendance Devices" "Devices en"
check_page "/Org" "چارت سازمانی" "Organization" "Org en"

echo "== خروجی اکسل en (سرستون‌ها) =="
curl -s -b $J -c $J -o /tmp/hr81-emp.xlsx -w "export=%{http_code} size=%{size_download}\n" "$B/Export/Employees"
python3 - <<'PY'
import zipfile, re
try:
    z = zipfile.ZipFile('/tmp/hr81-emp.xlsx')
    ss = z.read('xl/sharedStrings.xml').decode('utf-8')
    heads = re.findall(r'<t[^>]*>([^<]+)</t>', ss)[:12]
    print("EN headers:", heads)
except Exception as e:
    print("xlsx error:", e)
PY
