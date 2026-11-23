# -*- coding: utf-8 -*-
# فاز ۱۰۰ — به‌روزرسانی بستهٔ زبان عربی برای برچسب/راهنمای جدید محافظ صفحه
# ساختار فایل: {"code","title","dir","entries":{...}} — کلیدها باید داخل entries باشند.
# نسخهٔ ۲: حذف آلودگی سطح ریشهٔ نسخهٔ ۱ + اعمال درست روی entries
import json, io

OLD = "دقایق بی‌حرکتی تا اجرا (۱ تا ۱۲۰)"
NEW_MIN = ("دقایق بی‌حرکتی تا اجرا (۰ تا ۱۲۰ — صفر = فقط ثانیه)",
           "دقائق الخمول قبل البدء (٠ إلى ١٢٠ — صفر = ثوانٍ فقط)")
NEW_HINT = ("اجرای چندثانیه‌ای: برای آزمایش سریع، دقیقه را ۰ بگذارید و فقط ثانیه را تنظیم کنید (مثلاً ۰ دقیقه و ۱۰ ثانیه). کمینهٔ مجموع ۵ ثانیه است — مقدارهای کمتر به ۵ ثانیه گرد می‌شوند.",
            "التفعيل خلال ثوانٍ: لاختبار سريع، اجعل الدقائق صفرًا واضبط الثواني فقط (مثلًا: ٠ دقيقة و ١٠ ثوانٍ). الحد الأدنى للمجموع ٥ ثوانٍ — تُقرَّب القيم الأصغر إلى ٥ ثوانٍ.")
META_KEYS = {"code", "title", "dir", "entries"}

FILES = [
    "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-ar.json",
    "/home/z/my-project/aspnet/sc-run/App_Data/langpacks/hr-lang-ar.json",
]

for path in FILES:
    with io.open(path, encoding="utf-8") as f:
        data = json.load(f, object_pairs_hook=dict)
    entries = data.get("entries")
    assert isinstance(entries, dict), "unexpected structure: " + path
    changed = []
    # پاک‌سازی آلودگی نسخهٔ ۱ (کلیدهای اشتباه در سطح ریشه)
    for k in list(data.keys()):
        if k not in META_KEYS:
            del data[k]
            changed.append("-root-pollution")
    if OLD in entries:
        del entries[OLD]
        changed.append("-old")
    if NEW_MIN[0] not in entries:
        entries[NEW_MIN[0]] = NEW_MIN[1]
        changed.append("+min")
    else:
        entries[NEW_MIN[0]] = NEW_MIN[1]
    if NEW_HINT[0] not in entries:
        entries[NEW_HINT[0]] = NEW_HINT[1]
        changed.append("+hint")
    else:
        entries[NEW_HINT[0]] = NEW_HINT[1]
    with io.open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=0)
        f.write("\n")
    print(path.split("aspnet/")[1], "->", ",".join(changed) or "no-change", "| entries:", len(entries))
