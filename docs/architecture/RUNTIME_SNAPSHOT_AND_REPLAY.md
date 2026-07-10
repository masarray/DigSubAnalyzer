# Runtime Snapshot and Replay Architecture

## Purpose

The v1.4 architecture introduces two linked boundaries:

1. a coherent immutable snapshot for consumers such as UI, replay, export, and tests;
2. an offline capture replay path that feeds the same raw analyzer entry point used by live Npcap capture.

This avoids building a second protocol implementation for offline analysis and prevents consumers from retaining mutable analyzer display objects across refreshes.

## Data flow

```text
Live Npcap frame ───────────────┐
                                ├─> RawProcessBusAnalyzer.ObserveOwnedFrame
Classic Ethernet PCAP frame ───┘                  │
                                                   ▼
                                      AnalyzerSnapshot under analyzer lock
                                                   │
                                                   ▼ copy all mutable collections
                                      SvRuntimeSnapshotPublisher
                                                   │
                                                   ▼ atomic reference publication
                                     Immutable SvRuntimeSnapshot generation
```

## Snapshot guarantees

Each `SvRuntimeSnapshot` generation contains copied values for:

- selected stream identity;
- voltage and current channel samples;
- instant, RMS, and phasor angle values;
- waveform shape evidence;
- sample rate, samples per cycle, measured frequency, and window duration;
- packet, decode, sequence, missing-sample, timing, and jitter diagnostics.

A consumer sees either the previous complete generation or the next complete generation. Publication does not expose a partially assembled mix. Sample arrays are copied into read-only collections before publication, so advancing the analyzer cannot change an already published generation.

The snapshot does not claim that the internal analyzer has been fully decomposed. It is the compatibility boundary used while per-stream acquisition, sequence tracking, windowing, and calculations are progressively extracted.

## Replay guarantees

`ProcessBusReplaySession` reads bounded classic PCAP records and calls the same `RawProcessBusAnalyzer.ObserveOwnedFrame` method used by live capture. Recorded capture timestamps are converted into deterministic monotonic tick deltas for the analyzer timing path.

Supported in v1.4.0-beta.1:

- classic PCAP version 2.4;
- Ethernet link type 1;
- little-endian and big-endian files;
- microsecond and nanosecond timestamp variants;
- SV, GOOSE, and PTP frames understood by the existing raw decoder.

Rejected explicitly:

- unsupported magic/version/link type;
- zero or oversized captured frames;
- captured length above snap length;
- original length smaller than captured length;
- invalid timestamp fractions;
- truncated headers or frame payloads.

## Safety boundary

Replay is file input only. It does not open a transmit socket, publish SV/GOOSE, send IEC 61850 controls, or emulate a merging unit. Process Bus Insight remains receive-only.

## Known limitations

- PCAPNG is not supported by the first reader.
- Replay does not recreate switch loading, NIC buffering, hardware timestamping, or packet loss that was not present in the recorded capture.
- The WPF live workspace still consumes the existing analyzer display snapshot. Migration to the immutable runtime boundary is staged to avoid a risky all-at-once rewrite.
- Full `SvStreamRuntime` extraction remains planned for later v1.4 builds.

## Regression evidence

Run the focused suite with:

```powershell
dotnet test .\tests\ProcessBus.Tests\ProcessBus.Tests.csproj -c Release --filter "Category=RuntimeArchitecture"
```

The `Runtime architecture` GitHub Actions workflow repeats the suite and retains TRX evidence. Tests cover decoder-path replay, coherent window publication, capture duration preservation, immutable generations, three-stream isolation, unsupported link types, and truncated records.
