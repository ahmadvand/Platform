#!/usr/bin/env python3
# toc-pages.py (v2) — شماره صفحهٔ واقعی عناوین از OUTLINE خودِ PDF واسط (pypdf) گرفته می‌شود
# و سپس با add_toc_placeholders.py --entries به فیلد فهرست تزریق می‌گردد.
# مصرف: python3 toc-pages.py <docx> <passA.pdf>
import sys, os, json

DOCX_SCRIPTS = "/home/z/my-project/skills/docx/scripts"

def outline_entries(pdf):
    from pypdf import PdfReader
    r = PdfReader(pdf)
    out = []
    def walk(items, depth):
        for it in items:
            if isinstance(it, list):
                walk(it, depth + 1)
            else:
                try:
                    pg = r.get_destination_page_number(it) + 1
                except Exception:
                    continue
                title = (it.title or "").strip()
                lvl = 1 if depth == 0 else min(depth + 1, 3)
                if title:
                    out.append({"level": lvl, "text": title, "abs": pg})
    if r.outline:
        walk(r.outline, 0)
    return out

def main(docx, pdf):
    ents = outline_entries(pdf)
    if len(ents) < 4 or len({e["abs"] for e in ents}) < 3:
        print(f"ERROR: outline mapping looks broken ({len(ents)} entries) — aborting")
        sys.exit(2)
    body_start = ents[0]["abs"]
    entries = [{"level": e["level"], "text": e["text"], "page": str(max(1, e["abs"] - body_start + 1))} for e in ents]
    r = subprocess_run(docx, entries)
    print("entries:", len(entries), "| script rc:", r)
    sys.exit(r or 0)

def subprocess_run(docx, entries):
    import subprocess
    r = subprocess.run([sys.executable, os.path.join(DOCX_SCRIPTS, "add_toc_placeholders.py"),
                        docx, "--entries", json.dumps(entries, ensure_ascii=False)],
                       capture_output=True, text=True)
    if r.stdout.strip():
        print(r.stdout.strip().splitlines()[-1])
    if r.returncode != 0 and r.stderr.strip():
        print("ERR:", r.stderr.strip()[-300:])
    return r.returncode

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
