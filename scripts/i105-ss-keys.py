#!/usr/bin/env python3
# فاز ۱۰۵ — استخراج کلیدهای محافظ صفحه (فازهای ۱۰۳/۱۰۴/۱۰۵) و یافتن غایب‌های ترجمه در DB
import io, re, sqlite3, json, sys

FILES = [
    'src/Platform.Web/Modules/Core/Controllers/SettingsController.cs',
    'src/Platform.Web/Infrastructure/ScreensaverLockGate.cs',
    'src/Platform.Web/Views/Settings/Index.cshtml',
    'src/Platform.Web/Views/Shared/_Layout.cshtml',
]
# فقط ناحیه‌های محافظ صفحه در SettingsController (از فاز ۱۰۴ به بعد) و ویو
pat_t = re.compile(r'''(?:HrT\.T|HrT\.F|ToastErr|ToastOk|@HrT\.T|@HrT\.F|T)\(\s*"((?:[^"\\]|\\.)*)"' ''' .replace("' ",""))

def strings_of(text):
    out = []
    for m in re.finditer(r'''(?:HrT\.)?([TF])?\(\s*"((?:[^"\\\n]|\\.)*)"\s*[,)]''', text):
        out.append(m.group(2))
    for m in re.finditer(r'(?:ToastErr|ToastOk)\("((?:[^"\\\n]|\\.)*)"\)', text):
        out.append(m.group(1))
    return out

def unescape(s):
    return s.encode().decode('unicode_escape') if '\\' in s else s

# کلیدهای مرتبط با محافظ صفحه = کلیدهایی که وازهٔ «محافظ/قفل/بسته» یا الگوی فاز ۱۰۴/۱۰۵ دارند
SS_WORDS = ('محافظ', 'قفل', 'بسته', 'باران', 'بیدارسازی', 'سندباکس', 'صفحه')

db = sqlite3.connect('/home/z/my-project/aspnet/sc-run/hr.db')
cur = db.cursor()
langs = [r[0] for r in cur.execute("SELECT DISTINCT LangCode FROM HrLangEntries WHERE LangCode != 'fa'")]
existing = {}
for lg in langs:
    existing[lg] = set(k for (k,) in cur.execute("SELECT Key FROM HrLangEntries WHERE LangCode=?", (lg,)))

missing = {}
for path in FILES:
    text = io.open(path, encoding='utf-8').read()
    # برای SettingsController فقط بخش‌های محافظ صفحه (از تعریف فاز ۱۰۴ تا پایان ScreensaverRuntime)
    if 'SettingsController' in path:
        i0 = text.find('// ---------- فاز ۱۰۴')
        i1 = text.find('// ---------- لوگو ----------')
        if i0 > 0 and i1 > i0:
            text = text[i0:i1]
        else:
            text = ''
    for s in strings_of(text):
        u = unescape(s).strip()
        if len(u) < 3 or u.startswith('http'):
            continue
        if not any(w in u for w in SS_WORDS):
            continue
        for lg in langs:
            if u not in existing[lg]:
                missing.setdefault(u, set()).add(lg)

keys = sorted(missing)
print(json.dumps({'langs': langs, 'missing_count': len(keys), 'keys': keys}, ensure_ascii=False, indent=1))
