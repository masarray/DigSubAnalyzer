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
COMMERCIAL-LICENSE.md
COPYRIGHT.md
TRADEMARK.md
THIRD_PARTY_NOTICES.md
Licensing.md
```

Npcap is required on the target Windows machine for live raw Ethernet capture and is not bundled.

## License boundary

Packages built from post-transition revisions are `GPL-3.0-or-later`. `LICENSE.txt` must contain GNU GPL version 3. A historical Apache license file must not be included as a current package license.

The final active Apache-2.0 revision is commit `85d43a0fe58a5888a9e8008c168ab76d2333ea87`, preserved on `archive/apache-2.0-final`. Historical packages retain the grants under which they were distributed.

`COMMERCIAL-LICENSE.md` is an invitation to negotiate separate proprietary or OEM terms and grants no additional rights by itself.

## Version source

`Directory.Build.props` defines the default public-beta version. README package examples, landing-page structured data, release workflow defaults, release notes, and the package script must remain synchronized. The repository-health script checks this contract.

## Required documentation

The portable package includes `docs/QUICK_START.pdf` and `docs/USER_MANUAL.pdf`. They are intentionally tracked even though generated PDFs are otherwise ignored.

```powershell
git add -f docs/QUICK_START.pdf docs/USER_MANUAL.pdf
```

## Local packaging

```powershell
.\scripts\repository-health.ps1 -ExpectedVersion "1.4.0-beta.2"
.\scripts\publish-windows-portable.ps1 -Version "1.4.0-beta.2"
.\scripts\verify-release-package.ps1 -PackageZip ".\artifacts\release\ProcessBusInsight-v1.4.0-beta.2-win-x64-portable.zip"
```

## GitHub workflows

The candidate workflow runs for `stabilization/*`, `architecture/*`, and `legal/*` pull requests when relevant files change. It restores, builds, tests, packages, verifies the ZIP and legal documents, creates a candidate manifest, and uploads the candidate without publishing a public release.

The release workflow runs manually or for tags matching `v*`. It restores, builds, tests, packages, verifies the ZIP, produces a checksum and release manifest, uploads workflow evidence, and optionally creates a GitHub Release.

## Packaging design notes

The publish script performs a runtime-specific restore and then publishes with `--no-restore`. It splits prerelease versions into numeric assembly/file version and informational/package version. Package verification also checks the current GPL text, commercial notice boundary, historical-license documentation, single-root-EXE rule, required PDFs, and absence of a historical Apache license presented as current.