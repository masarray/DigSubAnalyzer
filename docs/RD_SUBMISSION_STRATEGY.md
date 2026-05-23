# R&D Submission Strategy - Raw Passive Process Bus Analyzer

## Goal

Make the project defensible for R&D review as a raw-passive IEC 61850 Process Bus visibility instrument.

The submission should not look like a recycled libiec61850 subscriber demo. It should look like a deliberately reworked raw capture analyzer with clear technical boundaries, honest timing confidence, and repeatable evidence.

## Product Positioning

Use this phrase:

> Raw Passive IEC 61850 Process Bus Visibility Tool with software-timestamp based arrival timing anomaly screening.

Avoid this phrase:

> Precision jitter meter or certified 300 us jitter measurement tool.

The application is useful for visibility, troubleshooting, SV/GOOSE decoding, stream continuity checks, and timing anomaly screening. Measurement-grade microsecond proof requires hardware timestamp validation.

## Phase 1 - Repository Hygiene

Objective: remove ambiguity that the product runtime is still based on libiec61850.

Actions:

1. Product solution must contain only:
   - `ProcessBus.App.Wpf`
   - `ProcessBus.Core`
   - `ProcessBus.Iec61850.Raw`
2. Keep legacy libiec61850 experiments outside the R&D product package.
3. Remove old documentation that describes wrapper/subscriber/callback architecture.
4. Ensure README states that the product WPF app does not reference, load, or call libiec61850.
5. Disable installer generation by default unless the installer script exists.

Definition of done:

- A reviewer opening the solution sees raw product projects only.
- Full-text search in product code does not show subscriber/callback/libiec61850 wording except deliberate historical notes.

## Phase 2 - UI and Evidence Wording

Objective: make the app explain itself clearly during review.

Actions:

1. Use `Raw Passive`, `Passive SV Decoder`, `Raw APDU decoded`, and `Raw frame decoded` wording.
2. Remove old words such as `subscriber` and `callback` from product UI/evidence.
3. Add or keep visible evidence labels:
   - raw engine mode,
   - capture adapter,
   - raw device name,
   - APPID/svID/source MAC,
   - sequence continuity,
   - missing sample count,
   - jitter statistics,
   - timing confidence.
4. Keep waveform wording honest:
   - reconstructed from decoded RMS/phasor and `smpCnt` timing,
   - not hardware-sampled oscilloscope waveform.

Definition of done:

- R&D can understand exactly what is measured, decoded, reconstructed, and only screened.

## Phase 3 - Timing Credibility

Objective: answer the `300 us jitter` criticism without overclaiming.

Actions:

1. Keep the `>=300 us` event detector.
2. Classify timing evidence based on adapter quality:
   - LOW for loopback/virtual/Wi-Fi Direct/wireless,
   - LOW/MEDIUM for unvalidated USB Ethernet,
   - SCREENING for physical Ethernet software timestamp,
   - VALIDATED only after external hardware timestamp proof.
3. In evidence text, separate:
   - sequence continuity (`smpCnt` clean or not),
   - arrival timing anomaly,
   - capture path confidence.
4. Do not blame payload continuity when `smpCnt` is clean but arrival jitter is high.

Definition of done:

- The tool can flag 300 us excursions while clearly saying whether the capture path is suitable for proof.

## Phase 4 - Diagnostic State Machine

Objective: avoid contradictory states such as `NO TRAFFIC` while packets or stale streams exist.

Actions:

1. Use separate states:
   - no live stream,
   - stream stale,
   - raw frames observed but no decoded SV,
   - raw SV active,
   - timing path suspect,
   - sequence/missing sample issue.
2. Avoid `NO TRAFFIC` when historical or current raw packet counters are non-zero.
3. Keep event log historical; keep health cards current.

Definition of done:

- A screenshot does not contradict itself between event log, stream explorer, and health cards.

## Phase 5 - Validation Evidence

Objective: make R&D review evidence repeatable.

Minimum evidence to capture:

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
Current jitter us
Average abs jitter us
Max abs jitter us
Recent jitter >=300 us count
Total jitter >=300 us count
Sequence error count
Missing sample count
Decode reject count
Timing confidence
Interpretation
```

Near-term enhancement:

- Add CSV export for the fields above.
- Add synthetic jitter injection in the bench publisher, but keep it outside the product app.

Definition of done:

- R&D can copy/paste an evidence snapshot and reproduce the interpretation.

## Phase 6 - Demo Setup Rule

Preferred R&D demo setup:

- physical Ethernet adapter,
- external publisher or Omicron CMC/real process bus stream,
- TAP or known Ethernet segment when possible,
- Npcap installed,
- administrator privileges if required.

Avoid for timing demo:

- Microsoft Wi-Fi Direct Virtual Adapter,
- Npcap loopback adapter,
- Hyper-V/VMware virtual NIC,
- wireless adapter,
- unvalidated USB Ethernet dongle.

Definition of done:

- The demo does not depend on a virtual adapter for timing evidence.

## Submission Checklist

Before sending to R&D:

- [ ] Product package opens with raw-only solution/projects.
- [ ] No legacy libiec61850/vendor folders in the submission package.
- [ ] Product code contains no old subscriber/callback wording.
- [ ] README explains raw-passive boundary and timing limitation.
- [ ] UI shows timing confidence and adapter risk.
- [ ] Evidence copy text is clean and professional.
- [ ] Test screenshot uses physical Ethernet or clearly marks virtual adapter as low confidence.
- [ ] Known limitations are stated before R&D finds them.
