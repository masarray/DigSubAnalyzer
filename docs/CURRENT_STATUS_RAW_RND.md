# Current Status - Raw Passive R&D Candidate

## Summary

The application has moved from the early libiec61850 subscriber prototype into a raw-passive WPF analyzer architecture. The product runtime is intended to use Npcap raw capture plus internal SV/GOOSE decoding.

## R&D-Defensible Improvements

- Raw Passive mode is visible in the UI.
- Product WPF project references the raw engine and core model only.
- Diagnostics can report arrival jitter excursions around the 300 us threshold.
- Evidence panel now separates raw decode, sequence continuity, capture path, timing confidence, and interpretation.
- UI wording avoids the old subscriber/callback framing.
- Installer generation is disabled by default to avoid release-build failure when installer script is absent.
- Application shutdown disposes the raw capture source.
- SCL binding matrix now classifies MATCHED, WEAK, MISSING, UNEXPECTED, MISMATCH, and CONFLICT.
- SCL binding rows expose expected-vs-observed evidence for R&D review instead of only a similarity summary.
- GOOSE inspector maps decoded allData values to SCL DataSet entries when a matching SCL GOOSE stream is available.
- Semantic GOOSE rows show signal reference and FC/CDC/bType beside the raw MMS value type.
- SV Advanced engineering evidence shows SCL payload element-to-signal mapping for the matched SCL SV stream.
- Repository is prepared for GPL-3.0-or-later source distribution with license metadata and third-party notices.

## Remaining Limits

- Software timestamp timing is screening-level only.
- Hardware timestamp or external analyzer validation is required for measurement-grade jitter proof.
- Scope waveform is reconstructed from decoded RMS/phasor and timing, not a hardware-sampled trace.
- Phasor/waveform rendering still uses the raw candidate mapping profile until SCL-based SV scaling and quality pairing are validated.

## Recommended Demo Setup

Use a physical Ethernet path and a known SV/GOOSE source. Avoid virtual adapters and loopback for timing discussion.

## Next Engineering Patch

Promote SCL-based SV semantic mapping into the rendering channel mapper after PCAP/live validation, then add CSV/JSON evidence export for the binding matrix.
