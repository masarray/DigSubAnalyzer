#!/usr/bin/env python3
"""Validate public license, version, claim, and generated-document wording."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "1.4.0-beta.2"
BOUNDARY = "85d43a0fe58a5888a9e8008c168ab76d2333ea87"

TEXT_FILES = [
    "README.md",
    "NOTICE",
    "COMMERCIAL-LICENSE.md",
    "CONTRIBUTOR-LICENSE-AGREEMENT.md",
    "THIRD_PARTY_NOTICES.md",
    "docs/LICENSING.md",
    "docs/index.html",
    "src/ProcessBus.App.Wpf/Views/AboutWindow.xaml",
    "src/ProcessBus.App.Wpf/Views/AboutWindow.xaml.cs",
]

REQUIRED = {
    "README.md": ["GPL-3.0-or-later", BOUNDARY, "COMMERCIAL-LICENSE.md"],
    "NOTICE": ["GPL-3.0-or-later", BOUNDARY, "grants no additional rights"],
    "docs/LICENSING.md": ["GPL-3.0-or-later", BOUNDARY, "SOURCE.md", "sbom.cdx.json"],
    "docs/index.html": [VERSION, "GPL-3.0-or-later", "grants no additional rights"],
    "src/ProcessBus.App.Wpf/Views/AboutWindow.xaml": ["GPL-3.0-or-later", "Separate negotiated and executed agreement", "does not by itself prove"],
    "src/ProcessBus.App.Wpf/Views/AboutWindow.xaml.cs": ["AssemblyInformationalVersionAttribute", "VersionText", "BuildText"],
}

FORBIDDEN = [
    (re.compile(r"Source code is licensed under Apache-2\.0", re.I), "active Apache source-license claim"),
    (re.compile(r"Text=\"Apache-2\.0\"", re.I), "active Apache About value"),
    (re.compile(r"Version 1\.0\.0", re.I), "stale About version"),
    (re.compile(r"Build 2026\.04", re.I), "stale About build"),
    (re.compile(r"oscilloscope-level", re.I), "unqualified oscilloscope-equivalence claim"),
    (re.compile(r"certified timing", re.I), "unqualified certified-timing claim"),
]


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def read(path: str) -> str:
    target = ROOT / path
    if not target.exists():
        fail(f"missing required public file: {path}")
    return target.read_text(encoding="utf-8")


def validate_text() -> None:
    contents = {path: read(path) for path in TEXT_FILES}
    for path, markers in REQUIRED.items():
        for marker in markers:
            if marker not in contents[path]:
                fail(f"{path} is missing required marker: {marker}")
    joined = "\n".join(f"--- {name} ---\n{text}" for name, text in contents.items())
    for pattern, description in FORBIDDEN:
        if pattern.search(joined):
            fail(f"public content contains {description}")
    if "https://spdx.org/licenses/GPL-3.0-or-later.html" not in contents["docs/index.html"]:
        fail("landing-page structured data does not identify GPL-3.0-or-later")


def pdf_text(path: Path) -> str:
    try:
        from pypdf import PdfReader
    except ImportError as exc:
        raise SystemExit("pypdf is required for PDF validation") from exc
    return "\n".join(page.extract_text() or "" for page in PdfReader(str(path)).pages)


def validate_pdfs() -> None:
    for name in ("QUICK_START.pdf", "USER_MANUAL.pdf"):
        path = ROOT / "docs" / name
        if not path.exists():
            fail(f"missing generated release document: docs/{name}")
        text = pdf_text(path)
        for marker in ("Process Bus Insight", VERSION, "GPL-3.0-or-later"):
            if marker not in text:
                fail(f"docs/{name} is missing generated marker: {marker}")
        if re.search(r"Free and open source under Apache-2\.0|Community license\s+Apache-2\.0", text, re.I):
            fail(f"docs/{name} still presents Apache-2.0 as the current license")


def main() -> None:
    validate_text()
    validate_pdfs()
    print("Public wording, licensing, version, and PDF validation: PASS")


if __name__ == "__main__":
    main()
