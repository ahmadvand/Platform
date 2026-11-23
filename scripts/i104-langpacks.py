# -*- coding: utf-8 -*-
# ======================================================================
# فاز ۱۰۴ — ترجمهٔ کلیدهای جدید «برون‌بری/درون‌ریزی تنظیمات محافظ صفحه»
# برای en/ru/ar (همان سه زبانی که فازهای ۱۰۱-۱۰۳ ترجمهٔ کامل دارند).
# ----------------------------------------------------------------------
# روش: بستهٔ hr-lang-pack فقط با کلیدهای «غایب» ساخته می‌شود و از مسیر
# رسمی خود سامانه (POST /Langs/Import — ادغام، بدون حذف) درون‌ریزی می‌شود؛
# هیچ ردیف موجودی دست‌نخورده تغییر نمی‌کند و کش ترجمه با langs.Bump()
# بی‌اعتبار می‌شود. اصطلاحات از ترجمه‌های قبلی محافظ صفحه هم‌ساز شده‌اند:
#   محافظ صفحه = screen saver / экранная заставка / حافظ الشاشة
#   روشن/خاموش = On/Off ، Вкл/Выкل ، مفتوح/مغلق
# مصرف: python3 scripts/i104-langpacks.py            # ساخت + درون‌ریزی
#       python3 scripts/i104-langpacks.py --dry      # فقط ساخت و گزارش
# ======================================================================
import json, os, re, sqlite3, subprocess, sys

ROOT = '/home/z/my-project'
DB = f'{ROOT}/aspnet/sc-run/hr.db'
OUT = f'{ROOT}/scripts/langpacks/out'
B = 'http://localhost:3000'
JAR = '/tmp/i104-jar.txt'
DRY = '--dry' in sys.argv[1:]

# کلید (متن فارسی مبدأ — دقیقاً مطابق سورس) → {en, ru, ar}
K = {
    'روشن': {
        'en': 'On', 'ru': 'Вкл', 'ar': 'مفتوح',
    },
    'با رمز': {
        'en': 'with password', 'ru': 'с паролем', 'ar': 'بكلمة المرور',
    },
    'بدون رمز': {
        'en': 'without password', 'ru': 'без пароля', 'ar': 'بدون كلمة المرور',
    },
    '{0} ثانیه': {
        'en': '{0} seconds', 'ru': '{0} сек.', 'ar': '{0} ثانية',
    },
    'برون‌بری تنظیمات محافظ صفحه (JSON)': {
        'en': 'Export screen saver settings (JSON)',
        'ru': 'Экспорт настроек экранной заставки (JSON)',
        'ar': 'تصدير إعدادات حافظ الشاشة (JSON)',
    },
    'درون‌ریزی از فایل': {
        'en': 'Import from file', 'ru': 'Импорт из файла', 'ar': 'استيراد من ملف',
    },
    'فایل بستهٔ تنظیمات': {
        'en': 'Settings package file', 'ru': 'Файл пакета настроек', 'ar': 'ملف حزمة الإعدادات',
    },
    'تنظیمات فعلی محافظ صفحه با محتوای فایل جایگزین شود؟': {
        'en': 'Replace the current screen saver settings with the file contents?',
        'ru': 'Заменить текущие настройки экранной заставки содержимым файла?',
        'ar': 'هل تُستبدل الإعدادات الحالية لحافظ الشاشة بمحتوى الملف؟',
    },
    'فایل تنظیمات محافظ صفحه را انتخاب کنید': {
        'en': 'Choose the screen saver settings file',
        'ru': 'Выберите файл настроек экранной заставки',
        'ar': 'اختر ملف إعدادات حافظ الشاشة',
    },
    'حجم فایل تنظیمات نباید بیش از ۱ مگابایت باشد': {
        'en': 'Settings file must not exceed 1 MB',
        'ru': 'Размер файла настроек не должен превышать 1 МБ',
        'ar': 'يجب ألا يتجاوز حجم ملف الإعدادات ١ ميغابايت',
    },
    'خواندن فایل ممکن نشد — ': {   # en موجود است — فقط ru/ar
        'ru': 'Не удалось прочитать файл — ', 'ar': 'تعذّرت قراءة الملف — ',
    },
    'این فایل بستهٔ تنظیمات محافظ صفحه نیست': {
        'en': 'This file is not a screen saver settings package',
        'ru': 'Этот файл не является пакетом настроек экранной заставки',
        'ar': 'هذا الملف ليس حزمة إعدادات حافظ الشاشة',
    },
    'بخش settings در فایل یافت نشد': {
        'en': 'The "settings" section was not found in the file',
        'ru': 'Раздел «settings» в файле не найден',
        'ar': 'لم يُعثر على القسم «settings» في الملف',
    },
    'قالب فایل معتبر نیست (JSON نامعتبر) — ': {
        'en': 'Invalid file format (invalid JSON) — ',
        'ru': 'Неверный формат файла (недопустимый JSON) — ',
        'ar': 'تنسيق الملف غير صالح (JSON غير صالح) — ',
    },
    'مقادیر وضعیت/دقیقه/ثانیه/رمز در فایل نامعتبر یا ناقص است': {
        'en': 'The enabled/minutes/seconds/requirePassword values in the file are invalid or incomplete',
        'ru': 'Значения enabled/minutes/seconds/requirePassword в файле недействительны или неполны',
        'ar': 'قيم الحالة/الدقائق/الثواني/كلمة المرور في الملف غير صالحة أو ناقصة',
    },
    'تنظیمات محافظ صفحه درون‌ریزی شد — وضعیت: {0}، زمان: {1}، بیدارسازی: {2}': {
        'en': 'Screen saver settings imported — state: {0}, timeout: {1}, wake-up: {2}',
        'ru': 'Настройки экранной заставки импортированы — состояние: {0}, время: {1}, пробуждение: {2}',
        'ar': 'تم استيراد إعدادات حافظ الشاشة — الحالة: {0}، الوقت: {1}، الاستيقاظ: {2}',
    },
    'برون‌بری: تنظیمات جاری محافظ صفحه (وضعیت، دقیقه، ثانیه، بیدارسازی با رمز) به‌صورت فایل JSON دانلود می‌شود؛ درون‌ریزی: فایل برون‌بری‌شده را بارگذاری کنید تا همان تنظیمات روی این نصب اعمال شود — مثلاً برای یکسان‌سازی چند نصب.': {
        'en': 'Export: downloads the current screen saver settings (state, minutes, seconds, password wake-up) as a JSON file; Import: upload an exported file to apply the same settings on this installation — e.g. to keep several installations identical.',
        'ru': 'Экспорт: текущие настройки экранной заставки (состояние, минуты, секунды, пробуждение по паролю) скачиваются в виде файла JSON; импорт: загрузите экспортированный файл, чтобы применить те же настройки на этой установке — например, чтобы несколько установок были одинаковыми.',
        'ar': 'التصدير: تُنزَّل الإعدادات الحالية لحافظ الشاشة (الحالة، الدقائق، الثواني، الاستيقاظ بكلمة المرور) كملف JSON؛ الاستيراد: حمِّل الملف المُصدَّر لتطبيق الإعدادات نفسها على هذا التنصيب — مثلًا لتوحيد عدّة تنصيبات.',
    },
}

LANG_META = {'en': 'English', 'ru': 'Русский', 'ar': 'العربية'}


def existing():
    con = sqlite3.connect(DB)
    rows = dict(con.execute(
        "select LangCode || '|' || Key, Value from HrLangEntries where LangCode in ('en','ru','ar')"))
    meta = {r[0]: (r[1], r[2]) for r in
            con.execute("select Code, Title, Dir from HrLangDefs where Code in ('en','ru','ar')")}
    con.close()
    return rows, meta


def tok(html):
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    return m.group(1) if m else ''


def curl(*args, **kw):
    return subprocess.run(['curl', '-s', *args], capture_output=True, text=True, **kw)


def login():
    if os.path.exists(JAR):
        os.unlink(JAR)
    t = tok(curl('-c', JAR, f'{B}/Auth/Login').stdout)
    r = curl('-b', JAR, '-c', JAR, '-o', '/dev/null', '-w', '%{http_code}',
             '-X', 'POST', f'{B}/Auth/Login',
             '--data-urlencode', 'identifier=admin',
             '--data-urlencode', 'secret=Admin@1234',
             '--data-urlencode', f'__RequestVerificationToken={t}')
    print('login POST ->', r.stdout.strip())
    # گیت لایسنس — در صورت ریدایرکت به Notice اول «فهمیدم» زده شود
    n = curl('-b', JAR, '-c', JAR, f'{B}/License/Notice').stdout
    at = tok(n)
    if at and ('License/Notice' in n or 'فهمیدم' in n or 'ack' in n.lower()):
        curl('-b', JAR, '-c', JAR, '-o', '/dev/null', '-X', 'POST', f'{B}/License/Ack',
             '--data-urlencode', f'__RequestVerificationToken={at}')
        print('license ack done')


def main():
    have, meta = existing()
    os.makedirs(OUT, exist_ok=True)
    packs = {}
    for code, title in LANG_META.items():
        d = meta.get(code, (title, 'ltr' if code != 'ar' else 'rtl'))
        entries = {k: v[code] for k, v in K.items() if f'{code}|{k}' not in have}
        packs[code] = {
            'format': 'hr-lang-pack', 'version': 1, 'code': code,
            'title': d[0], 'dir': d[1], 'entries': entries,
        }
        p = f'{OUT}/hr-lang-{code}-i104.json'
        tmp = p + '.tmp'
        json.dump(packs[code], open(tmp, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
        os.replace(tmp, p)
        print(f'{code}: {len(entries)} new keys -> {p}')

    if DRY:
        print('DRY — upload skipped')
        return

    login()
    page = curl('-b', JAR, '-c', JAR, f'{B}/Langs').stdout
    t = tok(page)
    if not t:
        print('FATAL: no token on /Langs'); sys.exit(1)
    for code in ('en', 'ru', 'ar'):
        p = f'{OUT}/hr-lang-{code}-i104.json'
        r = curl('-b', JAR, '-c', JAR, '-o', '/dev/null', '-w', '%{http_code} %{redirect_url}',
                 '-X', 'POST', f'{B}/Langs/Import',
                 '-F', f'file=@{p};type=application/json',
                 '-F', f'__RequestVerificationToken={t}')
        print(f'import {code} ->', r.stdout.strip())

    # راستی‌آزمایی مستقیم از DB
    have2, _ = existing()
    missing = [f'{c}|{k}' for c in ('en', 'ru', 'ar') for k, v in K.items() if f'{c}|{k}' not in have2 and c in v]
    print('still missing:', len(missing))
    if missing:
        for m in missing[:10]:
            print('  MISSING:', m)
        sys.exit(2)
    print('I104 LANGPACKS OK')


if __name__ == '__main__':
    main()
