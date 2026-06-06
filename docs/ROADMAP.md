# Roadmap — Process Bus Insight

Process Bus Insight is moving toward a practical field instrument for IEC 61850 Process Bus visibility and FAT/SAT evidence.

## Current focus

- Strong raw-passive SV, GOOSE, and PTP visibility.
- Target-aware diagnostics for streams, publishers, timing sources, and capture path.
- SCL expected-vs-observed validation.
- Evidence wording that is useful but does not overclaim timing accuracy.
- Release packaging that lets users try the app without Visual Studio.

## Planned improvements

### 1. Diagnostics maturity

- Traffic Health Navigator for SV, GOOSE, and PTP targets.
- Clear issue ownership: stream, publisher, timing source, or capture path.
- Better adapter confidence warnings.

### 2. Stable instrument rendering

- Strong stream key selection.
- More stable waveform/phasor update behavior during bad frames.
- Better handling of duplicate, out-of-order, and sequence-jump traffic.

### 3. PTP-aware timing interpretation

- Freshness states: never observed, live, stale, lost.
- Grandmaster/domain change warnings.
- Clock class interpretation.
- Clear timestamp source and timing confidence propagation.

### 4. GOOSE inspector maturity

- Better value labels for breaker position, trip, alarm, and interlock signals.
- More complete quality interpretation.
- Cleaner event filtering and publisher health state.

### 5. Evidence and reporting

- Exportable FAT/SAT evidence report.
- Screenshot-friendly diagnostic summaries.
- Repeatable validation scenarios and sample captures where legally shareable.

## Non-goals

- Not a relay test set.
- Not a control client.
- Not a PTP grandmaster.
- Not a certified timing measurement instrument without validated timestamp hardware.
