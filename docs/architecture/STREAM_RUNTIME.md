# SV Stream Runtime Architecture

## Purpose

The SV analyzer must remain stable under multiple simultaneous publishers, sample-counter rollover, packet loss, publisher restart, UI refresh, and user selection changes. The central rule is that every displayed engineering value belongs to one selected stream and one coherent runtime snapshot.

## Strong stream identity

A runtime stream key should use the strongest observed identity available, normally a combination of:

- source MAC
- destination multicast MAC
- APPID
- svID
- VLAN ID/priority when present

SCL metadata may enrich or validate this identity, but subscriber expectations must not be mistaken for additional live publishers.

## Per-stream ownership

Each stream runtime owns:

- latest protocol metadata
- sequence/rollover state
- coherent decoded sample points
- waveform window state
- RMS and phasor calculation inputs
- shape/distortion analysis state
- immutable latest snapshot

No shared global buffer may be used to render values for different streams.

## Snapshot contract

A selected-stream snapshot is atomic from the UI perspective:

```text
SelectedStreamSnapshot
  ├── stream identity and freshness
  ├── waveform points/window
  ├── RMS values
  ├── phasor vectors
  ├── sequence and timing evidence
  └── shape/distortion result
```

Waveform, RMS, and phasor should be derived from clearly related sample windows. A fast shape detector may use a shorter latest-sample tail, but its source stream and sample freshness must remain explicit.

## Scope behavior

- Visual scope windows use fixed sample counts for 2, 4, or 8 cycles.
- Partial arrays are not stretched across the complete plot width.
- Raw samples are preferred over reconstructed waveforms.
- Changing timebase changes the engine window, not only the label.
- Refreshing the UI must not require clicking the stream explorer.
- Rendering must not animate/tween engineering values between snapshots.

## Sequence and rollover

A healthy 9-2LE publisher may wrap `smpCnt` at the configured sample rate rather than 65536. Sequence tracking must recognize the applicable rollover, distinguish duplicate/missing/out-of-order samples, and avoid treating a normal rollover as a reason to freeze live values.

## UI selection

The SV explorer is the user-facing owner of selected-stream state. If the selected item is replaced during list refresh, selection is restored by strong stream key. Diagnostics may inspect a target but must not silently override the analyzer workspace selection.

## Validation requirements

Changes to this area require regression coverage for:

- startup auto-selection
- multiple live SV streams
- independent amplitude, angle, frequency, and harmonic changes
- sample-counter rollover and publisher restart
- 2/4/8-cycle windows
- no value/N/A flicker
- no cross-stream waveform/phasor/RMS contamination
- no click-required refresh
