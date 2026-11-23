#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""چاپ خطوط واقعی هیت‌های ui برای بازبینی و ویرایش دستی"""
import json, sys

d = json.load(open("/home/z/my-project/scripts/audit-i18n-classified.json", encoding="utf-8"))
ROOT = "/home/z/my-project/aspnet/src/"

files = sys.argv[1:]
for f, hits in d["ui"].items():
    if files and not any(x in f for x in files):
        continue
    print("=" * 30, f)
    try:
        lines = open(ROOT + f, encoding="utf-8").readlines()
    except Exception:
        continue
    for h in hits:
        print(f"{h[0]:5d}: {lines[h[0]-1].rstrip()[:150]}")
