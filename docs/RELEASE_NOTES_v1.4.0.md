# Process Bus Insight v1.4.0 — Stable

Public Windows x64 portable release of **Process Bus Insight**, the receive-only IEC 61850 SV / GOOSE / PTP analyzer.

## Operator improvements

- Five uncluttered primary workspaces: Analyzer, Diagnostics, GOOSE, Timing/PTP, and SCL.
- Advanced raw packet inspection and engineering validation stay accessible through the Tools menu instead of adding navigation tabs.
- SCL mismatch badges and expected/observed svID text remain readable with natural-height rows and multiline wrapping. Redundant column and explanatory captions are removed; full matched evidence is still available in technical details and tooltips.
- SV real-sample waveform display, zero-cross alignment, stream-aware auto peak scaling, calibrated-unit selection, phasors and RMS.
- Per-stream diagnostics: arrival jitter, smpCnt discontinuity, missing samples, svQuality/smpSynch and capture-path indicators.
- GOOSE event timeline, PTP context, SCL stream catalog, dataset order and compare-to-wire evidence.

## Install and verify

1. Download `ProcessBusInsight-v1.4.0-win-x64-portable.zip` from this release.
2. Compare SHA256 with `SHA256SUMS.txt` and extract the portable application.
3. Install Npcap separately for live network capture. For offline analysis, open a compatible classic Ethernet PCAP.
4. Select the capture adapter and click Run. Use SCL Import to load SCD/CID/ICD/IID for expected/observed identity comparison.

## Important engineering limitations

**Stable** denotes a packaged software release, not a calibrated or IEC 61850-conformance-certified test instrument. No protection operation or SV/GOOSE publishing is included. Captured traffic does not prove a subscriber acted on it.

- Normal Windows/Npcap timestamps and PCAP replay provide timing **screening evidence**; they are not proof of clock lock, precise jitter, or synchronization accuracy without independently validated capture and timestamping equipment.
- The 4I+4V nominal profile is an engineering display scaling assumption unless corresponding SCL mapping/instrument calibration is established.
- A stream with a different svID from expected SCL correctly reports mismatch even if APPID, destination MAC, VLAN and confRev agree.
- Real field acceptance, DPI clipping, USB Ethernet capture timing, packet losses and sustained CPU/GC must be verified in the intended environment.
- This release is licensed **GPL-3.0-or-later**. Review LICENSE, NOTICE, third-party and commercial-license documentation for the applicable terms.

## Verification

Windows Release build, xUnit regression suite, runtime stability and CodeQL / dependency audit are required before publication. The release workflow additionally rebuilds, tests, packages, and verifies the portable ZIP and SHA256 manifest.
