# Stable Workspace UX + Per-Stream Mapping Audit Phase

## Purpose

This phase stabilizes live multi-stream user experience so Process Bus Insight behaves like an instrument, not a live-sorted debug dashboard.

## Main Rules

- Explorer cards stay in first-seen order by default.
- Live `LastSeen`, packet counters, and warning status must not reorder cards.
- Selection is locked by strong stream/publisher/timing key.
- Target changes may show a short glass transition overlay.
- Normal live refresh must not show loading or flicker.

## Implemented Items

- SV streams are emitted from the raw analyzer in `FirstSeenOrder`.
- Shell state updates existing stream items instead of reordering by last seen.
- Diagnostics target navigator now uses stable ordering and sync-by-key updates.
- Advanced left rail acts as a raw target explorer for SV / GOOSE / PTP.
- Advanced center evidence changes according to selected target.
- SV stream details now include candidate phase order and channel angle evidence.
- GOOSE event table spacing and publisher column readability were refined.

## Still Required

- True SCL mapping for semantic signal names.
- User-selectable sort modes: First seen / Severity / Last seen / Name.
- Per-stream custom mapping override: Auto / ABC / ACB / Custom.
- smpCnt-aligned waveform renderer using real sample windows rather than reconstructed sine only.
- PCAP playback for repeatable debug.
