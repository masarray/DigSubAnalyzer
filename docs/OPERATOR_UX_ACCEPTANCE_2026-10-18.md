# Operator UX upgrade — engineering acceptance (2026-10-18)

Source baseline: `main@4cf439eaa2de1a53cca752b5af4428d5a212d6fd`.

## Operator tasks covered

1. **Analyzer** — select one publisher, see its actual sampled signal, scaled 4I+4V assumptions and phasor/RMS. The default scope now prefers the positive-going **Ua zero crossing** from an unbroken chronological window of raw decoded SV sample points. No synthetic sinusoid, per-channel phase shift, or value interpolation is permitted. On startup or when Ua is unavailable, it falls back to the prior slot-locked view. The rising crossing is a display alignment, **not a timestamp or clock-lock measurement**. Displayed values may lag up to one nominal cycle versus arrival when waiting for the most recent complete aligned window.
2. **Diagnostics** — retain current jitter, sample-counter jumps, missing samples, separate quality/sync, and detailed event evidence. Add one contextual, evidence-only operator next check instead of speculative root-cause detection. No new packet-thread text processing.
3. **GOOSE** — empty timeline describes exactly *no events in the selected filtered view*. Do not imply the publisher is offline. Make capture-adapter, VLAN mirror, publisher state, filtering and retransmission checks discoverable.
4. **Timing/PTP** — size factual metrics to content; reclaim remaining height for existing PTP event history. PTP presence remains distinct from verified clock lock.
5. **SCL** — show a concise selected identity verdict with the original expected/observed attributes and evidence under an expandable detail. The candidate similarity score is **not a probability**.

## Visual QA still required on actual Windows WPF

- 1366×768 at 100%, 1600×900 at 100%, 1920×1080 at 125%, 150% DPI and minimum supported window.
- No publisher / one / many / stale and fast stream switching; 4I+4V auto nominal versus operator raw override.
- Zero-cross start of Ua with offset sample counters, distortion, zero-amplitude Ua, counter rollover and missing samples. At the first complete window the legacy slot-locked fallback is expected until enough additional coherent samples arrive.
- GOOSE zero events and filter-empty state, event arrival transition, selection, retransmission on/off.
- SCL matched/mismatch/missing/unexpected and missing fields; open the full evidence expander.
- Long-duration WPF redraw, UI responsiveness, memory and CPU: no new high-rate callback or WPF animation was added. Real CPU/memory/GC numbers cannot be claimed without a Windows bench runner.
- The existing CI, runtime stability, CodeQL, dependency audit and candidate ZIP gates are necessary but do not replace physical acceptance issue #30.

## Safety boundary

Receive-only, raw-passive IEC 61850 SV / GOOSE / PTP. No publisher controls, synchronizing, protection operate, or subscriber acknowledgement. Software/Npcap arrival timestamps are screening evidence only, not certified timing.
