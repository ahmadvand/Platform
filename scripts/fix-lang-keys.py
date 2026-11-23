#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""هم‌ترازی دقیق کلید/مقدار ردیف‌های HrLangEntries با بسته‌های تعبیه‌شده.
علت: ImportEmbeddedLangPacksAsync کلید را Trim می‌کند ولی HrLangStore.Lookup
دقیق (بدون Trim) جستجو می‌کند → کلیدهای دارای فاصلهٔ ابتدا/انتها (رشته‌های
مجوز/خطا مثل «خطا در ارتباط با سرور: ») در بانک بریده ذخیره شده و در زبان‌های
پویا به انگلیسی fallback می‌شدند. این اسکریپت کلید را به شکل کامل بسته برمی‌گرداند.
"""
import sqlite3, json, glob

DB = "/home/z/my-project/aspnet/sc-run/hr.db"
PACKS = "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-*.json"

db = sqlite3.connect(DB, timeout=30)
for f in sorted(glob.glob(PACKS)):
    d = json.load(open(f, encoding="utf-8"))
    code, entries = d["code"], d["entries"]
    rows = dict(db.execute("SELECT Key, Value FROM HrLangEntries WHERE LangCode=?", (code,)))
    realign = value_sync = inserted = 0
    for k, v in entries.items():
        if k in rows:
            if rows[k] != v:
                db.execute("UPDATE HrLangEntries SET Value=? WHERE LangCode=? AND Key=?", (v, code, k))
                value_sync += 1
            continue
        t = k.strip()
        if t and t in rows:
            db.execute("UPDATE HrLangEntries SET Key=?, Value=? WHERE LangCode=? AND Key=?", (k, v, code, t))
            realign += 1
        else:
            db.execute("INSERT INTO HrLangEntries (LangCode, Key, Value) VALUES (?,?,?)", (code, k, v))
            inserted += 1
    db.commit()
    n = db.execute("SELECT COUNT(*) FROM HrLangEntries WHERE LangCode=?", (code,)).fetchone()[0]
    print(f"{code:4s}: rows={n} realign={realign} value-sync={value_sync} inserted={inserted}")
db.close()
print("DONE")
