# Process Bus Insight (DigSubAnalyzer)

[![CI](https://github.com/masarray/DigSubAnalyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/ci.yml)
[![Runtime Stability](https://github.com/masarray/DigSubAnalyzer/actions/workflows/runtime-stability.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/runtime-stability.yml)
[![Runtime Architecture](https://github.com/masarray/DigSubAnalyzer/actions/workflows/runtime-architecture.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/runtime-architecture.yml)
[![CodeQL](https://github.com/masarray/DigSubAnalyzer/actions/workflows/codeql.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/codeql.yml)
[![GitHub Pages](https://github.com/masarray/DigSubAnalyzer/actions/workflows/pages.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/pages.yml)
[![Release Package](https://github.com/masarray/DigSubAnalyzer/actions/workflows/release-package.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/release-package.yml)
[![Latest Release](https://img.shields.io/github/v/release/masarray/DigSubAnalyzer?include_prereleases&label=release)](https://github.com/masarray/DigSubAnalyzer/releases)
[![License: GPL-3.0-or-later](https://img.shields.io/badge/license-GPL--3.0--or--later-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078d4)](#download-and-run)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](#build-from-source)

**Process Bus Insight** is a free, open-source, receive-only **IEC 61850 Process Bus analyzer for Windows**. It provides engineering visibility into **Sampled Values (SV)**, **GOOSE**, **PTPv2 timing context**, and **SCL expected-vs-observed validation** for authorized FAT, SAT, commissioning-support, interoperability, laboratory, and troubleshooting workflows.

The project is currently a **public beta**. Its purpose is to identify live publishers, inspect protocol evidence, isolate unhealthy streams, reproduce sanitized captures offline, compare observed traffic against SCL, and preserve defensible findings without overstating measurement confidence.

> **Timing confidence:** normal Windows/Npcap timestamps and replayed capture timestamps are software evidence. Arrival timing is useful for screening and troubleshooting, but is not certification-grade jitter evidence unless the capture path is validated with appropriate hardware timestamping, TAP, or trusted timing equipment.

![Process Bus Insight analyzer overview](docs/screenshot/analyzer-overview.webp)

## Engineering scope

Process Bus Insight is intentionally receive-only. It does not send IEC 61850 commands, operate breakers, publish SV/GOOSE, or act as a protection/control client.

| Area | Capability |
| --- | --- |
| SV analyzer | Multi-stream discovery, APPID/svID/MAC/VLAN/confRev evidence, selected-stream workspace, decoded waveform, RMS, phasor, sequence diagnostics, shape/distortion indication, and selectable scope windows. |
| Runtime snapshots | Immutable selected-stream generations containing copied identity, waveform, analog, phasor, shape, and diagnostic evidence for coherent consumer reads. |
| PCAP replay | Bounded classic Ethernet PCAP replay through the same decoder/analyzer entry point used by live Npcap capture. |
| GOOSE inspector | Publisher discovery, stNum/sqNum tracking, event timeline, typed `allData` decoding, change summaries, and SCL-assisted semantic context. |
| PTP visibility | Passive PTPv2 context, grandmaster/domain evidence where available, freshness wording, and timestamp-confidence boundaries. |
| SCL validation | Load SCD/ICD/CID files and compare expected publishers/streams against observed APPID, destination MAC, VLAN, svID, confRev, and related evidence. |
| Evidence workflow | Copyable engineering evidence, cautious timing language, target-aware diagnostics, and screenshot-friendly workspaces. |

## Evidence boundary

The application can record what was configured in SCL, what the selected capture or replay point observed, and how the software decoded or calculated that evidence. It does **not** establish:

- formal IEC 61850 conformance or certification;
- calibrated measurement or deterministic real-time timing;
- functional-safety or cybersecurity approval;
- universal interoperability;
- equipment isolation, switching authority, or site authorization; or
- proof that an IED received, accepted, trusted, or acted on observed traffic.

## Download and run

1. Install **Npcap** on the Windows capture machine when live raw Ethernet capture is required.
2. Download the latest portable ZIP from [GitHub Releases](https://github.com/masarray/DigSubAnalyzer/releases).
3. Extract it to a local folder such as `C:\Tools\ProcessBusInsight`.
4. Run `ProcessBusInsight.exe`.
5. Select a physical Ethernet adapter connected through an authorized TAP, mirror port, or isolated engineering test network.
6. Start capture and select the SV/GOOSE/PTP target to inspect.

Current post-transition package naming:

```text
ProcessBusInsight-v1.4.0-beta.2-win-x64-portable.zip
SHA256SUMS.txt
release-manifest.json
```

A separate .NET runtime is not required for the self-contained package. Npcap remains separately installed and is not redistributed by this repository.

See [`docs/QUICK_START.md`](docs/QUICK_START.md) for the field checklist.

## Architecture

```text
Process Bus / TAP / Mirror Port ──> Npcap raw Ethernet capture ─┐
                                                                ├─> ProcessBus.Iec61850.Raw
Classic Ethernet PCAP ───────────> bounded replay reader ───────┘     SV / GOOSE / PTP decode
                                                                           ↓
                                                            immutable runtime generation
                                                                           ↓
                                                           WPF / tests / future export
```

The selected SV stream is the source of truth for waveform, RMS, phasor, and stream details. Per-stream state remains isolated; consumers must not combine values from different publishers. See [`docs/architecture/STREAM_RUNTIME.md`](docs/architecture/STREAM_RUNTIME.md) and [`docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md`](docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md).

The first replay reader supports classic PCAP 2.4 with Ethernet link type 1 in little/big-endian microsecond or nanosecond variants. PCAPNG is not yet claimed.

## Build from source

Requirements: Windows 10/11 x64, .NET 8 SDK, Visual Studio with the .NET desktop workload or the .NET CLI, and Npcap for authorized live-capture testing.

```powershell
git clone https://github.com/masarray/DigSubAnalyzer.git
cd DigSubAnalyzer
dotnet restore .\ProcessBusSuite.sln
dotnet build .\ProcessBusSuite.sln -c Release
dotnet test .\ProcessBusSuite.sln -c Release
dotnet run --project .\src\ProcessBus.App.Wpf\ProcessBus.App.Wpf.csproj -c Release
```

Create and verify a portable package:

```powershell
.\scripts\publish-windows-portable.ps1 -Version "1.4.0-beta.2"
.\scripts\verify-release-package.ps1 -PackageZip ".\artifacts\release\ProcessBusInsight-v1.4.0-beta.2-win-x64-portable.zip"
```

Run the repository-quality gate:

```powershell
.\scripts\repository-health.ps1
```

Focused deterministic suites:

```powershell
dotnet test .\tests\ProcessBus.Tests\ProcessBus.Tests.csproj -c Release --filter "Category=RuntimeStability"
dotnet test .\tests\ProcessBus.Tests\ProcessBus.Tests.csproj -c Release --filter "Category=RuntimeArchitecture"
```

## Validation status

The repository includes parser and regression tests, repeated multi-stream stability evidence, immutable runtime/replay evidence, CI build/test evidence, CodeQL analysis, dependency review, release-package verification, and repository-hygiene checks. Public-beta status does not imply certification or measurement-grade timing validation.

Review:

- [`docs/validation/TESTED_CONFIGURATIONS.md`](docs/validation/TESTED_CONFIGURATIONS.md)
- [`docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md`](docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md)
- [`docs/VALIDATION_MATRIX.md`](docs/VALIDATION_MATRIX.md)
- [`docs/VALIDATION_TEST_PLAN.md`](docs/VALIDATION_TEST_PLAN.md)
- [`docs/development/RELEASE_CHECKLIST.md`](docs/development/RELEASE_CHECKLIST.md)

## Documentation

- [`docs/QUICK_START.md`](docs/QUICK_START.md) — first-run and field checklist
- [`docs/USER_MANUAL.md`](docs/USER_MANUAL.md) — user workflow
- [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md) — capture and interpretation issues
- [`docs/LICENSING.md`](docs/LICENSING.md) — current GPL, historical Apache boundary, and commercial path
- [`docs/architecture/STREAM_RUNTIME.md`](docs/architecture/STREAM_RUNTIME.md) — selected-stream invariants
- [`docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md`](docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md) — immutable generation and PCAP replay
- [`docs/RELEASE_PACKAGING.md`](docs/RELEASE_PACKAGING.md) — portable packaging design
- [`SECURITY.md`](SECURITY.md) — vulnerability reporting
- [`SUPPORT.md`](SUPPORT.md) — community and commercial support boundaries

## Contributing

Contributions are welcome when they preserve the receive-only boundary, selected-stream isolation, evidence-focused wording, legal provenance, and honest timing confidence. Read [`CONTRIBUTING.md`](CONTRIBUTING.md), [`CONTRIBUTOR-LICENSE-AGREEMENT.md`](CONTRIBUTOR-LICENSE-AGREEMENT.md), [`DCO.txt`](DCO.txt), and [`AGENTS.md`](AGENTS.md).

## Licensing

Revisions after commit `85d43a0fe58a5888a9e8008c168ab76d2333ea87` on `main`, and release packages built from those revisions, are licensed **only** under [`GPL-3.0-or-later`](LICENSE).

The boundary commit and earlier revisions—including historical `v1.4.0-beta.1` artifacts built from that line—retain their Apache-2.0 grants and are preserved on `archive/apache-2.0-final`. Historical rights remain effective; this is not a current Apache-or-GPL dual-license offer.

A separate negotiated commercial path is available for proprietary integration, OEM/white-label distribution, closed-source redistribution, private branches, and contractual support or engineering services. [`COMMERCIAL-LICENSE.md`](COMMERCIAL-LICENSE.md) is an invitation to discuss terms and grants no additional rights by itself.

Third-party software retains its own terms. See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md), [`COPYRIGHT.md`](COPYRIGHT.md), and [`TRADEMARK.md`](TRADEMARK.md).