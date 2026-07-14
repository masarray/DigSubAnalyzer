# Repository engineering invariants

This file defines permanent rules for human and AI contributors. Historical phase notes belong in `docs/archive/`, not here.

## Product boundary

- Process Bus Insight is receive-only and raw-passive.
- Do not add IEC 61850 control, command, operate, publisher, or protection-action workflows.
- Do not claim certified timing accuracy from ordinary Windows/Npcap timestamps.
- Never commit customer/site captures, unsanitized SCL files, MAC/IP addresses, credentials, logs, or project-sensitive screenshots.

## Selected-stream ownership

- Every strong SV stream key owns its own runtime state.
- The UI-selected SV stream is the source of truth for waveform, RMS, phasor, mapping, diagnostics, and details.
- Never combine waveform from one stream with phasor/RMS from another.
- A refresh may hold the last coherent snapshot, but it must not publish partial cross-stream state.
- Clicking the explorer must not be required to make live data update.

## Waveform and phasor rules

- Render decoded SV samples, not a synthetic sine fallback, when raw samples are available.
- Use a coherent fixed-length window for the selected timebase.
- Do not stretch partial sample arrays across the full plot width.
- Timebase options must change the actual engine window, not only a UI label.
- Shape/distortion analysis may use a faster recent-sample window, but visual waveform, RMS, and phasor must retain clear source and stream ownership.
- UI controls must not tween or animate raw engineering values in a way that hides real changes.

## Protocol and SCL rules

- Treat malformed BER/APDU length fields as untrusted input.
- Distinguish publisher identity from subscriber expectations.
- Duplicate APPID alone is a design warning; hard conflict requires stronger identity collision evidence.
- Preserve raw evidence when semantic mapping is uncertain.

## Timing and claim language

Use wording such as `arrival timing`, `software timestamp`, `screening`, and `capture-path confidence`. Do not use `certified`, `measurement-grade`, `conformant`, `deterministic`, `safe`, `secure`, or universal interoperability claims without the exact evidence and authority required.

The software does not establish functional safety, cybersecurity approval, calibrated measurement, equipment isolation, switching authority, or proof that an IED accepted or acted on observed traffic.

## External-material boundary

External software may be used only as a lawfully licensed black-box interoperability or packet-observation endpoint. Do not use unrelated external source, generated bindings, API composition, internal structure, tests, wording, screenshots, icons, report layouts, UI composition, binaries, or extracted resources as implementation design material without documented rights.

Use synthetic or contributor-owned fixtures. Real SCL, PCAP, screenshots, logs, and diagnostics require documented authorization and sanitization.

## Licensing boundary

- Current community revisions and current packages are `GPL-3.0-or-later` only.
- Historical Apache-2.0 revisions end at `85d43a0fe58a5888a9e8008c168ab76d2333ea87` and remain on `archive/apache-2.0-final`.
- Commercial rights require a separate negotiated and executed agreement.
- The commercial notice grants no additional rights by itself.
- Third-party components retain their own licenses and notices.

## Repository quality gate

Before a pull request:

```powershell
.\scripts\repository-health.ps1
dotnet restore .\ProcessBusSuite.sln
dotnet build .\ProcessBusSuite.sln -c Release --no-restore
dotnet test .\ProcessBusSuite.sln -c Release --no-build
```

Update tests and documentation whenever runtime behavior, user-visible wording, screenshots, versioning, licensing, or release packaging changes.

## Version and release

- `Directory.Build.props` is the repository version source.
- README package examples, landing-page structured data, release workflow defaults, and release notes must match it.
- Release artifacts must include checksum, release manifest, current GPL text, commercial notice, copyright, trademark, third-party notices, and licensing summary.
- Do not tag a stable release while selected-stream updates, timebase behavior, or waveform/phasor/RMS coherence remain nondeterministic.