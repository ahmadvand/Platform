// ============================================================
// فاز ۱۰۴ — انتخابگر جستجومحور پرسنل (مقیاس‌پذیر برای ده‌ها هزار پرسنل)
// ----------------------------------------------------------------------
// مشکل: <select>هایی که همهٔ پرسنل را سمت سرور رندر می‌کردند با ۱۰٬۰۰۰ نفر
// چند مگابایت HTML و DOM سنگین می‌سازند (کندی مرورگر، انتخاب دشوار).
// راه‌حل: جستجوی زندهٔ سمت سرور (حداکثر ۵۰ نتیجه) + چیپ‌های انتخاب‌شده.
// قرارداد POST حفظ شده: حالت چندتایی = <select multiple hidden> با
// همان name؛ حالت تک‌انتخابی = <input type="hidden">.
// استفاده:
//   <div class="emp-picker" data-name="employeeIds" data-url="/Biometrics/EmpSearch"
//        data-multiple="1" data-all="1" data-all-field="allEmployees"
//        data-ph="…جستجو" data-empty="…">
//   </div>
// رشته‌ها با hrT (دیکشنری سرور) یا data-attrهای ترجمه‌شدهٔ سمت سرور.
// ============================================================
(function () {
    'use strict';

    function debounce(fn, ms) {
        var t = null;
        return function () {
            var a = arguments, self = this;
            clearTimeout(t);
            t = setTimeout(function () { fn.apply(self, a); }, ms);
        };
    }

    function initPicker(box) {
        if (box.dataset.ready) return;
        box.dataset.ready = '1';

        var multiple = box.dataset.multiple === '1';
        var url = box.dataset.url || '/Ui/EmpSearch';
        var name = box.dataset.name;
        var extraParams = box.dataset.params || '';   // مثلا "deviceId=..." — با {v} جایگزین می‌شود
        var ph = box.dataset.ph || '…';
        var emptyText = box.dataset.empty || 'موردی یافت نشد';
        var minChars = parseInt(box.dataset.min || '0', 10);
        var allMode = box.dataset.all === '1';
        var allField = box.dataset.allField || '';
        var allLabel = box.dataset.allLabel || '';
        var preselected = [];
        try { preselected = JSON.parse(box.dataset.selected || '[]'); } catch (e) { }

        var holder = document.createElement(multiple ? 'select' : 'input');
        if (multiple) { holder.multiple = true; holder.style.display = 'none'; }
        else holder.type = 'hidden';
        holder.name = name;
        box.appendChild(holder);

        // نوار جستجو + برچسب شمارنده
        var row = document.createElement('div');
        row.className = 'ep-row';
        var input = document.createElement('input');
        input.type = 'text';
        input.className = 'ep-input';
        input.placeholder = ph;
        input.autocomplete = 'off';
        row.appendChild(input);
        var count = document.createElement('span');
        count.className = 'ep-count hint';
        row.appendChild(count);
        box.appendChild(row);

        // فهرست نتایج
        var list = document.createElement('div');
        list.className = 'ep-list';
        list.style.display = 'none';
        box.appendChild(list);

        // چیپ‌ها
        var chips = document.createElement('div');
        chips.className = 'ep-chips';
        box.appendChild(chips);

        var selected = {};   // id -> label
        var allOn = false;
        var allCount = 0;

        function preselectItems() {
            (preselected || []).forEach(function (it) {
                if (it && it.id) selected[it.id] = it.label || it.name || it.id;
            });
        }
        preselectItems();

        function paintHolder() {
            holder.innerHTML = '';
            if (multiple) {
                Object.keys(selected).forEach(function (id) {
                    var o = document.createElement('option');
                    o.value = id; o.selected = true; o.textContent = selected[id];
                    holder.appendChild(o);
                });
            } else {
                holder.value = Object.keys(selected)[0] || '';
            }
        }

        function paintChips() {
            chips.innerHTML = '';
            var ids = Object.keys(selected);
            if (allOn) {
                var c1 = document.createElement('span');
                c1.className = 'ep-chip ep-chip-all';
                c1.textContent = allLabel.replace('{0}', allCount > 0 ? allCount.toLocaleString('en') : '');
                var x1 = document.createElement('button');
                x1.type = 'button'; x1.className = 'ep-x'; x1.textContent = '✕';
                x1.setAttribute('aria-label', 'remove');
                x1.onclick = function () { allOn = false; syncAllField(); paintChips(); paintCount(); };
                c1.appendChild(x1);
                chips.appendChild(c1);
            }
            ids.forEach(function (id) {
                var c = document.createElement('span');
                c.className = 'ep-chip';
                c.textContent = selected[id].length > 60 ? selected[id].slice(0, 58) + '…' : selected[id];
                var x = document.createElement('button');
                x.type = 'button'; x.className = 'ep-x'; x.textContent = '✕';
                x.setAttribute('aria-label', 'remove');
                x.onclick = function () { delete selected[id]; paintHolder(); paintChips(); paintCount(); };
                c.appendChild(x);
                chips.appendChild(c);
            });
            paintHolder();
        }

        function syncAllField() {
            var f = allField ? box.querySelector('input[name="' + allField + '"]') : null;
            if (f) f.value = allOn ? 'true' : 'false';
            // در حالت همه، انتخاب‌های تکی پاک می‌شوند (سرور همهٔ پرسنل را حساب می‌کند)
            if (allOn) { selected = {}; paintHolder(); }
        }

        function paintCount() {
            var n = Object.keys(selected).length;
            count.textContent = allOn ? '' : (n > 0 ? n.toLocaleString('en') + ' ✓' : '');
        }

        function addAllField() {
            if (!allMode || !allField) return;
            var f = document.createElement('input');
            f.type = 'hidden'; f.name = allField; f.value = 'false';
            box.appendChild(f);
        }
        addAllField();

        function addAllButton() {
            if (!allMode) return;
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'btn btn-sm ep-allbtn';
            b.textContent = allLabel.replace('{0}', '…');
            b.onclick = function () {
                allOn = !allOn;
                b.classList.toggle('ep-on', allOn);
                syncAllField();
                paintChips();
                paintCount();
            };
            row.appendChild(b);
        }

        var doSearch = debounce(function () {
            var q = input.value.trim();
            if (q.length < minChars) { list.style.display = 'none'; return; }
            var u = url + (url.indexOf('?') >= 0 ? '&' : '?') + 'q=' + encodeURIComponent(q) + '&limit=50';
            if (extraParams) u += '&' + extraParams.replace('{v}', encodeURIComponent(input.dataset.ctx || ''));
            fetch(u, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (r) { return r.ok ? r.json() : { items: [] }; })
                .then(function (j) {
                    var items = j.items || [];
                    list.innerHTML = '';
                    if (!items.length) {
                        var d = document.createElement('div');
                        d.className = 'ep-empty';
                        d.textContent = emptyText;
                        list.appendChild(d);
                    }
                    items.forEach(function (it) {
                        var opt = document.createElement('button');
                        opt.type = 'button';
                        opt.className = 'ep-item' + (selected[it.id] ? ' ep-sel' : '');
                        opt.innerHTML = '<span class="ep-main"></span><span class="ep-sub"></span>';
                        opt.querySelector('.ep-main').textContent = it.label || it.name;
                        opt.querySelector('.ep-sub').textContent = it.sub || '';
                        opt.onclick = function () {
                            if (multiple) {
                                if (selected[it.id]) delete selected[it.id];
                                else selected[it.id] = it.label || it.name;
                            } else {
                                selected = {}; selected[it.id] = it.label || it.name;
                                list.style.display = 'none';
                                input.value = '';
                            }
                            paintChips(); paintCount();
                            if (multiple) paintItems(lastItems);
                        };
                        list.appendChild(opt);
                    });
                    lastItems = items;
                    list.style.display = '';
                })
                .catch(function () { list.style.display = 'none'; });
        }, 220);

        // منبع انتخاب context (مثلاً دستگاه مبدأ) — تغییرش انتخاب‌ها و نتایج را پاک می‌کند
        if (box.dataset.ctxFrom) {
            var ctxEl = document.querySelector(box.dataset.ctxFrom);
            if (ctxEl) ctxEl.addEventListener('change', function () {
                input.dataset.ctx = ctxEl.value;
                input.value = '';
                lastItems = [];
                list.style.display = 'none';
                selected = {};
                allOn = false;
                var af = allField ? box.querySelector('input[name="' + allField + '"]') : null;
                if (af) af.value = 'false';
                var ab = box.querySelector('.ep-allbtn');
                if (ab) ab.classList.remove('ep-on');
                paintChips();
                paintCount();
            });
        }

        var lastItems = [];
        function paintItems(items) {
            Array.prototype.forEach.call(list.querySelectorAll('.ep-item'), function (el, i) {
                if (items[i]) el.classList.toggle('ep-sel', !!selected[items[i].id]);
            });
        }

        input.addEventListener('input', doSearch);
        input.addEventListener('focus', function () { if (input.value.trim().length >= minChars && lastItems.length) list.style.display = ''; });
        document.addEventListener('click', function (e) {
            if (!box.contains(e.target)) list.style.display = 'none';
        });

        paintChips();
        paintCount();
        if (allMode) addAllButton();
    }

    function boot(root) {
        (root || document).querySelectorAll('.emp-picker').forEach(initPicker);
    }
    window.empPickerBoot = boot;
    document.addEventListener('DOMContentLoaded', function () { boot(); });
    // پشتیبانی از ناوبری AJAX (hraj) — پس از تعویض صفحه دوباره اسکن کن
    document.addEventListener('hraj:navigated', function () { boot(); });
    document.addEventListener('hraj:swapped', function () { boot(); });
})();
