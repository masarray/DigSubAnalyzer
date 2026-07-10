# Top-Global Repository Upgrade

This package combines the latest tested working tree supplied during development with public-repository hardening for `v1.3.0-beta.1`.

## What is included

- Existing SV/GOOSE/PTP/SCL source and tests from the latest clean working tree
- CI build/test evidence and coverage artifact collection
- CodeQL, dependency review, and Dependabot
- Repository hygiene and version-consistency gate
- Issue forms, pull-request checklist, CODEOWNERS, support, security, conduct, and changelog
- Stream-runtime architecture and release-validation documentation
- Synchronized `1.3.0-beta.1` versioning
- Clean release workflow with checksum and release manifest
- Removal of obsolete installer hooks, duplicated roadmap content, phase-note clutter from active docs, and old product naming

## Safe push workflow

The included script does not overwrite the `.git` folder of your existing checkout. It creates a fresh temporary clone, mirrors this clean package into it, builds/tests, creates a hardening branch, pushes the branch, and creates a pull request when GitHub CLI is available.

Run from the extracted package root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\scripts\push-top-global.ps1
```

Do not merge until GitHub CI, CodeQL, and dependency review are green. The script intentionally pushes a branch instead of writing directly to `main`.
