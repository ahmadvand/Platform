# -*- coding: utf-8 -*-
"""
فاز ۹۴ — رفع مشکل انکودینگ فایل‌های installer برای Windows PowerShell 5.1
مشکل: فایل‌های UTF-8 بدون BOM در powershell.exe ویندوز (5.1) با کدپیج ANSI
(در ویندوز فارسی CP1256) خوانده می‌شوند → حروفی مثل «ل» (D9 84) و «ٔ» (D9 94)
و «—» (E2 80 94) به علامت نقل‌قول جعلی (U+201E/U+201D) تبدیل شده و
رشته‌ها را نیمه‌کاره می‌بندند → خطای ParserError.
راه‌حل: افزودن BOM به فایل‌های ps1/iss و افزودن chcp 65001 به bat.
هیچ تغییری در محتوای کد داده نمی‌شود.
"""
import os
import sys

INSTALLER = "/home/z/my-project/installer"

# ۱) فایل‌هایی که باید BOM بگیرند (ps1 برای PowerShell 5.1 و iss برای Inno Setup 6.0-6.2)
BOM_FILES = [
    "build-installer.ps1",
    "Install-HRPlatform.ps1",
    "update-runner.ps1",
    "HR-Platform-Setup.iss",
]

# ۲) فایل bat: BOM ممنوع (cmd خط اول را خراب می‌کند) اما باید chcp 65001 داشته باشد
BAT_FILE = "build-installer.bat"


def to_crlf(text: str) -> str:
    text = text.replace("\r\n", "\n")
    return text.replace("\n", "\r\n")


def main() -> int:
    ok = True

    # --- BOM برای ps1 / iss ---
    for name in BOM_FILES:
        path = os.path.join(INSTALLER, name)
        raw = open(path, "rb").read()
        if raw.startswith(b"\xef\xbb\xbf"):
            print(f"  = {name}: از قبل BOM دارد")
            continue
        text = raw.decode("utf-8")  # اگر UTF-8 نامعتبر بود اینجا خطا می‌گیریم
        with open(path, "wb") as f:
            f.write(b"\xef\xbb\xbf" + text.encode("utf-8"))
        print(f"  + {name}: BOM اضافه شد ({len(raw)} -> {len(raw)+3} بایت)")

    # --- bat: chcp 65001 + CRLF، بدون BOM ---
    bat_path = os.path.join(INSTALLER, BAT_FILE)
    raw = open(bat_path, "rb").read()
    if raw.startswith(b"\xef\xbb\xbf"):
        raw = raw[3:]
        print(f"  ! {BAT_FILE}: BOM حذف شد (برای cmd باید بدون BOM باشد)")
    text = raw.decode("utf-8")
    if "chcp 65001" not in text:
        lines = text.split("\n")
        # درج chcp بلافاصله بعد از @echo off
        for i, ln in enumerate(lines):
            if ln.strip().lower().replace(" ", "") == "@echooff":
                lines.insert(i + 1, "chcp 65001 >nul")
                break
        else:
            lines.insert(0, "chcp 65001 >nul")
        text = "\n".join(lines)
        print(f"  + {BAT_FILE}: chcp 65001 اضافه شد")
    else:
        print(f"  = {BAT_FILE}: chcp 65001 از قبل دارد")
    with open(bat_path, "wb") as f:
        f.write(to_crlf(text).encode("utf-8"))
    print(f"  + {BAT_FILE}: به CRLF تبدیل شد")

    # --- راستی‌آزمایی نهایی ---
    print("\n--- verification ---")
    for name in BOM_FILES:
        path = os.path.join(INSTALLER, name)
        raw = open(path, "rb").read()
        has_bom = raw.startswith(b"\xef\xbb\xbf")
        try:
            raw.decode("utf-8-sig")
            valid = True
        except UnicodeDecodeError:
            valid = False
        status = "OK" if (has_bom and valid) else "FAIL"
        if status == "FAIL":
            ok = False
        print(f"  [{status}] {name:28s} BOM={has_bom} utf8-valid={valid} size={len(raw)}")

    raw = open(bat_path, "rb").read()
    cond = (not raw.startswith(b"\xef\xbb\xbf")) and b"chcp 65001" in raw and b"\r\n" in raw
    print(f"  [{'OK' if cond else 'FAIL'}] {BAT_FILE:28s} no-BOM={not raw.startswith(b'\\xef\\xbb\\xbf')} chcp={b'chcp 65001' in raw} crlf={b'\\r\\n' in raw}")
    if not cond:
        ok = False

    # اثبات رفع مشکل: با BOM پاورشل 5.1 فایل را UTF-8 می‌خواند؛ بررسی می‌کنیم
    # که فایل UTF-8ِ معتبر است و متنِ درستِ فارسی داخلش سالم است
    txt = open(os.path.join(INSTALLER, "build-installer.ps1"), encoding="utf-8-sig").read()
    assert "اختیاری برای نصب آفلاین" in txt, "متن فارسی خراب شده!"
    assert "کامپایل Inno Setup ناموفق بود" in txt, "متن فارسی خراب شده!"
    print("  [OK] متن فارسی build-installer.ps1 با UTF-8 سالم خوانده می‌شود")

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
