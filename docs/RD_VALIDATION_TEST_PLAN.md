# R&D Validation Test Plan - Raw Passive SV/GOOSE Analyzer

## Purpose

Provide a focused validation route for R&D review. The goal is not to certify a timing instrument. The goal is to prove that the analyzer is raw-passive, decodes process-bus frames internally, and reports stream/timing anomalies honestly.

## Test 1 - Product Dependency Boundary

Expected result:

- `ProcessBus.App.Wpf` references only `ProcessBus.Core` and `ProcessBus.Iec61850.Raw`.
- No product project reference to `ProcessBus.Sv.LibIec61850` or vendor wrapper code.
- Product output does not require `iec61850.dll`.

Evidence:

- project file screenshot or source review,
- README statement,
- solution project list.

## Test 2 - Adapter Selection Evidence

Procedure:

1. Open app.
2. Select physical Ethernet adapter.
3. Start capture.
4. Copy R&D evidence.

Expected result:

- evidence contains adapter display name and raw device name,
- timing confidence is not LOW unless adapter is virtual/USB/wireless,
- app warns clearly if adapter is unsuitable.

## Test 3 - SV Stream Decode

Procedure:

1. Feed IEC 61850-9-2LE style SV traffic from Omicron/bench publisher/known source.
2. Observe stream explorer.
3. Select stream.

Expected result:

- stream card shows svID, APPID, and RAW status,
- diagnostics shows raw APDU decoded,
- sequence/missing-sample counters are visible,
- metering/phasor/scope update when mapping is available.

## Test 4 - 300 us Arrival Timing Excursion Screening

Procedure:

1. Use a controlled source capable of introducing delay/jitter, or use a known unstable capture path for screening demonstration.
2. Run capture for at least 10 seconds.
3. Open Diagnostics.
4. Copy evidence.

Expected result:

- `>=300 us` event count increments when excursions occur,
- event log records latest svID/APPID/smpCnt/delta/jitter,
- interpretation differentiates clean `smpCnt` from arrival timing path suspicion.

Acceptance wording:

- Pass as anomaly screening if event is detected and capture confidence is stated.
- Do not treat as measurement-grade proof unless externally validated.

## Test 5 - Stale/No Stream State

Procedure:

1. Start capture while stream is present.
2. Stop source/publisher.
3. Observe UI after stale timeout.

Expected result:

- stream card becomes STALE,
- diagnostics says no live stream or SV stale,
- UI does not claim no traffic if packets were already observed.

## Test 6 - GOOSE Passive Discovery

Procedure:

1. Feed or observe GOOSE traffic.
2. Open GOOSE tab.

Expected result:

- GOOSE traffic appears as passive decoded events,
- no control workflow is exposed,
- state/event history is append-oriented and readable.

## Final R&D Acceptance Criteria

The project is ready for R&D submission when:

- raw product boundary is clear,
- no legacy external IEC 61850 dependency appears in product solution,
- timing/jitter claims are honest and confidence-graded,
- event/evidence copy is repeatable,
- physical Ethernet demo path is prepared,
- known limitations are documented.
