# -*- coding: utf-8 -*-
# فاز ۱۰۱ — بازنویسی بلوک CSS محافظ صفحه در site.css
# نکته: سلکتور .hr-ss-pw[hidden] سالم است (نمایش قبلی ابزار گمراه‌کننده بود) — فقط چیدمان عوض می‌شود:
#   حذف: .hr-ss-c / .hr-ss-clock / .hr-ss-idle / .hr-ss-hint
#   افزودن: .hr-ss-corner (تاریخ/ساعت ریز پایین-چپ) + پنل رمز وسط‌چین + .hr-ss-pw-title
import io

P = '/home/z/my-project/aspnet/src/Platform.Web/wwwroot/css/site.css'
src = io.open(P, encoding='utf-8').read().split('\n')

# اعتبارسنجی شماره خطوط (۰-مبنا) — با dump کاراکتری تأیید شده
assert src[1426].startswith('.hr-ss-c {'), repr(src[1426])
assert src[1434].startswith('.hr-ss-pw[hidden]'), repr(src[1434])
assert src[1435].startswith('.hr-ss-pw input {'), repr(src[1435])

NEW = r'''/* فاز ۱۰۱ — تاریخ/ساعت ریزِ گوشهٔ پایین-چپ (فیزیکی؛ در RTL هم چپ می‌ماند) — ساعت بزرگ و «مدت بی‌حرکتی» حذف شدند */
.hr-ss-corner { position: absolute; left: 16px; bottom: 13px; z-index: 2; text-align: left;
    line-height: 1.7; text-shadow: 0 0 10px rgba(34,197,94,.45); font-variant-numeric: tabular-nums;
    max-width: 46vw; pointer-events: none; }
.hr-ss-time { font-size: 14px; font-weight: 700; letter-spacing: 1.5px; color: #86efac; }
.hr-ss-date { font-size: 11.5px; color: rgba(134,239,172,.78); font-family: inherit; }
/* فاز ۱۰۱ — پنل رمز: تنها فرزندِ جریانِ .hr-ss است و flex والد آن را دقیقاً وسط صفحه می‌کند */
.hr-ss-pw { position: relative; z-index: 3; display: flex; flex-direction: column; align-items: center; gap: 10px;
    padding: 26px 30px; min-width: min(360px, 86vw); border-radius: 18px;
    background: rgba(3,18,11,.84); border: 1px solid rgba(34,197,94,.35);
    box-shadow: 0 24px 70px rgba(0,0,0,.65), 0 0 44px rgba(34,197,94,.14); }
.hr-ss-pw[hidden] { display: none; }
.hr-ss-pw-title { font-size: 13.5px; color: #a7f3d0; text-align: center; line-height: 1.9; }'''.split('\n')

# جایگزینی خطوط ۱۴۲۷ تا ۱۴۳۵ (۱-مبنا) = بازهٔ ۰-مبنا [1426, 1435)
src[1426:1435] = NEW
out = '\n'.join(src)
# پهنای input داخل پنل: 78vw → 70vw
out = out.replace('.hr-ss-pw input { width: min(320px, 78vw);', '.hr-ss-pw input { width: min(320px, 70vw);')
io.open(P, 'w', encoding='utf-8').write(out)
print('OK — screensaver CSS block replaced')
