// ============================================================================
// فاز ۹۶ (درخواست کاربر) — محافظ صفحهٔ «باران ماتریکس» + بیدارسازی با رمز
// ---------------------------------------------------------------------------
// ۱) پس از «N دقیقه + M ثانیه بی‌حرکتی» (تنظیمات سامانه — فاز ۹۸: ثانیه هم دارد؛ فاز ۱۰۰: دقیقهٔ صفر مجاز —
//    فعال‌شدن چندثانیه‌ای با کف ۵ ثانیه) افکت باران ماتریکس تمام‌صفحه روشن می‌شود.
// ۲) با اولین حرکت ماوس/کلید:
//      • حالت «بیدارسازی با رمز» (پیش‌فرض): پنل رمز وسط صفحه باز می‌شود و فقط با
//        تأیید رمز (POST /Auth/VerifyWakePassword) محافظ جمع می‌شود.
//      • حالت بدون رمز: با اولین حرکت بلافاصله جمع می‌شود.
// ۳) محاسبهٔ زمان — ملاک، اختلاف واقعی Date.now() از آخرین رخداد ورودی کاربر است
//    (نه شمارش تیک setInterval که در تب مخفی throttle می‌شود)؛ رخدادهای ساختگی
//    (isTrusted=false) هرگز «حرکت» حساب نمی‌شوند.
// پیکربندی: اتریبیوت‌های تگ <script> — data-ss-enabled / data-ss-minutes /
// data-ss-seconds (فاز ۹۸) / data-ss-require-pass (از تنظیمات سامانه در _Layout رندر می‌شود).
// فاز ۱۰۰ — پارس بولین case-insensitive (Razor قبلاً "True" رندر می‌کرد) + دقیقهٔ صفر مجاز.
// ---------------------------------------------------------------------------
// فاز ۱۰۱ (درخواست کاربر — با اسکرین‌شات):
//   ۱) کاراکترهای باران ماتریکس از «زبان انتخاب‌شدهٔ سامانه» می‌آیند — فارسی/دری/
//      پشتو ← حروف فارسی، روسی/قرقیزی/تاجیکی ← سیریلیک، عربی ← عربی، گرجی ← گرجی،
//      انگلیسی/ترکمنی/ازبکی ← لاتین (قبلاً کاتاکانای ثابت بود).
//   ۲) روی محافظ «فقط باران» دیده می‌شود + تاریخ/ساعت به‌صورت ریز در «گوشهٔ پایین-
//      چپ»؛ ساعت بزرگِ وسط و شمارندهٔ «مدت بی‌حرکتی» حذف شدند.
//   ۳) تاریخ/ساعت با ارقام بومی زبان جاری — حتی برای فارسی (۱۱:۴۳:۳۱)؛ نام ماه/روز
//      هم از Intl با لوکال زبان جاری (هم‌سو با تقویم نمایشی ادمین).
//   ۴) متن‌های پنل رمز (عنوان/placeholder/دکمه/خطاها) با دیکشنری سرور
//      (window.__hrL10n ← HrL10n.Keys) به زبان جاری ترجمه می‌شوند — قبلاً همیشه
//      فارسی می‌ماند.
//   ۵) پنل رمز با اولین حرکت ماوس/کلید «دقیقاً وسط صفحه» باز می‌شود (قبلاً پایین
//      کادر مرکزی چسبیده بود).
// ---------------------------------------------------------------------------
// فاز ۱۰۲ (درخواست کاربر — رفع دورزدن رمز با رفرش):
//   «کافیه صفحه رو رفرش کنه و رمز دور زده میشه!» — چون قفل فقط سمت مرورگر بود و
//   F5 شمارندهٔ بی‌حرکتی را صفر می‌کرد. مکانیسم جدید:
//   ۱) فعال‌شدن محافظ (حالت رمزدار) ← POST /Auth/ScreensaverLock ← پرچم قفل
//      سرورسمت به‌ازای نشست مرورگر (هش کوکی hr_at).
//   ۲) _Layout با همان پرچم data-ss-start-locked رندر می‌کند ← بلافاصله پس از هر
//      بارگذاری کامل صفحه (رفرش/تب جدید/ناوبری) محافظ قفل می‌شود و رمز می‌خواهد.
//   ۳) برداشتن پرچم فقط پس از تأیید رمز در /Auth/VerifyWakePassword (سمت سرور).
//   ۴) گارد دستکاری DOM: اگر اورلی از کنسول حذف شود، حداکثر تا ۱ ثانیه بعد دوباره
//      ساخته می‌شود و کلاس قفل body هم برمی‌گردد.
// ---------------------------------------------------------------------------
// فاز ۱۰۳ (درخواست کاربر):
//   ۱) نام شرکت + نام سامانه کنار تاریخ/ساعت گوشهٔ پایین-چپ نمایش داده می‌شود
//      (اتریبیوت‌های data-ss-company / data-ss-system از _Layout رندر می‌شوند).
//   ۲) پنل رمز اگر تا ۱۰ ثانیه کاربری روی آن نشود، دوباره خودش را پنهان می‌کند؛
//      با هر حرکت/کلید دوباره باز و تایمر از نو چیده می‌شود — قفل سرور دست‌نخورده می‌ماند.
//   ۳) گیت سرورسمت AJAX حین قفل (ScreensaverLockGate): هر درخواست غیرمعاف حین قفل 423 می‌گیرد؛
//      این اسکریپت fetch/XHR را رصد می‌کند و با دیدن 423 تبِ جاری هم بی‌درنگ قفل و رمزدار می‌شود
//      (هم‌سویی همهٔ تب‌های یک نشست بدون انتظار برای بی‌حرکتی).
// ---------------------------------------------------------------------------
// فاز ۱۰۴ (درخواست کاربر):
//   ۱) باران ماتریکس با فونت «وزیرمتن» (Vazirmatn — همان فونت خوداتکای رابط) رسم می‌شود؛
//      قبلاً monospace بود. ctx.font هر فرم دوباره ست می‌شود و به‌محض آماده‌شدن فونت
//      باران با وزیر رسم می‌شود؛ برای زبان‌هایی که نویسه‌شان در وزیرمتن نیست
//      (سیریلیک/گرجی/...) مرورگر به پشتیبان monospace برمی‌گردد.
//   ۲) برون‌بری/درون‌ریزی تنظیمات محافظ صفحه — دکمه‌ها در تب «محافظ صفحه» تنظیمات
//      سامانه (سمت سرور: Settings/ExportScreensaver و Settings/ImportScreensaver).
// ---------------------------------------------------------------------------
// فاز ۱۰۵ (درخواست کاربر — بسته‌های محافظ صفحه: انتقال «خودِ اسکرین‌سیور»):
//   اگر بستهٔ فعال وجود داشته باشد (_Layout: data-ss-package=true)، به‌جای باران داخلی یک
//   iframe سندباکس‌شدهٔ تمام‌صفحه با src=/Settings/ScreensaverRuntime داخل اورلی ساخته می‌شود:
//     • sandbox="allow-scripts" بدون allow-same-origin → مبدأ کدر؛ کد بسته به کوکی/نشست/DOM
//       والد دسترسی ندارد (بیدارسازی/رمز/گوشهٔ ساعت همچنان والد).
//     • iframe با pointer-events:none — وگرنه همهٔ رخدادهای ورودی را می‌بلعد و پنل رمز
//       هرگز باز نمی‌شود؛ سمت سرور هم Runtime با هدر CSP «sandbox allow-scripts» و متا
//       connect-src 'none' سرو می‌شود — کد بسته هیچ درخواست شبکه‌ای نمی‌تواند بزند.
// ============================================================================
(function () {
    'use strict';
    var scr = document.currentScript;
    if (!scr) return;
    function attrBool(name, dflt) {
        var v = (scr.getAttribute(name) || '').trim().toLowerCase();
        if (!v) return dflt;
        return v === 'true' || v === '1' || v === 'on';
    }
    function attrInt(name, dflt, min, max) {
        var n = parseInt(scr.getAttribute(name), 10);
        if (isNaN(n)) n = dflt;
        return Math.min(max, Math.max(min, n));
    }
    var ENABLED = attrBool('data-ss-enabled', false);
    var MINUTES = attrInt('data-ss-minutes', 5, 0, 120);
    var SECONDS = attrInt('data-ss-seconds', 0, 0, 59);
    var REQUIRE_PASS = attrBool('data-ss-require-pass', true);
    if (!ENABLED) return;
    // فاز ۱۰۵ — بستهٔ فعال محافظ صفحه؟ (کد غریبه فقط در iframe سندباکس اجرا می‌شود)
    var PACKAGE = attrBool('data-ss-package', false);
    // فاز ۱۰۳ — نام شرکت + نام سامانه برای نمایش کنار تاریخ/ساعت روی محافظ
    var BRAND = [ (scr.getAttribute('data-ss-company') || '').trim(),
                  (scr.getAttribute('data-ss-system') || '').trim() ]
                 .filter(function (s) { return !!s; }).join(' — ');
    // فاز ۱۰۲ — اگر سرور گفته «این نشست قفل است» (محافظ فعال شده و رمز هنوز تأیید نشده)،
    // همین صفحه که تازه بارگذاری شده (رفرش/تب جدید) باید همان اول قفل باشد و رمز بخواهد
    var START_LOCKED = attrBool('data-ss-start-locked', false);
    if (START_LOCKED) REQUIRE_PASS = true;

    // فاز ۹۸ — آستانهٔ واقعی = دقیقه × ۶۰ + ثانیه؛ فاز ۱۰۰ — دقیقه ۰ مجاز؛ کف آستانه ۵ ثانیه
    var THRESHOLD_MS = Math.max(5000, (MINUTES * 60 + SECONDS) * 1000);
    // ترجمه از دیکشنری سرور (کلید = متن فارسی مبدأ؛ غایب = خودِ فارسی)
    // فاز ۱۰۱ — کلیدها به HrL10n.Keys اضافه شدند تا در هر زبان ترجمه شوند
    function T(s) { try { return (window.__hrL10n && window.__hrL10n[s]) || s; } catch (e) { return s; } }
    var MSG = {
        locked: T('محافظ صفحه فعال است'),
        passWake: T('برای بازگشت به سامانه، رمز کاربر جاری را وارد کنید'),
        pass: T('رمز عبور'),
        ok: T('تأیید و بازگشت'),
        wrong: T('رمز نادرست است — دوباره تلاش کنید'),
        serverErr: T('خطا در ارتباط با سرور: '),
        checking: T('⏳ در حال بررسی…'),
        relogin: T('نشست منقضی شده است — در حال انتقال به صفحهٔ ورود…'),
    };

    // ---------- فاز ۱۰۱ — کاراکترهای باران ماتریکس از زبان جاری ----------
    var LANG = (document.documentElement.getAttribute('data-lang') || 'fa').trim().toLowerCase();
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
    var CHARS = (CHARSETS[LANG] || CHARSETS.en).split('');

    // ---------- ارقام بومی + تاریخ تقویم نمایشی (هم‌سو با پیکر تاریخ site.js) ----------
    var CAL = document.documentElement.getAttribute('data-cal') || 'jalali';
    var ADJ = CAL === 'hijri' ? (parseInt(document.documentElement.getAttribute('data-hijri') || '0', 10) || 0) : 0;
    if (ADJ > 2) ADJ = 2; if (ADJ < -2) ADJ = -2;
    function digits(s) {
        try { if (window.HrDigits && window.HrDigits.conv) return window.HrDigits.conv(s); } catch (e) { }
        return s;
    }
    function addDaysIso(iso, n) {
        var t = Date.UTC(+iso.slice(0, 4), +iso.slice(5, 7) - 1, +iso.slice(8, 10)) + n * 86400000;
        return new Date(t).toISOString().slice(0, 10);
    }
    // فاز ۱۰۱ — لوکال = زبان جاری؛ زنجیرهٔ امنیت: زبان جاری ← fa ← en (Intl بعضی لوکال‌ها را ندارد)
    var dateFmt = null;
    try {
        var calkey = CAL === 'hijri' ? 'islamic-umalqura' : (CAL === 'gregorian' ? 'gregory' : 'persian');
        var locs = [LANG];
        if (LANG !== 'fa' && LANG !== 'en') locs.push('fa');
        locs.push('en');
        for (var li = 0; li < locs.length && !dateFmt; li++) {
            try {
                dateFmt = new Intl.DateTimeFormat(locs[li] + '-u-ca-' + calkey, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric', timeZone: 'UTC' });
            } catch (e) { }
        }
    } catch (e) { }
    function todayDate() {
        try {
            var iso = new Date().toISOString().slice(0, 10);
            if (ADJ) iso = addDaysIso(iso, ADJ);
            if (dateFmt) return dateFmt.format(new Date(iso + 'T00:00:00Z'));
        } catch (e) { }
        return '';
    }

    // ---------- وضعیت ----------
    var lastActivity = Date.now();
    var active = false;
    var busy = false;          // قفل ارسال آجاکس رمز
    var pwHideTimer = 0;       // فاز ۱۰۳ — تایمر پنهان‌سازی خودکار پنل رمز
    var overlay = null, canvas = null, ctx = null, timeEl = null, dateEl = null,
        pwBox = null, pwInput = null, pwMsg = null;
    var rainRaf = 0, tickTimer = 0, cols = [], drops = [], fs = 16;

    // ---------- محاسبهٔ زمان (فاز ۹۶ — اختلاف واقعی، نه شمارش تیک) ----------
    function elapsedMs() { return Date.now() - lastActivity; }

    var lastEvt = 0;
    function activity(evt) {
        // فقط رخدادهای واقعی کاربر (رخداد ساختگیِ اسکریپت‌ها حرکت حساب نمی‌شود)
        if (evt && evt.isTrusted === false) return;
        lastActivity = Date.now();
        if (active) {
            // فاز ۹۶ — درخواست کاربر: با حرکت، بیدارسازی؛ فاز ۱۰۱ — در حالت رمزدار پنل رمزِ وسط صفحه باز می‌شود
            if (REQUIRE_PASS) showPw(); else wake();
            return;
        }
    }
    var EVTS = ['mousemove', 'mousedown', 'wheel', 'keydown', 'touchstart', 'touchmove', 'pointerdown'];
    for (var i = 0; i < EVTS.length; i++) document.addEventListener(EVTS[i], activity, { passive: true });
    document.addEventListener('scroll', activity, { passive: true, capture: true });
    window.addEventListener('resize', rainResize);   // فاز ۱۰۲ — یک‌بار برای همیشه (قبلاً هر فعال‌سازی تکرار می‌شد)
    // در بازگشت به تب/پنجره بلافاصله ارزیابی شود (interval تب مخفی throttle می‌شود)
    document.addEventListener('visibilitychange', function () { if (!document.hidden) check(); });
    window.addEventListener('focus', check);

    // فاز ۱۰۲ — قفل از سرور (پرچم ScreensaverLock): بلافاصله پس از بارگذاری صفحه قفل شو
    if (START_LOCKED) activate();

    function check() {
        if (!active) { if (elapsedMs() >= THRESHOLD_MS) activate(); return; }
        // فاز ۱۰۲ — گارد دستکاری DOM: اورلیِ حذف‌شده از کنسول حداکثر تا ۱ ثانیه بعد دوباره ساخته می‌شود
        if (!overlay || !overlay.isConnected) { active = false; activate(); return; }
        if (!document.body.classList.contains('hr-ss-lock')) document.body.classList.add('hr-ss-lock');
    }

    // ---------- باران ماتریکس (کاراکترها از زبان جاری — فاز ۱۰۱) ----------
    function rainResize() {
        if (!canvas) return;
        canvas.width = window.innerWidth; canvas.height = window.innerHeight;
        cols = Math.floor(canvas.width / fs);
        drops = [];
        for (var i = 0; i < cols; i++) drops[i] = Math.floor(Math.random() * -100);
    }
    function rainFrame() {
        if (!ctx) return;
        ctx.fillStyle = 'rgba(3,7,10,0.09)';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.font = fs + 'px Vazirmatn, monospace';   // فاز ۱۰۴ — فونت وزیر (درخواست کاربر)
        for (var i = 0; i < cols; i++) {
            var ch = CHARS[Math.floor(Math.random() * CHARS.length)];
            var y = drops[i] * fs;
            ctx.fillStyle = Math.random() < 0.04 ? '#d1fae5' : '#22c55e';
            ctx.fillText(ch, i * fs, y);
            if (y > canvas.height && Math.random() > 0.977) drops[i] = 0;
            drops[i]++;
        }
        rainRaf = requestAnimationFrame(rainFrame);
    }

    // ---------- ساخت لایهٔ محافظ — فاز ۱۰۱: فقط باران + تاریخ/ساعت ریزِ گوشهٔ پایین-چپ ----------
    function buildOverlay() {
        var d = document.createElement('div');
        d.className = 'hr-ss';
        d.setAttribute('role', 'dialog');
        d.setAttribute('aria-label', MSG.locked);
        d.innerHTML =
            (PACKAGE
                ? '<iframe class="hr-ss-frame" sandbox="allow-scripts" title="" src="/Settings/ScreensaverRuntime"></iframe>'
                : '<canvas class="hr-ss-cv"></canvas>') +
            '<div class="hr-ss-corner" dir="ltr">' +
              (BRAND ? '<div class="hr-ss-brand" dir="auto"></div>' : '') +
              '<div class="hr-ss-time"></div>' +
              '<div class="hr-ss-date" dir="auto"></div>' +
            '</div>' +
            '<form class="hr-ss-pw" hidden>' +
              '<div class="hr-ss-pw-title">' + MSG.passWake + '</div>' +
              '<input type="password" autocomplete="current-password" placeholder="' + MSG.pass + '" />' +
              '<button type="submit" class="hr-ss-btn">' + MSG.ok + '</button>' +
              '<div class="hr-ss-msg" hidden></div>' +
            '</form>';
        document.body.appendChild(d);
        return d;
    }

    // فاز ۱۰۱ — تاریخ/ساعت گوشهٔ پایین-چپ با ارقام بومی زبان جاری
    function tick() {
        if (!timeEl) return;
        var n = new Date();
        timeEl.textContent = digits(('0' + n.getHours()).slice(-2) + ':' + ('0' + n.getMinutes()).slice(-2) + ':' + ('0' + n.getSeconds()).slice(-2));
        dateEl.textContent = todayDate();
    }

    // فاز ۱۰۲ — گزارش قفل به سرور (فقط حالت رمزدار): پرچم به‌ازای نشست مرورگر ثبت می‌شود تا
    // رفرش/تب جدید/ناوبری هم قفل بماند؛ شکست شبکه‌ای بحرانی نیست (نبود پرچم = رفتار فاز ۱۰۱)
    function tokenOf() {
        try {
            var t = document.querySelector('input[name="__RequestVerificationToken"]');
            return t ? t.value : '';
        } catch (e) { return ''; }
    }
    function reportLock() {
        if (!REQUIRE_PASS) return;
        var token = tokenOf();
        try {
            // فاز ۱۰۶ — ثبت قفل هم با retry شبکه‌ای (idempotent؛ شکستش یعنی ضدرفرش فاز ۱۰۲ بی‌اثر می‌ماند)
            postJsonRetry('/Auth/ScreensaverLock', {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
                body: '__RequestVerificationToken=' + encodeURIComponent(token),
            }).catch(function () { });
        } catch (e) { }
    }

    function activate() {
        if (active) return;
        active = true;
        overlay = buildOverlay();
        canvas = overlay.querySelector('.hr-ss-cv');           // فاز ۱۰۵ — در حالت بسته canvas وجود ندارد
        ctx = canvas ? canvas.getContext('2d') : null;
        timeEl = overlay.querySelector('.hr-ss-time');
        dateEl = overlay.querySelector('.hr-ss-date');
        pwBox = overlay.querySelector('.hr-ss-pw');
        pwInput = overlay.querySelector('input');
        pwMsg = overlay.querySelector('.hr-ss-msg');
        var brandEl = overlay.querySelector('.hr-ss-brand');   // فاز ۱۰۳ — نام شرکت + نام سامانه
        if (brandEl) brandEl.textContent = BRAND;
        if (!PACKAGE) {
            // فاز ۱۰۴ — فونت وزیرمتن برای باران (درخواست کاربر): اگر هنوز بارگذاری نشده باشد
            // همین‌جا درخواست می‌شود؛ چون ctx.font در هر فرم دوباره ست می‌شود، به‌محض آماده‌شدن
            // فونت، باران با وزیرمتن رسم می‌شود (قبل از آن پشتیبان monospace — بدون خطا).
            try { if (document.fonts && document.fonts.load) document.fonts.load(fs + 'px Vazirmatn'); } catch (e) { }
            rainResize();
            rainRaf = requestAnimationFrame(rainFrame);
        }
        // حالت بسته (فاز ۱۰۵): باران داخلی رسم نمی‌شود — بسته در iframe سندباکس خودش را می‌کشد
        tickTimer = setInterval(tick, 500);
        tick();
        document.body.classList.add('hr-ss-lock');
        reportLock();   // فاز ۱۰۲ — قفل سرورسمت (رفع دورزدن با رفرش)
        // بیدارسازی/نمایش پنل رمز به‌طور یکجا در شنوندهٔ سراسری activity() انجام می‌شود
        pwBox.addEventListener('submit', submitPw);
        // رخدادهای درون لایه نباید «حرکت» زیرین شوند — جز بیدارسازی هدفمند بالا
        overlay.addEventListener('wheel', function (e) { e.preventDefault(); }, { passive: false });
    }

    // فاز ۱۰۱ — پنل رمز وسط صفحه (خودِ فرم، تنها فرزند جریانِ overlay است و flex آن را وسط می‌کند)
    // فاز ۱۰۳ — باز شدن پنل، تایمر پنهان‌سازی خودکار را هم می‌چیند؛ هر حرکت/کلید تایمر را از نو می‌اندازد
    var PW_IDLE_MS = 10000;   // «اگر تا چند ثانیه کاری نکرد، دوباره باکس رمز مخفی کن»
    function armPwHide() {
        if (pwHideTimer) clearTimeout(pwHideTimer);
        pwHideTimer = setTimeout(hidePw, PW_IDLE_MS);
    }
    function hidePw() {
        pwHideTimer = 0;
        if (!pwBox || pwBox.hidden) return;
        if (busy) { armPwHide(); return; }   // حین بررسی رمز پنهان نشود
        pwBox.hidden = true;
        if (pwMsg) pwMsg.hidden = true;
        try { pwInput.blur(); } catch (e) { }
    }
    function showPw() {
        if (!REQUIRE_PASS || !pwBox) return;
        if (pwBox.hidden) {
            pwBox.hidden = false;
            try { pwInput.focus({ preventScroll: true }); } catch (e) { }
        }
        armPwHide();
    }

    // فاز ۱۰۶ — رفع «خطا در ارتباط با سرور: NetworkError …» که تا ورود بارها تکرار می‌شد:
    // پس از idle محافظ، مرورگر گاهی POST را روی اتصال keep-alive کهنه (بسته‌شده از سمت
    // سرور/پروکسی) می‌فرستد و fetch با NetworkError می‌شکند؛ پنجرهٔ کوتاه ری‌استارت اپ هم
    // همین اثر را دارد. راه‌حل: خطاهای سطح شبکه تا ۳ بار با فاصلهٔ فزاینده (۵۰۰/۱۲۰۰/۲۵۰۰ms)
    // خودکار تلاش می‌شوند؛ پاسخ‌های HTTP (رمز نادرست، قفل تلاش‌ها، نشست منقضی) هرگز retry
    // نمی‌شوند و همان لحظه نشان داده می‌شوند.
    function postJsonRetry(url, opts) {
        var attempt = 0;
        function go() {
            return fetch(url, opts).then(function (r) { return r.json(); }).catch(function (err) {
                var net = err instanceof TypeError || (err && err.message && /network|failed to fetch|load failed/i.test(err.message));
                if (!net) throw err;
                attempt++;
                if (attempt > 3) throw err;
                return new Promise(function (res) { setTimeout(res, attempt === 1 ? 500 : attempt === 2 ? 1200 : 2500); }).then(go);
            });
        }
        return go();
    }

    function submitPw(e) {
        e.preventDefault();
        if (busy) return;
        var pwd = pwInput.value || '';
        if (!pwd) { pwFail(MSG.wrong); return; }
        busy = true;
        var btn = pwBox.querySelector('.hr-ss-btn');
        var old = btn.textContent; btn.textContent = MSG.checking; btn.disabled = true;
        var token = '';
        try {
            var t = document.querySelector('input[name="__RequestVerificationToken"]');
            token = t ? t.value : '';
        } catch (ex) { }
        postJsonRetry('/Auth/VerifyWakePassword', {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
            body: 'password=' + encodeURIComponent(pwd) + '&__RequestVerificationToken=' + encodeURIComponent(token),
        }).then(function (j) {
            busy = false; btn.textContent = old; btn.disabled = false;
            if (j && j.ok) { wake(); return; }
            if (j && j.relogin) { pwMsg.hidden = false; pwMsg.textContent = MSG.relogin; setTimeout(function () { location.href = '/Auth/Login'; }, 1200); return; }
            pwFail((j && j.message) || MSG.wrong);
        }).catch(function (err) {
            busy = false; btn.textContent = old; btn.disabled = false;
            pwFail(MSG.serverErr + (err && err.message ? err.message : ''));
        });
    }

    function pwFail(msg) {
        if (!pwMsg) return;
        pwMsg.hidden = false;
        pwMsg.textContent = msg;
        pwBox.classList.remove('shake'); void pwBox.offsetWidth; pwBox.classList.add('shake');
        try { pwInput.select(); } catch (e) { }
    }

    function wake() {
        if (!active) return;
        active = false;
        cancelAnimationFrame(rainRaf);
        clearInterval(tickTimer);
        if (pwHideTimer) { clearTimeout(pwHideTimer); pwHideTimer = 0; }   // فاز ۱۰۳
        if (overlay) { overlay.remove(); overlay = null; canvas = null; ctx = null; timeEl = null; dateEl = null; pwBox = null; pwInput = null; pwMsg = null; }
        document.body.classList.remove('hr-ss-lock');
        // نقطهٔ صفر بی‌حرکتی — پس از بیدارسازی دوباره از همین لحظه شمرده می‌شود (فاز ۹۶)
        lastActivity = Date.now();
    }

    // فاز ۱۰۳ — رصد 423 (گیت سرورسمت حین قفل): نشست قفل است ولی این تب هنوز اورلی ندارد
    // (تب دیگر همان نشست را قفل کرده) ← همین تب هم بی‌درنگ قفل و رمزدار می‌شود.
    // هر دو مسیر رایج آجاکس پوشش داده می‌شود: fetch و XMLHttpRequest (jQuery/pjax).
    function forceLock() {
        REQUIRE_PASS = true;   // سرور حاکم است: پرچم قفل فقط در حالت رمزدار ثبت می‌شود
        if (!active) activate(); else showPw();
    }
    (function watch423() {
        if (typeof window.fetch === 'function') {
            var of = window.fetch;
            window.fetch = function () {
                var p = of.apply(this, arguments);
                try { p.then(function (r) { try { if (r && r.status === 423) forceLock(); } catch (e) { } }).catch(function () { }); } catch (e) { }
                return p;
            };
        }
        try {
            var xo = XMLHttpRequest.prototype.open;
            XMLHttpRequest.prototype.open = function () {
                var xhr = this;
                try { xhr.addEventListener('load', function () { try { if (xhr.status === 423) forceLock(); } catch (e) { } }); } catch (e) { }
                return xo.apply(this, arguments);
            };
        } catch (e) { }
    })();

    // ارزیابی هر ثانیه + ارزیابی فوری هنگام بارگذاری (اگر از تب دیگر برگشته باشد)
    setInterval(check, 1000);
    check();
})();
