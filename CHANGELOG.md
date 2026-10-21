# Changelog

All notable changes are documented here. Public stable versions retain immutable tags and published binaries.

## [Unreleased]

### Planned
- Sanitized golden PCAP/PCAPNG replay corpus with broader interoperability scenarios
- Further decomposition of the analyzer runtime and WPF workspace
- Expanded SCL validation
- Evidence export for FAT/SAT reports
- Independent canonical SV/PTP physical acceptance and soak evidence (tracked in issue #30)

## [1.4.2] - 2026-10-21

### Stable GOOSE engineering workflow
- Stable physically keyed multi-source GOOSE publisher selection and searchable Sources
- Compact one-line Messages header, Show repeats and Follow live switches, bounded event history
- Actual decoded DataSet changes correlated across Sources, Messages, and Inspector
- Shared operator acknowledgement state with unobtrusive red unread dot
- Change value emphasis only while unread; before/after values available in tooltip
- Robust stale-source handling, retransmission filtering, explicit decoder gap/overflow notices
- Windows x64 self-contained stable Inno Setup installer, portable ZIP, SHA256 and manifest

### Verification
- Official stable v1.4.2 Windows Release build on main; 241/241 unit tests, layout contract, package verification and Inno install/repair/uninstall smoke PASS
- Engineering PR checks also passed runtime stability, architecture, CodeQL and dependency security
- Independent field TAP/IED acceptance is not claimed by a software release

## [1.4.1] - 2026-10-21

### Release highlights
- Multi-stream SV metering, waveform, phasor and compact engineering workspaces
- Stable Npcap adapter handling, diagnostics grouping, and readable SCL/PTP presentation
- Self-contained Inno installer with in-place repair/upgrade and offline manuals
- Deterministic phasor, adapter discovery and status regression coverage

## [1.4.0-beta.2] - 2026-07-15

### Added

- GPL-3.0-or-later current community licensing model
- Separate negotiated commercial licensing path
- Historical Apache-2.0 preservation branch at `archive/apache-2.0-final`
- Copyright, trademark, CLA, DCO, transition, provenance, and public-claim records
- GPL and package legal-content verification in the repository-health gate
- Candidate-package validation for `legal/*` branches

### Changed

- Version advanced to `1.4.0-beta.2` so post-transition packages are not confused with historical Apache-licensed v1.4.0-beta.1 artifacts
- README, landing-page structured data, FAQ, manifest, contribution guidance, package scripts, release workflow, and release documentation now reflect GPL and commercial boundaries
- Current portable packages include GPL, commercial, copyright, trademark, third-party, and licensing-summary documents
- Release manifests record the community license and historical boundary

### Security and provenance

- External implementation material is limited to lawful black-box interoperability use unless documented incorporation rights exist
- Real captures, SCL, screenshots, and diagnostics require documented authorization and sanitization
- Public wording separates configured expectations, observed traffic, software interpretation, and external-device behavior

## [1.4.0-beta.1] - 2026-07-11

### Added

- Immutable selected-stream runtime generations with copied waveform, analog, identity, and diagnostic evidence
- Atomic runtime snapshot publisher for coherent consumer reads
- Classic PCAP replay through the same raw decoder/analyzer path used by live Npcap capture
- Microsecond and nanosecond PCAP timestamp variants in little-endian and big-endian formats
- Bounded rejection for unsupported link types, invalid headers, oversized records, and truncated captures
- Deterministic runtime-architecture tests covering replay timing, snapshot immutability, and three-stream isolation
- Dedicated Runtime Architecture GitHub Actions gate with downloadable TRX evidence
- Release-candidate packaging support for `architecture/*` branches

### Changed

- Release and documentation versioning use `1.4.0-beta.1`
- Runtime architecture exposes a coherent publication boundary instead of requiring consumers to retain mutable analyzer display models
- Offline replay is treated as a reproducibility path, not a separate decoder or traffic publisher

### Limitations

- PCAPNG is not yet supported by the first replay reader
- The internal analyzer remains partly monolithic; complete per-stream runtime extraction and live UI migration continue in later v1.4 builds

## [1.3.0-beta.2] - 2026-07-10

### Added

- Deterministic eight-stream selected-stream isolation regression coverage
- Exact 2/4/8-cycle scope-window regression coverage
- Explicit 65536 sample-counter rollover coverage
- Duplicate and forward-gap evidence checks that preserve the last coherent scope window
- Waveform/RMS same-window coherence checks
- Concurrent frame-observation and snapshot-read stress coverage
- Repeated Runtime Stability GitHub Actions gate with downloadable TRX evidence
- Standardized sanitized field-evidence record for live capture and replay validation

### Changed

- Release and documentation versioning use `1.3.0-beta.2`
- Runtime validation separates deterministic automated stress evidence from maintained live/replay soak evidence

## [1.3.0-beta.1] - 2026-07-10

### Added

- Raw decoded SV sample waveform workspace with selected-stream ownership
- Coherent waveform windows and selectable 2/4/8-cycle timebases
- RMS, phasor, and waveform-shape analysis from live SV samples
- BER length hardening and malformed-input tests
- Scope stability regression tests and golden SV frames
- Repository health/version gate, CodeQL, dependency review, Dependabot, issue templates, CODEOWNERS, and release manifest

### Changed

- SCL candidates with a valid primary identity anchor remain eligible when transport or configuration fields mismatch
- SV explorer prioritizes live streams and sorts by SV name
- Metering and waveform layouts reduce repeated status text

### Security

- Strengthened parsing boundaries and public guidance for sanitizing captures and SCL evidence

## [1.2.7]

- Hardened BER parsing, Npcap lifecycle, release version propagation, and public-repository packaging.

[Unreleased]: https://github.com/masarray/DigSubAnalyzer/compare/v1.4.2...HEAD
[1.4.2]: https://github.com/masarray/DigSubAnalyzer/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/masarray/DigSubAnalyzer/releases/tag/v1.4.1
[1.4.0-beta.2]: https://github.com/masarray/DigSubAnalyzer/compare/v1.4.0-beta.1...v1.4.0-beta.2
[1.4.0-beta.1]: https://github.com/masarray/DigSubAnalyzer/compare/v1.3.0-beta.2...v1.4.0-beta.1
[1.3.0-beta.2]: https://github.com/masarray/DigSubAnalyzer/compare/v1.3.0-beta.1...v1.3.0-beta.2
[1.3.0-beta.1]: https://github.com/masarray/DigSubAnalyzer/releases/tag/v1.3.0-beta.1
