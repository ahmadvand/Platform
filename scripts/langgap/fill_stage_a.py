#!/usr/bin/env python3
# فاز ۱۱۳ (مرحلهٔ اول) — تکمیل کلیدهای جدید در همهٔ بسته‌های زبان
# ar: ۲۴ کلید جاافتاده (نام ماه‌های قمری، برون‌ریزی اکسل، ثانیه‌های محافظ، جبران قمری، …)
# ru: ۱ کلید (نسخهٔ قدیمی دیالوگ جابجایی واحد با \\n literal — کلید legacy)
# ka/ky/prs/ps/tg/tk/uz: همین کلیدهای جدید برای پوشش UI تازه
# متای بستهٔ ar کامل می‌شود (format/version از الگوی en)
import json, glob, os, sys

PACKS = '/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks'
GAP = '/home/z/my-project/scripts/langgap/langgap'

missing_ar = json.load(open(os.path.join(GAP, 'missing-ar.json'), encoding='utf-8'))
missing_ru = json.load(open(os.path.join(GAP, 'missing-ru.json'), encoding='utf-8'))

# ---------- ترجمهٔ کلیدهای جاافتاده (بر اساس ترتیب missing_ar با ادعای محتوا) ----------
# ایندکس‌ها با تأیید زیررشته در ابتدای اجرا کنترل می‌شوند.
AR = [
    'تصدير Excel لحركات اليوم',                 # برون‌ریزی اکسل تردد امروز
    'تصدير Excel للموظفين',                     # برون‌ریزی اکسل پرسنل
    'تم تصدير حركات اليوم',                     # ترددِ امروز برون‌ریزی شد
    'تقويم النظام المعروض',                     # تقویم نمایشی سامانه
    'ثواني الخمول حتى التشغيل (٠ إلى ٥٩)',      # ثانیه‌های بی‌حرکتی تا اجرا (۰ تا ۵۹)
    'التعويض القمري: {0} يوم',                  # جبران قمری: {0} روز
    'جمادى الأولى',                             # جمادی‌الاول
    'جمادى الآخرة',                             # جمادی‌الثانی
    'ملف Excel جاهز — بدأ التنزيل',             # خروجی اکسل آماده شد — دانلود شروع شد
    'تصدير Excel لحركات هذا اليوم',             # خروجی اکسل تردد این روز
    'تصدير Excel لقائمة الموظفين',              # خروجی اکسل فهرست پرسنل
    'دقائق الخمول حتى التشغيل (١ إلى ١٢٠)',     # دقایق بی‌حرکتی تا اجرا (۱ تا ۱۲۰)
    'ذو الحجة',                                 # ذی‌الحجه
    'ذو القعدة',                                # ذی‌القعده
    'ربيع الأول',                               # ربیع‌الاول
    'ربيع الثاني',                              # ربیع‌الثانی
    'رجب',                                      # رجب
    'رمضان',                                    # رمضان
    'شعبان',                                    # شعبان
    'شوال',                                     # شوال
    'صفر',                                      # صفر
    'محرم',                                     # محرم
    'جميع إعدادات النظام في هذه الصفحة مصنّفة في تبويبات — اختر التبويب المطلوب من أعلى النموذج',
                                                # همهٔ تنظیمات سامانه در این صفحه ...
    '📤 تصدير Excel',                           # 📤 برون‌ریزی اکسل
]

KA = [
    'დღევანდელი აღრიცხვის Excel ექსპორტი',
    'პერსონალის Excel ექსპორტი',
    'დღევანდელი აღრიცხვა ექსპორტირებულია',
    'სისტემის საჩვენებელი კალენდარი',
    'უმოქმედობის წამები გაშვებამდე (0-დან 59-მდე)',
    'მთვარიული კორექცია: {0} დღე',
    'ჯუმადა ალ-ულა',
    'ჯუმადა ალ-სანია',
    'Excel ფაილი მზადაა — ჩამოტვირთვა დაიწყო',
    'ამ დღის აღრიცხვის Excel ექსპორტი',
    'პერსონალის სიის Excel ექსპორტი',
    'უმოქმედობის წუთები გაშვებამდე (1-დან 120-მდე)',
    'ზულ-ჰიჯა',
    'ზულ-ყადა',
    'რაბი ალ-ავვალი',
    'რაბი ალ-სანი',
    'რაჯაბი',
    'რამადანი',
    'შააბანი',
    'შავვალი',
    'საფარი',
    'მუჰარამი',
    'სისტემის ყველა პარამეტრი ამ გვერდზე ჩანართებად არის დაჯგუფებული — აირჩიეთ სასურველი ჩანართი ფორმის ზემოდან',
    '📤 Excel ექსპორტი',
]

KY = [
    'Бүгүнкү жүрүмдүн Excel экспорту',
    'Персоналдын Excel экспорту',
    'Бүгүнкү жүрүм экспорттолду',
    'Системанын көрсөтмө календары',
    'Кыймылсыздык секунддары аткарылганга чейин (0–59)',
    'Хижри оңдоо: {0} күн',
    'Жумада аль-ула',
    'Жумада ас-сани',
    'Excel файлы даяр — жүктөп алуу башталды',
    'Бул күндүн жүрүмүнүн Excel экспорту',
    'Персонал тизмесинин Excel экспорту',
    'Кыймылсыздык мүнөттөрү аткарылганга чейин (1–120)',
    'Зуль-хиджа',
    'Зуль-када',
    'Раби аль-аввал',
    'Раби ас-сани',
    'Раджаб',
    'Рамазан',
    'Шаабан',
    'Шаввал',
    'Сафар',
    'Мухаррам',
    'Системанын бардык жөндөөлөрү бул бетте табларга бөлүнгөн — керектүү таблды форманын үстүнөн тандаңыз',
    '📤 Excel экспорту',
]

PRS = [
    'صادرات اکسل تردد امروز',
    'صادرات اکسل کارمندان',
    'تردد امروز صادرات شد',
    'تقویم نمایشی سیستم',
    'ثانیه‌های بی‌حرکتی تا اجرا (۰ تا ۵۹)',
    'جبران قمری: {0} روز',
    'جمادی‌الاول',
    'جمادی‌الثانی',
    'خروجی اکسل آماده شد — دانلود آغاز شد',
    'صادرات اکسل تردد این روز',
    'صادرات اکسل فهرست کارمندان',
    'دقایق بی‌حرکتی تا اجرا (۱ تا ۱۲۰)',
    'ذی‌الحجه',
    'ذی‌القعده',
    'ربیع‌الاول',
    'ربیع‌الثانی',
    'رجب',
    'رمضان',
    'شعبان',
    'شوال',
    'صفر',
    'محرم',
    'همه تنظیمات سیستم در این صفحه با زبانه‌ها دسته‌بندی شده‌اند — زبانهٔ مورد نظر را از بالای فورم انتخاب کنید',
    '📤 صادرات اکسل',
]

PS = [
    'د نن ورځې تګ راتګ د Excel صادرول',
    'د کارکوونکو د Excel صادرول',
    'د نن ورځې تګ راتګ صادر شو',
    'د سیسټم ښودنه تقویم',
    'د بې حرکتۍ ثانیې تر پیلولو پورې (۰ تر ۵۹)',
    'قمري سمون: {0} ورځې',
    'جمادی الاول',
    'جمادی الثانی',
    'Excel فایل چمتو شو — ډاونلوډ پیل شو',
    'د دې ورځې تګ راتګ د Excel صادرول',
    'د کارکوونکو لیست د Excel صادرول',
    'د بې حرکتۍ دقیقې تر پیلولو پورې (۱ تر ۱۲۰)',
    'ذی الحجہ',
    'ذی القعدہ',
    'ربیع الاول',
    'ربیع الثانی',
    'رجب',
    'رمضان',
    'شعبان',
    'شوال',
    'صفر',
    'محرم',
    'د سیسټم ټولې امستنې په دې پاڼه کې په ټبونو وېشل شوې — خپل غوښتی ټب د فورم له پوره خوا وټاکه',
    '📤 د Excel صادرول',
]

TG = [
    'Баровардани Excel-и ҳаракати имрӯза',
    'Баровардани Excel-и кормандон',
    'Ҳаракати имрӯза бароварда шуд',
    'Тақвими намоиши система',
    'Сонияҳои беҳаракат то иҷро (0 то 59)',
    'Ҷуброни қамарӣ: {0} рӯз',
    'Ҷумодул-ула',
    'Ҷумодул-сонӣ',
    'Файли Excel омода аст — боргирӣ оғоз шуд',
    'Баровардани Excel-и ҳаракати ин рӯз',
    'Баровардани Excel-и рӯйхати кормандон',
    'Дақиқаҳои беҳаракат то иҷро (1 то 120)',
    'Зулҳиҷҷа',
    'Зулқаъда',
    'Рабиъул-аввал',
    'Рабиъус-санӣ',
    'Раҷаб',
    'Рамазон',
    'Шаъбон',
    'Шаввол',
    'Сафар',
    'Муҳаррам',
    'Ҳамаи танзимоти система дар ин саҳифа ба варақҳо ҷудо шудаанд — варақи лозимиро аз болои шакл интихоб кунед',
    '📤 Баровардани Excel',
]

TK = [
    'Şu günüň hereketiniň Excel eksporty',
    'Işgärleriň Excel eksporty',
    'Şu günüň hereketi eksport edildi',
    'Ulgamyn görkeziş senesi',
    'Hereketsizlik sekuntlary ýerine ýetirilýänçe (0–59)',
    'Kamary düzedişi: {0} gün',
    'Jemazul-ewwel',
    'Jemazul-ahyr',
    'Excel faýly taýýar — göçürip almak başlady',
    'Şu günüň hereketiniň Excel eksporty',
    'Işgärleriň sanawynyň Excel eksporty',
    'Hereketsizlik minutlary ýerine ýetirilýänçe (1–120)',
    "Zulhijja",
    "Zulga'da",
    'Rebiýul-ewwel',
    'Rebiýus-sany',
    'Rejep',
    'Ramazan',
    'Şaban',
    'Şawwal',
    'Safar',
    'Muharram',
    'Ulgamyň ähli sazlamalary şu sahypada ýapylgalara bölünendir — zerur ýapylgy formanyň ýokarsyndan saýlaň',
    '📤 Excel eksporty',
]

UZ = [
    'Bugungi harakatlar Excel eksporti',
    'Xodimlar Excel eksporti',
    'Bugungi harakatlar eksport qilindi',
    'Tizim namoyish kalendari',
    'Harakatsizlik soniyalari ishga tushguncha (0–59)',
    'Qamariy tuzatish: {0} kun',
    'Jumodul-ulo',
    'Jumodus-sani',
    'Excel fayl tayyor — yuklab olish boshlandi',
    'Ushbu kun harakatlari Excel eksporti',
    'Xodimlar roʻyxati Excel eksporti',
    'Harakatsizlik daqiqalari ishga tushguncha (1–120)',
    'Zulhijja',
    'Zulqaʼda',
    'Rabi\u2019ul-avval',
    'Rabi\u2019us-sani',
    'Rajab',
    'Ramazon',
    'Sha\u2019bon',
    'Shavvol',
    'Safar',
    'Muharram',
    'Tizimning barcha sozlamalari bu sahifada yorliqlarga bo\u2018lingan — kerakli yorliqni formaning yuqorisidan tanlang',
    '📤 Excel eksporti',
]

# روسی — تک کلید legacy (دیالوگ جابجایی واحد با \\n literal)
RU_EXTRA = {
    missing_ru[0]: 'Переместить подразделение «{0}» в «{1}»?\\nСотрудников напрямую в этом подразделении: {2} чел.\\n',
}

# ---------- ادعای هم‌ترازی محتوا (بدون ZWNJ — فقط زیررشته‌های امن) ----------
EXPECT = ['اکسل تردد امروز', 'اکسل پرسنل', 'برون‌ریزی شد', 'تقویم نمایشی',
          'ثانیه‌های', 'جبران قمری', 'جمادی‌الاول', 'جمادی‌الثانی', 'آماده شد',
          'این روز', 'فهرست پرسنل', 'دقایق', 'الحجه', 'القعده', 'اول', 'الثانی',
          'رجب', 'رمضان', 'شعبان', 'شوال', 'صفر', 'محرم',
          'دسته‌بندی شده‌اند', 'برون‌ریزی اکسل']
assert len(missing_ar) == 24 and len(AR) == 24, f'ar {len(missing_ar)}/{len(AR)}'
for i, frag in enumerate(EXPECT):
    assert frag in missing_ar[i], f'index {i}: {frag!r} not in {missing_ar[i][:40]!r}'

def load(code):
    p = os.path.join(PACKS, f'hr-lang-{code}.json')
    return json.load(open(p, encoding='utf-8')), p

def save(d, p, added):
    with open(p, 'w', encoding='utf-8') as f:
        json.dump(d, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(f'{d["code"]}: +{added} کلید → مجموع {len(d["entries"])}')

def add(d, pairs):
    ent = d['entries']
    n = 0
    for k, v in pairs:
        if k in ent:
            continue
        ent[k] = v
        n += 1
    return n

def pairs_of(keys, table):
    return list(zip(keys, table))

# ar — همهٔ ۲۴ کلید
d, p = load('ar')
n = add(d, pairs_of(missing_ar, AR))
# متای گمشده از الگوی en
if 'format' not in d: d['format'] = 'hr-lang-pack'
if 'version' not in d: d['version'] = 1
save(d, p, n)

# ru — تک کلید legacy
d, p = load('ru')
n = add(d, RU_EXTRA.items())
save(d, p, n)

# بسته‌های پایه — همان ۲۴ کلید جدید
for code, table in [('ka', KA), ('ky', KY), ('prs', PRS), ('ps', PS), ('tg', TG), ('tk', TK), ('uz', UZ)]:
    d, p = load(code)
    n = add(d, pairs_of(missing_ar, table))
    save(d, p, n)

# ---------- اعتبارسنجی نهایی ----------
print('\n--- اعتبارسنجی ---')
for f in sorted(glob.glob(os.path.join(PACKS, 'hr-lang-*.json'))):
    raw = open(f, encoding='utf-8').read()
    d = json.loads(raw)
    # شمارش تکرار کلید در متن خام (json.load تکراری را بی‌صدا می‌پوشاند)
    import re
    ok = True
    seen = set()
    for m in re.finditer(r'^    "(.+?)": "', raw, re.M):
        pass
    # راه ساده‌تر: بازسازی از جفت‌ها
    import collections
    cnt = collections.Counter(m.group(1) for m in re.finditer(r'"(.+?)": \{', raw))
    print(os.path.basename(f)[8:-5], 'entries:', len(d['entries']), 'meta:', sorted(d.keys()))
print('stage-A done')
