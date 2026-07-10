# Release Packaging — Process Bus Insight

The release workflow creates a Windows x64 self-contained portable package for users who do not have Visual Studio or a separate .NET runtime.

## Release files

```text
ProcessBusInsight-v<version>-win-x64-portable.zip
SHA256SUMS.txt
release-manifest.json
```

The ZIP contains:

```text
ProcessBusInsight.exe
Quick Start.pdf
User Manual.pdf
README.txt
LICENSE.txt
NOTICE.txt
THIRD_PARTY_NOTICES.md
```

Npcap is required on the target Windows machine and is not bundled.

## Version source

`Directory.Build.props` defines the default public-beta version. The README package example, landing-page structured data, release workflow default, release notes, and package script must remain synchronized. The repository health script checks this contract.

## Required documentation

The portable package includes `docs/QUICK_START.pdf` and `docs/USER_MANUAL.pdf`. They are intentionally tracked even though generated PDFs are otherwise ignored.

```powershell
git add -f docs/QUICK_START.pdf docs/USER_MANUAL.pdf
```

## Local packaging

```powershell
.\scripts\repository-health.ps1 -ExpectedVersion "1.3.0-beta.1"
.\scripts\publish-windows-portable.ps1 -Version "1.3.0-beta.1"
.\scripts\verify-release-package.ps1 -PackageZip ".\artifacts\release\ProcessBusInsight-v1.3.0-beta.1-win-x64-portable.zip"
```

## GitHub workflow

The release workflow runs manually or for tags matching `v*`. It restores, builds, tests, packages, verifies the ZIP, produces a checksum and release manifest, uploads workflow evidence, and optionally creates or updates a GitHub Release.

Manual inputs:

- `version` — semantic version label
- `publish_release` — create/update the GitHub Release
- `prerelease` — mark the release as prerelease
- `draft` — create a draft release
- `release_notes_file` — Markdown release body

## Packaging design notes

The publish script performs a runtime-specific restore and then publishes with `--no-restore`. It splits prerelease versions into numeric assembly/file version and informational/package version so a label such as `1.3.0-beta.1` remains valid while Windows assembly versions stay numeric.
