# Public Portfolio Strategy - Process Bus Insight

## Goal

Position Process Bus Insight as a mature, raw-passive IEC 61850 Process Bus engineering tool for public portfolio use, field learning, commissioning support, interoperability troubleshooting, and substation automation education.

The product should look like an intentionally engineered analyzer with clear technical boundaries, honest timing confidence, readable UX, and repeatable evidence. It must not look like a recycled subscriber example or a clone of any third-party IEC 61850 stack.

## Product Positioning

Use this phrase:

> Raw-passive IEC 61850 Process Bus Insight tool with SCL-aware SV, GOOSE, and PTP visibility.

Avoid these phrases:

> Precision jitter meter.

> Certified 300 us jitter measurement tool.

> IEC 61850 client/server stack.

The application is useful for visibility, troubleshooting, SV/GOOSE decoding, stream continuity checks, SCL binding, and timing anomaly screening. Measurement-grade microsecond proof requires hardware timestamp validation.

## Repository Hygiene

Objective: keep the product boundary clear and defensible.

Actions:

1. Product solution must contain only:
   - `ProcessBus.App.Wpf`
   - `ProcessBus.Core`
   - `ProcessBus.Iec61850.Raw`
2. Keep old external-library experiments outside the product package.
3. Remove documentation that describes wrapper/subscriber/callback architecture as the product path.
4. Ensure README states that the product WPF app does not reference, load, or call libIEC61850.
5. Keep third-party notices clear: Npcap is a runtime prerequisite and is not vendored.

Definition of done:

- A reviewer opening the solution sees raw product projects only.
- Full-text search in product code does not show vendor-library or subscriber/callback wording except deliberate boundary statements.

## UI and Evidence Wording

Objective: make the app understandable to engineers and new users.

Actions:

1. Use `Raw Passive`, `Passive SV Decoder`, `Raw APDU decoded`, and `Raw frame decoded` wording.
2. Avoid old words such as `subscriber` and `callback` in product UI/evidence.
3. Keep visible evidence labels:
   - raw engine mode,
   - capture adapter,
   - raw device name,
   - APPID/svID/source MAC,
   - sequence continuity,
   - missing sample count,
   - arrival timing statistics,
   - timing confidence.
4. Keep waveform wording honest:
   - reconstructed from decoded RMS/phasor and `smpCnt` timing,
   - not a hardware-sampled oscilloscope trace.

Definition of done:

- A new user can understand what is measured, decoded, reconstructed, and only screened.

## Timing Credibility

Objective: answer timing questions without overclaiming.

Actions:

1. Keep the `>=300 us` arrival excursion detector.
2. Classify timing evidence based on adapter quality:
   - LOW for loopback/virtual/Wi-Fi Direct/wireless,
   - LOW/MEDIUM for unvalidated USB Ethernet,
   - SCREENING for physical Ethernet software timestamp,
   - VALIDATED only after external hardware timestamp proof.
3. In evidence text, separate:
   - sequence continuity (`smpCnt` clean or not),
   - arrival timing anomaly,
   - capture path confidence.
4. Do not blame SV payload continuity when `smpCnt` is clean but arrival timing is unstable.

Definition of done:

- The tool can flag 300 us excursions while clearly saying whether the capture path is suitable for proof.

## Validation Evidence

Minimum evidence fields:

```text
Timestamp
Adapter display name
Adapter raw device name
APPID
svID
Source MAC
Destination MAC
VLAN
smpCnt
Expected interval us
Current delta us
Current arrival variation us
Average abs arrival variation us
Max abs arrival variation us
Recent >=300 us arrival excursion count
Total >=300 us arrival excursion count
Sequence error count
Missing sample count
Decode reject count
Timing confidence
Interpretation
```

Near-term enhancement:

- Add CSV export for the fields above.
- Add PCAP playback for repeatable support and portfolio walkthroughs.

Definition of done:

- A user can copy/paste an evidence snapshot and reproduce the interpretation.

## Public Portfolio Checklist

- [ ] Product package opens with raw-only solution/projects.
- [ ] No legacy libIEC61850/vendor folders in the product package.
- [ ] Product code contains no old subscriber/callback wording.
- [ ] README explains raw-passive boundary and timing limitation.
- [ ] UI shows timing confidence and adapter risk.
- [ ] Evidence text is clean and professional.
- [ ] Example screenshot uses physical Ethernet or clearly marks virtual adapter as low confidence.
- [ ] Known limitations are stated before users encounter them.
