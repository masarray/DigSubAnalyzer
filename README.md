# Process Bus Insight (DigSubAnalyzer)

[![CI](https://github.com/masarray/DigSubAnalyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/ci.yml)
[![Runtime Stability](https://github.com/masarray/DigSubAnalyzer/actions/workflows/runtime-stability.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/runtime-stability.yml)
[![Runtime Architecture](https://github.com/masarray/DigSubAnalyzer/actions/workflows/runtime-architecture.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/runtime-architecture.yml)
[![CodeQL](https://github.com/masarray/DigSubAnalyzer/actions/workflows/codeql.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/codeql.yml)
[![GitHub Pages](https://github.com/masarray/DigSubAnalyzer/actions/workflows/pages.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/pages.yml)
[![Release Package](https://github.com/masarray/DigSubAnalyzer/actions/workflows/release-package.yml/badge.svg)](https://github.com/masarray/DigSubAnalyzer/actions/workflows/release-package.yml)
[![Latest Release](https://img.shields.io/github/v/release/masarray/DigSubAnalyzer?include_prereleases&label=release)](https://github.com/masarray/DigSubAnalyzer/releases)
[![License](https://img.shields.io/github/license/masarray/DigSubAnalyzer)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078d4)](#download-and-run)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](#build-from-source)

**Process Bus Insight** is a free, open-source, receive-only **IEC 61850 Process Bus analyzer for Windows**. It provides engineering visibility into **Sampled Values (SV)**, **GOOSE**, **PTPv2 timing context**, and **SCL expected-vs-observed validation** for FAT, SAT, commissioning, interoperability checks, and troubleshooting.

The project is currently released as a **public beta**. Its design goal is field clarity without overstating measurement confidence: identify live publishers, inspect protocol evidence, isolate unhealthy streams, reproduce captured traffic offline, compare observed traffic against SCL, and capture defensible findings.

> **Timing confidence:** normal Windows/Npcap timestamps and replayed capture timestamps are software evidence. Arrival timing is useful for screening and troubleshooting, but is not certification-grade jitter evidence unless the capture path is validated with appropriate hardware timestamping, TAP, or trusted timing equipment.

![Process Bus Insight analyzer overview](docs/screenshot/analyzer-overview.webp)

## Engineering scope

Process Bus Insight is intentionally receive-only. It does not send IEC 61850 commands, operate breakers, publish SV/GOOSE, or act as a protection/control client.

Current capabilities include:

| Area | Capability |
| --- | --- |
| SV analyzer | Multi-stream discovery, APPID/svID/MAC/VLAN/confRev evidence, selected-stream workspace, raw decoded sample waveform, RMS, phasor, sequence diagnostics, shape/distortion indication, and selectable scope windows. |
| Runtime snapshots | Immutable selected-stream generations containing copied identity, waveform, analog, phasor, shape, and diagnostic evidence for coherent consumer reads. |
| PCAP replay | Bounded classic Ethernet PCAP replay through the same raw decoder/analyzer entry point used by live Npcap capture. |
| GOOSE inspector | Publisher discovery, stNum/sqNum tracking, event timeline, typed `allData` decoding, change summaries, and SCL-assisted semantic context. |
| PTP visibility | Passive PTPv2 message context, grandmaster/domain evidence where available, freshness wording, and timestamp-confidence boundaries. |
| SCL validation | Load SCD/ICD/CID files and compare expected publishers/streams against observed APPID, destination MAC, VLAN, svID, confRev, and related evidence. |
| Evidence workflow | Copyable engineering evidence, cautious timing language, target-aware diagnostics, and screenshot-friendly workspaces. |

## Why this exists

Wireshark remains an essential packet-analysis tool. Process Bus Insight focuses on the questions commissioning engineers repeatedly need answered quickly:

- Which SV streams and GOOSE publishers are live now?
- Is the observed APPID, MAC, VLAN, svID, or confRev consistent with the SCL design?
- Is the problem owned by the stream, publisher, timing source, adapter, or capture path?
- Are waveform, RMS, and phasor values coming from the same selected stream?
- Can a sanitized capture reproduce the same decoder and runtime behavior away from site?
- What evidence can be copied into a FAT/SAT finding without overclaiming timing accuracy?

## Download and run

1. Install **Npcap** on the Windows capture machine.
2. Download the latest portable ZIP from [GitHub Releases](https://github.com/masarray/DigSubAnalyzer/releases).
3. Extract it to a local folder such as `C:\Tools\ProcessBusInsight`.
4. Run `ProcessBusInsight.exe`.
5. Select a physical Ethernet adapter connected to a TAP, mirror port, or isolated test network.
6. Start capture and select the SV/GOOSE/PTP target to inspect.

Current public-beta package naming:

```text
ProcessBusInsight-v1.4.0-beta.1-win-x64-portable.zip
SHA256SUMS.txt
release-manifest.json
```

A separate .NET runtime is not required for the self-contained portable package. Npcap remains a separate runtime prerequisite and is not redistributed by this repository.

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

The selected SV stream is the source of truth for waveform, RMS, phasor, and stream details. Per-stream state must remain isolated; consumers must never combine values from different publishers. The v1.4 runtime snapshot boundary copies mutable analyzer collections before atomic publication. See [`docs/architecture/STREAM_RUNTIME.md`](docs/architecture/STREAM_RUNTIME.md) and [`docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md`](docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md).

The first replay reader supports classic PCAP 2.4 with Ethernet link type 1 in little/big-endian microsecond or nanosecond variants. PCAPNG is not yet claimed.

## Build from source

Requirements:

- Windows 10/11 x64
- .NET 8 SDK
- Visual Studio with the .NET desktop workload, or the .NET CLI
- Npcap for live capture tests

```powershell
git clone https://github.com/masarray/DigSubAnalyzer.git
cd DigSubAnalyzer
dotnet restore .\ProcessBusSuite.sln
dotnet build .\ProcessBusSuite.sln -c Release
dotnet test .\ProcessBusSuite.sln -c Release
```

Run the application:

```powershell
dotnet run --project .\src\ProcessBus.App.Wpf\ProcessBus.App.Wpf.csproj -c Release
```

Create and verify a portable package:

```powershell
.\scripts\publish-windows-portable.ps1 -Version "1.4.0-beta.1"
.\scripts\verify-release-package.ps1 -PackageZip ".\artifacts\release\ProcessBusInsight-v1.4.0-beta.1-win-x64-portable.zip"
```

Run the repository-quality gate:

```powershell
.\scripts\repository-health.ps1
```

Run only the deterministic runtime-stability suite:

```powershell
dotnet test .\tests\ProcessBus.Tests\ProcessBus.Tests.csproj -c Release --filter "Category=RuntimeStability"
```

Run only the immutable snapshot and PCAP replay suite:

```powershell
dotnet test .\tests\ProcessBus.Tests\ProcessBus.Tests.csproj -c Release --filter "Category=RuntimeArchitecture"
```

## Validation status

The repository includes parser and regression tests, repeated deterministic multi-stream stability evidence, immutable runtime/replay evidence, CI build/test evidence, CodeQL analysis, dependency review, release-package verification, and explicit repository-hygiene checks. Public-beta status does **not** imply vendor certification or measurement-grade timing validation.

Before interpreting results, review:

- [`docs/validation/TESTED_CONFIGURATIONS.md`](docs/validation/TESTED_CONFIGURATIONS.md)
- [`docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md`](docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md)
- [`docs/VALIDATION_MATRIX.md`](docs/VALIDATION_MATRIX.md)
- [`docs/VALIDATION_TEST_PLAN.md`](docs/VALIDATION_TEST_PLAN.md)
- [`docs/development/RELEASE_CHECKLIST.md`](docs/development/RELEASE_CHECKLIST.md)

## Documentation

- [`docs/QUICK_START.md`](docs/QUICK_START.md) — first-run and field checklist
- [`docs/USER_MANUAL.md`](docs/USER_MANUAL.md) — user workflow
- [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md) — capture and interpretation issues
- [`docs/architecture/STREAM_RUNTIME.md`](docs/architecture/STREAM_RUNTIME.md) — selected-stream and snapshot invariants
- [`docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md`](docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md) — immutable generation and PCAP replay design
- [`docs/validation/TESTED_CONFIGURATIONS.md`](docs/validation/TESTED_CONFIGURATIONS.md) — explicitly tested environments
- [`docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md`](docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md) — maintained 60-minute live stability evidence
- [`docs/RELEASE_PACKAGING.md`](docs/RELEASE_PACKAGING.md) — portable packaging design
- [`ROADMAP.md`](ROADMAP.md) — product direction
- [`SECURITY.md`](SECURITY.md) — vulnerability reporting and data-handling policy
- [`SUPPORT.md`](SUPPORT.md) — support boundaries and issue evidence

## Contributing

Contributions are welcome when they preserve the receive-only boundary, selected-stream isolation, evidence-focused wording, and honest timing confidence. Read [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`AGENTS.md`](AGENTS.md) before changing runtime or UI behavior.

## License and third-party software

Source code is licensed under **Apache-2.0**. See [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).

Npcap is a runtime prerequisite and is not vendored. Self-contained .NET/WPF runtime files may be present in generated release artifacts. See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
