#!/usr/bin/env python3.13
# ============================================================
# فاز ۱۱۲ — افزودن کلیدهای جدید UI به بسته‌های en/ru و درون‌ریزی پنلی
# کلیدها مستقیماً از سورس فاز ۱۱۲ استخراج می‌شوند؛ ترجمه‌های غایب
# از دیکشنری زیر درج می‌شوند (کلید غایب در دیکشنری = گزارش).
# ============================================================
import json, re, sys, subprocess, pathlib

ROOT = pathlib.Path("/home/z/my-project")
SRC = ROOT / "aspnet/src"
FILES = [
    SRC / "Platform.Web/Views/Data/Index.cshtml",
    SRC / "Platform.Web/Views/Data/ImportForm.cshtml",
    SRC / "Platform.Web/Views/Data/ImportPreview.cshtml",
    SRC / "Platform.Web/Views/Data/ImportResult.cshtml",
    SRC / "Platform.Web/Modules/Core/Controllers/DataController.cs",
    SRC / "Platform.Web/Services/DataExchange.cs",
    SRC / "Platform.Web/Views/Settings/Index.cshtml",
    SRC / "Platform.Web/Views/Shared/_Layout.cshtml",
    SRC / "modules/HrModules.Org/Views/Holidays/Index.cshtml",
    SRC / "modules/HrModules.Org/Controllers/HolidaysController.cs",
    SRC / "modules/HrModules.Devices/Views/Biometrics/Index.cshtml",
    SRC / "modules/HrModules.Devices/Controllers/BiometricsController.cs",
]
RX = re.compile(r'(?:HrT\.)?[TF]\(\s*"((?:[^"\\]|\\.)*)"')

# فقط کلیدهای «فاز ۱۱۲» — واژگان جدید این فاز
NEW = {
    # ---- تقویم تعطیلات ----
    "(تقویم میلادی)": {"en": "(Gregorian calendar)", "ru": "(григорианский календарь)"},
    "(تقویم قمری)": {"en": "(Hijri calendar)", "ru": "(лунный календарь)"},
    "(تقویم شمسی)": {"en": "(Solar Hijri calendar)", "ru": "(солнечный календарь хиджры)"},
    # ---- انتخابگر جدولی پرسنل ----
    "{0} پرسنل فعال در سامانه — جدولِ صفحه‌بندی‌شدهٔ زیر را مرور کنید و هر ردیف را تیک بزنید؛ برای ارسال همه هم می‌توانید از دکمهٔ «انتخاب همهٔ پرسنل» استفاده کنید.": {
        "en": "{0} active employees in the system — browse the paginated table below and tick each row; to send everyone you can also use the \"Select all employees\" button.",
        "ru": "Активных сотрудников в системе: {0} — просмотрите таблицу с разбивкой на страницы ниже и отметьте нужные строки; чтобы отправить всех, используйте кнопку «Выбрать всех сотрудников»."},
    "انتخاب": {"en": "Select", "ru": "Выбор"},
    "واحد سازمانی": {"en": "Department", "ru": "Подразделение"},
    "هویتی": {"en": "Identity", "ru": "Данные пользователя"},
    "کارت": {"en": "Card", "ru": "Карта"},
    # ---- محافظ صفحه ----
    "محافظ صفحه (اسکرین‌سیور باران ماتریکس)": {"en": "Screen saver (Matrix rain)", "ru": "Экранная заставка («Матрица»)"},
    "بعد از بیکاریِ … (۰ = خاموش)": {"en": "After idle time of… (0 = off)", "ru": "После бездействия… (0 = выключено)"},
    "ثانیه": {"en": "seconds", "ru": "секунд"},
    "دقیقه": {"en": "minutes", "ru": "минут"},
    "پس از بیکاری کاربر تا این مدت، کل صفحه با «باران ماتریکس» به‌صورت محافظ صفحه درمی‌آید — با حروفِ زبانی که هر کاربر برای رابط انتخاب کرده (فارسی/عربی/روسی/…). هر حرکت ماوس یا کلید آن را می‌بندد؛ هیچ نشست یا داده‌ای منقضی نمی‌شود و در دستگاه‌های واقعی هم روی مانیتور سالن‌ها می‌تواند به‌عنوان محافظ نمایشگر کار کند.": {
        "en": "After the user has been idle for this long, the whole screen turns into a \"Matrix rain\" screen saver — using letters of the language each user has chosen for the UI (Persian/Arabic/Russian/…). Any mouse move or key press closes it; no session or data expires, and it also works as a display protector for real wall monitors.",
        "ru": "Если пользователь бездействует указанное время, весь экран превращается в заставку «Матрица» — буквами того языка, который выбрал пользователь интерфейса (персидский/арабский/русский/…). Любое движение мыши или нажатие клавиши закрывает её; ни сеанс, ни данные не завершаются, и на реальных мониторах залов она работает как защита дисплея."},
    "محافظ صفحه فعال است — برای بازگشت، ماوس را حرکت دهید یا کلیدی بزنید": {
        "en": "Screen saver active — move the mouse or press any key to return",
        "ru": "Заставка активна — двигайте мышью или нажмите любую клавишу для возврата"},
    # ---- هاب داده ----
    "درون‌ریزی و برون‌بری داده": {"en": "Data Import & Export", "ru": "Импорт и экспорт данных"},
    "مهاجرت داده از و به هر نرم‌افزار دیگر": {"en": "Migrate data to and from any other software", "ru": "Перенос данных из любых других программ и обратно"},
    "قالب‌ها:": {"en": "Formats:", "ru": "Форматы:"},
    "خانواده‌های داده:": {"en": "Data families:", "ru": "Семейств данных:"},
    "برای مهاجرت از نرم‌افزار قبلی: خروجیِ آن نرم‌افزار را در قالب ستون‌های همین سامانه دربیاورید (ساده‌ترین راه: ویرایش خروجی اکسل هر جدول در همین صفحه) و در همین صفحه درون‌ریزی کنید. برای واگذاری داده به نرم‌افزار دیگر هم همین خروجی‌ها را بدهید.": {
        "en": "To migrate from your previous software: reshape its export into this system's column layout (easiest way: edit the Excel export of any table on this page) and import it here. Hand the same exports to another software to move data out.",
        "ru": "Для переноса из прежней программы: приведите её выгрузку к колонкам этой системы (проще всего — отредактировать Excel-выгрузку нужной таблицы на этой странице) и импортируйте здесь. Эти же выгрузки передаются в другую программу."},
    "داده": {"en": "Data", "ru": "Данные"},
    "رکوردها": {"en": "Records", "ru": "Записей"},
    "کلید هم‌آوری": {"en": "Upsert key", "ru": "Ключ сопоставления"},
    "درون‌ریزی": {"en": "Import", "ru": "Импорт"},
    "برون‌بری": {"en": "Export", "ru": "Экспорт"},
    "اکسل": {"en": "Excel", "ru": "Excel"},
    "قالب خالی": {"en": "Empty template", "ru": "Пустой шаблон"},
    "فقط برون‌بری": {"en": "Export only", "ru": "Только экспорт"},
    "درون‌ریزی داده": {"en": "Import data", "ru": "Импорт данных"},
    "← بازگشت به هاب داده": {"en": "← Back to data hub", "ru": "← Вернуться в центр данных"},
    "۱) آپلود فایل": {"en": "1) Upload file", "ru": "1) Загрузка файла"},
    "فایل داده (xlsx / csv / json)": {"en": "Data file (xlsx / csv / json)", "ru": "Файл данных (xlsx / csv / json)"},
    "ستون‌های فایل باید نام ویژگی‌های سامانه باشند — ساده‌ترین راه: خروجی اکسل این جدول را از هاب بگیرید، داده‌های نرم‌افزار قبلی را داخل همان ستون‌ها بچینید و همین فایل را بیاورید.": {
        "en": "File columns must use the system's property names — easiest way: take the Excel export of this table from the hub, arrange the old software's data into the same columns and bring the file back.",
        "ru": "Колонки файла должны называться как свойства системы — проще всего: скачать Excel-выгрузку таблицы из центра, разложить данные старой программы по тем же колонкам и загрузить файл."},
    "رکورد موجود با ستون «{0}» پیدا و ویرایش می‌شود؛ ردیف بدون آن، رکورد جدید می‌سازد.": {
        "en": "Existing records are found and updated by the \"{0}\" column; rows without it create new records.",
        "ru": "Существующие записи находятся и обновляются по колонке «{0}»; строки без неё создают новые записи."},
    "۲) ستون‌های این خانوادهٔ داده": {"en": "2) Columns of this data family", "ru": "2) Колонки этого семейства"},
    "{0} ستون": {"en": "{0} columns", "ru": "Колонок: {0}"},
    "پیش‌نمایش درون‌ریزی": {"en": "Import preview", "ru": "Предпросмотр импорта"},
    "فایل:": {"en": "File:", "ru": "Файл:"},
    "{0} ردیف": {"en": "{0} rows", "ru": "Строк: {0}"},
    "← فایل دیگر": {"en": "← Another file", "ru": "← Другой файл"},
    "ثبت انجام نمی‌شود — خطاهای زیر را برطرف کنید:": {"en": "Nothing will be committed — fix the errors below:", "ru": "Запись не выполняется — исправьте ошибки ниже:"},
    "← بازگشت و اصلاح فایل": {"en": "← Back and fix the file", "ru": "← Вернуться и исправить файл"},
    "فایل معتبر است — {0} ردیف آمادهٔ ثبت. جدول زیر پیش‌نمایش است؛ با تأیید، همهٔ ردیف‌ها با هم‌آوری ثبت می‌شوند.": {
        "en": "The file is valid — {0} rows ready to commit. The table below is a preview; on confirmation all rows are committed with upsert.",
        "ru": "Файл корректен — {0} строк готовы к записи. Таблица ниже — предпросмотр; после подтверждения все строки записываются с сопоставлением."},
    "پیش‌نمایش ردیف‌ها": {"en": "Row preview", "ru": "Предпросмотр строк"},
    "حداکثر {0} ردیف از {1}": {"en": "At most {0} rows of {1}", "ru": "Не более {0} строк из {1}"},
    "تأیید و ثبت {0} ردیف": {"en": "Confirm and commit {0} rows", "ru": "Подтвердить и записать {0} строк"},
    "انصراف": {"en": "Cancel", "ru": "Отмена"},
    "ثبت {0} ردیف در «{1}»؟ رکوردهای موجود با کلید هم‌آوری ویرایش می‌شوند.": {
        "en": "Commit {0} rows to \"{1}\"? Existing records will be updated by the upsert key.",
        "ru": "Записать {0} строк в «{1}»? Существующие записи будут обновлены по ключу сопоставления."},
    "گزارش درون‌ریزی": {"en": "Import report", "ru": "Отчёт об импорте"},
    "← هاب داده": {"en": "← Data hub", "ru": "← Центр данных"},
    "رکورد جدید": {"en": "New records", "ru": "Новых записей"},
    "به‌روزرسانی شد": {"en": "Updated", "ru": "Обновлено"},
    "خطا / نادیده‌گرفته": {"en": "Errors / skipped", "ru": "Ошибки / пропущено"},
    "گزارش خطاها": {"en": "Error report", "ru": "Отчёт об ошибках"},
    "درون‌ریزی بدون خطا انجام شد ✓": {"en": "Import completed without errors ✓", "ru": "Импорт выполнен без ошибок ✓"},
    "خانوادهٔ داده": {"en": "data family", "ru": "семейство данных"},
    "{0} خانوادهٔ داده": {"en": "{0} data families", "ru": "Семейств данных: {0}"},
    "Id": {"en": "Id", "ru": "Id"},
    "درون‌ریزی «{0}»": {"en": "Import \"{0}\"", "ru": "Импорт «{0}»"},
    "کلید هم‌آوری:": {"en": "Upsert key:", "ru": "Ключ сопоставления:"},
    "پیش‌نمایش و اعتبارسنجی": {"en": "Preview & validate", "ru": "Предпросмотр и проверка"},
    "پیش‌نمایش درون‌ریزی «{0}»": {"en": "Import preview of \"{0}\"", "ru": "Предпросмотр импорта «{0}»"},
    "گزارش درون‌ریزی «{0}»": {"en": "Import report of \"{0}\"", "ru": "Отчёт об импорте «{0}»"},
    # ---- پیام‌های کنترلر/موتور ----
    "خانوادهٔ «{0}» درون‌ریزی ندارد (فقط برون‌بری) — دادهٔ لاگ/محاسباتی از مسیر رسمی خودش ساخته می‌شود": {
        "en": "\"{0}\" cannot be imported (export only) — log/computed data is produced through its own official flow",
        "ru": "«{0}» нельзя импортировать (только экспорт) — журнальные/расчётные данные создаются собственными процессами системы"},
    "خانوادهٔ «{0}» درون‌ریزی ندارد": {"en": "\"{0}\" cannot be imported", "ru": "«{0}» недоступно для импорта"},
    "فایلی انتخاب نشده است": {"en": "No file selected", "ru": "Файл не выбран"},
    "داده‌ای برای ثبت نیست": {"en": "No data to commit", "ru": "Нет данных для записи"},
    "دادهٔ ارسالی نامعتبر است": {"en": "Submitted data is invalid", "ru": "Переданные данные недействительны"},
    "تعداد ردیف نامعتبر است (مجاز: ۱ تا {0})": {"en": "Invalid row count (allowed: 1 to {0})", "ru": "Недопустимое число строк (от 1 до {0})"},
    "درون‌ریزی «{0}» انجام شد — {1} جدید، {2} به‌روزرسانی": {"en": "Import of \"{0}\" done — {1} new, {2} updated", "ru": "Импорт «{0}» выполнен — новых: {1}, обновлено: {2}"},
    "{0} ردیف نادیده گرفته شد": {"en": "{0} rows were skipped", "ru": "Пропущено строк: {0}"},
    "درون‌ریزی «{0}» با خطا همراه بود — {1} جدید، {2} به‌روزرسانی، {3} خطا (گزارش در پایین صفحه)": {
        "en": "Import of \"{0}\" finished with errors — {1} new, {2} updated, {3} errors (report at the bottom of the page)",
        "ru": "Импорт «{0}» завершён с ошибками — новых: {1}, обновлено: {2}, ошибок: {3} (отчёт внизу страницы)"},
    "فایل خالی است — حداقل سربرگ و یک ردیف لازم است": {"en": "The file is empty — at least a header row and one data row are required", "ru": "Файл пуст — нужны как минимум строка заголовков и одна строка данных"},
    "قالب فایل پشتیبانی نمی‌شود — xlsx، csv یا json بفرستید": {"en": "File format not supported — send xlsx, csv or json", "ru": "Формат файла не поддерживается — отправьте xlsx, csv или json"},
    "خواندن فایل اکسل ناموفق بود": {"en": "Reading the Excel file failed", "ru": "Не удалось прочитать файл Excel"},
    "خواندن فایل CSV ناموفق بود": {"en": "Reading the CSV file failed", "ru": "Не удалось прочитать файл CSV"},
    "خواندن JSON ناموفق بود": {"en": "Reading the JSON failed", "ru": "Не удалось прочитать JSON"},
    "ساختار JSON باید آرایه یا شیء دارای «rows» باشد": {"en": "JSON must be an array or an object with a \"rows\" member", "ru": "JSON должен быть массивом или объектом с полем «rows»"},
    "حداکثر {0} ردیف در هر فایل درون‌ریزی مجاز است (فایل: {1} ردیف)": {
        "en": "At most {0} rows per import file are allowed (file: {1} rows)",
        "ru": "В одном файле импорта допускается не более {0} строк (в файле: {1})"},
    "ستون «{0}» در این خانوادهٔ داده وجود ندارد — ستون‌های معتبر = سربرگ قالب یا خروجی اکسل همین صفحه": {
        "en": "Column \"{0}\" does not exist in this data family — valid columns are the template header or the Excel export of this page",
        "ru": "Колонки «{0}» нет в этом семействе — допустимые колонки: заголовок шаблона или Excel-выгрузка этой страницы"},
    "ردیف {0}: مقدار ستون «{1}» قابل تبدیل به {2} نیست: {3}": {
        "en": "Row {0}: value of column \"{1}\" cannot be converted to {2}: {3}",
        "ru": "Строка {0}: значение колонки «{1}» нельзя преобразовать в {2}: {3}"},
    "ردیف {0}: کلید «{1}» در فایل تکراری است — فقط اولین ردیف ثبت شد": {
        "en": "Row {0}: key \"{1}\" is duplicated in the file — only the first row was committed",
        "ru": "Строка {0}: ключ «{1}» дублируется в файле — записана только первая строка"},
    "ردیف {0}: کلید تجاری «{1}» خالی است — ردیف نادیده گرفته شد": {
        "en": "Row {0}: business key \"{1}\" is empty — the row was skipped",
        "ru": "Строка {0}: ключ «{1}» пуст — строка пропущена"},
    "ردیف {0}: {1} [{2}]": {"en": "Row {0}: {1} [{2}]", "ru": "Строка {0}: {1} [{2}]"},
    "ثبت در بانک ناموفق بود — هیچ ردیفی ذخیره نشد": {"en": "Committing to the database failed — no rows were saved", "ru": "Не удалось записать в базу — ни одна строка не сохранена"},
    "ستون حساس «{0}» نادیده گرفته شد": {"en": "Sensitive column \"{0}\" was ignored", "ru": "Чувствительная колонка «{0}» проигнорирована"},
    "تاریخ نامعتبر (الگو: 2026-03-21 یا 1404/07/26)": {"en": "Invalid date (patterns: 2026-03-21 or 1404/07/26)", "ru": "Неверная дата (форматы: 2026-03-21 или 1404/07/26)"},
    "راهنما:": {"en": "Guide:", "ru": "Подсказка:"},
    "ستون‌ها دقیقاً نام ویژگی‌های سامانه‌اند — ساده‌ترین راه، ویرایش خروجی اکسل همین صفحه است.": {
        "en": "Columns are exactly the system's property names — the easiest way is editing this page's Excel export.",
        "ru": "Колонки — это в точности имена свойств системы; проще всего отредактировать Excel-выгрузку этой страницы."},
    "ستون Id خالی = رکورد جدید.": {"en": "Empty Id column = a new record.", "ru": "Пустая колонка Id = новая запись."},
    "ستون Id خالی = رکورد جدید؛ رکورد موجود با ستون {0} پیدا و ویرایش می‌شود.": {
        "en": "Empty Id column = a new record; existing records are found and updated by the {0} column.",
        "ru": "Пустая колонка Id = новая запись; существующие записи находятся и обновляются по колонке {0}."},
    "تاریخ نامعتبر است": {"en": "Invalid date", "ru": "Неверная дата"},
    # ---- عناوین/گروه‌های کاتالوگ داده ----
    "واحدهای سازمانی": {"en": "Departments", "ru": "Подразделения"},
    "پست‌های سازمانی": {"en": "Positions", "ru": "Должности"},
    "شرکت‌ها": {"en": "Companies", "ru": "Компании"},
    "شیفت‌ها": {"en": "Shifts", "ru": "Смены"},
    "تعطیلات رسمی": {"en": "Official holidays", "ru": "Официальные праздники"},
    "دستگاه‌های تردد": {"en": "Attendance devices", "ru": "Терминалы учёта"},
    "کاربران سامانه": {"en": "System users", "ru": "Пользователи системы"},
    "نقش‌ها": {"en": "Roles", "ru": "Роли"},
    "کاتالوگ دسترسی‌ها": {"en": "Permission catalog", "ru": "Каталог прав доступа"},
    "کارت‌های شناسایی": {"en": "ID cards", "ru": "Идентификационные карты"},
    "ارباب رجوع (مهمان/مراجع)": {"en": "Visitors (guests/applicants)", "ru": "Посетители (гости/заявители)"},
    "پیمانکاران": {"en": "Contractors", "ru": "Подрядчики"},
    "خودروهای پارکینگ": {"en": "Parking vehicles", "ru": "Автомобили парковки"},
    "اقلام فرمول‌ساز حقوق": {"en": "Payroll formula items", "ru": "Статьи формулы зарплаты"},
    "پارامترهای سالانه حقوق": {"en": "Annual payroll parameters", "ru": "Годовые параметры зарплаты"},
    "قواعد اعلان": {"en": "Notification rules", "ru": "Правила уведомлений"},
    "گروه‌های تردد": {"en": "Access groups", "ru": "Группы доступа"},
    "مناطق کنترل تردد": {"en": "Access zones", "ru": "Зоны контроля доступа"},
    "گیت‌ها": {"en": "Gates", "ru": "Турникеты"},
    "پارکینگ‌ها": {"en": "Parking lots", "ru": "Парковки"},
    "گیت‌های پارکینگ": {"en": "Parking gates", "ru": "Ворота парковки"},
    "پالت‌های رنگی": {"en": "Color palettes", "ru": "Цветовые палитры"},
    "دوربین‌های پلاک‌خوان": {"en": "ANPR cameras", "ru": "Камеры распознавания номеров"},
    "رکوردهای تردد (تاریخچه)": {"en": "Attendance records (history)", "ru": "Записи учёта (история)"},
    "درخواست‌های مرخصی": {"en": "Leave requests", "ru": "Заявления на отпуск"},
    "مأموریت‌ها": {"en": "Missions", "ru": "Командировки"},
    "مجوزهای اضافه‌کاری": {"en": "Overtime permits", "ru": "Разрешения на переработку"},
    "درخواست‌های اصلاح تردد": {"en": "Attendance correction requests", "ru": "Заявления на исправление учёта"},
    "فیش‌های حقوق": {"en": "Payslips", "ru": "Расчётные листки"},
    "سطرهای فیش حقوق": {"en": "Payslip lines", "ru": "Строки расчётных листков"},
    "لیست‌های حقوق": {"en": "Payroll runs", "ru": "Ведомости зарплаты"},
    "عیدی و پایه سنوات": {"en": "Annual bonus & seniority", "ru": "Годовая премия и стаж"},
    "تسویه‌حساب‌ها": {"en": "Settlements", "ru": "Окончательные расчёты"},
    "تنظیمات محاسبه حقوق": {"en": "Payroll calculation settings", "ru": "Настройки расчёта зарплаты"},
    "تنظیمات سامانه": {"en": "System settings", "ru": "Настройки системы"},
    "تنظیمات گیت‌وی": {"en": "Gateway settings", "ru": "Настройки шлюза"},
    "تنظیمات اعلان": {"en": "Notification settings", "ru": "Настройки уведомлений"},
    "زبان‌های نصب‌شده": {"en": "Installed languages", "ru": "Установленные языки"},
    "متن‌های ترجمه": {"en": "Translation strings", "ru": "Строки переводов"},
    "پیوند کاربر-نقش": {"en": "User-role links", "ru": "Связи пользователь-роль"},
    "پیوند نقش-دسترسی": {"en": "Role-permission links", "ru": "Связи роль-право"},
    "محدودیت رکوردی نقش‌ها": {"en": "Role row-level scopes", "ru": "Ограничения записей ролей"},
    "اعضای گروه تردد": {"en": "Access group members", "ru": "Члены групп доступа"},
    "گیت‌های گروه تردد": {"en": "Access group gates", "ru": "Турникеты групп доступа"},
    "مجوزهای تردد پرسنل": {"en": "Employee access permissions", "ru": "Права прохода сотрудников"},
    "کاربران ثبت‌شده روی دستگاه‌ها": {"en": "Users enrolled on devices", "ru": "Пользователи, зарегистрированные на устройствах"},
    "قالب‌های هویتی دستگاه‌ها": {"en": "Device biometric templates", "ru": "Биометрические шаблоны устройств"},
    "کارت‌های مهمان": {"en": "Guest cards", "ru": "Гостевые карты"},
    "قراردادهای پرسنل": {"en": "Employee contracts", "ru": "Договоры сотрудников"},
    "حکم‌های پرسنل": {"en": "Employee decrees", "ru": "Приказы сотрудников"},
    "لاگ ممیزی": {"en": "Audit log", "ru": "Журнал аудита"},
    "هشدارهای امنیتی": {"en": "Security alerts", "ru": "Оповещения безопасности"},
    "لاگ اعلان‌ها": {"en": "Notification log", "ru": "Журнал уведомлений"},
    "دستورهای دستگاه": {"en": "Device commands", "ru": "Команды устройств"},
    "لاگ تردد خام دستگاه‌ها": {"en": "Raw device punch log", "ru": "Журнал первичных считываний"},
    "حافظهٔ آفلاین دستگاه‌ها": {"en": "Device offline memory", "ru": "Автономная память устройств"},
    "رویدادهای کنترل تردد": {"en": "Access events", "ru": "События контроля доступа"},
    "رویدادهای پارکینگ": {"en": "Parking events", "ru": "События парковки"},
    "لاگ بازدیدها": {"en": "Visit log", "ru": "Журнал посещений"},
    "دستورهای پلاک‌خوان": {"en": "ANPR commands", "ru": "Команды камер ANPR"},
    "منابع انسانی": {"en": "Human resources", "ru": "Кадры"},
    "مدیریت سیستم": {"en": "System administration", "ru": "Администрирование"},
    "حضور و غیاب": {"en": "Attendance", "ru": "Учёт рабочего времени"},
    "دستگاه‌ها و گیت‌وی": {"en": "Devices & gateway", "ru": "Устройства и шлюз"},
    "کنترل تردد": {"en": "Access control", "ru": "Контроль доступа"},
    "پارکینگ": {"en": "Parking", "ru": "Парковка"},
    "حقوق و دستمزد": {"en": "Payroll", "ru": "Заработная плата"},
    "سایر داده‌ها": {"en": "Other data", "ru": "Прочие данные"},
    "پرسنل": {"en": "Employees", "ru": "Сотрудники"},
}

def extract_keys():
    keys = []
    for f in FILES:
        txt = f.read_text(encoding="utf-8")
        for m in RX.finditer(txt):
            k = m.group(1)
            k = k.replace("\\\"", "\"").replace("\\\\", "\\")
            keys.append(k)
    return dict.fromkeys(keys)  # مرتب و یکتا

def main():
    keys = list(extract_keys())
    missing_dict = [k for k in keys if k not in NEW]
    print(f"کلیدهای سورس فاز ۱۱۲: {len(keys)} — بدون ترجمه در دیکشنری: {len(missing_dict)}")
    for k in missing_dict[:30]:
        print("  MISS:", k[:90])
    langs = sys.argv[1:] if len(sys.argv) > 1 else ["en", "ru"]
    for lang in langs:
        p = SRC / f"Platform.Web/App_Data/langpacks/hr-lang-{lang}.json"
        pack = json.loads(p.read_text(encoding="utf-8"))
        entries = pack.setdefault("entries", {})
        added = 0
        for k in keys:
            tr = NEW.get(k, {}).get(lang)
            if tr and entries.get(k) != tr:
                entries[k] = tr
                added += 1
        pack["title"] = {"en": "English", "ru": "Русский"}.get(lang, pack.get("title", lang))
        p.write_text(json.dumps(pack, ensure_ascii=False, indent=1), encoding="utf-8")
        # کپی sc-run برای درون‌ریزی
        dst = ROOT / f"aspnet/sc-run/App_Data/langpacks/hr-lang-{lang}.json"
        dst.write_text(json.dumps(pack, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"{lang}: {added} کلید افزوده شد (مجموع: {len(entries)})")
        subprocess.run(["cp", str(p), str(ROOT / f"scripts/langpacks/cache2/{lang}.json")], check=False)

if __name__ == "__main__":
    main()
