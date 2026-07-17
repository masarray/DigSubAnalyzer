# Contributing to Process Bus Insight

Thank you for helping improve a receive-only IEC 61850 Process Bus engineering tool.

## Licensing and acceptance

Post-transition community revisions are `GPL-3.0-or-later`, and the project maintains a separate commercial licensing path for rights controlled by Ari Sulistiono.

Before merge, contributors must:

1. read and affirmatively accept **CLA Version 1.0, effective 2026-07-17**, in `CONTRIBUTOR-LICENSE-AGREEMENT.md`;
2. add a Developer Certificate of Origin sign-off to every commit;
3. have the legal right and any required employer, customer, educational, sponsor, or organizational authorization to submit the contribution; and
4. identify third-party material, assets, and fixtures together with provenance and license evidence.

Sign commits with:

```text
Signed-off-by: Full Name <email@example.com>
```

The CLA grants rights needed for GPL-compatible publication and a separate commercial path. It does not transfer ownership of the contributor's work. CI records the checked, versioned CLA statement and verifies DCO sign-offs.

## Start here

1. Read `AGENTS.md` for permanent engineering invariants.
2. Search existing issues and pull requests.
3. Use a focused branch and keep changes reviewable.
4. Remove customer, employer, site, device, capture, SCL, MAC/IP, credential, and project-sensitive information from all evidence.

## Development setup

```powershell
git clone https://github.com/masarray/DigSubAnalyzer.git
cd DigSubAnalyzer
dotnet restore .\ProcessBusSuite.sln
dotnet build .\ProcessBusSuite.sln -c Release
dotnet test .\ProcessBusSuite.sln -c Release
python .\scripts\generate-release-pdfs.py --check
python .\scripts\validate-public-content.py
.\scripts\repository-health.ps1
```

## Good contribution areas

- SV, GOOSE, PTP, Ethernet, BER, SCL, and supported-PCAP parser hardening
- Per-stream runtime isolation and coherent snapshot behavior
- Golden-frame, replay, rollover, malformed-input, and multi-stream tests
- UI clarity that reduces visual noise without hiding evidence
- Release signing, source/SBOM provenance, documentation, and field-validation scenarios

## Independent-development and data boundary

External software may be used only as a lawfully licensed black-box interoperability or packet-observation endpoint. Do not copy or mechanically translate unrelated source, API composition, tests, documentation wording, screenshots, icons, reports, UI layouts, binaries, or extracted resources unless the project has documented incorporation and relicensing rights.

Use synthetic or contributor-owned fixtures whenever practical. Real SCL, PCAP, screenshots, logs, and diagnostics require documented sharing rights and sanitization. New visual assets must update `docs/ASSET_PROVENANCE.md`.

Do not submit:

- IEC 61850 operate/control, publisher, or protection-action workflows;
- timing or measurement claims beyond the validated evidence source;
- restricted third-party binaries or proprietary material;
- confidential customer, employer, station, credential, or network data; or
- production captures and project files without explicit redistribution authority.

## Pull-request expectations

A pull request should explain the engineering problem, source of truth, validation method, compatibility impact, operational and data-handling impact, public wording changes, asset provenance, and remaining uncertainty. Runtime changes should include automated regression coverage where practical and a Windows smoke test.

The pull-request template is the minimum checklist, not a substitute for engineering evidence.
