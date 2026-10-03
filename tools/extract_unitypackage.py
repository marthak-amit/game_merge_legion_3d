#!/usr/bin/env python3
"""Extracts a .unitypackage (gzip'd tar of <guid>/{asset,asset.meta,pathname}) into a project folder.
Used by CI to install TMP Essential Resources without an interactive Unity import.
Usage: extract_unitypackage.py <package> [project_dir]"""
import os, shutil, sys, tarfile, tempfile

pkg = sys.argv[1]
project = sys.argv[2] if len(sys.argv) > 2 else "."
tmp = tempfile.mkdtemp()
with tarfile.open(pkg, "r:gz") as tar:
    tar.extractall(tmp)
count = 0
for guid in os.listdir(tmp):
    d = os.path.join(tmp, guid)
    pn = os.path.join(d, "pathname")
    if not os.path.isfile(pn):
        continue
    dest = os.path.join(project, open(pn, encoding="utf-8").readline().strip())
    asset, meta = os.path.join(d, "asset"), os.path.join(d, "asset.meta")
    if os.path.isfile(asset):
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(asset, dest)
        count += 1
    elif os.path.isfile(meta):
        os.makedirs(dest, exist_ok=True)  # folder entry
    if os.path.isfile(meta):
        shutil.copyfile(meta, dest + ".meta")
shutil.rmtree(tmp)
print(f"extracted {count} assets from {os.path.basename(pkg)}")
