# Process Bus Insight v1.4.0-beta.1

This architecture beta introduces a reproducible offline analysis path and a coherent immutable runtime publication boundary. The receive-only safety model is unchanged.

## Highlights

- Immutable selected-stream runtime generations with copied waveform samples, analog values, identity, and diagnostics
- Atomic snapshot publication so consumers see either the previous complete generation or the next complete generation
- Classic PCAP Ethernet replay through the same `RawProcessBusAnalyzer.ObserveOwnedFrame` entry point used by live capture
- Little-endian and big-endian PCAP support with microsecond and nanosecond timestamp variants
- Explicit limits and rejection for unsupported link types, oversized records, invalid timestamp fractions, and truncated captures
- Deterministic replay regression coverage for coherent two-cycle windows, three-stream isolation, timing preservation, and snapshot immutability
- Dedicated Runtime Architecture workflow with downloadable TRX evidence
- Verified Windows x64 candidate packaging for `architecture/*` branches

## Engineering intent

The replay path is not a second decoder. It feeds captured Ethernet frames into the same raw decoder and analyzer used by Npcap live capture. This makes field defects reproducible without requiring the original merging unit or network to remain connected.

The immutable runtime snapshot is the first step toward separating per-stream state, calculations, and UI consumption. The internal monolithic analyzer is not yet fully decomposed in this beta; that work continues in later v1.4 builds.

## Supported replay scope

- Classic PCAP version 2.4
- Ethernet link type 1
- Microsecond and nanosecond timestamp magic variants
- SV, GOOSE, and PTP frames already supported by the raw decoder

PCAPNG, non-Ethernet link types, capture editing, and traffic transmission are not claimed in this release.

## Important limitations

- This remains a public beta, not a certified protection or timing instrument.
- Windows/Npcap and replay timestamps are engineering screening evidence unless the capture path is independently validated.
- Replay preserves recorded ordering and timing context but does not recreate network loading, NIC buffering, or hardware timestamp behavior.
- The runtime snapshot boundary is available to replay and engineering consumers; full live UI migration to the new runtime architecture remains staged work.
- Npcap must be installed separately for live capture.
