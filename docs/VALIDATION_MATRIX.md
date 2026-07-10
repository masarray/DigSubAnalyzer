# Validation Matrix — Process Bus Insight

This matrix explains what the application is intended to validate and what remains outside the current product boundary.

| Area | Current validation value | Evidence produced | Boundary |
| --- | --- | --- | --- |
| SV discovery | Detects observed Sampled Values streams from raw Ethernet frames. | APPID, svID, VLAN, MAC, stream status, counters. | Not a relay test set and not a certified measurement instrument. |
| SV continuity | Tracks sequence behavior and missing/unstable sample indicators. | Continuity state, missing count, timing excursion context. | Interpretation depends on capture path quality. |
| SV display | Shows raw decoded sample waveform when channel mapping is available, with RMS, phasor, and shape context for the selected stream. | Coherent selected-stream visual evidence and explicit fallback status when raw rendering is unavailable. | Not a hardware oscilloscope. |
| GOOSE discovery | Detects observed publishers and important header fields. | APPID, source MAC, DataSet, confRev, stNum, sqNum. | Does not publish or control GOOSE. |
| GOOSE value decode | Shows typed dataset values where supported. | Changed values and typed item list. | Complex vendor-specific payloads may need more decoder coverage. |
| PTP context | Detects PTP traffic and timing context where visible. | Transport, message type, domain, GM context where decoded. | Does not discipline clocks or certify time sync. |
| SCL comparison | Compares expected objects with observed traffic. | Matched, weak, missing, unexpected, mismatched, conflicted state. | SCL semantic coverage is being expanded. |
| Evidence copy | Produces readable engineering wording. | Copyable status, expected/observed fields, caution labels. | Final FAT/SAT acceptance remains project-specific. |

## Recommended validation scenarios

1. Known-good SV publisher with expected APPID, VLAN, MAC, svID, and confRev.
2. Wrong APPID or VLAN to confirm mismatch detection.
3. GOOSE publisher with stNum/sqNum changes and typed value changes.
4. SCL file with expected streams and at least one intentionally missing stream.
5. PTP present and PTP absent scenarios to verify timing-confidence wording.
6. Physical NIC/TAP versus virtual/loopback adapter comparison to validate capture-path warnings.

## Evidence quality levels

| Level | Meaning |
| --- | --- |
| Screening | Useful for first diagnosis. Capture path may influence the symptom. |
| High confidence | Physical capture path is credible and evidence is consistent across views. |
| Measurement grade | Requires externally validated timestamping/test equipment. Do not claim this from normal Windows/Npcap timestamps alone. |
