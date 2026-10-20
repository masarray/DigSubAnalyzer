# Calm Engineering Polish — frozen baseline and acceptance (20 October 2026)

## Immutable pre-polish checkpoint

- Repository: `masarray/DigSubAnalyzer`
- Preserved branch: `checkpoint/pre-calm-engineering-polish-20261020`
- Exact pre-polish commit: `03fc3657f2ba21df2bc570d2198c59bb2f2be2dc`
- At checkpoint time: CI, Runtime Architecture, Runtime Stability, CodeQL, Dependency Security Review and Release Candidate Package **all passed**.
- Active development PR: #47 (`fix/phasor-ssot-responsive-lightweight-20261019`); do **not** merge to main or publish stable based solely on visual/CI evidence.
- Must preserve: 8-DIP interpanel geometry, baseline phasor exact-tip callouts, channel-specific HELD/MISSING, selected-stream SSOT, honest Npcap/PTP evidence, Inno upgrade/repair paths.

## Scoped calm polish

1. **Diagnostics:** bounded, exact-message grouping across up to 200 original capture events; grouped count and latest/earliest timestamp as presentation only. Original `AnalyzerShellState.Events` is intact. Incremental ListView updates minimize flicker.
2. **Timing interpretation:** signed host/Npcap interarrival variation is called `ARRIVAL Δ · HOST`; smpCnt-derived missing sample gaps are explicitly **inferred**, not independently confirmed packet loss.
3. **Waveforms:** green static `RX` indicator means packet reception, not clean SV integrity. No pulsing storyboard or extra timer. Existing amber `LIVE · WARN` is authoritative for stream health.
4. **Analyzer controls:** electrical U/I scale controls receive adequate width and full-value tooltips, without enlarging panels or recomputing samples.
5. **PTP:** fit protocol transport text and preserve event identities, timestamps, clock evidence and source selection.
6. **GOOSE:** correct no-publisher vs filtered-no-event empty state, preserving state-change-only default and observable bindings.
7. **SCL:** readable compact monospace engineering references with full-reference hover text.

## Quality gates

- [ ] Exact latest SHA Windows build and entire xUnit suite green
- [ ] Existing WPF layout geometry regression remains green
- [ ] Positive/negative event aggregation tests (same messages coalesce; different source, severity or engineering values remain separate; bounded at 200)
- [ ] Runtime Architecture and Runtime Stability green
- [ ] CodeQL and Dependency Security Review green
- [ ] Exact SHA Inno installer built; install/repair/uninstall and executable provenance/hash verification green
- [ ] User field screenshots at 100%, 125% and 150% DPI, with capture running and empty states
- [ ] Physical 60-minute target NIC/FAT gate remains separate; no stable publication without it

No visual screenshot or CI can establish measurement calibration, end-device subscriber state, precise PTP clock lock, or actual packet loss attribution from host-only timestamps.
