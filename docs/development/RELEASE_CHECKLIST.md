# Release Checklist

## Source and repository

- [ ] `main` is clean and protected by pull-request review where practical
- [ ] Repository health script passes
- [ ] No nested repo, RnD solution, build output, logs, captures, or local settings are tracked
- [ ] Version matches `Directory.Build.props`, README, landing page, release workflow, and release notes
- [ ] Changelog, architecture notes, and tested-configuration notes are updated

## Automated validation

- [ ] Restore succeeds from a clean checkout
- [ ] Release build succeeds
- [ ] All tests pass
- [ ] Runtime Stability workflow passes all repeated `Category=RuntimeStability` iterations
- [ ] Runtime Stability TRX artifact is retained with the release evidence
- [ ] Runtime Architecture workflow passes all repeated `Category=RuntimeArchitecture` iterations
- [ ] Runtime Architecture TRX artifact is retained with the release evidence
- [ ] Classic PCAP little/big-endian and micro/nanosecond variants pass
- [ ] Truncated, unsupported-link-type, invalid-timestamp, and oversized-record cases are rejected
- [ ] CodeQL completes
- [ ] Dependency review has no unresolved high-severity finding
- [ ] Portable candidate package verification succeeds
- [ ] ZIP checksum and candidate/release manifest are generated

## Runtime smoke test

- [ ] Application starts without Npcap crash handling regressions
- [ ] Physical/known adapter can start and stop cleanly
- [ ] Multiple SV streams remain isolated
- [ ] Initial selected stream updates without user click
- [ ] Waveform, RMS, and phasor update coherently
- [ ] Harmonic/shape changes are detected when payload samples change
- [ ] 2/4/8-cycle timebases show correct fixed-length windows
- [ ] 4000 and 65536 sample-counter rollover behavior is exercised
- [ ] Duplicate, forward-gap, and publisher-restart behavior is exercised
- [ ] GOOSE, PTP, and SCL workspaces open and update
- [ ] 30–60 minute soak test shows no material memory growth or UI freeze
- [ ] Maintained live evidence remains available in `docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md`

## Replay and immutable snapshot checks

- [ ] Sanitized classic Ethernet PCAP replays through `RawProcessBusAnalyzer.ObserveOwnedFrame`
- [ ] Recorded frame order and capture duration are preserved
- [ ] Selected-stream runtime generation contains copied identity, samples, analog, phasor, shape, and diagnostics
- [ ] Advancing the analyzer does not mutate a previously published generation
- [ ] Interleaved replay streams remain isolated when selected and published
- [ ] Replay is file-input only and does not open a transmit path
- [ ] PCAPNG is listed as unsupported until implemented and validated

## Claims and evidence

- [ ] Timing wording remains screening-level unless hardware evidence exists
- [ ] Replay is not described as recreating NIC, switch, or hardware timestamp behavior
- [ ] Automated deterministic stress is not described as a replacement for live/replay soak validation
- [ ] Full internal analyzer decomposition is not claimed before it is complete
- [ ] Screenshots match the released UI
- [ ] No customer or project-sensitive evidence is included
- [ ] Known limitations are listed in release notes

## Release

- [ ] Create signed/verified tag where available
- [ ] Publish portable ZIP, `SHA256SUMS.txt`, and `release-manifest.json`
- [ ] Mark beta releases as prerelease
- [ ] Confirm GitHub Pages and download links
