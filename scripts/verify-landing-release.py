#!/usr/bin/env python3
"""Static release-CTA safety contract (stdlib only).

The homepage MUST always offer a working, direct stable Windows setup EXE
without needing JavaScript. app.js may upgrade this fallback to the newest
public GitHub Release when the GitHub API is available in a browser.
"""
from __future__ import annotations

from html.parser import HTMLParser
from pathlib import Path
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / "docs" / "index.html"
INSTALLER_ROOT = "https://github.com/masarray/DigSubAnalyzer/releases/download/"
version = ET.parse(ROOT / "Directory.Build.props").getroot().find("./PropertyGroup/VersionPrefix")
assert version is not None and version.text, "Release VersionPrefix missing"
tag = "v" + version.text.strip()
fallback = f"{INSTALLER_ROOT}{tag}/ProcessBusInsight-{tag}-win-x64-setup.exe"

class Links(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.anchors: list[dict[str, str]] = []

    def handle_starttag(self, name, attrs):
        if name == "a":
            self.anchors.append(dict(attrs))

content = SITE.read_text(encoding="utf-8")
parser = Links()
parser.feed(content)
installer = [a for a in parser.anchors if a.get("data-release-download") == "installer"]
assert len(installer) >= 3, "Header, hero, and download-card must link to EXE"
assert all(a.get("href") == fallback for a in installer), "Every primary CTA must directly download the stable EXE"
assert not any("portable" in a.get("href", "").lower() for a in installer), "Installer CTA must never link to portable ZIP"
assert not any(a.get("href", "").endswith("/releases") for a in installer), "Installer CTA must not open a releases list"
assert "Download installer (.exe)" in content and "Download portable ZIP" not in content
assert all(a.get("href", "").startswith(INSTALLER_ROOT + tag + "/") for a in parser.anchors
           if a.get("data-release-download") in {"portable", "checksum"}), "Secondary downloads must match version"

scripts = re.findall(r'<script type="application/ld\+json">(.*?)</script>', content, flags=re.S)
assert scripts, "Missing structured app metadata"
metadata = json.loads(scripts[0])
assert metadata["softwareVersion"] == version.text.strip(), "Structured software version is stale"
assert metadata["downloadUrl"] == fallback, "Structured download URL must be the setup EXE"

js = (ROOT / "docs" / "app.js").read_text(encoding="utf-8")
for marker in ("releases/latest", "applyLatestStableRelease", "safeReleaseAsset",
               "setReleaseLinks('installer'", "release.prerelease", "release.draft"):
    assert marker in js, f"Dynamic latest stable EXE resolver missing: {marker}"
assert "data-release-version" in content, "Missing live stable version label"
css = (ROOT / "docs" / "styles.css").read_text(encoding="utf-8")
assert ".download-alternatives" in css and ".download-installer-card" in css
readme = (ROOT / "README.md").read_text(encoding="utf-8")
assert fallback in readme, "README stable installer link is stale"
print(f"Landing installer download contract: PASS ({tag}, 3+ direct EXE CTAs, API fallback)")
