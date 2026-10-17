# Operator UX release-candidate acceptance — 2026-10-17

## Source and scope

Base: `main@232015b4b0d8986e11af313ad31fdb7d8b35ca90`.
Candidate work: `fix/final-premium-ux-20261017`.

This change is **read-only presentation and binding hardening**. No SV/GOOSE/PTP wire decoder, raw capture path, SCL authority, measurement algorithm, or waveform sample interpolation has been changed. Real PTP lock, physical timing accuracy, and calibrated electrical units remain unverified unless independently substantiated.

## Problem → root cause → implementation → acceptance

| Problem | Root cause | Implementation | Acceptance |
| --- | --- | --- | --- |
| PTP matched two-step headline 0 while evidence count advances | `Diagnostics` object replacement did not notify bindings in normal Diagnostics/Timing refresh; derived `PtpTwoStepEvidenceText` was notified | Notify `Diagnostics` in both scoped UI refreshes | Headline and passive evidence read the same snapshot on every refresh |
| Six-phase metering cramped with RMS and unit as one string | 3-column fixed-angle ledger, ad-hoc row styling and generic formatted strings | Four-column phase/RMS/unit/angle ledger with shared WPF styles, invariant numeric formatting, group readability and more height for meter | At 1366×768 all phases legible with no numeric overlap |
| Uncalibrated/unknown or non-finite numeric metadata could appear as a physical measurement | Value formatter inferred V/A for unspecified units and did not reject non-finite values | Display only source-provided units; render unavailable/nonfinite as N/A; keep counts separate from calibrated units | Unit authority never invented; no NaN/Infinity shown as valid |
| Diagnostics findings become a text wall | Long `Message` rendered directly for each event; list virtualization not explicit; header incorrectly implied target filtering | Bounded existing evidence retained, concise summary/source columns, recycling virtualization, selected-event full evidence under expander; label all-source collection accurately | Short rows with full technical evidence available on selection |
| Metering difficult to see below phasor at reduced window height | Phasor minimum height consumed the constrained right column | Rebalance phasor/meter height while preserving diagram | No key phase values hidden at 1366×768 |

## Automated and manual gates

The repository's existing CI must confirm Release build, xUnit regression suite, runtime stability, CodeQL and dependency review. Windows candidate packaging must confirm ZIP publish, verification, checksum/manifest and artifact upload. New model regression tests cover raw count formatting, scaled engineering units, non-finite/unverified readings and diagnostic evidence preservation.

**Manual visual QA required** at 1366×768 (100%), 1600×900 (100%), 1920×1080 (125%), and 150% Windows DPI if possible, including resized windows; zero/one/many streams; uncalibrated/explicit scaling; invalid quality; PTP observed/absent; and rapid selected-stream switching. No pixel-level GUI pass can be inferred from CI alone.

**Physical acceptance remains OPEN** under issue #30: VLAN TAP/SPAN, 60-minute SV/PTP hardware soak with measured drops/CPU/GC, independent IED receiver, actual clock discipline evidence. Passing CI/package is only **Software Release Candidate Ready for Physical Acceptance**, not a certified or field-accepted release.
