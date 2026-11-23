#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""wrap بازوهای شرطی (ternary ?/switch =>) و نمایش CommandLabels با HrT.T
مقایسه‌ها (== "فعال") و کلیدهای switch دست‌نخورده می‌مانند."""
import glob, re, os

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")

FA = "[\u0600-\u06FF]"
rx_switch = re.compile(rf'(=>\s*)"([^"]*{FA}[^"]*)"')
rx_ternary = re.compile(rf'(?<![?\w])\?(\s*)"([^"]*{FA}[^"]*)"')
rx_cmdlabels = re.compile(r'@?(DeviceOps|ParkingOps)\.CommandLabels\.GetValueOrDefault\(([^()]*(?:\([^()]*\))?[^()]*)\)')

keys = []
n_sw = n_te = n_cmd = 0
files = 0
for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    before = src
    def sw(m):
        global n_sw; n_sw += 1; keys.append(m.group(2))
        return f'{m.group(1)}HrT.T("{m.group(2)}")'
    def te(m):
        global n_te; n_te += 1; keys.append(m.group(2))
        return f'? {m.group(1)}HrT.T("{m.group(2)}")'
    def cmd(m):
        global n_cmd; n_cmd += 1
        return f'@HrT.T({m.group(1)}.CommandLabels.GetValueOrDefault({m.group(2)}))'
    src = rx_switch.sub(sw, src)
    src = rx_ternary.sub(te, src)
    src = rx_cmdlabels.sub(cmd, src)
    if src != before:
        open(p, "w", encoding="utf-8").write(src)
        files += 1
print(f"files={files} switch={n_sw} ternary={n_te} cmdlabels={n_cmd}")
