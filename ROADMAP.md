# Roadmap — Process Bus Insight

The product is moving from a capable public beta toward a repeatable, evidence-backed field instrument.

## Completed foundation: v1.3.x beta

- Deterministic selected-stream ownership without click-triggered refresh
- Coherent waveform, RMS, and phasor snapshots
- Verified 2/4/8-cycle timebases
- Fast harmonic/shape change detection when raw payload changes
- Multi-stream, rollover, malformed-frame, and publisher-restart regression coverage
- Repeated deterministic Runtime Stability workflow with downloadable TRX evidence
- Maintained 60-minute Windows/Npcap field evidence
- Repository, CI, security, candidate-package, and release hardening

## Current: v1.4 runtime architecture

### v1.4.0-beta.1

- Immutable selected-stream runtime generations with copied samples and diagnostics
- Atomic snapshot publication boundary for coherent consumer reads
- Classic Ethernet PCAP replay through the same raw analyzer path used by live capture
- Bounded PCAP parsing with endian and micro/nanosecond timestamp variants
- Replay, immutability, capture timing, truncation, and multi-stream isolation regression coverage
- Dedicated Runtime Architecture workflow with downloadable TRX evidence

### Later v1.4 builds

- Extract the complete `SvStreamRuntime` from the monolithic analyzer
- Extract sequence tracking, waveform windowing, phasor calculation, and shape analysis into independently tested components
- Migrate the live WPF workspace to immutable runtime generations
- Add PCAPNG and a maintained sanitized golden replay corpus
- Add memory/CPU soak tests and observable performance budgets
- Add replay controls and evidence selection to the user interface after the engine boundary is stable

## Next: protocol and SCL maturity

- Expand multi-vendor SCL namespace and DataSet mapping tests
- Separate publisher identity, observed traffic, and subscriber expectations throughout the UI
- Improve GOOSE semantic labels and quality interpretation
- Extend PTP freshness and grandmaster-change evidence

## Next: evidence workflow

- Export sanitized CSV/JSON/PDF engineering findings
- Add tested-configuration and known-limit matrices to releases
- Add reproducible screenshots and short demonstrations generated from sanitized sources

## Non-goals

- Relay test-set replacement
- IEC 61850 control client
- SV/GOOSE publisher for protection operation
- PTP grandmaster
- Certified timing instrument without validated timestamp hardware
