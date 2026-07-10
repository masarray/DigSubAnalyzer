# Changelog

All notable changes are documented here. The project follows Semantic Versioning where practical while it remains in public beta.

## [Unreleased]

### Planned

- Golden PCAP replay corpus with sanitized multi-stream scenarios
- Further decomposition of the analyzer runtime and WPF workspace
- Expanded SCL multi-vendor validation
- Evidence export for FAT/SAT reports

## [1.3.0-beta.1] - 2026-07-10

### Added

- Raw decoded SV sample waveform workspace with selected-stream ownership
- Coherent waveform windows and selectable 2/4/8-cycle timebases
- RMS, phasor, and waveform-shape analysis from live SV samples
- BER length hardening and malformed-input tests
- Scope stability regression tests and golden SV frames
- Repository health/version gate, CodeQL, dependency review, Dependabot, issue templates, CODEOWNERS, and release manifest

### Changed

- SV explorer prioritizes live streams and sorts by SV name
- Live streams use `LIVE` state with health color rather than ambiguous warning-only labels
- Metering and waveform layouts reduce repeated status/noise text
- Release and documentation versioning use `1.3.0-beta.1`

### Security

- Strengthened parsing boundaries and public guidance for sanitizing captures and SCL evidence

## [1.2.7]

- Hardened BER parsing, Npcap lifecycle, release version propagation, and public-repository packaging.

[Unreleased]: https://github.com/masarray/DigSubAnalyzer/compare/v1.3.0-beta.1...HEAD
[1.3.0-beta.1]: https://github.com/masarray/DigSubAnalyzer/releases/tag/v1.3.0-beta.1
