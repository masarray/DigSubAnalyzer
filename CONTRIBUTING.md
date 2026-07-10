# Contributing to Process Bus Insight

Thank you for helping improve a receive-only IEC 61850 Process Bus engineering tool.

## Start here

1. Read [`AGENTS.md`](AGENTS.md) for permanent engineering invariants.
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

## Product boundaries

Do not add:

- IEC 61850 operate/control workflows
- protection-action or publisher behavior
- timing claims beyond the validated timestamp source
- restricted third-party binaries
- unsanitized customer/project evidence

## Pull-request expectations

A pull request should explain the engineering problem, source of truth, validation method, compatibility impact, and any remaining uncertainty. Runtime changes should include automated regression coverage where practical and a Windows smoke test.

The pull-request template is the minimum checklist, not a substitute for engineering evidence.
