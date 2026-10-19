# Process Bus Insight v1.4.1 — Release notes (stable publication gated)

Proposed public Windows x64 release of **Process Bus Insight**, the receive-only IEC 61850 SV / GOOSE / PTP analyzer. **Do not publish this build as stable until the current installer passes the documented physical acceptance gate** in `docs/validation/V1.4.1_PUBLIC_RELEASE_ACCEPTANCE.md`.

## Operator improvements

- Five uncluttered primary workspaces: Analyzer, Diagnostics, GOOSE, Timing/PTP, and SCL.
- Advanced raw packet inspection and engineering validation stay accessible through the Tools menu instead of adding navigation tabs.
- SCL mismatch badges and expected/observed svID text remain readable with natural-height rows and multiline wrapping. Redundant column and explanatory captions are removed; full matched evidence is still available in technical details and tooltips.
- SV real-sample waveform display, zero-cross alignment, stream-aware auto peak scaling, nominal engineering-unit selection, phasors and RMS. Nominal scaling is **not** metrological calibration.
- Compact overlay fasors distinguish solid voltage / dashed current strokes, use separate relative RMS scales, display numerical U/I maxima and preserve an exact measured angle from the selected-stream engine.
- Clear amber `LIVE · WARN` badge for an active stream with continuity/arrival warnings; green waveform LIVE means activity only. Stale vectors are explicitly held, not described as live.
- A native Npcap architecture/load problem cannot be represented as a capturable dummy Ethernet interface. The app shows an unavailable diagnostic until a valid driver/NIC is detected.
- Per-stream diagnostics: arrival jitter, smpCnt discontinuity, missing samples, svQuality/smpSynch and capture-path indicators.
- GOOSE event timeline, PTP context, SCL stream catalog, dataset order and compare-to-wire evidence.

## Install and verify

1. Download **`ProcessBusInsight-v1.4.1-win-x64-setup.exe`** and compare SHA256 with `SHA256SUMS.txt`.
2. Run the Inno Setup installer. It includes self-contained application files, **User Manual.pdf**, **Quick Start.pdf**, updated Markdown manuals, and **Install and Update.md** directly in the install folder. Start Menu manual shortcuts are available offline.
3. To update, close the application and run the newer installer; it retains the installation directory and can repair the same version. The optional portable ZIP remains available for manual use.
4. Install Npcap separately for live capture. For offline analysis use a compatible classic Ethernet PCAP.
5. Select a real Ethernet adapter and Run. Use SCL Import to load SCD/CID/ICD/IID for expected-vs-observed identity comparison. The installer is currently unsigned and does not provide unattended auto-updates.

## Important engineering limitations

**Stable** denotes a packaged software release, not a calibrated or IEC 61850-conformance-certified test instrument. No protection operation or SV/GOOSE publishing is included. Captured traffic does not prove a subscriber acted on it.

- Normal Windows/Npcap timestamps and PCAP replay provide timing **screening evidence**; they are not proof of clock lock, precise jitter, or synchronization accuracy without independently validated capture and timestamping equipment.
- The 4I+4V nominal profile is an engineering display scaling assumption unless corresponding SCL mapping/instrument calibration is established.
- A stream with a different svID from expected SCL correctly reports mismatch even if APPID, destination MAC, VLAN and confRev agree.
- Real field acceptance, DPI clipping, USB Ethernet capture timing, packet losses and sustained CPU/GC must be verified in the intended environment.
- This release is licensed **GPL-3.0-or-later**. Review LICENSE, NOTICE, third-party and commercial-license documentation for the applicable terms.

## Verification

Windows Release build, xUnit regression suite, runtime stability, CodeQL and dependency audit are required before publication. The release workflow builds and verifies the Inno installer **and** optional portable ZIP, checks SHA256, then smoke-tests silent install, repair, offline manuals and uninstall. **Physical Npcap/NIC, injector comparison, 60-minute current-build soak and display/DPI acceptance are separate blocking checks; historical tests do not substitute.** A public release is an explicit, manual, main-only action after those checks are documented.
