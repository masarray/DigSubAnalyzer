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

## Remaining Limits

- Software timestamp timing is screening-level only.
- Hardware timestamp or external analyzer validation is required for measurement-grade jitter proof.
- Scope waveform is reconstructed from decoded RMS/phasor and timing, not a hardware-sampled trace.
- Channel mapping is still profile-based and not a complete SCL semantic mapper.

## Recommended Demo Setup

Use a physical Ethernet path and a known SV/GOOSE source. Avoid virtual adapters and loopback for timing discussion.

## Next Engineering Patch

Add CSV evidence export for jitter/sequence diagnostics and prepare a controlled synthetic jitter injection test in a separate bench tool.
