# Process Bus Insight v1.4.2 — Stable

Process Bus Insight v1.4.2 is the public stable Windows x64 release focused on reliable IEC 61850 GOOSE engineering inspection. It builds on v1.4.1 and keeps the Analyzer/SV, Diagnostics, Timing/PTP and SCL workflows available.

## What changed

- **Stable GOOSE Sources:** persistent publisher identity based on source MAC, APPID, VLAN and GoCBRef; source selection survives continuous snapshots, retransmissions, search and temporary disappearance.
- **Compact Sources–Messages–Details workflow:** one-line Messages header with side-by-side *Show repeats* and *Follow live* switches, plus readable timestamp and physical-source filtering.
- **Navigate to actual GOOSE changes:** selecting a source with unread changes suspends live-follow, locates a decoded event, scrolls Messages to it, and pins the Inspector to the same captured event rather than the publisher's latest retransmission.
- **One acknowledgement state:** source unread notifications derive from the same bounded Messages event journal. Reading/acknowledging the currently inspected event through a click or Enter clears its unread alert without destroying the historical frame or acknowledging other events.
- **Quiet red notification dot:** a small red dot replaces verbose *Changed* labels in Sources; hover reveals the unread-event count.
- **Typed DataSet evidence:** the Inspector highlights only verified changed values while unread; the exact previous and new values appear in a tooltip rather than an extra row of text. Unmapped MMS values remain explicit raw entries, and SCL-derived signal references and quality interpretations are only shown when supported by matching configuration.
- **Safer event retention:** bounded buffers prioritize unread changes over retransmissions and disclose missed/evicted events rather than silently treating them as acknowledged.
- **Performance and stability:** incremental WPF source and DataSet rows, bounded history, virtualization, and no per-packet UI timers.

## Download and upgrade

**Recommended:** `ProcessBusInsight-v1.4.2-win-x64-setup.exe` — Windows x64, self-contained .NET 8 WPF application. A portable ZIP is available as an alternative.

1. Download the installer from this GitHub Release and compare its SHA256 with `SHA256SUMS.txt`.
2. Close any running Process Bus Insight instance and run the setup to update the existing installation in place. Inno Setup installation and repair are exercised automatically by the release pipeline.
3. Install Npcap separately if you need live Ethernet packet capture. Use your appropriate physical adapter, SPAN/TAP or replay source.
4. Offline User Manual and Quick Start PDF are included in the installation.

The application is receive-only: it does not inject SV, send GOOSE commands or control electrical equipment.

## Verification and scope

Version 1.4.2 is rebuilt on the default `main` branch by the official GitHub Actions stable-release workflow. Publication requires repository health, WPF layout contracts, Windows Release build, automated xUnit tests, package verification, and Windows installer installation/repair smoke tests. The PR-head engineering changes also passed runtime stability, runtime architecture, CodeQL and dependency security checks.

This is a **public stable community software release**, not an IEC 61850 conformance certificate or a claim that every NIC, relay vendor, traffic level and Windows scaling configuration has been physically validated. Real-world behavior depends on traffic, adapters, capture drivers and installation. Feedback from diverse hardware environments remains welcome.

- **License:** GPL-3.0-or-later. All notices and source code remain available in this public repository.
- **Windows installer:** unsigned, so Windows may show its standard unknown-publisher warning.
- **Release assets:** setup EXE, portable ZIP, `SHA256SUMS.txt`, `release-manifest.json`.
- **Previous release:** v1.4.1 remains available unchanged.
