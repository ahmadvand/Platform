#!/bin/bash
# ======================================================================
# اسکریپت توسعهٔ رسمی پلتفرم برای پروژهٔ HR (ASP.NET Core 8)
# start.sh پلتفرم هنگام بوت این فایل را به‌صورت پس‌زمینه اجرا می‌کند
# (جایگزین bun run dev) — اپ روی پورت 3000 = همان پورتی که Caddy پروکسی می‌کند
# ======================================================================
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

DOTNET=/home/z/.dotnet/dotnet

# خودترمیمی: ری‌بیلد سندباکس /home/z/.dotnet (بیرون از پروژه) را پاک می‌کند
# → در این حالت SDK را از اسکریپت کش‌شدهٔ داخل پروژه دوباره نصب کن
# فاز ۶۲ — SDK برای «اجرا» لازم نیست (باینری self-contained گیت‌شده);
# نصب ناموفق هم بوت را متوقف نمی‌کند — فقط بیلدهای بعدی محدود می‌شوند.
if [ ! -x "$DOTNET" ]; then
  echo "[dev.sh] dotnet SDK missing (sandbox rebuild?) — auto-installing..." >&2
  bash /home/z/my-project/scripts/dotnet-install.sh \
       --channel 8.0 --install-dir /home/z/.dotnet \
       >>/home/z/my-project/scripts/dev-install.log 2>&1 || true
fi

# فاز ۶۲ — ترتیب حیاتی بوت:
#  ۱) regen-dl --fast «همگام» (چند ثانیه، بدون بیلد) — مرکز دانلود باید «پیش از»
#     استارت اپ آماده باشد (مپینگ /dl در Program.cs فقط اگر پوشه هنگام استارت
#     موجود باشد ثبت می‌شود)
#  ۲) بسته‌های سنگین FULL/MINIMAL ده دقیقه بعد از بوت — بیرون از پنجرهٔ سلامت
#     ۱۲۰ثانیه‌ایِ لبه؛ بیلد هم‌زمان با بوت اپ را OOM کرد و ثبت مسیر preview از
#     دست می‌رفت ← ۴۰۴ خارجی دائمی (تجربهٔ بوت ۲۰۲۶-۱۱-۰۶)
#  ۳) نگه‌بان keepalive همیشه فعال (خط دوم دفاع پس از سوپروایزر start-web)
bash /home/z/my-project/scripts/regen-dl.sh --fast >/dev/null 2>&1 || true
( sleep 600 && bash /home/z/my-project/scripts/regen-dl.sh >/dev/null 2>&1 ) &
( sleep 5 && bash /home/z/my-project/scripts/keepalive.sh >/dev/null 2>&1 ) &

exec bash /home/z/my-project/scripts/start-web.sh
