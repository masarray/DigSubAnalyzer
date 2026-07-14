# Contributing to Process Bus Insight

Thank you for helping improve a receive-only IEC 61850 Process Bus engineering tool.

## Licensing and acceptance

The current community edition is `GPL-3.0-or-later` and the project maintains a separate commercial licensing path.

Before merge, contributors must:

1. read and affirmatively accept `CONTRIBUTOR-LICENSE-AGREEMENT.md`;
2. add a Developer Certificate of Origin sign-off to every commit;
3. have the legal right and any required employer or organizational authorization to submit the contribution; and
4. identify any third-party material together with its provenance and license.

Sign commits with:

```text
Signed-off-by: Full Name <email@example.com>
```

The CLA grants rights needed for GPL-compatible publication and a separate commercial path. It does not transfer ownership of the contributor's work.

## Start here

1. Read `AGENTS.md` for permanent engineering invariants.
2. Search existing issues and pull requests.
3. Use a focused branch and keep changes reviewable.
4. Remove customer, site, device, capture, SCL, MAC/IP, and project-sensitive information from all evidence.

## Development setup

```powershell
git clone https://github.com/masarray/DigSubAnalyzer.git
cd DigSubAnalyzer
dotnet restore .\ProcessBusSuite.sln
dotnet build .\ProcessBusSuite.sln -c Release
dotnet test .\ProcessBusSuite.sln -c Release
.\scripts\repository-health.ps1
```

## Good contribution areas

- SV, GOOSE, PTP, Ethernet, BER, and SCL parser hardening
- Per-stream runtime isolation and coherent snapshot behavior
- Golden-frame, replay, rollover, malformed-input, and multi-stream tests
- UI clarity that reduces visual noise without hiding evidence
- Release automation, documentation, and field-validation scenarios

## Independent-development and data boundary

External software may be used only as a lawfully licensed black-box interoperability or packet-observation endpoint. Do not copy or mechanically translate unrelated source, API composition, tests, documentation wording, screenshots, icons, reports, UI layouts, binaries, or extracted resources unless the project has documented incorporation and relicensing rights.

Use synthetic or contributor-owned fixtures whenever practical. Real SCL, PCAP, screenshots, logs, and diagnostics require documented sharing rights and sanitization.

Do not submit:

- IEC 61850 operate/control, publisher, or protection-action workflows;
- timing or measurement claims beyond the validated evidence source;
- restricted third-party binaries or proprietary material;
- confidential customer, employer, station, credential, or network data; or
- production captures and project files without explicit redistribution authority.

## Pull-request expectations

A pull request should explain the engineering problem, source of truth, validation method, compatibility impact, operational and data-handling impact, public wording changes, and remaining uncertainty. Runtime changes should include automated regression coverage where practical and a Windows smoke test.

The pull-request template is the minimum checklist, not a substitute for engineering evidence.