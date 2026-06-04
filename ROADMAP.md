# Process Bus Insight Roadmap

## Product Positioning

**Process Bus Insight** is a raw-passive IEC 61850 SV / GOOSE / PTP diagnostic instrument for digital substation commissioning, FAT/SAT, interoperability troubleshooting, and public engineering learning.

It should not become a Wireshark clone, a relay test set, a PTP grandmaster, or an IEC 61850 control client. Its value is:

```text
SV + GOOSE + PTP visibility
+ target-aware diagnostics
+ timing confidence
+ typed GOOSE values
+ SCL expected-vs-observed validation
+ evidence report readiness
```

## Runtime Boundary

Product projects:

```text
ProcessBus.App.Wpf
ProcessBus.Core
ProcessBus.Iec61850.Raw
```

The product runtime must remain raw-passive and must not reference `libiec61850` or external IEC 61850 subscriber stacks.

---

## Stage 1 — Raw Passive Foundation

Status: largely complete.

Done:

- Npcap raw capture path.
- Raw SV decode and stream discovery.
- Raw GOOSE decode.
- PTP transport support for Ethernet `0x88F7` and UDP `319/320`.
- Header capture controls moved to the top-right.
- Timing confidence wording introduced.

Remaining hardening:

- physical NIC/TAP validation,
- better adapter quality warnings,
- clean installer/release packaging.

---

## Stage 2 — Target-Aware Diagnostics

Status: current focus.

Goal: warnings must point to the affected target.

Required behavior:

- Diagnostics left rail becomes **Traffic Health Navigator**.
- SV, GOOSE, and PTP targets are listed with status and issue summaries.
- Selecting a target scopes the diagnostics page.
- Global warning must say whether the affected object is an SV stream, GOOSE publisher, PTP source, or capture path.
- Analyzer and GOOSE view should follow selected target where safe.

Done when:

```text
If only one of many SV streams is anomalous, the user can immediately see which stream is affected and inspect that stream.
```

---

## Stage 3 — Stable Instrument Rendering

Goal: waveform and phasor must be calm and trustworthy.

Required behavior:

- Stream key uses source MAC + destination MAC + VLAN + APPID + svID + confRev.
- Waveform/phasor update only from the selected stream key.
- Duplicate/out-of-order/sequence-jump frames do not directly disturb the visual instrument display.
- Diagnostics still count and expose bad frames.
- Move toward `smpCnt`-aligned display windows instead of last-packet anchoring.

Done when:

```text
SV instability is reported as a finding; the waveform does not randomly slide or rotate because of bad samples.
```

---

## Stage 4 — PTP-Aware Timing Architecture

Status: in progress.

Done:

- PTP UDP/IPv4 from PTPSync is decoded.
- PTP event table can show Sync / Follow_Up / Announce / PDelay.
- PTP status, GM, domain, clock class, and rates are visible.

Next:

- PTP freshness states: never observed / live / stale / lost.
- PTP event findings: GM changed, domain changed, announce timeout, sync timeout.
- Clock class interpretation.
- Timing confidence propagation to all workspaces.
- Keep software timestamp wording honest: screening only unless hardware timestamp validated.

Done when:

```text
User can confirm PTP traffic and timing context inside the app without Wireshark.
```

---

## Stage 5 — GOOSE Inspector Maturity

Status: semantic pass implemented.

Done:

- GOOSE event timeline.
- stNum/sqNum tracking.
- typed DataSet value decode.
- changed value summary.
- SCL DataSet entry mapping for selected GOOSE publisher values.
- Inspector displays signal reference and FC/CDC/bType when a matching SCL GOOSE stream is available.

Next:

- cleaner timeline layout and filtering.
- per-publisher health state.
- TTL/stNum/sqNum supervision.
- quality bit interpretation and common semantic value labels for breaker position, trip, alarm, and interlock signals.

Done when:

```text
GOOSE can be inspected similarly to IEDScout for header fields and typed values, then enhanced with SCL semantic names.
```

---

## Stage 6 — SCL Expected-vs-Observed Validation

Status: active productization.

Goal: become a commissioning checker, not just a viewer.

Required behavior:

- load SCD / ICD / CID read-only,
- extract expected SV streams,
- extract expected GOOSE publishers,
- compare observed APPID, VLAN, MAC, svID/goID, DataSet, confRev, and entry count,
- show missing / mismatch / unexpected traffic.
- show conflicts from duplicate APPID or contradictory multi-file SCL expectations.

Done:

- SCL workspace imports multiple documents into one engineering context.
- Expected SV/GOOSE stream catalog is visible.
- Live binding matrix shows MATCHED, WEAK, MISSING, UNEXPECTED, MISMATCH, and CONFLICT.
- Selected binding rows show expected-vs-observed evidence side-by-side.

Next:

- Promote SCL-backed SV semantic element mapping into validated render-channel mapping.
- CSV/JSON evidence export for the binding matrix.
- Dedicated validation dashboard with PASS / WARNING / FAIL / UNKNOWN.

Done when:

```text
Engineer can load SCL, start capture, and immediately see which expected traffic is present or mismatched.
```

---

## Stage 7 — PCAP Playback and Support Reproduction

Goal: make support and walkthroughs easy without hardware.

Required behavior:

- open PCAP/PCAPNG,
- replay or scan capture files,
- populate SV/GOOSE/PTP views from recorded traffic,
- ship sample PCAP + sample SCL.

Done when:

```text
A user can reproduce a case from a capture file without the live bench.
```

---

## Stage 8 — Reporting

Goal: compact engineering report for FAT/SAT and troubleshooting.

Required content:

- observed SV streams,
- observed GOOSE publishers,
- observed PTP source,
- target-aware findings,
- SCL mismatch results when available,
- timing confidence and limitations.

Do not overclaim timing precision.

---

## Near-Term Patch Order

1. Target-aware diagnostics and traffic health navigator.
2. Stable `smpCnt`-aligned waveform display improvements.
3. PTP freshness/event findings.
4. GOOSE filtering/typography cleanup.
5. SCL read-only mapping.
6. Expected-vs-observed validation.
7. PCAP playback.

---

## Stage 2B — Stable Workspace UX and Advanced Target Explorer

Status: in progress.

Goal: make the analyzer feel like a stable instrument while processing live streams.

Required behavior:

- SV stream explorer remains stable in first-seen order.
- Diagnostics target navigator remains stable in first-seen order.
- Live severity/last-seen updates must not make SV1/SV2 cards swap position.
- Selected stream/publisher/PTP source remains locked by strong key during refresh.
- Workspace target changes show a short glass transition overlay, but live refresh does not flicker.
- Advanced left rail becomes a raw target explorer for SV / GOOSE / PTP.
- Advanced center panel shows evidence for the selected target, not only SV1.

Done when:

```text
A user can run multiple SV streams live, click SV1 or SV2, and the card position and selected inspector remain stable while the waveform continues updating.
```

## Stage 3B — Per-Stream Mapping and Phase Order Audit

Goal: make phasor differences between SV streams explainable and auditable.

Required behavior:

- Mapping candidate is stored and shown per SV stream.
- Advanced view shows per-stream channel angle summary.
- App flags possible Ub/Uc or S/T swap as a warning candidate, not as a silent correction.
- Future SCL mapping must replace generic labels with actual dataset semantic names.

Done when:

```text
If SV1 and SV2 have different phase order, the engineer can inspect each stream and see whether the issue is payload order, mapping profile, or real signal orientation.
```


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


## Stage 4A — SCL Engineering Workspace

Status: implemented as first product pass.

Goal: make SCL a first-class engineering context layer rather than a small Advanced/debug panel.

Included:

- New `SCL` workspace tab.
- Multiple SCL document import in one session.
- Imported document cards with edition fingerprint, counts, and warnings.
- IED cards showing source file, vendor/type hints, SV/GOOSE/DataSet counts.
- SV/GOOSE semantic stream catalog with transport, DataSet reference, entries, and live-binding candidate status.
- Detail panel showing selected stream identity and DataSet entry order with signal reference, FC, CDC, and bType.

Next hardening steps:

1. Expand conflict resolver UX for duplicate IED names, duplicate APPID/MAC, confRev conflicts, and DataSet order conflicts.
2. Use SCL entry order to rename GOOSE DataSet values in the GOOSE inspector.
3. Use SCL entry order to build per-stream SV channel mapping and reduce phase-order ambiguity.
4. Promote binding evidence into a formal validation dashboard.
5. Add report/export for binding evidence.

## Stage 4B — SCL Live Binding Matrix

Status: implemented as engineering preview.

Goal: turn SCL from a passive semantic catalog into a live commissioning context that compares expected engineering streams against observed network traffic.

Included:

- Binding matrix in the SCL workspace.
- Expected SCL SV/GOOSE stream rows with status: `MATCHED`, `WEAK`, `MISSING`, `MISMATCH`, and `CONFLICT`.
- Unexpected live SV/GOOSE rows when traffic is observed but no imported SCL stream can explain it.
- Explainable scoring using APPID, destination MAC, VLAN, priority, svID/goID, control block, DataSet reference, and confRev where available.
- Selecting a binding row shows expected-vs-observed evidence side-by-side.
- Conflict screening catches duplicate expected APPID and contradictory duplicate control block definitions.

Next hardening steps:

1. Add richer per-field comparison columns: APPID, VLAN, MAC, confRev, DataSet, and score reason.
2. Add conflict resolver actions for multi-file SCL ambiguity.
3. Use SCL DataSet order to rename GOOSE inspector values.
4. Use SCL DataSet order to drive per-stream SV channel mapping.
5. Add report/export for binding evidence.
