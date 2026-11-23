#!/usr/bin/env python3
# دوبل-فورک دامینایزشن برای بقا در برابر reaping درخت پروسهٔ شل ابزار.
# تجربهٔ 2026-10-30: setsid ساده بین دو فراخوانِ ابزار کشته می‌شد (کل درخت فرزندِ
# شلِ ابزار reap می‌شود)؛ دوبل-فورک باعث می‌شود پروسهٔ نهایی به PID 1
# والد‌پذیرفته شود و در پیمایشِ درختِ فرزندانِ شل دیده نشود.
import os
import sys

SCRIPT = "/home/z/my-project/scripts/start-web.sh"


def _fork_and_die_parent():
    pid = os.fork()
    if pid > 0:
        os._exit(0)


def _daemonize():
    _fork_and_die_parent()          # ۱) جدا شدن از شل فراخوان
    os.setsid()                     # ۲) سشن و گروه جدید
    _fork_and_die_parent()          # ۳) فورک دوم — دیگر هیچ رهبر سشنی نیست


_daemonize()
os.close(0)
os.close(1)
os.close(2)
for fd in ("/dev/null",):
    fd = os.open(fd, os.O_RDWR)
    os.dup2(fd, 0)
    os.dup2(fd, 1)
    os.dup2(fd, 2)
os.execv("/bin/bash", ["bash", SCRIPT])
