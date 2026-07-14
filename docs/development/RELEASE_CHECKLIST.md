# Release Checklist

## Source, licensing, and repository

- [ ] `main` is clean and protected by pull-request review where practical
- [ ] Repository health script passes
- [ ] Root `LICENSE` is GNU GPL version 3 and project metadata declares `GPL-3.0-or-later`
- [ ] Current source and package wording do not present Apache-2.0 as an active alternative license
- [ ] Historical boundary commit `85d43a0fe58a5888a9e8008c168ab76d2333ea87` and `archive/apache-2.0-final` are documented
- [ ] Commercial notice states that it is not itself a license and grants no additional rights
- [ ] Copyright, trademark, CLA, DCO, third-party, provenance, and public-claim documents are current
- [ ] No nested repo, RnD solution, build output, logs, captures, or local settings are tracked
- [ ] Version matches `Directory.Build.props`, README, landing page, release workflow, release notes, and package script
- [ ] Changelog, architecture notes, and tested-configuration notes are updated

## Automated validation

- [ ] Restore succeeds from a clean checkout
- [ ] Release build succeeds
- [ ] All tests pass
- [ ] Runtime Stability workflow passes all repeated `Category=RuntimeStability` iterations
- [ ] Runtime Architecture workflow passes all repeated `Category=RuntimeArchitecture` iterations
- [ ] Classic PCAP little/big-endian and micro/nanosecond variants pass
- [ ] Truncated, unsupported-link-type, invalid-timestamp, and oversized-record cases are rejected
- [ ] CodeQL completes
- [ ] Dependency review has no unresolved high-severity finding
- [ ] Portable candidate package verification succeeds
- [ ] ZIP checksum and candidate/release manifest are generated

## Runtime smoke test

- [ ] Application starts without Npcap crash-handling regressions
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

## Replay and immutable snapshot checks

- [ ] Sanitized classic Ethernet PCAP replays through `RawProcessBusAnalyzer.ObserveOwnedFrame`
- [ ] Recorded frame order and capture duration are preserved
- [ ] Selected-stream runtime generation contains copied identity, samples, analog, phasor, shape, and diagnostics
- [ ] Advancing the analyzer does not mutate a previously published generation
- [ ] Interleaved replay streams remain isolated when selected and published
- [ ] Replay is file-input only and does not open a transmit path
- [ ] PCAPNG is listed as unsupported until implemented and validated

## Claims, data, and provenance

- [ ] Timing wording remains screening-level unless hardware evidence exists
- [ ] Replay is not described as recreating NIC, switch, or hardware timestamp behavior
- [ ] Expected configuration, observed traffic, software interpretation, and external-device behavior remain distinct
- [ ] No formal conformance, calibration, deterministic timing, functional-safety, cybersecurity, universal interoperability, switching-authority, or IED-acceptance claim is implied
- [ ] Screenshots match the released UI
- [ ] No customer, employer, station, credential, project-sensitive, or unlawfully shared evidence is included
- [ ] Every non-synthetic fixture has documented authorization, sanitization, and provenance
- [ ] Known limitations are listed in release notes

## Package legal content

- [ ] ZIP contains `LICENSE.txt`, `NOTICE.txt`, `COMMERCIAL-LICENSE.md`, `COPYRIGHT.md`, `TRADEMARK.md`, `THIRD_PARTY_NOTICES.md`, and `Licensing.md`
- [ ] `LICENSE.txt` contains GNU GPL version 3
- [ ] No historical Apache license file is presented as the current package license
- [ ] Commercial notice non-grant wording is intact
- [ ] Release manifest identifies `GPL-3.0-or-later` and the historical boundary

## Release

- [ ] Create signed/verified tag where available
- [ ] Publish portable ZIP, `SHA256SUMS.txt`, and `release-manifest.json`
- [ ] Mark beta releases as prerelease
- [ ] Confirm GitHub Pages and download links
- [ ] Do not replace a historical Apache release asset with a post-transition GPL binary under the same tag; use the new version `v1.4.0-beta.2` or later