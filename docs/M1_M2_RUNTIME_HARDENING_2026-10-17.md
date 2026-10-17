# M1/M2 receiver hardening — 17 October 2026

Scope: Process Bus Insight / DigSubAnalyzer receive-only application. This record
describes the bounded software tranche proposed in PR #29. Physical interoperability
and timestamp accuracy require separate evidence.

## Non-negotiable IEC 61850 boundaries

- The ARStack injector's canonical SCL-derived multicast/VLAN/APPID/svID identity and
  broadcast `AR_DIAG_SV1` mirror are distinct streams. A mirror observation cannot
  establish canonical multicast/VLAN acceptance.
- One frame may contain multiple SV ASDUs; each ASDU contributes to sample sequence,
  but the frame has only one host receive timestamp. Do not invent intra-frame jitter.
- `SmpRate/SmpMod` are sampling metadata, not universal sample-counter modulo rules.
  Counter wrap conclusions remain bounded by observed and profile-supported evidence.
- The eight (INT32 instMag, UINT32 q) pairs are a 64-byte 4I+4V *profile-specific*
  decoding authority. Never apply their quality interpretation to arbitrary SCL
  layouts solely because the payload is 64 bytes.
- Quality with unusable validity/detail flags immediately invalidates displayed
  engineering measurements; wait for a new complete coherent window before resuming.
  A TEST or operator-blocked quality remains visible with its truthful context.
- PTP observations are scoped to domain/sourcePortIdentity/transport; observation
  does not imply clock lock, grandmaster traceability, power-profile compliance,
  or reliable software receive timing.

## Data plane and UI rules

- One dedicated capture pump preserves input ordering. Do not introduce independent,
  unordered parallel SV workers or silent lossy queues merely to increase thread count.
- Npcap owns raw frame memory only until its next API read. The source transfers
  a newly allocated owned frame to the analyzer. The latest payload is retained for
  debug-on-demand; indiscriminate ArrayPool return would corrupt this evidence.
- Per-ASDU projections use direct bounded INT32 reads and stack-local eight-channel
  values. SCL eligibility and explanatory labels refresh when mapping/sequence state
  changes, not for each SV frame.
- Rolling jitter maximum uses a bounded monotonic ring rather than scanning the
  entire window for each sample. Retire the unused counter dictionary/linear scan.
- The active per-stream coherent waveform buffer is the single snapshot authority.
  RMS/phasor/waveform projections of the same generation reuse that window; an
  invalid quality state must invalidate prior measurements, never show stale values
  as live.
- User-visible SCL comparisons may be coalesced to 500 ms, with explicit user SCL
  reload forcing immediate recomputation.
- Stream history has a 256-identity guard. Reclaim only stale unselected state;
  when at active capacity, report rejection rather than silently evicting a stream
  currently being inspected.
- PTP source history is bounded and stale sources are labeled STALE instead of
  green OBSERVED; the interface should emphasize the fault and next diagnostic
  action rather than flooding raw details.

## Automated acceptance

`dotnet build ProcessBusSuite.sln -c Release` and `dotnet test
ProcessBusSuite.sln -c Release` plus repeated RuntimeStability acceptance must pass.

New regressions include:

- O(1) rolling jitter reference equivalence under a 30,000-sample run;
- invalid-quality suppression, complete coherent recovery, and TEST labeling;
- multi-ASDU continuity with a single frame timestamp and independent frame count;
- per-domain PTP source isolation and stale Announce;
- strict PTPv2 length/transportSpecific parsing;
- bounded stream identity churn and preserved selected stream;
- rejection of `SmpRate` as an unverified counter modulus.

## Evidence still required before field/product claims

1. Trusted independent canonical multicast/VLAN capture from ARStack ESP32-P4.
2. Same-run canonical SV (4000/4800 samples/s) plus Announce, Sync, Follow_Up,
   Pdelay and `smpSynch` transition stimuli.
3. Npcap/adapter drop counters, host CPU/GC/memory, sustained capture throughput,
   and 60+ minute bench soak with no cross-stream contamination.
4. Hardware timestamp validation for any measurement-grade jitter/offset statement.
5. Multi-vendor profile/SCL fixtures, arbitrary `nofASDU` and vendor DataSet layouts,
   and receiver/IED behavior with real devices.

The software tranche avoids claiming those field results. A green CI result means
the bounded software regressions passed, not that an IED accepted the traffic.
