#!/usr/bin/env python3
"""Fix broken if/else chain in Access/Index.cshtml:
move the orphaned 'else if (tab == "groups")' block (after the final else)
to BEFORE the final else, so Razor compiles a proper chain.
"""
path = "/home/z/my-project/aspnet/src/HrAttendance.Web/Views/Access/Index.cshtml"
with open(path, encoding="utf-8") as f:
    lines = f.read().split("\n")

# 1. Locate the orphaned groups block: from 'else if (tab == "groups")' to EOF closing brace
gi = next(i for i, l in enumerate(lines) if l.strip() == 'else if (tab == "groups")')
# block runs to the last non-empty line (the closing '}' of the groups block)
end = max(i for i, l in enumerate(lines) if l.strip())
assert lines[end].strip() == "}", f"expected closing brace, got: {lines[end]!r}"
groups_block = lines[gi:end + 1]

# 2. Locate the final 'else' (alerts block) that precedes it
ei = gi
while lines[ei].strip() != "else":
    ei -= 1
assert lines[ei].strip() == "else"

# 3. Rebuild: [0..ei) + groups_block + [ei..gi)  (alerts 'else' block stays last)
new_lines = lines[:ei] + groups_block + [""] + lines[ei:gi]
# strip trailing blank lines, keep single trailing newline
while new_lines and not new_lines[-1].strip():
    new_lines.pop()

with open(path, "w", encoding="utf-8") as f:
    f.write("\n".join(new_lines) + "\n")

print(f"moved lines {gi+1}..{end+1} to before line {ei+1} ('else')")
print(f"new chain order:")
for i, l in enumerate(new_lines):
    s = l.strip()
    if s.startswith("@if (tab") or s.startswith("else if (tab") or s == "else":
        print(f"  line {i+1}: {s}")
