#!/usr/bin/env python3
"""Enforce versioned CLA affirmation and DCO sign-off for pull requests."""

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from pathlib import Path

CLA_MARKER = "I have read and affirmatively accept CONTRIBUTOR-LICENSE-AGREEMENT.md (CLA Version 1.0, effective 2026-07-17)"
DCO_RE = re.compile(r"^Signed-off-by:\s+.+\s+<[^>]+>\s*$", re.IGNORECASE | re.MULTILINE)


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def run(*args: str) -> str:
    result = subprocess.run(args, check=False, text=True, capture_output=True)
    if result.returncode != 0:
        fail(f"Command failed: {' '.join(args)}\n{result.stdout}\n{result.stderr}")
    return result.stdout.strip()


def load_event() -> dict:
    event_path = os.getenv("GITHUB_EVENT_PATH")
    if not event_path:
        print("No GITHUB_EVENT_PATH; contribution-governance check skipped outside GitHub Actions.")
        return {}
    return json.loads(Path(event_path).read_text(encoding="utf-8"))


def main() -> None:
    event = load_event()
    pr = event.get("pull_request")
    if not pr:
        print("Not a pull-request event; contribution-governance check skipped.")
        return

    body = pr.get("body") or ""
    checked_marker = f"- [x] {CLA_MARKER}"
    if checked_marker.lower() not in body.lower():
        fail(f"Pull request body must contain the checked CLA affirmation:\n{checked_marker}")

    base_sha = pr["base"]["sha"]
    head_sha = pr["head"]["sha"]
    run("git", "fetch", "--no-tags", "origin", base_sha, head_sha)

    commit_shas = [x for x in run("git", "rev-list", "--reverse", f"{base_sha}..{head_sha}").splitlines() if x]
    if not commit_shas:
        fail("Pull request contains no commits to validate.")

    failures: list[str] = []
    for sha in commit_shas:
        message = run("git", "show", "-s", "--format=%B", sha)
        if not DCO_RE.search(message):
            failures.append(sha)

    if failures:
        fail("Every pull-request commit must contain a DCO Signed-off-by line. Missing: " + ", ".join(failures))

    print(f"Contribution governance: PASS ({len(commit_shas)} commits, CLA Version 1.0).")


if __name__ == "__main__":
    main()
