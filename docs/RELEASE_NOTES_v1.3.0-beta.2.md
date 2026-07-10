# Process Bus Insight v1.3.0-beta.2

This stabilization beta focuses on repeatable runtime evidence before the larger v1.4 runtime decomposition. It does not add control or publishing behavior and preserves the receive-only engineering boundary.

## Highlights

- Deterministic eight-stream SV isolation regression coverage
- Exact 2/4/8-cycle scope-window regression coverage
- Explicit 65536 sample-counter rollover coverage in addition to the existing rate-rollover tests
- Duplicate and forward-gap evidence checks while preserving the last coherent scope window
- Waveform and RMS coherence checks from the same selected-stream sample window
- Concurrent frame-observation and snapshot-read stress coverage
- Repeated runtime-stability GitHub Actions gate with downloadable TRX evidence
- Standardized field-evidence record for live capture and replay validation

## Validation intent

The automated stability suite exercises the same `ObserveFrame` and `GetAnalyzerSnapshot` surface used by the live analyzer. It uses deterministic synthetic IEC 61850 SV frames and does not require Npcap, customer captures, or proprietary SCL files.

For a release decision, automated evidence must be combined with a maintained Windows/Npcap smoke test and a documented 30–60 minute soak session using sanitized field or replay sources.

## Important limitations

- This remains a public beta, not a certified protection or timing instrument.
- Windows/Npcap packet timestamps remain screening evidence unless the capture path is independently validated.
- The repeated automated stability gate is deterministic stress evidence, not a replacement for a real-duration field soak test.
- Multi-vendor SCL semantic coverage still requires broader maintained evidence.
- Npcap must be installed separately for live capture.
