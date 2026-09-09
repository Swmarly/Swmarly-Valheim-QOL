#!/usr/bin/env python3
import json
import os
import shutil
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(ROOT, "dist")
SRC = os.path.join(ROOT, "src", "bin", "SwmarlyValheimQOL.dll")
TS = os.path.join(ROOT, "thunderstore")


def main():
    if not os.path.exists(SRC):
        raise SystemExit("Build first: src/bin/SwmarlyValheimQOL.dll is missing")
    with open(os.path.join(TS, "manifest.json",), encoding="utf-8") as handle:
        manifest = json.load(handle)
    version = manifest["version_number"]
    os.makedirs(DIST, exist_ok=True)
    stage = os.path.join(DIST, "stage-SwmarlyValheimQOL")
    shutil.rmtree(stage, ignore_errors=True)
    os.makedirs(stage)

    shutil.copy2(SRC, os.path.join(stage, "SwmarlyValheimQOL.dll"))
    for filename in ("manifest.json", "README.md", "CHANGELOG.md", "icon.png"):
        source = os.path.join(TS, filename)
        if not os.path.exists(source):
            raise SystemExit(f"Missing required Thunderstore file: {source}")
        shutil.copy2(source, os.path.join(stage, filename))
    for filename in ("LICENSE", "THIRD_PARTY.md"):
        source = os.path.join(ROOT, filename)
        if os.path.exists(source):
            shutil.copy2(source, os.path.join(stage, filename))

    output = os.path.join(DIST, f"SwmarlyValheimQOL-{version}.zip")
    if os.path.exists(output):
        os.remove(output)
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for filename in sorted(os.listdir(stage)):
            archive.write(os.path.join(stage, filename), filename)
    shutil.rmtree(stage)
    print(f"{output} ({os.path.getsize(output)} bytes)")


if __name__ == "__main__":
    main()
