# Process Bus Insight v1.4.2 — GOOSE Selection Hotfix Candidate

This hotfix follows published v1.4.1 and is **not** an IEC 61850 certification or a claim of completed physical Windows field acceptance.

## Operator-visible changes
- Publisher explorer holds a stable selected publisher when GOOSE snapshots update rapidly.
- Inspector live values use the latest coherent selected-publisher frame; clicking a timeline row deliberately pins historical evidence without overwriting the chosen live publisher.
- Explicit selection persists across retransmissions, filtering, and temporary publisher absence. STALE retains last valid values and last-received time.
- Publisher identity includes source MAC, APPID, VLAN, and GoCBRef, preventing collisions between similarly named control blocks.
- Typed DataSet values still use generic MMS decode when SCL is unavailable, and resolve matching SCL mapping when supplied.
- No layout spacing, phasor, waveform, PTP, or SV decoder changes.

## Verification before public release
A Windows hotfix candidate must pass repository health, Windows CI/test, Runtime Stability, Runtime Architecture, CodeQL, verified Inno installation/repair and exact-SHA BuildInfo provenance. A physical click/selection stream test remains a separate required check (100%, 125%, 150% DPI; sustained retransmissions, multiple publishers and filters).

**Distribution:** candidate artifacts produced by the PR workflow are not a published stable v1.4.2 release. Preserve v1.4.1 tag and published assets. Explicit approval required to release the hotfix.

## Engineer-focused GOOSE readout refinement

- Compact chevrons instead of prominent circular disclosure controls, with stable expanded raw MMS details during high-rate retransmissions.
- Each unmapped entry is explicitly named `Entry n`; signal references appear only from matching SCL data.
- `TEST` badges on publisher and Inspector, distinct `RX frames` and `visible events` counters, and GOOSE-specific capture status.
- Explicit `LIVE / STALE / PINNED EVENT` context. Returning to Live clears the timeline's historical selection.
- Strict 13-bit IEC 61850 q bit-string padding validation and additional positive/negative regression coverage.

Manual Windows GUI/display-scaling field acceptance remains a separate approval gate.
