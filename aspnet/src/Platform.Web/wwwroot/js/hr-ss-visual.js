// ============================================================================
// فاز ۱۰۵ — موتور بصری مستقل «باران ماتریکس» (نسخهٔ بسته‌پذیرِ محافظ داخلی)
// ---------------------------------------------------------------------------
// این فایل همان رفتار بارانِ داخلی (hr-screensaver.js) را به‌صورت «کدِ مستقل از
// پلتفرم» ارائه می‌کند تا با دکمهٔ «برون‌بری بسته» در تب محافظ صفحه، به‌عنوان
// بستهٔ hr-screensaver-package (spec 1) صادر و روی هر نصب دیگری از همین سامانه
// داخل iframe سندباکس‌شدهٔ /Settings/ScreensaverRuntime اجرا شود.
//
// قرارداد محیط میزبان (Runtime سندباکس):
//   • <script id="hr-ss-data" type="application/json">{"lang":"fa","name":"…"}</script>
//     — زبان جاری کاربر برای انتخاب نویسه‌های باران (غایب = fa).
//   • فاز ۱۰۶ — settings (اختیاری) هم در همان JSON می‌آید: {"lang":..,"name":..,"settings":{…}}
//     — ۴ فیلد محافظ + کلیدهای دلخواه بسته (مثل speed/density)؛ غایب = رفتار پیش‌فرض.
//   • فاز ۱۰۶ — میزبان @font-face وزیرمتن (font-src data:) را به Runtime تزریق می‌کند؛
//     پس 'Vazirmatn' در زنجیرهٔ فونت اکنون در سندباکس هم واقعاً وزیر است و اتصال حروف
//     (شکل‌دهی) با جداول OpenType انجام می‌شود — ریشهٔ متن مقطع همین نبودن فونت بود.
//   • بدنهٔ سند خالی و تمام‌صفحه است؛ این کد خودش canvas می‌سازد.
//   • CSP میزبان connect-src 'none' است — این کد هیچ درخواست شبکه‌ای نمی‌زند.
//
// نکتهٔ فونت: در محیط سندباکس فونت وزیرمتنِ میزبان در دسترس نیست (مبدأ کدر)؛
// 'Vazirmatn' در زنجیرهٔ فونت حفظ شده تا اگر میزبان @font-face با font-src
// مجاز ارائه کند با وزیر رسم شود، وگرنه پشتیبان monospace (همان رفتار زبان‌های
// سیریلیک/گرجی در خود سامانه).
// جدول نویسه‌ها همگام با hr-screensaver.js (فاز ۱۰۱) نگه داشته می‌شود.
// ============================================================================
(function () {
    'use strict';
    // ---------- زبان جاری از دادهٔ میزبان (یا اتریبیوت html) ----------
    var lang = 'fa';
    try {
        var el = document.getElementById('hr-ss-data');
        if (el) {
            var d = JSON.parse(el.textContent || '{}');
            if (d && d.lang) lang = String(d.lang).toLowerCase();
        }
        if (!lang && document.documentElement.getAttribute('data-lang'))
            lang = document.documentElement.getAttribute('data-lang').toLowerCase();
    } catch (e) { }

    // ---------- کاراکترهای باران از زبان (همگام با فاز ۱۰۱) ----------
    var CHARSETS = {
        fa:  'اآبپتثجچحخدذرزژسشصضطظعغفقکگلمنوهی۰۱۲۳۴۵۶۷۸۹',
        prs: 'اآبپتثجچحخدذرزژسشصضطظعغفقکگلمنوهیېۀ۰۱۲۳۴۵۶۷۸۹',
        ps:  'ابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهیېۍړږټډښګڅځ۰۱۲۳۴۵۶۷۸۹',
        ar:  'ابتثجحخدذرزسشصضطظعغفقكلمنهويءة٠١٢٣٤٥٦٧٨٩',
        ru:  'абвгдежзийклмнопрстуфхцчшщъыьэюяё0123456789',
        ky:  'абвгдежзийклмнопрстуфхцчшщъыьэюяёңөү0123456789',
        tg:  'абвгдежзийклмнопрстуфхцчшщъыьэюяёқғҷҳӣӯ0123456789',
        tk:  'abdefghijklmnoprstuwyzçäňöşüýž0123456789',
        uz:  'abcdefghilmnopqrstuvxyz0123456789',
        ka:  'აბგდევზთიკლმნოპჟრსტუფქღყშჩცძწჭხჯჰ0123456789',
        en:  'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    };
    var CHARS = (CHARSETS[lang] || CHARSETS.en).split('');
    var fs = 16;

    // ---------- canvas تمام‌صفحه ----------
    var cv = document.createElement('canvas');
    cv.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;display:block;opacity:.85';
    document.body.appendChild(cv);
    var ctx = cv.getContext('2d');
    var cols = [], drops = [], raf = 0;

    function resize() {
        cv.width = window.innerWidth; cv.height = window.innerHeight;
        cols = Math.floor(cv.width / fs);
        drops = [];
        for (var i = 0; i < cols; i++) drops[i] = Math.floor(Math.random() * -100);
    }
    function frame() {
        ctx.fillStyle = 'rgba(3,7,10,0.09)';
        ctx.fillRect(0, 0, cv.width, cv.height);
        ctx.font = fs + 'px Vazirmatn, monospace';   // وزیرمتن اگر میزبان فراهم کند، وگرنه monospace
        for (var i = 0; i < cols; i++) {
            var ch = CHARS[Math.floor(Math.random() * CHARS.length)];
            var y = drops[i] * fs;
            ctx.fillStyle = Math.random() < 0.04 ? '#d1fae5' : '#22c55e';
            ctx.fillText(ch, i * fs, y);
            if (y > cv.height && Math.random() > 0.977) drops[i] = 0;
            drops[i]++;
        }
        raf = requestAnimationFrame(frame);
    }
    window.addEventListener('resize', resize);
    // پاک‌سازی اختیاری — اگر میزبان قرارداد را پشتیبانی کند
    window.__hrSsGuestCleanup = function () {
        if (raf) cancelAnimationFrame(raf);
        try { cv.remove(); } catch (e) { }
    };
    try { if (document.fonts && document.fonts.load) document.fonts.load(fs + 'px Vazirmatn'); } catch (e) { }
    resize();
    raf = requestAnimationFrame(frame);
})();
