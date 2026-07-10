# Process Bus Insight v1.3.0-beta.1

This public beta consolidates SV runtime stability work and raises the repository, CI, security, and release process to a more professional open-source baseline.

## Highlights

- Raw decoded SV sample waveform workspace
- Per-selected-stream waveform, RMS, phasor, and state ownership
- Coherent fixed-window scope behavior with 2/4/8-cycle timebases
- Faster waveform shape/distortion response while keeping visual scope stability
- Live-stream ordering and cleaner status/noise presentation
- Hardened BER parsing and Npcap lifecycle behavior
- SCL expected-vs-observed binding improvements

## Repository and supply-chain improvements

- Build/test evidence uploaded from CI
- CodeQL analysis and pull-request dependency review
- Dependabot for NuGet and GitHub Actions
- Repository hygiene and version-consistency gate
- Structured issue forms, pull-request checklist, CODEOWNERS, support, security, and conduct policies
- Release manifest and checksum alongside the portable package

## Important limitations

- This is a public beta, not a certified protection or timing instrument.
- Windows/Npcap arrival timestamps are screening evidence unless the capture path is independently validated.
- Multi-vendor SCL scaling and semantic mapping require broader field validation.
- Npcap must be installed separately.

## Recommended validation

Exercise multiple simultaneous SV streams, amplitude/angle/harmonic changes, publisher restart, sample-counter rollover, and all supported timebases before relying on the build for FAT/SAT work.
