# Phasor SSOT and rendering acceptance — 2026-10-19

## Problem and evidence boundary

The analyzer previously corrected a phase-reference angle a second time in its WPF ViewModel, although the selected-stream raw engine had already resolved relative electrical angles. The plot also placed six text labels independently, so coincident or clustered voltage/current angles collided in a narrow layout. Instantaneous-value changes could cause needless phasor item rebuilds, and a stale publisher could leave its last valid arrows looking live.

This change **does not claim that an injector and receiver have been proven equivalent on the wire**. That requires a synchronized captured packet / injector settings comparison for the same svID, APPID, MAC, mapping, quality, smpCnt, RMS scale, and timestamp.

## New invariants

1. **Single authority**: the raw selected-stream snapshot is the only source of RMS magnitude and relative angle. The ViewModel and WPF control do not synthesize, quantize, hold, or rotate electrical angles.
2. **One selected stream**: measurement, waveform and vectors come from the existing locked selected-stream analyzer snapshot. No new packet decoder, stream cache, worker or UI stream owner is introduced.
3. **Independent channel truth**: current and voltage phases cannot borrow an angle merely because their R/S/T suffix matches. A missing or zero fundamental has no drawable vector.
4. **Responsive without churn**: an RMS or angle change (even a tiny one) invalidates the phasor list. An instantaneous sample changing while RMS/angles remain identical does not create a new six-vector plot list. All pens and grid brushes are frozen and reused, no animation or packet-rate WPF callback is added.
5. **Readable small panels**: voltage vectors use solid strokes, currents use dashed strokes and a separate normalized radius. Labels are packed per side with deterministic ordering and stay in the viewport at supported panel sizes. Voltage and current radii are not directly comparable physical scales.
6. **Freshness visible**: a non-live selected stream is rendered as a dimmed **STALE · last valid samples** hold; it is not a current live phasor estimate.

## Deterministic regression tests

- Existing `LiveCurrentPhasorRegressionTests` checks individually driven Ia/Ib/Ic versus Ua/Ub/Uc, live phase changes and zero-magnitude suppression.
- `PhasorPresentationContractTests` checks exact phasor change detection (no angular rounding), no repaints for instantaneous-only changes, independent Ic/Uc, missing-angle behavior and collision avoidance at multiple narrow panel sizes.

## Physical-bench acceptance (required before claiming injector equivalence)

1. Configure a known synthetic 4I+4V stream on the injector and record the source configuration in the test report.
2. Capture an authorized SV PCAP and note its independent smpCnt/APPID/svID/quality/DataSet mapping; verify the analyzer selected that exact publisher.
3. Compare each of six decoded channel RMS and relative angle values with injector setpoints, allowing only justified rounding and injection/capture uncertainties. Do not infer channel correctness from waveform color or nominal three-phase templates.
4. Change **one** current angle, for example Ic +90° to +45°, without changing Uc +120°. Confirm the analyzer's table and arrows follow the changed stream without an explorer click or an intermediate fabricated phase.
5. Change a voltage magnitude and a current magnitude independently, then stop publishing for more than two seconds. Verify stale status and last-valid-hold labeling.
6. Run a 15-minute live stream on the target Windows machine; record capture packets/s, UI update latency, CPU utilization and heap growth. This change's lightness is architectural; sustained performance remains **unmeasured** until bench results are attached.

## Build gates

On a Windows .NET 8 runner: `scripts/repository-health.ps1`, solution restore/build and all tests. This feature must not be described as wire-validated or performance-certified solely because unit tests pass.
