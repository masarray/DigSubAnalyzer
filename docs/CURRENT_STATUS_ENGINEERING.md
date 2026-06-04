# Current Status - Engineering Product

## Summary

Process Bus Insight is a raw-passive Windows engineering tool for IEC 61850 Process Bus visibility and validation. The product runtime uses Npcap raw capture plus internal SV, GOOSE, PTP, and SCL-aware decoding logic.

The application is positioned as a public portfolio project and a practical contribution to substation automation engineering: observe live process-bus traffic, understand its engineering context, validate it against SCL, and produce defensible evidence.

## Product Strengths

- Raw Passive mode is visible in the UI.
- Product WPF project references the raw engine and core model only.
- No product dependency on libIEC61850, MZ Automation libraries, or external IEC 61850 subscriber stacks.
- Diagnostics can report packet-arrival timing excursions around the 300 us threshold.
- Evidence panels separate raw decode, sequence continuity, capture path, timing confidence, and interpretation.
- UI wording avoids subscriber/control workflow framing.
- Application shutdown disposes the raw capture source.
- SCL binding matrix classifies MATCHED, WEAK, MISSING, UNEXPECTED, MISMATCH, and CONFLICT.
- SCL binding rows expose expected-vs-observed evidence instead of only a similarity summary.
- GOOSE inspector maps decoded allData values to SCL DataSet entries when a matching SCL GOOSE stream is available.
- Semantic GOOSE rows show signal reference and FC/CDC/bType beside the raw MMS value type.
- SV Advanced engineering evidence shows SCL payload element-to-signal mapping for the matched SCL SV stream.
- Repository is prepared for Apache-2.0 source distribution with license metadata and third-party notices.

## Remaining Limits

- Software timestamp timing is screening-level only.
- Hardware timestamp or external analyzer validation is required for measurement-grade jitter proof.
- Scope waveform is reconstructed from decoded RMS/phasor and timing, not a hardware-sampled trace.
- SCL-based SV scaling and quality pairing still need broader multi-vendor validation.

## Recommended Field Setup

Use a physical Ethernet path and a known SV/GOOSE source. Avoid virtual adapters and loopback for timing claims. When a virtual or wireless adapter is used, keep timing confidence low and describe the result as packet-arrival screening only.

## Next Engineering Patch

Promote SCL-based SV semantic mapping into the rendering channel mapper after PCAP/live validation, then add CSV/JSON evidence export for the binding matrix.
