# -*- coding: utf-8 -*-
# فاز ۹۱ — پیچیدن مقادیر وضعیت/شمارشی خام (از DB) در HrT.T تا چندزبانه شوند.
# قواعد:
#   • فقط «رندر مستقیم» @X.Prop — نه مقایسه‌ها (==/switch) و نه value="..." (مقدار خام باید بماند)
#   • after-char باید < یا (فاصله + ·) یا · باشد
#   • Statusهای انگلیسیِ فنی (pending/acked/...) دست نمی‌خورند
import os, re

ROOT = "/home/z/my-project/aspnet/src"
PROPS = "Status|Direction|IdDocType|VehicleType|MaritalStatus|Purpose|Kind|Result|Type"
RE_PLAIN = re.compile(r'@([A-Za-z_][A-Za-z0-9_]*)\.(' + PROPS + r')(?=[<\s·&])')
RE_PAREN = re.compile(r'@\(([A-Za-z_][A-Za-z0-9_.]*)\.(' + PROPS + r')\)')

changed = {}

for dp, dn, fn in os.walk(ROOT):
    if "/obj/" in dp or "/bin/" in dp:
        continue
    for f in fn:
        if not f.endswith(".cshtml"):
            continue
        p = os.path.join(dp, f)
        src = open(p, encoding="utf-8").read()
        out_lines = []
        n = 0
        for line in src.split("\n"):
            if "switch {" in line:
                out_lines.append(line); continue

            def repl(m, line=line):
                var, prop = m.group(1), m.group(2)
                after = line[m.end():m.end() + 4]
                rest = line[m.end():]
                # مقایسه/شرط؟
                if re.match(r'\s*(==|!=|\?|switch|\)\s*==)', rest):
                    return m.group(0)
                ok = (after and after[0] == '<') or after.startswith(' ·') or (after and after[0] == '·') \
                     or after.startswith('</') or ((after and after[0] == ' ') and '·' in after[:3])
                if ok:
                    # کد فنی انگلیسی؟
                    if prop == "Status" and ('"pending"' in line or '"acked"' in line):
                        return m.group(0)
                    return f'@HrT.T({var}.{prop})'
                return m.group(0)

            def repl_paren(m, line=line):
                rest = line[m.end():m.end() + 12]
                if rest.startswith(' ?? '):
                    var, prop = m.group(1), m.group(2)
                    return f'@({var}.{prop} is null ? "—" : HrT.T({var}.{prop}))'
                after = line[m.end():m.end() + 4]
                if after and (after[0] == '<' or after.startswith('</')):
                    return f'@HrT.T({m.group(1)}.{m.group(2)})'
                return m.group(0)

            new = RE_PLAIN.sub(repl, line)
            new = RE_PAREN.sub(repl_paren, new)
            if new != line:
                changed.setdefault(p.replace(ROOT + "/", ""), []).append(line.strip()[:110])
            out_lines.append(new)
        if n or any(changed.get(p.replace(ROOT + "/", "")) and changed[p.replace(ROOT + "/", "")] and out_lines[i] != src.split("\n")[i] for i in range(min(len(out_lines), len(src.split("\n"))))):
            pass
        # بازنویسی فقط اگر تغییری واقعاً رخ داده
        if "\n".join(out_lines) != src:
            open(p, "w", encoding="utf-8").write("\n".join(out_lines))
            print(f"{len(changed.get(p.replace(ROOT + '/', '')) or [])}  {p.replace(ROOT + '/', '')}")
print("total files:", len(changed))

# مورد خاص: فرم دستگاه — متن گزینهٔ @k باید ترجمه شود
p = ROOT + "/modules/HrModules.Devices/Views/Devices/_DeviceForm.cshtml"
src = open(p, encoding="utf-8").read()
old = '@DeviceDefaults.Kinds[k].Icon @k</option>'
if old in src:
    src = src.replace(old, '@DeviceDefaults.Kinds[k].Icon @HrT.T(k)</option>')
    open(p, "w", encoding="utf-8").write(src)
    print("special: _DeviceForm option text")
