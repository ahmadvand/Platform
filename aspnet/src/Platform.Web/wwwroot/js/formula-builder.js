// فاز ۹۲ — محافظ: اگر site.js لود نشده باشد، مترجم identity تعریف می‌شود (رفتار امن)
if (typeof window.hrT !== 'function') { window.hrT = function (k) { return k; }; window.hrF = function (k) { return k; }; }
// ==================================================================
// فاز ۴۴ — سازندهٔ بصری فرمول (Drag & Drop) — فرمول‌ساز اقلام حقوق
// ------------------------------------------------------------------
// کاربر به‌جای تایپ فرمول، بلوک‌ها (متغیر/تابع/عملگر/عدد/کد قلم) را
// از پالت به بوم می‌کشد و رها می‌کند؛ فرمول متنی به‌صورت زنده ساخته
// و در همان textarea اصلی نوشته می‌شود. حالت «نوشتن دستی» هم محفوظ
// است — هیچ رفتار قبلی حذف نشده (فقط additive).
// قرارداد با موتور سمت سرور (Services/PayrollFormula.cs) کاملاً
// هم‌ساخت است: همان توکن‌ها، همان تقدم عملگرها، همان توابع.
// استفاده: window.hrajFormulaBuilder(textarea, { mount, vars, funcs, items })
// ==================================================================
(function () {
    'use strict';

    // آرگومان هر تابع: [حداقل، حداکثر] — -1 یعنی نامحدود (دکمه «＋ آرگومان» می‌آید)
    var ARITY = {
        'round': [1, 2], 'min': [1, -1], 'max': [1, -1],
        'abs': [1, 1], 'ceil': [1, 1], 'floor': [1, 1],
        'if': [3, 3], 'قلم': [1, 1], 'مالیات': [1, 1],
    };

    var TWO_OPS = ['<=', '>=', '==', '!=', '&&', '||'];
    var SINGLE_OPS = '+-*/%^<>(),!';
    var PREC = { '||': 1, '&&': 2, '==': 3, '!=': 3, '<': 3, '<=': 3, '>': 3, '>=': 3, '+': 4, '-': 4, '*': 5, '/': 5, '%': 5, '^': 7 };

    // ---------- نرمال‌سازی ورودی فارسی — آینهٔ PayrollFormula.Normalize ----------
    function normalize(s) {
        var out = '';
        for (var i = 0; i < s.length; i++) {
            var c = s[i], cd = s.charCodeAt(i);
            if (c === '\u200C' || c === '\u200F' || c === '\u200E') out += '_';       // نیم‌فاصله → زیرخط
            else if (c === '\u066B') out += '.';                                       // ممیز عربی
            else if (c === 'ك') out += 'ک';
            else if (c === 'ي') out += 'ی';
            else if (cd >= 0x06F0 && cd <= 0x06F9) out += String(cd - 0x06F0);         // ۰-۹
            else if (cd >= 0x0660 && cd <= 0x0669) out += String(cd - 0x0660);         // ٠-٩
            else out += c;
        }
        return out;
    }

    function isLetter(c) { return /[\p{L}]/u.test(c); }
    function isWord(c) { return /[\p{L}\p{N}_]/u.test(c); }

    // ---------- توکنایزر — همان گرامر موتور سرور ----------
    function tokenize(src) {
        var toks = [], i = 0;
        while (i < src.length) {
            var c = src[i];
            if (c === ' ' || c === '\t' || c === '\n' || c === '\r') { i++; continue; }
            if (/[0-9.]/.test(c)) {
                var j = i;
                while (j < src.length && /[0-9.]/.test(src[j])) j++;
                var text = src.slice(i, j);
                if (!/^\d*\.?\d+$/.test(text)) throw new Error(hrF('عدد «{0}» نامعتبر است', text));
                toks.push({ k: 'num', t: text });
                i = j; continue;
            }
            if (isLetter(c) || c === '_') {
                var j2 = i;
                while (j2 < src.length && (isWord(src[j2]) || src[j2] === '_')) j2++;
                toks.push({ k: 'ident', t: src.slice(i, j2) });
                i = j2; continue;
            }
            if (c === "'") {
                var j3 = i + 1;
                while (j3 < src.length && src[j3] !== "'") j3++;
                if (j3 >= src.length) throw new Error(hrT("رشته بسته نشده است — علامت ' جا افتاده"));
                toks.push({ k: 'str', t: src.slice(i + 1, j3) });
                i = j3 + 1; continue;
            }
            var two = src.substr(i, 2);
            if (TWO_OPS.indexOf(two) >= 0) { toks.push({ k: 'op', t: two }); i += 2; continue; }
            if (SINGLE_OPS.indexOf(c) >= 0) {
                toks.push({ k: c === '(' ? 'lp' : c === ')' ? 'rp' : c === ',' ? 'comma' : 'op', t: c });
                i++; continue;
            }
            throw new Error(hrF('کاراکتر غیرمجاز «{0}» در فرمول', c));
        }
        toks.push({ k: 'end', t: '' });
        return toks;
    }

    // ---------- پارسر بازگشتی — همان تقدم موتور سرور ----------
    // خروجی: {k:'num',v} {k:'var',n} {k:'str',s} {k:'un',op,x} {k:'bin',op,l,r} {k:'call',n,args}
    function parseFormula(src) {
        var toks = tokenize(src), p = 0;
        function cur() { return toks[p]; }
        function eatOp(op) { var c = cur(); if (c.k === 'op' && c.t === op) { p++; return true; } return false; }
        function expr() { return or(); }
        function or() { var l = and(); while (eatOp('||')) l = { k: 'bin', op: '||', l: l, r: and() }; return l; }
        function and() { var l = cmp(); while (eatOp('&&')) l = { k: 'bin', op: '&&', l: l, r: cmp() }; return l; }
        function cmp() {
            var l = add(); var c = cur();
            if (c.k === 'op' && ['==', '!=', '<', '<=', '>', '>='].indexOf(c.t) >= 0) { p++; return { k: 'bin', op: c.t, l: l, r: add() }; }
            return l;
        }
        function add() { var l = mul(); while (true) { var c = cur(); if (c.k === 'op' && (c.t === '+' || c.t === '-')) { p++; l = { k: 'bin', op: c.t, l: l, r: mul() }; } else return l; } }
        function mul() { var l = un(); while (true) { var c = cur(); if (c.k === 'op' && (c.t === '*' || c.t === '/' || c.t === '%')) { p++; l = { k: 'bin', op: c.t, l: l, r: un() }; } else return l; } }
        function un() { var c = cur(); if (c.k === 'op' && (c.t === '-' || c.t === '!')) { p++; return { k: 'un', op: c.t, x: un() }; } return power(); }
        function power() { var l = primary(); if (eatOp('^')) return { k: 'bin', op: '^', l: l, r: un() }; return l; }
        function primary() {
            var c = toks[p++];
            if (!c || c.k === 'end') throw new Error(hrT('فرمول ناقص است — انتهای فرمول نامنتظره'));
            if (c.k === 'num') return { k: 'num', v: c.t };
            if (c.k === 'str') return { k: 'str', s: c.t };
            if (c.k === 'lp') {
                var e = expr();
                if (cur().k !== 'rp') throw new Error(hrT('پرانتز بسته نشده است'));
                p++; return e;
            }
            if (c.k === 'ident') {
                if (cur().k === 'lp') {
                    p++; var args = [];
                    if (cur().k !== 'rp') {
                        args.push(expr());
                        while (cur().k === 'comma') { p++; args.push(expr()); }
                    }
                    if (cur().k !== 'rp') throw new Error(hrF('پرانتز تابع «{0}» بسته نشده است', c.t));
                    p++; return { k: 'call', n: c.t, args: args };
                }
                return { k: 'var', n: c.t };
            }
            throw new Error(hrF('عبارت نامعتبر: «{0}»', c.t));
        }
        var node = expr();
        if (cur().k !== 'end') throw new Error(hrF('عبارت اضافه پس از پایان فرمول: «{0}»', cur().t));
        return node;
    }

    // ==================================================================
    // ساخت رابط کاربری
    // ==================================================================
    window.hrajFormulaBuilder = function (textarea, opts) {
        opts = opts || {};
        var host = opts.mount || textarea.parentNode;
        var DragState = null;   // { kind:'new'|'move', make?, el? }
        var manual = false;
        // ---------- عناصر اصلی ----------
        var root = document.createElement('div');
        root.className = 'fb';
        root.setAttribute('dir', 'rtl');
        root.innerHTML =
            '<div class="fb-head">' +
            '<span class="fb-cap">' + hrT('🧩 سازندهٔ بصری فرمول — بلوک را بکشید و در بوم رها کنید (یا فقط کلیک کنید)') + '</span>' +
            '<span style="flex:1"></span>' +
            '<button type="button" class="fb-tbtn" data-fb-act="mode">' + hrT('✍️ نوشتن دستی') + '</button>' +
            '<button type="button" class="fb-tbtn" data-fb-act="undo">' + hrT('↩️ برگشت') + '</button>' +
            '<button type="button" class="fb-tbtn" data-fb-act="help">' + hrT('؟ راهنما') + '</button>' +
            '<button type="button" class="fb-tbtn" data-fb-act="clear">' + hrT('🧹 پاک کردن') + '</button>' +
            '</div>' +
            '<div class="fb-body">' +
            '<div class="fb-pal" data-fb-pal>' +
            '<div class="fb-tabs">' +
            '<button type="button" class="fb-tab on" data-fb-tab="vars">' + hrT('متغیرها') + '</button>' +
            '<button type="button" class="fb-tab" data-fb-tab="funcs">' + hrT('توابع') + '</button>' +
            '<button type="button" class="fb-tab" data-fb-tab="items">' + hrT('اقلام') + '</button>' +
            '<button type="button" class="fb-tab" data-fb-tab="ops">' + hrT('عملگرها') + '</button>' +
            '<button type="button" class="fb-tab" data-fb-tab="lit">' + hrT('عدد/متن') + '</button>' +
            '<button type="button" class="fb-tab" data-fb-tab="tpl">' + hrT('نمونه‌ها') + '</button>' +
            '</div>' +
            '<input type="text" class="fb-search" data-fb-search placeholder="' + hrT('جستجو…') + '" style="display:none" />' +
            '<div class="fb-chips" data-fb-pane="vars"></div>' +
            '<div class="fb-chips" data-fb-pane="funcs" style="display:none"></div>' +
            '<div class="fb-chips" data-fb-pane="items" style="display:none"></div>' +
            '<div class="fb-chips" data-fb-pane="ops" style="display:none"></div>' +
            '<div class="fb-chips" data-fb-pane="lit" style="display:none"></div>' +
            '<div class="fb-chips" data-fb-pane="tpl" style="display:none"></div>' +
            '</div>' +
            '<div class="fb-main">' +
            '<div class="fb-canvas" data-fb-canvas dir="ltr"></div>' +
            '<div class="fb-out">' + hrT('فرمول: ') + '<code data-fb-out>—</code></div>' +
            '<div class="fb-fa" data-fb-fa></div>' +
            '<div class="fb-status ok" data-fb-status></div>' +
            '<div class="fb-hint">بلوک‌ها را با موس بکشید و رها کنید؛ برای حذف، بلوک را روی پالت بیندازید یا ✕ را بزنید. روی بلوکِ عدد/متن کلیک کنید تا ویرایش شود. ترتیب محاسبه مثل ریاضی است؛ برای اولویت از پرانتز استفاده کنید.</div>' +
            '</div>' +
            '</div>';

        // جایگذاری: mount مستقل (پس از textarea در markup) یا همان والد textarea
        if (opts.mount && host !== textarea.parentNode) host.appendChild(root);
        else host.insertBefore(root, textarea);

        var canvas = root.querySelector('[data-fb-canvas]');
        var outEl = root.querySelector('[data-fb-out]');
        var statusEl = root.querySelector('[data-fb-status]');
        var searchEl = root.querySelector('[data-fb-search]');
        var pal = root.querySelector('[data-fb-pal]');
        var faEl = root.querySelector('[data-fb-fa]');

        // ---------- فاز ۸۶ — برگشت‌پذیری (Undo) — هر تغییر بوم یک عکس لحظه‌ای نگه می‌دارد؛
        // حتی «پاک کردن» هم برگشت‌پذیر شد. رویدادها همگی delegation هستند، پس بازگردانی
        // innerHTML کاملاً بی‌خطر است (هیچ لیسنری روی بلوک‌ها نصب نیست).
        var undoStack = [];
        function pushUndo() {
            undoStack.push(canvas.innerHTML);
            if (undoStack.length > 100) undoStack.shift();
        }
        function undo() {
            if (!undoStack.length) { setStatus('warn', hrT('چیزی برای برگشت نیست — همین حالا چیزی حذف یا اضافه نکرده‌اید')); return; }
            canvas.innerHTML = undoStack.pop();
            sync();
        }

        // ---------- فاز ۸۶ — راهنمای سریع ۴ قدمی برای کاربر مبتدی ----------
        function showHelp() {
            var ov = document.getElementById('fb-help-overlay');
            if (ov) { ov.remove(); return; }   // با همان دکمه باز/بسته می‌شود
            ov = document.createElement('div');
            ov.id = 'fb-help-overlay';
            ov.className = 'fb-help-overlay';
            ov.innerHTML =
                '<div class="fb-help-card" dir="rtl">' +
                '<div class="fb-help-h">' + hrT('؟ راهنمای سریع فرمول‌ساز') + ' <button type="button" class="fb-help-close" data-fb-help-close>✕</button></div>' +
                '<ol class="fb-help-steps">' +
                '<li><b>' + hrT('بلوک اضافه کنید:') + '</b>' + hrT(' از تب‌های سمت راست (متغیرها، توابع، اقلام، عملگرها، عدد/متن، نمونه‌ها) بلوک را با موس روی بوم بکشید — یا فقط روی آن کلیک کنید تا به انتهای فرمول اضافه شود.') + '</li>' +
                '<li><b>' + hrT('چیدمان را تنظیم کنید:') + '</b>' + hrT(' ترتیب محاسبه مثل ریاضی است؛ برای اولویت از «( پرانتز باز» استفاده کنید. با کشیدن جابه‌جا و با ✕ حذف کنید (انداختن بلوک روی پالت هم حذف می‌کند).') + '</li>' +
                '<li><b>' + hrT('مقدارها را بدهید:') + '</b>' + hrT(' روی بلوک‌های عدد/متن کلیک کنید تا مقدارشان ویرایش شود؛ دکمهٔ «＋» داخل هر تابع آرگومان جدید می‌آورد.') + '</li>' +
                '<li><b>' + hrT('کنترل و ذخیره:') + '</b>' + hrT(' فرمول متنی زنده ساخته می‌شود؛ خط «💬 به فارسی» معنی فرمول را نشان می‌دهد و اگر فرمول ناقص باشد خطای دقیق همان‌جا می‌آید. اشتباه کردید؟ «↩️ برگشت» — حتی «پاک کردن» هم برگشت‌پذیر است.') + '</li>' +
                '</ol>' +
                '<div class="fb-help-foot"><button type="button" class="fb-tbtn" data-fb-help-close>' + hrT('فهمیدم، شروع می‌کنم') + '</button></div>' +
                '</div>';
            document.body.appendChild(ov);
            ov.addEventListener('click', function (e) {
                if (e.target === ov || e.target.closest('[data-fb-help-close]')) ov.remove();
            });
        }

        // ---------- فاز ۸۶ — خط «💬 به فارسی»: معنی زندهٔ فرمول به زبان آدمیزاد ----------
        function human(node) {
            switch (node.k) {
                case 'num': return node.v;
                case 'var': return node.n;
                case 'str': return '«' + node.s + '»';
                case 'un':
                    return node.op === '-' ? hrF('منفی ({0})', human(node.x)) : hrF('نقیض ({0})', human(node.x));
                case 'bin':
                    var A = human(node.l), B = human(node.r);
                    switch (node.op) {
                        case '+': return A + hrT(' به‌علاوه ') + B;
                        case '-': return A + hrT(' منها ') + B;
                        case '*': return A + hrT(' ضربدر ') + B;
                        case '/': return A + hrT(' تقسیم بر ') + B;
                        case '%': return hrF('باقیماندهٔ {0} بر {1}', A, B);
                        case '^': return A + hrT(' به‌توان ') + B;
                        case '==': return A + hrT(' مساوی ') + B;
                        case '!=': return A + hrT(' مخالف ') + B;
                        case '<': return A + hrT(' کوچکتر از ') + B;
                        case '<=': return A + hrT(' کوچکتر یا مساوی ') + B;
                        case '>': return A + hrT(' بزرگتر از ') + B;
                        case '>=': return A + hrT(' بزرگتر یا مساوی ') + B;
                        case '&&': return hrF('( {0} ) و ( {1} )', A, B);
                        case '||': return hrF('( {0} ) یا ( {1} )', A, B);
                    }
                    return A + ' ' + node.op + ' ' + B;
                case 'call':
                    var args = node.args.map(human);
                    var a0 = args[0] || '۰';
                    switch (node.n) {
                        case 'قلم': return hrF('مبلغ قلم {0}', a0);
                        case 'مالیات': return hrF('مالیات پلکانی {0}', a0);
                        case 'round': return args.length > 1 ? hrF('{0} گرد شده با {1} رقم اعشار', a0, args[1]) : hrF('{0} گرد شده', a0);
                        case 'min': return hrF('کمینهٔ {0}', args.join(hrT('، ')));
                        case 'max': return hrF('بیشینهٔ {0}', args.join(hrT('، ')));
                        case 'abs': return hrF('قدرمطلق {0}', a0);
                        case 'ceil': return hrF('{0} گرد شده به بالا', a0);
                        case 'floor': return hrF('{0} گرد شده به پایین', a0);
                        case 'if': return hrF('اگر ( {0} ) آنگاه {1} وگرنه {2}', a0, (args[1] || '۰'), (args[2] || '۰'));
                    }
                    return hrF('فراخوانی {0}({1})', node.n, args.join(hrT('، ')));
            }
            return '';
        }
        function syncFa() {
            var text = (textarea.value || '').trim();
            if (!text) {
                faEl.className = 'fb-fa';
                faEl.textContent = hrT('💬 به فارسی: فرمول هنوز خالی است — بلوکی از پالت اضافه کنید تا معنی‌اش را همین‌جا ببینید.');
                return;
            }
            try {
                var node = parseFormula(text);
                faEl.className = 'fb-fa ok';
                faEl.textContent = hrT('💬 به فارسی: ') + human(node);
            } catch (ex) {
                faEl.className = 'fb-fa bad';
                faEl.textContent = hrT('💬 به فارسی: ✖ فرمول ناقص/نامعتبر است — ') + ex.message;
            }
        }

        // ---------- ابزارهای سازندهٔ بلوک ----------
        function addX(el) {
            var x = document.createElement('i');
            x.className = 'fb-x'; x.textContent = '✕'; x.title = hrT('حذف بلوک');
            el.appendChild(x);
        }

        function mkChip(kind, tok, label, title) {
            var el = document.createElement('span');
            el.className = 'fb-chip fb-' + kind;
            el.draggable = true;
            el.dataset.fbKind = kind;
            el.dataset.fbTok = tok;
            // لیبل در span — Text node خاصیت style ندارد و ویرایش درجا به آن نیاز دارد
            var lbl = document.createElement('span');
            lbl.className = 'fb-lbl';
            lbl.textContent = label !== undefined ? label : tok;
            el.appendChild(lbl);
            if (title) el.title = title;
            addX(el);
            return el;
        }

        function commaEl() {
            var c = document.createElement('span');
            c.className = 'fb-comma'; c.textContent = ',';
            return c;
        }

        function slotEl(placeholder) {
            var s = document.createElement('span');
            s.className = 'fb-slot'; s.dir = 'ltr';
            s.dataset.fbSlot = '1';
            s.dataset.fbPh = placeholder || hrT('آرگومان');
            return s;
        }

        function addArgBtn() {
            var b = document.createElement('button');
            b.type = 'button'; b.className = 'fb-addarg'; b.textContent = '＋';
            b.title = hrT('افزودن آرگومان');
            return b;
        }

        function mkCallChip(name, argNodes) {
            var el = document.createElement('span');
            el.className = 'fb-chip fb-call';
            el.draggable = true;
            el.dataset.fbKind = 'call';
            el.dataset.fbName = name;
            var b = document.createElement('b'); b.textContent = name;
            el.appendChild(b);
            el.appendChild(document.createTextNode('('));
            var args = document.createElement('span');
            args.className = 'fb-args'; args.dir = 'ltr';
            var ar = ARITY[name] || [1, 1];
            var count = Math.max(ar[0], argNodes ? argNodes.length : 0);
            var ph = name === 'قلم' ? hrT("'کد'") : hrT('آرگومان');
            for (var i = 0; i < count; i++) {
                if (i > 0) args.appendChild(commaEl());
                args.appendChild(slotEl(ph));
            }
            if (ar[1] < 0 || count < ar[1]) args.appendChild(addArgBtn());
            el.appendChild(args);
            el.appendChild(document.createTextNode(')'));
            addX(el);
            if (argNodes) {
                var slots = el.querySelectorAll(':scope > .fb-args > .fb-slot');
                argNodes.forEach(function (a, idx) {
                    if (idx < slots.length) emit(a, 0, slots[idx]);
                });
            }
            return el;
        }

        function mkItemCall(code, title) {
            var chip = mkCallChip('قلم');
            var s = chip.querySelector(':scope > .fb-args > .fb-slot');
            s.appendChild(mkChip('str', code, "'" + code + "'", title || hrF('مبلغ قلم «{0}»', code)));
            return chip;
        }

        function mkFromDef(def) {
            if (def.k === 'var') return mkChip('var', def.t, def.t, def.d);
            if (def.k === 'op' || def.k === 'paren') return mkChip(def.k, def.t, def.t, def.d);
            if (def.k === 'num') return mkChip('num', def.t);
            if (def.k === 'func') return mkCallChip(def.n);
            if (def.k === 'itemcall') return mkItemCall(def.t, def.d);
            if (def.k === 'str') return mkChip('str', def.t, "'" + def.t + "'", def.d);
            if (def.k === 'tpl') return tplFragment(def.t); // قالب آماده — چند بلوک هم‌زمان
            return mkChip('var', '0');
        }

        // قالب آماده → چند بلوک هم‌زمان (برای کلیک و درگ)
        function tplFragment(text) {
            var node = parseFormula(text);
            var frag = document.createDocumentFragment();
            emit(node, 0, frag);
            return frag;
        }

        // ---------- پر کردن پالت ----------
        function palChip(def, label, title, cls) {
            var el = document.createElement('span');
            el.className = 'fb-chip ' + (cls || 'fb-' + def.k);
            el.draggable = true;
            el.textContent = label;
            if (title) el.title = title;
            el.dataset.fbDef = JSON.stringify(def);
            return el;
        }

        var paneVars = root.querySelector('[data-fb-pane="vars"]');
        var paneFuncs = root.querySelector('[data-fb-pane="funcs"]');
        var paneItems = root.querySelector('[data-fb-pane="items"]');
        var paneOps = root.querySelector('[data-fb-pane="ops"]');
        var paneLit = root.querySelector('[data-fb-pane="lit"]');
        var paneTpl = root.querySelector('[data-fb-pane="tpl"]');

        (opts.vars || []).forEach(function (v) {
            paneVars.appendChild(palChip({ k: 'var', t: v.c, d: v.t }, v.c, v.t));
        });
        (opts.funcs || []).forEach(function (f) {
            paneFuncs.appendChild(palChip({ k: 'func', n: f.n, d: f.d }, f.sig || f.n, f.d, 'fb-call'));
        });
        var itemsCount = (opts.items || []).length;
        (opts.items || []).forEach(function (it) {
            paneItems.appendChild(palChip(
                { k: 'itemcall', t: it.c, d: it.t },
                "قلم('" + it.c + "')",
                it.t + ' — ' + hrF('مبلغ قلم «{0}» در فرمول‌های قبلی', it.c), 'fb-call'));
        });
        if (!itemsCount) {
            var emptyItems = document.createElement('div');
            emptyItems.className = 'fb-hint';
            emptyItems.textContent = hrT('برای این شرکت هنوز قلمی تعریف نشده — با «قلم جدید» اولین قلم را بسازید.');
            paneItems.appendChild(emptyItems);
        }
        // فاز ۸۶ — لیبل فارسی کنار نماد عملگرها («+ جمع»، «* ضرب»، «( پرانتز باز» و…)
        var FA_OPS = {
            '+': hrT('جمع'), '-': hrT('منها'), '*': hrT('ضرب'), '/': hrT('تقسیم'), '%': hrT('باقیمانده تقسیم'), '^': hrT('توان'),
            '(': hrT('پرانتز باز'), ')': hrT('پرانتز بسته'),
            '<': hrT('کوچکتر'), '<=': hrT('کوچکتر یا مساوی'), '>': hrT('بزرگتر'), '>=': hrT('بزرگتر یا مساوی'),
            '==': hrT('مساوی'), '!=': hrT('نامساوی'), '&&': hrT('و منطقی — هر دو شرط'), '||': hrT('یا منطقی — یکی از شرط‌ها'), '!': hrT('نقیض'),
        };
        ['+', '-', '*', '/', '%', '^', '(', ')', '<', '<=', '>', '>=', '==', '!=', '&&', '||', '!'].forEach(function (o) {
            var kind = o === '(' ? 'paren' : o === ')' ? 'paren' : 'op';
            var fa = FA_OPS[o] || o;
            paneOps.appendChild(palChip({ k: kind, t: o, d: fa }, o + ' ' + fa, fa, kind === 'paren' ? 'fb-paren' : 'fb-op'));
        });

        // تب «عدد/متن» — افزودن سریع عدد یا رشته دلخواه
        paneLit.innerHTML =
            '<div class="fb-lit" style="margin-bottom:6px">' +
            '<input type="text" dir="ltr" placeholder="' + hrT('مثلاً 1.4') + '" data-fb-num />' +
            '<button type="button" class="fb-tbtn" data-fb-addnum>' + hrT('+ عدد') + '</button>' +
            '</div>' +
            '<div class="fb-lit" style="margin-bottom:6px">' +
            '<input type="text" dir="ltr" placeholder="' + hrT('کد قلم — مثلاً masken') + '" data-fb-str />' +
            '<button type="button" class="fb-tbtn" data-fb-addstr>' + hrT("+ متن '…'") + '</button>' +
            '</div>' +
            '<div class="fb-hint">' + hrT('عدد برای ضرایب (مثل نرخ ۱٫۴) و متن برای آرگومان تابع قلم(\'کد\') استفاده می‌شود.') + '</div>';

        // ---------- تب «نمونه‌ها» — قالب‌های آمادهٔ فرمول‌های رایاج حقوق و دستمزد ----------
        // فقط متغیرهای واقعی موتور (VarDocs) و توابع موجود استفاده شده — هم‌قرارداد با سرور
        var TEMPLATES = [
            { title: hrT('اضافه‌کاری ۱٫۴ برابر'), formula: 'دقیقه_اضافه_کاری / 60 * ساعتی * 1.4', hint: hrT('نوع پیشنهادی: مزایا — غیرمستمر مشمول بیمه'), tag: 'مزایا' },
            { title: hrT('اضافه‌کاری از مجوز تأییدشده'), formula: 'دقیقه_اضافه_کاری_مجوز / 60 * ساعتی * 1.4', hint: hrT('وقتی مبنای شرکت «مجوزهای تأییدشده» است'), tag: 'مزایا' },
            { title: hrT('حق شب‌کاری (۲۵٪)'), formula: 'دقیقه_شب_کاری / 60 * ساعتی * 0.25', hint: hrT('نرخ را با کلیک روی بلوک عدد می‌توانید عوض کنید'), tag: 'مزایا' },
            { title: hrT('مأموریت روزانه'), formula: 'روزانه * مأموریت_روز', hint: hrT('نرخ روزانه × روزهای مأموریت'), tag: 'مزایا' },
            { title: hrT('حق اولاد'), formula: 'پایه_مبنا * 0.03 * تعداد_فرزند', hint: hrT('۳٪ حقوق پایه به‌ازای هر فرزند'), tag: 'مزایا' },
            { title: hrT('عیدی به‌نسبت کارکرد'), formula: 'پایه_مبنا / 12 * (کارکرد_از_سال_روز / ماه_روزها)', hint: hrT('دوازدهم پایه × نسبت روزهای خدمت سال'), tag: 'مزایا' },
            { title: hrT('کسر غیبت'), formula: 'روزانه * غیبت_روز', hint: hrT('نوع پیشنهادی: کسور'), tag: 'کسور' },
            { title: hrT('کسر تأخیر (دقیقه‌ای)'), formula: 'دقیقه_تأخیر * ساعتی / 60', hint: hrT('هر دقیقه تأخیر = نرخ ساعتی ÷ ۶۰'), tag: 'کسور' },
            { title: hrT('کسر مرخصی بدون حقوق'), formula: 'روزانه * مرخصی_بدون_حقوق_روز', hint: hrT('نوع پیشنهادی: کسور'), tag: 'کسور' },
            { title: hrT('بیمه سهم پرسنل (۷٪)'), formula: 'درآمد_مشمول_بیمه * 0.07', hint: hrT('روی درآمد مشمول بیمهٔ همین ماه'), tag: 'کسور' },
            { title: hrT('مالیات پلکانی'), formula: 'مالیات(درآمد_مشمول_مالیات)', hint: hrT('از پارامتر «پلکان_مالیات» شرکت'), tag: 'کسور' },
            { title: hrT('خالص پرداختی تا این قلم'), formula: 'جمع_مزایا - جمع_کسور', hint: hrT('برای قلم‌های جمع‌بندی'), tag: 'جمع‌بندی' },
        ];
        TEMPLATES.forEach(function (tp) {
            var el = document.createElement('span');
            el.className = 'fb-chip fb-tplcard';
            el.draggable = true;
            el.dataset.fbDef = JSON.stringify({ k: 'tpl', t: tp.formula, d: tp.title });
            el.innerHTML = '<b>' + tp.title + '</b>' +
                '<code dir="ltr">' + tp.formula + '</code>' +
                '<small>' + tp.hint + '</small>';
            var tag = document.createElement('i');
            tag.className = 'fb-tpltag fb-tpltag-' + (tp.tag === 'کسور' ? 'neg' : tp.tag === 'جمع‌بندی' ? 'sum' : 'pos');
            tag.textContent = hrT(tp.tag);
            el.appendChild(tag);
            el.title = hrT('کلیک یا کشیدن روی بوم = افزودن قالب (بلوک‌ها بعد از آن قابل ویرایش‌اند)');
            paneTpl.appendChild(el);
        });

        // ---------- جستجو ----------
        function applySearch() {
            var q = normalize(searchEl.value.trim());
            var pane = root.querySelector('[data-fb-pane]:not([style*="display: none"])');
            if (!pane) return;
            var chips = pane.querySelectorAll('.fb-chip');
            chips.forEach(function (ch) {
                ch.style.display = (!q || normalize(ch.textContent).indexOf(q) >= 0) ? '' : 'none';
            });
        }
        searchEl.addEventListener('input', applySearch);

        // ---------- تب‌ها ----------
        root.addEventListener('click', function (e) {
            var tab = e.target.closest('.fb-tab');
            if (tab) {
                root.querySelectorAll('.fb-tab').forEach(function (t) { t.classList.remove('on'); });
                tab.classList.add('on');
                ['vars', 'funcs', 'items', 'ops', 'lit', 'tpl'].forEach(function (n) {
                    root.querySelector('[data-fb-pane="' + n + '"]').style.display = (n === tab.dataset.fbTab) ? '' : 'none';
                });
                searchEl.style.display = (tab.dataset.fbTab === 'vars' || tab.dataset.fbTab === 'items' || tab.dataset.fbTab === 'funcs' || tab.dataset.fbTab === 'tpl') ? '' : 'none';
                searchEl.value = ''; applySearch();
                return;
            }
            var act = e.target.closest('[data-fb-act]');
            if (act) {
                if (act.dataset.fbAct === 'clear') { pushUndo(); canvas.innerHTML = ''; sync(); }
                else if (act.dataset.fbAct === 'mode') setMode(!manual);
                else if (act.dataset.fbAct === 'undo') undo();
                else if (act.dataset.fbAct === 'help') showHelp();
                return;
            }
            if (e.target.closest('[data-fb-addnum]')) {
                var inp = paneLit.querySelector('[data-fb-num]');
                var v = normalize(inp.value.trim());
                if (!/^\d*\.?\d+$/.test(v)) { setStatus('err', hrT('عدد نامعتبر — فقط رقم و نقطه')); return; }
                pushUndo();
                canvas.appendChild(mkChip('num', v));
                inp.value = ''; sync(); return;
            }
            if (e.target.closest('[data-fb-addstr]')) {
                var si = paneLit.querySelector('[data-fb-str]');
                var sv = si.value.trim().replace(/'/g, '');
                if (!sv) { setStatus('err', hrT('متن خالی است')); return; }
                pushUndo();
                canvas.appendChild(mkChip('str', sv, "'" + sv + "'"));
                si.value = ''; sync(); return;
            }

            // حذف بلوک
            var x = e.target.closest('.fb-x');
            if (x) {
                var chip = x.closest('.fb-chip');
                if (chip && root.contains(chip) && !chip.closest('.fb-pal')) { pushUndo(); chip.remove(); sync(); }
                return;
            }
            // افزودن آرگومان به تابع
            var add = e.target.closest('.fb-addarg');
            if (add) {
                var args = add.closest('.fb-args');
                pushUndo();
                args.insertBefore(commaEl(), add);
                args.insertBefore(slotEl(hrT('آرگومان')), add);
                refreshEmpty(); sync(); return;
            }
            // کلیک روی بلوک بوم → ویرایش عدد/متن
            var body = e.target.closest('.fb-chip');
            if (body && root.contains(body) && !body.closest('.fb-pal')) {
                if (body.dataset.fbKind === 'num' || body.dataset.fbKind === 'str') { startEdit(body); return; }
            }
            // کلیک روی بلوک پالت → افزودن به انتهای بوم (راه ساده برای کسانی که drag راحت نیست)
            var palChipEl = e.target.closest('.fb-pal .fb-chip');
            if (palChipEl && palChipEl.dataset.fbDef) {
                var def = JSON.parse(palChipEl.dataset.fbDef);
                pushUndo();
                canvas.appendChild(mkFromDef(def));
                sync();
            }
        });

        // ---------- ویرایش درجای عدد/متن ----------
        function startEdit(chip) {
            if (chip.querySelector('.fb-edit')) return;
            var old = chip.dataset.fbTok;
            var lbl = chip.querySelector('.fb-lbl');
            var inp = document.createElement('input');
            inp.className = 'fb-edit'; inp.type = 'text'; inp.dir = 'ltr';
            inp.value = old;
            chip.insertBefore(inp, lbl);
            lbl.style.display = 'none';
            inp.focus(); inp.select();
            var done = false;   // Enter بعدش blur می‌آید — دوبار commit نشود
            function commit(ok) {
                if (done) return;
                done = true;
                if (!ok) { inp.remove(); lbl.style.display = ''; return; }
                var v = normalize(inp.value.trim()).replace(/'/g, '');
                if (chip.dataset.fbKind === 'num') {
                    if (!/^\d*\.?\d+$/.test(v)) { inp.classList.add('fb-edit-bad'); done = false; return; }
                } else if (!v) { inp.classList.add('fb-edit-bad'); done = false; return; }
                pushUndo();
                chip.dataset.fbTok = v;
                lbl.textContent = chip.dataset.fbKind === 'str' ? "'" + v + "'" : v;
                inp.remove(); lbl.style.display = '';
                sync();
            }
            inp.addEventListener('keydown', function (ev) {
                if (ev.key === 'Enter') { ev.preventDefault(); commit(true); }
                else if (ev.key === 'Escape') { ev.preventDefault(); commit(false); }
            });
            inp.addEventListener('blur', function () { commit(true); });
        }

        // ---------- Drag & Drop ----------
        function isFbChip(n) { return n && n.classList && n.classList.contains('fb-chip'); }

        function insertionRef(cont, x, exclude) {
            var chips = Array.prototype.filter.call(cont.children, function (n) {
                return isFbChip(n) && n !== exclude;
            });
            for (var i = 0; i < chips.length; i++) {
                var r = chips[i].getBoundingClientRect();
                if (x < r.left + r.width / 2) return chips[i];
            }
            return null;
        }

        function ensureCaret() {
            var c = root.querySelector('.fb-caret');
            if (!c) { c = document.createElement('span'); c.className = 'fb-caret'; }
            return c;
        }
        function removeCaret() {
            var c = root.querySelector('.fb-caret');
            if (c) c.remove();
        }

        function canvasDragOver(e, cont) {
            if (!DragState) return;
            e.preventDefault();
            e.stopPropagation();
            e.dataTransfer.dropEffect = DragState.kind === 'new' ? 'copy' : 'move';
            cont.classList.add('fb-over');
            removeCaret();
            var caret = ensureCaret();
            var ref = insertionRef(cont, e.clientX, DragState.el);
            if (ref) cont.insertBefore(caret, ref); else cont.appendChild(caret);
        }
        function canvasDragLeave(e, cont) {
            cont.classList.remove('fb-over');
            var c = root.querySelector('.fb-caret');
            if (c && c.parentNode === cont) c.remove();
        }
        function canvasDrop(e, cont) {
            e.preventDefault();
            e.stopPropagation();
            if (!DragState) return;
            var caret = root.querySelector('.fb-caret');
            var chip = null;
            if (DragState.kind === 'new') chip = DragState.make();
            else chip = DragState.el;
            pushUndo();
            if (caret && caret.parentNode === cont) cont.insertBefore(chip, caret);
            else cont.appendChild(chip);
            cleanupDrag();
            sync();
        }
        function cleanupDrag() {
            removeCaret();
            root.querySelectorAll('.fb-over').forEach(function (n) { n.classList.remove('fb-over'); });
            if (DragState && DragState.el) DragState.el.classList.remove('fb-dragging');
            DragState = null;
        }

        // بوم اصلی + همه اسلات‌ها — ویرایش زنده رویدادها با delegation
        root.addEventListener('dragstart', function (e) {
            var chip = e.target.closest('.fb-chip');
            if (!chip) return;
            e.stopPropagation();
            if (chip.closest('.fb-pal')) {
                // بلوک پالت — نسخه تازه ساخته می‌شود
                var def = JSON.parse(chip.dataset.fbDef);
                DragState = { kind: 'new', make: function () { return mkFromDef(def); } };
                e.dataTransfer.effectAllowed = 'copy';
            } else {
                DragState = { kind: 'move', el: chip };
                chip.classList.add('fb-dragging');
                e.dataTransfer.effectAllowed = 'move';
            }
            try { e.dataTransfer.setData('text/plain', chip.textContent || hrT('بلوک')); } catch (ex) { }
        });
        root.addEventListener('dragend', function () { cleanupDrag(); });

        // رویدادهای DnD یکجا با delegation — نزدیک‌ترین اسلات هدف است، وگرنه بوم اصلی
        // (بستن مستقیم روی canvas باعث اجرای دوباره در اسلات‌ها و جابجایی اشتباه می‌شد)
        root.addEventListener('dragover', function (e) {
            var slot = e.target.closest ? e.target.closest('.fb-slot') : null;
            canvasDragOver(e, slot || canvas);
        });
        root.addEventListener('dragleave', function (e) {
            var slot = e.target.closest ? e.target.closest('.fb-slot') : null;
            canvasDragLeave(e, slot || canvas);
        });
        root.addEventListener('drop', function (e) {
            var slot = e.target.closest ? e.target.closest('.fb-slot') : null;
            canvasDrop(e, slot || canvas);
        });

        // انداختن بلوک روی پالت = حذف
        pal.addEventListener('dragover', function (e) {
            if (DragState && DragState.kind === 'move') { e.preventDefault(); e.dataTransfer.dropEffect = 'move'; }
        });
        pal.addEventListener('drop', function (e) {
            if (DragState && DragState.kind === 'move') {
                e.preventDefault();
                pushUndo();
                DragState.el.remove();
                cleanupDrag();
                sync();
            }
        });

        // ---------- سریال‌سازی بلوک‌ها → متن فرمول ----------
        function serializeContainer(cont) {
            var parts = [];
            Array.prototype.forEach.call(cont.children, function (n) {
                if (!isFbChip(n)) return;
                parts.push(serializeChip(n));
            });
            return joinTokens(parts);
        }
        function serializeChip(chip) {
            var kind = chip.dataset.fbKind;
            if (kind === 'call') {
                chip.classList.remove('fb-incomplete');
                var args = [];
                chip.querySelectorAll(':scope > .fb-args > .fb-slot').forEach(function (s) {
                    var inner = serializeContainer(s);
                    if (!inner) { args.push('0'); chip.classList.add('fb-incomplete'); }
                    else args.push(inner);
                });
                return chip.dataset.fbName + '(' + args.join(',') + ')';
            }
            if (kind === 'str') return "'" + chip.dataset.fbTok + "'";
            return chip.dataset.fbTok;
        }
        function joinTokens(parts) {
            var out = '';
            parts.forEach(function (p) {
                if (!p) return;
                if (!out) { out = p; return; }
                if (p === ')' || p === ',' || out.slice(-1) === '(') out += p;
                else out += ' ' + p;
            });
            return out;
        }

        // ---------- خالی/پر بودن بوم و اسلات‌ها ----------
        function refreshEmpty() {
            // بوم اصلی
            var hasRoot = Array.prototype.some.call(canvas.children, isFbChip);
            if (!hasRoot && !canvas.querySelector('.fb-empty')) {
                var ph = document.createElement('span');
                ph.className = 'fb-empty';
                ph.textContent = hrT('بلوک‌ها را از پالت اینجا بکشید — یا روی بلوک پالت فقط کلیک کنید');
                canvas.appendChild(ph);
            } else if (hasRoot) {
                var e1 = canvas.querySelector('.fb-empty'); if (e1) e1.remove();
            }
            // اسلات‌های توابع
            root.querySelectorAll('.fb-slot').forEach(function (s) {
                var has = Array.prototype.some.call(s.children, isFbChip);
                if (!has && !s.querySelector('.fb-ph')) {
                    var p2 = document.createElement('span');
                    p2.className = 'fb-ph';
                    p2.textContent = s.dataset.fbPh || hrT('آرگومان');
                    s.appendChild(p2);
                } else if (has) {
                    var p3 = s.querySelector('.fb-ph'); if (p3) p3.remove();
                }
            });
        }

        function setStatus(tone, msg) {
            statusEl.className = 'fb-status ' + tone;
            statusEl.textContent = tone === 'ok'
                ? hrT('✔ فرمول آماده است — با ذخیرهٔ قلم، اعتبارسنجی نهایی خودکار انجام می‌شود')
                : msg;
        }

        // ---------- همگام‌سازی با textarea ----------
        function sync() {
            refreshEmpty();
            var text = serializeContainer(canvas);
            textarea.value = text;
            outEl.textContent = text || '—';
            syncFa();   // فاز ۸۶ — معنی زندهٔ فرمول به فارسی + خطای دقیق فرمول ناقص
            var bad = root.querySelectorAll('.fb-call.fb-incomplete').length;
            if (bad) setStatus('warn', hrF('⚠ {0} تابع، آرگومان خالی دارد — جای خالی فعلاً ۰ حساب می‌شود؛ آن را پر کنید', bad));
            else setStatus('ok');
            if (opts.onChange) opts.onChange(text);
        }

        // ---------- ساخت بلوک‌ها از متن ----------
        function buildFromString(text) {
            var node = parseFormula(text);
            pushUndo();
            canvas.innerHTML = '';
            emit(node, 0, canvas);
            sync();
        }

        function emit(node, parentPrec, cont) {
            var p = precOf(node);
            var needParen = p < parentPrec;
            if (needParen) cont.appendChild(mkChip('paren', '(', '(', hrT('گروه‌بندی')));
            switch (node.k) {
                case 'num': cont.appendChild(mkChip('num', node.v)); break;
                case 'str': cont.appendChild(mkChip('str', node.s, "'" + node.s + "'")); break;
                case 'var': cont.appendChild(mkChip('var', node.n, node.n, hrT('متغیر زمینه — شرح در کارت «متغیرها»'))); break;
                case 'un':
                    cont.appendChild(mkChip('op', node.op, node.op));
                    emit(node.x, 6, cont);
                    break;
                case 'bin':
                    if (node.op === '^') { emit(node.l, p + 1, cont); cont.appendChild(mkChip('op', '^')); emit(node.r, p, cont); }
                    else if (PREC[node.op] === 3) { emit(node.l, p + 1, cont); cont.appendChild(mkChip('op', node.op)); emit(node.r, p + 1, cont); }
                    else { emit(node.l, p, cont); cont.appendChild(mkChip('op', node.op)); emit(node.r, p + 1, cont); }
                    break;
                case 'call': cont.appendChild(mkCallChip(node.n, node.args)); break;
            }
            if (needParen) cont.appendChild(mkChip('paren', ')', ')', hrT('گروه‌بندی')));
        }
        function precOf(node) {
            if (node.k === 'bin') return PREC[node.op];
            if (node.k === 'un') return 6;
            return 100;
        }

        // ---------- دو حالت: سازندهٔ بصری / نوشتن دستی ----------
        // فاز ۹۰ — رفع باگ «برگشت از حالت دستی ممکن نبود» (درخواست کاربر):
        // قبلاً کل root (شامل دکمهٔ تغییر حالت) مخفی می‌شد → راه برگشتی وجود نداشت.
        // حالا فقط بدنهٔ سازنده (fb-body) مخفی می‌شود و نوار دکمه‌ها بالای آن
        // باقی می‌ماند؛ دکمهٔ «🧩 سازندهٔ بصری» همیشه در دسترس است.
        var modeBtn = root.querySelector('[data-fb-act="mode"]');
        var bodyEl = root.querySelector('.fb-body');
        var capEl = root.querySelector('.fb-cap');
        // پیام وضعیت در حالت دستی — بعد از textarea (چون fb-status داخل بدنهٔ مخفی است)
        var manualMsg = document.createElement('div');
        manualMsg.className = 'fb-status ok';
        manualMsg.style.display = 'none';
        manualMsg.setAttribute('data-fb-manual-msg', '1');
        function showManual(cls, msg) {
            manualMsg.className = 'fb-status ' + (cls || 'ok');
            manualMsg.textContent = msg;
            manualMsg.style.display = 'block';
        }
        function hideManual() { manualMsg.style.display = 'none'; }
        if (textarea.parentNode) textarea.parentNode.insertBefore(manualMsg, textarea.nextSibling);
        function setMode(toManual) {
            if (toManual) {
                manual = true;
                if (bodyEl) bodyEl.style.display = 'none';
                if (capEl) capEl.textContent = hrT('✍️ نوشتن دستی فرمول — برای بازگشت به سازندهٔ بصری، دکمهٔ «🧩 سازندهٔ بصری» را بزنید');
                textarea.style.display = '';
                textarea.setAttribute('required', 'required');
                modeBtn.textContent = hrT('🧩 سازندهٔ بصری');
                hideManual();
            } else {
                var val = textarea.value;
                try {
                    if (val.trim()) buildFromString(val);
                    else { canvas.innerHTML = ''; sync(); }
                    manual = false;
                    textarea.style.display = 'none';
                    textarea.removeAttribute('required');
                    if (bodyEl) bodyEl.style.display = '';
                    if (capEl) capEl.textContent = hrT('🧩 سازندهٔ بصری فرمول — بلوک را بکشید و در بوم رها کنید (یا فقط کلیک کنید)');
                    modeBtn.textContent = hrT('✍️ نوشتن دستی');
                    hideManual();
                } catch (ex) {
                    // فرمول دستی قابل تبدیل به بلوک نیست — در حالت نوشتن می‌مانیم
                    // فاز ۹۰ — پیام خطا حالا در حالت دستی (بالای دکمه‌ها) هم دیده می‌شود
                    showManual('err', hrF('✖ {0} — در حالت نوشتن بمانید یا فرمول را اصلاح کنید', ex.message));
                }
            }
        }

        // حالت اولیه — اگر فرمول موجود قابل تبدیل بود، سازنده باز می‌شود
        try {
            var initVal = textarea.value;
            if (initVal.trim()) {
                buildFromString(initVal);
                textarea.style.display = 'none';
                textarea.removeAttribute('required');
            } else {
                sync();
                textarea.style.display = 'none';
                textarea.removeAttribute('required');
            }
            textarea.insertAdjacentHTML('afterend', '');
        } catch (ex) {
            manual = true;
            // فاز ۹۰ — فقط بدنه مخفی شود؛ دکمهٔ برگشت به سازندهٔ بصری در دسترس بماند
            if (bodyEl) bodyEl.style.display = 'none';
            if (capEl) capEl.textContent = hrT('✍️ نوشتن دستی فرمول — برای بازگشت به سازندهٔ بصری، دکمهٔ «🧩 سازندهٔ بصری» را بزنید');
            modeBtn.textContent = hrT('🧩 سازندهٔ بصری');
            textarea.style.display = '';
            showManual('err', hrF('✖ {0} — در حالت نوشتن ادامه دهید', ex.message));
        }

        return {
            sync: sync,
            setMode: setMode,
            root: root,
        };
    };
})();
