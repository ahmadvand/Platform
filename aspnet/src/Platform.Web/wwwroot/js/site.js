// ============ فاز ۹۲ — مترجم رشته‌های JS (دیکشنری سرور: window.__hrL10n) ============
// کلید = رشتهٔ فارسی مبدأ (همان قرارداد HrT.T سمت سرور). غایب = خود رشتهٔ فارسی (رفتار امن).
window.hrT = function (fa) {
    var d = window.__hrL10n;
    return (d && Object.prototype.hasOwnProperty.call(d, fa)) ? d[fa] : fa;
};
window.hrF = function (fa) {
    var s = window.hrT(fa);
    for (var i = 1; i < arguments.length; i++) s = s.split('{' + (i - 1) + '}').join(String(arguments[i] === null ? '' : arguments[i]));
    return s;
};

// ============ ساعت زنده + تاریخ جلالی (Intl) ============
(function () {
    var fmt;
    try {
        fmt = new Intl.DateTimeFormat('fa-IR-u-ca-persian-nu-latn',
            { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false });
    } catch (e) { fmt = null; }
    function tick() {
        var now = new Date();
        var s = fmt ? fmt.format(now) : now.toTimeString().slice(0, 8);
        // هر تیک دوباره جستجو می‌شود — بعد از ناوبری AJAX (تعویض main) ساعت جدید هم بلافاصله کار می‌کند
        document.querySelectorAll('[data-clock]').forEach(function (c) { c.textContent = s; });
    }
    tick();
    setInterval(tick, 1000);
})();

// ============ بستن خودکار پیام‌ها ============
window.bindToasts = function () {
    document.querySelectorAll('[data-toast]').forEach(function (t) {
        if (t.dataset.toastBound) return;   // دوبار بستن نشود (بعد از ناوبری AJAX)
        t.dataset.toastBound = '1';
        setTimeout(function () {
            t.style.transition = 'opacity .5s';
            t.style.opacity = '0';
            setTimeout(function () { t.remove(); }, 500);
        }, 4200);
    });
};
window.bindToasts();

// ============ دیالوگ‌های سفارشی (جای alert/confirm/prompt مرورگر) ============
var _dlgOverlay = null;
function _ensureDialog() {
    if (_dlgOverlay) return _dlgOverlay;
    _dlgOverlay = document.createElement('div');
    _dlgOverlay.className = 'dlg-overlay';
    _dlgOverlay.innerHTML =
        '<div class="dlg" role="dialog" aria-modal="true">' +
        '<div class="dlg-ico"></div>' +
        '<h3 class="dlg-title"></h3>' +
        '<div class="dlg-msg"></div>' +
        '<div class="dlg-actions"></div>' +
        '</div>';
    document.body.appendChild(_dlgOverlay);
    return _dlgOverlay;
}
function _closeDialog() { _dlgOverlay.classList.remove('on'); }

/**
 * uiDialog({icon, tone, title, message, buttons:[{text, value, cls}]}) → Promise<value|null>
 * پیام فقط متن ساده است (textContent — بدون ریسک XSS)؛ خط جدید با \n
 */
window.uiDialog = function (opts) {
    var ov = _ensureDialog();
    var ico = ov.querySelector('.dlg-ico');
    ico.textContent = opts.icon || '❔';
    ico.className = 'dlg-ico ' + (opts.tone || 'info');
    ov.querySelector('.dlg-title').textContent = opts.title || hrT('تأیید');
    var msg = ov.querySelector('.dlg-msg');
    msg.textContent = opts.message || '';
    msg.style.display = opts.message ? '' : 'none';
    var acts = ov.querySelector('.dlg-actions');
    acts.innerHTML = '';
    ov.classList.add('on');
    return new Promise(function (resolve) {
        var done = false;
        function finish(v) {
            if (done) return;
            done = true;
            _closeDialog();
            document.removeEventListener('keydown', esc);
            resolve(v);
        }
        function esc(ev) { if (ev.key === 'Escape') finish(null); }
        (opts.buttons || []).forEach(function (b) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn ' + (b.cls || 'btn-ghost');
            btn.textContent = b.text;
            btn.addEventListener('click', function () { finish(b.value); });
            acts.appendChild(btn);
        });
        document.addEventListener('keydown', esc);
        ov.onclick = function (e) { if (e.target === ov) finish(null); };
    });
};

/** uiConfirm({message, danger, okText, cancelText, title, icon}) → Promise<boolean> */
window.uiConfirm = function (opts) {
    if (typeof opts === 'string') opts = { message: opts };
    var danger = !!opts.danger;
    return window.uiDialog({
        icon: opts.icon || (danger ? '⚠️' : '❔'),
        tone: danger ? 'danger' : 'info',
        title: opts.title || (danger ? hrT('این عملیات حساس است') : hrT('تأیید عملیات')),
        message: opts.message,
        buttons: [
            { text: opts.okText || hrT('بله، انجام بده'), value: true, cls: danger ? 'btn-err' : 'btn-primary' },
            { text: opts.cancelText || hrT('انصراف'), value: false, cls: 'btn-ghost' }
        ]
    });
};

// ============ رهگیری submit: data-confirm و data-choices ============
document.addEventListener('submit', function (e) {
    var f = e.target;
    if (!(f instanceof HTMLFormElement) || f.dataset.dlgBusy) return;
    var confirmMsg = f.getAttribute('data-confirm');
    var choices = f.getAttribute('data-choices');
    if (!confirmMsg && !choices) return;

    e.preventDefault();
    e.stopPropagation();

    function doSubmit() {
        f.dataset.dlgBusy = '1';
        if (f.requestSubmit) f.requestSubmit(); else f.submit();
        setTimeout(function () { delete f.dataset.dlgBusy; }, 200);
    }

    if (choices) {
        // دیالوگ جابجایی واحد: با پرسنل / بدون پرسنل / انصراف
        var dept = f.getAttribute('data-dept') || hrT('این واحد');
        var count = parseInt(f.getAttribute('data-count') || '0', 10);
        var sel = f.querySelector('select[name="parentId"]');
        var dest = sel && sel.selectedIndex >= 0 ? sel.options[sel.selectedIndex].text.trim() : hrT('مقصد');
        var old = f.getAttribute('data-old') || hrT('محل قبلی');
        var buttons = [{ text: hrT('جابجایی همراه پرسنل'), value: 'with', cls: 'btn-primary' }];
        if (count > 0) buttons.push({ text: hrT('جابجایی بدون پرسنل'), value: 'leave', cls: 'btn' });
        buttons.push({ text: hrT('انصراف'), value: null, cls: 'btn-ghost' });
        window.uiDialog({
            icon: '⇄', tone: 'info',
            title: hrT('جابجایی واحد سازمانی'),
            message: hrF('واحد «{0}» به «{1}» منتقل شود؟\nپرسنل مستقیم این واحد: {2} نفر\n', dept, dest, count) +
                (count > 0 ? hrF('در حالت «بدون پرسنل»، این {0} نفر در «{1}» باقی می‌مانند.', count, old)
                           : hrT('این واحد پرسنل مستقیمی ندارد؛ زیرمجموعه‌ها و پرسنل آن‌ها همراه واحد می‌آیند.')),
            buttons: buttons
        }).then(function (v) {
            if (v === null) return;
            var inp = f.querySelector('input[name="leaveEmployees"]');
            if (inp) inp.value = (v === 'leave') ? 'true' : 'false';
            doSubmit();
        });
        return;
    }

    window.uiConfirm({
        message: confirmMsg,
        danger: f.getAttribute('data-danger') === '1'
    }).then(function (ok) { if (ok) doSubmit(); });
}, true);

// ============ پرکردن فرم ورود (حساب‌های نمایشی) — فرم واحد تسک ۲۵: identifier/secret ============
window.fillLogin = function (u, p) {
    var uu = document.getElementById('identifier') || document.getElementById('username');
    var pp = document.getElementById('secret') || document.getElementById('password');
    if (uu) uu.value = u;
    if (pp) pp.value = p;
};

// ============ نمایش/پنهان‌سازی پنل‌های فرم (delegated — بعد از تعویض درجا هم کار می‌کند) ============
document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-toggle]');
    if (!btn) return;
    var target = document.getElementById(btn.getAttribute('data-toggle'));
    if (target) target.style.display = target.style.display === 'none' ? 'block' : 'none';
});

// ============ Toast زنده برای پاسخ‌های AJAX (بدون رفرش صفحه) ============
window.toast = function (msg, tone) {
    var host = document.querySelector('.content') || document.body;
    var t = document.createElement('div');
    t.className = 'toast ' + (tone === 'err' ? 'toast-err' : tone === 'info' ? 'toast-info' : 'toast-ok');
    t.textContent = msg;
    host.prepend(t);
    t.addEventListener('click', function () { t.remove(); });
    setTimeout(function () {
        t.style.transition = 'opacity .5s';
        t.style.opacity = '0';
        setTimeout(function () { t.remove(); }, 500);
    }, 6000);
};

// ============ اعتبارسنجی native مرورگر — حباب پیام خودِ سامانه به زبان رابط کاربر (فاز ۸۳) ============
/**
 * حباب پیش‌فرض مرورگر («Please fill out this field») به زبانِ UI خودِ مرورگر نمایش داده
 * می‌شود نه زبان سامانه، و شکل آن هم با پوسته نمی‌خواند. از فاز ۸۳ رویداد invalid سرکوب
 * می‌شود و به‌جایش یک حباب سفارشی هم‌رنگ پوسته و به زبان رابط کاربر (lang تگ html — فاز ۷۶)
 * کنار فیلد نمایش داده می‌شود. فرم نامعتبر نه به‌صورت کلاسیک ارسال می‌شود نه data-ajax
 * (گیت submit فاز capture، پیش از همهٔ هندلرهای دیگر)؛ اولین فیلد نامعتبر فوکوس می‌گیرد.
 */
(function () {
    var LANG = (document.documentElement.getAttribute('lang') || 'fa').slice(0, 2).toLowerCase() === 'en' ? 'en' : 'fa';
    var MSG = {
        fa: {
            required: hrT('تکمیل این فیلد الزامی است'),
            email: hrT('نشانی ایمیل معتبر نیست'),
            url: hrT('نشانی وب معتبر نیست'),
            pattern: hrT('قالب مقدار واردشده معتبر نیست'),
            tooShort: hrT('مقدار واردشده خیلی کوتاه است'),
            tooLong: hrT('مقدار واردشده خیلی بلند است'),
            rangeUnderflow: hrT('مقدار از حداقل مجاز کمتر است'),
            rangeOverflow: hrT('مقدار از حداکثر مجاز بیشتر است'),
            step: hrT('مقدار خارج از گام مجاز است'),
            generic: hrT('مقدار واردشده معتبر نیست')
        },
        en: {
            required: 'This field is required',
            email: 'Invalid email address',
            url: 'Invalid URL',
            pattern: 'Invalid value format',
            tooShort: 'Value is too short',
            tooLong: 'Value is too long',
            rangeUnderflow: 'Value is below the minimum',
            rangeOverflow: 'Value is above the maximum',
            step: 'Value is off the allowed step',
            generic: 'Invalid value'
        }
    };
    var m = MSG[LANG];

    var _bubble = null;
    function ensureBubble() {
        if (_bubble) return _bubble;
        _bubble = document.createElement('div');
        _bubble.className = 'hr-val-bubble';
        _bubble.setAttribute('role', 'alert');
        document.body.appendChild(_bubble);
        return _bubble;
    }
    function hideBubble() {
        if (_bubble) _bubble.classList.remove('on');
    }
    // جابه‌جایی حباب — fixed با مختصات viewport؛ در RTL لبهٔ راست فیلد ملاک است
    function showBubble(el, msg) {
        var b = ensureBubble();
        b.textContent = msg;
        b.classList.add('on');
        var r = el.getBoundingClientRect();
        var rtl = (document.documentElement.getAttribute('dir') || 'rtl').toLowerCase() === 'rtl';
        b.style.top = Math.round(r.bottom + 6) + 'px';
        if (rtl) {
            b.style.left = 'auto';
            b.style.right = Math.max(8, Math.round(window.innerWidth - r.right)) + 'px';
        } else {
            b.style.right = 'auto';
            b.style.left = Math.max(8, Math.round(r.left)) + 'px';
        }
        // لبهٔ پایین پنجره؟ حباب بالای فیلد بیفتد
        if (r.bottom + 14 + b.offsetHeight > window.innerHeight && r.top - b.offsetHeight - 6 > 0) {
            b.style.top = Math.round(r.top - b.offsetHeight - 6) + 'px';
        }
    }
    function msgFor(el) {
        var v = el.validity, out = m.generic;
        if (v.valueMissing) out = m.required;
        else if (v.typeMismatch) out = el.type === 'email' ? m.email : (el.type === 'url' ? m.url : m.generic);
        else if (v.patternMismatch) out = m.pattern;
        else if (v.tooShort) out = m.tooShort;
        else if (v.tooLong) out = m.tooLong;
        else if (v.rangeUnderflow) out = m.rangeUnderflow;
        else if (v.rangeOverflow) out = m.rangeOverflow;
        else if (v.stepMismatch) out = m.step;
        return out;
    }

    // ۱) نقطهٔ درستِ رهگیری — رویداد invalid (فاز ۸۳ب):
    // مرورگر فرمِ نامعتبر را «قبل از» انتشار submit رد می‌کند (اعتبارسنجی تعاملی)؛
    // پس گیت submit (پایین) برای فرم نامعتبر هرگز اجرا نمی‌شد و حباب خودِ سامانه
    // هیچ‌وقت دیده نمی‌شد — فقط حباب بومی سرکوب می‌شد و کاربر بدون هیچ پیامی
    // می‌ماند. رویداد invalid تنها نشانهٔ مطمئنِ «تلاش ارسالِ فرم نامعتبر» است:
    // حباب بومی همین‌جا preventDefault و به‌جایش حباب خود سامانه (به زبان UI) در
    // فریم بعد نمایش داده می‌شود. اولین فیلد نامعتبرِ هر تلاش، ملاک است.
    var _pendingEl = null;
    document.addEventListener('invalid', function (e) {
        e.preventDefault();                     // سرکوب حباب پیش‌فرض مرورگر
        if (_pendingEl) return;                 // همین تلاش قبلاً برنامه‌ریزی شده
        _pendingEl = e.target;
        requestAnimationFrame(function () {
            var el = _pendingEl; _pendingEl = null;
            // validity.valid (نه checkValidity — آن رویداد invalid دوباره می‌فرستد و حلقه می‌سازد)
            if (el && !el.validity.valid) {
                try { el.focus({ preventScroll: false }); } catch (_) { try { el.focus(); } catch (_) { } }
                showBubble(el, msgFor(el));
            }
        });
    }, true);

    // ۲) گیت ارسال — تور ایمنی: اگر به هر دلیلی submitِ فرم نامعتبر منتشر شد
    // (مثلاً فراخوانی برنامه‌ای)، به هیچ روشی ارسال نمی‌شود
    document.addEventListener('submit', function (e) {
        var f = e.target;
        if (!(f instanceof HTMLFormElement) || f.noValidate) { hideBubble(); return; }
        if (f.checkValidity()) { hideBubble(); return; }
        e.preventDefault();
        e.stopImmediatePropagation();
        var el = f.querySelector(':invalid');
        if (el) {
            // اول فوکوس (ممکن است اسکرول کند)، بعد جای‌گذاری حباب در فریم بعد
            try { el.focus(); } catch (_) { }
            requestAnimationFrame(function () { showBubble(el, msgFor(el)); });
        }
    }, true);

    // ۳) پنهان‌سازی حباب با اصلاح فیلد / اسکرول / تغییر اندازهٔ پنجره
    ['input', 'change'].forEach(function (ev) {
        document.addEventListener(ev, hideBubble, true);
    });
    window.addEventListener('scroll', hideBubble, true);
    window.addEventListener('resize', hideBubble);
})();

// ============ ارسال AJAX فرم‌ها (data-ajax) — بدون رفرش + Toast ============
/**
 * فرم را با fetch ارسال می‌کند؛ پاسخ JSON {ok, message} → toast.
 * submitter: دکمه فشرده‌شده (برای formaction و فیلد اضافه).
 * گزینه‌های فرم: data-ajax-redirect="/Devices" → در موفقیت redirect سمت کاربر.
 */
window.ajaxSubmit = function (f, submitter) {
    if (f.dataset.ajaxBusy) return Promise.resolve(null);
    var btns = Array.prototype.slice.call(f.querySelectorAll('button'));
    f.dataset.ajaxBusy = '1';
    btns.forEach(function (b) { b.disabled = true; b.classList.add('btn-busy'); });
    var url = (submitter && submitter.getAttribute('formaction')) ? submitter.getAttribute('formaction') : f.action;
    var body = new FormData(f);
    if (submitter && submitter.name) body.append(submitter.name, submitter.value);
    return fetch(url, {
        method: 'POST',
        body: body,
        credentials: 'same-origin',
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
    }).then(function (r) {
        if (!r.ok) throw new Error('HTTP ' + r.status);
        return r.json();
    }).then(function (j) {
        // سرور می‌تواند tone سفارشی بفرستد (مثلاً اسکن غیرمجاز: رویداد ثبت شد ولی قرمز نمایش داده شود)
        var tone = j.tone || (j.ok ? 'ok' : 'err');
        window.toast(j.message || (j.ok ? hrT('انجام شد') : hrT('خطا')), tone);
        if (j.ok && f.getAttribute('data-ajax-redirect')) {
            setTimeout(function () { window.location.href = f.getAttribute('data-ajax-redirect'); }, 700);
            return j;
        }
        if (j.ok) {
            // مودال والد؟ بسته شود
            var ov = f.closest('.modal-overlay');
            if (ov && ov._close) ov._close();
            // تعویض درجا بخش داده‌محور صفحه (بدون رفرش)
            var rf = f.getAttribute('data-refresh');
            if (rf !== null) window.refreshHost(rf || null);
        }
        // به‌روزرسانی بخش‌های زنده صفحه (لاگ دستورات و …) — رویداد سراسری با جزئیات پاسخ
        document.dispatchEvent(new CustomEvent('hraj:refresh', { detail: j }));
        return j;
    }).catch(function (ex) {
        window.toast(hrT('ارسال ناموفق بود: ') + ex.message, 'err');
    }).finally(function () {
        delete f.dataset.ajaxBusy;
        btns.forEach(function (b) { b.disabled = false; b.classList.remove('btn-busy'); });
    });
};

// رهگیری submit فرم‌های data-ajax — فاز capture، قبل از ارسال کلاسیک
document.addEventListener('submit', function (e) {
    var f = e.target;
    if (!(f instanceof HTMLFormElement) || !f.hasAttribute('data-ajax')) return;
    if ((f.hasAttribute('data-confirm') || f.hasAttribute('data-choices')) && !f.dataset.dlgBusy) {
        // دیالوگ تأیید صاحب این رویداد است — پس از تأیید کاربر، submit با dlgBusy دوباره می‌رسد
        return;
    }
    if (f.dataset.ajaxBusy) return;
    e.preventDefault();
    e.stopPropagation();
    window.ajaxSubmit(f, e.submitter);
}, true);

// ============ رصد زنده دستگاه‌ها و دستورات — «تا نتیجه برسد» ============
/**
 * poll دوره‌ای GET وضعیت زنده + اطلاع‌رسانی تغییر وضعیت دستورها با toast:
 *   در صف → تحویل شد → اجرا شد / ناموفق (با نتیجه واقعی دستگاه)
 * opts: { url, intervalSec, apply(j), types? }
 */
window.liveCommands = function (opts) {
    var known = null; // Map: commandId → status (اولین poll مبناست؛ toast فقط برای تغییرها)
    function poll() {
        if (document.hidden) return;
        fetch(opts.url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (j) {
                if (!j) return;
                var cmds = j.commands || [];
                if (opts.types) cmds = cmds.filter(function (c) { return opts.types.indexOf(c.type) >= 0; });
                if (known === null) {
                    known = {};
                    cmds.forEach(function (c) { known[c.id] = c.status; });
                } else {
                    cmds.forEach(function (c) {
                        var prev = known[c.id];
                        if (prev && prev !== c.status) {
                            var label = c.typeLabel || c.type || '';
                            if (c.status === 'delivered')
                                window.toast(hrF('⏳ دستور «{0}» به دستگاه «{1}» تحویل شد — در انتظار اجرا…', label, (c.device || '')), 'info');
                            else if (c.status === 'acked')
                                window.toast(hrF('✅ «{0}» روی «{1}» اجرا شد', label, (c.device || '')) + (c.result ? ' — ' + hrT(c.result) : ''), 'ok');
                            else if (c.status === 'failed')
                                window.toast(hrF('⚠️ «{0}» روی «{1}» ناموفق بود', label, (c.device || '')) + (c.result ? ' — ' + hrT(c.result) : ''), 'err');
                        }
                        known[c.id] = c.status;
                    });
                }
                if (opts.apply) opts.apply(j);
            })
            .catch(function () { /* خطای گذرا — چرخه بعدی */ });
    }
    poll();
    var timer = setInterval(poll, Math.max(6, opts.intervalSec || 12) * 1000);
    // ثبت برای پاکسازی هنگام ناوبری AJAX (pjax) — پولر صفحه قبلی زنده نمی‌ماند
    (window.hrajTimers = window.hrajTimers || []).push(timer);
    // بعد از هر فرم AJAX موفق، وضعیت تازه‌تر بیاید
    document.addEventListener('hraj:refresh', function () { setTimeout(poll, 500); });
};

// ============ تعویض درجا بخش داده‌محور صفحه (بدون رفرش کامل) ============
/**
 * بخش مشخص‌شده (پیش‌فرض [data-refresh-host]) را با نسخه تازه همان URL عوض می‌کند.
 * توکن‌های antiforgery داخل بخش هم تازه می‌شوند (از پاسخ سرور).
 * بعد از تعویض، hraj:swapped منتشر می‌شود (برای اسکریپت‌های وابسته به صفحه).
 */
window.refreshHost = function (sel) {
    var host = sel ? document.querySelector(sel) : document.querySelector('[data-refresh-host]');
    if (!host) return Promise.resolve(false);
    return fetch(window.location.pathname + window.location.search, {
        credentials: 'same-origin',
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
    })
        .then(function (r) { return r.ok ? r.text() : null; })
        .then(function (html) {
            if (!html) return false;
            var doc = new DOMParser().parseFromString(html, 'text/html');
            var fresh = sel ? doc.querySelector(sel) : doc.querySelector('[data-refresh-host]');
            if (!fresh) return false;
            host.innerHTML = fresh.innerHTML;
            document.dispatchEvent(new CustomEvent('hraj:swapped', { detail: { selector: sel || '[data-refresh-host]' } }));
            return true;
        })
        .catch(function () { return false; });
};

// ============ مودال فرم‌ها — «فرم در پاپ‌آپ + AJAX» ============
/**
 * فرم صفحه‌محور را در پاپ‌آپ باز می‌کند: fetch(url + partial=1) → رندر → ثبت همان‌جا با ajaxSubmit.
 * در موفقیت: مودال خودش بسته می‌شود، toast می‌آید و با data-refresh لیست تعویض درجا می‌شود.
 * opts: { url, title }
 */
window.uiModal = function (opts) {
    opts = opts || {};
    var ov = document.createElement('div');
    ov.className = 'modal-overlay';
    ov.innerHTML =
        '<div class="modal" role="dialog" aria-modal="true">' +
        '<div class="modal-h"><h3></h3><button type="button" class="modal-x" aria-label="' + hrT('بستن') + '">✕</button></div>' +
        '<div class="modal-b"><div class="modal-loading">' + hrT('در حال بارگذاری…') + '</div></div>' +
        '</div>';
    document.body.appendChild(ov);
    var b = ov.querySelector('.modal-b');
    ov.querySelector('h3').textContent = opts.title || hrT('فرم');

    // ---------- فاز ۵۱ — ماندگاری اندازهٔ پاپ‌آپ ----------
    // اندازه‌ای که کاربر با دستهٔ resize می‌کشد، برای همین عنوان/فرم در localStorage
    // می‌ماند و دفعهٔ بعد همان اندازه باز می‌شود؛ خطا (حالت خصوصی و…) نادیده گرفته می‌شود.
    var modal = ov.querySelector('.modal');
    var sizeKey = 'hrModalSize:' + (opts.title || opts.url || '');
    try {
        var saved = JSON.parse(localStorage.getItem(sizeKey) || 'null');
        if (saved && saved.w >= 320 && saved.h >= 160) {
            modal.style.width = Math.min(saved.w, window.innerWidth - 32) + 'px';
            modal.style.height = saved.h + 'px';
        }
    } catch (e) { }
    var ro = null, roTimer = null;
    if ('ResizeObserver' in window) {
        ro = new ResizeObserver(function () {
            clearTimeout(roTimer);
            roTimer = setTimeout(function () {
                try {
                    var r = modal.getBoundingClientRect();
                    if (r.width >= 320 && r.height >= 120)
                        localStorage.setItem(sizeKey, JSON.stringify({ w: Math.round(r.width), h: Math.round(r.height) }));
                } catch (e) { }
            }, 350);
        });
        ro.observe(modal);
    }

    function close() {
        document.removeEventListener('keydown', esc);
        if (ro) { clearTimeout(roTimer); ro.disconnect(); }
        ov.classList.remove('on');
        setTimeout(function () { ov.remove(); }, 200);
    }
    function esc(e) { if (e.key === 'Escape') close(); }
    ov._close = close;
    ov.querySelector('.modal-x').addEventListener('click', close);
    ov.addEventListener('click', function (e) { if (e.target === ov) close(); });
    document.addEventListener('keydown', esc);
    requestAnimationFrame(function () { ov.classList.add('on'); });

    var url = opts.url + (opts.url.indexOf('?') >= 0 ? '&' : '?') + 'partial=1';
    fetch(url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.text(); })
        .then(function (html) {
            b.innerHTML = html;
            // innerHTML اسکریپت‌ها را اجرا نمی‌کند — بازاجرایی
            b.querySelectorAll('script').forEach(function (s) {
                var n = document.createElement('script');
                if (s.src) n.src = s.src;
                else n.textContent = s.textContent;
                s.replaceWith(n);
            });
            var t = b.querySelector('[data-modal-title]');
            if (t) ov.querySelector('h3').textContent = t.getAttribute('data-modal-title');
            var first = b.querySelector('input:not([type=hidden]):not([disabled]), select, textarea');
            if (first) first.focus();
        })
        .catch(function (ex) {
            b.innerHTML = '<div class="modal-loading">' + hrT('بارگذاری فرم ناموفق بود — ') + ex.message + '</div>';
        });
    return { overlay: ov, close: close };
};

// دکمه‌های data-modal — باز کردن فرم در پاپ‌آپ (delegated)
document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-modal]');
    if (!btn) return;
    e.preventDefault();
    window.uiModal({ url: btn.getAttribute('data-modal'), title: btn.getAttribute('data-modal-title') || '' });
});

// دکمه انصراف داخل مودال (delegated)
// در مودال: مودال بسته می‌شود؛ خارج از مودال (فرم صفحه‌کامل): مسیر data-goto طی می‌شود (فاز ۴۲)
document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-close-modal]');
    if (!btn) return;
    e.preventDefault();
    var ov = btn.closest('.modal-overlay');
    if (ov && ov._close) { ov._close(); return; }
    var goto = btn.getAttribute('data-goto');
    if (goto) {
        if (window.pjax && window.pjax.load) window.pjax.load(goto, true);
        else location.href = goto;
    }
});

// ============ نوار بالایی (فاز ۴۵) — زنگ اعلان + جستجوی سراسری ============
// نوار داخل main است و pjax آن را تعویض می‌کند — پس از هر ناوبری دوباره اتصال می‌یابد.
(function () {
    function initTopbar() {
        var bellBtn = document.getElementById('tbBellBtn');
        var bellMenu = document.getElementById('tbBellMenu');
        var search = document.getElementById('tbSearch');
        var searchMenu = document.getElementById('tbSearchMenu');
        if (!bellBtn || bellBtn.dataset.tbBound) return;
        bellBtn.dataset.tbBound = '1';

        bellBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            bellMenu.hidden = !bellMenu.hidden;
            if (!bellMenu.hidden && searchMenu) searchMenu.hidden = true;
            // فاز ۵۲ — باز کردن یکی، دیگری بسته شود
            var um = document.getElementById('tbUserMenu');
            if (!bellMenu.hidden && um) { um.hidden = true; var ub = document.getElementById('tbProfileBtn'); if (ub) ub.setAttribute('aria-expanded', 'false'); }
        });
        if (bellMenu) {
            bellMenu.addEventListener('click', function (e) { e.stopPropagation(); });
        }

        // ============ فاز ۷۶ — سوئیچ زبان رابط کاربری (🌐 در نوار بالایی) ============
        var langBtn = document.getElementById('tbLangBtn');
        var langMenu = document.getElementById('tbLangMenu');
        if (langBtn && langMenu && !langBtn.dataset.langBound) {
            langBtn.dataset.langBound = '1';
            langBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                langMenu.hidden = !langMenu.hidden;
                langBtn.setAttribute('aria-expanded', langMenu.hidden ? 'false' : 'true');
                if (!langMenu.hidden) {
                    if (bellMenu) bellMenu.hidden = true;
                    if (searchMenu) searchMenu.hidden = true;
                    var um2 = document.getElementById('tbUserMenu');
                    if (um2) um2.hidden = true;
                }
            });
            langMenu.addEventListener('click', function (e) { e.stopPropagation(); });
        }

        // ============ منوی کاربر در نوار بالایی (فاز ۵۲) ============
        // کلیک روی نام/آواتار → دراپ‌داون (عکس پروفایل من / تغییر رمز عبور / خروج)؛
        // آیتم‌های پاپ‌آپی با data-modal باز می‌شوند (delegated در پایین‌تر) و منو بسته می‌شود.
        var userBtn = document.getElementById('tbProfileBtn');
        var userMenu = document.getElementById('tbUserMenu');
        if (userBtn && userMenu && !userBtn.dataset.userBound) {
            userBtn.dataset.userBound = '1';
            userBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                userMenu.hidden = !userMenu.hidden;
                userBtn.setAttribute('aria-expanded', userMenu.hidden ? 'false' : 'true');
                if (!userMenu.hidden) {
                    if (bellMenu) bellMenu.hidden = true;
                    if (searchMenu) searchMenu.hidden = true;
                }
            });
            userMenu.addEventListener('click', function (e) {
                // آیتم پاپ‌آپ (عکس/رمز) → منو بسته شود؛ مودال با دستگیرهٔ delegated data-modal
                // روی document باز می‌شود — پس اینجا stopPropagation ممنوع (رویداد باید برسد).
                if (e.target.closest('[data-modal]')) {
                    userMenu.hidden = true;
                    userBtn.setAttribute('aria-expanded', 'false');
                }
            });
        }

        // ============ جمع‌کنندهٔ منو روی خود سایدبار (فاز ۵۳ — جایگزین ☰ نوار بالا) ============
        // دکمهٔ #sbToggle بیرون از main است (pjax آن را تعویض نمی‌کند) و یک‌بار متصل می‌شود؛
        // وضعیت در localStorage می‌ماند تا پس از pjax/رفرش هم حفظ شود؛
        // سایدبار خارج از ظرف pjax است پس کلاس روی body در کل ناوبری می‌ماند.
        var sideBtn = document.getElementById('sbToggle');
        if (sideBtn && !sideBtn.dataset.sideBound) {
            sideBtn.dataset.sideBound = '1';
            sideBtn.addEventListener('click', function () {
                var on = document.body.classList.toggle('side-collapsed');
                sideBtn.setAttribute('aria-pressed', on ? 'true' : 'false');
                try { localStorage.setItem('hrSideCollapsed', on ? '1' : '0'); } catch (e) { }
            });
            sideBtn.setAttribute('aria-pressed', document.body.classList.contains('side-collapsed') ? 'true' : 'false');
        }

        // ============ کمبوباکس جستجوی سراسری (فاز ۴۶) ============
        // صفحه‌های مجاز کاربر به‌صورت محلی (از #tbNavIndex) + نتایج زندهٔ داده از /Home/Search.
        // ناوبری کامل کیبورد: ↑↓ حرکت، Enter انتخاب، Esc بستن — با ویژگی‌های ARIA برای صفحه‌خوان.
        var timer = null, items = [], activeIdx = -1;
        if (search && searchMenu) {
            function navIdx() {
                try {
                    var el = document.getElementById('tbNavIndex');
                    return el ? JSON.parse(el.textContent) : [];
                } catch (e) { return []; }
            }
            function norm(s) {
                return String(s || '')
                    .replace(/[\u200c\u200f\u200e]/g, '')
                    .replace(/ي/g, 'ی').replace(/ك/g, 'ک').replace(/ة/g, 'ه')
                    .replace(/[۰-۹]/g, function (c) { return String.fromCharCode(c.charCodeAt(0) - 0x06F0 + 48); })
                    .replace(/[٠-٩]/g, function (c) { return String.fromCharCode(c.charCodeAt(0) - 0x0660 + 48); })
                    .toLowerCase().trim();
            }
            function collect() { items = [].slice.call(searchMenu.querySelectorAll('a.tb-s-item')); activeIdx = -1; }
            function setActive(i, scroll) {
                if (!items.length) return;
                if (activeIdx >= 0 && items[activeIdx]) items[activeIdx].classList.remove('on');
                activeIdx = (i + items.length) % items.length;
                items[activeIdx].classList.add('on');
                search.setAttribute('aria-activedescendant', items[activeIdx].id);
                if (scroll) items[activeIdx].scrollIntoView({ block: 'nearest' });
            }
            function open(html, emptyMsg) {
                if (html) {
                    searchMenu.innerHTML = html;
                    collect();
                    searchMenu.hidden = false;
                    search.setAttribute('aria-expanded', 'true');
                } else {
                    searchMenu.innerHTML = emptyMsg ? '<div class="tb-s-empty">' + emptyMsg + '</div>' : '';
                    items = [];
                    searchMenu.hidden = false;
                    search.setAttribute('aria-expanded', 'true');
                }
            }
            function close() {
                searchMenu.hidden = true;
                search.setAttribute('aria-expanded', 'false');
            }
            function pageGroups(q) {
                var nq = norm(q), out = [];
                (navIdx() || []).forEach(function (g) {
                    var hits = (g.items || []).filter(function (it) {
                        return norm(it.l).indexOf(nq) >= 0 || norm(g.t).indexOf(nq) >= 0;
                    }).slice(0, 8);
                    if (hits.length) out.push({
                        title: g.t, icon: '⌘',
                        items: hits.map(function (it) { return { text: (it.ic || '') + ' ' + it.l, href: it.h }; })
                    });
                });
                return out;
            }
            function render(groups, pending) {
                var html = '', n = 0;
                (groups || []).forEach(function (g) {
                    html += '<div class="tb-s-group">' + (g.icon || '') + ' ' + (g.title || '') + '</div>';
                    (g.items || []).forEach(function (it) {
                        html += '<a class="tb-s-item" id="tb-s-o' + n + '" role="option" href="' + it.href + '">' + it.text + '</a>';
                        n++;
                    });
                });
                if (pending && !n) { open('<div class="tb-s-loading">' + hrT('در حال جستجو…') + '</div>'); return; }
                open(html, hrT('نتیجه‌ای یافت نشد'));
            }
            function queryRemote(q) {
                fetch('/Home/Search?q=' + encodeURIComponent(q), {
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                }).then(function (r) { return r.ok ? r.json() : null; }).then(function (j) {
                    if (!j || search.value.trim() !== q) return; // پاسخ قدیمی — پرسش عوض شده
                    // ادغام: صفحه‌های محلی همیشه بالای نتایج داده می‌مانند (فاز ۴۶)
                    render(pageGroups(q).concat(j.groups || []), false);
                }).catch(function () { /* خطای گذرا — نتایج محلی سر جایشان می‌مانند */ });
            }
            search.addEventListener('input', function () {
                clearTimeout(timer);
                var q = search.value.trim();
                if (q.length < 2) { close(); return; }
                var local = pageGroups(q);
                render(local, !local.length);             // صفحه‌های منطبق فوری نمایش داده می‌شوند
                timer = setTimeout(function () { queryRemote(q); }, 260);
            });
            search.addEventListener('keydown', function (e) {
                if (e.key === 'ArrowDown') {
                    e.preventDefault();
                    if (searchMenu.hidden) {
                        var q = search.value.trim();
                        if (q.length >= 2) { render(pageGroups(q), true); queryRemote(q); return; }
                        // دسترسی سریع — صفحه‌های منو بدون تایپ
                        var quick = (navIdx() || []).map(function (g) {
                            return {
                                title: g.t, icon: '⌘',
                                items: (g.items || []).slice(0, 6).map(function (it) { return { text: (it.ic || '') + ' ' + it.l, href: it.h }; })
                            };
                        }).slice(0, 4);
                        render(quick, false);
                    } else setActive(activeIdx + 1, true);
                } else if (e.key === 'ArrowUp') {
                    e.preventDefault();
                    if (!searchMenu.hidden) setActive(activeIdx - 1, true);
                } else if (e.key === 'Enter') {
                    if (!searchMenu.hidden && items.length) {
                        e.preventDefault();
                        var pick = items[activeIdx >= 0 ? activeIdx : 0];
                        close();
                        if (window.pjax && window.pjax.load) window.pjax.load(pick.getAttribute('href'), true);
                        else location.href = pick.getAttribute('href');
                    }
                } else if (e.key === 'Escape') {
                    close(); search.blur();
                }
            });
            search.addEventListener('focus', function () {
                if (!searchMenu.hidden) return;
                if (search.value.trim().length >= 2 && searchMenu.children.length) {
                    searchMenu.hidden = false;
                    search.setAttribute('aria-expanded', 'true');
                }
            });
            // انتخاب با موس → بستن (کلیک خودش توسط pjax مدیریت می‌شود)
            searchMenu.addEventListener('click', function (e) {
                if (e.target.closest('a.tb-s-item')) close();
            });
        }

        // کلیک بیرون — بستن همهٔ دراپ‌داون‌ها (اعلان‌ها، جستجو، منوی کاربر — فاز ۵۲ + زبان — فاز ۷۶)
        document.addEventListener('click', function (e) {
            if (bellMenu && !e.target.closest('.tb-bell-wrap')) bellMenu.hidden = true;
            if (langMenu && !e.target.closest('.tb-lang-wrap')) { langMenu.hidden = true; langBtn && langBtn.setAttribute('aria-expanded', 'false'); }
            if (searchMenu && !e.target.closest('.tb-search-wrap')) searchMenu.hidden = true;
            if (userMenu && !e.target.closest('.tb-user-wrap')) {
                userMenu.hidden = true;
                var ub = document.getElementById('tbProfileBtn');
                if (ub) ub.setAttribute('aria-expanded', 'false');
            }
        });
    }
    initTopbar();
    document.addEventListener('hraj:navigated', initTopbar);
    // بازیابی وضعیت ☰ منو (یک‌بار در بارگذاری — پیش از رندر بعدی برای جلوگیری از پرش)
    try { if (localStorage.getItem('hrSideCollapsed') === '1') document.body.classList.add('side-collapsed'); } catch (e) { }
})();

// ============ منوی فعال (فاز ۵۱) — همگام‌سازی سایدبار با مسیر جاری ============
// سایدبار خارج از ظرف pjax است و با ناوبری AJAX عوض نمی‌شود؛ تا پیش از این کلاس
// active روی همان آیتم اولیه می‌ماند (مثلاً «لیست حقوق» برای همهٔ زیرمنوهای حقوق و
// دستمزد، یا «دستگاه‌های تردد» برای «نمای زنده حراست»). این تابع پس از هر بارگذاری
// و ناوبری، آیتم متناظر با مسیر جاری را مشخص می‌کند:
//   ۱) تطبیق دقیق href با مسیر جاری؛  ۲) در نبود آن (صفحات فرعی مثل ItemEdit)،
//   آیتمی با بیشترین اشتراک مسیر (همان بخش اول کنترلر) انتخاب می‌شود.
(function () {
    function updateNavActive() {
        var items = [].slice.call(document.querySelectorAll('.nav-item'));
        if (!items.length) return;
        var path = location.pathname.replace(/\/+$/, '') || '/';
        items.forEach(function (a) { a.classList.remove('active'); });

        var hrefOf = function (el) {
            var h = el.getAttribute('href') || '';
            try { return new URL(h, location.origin).pathname.replace(/\/+$/, '') || '/'; } catch (e) { return h; }
        };
        var best = items.filter(function (a) { return hrefOf(a) === path; })[0];
        if (!best) {
            var seg = function (p) { return p.split('/').filter(Boolean); };
            var cur = seg(path);
            var score = function (h) {
                var s = seg(h), n = 0;
                while (n < s.length && n < cur.length && s[n] === cur[n]) n++;
                return n;
            };
            var max = 0;
            items.forEach(function (a) {
                var s = score(hrefOf(a));
                if (s > max) { max = s; }
            });
            if (max > 0) {
                var cands = items.filter(function (a) { return score(hrefOf(a)) === max; });
                // در حالت تساوی، آیتمی که کمترین بخش مسیر اضافه دارد (نزدیک‌ترین)
                best = cands.sort(function (a, b) {
                    return seg(hrefOf(a)).length - seg(hrefOf(b)).length;
                })[0];
            }
        }
        if (best) best.classList.add('active');
    }
    updateNavActive();
    document.addEventListener('hraj:navigated', updateNavActive);
})();

// ============ فاز ۱۳۰ — جمع‌شو/بازشو گروه‌های منوی کناری ============
// هر گروه سایدبار یک دکمهٔ عنوان دارد؛ وضعیت باز/بسته در localStorage («hrNavGroupsCollapsed»)
// می‌ماند تا پس از pjax/رفرش حفظ شود. سایدبار خارج از ظرف pjax است پس اتصال یک‌بار انجام
// می‌شود؛ فقط گروهِ آیتمِ صفحهٔ فعال پس از هر ناوبری خودکار باز می‌شود تا جای صفحه گم نشود.
(function () {
    var KEY = 'hrNavGroupsCollapsed';
    function readState() { try { return JSON.parse(localStorage.getItem(KEY) || '{}') || {}; } catch (e) { return {}; } }
    function writeState(s) { try { localStorage.setItem(KEY, JSON.stringify(s)); } catch (e) { } }
    function expandActiveGroup() {
        var act = document.querySelector('.nav .nav-item.active');
        if (!act) return;
        var sec = act.closest('.nav-sec');
        if (!sec) return;
        var k = sec.getAttribute('data-nav-key') || '';
        var st = readState();
        if (st[k]) { delete st[k]; writeState(st); }
        sec.removeAttribute('data-collapsed');
        var btn = sec.querySelector('.nav-group');
        if (btn) btn.setAttribute('aria-expanded', 'true');
    }
    [].slice.call(document.querySelectorAll('.nav-sec')).forEach(function (sec) {
        var k = sec.getAttribute('data-nav-key') || '';
        var st = readState();
        if (st[k]) sec.setAttribute('data-collapsed', '1');
        var btn = sec.querySelector('.nav-group');
        if (!btn) return;
        btn.setAttribute('aria-expanded', sec.hasAttribute('data-collapsed') ? 'false' : 'true');
        btn.addEventListener('click', function () {
            var willCollapse = !sec.hasAttribute('data-collapsed');
            var s2 = readState();
            if (willCollapse) { sec.setAttribute('data-collapsed', '1'); s2[k] = 1; }
            else { sec.removeAttribute('data-collapsed'); delete s2[k]; }
            btn.setAttribute('aria-expanded', willCollapse ? 'false' : 'true');
            writeState(s2);
        });
    });
    expandActiveGroup();
    document.addEventListener('hraj:navigated', expandActiveGroup);
})();

// ============ به‌روزرسانی زندهٔ آواتار نوار بالایی (فاز ۵۱ — پروفایل من) ============
// عکس پروفایل «خودِ کاربر» در پاپ‌آپ /Account/Photo ذخیره می‌شود؛ چون نوار بالایی
// خارج از ظرف pjax است، پس از موفقیت فرم (پاسخ شامل فیلد photo) آواتار همین‌جا
// تعویض می‌شود: photo = مسیر عکس → <img>، photo = null → آواتار حرفی.
// فاز ۵۲ — همهٔ آواتارهای خوشهٔ کاربر (دکمهٔ نوار + سربرگ دراپ‌داون) با هم تعویض می‌شوند.
(function () {
    function makeAv(photo, name) {
        if (photo) {
            var img = document.createElement('img');
            img.className = 'tb-av';
            img.src = photo + '?v=' + Date.now();
            img.alt = '';
            return img;
        }
        var txt = document.createElement('div');
        txt.className = 'tb-av tb-av-txt';
        txt.textContent = (name || '؟').charAt(0);
        return txt;
    }
    function updateTopbarAvatar(photo) {
        var wrap = document.querySelector('.tb-user-wrap');
        if (!wrap) return;   // نوار قدیمی/صفحهٔ ورود — کاری نکن
        var btn = document.getElementById('tbProfileBtn');
        var nameEl = btn ? btn.querySelector('.tb-pname') : null;
        var name = nameEl ? nameEl.textContent.trim() : '';
        var avs = [].slice.call(wrap.querySelectorAll('.tb-av'));
        if (!avs.length) return;
        var fresh = makeAv(photo, name);
        avs[0].replaceWith(fresh);
        for (var i = 1; i < avs.length; i++) {
            var c = fresh.cloneNode(true);
            if (c.tagName === 'IMG') c.src = photo + '?v=' + Date.now();
            avs[i].replaceWith(c);
        }
    }
    document.addEventListener('hraj:refresh', function (e) {
        try {
            var d = e.detail;
            if (d && d.ok && Object.prototype.hasOwnProperty.call(d, 'photo'))
                updateTopbarAvatar(d.photo);
        } catch (err) { }
    });
})();

// ============ کامپوننت پلاک ایرانی (فاز ۴۵) — هم‌قرارداد با PlateFmt سمت سرور ============
// فاز ۵۱ — پارسر کانونیال: از هر قالبی («36 ن 436 ایران 11»، «36ن436ایران11»،
// «36 436 ایران ن 11»، «ایران 11 36 ن 436») اجزای استاندارد پلاک بازسازی می‌شود تا
// حرف وسط پلاک دیگر داخل کادر «ایران» نمایش داده نشود (باگ «ایران ن11»).
window.plateHtml = function (text, mini) {
    var raw = (text == null || text === '' ? '—' : String(text)).trim();
    var esc = function (s) { return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); };
    var lat = function (s) {
        return s.replace(/[۰-۹]/g, function (c) { return String.fromCharCode(c.charCodeAt(0) - 0x06F0 + 48); })
                .replace(/[٠-٩]/g, function (c) { return String.fromCharCode(c.charCodeAt(0) - 0x0660 + 48); });
    };
    var IRAN = hrT('ایران');
    var hasIran = raw.indexOf(IRAN) >= 0;

    // ---- پارسر کانونیال (هم‌قرارداد با PlateFmt.Parse سمت سرور) ----
    function parsePlate(src) {
        var s = lat(src);
        var pre = s, post = '';
        var idx = s.indexOf(IRAN);
        if (idx >= 0) { pre = s.slice(0, idx); post = s.slice(idx + IRAN.length); }
        var tokens = function (t) {
            var out = [], m, re = /([0-9]+)|([\u0600-\u06FF\u200C]+)/g;
            while ((m = re.exec(t)) !== null) out.push({ d: /[0-9]/.test(m[0][0]), t: m[0] });
            return out;
        };
        var province = null, letterPost = null, postDigits = [];
        tokens(post).forEach(function (tk) {
            if (tk.d) { postDigits.push(tk.t); if (province === null) province = tk.t; }
            else if (letterPost === null) letterPost = tk.t.replace(/\u200C/g, '');
        });
        var seq = tokens(pre);
        if (letterPost) seq.push({ d: false, t: letterPost });
        for (var i = 1; i < postDigits.length; i++) seq.push({ d: true, t: postDigits[i] });
        var li = -1, di = [];
        seq.forEach(function (tk, k) { if (tk.d) di.push(k); else if (li < 0) li = k; });
        if (li >= 0 && di.length >= 2) {
            var before = di.filter(function (k) { return k < li; });
            var after = di.filter(function (k) { return k > li; });
            var left = null, right = null;
            if (before.length > 0 && after.length > 0) { left = seq[before[before.length - 1]].t; right = seq[after[0]].t; }
            else if (before.length >= 2 && after.length === 0) { var pr = pickPair(before.map(function (k) { return seq[k].t; })); left = pr[0]; right = pr[1]; }
            else if (after.length >= 2 && before.length === 0) { var pr2 = pickPair(after.map(function (k) { return seq[k].t; })); left = pr2[0]; right = pr2[1]; }
            if (left && right && /^[0-9]{1,2}$/.test(left) && /^[0-9]{2,4}$/.test(right))
                return { left: left, letter: seq[li].t.replace(/\u200C/g, ''), right: right, province: province || '' };
        }
        return null;
    }
    function pickPair(nums) {
        var left = nums.filter(function (n) { return n.length <= 2; })[0] || nums[0];
        var right = nums.filter(function (n) { return n.length >= 2 && n.length <= 4 && n !== left; }).pop() || nums[nums.length - 1];
        return left === right ? ['', ''] : [left, right];
    }

    function mainHtml(m) {
        var p = parsePlate(raw);
        if (p) {
            return '<span class="pr-tok">' + esc(p.left) + '</span>' +
                   '<span class="pr-tok pr-l">' + esc(p.letter) + '</span>' +
                   '<span class="pr-tok">' + esc(p.right) + '</span>';
        }
        m = lat(m).replace(/\s+/g, '');
        var out = '', mm;
        var re = /([0-9]+)|([\u0600-\u06FF\u200C]+)/g;
        while ((mm = re.exec(m)) !== null) {
            out += '<span class="pr-tok' + (mm[1] ? '' : ' pr-l') + '">' + esc(mm[0]) + '</span>';
        }
        return out || '<span class="pr-tok">' + esc(raw) + '</span>';
    }

    var h = '<span class="plate-real' + (mini ? ' mini' : '') + '">';
    var mainRaw = raw, region = '';
    var idx0 = raw.indexOf(IRAN);
    if (idx0 >= 0) { mainRaw = raw.slice(0, idx0).trim(); region = raw.slice(idx0 + IRAN.length).trim(); }
    h += '<span class="pr-main" dir="ltr">' + mainHtml(mainRaw) + '</span>';
    if (hasIran) {
        var p2 = parsePlate(raw);
        var code = p2 && p2.province ? p2.province : lat(region).replace(/\s+/g, '').replace(/[^\u0600-\u06FF0-9]/g, '');
        h += '<span class="pr-region" dir="rtl"><span class="pr-iran">' + hrT('ایران') + '</span>' +
             '<span class="pr-code">' + esc(code) + '</span></span>';
    }
    return h + '</span>';
};

// ==================================================================
// ناوبری AJAX (pjax) — رفع باگ «منو با هر کلیک به بالا برمی‌گردد»
// ------------------------------------------------------------------
// تا پیش از این، کلیک روی آیتم‌های منو بارگذاری کامل صفحه بود و DOM
// سایدبار از نو ساخته می‌شد → اسکرول منو صفر می‌شد. حالا کلیک‌های
// داخلی با fetch گرفته می‌شوند، فقط «main» (بخش چپ) + اسکریپت‌های
// صفحه تعویض می‌شود و سایدبار دست‌نخورده می‌ماند → اسکرول منو حفظ
// می‌شود و محتوا از طریق AJAX بارگذاری می‌شود.
// ==================================================================
window.pjax = (function () {
    var shell = document.querySelector('body > aside');
    if (!shell) return { load: function () { location.href = arguments[0]; } }; // صفحه لاگین — بدون پوسته

    if ('scrollRestoration' in history) history.scrollRestoration = 'manual';

    var FILE_RE = /\.(pdf|xlsx|xls|docx?|zip|csv|png|jpe?g|gif|svg|ico|woff2?)(\?|$)/i;
    var ctrl = null;

    function saveScroll() {
        try { sessionStorage.setItem('hraj:menuScroll', String(shell.scrollTop || 0)); } catch (e) { }
    }
    function restoreScroll() {
        try {
            var v = parseInt(sessionStorage.getItem('hraj:menuScroll') || '0', 10);
            if (v > 0) shell.scrollTop = v;
        } catch (e) { }
    }
    restoreScroll(); // پس از بارگذاری کامل (POST کلاسیک/رفرش) جای منو برمی‌گردد
    window.addEventListener('pagehide', saveScroll);
    window.addEventListener('beforeunload', saveScroll);

    function isActiveMatch(a) { return a.classList.contains('active'); }

    function setActive(href) {
        var path = href.replace(/^https?:\/\/[^/]+/, '');
        var items = document.querySelectorAll('.nav-item');
        items.forEach(function (a) { a.classList.remove('active'); });
        var best = null;
        items.forEach(function (a) {
            var h = a.getAttribute('href') || '';
            if (h === path) best = a;
        });
        if (!best) {
            // تطبیق جزئی‌تر: مسیر یکسان با کوئری متفاوت (مثلاً /Access?tab=…)
            var basePath = path.split('?')[0];
            items.forEach(function (a) {
                var h = (a.getAttribute('href') || '').split('?')[0];
                if (h === basePath && basePath !== '/') best = a;
            });
        }
        if (best) {
            best.classList.add('active');
            // اسکرول منو دست نمی‌خورد — دقیقاً همان درخواست کاربر
        }
    }

    function runScripts(container) {
        container.querySelectorAll('script').forEach(function (s) {
            var n = document.createElement('script');
            if (s.src) n.src = s.src; else n.textContent = s.textContent;
            s.replaceWith(n);
        });
    }

    function cleanupPage() {
        (window.hrajTimers || []).forEach(function (t) { clearInterval(t); });
        window.hrajTimers = [];
    }

    function load(url, push) {
        if (ctrl) ctrl.abort();
        ctrl = new AbortController();
        document.body.classList.add('pjax-loading');
        return fetch(url, {
            credentials: 'same-origin',
            headers: { 'X-Pjax': '1' },
            signal: ctrl.signal,
        }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.text();
        }).then(function (html) {
            var doc = new DOMParser().parseFromString(html, 'text/html');
            var newMain = doc.querySelector('body > main');
            var newAside = doc.querySelector('body > aside');
            // نشست تمام شده یا پاسخ غیرمنتظره → بارگذاری کامل
            if (!newAside || !newMain) { location.href = url; return; }
            cleanupPage();
            document.querySelector('body > main').replaceWith(newMain);
            var psOld = document.getElementById('pageScripts');
            var psNew = doc.getElementById('pageScripts');
            if (psNew) {
                if (psOld) psOld.replaceWith(psNew); else document.body.appendChild(psNew);
                runScripts(document.getElementById('pageScripts') || psNew);
            }
            document.title = doc.title || document.title;
            var activeLink = doc.querySelector('.nav-item.active');
            setActive(activeLink ? activeLink.getAttribute('href') : url);
            window.scrollTo(0, 0);
            if (push !== false) { try { history.pushState({ pjax: 1 }, '', url); } catch (e) { } }
            window.bindToasts();
            document.dispatchEvent(new CustomEvent('hraj:navigated', { detail: { url: url } }));
        }).catch(function (ex) {
            if (ex && ex.name === 'AbortError') return;
            location.href = url; // هر خطایی → مسیر مطمئن: بارگذاری کامل
        }).finally(function () {
            document.body.classList.remove('pjax-loading');
        });
    }

    function shouldInterceptLink(a, e) {
        if (!a || e.defaultPrevented) return false;
        if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return false;
        if (a.target && a.target !== '_self') return false;
        if (a.hasAttribute('download')) return false;
        if (a.closest('[data-modal]') || a.closest('[data-toggle]') || a.hasAttribute('data-no-pjax')) return false;
        var href = a.getAttribute('href') || '';
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return false;
        if (a.origin !== location.origin) return false;
        if (FILE_RE.test(href)) return false;
        if (a.pathname.startsWith('/api/')) return false;
        return true;
    }

    document.addEventListener('click', function (e) {
        var a = e.target.closest('a');
        if (!shouldInterceptLink(a, e)) return;
        e.preventDefault();
        saveScroll();
        load(a.href, true);
    });

    // فرم‌های GET (فیلترها/جستجو) هم بدون رفرش کامل — POSTها سرجایشان هستند
    document.addEventListener('submit', function (e) {
        var f = e.target;
        if (!(f instanceof HTMLFormElement)) return;
        if ((f.method || 'get').toLowerCase() !== 'get') return;
        if (f.hasAttribute('data-no-pjax') || f.target) return;
        if (f.dataset.ajaxBusy) return;
        e.preventDefault();
        saveScroll();
        var qs = new URLSearchParams(new FormData(f)).toString();
        var url = f.getAttribute('action') || location.pathname;
        url = url.split('?')[0] + (qs ? '?' + qs : '');
        load(url, true);
    }, true);

    window.addEventListener('popstate', function () {
        if (document.querySelector('body > aside')) { saveScroll(); load(location.href, false); }
    });

    return { load: load, saveScroll: saveScroll, restoreScroll: restoreScroll };
})();

// ============================================================================
// فاز ۸۹ (درخواست کاربر) — ارقام بومی بر اساس زبان رابط کاربری
// ---------------------------------------------------------------------------
// فارسی/دری/پشتو → ارقام فارسی (۰-۹)؛ عربی → ارقام عربی-هندی (٠-٩)؛
// انگلیسی/گرجی/قرقیزی/ازبکی/تاجیکی/ترکمنی → لاتین (بدون تغییر).
// فقط «متنِ» صفحه تبدیل می‌شود؛ attributeها، input/textarea و کدها (code/pre)
// دست‌نخورده می‌مانند تا binding سرور، اثر انگشت سخت‌افزار، محتوای مجوز و
// پارامترهای URL هرگز خراب نشوند (صفر ریسک روی داده).
// ============================================================================
window.HrDigits = (function () {
    var MAPS = { fa: '۰۱۲۳۴۵۶۷۸۹', prs: '۰۱۲۳۴۵۶۷۸۹', ps: '۰۱۲۳۴۵۶۷۸۹', ar: '٠١٢٣٤٥٦٧٨٩' };
    var target = MAPS[document.documentElement.getAttribute('data-lang') || ''] || null;
    var LATIN = '0123456789';
    function conv(x) {
        // فاز ۸۹ — ورودی می‌تواند عدد هم باشد (پیکر تاریخ) — همیشه به رشته تبدیل شود
        var s = (x === null || x === undefined) ? '' : String(x);
        if (!target || s === '') return s;
        var out = '', ch;
        for (var i = 0; i < s.length; i++) {
            ch = LATIN.indexOf(s[i]);
            out += ch >= 0 ? target[ch] : s[i];
        }
        return out;
    }
    var SKIP = { SCRIPT: 1, STYLE: 1, CODE: 1, PRE: 1, TEXTAREA: 1, NOSCRIPT: 1 };
    function ok(node) {
        var v = node.nodeValue;
        if (!v || v.indexOf('0') < 0 && v.indexOf('1') < 0 && v.indexOf('2') < 0 && v.indexOf('3') < 0 &&
            v.indexOf('4') < 0 && v.indexOf('5') < 0 && v.indexOf('6') < 0 && v.indexOf('7') < 0 &&
            v.indexOf('8') < 0 && v.indexOf('9') < 0) return false;
        var p = node.parentNode;
        while (p && p.nodeType === 1) {
            if (SKIP[p.nodeName]) return false;
            if (p.getAttribute && p.getAttribute('data-no-digits') === '1') return false;
            p = p.parentNode;
        }
        return true;
    }
    function walk(root) {
        if (!target || !root || root.nodeType !== 1) return;
        try {
            var w = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null), n, batch = [];
            while ((n = w.nextNode())) if (ok(n)) batch.push(n);
            for (var i = 0; i < batch.length; i++) batch[i].nodeValue = conv(batch[i].nodeValue);
        } catch (e) { }
    }
    var pending = null;
    function schedule(roots) {
        if (pending) return;
        pending = requestAnimationFrame(function () {
            pending = null;
            for (var i = 0; i < roots.length; i++) walk(roots[i]);
        });
    }
    function start() {
        walk(document.body);
        try {
            new MutationObserver(function (muts) {
                var roots = [];
                for (var i = 0; i < muts.length; i++) {
                    var m = muts[i];
                    if (m.type === 'characterData' && m.target && m.target.parentNode) roots.push(m.target.parentNode);
                    for (var j = 0; j < m.addedNodes.length; j++) {
                        var a = m.addedNodes[j];
                        if (a.nodeType === 1) roots.push(a);
                        else if (a.nodeType === 3 && a.parentNode) roots.push(a.parentNode);
                    }
                }
                if (roots.length) schedule(roots);
            }).observe(document.body, { childList: true, subtree: true, characterData: true });
        } catch (e) { }
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
    return { conv: conv, walk: walk };
})();

// ============================================================================
// فاز ۸۹ (درخواست کاربر) — انتخابگر تاریخ هم‌سو با «نوع تقویم» تنظیمات
// ---------------------------------------------------------------------------
// وقتی تقویم نمایشی شمسی یا قمری است، input[type=date] به پیکر همان تقویم تبدیل
// می‌شود (میلادی = پیکر native مرورگر). مقدار ارسالی به سرور همیشه ISO
// (yyyy-MM-dd) می‌ماند — در input hidden هم‌نام — تا binding و کوئری‌ها سالم بمانند.
// نام ماه/روز هفته از Intl با locale زبان جاری گرفته می‌شود (فارسی/پشتو/گرجی/…).
// ============================================================================
(function () {
    var CAL = document.documentElement.getAttribute('data-cal') || 'jalali';
    var LANG = document.documentElement.getAttribute('data-lang') || 'fa';
    if (CAL === 'gregorian') return; // میلادی: input[type=date] native مناسب است
    var CALKEY = CAL === 'hijri' ? 'islamic-umalqura' : 'persian';
    // فاز ۹۶/۱۱۰ — جبران روز تقویم قمری (تنظیمات سامانه؛ data-hijri از سرور می‌آید و همان
    // HijriAdjust سمت سرور است): مبنای ICU «ام‌القری» نسبت به تقویم رسمی ایران/افغانستان یک روز
    // اختلاف دارد؛ data-hijri=-1 (پیش‌فرض محصول) یعنی تاریخ ISO یک روز کم شود و بعد ام‌القری
    // خوانده شود — دقیقاً همان قاعدهٔ سمت سرور (HijriAdj). به این ترتیب پیکر و نمایش سرور همیشه
    // روی یک تاریخ واحد (تقویم دیواری رسمی) می‌افتند.
    var ADJ = CAL === 'hijri' ? (parseInt(document.documentElement.getAttribute('data-hijri') || '0', 10) || 0) : 0;
    if (ADJ > 2) ADJ = 2; if (ADJ < -2) ADJ = -2;
    var LOCALES = { fa: 'fa', prs: 'fa', ps: 'ps', ka: 'ka', ky: 'ky', uz: 'uz', tg: 'tg', tk: 'tk', en: 'en', ar: 'ar' };
    var loc = LOCALES[LANG] || 'fa';
    var partsFmt, titleFmt, wdFmt;
    try {
        partsFmt = new Intl.DateTimeFormat('en-u-ca-' + CALKEY, { year: 'numeric', month: 'numeric', day: 'numeric', timeZone: 'UTC' });
        titleFmt = new Intl.DateTimeFormat(loc + '-u-ca-' + CALKEY, { month: 'long', year: 'numeric', timeZone: 'UTC' });
        wdFmt = new Intl.DateTimeFormat(loc, { weekday: 'narrow', timeZone: 'UTC' });
    } catch (e) { return; }

    function addDays(iso, n) {
        var t = Date.UTC(+iso.slice(0, 4), +iso.slice(5, 7) - 1, +iso.slice(8, 10)) + n * 86400000;
        return new Date(t).toISOString().slice(0, 10);
    }
    function todayIso() { return new Date().toISOString().slice(0, 10); }
    function calParts(iso) {
        // فاز ۹۶ — تاریخ قمری همیشه با جبران ادمین خوانده می‌شود (جابجایی ثابت روز — جستجوی isoOf همچنان همگرا می‌ماند)
        var p = partsFmt.formatToParts(new Date((ADJ ? addDays(iso, ADJ) : iso) + 'T00:00:00Z')), o = {};
        p.forEach(function (x) { if (x.type !== 'literal') o[x.type] = parseInt(x.value, 10); });
        return { y: o.year, m: o.month, d: o.day };
    }
    // تاریخ تقویمی (y/m/d در تقویم جاری) → ISO میلادی — با گام‌زدن محدود و همگرا
    function isoOf(y, m, d) {
        var iso = todayIso();
        for (var i = 0; i < 80; i++) {
            var p = calParts(iso);
            var dm = (y - p.y) * 12 + (m - p.m);
            if (dm !== 0) { iso = addDays(iso, Math.round(dm * 29.6)); continue; }
            return addDays(iso, d - p.d);
        }
        return iso;
    }
    function monthLength(y, m) {
        var ny = m >= 12 ? y + 1 : y, nm = m >= 12 ? 1 : m + 1;
        return Math.round((Date.parse(isoOf(ny, nm, 1) + 'T00:00:00Z') - Date.parse(isoOf(y, m, 1) + 'T00:00:00Z')) / 86400000);
    }
    function pad(n) { return (n < 10 ? '0' : '') + n; }
    function fmtText(iso) {
        var p = calParts(iso || todayIso());
        return window.HrDigits ? window.HrDigits.conv(p.y + '/' + pad(p.m) + '/' + pad(p.d)) : (p.y + '/' + pad(p.m) + '/' + pad(p.d));
    }
    function weekdayLabels() {
        var rtl = document.documentElement.dir === 'rtl';
        var start = rtl ? 6 : 0; // شنبه‌محور برای RTL، یکشنبه‌محور برای LTR
        var out = [];
        for (var k = 0; k < 7; k++)
            out.push(wdFmt.format(new Date(addDays('2024-01-07', start + k) + 'T00:00:00Z'))); // 2024-01-07 یکشنبه است
        return out;
    }

    var pop = null, cur = null;
    function close() { if (pop) { pop.remove(); pop = null; } cur = null; }
    document.addEventListener('click', function (e) { if (pop && !pop.contains(e.target) && e.target !== cur) close(); }, true);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') close(); });

    function open(input, hidden) {
        if (cur === input && pop) return;
        close();
        cur = input;
        var p = calParts(hidden.value || todayIso());
        pop = document.createElement('div');
        pop.className = 'hr-dp';
        pop.setAttribute('role', 'dialog');
        document.body.appendChild(pop);
        render(p.y, p.m, hidden);
        var r = input.getBoundingClientRect();
        pop.style.top = (r.bottom + window.scrollY + 6) + 'px';
        var left = r.left + window.scrollX;
        var max = window.scrollX + document.documentElement.clientWidth - pop.offsetWidth - 8;
        pop.style.left = Math.max(8, Math.min(left, max)) + 'px';
    }

    function render(y, m, hidden) {
        var sel = hidden.value || '';
        var today = todayIso();
        var len = monthLength(y, m);
        var firstIso = isoOf(y, m, 1);
        var firstDow = new Date(firstIso + 'T00:00:00Z').getUTCDay();
        var rtl = document.documentElement.dir === 'rtl';
        var lead = rtl ? (firstDow + 1) % 7 : firstDow;
        var D = window.HrDigits ? window.HrDigits.conv : function (s) { return s; };
        var h = '<div class="hr-dp-h"><button type="button" class="hr-dp-nav" data-nav="-1">‹</button>' +
            '<b>' + titleFmt.format(new Date((ADJ ? addDays(firstIso, ADJ) : firstIso) + 'T00:00:00Z')) + '</b>' +
            '<button type="button" class="hr-dp-nav" data-nav="1">›</button></div><div class="hr-dp-g">';
        weekdayLabels().forEach(function (w) { h += '<span class="hr-dp-w">' + w + '</span>'; });
        for (var i = 0; i < lead; i++) h += '<span></span>';
        for (var d = 1; d <= len; d++) {
            var iso = addDays(firstIso, d - 1);
            h += '<button type="button" data-iso="' + iso + '" class="hr-dp-d' +
                (iso === sel ? ' sel' : '') + (iso === today ? ' today' : '') + '">' + D(d) + '</button>';
        }
        h += '</div>';
        pop.innerHTML = h;
        pop.querySelectorAll('[data-nav]').forEach(function (b) {
            b.addEventListener('click', function () {
                var nm = m + (+b.getAttribute('data-nav')), ny = y;
                if (nm > 12) { nm = 1; ny++; }
                if (nm < 1) { nm = 12; ny--; }
                render(ny, nm, hidden);
            });
        });
        pop.querySelectorAll('.hr-dp-d').forEach(function (b) {
            b.addEventListener('click', function () {
                var iso = b.getAttribute('data-iso');
                var inp = cur; // فاز ۸۹ — قبل از close() نگه داشته شود (close مقدار cur را null می‌کند)
                hidden.value = iso;
                inp.value = fmtText(iso);
                close();
                hidden.dispatchEvent(new Event('change', { bubbles: true }));
                inp.dispatchEvent(new Event('change', { bubbles: true }));
            });
        });
    }

    function enhance(input) {
        if (input.getAttribute('data-hrdate') === '1') return;
        input.setAttribute('data-hrdate', '1');
        var hidden = document.createElement('input');
        hidden.type = 'hidden';
        hidden.name = input.name || '';
        hidden.value = input.value || '';
        input.removeAttribute('name');
        input.setAttribute('type', 'text');
        input.readOnly = true;
        input.setAttribute('autocomplete', 'off');
        input.classList.add('hr-date-text');
        input.parentNode.insertBefore(hidden, input);
        input.value = fmtText(hidden.value || todayIso());
        input.addEventListener('click', function () { open(input, hidden); });
        input.addEventListener('focus', function () { open(input, hidden); });
        input._hrHidden = hidden;
    }
    function scan(root) {
        try { (root || document).querySelectorAll('input[type="date"]').forEach(enhance); } catch (e) { }
    }
    document.addEventListener('DOMContentLoaded', function () { scan(); });
    if (document.readyState !== 'loading') scan();
    document.addEventListener('focusin', function (e) {
        if (e.target && e.target.tagName === 'INPUT' && e.target.type === 'date') enhance(e.target);
    });
    // فاز ۹۰ — پس از ناوبری AJAX هم اسکن شود (pjax رویداد hraj:navigated می‌فرستد؛
    // hraj:swapped فقط در برخی مسیرهای دیگر است — هر دو گوشیده می‌شوند، مکمل هم‌اند)
    document.addEventListener('hraj:swapped', function () { scan(); });
    document.addEventListener('hraj:navigated', function () { scan(); });
})();

// ============================================================================
// فاز ۹۰ (درخواست کاربر) — زنگ اعلان: آیتم‌ها کلیک‌پذیر
// فاز ۹۲ (اصلاح اشکال کاربر) — «فقط اولین کلیک کار می‌کرد»:
//   علت: قفل posting در بستار (closure) هرگز ریست نمی‌شد و چون site.js فقط یک‌بار
//   لود می‌شود، همهٔ کلیک‌های بعدی بی‌اثر می‌شدند. اکنون به‌جای قفل سراسری، هر
//   آیتم شمار busy خودش را دارد؛ «خوانده‌شده» ابتدا ثبت و سپس ناوبری انجام می‌شود
//   (رندر سمت سرور پس از ثبت است ⇒ آیتم از فهرست حذف می‌شود) و اگر ثبت شکست بخورد،
//   پس از ناوبری دوباره تلاش می‌شود (idempotent — Resolved=true تکراری بی‌ضرر است).
// ============================================================================
(function () {
    var pending = {}; // alertId -> true (ثبت ناموفق/ناتمام — پس از ناوبری دوباره تلاش می‌شود)
    function token() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }
    function postRead(id) {
        if (!id) return Promise.resolve();
        pending[id] = true;
        try {
            return fetch('/Notifications/ReadAlert', {
                method: 'POST', credentials: 'same-origin', keepalive: true,
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token(),
                    'X-Requested-With': 'XMLHttpRequest',
                },
                body: 'id=' + encodeURIComponent(id) + '&__RequestVerificationToken=' + encodeURIComponent(token()),
            }).then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.json();
            }).then(function (j) {
                if (!j || j.ok !== true) throw new Error((j && j.message) || 'not ok');
                delete pending[id]; // ثبت قطعی شد — تلاش مجدد لازم نیست
            }).catch(function (err) {
                console.warn('[bell] read-alert failed for ' + id + ':', err);
            });
        } catch (ex) { return Promise.resolve(); }
    }
    document.addEventListener('click', function (e) {
        var a = e.target.closest && e.target.closest('a.tb-bell-item[data-alert-id]');
        if (!a) return;
        e.preventDefault();
        e.stopPropagation();
        if (a.getAttribute('data-busy') === '1') return; // دابل‌کلیک روی «همان» آیتم
        a.setAttribute('data-busy', '1');
        var id = a.getAttribute('data-alert-id') || '';
        var href = a.getAttribute('href') || '/Notifications';
        var done = false;
        function go() {
            if (done) return;
            done = true;
            if (window.pjax && window.pjax.load) window.pjax.load(href, true);
            else location.href = href;
        }
        // ثبت «خوانده‌شده» قبل از ناوبری — بدون قفل سراسری؛ هر کلیک مستقل کار می‌کند
        try {
            postRead(id).then(go, go);
            setTimeout(go, 2500); // شبکه کند/قطع — ناوبری حداقل انجام شود
        } catch (ex) { go(); }
    }, true);
    // پس از ناوبری pjax: اگر رندر سمت سرور پیش از ثبت رسیده بود (شکست شبکه و…) دوباره تلاش کن
    document.addEventListener('hraj:navigated', function () {
        for (var id in pending) { if (Object.prototype.hasOwnProperty.call(pending, id)) postRead(id); }
    });
})();

// ============================================================================
// فاز ۹۰ (درخواست کاربر) — دراپ‌داون اختصاصی برای «همهٔ selectهای برنامه»
// ---------------------------------------------------------------------------
// فهرست بازِ select بومی توسط مرورگر سیستم‌عامل رندر می‌شود و فونت آن با CSS
// قابل تضمین نیست (اسکرین‌شات کاربر: آیتم‌های کادر تقویم وزیر نبودند). این ماژول
// هر select تکی را به دراپ‌داون هم‌شکل خودِ برنامه تبدیل می‌کند که فهرستش هم
// صددرصد با فونت وزیر رندر می‌شود.
//   • select اصلی در DOM می‌ماند (مخفی) → FormData/POST/اعتبارسنجی/اسکریپت‌های
//     موجود همه سرجایشان هستند — صفر تغییر سمت سرور.
//   • دوطرفه همگام می‌ماند: کلیک گزینه → value + رویداد change؛ تغییر value از
//     کد (حتی بدون change) و بازسازی گزینه‌ها (innerHTML) → بازسازی فهرست.
//   • select چندگانه/لیست‌باکس/native-dd دست‌نخورده می‌ماند (استایل optionهای
//     درون‌صفحه‌ای همان وزیر قبلی است).
//   • پس از pjax/مودال/هر درج DOM هم خودکار فعال می‌شود (MutationObserver بدنه).
// ============================================================================
(function () {
    'use strict';

    function closeAll(except) {
        document.querySelectorAll('.hr-dd.open').forEach(function (dd) {
            if (dd === except) return;
            dd.classList.remove('open');
            var m = dd._menu; if (m) m.hidden = true;
            var b = dd._btn; if (b) b.setAttribute('aria-expanded', 'false');
        });
    }

    function labelOf(sel) {
        var o = sel.selectedOptions && sel.selectedOptions[0];
        if (!o && sel.options.length) o = sel.options[sel.selectedIndex] || null;
        return o ? o.textContent.trim() : '';
    }

    function rebuild(dd) {
        var sel = dd._src, menu = dd._menu;
        if (!sel || !menu) return;
        menu.textContent = '';
        var any = false;
        for (var i = 0; i < sel.options.length; i++) {
            var o = sel.options[i];
            if (o.getAttribute('data-hrdd-skip') === '1') continue;
            any = true;
            var el = document.createElement('div');
            el.className = 'hr-dd-opt' + (o.disabled ? ' dis' : '');
            el.setAttribute('role', 'option');
            el.setAttribute('data-v', o.value);
            el.textContent = o.textContent.trim();
            if (o.selected) el.classList.add('on');
            menu.appendChild(el);
        }
        if (!any) { var e0 = document.createElement('div'); e0.className = 'hr-dd-empty'; e0.textContent = '—'; menu.appendChild(e0); }
        refresh(dd);
    }

    function refresh(dd) {
        var sel = dd._src, lbl = dd._lbl;
        if (!sel || !lbl) return;
        var txt = labelOf(sel);
        lbl.textContent = txt || '\u00a0';
        var o = sel.selectedOptions && sel.selectedOptions[0];
        var empty = !txt || (o && o.disabled && sel.value === '');
        dd.classList.toggle('ph', !!empty);
        if (dd._menu) {
            dd._menu.querySelectorAll('.hr-dd-opt').forEach(function (el) {
                el.classList.toggle('on', el.getAttribute('data-v') === sel.value);
            });
        }
    }

    function place(dd) {
        var menu = dd._menu, btn = dd._btn;
        if (!menu || menu.hidden) return;
        var r = btn.getBoundingClientRect();
        menu.style.minWidth = r.width + 'px';
        menu.style.bottom = ''; // فاز ۹۵ — همیشه با top/left لنگر می‌زنیم؛ bottom قدیمی نماند

        // فاز ۹۵ — اندازه‌گیری در حالت مخفی تا پرش دیده نشود
        menu.style.visibility = 'hidden';
        menu.hidden = false;

        var mh = menu.offsetHeight || menu.scrollHeight;
        var below = window.innerHeight - r.bottom;
        var above = r.top;
        var openUp = below < Math.min(mh + 12, 220) && above > below;
        menu.style.maxHeight = Math.max(140, (openUp ? above : below) - 14) + 'px';

        var mw = Math.max(menu.offsetWidth, r.width);
        // فاز ۹۵ — تراز RTL: لبهٔ راست منو با لبهٔ راست دکمه (رشد منو به سمت چپ)
        var rtl = (getComputedStyle(document.documentElement).direction || 'ltr').toLowerCase() === 'rtl';
        var vx = rtl ? (r.right - mw) : r.left;
        var vy = openUp ? (r.top - menu.offsetHeight - 4) : (r.bottom + 4);
        vx = Math.max(8, Math.min(vx, window.innerWidth - mw - 8));
        if (vy < 8) vy = 8;

        // فاز ۹۵ — جبران «والد مختصاتی»: اگر بین منو و ریشه transform/backdrop-filter
        // (مثلاً مودال) باشد، position:fixed نسبت به همان والد حل می‌شود نه viewport؛
        // با گذاشتن موقت منو در (0,0) مبدأ واقعی فضای مختصات اندازه‌گیری و جبران می‌شود.
        menu.style.top = '0px';
        menu.style.left = '0px';
        var p0 = menu.getBoundingClientRect();
        menu.style.top = Math.round(vy - p0.top) + 'px';
        menu.style.left = Math.round(vx - p0.left) + 'px';
        menu.style.visibility = '';
    }

    function enhance(sel) {
        if (!sel || sel.getAttribute('data-hrdd') === '1') return;
        if (sel.multiple || (sel.size > 1) || sel.hasAttribute('data-native-dd')) return;
        // داخل خود دراپ‌داون من نیاید (حفاظ دوطرفه)
        if (sel.closest('.hr-dd-menu')) return;
        sel.setAttribute('data-hrdd', '1');
        sel.tabIndex = -1;

        var dd = document.createElement('div');
        dd.className = 'hr-dd' + (sel.disabled ? ' dis' : '');
        dd._src = sel;
        dd.innerHTML =
            '<button type="button" class="hr-dd-btn" aria-haspopup="listbox" aria-expanded="false">' +
            '<span class="hr-dd-lbl"></span><span class="hr-dd-ico">▾</span></button>' +
            '<div class="hr-dd-menu" role="listbox" hidden></div>';
        dd._btn = dd.querySelector('.hr-dd-btn');
        dd._lbl = dd.querySelector('.hr-dd-lbl');
        dd._menu = dd.querySelector('.hr-dd-menu');

        // عرض = عرض محاسبه‌شدهٔ select تا چیدمان فرم‌ها دست نخورد
        try {
            var w = getComputedStyle(sel).width;
            if (w && w.indexOf('px') > 0) dd.style.width = w;
            else dd.style.width = '100%';
        } catch (e) { dd.style.width = '100%'; }

        sel.classList.add('hr-dd-src');
        (sel.parentNode || document.body).insertBefore(dd, sel);
        dd.appendChild(sel); // select داخل wrapper می‌ماند (بازسازی‌های والدش سالم)

        // --- کلیک روی گزینه‌ها (delegation) ---
        dd._menu.addEventListener('click', function (e) {
            var opt = e.target.closest('.hr-dd-opt');
            if (!opt || opt.classList.contains('dis')) return;
            var v = opt.getAttribute('data-v');
            if (sel.value !== v) {
                sel.value = v;
                try { sel.dispatchEvent(new Event('change', { bubbles: true })); } catch (ex) { }
            }
            closeAll();
        });

        // --- باز/بسته ---
        dd._btn.addEventListener('click', function (e) {
            e.stopPropagation();
            if (sel.disabled) return;
            var willOpen = dd._menu.hidden;
            closeAll(willOpen ? dd : null);
            dd.classList.toggle('open', willOpen);
            dd._menu.hidden = !willOpen;
            dd._btn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
            if (willOpen) place(dd);
        });

        // --- کیبورد ---
        dd._btn.addEventListener('keydown', function (e) {
            if (sel.disabled) return;
            var opts = [].slice.call(dd._menu.querySelectorAll('.hr-dd-opt:not(.dis)'));
            var idx = opts.findIndex(function (o) { return o.getAttribute('data-v') === sel.value; });
            function pick(o) {
                if (!o) return;
                sel.value = o.getAttribute('data-v');
                try { sel.dispatchEvent(new Event('change', { bubbles: true })); } catch (ex) { }
                refresh(dd);
            }
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                if (dd._menu.hidden) { dd._btn.click(); }
                else closeAll();
            } else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                e.preventDefault();
                if (dd._menu.hidden) { dd._btn.click(); return; }
                pick(opts[(idx + (e.key === 'ArrowDown' ? 1 : opts.length - 1)) % (opts.length || 1)]);
            } else if (e.key === 'Escape') {
                closeAll();
            } else if (e.key.length === 1) {
                var ch = e.key.toLowerCase();
                var hit = opts.filter(function (o) { return (o.textContent || '').trim().toLowerCase().charAt(0) === ch; })[0];
                if (hit) pick(hit);
            }
        });

        // --- همگام‌سازی برگشتی: value از کد / بازسازی گزینه‌ها ---
        sel.addEventListener('change', function () { refresh(dd); });
        try {
            var proto = Object.getPrototypeOf(sel);
            var d = Object.getOwnPropertyDescriptor(proto, 'value');
            if (d && d.set) {
                Object.defineProperty(sel, 'value', {
                    get: function () { return d.get.call(this); },
                    set: function (v) { d.set.call(this, v); refresh(dd); },
                    configurable: true,
                });
            }
        } catch (e) { }
        try {
            new MutationObserver(function () { rebuild(dd); }).observe(sel, { childList: true, subtree: true });
        } catch (e) { }

        rebuild(dd);
    }

    function scan(root) {
        try {
            (root || document).querySelectorAll('select').forEach(function (s) {
                if (s.closest('template')) return;
                enhance(s);
            });
        } catch (e) { }
    }

    // اسکن اولیه + پس از ناوبری AJAX
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { scan(); });
    else scan();
    document.addEventListener('hraj:navigated', function () { scan(); });
    document.addEventListener('hraj:swapped', function () { scan(); });

    // هر درج DOM جدید (مودال‌ها، ردیف‌های پویا، …) → اسکن
    try {
        var pend = null;
        new MutationObserver(function (muts) {
            var found = false;
            for (var i = 0; i < muts.length && !found; i++) {
                var ad = muts[i].addedNodes;
                for (var j = 0; j < ad.length; j++) {
                    var n = ad[j];
                    if (n.nodeType === 1 && (n.tagName === 'SELECT' || n.querySelector && n.querySelector('select'))) { found = true; break; }
                }
            }
            if (!found || pend) return;
            pend = requestAnimationFrame(function () { pend = null; scan(); });
        }).observe(document.body, { childList: true, subtree: true });
    } catch (e) { }

    // کلیک بیرون / اسکرول/ریسایز → بستن یا جابه‌جایی
    document.addEventListener('click', function (e) {
        if (!e.target.closest || !e.target.closest('.hr-dd')) closeAll();
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeAll(); });
    window.addEventListener('scroll', function () {
        var open = document.querySelector('.hr-dd.open');
        if (open) place(open); else closeAll();
    }, true);
    window.addEventListener('resize', function () {
        var open = document.querySelector('.hr-dd.open');
        if (open) place(open);
    });
    window.hrajDropdown = { scan: scan, closeAll: closeAll };
})();
