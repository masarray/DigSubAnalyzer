# Release Checklist

## Source and repository

- [ ] `main` is clean and protected by pull-request review where practical
- [ ] Repository health script passes
- [ ] No nested repo, RnD solution, build output, logs, captures, or local settings are tracked
- [ ] Version matches `Directory.Build.props`, README, landing page, release workflow, and release notes
- [ ] Changelog and tested-configuration notes are updated

## Automated validation

- [ ] Restore succeeds from a clean checkout
- [ ] Release build succeeds
- [ ] All tests pass
- [ ] Runtime Stability workflow passes all repeated `Category=RuntimeStability` iterations
- [ ] Runtime Stability TRX artifact is retained with the release evidence
- [ ] CodeQL completes
- [ ] Dependency review has no unresolved high-severity finding
- [ ] Portable package verification succeeds
- [ ] ZIP checksum and release manifest are generated

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
- [ ] `docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md` is completed with sanitized evidence

## Claims and evidence

- [ ] Timing wording remains screening-level unless hardware evidence exists
- [ ] Automated deterministic stress is not described as a replacement for live/replay soak validation
- [ ] Screenshots match the released UI
- [ ] No customer or project-sensitive evidence is included
- [ ] Known limitations are listed in release notes

## Release

- [ ] Create signed/verified tag where available
- [ ] Publish portable ZIP, `SHA256SUMS.txt`, and `release-manifest.json`
- [ ] Mark beta releases as prerelease
- [ ] Confirm GitHub Pages and download links
