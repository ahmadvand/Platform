#!/usr/bin/env python3.13
# ============================================================
# dbq.py — کوئری provider-agnostic برای تست‌ها
# - اگر SQL Server (localhost,1433) در دسترس باشد از آن می‌خواند
# - وگرنه از SQLite (hr.db)
# استفاده:
#   python3.13 scripts/dbq.py "SELECT COUNT(*) FROM Devices"          → یک مقدار
#   python3.13 scripts/dbq.py --json "SELECT Id,Name FROM Devices"    → JSON
# پارامترها: "SELECT .. WHERE Id=%s" --p val1 --p val2
# ============================================================
import sys, json, os

DB = "/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db"
SA_PASSWORD = os.environ.get("MSSQL_SA_PASSWORD", "YourStrong!Passw0rd")


def connect():
    # ترجیح: SQL Server اگر env صریح Sqlite نباشد و پورت در دسترس باشد
    provider = os.environ.get("Database__Provider", "")
    if provider.lower() != "sqlite":
        try:
            import pymssql
            return pymssql.connect(
                server="localhost", port="1433", user="sa",
                password=SA_PASSWORD, database="HrAttendance",
                tds_version="7.4", login_timeout=4,
            ), "mssql"
        except Exception:
            pass
    import sqlite3
    # busy_timeout: تحت دسترسی هم‌زمان وب/تست، قفل دیتابیس خطا نمی‌دهد و صبر می‌کند
    return sqlite3.connect(DB, timeout=15), "sqlite"


def main():
    args = sys.argv[1:]
    as_json = "--json" in args
    if as_json:
        args.remove("--json")
    exec_mode = "--exec" in args
    if exec_mode:
        args.remove("--exec")
    params = []
    while "--p" in args:
        i = args.index("--p")
        params.append(args[i + 1])
        del args[i:i + 2]
    sql = args[0] if args else ""
    # سازگاری placeholder: ? (sqlite) → %s (mssql) بعد از تشخیص پروایدر
    con, kind = connect()
    cur = con.cursor()
    if kind == "mssql":
        q = sql.replace("?", "%s")
        # IFNULL (sqlite) → ISNULL (sql server) قبل از افزودن پیشوند N
        import re
        q = re.sub(r"\bIFNULL\s*\(", "ISNULL(", q, flags=re.I)
        # لیترال‌های رشته‌ای باید N'' باشند تا یونیکد (فارسی) درست مقایسه شود
        q = re.sub(r"(?<!N)'((?:[^']|'')*)'", r"N'\1'", q)
        # ترجمه ساده LIMIT n → TOP n برای SQL Server
        m = re.search(r"\sLIMIT\s+(\d+)\s*$", q, re.I)
        if m:
            q = q[: m.start()] + q[m.end() :]
            q = re.sub(r"^\s*SELECT\s+", f"SELECT TOP {m.group(1)} ", q, count=1, flags=re.I)
    else:
        q = sql.replace("%s", "?")
        # ISNULL (sql server) → IFNULL (sqlite)
        import re
        q = re.sub(r"\bISNULL\s*\(", "IFNULL(", q, flags=re.I)
        # و برعکس: TOP n → LIMIT n برای SQLite
        import re
        m = re.search(r"^\s*SELECT\s+TOP\s+(\d+)\s", q, re.I)
        if m:
            q = re.sub(r"^\s*SELECT\s+TOP\s+\d+\s", "SELECT ", q, count=1, flags=re.I) + f" LIMIT {m.group(1)}"
    cur.execute(q, params if kind == "mssql" else params)
    if exec_mode:
        con.commit()
        print("ok")
        return
    rows = cur.fetchall()
    cols = [d[0] for d in cur.description] if cur.description else []
    con.close()
    if as_json:
        print(json.dumps([dict(zip(cols, r)) for r in rows], default=str, ensure_ascii=False))
    else:
        if rows and len(rows[0]) == 1:
            print(rows[0][0])
        else:
            print(json.dumps([list(r) for r in rows], default=str, ensure_ascii=False))


if __name__ == "__main__":
    main()
