# Target-Aware Diagnostics Phase

## Purpose

This phase changes diagnostics from a global warning board into a scoped engineering navigator. In real process bus systems, many SV streams and GOOSE publishers can exist at the same time. A single warning must identify the affected object.

## Implemented Direction

The Diagnostics left rail is now designed as a Traffic Health Navigator:

- SV targets are built from discovered SV streams.
- GOOSE targets are built from observed GOOSE publishers/control blocks.
- PTP target is built from the observed timing reference state.

Selecting a target scopes the diagnostic header and, when possible, selects the corresponding SV stream or GOOSE publisher.

## Current Limitations

- SV per-stream issue summary is available from raw stream state, but the main metrics cards still primarily reflect the selected SV stream diagnostics.
- GOOSE and PTP target pages are not yet fully specialized; they identify scope and issue summary first.
- Findings list still contains raw event text; the next improvement should add target IDs and filtering.

## Next Steps

1. Add target ID to `DiagnosticEventItem`.
2. Filter findings by selected target.
3. Add PTP freshness events: stale/lost/GM changed/domain changed.
4. Add GOOSE supervision events: TTL expiry, sqNum anomaly, confRev change.
5. Convert SV waveform rendering to an explicit smpCnt-aligned display window.
