# Release Packaging — Process Bus Insight

The release workflow creates a Windows x64 portable package for users who want to try the app without Visual Studio. By default, the application payload is published as a self-contained single EXE.

## Output files

A release package contains:

```text
ProcessBusInsight-v<version>-win-x64-portable.zip
SHA256SUMS.txt
```

The ZIP contains:

```text
app/ProcessBusInsight.exe     single self-contained Windows application
Start-ProcessBusInsight.bat   simple launcher
README_QUICK_START.txt        user-facing run instructions
LICENSE
NOTICE
THIRD_PARTY_NOTICES.md
```

## Local packaging

Run from the repository root:

```powershell
.\scripts\publish-windows-portable.ps1 -Version "1.2.0-public-beta"
.\scripts\verify-release-package.ps1 -PackageZip ".\artifacts\release\ProcessBusInsight-v1.2.0-public-beta-win-x64-portable.zip"
```

## GitHub release workflow

The release workflow can be started manually from the GitHub Actions tab. It also runs on tags matching `v*`.

Manual inputs:

- `version` — release version label, for example `1.2.0-public-beta`.
- `publish_release` — when `true`, create or update a GitHub Release.
- `prerelease` — mark release as prerelease.
- `draft` — create a draft release.
- `release_notes_file` — markdown file used as release body.

## Runtime prerequisite

Npcap is required on the target Windows machine and is not bundled in the repository.
