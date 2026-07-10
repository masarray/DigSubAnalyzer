# Roadmap — Process Bus Insight

The product is moving from a capable public beta toward a repeatable, evidence-backed field instrument.

## Release gate: v1.3.x beta

- Deterministic selected-stream ownership without click-triggered refresh
- Coherent waveform, RMS, and phasor snapshots
- Verified 2/4/8-cycle timebases
- Fast harmonic/shape change detection when raw payload changes
- Multi-stream, rollover, malformed-frame, and publisher-restart regression coverage
- Repository, CI, security, and release hardening

## Next: runtime architecture

- Extract `SvStreamRuntime`, sequence tracking, waveform windowing, phasor calculation, and shape analysis from the monolithic analyzer
- Publish immutable per-stream snapshots
- Add PCAP replay as a first-class test and demonstration path
- Add memory/CPU soak tests and observable performance budgets

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
