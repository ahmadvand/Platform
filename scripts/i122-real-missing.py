# -*- coding: utf-8 -*-
# فاز ۱۰۵ب — universe واقعی + جاافتادهٔ واقعی هر زبان (هرس سریع درخت)
import json, re, os, sqlite3

OUT = "/home/z/my-project/scripts/langpacks"
pat = re.compile(r'(?:HrT\.)?[TF]\(\s*"((?:[^"\\]|\\.)*)"')
keys = set()
files = 0
SKIP = {"dl", "obj", "bin", ".git", "node_modules", "binwriter"}
for base, dirs, fnames in os.walk("/home/z/my-project/aspnet/src"):
    dirs[:] = [d for d in dirs if d not in SKIP]
    for f in fnames:
        if f.endswith((".cs", ".cshtml")):
            files += 1
            try:
                src = open(os.path.join(base, f), encoding="utf-8").read()
            except Exception:
                continue
            for m in pat.finditer(src):
                k = m.group(1)
                if re.search(r"[\u0600-\u06FF]", k):
                    keys.add(k)
print("source keys:", len(keys), "files:", files, flush=True)

con = sqlite3.connect("file:/home/z/my-project/aspnet/sc-run/hr.db?mode=ro", uri=True)
dbk = {r[0] for r in con.execute("SELECT DISTINCT Key FROM HrLangEntries")}
uni = keys | dbk
json.dump(sorted(uni), open(f"{OUT}/universe.json", "w"), ensure_ascii=False)
print("universe:", len(uni))

# مصنوع‌های شناخته‌شده: شناسه‌های انگلیسی (T جدولی DbInit) + قالب‌های عمومی + واریانت literal-\n
NL = "\n"
FALSEP = {
    " ({0})", "{0}: {1}",
    "ماژول «{0}» خاموش شود؟\\nمنو و صفحه\u200cهای آن بلافاصله پنهان می\u200cشود؛ داده\u200cها محفوظ می\u200cمانند.",
    "تغییر اتصال بانک داده اعمال شود؟\\nبانک جدید به\u200cصورت خودکار آماده و فعال می\u200cشود؛ داده\u200cهای بانک فعلی دست\u200cنخورده می\u200cمانند.",
}
eng_re = re.compile(r"[A-Za-z0-9_\.\- ]+")

result = {}
for code in ["ar", "ka", "ky", "prs", "ps", "tg", "tk", "uz", "ru", "en"]:
    e = json.load(open(f"/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-{code}.json", encoding="utf-8"))["entries"]
    miss = [k for k in uni if k not in e]
    real = [k for k in miss if k not in FALSEP and not eng_re.fullmatch(k)]
    result[code] = real
    print(f"{code}: pack={len(e)} real_missing={len(real)}")
    for k in real:
        print("   ", repr(k[:90]))
json.dump(result, open(f"{OUT}/real-missing.json", "w"), ensure_ascii=False)
