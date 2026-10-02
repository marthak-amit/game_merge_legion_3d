#!/usr/bin/env python3
"""Builds Resources/Localization/en.json from tools/loc/en.txt and reports Loc keys used in code but missing."""
import json, os, re, sys
root = os.path.join(os.path.dirname(__file__), "..")
src = os.path.join(root, "tools", "loc", "en.txt")
out = os.path.join(root, "Assets", "_Game", "Resources", "Localization", "en.json")
entries = []
keys = set()
for line in open(src, encoding="utf-8"):
    line = line.rstrip("\n")
    if not line.strip() or line.startswith("#"):
        continue
    k, v = line.split("=", 1)
    k = k.strip(); v = v.strip().replace("\\n", "\n")
    entries.append({"key": k, "value": v}); keys.add(k)
json.dump({"entries": entries}, open(out, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
used = set()
pat = re.compile(r'Loc\.(?:Get|Format)\(\s*"([^"]+)"')
for d, _, files in os.walk(os.path.join(root, "Assets", "_Game", "Scripts")):
    for f in files:
        if f.endswith(".cs"):
            used |= set(pat.findall(open(os.path.join(d, f), encoding="utf-8").read()))
missing = sorted(used - keys)
print("keys:", len(keys), "used in code:", len(used))
if missing:
    print("MISSING:", *missing, sep="\n  ")
    sys.exit(1)
