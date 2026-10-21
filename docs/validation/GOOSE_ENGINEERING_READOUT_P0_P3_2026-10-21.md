# GOOSE Engineering Readout — P0–P3 Validation Handoff

Date: 2026-10-21. Scope: receive-only GOOSE engineering workspace. Baseline: selection/inspector single-source-of-truth hotfix plus v1.4.2 GUI candidate. This is **not** an IEC 61850 conformance claim.

## Engineering invariants

1. Selection is keyed to source MAC, APPID, VLAN, and GoCB reference; transient WPF null/retransmission does not delete operator intent.
2. **Live** Inspector always shows the latest coherent frame of the explicitly selected publisher. Choosing an event pins an immutable historical snapshot. Returning to Live explicitly clears timeline selection; switching publisher cannot leave a misleading historic highlight.
3. RX frame count is distinct from event count **visible after filters**. Retransmission toggle governs only event history visibility; it does not stop receiving retransmitted packets.
4. Raw MMS data without matching SCL is labeled `Entry 1…n` with its decoded wire type/value. Signal names, DPC meanings, and IEC 61850 q quality are never inferred from arbitrary bit patterns.
5. Only a matched SCL quality field with a valid 13-bit MMS BIT STRING can be interpreted as IEC 61850 q. Invalid unused-bit padding, unexpected width, bad hex, and non-BitString types fail closed.
6. The initial observed DataSet is a baseline, not a fabricated field change. Subsequent changes use prior observed values.
7. Test-mode publishers show explicit **TEST** identifiers; no red/green Boolean good/bad assumptions.
8. Keyed publisher rows and per-index DataSet rows survive passive refreshes without collapsing an opened MMS detail. Deliberately changing publisher or pinning a different event resets only that DataSet context.

## Visual contract

- Workspace shell and panel gutters: 8 DIP. GOOSE detail width: 460 DIP; other workspaces unchanged.
- Compact Signal / Value rows with a subdued chevron, expanded MMS raw value and payload hex, semantically resolved quality details only with a matching SCL.
- Publisher label and full reference available without high-contrast action ornamentation.
- Timeline remains a forensic event list, not a decorative panel filled with unrelated cards.
- Time-sensitive status strip reflects GOOSE traffic when the GOOSE workspace is active, without changing other workspaces' status.

## Required automatic gates

Windows repository health, workspace layout/source contract, full .NET solution compilation, unit tests, Runtime Stability, Runtime Architecture, CodeQL, dependency checks and Inno installer package/install/repair. These must all reference the *same commit SHA* and be verified on the PR head.

### Regression coverage

- Empty/malformed quality, invalid unused padding, exact 13-bit validity values, substituted/test/operator-blocked detail flags.
- Generic entry names versus explicit matching SCL signal reference names.
- Publisher TEST flag changes on an existing selected keyed row.
- Multi-publisher identity, retransmission, same-GoID split MAC/VLAN, filter-miss, stale/recovery, 1,000 retransmissions, 500-source bounded churn.
- First-frame baseline versus later observed state-change semantics.

## Manual Windows field acceptance (still required after automated PASS)

At 1366×768, 1586×893 and 1920×1080 (where available) under 100%, 125%, and 150% display scaling: inspect normal/expanded DataSet rows without clipping, confirm chevron click and keyboard toggle, tab through TEST/LIVE/PINNED/STALE indicators, select multiple GOOSE publishers during continuous retransmissions, pin events, click Return to Live, use filter search/GOOSE ID with no selection loss, and observe updates with real or recorded GOOSE traffic. Import a matching SCL and verify genuine signal/quality mappings, then repeat without SCL to confirm raw-only fallback.

**Acceptance boundary:** a successful automated CI build does not prove physical UI readability or IEC 61850 interoperability at a specific field installation. Keep candidate publication and branch promotion separate from this manual sign-off.
