# Release Packaging - Process Bus Insight


## Release documentation PDFs

The Windows portable package includes `Quick Start.pdf` and `User Manual.pdf` at the ZIP root. These files are intentionally tracked in the repository because the release workflow packages them directly.

If the workflow fails with `Required package file missing: docs/QUICK_START.pdf`, the PDF files were not committed. Add them explicitly:

```powershell
git add -f docs/QUICK_START.pdf docs/USER_MANUAL.pdf
git commit -m "Add release PDF documentation"
git push origin main
```


The release workflow creates a Windows x64 portable package for users who want to try the app without Visual Studio. The application is published as a self-contained single EXE.

## Output files

A release creates:

```text
ProcessBusInsight-v<version>-win-x64-portable.zip
SHA256SUMS.txt
```

The ZIP contains:

```text
ProcessBusInsight.exe        single self-contained Windows application
Quick Start.pdf              first-run checklist for Windows users
User Manual.pdf              practical user guide for IEC 61850 capture workflow
README.txt                   short package note
LICENSE.txt
NOTICE.txt
THIRD_PARTY_NOTICES.md
```

The portable ZIP keeps startup clean: users run `ProcessBusInsight.exe` directly.

## Local packaging

Run from the repository root:

```powershell
.\scripts\publish-windows-portable.ps1 -Version "1.2.6"
.\scriptserify-release-package.ps1 -PackageZip ".rtifacts
elease\ProcessBusInsight-v1.2.6-win-x64-portable.zip"
```

## GitHub release workflow

The release workflow can be started manually from the GitHub Actions tab. It also runs on tags matching `v*`.

Manual inputs:

- `version` - release version label, for example `1.2.6`.
- `publish_release` - when `true`, create or update a GitHub Release.
- `prerelease` - mark release as prerelease.
- `draft` - create a draft release.
- `release_notes_file` - markdown file used as release body.

## Runtime prerequisite

Npcap is required on the target Windows machine and is not bundled in the repository.

## CI restore note

The portable release script performs a runtime-specific restore and then publishes with `--no-restore`. It intentionally does not pass `/p:AssemblyName=...` on the command line because MSBuild global properties also flow into referenced projects. Passing the same assembly name into the whole project graph can make NuGet restore report `Ambiguous project name`.
