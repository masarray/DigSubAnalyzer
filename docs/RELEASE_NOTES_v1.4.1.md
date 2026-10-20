# Process Bus Insight v1.4.1 — Stable

**Process Bus Insight** is a free, open-source Windows analyzer for monitoring IEC 61850 Sampled Values (SV/SMV), GOOSE, and Precision Time Protocol (PTP) traffic. Version 1.4.1 delivers a quieter, faster engineering workflow, multi-stream visibility, and an improved Windows installation experience.

## Highlights

- **Five focused workspaces:** Analyzer, Diagnostics, GOOSE, Timing/PTP, and SCL, with a compact, consistent, low-distraction dark interface.
- **Live multi-stream SMV monitoring:** stream explorer, waveform plots, phasors, RMS and angle measurements, power estimates, and fast stream switching.
- **Readable waveform and phasor presentation:** voltage and current share consistent channel colors; phasor arrows, labels, and U/I RMS scaling remain legible.
- **Per-channel freshness:** invalid/old channel data are clearly marked HELD or MISSING without hiding healthy phases; stale streams retain the last valid evidence in a muted appearance.
- **Actionable diagnostics:** SV continuity and timing findings, quality flags, capture-driver statistics, and grouped repetitive timing reports with preserved detailed evidence.
- **GOOSE inspection:** discover publishers, inspect state changes and retransmissions, and review typed DataSet values.
- **PTP visibility:** monitor message flows, Grandmaster identity, domains and traffic observations.
- **SCL engineering context:** import SCD, CID, ICD or IID files to compare configured publishers, stream identities and DataSet mappings against received traffic.
- **Improved capture and packaging:** robust Npcap adapter handling, reliable Start/Stop workflow, reproducible Windows x64 Inno installer with in-place update/repair, portable ZIP, and offline manuals.

## Download and installation

**Recommended:** `ProcessBusInsight-v1.4.1-win-x64-setup.exe` (Windows x64, self-contained .NET 8 application). The optional portable ZIP is intended for users who prefer manual deployment.

1. Download the installer and verify it against `SHA256SUMS.txt`.
2. Run the Inno Setup installer. The install directory includes `User Manual.pdf`, `Quick Start.pdf`, Markdown manuals, and `Install and Update.md`.
3. To update, close Process Bus Insight and run the new installer; the existing installation directory and shortcuts are maintained.
4. Install Npcap separately to capture live Ethernet traffic; classic Ethernet PCAP replay is available offline.
5. Choose the capture adapter, click **Run**, and select an SV stream.

This is a **receive-only monitoring and analysis application**. PTP timing statistics are based on the available capture timestamps; nominal 4I+4V scaling follows the selected engineering profile. The application does not transmit SV or GOOSE commands.

## Verification and distribution

The stable build is created from `main` by GitHub Actions following Windows Release build, automated regression tests, runtime stability/architecture checks, security analysis, package checksum verification, and Inno install/repair/uninstall smoke tests. Real-world performance also depends on NIC, Npcap, traffic and Windows configuration.

- **License:** GPL-3.0-or-later; source code and license notices are available in this repository.
- **Assets:** Inno setup EXE, optional portable ZIP, `SHA256SUMS.txt`, and `release-manifest.json`.
- **Installer:** unsigned; Windows may display its normal unknown-publisher prompt.
