# AGENTS

## Product Mission

Build **Process Bus Insight** as a receive-only, raw-passive IEC 61850 Process Bus diagnostic instrument for FAT/SAT, commissioning, interoperability troubleshooting, public engineering learning, and substation automation portfolio use.

The product must help an engineer answer, without Wireshark first:

- What SV streams are present and which one is unhealthy?
- What GOOSE publishers are present and which one changed state?
- Is PTP present, via which transport, and what timing confidence is allowed?
- Which specific stream / publisher / timing source is affected?
- What evidence supports the finding, and what is still only screening-level?

## Non-Negotiable Product Boundary

The analyzer must remain receive-only and raw-passive:

- Product app: `ProcessBus.App.Wpf`
- Shared model/state: `ProcessBus.Core`
- Raw decoder/capture engine: `ProcessBus.Iec61850.Raw`

The product app must not reference, load, or call `libiec61850` or any external IEC 61850 subscriber stack. Legacy wrapper/subscriber/publisher experiments may exist only outside the product package and must never be reintroduced into the WPF analyzer runtime.

## Current Technical Direction

- C# / WPF / .NET 8 Windows.
- Npcap raw frame capture.
- Internal raw Ethernet parser for SV and GOOSE.
- PTP passive visibility for native Ethernet `0x88F7` and UDP/IPv4/IPv6 `319/320`.
- SV stream identity must use a strong key: source MAC + destination MAC + VLAN + APPID + svID + confRev.
- GOOSE inspector must show typed DataSet values before SCL mapping is available.
- Diagnostics must be target-aware, not only global.

## Target-Aware Diagnostics Rule

Never show a serious warning without an affected target.

Diagnostics must make it clear whether the issue belongs to:

- a specific SV stream,
- a specific GOOSE publisher/control block,
- a PTP source / grandmaster / domain,
- or the capture path / adapter itself.

The left diagnostics rail should behave as a **Traffic Health Navigator**:

```text
SV Streams
GOOSE Publishers
PTP Sources
```

Selecting a target must scope the diagnostics view and, where safe, select the related stream/publisher in Analyzer or GOOSE view.

## Timing / Jitter Policy

The app may detect arrival timing excursions from packet timestamps and `smpCnt`-derived expected interval.

Do not claim certified or metrology-grade jitter measurement unless:

1. the capture path uses validated hardware timestamps, or
2. the result is externally cross-checked with trusted timing equipment/TAP.

Use these labels:

- `Arrival Variation`
- `Arrival Timing Excursion`
- `Timing Confidence: LOW / SCREENING / HIGH`
- `Timestamp Source: Npcap/software` unless hardware timestamping is actually validated.

Do not use product wording that implies certified jitter measurement from normal Windows/Npcap timestamps.

## PTP Implementation Rule

PTP is a timing context and confidence layer, not just a badge.

Required information:

- transport: UDP/IPv4, UDP/IPv6, or Ethernet `0x88F7`,
- message type: Sync, Follow_Up, Announce, Delay/PDelay,
- domain,
- grandmaster identity,
- clock class / accuracy / steps removed,
- Sync / Announce / Follow_Up rates,
- freshness/stale/lost state,
- raw PTP event timeline so users do not need Wireshark for basic confirmation.

PTP must not enter SV sample buffers, SV sequence logic, or phasor/waveform rendering. PTP may only affect timing confidence and timing interpretation.

## Waveform / Phasor Stability Rule

The instrument view must not visually jump because of bad frames.

- Update waveform/phasor only from the selected SV stream key.
- Reject or hold display on duplicate, out-of-order, or sequence-jump samples.
- Keep diagnostics counters for rejected/unstable packets.
- Prefer `smpCnt`-aligned display windows over last-packet anchoring.

If a stream is unstable, the UI should say so clearly instead of letting the waveform appear randomly wrong.

## GOOSE Inspector Rule

GOOSE must be useful without SCL, then better with SCL.

Minimum product behavior:

- event timeline grouped by publisher/control block,
- stNum/sqNum visible,
- typed allData decoding such as Boolean, BitString, Integer, Float, String, UTC time,
- changed-value summary,
- inspector panel with MAC, APPID, VLAN, DataSet, confRev, TTL, test/ndsCom,
- SCL semantic labels later.

Avoid showing `raw allData N bytes` as the primary value when typed decode exists.

## Visual / UX Rules

The app should feel like a mature engineering instrument, not a debug console.

- Header right side is for adapter + Run / Stop / Clear.
- Engine mode badges should be secondary status, not primary action.
- Main workspaces must be lean and readable.
- Advanced workspace is allowed to be raw/debug heavy; main workspaces are not.
- Avoid monospace typography in normal UI; reserve it only for raw hex/debug evidence.
- Reduce repeated footers, helper text, and status strips.
- Every screen should answer the engineer's next decision quickly.

## Engineering Layout & Data Grid Rule

Never ship a layout patch that makes engineering information unreadable, clipped, overlapped, or dependent on the user guessing hidden content.

WPF/XAML layout work must follow these rules:

- Before changing visual layout, inspect the active global styles in `Themes/*.xaml`, especially `DataGridRow`, `DataGridCell`, `RowHeight`, `Height`, `MaxHeight`, `MinHeight`, `TextWrapping`, `TextTrimming`, and virtualization settings. Local `MinRowHeight` or wrapping does not help if a global row style hardcodes `Height`.
- Main engineering grids must prefer readable rows over dense debug rows. Use auto row height for semantic/validation/evidence grids: `RowHeight="{x:Static sys:Double.NaN}"`, no fixed `DataGridRow.Height`, and a realistic `MinRowHeight` such as 72-120 px when rows contain multi-line expected/observed/evidence text.
- Use fixed compact row height only for raw packet/event lists where every cell is intentionally one-line forensic data. Do not reuse raw/debug grid density for SCL, validation, semantic signal, or evidence views.
- If a row contains expected-vs-observed evidence, it must show enough text to be understandable at 1080p. If the evidence is too long for a row, provide a selected-row detail panel/drawer/inspector instead of silently truncating the only useful evidence.
- Do not solve visual clutter by cutting information with `TextTrimming="CharacterEllipsis"` in primary engineering columns unless the full value is visible in the same screen via a detail panel, tooltip, or inspector.
- Use `TextWrapping="Wrap"` and top-aligned cells for multi-line engineering rows. Use `NoWrap` only for short IDs, counters, status badges, timestamps, APPID, VLAN, and compact raw fields.
- Avoid nested cards and repeated framed boxes. Prefer clear page structure: left navigator, center decision/work table, right inspector/detail. Use whitespace and section titles to express hierarchy.
- Navigation/explorer cards must be short and stable. They should show identity, status, and one or two key fields only. Long semantic mappings, evidence strings, raw elements, and phase-order details belong in the center or right detail panel.
- Validation dashboards must support multiple IEDs. Every validation row must carry IED/scope, object, expected, observed, status, and evidence. Global PASS/WARNING/FAIL cards are only summaries, not substitutes for per-target evidence.
- Advanced workspace may be forensic-heavy, but it still must not clip evidence. Long raw/engineering panels need enough vertical space or a parent `ScrollViewer`; do not trap important text in small fixed `MaxHeight` boxes.
- Avoid `MaxHeight` on important engineering/evidence text unless the same content is available in a larger selected detail area. Prefer scrollable parent regions over multiple tiny nested scroll viewers.
- For large-screen WPF layouts, use `Auto` for natural headers/toolbars, `*` for remaining work areas, and explicit minimum sizes for tables/panels that must remain readable. Do not let a `StackPanel` or fixed row starve a grid that needs vertical space.
- After visual changes, verify at 1600x900 and 1920x1080 scale: no clipped text, no hidden primary evidence, no overlapping row content, no horizontal scroll in main workspaces unless it is explicitly raw/advanced data, and selected rows remain readable.
- Treat Microsoft WPF layout/DataGrid behavior and established web-app table practices as the baseline: readable row density, responsive sizing, clear hierarchy, and detail-on-selection for long content.

## Scope Control

Do not add these before the target-aware receive path is stable:

- publisher/control workflows,
- replay productization,
- COMTRADE,
- FFT/THD,
- advanced report designer,
- GOOSE control,
- full SCL editor.

Allowed next product features:

- SCL read-only mapping,
- expected-vs-observed validation,
- PCAP playback for support/reproduction,
- compact FAT/SAT report.

## Required Status Reporting Per Stage

Always report:

1. what was changed,
2. what is now more defensible,
3. what remains screening-level or unproven,
4. what needs physical NIC/TAP validation,
5. the next safest patch.

## Stable Workspace UX Rule

Real-time traffic must never make navigation targets jump around.

- Explorer lists must keep **first-seen order** by default.
- Do not sort live cards by `LastSeen`, packet counter, or severity unless the user explicitly selects that sort mode.
- Do not `Clear()` and rebuild visible explorer collections on every refresh.
- Update existing target rows by key and append new targets at the end.
- Selection must be locked by strong target key, not by display name.
- When switching a target, show a short, calm loading/transition overlay. Do not show loading animation for normal live refresh.

## Per-Stream Mapping and Phase Order Rule

SV channel mapping is a per-stream concern. Never assume all SV streams have identical dataset order.

- Mapping profile must be scoped to the SV stream key.
- If Ub/Uc orientation differs from the expected profile, show `phase order suspect`; do not silently correct it.
- The Advanced workspace must expose per-stream RMS/angle evidence so an engineer can verify possible S/T or B/C swap.
- SCL mapping is the final source for semantic channel names; generic raw mapping is only a candidate until SCL is loaded.

## Advanced Workspace Rule

Advanced is a forensic target inspector, not a hardcoded SV1 dump.

- The left rail must list SV streams, GOOSE publishers, and PTP sources.
- Selecting a target changes the center panel evidence.
- SV shows raw elements, channel mapping, phase order, timing, and RMS evidence.
- GOOSE shows header fields and typed DataSet values.
- PTP shows raw event list and timing source evidence.


## SCL Semantic Mapping Direction

The project direction is SCL-first, not raw-packet-only. Before implementing expected-vs-observed validation, always implement semantic stream mapping:

1. Load SCL/CID/ICD/IID/SCD.
2. Detect IEC 61850-6 edition/namespace style.
3. Parse IED, LDevice, LN/LN0, DataSet, GSEControl, SampledValueControl, Communication/GSE/SMV addresses, and DataTypeTemplates.
4. Bind live SV/GOOSE streams to expected SCL streams using APPID, MAC, VLAN, svID/goID, control block, DataSet, and confRev.
5. Map payload order to DataSet entry order.
6. Resolve signal reference, FC, CDC, bType, type ID, and enum type.
7. Then perform expected-vs-observed validation.

Do not hardcode one global SV channel order as final truth. Mapping must be per stream and preferably SCL-backed. Without SCL, use lab profiles only as temporary hints and clearly label them as inferred.


## SCL Engineering Workspace Rule

SCL is a first-class engineering context workspace, not an Advanced/debug panel.

Required behavior:

- The product must support multiple imported SCL documents in one engineering context.
- Supported input types are SCD, CID, ICD, IID, and XML-based SCL exports.
- The SCL workspace must show imported document cards, IED cards, SV/GOOSE semantic stream catalog, DataSet entry order, and parse warnings.
- Live stream binding must be based on stable scoring, not a single brittle key. Use APPID, destination MAC, VLAN, svID/goID, control block, DataSet, confRev, and IED/source hints.
- Conflicts must be visible: duplicate IED names, duplicate APPIDs, different confRev, different DataSet order, or multiple candidate bindings.
- Advanced may expose raw SCL details, but semantic mapping belongs in the SCL workspace.

Done when an engineer can import several vendor files and understand which IEDs, GOOSE controls, SV controls, DataSets, and signal orders are available before expected-vs-observed validation begins.

## SCL Live Binding Matrix Rule

SCL import is not complete until expected streams are compared with live traffic.

Required behavior:

- The SCL workspace must show an expected-vs-observed binding matrix.
- Binding status must be explicit: `MATCHED`, `WEAK`, `MISSING`, `UNEXPECTED`, later `MISMATCH` and `CONFLICT`.
- Do not rely on one brittle key. Use scoring from APPID, destination MAC, VLAN, svID/goID, control block reference, DataSet, confRev, and IED/source hints.
- Missing expected streams and unexpected live streams are both important commissioning evidence.
- Binding matrix selection should drive the semantic inspector so the engineer can move from finding to evidence quickly.
- Never silently auto-correct semantic mapping. Show evidence and confidence.

## Public Repository Packaging Rule

Public-facing repo work must keep README, landing page, release notes, and package quick-start material written for users who want to understand, download, run, build, and contribute.

Do not write public docs as internal audit notes. Avoid wording such as "owner should", "next step for maintainer", or "audit found" in README, landing page, package notes, or release body.

For Windows desktop releases, keep the portable package flow working:

```text
scripts/publish-windows-portable.ps1
scripts/verify-release-package.ps1
.github/workflows/release-package.yml
```

The package should contain the published app, quick-start note, license, notice files, third-party notices, and a convenience launcher only when it helps desktop users run the app.
