# Release Packaging — Process Bus Insight

The release workflow produces a Windows x64 self-contained **Inno Setup 6 EXE installer (primary)**, plus an optional portable ZIP. The installer supports in-place upgrades and repairs via a stable AppId and includes both offline PDF manuals inside the installed program folder. It is currently unsigned and does not automatically download updates.

## Release files

```text
ProcessBusInsight-v<version>-win-x64-setup.exe
ProcessBusInsight-v<version>-win-x64-portable.zip
SHA256SUMS.txt
release-manifest.json
```

The ZIP contains:

```text
ProcessBusInsight.exe
Quick Start.pdf
User Manual.pdf
Quick Start.md
User Manual.md
ManualSources.json
Install and Update.md
README.txt
LICENSE.txt
NOTICE.txt
COMMERCIAL-LICENSE.md
COPYRIGHT.md
TRADEMARK.md
THIRD_PARTY_NOTICES.md
Licensing.md
```

Npcap is required on the target Windows machine for live raw Ethernet capture and is not bundled. Setup provides nonblocking guidance if the driver is not detected because offline analysis remains available.

The Inno Setup default location is `C:\Program Files\Process Bus Insight`. `User Manual.pdf`, `Quick Start.pdf`, `Install and Update.md`, and the read-only `Bench/` tools are physically installed there. Start Menu shortcuts link to the application and both PDFs. An optional desktop shortcut is available. To update, close the analyzer and rerun the newer installer; Inno remembers the install directory and updates in place. Reinstalling the same version can repair missing files. The uninstaller does not use broad rules to delete user captures or SCL data.

GitHub Actions verifies checksums and performs a real silent install, repair, PDF hash comparison, and uninstall. Bench performance and third-party antivirus compatibility are not implied by that test.

## License boundary

Packages built from post-transition revisions are `GPL-3.0-or-later`. `LICENSE.txt` must contain GNU GPL version 3. A historical Apache license file must not be included as a current package license.

The final active Apache-2.0 revision is commit `85d43a0fe58a5888a9e8008c168ab76d2333ea87`, preserved on `archive/apache-2.0-final`. Historical packages retain the grants under which they were distributed.

`COMMERCIAL-LICENSE.md` is an invitation to negotiate separate proprietary or OEM terms and grants no additional rights by itself.

## Version source

`Directory.Build.props` defines the default public-beta version. README package examples, landing-page structured data, release workflow defaults, release notes, and the package script must remain synchronized. The repository-health script checks this contract.

## Required documentation

The packaged **Quick Start.pdf** and **User Manual.pdf** are regenerated from their corresponding Markdown sources at build time, not copied from historically tracked PDF files. `ManualSources.json` records SHA256 for each Markdown source and generated PDF. Both the portable and installer verification reject stale/mismatched pairs.

Older tracked PDFs may remain in the repository for historical website references but are never packaged as the current offline manuals. Build requires Python with pinned `reportlab==4.4.9`; end users do not need Python or ReportLab.

```powershell
git add -f docs/QUICK_START.pdf docs/USER_MANUAL.pdf
```

## Local packaging

```powershell
.\scripts\repository-health.ps1 -ExpectedVersion "1.4.1"
python -m pip install reportlab==4.4.9
python .\scripts\render-offline-manuals.py
.\scripts\publish-windows-portable.ps1 -Version "1.4.1"
.\scripts\verify-release-package.ps1 -PackageZip ".\artifacts\release\ProcessBusInsight-v1.4.1-win-x64-portable.zip"
.\scripts\publish-windows-installer.ps1 -Version "1.4.1"
.\scripts\verify-windows-installer.ps1 -Installer ".\artifacts\release\ProcessBusInsight-v1.4.1-win-x64-setup.exe" -StageDir ".\artifacts\package\ProcessBusInsight-v1.4.1-win-x64-portable"
```

## GitHub workflows

The candidate workflow runs for supported pull-request branches (including `fix/*`, `feat/*`, `stabilization/*`, `architecture/*`, `legal/*`) when relevant files change. It restores, builds, tests, renders current offline PDFs, packages, verifies the ZIP/manual source hashes and legal documents, creates a candidate manifest, and uploads the candidate without publishing a public release.

The release workflow builds on manual dispatch or tags matching `v*`. **A tag push cannot publish a public release.** A public release is an explicit manual `publish_release=true` from `main` requiring `field_acceptance_verified=true`; this must correspond to the current final installer and documented physical soak gate. The workflow restores, builds, tests, packages the Inno EXE and optional ZIP, smoke-tests installation/repair/uninstall, produces checksums and a release manifest, and uploads evidence before publication.

## Packaging design notes

The publish script performs a runtime-specific restore and then publishes with `--no-restore`. It splits prerelease versions into numeric assembly/file version and informational/package version. Package verification also checks the current GPL text, commercial notice boundary, historical-license documentation, single-root-EXE rule, required PDFs, and absence of a historical Apache license presented as current.