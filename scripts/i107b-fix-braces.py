# -*- coding: utf-8 -*-
# فاز ۱۰۷ب — ترمیم ۶۵ مقدار ترجمه با آکولاد نامتوازن ({0) ، «{0» ، {0}} ، نقل‌قولهای دور)
# این خطا در صفحه‌های زبان‌های غیر فارسی FormatException/۵۰۰ می‌داد.
import json, glob, re, os

PACKS = "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks"

# دستی‌ها: (کد زبان، کلید) → مقدار درست
MANUAL = {
    ("ka", "دستور بازکردن راهبند «{0}» صف شود؟"): "ბარიერის «{0}» გახსნის ბრძანება რიგში ჩაიდოს?",
    ("ky", "ثبت {0} برای «{1}» انجام شد"): "«{1}» үчүн {0} жазылды",
    ("ky", "حالت درب «{0}» به «{1}» تغییر کرد"): "«{0}» эшигинин режими «{1}» өзгөрдү",
    ("ky", "درخواست بازدید «{0}» برای شرکت «{1}» ثبت شد"): "«{0}» боюнча «{1}» компаниясына башкаруу суроосу катталды",
    ("tk", "جفت‌کلید جدید ساخته شد — اثر انگشت: {0}. کلید خصوصی را دانلود و در محل امن نگه دارید."):
        "Täze açar jübüýi döredildi — bil: {0}. Hususy açary ýükle we howlu ýerde sakla.",
    ("tk", "درخواست بازدید «{0}» برای شرکت «{1}» ثبت شد"): "«{0}»-niň sapary «{1}» kärhanasy üçin ýazgylandy",
    ("tk", "ثبت {0} برای «{1}» انجام شد"): "«{1}» üçin {0} ýazdy",
    ("tk", "ردیف {0}: واحد سازمانی «{1}» یافت نشد — بدون واحد درج شد"):
        "Sanyr {0}: guruh birleşigi «{1}» tapylmady — birleşiksiz ýazyldy",
}

def repair(v):
    v = v.strip()
    if len(v) >= 2 and v[0] == '"' and v[-1] == '"':
        v = v[1:-1]
    v = v.replace('«{0}}»', '«{0}»').replace('«{1}}»', '«{1}»')
    v = v.replace('«{0}}', '«{0}»').replace('«{1}}', '«{1}»')
    v = re.sub(r'«\{(\d+)»\}?»?', '«{\\1}»', v)          # «{0»  /  «{0»»  →  «{0}»
    v = re.sub(r'(?<!«)\{(\d+)»', '{\\1}', v)             # {0»  →  {0}
    v = re.sub(r'\{(\d+)\)', '{\\1}', v)                 # {0)  →  {0}
    v = re.sub(r'(?<!«)"\{(\d+)"', '«{\\1}»', v)          # "{0"  →  «{0}»
    v = re.sub(r'\{(\d+) (?!})', '{\\1} ', v)            # ({0 rol) → ({0} rol)
    v = re.sub(r'\{(\d+)\.', '{\\1}.', v)                # surat: {0. → {0}.
    return v

fixed = 0
for p in sorted(glob.glob(f"{PACKS}/hr-lang-*.json")):
    d = json.load(open(p, encoding="utf-8"))
    code = d["code"]
    ch = 0
    for k in list(d["entries"].keys()):
        v = d["entries"][k]
        if v.count("{") != v.count("}"):
            if (code, k) in MANUAL:
                nv = MANUAL[(code, k)]
            else:
                nv = repair(v)
            kp = sorted(re.findall(r"\{(\d+)\}", k))
            vp = sorted(re.findall(r"\{(\d+)\}", nv))
            if kp != vp or nv.count("{") != nv.count("}"):
                print("!! SKIP", code, repr(k[:50]), "->", repr(nv[:60]))
                continue
            d["entries"][k] = nv
            ch += 1
    if ch:
        json.dump(d, open(p, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
        fixed += ch
        print(f"[{code}] fixed {ch}")
print("total fixed:", fixed)

# اعتبارسنجی نهایی
left = 0
for p in sorted(glob.glob(f"{PACKS}/hr-lang-*.json")):
    d = json.load(open(p, encoding="utf-8"))
    for k, v in d["entries"].items():
        if v.count("{") != v.count("}"):
            left += 1
            print("REMAIN:", d["code"], repr(k[:50]), repr(v[:50]))
print("remaining imbalanced:", left)
