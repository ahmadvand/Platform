# -*- coding: utf-8 -*-
# فاز ۱۰۴ن۲ — دو کلید دیالوگ تأیید با \n واقعی (سوییچ بانک داده / خاموش‌کردن ماژول)
import json, re

PACK = "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-ru.json"
CACHE = "/home/z/my-project/scripts/langpacks/cache2/ru.json"

NL = "\n"  # کلیدِ واقعی در runtime نویسهٔ خط جدید است (C# "\n")
T = {
    "ماژول «{0}» خاموش شود؟" + NL + "منو و صفحه‌های آن بلافاصله پنهان می‌شود؛ داده‌ها محفوظ می‌مانند.":
        "Выключить модуль «{0}»?" + NL + "Его меню и страницы будут немедленно скрыты; данные сохраняются.",
    "تغییر اتصال بانک داده اعمال شود؟" + NL + "بانک جدید به‌صورت خودکار آماده و فعال می‌شود؛ داده‌های بانک فعلی دست‌نخورده می‌مانند.":
        "Применить новое подключение к базе данных?" + NL + "Новая база данных будет автоматически подготовлена и активирована; данные текущей базы останутся без изменений.",
}

pack = json.load(open(PACK, encoding="utf-8"))
cache = json.load(open(CACHE, encoding="utf-8"))
add = {k: v for k, v in T.items() if k not in pack["entries"]}
print("adding:", len(add))
pack["entries"].update(add)
cache.update(T)
json.dump(pack, open(PACK, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
json.dump(cache, open(CACHE, "w", encoding="utf-8"), ensure_ascii=False)
print("pack size:", len(pack["entries"]))

# اعتبارسنجی placeholder
bad = []
for k, v in T.items():
    if sorted(re.findall(r"\{\d+\}", k)) != sorted(re.findall(r"\{\d+\}", v)):
        bad.append(k[:40])
print("placeholders:", "OK" if not bad else bad)
assert not bad
print("i118 COMPLETE")
