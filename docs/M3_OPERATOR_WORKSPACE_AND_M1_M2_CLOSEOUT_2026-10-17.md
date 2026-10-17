# M3 operator workspace and M1/M2 closeout — 17 October 2026

This engineering handoff describes the implemented changes on PR #29, and
distinguishes a successful software acceptance run from hardware interoperability.

## Operating contract

Process Bus Insight remains a **receive-only** IEC 61850 SV/GOOSE/PTP analyzer.
The operator workflow is:

1. Select the physical Ethernet/Npcap adapter.
2. Press **Start** to observe traffic.
3. Choose an SV publisher in the explorer to inspect waveform, RMS, phasors,
   stream identity, quality and counter continuity.
4. Read the one-line context-aware status for the current situation.
5. Open **Diagnostics** for capture-driver drops vs SV sequence evidence, and
   **Timing/PTP** for passive source-specific PTP evidence.
6. Open **SCL/Validation** only to compare engineering-file expectations with
   actual wire-observed identities; **Advanced** holds low-level raw evidence.

No value inferred from the diagnostic broadcast mirror is presented as proof
of canonical multicast, APPID, VLAN, PCP or SCL profile acceptance.

## M3 implementation

- Main capture-status strip displays one concise contextual message instead of
  five overlapping status chips. The persistent footer is short and conditional:
  it surfaces diagnostic-mirror, invalid-quality and stale-stream concerns.
- Real SV measurements are never tweened/interpolated between capture snapshots.
  Dead 30 FPS composition-loop/interpolation code was removed from the waveform
  control: WPF paints the latest decoded, coherent snapshot atomically.
- `LiveRenderCoalescer` compares **every numeric waveform sample** plus
  series/shape/status metadata and channel RMS/angle/instant/units. It suppresses
  equivalent redraw notifications while preserving tiny real transients, shape
  changes, quality invalidations, stream selections and cleared values.
- Phasor lists and per-stream UI workspace objects are reused while unchanged.
  Periodic SelectedStream/IsRunning re-notifications were eliminated to prevent
  selection churn.
- Diagnostic navigator refresh is capped to the passive UI cadence;
  managed-memory sampling runs at 1 Hz; SCL binding joins are coalesced.
- Engineering tables use recycling/row virtualization and shorter minimum row
  heights. Static flow instructions were removed from the main content.
- PTP two-step sequence correlation is passive and displayed only in the Timing
  workspace; there is **no** claim of measured PTP lock or UTC traceability.

## M1/M2 additional correctness/efficiency

- PTP packet-rate tracking uses fixed 5-second buckets, not an unbounded list
  of packet arrival timestamps per source.
- Announce/Sync/Follow_Up evidence is isolated by source/domain/transport;
  Sync+Follow_Up pairing additionally requires matching sequence and bounded
  freshness. Source history and pending correlators have hard capacity limits.
- Diagnostic/event collection indexes persist across UI refreshes, and remain
  bounded at 200 visible rows; reset removes both rows and indexes.
- Stable SV explorer rows are retained rather than replaced on every update.
- Npcap native drop statistics are polled at 1 Hz off the capture hot path.
  Driver drop counters are displayed as **capture-path evidence**, and are never
  folded into IEC 61850 sample-counter packet-loss inferences.
- Full IEEE 61850 profile compliance, PTP servo discipline, calibrated host
  jitter, and physical IED subscription are not asserted here.

## Automated regression gates

Windows .NET 8 full build and complete xUnit suite must pass together with
repeated RuntimeStability, CodeQL, and Dependency Security Review workflows on
the **same final commit**.

New coverage includes render-coalescing correctness, multi-ASDU timing,
quality invalidation/recovery, rate-window boundedness, cross-source two-step
correlation, stream catalog pressure and reset-safe UI event deduplication.
Do not merge if final-commit CI is red or incomplete.

## Physical and measured acceptance still necessary

The following must be collected on real hardware before an
**interoperable/field-ready** release claim:

- independent preserved-VLAN TAP/SPAN capture of ARStack canonical multicast
  alongside the separately labeled diagnostic mirror;
- 4000/4800 SV samples/s and concurrent L2 PTP source / receiving profile;
- verified observed APPID, VLAN, MAC, confRev, svID, quality, smpCnt rollover,
  two-step matching, source expiry and smpSynch transition evidence;
- actual Npcap drop counters, process CPU, allocation rate, Gen0/Gen1/Gen2 GC,
  heap/working set, UI frame responsiveness during sustained mixed traffic;
- 60+ minute hardware soak and multi-vendor IED capture/acceptance matrix.

**Receiver-worker decision:** maintain one ordered dedicated capture worker until
independent profiling demonstrates its insufficiency. Unbounded independent
workers or lossy frame queues would violate sample continuity. A multi-worker
dispatcher requires bounded queues, source-preserving ordering and explicit
drop accounting and remains a future measured-design gate, not a hidden
"optimization" in this PR.

The software milestone can be reviewed independently of that bench evidence.
