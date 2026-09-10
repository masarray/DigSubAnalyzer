# Source Addendum — Process Bus Hot Paths and Failure Architecture

The repository root `AGENTS.md` remains authoritative. This scoped addendum applies to code under `src/**` and strengthens failure handling, high-rate processing, diagnostics, and UI handoff rules without changing the product boundary.

## Root-cause circuit breaker

For non-trivial defects use:

REPRODUCE -> TRACE OWNERSHIP -> IDENTIFY ROOT CAUSE -> FIX -> REGRESSION TEST -> PERFORMANCE/FAILURE CHECK.

If three consecutive patches in the same subsystem are still treating symptoms, stop before patch four and re-audit state ownership, data flow, and the demonstrated cause.

Do not create a second stream cache, parser, waveform source, RMS authority, phasor authority, or selected-stream state merely to bypass an existing defect.

## Exception-free high-rate processing

Expected/recoverable failures must not use exceptions as normal control flow in packet receive/decode loops, BER/APDU parsing, SV sample processing, selected-stream snapshot construction, waveform/RMS/phasor analysis, or high-frequency UI handoff.

Use explicit status/result forms appropriate to .NET, such as `TryXxx`, a small typed result record, an enum plus output value, or nullable values only when failure detail is genuinely unnecessary.

Normal conditions such as malformed/truncated frame, unsupported tag, inconsistent length, incomplete sample set, unknown mapping, unavailable optional metadata, queue saturation, or stale stream state should return deterministic status rather than repeatedly throw/catch in the inner loop.

Exceptions from Npcap, filesystem, XML, framework, report/export, or other infrastructure may still occur. Catch them at the nearest meaningful boundary and convert them into the same structured application failure model. Do not scatter broad `catch (Exception)` blocks through per-frame processing.

## Defensive raw parsing

Before any offset/index access based on received data validate:
- minimum frame/header length;
- declared length against available bytes;
- offsets, counts, indexes, and integer arithmetic;
- BER/tag/value boundaries;
- sample count/layout assumptions;
- finite numeric conversion results.

Malformed traffic must not crash the capture loop, selected-stream engine, UI, or report path. Preserve raw evidence whenever semantic decoding is uncertain.

## Bounded async diagnostics

High-rate producers may emit only compact machine-readable diagnostic events/counters. Do not perform per-packet string formatting, file logging, JSON serialization, stack-trace generation, or synchronous UI notification.

Diagnostic transport must be bounded and non-blocking for capture/analysis producers. Under error storms, aggregate/deduplicate/rate-limit repeated events and keep occurrence/drop counters. Queue saturation must have an explicit policy.

Human-readable formatting, persistence, and UI presentation belong on a lower-rate/background consumer. Diagnostic failure must never block packet capture, selected-stream processing, or rendering.

## Selected-stream coherent commit

A new selected-stream snapshot must be built as a candidate and published only when its stream identity and dependent waveform/RMS/phasor/mapping fields are coherent.

Never expose a partially updated cross-stream snapshot. If candidate construction fails, retain the last coherent snapshot while surfacing explicit stale/error state.

## UI handoff and backpressure

Capture rate and UI rate are independent. Never create one WPF render/update per packet or sample.

Use bounded batching/coalescing/sampling according to semantics. Value-like presentation may use latest-state semantics; forensic evidence that must be retained requires an explicit bounded retention/export policy rather than an unbounded ObservableCollection.

The UI thread must not perform packet parsing, SCL/XML parsing, large capture processing, heavy waveform transforms, or blocking I/O.

## Performance discipline

For changes in high-rate paths, measure where relevant:
- packets/s or samples/s;
- dropped/coalesced diagnostic or presentation updates;
- queue high-water mark;
- allocation rate;
- sustained CPU;
- selected-stream snapshot latency;
- waveform/render latency;
- memory growth during long captures.

Do not add caches, workers, pools, or locks merely because they are generic performance patterns. Identify the bottleneck and preserve deterministic ownership first.

## Definition of done

A source change affecting capture, parsing, selected-stream state, analysis, or rendering is not complete until the exact failure mode is covered by a regression test where practical and the repository health/build/test gates pass. Performance-sensitive changes also require before/after evidence or a documented reason measurement is not applicable.
